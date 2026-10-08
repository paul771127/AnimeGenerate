"""多張圖逐格動畫:角色圖 + 動作描述 → 拆成關鍵姿勢 → AI 一張張畫 → 串成 GIF。

每一格都從「角色原圖」出發編輯(而不是從上一格接著改),並使用同一個 seed,
這樣誤差不會一格一格累積,角色比較不會越畫越走樣。
"""
from __future__ import annotations

import logging
import random
import re
import time
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Callable, Sequence

from PIL import Image

from .config import load_config
from .device import DeviceInfo, detect_device
from .editors import EditRequest, ImageEditor, choose_editor, get_editor_class
from .image import flatten_alpha
from .poses import Pose, plan_poses

log = logging.getLogger(__name__)

ProgressFn = Callable[[int, int, str], None]
FrameFn = Callable[[int, Image.Image], None]

_SLUG_RE = re.compile(r"[^0-9A-Za-z㐀-鿿]+")

PROMPT_TEMPLATES = {
    "zh": "保持同一個角色:臉、髮型、服裝、配色和畫風完全不變,背景不變,構圖、角色大小與鏡頭位置不變。{desc}只改變姿勢:{pose}",
    "en": (
        "Keep the exact same character with the same face, hairstyle, outfit, colors and art style. "
        "Keep the same background, framing, character size and camera position. {desc}Only change the pose: {pose}"
    ),
}


def build_edit_prompt(pose: str, language: str, character_desc: str = "") -> str:
    desc = character_desc.strip()
    if desc:
        desc = f"角色:{desc}。" if language == "zh" else f"Character: {desc}. "
    return PROMPT_TEMPLATES["en" if language == "en" else "zh"].format(desc=desc, pose=pose.strip())


@dataclass
class KeyframeResult:
    reference: Image.Image  # 角色原圖(每一格都從這張編輯)
    keyframes: list[Image.Image]  # 依序的每一格(含原圖時第一格是原圖)
    poses: list[str]  # 對應每一格的姿勢文字(原圖那格是 "(原圖)")
    pose_source: str
    editor: str
    seed: int
    gif: Path
    frames_dir: Path
    elapsed: float
    settings: dict[str, Any] = field(default_factory=dict)
    warnings: list[str] = field(default_factory=list)

    def summary(self) -> str:
        lines = [
            f"模型: {self.editor}  格數: {len(self.keyframes)}  seed: {self.seed}  "
            f"姿勢來源: {self.pose_source}  耗時: {self.elapsed:.1f}s",
            *[f"  第 {i + 1} 格: {p}" for i, p in enumerate(self.poses)],
            f"GIF: {self.gif}",
            f"每格 PNG: {self.frames_dir}",
        ]
        lines += [f"注意: {w}" for w in self.warnings]
        return "\n".join(lines)


def assemble_gif(
    keyframes: Sequence[Image.Image],
    path: str | Path,
    *,
    frame_ms: int = 180,
    pingpong: bool = False,
    tweens: int = 0,
    max_width: int | None = 512,
    loop: int = 0,
) -> Path:
    """把關鍵格串成 GIF。tweens > 0 時在相鄰兩格之間插入淡入淡出的過渡格。"""
    if not keyframes:
        raise ValueError("沒有任何影格")
    size = keyframes[0].size
    if max_width and size[0] > max_width:
        size = (max_width, max(1, round(size[1] * max_width / size[0])))
    frames = [k.convert("RGB").resize(size, Image.LANCZOS) if k.size != size else k.convert("RGB")
              for k in keyframes]
    if pingpong and len(frames) > 2:
        frames = frames + frames[-2:0:-1]

    out: list[Image.Image] = []
    durations: list[int] = []
    tween_ms = 40
    for i, frame in enumerate(frames):
        out.append(frame)
        durations.append(frame_ms)
        if tweens > 0 and len(frames) > 1:
            nxt = frames[(i + 1) % len(frames)]  # 最後一格也淡入回第一格,循環比較順
            for k in range(1, tweens + 1):
                out.append(Image.blend(frame, nxt, k / (tweens + 1)))
                durations.append(tween_ms)

    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    out[0].save(path, save_all=True, append_images=out[1:], duration=durations, loop=loop,
                optimize=False, disposal=1)
    return path


