# 工單 001：主角「大魔導師」3D 角色模型（第三人稱用）

## 目的
遊戲要改成第三人稱：攝影機在角色後上方，畫面要看得到**整隻**主角。先做「大魔導師」（archmage，男性）。

## 外觀（一定要跟 2D 立繪一致）
- **參考圖**：`spellduel-unity/Assets/SpellDuel/Resources/Story2D/classes/archmage.png`（遊戲裡的立繪，臉、髮型、服裝都照這張）
- 其他角度參考（舊的程式拼的模型，只看配色、比例）：`spellduel-unity/Docs/neck_hair_preview/archmage_views.jpg`
- 重點：
  - 棕色微亂短髮、棕色眼睛、年輕男性，日式動畫臉
  - 紫黑色寬領法袍、金色滾邊與菱形金飾、白色內襯長擺、黑色內衣與褲子
  - 胸前金色繩鍊、咖啡色皮帶與斜背帶、腰間吊飾（菱形水晶）
  - 右手持長法杖：深色木杖身、頂端金色尖刺爪托著紫色發光法球
  - 左手邊浮著一本紫色封面、金色裝飾的魔法書（可做成獨立物件，掛在左手骨頭附近）
  - 紫色魔法光（法球、書）用自發光貼圖或發光遮罩

## 畫風
- 日式漫畫／動畫風格，**賽璐璐上色**（兩階明暗、乾淨色塊、少量高光），要跟 2D 立繪放在一起不突兀
- 要有**黑色描邊**：做成反轉外殼（inverted hull，獨立網格，命名 `OUTLINE_*`，法線外推約 1.2cm），或者在 response.json 註明需要遊戲端用 shader 描邊
- 光影可以烘一部分在貼圖裡（遊戲端會用不打光或簡單 toon 的 shader）
- 不要寫實材質、不要照片紋理

## 規格
| 項目 | 要求 |
|---|---|
| 檔案 | `archmage.fbx`（Unity 匯入用）；貼圖 PNG |
| 高度 | 1.72m（腳底在原點，Y 朝上，正面朝 +Z） |
| 骨架 | Unity **Humanoid** 可以直接對應的骨架（Hips、Spine、Chest、Neck、Head、左右 Shoulder/UpperArm/LowerArm/Hand、UpperLeg/LowerLeg/Foot/Toes；手指可省略）；另外 `Staff`（法杖，跟著右手）、`Book`（魔法書，跟著左手附近浮動）、法袍下擺可加 2～3 節裙擺骨 |
| 面數 | 手機用：本體 ≤ 15,000 三角形（含法杖、書），描邊另計 |
| 貼圖 | 一張 2048 主貼圖（albedo）＋ 一張發光遮罩（emission，黑底、發光處白／紫）；材質最多 2 個 |
| 動畫（同一個 FBX 或分開的 `archmage@動作.fbx`） | `idle`（待機呼吸，循環）、`walk`（走，循環）、`run`（跑／衝刺，循環）、`cast`（舉杖詠唱 → 放出，約 1.2 秒）、`cast_loop`（詠唱中，循環）、`hit`（被打，0.4 秒）、`crouch`（蹲下躲避，循環）、`jump`（跳起來，0.8 秒）、`die`（倒下）、`victory`（勝利） |
| 動畫命名 | 動作名字就用上面括號裡的英文 |

## 交貨位置
- Unity 檔：`spellduel-unity/Assets/SpellDuel/Resources/CharKit/archmage_v1/`
  - `archmage.fbx`、動畫 fbx、`archmage_albedo.png`、`archmage_emission.png`
  - **不要放 .meta**（讓 Unity 自己產生），也不要放 Unity 的 .prefab
- 工單資料夾：`chargen/jobs/spellduel-cloud/001-archmage-hero/`
  - `response.json`：檔案清單、三角形數、骨頭名字對照、動畫名字與長度、描邊做法、跟規格不一樣的地方
  - `previews/`：正面、背面、側面、**後上方第三人稱角度**（攝影機在角色後方 3m、高 1.5m 往前看）各一張，加一張跟 2D 立繪並排的比較圖

## 交貨
1. push 到 `claude/mobile-camera-mic-battle-game-v5nhfr`，commit 訊息不要加 `[skip ci]`
2. 推完執行 `git log origin/claude/mobile-camera-mic-battle-game-v5nhfr -1`，回報完整 commit SHA
3. 不要改 `spellduel-unity/Assets/SpellDuel/Runtime/` 裡的程式（遊戲端會接）
