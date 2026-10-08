"""FLUX.1 Kontext [dev](12B)。只懂英文指令;4-bit 量化後 8~10GB 顯卡就能跑。

注意:模型在 Hugging Face 上需要先登入並同意授權才能下載,且授權為非商用。
"""
from __future__ import annotations

from PIL import Image

from .base import EditorSpec, EditRequest, ImageEditor

SPEC = EditorSpec(
    name="kontext",
    display_name="FLUX.1 Kontext dev(低 VRAM)",
    default_model_id="black-forest-labs/FLUX.1-Kontext-dev",
    default_steps=28,
    vram_full_gb=36,
    vram_offload_gb=26,
    vram_4bit_gb=10,
    language="en",
    quantize_components=("transformer", "text_encoder_2"),
    notes="只懂英文指令(內建動作會自動用英文)。需先 `huggingface-cli login` 並在模型頁同意授權。",
    license_note="FLUX.1 [dev] Non-Commercial License(非商用)",
    gated=True,
)


class KontextEditor(ImageEditor):
    spec = SPEC

    def _load(self):
        self._torch()
        from diffusers import FluxKontextPipeline

        pipe = FluxKontextPipeline.from_pretrained(self.model_id, **self._from_pretrained_kwargs())
        self._place(pipe)
        return pipe

    def edit(self, req: EditRequest) -> Image.Image:
        self.load()
        out = self.pipe(
            image=req.image,
            prompt=req.prompt,
            guidance_scale=2.5,
            num_inference_steps=req.steps,
            max_area=req.max_area,
            generator=self._generator(req.seed),
            callback_on_step_end=self._step_callback(req),
        )
        return out.images[0].convert("RGB")
