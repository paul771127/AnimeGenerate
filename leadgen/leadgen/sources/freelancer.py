"""Freelancer.com 官方 API 上的案子。

為什麼加這個來源:政府標案的廠商資格要營業登記、納稅證明與押標金,個人接案者
大部分投不了。外包平台剛好相反 —— 個人投標是常態,不需要任何公司資格,
而且客戶是中小企業與個人,案子規模小得多。

但這個平台有個陷阱必須處理:大量案子是 $30–250 美金搭配 90 幾個人投標的
殺價市場。實測「Vehicle LED VMS Display Design」預算 $30–250、98 人投標 ——
投這種案子是在浪費時間。所以預設一定會套兩個門檻:
  * min_budget_twd:預算下限
  * max_bids:投標人數上限(人越多,你的勝率與單價越低)
加上門檻後剩下的才是真的:機構設計 $750–1500、STM32 BLDC 韌體 $250–750、
FPGA PCIe £250–750(只有 19 人投)。

平台案子沒有聯絡方式,是站內投標 —— 所以 contacts_other 記的是投標管道。
"""
from __future__ import annotations

import logging
from datetime import datetime, timezone
from typing import Iterable

from ..http import Fetcher
from ..models import Lead
from .base import register

log = logging.getLogger("leadgen.freelancer")

API = "https://www.freelancer.com/api/projects/0.1/projects/active/"
PROJECT_URL = "https://www.freelancer.com/projects/"
USD_TWD = 32  # 粗估匯率,只用來做預算級距比較


def _budget_twd(project: dict) -> int | None:
    """把各國幣別的預算下限換算成台幣,用於跨來源的預算比較。"""
    budget = project.get("budget") or {}
    low = budget.get("minimum")
    if not low:
        return None
    # currency.exchange_rate 是「1 單位該幣別 = 多少美金」
    rate = (project.get("currency") or {}).get("exchange_rate") or 1.0
    try:
        return int(float(low) * float(rate) * USD_TWD)
    except (TypeError, ValueError):
        return None


@register
class FreelancerCom:
    name = "freelancer"

    def fetch(self, fetcher: Fetcher, cfg: dict) -> Iterable[Lead]:
        queries: list[str] = cfg.get("queries", [])
        limit = int(cfg.get("limit_per_query", 30))
        min_budget = int(cfg.get("min_budget_twd", 15_000))
        max_bids = int(cfg.get("max_bids", 40))

        seen: set[int] = set()
        for q in queries:
            try:
                d = fetcher.get_json(API, params={
                    "query": q,
                    "limit": limit,
                    "project_types[]": "fixed",
                })
            except Exception as e:
                log.warning("Freelancer 查詢失敗 q=%s: %s", q, e)
                continue

            projects = ((d.get("result") or {}).get("projects")) or []
            for proj in projects:
                pid = proj.get("id")
                if pid in seen:
                    continue
                seen.add(pid)

                budget = _budget_twd(proj)
                if min_budget and (budget is None or budget < min_budget):
                    continue

                bids = (proj.get("bid_stats") or {}).get("bid_count") or 0
                if max_bids and bids > max_bids:
                    continue  # 投標人數太多就是殺價市場,勝率與單價都不值得

                ts = proj.get("time_submitted")
                sig_date = ""
                if ts:
                    sig_date = datetime.fromtimestamp(
                        int(ts), tz=timezone.utc).date().isoformat()

                cur = (proj.get("currency") or {}).get("code", "")
                b = proj.get("budget") or {}
                yield Lead(
                    # owner_id 常常是 null(平台不公開),改用案件 id 當識別 ——
                    # 平台上每個案子就是一個獨立客戶,不能全部收斂成同一個。
                    org=f"Freelancer 案件 #{proj.get('owner_id') or pid}",
                    source=self.name,
                    source_url=PROJECT_URL + (proj.get("seo_url") or ""),
                    signal_title=proj.get("title", ""),
                    signal_type="marketplace_project",
                    signal_date=sig_date,
                    budget_twd=budget,
                    contacts_other=["freelancer.com 站內投標"],
                    tags=[q] + [j.get("name", "") for j in (proj.get("jobs") or [])][:6],
                    evidence=(proj.get("preview_description") or "")[:800],
                    notes=(f"預算 {b.get('minimum')}-{b.get('maximum')} {cur} / "
                           f"已有 {bids} 人投標"),
                )
