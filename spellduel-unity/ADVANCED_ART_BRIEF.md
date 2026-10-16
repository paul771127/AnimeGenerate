# 進階職業：美術與劇情需求（給 ChatGPT／Codex）

系統、數值、判定都已經做好（`Runtime/Advanced.cs`、`Combat.cs`）。這份文件列出**只需要美術與文字**的部分。
**不要改**：技能 id、數值、判定邏輯、語音辨識表（`SpeechInput.cs`）。技能名稱與咒語如果要改，請一併告訴 Claude 補語音辨識。

風格沿用 [`ART_STYLE.md`](ART_STYLE.md)：日式賽璐珞、兩階明暗、黑邊、彩度高。

---

## 1. 技能圖示（30 個）— `Runtime/SkillIcons.cs`

目前進階技能用 `IconShape()` 借用相近技能的圖形（只有顏色不同）。請為每個進階技能畫專屬圖案：
在 `Render()` 的 `switch` 加上 `case "<技能 id>": 新函式(cv, sc); break;`，並把 `IconShape()` 裡對應的那一行刪掉。
畫法參考現有的 `Slash`、`Fire`、`Holy` 等函式（純程式繪製，不能用圖片檔）。

| 進階職業 | 技能 id | 名稱 | 圖案建議 |
|---|---|---|---|
| 聖騎士 paladin（金黃 #fde047） | shieldbash | 聖盾衝鋒 | 發光的圓盾往前衝、後面有速度線 |
| | oath | 守護誓言 | 盾牌上有十字＋兩條相連的光帶 |
| | sanction | 神聖制裁 | 由上往下砸的光之錘＋地面衝擊環 |
| 狂戰士 berserker（血紅 #ef4444） | whirlwind | 旋風斬 | 雙手斧畫出的 360° 紅色旋風 |
| | bloodrage | 血之狂暴 | 滴血的怒吼面具／心臟 |
| | undying | 不死之軀 | 碎裂但仍站著的骷髏盾 |
| 神射手 marksman（金 #facc15） | pierce | 貫穿狙擊 | 一支箭穿過三個圓圈 |
| | deadmark | 致命標記 | 準星＋骷髏 |
| | arrowrain | 箭雨 | 很多箭由上往下落 |
| 遊俠 ranger（翠綠 #34d399） | falcon | 獵鷹 | 展翅俯衝的鷹 |
| | rollshot | 翻滾射擊 | 翻滾的弧線＋射出的箭 |
| | trapchain | 連環陷阱 | 三個連在一起的捕獸夾 |
| 大魔導士 archmage（紫 #a855f7） | tjudge | 雷霆審判 | 三道閃電從雲層劈下 |
| | frostfield | 冰封領域 | 地面上的雪花法陣＋冰柱 |
| | manashield | 魔力護盾 | 六角形的魔力盾 |
| 召喚師 summoner（橘 #fb923c） | firespirit | 火靈 | 有眼睛的小火焰精靈 |
| | golem | 石像守衛 | 方塊感的石頭巨人頭像 |
| | detonate | 元素爆裂 | 精靈形狀的爆炸 |
| 忍者 ninja（灰藍 #64748b） | substitute | 替身術 | 一截木頭＋煙 |
| | clone | 分身術 | 兩個重疊的半透明人影 |
| | fireseal | 火遁 | 結印的手＋往前噴的火焰 |
| 影舞者 shadowdancer（紫紅 #c026d3） | execute | 處決 | 交叉的雙刃＋斷頭線 |
| | shadowdance | 影舞 | 旋轉的影子舞者 |
| | poisonfog | 毒霧 | 綠色的骷髏毒霧 |
| 主教 bishop（淡金 #fef9c3） | revive | 復活術 | 光芒中的羽翼＋向上的箭頭 |
| | massheal | 群體治療 | 三個重疊的愛心／十字 |
| | sanctuary | 神聖庇護 | 圓頂形的光罩 |
| 審判者 inquisitor（琥珀 #f59e0b） | holylance | 聖光之槍 | 發光的長槍 |
| | chastise | 懲戒 | 鎖鏈＋烙印 |
| | heavenpunish | 天罰 | 三道光柱從天而降 |

做完請重新產生 `docs/skill_icons.png` 的預覽（如果有工具）。

## 2. 召喚物外觀 — `Runtime/SoloBattle.cs` 的 `UpdatePetViews()`

目前是用基本形狀代替：

| kind | 是什麼 | 現在 | 希望 |
|---|---|---|---|
| `falcon` | 遊俠的獵鷹（在頭上盤旋，攻擊時射出 `pet_falcon`） | 拍動的扁球 | 賽璐珞風的鷹（翅膀拍動） |
| `fire` | 召喚師的火靈（浮在主人肩膀旁） | 閃爍的橘色球 | 有臉的小火焰精靈，尾巴是火焰粒子 |
| `golem` | 石像守衛（站在主人前方 1m，HP 越低越紅） | 灰色方塊 | 石頭巨人（約 1.2m 高），受傷時出現裂痕 |
| `clone` | 忍者的分身（站在主人旁邊） | 半透明膠囊 | 半透明、帶殘影的忍者剪影 |

可以仿照 `MinionRig.cs` 寫一個 `PetRig.cs`（`Create(kind)`、`SetPose(pos, forward)`、`Hurt()`），再把 `UpdatePetViews()` 換成用它。
注意：位置每幀由 `Battle.pets` 決定（`p.pos` 是身體中心），外觀只負責好看。

## 3. 技能特效 — `SoloBattle.OnBattleEvent`

| 事件 | 現在 | 希望 |
|---|---|---|
| `aoe`（神聖制裁、旋風斬、冰封領域、元素爆裂；`text`＝技能 id） | 共用 `SpellFx.DispelRing` | 依技能 id 分：金色光錘衝擊波／紅色旋風／冰柱從地面竄出／火焰爆炸 |
| `fogzone`（毒霧）＋ 毒霧地面（`burnGo` 的綠色圓盤） | 綠色半透明圓盤＋小光點 | 翻騰的綠色霧氣粒子 |
| `petspawn` / `petgone` | 共用 `SpellFx.Impact` | 召喚魔法陣／消散的煙 |
| 「信仰爆發」「不死之軀」「替身術」等 `buff`／`evade` 文字 | 只有浮動文字 | 可以加簡單的光效（選做） |

## 4. 劇情文字（選做）

- **轉職試煉**的開場與結束對話：`SoloBattle.StartTrial()` 裡的 3 句公會長台詞（目前是佔位文字），可以改寫成和主線（三口鐘的故事）呼應的版本；說話者名字、句數都可以改。
- **職業工會**畫面的公會長台詞：`WorldUI.DrawGuild()` 的 `status` 四種情況。
- **進階職業的一句話介紹**：`Advanced.cs` 每個 `AdvDef` 的 `desc`（例：「守護隊友的坦克」）。`core`、`weak` 是規則說明，請不要改內容，只能潤飾文字。
- 可以在三座城鎮加一位和轉職有關的居民（`WorldUI.Towns` 的 `npcs`）。

## 5. 驗收

- iOS／Android 都要能編譯（Claude 會幫忙檢查與建置）。
- 不要動任何 `Battle`／`Fighter` 的欄位與判定；只改畫面、圖示、文字。
