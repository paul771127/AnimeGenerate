# 第十二輪：影片角色對齊（文字設定版）

開工 pull 與提交前 fetch 均確認：遠端 `Docs/story_video/ref/`、`frames/` **尚未提交任何圖片**。依需求文件明文允許，先用 `shots.json.characters` 與角色表實作。本目錄左欄是文字設定與色票，沒有假冒的定裝圖或影片截圖；待影片電腦提交圖片後才能逐圖校準相似度。

| 角色 | 文字設定／2D／3D 對照 |
|---|---|
| 艾琳 | [銀紫及肩髮、紫眼、金邊紫連帽長衣、書卷皮袋與紫寶珠木杖](Erin_comparison.png) |
| 老焰 | [白髮、長白鬍、紅工作長袍、木杖](Laoyan_comparison.png) |
| 小鐵 | [棕刺髮、紅頭巾、橘護目鏡、煤灰、棕圍裙](Xiaotie_comparison.png) |
| 翠 | [綠白長髮、精靈耳、綠袍與葉飾](Cui_comparison.png) |
| 小葉 | [綠雙馬尾、黃衣、木故事牌](Xiaoye_comparison.png) |
| 海斗 | [深藍短髮、白船長帽、藍海軍外套與金船錨](Haido_comparison.png) |
| 凪 | [酒紅短髮、粉紅洋裝、白圍裙、金耳環與提燈](Nagi_comparison.png) |
| 澪 | [淡藍長髮、半透明、柔光輪廓；文件只要求頭像](Mio_comparison.png) |
| 熔岩巨像 | [黑玄武岩、橘熔岩、黑晶](boss0_comparison.png) |
| 腐化樹王 | [扭曲樹皮、紫孢子、黑晶心臟](boss1_comparison.png) |
| 深淵海妖 | [深青綠、青符文、觸手與刻名黑晶](boss2_comparison.png) |

已有符合設定的髮型與配件保留；沒有重設玩家職業外觀、居民位置或遊戲規則。比例沿用 ART_STYLE.md 最後第八輪的有效規則：成人6.5～7頭身、兒童約5頭身；沒有因第十二輪摘要中的「二頭身」字樣重新縮放已驗收骨架。

`metrics.json` 為第十二輪開始前後的離線同條件比較。七位居民最高約1.216倍三角形、1.231倍 MeshRenderer；海妖增加3個刻字 MeshRenderer。Boss 數字不含粒子替身，海妖兩版本都含8個離線烘焙觸手代理，請看相對差異。

`verification.json` 記錄語法、離線 Unity API 合約編譯、七居民腳底／有限頂點／合法索引、三 Boss 建立／預備／死亡／復原、15職業資料及動作方法保護檢查。`reference_status.json` 記錄缺圖狀態。

**預覽是實際程序網格／像素的軟體投影，不是 Unity／手機截圖。** 基本球／圓柱以低面數同形網格近似，觸手依原骨骼與 bind pose 在 CPU 烘焙；軟體預覽省略粒子與原生陰影，因此紫孢子需在 Unity 檢查。此環境沒有 Unity Editor，尚未驗證原生 shader、iOS／Android 或 AR 實機。所有 PNG 僅在 Docs，Assets 仍完全程式產生。
