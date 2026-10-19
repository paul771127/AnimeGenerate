"""不碰網路的單元測試:去重、評分、聯絡資訊抽取、退訂名單。"""
from __future__ import annotations

import sys
from datetime import date, timedelta
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from leadgen.models import (  # noqa: E402
    Lead, extract_emails, extract_phones, is_role_email, normalize_org,
    registrable_domain,
)
from leadgen.pipeline import dedupe  # noqa: E402
from leadgen.scoring import score_lead  # noqa: E402
from leadgen.store import Store  # noqa: E402

SCORING = {
    "weights": {"intent": 30, "budget": 25, "fit": 25, "reach": 10, "fresh": 10},
    "budget_full_at_twd": 2_000_000,
    "fresh_half_life_days": 30,
    "profile": {"core": ["韌體", "firmware"], "plus": ["iot"], "exclude": ["保全"]},
}


def test_normalize_org():
    assert normalize_org("奧義智慧科技股份有限公司") == normalize_org("奧義智慧科技")
    assert normalize_org("Acme Co., Ltd.") == normalize_org("acme")


def test_registrable_domain():
    assert registrable_domain("https://www.Example.com/contact") == "example.com"
    assert registrable_domain("info@Example.com") == "example.com"
    assert registrable_domain("") == ""


def test_extract_contacts():
    text = "聯絡 info@acme.com.tw 或 sales@acme.com.tw,電話 (02)2911-2241,手機 0912-345-678"
    emails = extract_emails(text)
    assert "info@acme.com.tw" in emails and "sales@acme.com.tw" in emails
    assert any(p.startswith("02") for p in extract_phones(text))
    assert any(p.startswith("0912") for p in extract_phones(text))
    # 圖片檔名不該被當成信箱
    assert extract_emails("logo@2x.png") == []


def test_role_email():
    assert is_role_email("info@acme.com")
    assert is_role_email("contact-us@acme.com")
    assert not is_role_email("jason.lin@acme.com")


def test_dedupe_merges_contacts_across_signals():
    a = Lead(org="Acme", source="gov_tender", source_url="u1",
             signal_title="案 A", signal_type="tender_open",
             website="https://acme.com")
    b = Lead(org="Acme Co., Ltd.", source="remotive", source_url="u2",
             signal_title="Firmware Engineer", signal_type="job",
             website="https://acme.com", emails=["info@acme.com"])
    out = dedupe([a, b])
    assert len(out) == 2, "不同訊號要各自保留一筆"
    assert all("info@acme.com" in l.emails for l in out), "同一組織的聯絡方式要互補"


def test_dedupe_drops_identical_signal():
    mk = lambda: Lead(org="Acme", source="gov_tender", source_url="u",
                      signal_title="案 A", signal_type="tender_open",
                      signal_date="2026-10-01", website="https://acme.com")
    assert len(dedupe([mk(), mk()])) == 1


def test_scoring_ranks_intent_above_registry():
    today = date.today().isoformat()
    hot = Lead(org="A", source="s", source_url="u", signal_type="freelance_request",
               signal_title="SEEKING FREELANCER firmware stm32", signal_date=today,
               emails=["info@a.com"], budget_twd=2_000_000)
    cold = Lead(org="B", source="s", source_url="u", signal_type="registry",
                signal_title="登記資料", signal_date=today)
    hot_score, detail = score_lead(hot, SCORING)
    cold_score, _ = score_lead(cold, SCORING)
    assert hot_score > 85, detail
    assert cold_score < 20
    assert hot_score > cold_score


def test_scoring_exclude_zeroes_fit():
    lead = Lead(org="A", source="s", source_url="u", signal_type="tender_open",
                signal_title="保全系統維護", signal_date=date.today().isoformat())
    _, detail = score_lead(lead, SCORING)
    assert detail["fit"] == 0.0


def test_scoring_stale_signal_loses_freshness():
    old = (date.today() - timedelta(days=200)).isoformat()
    lead = Lead(org="A", source="s", source_url="u", signal_type="tender_open",
                signal_title="韌體開發", signal_date=old)
    _, detail = score_lead(lead, SCORING)
    assert detail["fresh"] == 0.0


def test_store_roundtrip_and_suppression(tmp_path):
    store = Store(tmp_path / "t.sqlite3")
    lead = Lead(org="Acme", source="s", source_url="u", signal_title="案",
                signal_type="tender_open", emails=["info@acme.com"],
                website="https://acme.com", score=80)
    assert store.upsert(lead) == "inserted"
    assert store.upsert(lead) == "updated"
    assert len(store.top(min_score=70)) == 1

    store.suppress("acme.com", "unsubscribe")
    assert store.is_suppressed(lead)
    assert store.upsert(lead) == "suppressed", "退訂後不得再寫入"
    store.close()


def test_store_preserves_manual_status(tmp_path):
    store = Store(tmp_path / "t.sqlite3")
    lead = Lead(org="Acme", source="s", source_url="u", signal_title="案",
                signal_type="tender_open", score=80)
    store.upsert(lead)
    store.conn.execute("UPDATE leads SET status='contacted', notes='已寄信'")
    store.conn.commit()
    store.upsert(lead)
    row = store.top()[0]
    assert row["status"] == "contacted" and row["notes"] == "已寄信"
    store.close()
