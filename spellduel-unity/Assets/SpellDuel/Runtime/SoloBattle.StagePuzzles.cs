using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事小關地圖的機關與密室（使用者要求：參考守望傳說，地圖本身就有謎題、隱藏房間、三星評價）：
    ///   ・推巨石填坑：主線走道上有深坑擋路，前面一顆巨石；從後面往前走就會推動它，推進坑裡就能過去。
    ///   ・機關石門：主線走道被發光的石門封住；踩下（或攻擊點燃）前一個地點角落的 2 個機關才會打開。
    ///   ・裂牆密室：走道旁有一面帶裂痕的牆，站在旁邊攻擊 3 下就會塌，後面是密室（寶箱＋一顆隱藏星星）。
    ///   ・三枚勳章（每關各記最好的）：🎖 通關、🎖 找齊 3 顆隱藏星星、🎖 限時內通關；每拿到一枚新的 +150 金幣。
    /// 機關只放在「擋住才到不了終點」的走道（不會卡死），位置每次都一樣（照地圖算）。
    /// </summary>
    public partial class SoloBattle
    {
        class SpBoulder { public Vector3 pos, dir, pit; public float left; public bool filled, said; public GameObject go, pitGo; }
        class SpKey { public Vector3 pos; public bool on; public GameObject go; }
        class SpDoor { public Vector3 pos, axis; public bool open, torches; public float hintAt; public readonly List<SpKey> keys = new List<SpKey>(); public GameObject go; }
        class SpCrack { public Vector2Int wall; public Vector3 face, dir; public int hits; public float lastHit; public bool broken, said; public GameObject go; }

        readonly List<SpBoulder> spBoulders = new List<SpBoulder>();
        readonly List<SpDoor> spDoors = new List<SpDoor>();
        readonly List<SpCrack> spCracks = new List<SpCrack>();
        readonly List<Vector3> stHiddenSpots = new List<Vector3>();
        readonly List<Vector3> stTaken = new List<Vector3>();
        float stT0, stPar; string stMedalStage;
        Vector3 spLastFeet;

        const float BoulderR = 1.9f, PushSpeed = 2.6f;
        const int CrackHits = 3;

        // ---------------------------------------------------------------- 產生
        void BuildStagePuzzles(ExploreRun run)
        {
            spBoulders.Clear(); spDoors.Clear(); spCracks.Clear(); stHiddenSpots.Clear(); stTaken.Clear();
            var map = run.map;
            map.extraBlock = StageBlock;
            int n = 0; foreach (var a in map.areas) if (!a.IsChest) n++;
            stT0 = Time.time; stPar = 90f * n + 60f; stMedalStage = dungeon.story.id;
            spLastFeet = Me != null ? Me.Feet : Vector3.zero;
            var rng = new System.Random((dungeon.story.id.GetHashCode() * 31 + 7) & 0x7fffffff);

            var fin = map.arenas.Find(x => x.n == 4);
            var finCell = fin != null ? map.Cell(fin.center) : map.start;
            bool Wall(int x, int z) => !map.InGrid(x, z) || map.At(x, z) == ExCell.Wall;
            bool InArea(Vector2Int c) => map.AreaAt(c) != null;
            int StartDist(Vector2Int c) => Mathf.Abs(c.x - map.start.x) + Mathf.Abs(c.y - map.start.y);
            // 擋住 blocked 這些格子：從起點走得到哪些格（門都算開著）
            HashSet<Vector2Int> Reach(HashSet<Vector2Int> blocked)
            {
                var seen = new HashSet<Vector2Int> { map.start }; var q = new Queue<Vector2Int>(); q.Enqueue(map.start);
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var d in ExploreMap.Dirs)
                    {
                        var nb = c + d;
                        if (Wall(nb.x, nb.y) || blocked.Contains(nb) || !seen.Add(nb)) continue;
                        q.Enqueue(nb);
                    }
                }
                return seen;
            }
            // 直直的一格走道：往 d 方向通、兩旁是牆
            bool Straight(Vector2Int c, Vector2Int d)
            {
                if (Wall(c.x, c.y) || map.At(c.x, c.y) != ExCell.Floor || InArea(c)) return false;
                var p = new Vector2Int(d.y, d.x);
                return !Wall(c.x + d.x, c.y + d.y) && !Wall(c.x - d.x, c.y - d.y) && Wall(c.x + p.x, c.y + p.y) && Wall(c.x - p.x, c.y - p.y);
            }
            var cells = new List<Vector2Int>();
            for (int x = 1; x < map.W - 1; x++) for (int z = 1; z < map.H - 1; z++) cells.Add(new Vector2Int(x, z));
            // 打亂（照種子）
            for (int i = cells.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = cells[i]; cells[i] = cells[j]; cells[j] = t; }
            var used = new List<Vector2Int>();
            bool FarFromUsed(Vector2Int c) { foreach (var u in used) if (Mathf.Abs(u.x - c.x) + Mathf.Abs(u.y - c.y) < 4) return false; return true; }

            // ---- 推巨石填坑
            foreach (var c in cells)
            {
                if (spBoulders.Count > 0 || StartDist(c) < 3 || !FarFromUsed(c)) continue;
                foreach (var d in ExploreMap.Dirs)
                {
                    if (!Straight(c, d) || !Straight(c + d, d) || !Straight(c + d * 2, d)) continue;
                    var blk = new HashSet<Vector2Int> { c, c + d, c + d * 2 };
                    var r = Reach(blk);
                    if (r.Contains(finCell) || !r.Contains(c - d)) continue;
                    var dir = new Vector3(d.x, 0f, d.y);
                    spBoulders.Add(new SpBoulder { pos = map.World(c.x, c.y), dir = dir, pit = map.World(c.x + d.x * 2, c.y + d.y * 2), left = ExploreMap.CellSize * 2f });
                    used.Add(c); used.Add(c + d); used.Add(c + d * 2);
                    break;
                }
            }
            // ---- 機關石門（鑰匙：前一個地點的兩個角落）
            foreach (var c in cells)
            {
                if (spDoors.Count > 0 || StartDist(c) < 3 || !FarFromUsed(c)) continue;
                foreach (var d in ExploreMap.Dirs)
                {
                    if (!Straight(c, d)) continue;
                    var r = Reach(new HashSet<Vector2Int> { c });
                    if (r.Contains(finCell)) continue;
                    // 鑰匙放在門這一邊、最近的地點（不放支線地點、終點）
                    ExArea best = null; float bd = float.MaxValue;
                    foreach (var a in map.areas)
                    {
                        var ac = map.Cell(a.center);
                        if (!r.Contains(ac) || a == null || chSides.Exists(s => s.node == a.node.id) || fin != null && Vector3.Distance(fin.center, a.center) < 0.1f) continue;
                        float dd = Battle.FlatDistance(a.center, map.World(c.x, c.y)); if (dd < bd) { bd = dd; best = a; }
                    }
                    if (best == null) continue;
                    var rc = best.rect;
                    var corners = new List<Vector3>();
                    foreach (var k in new[] { new Vector2Int(rc.xMin, rc.yMin), new Vector2Int(rc.xMax, rc.yMax), new Vector2Int(rc.xMin, rc.yMax), new Vector2Int(rc.xMax, rc.yMin) })
                    {
                        var p = map.World(k.x, k.y);
                        bool ok = map.Walkable(p, 0.4f);
                        foreach (var nn in map.npcs) if (Battle.FlatDistance(nn.pos, p) < 2.5f) ok = false;
                        foreach (var q in corners) if (Battle.FlatDistance(q, p) < 6f) ok = false;
                        if (ok) corners.Add(p);
                    }
                    if (corners.Count < 2) continue;
                    var door = new SpDoor { pos = map.World(c.x, c.y), axis = new Vector3(d.x, 0f, d.y), torches = rng.Next(2) == 0 };
                    for (int i = 0; i < 2; i++) { door.keys.Add(new SpKey { pos = corners[i] }); stTaken.Add(corners[i]); }
                    spDoors.Add(door); used.Add(c);
                    break;
                }
            }
            // ---- 裂牆密室：走道旁的牆後挖一間 3×2 的小房間
            foreach (var f in cells)
            {
                if (spCracks.Count > 0 || Wall(f.x, f.y) || map.At(f.x, f.y) != ExCell.Floor || InArea(f) || StartDist(f) < 3 || !FarFromUsed(f)) continue;
                foreach (var d in ExploreMap.Dirs)
                {
                    var w = f + d; var p = new Vector2Int(d.y, d.x);
                    if (!Wall(w.x, w.y) || !map.InGrid(w.x, w.y) || !Wall(w.x + p.x, w.y + p.y) || !Wall(w.x - p.x, w.y - p.y)) continue;
                    var room = new List<Vector2Int>();
                    for (int a = 2; a <= 3; a++) for (int b = -1; b <= 1; b++) room.Add(f + d * a + p * b);
                    bool ok = true;
                    foreach (var rc in room)
                    {
                        if (rc.x < 2 || rc.y < 2 || rc.x > map.W - 3 || rc.y > map.H - 3 || !Wall(rc.x, rc.y)) { ok = false; break; }
                        foreach (var dd in ExploreMap.Dirs)
                        {
                            var nb = rc + dd;
                            if (room.Contains(nb) || nb == w) continue;
                            if (!Wall(nb.x, nb.y) || InArea(nb)) { ok = false; break; }
                            foreach (var d2 in ExploreMap.Dirs) { var n2 = nb + d2; if (!room.Contains(n2) && n2 != w && map.InGrid(n2.x, n2.y) && !Wall(n2.x, n2.y)) { ok = false; break; } }   // 牆要兩格厚（從別的走道看不到）
                            if (!ok) break;
                        }
                        if (!ok) break;
                    }
                    if (!ok) continue;
                    foreach (var rc in room) map.cells[rc.x, rc.y] = ExCell.Floor;
                    var dir = new Vector3(d.x, 0f, d.y);
                    var face = map.World(w.x, w.y) - dir * (ExploreMap.CellSize * 0.5f - 0.05f);
                    spCracks.Add(new SpCrack { wall = w, face = face, dir = dir });
                    var far = f + d * 3; stHiddenSpots.Add(map.World(far.x, far.y));   // 密室最裡面：一顆隱藏星星
                    var ch = f + d * 2 + p; var chp = map.World(ch.x, ch.y);
                    map.sparkles.Add(new ExSparkle { id = "hidden_chest", kind = "chest", pos = chp, title = "密室的寶箱", text = "牆後的密室裡藏著寶箱！" });
                    used.Add(f); used.Add(w);
                    exGateHash = -1;   // 牆重畫
                    break;
                }
            }
            foreach (var b in spBoulders) { stTaken.Add(b.pos); stTaken.Add(b.pit); }
            foreach (var dr in spDoors) stTaken.Add(dr.pos);
        }

        /// <summary>ExploreMap.Walkable：巨石、還沒填的坑、關著的石門</summary>
        bool StageBlock(Vector3 p, float margin)
        {
            foreach (var b in spBoulders)
            {
                if (Battle.FlatDistance(p, b.pos) < BoulderR + margin) return true;
                if (!b.filled && Battle.FlatDistance(p, b.pit) < BoulderR + margin) return true;
            }
            foreach (var d in spDoors)
            {
                if (d.open) continue;
                var rel = p - d.pos; rel.y = 0f;
                float along = Mathf.Abs(Vector3.Dot(rel, d.axis)), lat = Mathf.Abs(Vector3.Dot(rel, new Vector3(d.axis.z, 0f, -d.axis.x)));
                if (along < 0.5f + margin && lat < ExploreMap.CellSize * 0.55f) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 模型
        GameObject SpPart(Transform parent, PrimitiveType t, Vector3 at, Vector3 size, Color c, Quaternion? rot = null)
        {
            var g = Prim(t, c); g.transform.SetParent(parent, false);
            g.transform.localPosition = at; g.transform.localScale = size; if (rot.HasValue) g.transform.localRotation = rot.Value;
            return g;
        }

        GameObject BoulderVisual()
        {
            var root = new GameObject("Push boulder");
            SpPart(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.45f, 0f), new Vector3(3.3f, 2.9f, 3.2f), new Color(0.48f, 0.45f, 0.42f));
            SpPart(root.transform, PrimitiveType.Sphere, new Vector3(0.6f, 2.2f, -0.5f), new Vector3(1.4f, 1.1f, 1.3f), new Color(0.56f, 0.53f, 0.5f));
            SpPart(root.transform, PrimitiveType.Sphere, new Vector3(-0.7f, 0.9f, 0.8f), new Vector3(1.2f, 1f, 1.2f), new Color(0.4f, 0.38f, 0.36f));
            return root;
        }

        GameObject PitVisual()
        {
            var root = new GameObject("Pit");
            SpPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(3.9f, 0.01f, 3.9f), new Color(0.22f, 0.18f, 0.15f));
            SpPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(3.3f, 0.01f, 3.3f), new Color(0.02f, 0.02f, 0.03f)).name = "Hole";
            return root;
        }

        GameObject DoorVisual(SpDoor d)
        {
            var root = new GameObject("Rune door");
            float half = ExploreMap.CellSize * 0.5f;
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(-half + 0.25f, 1.4f, 0f), new Vector3(0.5f, 2.8f, 0.6f), new Color(0.35f, 0.34f, 0.38f));
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(half - 0.25f, 1.4f, 0f), new Vector3(0.5f, 2.8f, 0.6f), new Color(0.35f, 0.34f, 0.38f));
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 2.9f, 0f), new Vector3(ExploreMap.CellSize, 0.35f, 0.7f), new Color(0.3f, 0.29f, 0.33f));
            var bar = SpPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.3f, 0f), new Vector3(ExploreMap.CellSize - 1f, 2.6f, 0.08f), d.torches ? new Color(1f, 0.55f, 0.2f, 0.45f) : new Color(0.45f, 0.75f, 1f, 0.45f));
            bar.name = "Barrier";
            for (int i = 0; i < 2; i++)
                SpPart(bar.transform, PrimitiveType.Cube, new Vector3(i == 0 ? -0.15f : 0.15f, 0f, -0.6f), new Vector3(0.12f, 0.5f, 1f), d.torches ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.95f, 1f));
            root.transform.localRotation = Quaternion.LookRotation(d.axis, Vector3.up);
            return root;
        }

        GameObject KeyVisual(bool torch)
        {
            var root = new GameObject(torch ? "Torch" : "Plate");
            if (torch)
            {
                SpPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.18f, 0.6f, 0.18f), new Color(0.4f, 0.28f, 0.16f));
                SpPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.25f, 0f), new Vector3(0.5f, 0.08f, 0.5f), new Color(0.3f, 0.3f, 0.32f));
                var fl = SpPart(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.5f, 0f), new Vector3(0.45f, 0.6f, 0.45f), new Color(1f, 0.6f, 0.15f)); fl.name = "Flame"; fl.SetActive(false);
                GlowHalo.Create(fl.transform, Vector3.zero, new Color(1f, 0.6f, 0.2f, 0.8f), 2.4f, 0.3f);
            }
            else
            {
                SpPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(1.4f, 0.04f, 1.4f), new Color(0.38f, 0.38f, 0.42f));
                var top = SpPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(1f, 0.04f, 1f), new Color(0.45f, 0.75f, 1f)); top.name = "Top";
                var glow = GlowHalo.Create(root.transform, new Vector3(0f, 0.2f, 0f), new Color(0.45f, 0.8f, 1f, 0.8f), 1.8f, 0.25f); glow.name = "Flame"; glow.gameObject.SetActive(false);
            }
            return root;
        }

        GameObject CrackVisual(SpCrack c)
        {
            var root = new GameObject("Cracked wall");
            var dark = new Color(0.08f, 0.07f, 0.07f);
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 2.2f, 0.04f), new Color(0.5f, 0.47f, 0.43f));
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.3f, -0.04f), new Vector3(0.09f, 1.7f, 0.03f), dark, Quaternion.Euler(0f, 0f, 18f));
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(0.35f, 1.6f, -0.04f), new Vector3(0.07f, 0.9f, 0.03f), dark, Quaternion.Euler(0f, 0f, -40f));
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(-0.3f, 0.9f, -0.04f), new Vector3(0.07f, 0.8f, 0.03f), dark, Quaternion.Euler(0f, 0f, 55f));
            SpPart(root.transform, PrimitiveType.Cube, new Vector3(0.1f, 0.5f, -0.04f), new Vector3(0.06f, 0.6f, 0.03f), dark, Quaternion.Euler(0f, 0f, -15f));
            root.transform.localRotation = Quaternion.LookRotation(c.dir, Vector3.up);
            return root;
        }

        // ---------------------------------------------------------------- 每幀
        void UpdateStagePuzzles(ExploreRun run, float dt)
        {
            var me = Me.Feet; float view = 30f;
            var mv = me - spLastFeet; mv.y = 0f; spLastFeet = me;
            // 巨石
            foreach (var b in spBoulders)
            {
                bool near = Battle.FlatDistance(b.pos, me) <= view || Battle.FlatDistance(b.pit, me) <= view;
                if (near && !b.go) { b.go = BoulderVisual(); b.go.transform.SetParent(chRoot, false); b.pitGo = PitVisual(); b.pitGo.transform.SetParent(chRoot, false); b.pitGo.transform.localPosition = b.pit; }
                if (b.go) b.go.transform.localPosition = b.pos + (b.filled ? Vector3.down * 2.4f : Vector3.zero);
                if (b.filled) continue;
                var rel = me - b.pos; rel.y = 0f;
                float along = Vector3.Dot(rel, b.dir), lat = (rel - b.dir * along).magnitude;
                if (!b.said && Battle.FlatDistance(me, b.pos) < 7f) { b.said = true; Say("🪨 前面有深坑！把巨石推進坑裡就能過去\n（站到巨石後面，往坑的方向走）", 4.5f); }
                if (along < 0f && along > -(BoulderR + 1.1f) && lat < 1.9f && Vector3.Dot(mv, b.dir) > 0.002f)
                {
                    float step = Mathf.Min(PushSpeed * dt, b.left);
                    b.pos += b.dir * step; b.left -= step;
                    if (b.go) b.go.transform.Rotate(Vector3.Cross(Vector3.up, b.dir), step / 1.5f * Mathf.Rad2Deg, Space.World);
                    if (Time.frameCount % 20 == 0) Haptics.Play(Haptics.Kind.Light);
                    if (b.left <= 0.01f)
                    {
                        b.filled = true; b.pos = b.pit;
                        var hole = b.pitGo ? b.pitGo.transform.Find("Hole") : null; if (hole) hole.gameObject.SetActive(false);
                        Burst(b.pit + Vector3.up * 0.3f, new Color(0.6f, 0.55f, 0.5f));
                        Sfx.Play("summon", 0.8f, 0.7f); Haptics.Play(Haptics.Kind.Heavy);
                        Say("🪨 巨石填平了深坑！路通了", 3f);
                    }
                }
            }
            // 石門
            foreach (var d in spDoors)
            {
                bool near = Battle.FlatDistance(d.pos, me) <= view;
                if (near && !d.go) { d.go = DoorVisual(d); d.go.transform.SetParent(chRoot, false); d.go.transform.localPosition = d.pos; }
                if (d.go)
                {
                    var bar = d.go.transform.Find("Barrier");
                    if (bar) { var lp = bar.localPosition; lp.y = Mathf.MoveTowards(lp.y, d.open ? -1.5f : 1.3f, dt * 2f); bar.localPosition = lp; if (d.open && lp.y <= -1.4f && bar.gameObject.activeSelf) bar.gameObject.SetActive(false); }
                }
                foreach (var k in d.keys)
                {
                    if (!k.go && Battle.FlatDistance(k.pos, me) <= view) { k.go = KeyVisual(d.torches); k.go.transform.SetParent(chRoot, false); k.go.transform.localPosition = k.pos; if (k.on) SetKeyOn(k); }
                    if (!k.on && !d.torches && Battle.FlatDistance(k.pos, me) <= 1.3f) KeyOn(d, k);
                }
                if (!d.open && Battle.FlatDistance(d.pos, me) < 6f && Time.time >= d.hintAt)
                {
                    d.hintAt = Time.time + 7f; int on = d.keys.FindAll(k => k.on).Count;
                    Say(d.torches ? $"🚪 石門封住了：點燃附近地點角落的 {d.keys.Count} 支火把（站在火把旁邊攻擊；{on}/{d.keys.Count}）" : $"🚪 石門封住了：踩下附近地點角落的 {d.keys.Count} 塊機關石板（{on}/{d.keys.Count}）", 4f);
                }
            }
            // 裂牆
            foreach (var c in spCracks)
            {
                if (c.broken) continue;
                bool near = Battle.FlatDistance(c.face, me) <= view;
                if (near && !c.go) { c.go = CrackVisual(c); c.go.transform.SetParent(chRoot, false); c.go.transform.localPosition = c.face; }
                if (!c.said && Battle.FlatDistance(c.face, me) < 4.5f) { c.said = true; Say("👀 這面牆有裂痕……站在旁邊攻擊看看？", 3.5f); }
                if (c.go) { float sh = Mathf.Clamp01(0.25f - (Time.time - c.lastHit)) * 0.3f; c.go.transform.localPosition = c.face + new Vector3(Mathf.Sin(Time.time * 80f) * sh, 0f, 0f); }
            }
        }

        void SetKeyOn(SpKey k)
        {
            if (!k.go) return;
            var f = k.go.transform.Find("Flame"); if (f) f.gameObject.SetActive(true);
            var top = k.go.transform.Find("Top"); if (top) top.localPosition = new Vector3(0f, 0.06f, 0f);
        }

        void KeyOn(SpDoor d, SpKey k)
        {
            k.on = true; SetKeyOn(k);
            int on = d.keys.FindAll(x => x.on).Count;
            Sfx.Play("click", 0.9f, d.torches ? 0.8f : 1.2f); Haptics.Play(Haptics.Kind.Success);
            if (on >= d.keys.Count)
            {
                d.open = true;
                Say("🚪 喀啦——遠處的石門打開了！", 3.5f); Sfx.Play("summon", 0.8f, 1.1f);
            }
            else Say(d.torches ? $"🔥 點燃了火把（{on}/{d.keys.Count}）" : $"◆ 機關石板沉下去了（{on}/{d.keys.Count}）", 2.5f);
        }

        /// <summary>OnBattleEvent：玩家出手（施法、斬擊）——點火把、敲裂牆</summary>
        void StagePuzzleCast(string kind)
        {
            if ((kind != "cast" && kind != "slice") || dungeon == null || dungeon.style != "stage" || !SideQuestMode || Me == null || exploreRun == null) return;
            var me = Me.Feet;
            foreach (var d in spDoors)
                if (d.torches) foreach (var k in d.keys) if (!k.on && Battle.FlatDistance(k.pos, me) <= 3.2f) KeyOn(d, k);
            foreach (var c in spCracks)
            {
                if (c.broken || Battle.FlatDistance(c.face, me) > 3.2f || Time.time - c.lastHit < 0.3f) continue;
                c.hits++; c.lastHit = Time.time;
                Sfx.Play("click", 0.9f, 0.6f); Haptics.Play(Haptics.Kind.Medium);
                Burst(c.face + Vector3.up * 1.2f, new Color(0.6f, 0.55f, 0.5f));
                if (c.hits < CrackHits) { Say($"💥 牆在晃……（{c.hits}/{CrackHits}）", 1.5f); continue; }
                c.broken = true; if (c.go) Destroy(c.go);
                exploreRun.map.cells[c.wall.x, c.wall.y] = ExCell.Floor; exGateHash = -1;
                Sfx.Play("summon", 0.9f, 0.8f); Haptics.Play(Haptics.Kind.Heavy);
                Say("💥 牆塌了！後面藏著一間密室！", 3.5f);
            }
        }

        /// <summary>小地圖：機關的位置（走過看到的才標）</summary>
        IEnumerable<(Vector3 pos, string icon)> StagePuzzleIcons()
        {
            foreach (var b in spBoulders) if (!b.filled) { yield return (b.pos, "🪨"); yield return (b.pit, "🕳"); }
            foreach (var d in spDoors)
            {
                if (d.open) continue;
                yield return (d.pos, "🚪");
                foreach (var k in d.keys) if (!k.on) yield return (k.pos, d.torches ? "🔥" : "◆");
            }
        }

        // ---------------------------------------------------------------- 三枚勳章
        static string MedalKey(string id) => "sd_medal_" + id;
        public static int MedalBits(string id) { int.TryParse(Progress.Get?.Invoke(MedalKey(id)) ?? "", out var b); return b; }

        /// <summary>選關畫面：有小關地圖的關卡顯示拿到幾枚勳章</summary>
        public static string MedalText(string id)
        {
            if (Dungeons.StageMap(id) == null) return "";
            int b = MedalBits(id), n = 0; for (int i = 0; i < 3; i++) if ((b & (1 << i)) != 0) n++;
            return $"　🎖 {n}/3";
        }

        /// <summary>故事通關（結算）：三枚勳章；回傳結算畫面要加的文字</summary>
        string GrantStageMedals(StoryStage st)
        {
            if (st == null || stMedalStage != st.id) return "";
            stMedalStage = null;
            float t = Time.time - stT0;
            bool stars = StarCount(st.id) >= StageStars, fast = t <= stPar;
            int got = 1 | (stars ? 2 : 0) | (fast ? 4 : 0);
            int old = MedalBits(st.id), fresh = got & ~old, nNew = 0;
            for (int i = 0; i < 3; i++) if ((fresh & (1 << i)) != 0) nNew++;
            if (fresh != 0) Progress.Set?.Invoke(MedalKey(st.id), (old | got).ToString());
            if (nNew > 0) pendingGold += 150 * nNew;
            string M(int bit, string name) => ((old | got) & bit) != 0 ? $"🎖{name}" : $"▫{name}";
            return $"\n🎖 勳章：{M(1, "通關")}　{M(2, $"找齊星星（{StarCount(st.id)}/{StageStars}）")}　{M(4, $"限時 {Mathf.FloorToInt(stPar / 60f)}:{Mathf.FloorToInt(stPar % 60f):00}（用了 {Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}）")}"
                + (nNew > 0 ? $"\n　新勳章 ×{nNew}：💰 +{150 * nNew}" : "");
        }
    }
}
