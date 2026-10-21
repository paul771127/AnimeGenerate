# 第十三輪外觀對照

左欄直接使用 `../story_video/ref/` 的定裝原圖，僅等比縮放。中欄為實際 `Portraits.Render()` 產生的像素；右欄為實際居民程序網格的正面軟體投影。`*_threequarter.png` 為相同網格的3/4側面。

|人物|正面三欄|3/4|
|---|---|---|
|艾琳|[Erin_comparison.png](Erin_comparison.png)|[3/4](Erin_threequarter.png)|
|老焰|[Laoyan_comparison.png](Laoyan_comparison.png)|[3/4](Laoyan_threequarter.png)|
|小鐵|[Xiaotie_comparison.png](Xiaotie_comparison.png)|[3/4](Xiaotie_threequarter.png)|
|翠|[Cui_comparison.png](Cui_comparison.png)|[3/4](Cui_threequarter.png)|
|小葉|[Xiaoye_comparison.png](Xiaoye_comparison.png)|[3/4](Xiaoye_threequarter.png)|
|海斗|[Haido_comparison.png](Haido_comparison.png)|[3/4](Haido_threequarter.png)|
|凪|[Nagi_comparison.png](Nagi_comparison.png)|[3/4](Nagi_threequarter.png)|
|澪|[Mio_comparison.png](Mio_comparison.png)|僅頭像，無3D實體|

Boss 對照：[熔岩巨像](Golem_comparison.png)、[腐化樹王](TreeKing_comparison.png)、[深淵海妖](Kraken_comparison.png)；[Boss 頭像總覽](boss_portraits.png)。

[開工前逐圖觀察](observations.md) · [頂點與繪製量](metrics.json) · [腳底與身高](geometry_checks.json) · [驗證記錄](verification.json)。完整逐項✔／✘及說明見 [FACE_3D_BRIEF.md 第十三輪結果](../../FACE_3D_BRIEF.md#第十三輪結果)。

本機無 Unity Editor。3D 是程序網格軟體投影，非 Unity／AR 實機截圖；基本圖元為低面數近似、觸手 CPU 蒙皮保留子網格材質、粒子省略，描邊為螢幕空間近似。原生 shader、iOS／Android 與手機 AR 仍待建置驗證。
