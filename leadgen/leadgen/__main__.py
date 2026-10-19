"""CLI 入口:python -m leadgen <command>"""
from __future__ import annotations

import argparse
import json
import logging
import sys

from . import export as exporter
from .pipeline import load_config, run
from .scoring import score_lead
from .sources import base as source_base
from .store import Store


def _setup_logging(verbose: bool):
    logging.basicConfig(
        level=logging.DEBUG if verbose else logging.INFO,
        format="%(asctime)s %(levelname)-7s %(name)s | %(message)s",
        datefmt="%H:%M:%S",
    )


def cmd_sources(args):
    source_base.load_all()
    cfg = load_config(args.config)
    enabled = cfg.get("sources") or {}
    for name in source_base.available():
        flag = "on " if (enabled.get(name) or {}).get("enabled", False) else "off"
        print(f"[{flag}] {name}")


def cmd_collect(args):
    cfg = load_config(args.config)
    result = run(cfg, only=args.source or None, skip_enrich=args.no_enrich)
    print(json.dumps(result, ensure_ascii=False, indent=2))


def cmd_rescore(args):
    """改了 config.yaml 的權重或 profile 之後,不用重抓就能重算分數。"""
    from .models import Lead
    cfg = load_config(args.config)
    store = Store(cfg.get("db", "leads.sqlite3"))
    changed = 0
    for r in store.all_rows():
        lead = Lead(
            org=r["org"], source=r["source"], source_url=r["source_url"],
            signal_title=r["signal_title"] or "", signal_type=r["signal_type"] or "",
            signal_date=r["signal_date"] or "", website=r["website"] or "",
            emails=json.loads(r["emails"] or "[]"), phones=json.loads(r["phones"] or "[]"),
            location=r["location"] or "", budget_twd=r["budget_twd"],
            tags=json.loads(r["tags"] or "[]"), evidence=r["evidence"] or "",
            notes=r["notes"] or "", collected_at=r["collected_at"] or "",
        )
        score, detail = score_lead(lead, cfg.get("scoring") or {})
        if score != r["score"]:
            store.rescore(r["signal_key"], score, detail)
            changed += 1
    print(f"重算完成,{changed} 筆分數變動")
    print(json.dumps(store.stats(), ensure_ascii=False, indent=2))
    store.close()


def cmd_export(args):
    cfg = load_config(args.config)
    store = Store(cfg.get("db", "leads.sqlite3"))
    if args.csv:
        n = exporter.to_csv(store, args.csv, min_score=args.min_score,
                            require_contact=args.require_contact, limit=args.limit)
        print(f"CSV {n} 筆 -> {args.csv}")
    if args.md:
        n = exporter.to_markdown(store, args.md, min_score=args.min_score,
                                 limit=min(args.limit, 50))
        print(f"Markdown {n} 筆 -> {args.md}")
    if not args.csv and not args.md:
        print("請指定 --csv 或 --md", file=sys.stderr)
    store.close()


def cmd_stats(args):
    cfg = load_config(args.config)
    store = Store(cfg.get("db", "leads.sqlite3"))
    print(json.dumps(store.stats(), ensure_ascii=False, indent=2))
    store.close()


def cmd_suppress(args):
    cfg = load_config(args.config)
    store = Store(cfg.get("db", "leads.sqlite3"))
    for v in args.value:
        store.suppress(v, args.reason)
        print(f"已加入永久阻擋名單:{v}（{args.reason}）")
    store.close()


def main(argv=None):
    p = argparse.ArgumentParser(prog="leadgen", description="接案潛在客戶名單系統")
    p.add_argument("-c", "--config", default="config.yaml")
    p.add_argument("-v", "--verbose", action="store_true")
    sub = p.add_subparsers(dest="cmd", required=True)

    s = sub.add_parser("sources", help="列出可用來源與啟用狀態")
    s.set_defaults(func=cmd_sources)

    s = sub.add_parser("collect", help="抓取、去重、補聯絡方式、評分、入庫")
    s.add_argument("--source", action="append", help="只跑指定來源(可重複)")
    s.add_argument("--no-enrich", action="store_true", help="跳過官網聯絡頁抓取")
    s.set_defaults(func=cmd_collect)

    s = sub.add_parser("rescore", help="依現在的 config 重算所有分數")
    s.set_defaults(func=cmd_rescore)

    s = sub.add_parser("export", help="匯出 CSV / Markdown 戰報")
    s.add_argument("--csv")
    s.add_argument("--md")
    s.add_argument("--min-score", type=int, default=70)
    s.add_argument("--limit", type=int, default=1000)
    s.add_argument("--require-contact", action="store_true", help="只匯出有聯絡方式的")
    s.set_defaults(func=cmd_export)

    s = sub.add_parser("stats", help="看資料庫概況")
    s.set_defaults(func=cmd_stats)

    s = sub.add_parser("suppress", help="把 email 或網域加入永久阻擋名單(退訂用)")
    s.add_argument("value", nargs="+")
    s.add_argument("--reason", default="unsubscribe")
    s.set_defaults(func=cmd_suppress)

    args = p.parse_args(argv)
    _setup_logging(args.verbose)
    args.func(args)


if __name__ == "__main__":
    main()
