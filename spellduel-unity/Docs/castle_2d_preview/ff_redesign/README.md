# 月影古堡：日式漫畫奇幻敵人重畫

8 種敵人改為正常比例、冷酷／兇狠戰鬥表情，維持各自武器與剪影。

- 48 張 RGBA 動作圖集，8 方向、每格 256×256，共 1,344 格；PNG 約 69.11 MiB。
- idle 4、walk 6、windup 2、attack 3、hurt 2、die 4；舊版單張備用圖未修改。
- `*_directions.jpg`：八方向；`*_walk.gif`、`*_attack.gif`：動畫；`*_all_frames.jpg`：完整影格；`before_after.jpg`：新舊比較。
- 使用內建 image_gen 生成／修訂原圖；來源與提示詞在 Tools/sprite_enemies/ff_sources。pack_castle_ff.py 只負責切格、預乘 alpha 等比縮放與打包，不用鏡像代替方向。
- 最後修訂提示：保持既有角色、四欄五列與動作順序；縮小繪圖以容納完整劍尖、翅膀與法杖；魔法光限於武器附近；完全透明背景。左右前方向另繪，非鏡像。
- check_castle_ff.py：48 張、1,344 格、尺寸、匯入設定、透明邊界、動作差異、死亡姿勢、備用圖 SHA 與 Runtime 不變，0 errors（validation.json）。
- 本機沒有 Unity；本輪沒有宣稱 Android／iOS 編譯或手機實機驗收已完成。推送後由 CI 與遊戲端驗收方向、動作播放及顯示大小。
