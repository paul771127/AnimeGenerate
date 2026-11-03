using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 月影古堡（style = castle）每個房間的陷阱。使用者要求：
    ///   「每個房間都加一些攻擊玩家的機關：地刺；玩家踩到某區域會觸發機關，牆壁拋物線射出南瓜炸彈（地上有紅色範圍預警）；
    ///    靠近某牆壁會突然有幽靈從牆壁衝向玩家攻擊」
    /// 位置照地圖的區域用固定種子決定（每台手機長得一樣）；判定只打自己（Me），每台手機各自跑，不用連線。
    /// 傷害照玩家最大血量的比例（副本 50 級，固定數字會太輕或太重）。
    /// </summary>
    public partial class SoloBattle
    {
        const float SpikePeriod = 4.2f, SpikeWarn = 0.9f, SpikeUp = 0.8f, SpikeHalf = 0.9f;
        const float PlateRadius = 0.8f, PlateCooldown = 7f, BombFlight = 1.5f, BombRadius = 1.7f;
        const float GhostTrigger = 2.6f, GhostCooldown = 11f, GhostSpeed = 6.5f, GhostLife = 1.6f;

        class CastleSpike { public Vector3 pos; public float phase; public Transform spikes; public Renderer plate; public float hitCycle = -1; }
        class CastlePlate { public Vector3 pos, wall; public float readyAt; public Renderer rend; }
        class CastleHaunt { public Vector3 pos, outDir; public float readyAt; }
        class CastleBomb { public Vector3 from, to; public float t0; public GameObject bomb, warn; }
        class CastleGhost { public Vector3 pos, dir; public float t0; public bool hit; public GameObject go; public SpriteEnemyView view; }

        List<CastleSpike> cSpikes; List<CastlePlate> cPlates; List<CastleHaunt> cHaunts;
        readonly List<CastleBomb> cBombs = new List<CastleBomb>();
        readonly List<CastleGhost> cGhosts = new List<CastleGhost>();
        Transform cTrapRoot;
        static SkillDef trapSpike, trapBomb, trapGhost;
        static Mesh spikeMesh;

        bool CastleTrapsOn(ExploreRun run) => run != null && run.IsDungeon && dungeon != null && dungeon.style == "castle";

        /// <summary>UpdateExplore（戰鬥中、自己活著）每幀叫</summary>
        void UpdateCastleTraps(ExploreRun run, float dt)
        {
            if (!CastleTrapsOn(run) || !exRoot) return;
            if (cSpikes == null || !cTrapRoot) BuildCastleTraps(run);
            float t = Time.time;
            var me = Me.Feet;

            // ---- 地刺：每 4.2 秒一輪，先發紅光、喀喀作響 0.9 秒，再刺出 0.8 秒
            foreach (var s in cSpikes)
            {
                float c = (t + s.phase) / SpikePeriod, ph = (c - Mathf.Floor(c)) * SpikePeriod;
                int cycle = Mathf.FloorToInt(c);
                bool warn = ph < SpikeWarn, up = ph >= SpikeWarn && ph < SpikeWarn + SpikeUp;
                float h = up ? Mathf.Clamp01((ph - SpikeWarn) / 0.08f) : warn ? 0.12f * Mathf.Abs(Mathf.Sin(ph * 40f)) * (ph / SpikeWarn) : 0f;
                if (s.spikes) s.spikes.localScale = new Vector3(1f, Mathf.Max(0.001f, h), 1f);
                if (s.plate) s.plate.material.color = warn ? Color.Lerp(new Color(.22f, .2f, .22f), new Color(.9f, .1f, .05f), 0.5f + 0.5f * Mathf.Sin(t * 22f)) : up ? new Color(.5f, .08f, .05f) : new Color(.22f, .2f, .22f);
                if (up && s.hitCycle != cycle && Mathf.Abs(me.x - s.pos.x) < SpikeHalf + 0.25f && Mathf.Abs(me.z - s.pos.z) < SpikeHalf + 0.25f)
                {
                    s.hitCycle = cycle;
                    TrapHit(trapSpike, 0.08f, s.pos);
                }
            }

            // ---- 壓力板：踩到 → 牆上射出南瓜炸彈（拋物線），落點先畫紅色圓圈
            foreach (var p in cPlates)
            {
                bool on = Battle.FlatDistance(p.pos, me) < PlateRadius;
                if (p.rend) p.rend.material.color = t < p.readyAt ? new Color(.35f, .25f, .15f) : on ? new Color(1f, .6f, .1f) : new Color(.62f, .48f, .22f);
                if (!on || t < p.readyAt) continue;
                p.readyAt = t + PlateCooldown;
                Say("喀嚓——牆裡傳來引信的嘶嘶聲！", 1.6f);
                Haptics.Play(Haptics.Kind.Light);
                for (int i = 0; i < 2; i++)   // 一發瞄準腳下、一發瞄準前方（往前跑也躲不掉，要橫向閃）
                {
                    var to = me + (i == 0 ? Vector3.zero : Fighter.Flat(Me.forward).normalized * 2.2f); to.y = 0f;
                    if (run.map != null && !run.map.Walkable(to, 0.2f)) to = me;
                    LaunchBomb(p.wall + Vector3.up * 2.4f, to, t + i * 0.35f);
                }
            }
            for (int i = cBombs.Count - 1; i >= 0; i--)
            {
                var b = cBombs[i]; float k = (t - b.t0) / BombFlight;
                if (k < 0f) { if (b.bomb) b.bomb.SetActive(false); continue; }
                if (b.bomb && !b.bomb.activeSelf) b.bomb.SetActive(true);
                if (b.warn) { float pulse = 0.5f + 0.5f * Mathf.Sin(t * (8f + 16f * k)); b.warn.GetComponent<Renderer>().material.color = new Color(1f, .08f, .04f, (0.22f + 0.4f * k) * (0.6f + 0.4f * pulse)); }
                if (k < 1f)
                {
                    var pos = Vector3.Lerp(b.from, b.to, k) + Vector3.up * (4f * 2.6f * k * (1f - k));
                    if (b.bomb) { b.bomb.transform.localPosition = pos; b.bomb.transform.localRotation = Quaternion.Euler(k * 540f, k * 220f, 0f); }
                    continue;
                }
                BossRig.SlamShockwave(WorldFrame.FromWorld(b.to), BombRadius, new Color(1f, .45f, .05f));
                MinionRig.SpawnFx(WorldFrame.FromWorld(b.to + Vector3.up * 0.4f));
                if (Battle.FlatDistance(b.to, Me.Feet) < BombRadius + 0.2f) TrapHit(trapBomb, 0.15f, b.to);
                else { float d = Battle.FlatDistance(b.to, Me.Feet); if (d < 5f) Kick(Mathf.Lerp(10f, 3f, d / 5f), 0f); }
                if (b.bomb) Destroy(b.bomb); if (b.warn) Destroy(b.warn);
                cBombs.RemoveAt(i);
            }

            // ---- 鬧鬼的牆：靠近 → 幽靈從牆裡衝出來（直線衝刺、稍微追一點；蹲下躲不掉，要閃開）
            foreach (var h in cHaunts)
            {
                if (t < h.readyAt || Battle.FlatDistance(h.pos, me) > GhostTrigger) continue;
                h.readyAt = t + GhostCooldown;
                var go = new GameObject("Castle wall ghost"); go.transform.SetParent(cTrapRoot, false);
                var start = h.pos - h.outDir * 0.3f;
                var dir = Fighter.Flat(me - start); dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : h.outDir;
                go.transform.localPosition = start; go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                var v = SpriteEnemyView.Create(SpriteMonsters.Get(204), go.transform);
                v.Attack(null);
                cGhosts.Add(new CastleGhost { pos = start, dir = dir, t0 = t, go = go, view = v });
                Haptics.Play(Haptics.Kind.Medium);
                Say("牆壁裡有東西衝出來了！", 1.4f);
            }
            for (int i = cGhosts.Count - 1; i >= 0; i--)
            {
                var g = cGhosts[i]; float age = t - g.t0;
                if (!g.hit && age > 0.25f)
                {
                    var want = Fighter.Flat(me - g.pos);
                    if (want.sqrMagnitude > 1e-4f) g.dir = Vector3.RotateTowards(g.dir, want.normalized, 1.2f * dt, 0f);   // 追一點點，不會一直黏著
                    g.pos += g.dir * GhostSpeed * dt;
                    if (Battle.FlatDistance(g.pos, me) < 0.75f) { g.hit = true; TrapHit(trapGhost, 0.1f, g.pos); g.view.SetDead(true); g.t0 = t - GhostLife + 0.7f; }
                }
                if (g.go) { g.go.transform.localPosition = g.pos; g.go.transform.localRotation = Quaternion.LookRotation(g.dir, Vector3.up); if (g.view) g.view.MoveSpeed = g.hit ? 0f : GhostSpeed; }
                if (!g.hit && age > GhostLife - 0.6f && g.view) g.view.SetDead(true);
                if (age > GhostLife) { if (g.go) Destroy(g.go); cGhosts.RemoveAt(i); }
            }
        }

        void TrapHit(SkillDef s, float frac, Vector3 at)
        {
            if (Me == null || !Me.Alive) return;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(Me.maxHp * frac));
            battle.ApplyRemoteHit(s, dmg, at, true);
            Haptics.Play(Haptics.Kind.Heavy); Kick(14f, 0f);
        }

        void LaunchBomb(Vector3 from, Vector3 to, float t0)
        {
            var bomb = new GameObject("Pumpkin bomb"); bomb.transform.SetParent(cTrapRoot, false);
            var body = Prim(PrimitiveType.Sphere, new Color(1f, .5f, .08f)); body.transform.SetParent(bomb.transform, false); body.transform.localScale = new Vector3(.55f, .45f, .55f);
            var stem = Prim(PrimitiveType.Cylinder, new Color(.25f, .45f, .12f)); stem.transform.SetParent(bomb.transform, false); stem.transform.localPosition = new Vector3(0, .25f, 0); stem.transform.localScale = new Vector3(.08f, .08f, .08f);
            var fuse = Prim(PrimitiveType.Sphere, new Color(1f, .95f, .4f), true); fuse.transform.SetParent(bomb.transform, false); fuse.transform.localPosition = new Vector3(0, .36f, 0); fuse.transform.localScale = Vector3.one * .12f;
            bomb.transform.localPosition = from;
            var warn = Prim(PrimitiveType.Quad, new Color(1f, .08f, .04f, .25f), true);
            warn.GetComponent<MeshFilter>().sharedMesh = FanMesh(180f);
            warn.transform.SetParent(cTrapRoot, false);
            warn.transform.localPosition = new Vector3(to.x, 0.03f, to.z); warn.transform.localRotation = Quaternion.identity;
            warn.transform.localScale = new Vector3(BombRadius, 1f, BombRadius);
            cBombs.Add(new CastleBomb { from = from, to = to, t0 = t0, bomb = bomb, warn = warn });
        }

        /// <summary>照地圖的區域放陷阱（固定種子：同一個副本每次一樣）</summary>
        void BuildCastleTraps(ExploreRun run)
        {
            ClearCastleTraps();
            cSpikes = new List<CastleSpike>(); cPlates = new List<CastlePlate>(); cHaunts = new List<CastleHaunt>();
            trapSpike ??= new SkillDef { id = "trap_spike", name = "地刺", color = new Color(.85f, .85f, .9f) };
            trapBomb ??= new SkillDef { id = "trap_bomb", name = "南瓜炸彈", color = new Color(1f, .5f, .1f) };
            trapGhost ??= new SkillDef { id = "trap_ghost", name = "牆中幽靈", color = new Color(.7f, .5f, 1f) };
            cTrapRoot = new GameObject("Castle traps").transform; cTrapRoot.SetParent(exRoot, false);
            var map = run.map;
            foreach (var a in map.areas)
            {
                if (a.IsChest) continue;
                var rng = new System.Random(9173 + a.index * 131);
                var cells = new List<Vector2Int>();
                for (int x = a.rect.xMin; x <= a.rect.xMax; x++) for (int z = a.rect.yMin; z <= a.rect.yMax; z++) if (map.Open(x, z)) cells.Add(new Vector2Int(x, z));
                if (cells.Count == 0) continue;
                var used = new HashSet<Vector2Int>();
                Vector2Int Pick(bool inner)
                {
                    for (int tries = 0; tries < 40; tries++)
                    {
                        var c = cells[rng.Next(cells.Count)];
                        if (used.Contains(c)) continue;
                        bool edge = c.x == a.rect.xMin || c.x == a.rect.xMax || c.y == a.rect.yMin || c.y == a.rect.yMax;
                        if (inner && edge) continue;
                        if (Battle.FlatDistance(map.World(c.x, c.y), a.center) < 1.5f) continue;   // 正中間留給怪物／首領
                        used.Add(c); return c;
                    }
                    return new Vector2Int(-999, -999);
                }
                // 地刺：一般房 3 塊、首領房 4 塊（錯開時間）
                int nSpike = a.IsBoss ? 4 : 3;
                for (int i = 0; i < nSpike; i++)
                {
                    var c = Pick(false); if (c.x < -900) continue;
                    var j = new Vector3((float)rng.NextDouble() - .5f, 0f, (float)rng.NextDouble() - .5f) * 1.2f;
                    var s = new CastleSpike { pos = map.World(c.x, c.y) + j, phase = (float)rng.NextDouble() * SpikePeriod };
                    MakeSpikeVisual(s); cSpikes.Add(s);
                }
                // 南瓜炸彈的壓力板（首領房不放：首領自己就夠忙了）
                if (!a.IsBoss)
                    for (int i = 0; i < 2; i++)
                    {
                        var c = Pick(true); if (c.x < -900) c = Pick(false); if (c.x < -900) continue;
                        var pos = map.World(c.x, c.y);
                        var p = new CastlePlate { pos = pos, wall = NearestWallPoint(map, c) };
                        var go = Prim(PrimitiveType.Cylinder, new Color(.62f, .48f, .22f));
                        go.transform.SetParent(cTrapRoot, false); go.transform.localPosition = pos + Vector3.up * 0.02f; go.transform.localScale = new Vector3(PlateRadius * 1.6f, 0.02f, PlateRadius * 1.6f);
                        var rune = Prim(PrimitiveType.Cylinder, new Color(.25f, .15f, .08f)); rune.transform.SetParent(go.transform, false); rune.transform.localPosition = new Vector3(0, 1.1f, 0); rune.transform.localScale = new Vector3(.45f, .5f, .45f);
                        p.rend = go.GetComponent<Renderer>();
                        cPlates.Add(p);
                    }
                // 鬧鬼的牆：區域邊上的牆格（2 處）
                var walls = new List<(Vector3 pos, Vector3 dir)>();
                foreach (var c in cells)
                {
                    var d4 = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
                    foreach (var d in d4)
                        if (!map.Open(c.x + d.x, c.y + d.y))
                        {
                            var dir = new Vector3(d.x, 0f, d.y);
                            walls.Add((map.World(c.x, c.y) + dir * (ExploreMap.CellSize * 0.5f - 0.1f), dir));
                        }
                }
                for (int i = 0; i < 2 && walls.Count > 0; i++)
                {
                    int k = rng.Next(walls.Count); var w = walls[k]; walls.RemoveAt(k);
                    cHaunts.Add(new CastleHaunt { pos = w.pos, outDir = -w.dir, readyAt = 0f });
                    // 看得出來的小提示：牆腳一抹紫色的霧光（不明顯，留給眼尖的人）
                    var glow = Prim(PrimitiveType.Quad, new Color(.55f, .3f, 1f, .18f), true);
                    glow.transform.SetParent(cTrapRoot, false);
                    glow.transform.localPosition = w.pos - w.dir * 0.05f + Vector3.up * 0.9f;
                    glow.transform.localRotation = Quaternion.LookRotation(w.dir, Vector3.up);
                    glow.transform.localScale = new Vector3(1.0f, 1.6f, 1f);
                }
            }
        }

        static Vector3 NearestWallPoint(ExploreMap map, Vector2Int c)
        {
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            for (int r = 1; r <= 6; r++)
                foreach (var d in dirs)
                    if (!map.Open(c.x + d.x * r, c.y + d.y * r))
                        return map.World(c.x + d.x * (r - 1), c.y + d.y * (r - 1)) + new Vector3(d.x, 0f, d.y) * (ExploreMap.CellSize * 0.5f - 0.15f);
            return map.World(c.x, c.y) + Vector3.right * 6f;
        }

        void MakeSpikeVisual(CastleSpike s)
        {
            var plate = Prim(PrimitiveType.Cube, new Color(.22f, .2f, .22f));
            plate.transform.SetParent(cTrapRoot, false);
            plate.transform.localPosition = s.pos + Vector3.up * 0.015f;
            plate.transform.localScale = new Vector3(SpikeHalf * 2f, 0.03f, SpikeHalf * 2f);
            s.plate = plate.GetComponent<Renderer>();
            var spikes = new GameObject("Spikes").transform; spikes.SetParent(cTrapRoot, false); spikes.localPosition = s.pos;
            if (!spikeMesh) spikeMesh = BuildSpikeMesh();
            for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
            {
                var sp = Prim(PrimitiveType.Cube, new Color(.78f, .78f, .82f));
                sp.GetComponent<MeshFilter>().sharedMesh = spikeMesh;
                sp.transform.SetParent(spikes, false);
                sp.transform.localPosition = new Vector3(x * 0.34f + ((z & 1) * 0.17f), 0f, z * 0.34f);
                sp.transform.localScale = new Vector3(0.18f, 0.75f, 0.18f);
            }
            spikes.localScale = new Vector3(1f, 0.001f, 1f);
            s.spikes = spikes;
        }

        /// <summary>四角錐（底 1×1、高 1，底面在 y=0）</summary>
        static Mesh BuildSpikeMesh()
        {
            var v = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(.5f, 0, -.5f), new Vector3(.5f, 0, .5f), new Vector3(-.5f, 0, .5f), new Vector3(0, 1, 0) };
            var t = new[] { 0, 4, 1, 1, 4, 2, 2, 4, 3, 3, 4, 0 };
            var m = new Mesh { name = "Spike", vertices = v, triangles = t };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        void ClearCastleTraps()
        {
            if (cTrapRoot) Destroy(cTrapRoot.gameObject);
            cTrapRoot = null; cSpikes = null; cPlates = null; cHaunts = null;
            cBombs.Clear(); cGhosts.Clear();
        }
    }
}
