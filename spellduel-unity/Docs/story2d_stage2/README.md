# Story2D 第二批：完整角色與背景

沿用已確認第一批的製作方式：內建 image_gen 生成 normal 母圖，再對同一母圖逐張編輯臉部表情；最後以臉部遮罩合回母圖。沒有用 3D 投影取代立繪，沒有修改 Runtime 或對話流程。

完整資源以 `Story2D.Cast` 為準：22 位人類／人形角色各六表情，9 位 Boss／殘影各 normal、angry，共 150 張立繪；18 張背景。艾琳、老焰與 village 沿用已確認的第一批插畫。為控制全套容量，立繪統一使用允許的 640×960 RGBA；背景維持 1080×1920 RGB JPG。全套遊戲圖片共 39,273,419 bytes（約 37.45 MiB，39.27 MB），包含第一批。實際完整性與容量見 [validation.json](validation.json)。

- [全員造型總覽](cast_lineup.jpg)
- [全部背景總覽](backgrounds.jpg)
- 每位角色的 `{id}_expressions.png`：一張全身構圖加上各表情臉部特寫。
- 對話排版：[礦坑](dialogue_mine.jpg)、[森林](dialogue_forest.jpg)、[海上](dialogue_sea.jpg)、[角鬥場](dialogue_arena.jpg)、[山道](dialogue_canyon.jpg)、[公會](dialogue_guild.jpg)。這些是依現有 UI 位置合成的美術示意，並非 Unity 或手機截圖。
- [生成提示詞](prompts.json)、[逐檔雜湊與製作記錄](manifest.json)、[劇本文字與設定保留檢查](script_integrity.json)。

## 參考與身份

小鐵、翠、小葉、海斗、凪、澪、熔岩巨像、腐化樹王、深海巨妖依 `Docs/story_video/ref/` 的同名定裝圖及 `frames/` 對照。翠採 v2_s03 的半睜綠眼；澪保留影片的淡藍長髮、閉眼常態與白色蕾絲靈體衣裝。艾琳／老焰的已確認立繪只作畫風參考，不將其服裝借給其他角色。

無定裝圖的人物依 `CHARACTER_PERSONA_BRIEF.md`、`Portraits.cs` 與既有居民服飾設定：米菈紫髮旅行商人、楓銅髮巡林者、阿潮灰髮草帽老漁夫、雷歐斷角頭盔、鐵娘皮工作圍裙、阿鈴三枚鈴鐺、公會長三鐘徽記、塔守紫色星燈斗篷。霸主是劇本中的角鬥場經營者，保留人類形象；青銅鬥魂是黑晶束縛的空甲。三種殘影仍分別對應巨像、樹王、巨妖，不變成人物。

這些插畫以影片身份與服飾為參照重繪；不宣稱是影片原始模型或逐像素複製。

## 表情固定與容量

只將生成結果的眉、眼、嘴與臉頰區域合回同一 normal。所有表情共用定位、縮放、alpha、臉部色盤與衣裝色盤；非臉部 RGBA 像素逐一檢查完全相同。遮罩放在 `masks/`，僅供工具檢查，不匯入遊戲。衣裝使用共用 64 色色盤、臉部使用共用 256 色色盤，無抖色；PNG 仍是四通道 RGBA，並保留透明邊緣。殘影與澪保留半透明。

第一批只做相同插畫的縮放與壓縮，沒有重新生成或改造型。頭部尺度以臉部視覺大小對齊，帽子、髮髻、長鬍與 Boss 不計入臉部高度；頭飾頂端約保留 6% 上方空間。人類立繪朝畫面右側，右側角色由既有 UI 翻轉。

背景沒有角色，主題集中上方，下方為對話框保留簡單地面。熔岩大廳對照 v1_s01，記名林對照 v2_s02，海港／礁石對照 v3_s03～v3_s05。TextureImporter 依第一批：最大 2048、Default 預設壓縮、關閉 mipmap、`nPOTScale: 0`；所有新貼圖 GUID 唯一。

## 劇本與驗證

其餘主線 97 句、副本 76 句角色台詞補上英文表情標記，Boss／殘影只用 normal 或 angry。補充城鎮、途中、首領與回城的背景指令。相對 188fb99 去除所有表情與背景指令後，原文字、分岔、任務、關卡 id、theme、waves、req、數值的雜湊完全相同。

```sh
python spellduel-unity/Tools/check_story2d.py
python spellduel-unity/Tools/check_dungeons.py
python spellduel-unity/Tools/preview_story2d.py
```

`check_story2d.py` 檢查完整 Cast、150 張表情、18 張背景、RGBA／RGB、尺寸、透明度、非臉部像素、表情差異、meta、唯一 GUID、劇本標記、背景引用、原文字與設定雜湊及 40 MiB 容量。

UNITY_ANDROID／UNITY_IOS 的實際編譯以本次提交的 SpellDuel Unity GitHub Actions 為準；Python 驗證不替代 Unity 編譯。
