"""Reddit 來源的離線測試。

Reddit 的 robots.txt 對所有 user-agent 是 Disallow: /(連公開的 .rss 都在內),
所以這個來源只走官方 OAuth API,沒有憑證就自動停用。這裡用官方 API 的 JSON
格式當固定資料測解析與過濾,不碰網路。

注意:OAuth 的實際連線沒有在這裡端到端驗證過 —— 需要使用者自己的
REDDIT_CLIENT_ID / REDDIT_CLIENT_SECRET,我不會也不該去申請。
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from leadgen.sources import reddit as reddit_mod  # noqa: E402
from leadgen.sources.reddit import RedditSource  # noqa: E402

# https://oauth.reddit.com/r/<sub>/new 的回應格式
POSTS = [
    {
        "title": "[USA-IL] Drawer Insert for IKEA Helmer Storage Unit",
        "author": "maker01",
        "url": "https://www.reddit.com/r/3Dprintmything/comments/a1/bracket/",
        "body": "Need a STEP file and a printed bracket in PETG.",
        "created": 1792000000,
    },
    {
        "title": "[For Hire] Embedded firmware developer, STM32",
        "author": "seller99",
        "url": "https://www.reddit.com/r/forhire/comments/a2/fh/",
        "body": "I write firmware, hire me!",
        "created": 1792003600,
    },
    {
        "title": "[Hiring] ESP32 firmware contractor for a sensor product",
        "author": "client42",
        "url": "https://www.reddit.com/r/forhire/comments/a3/h/",
        "body": "Looking for an ESP32 firmware contractor. mail me at a@b.io",
        "created": 1792007200,
    },
    {
        "title": "[USA-TX] Question about filament",
        "author": "random",
        "url": "https://www.reddit.com/r/3Dprintmything/comments/a4/q/",
        "body": "What is the best filament brand?",
        "created": 1792010800,
    },
]

KEYWORDS = ["3d print", "step file", "bracket", "firmware", "esp32", "stm32"]


def _leads(sub, require, skip, keywords=KEYWORDS):
    src = RedditSource()
    return [l for l in (src._to_lead(p, sub, require, skip, keywords)
                        for p in POSTS) if l]


def test_for_hire_posts_are_excluded():
    """[For Hire] 是同行在推銷自己,不是客戶 —— 撈進來等於把競爭者當名單。"""
    titles = [l.signal_title for l in _leads("forhire", ["[hiring]"], ["[for hire]"])]
    assert any("ESP32" in t for t in titles)
    assert not any("Embedded firmware developer" in t for t in titles)


def test_location_tag_is_parsed_but_hiring_marker_is_not():
    """標題開頭的 [USA-IL] 是發文者所在地,但 [Hiring] 不是地點。"""
    bracket = next(l for l in _leads("3Dprintmything", [], [])
                   if "Drawer Insert" in l.signal_title)
    assert bracket.location == "USA-IL"
    assert bracket.signal_title == "Drawer Insert for IKEA Helmer Storage Unit"

    hiring = _leads("forhire", ["[hiring]"], ["[for hire]"])[0]
    assert hiring.location == "", f"[Hiring] 不該被當成地點:{hiring.location!r}"


def test_keyword_filter_drops_chatter():
    """板上的閒聊問題不是案源。"""
    leads = _leads("3Dprintmything", [], [])
    assert not any("filament" in l.signal_title.lower() for l in leads)


def test_contact_is_reddit_dm_plus_any_posted_email():
    lead = _leads("forhire", ["[hiring]"], ["[for hire]"])[0]
    assert lead.contacts_other == ["Reddit 私訊 u/client42"]
    assert lead.emails == ["a@b.io"], lead.emails
    assert lead.has_contact


def test_created_timestamp_becomes_signal_date():
    lead = _leads("3Dprintmything", [], [])[0]
    assert lead.signal_date.startswith("2026-"), lead.signal_date


def test_source_is_disabled_without_credentials(monkeypatch):
    """沒有憑證時必須什麼都不做 —— 不可以改用 RSS 或其他方式繞過 robots.txt。"""
    monkeypatch.delenv("REDDIT_CLIENT_ID", raising=False)
    monkeypatch.delenv("REDDIT_CLIENT_SECRET", raising=False)
    assert not reddit_mod.oauth_enabled()

    class ExplodingFetcher:
        ua = "test"
        timeout = 10

        def get(self, *a, **k):
            raise AssertionError("沒有憑證時不該發出任何請求")

        def get_json(self, *a, **k):
            raise AssertionError("沒有憑證時不該發出任何請求")

    assert list(RedditSource().fetch(ExplodingFetcher(), {})) == []


def test_no_rss_fallback_remains_in_source():
    """守住這件事:robots.txt 是 Disallow: /,不能有繞過它的程式路徑。"""
    src = Path(reddit_mod.__file__).read_text(encoding="utf-8")
    for forbidden in ("new.rss", "_via_rss", "etree", "www.reddit.com/r/"):
        assert forbidden not in src, f"RSS 繞道殘留:{forbidden}"
