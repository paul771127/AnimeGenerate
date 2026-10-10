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
import base64
import binascii
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
import tempfile
import time
import traceback
import zipfile
import urllib.error
import urllib.parse
import urllib.request
import webbrowser
from datetime import datetime
from http.cookies import SimpleCookie
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, quote, unquote, urlsplit

HERE = Path(__file__).resolve().parent
STATIC_DIR = HERE / "static"
DEFAULT_DB = HERE / "data" / "homechat.db"

VERSION = "2026.10.18"  # 改了資料庫格式或 API 就更新,用來認出還在執行的舊版
COOKIE_NAME = "hc_session"
SESSION_IDLE_DAYS = 365  # 像 LINE 一樣一直保持登入;一年沒用才自動登出(每次使用都會重新計算)
INVITE_DAYS = 7  # 邀請連結 7 天內有效,只能用一次
MIN_PASSWORD = 8  # 主人
MIN_GUEST_PASSWORD = 6  # 朋友
USERNAME_RE = re.compile(r"^[A-Za-z0-9._-]{3,30}$")
MAX_PENDING = 20  # 最多同時 20 個待確認的好友申請
MAX_BODY = 20 * 1024 * 1024  # LINE 聊天紀錄可能很大
MAX_MESSAGE = 5000
MAX_UPLOAD = 100 * 1024 * 1024  # 圖片 / 檔案 / 語音,每個最大 100 MB
MIN_FREE_DISK = 500 * 1024 * 1024  # 硬碟剩不到 500 MB 就不收檔案
MESSAGE_KINDS = ("text", "image", "file", "audio", "sticker")  # 使用者自己送的;album / stickers 由伺服器產生
KIND_LABELS = {"image": "[圖片]", "audio": "[語音訊息]", "file": "[檔案]", "sticker": "[貼圖]",
               "album": "[相簿]", "stickers": "[分享貼圖]"}
MAX_ALBUMS = 100  # 每段對話最多 100 本相簿
MAX_ALBUM_PHOTOS = 2000
MAX_SHARE_STICKERS = 40  # 一次最多分享 40 張貼圖
STICKER_TYPES = {"image/png", "image/gif", "image/webp", "image/jpeg"}
MAX_STICKER = 2 * 1024 * 1024  # 每張貼圖最大 2 MB
MAX_STICKERS = 300  # 每個人最多 300 張
MAX_AVATAR = 1024 * 1024  # 大頭貼最大 1 MB(網頁會先縮成 512×512)
MAX_STATUS = 100  # 狀態消息最多 100 個字


def image_type(head: bytes) -> str | None:
    """看檔案開頭判斷是哪種圖片(不相信瀏覽器說的 Content-Type)。"""
    if head.startswith(b"\xff\xd8\xff"):
        return "image/jpeg"
    if head.startswith(b"\x89PNG\r\n\x1a\n"):
        return "image/png"
    if head[:4] == b"RIFF" and head[8:12] == b"WEBP":
        return "image/webp"
    if head[:6] in (b"GIF87a", b"GIF89a"):
        return "image/gif"
    return None


# ---------------------------------------------------------------- 推播通知(Web Push)
# 手機把 HomeChat 關掉時,要靠瀏覽器的推播服務(Google / Apple / Mozilla)叫醒它。
# 推播內容是空的(推播服務看不到訊息),手機被叫醒後自己來家裡電腦問「有什麼新訊息」。
# 推播服務要求用 VAPID(P-256 ECDSA 簽章)證明是同一台伺服器;Python 內建沒有,這裡自己算。
_P256_P = 2 ** 256 - 2 ** 224 + 2 ** 192 + 2 ** 96 - 1
_P256_N = 0xFFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551
_P256_B = 0x5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B
_P256_G = (0x6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296,
           0x4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5)


def _ec_add(a, b):
    if a is None:
        return b
    if b is None:
        return a
    p = _P256_P
    if a[0] == b[0]:
        if (a[1] + b[1]) % p == 0:
            return None
        lam = (3 * a[0] * a[0] - 3) * pow(2 * a[1], -1, p) % p
    else:
        lam = (b[1] - a[1]) * pow(b[0] - a[0], -1, p) % p
    x = (lam * lam - a[0] - b[0]) % p
    return x, (lam * (a[0] - x) - a[1]) % p


def _ec_mul(k: int, point):
    result = None
    while k:
        if k & 1:
            result = _ec_add(result, point)
        point = _ec_add(point, point)
        k >>= 1
    return result


def b64url(raw: bytes) -> str:
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode()


def vapid_keys() -> tuple[int, bytes]:
    """產生一組 P-256 金鑰:(私鑰, 公鑰 65 bytes)。"""
    d = secrets.randbelow(_P256_N - 1) + 1
    x, y = _ec_mul(d, _P256_G)
    return d, b"\x04" + x.to_bytes(32, "big") + y.to_bytes(32, "big")


def ecdsa_sign(d: int, data: bytes) -> bytes:
    """ES256 簽章(r||s,各 32 bytes)。"""
    z = int.from_bytes(hashlib.sha256(data).digest(), "big")
    while True:
        k = secrets.randbelow(_P256_N - 1) + 1
        r = _ec_mul(k, _P256_G)[0] % _P256_N
        s = pow(k, -1, _P256_N) * (z + r * d) % _P256_N
        if r and s:
            return r.to_bytes(32, "big") + s.to_bytes(32, "big")


def ecdsa_verify(public: bytes, data: bytes, sig: bytes) -> bool:
    """驗證 ES256 簽章(測試用)。"""
    q = (int.from_bytes(public[1:33], "big"), int.from_bytes(public[33:], "big"))
    if (q[1] ** 2 - q[0] ** 3 + 3 * q[0] - _P256_B) % _P256_P:
        return False
    r, s = int.from_bytes(sig[:32], "big"), int.from_bytes(sig[32:], "big")
    if not (0 < r < _P256_N and 0 < s < _P256_N):
        return False
    z = int.from_bytes(hashlib.sha256(data).digest(), "big")
    w = pow(s, -1, _P256_N)
    pt = _ec_add(_ec_mul(z * w % _P256_N, _P256_G), _ec_mul(r * w % _P256_N, q))
    return pt is not None and pt[0] % _P256_N == r


# 只送到真正的推播服務(朋友不能叫家裡電腦去連其他網址)
PUSH_HOSTS = re.compile(r"(^|\.)(fcm\.googleapis\.com|android\.googleapis\.com|push\.services\.mozilla\.com|"
                        r"push\.apple\.com|notify\.windows\.com)$")


def valid_push_endpoint(url: str) -> bool:
    try:
        parts = urlsplit(url)
    except ValueError:
        return False
    return parts.scheme == "https" and bool(parts.hostname) and bool(PUSH_HOSTS.search(parts.hostname)) \
        and len(url) <= 2000
