# 故事探索特效影片（影片生成機做）

這份是給**影片生成機**（裝了 ComfyUI＋MiniMax H3 的那台 Windows 電腦，做 SpellDuel 劇情影片的同一台）上新開的 Claude Code 工作階段看的。原本的工作階段被刪掉了，新的工作階段讀完這份就能接著做。

## 新工作階段怎麼開

1. 在生成機上打開終端機，進入專案資料夾（就是放劇情影片的那個 AnimeGenerate clone）：
   ```
   git checkout claude/mobile-camera-mic-battle-game-v5nhfr
   git pull
   claude
   ```
   或用 Claude 桌面版的 Claude Code，開這個資料夾。
2. 開 ComfyUI（`D:\ComfyUI_windows_portable`，API 在 `http://127.0.0.1:8188`）。
3. 對新工作階段貼這段：

   > 你是 SpellDuel 的影片生成機。先讀 `spellduel-unity/Docs/story_video/README.md`（以前做劇情影片的方式）和 `spellduel-unity/Docs/explore_fx/README.md`（這次的工作）。
   > 參考 `spellduel-unity/Docs/story_video/tools/gen_minimax_ref.py`、`gen_minimax_kf.py` 呼叫 ComfyUI 的方式（MiniMax H3 模型檔名、`/free`、輸出路徑都在裡面），寫一個 `spellduel-unity/Docs/explore_fx/tools/gen_fx.py`，把 `explore_fx/shots.json` 的每個 clip 生成成 mp4。
   > 先只做 `fx_C2_discover`、`fx_D4_spores` 兩支給我看（不用圖），背景確定是純黑再做其他不用圖的；要圖的等 Codex 的 `keyframes/` 推上來再做。做完照 README 的「交貨」推上去。

## 這次要做什麼

`shots.json` 裡 25 支短特效，用在故事探索「翠蔭迷林」。提示詞的來源與遊戲裡的用途在 `spellduel-unity/EXPLORE_ANIMATION_PROMPTS.md`。

- 提示詞＝`style` ＋ `clip.prompt` ＋ `backgrounds[clip.background]`；負面提示詞用 `negative`。
- **有 `keyframe` 欄位的 16 支用圖生影片**：圖是 Codex 從遊戲模型算出來的（`keyframes/`，單子是 `CODEX_TASK_EXPLORE_FX_KEYFRAMES.md`），影片裡的東西才會跟遊戲一樣。
  - `keyframe: "start"`：`keyframes/<id>.png` 當第一格；`loop` 的同一張也當最後一格。
  - `keyframe: "end"`：`keyframes/<id>_end.png` 當最後一格。
  - 圖還沒出來就先跳過那支（或先文字生影片當試做，之後重做）。
- 其他 9 支只有光和粒子，文字生影片就好。
- 設第一格／最後一格的寫法參考 `gen_minimax_kf.py`（MiniMaxH3ImageToVideo）。`loop` 的文字生影片做不到首尾相同也沒關係，Claude 會做淡入淡出接起來。
- `seconds` 是遊戲裡用的長度。照模型平常的長度生成（約 5 秒）就好，裁切、加速由 Claude 處理。

## 規格（重要）

mp4 **沒有透明**，所以背景要能讓遊戲去掉：

| `background` | 背景 | 遊戲怎麼去背 |
|---|---|---|
| `black` | 全黑，不能有地板、霧、暗角 | 加法混色（黑色＝透明），適合發光、火、光點 |
| `green` | 純綠 `#00FF00`，平均、沒有陰影 | 去綠幕，適合不發光的東西（鑰匙、草） |
| `full` | 整個畫面都是內容 | 直接貼在湖面上 |

- 主體放在畫面中間，不要碰到邊。鏡頭不要動。
- 960×544、24 fps、H.264。**拿掉聲音軌**（H3 會生成聲音）：`ffmpeg -i in.mp4 -an -c:v copy out.mp4`。
- 不要有字。地名橫幅只要空的框，地名遊戲自己放。
- 背景沒變乾淨（有地面、有灰霧、綠色不均勻）就重生成，或換 seed。

## 交貨

1. 先輸出到 `spellduel-unity/Docs/explore_fx/out/`（不要 commit）。
2. 檢查過的放進 `spellduel-unity/Docs/explore_fx/videos/`，檔名就是 `id`，例如 `fx_D3_fire.mp4`。
3. 每支 ≤ 3MB（太大就 `-crf 28`）。
4. commit 訊息寫哪幾支，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
5. 重做某支就直接覆蓋同檔名。

遊戲端（下載、去背、在觸發點播放）由 Claude 在雲端接，跟劇情影片一樣從 GitHub raw 下載，不打包進 App。

## 哪些不用做成影片

門打開、機關下沉、木門旋開、樹叢搖擺、旗子飄、洞窟水滴這類**立體的動作**，做成平面影片在 AR 裡角度會對不上，改由 Claude 用程式直接動 Codex 模型上有命名的子物件；影片只做疊在上面的光和粒子（例如 `fx_A_unseal_burst`）。
