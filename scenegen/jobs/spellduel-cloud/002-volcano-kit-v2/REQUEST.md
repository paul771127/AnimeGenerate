# 生成器工單 002：炎之山 3D 零件包・修改版（v2）

- **發單**：SpellDuel 遊戲端（雲端 Claude Code session）
- **執行**：本機的場景生成器 session（`E:\AI接案\rpg-world-builder`，`control.py`）
- **工單資料夾**：`scenegen/jobs/spellduel-cloud/002-volcano-kit-v2/`
- **成果放到**：`spellduel-unity/Assets/SpellDuel/Resources/SceneKit/volcano_v2/`
  - 新資料夾，**不要覆寫 001 的 `SceneKit/volcano/`**。
- **前一版**：`scenegen/jobs/spellduel-cloud/001-volcano-kit/`（規格、座標、原點、`kit.json` 格式都沿用 001 的 `REQUEST.md`，這裡只寫改的地方）

## 001 的檢查結果

**做得好的，請保持：**
- 面數、尺寸、原點、單張共用貼圖、沒附 `.meta` 和 URP 材質。
- `kit.json`、`inspect.json` 寫得很完整。
- 沒有宣稱手機 FPS 達標。

**要改的是外觀**。跟 Codex 畫的手繪 2D 障礙物放在一起，差距很明顯：
1. **形狀太方**：
   - 岩壁像一塊塊方磚，頂面平、側面平。
   - 蹲的障礙物就是一張「ㄇ字桌」。
   - 一看就知道是方塊拼的。
2. **貼圖是程式雜訊**：細碎的紋路鋪滿整面，像大理石，不像手繪。
3. **發光裂縫太多**：網格狀的亮線鋪滿每一面牆，畫面很花，搶了角色和敵人的焦點。
4. **預覽圖偏紫**：天空和打光是紫色的，看不出真正的顏色。

## 這次只做這些零件

跳、蹲、側步的障礙物、噴口、脫出點底座**不用再做**：遊戲改用 Codex 的手繪 2D 圖。

生成器專心做 **2D 做不到的大型立體結構**：

| id | 版本數 | 改的地方（其他照 001） |
|---|---|---|
| `wall_straight` | 4 | 見下面「形狀」 |
| `wall_corner_out` | 3 | 同上 |
| `wall_corner_in` | 3 | 同上 |
| `wall_end` | 3 | 收尾要像自然崩落的岩塊，不是切斷的面 |
| `gate`（`gate_frame` + `gate_door`） | 2 | 門框像兩塊巨岩夾著的岩縫；門板是一塊不規則的厚岩板（會下沉打開），表面可以有一點古代刻紋（不要文字） |
| `cave_entrance` | 1 | 首領的岩洞：洞口上緣懸垂、不規則；洞口兩側有倒下的岩柱和碎石坡 |
| `backdrop_rock` | 4 | 遠方的火山岩塊、層狀岩柱、尖峰；輪廓要有明顯的高低和斜度（遠看要好認） |

## 形狀

- **輪廓要不規則**：
  - 頂邊高低起伏（同一塊牆 3～6m 之間變化）。
  - 有往外傾斜的岩片、懸垂、裂開的岩柱。
  - 牆腳有碎石坡（scree）接到地面。
- **沒有大塊的平面**：任何一個平的面不要超過約 1m²；邊緣倒角，不要 90° 的直角。
- 可以參考的岩石型態：
  - 層狀的凝固熔岩（一層一層往外凸）。
  - 柱狀玄武岩（六角柱，有高有低、有幾根斷掉）。
  - 熔岩流冷卻後的繩狀紋理（pahoehoe）。
- **接縫**：直牆左右兩端（x = ±2m）仍要能接得自然。可以用「兩端比較低、中間比較高」或「兩端用同一個輪廓」的方式；可以稍微重疊。
- 同一種零件的不同版本，輪廓要明顯不同（不是只換貼圖）。
- 面數上限照 001：岩壁 600、岩門 1,200、岩洞 3,000、遠景 1,500。
  - 把面數花在輪廓（外形的起伏）上，不要花在看不出來的小細節。

## 貼圖：改成手繪風

- **參考 Codex 的手繪 2D 圖**（這是遊戲裡的實際畫風）：
  - `spellduel-unity/Assets/SpellDuel/Resources/StageArt/Obstacles/volcano_side_0.png`、`volcano_duck_0.png`、`volcano_rock_fall.png`
  - 總表：`spellduel-unity/Docs/stage_art_preview/obstacles/all_assets.jpg`
- **畫法**：
  - 大塊的平塗加柔和漸層（賽璐璐風），每個岩面一個主色，受光面亮、背光面暗，**分得很清楚**。
  - 邊緣有一點亮色的描邊或高光（光從左上方來）。
  - 裂縫是少量、清楚的深色線，**不要細碎的雜訊紋理**。
  - 細節的大小至少 20cm；從 3m 外看要乾淨。
- **顏色**（從 Codex 的圖取樣）：

  | 用途 | 顏色 |
  |---|---|
  | 最暗（縫隙、背光） | `#2b1e1f` |
  | 暗面 | `#3a282a`、`#492c2c` |
  | 中間調 | `#5b3837` |
  | 受光面 | `#6e413f` |
  | 高光、岩緣 | `#a6624c` |

- **發光**：
  - 只在牆腳、靠近地面的地方有**少量**橘紅裂縫，佔整個表面 5% 以下。
  - 岩門的門縫、岩洞深處可以亮一點。
  - 牆的上半部不要發光。
- **貼圖密度**：岩壁至少每公尺 128 像素。整包仍共用一張 2048×2048 的顏色圖集（`volcano_kit_v2_albedo.png`）和一張發光遮罩（`volcano_kit_v2_emissive.png`）。

## 預覽

- **打光**：暖色的主光從左上方來，暗紅褐色的天空。**不要紫色**。
- `previews/` 要有：
  1. `player_view.png`：跟 001 同一個鏡頭（眼睛 1.5m、走道寬 4m、會轉彎），兩邊岩壁、遠處岩門和岩洞入口。
     - 地面請用遊戲的 `spellduel-unity/Assets/SpellDuel/Resources/StageArt/volcano_0_ground.png`（2.5m 重複一次）。
     - 路中間放 Codex 的 `volcano_duck_0.png`、`volcano_side_0.png` 當立牌。
     - 這張是用來確認 3D 零件跟 2D 圖放在一起協不協調。
  2. `compare_001_002.png`：同一個鏡頭，001 和 002 左右並排。
  3. 每種零件一張（所有版本並排）。

## 交貨

1. 模型、貼圖、`kit.json`（格式同 001，`version` 寫 2）放到 `spellduel-unity/Assets/SpellDuel/Resources/SceneKit/volcano_v2/`。
2. 工單資料夾寫回 `generator_request.json`、`response.json`、`inspect.json`、`previews/`。
3. commit 訊息開頭寫 `scenegen 002:`，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
   - 推完用 `git log origin/claude/mobile-camera-mic-battle-game-v5nhfr -1` 確認，再回報完整的 commit SHA。
4. 生成器做不到的地方（例如某種形狀、手繪貼圖的畫法），寫在 `response.json` 說明用了什麼替代方法，其他照做。
