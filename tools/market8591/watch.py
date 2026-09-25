"""每日市場監控:把今天的掃描結果和上一份快照比對,找出值得進場的遊戲。

流程(每天一次):
  python tools/market8591/scan.py      # 產生 out/probe.json、out/deep.json
  python tools/market8591/watch.py     # 比對 history/ 裡上一份快照,輸出警示

輸出:
  history/YYYY-MM-DD.json      今天的精簡快照(要 commit,下次比對用)
  history/alerts-YYYY-MM-DD.md 今天的警示;沒有警示時只寫一行「無」

警示規則:
  新遊戲    上次快照沒有、或帳號成交 < 0.3 筆/天,今天 ≥ 1 筆/天
  成交暴增  初始號成交/天 ≥ 3,且是上次的 2 倍以上
  價格上漲  初始號成交/天 ≥ 3,中位價比上次高 30% 以上
  (初始號成交全來自同一個賣家的遊戲會略過)
"""

from __future__ import annotations

import json
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

HERE = Path(__file__).parent
OUT = HERE / "out"
HIST = HERE / "history"
TW = timezone(timedelta(hours=8))

NEW_MIN = 1.0
NEW_PREV_MAX = 0.3
SURGE_MIN = 3.0
SURGE_RATIO = 2.0
PRICE_RATIO = 1.3


def snapshot() -> dict:
    probes = json.loads((OUT / "probe.json").read_text())
    deep = json.loads((OUT / "deep.json").read_text())
    games = {p["id"]: {"name": p["name"], "per_day": p["per_day"], "median": p["median"]}
             for p in probes}
    for d in deep:
        g = games.setdefault(d["id"], {"name": d["name"]})
        i = d["init"]
        g["init"] = {k: i.get(k) for k in
                     ("per_day", "median", "p75", "gmv_per_day", "active", "sellers", "top3_share")}
    return games


def previous(today: str) -> tuple[str, dict] | None:
    files = sorted(f for f in HIST.glob("20*.json") if f.stem < today)
    if not files:
        return None
    return files[-1].stem, json.loads(files[-1].read_text())


def init_line(g: dict) -> str:
    i = g.get("init") or {}
    inv = round(i["active"] / i["per_day"], 1) if i.get("per_day") else "-"
    return (f"初始號 {i.get('per_day', 0)} 筆/天、中位價 {i.get('median')}、在架 {i.get('active')}"
            f"(庫存 {inv} 天)、前三大賣家佔 {i.get('top3_share')}")


def compare(prev: dict, cur: dict) -> list[str]:
    out = []
    for gid, g in cur.items():
        url = f"https://www.8591.com.tw/v3/mall/list/{gid}?searchType=2"
        p = prev.get(gid)
        if (g.get("init") or {}).get("sellers") == 1:
            continue  # 單一賣家一次大量出貨,不是真需求
        if g.get("per_day", 0) >= NEW_MIN and (not p or p.get("per_day", 0) < NEW_PREV_MAX):
            out.append(f"- 🆕 **{g['name']}**:帳號 {g['per_day']} 筆/天(上次 "
                       f"{p.get('per_day') if p else '無'}),{init_line(g)} [8591]({url})")
            continue
        ci, pi = g.get("init") or {}, (p or {}).get("init") or {}
        if not ci or ci.get("per_day", 0) < SURGE_MIN:
            continue
        if ci["per_day"] >= SURGE_RATIO * max(pi.get("per_day") or 0, 0.5):
            out.append(f"- 📈 **{g['name']}** 成交暴增:{pi.get('per_day')} → {ci['per_day']} 筆/天,"
                       f"{init_line(g)} [8591]({url})")
        elif pi.get("median") and ci.get("median") and ci["median"] >= PRICE_RATIO * pi["median"]:
            out.append(f"- 💰 **{g['name']}** 價格上漲:中位價 {pi['median']} → {ci['median']},"
                       f"{init_line(g)} [8591]({url})")
    return out


def main():
    HIST.mkdir(exist_ok=True)
    today = datetime.now(TW).strftime("%Y-%m-%d")
    cur = snapshot()
    (HIST / f"{today}.json").write_text(json.dumps(cur, ensure_ascii=False, indent=0))

    prev = previous(today)
    if prev is None:
        lines = ["第一份快照,下次開始比對。"]
    else:
        lines = compare(prev[1], cur) or ["無"]
        lines.insert(0, f"比對基準:{prev[0]}")
    text = f"# 8591 市場警示 {today}\n\n" + "\n".join(lines) + "\n"
    (HIST / f"alerts-{today}.md").write_text(text)
    print(text)


if __name__ == "__main__":
    sys.exit(main())
