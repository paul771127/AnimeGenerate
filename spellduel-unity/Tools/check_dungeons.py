#!/usr/bin/env python3
"""檢查副本劇本（Assets/SpellDuel/Resources/dungeons.txt）——和遊戲裡 Dungeons.Parse 同樣的規則。

用法：python3 Tools/check_dungeons.py [檔案]（預設檢查 Resources/dungeons.txt）
沒有錯誤＝exit 0；有錯誤會列出行號，exit 1。量產副本後先跑這個再提交。
"""
import re, sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT = os.path.join(HERE, "..", "Assets", "SpellDuel", "Resources", "dungeons.txt")
STORY = os.path.join(HERE, "..", "Assets", "SpellDuel", "Resources", "story.txt")
THEMES = {"vault","volcano", "熔岩", "0", "forest", "森林", "1", "sea", "海", "海上", "2", "arena", "角鬥場", "3", "canyon", "山道", "黃土山道", "4"}
LOOKS = {"mage", "archer", "swordsman", "priest", "assassin"}
ROOM_EVENTS = {"defend", "survive", "escort", "hunt", "totems", "runes", "chant", "elements", "mimic", "collapse", "dark"}
EVENT_KEYS = {"name", "hp", "time", "count", "seq", "riddle", "answer", "hint", "look", "fail"}
MAX_LINE = 60
ROOM_TAGS = ("enter", "clear", "boss_half")
NPC_TAGS = ("meet", "die", "done", "safe", "ally", "enemy", "third")


def story_stage_ids():
    ids = set()
    try:
        for l in open(STORY, encoding="utf-8"):
            m = re.match(r"\s*\[stage\s+([^\]]+)\]", l)
            if m: ids.add(m.group(1).strip())
    except OSError:
        pass
    return ids


def split_list(v):
    return [x.strip() for x in re.split(r"[,，、]", v) if x.strip()]


def is_key_line(raw):
    """「英文小寫 key = 值」：設定行（會結束上面的對話區塊）"""
    return re.match(r"^[a-z_]+\s*=", raw) is not None


