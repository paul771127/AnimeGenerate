# Codex 任務：大魔導士、遊俠新技能的特效（使用者要求，最優先）

使用者原話：「光之龍的特效要改成一條龍在飛」「把剛剛新加的這些技能的特效都請 Codex 做上去」。

**分工照舊**：Codex 做模型、動畫、特效、圖示；不要改戰鬥邏輯（`Combat*.cs`）、網路、存檔。下面標「新介面」的請照簽名加在美術檔（`SpellFx*.cs`、`SkillIcons.cs`），寫在 `CODEX_PENDING_BRIEF.md` 最後的「Codex 實作紀錄」，Claude 再接線。完成後推到 `claude/mobile-camera-mic-battle-game-v5nhfr`；Android、iOS 都要編譯通過。預覽放 `Docs/pending_preview/new_skill_fx/`（每招第一人稱＋旁觀各一張；光龍請附一段 GIF 或連續 4 張）。

所有技能資料在 `Skills.cs`（搜尋 id 即可），顏色用 `SkillDef.color`。現在這 9 招的畫面都是通用的球／灰色圓盤，沒有專屬外觀。

---

## A. 大魔導士（5 招）

### 1. 操魔光龍 `arcanewyrm` —— 使用者點名，第一個做

- 玩法：直線放出、**飛行中跟著玩家的準心轉向**（玩家轉頭就能帶著它繞彎，`SkillDef.steer`），穿過小怪繼續飛（`pierce`），速度 6m/s、最遠 16m、判定半徑 0.2m、顏色 `#a5f3fc`（淡青白）。
- 要做成**一條東方長龍在空中游動**，不是一顆球：
  - 龍頭：角、鬚、張開的嘴，眼睛發光；龍頭位置就是投射物位置（判定點），朝飛行方向。
  - 龍身：約 2～2.5m 長、由頭往尾漸細，帶鱗片光紋和背鰭；**龍身要沿著龍頭實際飛過的路徑排列**（記錄龍頭最近的位置歷史，身體各節取路徑上的點），所以玩家帶著它轉彎時整條龍會蜿蜒跟上，不是整根硬棒轉向。剛放出時身體從手邊逐漸「長出來」。
  - 游動感：身體在路徑上再疊一點左右／上下的正弦擺動；龍尾和鬚有拖尾光粒。
  - 穿過小怪時：那一點爆出小火花，龍不停。消失（射程到、打到 Boss）時從頭往尾化成光粒散掉。
- 介面：在 `SpellFx.CreateProjectileVisual` 加 `case "arcanewyrm"`。Claude 每幀設定根物件的位置與朝向（`LookRotation(飛行方向)`），物件被 Destroy 就是消失（請在 `OnDestroy` 前不要依賴其他通知；如果需要「散掉」的動畫，請另加 `SpellFx.WyrmFade(GameObject wyrm)`，Claude 在消失時改呼叫它，由它負責延遲銷毀）。
- **注意 AR 座標**：衝刺時場景會整體平移（`PlayArea`／`WorldFrame.Marker` 會變，`ShiftWorld` 只會平移場景根物件和 LineRenderer／TrailRenderer／ParticleSystem）。龍身路徑歷史請存在**戰場座標**（`WorldFrame.ToWorld(position)`），畫的時候再 `WorldFrame.FromWorld` 轉回來；不要存 Unity 世界座標，不然衝刺時龍身會被甩在原地。
- 隊友（連線）看到的是同一個投射物，同樣的外觀。
- 預算：龍身用一個動態 mesh 或 ≤24 節，不要每節一個粒子系統。

### 2. 隕星天降 `starfall`
- 玩法：準星指的位置、直徑 7m（`areaRadius` 3.5）範圍內連續落下 3 波流星（間隔 0.6 秒），每波範圍內敵人全部受傷並灼燒。顏色 `#fb7185`。
- 要做：放出時地上出現 7m 的星陣預警圈；每波從高空斜落好幾顆小流星（不是一顆大的，和法師「隕石」區分），落地時圈內火花＋衝擊環；第 3 波最大。
- 介面：投射物外觀 `CreateProjectileVisual` 的 `case "starfall"`（Claude 已經在用，`atTarget` 從上方落下）；新介面 `SpellFx.StarfallZone(Vector3 centerWorld, float radius, float seconds)` 畫地上的星陣，Claude 在放出時呼叫。

