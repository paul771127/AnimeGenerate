"""Generate one full-body character reference sheet per character with MiniMax H3.

Usage:  python -I gen_charsheets.py <shots.json> <ref_out_dir> [<character> ...]

Each character is rendered as the shortest possible H3 clip (5 frames) at 544x960 portrait,
20 steps, and the first decoded frame is saved as <ref_out_dir>/<character>.png.
All characters share one ComfyUI prompt (Qwen3-VL 32B is loaded once).
"""
import json
import shutil
import sys
import time
import urllib.request
import uuid
from pathlib import Path

COMFY = "http://127.0.0.1:8188"
COMFY_OUTPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\output")

UNET = "minimax_h3_fl2va_pruned_int8_convrot.safetensors"
TEXT_ENCODER = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors"
VIDEO_VAE = "minimax_h3_video_vae_fp16.safetensors"

WIDTH, HEIGHT = 544, 960
FRAMES = 5
STEPS = 20
SHIFT_VIDEO, SHIFT_AUDIO = 13.0, 3.0

SHEET_PROMPT = ("Character reference sheet, full-body standing pose, facing the camera directly, arms relaxed at the "
                "sides, whole body visible from head to feet, neutral expression, even soft lighting, plain flat light "
                "grey studio background, nothing else in the frame, still camera. Character: {desc}.")

# Per-character overrides for sheets the generic prompt gets wrong (monsters, back views, ghosts).
SHEET_OVERRIDES = {
    "Player": ("Character reference sheet, full-body view seen from directly behind, whole body visible from head to "
               "feet, standing still, even soft lighting, plain flat light grey studio background, nothing else in the "
               "frame, still camera. Character: a hooded traveler seen from the back, long weathered dark brown "
               "hooded cloak with a leather strap and a sword hilt at the hip, worn boots, the hood is up and the face "
               "is never shown."),
    "Mio": ("Character reference sheet, full-body standing pose, facing the camera, whole body visible, even soft "
            "lighting, plain flat light grey studio background, nothing else in the frame, still camera. Character: "
            "Mio, the ghostly singer of the old ship, a translucent pale blue spirit of a young woman wearing a long "
            "flowing old-fashioned dress with lace sleeves, very long flowing hair, gentle closed-eyes singing "
            "expression, soft glowing outline, faint drifting points of light around her."),
    "TreeKing": ("Creature reference sheet, a non-human monster, no people in the frame, the whole creature visible "
                 "from top to roots, centered, even soft lighting, plain flat light grey studio background, nothing "
                 "else in the frame, still camera. Creature: {desc}, a huge walking tree monster with a face of bark, "
                 "glowing purple spores drifting from its branches, a black crystal embedded in its chest."),
    "Kraken": ("Creature reference sheet, a non-human sea monster, no people in the frame, the whole creature visible "
               "including all tentacles, centered, even soft lighting, plain flat light grey studio background, nothing "
               "else in the frame, still camera. Creature: {desc}, a gigantic kraken with a bulbous head, glowing cyan "
               "eyes, cyan runes along its deep teal skin, a black crystal engraved with names on its chest."),
}


def api(path, data=None):
    req = urllib.request.Request(COMFY + path, data=None if data is None else json.dumps(data).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read())


def gpu_stats():
    try:
        s = api("/system_stats")
        d = s["devices"][0]
        return (f"VRAM used {(d['vram_total'] - d['vram_free']) / 2**30:.1f}/{d['vram_total'] / 2**30:.1f} GB, "
                f"RAM free {s['system']['ram_free'] / 2**30:.1f}/{s['system']['ram_total'] / 2**30:.1f} GB")
    except Exception as e:  # noqa: BLE001
        return f"stats unavailable: {e}"


