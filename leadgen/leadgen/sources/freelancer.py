"""Freelancer.com 官方 API 上的案子。

為什麼加這個來源:政府標案的廠商資格要營業登記、納稅證明與押標金,個人接案者
大部分投不了。外包平台剛好相反 —— 個人投標是常態,不需要任何公司資格,
而且客戶是中小企業與個人,案子規模小得多。

這個平台有三個陷阱必須處理:

1. **殺價市場。** 大量案子是 $30–250 美金搭配 90 幾個人投標 —— 實測
   「Vehicle LED VMS Display Design」預算 $30–250、98 人投標。所以一定要套
   `min_budget_twd`(預算下限)與 `max_bids`(投標人數上限)。

2. **query 是對全文搜尋的。** 用 `cad`、`automation` 這類關鍵字會撈到
   「Modern Logo for Arema Nasi Goreng」「Facebook & Instagram Image Post」
   這種行銷案(它們的描述裡剛好有那些字)。所以真正的過濾靠平台自己的技能
   標籤白名單(`skills`)——「Bow Sight STL Redesign」掛的是 CAD/CAM、
   Solidworks、3D Modelling,而「Cabin Foundation Blueprint」掛的是
   Building Architecture、Civil Engineering,兩者都命中 cad 但只有前者是你的。

3. **駐點案從台灣接不了。** 平台的 `local` 旗標能擋掉一部分(「Stereo
   Receiver Repair」是 local=True),但不夠 ——「On-Site ABB VFD Analysis
   and Repair」是 local=False 卻明顯要到現場。所以再加一層標題關鍵字判斷。

4. **時薪案不能拿固定價的門檻去比。** 一個 $15/hr 的海報案換算成「預算」
   只有 480 台幣,卻會通過「預算下限 / 40 小時」這種算法。時薪案另外用
   `min_hourly_twd` 判斷,並把預算欄位換算成「時薪 × 假設工時」,
   這樣預算分數才跟固定價案可比。

平台案子沒有聯絡方式,是站內投標 —— 所以 contacts_other 記的是投標管道。
"""
from __future__ import annotations

import logging
import re
from datetime import datetime, timezone
from typing import Iterable

from ..http import Fetcher
from ..models import Lead
from .base import register

log = logging.getLogger("leadgen.freelancer")

API = "https://www.freelancer.com/api/projects/0.1/projects/active/"
PROJECT_URL = "https://www.freelancer.com/projects/"
USD_TWD = 32  # 粗估匯率,只用來做預算級距比較

# 要到現場才能做的案子。平台的 local 旗標會漏(「On-Site ABB VFD Analysis」
# 是 local=False),所以補一層標題判斷。
# 「X-based」只有在 X 是地名時才代表駐點。原本用 [a-z]+-based 這個萬用模式,
# 結果 Cloud-based、Web-based、AI-based、Rust-based、Linux-based 全被當成
# 駐點案丟掉 —— 那些正好是要接的案子。寧可漏掉幾個駐點案,
# 也不要無聲刪掉好名單,所以改成白名單。
ONSITE = re.compile(
    r"(on-?site|in-?person|local(ly)? only|"
    r"must be (based|located|living|residing) in|"
    r"\b(uk|us|usa|u\.s\.|eu|canada|india|germany|france|spain|italy|"
    r"netherlands|poland|japan|korea|china|singapore|malaysia|australia|"
    r"nz|dubai|uae|israel|brazil|mexico|london|berlin|paris|sydney|toronto)"
    r"[- ]based\b)",
    re.IGNORECASE,
)


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
        # 原本只抓 fixed,漏掉時薪案 —— 長期的時薪案對接案者常常比一次性
        # 的固定價案更值錢(不用重新找案)。
        project_types: list[str] = cfg.get("project_types") or ["fixed", "hourly"]
        # 平台自己的技能標籤白名單 —— 這是主要的過濾依據,比 query 的全文
        # 搜尋精準得多。
        skills = [k.lower() for k in cfg.get("skills", [])]
        skip_skills = [k.lower() for k in cfg.get("exclude_skills", [])]
        min_hourly = int(cfg.get("min_hourly_twd", 800))
        hourly_hours = int(cfg.get("hourly_hours_assumed", 40))
        remote_only = bool(cfg.get("remote_only", True))

        seen: set[int] = set()
        for q in queries:
            try:
                d = fetcher.get_json(API, params=[
                    ("query", q),
                    ("limit", limit),
                    ("job_details", "true"),   # 要技能標籤才能精準過濾
                    *[("project_types[]", t) for t in project_types],
                ])
            except Exception as e:
                log.warning("Freelancer 查詢失敗 q=%s: %s", q, e)
                continue

            projects = ((d.get("result") or {}).get("projects")) or []
            for proj in projects:
                pid = proj.get("id")
                if pid in seen:
                    continue
                seen.add(pid)

                if remote_only:
                    if proj.get("local"):
                        continue          # 平台標為在地案
                    if ONSITE.search(proj.get("title") or ""):
                        continue          # 標題就說要到現場

                job_names = [str(j.get("name", "")) for j in (proj.get("jobs") or [])]
                low_jobs = " / ".join(job_names).lower()
                title_low = (proj.get("title") or "").lower()

                if skip_skills and any(k in low_jobs for k in skip_skills):
                    continue      # 建築、土木、平面設計這些不是你的
                if skills and not (any(k in low_jobs for k in skills)
                                   or any(k in title_low for k in skills)):
                    continue      # query 是全文搜尋,要靠技能標籤把行銷案擋掉

                is_hourly = proj.get("type") == "hourly"
                rate_or_budget = _budget_twd(proj)
                if is_hourly:
                    # budget.minimum 是每小時費率。先用時薪門檻判斷,
                    # 再換算成「時薪 x 假設工時」讓預算分數跟固定價案可比。
                    if rate_or_budget is None or rate_or_budget < min_hourly:
                        continue
                    budget = rate_or_budget * hourly_hours
                else:
                    budget = rate_or_budget
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
                    tags=[q, proj.get("type") or ""] + job_names[:6],
                    evidence=(proj.get("preview_description") or "")[:800],
                    notes=(f"{'時薪' if is_hourly else '固定價'} "
                           f"{b.get('minimum')}-{b.get('maximum')} {cur}"
                           + (f"(估 {hourly_hours} 小時)" if is_hourly else "")
                           + f" / 已有 {bids} 人投標"
                           + (f" / 技能 {'、'.join(job_names[:4])}" if job_names else "")),
                )
