"""把一句動作描述拆成依序的關鍵姿勢,每個姿勢之後會畫成一格。

來源優先順序:
  1. 使用者自己列出的姿勢(每行一個,或用 → / -> / ; 分隔)
  2. 內建動作範本(揮手、跳、點頭...),中英文都有
  3. 本機 Ollama 拆解(任意動作)
  4. 通用拆法:準備 → 動作進行中 → 動作完成
"""
from __future__ import annotations

import json
import logging
import re
from dataclasses import dataclass

log = logging.getLogger(__name__)

_SPLIT_RE = re.compile(r"\s*(?:\n|→|->|=>|;|;)\s*")


@dataclass(frozen=True)
class Pose:
    zh: str
    en: str

    def text(self, language: str) -> str:
        return self.en if language == "en" else self.zh


@dataclass(frozen=True)
class Template:
    keys: tuple[str, ...]
    poses: tuple[Pose, ...]


def _p(zh: str, en: str) -> Pose:
    return Pose(zh, en)


# 範本的第一格通常會用角色原圖(include_original),所以從「開始動作」的姿勢寫起;
# 最後一格接近原本的站姿,循環播放時比較順。
TEMPLATES: dict[str, Template] = {
    "wave": Template(
        ("揮手", "招手", "打招呼", "揮揮手", "bye", "wave", "waving", "hello", "greet"),
        (
            _p("右手舉到肩膀高度,手掌張開朝前,微笑", "raising the right hand to shoulder height, open palm facing forward, smiling"),
            _p("右手高舉過頭,手掌向左傾斜揮動,微笑", "right hand raised high above the head, palm tilted to the left mid-wave, smiling"),
            _p("右手高舉過頭,手掌向右傾斜揮動,開心地笑", "right hand raised high above the head, palm tilted to the right mid-wave, happy smile"),
            _p("右手高舉過頭,手掌向左傾斜揮動,開心地笑", "right hand raised high above the head, palm tilted to the left mid-wave, happy smile"),
            _p("右手慢慢放下到胸前,微笑", "lowering the right hand to chest height, smiling"),
        ),
    ),
    "jump": Template(
        ("跳", "蹦", "跳躍", "跳起", "jump", "hop", "leap"),
        (
            _p("膝蓋彎曲蹲低準備起跳,雙臂向後擺", "crouching with bent knees ready to jump, arms swung back"),
            _p("雙腳離地往上跳,雙臂向上伸直,身體伸展", "jumping up with both feet off the ground, arms stretched upward, body extended"),
            _p("在空中最高點,雙腳收起,開心的表情", "at the peak of the jump in mid-air, legs tucked, joyful expression"),
            _p("落地,膝蓋微彎緩衝,雙臂張開保持平衡", "landing on the ground with knees slightly bent, arms out for balance"),
        ),
    ),
    "nod": Template(
        ("點頭", "nod"),
        (
            _p("頭微微往下點,眼睛微閉,微笑", "head tilted slightly down in a nod, eyes half closed, smiling"),
            _p("頭往下點得更深,下巴靠近胸口", "head nodding further down, chin close to the chest"),
            _p("頭抬回正面,微笑看向前方", "head back up facing forward, smiling"),
        ),
    ),
    "bow": Template(
        ("鞠躬", "敬禮", "彎腰", "bow"),
        (
            _p("雙手放在身前,上半身開始向前彎", "hands together in front, upper body starting to bend forward"),
            _p("上半身向前彎腰 45 度鞠躬,低頭", "bowing forward 45 degrees at the waist, head lowered"),
            _p("上半身慢慢抬起,微笑", "straightening back up from the bow, smiling"),
        ),
    ),
    "walk": Template(
        ("走", "散步", "步行", "walk", "walking", "stroll"),
        (
            _p("走路中,左腳向前踏出,右手向前擺", "walking, left foot stepping forward, right arm swinging forward"),
            _p("走路中,雙腳交會,身體直立", "walking, legs passing each other, body upright"),
            _p("走路中,右腳向前踏出,左手向前擺", "walking, right foot stepping forward, left arm swinging forward"),
            _p("走路中,雙腳交會,身體直立", "walking, legs passing each other, body upright"),
        ),
    ),
    "run": Template(
        ("跑", "奔跑", "衝", "run", "running", "sprint", "dash"),
        (
            _p("奔跑中,左腳大步向前,右手向前擺,身體前傾", "running, left leg striding forward, right arm forward, body leaning forward"),
            _p("奔跑中,雙腳離地騰空,身體前傾", "running, both feet off the ground mid-stride, leaning forward"),
            _p("奔跑中,右腳大步向前,左手向前擺,身體前傾", "running, right leg striding forward, left arm forward, body leaning forward"),
            _p("奔跑中,雙腳離地騰空,身體前傾", "running, both feet off the ground mid-stride, leaning forward"),
        ),
    ),
    "spin": Template(
        ("轉圈", "轉一圈", "轉個圈", "旋轉", "轉身", "spin", "turn around", "twirl", "rotate"),
        (
            _p("身體轉向左側,側面朝向觀眾", "body turned to the left, side profile facing the viewer"),
            _p("背對觀眾,看得到背面", "turned around with the back facing the viewer"),
            _p("身體轉向右側,側面朝向觀眾", "body turned to the right, side profile facing the viewer"),
            _p("轉回正面,微笑,頭髮和衣服因旋轉飄起", "facing forward again, smiling, hair and clothes flowing from the spin"),
        ),
    ),
    "dance": Template(
        ("跳舞", "舞", "dance", "dancing"),
        (
            _p("跳舞,雙手舉高,身體向左扭", "dancing, both arms raised, hips swaying to the left"),
            _p("跳舞,右手指向天空,左手叉腰,右腳抬起", "dancing, right hand pointing to the sky, left hand on hip, right foot lifted"),
            _p("跳舞,雙手舉高,身體向右扭", "dancing, both arms raised, hips swaying to the right"),
            _p("跳舞,左手指向天空,右手叉腰,左腳抬起", "dancing, left hand pointing to the sky, right hand on hip, left foot lifted"),
        ),
    ),
    "cheer": Template(
        ("歡呼", "萬歲", "勝利", "cheer", "celebrate", "hooray"),
        (
            _p("雙手握拳舉到胸前,興奮的表情", "both fists raised to the chest, excited expression"),
            _p("雙手高舉過頭歡呼,張嘴大笑", "both arms raised high above the head cheering, mouth open laughing"),
            _p("雙手高舉握拳,稍微跳起,大笑", "both fists raised high, hopping slightly, laughing"),
        ),
    ),
    "clap": Template(
        ("拍手", "鼓掌", "clap", "applause"),
        (
            _p("雙手在胸前張開,準備拍手,微笑", "hands apart in front of the chest about to clap, smiling"),
            _p("雙手在胸前合起拍手,開心的表情", "hands clapped together in front of the chest, happy expression"),
        ),
    ),
    "punch": Template(
        ("出拳", "揍", "攻擊", "打拳", "punch", "attack", "strike"),
        (
            _p("擺出戰鬥姿勢,雙拳舉在臉前,眼神專注", "fighting stance, both fists raised in front of the face, focused eyes"),
            _p("右手往後拉蓄力,身體扭轉", "pulling the right fist back to wind up, torso twisted"),
            _p("右拳用力向前打出,手臂伸直,表情兇狠", "throwing a powerful right punch forward with the arm fully extended, fierce expression"),
            _p("收回右拳,回到戰鬥姿勢", "pulling the fist back, returning to fighting stance"),
        ),
    ),
    "kick": Template(
        ("踢", "kick"),
        (
            _p("擺出戰鬥姿勢,重心放在左腳", "fighting stance, weight on the left leg"),
            _p("右膝抬高準備踢", "right knee raised high preparing to kick"),
            _p("右腳用力向前踢出,腿伸直", "right leg kicking forward fully extended"),
            _p("右腳收回,回到戰鬥姿勢", "right leg retracted, back to fighting stance"),
        ),
    ),
}


