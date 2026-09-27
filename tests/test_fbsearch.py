"""fbsearch 測試(只用標準函式庫,unittest / pytest 都可跑)。"""

import json
import tempfile
import threading
import unittest
import urllib.request
import zipfile
from http.server import ThreadingHTTPServer
from pathlib import Path

from fbsearch.db import Item, Query, Store
from fbsearch.importer import fix_mojibake, iter_export
from fbsearch.price import extract_price, extract_prices
from fbsearch.server import make_handler


def moji(s: str) -> str:
    """模擬 Facebook 匯出的亂碼。"""
    return s.encode("utf-8").decode("latin-1")


RECENTLY_VIEWED = {
    "recently_viewed": [
        {
            "name": "Facebook Watch Videos and Shows",
            "description": "Videos you've watched",
            "children": [
                {"name": "Time Viewed", "entries": [
                    {"timestamp": 1758900000,
                     "data": {"name": moji("貓咪搞笑影片合輯"), "uri": "https://www.facebook.com/watch/?v=111"}},
                    {"timestamp": 1758800000,
                     "data": {"name": "Street food tour", "uri": "https://www.facebook.com/watch/?v=222"}},
                ]},
            ],
        },
        {
            "name": "Posts that have been shown to you in your Feed",
            "entries": [
                {"timestamp": 1758700000,
                 "data": {"name": moji("露營裝備分享,帳篷只要 3,500 元"), "uri": "https://www.facebook.com/groups/1/posts/9"}},
            ],
        },
        {
            "name": "Marketplace Interactions",
            "children": [
                {"name": "Marketplace Items you've viewed", "entries": [
                    {"timestamp": 1758600000,
                     "data": {"value": moji("Sony A7III 二手機身 NT$32,000"), "uri": "https://www.facebook.com/marketplace/item/5"}},
                    {"timestamp": 1758500000,
                     "data": {"value": moji("IKEA 沙發 1.2萬"), "uri": "https://www.facebook.com/marketplace/item/6"}},
                ]},
            ],
        },
    ]
}

SAVED = {
    "saves_v2": [
        {"timestamp": 1758400000, "title": moji("王小明儲存了一個連結。"),
         "attachments": [{"data": [{"external_context": {
             "name": moji("台北 10 間必吃拉麵"), "source": "Blog", "url": "https://example.com/ramen"}}]}]},
    ]
}

MY_POSTS = [
    {"timestamp": 1758300000, "data": [{"post": moji("出清 Switch 主機,售價 6500 可議")}],
     "title": moji("王小明更新了他的動態。")},
]


class PriceTest(unittest.TestCase):
    def test_formats(self):
        cases = {
            "售價 NT$18,000 可議": 18000, "1200元": 1200, "3萬5 含運": 35000, "價格:1.2萬": 12000,
            "$1,299.5": 1299.5, "NT$2k": 2000, "每個 50 元": 50, "開價800": 800, "800 NTD": 800,
        }
        for s, want in cases.items():
            self.assertEqual(extract_price(s), want, s)

    def test_no_false_positive(self):
        self.assertEqual(extract_prices("電話 0912345678,2023 年款 iPhone 15"), [])

    def test_multiple(self):
        self.assertEqual(extract_prices("大的 500元,小的 300 元"), [500, 300])


