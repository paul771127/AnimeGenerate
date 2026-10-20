# leadgen — 接案潛在客戶名單系統

針對**軟體 / 韌體 / 機構 + 自家快速打樣**一條龍接案者的 lead generation pipeline。
從公開資料源抓「有人正在找人做這件事」的訊號,去重、補聯絡方式、評分、匯出。

## 設計前提:不要廣撈

這套系統刻意**不**做「抓全網所有信箱」,因為那條路三面都是牆:

| 問題 | 後果 |
|---|---|
| 無意圖名單的冷信回覆率約 0.5–2% | 抓 10 萬筆的產出 < 精準 500 筆 |
| 爬來的信箱大量不存在 | 退信率拉高 → 寄件域名信譽燒掉 → 連正常信都進垃圾桶 |
| 個資法第 9 條:間接蒐集要告知來源與目的 | 拿不出來源就是違法蒐集 |
| LinkedIn / FB / IG / 104 的 ToS 明文禁止爬取 | 帳號封鎖,嚴重者民事求償 |

所以這裡做的是**意圖訊號 + 公開商業聯絡資訊**:每筆名單都附帶
「對方在什麼時候、在哪個公開頁面、說了什麼」,寫信時直接拿來當開場。

## 快速開始

```bash
pip install -r requirements.txt

# 1. 填你自己的信箱(爬取時 User-Agent 會帶它,讓對方站長找得到人)
#    config.yaml -> network.contact_email
vi config.yaml

# 2. 抓取 -> 去重 -> 補聯絡方式 -> 評分 -> 入庫
python -m leadgen collect

# 3. 匯出
python -m leadgen export --csv data/out/leads.csv --md data/out/today.md --min-score 70

# 其他
python -m leadgen sources          # 看來源與啟用狀態
python -m leadgen stats            # 資料庫概況
python -m leadgen rescore          # 改了權重後重算,不用重抓
python -m leadgen suppress a@b.com # 退訂 -> 永久阻擋(寄信前一定要查)
```

## 七個來源(含實測評價)

**排序的前提是「個人接案、沒有公司行號」。** 政府標案的廠商資格要營業登記、
納稅證明與押標金,大案子個人過不了資格審 —— 所以它不是主力,排在後面。
如果你之後成立了公司行號,把 `gov_tender.individual_only` 改成 `false`,
整個標案市場才會打開,來源的排序也要跟著重排。

### 1. `ptt` — PTT CodeJob + Mechanical 板 ★ 台灣、零門檻

實測各板第一頁的需求貼文密度:

| 板 | 本頁篇數 / 需求 | 需求標籤 | 內容 |
|---|---|---|---|
| CodeJob | 21 / 3 | `[發案]` | 軟體外包 |
| Mechanical | 21 / 5 | `[徵求]` | 加工、機構、設備 |
| Soho | 14 / 4 | `[徵才]` | 全是行銷、攝影、廣告投手 |
| Electronics | 16 / 1 | `[請益]` | 偶爾有故障排查 |
| Programming | 21 / 0 | — | 純討論板 |

所以主力是前兩個板。**實測 3 頁抓到 37 筆,23 筆有聯絡方式**,例如:

- NACHI MZ25 機械手臂教學指導、新竹 電木NC加工廠(Mechanical)
- 醫藥系統開發 React 前端 x1 + Python 後端 x1、掃描自動化轉檔軟體開發(CodeJob)
- 室內設計內部管理系統與 App 開發(LINE: holdsking)
- 線上協助 Docker 啟動修復與 Python 舊程式(LINE: edwardteng1219)

**`Soho` 板預設不開**:實測 5 頁 45 篇需求貼文只濾出 3 筆,而且是 1000~1500 元
的行銷微案,跟軟體/韌體/機構沒有交集。如果你也接小型網站或自動化腳本,
再把它加回 `boards`。

Soho 板的貼文有制式表格(個人名稱、聯絡方式、案件預算、理想接案對象),有表格
就照欄位解析;CodeJob 與 Mechanical 是自由格式,退回全文掃描聯絡方式。
金額級距小(幾千到幾萬),它的價值在零資格門檻、決策快、可以直接聊,不在金額。

### 2. `freelancer` — Freelancer.com 官方 API ★ 個人投標是常態

不需要任何公司資格,客戶是中小企業與個人。但這個平台有四個陷阱,
不處理的話名單會是垃圾:

1. **殺價市場。** 大量案子是 $30–250 美金搭配 90 幾人投標 —— 實測「Vehicle
   LED VMS Display Design」預算 $30–250、98 人投標。要套 `min_budget_twd`
   與 `max_bids`。
