using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 魔法森林場景（無盡模式第 11～20 波）：全部用程式產生，不需要任何圖檔。
    ///   1. 場地內的地板：深色泥土＋青苔＋落葉＋小草叢（貼在玩家畫的場地範圍上）。
    ///   2. 場地外：一大片更暗的森林地面（到 35m），上面飄著一層緩緩流動的低霧。
    ///   3. 場地周圍一圈程式產生的樹（闊葉樹與針葉樹）、灌木、發光蘑菇、倒下的樹幹，還有幾道從樹冠灑下的光束。
    ///   4. 遠方背景：一層層由近而遠、越來越朦朧的樹林剪影＋霧濛濛的天空與光束（圓柱內側）＋頭頂的樹冠頂蓋。
    ///   5. 空中飄著螢火蟲、花粉孢子、落葉；地面有薄霧。
    /// 地板與背景都是半透明，仍隱約看得到真實的房間（安全）。座標都是 AR session 座標。
    /// 樹木一律種在場地外（場地最大半徑 + 2m 以外），樹冠也不會伸進場地裡。
    /// </summary>
    public class ForestScene : MonoBehaviour
    {
        const float GroundRadius = 35f, BackRadius = 32f, BackHeight = 22f;
        Material floorMat, groundMat, mistMat, backMat, capMat, rayMat, fireflyMat, capGlowMat;
        ParticleSystem mushroomGlow;
        readonly List<Vector3> glowSpots = new List<Vector3>();
        readonly List<Transform> trees = new List<Transform>();
        readonly List<Quaternion> treeRot = new List<Quaternion>();
        readonly List<float> treePhase = new List<float>();
        float clock, glowTimer;
        int glowIdx;

        public static ForestScene Create(List<Vector3> polygon, float floorY)
        {
            var go = new GameObject("ForestScene");
            var fs = go.AddComponent<ForestScene>();
            fs.Build(polygon, floorY);
            return fs;
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
            var diffuse = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Mobile/Diffuse") ?? blend;

            // ---- 場地外：較暗的森林地面
            groundMat = NewMat(blend, UndergrowthTex(), 2905, new Color(0.5f, 0.5f, 0.5f, 0.46f));
            groundMat.mainTextureScale = new Vector2(GroundRadius / 3f, GroundRadius / 3f);
            MeshObj("ForestGround", Disc(GroundRadius, 64), new Vector3(center.x, floorY - 0.03f, center.z), groundMat);

            // ---- 場地內：青苔地面
            if (poly != null && poly.Count >= 3)
            {
                floorMat = NewMat(blend, MossFloorTex(), 2910, new Color(0.5f, 0.5f, 0.5f, 0.47f));
                MeshObj("MossFloor", PolyMesh(poly, center, 2f), new Vector3(0f, 0.004f, 0f), floorMat);
            }

            // ---- 場地外的低霧（環狀，不蓋到場地）
            mistMat = NewMat(blend, MistTex(), 2920, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            MeshObj("GroundMist", Annulus(areaR + 0.6f, GroundRadius - 4f, 64, 7f), new Vector3(center.x, floorY + 0.18f, center.z), mistMat);

            // ---- 遠方背景：樹林剪影＋霧濛濛的天空（圓柱內側）＋頂蓋
            backMat = NewMat(blend, BackdropTex(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            backMat.mainTextureScale = new Vector2(2f, 1f);
            MeshObj("Backdrop", Cylinder(BackRadius, BackHeight, 96), new Vector3(center.x, floorY - 1.5f, center.z), backMat);
            capMat = NewMat(blend, CapTex(), 2900, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            MeshObj("CanopyCap", Disc(BackRadius, 48), new Vector3(center.x, floorY - 1.5f + BackHeight, center.z), capMat);

            // ---- 樹、灌木、蘑菇、倒木
            var barkMat = new Material(diffuse) { mainTexture = BarkTex(), color = new Color(0.62f, 0.52f, 0.44f), name = "Forest Bark" };
            barkMat.mainTextureScale = new Vector2(2f, 1f);
            var leafTex = LeafTex();
            var leafCols = new[] { new Color(0.78f, 0.95f, 0.62f), new Color(0.6f, 0.85f, 0.62f), new Color(0.9f, 0.95f, 0.5f), new Color(0.5f, 0.72f, 0.62f) };
            var leafMats = new Material[leafCols.Length];
            for (int i = 0; i < leafCols.Length; i++) leafMats[i] = new Material(diffuse) { mainTexture = leafTex, color = leafCols[i], name = "Forest Leaves" + i };
            var pineMat = new Material(diffuse) { mainTexture = leafTex, color = new Color(0.42f, 0.66f, 0.58f), name = "Forest Pine" };

            var rng = new System.Random(23);
            var placed = new List<Vector2>();
            int treeCount = Mathf.Clamp(Mathf.RoundToInt(26f + areaR * 2f), 28, 38);
            for (int i = 0, tries = 0; i < treeCount && tries < 600; tries++)
            {
                // 越外圈越容易放樹（面積大），內圈仍保證至少 areaR + 2
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = areaR + 2f + Mathf.Sqrt((float)rng.NextDouble()) * 10f;
                var p2 = new Vector2(center.x + Mathf.Cos(a) * r, center.z + Mathf.Sin(a) * r);
                bool ok = true;
                foreach (var q in placed) if ((q - p2).sqrMagnitude < 2.4f * 2.4f) { ok = false; break; }
                if (!ok) continue;
                placed.Add(p2);
                bool conifer = rng.NextDouble() < 0.4;
                float h = (conifer ? 5f : 4f) + (float)rng.NextDouble() * 4.5f;
                if (rng.NextDouble() < 0.2) h += 3f;   // 幾棵特別高的老樹
                float spread = Mathf.Min(conifer ? 1.9f : 2.4f, r - areaR - 0.5f);   // 樹冠水平範圍不可伸進場地
                var go = new GameObject(conifer ? "PineTree" : "Tree");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(p2.x, floorY - 0.1f, p2.y);
                var rot = Quaternion.Euler((float)rng.NextDouble() * 3f - 1.5f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 3f - 1.5f);
                go.transform.rotation = rot;
                go.AddComponent<MeshFilter>().sharedMesh = conifer ? PineMesh(h, spread, 100 + i) : TreeMesh(h, spread, 100 + i);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { barkMat, conifer ? pineMat : leafMats[rng.Next(leafMats.Length)] };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trees.Add(go.transform); treeRot.Add(rot); treePhase.Add((float)rng.NextDouble() * 10f);
                i++;
            }

            // 灌木：兩組（兩種綠色），各合併成一個網格
            for (int g = 0; g < 2; g++)
            {
                var mb = new MB(1);
                for (int k = 0; k < 9; k++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, br = 0.35f + (float)rng.NextDouble() * 0.35f;
                    float r = areaR + 0.4f + br * 2.4f + (float)rng.NextDouble() * 9f;   // 灌木最外緣約 2.2×br，仍在場地外
                    var c = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    int n = 3 + rng.Next(3);
                    for (int b = 0; b < n; b++)
                    {
                        float s = br * (0.6f + 0.5f * (float)rng.NextDouble());
                        var o = new Vector3(((float)rng.NextDouble() - 0.5f) * br * 1.2f, s * 0.55f, ((float)rng.NextDouble() - 0.5f) * br * 1.2f);
                        Blob(mb, 0, c + o, new Vector3(s, s * 0.8f, s), 7, 5, k * 13 + b, Color.white);
                    }
                }
                MeshObjDiffuse(g == 0 ? "Bushes" : "Bushes2", mb.Build("ForestBushes"), new Vector3(center.x, floorY - 0.05f, center.z), new[] { leafMats[g == 0 ? 1 : 3] });
            }

            // 倒下的樹幹（沿圓周方向橫躺）
            {
                var mb = new MB(1);
                for (int k = 0; k < 5; k++)
                {
                    float a = (k + (float)rng.NextDouble() * 0.7f) / 5f * Mathf.PI * 2f, len = 1.6f + (float)rng.NextDouble() * 1.6f;
                    float lr = 0.16f + (float)rng.NextDouble() * 0.12f, r = areaR + 1.4f + lr + (float)rng.NextDouble() * 6f;
                    var pos = new Vector3(Mathf.Cos(a) * r, lr * 0.7f, Mathf.Sin(a) * r);
                    var dir = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var rot = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    Lathe(mb, 0, pos - dir * len * 0.5f, rot, new[] { 0f, len * 0.33f, len * 0.66f, len },
                          new[] { lr, lr * 1.05f, lr * 0.95f, lr * 0.85f }, 8, true, true, Color.white);
                }
                MeshObjDiffuse("FallenLogs", mb.Build("ForestLogs"), new Vector3(center.x, floorY - 0.05f, center.z), new[] { barkMat });
            }

            // 發光蘑菇：莖（受光）＋菌傘（不受光、自己發光）合併成一個網格兩個子網格
            {
                var stemMat = new Material(diffuse) { color = new Color(0.85f, 0.82f, 0.74f), name = "Forest MushroomStem" };
                capGlowMat = NewMat(blend, WhiteTex(), 2950, new Color(0.5f, 0.5f, 0.5f, 0.5f));
                var mb = new MB(2);
                for (int k = 0; k < 9; k++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = areaR + 0.9f + (float)rng.NextDouble() * 7f;
                    var c = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    bool violet = rng.NextDouble() < 0.35;
                    var rim = violet ? new Color(0.55f, 0.25f, 0.95f) : new Color(0.1f, 0.75f, 0.85f);
                    var top = violet ? new Color(0.95f, 0.75f, 1f) : new Color(0.7f, 1f, 0.95f);
                    int n = 2 + rng.Next(4);
                    for (int m = 0; m < n; m++)
                    {
                        float s = 0.6f + (float)rng.NextDouble() * 0.9f;
                        var o = c + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.4f, 0f, ((float)rng.NextDouble() - 0.5f) * 0.4f);
                        float sh = 0.12f * s, sr = 0.018f * s, cr = 0.07f * s;
                        Lathe(mb, 0, o, Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 16f, 0f, ((float)rng.NextDouble() - 0.5f) * 16f),
                              new[] { 0f, sh * 0.5f, sh }, new[] { sr * 1.3f, sr, sr * 0.9f }, 6, false, false, Color.white);
                        // 菌傘：圓頂，邊緣顏色深、頂端亮
                        var hs = new[] { sh * 0.85f, sh + cr * 0.15f, sh + cr * 0.45f, sh + cr * 0.62f, sh + cr * 0.7f };
                        var rs = new[] { cr, cr * 0.95f, cr * 0.7f, cr * 0.35f, 0.001f };
                        var cols = new[] { rim, rim, Color.Lerp(rim, top, 0.5f), top, Color.white };
                        LatheColored(mb, 1, o + Vector3.up * 0.005f, Quaternion.identity, hs, rs, cols, 9);
                    }
                    glowSpots.Add(new Vector3(center.x + c.x, floorY + 0.12f, center.z + c.z));
                }
                MeshObjDiffuse("Mushrooms", mb.Build("ForestMushrooms"), new Vector3(center.x, floorY - 0.02f, center.z), new[] { stemMat, capGlowMat });
            }

            // ---- 從樹冠灑下的光束（疊加的斜長面片）
            rayMat = NewMat(add, RayTex(), 2950, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            for (int k = 0; k < 6; k++)
            {
                float a = (k + (float)rng.NextDouble() * 0.6f) / 6f * Mathf.PI * 2f, r = areaR + 3.5f + (float)rng.NextDouble() * 8f;
                float w = 1.2f + (float)rng.NextDouble() * 1.4f, h = 9f + (float)rng.NextDouble() * 3f;
                var pos = new Vector3(center.x + Mathf.Cos(a) * r, floorY, center.z + Mathf.Sin(a) * r);
                var toC = new Vector3(center.x - pos.x, 0f, center.z - pos.z).normalized;
                var go = new GameObject("LightShaft");
                go.transform.SetParent(transform, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(toC) * Quaternion.Euler(0f, 0f, 18f + (float)rng.NextDouble() * 10f);
                go.AddComponent<MeshFilter>().sharedMesh = Quad(w, h);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = rayMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // ---- 粒子：螢火蟲、花粉孢子、落葉、地面薄霧、蘑菇的光點
            fireflyMat = new Material(SpellFx.AddMat(SpellFx.TexSoft)) { name = "Forest Firefly" };
            var ff = SpellFx.NewPS(transform, "Fireflies", fireflyMat, 120, true);
            ff.transform.position = new Vector3(center.x, floorY + 1.1f, center.z);
            SpellFx.Sphere(ff, areaR + 9f);
            var ffs = ff.shape; ffs.scale = new Vector3(1f, 0.1f, 1f);   // 壓扁成一層（約 0～2m 高）
            var fm = ff.main;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            fm.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            fm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 1f, 0.25f), new Color(1f, 0.95f, 0.35f));
            SpellFx.Noise(ff, 0.55f, 0.35f);
            SpellFx.FadeInOut(ff);
            SpellFx.SizeCurve(ff, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.2f, 1f), new Keyframe(0.35f, 0.5f),
                new Keyframe(0.55f, 1f), new Keyframe(0.7f, 0.45f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.3f)));   // 一閃一閃
            SpellFx.Rate(ff, 14f);
            ff.Play();

            var spores = SpellFx.NewPS(transform, "Spores", SpellFx.BlendMat(SpellFx.TexSoft), 90, true);
            spores.transform.position = new Vector3(center.x, floorY + 1.5f, center.z);
            SpellFx.Sphere(spores, areaR + 4f);
            var sps = spores.shape; sps.scale = new Vector3(1f, 0.35f, 1f);
            var sm = spores.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.8f, 0.65f), new Color(0.85f, 1f, 0.75f, 0.5f));
            SpellFx.Rise(spores, 0.04f);
            SpellFx.Noise(spores, 0.25f, 0.3f);
            SpellFx.FadeInOut(spores);
            SpellFx.Rate(spores, 10f);
            spores.Play();

            var leaves = SpellFx.NewPS(transform, "FallingLeaves", SpellFx.BlendMat(SpellFx.TexLeaf), 40, true);
            leaves.transform.position = new Vector3(center.x, floorY + 5f, center.z);
            SpellFx.Sphere(leaves, areaR + 10f, 0.5f);
            var ls = leaves.shape; ls.scale = new Vector3(1f, 0.15f, 1f);
            var lm = leaves.main;
            lm.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
            lm.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
            lm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            lm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.5f, 0.15f, 0.9f), new Color(0.45f, 0.65f, 0.2f, 0.9f));
            SpellFx.Rise(leaves, -0.4f);
            SpellFx.Noise(leaves, 0.5f, 0.4f);
            SpellFx.Spin(leaves, 1.5f);
            SpellFx.FadeInOut(leaves);
            SpellFx.Rate(leaves, 3f);
            leaves.Play();

            var mist = SpellFx.NewPS(transform, "LowMist", SpellFx.BlendMat(SpellFx.TexSoft), 60, true);
            mist.transform.SetPositionAndRotation(new Vector3(center.x, floorY + 0.35f, center.z), Quaternion.Euler(90f, 0f, 0f));   // 圓形發射區躺平
            SpellFx.Disc(mist, areaR + 11f, 0.55f);   // 只在外圈（不遮住場地）
            var mm = mist.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(9f, 13f);
            mm.startSize = new ParticleSystem.MinMaxCurve(2f, 3.5f);
            mm.startColor = new Color(0.62f, 0.74f, 0.74f, 0.2f);
            mist.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.8f;
            SpellFx.Noise(mist, 0.15f, 0.1f);
            SpellFx.FadeInOut(mist);
            SpellFx.Rate(mist, 5f);
            mist.Play();

            mushroomGlow = SpellFx.NewPS(transform, "MushroomGlow", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            var gm = mushroomGlow.main;
            gm.startLifetime = 3f;
            gm.startSize = 0.35f;
            mushroomGlow.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.5f;
            SpellFx.FadeInOut(mushroomGlow);
            SpellFx.Rise(mushroomGlow, 0.03f);
            mushroomGlow.Play();
        }

        void Update()
        {
            clock += Time.deltaTime;
            // 樹冠隨風輕輕搖晃（以樹根為軸的極小角度旋轉）
            for (int i = 0; i < trees.Count; i++)
            {
                float t = clock * 0.7f + treePhase[i];
                trees[i].rotation = treeRot[i] * Quaternion.Euler(Mathf.Sin(t) * 0.7f, 0f, Mathf.Sin(t * 0.77f + 1.3f) * 0.6f);
            }
            // 低霧慢慢流動
            if (mistMat) mistMat.mainTextureOffset = new Vector2(clock * 0.008f, clock * 0.005f);
            // 螢火蟲整體呼吸般地明暗；光束與蘑菇也微微脈動
            if (fireflyMat)
            {
                float k = 0.42f + 0.1f * Mathf.Sin(clock * 2.1f) + 0.05f * Mathf.Sin(clock * 5.3f);
                fireflyMat.SetColor("_TintColor", new Color(k, k, k, 0.5f));
            }
            if (rayMat)
            {
                float k = 0.4f + 0.08f * Mathf.Sin(clock * 0.45f);
                rayMat.SetColor("_TintColor", new Color(k, k, k, 0.5f));
            }
            if (capGlowMat)
            {
                float k = 0.47f + 0.05f * Mathf.Sin(clock * 1.3f);
                capGlowMat.SetColor("_TintColor", new Color(k, k, k, 0.5f));
            }
            // 蘑菇周圍不時冒出柔和的光暈
            glowTimer -= Time.deltaTime;
            if (mushroomGlow && glowSpots.Count > 0 && glowTimer <= 0f)
            {
                glowTimer = 0.35f;
                glowIdx = (glowIdx + 7) % glowSpots.Count;
                var ep = new ParticleSystem.EmitParams
                {
                    position = glowSpots[glowIdx] + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.1f), Random.Range(-0.15f, 0.15f)),
                    startColor = glowIdx % 3 == 0 ? new Color(0.6f, 0.35f, 1f, 0.6f) : new Color(0.25f, 0.9f, 1f, 0.6f),
                    startSize = Random.Range(0.25f, 0.45f),
                    startLifetime = Random.Range(2.5f, 4f),
                    applyShapeToPosition = false
                };
                mushroomGlow.Emit(ep, 1);
            }
        }

        // ================================================================ 小工具：材質與網格
        static Material NewMat(Shader sh, Texture2D tex, int queue, Color tint)
        {
            var m = new Material(sh) { mainTexture = tex, renderQueue = queue, name = "Forest " + tex.name };
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

        void MeshObjDiffuse(string name, Mesh mesh, Vector3 pos, Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>場地多邊形（由中心扇形三角化）；UV＝地板座標 ÷ tile 公尺</summary>
        static Mesh PolyMesh(List<Vector3> poly, Vector3 center, float tile)
        {
            int n = poly.Count;
            var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var tri = new int[n * 3];
            v[0] = center; uv[0] = new Vector2(center.x, center.z) / tile;
            for (int i = 0; i < n; i++) { v[i + 1] = new Vector3(poly[i].x, center.y, poly[i].z); uv[i + 1] = new Vector2(poly[i].x, poly[i].z) / tile; }
            for (int i = 0; i < n; i++) { tri[i * 3] = 0; tri[i * 3 + 1] = 1 + (i + 1) % n; tri[i * 3 + 2] = 1 + i; }
            var m = new Mesh { name = "ForestFloor", vertices = v, uv = uv, triangles = tri };
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
            var m = new Mesh { name = "ForestDisc", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>水平圓環（朝上）；UV＝平面座標 ÷ tile；頂點色的 alpha 在內外緣漸隱</summary>
        static Mesh Annulus(float r0, float r1, int seg, float tile)
        {
            const int rings = 4;
            var v = new Vector3[(seg + 1) * rings]; var uv = new Vector2[v.Length]; var col = new Color[v.Length];
            var tri = new int[seg * (rings - 1) * 6];
            for (int j = 0; j < rings; j++)
            {
                float t = j / (float)(rings - 1), r = Mathf.Lerp(r0, r1, t);
                float a01 = (j == 0 || j == rings - 1) ? 0f : 1f;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    int k = j * (seg + 1) + i;
                    v[k] = p; uv[k] = new Vector2(p.x, p.z) / tile; col[k] = new Color(1f, 1f, 1f, a01);
                }
            }
            int n = 0;
            for (int j = 0; j < rings - 1; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = j * (seg + 1) + i, b = a + 1, c = a + seg + 1, d = c + 1;
                    tri[n++] = a; tri[n++] = c; tri[n++] = b;
                    tri[n++] = b; tri[n++] = c; tri[n++] = d;
                }
            var m = new Mesh { name = "ForestMist", vertices = v, uv = uv, colors = col, triangles = tri };
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
            var m = new Mesh { name = "ForestBackdrop", vertices = v, uv = uv, triangles = tri };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>直立長方形面片（底邊在原點，往 +Y 長；粒子材質雙面顯示）</summary>
        static Mesh Quad(float w, float h)
        {
            var m = new Mesh
            {
                name = "ForestShaft",
                vertices = new[] { new Vector3(-w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, 0f, 0f), new Vector3(-w * 0.5f, h, 0f), new Vector3(w * 0.5f, h, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            m.RecalculateBounds();
            return m;
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

        /// <summary>旋轉體（沿本地 +Y 的一圈圈截面），可加頂蓋／底蓋；u 沿圓周、v＝高度</summary>
        static void Lathe(MB mb, int sub, Vector3 pos, Quaternion rot, float[] hs, float[] rs, int sides, bool capTop, bool capBottom, Color col)
        {
            var cols = new Color[hs.Length];
            for (int i = 0; i < cols.Length; i++) cols[i] = col;
            LatheCore(mb, sub, pos, rot, hs, rs, cols, sides, capTop, capBottom, null);
        }

        static void LatheColored(MB mb, int sub, Vector3 pos, Quaternion rot, float[] hs, float[] rs, Color[] cols, int sides)
            => LatheCore(mb, sub, pos, rot, hs, rs, cols, sides, false, false, null);

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

        /// <summary>凹凸的橢球（樹冠、灌木的一團葉子）；凹凸用 θ 的週期函數，接縫不會裂開</summary>
        static void Blob(MB mb, int sub, Vector3 c, Vector3 rad, int lon, int lat, int seed, Color col)
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
                    float jit = 1f + 0.13f * Mathf.Sin(3f * th + p1) * Mathf.Sin(2f * ph + p2) + 0.08f * Mathf.Sin(5f * th + p3) * sp;
                    var d = new Vector3(ct * sp, cp, st * sp);
                    var p = new Vector3(d.x * rad.x, d.y * rad.y, d.z * rad.z) * jit;
                    var n = new Vector3(d.x / rad.x, d.y / rad.y, d.z / rad.z).normalized;
                    mb.Add(c + p, n, new Vector2(s / (float)lon * 2f, j / (float)lat), col);
                }
            }
            for (int j = 0; j < lat; j++)
                for (int s = 0; s < lon; s++)
                {
                    int a = start + j * row + s, b = a + 1, cc = a + row, d = cc + 1;
                    mb.Tri(sub, a, b, cc); mb.Tri(sub, b, d, cc);
                }
        }

        /// <summary>闊葉樹：微彎、根部外擴的樹幹＋幾根粗枝＋一大團一團的樹冠（子網格 0＝樹皮、1＝葉）</summary>
        static Mesh TreeMesh(float h, float spread, int seed)
        {
            var rng = new System.Random(seed);
            var mb = new MB(2);
            float trunkH = h * (0.55f + 0.1f * (float)rng.NextDouble()), tr = 0.14f + h * 0.02f;
            const int rings = 7;
            var hs = new float[rings]; var rs = new float[rings]; var bend = new Vector3[rings]; var cols = new Color[rings];
            float bx = ((float)rng.NextDouble() - 0.5f) * 0.6f, bz = ((float)rng.NextDouble() - 0.5f) * 0.6f;
            for (int i = 0; i < rings; i++)
            {
                float t = i / (float)(rings - 1);
                hs[i] = t * trunkH;
                rs[i] = tr * (1f - 0.55f * t) * (1f + 0.9f * Mathf.Exp(-t * 14f));   // 根部外擴
                bend[i] = new Vector3(bx, 0f, bz) * Mathf.Sin(t * Mathf.PI * 0.8f) * Mathf.Min(1f, spread * 0.5f);
                cols[i] = Color.white;
            }
            LatheCore(mb, 0, Vector3.zero, Quaternion.identity, hs, rs, cols, 8, true, false, bend);
            var top = bend[rings - 1] + Vector3.up * trunkH;

            // 粗枝：從樹幹上段斜伸出去
            int nb = 2 + rng.Next(2);
            var tips = new List<Vector3>();
            for (int b = 0; b < nb; b++)
            {
                float a = (b + (float)rng.NextDouble() * 0.5f) / nb * Mathf.PI * 2f, len = Mathf.Min(spread * 0.6f, 1.2f) + 0.3f;
                float y0 = trunkH * (0.7f + 0.15f * (float)rng.NextDouble());
                var from = bend[Mathf.RoundToInt(y0 / trunkH * (rings - 1))] + Vector3.up * y0;
                var dir = new Vector3(Mathf.Cos(a) * 0.75f, 0.65f, Mathf.Sin(a) * 0.75f).normalized;
                Lathe(mb, 0, from, Quaternion.FromToRotation(Vector3.up, dir), new[] { 0f, len }, new[] { tr * 0.4f, tr * 0.18f }, 5, false, false, Color.white);
                tips.Add(from + dir * len);
            }

            // 樹冠：幾團凹凸的橢球；水平範圍（離樹軸距離＋半徑）不超過 spread
            int nBlobs = 6 + rng.Next(4);
            for (int k = 0; k < nBlobs; k++)
            {
                float br = Mathf.Min(spread * 0.62f, (0.7f + 0.35f * h / 8f) * (0.75f + 0.45f * (float)rng.NextDouble()));
                Vector3 c;
                if (k < tips.Count) c = tips[k];
                else
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = (float)rng.NextDouble() * spread * 0.55f;
                    c = top + new Vector3(Mathf.Cos(a) * d, ((float)rng.NextDouble() - 0.2f) * h * 0.22f, Mathf.Sin(a) * d);
                }
                float horiz = new Vector2(c.x, c.z).magnitude;
                if (horiz + br > spread) { float s = Mathf.Max(0f, spread - br) / Mathf.Max(0.001f, horiz); c.x *= s; c.z *= s; }
                Blob(mb, 1, c, new Vector3(br, br * 0.82f, br), 9, 6, seed * 31 + k, Color.white);
            }
            return mb.Build("ForestTree");
        }

        /// <summary>針葉樹：細長樹幹＋一層層下垂的圓錐（子網格 0＝樹皮、1＝針葉）</summary>
        static Mesh PineMesh(float h, float spread, int seed)
        {
            var rng = new System.Random(seed);
            var mb = new MB(2);
            float tr = 0.1f + h * 0.012f;
            Lathe(mb, 0, Vector3.zero, Quaternion.identity, new[] { 0f, 0.25f, h * 0.5f, h * 0.92f }, new[] { tr * 1.8f, tr, tr * 0.7f, tr * 0.25f }, 7, false, false, Color.white);
            int tiers = 4 + rng.Next(3);
            float baseY = h * (0.22f + 0.08f * (float)rng.NextDouble());
            for (int k = 0; k < tiers; k++)
            {
                float t = k / (float)tiers;
                float y = Mathf.Lerp(baseY, h * 0.86f, t);
                float r = Mathf.Lerp(spread, spread * 0.22f, t) * (0.9f + 0.15f * (float)rng.NextDouble());
                float th = (h - baseY) / tiers * 1.9f;
                // 每層：邊緣下垂的圓錐（四圈截面）＋底面
                Lathe(mb, 1, new Vector3(0f, y, 0f), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f),
                      new[] { -th * 0.12f, 0f, th * 0.5f, th }, new[] { r, r * 0.92f, r * 0.45f, 0.02f }, 10, false, true, Color.white);
            }
            return mb.Build("ForestPine");
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
        static int Wrap(int i, int n) => ((i % n) + n) % n;

        static readonly Color[] LeafPalette =
        {
            new Color(0.62f, 0.3f, 0.09f), new Color(0.72f, 0.48f, 0.13f), new Color(0.48f, 0.34f, 0.14f),
            new Color(0.4f, 0.2f, 0.09f), new Color(0.58f, 0.55f, 0.18f), new Color(0.3f, 0.36f, 0.12f)
        };

        /// <summary>可平鋪的落葉散佈：每格 0～2 片葉子（尖橢圓＋葉脈），回傳覆蓋度與顏色</summary>
        static float ScatterLeaves(float u, float v, int cells, int seed, float density, float sMin, float sMax, out Color col)
        {
            float x = u * cells, y = v * cells; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 0f, bestPrio = -1f; col = Color.black;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    for (int k = 0; k < 2; k++)
                    {
                        int s = seed + k * 101;
                        if (Hash(wx, wy, s) > density) continue;
                        float px = cx + Hash(wx, wy, s + 1), py = cy + Hash(wx, wy, s + 2);
                        float ang = Hash(wx, wy, s + 3) * Mathf.PI, L = Mathf.Lerp(sMin, sMax, Hash(wx, wy, s + 4));
                        float dx = x - px, dy = y - py, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                        float a = dx * ca + dy * sa, b = -dx * sa + dy * ca, t = a / L;
                        if (Mathf.Abs(t) >= 1.15f) continue;
                        float half = L * 0.42f * Mathf.Max(0f, 1f - t * t) * (1f + 0.25f * t);
                        float edge = Mathf.Clamp01((half - Mathf.Abs(b)) / 0.035f);
                        float stem = (t < -0.95f && t > -1.15f) ? Mathf.Clamp01((0.025f - Mathf.Abs(b)) / 0.015f) : 0f;
                        float cov = Mathf.Max(edge, stem);
                        float prio = Hash(wx, wy, s + 7);
                        if (cov > 0.01f && prio > bestPrio)
                        {
                            bestPrio = prio; best = cov;
                            var c = LeafPalette[Mathf.FloorToInt(Hash(wx, wy, s + 5) * LeafPalette.Length) % LeafPalette.Length];
                            float shade = 0.75f + 0.35f * Hash(wx, wy, s + 6);
                            float vein = 1f - 0.35f * Mathf.Exp(-b * b / 0.0004f) * (1f - Mathf.Abs(t));
                            float side = 1f - 0.18f * Mathf.Sign(b) * Mathf.Clamp01(Mathf.Abs(b) / Mathf.Max(0.01f, half));   // 葉片左右兩半深淺不同
                            float rim = 1f - 0.3f * (1f - SS(0f, 0.06f, half - Mathf.Abs(b)));
                            col = c * (shade * vein * side * rim);
                        }
                    }
                }
            return best;
        }

        /// <summary>可平鋪的小草叢：從中心放射的細葉片，回傳覆蓋度與亮度（0＝根部、1＝葉尖）</summary>
        static float ScatterGrass(float u, float v, int cells, int seed, float density, out float tip)
        {
            float x = u * cells, y = v * cells; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 0f; tip = 0f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    if (Hash(wx, wy, seed) > density) continue;
                    float px = cx + 0.2f + 0.6f * Hash(wx, wy, seed + 1), py = cy + 0.2f + 0.6f * Hash(wx, wy, seed + 2);
                    float R = 0.35f + 0.35f * Hash(wx, wy, seed + 3);
                    float dx = x - px, dy = y - py, r = Mathf.Sqrt(dx * dx + dy * dy) / R;
                    if (r >= 1f) continue;
                    float th = Mathf.Atan2(dy, dx);
                    int nBl = 5 + Mathf.FloorToInt(Hash(wx, wy, seed + 4) * 5f);
                    float ph = Hash(wx, wy, seed + 5) * 6.28f;
                    // 葉片：角度方向的窄峰，越往外越細，稍微彎曲
                    float spoke = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(nBl * th + ph + r * 1.2f), 6f + r * 20f);
                    float cov = Mathf.Clamp01(spoke * 1.6f * (1f - r * r)) + Mathf.Clamp01(1f - r / 0.18f) * 0.8f;
                    if (cov > best) { best = Mathf.Clamp01(cov); tip = r; }
                }
            return best;
        }

        /// <summary>可平鋪的細樹枝（線段）</summary>
        static float ScatterTwigs(float u, float v, int cells, int seed, float density)
        {
            float x = u * cells, y = v * cells; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float best = 0f;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = xi + i, cy = yi + j, wx = Wrap(cx, cells), wy = Wrap(cy, cells);
                    if (Hash(wx, wy, seed) > density) continue;
                    float px = cx + Hash(wx, wy, seed + 1), py = cy + Hash(wx, wy, seed + 2);
                    float ang = Hash(wx, wy, seed + 3) * Mathf.PI, len = 0.4f + 0.5f * Hash(wx, wy, seed + 4);
                    float ex = Mathf.Cos(ang) * len, ey = Mathf.Sin(ang) * len;
                    float dx = x - px, dy = y - py;
                    float t = Mathf.Clamp((dx * ex + dy * ey) / (len * len), -1f, 1f);
                    float bx = dx - ex * t, by = dy - ey * t;
                    // 稍微彎曲：沿長度方向偏移
                    float d = Mathf.Sqrt(bx * bx + by * by) - 0.04f * Mathf.Sin(t * 3f + ang);
                    float w = 0.022f * (1f - 0.5f * Mathf.Abs(t));
                    best = Mathf.Max(best, Mathf.Clamp01((w - Mathf.Abs(d)) / 0.012f));
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

        // ---------------------------------------------------------------- 場地內：青苔地面
        static Texture2D MossFloorTex() { const int S = 256; return Finish(NewTex("MossFloor", S, S, true, true), MossFloorPixels()); }

        public static Color32[] MossFloorPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    // 泥土：深棕色，帶細碎顆粒與小石子
                    float n = Fbm(u, v, 3, 4, 5), grain = Fbm(u, v, 5, 32, 2);
                    var soil = new Color(0.2f, 0.135f, 0.085f) * (0.65f + 0.5f * n) * (0.85f + 0.3f * grain);
                    float pebble = SS(0.78f, 0.84f, Fbm(u, v, 77, 16, 2));
                    soil = Color.Lerp(soil, new Color(0.34f, 0.31f, 0.27f) * (0.7f + 0.5f * grain), pebble * 0.8f);
                    // 青苔：大塊區域，邊緣毛茸茸，內部有細緻的明暗
                    float mossN = Fbm(u + 0.08f * Fbm(u, v, 9, 8, 2), v, 7, 3, 5);
                    float fuzz = Fbm(u, v, 11, 32, 3);
                    float moss = SS(0.46f, 0.54f, mossN + (fuzz - 0.5f) * 0.12f);
                    float deep = SS(0.52f, 0.7f, mossN);
                    var mossCol = Color.Lerp(new Color(0.15f, 0.27f, 0.07f), new Color(0.32f, 0.48f, 0.12f), SS(0.3f, 0.7f, fuzz));
                    mossCol = Color.Lerp(mossCol, new Color(0.15f, 0.3f, 0.09f), deep * 0.5f);   // 厚青苔中心較深
                    mossCol *= 0.7f + 0.55f * Fbm(u, v, 13, 64, 2);   // 苔蘚的細點
                    var c = Color.Lerp(soil, mossCol, moss);
                    // 青苔邊緣偏黃綠
                    float rim = moss * (1f - moss) * 4f;
                    c = Color.Lerp(c, new Color(0.45f, 0.5f, 0.15f), rim * 0.35f);

                    // 細樹枝
                    float twig = ScatterTwigs(u, v, 5, 401, 0.45f) * (1f - 0.7f * moss);
                    c = Color.Lerp(c, new Color(0.3f, 0.21f, 0.13f), twig * 0.9f);

                    // 落葉（青苔厚的地方比較少）
                    float leaf = ScatterLeaves(u, v, 8, 201, 0.55f, 0.28f, 0.42f, out var lc);
                    leaf *= 1f - 0.65f * deep;
                    // 葉子下的淡影子
                    float shadow = ScatterLeaves(u - 0.006f, v + 0.006f, 8, 201, 0.55f, 0.28f, 0.42f, out _) * (1f - 0.65f * deep);
                    c *= 1f - 0.35f * shadow * (1f - leaf);
                    c = Color.Lerp(c, lc, leaf);

                    // 小草叢（多長在青苔邊緣與泥土上）
                    float grass = ScatterGrass(u, v, 10, 301, 0.42f, out float tip) * (0.4f + 0.6f * (1f - deep));
                    var gc = Color.Lerp(new Color(0.12f, 0.24f, 0.05f), new Color(0.5f, 0.68f, 0.18f), tip);
                    c = Color.Lerp(c, gc, grass * 0.95f);

                    c.a = 0.92f;
                    px[y * S + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 場地外：較暗的森林地面
        static Texture2D UndergrowthTex() { const int S = 256; return Finish(NewTex("Undergrowth", S, S, true, true), UndergrowthPixels()); }

        public static Color32[] UndergrowthPixels()
        {
            const int S = 256; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u, v, 23, 4, 5), grain = Fbm(u, v, 25, 32, 2);
                    var c = new Color(0.13f, 0.09f, 0.06f) * (0.6f + 0.6f * n) * (0.85f + 0.3f * grain);
                    float mossN = Fbm(u, v, 27, 3, 4);
                    float moss = SS(0.48f, 0.6f, mossN + (grain - 0.5f) * 0.1f);
                    c = Color.Lerp(c, new Color(0.11f, 0.2f, 0.07f) * (0.8f + 0.4f * Fbm(u, v, 29, 32, 2)), moss * 0.85f);
                    // 厚厚一層深色落葉
                    float leaf = ScatterLeaves(u, v, 10, 501, 0.8f, 0.3f, 0.45f, out var lc);
                    float leaf2 = ScatterLeaves(u, v, 7, 601, 0.5f, 0.3f, 0.45f, out var lc2);
                    c = Color.Lerp(c, lc * 0.6f, leaf * (1f - 0.6f * moss));
                    c = Color.Lerp(c, lc2 * 0.75f, leaf2 * (1f - 0.5f * moss));
                    float twig = ScatterTwigs(u, v, 4, 701, 0.5f);
                    c = Color.Lerp(c, new Color(0.22f, 0.15f, 0.1f), twig * 0.8f);
                    // 蕨類／雜草的暗綠斑塊
                    float grass = ScatterGrass(u, v, 6, 801, 0.5f, out float tip);
                    c = Color.Lerp(c, Color.Lerp(new Color(0.06f, 0.14f, 0.05f), new Color(0.25f, 0.4f, 0.12f), tip), grass * 0.85f);
                    c.a = 0.95f;
                    px[y * S + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 樹皮
        static Texture2D BarkTex() { const int S = 128; return Finish(NewTex("Bark", S, S, true, true), BarkPixels()); }

        public static Color32[] BarkPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    // 縱向的樹皮：一條條粗細不一的隆起樹皮板，被 v 方向雜訊扭曲；板與板之間是深溝
                    float warp = (Fbm(u, v, 31, 2, 3) - 0.5f) * 0.9f + (Fbm(u, v, 33, 4, 2) - 0.5f) * 0.3f;
                    float cell = (u + warp * 0.18f) * 6f, ci = Mathf.Floor(cell), f = cell - ci;
                    float wobble = 0.15f * (Fbm(u, v, 35, 8, 2) - 0.5f);
                    float groove = 1f - SS(0.04f, 0.2f + wobble, Mathf.Min(f, 1f - f));
                    float plate = Fbm(u, v, 37, 8, 4);
                    float round = Mathf.Sin(Mathf.Clamp01(f) * Mathf.PI);   // 板子中間鼓起、比較亮
                    float lum = (0.45f + 0.35f * plate + 0.25f * round) * (1f - 0.8f * groove);
                    // 橫向的小裂痕
                    lum *= 1f - 0.35f * SS(0.72f, 0.82f, Fbm(u * 0.5f, v * 3f, 39, 8, 2)) * (1f - groove);
                    var c = Color.Lerp(new Color(0.36f, 0.27f, 0.2f), new Color(0.48f, 0.42f, 0.36f), plate) * lum;
                    // 溝裡的苔蘚
                    float moss = SS(0.5f, 0.65f, Fbm(u, v, 41, 4, 4)) * (0.3f + 0.7f * groove);
                    c = Color.Lerp(c, new Color(0.2f, 0.34f, 0.1f) * (0.7f + 0.5f * plate), moss * 0.8f);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 樹葉（給樹冠、灌木）
        static Texture2D LeafTex() { const int S = 128; return Finish(NewTex("Leaves", S, S, true, true), LeafPixels()); }

        public static Color32[] LeafPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float clump = Fbm(u, v, 41, 4, 4);
                    var c = Color.Lerp(new Color(0.12f, 0.26f, 0.1f), new Color(0.32f, 0.5f, 0.2f), clump);
                    // 一片片葉子：亮面與陰影
                    float leaf = ScatterLeaves(u, v, 10, 43, 0.95f, 0.3f, 0.45f, out var lc);
                    float lum = (lc.r + lc.g + lc.b) / 3f;
                    var leafCol = Color.Lerp(new Color(0.22f, 0.42f, 0.14f), new Color(0.55f, 0.75f, 0.28f), SS(0.15f, 0.45f, lum) * (0.5f + 0.5f * clump));
                    c = Color.Lerp(c, leafCol, leaf * 0.85f);
                    // 葉簇之間的深色縫隙
                    c *= 0.6f + 0.55f * SS(0.3f, 0.6f, clump);
                    c.a = 1f;
                    px[y * S + x] = c;
                }
            return px;
        }

        // ---------------------------------------------------------------- 低霧
        static Texture2D MistTex() { const int S = 128; return Finish(NewTex("Mist", S, S, true, true), MistPixels()); }

        public static Color32[] MistPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float n = Fbm(u + 0.15f * Fbm(u, v, 53, 2, 2), v, 51, 2, 5);
                    float a = SS(0.38f, 0.75f, n) * 0.55f;
                    px[y * S + x] = new Color(0.72f, 0.82f, 0.82f, a);
                }
            return px;
        }

        // ---------------------------------------------------------------- 光束（疊加用：中間亮、兩側與兩端柔和淡出）
        static Texture2D RayTex() { const int W = 64, H = 128; return Finish(NewTex("Ray", W, H, false, false), RayPixels()); }

        public static Color32[] RayPixels()
        {
            const int W = 64, H = 128; var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                    float across = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.22f, 2f));
                    float streak = 0.75f + 0.25f * Mathf.Sin(u * 37f) * Mathf.Sin(u * 13f + 1f);
                    float along = SS(0f, 0.35f, v) * (1f - SS(0.75f, 1f, v));
                    float a = across * along * streak * 0.55f;
                    px[y * W + x] = new Color(0.95f, 1f, 0.8f, a);
                }
            return px;
        }

        // ---------------------------------------------------------------- 遠方背景
        struct BgTree { public float u, h, w; public bool round; public float seed; }

        /// <summary>背景：好幾層由遠到近的樹林剪影（越遠越淡、越藍），層與層之間夾著地面霧氣，上方是霧濛濛的天空與斜射的光束（u 可平鋪）</summary>
        static Texture2D BackdropTex() { const int W = 1024, H = 256; return Finish(NewTex("Backdrop", W, H, true, false), BackdropPixels()); }

        public static Color32[] BackdropPixels()
        {
            const int W = 1024, H = 256; var px = new Color32[W * H];
            // 每一層：地面高度、樹高範圍、寬度範圍、數量、顏色、闊葉樹比例
            var layerBase = new[] { 0.16f, 0.13f, 0.1f, 0.07f };
            var layerHMin = new[] { 0.18f, 0.24f, 0.3f, 0.36f };
            var layerHMax = new[] { 0.34f, 0.42f, 0.5f, 0.6f };
            var layerW = new[] { 0.008f, 0.011f, 0.015f, 0.02f };
            var layerN = new[] { 90, 60, 38, 20 };
            var layerRound = new[] { 0.2f, 0.3f, 0.35f, 0.4f };
            var layerCol = new[] { new Color(0.27f, 0.38f, 0.4f), new Color(0.17f, 0.27f, 0.29f), new Color(0.1f, 0.17f, 0.18f), new Color(0.05f, 0.095f, 0.085f) };
            var fogCol = new Color(0.45f, 0.56f, 0.55f);
            var layers = new List<BgTree>[4];
            for (int l = 0; l < 4; l++)
            {
                layers[l] = new List<BgTree>();
                for (int i = 0; i < layerN[l]; i++)
                {
                    float r1 = Hash(i, l, 901), r2 = Hash(i, l, 902), r3 = Hash(i, l, 903), r4 = Hash(i, l, 904);
                    layers[l].Add(new BgTree
                    {
                        u = (i + r1 * 0.9f) / layerN[l],
                        h = Mathf.Lerp(layerHMin[l], layerHMax[l], r2 * r2),
                        w = layerW[l] * (0.7f + 0.6f * r3),
                        round = r4 < layerRound[l],
                        seed = r1 * 100f
                    });
                }
            }
            // 光束：從天上斜斜往下
            var shaftU = new[] { 0.07f, 0.21f, 0.38f, 0.55f, 0.71f, 0.88f };

            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                for (int y = 0; y < H; y++)
                {
                    float v = y / (float)H;
                    // 天空：地平線附近是淡青色的霧，往上變成深藍綠
                    float cloud = Fbm(u * 2f, v * 1.5f, 61, 4, 5);
                    var c = Color.Lerp(new Color(0.5f, 0.6f, 0.58f), new Color(0.08f, 0.13f, 0.16f), SS(0.2f, 1f, v));
                    c = Color.Lerp(c, new Color(0.55f, 0.62f, 0.6f), SS(0.45f, 0.8f, cloud) * 0.35f * (1f - v));
                    float alpha = 0.88f;

                    // 光束（天空與最遠兩層上）
                    float shaft = 0f;
                    foreach (var su in shaftU)
                    {
                        float cu = su + (1f - v) * 0.05f;
                        float du = Mathf.Abs(Mathf.DeltaAngle(u * 360f, cu * 360f)) / 360f;
                        float wd = 0.006f + 0.01f * (1f - v);
                        shaft += Mathf.Exp(-du * du / (wd * wd)) * (0.5f + 0.5f * Fbm(u * 4f, 0.2f, 63 + (int)(su * 100f), 8, 2));
                    }
                    shaft = Mathf.Clamp01(shaft) * SS(0.05f, 0.5f, v) * (1f - SS(0.85f, 1f, v));

                    for (int l = 0; l < 4; l++)
                    {
                        if (l == 2) c = Color.Lerp(c, new Color(0.85f, 0.9f, 0.72f), shaft * 0.35f);   // 光束在第二層後面
                        float b = layerBase[l] + 0.02f * (Fbm(u, 0.5f, 70 + l, 6, 3) - 0.5f);
                        bool inside = v < b;
                        float edge = 1f;
                        if (!inside)
                        {
                            foreach (var t in layers[l])
                            {
                                float du = Mathf.DeltaAngle(u * 360f, t.u * 360f) / 360f;
                                if (Mathf.Abs(du) > t.w * 2.2f) continue;
                                float tv = (v - b) / t.h;
                                if (tv < 0f || tv > 1.05f) continue;
                                float ad = Mathf.Abs(du);
                                if (t.round)
                                {
                                    // 闊葉樹：樹幹＋凹凸的圓樹冠
                                    float cy = 0.7f, ry = 0.3f, rx = t.w * 1.45f;
                                    float dy = (tv - cy) / ry, dx = du / rx;
                                    float ang = Mathf.Atan2(dy, dx);
                                    float bump = 1f + 0.12f * Mathf.Sin(ang * 5f + t.seed) + 0.06f * Mathf.Sin(ang * 11f + t.seed * 2f);
                                    if (dx * dx + dy * dy < bump * bump || (ad < t.w * 0.18f && tv < cy)) { inside = true; break; }
                                }
                                else
                                {
                                    // 針葉樹：一層層下垂的鋸齒
                                    float tiers = 5f + (t.seed % 3f);
                                    float s = tv * tiers - Mathf.Floor(tv * tiers);
                                    float half = t.w * (1f - tv) * (0.45f + 0.55f * (1f - s) * (1f - s)) + t.w * 0.03f;
                                    if (ad < half || (ad < t.w * 0.12f && tv < 0.2f)) { inside = true; break; }
                                }
                            }
                        }
                        if (inside)
                        {
                            float tex = Fbm(u * 3f, v * 2f, 80 + l, 16, 2);
                            var lc = layerCol[l] * (0.9f + 0.2f * tex);
                            // 越往下霧越濃（樹根埋在霧裡）
                            float fog = (1f - SS(b - 0.04f, b + 0.12f + 0.04f * l, v)) * (0.55f - 0.1f * l);
                            lc = Color.Lerp(lc, fogCol, Mathf.Max(0f, fog));
                            c = lc;
                            alpha = 0.95f * edge;
                        }
                        // 每層前面的地面霧帶
                        float band = Mathf.Exp(-Mathf.Pow((v - (b - 0.01f)) / 0.035f, 2f)) * Fbm(u * 2f, v, 90 + l, 6, 3);
                        c = Color.Lerp(c, fogCol, band * (0.5f - 0.08f * l));
                    }
                    px[y * W + x] = new Color(c.r, c.g, c.b, alpha);
                }
            }
            return px;
        }

        /// <summary>頭頂的樹冠頂蓋：深綠色葉層，中間有幾處透光的縫隙（徑向）</summary>
        static Texture2D CapTex() { const int S = 128; return Finish(NewTex("CanopyCap", S, S, false, false), CapPixels()); }

        public static Color32[] CapPixels()
        {
            const int S = 128; var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S, v = y / (float)S;
                    float r = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    float leaves = Fbm(u, v, 71, 6, 5);
                    var c = Color.Lerp(new Color(0.03f, 0.07f, 0.05f), new Color(0.1f, 0.2f, 0.12f), leaves);
                    float gap = SS(0.56f, 0.66f, Fbm(u, v, 73, 8, 4)) * (1f - SS(0.5f, 0.95f, r));
                    c = Color.Lerp(c, new Color(0.55f, 0.65f, 0.55f), gap * 0.8f);
                    // 外圈接上背景頂端的顏色
                    c = Color.Lerp(c, new Color(0.07f, 0.12f, 0.15f), SS(0.75f, 1f, r));
                    c.a = 0.93f;
                    px[y * S + x] = c;
                }
            return px;
        }
    }
}
