using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 海洋小怪的攻擊特效（全部程式產生，不用任何素材檔）。呼叫者傳入的都是 Unity 世界座標（已換算好的 AR 座標），世界 +Y 朝上。
    ///   Splash   ：水花（魚躍出／落水、海鳥俯衝落地）。at＝水面／地面上的點，size≈1 為一般大小，約 1.4 秒。
    ///   Shock    ：水母放電。at＝地面中心點；以它為中心、半徑 radius 的電光球（半埋在地面上的電光圓頂）＋地面往外竄的電弧，約 0.6 秒。
    ///   InkSplat ：墨汁落地。at＝地面中心點；黑紫色墨汁炸開、地上留下一攤墨漬後淡出，約 1.2 秒。
    ///   BubblePop：泡泡破掉。at＝泡泡中心（空中），size＝泡泡直徑，約 0.6 秒。
    /// 全部自動 Destroy。只用保證會進 build 的 shader：Legacy Particles（粒子、光環）、Sprites/Default（墨漬面、電光球）。
    /// </summary>
    public static class SeaFx
    {
        static readonly Color Foam = new Color(0.93f, 0.97f, 1f);
        static readonly Color Water = new Color(0.35f, 0.75f, 1f);
        static readonly Color ZapCyan = new Color(0.5f, 0.9f, 1f);
        static readonly Color ZapViolet = new Color(0.75f, 0.5f, 1f);
        static readonly Color InkA = new Color(0.07f, 0.03f, 0.1f);
        static readonly Color InkB = new Color(0.22f, 0.07f, 0.3f);

        static Material spriteMat;
        static Mesh splatMesh;
        static MaterialPropertyBlock mpb;
        internal static readonly int ColorId = Shader.PropertyToID("_Color");

        internal static MaterialPropertyBlock Mpb { get { Init(); return mpb; } }

        static void Init()
        {
            if (spriteMat != null) return;
            spriteMat = new Material(Shader.Find("Sprites/Default")) { color = Color.white, name = "SeaFxSprite" };   // 顏色由 PropertyBlock 給
            mpb = new MaterialPropertyBlock();
            splatMesh = SplatMesh();
        }

        /// <summary>墨漬：本地 XY 平面上、半徑約 1 的不規則墨團（一圈尖尖的噴濺）＋周圍幾滴小墨點。</summary>
        static Mesh SplatMesh()
        {
            var m = new PMesh(1);
            const int n = 28;
            var c = Color.white;
            for (int i = 0; i < n; i++)
            {
                float a0 = 2f * Mathf.PI * i / n, a1 = 2f * Mathf.PI * (i + 1) / n;
                m.Tri(Vector3.zero, Rim(a0, i), Rim(a1, i + 1), c);
            }
            for (int k = 0; k < 7; k++)
            {
                float a = k * 0.9f + 0.4f, r = 0.95f + 0.2f * Mathf.Sin(k * 2.7f), s = 0.07f + 0.05f * Mathf.Abs(Mathf.Sin(k * 1.9f));
                var ctr = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                for (int j = 0; j < 6; j++)
                {
                    float b0 = j * Mathf.PI / 3f, b1 = (j + 1) * Mathf.PI / 3f;
                    m.Tri(ctr, ctr + new Vector3(Mathf.Cos(b0), Mathf.Sin(b0), 0f) * s, ctr + new Vector3(Mathf.Cos(b1), Mathf.Sin(b1), 0f) * s, c);
                }
            }
            return m.Build("SeaSplat", false);
        }

        static Vector3 Rim(float a, int i)
        {
            float r = 0.62f + 0.08f * Mathf.Sin(a * 3f + 0.7f) + 0.06f * Mathf.Sin(a * 7f + 2.1f);
            if ((i & 1) == 1) r += 0.12f + 0.14f * Mathf.Abs(Mathf.Sin(i * 1.37f));   // 噴濺的尖端
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }

        /// <summary>地面特效的根物件：本地 XY 平面 = 地面（本地 +Z 朝下）。</summary>
        static Transform Ground(string name, Vector3 at, float life, out FxAnim anim)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
            anim = go.AddComponent<FxAnim>();
            anim.killAt = life;
            return go.transform;
        }

        /// <summary>根物件底下 +Z 朝上的子物件（粒子往上噴用）。</summary>
        static Transform Up(Transform root)
        {
            var t = new GameObject("Up").transform;
            t.SetParent(root, false);
            t.localRotation = Quaternion.Euler(180f, 0f, 0f);
            return t;
        }

        static void Flash(Transform parent, Color c, float size, float life)
        {
            var flash = SpellFx.NewPS(parent, "Flash", SpellFx.AddMat(SpellFx.TexSoft), 2, false);
            var m = flash.main;
            m.startLifetime = life;
            m.startSize = size;
            m.startColor = c;
            SpellFx.FadeOut(flash);
            SpellFx.SizeCurve(flash, SpellFx.PopCurve);
            flash.Play(); flash.Emit(1);
        }

        // ================================================================ 水花
        /// <summary>水花：白色泡沫環往外擴、皇冠狀往上噴的水滴、白色水團與水霧，約 1.4 秒後自己清除。</summary>
        public static void Splash(Vector3 at, float size)
        {
            Init();
            size = Mathf.Max(0.2f, size);
            float sp = Mathf.Sqrt(size);
            var root = Ground("Sea Splash", at, 1.4f, out var anim);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.1f * size, Foam, 0f, 0.7f, 0.15f * size, 0.8f * size, false);
            anim.AddRing(root, SpellFx.Zigzag(28, 0.8f, 1f), true, 0.05f * size, Foam, 0f, 0.55f, 0.1f * size, 0.55f * size, false, 40f);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.05f * size, Water, 0.05f, 0.8f, 0.2f * size, 1.1f * size);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.04f * size, Foam, 0.25f, 0.8f, 0.3f * size, 1.3f * size, false);   // 第二道漣漪
            Flash(root, new Color(0.7f, 0.9f, 1f, 0.6f), 1.1f * size, 0.3f);

            var up = Up(root);
            var drops = SpellFx.NewPS(up, "Drops", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            {
                var m = drops.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * sp, 3.2f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.025f * sp, 0.055f * sp);
                m.startColor = new ParticleSystem.MinMaxGradient(Foam, Water);
                m.gravityModifier = 1.2f;
                SpellFx.Cone(drops, 28f, 0.25f * size);   // 皇冠：從一圈往上斜噴
                SpellFx.FadeOut(drops);
                SpellFx.Stretch(drops, 0.05f, 1.3f);
                drops.Play(); drops.Emit(Mathf.RoundToInt(26 + 8 * size));
            }
            var blobs = SpellFx.NewPS(up, "Foam", SpellFx.BlendMat(SpellFx.TexSoft), 14, true);
            {
                var m = blobs.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * sp, 1.8f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.06f * size, 0.12f * size);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.98f, 1f, 0.85f), new Color(0.75f, 0.9f, 1f, 0.75f));
                m.gravityModifier = 1f;
                SpellFx.Cone(blobs, 45f, 0.15f * size);
                SpellFx.FadeOut(blobs);
                blobs.Play(); blobs.Emit(10);
            }
            var mist = SpellFx.NewPS(up, "Mist", SpellFx.BlendMat(SpellFx.TexSoft), 8, true);
            {
                var m = mist.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * sp, 0.7f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.35f * size, 0.6f * size);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.95f, 1f, 0.28f), new Color(0.6f, 0.85f, 1f, 0.22f));
                SpellFx.Cone(mist, 55f, 0.25f * size);
                SpellFx.FadeOut(mist);
                SpellFx.SizeCurve(mist, SpellFx.Grow);
                SpellFx.Drag(mist, 2f);
                mist.Play(); mist.Emit(6);
            }
        }

        // ================================================================ 水母放電
        /// <summary>水母放電：半徑 radius 的電光球（青紫色、內有白色核心、表面劈啪的電弧）＋地面往外竄的電弧與衝擊環，約 0.6 秒後自己清除。</summary>
        public static void Shock(Vector3 at, float radius)
        {
            Init();
            radius = Mathf.Max(0.2f, radius);
            var root = Ground("Sea Shock", at, 0.75f, out var anim);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.07f * radius, ZapCyan, 0f, 0.4f, 0.3f * radius, 1.1f * radius);
            anim.AddRing(root, SpellFx.Zigzag(30, 0.82f, 1f), true, 0.05f * radius, ZapViolet, 0.04f, 0.5f, 0.6f * radius, 1f * radius, true, 60f);
            anim.AddRing(root, SpellFx.Circle(32, 1f), true, 0.3f * radius, new Color(0.06f, 0.04f, 0.15f, 0.45f), 0f, 0.6f, 0.4f * radius, 0.75f * radius, false);   // 焦黑的地面

            // 世界朝向的掛點（電光球、電弧都用世界方向的本地座標）
            var w = new GameObject("World").transform;
            w.SetParent(root, false);
            w.SetPositionAndRotation(at, Quaternion.identity);
            var ballC = Vector3.up * (radius * 0.45f);
            var ball = new GameObject("Ball").transform;
            ball.SetParent(w, false);
            ball.localPosition = ballC;

            var fx = root.gameObject.AddComponent<SeaFxAnim>();
            // 電光球：半透明外殼（膨脹後淡出）＋白色核心
            var shell = Sphere(ball, "Shell");
            fx.AddFade(shell, Vector3.one * (radius * 1.2f), Vector3.one * (radius * 2f), 0.12f, 0.15f, 0.55f, new Color(0.55f, 0.8f, 1f, 0.35f));
            var coreS = Sphere(ball, "Core");
            fx.AddFade(coreS, Vector3.one * (radius * 0.2f), Vector3.one * (radius * 0.75f), 0.06f, 0.08f, 0.4f, new Color(0.92f, 0.97f, 1f, 0.85f));
            Flash(ball, new Color(0.6f, 0.85f, 1f, 0.85f), 2.6f * radius, 0.35f);

            // 電弧：球面上的弦、球往地面的落雷、地面往外竄的放射狀電弧（每 0.04 秒重新折一次）
            for (int i = 0; i < 5; i++)
            {
                var a = ballC + Random.onUnitSphere * radius * 0.85f;
                var b = ballC + Random.onUnitSphere * radius * 0.85f;
                fx.AddZap(w, a, b, 7, 0.12f * radius, 0.015f + 0.008f * radius, i % 2 == 0 ? ZapCyan : ZapViolet, Random.Range(0f, 0.12f), Random.Range(0.25f, 0.45f));
            }
            for (int i = 0; i < 7; i++)
            {
                float ang = (i * 360f / 7f + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var a = Vector3.up * 0.02f + d * radius * 0.15f;
                var b = Vector3.up * 0.02f + d * radius * Random.Range(0.75f, 1.1f);
                fx.AddZap(w, a, b, 8, 0.1f * radius, 0.02f + 0.01f * radius, i % 2 == 0 ? Color.white : ZapCyan, Random.Range(0f, 0.15f), Random.Range(0.3f, 0.45f), true);
            }
            for (int i = 0; i < 3; i++)
            {
                var d = Random.insideUnitCircle * radius * 0.8f;
                fx.AddZap(w, ballC + new Vector3(d.x, 0f, d.y) * 0.3f, new Vector3(d.x, 0.02f, d.y), 6, 0.08f * radius, 0.02f + 0.008f * radius, ZapViolet, Random.Range(0.02f, 0.2f), 0.25f);
            }

            var sparks = SpellFx.NewPS(ball, "Sparks", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            {
                var m = sparks.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * radius, 4f * radius);
                m.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
                m.startColor = new ParticleSystem.MinMaxGradient(ZapCyan, Color.white);
                m.gravityModifier = 0.4f;
                SpellFx.Sphere(sparks, radius * 0.6f, 0.2f);
                SpellFx.FadeOut(sparks);
                SpellFx.Stretch(sparks, 0.04f, 1.5f);
                SpellFx.Drag(sparks, 3f);
                sparks.Play(); sparks.Emit(34);
            }
        }

        // ================================================================ 墨汁
        /// <summary>墨汁落地：黑紫色墨汁炸開（四濺的墨滴＋黑霧），地上留下半徑 radius 的墨漬後淡出，約 1.2 秒後自己清除。</summary>
        public static void InkSplat(Vector3 at, float radius)
        {
            Init();
            radius = Mathf.Max(0.15f, radius);
            float sp = Mathf.Sqrt(radius);
            var root = Ground("Sea Ink Splat", at, 1.25f, out var anim);
            anim.AddRing(root, SpellFx.Circle(36, 1f), true, 0.1f * radius, new Color(0.12f, 0.04f, 0.18f, 0.85f), 0f, 0.5f, 0.3f * radius, 1.15f * radius, false);
            anim.AddRing(root, SpellFx.Zigzag(22, 0.7f, 1f), true, 0.04f * radius, new Color(0.6f, 0.3f, 0.9f, 0.7f), 0.03f, 0.5f, 0.2f * radius, 0.9f * radius, true, -70f);

            // 地上的墨漬（隨機轉個角度）：快速攤開 → 停留 → 淡出
            var splat = new GameObject("Splat");
            var st = splat.transform;
            st.SetParent(root, false);
            st.localPosition = new Vector3(0f, 0f, -0.004f);
            st.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            splat.AddComponent<MeshFilter>().sharedMesh = splatMesh;
            var sr = splat.AddComponent<MeshRenderer>();
            sr.sharedMaterial = spriteMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            var fx = root.gameObject.AddComponent<SeaFxAnim>();
            fx.AddFade(sr, Vector3.one * (0.25f * radius), Vector3.one * radius, 0.14f, 0.6f, 1.2f, new Color(0.09f, 0.03f, 0.13f, 0.88f));
            Flash(root, new Color(0.5f, 0.2f, 0.8f, 0.35f), 1.2f * radius, 0.25f);

            var up = Up(root);
            var drops = SpellFx.NewPS(up, "InkDrops", SpellFx.BlendMat(SpellFx.TexSoft), 34, true);
            {
                var m = drops.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * sp, 2.8f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(InkA.r, InkA.g, InkA.b, 0.95f), new Color(InkB.r, InkB.g, InkB.b, 0.95f));
                m.gravityModifier = 1.3f;
                SpellFx.Cone(drops, 60f, 0.15f * radius);
                SpellFx.FadeOut(drops);
                SpellFx.Stretch(drops, 0.04f, 1.2f);
                drops.Play(); drops.Emit(28);
            }
            var blobs = SpellFx.NewPS(up, "InkBlobs", SpellFx.BlendMat(SpellFx.TexSoft), 10, true);
            {
                var m = blobs.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.7f * sp, 1.5f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.1f * radius, 0.18f * radius);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(InkA.r, InkA.g, InkA.b, 0.9f), new Color(InkB.r, InkB.g, InkB.b, 0.9f));
                m.gravityModifier = 1.2f;
                SpellFx.Cone(blobs, 40f, 0.1f * radius);
                SpellFx.FadeOut(blobs);
                blobs.Play(); blobs.Emit(8);
            }
            var smoke = SpellFx.NewPS(up, "InkMist", SpellFx.BlendMat(SpellFx.TexSoft), 10, true);
            {
                var m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.5f * radius, 0.85f * radius);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.05f, 0.14f, 0.5f), new Color(0.25f, 0.1f, 0.32f, 0.4f));
                SpellFx.Cone(smoke, 70f, 0.35f * radius);
                SpellFx.FadeOut(smoke);
                SpellFx.SizeCurve(smoke, SpellFx.Grow);
                SpellFx.Drag(smoke, 2f);
                smoke.Play(); smoke.Emit(7);
            }
            var glints = SpellFx.NewPS(up, "InkGlints", SpellFx.AddMat(SpellFx.TexSoft), 12, true);   // 墨汁表面的紫色反光
            {
                var m = glints.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * sp, 1.6f * sp);
                m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.25f, 0.85f), new Color(0.8f, 0.55f, 1f));
                m.gravityModifier = 1f;
                SpellFx.Cone(glints, 55f, 0.15f * radius);
                SpellFx.FadeOut(glints);
                glints.Play(); glints.Emit(10);
            }
        }

        // ================================================================ 泡泡破掉
        /// <summary>泡泡破掉：面向鏡頭的一圈白色破裂環＋往外噴的細小水珠＋幾顆往上飄的小泡泡，約 0.6 秒後自己清除。</summary>
        public static void BubblePop(Vector3 at, float size)
        {
            Init();
            size = Mathf.Max(0.05f, size);
            var cam = Camera.main;
            var rot = cam != null ? Quaternion.LookRotation(at - cam.transform.position) : Quaternion.identity;
            var go = new GameObject("Sea Bubble Pop");
            go.transform.SetPositionAndRotation(at, rot);   // 本地 XY 平面面向鏡頭
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 0.7f;
            var root = go.transform;
            float r = size * 0.5f;
            anim.AddRing(root, SpellFx.Circle(28, 1f), true, 0.06f * size, Foam, 0f, 0.22f, 0.9f * r, 1.5f * r);
            anim.AddRing(root, SpellFx.Zigzag(16, 0.6f, 1f), true, 0.04f * size, new Color(0.7f, 0.85f, 1f), 0f, 0.28f, 0.7f * r, 1.7f * r, true, 30f);
            Flash(root, new Color(0.8f, 0.93f, 1f, 0.7f), 1.3f * size, 0.18f);

            var drops = SpellFx.NewPS(root, "Droplets", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            {
                var m = drops.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * size + 0.4f, 2.6f * size + 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.012f + 0.02f * size, 0.025f + 0.035f * size);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.92f, 1f), new Color(1f, 0.85f, 0.97f));   // 泡泡膜的虹彩
                m.gravityModifier = 0.7f;
                SpellFx.Sphere(drops, r * 0.9f, 0.1f);
                SpellFx.FadeOut(drops);
                SpellFx.Stretch(drops, 0.04f, 1.2f);
                SpellFx.Drag(drops, 2.5f);
                drops.Play(); drops.Emit(18);
            }
            var tiny = SpellFx.NewPS(root, "TinyBubbles", SpellFx.AddMat(SpellFx.TexSoft), 8, true);
            {
                var m = tiny.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.015f + 0.02f * size, 0.03f + 0.04f * size);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.9f, 1f, 0.8f), new Color(0.95f, 1f, 1f, 0.9f));
                SpellFx.Sphere(tiny, r * 0.6f);
                SpellFx.FadeInOut(tiny);
                SpellFx.Rise(tiny, 0.25f);
                tiny.Play(); tiny.Emit(5);
            }
        }

        // ================================================================ 小工具
        static Renderer Sphere(Transform parent, string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.zero;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = spriteMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }
    }

    /// <summary>海洋特效的小動畫：零件的「攤開 → 停住 → 淡出」（PropertyBlock 透明度）、亂折的電弧（每 0.04 秒重新折一次）。自己不負責 Destroy（由同物件的 FxAnim 清除）。</summary>
    internal class SeaFxAnim : MonoBehaviour
    {
        struct Fade { public Renderer r; public Vector3 s0, s1; public float grow, fadeStart, fadeEnd; public Color c; }
        struct Zap { public LineRenderer lr; public Vector3 a, b; public int n; public float amp, w, delay, life; public Color c; public bool flat; public Vector3[] pts; }
        readonly List<Fade> fades = new List<Fade>();
        readonly List<Zap> zaps = new List<Zap>();
        float time, rejag;

        public void AddFade(Renderer r, Vector3 s0, Vector3 s1, float grow, float fadeStart, float fadeEnd, Color c)
        {
            fades.Add(new Fade { r = r, s0 = s0, s1 = s1, grow = Mathf.Max(0.01f, grow), fadeStart = fadeStart, fadeEnd = Mathf.Max(fadeStart + 0.01f, fadeEnd), c = c });
            r.transform.localScale = s0;
            Apply(r, c, 1f);
        }

        /// <summary>電弧：parent 本地座標 a → b、n 個折點、橫向亂折幅度 amp；flat＝貼地的電弧（只在水平方向亂折、稍微離地）。</summary>
        public void AddZap(Transform parent, Vector3 a, Vector3 b, int n, float amp, float width, Color c, float delay, float life, bool flat = false)
        {
            n = Mathf.Max(2, n);
            var pts = new Vector3[n];
            var lr = SpellFx.Line(parent, "Zap", pts, false, width, c, true, false);
            lr.widthCurve = SpellFx.ArcTaper;
            lr.enabled = false;
            var z = new Zap { lr = lr, a = a, b = b, n = n, amp = amp, w = width, delay = delay, life = Mathf.Max(0.05f, life), c = c, flat = flat, pts = pts };
            Jag(z);
            zaps.Add(z);
        }

        static void Jag(Zap z)
        {
            var d = z.b - z.a;
            var side = Vector3.Cross(d, Vector3.up);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
            side.Normalize();
            var side2 = Vector3.Cross(d.normalized, side);
            for (int i = 0; i < z.n; i++)
            {
                float t = (float)i / (z.n - 1);
                var p = Vector3.Lerp(z.a, z.b, t);
                if (i > 0 && i < z.n - 1)
                {
                    p += side * Random.Range(-z.amp, z.amp);
                    if (z.flat) p.y += Random.Range(0f, z.amp * 0.4f);
                    else p += side2 * Random.Range(-z.amp, z.amp);
                }
                z.pts[i] = p;
            }
            z.lr.SetPositions(z.pts);
        }

        static void Apply(Renderer r, Color c, float alphaK)
        {
            var mpb = SeaFx.Mpb;
            mpb.Clear();
            c.a *= alphaK;
            mpb.SetColor(SeaFx.ColorId, c);
            r.SetPropertyBlock(mpb);
        }

        void Update()
        {
            time += Time.deltaTime;
            for (int i = 0; i < fades.Count; i++)
            {
                var f = fades[i];
                if (f.r == null) continue;
                float g = Mathf.Clamp01(time / f.grow);
                g = 1f - (1f - g) * (1f - g);
                f.r.transform.localScale = Vector3.LerpUnclamped(f.s0, f.s1, g);
                float k = 1f - Mathf.Clamp01((time - f.fadeStart) / (f.fadeEnd - f.fadeStart));
                Apply(f.r, f.c, k * k);
            }
            rejag -= Time.deltaTime;
            bool doJag = rejag <= 0f;
            if (doJag) rejag = 0.04f;
            for (int i = 0; i < zaps.Count; i++)
            {
                var z = zaps[i];
                if (z.lr == null) continue;
                float k = (time - z.delay) / z.life;
                if (k < 0f || k >= 1f) { if (z.lr.enabled) z.lr.enabled = false; continue; }
                if (!z.lr.enabled) z.lr.enabled = true;
                if (doJag) Jag(z);
                float flick = 0.55f + 0.45f * Random.value;   // 電弧閃爍
                var c = z.c; c.a *= (1f - k) * flick;
                z.lr.startColor = c; z.lr.endColor = c;
                z.lr.widthMultiplier = z.w * (0.7f + 0.6f * Random.value);
            }
        }
    }
}
