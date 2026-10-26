# Codex 總單：目前還沒交付的美術與畫面

這張單把目前還沒做的 Codex 工作集中在一起，依優先順序排列。每一項做完，就在本檔最後的「Codex 實作紀錄」寫下做了什麼、檔案放在哪裡。

**共通規則**
- Unity 2022.3：Android 與 iOS 都要編譯通過。
- 不要改戰鬥邏輯、數值、存檔格式和網路訊息。只做美術，以及把美術接到現有的入口。
- 2D 圖的畫風、解析度、檔名規則，沿用 `Resources/Village2D/`（城鎮）和 `Resources/Story2D/chars/`（立繪）裡現有的檔案。
- 預覽圖放在 `Docs/pending_preview/`。

---

## 1. 城鎮新建築：⚗ 藥劑師、🛠 工匠（最優先）

功能已經做好了（`WorldUI.Craft.cs`、`Crafting.cs`）。現在的入口只是兩顆暫時的按鈕：
- 道具店裡的「⚗ 藥劑師」：`WorldUI.Shop2D.cs`，`Design(40,1360,600,95)`
- 鐵匠鋪裡的「🛠 工匠」：`WorldUI.Smith2D.cs`，`D(40,1520,600,100)`

要把它們做成城鎮地圖上真正的建築。

### 1-1 城鎮地圖（5 個城鎮都要）

1. 在 `map_flame`、`map_forest`、`map_harbor`、`map_desert`、`map_snow` 的 png、`_fit.png` 和 json 各加兩棟建築。
   - json 的 key 是 `alchemist` 和 `crafter`，格式和現有的 `smith`、`shop` 一樣（x/y/w/h/label_x/label_y）。
   - 新建築不能壓到現有建築，標籤也不能和現有標籤重疊。
2. 在 `WorldUI.TownMap.cs` 裡：
   - `MapSites` 加上 `["alchemist"] = ("⚗ 藥劑師", 紫色)` 和 `["crafter"] = ("🛠 工匠", 金色)`。
   - 點擊的 switch 加兩個 case：`alchemist` 開 `Screen.Alchemy`，`crafter` 開 `Screen.Craft`，順便清掉 `craftSel` 和 `craftScroll`。
3. 建築外觀要配合各城鎮的主題：
   - 藥劑師：煉藥小屋，有冒煙的大鍋、藥草和瓶罐。
   - 工匠：工坊，有工作台、零件和設計圖。看起來要和現有的鐵匠鋪有明顯差別，不要只是另一間打鐵舖。

### 1-2 室內畫面（照 Shop2D、Smith2D 的做法）

1. 室內背景圖：`in_alchemist_<town>.jpg`、`in_crafter_<town>.jpg`。
   - 至少要有 flame 版。`FacilityBg` 目前只讀 `_flame`，可以改成先讀當地城鎮的版本，找不到再用 flame。
2. NPC 立繪：每人至少 normal、smile、serious 三種表情。
   - 藥劑師（每個城鎮一位，名字已經寫在 `WorldUI.Craft.cs` 的 `AlchemistNames`）：
     - 炭火鎮：藥婆婆・炭姨
     - 森林：藥師・青蘿
     - 港口：藥商・汐
     - 沙漠：藥劑官・賽琳
     - 雪原：旅藥師・駝鈴
   - 工匠：工匠・老錘（5 個城鎮是同一人，到處跑的老工匠）。
3. 把 `DrawAlchemy` 和 `DrawCraft` 改用 `Facility2DFrame` 呈現，也就是背景、NPC 立繪和台詞框。清單、製作按鈕和材料區沿用現有邏輯。
   - 台詞：仿照 `lines_shop.txt` 新增 `lines_alchemist.txt` 和 `lines_crafter.txt`，分成打招呼、做好了、材料不夠、做出高品質四組，每組 3～5 句。
4. 製作動畫（簡單即可）：
   - 藥劑師按「製作」：大鍋冒泡約 1 秒，變色後跳出藥瓶。
   - 工匠按「製作」：做好時發光，依稀有度變色。可以參考鐵匠的 anvil_0～5。
5. 拿掉 Shop2D 和 Smith2D 裡那兩顆暫時按鈕。新手指引不能因此壞掉（`UiLock` 的步驟沒有指向它們）。

