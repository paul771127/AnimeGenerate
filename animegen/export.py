"""把生成的影格輸出成 MP4 / GIF / PNG,並提供 ffmpeg 補幀。"""
from __future__ import annotations

import logging
import subprocess
from pathlib import Path
from typing import Sequence

import numpy as np
from PIL import Image

log = logging.getLogger(__name__)

FrameLike = Sequence  # list of PIL.Image / np.ndarray / torch tensor


def to_uint8_frames(frames: FrameLike) -> list[np.ndarray]:
    """把各種格式的影格統一成 uint8 HxWx3 numpy 陣列,並確保寬高為偶數(yuv420p 需要)。"""
    out: list[np.ndarray] = []
    for frame in frames:
        if isinstance(frame, Image.Image):
            arr = np.asarray(frame.convert("RGB"))
        else:
            if hasattr(frame, "detach"):  # torch tensor
                frame = frame.detach().cpu().float().numpy()
            arr = np.asarray(frame)
            if arr.ndim == 3 and arr.shape[0] in (1, 3, 4) and arr.shape[-1] not in (1, 3, 4):
                arr = np.transpose(arr, (1, 2, 0))  # CHW -> HWC
            if arr.dtype != np.uint8:
                arr = arr.astype(np.float32)
                if arr.min() < 0:  # [-1, 1]
                    arr = (arr + 1.0) / 2.0
                if arr.max() <= 1.0:
                    arr = arr * 255.0
                arr = np.clip(arr, 0, 255).round().astype(np.uint8)
            if arr.ndim == 2:
                arr = np.stack([arr] * 3, axis=-1)
            if arr.shape[-1] == 4:
                arr = arr[..., :3]
            elif arr.shape[-1] == 1:
                arr = np.repeat(arr, 3, axis=-1)
        h, w = arr.shape[:2]
        arr = arr[: h - (h % 2), : w - (w % 2)]
        out.append(np.ascontiguousarray(arr))
    if not out:
        raise ValueError("沒有任何影格可以輸出")
    return out


def pingpong(frames: list[np.ndarray]) -> list[np.ndarray]:
    """正放 + 倒放(去掉頭尾重複幀),做成可無縫循環的動畫。"""
    if len(frames) < 3:
        return list(frames)
    return list(frames) + list(frames[-2:0:-1])


def save_mp4(frames: list[np.ndarray], path: str | Path, fps: float) -> Path:
    import imageio

    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with imageio.get_writer(
        str(path),
        fps=fps,
        codec="libx264",
        quality=8,
        pixelformat="yuv420p",
        macro_block_size=1,
        ffmpeg_log_level="error",
    ) as writer:
        for frame in frames:
            writer.append_data(frame)
    return path


def save_gif(frames: list[np.ndarray], path: str | Path, fps: float, max_width: int | None = 512) -> Path:
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    images = [Image.fromarray(f) for f in frames]
    if max_width and images[0].width > max_width:
        ratio = max_width / images[0].width
        size = (max_width, max(1, round(images[0].height * ratio)))
        images = [im.resize(size, Image.LANCZOS) for im in images]
    duration_ms = max(20, round(1000 / fps))
    images[0].save(
        str(path),
        save_all=True,
        append_images=images[1:],
        duration=duration_ms,
        loop=0,
        optimize=False,
        disposal=2,
    )
    return path


def save_frames_png(frames: list[np.ndarray], folder: str | Path) -> Path:
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    for i, frame in enumerate(frames):
        Image.fromarray(frame).save(folder / f"frame_{i:04d}.png")
    return folder


def ffmpeg_exe() -> str:
    import imageio_ffmpeg

    return imageio_ffmpeg.get_ffmpeg_exe()


def interpolate_video(src: str | Path, dst: str | Path, src_fps: float, factor: int = 2) -> Path:
    """用 ffmpeg 的 minterpolate 濾鏡做運動補幀,fps 變成 src_fps * factor。"""
    if factor < 2:
        raise ValueError("補幀倍率至少要 2")
    src, dst = Path(src), Path(dst)
    target_fps = src_fps * factor
    cmd = [
        ffmpeg_exe(),
        "-y",
        "-loglevel",
        "error",
        "-i",
        str(src),
        "-vf",
        f"minterpolate=fps={target_fps}:mi_mode=mci:mc_mode=aobmc:me_mode=bidir:vsbmc=1",
        "-c:v",
        "libx264",
        "-pix_fmt",
        "yuv420p",
        "-crf",
        "18",
        str(dst),
    ]
    log.info("ffmpeg 補幀: %s → %s (%.0f fps)", src.name, dst.name, target_fps)
    subprocess.run(cmd, check=True)
    return dst


def read_video_frames(path: str | Path) -> list[np.ndarray]:
    import imageio

    with imageio.get_reader(str(path), "ffmpeg") as reader:
        return [np.asarray(f) for f in reader]
