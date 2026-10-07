# AnimeGen 角色動畫生成器

**一張角色圖 + 文字描述動作 → 這個角色做出那個動作的 GIF / MP4。**
全部在自己電腦上跑,使用開源免費的圖生影片模型(Wan2.2、LTX-Video、CogVideoX),不需要付費 API。

```
角色圖.png + 「揮手打招呼,然後開心地跳起來」 ──► 影片模型 ──► 角色動畫.gif / .mp4
```

## 硬體需求

真正讓角色「照描述做動作」需要 **NVIDIA GPU**:

| 後端 | 模型 | VRAM | 說明 |
|---|---|---|---|
| `wan22`(推薦) | Wan2.2 TI2V-5B | 12 GB+(最低 8 GB) | 品質最好,**看得懂中文**描述 |
| `ltx` | LTX-Video 2B | 8 GB+(最低 4 GB) | 速度最快,需用英文描述 |
| `cogvideox` | CogVideoX-5B-I2V | 14 GB+(最低 5 GB,很慢) | 低 VRAM 也能跑,需用英文描述 |
| `wan21` | Wan2.1 I2V-14B | 32 GB+ | 最大最慢 |
| `mock` | (無) | 不需 GPU | 只做簡單位移動畫,**用來測試流程,不會照描述做動作** |

`backend: auto` 會依你的 VRAM 自動選擇。沒有 GPU 時會退回 `mock`。
Apple Silicon (MPS) 可以跑但很慢。

## 安裝

```bash
python -m venv .venv && source .venv/bin/activate     # Windows: .venv\Scripts\activate
# 1. 先裝符合你 CUDA 版本的 PyTorch,例如:
pip install torch torchvision --index-url https://download.pytorch.org/whl/cu124
# 2. 再裝本專案
pip install -e ".[gpu]"
animegen info          # 確認有抓到 GPU、會用哪個模型
```

第一次生成會自動下載模型(Wan2.2 約 30 GB)。也可以先下載好,之後離線使用:`animegen download wan22`

## 使用

### 網頁介面

```bash
animegen ui            # 打開 http://127.0.0.1:7860
```

上傳角色圖 → 輸入動作描述 → 按「生成動畫」→ 預覽並下載 GIF / MP4。

### 命令列

```bash
animegen generate -i 角色.png -a "揮手打招呼,然後開心地跳起來"
animegen generate -i girl.png -a "turns around and waves, hair flowing" -b ltx --seed 42
animegen generate -i 角色.png -a "點頭微笑" --pingpong --interpolate 2 -f gif
```

| 參數 | 說明 |
|---|---|
| `-a` | 動作描述(必填) |
| `-c` | 角色外觀描述(選用),例如「銀色長髮、穿水手服的少女」,能幫助維持角色一致 |
| `-b` | 模型後端,預設 auto |
| `-f` | 輸出格式 `gif` `mp4` `png`(預設 gif + mp4) |
| `--frames` / `--steps` | 幀數(動畫長度)/ 推論步數(精細度);越大越慢 |
| `--seed` | 固定 seed 可重現同樣結果;不滿意就換 seed 再跑 |
| `--resolution` | 例如 `832x480`;預設依模型與 VRAM 自動決定 |
| `--pingpong` | 正放 + 倒放,做成無縫循環 |
| `--interpolate 2` | 用 ffmpeg 補幀,動作更順 |
| `--enhance` | 用本機 [Ollama](https://ollama.com) 把簡短描述改寫成詳細英文提示詞(LTX / CogVideoX 很有幫助) |

輸出在 `outputs/`。所有參數也可寫在 `config.yaml`(參考 `config.example.yaml`)。

## 讓結果更好的訣竅

- **角色圖**:角色清楚、全身或半身、背景單純效果最好。透明 PNG 會自動疊白底。
- **描述動作要具體、照時間順序**:「先舉起右手揮兩下,然後雙手握拳跳起來」比「很開心」好。
- **一次一到兩個動作**:一段動畫只有約 3–5 秒,塞太多動作會做不完。
- **鏡頭固定**:預設提示詞已要求鏡頭不動;不要描述換場景。
- 結果不滿意時先**換 seed**,再考慮增加 `--steps`。
- VRAM 不夠(Out of memory)時:降低解析度(`--resolution 640x384`)、減少幀數,或在 config 設 `memory_mode: sequential_offload`。

## 專案結構

```
animegen/
  cli.py        命令列
  ui.py         Gradio 網頁介面
  pipeline.py   主流程:讀圖 → 提示詞 → 模型 → 輸出
  backends/     各模型後端(wan / ltx / cogvideox / mock)
  prompt.py     提示詞組合、Ollama 強化
  image.py      圖片前處理
  export.py     GIF / MP4 / PNG 輸出、ffmpeg 補幀
  device.py     GPU / VRAM 偵測、自動選模型
tests/          用 mock 後端測試整條流程(不需 GPU):pytest
```

## 授權

程式碼自由使用。模型權重各有授權:Wan 為 Apache-2.0;LTX-Video、CogVideoX 請見各自模型頁面(商用可能有條件)。
