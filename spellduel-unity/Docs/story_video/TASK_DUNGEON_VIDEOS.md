# 影片任務：所有副本的劇情影片（開場、首領登場、結局）

## 開工條件

**等 Codex 交完 `CODEX_TASK_DUNGEON_STORY_VIDEOS.md`**，`shots.json` 裡才會有這些影片的分鏡。
- 用 Codex 回報的 commit 之後的版本。
- 先 `git pull`。

## 要做的影片（26 支）

`shots.json` 裡 id 開頭是 `vd_`，而且不是 `vd_castle_intro`、`vd_castle_boss` 的每一支：
- 7 個副本各 3 支：`vd_{副本}_intro`、`vd_{副本}_boss`、`vd_{副本}_end`。
- 飛毯 `sky_carpet`、千機迴廊 `thousand_trap_gallery` 各 2 支：只有 intro、end。
- 月影古堡結局 `vd_castle_test_end`。

不要重做 v0～v3、`vd_castle_intro`、`vd_castle_boss`。

## 步驟

流程同 `README.md`、`TASK_CASTLE_BOSS_VIDEO.md`，每一支：

1. **畫面**：
   - 照 `shots.json` 生成每個鏡頭（960×544、24fps、每個 124 幀）。
   - 新角色先做定裝圖（`ref/`），再用圖生影片，臉才會一致。
   - 依序接成 `videos/{id}_v2.mp4`。
2. **配音**：
   - `ONLY={這支的每一句 mp3 名稱}` 執行 `narration_tts_cosyvoice.py`。
   - 新角色的聲音已經由 Codex 加在 `CAST`。
   - 聽一遍，念錯就換同音字。
3. **混音**：
   - `narration_mix.py` 只混這次的影片，可以加篩選，不要重混舊的。
   - 輸出 `videos/{id}_v1.mp4`，更新 `narration/timing.json`。
4. **登記到遊戲**：
   - 在 `Assets/SpellDuel/Resources/dungeon_videos.json` 的 `videos` 加一筆。
   - 格式照已經有的兩筆月影古堡：

   ```json
   { "dungeon": "ash_mine", "kind": "intro", "file": "vd_ash_mine_intro_v1.mp4", "title": "灰燼礦坑・開場", "mb": 6,
     "subs": ["…每句字幕…"], "times": [開始, 結束, 開始, 結束, …] }
   ```

   - `kind`：`intro`、`boss`、`end`。
   - `subs`：照 `narration/subs.json`，但**旁白的句子去掉「旁白：」**，角色的台詞保留「名字：」。`{玩家}` 照原樣，遊戲會換成「你」。
   - `times`：`timing.json` 裡這支的每句 [開始, 結束]，攤平成一排。
   - `mb`：檔案大小的 MB，四捨五入。
   - 登記之後，遊戲下次打包就會播。沒登記的影片不會播。
5. **推上來**：
   - `videos/*_v1.mp4`、配音 mp3、`timing.json`、`dungeon_videos.json`。
   - 推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
   - 可以一個副本一批。
   - 回報 commit SHA。
