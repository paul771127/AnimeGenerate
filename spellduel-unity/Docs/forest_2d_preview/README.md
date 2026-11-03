# 森林敵人 2D 美術

## 第一批
毒液噴花、荊棘衝鋒獸、藤蔓術士：各 6 張八方向動作圖集。每格 256×256；idle 4、walk 6、windup 2、attack 3、hurt 2、die 4。

- 內建 image_gen 繪製；母圖、原始動作與完整提示詞：Tools/sprite_enemies/forest_boss_sources/forest_*/。
- 純機械切格與等比縮圖：pack_forest_enemies.py；check_forest_enemies.py --partial 檢查已完成批次，無 --partial 時要求全部 18 隻。
- *_directions.jpg、*_walk.gif、*_attack.gif 與 *_all_frames.jpg 供方向與動作檢視。manifest 含來源與輸出 SHA。
- 2026-11-04 使用者追加：其餘 15 隻稍微 Q 一點。後續人形約 4.5～5 頭身、生物頭略放大約 12%，保持精緻與戰鬥表情，不採 2.5 頭身幼兒風。前三隻及首領維持已完成比例。
- 不修改 Runtime、戰鬥、資料或網路。格式檢查不代表 Unity 平台編譯或手機實機驗收。
