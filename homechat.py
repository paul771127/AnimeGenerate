"""HomeChat - 架在自己家中電腦的聊天室。只用 Python 標準函式庫,不用裝任何套件。

- 對話紀錄存在家中電腦的 SQLite 檔(預設 data/homechat.db)
- 你(主人)用密碼登入,電腦、手機瀏覽器都能開
- 每個聯絡人有一條專屬邀請連結,透過 LINE 傳給對方,對方用手機瀏覽器打開就能跟你聊,不用裝 App
- 可匯入聯絡人名單(一行一個名字,或 CSV),也可匯入 LINE 匯出的聊天紀錄 .txt,把舊對話一起搬過來

用法:
  python homechat.py                      # 第一次執行會要你設定主人密碼
  python homechat.py --port 8800 --name 小明
  python homechat.py --set-password       # 改密碼
  python homechat.py --public-url https://xxx.trycloudflare.com   # 邀請連結用的對外網址
"""
from __future__ import annotations

import argparse
import getpass
import hashlib
import hmac
import json
import os
import queue
import re
import secrets
import socket
import sqlite3
import sys
import threading
import time
from datetime import datetime
from http import HTTPStatus
from http.cookies import SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, quote, urlsplit

HERE = Path(__file__).resolve().parent
STATIC_DIR = HERE / "static"
DEFAULT_DB = HERE / "data" / "homechat.db"

COOKIE_NAME = "hc_session"
SESSION_DAYS = 365
MAX_BODY = 20 * 1024 * 1024  # LINE 聊天紀錄可能很大
MAX_MESSAGE = 5000
PAGE_SIZE = 200
PBKDF2_ROUNDS = 200_000

SCHEMA = """
CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS contacts (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    line_id TEXT NOT NULL DEFAULT '',
    note TEXT NOT NULL DEFAULT '',
    invite_token TEXT NOT NULL UNIQUE,
    created_at REAL NOT NULL,
    owner_read_id INTEGER NOT NULL DEFAULT 0,
    guest_read_id INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS messages (
    id INTEGER PRIMARY KEY,
    contact_id INTEGER NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    sender TEXT NOT NULL CHECK (sender IN ('me', 'them')),
    body TEXT NOT NULL,
    created_at REAL NOT NULL,
    source TEXT NOT NULL DEFAULT 'chat'
);
CREATE INDEX IF NOT EXISTS idx_messages_contact ON messages(contact_id, created_at, id);
CREATE TABLE IF NOT EXISTS sessions (
    token TEXT PRIMARY KEY,
    role TEXT NOT NULL CHECK (role IN ('owner', 'guest')),
    contact_id INTEGER REFERENCES contacts(id) ON DELETE CASCADE,
    created_at REAL NOT NULL
);
"""


# ---------------------------------------------------------------- 密碼

def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, PBKDF2_ROUNDS)
    return f"pbkdf2${PBKDF2_ROUNDS}${salt.hex()}${digest.hex()}"


def verify_password(password: str, stored: str) -> bool:
    try:
        _, rounds, salt, digest = stored.split("$")
        got = hashlib.pbkdf2_hmac("sha256", password.encode(), bytes.fromhex(salt), int(rounds))
        return hmac.compare_digest(got.hex(), digest)
    except (ValueError, TypeError):
        return False


# ---------------------------------------------------------------- 資料庫

