"""匯入 Facebook「下載你的資訊」(Download Your Information)的 JSON 匯出。

Facebook 匯出的檔案結構常改版,所以這裡不綁死路徑,而是:
  1. 依「檔名 / 區塊名稱」判斷來源(觀看紀錄、收藏、自己的貼文、Marketplace)
  2. 遞迴走訪 JSON,凡是帶有 timestamp 的物件就視為一筆紀錄,盡量抽出標題、內文、網址、價格

也順便修正 Facebook 匯出的經典亂碼(UTF-8 被當成 latin-1 跳脫,中文變成 "Ã¤Â¸Â­")。

可接受:單一 .json、整個解壓後的資料夾、或原始 .zip 檔。
"""

from __future__ import annotations

import json
import re
import zipfile
from pathlib import Path
from typing import Any, Iterator

from .db import Item

# 只看這些檔名關鍵字的檔案(其他如訊息、好友清單、廣告偏好等都略過,保護隱私也加快速度)
_RELEVANT = re.compile(
    r"recently_viewed|recently_visited|saved_items|saved|collections|your_posts|posts"
    r"|videos|watch|marketplace|group_posts|groups|comments|reels|likes_and_reactions",
    re.IGNORECASE,
)
_SKIP = re.compile(r"message|inbox|friend|ads_|location|security|login|search_history"
                   r"|contact|device|payment|profile_information", re.IGNORECASE)

_URL_KEYS = ("url", "uri", "href", "permalink", "link", "source")
_TITLE_KEYS = ("title", "name")
_TEXT_KEYS = ("post", "text", "description", "comment", "value", "body")
_PRICE_KEYS = ("price", "listing_price", "amount")


def fix_mojibake(s: str) -> str:
    """把 Facebook 匯出的 latin-1 亂碼還原成 UTF-8。無法還原時原樣返回。"""
    if not s or all(ord(c) < 128 for c in s):
        return s
    try:
        return s.encode("latin-1").decode("utf-8")
    except (UnicodeEncodeError, UnicodeDecodeError):
        return s


def _fix(obj: Any) -> Any:
    if isinstance(obj, str):
        return fix_mojibake(obj)
    if isinstance(obj, list):
        return [_fix(x) for x in obj]
    if isinstance(obj, dict):
        return {fix_mojibake(k): _fix(v) for k, v in obj.items()}
    return obj


# ---------------------------------------------------------------- 分類規則
def classify(context: str) -> tuple[str, str]:
    """依檔名 + 區塊名稱(已轉小寫的一串文字)推斷 (kind, source)。"""
    c = context.lower()
    # 「你的貼文、打卡、相片和影片」這種檔名不代表每一筆都是影片
    c = re.sub(r"check.ins.{0,3}photos.and.videos", " ", c)
    if "marketplace" in c or "市集" in c:
        kind = "marketplace"
    elif any(k in c for k in ("video", "watch", "reel", "影片", "直播", "live")):
        kind = "video"
    elif "photo" in c or "相片" in c:
        kind = "photo"
    elif any(k in c for k in ("link", "article", "external", "連結")):
        kind = "link"
    else:
        kind = "post"

    if any(k in c for k in ("recently_viewed", "recently viewed", "viewed", "watched",
                            "visited", "最近觀看", "看過", "觀看")):
        source = "viewed"
    elif any(k in c for k in ("saved", "collection", "收藏", "儲存")):
        source = "saved"
    elif any(k in c for k in ("your_posts", "your posts", "group_posts", "items_sold",
                              "listings", "comments", "你的貼文")):
        source = "posted"
    else:
        source = "other"
    return kind, source


# ---------------------------------------------------------------- 欄位抽取
def _walk_strings(obj: Any, keys: tuple[str, ...], depth: int = 0) -> Iterator[str]:
    """在物件(含巢狀的 data/attachments)裡找指定 key 的字串值。"""
    if depth > 6:
        return
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k in keys and isinstance(v, str) and v.strip():
                yield v.strip()
        for k, v in obj.items():
            if isinstance(v, (dict, list)) and k not in ("entries", "children"):
                yield from _walk_strings(v, keys, depth + 1)
    elif isinstance(obj, list):
        for v in obj:
            yield from _walk_strings(v, keys, depth + 1)


def _first_url(rec: dict) -> str:
    for s in _walk_strings(rec, _URL_KEYS):
        if s.startswith("http"):
            return s
    return ""


_BOILERPLATE = re.compile(
    r"(更新了|分享了|儲存了|發佈了|張貼了|新增了|留言|回應了|讚了).{0,20}[。.]?$"
    r"|\b(updated (his|her|their) status|shared an? |saved an? |posted |commented on|was live)",
    re.IGNORECASE,
)

_VIDEO_URL = re.compile(r"facebook\.com/(?:watch|reel|[^/]+/videos/)|fb\.watch/|/videos?/", re.I)


