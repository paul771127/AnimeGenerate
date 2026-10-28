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

## 3. 一般小怪的第二招：設計提案 —— 總單第 12 項

- 還沒交。寫在 `Docs/ENEMY_MOVES_PROPOSAL.md`，只出文件；確認後由 Claude 實作，再請你做特效。
- 範圍：一般小怪（`Battle.MinionNames` 0～18）、特殊敵人（19～22）、各章菁英（`EliteCatalog.cs`）。每一種提 1～2 個第二招，加上兩三種怪一起時的組合招。
- 每一招寫：觸發條件、預警（看到什麼、幾秒）、躲法（跳、蹲、側步 ≤1m、離開圈、打斷）、傷害倍率（相對原本那一招）。
- 規則和上一份提案一樣：每一招都要有明確預警；躲法要能用身體做到；套用已確認的全場躲招排程（同時只有一個要躲的預警，兩次跳／兩次蹲至少隔 2 秒）；不要用「普攻集氣加速、閃避率、爆擊率」這類玩家的效果。
- 第 4、5 章要比前面更有壓迫感。示意圖放 `Docs/pending_preview/enemy_moves/`。

## 4. 新精魄的圖示（選做）

- 天穹飛毯的 6 顆精魄已經有圖示（`soul_sky_*`）。還沒有圖示的新精魄：輔助型 141～144、人形敵人 151～165（`Souls.cs` 的 `soul_m141`… `soul_m165`）。
- 照現有精魄圖示的畫風，用 `SkillIcons.Get` 能取到的 id（例如 `soul_m141`）。目前精魄介面只有文字；圖示畫好後，Claude 會把精魄圖鑑、鑲嵌介面改成顯示圖示。

## 交付順序
1 → 2 → 3（只交文件）→ 4。
