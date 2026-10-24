# Story2D 首批畫風確認

本批只包含艾琳、老焰各六種表情，以及炎之里 `village` 背景。其他角色與背景等待使用者確認後再製作。

- [六表情總覽](expressions.png)
- [對話合成預覽](dialogue_preview.jpg)：美術排版示意，非 Unity／手機截圖。
- [資源及一致性檢查](asset_checks.json)
- [生成提示詞集](prompts.json)

正式資源位於 `Assets/SpellDuel/Resources/Story2D/chars/`、`bg/`，依 `Story2D.Cast` 命名。12 張立繪為 RGBA 768×1152；背景為 RGB JPG 1080×1920。首批圖片共約 9.57 MiB。

使用內建 image_gen：兩張 normal 母圖依 Erin.png、Laoyan.png 與影片影格 v0_s05／v0_s03 生成，再各生成五個臉部表情編輯。人物與臉均微朝畫面右側；頭部尺寸以不含兜帽、髮髻及長鬍的臉部視覺尺度對齊至約 150px，頭飾最高處約在距頂 6% 附近。不可將生成結果宣稱為原影片模型或逐像素複製；本批供角色相似度與畫風確認。

表情僅取生成的臉部區域放回同一母圖，統一縮放、定位；非臉部 RGBA 像素已逐像素驗證相同，因此衣服、手勢、配件、姿勢及位置不隨表情跳動。face_mask 圖是檢查用範圍，不載入遊戲。未用程式重畫人物或替換為 3D 投影。

背景對照炎之里影片的瓦頂、鐘樓、石板廣場，保留山景及側邊鍛爐；沒有任何人物，下方三分之一為較簡單的鋪面。

主線 58 句、副本 13 句艾琳／老焰台詞加上 `［expression］`；原文字、關卡 id、theme、waves、req、數值與分岔完全保留。第一段村莊開場加上 `背景：village` 以展示本批背景，沿用既有換背景功能。沒有修改 Runtime、戰鬥或對話流程程式。

檢查：

```sh
python spellduel-unity/Tools/check_story2d.py
python spellduel-unity/Tools/check_dungeons.py
```

完整 UNITY_ANDROID／UNITY_IOS 編譯由此分支的 SpellDuel Unity Actions 執行；本目錄不把離線合成圖當作手機實機驗證。TextureImporter 使用 Default Texture、最大 2048、預設壓縮，關閉 UI 圖不需的 mipmap；角色保留來源 alpha。
