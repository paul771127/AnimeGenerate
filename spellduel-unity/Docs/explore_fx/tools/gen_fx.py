"""故事探索特效影片生成：shots.json -> Docs/explore_fx/out/<id>.mp4

用法（用 ComfyUI 內建的 python，只用標準庫 + numpy/PIL）:
  python -I gen_fx.py list
  python -I gen_fx.py run --only fx_C2_discover,fx_D4_spores
  python -I gen_fx.py run --no-keyframe          # 只做 9 支不用圖的
  python -I gen_fx.py run                        # 全部 25 支
  python -I gen_fx.py check out/fx_D3_fire.mp4   # 單獨檢查背景乾不乾淨

跟 story_video/tools/gen_minimax_ref.py 同一套做法：
  - 所有 clip 共用 loaders，塞在同一個 prompt 裡
  - 每個 conditioning 接一個 PreviewAny，排程器會先把文字全部編碼完才開始取樣，
    所以 32B 文字編碼器和影片模型各只載入一次（一支一個 prompt 的話每支都要重載）
  - 送出前先 /free

輸出會照 README 的規格：960x544、24fps、H.264、**拿掉聲音軌**。
每支都會跑背景乾淨度檢查，數字印出來，不合格的自己換 seed 重生。
"""
import argparse
import json
import shutil
import subprocess
import sys
import time
import urllib.request
import uuid
import zlib
from pathlib import Path

COMFY = "http://127.0.0.1:8188"
COMFY_OUTPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\output")
COMFY_INPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\input")
INPUT_SUBDIR = "explore_fx"

UNET = "minimax_h3_fl2va_pruned_int8_convrot.safetensors"
TEXT_ENCODER = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors"
VIDEO_VAE = "minimax_h3_video_vae_fp16.safetensors"
AUDIO_VAE = "minimax_h3_audio_vae_fp32.safetensors"

W, H, FRAMES, FPS, STEPS = 960, 544, 124, 24, 20
SHIFT_VIDEO, SHIFT_AUDIO = 13.0, 3.0
SAMPLER, SCHEDULER = "res_multistep", "simple"
MAX_MB = 3.0

FX = Path(__file__).resolve().parents[1]          # Docs/explore_fx
SHOTS = FX / "shots.json"
KEYFRAMES = FX / "keyframes"
OUT = FX / "out"


