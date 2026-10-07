"""GifMaker - 把圖片做成 GIF 動畫。只需要 Pillow。

兩種模式:
  1. 多張圖 → 輪播 GIF(可加淡入淡出轉場)
  2. 單張圖 → 加上動態特效的 GIF(縮放、搖晃、彈跳、旋轉、呼吸、擺盪...)

用法:
  python gifmaker.py a.png b.png c.png -o out.gif --hold 0.8 --fade 0.3
  python gifmaker.py cat.png -e bounce -o cat.gif
  python gifmaker.py frames/*.png --fps 12 -o anim.gif   # 逐格動畫
  python gifmaker.py --ui                                 # 網頁介面(需 gradio)
"""
from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path
from typing import Callable, Sequence

from PIL import Image, ImageOps

Color = tuple[int, int, int]

# ---------------------------------------------------------------- 圖片前處理


def parse_color(text: str) -> Color:
    """'#ffffff' / 'ffffff' / '255,255,255' / 'white' → (r, g, b)。"""
    from PIL import ImageColor

    text = text.strip()
    if "," in text:
        parts = [int(p) for p in text.split(",")]
        if len(parts) != 3:
            raise ValueError(f"顏色格式錯誤: {text!r}")
        return tuple(parts)  # type: ignore[return-value]
    if len(text) == 6 and all(c in "0123456789abcdefABCDEF" for c in text):
        text = "#" + text
    return ImageColor.getrgb(text)[:3]


def flatten(img: Image.Image, bg: Color) -> Image.Image:
    """透明圖疊到純色底上,輸出 RGB。"""
    img = ImageOps.exif_transpose(img)
    if img.mode in ("RGBA", "LA") or (img.mode == "P" and "transparency" in img.info):
        rgba = img.convert("RGBA")
        base = Image.new("RGBA", rgba.size, bg + (255,))
        base.alpha_composite(rgba)
        return base.convert("RGB")
    return img.convert("RGB")


def fit(img: Image.Image, size: tuple[int, int], mode: str, bg: Color) -> Image.Image:
    """把圖調整成 size。contain=完整放入補邊,cover=填滿裁切,stretch=拉伸。"""
    if img.size == size:
        return img
    if mode == "cover":
        return ImageOps.fit(img, size, Image.LANCZOS)
    if mode == "stretch":
        return img.resize(size, Image.LANCZOS)
    return ImageOps.pad(img, size, Image.LANCZOS, color=bg)


def target_size(images: Sequence[Image.Image], width: int | None) -> tuple[int, int]:
    """以第一張圖的長寬比為準;width 未指定時以第一張圖寬度為準(上限 800)。"""
    w0, h0 = images[0].size
    w = width or min(w0, 800)
    h = max(1, round(h0 * w / w0))
    return w, h


def load_images(paths: Sequence[str | Path], bg: Color) -> list[Image.Image]:
    out = []
    for p in paths:
        with Image.open(p) as im:
            out.append(flatten(im, bg))
    if not out:
        raise ValueError("至少需要一張圖片")
    return out


# ---------------------------------------------------------------- 多張圖:輪播


def slideshow(images: Sequence[Image.Image], hold: int, fade: int, loop_fade: bool = True) -> list[Image.Image]:
    """每張圖停留 hold 幀,圖與圖之間用 fade 幀做淡入淡出。"""
    frames: list[Image.Image] = []
    n = len(images)
    for i, img in enumerate(images):
        frames.extend([img] * max(1, hold))
        nxt = i + 1
        if nxt == n:
            if not (loop_fade and n > 1):
                break
            nxt = 0
        for k in range(1, fade + 1):
            frames.append(Image.blend(img, images[nxt], k / (fade + 1)))
    return frames


# ---------------------------------------------------------------- 單張圖:特效
#
# 每個特效是 t ∈ [0, 1) → 變換參數的週期函數,所以最後一幀能無縫接回第一幀。
# dx / dy 以「畫面寬 / 高的比例」表示,在 animate() 換算成像素。


def _transform(img: Image.Image, bg: Color, *, scale=1.0, angle=0.0, dx=0.0, dy=0.0,
               sx=1.0, sy=1.0, anchor="center") -> Image.Image:
    """以畫面中心(或底部中心)為基準做縮放 / 旋轉 / 位移,輸出尺寸不變。"""
    w, h = img.size
    nw, nh = max(1, round(w * scale * sx)), max(1, round(h * scale * sy))
    layer = img.convert("RGBA").resize((nw, nh), Image.BICUBIC)
    if angle:
        layer = layer.rotate(angle, Image.BICUBIC, expand=True)
    canvas = Image.new("RGBA", (w, h), bg + (255,))
    x = (w - layer.width) / 2 + dx
    y = (h - layer.height) if anchor == "bottom" else (h - layer.height) / 2
    canvas.paste(layer, (round(x), round(y + dy)), layer)  # paste 允許超出邊界
    return canvas.convert("RGB")


