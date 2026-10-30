# 新技能特效交付與接線

全部是程式產生的網格、線與粒子。此目錄圖片是 Unity 原生截圖，不是生成式示意圖。實際命中、狀態時長、多人事件由 Claude 的 963f229 接入大部分技能特效；本批美術提交不修改 Combat、SoloBattle、連線或存檔。

## 已直接替換的外觀

`CreateProjectileVisual` 已依 id 替換 arcanewyrm、starfall、arcanebreak、holyblade、headshot、scattershot、blastarrow、spiritbolt、earthspike、suiton、futon、poisondart、lightpillar、holyfire、verdict、rollarrow、echo_fire、echo_ice。散射的箭數仍由既有邏輯產生。51 枚圖示由 `SkillIcons.Render/Get` 直接取得。

## 美術介面與接線狀態

963f229 已接入下列龍、陷阱、獵鷹、增益、範圍與被動特效。02b5b63 亦已接上 B5 三種 Rig 的 `PlayStagger`。尚待整合的項目：裝備的 `CharacterRig.BlueprintImpact`（玉米命中爆米花）、選用的 `TeamBuffHit`（元素護殼受擊亮片）。後兩項目前只有美術介面，不會自行猜測傷害或發送事件。

所有 `Vector3 ...World` 輸入均和既有 SpellFx 一樣，是呼叫當下的 Unity session 世界位置；只有需要保留歷史的龍身、獵鷹與吸魂在內部轉成 WorldFrame 戰場座標。

| 介面 | 呼叫時機／責任 |
|---|---|
| `WyrmFade(GameObject)` | 取代到期直接 Destroy；停止外部移動，由特效延遲銷毀 |
| `WyrmPierce(Vector3)` | 穿透命中位置，龍本體繼續飛 |
| `CreateTrapVisual(SkillDef)` | 建立陷阱；未知 id 回 null，呼叫者保留原 fallback |
| `SetTrapArmed(GameObject,bool)` | 切換啟動狀態；銷毀仍由陷阱生命週期負責 |
| `MineBlast(Vector3,float,Color)` | 觸發地雷爆炸；radius 是半徑 |
| `FalconDive(Vector3,Vector3,float)` | from=頭頂、to=目標胸口，seconds=.35；不產生傷害 |
| `TeamBuffCast(string,Transform)` | 每個接受增益者各呼叫一次 |
| `TeamBuffAura(Transform,string,float)` | 每幀 remain01，0 關閉；同一人不同技能獨立 |
| `TeamBuffHit(Transform,Vector3)` | 可選；元素護殼對應受擊方向短暫變亮 |
| `StarfallZone(Vector3,float,float)` | 放招時建立星陣，seconds 覆蓋三波；範圍精確等於 radius |
| `ArcaneBreakHit(Vector3)` | 奧術崩滅命中，先內縮再爆開 |
| `AreaBurst(string,Vector3,float)` | 每段範圍傷害呼叫；支援 consecrate、blastarrow、earthspike、suiton、bladestorm、holynova、arcaneburst、venom_burst、starfall |
| `SkillImpact(string,Vector3)` | 專屬命中火花／毒霧／戰錘裂紋；arcanebreak 轉專屬內爆 |
| `ChainArc(Vector3,Vector3,Color)` | 連鎖兩端，0.15 秒 |
| `Thorns(Transform,Vector3)` | 反傷者與攻擊來源位置，純短刺光 |
| `StackAura(Transform,int)` | 0～10 層，0 隱藏；第一人稱放下緣 |
| `ExecuteHit(Vector3)`／`FirstStrikeHit(Vector3)` | 斬殺紅斬痕／先發白環 |
| `KillRush(Transform)`／`SoulDrain(Vector3,Transform)` | 擊殺疾風／靈魂飛回 |

隊伍增益涵蓋 arcanehaste、elementward、rangerhaste、hawkeye、layhands、pactcheer、nightveil、divinegrace、mindfocus、meditate、bless、haste、spring、focus，以及 10 種爆發技能。圖示不在介面標示流派。

## 預覽與驗證

- `newfx_*_firstperson.png`／`newfx_*_spectator.png`：同一招兩種視角。
- `newfx_arcanewyrm_frame_*.png`：從手邊長出、轉彎到完整龍身的四張連續畫面；另附消散。
- `newfx_*_area_*.png`：落點範圍效果。
- `newfx_icon_*.png`：51 枚圖示。
- Unity 原生驗證包含龍身 24 段、AR 平移連續、獵鷹抵達時間、增益獨立清除／快取。
- 本機純圖示檢查：51 枚不重複且確定性、157 枚既有圖示未改變，8／32／48／64／128 五尺寸。
- Android／iOS 與原生截圖 CI 結果見隨附 evidence JSON；實機 AR／多人接線後仍需由整合端驗收。

## B5 追加：專屬硬直動作

使用者確認一起納入遠端新增的 B5。三種 Rig 各提供 `PlayStagger(float seconds)`，一般傳入 `.9f`。Claude 已於 02b5b63 在既有硬直事件接上；有專屬動作的模型不要同時播放 `StaggerJolt`。Boss 觸發條件仍由戰鬥端排除，SkyEnemyRig 的空賊船長 Kind=4 也直接忽略。

| 外型 | 受擊姿勢 |
|---|---|
| 重裝／盾兵 | 重心低、較小但沉重後仰、武器落下 |
| 施法者／弓手／刺客 | 分別採抬頭失衡、側身退步、扭腰後仰 |
| 四足獸 | 前身仰起、前肢折下、後腿撐住 |
| 植物 | 花莖彎折、花頭垂落並回彈 |
| 魚／鳥 | 翻身甩尾／收翅跌落一小段 |
| 寄居蟹 | 壓低殼體、螯垂下 |
| 水母／河豚 | 傘面壓扁／洩氣縮小 |
| 天空機巧 | 軸心傾斜、左右手臂不對稱失衡 |

原生檢查涵蓋 53 種 Minion、23 種 Variant、5 種 Sky（其中 1 位 Boss 排除）。不改根物件位置／朝向；附加骨架姿勢先還原再套入，0 秒取樣不累積；重複觸發、回復、死亡與換型都保留正確生命週期。`newfx_stagger_*_peak.png` 是受擊峰值，部分代表模型另有 before／recovered。

原生驗證版本：`40beb40`，Actions `41057693326` 成功；共 355 張原生 PNG。B5 的 81 模型檢查全數通過，含空賊船長不播放硬直。`stagger_overview.jpg` 為全員峰值，`stagger_sequences.jpg` 為九種代表體型的前／中／後比較。

### 全站檢查的另外三項失敗

`full_site_checks.json` 保留 40beb40 全站檢查結果：支援敵人固定清單缺少 eh_121_sunbolt、DungeonSpawnAudit 的敵人總數不符、既有劍氣盾停用清除檢查失敗。對應檢查程式與 SpellFx.SwordShield.cs 均未由這批美術修改；本批沒有略過或移除斷言。新技能原生驗證成功不代表這三項已修復。
