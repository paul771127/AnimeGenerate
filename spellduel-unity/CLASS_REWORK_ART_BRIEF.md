# 職業改版（忍者／影舞者／主教／審判者／召喚師）：美術需求（給 Codex）

系統、數值、判定、連線同步都已經做好。這份只列**美術**要做的部分，目前全部是程式畫的簡單暫代造型。
**不要改**：技能 id、數值、判定、連線訊息、`Combat*.cs` 的邏輯。名稱若要改請告訴 Claude 補語音辨識。
風格沿用 [`ART_STYLE.md`](ART_STYLE.md)（日式賽璐珞、兩階明暗、黑邊、彩度高）；特效一律程式產生（`SpellFx` 的工具：`NewPS`、`Flash`、`Sparks`、`Line`、`Circle`、`FxAnim.AddRing` 等），不要用圖片檔。

## 1. 召喚物外觀 — `Runtime/PetRig.cs`
| 召喚物 | 目前 | 想要 |
|---|---|---|
| 冰靈（`BuildIce`，kind `ice`，浮在主人左肩） | 藍白冰晶 | 有臉的小冰晶精靈：六角冰晶身體、兩隻小冰手、身邊飄雪花粒子；攻擊時冰晶發亮 |
| 治癒妖精（`BuildFairy`，kind `fairy`，在主人頭上繞圈） | 粉紅光球＋翅膀 | 手掌大小的小妖精：粉紅頭髮、半透明蝴蝶翅膀（拍動）、拖著光點尾巴；每 3 秒治療時灑出愛心／十字光點 |

## 2. 收服的魔物 — `SoloBattle.DungeonMap.cs` `UpdateTamedRigs`
用原本小怪的模型（`MinionRig`），腳下一圈綠色光環。想要：光環換成有符文的契約法陣（慢慢旋轉），頭上有小小的爪印圖示；被收服的瞬間有「契約成立」的光柱特效。隊友手機上也會出現（同一段程式）。

## 3. 影分身（影舞者）— `SoloBattle.DungeonMap.cs` `UpdateNpcRigs`
目前和一般友方人物一樣用 `CharacterRig.Create("assassin")`（判斷：`n.cloneOf != null`，隊友那邊的代理是 `n.friendOwner >= 0 && n.tamedKind < 0 && n.name` 含「影分身」）。想要：主人的樣子但整個是半透明深紫色、邊緣冒黑紫色煙；放「影殺」時（`Emit("ambush", …, "影分身・影殺！")`）身體一閃；倒下時碎成影子消散。

## 4. 忍者的苦無 — `SpellFx.cs` `CreateProjectileVisual`
目前 `na_kunai`／`kunai_fire`／`kunai_ice`／`kunai_bolt`／`shuriken` 暫時借用飛刀 `knife` 的造型（只有顏色不同）。想要：
- `na_kunai`：銀色苦無（菱形刀身＋圓環柄＋紅色布條），不旋轉、筆直飛。
- `kunai_fire`：苦無包著火焰、拖火星尾巴；`kunai_ice`：刀身結霜、拖冰晶碎屑；`kunai_bolt`：刀身纏著黃色電弧（`Zigzag`）。
- `shuriken`：四角手裏劍，高速旋轉。
- 普攻集氣畫面（`SoloBattle.SwipeTrail.cs`，`na_kunai` 目前共用刺客的 `DrawDaggerCharge`）：換成銀白色、掌心浮出一枚苦無的樣子。

## 5. 審判之地／天罰 — `SoloBattle.Friends.cs` `HolyZoneView`、`TickHolyZone`
目前是金色半透明圓盤＋外圈線，偶爾冒光點。想要：地上的金色十字聖紋法陣（慢轉）、圓周豎起一圈短光柱、敵人站在裡面時頭上落下小光針；天罰（`z.skill.id == "heavenpunish"`）半徑 2 倍、更亮、外圈有天使羽毛飄落。落地瞬間（事件 `holyzone`，SoloBattle.cs）來一道從天而降的粗光柱。

## 6. 聖域障壁（主教）— `SoloBattle.Friends.cs` `UpdateBarrierViews`
目前是金色半透明球＋地上 3 圈光環（剩幾次就幾圈）。想要：六角形蜂巢花紋的光罩、被擋下攻擊時（`TryBarrier` 的 `mitigate` 事件，文字含「障壁抵擋」）擊中點出現漣漪並少一圈；最後一次碎掉（事件 `barrierbreak`）像玻璃一樣碎開。

## 7. 新技能圖示 — `Runtime/SkillIcons.cs`
| 技能 id | 名稱 | 圖案建議 |
|---|---|---|
| na_kunai | 苦無 | 斜放的苦無 |
| shuriken | 手裏劍 | 四角手裏劍＋旋轉線 |
| raiton | 雷遁 | 結印的手＋閃電 |
| shadowclone | 影分身 | 一個實心人影＋後面半透明紫色人影 |
| shadowkill | 影殺 | 從影子裡伸出的匕首 |
| deadlypoison | 致命劇毒 | 綠色毒針＋滴落的毒液 |
| plague | 萬毒引爆 | 綠色骷髏爆開 |
| holybarrier | 聖域障壁 | 六角形光罩＋數字 3 |
| icespirit／fairy／capture | 冰靈／治癒妖精／魔物捕捉 | 冰晶精靈臉／小妖精／契約法陣裡的爪印 |
