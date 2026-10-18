# 第二輪臉部比例預覽

27 個角色 × 3 個正交視角。相機沿頭部局部軸看 0°／45°／90°，不把預設歪頭誤當正面。主圖保留實際模型的裝備及遮擋；右下小圖用同一份五官網格去除遮擋，以檢查面罩或鬍鬚下的位置。

虛線是目標比例，白點是從實際共用頭模、眼片、眉線、嘴部及耳廓讀出的標記。三維資料及誤差見 [measurements.json](measurements.json)。目標依 FACE_3D_BRIEF.md，並非宣稱量測自《絕區零》官方模型。

產生方式：實際 C# 原始碼配合 Unity API 測試替身建立網格，以頭部矩陣轉到局部座標，再用軟體光柵器做正交投影；保留網格頂點色並近似人類陰影材質。這不是 Unity Editor／真機截圖，不能代替 shader 編譯與真機視覺驗收。

| 角色 | 正面 | 3/4 側 | 正側面 | 最大誤差 |
|---|---|---|---|---|
| 劍士 | [圖](swordsman_front.png) | [圖](swordsman_quarter.png) | [圖](swordsman_profile.png) | 0.000500 |
| 獵人 | [圖](archer_front.png) | [圖](archer_quarter.png) | [圖](archer_profile.png) | 0.001383 |
| 法師 | [圖](mage_front.png) | [圖](mage_quarter.png) | [圖](mage_profile.png) | 0.003558 |
| 刺客 | [圖](assassin_front.png) | [圖](assassin_quarter.png) | [圖](assassin_profile.png) | 0.002872 |
| 牧師 | [圖](priest_front.png) | [圖](priest_quarter.png) | [圖](priest_profile.png) | 0.002085 |
| 聖騎士 | [圖](paladin_front.png) | [圖](paladin_quarter.png) | [圖](paladin_profile.png) | 0.000500 |
| 狂戰士 | [圖](berserker_front.png) | [圖](berserker_quarter.png) | [圖](berserker_profile.png) | 0.006371 |
| 神射手 | [圖](marksman_front.png) | [圖](marksman_quarter.png) | [圖](marksman_profile.png) | 0.000777 |
| 遊俠 | [圖](ranger_front.png) | [圖](ranger_quarter.png) | [圖](ranger_profile.png) | 0.004715 |
| 大魔導士 | [圖](archmage_front.png) | [圖](archmage_quarter.png) | [圖](archmage_profile.png) | 0.002718 |
| 召喚師 | [圖](summoner_front.png) | [圖](summoner_quarter.png) | [圖](summoner_profile.png) | 0.003270 |
| 忍者 | [圖](ninja_front.png) | [圖](ninja_quarter.png) | [圖](ninja_profile.png) | 0.000780 |
| 影舞者 | [圖](shadowdancer_front.png) | [圖](shadowdancer_quarter.png) | [圖](shadowdancer_profile.png) | 0.004964 |
| 主教 | [圖](bishop_front.png) | [圖](bishop_quarter.png) | [圖](bishop_profile.png) | 0.001879 |
| 審判者 | [圖](inquisitor_front.png) | [圖](inquisitor_quarter.png) | [圖](inquisitor_profile.png) | 0.005924 |
| 老焰 | [圖](npc0_front.png) | [圖](npc0_quarter.png) | [圖](npc0_profile.png) | 0.004719 |
| 小鐵 | [圖](npc1_front.png) | [圖](npc1_quarter.png) | [圖](npc1_profile.png) | 0.005760 |
| 米菈 | [圖](npc2_front.png) | [圖](npc2_quarter.png) | [圖](npc2_profile.png) | 0.004633 |
| 翠 | [圖](npc3_front.png) | [圖](npc3_quarter.png) | [圖](npc3_profile.png) | 0.000500 |
| 楓 | [圖](npc4_front.png) | [圖](npc4_quarter.png) | [圖](npc4_profile.png) | 0.001744 |
| 小葉 | [圖](npc5_front.png) | [圖](npc5_quarter.png) | [圖](npc5_profile.png) | 0.004463 |
| 海斗 | [圖](npc6_front.png) | [圖](npc6_quarter.png) | [圖](npc6_profile.png) | 0.003688 |
| 凪 | [圖](npc7_front.png) | [圖](npc7_quarter.png) | [圖](npc7_profile.png) | 0.002087 |
| 阿潮 | [圖](npc8_front.png) | [圖](npc8_quarter.png) | [圖](npc8_profile.png) | 0.003671 |
| 鐵匠 | [圖](npc9_front.png) | [圖](npc9_quarter.png) | [圖](npc9_profile.png) | 0.000500 |
| 店員 | [圖](npc10_front.png) | [圖](npc10_quarter.png) | [圖](npc10_profile.png) | 0.003689 |
| 公會長 | [圖](npc11_front.png) | [圖](npc11_quarter.png) | [圖](npc11_profile.png) | 0.001382 |

共 81 張 PNG；每張 600×760，最大 25.2 KB（上限 200 KB）。
頭頂／下巴指皮膚頭模，不含帽子、頭髮或鬍鬚。耳上緣對眉線，耳下緣對鼻底。眼角允許 ±6° 眼型偏移；眉線允許個性化斜度與不對稱，所有幾何標記誤差仍低於 0.01。

參考來源：

- [Belle／Wise 官方角色介紹](https://zzz.mihoyo.com/news/102335)
- [Anby 官方展示](https://www.youtube.com/watch?v=Y2HTc9JZvwc)
- [Nicole 官方展示](https://www.youtube.com/watch?v=BA5eUSejUiQ)
- [Ellen 官方展示](https://www.youtube.com/watch?v=iOW8XhUce38)

參考用圖片留在開發暫存，未作為本專案素材。遊戲只使用程序網格與繪製程式，不讀取本資料夾。
