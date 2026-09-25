"""刷號流程引擎:依 games/<遊戲>.yaml 的規則,一輪一輪地開新帳號、抽卡、判斷要不要留。

每個 phase(階段)是一組「看到什麼 → 做什麼」的規則,不寫死步驟順序,
所以遊戲跳出公告、動畫長短不同、網路延遲都不會卡住。設定格式見 games/tos.yaml。
"""

from __future__ import annotations

import hashlib
import logging
import random
import time
from dataclasses import dataclass, field
from datetime import datetime
from pathlib import Path

import cv2
import numpy as np

from . import vision
from .adb import Device

log = logging.getLogger("reroll")

KEY_BACK = 4


class PhaseTimeout(Exception):
    pass


@dataclass
class CycleResult:
    status: str = "fail"          # keep / drop / fail
    seconds: float = 0
    numbers: dict = field(default_factory=dict)
    cards: dict = field(default_factory=dict)
    card_value: int = 0
    price: float | None = None
    error: str = ""
    snap_dir: str = ""


class Game:
    def __init__(self, cfg: dict, root: Path):
        self.cfg = cfg
        self.name = cfg["game"]
        self.package = cfg["package"]
        self.threshold = cfg.get("threshold", 0.85)
        self.tpl = vision.Templates(root / "templates" / cfg["templates"])
        ev = cfg.get("evaluate", {})
        self.cards = ev.get("cards", {})
        self.numbers = ev.get("numbers", {})
        self.keep_if = ev.get("keep_if", "False")
        self.price_expr = ev.get("price")
        self._digits: dict[str, dict] = {}

    def digits(self, name: str) -> dict:
        if name not in self._digits:
            self._digits[name] = vision.load_digits(self.tpl.root / self.numbers[name]["digits"])
        return self._digits[name]

    def check_templates(self) -> list[str]:
        """列出設定裡用到、但還沒截取的模板。"""
        names = set()
        scans = False
        for ph in self.cfg["phases"]:
            for key in ("until",):
                names.update(_as_list(ph.get(key)))
            for r in ph.get("rules", []):
                names.update(_as_list(r.get("when")))
                names.update(_as_list(r.get("when_not")))
                for a in _actions(r):
                    if "tap" in a:
                        names.add(a["tap"])
                    scans = scans or bool(a.get("scan_cards"))
        if scans:  # 沒有抽卡階段就不需要角色模板
            names.update(c["template"] for c in self.cards.values())
        missing = sorted(n for n in names if n and not self.tpl.exists(n))
        for n, spec in self.numbers.items():
            d = self.tpl.root / spec["digits"]
            if not all((d / f"{i}.png").exists() for i in range(10)):
                missing.append(f"{spec['digits']}/0~9.png")
        return missing


def _as_list(v) -> list:
    if v is None:
        return []
    return v if isinstance(v, list) else [v]


def _actions(rule: dict) -> list[dict]:
    """規則可以寫 do: [...],也可以直接把單一動作寫在規則上。"""
    if "do" in rule:
        return rule["do"]
    keys = ("tap", "tap_at", "swipe", "back", "wait", "text", "scan_cards", "read_number", "snap", "done")
    return [{k: rule[k]} for k in keys if k in rule]


