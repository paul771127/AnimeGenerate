"""匯出:CSV(進 CRM / Excel)與 Markdown 戰報(每天要寄哪幾封)。

CSV 一定帶 source_url 與 collected_at —— 對方問「你從哪拿到我的聯絡方式」時,
你要能在三秒內回答。這不只是合規,也是回信率的一部分。
"""
from __future__ import annotations

import csv
import json
from pathlib import Path

from .store import Store

CSV_COLUMNS = [
    "score", "org", "signal_type", "signal_title", "signal_date",
    "emails", "phones", "website", "location", "budget_twd",
    "source", "source_url", "collected_at", "status", "notes",
]


def _loads(v):
    try:
        return json.loads(v) if isinstance(v, str) else v
    except json.JSONDecodeError:
        return v


def to_csv(store: Store, out: str | Path, min_score: int = 0,
           require_contact: bool = False, limit: int = 1000) -> int:
    rows = store.top(min_score=min_score, limit=limit, require_contact=require_contact)
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open("w", newline="", encoding="utf-8-sig") as f:
        w = csv.DictWriter(f, fieldnames=CSV_COLUMNS, extrasaction="ignore")
        w.writeheader()
        for r in rows:
            d = dict(r)
            d["emails"] = ";".join(_loads(d["emails"]) or [])
            d["phones"] = ";".join(_loads(d["phones"]) or [])
            w.writerow(d)
    return len(rows)


def to_markdown(store: Store, out: str | Path, min_score: int = 70,
                limit: int = 25) -> int:
    rows = store.top(min_score=min_score, limit=limit, require_contact=False)
    lines = [
        f"# 今日名單(score >= {min_score})",
        "",
        f"共 {len(rows)} 筆。建議一天手動個人化 20–30 封,不要群發。",
        "",
    ]
    for i, r in enumerate(rows, 1):
        emails = _loads(r["emails"]) or []
        phones = _loads(r["phones"]) or []
        detail = _loads(r["score_detail"]) or {}
        budget = f"{r['budget_twd']:,}" if r["budget_twd"] else "未公開"
        lines += [
            f"## {i}. [{r['score']}] {r['org']}",
            f"- 訊號:{r['signal_type']} — {r['signal_title']}（{r['signal_date'] or '日期未知'}）",
            f"- 聯絡:{'、'.join(emails) or '—'}｜{'、'.join(phones) or '—'}",
            f"- 預算/規模:{budget}｜地點:{r['location'] or '—'}",
            f"- 分數組成:{detail}",
            f"- 出處:{r['source_url']}",
            "",
            "**開場切入點(寫信時改成你自己的話):**",
            f"> 看到你們{r['signal_title'][:40]}…",
            "",
        ]
    out = Path(out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines), encoding="utf-8")
    return len(rows)
