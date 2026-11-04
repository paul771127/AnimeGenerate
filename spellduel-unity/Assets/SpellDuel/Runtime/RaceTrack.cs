using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 賽跑玩法的跑道（純資料＋純邏輯）：一條由西往東、彎彎曲曲的長走道（一格 4m），中途有幾段分岔（繞遠路但有加速道具／抄近路但有泥沼），
    /// 還有幾條走不通的死路；路上有擋路的岩石、木箱（ExploreMap 的景物，會擋住衝刺）、泥沼（減速）、加速道具。
    /// 同一關每次都一樣（固定種子）。地圖接到副本大地圖的畫面（ExploreMap.dungeon＝這一關的小副本），區域只有起點、終點兩塊（都不會觸發戰鬥）。
    /// </summary>
    public class RaceTrack
    {
        public const int W = 62, H = 15;
        public readonly List<Vector2Int> main = new List<Vector2Int>();
        /// <summary>分岔：從主線第 from 格離開、繞到第 to 格接回（cells 不含兩端）</summary>
        public readonly List<(int from, int to, List<Vector2Int> cells, bool boosty)> forks = new List<(int, int, List<Vector2Int>, bool)>();
        public readonly List<Vector3> mud = new List<Vector3>();
        public readonly List<Vector3> boosts = new List<Vector3>();
        /// <summary>每一格（主線、分岔）跑者要走的位置（擋路的景物在哪一邊，就從另一邊過）</summary>
        public readonly Dictionary<Vector2Int, Vector3> lane = new Dictionary<Vector2Int, Vector3>();
        /// <summary>每一格大約在主線的第幾格（算名次用；分岔照前後兩端內插）</summary>
        public readonly Dictionary<Vector2Int, float> progressOf = new Dictionary<Vector2Int, float>();
        public RectInt finish;
        public ExploreMap map;
        public float Length => main.Count;

        public const float MudRadius = 1.3f, BoostRadius = 0.85f;

        public static RaceTrack Build(DungeonDef d, int theme, int seed)
        {
            var t = new RaceTrack();
            var rng = new System.Random(seed);
            var m = new ExploreMap(W, H) { name = d.name, theme = DungeonMaze.ThemeName(theme), dungeon = d };
            t.map = m;
            void Carve(Vector2Int c) { if (m.InGrid(c.x, c.y) && c.x > 0 && c.y > 0 && c.x < W - 1 && c.y < H - 1) m.cells[c.x, c.y] = ExCell.Floor; }

            // ---- 主線：從 (2, 7) 往東，每走 3～6 格就上下拐 2～3 格
            int x = 2, z = H / 2;
            m.start = new Vector2Int(x, z);
            t.main.Add(new Vector2Int(x, z));
            int endX = W - 5;
            while (x < endX)
            {
                int run = rng.Next(3, 7);
                for (int i = 0; i < run && x < endX; i++) { x++; t.main.Add(new Vector2Int(x, z)); }
                if (x >= endX) break;
                int dz = rng.Next(2, 4) * (rng.Next(2) == 0 ? 1 : -1);
                int nz = Mathf.Clamp(z + dz, 3, H - 4);
                int step = nz > z ? 1 : -1;
                while (z != nz) { z += step; t.main.Add(new Vector2Int(x, z)); }
            }
            foreach (var c in t.main) Carve(c);
            // 起點、終點：3×3 的空地
            for (int ix = 1; ix <= 3; ix++) for (int iz = m.start.y - 1; iz <= m.start.y + 1; iz++) Carve(new Vector2Int(ix, iz));
            var last = t.main[t.main.Count - 1];
            t.finish = new RectInt(last.x + 1, last.y - 1, 2, 2);
            for (int ix = last.x; ix <= last.x + 3; ix++) for (int iz = last.y - 1; iz <= last.y + 1; iz++) Carve(new Vector2Int(ix, iz));

            for (int i = 0; i < t.main.Count; i++) t.progressOf[t.main[i]] = i;

            // ---- 分岔：3 段（一段繞遠路＋加速道具、其他抄近路＋泥沼由主線負責）
            int seg = t.main.Count / 4;
            for (int f = 0; f < 3; f++)
            {
                int from = seg * f + rng.Next(4, Mathf.Max(5, seg - 8)), to = Mathf.Min(t.main.Count - 6, from + rng.Next(9, 14));
                if (to - from < 6) continue;
                var a = t.main[from]; var b = t.main[to];
                int side = (a.y + b.y) / 2 > H / 2 ? -1 : 1;
                int off = side * rng.Next(3, 5);
                int rz = Mathf.Clamp(Mathf.Max(a.y, b.y) * (side > 0 ? 1 : 0) + Mathf.Min(a.y, b.y) * (side < 0 ? 1 : 0) + off, 1, H - 2);
                var cells = new List<Vector2Int>();
                var cur = a;
                while (cur.y != rz) { cur.y += rz > cur.y ? 1 : -1; cells.Add(cur); }
                while (cur.x != b.x) { cur.x += b.x > cur.x ? 1 : -1; cells.Add(cur); }
                while (cur.y != b.y) { cur.y += b.y > cur.y ? 1 : -1; if (cur != b) cells.Add(cur); }
                cells.RemoveAll(c => c == b);
                foreach (var c in cells) Carve(c);
                for (int i = 0; i < cells.Count; i++) if (!t.progressOf.ContainsKey(cells[i])) t.progressOf[cells[i]] = Mathf.Lerp(from, to, (i + 1f) / (cells.Count + 1f));
                t.forks.Add((from, to, cells, f != 1));
            }
            // ---- 死路：從主線往旁邊挖 2～4 格（不通）
            for (int k = 0; k < 5; k++)
            {
                int i = rng.Next(6, t.main.Count - 6); var c = t.main[i];
                int dir = rng.Next(2) == 0 ? 1 : -1, len = rng.Next(2, 5);
                for (int s = 1; s <= len; s++) { var cc = new Vector2Int(c.x, c.y + dir * s); if (cc.y < 1 || cc.y > H - 2) break; Carve(cc); if (!t.progressOf.ContainsKey(cc)) t.progressOf[cc] = i; }
            }

            // ---- 地形
            for (int ix = 0; ix < W; ix++) for (int iz = 0; iz < H; iz++) { m.terrain[ix, iz] = DungeonMaze.WallOf(theme); m.ground[ix, iz] = DungeonMaze.GroundOf(theme); }

            // ---- 每一格跑者走的位置＋路上的東西
            Vector3 Dir(List<Vector2Int> path, int i)
            {
                var p0 = path[Mathf.Max(0, i - 1)]; var p1 = path[Mathf.Min(path.Count - 1, i + 1)];
                var dd = new Vector3(p1.x - p0.x, 0f, p1.y - p0.y); return dd.sqrMagnitude > 0f ? dd.normalized : Vector3.right;
            }
            bool Straight(List<Vector2Int> path, int i) => i > 0 && i < path.Count - 1 && (path[i] - path[i - 1]) == (path[i + 1] - path[i]);
            void Dress(List<Vector2Int> path, int startI, bool boosty, bool isMain)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    var c = path[i]; var w = m.World(c.x, c.y);
                    t.lane[c] = w;
                    if (isMain && (i < 5 || i > path.Count - 5)) continue;   // 起點、終點附近不放
                    if (!Straight(path, i)) continue;
                    var fwd = Dir(path, i); var right = new Vector3(fwd.z, 0f, -fwd.x);
                    int r = rng.Next(100);
                    if (r < (isMain ? 22 : 12))
                    {
                        // 擋路：岩石或木箱放在左、中、右其中一邊；跑者從空的那邊過
                        int sideK = rng.Next(3) - 1; float lat = sideK * 1.1f;
                        m.AddProp(rng.Next(2) == 0 ? "boulder" : "crate", w + right * lat, sideK == 0 ? 0.75f : 0.65f, rng.Next(3));
                        t.lane[c] = w + right * (sideK == 0 ? (rng.Next(2) == 0 ? 1.35f : -1.35f) : -sideK * 1.1f);
                    }
                    else if (r < (isMain ? 34 : (boosty ? 18 : 30))) t.mud.Add(w + right * ((float)rng.NextDouble() - 0.5f) * 1.2f);
                    else if (r < (isMain ? 40 : (boosty ? 40 : 34))) t.boosts.Add(w + right * ((float)rng.NextDouble() - 0.5f) * 1.6f);
                }
            }
            Dress(t.main, 0, false, true);
            foreach (var f in t.forks) Dress(f.cells, f.from, f.boosty, false);

            // ---- 區域（只給畫面與流程用：起點、終點都已經「進過、清除」，不會觸發戰鬥）
            var startA = new ExArea { index = 0, node = d.rooms[0], rect = new RectInt(1, m.start.y - 1, 2, 2), entered = true, cleared = true };
            var finA = new ExArea { index = 1, node = d.rooms[1], rect = t.finish, entered = true, cleared = true };
            foreach (var a in new[] { startA, finA })
            {
                a.center = m.World(a.rect.xMin + 1, a.rect.yMin + 1); a.radius = 1.6f * ExploreMap.CellSize;
                m.areas.Add(a);
            }
            m.arenas.Add(new ExArena { n = 4, center = finA.center, radius = finA.radius, name = "終點", cleared = true });
            m.landmarks.Add(new ExLandmark { id = "start", name = "起跑線", kind = "start", rect = startA.rect });
            m.landmarks.Add(new ExLandmark { id = "finish", name = "終點", kind = "palace", rect = t.finish });
            return t;
        }

        /// <summary>一位跑者的路線：主線，遇到分岔時照 pick 決定要不要走岔路</summary>
        public List<Vector3> RouteFor(System.Random rng)
        {
            var route = new List<Vector3>();
            int i = 0;
            while (i < main.Count)
            {
                var fk = forks.Find(f => f.from == i);
                route.Add(lane.TryGetValue(main[i], out var p) ? p : map.World(main[i].x, main[i].y));
                if (fk.cells != null && rng.Next(2) == 0)
                {
                    foreach (var c in fk.cells) route.Add(lane.TryGetValue(c, out var q) ? q : map.World(c.x, c.y));
                    i = fk.to; continue;
                }
                i++;
            }
            route.Add(map.World(finish.xMin + 1, finish.yMin + 1));
            return route;
        }

        /// <summary>這一點大約跑到第幾格（名次用）</summary>
        public float ProgressAt(Vector3 p)
        {
            var c = map.Cell(p);
            if (progressOf.TryGetValue(c, out var v)) return v;
            float best = 0f, bd = float.MaxValue;
            foreach (var kv in progressOf) { float d = (kv.Key - c).sqrMagnitude; if (d < bd) { bd = d; best = kv.Value; } }
            return best;
        }
        public bool AtFinish(Vector3 p) { var c = map.Cell(p); return c.x >= finish.xMin && c.x <= finish.xMax && c.y >= finish.yMin && c.y <= finish.yMax; }
    }
}
