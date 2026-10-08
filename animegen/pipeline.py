"""主流程:角色圖 + 動作描述 → 影片模型 → GIF / MP4。"""
from __future__ import annotations

import logging
import random
import re
import time
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path
from typing import Any, Callable

import numpy as np
from PIL import Image

from . import export
from .backends import GenerationRequest, VideoBackend, get_backend_class
from .config import load_config
from .device import DeviceInfo, choose_backend, detect_device
from .image import parse_resolution, prepare_image
from .prompt import _CJK_RE, OllamaEnhancer, build_prompt

log = logging.getLogger(__name__)

ProgressFn = Callable[[int, int, str], None]

_SLUG_RE = re.compile(r"[^0-9A-Za-z㐀-鿿]+")


@dataclass
class GenerateResult:
    frames: list[np.ndarray]
    fps: float
    files: dict[str, Path]
    prompt: str
    negative_prompt: str
    backend: str
    seed: int
    width: int
    height: int
    num_frames: int
    elapsed: float
    warnings: list[str] = field(default_factory=list)

    def summary(self) -> str:
        lines = [
            f"後端: {self.backend}  解析度: {self.width}x{self.height}  "
            f"幀數: {self.num_frames} @ {self.fps:g} fps  seed: {self.seed}  耗時: {self.elapsed:.1f}s",
            f"提示詞: {self.prompt}",
        ]
        lines += [f"{fmt.upper()}: {path}" for fmt, path in self.files.items()]
        lines += [f"注意: {w}" for w in self.warnings]
        return "\n".join(lines)


def _slug(text: str, limit: int = 24) -> str:
    return _SLUG_RE.sub("_", text).strip("_")[:limit] or "anim"


