"""多張圖逐格動畫模式的測試(mock 編輯器 + 假的 diffusers,不需要 GPU)。"""
import sys
import types
from pathlib import Path

import pytest
from PIL import Image

from animegen.cli import main
from animegen.config import load_config
from animegen.device import DeviceInfo
from animegen.editors import EditRequest, choose_editor
from animegen.editors.kontext import KontextEditor
from animegen.editors.qwen import QwenEditor
from animegen.keyframes import KeyframeAnimator, assemble_gif, build_edit_prompt
from animegen.poses import match_template, plan_poses, split_manual


@pytest.fixture
def character(tmp_path: Path) -> Path:
    img = Image.new("RGBA", (300, 400), (0, 0, 0, 0))
    img.paste((60, 120, 220, 255), (100, 80, 200, 360))
    path = tmp_path / "char.png"
    img.save(path)
    return path


@pytest.fixture
def anim() -> KeyframeAnimator:
    cfg = load_config()
    cfg["keyframe"]["editor"] = "mock"
    cfg["keyframe"]["inbetween"] = 0
    return KeyframeAnimator(cfg)


# ---- 姿勢拆解 -------------------------------------------------------------------------
@pytest.mark.parametrize("action,expected", [
    ("揮手打招呼", "wave"), ("開心地跳起來", "jump"), ("跳舞", "dance"), ("轉身一圈", "spin"),
    ("鞠躬", "bow"), ("轉一圈", "spin"), ("waves hello", "wave"), ("揮手然後跳起來", "wave"), ("吃拉麵", None),
])
def test_match_template(action, expected):
    assert match_template(action) == expected


def test_manual_poses():
    assert split_manual("舉起右手\n右手揮向左\n右手揮向右") == ["舉起右手", "右手揮向左", "右手揮向右"]
    assert split_manual("raise hand -> wave left -> wave right") == ["raise hand", "wave left", "wave right"]
    assert split_manual("揮手打招呼") is None


def test_generic_poses_count():
    poses, source = plan_poses("吃拉麵", 5)
    assert len(poses) == 5 and source.startswith("通用")
    assert len({p.zh for p in poses}) == 5  # 每格描述都不同,才不會畫出重複的格子


def test_prompt_language():
    assert "只改變姿勢:舉手" in build_edit_prompt("舉手", "zh")
    en = build_edit_prompt("raising hand", "en", "silver hair girl")
    assert "Only change the pose: raising hand" in en and "silver hair girl" in en


# ---- 生成流程 -------------------------------------------------------------------------
def test_generate_wave(anim, character, tmp_path):
    result = anim.generate(character, "揮手打招呼", seed=3, output_dir=tmp_path)
    assert result.editor == "mock" and result.seed == 3
    assert result.poses[0] == "(原圖)"
    assert len(result.keyframes) == 6  # 原圖 + 5 個範本姿勢
    assert all(k.size == (300, 400) for k in result.keyframes)
    with Image.open(result.gif) as gif:
        assert gif.n_frames == 6
    assert len(list(result.frames_dir.glob("frame_*.png"))) == 6
    assert result.warnings  # mock 會提醒


def test_custom_poses_without_original(anim, character, tmp_path):
    result = anim.generate(character, "", poses=["舉起右手", "右手揮向左"], include_original=False,
                           output_dir=tmp_path)
    assert result.poses == ["舉起右手", "右手揮向左"]
    assert result.pose_source == "自訂姿勢"


def test_redraw_and_reassemble(anim, character, tmp_path):
    result = anim.generate(character, "點頭", seed=1, output_dir=tmp_path)
    before = result.keyframes[2].tobytes()
    anim.redraw(result, 2, pose="頭往右歪", seed=99)
    assert result.poses[2] == "頭往右歪"
    assert result.keyframes[2].tobytes() != before
    with pytest.raises(ValueError):
        anim.redraw(result, 0)  # 原圖不能重畫
    result.settings.update(tweens=2, pingpong=True)
    anim.save(result)
    with Image.open(result.gif) as gif:
        n = len(result.keyframes)
        assert gif.n_frames == (2 * n - 2) * 3


def test_assemble_gif_durations(tmp_path):
    frames = [Image.new("RGB", (40, 30), c) for c in ("red", "green", "blue")]
    path = assemble_gif(frames, tmp_path / "a.gif", frame_ms=250, max_width=None)
    with Image.open(path) as gif:
        assert gif.n_frames == 3 and gif.info["duration"] == 250


