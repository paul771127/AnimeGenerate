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

### v6／v7：畫面配合聲音、拼音標記取消

- **v6（已完成，Claude 在雲端用 v5 的配音混的）**：`narration_mix.py` 改成畫面配合聲音。
  - 每個鏡頭至少放到「台詞講完 +0.35 秒」：先放慢，最多 1.3 倍，不夠再停在最後一格。
  - 每句都在自己的鏡頭開頭開口，所以畫面和聲音對得上。
  - 鏡頭交界實測在 `floor(i×124.333)` 幀，不是 5.184 秒。
- **v5 的拼音標記會把句子念壞**：`[zh][ǐ][h][ái]` 讓海斗那句變成「只寒儿再爱上」，已全部拿掉。罕用字改用同音常用字（凪→止）。
- **老焰後製減輕**：降音 0.9→0.95，顫音 0.06→0.025。太重會把字糊掉，聽起來像念錯；老人感主要靠指令。
- **v7 重做（生成機）**：
  1. 在 `narration/` 執行 `python ../tools/narration_tts_cosyvoice.py .`。
  2. 執行 `python ../tools/narration_mix.py v7`。
  3. 推上 `videos/*_v7.mp4`、`narration/timing.json` 和配音 mp3。

### 配音 v5（待生成機重做）：修正念錯字、字幕對不上

- **念錯字的原因**：
  - 送進 CosyVoice 的是繁體原文；模型以簡體訓練，繁體字和多音字（還、處、便、重、種、散、給、紀）容易念錯。
  - 「凪」是日文漢字，模型不認得。
  - 巨像的參考音是粵語、樹王的是英文，普通話會走音。
- **改法**：
  - `narration/emotion_script.json` 每句多了 `tts_text`：簡體，多音字用 CosyVoice3 的發音修正格式標讀音，例如 `家[h][ái]在这里`、`[zh][ǐ]`（凪）。字幕仍用 `text`。
  - 腳本改送 `tts_text`；巨像改用 `zh_cn_male.wav`、樹王改用 `zh_male_calm.wav`（後製不變）。
  - 還有念錯的字：在 `tts_text` 該字的位置改成 `[聲母][帶聲調韻母]` 即可。
- **字幕對不上的原因**：遊戲以前每 5.184 秒（一個鏡頭）換一句字幕，但混音時每句接在上一句後面，最多晚了 7 秒；分段播放的切點也會切到台詞。
  - 遊戲已改成照每句實際時間顯示（`StoryVideo.cs` 的 `LineTimes`，v4 的數字已經算好填入）。
  - `narration_mix.py` 現在會輸出 `narration/timing.json`。
- **重做步驟（生成機）**：
  1. `git pull`，開 ComfyUI。
  2. 在 `narration/` 執行 `python ../tools/narration_tts_cosyvoice.py .`。
  3. 再執行 `python ../tools/narration_mix.py v5`。
  4. 聽一遍，有念錯的字就改 `tts_text` 後用 `ONLY=...` 重做那幾句。
  5. 推上 `videos/*_v5.mp4` 和 `narration/timing.json`、`tts_v5_log.json`。遊戲端（檔名改 v5、貼上 LineTimes）由 Claude 接。

### 角色配音（v4，目前使用）：本機 CosyVoice3，帶情緒

- 工具：影片生成機上 ComfyUI 的 **CosyVoice3**（`Fun-CosyVoice3-0.5B`，節點包 `ComfyUI_FL-CosyVoice3` 的 `FL_CosyVoice3_Instruct2`）。這台電腦沒有裝 IndexTTS2／GPT-SoVITS／F5-TTS，CosyVoice3 是唯一能用文字指令控制情緒的本機模型。
- 每句的台詞、情緒與語氣指示在 `narration/emotion_script.json`；腳本 `tools/narration_tts_cosyvoice.py` 把它們組成中文指令送給模型，例如「你是苍老的老爷爷，声音低沉沙哑……请用悲傷、懷念的情绪说这句话，『……可它啞了』要停頓、嘆氣般說出。」
- 每個角色固定一段參考聲音（ComfyUI `input/` 裡的 5～9 秒中文樣本），全程同一個聲線：

  | 角色 | 參考聲音 | 後製 |
  |---|---|---|
  | 旁白 | `zh_cn_female.wav` | 暖色 EQ、短殘響 |
  | 艾琳 | `zh_female_warm.wav`（偏溫柔） | 同上 |
  | 老焰 | `zh_male_calm.wav` + 指令「蒼老的老爺爺」 | 降音 0.9、補回長度、顫音、低通 |
  | 熔岩巨像 | `zh_cantonese_male.wav` | 降音 0.78、低通 3 kHz、回聲 |
  | 腐化樹王 | `en_male_friendly.wav` | 降音 0.86、chorus 多重聲、回聲 |
  | 海斗 | `zh_cn_male.wav` | 暖色 EQ、短殘響 |
  | 澪 | `zh_female_bright.wav` | 長殘響、高通（空靈） |

- 語速：基本 1.0（老焰、巨像、樹王、澪 0.95），指示寫「放慢／停頓」再 −0.03、「急／喊」+0.08；太長的四句（老焰、巨像、樹王×2）另外 ×1.15 重做。句中停頓用 ffmpeg 壓到最多 0.4 秒，響度統一 −17 LUFS。每句的最終設定記在 `narration/tts_v4_log.json`。
- 混音同 v3 的方式（`tools/narration_mix.py`，改成可帶版本參數），畫面用 `*_v2.mp4`，輸出 `videos/*_v4.mp4`。語速最多 ×1.06；序章配音比畫面長約 5 秒，最後一格多停一下。
- 重做：ComfyUI 開著，在 `narration/` 底下執行 `python ../tools/narration_tts_cosyvoice.py .`，再 `python ../tools/narration_mix.py v4`。只重做幾句：`ONLY=v1_golem_01,v0_prologue_02 SPEED_MUL=1.15`。角色聲音與後製在 `CAST`。

### 角色配音（v3，已被 v4 取代）
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
- 重做方式：在 `narration/` 底下執行 `python3 ../tools/narration_tts.py` 和 `python3 ../tools/narration_mix.py v3`。角色的聲音設定在 `narration_tts.py` 的 `CAST`。
- 還是覺得有 AI 感的話，可以換成真人錄音，或用在本機 GPU 跑的聲音複製模型（例如 GPT-SoVITS）。只要把 `narration/` 裡同名的 mp3 換掉，再跑一次 `narration_mix.py` 就好。

### 旁白配音（v2，已被 v3 取代；`*_v2.mp4` 保留給 v3 當畫面來源）

- 影片原本每個鏡頭各自生成的配樂已拿掉，改成**同一個旁白**（edge-tts `zh-TW-HsiaoChenNeural`）念每個鏡頭的字幕；播放時背景音樂用遊戲的城鎮曲（壓低音量）。
- 字幕與旁白文字：`narration/subs.json`（也寫在 `StoryVideo.cs` 的 `Subs`；旁白的句子不標「旁白：」）。
- 重做：`tools/narration_tts.py` 產生每句的 mp3（`narration/`），`tools/narration_mix.py` 把旁白放到每個鏡頭開頭、太長就稍微加速，合成 `videos/*_v2.mp4`（畫面不重新編碼）。
