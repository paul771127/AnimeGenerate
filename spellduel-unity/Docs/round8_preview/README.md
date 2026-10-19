# 第八輪：頭身、性格表情與站姿

[頭部／全身並排](head_lineup.png) · [頭身量測](head_ratio.json) · [六種表情](expressions.png) · [待機／戰鬥站姿](poses.png) · [29 人頭像](portraits.png)

27 個 3D 角色（15 職業＋12 居民），另有艾琳／澪兩位只出現在對話頭像。共 54 張單人站姿圖＋4 張總覽，共 58 張 PNG，均小於 200 KB。圖取自實際 C# 程序網格與當時動畫狀態，由 Unity API 測試替身匯出後軟體投影，並非 Unity Editor 或手機 AR 截圖。

## 量測定義

`headHeight` 是下巴至最高髮頂（含頭頂髮束，不含垂到下巴以下的髮尾）；帽子、光環、武器不列入。`headWidth` 是裸頭正面投影寬，`hairWidthIncludingSideLocks` 另外記錄髮型外擴寬，保留雙馬尾等不同剪影。`measuredHeight` 是腳底至髮頂；`nominalHeight` 是原設計身高，沒有拿高帽或法杖湊頭身比。單位為角色根節點局部座標。量測取職業切到待機 0.5 秒、居民待機 1 秒。

成人 6.62～6.94 頭身，小葉 4.99 頭身。成人頭高相對平均最大差 4.12%，裸頭寬最大差 0.42%。頭部上排以同一物理尺度、下巴對齊，黃／青虛線是成人平均髮頂／下巴參考；下排保留實際身高差。

## 表情與待機

嘴寬固定為兩眼瞳距的 0.65 倍；施法張口最高為嘴寬 0.6 倍，露出上排牙。`expressions.png` 每列依序為預設、微表情加旁看、戰鬥、施法、受擊、倒地。居民的戰鬥／施法欄僅示範共用表情控制器，城鎮居民沒有新增戰鬥行為。

眨眼間隔 2～6 秒，旁看間隔 8～15 秒並於 1.6 秒內回來；微表情約 1.7 秒，身體小動作間隔 6～12 秒。這些是角色各自的種子時序，沒有使用戰鬥的隨機來源。口型、眼神材質與眉眼幾何均在建立時快取，不逐幀重建。完整對照見 [personality.json](personality.json)。

## 接線與驗證

新增唯一公開介面 `CharacterRig.InCombat`，預設 `true`。設定 `false` 會在 0.4 秒內轉為待機，即使角色正在城鎮走路也尊重這個值；原有走路、蓄力、防禦、施法、受擊與倒地動畫仍疊加。何時切換由 Claude 接線，本次沒有修改戰鬥或畫面流程。居民永遠只用待機，圖右格因此標示「延續待機」。

C# 測試替身通過貼地、頭部下降不超過 8%、0.4 秒切換、0.3 秒受擊閉眼、顯隱／施法／受擊／倒地／恢復、1,000 幀零受控配置，以及全部角色 2× 三角形上限。最高約 1.611×。Boss 頭像逐像素比對不變；原服裝、髮型、帽飾、臉部光照程式與 CharacterRig 原有公開方法本體保持。見 [verification.json](verification.json)、[metrics.json](metrics.json)。

居民只有固定胸部飾物合批；頭、眉眼、手臂與腿保留樞紐。眉毛與眼皮各自合併線段，減少 Renderer；工具跟隨手部，老焰拐杖保持直立貼地。軟體測試另依 [Unity 2022.3 LookRotation 文件](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Quaternion.LookRotation.html) 補正平行 up／forward 的替身回退行為，以免把垂直道具畫歪。

本機沒有 Unity Editor，因此未驗證引擎／IL2CPP／Metal／GLES3／Vulkan 編譯、Unity Profiler 或 AR 真機幀率；需後續手機建置驗收。較早的 `outfit_preview` 是第七輪服裝歷史對照，最新姿勢與頭身以本目錄為準。

|角色|待機|戰鬥（居民沿用待機）|頭身比|
|---|---|---|---:|
|劍士|[圖](swordsman_idle.png)|[圖](swordsman_combat.png)|6.942|
|獵人|[圖](archer_idle.png)|[圖](archer_combat.png)|6.668|
|法師|[圖](mage_idle.png)|[圖](mage_combat.png)|6.775|
|刺客|[圖](assassin_idle.png)|[圖](assassin_combat.png)|6.732|
|牧師|[圖](priest_idle.png)|[圖](priest_combat.png)|6.732|
|聖騎士|[圖](paladin_idle.png)|[圖](paladin_combat.png)|6.801|
|狂戰士|[圖](berserker_idle.png)|[圖](berserker_combat.png)|6.815|
|神射手|[圖](marksman_idle.png)|[圖](marksman_combat.png)|6.784|
|遊俠|[圖](ranger_idle.png)|[圖](ranger_combat.png)|6.720|
|大魔導士|[圖](archmage_idle.png)|[圖](archmage_combat.png)|6.768|
|召喚師|[圖](summoner_idle.png)|[圖](summoner_combat.png)|6.802|
|忍者|[圖](ninja_idle.png)|[圖](ninja_combat.png)|6.760|
|影舞者|[圖](shadowdancer_idle.png)|[圖](shadowdancer_combat.png)|6.760|
|主教|[圖](bishop_idle.png)|[圖](bishop_combat.png)|6.731|
|審判者|[圖](inquisitor_idle.png)|[圖](inquisitor_combat.png)|6.780|
|老焰|[圖](npc0_idle.png)|[圖](npc0_combat.png)|6.743|
|小鐵|[圖](npc1_idle.png)|[圖](npc1_combat.png)|6.623|
|米菈|[圖](npc2_idle.png)|[圖](npc2_combat.png)|6.772|
|翠|[圖](npc3_idle.png)|[圖](npc3_combat.png)|6.756|
|楓|[圖](npc4_idle.png)|[圖](npc4_combat.png)|6.746|
|小葉|[圖](npc5_idle.png)|[圖](npc5_combat.png)|4.988|
|海斗|[圖](npc6_idle.png)|[圖](npc6_combat.png)|6.747|
|凪|[圖](npc7_idle.png)|[圖](npc7_combat.png)|6.760|
|阿潮|[圖](npc8_idle.png)|[圖](npc8_combat.png)|6.785|
|鐵匠|[圖](npc9_idle.png)|[圖](npc9_combat.png)|6.765|
|店員|[圖](npc10_idle.png)|[圖](npc10_combat.png)|6.748|
|公會長|[圖](npc11_idle.png)|[圖](npc11_combat.png)|6.732|
