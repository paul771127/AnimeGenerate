"""v1_s05 via keyframe: render a kneeling-Golem still from the Golem sheet, then two video variants.

Usage:  python -I gen_minimax_kf.py <shots.json> <out_dir> <ref_dir>

One ComfyUI prompt:
  still   : MiniMaxH3ReferenceToVideo(<Picture 1> = Golem sheet, 5 frames) -> first frame -> out/ref/v1_s05_keyframe.png
  variant A (v1_s05_mm.mp4) : MiniMaxH3ImageToVideo with the still as LAST frame (crystal shatters, colossus sinks to a knee)
  variant B (v1_s05_mmB.mp4): MiniMaxH3ImageToVideo with the still as FIRST frame (already kneeling, veins dim, camera pulls back)
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
COMFY_INPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\input")

UNET = "minimax_h3_fl2va_pruned_int8_convrot.safetensors"
TEXT_ENCODER = "qwen3vl_32b_minimax_h3_nvfp4_awq.safetensors"
VIDEO_VAE = "minimax_h3_video_vae_fp16.safetensors"
AUDIO_VAE = "minimax_h3_audio_vae_fp32.safetensors"
W, H, FRAMES, FPS, STEPS = 960, 544, 124, 24, 20

STILL_PROMPT = ("the Lava Colossus, the creature shown in <Picture 1> ({desc}), kneeling on one knee on the floor of a dark "
                "magma hall, head bowed low, the black crystal in its chest shattered into glowing fragments, its magma "
                "veins dimmed to a faint dull ember glow, smoke drifting, low angle, emotional, still frame")
PROMPT_A = ("the black crystal in the chest of the Lava Colossus ({desc}) shatters into glowing shards, the colossus "
            "slowly sinks down onto one knee and bows its head, the orange glow in its magma veins fades to a dull "
            "ember, magma hall, emotional, slow motion")
PROMPT_B = ("the Lava Colossus ({desc}) kneels defeated on one knee with its head bowed, the shattered black crystal "
            "fragments in its chest flicker and go dark, the last orange glow in its magma veins slowly fades to dull "
            "ember, smoke drifts, the camera slowly pulls back, magma hall, emotional, slow motion")


def api(path, data=None):
    req = urllib.request.Request(COMFY + path, data=None if data is None else json.dumps(data).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        body = r.read()
        return json.loads(body) if body else None


def video_chain(tag, cond_node, seed, prefix):
    n = lambda s: f"{tag}_{s}"  # noqa: E731
    return {
        n("noise"): {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}},
        n("guider"): {"class_type": "BasicGuider", "inputs": {"model": ["L_shift", 0], "conditioning": [cond_node, 0]}},
        n("sample"): {"class_type": "SamplerCustomAdvanced",
                      "inputs": {"noise": [n("noise"), 0], "guider": [n("guider"), 0], "sampler": ["S_sampler", 0],
                                 "sigmas": ["S_sigmas", 0], "latent_image": [cond_node, 1]}},
        n("dec_v"): {"class_type": "VAEDecode", "inputs": {"samples": [n("sample"), 0], "vae": ["L_vae", 0]}},
        n("dec_a"): {"class_type": "VAEDecodeAudio", "inputs": {"samples": [n("sample"), 0], "vae": ["L_avae", 0]}},
        n("video"): {"class_type": "CreateVideo", "inputs": {"images": [n("dec_v"), 0], "audio": [n("dec_a"), 0], "fps": FPS}},
        n("save"): {"class_type": "SaveVideo",
                    "inputs": {"video": [n("video"), 0], "filename_prefix": prefix, "format": "mp4", "codec": "h264"}},
    }


def main():
    shots_path, out_dir, ref_dir = sys.argv[1:4]
    spec = json.loads(Path(shots_path).read_text(encoding="utf-8"))
    out_dir, ref_dir = Path(out_dir), Path(ref_dir)
    desc = spec["characters"]["Golem"]
    style = spec["style"] + " "
    (COMFY_INPUT / "spellduel_ref").mkdir(parents=True, exist_ok=True)
    shutil.copyfile(ref_dir / "Golem.png", COMFY_INPUT / "spellduel_ref" / "Golem.png")
    api("/free", {"unload_models": True, "free_memory": True})

    run = uuid.uuid4().hex[:6]
    wf = {
        "L_unet": {"class_type": "UNETLoader", "inputs": {"unet_name": UNET, "weight_dtype": "default"}},
        "L_shift": {"class_type": "MiniMaxH3SigmaShift", "inputs": {"model": ["L_unet", 0], "shift_video": 13.0, "shift_audio": 3.0}},
        "L_clip": {"class_type": "CLIPLoader", "inputs": {"clip_name": TEXT_ENCODER, "type": "minimax", "device": "default"}},
        "L_vae": {"class_type": "VAELoader", "inputs": {"vae_name": VIDEO_VAE}},
        "L_avae": {"class_type": "VAELoader", "inputs": {"vae_name": AUDIO_VAE}},
        "S_sampler": {"class_type": "KSamplerSelect", "inputs": {"sampler_name": "res_multistep"}},
        "S_sigmas": {"class_type": "BasicScheduler", "inputs": {"model": ["L_shift", 0], "scheduler": "simple", "steps": STEPS, "denoise": 1.0}},
        # --- still ---
        "k_ref": {"class_type": "LoadImage", "inputs": {"image": "spellduel_ref/Golem.png"}},
        "k_cond": {"class_type": "MiniMaxH3ReferenceToVideo",
                   "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0], "audio_vae": ["L_avae", 0],
                              "prompt": style + STILL_PROMPT.format(desc=desc), "width": W, "height": H, "length": 5,
                              "ref_image_size": "match", "ref_images.ref_image_0": ["k_ref", 0]}},
        "k_noise": {"class_type": "RandomNoise", "inputs": {"noise_seed": 91505}},
        "k_guider": {"class_type": "BasicGuider", "inputs": {"model": ["L_shift", 0], "conditioning": ["k_cond", 0]}},
        "k_sample": {"class_type": "SamplerCustomAdvanced",
                     "inputs": {"noise": ["k_noise", 0], "guider": ["k_guider", 0], "sampler": ["S_sampler", 0],
                                "sigmas": ["S_sigmas", 0], "latent_image": ["k_cond", 1]}},
        "k_dec": {"class_type": "VAEDecode", "inputs": {"samples": ["k_sample", 0], "vae": ["L_vae", 0]}},
        "k_frame": {"class_type": "ImageFromBatch", "inputs": {"image": ["k_dec", 0], "batch_index": 0, "length": 1}},
        "k_save": {"class_type": "SaveImage", "inputs": {"images": ["k_frame", 0], "filename_prefix": f"spellduel_ref/v1_s05_keyframe_{run}"}},
        # --- variant A: still as LAST frame ---
        "a_cond": {"class_type": "MiniMaxH3ImageToVideo",
                   "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0], "prompt": style + PROMPT_A.format(desc=desc),
                              "width": W, "height": H, "length": FRAMES, "last_frame": ["k_frame", 0]}},
        # --- variant B: still as FIRST frame ---
        "b_cond": {"class_type": "MiniMaxH3ImageToVideo",
                   "inputs": {"clip": ["L_clip", 0], "vae": ["L_vae", 0], "prompt": style + PROMPT_B.format(desc=desc),
                              "width": W, "height": H, "length": FRAMES, "first_frame": ["k_frame", 0]}},
    }
    wf.update(video_chain("a", "a_cond", 1601384605, f"spellduel/v1_s05_mm_{run}"))
    wf.update(video_chain("b", "b_cond", 1601384606, f"spellduel/v1_s05_mmB_{run}"))

    t0 = time.time()
    pid = api("/prompt", {"prompt": wf, "client_id": "spellduel"})["prompt_id"]
    print(f"=== v1_s05 keyframe pipeline submitted, prompt_id={pid}", flush=True)
    while True:
        time.sleep(20)
        hist = api(f"/history/{pid}")
        if pid in hist:
            break
    secs = time.time() - t0
    entry = hist[pid]
    if entry.get("status", {}).get("status_str") != "success":
        msgs = [m for m in entry["status"].get("messages", []) if m[0] == "execution_error"]
        raise RuntimeError(f"failed after {secs:.0f}s: {json.dumps(msgs, ensure_ascii=False)[:3000]}")
    outs = entry["outputs"]
    still = outs["k_save"]["images"][0]
    shutil.copyfile(COMFY_OUTPUT / still.get("subfolder", "") / still["filename"], ref_dir / "v1_s05_keyframe.png")
    print(f"  keyframe -> {ref_dir / 'v1_s05_keyframe.png'}", flush=True)
    for node, dest in (("a_save", "v1_s05_mm.mp4"), ("b_save", "v1_s05_mmB.mp4")):
        items = outs[node].get("images", []) + outs[node].get("videos", [])
        item = next(i for i in items if str(i.get("filename", "")).endswith(".mp4"))
        shutil.copyfile(COMFY_OUTPUT / item.get("subfolder", "") / item["filename"], out_dir / dest)
        print(f"  {dest} written", flush=True)
    print(f"  done in {secs:.0f}s\nALL DONE", flush=True)
    api("/free", {"unload_models": True, "free_memory": True})


if __name__ == "__main__":
    main()
