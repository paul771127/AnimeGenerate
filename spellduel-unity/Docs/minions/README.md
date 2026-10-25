# 章節範圍敵人：美術接線

| kind | 名稱 | 主題 | 本地高度（約） | 預備／出手 |
|---|---|---|---|---|
| 19 | 古根撼地獸 | forest / 1 | 1.5m（含樹冠） | 抬前腳、苔岩背甲後傾 → 踏地 |
| 20 | 潮鞭鰻 | sea / 2 | 1.8m | 蓄力後仰、帆鰭外張 → 橫甩尾 |
| 21 | 鏈錘鬥奴 | arena / 3 | 2.05m | 舉手過頭、鏈錘加速 → 水平一周 |
| 22 | 裂岩甲蜥 | canyon / 4 | 1.05m，身長含尾約3m | 抬起前身 → 重壓 |

`SetVariant(19..22)` 後照舊 `SetDress(theme)`。骨架面向本地 +Z，原點在地面；維持父物件比例1。`MoveSpeed` 驅動步態，`Windup(1.5f)` 漸入預備並維持，`Attack()` 播放約0.65秒的出手／復位；`PlayHit()` 和 `SetDead(bool)` 沿用。無新增戰鬥欄位、事件、訊息或技能資料。

## 特效呼叫（全部只有外觀）

```csharp
// 19：線性擴散，1秒抵達3.5m；位置是地面，不是身體中心。
MinionRig.GroundRingFx(feet, 3.5f, new Color(.65f,1f,.3f), 1f);
// 20：前方半圓，世界向上胸口高度。
MinionRig.SweepArcFx(feet, forward, 3f, 180f, 1.2f, new Color(.35f,.9f,1f,.7f));
// 21：整圈；方向仍傳入朝向。
MinionRig.SweepArcFx(feet, forward, 3f, 360f, 1.2f, new Color(1f,.68f,.28f,.7f));
// 22：於判定第一圈開始時呼叫一次，0.6秒後由判定端再呼叫一次。
MinionRig.GroundRingFx(feet, 3f, new Color(1f,.74f,.3f), 1f);
```

地波半徑以線性時間0→指定半徑；橫掃弧光以方向為中心、半徑固定、0.45秒淡出。兩者重用 SpellFx 的線段／快取發光材質；沒有 Collider、不自動出怪、不造成傷害、不自動接 Attack，避免和 Claude 判定時序重複。生成時配置點陣列，更新不建立陣列／材質，結束自毀。

## 預覽與驗證

`new_enemies.jpg` 是實際 C# 程序網格、姿勢與材質色的軟體投影（正面／側面／1.5秒預備），不是概念生成圖，也不是手機截圖。Unity Editor 可從 `SpellDuel/Art/Render new area enemies` 以真實 Toon shader 重拍同名檔。編輯器工具只建立臨時預覽物件，不修改場景或遊戲設定。

`validation.json` 記錄 .NET Unity API contract 煙霧檢查：19個舊變體仍可建構；四個新變體六種狀態可執行、頂點有限且三角索引合法、主題裝飾不越界。此檢查不是 Unity Android／iOS 完整編譯；本機沒有 Unity Editor。共用程序網格／材質保留快取；新大型敵人約2200～3500三角、27～35個部位 Renderer（含主題裝飾，出場限一隻由 Claude 接）。
