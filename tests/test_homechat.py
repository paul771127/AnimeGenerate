"""HomeChat 測試:啟動真的伺服器,用 HTTP 走過主人 / 訪客的完整流程。"""
import base64
import io
import zipfile
import json
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime
from http.cookiejar import CookieJar
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import homechat  # noqa: E402


LINE_TXT = """﻿[LINE] 與王小明的聊天記錄
儲存日期：2024/01/06 10:00

2024/01/05（五）
上午09:15\t王小明\t早安
09:16\t我\t早~
下午01:02\t王小明\t"第一行
第二行"
13:02\t王小明\t[貼圖]
2024/01/06（六）
00:30\t我\t晚安
"""


@pytest.fixture
def server(tmp_path):
    store = homechat.Store(tmp_path / "chat.db")
    store.set_password("secret123")
    store.set_setting("owner_username", "paul")
    app = homechat.App(store, owner_name="Paul")
    app.login_fails_all = homechat.RateLimiter(10_000, 600)
    srv = homechat.make_server(app, "127.0.0.1", 0)
    t = threading.Thread(target=srv.serve_forever, daemon=True)
    t.start()
    yield f"http://127.0.0.1:{srv.server_address[1]}", app
    srv.shutdown()
    srv.server_close()


class Client:
    def __init__(self, base):
        self.base = base
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(CookieJar()))

    def upload(self, path, raw, mime, headers=None):
        h = {"Content-Type": mime, "X-HomeChat-Upload": "1"}
        h.update(headers or {})
        r = urllib.request.Request(self.base + path, data=raw, headers=h, method="POST")
        try:
            with self.opener.open(r, timeout=5) as resp:
                return resp.status, json.loads(resp.read())
        except urllib.error.HTTPError as e:
            return e.code, json.loads(e.read() or b"{}")

    def raw(self, path, headers=None):
        r = urllib.request.Request(self.base + path, headers=headers or {})
        try:
            resp = self.opener.open(r, timeout=5)
            return resp.status, resp.headers, resp.read()
        except urllib.error.HTTPError as e:
            return e.code, e.headers, e.read()

    def req(self, path, body=None, headers=None):
        data = None if body is None else json.dumps(body).encode()
        h = {"Content-Type": "application/json"} if body is not None else {}
        h.update(headers or {})
        r = urllib.request.Request(self.base + path, data=data, headers=h)
        try:
            with self.opener.open(r, timeout=5) as resp:
                raw = resp.read()
                ctype = resp.headers.get("Content-Type", "")
                return resp.status, (json.loads(raw) if "json" in ctype else raw.decode())
        except urllib.error.HTTPError as e:
            raw = e.read()
            return e.code, (json.loads(raw) if "json" in e.headers.get("Content-Type", "") else raw.decode())


def login(base):
    c = Client(base)
    assert c.req("/api/login", {"username": "paul", "password": "secret123"})[0] == 200
    return c


def test_password_hash_roundtrip():
    h = homechat.hash_password("abc")
    assert homechat.verify_password("abc", h)
    assert not homechat.verify_password("abd", h)
    assert not homechat.verify_password("abc", "garbage")


def test_login_required_and_wrong_password(server):
    base, _ = server
    c = Client(base)
    assert c.req("/api/me")[1]["role"] is None
    assert c.req("/api/contacts")[0] == 401
    assert c.req("/api/login", {"username": "paul", "password": "nope"})[0] == 401
    assert c.req("/api/login", {"username": "", "password": "secret123"})[0] == 401
    c = login(base)
    assert c.req("/api/me")[1]["role"] == "owner"


def test_rejects_non_json_post(server):
    base, _ = server
    c = login(base)
    r = urllib.request.Request(base + "/api/contacts", data=b"name=x",
                               headers={"Content-Type": "application/x-www-form-urlencoded"})
    with pytest.raises(urllib.error.HTTPError) as e:
        c.opener.open(r, timeout=5)
    assert e.value.code == 415
    status, _ = c.req("/api/contacts", {"name": "x"}, headers={"Origin": "http://evil.example"})
    assert status == 403


def invite_path(contact):
    """邀請網址 → token。"""
    return contact["invite_url"].split("/c/")[1].split("?")[0]


_users = iter(range(10_000))


def join(base, contact, username=None, password="friend1"):
    g = Client(base)
    username = username or f"user{next(_users)}"
    status, data = g.req("/api/join", {"token": invite_path(contact), "username": username, "password": password})
    assert status == 200, data
    return g


def test_owner_and_guest_chat(server):
    base, _ = server
    owner = login(base)
    status, data = owner.req("/api/contacts", {"name": "阿明", "line_id": "ming"})
    assert status == 200
    contact = data["contact"]
    assert contact["invite_url"].startswith(base + "/c/") and contact["invite_url"].endswith("?openExternalBrowser=1")

    owner.req("/api/messages", {"contact_id": contact["id"], "body": "嗨,這是我家的聊天室"})

    guest = Client(base)
    token = invite_path(contact)
    # 打開連結(或 LINE 抓預覽)只會拿到網頁,不會用掉邀請
    status, page = guest.req(f"/c/{token}")
    assert status == 200 and "HomeChat" in page
    assert guest.req("/api/me")[1]["role"] is None
    assert guest.req(f"/api/invite?token={token}")[1] == {"valid": True, "owner_name": "Paul", "name": "阿明",
                                                          "username": ""}
    # 帳號密碼不合格時不會用掉連結
    assert guest.req("/api/join", {"token": token, "username": "a", "password": "friend1"})[0] == 400
    assert guest.req("/api/join", {"token": token, "username": "Paul", "password": "friend1"})[0] == 409
    assert guest.req("/api/join", {"token": token, "username": "ming", "password": "123"})[0] == 400
    # 按下「開始聊天」才加入
    assert guest.req("/api/join", {"token": token, "username": "Ming", "password": "friend1"})[0] == 200
    me = guest.req("/api/me")[1]
    assert me["role"] == "guest" and me["contact"]["name"] == "阿明" and me["contact"]["status"] == "active"
    assert me["username"] == "ming"

    # 連結只能用一次:別人再用就失效
    stranger = Client(base)
    assert stranger.req("/api/join", {"token": token, "username": "x123", "password": "friend1"})[0] == 410
    assert stranger.req(f"/api/invite?token={token}")[1]["valid"] is False
    assert owner.req("/api/contacts")[1]["contacts"][0]["invite_url"] == ""

    msgs = guest.req("/api/messages")[1]["messages"]
    assert [m["body"] for m in msgs] == ["嗨,這是我家的聊天室"]
    # 訪客不能指定別的聯絡人,也不能看聯絡人列表
    _, other = owner.req("/api/contacts", {"name": "別人"})
    owner.req("/api/messages", {"contact_id": other["contact"]["id"], "body": "秘密"})
    assert [m["body"] for m in guest.req(f"/api/messages?contact={other['contact']['id']}")[1]["messages"]] == ["嗨,這是我家的聊天室"]
    assert guest.req("/api/contacts")[0] == 403
    assert guest.req("/api/devices")[0] == 403

    _, sent = guest.req("/api/messages", {"contact_id": other["contact"]["id"], "body": "你好!"})
    assert sent["message"]["sender"] == "them" and sent["message"]["contact_id"] == contact["id"]

    contacts = {c["name"]: c for c in owner.req("/api/contacts")[1]["contacts"]}
    assert contacts["阿明"]["unread"] == 1 and contacts["阿明"]["devices"] == 1
    owner.req("/api/read", {"contact_id": contact["id"], "upto": sent["message"]["id"]})
    contacts = {c["name"]: c for c in owner.req("/api/contacts")[1]["contacts"]}
    assert contacts["阿明"]["unread"] == 0

    # 登出對方所有裝置
    owner.req(f"/api/contacts/{contact['id']}/logout-devices", {})
    assert guest.req("/api/me")[1]["role"] is None


def test_owner_opening_invite_does_not_log_out(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "測試"})[1]["contact"]
    assert owner.req("/api/join", {"token": invite_path(contact)})[1]["as_owner"] is True
    assert owner.req("/api/me")[1]["role"] == "owner"
    assert join(base, contact).req("/api/me")[1]["role"] == "guest"  # 朋友的連結沒有被用掉


def test_invite_expires_and_can_be_cancelled(server):
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "過期"})[1]["contact"]
    app.store.new_invite(contact["id"], days=-1)
    token = app.store.get_contact(contact["id"])["invite_token"]
    assert Client(base).req("/api/join", {"token": token, "username": "abc1", "password": "friend1"})[0] == 410

    c = owner.req(f"/api/contacts/{contact['id']}/invite", {})[1]["contact"]
    owner.req(f"/api/contacts/{contact['id']}/cancel-invite", {})
    assert Client(base).req("/api/join", {"token": invite_path(c), "username": "abc2", "password": "friend1"})[0] == 410
    # 新連結會讓舊的未使用連結失效
    c1 = owner.req(f"/api/contacts/{contact['id']}/invite", {})[1]["contact"]
    c2 = owner.req(f"/api/contacts/{contact['id']}/invite", {})[1]["contact"]
    assert Client(base).req("/api/join", {"token": invite_path(c1), "username": "abc3", "password": "friend1"})[0] == 410
    assert Client(base).req("/api/join", {"token": invite_path(c2), "username": "abc4", "password": "friend1"})[0] == 200


def test_friend_request_flow(server):
    base, _ = server
    owner = login(base)
    link = owner.req("/api/friend-link")[1]
    assert link == {"enabled": False, "url": ""}
    link = owner.req("/api/friend-link", {"enabled": True})[1]
    token = link["url"].split("/add/")[1].split("?")[0]

    friend = Client(base)
    assert friend.req(f"/api/add-info?token={token}")[1] == {"valid": True, "owner_name": "Paul"}
    acct = {"username": "hua", "password": "friend1"}
    assert friend.req("/api/request", {"token": "wrong", "name": "x", **acct})[0] == 410
    assert friend.req("/api/request", {"token": token, "name": "  ", **acct})[0] == 400
    assert friend.req("/api/request", {"token": token, "name": "小華", "username": "hua"})[0] == 400
    assert friend.req("/api/request", {"token": token, "name": "小華", "message": "我是國中同學", **acct})[0] == 200
    me = friend.req("/api/me")[1]
    assert me["role"] == "guest" and me["contact"]["status"] == "pending"
    # 還沒被接受:不能傳訊息、不能看訊息
    assert friend.req("/api/messages", {"body": "hi"})[0] == 403
    assert friend.req("/api/messages")[0] == 403

    pending = owner.req("/api/contacts")[1]["contacts"][0]
    assert pending["status"] == "pending" and pending["request_msg"] == "我是國中同學"
    assert owner.req("/api/messages", {"contact_id": pending["id"], "body": "x"})[0] == 403
    owner.req(f"/api/contacts/{pending['id']}/approve", {})
    assert friend.req("/api/messages", {"body": "hi"})[0] == 200

    # 拒絕 = 刪除,對方被登出
    other = Client(base)
    other.req("/api/request", {"token": token, "name": "陌生人", "username": "stranger", "password": "friend1"})
    cid = other.req("/api/me")[1]["contact"]["id"]
    owner.req(f"/api/contacts/{cid}/delete", {})
    assert other.req("/api/me")[1]["role"] is None

    # 換新連結 / 關閉後舊連結失效
    owner.req("/api/friend-link", {"reset": True})
    assert Client(base).req("/api/request", {"token": token, "name": "y", "username": "yyy", "password": "friend1"})[0] == 410


