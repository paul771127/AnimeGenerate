"""從貼文文字中抽取價格。

只認「有貨幣符號或價格關鍵字」的數字,避免把年份、電話、型號當成價格。
支援:NT$1,200、NTD 1200、$1200、1200元、1,200 元、1.2萬、3萬5、2k、
      售價 800、價格:800、開價800、每個 50 元……
"""

from __future__ import annotations

import re

_NUM = r"\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:\.\d+)?"

# 萬 / 千 / k 單位,例如 1.2萬、3萬5、2千、2k
_UNIT = r"(?P<unit>萬|w|W|千|k|K)(?P<tail>\d)?(?!\d)"

_PATTERNS = [
    # 貨幣符號在前:NT$ 1,200 / NTD1200 / $ 1200 / TWD 800 / US$30
    re.compile(
        r"(?:NT\$|NTD|TWD|US\$|USD|HK\$|\$|＄)\s*(?P<num>" + _NUM + r")\s*(?:" + _UNIT + r")?",
        re.IGNORECASE,
    ),
    # 關鍵字在前:售價 800、價格:1.2萬、開價800元、只要 500
    re.compile(
        r"(?:售價|價格|價錢|定價|開價|原價|特價|優惠價|售|賣|只要|僅|price)\s*[:：=]?\s*"
        r"(?:NT\$|NTD|\$|＄)?\s*(?P<num>" + _NUM + r")\s*(?:" + _UNIT + r")?",
        re.IGNORECASE,
    ),
    # 單位在後:1200元、1,200 塊、1.5萬元、800 NTD、30 USD
    re.compile(
        r"(?P<num>" + _NUM + r")\s*(?:" + _UNIT + r")?\s*(?:元|塊|圓|NTD|TWD|USD|\$)",
        re.IGNORECASE,
    ),
    # 只有萬:3萬5、1.2萬(沒寫「元」也視為價格)
    re.compile(r"(?P<num>\d+(?:\.\d+)?)\s*(?P<unit>萬)(?P<tail>\d)?(?!\d)"),
]


def _to_amount(m: re.Match) -> float | None:
    try:
        value = float(m.group("num").replace(",", ""))
    except (TypeError, ValueError):
        return None
    unit = (m.groupdict().get("unit") or "").lower()
    tail = m.groupdict().get("tail")
    if unit in ("萬", "w"):
        value *= 10000
        if tail:  # 3萬5 → 35000
            value += int(tail) * 1000
    elif unit in ("千", "k"):
        value *= 1000
        if tail:  # 2k5 → 2500
            value += int(tail) * 100
    return value


def extract_prices(text: str | None) -> list[float]:
    """回傳文字中出現的所有價格(依出現位置排序、去重)。"""
    if not text:
        return []
    found: dict[int, float] = {}
    spans: list[tuple[int, int]] = []
    for pat in _PATTERNS:
        for m in pat.finditer(text):
            start, end = m.span("num")
            if any(s <= start < e for s, e in spans):
                continue  # 已被前面的樣式抓過
            amount = _to_amount(m)
            if amount is None or amount <= 0 or amount > 1e9:
                continue
            spans.append((start, end))
            found[start] = amount
    out: list[float] = []
    for _, v in sorted(found.items()):
        if v not in out:
            out.append(v)
    return out


def extract_price(text: str | None) -> float | None:
    """回傳第一個出現的價格,沒有則 None。"""
    prices = extract_prices(text)
    return prices[0] if prices else None
