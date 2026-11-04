# 影片任務：月影古堡的兩支影片——副本開場（vd_castle_intro）＋魔劍士惡靈登場（vd_castle_boss）

> 第二次追加（使用者：「這副本開頭也加些劇情，影片一起開單」）：多一支**副本開場** `vd_castle_intro`，見最下面「B. 副本開場」。兩支流程相同，請一起做。

# A. 魔劍士惡靈登場（vd_castle_boss）

## 使用者的要求

> 測試副本最後的 Boss 房：先進 Boss 房後出現一些小劇情，敵人才撲過來。這個需要影片吧，開單吧。

## 遊戲端已經做好的部分（Claude）

- 走進月影古堡（`castle_test`）的王座之間時，**戰鬥先暫停**（單人）→ 播這支影片 → 播完敵人才撲過來。
  - 程式在 `Runtime/SoloBattle.DungeonExplore.cs` 的 `StartAreaNode`，影片設定在 `Runtime/StoryVideo.cs` 的 `ForDungeonBoss`。
- 影片還沒交之前，`StoryVideo.CastleBossFile = null`，遊戲改用對話框播同樣的台詞（`dungeons.txt` 王座之間的 `<enter>`）。
- 第一次看不能跳過；之後再進 Boss 房可以長按 1 秒跳過。

## 分鏡與台詞（已寫進資料檔）

- **分鏡**：`shots.json` 的 `videos` 最後一支 `vd_castle_boss`，6 個鏡頭，每個 5 秒，合計約 30 秒。
  - 新角色：`characters.Wraith`（魔劍士惡靈）。
  - 新場景：`characters.Castle`（王座之間）。
- **配音**：`narration/emotion_script.json` 的 `vd_castle_boss.mp4`，6 句，檔名 `vd_castle_boss_00.mp3`～`_05.mp3`。
  - 新聲音「魔劍士惡靈」：`tools/narration_tts_cosyvoice.py` 的 `CAST` 已經加好。用 `zh_male_calm.wav`，略降音、金屬感短迴音。
- **字幕**：`narration/subs.json` 的 `vd_castle_boss.mp4`。

| 鏡頭 | 畫面 | 配音 |
|---|---|---|
| s01 | 王座之間的燭火由近到遠一盞盞熄滅，只剩彩繪玻璃的紫光 | 旁白：王座之間的燭火，一盞接一盞熄滅了。 |
| s02 | 惡靈從王座站起，黑劍劍尖在石地上劃出火花 | 旁白：王座上坐著的不是國王——一個拖著黑劍的惡靈，緩緩站了起來。 |
| s03 | 面甲縫隙特寫，亮起兩點紫紅冥火 | 魔劍士惡靈：……又一個闖進來的。 |
| s04 | 惡靈舉劍指向鏡頭（玩家），劍身紫光流動 | 魔劍士惡靈：這座城堡的主人早就不在了。剩下的，由我和這把劍來守。 |
| s05 | 牆邊的盔甲、骷髏、石像鬼一個接一個轉頭，眼睛亮起 | 旁白：牆邊的盔甲、骷髏、石像鬼，一個接一個轉過頭來。 |
| s06 | 惡靈揮劍，冥火新月掃過大廳，敵人一起衝向鏡頭，淡出 | 魔劍士惡靈：上吧。把他們的名字，也刻進這座城堡的墓碑裡！ |

## 外觀參考（要跟遊戲裡的 2D 圖一致）

- 魔劍士惡靈：`Docs/castle_2d_preview/boss/castle_boss_directions.jpg`。
  - 破碎的黑色騎士甲、紫紅冥火、黑霧披風、發光黑劍。
  - **跟玩家差不多大**，不是巨人。
- 牆邊的敵人：`Docs/castle_2d_preview/ff_redesign/` 的骷髏劍兵、空殼騎士、石像鬼。
- 建議先用 `gen_charsheets.py` 的方式替 Wraith 做一張定裝圖（放 `ref/Wraith.png`），再用圖生影片，臉和盔甲才會一致。

## 在生成機上的步驟

跟前四支影片同一套流程，見 `README.md`。只做 `vd_castle_boss` 和 `vd_castle_intro` 這兩支，不要重做 v0～v3。

