"""設定檔載入與預設值。

所有可調參數都集中在 DEFAULT_CONFIG,專案根目錄的 config.yaml 會覆蓋對應欄位。
"""
from __future__ import annotations

import copy
from pathlib import Path
from typing import Any

import yaml

DEFAULT_CONFIG: dict[str, Any] = {
    # auto | wan22 | wan21 | cogvideox | ltx | mock
    "backend": "auto",
    # auto | cuda | mps | cpu
    "device": "auto",
    # auto | full | model_offload | sequential_offload
    "memory_mode": "auto",
    "output_dir": "outputs",
    # None = 使用 Hugging Face 預設快取 (~/.cache/huggingface);也可以指定資料夾
    "model_cache_dir": None,
    "generation": {
        "num_inference_steps": None,  # None = 後端預設
        "guidance_scale": None,
        "num_frames": None,
        "seed": -1,  # -1 = 隨機
        "resolution": "auto",  # 例如 "832x480";auto = 依後端與 VRAM 決定
        "fit_mode": "crop",  # crop | pad | stretch
        "background": [255, 255, 255],  # 透明 PNG 的底色
    },
    "prompt": {
        "style_prefix": (
            "Anime style 2D animation, clean line art, flat cel shading, "
            "the exact same character as in the reference image"
        ),
        "style_suffix": (
            "smooth natural motion, consistent character design, "
            "stable camera, high quality, detailed"
        ),
        # auto = 依後端選擇預設負面提示詞
        "negative_prompt": "auto",
        # none | ollama
        "enhancer": "none",
        "ollama": {
            "host": "http://127.0.0.1:11434",
            "model": "qwen2.5:7b",
            "timeout": 120,
        },
    },
    "export": {
        "fps": None,  # None = 後端預設
        "formats": ["mp4", "gif"],
        "interpolate": 1,  # 1 = 關閉;2 = 用 ffmpeg 補幀成兩倍 fps
        "pingpong": False,  # 正放 + 倒放,做成可循環的動畫
        "gif_max_width": 512,
        "save_frames": False,  # 另存每一幀 PNG
    },
    "backends": {
        "wan22": {"model_id": "Wan-AI/Wan2.2-TI2V-5B-Diffusers"},
        "wan21": {"model_id": "Wan-AI/Wan2.1-I2V-14B-480P-Diffusers"},
        "cogvideox": {"model_id": "THUDM/CogVideoX-5b-I2V"},
        "ltx": {"model_id": "Lightricks/LTX-Video"},
        "mock": {},
    },
}


def deep_merge(base: dict, override: dict) -> dict:
    """遞迴合併 dict,override 的值優先。"""
    result = copy.deepcopy(base)
    for key, value in (override or {}).items():
        if isinstance(value, dict) and isinstance(result.get(key), dict):
            result[key] = deep_merge(result[key], value)
        else:
            result[key] = copy.deepcopy(value)
    return result


def load_config(path: str | Path | None = None) -> dict[str, Any]:
    """載入設定:預設值 + (可選的) YAML 覆蓋。"""
    cfg = copy.deepcopy(DEFAULT_CONFIG)
    candidates = [Path(path)] if path else [Path("config.yaml"), Path("config.yml")]
    for candidate in candidates:
        if candidate.is_file():
            with open(candidate, encoding="utf-8") as f:
                data = yaml.safe_load(f) or {}
            if not isinstance(data, dict):
                raise ValueError(f"設定檔 {candidate} 的頂層必須是對應表 (mapping)")
            cfg = deep_merge(cfg, data)
            cfg["_config_path"] = str(candidate)
            break
    else:
        if path:
            raise FileNotFoundError(f"找不到設定檔: {path}")
    return cfg