class Store:
    """所有資料庫操作。SQLite 連線共用,用一把鎖保護。"""

    def __init__(self, path: Path | str):
        if str(path) != ":memory:":
            Path(path).parent.mkdir(parents=True, exist_ok=True)
        self.db = sqlite3.connect(str(path), check_same_thread=False)
        self.db.row_factory = sqlite3.Row
        self.lock = threading.Lock()
        with self.lock:
            self.db.execute("PRAGMA foreign_keys = ON")
            self.db.execute("PRAGMA journal_mode = WAL")
            self.db.executescript(SCHEMA)
            self.db.commit()

    # --- settings
    def get_setting(self, key: str, default: str = "") -> str:
        with self.lock:
            row = self.db.execute("SELECT value FROM settings WHERE key = ?", (key,)).fetchone()
        return row["value"] if row else default

    def set_setting(self, key: str, value: str) -> None:
        with self.lock:
            self.db.execute(
                "INSERT INTO settings (key, value) VALUES (?, ?) "
                "ON CONFLICT(key) DO UPDATE SET value = excluded.value",
                (key, value),
            )
            self.db.commit()

    def has_password(self) -> bool:
        return bool(self.get_setting("password_hash"))

    def set_password(self, password: str) -> None:
        self.set_setting("password_hash", hash_password(password))
        with self.lock:  # 改密碼時登出所有主人裝置
            self.db.execute("DELETE FROM sessions WHERE role = 'owner'")
            self.db.commit()

    def check_password(self, password: str) -> bool:
        stored = self.get_setting("password_hash")
        return bool(stored) and verify_password(password, stored)

    # --- sessions
    def create_session(self, role: str, contact_id: int | None = None) -> str:
        token = secrets.token_urlsafe(32)
        with self.lock:
            self.db.execute(
                "INSERT INTO sessions (token, role, contact_id, created_at) VALUES (?, ?, ?, ?)",
                (token, role, contact_id, time.time()),
            )
            self.db.commit()
        return token

    def get_session(self, token: str) -> dict | None:
        if not token:
            return None
        with self.lock:
            row = self.db.execute(
                "SELECT role, contact_id, created_at FROM sessions WHERE token = ?", (token,)
            ).fetchone()
        if not row or time.time() - row["created_at"] > SESSION_DAYS * 86400:
            return None
        return dict(row)

    def delete_session(self, token: str) -> None:
        with self.lock:
            self.db.execute("DELETE FROM sessions WHERE token = ?", (token,))
            self.db.commit()

    # --- contacts
    def _contact_row(self, row: sqlite3.Row) -> dict:
        return {
            "id": row["id"],
            "name": row["name"],
            "line_id": row["line_id"],
            "note": row["note"],
            "invite_token": row["invite_token"],
            "owner_read_id": row["owner_read_id"],
            "guest_read_id": row["guest_read_id"],
        }

    def list_contacts(self) -> list[dict]:
        with self.lock:
            rows = self.db.execute(
                """
                SELECT c.*,
                  (SELECT COUNT(*) FROM messages m WHERE m.contact_id = c.id AND m.sender = 'them'
                     AND m.source = 'chat' AND m.id > c.owner_read_id) AS unread,
                  (SELECT body FROM messages m WHERE m.contact_id = c.id
                     ORDER BY m.created_at DESC, m.id DESC LIMIT 1) AS last_body,
                  (SELECT created_at FROM messages m WHERE m.contact_id = c.id
                     ORDER BY m.created_at DESC, m.id DESC LIMIT 1) AS last_at
                FROM contacts c
                ORDER BY COALESCE(last_at, c.created_at) DESC
                """
            ).fetchall()
        out = []
        for r in rows:
            item = self._contact_row(r)
            item.update(unread=r["unread"], last_body=r["last_body"] or "", last_at=r["last_at"])
            out.append(item)
        return out

    def get_contact(self, contact_id: int) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM contacts WHERE id = ?", (contact_id,)).fetchone()
        return self._contact_row(row) if row else None

    def find_contact_by_name(self, name: str) -> dict | None:
        with self.lock:
            row = self.db.execute(
                "SELECT * FROM contacts WHERE name = ? ORDER BY id LIMIT 1", (name,)
            ).fetchone()
        return self._contact_row(row) if row else None

    def contact_by_invite(self, token: str) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM contacts WHERE invite_token = ?", (token,)).fetchone()
        return self._contact_row(row) if row else None

    def add_contact(self, name: str, line_id: str = "", note: str = "") -> dict:
        with self.lock:
            cur = self.db.execute(
                "INSERT INTO contacts (name, line_id, note, invite_token, created_at) "
                "VALUES (?, ?, ?, ?, ?)",
                (name, line_id, note, secrets.token_urlsafe(16), time.time()),
            )
            self.db.commit()
            contact_id = cur.lastrowid
        return self.get_contact(contact_id)

    def update_contact(self, contact_id: int, name: str, line_id: str, note: str) -> None:
        with self.lock:
            self.db.execute(
                "UPDATE contacts SET name = ?, line_id = ?, note = ? WHERE id = ?",
                (name, line_id, note, contact_id),
            )
            self.db.commit()

    def reset_invite(self, contact_id: int) -> None:
        """換一條新的邀請連結,舊連結和已用舊連結登入的裝置全部失效。"""
        with self.lock:
            self.db.execute(
                "UPDATE contacts SET invite_token = ? WHERE id = ?",
                (secrets.token_urlsafe(16), contact_id),
            )
            self.db.execute("DELETE FROM sessions WHERE contact_id = ?", (contact_id,))
            self.db.commit()

    def delete_contact(self, contact_id: int) -> None:
        with self.lock:
            self.db.execute("DELETE FROM contacts WHERE id = ?", (contact_id,))
            self.db.commit()

    # --- messages
    @staticmethod
    def _message_row(row: sqlite3.Row) -> dict:
        return {
            "id": row["id"],
            "contact_id": row["contact_id"],
            "sender": row["sender"],
            "body": row["body"],
            "created_at": row["created_at"],
            "source": row["source"],
        }

    def add_message(self, contact_id: int, sender: str, body: str) -> dict:
        with self.lock:
            cur = self.db.execute(
                "INSERT INTO messages (contact_id, sender, body, created_at) VALUES (?, ?, ?, ?)",
                (contact_id, sender, body, time.time()),
            )
            self.db.commit()
            row = self.db.execute("SELECT * FROM messages WHERE id = ?", (cur.lastrowid,)).fetchone()
        return self._message_row(row)

    def list_messages(self, contact_id: int, before: tuple[float, int] | None = None,
                      limit: int = PAGE_SIZE) -> tuple[list[dict], bool]:
        """回傳 (由舊到新的訊息, 是否還有更舊的)。before = (created_at, id) 游標。"""
        sql = "SELECT * FROM messages WHERE contact_id = ?"
        args: list = [contact_id]
        if before:
            sql += " AND (created_at < ? OR (created_at = ? AND id < ?))"
            args += [before[0], before[0], before[1]]
        sql += " ORDER BY created_at DESC, id DESC LIMIT ?"
        args.append(limit + 1)
        with self.lock:
            rows = self.db.execute(sql, args).fetchall()
        more = len(rows) > limit
        msgs = [self._message_row(r) for r in rows[:limit]]
        msgs.reverse()
        return msgs, more

    def all_messages(self, contact_id: int) -> list[dict]:
        with self.lock:
            rows = self.db.execute(
                "SELECT * FROM messages WHERE contact_id = ? ORDER BY created_at, id", (contact_id,)
            ).fetchall()
        return [self._message_row(r) for r in rows]

    def mark_read(self, contact_id: int, role: str, upto: int) -> None:
        col = "owner_read_id" if role == "owner" else "guest_read_id"
        with self.lock:
            self.db.execute(
                f"UPDATE contacts SET {col} = MAX({col}, ?) WHERE id = ?", (upto, contact_id)
            )
            self.db.commit()

    def import_messages(self, contact_id: int, items: list[tuple[float, str, str]]) -> int:
        """匯入舊訊息 (created_at, sender, body),重複匯入同一份檔案不會重複。"""
        added = 0
        with self.lock:
            existing = {
                (r["created_at"], r["sender"], r["body"])
                for r in self.db.execute(
                    "SELECT created_at, sender, body FROM messages WHERE contact_id = ? AND source = 'line'",
                    (contact_id,),
                )
            }
            for created_at, sender, body in items:
                key = (created_at, sender, body)
                if key in existing:
                    continue
                existing.add(key)
                self.db.execute(
                    "INSERT INTO messages (contact_id, sender, body, created_at, source) "
                    "VALUES (?, ?, ?, ?, 'line')",
                    (contact_id, sender, body, created_at),
                )
                added += 1
            self.db.commit()
        return added


