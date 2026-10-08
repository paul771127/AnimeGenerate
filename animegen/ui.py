"""Gradio 網頁介面。

分頁 1「多張圖逐格動畫」(推薦):角色圖 + 動作 → 拆成姿勢 → AI 一張張畫 → GIF,可單獨重畫某一格。
分頁 2「影片模型」:角色圖 + 動作 → 圖生影片模型 → GIF / MP4。
"""
from __future__ import annotations

import logging
from typing import Any

from .backends import all_specs
from .device import choose_backend
from .editors import EDITORS, choose_editor
from .keyframes import KeyframeAnimator
from .pipeline import AnimeGenerator

log = logging.getLogger(__name__)

EXAMPLES = [
    "揮手打招呼,露出微笑",
    "原地跳起來,雙手舉高歡呼",
    "轉身一圈,頭髮和裙子跟著飄動",
    "點頭然後比出 V 字手勢",
    "waves hello with the right hand and smiles",
]

FRAME_EXAMPLES = [
    "揮手打招呼",
    "開心地跳起來",
    "轉一圈",
    "鞠躬",
    "跳舞",
    "出拳攻擊",
    "雙手放在身側,面無表情\n右手摸頭,害羞地笑\n雙手摀住臉,臉紅",
]

ACTION_PLACEHOLDER = (
    "例如:揮手打招呼\n\n"
    "也可以每行寫一個姿勢(效果最好):\n"
    "舉起右手,微笑\n"
    "右手揮向左邊\n"
    "右手揮向右邊"
)


