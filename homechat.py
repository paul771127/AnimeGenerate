"""HomeChat - 架在自己家中電腦的聊天室。只用 Python 標準函式庫,不用裝任何套件。

- 對話紀錄存在家中電腦的 SQLite 檔(預設 data/homechat.db)
- 你(主人)用密碼登入,電腦、手機瀏覽器都能開
- 每個聯絡人有一條專屬邀請連結,透過 LINE 傳給對方,對方用手機瀏覽器打開就能跟你聊,不用裝 App
- 可匯入聯絡人名單(一行一個名字,或 CSV),也可匯入 LINE 匯出的聊天紀錄 .txt,把舊對話一起搬過來
- 啟動時自動用 Tailscale Funnel(或 Cloudflare 通道)開一個 https 網址,在外面用 4G / 別的 Wi-Fi 也能連

用法:
  python homechat.py --open               # 第一次執行會打開瀏覽器設定主人密碼
  python homechat.py --set-password       # 忘記密碼時在這裡重設
  python homechat.py --tunnel off         # 只在家裡 Wi-Fi 用,不開外部連線
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
import shutil
import signal
import socket
import sqlite3
import subprocess
import sys
import threading
import time
import traceback
import urllib.error
import urllib.request
import webbrowser
from datetime import datetime
from http.cookies import SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, quote, urlsplit

HERE = Path(__file__).resolve().parent
STATIC_DIR = HERE / "static"
DEFAULT_DB = HERE / "data" / "homechat.db"

VERSION = "2026.10.10"  # 改了資料庫格式或 API 就更新,用來認出還在執行的舊版
COOKIE_NAME = "hc_session"
SESSION_IDLE_DAYS = 365  # 像 LINE 一樣一直保持登入;一年沒用才自動登出(每次使用都會重新計算)
INVITE_DAYS = 7  # 邀請連結 7 天內有效,只能用一次
MIN_PASSWORD = 8  # 主人
MIN_GUEST_PASSWORD = 6  # 朋友
USERNAME_RE = re.compile(r"^[A-Za-z0-9._-]{3,30}$")
MAX_PENDING = 20  # 最多同時 20 個待確認的好友申請
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
    status TEXT NOT NULL DEFAULT 'active',       -- active / pending(好友申請等待確認)
    username TEXT,                               -- 朋友自己設定的登入帳號(小寫)
    password_hash TEXT NOT NULL DEFAULT '',
    request_msg TEXT NOT NULL DEFAULT '',
    invite_token TEXT NOT NULL UNIQUE,
    invite_expires REAL NOT NULL DEFAULT 0,      -- 0 = 沒有可用的邀請連結
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
    source TEXT NOT NULL DEFAULT 'chat',
    client_id TEXT                               -- 裝置產生的編號,離線排隊重送時不會重複
);
CREATE INDEX IF NOT EXISTS idx_messages_contact ON messages(contact_id, created_at, id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_messages_client ON messages(contact_id, client_id) WHERE client_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_contacts_username ON contacts(username) WHERE username IS NOT NULL;
CREATE TABLE IF NOT EXISTS sessions (
    id TEXT PRIMARY KEY,                         -- 登入 token 的 SHA-256,不存明碼
    role TEXT NOT NULL CHECK (role IN ('owner', 'guest')),
    contact_id INTEGER REFERENCES contacts(id) ON DELETE CASCADE,
    device TEXT NOT NULL DEFAULT '',
    created_at REAL NOT NULL,
    last_seen REAL NOT NULL
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


DUMMY_HASH = hash_password(secrets.token_hex(8))


# ---------------------------------------------------------------- 資料庫

def token_hash(token: str) -> str:
    """登入憑證只存雜湊:就算資料庫檔案外流,也不能拿來冒充登入。"""
    return hashlib.sha256(token.encode()).hexdigest()


def describe_device(user_agent: str) -> str:
    ua = user_agent or ""
    if "iPhone" in ua:
        os_name = "iPhone"
    elif "iPad" in ua:
        os_name = "iPad"
    elif "Android" in ua:
        os_name = "Android"
    elif "Windows" in ua:
        os_name = "Windows"
    elif "Macintosh" in ua or "Mac OS" in ua:
        os_name = "Mac"
    elif "Linux" in ua:
        os_name = "Linux"
    else:
        os_name = "裝置"
    for key, name in ((" Line/", "LINE"), ("Edg/", "Edge"), ("Firefox/", "Firefox"),
                      ("CriOS/", "Chrome"), ("Chrome/", "Chrome"), ("Safari/", "Safari")):
        if key in ua:
            return f"{os_name} · {name}"
    return os_name


class Store:
    """所有資料庫操作。SQLite 連線共用,用一把鎖保護。"""

    def __init__(self, path: Path | str):
        if str(path) != ":memory:":
            path = Path(path)
            path.parent.mkdir(parents=True, exist_ok=True)
            if os.name == "posix":  # 只有自己能讀
                os.chmod(path.parent, 0o700)
        self.db = sqlite3.connect(str(path), check_same_thread=False)
        self.db.row_factory = sqlite3.Row
        self.lock = threading.Lock()
        with self.lock:
            self.db.execute("PRAGMA foreign_keys = ON")
            self.db.execute("PRAGMA journal_mode = WAL")
            self._migrate()
            self.db.executescript(SCHEMA)
            self.db.commit()
        if str(path) != ":memory:" and os.name == "posix":
            for p in (path, Path(f"{path}-wal"), Path(f"{path}-shm")):
                if p.exists():
                    os.chmod(p, 0o600)

    def _migrate(self) -> None:
        """舊版資料庫升級。"""
        cols = {r["name"] for r in self.db.execute("PRAGMA table_info(sessions)")}
        if "token" in cols:  # 舊版存的是明碼 token:全部作廢,重新登入
            self.db.execute("DROP TABLE sessions")
        cols = {r["name"] for r in self.db.execute("PRAGMA table_info(contacts)")}
        if cols:
            for name, ddl in (("status", "TEXT NOT NULL DEFAULT 'active'"),
                              ("request_msg", "TEXT NOT NULL DEFAULT ''"),
                              ("invite_expires", "REAL NOT NULL DEFAULT 0"),
                              ("username", "TEXT"),
                              ("password_hash", "TEXT NOT NULL DEFAULT ''")):
                if name not in cols:
                    self.db.execute(f"ALTER TABLE contacts ADD COLUMN {name} {ddl}")
        cols = {r["name"] for r in self.db.execute("PRAGMA table_info(messages)")}
        if cols and "client_id" not in cols:
            self.db.execute("ALTER TABLE messages ADD COLUMN client_id TEXT")

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

    # --- sessions(每個登入的瀏覽器 = 一台裝置)
    def create_session(self, role: str, contact_id: int | None = None, device: str = "") -> str:
        token = secrets.token_urlsafe(32)
        now = time.time()
        with self.lock:
            self.db.execute(
                "INSERT INTO sessions (id, role, contact_id, device, created_at, last_seen) "
                "VALUES (?, ?, ?, ?, ?, ?)",
                (token_hash(token), role, contact_id, device[:60], now, now),
            )
            self.db.commit()
        return token

    def get_session(self, token: str) -> dict | None:
        if not token:
            return None
        sid = token_hash(token)
        with self.lock:
            row = self.db.execute("SELECT * FROM sessions WHERE id = ?", (sid,)).fetchone()
            if not row:
                return None
            now = time.time()
            if now - row["last_seen"] > SESSION_IDLE_DAYS * 86400:  # 太久沒用自動登出
                self.db.execute("DELETE FROM sessions WHERE id = ?", (sid,))
                self.db.commit()
                return None
            if now - row["last_seen"] > 60:
                self.db.execute("UPDATE sessions SET last_seen = ? WHERE id = ?", (now, sid))
                self.db.commit()
        return dict(row)

    def delete_session(self, sid: str) -> None:
        with self.lock:
            self.db.execute("DELETE FROM sessions WHERE id = ?", (sid,))
            self.db.commit()

    def delete_sessions(self, role: str, contact_id: int | None = None, keep: str = "") -> None:
        with self.lock:
            self.db.execute(
                "DELETE FROM sessions WHERE role = ? AND contact_id IS ? AND id != ?",
                (role, contact_id, keep),
            )
            self.db.commit()

    def list_sessions(self, role: str, contact_id: int | None = None) -> list[dict]:
        with self.lock:
            rows = self.db.execute(
                "SELECT id, device, created_at, last_seen FROM sessions "
                "WHERE role = ? AND contact_id IS ? ORDER BY last_seen DESC",
                (role, contact_id),
            ).fetchall()
        return [dict(r) for r in rows]

    # --- contacts
    @staticmethod
    def _contact_row(row: sqlite3.Row) -> dict:
        return {
            "id": row["id"],
            "name": row["name"],
            "line_id": row["line_id"],
            "note": row["note"],
            "status": row["status"],
            "request_msg": row["request_msg"],
            "username": row["username"] or "",
            "has_password": bool(row["password_hash"]),
            "invite_token": row["invite_token"],
            "invite_expires": row["invite_expires"],
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
                     ORDER BY m.created_at DESC, m.id DESC LIMIT 1) AS last_at,
                  (SELECT COUNT(*) FROM sessions s WHERE s.contact_id = c.id) AS devices,
                  (SELECT MAX(last_seen) FROM sessions s WHERE s.contact_id = c.id) AS last_seen
                FROM contacts c
                ORDER BY c.status = 'pending' DESC, COALESCE(last_at, c.created_at) DESC
                """
            ).fetchall()
        out = []
        for r in rows:
            item = self._contact_row(r)
            item.update(unread=r["unread"], last_body=r["last_body"] or "", last_at=r["last_at"],
                        devices=r["devices"], last_seen=r["last_seen"])
            out.append(item)
        return out

    def get_contact(self, contact_id: int) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM contacts WHERE id = ?", (contact_id,)).fetchone()
        return self._contact_row(row) if row else None

    def find_contact_by_name(self, name: str) -> dict | None:
        with self.lock:
            row = self.db.execute(
                "SELECT * FROM contacts WHERE name = ? AND status = 'active' ORDER BY id LIMIT 1", (name,)
            ).fetchone()
        return self._contact_row(row) if row else None

    def add_contact(self, name: str, line_id: str = "", note: str = "",
                    status: str = "active", request_msg: str = "") -> dict:
        with self.lock:
            cur = self.db.execute(
                "INSERT INTO contacts (name, line_id, note, status, request_msg, invite_token, "
                "invite_expires, created_at) VALUES (?, ?, ?, ?, ?, ?, 0, ?)",
                (name, line_id, note, status, request_msg, secrets.token_urlsafe(24), time.time()),
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

    def approve_contact(self, contact_id: int) -> None:
        with self.lock:
            self.db.execute("UPDATE contacts SET status = 'active' WHERE id = ?", (contact_id,))
            self.db.commit()

    def count_pending(self) -> int:
        with self.lock:
            return self.db.execute("SELECT COUNT(*) FROM contacts WHERE status = 'pending'").fetchone()[0]

    def new_invite(self, contact_id: int, days: float = INVITE_DAYS) -> None:
        """產生新的邀請連結:只能用一次、有期限。舊的未使用連結同時失效。"""
        with self.lock:
            self.db.execute(
                "UPDATE contacts SET invite_token = ?, invite_expires = ? WHERE id = ?",
                (secrets.token_urlsafe(24), time.time() + days * 86400, contact_id),
            )
            self.db.commit()

    def cancel_invite(self, contact_id: int) -> None:
        with self.lock:
            self.db.execute(
                "UPDATE contacts SET invite_token = ?, invite_expires = 0 WHERE id = ?",
                (secrets.token_urlsafe(24), contact_id),
            )
            self.db.commit()

    def invite_contact(self, token: str) -> dict | None:
        """查邀請連結(不使用掉)。"""
        if not token:
            return None
        with self.lock:
            row = self.db.execute(
                "SELECT * FROM contacts WHERE invite_token = ? AND invite_expires > ? AND status = 'active'",
                (token, time.time()),
            ).fetchone()
        return self._contact_row(row) if row else None

    def claim_invite(self, token: str, device: str, username: str | None = None,
                     password_hash: str = "") -> tuple[dict, str] | None:
        """使用邀請連結:連結立刻作廢,同時設定帳號密碼。回傳 (聯絡人, 登入 token)。"""
        if not token:
            return None
        with self.lock:
            row = self.db.execute(
                "SELECT * FROM contacts WHERE invite_token = ? AND invite_expires > ? AND status = 'active'",
                (token, time.time()),
            ).fetchone()
            if not row:
                return None
            self.db.execute(
                "UPDATE contacts SET invite_token = ?, invite_expires = 0, "
                "username = COALESCE(?, username), password_hash = ? WHERE id = ?",
                (secrets.token_urlsafe(24), username, password_hash or row["password_hash"], row["id"]),
            )
            self.db.commit()
        return self._contact_row(row), self.create_session("guest", row["id"], device)

    # --- 帳號
    def owner_username(self) -> str:
        return self.get_setting("owner_username")

    def username_taken(self, username: str, except_contact: int | None = None) -> bool:
        username = username.lower()
        if username == self.owner_username():
            return True
        with self.lock:
            row = self.db.execute(
                "SELECT id FROM contacts WHERE username = ? AND id IS NOT ?", (username, except_contact)
            ).fetchone()
        return row is not None

    def set_guest_account(self, contact_id: int, username: str | None, password: str) -> None:
        with self.lock:
            if username is None:
                self.db.execute("UPDATE contacts SET password_hash = ? WHERE id = ?",
                                (hash_password(password), contact_id))
            else:
                self.db.execute("UPDATE contacts SET username = ?, password_hash = ? WHERE id = ?",
                                (username.lower(), hash_password(password), contact_id))
            self.db.commit()

    def find_guest_login(self, username: str, password: str) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM contacts WHERE username = ?", (username.lower(),)).fetchone()
        if row and row["password_hash"] and verify_password(password, row["password_hash"]):
            return self._contact_row(row)
        if not row:  # 不存在的帳號也花一樣的時間,不讓人用時間猜帳號
            verify_password(password, DUMMY_HASH)
        return None

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
            "client_id": row["client_id"],
        }

    def add_message(self, contact_id: int, sender: str, body: str,
                    client_id: str | None = None) -> tuple[dict, bool]:
        """回傳 (訊息, 是不是新的)。同一個 client_id 重送不會重複新增。"""
        with self.lock:
            if client_id:
                row = self.db.execute(
                    "SELECT * FROM messages WHERE contact_id = ? AND client_id = ?", (contact_id, client_id)
                ).fetchone()
                if row:
                    return self._message_row(row), False
            cur = self.db.execute(
                "INSERT INTO messages (contact_id, sender, body, created_at, client_id) VALUES (?, ?, ?, ?, ?)",
                (contact_id, sender, body, time.time(), client_id),
            )
            self.db.commit()
            row = self.db.execute("SELECT * FROM messages WHERE id = ?", (cur.lastrowid,)).fetchone()
        return self._message_row(row), True

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
        self.subs: list[tuple[str, int | None, str, queue.Queue]] = []

    def subscribe(self, role: str, contact_id: int | None, sid: str = "") -> queue.Queue:
        q: queue.Queue = queue.Queue(maxsize=1000)
        with self.lock:
            self.subs.append((role, contact_id, sid, q))
        return q

    def unsubscribe(self, q: queue.Queue) -> None:
        with self.lock:
            self.subs = [s for s in self.subs if s[3] is not q]

    def publish(self, event: dict, contact_id: int, to_guest: bool = True) -> None:
        with self.lock:
            targets = [
                q for role, cid, _, q in self.subs
                if role == "owner" or (to_guest and cid == contact_id)
            ]
        for q in targets:
            try:
                q.put_nowait(event)
            except queue.Full:
                pass

    def kick_guests(self, contact_id: int) -> None:
        with self.lock:
            targets = [q for role, cid, _, q in self.subs if role == "guest" and cid == contact_id]
        self._kick(targets)

    def kick_guests_except(self, contact_id: int, keep: str) -> None:
        with self.lock:
            targets = [q for role, cid, s, q in self.subs if role == "guest" and cid == contact_id and s != keep]
        self._kick(targets)

    def kick(self, sid: str) -> None:
        with self.lock:
            targets = [q for _, _, s, q in self.subs if s == sid]
        self._kick(targets)

    @staticmethod
    def _kick(targets: list[queue.Queue]) -> None:
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