def _effect_zoom(t, amount):  # 緩慢推近再拉遠
    return dict(scale=1 + amount * 0.25 * (1 - math.cos(2 * math.pi * t)) / 2)


def _effect_pulse(t, amount):  # 心跳 / 呼吸
    return dict(scale=1 - amount * 0.08 * (1 - math.cos(2 * math.pi * t)) / 2)


def _effect_shake(t, amount):  # 左右抖動
    return dict(dx=amount * 0.03 * math.sin(2 * math.pi * t * 4), scale=1 + 0.06 * amount)


def _effect_bounce(t, amount):  # 跳一下 + 落地壓扁
    s = math.sin(math.pi * t)
    squash = max(0.0, 1 - s * 4) * 0.12 * amount  # 接近地面時壓扁
    return dict(dy=-amount * 0.15 * s, sx=1 + squash, sy=1 - squash, anchor="bottom", scale=0.85)


def _effect_spin(t, amount):  # 原地旋轉一圈
    return dict(angle=-360 * t, scale=0.75)


def _effect_swing(t, amount):  # 鐘擺
    return dict(angle=amount * 12 * math.sin(2 * math.pi * t), scale=0.9)


def _effect_float(t, amount):  # 上下漂浮
    return dict(dy=amount * 0.04 * math.sin(2 * math.pi * t), scale=0.92)


def _effect_wobble(t, amount):  # 果凍晃動
    k = 0.07 * amount * math.sin(2 * math.pi * t)
    return dict(sx=1 + k, sy=1 - k, anchor="bottom", scale=0.92)


EFFECTS: dict[str, tuple[str, Callable]] = {
    "zoom": ("緩慢推近再拉遠", _effect_zoom),
    "pulse": ("心跳 / 呼吸縮放", _effect_pulse),
    "shake": ("左右抖動", _effect_shake),
    "bounce": ("彈跳(落地壓扁)", _effect_bounce),
    "spin": ("旋轉一圈", _effect_spin),
    "swing": ("鐘擺擺盪", _effect_swing),
    "float": ("上下漂浮", _effect_float),
    "wobble": ("果凍晃動", _effect_wobble),
}


def animate(img: Image.Image, effect: str, frames: int, amount: float, bg: Color) -> list[Image.Image]:
    if effect not in EFFECTS:
        raise ValueError(f"未知特效 {effect!r},可用: {', '.join(EFFECTS)}")
    fn = EFFECTS[effect][1]
    out = []
    for i in range(frames):
        params = fn(i / frames, amount)
        params["dx"] = params.get("dx", 0.0) * img.width
        params["dy"] = params.get("dy", 0.0) * img.height
        out.append(_transform(img, bg, **params))
    return out


# ---------------------------------------------------------------- 輸出


def save_gif(frames: Sequence[Image.Image], path: str | Path, fps: float,
             loop: int = 0, pingpong: bool = False) -> Path:
    frames = list(frames)
    if pingpong and len(frames) > 2:
        frames = frames + frames[-2:0:-1]
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    # GIF 的時間單位是 1/100 秒,太短的值瀏覽器會當成 0.1 秒,所以下限 20ms
    duration = max(20, round(1000 / fps))
    frames[0].save(
        path, save_all=True, append_images=frames[1:],
        duration=duration, loop=loop, optimize=True, disposal=1,
    )
    return path


def make_gif(paths: Sequence[str | Path], output: str | Path, *, effect: str | None = None,
             fps: float = 15, hold: float = 1.0, fade: float = 0.4, frames: int = 30,
             amount: float = 1.0, width: int | None = None, fit_mode: str = "contain",
             bg: Color = (255, 255, 255), loop: int = 0, pingpong: bool = False) -> Path:
    """主要入口。effect=None 時:多張圖做輪播;effect 給定時:每張圖各套用特效後串接。

    hold / fade 單位是秒;hold=0 代表逐格動畫(每張圖一幀,依 fps 播放)。
    """
    images = load_images(paths, bg)
    size = target_size(images, width)
    images = [fit(im, size, fit_mode, bg) for im in images]

    if effect:
        seq: list[Image.Image] = []
        for im in images:
            seq.extend(animate(im, effect, frames, amount, bg))
    elif hold <= 0:
        seq = images
    else:
        seq = slideshow(images, round(hold * fps), round(fade * fps))
    return save_gif(seq, output, fps, loop=loop, pingpong=pingpong)


# ---------------------------------------------------------------- 網頁介面