def test_cli_frames(character, tmp_path, capsys):
    rc = main(["frames", "-i", str(character), "-a", "鞠躬", "-m", "mock", "-o", str(tmp_path), "--seed", "4"])
    assert rc == 0
    assert list(tmp_path.glob("*/animation.gif"))
    assert "內建範本:bow" in capsys.readouterr().out


# ---- 依顯卡選模型 / 量化 --------------------------------------------------------------
def gpu(vram: float) -> DeviceInfo:
    return DeviceInfo("cuda", "Fake GPU", vram, True)


@pytest.mark.parametrize("vram,editor", [(24, "qwen"), (16, "qwen"), (12, "kontext"), (8, "kontext")])
def test_choose_editor(vram, editor):
    assert choose_editor(gpu(vram))[0] == editor


def test_choose_editor_without_gpu():
    assert choose_editor(DeviceInfo("none", "", 0, False))[0] == "mock"
    assert choose_editor(DeviceInfo("cpu", "CPU", 0, True))[0] == "mock"


@pytest.mark.parametrize("cls,vram,quant,mode", [
    (QwenEditor, 16, "4bit", "model_offload"),
    (QwenEditor, 80, "none", "full"),
    (KontextEditor, 12, "4bit", "model_offload"),
    (KontextEditor, 32, "none", "model_offload"),
    (KontextEditor, 48, "none", "full"),
])
def test_memory_resolution(cls, vram, quant, mode):
    ed = cls({}, gpu(vram))
    assert (ed.quantize, ed.memory_mode) == (quant, mode)


# ---- 假的 diffusers:確認呼叫參數正確 -------------------------------------------------
class FakePipe:
    calls: list = []

    @classmethod
    def from_pretrained(cls, model_id, **kwargs):
        pipe = cls()
        pipe.model_id, pipe.kwargs = model_id, kwargs
        return pipe

    def load_lora_weights(self, repo, weight_name=None, **kwargs):
        self.lora = (repo, weight_name)

    def enable_model_cpu_offload(self, device=None):
        self.placed = ("offload", device)

    def to(self, device):
        self.placed = ("full", device)
        return self

    def __call__(self, **kwargs):
        FakePipe.calls.append(kwargs)
        img = kwargs["image"][0] if isinstance(kwargs["image"], list) else kwargs["image"]
        return types.SimpleNamespace(images=[img.resize((img.width + 8, img.height))])


@pytest.fixture
def fake_diffusers(monkeypatch):
    torch = types.ModuleType("torch")
    torch.bfloat16, torch.float32 = "bf16", "fp32"
    torch.Generator = lambda device: types.SimpleNamespace(manual_seed=lambda s: ("gen", s))
    torch.cuda = types.SimpleNamespace(is_available=lambda: False)
    diffusers = types.ModuleType("diffusers")
    diffusers.QwenImageEditPlusPipeline = FakePipe
    diffusers.FluxKontextPipeline = FakePipe
    diffusers.FlowMatchEulerDiscreteScheduler = types.SimpleNamespace(from_config=lambda c: ("sched", c))
    quantizers = types.ModuleType("diffusers.quantizers")
    quantizers.PipelineQuantizationConfig = lambda **kw: ("quant", kw)
    monkeypatch.setitem(sys.modules, "torch", torch)
    monkeypatch.setitem(sys.modules, "diffusers", diffusers)
    monkeypatch.setitem(sys.modules, "diffusers.quantizers", quantizers)
    monkeypatch.setitem(sys.modules, "bitsandbytes", types.ModuleType("bitsandbytes"))
    FakePipe.calls = []
    return FakePipe


def test_qwen_editor_call(fake_diffusers):
    ed = QwenEditor({}, gpu(16))
    img = Image.new("RGB", (300, 400), "white")
    out = ed.edit(EditRequest(image=img, prompt="pose", seed=7, steps=12, max_area=768 * 768))
    assert ed.pipe.model_id == "Qwen/Qwen-Image-Edit-2509"
    quant = ed.pipe.kwargs["quantization_config"][1]
    assert quant["quant_backend"] == "bitsandbytes_4bit"
    assert quant["components_to_quantize"] == ["transformer", "text_encoder"]
    assert ed.pipe.placed == ("offload", "cuda")
    call = fake_diffusers.calls[-1]
    assert call["num_inference_steps"] == 12 and call["true_cfg_scale"] == 4.0
    assert call["width"] % 16 == 0 and call["height"] % 16 == 0
    assert call["width"] * call["height"] <= 768 * 768 * 1.05
    assert out.mode == "RGB"


