# 第七輪：乾淨服裝、圓弧構件與淡粉臉部光影

[全員彩色](all_color.png) · [全員剪影](all_silhouettes.png) · [29 人頭像](portraits.png)

[腰帶下衣襬收合對照](hem_fit.png) · [臉部正面／45°／90° 光照](face_light.png) · [五職業構件引線](component_callouts.png) · [肩／褲／衣襬前後對照](corners_before_after.png)

[手型](hands.png) · [外衣下的人體三視圖](body_turntable.png) · [帽飾](hats.png) · [肩線疊圖](shoulders.png)

15 職業與 12 居民各有正面、3/4、背面，共 81 張；加 11 張總覽／特寫，共 92 張 PNG，全部低於 200 KB。使用相同相機尺度，保留兒童與成人的身高差。

全身、光照與構件圖由實際 C# 程序網格、頂點色、平滑法線及人類臉部公式作軟體投影；光照與描邊仍是近似，並非 Unity／真機截圖。頭像總覽直接使用 `Portraits.Render()` 輸出。

`face_light.png` 固定同一頭模與相機，只改主光水平角度；無髮版用於完整看見皮膚，瀏海柔影公式仍啟用。正面與 45° 不出現側頰陰影，90° 才沿顴骨／下顎柔化。Shader 與 `HumanFaceLight` 數值公式相同，C#／Python 2,646 取樣最大差約 0.00000053。

構件引線對應 [components.json](components.json) 中五基本職業的 `callouts`，左右同名構件以鄰近一側標示；皮膚、眼睛、頭髮沿用，陰影是同一布料的法線光照，不是額外色塊。其餘職業／居民沿用同一套衣服構件產生器，生活物件仍為背包、藥袋、釣竿、故事板、鐵鎚、鐘扣等。

衣襬已追加收窄：短衣襬向下略微內收，長衣襬只留少量胯部活動空間，前緣弧度依衣長縮小，避免兩側翹成尖翼。衣襬為連續圓弧殼；前緣淺弧開口可看見同色內側，內側的暗色是布料背光。只在劍士／聖騎士保留弧形護膝，在劍士／聖騎士／狂戰士保留圓弧肩甲。公會長原有兩片大型金肩章刪除，保留鐘形胸扣。

[三角形與 Renderer 計數](metrics.json) · [保留與光照檢查摘要](verification.json) · [帽飾貼合數值](hat_fitting.json) · [第七輪結果](../../FACE_3D_BRIEF.md#第七輪結果)

最大 1.592× 原版，全部低於 2×。本機沒有 Unity Editor；尚未驗證 Unity 2022.3 或 Metal／GLES3／Vulkan 真機結果。較早的 `face_preview` 與 FACE 文件舊輪次屬歷史紀錄；本目錄第七輪圖為目前實作。

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
