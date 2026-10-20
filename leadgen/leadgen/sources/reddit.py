"""Reddit 上的發案與代印請求貼文。

為什麼值得抓:個人接案者在 Reddit 完全沒有門檻 —— 不需要公司行號、不需要
平台帳號審核,看到貼文直接私訊就能談。對有 3D 列印機的人更直接,
r/3Dprintmything 整個板就是「幫我印這個」:

    [USA-IL] Drawer Insert for IKEA Helmer Storage Unit
    [US-MI] Nintendo Switch 2 Screen Cover in Black PETG
    [NJ, USA] Snowboard bevel guide

要先說清楚三件事:

1. **代印請求多半要寄送實體件**,發文者標的是美國州別。從台灣寄過去運費
   與時間都不划算 —— 真正吃得到的是 CAD 建模、逆向、修圖這種交檔案的部分,
   以及 r/forhire 上的軟體與韌體案。
2. **只能走官方 API,沒有別的路。** Reddit 的 robots.txt 對所有 user-agent
   是 `Disallow: /`,連公開的 .rss 都在內;未登入的請求另外還會收到 429。
   所以這個來源**沒有憑證就自動停用** —— 不會改用 RSS 或其他方式繞過。
3. 要兩個環境變數,免費、註冊一個 script app 兩分鐘:
       REDDIT_CLIENT_ID
       REDDIT_CLIENT_SECRET
   申請處:https://www.reddit.com/prefs/apps(選 "script" 類型)

各板的需求標記不一樣:r/forhire 用 [Hiring] 區分需求方,[For Hire] 是同行
在推銷自己,一定要排除。r/3Dprintmything 整板都是請求,不需要標記。
"""
from __future__ import annotations

import base64
import logging
import os
import re
import time
from datetime import datetime, timezone
from typing import Iterable

from bs4 import BeautifulSoup

from ..http import Fetcher
from ..models import Lead, extract_emails
from .base import register

log = logging.getLogger("leadgen.reddit")

TOKEN_URL = "https://www.reddit.com/api/v1/access_token"
OAUTH_BASE = "https://oauth.reddit.com"

DEFAULT_SUBS = [
    # 整板都是「幫我印這個」,不需要需求標記
    {"name": "3Dprintmything", "require": [], "skip": []},
    {"name": "PrintRequests", "require": [], "skip": []},
    # [Hiring] 是需求方,[For Hire] 是同行
    {"name": "forhire", "require": ["[hiring]"], "skip": ["[for hire]"]},
    {"name": "jobbit", "require": ["[hiring]"], "skip": []},
    {"name": "hwstartups", "require": [], "skip": []},
]

# 標題開頭的 [USA-IL] / [NJ, USA] 是發文者的所在地
_LOC = re.compile(r"^\[([^\]]{2,24})\]")


def _clean(html_or_text: str, limit: int = 800) -> str:
    if not html_or_text:
        return ""
    return BeautifulSoup(html_or_text, "lxml").get_text(" ", strip=True)[:limit]


def oauth_enabled() -> bool:
    return bool(os.getenv("REDDIT_CLIENT_ID") and os.getenv("REDDIT_CLIENT_SECRET"))


