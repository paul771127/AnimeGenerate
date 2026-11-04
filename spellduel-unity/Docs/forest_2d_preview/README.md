# 森林敵人：中等細節動畫風重製完成

18 隻全部依使用者確認的新風格重畫。每隻六種動作、八方向，共 108 張 RGBA 圖集、3024 格，約 155.47 MiB。保留原有檔名、GUID、匯入設定，未修改 Runtime。

- roster_grid.jpg：全員最新總覽。
- before_after.jpg：上方原版，下方新版本。
- 各角色 directions.jpg、all_frames.jpg、walk.gif、attack.gif：八方向及動作預覽。
- validation.json：完整圖集檢查 0 錯誤。
- 母圖與動作來源：Tools/sprite_enemies/forest_refined_sources/。三張概念總覽供造型追溯。

此環境未執行 Unity 編譯；平台建置由推送觸發 GitHub Actions，不能將素材格式檢查視為編譯通過。

## 前次交付與分批紀錄

# 森林敵人 2D 美術

## 第一批
毒液噴花、荊棘衝鋒獸、藤蔓術士：各 6 張八方向動作圖集。每格 256×256；idle 4、walk 6、windup 2、attack 3、hurt 2、die 4。

- 內建 image_gen 繪製；母圖、原始動作與完整提示詞：Tools/sprite_enemies/forest_boss_sources/forest_*/。
- 純機械切格與等比縮圖：pack_forest_enemies.py；check_forest_enemies.py --partial 檢查已完成批次，無 --partial 時要求全部 18 隻。
- *_directions.jpg、*_walk.gif、*_attack.gif 與 *_all_frames.jpg 供方向與動作檢視。manifest 含來源與輸出 SHA。
- 2026-11-04 使用者追加：其餘 15 隻稍微 Q 一點。後續人形約 4.5～5 頭身、生物頭略放大約 12%，保持精緻與戰鬥表情，不採 2.5 頭身幼兒風。前三隻及首領維持已完成比例。
- 不修改 Runtime、戰鬥、資料或網路。格式檢查不代表 Unity 平台編譯或手機實機驗收。


第二批：forest_chomper、forest_sporecap、forest_seedgunner 已完成；採用使用者要求的稍微 Q 版比例。累計六隻，格式與透明度驗證通過。


追加完成：古根撼地獸、苔甲守根者、棘莢狙擊手。累計 9 / 18 隻；詳見 validation.json。


追加完成：夢孢司祭、菌冠育巢者、翠翼巡獵者。累計 12 / 18 隻；詳見 validation.json。


追加完成：疾風翠豹、竄林孢子菇、霧林斥候。累計 15 / 18 隻；詳見 validation.json。


## 全部交付
18 隻全部完成，共 108 張 RGBA 動作圖集、3024 格；castle_boss 另外 11 張、272 格。森林圖集約 191.34 MiB，首領約 13.21 MiB。

使用內建 image_gen 繪製，母圖、各組動作提示詞和修正提示詞保存在 Tools/sprite_enemies/forest_boss_sources/。Python 僅負責切格、縮放、打包與預覽組版，沒有鏡像補方向或程序生成替代姿勢。

roster_grid.jpg 為三排全員總覽，每隻有 directions.jpg、all_frames.jpg、walk.gif、attack.gif。後十五隻遵照使用者追加要求採稍微 Q 版的緊湊比例；首三隻與首領維持先前確認畫風。

執行 check_forest_enemies.py（完整模式）與 check_forest_boss.py：0 錯誤。未變更 Runtime 或玩法。此環境未執行 Unity；平台建置以 GitHub Actions 為準。


追加完成：林間弓手、森林德魯伊、林語薩滿。累計 18 / 18 隻；詳見 validation.json。


2026-11-04 中等細節新風格：毒液噴花、荊棘衝鋒獸、藤蔓術士、食人花 已換新，累計 4/18。使用者已確認 concepts_01.png 的比例與細節程度。


2026-11-04 中等細節新風格：孢子菇、種子槍手 已換新，累計 6/18。使用者已確認 concepts_01.png 的比例與細節程度。


2026-11-04 中等細節新風格：古根撼地獸、苔甲守根者、棘莢狙擊手、夢孢司祭 已換新，累計 10/18。使用者已確認 concepts_01.png 的比例與細節程度。


2026-11-04 中等細節新風格：菌冠育巢者、翠翼巡獵者、疾風翠豹 已換新，累計 13/18。使用者已確認 concepts_01.png 的比例與細節程度。


2026-11-04 中等細節新風格：竄林孢子菇、霧林斥候、林間弓手 已換新，累計 16/18。使用者已確認 concepts_01.png 的比例與細節程度。


2026-11-04 中等細節新風格：森林德魯伊、林語薩滿 已換新，累計 18/18。使用者已確認 concepts_01.png 的比例與細節程度。
