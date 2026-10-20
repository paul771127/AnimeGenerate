"""Hacker News 的每月外包串與徵才串(走官方 Algolia API)。

兩種訊號:
  * "Ask HN: Freelancer? Seeking freelancer?" 裡的 SEEKING FREELANCER 留言
    = 有人公開說「我要找外包」,而且自己把聯絡方式貼在留言裡。意圖最高。
  * "Ask HN: Who is hiring?" 裡提到 firmware / embedded / hardware 的公司
    = 有硬體專案但缺人,是外包的切入點。

這些人是自願把聯絡方式公開張貼在公開論壇上徵求接案者,所以蒐集與使用的
目的跟他們張貼的目的一致 —— 這是整套系統裡少數可以直接寫信的來源。
"""
from __future__ import annotations

import html
import logging
import re
import time
from typing import Iterable

from bs4 import BeautifulSoup

from ..http import Fetcher
from ..models import Lead, extract_emails, extract_urls
from .base import register

log = logging.getLogger("leadgen.hn")

ALGOLIA = "https://hn.algolia.com/api/v1"
HN_ITEM = "https://news.ycombinator.com/item?id="

SEEKING_FREELANCER = re.compile(r"seeking\s*freelancer", re.IGNORECASE)
SEEKING_WORK = re.compile(r"seeking\s*work", re.IGNORECASE)


def _text(html_fragment: str) -> str:
    if not html_fragment:
        return ""
    soup = BeautifulSoup(html.unescape(html_fragment), "lxml")
    for br in soup.find_all(["br", "p"]):
        br.insert_after("\n")
    return re.sub(r"\n{3,}", "\n\n", soup.get_text(" ", strip=True))


def _first_line(text: str, limit: int = 120) -> str:
    for line in text.splitlines():
        line = line.strip(" |-–—•")
        if len(line) > 3:
            return line[:limit]
    return text[:limit]


# HN 徵才串的慣例格式是「公司 | 職稱 | 地點 | 其他」
_ORG_CUT = re.compile(
    r"\s+(?:is hiring|is looking|are hiring|are looking|seeks?|seeking|"
    r"wants?|needs?|needed|requires?)\b",
    re.IGNORECASE,
)
_SEEKING_PREFIX = re.compile(r"^\s*seeking\s*(freelancer|work)\b[\s:|–—-]*", re.IGNORECASE)
_URL_IN_TEXT = re.compile(r"https?://\S+|\bwww\.\S+")
_SENTENCE_START = re.compile(r'["“”(（\[]')


def _org_name(text: str, limit: int = 60) -> str:
    """從留言開頭取出組織名,而不是整段第一句。

    原本直接用第一行,結果 org 變成「Zoo.dev is hiring a Senior Frontend
    Software Engineer with a core focus on 3D web application performance...」。
    """
    head = _first_line(text, 300)
    head = _URL_IN_TEXT.sub(" ", head)              # 公司名裡不要塞網址
    head = _SEEKING_PREFIX.sub("", head)            # 去掉「SEEKING FREELANCER」前綴
    head = _SENTENCE_START.split(head, maxsplit=1)[0]  # 切在引號或括號開始的句子前
    head = " ".join(head.split())

    def ok(v: str) -> str | None:
        v = v.strip(" -–—:·,|")
        return v if 2 < len(v) <= limit else None

    if "|" in head and (v := ok(head.split("|", 1)[0])):
        return v
    if (v := ok(_ORG_CUT.split(head, maxsplit=1)[0])):
        return v
    return " ".join(head.split()[:6])[:limit]