### 1-3 圖示

- 10 種強化藥劑：`bp_str` 力量、`bp_eye` 鷹眼、`bp_regen` 再生、`bp_swift` 迅捷、`bp_wise` 智慧、`bp_ward` 抗魔、`bp_blood` 嗜血、`bp_iron` 鐵壁、`bp_vigor` 活力、`bp_wind` 疾風。
  - 戰鬥右上角的狀態圖示目前是用 `SkillIcons.Get(藥劑 id)` 程序產生的。換成真的藥瓶圖示（瓶子造型一樣，顏色不同即可）。
- 材料：`mat_ore` 鐵礦石、`mat_ore_hi` 高級鐵礦石、`mat_refine` 精煉石、`mat_refine_hi` 高級精煉石、`mat_elite` 菁英材料、5 種 Boss 核心（`mat_core_golem/tree/sea/arena/canyon`）、`mat_soul_smith` 神匠之魂。
- 圖示接到現有顯示文字的地方：背包、工匠和藥劑師的材料欄、掉落提示。找不到圖示時沿用現在的 emoji。

---

## 2. 千機迴廊的劇情立繪（測試目前失敗中）

`dungeons.txt` 裡有 7 位說話者沒有立繪 id，`classtest` 的「劇本裡所有說話者都有立繪 id」因此失敗：

| 說話者 | 建議 id | 說明 |
|---|---|---|
| 鈴音 | lingyin | 收齒輪的少女情報商 |
| 疤面亞克 | ark | 臉上有疤的盜賊頭目 |
| 小麥 | xiaomai | 要護送的小孩 |
| 老匠的殘影 | oldsmith_shade | 半透明的老工匠殘影 |
| 齒輪商人 | gearmerchant | 齒輪機械風格的商人 |
| 千機匠魂 | clockwork | 首領，參考 boss_clockwork 的 3D 造型 |
| 萬象之心 | heartcore | 機械中樞（非人形也可以，畫成發光核心） |

1. 每位至少畫 normal、angry 或 serious、surprised，放在 `Story2D/chars/<id>_<expr>.png`。
2. 在 `Story2D.Cast` 加上對應。
3. `{玩家}` 是代入玩家職業的佔位字，不需要畫。把測試改成跳過它即可。

---

## 3. 玩家 3D 角色：脖子、頭和頭髮（PLAYER_NECK_HAIR_BRIEF.md）

這張單的「Codex 實作紀錄」還是空的。MATCH_2D 那輪只做了頸部收束曲面和比較細的碎髮，使用者實際看到的仍然是「脖子跟臉沒接好」。

1. 照 `PLAYER_NECK_HAIR_BRIEF.md` 的要求逐項完成：
   - 頸部和下顎、領口無縫銜接。
   - 頭髮依你自己的審美重新設計。16 個職業可以分成幾種髮型，但要和 2D 立繪的髮色一致。
2. 交付時在原生 Unity 截一組圖：近距離正面、側面、45°、仰角各一張。
3. 寫回原檔的實作紀錄。

---

## 4. 合作模式：隊友的 3D 角色外觀檢查

合作時，每位隊友會用自己職業加裝備的 3D 角色出現。請確認在 6 人同場的情況下：
- 3D 角色和 2D 立繪一致（沿用 MATCH_2D 的成果）。
- 名牌和血條不會擋到臉。
- 隊友倒地、復活、離線消失時，都有清楚的視覺表現（倒地灰化、離線淡出）。

只改表現，不改同步邏輯。

---

## 5. 世界 Boss：只做入口美術（戰鬥之後另外開單）

三隻世界 Boss（`BossRig.CreateWorld`）目前只能在預覽中看到。這一輪只做下面兩樣，不接戰鬥：
1. 世界地圖上的「世界 Boss 出沒」標記圖示。
2. 告示板上的一張懸賞海報（三隻各一張，2D）。

---

## 優先順序

1 → 2 → 3 → 4 → 5。第 1、2 項請先做完、先推，我會先把它們接上去。

## Codex 實作紀錄

### 第 1、2 項：設施與千機立繪

