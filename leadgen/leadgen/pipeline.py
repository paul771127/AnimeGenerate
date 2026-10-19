"""主流程:collect -> dedupe -> enrich -> score -> store。"""
from __future__ import annotations

import logging
from pathlib import Path

import yaml

from .enrich import domain_lookup
from .enrich.site_contact import find_contacts
from .http import Fetcher
from .models import Lead
from .scoring import score_all
from .sources import base as source_base
from .store import Store

log = logging.getLogger("leadgen.pipeline")


def load_config(path: str | Path) -> dict:
    with open(path, encoding="utf-8") as f:
        return yaml.safe_load(f) or {}


def build_fetcher(cfg: dict) -> Fetcher:
    net = cfg.get("network") or {}
    contact = net.get("contact_email", "")
    if not contact or "@" not in contact:
        raise SystemExit(
            "請先在 config.yaml 的 network.contact_email 填你的真實信箱。\n"
            "爬取公開資料時 User-Agent 必須留得到人 —— 這是對方站長要擋你或"
            "聯絡你時唯一的管道,也是你主張自己不是惡意爬蟲的依據。"
        )
    ua = (f"{net.get('bot_name','leadgen-bot')}/1.0 "
          f"(+contact: {contact})")
    return Fetcher(
        user_agent=ua,
        cache_dir=Path(net.get("cache_dir", ".cache")),
        rate_per_host=float(net.get("rate_per_host", 1.5)),
        timeout=float(net.get("timeout", 25)),
        max_retries=int(net.get("max_retries", 3)),
        respect_robots=bool(net.get("respect_robots", True)),
        cache_ttl_hours=float(net.get("cache_ttl_hours", 12)),
    )


def collect(cfg: dict, only: list[str] | None = None) -> list[Lead]:
    source_base.load_all()
    fetcher = build_fetcher(cfg)
    wanted = only or [
        name for name, sc in (cfg.get("sources") or {}).items()
        if sc.get("enabled", True)
    ]

    leads: list[Lead] = []
    for name in wanted:
        scfg = (cfg.get("sources") or {}).get(name) or {}
        try:
            src = source_base.get_source(name)
        except KeyError as e:
            log.error("%s", e)
            continue
        log.info("抓取來源:%s", name)
        got = 0
        try:
            for lead in src.fetch(fetcher, scfg):
                leads.append(lead)
                got += 1
        except Exception as e:
            log.error("來源 %s 中斷(已取得 %s 筆):%s", name, got, e)
        log.info("  -> %s 筆", got)
    return leads


def dedupe(leads: list[Lead]) -> list[Lead]:
    """同一訊號只留一筆,並把同一組織的聯絡方式互相補齊。"""
    # 機關發「更正公告」時同一個案子會以不同日期出現多次,所以收斂的 key 不含
    # 日期,只留最新的那一筆,但把聯絡方式併進去。
    by_signal: dict[tuple[str, str, str], Lead] = {}
    for lead in leads:
        key = (lead.dedupe_key, lead.signal_title, lead.signal_type)
        if (cur := by_signal.get(key)) is not None:
            newer = lead if (lead.signal_date or "") > (cur.signal_date or "") else cur
            older = cur if newer is lead else lead
            newer.emails = list(dict.fromkeys(newer.emails + older.emails))
            newer.phones = list(dict.fromkeys(newer.phones + older.phones))
            newer.website = newer.website or older.website
            newer.budget_twd = newer.budget_twd or older.budget_twd
            by_signal[key] = newer
            continue
        by_signal[key] = lead

    contacts: dict[str, tuple[list[str], list[str], str]] = {}
    for lead in by_signal.values():
        e, p, w = contacts.get(lead.dedupe_key, ([], [], ""))
        contacts[lead.dedupe_key] = (
            list(dict.fromkeys(e + lead.emails)),
            list(dict.fromkeys(p + lead.phones)),
            w or lead.website,
        )
    for lead in by_signal.values():
        e, p, w = contacts[lead.dedupe_key]
        lead.emails, lead.phones = e, p
        lead.website = lead.website or w
    return list(by_signal.values())


def enrich(leads: list[Lead], cfg: dict, fetcher: Fetcher | None = None) -> list[Lead]:
    ecfg = cfg.get("enrich") or {}
    if not ecfg.get("enabled", True):
        return leads
    fetcher = fetcher or build_fetcher(cfg)
    budget = int(ecfg.get("max_sites", 25))
    min_score_hint = int(ecfg.get("min_prescore", 0))

    # 先用未 enrich 的分數排序,只對最有價值的前 N 名花網路成本
    scored = score_all(list(leads), cfg.get("scoring") or {})
    done: dict[str, tuple[list[str], list[str]]] = {}
    used = 0

    for lead in scored:
        if used >= budget:
            break
        if lead.emails or lead.score < min_score_hint:
            continue
        key = lead.dedupe_key

        if key not in done:
            site = lead.website
            if not site and domain_lookup.enabled():
                site = domain_lookup.find_website(fetcher, lead.org, lead.location[:6])
                lead.website = site
            if not site:
                continue
            used += 1
            log.info("補聯絡方式 [%s/%s] %s -> %s", used, budget, lead.org, site)
            done[key] = find_contacts(fetcher, site, int(ecfg.get("pages_per_site", 4)))

        e, p = done[key]
        lead.emails = list(dict.fromkeys(lead.emails + e))
        lead.phones = list(dict.fromkeys(lead.phones + p))

    # 把同組織的結果擴散回所有訊號
    for lead in scored:
        if (hit := done.get(lead.dedupe_key)):
            lead.emails = list(dict.fromkeys(lead.emails + hit[0]))
            lead.phones = list(dict.fromkeys(lead.phones + hit[1]))
    return scored


def run(cfg: dict, only: list[str] | None = None, skip_enrich: bool = False) -> dict:
    store = Store(cfg.get("db", "leads.sqlite3"))
    try:
        raw = collect(cfg, only)
        leads = dedupe(raw)
        if not skip_enrich:
            leads = enrich(leads, cfg)
        leads = score_all(leads, cfg.get("scoring") or {})

        counts = {"inserted": 0, "updated": 0, "suppressed": 0}
        for lead in leads:
            counts[store.upsert(lead)] += 1
        return {
            "raw": len(raw),
            "after_dedupe": len(leads),
            **counts,
            "stats": store.stats(),
        }
    finally:
        store.close()
