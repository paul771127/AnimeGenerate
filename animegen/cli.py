"""命令列介面。

  animegen frames -i 角色.png -a "揮手打招呼"       # 多張圖逐格動畫 → GIF(推薦)
  animegen generate -i 角色.png -a "揮手打招呼"     # 影片模型 → GIF + MP4
  animegen ui                                      # 網頁介面
  animegen info                                    # 顯示硬體與可用後端
  animegen download wan22                          # 預先下載模型(之後可離線使用)
"""
from __future__ import annotations

import argparse
import logging
import sys
from typing import Sequence

from . import __version__
from .backends import BACKENDS, all_specs, get_spec
from .editors import EDITORS
from .config import load_config


def _progress(i: int, n: int, msg: str) -> None:
    if n > 1:
        width = 30
        done = int(width * i / n)
        sys.stderr.write(f"\r{msg} [{'#' * done}{'.' * (width - done)}] {i}/{n}")
        if i >= n:
            sys.stderr.write("\n")
    else:
        sys.stderr.write(f"{msg}...\n")
    sys.stderr.flush()


def cmd_generate(args: argparse.Namespace, cfg: dict) -> int:
    from .pipeline import AnimeGenerator

    gen = AnimeGenerator(cfg)
    result = gen.generate(
        args.image,
        args.action,
        character_desc=args.character or "",
        backend=args.backend,
        num_frames=args.frames,
        steps=args.steps,
        guidance=args.guidance,
        seed=args.seed,
        resolution=args.resolution,
        fit_mode=args.fit,
        fps=args.fps,
        formats=args.format,
        pingpong=args.pingpong or None,
        interpolate=args.interpolate,
        enhance=args.enhance or None,
        output_dir=args.output,
        progress=_progress,
    )
    print(result.summary())
    return 0


def cmd_frames(args: argparse.Namespace, cfg: dict) -> int:
    from .keyframes import KeyframeAnimator

    anim = KeyframeAnimator(cfg)
    result = anim.generate(
        args.image,
        args.action or "",
        poses=args.pose,
        character_desc=args.character or "",
        editor=args.model,
        num_poses=args.num_poses,
        seed=args.seed,
        steps=args.steps,
        frame_ms=args.frame_ms,
        include_original=False if args.no_original else None,
        pingpong=args.pingpong or None,
        tweens=args.tweens,
        use_ollama=args.ollama or None,
        output_dir=args.output,
        progress=_progress,
    )
    print(result.summary())
    return 0


def cmd_info(args: argparse.Namespace, cfg: dict) -> int:
    from .device import choose_backend, detect_device

    info = detect_device(cfg.get("device", "auto"))
    name, reason = choose_backend(info)
    print(f"animegen {__version__}")
    print(f"裝置: {info.describe()}")
    from .editors import choose_editor

    ed_name, ed_reason = choose_editor(info)
    print(f"多張圖模式(frames)的圖片模型: {ed_name} — {ed_reason}")
    print(f"影片模式(generate)的後端: {name} — {reason}")
    if cfg.get("_config_path"):
        print(f"設定檔: {cfg['_config_path']}")
    print("\n多張圖模式的圖片模型:")
    for cls in EDITORS.values():
        spec = cls.spec
        vram = f"4-bit 約 {spec.vram_4bit_gb:g} GB / bf16 {spec.vram_offload_gb:g} GB+" if spec.vram_4bit_gb else "不需 GPU"
        print(f"  {spec.name:<10} {spec.display_name}  [{vram}]  {spec.license_note}")
        if spec.notes:
            print(f"  {'':<10} {spec.notes}")
    print("\n影片模式的後端:")
    for spec in all_specs():
        vram = f"最低 {spec.vram_min_gb:g} GB / 建議 {spec.vram_offload_gb:g} GB+" if spec.vram_min_gb else "不需 GPU"
        print(f"  {spec.name:<10} {spec.display_name}  [{vram}]")
        if spec.notes:
            print(f"  {'':<10} {spec.notes}")
    return 0


def cmd_download(args: argparse.Namespace, cfg: dict) -> int:
    try:
        from huggingface_hub import snapshot_download
    except ImportError:
        print("需要 huggingface_hub:pip install -r requirements-gpu.txt", file=sys.stderr)
        return 1
    for name in args.backends:
        if name in EDITORS:
            model_id = cfg.get("editors", {}).get(name, {}).get("model_id") or EDITORS[name].spec.default_model_id
        else:
            model_id = cfg.get("backends", {}).get(name, {}).get("model_id") or get_spec(name).default_model_id
        print(f"下載 {model_id} ...")
        path = snapshot_download(model_id, cache_dir=cfg.get("model_cache_dir"))
        print(f"  完成: {path}")
    return 0


