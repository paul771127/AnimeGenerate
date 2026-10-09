"""HomeChat 測試:啟動真的伺服器,用 HTTP 走過主人 / 訪客的完整流程。"""
import json
import sys
import threading
import urllib.error
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
    assert c.req("/api/login", {"password": "secret123"})[0] == 200
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
    assert c.req("/api/login", {"password": "nope"})[0] == 401
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


def join(base, contact):
    g = Client(base)
    status, _ = g.req("/api/join", {"token": invite_path(contact)})
    assert status == 200
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
    assert guest.req(f"/api/invite?token={token}")[1] == {"valid": True, "owner_name": "Paul", "name": "阿明"}
    # 按下「開始聊天」才加入
    assert guest.req("/api/join", {"token": token})[0] == 200
    me = guest.req("/api/me")[1]
    assert me["role"] == "guest" and me["contact"]["name"] == "阿明" and me["contact"]["status"] == "active"

    # 連結只能用一次:別人再用就失效
    stranger = Client(base)
    assert stranger.req("/api/join", {"token": token})[0] == 410
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
    assert Client(base).req("/api/join", {"token": token})[0] == 410

    c = owner.req(f"/api/contacts/{contact['id']}/invite", {})[1]["contact"]
    owner.req(f"/api/contacts/{contact['id']}/cancel-invite", {})
    assert Client(base).req("/api/join", {"token": invite_path(c)})[0] == 410
    # 新連結會讓舊的未使用連結失效
    c1 = owner.req(f"/api/contacts/{contact['id']}/invite", {})[1]["contact"]
    c2 = owner.req(f"/api/contacts/{contact['id']}/invite", {})[1]["contact"]
    assert Client(base).req("/api/join", {"token": invite_path(c1)})[0] == 410
    assert Client(base).req("/api/join", {"token": invite_path(c2)})[0] == 200


def test_friend_request_flow(server):
    base, _ = server
    owner = login(base)
    link = owner.req("/api/friend-link")[1]
    assert link == {"enabled": False, "url": ""}
    link = owner.req("/api/friend-link", {"enabled": True})[1]
    token = link["url"].split("/add/")[1].split("?")[0]

    friend = Client(base)
    assert friend.req(f"/api/add-info?token={token}")[1] == {"valid": True, "owner_name": "Paul"}
    assert friend.req("/api/request", {"token": "wrong", "name": "x"})[0] == 410
    assert friend.req("/api/request", {"token": token, "name": "  "})[0] == 400
    assert friend.req("/api/request", {"token": token, "name": "小華", "message": "我是國中同學"})[0] == 200
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
    other.req("/api/request", {"token": token, "name": "陌生人"})
    cid = other.req("/api/me")[1]["contact"]["id"]
    owner.req(f"/api/contacts/{cid}/delete", {})
    assert other.req("/api/me")[1]["role"] is None

    # 換新連結 / 關閉後舊連結失效
    owner.req("/api/friend-link", {"reset": True})
    assert Client(base).req("/api/request", {"token": token, "name": "y"})[0] == 410


def test_friend_request_rate_limit(server):
    base, _ = server
    owner = login(base)
    token = owner.req("/api/friend-link", {"enabled": True})[1]["url"].split("/add/")[1].split("?")[0]
    codes = [Client(base).req("/api/request", {"token": token, "name": f"人{i}"})[0] for i in range(7)]
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
        assert c.req("/api/setup", {"password": "short"})[0] == 400
        assert c.req("/api/setup", {"password": "abcdefgh", "owner_name": "阿保"})[0] == 200
        me = c.req("/api/me")[1]
        assert me["role"] == "owner" and me["owner_name"] == "阿保"
        assert Client(base).req("/api/setup", {"password": "hijacked1"})[0] == 403
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
    assert Client(base).req("/api/login", {"password": "newpass123"})[0] == 200


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
    assert c.req("/api/login", {"password": "secret123"}, headers=origin)[0] == 200
    status, data = c.req("/api/contacts", {"name": "外面的朋友"}, headers=origin)
    assert status == 200
    assert data["contact"]["invite_url"].startswith("https://home-pc.tail1234.ts.net/c/")
    assert c.req("/api/contacts", {"name": "x"}, headers={"Origin": "https://evil.example"})[0] == 403
