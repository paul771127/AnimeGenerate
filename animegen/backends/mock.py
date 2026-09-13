"""不需要 GPU 的假後端:用簡單的位移 / 縮放 / 旋轉讓角色「動起來」。

用途:沒有 GPU 的機器上測試整條流程(讀圖 → 提示詞 → 輸出 MP4/GIF → UI)。
它不會真的理解動作,只會依關鍵字挑一種簡單的運動模式。
"""
from __future__ import annotations

import math
import re

import numpy as np
from PIL import Image

from .base import BackendSpec, GenerationRequest, VideoBackend

SPEC = BackendSpec(
    name="mock",
    display_name="Mock(測試用,不需 GPU)",
    default_model_id="(無)",
    default_fps=16,
    default_frames=33,
    frame_mod=4,
    mod_value=16,
    default_max_area=832 * 480,
    low_vram_max_area=832 * 480,
    default_steps=1,
    default_guidance=0.0,
    vram_full_gb=0,
    vram_offload_gb=0,
    vram_min_gb=0,
    default_negative="",
    notes="只做簡單的 2D 位移動畫,用來測試流程;不是真正的 AI 生成。",
)

_MOTIONS = {
    "jump": ("jump", "hop", "leap", "跳", "跳躍", "蹦"),
    "wave": ("wave", "waving", "hello", "greet", "揮手", "招手", "打招呼", "揮"),
    "walk": ("walk", "run", "running", "走", "跑", "步"),
    "spin": ("spin", "turn around", "rotate", "twirl", "轉圈", "旋轉", "轉身"),
    "nod": ("nod", "bow", "點頭", "鞠躬", "彎腰"),
    "shake": ("shake", "dance", "搖", "跳舞", "舞"),
}


def classify_motion(action: str) -> str:
    text = (action or "").lower()
    for motion, keys in _MOTIONS.items():
        for key in keys:
            if re.search(re.escape(key), text):
                return motion
    return "idle"


class MockBackend(VideoBackend):
    spec = SPEC

    def _load(self) -> None:
        pass

    def generate(self, req: GenerationRequest) -> list[np.ndarray]:
        motion = classify_motion(req.extra.get("action", req.prompt))
        base = req.image.convert("RGB").resize((req.width, req.height), Image.LANCZOS)
        w, h = base.size
        # 用邊緣平均色當背景,旋轉 / 位移露出的地方比較不突兀
        arr = np.asarray(base)
        border = np.concatenate([arr[0], arr[-1], arr[:, 0], arr[:, -1]])
        bg = tuple(int(v) for v in border.mean(axis=0))
        rng = np.random.default_rng(req.seed)
        phase = float(rng.uniform(0, 2 * math.pi))

        frames: list[np.ndarray] = []
        n = req.num_frames
        for i in range(n):
            t = i / max(1, n - 1)
            theta = 2 * math.pi * t + phase
            dx, dy, angle, scale, squash = 0.0, 0.0, 0.0, 1.0, 1.0
            if motion == "idle":
                dy = math.sin(theta) * h * 0.012
                scale = 1 + 0.015 * math.sin(theta)
            elif motion == "jump":
                bounce = abs(math.sin(math.pi * t * 2))
                dy = -bounce * h * 0.12
                squash = 1 - 0.08 * (1 - bounce)
            elif motion == "wave":
                angle = 4.0 * math.sin(theta * 2)
                dy = math.sin(theta) * h * 0.01
            elif motion == "walk":
                dx = (t - 0.5) * w * 0.25
                dy = abs(math.sin(theta * 3)) * -h * 0.02
                angle = 2.0 * math.sin(theta * 3)
            elif motion == "spin":
                angle = 360.0 * t
            elif motion == "nod":
                squash = 1 - 0.06 * max(0.0, math.sin(theta))
                dy = max(0.0, math.sin(theta)) * h * 0.03
            elif motion == "shake":
                dx = math.sin(theta * 3) * w * 0.04
                angle = 3.0 * math.sin(theta * 3)

            frame = base
            if scale != 1.0 or squash != 1.0:
                new_size = (max(1, round(w * scale)), max(1, round(h * scale * squash)))
                frame = frame.resize(new_size, Image.BICUBIC)
            if angle:
                frame = frame.rotate(angle, resample=Image.BICUBIC, expand=True, fillcolor=bg)
            canvas = Image.new("RGB", (w, h), bg)
            # 對齊底部中央,縮放時腳不會飄
            x = round((w - frame.width) / 2 + dx)
            y = round(h - frame.height + dy) if motion in ("jump", "nod") else round((h - frame.height) / 2 + dy)
            canvas.paste(frame, (x, y))
            frames.append(np.asarray(canvas))
            if req.progress:
                req.progress(i + 1, n, "合成影格")
        return frames
