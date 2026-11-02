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

原生 Unity 驗證：[41536524288](https://github.com/paul771127/AnimeGenerate/actions/runs/41536524288)，來源 commit `8242dcb`，成功。`castle_redesign_castle_*.png` 是 Unity 直接使用 SpriteEnemyView 的貼圖、材質及 UV 格式渲染；六列依序是待機、移動、蓄力、攻擊、受擊、倒地，每列八方向。`native_overview.jpg` 是八種原生檢查圖的縮圖總覽。五間城堡的新版敵人場景圖在上一層資料夾。

八張遊戲 PNG 合計約 20.97 MiB，尺寸與原版相同；大型來源圖保存在 Tools，沒有放進 Assets，不會隨遊戲打包。本輪沒有修改 Runtime、戰鬥、連線、資料或動畫時間。Android／iOS 驗證：[41536527888](https://github.com/paul771127/AnimeGenerate/actions/runs/41536527888)，來源 commit `8242dcb`，Android、iOS Unity 匯出、Xcode 未簽署 IPA 均成功。原生及平台 CI JSON 位於上一層資料夾；check_castle_art.py 會比對實際受測內容，避免拿舊版驗證當作新版通過。這是 CI 編譯與原生渲染驗證，未宣稱手機實機遊玩驗收。
