# 第十一輪美術預覽

本目錄圖片由實際程序網格的離線軟體投影產生，**不是 Unity／AR 真機截圖**。不含粒子、原生陰影與景深。Boss 用到的 Unity 基本球／圓柱，以低面數同形旋轉體近似；自行建立的殼、岩層、刻字及服飾網格直接取自程式。原生 shader 編譯、粒子及裝置效能仍需 Unity 驗證。

- [Boss 狀態總覽](boss_states.png)：青銅鬥魂、黃土岩魔各有 idle / windup / strike / dead（跪倒）/ rubble（散甲或崩解）。個別圖為 `boss3_*`、`boss4_*`。
- [小怪主題裝飾](minion_dresses.png)：variant 0、8、14、17；theme3 黑晶項圈與斷鏈、theme4 沙色圍巾。19 種皆有專屬尺寸及掛點，其他主題清除裝飾。
- 城鎮全景： [炎之里](town0.png)、[森之里](town1.png)、[潮騷港](town2.png)、[赤砂城](town3.png)、[風沙驛](town4.png)。包含六位居民、原位置建築、圍欄、大門及傳送陣。
- 各鎮鐵匠／道具店／工會近景為 `town0_smith.png`、`town0_shop.png`、`town0_guild.png` 至 `town4_*`。
- [六位新居民](new_residents.png)：使用既有已接受的擬人臉型模板，新增斷角盔與胸甲、票冊、鍛甲鎚與圍裙、驛站披巾、馬幫鈴、說書卷。原十二位居民與戰鬥角色五官未改。
- `town_metrics.json`：對比第十輪完成時的完整城鎮，含居民、圍欄、大門與傳送陣；頂點及 Renderer 均在 1.5 倍以內。只量美術，不執行 TownWalk 互動。
- `boss_metrics.json`：Boss MeshRenderer 數；另沿用五個粒子系統，離線替身不生成 ParticleSystemRenderer，數字未計入它們。
- `verification.json`：驗證範圍與限制。全 Runtime 語法檢查、相關 C# 合約編譯，兩隻 Boss 原骨架對照與招式／死亡／復原 smoke test，以及 19 種小怪的主題替換／清除測試均通過。

所有功能招牌都是錘、瓶、劍的程序幾何圖示；沒有外部圖檔或模型進入 Assets。