class RateLimiter:
    """滑動視窗計數:同一個 key 在 window 秒內最多 limit 次。"""

    def __init__(self, limit: int, window: float):
        self.limit, self.window = limit, window
        self.hits: dict[str, list[float]] = {}
        self.lock = threading.Lock()

    def blocked(self, key: str) -> bool:
        now = time.time()
        with self.lock:
            recent = [t for t in self.hits.get(key, []) if now - t < self.window]
            if recent:
                self.hits[key] = recent
            else:
                self.hits.pop(key, None)
            return len(recent) >= self.limit

    def add(self, key: str) -> None:
        with self.lock:
            self.hits.setdefault(key, []).append(time.time())

    def take(self, key: str) -> bool:
        """沒超過就記一次並回傳 True。"""
        if self.blocked(key):
            return False
        self.add(key)
        return True


class App:
    def __init__(self, store: Store, owner_name: str | None = None):
        self.store = store
        self.hub = Hub()
        # 密碼:同一來源 10 分鐘錯 10 次鎖住;全部來源加起來錯 30 次也鎖(防止偽造來源繞過)
        self.login_fails = RateLimiter(10, 600)
        self.login_fails_all = RateLimiter(30, 600)
        # 好友申請:同一來源每小時 5 次,全部每小時 30 次
        self.requests = RateLimiter(5, 3600)
        self.requests_all = RateLimiter(30, 3600)
        self.messages = RateLimiter(30, 60)  # 每台裝置每分鐘 30 則
        if owner_name:
            store.set_setting("owner_name", owner_name)

    @property
    def owner_name(self) -> str:
        return self.store.get_setting("owner_name", "主人")

    def friend_link_token(self) -> str:
        token = self.store.get_setting("friend_token")
        if not token:
            token = secrets.token_urlsafe(18)
            self.store.set_setting("friend_token", token)
        return token


