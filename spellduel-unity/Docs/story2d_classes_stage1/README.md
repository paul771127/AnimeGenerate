# 選角職業頭像：第一批三張

依使用者指定的確認順序，本批只新增 `novice`、`swordsman`、`mage`，其餘 13 張等待這三張的畫風與主角身份確認後再製作。

[三張完整立繪與實際臉部裁切](portraits_and_crops.jpg)

正式檔案：`Assets/SpellDuel/Resources/Story2D/classes/{id}.png`，每張 768×1152 RGBA，保留真正透明背景。匯入 meta 沿用 chars：mipmap 關閉、最大 2048、保留 alpha、`nPOTScale: 0`，新 GUID 各自獨立。

使用內建 image_gen，先以 Player.png 的褐色兜帽旅裝生成初心者正面母圖；Player 參考圖只有背面，因此臉與短栗髮由本批建立。之後以同一母圖做 identity-preserve 職業換裝，保持年齡、臉型、髮型、琥珀眼與頭部角度，分別換成藍白輕甲與紫金法袍。艾琳／老焰作賽璐珞線條、明暗與完成度參考，不借用其人物身份。職業色參考 Skills.cs 的初心者米金、劍士藍、法師紫。

初心者握長劍，劍士握藍白劍光長劍，法師握杖並以另一手施放法陣；光效保留在臉部以外。三張採同一縮放與定位，頭部置中、面向右前方，上方保留空間。預覽下排直接使用程式指定的寬 30%～70%、高 2%～30% 裁切範圍，確認臉與髮型可讀。預覽為資源排版，非 Unity 實機截圖。

只做匯出尺寸、定位及色盤壓縮，沒有用程式重畫五官或裝備。完整提示詞見 [prompts.json](prompts.json)，資源雜湊及轉換參數見 [assets.json](assets.json)，格式檢查見 [validation.json](validation.json)。

```sh
python spellduel-unity/Tools/check_story2d_classes.py
# 全部 16 張獲確認並製作後：
python spellduel-unity/Tools/check_story2d_classes.py --all
```

沒有修改流程、戰鬥、數值或任何 Runtime 程式。Android 與 UNITY_IOS 編譯以本次提交的 GitHub Actions 結果為準。
