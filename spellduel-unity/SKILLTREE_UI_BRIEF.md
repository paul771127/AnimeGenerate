# 技能樹介面重新設計（給 Codex）

使用者的要求：**技能樹優化介面設計，交給美術設計。**
技能樹的規則、資料、存檔都不變；這次只改**畫面**。程式已經獨立在 **`Assets/SpellDuel/Runtime/CharacterPanel.Tree.cs`**，請只改這個檔（可以新增 `CharacterPanel.Tree.*.cs`、`SkillTreeArt.cs` 之類的輔助檔）。

## 目前的樣子與問題
- 位置：角色畫面（📜 角色）→「🌳 技能樹」分頁（基本職業），轉職後多一個「✨ 進階」分頁（進階職業）。兩個分頁都呼叫 `DrawTree(b, p, fs, fsS, gap, treeId)`。
- 現在：上方 62% 是樹狀圖（7 欄格子、節點是 `UiKit.Button` 方塊、父子之間畫直線），下方 36% 是選中節點的說明＋「學習」按鈕。
- 問題：
  - 節點都是一樣的方塊，看不出是「習得新技能」「強化技能」「能力值」還是「被動」。
  - 節點多的樹擠在一起，字很小，手機直式很難點。
  - 點了以後沒有回饋（只有一個音效）。

## 設計目標
1. **不要標示流派**（使用者要求：讓玩家自己發揮創意）：不要寫流派名稱、不要用底色區塊或旗幟把樹分成三區，也不要在說明裡告訴玩家「這是某某流派」。樹就是一棵自然生長的樹，分支的形狀本身就是提示。`SkillTrees.Schools()` 只給開發者看，介面不要用。
2. **節點類型用形狀與圖示區分**（`TreeNode.kind`）：
   - `Learn`（習得新技能）：大圓或寶石形，中間用 `SkillIcons.Get(node.skill)` 的技能圖示；最顯眼。
   - `Mod`（強化某個技能）：圓角方形，小技能圖示＋角落的加號或箭頭；`node.field` 是強化什麼（傷害、冷卻、蓄力、多發、穿透…）。
   - `Stat`（能力值）：小圓形，用能力值的圖示與顏色（`StatInfo.Icons[(int)node.stat]`、`StatInfo.Colors`）。
   - `Passive`（被動／流派核心）：菱形或六角形，金色邊框，表示「流派的關鍵」。
3. **狀態清楚**：
   - 未解鎖（父節點還沒點）：灰暗、帶鎖。
   - 可以點（`p.NodeBlock(n) == null`）：發光或呼吸閃爍，提示「這裡可以點」。
   - 點了一部分：等級用小點／刻度表示（`p.Rank(n.id)` / `n.max`，最高 5）。
   - 點滿：金色。
   - 連線：父節點點過的路線亮起來，沒點的路線暗；可以做成樹枝、藤蔓、魔力線（依職業風格）。
4. **手機直式好點**：節點最小約 `fs * 2.2` 像素；樹太高時可以上下捲動（拖曳或按鈕都可以），不要再把整棵樹硬擠進一個畫面。
5. **回饋**：點了一級時節點閃一下、等級點亮起、連線往下亮（可以做 0.3 秒的小動畫，用 `Time.time` 計算）。音效沿用 `Sfx.Play("charged")`。
6. **說明區**：選中的節點顯示圖示、名稱、等級、「現在」與「下一級」的效果、前置條件、學習按鈕。可以重新排版，但資訊不能少（見下面的資料）。沒選節點時顯示這棵樹的說明（怎麼點、轉職前上限），不提流派。
7. **職業風格**：顏色用職業色（`Skills.Classes[cls].color`）或進階職業色（`Advanced.Get(treeId).color`）；可以依職業換連線與背景的花紋（法師符文、弓手葉紋、刺客暗影、劍士鐵紋、牧師光紋）。

## 可以用的資料與函式（只讀；只有 `Learn` 會改資料）
| 用途 | 呼叫 |
|---|---|
| 這棵樹的所有節點 | `SkillTrees.Of(treeId)` → `List<TreeNode>` |
| 節點欄位 | `id`、`name`、`kind`、`skill`、`field`、`stat`、`passive`、`max`、`req`（父節點 id）、`col`（0～6 的欄）、`row`（第幾層）、`free`（樹狀圖節點，現在都是 true） |
| 前置節點 | `SkillTrees.Prereqs(treeId, node)` |
| 第 rank 級的效果文字 | `SkillTrees.Describe(node, rank)` |
| 一開始就會的技能 | `SkillTrees.Starters(cls)` |
| 技能資料／圖示 | `Skills.All[id]`（`name`、`desc`、`color`）、`SkillIcons.Get(id)`（Texture2D） |
| 目前等級 | `p.Rank(node.id)` |
| 為什麼不能點（null＝可以點） | `p.NodeBlock(node)`（例如「技能點不足」「要先點…」「轉職前職業技能樹最多只能點 30 點…」） |
| 點一級 | `p.Learn(node)` → 成功回傳 true，`DrawTree` 要回傳 true（呼叫者會存檔並套用到戰鬥中的角色） |
| 點數 | `p.skillPoints`、`p.UsableSkillPoints`、`p.BaseSpent`、`CharacterProfile.BaseTreeCap`、`p.adv`（null＝還沒轉職） |
| 進階職業 | `Advanced.Get(treeId)`（基本職業的樹回傳 null）：`name`、`color`、`core`、`skills` |

畫圖工具：`UiKit.Card / Text / Button / Bar / Pill`、`GUI.DrawTexture`、現有的 `Line()`。可以用程式產生 `Texture2D`（例如節點外框、光暈、背景花紋），**做好快取**（不要每幀產生貼圖）。**不能用圖檔**。

## 規則
- 純 IMGUI（`OnGUI` 裡畫），和遊戲其他介面一致。字體大小用傳進來的 `fs`／`fsS`。
- 不要改：`Progression.cs`（樹的資料與規則）、`SoloBattle.cs`、`CharacterPanel.cs` 的其他分頁、戰鬥與網路程式。
- 要支援所有樹：5 個基本職業＋10 個進階職業（`Advanced.All`）。每棵樹的層數、欄數不同。
- iOS／Android 都要能編譯（Claude 會再檢查與建置）。

## 驗收
- 如果有工具，產生 `docs/skilltree_ui.png`：至少法師、刺客、聖騎士三棵樹的畫面（直式手機比例，例如 1080×2340），包含「有點過一些節點」與「選中一個節點」的狀態。
- 點節點、學習、捲動在手機上都要好操作。