def test_friend_request_rate_limit(server):
    base, _ = server
    owner = login(base)
    token = owner.req("/api/friend-link", {"enabled": True})[1]["url"].split("/add/")[1].split("?")[0]
    codes = [Client(base).req("/api/request", {"token": token, "name": f"人{i}", "username": f"req{i}",
                                               "password": "friend1"})[0] for i in range(7)]
    assert codes[:5] == [200] * 5 and codes[5:] == [429, 429]


def test_first_run_setup_only_from_this_computer(tmp_path):
    store = homechat.Store(tmp_path / "new.db")
    app = homechat.App(store)
    srv = homechat.make_server(app, "127.0.0.1", 0)
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{srv.server_address[1]}"
    try:
        c = Client(base)
        # 經過通道進來的請求不能設定
        proxied = {"X-Forwarded-For": "8.8.8.8"}
        assert c.req("/api/me", headers=proxied)[1] == {"role": None, "setup": False}
        assert c.req("/api/setup", {"password": "abcdefgh"}, headers=proxied)[0] == 403
        assert c.req("/api/setup", {"password": "abcdefgh"}, headers={"Tailscale-Funnel-Request": "?1"})[0] == 403
        # DNS rebinding:網域被指到 127.0.0.1,Host / Origin 都是攻擊者的網域
        evil = {"Host": "evil.example", "Origin": "http://evil.example"}
        assert c.req("/api/setup", {"password": "abcdefgh"}, headers=evil)[0] == 403
        assert c.req("/api/me")[1] == {"role": None, "setup": True}
        assert c.req("/api/setup", {"username": "abao", "password": "short"})[0] == 400
        assert c.req("/api/setup", {"username": "a b", "password": "abcdefgh"})[0] == 400
        assert c.req("/api/setup", {"username": "Abao", "password": "abcdefgh", "owner_name": "阿保"})[0] == 200
        me = c.req("/api/me")[1]
        assert me["role"] == "owner" and me["owner_name"] == "阿保"
        assert Client(base).req("/api/setup", {"username": "evil", "password": "hijacked1"})[0] == 403
        assert Client(base).req("/api/login", {"username": "abao", "password": "abcdefgh"})[0] == 200
    finally:
        srv.shutdown()
        srv.server_close()


def test_sessions_stored_hashed_and_devices(server, tmp_path):
    base, app = server
    owner = login(base)
    other = login(base)
    rows = app.store.db.execute("SELECT id FROM sessions").fetchall()
    jar = next(h.cookiejar for h in owner.opener.handlers if hasattr(h, "cookiejar"))
    token = next(iter(jar)).value
    assert token not in {r["id"] for r in rows}
    assert homechat.token_hash(token) in {r["id"] for r in rows}

    devices = owner.req("/api/devices")[1]["devices"]
    assert len(devices) == 2 and sum(d["current"] for d in devices) == 1
    owner.req("/api/devices/remove", {"all_others": True})
    assert other.req("/api/me")[1]["role"] is None
    assert owner.req("/api/me")[1]["role"] == "owner"


def test_change_password(server):
    base, _ = server
    owner = login(base)
    other = login(base)
    assert owner.req("/api/password", {"old": "wrong", "new": "newpass123"})[0] == 400
    assert owner.req("/api/password", {"old": "secret123", "new": "short"})[0] == 400
    assert owner.req("/api/password", {"old": "secret123", "new": "newpass123"})[0] == 200
    assert owner.req("/api/me")[1]["role"] == "owner"  # 自己保持登入
    assert other.req("/api/me")[1]["role"] is None  # 其他裝置被登出
    assert Client(base).req("/api/login", {"username": "paul", "password": "newpass123"})[0] == 200


def test_guest_message_rate_limit(server):
    base, _ = server
    owner = login(base)
    guest = join(base, owner.req("/api/contacts", {"name": "話很快"})[1]["contact"])
    codes = [guest.req("/api/messages", {"body": str(i)})[0] for i in range(31)]
    assert codes[:30] == [200] * 30 and codes[30] == 429


def test_security_headers(server):
    base, _ = server
    with urllib.request.urlopen(base + "/", timeout=5) as r:
        assert r.headers["X-Frame-Options"] == "DENY"
        assert "frame-ancestors 'none'" in r.headers["Content-Security-Policy"]
        assert r.headers["Referrer-Policy"] == "no-referrer"
        assert r.headers["Server"] == "HomeChat"


def test_migrates_old_database(tmp_path):
    import sqlite3
    db = sqlite3.connect(tmp_path / "old.db")
    db.executescript("""
    CREATE TABLE contacts (id INTEGER PRIMARY KEY, name TEXT NOT NULL, line_id TEXT NOT NULL DEFAULT '',
      note TEXT NOT NULL DEFAULT '', invite_token TEXT NOT NULL UNIQUE, created_at REAL NOT NULL,
      owner_read_id INTEGER NOT NULL DEFAULT 0, guest_read_id INTEGER NOT NULL DEFAULT 0);
    CREATE TABLE sessions (token TEXT PRIMARY KEY, role TEXT NOT NULL, contact_id INTEGER, created_at REAL NOT NULL);
    INSERT INTO contacts (name, invite_token, created_at) VALUES ('舊朋友', 'oldtoken', 0);
    INSERT INTO sessions VALUES ('plain', 'owner', NULL, 0);
    """)
    db.commit()
    db.close()
    store = homechat.Store(tmp_path / "old.db")
    c = store.list_contacts()[0]
    assert c["name"] == "舊朋友" and c["status"] == "active" and c["invite_expires"] == 0
    assert store.get_session("plain") is None
    assert store.invite_contact("oldtoken") is None  # 舊的永久連結作廢


def test_events_stream_delivers_messages(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    resp = owner.opener.open(base + "/api/events", timeout=5)
    assert resp.headers["Content-Type"].startswith("text/event-stream")
    resp.readline()  # retry
    resp.readline()  # : hello
    resp.readline()
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "即時"})
    line = resp.readline().decode()
    event = json.loads(line[len("data: "):])
    assert event["type"] == "message" and event["message"]["body"] == "即時"
    resp.close()


def test_parse_contact_list():
    items = homechat.parse_contact_list("名字,LINE ID,備註\n王小明\n陳美美,mei,大學,同學\n\n王小明\n# 註解")
    assert items == [
        {"name": "王小明", "line_id": "", "note": ""},
        {"name": "陳美美", "line_id": "mei", "note": "大學,同學"},
    ]


def test_import_contacts_skips_existing(server):
    base, _ = server
    owner = login(base)
    owner.req("/api/contacts", {"name": "阿明"})
    r = owner.req("/api/contacts/import", {"text": "阿明\n小美\n阿公"})[1]
    assert r == {"added": ["小美", "阿公"], "skipped": ["阿明"]}


def test_parse_line_chat():
    p = homechat.parse_line_chat(LINE_TXT)
    assert p["contact"] == "王小明"
    assert p["senders"] == ["王小明", "我"]
    bodies = [(s, b) for _, s, b in p["messages"]]
    assert bodies == [("王小明", "早安"), ("我", "早~"), ("王小明", "第一行\n第二行"),
                      ("王小明", "[貼圖]"), ("我", "晚安")]
    times = [datetime.fromtimestamp(t) for t, _, _ in p["messages"]]
    assert times[0] == datetime(2024, 1, 5, 9, 15)
    assert (times[2].hour, times[2].minute) == (13, 2)
    assert times[3] > times[2]  # 同一分鐘保持順序
    assert times[4] == datetime(2024, 1, 6, 0, 30)


def test_parse_line_chat_english_header():
    txt = "[LINE] Chat history with Amy\nSaved on: 2024/02/01 10:00\n\n2024.02.01 Thursday\n10:00 AM\tAmy\thi\n12:30 PM\tMe\tyo\n"
    p = homechat.parse_line_chat(txt)
    assert p["contact"] == "Amy"
    assert [(datetime.fromtimestamp(t).hour, s) for t, s, _ in p["messages"]] == [(10, "Amy"), (12, "Me")]


def test_import_line_history(server):
    base, _ = server
    owner = login(base)
    r = owner.req("/api/contacts/import-line", {"text": LINE_TXT})[1]
    assert r["contact"]["name"] == "王小明" and r["imported"] == 5
    # 重複匯入不會重複
    assert owner.req("/api/contacts/import-line", {"text": LINE_TXT})[1]["imported"] == 0
    msgs = owner.req(f"/api/messages?contact={r['contact']['id']}")[1]["messages"]
    assert [m["sender"] for m in msgs] == ["them", "me", "them", "them", "me"]
    # 匯入的舊訊息不算未讀
    assert owner.req("/api/contacts")[1]["contacts"][0]["unread"] == 0
    export = owner.req(f"/api/export?contact={r['contact']['id']}")[1]
    assert "Paul\t早~" in export and '"第一行\n第二行"' in export


def test_import_line_without_header_asks_who(server):
    base, _ = server
    owner = login(base)
    txt = LINE_TXT.split("\n", 2)[2]
    r = owner.req("/api/contacts/import-line", {"text": txt})[1]
    assert r == {"need_choice": True, "senders": ["王小明", "我"]}
    r = owner.req("/api/contacts/import-line", {"text": txt, "contact_name": "王小明"})[1]
    assert r["imported"] == 5


def test_pagination(server, monkeypatch):
    base, app = server
    owner = login(base)
    cid = owner.req("/api/contacts", {"name": "話很多"})[1]["contact"]["id"]
    for i in range(250):
        app.store.add_message(cid, "me", f"m{i}")
    first = owner.req(f"/api/messages?contact={cid}")[1]
    assert first["more"] and len(first["messages"]) == 200 and first["messages"][-1]["body"] == "m249"
    top = first["messages"][0]
    older = owner.req(f"/api/messages?contact={cid}&before_at={top['created_at']}&before_id={top['id']}")[1]
    assert not older["more"] and len(older["messages"]) == 50 and older["messages"][0]["body"] == "m0"