def build_ui(cfg: dict[str, Any] | None = None):
    import gradio as gr

    gen = AnimeGenerator(cfg)
    anim = KeyframeAnimator(gen.cfg, device=gen.device)
    auto_backend, auto_backend_reason = choose_backend(gen.device)
    auto_editor, auto_editor_reason = choose_editor(gen.device)
    backend_choices = [(f"自動({auto_backend})", "auto")] + [(s.display_name, s.name) for s in all_specs()]
    editor_choices = [(f"自動({auto_editor})", "auto")] + [(c.spec.display_name, n) for n, c in EDITORS.items()]

    def progress_fn(progress):
        def report(i: int, n: int, msg: str) -> None:
            progress((i / n) if n > 1 else None, desc=msg)

        return report

    # ---- 多張圖逐格動畫 --------------------------------------------------------------
    def preview_poses(action, editor, num_poses, use_ollama):
        if not (action or "").strip():
            raise gr.Error("請先輸入動作描述")
        try:
            poses, source = anim.plan(action, int(num_poses), use_ollama)
            name, _ = anim.resolve_editor_name(editor)
        except ValueError as exc:
            raise gr.Error(str(exc)) from exc
        language = EDITORS[name].spec.language
        gr.Info(f"姿勢來源:{source}。可以直接修改下面的姿勢,每行一格。")
        return "\n".join(p.text(language) for p in poses)

    def estimate(action, poses_text, num_poses, inbetween, pingpong, include_original):
        poses = [p for p in (poses_text or "").splitlines() if p.strip()]
        if poses:
            n = len(poses)
        elif (action or "").strip():
            try:
                n = len(anim.plan(action, int(num_poses), False)[0])
            except ValueError:
                return ""
        else:
            return ""
        draw, total = anim.count_images(n, inbetween=int(inbetween), pingpong=pingpong,
                                        include_original=include_original)
        return f"預計讓 AI 畫 **{draw} 張**,GIF 共 **{total} 格**(關鍵姿勢 {n} 張 + 補間 {draw - n} 張)"

    def gallery_items(result):
        return [(f, f"{i + 1}. {p}") for i, (f, p) in enumerate(zip(result.keyframes, result.poses))]

    def run_frames(image, action, poses_text, character, editor, num_poses, seed, steps, inbetween, fast,
                   frame_ms, tweens, pingpong, include_original, use_ollama, progress=gr.Progress()):
        if image is None:
            raise gr.Error("請先上傳角色圖片")
        poses = [p.strip() for p in (poses_text or "").splitlines() if p.strip()]
        if not poses and not (action or "").strip():
            raise gr.Error("請輸入動作描述,或在「姿勢」欄每行寫一個姿勢")
        gen.release()  # 兩種模式共用一張顯卡,先卸載影片模型
        try:
            result = anim.generate(
                image,
                action or "",
                poses=poses or None,
                character_desc=character or "",
                editor=editor,
                num_poses=int(num_poses),
                seed=int(seed),
                steps=int(steps) or None,
                inbetween=int(inbetween),
                fast=fast,
                frame_ms=int(frame_ms) or None,
                tweens=int(tweens),
                pingpong=pingpong,
                include_original=include_original,
                use_ollama=use_ollama,
                progress=progress_fn(progress),
            )
        except (ValueError, RuntimeError) as exc:
            raise gr.Error(str(exc)) from exc
        for w in result.warnings:
            gr.Warning(w)
        poses_out = "\n".join(p for i, p in enumerate(result.poses) if result.kind(i) == "key")
        return (str(result.gif), gallery_items(result), str(result.gif), result.summary(), result,
                poses_out, int(result.seed), result.settings["frame_ms"])

    def redraw_frame(result, index, new_pose, character, redraw_seed, progress=gr.Progress()):
        if result is None:
            raise gr.Error("請先生成一次動畫")
        try:
            result = anim.redraw(result, int(index) - 1, pose=(new_pose or "").strip() or None,
                                 seed=int(redraw_seed), character_desc=character or "",
                                 progress=progress_fn(progress))
        except (ValueError, RuntimeError) as exc:
            raise gr.Error(str(exc)) from exc
        return str(result.gif), gallery_items(result), str(result.gif), result.summary(), result

    def reassemble(result, frame_ms, tweens, pingpong):
        if result is None:
            raise gr.Error("請先生成一次動畫")
        result.settings.update(frame_ms=int(frame_ms) or result.settings["frame_ms"], tweens=int(tweens),
                               pingpong=bool(pingpong))
        anim.save(result)
        return str(result.gif), str(result.gif), result

    # ---- 影片模型 ------------------------------------------------------------------------
    def run_video(image, action, character, backend, frames, steps, seed, resolution, fit_mode, pingpong,
                  interpolate, enhance, progress=gr.Progress()):
        if image is None:
            raise gr.Error("請先上傳角色圖片")
        if not (action or "").strip():
            raise gr.Error("請輸入要角色做的動作")
        anim.release()  # 先卸載圖片模型
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
                progress=progress_fn(progress),
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
            "上傳一張角色圖,用文字描述想要的動作,AI 讓這個角色做出那個動作。\n\n"
            f"**裝置:** {gen.device.describe()}"
        )
        with gr.Tab("多張圖逐格動畫(推薦)"):
            gr.Markdown(
                "把動作拆成幾個關鍵姿勢,AI 參考原圖一張張畫出來,再串成 GIF。"
                "不滿意的格子可以單獨重畫。\n\n"
                f"**自動選擇的圖片模型:** {auto_editor} — {auto_editor_reason}"
            )
            state = gr.State(None)
            with gr.Row():
                with gr.Column():
                    k_image = gr.Image(label="角色圖片", type="pil", image_mode="RGBA", height=360)
                    k_action = gr.Textbox(label="動作描述", lines=4, placeholder=ACTION_PLACEHOLDER)
                    gr.Examples(FRAME_EXAMPLES, inputs=k_action, label="範例")
                    with gr.Row():
                        k_preview = gr.Button("① 拆成姿勢(可修改)")
                    k_poses = gr.Textbox(label="每一格的姿勢(每行一格;留空 = 依動作描述自動拆)", lines=5)
                    k_character = gr.Textbox(label="角色外觀(選用)",
                                             placeholder="例如:銀色長髮、藍色眼睛、穿水手服的少女")
                    k_editor = gr.Dropdown(editor_choices, value="auto", label="圖片模型")
                    k_inbetween = gr.Slider(0, 3, int(anim.kcfg.get("inbetween", 1)), step=1,
                                            label="補間層數(用時間換連續性):每兩個姿勢之間補 0 / 1 / 3 / 7 張中間格")
                    k_fast = gr.Checkbox(bool(anim.kcfg.get("fast", True)),
                                         label="加速(Qwen 用 Lightning 8 步,約快 5 倍;格數多時建議開)")
                    k_estimate = gr.Markdown("")
                    with gr.Accordion("進階設定", open=False):
                        k_num = gr.Slider(2, 12, 4, step=1, label="沒有內建範本時拆成幾格")
                        k_seed = gr.Number(-1, label="Seed(-1 = 隨機;每一格用同一個 seed)", precision=0)
                        k_steps = gr.Slider(0, 60, 0, step=1, label="每格推論步數(0 = 模型預設)")
                        k_original = gr.Checkbox(True, label="第一格放角色原圖")
                        k_ollama = gr.Checkbox(False, label="沒有內建範本的動作用本機 Ollama 拆解")
                    k_run = gr.Button("② 生成動畫", variant="primary")
                with gr.Column():
                    k_gif = gr.Image(label="GIF", type="filepath")
                    with gr.Row():
                        k_ms = gr.Slider(0, 1000, 0, step=10, label="每格停留(毫秒;0 = 依補間自動)")
                        k_tweens = gr.Slider(0, 4, 0, step=1, label="淡入淡出過渡格")
                    k_pingpong = gr.Checkbox(False, label="來回播放(正放 + 倒放)")
                    k_apply = gr.Button("套用播放設定(不用重畫)")
                    k_gallery = gr.Gallery(label="每一格", columns=6, height=300)
                    with gr.Accordion("重畫某一格", open=False):
                        r_index = gr.Number(2, label="第幾格", precision=0)
                        r_pose = gr.Textbox(label="新的姿勢描述(留空 = 換 seed 重畫;補間格會依前後兩格重畫)")
                        r_seed = gr.Number(-1, label="Seed(-1 = 隨機)", precision=0)
                        r_run = gr.Button("重畫這一格")
                    k_file = gr.File(label="下載 GIF")
                    k_info = gr.Textbox(label="生成資訊", lines=8)
            k_preview.click(preview_poses, [k_action, k_editor, k_num, k_ollama], k_poses)
            est_inputs = [k_action, k_poses, k_num, k_inbetween, k_pingpong, k_original]
            for comp in est_inputs:
                comp.change(estimate, est_inputs, k_estimate)
            k_run.click(
                run_frames,
                [k_image, k_action, k_poses, k_character, k_editor, k_num, k_seed, k_steps, k_inbetween, k_fast,
                 k_ms, k_tweens, k_pingpong, k_original, k_ollama],
                [k_gif, k_gallery, k_file, k_info, state, k_poses, k_seed, k_ms],
                api_name="frames",
                concurrency_limit=1,  # GPU 一次只跑一個工作
            )
            r_run.click(redraw_frame, [state, r_index, r_pose, k_character, r_seed],
                        [k_gif, k_gallery, k_file, k_info, state], api_name="redraw", concurrency_limit=1)
            k_apply.click(reassemble, [state, k_ms, k_tweens, k_pingpong], [k_gif, k_file, state],
                          api_name="reassemble")

        with gr.Tab("影片模型"):
            gr.Markdown(
                "用圖生影片模型直接產生連續動作,動作較流暢,但比較吃顯卡。\n\n"
                f"**自動選擇的後端:** {auto_backend} — {auto_backend_reason}"
            )
            with gr.Row():
                with gr.Column():
                    image = gr.Image(label="角色圖片", type="pil", image_mode="RGBA", height=360)
                    action = gr.Textbox(label="動作描述", lines=3,
                                        placeholder="例如:揮手打招呼,然後開心地跳起來")
                    gr.Examples(EXAMPLES, inputs=action, label="範例")
                    character = gr.Textbox(label="角色外觀(選用)",
                                           placeholder="例如:銀色長髮、藍色眼睛、穿水手服的少女")
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
                run_video,
                [image, action, character, backend, frames, steps, seed, resolution, fit_mode, pingpong,
                 interpolate, enhance],
                [gif_out, mp4_out, files_out, info_out],
                api_name="generate",
                concurrency_limit=1,
            )
    return demo


def launch(cfg: dict[str, Any] | None = None, host: str = "127.0.0.1", port: int = 7860, share: bool = False):
    try:
        import gradio  # noqa: F401
    except ImportError as exc:
        raise RuntimeError("網頁介面需要 gradio:pip install gradio") from exc
    demo = build_ui(cfg)
    demo.queue().launch(server_name=host, server_port=port, share=share)