def api(path, data=None, timeout=120):
    req = urllib.request.Request(
        COMFY + path,
        data=None if data is None else json.dumps(data).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        body = r.read()
        return json.loads(body) if body else None


def load_shots():
    return json.loads(SHOTS.read_text(encoding="utf-8"))


# shots.json 的 backgrounds 只描述「背景長怎樣」，沒有說「背景不准變」。
# 實測 fx_B2_key_idle 的綠幕整個漂成紅色（首尾幀都是綠的，中段還是跑掉），
# 所以這裡多鎖一句。黑背景也鎖，免得中途浮出地面或灰霧。
BG_HOLD = {
    "green": ("the background stays exactly the same flat uniform chroma-key green for every "
              "frame, the background color never changes, never turns red or any other color"),
    "black": ("the background stays pure solid black for every frame, no ground plane appears, "
              "no fog, no vignette, no light spill on the background"),
}


# 第一輪跑出來不合格的，在這裡補一句和換 seed，再用 --force 重生。
# 模型對「小、細、微弱」很不敏感，會自己放大成塞滿畫面的光柱／光塊，所以要明講尺寸。
OVERRIDES = {
    "fx_B1_activate": {
        "seed": 7001,
        "extra": ("the ring of light lies flat on the ground and stays small, the beam is thin "
                  "and horizontal, there is no vertical pillar or column of light, nothing "
                  "touches the top or bottom edge of the frame"),
    },
    # 第二輪（seed 7002）模型把「細細一道白光透出來」畫成兩條人腿中間的光，
    # 所以這支要開 --cfg 4 讓 shots.json 的 negative（含 people, hands）生效。
    "fx_C1_glint_white": {
        "seed": 7012,
        "extra": ("the sliver of light is extremely thin and short, only a few pixels wide, it "
                  "occupies a tiny part of the frame and the rest of the frame stays pure black, "
                  "very dim and subtle, no large bright shape, no glowing slab, there is no "
                  "person, no legs, no body, no object, only darkness and the thin light"),
    },
    "fx_B2_key_idle": {
        "seed": 7003,
        "extra": "only the key moves, the green background is perfectly static and uniform",
    },
    # 這支是 full 背景，要直接貼在遊戲的湖面上，所以畫面裡只能有水。
    # 第一輪把岸邊的樹林也畫進來了，貼上去會在水面上長出樹。
    # 第二輪（seed 7004）畫出一個人坐在木棧道上看湖。把它當「水面材質」而不是
    # 「湖的風景」來描述，模型才不會自己補一個場景出來。
    "fx_D2_lake": {
        "seed": 7014,
        "extra": ("extreme close-up of the water surface itself, an abstract flat water texture "
                  "filling every pixel of the frame, gentle ripples and shimmering pale "
                  "moonlight reflections, this is a texture not a scene, there is no shore, "
                  "no bank, no land, no trees, no plants, no dock, no boat, no person, "
                  "no sky, no horizon, nothing but water"),
    },
    # 規格要的是草叢裡小動物的眼睛反光，第一輪畫成了大大的動漫人物眼睛（有睫毛）。
    "fx_C1_glint_eyes": {
        "seed": 7005,
        "extra": ("two very small round animal eyes low in the frame, like a tiny rodent hiding "
                  "in the dark, they are not human eyes, no eyelashes, no face, no body, "
                  "everything else in the frame stays pure black"),
    },
}


def compose(spec, clip):
    """提示詞 = style + clip.prompt + backgrounds[clip.background] (+ 背景穩定子句)"""
    parts = [spec["style"], clip["prompt"]]
    bg_key = clip.get("background", "black")
    bg = spec["backgrounds"].get(bg_key, "")
    if bg:
        parts.append(bg)
    if bg_key in BG_HOLD:
        parts.append(BG_HOLD[bg_key])
    extra = OVERRIDES.get(clip["id"], {}).get("extra")
    if extra:
        parts.append(extra)
    return ", ".join(p.strip().rstrip(",") for p in parts if p.strip())


def keyframe_paths(clip):
    """回傳 (first_png, last_png)，沒有就 None。"""
    kind = clip.get("keyframe")
    if not kind:
        return None, None
    if kind == "start":
        p = KEYFRAMES / f"{clip['id']}.png"
        if not p.exists():
            return None, None
        # loop 的同一張也當最後一格，首尾才接得起來
        return p, (p if clip.get("mode") == "loop" else None)
    if kind == "end":
        p = KEYFRAMES / f"{clip['id']}_end.png"
        return (None, p) if p.exists() else (None, None)
    return None, None


def stage_image(png):
    """複製到 ComfyUI 的 input 資料夾，回傳 LoadImage 要的相對路徑。"""
    dest_dir = COMFY_INPUT / INPUT_SUBDIR
    dest_dir.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(png, dest_dir / png.name)
    return f"{INPUT_SUBDIR}/{png.name}"


def build_graph(spec, clips, steps, cfg, seed_base, run):
    neg_text = spec.get("negative", "")
    wf = {
        "L_unet": {"class_type": "UNETLoader",
                   "inputs": {"unet_name": UNET, "weight_dtype": "default"}},
        "L_shift": {"class_type": "MiniMaxH3SigmaShift",
                    "inputs": {"model": ["L_unet", 0], "shift_video": SHIFT_VIDEO,
                               "shift_audio": SHIFT_AUDIO}},
        "L_clip": {"class_type": "CLIPLoader",
                   "inputs": {"clip_name": TEXT_ENCODER, "type": "minimax",
                              "device": "default"}},
        "L_vae": {"class_type": "VAELoader", "inputs": {"vae_name": VIDEO_VAE}},
        "L_avae": {"class_type": "VAELoader", "inputs": {"vae_name": AUDIO_VAE}},
        "S_sampler": {"class_type": "KSamplerSelect",
                      "inputs": {"sampler_name": SAMPLER}},
        "S_sigmas": {"class_type": "BasicScheduler",
                     "inputs": {"model": ["L_shift", 0], "scheduler": SCHEDULER,
                                "steps": steps, "denoise": 1.0}},
    }
    if cfg > 1.0:
        # 全部 clip 共用一個負面 conditioning（長寬、長度都一樣）
        wf["N_cond"] = {"class_type": "MiniMaxH3ImageToVideo",
                        "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0],
                                   "prompt": neg_text, "width": W, "height": H,
                                   "length": FRAMES}}
        wf["N_done"] = {"class_type": "PreviewAny", "inputs": {"source": ["N_cond", 0]}}

    saves = {}
    for clip in clips:
        cid = clip["id"]
        tag = "c" + uuid.uuid5(uuid.NAMESPACE_OID, cid).hex[:8]
        n = lambda s, tag=tag: f"{tag}_{s}"  # noqa: E731
        first_png, last_png = keyframe_paths(clip)

        cond_inputs = {"clip": ["L_clip", 0], "vae": ["L_vae", 0],
                       "prompt": compose(spec, clip),
                       "width": W, "height": H, "length": FRAMES}
        if first_png is not None:
            wf[n("img_f")] = {"class_type": "LoadImage",
                              "inputs": {"image": stage_image(first_png)}}
            cond_inputs["first_frame"] = [n("img_f"), 0]
        if last_png is not None:
            if last_png == first_png:
                cond_inputs["last_frame"] = [n("img_f"), 0]
            else:
                wf[n("img_l")] = {"class_type": "LoadImage",
                                  "inputs": {"image": stage_image(last_png)}}
                cond_inputs["last_frame"] = [n("img_l"), 0]

        seed = OVERRIDES.get(cid, {}).get(
            "seed", (seed_base + zlib.crc32(cid.encode())) % 2**31)
        wf[n("cond")] = {"class_type": "MiniMaxH3ImageToVideo", "inputs": cond_inputs}
        # 讓排程器先把所有文字編碼完，再開始第一次取樣
        wf[n("cond_done")] = {"class_type": "PreviewAny",
                              "inputs": {"source": [n("cond"), 0]}}
        wf[n("noise")] = {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}}
        if cfg > 1.0:
            wf[n("guider")] = {"class_type": "CFGGuider",
                               "inputs": {"model": ["L_shift", 0],
                                          "positive": [n("cond"), 0],
                                          "negative": ["N_cond", 0], "cfg": cfg}}
        else:
            wf[n("guider")] = {"class_type": "BasicGuider",
                               "inputs": {"model": ["L_shift", 0],
                                          "conditioning": [n("cond"), 0]}}
        wf[n("sample")] = {"class_type": "SamplerCustomAdvanced",
                           "inputs": {"noise": [n("noise"), 0], "guider": [n("guider"), 0],
                                      "sampler": ["S_sampler", 0], "sigmas": ["S_sigmas", 0],
                                      "latent_image": [n("cond"), 1]}}
        wf[n("dec_v")] = {"class_type": "VAEDecode",
                          "inputs": {"samples": [n("sample"), 0], "vae": ["L_vae", 0]}}
        wf[n("dec_a")] = {"class_type": "VAEDecodeAudio",
                          "inputs": {"samples": [n("sample"), 0], "vae": ["L_avae", 0]}}
        wf[n("video")] = {"class_type": "CreateVideo",
                          "inputs": {"images": [n("dec_v"), 0], "audio": [n("dec_a"), 0],
                                     "fps": FPS}}
        wf[n("save")] = {"class_type": "SaveVideo",
                         "inputs": {"video": [n("video"), 0],
                                    "filename_prefix": f"explore_fx/{cid}_{run}",
                                    "format": "mp4", "codec": "h264"}}
        saves[cid] = (n("save"), seed, first_png, last_png)
    return wf, saves


