"""PTT 上的發案貼文(多個板)。

實測各板第一頁的需求貼文密度,結論跟直覺不一樣:

    CodeJob      21 篇 / 需求 3 篇   [發案] 標籤,例:Power BI外包案
    Mechanical   21 篇 / 需求 5 篇   [徵求],例:新竹 電木NC加工廠、
                                     NACHI MZ25 機械手臂教學指導
    Soho         14 篇 / 需求 4 篇   但全是行銷/攝影/廣告投手
    Electronics  16 篇 / 需求 1 篇
    Programming  21 篇 / 需求 0 篇

所以主力是 CodeJob(軟體外包)與 Mechanical(加工、機構、設備),
Soho 預設不開 —— 實測 5 頁 45 篇需求貼文只濾出 3 筆,而且是 1000~1500 元
的行銷微案,跟軟體/韌體/機構沒有交集。

每個板的需求標籤不一樣,所以 config 裡逐板指定。Soho 板的貼文有制式表格:

    ◎個人徵才:
         個人名稱:莊小姐
         聯絡方式:line ID:katzeodin
    ◎徵求期限:10/21
    ◎工作內容描述:...
    ◎案件預算:約5000～7000元之間
    ⊙理想接案對象:個人接案

有表格就照欄位解析,沒有的板(CodeJob、Mechanical 是自由格式)就退回全文
掃描聯絡方式。預算級距小(幾千到幾萬),跟標案不是同一個量級 —— 它的價值
在零資格門檻、決策快、可以直接聊,不在金額。

也就是說:明確的需求、明確的預算、發案者自己張貼的聯絡方式,而且板規要求
標明「理想接案對象」—— 寫「個人接案」的那些完全沒有公司資格門檻。
對個人接案者來說,這是零門檻而且在台灣的案源。

要先說清楚兩件事:
  * 這個板的案子以行銷、設計、文案為多,軟體/韌體/機構的密度低,
    一頁十幾篇裡可能只有一兩篇對得上。要靠多翻頁累積。
  * 預算級距小(幾千到幾萬),跟標案不是同一個量級。它的價值在門檻低、
    決策快、可以直接聊,不在金額。

PTT 沒有 robots.txt(回 404),我們仍照一般規矩限速,並且只讀公開的網頁版。
聯絡方式多半是 LINE ID,所以放進 contacts_other 而不是 emails。
"""
from __future__ import annotations

import logging
import re
from datetime import date, datetime
from typing import Iterable
from urllib.parse import urljoin

from bs4 import BeautifulSoup

from ..http import Fetcher, RobotsDenied
from ..models import Lead, extract_emails, extract_phones
from .base import register

log = logging.getLogger("leadgen.ptt")

BASE = "https://www.ptt.cc"

# 各板的需求標籤不同:Soho 用 [徵才],CodeJob 用 [發案],Mechanical 用 [徵求]。
# [自介]/[個人] 是同行在推銷自己,[心得]/[公告]/[請益] 不是案源。
DEFAULT_BOARDS = [
    {"name": "CodeJob", "tags": ["[發案]", "[徵才]", "[外包]"]},
    {"name": "Mechanical", "tags": ["[徵求]", "[徵才]"]},
]

# 制式表格有兩層:外層用 ◎/⊙ 開頭,但「個人名稱」「聯絡方式」是縮排在
# 「◎個人徵才:」底下的。只抓外層會完全拿不到聯絡方式。
_FIELD = re.compile(r"^[ \t]*[◎⊙●∎]?[ \t]*([^:：\n（(]{2,16})[:：][ \t]*(.*)$",
                    re.MULTILINE)
# 標題寫已徵到的就不是機會了
_FILLED = re.compile(r"(已徵到|已找到|已結案|已刪除|徵滿|結束|取消)")
_MONEY = re.compile(r"([\d,]+)\s*(?:萬)?\s*元?")
_DEADLINE = re.compile(r"(\d{1,2})\s*[/月]\s*(\d{1,2})")
# \w 在 Python 包含中文,用它會抓到「LINE: 所在地區」這種整段中文當 ID;
# 而且 line 要有詞界,否則「StreamlineBio」裡的 line 也會被當成 LINE 帳號。
_LINE_ID = re.compile(
    r"(?<![A-Za-z])(?:line|賴)\s*(?:id|帳號)?\s*[:：]\s*([A-Za-z0-9._-]{3,30})",
    re.IGNORECASE,
)
_NOT_AN_ID = frozenset({"id", "line", "none", "n/a", "na", "tba", "same"})

