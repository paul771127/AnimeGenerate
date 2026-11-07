# 生成器工單 003：炎之山 3D 零件・重做貼圖（v3）

- **發單**：SpellDuel 遊戲端（雲端 Claude Code session）
- **執行**：本機的場景生成器 session（`E:\AI接案\rpg-world-builder`）
- **工單資料夾**：`scenegen/jobs/spellduel-cloud/003-volcano-kit-v3/`
- **成果放到**：`spellduel-unity/Assets/SpellDuel/Resources/SceneKit/volcano_v3/`
  - 新資料夾，不要動 `volcano/`、`volcano_v2/` 和 001、002 的工單資料夾。
- **前一版**：`002-volcano-kit-v2`。零件清單、尺寸、原點、面數、`kit.json` 格式全部沿用 002。

## 使用者的要求

> 岩石風格跟中間的障礙物不要突然顏色差很多，要同一風格。

「中間的障礙物」是 Codex 畫的手繪 2D 圖，放在走道中間：
- `spellduel-unity/Assets/SpellDuel/Resources/StageArt/Obstacles/volcano_duck_*.png`
- `volcano_side_*.png`、`volcano_jump_*.png`、`volcano_rock_fall.png`
- 總表：`spellduel-unity/Docs/stage_art_preview/obstacles/all_assets.jpg`

**這次的目標**：兩邊的 3D 岩壁要跟這些 2D 圖看起來像**同一個畫家、同一種石頭**，放在一起分不出哪個是 3D、哪個是 2D 的程度。

## 002 的檢查結果

**保留**：
- 002 的**模型形狀**：不規則輪廓、懸垂、傾斜岩片、牆腳碎石坡，都很好。
- 面數、尺寸、原點、發光比例（2.5%）。

**要改的都在貼圖和著色**：
1. **低多邊形的平面著色**：一面一個顏色、稜角銳利，看起來像 low-poly 遊戲；中間的 2D 圖是有筆觸、有細節的手繪。風格不同。
2. **黑色斜槓**：深色裂縫像亂塗的黑色粗線，邊緣是鋸齒，看起來像貼圖壞掉。
3. **顏色太亮、太橘**：整面亮橘紅；2D 圖是暗的紫褐、灰褐色。牆佔大半個畫面，顏色一差就非常明顯。

## 怎麼做

### 1. 用 2D 圖本身當貼圖的來源（最重要）

- 不要自己另外產生紋理。請**從 Codex 的 2D 圖裡裁出岩石表面**（`volcano_side_0/1/2`、`volcano_duck_0/1`、`volcano_rock_fall` 的岩面部分，避開透明邊緣和發光裂縫），做成 2～4 張可以無縫重複的手繪岩石貼圖。
- 再用三向投影（triplanar／box projection）把這些貼圖烘到零件的 UV 圖集上。
- 這樣筆觸、顏色、石頭的紋理就跟中間的障礙物是同一份。
- 貼圖密度：每公尺約 100～150 像素（跟 2D 圖在遊戲裡的大小差不多），不要放大到糊掉。

### 2. 著色：柔和，不要平面

- **用平滑著色**，不要一面一色：法線平滑角度約 40～50°，大的轉折保留稜線、小的轉折平滑過去。
- 烘焙的光影照 2D 圖的感覺：
  - 光從左上方來；受光面偏暖（帶一點橘），背光面偏冷（帶一點紫）。
  - 凹處、牆腳加柔和的 AO。
  - 岩塊的上緣、轉角加一點細的亮邊（2D 圖裡岩石邊緣的那種高光）。
- 可以在大的轉折處加一點深色的輪廓線，像 2D 圖的描邊，但要**細、邊緣平滑、不要鋸齒**。

### 3. 顏色：對齊 2D 圖的數值

從 Codex 的 2D 圖取樣：

| 2D 圖 | 平均 | 暗部（10%） | 中間調 | 亮部（90%） |
|---|---|---|---|---|
| `volcano_side_0` | `#563532` | `#291e1f` | `#54302e` | `#884b45` |
| `volcano_side_1` | `#513334` | `#301e1e` | `#412e34` | `#915452` |
| `volcano_duck_0` | `#563e3d` | `#2f2526` | `#5b3431` | `#8a5b59` |
| `volcano_rock_fall` | `#533b39` | `#2e2326` | `#43363a` | `#7e6364` |

- 岩壁烘好之後（含光影）的平均顏色要在 **`#4e3433`～`#5a3e3c`** 之間。
- 最亮的地方不要超過約 `#9a6058`。
- **不要整面亮橘紅**：橘色只出現在受光面的邊緣和少量的發光裂縫。
- 岩壁離鏡頭比較近、面積比較大，可以比 2D 障礙物**稍微暗一點、對比低一點**，讓走道中間的障礙物和敵人更顯眼；但色相要一樣。

### 4. 發光

- 照 002：只在牆腳有少量橘紅裂縫，比例 5% 以下。
- 裂縫的樣子照 2D 圖：細的、彎曲的亮線，不是一塊一塊的。

### 5. 要去掉的

- 002 的黑色斜槓、鋸齒邊緣。
- 任何程式感的雜訊（大理石紋、網格狀裂縫）。

## 自我檢查（請寫進 `inspect.json`）

1. 每個零件烘好之後的平均顏色（RGB 十六進位）、亮部 90% 的顏色，跟上面的範圍比較，寫「通過／不通過」。
2. 有沒有鋸齒的深色線條（請目視檢查預覽圖後寫結論）。
3. 面數照 002 的上限。

## 預覽

打光照 002（暖色主光從左上方來、暗紅褐色天空，不要紫色）。`previews/` 要有：

1. `player_view.png`：跟 002 同一個鏡頭（眼睛 1.5m、走道 4m 寬、會轉彎）。
   - 地面用 `StageArt/volcano_0_ground.png`（2.5m 重複一次）。
   - 走道中間放 Codex 的 `volcano_duck_0.png`、`volcano_side_0.png`、`volcano_jump_0.png` 當立牌。
2. `compare_002_003.png`：同一個鏡頭，002 和 003 左右並排。
3. `closeup_match.png`：一塊岩壁和 `volcano_side_0.png` 並排，近距離，看筆觸和顏色是否一致。
4. 每種零件一張（所有版本並排）。

## 交貨

1. 模型、貼圖、`kit.json`（`version` 寫 3）放到 `spellduel-unity/Assets/SpellDuel/Resources/SceneKit/volcano_v3/`。
   - 模型可以直接沿用 002 的網格，只換 UV 和貼圖。
   - 從 2D 圖裁出來的無縫岩石貼圖也一起附上（`volcano_rock_tile_*.png`），之後別的零件可以重用。
   - 沒有 `.meta`，沒有 URP 材質。
2. 工單資料夾寫回 `generator_request.json`、`response.json`、`inspect.json`、`previews/`。
3. commit 訊息開頭寫 `scenegen 003:`，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
   - 推完執行 `git log origin/claude/mobile-camera-mic-battle-game-v5nhfr -1` 確認，回報完整的 commit SHA。
4. 做不到的地方寫在 `response.json` 說明替代方法，其他照做。
