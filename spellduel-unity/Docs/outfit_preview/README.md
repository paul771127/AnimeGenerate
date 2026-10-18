# 第四輪：全身髮型與服飾預覽

[全員彩色並排](all_color.png) · [全員剪影並排](all_silhouettes.png) · [頭髮剪影](../face_preview/hair_silhouettes.png) · [頭像總覽](portraits.png)

15 個職業＋12 位居民。每人正面、3/4 側、背面，固定正交相機比例，保留既有站姿及頭部個性；居民保留原身高縮放。圖片為實際 C# 程序網格經 Unity API 測試替身匯出後的軟體投影，材質、平滑法線光照與描邊均為近似；不是 Unity／真機截圖。

剪影總覽上排包含武器、工具及配件，下排職業去除武器以檢查服裝輪廓，居民保留生活物件。頭髮總覽去除帽飾，只填黑頭模＋髮型；頭像總覽由實際 `Portraits.Render()` 像素產生。

設計說明與限制見 [第四輪結果](../../FACE_3D_BRIEF.md)，實際網格／Renderer 統計見 [metrics.json](metrics.json)。

| 角色 | 正面 | 3/4 側 | 背面 |
|---|---|---|---|
| 劍士 | [圖](swordsman_front.png) | [圖](swordsman_quarter.png) | [圖](swordsman_back.png) |
| 獵人 | [圖](archer_front.png) | [圖](archer_quarter.png) | [圖](archer_back.png) |
| 法師 | [圖](mage_front.png) | [圖](mage_quarter.png) | [圖](mage_back.png) |
| 刺客 | [圖](assassin_front.png) | [圖](assassin_quarter.png) | [圖](assassin_back.png) |
| 牧師 | [圖](priest_front.png) | [圖](priest_quarter.png) | [圖](priest_back.png) |
| 聖騎士 | [圖](paladin_front.png) | [圖](paladin_quarter.png) | [圖](paladin_back.png) |
| 狂戰士 | [圖](berserker_front.png) | [圖](berserker_quarter.png) | [圖](berserker_back.png) |
| 神射手 | [圖](marksman_front.png) | [圖](marksman_quarter.png) | [圖](marksman_back.png) |
| 遊俠 | [圖](ranger_front.png) | [圖](ranger_quarter.png) | [圖](ranger_back.png) |
| 大魔導士 | [圖](archmage_front.png) | [圖](archmage_quarter.png) | [圖](archmage_back.png) |
| 召喚師 | [圖](summoner_front.png) | [圖](summoner_quarter.png) | [圖](summoner_back.png) |
| 忍者 | [圖](ninja_front.png) | [圖](ninja_quarter.png) | [圖](ninja_back.png) |
| 影舞者 | [圖](shadowdancer_front.png) | [圖](shadowdancer_quarter.png) | [圖](shadowdancer_back.png) |
| 主教 | [圖](bishop_front.png) | [圖](bishop_quarter.png) | [圖](bishop_back.png) |
| 審判者 | [圖](inquisitor_front.png) | [圖](inquisitor_quarter.png) | [圖](inquisitor_back.png) |
| 老焰 | [圖](npc0_front.png) | [圖](npc0_quarter.png) | [圖](npc0_back.png) |
| 小鐵 | [圖](npc1_front.png) | [圖](npc1_quarter.png) | [圖](npc1_back.png) |
| 米菈 | [圖](npc2_front.png) | [圖](npc2_quarter.png) | [圖](npc2_back.png) |
| 翠 | [圖](npc3_front.png) | [圖](npc3_quarter.png) | [圖](npc3_back.png) |
| 楓 | [圖](npc4_front.png) | [圖](npc4_quarter.png) | [圖](npc4_back.png) |
| 小葉 | [圖](npc5_front.png) | [圖](npc5_quarter.png) | [圖](npc5_back.png) |
| 海斗 | [圖](npc6_front.png) | [圖](npc6_quarter.png) | [圖](npc6_back.png) |
| 凪 | [圖](npc7_front.png) | [圖](npc7_quarter.png) | [圖](npc7_back.png) |
| 阿潮 | [圖](npc8_front.png) | [圖](npc8_quarter.png) | [圖](npc8_back.png) |
| 鐵匠 | [圖](npc9_front.png) | [圖](npc9_quarter.png) | [圖](npc9_back.png) |
| 店員 | [圖](npc10_front.png) | [圖](npc10_quarter.png) | [圖](npc10_back.png) |
| 公會長 | [圖](npc11_front.png) | [圖](npc11_quarter.png) | [圖](npc11_back.png) |

每張三視圖 540×940；全員並排圖可放大檢視。遊戲不讀取本資料夾中的 PNG。眼睛畫法及比例未修改，僅更新頭髮、服裝與衣領。
