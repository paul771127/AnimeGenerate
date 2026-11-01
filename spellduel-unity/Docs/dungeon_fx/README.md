# 副本大地圖特效影片（影片生成機做）

這份給**影片生成機**上新開的 Claude Code 工作階段看。影片生成機就是裝了 ComfyUI＋MiniMax H3、做劇情影片和翠蔭迷林特效的那台 Windows 電腦。

副本已經全部改成像翠蔭迷林的大地圖（天穹航路除外），這次要做大地圖用的特效影片。

## 新工作階段怎麼開

1. 在生成機上進入專案資料夾，也就是放劇情影片的那個 AnimeGenerate clone：
   ```
   git checkout claude/mobile-camera-mic-battle-game-v5nhfr
   git pull
   claude
   ```
2. 開 ComfyUI（`D:\ComfyUI_windows_portable`，API 在 `http://127.0.0.1:8188`）。
3. 對新工作階段貼這段：

   > 你是 SpellDuel 的影片生成機。先讀 `spellduel-unity/Docs/explore_fx/README.md`（翠蔭迷林特效的做法）和 `spellduel-unity/Docs/dungeon_fx/README.md`（這次的工作）。
   > 照 `explore_fx/tools/` 裡的生成腳本，把 `dungeon_fx/shots.json` 的每個 clip 生成成 mp4。
   > 先只做 `fx_DG_gate_open_volcano`、`fx_DG_area_clear`、`fx_DG_lava_vent` 三支給我看。確定背景是純黑再做其他的。做完照 README 的「交貨」推上去。

   如果 `explore_fx/tools/` 裡沒有腳本，就參考 `spellduel-unity/Docs/story_video/tools/gen_minimax_ref.py`、`gen_minimax_kf.py` 呼叫 ComfyUI 的方式自己寫一個。MiniMax H3 的模型檔名、`/free`、輸出路徑都在那兩個檔案裡。

## 這次要做什麼

`shots.json` 裡有 26 支短特效，**全部是文字生影片**，只做光和粒子，不用圖。

- **提示詞**＝`style` ＋ `clip.prompt` ＋ `backgrounds[clip.background]`；負面提示詞用 `negative`。
  - 這三個欄位和翠蔭迷林的一樣。
- **`mode`**：`once` 播一次；`loop` 一直循環。
  - 循環的做不到首尾相同也沒關係，Claude 會用淡入淡出接起來。
- **`seconds`**：遊戲裡用的長度。照模型平常的長度生成（約 5 秒）就好，裁切、加速由 Claude 處理。

| 類別 | 影片 | 用途 |
|---|---|---|
| 出口封印（6 主題 × 2） | `fx_DG_gate_idle_<主題>` | 區域還沒清除時，出口門上一直播的結界 |
| | `fx_DG_gate_open_<主題>` | 清除時結界碎掉 |
| 區域 | `fx_DG_area_enter` | 走進戰鬥或事件區域時的警告圈 |
| | `fx_DG_area_clear` | 區域清除時的金光 |
| | `fx_DG_chest_open` | 寶箱地點打開 |
| | `fx_DG_event_start` | 特殊事件開始 |
| 首領 | `fx_DG_throne_aura` | 王座入口的不祥氣息（循環） |
| | `fx_DG_boss_rise` | 最後的首領現身 |
| | `fx_DG_midboss_rise` | 中途首領現身 |
| 環境（循環） | `fx_DG_lava_vent` | 熔岩 |
| | `fx_DG_forest_fireflies` | 森林 |
| | `fx_DG_sea_spray` | 海上 |
| | `fx_DG_arena_sand` | 角鬥場 |
| | `fx_DG_canyon_dust` | 峽谷 |
| | `fx_DG_clockwork_steam` | 千機迴廊 |
| 千機迴廊 | `fx_DG_trap_room_clear` | 一個機關房過完 |

主題是 `volcano`、`forest`、`sea`、`arena`、`canyon`、`clockwork`，各自的顏色和粒子已經寫在提示詞裡。

## 規格（重要，跟翠蔭迷林一樣）

- **全部是黑底**（`black`）：遊戲用加法混色去背，黑色就是透明。
  - 畫面裡不能有地板、灰霧、暗角。
  - 背景沒變乾淨就重生成或換 seed。
- 主體放在畫面中間，不要碰到邊。鏡頭不要動。
- 960×544、24 fps、H.264。
- **拿掉聲音軌**：`ffmpeg -i in.mp4 -an -c:v copy out.mp4`。
- 不要有字。
- 每支 3MB 以內，太大就用 `-crf 28`。

## 交貨

1. 先輸出到 `spellduel-unity/Docs/dungeon_fx/out/`，不要 commit。
2. 檢查過的放進 `spellduel-unity/Docs/dungeon_fx/videos/`，檔名就是 `id`，例如 `fx_DG_gate_open_volcano.mp4`。
3. commit 訊息寫做了哪幾支，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
4. 可以分批推。重做某支就直接覆蓋同檔名。

遊戲端由 Claude 在雲端接，跟翠蔭迷林的特效一樣：
- App 一開就從 GitHub 下載，不打包進 App。
- 在門、區域、首領房、環境點播放。

影片推上來之後跟 Claude 說一聲就會接。

## 哪些不用做成影片

門打開、機關轉動、旗子飄這類**立體動作**由 Codex 的模型做（單子是 `CODEX_TASK_DUNGEON_MAPS.md`），影片只做疊在上面的光和粒子。
