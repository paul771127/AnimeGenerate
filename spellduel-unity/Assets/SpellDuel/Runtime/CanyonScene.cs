using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 黃土山道場景（第 5 章，無盡模式第 41～50 波）：全部用程式產生，不需要任何圖檔。
    ///   1. 場地內的地板：壓實的黃土路面＋小石子與細裂紋，中間一條車轍（沿兩個主要缺口的方向貫穿場地）。
    ///   2. 場地外緊貼邊界：一圈層層堆疊的黃土／砂岩崖壁（崖腳在邊界外約 0.45～0.6m，崖頂高 4.5m＝CliffHeight，
    ///      崖頂邊緣約在邊界外 1～1.5m），有水平的岩層條紋、往內凹的沖溝、半腰的小平台；崖頂是一片平台，再往外還有兩層更高的梯田狀台地。
    ///      三個缺口讓山路穿過（兩個相對的主路口＋一個窄小的岔路）。
    ///   3. 崖上的乾枯灌木與枯樹、崖壁半腰的木棧道、主路口旁崖頂上的小土地廟（飄著香煙）。
    ///   4. 遠方背景：一層層霧濛濛的赭黃色山巒剪影＋淡色的天空（圓柱內側）＋天空頂蓋；空中飄著塵土，崖頂不時有碎石滾落。
    /// 地板與背景都是半透明，仍隱約看得到真實的房間（安全）。座標都是 AR session 座標。
    /// 所有不透明的東西一律在場地多邊形之外（離邊界至少約 0.3m）。
    /// </summary>
    public class CanyonScene : MonoBehaviour
    {
        /// <summary>崖頂的高度（相對地板）：遊戲從這個高度丟落石、讓小怪從崖頂跳下來</summary>
        public const float CliffHeight = 4.5f;
        const float BackRadius = 32f, BackHeight = 22f, VTile = 13f;
        const int NR = 17;   // 崖壁截面的點數

        // 缺口（山路穿過的地方）：方向角（相對場地中心，弧度）與半寬（公尺）。0 與 π 是主路（車轍沿這條線）、另一個是窄岔路
        static readonly float[] GapAng = { 0f, Mathf.PI, Mathf.PI * 0.58f };
        static readonly float[] GapHalf = { 1.15f, 1.15f, 0.8f };

        /// <summary>每個缺口在場地邊界上的位置（AR session 座標，y＝地板）</summary>
        public readonly List<Vector3> GapPoints = new List<Vector3>();

        Material floorMat, rutMat, backMat, capMat;
        ParticleSystem gust, trickle;
        readonly List<Vector3> lipSpots = new List<Vector3>(), topSpots = new List<Vector3>();
        List<Vector3> poly;
        Vector3 center;
        float areaR, clock, gustTimer = 0.5f, trickleTimer = 2f;

        public static CanyonScene Create(List<Vector3> polygon, float floorY)
        {
            var go = new GameObject("CanyonScene");
            var cs = go.AddComponent<CanyonScene>();
            cs.Build(polygon, floorY);
            return cs;
        }

        public void SetVisible(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }

        /// <summary>某個 AR session 座標的水平位置是否落在山路缺口裡（那裡沒有崖壁，崖頂上也不該站東西）</summary>
        public bool InGap(Vector3 sessionPos) => GapK(new Vector2(sessionPos.x - center.x, sessionPos.z - center.z)) < 0.5f;

        static Vector2 Dir(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));

        // ================================================================ 建置
        void Build(List<Vector3> polygon, float floorY)
        {
            poly = polygon != null && polygon.Count >= 3 ? polygon : null;
            bool hasPoly = poly != null;
            ArtKit.CenterAndRadius(poly, floorY, out center, out areaR);
            var origin = center;
            var blend = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var diffuse = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Mobile/Diffuse") ?? blend;
            var rng = new System.Random(53);

            for (int g = 0; g < GapAng.Length; g++)
            {
                var d = Dir(GapAng[g]);
                float R = ArtKit.EdgeDist(poly, center, GapAng[g], areaR);
                GapPoints.Add(new Vector3(center.x + d.x * R, floorY, center.z + d.y * R));
            }

            // ---- 場地內：黃土路面＋車轍
            if (hasPoly)
            {
                floorMat = ArtKit.NewMat(blend, CanyonTex.Loess(), 2910, new Color(0.5f, 0.5f, 0.5f, 0.47f), "Canyon");
                MeshObj("LoessFloor", ArtKit.PolyMesh(poly, center, 2.5f, "CanyonFloor"), new Vector3(0f, 0.004f, 0f), floorMat);
            }
            rutMat = ArtKit.NewMat(blend, CanyonTex.Ruts(), 2912, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Canyon");
            rutMat.mainTextureScale = new Vector2(1f, 1f);
            MeshObj("CartRuts", RutStrip(), origin + new Vector3(0f, 0.008f, 0f), rutMat);

            // ---- 遠方背景：層層山巒＋淡色天空＋頂蓋
            backMat = ArtKit.NewMat(blend, CanyonTex.Backdrop(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Canyon");
            backMat.mainTextureScale = new Vector2(2f, 1f);
            MeshObj("Backdrop", ArtKit.Cylinder(BackRadius, BackHeight, 96, "CanyonBackdrop"), new Vector3(center.x, floorY - 1.5f, center.z), backMat);
            capMat = ArtKit.NewMat(blend, CanyonTex.Cap(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f), "Canyon");
            MeshObj("SkyCap", ArtKit.Disc(BackRadius, 48, "CanyonCap"), new Vector3(center.x, floorY - 1.5f + BackHeight, center.z), capMat);

            // ---- 崖壁（一個網格）
            var cliffMat = new Material(diffuse) { mainTexture = CanyonTex.Cliff(), color = new Color(1f, 0.98f, 0.95f), name = "Canyon Cliff" };
            MeshObj("Cliffs", CliffMesh(), origin, cliffMat).GetComponent<MeshRenderer>().receiveShadows = true;

            // ---- 崖腳的碎石（每塊都檢查不會伸進場地）
            {
                var rockMat = new Material(diffuse) { mainTexture = ArtKit.WhiteTex(), color = new Color(0.8f, 0.64f, 0.42f), name = "Canyon Rock" };
                var mb = new ArtKit.MB(1);
                for (int k = 0, tries = 0; k < 22 && tries < 200; tries++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float sz = 0.12f + (float)rng.NextDouble() * 0.22f;
                    float R = ArtKit.EdgeDist(poly, center, a, areaR);
                    var prof = Profile(a, out _);
                    var p2 = Dir(a) * (R + prof.d[1] - 0.05f + (float)rng.NextDouble() * 0.15f);
                    if (GapK(p2) < 0.9f) continue;
                    if (ArtKit.SignedDistToPoly(poly, new Vector2(center.x + p2.x, center.z + p2.y)) < sz * 1.2f + 0.1f) continue;
                    ArtKit.FacetRock(mb, 0, new Vector3(p2.x, sz * 0.35f, p2.y), new Vector3(sz * 1.2f, sz * 0.8f, sz), 6, 4, 300 + k, 0.45f, (float)rng.NextDouble() * 6.28f);
                    k++;
                }
                MeshObj("TalusRocks", mb.Build("CanyonTalus"), origin, rockMat);
            }

            // ---- 乾枯灌木與枯樹（崖頂與台地上）
            {
                var dry1 = new Material(diffuse) { color = new Color(0.58f, 0.55f, 0.32f), name = "Canyon Shrub" };
                var dry2 = new Material(diffuse) { color = new Color(0.62f, 0.48f, 0.3f), name = "Canyon Shrub2" };
                var wood = new Material(diffuse) { mainTexture = ArtKit.WoodTex(), color = new Color(0.45f, 0.38f, 0.32f), name = "Canyon DeadWood" };
                var mb = new ArtKit.MB(3);
                for (int k = 0, tries = 0; k < 30 && tries < 300; tries++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float R = ArtKit.EdgeDist(poly, center, a, areaR);
                    var prof = Profile(a, out _);
                    bool far = rng.NextDouble() < 0.3;
                    float dd = far ? prof.d[13] + 0.6f + (float)rng.NextDouble() * 3f : prof.d[9] + 0.35f + (float)rng.NextDouble() * 2.6f;
                    var p2 = Dir(a) * (R + dd);
                    float h = SurfaceHeight(a, R + dd);
                    if (h < CliffHeight - 0.2f || GapK(p2) < 0.98f) continue;
                    float br = 0.22f + (float)rng.NextDouble() * 0.22f;
                    int n = 2 + rng.Next(2);
                    for (int b = 0; b < n; b++)
                    {
                        float s = br * (0.6f + 0.5f * (float)rng.NextDouble());
                        var o = new Vector3(((float)rng.NextDouble() - 0.5f) * br, s * 0.45f, ((float)rng.NextDouble() - 0.5f) * br);
                        ArtKit.Blob(mb, k % 2, new Vector3(p2.x, h - 0.04f, p2.y) + o, new Vector3(s, s * 0.7f, s), 6, 4, k * 7 + b);
                    }
                    k++;
                }
                // 枯樹：歪斜的樹幹＋幾根光禿禿的枝
                for (int k = 0, tries = 0; k < 4 && tries < 60; tries++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float R = ArtKit.EdgeDist(poly, center, a, areaR);
                    var prof = Profile(a, out _);
                    float dd = prof.d[9] + 0.9f + (float)rng.NextDouble() * 1.5f;
                    var p2 = Dir(a) * (R + dd);
                    float h = SurfaceHeight(a, R + dd);
                    if (h < CliffHeight - 0.1f || GapK(p2) < 0.98f) continue;
                    var basePos = new Vector3(p2.x, h - 0.1f, p2.y);
                    var tilt = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 24f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 24f);
                    float th = 1.6f + (float)rng.NextDouble() * 1.2f;
                    ArtKit.Lathe(mb, 2, basePos, tilt, new[] { 0f, 0.2f, th * 0.6f, th }, new[] { 0.14f, 0.1f, 0.07f, 0.03f }, 6, true, false);
                    for (int b = 0; b < 4; b++)
                    {
                        float y0 = th * (0.45f + 0.13f * b);
                        float ba = b * 2.1f + (float)rng.NextDouble();
                        var dir = tilt * new Vector3(Mathf.Cos(ba) * 0.8f, 0.6f + 0.2f * (float)rng.NextDouble(), Mathf.Sin(ba) * 0.8f).normalized;
                        float bl = 0.5f + (float)rng.NextDouble() * 0.6f;
                        ArtKit.Lathe(mb, 2, basePos + tilt * (Vector3.up * y0), Quaternion.FromToRotation(Vector3.up, dir), new[] { 0f, bl }, new[] { 0.04f, 0.008f }, 4, false, false);
                    }
                    k++;
                }
                MeshObj("DryPlants", mb.Build("CanyonPlants"), origin, dry1, dry2, wood);
            }

            // ---- 崖壁半腰的木棧道（離主路口遠的一側）＋崖頂上的小土地廟（主路口旁）
            BuildWalkway(diffuse, origin, Mathf.PI * 1.27f);
            BuildShrine(diffuse, origin);

            // ---- 粒子：空中的細塵、崖頂吹起的塵土、崖邊滾落的碎石、香煙
            var motes = SpellFx.NewPS(transform, "DustMotes", SpellFx.BlendMat(SpellFx.TexSoft), 90, true);
            motes.transform.position = new Vector3(center.x, floorY + 1.8f, center.z);
            SpellFx.Sphere(motes, areaR + 4f);
            var ms = motes.shape; ms.scale = new Vector3(1f, 0.35f, 1f);
            var mm = motes.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            mm.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            mm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.8f, 0.58f, 0.6f), new Color(0.8f, 0.66f, 0.45f, 0.45f));
            var mv = motes.velocityOverLifetime; mv.enabled = true; mv.space = ParticleSystemSimulationSpace.World;
            mv.x = 0.18f; mv.y = 0.01f; mv.z = 0.06f;   // 微微的風
            SpellFx.Noise(motes, 0.25f, 0.3f);
            SpellFx.FadeInOut(motes);
            SpellFx.Rate(motes, 10f);
            motes.Play();

            gust = SpellFx.NewPS(transform, "DustGust", SpellFx.BlendMat(SpellFx.TexSoft), 40, true);
            {
                var m = gust.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5f);
                m.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new Color(0.86f, 0.72f, 0.5f, 0.2f);
                gust.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.8f;
                SpellFx.FadeInOut(gust);
                SpellFx.SizeCurve(gust, SpellFx.Grow);
                SpellFx.Noise(gust, 0.3f, 0.2f);
                gust.Play();
            }
            trickle = SpellFx.NewPS(transform, "PebbleTrickle", SpellFx.BlendMat(SpellFx.TexSoft), 40, true);
            {
                var m = trickle.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                m.startColor = new Color(0.45f, 0.34f, 0.22f, 0.9f);
                m.gravityModifier = 0.8f;
                SpellFx.FadeOut(trickle);
                trickle.Play();
            }
            // 崖頂邊緣與台地上的點（給上面兩個粒子用）
            for (int k = 0; k < 24; k++)
            {
                float a = (k + 0.5f) / 24f * Mathf.PI * 2f;
                float R = ArtKit.EdgeDist(poly, center, a, areaR);
                var prof = Profile(a, out _);
                var lip = Dir(a) * (R + prof.d[9] - 0.03f);
                if (GapK(lip) < 0.98f) continue;
                lipSpots.Add(origin + new Vector3(lip.x, CliffHeight - 0.05f, lip.y));
                var top = Dir(a) * (R + prof.d[9] + 1.5f);
                topSpots.Add(origin + new Vector3(top.x, CliffHeight + 0.3f, top.y));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;
            // 崖頂不時被風吹起一團塵土（往同一個風向飄）
            gustTimer -= dt;
            if (gust && topSpots.Count > 0 && gustTimer <= 0f)
            {
                gustTimer = 0.6f + Random.value * 0.8f;
                var ep = new ParticleSystem.EmitParams
                {
                    position = topSpots[Random.Range(0, topSpots.Count)] + new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f)),
                    velocity = new Vector3(0.7f, 0.08f, 0.25f) * Random.Range(0.7f, 1.3f),
                    applyShapeToPosition = false
                };
                gust.Emit(ep, 1);
            }
            // 崖邊偶爾滾落幾顆碎石
            trickleTimer -= dt;
            if (trickle && lipSpots.Count > 0 && trickleTimer <= 0f)
            {
                trickleTimer = 1.5f + Random.value * 2.5f;
                var p = lipSpots[Random.Range(0, lipSpots.Count)];
                var outward = new Vector3(p.x - center.x, 0f, p.z - center.z).normalized;
                int n = Random.Range(3, 7);
                for (int i = 0; i < n; i++)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = p + new Vector3(Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f)),
                        velocity = -outward * Random.Range(0.2f, 0.5f) + Vector3.up * Random.Range(0f, 0.4f),
                        applyShapeToPosition = false
                    };
                    trickle.Emit(ep, 1);
                }
            }
        }

        // ================================================================ 崖壁的截面
        struct Prof { public float[] d, h; }

        /// <summary>
        /// a 方向的崖壁截面（不含缺口）：d＝離場地邊界的距離（沿中心射線）、h＝高度。
        /// 0～1：崖腳前的地面；1～9：崖壁（中間有岩層的小突出、半腰平台、沖溝往內凹）；9＝崖頂邊緣（h＝4.5）；
        /// 9～11：崖頂平台；11～13：第二層台地；14～16：第三層台地。
        /// </summary>
        Prof Profile(float a, out float d1)
        {
            float t = Mathf.Repeat(a / (Mathf.PI * 2f), 1f);
            float n1 = ArtKit.Noise1(t, 3, 12), n2 = ArtKit.Noise1(t, 7, 20);
            float gully = ArtKit.SS(0.6f, 0.78f, ArtKit.Noise1(t, 11, 40)) * 0.32f;   // 沖溝：上半部往內凹
            bool ledge = ArtKit.Noise1(t, 19, 10) > 0.58f;
            float d0 = 0.45f + 0.15f * n1;
            d1 = d0 + 0.6f + gully;
            var p = new Prof { d = new float[NR], h = new float[NR] };
            void S(int j, float d, float h) { p.d[j] = d; p.h[j] = h; }
            S(0, 0f, 0f);
            S(1, d0, 0f);
            S(2, d0 + 0.05f, 0.3f);
            S(3, d0 + 0.12f + 0.05f * n2, 1.15f);
            S(4, d0 + 0.08f + 0.05f * n2, 1.3f);                  // 岩層突出的下緣（微微外懸）
            S(5, d0 + 0.22f + gully * 0.5f, 2.3f);
            S(6, d0 + 0.22f + gully * 0.5f + (ledge ? 0.35f : 0.05f), 2.38f);   // 半腰的小平台
            S(7, d0 + 0.42f + gully, 3.5f);
            S(8, d0 + 0.38f + gully, 3.65f);
            S(9, d1, CliffHeight);
            S(10, d1 + 0.3f, CliffHeight + 0.03f + 0.05f * n2);
            S(11, d1 + 3.6f + n1, CliffHeight + 0.1f);
            S(12, d1 + 4.2f + n1, 5.8f + 0.4f * n2);
            S(13, d1 + 4.8f + n1, 6.8f + 0.4f * n2);
            S(14, d1 + 9f + 1.5f * n1, 7.0f + 0.4f * n2);
            S(15, d1 + 10f + 1.5f * n1, 9.0f + 0.8f * n1);
            S(16, d1 + 14f + n2, 9.4f + 0.8f * n1);
            return p;
        }

        /// <summary>缺口係數：1＝正常崖壁、0＝缺口裡（高度壓平成地面）。p＝相對場地中心的水平位置</summary>
        static float GapK(Vector2 p)
        {
            float k = 1f;
            for (int g = 0; g < GapAng.Length; g++)
            {
                var d = Dir(GapAng[g]);
                float along = Vector2.Dot(p, d);
                if (along <= 0f) continue;
                float lat = Mathf.Abs(d.x * p.y - d.y * p.x);
                float half = GapHalf[g] + along * 0.03f;            // 越遠越寬（像山谷）
                k = Mathf.Min(k, ArtKit.SS(half, half + 0.55f + along * 0.04f, lat));
            }
            return k;
        }

        /// <summary>a 方向、離中心 dist 處的崖壁表面高度（含缺口）</summary>
        float SurfaceHeight(float a, float dist)
        {
            float R = ArtKit.EdgeDist(poly, center, a, areaR);
            var p = Profile(a, out _);
            float d = dist - R, h = p.h[NR - 1];
            if (d <= p.d[0]) h = 0f;
            else
                for (int j = 0; j < NR - 1; j++)
                    if (d >= p.d[j] && d <= p.d[j + 1] && p.d[j + 1] > p.d[j]) { h = Mathf.Lerp(p.h[j], p.h[j + 1], (d - p.d[j]) / (p.d[j + 1] - p.d[j])); break; }
            return h * GapK(Dir(a) * dist);
        }

        /// <summary>崖壁：沿圓周一圈的高度場，分成三段（地面、崖面、崖頂以外），各段之間不共用頂點（稜線分明）</summary>
        Mesh CliffMesh()
        {
            var extra = new List<float>();
            for (int g = 0; g < GapAng.Length; g++)
                foreach (float r in new[] { areaR + 1f, areaR + 5f })
                    foreach (float lat in new[] { GapHalf[g] - 0.2f, GapHalf[g] + 0.2f, GapHalf[g] + 0.45f, GapHalf[g] + 0.75f })
                    {
                        float da = Mathf.Asin(Mathf.Clamp(lat / r, -1f, 1f));
                        extra.Add(GapAng[g] + da); extra.Add(GapAng[g] - da);
                    }
            var angs = ArtKit.RingAngles(poly, center, 176, extra);
            int n = angs.Count;
            var P = new Vector3[n + 1, NR]; var U = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                float a = angs[i];
                float R = ArtKit.EdgeDist(poly, center, a, areaR);
                var pr = Profile(a, out _);
                var dir = Dir(a);
                for (int j = 0; j < NR; j++)
                {
                    var p2 = dir * (R + pr.d[j]);
                    float h = pr.h[j] * GapK(p2);
                    if (h > 0f && poly != null && ArtKit.SignedDistToPoly(poly, new Vector2(center.x + p2.x, center.z + p2.y)) < 0.28f) h = 0f;   // 保險：不可伸進場地
                    P[i, j] = new Vector3(p2.x, h, p2.y);
                }
            }
            for (int j = 0; j < NR; j++) P[n, j] = P[0, j];
            // u：沿邊界外 1m 那一圈的弧長，總長取整數個 3m 的 tile
            float total = 0f;
            for (int i = 1; i <= n; i++) { total += (Ring1(angs, i) - Ring1(angs, i - 1)).magnitude; U[i] = total; }
            float tiles = Mathf.Max(1f, Mathf.Round(total / 3f));
            for (int i = 0; i <= n; i++) U[i] = U[i] / Mathf.Max(0.001f, total) * tiles;

            var mb = new ArtKit.MB(1);
            var rows = new[] { new[] { 0, 1 }, new[] { 1, 9 }, new[] { 9, NR - 1 } };
            foreach (var seg in rows)
            {
                int j0 = seg[0], j1 = seg[1], cols = j1 - j0 + 1, start = mb.V.Count;
                for (int i = 0; i <= n; i++)
                    for (int j = j0; j <= j1; j++)
                    {
                        var p = P[i, j];
                        float dd = new Vector2(p.x, p.z).magnitude - areaR;   // v 隨距離微調（平台頂面在貼圖上才有縱向變化）
                        mb.Add(p, Vector3.up, new Vector2(U[i], p.y / VTile + 0.0015f * dd));
                    }
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < cols - 1; j++)
                    {
                        int a = start + i * cols + j, b = a + cols, c = a + 1, d = b + 1;
                        mb.Tri(0, a, c, b); mb.Tri(0, b, c, d);
                    }
            }
            var m = mb.Build("CanyonCliffs");
            ArtKit.FixWindingUp(m);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        Vector2 Ring1(List<float> angs, int i)
        {
            float a = angs[i % angs.Count];
            return Dir(a) * (ArtKit.EdgeDist(poly, center, a, areaR) + 1f);
        }

        /// <summary>車轍：沿主路（0 與 π 方向）貫穿場地的長條，兩端漸隱（本地座標）</summary>
        Mesh RutStrip()
        {
            float L0 = ArtKit.EdgeDist(poly, center, Mathf.PI, areaR) + 14f, L1 = ArtKit.EdgeDist(poly, center, 0f, areaR) + 14f;
            const int segs = 32; const float hw = 0.85f;
            var mb = new ArtKit.MB(1);
            for (int i = 0; i <= segs; i++)
            {
                float x = Mathf.Lerp(-L0, L1, i / (float)segs);
                float fade = Mathf.Min(ArtKit.SS(-L0, -L0 + 6f, x), 1f - ArtKit.SS(L1 - 6f, L1, x));
                var col = new Color(1f, 1f, 1f, fade);
                mb.Add(new Vector3(x, 0f, -hw), Vector3.up, new Vector2(x / 2f, 0f), col);
                mb.Add(new Vector3(x, 0f, hw), Vector3.up, new Vector2(x / 2f, 1f), col);
            }
            for (int i = 0; i < segs; i++) { int a = i * 2; mb.Tri(0, a, a + 1, a + 2); mb.Tri(0, a + 2, a + 1, a + 3); }
            var m = mb.Build("CanyonRuts");
            ArtKit.FixWindingUp(m);
            return m;
        }

        // ================================================================ 木棧道與土地廟
        /// <summary>崖壁半腰（約 2.4m 高）沿著崖面的一段木棧道：木板、插進崖壁的橫樑、外側欄杆</summary>
        void BuildWalkway(Shader diffuse, Vector3 origin, float midAng)
        {
            var wood = new Material(diffuse) { mainTexture = ArtKit.WoodTex(), color = new Color(0.62f, 0.46f, 0.3f), name = "Canyon Plank" };
            var mb = new ArtKit.MB(1);
            const float y = 2.42f;
            float Rm = ArtKit.EdgeDist(poly, center, midAng, areaR) + 1f;
            float span = 4.2f / Rm;   // 約 4.2m 長
            int planks = 20;
            Vector3 prevPost = Vector3.zero; bool hasPrev = false;
            for (int k = 0; k <= planks; k++)
            {
                float a = midAng - span * 0.5f + span * k / planks;
                if (GapK(Dir(a) * Rm) < 0.98f) { hasPrev = false; continue; }
                float R = ArtKit.EdgeDist(poly, center, a, areaR);
                var pr = Profile(a, out _);
                float face = Mathf.Lerp(pr.d[5], pr.d[6], 0.5f);   // 這個高度的崖面
                float inner = Mathf.Max(face - 0.6f, 0.35f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var t = new Vector3(-dir.z, 0f, dir.x);
                var rot = Quaternion.LookRotation(dir, Vector3.up);
                float len = face + 0.1f - inner;
                // 木板（沿徑向）
                ArtKit.Box(mb, 0, dir * (R + inner + len * 0.5f) + Vector3.up * y, new Vector3(0.19f, 0.05f, len), rot);
                if (k % 4 == 0)
                {
                    // 橫樑插進崖壁、斜撐頂在下方崖面
                    ArtKit.Box(mb, 0, dir * (R + inner + (len + 0.3f) * 0.5f) + Vector3.up * (y - 0.07f), new Vector3(0.09f, 0.09f, len + 0.3f), rot);
                    var post = dir * (R + inner + 0.04f) + Vector3.up * (y + 0.45f);
                    ArtKit.Box(mb, 0, post, new Vector3(0.06f, 0.9f, 0.06f), rot);
                    var brFrom = dir * (R + inner + 0.08f) + Vector3.up * (y - 0.08f);
                    var brTo = dir * (R + pr.d[3] + 0.02f) + Vector3.up * 1.2f;
                    ArtKit.Lathe(mb, 0, brFrom, Quaternion.FromToRotation(Vector3.up, (brTo - brFrom).normalized), new[] { 0f, (brTo - brFrom).magnitude }, new[] { 0.035f, 0.035f }, 5, false, false);
                    if (hasPrev)
                    {
                        // 欄杆橫木：連到上一根柱子
                        var top0 = prevPost + Vector3.up * 0.38f; var top1 = post + Vector3.up * 0.38f;
                        ArtKit.Lathe(mb, 0, top0, Quaternion.FromToRotation(Vector3.up, (top1 - top0).normalized), new[] { 0f, (top1 - top0).magnitude }, new[] { 0.025f, 0.025f }, 5, false, false);
                    }
                    prevPost = post; hasPrev = true;
                }
            }
            MeshObj("Walkway", mb.Build("CanyonWalkway"), origin, wood);
        }

        /// <summary>主路口（0 方向）旁的崖頂上：一座小小的土地廟（石台、磚牆、黑洞洞的門、紅布、灰瓦攢尖頂）＋一縷香煙</summary>
        void BuildShrine(Shader diffuse, Vector3 origin)
        {
            float ga = GapAng[0];
            var gd = Dir(ga); var side = new Vector2(-gd.y, gd.x);
            float R = ArtKit.EdgeDist(poly, center, ga, areaR);
            Vector2 p2 = Vector2.zero; float h = 0f; bool ok = false;
            foreach (float s in new[] { 1f, -1f })
                for (float lat = GapHalf[0] + 1.6f; lat < GapHalf[0] + 4f && !ok; lat += 0.4f)
                {
                    var q = gd * (R + 2.2f) + side * (s * lat);
                    float a = Mathf.Atan2(q.y, q.x);
                    float hh = SurfaceHeight(a, q.magnitude);
                    if (hh > CliffHeight - 0.1f && GapK(q) > 0.99f) { p2 = q; h = hh; ok = true; }
                }
            if (!ok) return;
            var stone = new Material(diffuse) { color = new Color(0.62f, 0.6f, 0.56f), name = "Canyon ShrineStone" };
            var brick = new Material(diffuse) { color = new Color(0.66f, 0.5f, 0.4f), name = "Canyon ShrineBrick" };
            var roof = new Material(diffuse) { color = new Color(0.3f, 0.3f, 0.32f), name = "Canyon ShrineRoof" };
            var red = new Material(diffuse) { color = new Color(0.78f, 0.14f, 0.1f), name = "Canyon ShrineRed" };
            var mb = new ArtKit.MB(4);
            // 本地座標：+z＝朝向場地中心
            ArtKit.Box(mb, 0, new Vector3(0f, 0.12f, 0f), new Vector3(1.2f, 0.28f, 1.0f), Quaternion.identity);
            ArtKit.Box(mb, 1, new Vector3(0f, 0.65f, -0.05f), new Vector3(0.9f, 0.8f, 0.72f), Quaternion.identity);
            ArtKit.Box(mb, 2, new Vector3(0f, 0.55f, 0.31f), new Vector3(0.36f, 0.5f, 0.02f), Quaternion.identity);   // 門洞（暗）
            ArtKit.Box(mb, 3, new Vector3(0f, 0.86f, 0.32f), new Vector3(0.56f, 0.12f, 0.02f), Quaternion.identity);  // 門楣的紅布
            foreach (float s in new[] { -1f, 1f }) ArtKit.Box(mb, 3, new Vector3(s * 0.3f, 0.55f, 0.32f), new Vector3(0.08f, 0.5f, 0.02f), Quaternion.identity);   // 紅對聯
            ArtKit.Lathe(mb, 2, new Vector3(0f, 1.05f, -0.05f), Quaternion.Euler(0f, 45f, 0f), new[] { 0f, 0.06f, 0.42f }, new[] { 0.82f, 0.8f, 0.04f }, 4, false, true);   // 攢尖頂
            ArtKit.Lathe(mb, 3, new Vector3(0f, 1.44f, -0.05f), Quaternion.identity, new[] { 0f, 0.08f, 0.14f }, new[] { 0.04f, 0.06f, 0.0f }, 6, false, false);   // 頂上的寶珠
            ArtKit.Lathe(mb, 0, new Vector3(0f, 0.26f, 0.42f), Quaternion.identity, new[] { 0f, 0.1f, 0.14f }, new[] { 0.07f, 0.09f, 0.1f }, 8, false, true);   // 香爐
            var pos = origin + new Vector3(p2.x, h - 0.06f, p2.y);
            var go = MeshObj("RoadShrine", mb.Build("CanyonShrine"), pos, stone, brick, roof, red);
            var toC = new Vector3(-p2.x, 0f, -p2.y);
            go.transform.rotation = Quaternion.LookRotation(toC.sqrMagnitude > 1e-4f ? toC.normalized : Vector3.forward, Vector3.up);

            var incense = SpellFx.NewPS(transform, "Incense", SpellFx.BlendMat(SpellFx.TexSoft), 30, true);
            incense.transform.position = go.transform.TransformPoint(new Vector3(0f, 0.42f, 0.42f));
            SpellFx.Sphere(incense, 0.02f);
            var im = incense.main;
            im.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 3.5f);
            im.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
            im.startColor = new Color(0.85f, 0.85f, 0.82f, 0.35f);
            SpellFx.Rise(incense, 0.3f);
            SpellFx.Noise(incense, 0.12f, 0.8f);
            SpellFx.FadeInOut(incense);
            SpellFx.SizeCurve(incense, SpellFx.Grow);
            SpellFx.Rate(incense, 6f);
            incense.Play();
        }

        GameObject MeshObj(string name, Mesh mesh, Vector3 pos, params Material[] mats) => ArtKit.MeshObj(transform, name, mesh, pos, mats);
    }

    /// <summary>黃土山道用的程式產生貼圖</summary>
    static class CanyonTex
    {
        static float SS(float a, float b, float x) => ArtKit.SS(a, b, x);
        static float Fbm(float u, float v, int s, int p, int o) => ArtKit.Fbm(u, v, s, p, o);
        static float Hash(int x, int y, int s) => ArtKit.Hash(x, y, s);
        static readonly Color Earth = new Color(0.8f, 0.64f, 0.4f);

        /// <summary>壓實的黃土路面：赭黃底色＋細裂紋＋小石子（可平鋪，a≈0.92）</summary>
        public static Texture2D Loess() { const int S = 256; return ArtKit.Finish(ArtKit.NewTex("Loess", S, S, true, true), LoessPixels()); }

        public static Color32[] LoessPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 4, 4, 5), fine = Fbm(u, v, 8, 32, 2);
                    var c = Color.Lerp(Earth * 0.88f, new Color(0.88f, 0.74f, 0.5f), n);
                    c *= 0.93f + 0.1f * fine;
                    float crack = (1f - SS(0.0f, 0.03f, ArtKit.VoronoiEdge(u, v, 5, 13))) * SS(0.4f, 0.6f, Fbm(u, v, 15, 3, 3));
                    c *= 1f - 0.25f * crack;
                    // 小石子：每格一顆（亮面＋下方陰影）
                    const int cells = 14;
                    int cx = Mathf.FloorToInt(u * cells), cy = Mathf.FloorToInt(v * cells);
                    for (int j = -1; j <= 1; j++)
                        for (int i = -1; i <= 1; i++)
                        {
                            int wx = ((cx + i) % cells + cells) % cells, wy = ((cy + j) % cells + cells) % cells;
                            if (Hash(wx, wy, 21) > 0.45f) continue;
                            float r = (0.12f + 0.2f * Hash(wx, wy, 23)) / cells;
                            float pxc = (cx + i + 0.2f + 0.6f * Hash(wx, wy, 25)) / cells, pyc = (cy + j + 0.2f + 0.6f * Hash(wx, wy, 27)) / cells;
                            float dx = u - pxc, dy = v - pyc;
                            float sh = Mathf.Sqrt(dx * dx + (dy + r * 0.35f) * (dy + r * 0.35f));
                            if (sh < r * 1.1f) c *= 0.82f;
                            float d = Mathf.Sqrt(dx * dx + dy * dy);
                            if (d < r)
                            {
                                float tone = Hash(wx, wy, 29);
                                var pc = tone < 0.5f ? new Color(0.62f, 0.55f, 0.46f) : new Color(0.86f, 0.8f, 0.68f);
                                c = pc * (0.85f + 0.3f * (dy / r * 0.5f + 0.5f));
                            }
                        }
                    c.a = 0.92f;
                    px[y * S + x] = c;
                }
            return px;
        }

        /// <summary>車轍：兩道深色的輪溝（v＝0.25 與 0.75）、旁邊被擠高的亮邊；u 沿路可平鋪</summary>
        public static Texture2D Ruts() { const int W = 128, H = 64; return ArtKit.Finish(ArtKit.NewTex("Ruts", W, H, true, false), RutPixels()); }

        public static Color32[] RutPixels()
        {
            const int W = 128, H = 64; var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float wob = (Fbm(u, 0.3f, 3, 4, 3) - 0.5f) * 0.05f;
                    float groove = 0f, ridge = 0f;
                    foreach (float c0 in new[] { 0.25f, 0.75f })
                    {
                        float d = Mathf.Abs(v - c0 - wob);
                        groove = Mathf.Max(groove, 1f - SS(0.025f, 0.06f, d));
                        ridge = Mathf.Max(ridge, (1f - SS(0.0f, 0.03f, Mathf.Abs(d - 0.08f))));
                    }
                    float n = Fbm(u, v, 9, 8, 3);
                    float edge = SS(0f, 0.1f, v) * SS(1f, 0.9f, v);
                    var col = Color.Lerp(new Color(0.95f, 0.85f, 0.62f), new Color(0.42f, 0.3f, 0.18f), groove);
                    float a = Mathf.Clamp01(groove * (0.55f + 0.25f * n) + ridge * 0.25f) * edge;
                    col.a = a;
                    px[y * W + x] = col;
                }
            return px;
        }

        /// <summary>
        /// 崖壁：u 沿圓周（3m 一個 tile，可平鋪）、v＝高度 ÷ 13m（不平鋪）。
        /// 高度 0～0.3m 是地面色；往上是一層層深淺不同的水平岩層、垂直的沖溝暗紋；
        /// 4.4～5.0、6.7～8.0、8.9m 以上 是台地頂面（表土＋稀疏的乾草）。
        /// </summary>
        public static Texture2D Cliff() { const int W = 512, H = 256; return ArtKit.Finish(ArtKit.NewTex("Cliff", W, H, true, false), CliffPixels()); }

        public static Color32[] CliffPixels()
        {
            const int W = 512, H = 256; var px = new Color32[W * H];
            var tones = new[] { new Color(0.88f, 0.74f, 0.5f), new Color(0.78f, 0.58f, 0.34f), new Color(0.74f, 0.5f, 0.32f), new Color(0.84f, 0.68f, 0.44f), new Color(0.7f, 0.56f, 0.38f) };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = (y + 0.5f) / H, h = v * 13f;
                    float n = Fbm(u, v, 5, 8, 4);
                    Color c;
                    bool top = (h > 4.4f && h < 5.0f) || (h > 6.7f && h < 8.0f) || h > 8.9f;
                    if (h < 0.3f)
                    {
                        c = Color.Lerp(Earth * 0.92f, Earth, n);
                    }
                    else if (top)
                    {
                        // 表土：比崖面暗一點、帶點灰綠的乾草斑點
                        c = Color.Lerp(new Color(0.7f, 0.58f, 0.37f), new Color(0.8f, 0.68f, 0.45f), Fbm(u, v * 4f, 31, 8, 4));
                        float grass = SS(0.62f, 0.7f, Fbm(u, v * 4f, 33, 16, 3));
                        c = Color.Lerp(c, new Color(0.56f, 0.55f, 0.32f), grass * 0.7f);
                    }
                    else
                    {
                        // 岩層：高度帶微微起伏，每層的色調由雜湊決定
                        float layer = h * 2.6f + (Fbm(u, 0.5f, 7, 4, 3) - 0.5f) * 0.8f;
                        int band = Mathf.FloorToInt(layer);
                        float fb = layer - band;
                        c = tones[Mathf.Abs(band * 7 + 3) % tones.Length];
                        c *= 0.9f + 0.15f * n;
                        if (fb < 0.07f && Hash(band, 1, 3) < 0.5f) c *= 0.72f;          // 層與層之間的暗線
                        if (fb > 0.9f) c *= 0.92f;
                        // 垂直的沖溝暗紋與細雨痕
                        float gully = SS(0.58f, 0.75f, Fbm(u, v * 0.08f, 9, 16, 3));
                        c *= 1f - 0.18f * gully;
                        float rill = Mathf.Abs(Mathf.Sin((u * 48f + Fbm(u, v, 11, 4, 2) * 2f) * Mathf.PI));
                        c *= 0.96f + 0.05f * rill;
                        // 崖頂邊緣下方的深色（被雨水沖刷的黑紋）
                        if (h > 3.9f && h < 4.4f) c *= 0.88f + 0.12f * SS(0.4f, 0.6f, Fbm(u, v, 13, 32, 2));
                    }
                    c.a = 1f;
                    px[y * W + x] = c;
                }
            return px;
        }

        /// <summary>背景：三層由近而遠、越來越淡的黃土台地山巒（頂部平、有梯田般的階），上方是淡色的天空（u 可平鋪）</summary>
        public static Texture2D Backdrop() { const int W = 1024, H = 256; return ArtKit.Finish(ArtKit.NewTex("Backdrop", W, H, true, false), BackdropPixels()); }

        public static Color32[] BackdropPixels()
        {
            const int W = 1024, H = 256; var px = new Color32[W * H];
            var layers = new[]
            {
                (0.82f, 0.86f, 11, new Color(0.86f, 0.8f, 0.7f)),   // 最遠：最高、最淡
                (0.72f, 0.76f, 23, new Color(0.84f, 0.72f, 0.54f)),
                (0.5f, 0.62f, 37, new Color(0.78f, 0.62f, 0.42f)),   // 最近：最矮、最濃
            };
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                var tops = new float[layers.Length];
                for (int l = 0; l < layers.Length; l++)
                {
                    var (lo, hi, s, _) = layers[l];
                    float raw = Mathf.Lerp(lo - 0.25f, hi, Fbm(u, 0.4f, s, 3 + l * 2, 4));
                    float step = Mathf.Floor(raw * 40f) / 40f;   // 台地的階
                    tops[l] = Mathf.Lerp(raw, step, 0.7f);
                }
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)H;
                    float sky = SS(0.4f, 1f, v);
                    var c = Color.Lerp(new Color(0.93f, 0.9f, 0.82f), new Color(0.72f, 0.8f, 0.88f), sky);
                    c = Color.Lerp(c, new Color(0.97f, 0.95f, 0.9f), SS(0.55f, 0.75f, Fbm(u * 2f, v * 0.5f, 41, 4, 4)) * 0.35f);   // 淡淡的雲
                    for (int l = 0; l < layers.Length; l++)
                        if (v < tops[l])
                        {
                            var lc = layers[l].Item4;
                            float strata = 0.94f + 0.08f * Mathf.Sin(v * 220f + Fbm(u, v, 51 + l, 8, 2) * 4f);
                            lc *= strata * (0.92f + 0.12f * Fbm(u, v, 61 + l, 16, 3));
                            float haze = (1f - SS(0f, tops[l], v)) * 0.25f * (2 - l) / 2f;   // 山腳的霧
                            c = Color.Lerp(lc, new Color(0.93f, 0.88f, 0.78f), haze);
                        }
                    c.a = 0.93f;
                    px[y * W + x] = c;
                }
            }
            return px;
        }

        /// <summary>天空頂蓋：淡藍灰色，外圈偏黃（被塵土染色）</summary>
        public static Texture2D Cap() { const int S = 128; return ArtKit.Finish(ArtKit.NewTex("SkyCap", S, S, false, false), CapPixels()); }

        public static Color32[] CapPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    var c = Color.Lerp(new Color(0.66f, 0.76f, 0.86f), new Color(0.74f, 0.79f, 0.84f), r);
                    c = Color.Lerp(c, new Color(0.96f, 0.95f, 0.92f), SS(0.55f, 0.78f, Fbm(u, v, 71, 4, 4)) * 0.4f);
                    c.a = 0.93f;
                    px[y * S + x] = c;
                }
            return px;
        }
    }
}
