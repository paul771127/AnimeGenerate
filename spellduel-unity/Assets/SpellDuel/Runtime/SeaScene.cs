using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 海上礁石場景（無盡模式第 21～30 波）：全部用程式產生，不需要任何圖檔。
    ///   1. 場地內的地板＝礁石頂面：濕亮的深色岩石、藤壺、映著天空的小潮池、海藻、貝殼，裂縫邊有一點白色泡沫。
    ///   2. 礁石邊緣（場地外約 0.3～2m）：一圈往海裡傾斜的岩坡＋鋸齒狀的濕岩塊，岩石與海水交界處有會脈動、推進退回的白色浪花。
    ///   3. 場地外：一大片會起伏的海面（到 35m），兩層波紋貼圖往不同方向流動；海上散落著岩塊與海蝕柱，還有一顆浮標與一根沉船桅杆。
    ///   4. 遠方背景：海平線、遠方島嶼與海蝕柱剪影、明亮的藍天白雲與太陽光暈（圓柱內側）＋天空頂蓋。
    ///   5. 礁石邊的水霧、不時打上岩石的浪花飛濺、遠處盤旋的海鷗、海面上閃爍的陽光。
    /// 地板與背景都是半透明，仍隱約看得到真實的房間（安全）。座標都是 AR session 座標。
    /// 所有不透明的東西（岩坡、岩塊、海蝕柱、浮標、桅杆）一律在場地多邊形之外。
    /// </summary>
    public class SeaScene : MonoBehaviour
    {
        const float SeaRadius = 35f, BackRadius = 32f, BackHeight = 22f;
        const float WaterDrop = 0.22f;   // 海面比地板低多少
        Material floorMat, waterMat, glintMat, foamMatA, foamMatB, backMat, capMat, lampMat;
        Mesh waterMesh;
        Vector3[] waterBase, waterVerts;
        float[] waterAmp;
        ParticleSystem splash, buoyFlash;
        readonly List<Vector3> splashSpots = new List<Vector3>();
        Transform buoy, mast;
        Vector3 buoyPos, mastPos;
        Quaternion buoyRot, mastRot;
        Vector3 center;
        float clock, splashTimer = 1f, flashTimer = 1.5f;

        // 海鷗：每隻有自己的小網格（翅膀拍動時改頂點）
        class Gull { public Transform t; public Mesh mesh; public Vector3[] v; public float r, h, ang, speed, phase; }
        readonly List<Gull> gulls = new List<Gull>();

        public static SeaScene Create(List<Vector3> polygon, float floorY)
        {
            var go = new GameObject("SeaScene");
            var ss = go.AddComponent<SeaScene>();
            ss.Build(polygon, floorY);
            ss.variantArt=SceneVariantArt.Install(ss.transform,polygon,floorY,2);
            return ss;
        }

        public void SetVisible(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }
        /// <summary>同一主題的場景變化（0～2；SoloBattle.SceneVariant 決定）。第一次建場預建三組，切換只改顯示</summary>
        public int Variant { get; private set; }
        SceneVariantArt variantArt;
        public void SetVariant(int v) { Variant = Mathf.Clamp(v, 0, 2); if(variantArt)variantArt.Set(Variant); }

        // ================================================================ 建置
        void Build(List<Vector3> poly, float floorY)
        {
            bool hasPoly = poly != null && poly.Count >= 3;
            center = Vector3.zero;
            if (hasPoly) { foreach (var p in poly) center += p; center /= poly.Count; }
            center.y = floorY;
            float areaR = 2f;
            if (poly != null) foreach (var p in poly) areaR = Mathf.Max(areaR, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z)));
            float waterY = floorY - WaterDrop;

            var blend = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var add = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            var diffuse = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Mobile/Diffuse") ?? blend;

            // ---- 礁石輪廓：從中心往各方向，場地多邊形最遠的邊界距離 R(θ)（之後岩坡、浪花、海面都沿著它往外長）
            var ring = EdgeRing(hasPoly ? poly : null, center, areaR);
            int N = ring.Count;

            // ---- 海面（從礁石岩坡底下一路到 35m；兩層：波紋底色＋疊加的反光）
            waterMesh = WaterMesh(ring, out waterBase, out waterAmp, areaR);
            waterVerts = (Vector3[])waterBase.Clone();
            waterMat = NewMat(blend, WaterTex(), 2903, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            waterMat.mainTextureScale = new Vector2(1f / 4f, 1f / 4f);
            MeshObj("Ocean", waterMesh, new Vector3(center.x, waterY, center.z), waterMat);
            glintMat = NewMat(add, GlintTex(), 2904, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            glintMat.mainTextureScale = new Vector2(1f / 2.7f, 1f / 2.7f);
            MeshObj("OceanGlint", waterMesh, new Vector3(center.x, waterY, center.z), glintMat);

            // ---- 場地內：礁石頂面
            if (hasPoly)
            {
                floorMat = NewMat(blend, ReefFloorTex(), 2910, new Color(0.5f, 0.5f, 0.5f, 0.47f));
                MeshObj("ReefTop", PolyMesh(poly, center, 2f), new Vector3(0f, 0.004f, 0f), floorMat);
            }

            // ---- 礁石邊緣的岩坡（不透明，寫入深度 → 水面下的部分會被海水蓋住）
            var slopeMat = new Material(diffuse) { mainTexture = RockSlopeTex(), color = new Color(0.9f, 0.9f, 0.9f), name = "Sea ReefSlope" };
            MeshObjDiffuse("ReefSlope", SlopeMesh(ring, floorY, waterY), Vector3.zero, new[] { slopeMat });

            // ---- 岩石與海水交界的浪花（兩層，錯開節奏地推進、退回）
            var foamMesh = FoamMesh(ring, waterY + 0.012f);
            var foamTex = FoamTex();
            foamMatA = NewMat(blend, foamTex, 2920, new Color(0.5f, 0.5f, 0.5f, 0.45f));
            MeshObj("SurfA", foamMesh, Vector3.zero, foamMatA);
            foamMatB = NewMat(blend, foamTex, 2921, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            foamMatB.mainTextureScale = new Vector2(1.37f, 1f);
            MeshObj("SurfB", foamMesh, Vector3.zero, foamMatB);

            // ---- 遠方背景：海平線＋島嶼＋藍天白雲（圓柱內側）＋頂蓋
            backMat = NewMat(blend, BackdropTex(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            MeshObj("Backdrop", Cylinder(BackRadius, BackHeight, 96), new Vector3(center.x, floorY - 1.5f, center.z), backMat);
            capMat = NewMat(blend, CapTex(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            MeshObj("SkyCap", Disc(BackRadius, 48), new Vector3(center.x, floorY - 1.5f + BackHeight, center.z), capMat);

            // ---- 礁石邊的鋸齒岩塊（全部合併成一個網格；每塊都檢查不會伸進場地）
            var rockMat = new Material(diffuse) { mainTexture = RockTex(), color = new Color(0.85f, 0.85f, 0.85f), name = "Sea Rock" };
            var rng = new System.Random(37);
            {
                var mb = new MB(1);
                float perim = 0f;
                for (int i = 0; i < N; i++) perim += (RingPos(ring[(i + 1) % N], 1f) - RingPos(ring[i], 1f)).magnitude;
                int want = Mathf.Clamp(Mathf.RoundToInt(perim / 0.42f), 24, 70);
                for (int k = 0, tries = 0; k < want && tries < want * 6; tries++)
                {
                    var s = ring[rng.Next(N)];
                    bool outer = rng.NextDouble() < 0.35;   // 外圈：泡在水裡、被浪拍打的大石頭
                    float d = outer ? 1.0f + (float)rng.NextDouble() * 0.9f : 0.3f + (float)rng.NextDouble() * 0.8f;
                    float sz = outer ? 0.28f + (float)rng.NextDouble() * 0.32f : 0.15f + (float)rng.NextDouble() * 0.25f;
                    var p = RingPos(s, d);
                    if (hasPoly)
                    {
                        var p2 = new Vector2(center.x + p.x, center.z + p.z);
                        if (PointInPoly(poly, p2) || DistToPoly(poly, p2) < sz * 1.25f + 0.08f) continue;
                    }
                    float hy = sz * (0.55f + 0.5f * (float)rng.NextDouble());
                    // 內圈：坐在岩坡上；外圈：半泡在海裡、露出上半部
                    float cy = outer ? -WaterDrop + hy * 0.05f : SlopeHeight(d, floorY, waterY) - floorY + hy * 0.15f;
                    var c = new Vector3(p.x, cy, p.z);
                    FacetRock(mb, 0, c, new Vector3(sz * (0.9f + 0.4f * (float)rng.NextDouble()), hy, sz * (0.8f + 0.4f * (float)rng.NextDouble())),
                              7, 5, 500 + k, 0.45f, (float)rng.NextDouble() * 6.28f);
                    if (outer && splashSpots.Count < 24) splashSpots.Add(new Vector3(center.x + p.x, waterY + 0.05f, center.z + p.z));
                    k++;
                }
                MeshObjDiffuse("EdgeRocks", mb.Build("SeaEdgeRocks"), new Vector3(center.x, floorY, center.z), new[] { rockMat });
            }
            if (splashSpots.Count == 0)
                for (int i = 0; i < 8; i++) { var p = RingPos(ring[i * N / 8], 1.2f); splashSpots.Add(new Vector3(center.x + p.x, waterY + 0.05f, center.z + p.z)); }

            // ---- 海上的岩塊與海蝕柱（場地外 4～15m）
            {
                var mb = new MB(1);
                for (int k = 0; k < 9; k++)
                {
                    float a = (k + (float)rng.NextDouble() * 0.7f) / 9f * Mathf.PI * 2f, r = areaR + 4f + (float)rng.NextDouble() * 11f;
                    var c = new Vector3(Mathf.Cos(a) * r, -WaterDrop - 0.3f, Mathf.Sin(a) * r);
                    if (k % 3 == 0)
                        SeaStack(mb, 0, c, 0.7f + (float)rng.NextDouble() * 0.7f, 2.2f + (float)rng.NextDouble() * 3.2f, 700 + k);
                    else
                    {
                        float sz = 0.5f + (float)rng.NextDouble() * 0.9f;
                        FacetRock(mb, 0, c, new Vector3(sz * 1.2f, sz * (0.7f + 0.5f * (float)rng.NextDouble()), sz), 8, 5, 800 + k, 0.4f, (float)rng.NextDouble() * 6.28f);
                    }
                    // 旁邊幾顆小石頭
                    int smalls = 1 + rng.Next(3);
                    for (int m = 0; m < smalls; m++)
                    {
                        float sz = 0.15f + (float)rng.NextDouble() * 0.3f;
                        var o = c + new Vector3(((float)rng.NextDouble() - 0.5f) * 3f, 0.1f, ((float)rng.NextDouble() - 0.5f) * 3f);
                        FacetRock(mb, 0, o, new Vector3(sz, sz * 0.8f, sz * 0.9f), 6, 4, 900 + k * 7 + m, 0.4f, (float)rng.NextDouble() * 6.28f);
                    }
                }
                MeshObjDiffuse("SeaRocks", mb.Build("SeaRocks"), new Vector3(center.x, floorY, center.z), new[] { rockMat });
            }

            // ---- 浮標（紅白相間，頂端燈一閃一閃）
            {
                float a = 0.9f + (float)rng.NextDouble() * 0.5f, r = areaR + 7f;
                buoyPos = new Vector3(center.x + Mathf.Cos(a) * r, waterY - 0.12f, center.z + Mathf.Sin(a) * r);
                var red = new Material(diffuse) { color = new Color(0.85f, 0.16f, 0.1f), name = "Sea BuoyRed" };
                var white = new Material(diffuse) { color = new Color(0.92f, 0.92f, 0.88f), name = "Sea BuoyWhite" };
                lampMat = NewMat(blend, WhiteTex(), 2950, new Color(0.5f, 0.35f, 0.2f, 0.5f));
                var mb = new MB(3);
                Lathe(mb, 0, Vector3.zero, Quaternion.identity, new[] { 0f, 0.12f, 0.3f, 0.38f }, new[] { 0.3f, 0.4f, 0.38f, 0.22f }, 12, true, true, Color.white);
                Lathe(mb, 1, new Vector3(0f, 0.38f, 0f), Quaternion.identity, new[] { 0f, 0.35f, 0.65f }, new[] { 0.17f, 0.13f, 0.1f }, 10, false, false, Color.white);
                Lathe(mb, 0, new Vector3(0f, 1.03f, 0f), Quaternion.identity, new[] { 0f, 0.14f }, new[] { 0.11f, 0.11f }, 10, true, false, Color.white);
                Lathe(mb, 2, new Vector3(0f, 1.17f, 0f), Quaternion.identity, new[] { 0f, 0.07f, 0.12f }, new[] { 0.06f, 0.065f, 0.02f }, 8, true, false, Color.white);
                var go = MeshObjDiffuse("Buoy", mb.Build("SeaBuoy"), buoyPos, new[] { red, white, lampMat });
                buoy = go.transform; buoyPos = buoy.localPosition; buoyRot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f); buoy.rotation = buoyRot;
            }

            // ---- 沉船：斜插在海裡的斷桅杆＋橫桁＋露出水面的船肋
            {
                float a = 3.6f + (float)rng.NextDouble() * 0.8f, r = areaR + 13f;
                mastPos = new Vector3(center.x + Mathf.Cos(a) * r, waterY - 0.4f, center.z + Mathf.Sin(a) * r);
                var wood = new Material(diffuse) { mainTexture = RockTex(), color = new Color(0.55f, 0.4f, 0.28f), name = "Sea Wood" };
                var mb = new MB(1);
                var tilt = Quaternion.Euler(0f, 0f, 17f);
                Lathe(mb, 0, Vector3.zero, tilt, new[] { 0f, 2.5f, 4.6f, 4.9f }, new[] { 0.13f, 0.11f, 0.085f, 0.05f }, 8, true, false, Color.white);
                // 橫桁（歪一邊）
                var yardPos = tilt * new Vector3(0f, 3.6f, 0f);
                var yardRot = tilt * Quaternion.Euler(0f, 30f, 0f) * Quaternion.Euler(0f, 0f, 82f);
                Lathe(mb, 0, yardPos + yardRot * new Vector3(0f, -1.1f, 0f), yardRot, new[] { 0f, 1.1f, 2.0f }, new[] { 0.04f, 0.06f, 0.035f }, 6, true, true, Color.white);
                // 船肋：彎曲的細木條，一端在水裡
                for (int k = 0; k < 4; k++)
                {
                    var ribPos = new Vector3(-1.2f + k * 0.55f, 0.1f, 0.9f + k * 0.1f);
                    var hs = new[] { 0f, 0.5f, 1f, 1.4f }; var rs = new[] { 0.07f, 0.065f, 0.055f, 0.03f };
                    var bend = new[] { Vector3.zero, new Vector3(0f, 0f, -0.12f), new Vector3(0f, 0f, -0.35f), new Vector3(0f, 0f, -0.62f) };
                    var cols = new[] { Color.white, Color.white, Color.white, Color.white };
                    LatheCore(mb, 0, ribPos, Quaternion.Euler(0f, 0f, -8f + k * 3f), hs, rs, cols, 6, true, false, bend);
                }
                var go = MeshObjDiffuse("WreckMast", mb.Build("SeaWreck"), mastPos, new[] { wood });
                mast = go.transform; mastPos = mast.localPosition; mastRot = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 70f, 0f); mast.rotation = mastRot;
            }

            // ---- 海鷗（遠遠地在高處盤旋）
            {
                var gullMat = NewMat(blend, WhiteTex(), 2950, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                for (int i = 0; i < 5; i++)
                {
                    var g = new Gull
                    {
                        r = areaR + 6f + (float)rng.NextDouble() * 12f, h = 6f + (float)rng.NextDouble() * 5f,
                        ang = (float)rng.NextDouble() * 6.28f, speed = (rng.NextDouble() < 0.5 ? -1f : 1f) * (0.08f + (float)rng.NextDouble() * 0.08f),
                        phase = (float)rng.NextDouble() * 10f
                    };
                    g.mesh = GullMesh(out g.v);
                    var go = new GameObject("Seagull");
                    go.transform.SetParent(transform, false);
                    float sc = 1.5f + (float)rng.NextDouble() * 0.6f;
                    go.transform.localScale = new Vector3(sc, sc, sc);
                    go.AddComponent<MeshFilter>().sharedMesh = g.mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = gullMat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    g.t = go.transform;
                    gulls.Add(g);
                }
            }

            // ---- 粒子：礁石邊的水霧、浪花飛濺、海面閃光、浮標燈光
            var mist = SpellFx.NewPS(transform, "SeaMist", SpellFx.BlendMat(SpellFx.TexSoft), 50, true);
            mist.transform.SetPositionAndRotation(new Vector3(center.x, waterY + 0.25f, center.z), Quaternion.Euler(90f, 0f, 0f));   // 圓形發射區躺平
            SpellFx.Disc(mist, areaR + 2.2f, 0.4f);   // 只在礁石外圈
            var mm = mist.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            mm.startColor = new Color(0.9f, 0.95f, 0.97f, 0.16f);
            mist.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.6f;
            SpellFx.Rise(mist, 0.08f);
            SpellFx.Noise(mist, 0.2f, 0.15f);
            SpellFx.FadeInOut(mist);
            SpellFx.Rate(mist, 7f);
            mist.Play();

            splash = SpellFx.NewPS(transform, "Splash", SpellFx.BlendMat(SpellFx.TexSoft), 120, true);
            var spm = splash.main;
            spm.gravityModifier = 0.75f;
            spm.startLifetime = 1f;
            SpellFx.FadeOut(splash);
            SpellFx.SizeCurve(splash, SpellFx.Grow);
            splash.Play();

            var sparkle = SpellFx.NewPS(transform, "SeaSparkle", SpellFx.AddMat(SpellFx.TexStar), 60, true);
            sparkle.transform.SetPositionAndRotation(new Vector3(center.x, waterY + 0.04f, center.z), Quaternion.Euler(90f, 0f, 0f));
            SpellFx.Disc(sparkle, SeaRadius - 8f, Mathf.Clamp01(1f - (areaR + 3f) / (SeaRadius - 8f)));
            var skm = sparkle.main;
            skm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            skm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            skm.startRotation = new ParticleSystem.MinMaxCurve(0f, 0.8f);
            skm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.85f, 0.9f), new Color(0.8f, 0.95f, 1f, 0.8f));
            SpellFx.SizeCurve(sparkle, SpellFx.PopCurve);
            SpellFx.Rate(sparkle, 30f);
            sparkle.Play();

            buoyFlash = SpellFx.NewPS(transform, "BuoyFlash", SpellFx.AddMat(SpellFx.TexSoft), 4, true);
            buoyFlash.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.3f;
            SpellFx.SizeCurve(buoyFlash, SpellFx.PopCurve);
            buoyFlash.Play();
        }

        void Update()
        {
            if(Variant!=0)return;
            float dt = Time.deltaTime;
            clock += dt;
            // 兩層海面往不同方向流動；反光微微明暗
            if (waterMat) waterMat.mainTextureOffset = new Vector2(clock * 0.018f, clock * 0.011f);
            if (glintMat)
            {
                glintMat.mainTextureOffset = new Vector2(-clock * 0.013f, clock * 0.021f);
                float k = 0.42f + 0.08f * Mathf.Sin(clock * 0.9f) + 0.04f * Mathf.Sin(clock * 2.7f);
                glintMat.SetColor("_TintColor", new Color(k, k, k, 0.5f));
            }
            // 浪花：沿岸慢慢漂移＋一推一退（V 方向位移）＋濃淡脈動，兩層錯開
            if (foamMatA)
            {
                float w = Mathf.Sin(clock * 0.85f);
                foamMatA.mainTextureOffset = new Vector2(clock * 0.006f, 0.07f * w);
                foamMatA.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.42f + 0.08f * Mathf.Max(0f, w)));
            }
            if (foamMatB)
            {
                float w = Mathf.Sin(clock * 0.85f + 2.2f);
                foamMatB.mainTextureOffset = new Vector2(-clock * 0.009f + 0.37f, 0.09f * w - 0.02f);
                foamMatB.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.22f + 0.12f * Mathf.Max(0f, w)));
            }
            // 海面頂點緩緩起伏（只有離礁石遠的地方動）
            if (waterMesh != null)
            {
                for (int i = 0; i < waterBase.Length; i++)
                {
                    float a = waterAmp[i];
                    if (a <= 0f) continue;
                    var p = waterBase[i];
                    waterVerts[i].y = a * (0.6f * Mathf.Sin(p.x * 0.55f + p.z * 0.2f + clock * 1.1f) + 0.4f * Mathf.Sin(p.z * 0.7f - p.x * 0.3f - clock * 0.8f));
                }
                waterMesh.vertices = waterVerts;
            }
            // 浮標與桅杆隨波搖晃（用相對場景根物件的位置：衝刺平移世界時才會跟著走）
            if (buoy)
            {
                buoy.localPosition = buoyPos + Vector3.up * (0.05f * Mathf.Sin(clock * 1.3f));
                buoy.rotation = buoyRot * Quaternion.Euler(6f * Mathf.Sin(clock * 1.1f), 0f, 5f * Mathf.Sin(clock * 0.9f + 1f));
            }
            if (mast)
            {
                mast.localPosition = mastPos + Vector3.up * (0.03f * Mathf.Sin(clock * 0.7f + 2f));
                mast.rotation = mastRot * Quaternion.Euler(1.2f * Mathf.Sin(clock * 0.6f), 0f, 1f * Mathf.Sin(clock * 0.5f + 0.7f));
            }
            // 浮標燈：每 2 秒閃一下
            flashTimer -= dt;
            if (lampMat)
            {
                float on = Mathf.Clamp01(1f - (2.2f - flashTimer) * 2.5f);
                lampMat.SetColor("_TintColor", new Color(0.5f, 0.3f + 0.2f * on, 0.15f + 0.3f * on, 0.5f));
            }
            if (flashTimer <= 0f)
            {
                flashTimer = 2.2f;
                if (buoyFlash && buoy)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = buoy.TransformPoint(new Vector3(0f, 1.23f, 0f)), startColor = new Color(1f, 0.85f, 0.5f, 0.9f),
                        startSize = 0.9f, startLifetime = 0.6f, applyShapeToPosition = false
                    };
                    buoyFlash.Emit(ep, 1);
                }
            }
            // 浪花不時打上外圈的岩石
            splashTimer -= dt;
            if (splash && splashSpots.Count > 0 && splashTimer <= 0f)
            {
                splashTimer = Random.Range(0.5f, 1.3f);
                var at = splashSpots[Random.Range(0, splashSpots.Count)];
                var outDir = new Vector3(at.x - center.x, 0f, at.z - center.z).normalized;
                int n = Random.Range(9, 15);
                for (int i = 0; i < n; i++)
                {
                    var side = Random.insideUnitCircle * 0.6f;
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = at + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f)),
                        velocity = new Vector3(side.x, Random.Range(1.2f, 2.4f), side.y) + outDir * Random.Range(-0.6f, 0.1f),
                        startColor = new Color(0.95f, 0.98f, 1f, Random.Range(0.55f, 0.85f)),
                        startSize = Random.Range(0.05f, 0.12f), startLifetime = Random.Range(0.7f, 1.1f), applyShapeToPosition = false
                    };
                    splash.Emit(ep, 1);
                }
                // 兩團較大的水霧
                for (int i = 0; i < 2; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = at + Vector3.up * 0.15f, velocity = new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, Random.Range(-0.2f, 0.2f)),
                        startColor = new Color(0.95f, 0.98f, 1f, 0.3f), startSize = Random.Range(0.35f, 0.55f), startLifetime = 1.2f, applyShapeToPosition = false
                    };
                    splash.Emit(ep, 1);
                }
            }
            // 海鷗盤旋＋偶爾拍翅
            for (int i = 0; i < gulls.Count; i++)
            {
                var g = gulls[i];
                g.ang += g.speed * dt;
                float sgn = Mathf.Sign(g.speed);
                var pos = center + new Vector3(Mathf.Cos(g.ang) * g.r, g.h + 0.5f * Mathf.Sin(clock * 0.3f + g.phase), Mathf.Sin(g.ang) * g.r);
                var fwd = new Vector3(-Mathf.Sin(g.ang), 0f, Mathf.Cos(g.ang)) * sgn;
                g.t.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd) * Quaternion.Euler(0f, 0f, 14f * sgn));
                float flap = SS(0.2f, 0.7f, Mathf.Sin(clock * 0.35f + g.phase));
                float ph = clock * 7f + g.phase;
                float e = 0.06f + 0.16f * Mathf.Sin(ph) * flap;
                float tip = e * 1.5f + 0.06f + 0.14f * Mathf.Sin(ph - 0.9f) * flap;
                SetGullWings(g.v, e, tip);
                g.mesh.vertices = g.v;
            }
        }

        // ================================================================ 礁石輪廓
        /// <summary>輪廓上的一點：角度、方向、到場地邊界的距離（已含一點安全距離）、沿岸的 u 座標</summary>
        struct RingPt { public float ang, R, u; public Vector2 dir; }

        static Vector3 RingPos(RingPt s, float d) => new Vector3(s.dir.x * (s.R + d), 0f, s.dir.y * (s.R + d));

        /// <summary>
        /// 從中心往各方向射線，取與場地多邊形的「最遠」交點距離（射線上更遠處一定在場地外）。
        /// 角度取 128 等分＋每個多邊形頂點的角度（凸多邊形時輪廓剛好貼著邊界）。
        /// </summary>
        static List<RingPt> EdgeRing(List<Vector3> poly, Vector3 c, float areaR)
        {
            var angs = new List<float>();
            for (int i = 0; i < 128; i++) angs.Add(i / 128f * Mathf.PI * 2f);
            if (poly != null) foreach (var p in poly) { float a = Mathf.Atan2(p.z - c.z, p.x - c.x); if (a < 0f) a += Mathf.PI * 2f; angs.Add(a); }
            angs.Sort();
            var ring = new List<RingPt>();
            float last = -1f;
            foreach (var a in angs)
            {
                if (a - last < 0.004f) continue;
                last = a;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float R = poly != null ? RayPolyMax(poly, new Vector2(c.x, c.z), d) : areaR;
                if (R <= 0.01f) R = areaR;
                ring.Add(new RingPt { ang = a, R = R + 0.03f, dir = d });
            }
            if (ring.Count > 1 && ring[ring.Count - 1].ang - ring[0].ang > Mathf.PI * 2f - 0.004f) ring.RemoveAt(ring.Count - 1);
            // 沿岸 u：以外推 1m 的弧長計，總長取整數個 2.5m 的 tile，接縫才不會斷
            int n = ring.Count; float total = 0f; var cum = new float[n + 1];
            for (int i = 0; i < n; i++) { total += (RingPos(ring[(i + 1) % n], 1f) - RingPos(ring[i], 1f)).magnitude; cum[i + 1] = total; }
            float tiles = Mathf.Max(1f, Mathf.Round(total / 2.5f));
            for (int i = 0; i < n; i++) { var s = ring[i]; s.u = cum[i] / Mathf.Max(0.001f, total) * tiles; ring[i] = s; }
            return ring;
        }

        static float Cross2(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float RayPolyMax(List<Vector3> poly, Vector2 o, Vector2 d)
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

        static bool PointInPoly(List<Vector3> poly, Vector2 p)
        {
            bool inside = false; int n = poly.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = poly[i].x, zi = poly[i].z, xj = poly[j].x, zj = poly[j].z;
                if ((zi > p.y) != (zj > p.y) && p.x < (xj - xi) * (p.y - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        static float DistToPoly(List<Vector3> poly, Vector2 p)
        {
            float best = 1e9f; int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var a = new Vector2(poly[i].x, poly[i].z); var b = new Vector2(poly[(i + 1) % n].x, poly[(i + 1) % n].z);
                var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        // 岩坡的截面：離場地邊界的距離 → 高度（相對地板）
        static readonly float[] SlopeD = { 0f, 0.25f, 0.6f, 1.0f, 1.45f, 2.0f };
        static readonly float[] SlopeH = { -0.006f, -0.035f, -0.1f, -0.21f, -0.36f, -0.62f };

        static float SlopeHeight(float d, float floorY, float waterY)
        {
            for (int i = 0; i < SlopeD.Length - 1; i++)
                if (d <= SlopeD[i + 1]) return floorY + Mathf.Lerp(SlopeH[i], SlopeH[i + 1], (d - SlopeD[i]) / (SlopeD[i + 1] - SlopeD[i]));
            return floorY + SlopeH[SlopeH.Length - 1];
        }

        /// <summary>礁石邊緣的岩坡：沿輪廓一圈，從場地邊界往外斜斜沒入海裡（世界座標）；外側幾圈加一點凹凸</summary>
        Mesh SlopeMesh(List<RingPt> ring, float floorY, float waterY)
        {
            int n = ring.Count, rows = SlopeD.Length, row = n + 1;
            var v = new Vector3[row * rows]; var uv = new Vector2[v.Length];
            var tri = new int[n * (rows - 1) * 6];
            float uEnd = LastStep(ring);
            for (int j = 0; j < rows; j++)
                for (int i = 0; i <= n; i++)
                {
                    int ii = i % n; var s = ring[ii];
                    float jr = j == 0 ? 0f : (Hash(ii, j, 61) - 0.5f) * 0.22f * Mathf.Min(1f, j * 0.6f);
                    float jh = j == 0 ? 0f : (Hash(ii, j, 63) - 0.5f) * 0.06f;
                    var p = RingPos(s, SlopeD[j] + jr);
                    v[j * row + i] = new Vector3(center.x + p.x, floorY + SlopeH[j] + jh, center.z + p.z);
                    uv[j * row + i] = new Vector2(i == n ? uEnd : s.u, j / (float)(rows - 1));
                }
            int k = 0;
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                    tri[k++] = a; tri[k++] = b; tri[k++] = c;
                    tri[k++] = b; tri[k++] = d; tri[k++] = c;
                }
            var m = new Mesh { name = "SeaSlope", vertices = v, uv = uv, triangles = tri };
            FixWinding(m, center);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>接縫頂點的 u：最後一點的 u 加上最後一段（剛好等於總 tile 數）</summary>
        static float LastStep(List<RingPt> ring)
        {
            int n = ring.Count;
            float total = 0f; for (int i = 0; i < n; i++) total += (RingPos(ring[(i + 1) % n], 1f) - RingPos(ring[i], 1f)).magnitude;
            return Mathf.Max(1f, Mathf.Round(total / 2.5f));
        }

        /// <summary>確保環狀網格的三角形朝上（從上方看是正面）；角度方向不同時整體翻面</summary>
        static void FixWinding(Mesh m, Vector3 c)
        {
            var v = m.vertices; var t = m.triangles;
            if (t.Length < 3) return;
            var n = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            if (n.y >= 0f) return;
            for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
            m.triangles = t;
        }

        /// <summary>浪花帶：海面上一圈，從岩坡入水處往外約 2.3m；v＝0（岩石側）～1（外海側），外緣頂點透明</summary>
        Mesh FoamMesh(List<RingPt> ring, float y)
        {
            float[] ds = { 0.7f, 0.95f, 1.3f, 1.75f, 2.3f, 3.0f };
            int n = ring.Count, rows = ds.Length, row = n + 1;
            var v = new Vector3[row * rows]; var uv = new Vector2[v.Length]; var col = new Color[v.Length];
            var tri = new int[n * (rows - 1) * 6];
            float uEnd = LastStep(ring);
            for (int j = 0; j < rows; j++)
                for (int i = 0; i <= n; i++)
                {
                    var s = ring[i % n];
                    var p = RingPos(s, ds[j]);
                    v[j * row + i] = new Vector3(center.x + p.x, y, center.z + p.z);
                    uv[j * row + i] = new Vector2(i == n ? uEnd : s.u, (ds[j] - ds[0]) / (ds[rows - 1] - ds[0]));
                    col[j * row + i] = new Color(1f, 1f, 1f, j == rows - 1 ? 0f : 1f);
                }
            int k = 0;
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                    tri[k++] = a; tri[k++] = b; tri[k++] = c;
                    tri[k++] = b; tri[k++] = d; tri[k++] = c;
                }
            var m = new Mesh { name = "SeaFoam", vertices = v, uv = uv, colors = col, triangles = tri };
            FixWinding(m, center);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// 海面：內圈貼著礁石輪廓（藏在岩坡底下），往外漸變成 35m 的正圓；UV＝平面公尺座標（相對中心）。
        /// 最外兩圈頂點色漸透明，跟背景的海平面接起來。amp＝每個頂點起伏的幅度（礁石附近不動）。
        /// </summary>
        static Mesh WaterMesh(List<RingPt> ring, out Vector3[] verts, out float[] amp, float areaR)
        {
            float[] ts = { 0f, 0.02f, 0.05f, 0.1f, 0.17f, 0.26f, 0.37f, 0.5f, 0.65f, 0.82f, 1f };
            int n = ring.Count, rows = ts.Length, row = n + 1;
            verts = new Vector3[row * rows]; amp = new float[verts.Length];
            var uv = new Vector2[verts.Length]; var col = new Color[verts.Length];
            var tri = new int[n * (rows - 1) * 6];
            for (int j = 0; j < rows; j++)
                for (int i = 0; i <= n; i++)
                {
                    var s = ring[i % n];
                    float r = Mathf.Lerp(s.R + 0.55f, SeaRadius, ts[j]);
                    var p = new Vector3(s.dir.x * r, 0f, s.dir.y * r);
                    int k = j * row + i;
                    verts[k] = p; uv[k] = new Vector2(p.x, p.z);
                    amp[k] = 0.07f * SS(areaR + 2.5f, areaR + 8f, r);
                    col[k] = new Color(1f, 1f, 1f, j == rows - 1 ? 0f : (j == rows - 2 ? 0.75f : 1f));
                }
            int t = 0;
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < n; i++)
                {
                    int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                    tri[t++] = a; tri[t++] = b; tri[t++] = c;
                    tri[t++] = b; tri[t++] = d; tri[t++] = c;
                }
            var m = new Mesh { name = "SeaWater", vertices = verts, uv = uv, colors = col, triangles = tri };
            FixWinding(m, Vector3.zero);
            m.MarkDynamic();
            m.RecalculateBounds();
            return m;
        }

        // ================================================================ 小工具：材質與網格
        static Material NewMat(Shader sh, Texture2D tex, int queue, Color tint)
        {
            var m = new Material(sh) { mainTexture = tex, renderQueue = queue, name = "Sea " + tex.name };
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

        GameObject MeshObjDiffuse(string name, Mesh mesh, Vector3 pos, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>場地多邊形（由中心扇形三角化）；UV＝地板座標 ÷ tile 公尺</summary>
        static Mesh PolyMesh(List<Vector3> poly, Vector3 center, float tile)
        {
            int n = poly.Count;
            var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var tri = new int[n * 3];
            v[0] = center; uv[0] = new Vector2(center.x, center.z) / tile;
            for (int i = 0; i < n; i++) { v[i + 1] = new Vector3(poly[i].x, center.y, poly[i].z); uv[i + 1] = new Vector2(poly[i].x, poly[i].z) / tile; }
            for (int i = 0; i < n; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % n; tri[i * 3 + 2] = 1 + i; }
            var m = new Mesh { name = "SeaReefTop", vertices = v, uv = uv, triangles = tri };
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
            var m = new Mesh { name = "SeaDisc", vertices = v, uv = uv, triangles = tri };
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
            var m = new Mesh { name = "SeaBackdrop", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>海鷗：身體＋兩片翅膀（肩、肘、翼尖）；頂點色＝白身體、灰翅膀、黑翼尖。粒子材質雙面顯示。</summary>
        static Mesh GullMesh(out Vector3[] v)
        {
            v = new Vector3[10];
            // 0 頭、1 尾、2 左肩前、3 左肩後、4 左肘、5 左翼尖、6 右肩前、7 右肩後、8 右肘、9 右翼尖
            v[0] = new Vector3(0f, 0.01f, 0.2f); v[1] = new Vector3(0f, 0f, -0.2f);
            SetGullWings(v, 0.06f, 0.15f);
            var white = new Color(0.97f, 0.97f, 0.97f, 1f); var gray = new Color(0.72f, 0.75f, 0.8f, 1f); var dark = new Color(0.18f, 0.18f, 0.2f, 1f);
            var col = new[] { white, white, white, white, gray, dark, white, white, gray, dark };
            var tri = new[] { 0, 6, 1, 0, 1, 2, 2, 4, 3, 4, 5, 3, 6, 7, 8, 8, 7, 9 };
            var m = new Mesh { name = "SeaGull", vertices = v, colors = col, triangles = tri, uv = new Vector2[10] };
            m.MarkDynamic();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1.6f);
            return m;
        }

        static void SetGullWings(Vector3[] v, float elbowY, float tipY)
        {
            v[2] = new Vector3(-0.05f, 0f, 0.07f); v[3] = new Vector3(-0.05f, 0f, -0.06f);
            v[4] = new Vector3(-0.3f, elbowY, 0.04f); v[5] = new Vector3(-0.62f, tipY, -0.12f);
            v[6] = new Vector3(0.05f, 0f, 0.07f); v[7] = new Vector3(0.05f, 0f, -0.06f);
            v[8] = new Vector3(0.3f, elbowY, 0.04f); v[9] = new Vector3(0.62f, tipY, -0.12f);
        }

        /// <summary>簡單的網格組合器（多個子網格、頂點色）</summary>
        class MB
        {
            public readonly List<Vector3> V = new List<Vector3>(), N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int>[] T;
            public MB(int subs) { T = new List<int>[subs]; for (int i = 0; i < subs; i++) T[i] = new List<int>(); }
            public int Add(Vector3 p, Vector3 n, Vector2 uv, Color c) { V.Add(p); N.Add(n); UV.Add(uv); C.Add(c); return V.Count - 1; }
            public void Tri(int s, int a, int b, int c) { T[s].Add(a); T[s].Add(b); T[s].Add(c); }
            /// <summary>平面著色的三角形（各自的頂點＋面法線）；UV 依法線方向取平面投影</summary>
            public void Facet(int s, Vector3 a, Vector3 b, Vector3 c)
            {
                var n = Vector3.Cross(b - a, c - a).normalized;
                bool top = Mathf.Abs(n.y) > 0.7f;
                Vector2 P(Vector3 p) => top ? new Vector2(p.x, p.z) * 0.8f : new Vector2((p.x + p.z) * 0.8f, p.y * 0.8f);
                int i0 = Add(a, n, P(a), Color.white), i1 = Add(b, n, P(b), Color.white), i2 = Add(c, n, P(c), Color.white);
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
        /// 一圈圈截面（由下往上，每圈 lon 點、逆時針）組成的平面著色多面體，可加底尖／頂尖。
        /// 三角形順序與 VolcanoScene.Spire 相同（從外面看是順時針＝正面）。
        /// </summary>
        static void Facets(MB mb, int sub, List<Vector3[]> rings, Vector3? bottom, Vector3? top)
        {
            int lon = rings[0].Length;
            for (int r = 0; r < rings.Count - 1; r++)
                for (int s = 0; s < lon; s++)
                {
                    int s1 = (s + 1) % lon;
                    Vector3 a = rings[r][s], b = rings[r][s1], c = rings[r + 1][s], d = rings[r + 1][s1];
                    mb.Facet(sub, a, c, b); mb.Facet(sub, b, c, d);
                }
            if (top.HasValue) { var last = rings[rings.Count - 1]; for (int s = 0; s < lon; s++) mb.Facet(sub, last[s], top.Value, last[(s + 1) % lon]); }
            if (bottom.HasValue) { var first = rings[0]; for (int s = 0; s < lon; s++) mb.Facet(sub, first[s], first[(s + 1) % lon], bottom.Value); }
        }

        /// <summary>鋸齒狀的岩塊：隨機凹凸的橢球，頂部稍微削平（被海浪磨過），平面著色</summary>
        static void FacetRock(MB mb, int sub, Vector3 c, Vector3 rad, int lon, int lat, int seed, float jag, float rotY)
        {
            var rings = new List<Vector3[]>();
            float cr = Mathf.Cos(rotY), sr = Mathf.Sin(rotY);
            Vector3 Place(Vector3 p)
            {
                if (p.y > rad.y * 0.55f) p.y = rad.y * 0.55f + (p.y - rad.y * 0.55f) * 0.35f;   // 削平的頂
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
            Facets(mb, sub, rings, bottom, top);
        }

        /// <summary>海蝕柱：底部寬、往上略收、頂部平的高岩柱（每圈都有隨機凹凸與偏移），平面著色</summary>
        static void SeaStack(MB mb, int sub, Vector3 c, float w, float h, int seed)
        {
            const int lon = 8, rings = 6;
            var list = new List<Vector3[]>();
            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float rad = w * (1.15f - 0.4f * t) * (0.85f + 0.25f * Hash(r, 0, seed));
                if (r == 0) rad *= 1.25f;
                float ox = (Hash(r, 1, seed) - 0.5f) * w * 0.3f * t, oz = (Hash(r, 2, seed) - 0.5f) * w * 0.3f * t;
                var row = new Vector3[lon];
                for (int s = 0; s < lon; s++)
                {
                    float a = (s + (Hash(s, r, seed + 5) - 0.5f) * 0.5f) / lon * Mathf.PI * 2f;
                    float k = 0.8f + 0.4f * Hash(s, r, seed + 9);
                    row[s] = c + new Vector3(ox + Mathf.Cos(a) * rad * k, t * h + (Hash(s, r, seed + 11) - 0.5f) * 0.25f * (r == 0 ? 0f : 1f), oz + Mathf.Sin(a) * rad * k);
                }
                list.Add(row);
            }
            Facets(mb, sub, list, null, c + new Vector3((Hash(9, 9, seed) - 0.5f) * w * 0.3f, h * 1.04f, 0f));
        }

        /// <summary>旋轉體（沿本地 +Y 的一圈圈截面），可加頂蓋／底蓋；u 沿圓周、v＝高度</summary>
        static void Lathe(MB mb, int sub, Vector3 pos, Quaternion rot, float[] hs, float[] rs, int sides, bool capTop, bool capBottom, Color col)
        {
            var cols = new Color[hs.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = col;
            LatheCore(mb, sub, pos, rot, hs, rs, cols, sides, capTop, capBottom, null);
        }

        static void LatheCore(MB mb, int sub, Vector3 pos, Quaternion rot, float[] hs, float[] rs, Color[] cols, int sides, bool capTop, bool capBottom, Vector3[] bend)
        {
            int rings = hs.Length, start = mb.V.Count;
            for (int r = 0; r < rings; r++)
            {
                float dr = rs[Mathf.Max(0, r - 1)] - rs[Mathf.Min(rings - 1, r + 1)];
                float dh = Mathf.Max(0.001f, hs[Mathf.Min(rings - 1, r + 1)] - hs[Mathf.Max(0, r - 1)]);
                var off = bend != null ? bend[r] : Vector3.zero;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    var p = off + new Vector3(ca * rs[r], hs[r], sa * rs[r]);
                    var n = new Vector3(ca, dr / dh, sa).normalized;
                    mb.Add(pos + rot * p, rot * n, new Vector2(s / (float)sides, hs[r] / 2f), cols[r]);
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
                var off = bend != null ? bend[rings - 1] : Vector3.zero;
                int top = mb.Add(pos + rot * (off + Vector3.up * hs[rings - 1]), rot * Vector3.up, new Vector2(0.5f, 0.5f), cols[rings - 1]);
                int last = start + (rings - 1) * row;
                for (int s = 0; s < sides; s++) mb.Tri(sub, last + s, top, last + s + 1);
            }
            if (capBottom)
            {
                var off = bend != null ? bend[0] : Vector3.zero;
                int bc = mb.Add(pos + rot * (off + Vector3.up * hs[0]), rot * Vector3.down, new Vector2(0.5f, 0.5f), cols[0]);
                for (int s = 0; s < sides; s++) mb.Tri(sub, start + s + 1, bc, start + s);
            }
        }

        // ================================================================ 程式產生的貼圖
        /// <summary>GLSL 的 smoothstep(e0, e1, x)（Mathf.SmoothStep 的參數意義不同）</summary>
        static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        static float Hash(int x, int y, int s) { unchecked { int h = x * 374761393 + y * 668265263 + s * 982451653; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffff) / 65535f; } }
        static int Wrap(int i, int n) => ((i % n) + n) % n;
        static float ValueNoise(float x, float y, int s, int px, int py)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = Wrap(xi, px), y0 = Wrap(yi, py), x1 = (x0 + 1) % px, y1 = (y0 + 1) % py;
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, s), Hash(x1, y0, s), fx), Mathf.Lerp(Hash(x0, y1, s), Hash(x1, y1, s), fx), fy);
        }
        /// <summary>可無縫平鋪的分形雜訊（0～1）</summary>
        static float Fbm(float u, float v, int s, int baseP, int oct)
        {
            float sum = 0f, amp = 0.5f, norm = 0f; int p = baseP;
            for (int o = 0; o < oct; o++) { sum += amp * ValueNoise(u * p, v * p, s + o * 17, p, p); norm += amp; amp *= 0.5f; p *= 2; }
            return sum / norm;
        }
        /// <summary>只在 u 方向平鋪的分形雜訊（v 方向用任意頻率，給背景、岩坡這種上下不平鋪的貼圖）</summary>
        static float Fbm2(float u, float v, int s, int pu, float fv, int oct)
        {
            float sum = 0f, amp = 0.5f, norm = 0f; int p = pu; float f = fv;
            for (int o = 0; o < oct; o++) { sum += amp * ValueNoise(u * p, v * f, s + o * 17, p, 1 << 20); norm += amp; amp *= 0.5f; p *= 2; f *= 2f; }
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
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    float px = cx + 0.15f + 0.7f * Hash(wx, wy, s), py = cy + 0.15f + 0.7f * Hash(wx, wy, s + 5);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                }
            return d2 - d1;
        }

        /// <summary>可平鋪的藤壺：一叢一叢的小圓錐殼（外圈亮、中間開口暗、左上受光），回傳覆蓋度、亮度與周圍的影子</summary>
        static float Barnacles(float u, float v, int cells, int seed, float density, out float lum, out float shadow)
        {
            float x = u * cells, y = v * cells; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 0f; lum = 0f; shadow = 0f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    float cluster = ValueNoise(wx * 5f / cells, wy * 5f / cells, seed + 50, 5, 5);
                    if (Hash(wx, wy, seed) > density * SS(0.5f, 0.75f, cluster)) continue;
                    float px = cx + 0.5f + 0.4f * (Hash(wx, wy, seed + 1) - 0.5f), py = cy + 0.5f + 0.4f * (Hash(wx, wy, seed + 2) - 0.5f);
                    float R = 0.3f + 0.2f * Hash(wx, wy, seed + 3);
                    float dx = x - px, dy = y - py, d = Mathf.Sqrt(dx * dx + dy * dy) / R;
                    if (d < 1.35f) shadow = Mathf.Max(shadow, 1f - SS(0.9f, 1.35f, Mathf.Sqrt((dx - 0.08f) * (dx - 0.08f) + (dy + 0.08f) * (dy + 0.08f)) / R));
                    if (d >= 1f) continue;
                    float cov = Mathf.Clamp01((1f - d) / 0.15f);
                    if (cov <= best) continue;
                    best = cov;
                    float th = Mathf.Atan2(dy, dx);
                    float light = 1f + 0.45f * ((-dx * 0.7f + dy * 0.7f) / R) * (d > 0.35f ? 1f : -1f);   // 錐面受光；開口內側相反
                    float plates = 0.82f + 0.18f * Mathf.Cos(6f * th + Hash(wx, wy, seed + 4) * 6.28f);
                    lum = d < 0.3f ? 0.14f + 0.2f * d : (0.62f + 0.45f * (1f - d)) * light * plates;
                }
            return best;
        }

        /// <summary>可平鋪的海藻：一個固著點長出 2～4 條彎彎的帶狀葉片，回傳覆蓋度與「葉脈」（中央亮）</summary>
        static float Seaweed(float u, float v, int cells, int seed, float density, out float rib, out float kind)
        {
            float x = u * cells, y = v * cells; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 0f; rib = 0f; kind = 0f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    if (Hash(wx, wy, seed) > density) continue;
                    float px = cx + Hash(wx, wy, seed + 1), py = cy + Hash(wx, wy, seed + 2);
                    float baseAng = Hash(wx, wy, seed + 3) * 6.28f;
                    int fronds = 2 + Mathf.FloorToInt(Hash(wx, wy, seed + 4) * 3f);
                    for (int f = 0; f < fronds; f++)
                    {
                        float ang = baseAng + (f - fronds * 0.5f) * 0.45f, L = 0.55f + 0.4f * Hash(wx + f, wy, seed + 5);
                        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                        float dx = x - px, dy = y - py;
                        float a = dx * ca + dy * sa, b = -dx * sa + dy * ca, t = a / L;
                        if (t < 0f || t > 1f) continue;
                        b -= 0.09f * Mathf.Sin(t * 7f + f * 2f + baseAng) * t;   // 波浪狀擺動
                        float w = 0.055f * (0.35f + Mathf.Sin(Mathf.Min(1f, t * 1.15f) * Mathf.PI) * 0.9f);
                        float cov = Mathf.Clamp01((w - Mathf.Abs(b)) / 0.012f);
                        if (cov > best) { best = cov; rib = 1f - Mathf.Abs(b) / Mathf.Max(0.001f, w); kind = Hash(wx, wy, seed + 6); }
                    }
                }
            return best;
        }

        static readonly Color[] ShellPalette =
        {
            new Color(0.88f, 0.8f, 0.66f), new Color(0.86f, 0.58f, 0.52f), new Color(0.88f, 0.64f, 0.38f), new Color(0.9f, 0.88f, 0.82f), new Color(0.6f, 0.52f, 0.62f)
        };

        /// <summary>可平鋪的貝殼（扇貝：放射狀的肋紋）＋偶爾一隻海星，回傳覆蓋度與顏色</summary>
        static float Shells(float u, float v, int cells, int seed, float density, out Color col)
        {
            float x = u * cells, y = v * cells; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 0f; col = Color.black;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    if (Hash(wx, wy, seed) > density) continue;
                    float px = cx + 0.2f + 0.6f * Hash(wx, wy, seed + 1), py = cy + 0.2f + 0.6f * Hash(wx, wy, seed + 2);
                    float ang = Hash(wx, wy, seed + 3) * 6.28f, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                    float dx = x - px, dy = y - py, a = dx * ca + dy * sa, b = -dx * sa + dy * ca;
                    bool star = Hash(wx, wy, seed + 4) < 0.05f;
                    if (star)
                    {
                        // 海星：五隻手臂
                        float R = 0.32f, r = Mathf.Sqrt(a * a + b * b) / R, th = Mathf.Atan2(b, a);
                        float arm = 0.28f + 0.72f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * th)), 2.2f);
                        float cov = Mathf.Clamp01((arm - r) / 0.08f);
                        if (cov > best)
                        {
                            best = cov;
                            float dots = 0.85f + 0.15f * Mathf.Sin(r * 30f) * Mathf.Sin(th * 15f);
                            col = Color.Lerp(new Color(0.85f, 0.32f, 0.16f), new Color(0.95f, 0.55f, 0.3f), r) * dots;
                        }
                    }
                    else
                    {
                        // 扇貝：圓形、底部收成尖的鉸合處，放射肋紋
                        float R = 0.17f + 0.1f * Hash(wx, wy, seed + 5);
                        float hy = b + R * 0.55f;   // 以鉸合處為原點
                        float r = Mathf.Sqrt(a * a + (b * b)) / R, th = Mathf.Atan2(a, hy);
                        float fan = Mathf.Clamp01((1.05f - Mathf.Abs(th)) / 0.08f);
                        float cov = Mathf.Clamp01((1f - r) / 0.12f) * (hy > 0f ? fan : 0f);
                        if (cov > best)
                        {
                            best = cov;
                            var c = ShellPalette[Mathf.FloorToInt(Hash(wx, wy, seed + 6) * ShellPalette.Length) % ShellPalette.Length];
                            float ribs = 0.8f + 0.2f * Mathf.Cos(th * 18f);
                            float growth = 0.92f + 0.08f * Mathf.Sin(r * 22f);
                            float light = 1f + 0.25f * (-a * 0.7f + b * 0.7f) / R;
                            col = c * (ribs * growth * light);
                        }
                    }
                }
            return best;
        }

        static Texture2D Finish(Texture2D t, Color32[] px) { t.SetPixels32(px); t.Apply(true, true); return t; }

        static Texture2D NewTex(string name, int w, int h, bool repeatU, bool repeatV)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Bilinear, anisoLevel = 2 };
            t.wrapModeU = repeatU ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            t.wrapModeV = repeatV ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return t;
        }

        static Texture2D WhiteTex()
        {
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            return Finish(NewTex("White", 4, 4, false, false), px);
        }

        // ---------------------------------------------------------------- 場地內：礁石頂面
        static Texture2D ReefFloorTex() { const int S = 256; return Finish(NewTex("ReefTop", S, S, true, true), ReefFloorPixels()); }

        public static Color32[] ReefFloorPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    // 濕的深色岩石：大塊明暗＋細顆粒，一格格被裂縫切開
                    float n = Fbm(u, v, 3, 4, 5), grain = Fbm(u, v, 5, 32, 2);
                    float e = VoronoiEdge(u + 0.05f * (Fbm(u, v, 7, 4, 2) - 0.5f), v, 5, 9);
                    var c = Color.Lerp(new Color(0.17f, 0.16f, 0.15f), new Color(0.12f, 0.145f, 0.16f), Fbm(u, v, 11, 3, 3));
                    c *= (0.6f + 0.8f * n) * (0.85f + 0.3f * grain) * (0.85f + 0.25f * SS(0f, 0.3f, e));
                    // 濕亮的反光細點
                    float wet = SS(0.6f, 0.8f, Fbm(u, v, 15, 32, 2));
                    c += new Color(0.06f, 0.08f, 0.09f) * wet * SS(0.03f, 0.08f, e);
                    // 綠色藻類薄膜
                    float alg = SS(0.54f, 0.68f, Fbm(u, v, 19, 3, 4) + (grain - 0.5f) * 0.12f);
                    c = Color.Lerp(c, new Color(0.09f, 0.18f, 0.07f) * (0.7f + 0.6f * grain), alg * 0.75f);
                    // 裂縫：積著海水（深青色），邊上一點白色泡沫
                    float crack = 1f - SS(0.012f, 0.055f, e);
                    c = Color.Lerp(c, new Color(0.03f, 0.08f, 0.09f), crack * 0.9f);
                    float foam = (1f - SS(0.0f, 0.075f, e)) * SS(0.6f, 0.75f, Fbm(u, v, 17, 32, 2)) * SS(0.42f, 0.6f, Fbm(u, v, 23, 4, 2));
                    c = Color.Lerp(c, new Color(0.85f, 0.9f, 0.9f), foam * 0.85f);

                    // 潮池：映著藍天白雲，池底看得到一點深色岩石；池邊一圈更濕更暗，水面邊緣一條亮線
                    float pn = Fbm(u + 0.07f * (Fbm(u, v, 27, 4, 2) - 0.5f), v, 25, 3, 4);
                    float pool = SS(0.605f, 0.625f, pn);
                    float rim = SS(0.56f, 0.605f, pn) * (1f - pool);
                    c *= 1f - 0.4f * rim;
                    if (pool > 0f)
                    {
                        float depth = SS(0.61f, 0.72f, pn);
                        var bottom = Color.Lerp(new Color(0.12f, 0.27f, 0.26f), new Color(0.04f, 0.13f, 0.16f), depth);
                        float cloud = SS(0.45f, 0.75f, Fbm(u, v, 29, 6, 3));
                        var refl = Color.Lerp(new Color(0.36f, 0.58f, 0.78f), new Color(0.82f, 0.9f, 0.94f), cloud);
                        var pc = Color.Lerp(bottom, refl, 0.4f + 0.25f * depth);
                        pc += new Color(0.35f, 0.38f, 0.38f) * (1f - SS(0.625f, 0.645f, pn)) * pool;   // 水邊亮線
                        c = Color.Lerp(c, pc, pool);
                    }

                    // 藤壺（池子裡沒有）
                    float bar = Barnacles(u, v, 26, 401, 0.85f, out float bl, out float bsh) * (1f - pool);
                    c *= 1f - 0.45f * bsh * (1f - pool) * (1f - bar);
                    c = Color.Lerp(c, new Color(0.86f, 0.83f, 0.74f) * bl, bar);

                    // 海藻（可以垂進池子裡）
                    float sw = Seaweed(u, v, 4, 501, 0.42f, out float rib, out float kind);
                    float sws = Seaweed(u - 0.008f, v + 0.008f, 4, 501, 0.42f, out _, out _);
                    c *= 1f - 0.4f * sws * (1f - sw);
                    var swc = kind < 0.5f ? Color.Lerp(new Color(0.16f, 0.17f, 0.05f), new Color(0.42f, 0.4f, 0.12f), rib)
                                          : Color.Lerp(new Color(0.07f, 0.2f, 0.08f), new Color(0.2f, 0.45f, 0.16f), rib);
                    c = Color.Lerp(c, swc, sw * 0.95f);

                    // 貝殼與海星（池子外）
                    float sh = Shells(u, v, 7, 601, 0.2f, out var shc) * (1f - pool);
                    float shs = Shells(u - 0.006f, v + 0.006f, 7, 601, 0.2f, out _) * (1f - pool);
                    c *= 1f - 0.4f * shs * (1f - sh);
                    c = Color.Lerp(c, shc, sh);

                    c.a = pool > 0.5f ? 0.9f : 0.92f;
                    px[y * S + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 海面（底色層）
        static Texture2D WaterTex() { const int S = 256; return Finish(NewTex("Water", S, S, true, true), WaterPixels()); }

        /// <summary>波浪高度（可平鋪）：兩組整數波數的斜向正弦被雜訊扭曲＋細碎雜訊</summary>
        static float WaveH(float u, float v)
        {
            float w1 = Fbm(u, v, 101, 3, 3);
            float h = 0.45f * Mathf.Sin(2f * Mathf.PI * (3f * u + 2f * v) + 4f * w1)
                    + 0.3f * Mathf.Sin(2f * Mathf.PI * (-2f * u + 5f * v) + 3f * Fbm(u, v, 103, 4, 2))
                    + 0.8f * (Fbm(u, v, 105, 6, 4) - 0.5f);
            return h;
        }

        public static Color32[] WaterPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            var hgt = new float[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) hgt[y * S + x] = WaveH(x / (float)S, y / (float)S);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float h = hgt[y * S + x];
                    // 斜率當作受光（太陽在一側）
                    float sx = hgt[y * S + (x + 2) % S] - hgt[y * S + (x + S - 2) % S], sy = hgt[((y + 2) % S) * S + x] - hgt[((y + S - 2) % S) * S + x];
                    float light = Mathf.Clamp(-(sx * 0.6f + sy * 0.8f) * 6f, -1f, 1f);
                    var c = Color.Lerp(new Color(0.02f, 0.16f, 0.25f), new Color(0.05f, 0.34f, 0.43f), SS(-0.7f, 0.7f, h));
                    c = Color.Lerp(c, light > 0f ? new Color(0.25f, 0.6f, 0.66f) : new Color(0.01f, 0.1f, 0.18f), Mathf.Abs(light) * 0.55f);
                    // 浪峰：細細的亮線與零星的白浪
                    float ridge = 1f - Mathf.Abs(2f * Fbm(u + 0.06f * h, v, 107, 4, 4) - 1f);
                    c = Color.Lerp(c, new Color(0.35f, 0.64f, 0.7f), SS(0.84f, 0.95f, ridge) * 0.5f);
                    float cap = SS(0.93f, 0.985f, ridge) * SS(0.58f, 0.75f, Fbm(u, v, 109, 8, 2)) * SS(0.1f, 0.6f, h);
                    c = Color.Lerp(c, new Color(0.88f, 0.94f, 0.95f), cap * 0.85f);
                    c.a = 0.9f;
                    px[y * S + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 海面（疊加反光層）
        static Texture2D GlintTex() { const int S = 256; return Finish(NewTex("Glint", S, S, true, true), GlintPixels()); }

        public static Color32[] GlintPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    // 水面的光網：扭曲的 Voronoi 邊緣，只有部分區域亮
                    float wu = u + 0.04f * (Fbm(u, v, 121, 4, 2) - 0.5f), wv = v + 0.04f * (Fbm(u, v, 123, 4, 2) - 0.5f);
                    float e = VoronoiEdge(wu, wv, 9, 125);
                    float web = (1f - SS(0.0f, 0.04f, e)) * SS(0.5f, 0.72f, Fbm(u, v, 127, 3, 3));
                    // 零星的太陽亮點
                    float spark = SS(0.8f, 0.9f, Fbm(u, v, 129, 32, 2)) * SS(0.5f, 0.7f, Fbm(u, v, 131, 4, 2));
                    float a = Mathf.Clamp01(web * 0.22f + spark * 0.9f);
                    px[y * S + x] = new Color(0.75f + 0.25f * spark, 0.92f + 0.08f * spark, 1f, a);
                }
            return px;
        }

        // ---------------------------------------------------------------- 浪花帶（u 沿岸平鋪、v＝0 岩石側～1 外海側）
        static Texture2D FoamTex() { const int S = 256; return Finish(NewTex("Foam", S, S, true, false), FoamPixels()); }

        public static Color32[] FoamPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float wob = Fbm2(u, v, 141, 3, 2f, 3) - 0.5f;
                    float vv = v + wob * 0.18f;
                    // 貼著岩石的厚浪花帶
                    float solid = SS(0.0f, 0.06f, v) * (1f - SS(0.1f, 0.3f, vv));
                    // 往外散開的泡沫網（一個個泡泡的邊）
                    float wu = u + 0.035f * (Fbm2(u, v, 151, 8, 6f, 2) - 0.5f), wv = v * 1.1f + 0.05f * (Fbm2(u, v, 153, 8, 6f, 2) - 0.5f);
                    float e = VoronoiEdge(wu, wv, 14, 143);
                    float e2 = VoronoiEdge(wu, wv, 28, 145);
                    float lace = (1f - SS(0.03f, 0.13f, e)) * 0.7f + (1f - SS(0.01f, 0.05f, e2)) * 0.3f;
                    float soft = SS(0.5f, 0.72f, Fbm2(u, v, 155, 16, 14f, 3));
                    float patch = SS(0.35f, 0.6f, Fbm2(u, v, 147, 6, 3f, 4));
                    float reach = SS(0.0f, 0.08f, v) * (1f - SS(0.12f, 0.62f, vv));
                    float a = Mathf.Clamp01(solid * (0.75f + 0.25f * patch) + (lace * 0.85f + soft * 0.45f) * (0.35f + 0.65f * patch) * reach);
                    // 線狀的浪線（平行岸邊）
                    float line = Mathf.Exp(-Mathf.Pow((vv - 0.45f) / 0.025f, 2f)) * SS(0.4f, 0.6f, Fbm2(u, v, 149, 5, 1f, 3));
                    a = Mathf.Clamp01(a + line * 0.6f) * (1f - SS(0.85f, 1f, v));
                    float bright = 0.88f + 0.12f * solid;
                    px[y * S + x] = new Color(bright * 0.96f, bright, bright, a);
                }
            return px;
        }

        // ---------------------------------------------------------------- 岩坡（u 沿岸平鋪、v＝0 場地邊～1 海裡）
        static Texture2D RockSlopeTex() { const int W = 256, H = 128; return Finish(NewTex("ReefSlope", W, H, true, false), RockSlopePixels()); }

        public static Color32[] RockSlopePixels()
        {
            const int W = 256, H = 128; var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float n = Fbm2(u, v, 161, 4, 3f, 5), grain = Fbm2(u, v, 163, 48, 24f, 2);
                    // 平行岸邊的岩層與層間的縫
                    float strata = Mathf.Sin((v * 9f + 1.2f * Fbm2(u, v, 165, 3, 2f, 3)) * Mathf.PI * 2f);
                    float ledge = SS(0.75f, 0.95f, strata);
                    var c = Color.Lerp(new Color(0.24f, 0.22f, 0.2f), new Color(0.17f, 0.19f, 0.2f), n) * (0.7f + 0.6f * n) * (0.85f + 0.3f * grain);
                    c *= 1f - 0.45f * ledge;
                    // 越往下越濕越暗
                    c *= Mathf.Lerp(1f, 0.6f, SS(0.25f, 0.7f, v));
                    // 中段：藤壺帶
                    float bar = Barnacles(u, v * 0.5f, 28, 171, 0.9f, out float bl, out _) * SS(0.2f, 0.35f, v) * (1f - SS(0.55f, 0.65f, v));
                    c = Color.Lerp(c, new Color(0.62f, 0.6f, 0.55f) * bl, bar);
                    // 下段：深色淡菜＋綠藻
                    float mussel = SS(0.45f, 0.6f, Fbm2(u, v, 173, 24, 12f, 2)) * SS(0.5f, 0.62f, v) * (1f - SS(0.85f, 0.95f, v));
                    c = Color.Lerp(c, new Color(0.05f, 0.06f, 0.1f) + new Color(0.12f, 0.14f, 0.2f) * SS(0.7f, 0.9f, grain), mussel * 0.85f);
                    float alg = SS(0.4f, 0.55f, Fbm2(u, v, 175, 8, 6f, 3) + (v - 0.6f) * 0.8f);
                    c = Color.Lerp(c, new Color(0.1f, 0.24f, 0.08f) * (0.7f + 0.6f * grain), alg * 0.8f * SS(0.45f, 0.65f, v));
                    // 頂端靠場地的地方一點濕亮
                    c += new Color(0.05f, 0.06f, 0.07f) * SS(0.65f, 0.85f, grain) * (1f - SS(0.1f, 0.4f, v));
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 岩塊、海蝕柱（可平鋪）
        static Texture2D RockTex() { const int S = 128; return Finish(NewTex("SeaRock", S, S, true, true), RockPixels()); }

        public static Color32[] RockPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 181, 4, 5), grain = Fbm(u, v, 183, 32, 2);
                    float e = VoronoiEdge(u, v, 4, 185);
                    var c = Color.Lerp(new Color(0.34f, 0.32f, 0.3f), new Color(0.26f, 0.29f, 0.31f), n) * (0.7f + 0.5f * n) * (0.85f + 0.3f * grain);
                    c *= 0.55f + 0.45f * SS(0.0f, 0.12f, e);   // 岩面的裂紋
                    float lichen = SS(0.6f, 0.72f, Fbm(u, v, 187, 4, 4));
                    c = Color.Lerp(c, new Color(0.55f, 0.53f, 0.46f) * (0.8f + 0.4f * grain), lichen * 0.5f);   // 鹽漬與藤壺的淺色斑
                    float alg = SS(0.58f, 0.7f, Fbm(u, v, 189, 3, 4));
                    c = Color.Lerp(c, new Color(0.12f, 0.22f, 0.1f), alg * 0.6f);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 遠方背景
        static readonly Color ZenithCol = new Color(0.27f, 0.5f, 0.84f);
        const float Horizon = 0.135f;   // 海平線在背景圓柱上的高度（約等於眼睛高度）

        struct Isle { public float u, w, h, haze; public int kind; }

        /// <summary>背景：海平線以下是遠方的海（太陽倒影），海平線上是由遠到近的島嶼與海蝕柱剪影，再往上是藍天白雲與太陽光暈（u 可平鋪）</summary>
        static Texture2D BackdropTex() { const int W = 2048, H = 256; return Finish(NewTex("Backdrop", W, H, true, false), BackdropPixels()); }

        public static Color32[] BackdropPixels()
        {
            const int W = 2048, H = 256; var px = new Color32[W * H];
            const float sunU = 0.18f, sunV = 0.47f;
            // kind 0＝圓丘狀的島、1＝海蝕柱、2＝拱門岩；由遠到近排列（haze 大＝遠）
            var isles = new[]
            {
                new Isle { u = 0.05f, w = 0.06f, h = 0.09f, haze = 0.85f, kind = 0 },
                new Isle { u = 0.33f, w = 0.09f, h = 0.12f, haze = 0.8f, kind = 0 },
                new Isle { u = 0.62f, w = 0.05f, h = 0.07f, haze = 0.82f, kind = 0 },
                new Isle { u = 0.84f, w = 0.07f, h = 0.1f, haze = 0.78f, kind = 0 },
                new Isle { u = 0.45f, w = 0.035f, h = 0.075f, haze = 0.6f, kind = 0 },
                new Isle { u = 0.72f, w = 0.04f, h = 0.09f, haze = 0.55f, kind = 0 },
                new Isle { u = 0.12f, w = 0.006f, h = 0.12f, haze = 0.5f, kind = 1 },
                new Isle { u = 0.135f, w = 0.004f, h = 0.08f, haze = 0.5f, kind = 1 },
                new Isle { u = 0.52f, w = 0.012f, h = 0.1f, haze = 0.42f, kind = 2 },
                new Isle { u = 0.93f, w = 0.007f, h = 0.15f, haze = 0.38f, kind = 1 },
                new Isle { u = 0.27f, w = 0.025f, h = 0.06f, haze = 0.35f, kind = 0 },
            };
            const int lighthouse = 5;   // 這座島上有一座小燈塔
            float lhU = isles[lighthouse].u + 0.006f;

            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                // 每座島在這一欄的高度
                var tops = new float[isles.Length];
                for (int i = 0; i < isles.Length; i++)
                {
                    var s = isles[i];
                    float du = Mathf.Abs(Mathf.DeltaAngle(u * 360f, s.u * 360f)) / 360f, q = du / s.w;
                    if (s.kind == 0)
                    {
                        // 圓丘：兩側較陡（海崖），稜線起伏
                        float body = q < 1f ? Mathf.Pow(1f - q * q, 0.45f) : 0f;
                        tops[i] = body > 0f ? s.h * body * (0.7f + 0.55f * Fbm2(u, 0.3f, 191 + i, 48, 1f, 4)) : 0f;
                    }
                    else   // 海蝕柱／拱門：這一欄的最高點（實際寬度在下面逐點判斷）
                        tops[i] = du < s.w * 3.5f ? s.h * (0.9f + 0.1f * Fbm2(u, 0.2f, 195 + i, 256, 1f, 2)) : 0f;
                }
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)H;
                    Color c; float alpha = 0.92f;
                    // 太陽的位置（換算成公尺比例：u 一圈約 200m、v 全高約 22m）
                    float dsu = Mathf.DeltaAngle(u * 360f, sunU * 360f) / 360f * 201f, dsv = (v - sunV) * 22f;
                    float sunD = Mathf.Sqrt(dsu * dsu + dsv * dsv);
                    if (v < Horizon)
                    {
                        // 遠方的海：越近越深藍，海平線附近有一層淡淡的霧
                        float k = (Horizon - v) / Horizon;
                        c = Color.Lerp(new Color(0.42f, 0.6f, 0.68f), new Color(0.05f, 0.27f, 0.37f), SS(0f, 0.7f, k));
                        float streak = Fbm2(u, v, 201, 96, 220f, 3);
                        c *= 0.88f + 0.24f * streak;
                        // 太陽的倒影：海平線到下方一條閃閃發亮的光帶
                        float spread = 1.5f + 6f * k;
                        float glit = Mathf.Exp(-dsu * dsu / (spread * spread)) * SS(0.55f, 0.8f, Fbm2(u, v, 203, 384, 300f, 2));
                        c = Color.Lerp(c, new Color(1f, 0.97f, 0.85f), glit * 0.85f);
                        c = Color.Lerp(c, new Color(0.75f, 0.85f, 0.88f), Mathf.Exp(-k * 30f) * 0.45f);
                    }
                    else
                    {
                        // 天空：海平線附近淡、往上變深藍
                        float t = (v - Horizon) / (1f - Horizon);
                        c = Color.Lerp(new Color(0.8f, 0.89f, 0.94f), new Color(0.5f, 0.7f, 0.9f), SS(0f, 0.3f, t));
                        c = Color.Lerp(c, ZenithCol, SS(0.3f, 1f, t));
                        // 太陽光暈
                        c = Color.Lerp(c, new Color(1f, 0.97f, 0.86f), Mathf.Clamp01(Mathf.Exp(-sunD / 5f) * 0.55f + Mathf.Exp(-sunD * sunD / 2.5f) * 0.6f));
                        // 積雲：底部平、頂部蓬鬆，向光面亮、背光面帶灰藍
                        float band = SS(0.08f, 0.22f, t) * (1f - SS(0.55f, 0.85f, t));
                        float cn = Fbm2(u, v, 211, 14, 7f, 5);
                        float flatBase = SS(0.12f, 0.2f, t + 0.05f * Fbm2(u, 0.5f, 213, 8, 1f, 2));
                        float cloud = SS(0.5f, 0.62f, cn + 0.22f * band - 0.12f) * flatBase * band;
                        float cnUp = Fbm2(u, v + 0.03f, 211, 14, 7f, 5);
                        float lit = Mathf.Clamp01(0.6f + (cnUp - cn) * 6f + 0.3f * SS(0.5f, 0.75f, cn));
                        var cloudCol = Color.Lerp(new Color(0.68f, 0.74f, 0.84f), new Color(1f, 1f, 0.98f), lit);
                        cloudCol = Color.Lerp(cloudCol, new Color(1f, 0.95f, 0.82f), Mathf.Exp(-sunD / 9f) * 0.4f);
                        c = Color.Lerp(c, cloudCol, cloud * 0.92f);
                        // 海平線上的薄層雲
                        float strat = SS(0.55f, 0.75f, Fbm2(u, v, 215, 10, 60f, 3)) * Mathf.Exp(-t * 14f);
                        c = Color.Lerp(c, new Color(0.9f, 0.93f, 0.95f), strat * 0.5f);

                        // 島嶼剪影（由近到遠檢查，先碰到的在前面）
                        float dv = v - Horizon;
                        for (int i = isles.Length - 1; i >= 0; i--)
                        {
                            if (dv >= tops[i]) continue;
                            var s = isles[i];
                            float tv = dv / Mathf.Max(0.001f, tops[i]);
                            if (s.kind != 0)
                            {
                                // 海蝕柱：往上收窄、邊緣被浪蝕得坑坑疤疤，頂端圓鈍；拱門：較寬，中間下半部挖空
                                float du = Mathf.Abs(Mathf.DeltaAngle(u * 360f, s.u * 360f)) / 360f;
                                float rough = Fbm2(0.37f * i, v, 197 + i, 1, 70f, 3) - 0.5f;
                                float wv = s.w * (s.kind == 2 ? 3f : 1f) * (1.15f - 0.4f * tv + 0.6f * rough) * Mathf.Sqrt(1f - 0.75f * SS(0.8f, 1f, tv));
                                if (du > wv) continue;
                                if (s.kind == 2)
                                {
                                    float arch = du / (s.w * 1.5f), ah = s.h * 0.58f;
                                    if (arch < 1f && dv < ah * Mathf.Sqrt(1f - arch * arch)) continue;
                                }
                            }
                            var near = new Color(0.17f, 0.23f, 0.26f); var far = new Color(0.56f, 0.66f, 0.75f);
                            var ic = Color.Lerp(near, far, s.haze);
                            ic *= 0.85f + 0.3f * Fbm2(u, v, 221 + i, 128, 64f, 3);
                            ic = Color.Lerp(ic, far, (1f - SS(0f, 0.35f, tv)) * 0.25f);   // 根部被海霧籠罩
                            // 島頂的植被（綠）、迎光面稍亮、根部的白色浪花線
                            if (s.kind == 0) ic = Color.Lerp(ic, Color.Lerp(new Color(0.16f, 0.3f, 0.18f), far, s.haze), SS(0.55f, 0.9f, tv) * 0.6f);
                            float side = Mathf.DeltaAngle(u * 360f, s.u * 360f) < 0f ? 1f : 0f;
                            ic = Color.Lerp(ic, far, 0.12f * side);
                            ic = Color.Lerp(ic, new Color(0.88f, 0.92f, 0.94f), (1f - SS(0f, 0.012f, dv)) * (1f - s.haze) * 0.7f);
                            c = ic; alpha = 0.94f;
                            break;
                        }
                        // 小燈塔
                        {
                            float du = Mathf.Abs(Mathf.DeltaAngle(u * 360f, lhU * 360f)) / 360f;
                            float baseV = tops[lighthouse] * 0.85f;
                            float th = dv - baseV;
                            if (du < 0.0012f * (1f - 0.3f * Mathf.Clamp01(th / 0.05f)) && th > -0.01f && th < 0.05f)
                            {
                                var lc = th > 0.038f ? new Color(0.7f, 0.25f, 0.2f) : (Mathf.Repeat(th * 80f, 1f) < 0.5f ? new Color(0.9f, 0.9f, 0.88f) : new Color(0.72f, 0.3f, 0.25f));
                                c = Color.Lerp(lc, new Color(0.56f, 0.66f, 0.75f), isles[lighthouse].haze * 0.5f); alpha = 0.94f;
                            }
                        }
                        // 太陽本體（在雲與島的後面就被遮住）
                        if (alpha < 0.93f) c = Color.Lerp(c, new Color(1f, 0.99f, 0.93f), 1f - SS(0.9f, 1.2f, sunD));
                    }
                    px[y * W + x] = new Color(c.r, c.g, c.b, alpha);
                }
            }
            return px;
        }

        /// <summary>天空頂蓋：外圈接背景頂端的藍，往中心更深，飄著幾縷高空的捲雲（徑向）</summary>
        static Texture2D CapTex() { const int S = 128; return Finish(NewTex("SkyCap", S, S, false, false), CapPixels()); }

        public static Color32[] CapPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    var c = Color.Lerp(new Color(0.18f, 0.38f, 0.76f), ZenithCol, SS(0.2f, 1f, r));
                    float wisp = SS(0.55f, 0.75f, Fbm(u + 0.3f * v, v, 231, 4, 4));
                    c = Color.Lerp(c, new Color(0.85f, 0.9f, 0.97f), wisp * 0.45f * (1f - SS(0.85f, 1f, r)));
                    c.a = 0.92f;
                    px[y * S + x] = c;
                }
            return px;
        }
    }
}