- 五座城鎮各新增獨立藥劑師與工匠建築、名牌與點擊區，直接開既有 Alchemy／Craft 畫面並清除選取與捲動。原有點擊區未改，snow 仍沿用風沙驛黃土外觀。
- 新增兩張室內圖，先讀當地版本，缺圖回退 flame。六位設施人物各三表情、四組各四句台詞，套用 Facility2DFrame；製作泡泡／成品／稀有度光效只影響顯示，獨立亂數不影響製作抽值。藥劑品質台詞僅為敘事，沒有額外品質加成。
- 10 藥瓶、11 材料接入狀態、背包素材頁、製作材料、掉落提示；未知材料仍用原 emoji。111 個既有技能圖示已逐像素比對未變。
- 千機七名角色各三表情，新增 Cast 對應；臉外像素與透明輪廓鎖定。檢查略過玩家佔位字、旁白及設定行，保留劇本原文與關卡設定。
- 預覽、遮罩、生成提示與資產大小報告見 Docs/pending_preview/；原生 Unity 的資源／入口檢查已接 Snapshot，Android／iOS 結果待 CI 驗證後補記。

### 第 3 項：玩家頭頸、領口與 2D 對齊

由 Astra 逐一對照 16 職業 2D 圖，重做側分厚髮束、耳周髮際線與曲面肩披；依職業補開襟、交領、主教聖帶和圓扣。玩家頭身量測固定使用原主角髮型基準，居民路徑不變。頭骨下移、頸上環置於實際下顎內，領口有獨立內壁與厚滾邊。玩家髮殼描邊保留粗度，束間細線降為55%，NPC不受影響。細節與16職業表在 PLAYER_NECK_HAIR_BRIEF.md。

離線量測：16職業腳底與11掛點未變，三角數為目前基線94.2～98.6%，領口間隙9.66～13.06mm；這些是幾何契約檢查，原生截圖與雙平台編譯仍由 CI 驗證。

### 第 4 項：合作角色顯示

沿用現有 class／adv／gear 資料，只在外觀變更時重建造型；不改同步訊息或 Fighter。名牌依實際頭頂定位並向上避讓。倒地85%灰化，復活還原並短暫淡綠光，離線0.65秒淡出（身體與描邊同步抖色裁切）。靜止且狀態不變時不反覆寫 MaterialPropertyBlock。新增六人含裝備的原生 alive／down／revive／offline 圖與狀態、名牌避臉檢查。

### 第 5 項：世界 Boss 閱覽入口

三張768×1152懸賞海報取自既有3D辨識特徵，地圖在前3區已開啟城鎮上方顯示程序徽記，告示板可點海報放大。畫面明示調查中，只有閱覽與收起，不啟動戰鬥、不新增獎勵或存檔資料。海報生成提示在 Docs/pending_preview/worldboss_prompts.json。

### 整合驗證

修正第一批截圖測試在編輯模式誤啟音效的問題：測試期間暫時靜音並在 finally 還原，不改音效執行程式或玩家設定。原生 Unity 與雙平台驗證已完成，結果見下。

## 最終原生與平台驗收

原生 Unity 2022.3.62f2：[dac9dca](https://github.com/paul771127/AnimeGenerate/actions/runs/40268245669)。五城十入口、39 立繪、21 圖示與 366 條具名台詞對應通過；玩家佔位字略過。16 職業各 120 個最終背顱取樣全部覆髮，領口間隙 9.66～13.06mm，快取／下顎接合／130%三角上限通過。含 inactive 光環的三角比例為原目前基線的 93.2～99.2%；前段離線報告的可見網格統計口徑不同。

Astra 已目視確認最終原生後腦覆髮、主教白金前冠、胸扣貼合與完整取景，未發現新的必修問題。六人名牌在站立與倒地都高於實際頭模，灰化／復活／淡出及離線消失後不重建的檢查通過。

[平台建置 5c2787f](https://github.com/paul771127/AnimeGenerate/actions/runs/40268908768)：Android APK、iOS Xcode 匯出、未簽署 IPA、TestFlight 上傳均成功。此後續提交包含另行合入的真人分身出手動畫與影片更新，已保留；本次美術提交未改 Combat、Fighter、技能數值、存檔或訊息格式。

原生個別 PNG、來源提交與 hash 清單、2D/3D 對照及各視角在 Docs/neck_hair_preview/、Docs/pending_preview/。版面示意有明確標示，未冒充原生 OnGUI 截圖；未量測手機 AR FPS。
