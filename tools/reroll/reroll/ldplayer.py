"""雷電模擬器多開控制(ldconsole.exe)。

ldconsole 在雷電安裝目錄,例如 C:\\LDPlayer\\LDPlayer9\\ldconsole.exe。
第 i 個模擬器的 ADB 序號通常是 emulator-{5554 + 2i}。
"""

from __future__ import annotations

import subprocess
import time
from pathlib import Path


class LDConsole:
    def __init__(self, path: str):
        self.path = path

    def _run(self, *args: str, timeout: float = 120) -> str:
        r = subprocess.run([self.path, *args], capture_output=True, timeout=timeout)
        return r.stdout.decode("utf-8", errors="ignore") or r.stdout.decode("gbk", errors="ignore")

    def list(self) -> list[dict]:
        """每行:index,title,top_hwnd,bind_hwnd,android_started,pid,vbox_pid,..."""
        out = []
        for line in self._run("list2").splitlines():
            f = line.split(",")
            if len(f) >= 5:
                out.append({"index": int(f[0]), "title": f[1], "running": f[4] == "1"})
        return out

    def is_running(self, index: int) -> bool:
        return any(i["index"] == index and i["running"] for i in self.list())

    def launch(self, index: int, wait: float = 60):
        if self.is_running(index):
            return
        self._run("launch", "--index", str(index))
        deadline = time.time() + wait
        while time.time() < deadline:
            if self.is_running(index):
                time.sleep(10)  # Android 開機完成還要一點時間
                return
            time.sleep(2)
        raise TimeoutError(f"雷電 #{index} 啟動逾時")

    def quit(self, index: int):
        self._run("quit", "--index", str(index))

    def backup(self, index: int, file: Path):
        """整台模擬器備份(含遊客帳號資料),檔案可能有數 GB。"""
        file.parent.mkdir(parents=True, exist_ok=True)
        self._run("backup", "--index", str(index), "--file", str(file), timeout=1800)

    @staticmethod
    def serial(index: int) -> str:
        return f"emulator-{5554 + 2 * index}"