def match_template(action: str) -> str | None:
    text = (action or "").lower()
    best, best_rank = None, None
    for name, tpl in TEMPLATES.items():
        for key in tpl.keys:
            pos = text.find(key)
            # 描述裡最先出現的動作優先;同位置時取較長的關鍵字(「跳舞」優先於「跳」)
            rank = (pos, -len(key))
            if pos >= 0 and (best_rank is None or rank < best_rank):
                best, best_rank = name, rank
    return best


def split_manual(action: str) -> list[str] | None:
    """使用者自己列出姿勢時(至少兩段),回傳姿勢列表。"""
    parts = [p.strip(" ,。.、") for p in _SPLIT_RE.split(action or "")]
    parts = [p for p in parts if p]
    return parts if len(parts) >= 2 else None


def generic_poses(action: str, count: int) -> list[Pose]:
    """沒有範本也沒有 Ollama 時的通用拆法。"""
    count = max(2, count)
    stages = [_p(f"準備開始{action}的姿勢", f"getting ready to {action}, beginning of the motion")]
    for i in range(1, count - 1):
        pct = round(100 * i / (count - 1))
        stages.append(_p(f"正在{action},動作進行到約 {pct}%", f"in the middle of {action}, about {pct}% through the motion"))
    stages.append(_p(f"{action}的動作完成", f"finishing {action}, end of the motion"))
    return stages


