using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 森林植物小怪的攻擊特效（全部程式產生，不用任何素材檔）。呼叫者傳入的都是 Unity 世界座標（已換算好的 AR 座標），
    /// 地面 = 傳入點的高度、世界 +Y 朝上。
    ///   Vine       ：帶刺的扭曲藤蔓從 a 沿地面長到 b（離地約 0.1–0.35 m），life 秒後枯萎消失。
    ///   PoisonPool ：地上一攤發光的綠紫色毒液，冒泡、微微脈動，life 秒後淡出消失。
    ///   PoisonSplash：毒液彈落地的濺射（短暫）。
    ///   SporeBurst ：孢子菇的孢子雲（短暫）。
    /// 只用保證會進 build 的 shader：Legacy Shaders/Diffuse（實體）、Sprites/Default（毒液面）、Legacy Particles（粒子、光）。
    /// 持續型的效果（藤蔓、毒液區）的「life」是整個物件的壽命：最後一小段（約 0.4–0.6 秒）做枯萎／淡出，時間到自動 Destroy。
    /// </summary>
    public static class PlantFx
    {
        // ================================================================ 共用材質
        static Material litMat, spriteMat;
        static MaterialPropertyBlock mpb;
        internal static readonly int ColorId = Shader.PropertyToID("_Color");
        internal static readonly int TintId = Shader.PropertyToID("_TintColor");

        internal static MaterialPropertyBlock Mpb { get { Init(); return mpb; } }
        internal static Material LitMat { get { Init(); return litMat; } }

        static void Init()
        {
            if (litMat != null) return;
            var lit = Shader.Find("Legacy Shaders/Diffuse");
            if (lit == null) lit = Shader.Find("Mobile/Diffuse");
            if (lit == null) lit = Shader.Find("Diffuse");
            var spr = Shader.Find("Sprites/Default");
            if (lit == null) lit = spr;
            litMat = new Material(lit) { color = Color.white, name = "PlantFxLit" };      // 顏色由 PropertyBlock 給
            spriteMat = new Material(spr) { color = Color.white, name = "PlantFxSprite" };  // 頂點色＋_Color（毒液面）
            mpb = new MaterialPropertyBlock();
        }

        internal static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>稍微衝過頭再回來的 0→1（長出來、攤開來的感覺）。</summary>
        internal static float BackOut(float x)
        {
            x = Mathf.Clamp01(x) - 1f;
            const float k = 1.9f;
            return 1f + x * x * ((k + 1f) * x + k);
        }

        static void NoShadow(Renderer r)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ================================================================ 藤蔓
        /// <summary>藤蔓：從 a 長到 b（地面高度附近），grow 秒內長滿，life 秒後枯萎消失（自動 Destroy）。回傳 GameObject。</summary>
        public static GameObject Vine(Vector3 a, Vector3 b, float grow, float life)
        {
            Init();
            var go = new GameObject("Plant Vine");
            var flat = b - a; flat.y = 0f;
            var rot = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat.normalized, Vector3.up) : Quaternion.identity;
            go.transform.SetPositionAndRotation(a, rot);   // 本地 +Z = 藤蔓方向、+Y = 世界上方
            var vine = go.AddComponent<PlantVine>();
            vine.Setup(go.transform.InverseTransformPoint(b), grow, life, litMat);
            return go;
        }

        // ================================================================ 毒液區
        /// <summary>毒液區：地上一攤直徑 = radius*2 的發亮綠紫色毒液，冒泡；life 秒後淡出消失（自動 Destroy）。</summary>
        public static GameObject PoisonPool(Vector3 center, float radius, float life)
        {
            Init();
            radius = Mathf.Max(0.05f, radius);
            var go = new GameObject("Plant Poison Pool");
            go.transform.SetPositionAndRotation(center + Vector3.up * 0.015f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var pool = go.AddComponent<PlantPool>();
            pool.Setup(radius, life, spriteMat);
            return go;
        }

        /// <summary>毒液彈落地的濺射效果（短暫，約 1.3 秒後自己清除）。</summary>
        public static void PoisonSplash(Vector3 at, float radius)
        {
            Init();
            radius = Mathf.Max(0.1f, radius);
            var green = new Color(0.5f, 1f, 0.2f);
            var purple = new Color(0.7f, 0.3f, 1f);
            var go = new GameObject("Plant Poison Splash");
            // 本地 XY 平面 = 地面（本地 +Z 朝下）
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.03f, Quaternion.Euler(90f, 0f, 0f));
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 1.3f;
            var root = go.transform;
            anim.AddRing(root, SpellFx.Circle(36, 1f), true, 0.08f * radius, green, 0f, 0.45f, 0.2f * radius, 1.1f * radius);
            anim.AddRing(root, SpellFx.Zigzag(24, 0.75f, 1f), true, 0.05f * radius, purple, 0.05f, 0.5f, 0.15f * radius, 0.85f * radius, true, 90f);
            anim.AddRing(root, SpellFx.Circle(32, 1f), true, 0.3f * radius, new Color(0.15f, 0.3f, 0.05f, 0.5f), 0f, 0.8f, 0.3f * radius, 0.6f * radius, false);

            var flash = SpellFx.NewPS(root, "Flash", SpellFx.AddMat(SpellFx.TexSoft), 2, false);
            {
                var m = flash.main;
                m.startLifetime = 0.35f;
                m.startSize = 1.4f * radius;
                m.startColor = new Color(0.45f, 1f, 0.3f, 0.7f);
                SpellFx.FadeOut(flash);
                SpellFx.SizeCurve(flash, SpellFx.PopCurve);
                flash.Play(); flash.Emit(1);
            }
            var upT = new GameObject("Up").transform;
            upT.SetParent(root, false);
            upT.localRotation = Quaternion.Euler(180f, 0f, 0f);   // +Z 朝上
            float sp = Mathf.Sqrt(radius);
            var drops = SpellFx.NewPS(upT, "Drops", SpellFx.AddMat(SpellFx.TexSoft), 30, true);
            {
                var m = drops.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * sp, 2.8f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                m.startColor = new ParticleSystem.MinMaxGradient(green, purple);
                m.gravityModifier = 1.1f;
                SpellFx.Cone(drops, 55f, 0.15f * radius);
                SpellFx.FadeOut(drops);
                SpellFx.Stretch(drops, 0.05f, 1.4f);
                drops.Play(); drops.Emit(26);
            }
            var blobs = SpellFx.NewPS(upT, "Blobs", SpellFx.BlendMat(SpellFx.TexSoft), 12, true);
            {
                var m = blobs.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * sp, 1.8f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.25f, 0.6f, 0.1f, 0.85f), new Color(0.4f, 0.15f, 0.5f, 0.85f));
                m.gravityModifier = 1.2f;
                SpellFx.Cone(blobs, 45f, 0.1f * radius);
                SpellFx.FadeOut(blobs);
                blobs.Play(); blobs.Emit(10);
            }
            var mist = SpellFx.NewPS(upT, "Mist", SpellFx.BlendMat(SpellFx.TexSoft), 8, true);
            {
                var m = mist.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.5f * radius, 0.8f * radius);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.7f, 0.2f, 0.3f), new Color(0.45f, 0.25f, 0.55f, 0.28f));
                SpellFx.Cone(mist, 60f, 0.3f * radius);
                SpellFx.FadeOut(mist);
                SpellFx.SizeCurve(mist, SpellFx.Grow);
                SpellFx.Drag(mist, 2f);
                mist.Play(); mist.Emit(6);
            }
        }

        // ================================================================ 孢子爆發
        /// <summary>孢子爆發（孢子菇攻擊）：以 at（地面中心點）為中心、半徑 radius 的一團孢子雲，約 2 秒後自己清除。</summary>
        public static void SporeBurst(Vector3 at, float radius)
        {
            Init();
            radius = Mathf.Max(0.2f, radius);
            var glowA = new Color(0.75f, 1f, 0.35f);
            var glowB = new Color(0.8f, 0.5f, 1f);
            var go = new GameObject("Plant Spore Burst");
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 2.2f;
            var root = go.transform;
            // 地上的衝擊環（孢子雲的範圍）
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.07f * radius, glowA, 0f, 0.7f, 0.25f * radius, 1.05f * radius);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.12f * radius, glowB, 0.08f, 0.8f, 0.2f * radius, 0.9f * radius);

            // 雲的中心在地面上方（孢子菇的菇傘高度附近）
            var cloud = new GameObject("Cloud").transform;
            cloud.SetParent(root, false);
            cloud.position = at + Vector3.up * Mathf.Min(0.45f, 0.2f + 0.25f * radius);
            var flash = SpellFx.NewPS(cloud, "Flash", SpellFx.AddMat(SpellFx.TexSoft), 2, false);
            {
                var m = flash.main;
                m.startLifetime = 0.4f;
                m.startSize = 1.6f * radius;
                m.startColor = new Color(0.6f, 0.9f, 0.4f, 0.45f);
                SpellFx.FadeOut(flash);
                SpellFx.SizeCurve(flash, SpellFx.PopCurve);
                flash.Play(); flash.Emit(1);
            }
            var puffs = SpellFx.NewPS(cloud, "Puffs", SpellFx.BlendMat(SpellFx.TexSoft), 18, true);
            {
                var m = puffs.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * radius, 1.8f * radius);
                m.startSize = new ParticleSystem.MinMaxCurve(0.45f * radius, 0.8f * radius);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.4f, 0.62f, 0.42f), new Color(0.58f, 0.7f, 0.3f, 0.38f));
                SpellFx.Sphere(puffs, 0.25f * radius);
                SpellFx.FadeOut(puffs);
                SpellFx.SizeCurve(puffs, SpellFx.Grow);
                SpellFx.Spin(puffs, 0.6f);
                SpellFx.Drag(puffs, 2.5f);
                SpellFx.Rise(puffs, 0.06f);
                puffs.Play(); puffs.Emit(16);
            }
            var spores = SpellFx.NewPS(cloud, "Spores", SpellFx.AddMat(SpellFx.TexSoft), 50, true);
            {
                var m = spores.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * radius, 1.6f * radius);
                m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
                m.startColor = new ParticleSystem.MinMaxGradient(glowA, glowB);
                SpellFx.Sphere(spores, 0.2f * radius);
                SpellFx.FadeInOut(spores);
                SpellFx.Drag(spores, 2.2f);
                SpellFx.Noise(spores, 0.35f, 1.5f);
                SpellFx.Rise(spores, 0.08f);
                spores.Play(); spores.Emit(45);
            }
        }
    }

    // ==================================================================== 藤蔓元件
    /// <summary>
    /// 藤蔓：本地 +Z 從原點（a）到 end（b），兩股粗藤＋一股細藤螺旋交纏成的管子，離地 0.1–0.35 m 蜿蜒，
    /// 上面長滿往後勾的尖刺與幾片小葉。生長時每幀重建網格（尖端收成尖頭往前推），長滿後不再重建；
    /// 最後一段時間枯萎（變褐、變細、往下沉、刺縮回）。底下一條淡淡的綠光與地上的泥土痕跡。
    /// </summary>
    internal class PlantVine : MonoBehaviour
    {
        static readonly Color VineC = new Color(0.1f, 0.27f, 0.08f);
        static readonly Color ThornC = new Color(0.82f, 0.76f, 0.5f);
        static readonly Color LeafC = new Color(0.24f, 0.52f, 0.14f);
        static readonly Color DeadC = new Color(0.3f, 0.22f, 0.12f);
        static readonly Color GlowC = new Color(0.35f, 1f, 0.3f);

        Vector3 end;
        float L, grow, life, witherDur, time, seed, lastG = -1f, lastW = -1f;
        bool witherFx;
        Mesh mesh;
        MeshRenderer mr;
        LineRenderer glow, scar;
        ParticleSystem tipPs;
        Vector3[] linePts = new Vector3[0];
        readonly List<Vector3> verts = new List<Vector3>(4096);
        readonly List<int> triV = new List<int>(8192), triT = new List<int>(2048), triL = new List<int>(512);

        public void Setup(Vector3 endLocal, float grow, float life, Material lit)
        {
            end = endLocal;
            L = Mathf.Max(0.05f, end.magnitude);
            this.grow = Mathf.Max(0.02f, grow);
            this.life = Mathf.Max(0.1f, life);
            witherDur = Mathf.Clamp(this.life * 0.25f, 0.12f, 0.6f);
            seed = Random.Range(0f, 100f);

            mesh = new Mesh { name = "PlantVine" };
            mesh.MarkDynamic();
            var vg = new GameObject("Mesh");
            vg.transform.SetParent(transform, false);
            vg.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = vg.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { lit, lit, lit };   // 0 藤、1 刺、2 葉
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            int lineN = Mathf.Clamp(Mathf.CeilToInt(L / 0.15f) + 1, 4, 40);
            linePts = new Vector3[lineN];
            glow = SpellFx.Line(transform, "Glow", new[] { Vector3.zero, Vector3.zero }, false, 0.4f, SpellFx.WithA(GlowC, 0.2f), true, false);
            // 地上的泥土痕跡：平躺在地面（子物件 +Z 朝上，線的寬度方向在地面上）
            var scarT = new GameObject("Scar").transform;
            scarT.SetParent(transform, false);
            scarT.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            scar = SpellFx.Line(scarT, "Line", new[] { Vector3.zero, Vector3.zero }, false, 0.22f, new Color(0.12f, 0.08f, 0.04f, 0.55f), false, true);

            tipPs = SpellFx.NewPS(transform, "TipDebris", SpellFx.BlendMat(SpellFx.TexLeaf), 60, true);
            {
                var m = tipPs.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.45f, 0.1f), new Color(0.35f, 0.25f, 0.12f));
                m.gravityModifier = 0.6f;
                SpellFx.Sphere(tipPs, 0.08f);
                SpellFx.FadeOut(tipPs);
                SpellFx.Spin(tipPs, 5f);
                SpellFx.Drag(tipPs, 1.5f);
                SpellFx.Rate(tipPs, 35f);
                tipPs.Play();
                tipPs.Emit(10);   // 破土而出
            }
            Rebuild(0.02f, 0f);
        }

        // ---- 中心線：從地面冒出（起點高度 0）、往上到 0.2 m 左右，左右蜿蜒、上下起伏
        Vector3 Center(float s)
        {
            float u = Mathf.Clamp(s / L, -0.1f, 1f);
            float rise = PlantFx.Smooth01(s / 0.35f);
            float lift = rise * (0.21f + 0.07f * Mathf.Sin(s * 3.3f + seed));
            float side = 0.1f * Mathf.Sin(s * 2.1f + seed * 2f) * PlantFx.Smooth01(s / 0.5f);
            return end * u + Vector3.right * side + Vector3.up * lift;
        }

        void Frame(float s, out Vector3 c, out Vector3 t, out Vector3 n1, out Vector3 n2)
        {
            c = Center(s);
            t = (Center(s + 0.02f) - Center(s - 0.02f)).normalized;
            n1 = Vector3.Cross(Vector3.up, t);
            n1 = n1.sqrMagnitude > 1e-6f ? n1.normalized : Vector3.right;
            n2 = Vector3.Cross(t, n1);   // Cross(n1, n2) = t → 面朝外的繞向；n2 大致朝上
        }

        static float Hash(float x) { x = Mathf.Sin(x * 12.9898f) * 43758.5453f; return x - Mathf.Floor(x); }

        void Rebuild(float g, float w)
        {
            verts.Clear(); triV.Clear(); triT.Clear(); triL.Clear();
            float sink = 0.12f * w;
            float thick = 1f - w;
            var down = Vector3.down * sink;

            // ---- 三股藤：兩股粗的 180° 相對、一股細的反向纏繞
            const int sides = 6;
            for (int k = 0; k < 3; k++)
            {
                float rh = k < 2 ? 0.045f : 0.085f, rs = (k < 2 ? 0.06f : 0.022f) * thick;
                float twist = k < 2 ? 6.5f : -4.2f, ph = k < 2 ? k * Mathf.PI : seed;
                int start = verts.Count, rings = 0;
                float step = 0.06f;
                // 起點後方先放一圈半徑 0 的點把開口封起來
                for (float s = -0.03f; ; s += step)
                {
                    bool last = s >= g;
                    float ss = last ? g : s;
                    Frame(ss, out var c, out var t, out var n1, out var n2);
                    float th = ss * twist + ph;
                    var off = (n1 * Mathf.Cos(th) + n2 * Mathf.Sin(th)) * rh;
                    float taper = Mathf.Clamp01((g - ss) / 0.3f);
                    float r = ss < 0f ? 0f : rs * Mathf.Sqrt(taper);
                    if (k == 2) r *= 0.6f + 0.4f * Mathf.Sin(ss * 5f + seed);   // 細藤粗細不一
                    var cc = c + off * Mathf.Min(1f, taper * 2f + 0.2f) + down;
                    for (int j = 0; j < sides; j++)
                    {
                        float a = 2f * Mathf.PI * j / sides;
                        verts.Add(cc + (n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a)) * r);
                    }
                    rings++;
                    if (last) break;
                }
                for (int i = 0; i < rings - 1; i++)
                    for (int j = 0; j < sides; j++)
                    {
                        int j1 = (j + 1) % sides;
                        int p00 = start + i * sides + j, p01 = start + i * sides + j1, p10 = p00 + sides, p11 = p01 + sides;
                        triV.Add(p00); triV.Add(p11); triV.Add(p10);
                        triV.Add(p00); triV.Add(p01); triV.Add(p11);
                    }
            }

            // ---- 尖刺：往外並微微往回勾（像玫瑰刺），避開朝地面的方向
            int idx = 0;
            for (float s = 0.1f; s < g - 0.04f; s += 0.085f, idx++)
            {
                float sc = Mathf.Clamp01((g - s - 0.04f) / 0.2f) * (1f - w);
                if (sc < 0.02f) continue;
                Frame(s, out var c, out var t, out var n1, out var n2);
                float psi = (Hash(idx + seed) - 0.5f) * 220f * Mathf.Deg2Rad;
                var dir = n2 * Mathf.Cos(psi) + n1 * Mathf.Sin(psi);
                float len = (0.11f + 0.08f * Hash(idx * 3.7f + seed)) * sc;
                var b0 = c + dir * 0.065f * thick + down;
                var tip = b0 + dir * len - t * 0.035f * sc;
                var ax = (tip - b0).normalized;
                var m1 = Vector3.Cross(ax, Mathf.Abs(Vector3.Dot(ax, t)) < 0.95f ? t : Vector3.up).normalized;
                var m2 = Vector3.Cross(ax, m1);   // Cross(m1, m2) = ax
                float br = 0.028f * sc;
                for (int j = 0; j < 4; j++)
                {
                    float a0 = j * Mathf.PI * 0.5f, a1 = (j + 1) * Mathf.PI * 0.5f;
                    var p0 = b0 + (m1 * Mathf.Cos(a0) + m2 * Mathf.Sin(a0)) * br;
                    var p1 = b0 + (m1 * Mathf.Cos(a1) + m2 * Mathf.Sin(a1)) * br;
                    int vi = verts.Count;
                    verts.Add(p0); verts.Add(p1); verts.Add(tip);
                    triT.Add(vi); triT.Add(vi + 1); triT.Add(vi + 2);
                }
            }

            // ---- 小葉：每隔一段在上側左右交替長出一片（雙面）
            idx = 0;
            for (float s = 0.3f; s < g - 0.08f; s += 0.42f, idx++)
            {
                float sc = Mathf.Clamp01((g - s - 0.08f) / 0.25f) * (1f - w * 0.7f);
                if (sc < 0.02f) continue;
                Frame(s, out var c, out var t, out var n1, out var n2);
                float side = (idx & 1) == 0 ? 1f : -1f;
                var dir = (t * 0.55f + n1 * side * 0.75f + n2 * 0.45f).normalized;
                var wv = Vector3.Cross(dir, n2).normalized;
                var b0 = c + n2 * 0.05f + n1 * side * 0.04f + down;
                float len = 0.15f * sc, wid = 0.05f * sc;
                var tipP = b0 + dir * len - n2 * 0.02f * sc;
                var mid = b0 + dir * len * 0.45f + n2 * 0.012f * sc;
                var lp = mid + wv * wid; var rp = mid - wv * wid;
                AddLeafTri(b0, lp, tipP);
                AddLeafTri(b0, tipP, rp);
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(triV, 0, false);
            mesh.SetTriangles(triT, 1, false);
            mesh.SetTriangles(triL, 2, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // ---- 底下的綠光與泥土痕跡（只畫已長出的部分）
            int n = linePts.Length;
            float gl = Mathf.Max(0.02f, g);
            for (int i = 0; i < n; i++) linePts[i] = Center(gl * i / (n - 1)) + down;
            glow.positionCount = n;
            glow.SetPositions(linePts);
            var rot = Quaternion.Euler(90f, 0f, 0f);   // 藤蔓本地 → 平躺子物件的本地
            for (int i = 0; i < n; i++) { var p = linePts[i]; p.y = 0.012f; linePts[i] = rot * p; }
            scar.positionCount = n;
            scar.SetPositions(linePts);
        }

        void AddLeafTri(Vector3 a, Vector3 b, Vector3 c)
        {
            int vi = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            verts.Add(a); verts.Add(c); verts.Add(b);   // 背面（獨立頂點，法線才不會互相抵消）
            triL.Add(vi); triL.Add(vi + 1); triL.Add(vi + 2);
            triL.Add(vi + 3); triL.Add(vi + 4); triL.Add(vi + 5);
        }

        void Update()
        {
            time += Time.deltaTime;
            if (time >= life) { Destroy(gameObject); return; }
            float g = L * PlantFx.Smooth01(Mathf.Sqrt(Mathf.Clamp01(time / grow)));   // 一開始竄得快、最後放慢
            float w = PlantFx.Smooth01((time - (life - witherDur)) / witherDur);
            if (Mathf.Abs(g - lastG) > 1e-4f || Mathf.Abs(w - lastW) > 1e-4f)
            {
                Rebuild(g, w);
                lastG = g; lastW = w;
            }

            // 生長中的尖端噴出泥土與碎葉
            if (tipPs != null)
            {
                bool growing = g < L - 0.01f;
                SpellFx.Rate(tipPs, growing ? 35f : 0f);
                if (growing) tipPs.transform.position = transform.TransformPoint(Center(g));
                if (w > 0f && !witherFx)
                {
                    // 開始枯萎：沿著藤蔓掉下一些枯葉
                    witherFx = true;
                    var ep = new ParticleSystem.EmitParams();
                    for (int i = 0; i < 14; i++)
                    {
                        ep.position = transform.TransformPoint(Center(Random.Range(0f, L)));
                        ep.velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 0.6f), Random.Range(-0.3f, 0.3f));
                        ep.startColor = Color.Lerp(DeadC, new Color(0.5f, 0.42f, 0.2f), Random.value);
                        tipPs.Emit(ep, 1);
                    }
                }
            }

            // 顏色：藤＝深綠、刺＝骨白、葉＝綠；枯萎時變褐。光帶緩緩脈動
            var mpb = PlantFx.Mpb;
            mpb.Clear();
            mpb.SetColor(PlantFx.ColorId, Color.Lerp(VineC, DeadC, w));
            mr.SetPropertyBlock(mpb, 0);
            mpb.SetColor(PlantFx.ColorId, Color.Lerp(ThornC, DeadC, w));
            mr.SetPropertyBlock(mpb, 1);
            mpb.SetColor(PlantFx.ColorId, Color.Lerp(LeafC, DeadC, w));
            mr.SetPropertyBlock(mpb, 2);
            float ga = (0.16f + 0.06f * Mathf.Sin(time * 5f)) * (1f - w);
            var gc = SpellFx.WithA(GlowC, ga);
            glow.startColor = gc; glow.endColor = gc;
            var sc = new Color(0.12f, 0.08f, 0.04f, 0.55f * (1f - w));
            scar.startColor = sc; scar.endColor = sc;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }

    // ==================================================================== 毒液區元件
    /// <summary>
    /// 毒液區：不規則的一攤毒液（中心亮綠、往外轉成紫色、邊緣透明，旁邊幾滴小飛濺），上面一層疊加的綠光，
    /// 冒泡（泡泡長大後破掉）、飄出淡淡的毒氣。一開始快速攤開，平常微微脈動，最後淡出後 Destroy。
    /// </summary>
    internal class PlantPool : MonoBehaviour
    {
        float radius, life, fadeDur, time;
        Transform disc, glowT;
        MeshRenderer discR, glowR;
        ParticleSystem bubbles, fumes;
        Mesh discMesh, glowMesh;
        bool stopped;

        public void Setup(float radius, float life, Material spriteMat)
        {
            this.radius = radius;
            this.life = Mathf.Max(0.2f, life);
            fadeDur = Mathf.Clamp(this.life * 0.3f, 0.15f, 0.6f);

            // ---- 毒液面（Sprites/Default：頂點色）
            discMesh = PuddleMesh(radius);
            disc = new GameObject("Puddle").transform;
            disc.SetParent(transform, false);
            disc.gameObject.AddComponent<MeshFilter>().sharedMesh = discMesh;
            discR = disc.gameObject.AddComponent<MeshRenderer>();
            discR.sharedMaterial = spriteMat;
            NoShadow(discR);

            // ---- 疊加的綠光（柔和圓形貼圖的方片）
            glowMesh = GlowQuad(radius * 1.35f);
            glowT = new GameObject("Glow").transform;
            glowT.SetParent(transform, false);
            glowT.localPosition = Vector3.up * 0.01f;
            glowT.gameObject.AddComponent<MeshFilter>().sharedMesh = glowMesh;
            glowR = glowT.gameObject.AddComponent<MeshRenderer>();
            glowR.sharedMaterial = SpellFx.AddMat(SpellFx.TexSoft);
            NoShadow(glowR);

            // ---- 泡泡：在毒液面上隨機冒出、長大、破掉
            var flat = new GameObject("Surface").transform;
            flat.SetParent(transform, false);
            flat.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 粒子的圓盤形狀（本地 XY）平躺
            float area = radius * radius;
            bubbles = SpellFx.NewPS(flat, "Bubbles", SpellFx.AddMat(SpellFx.TexSoft), Mathf.Clamp(Mathf.CeilToInt(40f * area) + 8, 10, 60), true);
            {
                var m = bubbles.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.075f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 1f, 0.3f, 0.9f), new Color(0.8f, 0.45f, 1f, 0.8f));
                SpellFx.Disc(bubbles, radius * 0.8f);
                SpellFx.SizeCurve(bubbles, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.8f, 1f), new Keyframe(0.9f, 1.3f), new Keyframe(1f, 0f)));
                SpellFx.Rise(bubbles, 0.04f);
                SpellFx.Rate(bubbles, Mathf.Clamp(28f * area + 4f, 4f, 40f));
                bubbles.Play();
            }
            fumes = SpellFx.NewPS(flat, "Fumes", SpellFx.BlendMat(SpellFx.TexSoft), 16, true);
            {
                var m = fumes.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                m.startSpeed = 0f;
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.75f, 0.2f, 0.18f), new Color(0.5f, 0.3f, 0.6f, 0.16f));
                SpellFx.Disc(fumes, radius * 0.7f);
                SpellFx.FadeInOut(fumes);
                SpellFx.SizeCurve(fumes, SpellFx.Grow);
                SpellFx.Rise(fumes, 0.18f);
                SpellFx.Noise(fumes, 0.1f, 0.8f);
                SpellFx.Rate(fumes, Mathf.Clamp(3f * radius + 1f, 1f, 8f));
                fumes.Play();
            }
            Apply(0f);
        }

        static void NoShadow(Renderer r)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>不規則的毒液面（本地 XZ，y = 0）：中心亮綠 → 綠 → 紫 → 透明邊緣；外面幾滴小飛濺。</summary>
        static Mesh PuddleMesh(float radius)
        {
            const int n = 32;
            float p1 = Random.Range(0f, 6.3f), p2 = Random.Range(0f, 6.3f), p3 = Random.Range(0f, 6.3f);
            float[] rr = { 0.42f, 0.78f, 0.93f, 1.04f };
            var cc = new[]
            {
                new Color(0.55f, 1f, 0.25f, 0.92f), new Color(0.25f, 0.62f, 0.1f, 0.9f),
                new Color(0.42f, 0.16f, 0.55f, 0.88f), new Color(0.55f, 0.25f, 0.7f, 0.55f), new Color(0.5f, 0.2f, 0.65f, 0f),
            };
            var v = new List<Vector3>(); var col = new List<Color>(); var tri = new List<int>();
            v.Add(Vector3.zero); col.Add(cc[0]);
            for (int ri = 0; ri < rr.Length; ri++)
                for (int i = 0; i < n; i++)
                {
                    float a = 2f * Mathf.PI * i / n;
                    float edge = 1f + 0.12f * Mathf.Sin(3f * a + p1) + 0.07f * Mathf.Sin(5f * a + p2) + 0.05f * Mathf.Sin(8f * a + p3);
                    float r = radius * rr[ri] * (ri == 0 ? 1f : edge);
                    v.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    col.Add(cc[ri + 1]);
                }
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                tri.Add(0); tri.Add(1 + i1); tri.Add(1 + i);
            }
            for (int ri = 0; ri < rr.Length - 1; ri++)
                for (int i = 0; i < n; i++)
                {
                    int i1 = (i + 1) % n;
                    int a0 = 1 + ri * n + i, a1 = 1 + ri * n + i1, b0 = a0 + n, b1 = a1 + n;
                    tri.Add(a0); tri.Add(a1); tri.Add(b1);
                    tri.Add(a0); tri.Add(b1); tri.Add(b0);
                }
            // 外圍幾滴小飛濺
            for (int d = 0; d < 6; d++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = radius * Random.Range(1.1f, 1.3f), s = radius * Random.Range(0.04f, 0.09f);
                var c0 = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                int start = v.Count;
                v.Add(c0); col.Add(new Color(0.45f, 0.9f, 0.2f, 0.85f));
                for (int i = 0; i < 8; i++)
                {
                    float b = 2f * Mathf.PI * i / 8;
                    v.Add(c0 + new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * s); col.Add(new Color(0.45f, 0.2f, 0.6f, 0.6f));
                }
                for (int i = 0; i < 8; i++) { tri.Add(start); tri.Add(start + 1 + (i + 1) % 8); tri.Add(start + 1 + i); }
            }
            var m = new Mesh { name = "PlantPuddle" };
            m.SetVertices(v);
            m.SetColors(col);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>水平方片（本地 XZ），UV 0..1 對應柔和圓形貼圖。</summary>
        static Mesh GlowQuad(float r)
        {
            var m = new Mesh { name = "PlantPoolGlow" };
            m.SetVertices(new List<Vector3> { new Vector3(-r, 0f, -r), new Vector3(-r, 0f, r), new Vector3(r, 0f, r), new Vector3(r, 0f, -r) });
            m.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
            m.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            m.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            time += Time.deltaTime;
            if (time >= life) { Destroy(gameObject); return; }
            if (!stopped && time >= life - fadeDur)
            {
                stopped = true;
                SpellFx.Rate(bubbles, 0f);
                SpellFx.Rate(fumes, 0f);
            }
            Apply(time);
        }

        void Apply(float t)
        {
            float spread = t < 0.35f ? PlantFx.BackOut(t / 0.35f) : 1f;
            float fade = Mathf.Clamp01((life - t) / fadeDur);
            float pulse = 1f + 0.03f * Mathf.Sin(t * 3.1f) + 0.02f * Mathf.Sin(t * 5.3f + 1f);
            float s = Mathf.Max(0.001f, spread * pulse);
            disc.localScale = new Vector3(s, 1f, s);
            glowT.localScale = new Vector3(s, 1f, s);
            var mpb = PlantFx.Mpb;
            mpb.Clear();
            mpb.SetColor(PlantFx.ColorId, new Color(1f, 1f, 1f, fade));
            discR.SetPropertyBlock(mpb);
            mpb.Clear();
            float ga = (0.3f + 0.1f * Mathf.Sin(t * 4f)) * fade * Mathf.Clamp01(spread);
            mpb.SetColor(PlantFx.TintId, new Color(0.3f, 0.75f, 0.2f, ga));
            glowR.SetPropertyBlock(mpb);
        }

        void OnDestroy()
        {
            if (discMesh != null) Destroy(discMesh);
            if (glowMesh != null) Destroy(glowMesh);
        }
    }

    // ==================================================================== 生長動畫
    /// <summary>一次性的「長出來 → 停住 → 縮回去」縮放動畫（嫩芽等）；killAt > 0 時時間到 Destroy 自己的 GameObject。</summary>
    internal class PlantGrowAnim : MonoBehaviour
    {
        struct Item { public Transform t; public Vector3 s; public float delay, grow, hold, shrink; }
        readonly List<Item> items = new List<Item>();
        public float killAt;
        float time;

        public void Add(Transform t, float delay, float grow, float hold, float shrink)
        {
            items.Add(new Item { t = t, s = Vector3.one, delay = delay, grow = Mathf.Max(0.01f, grow), hold = hold, shrink = Mathf.Max(0.01f, shrink) });
            t.localScale = Vector3.zero;
        }

        void Update()
        {
            time += Time.deltaTime;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.t == null) continue;
                float k = time - it.delay, e;
                if (k <= 0f) e = 0f;
                else if (k < it.grow) e = PlantFx.BackOut(k / it.grow);
                else if (k < it.grow + it.hold) e = 1f;
                else e = 1f - PlantFx.Smooth01((k - it.grow - it.hold) / it.shrink);
                it.t.localScale = it.s * Mathf.Max(0f, e);
            }
            if (killAt > 0f && time >= killAt) Destroy(gameObject);
        }
    }
}
