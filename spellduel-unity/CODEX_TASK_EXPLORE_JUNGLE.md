# Codex 任務：故事探索「翠蔭迷林」的場景（第 2 版：地形、景物障礙、地標）

使用者要求一種新關卡「故事探索」：一整張固定的超大迷宮，用衝刺在裡面移動，有巡邏的怪、營地、NPC、小任務、隱藏閃光點、3 個小 Boss 競技場（打贏才打開藤蔓門）和最終 Boss 王座。

使用者追加的要求：

> 場景都請 Codex 做一些新的（場景針對這種新方式重新製作一些新的景點與地圖：森林主題，可以有湖泊、營地、城寨、山洞、岩石地形……不同森林宮殿、各種不同植物、祭壇……等等都行）。場景如植物、城寨……等都是障礙物。

Claude 已經把地圖改成有**地形種類、地面種類、景物障礙、地標**（碰撞都做好了：牆、湖水、城寨木牆和每一個景物，玩家和怪物都走不過去）。現在畫面全是暫代的色塊（綠方塊、灰方塊、藍色平板、圓柱、圓球），請做成真的森林場景。

**分工照舊**：Codex 做模型、動畫、特效；不要改 `Explore.cs`、`Combat*.cs`、`SoloBattle.Explore.cs` 的邏輯與碰撞。新介面請加在新的美術檔（例如 `ExploreArt.cs`），寫在 `CODEX_PENDING_BRIEF.md` 最後的「Codex 實作紀錄」，Claude 再接線。完成後推到 `claude/mobile-camera-mic-battle-game-v5nhfr`；Android、iOS 都要編譯通過。預覽放 `Docs/pending_preview/explore_jungle/`（每個地標第一人稱一張、整張地圖俯視一張）。

## 地圖資料（唯讀）

- `JungleMaze.Build()`：41×41 格，每格 `ExploreMap.CellSize`＝3m（約 123m 見方）。`map.World(x,z)`＝格子中心的戰場座標，`map.Cell(p)` 反過來。
- `map.At(x,z)`：`Wall`（走不過去）／`Floor`／`Gate`（藤蔓門）。
- **牆的地形** `map.TerrainAt(x,z)`（`ExTerrain`）：`Thicket` 樹叢（一般的迷宮牆）、`Rock` 岩壁、`Water` 湖水、`Palisade` 城寨木牆、`Ruin` 古殿石牆、`Cave` 洞窟岩壁。
- **地面** `map.GroundAt(x,z)`（`ExGround`）：`Grass` 草地、`Dirt` 泥土（營地、城寨、岩谷）、`Shore` 湖岸、`Cave` 洞窟地面、`Stone` 古殿石板。
- **景物障礙** `map.props`（`ExProp`：`kind`、`pos`、`radius`＝碰撞半徑、`variant`）：`tree` 大樹、`stump` 樹樁、`boulder` 巨石、`mushroom` 巨菇、`tent` 帳篷、`crate` 木箱、`totem` 圖騰、`pillar` 石柱、`statue` 雕像、`campfire` 篝火、`altar` 祭壇。**外型的「實心部分」要和碰撞半徑差不多**（玩家撞到看得到的東西才停，不要看起來能過卻過不去）。
- **地標** `map.landmarks`（`ExLandmark`：`id`、`name`、`kind`、`rect` 格子範圍）：

| id | 名稱 | kind | 內容 |
|---|---|---|---|
| start | 林語營地 | start | 起點：長老的小營地（篝火、木箱、帳篷） |
| lake | 月影湖 | lake | 湖面（Water 格，走不過去）＋一圈湖岸 |
| camp0 | 藤蔓營地 | camp | 怪物營地（獵人的任務）：帳篷、圖騰、篝火 |
| rocks | 碎岩谷 | rocks | 岩壁圍起來的谷地，地上散落巨石 |
| fort | 藤牙寨 | fort | 敵人的城寨：木樁圍牆（Palisade），西、北兩個寨門，裡面是營地 |
| grove | 巨菇林 | grove | 巨大的發光菇 |
| cave | 回音洞窟 | cave | 整片洞窟：走道是洞窟通道、營地是洞窟大廳，最深處有森林之靈 |
| camp4 | 枯骨營地 | camp | 骨頭與圖騰的營地 |
| palace | 翠玉古殿 | palace | 森林宮殿的廢墟：石牆、石板地、石柱、雕像，最深處是王座與祭壇 |
| arena1～3 | 三個小 Boss 競技場 | arena | 四個角落有大樹 |

