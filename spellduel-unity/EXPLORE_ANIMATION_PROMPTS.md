# 故事探索「翠蔭迷林」動畫提示詞

這份是故事探索場景裡**需要做動畫**的項目，給使用者另一個 session 的**地端 AI** 製作（不是 Codex）。Codex 只做靜態模型，並把會動的部位分成獨立、有命名的子物件（見 `CODEX_TASK_EXPLORE_JUNGLE.md`「動畫」一節與 `CODEX_PENDING_BRIEF.md` 的實作紀錄），地端 AI 的動畫做好後由 Claude 接到遊戲的觸發點。

**共同風格（每一則都可以接在提示詞後面）**
> Japanese anime cel-shaded style, two-tone toon shading with crisp black outlines, saturated jungle greens, moss green and bark brown, corrupted parts in black-purple glow, quest objects in warm white / pale blue light, game asset animation, clean loop, isolated on plain dark background, 3/4 front view, no text, no watermark

中文版共同風格：日式賽璐珞動畫風、兩階明暗加黑色描邊、高彩度的叢林綠／苔綠／樹皮棕、被腐化的部分是黑紫色發光、任務物品是暖白或淡藍色的光、遊戲素材動畫、可循環、深色素背景、斜前方視角、不要文字和浮水印。

**實際製作：影片生成機用 ComfyUI MiniMax H3 做成 mp4。** 要生成的清單、去背規格與交貨方式在 [`Docs/explore_fx/README.md`](Docs/explore_fx/README.md) 和 [`Docs/explore_fx/shots.json`](Docs/explore_fx/shots.json)（25 支，已經改寫成純黑／綠幕背景的版本）。門、機關、樹叢這類立體動作改由 Claude 用程式做，影片只做疊上去的光與粒子。

每則都附上：用途、長度、鏡頭、英文提示詞、中文說明。秒數就是遊戲裡的實際長度。

---

## A. 封印與門（打開的動畫）

### A1. 藤蔓門打開（小 Boss 打倒後）
- 用途：三個競技場後面擋路的藤蔓門，守衛倒下時打開。長度 **1.5 秒**，播一次。鏡頭：玩家站在門前 3m 平視。
- 英文：`A wall of thick tangled jungle vines with a faint black-purple corrupted glow blocks a 3-meter-wide forest path. The purple glow flickers out, then the vines writhe and retract into the ground from the center outward, scattering leaves and small sparks of green light, revealing the path behind. 1.5 seconds.`
- 中文：一面糾纏的粗藤蔓牆擋住 3 公尺寬的林間小路，表面有微弱的黑紫色腐化光；紫光先閃一下熄滅，藤蔓從中間往兩側扭動、縮回地面，飄落葉子和綠色光點，露出後面的路。

### A2. 苔根封印解開（拉動石雕機關後）
- 用途：第一個巢穴入口的封印。長度 **2 秒**，播一次。
- 英文：`A dense knot of mossy giant tree roots seals a stone archway in the jungle. A carved stone groove along the ground lights up with green runes traveling toward the roots; when the light reaches them, the roots loosen, uncoil and slide back into the earth with dust and falling moss, opening the archway.`
- 中文：苔蘚覆蓋的巨大樹根盤結封住一個石拱門；地上的石槽亮起綠色符文，光一路傳到樹根，樹根鬆開、捲回地底，揚起塵土和掉落的苔蘚，拱門打開。

### A3. 寨主之鎖打開（拿到古銅鑰匙）
- 用途：第二個祭壇入口，木柵上掛著大鎖。長度 **1.5 秒**。
- 英文：`A crude wooden palisade gate made of sharpened logs, chained shut with a huge ancient bronze padlock. A bronze key floats in, turns with a glint, the padlock snaps open and drops, the chains rattle loose and the two log doors swing open outward.`
- 中文：削尖木樁做的簡陋柵門，用鐵鍊和一把巨大古銅鎖鎖住；古銅鑰匙飄進去一轉、閃一下，鎖彈開掉落，鐵鍊鬆脫，兩扇木門往外打開。

### A4. 雙獵封印（兩個機關）
- 用途：第三個獵場入口，兩道藤蔓結，每拉一個機關解開一道。每道 **1 秒**。
- 英文：`Two glowing vine knots, one on the left and one on the right, seal a hunting-ground entrance decorated with feathers and hanging bone charms. The left knot flares amber and unties itself into loose vines falling away; a moment later the right knot does the same, and the entrance opens.`
- 中文：入口被左右兩道發光的藤蔓結封住，周圍掛著羽毛和骨頭吊飾；左邊的結先亮起琥珀色光、自己解開散落，接著右邊的也一樣，入口打開。

### A5. 翠玉封印（三片翠玉碎片）
- 用途：最終王座的石門，刻著三個凹槽。每放進一片 **0.8 秒**，三片都放好後開門 **2.5 秒**。
- 英文：`A massive ancient stone door overgrown with vines, carved with a forest-palace emblem and three empty sockets. One by one, three glowing jade shards fly into the sockets and lock in place with a bright green pulse; when the third shard lands, green light races along the carved lines, the vines burn away into green sparks, and the heavy door slides open revealing a dark throne hall.`
- 中文：爬滿藤蔓的巨大古老石門，刻著森林古殿的紋章和三個空凹槽；三片發光的翠玉碎片一片一片飛進凹槽卡住、閃一下綠光；第三片放好後綠光沿著刻紋奔流，藤蔓燒成綠色光點消散，沉重的石門滑開，露出昏暗的王座大廳。