### 3. 奧術崩滅 `arcanebreak`
- 玩法：單體超高傷害（130），飛得慢（9m/s），2m 內打不到。顏色 `#e879f9`。
- 要做：被壓縮到極限、表面不穩定在顫動的紫粉色奧術球，周圍有被吸進去的光線；命中時先向內一縮再炸開成大片奧術碎光＋短暫畫面扭曲感。要比一般魔法彈明顯「重」。
- 介面：`CreateProjectileVisual` 的 `case "arcanebreak"`；命中特效走現有 `SpellFx` 命中流程（`HitClassOf` 會回 `"mage"`），如需專屬命中請加 `SpellFx.ArcaneBreakHit(Vector3 atWorld)`。

### 4. 奧術加速 `arcanehaste`、5. 元素結界 `elementward`（隊伍增益）
- 奧術加速：自己與隊友 12 秒內詠唱／普攻變快 25%（青色 `#67e8f9`）。施放瞬間腳下時鐘法陣往上掃過全身；持續中手邊有細小的時鐘指針光繞著轉。
- 元素結界：自己與隊友 8 秒減傷 25%（金色 `#fcd34d`）。施放時火、水、風、土四色光片繞身旋轉後合成一層薄殼；持續中淡淡四色護殼，受擊時那一片亮一下。
- 介面見下面 **C. 隊伍增益共用介面**。

---

## B. 遊俠（4 招）

### 6. 爆裂地雷 `landmine`
- 玩法：埋在地上（判定半徑 0.4m），1.5 秒後才啟動，最多 3 顆，存在 40 秒；敵人踩到就**大爆炸，半徑 2.5m 內的敵人全部受傷**。技能樹可以一次放 2～3 顆（間距 1.6m）。顏色 `#f97316`。
- 現在：所有陷阱都是 `SoloBattle.cs` 裡一個扁平灰色圓盤（`trapGo`），爆炸借用「震地」的效果。
- 要做：
  - 地雷本體：半埋在地上的金屬圓盤＋引信，未啟動時暗，啟動後中間紅燈一閃一閃（越靠近敵人可以閃快一點，選做）。
  - 爆炸：地面火球＋衝擊環剛好 2.5m 半徑（範圍一眼看得出來）、碎石往外噴、煙柱，比捕獸夾大很多。
- 新介面：
  - `GameObject SpellFx.CreateTrapVisual(SkillDef s)`：回傳陷阱外觀；請順便把捕獸夾 `snaretrap`、連環陷阱 `trapchain` 也做成專屬外型（現在也是灰圓盤），不認得的 id 回傳 null（Claude 會用原本的圓盤）。
  - `void SpellFx.SetTrapArmed(GameObject trap, bool armed)`：未啟動／已啟動的外觀切換。
  - `void SpellFx.MineBlast(Vector3 atWorld, float radius, Color c)`：地雷爆炸。
  - Claude 會在 `SoloBattle.cs` 的陷阱更新換成這幾個介面，並在地雷觸發時呼叫 `MineBlast`。

### 7. 疾風箭意 `rangerhaste`、8. 鷹眼凝神 `hawkeye`（隊伍增益）
- 疾風箭意：10 秒加快 30%（青綠 `#5eead4`）。施放時一陣綠色旋風捲過全身；持續中弓身帶風紋、腳邊有風線。
- 鷹眼凝神：10 秒爆擊率 +25%（金色 `#fbbf24`）。施放時眼前浮現金色鷹眼紋章一閃；持續中第一人稱畫面邊緣淡淡金色準星框（不要擋中間），旁人看是身上金色鷹羽光。
- 介面見 **C**。

