# Codex 任務：全部介面與技能特效再美化一輪

使用者要求：

> 請 Codex 再美化一下所有介面設計與技能。

範圍很大，分成 A（介面）、B（技能圖示）、C（技能特效）三部分。**可以分批推**，每批都要能編譯、能玩。建議順序：A1 → B → A2 → A4 → C → A3。

---

## 共通原則

- **只改外觀，不改玩法**：數值、判定、按鈕的功能與觸發條件都不動。`Combat*.cs`、`Skills.cs`、`Economy.cs`、`Crafting.cs`、`Explore.cs` 的邏輯不改。
- **按鈕的位置與點擊範圍**：可以改外觀和大小，但要維持在原本大致的位置。新手教學的「按鈕鎖」（`UiLock.cs`、`SoloBattle.Walkthrough.cs`）會閃爍指定按鈕，改完要確認還對得上。
- **手機上的 emoji 常常顯示不出來**（使用者已經回報過消耗品只剩數字）。介面上用到的 emoji 圖示（💰🧪📜⚔🛡🎤❗ 等）請換成程式畫的圖示，做法參考 `ConsumableIcons.cs`、`SkillIcons*.cs`。文字裡夾的 emoji 可以保留，但要確定沒有 emoji 時版面不會壞。
- **統一的設計語言**：顏色、圓角、描邊、陰影、字級統一放在 `UiKit.cs`（例如 `UiKit.Theme`），各畫面改用它，不要每個檔案各寫一組顏色。
- **AR 可讀性**：底下是相機畫面，亮度和顏色都不固定。文字要有描邊或深色底板；重要資訊（HP、MP、冷卻、蓄力）在亮白牆與暗處都要看得清楚。
- **效能**：維持 IMGUI（`OnGUI`），不換 UI 系統。貼圖產生一次就快取，不要每幀 new 貼圖或材質。中階 Android 手機戰鬥中要能維持 60fps。
- **使用者已經定下的設計，不要改回去**：
  - 戰鬥上方沒有「第 x 波」欄，也沒有角色按鈕、待領取欄。
  - 消耗品用小圖示加數量；下方沒有普攻卡，下方面板是壓縮過的。
  - 故事探索的 NPC 對話框是不透明的，畫在最上層。
  - 衝刺的速度線（`SoloBattle.Dash.cs`）。

---

## A. 介面

### A1 戰鬥畫面（最常看到，先做）
`SoloBattle.cs` 的 `DrawHud`、`DrawStatusIcons`、`DrawCastingTile`、各職業的蓄力條（`DrawSwordCharge`、`DrawBowCharge`、`DrawMageCharge`、`DrawHolyCharge`、`DrawDaggerCharge`、`DrawKunaiCharge`…）、`DrawEnemySkillHud`、`DrawNpcBars`、`DrawStoryParty`／`DrawDungeonParty`（隊伍框）、`DrawLootRoll`（擲骰）。
- 技能卡：卡框依稀有度／職業色有質感（內陰影、邊光），冷卻用環形或由下往上的遮罩加秒數，MP 不夠時明顯變暗。
- HP／MP 條：有外框、扣血時白色殘影慢慢退、補血時綠光。
- 敵人讀條、Boss 血條、狀態圖示（燒、毒、凍、定身、護盾、增益）要一眼看懂，圖示統一大小與外框。
- 故事探索 HUD（`DrawExploreHud`、`DrawExploreMinimap`、`DrawExploreDialog`、`DrawExploreBubbles`）：小地圖加外框與指北、NPC／任務標記圖示化；對話框加名牌、頭像框；怪物對話泡泡做成圓角加小尾巴。

### A2 城鎮與選單
- 主畫面（`GameRoot.Lobby.cs`）、角色選擇與建立（`GameRoot.Characters.cs`）、設定（`SettingsMenu.cs`）。
- 戰鬥準備（`SoloBattle.Prep.cs`）、故事地圖（`DrawStoryMap`）、副本地圖（`SoloBattle.DungeonMap.cs`）。
- 角色面板（`CharacterPanel*.cs`：裝備、道具、精魄、寵物、技能樹、補給）。
- 城鎮設施：
  - 鐵匠（`WorldUI.Smith2D.cs`）、工匠配方（`WorldUI.Craft.cs`、`WorldUI.CraftArt.cs`）。
  - 道具店（`WorldUI.Shop2D.cs`）、試煉商店（`TrialShop.cs`）、村莊（`SoloBattle.Village.cs`、`UiKit.VillageArt.cs`）。
- 世界地圖（`Resources/WorldMap`）已經是 Codex 做的，這次只要配色和字體跟新的設計語言一致。
- 裝備稀有度色（普通／精良／稀有／史詩／傳說／神話）在所有清單、卡片、掉落提示要一致，傳說以上加細微的光澤動畫。

