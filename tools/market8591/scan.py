"""8591 手遊帳號市場掃描器。

找出「帳號」類別成交快、單價高的手機遊戲，特別是「初始號」(抽卡首抽號)市場。

資料來源(8591 前端自己在用的公開 API):
  - https://www.8591.com.tw/mobileGame.html?toJson=1   全部手遊 ID
  - https://api.8591.com.tw/v5/mall/completed-lists     最近成交紀錄(每次最多 200 筆)
  - https://api.8591.com.tw/v5/mall/lists               目前在架商品(total_rows = 在架數)

參數:ware_type=2 帳號;account_tag=2 初始號、3 進度號(成品號)。

用法:
  python tools/market8591/scan.py            # 完整掃描,輸出 out/ 下的 json/csv
  python tools/market8591/scan.py --top 50   # 只掃熱門 S 群組前 50 款(快速測試)
  python tools/market8591/scan.py --platform pc   # 線上遊戲(PC),輸出到 out/pc/
"""

from __future__ import annotations

import argparse
import csv
import json
import statistics
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
from pathlib import Path

import requests

API = "https://api.8591.com.tw"
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/128 Safari/537.36"
OUT = Path(__file__).parent / "out"
TW = timezone(timedelta(hours=8))  # 8591 的 action_time 是台灣時間

session = requests.Session()
session.headers.update({"User-Agent": UA, "Referer": "https://www.8591.com.tw/"})


def get(url: str, params: dict | None = None, retries: int = 4):
    for i in range(retries):
        try:
            r = session.get(url, params=params, timeout=20)
            if r.status_code == 200:
                return r.json()
        except (requests.RequestException, ValueError):
            pass
        time.sleep(2 ** i)
    return None


PLATFORM_PAGES = {"mobile": "mobileGame", "pc": "pcGame", "steam": "steam", "web": "webGame"}


def all_game_ids(platform: str = "mobile") -> list[str]:
    j = get(f"https://www.8591.com.tw/{PLATFORM_PAGES[platform]}.html", {"toJson": 1, "computer": 1})
    ids: list[str] = []
    for group in j.values():
        if not isinstance(group, list):
            continue
        for g in group:
            if not isinstance(g, dict):
                continue
            gid = g.get("id") or g.get("gameId")
            if gid and 2 in (g.get("wareType") or []) and str(gid) not in ids:
                ids.append(str(gid))
    return ids


def parse_time(s: str, now: datetime) -> datetime:
    # action_time 形如 "09-24 19:12"(台灣時間),沒有年份;比現在晚一天以上就視為去年
    t = datetime.strptime(f"{now.year}-{s}", "%Y-%m-%d %H:%M")
    if t > now + timedelta(days=1):
        t = t.replace(year=now.year - 1)
    return t


def completed(gid: str, limit: int, account_tag: int | None = None) -> list[dict]:
    p = {"game_id": gid, "ware_type": 2, "limit": limit}
    if account_tag:
        p["account_tag"] = account_tag
    j = get(f"{API}/v5/mall/completed-lists", p)
    if not j or not j.get("status"):
        return []
    return j["data"].get("list") or []


def active_count(gid: str, account_tag: int | None = None) -> int:
    p = {"game_id": gid, "ware_type": 2, "limit": 1}
    if account_tag:
        p["account_tag"] = account_tag
    j = get(f"{API}/v5/mall/lists", p)
    try:
        return int(j["data"]["total_rows"])
    except (TypeError, KeyError, ValueError):
        return 0


