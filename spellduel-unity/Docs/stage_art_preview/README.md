# 火山賽璐璐重畫交付

依最新 `CODEX_TASK_CEL_RESTYLE.md`：A 指定檔名實際 7 張、B 15 張，合計 **22 張 PNG 原檔覆蓋**。工單中 9／18／33 的數量是舊計數；本次以更新後列出的檔名為準。

- A：噴口待機、預警、四格噴發、落石、碎石、出口圓陣、加亮光柱。
- B：熔岩谷、火山口邊緣、廢棄礦坑各 far／mid／ground／decals／props。
- 31 份 StageArt `.meta` 與 9 張 jump／duck／side PNG 逐檔 SHA-256 完全不變。
- 尺寸、模式、原檔名、圖集格子、落地線與匯入設定沿用；本批不改 Runtime。

素材以內建 **image_gen** 逐檔重畫，參考 006 的 `part_wall_straight.png` 與 `volcano_cel_v3/cel_ground_ash.png`。採冷色玄武岩、大塊兩階明暗、左上暖光、少量彎曲熔岩線；沒有以舊紅褐圖做色相替換。

輸出 PNG 合計 15.68 MiB（同範圍舊圖 26.98 MiB）。這是檔案大小，並非實機 GPU 記憶體或效能量測。貼圖數量與尺寸沒有增加。

## 預覽與驗證

- `obstacles/all_assets.jpg`：7 張重畫素材總表。
- `volcano/all_assets.jpg`：15 張場景素材總表。
- `volcano/volcano_{0,1,2}_player_view.jpg`：三種場景的資產投影合成。
- `cel_match.png`：新的噴口、噴發、落石、碎石、出口放在工單指定的既有 Unity 走道截圖上；原始走道背景未重繪。
- `restyle_validation.json`：22 檔清單、SHA-256、保護檔案檢查與預覽相機常數。
- 兩個子目錄的 `generation_manifest.json`：每張 image_gen 提示詞、母圖尺寸及 SHA、技術打包步驟。

所有預覽均標明為**資產合成**；沒有宣稱新 Unity／Android／iOS 執行截圖。母圖未經程式重畫；程式只處理規格要求的裁切、等比縮放、預乘 alpha、圖集定位、週期邊界與合成檢視。

驗證沿用 `Tools/check_stage_art.py` 與 `Tools/check_obstacle_art.py` 的資產檢查，結果皆 0 錯誤。舊工具內預覽相機仍是 22m／14% 的早期數值；本批預覽另按目前 `StageArtLayer.cs` 的 **24m／6%、眼高 1.45m、中景 13m** 產生，不使用舊預覽公式。

## 交接備註

遠景的畫面取景基準位於距底 5.90%（約 6%），左右銜接像素一致。中景保留母圖上方透明留白，並等比排列三段；礦坑支架不會因裁掉留白而變成 13m 高。

遊戲端 `StageArtLayer.CapColor[0]` 目前仍固定為舊紅褐色 `(0.24, 0.13, 0.12)`。本工單僅更換 PNG；後續接線 session 若調整仰視天空，應一併對齊新天空色。

生成器自己的母圖與作業紀錄留在 rpg-world-builder 的 `jobs/spellduel-cloud/cel-restyle-stage-art/20261108T094053386901/`；遊戲 Git 只交付成品與檢查／預覽。