### A3 結算與提示
- 過關結算、戰敗、升級、掉落浮字（`floaters`）、教學提示卡（`DrawTip`、`DrawGuide`、放招提示 `SoloBattle.CastGuide.cs`）、讀取畫面。
- 掉落浮字依類型分色並配圖示：金幣、材料、裝備、精魄、配方。

### A4 城鎮的動態（使用者回報：城鎮看起來太靜態）
城鎮 2D 俯瞰圖（`WorldUI.TownMap.cs`，素材 `Resources/Village2D/map_*.png`）。Claude 已經先在 `WorldUI.TownAmbient.cs` 用程式加了一層：
- 居民呼吸、晃動、偶爾轉頭。
- 雲影慢慢飄過地圖。
- 各城鎮的氛圍粒子：炎之里火星、森之里落葉與螢火蟲、潮騷港海鷗、赤砂城與風沙驛飄沙。
- 鐵匠鋪冒煙。

請在這個基礎上做得更像活的城鎮（位置要對齊插畫，可以在 `map_*.json` 加錨點，例如 `chimney`、`water`、`flag`、`lamp`、`path`）：
- **走動的路人**：每個城鎮 3～5 個沒有名字的鎮民，沿著路慢慢走、停下來、轉身（`path` 錨點串成路線）。不能擋住名牌和居民的點擊範圍。
- **建築的小動畫**：
  - 煙囪冒煙（每棟有煙囪的都要，取代 Claude 只畫鐵匠鋪的那一縷）。
  - 旗子、布招牌飄動，風車轉。
  - 鐵匠鋪爐火閃、鐘樓的鐘偶爾擺。
- **水面**：潮騷港的海浪與反光、炎之里與森之里的小河或井水（有的話）。
- **燈火**：窗戶、燈籠微微閃（可以配合黃昏色調）。
- **城鎮特色**：
  - 炎之里：熔爐噴火星。
  - 森之里：葉燈搖晃。
  - 潮騷港：船隻上下起伏、燈塔的光轉動。
  - 赤砂城：角鬥場上方的旗幟與觀眾聲浪的塵土。
  - 風沙驛：駱駝商隊、駝鈴晃動。
- 效能：整個城鎮畫面 60fps；所有動態用程式畫或切好的小圖，快取，不要每幀產生貼圖。
- 新手教學會在城鎮畫面閃爍名牌和居民，動態不能蓋住它們。

---

## B. 技能圖示（`SkillIcons*.cs`）

- 所有玩家技能、轉職後的進階技能、寵物技能、敵人招式提示用的圖示，統一風格：同樣的外框、光源方向、描邊粗細；職業有自己的底色。
- 小尺寸（技能卡在手機上約 48～64px）也要看得出是什麼。
- 被動技能和主動技能的外框要能區分。
- 不新增圖片檔，用程式畫，並且快取。

---

## C. 技能特效（`SpellFx*.cs`、`AdvancedFx.cs`、`SoloBattle.BasicFx.cs`、`SoloBattle.NewSkillFx.cs`、`SoloBattle.GuardFx.cs`）

六個基本職業（初心者、劍士、獵人、法師、刺客、牧師）加上進階職業的每一招，分三段檢查並美化：

1. **蓄力**：手上或身邊的光要隨蓄力程度變強，滿了有明確的「可以放了」提示。
2. **施放／飛行**：彈道要有拖尾，大招要有前置的地面預警。
3. **命中／爆炸**：有衝擊感（閃光、碎片、地面痕跡），但 0.5 秒內要收掉，不擋視線。

規格：
- 第一人稱 AR 視角：自己放的招不能糊住鏡頭（參考 `SpellFx.SelfView.cs`），離鏡頭 1m 內要淡出。
- 隊友與敵人放的招，從旁邊看也要漂亮。
- 每個特效的粒子數、面數有上限：同時 6 個大招在場也要維持 60fps。數字寫在實作紀錄裡。
- 顏色跟職業色、技能圖示一致。
- 不改技能的範圍、時間、判定。特效的範圍要和實際判定一致（`Skills.All[id].radius`、`areaRadius`、`range`）。

---

## 驗收

- 原生預覽（每批都要），放 `Docs/pending_preview/ui_skill_polish/`：
  - A：每個畫面改前、改後各一張，手機直式（1080×2340）。戰鬥 HUD 另外各疊一張在亮背景、一張在暗背景上，確認可讀性。
  - B：所有技能圖示的總表（改前／改後），另外一張縮到 56px 的版本。
  - C：每個職業挑 3 招，各做一張蓄力、飛行、命中的連續截圖（第一人稱一張、旁觀一張）。
- 驗證腳本：沒有每幀產生貼圖或材質；圖示快取命中；新手教學的按鈕鎖還對得上；特效數量上限。
- Android、iOS 都要編譯通過，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
- 實作紀錄寫在 `CODEX_PENDING_BRIEF.md` 最後，列出改了哪些畫面、哪些技能、還沒做的部分。
