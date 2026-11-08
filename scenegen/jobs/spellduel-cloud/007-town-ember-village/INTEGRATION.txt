工單 007：炎之里 town0_cel_v1 交接

成果：31 FBX、3 PNG、kit.json、town_layout.json；32,295 個三角面（按實際擺放次數計，含外圍山壁、室內、擺設與遠景）。最大建築道具店 4,480 面。
13 棟建築／設施：鐵匠鋪、道具店、公會、老焰的家、副本嚮導棚、訓練場、告示板、觀戰席、晨鐘鐘樓、三間民房、工具棚。
四棟主要室內與嚮導棚各有實際無障礙 4×4m 活動區；門寬分別 4.6、4.6、4.8、2.6、4.0m，天花板下方 >=3.3m。
室內範圍／扣掉設備後的可走面積、每個 NPC 的門口距離、逐棟面數與最窄道路 2m 全列在 inspect.json。
NPC 人形只在預覽用，沒有把替代角色混進 TownKit；遊戲沿用自己的角色。

座標與擺放：
所有 JSON 座標都是村子世界座標，2D 陣列是 [x,z]，單位公尺；Y 向上，yaw=0 朝 +Z。不要再反轉 X/Z。
FBX 每棟在自己的地面中心，正門朝 +Z。載入後只套 building.pos、yaw 一次，不需額外縮放。
例如 Resources.Load<GameObject>("TownKit/town0_cel_v1/bld_smith")。
props、ground、environment、perimeter、gate 都依資料擺放；props.height 是可選的 Y，預設 0，scale 預設 1。
mountOwner 是分組提示；這些掛燈與貨物的位置依然是世界座標，不要當成建築／貨車的區域座標再轉一次。
environment 是村外景觀；ground_outside 在 -0.035m，是視覺地表，不擴張玩家半徑。

材質：
普通模型材質槽 0=town0_arch_albedo，槽 1=town0_detail_albedo；有些子物件只有槽 0。背景物件全用 town0_backdrop_albedo。
FBX 依首次材質使用排序時也保持此順序，已由實際 Unity 匯入與遮罩角色檢查驗證。請不要把兩張圖集互換。
使用共用 Built-in Unlit/Texture；沒有即時燈光、透明粒子、法線貼圖或 URP 材質。光影兩階已寫進共用貼圖。
Android/iPhone ASTC 6×6、mipmap、Read/Write 關閉、Trilinear。兩張主圖 2048、遠景 1024，全部 sRGB=true。
窗、藥瓶、燈籠火光均以不透明幾何／顏色表示。燈籠、木箱、木桶、火盆是共用模型；遊戲可依材質合批。

碰撞與室內：
root.colliders 是權威的完整清單；polygon 是世界 X/Z 多邊形，另有 minY/maxY。
建築 solid 是凹的 U 形牆體，不是實心地基；solidPolygons 是同一批牆／櫃台／爐子等的便利副本。
不要同時把 root.colliders 和 building.solid/solidPolygons 重複解析，也不要用整棟的圓形或包圍盒當牆，否則會封住室內。
walkable/interior 是室內地板邊界，碰撞多邊形優先、從中扣除；clearWalkRectangle 是經實際膠囊檢查的 4×4m 空間。
paths 是寬 2m 的室外路面中心線，pathInfo 對應寬度；interactionRoutes 是門內到可對話站位的驗證路徑。
中央 (0,5) 半徑 3.5m 沒有固定障礙，spawn=(0,0)、yaw=0。東出口 (13,5)，halfWidth=1.4；傳送陣 (5,.5)。

NPC：
角色 id 精確對應 smith、shop、guild、dungeon、training、bounty、watch、story:老焰、story:小鐵、story:米菈。
鐵匠／小鐵在鐵砧後方與爐邊，店員／公會長在櫃台後，老焰在家，嚮導在棚下；其他人站室外。
honour wander.enabled=false，不能沿用舊 TownWalk 的任意圓形閒逛，避免離開工作區或穿牆。
conversation.playerPos 是驗證過的玩家站位，與 NPC 相距 1.9m；可供互動／導航用，無須把玩家強制傳送過去。
NPC 的 inside 非 null 時，用該建築 interior 作為室內判定；櫃台、鐵砧與爐子仍是獨立碰撞。
晨鐘塔不可進入，landmarkState.bell=silent，遊戲接 chapter1_boss_defeated 後再啟用鐘聲；沒有自動播放聲音。

控制接口：
生成器根目錄新增 town_kit_control.py，API v1；沒有改既有 control.py 或 cel_kit_control.py。
鏡像在 scenegen/tools/town_cel_v1/；capabilities → plan → validate → generate → inspect。
generator_request.json 是本機實際請求。style.json 提供可攜輸入，可用 configFile 引用。重播必須指定全新 outputFolder。
鏡像工具需設定 BLENDER_PATH 到 Blender 4.5。請求／原始輸出分別留在生成器 jobs 與獨立 runs。
plan 列主要空間與通路，generate 另加掛燈與貨車貨物；最終 town_layout.json 是遊戲的完整擺放依據。

驗證與預覽：
原 62 個核心測試、13 個新增城鎮測試通過。31 FBX 重新匯入檢查 UV、原點、零面積與門寬／頂棚／活動區。
Unity 2022.3 Built-in 直接匯入本次 FBX，再以半徑 0.27m 的站立膠囊檢查 906 個路線點及 845 個活動區點。
10 個 NPC 以實際 FBX 碰撞檢查站位與可達對話位置。這些是電腦端幾何驗證，手機 AR FPS／熱尚未實測。
overview.png 是原生 Unity 圖加投影編號與圖例，屋頂未移除；spawn_view 眼高1.5m、位置(0,0)、朝+Z。
smith/shop/guild_inside 由門口看進去；另附 inside_labeled 版本。east_exit 是廣場看東出口的真實場景。
新 AI 圖片僅為材質色樣，使用內建 image_gen；完整提示詞在 atlas-prompt.txt，來源與輸出規格在 provenance.json。
所有城鎮預覽都是實際模型渲染，沒有用概念圖替代。既有 volcano_cel_v3 的 39 個檔案（含 .meta）雜湊保持不變。

本工單只供應場景生成器、資產與配置；依工單分工，由遊戲端 session 改 TownWalk.cs 讀取這一包，其他城鎮照舊。
