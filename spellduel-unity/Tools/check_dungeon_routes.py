#!/usr/bin/env python3
"""Check every route of the three new side dungeons (legacy four stay unchanged)."""
from check_dungeons import check, DEFAULT

EXPECTED = {"ember_bell_foundry": (6, 5, 9, 10),
            "mist_name_grove": (7, 12, 19, 20),
            "white_sail_echo_reef": (8, 21, 29, 30)}


def main():
    errors, dungeons = check(DEFAULT)
    if errors:
        raise SystemExit("\n".join(errors))
    found = {d["id"]: d for d in dungeons}
    for did, (count, low, high, boss) in EXPECTED.items():
        d = found[did]
        rooms = {r["id"]: r for r in d["rooms"]}
        routes = []

        def walk(r, battles, path):
            assert r["id"] not in path, f"{did}: cycle"
            is_boss = r["kind"] == "boss" or r["wave"] > 0 and r["wave"] % 10 == 0
            if is_boss:
                assert not r["next"] and r["wave"] == boss, f"{did}: wrong final boss"
            elif r["kind"] != "chest":
                assert low <= r["wave"] <= high, f"{did}: wrong normal wave"
                battles += 1
            if not r["next"]:
                assert is_boss, f"{did}: non-boss ending"
                assert 6 <= battles <= 9 and battles == count, f"{did}: {battles} battles"
                routes.append(battles)
            for nxt in r["next"]:
                walk(rooms[nxt], battles, path + [r["id"]])

        walk(d["rooms"][0], 0, [])
        assert len(routes) >= 2, f"{did}: missing branch"
        assert sum(r["kind"] == "chest" for r in rooms.values()) == 1
        print(f"{did}: {len(routes)} routes; normal battles {min(routes)}-{max(routes)}; final boss {boss}: OK")
    print("All new dungeon routes passed.")


if __name__ == "__main__":
    main()
