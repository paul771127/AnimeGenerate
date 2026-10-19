"""site_contact 的離線測試:用假的 Fetcher 餵固定 HTML,不碰網路。"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from leadgen.enrich.site_contact import find_contacts  # noqa: E402
from leadgen.http import RobotsDenied  # noqa: E402

HOME = """
<html><body>
  <a href="/about-us">關於我們</a>
  <a href="/contact">聯絡我們</a>
  <a href="https://partner.example.org/contact">合作夥伴</a>
  <img src="logo@2x.png">
</body></html>
"""

CONTACT = """
<html><body>
  <p>業務聯絡:<a href="mailto:sales@acme.com.tw">sales@acme.com.tw</a></p>
  <p>一般諮詢 info@acme.com.tw</p>
  <p>誤觸信箱 noreply@acme.com.tw</p>
  <p>其他公司 hello@partner.example.org</p>
  <p>電話 (02)2911-2241</p>
</body></html>
"""


class FakeFetcher:
    def __init__(self, pages: dict[str, str], denied: set[str] | None = None):
        self.pages = pages
        self.denied = denied or set()
        self.calls: list[str] = []

    def get(self, url: str, **kw) -> str:
        self.calls.append(url)
        if url in self.denied:
            raise RobotsDenied(url)
        if url not in self.pages:
            raise RuntimeError("404 " + url)
        return self.pages[url]


def test_find_contacts_prefers_role_email_and_same_domain():
    f = FakeFetcher({
        "https://acme.com.tw": HOME,
        "https://acme.com.tw/contact": CONTACT,
    })
    emails, phones = find_contacts(f, "https://acme.com.tw")
    assert emails, "應該要抓到聯絡頁的信箱"
    assert emails[0] in ("info@acme.com.tw", "sales@acme.com.tw"), emails
    assert all(e.endswith("acme.com.tw") for e in emails), f"不該收別家公司的信箱:{emails}"
    assert "noreply@acme.com.tw" not in emails
    assert any(p.startswith("02") for p in phones)


def test_find_contacts_respects_robots_denied_homepage():
    f = FakeFetcher({}, denied={"https://blocked.com"})
    assert find_contacts(f, "https://blocked.com") == ([], [])


def test_find_contacts_handles_missing_pages():
    f = FakeFetcher({"https://bare.com": "<html><body>沒有聯絡資訊</body></html>"})
    emails, phones = find_contacts(f, "https://bare.com")
    assert emails == [] and phones == []


def test_find_contacts_no_pattern_guessing():
    """確認實作沒有在猜 名.姓@網域 這種信箱。"""
    src = Path(__file__).resolve().parents[1] / "leadgen" / "enrich" / "site_contact.py"
    text = src.read_text(encoding="utf-8")
    for forbidden in ("first", "last_name", "%s@%s", "guess"):
        assert forbidden not in text.lower().replace("pattern guessing", ""), forbidden


def test_find_contacts_stays_within_page_budget():
    pages = {f"https://acme.com.tw/p{i}": "<html></html>" for i in range(20)}
    pages["https://acme.com.tw"] = "".join(
        [f'<a href="/p{i}">contact {i}</a>' for i in range(20)]
    )
    f = FakeFetcher(pages)
    find_contacts(f, "https://acme.com.tw", max_pages=3)
    assert len(f.calls) <= 4, f"抓了 {len(f.calls)} 頁,超過上限會對人家站台造成負擔"