2. **`query` 是對全文搜尋的。** 用 `cad`、`automation` 會撈到「Modern Logo
   for Arema Nasi Goreng」「Facebook & Instagram Image Post」這種行銷案
   (描述裡剛好有那些字)。所以真正的過濾靠**平台自己的技能標籤白名單**
   (`skills` / `exclude_skills`):「Bow Sight STL Redesign」掛的是 CAD/CAM、
   Solidworks、3D Modelling,「Cabin Foundation Blueprint」掛的是 Building
   Architecture、Civil Engineering —— 兩者都命中 `cad`,只有前者是你的。
3. **駐點案從台灣接不了。** 平台的 `local` 旗標能擋一部分,但不夠 ——
   「On-Site ABB VFD Analysis and Repair」是 `local=False` 卻明顯要到現場。
   所以 `remote_only` 再加一層標題判斷(on-site、UK-Based、must be based in)。
4. **時薪案不能拿固定價的門檻去比。** 一個 $15/hr 的海報案換算成「預算」
   只有 480 台幣,卻會通過「預算下限 / 40 小時」這種算法。時薪案改用
   `min_hourly_twd` 判斷,再換算成「時薪 × 假設工時」讓預算分數可比。

**套完四層過濾後實測剩 10 筆,全部是 CAD / PCB / 機構 / 3D:**

| 預算(台幣) | 案名 | 技能標籤 |
|---|---|---|
| 160,000 | License Your Archived KiCad PCB Projects | Electronics、PCB Layout |
| 24,000 | Semi-Automatic Pallet Shuttle Design | CAD/CAM、Mechanical Engineering |
| 48,000 | Realistic 3D Figurine Modeling and Printing | 3D Modelling、3D Design |
| 24,000 | Custom Basecamp Enclosure Engineering | Industrial Design、Solidworks |

時薪案在 800 元/小時(約 US$25)的門檻下當天全被濾掉 —— 那是正確結果,
不是 bug:平台上的時薪案費率普遍偏低。

平台案子沒有聯絡方式(站內投標),所以記在 `contacts_other`。

### 3. `hn_freelance` — Hacker News 每月徵才串 ☆ 外包串已停更

原本這是「可直接寫信」的主力:`Ask HN: Freelancer? Seeking freelancer?`
裡的 SEEKING FREELANCER 留言是對方公開徵求接案者**並自己留下聯絡方式**。

**但那個每月串在 2025-10 之後就沒再發了。** 所以現在只剩
`Ask HN: Who is hiring?`(還在每月更新),那是公司在徵人,意圖比外包需求弱
(0.55 vs 1.00),而且聯絡方式要靠留言裡自己貼的網址再去官網找。

這裡踩過一個很貴的 bug,值得記下來:我原本用 Algolia 的 `/search`
(按相關度排序)再把回傳的 30 筆按 story id 排序,以為那樣就能拿到最新的串 ——
其實那只是「相關度前 30 名裡最新的」。實測抓回來的是 2019~2024 年的串,
**379 筆 HN 名單裡只有 1 筆上得了 70 分**,因為聯絡人早就不在了。
改用 `/search_by_date` 才拿到真正的最新串,並加了 `max_thread_age_days`
(預設 120 天)把停更的外包串自動擋掉。兩條測試守住這兩件事。

### 4. `reddit` — r/3Dprintmything、r/forhire 等 ☆ 需要免費憑證

個人接案在 Reddit 完全沒有門檻 —— 看到貼文直接私訊就能談。
對有 3D 列印機的人最直接,r/3Dprintmything 整個板就是「幫我印這個」:

```
[USA-IL] Drawer Insert for IKEA Helmer Storage Unit
[US-MI] Nintendo Switch 2 Screen Cover in Black PETG
[NJ, USA] Snowboard bevel guide
```

`[Hiring]` 是需求方,`[For Hire]` 是同行在推銷自己 —— 後者一定要排除,
不然等於把競爭者當名單。

**這個來源沒有憑證就自動停用,而且不會改用其他方式繞過。**
Reddit 的 robots.txt 對所有 user-agent 是 `Disallow: /`,連公開的 `.rss`
都包含在內(我先用 RSS 做了一版,跑起來才發現失敗原因是 RobotsDenied
而不是限速,所以把整條 RSS 路徑刪掉了,並留了一條測試守住這件事)。
合法的路只有官方 API,要兩個環境變數,免費、註冊一個 script app 兩分鐘:

```
REDDIT_CLIENT_ID / REDDIT_CLIENT_SECRET      # https://www.reddit.com/prefs/apps
```

