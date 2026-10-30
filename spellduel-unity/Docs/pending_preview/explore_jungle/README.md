# 翠蔭迷林：靜態場景交付

只新增 ExploreArt*.cs、原生預覽與文件。Explore.cs、Combat*.cs、SoloBattle.Explore.cs 的玩法／碰撞未由本批修改。動畫及影片由另一個 session 製作；本批沒有 Update 動畫、計時器或播放影片。

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

這是美術工廠與原生測試場景的交付；正式探索模式的接線仍由 Claude 負責。不可把這些圖片當成已完成手機 AR 整合實測的證明。

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

原生 Unity run 41083564537（201215e）成功，輸出 73 張場景 PNG 與 16 張靜態首尾圖。24 組牆變體均通過 ≤300 三角形、格子邊界與無 Collider 檢查；封印切換與 mesh／material 快取檢查通過。16 張圖均通過 960×544、四角純黑／綠幕與四周留白檢查。詳見 native_ci.json、explore_*_audit.json、keyframe_validation.json。

已目視複查木牆四面、岩壁、地標視角、植物、NPC 與首尾圖。Android／iOS 的建置結果另補；AR 實機視野與動畫影片接線需整合端驗收。
