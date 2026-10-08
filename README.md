# AnimeGen 角色動畫生成器

**一張角色圖 + 文字描述動作 → 這個角色做出那個動作的 GIF。**
全部在自己電腦的顯示卡上跑,使用開源免費模型,不需要付費 API。

有兩種做法:

| | 多張圖逐格動畫(推薦) | 影片模型 |
|---|---|---|
| 原理 | 把動作拆成 4–6 個關鍵姿勢,AI 參考原圖**一張張畫**,再串成 GIF | 圖生影片模型直接產生連續影片 |
| 效果 | 像遊戲角色的逐格動畫,每格姿勢可控 | 動作流暢 |
| 可以修 | ✅ 不滿意的格子可以單獨重畫、改姿勢描述 | ❌ 只能整段重跑 |
| 指令 | `animegen frames` | `animegen generate` |

```
角色圖.png + 「揮手打招呼」
   → 拆成姿勢:舉起右手 → 手揮向左 → 手揮向右 → 手揮向左 → 放下
   → AI 從原圖畫出每個姿勢(每格同一個 seed,角色比較一致)
   → animation.gif
```

## 硬體需求

需要 **NVIDIA 顯示卡**。`auto` 會依顯卡記憶體 (VRAM) 自動選模型,不夠時自動 4-bit 量化。

**多張圖模式(圖片編輯模型)**

| 模型 | VRAM | 說明 |
|---|---|---|
| `qwen` Qwen-Image-Edit-2509(推薦) | 16 GB+(4-bit 量化) | 中文描述也看得懂,改姿勢時角色一致性好。Apache-2.0 可商用。第一次下載約 58 GB |
| `kontext` FLUX.1 Kontext dev | 8–12 GB(4-bit 量化) | 只懂英文(內建動作會自動用英文)。**非商用授權**,需先到[模型頁](https://huggingface.co/black-forest-labs/FLUX.1-Kontext-dev)同意授權並 `huggingface-cli login` |

**影片模式**

| 後端 | VRAM | 說明 |
|---|---|---|
| `wan22` Wan2.2 TI2V-5B(推薦) | 12 GB+ | 品質最好,看得懂中文 |
| `ltx` LTX-Video 2B | 8 GB+ | 速度最快,需用英文 |
| `cogvideox` CogVideoX-5B-I2V | 5 GB+(很慢) | 需用英文 |
| `wan21` Wan2.1 I2V-14B | 32 GB+ | 最大最慢 |

沒有 GPU 時會退回 `mock`:**只用來測試流程,不會真的照描述做動作**。

## 安裝

```bash
python -m venv .venv && source .venv/bin/activate     # Windows: .venv\Scripts\activate
# 1. 先裝符合你 CUDA 版本的 PyTorch,例如:
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124
# 2. 再裝本專案
pip install -e ".[gpu]"
animegen info          # 確認有抓到顯卡、會用哪個模型
```

第一次生成會自動下載模型。也可以先下載好,之後離線使用:`animegen download qwen`

## 使用

### 網頁介面(最簡單)

```bash
animegen ui            # 打開 http://127.0.0.1:7860
```

「多張圖逐格動畫」分頁:
1. 上傳角色圖,輸入動作(例如「揮手打招呼」)
2. 按「① 拆成姿勢」看看會畫哪幾格,**可以直接修改**,每行一格
3. 按「② 生成動畫」
4. 哪一格畫壞了 → 「重畫某一格」換 seed 或改描述重畫;想調速度 → 改「每格停留」後按「套用播放設定」(不用重畫)

### 命令列

```bash
# 內建動作(自動拆成姿勢)
animegen frames -i 角色.png -a "揮手打招呼"

# 自己指定每一格的姿勢(效果最好)
animegen frames -i 角色.png -p "舉起右手,微笑" "右手揮向左邊" "右手揮向右邊" "右手放下"

# 播放設定:每格 120ms、格與格之間加 1 張淡入淡出、來回播放
animegen frames -i 角色.png -a "跳舞" --frame-ms 120 --tweens 1 --pingpong

# 影片模式
animegen generate -i 角色.png -a "揮手打招呼,然後開心地跳起來"
```

`frames` 參數:

| 參數 | 說明 |
|---|---|
| `-a` | 動作描述;也可以用 `→` 或 `;` 分隔多個姿勢,例如 `"舉手→揮向左→揮向右"` |
| `-p` | 直接列出每一格的姿勢 |
| `-c` | 角色外觀描述(選用),例如「銀色長髮、穿水手服的少女」,幫助維持角色一致 |
| `-m` | 圖片模型 `qwen` / `kontext` / `mock`,預設 auto |
| `-n` | 沒有內建範本時拆成幾格(預設 4) |
| `--seed` | 每一格都用同一個 seed;不滿意就換 seed 再跑 |
| `--steps` | 每格推論步數(越多越精細越慢) |
| `--frame-ms` / `--tweens` / `--pingpong` | 每格停留毫秒數 / 過渡格數 / 來回播放 |
| `--no-original` | 第一格不放角色原圖 |
| `--ollama` | 沒有內建範本的動作,用本機 [Ollama](https://ollama.com) 拆成姿勢 |

**內建動作範本**:揮手、跳、點頭、鞠躬、走路、跑步、轉圈、跳舞、歡呼、拍手、出拳、踢(中英文關鍵字都可以)。
其他動作建議自己每行寫一個姿勢,或開啟 `--ollama`。

輸出在 `outputs/<時間>_<動作>_<seed>/`:`animation.gif`、每格 `frame_XX.png`、`poses.txt`。
所有參數也可寫在 `config.yaml`(參考 `config.example.yaml`)。

## 讓結果更好的訣竅

- **角色圖**:角色清楚、全身、背景單純效果最好。透明 PNG 會自動疊白底。
- **姿勢描述要具體**:寫出手、腳、頭的位置和表情,例如「右手高舉過頭,手掌向左揮,開心地笑」。
- **4–6 格最剛好**:格數多不一定比較好,每多一格就多一次可能走樣。
- 某一格角色走樣 → 只重畫那一格就好,不用整段重來。
- 動作看起來太跳 → 加 `--tweens 1` 或縮短 `--frame-ms`。
- 顯卡記憶體不夠(Out of memory)→ 在 config 設 `keyframe.max_area: 589824`(768×768)或改用 `kontext`。

## 專案結構

```
animegen/
  cli.py         命令列
  ui.py          Gradio 網頁介面
  keyframes.py   多張圖模式:拆姿勢 → 一格格畫 → GIF、重畫單格
  poses.py       動作 → 關鍵姿勢(內建範本 / 自訂 / Ollama / 通用拆法)
  editors/       圖片編輯模型(qwen / kontext / mock)
  pipeline.py    影片模式主流程
  backends/      影片模型(wan / ltx / cogvideox / mock)
  prompt.py      影片模式提示詞、Ollama 強化
  image.py / export.py / device.py   圖片前處理、輸出、顯卡偵測
tests/           用 mock 和假的 diffusers 測試(不需 GPU):pytest
```

## 授權

程式碼自由使用。模型權重各有授權:Wan、Qwen-Image-Edit 為 Apache-2.0;FLUX.1 Kontext dev 為非商用授權;LTX-Video、CogVideoX 請見各自模型頁面。