# ---------------------------------------------------------------- 匯入

def parse_contact_list(text: str) -> list[dict]:
    """一行一個聯絡人:`名字` 或 `名字,LINE ID,備註`(CSV,也接受 Tab 分隔)。"""
    out = []
    seen = set()
    for raw in text.splitlines():
        line = raw.strip().lstrip("﻿")
        if not line or line.startswith("#"):
            continue
        parts = [p.strip().strip('"') for p in re.split(r"[,\t]", line)]
        name = parts[0]
        if not name or name.lower() in ("name", "名字", "姓名", "名稱") or name in seen:
            continue
        seen.add(name)
        out.append({
            "name": name[:100],
            "line_id": (parts[1] if len(parts) > 1 else "")[:100],
            "note": (",".join(parts[2:]) if len(parts) > 2 else "")[:500],
        })
    return out


_HEADER_RES = [
    re.compile(r"^\[LINE\]\s*與(.+?)的聊天(?:記錄|紀錄)"),
    re.compile(r"^\[LINE\]\s*(.+?)的聊天(?:記錄|紀錄)"),
    re.compile(r"^\[LINE\]\s*Chat history (?:with|in) (.+)$", re.I),
    re.compile(r"^\[LINE\]\s*(.+?)とのトーク履歴"),
]
_DATE_RES = [
    re.compile(r"^(\d{4})[/.\-](\d{1,2})[/.\-](\d{1,2})(?:\s|（|\(|$)"),
    re.compile(r"^\w{3},?\s+(\d{1,2})/(\d{1,2})/(\d{4})\s*$"),  # Mon, 01/15/2024
]
_MSG_RE = re.compile(
    r"^(上午|下午|午前|午後|AM|PM)?\s*(\d{1,2}):(\d{2})\s*(AM|PM)?\t([^\t]+)\t(.*)$", re.I
)


