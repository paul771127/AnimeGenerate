"""圖片編輯後端的共用介面:看著角色原圖,畫出同一個角色擺出新姿勢的圖。"""
from __future__ import annotations

import gc
import logging
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any, Callable

from PIL import Image

from ..backends.base import apply_memory_mode
from ..device import DeviceInfo

log = logging.getLogger(__name__)

ProgressFn = Callable[[int, int, str], None]


@dataclass(frozen=True)
class EditorSpec:
    name: str
    display_name: str
    default_model_id: str
    default_steps: int
    vram_full_gb: float  # bf16 全部放進 GPU
    vram_offload_gb: float  # bf16 + model offload
    vram_4bit_gb: float  # 4-bit 量化 + model offload
    language: str  # 提示詞語言:zh(中英都懂)| en(只懂英文)
    quantize_components: tuple[str, ...] = ()
    notes: str = ""
    license_note: str = ""
    gated: bool = False  # Hugging Face 上需要先同意授權才能下載
    max_images: int = 1  # 一次能參考幾張圖(補間格需要 3 張:原圖 + 前一格 + 後一格)
    fast_steps: int | None = None  # 支援加速 LoRA 時的步數


@dataclass
class EditRequest:
    image: Image.Image  # 角色原圖(RGB)
    prompt: str
    seed: int
    steps: int
    max_area: int
    negative_prompt: str = ""
    progress: ProgressFn | None = None
    extra_images: list[Image.Image] = field(default_factory=list)  # 額外參考圖(Picture 2, 3...)


class ImageEditor(ABC):
    spec: EditorSpec

    def __init__(self, editor_cfg: dict[str, Any], device_info: DeviceInfo, *, memory_mode: str = "auto",
                 quantize: str = "auto", cache_dir: str | None = None, fast: bool = False):
        self.cfg = editor_cfg or {}
        self.device_info = device_info
        self.model_id: str = self.cfg.get("model_id") or self.spec.default_model_id
        self.cache_dir = cache_dir
        self.quantize, self.memory_mode = self._resolve_memory(quantize, memory_mode)
        self.fast = bool(fast and self.spec.fast_steps)
        self.pipe: Any = None

    def _resolve_memory(self, quantize: str, memory_mode: str) -> tuple[str, str]:
        """依 VRAM 決定要不要 4-bit 量化,以及記憶體模式。"""
        vram = self.device_info.vram_gb
        spec = self.spec
        if quantize in (None, "", "auto"):
            quantize = "none" if vram >= spec.vram_offload_gb or self.device_info.kind != "cuda" else "4bit"
        if memory_mode in (None, "", "auto"):
            if quantize == "none":
                memory_mode = "full" if vram >= spec.vram_full_gb else (
                    "model_offload" if vram >= spec.vram_offload_gb else "sequential_offload")
            else:
                memory_mode = "model_offload"
        return quantize, memory_mode

    @property
    def loaded(self) -> bool:
        return self.pipe is not None

    def describe(self) -> str:
        q = "4-bit 量化" if self.quantize == "4bit" else "bf16"
        fast = f", 加速 {self.spec.fast_steps} 步" if self.fast else ""
        return f"{self.spec.display_name}({q}, {self.memory_mode}{fast})"

    @property
    def default_steps(self) -> int:
        return self.spec.fast_steps if self.fast and self.spec.fast_steps else self.spec.default_steps

    # ---- diffusers 共用 ---------------------------------------------------------
    def _torch(self):
        try:
            import torch
        except ImportError as exc:
            raise RuntimeError(
                "此模型需要 PyTorch 與 diffusers,請先安裝:pip install -e \".[gpu]\""
                "(torch 請依 https://pytorch.org/get-started/locally/ 選擇對應 CUDA 版本)"
            ) from exc
        return torch

    def _from_pretrained_kwargs(self) -> dict[str, Any]:
        torch = self._torch()
        kwargs: dict[str, Any] = {"torch_dtype": torch.bfloat16 if self.device_info.has_gpu else torch.float32}
        if self.cache_dir:
            kwargs["cache_dir"] = self.cache_dir
        if self.quantize == "4bit":
            try:
                import bitsandbytes  # noqa: F401
            except ImportError as exc:
                raise RuntimeError("4-bit 量化需要 bitsandbytes:pip install bitsandbytes") from exc
            from diffusers.quantizers import PipelineQuantizationConfig

            kwargs["quantization_config"] = PipelineQuantizationConfig(
                quant_backend="bitsandbytes_4bit",
                quant_kwargs={"load_in_4bit": True, "bnb_4bit_quant_type": "nf4",
                              "bnb_4bit_compute_dtype": torch.bfloat16},
                components_to_quantize=list(self.spec.quantize_components),
            )
        return kwargs

    def _place(self, pipe) -> None:
        apply_memory_mode(pipe, self.device_info.device, self.memory_mode)

    def _generator(self, seed: int):
        return self._torch().Generator("cpu").manual_seed(int(seed))

    @staticmethod
    def _step_callback(req: EditRequest):
        if req.progress is None:
            return None

        def _cb(pipe, step_index, timestep, callback_kwargs):
            req.progress(step_index + 1, req.steps, "繪製中")
            return callback_kwargs

        return _cb

    # ---- 生命週期 -----------------------------------------------------------------
    def load(self) -> None:
        if self.pipe is None:
            log.info("載入 %s(%s)...", self.model_id, self.describe())
            self.pipe = self._load()

    def unload(self) -> None:
        self.pipe = None
        gc.collect()
        try:
            import torch

            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except ImportError:
            pass

    @abstractmethod
    def _load(self) -> Any: ...

    @abstractmethod
    def edit(self, req: EditRequest) -> Image.Image:
        """回傳編輯後的 RGB 圖。"""