def cmd_ui(args: argparse.Namespace, cfg: dict) -> int:
    from .ui import launch

    launch(cfg, host=args.host, port=args.port, share=args.share)
    return 0


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(prog="animegen", description="角色圖 + 動作描述 → 角色做出動作的 GIF / MP4")
    ap.add_argument("--config", help="設定檔路徑(預設讀取 ./config.yaml)")
    ap.add_argument("-v", "--verbose", action="store_true", help="顯示詳細記錄")
    ap.add_argument("--version", action="version", version=f"animegen {__version__}")
    sub = ap.add_subparsers(dest="command", required=True)

    k = sub.add_parser("frames", help="多張圖逐格動畫:AI 依序畫出每個姿勢,串成 GIF(推薦)")
    k.add_argument("-i", "--image", required=True, help="角色圖片")
    k.add_argument("-a", "--action", help="動作描述,例如「揮手打招呼」;也可以每行寫一個姿勢")
    k.add_argument("-p", "--pose", nargs="+", help="直接指定每一格的姿勢,例如 -p 舉起右手 右手揮向左 右手揮向右")
    k.add_argument("-c", "--character", help="(選用)角色外觀描述,幫助維持角色一致")
    k.add_argument("-m", "--model", choices=["auto", *EDITORS], help="圖片模型(預設 auto 依顯卡選擇)")
    k.add_argument("-n", "--num-poses", type=int, help="沒有內建範本時要拆成幾格(預設 4)")
    k.add_argument("--seed", type=int, help="隨機種子;每一格都用同一個 seed")
    k.add_argument("--steps", type=int, help="每格推論步數(越多越精細越慢)")
    k.add_argument("--frame-ms", type=int, help="每格停留毫秒數(預設 180)")
    k.add_argument("--tweens", type=int, help="每兩格之間插入幾張淡入淡出過渡格(預設 0)")
    k.add_argument("--pingpong", action="store_true", help="正放 + 倒放")
    k.add_argument("--no-original", action="store_true", help="第一格不放角色原圖")
    k.add_argument("--ollama", action="store_true", help="沒有內建範本的動作,用本機 Ollama 拆成姿勢")
    k.add_argument("-o", "--output", help="輸出資料夾(預設 outputs/)")
    k.set_defaults(func=cmd_frames)

    g = sub.add_parser("generate", aliases=["gen"], help="影片模型產生動畫(GIF + MP4)")
    g.add_argument("-i", "--image", required=True, help="角色圖片")
    g.add_argument("-a", "--action", required=True, help="要角色做的動作,例如「揮手打招呼然後跳起來」")
    g.add_argument("-c", "--character", help="(選用)角色外觀描述,幫助模型維持角色一致")
    g.add_argument("-b", "--backend", choices=["auto", *BACKENDS], help="模型後端(預設 auto 依 VRAM 選擇)")
    g.add_argument("-o", "--output", help="輸出資料夾(預設 outputs/)")
    g.add_argument("-f", "--format", nargs="+", choices=["gif", "mp4", "png"], help="輸出格式(預設 gif mp4)")
    g.add_argument("--frames", type=int, help="幀數(越多動畫越長、越慢)")
    g.add_argument("--steps", type=int, help="推論步數(越多越精細、越慢)")
    g.add_argument("--guidance", type=float, help="提示詞遵循強度")
    g.add_argument("--seed", type=int, help="隨機種子;同 seed + 同參數會得到同樣結果")
    g.add_argument("--resolution", help="解析度,例如 832x480(預設 auto)")
    g.add_argument("--fit", choices=["crop", "pad", "stretch"], help="圖片比例不符時的處理方式")
    g.add_argument("--fps", type=float, help="輸出 fps")
    g.add_argument("--pingpong", action="store_true", help="正放 + 倒放,做成可循環的動畫")
    g.add_argument("--interpolate", type=int, choices=[1, 2, 3, 4], help="ffmpeg 補幀倍率,讓動作更順")
    g.add_argument("--enhance", action="store_true", help="用本機 Ollama 把動作描述改寫成詳細英文提示詞")
    g.set_defaults(func=cmd_generate)

    u = sub.add_parser("ui", help="開啟網頁介面")
    u.add_argument("--host", default="127.0.0.1")
    u.add_argument("--port", type=int, default=7860)
    u.add_argument("--share", action="store_true", help="產生 gradio 公開連結")
    u.set_defaults(func=cmd_ui)

    i = sub.add_parser("info", help="顯示硬體與可用後端")
    i.set_defaults(func=cmd_info)

    d = sub.add_parser("download", help="預先下載模型權重")
    d.add_argument("backends", nargs="+", metavar="MODEL",
                   choices=[*(e for e in EDITORS if e != "mock"), *(b for b in BACKENDS if b != "mock")],
                   help="qwen / kontext(多張圖模式)或 wan22 / wan21 / ltx / cogvideox(影片模式)")
    d.set_defaults(func=cmd_download)
    return ap


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(levelname)s %(message)s",
    )
    try:
        cfg = load_config(args.config)
        return args.func(args, cfg)
    except (ValueError, RuntimeError, FileNotFoundError) as exc:
        print(f"錯誤: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
