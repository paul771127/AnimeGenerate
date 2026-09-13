"""後端註冊表。"""
from __future__ import annotations

from .base import BackendSpec, GenerationRequest, VideoBackend
from .cogvideox import CogVideoXBackend
from .ltx import LTXBackend
from .mock import MockBackend
from .wan import Wan21Backend, WanBackend

BACKENDS: dict[str, type[VideoBackend]] = {
    "wan22": WanBackend,
    "wan21": Wan21Backend,
    "ltx": LTXBackend,
    "cogvideox": CogVideoXBackend,
    "mock": MockBackend,
}


def get_backend_class(name: str) -> type[VideoBackend]:
    try:
        return BACKENDS[name]
    except KeyError as exc:
        raise ValueError(f"未知的後端 {name!r},可用: {', '.join(BACKENDS)}") from exc


def get_spec(name: str) -> BackendSpec:
    return get_backend_class(name).spec


def all_specs() -> list[BackendSpec]:
    return [cls.spec for cls in BACKENDS.values()]


__all__ = [
    "BACKENDS",
    "BackendSpec",
    "GenerationRequest",
    "VideoBackend",
    "all_specs",
    "get_backend_class",
    "get_spec",
]
