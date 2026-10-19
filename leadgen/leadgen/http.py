"""遵守 robots.txt、限速、帶快取的 HTTP client。

這個檔案是整套系統「不會被封鎖、不會變成惡意爬蟲」的關鍵:
  * 每個 host 都先讀 robots.txt,被 disallow 就直接不抓
  * 每個 host 獨立限速,預設 1.5 req/s
  * User-Agent 帶上聯絡信箱,對方站長要擋你或聯絡你都找得到人
  * 回應寫入磁碟快取,重跑不會重複打對方站台
  * 失敗指數退避,不硬打
"""
from __future__ import annotations

import hashlib
import json
import logging
import time
import urllib.robotparser as robotparser
from pathlib import Path
from urllib.parse import urlparse

import requests

log = logging.getLogger("leadgen.http")


class RobotsDenied(Exception):
    """robots.txt 不允許抓這個 URL。"""


class Fetcher:
    def __init__(
        self,
        user_agent: str,
        cache_dir: Path,
        rate_per_host: float = 1.5,
        timeout: float = 25.0,
        max_retries: int = 3,
        respect_robots: bool = True,
        cache_ttl_hours: float = 12.0,
    ):
        self.ua = user_agent
        self.cache_dir = Path(cache_dir)
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        self.min_interval = 1.0 / rate_per_host if rate_per_host > 0 else 0.0
        self.timeout = timeout
        self.max_retries = max_retries
        self.respect_robots = respect_robots
        self.cache_ttl = cache_ttl_hours * 3600
        self._last_hit: dict[str, float] = {}
        self._robots: dict[str, robotparser.RobotFileParser | None] = {}
        self.session = requests.Session()
        self.session.headers.update({
            "User-Agent": self.ua,
            "Accept-Language": "zh-TW,zh;q=0.9,en;q=0.8",
        })

    # ---------- robots ----------
    def _robots_for(self, host_root: str):
        if host_root in self._robots:
            return self._robots[host_root]
        rp = robotparser.RobotFileParser()
        rp.set_url(f"{host_root}/robots.txt")
        try:
            resp = self.session.get(f"{host_root}/robots.txt", timeout=10)
            if resp.status_code == 200 and "text" in resp.headers.get("content-type", ""):
                rp.parse(resp.text.splitlines())
            else:
                # 沒有 robots.txt 或被 CDN 擋 -> 視為未宣告,仍照限速走
                rp = None
        except requests.RequestException:
            rp = None
        self._robots[host_root] = rp
        return rp

    def allowed(self, url: str) -> bool:
        if not self.respect_robots:
            return True
        p = urlparse(url)
        root = f"{p.scheme}://{p.netloc}"
        rp = self._robots_for(root)
        if rp is None:
            return True
        return rp.can_fetch(self.ua, url)

    # ---------- 限速 ----------
    def _throttle(self, host: str):
        if self.min_interval <= 0:
            return
        last = self._last_hit.get(host, 0.0)
        wait = self.min_interval - (time.monotonic() - last)
        if wait > 0:
            time.sleep(wait)
        self._last_hit[host] = time.monotonic()

    # ---------- 快取 ----------
    def _cache_path(self, url: str) -> Path:
        return self.cache_dir / (hashlib.sha1(url.encode()).hexdigest() + ".json")

    def _read_cache(self, url: str):
        p = self._cache_path(url)
        if not p.exists() or (time.time() - p.stat().st_mtime) > self.cache_ttl:
            return None
        try:
            return json.loads(p.read_text())["body"]
        except Exception:
            return None

    def _write_cache(self, url: str, body: str):
        try:
            self._cache_path(url).write_text(json.dumps({"url": url, "body": body}))
        except OSError as e:
            log.debug("cache write failed: %s", e)

    # ---------- 對外 ----------
    def get(self, url: str, *, use_cache: bool = True, params: dict | None = None) -> str:
        if params:
            req = requests.Request("GET", url, params=params).prepare()
            url = req.url
        if use_cache and (cached := self._read_cache(url)) is not None:
            log.debug("cache hit %s", url)
            return cached
        if not self.allowed(url):
            raise RobotsDenied(url)

        host = urlparse(url).netloc
        last_err: Exception | None = None
        for attempt in range(self.max_retries):
            self._throttle(host)
            try:
                r = self.session.get(url, timeout=self.timeout)
                if r.status_code == 429 or 500 <= r.status_code < 600:
                    raise requests.HTTPError(f"status {r.status_code}")
                if r.status_code in (401, 403):
                    # 需要登入或被擋:不繞、不重試,直接放棄這個來源
                    raise RobotsDenied(f"{url} -> {r.status_code} (需登入或被阻擋,不繞過)")
                r.raise_for_status()
                if use_cache:
                    self._write_cache(url, r.text)
                return r.text
            except RobotsDenied:
                raise
            except requests.RequestException as e:
                last_err = e
                backoff = 2 ** attempt
                log.warning("fetch failed (%s/%s) %s: %s; retry in %ss",
                            attempt + 1, self.max_retries, url, e, backoff)
                time.sleep(backoff)
        raise RuntimeError(f"fetch failed after retries: {url}") from last_err

    def get_json(self, url: str, *, use_cache: bool = True, params: dict | None = None):
        return json.loads(self.get(url, use_cache=use_cache, params=params))
