# 道具店／職業工會

`shop_mockup.jpg`、`guild_mockup.jpg` 是畫面示意；同名 `_layout.jpg` 標示元件 id，`_layout.json` 提供逐個點擊與列表區域。

道具店沿用買道具／賣裝備兩分頁。買清單每列為圖示、名稱、說明、價格；切到賣裝備時，在同一 scroll 區域置換為裝備名、穿著狀態與賣價按鈕，保留原本可售條件。`lines_shop.txt` 分 sale／insufficient／sell 三組。

新增店員 id `clerk`，normal／smile／surprised 在 `Story2D/chars/`；服裝及道具固定，只替換臉部。公會長沿用 `guildmaster_normal/serious/smile`。五職業卡片沿用 `Story2D/classes/` 現有圖。

所有示意的裝備名、價格與試煉狀態只作排版；實際資料、按鈕動作、是否開放與是否選中均由現有程式供應。按鈕若採換行顯示，請仍保留新手指引原始文字索引。
