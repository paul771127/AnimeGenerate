# Codex 任務：副本都做成故事（劇本加長＋每段劇情的影片分鏡）

## 使用者的要求

> 把副本都做成故事吧，需要的動畫也一併交辦。

範本是月影古堡（`castle_test`），它已經完整做好：
- 開場劇情＋影片 `vd_castle_intro`。
- 走進首領房時戰鬥暫停，播首領登場劇情＋影片 `vd_castle_boss`，播完敵人才撲過來。

其他副本也要做成這樣：**開場 → 首領登場 → 結局**三段完整的劇情，每段都有影片。

## Claude 已經做好的部分（遊戲端，不用改程式）

| 段落 | 遊戲裡什麼時候播 | 對應 `dungeons.txt` 的哪裡 |
|---|---|---|
| 開場 intro | 第一次進副本、進地圖前；「📖 看故事」重看 | 副本的 `<before>` |
| 首領登場 boss | 走進最後的首領房，戰鬥暫停，播完敵人才撲過來（單人；連線改字幕卡） | 最後首領地點的 `<enter>` |
| 結局 end | 通關後；「📖 看故事」重看（通關過） | 副本的 `<after>` |

- **影片的清單**：`Assets/SpellDuel/Resources/dungeon_videos.json`。影片還沒交之前，遊戲用對話框播同樣的台詞。
- **規則**：影片的配音＝那一段的台詞，一句一個鏡頭。所以有影片時，遊戲就不再跳同樣的對話框。**劇本和分鏡的台詞必須一字不差。**

## 任務一：劇本（只改 `Assets/SpellDuel/Resources/dungeons.txt`）

格式見 `DUNGEON_FORMAT.md`，寫作要求同 `DUNGEON_STORY_BRIEF.md`：每句 60 字以內，用 `{玩家}` 稱呼主角，不能和 `story.txt` 的主線矛盾。

| 副本 | 開場 `<before>` | 首領登場（最後首領地點的 `<enter>`） | 結局 `<after>` |
|---|---|---|---|
| ash_mine 灰燼礦坑 | 5～8 句 | 3～6 句 | 5～8 句 |
| ember_bell_foundry 餘燼鑄鐘坊 | 5～8 句 | 3～6 句 | 5～8 句 |
| mist_name_grove 霧燈記名徑 | 5～8 句 | 3～6 句 | 5～8 句 |
| rot_temple 腐根神殿 | 5～8 句 | 3～6 句 | 5～8 句 |
| white_sail_echo_reef 白帆回聲礁 | 5～8 句 | 3～6 句 | 5～8 句 |
| sunken_throne 沉沒王座 | 5～8 句 | 3～6 句 | 5～8 句 |
| dawn_gate 黎明之門 | 5～8 句 | 3～6 句（只有最後一位首領） | 5～8 句 |
| sky_carpet 天穹航路 | 5～8 句 | （不用，飛毯沒有首領房暫停） | 5～8 句 |
| thousand_trap_gallery 千機迴廊 | 5～8 句 | （不用，機關房流程不同） | 5～8 句 |
| castle_test 月影古堡 | （已完成，不要改） | （已完成，不要改） | **新寫** 5～8 句：惡靈倒下、城堡的鐘不再響、被叫走的村民回家 |

- **開場**：交代為什麼來、這裡發生了什麼。有一個畫面感強的鉤子，例如月影古堡的「聽見鐘聲的人再也沒有回來」。
- **首領登場**：
  - 首領要親自說話（`名字（boss）：…`）。
  - 最後一句要是開打的號令，因為播完敵人就撲過來。
- **結局**：解決這個副本的事件，並且留一句跟主線（晨鐘、鐘舌、黑晶、白帆號、「讓黎明停止的人」）相連的伏筆。
- **現有台詞**：保留現有劇情的方向，可以改寫、加長，但不要刪掉主線伏筆。

## 任務二：影片分鏡與配音稿（`Docs/story_video/` 的資料檔）

每個段落一支影片。共 **26 支**：7 個副本 × 3 段 ＋ 飛毯、千機迴廊各 2 段 ＋ 月影古堡的結局 1 段。

- **id**：`vd_{副本id}_{intro|boss|end}`，例如 `vd_ash_mine_intro`。月影古堡的結局是 `vd_castle_test_end`。

格式照現有的 `vd_castle_intro`、`vd_castle_boss`：

1. **`shots.json` 的 `videos`**：每支一筆，有 `id`、`title`、`play`、`shots`。
   - 鏡頭數＝那一段的台詞句數，一句一個鏡頭，每個 5 秒。
   - 鏡頭 `id`：`vd_ash_mine_intro_s01`…
   - `zh`：中文畫面說明。
   - `prompt`：英文提示詞，角色用 `{角色記號}`。
   - 新角色、新場景加進 `characters`：
     - 各副本的首領、劇情人物、場景。
     - 外觀要和遊戲裡一致：有立繪的照 `Resources/Story2D/chars/`；有 2D 圖的照 `Docs/*_2d_preview/`。
     - 玩家只拍背影或剪影（`{Player}`）。
2. **`narration/emotion_script.json` 的 `videos`**：
   - key 是 `vd_…_intro.mp4`。
   - 每句一筆：`file`（`vd_…_intro_00.mp3`…）、`speaker`、`text`、`emotion`、`direction`、`tts_text`、`tts_emotion`、`tts_direction`。
   - `text`＝`dungeons.txt` 的那一句，一字不差。
   - `tts_text`：轉簡體；`{玩家}` 改成「你」；罕用字換同音常用字；不要用拼音標記（見 `tts_note`）。
   - 新的說話角色加進 `voices`，寫清楚聲音描述。
3. **`narration/subs.json`**：key 同上，每句寫成 `說話的人：台詞`。
4. **`tools/narration_tts_cosyvoice.py` 的 `CAST`**：
   - 新的說話角色各加一行，從現有參考音挑最接近的一個：`zh_cn_female.wav`、`zh_female_warm.wav`、`zh_male_calm.wav`、`zh_cn_male.wav`、`zh_female_bright.wav`。
   - 加上後製，參考現有的老焰、巨像、樹王、魔劍士惡靈。
   - 首領的聲音不要都一樣。

## 交貨

1. 推到 `claude/mobile-camera-mic-battle-game-v5nhfr`，回報**完整的 commit SHA**。
   - 不要加 `[skip ci]`。
   - 可以分批，建議依副本等級順序。
2. Claude 會檢查：
   - 劇本和配音稿的每一句是否一字不差。
   - 句數＝鏡頭數。
   - JSON 格式。

之後再轉給影片生成機（`Docs/story_video/TASK_DUNGEON_VIDEOS.md`）。
