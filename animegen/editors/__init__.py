"""圖片編輯後端註冊表(多張關鍵姿勢圖 → GIF 模式使用)。"""
from __future__ import annotations

from ..device import DeviceInfo
from .base import EditorSpec, EditRequest, ImageEditor
from .kontext import KontextEditor
from .mock import MockEditor
from .qwen import QwenEditor

EDITORS: dict[str, type[ImageEditor]] = {
    "qwen": QwenEditor,
    "kontext": KontextEditor,
    "mock": MockEditor,
}


def get_editor_class(name: str) -> type[ImageEditor]:
    try:
        return EDITORS[name]
    except KeyError as exc:
        raise ValueError(f"未知的圖片模型 {name!r},可用: {', '.join(EDITORS)}") from exc


def choose_editor(info: DeviceInfo) -> tuple[str, str]:
    """依硬體挑選圖片編輯模型,回傳 (名稱, 原因)。"""
    if not info.torch_available:
        return "mock", "未安裝 torch / diffusers,改用 mock(只用來測試流程)"
    if info.kind == "cpu":
        return "mock", "沒有偵測到 GPU;圖片編輯模型在 CPU 上太慢,改用 mock"
    if info.vram_gb >= QwenEditor.spec.vram_4bit_gb - 0.5:
        return "qwen", f"VRAM {info.vram_gb:.0f} GB → Qwen-Image-Edit-2509"
    return "kontext", f"VRAM {info.vram_gb:.0f} GB → FLUX.1 Kontext(4-bit)"


__all__ = ["EDITORS", "EditRequest", "EditorSpec", "ImageEditor", "choose_editor", "get_editor_class"]
