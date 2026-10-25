# 玩家 3D 角色對齊 2D 立繪（給 Codex）

## 使用者的要求

> 把所有玩家 3D 角色，盡量修到跟 2D 圖一樣。

玩家能用的 16 個職業都已經有 2D 立繪。3D 戰鬥／城鎮角色的造型要盡量對齊這些立繪。

- 對象：初心者、5 個基本職業、10 個進階職業。
- 要對齊的項目：髮型、髮色、臉、服裝輪廓與配色、配件、武器。
- 目標：玩家看 2D 頭像和 3D 角色時，一眼就知道是同一個人。

## 參考圖（2D 立繪）

`Assets/SpellDuel/Resources/Story2D/classes/<id>.png`：

| id | 職業 | id | 職業 |
|---|---|---|---|
| novice | 初心者 | | |
| swordsman | 劍士 | paladin／berserker | 聖騎士／劍仙 |
| archer | 獵人 | marksman／ranger | 神射手／遊俠 |
| mage | 法師 | archmage／summoner | 大魔導士／召喚師 |
| assassin | 刺客 | ninja／shadowdancer | 忍者／影舞者 |
| priest | 牧師 | bishop／inquisitor | 主教／審判者 |

- 進階職業的 3D 外觀由 `CharacterRig.AdvancedId` 決定。
- 劍仙的 id 是 `berserker`（歷史原因，不要改 id）。

## 現在 3D 角色的程式

| 部位 | 檔案 |
|---|---|
| 身體、比例、骨架 | `Runtime/CharacterRig.cs`、`CharacterRig.Body.cs` |
| 服裝 | `CharacterRig.Outfit.cs`、`CharacterLook.cs` |
| 臉 | `AnimeFace.cs`、`HumanFaceLight.cs` |
| 頭髮 | `AnimeHair.cs` |
| 武器、裝備外觀 | `CharacterRig.Gear.cs`、`CharacterRig.GearArt.cs`、`GearLook.cs`、`GearVisualLease.cs` |
| 角色畫面的 3D 預覽 | `GearPreview.cs` |
| 著色 | `Shaders/Toon.shader`、`ToonStyle.cs` |

渲染截圖可以用 `.github/workflows/spellduel-snapshots.yml`（`Editor/Snapshot.cs`），把每個職業的 3D 角色輸出成 PNG。

## 要做的事

### 1. 逐一比對

每個職業的 2D 立繪和現在的 3D 截圖要逐一比對，把差異列在本檔最後的「Codex 比對表」：

- 髮型（長短、瀏海、馬尾／辮子、翹髮）與髮色。
- 眼睛顏色、眉型、表情氣質。
- 服裝：
  - 上衣、外袍、披風、裙／褲的輪廓（長度、開衩、層次）。
  - 主色、副色、滾邊顏色。
  - 金屬件、紋章、腰帶、綁帶。
- 配件：帽子、頭冠、髮飾、耳飾、圍巾、肩甲、護手、靴子。
- 預設武器：形狀、顏色、拿法（單手、雙手、背在身上）。

### 2. 改 3D

照比對表修改 3D 角色。差異大的先改，順序如下：

1. 剪影：頭髮、外袍、披風、帽子、武器。
2. 配色。
3. 小配件。

### 3. 輸出對照圖

把每個職業的 2D 立繪（左）和 3D 正面＋斜 45° 截圖（右）並排，放在 `Docs/match2d_preview/`。另外附一張 16 個職業的總覽圖。

## 限制（一定要遵守）

- **純程式繪製**：可以在執行時產生 Mesh、`Texture2D`、頂點色，不能加入模型檔或貼圖檔。2D 立繪只能拿來看，不能貼在 3D 上。
- **手機效能**：
  - 一個角色的三角形數，最多比現在多 50%。
  - 場上最多 6 個玩家加上 Boss，不能明顯掉幀。
  - 要在 iOS（Metal）和 Android（OpenGL ES 3）上都能跑。
- **不能改遊戲邏輯**：
  - `CharacterRig` 的公開 API 不能改：`Create`、`SetPose`、`PlayCast`、`PlayHit`、`SetDead`、`SetVisible`、`MoveSpeed`、`Charge`、`Guarding`、`ClassId`、`AdvancedId`。
  - 站立高度、腳底位置、手部掛點不能變。戰鬥判定和技能特效的發射點會用到。
  - 眨眼、張嘴、受擊、施法、普攻揮擊（包含劍仙「劍來」的飛劍跟著揮）都要照常運作。
- **裝備系統**：
  - 穿上的裝備（`CharacterRig.Gear*`、`GearLook`）照樣要顯示出來。
  - 2D 立繪的造型是「沒有穿特殊裝備時的預設外觀」。
  - 穿上裝備時用裝備的外觀覆蓋，但髮型、臉、職業特徵（例如主教的頭冠）要保留。
- **不要動的東西**：Boss、小怪、寵物、城鎮居民（另外的單子）。只改玩家職業。
- 2D 立繪和 3D 有衝突、3D 做不到的地方（例如很細的蕾絲、半透明薄紗），用最接近的做法，並寫進比對表。

## 驗收

- `Docs/match2d_preview/` 有 16 張對照圖和 1 張總覽圖。
- 不看名字，只看 3D 截圖，能把每個角色對到正確的 2D 立繪。
- 在 Unity 2022.3 編譯 Android 和 iOS 都沒有錯誤。
- 戰鬥中的動作、裝備顯示、角色畫面的 3D 預覽都正常。

## Codex 比對表

（Codex 填寫：每個職業一列，寫明「2D 立繪特徵／原本 3D／改成」）
