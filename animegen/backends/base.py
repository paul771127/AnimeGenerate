"""影片生成後端的共用介面。"""
from __future__ import annotations

import logging
from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from typing import Any, Callable

import numpy as np
from PIL import Image

from ..device import DeviceInfo, choose_memory_mode
from ..image import fit_resolution, round_to_mod

log = logging.getLogger(__name__)

ProgressFn = Callable[[int, int, str], None]


@dataclass(frozen=True)
class BackendSpec:
    name: str
    display_name: str
    default_model_id: str
    default_fps: int
    default_frames: int
    frame_mod: int  # 有效幀數 = k * frame_mod + 1
    mod_value: int  # 寬高必須是這個數的倍數
    default_max_area: int  # 預設解析度的像素總數上限
    low_vram_max_area: int  # offload 模式下使用的像素總數上限
    default_steps: int
    default_guidance: float
    vram_full_gb: float  # 全部載入 GPU 需要的 VRAM
    vram_offload_gb: float  # model offload 需要的 VRAM
    vram_min_gb: float  # sequential offload 的最低需求
    default_negative: str
    fixed_resolution: tuple[int, int] | None = None
    notes: str = ""
    license_note: str = ""

    def resolve_frames(self, requested: int | None) -> int:
        n = self.default_frames if not requested else int(requested)
        k = max(1, round((n - 1) / self.frame_mod))
        return k * self.frame_mod + 1


@dataclass
class GenerationRequest:
    image: Image.Image
    prompt: str
    negative_prompt: str
    width: int
    height: int
    num_frames: int
    num_inference_steps: int
    guidance_scale: float
    seed: int
    progress: ProgressFn | None = None
    extra: dict[str, Any] = field(default_factory=dict)


class VideoBackend(ABC):
    spec: BackendSpec

    def __init__(self, backend_cfg: dict[str, Any], device_info: DeviceInfo, memory_mode: str = "auto",
                 cache_dir: str | None = None):
        self.cfg = backend_cfg or {}
        self.device_info = device_info
        self.model_id: str = self.cfg.get("model_id") or self.spec.default_model_id
        self.cache_dir = cache_dir
        self.memory_mode = (
            choose_memory_mode(device_info.vram_gb, device_info.kind, self.spec.vram_full_gb, self.spec.vram_offload_gb)
            if memory_mode in (None, "auto")
            else memory_mode
        )
        self.loaded = False

    # ---- 尺寸 / 參數解析 -------------------------------------------------
    def resolve_resolution(self, image: Image.Image, requested: tuple[int, int] | None) -> tuple[int, int]:
        if self.spec.fixed_resolution:
            return self.spec.fixed_resolution
        if requested:
            return round_to_mod(requested[0], self.spec.mod_value), round_to_mod(requested[1], self.spec.mod_value)
        max_area = self.spec.default_max_area if self.memory_mode == "full" else self.spec.low_vram_max_area
        return fit_resolution(image.width, image.height, max_area, self.spec.mod_value)

    def resolve_steps(self, requested: int | None) -> int:
        return int(requested) if requested else self.spec.default_steps

    def resolve_guidance(self, requested: float | None) -> float:
        return float(requested) if requested is not None else self.spec.default_guidance

    # ---- 生命週期 -----------------------------------------------------------
    def load(self) -> None:
        if not self.loaded:
            self._load()
            self.loaded = True

    def unload(self) -> None:
        self._unload()
        self.loaded = False

    def _unload(self) -> None:  # 子類別可覆寫
        pass

    @abstractmethod
    def _load(self) -> None: ...

    @abstractmethod
    def generate(self, req: GenerationRequest) -> list[np.ndarray]:
        """回傳 uint8 HxWx3 的影格列表。"""


class DiffusersBackend(VideoBackend):
    """基於 diffusers 的後端共用邏輯:dtype、offload、seed、進度回報。"""

    pipe: Any = None

    def _torch(self):
        try:
            import torch
        except ImportError as exc:
            raise RuntimeError(
                "此後端需要 PyTorch 與 diffusers,請先安裝:pip install -r requirements-gpu.txt "
                "(torch 請依 https://pytorch.org/get-started/locally/ 選擇對應 CUDA 版本)"
            ) from exc
        return torch

    @property
    def dtype(self):
        torch = self._torch()
        if self.device_info.kind == "cpu":
            return torch.float32
        return torch.bfloat16

    def _from_pretrained_kwargs(self) -> dict[str, Any]:
        kwargs: dict[str, Any] = {}
        if self.cache_dir:
            kwargs["cache_dir"] = self.cache_dir
        return kwargs

    def _apply_memory_mode(self, pipe) -> None:
        device = self.device_info.device
        mode = self.memory_mode
        log.info("記憶體模式: %s (裝置 %s)", mode, device)
        if mode == "full":
            pipe.to(device)
        elif mode == "model_offload":
            pipe.enable_model_cpu_offload(device=device)
        elif mode == "sequential_offload":
            pipe.enable_sequential_cpu_offload(device=device)
        else:
            raise ValueError(f"未知的 memory_mode: {mode!r}")
        vae = getattr(pipe, "vae", None)
        if vae is not None:
            for fn in ("enable_slicing", "enable_tiling"):
                if hasattr(vae, fn):
                    try:
                        getattr(vae, fn)()
                    except Exception as exc:  # noqa: BLE001
                        log.debug("VAE %s 失敗: %s", fn, exc)

    def _generator(self, seed: int):
        torch = self._torch()
        return torch.Generator("cpu").manual_seed(int(seed))

    def _step_callback(self, req: GenerationRequest):
        if req.progress is None:
            return None

        def _cb(pipe, step_index, timestep, callback_kwargs):
            req.progress(step_index + 1, req.num_inference_steps, "去噪中")
            return callback_kwargs

        return _cb

    def _unload(self) -> None:
        self.pipe = None
        try:
            import gc

            import torch

            gc.collect()
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
        except ImportError:
            pass

    @staticmethod
    def _output_to_frames(output) -> list[np.ndarray]:
        from ..export import to_uint8_frames

        frames = output.frames[0] if hasattr(output, "frames") else output
        return to_uint8_frames(frames)
