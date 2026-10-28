# 天穹飛毯 A2 接線與預覽

這批是可呼叫的程序美術，不會自行產生戰鬥、掉落、碰撞、彈幕排程或更改 dungeons.txt。新類別都在 `Assets/SpellDuel/Runtime/`。使用者定案的二階最高12枚由判定端控制；SkyShotView不設數量上限。

## 座標與生命週期

所有 Vector3 入口採 Unity 場景座標（AR session space）。若手上是 Battle/WorldFrame 的邏輯座標，呼叫前用 WorldFrame.FromWorld／DirFromWorld 轉換；同一組參數不要混用座標系。新敵人腳底附近為根原點、本地+Z向前。新飛艇根是固定航行錨點、本地+Z朝船首。

- `CarpetScene.Create(List<Vector3>, floorY, forward)`：接受簡單、非自交多邊形；可處理凹形，forward忽略垂直分量。自行建立五站快取景物，`SetVariant(0..4)`只切預建組。每站30個移動塊、近中遠移速1／0.35／0.02；塊內依材質合批。
- `Speed` 預設12m/s，只影響景物和風線，不能用它推算判定抵達時間。`SwayScale` 預設1，0時取消布面／背景晃動與下沉，不移動玩家相機。
- `Impact(worldPoint, strength)` 顯示布面局部下沉、波紋及塵土。手機震動由邏輯端呼叫。
- 額外 `SurfacePoint(worldPoint)` 把**未晃動**的場景座標轉為貼布面的顯示位置。只給預警／光環使用，禁止回寫戰鬥坐標。預警與載具內已使用；同時只應有一張啟用的飛毯。
- `CarpetObstacleView.Create(kind, station)`／`Tick(...)` 按任務單的參數順序。`frontCenter`是未晃動的飛毯前緣中心，`side`沿forward的左右；`gapCenter`／`gapWidth`單位公尺。
- **新增 `TravelSeconds`（預設6）與 `WarningLeadSeconds`（預設1.8，下限1.2）**：原Tick只有progress，無法由它知道剩餘秒數。呼叫端必須提供實際飛行秒數；若不是線性進度，請把progress映射成剩餘時間比例再送入。不能僅因預設值存在就假設預警合格。
- `Hit()`做局部撞擊後掠過；`Passed()`只掠過；`Release()`停用，呼叫端可回收。重新啟用後再Tick可重用。最後不再需要時Destroy根物件。

## 敵人與載具

`SkyEnemyRig.Create(0..4)`依序為風箏盜賊、雲鷹騎兵、滑翔翼兵、浮游魔偶、空賊隊長，提供MoveSpeed／Windup／Attack／PlayHit／SetDead。獨立骨架，不借MinionRig。各rig只動畫，不自行移動根座標或判定傷害。

`SkyVehicleView.Create(kind)`與敵人同一kind；`Tick(progress, from, landPoint)`的0～.35接近、.35～.55並排、.55～.80起跳／落地、.80～1離場。落點圈在.55～.80顯示；這份時間表應使用4秒總時長，才能有完整1秒預警。敵人本體的弧線、落地開始戰鬥和載具回收仍由Claude處理。浮游魔偶的三葉環是自己的飛行結構。

## 飛艇與砲彈

`AirshipRig.Create()`、`SetPhase(1/2)`、`Warn(move,dur)`、`Fire(move)`、`SetSide(-1/+1)`、`SetGap(x)`、`SetSwoop(0..1)`、`DropPirate(side)`、`SetDying(0..1)`皆已提供。

- move 0低砲、1高雷索、2半邊、3缺口、4彈幕。SetGap為**船身本地X**公尺；船頭面向玩家時，船的左右與玩家可能相反，判定端須轉換。
- `SetSwoop(1)`把內部船體沿本地+Z推近8m、下降2m，不覆寫根座標。近戰可命中的錨台在船體本地(0,-.7,4.1)。例如根放在前緣向前12.1m、向上2.7m、船首反向面對玩家時，Swoop=1的錨台落到前緣附近。命中部位要跟實際錨台對齊，不能只打根座標。
- **新增 `SetPirateLanding(side, worldPoint)`**：DropPirate前先提供真正落點，甲板替身飛向該處、落前1秒圈跟著布面晃動。未設定只有飛毯中心／側邊的美術預設，不代表安全或合法生成點。真正戰鬥單位由Claude接續，不能把甲板替身當第二隻怪。
- `SkyShotView.Create(bool low)`低位橘雙尾、高位藍雙尾；方向沿本地+Z，位置／旋轉／傷害／回收由呼叫端控制。
- `Sample`方法只供預覽和測試，遊戲Update已會驅動；不要同時手動Sample與自動Update，避免動畫兩倍速。

## 預覽與驗證

`carpet_station_*`為五站；`carpet_enemy_*`包含待機與windup；`carpet_vehicle_*`為五載具；`carpet_boss*`為兩階段；另有障礙、落地與高低彈。圖片由Unity Snapshot產生，最終來源、SHA-256和原生／Android／iOS結果會記錄在本目錄的驗證資料。沒有手機AR實拍，也沒有宣稱實機60fps。


## 最終驗收（2026-10-28）

驗證來源 `6a9762828f0e8f3b77a44cf97218e3726340759d`。Unity 原生 Snapshot、Android APK、iOS Xcode 匯出與未簽署 IPA 編譯皆通過；詳見 build_validation.json。原生圖共76張，另附檢查JSON；總覽裁去照片空白後等比例排列，原始PNG保持不變。寵物分頁圖片為明確標示的排版示意，並非手機OnGUI截圖。

目視確認：五站的織毯與主題景物不同、船殼沿正確長軸、氣囊箍貼合、敵人比例無畫幅拉伸、五種載具可辨。障礙圖只呈現接近時刻，完整安全範圍與1.2秒預警須由Claude接實際時序並在AR中驗收。