def check(path):
    errors, dungeons = [], []
    d = room = npc = None
    lines = None
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
                if not re.fullmatch(r"[A-Za-z0-9_]+", arg): errors.append(f"第 {ln} 行：副本 id 只能用英文、數字、底線")
                if any(x["id"] == arg for x in dungeons): errors.append(f"第 {ln} 行：副本 id「{arg}」重複了")
                d = {"id": arg, "name": title, "level": 0, "unlock": None, "rooms": [], "npcs": [], "before": 0, "after": 0, "line": ln}
                dungeons.append(d); room = npc = None
            elif kind in ("room", "node"):
                if d is None: errors.append(f"第 {ln} 行：[{kind}] 要放在 [dungeon] 底下"); continue
                if kind == "node" and not arg: errors.append(f"第 {ln} 行：[node] 後面要寫地點 id")
                rid = arg or f"r{len(d['rooms']) + 1}"
                if any(r["id"] == rid for r in d["rooms"]): errors.append(f"第 {ln} 行：地點 id「{rid}」重複了")
                room = {"id": rid, "title": title, "wave": 0, "kind": "battle", "next": [], "npcs": [], "enter": 0, "line": ln}
                d["rooms"].append(room); npc = None
            elif kind == "npc":
                if d is None: errors.append(f"第 {ln} 行：[npc] 要放在 [dungeon] 底下"); continue
                if not arg: errors.append(f"第 {ln} 行：[npc] 後面要寫 id"); continue
                if any(n["id"] == arg for n in d["npcs"]): errors.append(f"第 {ln} 行：人物 id「{arg}」重複了")
                npc = {"id": arg, "name": title, "role": "escort", "options": 0, "goal": None, "goal_node": None, "until": None, "meet": 0, "line": ln}
                d["npcs"].append(npc); room = None
            else:
                errors.append(f"第 {ln} 行：不認得的區塊 [{kind}]")
            continue
        if raw.startswith("<"):
            tag = raw.strip("<> ").lower()
            if npc is not None: ok = tag in NPC_TAGS
            elif room is not None: ok = tag in ROOM_TAGS
            else: ok = d is not None and tag in ("before", "after")
            if not ok:
                errors.append(f"第 {ln} 行：<{tag}> 放錯地方（副本用 before／after，地點用 enter／clear／boss_half，人物用 meet／die／done／safe／ally／enemy／third）")
                lines = None
            else:
                lines = tag
            continue
        if lines and not is_key_line(raw):
            text = re.split(r"[：:]", raw, maxsplit=1)[-1].strip() if re.match(r"^[^：:]{1,24}[：:]", raw) else raw
            if len(text) > MAX_LINE: errors.append(f"第 {ln} 行：台詞超過 {MAX_LINE} 字（{len(text)}），請拆成兩句")
            if npc is not None and lines == "meet": npc["meet"] += 1
            elif room is not None and lines == "enter": room["enter"] += 1
            elif room is None and npc is None and lines in ("before", "after"): d[lines] += 1
            continue
        lines = None
        if "=" in raw:
            k, v = [x.strip() for x in raw.split("=", 1)]
            k = k.lower()
            parts = v.split()
            if npc is not None:
                if k == "role":
                    if v not in ("escort", "choice", "quest", "lever", "shop"): errors.append(f"第 {ln} 行：role 只能是 escort／choice／quest")
                    npc["role"] = v
                elif k == "hp":
                    if not v.isdigit() or int(v) < 1: errors.append(f"第 {ln} 行：hp 要是 1 以上的整數")
                elif k == "look":
                    if v not in LOOKS: errors.append(f"第 {ln} 行：look 只能是 mage／archer／swordsman／priest／assassin")
                elif k in ("tag", "optional"): pass
                elif k == "until": npc["until"] = v
                elif k == "option":
                    m = re.match(r"(.+?)\s*->\s*(\w+)\s*$", v)
                    if not m or m.group(2).lower() not in (("correct", "wrong", "neutral") if npc["role"] == "lever" else ("ally", "enemy", "third")): errors.append(f"第 {ln} 行：option 的格式是「選項文字 -> ally／enemy／third」")
                    else: npc["options"] += 1
                elif k == "goal":
                    g = parts[0].lower() if parts else ""
                    if g == "kill" and len(parts) >= 2 and parts[1].isdigit(): npc["goal"] = g
                    elif g == "collect" and len(parts) >= 3 and parts[1].isdigit(): npc["goal"] = g
                    elif g == "reach" and len(parts) >= 2: npc["goal"] = g; npc["goal_node"] = parts[1]
                    else: errors.append(f"第 {ln} 行：goal 的格式是「kill 數量」「collect 數量 物品名」或「reach 地點id」")
                elif k == "reward":
                    ok = len(parts) >= 2 and ((parts[0] == "gold" and parts[1].isdigit()) or (parts[0] == "item" and parts[1] in ("1", "2", "3", "4")))
                    if not ok: errors.append(f"第 {ln} 行：reward 的格式是「gold 金額」或「item 稀有度(1～4)」")
                else: errors.append(f"第 {ln} 行：人物不認得的設定 {k}")
            elif room is not None:
                if k == "wave":
                    if not v.isdigit() or int(v) < 1: errors.append(f"第 {ln} 行：wave 要是 1 以上的整數")
                    else: room["wave"] = int(v)
                elif k == "theme":
                    if v.lower() not in THEMES: errors.append(f"第 {ln} 行：theme 只能是 volcano／forest／sea／arena／canyon")
                elif k == "event":
                    if v.lower() not in {"battle", "chest", "boss"} | ROOM_EVENTS: errors.append(f"第 {ln} 行：不認得的 event {v}")
                    room["kind"] = v.lower()
                elif k in EVENT_KEYS:
                    if k in {"hp", "time", "count", "seq"} and (not v.isdigit() or int(v) < 1): errors.append(f"第 {ln} 行：{k} 要是正整數")
                    if k == "look" and v not in LOOKS: errors.append(f"第 {ln} 行：look 不認得 {v}")
                elif k == "next": room["next"] += split_list(v)
                elif k == "npc": room["npcs"] += split_list(v)
                else: errors.append(f"第 {ln} 行：地點不認得的設定 {k}")
            elif d is not None:
                if k == "level":
                    if not v.isdigit(): errors.append(f"第 {ln} 行：level 要是整數")
                    else: d["level"] = int(v)
                elif k == "players":
                    if not re.fullmatch(r"\d+(-\d+)?", v): errors.append(f"第 {ln} 行：players 寫成 2-4 這樣")
                elif k == "time_limit":
                    if not v.isdigit() or int(v)<1: errors.append(f"第 {ln} 行：time_limit 必須為分鐘正整數")
                    else: d["time_limit"]=int(v)
                elif k == "traps":
                    if v not in ("on","off"): errors.append(f"第 {ln} 行：traps 只能是 on/off")
                    d["traps"]=v
                elif k == "unlock": d["unlock"] = None if v in ("", "none") else v
                else: errors.append(f"第 {ln} 行：副本不認得的設定 {k}")
            continue

    stages = story_stage_ids()
    for d in dungeons:
        rooms, npcs = d["rooms"], d["npcs"]
        by_id = {r["id"]: r for r in rooms}
        npc_ids = {n["id"] for n in npcs}
        if len(rooms) < 2: errors.append(f"副本 {d['id']}：至少要 2 個地點")
        if d["level"] < 1: errors.append(f"副本 {d['id']}：缺少 level")
        if d["before"] == 0: errors.append(f"副本 {d['id']}：缺少 <before>（開場）")
        if d["after"] == 0: errors.append(f"副本 {d['id']}：缺少 <after>（通關）")
        if d["unlock"] and stages and d["unlock"] not in stages: errors.append(f"副本 {d['id']}：unlock 指到不存在的故事關卡 {d['unlock']}")
        is_boss = lambda r: r["kind"] == "boss" or (r["kind"] == "battle" and r["wave"] > 0 and r["wave"] % 10 == 0)
        for r in rooms:
            if r["kind"] != "chest" and r["wave"] < 1: errors.append(f"副本 {d['id']} 地點「{r['title']}」：缺少 wave")
            if r["kind"] == "boss" and r["wave"] % 10 != 0: errors.append(f"副本 {d['id']} 地點「{r['title']}」：首領的 wave 要是 10 的倍數")
            if r["enter"] == 0: errors.append(f"副本 {d['id']} 地點「{r['title']}」：缺少 <enter>")
            for x in r["next"]:
                if x not in by_id: errors.append(f"副本 {d['id']} 地點「{r['title']}」：next 指到不存在的地點 {x}")
            for x in r["npcs"]:
                if x not in npc_ids: errors.append(f"副本 {d['id']} 地點「{r['title']}」：npc 指到不存在的人物 {x}")
        is_map = any(r["next"] for r in rooms)
        if not rooms: continue
        if not is_map:
            if rooms[-1]["wave"] % 10 != 0: errors.append(f"副本 {d['id']}：最後一個房間要是首領（wave 是 10 的倍數）")
        else:
            exits = lambda r: [by_id[x] for x in r["next"] if x in by_id]
            seen, stack = set(), set()
            def walk(r):
                if r["id"] in stack: errors.append(f"副本 {d['id']}：地圖繞圈了（{r['id']}）"); return
                if r["id"] in seen: return
                seen.add(r["id"]); stack.add(r["id"])
                ex = exits(r)
                if not ex and not is_boss(r): errors.append(f"副本 {d['id']}：地點 {r['id']} 沒有 next，但不是首領（終點一定要是首領）")
                for e in ex: walk(e)
                stack.discard(r["id"])
            walk(rooms[0])
            for r in rooms:
                if r["id"] not in seen: errors.append(f"副本 {d['id']}：地點 {r['id']} 從起點走不到")
            if rooms[0]["kind"] == "chest": errors.append(f"副本 {d['id']}：第一個地點不能是寶箱")
        for n in npcs:
            if n["role"] in ("choice", "lever") and n["options"] < 2: errors.append(f"副本 {d['id']} 人物 {n['id']}：抉擇人物至少要 2 個 option")
            if n["role"] == "quest" and not n["goal"]: errors.append(f"副本 {d['id']} 人物 {n['id']}：任務人物要寫 goal")
            if n["goal_node"] and n["goal_node"] not in by_id: errors.append(f"副本 {d['id']} 人物 {n['id']}：goal reach 指到不存在的地點 {n['goal_node']}")
            if n["until"] and n["until"] not in by_id: errors.append(f"副本 {d['id']} 人物 {n['id']}：until 指到不存在的地點 {n['until']}")
            if n["meet"] == 0: errors.append(f"副本 {d['id']} 人物 {n['id']}：要有 <meet>（登場台詞）")
            if not any(n["id"] in r["npcs"] for r in rooms): errors.append(f"副本 {d['id']} 人物 {n['id']}：沒有任何地點寫 npc = {n['id']}")
    for d in dungeons:
        if d['id'] != 'thousand_trap_gallery': continue
        by_id={r['id']:r for r in d['rooms']}
        def paths(r,n,visited):
            if r['id'] in visited:return
            n+=r['kind']!='chest'
            if not r['next']:
                if n!=10:errors.append(f"千機迴廊路線必須恰好10戰，目前{n}")
                return
            for key in r['next']:
                if key in by_id:paths(by_id[key],n,visited|{r['id']})
        paths(d['rooms'][0],0,set())
        if d.get('time_limit')!=25 or d.get('traps')!='on':errors.append('千機迴廊必須25分鐘且traps=on')
    return errors, dungeons


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT
    errors, dungeons = check(path)
    for d in dungeons:
        kinds = [r["kind"] for r in d["rooms"]]
        is_map = any(r["next"] for r in d["rooms"])
        print(f"  {d['id']} {d['name']} Lv{d['level']}：{'地圖' if is_map else '線性'} {len(d['rooms'])} 個地點（寶箱 {kinds.count('chest')}）、人物 {len(d['npcs'])} 個")
    for e in errors: print("錯誤：" + e)
    print(f"共 {len(dungeons)} 個副本，{len(errors)} 個錯誤")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