def strip_audio(src, dest):
    """照 README：拿掉聲音軌。太大就重壓。"""
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src),
                    "-an", "-c:v", "copy", str(dest)], check=True)
    if dest.stat().st_size > MAX_MB * 1024 * 1024:
        tmp = dest.with_suffix(".crf.mp4")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(dest),
                        "-an", "-c:v", "libx264", "-crf", "28", "-pix_fmt", "yuv420p",
                        str(tmp)], check=True)
        tmp.replace(dest)


def check_background(mp4, background):
    """量外框 8% 的邊緣像素，回報背景乾不乾淨。"""
    try:
        import numpy as np
        from PIL import Image
    except ImportError:
        return "（缺 numpy/PIL，跳過檢查）"
    tmp = Path(COMFY_OUTPUT) / f"_bgcheck_{uuid.uuid4().hex[:6]}.png"
    vals = []
    for t in (0.2, 2.0, 4.5):
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-ss", str(t),
                        "-i", str(mp4), "-frames:v", "1", str(tmp)], check=False)
        if not tmp.exists():
            continue
        a = np.asarray(Image.open(tmp).convert("RGB")).astype(float)
        tmp.unlink()
        h, w = a.shape[:2]
        m = max(2, int(min(h, w) * 0.08))
        border = np.concatenate([a[:m].reshape(-1, 3), a[-m:].reshape(-1, 3),
                                 a[:, :m].reshape(-1, 3), a[:, -m:].reshape(-1, 3)])
        vals.append(border)
    if not vals:
        return "（取不到畫格）"
    b = np.concatenate(vals)
    if background == "green":
        g_mean = b[:, 1].mean()
        off = max(b[:, 0].mean(), b[:, 2].mean())
        ok = g_mean > 180 and off < 90 and b[:, 1].std() < 30
        return (f"綠幕邊緣 G={g_mean:.0f} R/B={off:.0f} 變異={b[:, 1].std():.0f} "
                f"{'OK' if ok else '不夠均勻，建議換 seed 重生'}")
    if background == "full":
        return "full 背景，不檢查"
    # 用中位數和超標比例判斷，不能用最亮值：特效本身的光點飄到邊緣是正常的，
    # 真正要抓的是地面／灰霧／暗角那種「一整片都不黑」。
    luma = b.mean(axis=1)
    med, hot = float(np.median(luma)), float((luma > 16).mean() * 100)
    ok = med < 2 and hot < 12
    return (f"黑背景邊緣 中位={med:.1f} 平均={luma.mean():.2f} >16={hot:.1f}% "
            f"{'OK' if ok else '不夠黑（有地面/霧/暗角），建議換 seed 重生'}")


