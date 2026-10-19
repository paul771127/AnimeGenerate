"""從公司官網的公開聯絡頁取得商業聯絡方式。

明確的界線:
  * 只讀 robots.txt 允許的頁面
  * 只抓公司自己張貼在官網上、給外界聯絡用的信箱
  * 絕不做 email pattern guessing(不組 名.姓@公司.com 這種),那是猜測個資,
    而且猜錯會造成大量退信、把你的寄件域名信譽燒掉
  * 優先採用 info@ / contact@ / sales@ 這類角色信箱
"""
from __future__ import annotations

import logging
import re
from urllib.parse import urljoin, urlparse

from bs4 import BeautifulSoup

from ..http import Fetcher, RobotsDenied
from ..models import extract_emails, extract_phones, is_role_email

log = logging.getLogger("leadgen.site_contact")

CONTACT_HINTS = re.compile(
    r"(contact|about|聯絡|聯繫|關於|客服|業務|服務|inquiry|support|imprint)",
    re.IGNORECASE,
)
CANDIDATE_PATHS = ("/contact", "/contact-us", "/about", "/about-us", "/zh-tw/contact")

# 這些信箱抓到也沒用,排除
JUNK = re.compile(r"(no-?reply|example\.com|sentry\.io|wixpress|@email|u002)", re.IGNORECASE)


def _clean(emails: list[str]) -> list[str]:
    out = [e for e in emails if not JUNK.search(e)]
    # 角色信箱排前面
    out.sort(key=lambda e: (not is_role_email(e), len(e)))
    return out[:5]


def find_contacts(fetcher: Fetcher, website: str, max_pages: int = 4) -> tuple[list[str], list[str]]:
    """回傳 (emails, phones)。失敗就回空,不拋例外。"""
    if not website:
        return [], []
    if "://" not in website:
        website = "https://" + website
    root = f"{urlparse(website).scheme}://{urlparse(website).netloc}"
    host = urlparse(root).netloc

    emails: list[str] = []
    phones: list[str] = []
    visited: set[str] = set()
    queue: list[str] = [website]

    # 先把首頁的候選連結找出來,再補上常見路徑
    try:
        home = fetcher.get(website)
        visited.add(website)
        emails += extract_emails(home)
        phones += extract_phones(home)
        soup = BeautifulSoup(home, "lxml")
        for a in soup.find_all("a", href=True):
            label = f"{a.get_text(' ', strip=True)} {a['href']}"
            if not CONTACT_HINTS.search(label):
                continue
            url = urljoin(root, a["href"])
            if urlparse(url).netloc == host and url not in visited:
                queue.append(url)
        # mailto: 是站方明確張貼的聯絡方式,優先採用
        for a in soup.select('a[href^="mailto:"]'):
            emails += extract_emails(a["href"])
    except RobotsDenied as e:
        log.info("robots.txt 不允許,跳過:%s", e)
        return [], []
    except Exception as e:
        log.debug("首頁讀取失敗 %s: %s", website, e)

    queue += [root + p for p in CANDIDATE_PATHS]

    for url in queue:
        if len(visited) >= max_pages + 1 or (emails and phones):
            break
        if url in visited:
            continue
        visited.add(url)
        try:
            body = fetcher.get(url)
        except RobotsDenied:
            continue
        except Exception:
            continue
        emails += extract_emails(body)
        phones += extract_phones(body)
        soup = BeautifulSoup(body, "lxml")
        for a in soup.select('a[href^="mailto:"]'):
            emails += extract_emails(a["href"])

    # 只留同網域(或其子網域)的信箱,避免抓到合作夥伴或代理商
    base = host.removeprefix("www.")
    same = [e for e in _clean(list(dict.fromkeys(emails)))
            if e.rsplit("@", 1)[-1].endswith(base) or base.endswith(e.rsplit("@", 1)[-1])]
    return (same or _clean(list(dict.fromkeys(emails)))[:2],
            list(dict.fromkeys(phones))[:3])
