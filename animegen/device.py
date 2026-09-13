"""裝置與 VRAM 偵測,以及依硬體自動挑選後端 / 記憶體模式。"""
from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass
class DeviceInfo:
    kind: str  # cuda | mps | cpu | none
    name: str
    vram_gb: float
    torch_available: bool

    @property
    def device(self) -> str:
        return self.kind if self.kind in ("cuda", "mps") else "cpu"

    @property
    def has_gpu(self) -> bool:
        return self.kind in ("cuda", "mps")

    def describe(self) -> str:
        if not self.torch_available:
            return "未安裝 PyTorch(只能使用 mock 後端)"
        if self.kind == "cpu":
            return "CPU(沒有可用的 GPU)"
        return f"{self.name} / {self.vram_gb:.1f} GB"


def _system_ram_gb() -> float:
    try:
        pages = os.sysconf("SC_PHYS_PAGES")
        page_size = os.sysconf("SC_PAGE_SIZE")
        return pages * page_size / 2**30
    except (ValueError, OSError, AttributeError):
        return 16.0


def detect_device(prefer: str = "auto") -> DeviceInfo:
    """偵測可用裝置。torch 未安裝時回傳 kind="none"。"""
    try:
        import torch
    except ImportError:
        return DeviceInfo("none", "(torch 未安裝)", 0.0, False)

    if prefer in ("auto", "cuda") and torch.cuda.is_available():
        props = torch.cuda.get_device_properties(0)
        return DeviceInfo("cuda", props.name, props.total_memory / 2**30, True)

    mps = getattr(torch.backends, "mps", None)
    if prefer in ("auto", "mps") and mps is not None and mps.is_available():
        # Apple Silicon 是統一記憶體,拿系統 RAM 的 3/4 當可用量的估計
        return DeviceInfo("mps", "Apple Silicon (MPS)", _system_ram_gb() * 0.75, True)

    return DeviceInfo("cpu", "CPU", 0.0, True)


def choose_backend(info: DeviceInfo) -> tuple[str, str]:
    """依硬體挑選預設後端,回傳 (後端名稱, 原因)。"""
    if not info.torch_available:
        return "mock", "未安裝 torch / diffusers,改用 mock 後端(只會做簡單位移動畫,用來測試流程)"
    if info.kind == "cpu":
        return "mock", "沒有偵測到 GPU;影片擴散模型在 CPU 上要跑數小時,改用 mock 後端"
    vram = info.vram_gb
    if vram >= 10:
        return "wan22", f"VRAM {vram:.0f} GB → Wan2.2 TI2V-5B(品質最好)"
    if vram >= 6:
        return "ltx", f"VRAM {vram:.0f} GB → LTX-Video 2B(速度快、需求低)"
    return "cogvideox", f"VRAM {vram:.0f} GB → CogVideoX-5B-I2V + sequential offload(慢但能跑)"


def choose_memory_mode(vram_gb: float, kind: str, vram_full_gb: float, vram_offload_gb: float) -> str:
    """依 VRAM 與後端需求決定記憶體模式。"""
    if kind == "mps":
        return "full" if vram_gb >= vram_offload_gb else "model_offload"
    if vram_gb >= vram_full_gb:
        return "full"
    if vram_gb >= vram_offload_gb:
        return "model_offload"
    return "sequential_offload"
