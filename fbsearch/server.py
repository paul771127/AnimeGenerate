"""本機網頁介面 + JSON API(只用標準函式庫)。

預設只綁 127.0.0.1,資料不會離開你的電腦。

API:
  GET    /api/search   ?q=&kind=&source=&price_min=&price_max=&has_price=1
                       &date_from=YYYY-MM-DD&date_to=YYYY-MM-DD&sort=&limit=&offset=
  GET    /api/stats
  POST   /api/items          JSON {url,title,text,kind,source,price,tags,note}
  PATCH  /api/items/<id>     JSON 欄位
  DELETE /api/items/<id>
  POST   /api/import?name=檔名.zip|.json   (request body 為檔案內容)
  GET    /api/export         下載全部資料 JSON
"""

from __future__ import annotations

import json
import shutil
import tempfile
import threading
import time
from datetime import datetime
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlparse

from .db import Item, Query, Store
from .importer import iter_export

STATIC = Path(__file__).parent / "static"
MAX_UPLOAD = 4 * 1024**3  # 4 GB


def parse_date(s: str | None, end: bool = False) -> int | None:
    if not s:
        return None
    try:
        d = datetime.strptime(s.strip(), "%Y-%m-%d")
    except ValueError:
        return None
    ts = int(d.timestamp())
    return ts + 86399 if end else ts


def _num(s: str | None) -> float | None:
    if s is None or str(s).strip() == "":
        return None
    try:
        return float(str(s).replace(",", ""))
    except ValueError:
        return None


def query_from_params(p: dict[str, str]) -> Query:
    def _int(key: str, default: int, lo: int, hi: int) -> int:
        try:
            return max(lo, min(hi, int(p.get(key, default))))
        except ValueError:
            return default

    return Query(
        q=p.get("q", ""),
        kind=p.get("kind", ""),
        source=p.get("source", ""),
        price_min=_num(p.get("price_min")),
        price_max=_num(p.get("price_max")),
        has_price=p.get("has_price") in ("1", "true", "on"),
        date_from=parse_date(p.get("date_from")),
        date_to=parse_date(p.get("date_to"), end=True),
        sort=p.get("sort", "recent"),
        limit=_int("limit", 50, 1, 500),
        offset=_int("offset", 0, 0, 10**9),
    )


def item_from_json(d: dict) -> Item:
    ts = d.get("ts")
    try:
        ts = int(ts) if ts else int(time.time())
    except (TypeError, ValueError):
        ts = int(time.time())
    return Item(
        kind=str(d.get("kind") or "post"),
        source=str(d.get("source") or "manual"),
        title=str(d.get("title") or "").strip(),
        text=str(d.get("text") or "").strip(),
        url=str(d.get("url") or "").strip(),
        author=str(d.get("author") or "").strip(),
        price=_num(d.get("price")),
        tags=str(d.get("tags") or "").strip(),
        note=str(d.get("note") or "").strip(),
        ts=ts,
        origin="manual",
    )


