# 全部選單的介面美術（給 Codex）

使用者的要求：**各個選單都讓美術設計一下。**
目前所有選單都用 `Runtime/UiKit.cs` 的幾個小工具畫：深色半透明圓角卡片、圓角按鈕、圓角血條、置中訊息膠囊。功能都正常，但看起來像除錯介面，沒有遊戲感。
風格：日式賽璐珞奇幻（見 [`ART_STYLE.md`](ART_STYLE.md)）。可以參考 RPG 手遊的選單：羊皮紙／魔法水晶面板、金屬或寶石邊框、有層次的按鈕、標題緞帶。

請分兩步做（兩步可以在同一次完成，但第一步最重要）：

## 第一步：整體主題（`Runtime/UiKit.cs`）——改一次全部選單都變
所有選單都只呼叫這五個函式，**簽名不能改**（參數、回傳值），只改畫出來的樣子：

| 函式 | 用在哪 | 要設計成 |
|---|---|---|
| `Card(r, alpha, accent, accentAlpha)` | 面板、卡片、資訊框 | 有質感的面板：底色漸層＋花紋或雜訊紋理、雙線或寶石角飾的邊框；`accent` 是主題色（職業色、地區色、稀有度色），邊框與角飾用它 |
| `Button(r, text, size, selected, accent, enabled)` | 所有按鈕 | 立體按鈕：上亮下暗、邊框、按下時下沉（`GUIUtility.hotControl` 或 `Event.current` 判斷）；`selected`＝發光／勾選感；`enabled=false`＝灰暗 |
| `Text(r, text, size, color, anchor, bold, minScale)` | 所有文字 | 保留自動縮字與換行；可以加文字描邊或陰影讓它在 AR 畫面上更清楚 |
| `Bar(r, v, c, text, size)` | HP／MP／經驗條 | 有框的能量條，填色漸層＋亮光 |
| `Pill(centerY, text, size, color, maxWidth, alpha)` | 畫面中央的提示訊息 | 緞帶或卷軸形的提示 |

可以新增工具函式給第二步用，例如：
- `Title(r, text, accent)`：標題緞帶。
- `Tab(r, text, selected, accent)`：分頁籤。
- `Divider(...)`：分隔線。
- `IconBadge(...)`：圓形圖示框。
- `Backdrop(...)`：全螢幕選單底。

規則：
- 貼圖都用程式產生（像現在的 `MakeRound`），**不能用圖檔**。
- 九宮格切片（`GUIStyle.border`），任何大小都不變形。
- **只產生一次並快取**，不要每幀產生。
- 文字要清楚：在相機畫面（亮或暗的房間）上都看得清楚。

## 第二步：各畫面的版面
**只能改畫面（位置、大小、顏色、圖示、裝飾、分組），不能改每個按鈕做的事、出現的條件、顯示的資訊。** 每個 `if (UiKit.Button(...)) { … }` 大括號裡的程式碼要原封不動保留。

| 畫面 | 檔案／函式 | 重點 |
|---|---|---|
| 首頁（選模式） | `GameRoot.cs` 的 `OnGUI` 裡 `mode == Mode.Choose` 那段 | 遊戲標題 Logo（程式畫的字樣＋光效）、四張模式卡片有插圖感圖示 |
| 場地／房間設定 | `GameRoot.cs` 的 `OnGUI` 其他段落 | 只換成新主題的按鈕與面板，版面不用大改（AR 操作中，要看得到相機畫面） |
| 選職業與技能（開戰前） | `SoloBattle.cs` 的 `DrawSetup`、`DrawDifficultyRow` | 職業卡片要有角色感（職業色、圖示），技能卡用 `SkillIcons.Get(id)` |
| 技能說明視窗 | `SoloBattle.cs` 的 `DrawSkillInfo` | 像卡牌說明：大圖示、名稱、數值表 |
| 劇情對話框 | `SoloBattle.cs` 的 `DrawDialogue` | 視覺小說風：頭像框、名牌緞帶、台詞框 |
| 結算畫面（勝利／失敗） | `SoloBattle.cs` 的 `DrawHud` 最後 `phase == Phase.Over` 那段 | 勝利有光芒、失敗偏暗 |
| 角色畫面（能力值、裝備） | `CharacterPanel.cs` 的 `Draw`、`DrawStats`、`DrawGear` | 分頁籤、能力值列、裝備格子（稀有度色邊框）、背包格子 |
| 技能樹 | `CharacterPanel.Tree.cs` | 另見 [`SKILLTREE_UI_BRIEF.md`](SKILLTREE_UI_BRIEF.md)；**不要標示流派**（不寫流派名、不分區塊），讓玩家自己摸索搭配 |
| 大地圖 | `WorldUI.cs` 的 `DrawMap` | 像一張手繪地圖：三個地區的地形、路線、關卡節點 |
| 城鎮（走動中的 HUD、清單模式） | `WorldUI.cs` 的 `DrawTownWalk`、`DrawTown` | 走動時介面要少、不擋畫面；名牌像木牌或對話泡泡 |
| 鐵匠鋪／道具店／職業工會 | `WorldUI.cs` 的 `DrawSmith`、`DrawShop`、`DrawGuild` | 各有店鋪風格（鐵砧火光、藥水架、公會徽章） |
| 出口選關、傳送陣 | `WorldUI.cs` 的 `DrawGate`、`DrawWarp` | 關卡卡片（通關星號／鎖）、傳送陣的城鎮清單 |

**戰鬥中的 HUD**（`DrawHud` 的血條、技能鈕、提示）只換成新主題的 `Bar`／`Button` 外觀，版面不要動，避免擋住戰鬥畫面或改變按鈕位置。

## 規則
- 純 IMGUI（`OnGUI`），字體大小用現有的 `UiKit.BaseFont` 與各函式傳進來的 `fs`／`fsS`。
- 手機直式（約 1080×2340），要注意瀏海與底部安全區（`Screen.safeArea`，現有程式已經處理，版面調整時保留）。
- 不要改：戰鬥（`Combat.cs`）、技能資料（`Skills.cs`）、技能樹規則（`Progression.cs`）、存檔、網路（`NetLink.cs`、`RelayLink.cs`）、語音、AR 追蹤。
- iOS／Android 都要能編譯（Claude 會再檢查與建置）。

## 驗收
如果有工具，產生 `docs/ui_preview.png`：把新的 Card／Button／Bar／Pill／Tab／Title 各畫一個，用預覽工具或截圖都可以。
