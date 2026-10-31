# 翠蔭迷林：靜態場景交付

只新增／調整 ExploreArt*.cs、探索專用 shader、原生預覽與文件。Explore.cs、Combat*.cs、SoloBattle.Explore.cs 的玩法／碰撞未由本批修改。動畫及影片由另一個 session 製作；本批沒有 Update 動畫、計時器或播放影片。

## 需求對照

| 項目 | 已提供的靜態內容 | 預覽 |
|---|---|---|
| 1 牆 | 樹叢、岩壁、水面、木牆、古殿石牆、洞窟，四個快取變體 | walls_overview.jpg |
| 3 景物 | 11 種；樹 5 型、其他各 3 型，共 35 種 | props_overview.jpg |
| 5 藤蔓門 | 獨立藤束與開／關狀態 | 地標實景與 keyframe 匯出器 |
| 4 地標 | 12 處地標第一人稱、全圖俯視 | landmarks_overview.jpg、explore_map_top.png |
| 6 競技場 | 巨根巢、菇壇、獵場、腐化王座；邊界與遠端佈景 | arenas_overview.jpg |
| 2 地面 | 草、泥、岸、洞窟、石板的低透明度地貼 | 地標實景 |
| 12 岔路解謎 | 四封印、凹槽、壓板、鑰匙、碎片 | explore_seal*_closed／ready.png |
| 7～8 任務物件 | 古樹之心、月光草、微弱閃光點 | animation_keyframes.jpg |
| 9 NPC | 長老、獵人、探險家、守衛、草藥師、守林人、森林之靈 | npc_overview.jpg |
| 10～11 提示 | 驚嘆號、問號、求援標記、地名橫幅 | animation_keyframes.jpg |

Claude 已於 a5a4627 將整套美術及天空接入正式探索模式。本目錄是 Unity 原生模型預覽，仍不可當成已完成手機 AR 實機測試的證明。

## 放置契約

- CreateWall／CreateGround：地面中心 root，cell 決定尺寸；父物件下 localPosition = map.World(x,z)，不要再套舊色塊的高度偏移／縮放。
- CreateProp：root 在碰撞圓心，水平 radius 已縮放，樹、巨菇、圖騰等另有垂直比例以維持可辨識高度；localPosition = prop.pos。樹基、石塊、石壇／樹樁等實心輪廓靠近碰撞範圍，不加 Collider。
- CreateLandmark：回傳 root.localPosition 已是地標 rect 中心的 map.World。SetParent(exRoot,false) 保留位置；內部裝飾用相對座標，僅挑選真正牆格，不跨走道架屋頂。
- CreateArena：放在 arena.center。透明邊界等於傳入 radius；RimSet 使用本張固定 JungleMaze 的牆格位置；第二競技場避開北門偏一格，三角形水平範圍限於該牆格。原生驗證會檢查實際落點仍是 Wall。
- SetGateOpen／SetSealOpen：只切 Closed 的 active 狀態。封印進度每改變一次也呼叫 SetSealOpen(g,s.open)，會讀 s.have 更新凹槽，沒有反寫遊戲資料。
- SetSwitchDone：壓板立即下沉，或隱藏鑰匙／碎片；SetAltarGlow：立即顯示／隱藏 AncientHeart。
- DressNpc 可重複呼叫與換型，配件掛既有骨架；spirit 只隱藏舊 Renderer，切回其他身分時還原。
- QuestMark 0 隱藏，1 驚嘆號，2 問號。AlertMark 顯示靜態圖示；動畫端負責結束時隱藏 AlertMark。
- LandmarkFound(l) 顯示靜態框與地名，LandmarkFound(null) 清除；退出探索時由呼叫端清除。SparkleFound 依單保留空函式。

## 動畫掛點

| 模型 | 子物件 |
|---|---|
| 牆 | WallBody、LooseVine_*、WarBanner、BoneCharm |
| 藤蔓門 | Closed/Vine_0～5 |
| 封印 | Closed/RootKnot_*、DoorLeft／DoorRight、BronzeLock、StoneDoor、Socket_0～2/Lit |
| 機關與拾取物 | PressurePlate/Rune、BronzeKey、JadeShard |
| 祭壇、草與火 | AncientHeart、MoonLeaves、Fire |
| 地標 | MoonReflection_*、Fireflies_*、Banner_*、Torch_*、WaterDrops_*、Spores_*、WallScenery_* |
| 森林之靈 | ExploreNpcDress/Explore Forest spirit/SpiritBody、OrbitLeaf_* |
| 提示 | QuestMark/Exclaim、QuestMark/Question、AlertMark/Glyph、CallerAccent |
| 地名 | RibbonFrame；地名由 GUI 另外畫，靜態圖無字 |

## 原生圖片

