# 對話用 2D 立繪、表情、背景（給 Codex）

## 使用者的要求
對話介面改成《永遠的7日之都》那樣的風格。介面程式已經做好，現在只缺美術：
- 角色在對話時的 **2D 立繪和表情**。
- 對話的 **2D 背景**。

對話時一律用 2D（背景也是 2D），只有進入戰鬥才是 3D。**人物要和劇情影片裡的角色一模一樣。**

## 介面（已完成，給你參考構圖）
- 全螢幕背景圖。
- 左右兩個立繪位置：說話的人在前面、亮的；另一位變暗、縮小一點。右邊的人會自動左右翻轉，臉朝畫面中間。
- 下方寬版對話框，名牌是斜切色塊，壓在框的上緣。
- 程式在 `Runtime/SoloBattle.VnDialogue.cs`、`Runtime/Story2D.cs`。
- 手機是直式畫面：立繪高度約為螢幕的 66%，下面約 1/3 被對話框蓋住。所以**重要的是臉和上半身**。

## 參考圖（一定要對照，長得要一模一樣）
- 角色定裝圖在 `Docs/story_video/ref/`：
  - `Erin.png`（艾琳）、`Laoyan.png`（老焰）、`Xiaotie.png`（小鐵）、`Xiaoye.png`（小葉）
  - `Cui.png`（翠）、`Haido.png`（海斗）、`Mio.png`（澪）、`Nagi.png`（凪）
  - `Golem.png`（熔岩巨像）、`TreeKing.png`（腐化樹王）、`Kraken.png`（深海巨妖）
- 影片截圖在 `Docs/story_video/frames/`（臉部特寫、光影、畫風都以這裡為準）。
- 沒有定裝圖的角色：照 `CHARACTER_PERSONA_BRIEF.md`、`Runtime/Portraits.cs` 裡的設定（髮色、服裝、個性），畫風要和上面的角色一致。

## 立繪規格
- **檔案位置**：`Assets/SpellDuel/Resources/Story2D/chars/{角色id}_{表情}.png`
- **格式**：PNG，**透明背景**，寬 768 × 高 1152。
- **構圖**：大腿以上的半身像。
  - 頭頂距離上緣約 6%。
  - 每個角色**頭的大小一致**（約 150px 高；Boss 例外）。
- **方向**：身體和臉微微朝**畫面右邊**（程式會幫右邊的人翻轉）。
- **畫風**：和影片一樣的日系動畫賽璐珞，乾淨的線條，兩到三階陰影。
- **同一個角色的所有表情要完全同一套**：同一套衣服、同一個姿勢、同一個位置，只換臉（眉、眼、嘴、臉頰）。換表情時才不會跳動。

**表情**（每個人類角色 6 張）：

| 表情 id | 用途 |
|---|---|
| `normal` | 平常 |
| `smile` | 笑 |
| `angry` | 生氣 |
| `sad` | 難過 |
| `surprised` | 驚訝 |
| `serious` | 認真、堅定 |

**Boss** 只要 `normal`、`angry` 兩張，構圖可以是上半身或臉部特寫，大小不受「頭的大小一致」限制。

