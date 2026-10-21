"""Render SpellDuel character shots with MiniMax H3, conditioned on character reference sheets.

Usage:  python -I gen_minimax_ref.py <shots.json> <out_dir> <ref_dir> <workflow_out.json> <suffix> <WxH> <steps> <shot_id> [...]
e.g.    ... out out/ref workflow_minimax_h3_ref2v.json _mm 960x544 20 v0_s05

For every {Name} placeholder in a shot's prompt, <ref_dir>/<Name>.png is copied into ComfyUI's
input folder and fed to MiniMaxH3ReferenceToVideo as ref_image_i; the placeholder is replaced by
"<Name>, the character shown in <Picture i>: <description>" so the prompt uses the node's tags.
Shots without placeholders fall back to the plain MiniMaxH3ImageToVideo text conditioning.
All shots share the loaders in one prompt; each conditioning feeds a PreviewAny output so the
scheduler encodes every prompt before the first sampler. Models are unloaded via /free first.
"""
import json
import re
import shutil
import sys
import time
import urllib.request
import uuid
from pathlib import Path

COMFY = "http://127.0.0.1:8188"
COMFY_OUTPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\output")
COMFY_INPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\input")

UNET = "minimax_h3_fl2va_pruned_int8_convrot.safetensors"
TEXT_ENCODER = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors"
VIDEO_VAE = "minimax_h3_video_vae_fp16.safetensors"
AUDIO_VAE = "minimax_h3_audio_vae_fp32.safetensors"

FRAMES = 124
FPS = 24
SHIFT_VIDEO, SHIFT_AUDIO = 13.0, 3.0
SAMPLER = "res_multistep"


def api(path, data=None):
    req = urllib.request.Request(COMFY + path, data=None if data is None else json.dumps(data).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        body = r.read()
        return json.loads(body) if body else None


def gpu_stats():
    try:
        s = api("/system_stats")
        d = s["devices"][0]
        return (f"VRAM used {(d['vram_total'] - d['vram_free']) / 2**30:.1f}/{d['vram_total'] / 2**30:.1f} GB, "
                f"RAM free {s['system']['ram_free'] / 2**30:.1f}/{s['system']['ram_total'] / 2**30:.1f} GB")
    except Exception as e:  # noqa: BLE001
        return f"stats unavailable: {e}"


def shared_nodes(steps):
    return {
        "L_unet": {"class_type": "UNETLoader", "inputs": {"unet_name": UNET, "weight_dtype": "default"}},
        "L_shift": {"class_type": "MiniMaxH3SigmaShift",
                    "inputs": {"model": ["L_unet", 0], "shift_video": SHIFT_VIDEO, "shift_audio": SHIFT_AUDIO}},
        "L_clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": TEXT_ENCODER, "type": "minimax", "device": "default"}},
        "L_vae": {"class_type": "VAELoader", "inputs": {"vae_name": VIDEO_VAE}},
        "L_avae": {"class_type": "VAELoader", "inputs": {"vae_name": AUDIO_VAE}},
        "S_sampler": {"class_type": "KSamplerSelect", "inputs": {"sampler_name": SAMPLER}},
        "S_sigmas": {"class_type": "BasicScheduler",
                     "inputs": {"model": ["L_shift", 0], "scheduler": "simple", "steps": steps, "denoise": 1.0}},
    }


def build_prompt(shot, spec, ref_dir):
    """Return (prompt_text, [(name, png_path), ...]) with <Picture i> tags for every referenced character."""
    chars = spec["characters"]
    order = []
    for m in re.finditer(r"\{(\w+)\}", shot["prompt"]):
        if m.group(1) in chars and m.group(1) not in order:
            order.append(m.group(1))
    refs = [(n, ref_dir / f"{n}.png") for n in order if (ref_dir / f"{n}.png").exists()]
    idx = {n: i + 1 for i, (n, _) in enumerate(refs)}

    def sub(m):
        n = m.group(1)
        if n in idx:
            return f"{n}, the character shown in <Picture {idx[n]}> ({chars[n]})"
        return chars.get(n, m.group(0))

    body = re.sub(r"\{(\w+)\}", sub, shot["prompt"])
    text = spec["style"] + " " + body if spec.get("style_position") == "prefix" else body + ", " + spec["style"]
    return text, refs


def shot_nodes(tag, text, refs, seed, prefix, width, height):
    n = lambda name: f"{tag}_{name}"  # noqa: E731
    nodes = {}
    if refs:
        cond_inputs = {"clip": ["L_clip", 0], "vae": ["L_vae", 0], "audio_vae": ["L_avae", 0], "prompt": text,
                       "width": width, "height": height, "length": FRAMES, "ref_image_size": "match"}
        for i, (name, _) in enumerate(refs):
            nodes[n(f"img{i}")] = {"class_type": "LoadImage", "inputs": {"image": f"spellduel_ref/{name}.png"}}
            cond_inputs[f"ref_images.ref_image_{i}"] = [n(f"img{i}"), 0]
        nodes[n("cond")] = {"class_type": "MiniMaxH3ReferenceToVideo", "inputs": cond_inputs}
    else:
        nodes[n("cond")] = {"class_type": "MiniMaxH3ImageToVideo",
                            "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0], "prompt": text,
                                       "width": width, "height": height, "length": FRAMES}}
    nodes.update({
        n("cond_done"): {"class_type": "PreviewAny", "inputs": {"source": [n("cond"), 0]}},
        n("noise"): {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}},
        n("guider"): {"class_type": "BasicGuider", "inputs": {"model": ["L_shift", 0], "conditioning": [n("cond"), 0]}},
        n("sample"): {"class_type": "SamplerCustomAdvanced",
                      "inputs": {"noise": [n("noise"), 0], "guider": [n("guider"), 0], "sampler": ["S_sampler", 0],
                                 "sigmas": ["S_sigmas", 0], "latent_image": [n("cond"), 1]}},
        n("dec_v"): {"class_type": "VAEDecode", "inputs": {"samples": [n("sample"), 0], "vae": ["L_vae", 0]}},
        n("dec_a"): {"class_type": "VAEDecodeAudio", "inputs": {"samples": [n("sample"), 0], "vae": ["L_avae", 0]}},
        n("video"): {"class_type": "CreateVideo", "inputs": {"images": [n("dec_v"), 0], "audio": [n("dec_a"), 0], "fps": FPS}},
        n("save"): {"class_type": "SaveVideo",
                    "inputs": {"video": [n("video"), 0], "filename_prefix": prefix, "format": "mp4", "codec": "h264"}},
    })
    return nodes


