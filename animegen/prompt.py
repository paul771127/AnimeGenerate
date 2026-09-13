"""提示詞組合,以及可選的本機 Ollama 提示詞強化。"""
from __future__ import annotations

import logging
import re
from typing import Any

log = logging.getLogger(__name__)

# Wan 官方建議的負面提示詞(中文效果最好)
WAN_NEGATIVE = (
    "色调艳丽,过曝,静态,细节模糊不清,字幕,风格,作品,画作,画面,静止,整体发灰,"
    "最差质量,低质量,JPEG压缩残留,丑陋的,残缺的,多余的手指,画得不好的手部,"
    "画得不好的脸部,畸形的,毁容的,形态畸形的肢体,手指融合,静止不动的画面,"
    "杂乱的背景,三条腿,背景人很多,倒着走"
)

GENERIC_NEGATIVE = (
    "worst quality, low quality, blurry, jittery, distorted, deformed, disfigured, "
    "extra limbs, extra fingers, bad hands, bad anatomy, mutated, text, watermark, "
    "subtitles, static image, frozen, flickering, inconsistent character, "
    "different character, changing clothes, morphing face"
)

_WS_RE = re.compile(r"\s+")


def _clean(text: str | None) -> str:
    text = (text or "").strip()
    text = _WS_RE.sub(" ", text)
    return text.rstrip(".,;,。 ")


def build_prompt(
    action: str,
    character_desc: str = "",
    prompt_cfg: dict[str, Any] | None = None,
    backend_negative: str | None = None,
) -> tuple[str, str]:
    """組合正面 / 負面提示詞。

    回傳 (prompt, negative_prompt)。
    """
    prompt_cfg = prompt_cfg or {}
    action = _clean(action)
    if not action:
        raise ValueError("請輸入要角色做的動作,例如「揮手打招呼」或 'waves and smiles'")

    parts = [_clean(prompt_cfg.get("style_prefix"))]
    character_desc = _clean(character_desc)
    if character_desc:
        parts.append(character_desc)
    parts.append(f"The character {action}")
    parts.append(_clean(prompt_cfg.get("style_suffix")))
    prompt = ". ".join(p for p in parts if p) + "."

    negative_setting = prompt_cfg.get("negative_prompt", "auto")
    if negative_setting in (None, "", "auto"):
        negative = backend_negative or GENERIC_NEGATIVE
    else:
        negative = str(negative_setting)
    return prompt, negative


ENHANCER_SYSTEM_PROMPT = (
    "You are a prompt engineer for an image-to-video diffusion model that animates a single "
    "anime character from a reference picture. Rewrite the user's prompt into ONE detailed "
    "English paragraph (60-120 words) describing: the character's motion step by step in "
    "chronological order, body parts involved, facial expression, and a stable camera. Keep "
    "the character's appearance identical to the reference image and do not invent new "
    "characters, scene changes or camera cuts. Output only the rewritten prompt."
)


class OllamaEnhancer:
    """用本機 Ollama (免費、離線) 把簡短的動作描述改寫成詳細英文提示詞。"""

    def __init__(self, host: str = "http://127.0.0.1:11434", model: str = "qwen2.5:7b", timeout: int = 120):
        self.host = host.rstrip("/")
        self.model = model
        self.timeout = timeout

    def is_available(self) -> bool:
        try:
            import requests

            r = requests.get(f"{self.host}/api/tags", timeout=3)
            return r.ok
        except Exception:  # noqa: BLE001
            return False

    def enhance(self, prompt: str) -> str:
        """失敗時回傳原本的 prompt,不會讓整個流程中斷。"""
        try:
            import requests

            payload = {
                "model": self.model,
                "system": ENHANCER_SYSTEM_PROMPT,
                "prompt": prompt,
                "stream": False,
                "options": {"temperature": 0.4},
            }
            r = requests.post(f"{self.host}/api/generate", json=payload, timeout=self.timeout)
            r.raise_for_status()
            text = _clean(r.json().get("response", ""))
            if len(text) < 20:
                log.warning("Ollama 回傳的內容太短,改用原始提示詞")
                return prompt
            return text
        except Exception as exc:  # noqa: BLE001
            log.warning("Ollama 提示詞強化失敗(%s),改用原始提示詞", exc)
            return prompt
