# Codex 任務：第二批特殊裝備的造型＋新材料圖示

使用者要求「多設計幾種新裝備配方與掉落材料」。玩法已經接好（`Crafting.cs`、`Blueprints.cs`），這張單只要美術。

## 1. 六件特殊裝備的造型（`CharacterRig.BlueprintArt.cs` 的 `MakeBlueprint`）

跟第一批（燒肉串串大劍、咕咕警報頭盔…）一樣：搶眼、kuso，配色用配方的 `main`／`accent`。

| id | 名稱 | 部位 | 外型（`look`） |
|---|---|---|---|
| bp_bonsai | 盆景森林頭盔 | 頭 | 頭上頂著一整座小盆景：歪脖子松、苔蘚、小石燈籠，下雨時會自己澆水 |
| bp_eelwhip | 電鰻跳繩鞭 | 武器 | 一條活的電鰻當鞭子，揮動時像跳繩甩圈，尾巴放電，偶爾回頭瞪主人 |
| bp_dumbbell | 啞鈴流星錘護符 | 飾品（腰） | 腰上用鐵鏈拴著一顆大啞鈴，走路晃來晃去，被打時自己甩出去 |
| bp_griddle | 熔岩烤盤胸甲 | 身體 | 胸前一塊滾燙鐵烤盤，一直煎著荷包蛋和香腸，背後岩板縫冒橘光 |
| bp_rocketskate | 火箭溜冰鞋 | 腳 | 鞋底溜冰輪，鞋跟兩支小火箭，衝刺時噴火、停下冒煙 |
| bp_jadesprite | 翠玉樹精耳環 | 飾品（頭旁） | 耳邊坐著翠玉小樹精，會晃腳、打哈欠，戰鬥時吹口哨加油 |

**重要**：做好之後把 id 加進 `SkillIcons.Blueprints.cs` 的 `BlueprintArtIds`。現在沒加進去的會照一般裝備畫（`HasBlueprintArt` 擋著），不會隱形。配方卷軸的圖示也一樣在那裡畫。

## 2. 六種新材料的圖示（`SkillIcons`，跟其他材料一樣的風格）

| id | 名稱 | 誰掉 | 方向 |
|---|---|---|---|
| mat_root_ring | 古根年輪 | 古根撼地獸 | 一片樹幹橫切面，年輪發黃綠光 |
| mat_whip_fin | 潮鞭鰭骨 | 潮鞭鰻 | 扇形鰭骨，半透明青色 |
| mat_shackle | 斷鐐鐵環 | 鏈錘鬥奴 | 斷掉的鐵手鐐，一節鏈子 |
| mat_rift_shard | 裂岩熔片 | 裂岩甲蜥 | 岩片，裂縫透橘紅光 |
| mat_swift_mane | 疾風鬃毛 | 疾風稀有怪 | 一撮被風吹起的鬃毛，旁邊風線 |
| mat_jade_chip | 翠玉碎屑 | 故事探索的怪 | 幾顆翠綠玉石碎片 |

## 規格

- 程式生成網格，共用既有材質，不新增圖片檔；網格快取。每件裝備 ≤ 1500 三角形。
- 不改 `Crafting.cs`、`Blueprints.cs`、`Combat*`、`SoloBattle*` 的玩法與數值。
- 原生預覽：六件裝備穿在玩家身上（正面、側面）、六個材料圖示一張。放 `Docs/pending_preview/recipes_2/`。
- Android、iOS 都要編譯通過，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`，實作紀錄寫在 `CODEX_PENDING_BRIEF.md` 最後。
