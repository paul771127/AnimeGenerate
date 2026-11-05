# 熔岩 HD-2D 試做素材

僅交付熔岩 0／1／2，待使用者確認後再製作其他主題。本批不改 Runtime 或玩法。

## 交付
- Resources/StageArt：15 張 PNG + TextureImporter .meta。
- 0 熔岩谷：霧紅天空、遠方火山、熔岩瀑布、玄武岩與焦木。
- 1 火山口邊緣：巨大環狀盆地、遠處噴發、黑曜石與硫磺岩。
- 2 廢棄礦坑：洞頂、木構採礦廊道、礦車、提燈與散落軌道。
- 每套 16 格景物、16 格貼花；順序從左上逐列往右，格尺寸 512。
- 景物有透明留白，腳底在格內 y=491（由頂端起算；約距底 4%）。

## 匯入與接線
far 4096×512、mid 4096×384、ground 1024×1024、兩種 atlas 2048×2048。
Default、sRGB、Bilinear、不開 mipmap、不縮放非方形貼圖。
far／mid／ground Repeat；atlas Clamp。far／mid 最大 4096，其他 2048。
far／ground 不透明；mid／atlas 真正 RGBA。
所有 PNG 共約 24.53 MiB（不含預覽）。

mid 保留繪圖比例後分段排列，沒有將木架直接橫向拉扁。
預覽使用 mid 圓筒半徑 22m、高 **13m**、底部 y=0；
建議接線時沿用此高度，避免木架變寬。遠景半徑 32m、高 22m、底部 -1.5m。
地板每張 2.5m，立牌每格對應 3m。實際光暈由遊戲加入。

## 預覽與檢查
volcano_N_player_view.jpg 是資產投影合成示意，**不是 Unity 實機截圖**。
相機眼高 1.5m、水平視角 80°，前方為現有 arena_imp 的 idle 格；
無 UI、光暈或遊戲後製。ground 延伸只用來呈現透視，不能解讀成碰撞場地大小。
volcano_N_far_strip.png 為交付遠景原尺寸長條。
volcano_N_ground_repeat.jpg 為 2×2 接縫檢視。

執行：python spellduel-unity/Tools/check_stage_art.py
結果：15 張齊全，尺寸／alpha／匯入設定／接縫／32 格分割與基準線檢查 0 錯誤。
每個主題的 atlas 各 16 格，總共 96 格。
邊界像素一致不代表整張畫面完全沒有重複感；已另行查看合成圖和地面重複圖。
本批為美術資產，不宣稱完成 Unity、Android 或 iOS 編譯／遊戲接線。

## 製作
使用內建 image_gen 生成手繪母圖；generation_manifest.json 保留每張提示詞。
pack_stage_art.py 只做指定尺寸、邊界週期銜接、透明 atlas 分格與落地基準整理；
check_stage_art.py 產生檢查報告與資產合成圖。
生成來源尺寸與輸出尺寸分別記錄，4096 長條不是原生 4096 繪製。
