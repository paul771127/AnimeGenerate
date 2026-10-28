# Codex 任務：還沒做完的項目（合併單）

使用者要求：「沒做的一起開單請 Codex」。這份把目前所有還沒交的 Codex 工作放在一起，讀這一份就夠了；細節在各自的任務單。

**分工照舊**：Codex 做模型、動畫、特效、2D 圖、設計提案；不要改戰鬥邏輯（`Combat*.cs`）、網路、存檔。每做完一項就先推到 `claude/mobile-camera-mic-battle-game-v5nhfr`，在 `CODEX_PENDING_BRIEF.md` 最後的「Codex 實作紀錄」寫下做了什麼。Android、iOS 都要編譯通過。

## 1. 「空賊船長」的劇情立繪（修好全站 Snapshot 檢查）—— 最優先

- 現況：天穹飛毯副本（`Resources/dungeons.txt` 的 `sky_carpet`）首領房的台詞說話人是「空賊船長（boss）」，但 `Story2D.IdOf` 沒有這個人，`PendingArtAudit` 報「speaker has no portrait id: 空賊船長」，整個共用 Snapshot 失敗。
- 要做：
  - 新角色立繪：空賊飛艇「逐風號」的船長。造型配合飛艇（紅白氣囊、黃銅、葡萄紫＋黃銅的空賊配色），敵方、豪邁、狡猾。
  - 照現有立繪規格（`Resources/Story2D/chars/<id>_<表情>`，768×1152），至少 normal、angry、smile、serious 四種表情。
  - 在 `Story2D.cs` 的對照表加上「空賊船長」→ 新 id；3D 對話頭像（`Portraits.cs`）如果需要也補上。
  - 修好後全站 Snapshot 要能通過，不要刪掉或跳過原本的檢查。
- 順便檢查：`dungeons.txt`、`story.txt` 裡所有說話人都有立繪（同一個檢查會列出）。

## 2. 我方 3D 角色的動作 —— 詳見 `CODEX_TASK_PLAYER_POSES.md`

6 個基本職業＋10 個進階職業，每一種的站姿、戰鬥架勢、蹲、跳、蓄力、施放技能、防禦。介面、規則、預覽要求都在那份任務單。

## 3. 一般小怪、特殊敵人、菁英的第二招：特效（提案已確認、邏輯已做好）

- 你交的 `Docs/ENEMY_MOVES_PROPOSAL.md` 已由使用者確認（開頭「使用者定案」：菁英是**另外加的第三招**；裂岩甲蜥雙環改隔 2 秒）。
- Claude 已實作 48 招，表在 `Combat.EnemySkills.cs` 的 `EnemySkills.ByKind`，技能 id 是 `ek_<kind>_<名稱>`（例如 `ek_0_emberclaw`、`ek_22_platevolley`、`ek_26_kilnmend`）。
- 目前畫面走你上一輪的通用介面，已經看得到：`EnemySkillFx.Telegraph／Cancel／Cast`、`EnemyShotView.Create`（`EnemySkills.Find(id)` 已經找得到這 48 招，形狀、角度、尺寸照表）。治療類（26、31、36、41、46）用 `EnemySupportFx.Heal`。
- 要做的：
  1. 依提案每一招的「可見預警」做專屬外觀（例如熾爪掠火的火星直線、低空火輪的貼地火環、裂板直射的三片背板依序亮）。在 `EnemySkillFx` 依 skillId 分支即可，介面不變。
  2. 新形狀提示：`EnemyCue.pose`＝1（直線／投射物也能跳過，例如低空火輪、滾石、背鰭滑切）要畫「橘色＋向上雙箭頭」；＝2（也能蹲過，例如平翼掠肩、翼下切風）畫「藍色胸口帶＋向下箭頭」。定義在 `EnemySkillDef.pose`。
  3. 投射物外型（`EnemyShotView.Create`）：低空火輪、滾石、雙點校射、釘莢、裂板、熔栓、鎖弩、砂晶。
  4. 小怪本身的預備動作：目前用 `MinionRig.Windup(秒數)`（原本那一招的預備）；如果要每招不同的姿勢，請在 `MinionRig`／菁英的模型加 `Windup(string move, float seconds)`，寫在實作紀錄，Claude 再接。
- 規則同前：不改 `Combat*.cs`；Android、iOS 都要編譯通過；預覽放 `Docs/pending_preview/enemy_moves/`。

## 4. 新精魄的圖示（選做）

- 天穹飛毯的 6 顆精魄已經有圖示（`soul_sky_*`）。還沒有圖示的新精魄：輔助型 141～144、人形敵人 151～165（`Souls.cs` 的 `soul_m141`… `soul_m165`）。
- 照現有精魄圖示的畫風，用 `SkillIcons.Get` 能取到的 id（例如 `soul_m141`）。目前精魄介面只有文字；圖示畫好後，Claude 會把精魄圖鑑、鑲嵌介面改成顯示圖示。

## 交付順序
1 → 2 → 3（特效）→ 4。