HTML_CSP = ("default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; "
            "img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'")


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    app: App  # 由 make_server 設定

    def log_message(self, fmt: str, *args) -> None:  # 不要把每個請求都印出來
        pass

    def version_string(self) -> str:  # 不透露 Python 版本
        return "HomeChat"

    # --- helpers
    def _from_proxy(self) -> bool:
        """經過 Tailscale / Cloudflare 通道進來的請求,在本機看起來是 127.0.0.1。"""
        return any(self.headers.get(h) for h in ("X-Forwarded-For", "CF-Connecting-IP", "Tailscale-User-Login",
                                                 "Tailscale-Funnel-Request", "X-Forwarded-Host",
                                                 "X-Forwarded-Proto", "Forwarded"))

    def _is_local(self) -> bool:
        """真的是坐在這台電腦前面(不是經過通道)。

        Host 也必須是 localhost,防止 DNS rebinding:惡意網站把自己的網域指到 127.0.0.1,
        讓這台電腦的瀏覽器替它呼叫 API。
        """
        host = urlsplit("//" + self.headers.get("Host", "")).hostname or ""
        return (self.client_address[0] in ("127.0.0.1", "::1") and not self._from_proxy()
                and host in ("127.0.0.1", "localhost", "::1"))

    def _client_key(self) -> str:
        peer = self.client_address[0]
        if peer in ("127.0.0.1", "::1"):
            if self.app.store.get_setting("public_url_auto") == "cloudflare" and self.headers.get("CF-Connecting-IP"):
                return self.headers["CF-Connecting-IP"]  # Cloudflare 會覆寫,不能偽造
            fwd = self.headers.get("X-Forwarded-For", "")
            if fwd:
                return fwd.split(",")[-1].strip()  # 最後一個是通道自己加的
        return peer

    def _is_https(self) -> bool:
        if self.headers.get("X-Forwarded-Proto", "").lower() == "https":
            return True
        public = self.app.store.get_setting("public_url")
        return public.startswith("https://") and urlsplit(public).netloc == self.headers.get("Host", "")

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
        if sess and sess["role"] == "guest":
            contact = self.app.store.get_contact(sess["contact_id"])
            if not contact:
                return None
            sess["status"] = contact["status"]
        return sess

    def _cookie_header(self, token: str, max_age: int) -> str:
        parts = [f"{COOKIE_NAME}={token}", "Path=/", "HttpOnly", "SameSite=Lax", f"Max-Age={max_age}"]
        if self._is_https():
            parts.append("Secure")
        return "; ".join(parts)

    def _login_cookie(self, token: str) -> dict[str, str]:
        return {"Set-Cookie": self._cookie_header(token, SESSION_IDLE_DAYS * 86400)}

    def _send(self, status: int, body: bytes, content_type: str,
              headers: dict[str, str] | None = None) -> None:
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Referrer-Policy", "no-referrer")  # 邀請連結不會從 Referer 外流
        self.send_header("Permissions-Policy", "camera=(), microphone=(), geolocation=()")
        self.send_header("X-HomeChat-Version", VERSION)
        if self._is_https():
            self.send_header("Strict-Transport-Security", "max-age=31536000")
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
        if not origin:
            return
        # 經過 Tailscale / Cloudflare 時 Host 可能被改成 localhost,所以也接受對外網址
        allowed = {self.headers.get("Host", ""), self.headers.get("X-Forwarded-Host", "")}
        public = self.app.store.get_setting("public_url")
        if public:
            allowed.add(urlsplit(public).netloc)
        if urlsplit(origin).netloc not in allowed:
            raise ApiError(403, "來源不符")

    def _require(self, role: str | None = None, active: bool = True) -> dict:
        sess = self._session()
        if not sess:
            raise ApiError(401, "請先登入")
        if role and sess["role"] != role:
            raise ApiError(403, "沒有權限")
        if active and sess["role"] == "guest" and sess["status"] != "active":
            raise ApiError(403, "等待對方確認好友")
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
        if c["invite_expires"] > time.time():
            # openExternalBrowser=1:在 LINE 裡點開時改用手機的瀏覽器,登入狀態才不會不見
            out["invite_url"] = f"{self._base_url()}/c/{c['invite_token']}?openExternalBrowser=1"
        else:
            out["invite_url"] = ""
            out["invite_expires"] = 0
        return out

    def _device(self) -> str:
        return describe_device(self.headers.get("User-Agent", ""))

    # --- dispatch
    def do_HEAD(self) -> None:
        self.do_GET()

    def do_GET(self) -> None:
        self._safely(self._route_get)

    def do_POST(self) -> None:
        self._safely(self._route_post)

    def _safely(self, route) -> None:
        """任何沒料到的錯誤都回 500,而不是直接斷線(斷線會讓網頁以為家裡電腦關機)。"""
        try:
            route()
        except ApiError as e:
            self._json({"error": e.message}, e.status)
        except (BrokenPipeError, ConnectionResetError):
            pass
        except Exception:
            print(f"----- {datetime.now():%Y-%m-%d %H:%M:%S} 處理 {self.command} {urlsplit(self.path).path} 時出錯 -----")
            traceback.print_exc()
            try:
                self._json({"error": "家裡電腦的 HomeChat 發生錯誤,請看 data/homechat.log"}, 500)
            except Exception:
                pass

    def _route_get(self) -> None:
        url = urlsplit(self.path)
        qs = {k: v[-1] for k, v in parse_qs(url.query).items()}
        if url.path in ("/", "/index.html") or re.fullmatch(r"/(c|add)/[A-Za-z0-9_-]{1,64}", url.path):
            return self._static("index.html")
        if url.path in ("/manifest.webmanifest", "/icon.svg", "/sw.js"):
            return self._static(url.path.lstrip("/"))
        routes = {
            "/api/me": self._me,
            "/api/invite": self._invite_info,
            "/api/add-info": self._add_info,
            "/api/contacts": self._list_contacts,
            "/api/messages": self._get_messages,
            "/api/events": self._events,
            "/api/export": self._export,
            "/api/friend-link": self._friend_link,
            "/api/devices": self._devices,
        }
        if url.path in routes:
            return routes[url.path](qs)
        raise ApiError(404, "找不到")

    def _route_post(self) -> None:
        url = urlsplit(self.path)
        self._check_origin()
        data = self._read_json()
        routes = {
            "/api/setup": self._setup,
            "/api/login": self._login,
            "/api/logout": self._logout,
            "/api/password": self._change_password,
            "/api/join": self._join,
            "/api/request": self._request_friend,
            "/api/messages": self._post_message,
            "/api/read": self._read,
            "/api/contacts": self._add_contact,
            "/api/contacts/import": self._import_contacts,
            "/api/contacts/import-line": self._import_line,
            "/api/friend-link": self._set_friend_link,
            "/api/devices/remove": self._remove_device,
            "/api/settings": self._settings,
        }
        if url.path in routes:
            return routes[url.path](data)
        m = re.fullmatch(r"/api/contacts/(\d+)/(update|invite|cancel-invite|approve|logout-devices|delete)",
                         url.path)
        if m:
            return self._contact_action(int(m.group(1)), m.group(2), data)
        raise ApiError(404, "找不到")

    # --- pages
    def _static(self, name: str) -> None:
        path = STATIC_DIR / name
        types = {".html": "text/html; charset=utf-8", ".svg": "image/svg+xml",
                 ".webmanifest": "application/manifest+json", ".js": "text/javascript; charset=utf-8"}
        self._send(200, path.read_bytes(), types[path.suffix], {
            "Cache-Control": "no-cache",
            "Content-Security-Policy": HTML_CSP,
        })

    # --- 主人帳號
    def _me(self, qs: dict) -> None:
        if not self.app.store.has_password():
            # 第一次使用:只有坐在這台電腦前面的人可以設定密碼
            return self._json({"role": None, "setup": self._is_local()})
        sess = self._session()
        if not sess:
            return self._json({"role": None, "owner_name": self.app.owner_name})
        # 每次打開都把登入延長一年,像 LINE 一樣不用一直重新登入
        refresh = self._login_cookie(self._session_token())
        if sess["role"] == "owner":
            return self._json({
                "role": "owner",
                "owner_name": self.app.owner_name,
                "username": self.app.store.owner_username(),
                "public_url": self.app.store.get_setting("public_url"),
                "tunnel": self.app.store.get_setting("public_url_auto"),
                "tunnel_note": self.app.store.get_setting("tunnel_note"),
            }, headers=refresh)
        contact = self.app.store.get_contact(sess["contact_id"])
        return self._json({
            "role": "guest",
            "owner_name": self.app.owner_name,
            "username": contact["username"],
            "contact": {"id": contact["id"], "name": contact["name"], "status": contact["status"],
                        "owner_read_id": contact["owner_read_id"], "has_password": contact["has_password"]},
        }, headers=refresh)

    @staticmethod
    def _check_new_password(pw: str, minimum: int = MIN_PASSWORD) -> None:
        if len(pw) < minimum:
            raise ApiError(400, f"密碼至少要 {minimum} 個字")
        if len(pw) > 200:
            raise ApiError(400, "密碼太長")

    def _check_new_username(self, username: str, except_contact: int | None = None) -> str:
        username = username.strip()
        if not USERNAME_RE.match(username):
            raise ApiError(400, "帳號要 3~30 個字,只能用英文字母、數字和 . _ -")
        if self.app.store.username_taken(username, except_contact):
            raise ApiError(409, "這個帳號已經有人用了,換一個吧")
        return username.lower()

    def _setup(self, data: dict) -> None:
        if self.app.store.has_password():
            raise ApiError(403, "已經設定過密碼")
        if not self._is_local():
            raise ApiError(403, "第一次設定只能在家裡這台電腦上操作")
        pw = str(data.get("password", ""))
        username = self._check_new_username(str(data.get("username", "")))
        self._check_new_password(pw)
        self.app.store.set_password(pw)
        self.app.store.set_setting("owner_username", username)
        name = self._clean(data.get("owner_name"), 50)
        if name:
            self.app.store.set_setting("owner_name", name)
        token = self.app.store.create_session("owner", device=self._device())
        self._json({"ok": True}, headers=self._login_cookie(token))

    def _login(self, data: dict) -> None:
        key = self._client_key()
        if self.app.login_fails.blocked(key) or self.app.login_fails_all.blocked("*"):
            raise ApiError(429, "密碼錯誤太多次,請 10 分鐘後再試")
        username = str(data.get("username", "")).strip().lower()
        password = str(data.get("password", ""))
        owner_user = self.app.store.owner_username()
        token = None
        # 舊版沒有設定主人帳號時,主人只看密碼
        if (not owner_user or username == owner_user) and self.app.store.check_password(password):
            token = self.app.store.create_session("owner", device=self._device())
        elif username and username != owner_user:
            contact = self.app.store.find_guest_login(username, password)
            if contact:
                token = self.app.store.create_session("guest", contact["id"], self._device())
        if not token:
            self.app.login_fails.add(key)
            self.app.login_fails_all.add("*")
            time.sleep(1)
            raise ApiError(401, "帳號或密碼錯誤")
        self._json({"ok": True}, headers=self._login_cookie(token))

    def _logout(self, data: dict) -> None:
        self.app.store.delete_session(token_hash(self._session_token()))
        self._json({"ok": True}, headers={"Set-Cookie": self._cookie_header("", 0)})

    def _change_password(self, data: dict) -> None:
        sess = self._require(active=False)
        old, new = str(data.get("old", "")), str(data.get("new", ""))
        if sess["role"] == "owner":
            if not self.app.store.check_password(old):
                time.sleep(1)
                raise ApiError(400, "目前的密碼不對")
            self._check_new_password(new)
            self.app.store.set_password(new)  # 會登出所有主人裝置
            token = self.app.store.create_session("owner", device=self._device())
            return self._json({"ok": True}, headers=self._login_cookie(token))
        contact = self.app.store.get_contact(sess["contact_id"])
        if not contact["username"] or not self.app.store.find_guest_login(contact["username"], old):
            time.sleep(1)
            raise ApiError(400, "目前的密碼不對")
        self._check_new_password(new, MIN_GUEST_PASSWORD)
        self.app.store.set_guest_account(contact["id"], None, new)
        self.app.store.delete_sessions("guest", contact["id"], keep=sess["id"])  # 其他裝置登出
        self.app.hub.kick_guests_except(contact["id"], sess["id"])
        self._json({"ok": True})

    def _devices(self, qs: dict) -> None:
        sess = self._require("owner")
        if "contact" in qs:
            contact = self._contact_for(sess, qs["contact"])
            items = self.app.store.list_sessions("guest", contact["id"])
        else:
            items = self.app.store.list_sessions("owner")
        for item in items:
            item["current"] = item["id"] == sess["id"]
        self._json({"devices": items})

    def _remove_device(self, data: dict) -> None:
        sess = self._require("owner")
        sid = str(data.get("id", ""))
        if data.get("all_others"):
            self.app.store.delete_sessions("owner", None, keep=sess["id"])
            return self._json({"ok": True})
        target = None
        with self.app.store.lock:
            row = self.app.store.db.execute("SELECT role, contact_id FROM sessions WHERE id = ?", (sid,)).fetchone()
            target = dict(row) if row else None
        if not target:
            raise ApiError(404, "找不到這台裝置")
        self.app.store.delete_session(sid)
        if target["role"] == "guest":
            self.app.hub.kick(sid)
        self._json({"ok": True})

    # --- 加好友
    def _invite_info(self, qs: dict) -> None:
        contact = self.app.store.invite_contact(qs.get("token", ""))
        if not contact:
            return self._json({"valid": False, "owner_name": self.app.owner_name})
        self._json({"valid": True, "owner_name": self.app.owner_name, "name": contact["name"],
                    "username": contact["username"]})

    def _join(self, data: dict) -> None:
        """朋友按下「開始聊天」才用掉邀請連結(LINE 預覽連結時不會用掉)。"""
        current = self._session()
        if current and current["role"] == "owner":
            # 主人自己點開測試:不要用掉朋友的連結,也不要把主人登出
            if not self.app.store.invite_contact(str(data.get("token", ""))):
                raise ApiError(410, "這條邀請連結已經用過或過期了。")
            return self._json({"ok": True, "as_owner": True})
        token_in = str(data.get("token", ""))
        target = self.app.store.invite_contact(token_in)
        if not target:
            raise ApiError(410, "這條邀請連結已經用過或過期了,請向對方要一條新的。")
        # 先檢查帳號密碼,有問題就不要用掉邀請連結
        password = str(data.get("password", ""))
        username = None
        if not target["username"]:
            username = self._check_new_username(str(data.get("username", "")), target["id"])
        self._check_new_password(password, MIN_GUEST_PASSWORD)
        result = self.app.store.claim_invite(token_in, self._device(), username, hash_password(password))
        if not result:
            raise ApiError(410, "這條邀請連結已經用過或過期了,請向對方要一條新的。")
        contact, token = result
        self.app.hub.publish({"type": "joined", "contact_id": contact["id"], "name": contact["name"]},
                             contact["id"], to_guest=False)
        self._json({"ok": True, "as_owner": False}, headers=self._login_cookie(token))

    def _add_info(self, qs: dict) -> None:
        enabled = self.app.store.get_setting("friend_link_on") == "1"
        valid = enabled and hmac.compare_digest(qs.get("token", ""), self.app.friend_link_token())
        self._json({"valid": valid, "owner_name": self.app.owner_name})

    def _request_friend(self, data: dict) -> None:
        enabled = self.app.store.get_setting("friend_link_on") == "1"
        if not (enabled and hmac.compare_digest(str(data.get("token", "")), self.app.friend_link_token())):
            raise ApiError(410, "這個加好友連結已經關閉或換新了。")
        name = self._clean(data.get("name"), 40)
        if not name:
            raise ApiError(400, "請輸入你的名字")
        username = self._check_new_username(str(data.get("username", "")))
        password = str(data.get("password", ""))
        self._check_new_password(password, MIN_GUEST_PASSWORD)
        key = self._client_key()
        if (self.app.store.count_pending() >= MAX_PENDING
                or not self.app.requests.take(key) or not self.app.requests_all.take("*")):
            raise ApiError(429, "申請太多了,請晚點再試")
        contact = self.app.store.add_contact(name, status="pending",
                                             request_msg=self._clean(data.get("message"), 200))
        self.app.store.set_guest_account(contact["id"], username, password)
        token = self.app.store.create_session("guest", contact["id"], self._device())
        self.app.hub.publish({"type": "request", "contact_id": contact["id"], "name": name},
                             contact["id"], to_guest=False)
        self._json({"ok": True}, headers=self._login_cookie(token))

    def _friend_link(self, qs: dict) -> None:
        self._require("owner")
        on = self.app.store.get_setting("friend_link_on") == "1"
        url = f"{self._base_url()}/add/{self.app.friend_link_token()}?openExternalBrowser=1"
        self._json({"enabled": on, "url": url if on else ""})

    def _set_friend_link(self, data: dict) -> None:
        self._require("owner")
        if data.get("reset"):
            self.app.store.set_setting("friend_token", secrets.token_urlsafe(18))
        if "enabled" in data:
            self.app.store.set_setting("friend_link_on", "1" if data["enabled"] else "")
        self._friend_link({})

    # --- 聊天
    def _list_contacts(self, qs: dict) -> None:
        self._require("owner")
        self._json({"contacts": [self._public_contact(c) for c in self.app.store.list_contacts()]})

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
        if contact["status"] != "active":
            raise ApiError(403, "請先接受好友申請")
        body = str(data.get("body", "")).strip()
        if not body:
            raise ApiError(400, "訊息是空的")
        if len(body) > MAX_MESSAGE:
            raise ApiError(400, f"訊息太長(上限 {MAX_MESSAGE} 字)")
        if sess["role"] == "guest" and not self.app.messages.take(sess["id"]):
            raise ApiError(429, "訊息傳太快了,休息一下")
        sender = "me" if sess["role"] == "owner" else "them"
        client_id = self._clean(data.get("client_id"), 64) or None
        msg, new = self.app.store.add_message(contact["id"], sender, body, client_id)
        if new:
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

    def _events(self, qs: dict) -> None:
        sess = self._require(active=False)  # 等待確認的朋友也要能收到「已接受」
        q = self.app.hub.subscribe(sess["role"], sess.get("contact_id"), sess["id"])
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
                    if not self.app.store.get_session(self._session_token()):
                        event = None  # 登入已失效
                    else:
                        self.wfile.write(b": ping\n\n")
                        self.wfile.flush()
                        continue
                if event is None:  # 被登出 / 被移除
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
        """下載聊天紀錄 .txt。主人和朋友都可以把對話存到自己的電腦。"""
        sess = self._require()
        contact = self._contact_for(sess, qs.get("contact"))
        if sess["role"] == "owner":
            names = {"me": self.app.owner_name, "them": contact["name"]}
            title = contact["name"]
        else:
            names = {"me": self.app.owner_name, "them": contact["name"]}
            title = self.app.owner_name
        lines = [f"[HomeChat] 與{title}的聊天記錄", f"下載時間:{datetime.now():%Y/%m/%d %H:%M}", ""]
        day = None
        for m in self.app.store.all_messages(contact["id"]):
            dt = datetime.fromtimestamp(m["created_at"])
            if dt.date() != day:
                day = dt.date()
                lines += ["", dt.strftime("%Y/%m/%d")]
            who = names[m["sender"]]
            body = m["body"]
            if "\n" in body:
                body = f'"{body}"'
            lines.append(f"{dt:%H:%M}\t{who}\t{body}")
        filename = quote(f"HomeChat_{title}_{datetime.now():%Y%m%d}.txt")
        self._send(200, "\n".join(lines).encode("utf-8"), "text/plain; charset=utf-8", {
            "Content-Disposition": f"attachment; filename*=UTF-8''{filename}",
            "Cache-Control": "no-store",
        })

    @staticmethod
    def _clean(value, limit: int) -> str:
        # 去掉控制字元(換行以外)
        text = re.sub(r"[\x00-\x08\x0b-\x1f\x7f‪-‮⁦-⁩]", "", str(value or ""))
        return text.strip()[:limit]

    def _add_contact(self, data: dict) -> None:
        self._require("owner")
        name = self._clean(data.get("name"), 100)
        if not name:
            raise ApiError(400, "請輸入名字")
        c = self.app.store.add_contact(name, self._clean(data.get("line_id"), 100),
                                       self._clean(data.get("note"), 500))
        if data.get("invite", True):
            self.app.store.new_invite(c["id"])
            c = self.app.store.get_contact(c["id"])
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
            contact = self.app.store.add_contact(contact_name[:100])
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
        store, hub = self.app.store, self.app.hub
        if action == "update":
            name = self._clean(data.get("name"), 100) or contact["name"]
            store.update_contact(contact_id, name, self._clean(data.get("line_id"), 100),
                                 self._clean(data.get("note"), 500))
        elif action == "invite":
            store.new_invite(contact_id)
        elif action == "cancel-invite":
            store.cancel_invite(contact_id)
        elif action == "approve":
            store.approve_contact(contact_id)
            hub.publish({"type": "approved"}, contact_id)
        elif action == "logout-devices":
            store.delete_sessions("guest", contact_id)
            hub.kick_guests(contact_id)
        elif action == "delete":  # 也用來拒絕好友申請
            hub.kick_guests(contact_id)
            store.delete_contact(contact_id)
            hub.publish({"type": "contacts"}, contact_id, to_guest=False)
            return self._json({"ok": True})
        hub.publish({"type": "contacts"}, contact_id, to_guest=False)
        self._json({"contact": self._public_contact(store.get_contact(contact_id))})

    def _settings(self, data: dict) -> None:
        self._require("owner")
        if "username" in data and str(data["username"]).strip().lower() != self.app.store.owner_username():
            self.app.store.set_setting("owner_username", self._check_new_username(str(data["username"])))
        if "owner_name" in data:
            name = self._clean(data["owner_name"], 50)
            if name:
                self.app.store.set_setting("owner_name", name)
        if "public_url" in data:
            url = self._clean(data["public_url"], 300).rstrip("/")
            if url and not re.match(r"^https?://[^\s/]+$", url):
                raise ApiError(400, "對外網址格式像 https://example.com(不要有路徑)")
            if url != self.app.store.get_setting("public_url"):
                self.app.store.set_setting("public_url", url)
                self.app.store.set_setting("public_url_auto", "")  # 手動設定的,啟動時不要蓋掉
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


