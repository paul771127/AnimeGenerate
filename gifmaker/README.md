# GifMaker

把圖片做成 GIF 動畫。核心只需要 Pillow(`pip install pillow`);網頁介面另外需要 `gradio`。

```bash
# 多張圖 → 輪播(每張停 0.8 秒,淡入淡出 0.3 秒)
python gifmaker.py a.png b.png c.png -o slide.gif --hold 0.8 --fade 0.3

# 多張圖 → 逐格動畫(每張一幀,每秒 12 幀)
python gifmaker.py frames/*.png --hold 0 --fps 12 -o anim.gif

# 單張圖 → 加特效
python gifmaker.py cat.png -e bounce -o cat.gif
python gifmaker.py logo.png -e spin --frames 40 --bg "#000000" -o spin.gif

# 網頁介面(瀏覽器開 http://127.0.0.1:7860)
pip install gradio
python gifmaker.py --ui
```

| 特效 | 說明 |
|---|---|
| `zoom` | 緩慢推近再拉遠 |
| `pulse` | 心跳 / 呼吸縮放 |
| `shake` | 左右抖動 |
| `bounce` | 彈跳(落地壓扁) |
| `spin` | 旋轉一圈 |
| `swing` | 鐘擺擺盪 |
| `float` | 上下漂浮 |
| `wobble` | 果凍晃動 |

特效都是週期函數,最後一幀能無縫接回第一幀。多張圖加 `-e` 時,每張圖各套用一次特效後串接。

| 參數 | 預設 | 說明 |
|---|---|---|
| `--fps` | 15 | 每秒幀數 |
| `--hold` / `--fade` | 1.0 / 0.4 | 輪播停留 / 淡入淡出秒數;`--hold 0` = 逐格動畫 |
| `--frames` / `--amount` | 30 / 1.0 | 特效幀數 / 強度 |
| `--width` | 第一張圖寬(上限 800) | 輸出寬度,高度依比例 |
| `--fit` | contain | 圖片尺寸不同時:`contain` 補邊、`cover` 裁切、`stretch` 拉伸 |
| `--bg` | #ffffff | 背景色(透明圖會疊在這個顏色上) |
| `--loop` | 0 | 重複次數,0 = 無限 |
| `--pingpong` | 關 | 正放 + 倒放 |

也可以在 Python 裡直接呼叫:

```python
from gifmaker import make_gif
make_gif(["a.png", "b.png"], "out.gif", hold=0.5, fade=0.2)
make_gif(["cat.png"], "cat.gif", effect="wobble", amount=1.5)
```
