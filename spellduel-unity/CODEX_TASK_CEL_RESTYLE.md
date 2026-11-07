# Codex 任務：熔岩主題的 2D 美術改成新的賽璐璐風格

## 使用者的要求

> 全面換成原神風（賽璐璐風格的關卡風格）。

場景生成器已經交了新的 3D 岩壁，遊戲已經換上：
- 零件：`Assets/SpellDuel/Resources/SceneKit/volcano_cel_v1/`
- 預覽：`scenegen/jobs/codex-style/volcano-cel-v1/previews/`
  - 先看 `unity_player_view.png`、`unity_clearing_view.png`、`hero_view.png`
- 材質原圖：`scenegen/jobs/codex-style/volcano-cel-v1/source/anime-volcano-source-atlas.png`

現在**路中間的 2D 障礙物**和**一般關卡的 2D 場景**還是舊的紅褐色，跟新的岩壁色調差很多。
請照新風格**重畫，直接覆蓋同樣的檔名**。尺寸、格子、比例規則全部不變，遊戲程式不用改。

## 新風格（跟 3D 岩壁一致）

**畫法**
- 乾淨的日式動畫冒險風景：大塊的手繪平面、柔和的漸層、明暗兩階的賽璐璐上色。
- 細節少而大，不要照片質感、細碎雜訊或大理石紋。
- 光從左上方來。受光面偏暖（淡灰褐、一點赭黃），背光面偏冷（藍灰、藍綠）。
- 輪廓清楚，可以有一點亮邊。

**顏色**（從 3D 岩壁的貼圖取樣）

| 用途 | 顏色 |
|---|---|
| 岩石暗部 | `#2c3842` |
| 岩石中間調 | `#454b53` |
| 岩石亮部 | `#6f6f6b` |
| 岩石平均 | `#474f55` |
| 火山灰地面 | `#5c585f`（帶一點紫灰） |
| 凝固熔岩地殼 | `#384353`（深藍炭色） |
| 天空 | 地平線附近是帶暖色的薄霧，約 `#ccA894`；上方是清透的青藍色，約 `#5c94ad` |

**熔岩**
- 熔岩、裂縫、噴發仍然是橘紅色發光，但面積要少：細的、彎曲的線，或小池子。
- 跟冷色的岩石形成對比。

**不要做**
- 不要畫出原神裡特定的地點、建築、標誌、角色、怪物。
- 不要描原神的截圖，只學通用的畫風。

## 要重畫的圖

### A. 第一大關的障礙物、陷阱、脫出點

位置：`Assets/SpellDuel/Resources/StageArt/Obstacles/`，共 18 張，原檔名覆蓋。

規格照 `CODEX_TASK_OBSTACLE_ART.md`，**以下比例一定要跟舊圖一樣**（遊戲照這些數字擺放）：

| 圖 | 規則 |
|---|---|
| `volcano_jump_{0,1,2}`（2048×512） | 地面在圖高 4%；最高點不超過 45% |
| `volcano_duck_{0,1,2}`（2048×1024） | 地面在圖高 4%；**橫樑底邊在 45%**（從底下往上量）；底下透空 |
| `volcano_side_{0,1,2}`（1024×1024） | 地面在 4%；右邊是自然收尾 |
| `volcano_vent_idle` / `volcano_vent_warn`（512） | 俯視的噴口 |
| `volcano_vent_erupt`（1024，4 格橫排） | 噴發的岩漿柱 |
| `volcano_rock_fall` / `volcano_rock_rubble`（512） | 落石、落地後的碎石 |
| `exit_circle`（1024） | 俯視的藍色魔法陣 |
| `exit_pillar`（256×1024） | 光柱 |

- 岩石的顏色、筆觸要跟 3D 岩壁（`volcano_cel_v1`）像同一套。
- 噴口、碎石的地面色用火山灰 `#5c585f` 和凝固熔岩地殼 `#384353`。

### B. 一般關卡（10m×10m）的熔岩場景

位置：`Assets/SpellDuel/Resources/StageArt/`，共 15 張，原檔名覆蓋。

規格照 `CODEX_TASK_STAGE_ART.md`：
- 檔名 `volcano_{0,1,2}_{far,mid,ground,decals,props}.png`
- 三種變化：熔岩谷、火山口邊緣、廢棄礦坑。
- 尺寸、無縫規則照原本的規格。

**地平線**：原規格寫圖高往上 14%，舊圖實際畫在約 6%，遊戲照 6% 擺放。**新圖請畫在約 6%**，不然地平線會跑掉。

- 天空改成上面的新天空色。
- 遠山是冷色的岩塊和火山錐，越遠越淡、越偏藍；火山口可以有一點橘紅的光和煙。
- 地面 `ground` 跟 `SceneKit/volcano_cel_v1/cel_ground_ash.png` 協調。
- 廢棄礦坑偏暗，但也是冷色調，礦燈是暖色的點綴。

## 交貨

1. 覆蓋上面 33 張圖。`.meta` 保留原本的不要動，只換 PNG。
2. 推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
   - commit 訊息不要加 `[skip ci]`。
   - 推完執行 `git log origin/claude/mobile-camera-mic-battle-game-v5nhfr -1` 確認，回報完整的 commit SHA。
3. 預覽：
   - `Docs/stage_art_preview/obstacles/` 和 `Docs/stage_art_preview/volcano/` 換成新圖的總表。
   - 另外一張 `Docs/stage_art_preview/cel_match.png`：新的 2D 障礙物放在 `scenegen/jobs/codex-style/volcano-cel-v1/previews/unity_player_view.png` 的走道上，確認跟 3D 岩壁是同一套美術。