- 動畫用 16 張：`Docs/explore_fx/keyframes/`，960×544 PNG。純黑或 #00FF00 背景，沒有地板、天空、霧、粒子、文字。
- 選單 `SpellDuel/Explore/Export 16 animation keyframes` 可重出首尾圖。
- 選單 `SpellDuel/Explore/Export jungle scene and keyframes` 另輸出每地標第一人稱、全圖俯視、景物／牆／競技場／封印狀態。這些輸出在 snapshots，經檢查後收錄此目錄。
- 原生模型圖不是生成式示意圖。遊戲不載入本目錄圖片。

## 性能與驗收

網格依類型與有限變體鍵共用；材質共用。Factory 不增加 Collider。牆驗證逐一斷言三角形 ≤300、頂點限制在 3m 格子內。狀態可反覆切換，重新生成後共用既有 mesh／material。全圖只為預覽，遊戲仍由既有程式限制 24m 附近生成。

最新原生 Unity run 41159619094（73854ee）成功，輸出 95 張場景 PNG 與 16 張靜態首尾圖。24 組牆變體通過 ≤300 三角形、格子邊界與無 Collider 檢查；封印切換與 mesh／material 快取檢查通過。16 張圖通過 960×544、四角純黑／綠幕與四周留白檢查。另通過 roundedLakeCoverage、凹形湖岸平滑檢查與 4m 世界座標邊界檢查；湖心水格中心都有覆蓋。詳見 native_ci.json、explore_*_audit.json、keyframe_validation.json。

已目視複查新葉片群、加高樹木、三類岩石、帳篷與角色等比例圖、水材質／岸邊、篝火及 60m 視距下的天空／雲／遠林。本次道路與湖岸 run 41159619031（73854ee）的 Android、iOS 匯出與 IPA 編譯均成功；platform_ci.json 記錄本次版本的證據。AR 實機視野與動畫影片接線仍需整合端驗收。

## 2026-10-31 造型重修

- 樹叢、榕樹與發光藤樹取消大塊實心葉團，改程序葉片群：4 張 256×256 Texture2D 在建立時生成葉片尖端、深邊、葉脈與色差，共用並產生 mipmap，不新增圖片資源檔。CanopyLeaves 是獨立子物件。棕櫚／蕨類使用彎曲葉軸與細小葉片。
- Thicket 垂直比例增加 40%，獨立 tree 最小垂直比例改為 1.25，水平碰撞範圍不變。獨立樹五種植物，各有依座標決定的有限快取輪廓。
- 岩壁使用不規則九邊截面與收底斜頂；獨立石頭分矮圓、裂縫雙岩、層疊三型，各有四個快取種子。
- 帳篷檐高 1.70m、屋脊 2.25m，入口寬 1.12×radius、入口高約 1.65m。現有 radius 0.9～1m 時高度不再跟著縮小。仍保持原碰撞圓範圍，不改玩法或碰撞數值。
- 湖水改專用靜態水材質，依視角顯示深淺與天空反射色、細小高光；移除白色直線，增加細弧波紋與岸邊 Shore_* 過渡／蘆葦。沒有讀取時間或製作水流動畫。
- 篝火改不規則石圈、木柴切面、炭塊與多層彎曲火舌；Fire 掛點與 fx_D3_fire.png 檔名不變，動畫 session 請使用更新的靜態圖。
- 新圖：explore_tree_variation_lineup.png、explore_thicket_variation_lineup.png、explore_tent_scale.png、refinement_before_after.jpg、water_fire_before_after.jpg。帳篷對照使用真正的 MinionRig 9 與 CharacterRig 劍士，沒有放大／縮小比較角色。前後單件總覽各自取景，只比較輪廓；實際比例請看 explore_tent_scale.png。

### 天空整合（a5a4627 已接線）

進入探索時呼叫一次 `ExploreArt.CreateBackdrop(map)`，以 `SetParent(exRoot,false)` 保留已設定的地圖中心 localPosition；退出探索時跟世界根物件一同移除。不要放進 24m 格子串流。子物件包含 SkyDome、Sun、Cloud_*、DistantForest_*。上述呼叫已由 Claude 接入 SoloBattle.Explore.cs；本批美術修正沒有修改該檔案。

遠景 shader 將場地外的天空／雲／林帶沿原視線顯示在遠平面內；不寫深度、不投影，保持在近處敵人後方。相容目前遊戲相機 60m 遠裁切，不需更改 GameRoot。原生第一人稱圖使用同樣 60m 視距，俯視圖另加長視距並隱藏天空。新 shader 都在 Resources/Shaders，避免手機建置剔除。

### 整合後湖面完整性

CreateLandmark(lake) 的 LakeSurface 依實際 Water 格建立完整共用水面；即使遊戲只串流岸邊水格，湖心仍完整覆蓋。水面寫入深度，避免與串流格重疊產生色差。原生預覽按相同的邊緣格規則建立場景，並斷言水面覆蓋所有水格。

六項前後比較見 `refinement_all_before_after.jpg`；水面及篝火近景見 `water_fire_before_after.jpg`。

### 2026-10-31 地面與草皮細修

