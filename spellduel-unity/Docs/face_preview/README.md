# 第三輪眼睛與髮型預覽

27 個角色 × 3 個正交視角（81 張），另有 [眼睛特寫總覽](eyes_overview.png) 與 [無帽髮型剪影總覽](hair_silhouettes.png)。眼型／髮型和成本對照表見 [需求文件的第三輪結果](../../FACE_3D_BRIEF.md)。

相機沿頭部局部軸看 0°／45°／90°，不把預設歪頭誤當正面。主圖保留實際模型的裝備和遮擋；右下小圖使用同一份五官網格去除頭髮和帽飾，便於檢查被遮住的位置。髮型總覽填黑頭模＋頭髮，移除帽飾以檢查髮型本身。

虛線是第二輪基準比例，白點是共用頭模、眼片標尺、眉線、嘴部和耳廓的採樣標記。眼型的露白高度與眼尾會依角色改變；眼片標尺不是虹膜／眼線的逐像素邊界，`nominalHeights` 是標準眼高，眼尾可伸到標尺之外。三維資料與誤差見 [measurements.json](measurements.json)。

產生方式：實際 C# 原始碼配合 Unity API 測試替身建立網格，依頭部矩陣轉到局部座標，以軟體光柵器作正交投影；眼睛使用實際程序圖集，保留髮型頂點色，近似賽璐珞陰影和描邊。這些不是 Unity Editor／真機截圖，不能代替 shader 編譯、透明材質排序與真機視覺驗收。

| 角色 | 正面 | 3/4 側 | 正側面 | 標尺最大誤差 |
|---|---|---|---|---|
| 劍士 | [圖](swordsman_front.png) | [圖](swordsman_quarter.png) | [圖](swordsman_profile.png) | 0.000500 |
| 獵人 | [圖](archer_front.png) | [圖](archer_quarter.png) | [圖](archer_profile.png) | 0.000500 |
| 法師 | [圖](mage_front.png) | [圖](mage_quarter.png) | [圖](mage_profile.png) | 0.003558 |
| 刺客 | [圖](assassin_front.png) | [圖](assassin_quarter.png) | [圖](assassin_profile.png) | 0.002872 |
| 牧師 | [圖](priest_front.png) | [圖](priest_quarter.png) | [圖](priest_profile.png) | 0.001458 |
| 聖騎士 | [圖](paladin_front.png) | [圖](paladin_quarter.png) | [圖](paladin_profile.png) | 0.000500 |
| 狂戰士 | [圖](berserker_front.png) | [圖](berserker_quarter.png) | [圖](berserker_profile.png) | 0.006371 |
| 神射手 | [圖](marksman_front.png) | [圖](marksman_quarter.png) | [圖](marksman_profile.png) | 0.000500 |
| 遊俠 | [圖](ranger_front.png) | [圖](ranger_quarter.png) | [圖](ranger_profile.png) | 0.004715 |
| 大魔導士 | [圖](archmage_front.png) | [圖](archmage_quarter.png) | [圖](archmage_profile.png) | 0.002718 |
| 召喚師 | [圖](summoner_front.png) | [圖](summoner_quarter.png) | [圖](summoner_profile.png) | 0.003270 |
| 忍者 | [圖](ninja_front.png) | [圖](ninja_quarter.png) | [圖](ninja_profile.png) | 0.000500 |
| 影舞者 | [圖](shadowdancer_front.png) | [圖](shadowdancer_quarter.png) | [圖](shadowdancer_profile.png) | 0.004964 |
| 主教 | [圖](bishop_front.png) | [圖](bishop_quarter.png) | [圖](bishop_profile.png) | 0.001821 |
| 審判者 | [圖](inquisitor_front.png) | [圖](inquisitor_quarter.png) | [圖](inquisitor_profile.png) | 0.005924 |
| 老焰 | [圖](npc0_front.png) | [圖](npc0_quarter.png) | [圖](npc0_profile.png) | 0.004719 |
| 小鐵 | [圖](npc1_front.png) | [圖](npc1_quarter.png) | [圖](npc1_profile.png) | 0.005760 |
| 米菈 | [圖](npc2_front.png) | [圖](npc2_quarter.png) | [圖](npc2_profile.png) | 0.004633 |
| 翠 | [圖](npc3_front.png) | [圖](npc3_quarter.png) | [圖](npc3_profile.png) | 0.000500 |
| 楓 | [圖](npc4_front.png) | [圖](npc4_quarter.png) | [圖](npc4_profile.png) | 0.001744 |
| 小葉 | [圖](npc5_front.png) | [圖](npc5_quarter.png) | [圖](npc5_profile.png) | 0.004463 |
| 海斗 | [圖](npc6_front.png) | [圖](npc6_quarter.png) | [圖](npc6_profile.png) | 0.003688 |
| 凪 | [圖](npc7_front.png) | [圖](npc7_quarter.png) | [圖](npc7_profile.png) | 0.001458 |
| 阿潮 | [圖](npc8_front.png) | [圖](npc8_quarter.png) | [圖](npc8_profile.png) | 0.003671 |
| 鐵匠 | [圖](npc9_front.png) | [圖](npc9_quarter.png) | [圖](npc9_profile.png) | 0.000500 |
| 店員 | [圖](npc10_front.png) | [圖](npc10_quarter.png) | [圖](npc10_profile.png) | 0.003689 |
| 公會長 | [圖](npc11_front.png) | [圖](npc11_quarter.png) | [圖](npc11_profile.png) | 0.000500 |

共 83 張 PNG，全部低於 200 KB；三視圖每張 600×760。最大圖片 167.9 KB。
頭頂／下巴指皮膚頭模，不含帽子、頭髮、鬍鬚；耳上緣對眉線、耳下緣對鼻底。角色的眉斜度與不對稱保留，標尺的三維誤差均小於 0.01。

參考來源：

- [Belle／Wise 官方角色介紹](https://zzz.mihoyo.com/news/102335)
- [Anby 官方展示](https://www.youtube.com/watch?v=Y2HTc9JZvwc)
- [Nicole 官方展示](https://www.youtube.com/watch?v=BA5eUSejUiQ)
- [Ellen 官方展示](https://www.youtube.com/watch?v=iOW8XhUce38)

參考用圖片未作為專案素材。遊戲只使用程序網格及程式生成像素，不讀取本資料夾。