class ImportTest(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        root = self.tmp / "export"
        (root / "your_activity_across_facebook").mkdir(parents=True)
        (root / "your_facebook_activity/saved_items_and_collections").mkdir(parents=True)
        (root / "your_facebook_activity/posts").mkdir(parents=True)
        (root / "your_facebook_activity/messages").mkdir(parents=True)
        (root / "your_activity_across_facebook/recently_viewed.json").write_text(json.dumps(RECENTLY_VIEWED))
        (root / "your_facebook_activity/saved_items_and_collections/your_saved_items.json").write_text(json.dumps(SAVED))
        (root / "your_facebook_activity/posts/your_posts__check_ins__photos_and_videos_1.json").write_text(json.dumps(MY_POSTS))
        (root / "your_facebook_activity/messages/inbox.json").write_text(json.dumps([{"timestamp": 1, "text": "私訊"}]))
        self.root = root

    def _items(self, path):
        return [it for _, items in iter_export(path) for it in items]

    def test_mojibake(self):
        self.assertEqual(fix_mojibake(moji("中文測試")), "中文測試")
        self.assertEqual(fix_mojibake("plain"), "plain")
        self.assertEqual(fix_mojibake("已經是中文"), "已經是中文")

    def test_folder(self):
        items = self._items(self.root)
        by_url = {it.url: it for it in items}
        self.assertEqual(len(items), 7)  # 私訊不匯入
        v = by_url["https://www.facebook.com/watch/?v=111"]
        self.assertEqual((v.kind, v.source, v.title), ("video", "viewed", "貓咪搞笑影片合輯"))
        feed = by_url["https://www.facebook.com/groups/1/posts/9"]
        self.assertEqual((feed.kind, feed.source), ("post", "viewed"))
        m = by_url["https://www.facebook.com/marketplace/item/5"]
        self.assertEqual((m.kind, m.source), ("marketplace", "viewed"))
        s = by_url["https://example.com/ramen"]
        self.assertEqual(s.source, "saved")
        self.assertIn("台北 10 間必吃拉麵", s.text + s.title)
        mine = [it for it in items if it.source == "posted"]
        self.assertEqual(len(mine), 1)
        self.assertEqual(mine[0].kind, "post")  # 檔名有 photos_and_videos 也不該變影片
        self.assertTrue(mine[0].title.startswith("出清 Switch"))  # 系統句子不當標題

    def test_zip(self):
        z = self.tmp / "facebook-export.zip"
        with zipfile.ZipFile(z, "w") as zf:
            for f in self.root.rglob("*.json"):
                zf.write(f, f.relative_to(self.root).as_posix())
        self.assertEqual(len(self._items(z)), 7)


class StoreTest(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.store = Store(self.tmp / "t.db")
        root = self.tmp / "export"
        root.mkdir()
        (root / "recently_viewed.json").write_text(json.dumps(RECENTLY_VIEWED))
        (root / "your_saved_items.json").write_text(json.dumps(SAVED))
        (root / "your_posts_1.json").write_text(json.dumps(MY_POSTS))
        for _, items in iter_export(root):
            self.store.upsert_many(items)
        self.root = root

    def s(self, **kw):
        return self.store.search(Query(**kw))

    def test_dedupe_on_reimport(self):
        before = self.store.stats()["total"]
        new = sum(self.store.upsert_many(items)[0] for _, items in iter_export(self.root))
        self.assertEqual(new, 0)
        self.assertEqual(self.store.stats()["total"], before)

    def test_fulltext_chinese(self):
        self.assertEqual(self.s(q="搞笑影片")["total"], 1)
        self.assertEqual(self.s(q="沙發")["total"], 1)  # 兩個字 → LIKE
        self.assertEqual(self.s(q="sony")["total"], 1)  # 大小寫不敏感
        self.assertEqual(self.s(q="影片 -貓咪")["total"], 0)
        self.assertEqual(self.s(q='"Street food"')["total"], 1)

    def test_price_range(self):
        r = self.s(price_min=3000, price_max=15000, sort="price_asc")
        self.assertEqual([it["price"] for it in r["items"]], [3500, 6500, 12000])
        self.assertEqual(self.s(price_min=30000)["items"][0]["price"], 32000)

    def test_recent_viewed_videos(self):
        r = self.s(kind="video", source="viewed", sort="recent")
        self.assertEqual([it["ts"] for it in r["items"]], [1758900000, 1758800000])
        r = self.s(source="viewed", date_from=1758650000)
        self.assertEqual(r["total"], 3)

    def test_manual_edit_delete(self):
        it = self.store.add(Item(source="manual", title="徵 二手腳踏車", text="預算 5000 元內",
                                 url="https://www.facebook.com/groups/2/posts/3", tags="腳踏車"))
        self.assertEqual(it["price"], 5000)
        self.store.update(it["id"], note="已私訊賣家", tags="腳踏車 已聯絡")
        self.assertEqual(self.s(q="已私訊")["total"], 1)
        self.assertEqual(self.s(q="已聯絡")["total"], 1)
        self.assertTrue(self.store.delete(it["id"]))
        self.assertEqual(self.s(q="腳踏車")["total"], 0)


class ServerTest(unittest.TestCase):
    def setUp(self):
        self.store = Store(Path(tempfile.mkdtemp()) / "s.db")
        self.httpd = ThreadingHTTPServer(("127.0.0.1", 0), make_handler(self.store))
        threading.Thread(target=self.httpd.serve_forever, daemon=True).start()
        self.base = f"http://127.0.0.1:{self.httpd.server_port}"

    def tearDown(self):
        self.httpd.shutdown()
        self.httpd.server_close()

    def req(self, path, data=None, method=None, headers=None, raw=False):
        body = data if raw else (json.dumps(data).encode() if data is not None else None)
        r = urllib.request.Request(self.base + path, data=body, method=method, headers=headers or {})
        with urllib.request.urlopen(r) as resp:
            return json.loads(resp.read())

    def test_flow(self):
        with urllib.request.urlopen(self.base + "/") as resp:
            self.assertIn("FB 資料搜尋", resp.read().decode())
        it = self.req("/api/items", {"url": "https://www.facebook.com/reel/1", "title": "手沖咖啡教學",
                                     "text": "磨豆機 NT$1,980", "kind": "video"})
        self.assertEqual(it["price"], 1980)
        r = self.req("/api/search?q=%E5%92%96%E5%95%A1&price_min=1000&price_max=2000")
        self.assertEqual(r["total"], 1)
        self.req(f"/api/items/{it['id']}", {"tags": "咖啡"}, method="PATCH")
        self.assertEqual(self.req(f"/api/items/{it['id']}")["tags"], "咖啡")

        payload = json.dumps(RECENTLY_VIEWED).encode()
        res = self.req("/api/import?name=recently_viewed.json", payload, raw=True,
                       headers={"Content-Type": "application/octet-stream"})
        self.assertEqual(res["new"], 5)
        self.assertEqual(self.req("/api/stats")["total"], 6)

    def test_cross_origin_write_blocked(self):
        with self.assertRaises(urllib.error.HTTPError) as cm:
            self.req("/api/items", {"title": "x"}, headers={"Origin": "https://evil.example"})
        self.assertEqual(cm.exception.code, 403)


if __name__ == "__main__":
    unittest.main()