# ---------------------------------------------------------------- 讓外面連進來

# 背景執行(pythonw)時開子程式不要跳出黑色視窗
NO_WINDOW = {"creationflags": subprocess.CREATE_NO_WINDOW} if os.name == "nt" else {}

TS_URL_RE = re.compile(r"https://[a-z0-9-]+(?:\.[a-z0-9-]+)*\.ts\.net")
CF_URL_RE = re.compile(r"https://[a-z0-9-]+\.trycloudflare\.com")


def find_program(name: str, extra: list[str]) -> str | None:
    found = shutil.which(name)
    if found:
        return found
    for path in extra:
        if Path(path).is_file():
            return path
    return None


def find_tailscale() -> str | None:
    return find_program("tailscale", [
        r"C:\Program Files\Tailscale\tailscale.exe",
        "/Applications/Tailscale.app/Contents/MacOS/Tailscale",
    ])


def find_cloudflared() -> str | None:
    return find_program("cloudflared", [
        r"C:\Program Files (x86)\cloudflared\cloudflared.exe",
        r"C:\Program Files\cloudflared\cloudflared.exe",
        "/opt/homebrew/bin/cloudflared",
        "/usr/local/bin/cloudflared",
    ])


def start_tailscale(exe: str, port: int, timeout: float = 300, on_note=None) -> str | None:
    """用 Tailscale Funnel 開一個固定的 https 網址。回傳網址或 None。"""
    try:
        status = subprocess.run([exe, "status", "--json"], capture_output=True, text=True, timeout=20, **NO_WINDOW)
        info = json.loads(status.stdout or "{}")
    except (OSError, subprocess.SubprocessError, json.JSONDecodeError):
        info = {}
    if info.get("BackendState") not in (None, "Running"):
        print("  Tailscale 還沒登入:請打開 Tailscale App 登入後再重新啟動 HomeChat。")
        if on_note:
            on_note("Tailscale 還沒登入:請打開 Tailscale 登入,然後重新開機(或重新執行安裝程式)。")
        return None
    print("  正在開啟 Tailscale Funnel…(第一次使用時,如果下面出現網址,請用瀏覽器打開並按「啟用」)")
    try:
        proc = subprocess.Popen([exe, "funnel", "--bg", str(port)], stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace", **NO_WINDOW)
    except OSError as e:
        print(f"  無法執行 Tailscale:{e}")
        return None
    timer = threading.Timer(timeout, proc.kill)
    timer.start()
    url = None
    opened = False
    try:
        for line in proc.stdout:
            line = line.rstrip()
            m = TS_URL_RE.search(line)
            if m:
                url = m.group(0)
            elif line.strip():
                print("    " + line.strip())  # 例如「Funnel is not enabled…請到這個網址啟用」
                enable = re.search(r"https://login\.tailscale\.com/\S+", line)
                if enable and not opened:
                    # 第一次要在 Tailscale 網站按「啟用」:直接幫忙打開(背景執行時也看得到)
                    opened = True
                    if on_note:
                        on_note(f"第一次使用要啟用 Tailscale Funnel:請打開 {enable.group(0)} 按「Enable」")
                    open_browser(enable.group(0))
        proc.wait()
    finally:
        timer.cancel()
    if proc.returncode != 0 and not url:
        return None
    if not url:  # 有些版本不印網址,改從 status 讀機器的網域
        try:
            info = json.loads(subprocess.run([exe, "status", "--json"], capture_output=True,
                                             text=True, timeout=20, **NO_WINDOW).stdout)
            dns = info["Self"]["DNSName"].rstrip(".")
            url = f"https://{dns}" if dns else None
        except (OSError, subprocess.SubprocessError, json.JSONDecodeError, KeyError, TypeError):
            url = None
    return url


def start_cloudflared(exe: str, port: int, timeout: float = 60) -> tuple[str | None, subprocess.Popen | None]:
    """用 Cloudflare 快速通道開一個臨時 https 網址(每次啟動都會變)。"""
    try:
        proc = subprocess.Popen([exe, "tunnel", "--no-autoupdate", "--url", f"http://127.0.0.1:{port}"],
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True,
                                encoding="utf-8", errors="replace", **NO_WINDOW)
    except OSError as e:
        print(f"  無法執行 cloudflared:{e}")
        return None, None
    found: dict = {}
    ready = threading.Event()

    def pump() -> None:
        # 一直讀輸出,不然 cloudflared 的輸出塞滿會卡住
        for line in proc.stdout:
            if "url" not in found:
                m = CF_URL_RE.search(line)
                if m:
                    found["url"] = m.group(0)
                    ready.set()
            if "Registered tunnel connection" in line and not found.get("connected"):
                found["connected"] = True
                print("  Cloudflare 通道已連上")
        ready.set()

    threading.Thread(target=pump, daemon=True).start()
    ready.wait(timeout)
    if "url" not in found:
        proc.terminate()
        return None, None
    return found["url"], proc


def open_tunnel(kind: str, port: int, on_note=None) -> tuple[str | None, str | None, subprocess.Popen | None]:
    """回傳 (對外網址, 用的是哪種, 要在結束時關掉的程式)。"""
    if kind in ("auto", "tailscale"):
        exe = find_tailscale()
        if exe:
            url = start_tailscale(exe, port, on_note=on_note)
            if url:
                return url, "tailscale", None
            print("  Tailscale Funnel 沒有開成功。")
        elif kind == "tailscale":
            print("  找不到 Tailscale,請先安裝:https://tailscale.com/download")
    if kind in ("auto", "cloudflare"):
        exe = find_cloudflared()
        if exe:
            print("  正在開啟 Cloudflare 快速通道…")
            url, proc = start_cloudflared(exe, port)
            if url:
                return url, "cloudflare", proc
            print("  Cloudflare 通道沒有開成功。")
        elif kind == "cloudflare":
            print("  找不到 cloudflared,請先安裝(見 README)")
    return None, None, None


def ask_password() -> str:
    while True:
        pw = getpass.getpass(f"設定主人密碼(至少 {MIN_PASSWORD} 個字): ")
        if len(pw) < MIN_PASSWORD:
            print("太短了,再試一次。")
            continue
        if getpass.getpass("再輸入一次: ") != pw:
            print("兩次不一樣,再試一次。")
            continue
        return pw


def running_version(port: int) -> str | None:
    """這個 port 上是不是 HomeChat?是的話回傳版本("old" = 沒有版本號的舊版),不是回傳 None。

    用 /icon.svg 探測:不碰資料庫,舊版遇到新版資料庫也不會當掉。
    """
    try:
        with urllib.request.urlopen(f"http://127.0.0.1:{port}/icon.svg", timeout=3) as r:
            if not r.headers.get("Server", "").startswith("HomeChat"):
                return None
            return r.headers.get("X-HomeChat-Version") or "old"
    except urllib.error.HTTPError as e:
        if e.headers.get("Server", "").startswith("HomeChat"):
            return e.headers.get("X-HomeChat-Version") or "old"
        return None
    except Exception:
        return None


def pids_listening(port: int) -> set[int]:
    """找出在這個 port 等連線的程式。"""
    pids: set[int] = set()
    try:
        if os.name == "nt":
            out = subprocess.run(["netstat", "-ano", "-p", "TCP"], capture_output=True, text=True,
                                 timeout=15, **NO_WINDOW).stdout
            for line in out.splitlines():
                # TCP  0.0.0.0:8800  0.0.0.0:0  LISTENING  1234(狀態文字會隨語言改變,所以看遠端位址)
                parts = line.split()
                if (len(parts) >= 5 and parts[0].upper() == "TCP" and parts[1].endswith(f":{port}")
                        and parts[2] in ("0.0.0.0:0", "[::]:0") and parts[-1].isdigit()):
                    pids.add(int(parts[-1]))
        else:
            out = subprocess.run(["lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN", "-t"], capture_output=True,
                                 text=True, timeout=15).stdout
            pids.update(int(x) for x in out.split() if x.isdigit())
    except (OSError, subprocess.SubprocessError, ValueError):
        pass
    pids.discard(os.getpid())
    return pids


def stop_process(pid: int) -> None:
    try:
        if os.name == "nt":
            subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True, timeout=15, **NO_WINDOW)
        else:
            os.kill(pid, signal.SIGTERM)
    except (OSError, subprocess.SubprocessError):
        pass


