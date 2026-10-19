"""台灣公司登記公開資料(g0v 台灣公司資料 API)。

這個來源「不產生聯絡方式」,它產生的是目標名單(target account list):
依營業項目、所在地、資本額篩出一批「可能需要韌體/機構/軟體外包」的公司,
再由 site_contact enricher 去它們官網的公開聯絡頁取得 info@ 這類角色信箱。

刻意不保留的欄位:負責人姓名。那是可識別特定自然人的個資,拿它做行銷
聯絡屬於目的外利用,風險遠高於它帶來的價值 —— 所以直接丟掉,不入庫。
"""
from __future__ import annotations

import logging
import re
from typing import Iterable

from ..http import Fetcher
from ..models import Lead
from .base import register

log = logging.getLogger("leadgen.tw_company")

API = "https://company.g0v.ronny.tw/api/search"

DEAD_STATES = ("歇業", "撤銷", "廢止", "解散", "停業")
_NAME_KEYS = ("公司名稱", "商業名稱")
_CAPITAL_KEYS = ("資本額(元)", "資本總額(元)", "資本額")


def _pick(rec: dict, keys: tuple[str, ...]) -> str:
    for k in keys:
        if rec.get(k):
            return str(rec[k])
    return ""


def _capital(rec: dict) -> int | None:
    raw = _pick(rec, _CAPITAL_KEYS)
    if not raw:
        return None
    digits = re.sub(r"[^\d]", "", raw)
    return int(digits) if digits else None


@register
class TWCompany:
    name = "tw_company"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        name_keywords: list[str] = cfg.get("name_keywords", [])
        industry_terms: list[str] = cfg.get("industry_terms", [])
        regions: list[str] = cfg.get("regions", [])
        min_capital = int(cfg.get("min_capital", 5_000_000))
        max_pages = int(cfg.get("max_pages", 2))

        seen: set[str] = set()
        for kw in name_keywords:
            for page in range(1, max_pages + 1):
                try:
                    d = fetcher.get_json(API, params={"q": kw, "page": page})
                except Exception as e:
                    log.warning("公司登記查詢失敗 kw=%s page=%s: %s", kw, page, e)
                    break
                rows = d.get("data") or []
                if not rows:
                    break

                for rec in rows:
                    name = _pick(rec, _NAME_KEYS)
                    if not name or name in seen:
                        continue

                    state = str(rec.get("現況", ""))
                    if any(s in state for s in DEAD_STATES):
                        continue

                    items = str(rec.get("營業項目", ""))
                    if industry_terms and not any(t in items for t in industry_terms):
                        continue

                    addr = str(rec.get("地址", ""))
                    if regions and not any(r in addr for r in regions):
                        continue

                    cap = _capital(rec)
                    if min_capital and (cap is None or cap < min_capital):
                        continue

                    seen.add(name)
                    setup = rec.get("核准設立日期") or {}
                    setup_iso = ""
                    if isinstance(setup, dict) and setup.get("year"):
                        setup_iso = (f"{setup['year']:04d}-"
                                     f"{setup.get('month',1):02d}-"
                                     f"{setup.get('day',1):02d}")

                    yield Lead(
                        org=name,
                        source=self.name,
                        source_url=f"https://company.g0v.ronny.tw/search?q={name}",
                        signal_title=f"登記營業項目含「{kw}」",
                        signal_type="registry",
                        signal_date=setup_iso,
                        location=addr,
                        budget_twd=cap,
                        tags=[kw] + [t for t in industry_terms if t in items][:5],
                        evidence=items[:500],
                        notes=f"資本額 {cap:,} 元 / 現況 {state}" if cap else f"現況 {state}",
                    )