def main():
    shots_path, ref_dir, *names = sys.argv[1:]
    spec = json.loads(Path(shots_path).read_text(encoding="utf-8"))
    ref_dir = Path(ref_dir)
    ref_dir.mkdir(parents=True, exist_ok=True)
    chars = spec["characters"]
    names = names or list(chars)

    wf = {
        "L_unet": {"class_type": "UNETLoader", "inputs": {"unet_name": UNET, "weight_dtype": "default"}},
        "L_shift": {"class_type": "MiniMaxH3SigmaShift",
                    "inputs": {"model": ["L_unet", 0], "shift_video": SHIFT_VIDEO, "shift_audio": SHIFT_AUDIO}},
        "L_clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": TEXT_ENCODER, "type": "minimax", "device": "default"}},
        "L_vae": {"class_type": "VAELoader", "inputs": {"vae_name": VIDEO_VAE}},
        "S_sampler": {"class_type": "KSamplerSelect", "inputs": {"sampler_name": "res_multistep"}},
        "S_sigmas": {"class_type": "BasicScheduler",
                     "inputs": {"model": ["L_shift", 0], "scheduler": "simple", "steps": STEPS, "denoise": 1.0}},
    }
    meta = {}
    for name in names:
        prompt = spec["style"] + " " + SHEET_OVERRIDES.get(name, SHEET_PROMPT).format(desc=chars[name])
        seed = int.from_bytes(("ref2_" + name).encode(), "big") % (2**32)
        t = f"c_{name}"
        prefix = f"spellduel_ref/{name}_{uuid.uuid4().hex[:6]}"
        wf.update({
            f"{t}_cond": {"class_type": "MiniMaxH3ImageToVideo",
                          "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0], "prompt": prompt,
                                     "width": WIDTH, "height": HEIGHT, "length": FRAMES}},
            f"{t}_done": {"class_type": "PreviewAny", "inputs": {"source": [f"{t}_cond", 0]}},
            f"{t}_noise": {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}},
            f"{t}_guider": {"class_type": "BasicGuider", "inputs": {"model": ["L_shift", 0], "conditioning": [f"{t}_cond", 0]}},
            f"{t}_sample": {"class_type": "SamplerCustomAdvanced",
                            "inputs": {"noise": [f"{t}_noise", 0], "guider": [f"{t}_guider", 0], "sampler": ["S_sampler", 0],
                                       "sigmas": ["S_sigmas", 0], "latent_image": [f"{t}_cond", 1]}},
            f"{t}_dec": {"class_type": "VAEDecode", "inputs": {"samples": [f"{t}_sample", 0], "vae": ["L_vae", 0]}},
            f"{t}_save": {"class_type": "SaveImage", "inputs": {"images": [f"{t}_dec", 0], "filename_prefix": prefix}},
        })
        meta[f"{t}_save"] = {"name": name, "seed": seed, "prompt": prompt}
        print(f"=== {name}  seed={seed}", flush=True)
    print(f"  before: {gpu_stats()}", flush=True)

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
        imgs = entry["outputs"].get(node_id, {}).get("images", [])
        if not imgs:
            raise RuntimeError(f"{info['name']}: no image output")
        src = COMFY_OUTPUT / imgs[0].get("subfolder", "") / imgs[0]["filename"]
        dest = ref_dir / f"{info['name']}.png"
        if dest.exists():  # keep the previous attempt for comparison
            shutil.move(dest, ref_dir / f"{info['name']}_v1.png")
        shutil.copyfile(src, dest)
        print(f"  {info['name']} -> {dest}", flush=True)
        results.append({**info, "file": str(dest)})
    print(f"  batch done in {secs:.0f}s for {len(names)} character(s)\n  after: {gpu_stats()}", flush=True)
    (ref_dir / "ref_log.json").write_text(json.dumps({"batch_seconds": round(secs), "size": [WIDTH, HEIGHT, FRAMES],
                                                      "steps": STEPS, "characters": results}, indent=2, ensure_ascii=False),
                                          encoding="utf-8")
    print("\nALL DONE", flush=True)


if __name__ == "__main__":
    main()
