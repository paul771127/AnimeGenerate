# fbsearch:個人 Facebook 資料整理與搜尋系統

在自己電腦上把 Facebook 看過 / 收藏 / 發過的文章與影片整理成可搜尋的資料庫。

- 🔍 **全文搜尋**(中英文皆可,不用斷詞):`"完整片語"`、`-排除字`
- 💰 **價格區間篩選**:自動從內文抓出價格(`NT$1,200`、`1200元`、`3萬5`、`售價 800`…),可依價格排序
- 🕒 **最近觀看的文章/影片**:一鍵篩選「最近看的影片」、「最近看的文章」、「市集商品」
- 🏷️ 類型 / 來源 / 日期篩選,可加標籤與備註
- 📌 **書籤小工具**:瀏覽 FB 時反白貼文文字,一鍵存進資料庫
- 🔒 全地端、零外部套件(只用 Python 標準函式庫 + SQLite),預設只開在 `127.0.0.1`

## 為什麼用「下載你的資訊」而不是爬蟲?

Facebook 沒有開放搜尋貼文或觀看紀錄的 API,而自動爬取違反使用條款,帳號可能被停權。
Facebook 官方提供的 **下載你的資訊** 就包含「最近觀看」、「已儲存的項目」、「你的貼文」、「Marketplace」等紀錄,
這是合法取得自己資料的方式。平常瀏覽時再搭配書籤小工具補上即時看到的貼文。

## 快速開始

```bash
# 需要 Python 3.10+,不需要安裝任何套件
python -m fbsearch serve            # 開啟 http://127.0.0.1:8765
```

### 1. 從 Facebook 匯出資料

1. Facebook → **設定和隱私 → 設定 → 帳號管理中心 → 你的資訊和權限 → 下載你的資訊**
2. 格式選 **JSON**(重要,不要選 HTML),媒體畫質選「低」可以讓檔案小很多
3. 可只勾選:*你在 Facebook 上的活動(含最近觀看)*、*已儲存的項目和珍藏*、*貼文*、*Marketplace*
4. 下載 `.zip` 後,在網頁按「匯入」丟進去,或用命令列:

```bash
python -m fbsearch import ~/Downloads/facebook-你的名字.zip
```

重複匯入沒關係,相同網址會自動合併(只更新觀看時間)。私訊、好友、位置等檔案預設**不會**匯入。
Facebook 匯出檔裡中文變亂碼(`Ã¤Â¸Â­` 這種)的問題會自動修正。

### 2. 瀏覽時一鍵收藏

在網頁按「匯入」→ 把「📌 存到 FB 搜尋」拖到書籤列。
在 FB 看到想留的貼文:**反白貼文文字(含價格)→ 按書籤** → 跳出小視窗確認後儲存。
網址含 `/reel/`、`/watch` 會自動標為影片,含 `marketplace` 標為市集。

## 命令列

```bash
python -m fbsearch search 相機 --min 5000 --max 30000 --sort price_asc   # 價格區間
python -m fbsearch search --kind video --source viewed --days 7          # 最近 7 天看的影片
python -m fbsearch search "露營 -帳篷"                                    # 排除字
python -m fbsearch add --url https://www.facebook.com/... --text "二手單車 4500 元" --tags 單車
python -m fbsearch stats
```

資料庫預設是目前資料夾的 `fbsearch.db`,可用 `--db 路徑` 或環境變數 `FBSEARCH_DB` 指定。

## 資料欄位

| 欄位 | 說明 |
|---|---|
| kind | `post` 文章、`video` 影片、`marketplace` 市集、`link` 連結、`photo` 相片、`other` |
| source | `viewed` 看過、`saved` 收藏、`posted` 自己發的、`manual` 手動加入、`other` |
| price | 自動抽取的第一個價格,可在編輯中手動修改 |
| ts | 觀看 / 收藏 / 發文時間 |

## API(給其他工具串接)

| 方法 | 路徑 | 說明 |
|---|---|---|
| GET | `/api/search?q=&kind=&source=&price_min=&price_max=&has_price=1&date_from=&date_to=&sort=` | 搜尋,`sort` 可為 `recent` `oldest` `relevance` `price_asc` `price_desc` |
| GET | `/api/stats` | 統計 |
| POST | `/api/items` | 新增(JSON) |
| PATCH / DELETE | `/api/items/<id>` | 編輯 / 刪除 |
| POST | `/api/import?name=xxx.zip` | 上傳匯出檔 |
| GET | `/api/export` | 匯出全部 JSON |

## 測試

```bash
python -m unittest tests.test_fbsearch -v
```
