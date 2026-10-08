"""不需要 GPU 的假編輯器:在原圖上做小幅位移並標上姿勢文字,只用來測試流程。"""
from __future__ import annotations

import hashlib

from PIL import Image, ImageDraw

from .base import EditorSpec, EditRequest, ImageEditor

SPEC = EditorSpec(
    name="mock",
    display_name="Mock(測試用,不需 GPU)",
    default_model_id="(無)",
    default_steps=1,
    vram_full_gb=0,
    vram_offload_gb=0,
    vram_4bit_gb=0,
    language="zh",
    notes="不會真的改姿勢,只會位移並標上姿勢文字,用來測試流程。",
)


class MockEditor(ImageEditor):
    spec = SPEC

    def _load(self):
        return object()

    def edit(self, req: EditRequest) -> Image.Image:
        self.load()
        img = req.image.convert("RGB")
        w, h = img.size
        digest = hashlib.md5(f"{req.prompt}|{req.seed}".encode()).digest()
        dx = (digest[0] - 128) * w // 2560
        dy = (digest[1] - 128) * h // 2560
        canvas = Image.new("RGB", (w, h), img.getpixel((0, 0)))
        canvas.paste(img, (dx, dy))
        pose = req.prompt.rsplit(":", 1)[-1].rsplit(":", 1)[-1].strip()
        ImageDraw.Draw(canvas).text((6, 6), pose[:60], fill=(255, 0, 0))
        if req.progress:
            req.progress(1, 1, "繪製中")
        return canvas