def parse_line_chat(text: str) -> dict:
    """解析 LINE「傳送聊天記錄」匯出的 .txt。

    回傳 {"contact": 標題裡的對方名字或 None, "senders": [...],
          "messages": [(timestamp, 發話者名字, 內容), ...]}
    """
    contact = None
    day: tuple[int, int, int] | None = None
    messages: list[list] = []
    in_quote = False
    for raw in text.splitlines():
        line = raw.rstrip("\r").lstrip("﻿")
        if in_quote and messages:
            # LINE 把多行訊息用 "..." 包起來
            if line.endswith('"'):
                messages[-1][2] += "\n" + line[:-1]
                in_quote = False
            else:
                messages[-1][2] += "\n" + line
            continue
        if contact is None:
            for rx in _HEADER_RES:
                m = rx.match(line.strip())
                if m:
                    contact = m.group(1).strip()
                    break
            if contact is not None:
                continue
        m = _DATE_RES[0].match(line)
        if m:
            day = (int(m.group(1)), int(m.group(2)), int(m.group(3)))
            continue
        m = _DATE_RES[1].match(line)
        if m:
            day = (int(m.group(3)), int(m.group(1)), int(m.group(2)))
            continue
        m = _MSG_RE.match(line)
        if m and day:
            ampm = (m.group(1) or m.group(4) or "").upper()
            hour, minute = int(m.group(2)), int(m.group(3))
            if ampm in ("下午", "午後", "PM") and hour < 12:
                hour += 12
            elif ampm in ("上午", "午前", "AM") and hour == 12:
                hour = 0
            try:
                ts = datetime(day[0], day[1], day[2], hour, minute).timestamp()
            except ValueError:
                continue
            body = m.group(6)
            if body.startswith('"') and not (len(body) > 1 and body.endswith('"')):
                body = body[1:]
                in_quote = True
            elif len(body) > 1 and body.startswith('"') and body.endswith('"') and "\n" not in body:
                body = body[1:-1]
            messages.append([ts, m.group(5).strip(), body])
            continue
        # 其他行(存檔日期、系統訊息...)略過
    senders: list[str] = []
    for _, sender, _ in messages:
        if sender not in senders:
            senders.append(sender)
    # 同一分鐘的多則訊息加上微小位移,保持原本順序
    last = None
    offset = 0
    for msg in messages:
        if msg[0] == last:
            offset += 1
        else:
            last, offset = msg[0], 0
        msg[0] = msg[0] + offset * 0.001
    return {"contact": contact, "senders": senders, "messages": [tuple(m) for m in messages]}


# ---------------------------------------------------------------- 即時推播 (Server-Sent Events)

class Hub:
    def __init__(self) -> None:
        self.lock = threading.Lock()
        self.subs: list[tuple[str, int | None, queue.Queue]] = []

    def subscribe(self, role: str, contact_id: int | None) -> queue.Queue:
        q: queue.Queue = queue.Queue(maxsize=1000)
        with self.lock:
            self.subs.append((role, contact_id, q))
        return q

    def unsubscribe(self, q: queue.Queue) -> None:
        with self.lock:
            self.subs = [s for s in self.subs if s[2] is not q]

    def publish(self, event: dict, contact_id: int, to_guest: bool = True) -> None:
        with self.lock:
            targets = [
                q for role, cid, q in self.subs
                if role == "owner" or (to_guest and cid == contact_id)
            ]
        for q in targets:
            try:
                q.put_nowait(event)
            except queue.Full:
                pass

    def kick_guests(self, contact_id: int) -> None:
        with self.lock:
            targets = [q for role, cid, q in self.subs if role == "guest" and cid == contact_id]
        for q in targets:
            try:
                q.put_nowait(None)
            except queue.Full:
                pass


# ---------------------------------------------------------------- HTTP

class ApiError(Exception):
    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status
        self.message = message


class LoginLimiter:
    """同一個來源 10 分鐘內最多錯 10 次。"""

    def __init__(self, limit: int = 10, window: float = 600):
        self.limit, self.window = limit, window
        self.fails: dict[str, list[float]] = {}
        self.lock = threading.Lock()

    def blocked(self, key: str) -> bool:
        now = time.time()
        with self.lock:
            recent = [t for t in self.fails.get(key, []) if now - t < self.window]
            self.fails[key] = recent
            return len(recent) >= self.limit

    def fail(self, key: str) -> None:
        with self.lock:
            self.fails.setdefault(key, []).append(time.time())


class App:
    def __init__(self, store: Store, owner_name: str | None = None, public_url: str = ""):
        self.store = store
        self.hub = Hub()
        self.limiter = LoginLimiter()
        if owner_name:
            store.set_setting("owner_name", owner_name)
        if public_url:
            store.set_setting("public_url", public_url.rstrip("/"))

    @property
    def owner_name(self) -> str:
        return self.store.get_setting("owner_name", "主人")


