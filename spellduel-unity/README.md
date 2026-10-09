# SpellDuel（Unity 版）— 共用房間座標的 AR 對戰

網頁版（`spellbattle/`）每支手機各自用 2D 畫面判斷，兩邊看到的法術位置對不上。
這個 Unity 版改成**兩支手機共用同一套房間座標**：

1. **6DoF 追蹤**：ARKit（iPhone）／ARCore（Android）即時知道手機在房間裡的位置與朝向。
2. **共同標記圖**：兩支手機都掃描地上同一張印出來的圖，以它為世界原點 → 座標對齊。
3. **法術用世界座標模擬**：發射時只送「起點、方向、速度、半徑、發射時間」，兩邊用同一公式算位置，所以兩邊看到的一定是同一個點、大小也符合透視。
4. **攻擊方鏡頭輔助定位**：攻擊方的鏡頭偵測畫面中對手的骨架，腳踝射線與地板的交點＝對手站的位置，再和對手手機回報的位置融合；鏡頭真的看到對手才能鎖定、施法。
5. **攻擊方判定命中**：用融合後的世界座標計算，再廣播結果 → 不會一邊中、一邊沒中，也和對手手機轉向哪裡無關。

## 目前進度：第 1 階段（座標對齊 + 連線 + 測試用法術）

- [x] 程式建立 AR 鏡頭、執行期載入標記圖（不需要在編輯器建資產）
- [x] 掃描標記圖對齊世界座標（靠近標記圖時會持續修正）
- [x] 區域網路連線（同一個 Wi-Fi，輸入對方 IP）、對時
- [x] 每秒 20 次同步雙方位置，顯示對手的身體判定框（半透明膠囊）
- [x] 點螢幕發射測試法術，兩邊同步飛行、攻擊方判定命中、HP
- [x] **單人模式：畫遊戲場地**（像 Meta Quest 的邊界，不需要標記圖）
- [x] **單人對戰**：可選職業的電腦敵人在場地內走動、主動攻擊；四職業技能全部移植，用場地座標（公尺）模擬與判定
- [x] 雙人模式也可開「練習假人」（站在標記圖上）
- [ ] 第 2 階段（剩下）：雙人模式接上職業與技能、更好的 3D 特效
- [ ] 第 3 階段：手勢（MediaPipe）、語音唸咒
- [ ] 第 4 階段：陷阱放在地板、介面

## 一次性設定

### 1. Unity 授權（免費個人版）
雲端編譯需要你的 Unity 授權檔。Unity 已經不能在雲端直接啟用免費授權，所以要在自己電腦上做一次：