兩個誠實的限制:**OAuth 連線沒有在這裡端到端驗證過**(需要你自己的憑證,
我不會去申請),解析與過濾的邏輯有單元測試覆蓋;另外**代印請求多半要寄送
實體件**(標題的 `[USA-IL]` 是發文者所在州),從台灣寄運費與時間都不划算 ——
真正吃得到的是 CAD 建模、逆向這種交檔案的部分,以及 r/forhire 上的軟體案。

### 5. `gov_tender` — 政府電子採購網 ☆ 只剩兩種個人投得了

`individual_only: true`(預設)只留這兩類,其他的個人過不了資格審:

- **公開徵求廠商提供參考資料**:這是 RFI,只是請你提供資料,沒有資格門檻,
  而且是讓機關認識你最便宜的方式 —— 後續的案子常常就找有回應過的人。
- **公開取得報價單或企劃書**:用於未達公告金額的採購,機關可以直接洽個人。

開了這個篩選後,實測可投標的從約 100 筆降到 **46 筆,全部標記「個人可投」**:

| 分數 | 機關 | 案名 |
|---|---|---|
| 76 | 臺北大眾捷運 | 文湖線未來車車頭暨駕駛平台模型製作（公開取得報價單） |
| 76 | 桃園國際機場 | 「國門意象彩繪機」視覺設計與典藏模型製作（RFI） |
| 72 | 法務部調查局 | 115年AI資料中台系統開發（未達公告金額,截止 10/28） |
| 68 | 國立雲林科技大學 | 韌性低碳建築輕質牆測試線夾具一套（未達公告金額） |

決標公告仍然保留(`include_awards`),那是情報:哪些機關固定在買這類服務、誰在搶。

#### 設備採購 vs 委託製作:這個分類器決定名單是乾淨還是垃圾

打樣類關鍵字**有九成命中的是「機關要買一台 3D 列印機」**,不是「機關要找人打樣」。
「3D列印」在採購網有 1871 筆,第一名是「教學用所需3D列印機採購」。
所以 `classify()` 把標案分四類:

| 類 | 意思 | 例子 | 預設 |
|---|---|---|---|
| `service` | 找人做 | 真空輔助液流電池接合治具**設計與製作** | 留,意圖 0.95 |
| `goods` | 買零件,但零件要有人做出來 | 進氣端外殼等2項 | 留,意圖 0.60 |
| `equipment` | 買現成設備 / 軟體授權 / 材料 | 教學用所需3D列印機**採購** | **丟** |
| `construction` | 土木工程 | 校舍耐震補強工程 | **丟** |

`goods` 刻意保留 —— 對自己有列印機、能代工供貨的人來說,「買機構件」是真的機會。

判斷順序是「強動詞(委託/開發/設計/打樣) → 設備+採購 → 材料+數量詞 → 弱動詞
(製作/加工/組裝) → 採購類別」。這個順序有意義:機關很愛把委外案寫成「採購案」,
所以強動詞必須先判;而「3D列印暨無人機組裝材料等2案」有弱動詞「組裝」,
但它買的是線材不是你的工時,所以材料 + 數量詞要在弱動詞之前攔下來。

#### 中文關鍵字沒有詞界

關鍵字「試作」命中了「經建版地形圖…文字註記產製向量圖磚測**試作**業採購」——
那是測繪案。這類誤判在中文沒辦法用詞界擋,只能不要用那個關鍵字,
所以 `試作`、`模型`、`設計`、`製作`、`開發` 這些太短或太泛的詞不收,
有一條測試擋住把它們加回來。(「治具櫃」仍然是治具、「原型機」仍然是原型,
那是合法的複合詞,交給分類器處理就好。)

### 6. `job_boards` — Remote OK API + We Work Remotely RSS ☆ 最弱

公司在徵 firmware / embedded / mechanical 工程師 = 有專案缺人。
**實測結論要說實話:這兩個板子的職缺嚴重偏網頁與行銷,拿硬體關鍵字去濾,
當天最新的 100 筆常常是 0 命中。** 它不花成本、有命中就是真機會,所以留著,
但不要期待它是主力。而且它給的是正職職缺,不是外包案。

關鍵字只比對**職稱**而且用詞界比對,因為 Remote OK 的 tags 是人工亂貼的
(運輸規劃師會被貼上 `engineer`/`medical`/`hr`),而單字 `driver` 會命中
「Gardener Handyman **Driver**」。另外 **Remotive 沒有收錄** —— 它的 robots.txt
明文 `Disallow: /api/*`。Remote OK 的 API 條款要求標註來源,
所以匯出時**不要拿掉 `source_url`**。

