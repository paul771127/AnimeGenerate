"""SQLite 儲存層。

一間公司(dedupe_key)可以有多筆訊號(signal_key),所以主鍵是 signal_key,
查詢時再按 dedupe_key 聚合。另外有 suppression 表:退訂或拒絕聯絡的永久阻擋名單。
"""
from __future__ import annotations

import json
import sqlite3
from datetime import datetime, timezone
from pathlib import Path

from .models import Lead

SCHEMA = """
CREATE TABLE IF NOT EXISTS leads (
    signal_key   TEXT PRIMARY KEY,
    dedupe_key   TEXT NOT NULL,
    org          TEXT NOT NULL,
    domain       TEXT,
    source       TEXT NOT NULL,
    source_url   TEXT NOT NULL,
    signal_title TEXT,
    signal_type  TEXT,
    signal_date  TEXT,
    website      TEXT,
    emails       TEXT,
    phones       TEXT,
    contacts_other TEXT,
    location     TEXT,
    budget_twd   INTEGER,
    tags         TEXT,
    evidence     TEXT,
    notes        TEXT,
    score        INTEGER,
    score_detail TEXT,
    collected_at TEXT,
    status       TEXT DEFAULT 'new'
);
CREATE INDEX IF NOT EXISTS idx_leads_dedupe ON leads(dedupe_key);
CREATE INDEX IF NOT EXISTS idx_leads_score  ON leads(score DESC);
CREATE INDEX IF NOT EXISTS idx_leads_status ON leads(status);

-- 退訂 / 拒絕聯絡名單。寄信前必查,永不刪除。
CREATE TABLE IF NOT EXISTS suppression (
    value   TEXT PRIMARY KEY,   -- email 或 domain
    reason  TEXT,
    added_at TEXT
);

-- 聯絡紀錄,避免重複騷擾同一個人
CREATE TABLE IF NOT EXISTS outreach (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    dedupe_key TEXT NOT NULL,
    channel    TEXT,
    target     TEXT,
    sent_at    TEXT,
    note       TEXT
);
CREATE INDEX IF NOT EXISTS idx_outreach_dedupe ON outreach(dedupe_key);
"""

_LIST_COLS = ("emails", "phones", "contacts_other", "tags")


class Store:
    def __init__(self, path: str | Path):
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.conn = sqlite3.connect(self.path)
        self.conn.row_factory = sqlite3.Row
        self.conn.executescript(SCHEMA)
        self.conn.commit()

    def close(self):
        self.conn.close()

    # ---------- 寫入 ----------
    def upsert(self, lead: Lead) -> str:
        """回傳 'inserted' / 'updated' / 'suppressed'。"""
        if self.is_suppressed(lead):
            return "suppressed"
        row = lead.to_row()
        for c in _LIST_COLS:
            row[c] = json.dumps(row[c], ensure_ascii=False)
        row["score_detail"] = json.dumps(row["score_detail"], ensure_ascii=False)
        cols = [
            "signal_key", "dedupe_key", "org", "domain", "source", "source_url",
            "signal_title", "signal_type", "signal_date", "website", "emails",
            "phones", "contacts_other", "location", "budget_twd", "tags",
            "evidence", "notes",
            "score", "score_detail", "collected_at", "status",
        ]
        existing = self.conn.execute(
            "SELECT status FROM leads WHERE signal_key=?", (row["signal_key"],)
        ).fetchone()
        if existing:
            # 不覆蓋人工維護的 status 與 notes
            upd = [c for c in cols if c not in ("signal_key", "status", "notes")]
            self.conn.execute(
                f"UPDATE leads SET {','.join(f'{c}=?' for c in upd)} WHERE signal_key=?",
                [row[c] for c in upd] + [row["signal_key"]],
            )
            self.conn.commit()
            return "updated"
        self.conn.execute(
            f"INSERT INTO leads ({','.join(cols)}) VALUES ({','.join('?' * len(cols))})",
            [row[c] for c in cols],
        )
        self.conn.commit()
        return "inserted"

    # ---------- 退訂名單 ----------
    def suppress(self, value: str, reason: str = "manual"):
        self.conn.execute(
            "INSERT OR REPLACE INTO suppression (value, reason, added_at) VALUES (?,?,?)",
            (value.lower().strip(), reason,
             datetime.now(timezone.utc).isoformat(timespec="seconds")),
        )
        self.conn.commit()

    def is_suppressed(self, lead: Lead) -> bool:
        vals = [e.lower() for e in lead.emails]
        if lead.domain:
            vals.append(lead.domain)
        if not vals:
            return False
        q = ",".join("?" * len(vals))
        hit = self.conn.execute(
            f"SELECT 1 FROM suppression WHERE value IN ({q}) LIMIT 1", vals
        ).fetchone()
        return hit is not None

    # ---------- 查詢 ----------
    def top(self, min_score: int = 0, limit: int = 100, status: str | None = None,
            require_contact: bool = False) -> list[sqlite3.Row]:
        sql = "SELECT * FROM leads WHERE score >= ?"
        args: list = [min_score]
        if status:
            sql += " AND status = ?"
            args.append(status)
        if require_contact:
            sql += (" AND (emails != '[]' OR phones != '[]'"
                    " OR (contacts_other IS NOT NULL AND contacts_other != '[]'))")
        sql += " ORDER BY score DESC, signal_date DESC LIMIT ?"
        args.append(limit)
        return self.conn.execute(sql, args).fetchall()

    def stats(self) -> dict:
        c = self.conn
        return {
            "signals": c.execute("SELECT COUNT(*) FROM leads").fetchone()[0],
            "orgs": c.execute("SELECT COUNT(DISTINCT dedupe_key) FROM leads").fetchone()[0],
            "with_contact": c.execute(
                "SELECT COUNT(*) FROM leads WHERE emails != '[]' OR phones != '[]' "
                "OR (contacts_other IS NOT NULL AND contacts_other != '[]')"
            ).fetchone()[0],
            "score_70plus": c.execute("SELECT COUNT(*) FROM leads WHERE score >= 70").fetchone()[0],
            "by_source": dict(c.execute(
                "SELECT source, COUNT(*) FROM leads GROUP BY source ORDER BY 2 DESC"
            ).fetchall()),
            "suppressed": c.execute("SELECT COUNT(*) FROM suppression").fetchone()[0],
        }

    def domains_needing_contact(self, limit: int = 50) -> list[sqlite3.Row]:
        """有網站但還沒抓到聯絡方式的,交給 site_contact enricher。"""
        return self.conn.execute(
            "SELECT dedupe_key, org, website, MAX(score) AS score FROM leads "
            "WHERE website != '' AND emails = '[]' "
            "GROUP BY dedupe_key ORDER BY score DESC LIMIT ?", (limit,)
        ).fetchall()

    def set_contacts(self, dedupe_key: str, emails: list[str], phones: list[str]):
        self.conn.execute(
            "UPDATE leads SET emails=?, phones=? WHERE dedupe_key=? AND emails='[]'",
            (json.dumps(emails, ensure_ascii=False),
             json.dumps(phones, ensure_ascii=False), dedupe_key),
        )
        self.conn.commit()

    def rescore(self, signal_key: str, score: int, detail: dict):
        self.conn.execute(
            "UPDATE leads SET score=?, score_detail=? WHERE signal_key=?",
            (score, json.dumps(detail, ensure_ascii=False), signal_key),
        )
        self.conn.commit()

    def all_rows(self) -> list[sqlite3.Row]:
        return self.conn.execute("SELECT * FROM leads").fetchall()
