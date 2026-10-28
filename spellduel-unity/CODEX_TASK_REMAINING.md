# Codex 任務：第二輪合併單（還沒做的全部在這裡）

上一份合併單的第 1 項（空賊船長立繪）、第 2 項（16 職業動作）、第 4 項（新精魄圖示）都已交付並驗證，謝謝。這份只列**現在還要做的**；讀這一份就夠了，細節在各自的任務單。

**分工照舊**：Codex 做模型、動畫、特效、2D 圖；不要改戰鬥邏輯（`Combat*.cs`）、網路、存檔、`Blueprints.cs`、`Crafting.cs`。需要新介面就加在美術檔，寫在 `CODEX_PENDING_BRIEF.md` 最後的「Codex 實作紀錄」，Claude 再接線。每做完一項先推到 `claude/mobile-camera-mic-battle-game-v5nhfr`；Android、iOS 都要編譯通過。

## 1. 一般小怪、特殊敵人、菁英的第二招：特效（提案已確認、邏輯已做好）

- 你交的 `Docs/ENEMY_MOVES_PROPOSAL.md` 已由使用者確認（開頭「使用者定案」：菁英是**另外加的第三招**；裂岩甲蜥雙環改隔 2 秒）。
- Claude 已實作 48 招，表在 `Combat.EnemySkills.cs` 的 `EnemySkills.ByKind`，技能 id 是 `ek_<kind>_<名稱>`（例如 `ek_0_emberclaw`、`ek_22_platevolley`、`ek_26_kilnmend`）。
- 目前畫面走你上一輪的通用介面，已經看得到：`EnemySkillFx.Telegraph／Cancel／Cast`、`EnemyShotView.Create`（`EnemySkills.Find(id)` 已經找得到這 48 招，形狀、角度、尺寸照表）。治療類（26、31、36、41、46）用 `EnemySupportFx.Heal`。
- 要做的：
  1. 依提案每一招的「可見預警」做專屬外觀（例如熾爪掠火的火星直線、低空火輪的貼地火環、裂板直射的三片背板依序亮）。在 `EnemySkillFx` 依 skillId 分支即可，介面不變。
  2. 新形狀提示：`EnemyCue.pose`＝1（直線／投射物也能跳過，例如低空火輪、滾石、背鰭滑切）要畫「橘色＋向上雙箭頭」；＝2（也能蹲過，例如平翼掠肩、翼下切風）畫「藍色胸口帶＋向下箭頭」。定義在 `EnemySkillDef.pose`。
  3. 投射物外型（`EnemyShotView.Create`）：低空火輪、滾石、雙點校射、釘莢、裂板、熔栓、鎖弩、砂晶。
  4. 小怪本身的預備動作：目前用 `MinionRig.Windup(秒數)`（原本那一招的預備）；如果要每招不同的姿勢，請在 `MinionRig`／菁英的模型加 `Windup(string move, float seconds)`，寫在實作紀錄，Claude 再接。
- 規則同前：不改 `Combat*.cs`；Android、iOS 都要編譯通過；預覽放 `Docs/pending_preview/enemy_moves/`。

## 2. 特殊裝備（配方製作）的外型與圖示 —— 詳見 `CODEX_TASK_BLUEPRINT_GEAR.md`

- 15 件人形敵人配方做出來的裝備（燒肉串串大劍、咕咕警報頭盔、行動暖爐鎧、香菇大頭帽、竹筍風笛弓、會走路的盆栽靴、蘭姆酒桶鎧、水母果凍法杖、鯊魚咬咬鞋、冠軍獎盃頭盔、巨型烤玉米標槍、霓虹天使光環、招財沙漠貓護符、倒轉沙漏護符、駱駝雙峰重甲）。
- 使用者要求：**外型要夠搶眼或 kuso**。`GearLook.Piece.special` 是配方 id，在 `CharacterRig.GearArt.cs` 依它換專屬造型（可以有小動畫）；神話級加常駐光效。
- 圖示：配方卷軸 `rcp_bp_xxx`、裝備 `bp_xxx`、新材料 `mat_band_crest`、`mat_storm_gem`、`mat_night_edge`、`mat_aid_sigil`。

## 3. 牧師「審判」、審判者「天罰」：改成以自己為中心後的特效

- 使用者回報「審判放了看不到東西」。原因是邏輯 bug（已修）；同時使用者要求兩招都**改成以自己為中心**：光柱落在施法者自己腳下，周圍（審判 1.5m、天罰 3m）的敵人立刻受傷，腳下留下審判之地／天罰之地（5／6 秒）。
- 現在用的是原本的 `SpellFx.HolyLanding(at, heaven)`（`at`＝自己腳下）和 `SpellFx.HolyGround(radius, heaven)`。問題：AR 裡鏡頭就是玩家的眼睛，光柱從頭頂正上方落下會**擋住整個畫面**或根本看不到。
- 要做：重新設計成以自己為中心、第一人稱看得清楚的樣子，例如：
  - 從腳下往外擴散的金色光環＋地面聖紋（審判 1.5m、天罰 3m，範圍要一眼看得出來）；
  - 光柱改成在範圍邊緣一圈落下的多道細光，或從地面往上噴的光，不要正對鏡頭；
  - 畫面邊緣短暫的金色光暈，讓玩家知道放出去了；天罰更大、更震撼（現在有輕微震動）。
  - 被打中的敵人身上有聖光命中效果。
- 介面不變（`HolyLanding`、`HolyGround` 參數一樣），隊友看到的位置是施法者腳下。預覽放 `Docs/pending_preview/holy_self/`（第一人稱視角＋旁觀視角）。

## 4. 劍士系列的新被動：傷害轉護盾的特效

- 使用者要求：劍士系列移除吸血，改成護盾。劍士「劍氣護體」（普攻傷害轉護盾）、劍仙「劍氣凝盾」（所有傷害轉護盾）。護盾 6 秒、最多最大 HP 的 40%，一直打就一直續。
- 要做（新介面，Claude 接線）：
  - `SpellFx.SwordShieldGain(Transform owner, float fill)`：護盾增加的瞬間，劍氣碎片往身上聚攏成一層淡藍劍氣（fill＝目前護盾／最大 HP×0.4，0～1；越滿越亮）。打得很快時會一直呼叫，請自己節流（例如 0.3 秒內只播一次）。
  - `SpellFx.SwordShieldAura(Transform owner, float fill)`：護盾存在時的常駐外觀（淡淡的劍氣環繞），fill＝0 時消失；Claude 每幀更新。
  - 第一人稱：畫面邊緣淡藍劍氣框，跟 fill 一起變化，不要擋住中間。
- 和審判者的「傷害轉護盾」、一般護盾的外觀要分得出來（劍氣感、藍白色）。

## 交付順序
3 → 4 → 1 → 2（審判看不到是使用者實際遇到的問題，最優先）。
