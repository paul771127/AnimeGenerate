"""副本大地圖特效影片生成：shots.json -> Docs/dungeon_fx/out/<id>.mp4

用法（用 ComfyUI 內建的 python，只用標準庫 + numpy/PIL）:
  python -I gen_fx.py list
  python -I gen_fx.py run --only fx_DG_gate_open_volcano,fx_DG_area_clear,fx_DG_lava_vent
  python -I gen_fx.py run                        # 全部 26 支（已有的跳過，--force 重生）
  python -I gen_fx.py check out/fx_DG_lava_vent.mp4
  python -I gen_fx.py sheet --only fx_DG_area_clear      # 做成接觸印樣，肉眼看背景

從 explore_fx/tools/gen_fx.py 改的，同一套呼叫 ComfyUI 的方式：
  - 所有 clip 共用 loaders，塞在同一個 prompt 裡
  - 每個 conditioning 接一個 PreviewAny，排程器會先把文字全部編碼完才開始取樣，
    所以 32B 文字編碼器和影片模型各只載入一次（一支一個 prompt 的話每支都要重載）
  - 送出前先 /free

跟 explore_fx 的差別：這 26 支**全部是文字生影片、全部黑底**，所以把首尾幀
（keyframe / LoadImage）那一段整個拿掉了，檢查也只留黑底的。

輸出會照 README 的規格：960x544、24fps、H.264、**拿掉聲音軌**，長度就是模型原生的
124 frame（5.17 秒）—— 裁切、加速、循環淡接都是遊戲端做的。
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

UNET = "minimax_h3_fl2va_pruned_int8_convrot.safetensors"
TEXT_ENCODER = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors"
VIDEO_VAE = "minimax_h3_video_vae_fp16.safetensors"
AUDIO_VAE = "minimax_h3_audio_vae_fp32.safetensors"

W, H, FRAMES, FPS, STEPS = 960, 544, 124, 24, 20
SHIFT_VIDEO, SHIFT_AUDIO = 13.0, 3.0
SAMPLER, SCHEDULER = "res_multistep", "simple"
MAX_MB = 3.0

FX = Path(__file__).resolve().parents[1]          # Docs/dungeon_fx
SHOTS = FX / "shots.json"
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


# shots.json 的 backgrounds 只描述「背景長怎樣」，沒有說「背景不准變」，也沒說
# 「不准碰到邊」。這兩件是 README 規格裡的硬要求，所以補上。
# （explore_fx 實測過：不鎖的話中段會自己浮出地面或暗角。）
BG_HOLD = ("the background stays pure solid black for every frame, no ground plane appears, "
           "no fog, no haze, no vignette, no light spill on the background")
MARGIN = ("the effect is centered with empty black space around it and does not touch the "
          "edges of the frame")

# 這三支 shots.json 自己就寫了效果要從邊緣進來（「from the bottom of the frame」、
# 「blowing sideways across the frame」），加 MARGIN 會跟提示詞自己打架，所以不加。
EDGE_BY_DESIGN = {"fx_DG_area_clear", "fx_DG_lava_vent", "fx_DG_canyon_dust"}


# 第一輪跑出來不合格的，在這裡補一句和換 seed，再用 --force 重生。
# explore_fx 學到的：模型對「小、細、少」很不敏感，會自己放大成塞滿畫面的東西，
# 所以要明講尺寸、並且明確列出不准出現的東西，光說 negative 沒用（cfg=1 時根本沒吃）。
OVERRIDES = {
    # 第一、二輪都畫成一朵蓮花加一顆發光球。正面提示詞裡寫「no flower」沒有用
    # ——模型對正面句子裡的否定幾乎無感，反而把「flower」餵得更熟。
    # 第三輪改成：正面只描述要的東西，不要的全部丟進 negative，並開 cfg 讓它生效。
    # 第三輪開 cfg 4 之後蓮花變成一隻動漫角色 —— 模型一定要在正中間放一個「主角」。
    # 第四輪把 cfg 降到 2（4 太高，角鬥封印那支直接燒出直條紋），並且在負面裡
    # 連「人物」一起擋掉，正面改用粒子的講法（dust motes），不要再提 spores。
    # 這支連四輪都在正中間放一個主角：蓮花 → 蓮花 → 動漫角色 → 動漫角色。
    # 第五輪才看出來為什麼：青綠色＋動漫＋發光點，模型一路滑到 Vocaloid 那類角色
    # （雙馬尾、青綠髮）。所以負面要指名道姓擋那個族群，光寫 girl / character 不夠。
    "fx_DG_forest_fireflies": {
        "seed": 8301,
        "cfg": 2.0,
        "extra": ("a loose scattering of small glowing specks at random positions across the "
                  "dark field, like dust motes catching light, drifting slowly and blinking "
                  "independently, each speck only a few pixels wide, the middle of the frame "
                  "is empty darkness"),
        "negative": ("hatsune miku, vocaloid, idol, anime girl, twintails, long teal hair, "
                     "humanoid, mascot, character design, girl, woman, character, person, "
                     "figure, fairy, doll, face, body, dress, flower, lotus, blossom, petals, "
                     "plant, leaves, water lily, orb, sphere, moon, large glowing ball, "
                     "single light source, centerpiece object"),
    },
    # 第一輪整張畫面都是中灰（邊緣中位數 9），而且畫成卡通海浪不是水氣。
    "fx_DG_sea_spray": {
        "seed": 8002,
        "extra": ("a small puff of fine white mist low in the frame, the rest of the frame is "
                  "pure black, the background is black not grey and not blue, there is no "
                  "ocean, no sea surface, no wave, no breaking wave, no horizon, no water line"),
    },
    # 第一、二輪都是一整片不透明的米色雲堆佔滿下半畫面（下緣比上緣亮 96 階）。
    # 第三輪 cfg 4 畫成一塊平塗的橘色剪影，比雲堆更不像沙塵。第四輪降到 cfg 2，
    # 負面補上「平塗色塊」這類講法。
    "fx_DG_canyon_dust": {
        "seed": 8203,
        "cfg": 2.0,
        "extra": ("a faint translucent streak of fine airborne grains drifting sideways, the "
                  "individual grains are visible as separate specks, very sparse and thin, "
                  "most of the frame stays empty"),
        "negative": ("cloud, clouds, cumulus, cloud bank, billowing smoke, fog bank, solid "
                     "cloud shapes, flat color shape, opaque silhouette, vector graphic, "
                     "sand dune, desert landscape, ground, horizon, opaque mass"),
    },
    # 第一輪畫出一枚金幣實體，而且整張罩著一層金色霧。
    "fx_DG_chest_open": {
        "seed": 8004,
        "extra": ("a narrow vertical beam of light and a few small separate sparkles, nothing "
                  "else, there is no coin object, no medallion, no disc, no treasure chest, "
                  "the light does not spread into a wide glow or haze, the rest of the frame "
                  "stays pure black"),
    },
    # 這兩支的「沙塵」被畫成一整塊棕色霧罩住畫面，第二輪還是 20.6%。
    "fx_DG_gate_open_arena": {
        "seed": 8105,
        "cfg": 4.0,
        "extra": ("the sand grains fly as separate bright specks with hard clean edges against "
                  "black, each spark is a distinct point"),
        "negative": ("smoke, haze, fog, mist, dust cloud, brown fog, glow halo, bloom, soft "
                     "gradient background, grey background"),
    },
    # 第二輪的第一格直接畫成一片沙丘風景照（框裡有沙丘和天空），不是光做的結界。
    "fx_DG_gate_open_canyon": {
        "seed": 8106,
        "cfg": 4.0,
        "extra": ("the barrier is made of glowing amber light only, the rock chips fly as "
                  "separate solid pieces with hard clean edges against black"),
        "negative": ("landscape, scenery, desert, sand dune, dunes, sky, clouds, horizon, "
                     "photograph, painting of a place, window, smoke, haze, fog, dust cloud, "
                     "brown fog, grey background"),
    },
    # 第一輪在封印下面畫了一塊會反光的地板（下緣比上緣亮 40 階）。
    "fx_DG_gate_idle_clockwork": {
        "seed": 8007,
        "extra": ("there is no floor, no ground, no reflection, no mirrored surface, nothing "
                  "below the barrier, the area under the barrier is pure black"),
    },
}


def compose(spec, clip):
    """提示詞 = style + clip.prompt + backgrounds[clip.background] (+ 背景穩定子句)"""
    parts = [spec["style"], clip["prompt"]]
    bg = spec["backgrounds"].get(clip.get("background", "black"), "")
    if bg:
        parts.append(bg)
    parts.append(BG_HOLD)
    if clip["id"] not in EDGE_BY_DESIGN:
        parts.append(MARGIN)
    extra = OVERRIDES.get(clip["id"], {}).get("extra")
    if extra:
        parts.append(extra)
    return ", ".join(p.strip().rstrip(",") for p in parts if p.strip())


def seed_for(cid, seed_base):
    return OVERRIDES.get(cid, {}).get("seed", (seed_base + zlib.crc32(cid.encode())) % 2**31)


def build_graph(spec, clips, steps, cfg, seed_base, run):
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
    def negative_node(cid):
        """OVERRIDES 給了 negative 的話那支用自己的，其餘共用 shots.json 的；用到才建。"""
        extra_neg = OVERRIDES.get(cid, {}).get("negative")
        node = "N_cond" if not extra_neg else "N_" + uuid.uuid5(uuid.NAMESPACE_OID, cid).hex[:8]
        if node not in wf:
            text = ", ".join(x for x in (spec.get("negative", ""), extra_neg or "") if x)
            wf[node] = {"class_type": "MiniMaxH3ImageToVideo",
                        "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0],
                                   "prompt": text, "width": W, "height": H,
                                   "length": FRAMES}}
            wf[node + "_done"] = {"class_type": "PreviewAny", "inputs": {"source": [node, 0]}}
        return node

    saves = {}
    for clip in clips:
        cid = clip["id"]
        tag = "c" + uuid.uuid5(uuid.NAMESPACE_OID, cid).hex[:8]
        n = lambda s, tag=tag: f"{tag}_{s}"  # noqa: E731
        seed = seed_for(cid, seed_base)

        wf[n("cond")] = {"class_type": "MiniMaxH3ImageToVideo",
                         "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0],
                                    "prompt": compose(spec, clip),
                                    "width": W, "height": H, "length": FRAMES}}
        # 讓排程器先把所有文字編碼完，再開始第一次取樣
        wf[n("cond_done")] = {"class_type": "PreviewAny",
                              "inputs": {"source": [n("cond"), 0]}}
        wf[n("noise")] = {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}}
        # 難搞的那幾支在 OVERRIDES 裡自己指定 cfg，其餘吃命令列的
        clip_cfg = OVERRIDES.get(cid, {}).get("cfg", cfg)
        if clip_cfg > 1.0:
            wf[n("guider")] = {"class_type": "CFGGuider",
                               "inputs": {"model": ["L_shift", 0],
                                          "positive": [n("cond"), 0],
                                          "negative": [negative_node(cid), 0],
                                          "cfg": clip_cfg}}
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
                                    "filename_prefix": f"dungeon_fx/{cid}_{run}",
                                    "format": "mp4", "codec": "h264"}}
        saves[cid] = (n("save"), seed)
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


def frames_of(mp4, times, scale=None):
    """抓幾個時間點的畫格，回傳 [np.ndarray(H,W,3) float]。"""
    import numpy as np
    from PIL import Image
    OUT.mkdir(parents=True, exist_ok=True)
    tmp = OUT / f"_f_{uuid.uuid4().hex[:6]}.png"
    got = []
    for t in times:
        cmd = ["ffmpeg", "-y", "-loglevel", "error", "-ss", f"{t:.2f}",
               "-i", str(mp4), "-frames:v", "1"]
        if scale:
            cmd += ["-vf", f"scale={scale[0]}:{scale[1]}"]
        subprocess.run(cmd + [str(tmp)], check=False)
        if tmp.exists():
            got.append(np.asarray(Image.open(tmp).convert("RGB")).astype(float))
            tmp.unlink()
    return got


def check_background(mp4):
    """量外框 8% 的邊緣像素 + 四角，回報黑底乾不乾淨。"""
    try:
        import numpy as np
    except ImportError:
        return "（缺 numpy/PIL，跳過檢查）"
    got = frames_of(mp4, (0.2, 1.3, 2.6, 3.9, 5.0))
    if not got:
        return "（取不到畫格）"
    border, corner = [], []
    for a in got:
        h, w = a.shape[:2]
        m = max(2, int(min(h, w) * 0.08))
        border.append(np.concatenate([a[:m].reshape(-1, 3), a[-m:].reshape(-1, 3),
                                      a[:, :m].reshape(-1, 3), a[:, -m:].reshape(-1, 3)]))
        # 四角抓暗角／地板：特效的光飄到上下邊是正常的，飄到角落不是
        k = max(8, int(min(h, w) * 0.12))
        corner.append(np.concatenate([a[:k, :k].reshape(-1, 3), a[:k, -k:].reshape(-1, 3),
                                      a[-k:, :k].reshape(-1, 3), a[-k:, -k:].reshape(-1, 3)]))
    b = np.concatenate(border).mean(axis=1)
    c = np.concatenate(corner).mean(axis=1)
    # 用中位數和超標比例判斷，不能用最亮值：特效本身的光點飄到邊緣是正常的，
    # 真正要抓的是地面／灰霧／暗角那種「一整片都不黑」。
    med, hot = float(np.median(b)), float((b > 16).mean() * 100)
    cmed, chot = float(np.median(c)), float((c > 16).mean() * 100)
    ok = med < 2 and hot < 12 and cmed < 2 and chot < 10
    return (f"邊緣 中位={med:.1f} >16={hot:.1f}% | 角落 中位={cmed:.1f} >16={chot:.1f}% "
            f"{'OK' if ok else '不夠黑（有地面/霧/暗角），建議換 seed 重生'}")


def cmd_margin(args):
    """把主體縮小、四周補黑，確保不碰到畫面邊緣。

    為什麼用後製不用重生：MARGIN 那句話本來就在提示詞裡了，模型還是照樣把封印撐到
    上緣、把金環推出左右 —— explore_fx 那次也是，模型對「小一點」很不敏感。縮放補黑
    是確定會成功的做法（留多少黑邊是算出來的，不是求模型），而且畫面內容原封不動，
    風格不會因為重抽 seed 而跑掉。原始的全出血版本留在 out/_fullbleed/。
    """
    spec = load_shots()
    keep = OUT / "_fullbleed"
    keep.mkdir(parents=True, exist_ok=True)
    s = args.scale
    iw, ih = (int(W * s) // 2 * 2, int(H * s) // 2 * 2)
    x, y = (W - iw) // 2, (H - ih) // 2
    print(f"縮到 {iw}x{ih}，四周黑邊 左右{x}px 上下{y}px")
    for clip in select(spec, args):
        src = OUT / f"{clip['id']}.mp4"
        if not src.exists():
            print(f"  -- {clip['id']} 還沒生成")
            continue
        orig = keep / f"{clip['id']}.mp4"
        if not orig.exists():
            shutil.copyfile(src, orig)
        tmp = src.with_suffix(".pad.mp4")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(orig), "-an",
                        "-vf", f"scale={iw}:{ih}:flags=lanczos,pad={W}:{H}:{x}:{y}:black",
                        "-c:v", "libx264", "-crf", "23", "-pix_fmt", "yuv420p",
                        str(tmp)], check=True)
        tmp.replace(src)
        if src.stat().st_size > MAX_MB * 1024 * 1024:
            tmp = src.with_suffix(".crf.mp4")
            subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(src), "-an",
                            "-c:v", "libx264", "-crf", "28", "-pix_fmt", "yuv420p",
                            str(tmp)], check=True)
            tmp.replace(src)
        print(f"  ✓ {clip['id']:26s} {src.stat().st_size / 1024 / 1024:.1f}MB")


def cmd_sheet(args):
    """每支抓 6 格拼成一張圖，肉眼確認背景是純黑、主體在中間。"""
    import numpy as np
    from PIL import Image
    spec = load_shots()
    clips = [c for c in select(spec, args) if (OUT / f"{c['id']}.mp4").exists()]
    if not clips:
        sys.exit("out/ 裡沒有這些 clip 的 mp4")
    cols, cw, ch = 6, 320, 181
    sheet = Image.new("RGB", (cols * cw, len(clips) * ch), (40, 0, 60))
    for r, clip in enumerate(clips):
        got = frames_of(OUT / f"{clip['id']}.mp4",
                        [0.1 + i * 5.0 / (cols - 1) for i in range(cols)], scale=(cw, ch))
        for i, a in enumerate(got[:cols]):
            sheet.paste(Image.fromarray(a.astype(np.uint8)), (i * cw, r * ch))
    dest = OUT / (args.name or "_sheet.png")
    sheet.save(dest)
    print(f"{dest}  ({len(clips)} 支 x {cols} 格)")
    for r, clip in enumerate(clips):
        print(f"  第 {r + 1} 列: {clip['id']}  {clip['zh']}")


def cmd_list(args):
    spec = load_shots()
    for c in spec["clips"]:
        done = "有" if (OUT / f"{c['id']}.mp4").exists() else "-"
        print(f"{c['id']:26s} {c['background']:6s} {c['mode']:5s} "
              f"{c['seconds']:>4}s out={done}  {c['zh']}")


def cmd_prompts(args):
    spec = load_shots()
    for c in select(spec, args):
        print(f"=== {c['id']}  seed={seed_for(c['id'], 0)}\n{compose(spec, c)}\n")


def cmd_check(args):
    for p in args.files:
        p = Path(p)
        print(f"{p.name}: {check_background(p)}")


def select(spec, args):
    clips = spec["clips"]
    if getattr(args, "only", None):
        want = [s.strip() for s in args.only.split(",") if s.strip()]
        by_id = {c["id"]: c for c in clips}
        missing = [w for w in want if w not in by_id]
        if missing:
            sys.exit(f"shots.json 裡沒有: {missing}")
        clips = [by_id[w] for w in want]
    return clips


def cmd_run(args):
    spec = load_shots()
    clips = select(spec, args)
    if not args.force:
        have = [c["id"] for c in clips if (OUT / f"{c['id']}.mp4").exists()]
        clips = [c for c in clips if c["id"] not in have]
        if have:
            print(f"已存在，跳過: {', '.join(have)}")
    if not clips:
        print("沒有要生成的 clip。")
        return

    OUT.mkdir(parents=True, exist_ok=True)
    size = args.group if args.group > 0 else len(clips)
    groups = [clips[i:i + size] for i in range(0, len(clips), size)]
    for gi, group in enumerate(groups, 1):
        print(f"\n=== 第 {gi}/{len(groups)} 批 ===", flush=True)
        try:
            submit_group(spec, group, args)
        except Exception as e:  # noqa: BLE001
            # 一批失敗不要把後面的也拖掉
            print(f"  !! 第 {gi} 批失敗: {e}", flush=True)


def submit_group(spec, clips, args):
    run = uuid.uuid4().hex[:6]
    wf, saves = build_graph(spec, clips, args.steps, args.cfg, args.seed, run)
    (OUT / f"_workflow_{run}.json").write_text(
        json.dumps(wf, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"送出 {len(clips)} 支 clip（一個 prompt，模型只載入一次）"
          f" steps={args.steps} cfg={args.cfg}")
    for c in clips:
        print(f"  {c['id']:26s} {c['mode']:5s} seed={seed_for(c['id'], args.seed)}")

    api("/free", {"unload_models": True, "free_memory": True})
    t0 = time.time()
    pid = api("/prompt", {"prompt": wf, "client_id": "dungeon_fx"})["prompt_id"]
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
    for cid, (node, seed) in saves.items():
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
        note = check_background(dest)
        mb = dest.stat().st_size / 1024 / 1024
        print(f"  ✓ {cid:26s} {mb:.1f}MB  {note}", flush=True)
        report[cid] = {"seed": seed, "steps": args.steps, "cfg": args.cfg,
                       "mode": clip["mode"], "seconds": clip["seconds"],
                       "size_mb": round(mb, 2), "bg_check": note}
    log = OUT / "run_log.json"
    old = json.loads(log.read_text(encoding="utf-8")) if log.exists() else {}
    old.update(report)
    log.write_text(json.dumps(old, ensure_ascii=False, indent=2), encoding="utf-8")
    api("/free", {"unload_models": True, "free_memory": True})
    print(f"完成，{secs / 60:.0f} 分（{secs / 60 / max(1, len(clips)):.1f} 分/支）-> {OUT}")


def main():
    p = argparse.ArgumentParser(description="副本大地圖特效影片生成 (MiniMax H3)")
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list").set_defaults(func=cmd_list)
    pr = sub.add_parser("prompts")
    pr.add_argument("--only")
    pr.set_defaults(func=cmd_prompts)
    c = sub.add_parser("check")
    c.add_argument("files", nargs="+")
    c.set_defaults(func=cmd_check)
    m = sub.add_parser("margin")
    m.add_argument("--only")
    m.add_argument("--scale", type=float, default=0.84,
                   help="主體縮到原來的幾成，其餘補黑；0.84 = 四周約 8% 黑邊")
    m.set_defaults(func=cmd_margin)
    s = sub.add_parser("sheet")
    s.add_argument("--only")
    s.add_argument("--name", help="輸出檔名，預設 _sheet.png")
    s.set_defaults(func=cmd_sheet)
    r = sub.add_parser("run")
    r.add_argument("--only", help="逗號分隔的 clip id")
    r.add_argument("--steps", type=int, default=STEPS)
    r.add_argument("--cfg", type=float, default=1.0,
                   help=">1 才會用 shots.json 的 negative，但取樣會變兩倍慢")
    r.add_argument("--seed", type=int, default=0)
    r.add_argument("--group", type=int, default=6,
                   help="一個 prompt 塞幾支（模型只載入一次）；0 = 全部一次送")
    r.add_argument("--force", action="store_true")
    r.set_defaults(func=cmd_run)
    args = p.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
