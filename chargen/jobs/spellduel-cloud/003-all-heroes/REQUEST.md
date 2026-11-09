# 工單 003：其餘 15 個職業的主角 3D 模型（照大魔導師的做法）

## 目的
第三人稱主角。大魔導師（工單 001＋002）已經在遊戲裡了，效果確認可以用。
請用**同樣的做法、同樣的規格**把其他職業都做出來。遊戲端已經寫好：資料夾一放進來就自動換上，不用改程式。

## 職業清單（外觀一律以立繪為準）
立繪：`spellduel-unity/Assets/SpellDuel/Resources/Story2D/classes/<id>.png`

| 順序 | id | 職業 | 武器（以立繪為準） | 普攻 `attack` 怎麼做 |
|---|---|---|---|---|
| 1 | `mage` | 法師 | 法杖 | 法杖往前推出法球 |
| 2 | `swordsman` | 劍士 | 劍 | 單手揮砍 |
| 3 | `archer` | 獵人 | 弓 | 拉弓放箭 |
| 4 | `assassin` | 刺客 | 雙匕首 | 快速突刺 |
| 5 | `priest` | 祭司 | 聖杖 | 舉杖放出聖光 |
| 6 | `novice` | 初心者 | 木棍 | 揮棍 |
| 7 | `paladin` | 聖騎士 | 劍＋盾 | 揮砍（盾在左手） |
| 8 | `berserker` | 劍仙 | 直劍 | 大幅度揮劍 |
| 9 | `marksman` | 神射手 | 長弓／弩 | 瞄準射擊 |
| 10 | `ranger` | 遊俠 | 弓 | 拉弓放箭 |
| 11 | `summoner` | 召喚師 | 法杖／書 | 舉杖召喚 |
| 12 | `ninja` | 忍者 | 苦無／忍刀 | 投擲或斬擊 |
| 13 | `shadowdancer` | 影舞者 | 雙刃 | 迴旋斬 |
| 14 | `bishop` | 主教 | 權杖 | 舉杖放出聖光 |
| 15 | `inquisitor` | 審判者 | 照立繪 | 照立繪的武器 |

可以分批交（例如先 1～6 基本職業，再 7～15 進階職業），每做完一批就推一次。

## 每個職業的規格（跟大魔導師一樣）
- 日式動畫賽璐璐、黑色反轉外殼描邊（`OUTLINE_*`）、跟立繪一致
- 身高約 1.72m（矮的職業照立繪比例），腳底在原點、Y 朝上、正面 +Z
- 跟大魔導師同一套骨架命名（`J_Bip_*`），武器掛在手的骨頭底下（`Weapon_R`、需要的話 `Weapon_L`、`Shield`）
- 本體 ≤ 15,000 三角形；一張 2048 albedo＋一張發光遮罩
- 授權：只能用 CC0 或自己做的素材，`sources.json` 寫清楚

## 動作（名字一定要照這個）
| 名字 | 內容 | 循環 |
|---|---|---|
| `idle` `walk` `run` | 待機、走、跑 | 是 |
| `walk_back` `walk_left` `walk_right` | 面向前方後退、左右側步 | 是 |
| `run_back` `run_left` `run_right` | 後退跑、左右側跑 | 是 |
| `attack` | 普攻（見上表，用**武器**做，0.5～0.8 秒） | 否 |
| `skill` | 放技能（用武器，約 1 秒） | 否 |
| `skill_loop` | 詠唱／蓄力中（舉起武器聚力） | 是 |
| `hit` `crouch` `jump` `die` `victory` | 同大魔導師 | 照 001 |

全部原地播放（不要根位移），30fps。

## 交貨位置
- `spellduel-unity/Assets/SpellDuel/Resources/CharKit/<id>_v1/`
  - `<id>.fbx`（全部動作在同一個檔，或另外放 `<id>@動作.fbx`）
  - `<id>_albedo.png`、`<id>_emission.png`
  - **不用附 shader**（遊戲共用 `Resources/Shaders/CharCel.shader`）；不要放 .meta、Prefab
- `chargen/jobs/spellduel-cloud/003-all-heroes/<id>/`：`response.json`、`sources.json`、`previews/`（正面、背面、第三人稱後上方、跟立繪並排、attack 動作）
- 不要改 `spellduel-unity/Assets/SpellDuel/Runtime/` 的程式

## 交貨
每一批：push 到 `claude/mobile-camera-mic-battle-game-v5nhfr`（commit 訊息不要加 `[skip ci]`），推完執行 `git log origin/claude/mobile-camera-mic-battle-game-v5nhfr -1`，回報完整 commit SHA 和這批做了哪些職業。
