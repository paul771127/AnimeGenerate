"""Qwen-Image-Edit-2509(Apache-2.0)。中英文指令都懂,改姿勢時角色一致性好。

20B 參數:bf16 需要 ~48GB VRAM;4-bit 量化 + offload 約 16GB 可跑(12GB 勉強)。
"""
from __future__ import annotations

from PIL import Image

from .base import EditorSpec, EditRequest, ImageEditor

SPEC = EditorSpec(
    name="qwen",
    display_name="Qwen-Image-Edit-2509(推薦)",
    default_model_id="Qwen/Qwen-Image-Edit-2509",
    default_steps=40,
    vram_full_gb=64,
    vram_offload_gb=48,
    vram_4bit_gb=16,
    language="zh",
    quantize_components=("transformer", "text_encoder"),
    notes="中文指令也看得懂;VRAM 不到 48GB 時自動 4-bit 量化(16GB 可跑)。第一次會下載約 58GB。",
    license_note="Apache-2.0(可商用)",
)


def _dims(image: Image.Image, max_area: int, mod: int = 16) -> tuple[int, int]:
    ratio = image.width / image.height
    w = (max_area * ratio) ** 0.5
    h = w / ratio
    return max(mod, round(w / mod) * mod), max(mod, round(h / mod) * mod)


class QwenEditor(ImageEditor):
    spec = SPEC

    def _load(self):
        self._torch()
        from diffusers import QwenImageEditPlusPipeline

        pipe = QwenImageEditPlusPipeline.from_pretrained(self.model_id, **self._from_pretrained_kwargs())
        self._place(pipe)
        return pipe

    def edit(self, req: EditRequest) -> Image.Image:
        self.load()
        width, height = _dims(req.image, req.max_area)
        out = self.pipe(
            image=[req.image],
            prompt=req.prompt,
            negative_prompt=req.negative_prompt or " ",
            true_cfg_scale=4.0,
            width=width,
            height=height,
            num_inference_steps=req.steps,
            generator=self._generator(req.seed),
            callback_on_step_end=self._step_callback(req),
        )
        return out.images[0].convert("RGB")