@register
class RedditSource:
    name = "reddit"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        subs = cfg.get("subreddits") or DEFAULT_SUBS
        limit = int(cfg.get("limit", 25))
        delay = float(cfg.get("delay_seconds", 8))
        keywords = [k.lower() for k in cfg.get("keywords", [])]

        if not oauth_enabled():
            log.warning(
                "Reddit 來源已停用:沒有 REDDIT_CLIENT_ID / REDDIT_CLIENT_SECRET。"
                "Reddit 的 robots.txt 是 Disallow: /(連 .rss 都包含在內),"
                "所以不會改用其他方式繞過。免費申請:"
                "https://www.reddit.com/prefs/apps(選 script 類型)"
            )
            return
        token = self._token(fetcher)
        if not token:
            return
        log.info("Reddit:使用官方 OAuth")

        for i, sub in enumerate(subs):
            name = sub["name"] if isinstance(sub, dict) else str(sub)
            require = [r.lower() for r in (sub.get("require") or [])] \
                if isinstance(sub, dict) else []
            skip = [s.lower() for s in (sub.get("skip") or [])] \
                if isinstance(sub, dict) else []

            if i:
                time.sleep(delay)      # 官方 API 也要客氣,拉開間隔

            posts = self._via_oauth(fetcher, name, limit, token)
            got = 0
            for post in posts:
                lead = self._to_lead(post, name, require, skip, keywords)
                if lead:
                    got += 1
                    yield lead
            log.info("  r/%s -> %s 筆", name, got)

    # ---------- 取得貼文 ----------
    def _token(self, fetcher: Fetcher) -> str | None:
        """app-only OAuth(client_credentials)。"""
        cid = os.environ["REDDIT_CLIENT_ID"]
        secret = os.environ["REDDIT_CLIENT_SECRET"]
        basic = base64.b64encode(f"{cid}:{secret}".encode()).decode()
        try:
            resp = fetcher.session.post(
                TOKEN_URL,
                data={"grant_type": "client_credentials"},
                headers={"Authorization": f"Basic {basic}",
                         "User-Agent": fetcher.ua},
                timeout=fetcher.timeout,
            )
            resp.raise_for_status()
            return resp.json().get("access_token")
        except Exception as e:
            log.warning("Reddit OAuth 取 token 失敗,改用 RSS:%s", e)
            return None

    def _via_oauth(self, fetcher: Fetcher, sub: str, limit: int,
                   token: str) -> list[dict]:
        try:
            resp = fetcher.session.get(
                f"{OAUTH_BASE}/r/{sub}/new",
                params={"limit": limit},
                headers={"Authorization": f"Bearer {token}",
                         "User-Agent": fetcher.ua},
                timeout=fetcher.timeout,
            )
            resp.raise_for_status()
            children = (resp.json().get("data") or {}).get("children") or []
        except Exception as e:
            log.warning("r/%s OAuth 讀取失敗:%s", sub, e)
            return []
        out = []
        for ch in children:
            d = ch.get("data") or {}
            out.append({
                "title": d.get("title", ""),
                "author": d.get("author", ""),
                "url": "https://www.reddit.com" + (d.get("permalink") or ""),
                "body": d.get("selftext", ""),
                "created": d.get("created_utc"),
            })
        return out

    # ---------- 轉成 Lead ----------
    def _to_lead(self, post: dict, sub: str, require: list[str],
                 skip: list[str], keywords: list[str]) -> Lead | None:
        title = post["title"]
        low = title.lower()
        if skip and any(s in low for s in skip):
            return None          # [For Hire] 是同行,不是客戶
        if require and not any(r in low for r in require):
            return None

        body = _clean(post.get("body") or "")
        if keywords and not any(k in f"{low}\n{body.lower()}" for k in keywords):
            return None

        created = post.get("created")
        sig_date = ""
        if isinstance(created, (int, float)):
            sig_date = datetime.fromtimestamp(
                created, tz=timezone.utc).date().isoformat()
        elif isinstance(created, str) and created:
            sig_date = created[:10]

        location = ""
        if m := _LOC.search(title):
            tag = m.group(1)
            # [Hiring] 這類標記不是地點
            if not any(w in tag.lower() for w in ("hiring", "for hire", "hire")):
                location = tag

        author = post.get("author") or "unknown"
        return Lead(
            org=f"Reddit u/{author}",
            source=self.name,
            source_url=post.get("url", ""),
            signal_title=re.sub(r"^\[[^\]]*\]\s*", "", title).strip()[:120],
            signal_type="freelance_request",
            signal_date=sig_date,
            location=location,
            emails=extract_emails(body),
            contacts_other=[f"Reddit 私訊 u/{author}"],
            tags=[f"r/{sub}"],
            evidence=body or title,
            notes=f"來自 r/{sub}",
        )