### 9. 獵鷹俯衝 `falcondive`（被動「獵鷹突襲」「獵鷹巡獵」觸發）
- 玩法：遊俠不用召喚獵鷹，被動觸發時一隻獵鷹從遊俠頭上方俯衝到目標，0.35 秒後打中（傷害由 Claude 處理）。
- 現在只有一行字「🦅 獵鷹突襲！」，看不到鳥。
- 要做：一隻發著橘金光的獵鷹（可以沿用 `PetRig.BuildFalcon` 的造型縮小＋發光），從施放者頭頂後方出現 → 收翅俯衝 → 0.35 秒到目標胸口 → 爪擊火花＋羽毛 → 往上拉起飛走淡出。
- 新介面：`void SpellFx.FalconDive(Vector3 fromWorld, Vector3 toWorld, float seconds)`，Claude 在觸發時呼叫（from＝遊俠頭頂、to＝目標胸口、seconds＝0.35）。

---

## B2. 其他進階職業的新技能（使用者要求「其他進階職業的技能也多樣點」，Claude 已實作邏輯，19 招）

這些招都走現有的投射物／範圍／增益流程，**現在看得到但都是通用外觀**。請在 `CreateProjectileVisual` 依 id 加專屬外型、命中特效，增益類用下面 **C** 的介面（`TeamBuffCast`／`TeamBuffAura`，對自己的增益也用它）。範圍類（`areaFoes`）目前只有震地效果，請加 `SpellFx.AreaBurst(string skillId, Vector3 centerWorld, float radius)`，Claude 在每段傷害時呼叫。

| 職業 | id | 名稱 | 玩法（數值見 `Skills.cs`） | 特效方向 |
|---|---|---|---|---|
| 聖騎士 | `holyblade` | 聖光斬 | 新月劍氣直線 6m | 金色新月劍氣、拖光 |
| 聖騎士 | `consecrate` | 聖光震地 | 自己周圍 2.5m、每 0.8 秒 1 段共 4 段 | 腳下聖紋一圈一圈往外擴 |
| 聖騎士 | `layhands` | 聖療之手 | 自己與隊友回 25% HP | 手掌金光、身上光羽 |
| 神射手 | `headshot` | 爆頭狙擊 | 單體重擊、斬殺 | 細長紅色彈道、命中爆頭火花 |
| 神射手 | `scattershot` | 扇形散射 | 5 箭扇形 | 5 支黃色光箭 |
| 神射手 | `blastarrow` | 爆裂箭 | 準星位置 2m 爆炸＋灼燒 | 箭插地→火焰爆炸 |
| 召喚師 | `spiritbolt` | 靈魂飛彈 | 3 顆扇形 | 淡紫色小靈魂（有臉／尾巴的小鬼火） |
| 召喚師 | `earthspike` | 大地之棘 | 準星位置 1.8m 石刺＋定身 | 地面竄出一圈石刺 |
| 召喚師 | `pactcheer` | 契約鼓舞 | 隊伍攻擊 +20% | 橘色契約符文、召喚物也亮起來 |
| 忍者 | `suiton` | 水遁・水牢 | 準星位置 1.8m 水牢＋定身 | 水柱升起包住敵人 |
| 忍者 | `futon` | 風遁・真空刃 | 新月風刃、穿透 | 透明綠色真空刃、空氣扭曲 |
| 影舞者 | `bladestorm` | 影刃旋舞 | 周圍 2m 連斬 4 段 | 紫色刀光旋風 |
| 影舞者 | `poisondart` | 毒鏢扇 | 3 枚毒鏢、中毒 | 綠色毒鏢、命中毒霧 |
| 影舞者 | `nightveil` | 暗影斗篷 | 自己減傷 30% 6 秒 | 黑紫斗篷煙霧 |
| 主教 | `holynova` | 聖光新星 | 自己周圍 3m | 從身上爆開的白金光環 |
| 主教 | `divinegrace` | 神蹟 | 全隊淨化＋回血 | 綠白光雨落在每個人身上 |
| 主教 | `lightpillar` | 裁光之柱 | 單體從上方落下 | 細長白金光柱（旁觀者看；第一人稱別擋畫面） |
| 審判者 | `holyfire` | 聖火 | 單體＋灼燒 | 金橘色聖火球 |
| 審判者 | `verdict` | 裁決之錘 | 從上方落下的重擊＋暈眩 | 巨大的光之戰錘砸下、地面裂紋 |