def open_browser(url: str) -> None:
    try:
        webbrowser.open(url)
    except Exception:
        pass


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description="HomeChat - 架在家中電腦的聊天室")
    p.add_argument("--host", default="0.0.0.0", help="監聽位址(預設 0.0.0.0,同 Wi-Fi 的手機也能連)")
    p.add_argument("--port", type=int, default=8800)
    p.add_argument("--db", default=str(DEFAULT_DB), help="資料庫檔案位置")
    p.add_argument("--name", help="你的顯示名稱(朋友會看到)")
    p.add_argument("--tunnel", choices=["auto", "tailscale", "cloudflare", "off"], default="auto",
                   help="自動開外部連線,讓不同 Wi-Fi / 4G 也能連(預設 auto:有 Tailscale 用 Tailscale,否則 cloudflared)")
    p.add_argument("--public-url", default="", help="自己的固定對外網址(設定後不會自動開通道)")
    p.add_argument("--open", action="store_true", help="啟動後打開瀏覽器")
    p.add_argument("--set-password", action="store_true", help="在這個視窗重設主人密碼(忘記密碼時用)")
    args = p.parse_args(argv)
    if sys.stdout is None or sys.stderr is None:
        # 沒有視窗的背景執行(Windows pythonw):訊息寫到 data/homechat.log
        log_path = Path(args.db).parent / "homechat.log"
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log = open(log_path, "a", encoding="utf-8", buffering=1)
        sys.stdout = sys.stderr = log
        print(f"\n----- {datetime.now():%Y-%m-%d %H:%M:%S} 啟動 -----")
    for stream in (sys.stdout, sys.stderr):  # Windows 輸出到檔案時遇到特殊字元不要當掉
        try:
            stream.reconfigure(errors="replace")
        except (AttributeError, ValueError):
            pass

    local_url = f"http://127.0.0.1:{args.port}"
    store = Store(args.db)
    if args.set_password:
        store.set_password(os.environ.get("HOMECHAT_PASSWORD") or ask_password())
        print("密碼已更新,所有主人裝置需要重新登入。")
        return 0
    if os.environ.get("HOMECHAT_PASSWORD") and not store.has_password():
        store.set_password(os.environ["HOMECHAT_PASSWORD"])

    app = App(store, owner_name=args.name)
    server = None
    for attempt in range(2):
        try:
            server = make_server(app, args.host, args.port)
            break
        except OSError as e:
            version = running_version(args.port)
            if version == VERSION:
                # 已經開著了(例如開機自動啟動),桌面捷徑只要打開瀏覽器就好
                print(f"HomeChat 已經在執行中:{local_url}")
                open_browser(local_url)
                return 0
            if version and attempt == 0:
                # 更新後舊版還開著(例如之前自己在黑色視窗執行的):關掉它換新版
                print(f"發現舊版 HomeChat({version})還在執行,先把它關掉…")
                for pid in pids_listening(args.port):
                    stop_process(pid)
                for _ in range(20):
                    time.sleep(0.5)
                    if running_version(args.port) is None:
                        break
                continue
            print(f"無法使用 port {args.port}({e})。是不是有其他程式佔用了?", file=sys.stderr)
            return 1
    if server is None:
        print(f"無法啟動:port {args.port} 一直被佔用", file=sys.stderr)
        return 1
    threading.Thread(target=server.serve_forever, daemon=True).start()
    print(f"HomeChat 已啟動,資料存在 {Path(args.db).resolve()}")

    if not store.has_password():
        # 第一次使用:在瀏覽器設定密碼。設好之前不開外部連線,外面的人搶不到設定頁
        print(f"第一次使用:請在瀏覽器打開 {local_url} 設定密碼…")
        open_browser(local_url)
        try:
            while not store.has_password():
                time.sleep(1)
        except KeyboardInterrupt:
            server.shutdown()
            return 0
        print("密碼設定完成")
    elif args.open:
        open_browser(local_url)

    store.set_setting("tunnel_note", "")
    tunnel_proc = None
    if args.public_url:
        # 自己有固定網域(例如 Cloudflare 具名通道)
        store.set_setting("public_url", args.public_url.rstrip("/"))
        store.set_setting("public_url_auto", "")
    elif args.tunnel != "off":
        print("設定外部連線…")
        url, kind, tunnel_proc = open_tunnel(args.tunnel, args.port,
                                             on_note=lambda text: store.set_setting("tunnel_note", text))
        if url:
            store.set_setting("public_url", url)
            store.set_setting("public_url_auto", kind)
            store.set_setting("tunnel_note", "")
        elif store.get_setting("public_url_auto"):
            store.set_setting("public_url", "")  # 上次自動設的網址已經失效
            store.set_setting("public_url_auto", "")
    elif store.get_setting("public_url_auto"):
        store.set_setting("public_url", "")
        store.set_setting("public_url_auto", "")

    public = store.get_setting("public_url")
    kind = store.get_setting("public_url_auto")
    print()
    print("=" * 60)
    if public:
        print(f"  在外面(4G/5G、別的 Wi-Fi、朋友):{public}")
        if kind == "cloudflare":
            print("  ⚠ 這是 Cloudflare 臨時網址,每次重開 HomeChat 都會變,")
            print("    舊的邀請連結會失效。要固定網址請改用 Tailscale(見 README)。")
    else:
        print("  ⚠ 目前只有同一個 Wi-Fi 連得到。要在外面也能連,請照 README")
        print("    安裝 Tailscale(推薦)或 cloudflared,然後重新啟動 HomeChat。")
        if args.tunnel != "off" and not store.get_setting("tunnel_note"):
            store.set_setting("tunnel_note", "目前只有同一個 Wi-Fi 連得到。要在外面也能連,請安裝 Tailscale(重新執行安裝程式即可),然後重新開機。")
    print(f"  這台電腦:{local_url}")
    for ip in lan_addresses():
        print(f"  同 Wi-Fi:http://{ip}:{args.port}")
    print("=" * 60)
    print("這個視窗不要關(可以縮小)。按 Ctrl+C 結束")
    try:
        while True:
            time.sleep(3600)
    except KeyboardInterrupt:
        print("\n已關閉")
    finally:
        server.shutdown()
        server.server_close()
        if tunnel_proc:
            tunnel_proc.terminate()
    return 0


if __name__ == "__main__":
    sys.exit(main())
