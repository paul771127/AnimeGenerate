using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 熔岩火山場景（無盡模式第 1～10 波）：全部用程式產生，不需要任何圖檔。
    ///   1. 場地內的地板：黑色玄武岩＋會發光脈動的熔岩裂縫（貼在玩家畫的場地範圍上）。
    ///   2. 場地外：一大片緩緩流動的熔岩海（到 35m）。
    ///   3. 遠方背景：一圈火山剪影（山頂發光、熔岩從山坡流下）＋暗紅色煙霧天空，三座火山冒煙。
    ///   4. 場地周圍幾根尖銳的岩柱；空中飄著火星、灰燼。
    /// 地板與背景都是半透明，仍隱約看得到真實的房間（安全）。座標都是 AR session 座標。
    /// </summary>
    public class VolcanoScene : MonoBehaviour
    {
        const float SeaRadius = 35f, BackRadius = 32f, BackHeight = 22f;
        Material basaltMat, crackMat, seaMat, backMat, capMat;
        float clock;

        public static VolcanoScene Create(List<Vector3> polygon, float floorY)
        {
            var go = new GameObject("VolcanoScene");
            var vs = go.AddComponent<VolcanoScene>();
            vs.Build(polygon, floorY);
            return vs;
        }

        public void SetVisible(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }

        // ================================================================ 建置
        void Build(List<Vector3> poly, float floorY)
        {
            var center = Vector3.zero;
            if (poly != null && poly.Count >= 3) { foreach (var p in poly) center += p; center /= poly.Count; }
            center.y = floorY;
            float areaR = 2f;
            if (poly != null) foreach (var p in poly) areaR = Mathf.Max(areaR, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z)));

            var blend = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var add = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");

            // ---- 熔岩海（場地外）
            seaMat = NewMat(blend, LavaSeaTex(), 2905, new Color(0.5f, 0.5f, 0.5f, 0.45f));
            seaMat.mainTextureScale = new Vector2(SeaRadius / 3f, SeaRadius / 3f);
            MeshObj("LavaSea", Disc(SeaRadius, 64), new Vector3(center.x, floorY - 0.03f, center.z), seaMat);

            // ---- 場地內：玄武岩＋熔岩裂縫
            if (poly != null && poly.Count >= 3)
            {
                var floor = PolyMesh(poly, center, 2.2f);
                basaltMat = NewMat(blend, BasaltTex(), 2910, new Color(0.5f, 0.5f, 0.5f, 0.47f));
                MeshObj("Basalt", floor, new Vector3(0f, 0.004f, 0f), basaltMat);
                crackMat = NewMat(add, CrackTex(), 2915, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                MeshObj("LavaCracks", floor, new Vector3(0f, 0.006f, 0f), crackMat);
            }

            // ---- 遠方背景：火山剪影＋煙霧天空（圓柱內側）＋頂蓋
            backMat = NewMat(blend, BackdropTex(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            backMat.mainTextureScale = new Vector2(2f, 1f);
            MeshObj("Backdrop", Cylinder(BackRadius, BackHeight, 96), new Vector3(center.x, floorY - 1.5f, center.z), backMat);
            capMat = NewMat(blend, CapTex(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            MeshObj("SkyCap", Disc(BackRadius, 48), new Vector3(center.x, floorY - 1.5f + BackHeight, center.z), capMat);

            // ---- 場地外圍的岩柱
            var rockShader = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Mobile/Diffuse") ?? blend;
            var rockMat = new Material(rockShader) { color = new Color(0.12f, 0.09f, 0.08f), name = "VolcanoRock" };
            var rng = new System.Random(11);
            for (int i = 0; i < 9; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.6f) / 9f * Mathf.PI * 2f, r = areaR + 3f + (float)rng.NextDouble() * 6f;
                float h = 1.5f + (float)rng.NextDouble() * 3.5f, w = 0.5f + (float)rng.NextDouble() * 0.8f;
                var go = new GameObject("RockSpire");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(center.x + Mathf.Cos(a) * r, floorY - 0.2f, center.z + Mathf.Sin(a) * r);
                go.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 12f - 6f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 12f - 6f);
                go.AddComponent<MeshFilter>().sharedMesh = Spire(w, h, i);
                go.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
            }

            // ---- 粒子：場地上的火星與灰燼、遠方火山的煙
            var embers = SpellFx.NewPS(transform, "Embers", SpellFx.AddMat(0), 160, true);
            embers.transform.SetPositionAndRotation(new Vector3(center.x, floorY + 0.2f, center.z), Quaternion.Euler(90f, 0f, 0f));   // 圓形發射區躺平在地上
            SpellFx.Disc(embers, areaR + 2f);
            var em = embers.main;
            em.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            em.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            em.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.85f, 0.4f));
            SpellFx.Rise(embers, 0.35f);
            SpellFx.Noise(embers, 0.4f, 0.5f);
            SpellFx.FadeInOut(embers);
            SpellFx.Rate(embers, 22f);
            embers.Play();

            var ash = SpellFx.NewPS(transform, "Ash", SpellFx.BlendMat(0), 80, true);
            ash.transform.SetPositionAndRotation(new Vector3(center.x, floorY + 3.5f, center.z), Quaternion.Euler(90f, 0f, 0f));
            SpellFx.Disc(ash, areaR + 3f);
            var am = ash.main;
            am.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            am.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            am.startColor = new Color(0.25f, 0.22f, 0.22f, 0.7f);
            am.gravityModifier = 0.02f;
            SpellFx.Noise(ash, 0.3f, 0.3f);
            SpellFx.FadeInOut(ash);
            SpellFx.Rate(ash, 10f);
            ash.Play();

            foreach (float deg in new[] { 20f, 150f, 260f })
            {
                float a = deg * Mathf.Deg2Rad;
                var smoke = SpellFx.NewPS(transform, "VolcanoSmoke", SpellFx.BlendMat(0), 40, true);
                smoke.transform.position = new Vector3(center.x + Mathf.Cos(a) * (BackRadius - 2f), floorY + 9f, center.z + Mathf.Sin(a) * (BackRadius - 2f));
                SpellFx.Sphere(smoke, 1.5f);
                var sm = smoke.main;
                sm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
                sm.startSize = new ParticleSystem.MinMaxCurve(3f, 5f);
                sm.startColor = new Color(0.18f, 0.12f, 0.12f, 0.55f);
                var r = smoke.GetComponent<ParticleSystemRenderer>(); r.maxParticleSize = 1.5f;
                SpellFx.Rise(smoke, 1.2f);
                SpellFx.Noise(smoke, 0.6f, 0.2f);
                SpellFx.FadeInOut(smoke);
                SpellFx.Rate(smoke, 5f);
                smoke.Play();
            }
        }

        void Update()
        {
            clock += Time.deltaTime;
            // 熔岩海慢慢流動；裂縫呼吸般地脈動
            if (seaMat) seaMat.mainTextureOffset = new Vector2(clock * 0.012f, clock * 0.007f);
            if (crackMat)
            {
                float k = 0.38f + 0.12f * Mathf.Sin(clock * 1.6f) + 0.05f * Mathf.Sin(clock * 4.3f);
                crackMat.SetColor("_TintColor", new Color(k, k, k, 0.5f));
            }
        }

        // ================================================================ 小工具：材質與網格
        static Material NewMat(Shader sh, Texture2D tex, int queue, Color tint)
        {
            var m = new Material(sh) { mainTexture = tex, renderQueue = queue, name = "Volcano " + tex.name };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint); else m.color = new Color(tint.r * 2f, tint.g * 2f, tint.b * 2f, tint.a * 2f);
            return m;
        }

        void MeshObj(string name, Mesh mesh, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>場地多邊形（由中心扇形三角化）；UV＝地板座標 ÷ tile 公尺</summary>
        static Mesh PolyMesh(List<Vector3> poly, Vector3 center, float tile)
        {
            int n = poly.Count;
            var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var tri = new int[n * 3];
            v[0] = center; uv[0] = new Vector2(center.x, center.z) / tile;
            for (int i = 0; i < n; i++) { v[i + 1] = new Vector3(poly[i].x, center.y, poly[i].z); uv[i + 1] = new Vector2(poly[i].x, poly[i].z) / tile; }
            for (int i = 0; i < n; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % n; tri[i * 3 + 2] = 1 + i; }
            var m = new Mesh { name = "VolcanoFloor", vertices = v, uv = uv, triangles = tri };
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
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                uv[i + 1] = new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a));
                tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % seg; tri[i * 3 + 2] = 1 + i;
            }
            var m = new Mesh { name = "VolcanoDisc", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>開口圓柱（從內側看）：u 沿圓周、v 由下往上</summary>
        static Mesh Cylinder(float r, float h, int seg)
        {
            var v = new Vector3[(seg + 1) * 2]; var uv = new Vector2[(seg + 1) * 2]; var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                v[i * 2] = p; v[i * 2 + 1] = p + Vector3.up * h;
                uv[i * 2] = new Vector2(i / (float)seg, 0f); uv[i * 2 + 1] = new Vector2(i / (float)seg, 1f);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 2;
                tri[i * 6] = a; tri[i * 6 + 1] = a + 1; tri[i * 6 + 2] = b;
                tri[i * 6 + 3] = b; tri[i * 6 + 4] = a + 1; tri[i * 6 + 5] = b + 1;
            }
            var m = new Mesh { name = "VolcanoBackdrop", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>尖岩柱：不規則的錐體（6 邊，往上收尖，帶一點扭曲）</summary>
        static Mesh Spire(float w, float h, int seed)
        {
            var rng = new System.Random(seed * 31 + 7);
            const int sides = 6, rings = 4;
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings, rad = w * (1f - t) * (0.85f + 0.3f * (float)rng.NextDouble()) + 0.02f;
                float ox = ((float)rng.NextDouble() - 0.5f) * w * 0.4f * t, oz = ((float)rng.NextDouble() - 0.5f) * w * 0.4f * t;
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    float j = 0.8f + 0.4f * (float)rng.NextDouble();
                    verts.Add(new Vector3(ox + Mathf.Cos(a) * rad * j, t * h, oz + Mathf.Sin(a) * rad * j));
                }
            }
            verts.Add(new Vector3(0f, h * 1.08f, 0f));
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = r * sides + s, b = r * sides + (s + 1) % sides, c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
                }
            int top = verts.Count - 1, last = rings * sides;
            for (int s = 0; s < sides; s++) { tris.Add(last + s); tris.Add(top); tris.Add(last + (s + 1) % sides); }
            var m = new Mesh { name = "VolcanoSpire", vertices = verts.ToArray(), triangles = tris.ToArray() };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ================================================================ 程式產生的貼圖
        /// <summary>GLSL 的 smoothstep(e0, e1, x)（Mathf.SmoothStep 的參數意義不同）</summary>
        static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 982451653; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static float ValueNoise(float x, float y, int s, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = ((xi % period) + period) % period, y0 = ((yi % period) + period) % period, x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, s), Hash(x1, y0, s), fx), Mathf.Lerp(Hash(x0, y1, s), Hash(x1, y1, s), fx), fy);
        }
        /// <summary>可無縫平鋪的分形雜訊（0～1）</summary>
        static float Fbm(float u, float v, int s, int baseP, int oct)
        {
            float sum = 0f, amp = 0.5f, norm = 0f; int p = baseP;
            for (int o = 0; o < oct; o++) { sum += amp * ValueNoise(u * p, v * p, s + o * 17, p); norm += amp; amp *= 0.5f; p *= 2; }
            return sum / norm;
        }
        /// <summary>可平鋪的 Voronoi：回傳到最近邊界的距離（越小越接近裂縫）</summary>
        static float VoronoiEdge(float u, float v, int cells, int s)
        {
            float x = u * cells, y = v * cells; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float d1 = 9f, d2 = 9f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = ((cx % cells) + cells) % cells, wy = ((cy % cells) + cells) % cells;
                    float px = cx + 0.15f + 0.7f * Hash(wx, wy, s), py = cy + 0.15f + 0.7f * Hash(wx, wy, s + 5);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
            return d2 - d1;
        }

        static Texture2D Finish(Texture2D t, Color32[] px) { t.SetPixels32(px); t.Apply(true, true); return t; }

        static Texture2D NewTex(string name, int w, int h, bool repeatU, bool repeatV)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            t.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return t;
        }

        static Texture2D BasaltTex() { const int S = 256; return Finish(NewTex("Basalt", S, S, true, true), BasaltPixels()); }

        public static Color32[] BasaltPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 3, 4, 5);
                    float e = VoronoiEdge(u, v, 6, 9);
                    float rock = 0.07f + 0.13f * n + 0.05f * SS(0f, 0.25f, e);
                    float seam = 1f - SS(0.02f, 0.09f, e);   // 石板之間的縫（深色）
                    rock *= 1f - 0.6f * seam;
                    var c = new Color(rock * 1.05f, rock * 0.9f, rock * 0.85f, 0.92f);
                    px[y * S + x] = c;
                }
            return px;
        }

        static Texture2D CrackTex() { const int S = 256; return Finish(NewTex("Cracks", S, S, true, true), CrackPixels()); }

        public static Color32[] CrackPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float e = VoronoiEdge(u, v, 6, 9);
                    float n = Fbm(u, v, 21, 4, 3);
                    float glow = (1f - SS(0.0f, 0.07f, e)) * SS(0.35f, 0.6f, n);   // 只有部分縫裡有熔岩
                    float halo = (1f - SS(0.0f, 0.18f, e)) * SS(0.35f, 0.6f, n) * 0.35f;
                    float k = Mathf.Clamp01(glow + halo);
                    px[y * S + x] = new Color(1f * k, (0.35f + 0.4f * glow) * k, 0.06f * k, k);
                }
            return px;
        }

        static Texture2D LavaSeaTex() { const int S = 256; return Finish(NewTex("LavaSea", S, S, true, true), LavaSeaPixels()); }

        public static Color32[] LavaSeaPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u + 0.3f * Fbm(u, v, 41, 2, 3), v, 33, 3, 5);   // 扭曲的雜訊＝流動感
                    float crust = SS(0.47f, 0.55f, n);                // 冷掉的黑色硬殼（一塊塊漂著）
                    var lava = Color.Lerp(new Color(0.7f, 0.1f, 0.02f), new Color(1f, 0.42f, 0.04f), SS(0.25f, 0.45f, n));
                    float vein = 1f - SS(0.0f, 0.025f, Mathf.Abs(n - 0.45f));   // 硬殼邊緣的亮黃色
                    lava = Color.Lerp(lava, new Color(1f, 0.85f, 0.35f), vein * 0.8f);
                    var c = Color.Lerp(lava, new Color(0.08f, 0.04f, 0.03f), crust);
                    c.a = 0.95f;
                    px[y * S + x] = c;
                }
            return px;
        }

        /// <summary>背景：下方是一排火山剪影（山頂發光、熔岩流下），上方是暗紅色煙霧天空（u 可平鋪）</summary>
        static Texture2D BackdropTex() { const int W = 1024, H = 256; return Finish(NewTex("Backdrop", W, H, true, false), BackdropPixels()); }

        public static Color32[] BackdropPixels()
        {
            const int W = 1024, H = 256; var px = new Color32[W * H];
            // 山的輪廓：幾座大火山（三角形帶缺口的山頂）＋小丘陵雜訊
            var peaks = new[] { (0.08f, 0.62f, 0.13f), (0.31f, 0.48f, 0.1f), (0.52f, 0.7f, 0.15f), (0.77f, 0.55f, 0.11f), (0.93f, 0.42f, 0.08f) };
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                float ridge = 0.18f + 0.08f * Fbm(u, 0.3f, 5, 6, 4);
                float crater = 0f, flowX = 0f;
                foreach (var (pu, ph, pw) in peaks)
                {
                    float du = Mathf.Abs(Mathf.DeltaAngle(u * 360f, pu * 360f)) / 360f;
                    float hgt = ph * Mathf.Clamp01(1f - du / pw * 0.95f) + 0.025f * (Fbm(u, 0.7f, 61, 24, 3) - 0.5f);   // 山坡凹凸
                    hgt -= Mathf.Max(0f, 0.035f - du * 0.9f);   // 山頂的火口缺口
                    if (hgt > ridge) { ridge = hgt; crater = Mathf.Clamp01(1f - du / (pw * 0.18f)); flowX = du / pw; }
                }
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)H;
                    Color c;
                    if (v < ridge)
                    {
                        // 山體：深色岩石，靠近山頂與熔岩流的地方發紅光
                        float rock = 0.05f + 0.05f * Fbm(u, v, 7, 16, 3);
                        c = new Color(rock * 1.2f, rock * 0.8f, rock * 0.75f, 1f);
                        float stream = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Sin((u * 60f + v * 8f + Fbm(u, v, 9, 8, 2) * 3f))) * 6f) * SS(0.2f, 0.9f, v / Mathf.Max(0.01f, ridge)) * (1f - flowX);
                        float topGlow = SS(ridge - 0.08f, ridge, v) * crater;
                        float lava = Mathf.Clamp01(stream * 0.9f + topGlow);
                        c = Color.Lerp(c, new Color(1f, 0.42f + 0.3f * lava, 0.08f, 1f), lava);
                        c.a = 0.97f;
                    }
                    else
                    {
                        // 天空：地平線附近橘紅、往上變成暗紅煙霧
                        float sky = (v - ridge) / Mathf.Max(0.01f, 1f - ridge);
                        float smoke = Fbm(u * 2f, v * 1.2f, 13, 4, 5);
                        var low = new Color(0.55f, 0.16f, 0.05f); var high = new Color(0.12f, 0.05f, 0.05f);
                        c = Color.Lerp(low, high, SS(0f, 0.6f, sky));
                        c = Color.Lerp(c, new Color(0.08f, 0.06f, 0.06f), smoke * 0.55f);
                        // 火山口上方的光暈
                        foreach (var (pu, ph, pw) in peaks)
                        {
                            float du = Mathf.Abs(Mathf.DeltaAngle(u * 360f, pu * 360f)) / 360f, dv = v - ph;
                            float g = Mathf.Exp(-(du * du) / 0.0008f - (dv * dv) / 0.01f) * 0.6f;
                            c = Color.Lerp(c, new Color(1f, 0.45f, 0.1f), g);
                        }
                        c.a = 0.93f;
                    }
                    px[y * W + x] = c;
                }
            }
            return px;
        }

        /// <summary>天空頂蓋：中心更暗的煙霧（徑向）</summary>
        static Texture2D CapTex() { const int S = 128; return Finish(NewTex("SkyCap", S, S, false, false), CapPixels()); }

        public static Color32[] CapPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    float smoke = Fbm(u, v, 51, 4, 4);
                    var c = Color.Lerp(new Color(0.08f, 0.05f, 0.05f), new Color(0.12f, 0.05f, 0.05f), r);
                    c = Color.Lerp(c, new Color(0.05f, 0.04f, 0.04f), smoke * 0.5f);
                    c.a = 0.93f;
                    px[y * S + x] = c;
                }
            return px;
        }
    }
}