---

## B. 岔路裡的東西

### B1. 石雕機關（啟動）
- 用途：死路底的機關，玩家走到旁邊就啟動。待機循環 **2 秒**，啟動 **1 秒**。
- 英文（待機）：`A small carved stone pedestal with a mossy pressure plate at the end of a narrow jungle path, a faint green rune on top pulsing slowly like breathing, seamless loop.`
- 英文（啟動）：`The pressure plate sinks down with a heavy click, the rune flashes bright green, a ring of light spreads across the ground and a beam of green light shoots along a groove into the distance.`
- 中文：窄路盡頭一座長苔的小石台，上面的綠色符文像呼吸一樣慢慢亮暗；啟動時石盤往下沉、喀的一聲，符文亮起，一圈光在地上擴散，一道綠光沿著石槽射向遠方。

### B2. 古銅鑰匙／翠玉碎片（漂浮待機、撿起）
- 用途：死路底遠遠就要看得到一點光。待機循環 **3 秒**，撿起 **0.6 秒**。
- 英文：`An ornate ancient bronze key (and, separately, a sharp crystal shard of glowing jade) floating at chest height above a mossy stone, slowly rotating and bobbing, casting a soft light on the ground, tiny motes of light drifting upward, seamless loop. On pickup it spins fast, shrinks into a bright star and flies toward the camera.`
- 中文：精緻的古銅鑰匙（另一個是發光的翠玉碎片）浮在長苔的石頭上方胸口高度，慢慢旋轉上下浮動，在地上照出柔光，小光點往上飄；撿起時快速旋轉、縮成一顆亮星飛向鏡頭。

---

## C. 任務物品與閃光點

### C1. 隱藏閃光點（偶爾閃一下）
- 用途：使用者要的「隱藏的閃光點」，平常幾乎看不到。循環 **3 秒**：前 2.6 秒幾乎看不見，最後 0.4 秒閃一下。
- 英文：`A barely visible hint on the jungle floor among grass and roots; every few seconds a tiny four-pointed star glint briefly flashes and fades, very subtle, seamless loop.` 依種類換開頭：`in loose soil (golden glint)`、`inside a hollow tree trunk (green glow)`、`in a bush, the reflection of a small animal's eyes`、`in a gap of a vine-covered wall (thin white light)`。
- 中文：草叢和樹根間幾乎看不到的線索，每隔幾秒閃一下很小的四芒星光就消失，非常低調。依種類：鬆土裡的金光、樹洞裡的綠光、草叢裡小動物眼睛的反光、藤牆縫隙透出的細白光。

### C2. 發現閃光點
- 長度 **1 秒**。
- 英文：`The tiny glint bursts into a ring of soft white-gold light and a shower of sparkles rising upward, with a gentle chime-like shimmer.`
- 中文：小光點爆開成一圈柔和的白金色光環，一陣光粒往上飄。

### C3. 月光草
- 循環 **4 秒**。
- 英文：`A small cluster of slender jungle herbs with pale blue bioluminescent leaf tips, glowing brighter and dimmer like slow breathing, a few firefly-like motes floating around, seamless loop.`
- 中文：一小叢細長的草，葉尖有淡藍色的螢光，像緩慢呼吸一樣亮暗，旁邊飄幾顆像螢火蟲的光點。

### C4. 祭壇與古樹之心
- 用途：Boss 倒下後浮出，被拿走後熄滅。浮出 **2 秒**、待機循環 **3 秒**、拿走 **1 秒**。
- 英文：`An ancient mossy stone altar behind a withered tree throne. After the boss falls, a heart-shaped seed of living wood wrapped in green light rises from the altar, pulsing like a heartbeat with leaves sprouting around it (loop). When taken, the light flows into the camera and the altar's runes fade to dark.`
- 中文：枯樹王座後面長苔的古石祭壇；Boss 倒下後，一顆被綠光包著、活木做的心形種子從祭壇升起，像心跳一樣脈動，周圍冒出新葉（循環）；被拿走時光流向鏡頭，祭壇上的符文熄滅。

---

## D. 環境的小動畫（循環）

### D1. 叢林樹叢牆（微風）
- 循環 **4 秒**，動作要非常小（不要讓玩家以為能穿過去）。
- 英文：`A dense wall of jungle foliage — broad leaves, ferns, hanging vines — gently swaying in a light breeze, vines swinging slightly, seamless loop, subtle motion.`
- 中文：密實的叢林樹叢牆（闊葉、蕨類、垂藤）在微風中輕輕搖擺，垂藤微微晃動，動作要很小。