class KeyframeAnimator:
    """持有已載入的圖片模型;同一個實例重複生成時不會重新載入。"""

    def __init__(self, cfg: dict[str, Any] | None = None, device: DeviceInfo | None = None):
        self.cfg = cfg if cfg is not None else load_config()
        self.kcfg: dict[str, Any] = self.cfg.get("keyframe", {})
        self.device = device or detect_device(self.cfg.get("device", "auto"))
        self._editor: ImageEditor | None = None

    # ---- 模型 ---------------------------------------------------------------------
    def resolve_editor_name(self, name: str | None = None) -> tuple[str, str]:
        name = name or self.kcfg.get("editor", "auto")
        if name in (None, "", "auto"):
            return choose_editor(self.device)
        get_editor_class(name)
        return name, "手動指定"

    def get_editor(self, name: str) -> ImageEditor:
        if self._editor is not None and self._editor.spec.name == name:
            return self._editor
        if self._editor is not None:
            self._editor.unload()
            self._editor = None
        if name != "mock" and not self.device.torch_available:
            raise RuntimeError(
                f"圖片模型 {name} 需要 PyTorch 與 diffusers。請先安裝:pip install -e \".[gpu]\""
                "(torch 請依 https://pytorch.org/get-started/locally/ 選擇對應 CUDA 版本)"
            )
        cls = get_editor_class(name)
        self._editor = cls(
            self.cfg.get("editors", {}).get(name, {}),
            self.device,
            memory_mode=self.kcfg.get("memory_mode", "auto"),
            quantize=self.kcfg.get("quantize", "auto"),
            cache_dir=self.cfg.get("model_cache_dir"),
        )
        return self._editor

    def release(self) -> None:
        """卸載模型,釋放顯卡記憶體(切換到另一種模式前呼叫)。"""
        if self._editor is not None:
            self._editor.unload()
            self._editor = None

    def _max_area(self) -> int:
        value = self.kcfg.get("max_area", "auto")
        if value in (None, "", "auto"):
            # 顯卡記憶體小時降低工作解析度,比較不會 out of memory
            return 1024 * 1024 if self.device.vram_gb >= 20 or not self.device.has_gpu else 768 * 768
        return int(value)

    # ---- 生成 -----------------------------------------------------------------------
    def plan(self, action: str, num_poses: int | None = None, use_ollama: bool | None = None) -> tuple[list[Pose], str]:
        count = int(num_poses or self.kcfg.get("num_poses", 4))
        use_ollama = use_ollama if use_ollama is not None else self.cfg.get("prompt", {}).get("enhancer") == "ollama"
        ollama = self.cfg.get("prompt", {}).get("ollama", {}) if use_ollama else None
        return plan_poses(action, count, ollama)

    def draw_pose(self, reference: Image.Image, pose: str, *, editor: str | None = None, seed: int,
                  steps: int | None = None, character_desc: str = "",
                  progress: ProgressFn | None = None) -> Image.Image:
        """依姿勢文字從角色原圖畫出一格,輸出尺寸與原圖相同。"""
        name, _ = self.resolve_editor_name(editor)
        ed = self.get_editor(name)
        language = ed.spec.language
        prompt = build_edit_prompt(pose, language, character_desc)
        req = EditRequest(
            image=reference,
            prompt=prompt,
            seed=int(seed),
            steps=int(steps or self.kcfg.get("steps") or ed.spec.default_steps),
            max_area=self._max_area(),
            progress=progress,
        )
        out = ed.edit(req)
        return out.resize(reference.size, Image.LANCZOS) if out.size != reference.size else out

    def generate(
        self,
        image: Image.Image | str | Path,
        action: str,
        *,
        poses: Sequence[str] | None = None,
        character_desc: str = "",
        editor: str | None = None,
        num_poses: int | None = None,
        seed: int | None = None,
        steps: int | None = None,
        frame_ms: int | None = None,
        include_original: bool | None = None,
        pingpong: bool | None = None,
        tweens: int | None = None,
        use_ollama: bool | None = None,
        output_dir: str | Path | None = None,
        progress: ProgressFn | None = None,
        on_frame: FrameFn | None = None,
    ) -> KeyframeResult:
        started = time.time()
        warnings: list[str] = []

        def report(i: int, n: int, msg: str) -> None:
            if progress:
                progress(i, n, msg)

        # 1. 模型
        name, reason = self.resolve_editor_name(editor)
        log.info("圖片模型: %s(%s)", name, reason)
        ed = self.get_editor(name)
        if name == "mock":
            warnings.append(
                "目前使用 mock:不會真的改變姿勢,只用來測試流程。要讓角色照描述擺姿勢,"
                "需要 NVIDIA 顯卡(建議 16GB+ VRAM)並安裝 pip install -e \".[gpu]\"。"
            )

        # 2. 角色圖
        if isinstance(image, (str, Path)):
            with Image.open(image) as im:
                image = im.copy()
        reference = flatten_alpha(image, self.cfg.get("generation", {}).get("background", (255, 255, 255)))

        # 3. 姿勢
        if poses:
            pose_objs = [Pose(p, p) for p in poses if p.strip()]
            source = "自訂姿勢"
        else:
            pose_objs, source = self.plan(action, num_poses, use_ollama)
        language = ed.spec.language
        pose_texts = [p.text(language) for p in pose_objs]
        if language == "en" and any(re.search(r"[㐀-鿿]", t) for t in pose_texts):
            warnings.append(f"{ed.spec.display_name} 只懂英文,中文姿勢描述可能會被忽略;建議改用英文或 Qwen 模型")
        if source.startswith("通用"):
            warnings.append("這個動作沒有內建範本,用的是通用拆法;每行寫一個姿勢(例如「舉起右手」換行「右手揮向左邊」)效果會好很多")

        # 4. 一格一格畫
        seed_val = seed if seed is not None else self.kcfg.get("seed", -1)
        if seed_val is None or int(seed_val) < 0:
            seed_val = random.randint(0, 2**31 - 1)
        seed_val = int(seed_val)
        include = include_original if include_original is not None else self.kcfg.get("include_original", True)

        keyframes: list[Image.Image] = []
        labels: list[str] = []
        if include:
            keyframes.append(reference)
            labels.append("(原圖)")
            if on_frame:
                on_frame(0, reference)
        total = len(pose_texts)
        for i, pose in enumerate(pose_texts):
            report(i, total, f"畫第 {i + 1}/{total} 格:{pose}")

            def step_progress(s: int, n: int, msg: str, _i: int = i) -> None:
                report(_i * n + s, total * n, f"第 {_i + 1}/{total} 格 {msg}")

            frame = self.draw_pose(reference, pose, editor=name, seed=seed_val, steps=steps,
                                   character_desc=character_desc, progress=step_progress)
            keyframes.append(frame)
            labels.append(pose)
            if on_frame:
                on_frame(len(keyframes) - 1, frame)

        # 5. 輸出
        out_root = Path(output_dir or self.cfg.get("output_dir", "outputs"))
        stem = f"{datetime.now():%Y%m%d-%H%M%S}_{_SLUG_RE.sub('_', action).strip('_')[:24] or 'anim'}_{seed_val}"
        frames_dir = out_root / stem
        settings = {
            "frame_ms": int(frame_ms or self.kcfg.get("frame_ms", 180)),
            "pingpong": bool(pingpong if pingpong is not None else self.kcfg.get("pingpong", False)),
            "tweens": int(tweens if tweens is not None else self.kcfg.get("tweens", 0)),
            "max_width": self.kcfg.get("gif_max_width", 512),
        }
        result = KeyframeResult(
            reference=reference, keyframes=keyframes, poses=labels, pose_source=source, editor=name, seed=seed_val,
            gif=frames_dir / "animation.gif", frames_dir=frames_dir, elapsed=0.0,
            settings=settings, warnings=warnings,
        )
        self.save(result)
        result.elapsed = time.time() - started
        return result

    def save(self, result: KeyframeResult) -> Path:
        """把每格 PNG 與 GIF 寫到 result.frames_dir(重畫某一格後也用這個重新輸出)。"""
        result.frames_dir.mkdir(parents=True, exist_ok=True)
        for old in result.frames_dir.glob("frame_*.png"):
            old.unlink()
        for i, frame in enumerate(result.keyframes):
            frame.save(result.frames_dir / f"frame_{i:02d}.png")
        (result.frames_dir / "poses.txt").write_text(
            "\n".join(f"{i + 1}. {p}" for i, p in enumerate(result.poses)) + "\n", encoding="utf-8")
        s = result.settings
        return assemble_gif(result.keyframes, result.gif, frame_ms=s["frame_ms"], pingpong=s["pingpong"],
                            tweens=s["tweens"], max_width=s["max_width"])

    def redraw(self, result: KeyframeResult, index: int, *, pose: str | None = None, seed: int | None = None,
               steps: int | None = None, character_desc: str = "",
               progress: ProgressFn | None = None) -> KeyframeResult:
        """重畫其中一格(可換姿勢文字或 seed),並重新輸出 GIF。"""
        if not 0 <= index < len(result.keyframes):
            raise ValueError(f"沒有第 {index + 1} 格")
        if result.poses[index] == "(原圖)" and not pose:
            raise ValueError("這一格是角色原圖,不需要重畫(要改它的話請輸入新的姿勢描述)")
        new_pose = pose or result.poses[index]
        new_seed = seed if seed is not None and seed >= 0 else random.randint(0, 2**31 - 1)
        frame = self.draw_pose(result.reference, new_pose, editor=result.editor, seed=new_seed, steps=steps,
                               character_desc=character_desc, progress=progress)
        result.keyframes[index] = frame
        result.poses[index] = new_pose
        self.save(result)
        return result
