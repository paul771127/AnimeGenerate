"""政府電子採購網(標案)。

資料來自 g0v / openfun 的公開採購網 API,原始資料是依政府採購法公告的公開資訊,
機關聯絡人與電話本來就是「公告給廠商聯絡用」的,所以這是整套系統裡法律風險最低、
意圖訊號最強的來源。

對軟體 / 韌體 / 機構接案者的用法:
  * 招標中的案子 -> 直接投標或找得標廠商當二手承包
  * 已決標的案子 -> 看哪些機關固定在買這類服務,以及誰在搶(競爭者名單)
"""
from __future__ import annotations

import logging
import re
from datetime import date, datetime, timedelta
from typing import Iterable

from ..http import Fetcher, RobotsDenied
from ..models import Lead, extract_emails, extract_phones
from .base import register

log = logging.getLogger("leadgen.gov_tender")

API = "https://pcc-api.openfun.app/api"

# 還可以投標的公告類型 = 高意圖
OPEN_TYPES = (
    "公開招標公告",
    "公開取得報價單或企劃書公告",
    "經公開評選或公開徵求之限制性招標公告",
    "公開徵求廠商提供參考資料公告",
    "限制性招標公告",
)
# 已決標 = 情報價值(誰在買、誰在搶),意圖分數較低
AWARD_TYPES = ("決標公告", "無法決標公告")

_MONEY = re.compile(r"([\d,]+)\s*元")


def _parse_money(s: str) -> int | None:
    if not s:
        return None
    m = _MONEY.search(s)
    if not m:
        return None
    try:
        return int(m.group(1).replace(",", ""))
    except ValueError:
        return None


_ROC = re.compile(r"(\d{2,3})/(\d{1,2})/(\d{1,2})")


def _parse_roc(s: str) -> str:
    """民國日期 "115/08/21 17:00" -> "2026-08-21"。"""
    m = _ROC.search(s or "")
    if not m:
        return ""
    y, mo, d = (int(x) for x in m.groups())
    try:
        return date(y + 1911, mo, d).isoformat()
    except ValueError:
        return ""


def _parse_date(n: int) -> str:
    try:
        return datetime.strptime(str(n), "%Y%m%d").date().isoformat()
    except ValueError:
        return ""


@register
class GovTender:
    name = "gov_tender"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        keywords: list[str] = cfg.get("keywords", [])
        max_pages = int(cfg.get("max_pages", 2))
        within_days = int(cfg.get("within_days", 120))
        detail_budget = int(cfg.get("detail_fetches", 40))  # 細節頁的抓取上限
        include_awards = bool(cfg.get("include_awards", True))
        drop_expired = bool(cfg.get("drop_expired", True))
        cutoff = date.today() - timedelta(days=within_days)

        seen: set[str] = set()
        details_used = 0

        for kw in keywords:
            for page in range(1, max_pages + 1):
                try:
                    data = fetcher.get_json(
                        f"{API}/searchbytitle", params={"query": kw, "page": page}
                    )
                except (RobotsDenied, RuntimeError, ValueError) as e:
                    log.warning("標案查詢失敗 kw=%s page=%s: %s", kw, page, e)
                    break

                records = data.get("records") or []
                if not records:
                    break

                for rec in records:
                    brief = rec.get("brief") or {}
                    btype = brief.get("type", "")
                    is_open = any(t in btype for t in OPEN_TYPES)
                    is_award = any(t in btype for t in AWARD_TYPES)
                    if not (is_open or (include_awards and is_award)):
                        continue

                    sig_date = _parse_date(rec.get("date", 0))
                    if sig_date and date.fromisoformat(sig_date) < cutoff:
                        continue

                    key = f"{rec.get('unit_id')}|{rec.get('job_number')}|{btype}"
                    if key in seen:
                        continue
                    seen.add(key)

                    unit = rec.get("unit_name", "")
                    page_url = "https://web.pcc.gov.tw/prkms/tender/common/basic/readTenderBasic"
                    public_url = rec.get("url", "")
                    source_url = (
                        f"https://pcc.g0v.ronny.tw{public_url}" if public_url else page_url
                    )

                    lead = Lead(
                        org=unit,
                        source=self.name,
                        source_url=source_url,
                        signal_title=brief.get("title", ""),
                        signal_type="tender_open" if is_open else "tender_award",
                        signal_date=sig_date,
                        tags=[t for t in [kw, brief.get("category", "")] if t],
                        evidence=f"{btype}:{brief.get('title','')}",
                        notes=f"案號 {rec.get('job_number','')} / {btype}",
                    )

                    # 只對高價值的公告抓細節頁(聯絡人、預算金額)
                    if is_open and details_used < detail_budget:
                        details_used += 1
                        self._attach_detail(fetcher, rec, lead)

                    # 投標期限已過的案子不是機會,預設直接丟掉
                    if deadline := next(
                        (t[9:] for t in lead.tags if t.startswith("deadline:")), ""
                    ):
                        if date.fromisoformat(deadline) < date.today():
                            if drop_expired:
                                continue
                            lead.signal_type = "tender_expired"

                    winners = (brief.get("companies") or {}).get("names") or []
                    if is_award and winners:
                        lead.notes += f" / 得標:{'、'.join(winners[:3])}"

                    yield lead

    def _attach_detail(self, fetcher: Fetcher, rec: dict, lead: Lead) -> None:
        url = f"{API}/tender"
        params = {"unit_id": rec.get("unit_id"), "job_number": rec.get("job_number")}
        try:
            d = fetcher.get_json(url, params=params)
        except Exception as e:  # 細節頁失敗不該讓整批掛掉
            log.debug("標案細節失敗 %s: %s", params, e)
            return
        recs = d.get("records") or []
        if not recs:
            return
        det = recs[0].get("detail") or {}

        for k, v in det.items():
            if "聯絡電話" in k or "傳真" in k:
                lead.phones.extend(extract_phones(str(v)))
            elif "機關地址" in k:
                lead.location = str(v)
            elif "電子郵件信箱" in k:
                # 這是公告裡「請廠商聯絡此窗口」的指定信箱,蒐集目的與公告目的一致
                lead.emails.extend(extract_emails(str(v)))
            elif "聯絡人" in k:
                # 只留稱謂(「雷先生」),不展開成可識別的個人資料
                lead.notes += f" / 聯絡窗口:{v}"
            elif "預算金額" in k and "是否" not in k:
                lead.budget_twd = _parse_money(str(v))
            elif k.endswith("截止投標"):
                if (dl := _parse_roc(str(v))):
                    lead.tags.append(f"deadline:{dl}")
                    lead.notes += f" / 截止投標 {dl}"
            elif k.endswith("履約期限"):
                lead.notes += f" / 履約 {v}"
        lead.phones = list(dict.fromkeys(lead.phones))
        lead.emails = list(dict.fromkeys(lead.emails))
