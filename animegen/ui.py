"""Gradio 網頁介面:上傳角色圖、輸入動作描述 → 預覽 GIF / MP4。"""
from __future__ import annotations

import logging
from typing import Any

from .backends import all_specs
from .device import choose_backend
from .pipeline import AnimeGenerator

log = logging.getLogger(__name__)

EXAMPLES = [
    "揮手打招呼,露出微笑",
    "原地跳起來,雙手舉高歡呼",
    "轉身一圈,頭髮和裙子跟著飄動",
    "點頭然後比出 V 字手勢",
    "waves hello with the right hand and smiles",
]


def build_ui(cfg: dict[str, Any] | None = None):
    import gradio as gr

    gen = AnimeGenerator(cfg)
    auto_name, auto_reason = choose_backend(gen.device)
    backend_choices = [(f"自動({auto_name})", "auto")] + [(s.display_name, s.name) for s in all_specs()]

    def run(image, action, character, backend, frames, steps, seed, resolution, fit_mode, pingpong,
            interpolate, enhance, progress=gr.Progress()):
        if image is None:
            raise gr.Error("請先上傳角色圖片")
        if not (action or "").strip():
            raise gr.Error("請輸入要角色做的動作")

        def report(i: int, n: int, msg: str) -> None:
            progress((i / n) if n > 1 else None, desc=msg)

        try:
            result = gen.generate(
                image,
                action,
                character_desc=character or "",
                backend=backend,
                num_frames=int(frames) or None,
                steps=int(steps) or None,
                seed=int(seed),
                resolution=resolution or "auto",
                fit_mode=fit_mode,
                formats=["gif", "mp4"],
                pingpong=pingpong,
                interpolate=2 if interpolate else 1,
                enhance=enhance,
                progress=report,
            )
        except (ValueError, RuntimeError) as exc:
            raise gr.Error(str(exc)) from exc
        for w in result.warnings:
            gr.Warning(w)
        gif = str(result.files["gif"])
        mp4 = str(result.files["mp4"])
        return gif, mp4, [gif, mp4], result.summary()

    with gr.Blocks(title="AnimeGen 角色動畫生成器") as demo:
        gr.Markdown(
            "# AnimeGen 角色動畫生成器\n"
            "上傳一張角色圖,用文字描述想要的動作,AI 影片模型會讓這個角色做出那個動作。\n\n"
            f"**裝置:** {gen.device.describe()} **自動後端:** {auto_name} — {auto_reason}"
        )
        with gr.Row():
            with gr.Column():
                image = gr.Image(label="角色圖片", type="pil", image_mode="RGBA", height=360)
                action = gr.Textbox(label="動作描述", lines=3,
                                    placeholder="例如:揮手打招呼,然後開心地跳起來")
                gr.Examples(EXAMPLES, inputs=action, label="範例")
                character = gr.Textbox(label="角色外觀(選用)", placeholder="例如:銀色長髮、藍色眼睛、穿水手服的少女")
                backend = gr.Dropdown(backend_choices, value="auto", label="模型")
                with gr.Accordion("進階設定", open=False):
                    frames = gr.Slider(0, 161, 0, step=1, label="幀數(0 = 模型預設;越多越長越慢)")
                    steps = gr.Slider(0, 80, 0, step=1, label="推論步數(0 = 模型預設;越多越精細越慢)")
                    seed = gr.Number(-1, label="Seed(-1 = 隨機;固定 seed 可重現結果)", precision=0)
                    resolution = gr.Textbox("auto", label="解析度(auto 或 例如 832x480)")
                    fit_mode = gr.Radio(["crop", "pad", "stretch"], value="crop",
                                        label="圖片比例不符時:裁切 / 補邊 / 拉伸")
                    pingpong = gr.Checkbox(False, label="來回播放(正放 + 倒放,做成無縫循環)")
                    interpolate = gr.Checkbox(False, label="補幀 2 倍(動作更順)")
                    enhance = gr.Checkbox(False, label="用本機 Ollama 強化提示詞(需先安裝並執行 Ollama)")
                btn = gr.Button("生成動畫", variant="primary")
            with gr.Column():
                gif_out = gr.Image(label="GIF", type="filepath")
                mp4_out = gr.Video(label="MP4")
                files_out = gr.File(label="下載", file_count="multiple")
                info_out = gr.Textbox(label="生成資訊", lines=5)
        btn.click(
            run,
            [image, action, character, backend, frames, steps, seed, resolution, fit_mode, pingpong,
             interpolate, enhance],
            [gif_out, mp4_out, files_out, info_out],
            api_name="generate",
            concurrency_limit=1,  # GPU 一次只跑一個工作
        )
    return demo


def launch(cfg: dict[str, Any] | None = None, host: str = "127.0.0.1", port: int = 7860, share: bool = False):
    try:
        import gradio  # noqa: F401
    except ImportError as exc:
        raise RuntimeError("網頁介面需要 gradio:pip install gradio") from exc
    demo = build_ui(cfg)
    demo.queue().launch(server_name=host, server_port=port, share=share)
