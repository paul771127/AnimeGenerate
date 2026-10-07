"""用 mock 後端測試整條流程(不需要 GPU / torch)。"""
from pathlib import Path

import pytest
from PIL import Image

from animegen.backends import BACKENDS, get_spec
from animegen.backends.mock import classify_motion
from animegen.cli import main
from animegen.config import load_config
from animegen.pipeline import AnimeGenerator
from animegen.prompt import build_prompt


@pytest.fixture
def character(tmp_path: Path) -> Path:
    img = Image.new("RGBA", (320, 480), (0, 0, 0, 0))
    img.paste((220, 80, 80, 255), (110, 120, 210, 400))
    path = tmp_path / "char.png"
    img.save(path)
    return path


@pytest.fixture
def gen() -> AnimeGenerator:
    cfg = load_config()
    cfg["backend"] = "mock"
    return AnimeGenerator(cfg)


def test_generate_gif_and_mp4(gen, character, tmp_path):
    result = gen.generate(character, "揮手打招呼", seed=7, output_dir=tmp_path, formats=["gif", "mp4"])
    assert result.backend == "mock"
    assert result.seed == 7
    assert result.files["gif"].exists() and result.files["mp4"].exists()
    with Image.open(result.files["gif"]) as gif:
        assert gif.n_frames > 1
    assert "角色動作:揮手打招呼" in result.prompt
    assert result.warnings  # mock 後端會提醒不是真正的 AI 生成


def test_same_seed_is_reproducible(gen, character, tmp_path):
    a = gen.generate(character, "jump", seed=3, output_dir=tmp_path, formats=["gif"])
    b = gen.generate(character, "jump", seed=3, output_dir=tmp_path, formats=["gif"])
    assert all((x == y).all() for x, y in zip(a.frames, b.frames))


def test_pingpong_and_interpolate(gen, character, tmp_path):
    base = gen.generate(character, "spin", seed=1, num_frames=17, output_dir=tmp_path, formats=["gif"])
    pp = gen.generate(character, "spin", seed=1, num_frames=17, pingpong=True, output_dir=tmp_path, formats=["gif"])
    assert len(pp.frames) == 2 * len(base.frames) - 2
    smooth = gen.generate(character, "spin", seed=1, num_frames=17, interpolate=2, output_dir=tmp_path,
                          formats=["gif", "mp4"])
    assert smooth.fps == base.fps * 2
    assert len(smooth.frames) > len(base.frames)


def test_resolution_and_fit(gen, character, tmp_path):
    result = gen.generate(character, "nod", resolution="320x240", fit_mode="pad", output_dir=tmp_path, formats=["png"])
    assert (result.width, result.height) == (320, 240)
    assert len(list(result.files["png"].glob("*.png"))) == result.num_frames


def test_real_backend_without_torch_gives_clear_error(gen, character, tmp_path):
    if gen.device.torch_available:
        pytest.skip("已安裝 torch")
    with pytest.raises(RuntimeError, match="requirements-gpu"):
        gen.generate(character, "wave", backend="wan22", output_dir=tmp_path)


def test_empty_action_rejected():
    with pytest.raises(ValueError):
        build_prompt("   ")


def test_english_prompt():
    prompt, negative = build_prompt("waves hello", "silver hair girl")
    assert "The character waves hello" in prompt and "silver hair girl" in prompt
    assert negative


@pytest.mark.parametrize("name", list(BACKENDS))
def test_frame_counts_valid(name):
    spec = get_spec(name)
    n = spec.resolve_frames(50)
    assert (n - 1) % spec.frame_mod == 0


def test_classify_motion():
    assert classify_motion("開心地跳起來") == "jump"
    assert classify_motion("waves hello") == "wave"
    assert classify_motion("發呆") == "idle"


def test_cli(character, tmp_path, capsys):
    rc = main(["generate", "-i", str(character), "-a", "轉圈", "-b", "mock", "-o", str(tmp_path), "-f", "gif",
               "--seed", "5"])
    assert rc == 0
    assert list(tmp_path.glob("*.gif"))
    assert "seed: 5" in capsys.readouterr().out
