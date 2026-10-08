"""Qwen-Image-Edit-2509(Apache-2.0)。中英文指令都懂,改姿勢時角色一致性好。

20B 參數:bf16 需要 ~48GB VRAM;4-bit 量化 + offload 約 16GB 可跑(12GB 勉強)。
一次最多參考 3 張圖,補間格用「原圖 + 前一格 + 後一格」來畫中間姿勢。
加速模式載入 lightx2v 的 Lightning LoRA,8 步出圖(預設 40 步),適合要畫很多格的時候。
"""
from __future__ import annotations

import logging
import math

from PIL import Image

from .base import EditorSpec, EditRequest, ImageEditor

log = logging.getLogger(__name__)

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
    max_images=3,
    fast_steps=8,
)

LIGHTNING_REPO = "lightx2v/Qwen-Image-Lightning"
LIGHTNING_WEIGHT = "Qwen-Image-Edit-2509/Qwen-Image-Edit-2509-Lightning-8steps-V1.0-bf16.safetensors"

# 出處:https://huggingface.co/lightx2v/Qwen-Image-Lightning(蒸餾時使用 shift=3)
LIGHTNING_SCHEDULER = {
    "base_image_seq_len": 256,
    "base_shift": math.log(3),
    "invert_sigmas": False,
    "max_image_seq_len": 8192,
    "max_shift": math.log(3),
    "num_train_timesteps": 1000,
    "shift": 1.0,
    "shift_terminal": None,
    "stochastic_sampling": False,
    "time_shift_type": "exponential",
    "use_beta_sigmas": False,
    "use_dynamic_shifting": True,
    "use_exponential_sigmas": False,
    "use_karras_sigmas": False,
}


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

        kwargs = self._from_pretrained_kwargs()
        if self.fast:
            from diffusers import FlowMatchEulerDiscreteScheduler

            kwargs["scheduler"] = FlowMatchEulerDiscreteScheduler.from_config(LIGHTNING_SCHEDULER)
        pipe = QwenImageEditPlusPipeline.from_pretrained(self.model_id, **kwargs)
        if self.fast:
            weight = self.cfg.get("lightning_weight", LIGHTNING_WEIGHT)
            log.info("載入加速 LoRA %s/%s", LIGHTNING_REPO, weight)
            lora_kwargs = {"cache_dir": self.cache_dir} if self.cache_dir else {}
            pipe.load_lora_weights(self.cfg.get("lightning_repo", LIGHTNING_REPO), weight_name=weight,
                                   **lora_kwargs)
        self._place(pipe)
        return pipe

    def edit(self, req: EditRequest) -> Image.Image:
        self.load()
        width, height = _dims(req.image, req.max_area)
        images = [req.image, *req.extra_images][: self.spec.max_images]
        out = self.pipe(
            image=images,
            prompt=req.prompt,
            negative_prompt=req.negative_prompt or " ",
            true_cfg_scale=1.0 if self.fast else 4.0,  # Lightning LoRA 已蒸餾掉 CFG
            width=width,
            height=height,
            num_inference_steps=req.steps,
            generator=self._generator(req.seed),
            callback_on_step_end=self._step_callback(req),
        )
        return out.images[0].convert("RGB")
