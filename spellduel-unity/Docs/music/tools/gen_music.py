"""Generate the SpellDuel soundtrack from tracks.json on the local ComfyUI + ACE-Step 1.5 turbo rig.

Usage (ComfyUI's embedded python, stdlib + numpy only):
    python -I gen_music.py list
    python -I gen_music.py prompts [--only title ch0]
    python -I gen_music.py run --only title ch0 ch0_boss explore
    python -I gen_music.py post --only title            # redo the loop/master step, no regeneration
    python -I gen_music.py check out/title.mp3 ...

Two stages, deliberately separate so the expensive one never has to repeat:

  run   ACE-Step 1.5 renders a LONGER piece than the track needs, saved lossless to out/raw/<id>.flac.
  post  Cuts a bar-aligned seamless loop out of that raw take, normalises it and encodes the mp3.

`post` runs automatically at the end of `run`, but can be re-run on its own: every loop/level
decision is cheap and reversible, only the generation is not.

Why generate long: ACE-Step composes to fill the `duration` it is told, so a 90 s request gets a
90 s *piece* with an intro and an ending - neither of which loops. Asking for ~30 s more and then
cutting a loop out of the middle gives steady-state music, which is what a game needs.
"""
import argparse
import json
import math
import shutil
import subprocess
import sys
import time
import urllib.request
import uuid
import zlib
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent          # Docs/music
TRACKS = ROOT / "tracks.json"
OUT = ROOT / "out"
RAW = OUT / "raw"

COMFY = "http://127.0.0.1:8188"
COMFY_OUTPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\output")
COMFY_INPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\input")
INPUT_SUBDIR = "spellduel_music"
REF_SECONDS = 30.0        # enough of the normal theme to carry its timbre, cheap to encode

CKPT = "ace_step_1.5_turbo_aio.safetensors"
SR_MODEL = 48000          # ACE-Step 1.5 renders at 48 kHz
SR_OUT = 44100            # README: 44.1 kHz stereo 128 kbps mp3
MP3_KBPS = "128k"
LUFS, TRUE_PEAK, LRA = -16.0, -1.0, 11.0

STEPS, CFG = 8, 1.0       # turbo checkpoint: 8 steps is plenty, cfg 1 needs no negative pass
SAMPLER, SCHEDULER = "euler", "simple"

PHRASE_BARS = 8           # loop length is a whole number of 8-bar phrases
XFADE_BARS = 2            # README: cross-fade the last 1-2 bars before the opening
LEAD_IN_BARS = 4          # skip the intro before looking for a loop start
SEARCH_BARS = 8           # how far to hunt for the best-matching loop start
# ACE-Step composes an ending and then trails off, so a take holds less usable audio than the
# duration it was asked for - measured 91-98% across the first batch. Ask for extra so the
# search still has every candidate available instead of one survivor.
TAIL_MARGIN = 1.20
FLATTEN_DB = 3.0          # most slow level drift the loop may be corrected by
FLATTEN_SEC = 6.0         # envelope smoothing: slower than a phrase, so bar dynamics survive

# keyscale is a fixed enum on the node; anything it cannot express (modes, pentatonic) still goes
# into the prompt text, which is what the README asks for.
KEYSCALE_ENUM = {f"{r} {q}" for q in ("major", "minor")
                 for r in ("C", "C#", "Db", "D", "D#", "Eb", "E", "F", "F#", "Gb",
                           "G", "G#", "Ab", "A", "A#", "Bb", "B")}
# modes whose tonic triad has a major third - "phrygian dominant" is in here on purpose
MAJOR_THIRD_MODES = ("ionian", "lydian", "mixolydian", "phrygian dominant")


# --------------------------------------------------------------------------- spec

def load_spec():
    return json.loads(TRACKS.read_text(encoding="utf-8"))


def bar_seconds(bpm):
    return 240.0 / bpm      # one 4/4 bar


def keyscale_for(key):
    """Split tracks.json's `key` into (enum value for the node, leftover words for the prompt)."""
    if key in KEYSCALE_ENUM:
        return key, ""
    parts = key.split()
    for n in (2, 1):
        head = " ".join(parts[:n])
        if head in KEYSCALE_ENUM:
            return head, " ".join(parts[n:])
    # A mode has no enum value, so fall back to the enum that shares its THIRD - lydian and
    # mixolydian are major modes and would fight the prompt if forced to the parallel minor.
    # The mode name itself still goes into the prompt, which is where the colour comes from.
    root, mode = parts[0], " ".join(parts[1:]).lower()
    quality = "major" if any(m in mode for m in MAJOR_THIRD_MODES) else "minor"
    enum = f"{root} {quality}"
    return (enum if enum in KEYSCALE_ENUM else f"A {quality}"), " ".join(parts[1:])


def plan(track):
    """Everything derived from a track's bpm/seconds/loop: loop length, cross-fade, render length."""
    bpm, bar = track["bpm"], bar_seconds(track["bpm"])
    phrase = PHRASE_BARS * bar
    if track["loop"]:
        phrases = max(1, round(track["seconds"] / phrase))
        loop_len = phrases * phrase
        xfade = XFADE_BARS * bar
        lead_in = LEAD_IN_BARS * bar
        search = SEARCH_BARS * bar
        gen = math.ceil((lead_in + search + loop_len + xfade) * TAIL_MARGIN / bar) * bar
    else:
        # victory / defeat: play once. Keep the model's own ending, just render a little slack.
        phrases, loop_len, xfade, lead_in, search = 0, float(track["seconds"]), 0.0, 0.0, 0.0
        gen = math.ceil((loop_len + 8.0) / bar) * bar
    return {"bpm": bpm, "bar": bar, "phrases": phrases, "loop_len": loop_len,
            "xfade": xfade, "lead_in": lead_in, "search": search, "gen": gen}


def compose(spec, track):
    keyscale, leftover = keyscale_for(track["key"])
    bits = [spec["style"], track["prompt"], f"{track['bpm']} BPM", track["key"]]
    if track["loop"]:
        bits.append("steady groove all the way through, no intro build-up, no ending, "
                    "consistent instrumentation and loudness from start to finish")
    else:
        bits.append("clean final chord, ends decisively")
    return ", ".join(b for b in bits if b), keyscale, leftover