def launch_ui(port: int = 7860) -> None:
    try:
        import gradio as gr
    except ImportError:
        sys.exit("網頁介面需要 gradio:pip install gradio")
    import tempfile

    effect_choices = [("無(多張圖輪播 / 逐格)", "")] + [(f"{k} - {v[0]}", k) for k, v in EFFECTS.items()]

    def run(files, effect, fps, hold, fade, frames, amount, width, fit_mode, bg, pingpong):
        if not files:
            raise gr.Error("請先上傳圖片")
        paths = [f if isinstance(f, str) else f.name for f in files]
        out = Path(tempfile.mkdtemp()) / "output.gif"
        make_gif(paths, out, effect=effect or None, fps=fps, hold=hold, fade=fade,
                 frames=int(frames), amount=amount, width=int(width) or None,
                 fit_mode=fit_mode, bg=parse_color(bg), pingpong=pingpong)
        return str(out), str(out)

    with gr.Blocks(title="GifMaker") as demo:
        gr.Markdown("# GifMaker\n上傳一張圖套特效,或上傳多張圖做成輪播 / 逐格動畫。")
        with gr.Row():
            with gr.Column():
                files = gr.File(label="圖片(可多選,依上傳順序)", file_count="multiple", file_types=["image"])
                effect = gr.Dropdown(effect_choices, value="", label="特效")
                fps = gr.Slider(1, 50, 15, step=1, label="FPS")
                with gr.Accordion("輪播設定", open=True):
                    hold = gr.Slider(0, 5, 1.0, step=0.1, label="每張停留秒數(0 = 逐格動畫)")
                    fade = gr.Slider(0, 2, 0.4, step=0.1, label="淡入淡出秒數")
                with gr.Accordion("特效設定", open=True):
                    frames = gr.Slider(8, 120, 30, step=1, label="特效幀數")
                    amount = gr.Slider(0.2, 3, 1.0, step=0.1, label="特效強度")
                with gr.Accordion("輸出", open=False):
                    width = gr.Number(0, label="寬度(0 = 自動)", precision=0)
                    fit_mode = gr.Radio(["contain", "cover", "stretch"], value="contain", label="尺寸不同時")
                    bg = gr.Textbox("#ffffff", label="背景色")
                    pingpong = gr.Checkbox(False, label="來回播放(正放 + 倒放)")
                btn = gr.Button("產生 GIF", variant="primary")
            with gr.Column():
                preview = gr.Image(label="預覽", type="filepath")
                download = gr.File(label="下載")
        btn.click(run, [files, effect, fps, hold, fade, frames, amount, width, fit_mode, bg, pingpong],
                  [preview, download])
    demo.launch(server_port=port)


# ---------------------------------------------------------------- CLI


def main(argv: Sequence[str] | None = None) -> int:
    ap = argparse.ArgumentParser(
        description="把圖片做成 GIF 動畫",
        epilog="特效: " + ", ".join(f"{k}({v[0]})" for k, v in EFFECTS.items()),
    )
    ap.add_argument("images", nargs="*", help="輸入圖片(依順序)")
    ap.add_argument("-o", "--output", default="output.gif", help="輸出檔名 (預設 output.gif)")
    ap.add_argument("-e", "--effect", choices=list(EFFECTS), help="單張圖特效")
    ap.add_argument("--fps", type=float, default=15, help="每秒幀數 (預設 15)")
    ap.add_argument("--hold", type=float, default=1.0, help="輪播每張停留秒數;0 = 逐格動畫 (預設 1.0)")
    ap.add_argument("--fade", type=float, default=0.4, help="輪播淡入淡出秒數 (預設 0.4)")
    ap.add_argument("--frames", type=int, default=30, help="特效幀數 (預設 30)")
    ap.add_argument("--amount", type=float, default=1.0, help="特效強度 (預設 1.0)")
    ap.add_argument("--width", type=int, help="輸出寬度,高度依比例 (預設 = 第一張圖,上限 800)")
    ap.add_argument("--fit", choices=["contain", "cover", "stretch"], default="contain",
                    help="圖片尺寸不同時的處理方式 (預設 contain)")
    ap.add_argument("--bg", default="#ffffff", help="背景色 (預設 #ffffff)")
    ap.add_argument("--loop", type=int, default=0, help="重複次數;0 = 無限 (預設 0)")
    ap.add_argument("--pingpong", action="store_true", help="正放 + 倒放")
    ap.add_argument("--ui", action="store_true", help="開啟網頁介面(需 gradio)")
    ap.add_argument("--port", type=int, default=7860, help="網頁介面埠號")
    args = ap.parse_args(argv)

    if args.ui:
        launch_ui(args.port)
        return 0
    if not args.images:
        ap.error("請指定至少一張圖片,或使用 --ui")

    out = make_gif(
        args.images, args.output, effect=args.effect, fps=args.fps, hold=args.hold,
        fade=args.fade, frames=args.frames, amount=args.amount, width=args.width,
        fit_mode=args.fit, bg=parse_color(args.bg), loop=args.loop, pingpong=args.pingpong,
    )
    with Image.open(out) as im:
        n = getattr(im, "n_frames", 1)
        size = im.size
    print(f"已輸出 {out}  ({size[0]}x{size[1]}, {n} 幀, {out.stat().st_size / 1024:.0f} KB)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
