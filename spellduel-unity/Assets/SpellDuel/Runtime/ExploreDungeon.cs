using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本大地圖的一個區域（使用者要求：副本全部改成像翠蔭迷林那樣，在一張大地圖上探索、打怪）：
    /// 原本副本的每個房間（地點）變成地圖上的一塊區域。走進去就開始那個地點的內容（怪物營地、特殊事件、寶箱、首領），
    /// 清除之前，往下一站的出口（exits）是關著的門。
    /// </summary>
    public class ExArea
    {
        public int index;
        public DungeonRoom node;
        public RectInt rect;              // 格子範圍（含 xMax、yMax）
        public Vector3 center;
        public float radius;              // 走進這個半徑＝進入區域
        public readonly List<Vector2Int> exits = new List<Vector2Int>();   // 往下一站的門（清除前關著）
        public readonly List<int> next = new List<int>();                   // 下一站的區域編號
        public bool entered, cleared;
        public int wave => node.wave;
        public string Title => string.IsNullOrEmpty(node.title) ? node.id : node.title;
        public bool IsChest => node.IsChest;
        public bool IsBoss => node.IsBoss;
        public bool IsEvent => node.IsEvent;
        public bool Contains(Vector2Int c) => c.x >= rect.xMin && c.x <= rect.xMax && c.y >= rect.yMin && c.y <= rect.yMax;
    }

    /// <summary>
    /// 把一個副本（DungeonDef）變成一張大地圖（純資料＋純邏輯，可以離線測試）：
    ///   ・照地點的連接（next；線性副本＝照順序）排成由西往東的幾欄：起點在最西邊，首領在最東邊。
    ///   ・每個地點是一塊 5×5 格的區域（首領 7×7）；地點之間用彎彎曲曲的走道連起來，走道上有岔路（死路盡頭可能藏寶箱）。
    ///   ・戰鬥地點：區域裡有一群守著的怪（營地），全部打倒＝清除；特殊事件：走進去開始，完成＝清除；寶箱：走進去就打開。
    ///   ・走道上有巡邏的怪（強度照出發的那個地點）。
    ///   ・同一個副本每次都長一樣（固定種子）。
    /// </summary>
    public static class DungeonMaze
    {
        public const int AreaSize = 5, BossSize = 7, GapX = 6, GapZ = 5, StartLead = 4, Margin = 3, PerBand = 5;
        public const int CampMonsters = 5, PatrolPerCorridor = 2;

        /// <summary>這個副本能不能改成大地圖（飛毯副本不改：使用者要求）</summary>
        public static bool Supports(DungeonDef d) => d != null && d.rooms.Count > 0 && !d.rooms.Exists(r => r.CarpetStation >= 0);

        public static ExploreMap Build(DungeonDef d)
        {
            int n = d.rooms.Count;
            // ---- 每個地點在第幾欄：從起點走過來最長要幾步（首領放最後一欄）
            var depth = new int[n];
            for (int i = 0; i < n; i++) depth[i] = -1;
            depth[0] = 0;
            for (int iter = 0; iter < n + 2; iter++)
                for (int i = 0; i < n; i++)
                {
                    if (depth[i] < 0) continue;
                    foreach (var e in d.ExitsOf(d.rooms[i])) { int j = d.rooms.IndexOf(e); if (j >= 0 && depth[j] < depth[i] + 1 && depth[i] + 1 <= n) depth[j] = depth[i] + 1; }
                }
            for (int i = 0; i < n; i++) if (depth[i] < 0) depth[i] = i;   // 走不到的（劇本寫錯）：照順序
            int boss = d.rooms.FindLastIndex(r => r.IsBoss); if (boss < 0) boss = n - 1;   // 最後一個首領（黎明之門有三個首領房：前兩個是中途的首領區域）
            int maxD = 0; for (int i = 0; i < n; i++) maxD = Math.Max(maxD, depth[i]);
            for (int i = 0; i < n; i++) if (i != boss && depth[i] >= depth[boss]) depth[boss] = depth[i] + 1;
            maxD = 0; for (int i = 0; i < n; i++) maxD = Math.Max(maxD, depth[i]);
            int cols = maxD + 1;
            var colItems = new List<int>[cols];
            for (int c = 0; c < cols; c++) colItems[c] = new List<int>();
            for (int i = 0; i < n; i++) colItems[depth[i]].Add(i);
            int rows = 1; foreach (var l in colItems) rows = Math.Max(rows, l.Count);

            // ---- 尺寸：欄太多（長長一條）就折成蛇形（每一帶 PerBand 欄，奇數帶由東往西），地圖比較接近方形
            int rowH = BossSize + GapZ, colStep = BossSize + GapX;
            int bands = (cols + PerBand - 1) / PerBand, perBand = Math.Min(cols, PerBand);
            int bandH = (rows - 1) * rowH + BossSize + GapZ + 1;
            int W = 1 + StartLead + perBand * colStep + 1, H = Margin * 2 + bands * bandH - (GapZ + 1);
            var m = new ExploreMap(W, H) { name = d.name, theme = ThemeName(d.ThemeOf(d.rooms[boss])), dungeon = d, nodeAreas = d.id == TrapGallery.Id };
            var rng = new System.Random(StableHash(d.id));

            // ---- 區域
            var areaOf = new ExArea[n];
            for (int c = 0; c < cols; c++)
            {
                int band = c / PerBand, slotX = c % PerBand;
                if (band % 2 == 1) slotX = perBand - 1 - slotX;   // 蛇形：奇數帶反過來
                int colX = 1 + StartLead + slotX * colStep;
                var items = colItems[c];
                for (int k = 0; k < items.Count; k++)
                {
                    int i = items[k]; int size = i == boss ? BossSize : AreaSize;
                    // 這一欄的區域上下平均分開
                    float slot = (rows - items.Count) * 0.5f + k;
                    int z0 = Margin + band * bandH + Mathf.RoundToInt(slot * rowH) + (BossSize - size) / 2;
                    int x0 = colX + (BossSize - size) / 2;
                    var a = new ExArea { index = i, node = d.rooms[i], rect = new RectInt(x0, z0, size - 1, size - 1) };
                    areaOf[i] = a;
                    for (int x = x0; x < x0 + size; x++) for (int z = z0; z < z0 + size; z++) m.cells[x, z] = ExCell.Floor;
                }
            }
            // 起點：最西邊，對齊第一個地點
            var first = areaOf[0];
            int sz = first.rect.yMin + first.rect.height / 2;
            m.start = new Vector2Int(1, sz);
            for (int x = 1; x < first.rect.xMin; x++) m.cells[x, sz] = ExCell.Floor;
            if (sz + 1 < H - 1) m.cells[1, sz + 1] = ExCell.Floor;
            if (sz - 1 > 0) m.cells[1, sz - 1] = ExCell.Floor;
            foreach (var a in areaOf)
            {
                a.center = m.World(a.rect.xMin + a.rect.width / 2, a.rect.yMin + a.rect.height / 2);
                a.radius = (a.rect.width * 0.5f + 0.2f) * ExploreMap.CellSize;
                m.areas.Add(a);
            }
            m.areas.Sort((p, q) => p.index.CompareTo(q.index));

            // ---- 走道：每條連接（A → B）從 A 朝向 B 那一邊的出口門出發，走到 B 朝向 A 那一邊的入口（Dijkstra 繞開其他區域）
            var corridorCells = new List<(Vector2Int c, int from)>();
            for (int i = 0; i < n; i++)
            {
                var a = areaOf[i];
                var exits = d.ExitsOf(d.rooms[i]);
                int lane = 0;
                foreach (var e in exits)
                {
                    int j = d.rooms.IndexOf(e); if (j < 0) continue;
                    var b = areaOf[j];
                    a.next.Add(j);
                    int off = exits.Count > 1 ? (lane - (exits.Count - 1) / 2) * 2 : 0;   // 往不同下一站的門錯開
                    var (door, outside) = Side(a, b.rect.center, off);
                    var (entry, _) = Side(b, a.rect.center, 0);
                    m.cells[door.x, door.y] = ExCell.Gate;
                    if (!a.exits.Contains(door)) a.exits.Add(door);
                    var path = Route(m, rng, outside, entry, a, b, areaOf, lane);
                    foreach (var c in path) { if (m.cells[c.x, c.y] == ExCell.Wall) m.cells[c.x, c.y] = ExCell.Floor; corridorCells.Add((c, i)); }
                    lane++;
                }
            }
            // ---- 岔路：走道旁邊挖幾條死路（盡頭偶爾有寶箱閃光點）
            int spurs = Mathf.Max(3, n), made = 0, sp = 0;
            for (int t = 0; t < 400 && made < spurs && corridorCells.Count > 0; t++)
            {
                var (c, from) = corridorCells[rng.Next(corridorCells.Count)];
                var dir = ExploreMap.Dirs[rng.Next(4)];
                int len = 2 + rng.Next(3);
                var cells = new List<Vector2Int>();
                var cur = c; bool ok = true;
                for (int s = 0; s < len; s++)
                {
                    cur += dir;
                    if (cur.x < 1 || cur.y < 1 || cur.x >= W - 1 || cur.y >= H - 1 || m.cells[cur.x, cur.y] != ExCell.Wall || m.AreaAt(cur) != null || NearArea(areaOf, cur)) { ok = false; break; }
                    // 不能貼著別的走道（不然變成捷徑）
                    int open = 0; foreach (var dd in ExploreMap.Dirs) { var nb = cur + dd; if (nb != cur - dir && m.At(nb.x, nb.y) != ExCell.Wall) open++; }
                    if (open > 0) { ok = false; break; }
                    cells.Add(cur);
                }
                if (!ok || cells.Count < 2) continue;
                foreach (var cc in cells) m.cells[cc.x, cc.y] = ExCell.Floor;
                made++;
                var end = cells[cells.Count - 1];
                if (rng.Next(3) == 0)
                    m.sparkles.Add(new ExSparkle { id = "dsp" + (++sp), kind = "chest", pos = m.World(end.x, end.y), title = "藏起來的寶箱", text = "走道盡頭的角落裡藏著一個寶箱！" });
            }

            // ---- 首領的王座（競技場 n＝4：走進去首領才出現）
            var ba = areaOf[boss];
            m.arenas.Add(new ExArena { n = 4, center = ba.center, radius = ba.radius, name = ba.Title });
            m.altar = ba.center + new Vector3(0f, 0f, (BossSize * 0.5f - 0.8f) * ExploreMap.CellSize);

            // ---- 怪物
            int id = 0;
            foreach (var a in areaOf)
            {
                if (a.IsChest || a.IsBoss || a.IsEvent || m.nodeAreas) continue;   // 寶箱、首領、特殊事件、千機迴廊的機關房：沒有守著的營地（地點自己出怪）
                int theme = d.ThemeOf(a.node);
                var offs = new[] { new Vector2Int(0, 0), new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(0, 2), new Vector2Int(0, -2) };
                int cx = a.rect.xMin + a.rect.width / 2, cz = a.rect.yMin + a.rect.height / 2;
                for (int k = 0; k < CampMonsters; k++)
                    m.monsters.Add(new ExMonster { id = ++id, kind = PickKind(theme, a.wave, rng), zone = a.index, area = a.index, wave = a.wave, camp = 100 + a.index, caller = k == 0, home = m.World(cx + offs[k].x, cz + offs[k].y) });
            }
            // 走道上巡邏的怪：每條走道幾隻（強度照出發的地點）
            var byFrom = new Dictionary<int, List<Vector2Int>>();
            foreach (var (c, from) in corridorCells) { if (!byFrom.TryGetValue(from, out var l)) byFrom[from] = l = new List<Vector2Int>(); l.Add(c); }
            foreach (var kv in byFrom)
            {
                var a = areaOf[kv.Key]; if (a.IsBoss) continue;
                int theme = d.ThemeOf(a.node);
                int w = Mathf.Max(1, a.wave > 0 ? a.wave : NearestWave(d, kv.Key));
                var list = kv.Value; int placed = 0;
                for (int t = 0; t < 200 && placed < PatrolPerCorridor * Mathf.Max(1, a.next.Count); t++)
                {
                    var c = list[rng.Next(list.Count)];
                    var p = m.World(c.x, c.y);
                    bool near = false;
                    foreach (var o in m.monsters) if (Vector3.Distance(o.home, p) < 6f) near = true;
                    foreach (var ar in areaOf) if (Vector3.Distance(ar.center, p) < ar.radius + 3f) near = true;
                    if (near) continue;
                    var path = new List<Vector3> { p }; var cur = c;
                    for (int s = 0; s < 4; s++)
                    {
                        var opts = new List<Vector2Int>();
                        foreach (var dd in ExploreMap.Dirs) { var nx = cur + dd; if (m.At(nx.x, nx.y) == ExCell.Floor && m.AreaAt(nx) == null && !path.Contains(m.World(nx.x, nx.y))) opts.Add(nx); }
                        if (opts.Count == 0) break;
                        cur = opts[rng.Next(opts.Count)]; path.Add(m.World(cur.x, cur.y));
                    }
                    m.monsters.Add(new ExMonster { id = ++id, kind = PickKind(theme, w, rng), zone = a.index, wave = w, home = p, patrol = path.ToArray() });
                    placed++;
                }
            }
            // 地形：牆和地面照副本主題（程式換色；正式美術之後由 Codex 做）
            for (int x = 0; x < W; x++) for (int z = 0; z < H; z++)
            {
                var ar = NearestAreaTheme(d, areaOf, m, x, z);
                m.terrain[x, z] = WallOf(ar); m.ground[x, z] = GroundOf(ar);
            }
            foreach (var a in areaOf)
                m.landmarks.Add(new ExLandmark { id = "area" + a.index, name = a.Title, kind = a.IsBoss ? "palace" : a.IsChest ? "camp" : "arena", rect = a.rect });
            m.landmarks.Add(new ExLandmark { id = "start", name = "副本入口", kind = "start", rect = new RectInt(1, sz - 1, 0, 2) });
            return m;
        }

        /// <summary>
        /// 走道：從 from 走到 to（Dijkstra 找路）：避開其他區域（含旁邊一格）、地圖邊緣；每格的花費加一點亂數，走道會彎彎曲曲；
        /// 已經是走道的格子花費比較高（盡量不要和別條走道併在一起，不然會變成繞過門的捷徑）。回傳經過的格子
        /// </summary>
        static List<Vector2Int> Route(ExploreMap m, System.Random rng, Vector2Int from, Vector2Int to, ExArea a, ExArea b, ExArea[] all, int lane)
        {
            int W = m.W, H = m.H;
            var cost = new float[W, H];
            for (int x = 0; x < W; x++) for (int z = 0; z < H; z++)
            {
                var c = new Vector2Int(x, z);
                if (x < 1 || z < 1 || x >= W - 1 || z >= H - 1 || Blocked(all, c, a, b) || a.Contains(c) || b.Contains(c)) { cost[x, z] = -1f; continue; }
                cost[x, z] = 1f + (float)rng.NextDouble() * 2.2f + (m.cells[x, z] != ExCell.Wall ? 4f : 0f);
            }
            cost[from.x, from.y] = 1f; cost[to.x, to.y] = 1f;
            var dist = new float[W, H]; var prev = new Vector2Int[W, H];
            for (int x = 0; x < W; x++) for (int z = 0; z < H; z++) dist[x, z] = float.MaxValue;
            dist[from.x, from.y] = 0f;
            var open = new List<Vector2Int> { from };
            while (open.Count > 0)
            {
                int bi = 0; for (int i = 1; i < open.Count; i++) if (dist[open[i].x, open[i].y] < dist[open[bi].x, open[bi].y]) bi = i;
                var c = open[bi]; open.RemoveAt(bi);
                if (c == to) break;
                foreach (var d in ExploreMap.Dirs)
                {
                    var nb = c + d;
                    if (nb.x < 0 || nb.y < 0 || nb.x >= W || nb.y >= H || cost[nb.x, nb.y] < 0f) continue;
                    float nd = dist[c.x, c.y] + cost[nb.x, nb.y];
                    if (nd < dist[nb.x, nb.y]) { if (dist[nb.x, nb.y] == float.MaxValue) open.Add(nb); dist[nb.x, nb.y] = nd; prev[nb.x, nb.y] = c; }
                }
            }
            var path = new List<Vector2Int>();
            if (dist[to.x, to.y] == float.MaxValue) { path.Add(from); return path; }   // 找不到（不該發生）：測試會抓到
            for (var c = to; c != from; c = prev[c.x, c.y]) path.Add(c);
            path.Add(from); path.Reverse();
            return path;
        }

        /// <summary>區域 a 朝向 target 那一邊的門：門的格子（貼著區域外面）與門外再一格（走道從這裡開始）；off＝沿著那一邊錯開幾格</summary>
        static (Vector2Int door, Vector2Int outside) Side(ExArea a, Vector2 target, int off)
        {
            var r = a.rect; var c = r.center; float dx = target.x - c.x, dz = target.y - c.y;
            int cx = r.xMin + r.width / 2, cz = r.yMin + r.height / 2;
            if (Mathf.Abs(dx) >= Mathf.Abs(dz))
            {
                int z = Mathf.Clamp(cz + off, r.yMin, r.yMax);
                return dx >= 0 ? (new Vector2Int(r.xMax + 1, z), new Vector2Int(r.xMax + 2, z)) : (new Vector2Int(r.xMin - 1, z), new Vector2Int(r.xMin - 2, z));
            }
            int x = Mathf.Clamp(cx + off, r.xMin, r.xMax);
            return dz >= 0 ? (new Vector2Int(x, r.yMax + 1), new Vector2Int(x, r.yMax + 2)) : (new Vector2Int(x, r.yMin - 1), new Vector2Int(x, r.yMin - 2));
        }

        static bool Blocked(ExArea[] all, Vector2Int c, ExArea a, ExArea b)
        {
            foreach (var ar in all) { if (ar == a || ar == b) continue; var r = ar.rect; if (c.x >= r.xMin - 1 && c.x <= r.xMax + 1 && c.y >= r.yMin - 1 && c.y <= r.yMax + 1) return true; }
            return false;
        }
        static bool NearArea(ExArea[] all, Vector2Int c)
        {
            foreach (var ar in all) { var r = ar.rect; if (c.x >= r.xMin - 1 && c.x <= r.xMax + 1 && c.y >= r.yMin - 1 && c.y <= r.yMax + 1) return true; }
            return false;
        }

        static int NearestWave(DungeonDef d, int i) { for (int k = i; k < d.rooms.Count; k++) if (d.rooms[k].wave > 0) return d.rooms[k].wave; for (int k = i; k >= 0; k--) if (d.rooms[k].wave > 0) return d.rooms[k].wave; return 1; }

        static int NearestAreaTheme(DungeonDef d, ExArea[] all, ExploreMap m, int x, int z)
        {
            float best = float.MaxValue; int th = 0;
            foreach (var a in all) { var c = a.rect.center; float dd = (c.x - x) * (c.x - x) + (c.y - z) * (c.y - z); if (dd < best) { best = dd; th = d.ThemeOf(a.node); } }
            return th;
        }

        /// <summary>主題 → 牆的地形（熔岩＝岩壁、森林＝樹叢、海上＝湖水邊的岩石、角鬥場＝木牆、山道＝岩壁）</summary>
        public static ExTerrain WallOf(int theme) => theme == 0 ? ExTerrain.Cave : theme == 1 ? ExTerrain.Thicket : theme == 2 ? ExTerrain.Rock : theme == 3 ? ExTerrain.Palisade : ExTerrain.Rock;
        public static ExGround GroundOf(int theme) => theme == 0 ? ExGround.Cave : theme == 1 ? ExGround.Grass : theme == 2 ? ExGround.Shore : theme == 3 ? ExGround.Stone : ExGround.Dirt;
        public static string ThemeName(int theme) => theme == 0 ? "volcano" : theme == 1 ? "jungle" : theme == 2 ? "sea" : theme == 3 ? "arena" : "canyon";

        /// <summary>這個主題、這個強度的小怪種類（跟一般關卡出怪的種類一致）</summary>
        public static int PickKind(int theme, int wave, System.Random rng)
        {
            var l = new List<int>();
            for (int k = 0; k <= 18; k++) if (Battle.KindInTheme(k, theme, wave)) l.Add(k);
            if (l.Count == 0) return theme == 1 ? 10 : theme == 2 ? 13 : 0;
            return l[rng.Next(l.Count)];
        }

        static int StableHash(string s) { unchecked { int h = 23; foreach (char ch in s ?? "") h = h * 31 + ch; return h & 0x7fffffff; } }
    }
}
