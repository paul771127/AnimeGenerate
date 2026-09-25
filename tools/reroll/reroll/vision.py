"""畫面辨識:模板比對、數字辨識。

模板都要在「和執行時相同解析度」的截圖上裁切(用 capture.py)。
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np


def imread(path) -> np.ndarray | None:
    """cv2.imread 在 Windows 讀不了中文路徑,改用 imdecode。"""
    p = Path(path)
    if not p.exists():
        return None
    return cv2.imdecode(np.fromfile(str(p), np.uint8), cv2.IMREAD_COLOR)


def imwrite(path, img: np.ndarray):
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    ok, buf = cv2.imencode(".png", img)
    if not ok:
        raise RuntimeError(f"無法寫入 {p}")
    buf.tofile(str(p))


@dataclass
class Match:
    x: int  # 中心點
    y: int
    score: float


class Templates:
    """依名稱載入 templates/<game>/<name>.png,並快取。"""

    def __init__(self, root: Path):
        self.root = Path(root)
        self._cache: dict[str, np.ndarray] = {}

    def get(self, name: str) -> np.ndarray:
        if name not in self._cache:
            p = self.root / (name if name.endswith(".png") else f"{name}.png")
            img = imread(p)
            if img is None:
                raise FileNotFoundError(f"找不到模板 {p},請用 capture.py 截取")
            self._cache[name] = img
        return self._cache[name]

    def exists(self, name: str) -> bool:
        return (self.root / f"{name}.png").exists()


def _crop(img: np.ndarray, region):
    if not region:
        return img, 0, 0
    x, y, w, h = region
    return img[y:y + h, x:x + w], x, y


def find(screen: np.ndarray, tpl: np.ndarray, threshold: float = 0.85, region=None) -> Match | None:
    area, ox, oy = _crop(screen, region)
    if area.shape[0] < tpl.shape[0] or area.shape[1] < tpl.shape[1]:
        return None
    res = cv2.matchTemplate(area, tpl, cv2.TM_CCOEFF_NORMED)
    _, score, _, (x, y) = cv2.minMaxLoc(res)
    if score < threshold:
        return None
    h, w = tpl.shape[:2]
    return Match(ox + x + w // 2, oy + y + h // 2, float(score))


def find_all(screen: np.ndarray, tpl: np.ndarray, threshold: float = 0.85, region=None) -> list[Match]:
    """找出所有出現位置(非極大值抑制,避免同一個位置重複算)。"""
    area, ox, oy = _crop(screen, region)
    if area.shape[0] < tpl.shape[0] or area.shape[1] < tpl.shape[1]:
        return []
    res = cv2.matchTemplate(area, tpl, cv2.TM_CCOEFF_NORMED)
    h, w = tpl.shape[:2]
    ys, xs = np.where(res >= threshold)
    cands = sorted(zip(res[ys, xs], xs, ys), reverse=True)
    picked: list[Match] = []
    for score, x, y in cands:
        cx, cy = ox + x + w // 2, oy + y + h // 2
        if all(abs(cx - m.x) >= w * 0.6 or abs(cy - m.y) >= h * 0.6 for m in picked):
            picked.append(Match(int(cx), int(cy), float(score)))
    return picked


def read_number(screen: np.ndarray, region, digits: dict[str, np.ndarray],
                threshold: float = 0.8) -> int | None:
    """用 0~9 的數字模板讀出區域內的整數(例如魔法石數量)。"""
    hits: list[tuple[int, str, float]] = []
    for d, tpl in digits.items():
        for m in find_all(screen, tpl, threshold, region):
            hits.append((m.x, d, m.score))
    hits.sort()
    # 同一個位置可能被兩個數字模板命中,留分數高的
    merged: list[tuple[int, str, float]] = []
    min_gap = min(t.shape[1] for t in digits.values()) * 0.6
    for h in hits:
        if merged and h[0] - merged[-1][0] < min_gap:
            if h[2] > merged[-1][2]:
                merged[-1] = h
        else:
            merged.append(h)
    if not merged:
        return None
    return int("".join(d for _, d, _ in merged))


def load_digits(root: Path) -> dict[str, np.ndarray]:
    out = {}
    for d in "0123456789":
        img = imread(Path(root) / f"{d}.png")
        if img is not None:
            out[d] = img
    if not out:
        raise FileNotFoundError(f"{root} 沒有數字模板 0.png~9.png")
    return out
