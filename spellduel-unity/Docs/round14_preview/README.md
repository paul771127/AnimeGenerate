# 第十四輪預覽

基準 `68f7e9f`；對應 [第十四輪結果](../../FACE_3D_BRIEF.md)。

3D 是真實程序網格的離線軟體投影，非 Unity／AR 截圖；眼睛貼圖與 2D 頭像由實際 C# 美術程式輸出。原圖來源位於 `../story_video/ref/`，影片影格位於 `../story_video/frames/`，均未更動。腳底、名義身高與正面方向對齊，無非等比拉伸。

|角色|三欄全身|臉部|剪影重疊|原圖量測點|
|---|---|---|---|---|
|艾琳|[原圖／十三／十四](Erin_comparison.png)|[特寫](Erin_face.png)|[IoU](Erin_silhouette.png)|[標記](Erin_landmarks.png)|
|凪|[原圖／十三／十四](Nagi_comparison.png)|[特寫](Nagi_face.png)|[IoU](Nagi_silhouette.png)|[標記](Nagi_landmarks.png)|
|老焰|[原圖／十三／十四](Laoyan_comparison.png)|[特寫](Laoyan_face.png)|[IoU](Laoyan_silhouette.png)|[標記](Laoyan_landmarks.png)|
|海斗|[原圖／十三／十四](Haido_comparison.png)|[特寫](Haido_face.png)|[IoU](Haido_silhouette.png)|[標記](Haido_landmarks.png)|
|小鐵|[原圖／十三／十四](Xiaotie_comparison.png)|[特寫](Xiaotie_face.png)|[IoU](Xiaotie_silhouette.png)|[標記](Xiaotie_landmarks.png)|
|翠|[原圖／十三／十四](Cui_comparison.png)|[特寫](Cui_face.png)|[IoU](Cui_silhouette.png)|[標記](Cui_landmarks.png)|
|小葉|[原圖／十三／十四](Xiaoye_comparison.png)|[特寫](Xiaoye_face.png)|[IoU](Xiaoye_silhouette.png)|[標記](Xiaoye_landmarks.png)|

- [十五職業肩線](player_shoulders.png)：每格左為十三輪、右為十四輪；頸根→肩峰標線。
- 攻擊：[劍士](swordsman_attack.png)、[獵人](archer_attack.png)、[法師](mage_attack.png)。
- [八位角色 2D 頭像](portraits.png)、[一般居民與人形小怪](other_humanoids.png)。
- [原圖／3D 比對量測](comparison_metrics.json)、[所有人形量測](model_measurements.json)、[預算](budget.json)。
- [離線手腕取樣](offline_motion_audit.json)、[十九小怪動作 smoke](minion_smoke.json)。

IoU 六位提高；翠仍下降，未達該角色改善目標。去背採邊界連通灰背景法，包含道具與原待機姿勢，淺色髮絲也可能受背景閾值影響。肩寬、斜角與頭身的量測定義、手工標記誤差，以及已知差距均列於結果文件。

Unity 補驗：開啟 `SpellDuel/Art/Validate Round 14 wrists`，會另外產生 `unity_motion_audit.json`。目前提交的是離線替身執行的結果，兩者不能混稱。本次沒有 Unity Editor／手機 AR 的原生驗證。
