# Codex 任務：我方 3D 角色的動作（戰鬥架勢、站姿、蹲姿、跳躍、蓄力、施放技能）

使用者要求：「我方 3D 角色也要有戰鬥姿勢、站姿、蹲姿、跳躍、施放技能的樣子。」

## 哪裡會看到我方 3D 角色
- 合作模式：隊友在我畫面上的角色（`SoloBattle.UpdateAllyRigs`）——姿勢由真人的手機驅動。
- 遠端對戰的對手、觀戰模式。
- 影舞者的影分身、副本的友方劇情人物（`SoloBattle.UpdateNpcRigs`）。
- 角色選擇、角色面板的預覽（`GearPreview`）：Claude 已經讓預覽每 2.2 秒輪流展示「站姿 → 戰鬥架勢 → 蓄力 → 出招 → 防禦」，方便你和使用者檢查。

## 目前已經有的（`CharacterRig*.cs`，請在這個基礎上改進，不要換掉介面）
| 狀態 | 誰設定 | 介面 |
|---|---|---|
| 站姿／戰鬥架勢 | Claude | `InCombat`（false＝城鎮、預覽的站姿；true＝戰鬥架勢）、`HumanPersonality.Stance(id, battle)` |
| 走路 | Claude | `MoveSpeed` |
| 蹲下、跳起 | 手機高度 | `DrivePhone(...)` → `ApplyPhoneBody`：手機比站姿低＝蹲（`crouchAmt`）、比站姿高＝跳（`lift`） |
| 蓄力 | Claude（隊友的蓄力進度已經同步） | `Charge`（0＝沒有、0～1＝蓄力進度） |
| 防禦 | Claude（隊友格擋、護盾中） | `Guarding` |
| 出招 | Claude | `PlayAttack(kind, windup, reverse)`：kind＝slash、stab、shoot、magic、lunge、novice、novice_throw；`PlayCast()` |
| 受擊、倒下 | Claude | `PlayHit()`、`SetDead(bool)` |

## 要做的
六個基本職業（初心者、劍士、獵人、法師、刺客、牧師）加十個進階職業（聖騎士、劍仙、神射手、遊俠、大魔導士、召喚師、忍者、影舞者、主教、審判者），每一種都要有自己的：
1. **站姿**（`InCombat=false`）：放鬆、看得出職業個性。
2. **戰鬥架勢**（`InCombat=true`）：劍士壓低重心舉劍、獵人半拉弓、法師舉杖聚光、刺客低伏反握匕首、牧師持聖徽、初心者雙手握木棍……進階職業要和基本職業有明顯差別。
3. **蹲姿**：手機往下時自然蹲低（膝蓋、髖、上身一起動，武器不穿模），蹲到底時的姿勢要好看；配合 `crouchAmt` 0～1 連續變化。
4. **跳躍**：手機往上時身體離地、收腿、手臂上揚，落地有一點緩衝；配合 `lift`。
5. **蓄力**：依 `Charge` 0→1 漸進（劍往後收、弓越拉越滿、法杖光越來越亮、刺客壓低準備撲），每個職業不同。
6. **施放技能**：每種 `PlayAttack` kind 的預備→出手→收招更有力；法系要有「前推釋放」的手勢，和蓄力姿勢接得起來。
7. **防禦**（`Guarding`）：劍士舉盾／橫劍、其他職業的護身動作。

## 規則
- 不改戰鬥邏輯、網路、存檔；`CharacterRig` 的公開介面名字不變（可以新增，請寫在實作紀錄）。
- 手機驅動的蹲、跳優先於其他動作（真人在躲招），出招動作疊在上面。
- 每一種都要在 Snapshot 產生預覽圖（站、架勢、蹲、跳、蓄力、出招、防禦），放 `Docs/pending_preview/player_poses/`。
- Android、iOS 都要編譯通過；每做完一批職業就先推，在 `CODEX_PENDING_BRIEF.md` 的「Codex 實作紀錄」寫下做了什麼。