def stats(rows: list[dict], now: datetime) -> dict:
    """成交速度(筆/天)與價格分佈。"""
    if not rows:
        return {"deals": 0, "per_day": 0.0, "span_days": None,
                "p25": None, "median": None, "p75": None, "mean": None, "gmv_per_day": 0.0}
    times = [parse_time(r["action_time"], now) for r in rows]
    prices = sorted(int(str(r["ware_price"]).replace(",", "")) for r in rows)
    span = max((now - min(times)).total_seconds() / 86400, 1 / 24)
    per_day = len(rows) / span
    q = statistics.quantiles(prices, n=4) if len(prices) >= 2 else [prices[0]] * 3
    mean = statistics.mean(prices)
    return {
        "deals": len(rows),
        "per_day": round(per_day, 2),
        "span_days": round(span, 2),
        "p25": round(q[0]), "median": round(statistics.median(prices)), "p75": round(q[2]),
        "mean": round(mean),
        "gmv_per_day": round(per_day * mean),
    }


def probe(gid: str, now: datetime) -> dict | None:
    rows = completed(gid, 30)
    if not rows:
        return None
    return {"id": gid, "name": rows[0]["game_info"]["game"]["name"], **stats(rows, now)}


def deep(g: dict, now: datetime) -> dict:
    gid = g["id"]
    allrows = completed(gid, 200)
    init = completed(gid, 200, account_tag=2)
    prog = completed(gid, 200, account_tag=3)
    out = {"id": gid, "name": g["name"],
           "url": f"https://www.8591.com.tw/v3/mall/list/{gid}?searchType=2"}
    for key, rows, tag in (("all", allrows, None), ("init", init, 2), ("prog", prog, 3)):
        s = stats(rows, now)
        s["active"] = active_count(gid, tag)
        out[key] = s
    out["init_samples"] = [(r["ware_price"], r["ware_title"][:60]) for r in init[:8]]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--platform", choices=list(PLATFORM_PAGES), default="mobile")
    ap.add_argument("--top", type=int, default=0, help="只掃前 N 款(測試用)")
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--min-per-day", type=float, default=1.0, help="進入深度掃描的帳號成交門檻(筆/天)")
    a = ap.parse_args()
    out = OUT if a.platform == "mobile" else OUT / a.platform
    out.mkdir(parents=True, exist_ok=True)
    now = datetime.now(TW).replace(tzinfo=None)

    ids = all_game_ids(a.platform)
    if a.top:
        ids = ids[: a.top]
    print(f"遊戲數:{len(ids)}", file=sys.stderr)

    probes: list[dict] = []
    with ThreadPoolExecutor(a.workers) as ex:
        for i, r in enumerate(ex.map(lambda g: probe(g, now), ids), 1):
            if r:
                probes.append(r)
            if i % 200 == 0:
                print(f"  probe {i}/{len(ids)}  有成交 {len(probes)}", file=sys.stderr)
    probes.sort(key=lambda r: -r["per_day"])
    (out / "probe.json").write_text(json.dumps(probes, ensure_ascii=False, indent=1))

    cands = [p for p in probes if p["per_day"] >= a.min_per_day]
    print(f"深度掃描:{len(cands)} 款", file=sys.stderr)
    with ThreadPoolExecutor(a.workers) as ex:
        results = list(ex.map(lambda g: deep(g, now), cands))
    results.sort(key=lambda r: -r["all"]["gmv_per_day"])
    (out / "deep.json").write_text(json.dumps(results, ensure_ascii=False, indent=1))

    with open(out / "deep.csv", "w", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        w.writerow(["id", "遊戲",
                    "帳號 成交/天", "帳號 中位價", "帳號 日成交額", "帳號 在架",
                    "初始號 成交/天", "初始號 P25", "初始號 中位價", "初始號 P75", "初始號 日成交額", "初始號 在架",
                    "進度號 成交/天", "進度號 中位價", "進度號 在架", "連結"])
        for r in results:
            A, I, P = r["all"], r["init"], r["prog"]
            w.writerow([r["id"], r["name"],
                        A["per_day"], A["median"], A["gmv_per_day"], A["active"],
                        I["per_day"], I["p25"], I["median"], I["p75"], I["gmv_per_day"], I["active"],
                        P["per_day"], P["median"], P["active"], r["url"]])
    print(f"完成 → {out}", file=sys.stderr)


if __name__ == "__main__":
    main()