# 可以直接在網頁上顯示 / 播放的格式;其他一律當成下載,避免 HTML、SVG 之類夾帶程式
INLINE_TYPES = {
    "image/jpeg", "image/png", "image/gif", "image/webp", "image/avif",
    "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/aac", "audio/x-m4a", "audio/wav",
    "video/mp4", "video/webm", "video/quicktime",
}
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
    guest_read_id INTEGER NOT NULL DEFAULT 0,
    peer_id TEXT,                                -- 互通:對方 HomeChat 的編號
    peer_url TEXT,                               -- 互通:對方 HomeChat 的網址
    peer_secret TEXT,                            -- 互通:兩台之間簽章用的共享密鑰
    peer_name TEXT,                              -- 互通:對方 HomeChat 主人的名字
    peer_ok_at REAL,                             -- 最後一次成功連到對方的時間
    peer_error TEXT,                             -- 最後一次連不上的原因
    status_msg TEXT NOT NULL DEFAULT '',         -- 狀態消息(朋友自己設定的,或互通時對方主人的)
    avatar TEXT NOT NULL DEFAULT ''              -- 大頭貼版本(空的 = 沒設),檔案在 data/avatars/c<id>
);
CREATE TABLE IF NOT EXISTS messages (
    id INTEGER PRIMARY KEY,
    contact_id INTEGER NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    sender TEXT NOT NULL CHECK (sender IN ('me', 'them')),
    body TEXT NOT NULL,
    created_at REAL NOT NULL,
    source TEXT NOT NULL DEFAULT 'chat',
    client_id TEXT,                              -- 裝置產生的編號,離線排隊重送時不會重複
    kind TEXT NOT NULL DEFAULT 'text',           -- text / image / file / audio
    attachment_id TEXT,
    uid TEXT,                                    -- 全域編號:互通時兩台用它認出同一則訊息
    from_peer INTEGER NOT NULL DEFAULT 0,        -- 1 = 從對方 HomeChat 收到的(不再轉送回去)
    fed_state TEXT                               -- 互通:queued 排隊中 / sent 已送到對方電腦
);
CREATE TABLE IF NOT EXISTS fed_outbox (         -- 互通:要送到對方 HomeChat 的東西,連不上就排隊重試
    id INTEGER PRIMARY KEY,
    contact_id INTEGER NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    payload TEXT NOT NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    next_try REAL NOT NULL,
    created_at REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS pair_codes (         -- 互通碼:只能用一次、24 小時內有效
    token TEXT PRIMARY KEY,
    contact_id INTEGER REFERENCES contacts(id) ON DELETE CASCADE,  -- 連到既有聯絡人;NULL = 新增
    name TEXT NOT NULL DEFAULT '',
    expires REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS stickers (           -- 自己的貼圖,檔案在 data/stickers/<id>
    id TEXT PRIMARY KEY,
    role TEXT NOT NULL CHECK (role IN ('owner', 'guest')),
    contact_id INTEGER REFERENCES contacts(id) ON DELETE CASCADE,  -- 朋友的貼圖屬於他的聯絡人;主人的是 NULL
    mime TEXT NOT NULL,
    size INTEGER NOT NULL,
    created_at REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS albums (             -- 共同相簿:屬於一段對話,兩個人都能加照片
    id INTEGER PRIMARY KEY,
    contact_id INTEGER NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    created_by TEXT NOT NULL,                    -- me / them
    created_at REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS album_photos (
    id INTEGER PRIMARY KEY,
    album_id INTEGER NOT NULL REFERENCES albums(id) ON DELETE CASCADE,
    attachment_id TEXT NOT NULL,
    added_by TEXT NOT NULL,
    created_at REAL NOT NULL,
    UNIQUE (album_id, attachment_id)
);
CREATE TABLE IF NOT EXISTS message_files (      -- 一則訊息用到好幾個檔案時(分享貼圖組)
    message_id INTEGER NOT NULL REFERENCES messages(id) ON DELETE CASCADE,
    attachment_id TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS attachments (        -- 圖片、檔案、語音,實際檔案在 data/files/<id>
    id TEXT PRIMARY KEY,
    contact_id INTEGER NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
    uploader TEXT NOT NULL,                      -- me / them
    name TEXT NOT NULL,
    mime TEXT NOT NULL,
    size INTEGER NOT NULL,
    created_at REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_messages_contact ON messages(contact_id, created_at, id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_messages_client ON messages(contact_id, client_id) WHERE client_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_contacts_username ON contacts(username) WHERE username IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_messages_uid ON messages(contact_id, uid) WHERE uid IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_contacts_peer ON contacts(peer_id) WHERE peer_id IS NOT NULL;
CREATE TABLE IF NOT EXISTS push_subs (           -- 推播通知:每台裝置的瀏覽器給的推播網址
    endpoint TEXT PRIMARY KEY,
    session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
    vibrate INTEGER NOT NULL DEFAULT 1,
    created_at REAL NOT NULL
);
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

def content_disposition(kind: str, name: str) -> str:
    """中文檔名用 filename*,另外附一個英文備用檔名給看不懂的舊瀏覽器 / App 內建瀏覽器。"""
    stem, dot, ext = name.rpartition(".")
    ascii_ext = re.sub(r"[^A-Za-z0-9]", "", ext) if dot else ""
    ascii_stem = re.sub(r"[^A-Za-z0-9._-]+", "_", stem if dot else name).strip("_") or "homechat"
    fallback = ascii_stem + (f".{ascii_ext}" if ascii_ext else "")
    return f"{kind}; filename=\"{fallback}\"; filename*=UTF-8''{quote(name)}"


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
            self.files_dir = path.parent / "files"
            self.stickers_dir = path.parent / "stickers"
            self.avatars_dir = path.parent / "avatars"
        else:
            tmp = Path(tempfile.mkdtemp(prefix="homechat-"))
            self.files_dir, self.stickers_dir, self.avatars_dir = tmp / "files", tmp / "stickers", tmp / "avatars"
        self.files_dir.mkdir(exist_ok=True)
        self.stickers_dir.mkdir(exist_ok=True)
        self.avatars_dir.mkdir(exist_ok=True)
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
                              ("password_hash", "TEXT NOT NULL DEFAULT ''"),
                              ("peer_id", "TEXT"), ("peer_url", "TEXT"), ("peer_secret", "TEXT"),
                              ("peer_name", "TEXT"), ("peer_ok_at", "REAL"), ("peer_error", "TEXT"),
                              ("status_msg", "TEXT NOT NULL DEFAULT ''"), ("avatar", "TEXT NOT NULL DEFAULT ''")):
                if name not in cols:
                    self.db.execute(f"ALTER TABLE contacts ADD COLUMN {name} {ddl}")
        cols = {r["name"] for r in self.db.execute("PRAGMA table_info(messages)")}
        if cols:
            for name, ddl in (("client_id", "TEXT"), ("kind", "TEXT NOT NULL DEFAULT 'text'"),
                              ("attachment_id", "TEXT"), ("uid", "TEXT"),
                              ("from_peer", "INTEGER NOT NULL DEFAULT 0"), ("fed_state", "TEXT")):
                if name not in cols:
                    self.db.execute(f"ALTER TABLE messages ADD COLUMN {name} {ddl}")
            # 舊訊息補上全域編號
            self.db.execute("UPDATE messages SET uid = lower(hex(randomblob(12))) WHERE uid IS NULL")

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
            # 互通狀態(不含密鑰)
            "peer": {"name": row["peer_name"], "url": row["peer_url"], "ok_at": row["peer_ok_at"],
                     "error": row["peer_error"]} if row["peer_id"] else None,
            "status_msg": row["status_msg"],
            "avatar": row["avatar"],
        }

    def list_contacts(self) -> list[dict]:
        with self.lock:
            rows = self.db.execute(
                """
                SELECT c.*,
                  (SELECT COUNT(*) FROM messages m WHERE m.contact_id = c.id AND m.sender = 'them'
                     AND m.source = 'chat' AND m.id > c.owner_read_id) AS unread,
                  (SELECT CASE m.kind WHEN 'image' THEN '[圖片]' WHEN 'audio' THEN '[語音訊息]'
                                      WHEN 'file' THEN '[檔案]' WHEN 'sticker' THEN '[貼圖]'
                                      WHEN 'album' THEN '[相簿]' WHEN 'stickers' THEN '[分享貼圖]' ELSE m.body END
                     FROM messages m WHERE m.contact_id = c.id
                     ORDER BY m.created_at DESC, m.id DESC LIMIT 1) AS last_body,
                  (SELECT created_at FROM messages m WHERE m.contact_id = c.id
                     ORDER BY m.created_at DESC, m.id DESC LIMIT 1) AS last_at,
                  (SELECT COUNT(*) FROM sessions s WHERE s.contact_id = c.id) AS devices,
                  (SELECT MAX(last_seen) FROM sessions s WHERE s.contact_id = c.id) AS last_seen,
                  (SELECT COUNT(*) FROM fed_outbox o WHERE o.contact_id = c.id AND o.payload LIKE '%"message"%') AS peer_queued
                FROM contacts c
                ORDER BY c.status = 'pending' DESC, COALESCE(last_at, c.created_at) DESC
                """
            ).fetchall()
        out = []
        for r in rows:
            item = self._contact_row(r)
            item.update(unread=r["unread"], last_body=r["last_body"] or "", last_at=r["last_at"],
                        devices=r["devices"], last_seen=r["last_seen"])
            if item["peer"]:
                item["peer"]["queued"] = r["peer_queued"]
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
            ids = [r["id"] for r in self.db.execute("SELECT id FROM attachments WHERE contact_id = ?", (contact_id,))]
            stickers = [r["id"] for r in self.db.execute("SELECT id FROM stickers WHERE contact_id = ?", (contact_id,))]
            self.db.execute("DELETE FROM contacts WHERE id = ?", (contact_id,))
            self.db.commit()
        for att_id in ids:  # 對話刪掉,圖片和檔案也一起刪
            self.file_path(att_id).unlink(missing_ok=True)
        for sid in stickers:  # 朋友的貼圖也刪
            self.sticker_path(sid).unlink(missing_ok=True)
        self.avatar_path(f"c{contact_id}").unlink(missing_ok=True)

    # --- 大頭貼和狀態消息(主人的存在 settings,朋友 / 互通對象的存在 contacts)
    def avatar_path(self, key: str) -> Path:
        return self.avatars_dir / key  # key:owner 或 c<聯絡人編號>

    def set_contact_profile(self, contact_id: int, *, status_msg: str | None = None, avatar: str | None = None) -> None:
        with self.lock:
            if status_msg is not None:
                self.db.execute("UPDATE contacts SET status_msg = ? WHERE id = ?", (status_msg, contact_id))
            if avatar is not None:
                self.db.execute("UPDATE contacts SET avatar = ? WHERE id = ?", (avatar, contact_id))
            self.db.commit()

    def peer_contact_ids(self) -> list[int]:
        with self.lock:
            return [r["id"] for r in self.db.execute("SELECT id FROM contacts WHERE peer_id IS NOT NULL")]

    # --- 推播通知
    def vapid(self) -> tuple[int, str]:
        """(私鑰, 公鑰 base64url)。第一次用到時產生,之後一直用同一組(換了手機就要重新訂閱)。"""
        raw = self.get_setting("vapid_private")
        if not raw:
            d, public = vapid_keys()
            self.set_setting("vapid_private", format(d, "x"))
            self.set_setting("vapid_public", b64url(public))
            raw = format(d, "x")
        return int(raw, 16), self.get_setting("vapid_public")

    def add_push_sub(self, endpoint: str, session_id: str, vibrate: bool) -> None:
        with self.lock:
            self.db.execute("INSERT INTO push_subs (endpoint, session_id, vibrate, created_at) VALUES (?, ?, ?, ?) "
                            "ON CONFLICT(endpoint) DO UPDATE SET session_id = excluded.session_id, "
                            "vibrate = excluded.vibrate", (endpoint, session_id, int(vibrate), time.time()))
            # 每台裝置最多 5 個(換瀏覽器、重新訂閱留下的舊的清掉)
            self.db.execute("DELETE FROM push_subs WHERE session_id = ? AND endpoint NOT IN "
                            "(SELECT endpoint FROM push_subs WHERE session_id = ? ORDER BY created_at DESC LIMIT 5)",
                            (session_id, session_id))
            self.db.commit()

    def remove_push_sub(self, endpoint: str, session_id: str | None = None) -> None:
        with self.lock:
            if session_id is None:
                self.db.execute("DELETE FROM push_subs WHERE endpoint = ?", (endpoint,))
            else:
                self.db.execute("DELETE FROM push_subs WHERE endpoint = ? AND session_id = ?", (endpoint, session_id))
            self.db.commit()

    def push_subs_for(self, role: str, contact_id: int | None = None, session_id: str | None = None) -> list[dict]:
        with self.lock:
            if session_id:
                rows = self.db.execute("SELECT p.* FROM push_subs p WHERE p.session_id = ?", (session_id,))
            elif role == "owner":
                rows = self.db.execute("SELECT p.* FROM push_subs p JOIN sessions s ON s.id = p.session_id "
                                       "WHERE s.role = 'owner'")
            else:
                rows = self.db.execute("SELECT p.* FROM push_subs p JOIN sessions s ON s.id = p.session_id "
                                       "WHERE s.role = 'guest' AND s.contact_id = ?", (contact_id,))
            return [dict(r) for r in rows]

    def latest_unread(self, role: str, contact_id: int | None = None) -> tuple[dict | None, int]:
        """最新一則還沒讀的訊息和未讀總數(通知要顯示的)。"""
        with self.lock:
            if role == "owner":
                where = "m.sender = 'them' AND m.id > c.owner_read_id AND c.status = 'active'"
                args: tuple = ()
            else:
                where = "m.sender = 'me' AND m.id > c.guest_read_id AND c.id = ?"
                args = (contact_id,)
            row = self.db.execute(f"SELECT m.id, m.contact_id, m.kind, m.body, c.name FROM messages m "
                                  f"JOIN contacts c ON c.id = m.contact_id WHERE {where} "
                                  f"ORDER BY m.created_at DESC, m.id DESC LIMIT 1", args).fetchone()
            count = self.db.execute(f"SELECT COUNT(*) FROM messages m JOIN contacts c ON c.id = m.contact_id "
                                    f"WHERE {where}", args).fetchone()[0]
        return (dict(row) if row else None), count

    # --- 附件(圖片、檔案、語音)
    def file_path(self, att_id: str) -> Path:
        return self.files_dir / att_id

    def add_attachment(self, att_id: str, contact_id: int, uploader: str, name: str, mime: str, size: int) -> dict:
        with self.lock:
            self.db.execute(
                "INSERT INTO attachments (id, contact_id, uploader, name, mime, size, created_at) "
                "VALUES (?, ?, ?, ?, ?, ?, ?)",
                (att_id, contact_id, uploader, name, mime, size, time.time()),
            )
            self.db.commit()
        return self.get_attachment(att_id)

    def get_attachment(self, att_id: str) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM attachments WHERE id = ?", (att_id,)).fetchone()
        return dict(row) if row else None

    # --- 貼圖
    def sticker_path(self, sid: str) -> Path:
        return self.stickers_dir / sid

    def add_sticker(self, sid: str, role: str, contact_id: int | None, mime: str, size: int) -> dict:
        with self.lock:
            self.db.execute(
                "INSERT INTO stickers (id, role, contact_id, mime, size, created_at) VALUES (?, ?, ?, ?, ?, ?)",
                (sid, role, contact_id, mime, size, time.time()),
            )
            self.db.commit()
        return self.get_sticker(sid)

    def get_sticker(self, sid: str) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM stickers WHERE id = ?", (sid,)).fetchone()
        return dict(row) if row else None

    def list_stickers(self, role: str, contact_id: int | None) -> list[dict]:
        with self.lock:
            rows = self.db.execute(
                "SELECT id, mime, size, created_at FROM stickers WHERE role = ? AND contact_id IS ? "
                "ORDER BY created_at DESC", (role, contact_id),
            ).fetchall()
        return [dict(r) for r in rows]

    def delete_sticker(self, sid: str) -> None:
        with self.lock:
            self.db.execute("DELETE FROM stickers WHERE id = ?", (sid,))
            self.db.commit()
        self.sticker_path(sid).unlink(missing_ok=True)

    def find_message(self, contact_id: int, client_id: str) -> dict | None:
        with self.lock:
            row = self.db.execute(self.MSG_SELECT + "WHERE m.contact_id = ? AND m.client_id = ?",
                                  (contact_id, client_id)).fetchone()
        return self._message_row(row) if row else None

    # --- 共同相簿
    def list_albums(self, contact_id: int) -> list[dict]:
        with self.lock:
            rows = self.db.execute(
                """
                SELECT al.*,
                  (SELECT COUNT(*) FROM album_photos p WHERE p.album_id = al.id) AS count,
                  (SELECT p.attachment_id FROM album_photos p WHERE p.album_id = al.id
                     ORDER BY p.created_at DESC, p.id DESC LIMIT 1) AS cover
                FROM albums al WHERE al.contact_id = ? ORDER BY al.created_at DESC
                """, (contact_id,)).fetchall()
        return [dict(r) for r in rows]

    def get_album(self, album_id: int) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM albums WHERE id = ?", (album_id,)).fetchone()
        return dict(row) if row else None

    def add_album(self, contact_id: int, name: str, created_by: str) -> dict:
        with self.lock:
            cur = self.db.execute("INSERT INTO albums (contact_id, name, created_by, created_at) VALUES (?, ?, ?, ?)",
                                  (contact_id, name, created_by, time.time()))
            self.db.commit()
        return self.get_album(cur.lastrowid)

    def rename_album(self, album_id: int, name: str) -> None:
        with self.lock:
            self.db.execute("UPDATE albums SET name = ? WHERE id = ?", (name, album_id))
            self.db.commit()

    def delete_album(self, album_id: int) -> None:
        """刪相簿只是拿掉相簿;照片檔案如果聊天裡也沒用到,之後會被自動清掉。"""
        with self.lock:
            self.db.execute("DELETE FROM albums WHERE id = ?", (album_id,))
            self.db.commit()

    def album_photos(self, album_id: int) -> list[dict]:
        with self.lock:
            rows = self.db.execute(
                "SELECT p.id, p.added_by, p.created_at, a.id AS att_id, a.name, a.mime, a.size "
                "FROM album_photos p JOIN attachments a ON a.id = p.attachment_id "
                "WHERE p.album_id = ? ORDER BY p.created_at DESC, p.id DESC", (album_id,)).fetchall()
        return [{"id": r["id"], "added_by": r["added_by"], "created_at": r["created_at"],
                 "attachment": {"id": r["att_id"], "name": r["name"], "mime": r["mime"], "size": r["size"]}}
                for r in rows]

    def add_album_photos(self, album_id: int, attachment_ids: list[str], added_by: str) -> int:
        added = 0
        now = time.time()
        with self.lock:
            for i, att_id in enumerate(attachment_ids):
                cur = self.db.execute(
                    "INSERT OR IGNORE INTO album_photos (album_id, attachment_id, added_by, created_at) VALUES (?, ?, ?, ?)",
                    (album_id, att_id, added_by, now + i * 0.001))
                added += cur.rowcount
            self.db.commit()
        return added

    def get_album_photo(self, photo_id: int) -> dict | None:
        with self.lock:
            row = self.db.execute("SELECT * FROM album_photos WHERE id = ?", (photo_id,)).fetchone()
        return dict(row) if row else None

    def remove_album_photo(self, photo_id: int) -> None:
        with self.lock:
            self.db.execute("DELETE FROM album_photos WHERE id = ?", (photo_id,))
            self.db.commit()

    def add_message_files(self, message_id: int, attachment_ids: list[str]) -> None:
        with self.lock:
            self.db.executemany("INSERT INTO message_files (message_id, attachment_id) VALUES (?, ?)",
                                [(message_id, a) for a in attachment_ids])
            self.db.commit()

    def cleanup_attachments(self, older_than: float = 86400) -> int:
        """上傳了但沒有送出的檔案(例如上傳到一半關掉)過一天就刪掉;也清掉沒有紀錄的殘留檔案。"""
        cutoff = time.time() - older_than
        with self.lock:
            ids = [r["id"] for r in self.db.execute(
                "SELECT a.id FROM attachments a WHERE a.created_at < ? "
                "AND NOT EXISTS (SELECT 1 FROM messages m WHERE m.attachment_id = a.id) "
                "AND NOT EXISTS (SELECT 1 FROM album_photos p WHERE p.attachment_id = a.id) "
                "AND NOT EXISTS (SELECT 1 FROM message_files f WHERE f.attachment_id = a.id)", (cutoff,))]
            self.db.executemany("DELETE FROM attachments WHERE id = ?", [(i,) for i in ids])
            self.db.commit()
            known = {r["id"] for r in self.db.execute("SELECT id FROM attachments")}
        removed = 0
        for att_id in ids:
            self.file_path(att_id).unlink(missing_ok=True)
            removed += 1
        for f in self.files_dir.iterdir():
            if f.is_file() and f.name not in known and f.stat().st_mtime < cutoff:
                f.unlink(missing_ok=True)
                removed += 1
        return removed

    # --- messages
    MSG_SELECT = ("SELECT m.*, a.name AS a_name, a.mime AS a_mime, a.size AS a_size "
                  "FROM messages m LEFT JOIN attachments a ON a.id = m.attachment_id ")

    @staticmethod
    def _message_row(row: sqlite3.Row) -> dict:
        msg = {
            "id": row["id"],
            "contact_id": row["contact_id"],
            "sender": row["sender"],
            "body": row["body"],
            "created_at": row["created_at"],
            "source": row["source"],
            "client_id": row["client_id"],
            "kind": row["kind"],
            "attachment": None,
            "uid": row["uid"],
            "fed_state": row["fed_state"],
        }
        if row["attachment_id"] and row["a_name"] is not None:
            msg["attachment"] = {"id": row["attachment_id"], "name": row["a_name"],
                                 "mime": row["a_mime"], "size": row["a_size"]}
        return msg

    def add_message(self, contact_id: int, sender: str, body: str, client_id: str | None = None,
                    kind: str = "text", attachment_id: str | None = None, *, uid: str | None = None,
                    created_at: float | None = None, from_peer: bool = False, source: str = "chat",
                    fed_state: str | None = None) -> tuple[dict, bool]:
        """回傳 (訊息, 是不是新的)。同一個 client_id / uid 重送不會重複新增。"""
        with self.lock:
            for col, value in (("client_id", client_id), ("uid", uid)):
                if value:
                    row = self.db.execute(
                        self.MSG_SELECT + f"WHERE m.contact_id = ? AND m.{col} = ?", (contact_id, value)
                    ).fetchone()
                    if row:
                        return self._message_row(row), False
            cur = self.db.execute(
                "INSERT INTO messages (contact_id, sender, body, created_at, client_id, kind, attachment_id, "
                "uid, from_peer, source, fed_state) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                (contact_id, sender, body, created_at or time.time(), client_id, kind, attachment_id,
                 uid or secrets.token_hex(12), int(from_peer), source, fed_state),
            )
            self.db.commit()
            row = self.db.execute(self.MSG_SELECT + "WHERE m.id = ?", (cur.lastrowid,)).fetchone()
        return self._message_row(row), True

    def list_messages(self, contact_id: int, before: tuple[float, int] | None = None,
                      limit: int = PAGE_SIZE) -> tuple[list[dict], bool]:
        """回傳 (由舊到新的訊息, 是否還有更舊的)。before = (created_at, id) 游標。"""
        sql = self.MSG_SELECT + "WHERE m.contact_id = ?"
        args: list = [contact_id]
        if before:
            sql += " AND (m.created_at < ? OR (m.created_at = ? AND m.id < ?))"
            args += [before[0], before[0], before[1]]
        sql += " ORDER BY m.created_at DESC, m.id DESC LIMIT ?"
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
                self.MSG_SELECT + "WHERE m.contact_id = ? ORDER BY m.created_at, m.id", (contact_id,)
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
                    "INSERT INTO messages (contact_id, sender, body, created_at, source, uid) "
                    "VALUES (?, ?, ?, ?, 'line', ?)",
                    (contact_id, sender, body, created_at, secrets.token_hex(12)),
                )
                added += 1
            self.db.commit()
        return added

    # --- 互通(跟朋友的 HomeChat 連在一起,兩邊各存一份完整紀錄)
    def server_id(self) -> str:
        sid = self.get_setting("server_id")
        if not sid:
            sid = secrets.token_urlsafe(16)
            self.set_setting("server_id", sid)
        return sid

    def peer_of(self, contact_id: int) -> dict | None:
        """對方 HomeChat 的連線資料(含密鑰,只在伺服器內部用)。"""
        with self.lock:
            row = self.db.execute(
                "SELECT id, peer_id, peer_url, peer_secret, peer_name FROM contacts WHERE id = ? AND peer_id IS NOT NULL",
                (contact_id,)).fetchone()
        return dict(row) if row else None

    def contact_by_peer(self, peer_id: str) -> dict | None:
        with self.lock:
            row = self.db.execute(
                "SELECT id, peer_id, peer_url, peer_secret, peer_name FROM contacts WHERE peer_id = ?", (peer_id,)
            ).fetchone()
        return dict(row) if row else None

    def link_peer(self, contact_id: int, peer_id: str, url: str, secret: str, name: str) -> None:
        with self.lock:
            # 同一台 HomeChat 只能連到一個聯絡人:重新配對時把舊的連結拿掉
            self.db.execute("UPDATE contacts SET peer_id = NULL, peer_url = NULL, peer_secret = NULL "
                            "WHERE peer_id = ? AND id != ?", (peer_id, contact_id))
            self.db.execute("UPDATE contacts SET peer_id = ?, peer_url = ?, peer_secret = ?, peer_name = ?, "
                            "peer_ok_at = ?, peer_error = NULL WHERE id = ?",
                            (peer_id, url, secret, name, time.time(), contact_id))
            self.db.commit()

    def unlink_peer(self, contact_id: int) -> None:
        with self.lock:
            self.db.execute("UPDATE contacts SET peer_id = NULL, peer_url = NULL, peer_secret = NULL, "
                            "peer_error = NULL WHERE id = ?", (contact_id,))
            self.db.execute("DELETE FROM fed_outbox WHERE contact_id = ?", (contact_id,))
            self.db.execute("UPDATE messages SET fed_state = NULL WHERE contact_id = ? AND fed_state = 'queued'",
                            (contact_id,))
            self.db.commit()

    def set_peer_status(self, contact_id: int, error: str | None) -> None:
        with self.lock:
            if error is None:
                self.db.execute("UPDATE contacts SET peer_ok_at = ?, peer_error = NULL WHERE id = ?",
                                (time.time(), contact_id))
            else:
                self.db.execute("UPDATE contacts SET peer_error = ? WHERE id = ?", (error[:200], contact_id))
            self.db.commit()

    def create_pair_code(self, contact_id: int | None, name: str, days: float = 1) -> str:
        token = secrets.token_urlsafe(24)
        with self.lock:
            self.db.execute("DELETE FROM pair_codes WHERE expires < ?", (time.time(),))
            self.db.execute("INSERT INTO pair_codes (token, contact_id, name, expires) VALUES (?, ?, ?, ?)",
                            (token, contact_id, name, time.time() + days * 86400))
            self.db.commit()
        return token

    def take_pair_code(self, token: str) -> dict | None:
        """用掉互通碼(只能用一次)。"""
        with self.lock:
            row = self.db.execute("SELECT * FROM pair_codes WHERE token = ? AND expires > ?",
                                  (token, time.time())).fetchone()
            if not row:
                return None
            self.db.execute("DELETE FROM pair_codes WHERE token = ?", (token,))
            self.db.commit()
        return dict(row)

    def enqueue(self, contact_id: int, payload: dict, delay: float = 0) -> None:
        now = time.time()
        with self.lock:
            self.db.execute("INSERT INTO fed_outbox (contact_id, payload, next_try, created_at) VALUES (?, ?, ?, ?)",
                            (contact_id, json.dumps(payload, ensure_ascii=False), now + delay, now))
            self.db.commit()

    def due_outbox(self, limit: int = 100) -> list[dict]:
        with self.lock:
            rows = self.db.execute("SELECT * FROM fed_outbox WHERE next_try <= ? ORDER BY id LIMIT ?",
                                   (time.time(), limit)).fetchall()
        return [dict(r) for r in rows]

    def outbox_done(self, item_id: int) -> None:
        with self.lock:
            self.db.execute("DELETE FROM fed_outbox WHERE id = ?", (item_id,))
            self.db.commit()

    def outbox_retry(self, item_id: int, attempts: int, delay: float) -> None:
        with self.lock:
            self.db.execute("UPDATE fed_outbox SET attempts = ?, next_try = ? WHERE id = ?",
                            (attempts, time.time() + delay, item_id))
            self.db.commit()

    def retry_now(self, contact_id: int) -> None:
        with self.lock:
            self.db.execute("UPDATE fed_outbox SET next_try = ? WHERE contact_id = ?", (time.time(), contact_id))
            self.db.commit()

    def set_fed_state(self, message_id: int, state: str | None) -> None:
        with self.lock:
            self.db.execute("UPDATE messages SET fed_state = ? WHERE id = ?", (state, message_id))
            self.db.commit()

    def message_by_uid(self, contact_id: int, uid: str) -> dict | None:
        with self.lock:
            row = self.db.execute(self.MSG_SELECT + "WHERE m.contact_id = ? AND m.uid = ?", (contact_id, uid)).fetchone()
        return self._message_row(row) if row else None

    def get_message(self, message_id: int) -> dict | None:
        with self.lock:
            row = self.db.execute(self.MSG_SELECT + "WHERE m.id = ?", (message_id,)).fetchone()
        return self._message_row(row) if row else None

    def last_message_upto(self, contact_id: int, upto: int) -> dict | None:
        """已讀同步用:這個人讀到的最後一則訊息。"""
        with self.lock:
            row = self.db.execute(self.MSG_SELECT + "WHERE m.contact_id = ? AND m.id <= ? ORDER BY m.id DESC LIMIT 1",
                                  (contact_id, upto)).fetchone()
        return self._message_row(row) if row else None

    def local_messages(self, contact_id: int) -> list[dict]:
        """配對時把以前的對話複製給對方:只送本機產生的(對方給的不送回去)。"""
        with self.lock:
            rows = self.db.execute(self.MSG_SELECT + "WHERE m.contact_id = ? AND m.from_peer = 0 ORDER BY m.id",
                                   (contact_id,)).fetchall()
        return [self._message_row(r) for r in rows]


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

    def subscribe(self, role: str, contact_id: int | None, sid: str = "", visible: bool = True) -> queue.Queue:
        q: queue.Queue = queue.Queue(maxsize=1000)
        q.token = secrets.token_urlsafe(9)  # type: ignore[attr-defined]  # 網頁回報「正在看 / 在背景」用
        q.visible = visible  # type: ignore[attr-defined]
        with self.lock:
            self.subs.append((role, contact_id, sid, q))
        return q

    def unsubscribe(self, q: queue.Queue) -> None:
        with self.lock:
            self.subs = [s for s in self.subs if s[3] is not q]

    def set_visible(self, sid: str, token: str, visible: bool) -> None:
        with self.lock:
            for _, _, s_id, q in self.subs:
                if s_id == sid and q.token == token:  # type: ignore[attr-defined]
                    q.visible = visible  # type: ignore[attr-defined]

    def visible_for(self, role: str, contact_id: int | None = None) -> bool:
        """這個人有沒有正開著 HomeChat 在看(有的話網頁自己會提醒,不用推播)。"""
        with self.lock:
            return any(q.visible for r, cid, _, q in self.subs  # type: ignore[attr-defined]
                       if r == role and (role == "owner" or cid == contact_id))

    def online(self, sid: str) -> bool:
        with self.lock:
            return any(s_id == sid for _, _, s_id, _ in self.subs)

    def publish_to(self, event: dict, role: str, contact_id: int | None = None, sid: str | None = None) -> None:
        """只送給某一邊(主人 / 某個朋友),或某一台裝置。"""
        with self.lock:
            targets = [q for r, cid, s_id, q in self.subs
                       if (s_id == sid if sid else (r == role and (role == "owner" or cid == contact_id)))]
        for q in targets:
            try:
                q.put_nowait(event)
            except queue.Full:
                pass

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

    def broadcast(self, event: dict) -> None:
        """送給所有連著的人(主人改了大頭貼 / 狀態,每個朋友都要更新)。"""
        with self.lock:
            targets = [s[3] for s in self.subs]
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


# ---------------------------------------------------------------- 互通(server 對 server)
#
# 兩台 HomeChat 配對後各自保存完整的對話。新訊息先存自己這邊,再放進 fed_outbox 推給對方;
# 對方關機就依序排隊重試。每個請求都用配對時交換的共享密鑰做 HMAC 簽章,
# 簽的內容是「時間\n方法\n路徑\n內容的 SHA-256」,超過 10 分鐘的請求不收。

FED_MAX_SKEW = 600
FED_BACKOFF = (5, 15, 30, 60, 120, 300, 600)  # 連不上時第 1、2、3… 次重試前等幾秒
PAIR_CODE_PREFIX = "HC1."
LOCAL_HOSTS = ("127.0.0.1", "localhost", "::1")


def fed_sign(secret: str, ts: str, method: str, path: str, body_hash: str) -> str:
    msg = f"{ts}\n{method}\n{path}\n{body_hash}".encode()
    return hmac.new(secret.encode(), msg, hashlib.sha256).hexdigest()


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def valid_peer_url(url: str) -> bool:
    """對方網址要是 https(本機測試可以用 http://127.0.0.1)。"""
    parts = urlsplit(url)
    if parts.scheme == "https" and parts.hostname:
        return True
    return parts.scheme == "http" and parts.hostname in LOCAL_HOSTS


def encode_pair_code(url: str, token: str, name: str) -> str:
    raw = json.dumps({"u": url, "t": token, "n": name}, ensure_ascii=False, separators=(",", ":")).encode()
    return PAIR_CODE_PREFIX + base64.urlsafe_b64encode(raw).decode().rstrip("=")


def decode_pair_code(code: str) -> dict | None:
    code = re.sub(r"\s+", "", code or "")
    if not code.startswith(PAIR_CODE_PREFIX):
        return None
    data = code[len(PAIR_CODE_PREFIX):]
    try:
        info = json.loads(base64.urlsafe_b64decode(data + "=" * (-len(data) % 4)))
    except (ValueError, binascii.Error):
        return None
    if not isinstance(info, dict) or not all(isinstance(info.get(k), str) for k in ("u", "t", "n")):
        return None
    return info


class PeerError(Exception):
    """對方回了錯誤。permanent = 重送也沒用(例如格式錯誤),直接放棄這一筆。"""

    def __init__(self, status: int, message: str):
        super().__init__(message)
        self.status = status
        self.permanent = status in (400, 404, 413, 415, 422)


def fed_request(url: str, path: str, *, secret: str | None = None, server_id: str = "",
                payload: dict | None = None, file: Path | None = None, content_type: str = "",
                timeout: float = 30) -> dict:
    """送一個簽過章的請求到對方 HomeChat。連不上會丟 OSError / PeerError。"""
    if file is not None:
        body_hash = sha256_file(file)
        size = file.stat().st_size
        data = open(file, "rb")
        ctype = content_type or "application/octet-stream"
    else:
        raw = json.dumps(payload or {}, ensure_ascii=False).encode()
        body_hash = hashlib.sha256(raw).hexdigest()
        size = len(raw)
        data = raw
        ctype = "application/json"
    headers = {"Content-Type": ctype, "Content-Length": str(size), "User-Agent": f"HomeChat/{VERSION}"}
    if secret:
        ts = str(int(time.time()))
        headers.update({"X-HC-Peer": server_id, "X-HC-Time": ts, "X-HC-Body": body_hash,
                        "X-HC-Sig": fed_sign(secret, ts, "POST", path, body_hash)})
    req = urllib.request.Request(url.rstrip("/") + path, data=data, headers=headers, method="POST")
    # 本機測試不要經過系統 proxy
    handlers = [urllib.request.ProxyHandler({})] if urlsplit(url).hostname in LOCAL_HOSTS else []
    try:
        with urllib.request.build_opener(*handlers).open(req, timeout=timeout) as r:
            body = r.read()
    except urllib.error.HTTPError as e:
        try:
            message = json.loads(e.read() or b"{}").get("error", "")
        except ValueError:
            message = ""
        if e.code in (502, 503, 504):  # 通道開著但對方電腦沒回應
            raise OSError(f"對方的電腦沒有回應({e.code})")
        raise PeerError(e.code, message or f"對方回應錯誤 {e.code}")
    finally:
        if file is not None:
            data.close()
    try:
        return json.loads(body or b"{}")
    except ValueError:
        raise OSError("對方回傳的內容看不懂(網址可能不是 HomeChat)")


class Federation:
    """把本機的新訊息、已讀推到互通的對方 HomeChat。背景執行,連不上就排隊重試。"""

    def __init__(self, app: "App"):
        self.app = app
        self.store = app.store
        self.wake = threading.Event()
        self.lock = threading.Lock()
        threading.Thread(target=self._loop, daemon=True, name="homechat-federation").start()

    def _loop(self) -> None:
        while True:
            self.wake.wait(5)
            self.wake.clear()
            try:
                self.process()
            except Exception:
                traceback.print_exc()

    # --- 產生要送的東西
    def on_new_message(self, msg: dict) -> None:
        """本機新增的訊息(主人或用訪客身分登入的朋友傳的)→ 也送一份到對方 HomeChat。"""
        if not self.store.peer_of(msg["contact_id"]):
            return
        self.store.set_fed_state(msg["id"], "queued")
        msg["fed_state"] = "queued"
        self.store.enqueue(msg["contact_id"], {"type": "message", "message_id": msg["id"]})
        self.wake.set()

    def on_read(self, contact_id: int, role: str, upto: int) -> None:
        if not self.store.peer_of(contact_id):
            return
        last = self.store.last_message_upto(contact_id, upto)
        if last:
            self.store.enqueue(contact_id, {"type": "read", "uid": last["uid"], "reader": role})
            self.wake.set()

    def on_profile(self) -> None:
        """主人改了大頭貼 / 狀態 → 告訴每台互通的 HomeChat。"""
        for cid in self.store.peer_contact_ids():
            self.store.enqueue(cid, {"type": "profile"})
        self.wake.set()

    def sync_history(self, contact_id: int, delay: float = 0) -> int:
        """剛配對:把以前的對話也複製一份給對方(重複的對方會自動略過)。

        delay:被配對的那一邊要等對方先存好連線資料,晚幾秒再送。
        """
        msgs = self.store.local_messages(contact_id)
        for m in msgs:
            self.store.enqueue(contact_id, {"type": "message", "message_id": m["id"], "history": True}, delay)
        self.store.enqueue(contact_id, {"type": "profile"}, delay)  # 也把我的大頭貼和狀態給對方
        if not delay:
            self.wake.set()
        return len(msgs)

    # --- 送出
    def process(self) -> int:
        """把到期的項目依序送出。同一個對方只要有一筆失敗,後面的先不送(保持順序)。"""
        sent = 0
        with self.lock:
            blocked: set[int] = set()
            for item in self.store.due_outbox():
                cid = item["contact_id"]
                if cid in blocked:
                    continue
                peer = self.store.peer_of(cid)
                if not peer:
                    self.store.outbox_done(item["id"])
                    continue
                try:
                    self._deliver(peer, json.loads(item["payload"]))
                except PeerError as e:
                    if e.permanent:
                        print(f"互通:對方拒收一筆資料({e.status} {e}),略過")
                        self.store.outbox_done(item["id"])
                        continue
                    self._failed(item, cid, blocked, f"對方拒絕:{e}" if e.status in (401, 403) else str(e))
                    continue
                except (OSError, ValueError) as e:
                    if item["attempts"] == 0:  # 技術細節寫進記錄檔,畫面上顯示看得懂的
                        print(f"互通:連不上 {peer['peer_url']}:{e}")
                    self._failed(item, cid, blocked, "連不上對方的電腦(可能關機、睡眠或沒有網路)")
                    continue
                self.store.outbox_done(item["id"])
                if self.store.peer_of(cid):
                    self.store.set_peer_status(cid, None)
                sent += 1
        return sent

    def _failed(self, item: dict, cid: int, blocked: set, reason: str) -> None:
        blocked.add(cid)
        attempts = item["attempts"] + 1
        self.store.outbox_retry(item["id"], attempts, FED_BACKOFF[min(attempts, len(FED_BACKOFF)) - 1])
        first_failure = attempts == 1
        self.store.set_peer_status(cid, reason)
        if first_failure:
            self.app.hub.publish({"type": "contacts"}, cid, to_guest=False)

    def _deliver(self, peer: dict, payload: dict) -> None:
        sid = self.store.server_id()
        if payload["type"] == "message":
            msg = self.store.get_message(payload["message_id"])
            if not msg:
                return
            kind, body, att = msg["kind"], msg["body"], msg["attachment"]
            if kind in ("album", "stickers"):
                # 相簿 / 貼圖組卡片:第一階段先轉成一行文字
                try:
                    info = json.loads(body)
                except ValueError:
                    info = {}
                body = (f"📷 新增了 {info.get('count', 0)} 張照片到相簿「{info.get('name', '')}」" if kind == "album"
                        else f"⭐ 分享了 {info.get('count', 0)} 張貼圖")
                kind, att = "text", None
            if att:
                path = self.store.file_path(att["id"])
                if path.exists():
                    q = urllib.parse.urlencode({"name": att["name"], "mime": att["mime"], "sender": msg["sender"]})
                    fed_request(peer["peer_url"], f"/fed/files/{att['id']}?{q}", secret=peer["peer_secret"],
                                server_id=sid, file=path, content_type=att["mime"], timeout=600)
                else:
                    kind, att, body = "text", None, f"{KIND_LABELS.get(kind, '[檔案]')}(檔案已不在)"
            fed_request(peer["peer_url"], "/fed/inbox", secret=peer["peer_secret"], server_id=sid, payload={
                "type": "message", "uid": msg["uid"], "sender": msg["sender"], "kind": kind, "body": body,
                "created_at": msg["created_at"], "source": msg["source"], "history": bool(payload.get("history")),
                "attachment": {k: att[k] for k in ("id", "name", "mime", "size")} if att else None,
            })
            if msg["fed_state"] == "queued":
                self.store.set_fed_state(msg["id"], "sent")
                self.app.hub.publish({"type": "delivered", "contact_id": msg["contact_id"], "id": msg["id"]},
                                     msg["contact_id"])
        elif payload["type"] == "profile":
            # 送的是「現在」的樣子(排隊時改了好幾次也只要最新的)
            ver = self.store.get_setting("owner_avatar")
            path = self.store.avatar_path("owner")
            if ver and path.exists():
                with open(path, "rb") as f:
                    mime = image_type(f.read(16)) or "image/jpeg"
                fed_request(peer["peer_url"], "/fed/avatar", secret=peer["peer_secret"], server_id=sid,
                            file=path, content_type=mime, timeout=120)
            else:
                ver = ""
            fed_request(peer["peer_url"], "/fed/inbox", secret=peer["peer_secret"], server_id=sid, payload={
                "type": "profile", "status": self.store.get_setting("owner_status"), "avatar": ver})
        else:
            fed_request(peer["peer_url"], "/fed/inbox", secret=peer["peer_secret"], server_id=sid, payload=payload)

    def unpair(self, contact_id: int) -> None:
        """解除互通:先試著通知對方(連不上也沒關係),再把本機的連結拿掉。"""
        peer = self.store.peer_of(contact_id)
        if not peer:
            return
        try:
            fed_request(peer["peer_url"], "/fed/inbox", secret=peer["peer_secret"], server_id=self.store.server_id(),
                        payload={"type": "unpair"}, timeout=10)
        except (OSError, PeerError, ValueError):
            pass
        self.store.unlink_peer(contact_id)


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


class Pusher:
    """背景送推播(推播服務慢或連不上時不要卡住聊天)。"""

    def __init__(self, store: Store):
        self.store = store
        self.q: queue.Queue = queue.Queue()
        self.thread: threading.Thread | None = None
        self.jwts: dict[str, tuple[str, float]] = {}
        self.sent = 0  # 測試用

    def push(self, role: str, contact_id: int | None = None, session_id: str | None = None) -> None:
        subs = self.store.push_subs_for(role, contact_id, session_id)
        if not subs:
            return
        for sub in subs:
            self.q.put(sub["endpoint"])
        if not self.thread or not self.thread.is_alive():
            self.thread = threading.Thread(target=self._loop, name="push", daemon=True)
            self.thread.start()

    def _loop(self) -> None:
        while True:
            try:
                endpoint = self.q.get(timeout=60)
            except queue.Empty:
                return
            pending = {endpoint}
            while True:  # 同時好幾則只要叫醒一次
                try:
                    pending.add(self.q.get_nowait())
                except queue.Empty:
                    break
            for ep in pending:
                try:
                    self.send(ep)
                except Exception as e:  # noqa: BLE001 - 推播失敗不影響聊天
                    print(f"推播失敗:{e}")

    def _jwt(self, audience: str) -> str:
        cached = self.jwts.get(audience)
        if cached and cached[1] > time.time() + 600:
            return cached[0]
        d, _ = self.store.vapid()
        exp = int(time.time()) + 12 * 3600
        public = self.store.get_setting("public_url")
        sub = public if public.startswith("https://") else "mailto:homechat@example.com"
        head = b64url(json.dumps({"typ": "JWT", "alg": "ES256"}).encode())
        body = b64url(json.dumps({"aud": audience, "exp": exp, "sub": sub}).encode())
        token = f"{head}.{body}"
        token += "." + b64url(ecdsa_sign(d, token.encode()))
        self.jwts[audience] = (token, exp)
        return token

    def send(self, endpoint: str) -> int:
        if not valid_push_endpoint(endpoint):
            self.store.remove_push_sub(endpoint)
            return 0
        parts = urlsplit(endpoint)
        _, public = self.store.vapid()
        req = urllib.request.Request(endpoint, data=b"", method="POST", headers={
            "TTL": "86400", "Urgency": "high", "Content-Length": "0",
            "Authorization": f"vapid t={self._jwt(f'{parts.scheme}://{parts.netloc}')}, k={public}",
        })
        try:
            with urllib.request.urlopen(req, timeout=15) as r:
                status = r.status
        except urllib.error.HTTPError as e:
            status = e.code
        if status in (404, 410):  # 對方取消訂閱了(或換了手機)
            self.store.remove_push_sub(endpoint)
        elif status >= 400:
            print(f"推播服務回應 {status}")
        self.sent += 1
        return status


CALL_RING_SECONDS = 45


class Calls:
    """語音通話:家裡電腦只負責「打電話 / 接起來 / 掛掉」和轉送連線資訊,聲音是兩台裝置直接傳(WebRTC)。"""

    def __init__(self, app: "App"):
        self.app = app
        self.lock = threading.Lock()
        self.calls: dict[str, dict] = {}

    @staticmethod
    def other(role: str) -> str:
        return "guest" if role == "owner" else "owner"

    def find(self, call_id: str) -> dict | None:
        with self.lock:
            return self.calls.get(call_id)

    def for_contact(self, contact_id: int) -> dict | None:
        with self.lock:
            return next((c for c in self.calls.values() if c["contact_id"] == contact_id), None)

    def start(self, contact_id: int, role: str, sid: str, video: bool = False) -> dict:
        hub = self.app.hub
        busy = self.for_contact(contact_id)
        if busy:
            # 兩邊網頁都關掉了還留著的:直接結束
            gone = not hub.online(busy["caller_sid"]) and (not busy["callee_sid"] or not hub.online(busy["callee_sid"]))
            if not gone:
                raise ApiError(409, "對方正在通話中")
            self.end(busy["id"], "gone")
        call = {"id": secrets.token_urlsafe(12), "contact_id": contact_id, "caller": role, "caller_sid": sid,
                "callee_sid": None, "state": "ringing", "started": time.time(), "answered_at": None, "video": video}
        with self.lock:
            self.calls[call["id"]] = call
        callee = self.other(role)
        hub.publish_to({"type": "call", "action": "ring", "call_id": call["id"], "contact_id": contact_id,
                        "video": video}, callee, contact_id)
        if not hub.visible_for(callee, contact_id):
            self.app.pusher.push(callee, contact_id)
        timer = threading.Timer(CALL_RING_SECONDS, self._timeout, args=(call["id"],))
        timer.daemon = True
        timer.start()
        return call

    def _timeout(self, call_id: str) -> None:
        call = self.find(call_id)
        if call and call["state"] == "ringing":
            self.end(call_id, "missed")

    def answer(self, call: dict, sid: str) -> None:
        with self.lock:
            if call["state"] != "ringing":
                raise ApiError(409, "這通電話已經結束了")
            call["state"], call["callee_sid"], call["answered_at"] = "active", sid, time.time()
        hub = self.app.hub
        hub.publish_to({"type": "call", "action": "answered", "call_id": call["id"]}, "", sid=call["caller_sid"])
        # 同一個人的其他裝置停止響鈴
        hub.publish_to({"type": "call", "action": "taken", "call_id": call["id"]}, self.other(call["caller"]),
                       call["contact_id"])

    def signal(self, call: dict, sid: str, data: dict) -> None:
        if sid == call["caller_sid"]:
            target = call["callee_sid"]
        elif sid == call["callee_sid"]:
            target = call["caller_sid"]
        else:
            raise ApiError(403, "不是這通電話")
        if target:
            self.app.hub.publish_to({"type": "call", "action": "signal", "call_id": call["id"], "data": data},
                                    "", sid=target)

    def end(self, call_id: str, reason: str) -> None:
        with self.lock:
            call = self.calls.pop(call_id, None)
        if not call:
            return
        cid = call["contact_id"]
        event = {"type": "call", "action": "end", "call_id": call_id, "reason": reason}
        self.app.hub.publish_to(event, "owner")
        self.app.hub.publish_to(event, "guest", cid)
        icon, kind = ("📹", "視訊通話") if call.get("video") else ("📞", "語音通話")
        if call["answered_at"]:
            secs = int(time.time() - call["answered_at"])
            body = f"{icon} {kind} {secs // 60}:{secs % 60:02d}"
        else:
            body = icon + " " + {"declined": "對方拒接", "canceled": "已取消"}.get(reason, "未接來電")
        # 通話紀錄寫進對話(由打電話的那一方送出)
        sender = "me" if call["caller"] == "owner" else "them"
        store = self.app.store
        if not store.get_contact(cid):
            return
        msg, _ = store.add_message(cid, sender, body, None, "text", None)
        store.mark_read(cid, call["caller"], msg["id"])
        self.app.fed.on_new_message(msg)
        self.app.hub.publish({"type": "message", "message": msg}, cid)
        if reason not in ("canceled",):
            self.app.notify_message(msg)


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
        self.uploads = RateLimiter(60, 3600)  # 朋友每台裝置每小時 60 個檔案
        self.pairs = RateLimiter(10, 3600)  # 互通配對:同一來源每小時最多試 10 次
        if owner_name:
            store.set_setting("owner_name", owner_name)
        self.fed = Federation(self)
        self.pusher = Pusher(store)
        self.calls = Calls(self)
        store.vapid()  # 推播金鑰先準備好

    def notify_message(self, msg: dict) -> None:
        """新訊息:收的那個人沒開著 HomeChat 的話,推播到他的手機。"""
        role = "owner" if msg["sender"] == "them" else "guest"
        if self.hub.visible_for(role, msg["contact_id"]):
            return
        self.pusher.push(role, msg["contact_id"])

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
            "img-src 'self' data: blob:; media-src 'self' blob:; connect-src 'self'; frame-ancestors 'none'; "
            "base-uri 'none'; form-action 'self'")
# 下載的檔案:就算是 HTML 也不能執行任何東西
FILE_CSP = "default-src 'none'; img-src 'self'; media-src 'self'; style-src 'unsafe-inline'; sandbox"


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
        self.send_header("Permissions-Policy", "camera=(self), microphone=(self), geolocation=()")  # 麥克風:語音輸入、錄音、通話;相機:視訊通話
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
        if url.path in ("/manifest.webmanifest", "/icon.svg", "/sw.js", "/sticker-maker.js"):
            return self._static(url.path.lstrip("/"))
        m = re.fullmatch(r"/api/files/([A-Za-z0-9_-]{16,64})", url.path)
        if m:
            return self._file(m.group(1), qs)
        m = re.fullmatch(r"/api/sticker-files/([A-Za-z0-9_-]{16,64})", url.path)
        if m:
            return self._sticker_file(m.group(1), qs)
        m = re.fullmatch(r"/api/avatar/(owner|\d+)", url.path)
        if m:
            return self._avatar(m.group(1))
        m = re.fullmatch(r"/api/albums/(\d+)/(photos|zip)", url.path)
        if m:
            album = self._album_for(self._require(), int(m.group(1)))
            return self._album_photos(album) if m.group(2) == "photos" else self._album_zip(album)
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
            "/api/stickers": self._stickers,
            "/api/stickers/zip": self._stickers_zip,
            "/api/albums": self._albums,
            "/api/push/key": self._push_key,
            "/api/notify": self._notify_info,
            "/api/call": self._call_state,
        }
        if url.path in routes:
            return routes[url.path](qs)
        raise ApiError(404, "找不到")

    def _route_post(self) -> None:
        url = urlsplit(self.path)
        # 互通:其他 HomeChat 打過來的(用簽章驗證,不是瀏覽器)
        if url.path == "/fed/pair":
            return self._fed_pair()
        if url.path == "/fed/inbox":
            return self._fed_inbox()
        if url.path == "/fed/avatar":
            return self._fed_avatar()
        m = re.fullmatch(r"/fed/files/([A-Za-z0-9_-]{16,64})", url.path)
        if m:
            return self._fed_file(m.group(1), {k: v[-1] for k, v in parse_qs(url.query).items()})
        self._check_origin()
        if url.path == "/api/profile/avatar":
            return self._upload_avatar()
        if url.path == "/api/upload":
            return self._upload({k: v[-1] for k, v in parse_qs(url.query).items()})
        if url.path == "/api/stickers/upload":
            return self._upload_sticker()
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
            "/api/profile": self._profile,
            "/api/presence": self._presence,
            "/api/push/subscribe": self._push_subscribe,
            "/api/push/unsubscribe": self._push_unsubscribe,
            "/api/push/test": self._push_test,
            "/api/call/start": self._call_start,
            "/api/call/answer": self._call_answer,
            "/api/call/signal": self._call_signal,
            "/api/call/end": self._call_end,
            "/api/stickers/delete": self._delete_sticker,
            "/api/stickers/save": self._save_sticker,
            "/api/stickers/share": self._share_stickers,
            "/api/albums": self._create_album,
            "/api/fed/code": self._fed_code,
            "/api/fed/connect": self._fed_connect,
        }
        if url.path in routes:
            return routes[url.path](data)
        m = re.fullmatch(r"/api/albums/(\d+)/(rename|delete|add|remove)", url.path)
        if m:
            return self._album_action(int(m.group(1)), m.group(2), data)
        m = re.fullmatch(r"/api/contacts/(\d+)/(update|invite|cancel-invite|approve|logout-devices|delete|unlink|peer-retry)",
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
                **self._owner_profile(),
                "username": self.app.store.owner_username(),
                "public_url": self.app.store.get_setting("public_url"),
                "tunnel": self.app.store.get_setting("public_url_auto"),
                "tunnel_note": self.app.store.get_setting("tunnel_note"),
                "turn_url": self.app.store.get_setting("turn_url"),
                "turn_user": self.app.store.get_setting("turn_user"),
            }, headers=refresh)
        contact = self.app.store.get_contact(sess["contact_id"])
        return self._json({
            "role": "guest",
            "owner_name": self.app.owner_name,
            **self._owner_profile(),
            "username": contact["username"],
            "contact": {"id": contact["id"], "name": contact["name"], "status": contact["status"],
                        "owner_read_id": contact["owner_read_id"], "has_password": contact["has_password"],
                        "status_msg": contact["status_msg"], "avatar": contact["avatar"]},
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
        sender = "me" if sess["role"] == "owner" else "them"
        kind = str(data.get("kind") or "text")
        if kind not in MESSAGE_KINDS:
            raise ApiError(400, "訊息類型錯誤")
        body = str(data.get("body", "")).strip()
        att_id = None
        client_id = self._clean(data.get("client_id"), 64) or None
        if sess["role"] == "guest" and not self.app.messages.take(sess["id"]):
            raise ApiError(429, "訊息傳太快了,休息一下")
        if kind == "sticker":
            # 傳貼圖:複製一份成為這個對話的附件(之後刪掉貼圖,聊天紀錄裡的還在)
            existing = self.app.store.find_message(contact["id"], client_id) if client_id else None
            if existing:
                return self._json({"message": existing})
            st = self._my_sticker(sess, str(data.get("sticker_id", "")))
            att_id = secrets.token_urlsafe(18)
            shutil.copyfile(self.app.store.sticker_path(st["id"]), self.app.store.file_path(att_id))
            ext = st["mime"].split("/")[1].replace("jpeg", "jpg")
            self.app.store.add_attachment(att_id, contact["id"], sender, f"貼圖.{ext}", st["mime"], st["size"])
            body = ""
        elif kind == "text":
            if not body:
                raise ApiError(400, "訊息是空的")
        else:
            att = self.app.store.get_attachment(str(data.get("attachment_id", "")))
            # 只能用自己在這個對話上傳的檔案
            if not att or att["contact_id"] != contact["id"] or att["uploader"] != sender:
                raise ApiError(400, "找不到上傳的檔案,請重新傳送")
            att_id = att["id"]
            if (kind == "image" and not att["mime"].startswith("image/")) or \
               (kind == "audio" and not att["mime"].startswith(("audio/", "video/"))):
                kind = "file"
        if len(body) > MAX_MESSAGE:
            raise ApiError(400, f"訊息太長(上限 {MAX_MESSAGE} 字)")
        msg, new = self.app.store.add_message(contact["id"], sender, body, client_id, kind, att_id)
        if new:
            # 自己送出的就算已讀
            self.app.store.mark_read(contact["id"], sess["role"], msg["id"])
            self.app.fed.on_new_message(msg)  # 有互通的話也送一份到對方 HomeChat
            self.app.hub.publish({"type": "message", "message": msg}, contact["id"])
            self.app.notify_message(msg)
        self._json({"message": msg})

    def _receive_body(self, dest: Path, length: int) -> None:
        """把 request body 存成檔案(先寫暫存檔,完整收到才改名)。"""
        tmp = dest.with_name(dest.name + ".part")
        remaining = length
        try:
            with open(tmp, "wb") as f:
                while remaining > 0:
                    chunk = self.rfile.read(min(65536, remaining))
                    if not chunk:
                        raise ApiError(400, "上傳中斷了,請再試一次")
                    f.write(chunk)
                    remaining -= len(chunk)
            os.replace(tmp, dest)
        finally:
            if tmp.exists():
                tmp.unlink()

    # --- 貼圖
    def _sticker_owner(self, sess: dict) -> tuple[str, int | None]:
        return ("owner", None) if sess["role"] == "owner" else ("guest", sess["contact_id"])

    def _my_sticker(self, sess: dict, sid: str) -> dict:
        st = self.app.store.get_sticker(sid)
        if not st or (st["role"], st["contact_id"]) != self._sticker_owner(sess):
            raise ApiError(404, "找不到這張貼圖")
        return st

    def _stickers(self, qs: dict) -> None:
        sess = self._require()
        self._json({"stickers": self.app.store.list_stickers(*self._sticker_owner(sess))})

    def _sticker_file(self, sid: str, qs: dict | None = None) -> None:
        """自己的貼圖只有自己拿得到(傳出去時會另外複製一份給對話)。"""
        sess = self._require()
        st = self._my_sticker(sess, sid)
        path = self.app.store.sticker_path(sid)
        if not path.exists():
            raise ApiError(404, "找不到這張貼圖")
        headers = {"Content-Security-Policy": FILE_CSP, "Cache-Control": "private, max-age=31536000, immutable"}
        if qs and "download" in qs:  # 存到自己的裝置
            ext = st["mime"].split("/")[1].replace("jpeg", "jpg")
            headers["Content-Disposition"] = content_disposition("attachment", f"sticker_{sid[:6]}.{ext}")
        self._send(200, path.read_bytes(), st["mime"], headers)

    def _send_zip(self, filename: str, entries: list[tuple[str, Path]]) -> None:
        """把多個檔案打包成 zip 下載(照片本來就壓縮過,用 stored 比較快)。"""
        used: set[str] = set()
        with tempfile.TemporaryFile() as tmp:
            with zipfile.ZipFile(tmp, "w", zipfile.ZIP_STORED) as zf:
                for name, path in entries:
                    if not path.exists():
                        continue
                    stem, dot, ext = name.rpartition(".")
                    unique, n = name, 1
                    while unique.lower() in used:
                        n += 1
                        unique = f"{stem}_{n}.{ext}" if dot else f"{name}_{n}"
                    used.add(unique.lower())
                    zf.write(path, unique)
            size = tmp.tell()
            tmp.seek(0)
            self.send_response(200)
            self.send_header("Content-Type", "application/zip")
            self.send_header("Content-Length", str(size))
            self.send_header("Content-Disposition", content_disposition("attachment", filename))
            self.send_header("Cache-Control", "no-store")
            self.send_header("X-Content-Type-Options", "nosniff")
            self.end_headers()
            shutil.copyfileobj(tmp, self.wfile, 65536)

    def _stickers_zip(self, qs: dict) -> None:
        sess = self._require()
        ids = [i for i in qs.get("ids", "").split(",") if i][:MAX_STICKERS]
        entries = []
        for n, sid in enumerate(ids, 1):
            st = self._my_sticker(sess, sid)
            ext = st["mime"].split("/")[1].replace("jpeg", "jpg")
            entries.append((f"貼圖_{n:03d}.{ext}", self.app.store.sticker_path(sid)))
        if not entries:
            raise ApiError(400, "沒有選貼圖")
        self._send_zip(f"HomeChat貼圖_{datetime.now():%Y%m%d}.zip", entries)

    def _share_stickers(self, data: dict) -> None:
        """把自己的幾張貼圖打包成一則「貼圖組」訊息,對方可以一次全部收下。"""
        sess = self._require()
        contact = self._contact_for(sess, data.get("contact_id"))
        if contact["status"] != "active":
            raise ApiError(403, "請先接受好友申請")
        ids = list(dict.fromkeys(str(i) for i in (data.get("sticker_ids") or [])))
        if not ids:
            raise ApiError(400, "沒有選貼圖")
        if len(ids) > MAX_SHARE_STICKERS:
            raise ApiError(400, f"一次最多分享 {MAX_SHARE_STICKERS} 張")
        client_id = self._clean(data.get("client_id"), 64) or None
        if client_id:
            existing = self.app.store.find_message(contact["id"], client_id)
            if existing:
                return self._json({"message": existing})
        if sess["role"] == "guest" and not self.app.messages.take(sess["id"]):
            raise ApiError(429, "訊息傳太快了,休息一下")
        stickers = [self._my_sticker(sess, sid) for sid in ids]
        sender = "me" if sess["role"] == "owner" else "them"
        items = []
        for st in stickers:
            att_id = secrets.token_urlsafe(18)
            shutil.copyfile(self.app.store.sticker_path(st["id"]), self.app.store.file_path(att_id))
            ext = st["mime"].split("/")[1].replace("jpeg", "jpg")
            self.app.store.add_attachment(att_id, contact["id"], sender, f"貼圖.{ext}", st["mime"], st["size"])
            items.append({"id": att_id, "mime": st["mime"]})
        body = json.dumps({"count": len(items), "items": items}, ensure_ascii=False)
        msg = self._publish_message(contact, sess, "stickers", body, items[0]["id"], client_id)
        self.app.store.add_message_files(msg["id"], [i["id"] for i in items])
        self._json({"message": msg})

    def _publish_message(self, contact: dict, sess: dict, kind: str, body: str,
                         att_id: str | None, client_id: str | None = None) -> dict:
        sender = "me" if sess["role"] == "owner" else "them"
        msg, new = self.app.store.add_message(contact["id"], sender, body, client_id, kind, att_id)
        if new:
            self.app.store.mark_read(contact["id"], sess["role"], msg["id"])
            self.app.fed.on_new_message(msg)
            self.app.hub.publish({"type": "message", "message": msg}, contact["id"])
            self.app.notify_message(msg)
        return msg

    # --- 共同相簿
    def _album_for(self, sess: dict, album_id: int) -> dict:
        album = self.app.store.get_album(album_id)
        if not album or (sess["role"] == "guest" and album["contact_id"] != sess["contact_id"]):
            raise ApiError(404, "找不到這本相簿")
        return album

    def _albums(self, qs: dict) -> None:
        sess = self._require()
        contact = self._contact_for(sess, qs.get("contact"))
        self._json({"albums": self.app.store.list_albums(contact["id"])})

    def _create_album(self, data: dict) -> None:
        sess = self._require()
        contact = self._contact_for(sess, data.get("contact_id"))
        if contact["status"] != "active":
            raise ApiError(403, "請先接受好友申請")
        name = self._clean(data.get("name"), 40)
        if not name:
            raise ApiError(400, "請輸入相簿名稱")
        if len(self.app.store.list_albums(contact["id"])) >= MAX_ALBUMS:
            raise ApiError(409, f"相簿已經有 {MAX_ALBUMS} 本了")
        album = self.app.store.add_album(contact["id"], name, "me" if sess["role"] == "owner" else "them")
        self.app.hub.publish({"type": "album", "contact_id": contact["id"]}, contact["id"])
        self._json({"album": album})

    def _album_photos(self, album: dict) -> None:
        self._json({"album": album, "photos": self.app.store.album_photos(album["id"])})

    def _album_zip(self, album: dict) -> None:
        photos = self.app.store.album_photos(album["id"])
        entries = [(p["attachment"]["name"], self.app.store.file_path(p["attachment"]["id"])) for p in reversed(photos)]
        if not entries:
            raise ApiError(400, "相簿裡還沒有照片")
        self._send_zip(f"{album['name']}.zip", entries)

    def _album_action(self, album_id: int, action: str, data: dict) -> None:
        sess = self._require()
        album = self._album_for(sess, album_id)
        me = "me" if sess["role"] == "owner" else "them"
        store = self.app.store
        if action == "rename":
            name = self._clean(data.get("name"), 40)
            if not name:
                raise ApiError(400, "請輸入相簿名稱")
            store.rename_album(album_id, name)
        elif action == "delete":
            if sess["role"] != "owner" and album["created_by"] != me:
                raise ApiError(403, "只有建立相簿的人可以刪除")
            store.delete_album(album_id)
        elif action == "remove":
            try:
                photo = store.get_album_photo(int(data.get("photo_id", 0)))
            except (TypeError, ValueError):
                photo = None
            if not photo or photo["album_id"] != album_id:
                raise ApiError(404, "找不到這張照片")
            if sess["role"] != "owner" and photo["added_by"] != me:
                raise ApiError(403, "只能移除自己加的照片")
            store.remove_album_photo(photo["id"])
        elif action == "add":
            ids = [str(i) for i in (data.get("attachment_ids") or [])][:100]
            valid = []
            for att_id in ids:
                att = store.get_attachment(att_id)
                # 只能加這段對話裡的照片 / 影片
                if att and att["contact_id"] == album["contact_id"] and att["mime"].startswith(("image/", "video/")):
                    valid.append(att_id)
            if not valid:
                raise ApiError(400, "沒有可以加的照片")
            if len(store.album_photos(album_id)) + len(valid) > MAX_ALBUM_PHOTOS:
                raise ApiError(409, f"一本相簿最多 {MAX_ALBUM_PHOTOS} 張")
            added = store.add_album_photos(album_id, valid, me)
            if added and data.get("notify", True):
                # 在聊天室留一張卡片:「新增了 N 張照片到相簿」
                contact = store.get_contact(album["contact_id"])
                body = json.dumps({"album_id": album_id, "name": album["name"], "count": added}, ensure_ascii=False)
                self._publish_message(contact, sess, "album", body, valid[0])
            self.app.hub.publish({"type": "album", "contact_id": album["contact_id"]}, album["contact_id"])
            return self._json({"added": added})
        self.app.hub.publish({"type": "album", "contact_id": album["contact_id"]}, album["contact_id"])
        self._json({"ok": True})

    def _check_sticker_room(self, sess: dict) -> None:
        if len(self.app.store.list_stickers(*self._sticker_owner(sess))) >= MAX_STICKERS:
            raise ApiError(409, f"貼圖已經有 {MAX_STICKERS} 張了,先刪掉一些吧")

    def _upload_sticker(self) -> None:
        if self.headers.get("X-HomeChat-Upload") != "1":
            raise ApiError(403, "上傳格式錯誤")
        sess = self._require()
        mime = self.headers.get("Content-Type", "").split(";")[0].strip().lower()
        if mime not in STICKER_TYPES:
            raise ApiError(415, "貼圖要是 PNG、GIF、WebP 或 JPG 圖片")
        try:
            length = int(self.headers.get("Content-Length", ""))
        except ValueError:
            raise ApiError(411, "需要 Content-Length")
        if length <= 0:
            raise ApiError(400, "檔案是空的")
        if length > MAX_STICKER:
            raise ApiError(413, "貼圖太大了(上限 2 MB),做小一點或少幾格")
        if sess["role"] == "guest" and not self.app.uploads.take(sess["id"]):
            raise ApiError(429, "傳太多檔案了,晚點再試")
        self._check_sticker_room(sess)
        sid = secrets.token_urlsafe(18)
        self._receive_body(self.app.store.sticker_path(sid), length)
        st = self.app.store.add_sticker(sid, *self._sticker_owner(sess), mime, length)
        self._json({"sticker": {k: st[k] for k in ("id", "mime", "size", "created_at")}})

    def _delete_sticker(self, data: dict) -> None:
        sess = self._require()
        st = self._my_sticker(sess, str(data.get("id", "")))
        self.app.store.delete_sticker(st["id"])
        self._json({"ok": True})

    def _save_sticker(self, data: dict) -> None:
        """把對話裡收到的貼圖 / 圖片加到自己的貼圖。attachment_ids 可以一次收下整組。"""
        sess = self._require()
        many = "attachment_ids" in data
        ids = [str(i) for i in (data.get("attachment_ids") or [])][:MAX_SHARE_STICKERS] if many \
            else [str(data.get("attachment_id", ""))]
        saved = []
        for att_id in ids:
            att = self.app.store.get_attachment(att_id)
            if not att or (sess["role"] == "guest" and att["contact_id"] != sess["contact_id"]):
                raise ApiError(404, "找不到這張圖")
            if att["mime"] not in STICKER_TYPES or att["size"] > MAX_STICKER:
                raise ApiError(415, "這個檔案不能當貼圖(要是 2 MB 以下的圖片)")
            self._check_sticker_room(sess)
            sid = secrets.token_urlsafe(18)
            shutil.copyfile(self.app.store.file_path(att["id"]), self.app.store.sticker_path(sid))
            st = self.app.store.add_sticker(sid, *self._sticker_owner(sess), att["mime"], att["size"])
            saved.append({k: st[k] for k in ("id", "mime", "size", "created_at")})
        self._json({"stickers": saved} if many else {"sticker": saved[0]})

    def _upload(self, qs: dict) -> None:
        """上傳圖片 / 檔案 / 語音。內容直接放在 request body(不是 JSON)。"""
        if self.headers.get("X-HomeChat-Upload") != "1":
            # 自訂標頭:其他網站的表單送不出來(防 CSRF)
            raise ApiError(403, "上傳格式錯誤")
        sess = self._require()
        contact = self._contact_for(sess, qs.get("contact"))
        if contact["status"] != "active":
            raise ApiError(403, "請先接受好友申請")
        try:
            length = int(self.headers.get("Content-Length", ""))
        except ValueError:
            raise ApiError(411, "需要 Content-Length")
        if length <= 0:
            raise ApiError(400, "檔案是空的")
        if length > MAX_UPLOAD:
            raise ApiError(413, f"檔案太大了(上限 {MAX_UPLOAD // 1024 // 1024} MB)")
        if sess["role"] == "guest" and not self.app.uploads.take(sess["id"]):
            raise ApiError(429, "傳太多檔案了,晚點再試")
        store = self.app.store
        if shutil.disk_usage(store.files_dir).free < length + MIN_FREE_DISK:
            raise ApiError(507, "家裡電腦的硬碟快滿了,沒辦法再收檔案")
        mime = self.headers.get("Content-Type", "").split(";")[0].strip().lower()
        if not re.fullmatch(r"[a-z0-9.+-]+/[a-z0-9.+-]+", mime):
            mime = "application/octet-stream"
        name = re.sub(r'[\\/:*?"<>|]', "_", self._clean(unquote(qs.get("name", "")), 200)) or "檔案"
        att_id = secrets.token_urlsafe(18)
        self._receive_body(store.file_path(att_id), length)
        uploader = "me" if sess["role"] == "owner" else "them"
        att = store.add_attachment(att_id, contact["id"], uploader, name, mime, length)
        self._json({"attachment": {k: att[k] for k in ("id", "name", "mime", "size")}})

    def _file(self, att_id: str, qs: dict) -> None:
        """下載 / 顯示附件。只有這個對話的兩個人拿得到;支援 Range(iPhone 播放語音需要)。"""
        sess = self._require()
        att = self.app.store.get_attachment(att_id)
        if not att or (sess["role"] == "guest" and att["contact_id"] != sess["contact_id"]):
            raise ApiError(404, "找不到檔案")
        path = self.app.store.file_path(att_id)
        if not path.exists():
            raise ApiError(404, "檔案已經不在了")
        size = path.stat().st_size
        inline = att["mime"] in INLINE_TYPES and "download" not in qs
        start, end = 0, size - 1
        status = 200
        rng = self.headers.get("Range", "")
        m = re.fullmatch(r"bytes=(\d*)-(\d*)", rng.strip())
        if m and (m.group(1) or m.group(2)):
            if m.group(1):
                start = int(m.group(1))
                end = min(int(m.group(2)), size - 1) if m.group(2) else size - 1
            else:  # 最後 N 個 byte
                start = max(0, size - int(m.group(2)))
            if start > end or start >= size:
                self.send_response(416)
                self.send_header("Content-Range", f"bytes */{size}")
                self.send_header("Content-Length", "0")
                self.end_headers()
                return
            status = 206
        self.send_response(status)
        self.send_header("Content-Type", att["mime"] if inline else "application/octet-stream")
        self.send_header("Content-Length", str(end - start + 1))
        self.send_header("Accept-Ranges", "bytes")
        if status == 206:
            self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.send_header("Content-Disposition", content_disposition("inline" if inline else "attachment", att["name"]))
        self.send_header("Content-Security-Policy", FILE_CSP)
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Cache-Control", "private, max-age=31536000, immutable")  # 內容不會變,裝置可以一直留著
        self.send_header("X-HomeChat-Version", VERSION)
        self.end_headers()
        if self.command == "HEAD":
            return
        with open(path, "rb") as f:
            f.seek(start)
            remaining = end - start + 1
            while remaining > 0:
                chunk = f.read(min(65536, remaining))
                if not chunk:
                    break
                self.wfile.write(chunk)
                remaining -= len(chunk)

    def _read(self, data: dict) -> None:
        sess = self._require()
        contact = self._contact_for(sess, data.get("contact_id"))
        try:
            upto = int(data.get("upto", 0))
        except (TypeError, ValueError):
            raise ApiError(400, "upto 錯誤")
        before = self.app.store.get_contact(contact["id"])
        self.app.store.mark_read(contact["id"], sess["role"], upto)
        fresh = self.app.store.get_contact(contact["id"])
        col = "owner_read_id" if sess["role"] == "owner" else "guest_read_id"
        if fresh[col] > before[col]:
            self.app.fed.on_read(contact["id"], sess["role"], fresh[col])  # 已讀也同步給對方 HomeChat
        self.app.hub.publish({
            "type": "read", "contact_id": contact["id"],
            "owner_read_id": fresh["owner_read_id"], "guest_read_id": fresh["guest_read_id"],
        }, contact["id"])
        self._json({"ok": True})

    def _events(self, qs: dict) -> None:
        sess = self._require(active=False)  # 等待確認的朋友也要能收到「已接受」
        q = self.app.hub.subscribe(sess["role"], sess.get("contact_id"), sess["id"], qs.get("visible") != "0")
        try:
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream; charset=utf-8")
            self.send_header("Cache-Control", "no-cache, no-transform")
            self.send_header("X-Accel-Buffering", "no")
            self.send_header("Connection", "close")
            self.end_headers()
            self.close_connection = True
            hello = json.dumps({"type": "hello", "sub": q.token})  # type: ignore[attr-defined]
            self.wfile.write(f"retry: 3000\ndata: {hello}\n\n".encode())
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
            att = m["attachment"]
            if m["kind"] in ("album", "stickers"):
                try:
                    info = json.loads(body)
                except ValueError:
                    info = {}
                body = (f"[相簿] 新增 {info.get('count', 0)} 張照片到「{info.get('name', '')}」" if m["kind"] == "album"
                        else f"[分享貼圖] {info.get('count', 0)} 張")
            elif m["kind"] != "text":
                body = " ".join(x for x in (KIND_LABELS[m["kind"]], att["name"] if att else "", body) if x)
            if "\n" in body:
                body = f'"{body}"'
            lines.append(f"{dt:%H:%M}\t{who}\t{body}")
        self._send(200, "\n".join(lines).encode("utf-8"), "text/plain; charset=utf-8", {
            "Content-Disposition": content_disposition("attachment", f"HomeChat_{title}_{datetime.now():%Y%m%d}.txt"),
            "Cache-Control": "no-store",
        })

    @staticmethod
    def _clean(value, limit: int) -> str:
        # 去掉控制字元(換行以外)
        text = re.sub(r"[\x00-\x08\x0b-\x1f\x7f\u202a-\u202e\u2066-\u2069]", "", str(value or ""))
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

    # --- 互通:主人操作
    def _own_public_url(self) -> str:
        public = self.app.store.get_setting("public_url")
        if not public or not valid_peer_url(public):
            raise ApiError(400, "你的 HomeChat 還沒有 https 對外網址,請先設定 Tailscale(見 README)")
        return public

    def _fed_code(self, data: dict) -> None:
        """產生互通碼給朋友:朋友貼到他的 HomeChat 就會連上。"""
        sess = self._require("owner")
        public = self._own_public_url()
        cid = None
        if data.get("contact_id"):
            cid = self._contact_for(sess, data["contact_id"])["id"]
        token = self.app.store.create_pair_code(cid, self._clean(data.get("name"), 100))
        self._json({"code": encode_pair_code(public, token, self.app.owner_name), "expires_in": 86400})

    def _fed_connect(self, data: dict) -> None:
        """輸入朋友給的互通碼:連到他的 HomeChat。"""
        sess = self._require("owner")
        info = decode_pair_code(str(data.get("code", "")))
        if not info or not valid_peer_url(info["u"]):
            raise ApiError(400, "互通碼格式不對,請整段複製貼上(HC1. 開頭)")
        public = self._own_public_url()
        target = self._contact_for(sess, data["contact_id"]) if data.get("contact_id") else None
        store = self.app.store
        try:
            resp = fed_request(info["u"], "/fed/pair", timeout=20, payload={
                "token": info["t"], "url": public, "name": self.app.owner_name, "server_id": store.server_id()})
        except PeerError as e:
            raise ApiError(400, "互通碼已經用過或過期,請朋友重新產生一組" if e.status == 410 else f"對方拒絕:{e}")
        except OSError as e:
            raise ApiError(502, f"連不上對方的 HomeChat({e})。請確認對方電腦開著")
        if not all(isinstance(resp.get(k), str) and resp.get(k) for k in ("secret", "server_id")):
            raise ApiError(502, "對方的 HomeChat 版本太舊,請對方先更新")
        if resp["server_id"] == store.server_id():
            raise ApiError(400, "這是你自己的互通碼喔")
        peer_name = self._clean(resp.get("name") or info["n"], 100) or "朋友"
        # 跟同一台重新配對(例如對方重灌):沿用原本的聯絡人,不要多一個
        again = store.contact_by_peer(resp["server_id"])
        contact = target or (store.get_contact(again["id"]) if again else None) or store.add_contact(peer_name)
        store.link_peer(contact["id"], resp["server_id"], info["u"].rstrip("/"), resp["secret"], peer_name)
        self.app.fed.sync_history(contact["id"])
        self.app.hub.publish({"type": "contacts"}, contact["id"], to_guest=False)
        self._json({"contact": self._public_contact(store.get_contact(contact["id"]))})

    # --- 互通:其他 HomeChat 打過來的
    def _fed_pair(self) -> None:
        """對方輸入了我們的互通碼,來配對。用互通碼驗證(只能用一次)。"""
        key = self._client_key()
        if not self.app.pairs.take(key):
            raise ApiError(429, "嘗試太多次了,晚點再試")
        data = self._read_json()
        store = self.app.store
        url = str(data.get("url", "")).rstrip("/")
        server_id = self._clean(data.get("server_id"), 64)
        if not valid_peer_url(url) or not server_id:
            raise ApiError(400, "配對資料不完整")
        if server_id == store.server_id():
            raise ApiError(400, "不能跟自己配對")
        code = store.take_pair_code(str(data.get("token", "")))
        if not code:
            raise ApiError(410, "互通碼已經用過或過期")
        name = self._clean(data.get("name"), 100) or code["name"] or "朋友"
        contact = store.get_contact(code["contact_id"]) if code["contact_id"] else None
        again = store.contact_by_peer(server_id)
        if not contact and again:
            contact = store.get_contact(again["id"])  # 重新配對:沿用原本的聯絡人
        if not contact:
            contact = store.add_contact(code["name"] or name)
        secret = secrets.token_urlsafe(32)
        store.link_peer(contact["id"], server_id, url, secret, name)
        self.app.fed.sync_history(contact["id"], delay=3)
        self.app.hub.publish({"type": "peer_linked", "contact_id": contact["id"], "name": name},
                             contact["id"], to_guest=False)
        self._json({"server_id": store.server_id(), "secret": secret, "name": self.app.owner_name})

    def _fed_peer(self) -> tuple[dict, str]:
        """檢查對方 HomeChat 的簽章,回傳 (對方, 內容的 SHA-256)。"""
        peer_id = self.headers.get("X-HC-Peer", "")
        ts = self.headers.get("X-HC-Time", "")
        body_hash = self.headers.get("X-HC-Body", "")
        try:
            skew = abs(time.time() - int(ts))
        except ValueError:
            raise ApiError(401, "缺少簽章")
        if skew > FED_MAX_SKEW:
            raise ApiError(401, "兩台電腦的時間差太多,請確認電腦時間正確")
        peer = self.app.store.contact_by_peer(peer_id) if peer_id else None
        if not peer:
            raise ApiError(401, "不認得這台 HomeChat(可能已經解除互通)")
        expected = fed_sign(peer["peer_secret"], ts, "POST", self.path, body_hash)
        if not hmac.compare_digest(expected, self.headers.get("X-HC-Sig", "")):
            raise ApiError(401, "簽章不符")
        return peer, body_hash

    def _fed_inbox(self) -> None:
        peer, body_hash = self._fed_peer()
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            raise ApiError(400, "Content-Length 錯誤")
        if length > 1024 * 1024:
            raise ApiError(413, "內容太大")
        raw = self.rfile.read(length)
        if not hmac.compare_digest(hashlib.sha256(raw).hexdigest(), body_hash):
            raise ApiError(401, "內容和簽章不符")
        try:
            data = json.loads(raw)
        except ValueError:
            raise ApiError(400, "JSON 格式錯誤")
        store, hub, cid = self.app.store, self.app.hub, peer["id"]
        kind_of = data.get("type")
        if kind_of == "message":
            uid = self._clean(data.get("uid"), 64)
            if not uid:
                raise ApiError(400, "缺少訊息編號")
            # 對方的「me」是我們的「them」
            sender = "them" if data.get("sender") == "me" else "me"
            kind = data.get("kind") if data.get("kind") in MESSAGE_KINDS else "text"
            body = str(data.get("body", ""))[:MAX_MESSAGE]
            try:
                created = float(data.get("created_at"))
            except (TypeError, ValueError):
                created = time.time()
            if not 0 < created < time.time() + 86400:
                created = time.time()
            source = data.get("source") if data.get("source") in ("chat", "line") else "chat"
            att_id = None
            meta = data.get("attachment")
            if kind != "text":
                att = store.get_attachment(str((meta or {}).get("id", "")))
                if not att or att["contact_id"] != cid:
                    raise ApiError(409, "檔案還沒收到")  # 對方會重送(先傳檔案再傳訊息)
                att_id = att["id"]
            msg, new = store.add_message(cid, sender, body, None, kind, att_id, uid=uid, created_at=created,
                                         from_peer=True, source=source)
            if new:
                if data.get("history") and sender == "them":
                    store.mark_read(cid, "owner", msg["id"])  # 配對時複製過來的舊訊息不算未讀
                hub.publish({"type": "message", "message": msg}, cid)
                if sender == "them" and not data.get("history"):
                    self.app.notify_message(msg)
            return self._json({"ok": True, "id": msg["id"]})
        if kind_of == "read":
            m = store.message_by_uid(cid, self._clean(data.get("uid"), 64))
            if m:
                # 對方主人讀了 = 我們這邊的「朋友」讀了
                role = "guest" if data.get("reader") == "owner" else "owner"
                store.mark_read(cid, role, m["id"])
                fresh = store.get_contact(cid)
                hub.publish({"type": "read", "contact_id": cid, "owner_read_id": fresh["owner_read_id"],
                             "guest_read_id": fresh["guest_read_id"]}, cid)
            return self._json({"ok": True})
        if kind_of == "profile":
            ver = str(data.get("avatar") or "")
            ver = ver if re.fullmatch(r"[A-Za-z0-9_-]{1,32}", ver) else ""
            if not ver:
                store.avatar_path(f"c{cid}").unlink(missing_ok=True)
            elif not store.avatar_path(f"c{cid}").exists():
                ver = ""  # 圖還沒收到(照理說會先傳圖)
            store.set_contact_profile(cid, status_msg=self._clean(data.get("status"), MAX_STATUS), avatar=ver)
            hub.publish({"type": "contacts"}, cid, to_guest=False)
            return self._json({"ok": True})
        if kind_of == "unpair":
            store.unlink_peer(cid)  # 對話紀錄留著
            hub.publish({"type": "contacts"}, cid, to_guest=False)
            return self._json({"ok": True})
        raise ApiError(400, "不支援的類型")

    def _fed_avatar(self) -> None:
        """收對方主人的大頭貼(接著會收到 profile,裡面有版本號)。"""
        peer, body_hash = self._fed_peer()
        try:
            length = int(self.headers.get("Content-Length", ""))
        except ValueError:
            raise ApiError(411, "需要 Content-Length")
        if not 0 < length <= MAX_AVATAR:
            raise ApiError(413, "大頭貼太大")
        raw = self.rfile.read(length)
        if not hmac.compare_digest(hashlib.sha256(raw).hexdigest(), body_hash):
            raise ApiError(401, "內容和簽章不符")
        if not image_type(raw[:16]):
            raise ApiError(415, "大頭貼要是圖片")
        self._write_avatar(f"c{peer['id']}", raw)
        self._json({"ok": True})

    def _fed_file(self, att_id: str, qs: dict) -> None:
        """收對方傳來的圖片 / 檔案(訊息之前先傳)。內容的 SHA-256 在簽章裡,收完會核對。"""
        peer, body_hash = self._fed_peer()
        store = self.app.store
        try:
            length = int(self.headers.get("Content-Length", ""))
        except ValueError:
            raise ApiError(411, "需要 Content-Length")
        if not 0 < length <= MAX_UPLOAD:
            raise ApiError(413, "檔案太大")
        existing = store.get_attachment(att_id)
        if existing:
            if existing["contact_id"] != peer["id"]:
                raise ApiError(409, "檔案編號衝突")
            remaining = length  # 已經有了(重送):讀掉就好
            while remaining > 0:
                chunk = self.rfile.read(min(65536, remaining))
                if not chunk:
                    break
                remaining -= len(chunk)
            return self._json({"ok": True})
        if shutil.disk_usage(store.files_dir).free < length + MIN_FREE_DISK:
            raise ApiError(507, "硬碟快滿了")
        dest = store.file_path(att_id)
        tmp = dest.with_name(dest.name + ".part")
        h = hashlib.sha256()
        remaining = length
        try:
            with open(tmp, "wb") as f:
                while remaining > 0:
                    chunk = self.rfile.read(min(65536, remaining))
                    if not chunk:
                        raise ApiError(400, "傳到一半斷了")
                    h.update(chunk)
                    f.write(chunk)
                    remaining -= len(chunk)
            if not hmac.compare_digest(h.hexdigest(), body_hash):
                raise ApiError(401, "檔案內容和簽章不符")
            os.replace(tmp, dest)
        finally:
            if tmp.exists():
                tmp.unlink()
        mime = str(qs.get("mime", "")).lower()
        if not re.fullmatch(r"[a-z0-9.+-]+/[a-z0-9.+-]+", mime):
            mime = "application/octet-stream"
        name = re.sub(r'[\\/:*?"<>|]', "_", self._clean(qs.get("name"), 200)) or "檔案"
        uploader = "them" if qs.get("sender") == "me" else "me"
        store.add_attachment(att_id, peer["id"], uploader, name, mime, length)
        self._json({"ok": True})

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
        elif action == "unlink":
            self.app.fed.unpair(contact_id)  # 解除互通:已有的對話紀錄兩邊都保留
        elif action == "peer-retry":
            store.retry_now(contact_id)
            self.app.fed.wake.set()
        elif action == "delete":  # 也用來拒絕好友申請
            hub.kick_guests(contact_id)
            self.app.fed.unpair(contact_id)
            store.delete_contact(contact_id)
            hub.publish({"type": "contacts"}, contact_id, to_guest=False)
            return self._json({"ok": True})
        hub.publish({"type": "contacts"}, contact_id, to_guest=False)
        self._json({"contact": self._public_contact(store.get_contact(contact_id))})

    # --- 大頭貼和狀態消息
    def _owner_profile(self) -> dict:
        store = self.app.store
        return {"owner_status": store.get_setting("owner_status"), "owner_avatar": store.get_setting("owner_avatar")}

    def _write_avatar(self, key: str, raw: bytes) -> None:
        dest = self.app.store.avatar_path(key)
        tmp = dest.with_name(dest.name + ".part")
        tmp.write_bytes(raw)
        os.replace(tmp, dest)

    def _avatar(self, key: str) -> None:
        """主人看得到所有人的;朋友只看得到主人和自己的。"""
        sess = self._require()
        if sess["role"] == "guest" and key not in ("owner", str(sess["contact_id"])):
            raise ApiError(404, "找不到")
        path = self.app.store.avatar_path("owner" if key == "owner" else f"c{key}")
        if not path.exists():
            raise ApiError(404, "找不到")
        raw = path.read_bytes()
        # 網址裡有版本號(?v=),換照片網址就變,所以可以放心快取
        self._send(200, raw, image_type(raw[:16]) or "application/octet-stream",
                   {"Content-Security-Policy": FILE_CSP, "Cache-Control": "private, max-age=31536000, immutable"})

    def _profile_changed(self, sess: dict) -> None:
        if sess["role"] == "owner":
            self.app.hub.broadcast({"type": "profile"})
            self.app.fed.on_profile()
        else:
            self.app.hub.publish({"type": "profile", "contact_id": sess["contact_id"]}, sess["contact_id"])

    def _upload_avatar(self) -> None:
        """換大頭貼:request body 就是圖片(網頁已經裁成正方形、縮小)。"""
        if self.headers.get("X-HomeChat-Upload") != "1":
            raise ApiError(403, "上傳格式錯誤")
        sess = self._require()
        try:
            length = int(self.headers.get("Content-Length", ""))
        except ValueError:
            raise ApiError(411, "需要 Content-Length")
        if length <= 0:
            raise ApiError(400, "檔案是空的")
        if length > MAX_AVATAR:
            raise ApiError(413, "照片太大了(上限 1 MB)")
        if sess["role"] == "guest" and not self.app.uploads.take(sess["id"]):
            raise ApiError(429, "換太多次了,晚點再試")
        raw = self.rfile.read(length)
        if len(raw) != length:
            raise ApiError(400, "上傳中斷了,請再試一次")
        if not image_type(raw[:16]):
            raise ApiError(415, "大頭貼要是 JPG、PNG、WebP 或 GIF 圖片")
        ver = secrets.token_urlsafe(6)
        store = self.app.store
        if sess["role"] == "owner":
            self._write_avatar("owner", raw)
            store.set_setting("owner_avatar", ver)
        else:
            self._write_avatar(f"c{sess['contact_id']}", raw)
            store.set_contact_profile(sess["contact_id"], avatar=ver)
        self._profile_changed(sess)
        self._json({"avatar": ver})

    def _profile(self, data: dict) -> None:
        """改狀態消息 / 移除大頭貼。"""
        sess = self._require()
        store = self.app.store
        owner = sess["role"] == "owner"
        if "status_msg" in data:
            text = " ".join(str(data.get("status_msg") or "").split())[:MAX_STATUS]  # 一行就好
            if owner:
                store.set_setting("owner_status", text)
            else:
                store.set_contact_profile(sess["contact_id"], status_msg=text)
        if data.get("remove_avatar"):
            store.avatar_path("owner" if owner else f"c{sess['contact_id']}").unlink(missing_ok=True)
            if owner:
                store.set_setting("owner_avatar", "")
            else:
                store.set_contact_profile(sess["contact_id"], avatar="")
        self._profile_changed(sess)
        self._json({"ok": True})

    # --- 通知
    def _presence(self, data: dict) -> None:
        """網頁回報:正在看(就不推播,網頁自己提醒)/ 在背景(要推播)。"""
        sess = self._require(active=False)
        self.app.hub.set_visible(sess["id"], str(data.get("sub", "")), bool(data.get("visible")))
        self._json({"ok": True})

    def _push_key(self, qs: dict) -> None:
        self._require(active=False)
        self._json({"key": self.app.store.vapid()[1]})

    def _push_subscribe(self, data: dict) -> None:
        sess = self._require(active=False)
        endpoint = str((data.get("subscription") or {}).get("endpoint", ""))
        if not valid_push_endpoint(endpoint):
            raise ApiError(400, "這個瀏覽器的推播服務不支援")
        self.app.store.add_push_sub(endpoint, sess["id"], bool(data.get("vibrate", True)))
        self._json({"ok": True})

    def _push_unsubscribe(self, data: dict) -> None:
        sess = self._require(active=False)
        self.app.store.remove_push_sub(str(data.get("endpoint", "")), sess["id"])
        self._json({"ok": True})

    def _push_test(self, data: dict) -> None:
        sess = self._require(active=False)
        subs = self.app.store.push_subs_for(sess["role"], session_id=sess["id"])
        if not subs:
            raise ApiError(400, "這台裝置還沒開啟通知")
        self.app.store.set_setting(f"push_test:{sess['id']}", str(time.time()))
        self.app.pusher.push(sess["role"], session_id=sess["id"])
        self._json({"ok": True})

    def _notify_info(self, qs: dict) -> None:
        """手機被推播叫醒後來問:要顯示什麼通知。"""
        sess = self._require(active=False)
        store, role = self.app.store, sess["role"]
        cid = sess.get("contact_id")
        subs = store.push_subs_for(role, session_id=sess["id"])
        vibrate = any(s["vibrate"] for s in subs) if subs else True
        base = {"vibrate": vibrate, "icon": "/icon.svg"}
        # 來電
        for call in list(self.app.calls.calls.values()):
            if call["state"] == "ringing" and call["caller"] != role and (role == "owner" or call["contact_id"] == cid):
                c = store.get_contact(call["contact_id"])
                name = (c["name"] if c else "朋友") if role == "owner" else self.app.owner_name
                title, verb = ("📹 視訊來電", "想跟你視訊") if call.get("video") else ("📞 來電", "打電話給你")
                return self._json({**base, "title": title, "body": f"{name} {verb}", "tag": "hc-call",
                                   "contact_id": call["contact_id"], "call": call["id"]})
        test_at = store.get_setting(f"push_test:{sess['id']}")
        if test_at and time.time() - float(test_at) < 120:
            store.set_setting(f"push_test:{sess['id']}", "")
            return self._json({**base, "title": "HomeChat", "body": "🔔 通知測試成功!有新訊息時會像這樣跳出來",
                               "tag": "hc-test"})
        msg, count = store.latest_unread(role, cid)
        if not msg:
            return self._json({**base, "title": "HomeChat", "body": "有新訊息", "tag": "hc-new"})
        name = msg["name"] if role == "owner" else self.app.owner_name
        text = msg["body"] if msg["kind"] == "text" else KIND_LABELS.get(msg["kind"], "[訊息]")
        if msg["kind"] in ("image", "audio", "file") and msg["body"]:
            text = f"{text} {msg['body']}"
        if count > 1:
            text = f"{text}(共 {count} 則未讀)"
        self._json({**base, "title": name, "body": text[:200], "tag": f"hc-{msg['contact_id']}",
                    "contact_id": msg["contact_id"]})

    # --- 語音通話
    def _ice_servers(self) -> list[dict]:
        servers: list[dict] = [{"urls": ["stun:stun.l.google.com:19302", "stun:stun.cloudflare.com:3478"]}]
        store = self.app.store
        turn = store.get_setting("turn_url")
        if turn:
            servers.append({"urls": [u.strip() for u in turn.split(",") if u.strip()],
                            "username": store.get_setting("turn_user"), "credential": store.get_setting("turn_pass")})
        return servers

    def _my_call(self, sess: dict, call_id) -> dict:
        call = self.app.calls.find(str(call_id or ""))
        if not call or (sess["role"] == "guest" and call["contact_id"] != sess["contact_id"]):
            raise ApiError(404, "這通電話已經結束了")
        return call

    def _call_start(self, data: dict) -> None:
        sess = self._require()
        contact = self._contact_for(sess, data.get("contact_id"))
        if contact["status"] != "active":
            raise ApiError(400, "還不是好友")
        if sess["role"] == "owner" and not contact["username"] and not self.app.store.list_sessions("guest", contact["id"]):
            raise ApiError(400, "對方還沒加入,不能打電話" if not contact["peer"] else "互通的朋友目前還不能直接通話")
        call = self.app.calls.start(contact["id"], sess["role"], sess["id"], bool(data.get("video")))
        self._json({"call_id": call["id"], "ice_servers": self._ice_servers(), "ring_seconds": CALL_RING_SECONDS})

    def _call_answer(self, data: dict) -> None:
        sess = self._require()
        call = self._my_call(sess, data.get("call_id"))
        if call["caller"] == sess["role"]:
            raise ApiError(400, "不能接自己打的電話")
        self.app.calls.answer(call, sess["id"])
        self._json({"ok": True, "ice_servers": self._ice_servers()})

    def _call_signal(self, data: dict) -> None:
        sess = self._require()
        call = self._my_call(sess, data.get("call_id"))
        payload = data.get("data")
        if not isinstance(payload, dict) or len(json.dumps(payload)) > 20000:
            raise ApiError(400, "連線資料格式錯誤")
        self.app.calls.signal(call, sess["id"], payload)
        self._json({"ok": True})

    def _call_end(self, data: dict) -> None:
        sess = self._require()
        call = self._my_call(sess, data.get("call_id"))
        mine = call["caller"] == sess["role"]
        if call["state"] == "ringing":
            reason = "canceled" if mine else "declined"
        else:
            if sess["id"] not in (call["caller_sid"], call["callee_sid"]):
                raise ApiError(403, "不是這通電話")
            reason = "ended"
        self.app.calls.end(call["id"], reason)
        self._json({"ok": True})

    def _call_state(self, qs: dict) -> None:
        """從通知點進來時:有沒有正在響的電話。"""
        sess = self._require()
        for call in list(self.app.calls.calls.values()):
            if call["state"] == "ringing" and call["caller"] != sess["role"] and \
                    (sess["role"] == "owner" or call["contact_id"] == sess["contact_id"]):
                return self._json({"call": {"call_id": call["id"], "contact_id": call["contact_id"],
                                            "video": bool(call.get("video"))}})
        self._json({"call": None})

    def _settings(self, data: dict) -> None:
        self._require("owner")
        if "username" in data and str(data["username"]).strip().lower() != self.app.store.owner_username():
            self.app.store.set_setting("owner_username", self._check_new_username(str(data["username"])))
        if "owner_name" in data:
            name = self._clean(data["owner_name"], 50)
            if name:
                self.app.store.set_setting("owner_name", name)
        for key in ("turn_url", "turn_user", "turn_pass"):
            if key in data:
                value = self._clean(data[key], 500)
                if key == "turn_url" and value and not all(re.match(r"^turns?:[^\s]+$", u.strip())
                                                           for u in value.split(",")):
                    raise ApiError(400, "TURN 伺服器格式像 turn:example.com:3478")
                self.app.store.set_setting(key, value)
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

    try:
        store.cleanup_attachments()
    except OSError:
        pass
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
