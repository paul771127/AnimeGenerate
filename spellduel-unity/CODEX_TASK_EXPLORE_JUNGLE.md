# Codex 任務：故事探索「翠蔭迷林」的叢林迷宮美術（使用者要求）

使用者要求一種新關卡「故事探索」：一整張固定的超大迷宮（第一張是叢林），用衝刺在裡面移動，有巡邏的怪、營地、NPC、小任務、隱藏閃光點、3 個小 Boss 競技場（打贏才打開藤蔓門）和最終 Boss 王座。**邏輯和畫面接線 Claude 已經做好，現在全部是暫代外觀（綠色方塊、小球）**，請把它做成真的叢林。

**分工照舊**：Codex 做模型、動畫、特效；不要改 `Explore.cs`、`Combat*.cs`、`SoloBattle.Explore.cs` 的邏輯。下面的新介面請加在新的美術檔（例如 `ExploreArt.cs`），寫在 `CODEX_PENDING_BRIEF.md` 最後的「Codex 實作紀錄」，Claude 再接線。完成後推到 `claude/mobile-camera-mic-battle-game-v5nhfr`；Android、iOS 都要編譯通過。預覽放 `Docs/pending_preview/explore_jungle/`（第一人稱走在走道上、營地、競技場、王座、小地圖整張俯視各一張）。

## 地圖資料（唯讀，給你參考）

- `JungleMaze.Build()` 產生固定地圖：41×41 格，每格 `ExploreMap.CellSize`＝3m；`ExCell.Wall`／`Floor`／`Gate`。座標換算 `map.World(x,z)`（格子中心的戰場座標）、`map.Cell(p)`。
- 四個區域：西南（起點）→ 東南 → 東北 → 西北（王座）。房間：起點、5 個營地、3 個競技場（`map.arenas`，n＝1～3）、王座（n＝4，`map.altar` 是王座後面的祭壇）。
- NPC：`map.npcs`（id：elder 林語長老、hunter 獵人巴克、lost 迷路的探險家、outpost 前哨站守衛、herbalist 草藥師莉亞、guardian 守林人、spirit 森林之靈）。道具：月光草（`map.items`）。閃光點：`map.sparkles`（chest 寶藏、rare 沉睡的古根、spirit 發光的樹洞、beast 受傷的小獸、passage 藤牆密道）。
- 你可以直接呼叫 `JungleMaze.Build()` 做預覽；畫面怎麼擺請看 `SoloBattle.Explore.cs`（`RefreshExploreCells`、`UpdateExploreProps`）。

## 要做的（新介面）

所有物件的 pivot 在**地面中心**、尺寸以 1 格（3m×3m）為準；Claude 會把它們放進一個跟著世界原點走的根物件底下（localPosition＝`map.World(x,z)`）。

1. **牆（叢林樹叢）** `GameObject ExploreArt.CreateWall(int x, int z, float cell)`：一格大小、高 2.4～3m 的密林（樹幹、闊葉、垂藤、蕨類），用 (x,z) 決定變化（每次一樣）。玩家只看得到靠走道的那一面，所以只畫外側就好。**這是數量最多的東西**：同時會有 80～120 個，請共用材質、低面數（每個 ≤300 三角形）、可以合批。
2. **藤蔓門** `GameObject ExploreArt.CreateGate(int arena, float cell)` ＋ `void ExploreArt.SetGateOpen(GameObject gate, bool open)`：關著時是糾纏的粗藤蔓牆（帶一點紫黑色的腐化光，看得出是被封住的），打開時藤蔓往兩側縮回地面（約 1.5 秒動畫）。現在打開時 Claude 直接刪掉物件，有動畫後改呼叫 SetGateOpen。
3. **營地** `GameObject ExploreArt.CreateCamp(int camp, float radius)`：營地中央的佈置（簡陋的帳篷、圖騰柱、篝火、骨頭與木樁圍欄），半徑約 6m，不要擋住中間的戰鬥空間。營地中心＝同一個 `camp` 編號的怪的平均位置。
4. **競技場** `GameObject ExploreArt.CreateArena(int n, float radius)`：小 Boss 的巢穴／祭壇／獵場（1 苔甲守根者的巢穴：苔蘚與巨根；2 夢孢司祭的祭壇：發光菇與孢子霧；3 翠翼巡獵者的獵場：羽毛與掛著的獵物骨），以及 n＝4 腐化樹王的王座（巨大的枯樹王座、黑紫腐化根脈）。地上要看得出範圍邊界（半徑＝`arena.radius`）。
5. **祭壇與古樹之心** `GameObject ExploreArt.CreateAltar()` ＋ `void ExploreArt.SetAltarGlow(GameObject altar, bool heartThere)`：王座後面的石祭壇，Boss 倒下後上面浮著發綠光的「古樹之心」，被拿走後光熄滅。
6. **月光草** `GameObject ExploreArt.CreateHerb()`：會微微發藍白光的草（夜光、會呼吸般亮暗）。
7. **閃光點** `GameObject ExploreArt.CreateSparkle(string kind)`：使用者要求是「隱藏的」——平常幾乎看不到，**偶爾閃一下**（每 2～4 秒一次的小星光），玩家要仔細看才會發現。依 kind 可以有一點不同（土裡的金光、樹洞的綠光、草叢裡的小獸眼睛反光、藤牆的縫隙光）。點到時 Claude 會呼叫 `ExploreArt.SparkleFound(Vector3 atWorld, string kind)`，請做一個發現的特效。
8. **NPC 外型** `void ExploreArt.DressNpc(CharacterRig rig, string npcId)`：在 `CharacterRig.Create(look)` 之後換成這幾位的樣子（長老：長鬍子、樹枝法杖、葉子披肩；獵人巴克：獸皮、長弓；探險家：背包、地圖、帽子；前哨站守衛：簡單皮甲、長矛；草藥師莉亞：草藥籃、圍裙；守林人：樹皮盔甲；森林之靈：半透明發光的綠色精靈）。頭上的任務標記另外做 `void ExploreArt.QuestMark(Transform npc, int state)`（0 無、1 ❗ 有任務、2 ❓ 可回報），Claude 每幀呼叫。
9. **怪物發現玩家** `void ExploreArt.AlertMark(Transform monster, bool callsHelp)`：頭上跳出紅色「！」（叫同伴的那隻再加一圈往外擴散的聲波）。
10. **起點** `GameObject ExploreArt.CreateStartCamp()`：長老所在的小營地（篝火、幾個木箱、旗子）。

## 預算與注意

- AR 裡畫面就是玩家的眼睛：牆不要做成整片不透明的高牆擋住全部視線也可以（叢林本來就該擋），但**走道上方不要有東西**（不要讓樹冠蓋住頭頂），走道地面保持乾淨看得到真實地板，戰鬥時才看得清楚敵人和地上的預警。
- 中階手機要順：玩家附近約 24m 內的東西會被生成、離開就刪掉，所以生成要便宜（不要在 Create 裡做大量 mesh 運算；共用 mesh／材質）。
- 衝刺時整個世界會平移：請不要用世界座標的粒子／線條常駐（或用 local space），不然會被甩在原地。
- 主題色：深綠、苔綠、樹皮棕；腐化的東西用黑紫色；任務相關的東西（月光草、祭壇、閃光點）用暖白或藍白光，讓它在綠色裡跳出來。

## 交付順序
**1 牆 → 2 藤蔓門 → 7 閃光點 → 6 月光草 → 4 競技場與王座 → 5 祭壇 → 3 營地 → 8 NPC → 9 → 10**。每做完一項就推，Claude 會接線。