class Handler(BaseHTTPRequestHandler):
    server_version = "HomeChat/1.0"
    protocol_version = "HTTP/1.1"
    app: App  # 由 make_server 設定

    def log_message(self, fmt: str, *args) -> None:  # 不要把每個請求都印出來
        pass

    # --- helpers
    def _client_key(self) -> str:
        peer = self.client_address[0]
        if peer in ("127.0.0.1", "::1"):  # 經過 cloudflared 等反向代理
            return self.headers.get("CF-Connecting-IP") or self.headers.get("X-Forwarded-For", peer).split(",")[0].strip()
        return peer

    def _is_https(self) -> bool:
        return self.headers.get("X-Forwarded-Proto", "").lower() == "https"

    def _session_token(self) -> str:
        cookie = SimpleCookie()
        try:
            cookie.load(self.headers.get("Cookie", ""))
        except Exception:
            return ""
        morsel = cookie.get(COOKIE_NAME)
        return morsel.value if morsel else ""

    def _session(self) -> dict | None:
        sess = self.app.store.get_session(self._session_token())
        if sess and sess["role"] == "guest" and not self.app.store.get_contact(sess["contact_id"]):
            return None
        return sess

    def _cookie_header(self, token: str, max_age: int) -> str:
        parts = [f"{COOKIE_NAME}={token}", "Path=/", "HttpOnly", "SameSite=Lax", f"Max-Age={max_age}"]
        if self._is_https():
            parts.append("Secure")
        return "; ".join(parts)

    def _send(self, status: int, body: bytes, content_type: str,
              headers: dict[str, str] | None = None) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        for k, v in (headers or {}).items():
            self.send_header(k, v)
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(body)

    def _json(self, data, status: int = 200, headers: dict[str, str] | None = None) -> None:
        body = json.dumps(data, ensure_ascii=False).encode()
        h = {"Cache-Control": "no-store"}
        h.update(headers or {})
        self._send(status, body, "application/json; charset=utf-8", h)

    def _read_json(self) -> dict:
        ctype = self.headers.get("Content-Type", "")
        if not ctype.startswith("application/json"):
            # 要求 JSON 可以擋掉跨站表單偽造請求 (CSRF)
            raise ApiError(415, "需要 application/json")
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            raise ApiError(400, "Content-Length 錯誤")
        if length > MAX_BODY:
            raise ApiError(413, "內容太大")
        raw = self.rfile.read(length) if length else b"{}"
        try:
            data = json.loads(raw.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            raise ApiError(400, "JSON 格式錯誤")
        if not isinstance(data, dict):
            raise ApiError(400, "JSON 格式錯誤")
        return data

    def _check_origin(self) -> None:
        origin = self.headers.get("Origin")
        if origin and urlsplit(origin).netloc != self.headers.get("Host", ""):
            raise ApiError(403, "來源不符")

    def _require(self, role: str | None = None) -> dict:
        sess = self._session()
        if not sess:
            raise ApiError(401, "請先登入")
        if role and sess["role"] != role:
            raise ApiError(403, "沒有權限")
        return sess

    def _base_url(self) -> str:
        public = self.app.store.get_setting("public_url")
        if public:
            return public
        scheme = "https" if self._is_https() else "http"
        return f"{scheme}://{self.headers.get('Host', 'localhost')}"

    def _contact_for(self, sess: dict, contact_id) -> dict:
        """主人可以指定聯絡人;訪客只能存取自己。"""
        if sess["role"] == "guest":
            cid = sess["contact_id"]
        else:
            try:
                cid = int(contact_id)
            except (TypeError, ValueError):
                raise ApiError(400, "缺少聯絡人")
        contact = self.app.store.get_contact(cid)
        if not contact:
            raise ApiError(404, "找不到聯絡人")
        return contact

    def _public_contact(self, c: dict) -> dict:
        out = {k: v for k, v in c.items() if k != "invite_token"}
        out["invite_url"] = f"{self._base_url()}/c/{c['invite_token']}"
        return out

    # --- dispatch
    def do_HEAD(self) -> None:
        self.do_GET()

    def do_GET(self) -> None:
        url = urlsplit(self.path)
        qs = {k: v[-1] for k, v in parse_qs(url.query).items()}
        try:
            if url.path in ("/", "/index.html"):
                return self._static("index.html")
            if url.path in ("/manifest.webmanifest", "/icon.svg"):
                return self._static(url.path.lstrip("/"))
            if url.path.startswith("/c/"):
                return self._join(url.path[3:])
            if url.path == "/api/me":
                return self._me()
            if url.path == "/api/contacts":
                self._require("owner")
                return self._json({"contacts": [self._public_contact(c) for c in self.app.store.list_contacts()]})
            if url.path == "/api/messages":
                return self._get_messages(qs)
            if url.path == "/api/events":
                return self._events()
            if url.path == "/api/export":
                return self._export(qs)
            raise ApiError(404, "找不到")
        except ApiError as e:
            self._json({"error": e.message}, e.status)

    def do_POST(self) -> None:
        url = urlsplit(self.path)
        try:
            self._check_origin()
            data = self._read_json()
            routes = {
                "/api/login": self._login,
                "/api/logout": self._logout,
                "/api/messages": self._post_message,
                "/api/read": self._read,
                "/api/contacts": self._add_contact,
                "/api/contacts/import": self._import_contacts,
                "/api/contacts/import-line": self._import_line,
                "/api/settings": self._settings,
            }
            if url.path in routes:
                return routes[url.path](data)
            m = re.fullmatch(r"/api/contacts/(\d+)/(update|reset-invite|delete)", url.path)
            if m:
                return self._contact_action(int(m.group(1)), m.group(2), data)
            raise ApiError(404, "找不到")
        except ApiError as e:
            self._json({"error": e.message}, e.status)

    # --- pages
    def _static(self, name: str) -> None:
        path = STATIC_DIR / name
        types = {".html": "text/html; charset=utf-8", ".svg": "image/svg+xml",
                 ".webmanifest": "application/manifest+json"}
        self._send(200, path.read_bytes(), types[path.suffix], {
            "Cache-Control": "no-cache",
            "Content-Security-Policy": "default-src 'self'; style-src 'self' 'unsafe-inline'; "
                                       "script-src 'self' 'unsafe-inline'; img-src 'self' data:",
        })

    def _join(self, token: str) -> None:
        contact = self.app.store.contact_by_invite(token)
        if not contact:
            body = "<!doctype html><meta charset=utf-8><meta name=viewport content='width=device-width'>" \
                   "<p style='font:16px sans-serif;padding:24px'>這條邀請連結已失效,請向對方要新的連結。</p>"
            return self._send(404, body.encode(), "text/html; charset=utf-8")
        current = self._session()
        headers = {"Location": "/", "Cache-Control": "no-store"}
        # 主人自己點開邀請連結時不要被登出
        if not current or current["role"] != "owner":
            if not (current and current["role"] == "guest" and current["contact_id"] == contact["id"]):
                token = self.app.store.create_session("guest", contact["id"])
                headers["Set-Cookie"] = self._cookie_header(token, SESSION_DAYS * 86400)
        self._send(303, b"", "text/plain", headers)

    # --- api
    def _me(self) -> None:
        sess = self._session()
        if not sess:
            return self._json({"role": None, "owner_name": self.app.owner_name})
        if sess["role"] == "owner":
            return self._json({
                "role": "owner",
                "owner_name": self.app.owner_name,
                "public_url": self.app.store.get_setting("public_url"),
            })
        contact = self.app.store.get_contact(sess["contact_id"])
        return self._json({
            "role": "guest",
            "owner_name": self.app.owner_name,
            "contact": {"id": contact["id"], "name": contact["name"], "owner_read_id": contact["owner_read_id"]},
        })

    def _login(self, data: dict) -> None:
        key = self._client_key()
        if self.app.limiter.blocked(key):
            raise ApiError(429, "密碼錯誤太多次,請 10 分鐘後再試")
        if not self.app.store.check_password(str(data.get("password", ""))):
            self.app.limiter.fail(key)
            time.sleep(1)
            raise ApiError(401, "密碼錯誤")
        token = self.app.store.create_session("owner")
        self._json({"ok": True}, headers={"Set-Cookie": self._cookie_header(token, SESSION_DAYS * 86400)})

    def _logout(self, data: dict) -> None:
        self.app.store.delete_session(self._session_token())
        self._json({"ok": True}, headers={"Set-Cookie": self._cookie_header("", 0)})

    def _get_messages(self, qs: dict) -> None:
        sess = self._require()
        contact = self._contact_for(sess, qs.get("contact"))
        before = None
        if "before_at" in qs and "before_id" in qs:
            try:
                before = (float(qs["before_at"]), int(qs["before_id"]))
            except ValueError:
                raise ApiError(400, "游標錯誤")
        msgs, more = self.app.store.list_messages(contact["id"], before)
        self._json({
            "messages": msgs,
            "more": more,
            "owner_read_id": contact["owner_read_id"],
            "guest_read_id": contact["guest_read_id"],
        })

    def _post_message(self, data: dict) -> None:
        sess = self._require()
        contact = self._contact_for(sess, data.get("contact_id"))
        body = str(data.get("body", "")).strip()
        if not body:
            raise ApiError(400, "訊息是空的")
        if len(body) > MAX_MESSAGE:
            raise ApiError(400, f"訊息太長(上限 {MAX_MESSAGE} 字)")
        sender = "me" if sess["role"] == "owner" else "them"
        msg = self.app.store.add_message(contact["id"], sender, body)
        # 自己送出的就算已讀
        self.app.store.mark_read(contact["id"], sess["role"], msg["id"])
        self.app.hub.publish({"type": "message", "message": msg}, contact["id"])
        self._json({"message": msg})

    def _read(self, data: dict) -> None:
        sess = self._require()
        contact = self._contact_for(sess, data.get("contact_id"))
        try:
            upto = int(data.get("upto", 0))
        except (TypeError, ValueError):
            raise ApiError(400, "upto 錯誤")
        self.app.store.mark_read(contact["id"], sess["role"], upto)
        fresh = self.app.store.get_contact(contact["id"])
        self.app.hub.publish({
            "type": "read", "contact_id": contact["id"],
            "owner_read_id": fresh["owner_read_id"], "guest_read_id": fresh["guest_read_id"],
        }, contact["id"])
        self._json({"ok": True})

    def _events(self) -> None:
        sess = self._require()
        q = self.app.hub.subscribe(sess["role"], sess.get("contact_id"))
        try:
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream; charset=utf-8")
            self.send_header("Cache-Control", "no-cache, no-transform")
            self.send_header("X-Accel-Buffering", "no")
            self.send_header("Connection", "close")
            self.end_headers()
            self.close_connection = True
            self.wfile.write(b"retry: 3000\n: hello\n\n")
            self.wfile.flush()
            while True:
                try:
                    event = q.get(timeout=20)
                except queue.Empty:
                    self.wfile.write(b": ping\n\n")
                    self.wfile.flush()
                    continue
                if event is None:  # 連結被重設,把訪客踢掉
                    self.wfile.write(b"event: kicked\ndata: {}\n\n")
                    self.wfile.flush()
                    return
                payload = json.dumps(event, ensure_ascii=False)
                self.wfile.write(f"data: {payload}\n\n".encode())
                self.wfile.flush()
        except (BrokenPipeError, ConnectionResetError, OSError):
            pass
        finally:
            self.app.hub.unsubscribe(q)

    def _export(self, qs: dict) -> None:
        sess = self._require("owner")
        contact = self._contact_for(sess, qs.get("contact"))
        lines = [f"[HomeChat] 與{contact['name']}的聊天記錄", ""]
        day = None
        for m in self.app.store.all_messages(contact["id"]):
            dt = datetime.fromtimestamp(m["created_at"])
            if dt.date() != day:
                day = dt.date()
                lines += ["", dt.strftime("%Y/%m/%d")]
            who = self.app.owner_name if m["sender"] == "me" else contact["name"]
            body = m["body"]
            if "\n" in body:
                body = f'"{body}"'
            lines.append(f"{dt:%H:%M}\t{who}\t{body}")
        filename = quote(f"HomeChat_{contact['name']}.txt")
        self._send(200, "\n".join(lines).encode("utf-8"), "text/plain; charset=utf-8", {
            "Content-Disposition": f"attachment; filename*=UTF-8''{filename}",
            "Cache-Control": "no-store",
        })

    @staticmethod
    def _clean(value, limit: int) -> str:
        return str(value or "").strip()[:limit]

    def _add_contact(self, data: dict) -> None:
        self._require("owner")
        name = self._clean(data.get("name"), 100)
        if not name:
            raise ApiError(400, "請輸入名字")
        c = self.app.store.add_contact(name, self._clean(data.get("line_id"), 100),
                                       self._clean(data.get("note"), 500))
        self.app.hub.publish({"type": "contacts"}, c["id"], to_guest=False)
        self._json({"contact": self._public_contact(c)})

    def _import_contacts(self, data: dict) -> None:
        self._require("owner")
        added, skipped = [], []
        for item in parse_contact_list(str(data.get("text", ""))):
            if self.app.store.find_contact_by_name(item["name"]):
                skipped.append(item["name"])
                continue
            added.append(self.app.store.add_contact(item["name"], item["line_id"], item["note"])["name"])
        if added:
            self.app.hub.publish({"type": "contacts"}, 0, to_guest=False)
        self._json({"added": added, "skipped": skipped})

    def _import_line(self, data: dict) -> None:
        self._require("owner")
        parsed = parse_line_chat(str(data.get("text", "")))
        if not parsed["messages"]:
            raise ApiError(400, "看不懂這個檔案。請用 LINE 聊天室的「傳送聊天記錄」匯出的 .txt 檔")
        senders = parsed["senders"]
        contact_name = self._clean(data.get("contact_name"), 100)
        if contact_name:
            if contact_name not in senders:
                raise ApiError(400, "檔案裡沒有這個人說的話")
        elif parsed["contact"] and (parsed["contact"] in senders or len(senders) == 1):
            contact_name = parsed["contact"]  # 標題的名字(對方可能一句話都沒說)
        else:
            # 標題沒有對方名字、對方改過名字、或是群組:請主人選誰是對方
            return self._json({"need_choice": True, "senders": senders})
        contact = self.app.store.find_contact_by_name(contact_name)
        if not contact:
            contact = self.app.store.add_contact(contact_name)
        items = [
            (ts, "them" if sender == contact_name else "me", body)
            for ts, sender, body in parsed["messages"]
        ]
        added = self.app.store.import_messages(contact["id"], items)
        self.app.hub.publish({"type": "contacts"}, contact["id"], to_guest=False)
        self._json({"contact": self._public_contact(contact), "imported": added, "total": len(items)})

    def _contact_action(self, contact_id: int, action: str, data: dict) -> None:
        sess = self._require("owner")
        contact = self._contact_for(sess, contact_id)
        if action == "update":
            name = self._clean(data.get("name"), 100) or contact["name"]
            self.app.store.update_contact(contact_id, name, self._clean(data.get("line_id"), 100),
                                          self._clean(data.get("note"), 500))
        elif action == "reset-invite":
            self.app.store.reset_invite(contact_id)
            self.app.hub.kick_guests(contact_id)
        elif action == "delete":
            self.app.hub.kick_guests(contact_id)
            self.app.store.delete_contact(contact_id)
            self.app.hub.publish({"type": "contacts"}, contact_id, to_guest=False)
            return self._json({"ok": True})
        self.app.hub.publish({"type": "contacts"}, contact_id, to_guest=False)
        self._json({"contact": self._public_contact(self.app.store.get_contact(contact_id))})

    def _settings(self, data: dict) -> None:
        self._require("owner")
        if "owner_name" in data:
            name = self._clean(data["owner_name"], 50)
            if name:
                self.app.store.set_setting("owner_name", name)
        if "public_url" in data:
            url = self._clean(data["public_url"], 300).rstrip("/")
            if url and not re.match(r"^https?://", url):
                raise ApiError(400, "對外網址要以 http:// 或 https:// 開頭")
            self.app.store.set_setting("public_url", url)
        self._json({"ok": True})


def make_server(app: App, host: str, port: int) -> ThreadingHTTPServer:
    handler = type("BoundHandler", (Handler,), {"app": app})
    server = ThreadingHTTPServer((host, port), handler)
    server.daemon_threads = True
    return server


def lan_addresses() -> list[str]:
    ips = []
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.connect(("10.255.255.255", 1))  # 不會真的送封包,只是查路由
            ips.append(s.getsockname()[0])
    except OSError:
        pass
    return [ip for ip in ips if not ip.startswith("127.")]


def ask_password() -> str:
    while True:
        pw = getpass.getpass("設定主人密碼(至少 6 個字): ")
        if len(pw) < 6:
            print("太短了,再試一次。")
            continue
        if getpass.getpass("再輸入一次: ") != pw:
            print("兩次不一樣,再試一次。")
            continue
        return pw


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description="HomeChat - 架在家中電腦的聊天室")
    p.add_argument("--host", default="0.0.0.0", help="監聽位址(預設 0.0.0.0,同 Wi-Fi 的手機也能連)")
    p.add_argument("--port", type=int, default=8800)
    p.add_argument("--db", default=str(DEFAULT_DB), help="資料庫檔案位置")
    p.add_argument("--name", help="你的顯示名稱(朋友會看到)")
    p.add_argument("--public-url", default="", help="對外網址,例如 Cloudflare Tunnel 給的 https 網址")
    p.add_argument("--set-password", action="store_true", help="設定 / 更改主人密碼後結束")
    args = p.parse_args(argv)

    store = Store(args.db)
    env_pw = os.environ.get("HOMECHAT_PASSWORD")
    if args.set_password:
        store.set_password(env_pw or ask_password())
        print("密碼已更新,所有主人裝置需要重新登入。")
        return 0
    if not store.has_password():
        if env_pw:
            store.set_password(env_pw)
        elif sys.stdin.isatty():
            print("第一次使用,先設定主人密碼。")
            store.set_password(ask_password())
        else:
            print("還沒設定密碼:請先執行 python homechat.py --set-password", file=sys.stderr)
            return 1

    app = App(store, owner_name=args.name, public_url=args.public_url)
    server = make_server(app, args.host, args.port)
    print(f"HomeChat 已啟動,資料存在 {Path(args.db).resolve()}")
    print(f"  這台電腦:   http://127.0.0.1:{args.port}")
    for ip in lan_addresses():
        print(f"  同 Wi-Fi 手機: http://{ip}:{args.port}")
    print("  在外面 / 給朋友連:見 README 的「讓外面連進來」")
    print("按 Ctrl+C 結束")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n已關閉")
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