def _looks_like_video(rec: dict, url: str) -> bool:
    if url and _VIDEO_URL.search(url):
        return True
    return any(s.lower().endswith((".mp4", ".mov", ".webm"))
               for s in _walk_strings(rec, ("uri",)))


def _price(rec: dict) -> float | None:
    for k in _PRICE_KEYS:
        v = rec.get(k)
        if isinstance(v, (int, float)) and not isinstance(v, bool):
            return float(v)
        if isinstance(v, str):
            m = re.search(r"\d[\d,]*(?:\.\d+)?", v)
            if m:
                try:
                    return float(m.group().replace(",", ""))
                except ValueError:
                    pass
    for v in rec.values():
        if isinstance(v, dict):
            p = _price(v)
            if p is not None:
                return p
    return None


def _record_to_item(rec: dict, context: str, origin: str) -> Item | None:
    ts = rec.get("timestamp") or rec.get("creation_timestamp") or rec.get("time") or 0
    try:
        ts = int(ts)
    except (TypeError, ValueError):
        ts = 0
    if ts > 10**12:  # 毫秒
        ts //= 1000

    # 物件自己的 title/name 優先,其次是 data/attachments 裡的
    title = ""
    for k in _TITLE_KEYS:
        if isinstance(rec.get(k), str) and rec[k].strip():
            title = rec[k].strip()
            break
    texts: list[str] = []
    for s in _walk_strings(rec, _TEXT_KEYS + _TITLE_KEYS):
        if s != title and s not in texts and not s.startswith("http"):
            texts.append(s)
    if texts and (not title or _BOILERPLATE.search(title)):
        # 「王小明更新了他的動態。」這類系統句子沒有資訊量,改用真正的內文當標題
        if title:
            texts.append(title)
        title = texts.pop(0)
    text = "\n".join(texts)
    url = _first_url(rec)
    if not (title or text or url):
        return None

    kind, source = classify(f"{context} {title}")
    if kind in ("post", "link", "photo") and _looks_like_video(rec, url):
        kind = "video"
    author = ""
    for k in ("author", "seller", "actor", "owner"):
        if isinstance(rec.get(k), str):
            author = rec[k]
            break
    return Item(kind=kind, source=source, title=title[:500], text=text[:5000], url=url,
                author=author, price=_price(rec), ts=ts, origin=origin)


def _extract(obj: Any, context: str, origin: str, depth: int = 0) -> Iterator[Item]:
    if depth > 12:
        return
    if isinstance(obj, list):
        for v in obj:
            yield from _extract(v, context, origin, depth + 1)
        return
    if not isinstance(obj, dict):
        return
    # 帶 name 的區塊(例如 "Videos you have watched")會影響底下紀錄的分類
    label = obj.get("name") if isinstance(obj.get("name"), str) else ""
    has_ts = any(k in obj for k in ("timestamp", "creation_timestamp"))
    has_children = any(isinstance(obj.get(k), list) for k in ("entries", "children"))
    if has_ts and not has_children:
        item = _record_to_item(obj, context, origin)
        if item:
            yield item
        return
    sub_ctx = f"{context} {label}" if label else context
    for k, v in obj.items():
        if isinstance(v, (list, dict)):
            yield from _extract(v, f"{sub_ctx} {k}", origin, depth + 1)


def parse_json(data: Any, name: str) -> list[Item]:
    """解析一份已讀入的 JSON。name 是檔案路徑(用來判斷來源)。"""
    data = _fix(data)
    return list(_extract(data, name.replace("/", " ").replace("_", " ") + " " + name, name))


def _relevant(name: str) -> bool:
    n = name.lower()
    return n.endswith(".json") and bool(_RELEVANT.search(n)) and not _SKIP.search(n)


def iter_export(path: str | Path, all_files: bool = False) -> Iterator[tuple[str, list[Item]]]:
    """逐檔解析一個匯出(.json / 資料夾 / .zip),產生 (檔名, 紀錄清單)。"""
    p = Path(path)
    if p.is_file() and p.suffix.lower() == ".zip":
        with zipfile.ZipFile(p) as zf:
            for info in zf.infolist():
                if info.is_dir() or not (all_files or _relevant(info.filename)):
                    continue
                if not info.filename.lower().endswith(".json"):
                    continue
                try:
                    data = json.loads(zf.read(info).decode("utf-8"))
                except (ValueError, UnicodeDecodeError):
                    continue
                yield info.filename, parse_json(data, info.filename)
    elif p.is_dir():
        for f in sorted(p.rglob("*.json")):
            rel = f.relative_to(p).as_posix()
            if not (all_files or _relevant(rel)):
                continue
            try:
                data = json.loads(f.read_text(encoding="utf-8"))
            except (ValueError, UnicodeDecodeError):
                continue
            yield rel, parse_json(data, rel)
    elif p.is_file():
        data = json.loads(p.read_text(encoding="utf-8"))
        yield p.name, parse_json(data, p.name)
    else:
        raise FileNotFoundError(path)
