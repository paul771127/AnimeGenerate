# Codex 任務：月影古堡（測試副本）的 2D 美術——8 方向 Q 版敵人＋2D 化的城堡場景

## 使用者的要求

> 幫我改一版測試用：做一關副本，場景仍是 3D，但敵人變成用 8 方向 2D 圖片，製作日式 Q 版的敵人（各個動作都做 8 方向），副本敵人先做 8 種，讓我進去試試。副本場景是歐式城堡內部。
>
> 副本的 3D 場景也試著都改用 2D 來表現。2D 部分讓 Codex 做。

範圍：先只做測試副本 `castle_test`（月影古堡），也就是 `dungeons.txt` 裡寫了 `style = castle` 的副本。其他副本不動。

## Claude 已經做好的部分（遊戲端）

### 1. 測試副本
- 位置：`Resources/dungeons.txt` 最後的 `[dungeon castle_test] 🧪 月影古堡（2D 敵人測試）`。
- 等級 5，1～4 人。
- 路線：玄關大廳 → 肖像畫廊 → 禁書圖書館 → 寶物庫 → 王座之間（首領）。
- `style = castle`：`DungeonDef.style`，地圖的 `ExploreMap.theme` 會是 `"castle"`（`ExploreDungeon.cs`）。

### 2. 8 種城堡敵人
定義在 `Runtime/Combat.SpriteMonsters.cs`，變種 id 201～208。

| id | 圖檔（sheet） | 名字 | 沿用的 AI（baseKind） | 一格的高度 |
|---|---|---|---|---|
| 201 | castle_skeleton | 骷髏劍兵 | 0 近戰 | 1.6 m |
| 202 | castle_knight | 空殼騎士 | 2 坦克（雙手高舉砸下） | 2.0 m |
| 203 | castle_archer | 骷髏弓手 | 1 遠程射擊 | 1.6 m |
| 204 | castle_ghost | 提燈幽靈 | 6 術士（會瞬移） | 1.6 m |
| 205 | castle_bat | 吸血小蝙蝠 | 14 俯衝飛鳥（飛在空中） | 1.4 m |
| 206 | castle_pumpkin | 南瓜炸彈 | 3 自爆 | 1.4 m |
| 207 | castle_gargoyle | 小石像鬼 | 4 岩石投擲（舉過頭丟） | 1.6 m |
| 208 | castle_mage | 見習黑魔導 | 5 狙擊（法杖蓄能） | 1.6 m |

- 城堡副本裡生出來的小怪，會自動換成這 8 種之一：遠程換遠程，近戰換近戰（`Battle.CastleRemap`）。
- AI、碰撞、出招沿用 baseKind，只換名字和外觀。
- 合作模式下，隊友收到的是變種 id，畫面一樣。

### 3. 顯示元件：`Runtime/SpriteEnemyView.cs`
- 一片永遠正對鏡頭（只轉水平方向）的四邊形，材質是 `Sprites/Default`。
- 依「敵人面向」和「鏡頭方向」的夾角選 8 方向之一，再播對應動作的影格。
- 接在 `SoloBattle.VariantArt.cs` 的 `DressSprite`，原本的 3D 骨架藏起來。
- 預備、出手、受擊、倒下、移動速度，跟其他敵人走同一條管線。
- 受擊閃紅、倒下淡出由程式做，圖不用畫閃白。

### 4. 暫用圖
- 位置：`Resources/SpriteEnemies/castle_*.png`。
- 用 `Tools/sprite_enemies/gen_castle_sprites.py` 程式拼的，很粗糙，只是讓測試副本能進去。
- 請整套換掉。

## 請 Codex 做的

### A. 8 種敵人的 2D 圖——已由 Claude 完成，不用做

使用者後來說「你自己做 2D 圖像也可以，但要精緻可愛點」，所以敵人圖由 Claude 用 `Tools/sprite_enemies/gen_castle_sprites.py` 做好了：
- 立體零件，從 8 個方向畫成 2D。
- 賽璐璐陰影、內線、粗外框、動畫大眼。
- 圖檔已經在 `Resources/SpriteEnemies/`，預覽在 `Docs/castle_2d_preview/`。

格式以 `SpriteEnemyView.cs` 為準：2048×2048，每格 128；`slot = (動作第一張 + 第幾張) × 8 + 方向`；動作 idle 4、walk 6、windup 2、attack 3、hurt 2、die 4；方向 0 正面、2 面向畫面右邊、4 背面；腳底在格子底邊往上 6%。

### B. 2D 化的城堡場景（只有 `map.theme == "castle"`）

**使用者想要的效果**：副本場景也用 2D 表現。地圖的格子、走道、牆的位置照舊（3D 空間、玩家照樣走），但看到的東西改成 2D 手繪風格，類似 HD-2D。

- **牆**：一格一片或幾片平面，貼上手繪的城堡牆面圖。
  - 石磚牆、拱窗加彩繪玻璃、紅金旗幟、壁掛火把、肖像畫、書架。
  - 依地點換主題：大廳、畫廊、圖書館、寶物庫、王座廳。
- **地面**：手繪貼圖。大理石棋盤格，沿著走道鋪紅地毯；區域邊緣有金色的框。
- **景物**：跟敵人一樣是永遠正對鏡頭的 2D 圖片。
  - 盔甲擺設、燭台、水晶吊燈（掛高處）、花瓶、王座、寶箱、書堆。
  - 不用 8 方向，單張就好。要動的（燭火、吊燈搖晃）可以做 2～4 張循環。
- **遠景**：室內，不要看到天空。
  - 高聳的石牆、柱子、拱頂的 2D 剪影圖，色調是昏暗的暖色。
  - 請一併確認地平線以下不會透出現實背景，參考 `ExploreArt.Atmosphere.cs` 的 `GroundBowl`。
- **門**：區域的閘門也換成 2D 圖。例如鐵柵門，打開的動畫可以是幾張圖。

**要改的地方**

`"castle"` 目前沒有專屬外觀，會被當成峽谷主題（4）。所以 Codex 要接上 castle 的分支：
- `ExploreArt.Dungeons.cs`：`DungeonThemeAt`、`DungeonBaseGround`、`CreateDungeonWall`、`DungeonWallBody`、`DungeonProp`、`DungeonWallGlow`
- `ExploreArt.DungeonSurfaces.cs`：`DressDungeonPath`、`CreateDungeonGate`、`CreateDungeonBackdrop` 開頭的主題對照
- `ExploreArt.Atmosphere.cs`：`HorizonColors`、`CreateBackdrop`
- 地板 shader：`Resources/Shaders/DungeonExploreFloor.shader` 的 `_Theme`，可以新增一個值給城堡

建議 castle 用新的主題編號 6。

**注意**
- 只改外觀，不要動地圖資料、區域、門的邏輯，也不要動戰鬥和連線。
- 2D 圖片的資源放在 `Assets/SpellDuel/Resources/CastleScene/`（或你覺得合適的資料夾）。
- 要考慮手機效能：
  - 材質、網格盡量共用。
  - 圖集不要超過 2048。
  - 景物數量照現有的翠蔭迷林。
- 預覽圖放 `Docs/castle_2d_preview/`。

## 交貨
- commit 寫清楚做了哪些敵人、哪些場景元素，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
- 推完跟使用者說一聲。Claude 會檢查方向、動作是否對得上，以及編譯有沒有問題。
