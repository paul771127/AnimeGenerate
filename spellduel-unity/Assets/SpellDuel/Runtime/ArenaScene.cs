using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角鬥場場景（第 4 章，無盡模式第 31～40 波）：全部用程式產生，不需要任何圖檔。
    ///   1. 場地內的地板：耙過的暖色沙地＋淡淡的腳印與污漬＋一圈褪色的彩繪圓環（貼在玩家畫的場地範圍上）。
    ///   2. 場地邊界外 1.5m：一圈 2.8m 高的石牆（腳座、牆面、紅金色的飾帶、壓頂），中間的空地仍舖著沙。
    ///      牆上有三座拱門（黑鐵柵門），懸掛紅／金色旗幟，牆頂有燃燒的火盆。
    ///   3. 牆後：一層層往外升高的看台（到 12～18m），坐滿各色觀眾剪影（四組輪流微微起伏），最上層是拱廊外牆＋旗桿；
    ///      其中一側是皇帝的包廂（大理石柱、紅色帷幕、金色寶座）。
    ///   4. 遠方背景：午後金黃色的塵霧天空（圓柱內側）＋天空頂蓋；空中飄著金色的塵埃。
    /// 地板與背景都是半透明，仍隱約看得到真實的房間（安全）。座標都是 AR session 座標。
    /// 所有不透明的東西（牆、看台、包廂）一律在場地多邊形之外，牆的內面離場地邊界至少 1.5m（小怪從那片沙地走進場）。
    /// </summary>
    public class ArenaScene : MonoBehaviour
    {
        const float BackRadius = 32f, BackHeight = 22f;
        public const float WallHeight = 2.8f;   // 石牆高度
        public const float WallGap = 1.5f;      // 牆的內面離場地邊界的最小距離
        const int Seg = 112;                    // 牆與看台沿圓周的分段數
        const float Riser = 0.55f, Tread = 0.85f, WallTop = 0.7f, RimH = 2.4f;
        const int Tiers = 9;
        // 拱門與皇帝包廂的方向（相對場地中心的角度，弧度）
        static readonly float[] GateAngles = { 0f, Mathf.PI, Mathf.PI * 1.5f };
        const float EmperorAngle = Mathf.PI * 0.5f;

        Material sandMat, ringMat, backMat, capMat, fireMat;
        readonly Transform[] crowd = new Transform[4];
        readonly Vector3[] crowdBase = new Vector3[4];
        ParticleSystem fire, smoke;
        readonly List<Vector3> fireSpots = new List<Vector3>();
        float[] wallR;
        Vector3 center;
        float clock, fireAcc, smokeAcc;

        public static ArenaScene Create(List<Vector3> polygon, float floorY)
        {
            var go = new GameObject("ArenaScene");
            var a = go.AddComponent<ArenaScene>();
            a.Build(polygon, floorY);
            a.variantArt=SceneVariantArt.Install(a.transform,polygon,floorY,3);
            return a;
        }

        public void SetVisible(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }
        /// <summary>同一主題的場景變化（0～2；SoloBattle.SceneVariant 決定）。第一次建場預建三組，切換只改顯示</summary>
        public int Variant { get; private set; }
        SceneVariantArt variantArt;
        public void SetVariant(int v) { Variant = Mathf.Clamp(v, 0, 2); if(variantArt)variantArt.Set(Variant); }

        /// <summary>某個方向（相對場地中心的角度，弧度）上，石牆內面離中心的水平距離</summary>
        public float WallRadiusAt(float ang)
        {
            if (wallR == null) return 0f;
            float f = Mathf.Repeat(ang, Mathf.PI * 2f) / (Mathf.PI * 2f) * Seg;
            int i0 = Mathf.FloorToInt(f) % Seg;
            return Mathf.Lerp(wallR[i0], wallR[(i0 + 1) % Seg], f - Mathf.Floor(f));
        }

        static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        // ================================================================ 建置
        void Build(List<Vector3> poly, float floorY)
        {
            bool hasPoly = poly != null && poly.Count >= 3;
            ArtKit.CenterAndRadius(poly, floorY, out center, out float areaR);
            var origin = center;

            var blend = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var diffuse = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Mobile/Diffuse") ?? blend;
            var rng = new System.Random(41);

            // ---- 牆的輪廓：場地邊界外推 1.5m，取鄰近角度的最大值再平滑（圓角、略帶橢圓），最後確保離邊界 ≥ 1.5m
            wallR = new float[Seg];
            {
                var raw = new float[Seg];
                for (int i = 0; i < Seg; i++) raw[i] = ArtKit.EdgeDist(poly, center, i / (float)Seg * Mathf.PI * 2f, areaR) + WallGap;
                int win = Seg / 30;
                for (int i = 0; i < Seg; i++)
                {
                    float m = 0f;
                    for (int k = -win; k <= win; k++) m = Mathf.Max(m, raw[(i + k + Seg) % Seg]);
                    wallR[i] = m;
                }
                Smooth(wallR, 4);
                float maxR = 0f; foreach (var r in wallR) maxR = Mathf.Max(maxR, r);
                for (int i = 0; i < Seg; i++) wallR[i] = Mathf.Lerp(wallR[i], maxR, 0.25f);
                Enforce(poly, hasPoly);
                Smooth(wallR, 2);
                Enforce(poly, hasPoly);
            }

            // ---- 場地內：耙過的沙地；場地與牆之間也舖同樣的沙
            sandMat = ArtKit.NewMat(blend, ArenaTex.Sand(), 2910, new Color(0.5f, 0.5f, 0.5f, 0.47f), "Arena");
            if (hasPoly)
            {
                MeshObj("SandFloor", ArtKit.PolyMesh(poly, center, 2.5f, "ArenaFloor"), new Vector3(0f, 0.004f, 0f), sandMat);
                // 彩繪圓環（以場地中心為圓心，貼圖不平鋪）
                var marks = ArtKit.PolyMesh(poly, center, 1f, "ArenaMarks");
                var v = marks.vertices; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++) uv[i] = new Vector2((v[i].x - center.x) / (2f * areaR) + 0.5f, (v[i].z - center.z) / (2f * areaR) + 0.5f);
                marks.uv = uv;
                ringMat = ArtKit.NewMat(blend, ArenaTex.RingMarks(), 2911, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Arena");
                MeshObj("PaintedRings", marks, new Vector3(0f, 0.006f, 0f), ringMat);
            }
            MeshObj("SandApron", SandApron(poly, areaR), origin + new Vector3(0f, 0.002f, 0f), sandMat);

            // ---- 遠方背景：金黃色塵霧天空＋頂蓋
            backMat = ArtKit.NewMat(blend, ArenaTex.Backdrop(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Arena");
            backMat.mainTextureScale = new Vector2(2f, 1f);
            MeshObj("Backdrop", ArtKit.Cylinder(BackRadius, BackHeight, 96, "ArenaBackdrop"), new Vector3(center.x, floorY - 1.5f, center.z), backMat);
            capMat = ArtKit.NewMat(blend, ArenaTex.Cap(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Arena");
            MeshObj("SkyCap", ArtKit.Disc(BackRadius, 48, "ArenaCap"), new Vector3(center.x, floorY - 1.5f + BackHeight, center.z), capMat);

            // ---- 石牆＋看台＋最上層的拱廊外牆（一個網格；每一段都是沿圓周的一條帶子）
            var stoneMat = new Material(diffuse) { mainTexture = ArenaTex.Stone(), color = new Color(0.95f, 0.93f, 0.9f), name = "Arena Stone" };
            {
                var mb = new ArtKit.MB(1);
                const float tile = 2f;
                // 牆：腳座 → 牆面 → 紅金飾帶 → 壓頂下緣 → 壓頂 → 牆頂走道
                Strip(mb, -0.12f, 0f, -0.12f, 0.38f, 0f, 0.05f, tile);
                Strip(mb, -0.12f, 0.38f, 0f, 0.45f, 0.05f, 0.06f, tile);
                Strip(mb, 0f, 0.45f, 0f, 2.2f, 0.06f, 0.30f, tile);
                Strip(mb, 0f, 2.2f, 0f, 2.55f, 0.30f, 0.36f, tile);
                Strip(mb, 0f, 2.55f, -0.1f, 2.6f, 0.36f, 0.37f, tile);
                Strip(mb, -0.1f, 2.6f, -0.1f, WallHeight, 0.37f, 0.40f, tile);
                Strip(mb, -0.1f, WallHeight, WallTop, WallHeight, 0.50f, 0.60f, tile);
                // 看台：每層一個踢面（直）＋一個踏面（平）
                for (int k = 0; k < Tiers; k++)
                {
                    float d0 = WallTop + k * Tread, h0 = WallHeight + k * Riser;
                    Strip(mb, d0, h0, d0, h0 + Riser, 0.40f, 0.50f, tile);
                    Strip(mb, d0, h0 + Riser, d0 + Tread, h0 + Riser, 0.50f, 0.60f, tile);
                }
                // 最上層：拱廊外牆＋牆頂
                float dR = WallTop + Tiers * Tread, hR = WallHeight + Tiers * Riser;
                Strip(mb, dR, hR, dR, hR + RimH, 0.60f, 1.0f, tile);
                Strip(mb, dR, hR + RimH, dR + 0.6f, hR + RimH, 0.50f, 0.60f, tile);

                // 拱門兩側的石柱與門楣（貼圖取牆面石塊的範圍）
                var blockUV = new Vector4(0f, 0.08f, 0.35f, 0.28f);
                foreach (float ga in GateAngles)
                {
                    var d = Dir(ga); var dir = new Vector3(d.x, 0f, d.y); var t = new Vector3(-d.y, 0f, d.x);
                    float W = WallRadiusAt(ga);
                    var rot = Quaternion.LookRotation(-dir, Vector3.up);
                    foreach (float s in new[] { -1f, 1f })
                        ArtKit.Box(mb, 0, dir * (W - 0.2f) + t * (s * 1.3f) + Vector3.up * 1.45f, new Vector3(0.38f, 2.9f, 0.34f), rot, blockUV);
                    ArtKit.Box(mb, 0, dir * (W - 0.22f) + Vector3.up * 2.72f, new Vector3(3.0f, 0.34f, 0.38f), rot, blockUV);
                    ArtKit.Box(mb, 0, dir * (W - 0.3f) + Vector3.up * 2.62f, new Vector3(0.36f, 0.42f, 0.3f), rot, new Vector4(0.6f, 0.37f, 0.8f, 0.40f));   // 拱心石
                }
                var m = mb.Build("ArenaStands");
                m.RecalculateNormals();
                var go = MeshObj("WallAndStands", m, origin, stoneMat);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
            }

            // ---- 拱門的黑鐵柵門（半透明貼花，貼在牆面前方）
            {
                var gateMat = ArtKit.NewMat(blend, ArenaTex.Portcullis(), 2940, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Arena");
                var mb = new ArtKit.MB(1);
                foreach (float ga in GateAngles)
                {
                    var d = Dir(ga); var dir = new Vector3(d.x, 0f, d.y); var t = new Vector3(-d.y, 0f, d.x);
                    var c = dir * (WallRadiusAt(ga) - 0.26f);
                    WallQuad(mb, c, t, 1.12f, 0.01f, 2.55f, new Vector2(0f, 0f), new Vector2(1f, 1f), Color.white);
                }
                MeshObj("Portcullis", mb.Build("ArenaGates"), origin, gateMat);
            }

            // ---- 旗幟：牆面上的垂幅（紅／金交錯）＋皇帝包廂下方的大帷幕
            var bannerMat = ArtKit.NewMat(blend, ArenaTex.Banners(), 2945, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Arena");
            {
                var mb = new ArtKit.MB(1);
                for (int k = 0; k < 18; k++)
                {
                    float a = (k + 0.5f) / 18f * Mathf.PI * 2f;
                    if (NearSpecial(a, 2.2f, 3.2f)) continue;
                    var d = Dir(a); var dir = new Vector3(d.x, 0f, d.y); var t = new Vector3(-d.y, 0f, d.x);
                    int variant = k % 2;
                    WallQuad(mb, dir * (WallRadiusAt(a) - 0.2f), t, 0.36f, 1.0f, 2.7f, new Vector2(variant / 3f, 0f), new Vector2((variant + 1) / 3f, 1f), Color.white);
                }
                {
                    var d = Dir(EmperorAngle); var dir = new Vector3(d.x, 0f, d.y); var t = new Vector3(-d.y, 0f, d.x);
                    WallQuad(mb, dir * (WallRadiusAt(EmperorAngle) - 0.22f), t, 2.1f, 1.3f, 2.75f, new Vector2(2f / 3f, 0f), new Vector2(1f, 1f), Color.white);
                }
                // 最上層旗桿上的長旗（面向場地中心）
                float dR = WallTop + Tiers * Tread + 0.3f, hTop = WallHeight + Tiers * Riser + RimH;
                for (int k = 0; k < 14; k++)
                {
                    float a = (k + 0.25f) / 14f * Mathf.PI * 2f;
                    var d = Dir(a); var dir = new Vector3(d.x, 0f, d.y); var t = new Vector3(-d.y, 0f, d.x);
                    var basePos = dir * (WallRadiusAt(a) + dR - 0.12f);
                    WallQuad(mb, basePos + t * 0.36f, t, 0.32f, hTop + 1.2f, hTop + 3.2f, new Vector2((k % 2) / 3f, 0f), new Vector2((k % 2 + 1) / 3f, 1f), Color.white);
                }
                MeshObj("Banners", mb.Build("ArenaBanners"), origin, bannerMat);
            }

            // ---- 火盆（牆頂）與旗桿（最上層）：0＝青銅、1＝黑鐵／炭
            {
                var bronze = new Material(diffuse) { color = new Color(0.72f, 0.5f, 0.26f), name = "Arena Bronze" };
                var iron = new Material(diffuse) { color = new Color(0.16f, 0.14f, 0.13f), name = "Arena Iron" };
                var mb = new ArtKit.MB(2);
                for (int k = 0; k < 12; k++)
                {
                    float a = (k + 0.5f) / 12f * Mathf.PI * 2f;
                    if (NearSpecial(a, 1.4f, 2.8f)) continue;
                    var d = Dir(a); var dir = new Vector3(d.x, 0f, d.y);
                    var p = dir * (WallRadiusAt(a) + 0.3f) + Vector3.up * WallHeight;
                    for (int l = 0; l < 3; l++)
                    {
                        float la = l / 3f * Mathf.PI * 2f;
                        var foot = p + new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la)) * 0.22f;
                        var top = p + Vector3.up * 0.82f + new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la)) * 0.08f;
                        var ld = top - foot;
                        ArtKit.Lathe(mb, 1, foot, Quaternion.FromToRotation(Vector3.up, ld.normalized), new[] { 0f, ld.magnitude }, new[] { 0.025f, 0.02f }, 5, false, false);
                    }
                    ArtKit.Lathe(mb, 0, p + Vector3.up * 0.78f, Quaternion.identity, new[] { 0f, 0.1f, 0.22f, 0.28f, 0.3f }, new[] { 0.06f, 0.24f, 0.36f, 0.4f, 0.37f }, 10, false, true);
                    ArtKit.Lathe(mb, 1, p + Vector3.up * 1.0f, Quaternion.identity, new[] { 0f, 0.04f }, new[] { 0.35f, 0.3f }, 10, true, false);   // 炭
                    fireSpots.Add(origin + p + Vector3.up * 1.1f);
                }
                float dR = WallTop + Tiers * Tread + 0.3f, hTop = WallHeight + Tiers * Riser + RimH;
                for (int k = 0; k < 14; k++)
                {
                    float a = (k + 0.25f) / 14f * Mathf.PI * 2f;
                    var d = Dir(a); var dir = new Vector3(d.x, 0f, d.y); var t = new Vector3(-d.y, 0f, d.x);
                    var p = dir * (WallRadiusAt(a) + dR) + Vector3.up * hTop;
                    ArtKit.Lathe(mb, 0, p, Quaternion.identity, new[] { 0f, 3.3f }, new[] { 0.05f, 0.035f }, 6, true, false);
                    ArtKit.Lathe(mb, 0, p + Vector3.up * 3.3f, Quaternion.identity, new[] { 0f, 0.06f, 0.14f }, new[] { 0.05f, 0.08f, 0.01f }, 6, false, false);   // 頂端的小尖
                    ArtKit.Lathe(mb, 0, p + Vector3.up * 3.22f - t * 0.05f, Quaternion.FromToRotation(Vector3.up, t), new[] { 0f, 0.82f }, new[] { 0.022f, 0.022f }, 5, true, true);   // 橫桿
                }
                MeshObj("BraziersAndPoles", mb.Build("ArenaProps"), origin, bronze, iron).GetComponent<MeshRenderer>().receiveShadows = true;
            }

            // ---- 皇帝的包廂
            BuildEmperorBox(diffuse, origin);

            // ---- 觀眾（四組，各自輕輕起伏）
            BuildCrowd(blend, origin, rng);

            // ---- 粒子：火盆的火焰與煙、空中的金色塵埃
            fireMat = new Material(SpellFx.AddMat(SpellFx.TexSoft)) { name = "Arena Fire" };
            fire = SpellFx.NewPS(transform, "BrazierFire", fireMat, 220, true);
            {
                var m = fire.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.8f, 0.3f));
                SpellFx.Rise(fire, 0.9f);
                SpellFx.Noise(fire, 0.3f, 1.5f);
                SpellFx.FadeOut(fire);
                SpellFx.SizeCurve(fire, SpellFx.Shrink);
                fire.Play();
            }
            smoke = SpellFx.NewPS(transform, "BrazierSmoke", SpellFx.BlendMat(SpellFx.TexSoft), 80, true);
            {
                var m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                m.startColor = new Color(0.3f, 0.26f, 0.22f, 0.3f);
                SpellFx.Rise(smoke, 0.6f);
                SpellFx.Noise(smoke, 0.3f, 0.4f);
                SpellFx.FadeInOut(smoke);
                SpellFx.SizeCurve(smoke, SpellFx.Grow);
                smoke.Play();
            }
            var motes = SpellFx.NewPS(transform, "GoldenDust", SpellFx.BlendMat(SpellFx.TexSoft), 90, true);
            motes.transform.position = new Vector3(center.x, floorY + 2f, center.z);
            SpellFx.Sphere(motes, areaR + 5f);
            var ms = motes.shape; ms.scale = new Vector3(1f, 0.3f, 1f);
            var mm = motes.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            mm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.88f, 0.6f, 0.6f), new Color(0.95f, 0.78f, 0.5f, 0.45f));
            SpellFx.Rise(motes, 0.03f);
            SpellFx.Noise(motes, 0.25f, 0.3f);
            SpellFx.FadeInOut(motes);
            SpellFx.Rate(motes, 9f);
            motes.Play();
        }

        void Update()
        {
            if(Variant!=0)return;
            float dt = Time.deltaTime;
            clock += dt;
            // 觀眾：四組各自用不同的節奏輕輕起伏（像在歡呼）
            for (int g = 0; g < crowd.Length; g++)
                if (crowd[g])
                {
                    float s = Mathf.Sin(clock * (2.1f + g * 0.37f) + g * 1.7f);
                    crowd[g].position = crowdBase[g] + Vector3.up * (0.035f * Mathf.Max(0f, s) + 0.01f * Mathf.Sin(clock * 5.3f + g));
                }
            // 火盆：每個火盆每秒約 14 顆火焰粒子、2 顆煙
            if (fire && fireSpots.Count > 0)
            {
                fireAcc += dt * 14f * fireSpots.Count;
                int n = Mathf.Min(40, (int)fireAcc); fireAcc -= n;
                for (int i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = fireSpots[Random.Range(0, fireSpots.Count)] + new Vector3(Random.Range(-0.14f, 0.14f), Random.Range(-0.05f, 0.08f), Random.Range(-0.14f, 0.14f)),
                        applyShapeToPosition = false
                    };
                    fire.Emit(ep, 1);
                }
            }
            if (smoke && fireSpots.Count > 0)
            {
                smokeAcc += dt * 2f * fireSpots.Count;
                int n = Mathf.Min(10, (int)smokeAcc); smokeAcc -= n;
                for (int i = 0; i < n; i++)
                    smoke.Emit(new ParticleSystem.EmitParams { position = fireSpots[Random.Range(0, fireSpots.Count)] + Vector3.up * 0.5f, applyShapeToPosition = false }, 1);
            }
            if (fireMat)
            {
                float k = 0.45f + 0.05f * Mathf.Sin(clock * 9f) + 0.03f * Mathf.Sin(clock * 23f);
                fireMat.SetColor("_TintColor", new Color(k, k, k, 0.5f));
            }
        }

        // ================================================================ 牆的輪廓
        void Smooth(float[] r, int passes)
        {
            var tmp = new float[r.Length];
            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < r.Length; i++) tmp[i] = (r[(i - 1 + r.Length) % r.Length] + 2f * r[i] + r[(i + 1) % r.Length]) * 0.25f;
                System.Array.Copy(tmp, r, r.Length);
            }
        }

        /// <summary>確保每一點離場地邊界 ≥ WallGap（不夠就往外推）</summary>
        void Enforce(List<Vector3> poly, bool hasPoly)
        {
            if (!hasPoly) return;
            for (int i = 0; i < Seg; i++)
            {
                var d = Dir(i / (float)Seg * Mathf.PI * 2f);
                for (int it = 0; it < 200; it++)
                {
                    var p = new Vector2(center.x + d.x * wallR[i], center.z + d.y * wallR[i]);
                    if (ArtKit.SignedDistToPoly(poly, p) >= WallGap) break;
                    wallR[i] += 0.05f;
                }
            }
        }

        /// <summary>a 是否靠近拱門（弧長 gateM 公尺內）或皇帝包廂（emperorM 公尺內）</summary>
        bool NearSpecial(float a, float gateM, float emperorM)
        {
            foreach (float g in GateAngles) if (Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, g * Mathf.Rad2Deg)) * Mathf.Deg2Rad * WallRadiusAt(g) < gateM) return true;
            return Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, EmperorAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad * WallRadiusAt(EmperorAngle) < emperorM;
        }

        // ================================================================ 網格
        /// <summary>
        /// 沿圓周的一條帶子：從截面點 A（離牆內面 dA、高 hA）到 B（dB、hB），整圈 Seg 段（本地座標，原點＝場地中心地面）。
        /// u＝沿 A 那一圈的弧長 ÷ tile（取整數個 tile，接縫不斷），v＝vA～vB（貼圖上的分區）。正面朝向場地中心（上方）。
        /// </summary>
        void Strip(ArtKit.MB mb, float dA, float hA, float dB, float hB, float vA, float vB, float tile)
        {
            int start = mb.V.Count;
            var cum = new float[Seg + 1];
            Vector3 PA(int i) { var d = Dir(i / (float)Seg * Mathf.PI * 2f); float r = wallR[i % Seg] + dA; return new Vector3(d.x * r, hA, d.y * r); }
            for (int i = 1; i <= Seg; i++) cum[i] = cum[i - 1] + (PA(i) - PA(i - 1)).magnitude;
            float tiles = Mathf.Max(1f, Mathf.Round(cum[Seg] / tile));
            for (int i = 0; i <= Seg; i++)
            {
                var d = Dir(i / (float)Seg * Mathf.PI * 2f);
                float rA = wallR[i % Seg] + dA, rB = wallR[i % Seg] + dB, u = cum[i] / Mathf.Max(0.001f, cum[Seg]) * tiles;
                mb.Add(new Vector3(d.x * rA, hA, d.y * rA), Vector3.up, new Vector2(u, vA));
                mb.Add(new Vector3(d.x * rB, hB, d.y * rB), Vector3.up, new Vector2(u, vB));
            }
            var eye = new Vector3(0f, (hA + hB) * 0.5f + 3f, 0f);
            var n = Vector3.Cross(mb.V[start + 1] - mb.V[start], mb.V[start + 2] - mb.V[start]);
            bool flip = Vector3.Dot(n, eye - mb.V[start]) < 0f;
            for (int i = 0; i < Seg; i++)
            {
                int a = start + i * 2, b = a + 1, c = a + 2, dd = a + 3;
                if (!flip) { mb.Tri(0, a, b, c); mb.Tri(0, c, b, dd); }
                else { mb.Tri(0, a, c, b); mb.Tri(0, c, dd, b); }
            }
        }

        /// <summary>直立、面向場地中心的長方形（以 c 為底邊中點、t＝切線方向、半寬 hw、高 y0～y1）</summary>
        static void WallQuad(ArtKit.MB mb, Vector3 c, Vector3 t, float hw, float y0, float y1, Vector2 uv0, Vector2 uv1, Color col)
        {
            var a = c - t * hw + Vector3.up * y0; var b = c + t * hw + Vector3.up * y0;
            var cc = c + t * hw + Vector3.up * y1; var d = c - t * hw + Vector3.up * y1;
            ArtKit.QuadToward(mb, 0, a, b, cc, d, new Vector3(0f, (y0 + y1) * 0.5f, 0f),
                new[] { new Vector2(uv0.x, uv0.y), new Vector2(uv1.x, uv0.y), new Vector2(uv1.x, uv1.y), new Vector2(uv0.x, uv1.y) }, col);
        }

        /// <summary>場地邊界到牆腳之間的沙地（本地座標）；UV＝世界平面座標 ÷ 2.5m，跟場地內的沙地接得起來</summary>
        Mesh SandApron(List<Vector3> poly, float areaR)
        {
            var angs = ArtKit.RingAngles(poly, center, 128);
            int n = angs.Count;
            var v = new Vector3[(n + 1) * 2]; var uv = new Vector2[v.Length]; var tri = new int[n * 6];
            for (int i = 0; i <= n; i++)
            {
                float a = angs[i % n];
                var d = Dir(a);
                float r0 = ArtKit.EdgeDist(poly, center, a, areaR) - 0.02f, r1 = WallRadiusAt(a) - 0.1f;
                v[i * 2] = new Vector3(d.x * r0, 0f, d.y * r0); v[i * 2 + 1] = new Vector3(d.x * r1, 0f, d.y * r1);
                uv[i * 2] = new Vector2(center.x + v[i * 2].x, center.z + v[i * 2].z) / 2.5f;
                uv[i * 2 + 1] = new Vector2(center.x + v[i * 2 + 1].x, center.z + v[i * 2 + 1].z) / 2.5f;
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                tri[i * 6] = a; tri[i * 6 + 1] = a + 1; tri[i * 6 + 2] = a + 2;
                tri[i * 6 + 3] = a + 2; tri[i * 6 + 4] = a + 1; tri[i * 6 + 5] = a + 3;
            }
            var m = new Mesh { name = "ArenaApron", vertices = v, uv = uv, triangles = tri };
            ArtKit.FixWindingUp(m);
            m.RecalculateBounds();
            return m;
        }

        // ================================================================ 皇帝包廂
        void BuildEmperorBox(Shader diffuse, Vector3 origin)
        {
            var marble = new Material(diffuse) { mainTexture = ArenaTex.Stone(), color = new Color(1.08f, 1.06f, 1.04f), name = "Arena Marble" };
            marble.mainTextureScale = new Vector2(0.3f, 1f);
            var crimson = new Material(diffuse) { color = new Color(0.62f, 0.1f, 0.09f), name = "Arena Crimson" };
            var gold = new Material(diffuse) { color = new Color(0.95f, 0.72f, 0.25f), name = "Arena Gold" };
            var mb = new ArtKit.MB(3);   // 0＝大理石、1＝深紅、2＝金
            var tread = new Vector4(0.1f, 0.52f, 0.4f, 0.58f);
            // 本地座標：x＝沿牆的切線、z＝往場地中心（0＝牆的內面）、y＝高度（相對地板）
            ArtKit.Box(mb, 0, new Vector3(0f, 3.2f, -1.6f), new Vector3(4.6f, 0.8f, 3.8f), Quaternion.identity, tread);   // 台座
            ArtKit.Box(mb, 0, new Vector3(0f, 3.85f, 0.12f), new Vector3(4.6f, 0.5f, 0.22f), Quaternion.identity, tread);   // 前欄
            ArtKit.Box(mb, 2, new Vector3(0f, 4.12f, 0.12f), new Vector3(4.66f, 0.06f, 0.26f), Quaternion.identity);
            foreach (float x in new[] { -2.05f, -0.7f, 0.7f, 2.05f })
            {
                ArtKit.Lathe(mb, 0, new Vector3(x, 3.6f, 0.05f), Quaternion.identity, new[] { 0f, 0.12f, 0.2f, 2.5f, 2.62f }, new[] { 0.2f, 0.2f, 0.14f, 0.12f, 0.12f }, 8, false, false);
                ArtKit.Lathe(mb, 2, new Vector3(x, 6.22f, 0.05f), Quaternion.identity, new[] { 0f, 0.1f, 0.18f }, new[] { 0.13f, 0.2f, 0.22f }, 8, true, false);   // 金色柱頭
            }
            ArtKit.Box(mb, 1, new Vector3(0f, 5.0f, -3.35f), new Vector3(4.6f, 2.8f, 0.2f), Quaternion.identity);         // 後牆（紅）
            foreach (float s in new[] { -1f, 1f }) ArtKit.Box(mb, 0, new Vector3(s * 2.25f, 5.0f, -1.65f), new Vector3(0.16f, 2.8f, 3.4f), Quaternion.identity, tread);
            ArtKit.Box(mb, 0, new Vector3(0f, 6.6f, -1.6f), new Vector3(5.0f, 0.4f, 4.0f), Quaternion.identity, tread);     // 簷
            ArtKit.Box(mb, 2, new Vector3(0f, 6.48f, 0.42f), new Vector3(5.0f, 0.1f, 0.06f), Quaternion.identity);
            ArtKit.Box(mb, 1, new Vector3(0f, 6.2f, 0.3f), new Vector3(4.5f, 0.32f, 0.05f), Quaternion.identity);         // 紅色垂簾
            // 山牆（三角形）＋兩片屋頂斜面
            {
                Vector3 l0 = new Vector3(-2.5f, 6.8f, 0.4f), r0 = new Vector3(2.5f, 6.8f, 0.4f), t0 = new Vector3(0f, 7.7f, 0.4f);
                Vector3 l1 = new Vector3(-2.5f, 6.8f, -3.6f), r1 = new Vector3(2.5f, 6.8f, -3.6f), t1 = new Vector3(0f, 7.7f, -3.6f);
                var inside = new Vector3(0f, 7.1f, -1.6f);
                ArtKit.QuadAway(mb, 0, l0, r0, t0, t0, inside);
                ArtKit.QuadAway(mb, 0, l1, r1, t1, t1, inside);
                ArtKit.QuadAway(mb, 1, l0, t0, t1, l1, inside);
                ArtKit.QuadAway(mb, 1, r0, t0, t1, r1, inside);
                ArtKit.Box(mb, 2, new Vector3(0f, 7.15f, 0.44f), new Vector3(0.36f, 0.36f, 0.05f), Quaternion.Euler(0f, 0f, 45f));   // 金色徽記
            }
            // 寶座
            ArtKit.Box(mb, 2, new Vector3(0f, 3.85f, -2.3f), new Vector3(0.9f, 0.5f, 0.7f), Quaternion.identity);
            ArtKit.Box(mb, 2, new Vector3(0f, 4.6f, -2.62f), new Vector3(0.9f, 1.3f, 0.14f), Quaternion.identity);
            // 兩側的金鷹軍旗
            foreach (float s in new[] { -1f, 1f })
            {
                ArtKit.Lathe(mb, 2, new Vector3(s * 2.7f, 3.6f, 0.0f), Quaternion.identity, new[] { 0f, 3.4f }, new[] { 0.035f, 0.03f }, 6, false, false);
                ArtKit.Box(mb, 2, new Vector3(s * 2.7f, 7.15f, 0.0f), new Vector3(0.42f, 0.3f, 0.05f), Quaternion.identity);
                ArtKit.Box(mb, 1, new Vector3(s * 2.7f, 6.55f, 0.0f), new Vector3(0.3f, 0.6f, 0.03f), Quaternion.identity);
            }
            var d = Dir(EmperorAngle);
            var dir = new Vector3(d.x, 0f, d.y);
            var go = MeshObj("EmperorBox", mb.Build("ArenaEmperorBox"), origin + dir * WallRadiusAt(EmperorAngle), marble, crimson, gold);
            go.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        // ================================================================ 觀眾
        void BuildCrowd(Shader blend, Vector3 origin, System.Random rng)
        {
            var pal = new[]
            {
                new Color(0.78f, 0.62f, 0.42f), new Color(0.72f, 0.42f, 0.3f), new Color(0.55f, 0.58f, 0.4f), new Color(0.48f, 0.55f, 0.66f),
                new Color(0.86f, 0.8f, 0.68f), new Color(0.55f, 0.42f, 0.32f), new Color(0.6f, 0.52f, 0.62f), new Color(0.75f, 0.68f, 0.5f),
                new Color(0.66f, 0.3f, 0.26f), new Color(0.45f, 0.48f, 0.44f)
            };
            var tex = ArenaTex.Crowd();
            var mbs = new ArtKit.MB[4];
            for (int g = 0; g < 4; g++) mbs[g] = new ArtKit.MB(1);
            float eAng = EmperorAngle;
            for (int k = 0; k < Tiers; k++)
            {
                int g = Mathf.Min(3, k * 4 / Tiers);
                float dMid = WallTop + k * Tread + Tread * 0.55f, h = WallHeight + (k + 1) * Riser;
                // 沿這一層走一圈，每 0.55～0.7m 一個人
                float carry = (float)rng.NextDouble() * 0.5f;
                for (int i = 0; i < Seg; i++)
                {
                    float a0 = i / (float)Seg * Mathf.PI * 2f, a1 = (i + 1) / (float)Seg * Mathf.PI * 2f;
                    var p0 = Dir(a0) * (wallR[i] + dMid); var p1 = Dir(a1) * (wallR[(i + 1) % Seg] + dMid);
                    float len = (p1 - p0).magnitude, s = carry;
                    while (s < len)
                    {
                        float f = s / len, a = Mathf.Lerp(a0, a1, f);
                        s += 0.55f + (float)rng.NextDouble() * 0.15f;
                        // 走道（每 22.5° 一條）、空位、皇帝包廂底下都不放人
                        if (Mathf.Repeat(a / (Mathf.PI * 2f) * 16f, 1f) < 0.06f) continue;
                        if (rng.NextDouble() < 0.1) continue;
                        if (dMid < 3.9f && Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, eAng * Mathf.Rad2Deg)) * Mathf.Deg2Rad * (wallR[i] + dMid) < 2.6f) continue;
                        var p = Vector2.Lerp(p0, p1, f) + Dir(a) * (((float)rng.NextDouble() - 0.5f) * 0.18f);
                        var t = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                        float sc = 0.88f + (float)rng.NextDouble() * 0.24f;
                        var col = pal[rng.Next(pal.Length)] * (0.85f + 0.25f * (float)rng.NextDouble()); col.a = 1f;
                        int variant = rng.NextDouble() < 0.6 ? 0 : 1 + rng.Next(3);
                        WallQuadG(mbs[g], new Vector3(p.x, 0f, p.y), t, 0.27f * sc, h - 0.12f, h - 0.12f + 0.95f * sc, variant, col);
                    }
                    carry = s - len;
                }
            }
            // 包廂裡的皇帝與侍從（大一點，紫色與白色）
            {
                var d = Dir(eAng); var dir = new Vector3(d.x, 0f, d.y); var t = new Vector3(-d.y, 0f, d.x);
                float W = WallRadiusAt(eAng);
                WallQuadG(mbs[0], dir * (W + 2.15f), t, 0.36f, 3.75f, 5.05f, 1, new Color(0.62f, 0.36f, 0.72f));
                WallQuadG(mbs[0], dir * (W + 1.6f) + t * 1.2f, t, 0.3f, 3.6f, 4.75f, 0, new Color(0.92f, 0.9f, 0.84f));
                WallQuadG(mbs[0], dir * (W + 1.6f) - t * 1.2f, t, 0.3f, 3.6f, 4.75f, 0, new Color(0.92f, 0.9f, 0.84f));
            }
            for (int g = 0; g < 4; g++)
            {
                var mat = ArtKit.NewMat(blend, tex, 2933 - g, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Arena Crowd" + g);   // 前排最後畫
                var go = MeshObj("Crowd" + g, mbs[g].Build("ArenaCrowd" + g), origin, mat);
                crowd[g] = go.transform; crowdBase[g] = origin;
            }
        }

        static void WallQuadG(ArtKit.MB mb, Vector3 c, Vector3 t, float hw, float y0, float y1, int variant, Color col)
            => WallQuad(mb, c, t, hw, y0, y1, new Vector2(variant / 4f + 0.004f, 0f), new Vector2((variant + 1) / 4f - 0.004f, 1f), col);

        GameObject MeshObj(string name, Mesh mesh, Vector3 pos, params Material[] mats) => ArtKit.MeshObj(transform, name, mesh, pos, mats);
    }

    /// <summary>角鬥場用的程式產生貼圖</summary>
    static class ArenaTex
    {
        static float SS(float a, float b, float x) => ArtKit.SS(a, b, x);
        static float Fbm(float u, float v, int s, int p, int o) => ArtKit.Fbm(u, v, s, p, o);
        static float Hash(int x, int y, int s) => ArtKit.Hash(x, y, s);

        /// <summary>耙過的沙地：暖色沙＋細細的耙紋＋腳印＋深色污漬（可平鋪，a≈0.92）</summary>
        public static Texture2D Sand() { const int S = 256; return ArtKit.Finish(ArtKit.NewTex("Sand", S, S, true, true), SandPixels()); }

        public static Color32[] SandPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 3, 4, 5), fine = Fbm(u, v, 9, 32, 2);
                    var c = Color.Lerp(new Color(0.8f, 0.64f, 0.42f), new Color(0.93f, 0.8f, 0.58f), n);
                    c *= 0.94f + 0.1f * fine;
                    // 耙紋：沿 u 的細波紋（不時被踩亂）
                    float rake = 0.5f + 0.5f * Mathf.Sin((v * 30f + Fbm(u, v, 5, 2, 2) * 1.5f) * Mathf.PI * 2f);
                    float disturb = SS(0.45f, 0.65f, Fbm(u, v, 13, 3, 3));
                    c *= 1f - 0.06f * rake * (1f - disturb);
                    // 污漬：大片深一點的暗紅褐色
                    float stain = SS(0.62f, 0.78f, Fbm(u, v, 21, 3, 4));
                    c = Color.Lerp(c, new Color(0.55f, 0.36f, 0.24f), stain * 0.35f);
                    // 腳印：每格有機會出現一對（左右腳）
                    const int cells = 6;
                    int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
                    for (int j = -1; j <= 1; j++)
                        for (int i = -1; i <= 1; i++)
                        {
                            int wx = ((cx + i) % cells + cells) % cells, wy = ((cy + j) % cells + cells) % cells;
                            if (Hash(wx, wy, 31) > 0.55f) continue;
                            float ang = Hash(wx, wy, 33) * Mathf.PI * 2f, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                            for (int f = 0; f < 2; f++)
                            {
                                float side = f == 0 ? -0.022f : 0.022f, along = f == 0 ? 0f : 0.05f;
                                float px0 = (cx + i + 0.5f + (Hash(wx, wy, 35) - 0.5f) * 0.5f) / cells + ca * along - sa * side;
                                float py0 = (cy + j + 0.5f + (Hash(wx, wy, 37) - 0.5f) * 0.5f) / cells + sa * along + ca * side;
                                float dx = u - px0, dy = v - py0;
                                float lx = dx * ca + dy * sa, ly = -dx * sa + dy * ca;
                                float e = (lx * lx) / (0.028f * 0.028f) + (ly * ly) / (0.013f * 0.013f);
                                if (e < 1.6f) c *= 1f - 0.16f * (1f - SS(0.7f, 1.6f, e));
                            }
                        }
                    c.a = 0.92f;
                    px[y * S + x] = c;
                }
            return px;
        }

        /// <summary>褪色的彩繪圓環（以場地中心為圓心，0.5＝中心、半徑 0.5＝場地最遠半徑）</summary>
        public static Texture2D RingMarks() { const int S = 256; return ArtKit.Finish(ArtKit.NewTex("RingMarks", S, S, false, false), RingPixels()); }

        public static Color32[] RingPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S - 0.5f, v = y / (float)S - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f, a = Mathf.Atan2(v, u);
                    float line = 0f;
                    line = Mathf.Max(line, 1f - SS(0.006f, 0.014f, Mathf.Abs(r - 0.1f)));
                    line = Mathf.Max(line, 1f - SS(0.008f, 0.016f, Mathf.Abs(r - 0.3f)));
                    line = Mathf.Max(line, 1f - SS(0.006f, 0.013f, Mathf.Abs(r - 0.58f)));
                    line = Mathf.Max(line, 1f - SS(0.006f, 0.013f, Mathf.Abs(r - 0.63f)));
                    // 兩圈之間的放射刻度
                    if (r > 0.58f && r < 0.63f) line = Mathf.Max(line, 1f - SS(0.01f, 0.02f, Mathf.Abs(Mathf.Repeat(a / (Mathf.PI * 2f) * 24f, 1f) - 0.5f) * r * 0.26f));
                    // 中央的星形
                    float star = Mathf.Abs(Mathf.Cos(a * 4f));
                    if (r < 0.1f) line = Mathf.Max(line, (1f - SS(0.05f + 0.03f * star, 0.06f + 0.03f * star, r)) * 0.8f);
                    float wear = SS(0.3f, 0.6f, Fbm(x / (float)S, y / (float)S, 41, 6, 4));   // 被踩掉的地方
                    float alpha = line * 0.4f * wear;
                    px[y * S + x] = new Color(0.62f, 0.24f, 0.14f, alpha);
                }
            return px;
        }

        /// <summary>
        /// 牆與看台的貼圖分區（u 可平鋪、v 分區）：
        /// 0～0.06 腳座、0.06～0.30 牆面石塊、0.30～0.36 紅金飾帶、0.36～0.40 壓頂、0.40～0.50 看台踢面、0.50～0.60 踏面、0.60～1 拱廊外牆
        /// </summary>
        public static Texture2D Stone()
        {
            if (stoneTex != null) return stoneTex;
            const int W = 512, H = 512;
            stoneTex = ArtKit.Finish(ArtKit.NewTex("Stone", W, H, true, false), StonePixels());
            return stoneTex;
        }
        static Texture2D stoneTex;

        public static Color32[] StonePixels()
        {
            const int W = 512, H = 512; var px = new Color32[W * H];
            var sand = new Color(0.84f, 0.72f, 0.55f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = (y + 0.5f) / H;
                    float n = Fbm(u, v, 7, 8, 4);
                    Color c;
                    if (v < 0.06f)
                    {
                        c = new Color(0.6f, 0.55f, 0.48f) * (0.85f + 0.25f * n);
                        if (Mathf.Abs(Mathf.Repeat(u * 6f, 1f) - 0.5f) > 0.48f) c *= 0.7f;
                    }
                    else if (v < 0.30f)
                    {
                        // 石塊：每塊 1m 寬、0.45m 高（u 1＝2m；牆面 v 0.24＝1.75m），上下排錯開半塊
                        float by = (v - 0.06f) / 0.24f * 1.75f / 0.45f;
                        int row = Mathf.FloorToInt(by);
                        float bx = u * 2f + (row % 2) * 0.5f;   // 每個 tile（2m）兩塊
                        int col = Mathf.FloorToInt(bx);
                        float fx = bx - col, fy = by - row;
                        float tone = 0.88f + 0.16f * Hash(col % 2, row, 3);
                        c = sand * tone * (0.9f + 0.15f * n);
                        float mortar = Mathf.Max(1f - SS(0.0f, 0.04f, fx), 1f - SS(0.0f, 0.04f, 1f - fx));
                        mortar = Mathf.Max(mortar, Mathf.Max(1f - SS(0.0f, 0.06f, fy), 1f - SS(0.0f, 0.06f, 1f - fy)));
                        c = Color.Lerp(c, new Color(0.45f, 0.38f, 0.3f), mortar * 0.7f);
                        c *= 1f - 0.25f * SS(0.12f, 0.06f, v);   // 牆腳的髒污
                        c *= 1f - 0.12f * SS(0.55f, 0.75f, Fbm(u, v, 17, 4, 3));   // 水漬
                    }
                    else if (v < 0.36f)
                    {
                        float t = (v - 0.30f) / 0.06f;
                        c = new Color(0.6f, 0.12f, 0.09f) * (0.9f + 0.15f * n);
                        bool goldLine = t < 0.14f || t > 0.86f;
                        float m = Mathf.Repeat(u * 16f, 1f) - 0.5f, dy = t - 0.5f;
                        bool motif = Mathf.Abs(m) + Mathf.Abs(dy) * 1.2f < 0.24f && Mathf.Abs(m) + Mathf.Abs(dy) * 1.2f > 0.14f;   // 菱形
                        if (goldLine || motif) c = new Color(0.9f, 0.7f, 0.3f) * (0.9f + 0.15f * n);
                    }
                    else if (v < 0.40f)
                    {
                        c = new Color(0.92f, 0.85f, 0.7f) * (0.92f + 0.1f * n);
                        if (v > 0.395f) c *= 0.8f;
                    }
                    else if (v < 0.50f)
                    {
                        c = new Color(0.62f, 0.52f, 0.4f) * (0.88f + 0.2f * n);
                        if (Mathf.Abs(Mathf.Repeat(u * 8f, 1f) - 0.5f) > 0.48f) c *= 0.72f;
                        c *= 1f - 0.3f * SS(0.47f, 0.5f, v);   // 踏面邊緣的陰影
                    }
                    else if (v < 0.60f)
                    {
                        c = new Color(0.88f, 0.79f, 0.63f) * (0.9f + 0.15f * n);
                        if (Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f) > 0.49f) c *= 0.8f;
                    }
                    else
                    {
                        // 拱廊：每 1m 一個拱（u 0.5），拱內是暗的
                        float t = (v - 0.60f) / 0.40f;
                        c = sand * (0.9f + 0.15f * n);
                        float bx = Mathf.Repeat(u * 2f, 1f) - 0.5f;
                        float archTop = 0.62f, half = 0.3f;
                        bool inArch = t > 0.06f && Mathf.Abs(bx) < half && (t < archTop || (bx * bx) / (half * half) + ((t - archTop) * (t - archTop)) / (0.16f * 0.16f) < 1f);
                        if (inArch) c = Color.Lerp(new Color(0.16f, 0.11f, 0.08f), new Color(0.32f, 0.23f, 0.16f), t);
                        else
                        {
                            float ring = (bx * bx) / (0.36f * 0.36f) + ((t - archTop) * (t - archTop)) / (0.22f * 0.22f);
                            if (t > archTop && ring < 1f) c *= 0.9f;   // 拱圈石
                            if (t > 0.9f) c = new Color(0.93f, 0.86f, 0.72f) * (0.92f + 0.1f * n);   // 簷口
                            if (t > 0.88f && t < 0.9f) c *= 0.7f;
                        }
                    }
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            return px;
        }

        /// <summary>拱門：石砌拱圈＋黑暗的通道＋黑鐵柵門（拱外透明）。u＝門寬、v＝高（2.54m）</summary>
        public static Texture2D Portcullis() { const int W = 128, H = 256; return ArtKit.Finish(ArtKit.NewTex("Portcullis", W, H, false, false), PortcullisPixels()); }

        public static Color32[] PortcullisPixels()
        {
            const int W = 128, H = 256; var px = new Color32[W * H];
            const float spring = 0.6f, open = 0.78f, ry = 0.3f;   // 拱腳高度、開口半寬（x 單位 −1～1）、拱的高度
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = x / (float)(W - 1) * 2f - 1f, fy = y / (float)(H - 1);
                    float ex = fx / open, ey = fy > spring ? (fy - spring) / ry : 0f;
                    float e = ex * ex + ey * ey;                       // <1 在開口內
                    float ex2 = fx / 0.98f, ey2 = fy > spring ? (fy - spring) / (ry + 0.08f) : 0f;
                    float e2 = ex2 * ex2 + ey2 * ey2;                  // <1 在拱圈石外緣內
                    Color c; float a;
                    if (e < 1f)
                    {
                        // 通道：深處很暗，底部看得到一點沙地
                        c = Color.Lerp(new Color(0.07f, 0.05f, 0.045f), new Color(0.13f, 0.09f, 0.07f), fy);
                        if (fy < 0.1f) c = Color.Lerp(new Color(0.36f, 0.28f, 0.19f), c, fy / 0.1f);
                        // 柵門：直條＋橫條，左側有亮邊；下端尖頭
                        float gx = Mathf.Repeat(fx * 6.5f + 0.5f, 1f), gy = Mathf.Repeat(fy * 7f, 1f);
                        bool vbar = gx < 0.16f && fy > 0.03f - 0.03f * (1f - Mathf.Abs(gx - 0.08f) / 0.08f);
                        bool hbar = gy < 0.07f && fy > 0.1f;
                        if (vbar || hbar)
                        {
                            c = new Color(0.2f, 0.19f, 0.2f);
                            if (vbar && gx < 0.05f) c = new Color(0.46f, 0.45f, 0.47f);
                            if (vbar && hbar) c = new Color(0.55f, 0.52f, 0.5f);   // 鉚釘
                        }
                        a = 1f;
                    }
                    else if (e2 < 1f)
                    {
                        // 拱圈石（放射狀分塊）
                        float ang = Mathf.Atan2(Mathf.Max(0f, fy - spring) * 2.5f, fx);
                        float blocks = Mathf.Abs(Mathf.Repeat(ang / Mathf.PI * 9f, 1f) - 0.5f);
                        c = new Color(0.86f, 0.76f, 0.6f) * (0.9f + 0.12f * ArtKit.Fbm(x / (float)W, y / (float)H, 3, 4, 3));
                        if (fy > spring && blocks > 0.45f) c *= 0.7f;
                        if (fy <= spring && Mathf.Abs(Mathf.Repeat(fy * 6f, 1f) - 0.5f) > 0.46f) c *= 0.7f;
                        if (e < 1.08f) c *= 0.65f;   // 開口邊緣的陰影
                        a = 1f;
                    }
                    else { c = Color.black; a = 0f; }
                    c.a = a;
                    px[y * W + x] = c;
                }
            return px;
        }

        /// <summary>旗幟（三格）：0＝紅底金邊燕尾旗、1＝金底紅邊燕尾旗、2＝帷幕（紅底金條、扇形下緣、金流蘇）</summary>
        public static Texture2D Banners() { const int W = 192, H = 128; return ArtKit.Finish(ArtKit.NewTex("Banners", W, H, false, false), BannerPixels()); }

        public static Color32[] BannerPixels()
        {
            const int W = 192, H = 128; var px = new Color32[W * H];
            var crimson = new Color(0.66f, 0.1f, 0.08f); var gold = new Color(0.93f, 0.72f, 0.28f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int cell = x / 64; float lx = (x % 64) / 63f, ly = y / 127f;   // 0～1
                    float fold = 0.92f + 0.1f * Mathf.Sin(lx * Mathf.PI * 4f);   // 布的皺褶
                    Color c; float a = 1f;
                    if (cell < 2)
                    {
                        var bg = cell == 0 ? crimson : gold; var fg = cell == 0 ? gold : crimson;
                        float notch = 0.18f * (1f - Mathf.Abs(lx - 0.5f) * 2f);   // 燕尾
                        if (ly < notch) a = 0f;
                        c = bg;
                        bool border = lx < 0.08f || lx > 0.92f || ly > 0.93f || (ly < notch + 0.05f);
                        if (border) c = fg;
                        // 徽記：月桂環＋中間的菱形星
                        float dx = (lx - 0.5f) * 0.5f, dy = (ly - 0.58f);   // 格子是 1:2，把 x 縮回同一比例
                        float r = Mathf.Sqrt(dx * dx * 4f + dy * dy * 4f) * 0.5f, ang = Mathf.Atan2(dy, dx);
                        if (r > 0.1f && r < 0.135f && ang > -1.25f || r > 0.1f && r < 0.135f && ang < -1.9f) c = fg;
                        if (Mathf.Abs(dx) * 2.2f + Mathf.Abs(dy) < 0.06f) c = fg;
                        if (ly > 0.96f) c *= 0.6f;   // 頂端的橫桿陰影
                    }
                    else
                    {
                        float scallop = 0.08f + 0.06f * Mathf.Abs(Mathf.Sin(lx * Mathf.PI * 4f));
                        if (ly < scallop - 0.06f) a = 0f;
                        c = crimson;
                        if (Mathf.Abs(Mathf.Repeat(lx * 6f, 1f) - 0.5f) < 0.06f) c = gold;
                        if (ly > 0.88f && ly < 0.94f) c = gold;
                        if (ly < scallop + 0.03f && a > 0f) c = Color.Lerp(gold, gold * 0.7f, Mathf.Repeat(lx * 60f, 1f));   // 流蘇
                    }
                    c *= fold; c.a = a;
                    px[y * W + x] = c;
                }
            return px;
        }

        /// <summary>觀眾剪影（四格 64×128，白色＝由頂點色上色）：0＝坐著、1＝舉一手、2＝舉雙手、3＝戴頭巾</summary>
        public static Texture2D Crowd() { const int W = 256, H = 128; return ArtKit.Finish(ArtKit.NewTex("Crowd", W, H, false, false), CrowdPixels()); }

        static float Capsule(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float vx = bx - ax, vy = by - ay, wx = px - ax, wy = py - ay;
            float t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy));
            float dx = wx - vx * t, dy = wy - vy * t;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        public static Color32[] CrowdPixels()
        {
            const int W = 256, H = 128; var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int cell = x / 64; float lx = x % 64 + 0.5f, ly = y + 0.5f;
                    // 身體：圓角梯形（SDF，像素單位）
                    float bxw = 22f - (ly / 72f) * 3f;
                    float qx = Mathf.Abs(lx - 32f) - (bxw - 12f), qy = Mathf.Abs(ly - 36f) - (36f - 12f);
                    float body = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - 12f;
                    float neck = Capsule(lx, ly, 32f, 70f, 32f, 80f, 5f);
                    float hr = cell == 3 ? 15f : 13f;
                    float head = Mathf.Sqrt((lx - 32f) * (lx - 32f) + (ly - 88f) * (ly - 88f) * (cell == 3 ? 0.85f : 1f)) - hr;
                    float d = Mathf.Min(body, Mathf.Min(neck, head));
                    if (cell == 1) d = Mathf.Min(d, Mathf.Min(Capsule(lx, ly, 14f, 64f, 9f, 114f, 5f), Mathf.Sqrt((lx - 9f) * (lx - 9f) + (ly - 117f) * (ly - 117f)) - 6.5f));
                    if (cell == 2)
                    {
                        d = Mathf.Min(d, Mathf.Min(Capsule(lx, ly, 14f, 64f, 7f, 112f, 5f), Mathf.Sqrt((lx - 7f) * (lx - 7f) + (ly - 115f) * (ly - 115f)) - 6f));
                        d = Mathf.Min(d, Mathf.Min(Capsule(lx, ly, 50f, 64f, 57f, 112f, 5f), Mathf.Sqrt((lx - 57f) * (lx - 57f) + (ly - 115f) * (ly - 115f)) - 6f));
                    }
                    float a = Mathf.Clamp01(0.5f - d);
                    // 明暗：頭亮、身體往下變暗；邊緣一圈深色描線（賽璐珞感）
                    float k = head < 0.5f ? 1f : 0.72f + 0.25f * Mathf.Clamp01(ly / 72f);
                    if (head < 0.5f && cell != 3) k = Mathf.Lerp(1f, 0.85f, Mathf.Clamp01((lx - 26f) / 14f));   // 臉的一側陰影
                    float ink = 1f - SS(-3f, -1.5f, d);
                    k *= 1f - 0.55f * ink;
                    px[y * W + x] = new Color(k, k, k, a);
                }
            return px;
        }

        /// <summary>背景：午後金黃色的塵霧天空、低矮的遠山與絲絲雲層（u 可平鋪）</summary>
        public static Texture2D Backdrop() { const int W = 1024, H = 256; return ArtKit.Finish(ArtKit.NewTex("Backdrop", W, H, true, false), BackdropPixels()); }

        public static Color32[] BackdropPixels()
        {
            const int W = 1024, H = 256; var px = new Color32[W * H];
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                float hill = 0.16f + 0.08f * Fbm(u, 0.2f, 5, 6, 4);
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)H;
                    Color c;
                    if (v < hill)
                    {
                        c = Color.Lerp(new Color(0.62f, 0.5f, 0.36f), new Color(0.78f, 0.64f, 0.45f), v / hill);
                        c.a = 0.95f;
                    }
                    else
                    {
                        float sky = (v - hill) / (1f - hill);
                        c = Color.Lerp(new Color(0.98f, 0.84f, 0.58f), new Color(0.8f, 0.72f, 0.62f), SS(0f, 0.8f, sky));
                        float cloud = SS(0.5f, 0.75f, Fbm(u * 2f, v * 0.6f, 13, 4, 5));
                        c = Color.Lerp(c, new Color(1f, 0.93f, 0.78f), cloud * 0.5f);
                        float du = Mathf.Abs(Mathf.DeltaAngle(u * 360f, 110f)) / 360f, dv = v - 0.75f;
                        c = Color.Lerp(c, new Color(1f, 0.95f, 0.75f), Mathf.Exp(-(du * du) / 0.004f - (dv * dv) / 0.03f) * 0.6f);   // 太陽光暈
                        c.a = 0.93f;
                    }
                    px[y * W + x] = c;
                }
            }
            return px;
        }

        /// <summary>天空頂蓋：中心稍淡的塵霧藍，外圈金黃</summary>
        public static Texture2D Cap() { const int S = 128; return ArtKit.Finish(ArtKit.NewTex("SkyCap", S, S, false, false), CapPixels()); }

        public static Color32[] CapPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    var c = Color.Lerp(new Color(0.72f, 0.72f, 0.7f), new Color(0.8f, 0.72f, 0.62f), r);
                    c = Color.Lerp(c, new Color(0.95f, 0.88f, 0.74f), SS(0.5f, 0.75f, Fbm(u, v, 51, 4, 4)) * 0.45f);
                    c.a = 0.93f;
                    px[y * S + x] = c;
                }
            return px;
        }
    }
}