def seed_for(shot_id):
    return int.from_bytes(shot_id.encode(), "big") % (2**32)


def main():
    shots_path, out_dir, ref_dir, wf_out, suffix, size, steps, *ids = sys.argv[1:]
    width, height = (int(x) for x in size.lower().split("x"))
    steps = int(steps)
    spec = json.loads(Path(shots_path).read_text(encoding="utf-8"))
    out_dir, ref_dir = Path(out_dir), Path(ref_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    by_id = {s["id"]: s for v in spec["videos"] for s in v["shots"]}
    overrides_path = out_dir / "prompt_overrides.json"
    if overrides_path.exists():
        for sid, text in json.loads(overrides_path.read_text(encoding="utf-8")).items():
            if sid in by_id:
                by_id[sid] = {**by_id[sid], "prompt": text}
                print(f"  (prompt override applied for {sid})", flush=True)

    # Reference sheets must live under ComfyUI/input for LoadImage.
    (COMFY_INPUT / "spellduel_ref").mkdir(parents=True, exist_ok=True)
    for png in ref_dir.glob("*.png"):
        shutil.copyfile(png, COMFY_INPUT / "spellduel_ref" / png.name)

    template = shared_nodes(steps)
    template.update(shot_nodes("shot", "{POSITIVE with <Picture 1> tags}", [("Erin", None)], 0,
                               "spellduel/{SHOT_ID}", width, height))
    Path(wf_out).write_text(json.dumps(template, indent=2, ensure_ascii=False), encoding="utf-8")

    # Drop whatever the previous batch left in VRAM (e.g. the LTX model) before loading H3.
    api("/free", {"unload_models": True, "free_memory": True})

    wf = shared_nodes(steps)
    meta = {}
    for i, sid in enumerate(ids):
        shot = by_id[sid]
        text, refs = build_prompt(shot, spec, ref_dir)
        seed = seed_for(sid)
        prefix = f"spellduel/{sid}{suffix}_{uuid.uuid4().hex[:6]}"
        tag = f"s{i}"
        wf.update(shot_nodes(tag, text, refs, seed, prefix, width, height))
        meta[f"{tag}_save"] = {"id": sid, "seed": seed, "refs": [n for n, _ in refs], "prompt": text}
        print(f"=== {sid}  seed={seed}  refs={[n for n, _ in refs]}\n  prompt: {text}", flush=True)
    print(f"  size={width}x{height} steps={steps} frames={FRAMES}\n  before: {gpu_stats()}", flush=True)

    t0 = time.time()
    pid = api("/prompt", {"prompt": wf, "client_id": "spellduel"})["prompt_id"]
    while True:
        time.sleep(15)
        hist = api(f"/history/{pid}")
        if pid in hist:
            break
        print(f"  {time.time() - t0:6.0f}s  {gpu_stats()}", flush=True)
    secs = time.time() - t0
    entry = hist[pid]
    status = entry.get("status", {})
    if status.get("status_str") != "success":
        msgs = [m for m in status.get("messages", []) if m[0] == "execution_error"]
        raise RuntimeError(f"batch failed after {secs:.0f}s: {json.dumps(msgs, ensure_ascii=False)[:3000]}")

    results = []
    for node_id, info in meta.items():
        produced = None
        outs = entry["outputs"].get(node_id, {})
        for item in outs.get("images", []) + outs.get("videos", []):
            if str(item.get("filename", "")).endswith(".mp4"):
                produced = COMFY_OUTPUT / item.get("subfolder", "") / item["filename"]
        if produced is None or not produced.exists():
            raise RuntimeError(f"{info['id']}: no mp4 in outputs of {node_id}: {json.dumps(outs)[:800]}")
        dest = out_dir / f"{info['id']}{suffix}.mp4"
        shutil.copyfile(produced, dest)
        print(f"  {info['id']} -> {dest}  ({dest.stat().st_size / 2**20:.1f} MB)", flush=True)
        results.append({**info, "file": str(dest)})
    print(f"  batch done in {secs:.0f}s for {len(ids)} shot(s)\n  after: {gpu_stats()}", flush=True)
    log_path = out_dir / f"render_log{suffix}.json"
    previous = json.loads(log_path.read_text(encoding="utf-8")) if log_path.exists() else {}
    batches = previous.get("batches", [])
    batches.append({"batch_seconds": round(secs), "model": "minimax_h3_ref", "size": [width, height, FRAMES],
                    "steps": steps, "shots": results})
    log_path.write_text(json.dumps({"batches": batches}, indent=2, ensure_ascii=False), encoding="utf-8")
    api("/free", {"unload_models": True, "free_memory": True})
    print("\nALL DONE", flush=True)


if __name__ == "__main__":
    main()