# ---------------------------------------------------------------- 外部連線

def fake_program(tmp_path, name, script):
    path = tmp_path / name
    path.write_text(f"#!{sys.executable}\nimport sys, time, json\n{script}\n")
    path.chmod(0o755)
    return str(path)


def test_cloudflared_quick_tunnel_url(tmp_path):
    # 照 cloudflared 2026.10 真實輸出格式
    exe = fake_program(tmp_path, "cloudflared", """
print("2026-10-09T22:45:42Z INF Requesting new quick Tunnel on trycloudflare.com...", flush=True)
print("2026-10-09T22:45:45Z INF |  https://manufacture-capacity-heads-aquarium.trycloudflare.com    |", flush=True)
print("2026-10-09T22:45:46Z INF Registered tunnel connection connIndex=0", flush=True)
time.sleep(30)
""")
    url, proc = homechat.start_cloudflared(exe, 8800, timeout=10)
    try:
        assert url == "https://manufacture-capacity-heads-aquarium.trycloudflare.com"
        assert proc.poll() is None  # 通道要一直開著
    finally:
        proc.terminate()


def test_cloudflared_failure(tmp_path):
    exe = fake_program(tmp_path, "cloudflared", "print('ERR failed'); sys.exit(1)")
    assert homechat.start_cloudflared(exe, 8800, timeout=5) == (None, None)


def test_tailscale_funnel_url(tmp_path):
    exe = fake_program(tmp_path, "tailscale", """
if sys.argv[1] == "status":
    print(json.dumps({"BackendState": "Running", "Self": {"DNSName": "home-pc.tail1234.ts.net."}}))
else:
    print("Available on the internet:\\n\\nhttps://home-pc.tail1234.ts.net/\\n|-- proxy http://127.0.0.1:8800\\n")
""")
    assert homechat.start_tailscale(exe, 8800, timeout=10) == "https://home-pc.tail1234.ts.net"


def test_tailscale_logged_out(tmp_path):
    exe = fake_program(tmp_path, "tailscale", """
print(json.dumps({"BackendState": "NeedsLogin"}))
""")
    assert homechat.start_tailscale(exe, 8800, timeout=10) is None


def test_open_tunnel_falls_back_to_cloudflare(tmp_path, monkeypatch):
    ts = fake_program(tmp_path, "tailscale", "print(json.dumps({'BackendState': 'Running'})) if sys.argv[1] == 'status' else sys.exit(1)")
    cf = fake_program(tmp_path, "cloudflared", "print('https://abc-def.trycloudflare.com', flush=True); time.sleep(30)")
    monkeypatch.setattr(homechat, "find_tailscale", lambda: ts)
    monkeypatch.setattr(homechat, "find_cloudflared", lambda: cf)
    url, kind, proc = homechat.open_tunnel("auto", 8800)
    proc.terminate()
    assert (url, kind) == ("https://abc-def.trycloudflare.com", "cloudflare")
    monkeypatch.setattr(homechat, "find_tailscale", lambda: None)
    monkeypatch.setattr(homechat, "find_cloudflared", lambda: None)
    assert homechat.open_tunnel("auto", 8800) == (None, None, None)


def test_proxy_rewrites_host(server):
    """經過通道時 Host 可能是 127.0.0.1,瀏覽器的 Origin 是對外網址,要能正常登入和送訊息。"""
    base, app = server
    app.store.set_setting("public_url", "https://home-pc.tail1234.ts.net")
    c = Client(base)
    origin = {"Origin": "https://home-pc.tail1234.ts.net"}
    assert c.req("/api/login", {"username": "paul", "password": "secret123"}, headers=origin)[0] == 200
    status, data = c.req("/api/contacts", {"name": "外面的朋友"}, headers=origin)
    assert status == 200
    assert data["contact"]["invite_url"].startswith("https://home-pc.tail1234.ts.net/c/")
    assert c.req("/api/contacts", {"name": "x"}, headers={"Origin": "https://evil.example"})[0] == 403


# ---------------------------------------------------------------- 帳號 / 自動登入 / 每台裝置存一份

def test_guest_logs_in_on_new_device_with_account(server):
    """朋友換手機、加入主畫面(cookie 不見)時,用帳號密碼登入回到同一個對話。"""
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    phone1 = join(base, contact, "mei", "meipass")
    phone1.req("/api/messages", {"body": "第一支手機"})

    phone2 = Client(base)
    assert phone2.req("/api/login", {"username": "mei", "password": "wrong"})[0] == 401
    assert phone2.req("/api/login", {"username": "nobody", "password": "meipass"})[0] == 401
    assert phone2.req("/api/login", {"username": "MEI", "password": "meipass"})[0] == 200
    me = phone2.req("/api/me")[1]
    assert me["role"] == "guest" and me["contact"]["id"] == contact["id"]
    assert [m["body"] for m in phone2.req("/api/messages")[1]["messages"]] == ["第一支手機"]
    # 朋友帳號不能拿來登入主人
    assert phone2.req("/api/contacts")[0] == 403
    contacts = owner.req("/api/contacts")[1]["contacts"]
    assert contacts[0]["username"] == "mei" and contacts[0]["devices"] == 2


def test_guest_forgot_password_new_invite_resets_it(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "健忘"})[1]["contact"]
    join(base, contact, "forget", "oldpass")
    c = owner.req(f"/api/contacts/{contact['id']}/invite", {})[1]["contact"]
    g = Client(base)
    info = g.req(f"/api/invite?token={invite_path(c)}")[1]
    assert info["username"] == "forget"  # 已經有帳號:只要設新密碼
    assert g.req("/api/join", {"token": invite_path(c), "password": "newpass"})[0] == 200
    assert Client(base).req("/api/login", {"username": "forget", "password": "oldpass"})[0] == 401
    assert Client(base).req("/api/login", {"username": "forget", "password": "newpass"})[0] == 200


def test_guest_change_password_logs_out_other_devices(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "改密碼"})[1]["contact"]
    a = join(base, contact, "change", "pass111")
    b = Client(base)
    b.req("/api/login", {"username": "change", "password": "pass111"})
    assert a.req("/api/password", {"old": "wrong", "new": "pass222"})[0] == 400
    assert a.req("/api/password", {"old": "pass111", "new": "pass222"})[0] == 200
    assert a.req("/api/me")[1]["role"] == "guest"
    assert b.req("/api/me")[1]["role"] is None


def test_duplicate_username_rejected(server):
    base, _ = server
    owner = login(base)
    c1 = owner.req("/api/contacts", {"name": "一號"})[1]["contact"]
    c2 = owner.req("/api/contacts", {"name": "二號"})[1]["contact"]
    join(base, c1, "same", "pass111")
    status, data = Client(base).req("/api/join", {"token": invite_path(c2), "username": "SAME", "password": "pass111"})
    assert status == 409 and "已經有人用" in data["error"]


def test_login_cookie_slides_forward(server):
    base, _ = server
    owner = login(base)
    r = owner.opener.open(base + "/api/me", timeout=5)
    cookie = r.headers["Set-Cookie"]
    assert "Max-Age=31536000" in cookie and "HttpOnly" in cookie


def test_offline_resend_with_client_id_is_not_duplicated(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "離線"})[1]["contact"]
    guest = join(base, contact)
    first = guest.req("/api/messages", {"body": "排隊的訊息", "client_id": "abc-123"})[1]["message"]
    again = guest.req("/api/messages", {"body": "排隊的訊息", "client_id": "abc-123"})[1]["message"]
    assert first["id"] == again["id"] and first["client_id"] == "abc-123"
    assert len(owner.req(f"/api/messages?contact={contact['id']}")[1]["messages"]) == 1


def test_guest_can_download_own_history(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest = join(base, contact)
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "嗨"})
    guest.req("/api/messages", {"body": "你好"})
    status, text = guest.req("/api/export")
    assert status == 200
    assert "與Paul的聊天記錄" in text and "Paul\t嗨" in text and "小美\t你好" in text
    # 朋友不能下載別人的
    other = owner.req("/api/contacts", {"name": "別人"})[1]["contact"]
    owner.req("/api/messages", {"contact_id": other["id"], "body": "秘密"})
    assert "秘密" not in guest.req(f"/api/export?contact={other['id']}")[1]


def test_service_worker_served(server):
    base, _ = server
    with urllib.request.urlopen(base + "/sw.js", timeout=5) as r:
        assert r.headers["Content-Type"].startswith("text/javascript")
        assert b"/api/" in r.read()


# ---------------------------------------------------------------- 更新 / 錯誤處理

def test_unexpected_error_returns_500_instead_of_dropping(server, monkeypatch):
    """沒料到的錯誤要回 500;直接斷線會讓網頁以為家裡電腦關機。"""
    base, app = server
    owner = login(base)
    monkeypatch.setattr(homechat.Store, "list_contacts", lambda self: 1 / 0)
    status, data = owner.req("/api/contacts")
    assert status == 500 and "錯誤" in data["error"]
    assert owner.req("/api/me")[0] == 200  # 伺服器沒有掛掉


def test_running_version_detects_homechat(server):
    base, _ = server
    port = int(base.rsplit(":", 1)[1])
    assert homechat.running_version(port) == homechat.VERSION
    assert homechat.running_version(1) is None  # 沒有程式


def test_pids_listening_parses_windows_netstat(monkeypatch):
    out = """
Active Connections

  Proto  Local Address          Foreign Address        State           PID
  TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1000
  TCP    0.0.0.0:8800           0.0.0.0:0              LISTENING       4242
  TCP    127.0.0.1:8800         127.0.0.1:51000        ESTABLISHED     4242
  TCP    127.0.0.1:51000        127.0.0.1:8800         ESTABLISHED     777
  TCP    [::]:8800              [::]:0                 接聽            4243
  TCP    0.0.0.0:18800          0.0.0.0:0              LISTENING       5555
"""
    class R:
        stdout = out
    monkeypatch.setattr(homechat.os, "name", "nt")
    monkeypatch.setattr(homechat, "NO_WINDOW", {})
    monkeypatch.setattr(homechat.subprocess, "run", lambda *a, **k: R())
    assert homechat.pids_listening(8800) == {4242, 4243}


# ---------------------------------------------------------------- 圖片 / 檔案 / 語音

PNG = (b"\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15"
       b"\xc4\x89\x00\x00\x00\rIDATx\x9cc\xf8\xcf\xc0\xf0\x1f\x00\x05\x00\x01\xff\x89\x99=\x1d\x00\x00\x00\x00IEND\xaeB`\x82")


def send_file(client, contact_id, raw, mime, name, kind, caption=""):
    q = f"?name={urllib.parse.quote(name)}" + (f"&contact={contact_id}" if contact_id else "")
    status, data = client.upload("/api/upload" + q, raw, mime)
    assert status == 200, data
    status, data = client.req("/api/messages", {"contact_id": contact_id, "kind": kind, "body": caption,
                                                "attachment_id": data["attachment"]["id"]})
    assert status == 200, data
    return data["message"]