1. **畫面**：
   - 照 `shots.json` 的 `vd_castle_boss` 生成 6 個鏡頭（960×544、24fps、每個 124 幀）。
   - 依序接成 `videos/vd_castle_boss_v2.mp4`（`concat_video.py ../shots.json <out_dir> vd_castle_boss`）。
2. **配音**：
   - 在 `narration/` 執行 `ONLY=vd_castle_boss_00,vd_castle_boss_01,vd_castle_boss_02,vd_castle_boss_03,vd_castle_boss_04,vd_castle_boss_05 python ../tools/narration_tts_cosyvoice.py .`
   - 聽一遍，念錯就換同音字（不要用拼音標記，見 `tts_note`）。
3. **混音**：
   - `narration_mix.py` 會跑 `subs.json` 裡的每一支。這次只要混 `vd_castle_boss.mp4`：可以暫時只留這一支，或在腳本裡加篩選。
   - 輸出 `videos/vd_castle_boss_v1.mp4`，並更新 `narration/timing.json`。
4. **推上來**：
   - `videos/vd_castle_boss_v1.mp4`
   - 6 個配音 mp3
   - `narration/timing.json`
   - 要推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
5. **回報**：完整的 commit SHA，以及這支的 `timing.json` 數字。

## 交貨後 Claude 要做的

- `StoryVideo.CastleBossFile = "vd_castle_boss_v1.mp4"`。
- `LineTimes`、`Subs` 加上這支。
- 確認在遊戲裡播完會接到敵人撲過來。


# B. 副本開場（vd_castle_intro）

## 遊戲端（已做好）

- **播放時機**：第一次進月影古堡時，播完影片才進地圖。「📖 看故事」重看時也會播，重看可以長按跳過。
- **相關程式**：`SoloBattle.StartDungeonRun`、`StoryVideo.ForDungeonIntro`。
- **影片交來之前**：`CastleIntroFile = null`，改用對話框播同樣的 6 句（`dungeons.txt` 的 `castle_test` `<before>`）。
- **之前看過舊版開場的人**：會再播一次新版。

## 分鏡與台詞

- **分鏡**：`shots.json` 的 `vd_castle_intro`，6 個鏡頭。用到 `{Erin}`、`{Player}`：玩家只拍背影或剪影，不露臉。
- **配音**：`narration/emotion_script.json` 的 `vd_castle_intro.mp4`，檔名 `vd_castle_intro_00.mp3`～`_05.mp3`。
- **字幕**：`narration/subs.json`。

| 鏡頭 | 畫面 | 配音 |
|---|---|---|
| s01 | 月下山丘上的古堡，窗戶一扇扇亮起紫光 | 旁白：月亮被雲遮住的夜裡，山丘上那座沒人住的古堡，窗戶一扇一扇亮了起來。 |
| s02 | 山腳的村子，古堡的鐘聲響起，提燈的村民恍惚地走上山路 | 旁白：村裡的人說，每到這種夜晚，古堡的鐘就會響；聽見鐘聲的人，再也沒有回來。 |
| s03 | 艾琳和玩家站在城門前，艾琳翻開發光的古書 | 艾琳：{玩家}，地圖上沒有這座城堡的名字……書裡只寫了「月影」兩個字。 |
| s04 | 城門自己打開，兩人走進有水晶吊燈的玄關，大門在身後砰地關上 | 旁白：沉重的城門自己打開了，又在兩人身後「砰」地關上。 |
| s05 | 長廊上骷髏站起、空盔甲的面甲亮起藍光、提燈幽靈從牆裡飄出、蝙蝠飛散 | 旁白：走廊深處傳來喀啦喀啦的骨頭聲。盔甲、骷髏、提燈的幽靈，全都醒了。 |
| s06 | 地板縫亮紅光、尖刺微微升起，艾琳拉住玩家；前方長廊亮滿眼睛，淡出 | 艾琳：小心腳下——地板的縫裡有東西在發光。這座城堡，到處都是機關。 |

- **玩家稱呼**：台詞裡的 `{玩家}` 在字幕會換成「你」，配音的 `tts_text` 已經寫成「你」。
- **生成步驟**：同 A。
  - 配音：`ONLY=vd_castle_intro_00,...,vd_castle_intro_05`。
  - 輸出：`videos/vd_castle_intro_v1.mp4`。
- **交貨後 Claude 要做的**：填 `StoryVideo.CastleIntroFile`、`LineTimes`、`Subs`。