def make_handler(store: Store):
    lock = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        server_version = "fbsearch/0.1"

        def log_message(self, fmt, *args):  # 安靜一點
            pass

        # ---------------------------------------------------------- 工具
        def _send(self, status: int, body: bytes, ctype: str, extra: dict | None = None):
            self.send_response(status)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            for k, v in (extra or {}).items():
                self.send_header(k, v)
            self.end_headers()
            self.wfile.write(body)

        def _json(self, obj, status: int = 200, extra: dict | None = None):
            self._send(status, json.dumps(obj, ensure_ascii=False).encode("utf-8"),
                       "application/json; charset=utf-8", extra)

        def _error(self, status: int, msg: str):
            self._json({"error": msg}, status)

        def _body_json(self) -> dict:
            n = int(self.headers.get("Content-Length") or 0)
            if n <= 0 or n > 5 * 1024**2:
                return {}
            try:
                data = json.loads(self.rfile.read(n).decode("utf-8"))
            except (ValueError, UnicodeDecodeError):
                return {}
            return data if isinstance(data, dict) else {}

        def _same_origin(self) -> bool:
            # 防止其他網站用 CSRF 對本機 API 寫資料
            origin = self.headers.get("Origin")
            if not origin:
                return True
            host = self.headers.get("Host", "")
            return urlparse(origin).netloc == host

        def _item_id(self, path: str) -> int | None:
            try:
                return int(path.rsplit("/", 1)[-1])
            except ValueError:
                return None

        # ---------------------------------------------------------- GET
        def do_GET(self):
            u = urlparse(self.path)
            params = {k: v[-1] for k, v in parse_qs(u.query).items()}
            if u.path in ("/", "/index.html", "/add"):
                body = (STATIC / "index.html").read_bytes()
                return self._send(200, body, "text/html; charset=utf-8")
            if u.path == "/api/search":
                with lock:
                    return self._json(store.search(query_from_params(params)))
            if u.path == "/api/stats":
                with lock:
                    return self._json(store.stats())
            if u.path == "/api/export":
                with lock:
                    body = store.export_json().encode("utf-8")
                return self._send(200, body, "application/json; charset=utf-8",
                                  {"Content-Disposition": 'attachment; filename="fbsearch-export.json"'})
            if u.path.startswith("/api/items/"):
                iid = self._item_id(u.path)
                with lock:
                    it = store.get(iid) if iid else None
                return self._json(it) if it else self._error(404, "找不到這筆資料")
            self._error(404, "not found")

        # ---------------------------------------------------------- 寫入
        def do_POST(self):
            if not self._same_origin():
                return self._error(403, "forbidden")
            u = urlparse(self.path)
            if u.path == "/api/items":
                item = item_from_json(self._body_json())
                if not (item.title or item.text or item.url):
                    return self._error(400, "至少要有網址、標題或內文其中之一")
                with lock:
                    return self._json(store.add(item), 201)
            if u.path == "/api/import":
                return self._import(u)
            self._error(404, "not found")

        def do_PATCH(self):
            if not self._same_origin():
                return self._error(403, "forbidden")
            u = urlparse(self.path)
            iid = self._item_id(u.path) if u.path.startswith("/api/items/") else None
            if not iid:
                return self._error(404, "not found")
            fields = self._body_json()
            if "price" in fields:
                fields["price"] = _num(fields["price"])
            with lock:
                it = store.update(iid, **fields)
            return self._json(it) if it else self._error(404, "找不到這筆資料")

        def do_DELETE(self):
            if not self._same_origin():
                return self._error(403, "forbidden")
            u = urlparse(self.path)
            iid = self._item_id(u.path) if u.path.startswith("/api/items/") else None
            with lock:
                ok = bool(iid) and store.delete(iid)
            return self._json({"deleted": ok}, 200 if ok else 404)

        def _import(self, u):
            params = {k: v[-1] for k, v in parse_qs(u.query).items()}
            name = Path(params.get("name") or "upload.json").name
            if not name.lower().endswith((".zip", ".json")):
                return self._error(400, "只接受 .zip 或 .json")
            n = int(self.headers.get("Content-Length") or 0)
            if n <= 0 or n > MAX_UPLOAD:
                return self._error(400, "檔案大小不正確")
            tmpdir = tempfile.mkdtemp(prefix="fbsearch-")
            try:
                dest = Path(tmpdir) / name
                remaining = n
                with open(dest, "wb") as f:
                    while remaining > 0:
                        chunk = self.rfile.read(min(1024 * 1024, remaining))
                        if not chunk:
                            break
                        f.write(chunk)
                        remaining -= len(chunk)
                report, new, dup = [], 0, 0
                try:
                    with lock:
                        for fname, items in iter_export(dest):
                            a, b = store.upsert_many(items)
                            new += a
                            dup += b
                            if items:
                                report.append({"file": fname, "records": len(items)})
                except (ValueError, OSError) as e:
                    return self._error(400, f"無法解析檔案:{e}")
                return self._json({"new": new, "existing": dup, "files": report})
            finally:
                shutil.rmtree(tmpdir, ignore_errors=True)

    return Handler


def serve(db_path: str, host: str = "127.0.0.1", port: int = 8765) -> None:
    store = Store(db_path)
    httpd = ThreadingHTTPServer((host, port), make_handler(store))
    print(f"fbsearch 已啟動:http://{host}:{port}   (資料庫:{db_path},Ctrl+C 結束)")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        httpd.server_close()
        store.close()