def seed_for(track_id, salt=0):
    return (zlib.crc32(track_id.encode()) + salt * 7919) % (2 ** 31)


# --------------------------------------------------------------------------- comfyui

def api(path, data=None, timeout=60):
    req = urllib.request.Request(
        COMFY + path, data=None if data is None else json.dumps(data).encode(),
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        body = r.read()
        return json.loads(body) if body else None


def gpu_stats():
    try:
        s = api("/system_stats")
        d = s["devices"][0]
        return (f"VRAM {(d['vram_total'] - d['vram_free']) / 2**30:.1f}/{d['vram_total'] / 2**30:.1f} GB, "
                f"RAM free {s['system']['ram_free'] / 2**30:.1f} GB")
    except Exception as e:  # noqa: BLE001
        return f"stats unavailable: {e}"


def shared_nodes():
    return {"L_ckpt": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": CKPT}}}


def stage_reference(src, track_id, seconds):
    """Put the reference theme where LoadAudio can see it, looped to `seconds`.

    ReferenceTimbreAudio caps the generated length at the reference's length - a 30 s reference
    gave 29.9 s of audio in a 132 s request, with or without the code LLM. Since the reference is
    a seamless loop anyway, repeating it to the full render length costs nothing and lifts that
    cap. (`-stream_loop -1` with `-t` repeats the input until the duration is filled.)
    """
    d = COMFY_INPUT / INPUT_SUBDIR
    d.mkdir(parents=True, exist_ok=True)
    name = f"ref_{track_id}.flac"
    ffmpeg(["-stream_loop", "-1", "-i", str(src), "-t", f"{seconds:.2f}",
            "-ac", "2", "-ar", str(SR_MODEL), "-c:a", "flac", str(d / name)])
    return f"{INPUT_SUBDIR}/{name}"


def track_nodes(tag, tags_text, neg_text, seed, bpm, keyscale, gen_seconds, prefix, steps, cfg,
                ref=None):
    """Nodes for one track. `ref` is a path under ComfyUI/input to use as a timbre reference.

    The README wants a dungeon's boss theme to sound like family with its normal theme, and asks
    for the normal theme to be used as an audio reference where the model supports it - ACE-Step
    1.5 does, via ReferenceTimbreAudio on the conditioning. The code LLM is switched off when a
    reference is given, as that node's own tooltip advises.
    """
    n = lambda s: f"{tag}_{s}"  # noqa: E731
    enc = {"clip": ["L_ckpt", 1], "lyrics": "[instrumental]", "seed": seed, "bpm": int(bpm),
           "duration": round(gen_seconds, 2), "timesignature": "4", "language": "en",
           "keyscale": keyscale, "cfg_scale": 2.0, "temperature": 0.85, "top_p": 0.9,
           "top_k": 0, "min_p": 0.0}
    nodes = {
        n("pos"): {"class_type": "TextEncodeAceStepAudio1.5",
                   "inputs": {**enc, "tags": tags_text, "generate_audio_codes": True}},
    }
    pos = [n("pos"), 0]
    if ref is not None:
        nodes.update({
            n("ref_a"): {"class_type": "LoadAudio", "inputs": {"audio": ref}},
            n("ref_l"): {"class_type": "VAEEncodeAudio",
                         "inputs": {"audio": [n("ref_a"), 0], "vae": ["L_ckpt", 2]}},
            n("ref_c"): {"class_type": "ReferenceTimbreAudio",
                         "inputs": {"conditioning": [n("pos"), 0], "latent": [n("ref_l"), 0]}},
        })
        pos = [n("ref_c"), 0]
    if cfg > 1.0:
        # Real negative pass only when the sampler will actually use it. generate_audio_codes is
        # off here: the code LLM is the slow half of the encode and the negative does not need it.
        nodes[n("neg")] = {"class_type": "TextEncodeAceStepAudio1.5",
                           "inputs": {**enc, "tags": neg_text, "generate_audio_codes": False}}
    else:
        nodes[n("neg")] = {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": pos}}
    nodes.update({
        # PreviewAny on every conditioning makes ComfyUI's scheduler run all the text encodes
        # before the first sampler, so the 4B code LLM loads once per prompt instead of once
        # per track. Same trick as Docs/story_video/tools and Docs/explore_fx/tools.
        n("pos_done"): {"class_type": "PreviewAny", "inputs": {"source": pos}},
        n("lat"): {"class_type": "EmptyAceStep1.5LatentAudio",
                   "inputs": {"seconds": round(gen_seconds, 2), "batch_size": 1}},
        n("ks"): {"class_type": "KSampler",
                  "inputs": {"model": ["L_ckpt", 0], "positive": pos, "negative": [n("neg"), 0],
                             "latent_image": [n("lat"), 0], "seed": seed, "steps": steps, "cfg": cfg,
                             "sampler_name": SAMPLER, "scheduler": SCHEDULER, "denoise": 1.0}},
        n("dec"): {"class_type": "VAEDecodeAudio", "inputs": {"samples": [n("ks"), 0], "vae": ["L_ckpt", 2]}},
        n("save"): {"class_type": "SaveAudio", "inputs": {"audio": [n("dec"), 0], "filename_prefix": prefix}},
    })
    return nodes


def submit(jobs, steps, cfg):
    """Render one ComfyUI prompt holding every job in `jobs`; returns id -> raw flac path."""
    wf, meta = shared_nodes(), {}
    for i, j in enumerate(jobs):
        tag = f"t{i}"
        prefix = f"spellduel_music/{j['id']}_{uuid.uuid4().hex[:6]}"
        wf.update(track_nodes(tag, j["tags"], j["negative"], j["seed"], j["bpm"],
                              j["keyscale"], j["gen"], prefix, steps, cfg, j.get("ref")))
        meta[f"{tag}_save"] = j
        print(f"=== {j['id']}  seed={j['seed']} bpm={j['bpm']} key={j['keyscale']} "
              f"render={j['gen']:.1f}s -> loop {j['loop_len']:.2f}s"
              + (f"\n    ref: {j['ref']}" if j.get("ref") else "")
              + f"\n    {j['tags']}", flush=True)
    print(f"  steps={steps} cfg={cfg} sampler={SAMPLER}/{SCHEDULER}\n  before: {gpu_stats()}", flush=True)

    api("/free", {"unload_models": True, "free_memory": True})
    t0 = time.time()
    pid = api("/prompt", {"prompt": wf, "client_id": "spellduel_music"})["prompt_id"]
    while True:
        time.sleep(15)
        hist = api(f"/history/{pid}")
        if pid in hist:
            break
        print(f"  {time.time() - t0:6.0f}s  {gpu_stats()}", flush=True)
    secs = time.time() - t0
    entry = hist[pid]
    if entry.get("status", {}).get("status_str") != "success":
        msgs = [m for m in entry.get("status", {}).get("messages", []) if m[0] == "execution_error"]
        raise RuntimeError(f"render failed after {secs:.0f}s: {json.dumps(msgs, ensure_ascii=False)[:3000]}")

    produced = {}
    for node_id, j in meta.items():
        got = None
        outs = entry["outputs"].get(node_id, {})
        for item in outs.get("audio", []) + outs.get("images", []):
            got = COMFY_OUTPUT / item.get("subfolder", "") / item["filename"]
        if got is None or not got.exists():
            raise RuntimeError(f"{j['id']}: no audio in {node_id}: {json.dumps(outs)[:600]}")
        RAW.mkdir(parents=True, exist_ok=True)
        dest = RAW / f"{j['id']}{got.suffix}"
        shutil.copyfile(got, dest)
        produced[j["id"]] = dest
        print(f"  {j['id']} -> {dest.name}  ({dest.stat().st_size / 2**20:.1f} MB)", flush=True)
    print(f"  rendered {len(jobs)} track(s) in {secs:.0f}s ({secs / len(jobs) / 60:.1f} min each)\n"
          f"  after: {gpu_stats()}", flush=True)
    return produced, secs


# --------------------------------------------------------------------------- audio

def ffmpeg(args):
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostdin", "-y", *args],
                       capture_output=True, text=True, encoding="utf-8", errors="replace")
    if p.returncode != 0:
        raise RuntimeError(f"ffmpeg failed: {' '.join(args)[:300]}\n{(p.stderr or '')[-2500:]}")
    return p.stderr or ""


def decode(path, sr):
    """Whole file as float32 (n, 2)."""
    p = subprocess.run(["ffmpeg", "-hide_banner", "-nostdin", "-v", "error", "-i", str(path),
                        "-f", "f32le", "-ac", "2", "-ar", str(sr), "-"],
                       stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if p.returncode != 0:
        raise RuntimeError(f"decode failed for {path}: {p.stderr.decode('utf-8', 'replace')[-1500:]}")
    return np.frombuffer(p.stdout, dtype="<f4").reshape(-1, 2).astype(np.float32)


def encode_wav(audio, sr, path):
    p = subprocess.Popen(["ffmpeg", "-hide_banner", "-nostdin", "-y", "-v", "error",
                          "-f", "f32le", "-ac", "2", "-ar", str(sr), "-i", "-",
                          "-c:a", "pcm_f32le", str(path)],
                         stdin=subprocess.PIPE, stderr=subprocess.PIPE)
    _, err = p.communicate(audio.astype("<f4").tobytes())
    if p.returncode != 0:
        raise RuntimeError(f"wav write failed: {err.decode('utf-8', 'replace')[-1500:]}")


def spectrum(x, bands=48):
    """Coarse log-ish band energies of a window, for comparing two candidate seam points."""
    mono = x.mean(axis=1)
    if mono.size < 2048:
        mono = np.pad(mono, (0, 2048 - mono.size))
    win = np.hanning(2048).astype(np.float32)
    acc = np.zeros(1025, dtype=np.float64)
    hop, frames = 1024, 0
    for i in range(0, mono.size - 2048, hop):
        acc += np.abs(np.fft.rfft(mono[i:i + 2048] * win))
        frames += 1
    acc /= max(frames, 1)
    edges = np.unique(np.geomspace(1, 1025, bands + 1).astype(int))
    return np.array([acc[a:b].mean() for a, b in zip(edges[:-1], edges[1:])])


def envelope(audio, sr, hop_sec=0.1):
    """Frame RMS of the mix, one value every hop_sec."""
    hop = max(1, int(sr * hop_sec))
    n = (audio.shape[0] // hop) * hop
    frames = audio[:n].mean(axis=1).reshape(-1, hop)
    return np.sqrt((frames ** 2).mean(axis=1) + 1e-12), hop_sec


def usable_end(audio, sr):
    """Where the take stops being audio. ACE-Step pads the tail with near-silence.

    Thresholded against a high percentile, NOT the median: when a take is mostly silence the
    median IS silence, so a median-relative threshold sits under the noise floor and reports the
    whole file as usable. town_0 rendered 73 s of music in a 146 s take and this said 145.9 s.
    """
    env, hop_sec = envelope(audio, sr)
    live = np.flatnonzero(env > 0.1 * np.percentile(env, 90))
    return float(live[-1] * hop_sec) if live.size else 0.0


def flatten(audio, sr, max_db=FLATTEN_DB, smooth_sec=FLATTEN_SEC):
    """Take the slow level arc out of a loop body, leaving bar-to-bar dynamics alone.

    The model writes a *piece* - it opens quiet, builds, and tapers - so often no window of the
    take has matching level at both ends. Left alone that shows up as the level jumping back at
    every wrap (title came out +1.9 dB, explore -2.8 dB). Correcting it with a gain curve derived
    from a 6 s-smoothed envelope spreads the same correction over the whole loop: ~2 dB across
    90 s is 0.02 dB/s, inaudible, where the jump at the wrap is not.

    Applied to the body BEFORE the cross-fade cut, so the cut still joins samples that were
    adjacent in the source and the wrap stays continuous.

    Attenuation only, never boost. ACE-Step's output already touches full scale (0.013% of
    samples on the brass/timpani tracks), so lifting a quiet stretch towards the mean amplifies
    material that is already clipped and leaves true-peak overshoot no limiter can take back.
    Cutting towards the quietest part instead reaches the same flatness and can only lower peaks
    - the level comes back when the track is normalised to -16 LUFS a step later.
    """
    env, hop_sec = envelope(audio, sr)
    k = max(1, int(round(smooth_sec / hop_sec)))
    pad = np.concatenate([np.full(k, env[0]), env, np.full(k, env[-1])])
    smooth = np.convolve(pad, np.ones(2 * k + 1) / (2 * k + 1), mode="same")[k:k + env.size]
    db = 20 * np.log10(smooth)
    ref = np.percentile(db, 10)                  # aim at the quiet end, so gain is never positive
    gain_db = np.clip(ref - db, -max_db, 0.0)
    # resample the per-frame gain up to per-sample and apply
    xs = (np.arange(gain_db.size) + 0.5) * hop_sec * sr
    g = np.interp(np.arange(audio.shape[0]), xs, gain_db)
    out = audio * (10 ** (g / 20))[:, None]
    return out.astype(np.float32), {"applied_db": [round(float(gain_db.min()), 2),
                                                   round(float(gain_db.max()), 2)]}


def pick_loop_start(audio, sr, p):
    """Choose a bar-aligned start whose cross-fade regions match best.

    The cross-fade blends audio[t0 : t0+xfade] with audio[t0+loop : t0+loop+xfade]. Those two
    windows sit at the same position in the bar, so they are already beat-aligned; picking the
    candidate where they also agree in timbre and level is what makes the seam inaudible.

    Scoring the cross-fade windows alone was not enough: it says the splice itself is clean but
    says nothing about whether the loop comes round LOUDER than it ended, which is what actually
    got rejected (town_2 -2.3 dB, ch1 +1.8 dB, tower +4.1 dB). So each candidate is built for
    real and measured on the finished article, and the loop LENGTH is searched too - one phrase
    either side of the target - because sometimes no start point works at a fixed length.
    """
    bar, xf = p["bar"], p["xfade"]
    take_rms = float(np.sqrt((audio ** 2).mean()))
    for floor in (0.1 * take_rms, 1e-4):
        rows = _candidates(audio, sr, p, bar, xf, floor)
        if rows:
            break
        print(f"    (no candidate above {floor:.4f} rms, relaxing the quiet filter)", flush=True)
    if not rows:
        raise RuntimeError("render too short, or too quiet, for the requested loop length")
    # Candidates that would pass come first. Among those, rank mainly on how far inside its
    # own allowance the seam sits - once silence can no longer inflate `timbre`, the surviving
    # candidates' fit values are all much of a muchness and the seam is what separates them.
    # Nominal length wins outright among candidates that pass: `seconds` in tracks.json is the
    # finished length the game was designed around, and a shorter loop repeats more audibly. The
    # +-1 phrase alternatives are an escape hatch for takes where no nominal-length loop works,
    # not a free parameter - left unweighted they pulled tracks 33% away from spec.
    rows.sort(key=lambda r: (r["penalty"] > 0, r["penalty"], abs(r["phrases"] - p["phrases"]),
                             abs(r["step"]) / r["allow"] + 0.3 * r["click"] - 0.3 * r["fit"]))
    best = rows[0]
    for r in sorted(rows, key=lambda r: (r["phrases"], r["bar"]))[:40]:
        print(f"    {r['phrases'] * PHRASE_BARS:>3} bars  bar{r['bar']:>3}  t0={r['t0']:7.2f}s  "
              f"fit={r['fit']:.4f} seam={r['step']:+6.2f}dB tail={r['quiet']:.3f} "
              f"click={r['click']:.2f} (allow {r['allow']:.2f})"
              f"{'  <-' if r is best else ''}", flush=True)
    return best


def _candidates(audio, sr, p, bar, xf, quiet_floor):
    rows = []
    for dphr in (0, -1, 1):
        phrases = p["phrases"] + dphr
        if phrases < 1:
            continue
        q = {**p, "phrases": phrases, "loop_len": phrases * PHRASE_BARS * bar}
        for k in range(SEARCH_BARS + 1):
            t0 = p["lead_in"] + k * bar
            if int(round((t0 + q["loop_len"] + xf) * sr)) > audio.shape[0]:
                continue
            a = audio[int(round(t0 * sr)):int(round((t0 + xf) * sr))]
            b = audio[int(round((t0 + q["loop_len"]) * sr)):
                      int(round((t0 + q["loop_len"] + xf) * sr))]
            ra, rb = float(np.sqrt((a ** 2).mean())), float(np.sqrt((b ** 2).mean()))
            # Relative to the take, not an absolute floor: two near-silent windows score ~0.99
            # timbre because silence matches silence, which let quiet gaps outrank real music.
            if min(ra, rb) < quiet_floor:
                continue
            sa, sb = spectrum(a), spectrum(b)
            timbre = float(np.dot(sa, sb) / (np.linalg.norm(sa) * np.linalg.norm(sb) + 1e-9))
            level = 1.0 - abs(ra - rb) / (max(ra, rb) + 1e-9)
            loop, _ = make_loop(audio, sr, q, t0)
            m = loop_metrics(loop, sr, bar)
            # The search measures head/tail level RELATIVE to the loop while `verdict` applies an
            # absolute 0.02 floor to the mastered file, and LUFS is K-weighted so the two do not
            # convert exactly: 0.15 here still produced a 0.0177 head. 0.22 leaves real margin.
            # `verdict` rejects a click above 3x the track's own p99.9 jump, so steer well clear.
            penalty = (max(0.0, abs(m["step"]) - m["allow"])
                       + 3.0 * max(0.0, 1.0 - m["quiet"] / 0.22)
                       + 2.0 * max(0.0, m["click_ratio"] / 2.0 - 1.0))
            rows.append({"t0": t0, "bar": k, "phrases": phrases, "timbre": timbre,
                         "level": level, "step": m["step"], "allow": m["allow"],
                         "quiet": m["quiet"], "click": m["click_ratio"], "penalty": penalty,
                         "fit": 0.65 * timbre + 0.35 * level})
    return rows


def loop_metrics(loop, sr, bar):
    """Everything `verdict` will later reject a loop for, measured on the candidate itself.

    Each criterion the search ignores is one it will happily optimise straight into: scoring only
    the splice chose loops that came back 1.8 dB louder, and adding the seam step alone then chose
    a loop whose head and tail were nearly silent. So seam step, the piece's own phrase movement
    and the head/tail level all get measured here, the same way they are measured on the finished
    mp3 later.
    """
    one = int(1.0 * sr)
    rms = lambda x: float(np.sqrt((x ** 2).mean())) + 1e-12  # noqa: E731
    if loop.shape[0] < 2 * one:
        return {"step": 0.0, "allow": 1.5, "quiet": 1.0}
    step = 20 * math.log10(rms(loop[:one]) / rms(loop[-one:]))
    phrase, steps, k = PHRASE_BARS * bar, [], 1
    while int(k * phrase * sr) + one < loop.shape[0]:
        i = int(k * phrase * sr)
        steps.append(abs(20 * math.log10(rms(loop[i:i + one]) / rms(loop[i - one:i]))))
        k += 1
    half = int(0.5 * sr)
    # the game restarts the track from sample 0, so neither end may be a near-silent passage
    quiet = min(rms(loop[:half]), rms(loop[-half:])) / rms(loop)
    # Predicted click once this is an mp3. The cross-fade makes the wrap continuous in float, but
    # mp3 carries a 1105-sample LAME encoder delay (measured: start_time 0.025057 s) and a decode
    # that does not strip it hands back near-silence where the loop should start. The audible
    # step is then just the signal level at the boundary - so keep those 25 ms quiet and the
    # artefact goes with it, whatever the decoder does.
    edge = int(round(0.025 * sr))
    click = max(float(np.abs(loop[:edge]).max()), float(np.abs(loop[-edge:]).max()))
    typ = float(np.percentile(np.abs(np.diff(loop, axis=0)).max(axis=1), 99.9))
    return {"step": step, "allow": max(1.5, max(steps, default=0.0)), "quiet": quiet,
            "click_ratio": click / (typ + 1e-12)}


def make_loop(audio, sr, p, t0):
    """Cut a loop of exactly p['loop_len'] whose end runs back into its own start.

    body = audio[t0 : t0+loop+xfade]; result = body[0:loop] with the first `xfade` seconds
    cross-faded from body[loop : loop+xfade]. Then playing result and jumping back to 0 steps
    from body[loop-1] to body[loop] - adjacent samples of the original take - so the wrap is
    continuous by construction, not by luck.
    """
    L = int(round(p["loop_len"] * sr))
    X = int(round(p["xfade"] * sr))
    s = int(round(t0 * sr))
    body = audio[s:s + L + X]
    if body.shape[0] < L + X:
        raise RuntimeError("render too short to cut the loop")
    body, flat = flatten(body, sr)
    out = body[:L].copy()
    t = np.linspace(0.0, 1.0, X, endpoint=False, dtype=np.float32)[:, None]
    # equal-power cross-fade: holds level whether the two windows correlate or not
    w_in, w_out = np.sin(t * np.pi / 2), np.cos(t * np.pi / 2)
    out[:X] = body[:X] * w_in + body[L:L + X] * w_out
    return out, flat


def master(src, dst, sr_out):
    """Static gain to -16 LUFS with a true-peak ceiling, verified on the finished mp3.

    Deliberately NOT ffmpeg's `loudnorm`:
      * its dynamic mode rides the level, which would put a loudness ramp across the cross-fade
        and reintroduce the very seam step the README warns about;
      * its linear mode applies one gain (good) but silently backs that gain off when the gain
        would breach the true-peak ceiling, so sparse tracks land quiet - `defeat` came out at
        -16.7 LUFS that way. A game that mixes those tracks needs them equally loud, so the
        ceiling is held with a limiter instead and the loudness target is met.
    Because mp3 encoding shifts both loudness and peaks a little, the gain is re-measured on the
    actual mp3 and corrected until it lands, rather than trusted from the source measurement.
    """
    m0 = ebur128(src)
    gain = LUFS - m0["I"]
    # alimiter works on sample peaks while the spec is a TRUE peak, and mp3 encoding nudges both,
    # so start below the ceiling and walk it down if the finished file still measures over.
    ceil_db = TRUE_PEAK - 0.5
    history = []
    for _ in range(5):
        # Limit at 4x the output rate: the spec is a TRUE peak, and the inter-sample peaks that
        # a 48 kHz sample-domain limiter cannot see are precisely what the clipped transients in
        # the render turn into. Oversampling puts them inside the limiter's reach.
        af = (f"volume={gain:.3f}dB,"
              f"aresample={sr_out * 4}:resampler=soxr:precision=28,"
              f"alimiter=limit={10 ** (ceil_db / 20):.6f}:attack=5:release=50:level=false,"
              f"aresample={sr_out}:resampler=soxr:precision=28")
        ffmpeg(["-i", str(src), "-af", af, "-ar", str(sr_out), "-ac", "2",
                "-c:a", "libmp3lame", "-b:a", MP3_KBPS, str(dst)])
        got = ebur128(dst)
        history.append({"gain_db": round(gain, 3), "ceil_db": round(ceil_db, 2),
                        "I": got["I"], "TP": got["TP"]})
        over = (got["TP"] or -99) - TRUE_PEAK
        if abs(got["I"] - LUFS) <= 0.3 and over <= 0.0:
            break
        if over > 0.0:
            ceil_db -= over + 0.2
        gain += LUFS - got["I"]
    return {"source": m0, "passes": history}


def ebur128(path):
    log = ffmpeg(["-i", str(path), "-af", "ebur128=peak=true:framelog=verbose", "-f", "null", "-"])
    tail = log[log.rfind("Integrated loudness"):]
    def grab(label):
        i = tail.find(label)
        if i < 0:
            return None
        return float(tail[i + len(label):].split()[0])
    return {"I": grab("I:"), "LRA": grab("LRA:"), "TP": grab("Peak:")}


def seam_report(path, p):
    """Play the finished loop twice and measure the wrap point against the rest of the track."""
    a = decode(path, SR_OUT)
    if a.shape[0] == 0:
        raise RuntimeError(f"{path} decoded empty")
    two = np.concatenate([a, a], axis=0)
    seam = a.shape[0]
    rms = lambda x: float(np.sqrt((x ** 2).mean())) + 1e-12  # noqa: E731
    # The perceptual question is "does it get louder or quieter when it comes round again", so
    # compare the second before the wrap with the second after. A 0.25 s window was too short to
    # mean anything on percussive material - one cymbal hit moved it by over a dB.
    one = int(1.0 * SR_OUT)
    before, after = two[seam - one:seam], two[seam:seam + one]
    # sample-to-sample jump at the wrap, against the loudest jump anywhere in the track
    d_seam = float(np.abs(two[seam] - two[seam - 1]).max())
    d_typ = float(np.percentile(np.abs(np.diff(a, axis=0)).max(axis=1), 99.9))
    head, tail = a[:int(0.5 * SR_OUT)], a[-int(0.5 * SR_OUT):]
    # A level step at the wrap only matters compared with how this piece already moves between
    # phrases. `title` steps +3.3..+20.8 dB at its own internal 8-bar boundaries - judging its
    # seam against a flat 1.5 dB would reject a join quieter than every transition around it.
    phrase = PHRASE_BARS * p["bar"]
    steps, k = [], 1
    while int(k * phrase * SR_OUT) + one < a.shape[0]:
        i = int(k * phrase * SR_OUT)
        steps.append(20 * math.log10(rms(a[i:i + one]) / rms(a[i - one:i])))
        k += 1
    return {
        "seconds": a.shape[0] / SR_OUT,
        "seam_level_db": 20 * math.log10(rms(after) / rms(before)),
        "phrase_steps_db": [round(s, 2) for s in steps],
        "phrase_step_max_db": round(max((abs(s) for s in steps), default=0.0), 2),
        "seam_jump": d_seam,
        "jump_p999": d_typ,
        "head_rms": rms(head),
        "tail_rms": rms(tail),
        "peak": float(np.abs(a).max()),
    }


def probe(path):
    p = subprocess.run(["ffprobe", "-v", "error", "-show_entries",
                        "stream=codec_name,sample_rate,channels,bit_rate:format=duration,size",
                        "-of", "json", str(path)], capture_output=True, text=True)
    return json.loads(p.stdout)


def post(track, p, raw, verbose=True):
    """raw flac -> bar-aligned loop -> -16 LUFS -> 44.1k 128k mp3 in out/."""
    OUT.mkdir(parents=True, exist_ok=True)
    audio = decode(raw, SR_MODEL)
    dst = OUT / f"{track['id']}.mp3"
    tmp = OUT / f".{track['id']}.wav"
    info = {"id": track["id"], "raw_seconds": round(audio.shape[0] / SR_MODEL, 2)}

    if track["loop"]:
        live = usable_end(audio, SR_MODEL)
        info["usable_seconds"] = round(live, 2)
        need = p["loop_len"] + p["xfade"] + p["lead_in"]
        if live < need:
            raise RuntimeError(
                f"{track['id']}: only {live:.1f}s of usable audio in a {info['raw_seconds']}s take, "
                f"need {need:.1f}s - re-render with a longer duration")
        if verbose:
            print(f"  {track['id']}: picking loop start "
                  f"(bar={p['bar']:.4f}s, {p['phrases']}x{PHRASE_BARS} bars = {p['loop_len']:.2f}s, "
                  f"xfade={p['xfade']:.2f}s, usable {live:.1f}s of {info['raw_seconds']}s)", flush=True)
        pick = pick_loop_start(audio, SR_MODEL, p)
        q = {**p, "phrases": pick["phrases"], "loop_len": pick["phrases"] * PHRASE_BARS * p["bar"]}
        body, flat = make_loop(audio, SR_MODEL, q, pick["t0"])
        info.update({"loop_start": round(pick["t0"], 3), "loop_seconds": round(q["loop_len"], 3),
                     "xfade_seconds": round(q["xfade"], 3), "bars": pick["phrases"] * PHRASE_BARS,
                     "predicted_seam_db": round(pick["step"], 2),
                     "seam_allowance_db": round(pick["allow"], 2), "flatten": flat})
        p = q
    else:
        keep = min(audio.shape[0], int(round(track["seconds"] * SR_MODEL)))
        body = audio[:keep]
        info.update({"loop_seconds": round(keep / SR_MODEL, 3), "loop": False})

    encode_wav(body, SR_MODEL, tmp)
    info["master"] = master(tmp, dst, SR_OUT)
    tmp.unlink(missing_ok=True)
    info["measured"] = ebur128(dst)
    if track["loop"]:
        info["seam"] = seam_report(dst, p)
    info["mp3"] = {"bytes": dst.stat().st_size,
                   "streams": probe(dst).get("streams"), "format": probe(dst).get("format")}
    if verbose:
        m, s = info["measured"], info.get("seam", {})
        print(f"  {track['id']} -> {dst.name}  {dst.stat().st_size / 1000:.0f} kB  "
              f"I={m['I']} LUFS TP={m['TP']} dBTP"
              + (f"  seam {s['seam_level_db']:+.2f} dB, jump {s['seam_jump']:.4f} "
                 f"(p99.9 {s['jump_p999']:.4f})" if s else ""), flush=True)
    return info


# --------------------------------------------------------------------------- checks

def verdict(track, info):
    """Hard spec checks from the README. Anything here failing means do not ship the file."""
    bad = []
    m, s = info["measured"], info.get("seam")
    st = (info["mp3"]["streams"] or [{}])[0]
    if abs((m["I"] or -99) - LUFS) > 0.6:
        bad.append(f"loudness {m['I']} LUFS (want {LUFS}+-0.6)")
    if (m["TP"] or 0) > TRUE_PEAK:
        bad.append(f"true peak {m['TP']} dBTP (want <= {TRUE_PEAK})")
    if st.get("codec_name") != "mp3" or int(st.get("sample_rate", 0)) != SR_OUT or st.get("channels") != 2:
        bad.append(f"format {st.get('codec_name')} {st.get('sample_rate')} {st.get('channels')}ch")
    if s:
        # 1.5 dB floor so a genuinely steady track gets no free pass; above that, the piece's own
        # phrase transitions set the bar, because the loop restarts on a phrase downbeat too.
        allow = max(1.5, s["phrase_step_max_db"])
        if abs(s["seam_level_db"]) > allow:
            bad.append(f"level step at the seam {s['seam_level_db']:+.2f} dB "
                       f"(its own phrase boundaries move at most {s['phrase_step_max_db']:.2f} dB)")
        if s["seam_jump"] > max(3 * s["jump_p999"], 0.05):
            bad.append(f"click at the seam (jump {s['seam_jump']:.4f} vs p99.9 {s['jump_p999']:.4f})")
        if s["head_rms"] < 0.02 or s["tail_rms"] < 0.02:
            bad.append(f"fades to silence (head rms {s['head_rms']:.4f}, tail {s['tail_rms']:.4f})")
        if abs(s["seconds"] - info["loop_seconds"]) > 0.08:
            bad.append(f"mp3 is {s['seconds']:.3f}s, loop should be {info['loop_seconds']:.3f}s")
    if info["mp3"]["bytes"] > 4_000_000:
        bad.append(f"{info['mp3']['bytes'] / 1e6:.1f} MB")
    return bad


# --------------------------------------------------------------------------- cli

def selected(spec, only):
    by_id = {t["id"]: t for t in spec["tracks"]}
    if not only:
        return list(spec["tracks"])
    missing = [i for i in only if i not in by_id]
    if missing:
        sys.exit(f"unknown track id(s): {missing}")
    return [by_id[i] for i in only]


def cmd_list(spec, args):
    print(f"{'id':<16}{'bpm':>5}{'key':<22}{'sec':>6}{'loop':>6}  bars   loop_len  render")
    for t in spec["tracks"]:
        p = plan(t)
        ks, leftover = keyscale_for(t["key"])
        note = f"  (node key: {ks}" + (f" + '{leftover}' in prompt)" if leftover else ")") if leftover or ks != t["key"] else ""
        print(f"{t['id']:<16}{t['bpm']:>5}{t['key']:<22}{t['seconds']:>6}{str(t['loop']):>6}  "
              f"{p['phrases'] * PHRASE_BARS:>4}  {p['loop_len']:>8.2f}s {p['gen']:>7.1f}s{note}")


def cmd_prompts(spec, args):
    for t in selected(spec, args.only):
        tags, ks, leftover = compose(spec, t)
        print(f"=== {t['id']}  ({t['zh']} / {t['use']})\n  keyscale={ks}  bpm={t['bpm']}\n"
              f"  tags: {tags}\n  neg:  {spec['negative']}\n")


def cmd_seam(spec, args):
    """Write a short clip centred on the wrap point, for auditioning the loop by ear.

    The numbers in `verdict` catch clicks and level steps but cannot hear a bar landing in the
    wrong place, so this renders `secs` either side of the join: what you hear in the middle of
    the clip is exactly what the game will play when the track repeats.
    """
    (OUT / "seam").mkdir(parents=True, exist_ok=True)
    for t in selected(spec, args.only):
        src = OUT / f"{t['id']}.mp3"
        if not src.exists() or not t["loop"]:
            continue
        a = decode(src, SR_OUT)
        n = int(round(args.secs * SR_OUT))
        clip = np.concatenate([a[-n:], a[:n]], axis=0)
        tmp = OUT / "seam" / f".{t['id']}.wav"
        dst = OUT / "seam" / f"{t['id']}_seam.mp3"
        encode_wav(clip, SR_OUT, tmp)
        ffmpeg(["-i", str(tmp), "-ac", "2", "-ar", str(SR_OUT),
                "-c:a", "libmp3lame", "-b:a", MP3_KBPS, str(dst)])
        tmp.unlink(missing_ok=True)
        print(f"  {dst}  ({args.secs:.0f}s + {args.secs:.0f}s, wrap at {args.secs:.0f}s)")


def cmd_post(spec, args):
    log = {}
    for t in selected(spec, args.only):
        raws = sorted(RAW.glob(f"{t['id']}.*"))
        if not raws:
            print(f"  {t['id']}: no raw take in {RAW}, run `run` first", flush=True)
            continue
        try:
            info = post(t, plan(t), raws[0])
            bad = verdict(t, info)
        except Exception as e:  # noqa: BLE001  one unusable take must not abort the batch
            print(f"    ERROR: {e}", flush=True)
            log[t["id"]] = {"id": t["id"], "fails": [str(e)]}
            continue
        info["fails"] = bad
        print(f"    {'OK' if not bad else 'FAIL: ' + '; '.join(bad)}", flush=True)
        log[t["id"]] = info
    merge_log(log)


def cmd_check(spec, args):
    by_id = {t["id"]: t for t in spec["tracks"]}
    for f in args.files:
        path = Path(f)
        tid = path.stem
        t = by_id.get(tid)
        s = seam_report(path, plan(t)) if t and t["loop"] else {}
        m, pr = ebur128(path), probe(path)
        st = (pr.get("streams") or [{}])[0]
        print(f"{path.name}: {st.get('codec_name')} {st.get('sample_rate')}Hz {st.get('channels')}ch "
              f"{int(st.get('bit_rate', 0)) // 1000}kbps {float(pr['format']['duration']):.2f}s "
              f"{int(pr['format']['size']) / 1000:.0f}kB  I={m['I']} TP={m['TP']} LRA={m['LRA']}"
              + (f"  seam {s['seam_level_db']:+.2f}dB jump {s['seam_jump']:.4f}/{s['jump_p999']:.4f} "
                 f"head/tail rms {s['head_rms']:.3f}/{s['tail_rms']:.3f}" if s else ""))


def cmd_family(spec, args):
    """Check the README's family requirement: a boss theme must sound related to its own theme.

    Compares each boss track's average spectrum against its own normal theme, and against every
    OTHER normal theme. If the reference audio did its job, a pair's own similarity should sit
    above that pair's similarity to unrelated tracks; if it is merely average, the pair is not
    actually a family and the prompts are carrying it alone.
    """
    ids = [t["id"] for t in spec["tracks"]]
    pairs = [(i, i[:-5]) for i in ids
             if i.endswith("_boss") and i != "worldboss" and i[:-5] in ids]
    sig = {}
    for tid in {x for pr in pairs for x in pr}:
        f = OUT / f"{tid}.mp3"
        if f.exists():
            v = spectrum(decode(f, SR_OUT))
            sig[tid] = v / (np.linalg.norm(v) + 1e-9)
    bases = [b for _, b in pairs if b in sig]
    print(f"{'boss':<30}{'own':>7}{'others avg':>12}{'rank':>8}  verdict")
    weak = []
    for boss, base in pairs:
        if boss not in sig or base not in sig:
            continue
        own = float(np.dot(sig[boss], sig[base]))
        others = sorted(((float(np.dot(sig[boss], sig[b])), b) for b in bases if b != base),
                        reverse=True)
        avg = sum(v for v, _ in others) / max(len(others), 1)
        rank = 1 + sum(1 for v, _ in others if v > own)
        ok = own > avg
        if not ok:
            weak.append(boss)
        print(f"{boss:<30}{own:>7.3f}{avg:>12.3f}{rank:>5}/{len(others) + 1}  "
              f"{'closer than average' if ok else 'NOT closer than average'}")
    print()
    print("weakest family links:", weak or "none - every boss theme is closer to its own "
          "normal theme than to the average of the others")


def merge_log(new):
    path = OUT / "run_log.json"
    old = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
    old.update(new)
    path.write_text(json.dumps(old, indent=2, ensure_ascii=False), encoding="utf-8")


def cmd_run(spec, args):
    todo = []
    for t in selected(spec, args.only):
        if not args.force and (OUT / f"{t['id']}.mp3").exists():
            print(f"  skip {t['id']} (out/{t['id']}.mp3 exists, --force to redo)")
            continue
        tags, ks, _ = compose(spec, t)
        p = plan(t)
        job = {**t, **p, "tags": tags, "keyscale": ks, "negative": spec["negative"],
               "seed": args.seed if args.seed is not None else seed_for(t["id"], args.reroll)}
        # Family coherence: a boss theme references its own normal theme, as the README asks.
        base = t["id"][:-5] if t["id"].endswith("_boss") else None
        if not args.no_ref and base and (OUT / f"{base}.mp3").exists():
            job["ref"] = stage_reference(OUT / f"{base}.mp3", t["id"], p["gen"])
            job["ref_from"] = base
        elif base and not args.no_ref:
            print(f"  note: {t['id']} has no out/{base}.mp3 to reference, "
                  f"falling back to the shared instrument description in the prompt")
        todo.append(job)
    if not todo:
        print("nothing to do")
        return
    log, failed = {}, []
    for i in range(0, len(todo), args.group):
        batch = todo[i:i + args.group]
        print(f"\n--- batch {i // args.group + 1}: {[j['id'] for j in batch]}", flush=True)
        produced, secs = submit(batch, args.steps, args.cfg)
        for j in batch:
            try:
                info = post(j, j, produced[j["id"]])
            except Exception as e:  # noqa: BLE001  keep the other tracks in the batch
                print(f"    ERROR: {e}", flush=True)
                failed.append(j["id"])
                log[j["id"]] = {"id": j["id"], "seed": j["seed"], "fails": [str(e)]}
                continue
            info.update({"seed": j["seed"], "steps": args.steps, "cfg": args.cfg,
                         "sampler": f"{SAMPLER}/{SCHEDULER}", "render_seconds": round(j["gen"], 2),
                         "batch_seconds": round(secs), "tags": j["tags"],
                         "ref_from": j.get("ref_from")})
            bad = verdict(j, info)
            info["fails"] = bad
            print(f"    {'OK' if not bad else 'FAIL: ' + '; '.join(bad)}", flush=True)
            if bad:
                failed.append(j["id"])
            log[j["id"]] = info
        merge_log(log)
    api("/free", {"unload_models": True, "free_memory": True})
    print(f"\nDONE. {len(todo) - len(failed)}/{len(todo)} passed."
          + (f" Needs attention: {failed}" if failed else ""), flush=True)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list")
    for name in ("prompts", "post"):
        s = sub.add_parser(name)
        s.add_argument("--only", nargs="*", default=None)
    r = sub.add_parser("run")
    r.add_argument("--only", nargs="*", default=None)
    r.add_argument("--steps", type=int, default=STEPS)
    r.add_argument("--cfg", type=float, default=CFG)
    r.add_argument("--seed", type=int, default=None)
    r.add_argument("--reroll", type=int, default=0, help="bump for a different take of the same ids")
    r.add_argument("--group", type=int, default=4, help="tracks per ComfyUI prompt")
    r.add_argument("--force", action="store_true")
    r.add_argument("--no-ref", action="store_true",
                   help="do not use a boss track's normal theme as an audio reference")
    c = sub.add_parser("check")
    c.add_argument("files", nargs="+")
    sub.add_parser("family")
    s = sub.add_parser("seam")
    s.add_argument("--only", nargs="*", default=None)
    s.add_argument("--secs", type=float, default=8.0)
    args = ap.parse_args()
    spec = load_spec()
    {"list": cmd_list, "prompts": cmd_prompts, "run": cmd_run, "post": cmd_post,
     "check": cmd_check, "seam": cmd_seam, "family": cmd_family}[args.cmd](spec, args)


if __name__ == "__main__":
    main()
