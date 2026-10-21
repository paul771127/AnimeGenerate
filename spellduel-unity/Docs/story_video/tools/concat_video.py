"""Concatenate a story video's shots in shots.json order into out/<video_id>.mp4 at 960x544.

Usage:  python -I concat_video.py <shots.json> <out_dir> <video_id> [<video_id> ...]
Per shot the first existing file wins: <id>_mm.mp4, <id>_mm_full.mp4, <id>_ltx.mp4.
"""
import json
import subprocess
import sys
from pathlib import Path

FFMPEG = r"C:\ffmpeg\ffmpeg-2024-05-23-git-ece95dc3dc-full_build\bin\ffmpeg.exe"
W, H, FPS = 960, 544, 24


def main():
    shots_path, out_dir, *video_ids = sys.argv[1:]
    spec = json.loads(Path(shots_path).read_text(encoding="utf-8"))
    out_dir = Path(out_dir)
    for vid in video_ids:
        video = next(v for v in spec["videos"] if v["id"] == vid)
        files = []
        for s in video["shots"]:
            pick = next((out_dir / f"{s['id']}{suf}.mp4" for suf in ("_mm", "_mm_full", "_ltx")
                         if (out_dir / f"{s['id']}{suf}.mp4").exists()), None)
            if pick is None:
                raise SystemExit(f"{vid}: missing clip for {s['id']}")
            files.append(pick)
            print(f"  {s['id']} <- {pick.name}")
        args = [FFMPEG, "-y", "-v", "error"]
        for f in files:
            args += ["-i", str(f)]
        fc = ""
        for i in range(len(files)):
            fc += (f"[{i}:v]scale={W}:{H}:force_original_aspect_ratio=decrease,pad={W}:{H}:(ow-iw)/2:(oh-ih)/2,"
                   f"fps={FPS},setsar=1,format=yuv420p[v{i}];[{i}:a]aresample=48000,aformat=channel_layouts=stereo[a{i}];")
        fc += "".join(f"[v{i}][a{i}]" for i in range(len(files))) + f"concat=n={len(files)}:v=1:a=1[v][a]"
        dest = out_dir / f"{vid}.mp4"
        args += ["-filter_complex", fc, "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-crf", "18",
                 "-preset", "medium", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", str(dest)]
        subprocess.run(args, check=True)
        print(f"{vid} -> {dest} ({dest.stat().st_size / 2**20:.1f} MB, {len(files)} shots)")


if __name__ == "__main__":
    main()
