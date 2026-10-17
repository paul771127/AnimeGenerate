using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 可走動城鎮的外觀（美術負責：見 TOWN_ART_BRIEF.md）。
    /// TownWalk 只呼叫這裡的三個函式；座標都是「城鎮座標」（父物件的 local，y=0 是地面，單位公尺，
    /// 玩家一進城站在 (0,0) 面向 +z）。目前是程式產生的佔位外觀：方塊房子、圓柱樹、膠囊路燈。
    ///   BuildScenery(town, root)        地面、遠景、道路、裝飾（不能擋路的東西）
    ///   BuildBuilding(town, kind, parent) 一棟建築（kind：smith／shop／guild／house／landmark），門朝 -z
    ///   CreateNpc(name, parent)          一個居民（腳底在 parent 原點、面向 +z）；回傳根物件
    ///   BuildFence(town, root, center, radius, gaps, half)  城鎮外圍的柵欄／城牆（gaps＝出口方向，缺口寬 2×half）
    ///   BuildGate(town, theme, parent)   出口的大門（+z 朝城外；theme＝通往第幾章）
    ///   BuildWarpPad(town, parent)       傳送陣（地上的魔法陣，直徑約 1.6m）
    /// </summary>
    public static class TownArt
    {
        // ---------------------------------------------------------------- 佈景
        public static void BuildScenery(int town, Transform root)
        {
            var c = WorldUI.Towns[town].color;
            // 地面：城鎮顏色的大圓盤＋中央廣場
            var ground = Box(root, "Ground", new Vector3(0f, -0.02f, 5f), new Vector3(32f, 0.04f, 32f), Mix(GroundColor(town), c, 0.12f));
            ground.transform.localRotation = Quaternion.identity;
            Cyl(root, "Plaza", new Vector3(0f, 0f, 5f), new Vector3(7f, 0.01f, 7f), new Color(0.78f, 0.74f, 0.66f));
            // 道路：廣場通往三個設施
            Box(root, "Road N", new Vector3(0f, 0.005f, 9f), new Vector3(1.6f, 0.01f, 6f), new Color(0.7f, 0.66f, 0.58f));
            Box(root, "Road W", new Vector3(-4f, 0.005f, 6f), new Vector3(4f, 0.01f, 1.4f), new Color(0.7f, 0.66f, 0.58f));
            Box(root, "Road E", new Vector3(4f, 0.005f, 6f), new Vector3(4f, 0.01f, 1.4f), new Color(0.7f, 0.66f, 0.58f));
            // 通往出口（東西）的路，延伸到城外
            Box(root, "Exit Road W", new Vector3(-10.5f, 0.004f, 5f), new Vector3(12f, 0.01f, 2.4f), new Color(0.66f, 0.6f, 0.5f));
            Box(root, "Exit Road E", new Vector3(10.5f, 0.004f, 5f), new Vector3(12f, 0.01f, 2.4f), new Color(0.66f, 0.6f, 0.5f));
            // 圍繞城鎮的樹／岩石（外圈，裝飾用）
            var rng = new System.Random(31 + town);
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * Mathf.PI * 2f, r = 14.5f + (float)rng.NextDouble() * 2f;
                var p = new Vector3(Mathf.Cos(a) * r, 0f, 5f + Mathf.Sin(a) * r);
                if (town == 2 && p.z < 0f) continue;   // 港口：南邊是海
                if (Mathf.Abs(Mathf.Sin(a)) < 0.25f) continue;   // 東西兩邊是出口的路
                Tree(root, p, town, (float)rng.NextDouble());
            }
            if (town == 2)
            {
                // 港口：南邊一片海＋棧橋
                Box(root, "Sea", new Vector3(0f, -0.05f, -9f), new Vector3(40f, 0.02f, 12f), new Color(0.2f, 0.45f, 0.75f));
                Box(root, "Pier", new Vector3(5f, 0.08f, -5f), new Vector3(1.4f, 0.12f, 6f), new Color(0.5f, 0.36f, 0.22f));
            }
            // 路燈
            foreach (var p in new[] { new Vector3(-2.5f, 0f, 2.5f), new Vector3(2.5f, 0f, 2.5f), new Vector3(-2.5f, 0f, 8.5f), new Vector3(2.5f, 0f, 8.5f) })
            {
                Cyl(root, "Lamp Post", p + Vector3.up * 0.9f, new Vector3(0.08f, 0.9f, 0.08f), new Color(0.2f, 0.18f, 0.16f));
                Glow(root, "Lamp", p + Vector3.up * 1.85f, 0.22f, new Color(1f, 0.85f, 0.5f));
            }
        }

        static Color GroundColor(int town) =>
            town == 0 ? new Color(0.42f, 0.33f, 0.28f) : town == 1 ? new Color(0.3f, 0.48f, 0.26f) : new Color(0.76f, 0.7f, 0.55f);

        static void Tree(Transform root, Vector3 p, int town, float k)
        {
            float h = 1.6f + k * 1.4f;
            if (town == 0)
            {
                // 熔岩地帶：黑岩
                var rock = Box(root, "Rock", p + Vector3.up * h * 0.35f, new Vector3(1.2f, h * 0.7f, 1.1f), new Color(0.22f, 0.17f, 0.15f));
                rock.transform.localRotation = Quaternion.Euler(0f, k * 90f, 8f);
                return;
            }
            Cyl(root, "Trunk", p + Vector3.up * h * 0.5f, new Vector3(0.25f, h * 0.5f, 0.25f), new Color(0.4f, 0.27f, 0.16f));
            var leaf = town == 1 ? new Color(0.2f, 0.55f, 0.25f) : new Color(0.3f, 0.65f, 0.35f);
            Ball(root, "Leaves", p + Vector3.up * (h + 0.4f), new Vector3(1.6f, 1.4f, 1.6f), leaf);
        }

        // ---------------------------------------------------------------- 建築
        public static void BuildBuilding(int town, string kind, Transform parent)
        {
            var tc = WorldUI.Towns[town].color;
            Color wall = town == 0 ? new Color(0.55f, 0.42f, 0.36f) : town == 1 ? new Color(0.55f, 0.42f, 0.28f) : new Color(0.85f, 0.85f, 0.8f);
            Color roof = kind == "smith" ? new Color(0.45f, 0.25f, 0.15f) : kind == "shop" ? new Color(0.25f, 0.5f, 0.75f) : kind == "guild" ? new Color(0.5f, 0.3f, 0.7f) : Mix(tc, Color.black, 0.3f);
            float w = kind == "guild" ? 5f : kind == "landmark" ? 2f : 3.6f, d = kind == "guild" ? 4f : 3f, h = kind == "guild" ? 3.4f : 2.6f;
            if (kind == "landmark") { Landmark(town, parent); return; }
            Box(parent, "Wall", new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), wall);
            // 屋頂：兩片斜板
            for (int s = -1; s <= 1; s += 2)
            {
                var r = Box(parent, "Roof", new Vector3(s * w * 0.25f, h + 0.45f, 0f), new Vector3(w * 0.6f, 0.12f, d + 0.4f), roof);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 28f);
            }
            Box(parent, "Door", new Vector3(0f, 0.75f, -d / 2f - 0.01f), new Vector3(0.9f, 1.5f, 0.05f), new Color(0.3f, 0.2f, 0.12f));
            // 招牌（設施的顏色）
            if (kind != "house")
                Box(parent, "Sign", new Vector3(0f, h - 0.3f, -d / 2f - 0.05f), new Vector3(1.6f, 0.5f, 0.06f), roof * 1.3f);
            if (kind == "smith")
            {
                Cyl(parent, "Chimney", new Vector3(w * 0.3f, h + 1.2f, 0.3f), new Vector3(0.45f, 0.8f, 0.45f), new Color(0.3f, 0.28f, 0.27f));
                Glow(parent, "Forge Glow", new Vector3(-w * 0.35f, 0.6f, -d / 2f - 0.3f), 0.35f, new Color(1f, 0.5f, 0.15f));
                Box(parent, "Anvil", new Vector3(w * 0.32f, 0.35f, -d / 2f - 0.6f), new Vector3(0.6f, 0.35f, 0.3f), new Color(0.25f, 0.25f, 0.28f));
            }
            else if (kind == "shop")
            {
                Box(parent, "Awning", new Vector3(0f, 1.9f, -d / 2f - 0.5f), new Vector3(w * 0.9f, 0.06f, 1f), new Color(0.9f, 0.9f, 0.95f));
                Ball(parent, "Potion", new Vector3(-w * 0.3f, 0.9f, -d / 2f - 0.5f), new Vector3(0.25f, 0.3f, 0.25f), new Color(1f, 0.3f, 0.35f));
                Ball(parent, "Potion", new Vector3(-w * 0.3f + 0.3f, 0.9f, -d / 2f - 0.5f), new Vector3(0.25f, 0.3f, 0.25f), new Color(0.3f, 0.5f, 1f));
            }
            else if (kind == "guild")
            {
                for (int s = -1; s <= 1; s += 2)
                    Cyl(parent, "Pillar", new Vector3(s * 1.4f, 1.3f, -d / 2f - 0.4f), new Vector3(0.3f, 1.3f, 0.3f), new Color(0.9f, 0.88f, 0.82f));
                Box(parent, "Banner", new Vector3(0f, h + 1f, -d / 2f + 0.2f), new Vector3(0.7f, 1.2f, 0.04f), new Color(0.7f, 0.45f, 1f));
            }
        }

        static void Landmark(int town, Transform p)
        {
            if (town == 0)
            {
                // 炎之里：晨鐘塔
                Box(p, "Tower", new Vector3(0f, 2f, 0f), new Vector3(1.6f, 4f, 1.6f), new Color(0.45f, 0.35f, 0.3f));
                Ball(p, "Bell", new Vector3(0f, 3.7f, -0.6f), new Vector3(0.6f, 0.7f, 0.6f), new Color(0.85f, 0.65f, 0.25f));
                Box(p, "Tower Roof", new Vector3(0f, 4.3f, 0f), new Vector3(2f, 0.3f, 2f), new Color(0.35f, 0.18f, 0.12f));
            }
            else if (town == 1)
            {
                // 森之里：古樹＋木牌
                Cyl(p, "Old Tree", new Vector3(0f, 2.5f, 0f), new Vector3(1.2f, 2.5f, 1.2f), new Color(0.38f, 0.26f, 0.15f));
                Ball(p, "Crown", new Vector3(0f, 5.5f, 0f), new Vector3(5f, 3f, 5f), new Color(0.22f, 0.5f, 0.25f));
                for (int i = 0; i < 5; i++)
                    Box(p, "Name Board", new Vector3(-1.2f + i * 0.6f, 0.4f, -1.1f), new Vector3(0.35f, 0.5f, 0.05f), new Color(0.65f, 0.5f, 0.3f));
                Glow(p, "Leaf Lantern", new Vector3(0.9f, 2f, -0.9f), 0.25f, new Color(0.6f, 1f, 0.5f));
            }
            else
            {
                // 潮騷港：舊燈塔
                Cyl(p, "Lighthouse", new Vector3(0f, 2.5f, 0f), new Vector3(1.4f, 2.5f, 1.4f), new Color(0.92f, 0.92f, 0.9f));
                Cyl(p, "Stripe", new Vector3(0f, 3f, 0f), new Vector3(1.45f, 0.3f, 1.45f), new Color(0.85f, 0.25f, 0.25f));
                Glow(p, "Light", new Vector3(0f, 5.3f, 0f), 0.6f, new Color(1f, 0.95f, 0.6f));
            }
        }

        // ---------------------------------------------------------------- 柵欄、出口、傳送陣
        public static void BuildFence(int town, Transform root, Vector3 center, float radius, List<Vector2> gaps, float half)
        {
            Color post = town == 0 ? new Color(0.3f, 0.24f, 0.22f) : town == 1 ? new Color(0.45f, 0.32f, 0.2f) : new Color(0.85f, 0.85f, 0.82f);
            Color rail = town == 2 ? new Color(0.35f, 0.55f, 0.8f) : Mix(post, Color.white, 0.15f);
            int n = Mathf.CeilToInt(2f * Mathf.PI * radius / 1.5f);
            Vector3 Pt(int i) { float a = i * 2f * Mathf.PI / n; return center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius; }
            bool InGap(Vector3 p)
            {
                var v = new Vector2(p.x - center.x, p.z - center.z);
                foreach (var g in gaps) if (Vector2.Dot(v, g) > 0f && Mathf.Abs(g.x * v.y - g.y * v.x) < half + 0.2f) return true;
                return false;
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 a = Pt(i), b = Pt(i + 1);
                if (InGap(a)) continue;
                Box(root, "Fence Post", a + Vector3.up * 0.6f, new Vector3(0.18f, 1.2f, 0.18f), post);
                if (InGap(b)) continue;
                var mid = (a + b) / 2f; var d = b - a;
                for (int k = 0; k < 2; k++)
                {
                    var r = Box(root, "Fence Rail", mid + Vector3.up * (0.45f + k * 0.45f), new Vector3(0.08f, 0.1f, d.magnitude), rail);
                    r.transform.localRotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                }
            }
        }

        public static void BuildGate(int town, int theme, Transform parent)
        {
            var c = WorldUI.RegionColors[Mathf.Clamp(theme, 0, WorldUI.RegionColors.Length - 1)];
            Color wood = new Color(0.4f, 0.28f, 0.18f);
            for (int s = -1; s <= 1; s += 2)
                Box(parent, "Gate Pillar", new Vector3(s * 1.5f, 1.3f, 0f), new Vector3(0.35f, 2.6f, 0.35f), wood);
            Box(parent, "Gate Beam", new Vector3(0f, 2.7f, 0f), new Vector3(3.6f, 0.3f, 0.4f), wood);
            Box(parent, "Gate Sign", new Vector3(0f, 2.3f, -0.22f), new Vector3(1.8f, 0.45f, 0.06f), c);
            Glow(parent, "Gate Light", new Vector3(0f, 1.2f, 2f), 0.4f, c);
        }

        public static void BuildWarpPad(int town, Transform parent)
        {
            Cyl(parent, "Warp Base", new Vector3(0f, 0.02f, 0f), new Vector3(1.7f, 0.02f, 1.7f), new Color(0.3f, 0.28f, 0.4f));
            Prim(PrimitiveType.Cylinder, parent, "Warp Glow", new Vector3(0f, 0.045f, 0f), new Vector3(1.4f, 0.01f, 1.4f), Mat(new Color(0.6f, 0.5f, 1f), true));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                Glow(parent, "Warp Crystal", new Vector3(Mathf.Cos(a) * 0.95f, 0.5f, Mathf.Sin(a) * 0.95f), 0.18f, new Color(0.7f, 0.6f, 1f));
            }
            parent.gameObject.AddComponent<Spin>();
        }

        /// <summary>傳送陣慢慢轉</summary>
        class Spin : MonoBehaviour { void Update() { transform.Rotate(0f, 30f * Time.deltaTime, 0f, Space.Self); } }

        // ---------------------------------------------------------------- 居民
        /// <summary>居民的外觀：目前借用職業角色模型（依角色個性挑職業），小孩縮小</summary>
        public static GameObject CreateNpc(string name, Transform parent)
        {
            string key = Portraits.Key(name);
            string cls = NpcClass.TryGetValue(key, out var c) ? c : "priest";
            var rig = CharacterRig.Create(cls, parent);
            if (key == "小葉") rig.transform.localScale = Vector3.one * 0.72f;
            if (key == "小鐵") rig.transform.localScale = Vector3.one * 0.9f;
            return rig.gameObject;
        }

        static readonly Dictionary<string, string> NpcClass = new Dictionary<string, string>
        {
            { "老焰", "priest" }, { "小鐵", "swordsman" }, { "米菈", "archer" },
            { "翠", "mage" }, { "楓", "archer" }, { "小葉", "assassin" },
            { "海斗", "swordsman" }, { "凪", "priest" }, { "阿潮", "archer" },
            { "鐵匠", "swordsman" }, { "店員", "mage" }, { "公會長", "priest" },
        };

        // ---------------------------------------------------------------- 小工具
        static Shader lit, glow;
        static readonly Dictionary<Color32, Material> mats = new Dictionary<Color32, Material>();

        static Material Mat(Color c, bool glowing = false)
        {
            if (lit == null)
            {
                lit = ToonStyle.LitShader() ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse") ?? Shader.Find("Sprites/Default");
                glow = Shader.Find("Sprites/Default") ?? lit;
            }
            c.a = glowing ? 0.999f : 1f;
            Color32 key = c;
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(glowing ? glow : lit) { color = c, name = "Town " + ColorUtility.ToHtmlStringRGB(c) };
            mats[key] = m;
            return m;
        }

        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            var col = go.GetComponent<Collider>(); if (col) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        public static GameObject Box(Transform p, string n, Vector3 pos, Vector3 s, Color c) => Prim(PrimitiveType.Cube, p, n, pos, s, Mat(c));
        public static GameObject Cyl(Transform p, string n, Vector3 pos, Vector3 s, Color c) => Prim(PrimitiveType.Cylinder, p, n, pos, s, Mat(c));
        public static GameObject Ball(Transform p, string n, Vector3 pos, Vector3 s, Color c) => Prim(PrimitiveType.Sphere, p, n, pos, s, Mat(c));
        public static GameObject Glow(Transform p, string n, Vector3 pos, float size, Color c) => Prim(PrimitiveType.Sphere, p, n, pos, Vector3.one * size, Mat(c, true));
    }
}
