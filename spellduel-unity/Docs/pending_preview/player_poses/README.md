# 我方16職業動作

每職業的 `player_pose_{id}_overview.png` 由左至右：**站姿、戰鬥架勢、深蹲、跳躍、蓄力、出手、防禦**。每個狀態另有480×640原生Unity PNG，`native_validation.json` 記錄同一版程式的數值檢查；`build_validation.json` 指向平台與截圖作業。圖像皆為程序模型實際渲染，不是生成概念圖。

## 職業動作重點

| id／職業 | 站姿／架勢 | 蓄力／防禦與空中差異 |
|---|---|---|
| [novice](player_pose_novice_overview.png) 初心者 | 放鬆握原有木棍→雙手扶棍 | 後收棍、前送揮擊；較大的收腿 |
| [swordsman](player_pose_swordsman_overview.png) 劍士 | 單手低持→分腿提劍 | 收右肩轉腰、橫劍護身；中度收腿 |
| [archer](player_pose_archer_overview.png) 獵人 | 低持弓→半拉弓 | 右手跟弦，逐步拉滿、放箭回彈；左右腿錯開 |
| [mage](player_pose_mage_overview.png) 法師 | 持杖、另一手放鬆→聚光掌 | 法杖外傾露臉，雙手前推；較輕的收腿 |
| [assassin](player_pose_assassin_overview.png) 刺客 | 雙短刃收近→低重心 | 腰邊蓄力、交錯護身；深收腿 |
| [priest](player_pose_priest_overview.png) 牧師 | 平穩持聖杖→抬聖徽 | 掌心聚光再前推；緊湊收腿 |
| [paladin](player_pose_paladin_overview.png) 聖騎士 | 盾留身側→前盾持劍 | 盾護身、劍後收；較穩定對稱落地 |
| [berserker](player_pose_berserker_overview.png) 劍仙 | 保留現有劍／劍氣→劍指引勢 | 劍指前送與轉腰；輕盈的不對稱收腿 |
| [marksman](player_pose_marksman_overview.png) 神射手 | 精準持弓→較完整瞄準 | 拉弦更直、收招短促；小幅度左右差 |
| [ranger](player_pose_ranger_overview.png) 遊俠 | 低弓、承鷹手→靈活側身 | 鷹仍依附前臂；收腿幅度與左右差較大 |
| [archmage](player_pose_archmage_overview.png) 大魔導士 | 書與杖分開→較開展聚能 | 高抬杖、掌前推；保留長袍緊湊姿勢 |
| [summoner](player_pose_summoner_overview.png) 召喚師 | 開掌招呼→向側前方引導 | 左掌召引、右杖聚能；不對稱收腿 |
| [ninja](player_pose_ninja_overview.png) 忍者 | 刃收身前→低姿準備 | 交錯手勢、深收腿、肩腰反向蓄力 |
| [shadowdancer](player_pose_shadowdancer_overview.png) 影舞者 | 左右不對稱→扭腰雙刃 | 外開左刃、前送右刃；最大左右腿節奏差 |
| [bishop](player_pose_bishop_overview.png) 主教 | 托書持杖→開展祈禱 | 書不被手機IK舉到頭上；較小對稱收腿 |
| [inquisitor](player_pose_inquisitor_overview.png) 審判者 | 持聖杖→較前傾架勢 | 右肩蓄力、雙臂護身；中度收腿與較強轉腰 |

## 沿用介面與優先序

- 沿用 `InCombat`、`MoveSpeed`、`DrivePhone`、`Charge`、`Guarding`、`PlayAttack`（全部既有kind與reverse）、`PlayCast`、`PlayHit`、`SetDead`。沒有新增或改名CharacterRig公開成員。
- 手機高度決定蹲／跳；攻擊只疊轉腰和手部，不能把追蹤的身體往前推或再壓低。倒地優先。
- 空手左手仍可對應手機；已持弓、短刃、盾書的手保留持物姿勢。右手手勢輸入保留。
- `Charge` 0→1由輕準備逐步到完整蓄力。防禦不被殘留的蓄力手勢蓋掉。跳起收腿，落地有短壓縮並自動回復。
- 站姿／戰鬥姿勢沿用0.4秒平滑切換。武器仍是手腕子物件；初心者回復既有木棍，未創建新服裝或修改身體比例。
- 弓弦是快取的三點LineRenderer，頂底固定於弓梢、中點隨拉弦；未使用圖片。姿勢動畫不重新建立網格／材質。

## 驗證範圍

原生檢查包括：16×7狀態有限座標、深蹲高度與腳底接地、跳躍高度、短落地壓縮回復、蓄力姿勢漸進、手機高度不被攻擊覆蓋、收招結束、動畫期間網格及材質數不增加。全站Snapshot既有所有檢查保留；獨立 `SpellDuel player pose validation` 使用同一份檢查與畫圖函式，只用來加速本項迭代。

手機AR現場舒適度與幀時間尚未實機量測；這份資料證明原生編輯器畫面與平台編譯，不宣稱實機效能數字。

預覽使用各張模型邊界自動取景，並非固定相機的高度比較；蹲跳高度以 `native_validation.json` 的公尺值為準。深蹲腳底最大誤差為 0.00000006m（浮點數精度範圍）。

原生程式版本：`f8b7e96`。全站 Snapshot `40660751770`、動作檢查 `40660751663` 成功；平台作業 `40660751701` 的 Android APK、iOS 匯出、未簽署 IPA 全部成功。TestFlight 發佈不列作本項完成條件。
