using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的兩種新玩法（使用者要求）：
    ///
    /// 賽跑（race）：
    ///   幾個 NPC 對手跟玩家一起起跑；一路躲開障礙（岩石、木箱會擋住衝刺）、選岔路，跑到終點就過關（名次影響額外金幣）。
    ///   ・泥沼：踩進去速度剩 45%（出來後再慢 1 秒）；加速道具：碰到速度 ×1.8（4 秒），12 秒後再長出來（對手也會撿）。
    ///   ・攻擊：用任何技能打中對手 → 對手原地暈 5 秒；對手也會瞄準玩家（紅線預警 0.9 秒）射出暈眩彈，被打中 → 5 秒不能衝刺。
    ///   ・開跑前倒數 3 秒（不能衝刺）。對手會看距離稍微調整速度（落後太多會追、領先太多會放慢），讓比賽一直有得拚。
    ///
    /// 潛入（stealth）：
    ///   小地圖上有許多巡邏、站崗的敵人（探索的視野規則：正面 90° 扇形 7m、被牆擋住看不到、睡著的要更近），
    ///   被任何一隻發現（或打了牠們）＝關卡失敗；一路不被發現走進最後的地點就過關。
    /// </summary>
    public partial class SoloBattle
    {
        // ---------------------------------------------------------------- 衝刺速度（SoloBattle.Dash.cs 用）
        /// <summary>賽跑中的衝刺倍率（泥沼、加速）；不在賽跑＝1</summary>
        float RaceSpeedMul => race == null ? 1f : Time.time < raceBoostUntil ? 1.8f : Time.time < raceMudUntil ? 0.45f : 1f;
        /// <summary>賽跑中不能衝刺（開跑前倒數、被暈眩）</summary>
        bool RaceFrozen => race != null && (Time.time < raceGoAt || Time.time < raceStunUntil || raceDone);

        class Racer { public Fighter f; public List<Vector3> route; public int idx; public float baseSpeed, stunUntil, boostUntil, mudUntil, nextShotAt, aimUntil = -1f; public bool done; public int rank; public GameObject aimLine, stars; }
        class RaceBolt { public Vector3 pos, dir; public float t0; public GameObject go; }
        class RaceItem { public Vector3 pos; public float back; public GameObject go; }

        RaceTrack race;
        readonly List<Racer> racers = new List<Racer>();
        readonly List<RaceBolt> raceBolts = new List<RaceBolt>();
        readonly List<RaceItem> raceBoostItems = new List<RaceItem>();
        readonly List<GameObject> raceMudGo = new List<GameObject>();
        Transform raceRoot;
        float raceGoAt, raceStunUntil, raceBoostUntil, raceMudUntil, raceSaidAt, raceCountSaid = -1;
        bool raceDone; int raceFinishedAhead;

        const float RacerTopSpeed = 2.5f * 1.5f;   // 玩家在探索地圖的衝刺速度（SoloBattle.Dash.cs：DashSpeed × ExploreDashMul）
        const float RacerSpeedLo = 0.86f, RacerSpeedHi = 0.98f;   // 對手速度＝玩家衝刺速度 ×（0.86～0.98）
        const float RaceStun = 5f, RaceShotSpeed = 9f, RaceShotRange = 7f, RaceAimTime = 0.9f;

        static readonly string[][] RacerNames =
        {
            new[] { "赤爐傭兵・快腿", "熔岩小鬼頭目", "礦坑跑腿小子" },
            new[] { "霧林斥候・疾影", "翠豹騎手", "孢子郵差" },
            new[] { "黑潮海盜・飛刀", "甲板刺客・浪影", "浪尖飛鰭騎手" },
            new[] { "冠軍鬥士・疾足", "標槍手・追風", "影步鬥犬馴師" },
            new[] { "沙匪刺客・砂行", "沙漠獵手・鷹眼", "砂暴狐魅" },
        };
        static readonly string[] RacerLooks = { "assassin", "archer", "swordsman" };

        /// <summary>StartStoryMaze 叫：賽跑的跑道（取代一般的小地圖）</summary>
        ExploreMap BuildRaceMap(DungeonDef d, StoryStage st)
        {
            race = RaceTrack.Build(d, st.theme, 7919 + st.id.GetHashCode() % 100000);
            return race.map;
        }

        /// <summary>StoryMazeBegun 叫：放對手、道具，開始倒數</summary>
        void BeginRace()
        {
            ClearRace(false);
            var st = dungeon.story;
            raceRoot = new GameObject("Race").transform; raceRoot.SetParent(exRoot ? exRoot : null, false);
            raceGoAt = Time.time + 3.5f; raceStunUntil = raceBoostUntil = raceMudUntil = 0f; raceDone = false; raceFinishedAhead = 0; raceCountSaid = -1;
            var rng = new System.Random(st.id.GetHashCode());
            var names = RacerNames[Mathf.Clamp(st.theme, 0, RacerNames.Length - 1)];
            var startW = race.map.World(race.map.start.x, race.map.start.y);
            for (int i = 0; i < 3; i++)
            {
                var at = startW + new Vector3(-0.6f * (i % 2), 0f, (i - 1) * 1.3f);
                var f = battle.SpawnNpc("racer_" + i, names[i], RacerLooks[i], 1, false, 99999, 0, at);
                f.rooted = true;   // 走路、出手都由這裡控制（Combat 的 NPC AI 不動它）
                f.forward = Vector3.right;
                racers.Add(new Racer { f = f, route = race.RouteFor(rng), idx = 1, baseSpeed = Mathf.Lerp(RacerSpeedLo, RacerSpeedHi, i / 2f), nextShotAt = raceGoAt + 4f + i * 2.5f });
            }
            // 道具：泥沼、加速（畫在地上）
            foreach (var p in race.mud)
            {
                var g = Prim(PrimitiveType.Cylinder, new Color(0.32f, 0.2f, 0.12f, 0.85f));
                g.transform.SetParent(raceRoot, false); g.transform.localPosition = p + Vector3.up * 0.015f; g.transform.localScale = new Vector3(RaceTrack.MudRadius * 2f, 0.01f, RaceTrack.MudRadius * 2f);
                raceMudGo.Add(g);
            }
            foreach (var p in race.boosts)
            {
                var g = new GameObject("Boost"); g.transform.SetParent(raceRoot, false); g.transform.localPosition = p;
                var ring = Prim(PrimitiveType.Cylinder, new Color(1f, 0.85f, 0.2f), true); ring.transform.SetParent(g.transform, false); ring.transform.localPosition = Vector3.up * 0.03f; ring.transform.localScale = new Vector3(RaceTrack.BoostRadius * 2f, 0.01f, RaceTrack.BoostRadius * 2f);
                var orb = Prim(PrimitiveType.Sphere, new Color(1f, 0.95f, 0.5f), true); orb.transform.SetParent(g.transform, false); orb.transform.localPosition = Vector3.up * 0.7f; orb.transform.localScale = Vector3.one * 0.35f;
                raceBoostItems.Add(new RaceItem { pos = p, go = g });
            }
            Say("🏁 賽跑！跑到終點就過關（名次越前面獎勵越多）\n岩石、木箱會擋路；泥沼會變慢；黃色光球加速；用技能打中對手可以讓他暈 5 秒", 5f);
        }

        void UpdateRace(float dt)
        {
            if (race == null || battle == null || Me == null) return;
            float now = Time.time;
            // 倒數
            if (now < raceGoAt)
            {
                int n = Mathf.CeilToInt(raceGoAt - now);
                if (n != raceCountSaid && n <= 3) { raceCountSaid = n; Say($"🏁 {n}", 0.9f); Haptics.Play(Haptics.Kind.Light); }
                return;
            }
            if (raceCountSaid != 0) { raceCountSaid = 0; Say("🏁 GO！", 1.2f); Haptics.Play(Haptics.Kind.Heavy); }
            var me = Me.Feet;
            float myProg = race.ProgressAt(me);

            // 我踩到泥沼、加速
            foreach (var p in race.mud) if (Battle.FlatDistance(p, me) < RaceTrack.MudRadius) { if (raceMudUntil < now + 0.5f && now > raceMudUntil) Say("🟫 泥沼！速度變慢", 1f); raceMudUntil = now + 1f; }
            foreach (var it in raceBoostItems)
            {
                bool on = now >= it.back;
                if (it.go && it.go.activeSelf != on) it.go.SetActive(on);
                if (on && it.go) it.go.transform.GetChild(1).localPosition = Vector3.up * (0.7f + 0.12f * Mathf.Sin(now * 4f + it.pos.x));
                if (on && Battle.FlatDistance(it.pos, me) < RaceTrack.BoostRadius) { it.back = now + 12f; raceBoostUntil = now + 4f; Say("⚡ 加速！", 1f); Haptics.Play(Haptics.Kind.Success); }
            }

            // 對手
            int ahead = 0;
            foreach (var r in racers)
            {
                var f = r.f; if (f == null) continue;
                // 被我打到：暈 5 秒（血量補回去）
                if (f.hp < f.maxHp - 0.5f)
                {
                    f.hp = f.maxHp;
                    if (!r.done && now >= r.stunUntil) { r.stunUntil = now + RaceStun; r.aimUntil = -1f; Say($"💫 {f.name}被打暈了 5 秒！", 1.5f); }
                }
                if (r.stars) r.stars.SetActive(now < r.stunUntil);
                if (now < r.stunUntil && !r.stars) { r.stars = Prim(PrimitiveType.Sphere, new Color(1f, 0.95f, 0.3f), true); r.stars.transform.localScale = Vector3.one * 0.18f; }
                if (r.stars) r.stars.transform.position = WorldFrame.FromWorld(f.head + Vector3.up * 0.45f + new Vector3(Mathf.Cos(now * 6f), 0f, Mathf.Sin(now * 6f)) * 0.3f);
                if (r.done) { if (r.aimLine) r.aimLine.SetActive(false); continue; }
                float prog = race.ProgressAt(f.Feet);
                if (prog > myProg + 0.5f) ahead++;
                if (now < r.stunUntil) { if (r.aimLine) r.aimLine.SetActive(false); continue; }
                // 速度：基本×（泥沼／加速）×（拉近差距）
                float gap = prog - myProg;
                float rubber = gap > 6f ? 0.85f : gap < -5f ? 1.12f : 1f;
                foreach (var p in race.mud) if (Battle.FlatDistance(p, f.Feet) < RaceTrack.MudRadius) r.mudUntil = now + 0.8f;
                foreach (var it in raceBoostItems) if (now >= it.back && Battle.FlatDistance(it.pos, f.Feet) < RaceTrack.BoostRadius) { it.back = now + 12f; r.boostUntil = now + 3f; }
                float sp = RacerTopSpeed * r.baseSpeed * rubber * (now < r.boostUntil ? 1.6f : now < r.mudUntil ? 0.5f : 1f);
                if (now < r.aimUntil) sp *= 0.4f;   // 瞄準時放慢
                var target = r.route[Mathf.Min(r.idx, r.route.Count - 1)];
                var to = Fighter.Flat(target - f.Feet); float dist = Battle.FlatDistance(target, f.Feet);
                if (dist < 0.4f) { r.idx++; if (r.idx >= r.route.Count) { r.done = true; r.rank = 1 + racers.FindAll(o => o.done && o != r).Count + (raceDone ? 1 : 0); if (!raceDone) raceFinishedAhead++; Say($"🏁 {f.name}抵達終點（第 {r.rank} 名）", 2f); } }
                else
                {
                    var step = to * Mathf.Min(dist, sp * dt);
                    f.head = new Vector3(f.head.x + step.x, f.head.y, f.head.z + step.z);
                    if (now >= r.aimUntil) f.forward = to;
                }
                // 攻擊：玩家在射程內 → 紅線瞄準 0.9 秒 → 射出暈眩彈
                float dMe = Battle.FlatDistance(f.Feet, me);
                if (r.aimUntil < 0f && now >= r.nextShotAt && dMe < RaceShotRange && dMe > 1.2f && !raceDone) r.aimUntil = now + RaceAimTime;
                if (r.aimUntil > 0f)
                {
                    var aim = Fighter.Flat(me - f.Feet); f.forward = aim;
                    if (!r.aimLine) { r.aimLine = Prim(PrimitiveType.Cube, new Color(1f, 0.15f, 0.1f, 0.6f), true); }
                    r.aimLine.SetActive(true);
                    var a = WorldFrame.FromWorld(f.Chest); var b = WorldFrame.FromWorld(Me.Chest);
                    r.aimLine.transform.position = (a + b) * 0.5f; r.aimLine.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                    r.aimLine.transform.localScale = new Vector3(0.03f, 0.03f, Vector3.Distance(a, b));
                    if (now >= r.aimUntil)
                    {
                        r.aimUntil = -1f; r.nextShotAt = now + Random.Range(6f, 10f); r.aimLine.SetActive(false);
                        var go = Prim(PrimitiveType.Sphere, new Color(0.9f, 0.3f, 1f), true); go.transform.localScale = Vector3.one * 0.3f;
                        raceBolts.Add(new RaceBolt { pos = f.Chest, dir = (Me.Chest - f.Chest).normalized, t0 = now, go = go });
                    }
                }
                else if (r.aimLine) r.aimLine.SetActive(false);
            }
            // 暈眩彈
            for (int i = raceBolts.Count - 1; i >= 0; i--)
            {
                var b = raceBolts[i];
                b.pos += b.dir * RaceShotSpeed * dt;
                if (b.go) b.go.transform.position = WorldFrame.FromWorld(b.pos);
                bool hit = Vector3.Distance(b.pos, Me.Chest) < 0.55f;
                if (hit && !raceDone)
                {
                    raceStunUntil = now + RaceStun; Say("💫 被打暈了！5 秒不能衝刺", 2f); Haptics.Play(Haptics.Kind.Heavy); Kick(12f, 0f);
                }
                if (hit || now - b.t0 > 1.6f || !race.map.Walkable(new Vector3(b.pos.x, 0f, b.pos.z), 0f)) { if (b.go) Destroy(b.go); raceBolts.RemoveAt(i); }
            }
            // 到終點
            if (!raceDone && race.AtFinish(me))
            {
                raceDone = true;
                int rank = 1 + raceFinishedAhead;
                int bonus = rank == 1 ? 300 : rank == 2 ? 150 : rank == 3 ? 60 : 0;
                pendingGold += Mathf.RoundToInt(bonus * (1f + 0.5f * Mathf.Max(0, dungeon.story.theme)));
                Say(rank == 1 ? "🥇 第 1 名！冠軍獎金到手" : $"🏁 抵達終點！第 {rank} 名" + (bonus > 0 ? "（有名次獎金）" : ""), 4f);
                battle.stageCleared = true;
                return;
            }
            // 名次、剩幾公尺（每秒更新一次）
            if (now > raceSaidAt + 1f)
            {
                raceSaidAt = now;
                float left = Mathf.Max(0f, (race.Length - myProg) * ExploreMap.CellSize);
                string st = now < raceStunUntil ? $"💫 暈眩 {raceStunUntil - now:0} 秒・" : now < raceBoostUntil ? "⚡ 加速中・" : now < raceMudUntil ? "🟫 泥沼・" : "";
                Say($"🏁 {st}第 {1 + ahead} 名・終點還有 {left:0}m", 1.1f);
            }
        }

        void ClearRace(bool all)
        {
            foreach (var r in racers) { if (r.aimLine) Destroy(r.aimLine); if (r.stars) Destroy(r.stars); }
            racers.Clear();
            foreach (var b in raceBolts) if (b.go) Destroy(b.go);
            raceBolts.Clear(); raceBoostItems.Clear(); raceMudGo.Clear();
            if (raceRoot) Destroy(raceRoot.gameObject); raceRoot = null;
            if (all) race = null;
        }

        // ---------------------------------------------------------------- 潛入
        bool stealthHooked;
        /// <summary>潛入：怪物少一點（營地最多 3 隻、巡邏一群只留 1 隻），不然幾乎不可能不被發現</summary>
        static void ThinForStealth(ExploreMap m)
        {
            var keep = new List<ExMonster>(); var count = new Dictionary<int, int>();
            foreach (var mon in m.monsters)
            {
                count.TryGetValue(mon.camp, out var c);
                int cap = mon.patrol != null ? 1 : 3;
                if (c >= cap) continue;
                count[mon.camp] = c + 1; keep.Add(mon);
            }
            m.monsters.Clear(); m.monsters.AddRange(keep);
            foreach (var a in m.areas) a.cleared = true;   // 門一開始就開著（不用打倒守衛；被發現才失敗）
        }

        void BeginStealth()
        {
            Say("🥷 潛入：別被巡邏的敵人發現！牠們只看得到正面約 7m 的扇形，牆後看不到；被發現就失敗。走進最後的地點就過關", 6f);
            if (!stealthHooked)
            {
                stealthHooked = true;
                battle.OnExAlert += (m, call) =>
                {
                    if (dungeon == null || dungeon.storyMode != "stealth" || battle.Over) return;
                    Haptics.Play(Haptics.Kind.Warning);
                    battle.FailStage($"被{m.name}發現了！");
                };
            }
        }
    }
}
