# 第五、六輪：人體、手型、帽飾與服裝預覽

[全員彩色並排](all_color.png) · [全員剪影並排](all_silhouettes.png) · [頭像總覽](portraits.png)

[手型](hands.png) · [外衣下的人體三視圖](body_turntable.png) · [帽飾正側面](hats.png) · [新舊肩線](shoulders.png)

15 個職業＋12 位居民，各有正面、3/4、背面，共 81 張；另有 7 張總覽／特寫，共 88 張 PNG。全部以同一正交相機比例呈現，兒童保留原身高。所有圖檔皆小於 200 KB。

全身與特寫取自實際 C# 程序網格、頂點色及平滑法線，經 Unity API 測試替身匯出後作軟體投影。材質、光照與描邊為近似，這不是 Unity Editor 或手機截圖。`portraits.png` 則直接使用實際 `Portraits.Render()` 的 29 人像素。

剪影上排包含裝備，下排去除職業武器以檢查服裝輪廓；居民保留生活物件。人體三視圖去除外衣，保留褲子／鞋，展示兩種體型的肩部、軀幹和手臂。帽飾圖固定在頭部局部軸，避免角色歪頭影響貼合判讀。

構件清單與計數見 [第五、六輪結果](../../FACE_3D_BRIEF.md#第五六輪結果)、[components.json](components.json)、[metrics.json](metrics.json)、[hat_fitting.json](hat_fitting.json)。未戴帽的原始髮束網格保留；戴帽時僅壓低帽內髮頂，帽外／帽緣下髮束保持原風格。

|角色|正面|3/4|背面|
|---|---|---|---|
|劍士|[圖](swordsman_front.png)|[圖](swordsman_quarter.png)|[圖](swordsman_back.png)|
|獵人|[圖](archer_front.png)|[圖](archer_quarter.png)|[圖](archer_back.png)|
|法師|[圖](mage_front.png)|[圖](mage_quarter.png)|[圖](mage_back.png)|
|刺客|[圖](assassin_front.png)|[圖](assassin_quarter.png)|[圖](assassin_back.png)|
|牧師|[圖](priest_front.png)|[圖](priest_quarter.png)|[圖](priest_back.png)|
|聖騎士|[圖](paladin_front.png)|[圖](paladin_quarter.png)|[圖](paladin_back.png)|
|狂戰士|[圖](berserker_front.png)|[圖](berserker_quarter.png)|[圖](berserker_back.png)|
|神射手|[圖](marksman_front.png)|[圖](marksman_quarter.png)|[圖](marksman_back.png)|
|遊俠|[圖](ranger_front.png)|[圖](ranger_quarter.png)|[圖](ranger_back.png)|
|大魔導士|[圖](archmage_front.png)|[圖](archmage_quarter.png)|[圖](archmage_back.png)|
|召喚師|[圖](summoner_front.png)|[圖](summoner_quarter.png)|[圖](summoner_back.png)|
|忍者|[圖](ninja_front.png)|[圖](ninja_quarter.png)|[圖](ninja_back.png)|
|影舞者|[圖](shadowdancer_front.png)|[圖](shadowdancer_quarter.png)|[圖](shadowdancer_back.png)|
|主教|[圖](bishop_front.png)|[圖](bishop_quarter.png)|[圖](bishop_back.png)|
|審判者|[圖](inquisitor_front.png)|[圖](inquisitor_quarter.png)|[圖](inquisitor_back.png)|
|老焰|[圖](npc0_front.png)|[圖](npc0_quarter.png)|[圖](npc0_back.png)|
|小鐵|[圖](npc1_front.png)|[圖](npc1_quarter.png)|[圖](npc1_back.png)|
|米菈|[圖](npc2_front.png)|[圖](npc2_quarter.png)|[圖](npc2_back.png)|
|翠|[圖](npc3_front.png)|[圖](npc3_quarter.png)|[圖](npc3_back.png)|
|楓|[圖](npc4_front.png)|[圖](npc4_quarter.png)|[圖](npc4_back.png)|
|小葉|[圖](npc5_front.png)|[圖](npc5_quarter.png)|[圖](npc5_back.png)|
|海斗|[圖](npc6_front.png)|[圖](npc6_quarter.png)|[圖](npc6_back.png)|
|凪|[圖](npc7_front.png)|[圖](npc7_quarter.png)|[圖](npc7_back.png)|
|阿潮|[圖](npc8_front.png)|[圖](npc8_quarter.png)|[圖](npc8_back.png)|
|鐵匠|[圖](npc9_front.png)|[圖](npc9_quarter.png)|[圖](npc9_back.png)|
|店員|[圖](npc10_front.png)|[圖](npc10_quarter.png)|[圖](npc10_back.png)|
|公會長|[圖](npc11_front.png)|[圖](npc11_quarter.png)|[圖](npc11_back.png)|