OLLAMA_SYSTEM = (
    "You split a character action into sequential key poses for a short looping 2D animation. "
    "Each pose must be a single still frame that an artist could draw: describe body, arms, legs, head direction "
    "and facial expression. Do not mention the camera, background or other characters. "
    "Reply with ONLY a JSON array of objects with keys \"zh\" (Traditional Chinese) and \"en\" (English)."
)


def ollama_poses(action: str, count: int, host: str, model: str, timeout: int = 120) -> list[Pose] | None:
    try:
        import requests

        r = requests.post(
            f"{host.rstrip('/')}/api/generate",
            json={
                "model": model,
                "system": OLLAMA_SYSTEM,
                "prompt": f"Action: {action}\nNumber of key poses: {count}",
                "stream": False,
                "format": "json",
                "options": {"temperature": 0.3},
            },
            timeout=timeout,
        )
        r.raise_for_status()
        data = json.loads(r.json().get("response", ""))
        if isinstance(data, dict):  # 有些模型會包一層 {"poses": [...]}
            data = next((v for v in data.values() if isinstance(v, list)), [])
        poses = [Pose(str(d.get("zh") or d.get("en")), str(d.get("en") or d.get("zh")))
                 for d in data if isinstance(d, dict) and (d.get("zh") or d.get("en"))]
        return poses or None
    except Exception as exc:  # noqa: BLE001
        log.warning("Ollama 拆解動作失敗(%s),改用通用拆法", exc)
        return None


def plan_poses(action: str, count: int = 4, ollama: dict | None = None) -> tuple[list[Pose], str]:
    """回傳 (姿勢列表, 來源說明)。ollama 給定時(host/model)才會呼叫 Ollama。"""
    action = (action or "").strip()
    if not action:
        raise ValueError("請輸入要角色做的動作,例如「揮手打招呼」,或每行寫一個姿勢")
    manual = split_manual(action)
    if manual:
        return [Pose(p, p) for p in manual], "自訂姿勢"
    name = match_template(action)
    if name:
        return list(TEMPLATES[name].poses), f"內建範本:{name}"
    if ollama:
        poses = ollama_poses(action, count, ollama.get("host", "http://127.0.0.1:11434"),
                             ollama.get("model", "qwen2.5:7b"), ollama.get("timeout", 120))
        if poses:
            return poses, "Ollama 拆解"
    return generic_poses(action, count), "通用拆法(建議自己每行寫一個姿勢,效果會好很多)"