def test_kontext_editor_call(fake_diffusers):
    ed = KontextEditor({}, gpu(48))
    ed.edit(EditRequest(image=Image.new("RGB", (64, 64)), prompt="pose", seed=1, steps=28, max_area=1024 ** 2))
    assert "quantization_config" not in ed.pipe.kwargs
    assert ed.pipe.placed == ("full", "cuda")
    call = fake_diffusers.calls[-1]
    assert call["guidance_scale"] == 2.5 and call["max_area"] == 1024 ** 2


def test_animator_with_fake_qwen(fake_diffusers, character, tmp_path):
    cfg = load_config()
    a = KeyframeAnimator(cfg, device=gpu(24))
    result = a.generate(character, "鞠躬", seed=5, inbetween=0, fast=False, output_dir=tmp_path)
    assert result.editor == "qwen"
    assert all(k.size == (300, 400) for k in result.keyframes)  # 輸出尺寸統一回原圖大小
    assert all(c["generator"] == ("gen", 5) for c in fake_diffusers.calls)  # 每格同一個 seed
    assert "只改變姿勢" in fake_diffusers.calls[0]["prompt"]


# ---- 補間(用時間換連續性) ----------------------------------------------------------
@pytest.mark.parametrize("levels,pingpong,expected_frames", [
    (0, False, 6),        # 原圖 + 5 個揮手姿勢
    (1, False, 6 + 6),    # 6 個間隔(含最後一格回到第一格)各補 1 格
    (2, False, 6 + 18),   # 各補 3 格
    (1, True, 6 + 5),     # 來回播放不需要補「最後 → 第一格」
])
def test_inbetween_counts(anim, character, tmp_path, levels, pingpong, expected_frames):
    result = anim.generate(character, "揮手", seed=1, inbetween=levels, pingpong=pingpong, output_dir=tmp_path)
    assert len(result.keyframes) == expected_frames
    assert result.kinds.count("inbetween") == expected_frames - 6
    assert result.kinds[0] == "original"
    assert result.settings["frame_ms"] == max(50, round(180 / 2 ** levels))


def test_inbetween_order(anim, character, tmp_path):
    result = anim.generate(character, "", poses=["A", "B"], seed=1, inbetween=2, output_dir=tmp_path)
    labels = [p.split(":")[0] for p in result.poses]
    assert labels[:5] == ["(原圖)", "補間 25%", "補間 50%", "補間 75%", "A"]
    # mock 的補間格 = 前後兩格的平均,所以 50% 那格應該在原圖與 A 之間
    assert all(k.size == (300, 400) for k in result.keyframes)


def test_redraw_inbetween_uses_neighbours(anim, character, tmp_path):
    result = anim.generate(character, "點頭", seed=1, inbetween=1, output_dir=tmp_path)
    i = result.kinds.index("inbetween")
    anim.redraw(result, i, seed=3)
    assert result.kinds[i] == "inbetween"  # 沒給新描述 → 還是補間格
    anim.redraw(result, i, pose="頭往右歪")
    assert result.kinds[i] == "key" and result.poses[i] == "頭往右歪"


def test_qwen_fast_inbetween(fake_diffusers, character, tmp_path):
    a = KeyframeAnimator(load_config(), device=gpu(24))
    result = a.generate(character, "", poses=["舉起右手"], seed=2, inbetween=1, fast=True, output_dir=tmp_path)
    ed = a._editor
    assert ed.fast and ed.pipe.lora[0] == "lightx2v/Qwen-Image-Lightning"
    assert "Edit-2509" in ed.pipe.lora[1] and ed.pipe.kwargs["scheduler"][0] == "sched"
    calls = fake_diffusers.calls
    assert all(c["num_inference_steps"] == 8 and c["true_cfg_scale"] == 1.0 for c in calls)
    between = [c for c in calls if len(c["image"]) == 3]  # 原圖 + 前一格 + 後一格
    assert len(between) == 1 and "Picture 2" in between[0]["prompt"]
    assert len(result.keyframes) == 3  # 原圖、補間、舉起右手(只有 2 格時不補回頭的間隔)


def test_kontext_inbetween_falls_back_to_text(fake_diffusers, character, tmp_path):
    a = KeyframeAnimator(load_config(), device=gpu(12))
    a.generate(character, "", poses=["raise hand", "wave"], seed=2, inbetween=1, editor="kontext",
               output_dir=tmp_path)
    prompts = [c["prompt"] for c in fake_diffusers.calls]
    assert any("halfway between" in p for p in prompts)
    assert all(not isinstance(c["image"], list) for c in fake_diffusers.calls)
