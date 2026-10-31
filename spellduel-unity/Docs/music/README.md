# SpellDuel 配樂（地端音樂生成機做）

這份是給**音樂生成機**（地端能生成音樂的電腦）上新開的 Claude Code 工作階段看的。遊戲現在的配樂是程式即時合成的，這次要全部換成生成的配樂，連城鎮、選單的背景音樂也一起重做。

## 新工作階段怎麼開

1. 在音樂生成機上進入專案資料夾（AnimeGenerate 的 clone），切到分支：
   ```
   git checkout claude/mobile-camera-mic-battle-game-v5nhfr
   git pull
   claude
   ```
2. 對新工作階段貼這段：

   > 你是 SpellDuel 的音樂生成機。讀 `spellduel-unity/Docs/music/README.md` 和 `tracks.json`，用這台電腦的音樂生成程式把 `tracks.json` 的每一首生成出來。
   > 先只做 `title`、`ch0`、`ch0_boss`、`explore` 四首給我聽，確定風格再做其他的。做完照 README 的「交貨」推上去。

## 曲目

`tracks.json` 共 42 首：

| 類別 | 曲目 |
|---|---|
| 選單 | `title`（主畫面）、`worldmap`（世界地圖） |
| 城鎮 | `town_0`～`town_4`：炎之里、森之里、潮騷港、赤砂城、風沙驛 |
| 五章戰鬥 | `ch0`～`ch4`：熔岩、森林、海港、角鬥場、峽谷 |
| 五章 Boss | `ch0_boss`～`ch4_boss` |
| 故事探索 | `explore`（迷宮裡走路，要能聽很久）、`explore_boss` |
| 副本 | `dg_<id>`＋`dg_<id>_boss`，9 個副本共 18 首 |
| 其他模式 | `tower`（無盡塔）、`worldboss`（世界王）、`practice`（練習、每日、懸賞） |
| 短曲 | `victory`（勝利）、`defeat`（失敗），播一次、不循環 |

每一首的欄位：
- `id`：輸出檔名。
- `zh`、`use`：中文名稱與在遊戲裡哪裡播。
- `bpm`、`key`：建議的速度與調性。模型如果吃得到就填進去，吃不到就寫進提示詞，例如「140 BPM, D minor」。
- `seconds`：成品長度。
- `loop`：要不要做成無縫循環。
- `prompt`：英文提示詞。

**提示詞**＝`style` ＋ `, ` ＋ `prompt` ＋ `, {bpm} BPM, {key}`。負面提示詞（模型有支援的話）用 `negative`。

同一個副本的一般曲和首領曲（`dg_x` 與 `dg_x_boss`）、同一章的戰鬥和 Boss（`chN` 與 `chN_boss`）要聽得出是同一家族。做法：
- 模型支援參考音訊（audio-to-audio、continuation、style reference）的話，Boss 版拿一般版當參考。
- 不支援的話，就用同樣的樂器描述。提示詞已經這樣寫好了。

## 規格（重要）

- **純音樂、不要人聲**。有歌詞、唸白、明顯人聲的重生成。
- **循環**（`loop: true`）。生成模型通常不會給完美循環，要後製：
  1. 生成比 `seconds` 長一點（例如 2 分鐘）。
  2. 挑兩個在小節線上的點（看 BPM 算：一小節＝240/BPM 秒），中間長度接近 `seconds`。
  3. 剪出來，最後 1～2 小節和開頭交叉淡化接起來（ffmpeg `acrossfade`，或用 Python 自己混），讓結尾自然接回開頭。
  4. 頭尾都**不能淡出到無聲**：遊戲會直接從頭重播。
  5. 自己檢查：把成品接兩次聽接縫，不能有爆音、拍子跳掉、音量落差。
- 短曲（`victory`、`defeat`）：照秒數，結尾要乾淨收在和弦上，可以淡出。
- **音量一致**：全部做響度正規化到 **−16 LUFS**，峰值 −1 dBTP（`ffmpeg -af loudnorm=I=-16:TP=-1:LRA=11`）。
- 格式：**MP3，44.1kHz 立體聲，128kbps**。90 秒一首約 1.4MB。
- 檔名就是 `id`，例如 `ch0_boss.mp3`。

## 交貨

1. 先輸出到 `spellduel-unity/Docs/music/out/`（不要 commit）。
2. 檢查過的放進 `spellduel-unity/Docs/music/tracks/`，檔名用 `id`。
3. 重做某首就直接覆蓋同檔名。
4. commit 訊息寫做了哪幾首，推到 `claude/mobile-camera-mic-battle-game-v5nhfr`。
5. 可以分批推，不用 42 首一次做完。

遊戲端（下載、循環播放、換曲淡入淡出、哪裡播哪首）由 Claude 在雲端接，跟劇情影片一樣：從 GitHub 下載，不打包進 App。還沒下載好、或還沒做的曲子，就先用現在程式合成的配樂頂著。
