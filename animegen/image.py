"""輸入圖片處理:去透明、決定解析度、裁切 / 補邊到目標尺寸。"""
from __future__ import annotations

import math
import re
from typing import Sequence

from PIL import Image

_RES_RE = re.compile(r"^\s*(\d+)\s*[xX×*]\s*(\d+)\s*$")


def parse_resolution(text: str | None) -> tuple[int, int] | None:
    """把 "832x480" 這類字串解析成 (width, height);auto / 空字串回傳 None。"""
    if not text or str(text).strip().lower() in ("auto", "none", ""):
        return None
    m = _RES_RE.match(str(text))
    if not m:
        raise ValueError(f"解析度格式錯誤: {text!r},請用 寬x高,例如 832x480")
    w, h = int(m.group(1)), int(m.group(2))
    if w <= 0 or h <= 0:
        raise ValueError("解析度必須是正整數")
    return w, h


def round_to_mod(value: int, mod: int, minimum: int | None = None) -> int:
    """向下取到 mod 的倍數,但不低於 minimum(預設 mod)。"""
    minimum = mod if minimum is None else minimum
    return max(minimum, (int(value) // mod) * mod)


def fit_resolution(width: int, height: int, max_area: int, mod: int) -> tuple[int, int]:
    """維持長寬比,把解析度縮放到不超過 max_area,且寬高都是 mod 的倍數。"""
    aspect = height / width
    new_h = round_to_mod(round(math.sqrt(max_area * aspect)), mod)
    new_w = round_to_mod(round(math.sqrt(max_area / aspect)), mod)
    return new_w, new_h


def flatten_alpha(img: Image.Image, background: Sequence[int] = (255, 255, 255)) -> Image.Image:
    """把透明 PNG 疊在純色底上,輸出 RGB。"""
    if img.mode in ("RGBA", "LA") or (img.mode == "P" and "transparency" in img.info):
        rgba = img.convert("RGBA")
        bg = Image.new("RGBA", rgba.size, tuple(background) + (255,))
        bg.alpha_composite(rgba)
        return bg.convert("RGB")
    return img.convert("RGB")


def prepare_image(
    img: Image.Image,
    width: int,
    height: int,
    fit_mode: str = "crop",
    background: Sequence[int] = (255, 255, 255),
) -> Image.Image:
    """把圖片轉成 width x height 的 RGB 圖。

    crop:   等比縮放後置中裁切(不變形、不留邊,可能裁掉一點邊緣)
    pad:    等比縮放後置中補邊(完整保留、會出現底色邊框)
    stretch: 直接拉伸
    """
    img = flatten_alpha(img, background)
    if fit_mode == "stretch":
        return img.resize((width, height), Image.LANCZOS)

    src_w, src_h = img.size
    scale_crop = max(width / src_w, height / src_h)
    scale_pad = min(width / src_w, height / src_h)
    scale = scale_crop if fit_mode == "crop" else scale_pad
    new_w = max(1, round(src_w * scale))
    new_h = max(1, round(src_h * scale))
    resized = img.resize((new_w, new_h), Image.LANCZOS)

    if fit_mode == "crop":
        left = (new_w - width) // 2
        top = (new_h - height) // 2
        return resized.crop((left, top, left + width, top + height))
    if fit_mode == "pad":
        canvas = Image.new("RGB", (width, height), tuple(background))
        canvas.paste(resized, ((width - new_w) // 2, (height - new_h) // 2))
        return canvas
    raise ValueError(f"未知的 fit_mode: {fit_mode!r}(可用 crop / pad / stretch)")