你可以直接呼叫 `JungleMaze.Build()` 做預覽；現在怎麼擺請看 `SoloBattle.Explore.cs`（`RefreshExploreCells`、`UpdateExploreProps`）。

## 要做的（新介面）

所有物件的 pivot 在**地面中心**；Claude 把它們放進一個跟著世界原點走的根物件底下（localPosition＝`map.World(x,z)` 或 `prop.pos`），只生成玩家附近約 24m 內的東西。

1. **牆** `GameObject ExploreArt.CreateWall(ExTerrain t, int x, int z, float cell)`：一格（3m×3m）大小，依地形：
   - `Thicket` 叢林樹叢（樹幹、闊葉、垂藤、蕨類，2.4～3m 高，用 (x,z) 決定變化）
   - `Rock` 岩壁（層狀岩、苔蘚、裂縫裡長出的草）
   - `Water` 湖面（平的水面、波光、睡蓮、湖邊的蘆葦；高度幾乎貼地）——看起來要明顯是「走不過去的水」
   - `Palisade` 城寨木牆（削尖的木樁、綁繩、掛著的獸骨和旗子）
   - `Ruin` 古殿石牆（長滿藤蔓的翠綠石磚、斷掉的拱門、碎石）
   - `Cave` 洞窟岩壁（深色岩石、鐘乳石、發光苔蘚、從頭上垂下的根；洞窟區可以有低低的岩頂，但走道上方要留空間）
   數量最多（同時 80～120 個）：共用材質、每個 ≤300 三角形、可以合批。
2. **地面** `GameObject ExploreArt.CreateGround(ExGround g, int x, int z, float cell)`（選做但很有幫助）：一格的地面貼片（草、泥土、湖岸沙、洞窟碎石、古殿石板），貼地、半透明邊緣，不要擋到真實地板上的預警圈。
3. **景物** `GameObject ExploreArt.CreateProp(ExProp p)`：上面 11 種，依 `variant` 做 2～3 種變化；大小照 `radius`。另外請多做幾種**植物**變化放進 `tree`／`mushroom` 的 variant（使用者要「各種不同植物」：棕櫚、榕樹氣根、巨大蕨類、食人花造型的灌木、發光藤……）。
4. **地標佈置** `GameObject ExploreArt.CreateLandmark(ExLandmark l, ExploreMap map)`：整個地標的裝飾（不能有碰撞、不能擋路，只能放在牆格上方、或貼地）：
   - 月影湖：湖心的倒影月光、漂浮的螢火蟲、湖邊的石燈
   - 藤牙寨：寨門的拱門（上面掛骷髏與藤蔓旗）、瞭望台（放在木牆格上）、火把
   - 碎岩谷：高聳的岩柱、從岩壁長出的扭曲樹
   - 回音洞窟：洞口的岩石拱門、發光水晶、水滴落下的特效
   - 巨菇林：飄浮的孢子光點
   - 翠玉古殿：入口的大石門框、殘破的翠綠屋頂（放在牆格上方）、藤蔓纏繞的石像
   - 營地：晾掛的獸皮、柵欄
   - 林語營地：長老的小屋、旗子
