# WorldMap 美術交接

依 WORLD_MAP_BRIEF.md 製作；只交付美術、座標和離線示意，沒有修改 Runtime 或解鎖規則。

## 第一批

- `Resources/WorldMap/region_1.jpg`：2160×1920，炎之里、灰燼山路、斷鐘熔爐、鐘塔遺跡、火山口與第 5 關預留空地。
- `nodes.json`：目前交付區域列在 availableRegions；座標始終以整卷 10800×1920、左上原點計算。每個點都有站位和名牌中心。
- `ui/`：七種關卡／城鎮／塔標記、label_plate、三張 fog、四種星數，共 15 張透明素材。label_plate 是 512×128，9-slice 邊界 left=64、bottom=32、right=64、top=32。
- `hero_novice_side_0..3.png`、`hero_novice_idle_0..1.png`：192×256 透明小人，腳底在圖下緣中央附近；建議顯示錨點 (96,252)，左右行走由程式翻轉。
- `mockups/01_start.jpg` 與 `03_select_1_3.jpg` 是 1080×1920 版面示意，同名 JSON 記錄視窗偏移和按鈕位置；不是 Unity 真機截圖。

## 接線規則

目前每章仍是四關。`x-5` 設 `reserved:true`、`visibleByDefault:false`；不能當成新關卡顯示或解鎖。paths 中 **只使用 enabled=true 的 current 路線**。預留邊另外標記 future-five-stage、enabled=false；之後擴關才啟用。Boss 直接接下一座城鎮，路線經過預留空地。

無盡塔現在從 2-4 分岔；futureBranchFrom 只是未來改接 2-5 的交接提示。本次沒有新增任何遊戲規則。

霧圖邊緣透明，需要重疊鋪排；為了完全遮住未知地形，示意另鋪不透明底色並以雲形柔邊過渡。開放位置、雲霧散開、走路及 zoom-in 都由程式控制。角色沿折線移動時使用腳底錨點，不用貼圖中心。

按鈕文字保留「🗺 大地圖」「⚔ 出發」「📖 看故事」「📜 角色」「🏘 回城鎮」。範例金幣與推薦等級只是佔位字，不是新的數值。

素材使用內建 image_gen，參考 Village2D 與已確認的 Story2D 職業主角。提示詞、來源尺寸、匯出尺寸和雜湊在 prompts.json／assets.json。輸出按規格重採樣，並不宣稱全部為原生目標解析度。JPG 地圖最大匯入 4096，PNG 最大 2048，全部關 mipmap、NPOT 不縮放。

檢查：`python spellduel-unity/Tools/check_worldmap.py --partial` 用於分批交付，最後應執行不帶 partial 的完整檢查。partial 的 pending 不能當作全套完成。

## 第二批：森林與無盡塔

新增 region_2、森之里、四關與預留點，及 2-4 → tower 的八點支線。塔站位量測在上層橋入口，避免角色走到森林懸崖或下層裝飾橋；塔頂沒入霧中。

`mockups/02_tower_branch.jpg` 顯示岔路。`seams/seam_1.jpg` 是第一、二區接縫放大檢查；邊界先由 image_gen 補繪，再以固定羽化遮罩合成，只有接縫附近變動。數值差異報告只用來輔助找接縫，不能代替視覺驗收。

## 第三批：五區全卷

新增 region_3～5：港口／航圖台／歌聲小屋／白帆船／巨妖漩渦，赤砂市集／鐵門銅鑼／地刺坑／高層看台／青銅鬥場，以及風沙驛／駝鈴崖道／貨箱岩棚／岩壁刻痕／岩之鐘。沒有把第五區改成雪景，也沒有在地圖烘焙文字或人物。

`full_preview.jpg` 為 5400×960 的無標註全景，`nodes_preview.jpg` 疊出全部站位與目前可行走折線。四個接縫放大在 seams/，主路跨區連續。nodes.json 共 31 個點：五城、20 關、五個隱藏預留點及塔；34 段線中，25 段目前啟用、九段是未來擴關的停用線。

五區圖片合計 10800×1920，請按 region 分片載入／卸載，勿常駐解壓整卷；手機上的平移與縮放、點擊、行走加速及整備轉場均由程式整合。