@register
class HNFreelance:
    name = "hn_freelance"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        skills: list[str] = [s.lower() for s in cfg.get("skill_keywords", [])]
        months = int(cfg.get("threads", 3))
        include_hiring = bool(cfg.get("include_who_is_hiring", True))
        # 太舊的串不要抓:「Ask HN: Freelancer? Seeking freelancer?」這個每月串
        # 在 2025-10 之後就沒再發了,照 threads 數去翻會翻到一年前的貼文,
        # 那些聯絡人早就不在。預設只收 120 天內的串。
        max_age = int(cfg.get("max_thread_age_days", 120))

        threads = self._find_threads(fetcher, "Freelancer", months, max_age)
        if include_hiring:
            threads += self._find_threads(fetcher, "Who is hiring", months, max_age)
        if not threads:
            log.warning("HN:%s 天內沒有符合的串(每月外包串可能已停更)", max_age)

        for story_id, title in threads:
            try:
                item = fetcher.get_json(f"{ALGOLIA}/items/{story_id}")
            except Exception as e:
                log.warning("HN 串讀取失敗 %s: %s", story_id, e)
                continue

            is_freelance_thread = "freelancer" in title.lower()
            for child in item.get("children") or []:
                body = _text(child.get("text") or "")
                if not body:
                    continue

                if is_freelance_thread:
                    if not SEEKING_FREELANCER.search(body):
                        continue  # SEEKING WORK 是同行,不是客戶
                    signal_type = "freelance_request"
                else:
                    if SEEKING_WORK.search(body):
                        continue
                    signal_type = "job"

                low = body.lower()
                matched = [s for s in skills if s in low]
                if skills and not matched:
                    continue

                emails = extract_emails(body)
                # 徵求外包的人通常會貼自家網址。沒留信箱的那些,就靠這個網址讓
                # enricher 去官網的公開聯絡頁找 info@ —— 這是 enrich 最主要的輸入。
                urls = extract_urls(body)
                author = child.get("author") or "unknown"
                # 外包串的留言常常是個人發的,格式是「SEEKING FREELANCER | 地點 |
                # 說明」,沒有公司名可抽 —— 硬抽會把地點當成公司。用作者帳號當
                # 組織識別才準,而且同一個人跨月重複發文也能正確收斂。
                org = f"HN:{author}" if is_freelance_thread else _org_name(body)
                yield Lead(
                    org=org or f"HN:{author}",
                    source=self.name,
                    source_url=f"{HN_ITEM}{child.get('id')}",
                    signal_title=_first_line(body),
                    signal_type=signal_type,
                    signal_date=(child.get("created_at") or "")[:10],
                    website=urls[0] if urls else "",
                    emails=emails,
                    tags=matched + [f"hn:{author}"],
                    evidence=body[:800],
                    notes=f"來自「{title}」",
                )

    def _find_threads(self, fetcher: Fetcher, title_kw: str, n: int,
                      max_age_days: int = 0) -> list[tuple[int, str]]:
        """取最近的幾串。

        這裡原本用 /search(按相關度排序)再把回傳的 30 筆按 story id 排序,
        以為那樣就能拿到最新的 —— 其實只是「相關度前 30 名裡最新的」,
        實測抓回來的是 2019~2024 年的串,聯絡人早就不在了。
        要按時間排就得用 /search_by_date。
        """
        try:
            d = fetcher.get_json(
                f"{ALGOLIA}/search_by_date",
                params={
                    "tags": "story,author_whoishiring",
                    "query": title_kw,
                    "hitsPerPage": 50,
                },
                use_cache=False,
            )
        except Exception as e:
            log.warning("HN 串搜尋失敗 (%s): %s", title_kw, e)
            return []
        cutoff = 0
        if max_age_days:
            cutoff = int(time.time()) - max_age_days * 86400

        out = []
        for h in d.get("hits", []):
            t = h.get("title") or ""
            if title_kw.lower() not in t.lower():
                continue
            created = h.get("created_at_i") or 0
            if cutoff and created and created < cutoff:
                continue      # 一年前的串裡的聯絡人早就不在了
            out.append((int(h["objectID"]), t))
        # search_by_date 已經是新到舊,直接取前 n 串
        return out[:n]
