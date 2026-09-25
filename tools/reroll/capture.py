"""截取模板工具:從模擬器截圖,用滑鼠框選要辨識的按鈕/角色,存成 png。

  python capture.py tos btn_skip            # 截圖 → 框選 → 存成 templates/tos/btn_skip.png
  python capture.py tos cards/atem          # 角色模板放 cards/ 子資料夾
  python capture.py tos digits/stone/7      # 魔法石數字 7(0~9 各截一次)
  python capture.py tos --region            # 只印出框選區域座標(填 yaml 的 region 用)
  python capture.py tos --test btn_skip     # 測試模板在目前畫面的比對分數
  python capture.py tos --shot              # 只存整張截圖到 output/tos/shots/

框選視窗:拖曳框出範圍後按 Enter 或空白鍵確認,按 c 取消。
"""

from __future__ import annotations

import argparse
from datetime import datetime
from pathlib import Path

import cv2

from reroll import vision
from reroll.adb import Device
from reroll.ldplayer import LDConsole

ROOT = Path(__file__).parent


def select(img, title="拖曳框選,Enter 確認"):
    # 螢幕太小時縮放顯示,座標再換算回原圖
    scale = min(1.0, 900 / img.shape[0])
    view = cv2.resize(img, None, fx=scale, fy=scale) if scale < 1 else img
    x, y, w, h = cv2.selectROI(title, view, showCrosshair=True)
    cv2.destroyAllWindows()
    if w == 0 or h == 0:
        return None
    return [round(v / scale) for v in (x, y, w, h)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("game", help="templates 底下的資料夾名稱,例如 tos")
    ap.add_argument("name", nargs="?")
    ap.add_argument("--instance", type=int, default=0)
    ap.add_argument("--adb", default=r"C:\LDPlayer\LDPlayer9\adb.exe")
    ap.add_argument("--region", action="store_true")
    ap.add_argument("--test")
    ap.add_argument("--shot", action="store_true")
    a = ap.parse_args()

    adb = a.adb if Path(a.adb).exists() else "adb"
    dev = Device(LDConsole.serial(a.instance), adb=adb)
    img = dev.screenshot()
    print(f"截圖大小 {img.shape[1]}x{img.shape[0]}")
    tdir = ROOT / "templates" / a.game

    if a.shot:
        p = ROOT / "output" / a.game / "shots" / f"{datetime.now():%Y%m%d-%H%M%S}.png"
        vision.imwrite(p, img)
        print("已存", p)
        return
    if a.test:
        tpl = vision.Templates(tdir).get(a.test)
        res = cv2.matchTemplate(img, tpl, cv2.TM_CCOEFF_NORMED)
        _, score, _, loc = cv2.minMaxLoc(res)
        print(f"{a.test}: 最高分數 {score:.3f},位置 {loc}(預設門檻 0.85)")
        return
    r = select(img)
    if not r:
        print("取消")
        return
    x, y, w, h = r
    if a.region or not a.name:
        print(f"region: [{x}, {y}, {w}, {h}]")
        return
    p = tdir / f"{a.name}.png"
    vision.imwrite(p, img[y:y + h, x:x + w])
    print(f"已存 {p}  region: [{x}, {y}, {w}, {h}]")


if __name__ == "__main__":
    main()