def test_send_image_and_file(server):
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest = join(base, contact)

    msg = send_file(owner, contact["id"], PNG, "image/png", "貓咪.png", "image", "看我的貓")
    assert msg["kind"] == "image" and msg["body"] == "看我的貓"
    assert msg["attachment"]["name"] == "貓咪.png" and msg["attachment"]["size"] == len(PNG)
    # 朋友看得到、下載得到
    got = guest.req("/api/messages")[1]["messages"][-1]
    assert got["attachment"]["id"] == msg["attachment"]["id"]
    status, headers, body = guest.raw(f"/api/files/{msg['attachment']['id']}")
    assert status == 200 and body == PNG and headers["Content-Type"] == "image/png"
    assert headers["Content-Disposition"].startswith("inline")
    assert "sandbox" in headers["Content-Security-Policy"]
    # 朋友傳檔案
    pdf = b"%PDF-1.4 fake"
    fmsg = send_file(guest, None, pdf, "application/pdf", "報告.pdf", "file")
    assert fmsg["sender"] == "them" and fmsg["kind"] == "file"
    status, headers, body = owner.raw(f"/api/files/{fmsg['attachment']['id']}")
    assert body == pdf and headers["Content-Disposition"].startswith("attachment")
    assert headers["Content-Type"] == "application/octet-stream"
    # 列表顯示 [檔案]
    assert owner.req("/api/contacts")[1]["contacts"][0]["last_body"] == "[檔案]"
    # 匯出
    text = owner.req(f"/api/export?contact={contact['id']}")[1]
    assert "[圖片] 貓咪.png 看我的貓" in text and "[檔案] 報告.pdf" in text


def test_dangerous_files_are_never_rendered(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "x"})[1]["contact"]
    for mime, name in (("text/html", "a.html"), ("image/svg+xml", "a.svg")):
        msg = send_file(owner, contact["id"], b"<script>alert(1)</script>", mime, name, "image")
        assert msg["kind"] == "file" or mime == "image/svg+xml"
        status, headers, _ = owner.raw(f"/api/files/{msg['attachment']['id']}")
        assert headers["Content-Type"] == "application/octet-stream"
        assert headers["Content-Disposition"].startswith("attachment")


def test_file_access_is_private(server):
    base, _ = server
    owner = login(base)
    a = owner.req("/api/contacts", {"name": "A"})[1]["contact"]
    b = owner.req("/api/contacts", {"name": "B"})[1]["contact"]
    guest_b = join(base, b)
    msg = send_file(owner, a["id"], PNG, "image/png", "a.png", "image")
    url = f"/api/files/{msg['attachment']['id']}"
    assert guest_b.raw(url)[0] == 404  # 別人的對話
    assert Client(base).raw(url)[0] == 401  # 沒登入
    # 不能拿別人上傳的檔案來發訊息
    status, _ = guest_b.req("/api/messages", {"kind": "image", "attachment_id": msg["attachment"]["id"]})
    assert status == 400


def test_upload_checks(server, monkeypatch):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "x"})[1]["contact"]
    q = f"/api/upload?contact={contact['id']}&name=a.png"
    # 沒有自訂標頭(例如其他網站的表單)
    r = urllib.request.Request(base + q, data=PNG, headers={"Content-Type": "image/png"}, method="POST")
    with pytest.raises(urllib.error.HTTPError) as e:
        owner.opener.open(r, timeout=5)
    assert e.value.code == 403
    monkeypatch.setattr(homechat, "MAX_UPLOAD", 10)
    assert owner.upload(q, PNG, "image/png")[0] == 413


def test_range_request_for_audio(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "x"})[1]["contact"]
    audio = bytes(range(256)) * 4
    msg = send_file(owner, contact["id"], audio, "audio/mp4", "voice.m4a", "audio")
    assert msg["kind"] == "audio"
    url = f"/api/files/{msg['attachment']['id']}"
    status, headers, body = owner.raw(url, {"Range": "bytes=0-1"})
    assert status == 206 and body == audio[:2] and headers["Content-Range"] == f"bytes 0-1/{len(audio)}"
    status, headers, body = owner.raw(url, {"Range": "bytes=1000-"})
    assert status == 206 and body == audio[1000:]
    assert owner.raw(url, {"Range": "bytes=5000-"})[0] == 416


def test_deleting_contact_deletes_files(server):
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "x"})[1]["contact"]
    msg = send_file(owner, contact["id"], PNG, "image/png", "a.png", "image")
    path = app.store.file_path(msg["attachment"]["id"])
    assert path.exists()
    owner.req(f"/api/contacts/{contact['id']}/delete", {})
    assert not path.exists()


def test_unsent_uploads_are_cleaned_up(server):
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "x"})[1]["contact"]
    att = owner.upload(f"/api/upload?contact={contact['id']}&name=a.png", PNG, "image/png")[1]["attachment"]
    sent = send_file(owner, contact["id"], PNG, "image/png", "b.png", "image")
    assert app.store.cleanup_attachments(older_than=-1) == 1
    assert not app.store.file_path(att["id"]).exists()
    assert app.store.file_path(sent["attachment"]["id"]).exists()


# ---------------------------------------------------------------- 自己的貼圖

GIF = (b"GIF89a\x01\x00\x01\x00\x80\x00\x00\x00\x00\x00\xff\xff\xff!\xf9\x04\x01\x00\x00\x00\x00,"
       b"\x00\x00\x00\x00\x01\x00\x01\x00\x00\x02\x02D\x01\x00;")


def add_sticker(client, raw=GIF, mime="image/gif"):
    return client.upload("/api/stickers/upload", raw, mime)


def test_stickers_upload_send_and_privacy(server):
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest = join(base, contact)
    status, data = add_sticker(owner)
    assert status == 200
    sid = data["sticker"]["id"]
    assert [s["id"] for s in owner.req("/api/stickers")[1]["stickers"]] == [sid]
    assert guest.req("/api/stickers")[1]["stickers"] == []  # 各自的貼圖庫
    assert owner.raw(f"/api/sticker-files/{sid}")[2] == GIF
    assert guest.raw(f"/api/sticker-files/{sid}")[0] == 404  # 別人的貼圖庫拿不到

    status, data = owner.req("/api/messages", {"contact_id": contact["id"], "kind": "sticker",
                                               "sticker_id": sid, "client_id": "st-1"})
    msg = data["message"]
    assert status == 200 and msg["kind"] == "sticker" and msg["attachment"]["mime"] == "image/gif"
    # 重送同一個 client_id 不會多一則
    again = owner.req("/api/messages", {"contact_id": contact["id"], "kind": "sticker",
                                        "sticker_id": sid, "client_id": "st-1"})[1]["message"]
    assert again["id"] == msg["id"]
    # 朋友收得到,並可以加到自己的貼圖
    got = guest.req("/api/messages")[1]["messages"][-1]
    assert got["kind"] == "sticker"
    assert guest.raw(f"/api/files/{got['attachment']['id']}")[2] == GIF
    saved = guest.req("/api/stickers/save", {"attachment_id": got["attachment"]["id"]})[1]["sticker"]
    assert [s["id"] for s in guest.req("/api/stickers")[1]["stickers"]] == [saved["id"]]
    assert owner.req("/api/contacts")[1]["contacts"][0]["last_body"] == "[貼圖]"
    # 刪掉貼圖,聊天紀錄裡的還在
    owner.req("/api/stickers/delete", {"id": sid})
    assert owner.req("/api/stickers")[1]["stickers"] == []
    assert owner.raw(f"/api/files/{msg['attachment']['id']}")[2] == GIF
    # 不能用別人的貼圖傳送,也不能刪別人的
    status, _ = owner.req("/api/messages", {"contact_id": contact["id"], "kind": "sticker", "sticker_id": saved["id"]})
    assert status == 404
    assert owner.req("/api/stickers/delete", {"id": saved["id"]})[0] == 404


def test_sticker_upload_checks(server, monkeypatch):
    base, _ = server
    owner = login(base)
    assert add_sticker(owner, b"<svg/>", "image/svg+xml")[0] == 415
    assert add_sticker(owner, b"<html>", "text/html")[0] == 415
    monkeypatch.setattr(homechat, "MAX_STICKER", 10)
    assert add_sticker(owner)[0] == 413
    monkeypatch.setattr(homechat, "MAX_STICKER", 2 * 1024 * 1024)
    monkeypatch.setattr(homechat, "MAX_STICKERS", 2)
    assert add_sticker(owner)[0] == 200 and add_sticker(owner)[0] == 200
    assert add_sticker(owner)[0] == 409
    r = urllib.request.Request(base + "/api/stickers/upload", data=GIF, headers={"Content-Type": "image/gif"}, method="POST")
    with pytest.raises(urllib.error.HTTPError) as e:
        owner.opener.open(r, timeout=5)
    assert e.value.code == 403


def test_cannot_save_non_image_as_sticker(server):
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "x"})[1]["contact"]
    msg = send_file(owner, contact["id"], b"%PDF", "application/pdf", "a.pdf", "file")
    assert owner.req("/api/stickers/save", {"attachment_id": msg["attachment"]["id"]})[0] == 415


def test_deleting_contact_deletes_guest_stickers(server):
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "x"})[1]["contact"]
    guest = join(base, contact)
    sid = add_sticker(guest)[1]["sticker"]["id"]
    assert app.store.sticker_path(sid).exists()
    owner.req(f"/api/contacts/{contact['id']}/delete", {})
    assert not app.store.sticker_path(sid).exists()


# ---------------------------------------------------------------- 共同相簿

def upload_photo(client, contact_id, name="a.png", raw=PNG):
    q = f"?name={urllib.parse.quote(name)}" + (f"&contact={contact_id}" if contact_id else "")
    return client.upload("/api/upload" + q, raw, "image/png")[1]["attachment"]


