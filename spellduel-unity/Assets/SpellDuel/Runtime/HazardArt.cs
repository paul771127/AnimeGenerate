using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 陷阱／機關的造型（全部用程式產生，不需要任何圖檔）：巨大鐘擺斧、地刺機關、落石、落石著地的塵土。
    /// 每個建構函式都回傳一個「在原點」的新 GameObject，由呼叫端決定位置與方向。
    /// 材質用 ToonStyle 的賽璐珞著色器（跟小怪、Boss 一樣有黑邊＋兩階明暗）；每個模型都在 3000 頂點以內。
    /// </summary>
    public static partial class HazardArt
    {
        static Material steelMat, edgeMat, ironMat, woodMat, plateMat, rockMat, brassMat;
        static Mesh pebbleMesh;

        static Material Toon(ref Material cache, string name, Color c, Texture2D tex = null)
        {
            if (cache != null) return cache;
            cache = new Material(ToonStyle.LitShader()) { name = "Hazard " + name, color = c };
            if (tex != null) cache.mainTexture = tex;
            return cache;
        }
        static Material Steel => Toon(ref steelMat, "Steel", new Color(0.62f, 0.66f, 0.72f));
        static Material Edge => Toon(ref edgeMat, "Edge", new Color(0.96f, 0.98f, 1f));
        static Material Iron => Toon(ref ironMat, "Iron", new Color(0.22f, 0.21f, 0.23f));
        static Material Wood => Toon(ref woodMat, "Wood", new Color(0.5f, 0.34f, 0.2f), ArtKit.WoodTex());
        static Material Brass => Toon(ref brassMat, "Brass", new Color(0.78f, 0.6f, 0.28f));
        static Material Plate => Toon(ref plateMat, "SpikePlate", Color.white, SpikePlateTex());
        static Material Rock => Toon(ref rockMat, "Boulder", Color.white, BoulderTex());

        static GameObject Part(Transform parent, string name, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // ================================================================ 鐘擺斧
        /// <summary>
        /// 巨大鐘擺斧。根物件在地面中心；本地 +X＝擺動方向。
        /// 龍門架：兩根柱子在本地 z＝±1.4，高到 pivotHeight；橫樑沿 Z 軸架在 pivotHeight。
        /// <paramref name="swing"/> 是位在本地 (0, pivotHeight, 0) 的子物件，斧刃在它下方 length 處。
        /// 擺動角 θ（度，正值＝往本地 +X）請用 <c>swing.localRotation = HazardArt.AxeSwingRotation(θ)</c>
        /// （＝ Quaternion.Euler(0, 0, θ)）：斧刃中心會在本地 (length·sinθ, pivotHeight − length·cosθ, 0)。
        /// 斧刃是躺在 X–Y 平面的雙刃新月（寬約 1.2m、厚 0.15m），刃口朝 ±X。地上的紅色危險帶不在這裡畫。
        /// </summary>
        public static GameObject Axe(float pivotHeight, float length, out Transform swing)
        {
            pivotHeight = Mathf.Max(1f, pivotHeight);
            length = Mathf.Clamp(length, 0.5f, pivotHeight - 0.2f);
            var root = new GameObject("PendulumAxe");

            // ---- 龍門架：木柱＋鐵箍＋斜撐＋底座，橫樑沿 Z
            {
                var mb = new ArtKit.MB(2);   // 0＝木頭、1＝鐵
                foreach (float z in new[] { -1.4f, 1.4f })
                {
                    ArtKit.Box(mb, 0, new Vector3(0f, (pivotHeight + 0.2f) * 0.5f, z), new Vector3(0.22f, pivotHeight + 0.2f, 0.22f), Quaternion.identity);
                    ArtKit.Box(mb, 1, new Vector3(0f, 0.06f, z), new Vector3(0.5f, 0.12f, 0.5f), Quaternion.identity);   // 鐵底座
                    for (int k = 1; k <= 3; k++)
                        ArtKit.Box(mb, 1, new Vector3(0f, pivotHeight * k / 3.6f, z), new Vector3(0.25f, 0.06f, 0.25f), Quaternion.identity);   // 鐵箍
                    // 斜撐（沿 X 兩側各一根）
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        float bl = 1.1f;
                        var c = new Vector3(sx * 0.42f, 0.4f, z);
                        ArtKit.Box(mb, 0, c, new Vector3(0.1f, bl, 0.1f), Quaternion.Euler(0f, 0f, sx * 40f));
                    }
                }
                ArtKit.Box(mb, 0, new Vector3(0f, pivotHeight + 0.05f, 0f), new Vector3(0.26f, 0.3f, 3.2f), Quaternion.identity);   // 橫樑
                ArtKit.Box(mb, 1, new Vector3(0f, pivotHeight + 0.22f, 0f), new Vector3(0.3f, 0.05f, 3.3f), Quaternion.identity);   // 樑上鐵條
                Part(root.transform, "Gantry", mb.Build("AxeGantry"), Wood, Iron);
            }

            // ---- 擺動的部分
            var sw = new GameObject("Swing").transform;
            sw.SetParent(root.transform, false);
            sw.localPosition = new Vector3(0f, pivotHeight, 0f);
            swing = sw;
            {
                var mb = new ArtKit.MB(3);   // 0＝鐵、1＝鋼、2＝亮刃
                // 軸心：沿 Z 的鐵筒
                ArtKit.Lathe(mb, 0, new Vector3(0f, 0f, -0.2f), Quaternion.Euler(90f, 0f, 0f), new[] { 0f, 0.4f }, new[] { 0.09f, 0.09f }, 10, true, true);
                // 吊桿：鐵棒＋幾個鐵環
                float rodEnd = -length + 0.22f;
                ArtKit.Lathe(mb, 0, Vector3.zero, Quaternion.Euler(180f, 0f, 0f), new[] { 0f, -rodEnd }, new[] { 0.045f, 0.045f }, 8, false, false);
                for (int k = 1; k <= 3; k++)
                {
                    float y = rodEnd * k / 4f;
                    ArtKit.Lathe(mb, 0, new Vector3(0f, y - 0.04f, 0f), Quaternion.identity, new[] { 0f, 0.02f, 0.06f, 0.08f }, new[] { 0.06f, 0.075f, 0.075f, 0.06f }, 8, false, false);
                }
                // 刃座：沿 Z 的短圓筒（夾住斧刃）
                ArtKit.Lathe(mb, 1, new Vector3(0f, -length + 0.08f, -0.13f), Quaternion.Euler(90f, 0f, 0f), new[] { 0f, 0.26f }, new[] { 0.13f, 0.13f }, 10, true, true);
                AxeBlade(mb, 1, 2, new Vector3(0f, -length, 0f));
                Part(sw, "Blade", mb.Build("AxeSwing"), Iron, Steel, Edge);
            }
            return root;
        }

        /// <summary>給 Axe 的 swing 用的本地旋轉：θ 度（正＝往本地 +X）→ 斧刃在 (L·sinθ, −L·cosθ)（相對軸心）。</summary>
        public static Quaternion AxeSwingRotation(float thetaDeg) => Quaternion.Euler(0f, 0f, thetaDeg);

        /// <summary>
        /// 雙刃新月斧刃（X–Y 平面、厚度沿 Z）：外弧（下緣）與兩個尖角是亮色的刃口，內弧（上緣）是鈍的刀背。
        /// c＝斧刃中心；尖角在 (±0.6, +0.15)，外弧最低點 (0, −0.42)，內弧最低點 (0, −0.04)。
        /// </summary>
        static void AxeBlade(ArtKit.MB mb, int body, int edge, Vector3 c)
        {
            const int n = 16;
            const float W = 0.6f, half = 0.075f;
            var outer = new Vector3[n + 1]; var bevel = new Vector3[n + 1]; var inner = new Vector3[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float ph = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, i / (float)n), sp = Mathf.Sin(ph), cp = Mathf.Cos(ph);
                outer[i] = new Vector3(W * sp, 0.15f - 0.57f * cp, 0f);
                inner[i] = new Vector3(W * sp, 0.15f - 0.19f * cp, 0f);
                // 刃面斜邊：從外弧往內約 0.11m（尖角附近收窄）
                bevel[i] = Vector3.Lerp(outer[i], inner[i], 0.3f);
            }
            var bodyCenter = c + new Vector3(0f, -0.1f, 0f);
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                var z = new Vector3(0f, 0f, sgn * half);
                for (int i = 0; i < n; i++)
                {
                    // 平面（刀身）：bevel ↔ inner
                    ArtKit.QuadAway(mb, body, c + bevel[i] + z, c + bevel[i + 1] + z, c + inner[i + 1] + z, c + inner[i] + z, c + bevel[i] + z - new Vector3(0f, 0f, sgn));
                    // 斜面（刃口）：outer(z=0) ↔ bevel(z=±half)
                    ArtKit.QuadAway(mb, edge, c + outer[i], c + outer[i + 1], c + bevel[i + 1] + z, c + bevel[i] + z, c + bevel[i] - new Vector3(0f, 0f, sgn));
                }
            }
            // 刀背（內弧的厚度面）
            for (int i = 0; i < n; i++)
            {
                var a = c + inner[i] + new Vector3(0f, 0f, -half); var b = c + inner[i + 1] + new Vector3(0f, 0f, -half);
                var d = c + inner[i] + new Vector3(0f, 0f, half); var e = c + inner[i + 1] + new Vector3(0f, 0f, half);
                ArtKit.QuadAway(mb, body, a, b, e, d, bodyCenter + new Vector3(0f, -0.3f, 0f));
            }
        }

        // ================================================================ 地刺機關
        // 地刺的排列（以半徑 1 正規化）：內圈 3 根、外圈 7 根
        static readonly Vector2[] SpikeLayout = BuildSpikeLayout();
        const float SpikeBase = 0.12f;   // 刺的底半徑（× 機關半徑）
        static Vector2[] BuildSpikeLayout()
        {
            var l = new List<Vector2>();
            for (int i = 0; i < 3; i++) { float a = (90f + i * 120f) * Mathf.Deg2Rad; l.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.3f); }
            for (int i = 0; i < 7; i++) { float a = (13f + i * 360f / 7f) * Mathf.Deg2Rad; l.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.68f); }
            return l.ToArray();
        }

        /// <summary>
        /// 地刺機關：與地面齊平的圓形鐵格板（有孔）＋10 根鋼刺（高 0.7m）。
        /// <paramref name="spikes"/> 的 localPosition.y 由遊戲控制：−0.75＝完全收起（刺尖在 y＝−0.05，藏在板子下面）、0＝完全伸出。
        /// AR 的地板是半透明的，收到底時建議 spikes.gameObject.SetActive(false)，避免從斜角隱約看到地下的刺。
        /// 根物件本身的 MeshRenderer 是警告光環（疊加材質），用 <see cref="SetSpikeWarn"/> 控制亮度。
        /// </summary>
        public static GameObject SpikeTile(float radius, out Transform spikes)
        {
            radius = Mathf.Max(0.3f, radius);
            var root = new GameObject("SpikeTile");

            // ---- 警告光環（根物件本身）：每個機關一個材質副本，SetSpikeWarn 只改它的顏色
            {
                var glow = new Material(SpellFx.AddMat(SpellFx.TexSoft)) { name = "Hazard SpikeWarn", mainTexture = WarnTex(), renderQueue = 2960 };
                SetTint(glow, Color.black);
                root.AddComponent<MeshFilter>().sharedMesh = ArtKit.FlatDisc(radius * 1.12f, 40, 0.032f, "SpikeWarn");
                var r = root.AddComponent<MeshRenderer>();
                r.sharedMaterial = glow;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
            }

            // ---- 鐵板：頂面（有孔的貼圖，平面 UV）＋邊緣斜角＋側面
            {
                var mb = new ArtKit.MB(2);
                ArtKit.TopDisc(mb, 0, 0.025f, radius * 0.95f, 40, radius);   // 貼圖 ±1＝機關半徑
                ArtKit.Lathe(mb, 1, Vector3.zero, Quaternion.identity, new[] { -0.08f, 0.0f, 0.018f, 0.025f }, new[] { radius, radius, radius * 0.985f, radius * 0.95f }, 40, false, false);
                // 鉚釘
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 0.89f + Vector3.up * 0.022f;
                    ArtKit.Lathe(mb, 1, p, Quaternion.identity, new[] { 0f, 0.012f, 0.018f }, new[] { 0.025f, 0.02f, 0.004f }, 6, false, false);
                }
                Part(root.transform, "Plate", mb.Build("SpikePlate"), Plate, Iron);
            }

            // ---- 鋼刺
            var sp = new GameObject("Spikes").transform;
            sp.SetParent(root.transform, false);
            sp.localPosition = new Vector3(0f, -0.75f, 0f);
            spikes = sp;
            {
                var mb = new ArtKit.MB(2);
                float b = SpikeBase * radius;
                foreach (var q in SpikeLayout)
                {
                    var p = new Vector3(q.x * radius, -0.02f, q.y * radius);
                    ArtKit.Lathe(mb, 0, p, Quaternion.identity, new[] { 0f, 0.1f, 0.12f }, new[] { b * 1.05f, b * 1.05f, b }, 8, false, false);   // 刺座（鐵）
                    ArtKit.Lathe(mb, 1, p + Vector3.up * 0.12f, Quaternion.identity, new[] { 0f, 0.3f, 0.58f - 0.02f }, new[] { b, b * 0.55f, 0.006f }, 8, true, false);
                }
                // 刺尖高度：-0.02 + 0.12 + 0.56 ≈ 0.66～0.7
                Part(sp, "SpikeCones", mb.Build("SpikeCones"), Iron, Steel);
            }
            return root;
        }

        /// <summary>地刺的警告光環：k＝0 熄滅、1＝最亮的紅橘色。每幀呼叫也沒關係（只改一個材質顏色）。</summary>
        public static void SetSpikeWarn(GameObject tile, float k)
        {
            if (tile == null) return;
            var r = tile.GetComponent<MeshRenderer>();
            if (r == null) return;
            k = Mathf.Clamp01(k);
            bool on = k > 0.004f;
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            var m = r.sharedMaterial;
            if (m == null) return;
            float g = k * (0.75f + 0.25f * Mathf.Sin(Time.time * 18f) * k);   // 越接近觸發越閃
            SetTint(m, new Color(0.5f * g, 0.16f * g, 0.03f * g, 0.5f));
        }

        static void SetTint(Material m, Color c)
        {
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c); else m.color = new Color(c.r * 2f, c.g * 2f, c.b * 2f, 1f);
        }

        // ================================================================ 落石
        /// <summary>
        /// 一顆厚重的砂岩／黃土岩塊（不規則多面體，以原點為中心、半徑約 radius）。
        /// 子物件 "DustTrail" 是移動時拖出的塵土（世界座標粒子，依移動距離發射；不需要可以關掉）。
        /// </summary>
        public static GameObject Boulder(float radius)
        {
            radius = Mathf.Max(0.1f, radius);
            var root = new GameObject("Boulder");
            var mb = new ArtKit.MB(1);
            int seed = Random.Range(1, 9999);
            ArtKit.FacetRock(mb, 0, Vector3.zero, new Vector3(radius * 1.05f, radius * 0.9f, radius), 9, 7, seed, 0.38f, 0f, false, 0.6f / radius);
            var mr = root.AddComponent<MeshRenderer>();
            root.AddComponent<MeshFilter>().sharedMesh = mb.Build("Boulder");
            mr.sharedMaterial = Rock;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var dust = SpellFx.NewPS(root.transform, "DustTrail", SpellFx.BlendMat(SpellFx.TexSoft), 40, true);
            SpellFx.Sphere(dust, radius * 0.6f);
            var m = dust.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.6f, radius * 1.1f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.6f, 0.42f, 0.45f), new Color(0.6f, 0.5f, 0.36f, 0.3f));
            var em = dust.emission; em.enabled = true; em.rateOverDistance = 5f;
            SpellFx.FadeOut(dust);
            SpellFx.SizeCurve(dust, SpellFx.Grow);
            SpellFx.Rise(dust, 0.15f);
            dust.Play();
            return root;
        }

        /// <summary>落石著地：一圈往外滾的塵土＋往上噴的碎石；約 2.2 秒後自動刪除。</summary>
        public static void RockImpact(Vector3 pos, float radius)
        {
            radius = Mathf.Max(0.1f, radius);
            var root = new GameObject("RockImpact");
            root.transform.position = pos;

            // 塵土環：圓形發射區躺平，粒子沿半徑往外
            var dust = SpellFx.NewPS(root.transform, "DustRing", SpellFx.BlendMat(SpellFx.TexSoft), 28, true);
            dust.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dust.transform.localPosition = Vector3.up * 0.1f;
            SpellFx.Disc(dust, radius * 0.4f);
            var dm = dust.main;
            dm.loop = false;
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            dm.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * radius + 0.8f, 3.5f * radius + 1.2f);
            dm.startSize = new ParticleSystem.MinMaxCurve(0.5f * radius + 0.2f, 0.9f * radius + 0.35f);
            dm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            dm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.66f, 0.46f, 0.7f), new Color(0.62f, 0.5f, 0.34f, 0.55f));
            SpellFx.FadeOut(dust);
            SpellFx.SizeCurve(dust, SpellFx.Grow);
            SpellFx.Drag(dust, 3f);
            SpellFx.Rise(dust, 0.25f);
            dust.Play(); dust.Emit(24);

            // 中央的一團塵煙（往上冒）
            var puff = SpellFx.NewPS(root.transform, "DustPuff", SpellFx.BlendMat(SpellFx.TexSoft), 10, true);
            SpellFx.Sphere(puff, radius * 0.5f);
            var pm = puff.main;
            pm.loop = false;
            pm.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.7f);
            pm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            pm.startSize = new ParticleSystem.MinMaxCurve(radius * 1.0f + 0.3f, radius * 1.6f + 0.4f);
            pm.startColor = new Color(0.74f, 0.63f, 0.45f, 0.55f);
            SpellFx.FadeOut(puff);
            SpellFx.SizeCurve(puff, SpellFx.Grow);
            SpellFx.Rise(puff, 0.5f);
            puff.Play(); puff.Emit(8);

            // 碎石：小岩塊網格粒子，往上噴、受重力落下
            var peb = SpellFx.NewPS(root.transform, "Pebbles", Rock, 20, true);
            peb.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 錐形往上
            SpellFx.Cone(peb, 55f, radius * 0.4f);
            var bm = peb.main;
            bm.loop = false;
            bm.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.4f);
            bm.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.2f);
            bm.startSize = new ParticleSystem.MinMaxCurve(0.05f + radius * 0.08f, 0.1f + radius * 0.16f);
            bm.startRotation3D = true;
            bm.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            bm.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            bm.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            bm.gravityModifier = 1.2f;
            var pr = peb.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Mesh;
            pr.mesh = PebbleMesh();
            var ro = peb.rotationOverLifetime; ro.enabled = true; ro.separateAxes = true;
            ro.x = new ParticleSystem.MinMaxCurve(-6f, 6f); ro.y = new ParticleSystem.MinMaxCurve(-6f, 6f); ro.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var col = peb.collision;   // 落地時彈一下（以著地點的高度為地面）
            col.enabled = true; col.type = ParticleSystemCollisionType.Planes; col.SetPlane(0, root.transform);
            col.bounce = 0.35f; col.dampen = 0.5f; col.lifetimeLoss = 0f;
            peb.Play(); peb.Emit(14);

            Object.Destroy(root, 2.2f);
        }

        static Mesh PebbleMesh()
        {
            if (pebbleMesh != null) return pebbleMesh;
            var mb = new ArtKit.MB(1);
            ArtKit.FacetRock(mb, 0, Vector3.zero, new Vector3(0.5f, 0.4f, 0.45f), 5, 4, 77, 0.45f, 0f, false, 1f);
            pebbleMesh = mb.Build("Pebble");
            return pebbleMesh;
        }

        // ================================================================ 貼圖
        static Texture2D spikePlateTex, warnTex, boulderTex;

        /// <summary>鐵格板頂面：深色鐵板、放射狀與同心的格條、刺孔（黑洞＋亮邊）、一點鏽色</summary>
        static Texture2D SpikePlateTex()
        {
            if (spikePlateTex != null) return spikePlateTex;
            const int S = 256; var px = new Color32[S * S];
            float hole = SpikeBase * 1.25f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S * 2f - 1f, v = y / (float)S * 2f - 1f;   // −1～1（對應板子半徑）
                    float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                    float n = ArtKit.Fbm(x / (float)S, y / (float)S, 5, 4, 4);
                    float k = 0.3f + 0.12f * n;
                    // 格條：同心環＋放射條（凸起的亮線、旁邊的暗線）
                    float ringL = Mathf.Abs(Mathf.Repeat(r * 5f, 1f) - 0.5f);
                    float spoke = Mathf.Abs(Mathf.Repeat(a / (Mathf.PI * 2f) * 12f, 1f) - 0.5f) * r;
                    float bar = Mathf.Max(1f - ArtKit.SS(0.03f, 0.06f, Mathf.Abs(ringL - 0.5f) ), 1f - ArtKit.SS(0.01f, 0.025f, Mathf.Abs(spoke - 0.25f * r)));
                    k += 0.12f * bar;
                    var c = new Color(k * 0.95f, k * 0.93f, k, 1f);
                    float rust = ArtKit.SS(0.6f, 0.75f, ArtKit.Fbm(x / (float)S, y / (float)S, 19, 3, 4));
                    c = Color.Lerp(c, new Color(0.42f, 0.22f, 0.12f), rust * 0.55f);
                    // 外緣的危險色條紋（黃黑斜紋，磨損）
                    if (r > 0.82f && r < 0.93f)
                    {
                        float st = Mathf.Repeat(a / (Mathf.PI * 2f) * 24f + r * 3f, 1f);
                        var warn = st < 0.5f ? new Color(0.85f, 0.62f, 0.12f) : new Color(0.08f, 0.07f, 0.07f);
                        c = Color.Lerp(c, warn, 0.75f * (1f - rust * 0.6f));
                    }
                    // 刺孔
                    foreach (var q in SpikeLayout)
                    {
                        float d = Mathf.Sqrt((u - q.x) * (u - q.x) + (v - q.y) * (v - q.y));
                        if (d < hole * 1.5f)
                        {
                            float rim = 1f - ArtKit.SS(0f, hole * 0.35f, Mathf.Abs(d - hole));
                            c = Color.Lerp(c, new Color(0.7f, 0.7f, 0.72f), rim * 0.6f);
                            if (d < hole) c = Color.Lerp(new Color(0.02f, 0.02f, 0.025f), c, ArtKit.SS(hole * 0.8f, hole, d));
                        }
                    }
                    if (r > 0.97f) c *= 0.7f;
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            spikePlateTex = ArtKit.Finish(ArtKit.NewTex("SpikePlate", S, S, false, false), px);
            return spikePlateTex;
        }

        /// <summary>警告光環：邊緣一圈亮環（外側柔和光暈）＋每個刺孔透出的光</summary>
        static Texture2D WarnTex()
        {
            if (warnTex != null) return warnTex;
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x / (float)S * 2f - 1f) * 1.12f, v = (y / (float)S * 2f - 1f) * 1.12f;   // 單位＝機關半徑
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ring = Mathf.Exp(-Mathf.Pow((r - 0.95f) / 0.05f, 2f)) + 0.45f * Mathf.Exp(-Mathf.Pow((r - 1.0f) / 0.1f, 2f));
                    float holes = 0f;
                    foreach (var q in SpikeLayout)
                    {
                        float d = Mathf.Sqrt((u - q.x) * (u - q.x) + (v - q.y) * (v - q.y));
                        holes += Mathf.Exp(-d * d / (SpikeBase * SpikeBase * 2.2f));
                    }
                    float inner = 0.12f * ArtKit.SS(0.95f, 0.3f, r);
                    float k = Mathf.Clamp01(ring + holes * 0.8f + inner) * (r > 1.11f ? 0f : 1f);
                    px[y * S + x] = new Color(1f, 1f, 1f, k);
                }
            warnTex = ArtKit.Finish(ArtKit.NewTex("SpikeWarn", S, S, false, false), px);
            return warnTex;
        }

        /// <summary>落石：黃土砂岩（水平層理＋斑點），UV 由 Facet 平面投影</summary>
        static Texture2D BoulderTex()
        {
            if (boulderTex != null) return boulderTex;
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = ArtKit.Fbm(u, v, 31, 4, 4);
                    float strata = 0.5f + 0.5f * Mathf.Sin(v * Mathf.PI * 2f * 6f + n * 4f);
                    var c = Color.Lerp(new Color(0.72f, 0.55f, 0.33f), new Color(0.86f, 0.71f, 0.47f), strata * 0.6f + n * 0.4f);
                    float spots = ArtKit.SS(0.68f, 0.74f, ArtKit.Fbm(u, v, 77, 8, 2));
                    c = Color.Lerp(c, new Color(0.5f, 0.38f, 0.24f), spots * 0.5f);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            boulderTex = ArtKit.Finish(ArtKit.NewTex("Boulder", S, S, true, true), px);
            return boulderTex;
        }
    }

    /// <summary>
    /// 程式美術共用的小工具（ArenaScene、CanyonScene、HazardArt 共用）：網格組合器、旋轉體、多面岩塊、
    /// 場地多邊形的幾何、可平鋪雜訊、貼圖建立。寫法沿用 VolcanoScene／ForestScene／SeaScene。
    /// </summary>
    internal static class ArtKit
    {
        // ================================================================ 網格組合器
        /// <summary>簡單的網格組合器（多個子網格、頂點色）</summary>
        public class MB
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int>[] T;
            public MB(int subs) { T = new List<int>[subs]; for (int i = 0; i < subs; i++) T[i] = new List<int>(); }
            public int Add(Vector3 p, Vector3 n, Vector2 uv, Color c) { V.Add(p); N.Add(n); UV.Add(uv); C.Add(c); return V.Count - 1; }
            public int Add(Vector3 p, Vector3 n, Vector2 uv) => Add(p, n, uv, Color.white);
            public void Tri(int s, int a, int b, int c) { T[s].Add(a); T[s].Add(b); T[s].Add(c); }
            /// <summary>平面著色的三角形（各自的頂點＋面法線）；UV 依法線方向取平面投影（uvScale＝每公尺幾個 UV）</summary>
            public void Facet(int s, Vector3 a, Vector3 b, Vector3 c, float uvScale = 0.8f)
            {
                var n = Vector3.Cross(b - a, c - a).normalized;
                bool top = Mathf.Abs(n.y) > 0.7f;
                Vector2 P(Vector3 p) => top ? new Vector2(p.x, p.z) * uvScale : new Vector2((p.x + p.z) * uvScale, p.y * uvScale);
                int i0 = Add(a, n, P(a)), i1 = Add(b, n, P(b)), i2 = Add(c, n, P(c));
                Tri(s, i0, i1, i2);
            }
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetColors(C);
                m.subMeshCount = T.Length;
                for (int i = 0; i < T.Length; i++) m.SetTriangles(T[i], i);
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>
        /// 四邊形 a→b→c→d（平面著色、自己的四個頂點），自動調整繞向讓正面朝向 eye（Unity：從正面看是順時針）。
        /// uv 依序給 a,b,c,d；不給就用 (0,0),(1,0),(1,1),(0,1)。
        /// </summary>
        public static void QuadToward(MB mb, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 eye, Vector2[] uv = null, Color? col = null)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            bool flip = Vector3.Dot(n, eye - (a + b + c + d) * 0.25f) < 0f;
            n = (flip ? -n : n).normalized;
            var cc = col ?? Color.white;
            var u0 = uv != null ? uv[0] : new Vector2(0f, 0f); var u1 = uv != null ? uv[1] : new Vector2(1f, 0f);
            var u2 = uv != null ? uv[2] : new Vector2(1f, 1f); var u3 = uv != null ? uv[3] : new Vector2(0f, 1f);
            int i0 = mb.Add(a, n, u0, cc), i1 = mb.Add(b, n, u1, cc), i2 = mb.Add(c, n, u2, cc), i3 = mb.Add(d, n, u3, cc);
            if (!flip) { mb.Tri(sub, i0, i1, i2); mb.Tri(sub, i0, i2, i3); }
            else { mb.Tri(sub, i0, i2, i1); mb.Tri(sub, i0, i3, i2); }
        }

        /// <summary>四邊形，正面朝向「遠離 inside 點」的方向（封閉物體的外表面）；UV 用平面投影</summary>
        public static void QuadAway(MB mb, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
        {
            var mid = (a + b + c + d) * 0.25f;
            var n = Vector3.Cross(b - a, c - a);
            bool top = Mathf.Abs(n.normalized.y) > 0.7f;
            Vector2 P(Vector3 p) => top ? new Vector2(p.x, p.z) : new Vector2(p.x + p.z, p.y);
            QuadToward(mb, sub, a, b, c, d, mid + (mid - inside), new[] { P(a), P(b), P(c), P(d) });
        }

        /// <summary>長方體（中心、尺寸、旋轉），平面著色；uvRect＝貼圖上的範圍（u0,v0,u1,v1），每個面都映射到這個範圍</summary>
        public static void Box(MB mb, int sub, Vector3 c, Vector3 size, Quaternion rot, Vector4? uvRect = null)
        {
            var h = size * 0.5f;
            Vector3 P(float x, float y, float z) => c + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            var r = uvRect ?? new Vector4(0f, 0f, 1f, 1f);
            var uv = new[] { new Vector2(r.x, r.y), new Vector2(r.z, r.y), new Vector2(r.z, r.w), new Vector2(r.x, r.w) };
            void F(Vector3 a, Vector3 b, Vector3 cc, Vector3 d) => QuadToward(mb, sub, a, b, cc, d, c + ((a + cc) * 0.5f - c) * 2f, uv);
            F(P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1));   // -Z
            F(P(1, -1, 1), P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1));       // +Z
            F(P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1));   // -X
            F(P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1));       // +X
            F(P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1));       // +Y
            F(P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1));   // -Y
        }

        /// <summary>旋轉體（沿本地 +Y 的一圈圈截面），可加頂蓋／底蓋；u 沿圓周、v＝高度</summary>
        public static void Lathe(MB mb, int sub, Vector3 pos, Quaternion rot, float[] hs, float[] rs, int sides, bool capTop, bool capBottom, Color? col = null)
        {
            int rings = hs.Length, start = mb.V.Count;
            var cc = col ?? Color.white;
            for (int r = 0; r < rings; r++)
            {
                float dr = rs[Mathf.Max(0, r - 1)] - rs[Mathf.Min(rings - 1, r + 1)];
                float dh = Mathf.Max(0.001f, hs[Mathf.Min(rings - 1, r + 1)] - hs[Mathf.Max(0, r - 1)]);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    var p = new Vector3(ca * rs[r], hs[r], sa * rs[r]);
                    var n = new Vector3(ca, dr / dh, sa).normalized;
                    mb.Add(pos + rot * p, rot * n, new Vector2(s / (float)sides, hs[r] / 2f), cc);
                }
            }
            int row = sides + 1;
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + r * row + s, b = a + 1, c = a + row, d = c + 1;
                    mb.Tri(sub, a, c, b); mb.Tri(sub, b, c, d);
                }
            if (capTop)
            {
                int top = mb.Add(pos + rot * (Vector3.up * hs[rings - 1]), rot * Vector3.up, new Vector2(0.5f, 0.5f), cc);
                int last = start + (rings - 1) * row;
                for (int s = 0; s < sides; s++) mb.Tri(sub, last + s, top, last + s + 1);
            }
            if (capBottom)
            {
                int bc = mb.Add(pos + rot * (Vector3.up * hs[0]), rot * Vector3.down, new Vector2(0.5f, 0.5f), cc);
                for (int s = 0; s < sides; s++) mb.Tri(sub, start + s + 1, bc, start + s);
            }
        }

        /// <summary>朝上的圓盤（平面 UV：中心 0.5、半徑 uvR 對應到 0～1）</summary>
        public static void TopDisc(MB mb, int sub, float y, float r, int seg, float uvR)
        {
            int c = mb.Add(new Vector3(0f, y, 0f), Vector3.up, new Vector2(0.5f, 0.5f));
            int start = mb.V.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                mb.Add(p, Vector3.up, new Vector2(0.5f + 0.5f * p.x / uvR, 0.5f + 0.5f * p.z / uvR));
            }
            // 從上往下看順時針：center → p(i+1) → p(i)
            for (int i = 0; i < seg; i++) mb.Tri(sub, c, start + i + 1, start + i);
        }

        /// <summary>單一的朝上圓盤網格（貼圖 UV 0～1 覆蓋整個圓盤）</summary>
        public static Mesh FlatDisc(float r, int seg, float y, string name)
        {
            var mb = new MB(1);
            TopDisc(mb, 0, y, r, seg, r);
            return mb.Build(name);
        }

        /// <summary>
        /// 一圈圈截面（由下往上，每圈 lon 點、逆時針）組成的平面著色多面體，可加底尖／頂尖。
        /// 三角形順序與 VolcanoScene.Spire 相同（從外面看是順時針＝正面）。
        /// </summary>
        public static void Facets(MB mb, int sub, List<Vector3[]> rings, Vector3? bottom, Vector3? top, float uvScale = 0.8f)
        {
            int lon = rings[0].Length;
            for (int r = 0; r < rings.Count - 1; r++)
                for (int s = 0; s < lon; s++)
                {
                    int s1 = (s + 1) % lon;
                    Vector3 a = rings[r][s], b = rings[r][s1], c = rings[r + 1][s], d = rings[r + 1][s1];
                    mb.Facet(sub, a, c, b, uvScale); mb.Facet(sub, b, c, d, uvScale);
                }
            if (top.HasValue) { var last = rings[rings.Count - 1]; for (int s = 0; s < lon; s++) mb.Facet(sub, last[s], top.Value, last[(s + 1) % lon], uvScale); }
            if (bottom.HasValue) { var first = rings[0]; for (int s = 0; s < lon; s++) mb.Facet(sub, first[s], first[(s + 1) % lon], bottom.Value, uvScale); }
        }

        /// <summary>鋸齒狀的岩塊：隨機凹凸的橢球（flatTop＝頂部削平），平面著色</summary>
        public static void FacetRock(MB mb, int sub, Vector3 c, Vector3 rad, int lon, int lat, int seed, float jag, float rotY, bool flatTop = true, float uvScale = 0.8f)
        {
            var rings = new List<Vector3[]>();
            float cr = Mathf.Cos(rotY), sr = Mathf.Sin(rotY);
            Vector3 Place(Vector3 p)
            {
                if (flatTop && p.y > rad.y * 0.55f) p.y = rad.y * 0.55f + (p.y - rad.y * 0.55f) * 0.35f;
                return c + new Vector3(p.x * cr - p.z * sr, p.y, p.x * sr + p.z * cr);
            }
            for (int j = 1; j < lat; j++)
            {
                float ph = Mathf.PI * (1f - j / (float)lat), sp = Mathf.Sin(ph), cp = Mathf.Cos(ph);
                var row = new Vector3[lon];
                for (int s = 0; s < lon; s++)
                {
                    float th = (s + (Hash(s, j, seed) - 0.5f) * 0.6f) / lon * Mathf.PI * 2f;
                    float k = 1f + jag * (Hash(s, j, seed + 7) - 0.5f);
                    row[s] = Place(new Vector3(Mathf.Cos(th) * sp * rad.x * k, cp * rad.y * k, Mathf.Sin(th) * sp * rad.z * k));
                }
                rings.Add(row);
            }
            var bottom = c + new Vector3(0f, -rad.y, 0f);
            var top = Place(new Vector3((Hash(1, 1, seed + 3) - 0.5f) * rad.x * 0.4f, rad.y * (0.9f + 0.2f * Hash(2, 2, seed)), (Hash(3, 3, seed) - 0.5f) * rad.z * 0.4f));
            Facets(mb, sub, rings, bottom, top, uvScale);
        }

        /// <summary>凹凸的橢球（灌木的一團葉子）</summary>
        public static void Blob(MB mb, int sub, Vector3 c, Vector3 rad, int lon, int lat, int seed)
        {
            var rng = new System.Random(seed * 17 + 3);
            float p1 = (float)rng.NextDouble() * 6.28f, p2 = (float)rng.NextDouble() * 6.28f, p3 = (float)rng.NextDouble() * 6.28f;
            int start = mb.V.Count, row = lon + 1;
            for (int j = 0; j <= lat; j++)
            {
                float ph = j / (float)lat * Mathf.PI, sp = Mathf.Sin(ph), cp = Mathf.Cos(ph);
                for (int s = 0; s <= lon; s++)
                {
                    float th = s / (float)lon * Mathf.PI * 2f, ct = Mathf.Cos(th), st = Mathf.Sin(th);
                    float jit = 1f + 0.16f * Mathf.Sin(3f * th + p1) * Mathf.Sin(2f * ph + p2) + 0.1f * Mathf.Sin(5f * th + p3) * sp;
                    var d = new Vector3(ct * sp, cp, st * sp);
                    var p = new Vector3(d.x * rad.x, d.y * rad.y, d.z * rad.z) * jit;
                    var n = new Vector3(d.x / rad.x, d.y / rad.y, d.z / rad.z).normalized;
                    mb.Add(c + p, n, new Vector2(s / (float)lon * 2f, j / (float)lat));
                }
            }
            for (int j = 0; j < lat; j++)
                for (int s = 0; s < lon; s++)
                {
                    int a = start + j * row + s, b = a + 1, cc = a + row, d = cc + 1;
                    mb.Tri(sub, a, b, cc); mb.Tri(sub, b, d, cc);
                }
        }

        /// <summary>確保網格的三角形整體朝上（第一個三角形的法線朝下就全部翻面）</summary>
        public static void FixWindingUp(Mesh m)
        {
            var v = m.vertices; var t = m.triangles;
            if (t.Length < 3) return;
            float sum = 0f;
            for (int i = 0; i < t.Length; i += 3) sum += Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).y;
            if (sum >= 0f) return;
            for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
            m.triangles = t;
        }

        // ================================================================ 場地多邊形的幾何
        public static void CenterAndRadius(List<Vector3> poly, float floorY, out Vector3 center, out float areaR)
        {
            center = Vector3.zero;
            if (poly != null && poly.Count >= 3) { foreach (var p in poly) center += p; center /= poly.Count; }
            center.y = floorY;
            areaR = 2f;
            if (poly != null) foreach (var p in poly) areaR = Mathf.Max(areaR, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z)));
        }

        /// <summary>輪廓角度表：均分 n 個＋每個多邊形頂點的角度（0～2π，已排序、去掉太近的）</summary>
        public static List<float> RingAngles(List<Vector3> poly, Vector3 c, int n, IEnumerable<float> extra = null)
        {
            var angs = new List<float>();
            for (int i = 0; i < n; i++) angs.Add(i / (float)n * Mathf.PI * 2f);
            if (poly != null) foreach (var p in poly) { float a = Mathf.Atan2(p.z - c.z, p.x - c.x); if (a < 0f) a += Mathf.PI * 2f; angs.Add(a); }
            if (extra != null) foreach (var e in extra) angs.Add(Mathf.Repeat(e, Mathf.PI * 2f));
            angs.Sort();
            var outp = new List<float>();
            float last = -1f;
            foreach (var a in angs) { if (a - last < 0.004f) continue; last = a; outp.Add(a); }
            if (outp.Count > 1 && outp[outp.Count - 1] - outp[0] > Mathf.PI * 2f - 0.004f) outp.RemoveAt(outp.Count - 1);
            return outp;
        }

        /// <summary>從中心往 a 方向，場地多邊形「最遠」的邊界距離（射線上更遠處一定在場地外）；沒有多邊形就回傳 fallback</summary>
        public static float EdgeDist(List<Vector3> poly, Vector3 c, float a, float fallback)
        {
            if (poly == null || poly.Count < 3) return fallback;
            float R = RayPolyMax(poly, new Vector2(c.x, c.z), new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            return R <= 0.01f ? fallback : R + 0.02f;
        }

        static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static float RayPolyMax(List<Vector3> poly, Vector2 o, Vector2 d)
        {
            float best = 0f; int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var p = new Vector2(poly[i].x, poly[i].z); var q = new Vector2(poly[(i + 1) % n].x, poly[(i + 1) % n].z);
                var e = q - p; float den = Cross2(d, e);
                if (Mathf.Abs(den) < 1e-6f) continue;
                var w = p - o;
                float t = Cross2(w, e) / den, s = Cross2(w, d) / den;
                if (t > 0f && s >= -1e-4f && s <= 1f + 1e-4f) best = Mathf.Max(best, t);
            }
            return best;
        }

        public static bool PointInPoly(List<Vector3> poly, Vector2 p)
        {
            if (poly == null || poly.Count < 3) return false;
            bool inside = false; int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = poly[i].x, zi = poly[i].z, xj = poly[j].x, zj = poly[j].z;
                if ((zi > p.y) != (zj > p.y) && p.x < (xj - xi) * (p.y - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>點到多邊形邊界的距離（在場地內時回傳負值）；沒有多邊形就回傳很大的數</summary>
        public static float SignedDistToPoly(List<Vector3> poly, Vector2 p)
        {
            if (poly == null || poly.Count < 3) return 1e9f;
            float best = 1e9f; int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = new Vector2(poly[i].x, poly[i].z); var b = new Vector2(poly[(i + 1) % n].x, poly[(i + 1) % n].z);
                var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return PointInPoly(poly, p) ? -best : best;
        }

        // ================================================================ 常用網格
        /// <summary>場地多邊形（由中心扇形三角化）；UV＝地板座標 ÷ tile 公尺</summary>
        public static Mesh PolyMesh(List<Vector3> poly, Vector3 center, float tile, string name)
        {
            int n = poly.Count;
            var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var tri = new int[n * 3];
            v[0] = center; uv[0] = new Vector2(center.x, center.z) / tile;
            for (int i = 0; i < n; i++) { v[i + 1] = new Vector3(poly[i].x, center.y, poly[i].z); uv[i + 1] = new Vector2(poly[i].x, poly[i].z) / tile; }
            for (int i = 0; i < n; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % n; tri[i * 3 + 2] = 1 + i; }
            var m = new Mesh { name = name, vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Disc(float r, int seg, string name)
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
            var m = new Mesh { name = name, vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>開口圓柱（從內側看）：u 沿圓周、v 由下往上</summary>
        public static Mesh Cylinder(float r, float h, int seg, string name)
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
            var m = new Mesh { name = name, vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        // ================================================================ 材質與物件
        public static Material NewMat(Shader sh, Texture2D tex, int queue, Color tint, string prefix)
        {
            var m = new Material(sh) { mainTexture = tex, renderQueue = queue, name = prefix + " " + tex.name };
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", tint); else m.color = new Color(tint.r * 2f, tint.g * 2f, tint.b * 2f, tint.a * 2f);
            return m;
        }

        public static GameObject MeshObj(Transform parent, string name, Mesh mesh, Vector3 pos, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // ================================================================ 雜訊與貼圖
        /// <summary>GLSL 的 smoothstep(e0, e1, x)（Mathf.SmoothStep 的參數意義不同）</summary>
        public static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        public static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 982451653; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        public static float ValueNoise(float x, float y, int s, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = ((xi % period) + period) % period, y0 = ((yi % period) + period) % period, x1 = (x0 + 1) % period, y1 = (y0 + 1) % period;
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, s), Hash(x1, y0, s), fx), Mathf.Lerp(Hash(x0, y1, s), Hash(x1, y1, s), fx), fy);
        }
        /// <summary>可無縫平鋪的分形雜訊（0～1）</summary>
        public static float Fbm(float u, float v, int s, int baseP, int oct)
        {
            float sum = 0f, amp = 0.5f, norm = 0f; int p = baseP;
            for (int o = 0; o < oct; o++) { sum += amp * ValueNoise(u * p, v * p, s + o * 17, p); norm += amp; amp *= 0.5f; p *= 2; }
            return sum / norm;
        }
        /// <summary>一維可循環雜訊（u 0～1 循環），用在沿圓周變化的東西</summary>
        public static float Noise1(float u, int s, int period, int oct = 3) => Fbm(Mathf.Repeat(u, 1f), 0.37f, s, period, oct);

        /// <summary>可平鋪的 Voronoi：回傳到最近邊界的距離（越小越接近裂縫）</summary>
        public static float VoronoiEdge(float u, float v, int cells, int s)
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

        public static Texture2D Finish(Texture2D t, Color32[] px) { t.SetPixels32(px); t.Apply(true, true); return t; }

        public static Texture2D NewTex(string name, int w, int h, bool repeatU, bool repeatV)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            t.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return t;
        }

        static Texture2D white, wood;
        public static Texture2D WhiteTex()
        {
            if (white != null) return white;
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            white = Finish(NewTex("White", 4, 4, false, false), px);
            return white;
        }

        /// <summary>木紋（灰階，乘上材質顏色）：沿 v 的長條紋＋節</summary>
        public static Texture2D WoodTex()
        {
            if (wood != null) return wood;
            const int S = 64; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float g = 0.5f + 0.5f * Mathf.Sin((u * 9f + Fbm(u, v, 3, 2, 3) * 2.5f) * Mathf.PI * 2f);
                    float k = 0.72f + 0.2f * g + 0.08f * Fbm(u, v * 0.25f, 9, 8, 2);
                    px[y * S + x] = new Color(k, k, k, 1f);
                }
            wood = Finish(NewTex("Wood", S, S, true, true), px);
            return wood;
        }
    }
}
