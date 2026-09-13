"""Wan 2.x 影像轉影片後端(Apache-2.0 開源權重)。

- wan22: Wan2.2 TI2V-5B  → 品質好、5B 參數,12GB VRAM 以上可用 model offload 跑 480p
- wan21: Wan2.1 I2V-14B-480P → 更大更慢,24GB+ 較合適
"""
from __future__ import annotations

import logging

import numpy as np

from ..prompt import WAN_NEGATIVE
from .base import BackendSpec, DiffusersBackend, GenerationRequest

log = logging.getLogger(__name__)

WAN22_SPEC = BackendSpec(
    name="wan22",
    display_name="Wan2.2 TI2V-5B(推薦)",
    default_model_id="Wan-AI/Wan2.2-TI2V-5B-Diffusers",
    default_fps=24,
    default_frames=81,
    frame_mod=4,
    mod_value=32,
    default_max_area=1280 * 704,
    low_vram_max_area=832 * 480,
    default_steps=40,
    default_guidance=5.0,
    vram_full_gb=28,
    vram_offload_gb=12,
    vram_min_gb=8,
    default_negative=WAN_NEGATIVE,
    notes="支援中文提示詞。full 模式輸出 704p,offload 模式自動降到 480p。",
    license_note="Apache-2.0",
)

WAN21_SPEC = BackendSpec(
    name="wan21",
    display_name="Wan2.1 I2V-14B-480P",
    default_model_id="Wan-AI/Wan2.1-I2V-14B-480P-Diffusers",
    default_fps=16,
    default_frames=81,
    frame_mod=4,
    mod_value=16,
    default_max_area=832 * 480,
    low_vram_max_area=832 * 480,
    default_steps=40,
    default_guidance=5.0,
    vram_full_gb=80,
    vram_offload_gb=32,
    vram_min_gb=12,
    default_negative=WAN_NEGATIVE,
    notes="14B 參數,品質高但速度慢;VRAM 不足會自動 sequential offload(很慢)。",
    license_note="Apache-2.0",
)


class WanBackend(DiffusersBackend):
    spec = WAN22_SPEC
    _needs_image_encoder = False

    def _load(self) -> None:
        torch = self._torch()
        from diffusers import AutoencoderKLWan, WanImageToVideoPipeline

        kwargs = self._from_pretrained_kwargs()
        log.info("載入 %s ...", self.model_id)
        vae = AutoencoderKLWan.from_pretrained(self.model_id, subfolder="vae", torch_dtype=torch.float32, **kwargs)
        pipe_kwargs = dict(vae=vae, torch_dtype=self.dtype, **kwargs)
        if self._needs_image_encoder:
            from transformers import CLIPVisionModel

            pipe_kwargs["image_encoder"] = CLIPVisionModel.from_pretrained(
                self.model_id, subfolder="image_encoder", torch_dtype=torch.float32, **kwargs
            )
        self.pipe = WanImageToVideoPipeline.from_pretrained(self.model_id, **pipe_kwargs)
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


class Wan21Backend(WanBackend):
    spec = WAN21_SPEC
    _needs_image_encoder = True
