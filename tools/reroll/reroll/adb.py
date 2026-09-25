"""ADB 控制:截圖、點擊、滑動、清除遊戲資料。"""

from __future__ import annotations

import random
import subprocess
import time

import cv2
import numpy as np


class Device:
    def __init__(self, serial: str, adb: str = "adb", jitter: int = 6):
        self.serial = serial
        self.adb = adb
        self.jitter = jitter

    def _run(self, *args: str, timeout: float = 30) -> bytes:
        cmd = [self.adb, "-s", self.serial, *args]
        return subprocess.run(cmd, capture_output=True, timeout=timeout, check=True).stdout

    def shell(self, cmd: str, timeout: float = 30) -> str:
        return self._run("shell", cmd, timeout=timeout).decode(errors="ignore")

    def screenshot(self) -> np.ndarray:
        png = self._run("exec-out", "screencap", "-p")
        img = cv2.imdecode(np.frombuffer(png, np.uint8), cv2.IMREAD_COLOR)
        if img is None:
            raise RuntimeError(f"{self.serial} 截圖失敗")
        return img

    def tap(self, x: int, y: int):
        j = self.jitter
        x, y = x + random.randint(-j, j), y + random.randint(-j, j)
        self.shell(f"input tap {x} {y}")

    def swipe(self, x1: int, y1: int, x2: int, y2: int, ms: int = 400):
        self.shell(f"input swipe {x1} {y1} {x2} {y2} {ms + random.randint(-60, 60)}")

    def key(self, code: int):
        self.shell(f"input keyevent {code}")

    def text(self, s: str):
        self.shell(f"input text {s}")

    def start_app(self, package: str):
        self.shell(f"monkey -p {package} -c android.intent.category.LAUNCHER 1")

    def stop_app(self, package: str):
        self.shell(f"am force-stop {package}")

    def clear_app(self, package: str):
        """清除遊戲資料 = 新的遊客帳號。"""
        self.stop_app(package)
        out = self.shell(f"pm clear {package}")
        if "Success" not in out:
            raise RuntimeError(f"{self.serial} 清除 {package} 失敗:{out.strip()}")

    @staticmethod
    def human_pause(lo: float = 0.3, hi: float = 0.9):
        time.sleep(random.uniform(lo, hi))


def connected_serials(adb: str = "adb") -> list[str]:
    out = subprocess.run([adb, "devices"], capture_output=True, text=True, check=True).stdout
    return [l.split()[0] for l in out.splitlines()[1:] if l.strip().endswith("device")]
