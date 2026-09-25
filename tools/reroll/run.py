"""刷號主程式(Windows + 雷電模擬器)。

  python run.py games/tos.yaml --check                  # 檢查還缺哪些模板
  python run.py games/tos.yaml --instances 0            # 用雷電 #0 跑
  python run.py games/tos.yaml --instances 0 1 2 3      # 4 開
  python run.py games/tos.yaml --cycles 5 --debug       # 只跑 5 輪、印出每條規則
  python run.py games/tos.yaml --stats                  # 看出貨率、每輪時間、預估時薪

雷電路徑預設 C:\\LDPlayer\\LDPlayer9,可用 --ld 指定。
"""

from __future__ import annotations

import argparse
import csv
import logging
import sys
import threading
from datetime import datetime
from pathlib import Path

import yaml

from reroll.adb import Device
from reroll.flow import Game, Runner
from reroll.ldplayer import LDConsole

ROOT = Path(__file__).parent
log = logging.getLogger("reroll")
lock = threading.Lock()


def records_path(game: Game) -> Path:
    return ROOT / "output" / game.cfg["templates"] / "records.csv"


def write_record(path: Path, inst: str, r):
    path.parent.mkdir(parents=True, exist_ok=True)
    new = not path.exists()
    with lock, open(path, "a", newline="", encoding="utf-8-sig") as f:
        w = csv.writer(f)
        if new:
            w.writerow(["time", "instance", "status", "seconds", "numbers", "cards",
                        "card_value", "price", "error", "snap_dir"])
        w.writerow([f"{datetime.now():%Y-%m-%d %H:%M:%S}", inst, r.status, r.seconds,
                    yaml.safe_dump(r.numbers, allow_unicode=True, default_flow_style=True).strip(),
                    yaml.safe_dump(r.cards, allow_unicode=True, default_flow_style=True).strip(),
                    r.card_value, r.price if r.price is not None else "", r.error, r.snap_dir])


def worker(game: Game, index: int, a, ld: LDConsole | None, stop: threading.Event):
    serial = LDConsole.serial(index)
    if ld:
        ld.launch(index)
    dev = Device(serial, adb=a.adb)
    runner = Runner(game, dev, ROOT / "output" / game.cfg["templates"], name=f"#{index}")
    on_keep = game.cfg.get("on_keep", "continue")
    n = 0
    while not stop.is_set() and (a.cycles == 0 or n < a.cycles):
        n += 1
        try:
            r = runner.cycle()
        except Exception as e:  # adb 斷線、模擬器當掉
            log.exception("[#%d] 第 %d 輪發生錯誤", index, n)
            from reroll.flow import CycleResult
            r = CycleResult(status="fail", error=repr(e))
        write_record(records_path(game), f"#{index}", r)
        log.info("[#%d] 第 %d 輪 %s  %.0f 秒  %s %s", index, n, r.status, r.seconds, r.numbers, r.cards)
        if r.status == "keep":
            if on_keep == "pause":
                log.warning("[#%d] 抽到好號,這台暫停,資料保留在模擬器裡。處理完再重新啟動。", index)
                return
            if on_keep == "backup" and ld:
                f = ROOT / "output" / game.cfg["templates"] / "backups" / f"{datetime.now():%Y%m%d-%H%M%S}-{index}.ldbk"
                log.info("[#%d] 備份到 %s", index, f)
                ld.quit(index)
                ld.backup(index, f)
                ld.launch(index)
    runner.stop = True


def stats(game: Game):
    p = records_path(game)
    if not p.exists():
        print("還沒有紀錄")
        return
    rows = list(csv.DictReader(open(p, encoding="utf-8-sig")))
    done = [r for r in rows if r["status"] in ("keep", "drop")]
    keep = [r for r in rows if r["status"] == "keep"]
    fail = [r for r in rows if r["status"] == "fail"]
    secs = [float(r["seconds"]) for r in rows]
    hours = sum(secs) / 3600
    value = sum(float(r["price"]) for r in keep if r["price"])
    print(f"總輪數 {len(rows)}(完成 {len(done)}、失敗 {len(fail)})")
    if done:
        print(f"出貨率 {len(keep)}/{len(done)} = {len(keep) / len(done):.1%}")
    if secs:
        print(f"平均每輪 {sum(secs) / len(secs):.0f} 秒;累計機器時間 {hours:.1f} 小時")
    if keep:
        print(f"好號 {len(keep)} 個,預估價值 {value:.0f} 元")
    if hours:
        print(f"每台模擬器每小時預估 {value / hours:.0f} 元(未扣 8591 手續費)")
    if fail:
        print("最近失敗原因:")
        for r in fail[-5:]:
            print(f"  {r['time']} {r['instance']} {r['error'][:80]}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("config")
    ap.add_argument("--instances", type=int, nargs="+", default=[0])
    ap.add_argument("--cycles", type=int, default=0, help="每台跑幾輪,0 = 不停")
    ap.add_argument("--ld", default=r"C:\LDPlayer\LDPlayer9", help="雷電安裝目錄")
    ap.add_argument("--no-launch", action="store_true", help="不自動開模擬器(已手動開好)")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--stats", action="store_true")
    ap.add_argument("--debug", action="store_true")
    a = ap.parse_args()

    logging.basicConfig(level=logging.DEBUG if a.debug else logging.INFO,
                        format="%(asctime)s %(message)s", datefmt="%H:%M:%S")
    cfg = yaml.safe_load(open(a.config, encoding="utf-8"))
    game = Game(cfg, ROOT)

    if a.stats:
        return stats(game)
    missing = game.check_templates()
    if a.check or missing:
        if missing:
            print("還缺這些模板(用 capture.py 截取):")
            for m in missing:
                print("  ", m)
            return 1
        print("模板齊全")
        return 0

    ld_dir = Path(a.ld)
    a.adb = str(ld_dir / "adb.exe") if (ld_dir / "adb.exe").exists() else "adb"
    ld = None
    if not a.no_launch and (ld_dir / "ldconsole.exe").exists():
        ld = LDConsole(str(ld_dir / "ldconsole.exe"))

    stop = threading.Event()
    threads = [threading.Thread(target=worker, args=(game, i, a, ld, stop), daemon=True)
               for i in a.instances]
    for t in threads:
        t.start()
    try:
        for t in threads:
            while t.is_alive():
                t.join(1)
    except KeyboardInterrupt:
        print("已停止")
        stop.set()
    stats(game)


if __name__ == "__main__":
    sys.exit(main())