def test_shared_album(server):
    import io
    import zipfile
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest = join(base, contact)
    album = guest.req("/api/albums", {"name": "旅行"})[1]["album"]
    assert album["created_by"] == "them"
    # 兩邊都看得到、都能加照片
    assert [a["name"] for a in owner.req(f"/api/albums?contact={contact['id']}")[1]["albums"]] == ["旅行"]
    a1 = upload_photo(guest, None, "海邊.png")
    a2 = upload_photo(guest, None, "夕陽.png")
    assert guest.req(f"/api/albums/{album['id']}/add", {"attachment_ids": [a1["id"], a2["id"]]})[1]["added"] == 2
    # 聊天室出現相簿卡片
    card = owner.req(f"/api/messages?contact={contact['id']}")[1]["messages"][-1]
    assert card["kind"] == "album" and json.loads(card["body"]) == {"album_id": album["id"], "name": "旅行", "count": 2}
    assert owner.req("/api/contacts")[1]["contacts"][0]["last_body"] == "[相簿]"
    # 主人把聊天裡的照片加進相簿(不會重複)
    chat_msg = send_file(owner, contact["id"], PNG, "image/png", "合照.png", "image")
    r = owner.req(f"/api/albums/{album['id']}/add", {"attachment_ids": [chat_msg["attachment"]["id"], a1["id"]],
                                                     "notify": False})[1]
    assert r["added"] == 1
    photos = owner.req(f"/api/albums/{album['id']}/photos")[1]["photos"]
    assert [p["attachment"]["name"] for p in photos] == ["合照.png", "夕陽.png", "海邊.png"]
    assert owner.req(f"/api/albums?contact={contact['id']}")[1]["albums"][0]["count"] == 3
    # 下載整本 zip
    status, headers, body = guest.raw(f"/api/albums/{album['id']}/zip")
    assert status == 200 and headers["Content-Type"] == "application/zip"
    assert sorted(zipfile.ZipFile(io.BytesIO(body)).namelist()) == ["合照.png", "夕陽.png", "海邊.png"]
    # 朋友只能移除自己加的照片;不能刪主人的相簿
    owner_photo = next(p for p in photos if p["added_by"] == "me")
    assert guest.req(f"/api/albums/{album['id']}/remove", {"photo_id": owner_photo["id"]})[0] == 403
    mine = next(p for p in photos if p["added_by"] == "them")
    assert guest.req(f"/api/albums/{album['id']}/remove", {"photo_id": mine["id"]})[0] == 200
    owner_album = owner.req("/api/albums", {"contact_id": contact["id"], "name": "家庭"})[1]["album"]
    assert guest.req(f"/api/albums/{owner_album['id']}/delete", {})[0] == 403
    assert guest.req(f"/api/albums/{album['id']}/rename", {"name": "2026 旅行"})[0] == 200
    # 匯出
    text = owner.req(f"/api/export?contact={contact['id']}")[1]
    assert "[相簿] 新增 2 張照片到「旅行」" in text
    # 從相簿移除的照片:聊天沒用到的檔案會被清掉,聊天裡的留著
    assert guest.req(f"/api/albums/{album['id']}/delete", {})[0] == 200
    app.store.cleanup_attachments(older_than=-1)
    assert app.store.file_path(chat_msg["attachment"]["id"]).exists()
    assert app.store.file_path(a1["id"]).exists()  # 相簿卡片用它當封面


def test_album_privacy(server):
    base, _ = server
    owner = login(base)
    a = owner.req("/api/contacts", {"name": "A"})[1]["contact"]
    b = owner.req("/api/contacts", {"name": "B"})[1]["contact"]
    guest_b = join(base, b)
    album = owner.req("/api/albums", {"contact_id": a["id"], "name": "A 的相簿"})[1]["album"]
    assert guest_b.req(f"/api/albums/{album['id']}/photos")[0] == 404
    assert guest_b.raw(f"/api/albums/{album['id']}/zip")[0] == 404
    assert guest_b.req(f"/api/albums/{album['id']}/add", {"attachment_ids": ["x"]})[0] == 404
    assert guest_b.req(f"/api/albums?contact={a['id']}")[1]["albums"] == []  # 只看得到自己的對話
    # 不能把別的對話的照片加進來
    other = upload_photo(guest_b, None)
    assert owner.req(f"/api/albums/{album['id']}/add", {"attachment_ids": [other["id"]]})[0] == 400


# ---------------------------------------------------------------- 分享 / 儲存貼圖

def test_share_sticker_pack_and_save_all(server):
    import io
    import zipfile
    base, _ = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest = join(base, contact)
    ids = [add_sticker(owner)[1]["sticker"]["id"] for _ in range(3)]
    msg = owner.req("/api/stickers/share", {"contact_id": contact["id"], "sticker_ids": ids, "client_id": "pk1"})[1]["message"]
    info = json.loads(msg["body"])
    assert msg["kind"] == "stickers" and info["count"] == 3
    again = owner.req("/api/stickers/share", {"contact_id": contact["id"], "sticker_ids": ids, "client_id": "pk1"})[1]
    assert again["message"]["id"] == msg["id"]
    got = guest.req("/api/messages")[1]["messages"][-1]
    att_ids = [i["id"] for i in json.loads(got["body"])["items"]]
    saved = guest.req("/api/stickers/save", {"attachment_ids": att_ids})[1]["stickers"]
    assert len(saved) == 3 and len(guest.req("/api/stickers")[1]["stickers"]) == 3
    # 分享後刪掉自己的貼圖,對方收到的還在;檔案不會被清掉
    for sid in ids:
        owner.req("/api/stickers/delete", {"id": sid})
    server[1].store.cleanup_attachments(older_than=-1)
    assert all(guest.raw(f"/api/files/{a}")[0] == 200 for a in att_ids)
    # 不能分享別人的貼圖
    assert owner.req("/api/stickers/share", {"contact_id": contact["id"], "sticker_ids": [saved[0]["id"]]})[0] == 404
    # 下載:一張 / 多張打包
    status, headers, body = guest.raw(f"/api/sticker-files/{saved[0]['id']}?download=1")
    assert body == GIF and headers["Content-Disposition"].startswith("attachment")
    status, headers, body = guest.raw(f"/api/stickers/zip?ids={','.join(s['id'] for s in saved)}")
    assert len(zipfile.ZipFile(io.BytesIO(body)).namelist()) == 3
    assert owner.raw(f"/api/stickers/zip?ids={saved[0]['id']}")[0] == 404
    assert "[分享貼圖] 3 張" in owner.req(f"/api/export?contact={contact['id']}")[1]


# ---------------------------------------------------------------- 互通(兩台 HomeChat)

class Home:
    """一台完整的 HomeChat(自己的資料庫、自己的網址)。"""

    def __init__(self, tmp_path, name, owner, port=0):
        self.dir = tmp_path / name
        self.store = homechat.Store(self.dir / "chat.db")
        self.store.set_password("secret123")
        self.store.set_setting("owner_username", owner.lower())
        self.app = homechat.App(self.store, owner_name=owner)
        self.app.login_fails_all = homechat.RateLimiter(10_000, 600)
        self.owner_user = owner.lower()
        self.start(port)

    def start(self, port=0):
        self.srv = homechat.make_server(self.app, "127.0.0.1", port)
        threading.Thread(target=self.srv.serve_forever, daemon=True).start()
        self.base = f"http://127.0.0.1:{self.srv.server_address[1]}"
        self.store.set_setting("public_url", self.base)
        self.owner = Client(self.base)
        assert self.owner.req("/api/login", {"username": self.owner_user, "password": "secret123"})[0] == 200

    def stop(self):
        self.srv.shutdown()
        self.srv.server_close()

    def contacts(self):
        return self.owner.req("/api/contacts")[1]["contacts"]

    def messages(self, contact_id):
        return self.owner.req(f"/api/messages?contact={contact_id}")[1]["messages"]


def flush(*homes, rounds=20):
    """把兩邊排隊的東西送完(背景也會自己送,這裡直接叫比較快)。"""
    for _ in range(rounds):
        moved = sum(h.app.fed.process() for h in homes)
        if not moved and not any(h.store.due_outbox() for h in homes):
            return


@pytest.fixture
def two_homes(tmp_path):
    a, b = Home(tmp_path, "a", "Alice"), Home(tmp_path, "b", "Bob")
    yield a, b
    for h in (a, b):
        try:
            h.stop()
        except Exception:
            pass


def link(a, b, contact_id=None):
    """A 產生互通碼,B 輸入 → 回傳 (A 這邊的聯絡人, B 這邊的聯絡人)。"""
    body = {"contact_id": contact_id} if contact_id else {}
    code = a.owner.req("/api/fed/code", body)[1]["code"]
    assert code.startswith("HC1.")
    status, data = b.owner.req("/api/fed/connect", {"code": code})
    assert status == 200, data
    on_b = data["contact"]
    on_a = next(c for c in a.contacts() if c["peer"])
    return on_a, on_b


def test_federation_pair_and_chat(two_homes):
    a, b = two_homes
    on_a, on_b = link(a, b)
    assert on_b["name"] == "Alice" and on_b["peer"]["name"] == "Alice"
    assert on_a["name"] == "Bob" and on_a["peer"]["url"] == b.base

    # A → B
    sent = a.owner.req("/api/messages", {"contact_id": on_a["id"], "body": "嗨 Bob"})[1]["message"]
    assert sent["fed_state"] == "queued"
    flush(a, b)
    got = b.messages(on_b["id"])
    assert [(m["sender"], m["body"]) for m in got] == [("them", "嗨 Bob")]
    assert got[0]["uid"] == sent["uid"]
    assert a.messages(on_a["id"])[0]["fed_state"] == "sent"
    # 未讀 / 已讀同步:B 讀了 A 的訊息 → A 看到已讀
    assert next(c for c in b.contacts() if c["id"] == on_b["id"])["unread"] == 1
    b.owner.req("/api/read", {"contact_id": on_b["id"], "upto": got[0]["id"]})
    flush(a, b)
    assert a.store.get_contact(on_a["id"])["guest_read_id"] == sent["id"]
    # B → A
    b.owner.req("/api/messages", {"contact_id": on_b["id"], "body": "嗨 Alice!"})
    flush(a, b)
    assert [(m["sender"], m["body"]) for m in a.messages(on_a["id"])] == [("me", "嗨 Bob"), ("them", "嗨 Alice!")]

    # 照片:先傳檔案再傳訊息,兩邊各有一份
    photo = send_file(a.owner, on_a["id"], PNG, "image/png", "貓.png", "image", "看貓")
    flush(a, b)
    m = b.messages(on_b["id"])[-1]
    assert m["kind"] == "image" and m["body"] == "看貓" and m["attachment"]["name"] == "貓.png"
    assert b.owner.raw(f"/api/files/{m['attachment']['id']}")[2] == PNG
    assert b.store.file_path(photo["attachment"]["id"]).exists()

    # 重送不會重複
    a.store.enqueue(on_a["id"], {"type": "message", "message_id": sent["id"]})
    flush(a, b)
    assert len(b.messages(on_b["id"])) == 3