def cmd_list(args):
    spec = load_shots()
    for c in spec["clips"]:
        f, l = keyframe_paths(c)
        kf = c.get("keyframe", "-")
        have = "有圖" if (f or l) else ("缺圖" if kf != "-" else "文字")
        print(f"{c['id']:22s} {c['background']:6s} {c['mode']:5s} {c['seconds']:>4}s "
              f"kf={kf:5s} {have:4s} {c['zh']}")


def cmd_check(args):
    spec = load_shots()
    by_id = {c["id"]: c for c in spec["clips"]}
    for p in args.files:
        p = Path(p)
        clip = by_id.get(p.stem.split("_v")[0]) or by_id.get(p.stem)
        bg = clip["background"] if clip else "black"
        print(f"{p.name}: {check_background(p, bg)}")


def cmd_run(args):
    spec = load_shots()
    clips = spec["clips"]
    if args.only:
        want = [s.strip() for s in args.only.split(",") if s.strip()]
        by_id = {c["id"]: c for c in clips}
        missing = [w for w in want if w not in by_id]
        if missing:
            sys.exit(f"shots.json 裡沒有: {missing}")
        clips = [by_id[w] for w in want]
    if args.no_keyframe:
        clips = [c for c in clips if not c.get("keyframe")]
    if args.need_keyframe:
        clips = [c for c in clips if c.get("keyframe")]
    skipped = []
    if not args.allow_missing_keyframe:
        kept = []
        for c in clips:
            if c.get("keyframe") and keyframe_paths(c) == (None, None):
                skipped.append(c["id"])
            else:
                kept.append(c)
        clips = kept
    if not args.force:
        have = [c["id"] for c in clips if (OUT / f"{c['id']}.mp4").exists()]
        clips = [c for c in clips if c["id"] not in have]
        if have:
            print(f"已存在，跳過: {', '.join(have)}")
    if skipped:
        print(f"缺首尾幀，跳過: {', '.join(skipped)}")
    if not clips:
        print("沒有要生成的 clip。")
        return

    OUT.mkdir(parents=True, exist_ok=True)
    size = args.group if args.group > 0 else len(clips)
    groups = [clips[i:i + size] for i in range(0, len(clips), size)]
    for gi, group in enumerate(groups, 1):
        print(f"\n=== 第 {gi}/{len(groups)} 批 ===")
        submit_group(spec, group, args)


