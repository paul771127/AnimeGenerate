"""名單評分。

這是整套系統真正的價值所在:一天寄 20 封 80 分的信,效果遠勝一天寄一萬封。
五個維度(總分 100),權重可在 config.yaml 調:

  intent  訊號強度 —— 對方是不是「現在就在找人做這件事」
  budget  付得出錢的證據 —— 標案預算、薪資級距、資本額
  fit     跟你的技能棧契合度 —— 軟體 / 韌體 / 機構的關鍵字命中
  reach   有沒有可用的公開聯絡方式
  fresh   訊號新鮮度 —— 兩個月前的需求大多已經發出去了
"""
from __future__ import annotations

from datetime import date

from .models import Lead, is_role_email

DEFAULT_WEIGHTS = {"intent": 30, "budget": 25, "fit": 25, "reach": 10, "fresh": 10}

# 訊號類型 -> 意圖強度(0..1)
INTENT_BY_TYPE = {
    "freelance_request": 1.00,  # 公開說「我要找外包」
    "tender_open": 0.95,        # 招標中,可直接投
    "tender_goods": 0.60,       # 招標中,但標的是零件/治具這類「買東西」(可代工供貨)
    "job": 0.55,                # 在徵相關職位 = 有專案缺人
    "tender_award": 0.35,       # 已決標,情報價值
    "tender_expired": 0.12,     # 投標期限已過,只剩「這個機關會買」的情報價值
    "registry": 0.08,           # 只是登記資料,沒有任何意圖訊號
}


def _budget_ratio(amount: int | None, full_at: int, unknown: float) -> float:
    """預算未公開 != 預算是 0。

    政府標案有很大一部分「預算金額是否公開 = 否」,職缺也多半不寫薪資。
    把未知當 0 分會系統性地把這些名單壓在 75 分以下,等於懲罰資料缺失,
    所以未知給一個中性分數,已知的小預算才真的扣分。
    """
    if amount is None:
        return unknown
    if amount <= 0:
        return 0.0
    return min(1.0, amount / float(full_at))


def _fit_ratio(lead: Lead, profile: dict) -> float:
    """命中你的技能關鍵字越多、越集中在核心技能,分數越高。"""
    haystack = " ".join([
        lead.signal_title, lead.evidence, lead.notes, " ".join(lead.tags)
    ]).lower()
    core = [k.lower() for k in profile.get("core", [])]
    plus = [k.lower() for k in profile.get("plus", [])]
    minus = [k.lower() for k in profile.get("exclude", [])]

    if any(m in haystack for m in minus):
        return 0.0
    core_hits = sum(1 for k in core if k in haystack)
    plus_hits = sum(1 for k in plus if k in haystack)
    if core_hits == 0 and plus_hits == 0:
        return 0.0
    # 中文標案的標題很短(「文湖線未來車車頭暨駕駛平台模型製作採購案」),
    # 命中兩個核心關鍵字已經是強契合,要求三個等於永遠拿不到滿分,
    # 會把所有中文來源系統性壓低一級。
    base = {0: 0.45, 1: 0.80, 2: 0.95}.get(core_hits, 1.0)
    return min(1.0, base + 0.05 * plus_hits)


def _reach_ratio(lead: Lead) -> float:
    if any(is_role_email(e) for e in lead.emails):
        return 1.0      # info@ / sales@ 這類角色信箱,最乾淨
    if lead.emails:
        return 0.8
    if lead.phones:
        return 0.5
    if lead.website:
        return 0.2      # 還要靠 enricher 補
    return 0.0


def _fresh_ratio(signal_date: str, half_life_days: int) -> float:
    if not signal_date:
        return 0.3
    try:
        d = date.fromisoformat(signal_date[:10])
    except ValueError:
        return 0.3
    age = (date.today() - d).days
    if age <= 7:
        return 1.0
    if age >= half_life_days * 4:
        return 0.0
    return max(0.0, 1.0 - (age - 7) / float(half_life_days * 4 - 7))


def score_lead(lead: Lead, cfg: dict) -> tuple[int, dict]:
    w = {**DEFAULT_WEIGHTS, **(cfg.get("weights") or {})}
    profile = cfg.get("profile") or {}
    budget_full_at = int(cfg.get("budget_full_at_twd", 2_000_000))
    unknown_budget = float(cfg.get("unknown_budget_ratio", 0.4))
    half_life = int(cfg.get("fresh_half_life_days", 30))

    ratios = {
        "intent": INTENT_BY_TYPE.get(lead.signal_type, 0.3),
        "budget": _budget_ratio(lead.budget_twd, budget_full_at, unknown_budget),
        "fit": _fit_ratio(lead, profile),
        "reach": _reach_ratio(lead),
        "fresh": _fresh_ratio(lead.signal_date, half_life),
    }
    detail = {k: round(ratios[k] * w[k], 1) for k in ratios}
    total = int(round(sum(detail.values())))
    return total, detail


def score_all(leads: list[Lead], cfg: dict) -> list[Lead]:
    for lead in leads:
        lead.score, lead.score_detail = score_lead(lead, cfg)
    leads.sort(key=lambda x: x.score, reverse=True)
    return leads
