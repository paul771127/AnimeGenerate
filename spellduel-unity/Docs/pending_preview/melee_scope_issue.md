# 範圍外問題：副本道具近戰選敵與隊友轉送

狀態：使用者已明確允許最小修改 `Runtime/Combat.cs`；上述範圍已修復，原生 Unity 338 項檢查與 Android／iOS Xcode 匯出皆通過。

`CODEX_PENDING_BRIEF.md` 第 10 項原文：「這一項例外，可以改 `Combat.RoomEvents.cs` 和 `SoloBattle.RoomEvents.cs`」。以下入口在 `Combat.cs`，已取得使用者額外授權，僅修正副本道具相關入口。

## 修復前重現

以 `RoomCoopAudit.Make(2, "totems")` 的實際 Battle 來源建立房間，在外部 .NET 契約環境呼叫原近戰／直接攻擊方法：

1. 將 player.head 放在圖騰 head 後方 0.6m、朝向圖騰，以 `na_sword` 呼叫 `MeleeSweep`。結果圖騰 hp 未減；`MeleeFoes()` 沒有列出 NPC。
2. 以同房間設定 `remoteEnemy=true`，訂閱 `OnMinionHit`，呼叫 `HitFoe` 攻擊圖騰。結果轉送次數為 0，本機 hp 反而減少。該轉送分支僅檢查 `v.isMinion`。

這是現有入口的缺漏，也會影響新增護送路障；上述 146 項房間規則測試不涵蓋這兩個成功命中案例。

## 已實作最小修正

- `MeleeFoes()` 僅補入存活、敵對、`npcId` 以 `ev_` 開頭的副本道具，不變更其他 NPC 的敵我規則。
- `HitFoe`／`MeleeHit` 以及對應直接劍技、燃燒區域的遠端命中分支，讓同類道具沿既有 `OnMinionHit`／`mhit` 通知房主；不在客戶端扣 hp。
- 使用既有 `ApplyHitOnMinion` 房主入口，無須新訊息欄位；保留解謎雕像／寶箱在 `EvPropHit` 的特殊判定。
- 補測房主近戰成功、隊友只轉送不本機扣血、房主收訊後扣血／判題、友方守護鐘不成為攻擊目標，以及原有小怪路徑不變。

## 補測結果

`RoomCoopAudit` 在實際 Battle 來源的 .NET 契約環境通過 338 項檢查（原 146 項加 192 項）：單人／2 人／3 人、圖騰／路障的揮砍／刀線／突刺／直接技能／伏擊／燃燒，隊友一次轉送且不本機扣血、房主依轉送 ID 扣血、友方守護物／死亡道具／一般 NPC 選敵不變、小怪原轉送流程不變、雕像與寶箱由房主判斷成功／懲罰，以及毒爆只在房主引爆。

沒有新增訊息種類、欄位或技能數值。`OnDealt` 的毒爆特判也納入相同道具分類，避免轉送後又在客戶端引爆。

## 原生與平台驗收

驗證版本 `8505e98`：[原生 Unity](https://github.com/paul771127/AnimeGenerate/actions/runs/40533047036) 通過 338 項房間與命中檢查；[Android／iOS 建置](https://github.com/paul771127/AnimeGenerate/actions/runs/40533046929) 的 APK、Xcode 匯出成功，APK 已發布。IPA／TestFlight 後續工作在記錄時仍排隊，未列入已通過項目。

完整結果見 [room_contact_validation.json](room_contact_validation.json)，原生摘要見 [room_contact_native_audit.log](room_contact_native_audit.log)。測試涵蓋實際 Battle 的客戶端回呼與房主收訊入口，不代表已用多支手機進行實體連線測試。
