# 月影古堡：魔劍士惡靈

- castle_boss：6 個基本動作＋cleave_windup／cleave／wave_windup／wave／cast，共 11 張 8 方向 RGBA 圖集、272 格、13.21 MiB。
- 256×256 每格，腳底 94%，透明安全邊界；Default、sRGB、alpha、mipmap off、Clamp、Bilinear、max 2048、Normal compression。
- 使用內建 image_gen，來源與完整提示詞在 Tools/sprite_enemies/forest_boss_sources/castle_boss/recipe.json；打包僅切格與等比縮圖，沒有鏡像替代方向。
- directions：8 方向；walk／attack／cleave／wave／cast GIF：各動作；all_frames：全影格；before_after：原空殼騎士備用圖與新首領。
- check_forest_boss.py：11 張、272 格、0 errors（validation.json）。未修改 Runtime 或舊圖。未聲稱 Unity 平台編譯或實機驗收已完成。
