"""用 Google Programmable Search JSON API 找公司官網(選用)。

為什麼要走官方 API:直接爬搜尋引擎結果頁違反 ToS 而且很快被擋。
Programmable Search 有免費額度(每天 100 次查詢),設定兩個環境變數就能用:
    GOOGLE_CSE_KEY  API key
    GOOGLE_CSE_CX   search engine id(建立時選「搜尋整個網路」)
沒設定就自動停用,tw_company 這類沒有官網欄位的來源會留著空的 website。
"""
from __future__ import annotations

import logging
import os
from urllib.parse import urlparse

from ..http import Fetcher

log = logging.getLogger("leadgen.domain_lookup")

ENDPOINT = "https://www.googleapis.com/customsearch/v1"
# 這些網域是平台頁而不是公司官網
SKIP_HOSTS = (
    "facebook.com", "linkedin.com", "104.com.tw", "1111.com.tw", "yes123.com.tw",
    "wikipedia.org", "twincn.com", "find.taipei", "cmoney.tw", "youtube.com",
    "instagram.com", "g0v.ronny.tw", "gov.tw",
)


def enabled() -> bool:
    return bool(os.getenv("GOOGLE_CSE_KEY") and os.getenv("GOOGLE_CSE_CX"))


def find_website(fetcher: Fetcher, org: str, hint: str = "") -> str:
    if not enabled() or not org:
        return ""
    q = f"{org} {hint}".strip() + " 官網"
    try:
        d = fetcher.get_json(ENDPOINT, params={
            "key": os.environ["GOOGLE_CSE_KEY"],
            "cx": os.environ["GOOGLE_CSE_CX"],
            "q": q,
            "num": 5,
        })
    except Exception as e:
        log.warning("官網查詢失敗 %s: %s", org, e)
        return ""
    for item in d.get("items") or []:
        host = (urlparse(item.get("link", "")).hostname or "").lower()
        if host and not any(s in host for s in SKIP_HOSTS):
            return f"https://{host}"
    return ""
