# 月影古堡敵人重畫

使用者定案：可愛但有戰鬥感、精緻服裝、大眼睛、約 2.5 頭身。

八種全數重畫：骷髏劍兵、空殼騎士、骷髏弓手、提燈幽靈、吸血小蝙蝠、南瓜炸彈、小石像鬼、見習黑魔導。

- `before_after.jpg`：上排原版，下排新版，使用遊戲實際 128px 尺寸。
- `*_all_frames.jpg`：每種 21 個動作影格 × 8 方向。列號 0–3 待機、4–9 移動、10–11 蓄力、12–14 攻擊、15–16 受擊、17–20 倒地。
- `*_animation.gif`：八方向同步播放，方便檢查輪廓、腳底與動作。
- `atlas_manifest.json`：最終圖集及來源圖 SHA-256、縮放參數。

使用內建 image_gen 產生定稿及各動作圖；原圖與提示詞保存在 `Tools/sprite_enemies/painted_sources/`。繪圖風格、服裝及武器以各自 turnaround 為參考。弓手待機的背左方向另外繪製四格，沒有用鏡像代替。南瓜與石像鬼移動重新生成，避免混入攻擊姿勢；灰霧背景用 image_gen 清除。

`pack_castle_painted.py` 僅找透明間隙、切格、等比縮放及排列。沒有用幾何圖形代畫角色、沒有扭曲單張靜態圖假裝動畫。各動作組使用共同縮放值，倒地不會被放大到站姿高度；腳底對齊原本 6% 留白。半透明邊緣以預乘 alpha 縮圖。圖集維持 2048×2048 RGBA、128px 格、168 個使用格與 88 空白格，沿用原 `.meta`。

重建：`python spellduel-unity/Tools/sprite_enemies/pack_castle_painted.py`
驗證：`python spellduel-unity/Tools/check_castle_enemies.py`

原生 Unity 預覽及 Android/iOS 編譯證據完成後補入。本輪沒有修改 Runtime、戰鬥、連線、資料或動畫時間。
