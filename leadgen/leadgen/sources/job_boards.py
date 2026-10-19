"""遠端職缺板:Remote OK 官方 API + We Work Remotely RSS。

邏輯:一間公司在徵 firmware / embedded / hardware / mechanical 工程師,代表它
有專案但人力不足 —— 這是外包與人力支援最常見的切入點。而且國外客戶的預算
通常比國內高一個級距。

為什麼選這兩個:它們的 robots.txt 允許抓取(Remote OK 的 /api 與 WWR 的 RSS
都是公開給程式讀的),而 Remotive 的 robots.txt 明文 `Disallow: /api/*`,
所以不收錄 —— 要用它請先向對方取得書面同意。

Remote OK 的 API ToS 要求標註來源並連回原頁,所以每筆 lead 的 source_url
都保留它的職缺頁連結,匯出時不要拿掉。

職缺頁本身不給 email,這個來源負責「產生目標公司清單」,
聯絡方式由 site_contact enricher 從公司官網的公開聯絡頁取得。
"""
from __future__ import annotations

import html
import logging
import re
from datetime import datetime
from typing import Iterable

from bs4 import BeautifulSoup
from lxml import etree

from ..http import Fetcher
from ..models import Lead
from .base import register

log = logging.getLogger("leadgen.job_boards")

REMOTEOK_API = "https://remoteok.com/api"
WWR_RSS = "https://weworkremotely.com/remote-jobs.rss"

USD_TWD = 32  # 粗估匯率,只用來做預算級距比較


def _text(frag: str, limit: int = 800) -> str:
    if not frag:
        return ""
    return BeautifulSoup(html.unescape(frag), "lxml").get_text(" ", strip=True)[:limit]


def _match(haystack: str, keywords: list[str]) -> list[str]:
    """英文關鍵字做詞界比對,避免 "driver" 命中 "Screwdriver" 之類的東西。"""
    low = haystack.lower()
    out = []
    for k in keywords:
        kl = k.lower()
        pat = rf"\b{re.escape(kl)}\b" if kl.isascii() else re.escape(kl)
        if re.search(pat, low):
            out.append(k)
    return out


@register
class JobBoards:
    name = "job_boards"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        keywords: list[str] = cfg.get("keywords", [])
        # 關鍵字必須出現在職稱或標籤裡。只在內文出現的不算 —— 不然
        # "Senior Sales Executive" 會因為公司簡介提到 firmware 就被撈進來。
        cfg = {**cfg, "require_title_match": cfg.get("require_title_match", True)}
        if cfg.get("remoteok", True):
            yield from self._remoteok(fetcher, keywords, cfg)
        if cfg.get("weworkremotely", True):
            yield from self._wwr(fetcher, keywords, cfg)

    # ---------- Remote OK ----------
    def _remoteok(self, fetcher: Fetcher, keywords: list[str], cfg: dict) -> Iterable[Lead]:
        try:
            rows = fetcher.get_json(REMOTEOK_API)
        except Exception as e:
            log.warning("Remote OK 讀取失敗:%s", e)
            return
        for job in rows:
            if "legal" in job:        # 第一筆是 ToS 聲明
                continue
            desc = _text(job.get("description", ""))
            # Remote OK 的 tags 品質很差(一個運輸規劃師會被貼上 engineer/medical/hr),
            # 拿它比對會把業務職缺也撈進來,所以只認職稱。
            title_hay = job.get("position", "")
            matched = _match(title_hay, keywords)
            if not matched and not cfg["require_title_match"]:
                matched = _match(desc, keywords)
            if keywords and not matched:
                continue

            budget = None
            if job.get("salary_min"):
                try:
                    budget = int(float(job["salary_min"]) * USD_TWD)
                except (TypeError, ValueError):
                    budget = None

            yield Lead(
                org=job.get("company", "") or "",
                source=self.name,
                source_url=job.get("url") or job.get("apply_url") or "",
                signal_title=job.get("position", ""),
                signal_type="job",
                signal_date=(job.get("date") or "")[:10],
                location=job.get("location", "") or "",
                budget_twd=budget,
                tags=matched + (job.get("tags") or [])[:6],
                evidence=desc,
                notes="來源 Remote OK(匯出時請保留 source_url,其 API 條款要求標註來源)",
            )

    # ---------- We Work Remotely ----------
    def _wwr(self, fetcher: Fetcher, keywords: list[str], cfg: dict) -> Iterable[Lead]:
        try:
            xml = fetcher.get(WWR_RSS)
        except Exception as e:
            log.warning("WWR RSS 讀取失敗:%s", e)
            return
        try:
            root = etree.fromstring(xml.encode("utf-8", "replace"))
        except etree.XMLSyntaxError as e:
            log.warning("WWR RSS 解析失敗:%s", e)
            return

        for item in root.iterfind(".//item"):
            def g(tag: str) -> str:
                el = item.find(tag)
                return (el.text or "").strip() if el is not None and el.text else ""

            title = g("title")
            desc = _text(g("description"))
            title_hay = f"{title} {g('category')} {g('skills')}"
            matched = _match(title_hay, keywords)
            if not matched and not cfg["require_title_match"]:
                matched = _match(desc, keywords)
            if keywords and not matched:
                continue

            # WWR 的 title 格式是 "Company: Position"
            org, _, position = title.partition(":")
            if not position:
                org, position = "", title

            pub = g("pubDate")
            try:
                sig_date = datetime.strptime(pub[:25].strip(), "%a, %d %b %Y %H:%M:%S").date().isoformat()
            except ValueError:
                sig_date = ""

            yield Lead(
                org=org.strip(),
                source=self.name,
                source_url=g("link"),
                signal_title=position.strip(),
                signal_type="job",
                signal_date=sig_date,
                location=g("region") or g("country"),
                tags=matched + [t for t in [g("category"), g("type")] if t],
                evidence=desc,
                notes="來源 We Work Remotely RSS",
            )