def test_federation_queues_while_peer_offline(two_homes):
    a, b = two_homes
    on_a, on_b = link(a, b)
    flush(a, b)
    port = b.srv.server_address[1]
    b.stop()  # 對方電腦關機
    for text in ("第一則", "第二則", "第三則"):
        a.owner.req("/api/messages", {"contact_id": on_a["id"], "body": text})
    flush(a)
    contact = next(c for c in a.contacts() if c["id"] == on_a["id"])
    assert contact["peer"]["queued"] == 3 and "連不上" in contact["peer"]["error"]
    assert all(m["fed_state"] == "queued" for m in a.messages(on_a["id"]))
    b.start(port)  # 開機
    a.owner.req(f"/api/contacts/{on_a['id']}/peer-retry", {})
    flush(a, b)
    assert [m["body"] for m in b.messages(on_b["id"])] == ["第一則", "第二則", "第三則"]  # 順序不變
    contact = next(c for c in a.contacts() if c["id"] == on_a["id"])
    assert contact["peer"]["queued"] == 0 and contact["peer"]["error"] is None
    assert all(m["fed_state"] == "sent" for m in a.messages(on_a["id"]))


def test_federation_copies_existing_history(two_homes):
    a, b = two_homes
    # A 原本就有一個用邀請連結聊天的朋友
    mei = a.owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest = join(a.base, mei)
    a.owner.req("/api/messages", {"contact_id": mei["id"], "body": "以前的訊息"})
    guest.req("/api/messages", {"body": "以前的回覆"})
    a.owner.req("/api/contacts/import-line", {"text": LINE_TXT, "contact_name": "王小明"})
    # 小美後來自己也裝了 HomeChat(B),連到 A 的「小美」這個聯絡人
    on_a, on_b = link(a, b, contact_id=mei["id"])
    assert on_a["id"] == mei["id"]
    time.sleep(3.2)  # 被配對的一邊等對方存好連線資料才送
    flush(a, b)
    got = b.messages(on_b["id"])
    assert [(m["sender"], m["body"]) for m in got] == [("them", "以前的訊息"), ("me", "以前的回覆")]
    assert next(c for c in b.contacts() if c["id"] == on_b["id"])["unread"] == 0  # 舊訊息不算未讀
    # 小美之後用訪客身分在 A 傳的訊息,也會到 B
    guest.req("/api/messages", {"body": "我用網頁版傳的"})
    flush(a, b)
    assert b.messages(on_b["id"])[-1]["sender"] == "me"


def test_federation_security(two_homes):
    a, b = two_homes
    on_a, on_b = link(a, b)
    peer = a.store.peer_of(on_a["id"])
    # 互通碼只能用一次(重新配對會把連線移到新的聯絡人,密鑰也換新)
    code = a.owner.req("/api/fed/code", {})[1]["code"]
    assert b.owner.req("/api/fed/connect", {"code": code})[0] == 200
    assert b.owner.req("/api/fed/connect", {"code": code})[0] == 400
    # 不能用自己的互通碼
    own = b.owner.req("/api/fed/code", {})[1]["code"]
    assert b.owner.req("/api/fed/connect", {"code": own})[0] == 400
    assert b.owner.req("/api/fed/connect", {"code": "HC1.亂打"})[0] == 400
    # 對方網址一定要 https(本機測試例外)
    assert not homechat.valid_peer_url("http://example.com") and homechat.valid_peer_url("https://x.ts.net")
    # 偽造的請求;舊的密鑰在重新配對後也失效
    payload = {"type": "message", "uid": "fake1", "sender": "me", "kind": "text", "body": "偽造"}
    with pytest.raises(homechat.PeerError) as e:
        homechat.fed_request(b.base, "/fed/inbox", secret=peer["peer_secret"], server_id=a.store.server_id(),
                             payload=payload)
    assert e.value.status == 401
    peer = a.store.contact_by_peer(b.store.server_id())
    # 重新配對沿用原本的聯絡人,兩邊都不會多出一個
    assert peer["id"] == on_a["id"] and len(a.contacts()) == 1 and len(b.contacts()) == 1
    with pytest.raises(homechat.PeerError) as e:
        homechat.fed_request(b.base, "/fed/inbox", secret="wrong-secret", server_id=a.store.server_id(), payload=payload)
    assert e.value.status == 401
    with pytest.raises(homechat.PeerError):
        homechat.fed_request(b.base, "/fed/inbox", secret=peer["peer_secret"], server_id="someone-else", payload=payload)
    r = urllib.request.Request(b.base + "/fed/inbox", data=b"{}", method="POST",
                               headers={"Content-Type": "application/json"})
    with pytest.raises(urllib.error.HTTPError) as e2:
        urllib.request.urlopen(r, timeout=5)
    assert e2.value.code == 401
    assert all(m["body"] != "偽造" for m in b.messages(on_b["id"]))
    # 真的密鑰就收
    homechat.fed_request(b.base, "/fed/inbox", secret=peer["peer_secret"], server_id=a.store.server_id(), payload=payload)
    assert b.messages(on_b["id"])[-1]["body"] == "偽造"
    # 網頁的登入不能拿來打互通 API,互通的密鑰也不能拿來用網頁 API
    assert b.owner.req("/fed/inbox", {"type": "unpair"})[0] == 401


def test_federation_unlink_keeps_history(two_homes):
    a, b = two_homes
    on_a, on_b = link(a, b)
    a.owner.req("/api/messages", {"contact_id": on_a["id"], "body": "留著"})
    flush(a, b)
    assert a.owner.req(f"/api/contacts/{on_a['id']}/unlink", {})[0] == 200
    assert next(c for c in a.contacts() if c["id"] == on_a["id"])["peer"] is None
    assert next(c for c in b.contacts() if c["id"] == on_b["id"])["peer"] is None  # 對方也收到解除
    assert [m["body"] for m in b.messages(on_b["id"])] == ["留著"]
    # 解除後不再轉送
    a.owner.req("/api/messages", {"contact_id": on_a["id"], "body": "只在 A"})
    flush(a, b)
    assert [m["body"] for m in b.messages(on_b["id"])] == ["留著"]


def test_pair_code_roundtrip():
    code = homechat.encode_pair_code("https://a.ts.net", "tok", "小明")
    assert homechat.decode_pair_code(code) == {"u": "https://a.ts.net", "t": "tok", "n": "小明"}
    assert homechat.decode_pair_code(" " + code[:10] + "\n" + code[10:]) is not None  # 換行 / 空白沒關係
    assert homechat.decode_pair_code("abc") is None


# --- 大頭貼和狀態消息
def test_profile_avatar_and_status(server, monkeypatch):
    base, app = server
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    guest = join(base, contact)
    # 主人換大頭貼、打狀態
    assert owner.upload("/api/profile/avatar", PNG, "image/png")[0] == 200
    assert owner.req("/api/profile", {"status_msg": "今天好累\n😴   zzz"})[0] == 200
    me = guest.req("/api/me")[1]
    assert me["owner_status"] == "今天好累 😴 zzz" and me["owner_avatar"]
    status, headers, body = guest.raw(f"/api/avatar/owner?v={me['owner_avatar']}")
    assert status == 200 and body == PNG and headers["Content-Type"] == "image/png"
    # 朋友也能換自己的
    assert guest.upload("/api/profile/avatar", GIF, "image/gif")[0] == 200
    assert guest.req("/api/profile", {"status_msg": "x" * 300})[0] == 200
    c = next(x for x in owner.req("/api/contacts")[1]["contacts"] if x["id"] == contact["id"])
    assert c["avatar"] and c["status_msg"] == "x" * homechat.MAX_STATUS
    assert owner.raw(f"/api/avatar/{contact['id']}")[2] == GIF
    # 朋友看不到別的朋友的;沒登入看不到主人的
    other = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest2 = join(base, other, username="mei")
    assert guest2.raw(f"/api/avatar/{contact['id']}")[0] == 404
    assert Client(base).raw("/api/avatar/owner")[0] == 401
    # 不是圖片、沒有自訂標頭、太大都不收
    assert owner.upload("/api/profile/avatar", b"<svg onload=alert(1)>", "image/png")[0] == 415
    assert owner.upload("/api/profile/avatar", PNG, "image/png", {"X-HomeChat-Upload": "0"})[0] == 403
    monkeypatch.setattr(homechat, "MAX_AVATAR", 40)
    assert owner.upload("/api/profile/avatar", PNG + b"0" * 40, "image/png")[0] == 413
    monkeypatch.undo()
    # 移除照片
    assert owner.req("/api/profile", {"remove_avatar": True})[0] == 200
    assert guest.req("/api/me")[1]["owner_avatar"] == ""
    assert owner.raw("/api/avatar/owner")[0] == 404
    # 刪聯絡人時朋友的大頭貼也刪掉
    owner.req(f"/api/contacts/{contact['id']}/delete", {})
    assert not app.store.avatar_path(f"c{contact['id']}").exists()


def test_federation_shares_profile(two_homes):
    a, b = two_homes
    a.owner.upload("/api/profile/avatar", PNG, "image/png")
    a.owner.req("/api/profile", {"status_msg": "在家 🏠"})
    on_a, on_b = link(a, b)
    for h in (a, b):  # 配對後延遲送出的也馬上送
        h.store.db.execute("UPDATE fed_outbox SET next_try = 0")
    flush(a, b)
    alice_on_b = next(c for c in b.contacts() if c["id"] == on_b["id"])
    assert alice_on_b["status_msg"] == "在家 🏠" and alice_on_b["avatar"]
    assert b.owner.raw(f"/api/avatar/{on_b['id']}")[2] == PNG
    # 之後改了也會同步;移除照片也會
    b.owner.req("/api/profile", {"status_msg": "上班中"})
    a.owner.req("/api/profile", {"remove_avatar": True})
    flush(a, b)
    assert next(c for c in a.contacts() if c["id"] == on_a["id"])["status_msg"] == "上班中"
    alice_on_b = next(c for c in b.contacts() if c["id"] == on_b["id"])
    assert alice_on_b["avatar"] == "" and b.owner.raw(f"/api/avatar/{on_b['id']}")[0] == 404


# --- 推播通知
def sse(client, visible=True):
    """打開即時事件,回傳 (連線, 讀下一個事件的函式)。"""
    resp = client.opener.open(client.base + "/api/events?visible=" + ("1" if visible else "0"), timeout=5)
    resp.readline()  # retry
    hello = json.loads(resp.readline().decode()[len("data: "):])
    resp.readline()

    def next_event(kind=None):
        while True:
            line = resp.readline().decode()
            if line.startswith("data: "):
                ev = json.loads(line[len("data: "):])
                if kind is None or ev.get("type") == kind:
                    return ev
    return resp, hello["sub"], next_event


