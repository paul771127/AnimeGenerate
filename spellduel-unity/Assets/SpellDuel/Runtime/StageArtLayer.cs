using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 普通關卡的手繪 2D 場景（HD-2D，CODEX_TASK_STAGE_ART.md）：有圖就用圖，沒圖照舊用程式畫的場景。
    ///   圖：Resources/StageArt/{主題}_{變化}_{far|mid|ground|decals|props}.png，五張都在才換。
    ///   遠景：半徑 32m 的圓筒內側（360° 環景，地平線對到玩家眼睛的高度）。
    ///   中景：半徑 22m 的圓筒（透明剪影），跟遠景錯開產生景深。
    ///   地板：場地多邊形，每 2.5m 重複一次；上面隨機散落幾個貼花。
    ///   景物：場地外一圈 2D 立牌，永遠轉向鏡頭（只轉水平）。
    /// 換圖時會藏起這個變化原本的天空、遠景剪影和地板（例如熔岩谷的 Backdrop、Basalt、LavaCracks）。
    /// </summary>
    public sealed class StageArtLayer : MonoBehaviour
    {
        public static readonly string[] ThemeKeys = { "volcano", "forest", "sea", "arena", "canyon" };
        const float FarRadius = 32f, FarHeight = 24f, FarHorizon = 0.06f, EyeHeight = 1.45f;
        const float MidRadius = 22f, MidHeight = 13f;
        const float GroundTile = 2.5f, PropCell = 3f, PropFoot = 0.04f;
        /// <summary>每個主題換圖時要藏起來的舊物件（名字）</summary>
        static readonly string[] HideNames = { "Backdrop", "SkyCap", "Basalt", "LavaCracks", "Distant skyline", "Arena surface" };
        /// <summary>天空頂蓋的顏色（圓筒頂端以上看得到的部分）</summary>
        static readonly Color[] CapColor = { new Color(0.24f, 0.13f, 0.12f), new Color(0.55f, 0.66f, 0.62f), new Color(0.55f, 0.72f, 0.85f), new Color(0.6f, 0.62f, 0.7f), new Color(0.78f, 0.62f, 0.45f) };

        readonly List<Transform> props = new List<Transform>();
        readonly List<Object> owned = new List<Object>();
        static Material spriteMat;

        static Texture2D Load(string key, string layer) => Resources.Load<Texture2D>("StageArt/" + key + "_" + layer);

        public static bool HasArt(int theme, int variant)
        {
            if (theme < 0 || theme >= ThemeKeys.Length) return false;
            string k = ThemeKeys[theme] + "_" + variant;
            return Load(k, "far") && Load(k, "mid") && Load(k, "ground") && Load(k, "decals") && Load(k, "props");
        }

        /// <summary>SceneVariantArt.Install 做完三種變化後叫：有圖的變化換成手繪場景</summary>
        public static void Apply(GameObject[] groups, List<Vector3> poly, float floorY, int theme)
        {
            if (groups == null) return;
            ArtKit.CenterAndRadius(poly, floorY, out var center, out float areaR);
            for (int v = 0; v < groups.Length; v++)
            {
                if (!groups[v] || !HasArt(theme, v)) continue;
                foreach (var t in groups[v].GetComponentsInChildren<Transform>(true))
                    if (System.Array.IndexOf(HideNames, t.name) >= 0) t.gameObject.SetActive(false);
                var go = new GameObject("Painted stage " + ThemeKeys[theme] + " " + v);
                go.transform.SetParent(groups[v].transform, false);
                go.AddComponent<StageArtLayer>().Build(ThemeKeys[theme] + "_" + v, poly, center, areaR, theme, v);
            }
        }

        Material Mat(Texture2D tex, int queue, Color c)
        {
            if (!spriteMat) spriteMat = new Material(Shader.Find("Sprites/Default"));
            var m = new Material(spriteMat) { name = "Stage art " + (tex ? tex.name : "cap"), mainTexture = tex, renderQueue = queue, color = c };
            owned.Add(m);
            return m;
        }

        GameObject Obj(string name, Mesh mesh, Material mat, Vector3 pos)
        {
            owned.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        void Build(string key, List<Vector3> poly, Vector3 center, float areaR, int theme, int variant)
        {
            var far = Load(key, "far"); var mid = Load(key, "mid"); var ground = Load(key, "ground"); var decals = Load(key, "decals"); var propTex = Load(key, "props");
            far.wrapMode = TextureWrapMode.Repeat; mid.wrapMode = TextureWrapMode.Repeat; ground.wrapMode = TextureWrapMode.Repeat;
            decals.wrapMode = TextureWrapMode.Clamp; propTex.wrapMode = TextureWrapMode.Clamp;
            var rng = new System.Random(1234 + theme * 31 + variant * 7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float floorY = center.y;

            // ---- 遠景：圖上的地平線（圖高往上 FarHorizon）對到玩家眼睛的高度
            float farBase = floorY + EyeHeight - FarHorizon * FarHeight;
            Obj("Painted far", Cylinder(FarRadius, FarHeight, 128, 0f), Mat(far, 2890, Color.white), new Vector3(center.x, farBase, center.z));
            Obj("Painted sky cap", Disc(FarRadius + 0.5f, 48), Mat(null, 2889, CapColor[theme]), new Vector3(center.x, farBase + FarHeight - 0.05f, center.z));
            // 遠景底下：地平線以下的縫補成地面色（避免從圓筒底下看到 AR 鏡頭畫面或黑邊）
            Obj("Painted far skirt", Cylinder(FarRadius, 1.6f, 64, 0f), Mat(null, 2889, CapColor[theme] * 0.6f + new Color(0, 0, 0, 0.4f)), new Vector3(center.x, farBase - 1.55f, center.z));

            // ---- 中景：底部就是地面；隨機轉一個角度，跟遠景錯開
            var midGo = Obj("Painted mid", Cylinder(MidRadius, MidHeight, 96, R(0f, 1f)), Mat(mid, 2895, Color.white), new Vector3(center.x, floorY - 0.15f, center.z));

            // ---- 地板：場地多邊形，世界座標每 GroundTile 公尺重複一次
            if (poly != null && poly.Count >= 3)
                Obj("Painted ground", PolyMesh(poly, center, GroundTile), Mat(ground, 2908, Color.white), new Vector3(0f, 0.009f, 0f));

            // ---- 貼花：16 格挑 9 個，平躺在場地裡
            float inner = Mathf.Max(1.5f, areaR * 0.75f);
            for (int i = 0; i < 9; i++)
            {
                int cell = rng.Next(16);
                float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(0.05f, 1f)) * inner, size = R(1.1f, 2.2f);
                var p = new Vector3(center.x + Mathf.Cos(a) * r, floorY + 0.012f + i * 0.0006f, center.z + Mathf.Sin(a) * r);
                var d = Obj("Decal " + cell, CellQuad(cell, size, size, 0.5f, true), Mat(decals, 2912, new Color(1f, 1f, 1f, 0.92f)), p);
                d.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            }

            // ---- 景物立牌：場地外一圈，近的小一點、遠的大一點
            int n = 14;
            for (int i = 0; i < n; i++)
            {
                int cell = rng.Next(16);
                float a = (i + R(-0.3f, 0.3f)) / n * Mathf.PI * 2f, r = R(areaR + 2.2f, Mathf.Min(MidRadius - 3f, areaR + 11f));
                float s = PropCell * Mathf.Lerp(0.9f, 1.5f, Mathf.InverseLerp(areaR + 2f, MidRadius - 3f, r));
                var go = Obj("Prop " + cell, CellQuad(cell, s, s, PropFoot, false), Mat(propTex, 2920 + i % 3, Color.white), new Vector3(center.x + Mathf.Cos(a) * r, floorY, center.z + Mathf.Sin(a) * r));
                props.Add(go.transform);
            }
            LateUpdate();
        }

        void LateUpdate()
        {
            var cam = Camera.main; if (!cam) return;
            var cp = cam.transform.position;
            foreach (var t in props)
            {
                if (!t) continue;
                var look = t.position - cp; look.y = 0f;
                if (look.sqrMagnitude > 1e-4f) t.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }
        }

        void OnDestroy()
        {
            foreach (var o in owned) if (o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }
        }

        // ---------------------------------------------------------------- 網格
        /// <summary>從裡面看的圓筒：u 往畫面右邊增加（圖不會左右顛倒），uOffset 讓接縫轉到別的方向</summary>
        static Mesh Cylinder(float r, float h, int seg, float uOffset)
        {
            var v = new Vector3[(seg + 1) * 2]; var uv = new Vector2[(seg + 1) * 2]; var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f, u = uOffset + 1f - i / (float)seg;
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                v[i * 2] = p; v[i * 2 + 1] = p + Vector3.up * h;
                uv[i * 2] = new Vector2(u, 0f); uv[i * 2 + 1] = new Vector2(u, 1f);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 2;
                tri[i * 6] = a; tri[i * 6 + 1] = b; tri[i * 6 + 2] = a + 1;
                tri[i * 6 + 3] = b; tri[i * 6 + 4] = b + 1; tri[i * 6 + 5] = a + 1;
            }
            var m = new Mesh { name = "Stage art cylinder", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        static Mesh Disc(float r, int seg)
        {
            var v = new Vector3[seg + 1]; var uv = new Vector2[seg + 1]; var tri = new int[seg * 3];
            v[0] = Vector3.zero; uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); uv[i + 1] = new Vector2(0.5f, 0.5f);
                tri[i * 3] = 0; tri[i * 3 + 1] = i + 1; tri[i * 3 + 2] = (i + 1) % seg + 1;
            }
            var m = new Mesh { name = "Stage art disc", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        static Mesh PolyMesh(List<Vector3> poly, Vector3 center, float tile)
        {
            int n = poly.Count;
            var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var tri = new int[n * 3];
            v[0] = center; uv[0] = new Vector2(center.x, center.z) / tile;
            for (int i = 0; i < n; i++) { v[i + 1] = new Vector3(poly[i].x, center.y, poly[i].z); uv[i + 1] = new Vector2(poly[i].x, poly[i].z) / tile; }
            for (int i = 0; i < n; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = i + 1; tri[i * 3 + 2] = (i + 1) % n + 1; }
            var m = new Mesh { name = "Stage art ground", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>4×4 圖集的一格：flat＝平躺（貼花，中心在原點）；否則直立（立牌，腳底在原點，foot＝格子底邊往上多少是腳底）</summary>
        static Mesh CellQuad(int cell, float w, float h, float foot, bool flat)
        {
            int cx = cell % 4, cy = cell / 4;
            const float pad = 1f / 1024f;
            float u0 = cx / 4f + pad, u1 = (cx + 1) / 4f - pad, v1 = 1f - cy / 4f - pad, v0 = 1f - (cy + 1) / 4f + pad;
            Vector3[] verts = flat
                ? new[] { new Vector3(-w / 2f, 0f, -h / 2f), new Vector3(w / 2f, 0f, -h / 2f), new Vector3(w / 2f, 0f, h / 2f), new Vector3(-w / 2f, 0f, h / 2f) }
                : new[] { new Vector3(-w / 2f, -h * foot, 0f), new Vector3(w / 2f, -h * foot, 0f), new Vector3(w / 2f, h * (1f - foot), 0f), new Vector3(-w / 2f, h * (1f - foot), 0f) };
            var m = new Mesh { name = "Stage art cell", vertices = verts, uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
            m.RecalculateBounds();
            return m;
        }
    }
}
