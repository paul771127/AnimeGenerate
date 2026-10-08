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

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "homechat"))
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


def test_owner_and_guest_chat(server):
    base, _ = server
    owner = login(base)
    status, data = owner.req("/api/contacts", {"name": "阿明", "line_id": "ming"})
    assert status == 200
    contact = data["contact"]
    assert contact["invite_url"].startswith(base + "/c/")

    owner.req("/api/messages", {"contact_id": contact["id"], "body": "嗨,這是我家的聊天室"})

    guest = Client(base)
    status, page = guest.req(contact["invite_url"][len(base):])
    assert status == 200 and "HomeChat" in page  # 轉址到首頁
    me = guest.req("/api/me")[1]
    assert me == {"role": "guest", "owner_name": "Paul",
                  "contact": {"id": contact["id"], "name": "阿明", "owner_read_id": me["contact"]["owner_read_id"]}}

    msgs = guest.req("/api/messages")[1]["messages"]
    assert [m["body"] for m in msgs] == ["嗨,這是我家的聊天室"]
    # 訪客不能指定別的聯絡人,也不能看聯絡人列表
    _, other = owner.req("/api/contacts", {"name": "別人"})
    owner.req("/api/messages", {"contact_id": other["contact"]["id"], "body": "秘密"})
    assert [m["body"] for m in guest.req(f"/api/messages?contact={other['contact']['id']}")[1]["messages"]] == ["嗨,這是我家的聊天室"]
    assert guest.req("/api/contacts")[0] == 403

    _, sent = guest.req("/api/messages", {"contact_id": other["contact"]["id"], "body": "你好!"})
    assert sent["message"]["sender"] == "them" and sent["message"]["contact_id"] == contact["id"]

    contacts = {c["name"]: c for c in owner.req("/api/contacts")[1]["contacts"]}
    assert contacts["阿明"]["unread"] == 1
    owner.req("/api/read", {"contact_id": contact["id"], "upto": sent["message"]["id"]})
    contacts = {c["name"]: c for c in owner.req("/api/contacts")[1]["contacts"]}
    assert contacts["阿明"]["unread"] == 0

    # 換新連結後訪客失效
    owner.req(f"/api/contacts/{contact['id']}/reset-invite", {})
    assert guest.req("/api/me")[1]["role"] is None
    assert guest.req(contact["invite_url"][len(base):])[0] == 404


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
