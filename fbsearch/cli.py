"""命令列工具。

  fbsearch import  <匯出.zip | 資料夾 | 檔案.json> ...
  fbsearch serve   [--port 8765]
  fbsearch search  關鍵字 [--kind video] [--source viewed] [--min 100 --max 5000] [--days 7]
  fbsearch add     --url ... --title ... --text ...
  fbsearch stats
"""

from __future__ import annotations

import argparse
import os
import sys
import time
from datetime import datetime

from .db import KINDS, SOURCES, Query, Store
from .importer import iter_export
from .server import item_from_json, parse_date, serve

DEFAULT_DB = os.environ.get("FBSEARCH_DB", "fbsearch.db")


def _cmd_import(args) -> int:
    store = Store(args.db)
    total_new = total_dup = 0
    for path in args.paths:
        try:
            for fname, items in iter_export(path, all_files=args.all):
                new, dup = store.upsert_many(items)
                total_new += new
                total_dup += dup
                if items:
                    print(f"  {fname}: {len(items)} 筆(新增 {new})")
        except FileNotFoundError:
            print(f"找不到:{path}", file=sys.stderr)
            return 1
    print(f"完成:新增 {total_new} 筆,已存在 {total_dup} 筆。資料庫:{args.db}")
    return 0


def _cmd_search(args) -> int:
    store = Store(args.db)
    q = Query(q=" ".join(args.query), kind=args.kind or "", source=args.source or "",
              price_min=args.min, price_max=args.max, has_price=args.has_price,
              date_from=parse_date(args.since), date_to=parse_date(args.until, end=True),
              sort=args.sort, limit=args.limit)
    if args.days:
        q.date_from = int(time.time()) - args.days * 86400
    res = store.search(q)
    print(f"找到 {res['total']} 筆(顯示前 {len(res['items'])} 筆)\n")
    for it in res["items"]:
        when = datetime.fromtimestamp(it["ts"]).strftime("%Y-%m-%d %H:%M") if it["ts"] else "          "
        price = f"${it['price']:,.0f}" if it["price"] is not None else ""
        title = (it["title"] or it["text"] or it["url"]).replace("\n", " ")[:70]
        print(f"[{when}] {it['kind']:<11} {it['source']:<7} {price:>9}  {title}")
        if it["url"]:
            print(f"{'':>40}{it['url']}")
    return 0


def _cmd_add(args) -> int:
    store = Store(args.db)
    it = store.add(item_from_json(vars(args)))
    print(f"已加入 #{it['id']}" + (f",價格 {it['price']:,.0f}" if it["price"] is not None else ""))
    return 0


def _cmd_stats(args) -> int:
    s = Store(args.db).stats()
    print(f"總數:{s['total']}(有標價 {s['with_price']})")
    print("類型:", ", ".join(f"{k}={v}" for k, v in s["by_kind"].items()))
    print("來源:", ", ".join(f"{k}={v}" for k, v in s["by_source"].items()))
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="fbsearch", description="個人 Facebook 資料整理與搜尋")
    ap.add_argument("--db", default=DEFAULT_DB, help=f"資料庫路徑(預設 {DEFAULT_DB},或設 FBSEARCH_DB)")
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("import", help="匯入 Facebook「下載你的資訊」JSON 匯出")
    p.add_argument("paths", nargs="+")
    p.add_argument("--all", action="store_true", help="掃描所有 JSON 檔(預設只看相關檔案)")
    p.set_defaults(func=_cmd_import)

    p = sub.add_parser("serve", help="啟動本機網頁介面")
    p.add_argument("--host", default="127.0.0.1")
    p.add_argument("--port", type=int, default=8765)
    p.set_defaults(func=lambda a: serve(a.db, a.host, a.port) or 0)

    p = sub.add_parser("search", help="在命令列搜尋")
    p.add_argument("query", nargs="*")
    p.add_argument("--kind", help="、".join(KINDS) + "(逗號分隔可多選)")
    p.add_argument("--source", help="、".join(SOURCES) + "(逗號分隔可多選)")
    p.add_argument("--min", type=float, help="最低價格")
    p.add_argument("--max", type=float, help="最高價格")
    p.add_argument("--has-price", action="store_true")
    p.add_argument("--since", help="起始日期 YYYY-MM-DD")
    p.add_argument("--until", help="結束日期 YYYY-MM-DD")
    p.add_argument("--days", type=int, help="只看最近 N 天")
    p.add_argument("--sort", default="recent",
                   choices=["recent", "oldest", "relevance", "price_asc", "price_desc"])
    p.add_argument("--limit", type=int, default=30)
    p.set_defaults(func=_cmd_search)

    p = sub.add_parser("add", help="手動加入一筆")
    p.add_argument("--url", default="")
    p.add_argument("--title", default="")
    p.add_argument("--text", default="")
    p.add_argument("--kind", default="post", choices=KINDS)
    p.add_argument("--source", default="manual", choices=SOURCES)
    p.add_argument("--price")
    p.add_argument("--tags", default="")
    p.add_argument("--note", default="")
    p.set_defaults(func=_cmd_add)

    p = sub.add_parser("stats", help="資料統計")
    p.set_defaults(func=_cmd_stats)

    args = ap.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