### D2. 月影湖湖面
- 循環 **5 秒**。
- 英文：`A calm jungle lake surface at dusk with moonlight reflections shimmering, lily pads drifting slightly, reeds at the shore swaying, fireflies hovering above the water, seamless loop.`
- 中文：黃昏的平靜湖面，月光倒影閃爍，睡蓮輕輕漂，湖邊蘆葦搖擺，水面上有螢火蟲。

### D3. 篝火（營地、城寨）
- 循環 **1 秒**。
- 英文：`A small campfire of crossed logs ringed by stones, flickering orange flames, rising sparks and thin smoke, seamless loop.`
- 中文：石頭圍起來、木柴交叉的小篝火，火焰跳動，火星往上飄，冒細煙。

### D4. 巨菇林孢子
- 循環 **4 秒**。
- 英文：`Giant glowing jungle mushrooms with pink and teal caps slowly pulsing, releasing drifting luminous spores that float upward and fade, seamless loop.`
- 中文：粉紅和青綠菇傘的發光巨菇慢慢脈動，飄出發光孢子往上浮、慢慢消失。

### D5. 回音洞窟
- 循環 **3 秒**。
- 英文：`Inside a dark jungle cave: water drops falling from stalactites into small puddles with ripples, glowing blue moss and crystals pulsing softly on the rock walls, roots hanging from the ceiling, seamless loop.`
- 中文：昏暗的洞窟裡，水滴從鐘乳石滴進小水窪泛起漣漪，岩壁上發藍光的苔蘚和水晶柔和地脈動，天花板垂下樹根。

### D6. 藤牙寨（旗子、火把）
- 循環 **2 秒**。
- 英文：`A crude jungle fortress palisade of sharpened logs with a lookout tower, tattered vine-green war banners flapping in the wind, skull charms swinging, burning torches flickering, seamless loop.`
- 中文：削尖木樁圍成的叢林城寨和瞭望台，破舊的藤綠色戰旗隨風飄動，骷髏吊飾搖晃，火把閃爍。

---

## E. 人物與提示

### E1. 森林之靈（NPC 待機）
- 循環 **3 秒**。
- 英文：`A small translucent forest spirit made of soft green light, shaped like a child with leaf-like hair and a long wispy tail instead of legs, floating and slowly bobbing, leaves orbiting around it, gentle smile, seamless loop.`
- 中文：用柔和綠光構成的半透明小精靈，像小孩的樣子，頭髮像葉子，沒有腳、是一條長長的光尾，上下飄浮，周圍有葉子繞著轉，溫柔地微笑。

### E2. 怪物發現玩家的「！」
- 長度 **0.8 秒**；會叫同伴的那隻再加 **1 秒**的聲波。
- 英文：`A bold red exclamation mark pops up above a jungle monster's head with a quick squash-and-stretch bounce and fades out. Variant: the monster throws its head back and roars, sending two expanding red sound-wave rings outward.`
- 中文：叢林怪物頭上彈出紅色粗驚嘆號（壓扁拉長的彈跳）後淡出；會叫同伴的那隻：仰頭吼叫，往外擴散兩圈紅色聲波。

### E3. NPC 頭上的任務標記
- 循環 **1.5 秒**。
- 英文：`A golden exclamation mark (and, separately, a golden question mark) floating above an NPC's head, gently bobbing and slowly spinning with a soft glow, seamless loop.`
- 中文：NPC 頭上浮著金色驚嘆號（另一個是金色問號），輕輕上下浮動、慢慢旋轉、柔和發光。

### E4. 發現地標的地名橫幅
- 長度 **2.5 秒**（出現 0.4、停 1.7、消失 0.4）。
- 英文：`An elegant fantasy location title banner appears at the top of the screen: a thin ornamental vine-and-leaf frame unfurls from the center, the place name fades in on a soft dark-green ribbon with gold edges, holds, then dissolves into drifting leaves. UI animation, transparent background.`
- 中文：畫面上方出現精緻的地名橫幅：細藤蔓和葉子的裝飾框從中間展開，地名在深綠色、金邊的柔和緞帶上淡入，停一下後化成飄落的葉子消失。

---

## 做好之後

地端 AI 的動畫做好後，Claude 依下面的觸發點接進遊戲（遊戲裡什麼時候播）：

| 動畫 | 什麼時候播 |
|---|---|
| A1 藤蔓門打開 | 小 Boss 被打倒 |
| A2～A5 封印解開 | 機關、鑰匙、碎片湊齊（A5 每放進一片碎片播一次凹槽亮起） |
| B1 機關待機／啟動、B2 鑰匙與碎片待機／撿起 | 平常循環；玩家走到旁邊時啟動／撿起 |
| C1 閃光點、C2 發現 | 平常循環；玩家點到時 |
| C3 月光草、C4 祭壇 | 平常循環；Boss 倒下時浮出古樹之心、拿走時熄滅 |
| D 系列環境 | 玩家附近時循環 |
| E1 森林之靈 | 點到「發光的樹洞」閃光點、森林之靈出現後循環 |
| E2「！」 | 怪物發現玩家（會叫同伴的加聲波） |
| E3 任務標記 | NPC 有任務（❗）、可以回報（❓）時循環 |
| E4 地名橫幅 | 第一次走進地標 |
