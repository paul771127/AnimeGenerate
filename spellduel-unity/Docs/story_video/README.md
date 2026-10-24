# 劇情影片製作（另一台電腦生成）

分鏡與提示詞都在 [`shots.json`](shots.json)：
- 4 支影片：序章、三章的 Boss 戰前／通關，共 27 個鏡頭。
- 每個鏡頭約 5 秒。目前多數影片生成模型一次就是 4～6 秒，生成後再剪接。

## 檔案格式

- `style`：每個鏡頭都要加在提示詞後面的共同畫風。
- `negative`：負面提示詞。
- `characters`：角色外觀定義。提示詞裡的 `{Erin}` 這類記號要換成這裡的描述，讓同一個角色在每個鏡頭長得一樣。
- `videos[].shots[]`：
  - `id`：輸出檔名，例如 `v0_s01.mp4`。
  - `seconds`：長度。
  - `zh`：中文說明。
  - `prompt`：英文提示詞。

## 在生成影片的電腦上

1. 開一個 Claude 工作階段：Claude 桌面版，或在終端機進入專案資料夾執行 `claude remote-control`。
2. 告訴它：「讀 `spellduel-unity/Docs/story_video/shots.json`，用這台電腦的影片生成程式逐一生成每個鏡頭。」它會依你裝的程式寫批次腳本，例如：
   - ComfyUI：用它的 HTTP API 送工作流程。
   - 其他程式：用它的命令列。
3. 生成時：
   - 每個鏡頭的提示詞＝`style` **放在最前面**＋`prompt`（換掉角色記號）；負面提示詞用 `negative`。LTX 對開頭的字權重較高，風格放在後面容易變寫實。
   - LTX-2.3：寬高要是 32 的倍數（用 768×448 或 1024×576）；幀數 121（24 fps 約 5 秒）。工作流：`workflow_ltx23_t2v.json`（ComfyUI API 格式）。
   - 同一角色先生成一張「角色定裝圖」，之後用圖生影片，臉才會一致。
4. 輸出放到 `spellduel-unity/Docs/story_video/out/`，檔名用鏡頭 `id`。
5. 每支影片的鏡頭生好後，用 ffmpeg 依順序接成一支（例如 `v0_prologue.mp4`），再配上遊戲的音樂與旁白字幕。

## 放進遊戲

建議：
- 解析度 720p、H.264。
- 每支約 30～40 秒、10～20MB。
- 不要直接打包進 App（會讓安裝檔變太大），改成第一次播放時下載，或做成可選的「劇情包」。

在遊戲裡的播放時機：
- 播放時機寫在每支影片的 `play` 欄位。
- 播完後接原本的對話。
- 已經看過的影片可以在「📖 看故事」重看。

### 已經接上遊戲（StoryVideo.cs）

- **影片檔放哪裡**：完成的四支影片放在 `spellduel-unity/Docs/story_video/videos/`，檔名 `v0_prologue.mp4`、`v1_golem.mp4`、`v2_treeking.mp4`、`v3_kraken.mp4`。這個資料夾在 Assets 外面，不會被打包。
- **怎麼下載**：遊戲第一次要播時從 GitHub（raw.githubusercontent.com）下載，存在手機裡，之後不用再下載。
- **播放時機**：
  - 1-1 開場前：播序章整支。
  - 1-4、2-4、3-4 開場前：播前 4 個鏡頭；通關後播剩下的鏡頭。
  - 「📖 看故事」重看時也會播。
- **操作**：遊戲一開始就把四支影片全部下載（畫面顯示「載入中」，不能取消）；播放時下方有字幕（`StoryVideo.cs` 的 `Subs`，每個鏡頭一句）。第一次看不能跳過，「📖 看故事」重看時可以長按畫面 1 秒跳過。
- **重新生成影片**：換掉 `videos/` 裡的檔案就好（檔名不變）。已經下載過的手機要清除 App 資料，才會重新下載。

### 角色配音（v3，目前使用）
- 每個角色各自的聲音（edge-tts）：

  | 角色 | 聲音 |
  |---|---|
  | 旁白 | HsiaoChen |
  | 艾琳 | HsiaoYu |
  | 老焰 | YunJhe，降低音調、放慢，加一點顫音，聽起來像老人男聲 |
  | 熔岩巨像 | 很低、悶、有回聲 |
  | 腐化樹王 | 低沉、多重聲音 |
  | 海斗 | Yunxi |
  | 澪 | Xiaoxiao |

- 為了比較不像機器念稿，每句都做了後製：
  - 去掉頭尾的空白。
  - 加一點房間殘響和暖色 EQ。
  - 把每句的響度調成一致。
- 接進影片時每句從自己鏡頭的開頭開始。上一句還沒講完，就接在它後面，最多整體加快 1.06 倍；最後一句講不完時，最後一格畫面會多停一下。
- 輸出 `videos/*_v3.mp4`（檔名換了，手機才會重新下載）。遊戲的鏡頭切點固定 5.184 秒（`StoryVideo.ShotSec`）。
- 重做方式：在 `narration/` 底下執行 `python3 ../tools/narration_tts.py` 和 `python3 ../tools/narration_mix.py`。角色的聲音設定在 `narration_tts.py` 的 `CAST`。
- 還是覺得有 AI 感的話，可以換成真人錄音，或用在本機 GPU 跑的聲音複製模型（例如 GPT-SoVITS）。只要把 `narration/` 裡同名的 mp3 換掉，再跑一次 `narration_mix.py` 就好。

### 旁白配音（v2，已被 v3 取代；`*_v2.mp4` 保留給 v3 當畫面來源）

- 影片原本每個鏡頭各自生成的配樂已拿掉，改成**同一個旁白**（edge-tts `zh-TW-HsiaoChenNeural`）念每個鏡頭的字幕；播放時背景音樂用遊戲的城鎮曲（壓低音量）。
- 字幕與旁白文字：`narration/subs.json`（也寫在 `StoryVideo.cs` 的 `Subs`；旁白的句子不標「旁白：」）。
- 重做：`tools/narration_tts.py` 產生每句的 mp3（`narration/`），`tools/narration_mix.py` 把旁白放到每個鏡頭開頭、太長就稍微加速，合成 `videos/*_v2.mp4`（畫面不重新編碼）。
