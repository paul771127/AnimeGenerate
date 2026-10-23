#!/usr/bin/env python3
"""檢查副本劇本（Assets/SpellDuel/Resources/dungeons.txt）——和遊戲裡 Dungeons.Parse 同樣的規則。

用法：python3 Tools/check_dungeons.py [檔案]（預設檢查 Resources/dungeons.txt）
沒有錯誤＝exit 0；有錯誤會列出行號，exit 1。量產副本後先跑這個再提交。
"""
import re, sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT = os.path.join(HERE, "..", "Assets", "SpellDuel", "Resources", "dungeons.txt")
STORY = os.path.join(HERE, "..", "Assets", "SpellDuel", "Resources", "story.txt")
THEMES = {"volcano", "熔岩", "0", "forest", "森林", "1", "sea", "海", "海上", "2", "arena", "角鬥場", "3", "canyon", "山道", "黃土山道", "4"}
MAX_LINE = 60

def story_stage_ids():
    ids = set()
    try:
        for l in open(STORY, encoding="utf-8"):
            m = re.match(r"\s*\[stage\s+([^\]]+)\]", l)
            if m: ids.add(m.group(1).strip())
    except OSError:
        pass
    return ids

def check(path):
    errors, dungeons = [], []
    d = room = lines = None
    for ln, raw0 in enumerate(open(path, encoding="utf-8").read().replace("\r", "").split("\n"), 1):
        raw = raw0.strip()
        if not raw or raw.startswith("#") or raw.startswith("//"): continue
        if raw.startswith("["):
            m = re.match(r"\[(\w+)\s*([^\]]*)\]\s*(.*)", raw)
            if not m: errors.append(f"第 {ln} 行：區塊寫錯（缺少 ]）"); continue
            kind, arg, title = m.group(1).lower(), m.group(2).strip(), m.group(3).strip()
            lines = None
            if kind == "dungeon":
                if not arg: errors.append(f"第 {ln} 行：[dungeon] 後面要寫 id"); d = None; continue
                if any(x["id"] == arg for x in dungeons): errors.append(f"第 {ln} 行：副本 id「{arg}」重複了")
                d = {"id": arg, "name": title, "level": 0, "unlock": None, "rooms": [], "before": 0, "after": 0, "line": ln}
                dungeons.append(d); room = None
            elif kind == "room":
                if d is None: errors.append(f"第 {ln} 行：[room] 要放在 [dungeon] 底下"); continue
                room = {"title": title, "wave": 0, "enter": 0, "line": ln}
                d["rooms"].append(room)
            else:
                errors.append(f"第 {ln} 行：不認得的區塊 [{kind}]")
            continue
        if raw.startswith("<"):
            tag = raw.strip("<> ").lower()
            ok = (room is not None and tag in ("enter", "clear", "boss_half")) or (room is None and d is not None and tag in ("before", "after"))
            if not ok: errors.append(f"第 {ln} 行：<{tag}> 放錯地方（副本用 before／after，房間用 enter／clear／boss_half）"); lines = None
            else:
                lines = tag
            continue
        if lines:
            text = re.split(r"[：:]", raw, 1)[-1].strip() if re.match(r"^[^：:]{1,24}[：:]", raw) else raw
            if len(text) > MAX_LINE: errors.append(f"第 {ln} 行：台詞超過 {MAX_LINE} 字（{len(text)}），請拆成兩句")
            if room is not None and lines == "enter": room["enter"] += 1
            elif room is None and lines in ("before", "after"): d[lines] += 1
            continue
        if "=" in raw:
            k, v = [x.strip() for x in raw.split("=", 1)]
            k = k.lower()
            if room is not None:
                if k == "wave":
                    if not v.isdigit() or int(v) < 1: errors.append(f"第 {ln} 行：wave 要是 1 以上的整數")
                    else: room["wave"] = int(v)
                elif k == "theme":
                    if v.lower() not in THEMES: errors.append(f"第 {ln} 行：theme 只能是 volcano／forest／sea／arena／canyon")
                else: errors.append(f"第 {ln} 行：房間不認得的設定 {k}")
            elif d is not None:
                if k == "level":
                    if not v.isdigit(): errors.append(f"第 {ln} 行：level 要是整數")
                    else: d["level"] = int(v)
                elif k == "players":
                    if not re.fullmatch(r"\d+(-\d+)?", v): errors.append(f"第 {ln} 行：players 寫成 2-4 這樣")
                elif k == "unlock": d["unlock"] = None if v in ("", "none") else v
                else: errors.append(f"第 {ln} 行：副本不認得的設定 {k}")
            continue
    stages = story_stage_ids()
    for d in dungeons:
        tag = f"副本 {d['id']}（第 {d['line']} 行）"
        if not d["rooms"]: errors.append(f"{tag}：沒有房間"); continue
        if d["level"] < 1: errors.append(f"{tag}：缺少 level")
        if d["before"] == 0 or d["after"] == 0: errors.append(f"{tag}：要有 <before> 和 <after> 劇情")
        for r in d["rooms"]:
            if r["wave"] < 1: errors.append(f"{tag} 房間「{r['title']}」：缺少 wave")
            if r["enter"] == 0: errors.append(f"{tag} 房間「{r['title']}」：要有 <enter> 台詞")
        if d["rooms"][-1]["wave"] % 10 != 0: errors.append(f"{tag}：最後一個房間要是首領（wave 是 10 的倍數）")
        if d["unlock"] and stages and d["unlock"] not in stages: errors.append(f"{tag}：unlock 的故事關卡「{d['unlock']}」不存在")
    return dungeons, errors

if __name__ == "__main__":
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    dungeons, errors = check(path)
    for d in dungeons:
        print(f"{d['id']:<16} {d['name']}　Lv{d['level']}　房間 " + ",".join(str(r['wave']) for r in d['rooms']))
    for e in errors: print("❌ " + e)
    print(f"{len(dungeons)} 個副本，{len(errors)} 個錯誤")
    sys.exit(1 if errors else 0)
