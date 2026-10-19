"""Lead 資料模型與正規化。

設計原則:只保存「公開張貼的商業聯絡資訊」,每筆都必須帶 source_url 與
collected_at,被對方詢問來源時拿得出來(個資法第 9 條:間接蒐集的告知義務)。
"""
from __future__ import annotations

import hashlib
import re
from dataclasses import dataclass, field, asdict
from datetime import datetime, timezone
from urllib.parse import urlparse

# 公司名稱雜訊:做去重 key 時要先剝掉
_ORG_NOISE = re.compile(
    r"(股份有限公司|有限公司|企業社|工作室|商行|公司|"
    r"co\.?,?\s*ltd\.?|corporation|corp\.?|inc\.?|llc|limited|gmbh)",
    re.IGNORECASE,
)
_NON_WORD = re.compile(r"[^\w一-鿿]+")
_EMAIL = re.compile(r"[\w.+-]+@[\w-]+\.[\w.-]+")
# 台灣市話 / 手機 / 國際碼。支援 (02)2911-2241、02-2911-2241、0912-345-678、+886-2-...
_PHONE = re.compile(
    r"(?:\+886[-\s.]?|\(0\d{1,3}\)[-\s.]?|0)"
    r"\d(?:[-\s.]?\d){7,10}"
)

# 角色信箱:公開張貼給外界聯絡用,風險最低,優先採用
ROLE_PREFIXES = (
    "info", "contact", "service", "sales", "hello", "support",
    "business", "bd", "rfq", "purchasing", "pm", "hr", "recruit",
)


def normalize_org(name: str) -> str:
    """公司名稱正規化,用於跨來源去重。"""
    if not name:
        return ""
    s = _ORG_NOISE.sub("", name)
    s = _NON_WORD.sub("", s).lower()
    return s


def registrable_domain(url_or_email: str) -> str:
    """從 URL 或 email 取出網域(不做 PSL 切割,保留完整 host 去掉 www)。"""
    if not url_or_email:
        return ""
    s = url_or_email.strip()
    if "@" in s and "://" not in s:
        host = s.rsplit("@", 1)[-1]
    else:
        if "://" not in s:
            s = "https://" + s
        host = urlparse(s).hostname or ""
    host = host.lower().removeprefix("www.")
    return host


def extract_emails(text: str) -> list[str]:
    seen, out = set(), []
    for m in _EMAIL.findall(text or ""):
        e = m.lower().rstrip(".")
        # 擋掉圖片檔名之類的誤判
        if e.endswith((".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg")):
            continue
        if e not in seen:
            seen.add(e)
            out.append(e)
    return out


def extract_phones(text: str) -> list[str]:
    seen, out = set(), []
    for m in _PHONE.findall(text or ""):
        p = re.sub(r"\D", "", m)
        if 9 <= len(p) <= 14 and p not in seen:
            seen.add(p)
            out.append(p)
    return out


def is_role_email(email: str) -> bool:
    local = email.split("@", 1)[0].lower()
    return any(local == p or local.startswith(p) for p in ROLE_PREFIXES)


@dataclass
class Lead:
    """一筆潛在客戶。source_url / collected_at / evidence 是合規必填欄位。"""

    org: str                      # 組織名稱(公司、機關、發案者)
    source: str                   # 來源 adapter 名稱
    source_url: str               # 這筆資料的公開出處
    signal_title: str = ""        # 訊號標題(標案名、職缺名、需求貼文標題)
    signal_type: str = ""         # tender / job / freelance_request / registry
    signal_date: str = ""         # ISO date,訊號發生時間
    website: str = ""
    emails: list[str] = field(default_factory=list)
    phones: list[str] = field(default_factory=list)
    location: str = ""
    budget_twd: int | None = None  # 統一換算成新台幣,無資料為 None
    tags: list[str] = field(default_factory=list)
    evidence: str = ""            # 原文節錄,寫信個人化時用
    notes: str = ""
    score: int = 0
    score_detail: dict = field(default_factory=dict)
    collected_at: str = ""
    status: str = "new"           # new / queued / contacted / replied / won / dead

    def __post_init__(self):
        if not self.collected_at:
            self.collected_at = datetime.now(timezone.utc).isoformat(timespec="seconds")
        self.org = (self.org or "").strip()
        self.emails = [e.lower() for e in dict.fromkeys(self.emails)]
        self.phones = list(dict.fromkeys(self.phones))

    @property
    def domain(self) -> str:
        return registrable_domain(self.website) or (
            registrable_domain(self.emails[0]) if self.emails else ""
        )

    @property
    def dedupe_key(self) -> str:
        """有網域就用網域,否則用正規化公司名 + 訊號標題。"""
        d = self.domain
        if d:
            base = f"domain:{d}"
        elif self.org:
            base = f"org:{normalize_org(self.org)}"
        else:
            base = f"url:{self.source_url}"
        return hashlib.sha1(base.encode()).hexdigest()[:16]

    @property
    def signal_key(self) -> str:
        """同一組織的不同訊號要分開保存(一間公司可能發三個標案)。"""
        raw = f"{self.dedupe_key}|{self.source}|{self.signal_title}|{self.signal_date}"
        return hashlib.sha1(raw.encode()).hexdigest()[:16]

    @property
    def has_contact(self) -> bool:
        return bool(self.emails or self.phones)

    def to_row(self) -> dict:
        d = asdict(self)
        d["dedupe_key"] = self.dedupe_key
        d["signal_key"] = self.signal_key
        d["domain"] = self.domain
        return d
