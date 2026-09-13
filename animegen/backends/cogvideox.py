"""CogVideoX-5B-I2V 後端(開源權重,5B 參數)。

固定輸出 720x480、49 幀 @ 8 fps。sequential offload 約 5GB VRAM 就能跑(但很慢)。
"""
from __future__ import annotations

import logging

import numpy as np

from ..prompt import GENERIC_NEGATIVE
from .base import BackendSpec, DiffusersBackend, GenerationRequest

log = logging.getLogger(__name__)

SPEC = BackendSpec(
    name="cogvideox",
    display_name="CogVideoX-5B-I2V(低 VRAM)",
    default_model_id="THUDM/CogVideoX-5b-I2V",
    default_fps=8,
    default_frames=49,
    frame_mod=8,
    mod_value=16,
    default_max_area=720 * 480,
    low_vram_max_area=720 * 480,
    default_steps=50,
    default_guidance=6.0,
    vram_full_gb=28,
    vram_offload_gb=14,
    vram_min_gb=5,
    default_negative=GENERIC_NEGATIVE,
    fixed_resolution=(720, 480),
    notes="固定 720x480;建議用英文、描述詳細的提示詞(可開啟 Ollama 強化)。",
    license_note="CogVideoX License(可免費使用,商用有條件,請看模型頁面)",
)


class CogVideoXBackend(DiffusersBackend):
    spec = SPEC

    def _load(self) -> None:
        self._torch()
        from diffusers import CogVideoXImageToVideoPipeline

        log.info("載入 %s ...", self.model_id)
        self.pipe = CogVideoXImageToVideoPipeline.from_pretrained(
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
            use_dynamic_cfg=True,
            generator=self._generator(req.seed),
            callback_on_step_end=self._step_callback(req),
        )
        return self._output_to_frames(output)
