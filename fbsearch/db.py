"""SQLite 資料庫:儲存、去重、全文搜尋與篩選。

全文索引使用 FTS5 的 trigram 分詞器,中文不需要斷詞也能搜尋;
少於 3 個字的關鍵字(例如「沙發」)自動退回 LIKE 搜尋。
"""

from __future__ import annotations

import hashlib
import json
import sqlite3
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable

from .price import extract_price

# 類型
KINDS = ("post", "video", "marketplace", "link", "photo", "other")
# 來源
SOURCES = ("viewed", "saved", "posted", "manual", "other")

SCHEMA = """
CREATE TABLE IF NOT EXISTS items (
    id          INTEGER PRIMARY KEY,
    uid         TEXT NOT NULL UNIQUE,
    kind        TEXT NOT NULL DEFAULT 'post',
    source      TEXT NOT NULL DEFAULT 'other',
    title       TEXT NOT NULL DEFAULT '',
    text        TEXT NOT NULL DEFAULT '',
    url         TEXT NOT NULL DEFAULT '',
    author      TEXT NOT NULL DEFAULT '',
    price       REAL,
    tags        TEXT NOT NULL DEFAULT '',
    note        TEXT NOT NULL DEFAULT '',
    ts          INTEGER NOT NULL DEFAULT 0,   -- 觀看 / 收藏 / 發文時間(epoch 秒)
    origin      TEXT NOT NULL DEFAULT '',     -- 從哪個匯出檔來的
    added_at    INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_items_ts ON items(ts DESC);
CREATE INDEX IF NOT EXISTS idx_items_price ON items(price);
CREATE INDEX IF NOT EXISTS idx_items_kind_source ON items(kind, source);

CREATE VIRTUAL TABLE IF NOT EXISTS items_fts USING fts5(
    title, text, author, tags, note,
    content='items', content_rowid='id', tokenize='trigram'
);
CREATE TRIGGER IF NOT EXISTS items_ai AFTER INSERT ON items BEGIN
    INSERT INTO items_fts(rowid, title, text, author, tags, note)
    VALUES (new.id, new.title, new.text, new.author, new.tags, new.note);
END;
CREATE TRIGGER IF NOT EXISTS items_ad AFTER DELETE ON items BEGIN
    INSERT INTO items_fts(items_fts, rowid, title, text, author, tags, note)
    VALUES ('delete', old.id, old.title, old.text, old.author, old.tags, old.note);
END;
CREATE TRIGGER IF NOT EXISTS items_au AFTER UPDATE ON items BEGIN
    INSERT INTO items_fts(items_fts, rowid, title, text, author, tags, note)
    VALUES ('delete', old.id, old.title, old.text, old.author, old.tags, old.note);
    INSERT INTO items_fts(rowid, title, text, author, tags, note)
    VALUES (new.id, new.title, new.text, new.author, new.tags, new.note);
END;
"""

COLUMNS = ("id", "uid", "kind", "source", "title", "text", "url", "author",
           "price", "tags", "note", "ts", "origin", "added_at")


@dataclass
class Item:
    kind: str = "post"
    source: str = "other"
    title: str = ""
    text: str = ""
    url: str = ""
    author: str = ""
    price: float | None = None
    tags: str = ""
    note: str = ""
    ts: int = 0
    origin: str = ""
    extra: dict[str, Any] = field(default_factory=dict)

    def make_uid(self) -> str:
        # 同一個網址 + 同一種來源視為同一筆(重複觀看只更新時間)
        if self.url:
            key = f"{self.source}|{self.url}"
        else:
            key = f"{self.source}|{self.ts}|{self.title}|{self.text[:200]}"
        return hashlib.sha1(key.encode("utf-8")).hexdigest()


@dataclass
class Query:
    q: str = ""
    kind: str = ""            # 逗號分隔可多選
    source: str = ""          # 逗號分隔可多選
    price_min: float | None = None
    price_max: float | None = None
    has_price: bool = False
    date_from: int | None = None   # epoch 秒
    date_to: int | None = None
    sort: str = "recent"      # recent | oldest | price_asc | price_desc | relevance
    limit: int = 50
    offset: int = 0