## 角色清單（id 已寫在 `Story2D.Cast`，請照用）
| 名字 | id | 參考圖 | 備註 |
|---|---|---|---|
| 艾琳 | erin | Erin.png | 主要夥伴，台詞最多，優先做 |
| 老焰 | laoyan | Laoyan.png | 炎之里村長，老人 |
| 小鐵 | xiaotie | Xiaotie.png | 見習鐵匠 |
| 米菈 | mila | — | 旅行商人 |
| 翠 | cui | Cui.png | 森之里長老（精靈） |
| 楓 | feng | — | 巡林獵人 |
| 小葉 | xiaoye | Xiaoye.png | 森之里的小孩 |
| 海斗 | haido | Haido.png | 白帆號船長的後人 |
| 凪 | nagi | Nagi.png | 酒館老闆娘 |
| 澪 | mio | Mio.png | 唱歸航曲的女孩 |
| 阿潮 | achao | — | 老漁夫 |
| 雷歐 | leo | — | 斷角的退役鬥士 |
| 小沙 | xiaosha | — | 賣票少女 |
| 鐵娘 | tieniang | — | 鍛甲師 |
| 老駝 | laotuo | — | 驛站長，老人 |
| 阿鈴 | aling | — | 馬幫少女 |
| 石先生 | shi | — | 說書人 |
| 工頭老石 | foreman | — | 礦坑工頭 |
| 阿岩 | ayan | — | 礦工 |
| 黑衣鑄鐘匠 | bellsmith | — | 神祕反派 |
| 公會長 | guildmaster | — | 職業工會長 |
| 塔守 | towerkeeper | — | 無盡塔守塔人 |
| 熔岩巨像 | golem | Golem.png | Boss |
| 腐化樹王 | treeking | TreeKing.png | Boss |
| 深海巨妖 | kraken | Kraken.png | Boss |
| 青銅鬥魂 | bronze | — | Boss（第 4 章） |
| 黃土岩魔 | rockfiend | — | Boss（第 5 章） |
| 霸主 | overlord | — | Boss |
| 熔岩殘影／樹心殘影／潮影 | lavashade／treeshade／tideshade | — | 副本敵人（半透明、殘影感） |

## 背景規格
- **檔案位置**：`Assets/SpellDuel/Resources/Story2D/bg/{背景id}.jpg`
- **格式**：JPG，**直式** 1080 × 1920。
- **構圖**：下方 1/3 會被對話框蓋住；上方 2/3 要有主題重點；不要有人物。
- **畫風**：和影片場景一致的動畫背景。

| 背景 id | 場景 |
|---|---|
| village | 炎之里（山村、晨鐘鐘樓、鍛爐） |
| town_forest | 森之里（古樹、葉燈、刻名木牌） |
| town_port | 潮騷港（港口、舊燈塔、酒館） |
| town_arena | 赤砂城（角鬥場外的城鎮） |
| town_canyon | 風沙驛（黃土山道上的驛站） |
| guild | 職業工會內部 |
| tower | 無盡塔入口 |
| dungeon | 副本入口（洞窟或遺跡） |
| stage_volcano／boss_volcano | 熔岩地帶的山路／熔岩大廳（巨像守的石閘） |
| stage_forest／boss_forest | 迷霧森林／記名林深處（腐化樹王） |
| stage_sea／boss_sea | 海上礁石／暴風雨的燈塔礁石（深海巨妖） |
| stage_arena／boss_arena | 角鬥場看台通道／角鬥場中央 |
| stage_canyon／boss_canyon | 黃土山道／崖頂（岩魔） |

程式的預設規則：故事關卡打的波數有包含第 10 波（Boss）就用 `boss_` 開頭，否則用 `stage_`；城鎮、工會、塔、副本依當下畫面決定。劇本裡寫一行 `背景：bg_id` 可以中途換背景。

## 劇本加上表情
在 `Resources/story.txt`、`Resources/dungeons.txt` 的台詞，名字後面加 `［表情］`，例如：

```
艾琳（mage）［smile］：走吧，先去礦坑救採礦隊。
老焰［sad］：鐘聲該讓出門的人知道，家還在這裡……可它啞了。
```

表情可以寫英文 id，或寫中文：笑、怒、哀、驚、認真、普通。沒寫的話，程式會從台詞猜一個。

需要換場景的地方，可以加 `背景：xxx` 這一行。

## 安裝檔大小
- 全部圖片加起來盡量在 40MB 以內。
- Unity 匯入設定維持預設壓縮即可，最大尺寸 2048。
- 立繪若太大可以縮到 640 × 960。

## 不要改
戰鬥判定、技能、數值、連線、`SoloBattle.VnDialogue.cs` 的流程。介面外觀如果要微調，例如名牌樣式、對話框的花紋，可以改，但不要改操作方式。

## 建議順序
1. 先做艾琳 6 張、老焰 6 張、`village` 背景，push 一次讓使用者確認畫風。
2. 確認後再做其他角色和背景。

## 完成後
確認 `UNITY_IOS`、`UNITY_ANDROID` 都能編譯，commit 並 push 到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