## B3. 基本職業還沒有專屬特效／圖示的技能（使用者要求「其他職業有新增的技能也一併加入」）

Claude 盤點了所有玩家技能，下面這些**沒有圖示**（`SkillIcons.cs` 沒有對應的 case），或是**只有通用外觀**：

| 職業 | id | 名稱 | 類型 | 要做 |
|---|---|---|---|---|
| 獵人 | `focus`（見下方注意） | 精神集中 | 自身：20 秒冷卻減半 | 圖示＋施放／持續特效（用 C 的介面） |
| 法師 | `meditate` | 冥想 | 自身：回 30 MP | 圖示＋藍色魔力往身上聚集 |
| 刺客 | `prep` | 刺殺準備 | 自身：蓄力伏擊 | 圖示 |
| 劍士 | `taunt` | 嘲諷 | 自身：減傷＋拉仇恨 | 圖示（特效已有 `sword_taunt`） |
| 牧師 | `bless` | 聖光祝福 | 隊伍攻擊 +% | 圖示＋ C 介面的施放／持續特效 |
| 牧師 | `haste` | 迅捷禱言 | 隊伍加速 | 同上 |
| 牧師 | `spring` | 生命湧泉 | 隊伍每秒回血 | 同上 |
| 牧師 | `focus`（見下方注意） | 精準聖歌 | 隊伍爆擊 +% | 同上 |
| 獵人（遊俠） | `rollarrow` | 翻滾箭 | 翻滾射擊自動射出的箭 | 投射物外觀（綠色、帶翻滾殘影） |
| 法師 | `echo_fire`、`echo_ice` | 回響火球、回響冰錐 | 奧術回響追加的小火球／冰錐 | 投射物外觀（比火球／冰槍小一號、帶紫色奧術光） |
| 法師 | `arcaneburst` | 奧術爆裂 | 奧術飛彈命中爆炸 1.5m | 用 `AreaBurst` 介面做紫色爆開 |
| 刺客 | `venom_burst` | 毒爆 | 毒疊滿爆開 | 用 `AreaBurst` 介面做綠色毒爆 |

**注意**：獵人的「精神集中」和牧師的「精準聖歌」程式裡用了同一個 id `focus`（既有 bug，Claude 另外處理；處理完 id 可能會變，請以 `Skills.cs` 為準）。圖示請先依 `SkillDef.cls` 分開畫（`archer`／`priest`）。

## C. 隊伍增益共用介面（4、5、7、8 用）

- `void SpellFx.TeamBuffCast(string skillId, Transform owner)`：施放瞬間的特效（自己和每個吃到增益的隊友都會各呼叫一次）。
- `void SpellFx.TeamBuffAura(Transform owner, string skillId, float remain01)`：持續中的常駐外觀，Claude 每幀更新，`remain01` 從 1 降到 0，0 時請收掉；同一個人可能同時有好幾種，請各自獨立。
- 第一人稱（owner 是自己）：只在畫面邊緣表現，不要擋住中間。
- 和既有的牧師增益、審判者／劍士護盾外觀要分得出來。

## D. 技能圖示

`SkillIcons.cs` 加這 9 個 id，以及 B2、B3 表格裡的 id 的圖示（照現有 `case "rollshot": RollShot(cv, sc)` 的寫法）：`starfall`、`arcanebreak`、`arcanewyrm`（龍頭）、`arcanehaste`、`elementward`、`landmine`、`rangerhaste`、`hawkeye`、`falcondive`。

## 交付順序

**1（光龍）→ 6（地雷）→ 9（獵鷹俯衝）→ C（4、5、7、8）→ 2、3 → B2 → B3 → D**。每做完一項就推，Claude 會接線。