class AnimeGenerator:
    """持有設定與已載入的模型;同一個實例重複生成時不會重新載入模型。"""

    def __init__(self, cfg: dict[str, Any] | None = None):
        self.cfg = cfg if cfg is not None else load_config()
        self.device: DeviceInfo = detect_device(self.cfg.get("device", "auto"))
        self._backend: VideoBackend | None = None

    # ---- 後端 -----------------------------------------------------------------
    def resolve_backend_name(self, name: str | None = None) -> tuple[str, str]:
        name = name or self.cfg.get("backend", "auto")
        if name in (None, "", "auto"):
            return choose_backend(self.device)
        get_backend_class(name)  # 名稱錯誤時丟出 ValueError
        return name, "手動指定"

    def get_backend(self, name: str) -> VideoBackend:
        if self._backend is not None and self._backend.spec.name == name:
            return self._backend
        if self._backend is not None:
            log.info("卸載後端 %s", self._backend.spec.name)
            self._backend.unload()
        cls = get_backend_class(name)
        if name != "mock" and not self.device.torch_available:
            raise RuntimeError(
                f"後端 {name} 需要 PyTorch 與 diffusers。請先安裝:pip install -r requirements-gpu.txt"
                "(torch 請依 https://pytorch.org/get-started/locally/ 選擇對應 CUDA 版本)"
            )
        self._backend = cls(
            self.cfg.get("backends", {}).get(name, {}),
            self.device,
            memory_mode=self.cfg.get("memory_mode", "auto"),
            cache_dir=self.cfg.get("model_cache_dir"),
        )
        return self._backend

    def release(self) -> None:
        """卸載模型,釋放顯卡記憶體(切換到另一種模式前呼叫)。"""
        if self._backend is not None:
            self._backend.unload()
            self._backend = None

    # ---- 生成 -----------------------------------------------------------------
    def generate(
        self,
        image: Image.Image | str | Path,
        action: str,
        *,
        character_desc: str = "",
        backend: str | None = None,
        num_frames: int | None = None,
        steps: int | None = None,
        guidance: float | None = None,
        seed: int | None = None,
        resolution: str | None = None,
        fit_mode: str | None = None,
        fps: float | None = None,
        formats: list[str] | None = None,
        pingpong: bool | None = None,
        interpolate: int | None = None,
        enhance: bool | None = None,
        output_dir: str | Path | None = None,
        progress: ProgressFn | None = None,
    ) -> GenerateResult:
        gen_cfg = self.cfg.get("generation", {})
        exp_cfg = self.cfg.get("export", {})
        prompt_cfg = self.cfg.get("prompt", {})
        warnings: list[str] = []
        started = time.time()

        def report(i: int, n: int, msg: str) -> None:
            if progress:
                progress(i, n, msg)

        # 1. 後端
        backend_name, reason = self.resolve_backend_name(backend)
        log.info("後端: %s(%s)", backend_name, reason)
        if backend_name == "mock":
            warnings.append(
                "目前使用 mock 後端:只會做簡單的位移動畫,不會真的照描述做動作。"
                "要讓角色依照文字做動作,需要有 NVIDIA GPU(建議 12GB+ VRAM)並安裝 requirements-gpu.txt。"
            )
        be = self.get_backend(backend_name)
        spec = be.spec

        # 2. 圖片
        if isinstance(image, (str, Path)):
            with Image.open(image) as im:
                image = im.copy()
        if not isinstance(image, Image.Image):
            raise TypeError("image 必須是圖片路徑或 PIL.Image")
        res = parse_resolution(resolution if resolution is not None else gen_cfg.get("resolution"))
        width, height = be.resolve_resolution(image, res)
        prepared = prepare_image(
            image, width, height,
            fit_mode=fit_mode or gen_cfg.get("fit_mode", "crop"),
            background=gen_cfg.get("background", (255, 255, 255)),
        )

        # 3. 提示詞
        prompt, negative = build_prompt(action, character_desc, prompt_cfg, spec.default_negative)
        use_enhancer = enhance if enhance is not None else prompt_cfg.get("enhancer") == "ollama"
        if use_enhancer:
            report(0, 1, "Ollama 強化提示詞")
            o = prompt_cfg.get("ollama", {})
            enhancer = OllamaEnhancer(o.get("host", "http://127.0.0.1:11434"), o.get("model", "qwen2.5:7b"),
                                      o.get("timeout", 120))
            if enhancer.is_available():
                prompt = enhancer.enhance(prompt)
            else:
                warnings.append("連不到 Ollama,改用原始提示詞(請先執行 `ollama serve`)")
        elif _CJK_RE.search(action) and backend_name in ("ltx", "cogvideox"):
            warnings.append(f"{spec.display_name} 只看得懂英文,建議用英文描述動作,或開啟 Ollama 提示詞強化")

        # 4. 生成
        frames_n = spec.resolve_frames(num_frames if num_frames is not None else gen_cfg.get("num_frames"))
        seed_val = seed if seed is not None else gen_cfg.get("seed", -1)
        if seed_val is None or int(seed_val) < 0:
            seed_val = random.randint(0, 2**31 - 1)
        req = GenerationRequest(
            image=prepared,
            prompt=prompt,
            negative_prompt=negative,
            width=width,
            height=height,
            num_frames=frames_n,
            num_inference_steps=be.resolve_steps(steps if steps is not None else gen_cfg.get("num_inference_steps")),
            guidance_scale=be.resolve_guidance(guidance if guidance is not None else gen_cfg.get("guidance_scale")),
            seed=int(seed_val),
            progress=progress,
            extra={"action": action},
        )
        log.info("生成 %dx%d, %d 幀, %d 步, seed=%d", width, height, frames_n, req.num_inference_steps, req.seed)
        if not be.loaded and backend_name != "mock":
            report(0, 1, "載入模型(第一次會下載模型,可能要很久)")
        frames = export.to_uint8_frames(be.generate(req))

        # 5. 輸出
        out_fps = float(fps or exp_cfg.get("fps") or spec.default_fps)
        if pingpong if pingpong is not None else exp_cfg.get("pingpong", False):
            frames = export.pingpong(frames)
        formats = [f.lower() for f in (formats or exp_cfg.get("formats") or ["gif"])]
        out_dir = Path(output_dir or self.cfg.get("output_dir", "outputs"))
        stem = f"{datetime.now():%Y%m%d-%H%M%S}_{_slug(action)}_{req.seed}"
        files: dict[str, Path] = {}

        factor = int(interpolate if interpolate is not None else exp_cfg.get("interpolate", 1))
        if factor >= 2:
            report(0, 1, "ffmpeg 補幀")
            raw = export.save_mp4(frames, out_dir / f"{stem}_raw.mp4", out_fps)
            smooth = export.interpolate_video(raw, out_dir / f"{stem}.mp4", out_fps, factor)
            raw.unlink(missing_ok=True)
            frames = export.read_video_frames(smooth)
            out_fps *= factor
            if "mp4" in formats:
                files["mp4"] = smooth
            else:
                smooth.unlink(missing_ok=True)

        report(0, 1, "輸出檔案")
        if "gif" in formats:
            files["gif"] = export.save_gif(frames, out_dir / f"{stem}.gif", out_fps, exp_cfg.get("gif_max_width", 512))
        if "mp4" in formats and "mp4" not in files:
            files["mp4"] = export.save_mp4(frames, out_dir / f"{stem}.mp4", out_fps)
        if "png" in formats or exp_cfg.get("save_frames"):
            files["png"] = export.save_frames_png(frames, out_dir / f"{stem}_frames")

        result = GenerateResult(
            frames=frames, fps=out_fps, files=files, prompt=prompt, negative_prompt=negative,
            backend=backend_name, seed=req.seed, width=width, height=height,
            num_frames=len(frames), elapsed=time.time() - started, warnings=warnings,
        )
        return result
