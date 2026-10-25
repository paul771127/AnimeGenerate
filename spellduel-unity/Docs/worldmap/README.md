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
