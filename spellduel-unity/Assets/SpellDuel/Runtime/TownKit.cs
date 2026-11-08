using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 場景生成器做的 3D 城鎮（scenegen 工單 007：Resources/TownKit/town0_cel_v1/，炎之里）：
    /// 照 town_layout.json 擺建築、擺設、地面、外圍山壁和遠景；碰撞用多邊形（牆是 U 形，室內可以走進去）；
    /// NPC 的站位、面向照配置（鐵匠、店員、公會長在屋裡的櫃台／鐵砧後面），不閒晃。
    /// 光影烘在貼圖裡 → 用不打光的 SpellDuel/StageKit（不發光、不加熔岩反光）。
    /// 沒有這份配置（或不是城鎮 0）＝null，TownWalk 照舊用程式畫的村子（TownArt）。
    /// </summary>
    public sealed class TownKit
    {
        public class NpcSpot { public string role, name, inside; public Vector2 pos; public float yaw; }
        public class SignSpot { public string kind; public Vector3 pos; }

        public readonly List<Vector2[]> colliders = new List<Vector2[]>();
        public readonly List<NpcSpot> npcs = new List<NpcSpot>();
        public readonly List<SignSpot> signs = new List<SignSpot>();
        public Vector2 warp = new Vector2(5f, 0.5f);

        static readonly Dictionary<int, TownKit> cache = new Dictionary<int, TownKit>();
        static Material archMat, detailMat, backMat;
        static readonly Dictionary<(Material, int), Material> layered = new Dictionary<(Material, int), Material>();
        /// <summary>地面要往後推幾層（使用者回報村子多處貼圖閃爍：城鎮地面、路面、室內地板疊在幾乎同一個高度）</summary>
        static int GroundLayer(string file) => file == null ? 0 : file.Contains("ground_outside") ? 3 : file.Contains("ground_town") ? 2 : file.Contains("ground_paths") ? 1 : 0;
        static Material Layer(Material m, int layer)
        {
            if (layer <= 0 || !m) return m;
            if (layered.TryGetValue((m, layer), out var lm) && lm) return lm;
            lm = new Material(m) { name = m.name + " (ground " + layer + ")" };
            lm.SetFloat("_OffsetFactor", layer); lm.SetFloat("_OffsetUnits", layer * 4f);
            lm.SetFloat("_LitHalf", 1f);   // 地面只用圖集的受光半邊（不會一塊暗一塊亮）
            layered[(m, layer)] = lm; return lm;
        }
        string dir; object layout;

        static readonly string[] Folders = { "TownKit/town0_cel_v1/" };

        /// <summary>這個城鎮有沒有生成器的版本（目前只有炎之里）</summary>
        public static TownKit For(int town)
        {
            if (cache.TryGetValue(town, out var k)) return k;
            k = null;
            if (town >= 0 && town < Folders.Length)
            {
                var ta = Resources.Load<TextAsset>(Folders[town] + "town_layout");
                var sh = Resources.Load<Shader>("Shaders/StageKit");
                var arch = Resources.Load<Texture2D>(Folders[town] + "town0_arch_albedo");
                if (ta != null && sh != null && sh.isSupported && arch)
                {
                    var o = MiniJson.Parse(ta.text);
                    if (o != null) { k = new TownKit { dir = Folders[town], layout = o }; k.Read(); }
                    if (!archMat)
                    {
                        Material M(string tex, float haze)
                        {
                            var m = new Material(sh) { name = "Town kit " + tex, mainTexture = Resources.Load<Texture2D>(Folders[town] + tex), enableInstancing = true };
                            m.SetFloat("_Glow", 0f); m.SetFloat("_UnderGlowStrength", 0f);
                            m.SetColor("_Haze", new Color(0.8f, 0.66f, 0.58f)); m.SetFloat("_HazeNear", 30f); m.SetFloat("_HazeFar", 110f); m.SetFloat("_HazeMax", haze);
                            return m;
                        }
                        archMat = M("town0_arch_albedo", 0.25f); detailMat = M("town0_detail_albedo", 0.25f); backMat = M("town0_backdrop_albedo", 0.55f);
                    }
                }
            }
            cache[town] = k;
            return k;
        }

        static Vector2 V2(object o) { var l = o as List<object>; return l != null && l.Count >= 2 ? new Vector2((float)(double)l[0], (float)(double)l[1]) : Vector2.zero; }

        void Read()
        {
            foreach (var c in MiniJson.Arr(layout, "colliders") ?? new List<object>())
            {
                var poly = MiniJson.Arr(c, "polygon"); if (poly == null || poly.Count < 3) continue;
                var pts = new Vector2[poly.Count]; for (int i = 0; i < pts.Length; i++) pts[i] = V2(poly[i]);
                colliders.Add(pts);
            }
            foreach (var n in MiniJson.Arr(layout, "npcs") ?? new List<object>())
            {
                var d = n as Dictionary<string, object>;
                npcs.Add(new NpcSpot { role = MiniJson.Str(n, "role"), name = MiniJson.Str(n, "name"), inside = MiniJson.Str(n, "inside"), pos = V2(d["pos"]), yaw = MiniJson.Num(n, "yaw") });
            }
            foreach (var b in MiniJson.Arr(layout, "buildings") ?? new List<object>())
            {
                var s = MiniJson.Arr(b, "sign");
                if (s != null && s.Count >= 3) signs.Add(new SignSpot { kind = MiniJson.Str(b, "id"), pos = new Vector3((float)(double)s[0], (float)(double)s[1], (float)(double)s[2]) });
            }
            if (MiniJson.Has(layout, "warp")) warp = V2(((Dictionary<string, object>)layout)["warp"]);
        }

        GameObject Place(Transform root, string file, Vector2 pos, float yaw, float scale, float height, bool backdrop)
        {
            if (string.IsNullOrEmpty(file)) return null;
            var src = Resources.Load<GameObject>(dir + System.IO.Path.GetFileNameWithoutExtension(file));
            if (!src) return null;
            var g = Object.Instantiate(src, root, false);
            g.name = file;
            g.transform.localPosition = new Vector3(pos.x, height, pos.y);
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (Mathf.Abs(scale - 1f) > 1e-4f) g.transform.localScale = g.transform.localScale * scale;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                int layer = GroundLayer(file);
                for (int i = 0; i < mats.Length; i++) mats[i] = Layer(backdrop ? backMat : i == 0 ? archMat : detailMat, layer);   // 槽 0＝建築圖集、槽 1＝細節圖集；遠景全部用遠景圖集；地面往後推
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            return g;
        }

        void PlaceAll(Transform root, List<object> list, bool backdrop)
        {
            if (list == null) return;
            foreach (var e in list)
            {
                var d = e as Dictionary<string, object>; if (d == null || !d.ContainsKey("pos")) continue;
                bool back = backdrop || MiniJson.Str(e, "atlas") == "backdrop";
                Place(root, MiniJson.Str(e, "file"), V2(d["pos"]), MiniJson.Num(e, "yaw"), MiniJson.Num(e, "scale", 1f), MiniJson.Num(e, "height"), back);
            }
        }

        /// <summary>把整個村子擺到 root 底下（root 的座標＝村子座標）</summary>
        public void Build(Transform root)
        {
            var g = new GameObject("Town kit").transform; g.SetParent(root, false);
            PlaceAll(g, MiniJson.Arr(layout, "ground"), false);
            PlaceAll(g, MiniJson.Arr(layout, "buildings"), false);
            PlaceAll(g, MiniJson.Arr(layout, "props"), false);
            foreach (var e in MiniJson.Arr(layout, "environment") ?? new List<object>())
            {
                var d = e as Dictionary<string, object>; if (d == null) continue;
                string file = MiniJson.Str(e, "file");
                bool back = file != null && (file.Contains("volcano") || file.Contains("cliff") || MiniJson.Str(e, "atlas") == "backdrop");
                Place(g, file, V2(d["pos"]), MiniJson.Num(e, "yaw"), MiniJson.Num(e, "scale", 1f), MiniJson.Num(e, "height"), back);
            }
            foreach (var key in new[] { "perimeter", "gate" })
            {
                var o = MiniJson.Obj(layout, key); if (o == null) continue;
                Place(g, MiniJson.Str(o, "file"), V2(o["pos"]), MiniJson.Num(o, "yaw"), 1f, 0f, false);
            }
        }

        // ---------------------------------------------------------------- 碰撞（多邊形）
        /// <summary>點到多邊形邊的最近點；inside＝點在多邊形裡面（射線法，凹多邊形也對）</summary>
        static Vector2 Closest(Vector2[] poly, Vector2 p, out bool inside)
        {
            inside = false; float best = float.MaxValue; var bp = p;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                if ((b.y > p.y) != (a.y > p.y) && p.x < (a.x - b.x) * (p.y - b.y) / (a.y - b.y + 1e-9f) + b.x) inside = !inside;
                var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
                var q = a + ab * t; float d = (p - q).sqrMagnitude;
                if (d < best) { best = d; bp = q; }
            }
            return bp;
        }

        /// <summary>半徑 r 的圓（玩家、寵物）不能進牆、櫃台：推到最近的邊外面</summary>
        public Vector2 Push(Vector2 p, float r)
        {
            for (int k = 0; k < 2; k++)
                foreach (var poly in colliders)
                {
                    var q = Closest(poly, p, out bool inside);
                    var v = p - q; float d = v.magnitude;
                    if (inside) p = q + (d > 1e-5f ? -v / d : Vector2.down) * r;
                    else if (d < r) p = q + (d > 1e-5f ? v / d : Vector2.down) * r;
                }
            return p;
        }

        public bool Blocked(Vector2 p, float r)
        {
            foreach (var poly in colliders) { var q = Closest(poly, p, out bool inside); if (inside || (p - q).sqrMagnitude < r * r) return true; }
            return false;
        }
    }
}
