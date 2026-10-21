"""Wait for a ComfyUI prompt to finish and copy its SaveVideo outputs to <out_dir>/<shot_id><suffix>.mp4.

Usage:  python -I collect_prompt.py <prompt_id> <out_dir> <suffix>
Filename prefixes are spellduel/<shot_id><suffix>_<hex>, so the shot id is recovered from the file name.
"""
import json
import re
import shutil
import sys
import time
import urllib.request
from pathlib import Path

COMFY = "http://127.0.0.1:8188"
COMFY_OUTPUT = Path(r"D:\ComfyUI_windows_portable\ComfyUI\output")


def api(path, data=None):
    req = urllib.request.Request(COMFY + path, data=None if data is None else json.dumps(data).encode(),
                                 headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        body = r.read()
        return json.loads(body) if body else None


def main():
    pid, out_dir, suffix = sys.argv[1:4]
    out_dir = Path(out_dir)
    t0 = time.time()
    while True:
        hist = api(f"/history/{pid}")
        if pid in hist:
            break
        time.sleep(20)
    entry = hist[pid]
    status = entry.get("status", {})
    if status.get("status_str") != "success":
        msgs = [m for m in status.get("messages", []) if m[0] == "execution_error"]
        raise RuntimeError(f"prompt {pid} failed: {json.dumps(msgs, ensure_ascii=False)[:3000]}")
    pat = re.compile(rf"^(v\d_s\d\d){re.escape(suffix)}_[0-9a-f]{{6}}")
    for node_id, outs in entry["outputs"].items():
        for item in outs.get("images", []) + outs.get("videos", []):
            name = str(item.get("filename", ""))
            m = pat.match(name)
            if not name.endswith(".mp4") or not m:
                continue
            src = COMFY_OUTPUT / item.get("subfolder", "") / name
            dest = out_dir / f"{m.group(1)}{suffix}.mp4"
            shutil.copyfile(src, dest)
            print(f"  {m.group(1)} -> {dest}  ({dest.stat().st_size / 2**20:.1f} MB)", flush=True)
    print(f"  collected after {time.time() - t0:.0f}s of waiting\nALL DONE", flush=True)


if __name__ == "__main__":
    main()
