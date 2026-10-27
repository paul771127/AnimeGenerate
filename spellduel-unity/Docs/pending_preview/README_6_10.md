# 第 6～10 項原生預覽與驗證

以下圖片由 Unity 2022.3 的 `Snapshot.Render` 直接渲染；`overview.jpg` 只是個別原生 PNG 的縮排總覽。不是手機 AR 實拍，沒有量測實機 FPS。提交與 Actions 來源見 `environment_native_source.json`。

| 目錄 | 內容 |
|---|---|
| [nature](nature/overview.jpg) | Lava／Roots／Spore／Tide／Whirl，各收起、預警、啟動三態；`nature_audit.json` 檢查有限頂點、3000 三角上限及收起隱藏 |
| [rooms](rooms/overview.jpg) | 鐘、圖騰、一般／雙屬性像、寶箱、路障、符文、門、出口、屬性球、崩塌、紅眼；`room_coop_audit.json` 是房間規則測試 |
| [scenes](scenes/overview.jpg) | 五主題 × 三變化；變化 0 為既有場景，1／2 是本次新增 |
| [weather](weather/overview.jpg) | 林間遺跡內的雨、霧、月、風；粒子預先模擬 8 秒 |

## 重跑

使用專案指定的 Unity 版本：

```text
Unity -batchmode -projectPath spellduel-unity -executeMethod SpellDuel.EditorTools.RoomCoopAudit.Run -quit -logFile room-coop.log
Unity -batchmode -projectPath spellduel-unity -executeMethod SpellDuel.EditorTools.Snapshot.Render -quit -logFile snapshots.log
python3 spellduel-unity/Tools/check_dungeons.py
```

`RoomCoopAudit` 對實際 Battle 執行 1、2、3 人房間：成功、守護／護送失敗、5 秒連結重生、雙元素 3 秒窗口、錯題懲罰、指定觀看者、分段線索、路障、共同視野、聚集崩塌、死亡清理後重新 Spawn 及遠端不自行判定。測試不替代多支手機的連線與 AR 實測。

只有 `rev.ks[2..4]`、`reve.id/ok` 增加房間資料的使用方式；沒有新增訊息類型或 Msg 欄位。部署多人版時，所有參與手機需使用同版。

詳細重現與修正範圍：[副本道具近戰／遠端命中](melee_scope_issue.md)。

## 另行確認中的既有問題

完整近戰與隊友命中入口的外部重現發現：`Combat.cs` 的 `MeleeFoes()` 未列入 `ev_` 道具，因此劍士普攻選不到圖騰與新路障；部分直接攻擊的遠端轉送只檢查 `isMinion`，沒有涵蓋道具 NPC。這兩項不在上述 146 項房間規則成功結果的涵蓋範圍。第 10 項僅明列允許兩個 RoomEvents 檔，已提出最小擴充修改範圍的確認，尚未修改 `Combat.cs`。
