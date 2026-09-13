"""LTX-Video 後端(2B 參數,速度最快、VRAM 需求低)。

提示詞越詳細越好;建議搭配 Ollama 強化。
"""
from __future__ import annotations

import logging

import numpy as np

from ..prompt import GENERIC_NEGATIVE
from .base import BackendSpec, DiffusersBackend, GenerationRequest

log = logging.getLogger(__name__)

SPEC = BackendSpec(
    name="ltx",
    display_name="LTX-Video 2B(快速)",
    default_model_id="Lightricks/LTX-Video",
    default_fps=24,
    default_frames=97,
    frame_mod=8,
    mod_value=32,
    default_max_area=768 * 512,
    low_vram_max_area=704 * 480,
    default_steps=40,
    default_guidance=3.0,
    vram_full_gb=16,
    vram_offload_gb=8,
    vram_min_gb=4,
    default_negative=GENERIC_NEGATIVE,
    notes="速度快,對簡短提示詞較不敏感,請描述得詳細一點或開啟 Ollama 提示詞強化。",
    license_note="LTX-Video 開源授權(請看模型頁面)",
)


class LTXBackend(DiffusersBackend):
    spec = SPEC

    def _load(self) -> None:
        self._torch()
        from diffusers import LTXImageToVideoPipeline

        log.info("載入 %s ...", self.model_id)
        self.pipe = LTXImageToVideoPipeline.from_pretrained(
            self.model_id, torch_dtype=self.dtype, **self._from_pretrained_kwargs()
        )
        self._apply_memory_mode(self.pipe)

    def generate(self, req: GenerationRequest) -> list[np.ndarray]:
        self.load()
        output = self.pipe(
            image=req.image,
            prompt=req.prompt,
            negative_prompt=req.negative_prompt,
            height=req.height,
            width=req.width,
            num_frames=req.num_frames,
            guidance_scale=req.guidance_scale,
            num_inference_steps=req.num_inference_steps,
            generator=self._generator(req.seed),
            callback_on_step_end=self._step_callback(req),
        )
        return self._output_to_frames(output)
