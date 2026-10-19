"""來源 adapter 介面。

每個 adapter 只負責「把某個公開資料源變成 Lead 串流」,不做去重、不做評分、
不做寄信。新增來源只要實作 fetch() 並掛進 registry。
"""
from __future__ import annotations

import logging
from typing import Iterable, Protocol

from ..http import Fetcher
from ..models import Lead

log = logging.getLogger("leadgen.sources")


class Source(Protocol):
    name: str

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]: ...


_REGISTRY: dict[str, type] = {}


def register(cls):
    _REGISTRY[cls.name] = cls
    return cls


def get_source(name: str):
    if name not in _REGISTRY:
        raise KeyError(f"未知來源 '{name}',可用:{sorted(_REGISTRY)}")
    return _REGISTRY[name]()


def available() -> list[str]:
    return sorted(_REGISTRY)


def load_all():
    """import 所有 adapter 模組以觸發 register。"""
    from . import gov_tender, hn_freelance, job_boards, tw_company  # noqa: F401
