# 鐵匠鋪交接

素材：`Village2D/in_smith_flame.jpg`、`anvil_0..5.png`、`lines_smith.txt`；人物位於 `Story2D/chars/`，小鐵及師父各有 focus／proud／awkward。師父另附 normal。

`smith_layout.json` 是 1080×1920、左上原點的設計座標。裝備列表需裁切、垂直拖曳；範例裝備、金幣、費用、成功率全部只是版面文字，接現有資料與條件。

強化按鈕可換行繪製，但供指引查找的文字保留 `⚒ 強化（💰 {cost}・成功率 {rate}%）`。回城按鈕保留現行 `🏘 回城鎮`，對應 brief 的「回村子」。

鐵砧每格約 83.33ms，六格循環三次共 1500ms；結果時間與強化判定仍由既有程式決定。下半部砧座像素固定；上半部鎚子和火花變動。`anvil.gif` 是循環預覽。

成功／失敗各十句，依 `[success]`、`[failure]` 分組。新人物與特殊姿勢記錄在 `Village2D/cast.json`；尚未更動 Runtime Cast 或事件。