### 7. `tw_company` — 台灣公司登記資料 ☆ 預設關閉

依營業項目、地區、資本額篩出目標名單,再由 enricher 去官網的公開聯絡頁取 `info@`。
**預設關閉**:它完全沒有意圖訊號,開了會把資料庫灌滿低分名單。
刻意**不保留負責人姓名** —— 那是可識別特定自然人的個資。

### 試過但不可行的

| 來源 | 原因 |
|---|---|
| 104 外包網、嘖嘖、Kickstarter | Cloudflare 擋到連 robots.txt 都拿不到,ToS 也禁止 |
| Tasker 出任務 | robots.txt 明文 `Disallow: /case/detail/*`,正好是案件頁 |
| Reddit（r/forhire 等） | 未登入的 API 回 403 |
| Upwork RSS | Cloudflare 403 |
| Remotive | robots.txt 明文 `Disallow: /api/*` |
| Guru.com | 整站在 Incapsula 後面回 403,沒有公開 API |
| PeoplePerHour | 有 `/api/v1/` 但端點未公開,猜不出來(也不該猜) |
| freelancermap | robots.txt 完全允許,但搜出來是歐洲企業 IT 的駐點約聘(SAP Consultant、Commissioning Manager),不是遠端專案 |
| Reddit 的 .rss | robots.txt 是 `Disallow: /`,只能走官方 API |

募資平台的硬體專案確實是打樣的好客戶,但要走這條得用官方 API 或人工看。

## 聯絡方式補齊(`enrich`)

對分數夠高、但還沒有信箱的公司,去它官網的公開聯絡頁抓 `info@` / `sales@` 這類角色信箱。

明確的界線:
- 只讀 robots.txt 允許的頁面;遇到 401/403 直接放棄,不繞
- 每站最多 4 頁、每網域每秒 1.5 次請求
- **絕不做 email pattern guessing**(不組 `名.姓@公司.com`)。那是猜測個資,
  而且猜錯會造成大量退信、燒掉你的寄件域名信譽。有一條測試專門守這件事
- 只收同網域的信箱,避免抓到合作夥伴或代理商

`tw_company` 這類沒有官網欄位的來源,可以選用 Google Programmable Search JSON API
找官網(官方 API,免費額度每天 100 次):設 `GOOGLE_CSE_KEY` 與 `GOOGLE_CSE_CX` 即啟用,
沒設就自動停用。

## 評分

總分 100,權重在 `config.yaml` 可調:

| 維度 | 權重 | 看什麼 |
|---|---|---|
| `intent` | 30 | 對方是不是「現在就在找人做這件事」 |
| `budget` | 25 | 標案預算 / 薪資級距 / 資本額 |
| `fit` | 25 | 跟你的技能棧契合度(`profile.core` / `plus` / `exclude`) |
| `reach` | 10 | 有沒有可用的公開聯絡方式(角色信箱最高分) |
| `fresh` | 10 | 訊號新鮮度,兩個月前的需求大多已經發出去了 |

意圖強度按訊號類型:公開徵求外包 1.00 > 招標中(找人做)0.95 > **外包平台案子 0.90** >
招標中(買零件)0.60 > 在徵相關職位 0.55 > 已決標 0.35 > 投標期限已過 0.12 >
只是公司登記 0.08。

`reach` 認三種管道:角色信箱(`info@`)1.0 > **LINE ID / 平台站內投標 0.9** >
一般信箱 0.8 > 電話 0.5。台灣的個人發案者多半只留 LINE,外包平台則根本不給
聯絡方式 —— 沒有 `contacts_other` 這個欄位,這兩類來源的 reach 會永遠是 0 分,
整個來源排不進寄信名單。

三個刻意的校準決定:

- **「預算未公開」不等於「預算是 0」。** 政府標案有一大部分不公開預算,職缺也多半
  不寫薪資,把未知當 0 分會系統性把這些名單壓在 75 分以下,等於懲罰資料缺失。
  未知給中性分(`unknown_budget_ratio: 0.4`),已知的小預算才真的扣分。
- **中文標題很短,不能要求命中三個核心關鍵字。** 「文湖線未來車車頭暨駕駛平台模型
  製作採購案」只會命中一個。所以命中一個就 0.80、兩個 0.95,否則所有中文來源
  會被系統性壓低一級。