# [徵求] 板上也有賣東西、轉讓課程的貼文,那些不是案源
# 多字詞不需要詞界(「課程轉讓」中間沒有空白);單字的售/賣要有詞界,
# 否則會誤殺「銷售系統開發」這類正常案子。
_NOT_A_JOB = re.compile(r"(轉讓|二手|面交|開團|代購)|(?:^|\s|\])\s*[售賣]")


def _looks_like_line_id(v: str) -> bool:
    return (v.lower() not in _NOT_AN_ID
            and any(c.isalpha() for c in v)
            and not v.isdigit())


def _parse_budget(raw: str) -> int | None:
    """「約5000～7000元之間」-> 5000;「1.5萬」-> 15000。"""
    if not raw:
        return None
    if m := re.search(r"([\d.]+)\s*萬", raw):
        try:
            return int(float(m.group(1)) * 10_000)
        except ValueError:
            pass
    nums = [int(n.replace(",", "")) for n in _MONEY.findall(raw) if n.strip(",")]
    nums = [n for n in nums if n >= 500]      # 濾掉「一週內」這種誤判
    return min(nums) if nums else None


def _parse_deadline(raw: str, posted: date | None) -> str:
    """徵求期限只寫「10/21」,年份要從發文日期推。"""
    if not raw or not posted:
        return ""
    m = _DEADLINE.search(raw)
    if not m:
        return ""
    mo, day = int(m.group(1)), int(m.group(2))
    year = posted.year
    # 12 月發文、期限寫 1 月,表示跨年
    if mo < posted.month - 6:
        year += 1
    try:
        return date(year, mo, day).isoformat()
    except ValueError:
        return ""