1. 安裝 [Unity Hub](https://unity.com/download)，登入 Unity 帳號。
2. Unity Hub → 偏好設定（Preferences）→ Licenses → Add → **Get a free personal license**。
3. 找到授權檔 `Unity_lic.ulf`：
   - Windows：`C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS：`/Library/Application Support/Unity/Unity_lic.ulf`
4. 到 GitHub → 這個 repo → Settings → Secrets and variables → Actions → New repository secret，新增三個：
   | 名稱 | 內容 |
   |---|---|
   | `UNITY_LICENSE` | `Unity_lic.ulf` 的**完整檔案內容**（用記事本打開全選複製） |
   | `UNITY_EMAIL` | Unity 帳號 email |
   | `UNITY_PASSWORD` | Unity 帳號密碼 |

> 帳號若開啟兩步驟驗證或用 Google/Apple 登入，雲端啟用可能失敗；建議用 email + 密碼登入的帳號。

### 2b. 改用 Codemagic 編譯（設定檔：repo 根目錄的 `codemagic.yaml`）
1. 到 [codemagic.io](https://codemagic.io) 用 GitHub 登入 → Add application → 選 `paul771127/AnimeGenerate` → 選 **codemagic.yaml** 設定方式。
2. App settings → **Environment variables**，建立群組 `unity_credentials`（全部勾 Secret）：
   | 名稱 | 內容 |
   |---|---|
   | `UNITY_EMAIL` | Unity 帳號 email |
   | `UNITY_PASSWORD` | Unity 帳號密碼 |
   | `UNITY_LICENSE` | 免費個人版：`Unity_lic.ulf` 的完整內容（同上一節） |
   | `UNITY_SERIAL` | 只有 Unity Plus/Pro 才填（填了就用序號啟用） |
3. **Start new build** → 分支選 `claude/mobile-camera-mic-battle-game-v5nhfr` → 流程選 **SpellDuel iOS（未簽署 IPA）** 或 **SpellDuel Android（APK）**。
4. 完成後在建置頁面下載 `SpellDuel-unsigned.ipa`（Sideloadly 安裝）或 `.apk`。

> ⚠ Codemagic 官方文件寫「雲端編譯 Unity 需要 Plus 或 Pro 授權」。用個人版 `.ulf` 是非官方做法，若卡在授權啟用（log 出現 license / No valid Unity Editor license），就改用下面的 GitHub Actions（同一份授權檔，個人版可用，公開 repo 的 macOS 機器也免費）。
> Codemagic 免費額度每月 500 分鐘 M2 機器；第一次編譯（安裝 Unity＋匯入專案）約 40～60 分鐘。

### 2. 雲端編譯（GitHub Actions）
設定好 Secrets 後，推送 `spellduel-unity/` 的變更會自動編譯（也可以在 GitHub → Actions → **SpellDuel Unity build** → Run workflow 手動執行）。
第一次約 40～60 分鐘（之後有快取會快很多）。完成後在該次執行頁面最下方的 **Artifacts** 下載：

| Artifact | 用途 |
|---|---|
| `SpellDuel-Android-APK` | Android 安裝檔，傳到手機直接安裝（要允許「安裝未知來源 App」） |
| `SpellDuel-iOS-unsigned-IPA` | iPhone 安裝檔（未簽署），用 Sideloadly 安裝 |
| `SpellDuel-iOS-XcodeProject` | iOS 的 Xcode 專案（之後要上 TestFlight 時用） |

### 3. iPhone 安裝（Sideloadly，免費）
1. 電腦安裝 [Sideloadly](https://sideloadly.io/)（Windows 需先裝 iTunes 與 iCloud 的**官網下載版**）。
2. iPhone 用傳輸線接電腦，信任這台電腦。
3. 把 `SpellDuel-unsigned.ipa` 拖進 Sideloadly，輸入你的 Apple ID，按 Start。
4. iPhone：設定 → 一般 → VPN 與裝置管理 → 信任你的 Apple ID。iOS 16 以上還要開啟 設定 → 隱私權與安全性 → **開發者模式**。
5. 免費 Apple ID 簽的 App **7 天後失效**，到時用 Sideloadly 再裝一次即可。

### 4. 列印標記圖
列印 [`marker/spellduel_marker_A4.pdf`](marker/spellduel_marker_A4.pdf)，**用 100% 實際大小列印**（不要「縮放至頁面大小」）。
印好後量一下圖的寬度應為 **20 公分**（不是的話，對齊的距離會等比例偏差）。
平放在兩人中間的地上，不要反光、不要皺。

## 單人模式：畫遊戲場地（不需要標記圖）

開啟 App 選「**單人練習（畫場地）**」：

1. **找地板**：慢慢移動手機讓鏡頭掃過地板，畫面上方顯示「地板已找到」後，地上會出現白色小圓圈（準星落在地板的位置）。
2. **建立場地**（二選一）：
   - **方形 2.5m／3.5m**：一鍵在你前方放一個正方形場地，你站在靠近自己那一側、邊界內 0.5 公尺。
   - **手繪場地**：**按住螢幕**，把畫面中央的準星沿著想要的場地邊緣在地板上畫一圈，**放開就完成**（像 Quest 用手把畫邊界）。至少要 1 平方公尺。
3. 地上會出現藍色的場地邊框，**場地中心就是世界原點**。
4. **邊界提醒**：離邊界 1 公尺內會浮出半透明格子牆（越近越明顯）；走出場地時整圈牆都會出現、畫面提示「回到場地內」並每秒震動。
5. **選職業**：我的職業與 3 個技能、敵人職業（或隨機），按「開始戰鬥」。

### 單人對戰

敵人出現在場地內、你面對的那一側，**全部用場地座標（公尺）計算**——你在房間裡看到的就是它的真實位置：

- **敵人會走動**：依職業保持距離（刺客、劍士逼近；法師、弓箭手拉開），並繞著你左右移動；蓄力中的技能打不到時會走近或退開；永遠留在場地內。
- **敵人會主動攻擊**：照同樣的規則（MP、冷卻、蓄力、射程），血少時補血，看到你的攻擊飛來有機率開格擋／鐵壁／反擊。
- **操作**：點下方技能按鈕開始詠唱 → 蓄力完成後**點畫面**朝那個方向發射（陷阱設在你點的地板，要在 3 公尺內；治療／防禦點畫面任何地方發動）。
- **閃避是真的**：敵人的法術在空間中朝你當下的位置飛，**你實際走開就閃得掉**；慢的（火球、冰槍、隕石）容易閃，快的（雷擊、箭）和近身的（斬擊、背刺）幾乎閃不掉——對付刺客要拉開距離。
- 敵人法術逼近時畫面邊框閃紅；**被打中時手機震動、整個畫面閃一下紅色、扣血**（中毒扣血時閃淡紅）；被煙霧彈打中畫面會被遮住。
- **鎖定（和雙人模式相同）**：敵人出現在畫面中才會出現綠色「🎯 鎖定」框，攻擊法術才能發射；敵人不在畫面中時會提示「◀ 敵人在左邊／敵人在右邊 ▶」。自身技能（治癒、防禦）與陷阱不需要鎖定。
- **AR 追蹤中斷時戰鬥暫停**（對著白牆／天花板、太暗、晃太快、遮住鏡頭）：敵人、法術、判定全部停住，畫面顯示原因；追蹤恢復並穩定 0.5 秒後自動繼續。只是把鏡頭轉開、看不到敵人時**不會**暫停，敵人照樣從畫面外攻擊。（雙人模式不暫停，見下方。）
- 技能數值（`Assets/SpellDuel/Runtime/Skills.cs`）從網頁版移植，單位改成真實空間：速度 m/s、判定半徑 m、射程 m。法術最多飛到射程 +1 公尺就消失。

模擬測試（3.5m 場地、各職業敵人各 5 局 × 60 秒）：站著不動時敵人法術幾乎全中；會側移閃躲時，火球 39/39 → 6/33、冰槍 36/36 → 3/34；敵人在所有測試中都沒有走出場地。

> 單人模式只靠 AR 追蹤，不需要列印任何東西。雙人模式仍然用標記圖，因為兩支手機要對齊到同一個原點，各自畫框無法精準對上。

## 怎麼測（雙人模式）

1. 兩支手機連上**同一個 Wi-Fi**，打開 SpellDuel，選「**雙人對戰（標記圖）**」。
2. **對齊座標**：蹲低把鏡頭對準地上的標記圖（約 30～80 公分距離），等上方顯示「座標：✅ 已對齊」。標記圖上會出現綠色方塊和箭頭，應該剛好蓋在圖上。
3. **連線**：一支按「建立房間」，上方會顯示它的 IP；另一支在輸入框填這個 IP，按「加入」。
4. 對齊後，兩人站開 2～3 公尺。你應該會看到對手身上套著一個**半透明的藍色膠囊**——這是對手手機回報的位置，應該跟真人重疊。
5. **鎖定對手**：鏡頭偵測到對手時，畫面上會出現黃色（身體）／綠色（腳踝）的骨架點、地上一個綠色圓盤（鏡頭算出的站位），對手身上出現綠色「🎯 鎖定」框；鏡頭沒看到對手就不能施法。上方會顯示「鏡頭偵測」狀態、站位距離、與對手回報位置的差距。
6. **點螢幕發射**：橘色球從你手機往點擊方向飛。對手畫面會看到紫色球飛來；被打中的一方會震動、閃紅並扣 HP，閃開則顯示「閃過了！」。
7. 單人測試也可以不連線按「假人:開」，標記圖上會站一個橘色假人。
8. **只有一支手機也能測鏡頭定位**：不連線、假人關閉時，鏡頭拍到的真人就是靶（看得到腳才算）。可以請朋友站在不同距離，拿捲尺比對畫面上「站位 x.xx m」的準確度。

**要確認的重點**

| 項目 | 看什麼 |
|---|---|
| 對齊準不準 | 綠色方塊是否貼合標記圖；走動後再回來看是否還貼合 |
| 雙方位置對不對 | 藍色膠囊是否跟對手真人重疊（誤差幾公分～十幾公分屬正常） |
| 法術是否一致 | 兩邊同時錄影：同一顆球在兩邊的位置、命中與否是否一致 |
| 延遲 | 上方「時鐘差／來回」：來回時間一般 20～80ms |

**位置偏掉時**：按「重新對齊」再掃一次標記圖。靠近標記圖（1.5 公尺內）時會自動持續修正。

**被攻擊方把手機轉開時**：不影響判定。判定只用雙方在房間裡的世界座標，攻擊方用鏡頭看到的位置（加上對手回報的位置）計算；對手手機朝向哪裡、甚至 AR 追蹤中斷（對著白牆、鏡頭被擋），都照樣會被打中——攻擊方鏡頭看得到他就用即時位置，看不到時用他最後的正確位置（判定框變灰）；他自己暫時不能施法。

### 鏡頭定位的準確度

腳踝射線打到地板的方法：左右方向很準（角度誤差約 0.5°），前後距離的誤差隨距離變大。手機拿在胸前約 1.4 公尺高的估算：

| 對手距離 | 前後誤差 | 左右誤差 |
|---|---|---|
| 1.5 m | ±4 cm | ±1 cm |
| 2 m | ±6 cm | ±2 cm |
| 3 m | ±10 cm | ±3 cm |
| 4 m | ±15 cm | ±4 cm |
| 5 m | ±21 cm | ±4 cm |

身體判定半徑是 28 公分，4 公尺內都夠用。所以融合時**左右以鏡頭為主**；**前後距離近時多信鏡頭、遠時多信對手回報**。
手機平拿時，對手在約 2.3 公尺內腳會出畫面——這時只用鏡頭的「方向」，距離用對手回報的。畫面裡有別人時，離對手回報位置超過 1 公尺的人不採用。

人體偵測：Android 用 Google ML Kit Pose Detection（Gradle 自動下載），iOS 用 Apple Vision（**需要 iOS 14 以上**；iOS 13 會退回只用對手回報的位置）。都在手機上執行、免費、不需要網路。被打中時震動＋全畫面閃紅＋扣血。

## 架構

| 檔案 | 功能 |
|---|---|
| `Assets/SpellDuel/Runtime/GameRoot.cs` | 啟動時用程式建立 AR 鏡頭、標記圖追蹤、雙方位置同步、法術模擬與命中判定、介面 |
| `Assets/SpellDuel/Runtime/Skills.cs` | 職業與技能資料（真實空間單位） |
| `Assets/SpellDuel/Runtime/Combat.cs` | 戰鬥核心（純邏輯）：施法、法術飛行與命中、防禦、異常狀態、陷阱 |
| `Assets/SpellDuel/Runtime/EnemyAI.cs` | 電腦敵人：場地內走位、選招、瞄準、防禦 |
| `Assets/SpellDuel/Runtime/SoloBattle.cs` | 單人對戰的 3D 顯示與介面 |
| `Assets/SpellDuel/Runtime/PlayArea.cs` | 單人場地：偵測地板、方形／手繪場地、邊界格子牆與出界警告 |
| `Assets/SpellDuel/Runtime/PoseDetector.cs` | 擷取 AR 畫面、呼叫手機內建的人體偵測、把關鍵點換成射線 |
| `Assets/Plugins/Android/PoseBridge.java` | Android：ML Kit 人體偵測 |
| `Assets/Plugins/iOS/PoseBridge.mm` | iOS：Apple Vision 人體偵測 |
| `Assets/SpellDuel/Runtime/Tracking.cs` | AR 追蹤狀態與中斷原因（單人中斷時暫停戰鬥；雙人中斷時不能施法、停止回報位置） |
| `Assets/SpellDuel/Runtime/WorldFrame.cs` | 共用世界座標（以標記圖為原點）與 AR 座標的換算 |
| `Assets/SpellDuel/Runtime/NetLink.cs` | 區域網路 TCP 連線（一行一個 JSON） |
| `Assets/SpellDuel/Runtime/Messages.cs` | 網路訊息格式 |
| `Assets/SpellDuel/Editor/BuildScript.cs` | 雲端編譯入口：用程式建立場景、玩家設定、ARCore/ARKit 設定、iOS 權限說明 |
| `Assets/Resources/marker.bytes` | 內建標記圖（PNG），與列印版是同一張圖 |
| `../.github/workflows/spellduel-unity.yml` | GitHub Actions：Android APK、iOS Xcode 專案、未簽署 IPA |

場景是空的：所有物件都在 `GameRoot` 啟動時建立，所以不需要 Unity 編輯器就能開發。

**同步協定**（世界座標，單位公尺）：`ping/pong` 對時（房主時鐘為共同時間）、`pose` 雙方位置（20Hz）、`shot` 發射、`hit/miss` 攻擊方的判定結果、`hp` 被打的一方回報血量、`track` 追蹤狀態。

## 已知限制

- 兩支手機必須在同一個 Wi-Fi（區域網路直連）。之後可改用網路配對伺服器。
- 標記圖要平放在地上（身體判定框沿重力方向往下延伸，與標記圖方向無關，但地板高度以手機下方 1.45 公尺估算）。
- AR 追蹤在太暗、白牆、快速晃動時會暫時失效；長時間遊玩可能累積幾公分到十幾公分的偏移，靠近標記圖會自動修正。
- 這個 repo 是公開的，所以 GitHub 的 macOS 編譯免費；若改成私人 repo，iOS 編譯步驟可改用 Codemagic（只編 Xcode 專案，不需要 Unity 授權）。