5. **藤蔓門** `GameObject ExploreArt.CreateGate(int arena, float cell)` ＋ `void ExploreArt.SetGateOpen(GameObject gate, bool open)`：關著時是糾纏的粗藤蔓（帶黑紫色腐化光），打開時縮回地面（約 1.5 秒）。
6. **競技場與王座** `GameObject ExploreArt.CreateArena(int n, float radius)`：1 苔甲守根者的巢穴（苔蘚、巨根）、2 夢孢司祭的祭壇（發光菇、孢子霧）、3 翠翼巡獵者的獵場（羽毛、掛著的獵物骨）、4 腐化樹王的王座（巨大的枯樹王座、黑紫腐化根脈，在古殿最深處）。地上要看得出範圍邊界。
7. **祭壇與古樹之心** `void ExploreArt.SetAltarGlow(GameObject altarProp, bool heartThere)`：`altar` 這個景物在 Boss 倒下後浮出發綠光的「古樹之心」，被拿走後光熄滅。
8. **月光草** `GameObject ExploreArt.CreateHerb()`、**閃光點** `GameObject ExploreArt.CreateSparkle(string kind)`（平常幾乎看不到，每 2～4 秒閃一下）＋ `void ExploreArt.SparkleFound(Vector3 atWorld, string kind)`（發現的特效）。
9. **NPC 外型** `void ExploreArt.DressNpc(CharacterRig rig, string npcId)`（elder 長老、hunter 獵人、lost 探險家、outpost 前哨站守衛、herbalist 草藥師、guardian 守林人、spirit 森林之靈）＋ `void ExploreArt.QuestMark(Transform npc, int state)`（0 無、1 ❗、2 ❓）。
10. **怪物發現玩家** `void ExploreArt.AlertMark(Transform monster, bool callsHelp)`：頭上的紅色「！」（叫同伴的再加往外擴散的聲波）。
11. **發現地標** `void ExploreArt.LandmarkFound(ExLandmark l)`（選做）：第一人稱畫面上方一行漂亮的地名橫幅（像開放世界遊戲進入新區域那樣）。

12. **岔路解謎（使用者追加）**：`map.seals`（封印：苔根封印、寨主之鎖、雙獵封印、翠玉封印，`cells` 是被封住的入口格）和 `map.switches`（`kind`：lever 機關、key 鑰匙、shard 翠玉碎片，放在岔路死路底）。
   - `GameObject ExploreArt.CreateSeal(ExSeal s, int x, int z, float cell)` ＋ `void ExploreArt.SetSealOpen(GameObject g, bool open)`：四種封印外觀不同（苔根盤結、掛著古銅大鎖的木柵、兩道藤蔓結、刻著三個凹槽的翠玉石門；凹槽依 `s.have` 亮起），解開時的動畫。現在是紫色方塊。
   - `GameObject ExploreArt.CreateSwitch(ExSwitch sw)` ＋ `void ExploreArt.SetSwitchDone(GameObject g)`：石雕機關（拉桿或可以踩的石盤，啟動後下沉發光）、古銅鑰匙、翠玉碎片（浮在空中慢慢轉、發綠光）。在死路底要遠遠就隱約看得到一點光，引導玩家走進岔路。

## 預算與注意

- **景物和牆都是障礙物**：碰撞已經由 Claude 用格子和半徑算好，你只要讓外型和碰撞對得上（不要加 Collider）。
- AR 裡畫面就是玩家的眼睛：**走道上方不要有東西**（樹冠、屋頂只能放在牆格上方），走道地面保持看得到，戰鬥時才看得清楚敵人和地上的預警。
- 中階手機要順：玩家附近約 24m 內的東西會被生成、離開就刪掉，所以 Create 要便宜（共用 mesh／材質，不要每次重算大量網格）。
- 衝刺時整個世界會平移：粒子請用 local space（或跟著父物件），不然會被甩在原地。
- 主題色：深綠、苔綠、樹皮棕、岩灰、湖藍；腐化的東西用黑紫；任務相關（月光草、祭壇、閃光點）用暖白或藍白光，讓它在綠色裡跳出來。

## 交付順序
**1 牆（六種地形）→ 3 景物 → 5 藤蔓門 → 4 地標佈置 → 6 競技場與王座 → 2 地面 → 12 → 7 → 8 → 9 → 10 → 11**。每做完一項就推，Claude 會接線。
