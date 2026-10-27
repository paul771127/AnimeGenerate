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

## 道具命中補修

使用者已允許最小修改 `Combat.cs`。近戰候選補入活著的敵對 `ev_` 道具，相關隊友攻擊經既有 `mhit` 交房主；一般 NPC 規則不變。補測提升為 338 項，原生 Unity、Android APK 與 iOS Xcode 匯出皆通過，見 [補修驗收紀錄](room_contact_validation.json)；前批美術來源紀錄仍保留。
