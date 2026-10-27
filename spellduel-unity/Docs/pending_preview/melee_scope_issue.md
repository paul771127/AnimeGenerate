# 範圍外問題：副本道具近戰選敵與隊友轉送

狀態：待確認是否允許修改 `Runtime/Combat.cs`。本次沒有修改該檔。

`CODEX_PENDING_BRIEF.md` 第 10 項原文：「這一項例外，可以改 `Combat.RoomEvents.cs` 和 `SoloBattle.RoomEvents.cs`」。以下入口在 `Combat.cs`，因此另行詢問，不擴大到一般戰鬥。

## 已重現

以 `RoomCoopAudit.Make(2, "totems")` 的實際 Battle 來源建立房間，在外部 .NET 契約環境呼叫原近戰／直接攻擊方法：

1. 將 player.head 放在圖騰 head 後方 0.6m、朝向圖騰，以 `na_sword` 呼叫 `MeleeSweep`。結果圖騰 hp 未減；`MeleeFoes()` 沒有列出 NPC。
2. 以同房間設定 `remoteEnemy=true`，訂閱 `OnMinionHit`，呼叫 `HitFoe` 攻擊圖騰。結果轉送次數為 0，本機 hp 反而減少。該轉送分支僅檢查 `v.isMinion`。

這是現有入口的缺漏，也會影響新增護送路障；上述 146 項房間規則測試不涵蓋這兩個成功命中案例。

## 擬採最小修正

- `MeleeFoes()` 僅補入存活、敵對、`npcId` 以 `ev_` 開頭的副本道具，不變更其他 NPC 的敵我規則。
- `HitFoe`／`MeleeHit` 以及對應直接劍技、燃燒區域的遠端命中分支，讓同類道具沿既有 `OnMinionHit`／`mhit` 通知房主；不在客戶端扣 hp。
- 使用既有 `ApplyHitOnMinion` 房主入口，無須新訊息欄位；保留解謎雕像／寶箱在 `EvPropHit` 的特殊判定。
- 補測房主近戰成功、隊友只轉送不本機扣血、房主收訊後扣血／判題、友方守護鐘不成為攻擊目標，以及原有小怪路徑不變。
