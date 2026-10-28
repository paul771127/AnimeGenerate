# 空賊船長立繪

逐風號船長以葡萄紫外套、黃銅護目鏡與佩刀對應飛艇。`aircaptain` 提供 normal、angry、smile、serious，768×1152 RGBA；沿用既有臉部遮罩組裝流程，四張臉外像素完全相同。透明背景已檢查 alpha，材質沿用老焰匯入設定。

使用內建 image_gen：先以老焰立繪作線條／賽璐珞風格參考生成船長，再以母圖分別只編輯憤怒、狡猾笑容、嚴肅表情。實際生成提示與來源紀錄見 aircaptain_prompts.json。概念立繪不是 Unity 模型截圖。

Story2D.Cast 新增對照。PendingArtAudit 保留全部既有檢查，額外檢查四張船長立繪尺寸，以及所有劇本說話人實際可載入立繪。既有對話頁直接使用 Story2D；不改對話流程。

Python 劇本保真基準更新到先前已提交的飛毯副本 6697a27，保留舊基準，台詞與關卡設定不變，船長兩句僅加 smile／angry 表情標記。原生全站 Snapshot 與雙平台結果待 CI 回傳後補記。
