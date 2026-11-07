# 生成器工單 005：賽璐璐火山零件・加上熔岩（cel v2）

- **發單**：SpellDuel 遊戲端（雲端 Claude Code session）
- **執行**：本機的場景生成器 session（`cel_kit_control.py`）
- **工單資料夾**：`scenegen/jobs/spellduel-cloud/005-volcano-cel-lava/`
- **成果放到**：`spellduel-unity/Assets/SpellDuel/Resources/SceneKit/volcano_cel_v2/`
  - 新資料夾，不要動 `volcano_cel_v1/`。
- **前一版**：`scenegen/jobs/codex-style/volcano-cel-v1/`
  - 風格、色調、零件 id、尺寸、原點、面數上限全部沿用。

## 使用者的要求

> 岩壁不能有點熔岩的感覺嗎？

cel v1 的岩壁很乾淨，但看起來像寒冷的山區，熔岩只剩門框一圈橘光。請保留賽璐璐的畫法和冷色岩石，**加上熔岩**，讓人一看就知道是火山。

## 遊戲端已經加的效果（不要重複烘）

遊戲的 shader 已經在牆腳加了「地上熔岩照上來」的暖橘反光：
- 離地 0～1.6m，越低越亮。
- 沿著牆有亮有暗，會慢慢流動。

所以**牆腳不要再烘一層橘色**，不然會太亮。

另外，發光遮罩（emissive）在遊戲裡會慢慢脈動。

## 要加的熔岩

### 1. 岩壁上的熔岩脈（所有岩壁、轉角、牆尾）

- 細的、彎曲的橘紅發光線，從岩縫裡透出來，像岩石裂開、裡面還在發熱。
- 分布：
  - 下半部多，上半部少。
  - 每塊牆有 1～3 條比較明顯的。
  - 不要整面網格。
- 畫在顏色圖裡（亮橘黃中心、紅橘外圈，賽璐璐兩階），同時寫進發光遮罩。
- 發光遮罩佔表面約 **6～10%**（v1 很少；舊的 003 是 2.5%）。

### 2. 新零件：熔岩瀑布牆 `wall_lavafall`（2 個版本）

- 跟 `wall_straight` 同尺寸、同原點、同接縫規則，可以直接替換。
- 牆的中間有一道熔岩從上面的岩縫流下來，在牆腳積成一小灘。
  - 熔岩灘最多往走道凸出 **0.6m**。
- 熔岩用**單獨的子物件** `lava_flow`，UV 往下排好（V 方向是流向）。遊戲會讓它的貼圖往下捲動，看起來在流。
- 面數上限 700。
- 遊戲會在長的直牆上偶爾（約每 6～8 段一次）放一個。

### 3. 岩門、岩洞

- **岩門**：門框的岩縫透出熔岩光；門板中間有一條發光的封印紋，可以是幾何圖案，不要文字。
- **岩洞入口**：洞口深處是熔岩的橘紅光，從裡面往外照，洞口下緣有一點熔岩流出來。

### 4. 遠景岩塊

- 至少 2 個版本改成**有火山口的火山錐**：
  - 山頂有發光的熔岩。
  - 山坡上有 1～2 條熔岩河流下來。
  - 細細的發光線，遠看要清楚。
- 遠景用單獨的發光遮罩（`volcano_cel_v2_backdrop_emissive.png`），或在 `kit.json` 註明發光在顏色圖裡。

### 5. 地面（選做）

`cel_ground_crust.png` 加幾條很細的發光裂縫，佔 3% 以下。

## 顏色

- **岩石照 cel v1**，不要變暖、不要變紅：

  | 用途 | 顏色 |
  |---|---|
  | 暗部 | `#2c3842` |
  | 中間調 | `#454b53` |
  | 亮部 | `#6f6f6b` |

- **熔岩**：中心亮黃 `#ffd36a` → 橘 `#ff8a2a` → 外圈紅 `#d8452a`。
- 冷色岩石加暖色熔岩的對比，就是這次要的「火山感」。

## 規格（照 cel v1）

- 單一主圖集＋遠景圖集；光影烘進顏色圖（遊戲用不打光的材質）。
- 不附 `.meta`、`.mat`、URP 材質。
- 面數照 cel v1 的上限；`wall_lavafall` 700。
- `kit.json`：`version` 寫 5，列出 `wall_lavafall` 和 `lava_flow` 子物件，以及熔岩捲動的方向和建議速度。
- 手機匯入建議照 cel v1 的 `mobile-import-settings.json`。

## 預覽

- **天空**：地平線附近暖色薄霧，上方青藍（遊戲裡的天空：地平線約 `#cca894`，上方約 `#5c94ad`）。
- `previews/` 要有：
  1. `unity_player_view.png`：跟 cel v1 同一個鏡頭；走道上要看得到一段熔岩瀑布牆、遠處一座有熔岩的火山錐。
  2. `compare_cel_v1_v2.png`：cel v1 和 v2 並排。
  3. 每種零件一張。

## 交貨

1. 成果放 `SceneKit/volcano_cel_v2/`。工單資料夾寫回 `generator_request.json`、`response.json`、`inspect.json`、`previews/`。
2. commit 訊息開頭寫 `scenegen 005:`，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
3. 推完執行下面兩個指令，兩個 SHA 一樣才算推上去了：
   - `git ls-remote origin claude/mobile-camera-mic-battle-game-v5nhfr`
   - `git log -1 --format=%H`

   回報完整的 SHA 和兩個指令的原始輸出。