def test_vapid_signature_and_jwt():
    d, public = homechat.vapid_keys()
    assert len(public) == 65 and public[0] == 4
    sig = homechat.ecdsa_sign(d, b"hello")
    assert homechat.ecdsa_verify(public, b"hello", sig)
    assert not homechat.ecdsa_verify(public, b"hellp", sig)
    store = homechat.Store(":memory:")
    pusher = homechat.Pusher(store)
    token = pusher._jwt("https://fcm.googleapis.com")
    head, body, sig = token.split(".")
    pad = lambda x: x + "=" * (-len(x) % 4)
    claims = json.loads(base64.urlsafe_b64decode(pad(body)))
    assert claims["aud"] == "https://fcm.googleapis.com" and claims["exp"] > time.time()
    key = base64.urlsafe_b64decode(pad(store.vapid()[1]))
    assert homechat.ecdsa_verify(key, f"{head}.{body}".encode(), base64.urlsafe_b64decode(pad(sig)))


def test_push_endpoint_must_be_a_push_service():
    ok = homechat.valid_push_endpoint
    assert ok("https://fcm.googleapis.com/fcm/send/abc")
    assert ok("https://web.push.apple.com/QGx")
    assert ok("https://updates.push.services.mozilla.com/wpush/v2/x")
    assert ok("https://wns2-par02p.notify.windows.com/w/?token=x")
    assert not ok("http://fcm.googleapis.com/x")
    assert not ok("https://127.0.0.1/x")
    assert not ok("https://fcm.googleapis.com.evil.com/x")
    assert not ok("https://evilfcm.googleapis.com.example/x")


def test_push_notification_when_app_closed(server, monkeypatch):
    base, app = server
    sent = []
    monkeypatch.setattr(homechat.Pusher, "send", lambda self, ep: sent.append(ep) or 201)
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    guest = join(base, contact)
    ep = "https://fcm.googleapis.com/fcm/send/guest1"
    assert guest.req("/api/push/subscribe", {"subscription": {"endpoint": "https://10.0.0.1/x"}})[0] == 400
    assert guest.req("/api/push/subscribe", {"subscription": {"endpoint": ep}, "vibrate": False})[0] == 200
    assert guest.req("/api/push/key")[1]["key"] == app.store.vapid()[1]

    def wait_sent(n):
        for _ in range(50):
            if len(sent) >= n:
                return True
            time.sleep(0.05)
        return False

    # 朋友沒開著 → 推播
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "在嗎?"})
    assert wait_sent(1) and sent[-1] == ep
    info = guest.req("/api/notify")[1]
    assert info["title"] == "Paul" and info["body"] == "在嗎?" and info["vibrate"] is False
    # 朋友正在看 → 不推播(網頁自己提醒)
    resp, sub, _ = sse(guest, visible=True)
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "第二則"})
    time.sleep(0.3)
    assert len(sent) == 1
    # 切到背景 → 推播
    guest.req("/api/presence", {"sub": sub, "visible": False})
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "第三則"})
    assert wait_sent(2)
    assert "共 3 則未讀" in guest.req("/api/notify")[1]["body"]
    resp.close()
    # 測試通知
    guest.req("/api/push/test", {})
    assert wait_sent(3)
    assert "測試" in guest.req("/api/notify")[1]["body"]
    # 主人收到朋友的訊息(主人沒有訂閱 → 不送)
    guest.req("/api/messages", {"contact_id": contact["id"], "body": "回覆"})
    time.sleep(0.2)
    assert len(sent) == 3
    # 登出後訂閱跟著消失
    guest.req("/api/logout", {})
    assert app.store.push_subs_for("guest", contact["id"]) == []


# --- 語音通話
def test_voice_call_flow(server, monkeypatch):
    base, app = server
    monkeypatch.setattr(homechat.Pusher, "send", lambda self, ep: 201)
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    # 還沒加入不能打
    assert owner.req("/api/call/start", {"contact_id": contact["id"]})[0] == 400
    guest = join(base, contact)
    o_resp, _, o_next = sse(owner)
    g_resp, _, g_next = sse(guest)
    status, data = owner.req("/api/call/start", {"contact_id": contact["id"]})
    assert status == 200 and data["ice_servers"][0]["urls"]
    call_id = data["call_id"]
    ring = g_next("call")
    assert ring["action"] == "ring" and ring["call_id"] == call_id
    assert guest.req("/api/call")[1]["call"]["call_id"] == call_id
    assert "打電話給你" in guest.req("/api/notify")[1]["body"]
    # 通話中不能再打
    assert guest.req("/api/call/start", {"contact_id": contact["id"]})[0] == 409
    # 自己不能接自己打的
    assert owner.req("/api/call/answer", {"call_id": call_id})[0] == 400
    assert guest.req("/api/call/answer", {"call_id": call_id})[0] == 200
    assert o_next("call")["action"] == "answered"
    assert g_next("call")["action"] == "taken"
    # 連線資訊互相轉送
    owner.req("/api/call/signal", {"call_id": call_id, "data": {"sdp": {"type": "offer", "sdp": "v=0"}}})
    sig = g_next("call")
    assert sig["action"] == "signal" and sig["data"]["sdp"]["type"] == "offer"
    guest.req("/api/call/signal", {"call_id": call_id, "data": {"candidate": {"candidate": "x"}}})
    assert o_next("call")["data"]["candidate"]["candidate"] == "x"
    # 別的朋友插不進來
    other = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    intruder = join(base, other)
    assert intruder.req("/api/call/signal", {"call_id": call_id, "data": {"sdp": {}}})[0] == 404
    assert intruder.req("/api/call/end", {"call_id": call_id})[0] == 404
    # 掛斷 → 兩邊收到、對話裡有通話紀錄
    assert guest.req("/api/call/end", {"call_id": call_id})[0] == 200
    assert o_next("call")["action"] == "end"
    msgs = owner.req(f"/api/messages?contact={contact['id']}")[1]["messages"]
    assert msgs[-1]["body"].startswith("📞 語音通話 0:0") and msgs[-1]["sender"] == "me"
    o_resp.close(); g_resp.close()


def test_missed_and_declined_calls(server, monkeypatch):
    base, app = server
    monkeypatch.setattr(homechat.Pusher, "send", lambda self, ep: 201)
    monkeypatch.setattr(homechat, "CALL_RING_SECONDS", 0.3)
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    guest = join(base, contact)
    resp, _, _ = sse(guest)
    guest.req("/api/call/start", {"contact_id": contact["id"]})
    time.sleep(0.8)
    msgs = owner.req(f"/api/messages?contact={contact['id']}")[1]["messages"]
    assert msgs[-1]["body"] == "📞 未接來電" and msgs[-1]["sender"] == "them"
    call_id = guest.req("/api/call/start", {"contact_id": contact["id"]})[1]["call_id"]
    owner.req("/api/call/end", {"call_id": call_id})  # 主人拒接
    assert owner.req(f"/api/messages?contact={contact['id']}")[1]["messages"][-1]["body"] == "📞 對方拒接"
    resp.close()


def test_turn_settings(server):
    base, _ = server
    owner = login(base)
    assert owner.req("/api/settings", {"turn_url": "http://x"})[0] == 400
    assert owner.req("/api/settings", {"turn_url": "turn:turn.example.com:3478", "turn_user": "u", "turn_pass": "p"})[0] == 200
    contact = owner.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    join(base, contact)
    ice = owner.req("/api/call/start", {"contact_id": contact["id"]})[1]["ice_servers"]
    assert ice[-1] == {"urls": ["turn:turn.example.com:3478"], "username": "u", "credential": "p"}


def test_video_call(server, monkeypatch):
    base, app = server
    monkeypatch.setattr(homechat.Pusher, "send", lambda self, ep: 201)
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    guest = join(base, contact)
    g_resp, _, g_next = sse(guest)
    call_id = owner.req("/api/call/start", {"contact_id": contact["id"], "video": True})[1]["call_id"]
    ring = g_next("call")
    assert ring["video"] is True
    assert guest.req("/api/call")[1]["call"]["video"] is True
    assert guest.req("/api/notify")[1]["title"] == "📹 視訊來電"
    guest.req("/api/call/answer", {"call_id": call_id})
    owner.req("/api/call/end", {"call_id": call_id})
    msgs = owner.req(f"/api/messages?contact={contact['id']}")[1]["messages"]
    assert msgs[-1]["body"].startswith("📹 視訊通話 0:0")
    _, headers, _ = owner.raw("/")
    assert "camera=(self)" in headers["Permissions-Policy"]
    g_resp.close()


# --- 幫朋友裝 HomeChat:連結 → 安裝程式 → 自動互通
def test_setup_link_auto_pairs(two_homes):
    a, b = two_homes
    contact = a.owner.req("/api/contacts", {"name": "Bob"})[1]["contact"]
    a.owner.req("/api/messages", {"contact_id": contact["id"], "body": "以前在 Alice 家聊的"})
    link = a.owner.req("/api/fed/setup-link", {"contact_id": contact["id"]})[1]
    token = link["url"].rsplit("/", 1)[1]
    status, headers, page = a.owner.raw(f"/setup/{token}")
    assert status == 200 and "Alice 邀請你用 HomeChat" in page.decode()
    zdata = Client(a.base).raw(f"/setup/{token}/HomeChat.zip")[2]
    code = zipfile.ZipFile(io.BytesIO(zdata)).read("pair.txt").decode().strip()
    bat = Client(a.base).raw(f"/setup/{token}/HomeChat-Setup.bat")[2].decode("ascii")
    assert f"/setup/{token}/HomeChat.zip" in bat and "install-windows.ps1" in bat
    # Bob 的電腦:安裝程式把 pair.txt 放好 → 啟動時記下來 → 有網址後自動互通
    b.store.set_setting("pending_pair", code)
    assert b.owner.req("/api/me")[1]["pending_pair"] == "Alice"
    b.app.try_auto_pair()
    assert b.store.get_setting("pending_pair") == ""
    on_b = next(c for c in b.contacts() if c["peer"])
    on_a = next(c for c in a.contacts() if c["id"] == contact["id"])
    assert on_b["name"] == "Alice" and on_a["peer"]
    flush(a, b)
    for h in (a, b):
        h.store.db.execute("UPDATE fed_outbox SET next_try = 0")
    flush(a, b)
    assert [m["body"] for m in b.messages(on_b["id"])] == ["以前在 Alice 家聊的"]
    # 連結用過就失效
    assert "連結已失效" in a.owner.raw(f"/setup/{token}")[2].decode()
    assert Client(a.base).raw(f"/setup/{token}/HomeChat.zip")[0] == 410


def test_auto_pair_bad_code_reports_error(two_homes):
    _, b = two_homes
    b.store.set_setting("pending_pair", homechat.encode_pair_code(b.base.replace("127.0.0.1", "localhost"), "x" * 20, "X"))
    b.app.try_auto_pair()
    assert b.store.get_setting("pending_pair") == ""
    assert b.owner.req("/api/me")[1]["pending_pair_error"]


