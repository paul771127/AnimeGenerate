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

## C. 隊伍增益共用介面（4、5、7、8 用）

- `void SpellFx.TeamBuffCast(string skillId, Transform owner)`：施放瞬間的特效（自己和每個吃到增益的隊友都會各呼叫一次）。
- `void SpellFx.TeamBuffAura(Transform owner, string skillId, float remain01)`：持續中的常駐外觀，Claude 每幀更新，`remain01` 從 1 降到 0，0 時請收掉；同一個人可能同時有好幾種，請各自獨立。
- 第一人稱（owner 是自己）：只在畫面邊緣表現，不要擋住中間。
- 和既有的牧師增益、審判者／劍士護盾外觀要分得出來。

## D. 技能圖示

`SkillIcons.cs` 加這 9 個 id 的圖示（照現有 `case "rollshot": RollShot(cv, sc)` 的寫法）：`starfall`、`arcanebreak`、`arcanewyrm`（龍頭）、`arcanehaste`、`elementward`、`landmine`、`rangerhaste`、`hawkeye`、`falcondive`。

## 交付順序

**1（光龍）→ 6（地雷）→ 9（獵鷹俯衝）→ C（4、5、7、8）→ 2、3 → D**。每做完一項就推，Claude 會接線。
