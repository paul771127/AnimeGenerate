# 生成器工單 006：熔岩脈改成不規則（cel v3）

- **發單**：SpellDuel 遊戲端（雲端 Claude Code session）
- **執行**：本機的場景生成器 session（`cel_kit_control.py`）
- **工單資料夾**：`scenegen/jobs/spellduel-cloud/006-volcano-cel-veins/`
- **成果放到**：`spellduel-unity/Assets/SpellDuel/Resources/SceneKit/volcano_cel_v3/`
  - 新資料夾。不要動 `volcano_cel_v2/` 和 005 的工單資料夾。
- **前一版**：工單 005（`volcano_cel_v2`）。
  - 零件、網格、UV、尺寸、原點、面數、熔岩瀑布的 `lava_flow` 子物件和捲動規則全部沿用。
  - **這次只重畫熔岩脈。**

## 使用者的要求

> 熔岩脈改成不規則吧。

005 的熔岩脈大多是**直直往下、間隔差不多的平行線**，近看像一根根燈管，很規律、不自然（見 005 的 `previews/compare_cel_v1_v2.png` 右半邊、`hero_view.png`）。

## 怎麼改

### 形狀

- **跟著岩石走**：沿著岩塊之間的縫、岩層的走向。
  - 可以斜的、橫的、彎的，不要都是垂直線。
- **會分岔**：主脈分出 1～3 條細支脈；有的支脈很短就斷掉。
- **粗細有變化**：同一條脈有粗有細。交會處、轉折處粗一點、亮一點，像熔岩在那裡積著。
- **長短不一**：有長的主脈，也有很多短的小裂口（10～40cm）。
- **不要等間距、不要平行排列**：相鄰兩條的方向、長度、間距都要不一樣。

### 分布

- **每塊牆不一樣**：
  - 有的幾乎沒有（只有 1～2 個小裂口）。
  - 有的有一個明顯的大裂縫網。
  - 同一種零件的不同版本差異要明顯。
- 仍然是**下半部多、上半部少**。
- 轉角、牆尾、岩門框、岩洞、遠景火山錐都照同樣的規則。
  - 火山錐的熔岩河可以維持 005 的流向，但要有彎曲和分支，不要兩條平行線。
- 發光遮罩佔表面約 **5～9%**（005 大約是這個量，總量不用增加，只是分布要不規則）。

### 畫法、顏色（照 005）

- 賽璐璐兩階：
  - 中心亮黃 `#ffd36a`。
  - 外圈橘 `#ff8a2a`～紅 `#d8452a`。
  - 邊緣可以有一點暗紅的焦痕。
- 岩石照 cel v1／005 的冷色，不要變。
- 發光遮罩跟顏色圖用同一組 UV，脈的位置要一致。

### 不要動的

- 熔岩瀑布 `lava_flow` 的 UV 帶（`kit.json.lavaFlow.uRange`）和流向。
- 牆腳不要另外烘暖橘色（遊戲已經加了牆腳反光）。
- 網格、面數、原點、檔名（零件檔名照 005；貼圖改成 `volcano_cel_v3_*.png`）。

## 自我檢查（寫進 `inspect.json`）

1. 熔岩脈方向的分布：
   - 統計所有脈段的角度，**垂直（±15°）的比例不要超過 40%**。
   - 寫出實際比例。
2. 每個零件的發光比例。
3. 面數和網格跟 005 一致（應該是同一份網格）。

## 預覽

`previews/` 要有：
1. `unity_player_view.png`：跟 005 同一個鏡頭。
2. `compare_v2_v3.png`：005 和 006 並排，同一個鏡頭。
3. `closeup_veins.png`：近距離看一面牆。
4. 每種零件一張。

## 交貨

1. 成果放 `SceneKit/volcano_cel_v3/`：
   - FBX（可以直接複製 005 的）
   - 新貼圖
   - `kit.json`（`version` 寫 6，貼圖名稱改成 v3）
2. 工單資料夾寫回 `generator_request.json`、`response.json`、`inspect.json`、`previews/`。
3. commit 訊息開頭寫 `scenegen 006:`，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
4. 推完執行下面兩個指令，兩個 SHA 一樣才算推上去了：
   - `git ls-remote origin claude/mobile-camera-mic-battle-game-v5nhfr`
   - `git log -1 --format=%H`

   回報完整的 SHA 和兩個指令的原始輸出。