def test_peer_url_change_is_announced(two_homes):
    a, b = two_homes
    on_a, on_b = link(a, b)
    flush(a, b)
    a.app.fed.announce_url()
    flush(a, b)
    new = a.base.replace("127.0.0.1", "localhost")
    a.store.set_setting("public_url", new)
    a.app.fed.announce_url()
    flush(a, b)
    assert b.store.peer_of(on_b["id"])["peer_url"] == new


def test_federation_syncs_albums_and_sticker_packs(two_homes):
    a, b = two_homes
    # 互通前就有的相簿也會複製過去
    bob_on_a = a.owner.req("/api/contacts", {"name": "Bob"})[1]["contact"]
    old = a.owner.req("/api/albums", {"contact_id": bob_on_a["id"], "name": "舊相簿"})[1]["album"]
    att = upload_photo(a.owner, bob_on_a["id"], "old.png")
    a.owner.req(f"/api/albums/{old['id']}/add", {"attachment_ids": [att["id"]]})
    on_a, on_b = link(a, b, bob_on_a["id"])
    for h in (a, b):
        h.store.db.execute("UPDATE fed_outbox SET next_try = 0")
    flush(a, b)
    albums_b = b.owner.req(f"/api/albums?contact={on_b['id']}")[1]["albums"]
    assert [(x["name"], x["count"], x["created_by"]) for x in albums_b] == [("舊相簿", 1, "them")]
    # 聊天裡的相簿卡片在 Bob 那邊打得開(對到 Bob 自己的相簿編號)
    card = next(m for m in b.messages(on_b["id"]) if m["kind"] == "album")
    assert json.loads(card["body"])["album_id"] == albums_b[0]["id"]
    # Bob 建新相簿、加 3 張照片 → Alice 那邊也有
    new = b.owner.req("/api/albums", {"contact_id": on_b["id"], "name": "旅行"})[1]["album"]
    ids = [upload_photo(b.owner, on_b["id"], f"t{i}.png", PNG + bytes([i]))["id"] for i in range(3)]
    b.owner.req(f"/api/albums/{new['id']}/add", {"attachment_ids": ids})
    flush(a, b)
    trip_a = next(x for x in a.owner.req(f"/api/albums?contact={on_a['id']}")[1]["albums"] if x["name"] == "旅行")
    photos_a = a.owner.req(f"/api/albums/{trip_a['id']}/photos")[1]["photos"]
    assert len(photos_a) == 3 and all(p["added_by"] == "them" for p in photos_a)
    assert a.owner.raw(f"/api/files/{photos_a[0]['attachment']['id']}")[0] == 200
    # 改名、移除照片、刪除也同步
    b.owner.req(f"/api/albums/{new['id']}/rename", {"name": "沖繩旅行"})
    photo_b = b.owner.req(f"/api/albums/{new['id']}/photos")[1]["photos"][0]
    b.owner.req(f"/api/albums/{new['id']}/remove", {"photo_id": photo_b["id"]})
    flush(a, b)
    trip_a = a.store.get_album(trip_a["id"])
    assert trip_a["name"] == "沖繩旅行" and len(a.store.album_photos(trip_a["id"])) == 2
    a.owner.req(f"/api/albums/{trip_a['id']}/delete", {})
    flush(a, b)
    assert [x["name"] for x in b.owner.req(f"/api/albums?contact={on_b['id']}")[1]["albums"]] == ["舊相簿"]
    # 貼圖組:整組到對方,對方可以一次收下
    sids = [add_sticker(a.owner)[1]["sticker"]["id"] for _ in range(3)]
    a.owner.req("/api/stickers/share", {"contact_id": on_a["id"], "sticker_ids": sids})
    flush(a, b)
    pack = next(m for m in b.messages(on_b["id"]) if m["kind"] == "stickers")
    items = [i["id"] for i in json.loads(pack["body"])["items"]]
    assert len(items) == 3 and all(b.owner.raw(f"/api/files/{i}")[0] == 200 for i in items)
    saved = b.owner.req("/api/stickers/save", {"attachment_ids": items})[1]["stickers"]
    assert len(saved) == 3


def test_federated_call(two_homes, monkeypatch):
    a, b = two_homes
    monkeypatch.setattr(homechat.Pusher, "send", lambda self, ep: 201)
    on_a, on_b = link(a, b)
    flush(a, b)
    a_resp, _, a_next = sse(a.owner)
    b_resp, _, b_next = sse(b.owner)
    call_id = a.owner.req("/api/call/start", {"contact_id": on_a["id"], "video": True})[1]["call_id"]
    ring = b_next("call")
    assert ring["action"] == "ring" and ring["contact_id"] == on_b["id"] and ring["video"] is True
    assert b.owner.req("/api/call")[1]["call"]["call_id"] == call_id
    assert b.owner.req("/api/call/answer", {"call_id": call_id})[0] == 200
    assert a_next("call")["action"] == "answered"
    assert b_next("call")["action"] == "taken"
    a.owner.req("/api/call/signal", {"call_id": call_id, "data": {"sdp": {"type": "offer", "sdp": "v=0"}}})
    assert b_next("call")["data"]["sdp"]["type"] == "offer"
    b.owner.req("/api/call/signal", {"call_id": call_id, "data": {"candidate": {"candidate": "c1"}}})
    assert a_next("call")["data"]["candidate"]["candidate"] == "c1"
    b.owner.req("/api/call/end", {"call_id": call_id})
    assert a_next("call")["action"] == "end"
    for _ in range(50):  # 等 Alice 那邊寫好通話紀錄
        if any("通話" in m["body"] for m in a.messages(on_a["id"])):
            break
        time.sleep(0.05)
    for _ in range(50):  # 背景也在送:等 Bob 那邊收到
        flush(a, b)
        if any("通話" in m["body"] for m in b.messages(on_b["id"])):
            break
        time.sleep(0.05)
    logs_a = [m["body"] for m in a.messages(on_a["id"]) if "通話" in m["body"]]
    logs_b = [m["body"] for m in b.messages(on_b["id"]) if "通話" in m["body"]]
    assert len(logs_a) == 1 and logs_a == logs_b and logs_a[0].startswith("📹 視訊通話")
    # Bob 拒接 → Alice 這邊「對方拒接」
    call_id = a.owner.req("/api/call/start", {"contact_id": on_a["id"]})[1]["call_id"]
    b_next("call")
    b.owner.req("/api/call/end", {"call_id": call_id})
    assert a_next("call")["reason"] == "declined"
    for _ in range(50):
        if a.messages(on_a["id"])[-1]["body"] == "📞 對方拒接":
            break
        time.sleep(0.05)
    assert a.messages(on_a["id"])[-1]["body"] == "📞 對方拒接"
    # Bob 的電腦關機 → 打不通
    b_resp.close()
    b.stop()
    assert a.owner.req("/api/call/start", {"contact_id": on_a["id"]})[0] == 502
    a_resp.close()


def test_push_goes_to_phone_while_pc_tab_is_open(server, monkeypatch):
    """家裡電腦的 HomeChat 分頁一直開著,口袋裡的手機還是要收到通知。"""
    base, app = server
    sent = []
    monkeypatch.setattr(homechat.Pusher, "send", lambda self, ep: sent.append(ep) or 201)

    def wait_sent(n):
        for _ in range(60):
            if len(sent) >= n:
                return True
            time.sleep(0.05)
        return False
    pc = login(base)
    phone = login(base)
    contact = pc.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    guest = join(base, contact)
    phone.req("/api/push/subscribe", {"subscription": {"endpoint": "https://fcm.googleapis.com/fcm/send/phone"}})
    pc_resp, _, _ = sse(pc, visible=True)  # 電腦的分頁開著
    guest.req("/api/messages", {"contact_id": contact["id"], "body": "在嗎"})
    assert wait_sent(1) and sent[-1].endswith("/phone")
    # 來電也一樣
    guest.req("/api/call/start", {"contact_id": contact["id"]})
    assert wait_sent(2)
    pc_resp.close()


def test_stale_connection_does_not_block_push(server, monkeypatch):
    """手機關掉網頁,但經過通道的連線沒斷:停止回報後還是要推播。"""
    base, app = server
    sent = []
    monkeypatch.setattr(homechat.Pusher, "send", lambda self, ep: sent.append(ep) or 201)
    monkeypatch.setattr(homechat, "VISIBLE_TTL", 0.3)
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "阿明"})[1]["contact"]
    guest = join(base, contact)
    guest.req("/api/push/subscribe", {"subscription": {"endpoint": "https://fcm.googleapis.com/fcm/send/g"}})
    resp, sub, _ = sse(guest, visible=True)
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "一"})
    time.sleep(0.2)
    assert sent == []  # 正在看:網頁自己提醒
    time.sleep(0.4)  # 網頁被關掉,不再回報
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "二"})
    for _ in range(40):
        if sent:
            break
        time.sleep(0.05)
    assert sent
    # 再回報「正在看」就又不推
    guest.req("/api/presence", {"sub": sub, "visible": True})
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "三"})
    time.sleep(0.2)
    assert len(sent) == 1
    resp.close()


def test_owner_can_see_friend_notification_status(server, monkeypatch):
    base, app = server
    results = iter([201, 403])

    def fake_send(self, ep):
        status = next(results)
        self.store.record_push(ep, status, "" if status < 400 else "invalid JWT")
        return status
    monkeypatch.setattr(homechat.Pusher, "send", fake_send)
    owner = login(base)
    contact = owner.req("/api/contacts", {"name": "小美"})[1]["contact"]
    guest = join(base, contact)
    # 還沒開通知:打電話時提醒「對方可能不會知道」
    r = owner.req("/api/call/start", {"contact_id": contact["id"]})[1]
    assert r["reachable"] is False
    owner.req("/api/call/end", {"call_id": r["call_id"]})
    dev = owner.req(f"/api/devices?contact={contact['id']}")[1]["devices"][0]
    assert dev["push"] is None and dev["open"] is False
    # 開了通知 → 看得到;推播成功 / 失敗也看得到
    guest.req("/api/push/subscribe", {"subscription": {"endpoint": "https://fcm.googleapis.com/fcm/send/x"}})
    r = owner.req("/api/call/start", {"contact_id": contact["id"]})[1]
    assert r["reachable"] is True
    owner.req("/api/call/end", {"call_id": r["call_id"]})
    for _ in range(40):
        dev = owner.req(f"/api/devices?contact={contact['id']}")[1]["devices"][0]
        if dev["push"].get("status"):
            break
        time.sleep(0.05)
    assert dev["push"]["status"] == 201
    owner.req("/api/messages", {"contact_id": contact["id"], "body": "hi"})
    for _ in range(40):
        dev = owner.req(f"/api/devices?contact={contact['id']}")[1]["devices"][0]
        if dev["push"]["status"] == 403:
            break
        time.sleep(0.05)
    assert dev["push"]["status"] == 403 and "JWT" in dev["push"]["error"]
