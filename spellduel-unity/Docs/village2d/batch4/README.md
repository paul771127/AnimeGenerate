# 六設施與共用 UI

副本入口、訓練場、懸賞、觀戰、傳送陣、村口的背景在 `Village2D/in_{key}_flame.jpg`。其餘城鎮先共用此套。每個設施各附 mockup、標註版及 JSON 座標。

新增嚮導 guide、教官 instructor、懸賞官 bountyofficer、播報員 announcer，各有 normal／smile／surprised。warp 與 gate 也共用嚮導；既有公會長不重畫。cast.json 只供素材交接，不替代 Runtime Cast；台詞流程由程式端接。

共用框七張置於 `Village2D/ui/`。`slices.json` 提供尺寸、左右上下邊界；PNG meta 已設 Sprite、保留透明、無 mipmap、NPOT 不縮放。blank 列表中央保留半透明，文字由程式繪製。speech_bubble 尾巴在右下，右側與下側保留較大邊界。

所有元件仍沿用既有條件。訓練場四張挑戰卡，教學與導覽按鈕保留原文；懸賞按鈕保留「🔥 接下懸賞，出發討伐」；觀戰顯示完整代碼與獨立數字鍵；傳送清單只依現有到訪狀態；村口保留出發與故事兩個功能。

`*_layout.json` 的 scroll 區塊需要垂直捲動和裁切；子列是目前可見的設計示例，資料數量增加時延伸內容，不延伸遮罩。NPC 區與左側功能區分開，避免人物擋住操作。金額、推薦等級、裝備名稱、獎勵全部是示意，勿作數值資料使用。

整套是美術交接，沒有修改 WorldUI、戰鬥、存檔、網路或指引程式。mockup 使用交付框作 9-slice 合成；手機實際邊距由程式整合後再驗收。