CreateGround 取消固定七片大草葉、凸起多角石塊與寬方形淡出，改為低對比的細草／苔土、泥土、岸沙、洞窟碎石與磨損石板。紋理完全由程式建立：草皮 512×512，其餘四種 256×256，重建時共用，含 mipmap 約 2.67 MiB；trilinear 與 4x anisotropic 控制遠處閃爍。

每格僅兩個三角形，6cm 羽化裙邊互相重疊，以光學透明度互補消除接縫；不增加碰撞、不擋視線、不寫深度，排序在戰鬥地面提示之前。草皮不透明度 .42，其餘 .34，仍保留真實地板。紋理與羽化座標存入共用網格 UV，可合批且不會在衝刺時滑動。九格預覽會檢查九個位置的實際像素，避免 Unity 合批後只剩原點附近可見。

`explore_ground_*_top.png` 與 `explore_ground_*_eye.png` 各五張是九格連續地面的俯視與 1.65m 視角。底下的灰褐平面僅為預覽中的中性 AR 合成參考，不會加入正式場景；它不是實機拍攝。

地面最終原生 run 41150829358（416fd39）已通過；`explore_ground_audit.json` 包含九格實際像素覆蓋檢查。總覽見 `ground_materials_overview.jpg`，草皮九格與局部放大見 `grass_detail.jpg`。

## 4m 連續路徑與圓弧湖岸（2026-10-31）

目前地圖 CellSize=4。**新接線請以本節為準**：

- Floor 用 `ExploreArt.CreatePath(map,x,z)`，以 SetParent(exRoot,false) 保留局部零點，再放到 map.World(x,z)。它取代原來很淡的 CreateGround；四向、斜向、1.4m 圓角、.35m 邊距與景物清空區都畫入材質。路中心 alpha .97、兩側深色草／石肩，預警圈仍畫在上方。
- Wall 改用 `ExploreArt.CreateWall(map,x,z)`，依鄰接路格圓化外露凸牆角；所有子網格都留在格子內。舊版地形參數介面仍保留。
- Gate／密道狀態變動後，重建改變格及周圍八格的路面／牆景。只讀地圖，沒有修改碰撞或玩法。**Claude 已於 85c4901 接入上述兩處新介面**；開門會清除格子畫面重建。
- 湖仍使用既有 CreateLandmark(lake)，但 LakeSurface 是整片 Water 格外框的平滑網格。逐格水牆只留下睡蓮／波紋裝飾，不再畫方形水面，因此不可省略整片湖地標。Shore_CurvedContour 是新岸邊子物件，沿平滑輪廓排列石頭、蘆葦。
- 凸角二次曲線切線長 1.5m，最大向原格邊內退 .375m；凹角切線長 1.25m，填順 .3125m；直岸微彎最大內退 .34m，短岸段按長度平方降低彎幅。沒有更改 Water 碰撞。
- 留下的 3m、ClampCell(1.42) 是母網格作者座標：牆／地標／競技場景物皆按 CellSize/3 水平縮放，4m 格子內實際上限為 1.893m；map.World 位移不重複縮放。驗證依真實世界座標量測，而非只看原始 mesh。
- `explore_path_ar_{neutral,wood,tile,carpet}_{top,eye}.png` 是程序模擬室內地板上的原生合成參考，不是 AR 實機截圖。`explore_lake_top_close.png` 顯示整片湖岸，另保留各地標第一人稱和全圖俯視。

最新目視修正：路口連通遮罩取位元前四捨五入，避免透視插值造成細條紋；岸帶採連續外法線消除裂縫，岸邊石頭／蘆葦沿曲線依距離配置，睡蓮減量並改變方向。最終原生 run 41159619094（73854ee）成功；105 組景物半徑、4m 牆界、凹形湖及四種背景道路像素檢查通過。第一人稱條紋已目視確認消失，湖岸無楔形裂縫。雙平台 run 41159619031（73854ee）的 Android、iOS 匯出與 IPA 編譯均成功；TestFlight 發布另行執行，未列入本次編譯驗收。


### 追加：月影湖加大圓角（2026-10-31）

依使用者最新要求，月影湖不再使用 1.5m 小圓角，改為 5.5m 真圓弧，與長岸相切，兩端接近半圓。通用半徑為 CellSize×1.375，最大為任一鄰邊的 46%，以保護窄形水域；凹角仍使用小幅二次曲線。

此項取代上述 .4m 內縮規格：湖角向任一原邊最多約 1.61m，所有水格中心仍有水覆蓋。碰撞未變，退縮的水角依然不可走。岸邊石頭、蘆葦沿大圓角重排。新增 20×12m 湖形測試，要求水面到原方角的最近距離至少 2m，並沿用閉合邊界、無尖角與水格覆蓋驗證。

最終原生 run 41163937989（95c8d02）成功；已更新湖泊俯視、第一人稱、全圖俯視及 lake_round_before_after.jpg。凸角水格的睡蓮／細波停用，避免大圓角外殘留水面裝飾。雙平台 run 41163938050（95c8d02）Android、iOS 匯出與 IPA 編譯成功。TestFlight 發布另行執行，不計入本次編譯驗收。
