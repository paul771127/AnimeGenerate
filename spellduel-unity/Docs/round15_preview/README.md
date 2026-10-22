# 第十五輪：裝備外觀預覽

對應 [第十五輪結果](../../FACE_3D_BRIEF.md)。下列皆為實際程序網格的離線投影，非原生 Unity／AR 截圖。

|基本職業|五欄 × 五稀有度（局部特寫）|七套整套（每組正面／背面）|
|---|---|---|
|劍士|[稀有度階梯](swordsman_rarities.png)|[七套整套](swordsman_sets.png)|
|獵人|[稀有度階梯](archer_rarities.png)|[七套整套](archer_sets.png)|
|法師|[稀有度階梯](mage_rarities.png)|[七套整套](mage_sets.png)|
|刺客|[稀有度階梯](assassin_rarities.png)|[七套整套](assassin_sets.png)|
|牧師|[稀有度階梯](priest_rarities.png)|[七套整套](priest_sets.png)|

混搭：[聖騎士](paladin_mixed.png)、[狂戰士](berserker_mixed.png)、[神射手](marksman_mixed.png)。

動作：[攻擊](berserker_action.png)、[施法](archmage_action.png)、[走路](shadowdancer_action.png)。

量測與檢查：

- [各職業預算](budget.json)、[570 種配置離線測試](offline_equipment_audit.json)。
- [Editor 檢查碼的離線執行](editor_audit_offline.json)；原生 Unity 執行會另存 `unity_equipment_audit.json`。
- [表面間距／快取／四件套／隱藏換裝](geometry_audit.json)。
- [延後刪除與隱藏生命週期模擬](deferred_audit.json)。

胸甲、護脛與徽記都是有對應構件的程序網格。所有部件依現有關節移動；不換武器形狀，不增加強化發光。網格、材質還原及預算檢查通過；目前沒有 Unity Editor／手機 AR 原生驗證，完整限制見結果文件。