- **預算滿分線是 30 萬,不是 200 萬。** 原本照政府標案的刻度設 200 萬,結果
  20 萬的外包案(對一個人來說是好案子)只拿到 2.5/25 分,所有小客戶來源都被
  系統性壓在標案底下。30 萬是「對一個人來說是大案子」的量級。之後成立公司行號、
  開始接百萬級的案子,再把 `budget_full_at_twd` 調回去。

**70 分以上才值得寄信。** 一天手動個人化 20–30 封,不要群發。
改完 `profile` 或權重後跑 `rescore`,不用重抓。

## 合規底線

這些已經寫進程式,不是註解:

- 每筆名單都存 `source_url` + `collected_at`,匯出的 CSV 一定帶這兩欄。
  對方問「你從哪拿到我的聯絡方式」時,你要能在三秒內回答
- `suppression` 表是**永久**阻擋名單。退訂寫進去之後,`upsert` 會直接拒絕寫入該網域
- User-Agent 必須帶真實信箱,`contact_email` 沒填程式會拒絕執行
- 遵守 robots.txt(`respect_robots` 不要關)
- 不碰需登入才看得到的資料、不繞 CAPTCHA、不用假帳號
- 不保留公司登記資料裡的負責人姓名
- **寄信端不在這套系統裡** —— 這是故意的。寄信一律附退訂連結,
  歐盟對象另外處理 GDPR(合法利益評估 + 告知)

## 不要忽略另一半:讓客戶來找你

抓名單是 outbound,天花板很明顯。同樣的工程時間投在這些地方,複利更高:

- 作品集頁 + 針對「韌體外包」「機構設計委託」「3D列印打樣」這類關鍵字的 SEO 落地頁。
  自家有列印機這件事值得單獨做一頁:**「軟體+韌體+機構+當天打樣」能一個人吃下
  從概念到可動原型的整段**,這個組合在接案市場不多,是真正的差異化,
  而不是又一個「可以接軟體案」的人
- 在客戶聚集的地方持續產出(技術部落格、GitHub、社群)
- 把服務掛上外包平台並累積評價

最強的組合是:**outbound 名單系統負責冷啟動,inbound 負責長期壓低獲客成本。**

## 加新來源

```python
# leadgen/sources/your_source.py
from ..http import Fetcher
from ..models import Lead
from .base import register

@register
class YourSource:
    name = "your_source"

    def fetch(self, fetcher: Fetcher, cfg: dict):
        yield Lead(org=..., source=self.name, source_url=..., signal_type=...)
```

掛進 `leadgen/sources/base.py` 的 `load_all()`,在 `config.yaml` 加一段設定就能用。
adapter 只負責「把公開資料源變成 Lead 串流」,去重、評分、匯出都不用管。

值得加的方向(按投報率):NCC/FCC 認證公告(剛認證完新無線產品 = 強硬體訊號)、
募資平台新上線專案、Crunchbase 新融資、Steam / Google Play 新上架。
要衝量的話,Apollo.io / Hunter.io / Clay 這類商用資料商比自己爬划算也安全得多 ——
他們幫你擔合規與驗證。

**已經試過但不可行的**:Kickstarter、嘖嘖、104 都在 Cloudflare 後面(連 robots.txt
都拿不到),而且 ToS 本來就禁止爬取。募資平台的硬體專案確實是打樣的好客戶,
但要走這條得用它們的官方 API 或人工看。

## 測試

```bash
python -m pytest tests -q   # 50 passed,完全不碰網路
```

涵蓋:去重與跨訊號聯絡方式互補(含更正公告收斂)、評分排序與排除規則、
預算未知的處理、中文短標題的契合度、過期訊號、退訂阻擋、人工 status 不被覆寫、
標案四分類(12 個真實標題)、中文關鍵字陷阱、PTT 的 LINE ID 解析與賣物貼文排除、
徵求期限的跨年推算、外包平台案子的 reach 計分、駐點案偵測、時薪與固定價的
預算換算、駐點判斷不能誤殺 Cloud-based/Rust-based、HN 串的新鮮度與
按日期排序、Reddit 的 [For Hire] 排除與「沒憑證就不發請求」、聯絡頁抽取(角色信箱優先 /
同網域過濾 / robots 拒絕 / 頁數上限),以及一條守住「不做 email pattern
guessing」的測試。

新增的測試幾乎都是**用實際踩到的髒資料當案例**:`leaflet@1.9.4` 被當成信箱、
`you@awesome.sauce` 這種範本信箱、SpaceX 與 Canonical 因為共用 `grnh.se`
短網址而被合併、「LINE: 所在地區」被當成帳號、「StreamlineBio」裡的 line
被當成 LINE 帳號。
