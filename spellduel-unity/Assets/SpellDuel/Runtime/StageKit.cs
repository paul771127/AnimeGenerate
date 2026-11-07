using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 場景生成器的 3D 地形零件（scenegen 工單 003：Resources/SceneKit/volcano_cel_v1/）接到副本大地圖（熔岩區）：
    ///   ・牆格：每一面臨路的邊放一段直岩壁（可走的那面朝路）；凸角放 corner_out、凹角放 corner_in。
    ///   ・門格：岩門（門框＋門板）；區域清除後門板沉進地底。
    ///   ・首領區域：最裡面那一邊放岩洞入口（首領從洞裡出來的感覺）；地圖外圈放遠景岩塊。
    /// 光影烘在顏色圖裡 → 用不打光的 SpellDuel/StageKit（加發光遮罩、遠處的霧色）；同網格的零件用 GPU instancing 合併繪製。
    /// 沒有零件（或不是熔岩區）就照舊用程式畫的牆（ExploreArt.CreateWall／CreateDungeonGate）。
    /// </summary>
    public static class StageKit
    {
        const string Dir = "SceneKit/volcano_cel_v1/";
        static bool loaded, ok;
        static Material mainMat, backMat, groundMat;
        static Texture2D groundAsh, groundCrust;
        static readonly Dictionary<string, GameObject> parts = new Dictionary<string, GameObject>();
        static readonly string[] StraightV = { "a", "b", "c", "d" }, CornerV = { "a", "b", "c" };
        static readonly Vector2Int[] Dirs4 = { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1) };

        static GameObject Part(string name)
        {
            if (!parts.TryGetValue(name, out var g)) { g = Resources.Load<GameObject>(Dir + name); parts[name] = g; }
            return g;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var sh = Resources.Load<Shader>("Shaders/StageKit");
            var alb = Resources.Load<Texture2D>(Dir + "volcano_cel_albedo");
            var em = Resources.Load<Texture2D>(Dir + "volcano_cel_emissive");
            var back = Resources.Load<Texture2D>(Dir + "volcano_cel_backdrop");
            ok = sh != null && sh.isSupported && alb && Part("wall_straight_a") && Part("wall_corner_out_a") && Part("wall_corner_in_a");
            if (!ok) return;
            mainMat = new Material(sh) { name = "Stage kit (volcano)", mainTexture = alb, enableInstancing = true };
            if (em) mainMat.SetTexture("_EmissionMap", em);
            mainMat.SetFloat("_Glow", 1.8f);   // 裂縫的熔岩光（會慢慢脈動）
            mainMat.SetFloat("_UnderGlowStrength", 0.55f);   // 使用者要求岩壁要有熔岩感：牆腳有地上熔岩照上來的暖橘反光（StageKit.shader）
            // 手繪地面（賽璐璐版附的火山灰地面，每 3m 重複一次）
            groundAsh = Resources.Load<Texture2D>(Dir + "cel_ground_ash");
            groundCrust = Resources.Load<Texture2D>(Dir + "cel_ground_crust");
            var gsh = Resources.Load<Shader>("Shaders/StageKitGround");
            if (groundAsh && gsh != null && gsh.isSupported) { groundMat = new Material(gsh) { name = "Stage kit ground (volcano)", mainTexture = groundAsh }; groundMat.SetFloat("_Bright", 1.18f); }   // 可以走的路：亮一點的火山灰
            backMat = new Material(sh) { name = "Stage kit backdrop (volcano)", mainTexture = back ? back : alb, enableInstancing = true };
            backMat.SetFloat("_Glow", 0f); backMat.SetFloat("_HazeNear", 25f); backMat.SetFloat("_HazeFar", 90f); backMat.SetFloat("_HazeMax", 0.75f);
        }

        /// <summary>這一格用不用 3D 零件：副本大地圖的熔岩區（千機迴廊、城堡不用）</summary>
        public static bool UseAt(ExploreMap map, int x, int z)
        {
            if (map == null || map.dungeon == null || map.nodeAreas || map.theme == "castle") return false;
            Load();
            return ok && ExploreArt.DungeonThemeAt(map, x, z) == 0;
        }

        /// <summary>整張地圖用賽璐璐版的火山（天空、霧色、地面都跟著換）：副本大地圖的熔岩主題</summary>
        public static bool CelMap(ExploreMap map)
        {
            if (map == null || map.dungeon == null || map.nodeAreas || map.theme != "volcano") return false;
            Load();
            return ok && groundAsh;
        }

        /// <summary>賽璐璐版的天空：地平線附近是帶一點火山暖色的薄霧，上面是清透的青藍色</summary>
        public static readonly Color CelSkyLow = new Color(0.80f, 0.66f, 0.58f), CelSkyHigh = new Color(0.36f, 0.58f, 0.68f), CelGround = new Color(0.36f, 0.35f, 0.39f);

        /// <summary>這一格地面的材質（熔岩區用手繪地面；沒有＝null，照舊）</summary>
        public static Material GroundAt(ExploreMap map, int theme) => theme == 0 && CelMap(map) ? groundMat : null;
        /// <summary>底下整片（不能走的地方）：暗的凝固熔岩地殼，跟可以走的火山灰路分得清楚</summary>
        public static Texture2D GroundTexture => groundCrust ? groundCrust : groundAsh;

        /// <summary>遠處的霧色跟著地圖的天色（ExploreArt.HorizonColors）</summary>
        public static void SetHaze(ExploreMap map)
        {
            Load(); if (!ok || map == null) return;
            ExploreArt.HorizonColors(map, out var g, out var s);
            var h = ExploreArt.HazeOf(g, s);
            mainMat.SetColor("_Haze", h); backMat.SetColor("_Haze", h);
            if (groundMat) groundMat.SetColor("_Haze", h);
        }

        /// <summary>岩壁的高度倍率（使用者要求：不能走的地方岩壁至少要人的 2 倍高 → 最矮的鞍部約 3.9m、中間 7～9m）</summary>
        const float WallScaleY = 1.55f;
        /// <summary>岩壁可走的那面原本往路上凸 0.55m：往牆裡退，不擋路、走路邊時也不會鑽進岩石裡（使用者回報走一走有奇怪的遮擋物）</summary>
        const float WallBack = 0.6f;

        static GameObject Spawn(Transform parent, string name, Vector3 pos, Quaternion rot, Material m, Vector3? scale = null)
        {
            var src = Part(name); if (!src) return null;
            var g = Object.Instantiate(src, parent, false);
            g.name = name;
            g.transform.localPosition = pos; g.transform.localRotation = rot;
            if (scale.HasValue) g.transform.localScale = Vector3.Scale(g.transform.localScale, scale.Value);
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = m; r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            return g;
        }

        /// <summary>local +X 朝 a、+Z 朝 b（兩個互相垂直的水平方向）的旋轉</summary>
        static Quaternion Corner(Vector3 a, Vector3 b) =>
            Vector3.Dot(Vector3.Cross(Vector3.up, b), a) > 0.5f ? Quaternion.LookRotation(b, Vector3.up) : Quaternion.LookRotation(a, Vector3.up);

        /// <summary>牆格要不要畫：旁邊（含斜角，凹角要用）有路</summary>
        public static bool WallVisible(ExploreMap map, int x, int z)
        {
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if ((dx != 0 || dz != 0) && map.At(x + dx, z + dz) != ExCell.Wall) return true;
            return false;
        }

        /// <summary>一個牆格：原點在格子中心（呼叫的地方放到 map.World(x, z)）</summary>
        public static GameObject CreateWall(ExploreMap map, int x, int z)
        {
            var root = new GameObject("Kit wall");
            var t = root.transform;
            float h = ExploreMap.CellSize * 0.5f;
            bool Wall(int dx, int dz) => map.At(x + dx, z + dz) == ExCell.Wall;
            int hash = Mathf.Abs((x * 73856093) ^ (z * 19349663));
            for (int i = 0; i < 4; i++)
            {
                var d = Dirs4[i]; if (Wall(d.x, d.y)) continue;
                var dv = new Vector3(d.x, 0f, d.y);
                Spawn(t, "wall_straight_" + StraightV[(hash + i) % 4], dv * (h - WallBack), Quaternion.LookRotation(dv, Vector3.up), mainMat, new Vector3(1.1f, WallScaleY, 1f));   // 可走的那面（+Z）朝路；寬一點補退後留下的縫
            }
            for (int i = 0; i < 4; i++)
            {
                var a = Dirs4[i]; var b = Dirs4[(i + 1) % 4];
                bool oa = !Wall(a.x, a.y), ob = !Wall(b.x, b.y), diag = !Wall(a.x + b.x, a.y + b.y);
                string kind = oa && ob ? "wall_corner_out_" : !oa && !ob && diag ? "wall_corner_in_" : null;
                if (kind == null) continue;
                var va = new Vector3(a.x, 0f, a.y); var vb = new Vector3(b.x, 0f, b.y);
                bool outer = kind == "wall_corner_out_";
                Spawn(t, kind + CornerV[(hash + 7 * i) % 3], (va + vb) * (h - (outer ? WallBack : WallBack * 0.5f)), Corner(va, vb), mainMat, new Vector3(1f, WallScaleY, 1f));
            }
            return root;
        }

        // ---------------------------------------------------------------- 岩門
        static ExploreMap sunkMap;
        static readonly HashSet<Vector2Int> sunk = new HashSet<Vector2Int>();

        sealed class KitGate : MonoBehaviour
        {
            public Transform door; public bool open; public Vector2Int cell;
            void Update()
            {
                if (!door || !open) return;
                var p = door.localPosition;
                p.y = Mathf.MoveTowards(p.y, -5.9f, Time.deltaTime * 4f);
                door.localPosition = p;
                if (p.y <= -5.85f) { door.gameObject.SetActive(false); sunk.Add(cell); enabled = false; }
            }
        }

        /// <summary>一個門格：門框＋門板；門開了門板沉下去（已經沉過的重建時直接藏起來）</summary>
        public static GameObject CreateGate(ExploreMap map, int x, int z)
        {
            if (sunkMap != map) { sunkMap = map; sunk.Clear(); }
            var root = new GameObject("Kit gate");
            string v = ((x + z) & 1) == 0 ? "a" : "b";
            // 開口原本只有 2.2m（走道 4m）：橫向放大，開口約 3.5m，走路邊也不會撞進門框；高度跟著岩壁加高
            var gs = new Vector3(1.6f, 1.35f, 1f);
            Spawn(root.transform, "gate_frame_" + v, Vector3.zero, Quaternion.identity, mainMat, gs);
            var door = Spawn(root.transform, "gate_door_" + v, Vector3.zero, Quaternion.identity, mainMat, gs);
            var cell = new Vector2Int(x, z);
            bool open = map.GateOpen(x, z);
            if (door != null && open && sunk.Contains(cell)) door.SetActive(false);
            else if (door != null) { var g = root.AddComponent<KitGate>(); g.door = door.transform; g.open = open; g.cell = cell; }
            return root;
        }

        // ---------------------------------------------------------------- 首領的岩洞、遠景岩塊
        /// <summary>首領區域最裡面那一邊放岩洞入口（開口朝房間），地圖外圈放遠景岩塊；parent 跟著地圖原點</summary>
        public static void Decorate(ExploreMap map, Transform parent)
        {
            Load(); if (!ok || map == null || parent == null) return;
            SetHaze(map);
            var boss = map.areas.FindLast(a => a.IsBoss);
            if (boss != null && Part("cave_entrance_a"))
            {
                var from = map.areas.Find(a => a.next.Contains(boss.index));
                var d = from != null ? boss.center - from.center : Vector3.right; d.y = 0f;
                d = Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
                float half = (boss.rect.width + 1) * ExploreMap.CellSize * 0.5f;
                Spawn(parent, "cave_entrance_a", boss.center + d * half, Quaternion.LookRotation(-d, Vector3.up), mainMat);   // 洞身往房間外面延伸（牆裡）
            }
            if (Part("backdrop_rock_a"))
            {
                var lo = map.World(0, 0); var hi = map.World(map.W - 1, map.H - 1);
                var c = (lo + hi) * 0.5f; c.y = 0f;
                float rx = Mathf.Abs(hi.x - lo.x) * 0.5f + 14f, rz = Mathf.Abs(hi.z - lo.z) * 0.5f + 14f;
                var rng = new System.Random(911);
                for (int i = 0; i < 20; i++)
                {
                    float a = (i + (float)rng.NextDouble() * 0.5f) / 20f * Mathf.PI * 2f, k = 1f + (float)rng.NextDouble() * 0.35f;
                    var p = c + new Vector3(Mathf.Cos(a) * rx * k, 0f, Mathf.Sin(a) * rz * k);
                    var look = c - p; look.y = 0f;
                    Spawn(parent, "backdrop_rock_" + "abcd"[i % 4], p, Quaternion.LookRotation(look.normalized, Vector3.up), backMat);   // 正面朝地圖
                }
            }
        }
    }
}