def submit_group(spec, clips, args):
    run = uuid.uuid4().hex[:6]
    wf, saves = build_graph(spec, clips, args.steps, args.cfg, args.seed, run)
    (OUT / f"_workflow_{run}.json").write_text(
        json.dumps(wf, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"送出 {len(clips)} 支 clip（一個 prompt，模型只載入一次）"
          f" steps={args.steps} cfg={args.cfg}")
    for c in clips:
        f, l = keyframe_paths(c)
        print(f"  {c['id']:22s} {c['background']:6s} "
              f"first={'有' if f else '-'} last={'有' if l else '-'}")

    api("/free", {"unload_models": True, "free_memory": True})
    t0 = time.time()
    pid = api("/prompt", {"prompt": wf, "client_id": "explore_fx"})["prompt_id"]
    print(f"prompt_id={pid}，開始等…", flush=True)
    while True:
        time.sleep(20)
        hist = api(f"/history/{pid}")
        if pid in hist:
            break
        el = time.time() - t0
        if int(el) % 300 < 20:
            print(f"  ... 已跑 {el / 60:.0f} 分", flush=True)
    secs = time.time() - t0
    entry = hist[pid]
    status = entry.get("status", {})
    if status.get("status_str") != "success":
        msgs = [m for m in status.get("messages", []) if m[0] == "execution_error"]
        raise RuntimeError(f"失敗（{secs:.0f}s）: {json.dumps(msgs, ensure_ascii=False)[:3000]}")

    outs = entry["outputs"]
    report = {}
    for cid, (node, seed, first_png, last_png) in saves.items():
        items = outs.get(node, {})
        files = (items.get("videos") or []) + (items.get("images") or [])
        item = next((i for i in files if str(i.get("filename", "")).endswith(".mp4")), None)
        if item is None:
            print(f"  !! {cid} 沒有輸出")
            continue
        produced = COMFY_OUTPUT / item.get("subfolder", "") / item["filename"]
        dest = OUT / f"{cid}.mp4"
        strip_audio(produced, dest)
        clip = next(c for c in spec["clips"] if c["id"] == cid)
        note = check_background(dest, clip["background"])
        mb = dest.stat().st_size / 1024 / 1024
        print(f"  ✓ {cid:22s} {mb:.1f}MB  {note}")
        report[cid] = {"seed": seed, "steps": args.steps, "cfg": args.cfg,
                       "background": clip["background"], "mode": clip["mode"],
                       "seconds": clip["seconds"], "size_mb": round(mb, 2),
                       "keyframe_first": first_png.name if first_png else None,
                       "keyframe_last": last_png.name if last_png else None,
                       "bg_check": note}
    log = OUT / "run_log.json"
    old = json.loads(log.read_text(encoding="utf-8")) if log.exists() else {}
    old.update(report)
    log.write_text(json.dumps(old, ensure_ascii=False, indent=2), encoding="utf-8")
    api("/free", {"unload_models": True, "free_memory": True})
    print(f"完成，{secs / 60:.0f} 分（{secs / 60 / max(1, len(clips)):.1f} 分/支）-> {OUT}")


def main():
    p = argparse.ArgumentParser(description="故事探索特效影片生成 (MiniMax H3)")
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list").set_defaults(func=cmd_list)
    c = sub.add_parser("check")
    c.add_argument("files", nargs="+")
    c.set_defaults(func=cmd_check)
    r = sub.add_parser("run")
    r.add_argument("--only", help="逗號分隔的 clip id")
    r.add_argument("--no-keyframe", action="store_true", help="只做不用圖的")
    r.add_argument("--need-keyframe", action="store_true", help="只做要用圖的")
    r.add_argument("--allow-missing-keyframe", action="store_true",
                   help="圖還沒推上來也先用文字生")
    r.add_argument("--steps", type=int, default=STEPS)
    r.add_argument("--cfg", type=float, default=1.0,
                   help=">1 才會用 shots.json 的 negative，但取樣會變兩倍慢")
    r.add_argument("--seed", type=int, default=0)
    r.add_argument("--group", type=int, default=6,
                   help="一個 prompt 塞幾支（模型只載入一次）；0 = 全部一次送，但中途失敗會整批掉")
    r.add_argument("--force", action="store_true")
    r.set_defaults(func=cmd_run)
    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