class Runner:
    def __init__(self, game: Game, dev: Device, out_dir: Path, name: str = ""):
        self.g = game
        self.dev = dev
        self.out_dir = out_dir
        self.name = name or dev.serial
        self.stop = False

    # ---------- 單一輪 ----------

    def cycle(self) -> CycleResult:
        r = CycleResult()
        t0 = time.time()
        self._numbers: dict[str, int] = {}
        self._cards: dict[str, int] = {}
        self._last_scan = None
        self._snap_dir = self.out_dir / "snaps" / f"{datetime.now():%Y%m%d-%H%M%S}-{self.name}"
        try:
            if self.g.cfg.get("reset", "clear_data") == "clear_data":
                self.dev.clear_app(self.g.package)
            self.dev.start_app(self.g.package)
            for ph in self.g.cfg["phases"]:
                if ph.get("on_keep"):
                    continue
                self.run_phase(ph)
            self._evaluate(r)
            if r.status == "keep":
                for ph in self.g.cfg["phases"]:
                    if ph.get("on_keep"):
                        self.run_phase(ph)
                self._snap("keep")
        except PhaseTimeout as e:
            r.status, r.error = "fail", str(e)
            self._snap("timeout")
        r.seconds = round(time.time() - t0, 1)
        r.numbers, r.cards = dict(self._numbers), dict(self._cards)
        if self._snap_dir.exists():
            r.snap_dir = str(self._snap_dir)
        return r

    def _evaluate(self, r: CycleResult):
        env = {**{n: 0 for n in self.g.numbers}, **self._numbers}
        env.update({k: self._cards.get(k, 0) for k in self.g.cards})
        r.card_value = sum(self.g.cards[k].get("value", 0) * n for k, n in self._cards.items())
        env["card_value"] = r.card_value
        safe = {"__builtins__": {}, "min": min, "max": max}
        keep = bool(eval(self.g.keep_if, safe, env))
        if self.g.price_expr:
            r.price = float(eval(self.g.price_expr, safe, env))
        r.status = "keep" if keep else "drop"

    # ---------- 階段 ----------

    def run_phase(self, ph: dict):
        name = ph["name"]
        timeout = ph.get("timeout", 600)
        stuck_after = ph.get("stuck_after", 20)
        until = _as_list(ph.get("until"))
        until_hits = ph.get("until_hits")  # {rule: 名稱, count: N}
        hits: dict[str, int] = {}
        start = last_progress = time.time()
        log.info("[%s] 階段 %s 開始", self.name, name)
        while not self.stop:
            now = time.time()
            if now - start > timeout:
                raise PhaseTimeout(f"階段 {name} 超過 {timeout} 秒")
            screen = self.dev.screenshot()
            if until and any(self._find(screen, t) for t in until):
                log.info("[%s] 階段 %s 完成(%.0f 秒)", self.name, name, now - start)
                return
            if until_hits and hits.get(until_hits["rule"], 0) >= until_hits["count"]:
                return
            fired = False
            for i, rule in enumerate(ph.get("rules", [])):
                rid = rule.get("name", str(i))
                if rule.get("max_hits") and hits.get(rid, 0) >= rule["max_hits"]:
                    continue
                if not self._matches(screen, rule):
                    continue
                hits[rid] = hits.get(rid, 0) + 1
                log.debug("[%s] %s: 規則 %s", self.name, name, rid)
                if self._do(screen, rule) == "done":
                    return
                fired = True
                last_progress = time.time()
                break
            if not fired and time.time() - last_progress > stuck_after:
                log.info("[%s] %s 卡住 %d 秒,執行 stuck 動作", self.name, name, stuck_after)
                self._do(screen, ph.get("stuck", {"back": True}))
                last_progress = time.time()
            self.dev.human_pause(*ph.get("poll", (0.5, 1.0)))

    def _find(self, screen, name, threshold=None):
        return vision.find(screen, self.g.tpl.get(name), threshold or self.g.threshold)

    def _matches(self, screen, rule) -> bool:
        th = rule.get("threshold")
        if not all(self._find(screen, t, th) for t in _as_list(rule.get("when"))):
            return False
        if any(self._find(screen, t, th) for t in _as_list(rule.get("when_not"))):
            return False
        return True

    # ---------- 動作 ----------

    def _do(self, screen, rule) -> str | None:
        for a in _actions(rule):
            if "tap" in a:
                m = self._find(screen, a["tap"], rule.get("threshold"))
                if m:
                    self.dev.tap(m.x, m.y)
            elif "tap_at" in a:
                self.dev.tap(*a["tap_at"])
            elif "swipe" in a:
                self.dev.swipe(*a["swipe"])
            elif a.get("back"):
                self.dev.key(KEY_BACK)
            elif "wait" in a:
                w = a["wait"]
                time.sleep(random.uniform(*w) if isinstance(w, list) else w)
                continue
            elif "text" in a:
                self.dev.text(str(a["text"]).replace("{rand}", str(random.randint(1000, 9999))))
            elif a.get("scan_cards"):
                self._scan_cards(screen)
                continue
            elif "read_number" in a:
                self._read_number(screen, a["read_number"])
                continue
            elif "snap" in a:
                self._snap(a["snap"], screen)
                continue
            elif a.get("done"):
                return "done"
            self.dev.human_pause()
        return None

    def _scan_cards(self, screen):
        # 同一張結果畫面連續掃兩次只算一次
        h = hashlib.md5(cv2.resize(screen, (64, 64)).tobytes()).hexdigest()
        if h == self._last_scan:
            return
        self._last_scan = h
        for key, spec in self.g.cards.items():
            n = len(vision.find_all(screen, self.g.tpl.get(spec["template"]),
                                    spec.get("threshold", self.g.threshold), spec.get("region")))
            if n:
                self._cards[key] = self._cards.get(key, 0) + n
                log.info("[%s] 抽到 %s ×%d", self.name, key, n)

    def _read_number(self, screen, name):
        spec = self.g.numbers[name]
        v = vision.read_number(screen, spec["region"], self.g.digits(name), spec.get("threshold", 0.8))
        if v is not None:
            self._numbers[name] = max(v, self._numbers.get(name, 0))
            log.info("[%s] %s = %d", self.name, name, v)

    def _snap(self, label, screen: np.ndarray | None = None):
        img = screen if screen is not None else self.dev.screenshot()
        vision.imwrite(self._snap_dir / f"{datetime.now():%H%M%S}-{label}.png", img)
