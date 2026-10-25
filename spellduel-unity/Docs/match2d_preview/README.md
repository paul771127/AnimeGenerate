# 玩家3D／2D對照

- 根目錄16張職業JPG：左為既有2D立繪，右為程序3D網格正面、45°投影，以大腿以上方便看臉和服裝。
- `all_16.jpg`：16職業總覽。
- `full/`：32張完整造型投影，含高於人物的法杖、弓與全身衣襬，不以對照卡片的裁切代替完整檢查。
- `geometry_validation.json`：與57d150f的逐職業預算、腳底、11個骨架掛點比對。
- `contract_audit.json`：C#程序網格在.NET Unity API契約環境中執行，驗證快取、動畫與換裝；不是Unity平台建置紀錄。

這批JPG是實際程序網格軟體投影，包含程序眼睛RGBA與透明墨線的混色，不是AI概念圖，不是Unity／手機截圖。Unity原生截圖由既有spellduel-snapshots工作流程的Snapshot.Render產生：`match_<id>.png`、`match_<id>_front.png`、`match_<id>_45.png`、`match_all_16.png`，以及`match_validation.json`。若CI成功下載，會另放在unity/。

只在Runtime加入程序C#，未新增任何模型或貼圖資源。所有參考立繪檔保持原樣。每職業的差異與近似方式見PLAYER_3D_MATCH_2D_BRIEF.md末段。

檢查：`python spellduel-unity/Tools/check_player_match2d.py`；有完整CI記錄後可加`--require-unity`，這個選項必須Android、iOS匯出與Unity快照皆成功才會通過。