@register
class Ptt:
    name = "ptt"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        boards = cfg.get("boards") or DEFAULT_BOARDS
        pages = int(cfg.get("pages", 3))
        keywords: list[str] = [k.lower() for k in cfg.get("keywords", [])]
        individual_only = bool(cfg.get("individual_only", False))
        excludes: list[str] = [k.lower() for k in cfg.get("exclude_keywords", [])]
        drop_expired = bool(cfg.get("drop_expired", True))

        for board in boards:
            name = board["name"] if isinstance(board, dict) else str(board)
            tags = tuple(board.get("tags") or ["[徵才]"]) if isinstance(board, dict) \
                else ("[徵才]",)
            for url in self._post_urls(fetcher, name, tags, pages):
                try:
                    html = fetcher.get(url)
                except (RobotsDenied, RuntimeError):
                    continue
                lead = self._parse_post(url, html, keywords, excludes,
                                        individual_only, drop_expired)
                if lead:
                    yield lead

    # ---------- 列表頁 ----------
    def _post_urls(self, fetcher: Fetcher, board: str, wanted: tuple[str, ...],
                   pages: int) -> list[str]:
        urls: list[str] = []
        index = f"{BASE}/bbs/{board}/index.html"
        for _ in range(pages):
            try:
                html = fetcher.get(index, use_cache=False)
            except Exception as e:
                log.warning("PTT 列表頁失敗 %s: %s", index, e)
                break
            soup = BeautifulSoup(html, "lxml")
            for div in soup.select("div.r-ent div.title"):
                a = div.find("a", href=True)
                if not a:
                    continue  # 已刪除的文章沒有連結
                title = a.get_text(strip=True)
                # Fw: 轉錄的徵才也算,所以用 in 而不是 startswith
                if not any(t in title for t in wanted):
                    continue
                urls.append(urljoin(BASE, a["href"]))
            prev = soup.select_one("a.btn.wide:-soup-contains('‹ 上頁')")
            if not prev or not prev.get("href"):
                break
            index = urljoin(BASE, prev["href"])
        log.info("PTT %s 板:找到 %s 篇需求貼文", board, len(urls))
        return urls

    # ---------- 內容頁 ----------
    def _parse_post(self, url: str, html: str, keywords: list[str],
                    excludes: list[str], individual_only: bool,
                    drop_expired: bool) -> Lead | None:
        soup = BeautifulSoup(html, "lxml")
        main = soup.select_one("#main-content")
        if not main:
            return None

        meta = {}
        for line in soup.select(".article-metaline"):
            tag = line.select_one(".article-meta-tag")
            val = line.select_one(".article-meta-value")
            if tag and val:
                meta[tag.get_text(strip=True)] = val.get_text(strip=True)

        title = meta.get("標題", "")
        if _FILLED.search(title):
            return None      # 「(已徵到)」的貼文不是機會
        if _NOT_A_JOB.search(title):
            return None      # 「售 3D列印機」「PTC Creo 課程轉讓」不是發案

        body = re.sub(r"\n{2,}", "\n", main.get_text("\n"))
        fields = {k.strip(): v.strip() for k, v in _FIELD.findall(body)}

        desc = fields.get("工作內容描述", "") or fields.get("工作內容", "")
        # 關鍵字只比對標題與工作內容。比對整篇會命中板規與範本說明裡的字,
        # 結果「名片設計」「網站SEO」這種案子也被撈進來。
        # 有制式表格就只比對標題與工作內容(比對整篇會命中板規範本的字);
        # 自由格式的板沒有表格,就比對全文。
        haystack = (f"{title}\n{desc}\n{fields.get('徵求條件','')}" if desc
                    else f"{title}\n{body.split('※ 發信站')[0]}").lower()
        if keywords and not any(k in haystack for k in keywords):
            return None
        # 這個板以行銷與平面設計為主,光靠正面關鍵字會把「網站SEO」「名片設計」
        # 這類案子一起撈進來(它們的內容描述也會提到網站、系統)。
        if excludes and any(k in haystack for k in excludes):
            return None

        ideal = fields.get("理想接案對象", "")
        if individual_only and "個人" not in ideal:
            return None

        posted = None
        if raw_time := meta.get("時間"):
            try:
                posted = datetime.strptime(raw_time, "%a %b %d %H:%M:%S %Y").date()
            except ValueError:
                posted = None

        deadline = _parse_deadline(fields.get("徵求期限", ""), posted)
        if drop_expired and deadline and date.fromisoformat(deadline) < date.today():
            return None

        # CodeJob / Mechanical 是自由格式,沒有「◎聯絡方式:」欄位。
        # 全文只能用來「抽取」email/電話/LINE —— 原本把整篇存進
        # contacts_other,結果欄位裡塞的是文章標頭。
        contact_field = fields.get("聯絡方式", "")
        scan = contact_field or body.split("※ 發信站")[0]

        other: list[str] = []
        for m in _LINE_ID.finditer(scan):
            if _looks_like_line_id(m.group(1)):
                other.append(f"LINE: {m.group(1)}")
                break
        if not other and contact_field and not extract_emails(contact_field):
            # 制式欄位的值很短,可以原樣留(站內信、IG、Discord 之類)
            short = " ".join(contact_field.split())[:60]
            if 2 < len(short) <= 60:
                other.append(short)
        if not other and "站內信" in scan:
            other.append("PTT 站內信")
        contact_raw = scan

        org = (fields.get("公司名稱") or fields.get("個人名稱")
               or meta.get("作者", "").split(" ")[0] or "PTT 發案者")

        return Lead(
            org=org,
            source=self.name,
            source_url=url,
            signal_title=re.sub(r"^(Fw:\s*)?\[[^\]]+\]\s*", "", title).strip()
            or desc[:60],
            signal_type="freelance_request",
            signal_date=posted.isoformat() if posted else "",
            emails=extract_emails(contact_raw),
            phones=extract_phones(contact_raw),
            contacts_other=other,
            budget_twd=_parse_budget(
                fields.get("案件預算") or fields.get("預算")
                or fields.get("報價") or fields.get("費用") or ""
            ),
            tags=[t for t in [f"ptt:{board_of(url)}",
                              f"對象:{ideal}" if ideal else ""] if t],
            evidence=(desc or body)[:800],
            notes=" / ".join(filter(None, [
                f"徵求期限 {deadline}" if deadline else "",
                f"交件 {fields.get('交件時間','')}" if fields.get("交件時間") else "",
                f"發票 {fields.get('是否需要開立發票','')}"
                if fields.get("是否需要開立發票") else "",
            ])),
        )


def board_of(url: str) -> str:
    m = re.search(r"/bbs/([^/]+)/", url)
    return m.group(1) if m else "?"