class Store:
    def __init__(self, path: str | Path = "fbsearch.db"):
        self.path = str(path)
        self.conn = sqlite3.connect(self.path, check_same_thread=False)
        self.conn.row_factory = sqlite3.Row
        self.conn.executescript(SCHEMA)

    def close(self) -> None:
        self.conn.close()

    # ------------------------------------------------------------------ 寫入
    def upsert(self, item: Item) -> bool:
        """新增一筆;已存在則更新時間(取較新)與補齊欄位。回傳是否為新資料。"""
        if item.kind not in KINDS:
            item.kind = "other"
        if item.source not in SOURCES:
            item.source = "other"
        if item.price is None:
            item.price = extract_price(f"{item.title}\n{item.text}")
        uid = item.make_uid()
        cur = self.conn.execute("SELECT id, ts FROM items WHERE uid = ?", (uid,))
        row = cur.fetchone()
        if row is None:
            self.conn.execute(
                "INSERT INTO items (uid, kind, source, title, text, url, author, price,"
                " tags, note, ts, origin, added_at) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)",
                (uid, item.kind, item.source, item.title, item.text, item.url,
                 item.author, item.price, item.tags, item.note, int(item.ts or 0),
                 item.origin, int(time.time())),
            )
            return True
        self.conn.execute(
            "UPDATE items SET ts = MAX(ts, ?),"
            " title = CASE WHEN title = '' THEN ? ELSE title END,"
            " text = CASE WHEN length(text) < length(?) THEN ? ELSE text END,"
            " author = CASE WHEN author = '' THEN ? ELSE author END,"
            " price = COALESCE(price, ?)"
            " WHERE id = ?",
            (int(item.ts or 0), item.title, item.text, item.text, item.author,
             item.price, row["id"]),
        )
        return False

    def upsert_many(self, items: Iterable[Item]) -> tuple[int, int]:
        """回傳 (新增筆數, 已存在筆數)。"""
        new = dup = 0
        with self.conn:
            for it in items:
                if self.upsert(it):
                    new += 1
                else:
                    dup += 1
        return new, dup

    def add(self, item: Item) -> dict:
        with self.conn:
            self.upsert(item)
        row = self.conn.execute("SELECT * FROM items WHERE uid = ?", (item.make_uid(),)).fetchone()
        return dict(row)

    def update(self, item_id: int, **fields: Any) -> dict | None:
        allowed = {"title", "text", "tags", "note", "price", "kind", "source", "author", "url"}
        sets = {k: v for k, v in fields.items() if k in allowed}
        if sets:
            cols = ", ".join(f"{k} = ?" for k in sets)
            with self.conn:
                self.conn.execute(f"UPDATE items SET {cols} WHERE id = ?", (*sets.values(), item_id))
        return self.get(item_id)

    def delete(self, item_id: int) -> bool:
        with self.conn:
            cur = self.conn.execute("DELETE FROM items WHERE id = ?", (item_id,))
        return cur.rowcount > 0

    def get(self, item_id: int) -> dict | None:
        row = self.conn.execute("SELECT * FROM items WHERE id = ?", (item_id,)).fetchone()
        return dict(row) if row else None

    # ------------------------------------------------------------------ 搜尋
    def search(self, qry: Query) -> dict:
        where: list[str] = []
        params: list[Any] = []
        use_fts = False

        include, exclude = _parse_terms(qry.q)
        long_terms = [t for t in include if len(t) >= 3]
        short_terms = [t for t in include if len(t) < 3]
        if long_terms:
            use_fts = True
            where.append("items.id IN (SELECT rowid FROM items_fts WHERE items_fts MATCH ?)")
            params.append(" AND ".join(_fts_quote(t) for t in long_terms))
        for t in short_terms:
            where.append("(items.title || ' ' || items.text || ' ' || items.author"
                         " || ' ' || items.tags || ' ' || items.note) LIKE ? ESCAPE '\\'")
            params.append(f"%{_like_escape(t)}%")
        for t in exclude:
            where.append("(items.title || ' ' || items.text || ' ' || items.author"
                         " || ' ' || items.tags || ' ' || items.note) NOT LIKE ? ESCAPE '\\'")
            params.append(f"%{_like_escape(t)}%")

        for col, raw in (("kind", qry.kind), ("source", qry.source)):
            vals = [v.strip() for v in (raw or "").split(",") if v.strip()]
            if vals:
                where.append(f"items.{col} IN ({','.join('?' * len(vals))})")
                params.extend(vals)

        if qry.price_min is not None:
            where.append("items.price >= ?")
            params.append(qry.price_min)
        if qry.price_max is not None:
            where.append("items.price <= ?")
            params.append(qry.price_max)
        if qry.has_price:
            where.append("items.price IS NOT NULL")
        if qry.date_from is not None:
            where.append("items.ts >= ?")
            params.append(qry.date_from)
        if qry.date_to is not None:
            where.append("items.ts <= ?")
            params.append(qry.date_to)

        where_sql = ("WHERE " + " AND ".join(where)) if where else ""
        order = {
            "recent": "items.ts DESC, items.id DESC",
            "oldest": "items.ts ASC, items.id ASC",
            "price_asc": "items.price IS NULL, items.price ASC, items.ts DESC",
            "price_desc": "items.price IS NULL, items.price DESC, items.ts DESC",
        }.get(qry.sort)
        if qry.sort == "relevance" and use_fts:
            sql = (
                "SELECT items.* FROM items JOIN items_fts ON items_fts.rowid = items.id"
                f" {where_sql} {'AND' if where_sql else 'WHERE'} items_fts MATCH ?"
                " ORDER BY bm25(items_fts), items.ts DESC LIMIT ? OFFSET ?"
            )
            run_params = [*params, params[0], qry.limit, qry.offset]
        else:
            sql = (f"SELECT items.* FROM items {where_sql}"
                   f" ORDER BY {order or 'items.ts DESC'} LIMIT ? OFFSET ?")
            run_params = [*params, qry.limit, qry.offset]

        total = self.conn.execute(f"SELECT COUNT(*) FROM items {where_sql}", params).fetchone()[0]
        rows = [dict(r) for r in self.conn.execute(sql, run_params)]
        return {"total": total, "items": rows, "limit": qry.limit, "offset": qry.offset}

    def stats(self) -> dict:
        c = self.conn
        return {
            "total": c.execute("SELECT COUNT(*) FROM items").fetchone()[0],
            "with_price": c.execute("SELECT COUNT(*) FROM items WHERE price IS NOT NULL").fetchone()[0],
            "by_kind": {r[0]: r[1] for r in c.execute("SELECT kind, COUNT(*) FROM items GROUP BY kind")},
            "by_source": {r[0]: r[1] for r in c.execute("SELECT source, COUNT(*) FROM items GROUP BY source")},
            "range": dict(zip(("min_ts", "max_ts"),
                              c.execute("SELECT MIN(ts), MAX(ts) FROM items WHERE ts > 0").fetchone())),
        }

    def export_json(self) -> str:
        rows = [dict(r) for r in self.conn.execute("SELECT * FROM items ORDER BY ts DESC")]
        return json.dumps(rows, ensure_ascii=False, indent=2)


def _parse_terms(q: str) -> tuple[list[str], list[str]]:
    """把搜尋字串拆成 (要包含的詞, 要排除的詞)。支援 "雙引號片語" 與 -排除詞。"""
    include: list[str] = []
    exclude: list[str] = []
    buf, quoted, tokens = "", False, []
    for ch in q or "":
        if ch == '"':
            quoted = not quoted
            if not quoted and buf:
                tokens.append(buf)
                buf = ""
        elif ch.isspace() and not quoted:
            if buf:
                tokens.append(buf)
                buf = ""
        else:
            buf += ch
    if buf:
        tokens.append(buf)
    for tok in tokens:
        if tok.startswith("-") and len(tok) > 1:
            exclude.append(tok[1:])
        elif tok != "-":
            include.append(tok)
    return include, exclude


def _fts_quote(term: str) -> str:
    return '"' + term.replace('"', '""') + '"'


def _like_escape(term: str) -> str:
    return term.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_")
