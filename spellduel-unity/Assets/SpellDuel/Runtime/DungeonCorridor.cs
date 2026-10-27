using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本的「連續場景」（使用者要求）：每個地點的場地前方都看得到一條通道（地板、兩側石柱／樹幹／礁石、懸掛的燈火、盡頭的黑霧），
    /// 入口被一道發光的結界擋住（清完這一波才打開）；清完後 SoloBattle.DungeonTravel 把整個場景往後推（3D 運鏡），
    /// 玩家就像沿著通道走到下一個場景。全部用 AR 座標（和 VolcanoScene 等場景一樣），掛在同一個根物件下就能一起移動。
    /// </summary>
    public class DungeonCorridor : MonoBehaviour
    {
        public const float Length = 14f, Width = 3.2f;
        /// <summary>通道入口（場地邊緣上的點）與方向（AR 座標、水平）</summary>
        public Vector3 Start { get; private set; }
        public Vector3 Dir { get; private set; }
        public bool Open { get; private set; }
        GameObject gate; Material gateMat; float gateOpenAt = -1f; Color gateColor; Vector3 gateHome;
        readonly List<Light> lamps = new List<Light>();
        float clock;

        public static DungeonCorridor Create(List<Vector3> poly, float floorY, Vector3 dir, int theme)
        {
            var go = new GameObject("DungeonCorridor");
            var c = go.AddComponent<DungeonCorridor>();
            c.Build(poly, floorY, dir, theme);
            c.variantTheme=theme;for(int v=0;v<3;v++)SceneVariantArt.Floor(theme,v);
            c.variantRenderers=c.GetComponentsInChildren<Renderer>();
            return c;
        }

        int variantTheme;
        Renderer[] variantRenderers;
        public void SetVariant(int variant)
        {
            var tint=SceneVariantArt.Palette(variantTheme,variant);
            foreach(var r in variantRenderers)
            {
                if(r.gameObject.name=="CorridorFloor")r.sharedMaterial.mainTexture=SceneVariantArt.Floor(variantTheme,variant);
                else if(r.gameObject.name=="CorridorWall"||r.gameObject.name=="CorridorPillar"||r.gameObject.name=="CorridorBeam")
                {var b=new MaterialPropertyBlock();b.SetColor("_Color",tint);r.SetPropertyBlock(b);}
            }
        }
        // ---------------------------------------------------------------- 主題色
        struct Palette { public Color wall, wallDark, floorTint, glow, fog; public Texture2D floor; }
        static Palette PaletteOf(int theme)
        {
            switch (theme)
            {
                case 1: return new Palette { wall = new Color(0.22f, 0.16f, 0.1f), wallDark = new Color(0.12f, 0.1f, 0.07f), floorTint = new Color(0.5f, 0.5f, 0.5f, 0.5f), glow = new Color(0.55f, 1f, 0.5f), fog = new Color(0.03f, 0.06f, 0.03f), floor = Tex("CorridorMoss", ForestScene.MossFloorPixels()) };
                case 2: return new Palette { wall = new Color(0.16f, 0.22f, 0.26f), wallDark = new Color(0.08f, 0.12f, 0.15f), floorTint = new Color(0.5f, 0.5f, 0.5f, 0.5f), glow = new Color(0.45f, 0.85f, 1f), fog = new Color(0.02f, 0.05f, 0.08f), floor = Tex("CorridorReef", SeaScene.ReefFloorPixels()) };
                case Battle.ArenaTheme: return new Palette { wall = new Color(0.42f, 0.36f, 0.28f), wallDark = new Color(0.22f, 0.18f, 0.14f), floorTint = new Color(0.5f, 0.5f, 0.5f, 0.5f), glow = new Color(1f, 0.75f, 0.4f), fog = new Color(0.05f, 0.04f, 0.03f), floor = ArenaTex.Stone() };
                case Battle.CanyonTheme: return new Palette { wall = new Color(0.5f, 0.38f, 0.22f), wallDark = new Color(0.28f, 0.2f, 0.12f), floorTint = new Color(0.5f, 0.5f, 0.5f, 0.5f), glow = new Color(1f, 0.85f, 0.5f), fog = new Color(0.08f, 0.06f, 0.03f), floor = CanyonTex.Loess() };
                default: return new Palette { wall = new Color(0.14f, 0.1f, 0.09f), wallDark = new Color(0.07f, 0.05f, 0.05f), floorTint = new Color(0.5f, 0.5f, 0.5f, 0.5f), glow = new Color(1f, 0.55f, 0.2f), fog = new Color(0.05f, 0.02f, 0.01f), floor = Tex("CorridorBasalt", VolcanoScene.BasaltPixels()) };
            }
        }
        static Texture2D Tex(string name, Color32[] px) { const int S = 256; return ArtKit.Finish(ArtKit.NewTex(name, S, S, true, true), px); }

        // ---------------------------------------------------------------- 建置
        void Build(List<Vector3> poly, float floorY, Vector3 dir, int theme)
        {
            dir.y = 0f; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward; dir.Normalize();
            var center = Vector3.zero;
            if (poly != null && poly.Count >= 3) { foreach (var p in poly) center += p; center /= poly.Count; }
            center.y = floorY;
            Start = EdgeHit(poly, center, dir, floorY);
            Dir = dir;
            var right = Vector3.Cross(Vector3.up, dir).normalized;
            var pal = PaletteOf(theme);
            var blend = Shader.Find("Legacy Shaders/Particles/Alpha Blended") ?? Shader.Find("Sprites/Default");
            var add = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            var diffuse = Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Mobile/Diffuse") ?? blend;
            var rot = Quaternion.LookRotation(dir, Vector3.up);

            // ---- 地板：從場地邊緣往外延伸 Length 公尺的長條（貼圖沿著通道重複）
            var floorMat = ArtKit.NewMat(blend, pal.floor, 2908, pal.floorTint, "Corridor");
            floorMat.mainTextureScale = new Vector2(Width / 2.5f, Length / 2.5f);
            var floor = ArtKit.MeshObj(transform, "CorridorFloor", Strip(Width, Length + 1.5f), Start + dir * ((Length + 1.5f) * 0.5f - 1.5f) + Vector3.up * 0.005f, floorMat);
            floor.transform.rotation = rot;

            // ---- 兩側：石柱／樹幹／礁石（每 2m 一根，越遠越暗）＋低矮的牆
            var wallMat = new Material(diffuse) { color = pal.wall, name = "CorridorWall" };
            var darkMat = new Material(diffuse) { color = pal.wallDark, name = "CorridorWallDark" };
            var rng = new System.Random(theme * 7 + 3);
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(wall.GetComponent<Collider>());
                wall.name = "CorridorWall";
                wall.transform.SetParent(transform, false);
                wall.transform.SetPositionAndRotation(Start + dir * (Length * 0.5f) + right * side * (Width * 0.5f + 0.35f) + Vector3.up * 0.9f, rot);
                wall.transform.localScale = new Vector3(0.7f, 1.8f, Length);
                var wr = wall.GetComponent<Renderer>(); wr.sharedMaterial = darkMat; wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; wr.receiveShadows = false;
                for (float z = 0.6f; z < Length; z += 2f)
                {
                    float h = 2.4f + (float)rng.NextDouble() * 1.2f, w = 0.35f + (float)rng.NextDouble() * 0.2f;
                    var pillar = GameObject.CreatePrimitive(theme == 1 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                    Destroy(pillar.GetComponent<Collider>());
                    pillar.name = "CorridorPillar";
                    pillar.transform.SetParent(transform, false);
                    pillar.transform.SetPositionAndRotation(Start + dir * z + right * side * (Width * 0.5f + 0.2f) + Vector3.up * (h * 0.5f), rot * Quaternion.Euler((float)rng.NextDouble() * 6f - 3f, 0f, side * ((float)rng.NextDouble() * 5f)));
                    pillar.transform.localScale = new Vector3(w, theme == 1 ? h * 0.5f : h, w);
                    var pr = pillar.GetComponent<Renderer>(); pr.sharedMaterial = wallMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
                }
            }
            // ---- 頂上的橫樑（每 4m 一根）：讓它看起來像一條廊道
            for (float z = 2f; z < Length; z += 4f)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(beam.GetComponent<Collider>());
                beam.name = "CorridorBeam";
                beam.transform.SetParent(transform, false);
                beam.transform.SetPositionAndRotation(Start + dir * z + Vector3.up * 2.9f, rot);
                beam.transform.localScale = new Vector3(Width + 1.2f, 0.3f, 0.35f);
                var br = beam.GetComponent<Renderer>(); br.sharedMaterial = darkMat; br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
            }

            // ---- 懸掛的燈火：橫樑下的發光球＋點光源（手機上最多 3 盞）
            var glowMat = new Material(add) { color = pal.glow, name = "CorridorGlow" };
            if (glowMat.HasProperty("_TintColor")) glowMat.SetColor("_TintColor", pal.glow);
            int n = 0;
            for (float z = 2f; z < Length; z += 4f, n++)
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(lamp.GetComponent<Collider>());
                lamp.name = "CorridorLamp";
                lamp.transform.SetParent(transform, false);
                lamp.transform.position = Start + dir * z + Vector3.up * 2.55f;
                lamp.transform.localScale = Vector3.one * 0.22f;
                var lr = lamp.GetComponent<Renderer>(); lr.sharedMaterial = glowMat; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (n < 3)
                {
                    var lt = lamp.AddComponent<Light>();
                    lt.type = LightType.Point; lt.color = pal.glow; lt.range = 5f; lt.intensity = 1.4f; lt.shadows = LightShadows.None;
                    lamps.Add(lt);
                }
            }

            // ---- 盡頭的黑霧：越遠越看不見（兩層漸淡的直立面）
            var fogMat = new Material(blend) { name = "CorridorFog" };
            if (fogMat.HasProperty("_TintColor")) fogMat.SetColor("_TintColor", new Color(pal.fog.r, pal.fog.g, pal.fog.b, 0.5f)); else fogMat.color = new Color(pal.fog.r, pal.fog.g, pal.fog.b, 0.9f);
            fogMat.renderQueue = 3050;
            for (int i = 0; i < 3; i++)
            {
                var fog = ArtKit.MeshObj(transform, "CorridorFog", Wall(Width + 2f, 4f), Start + dir * (Length - 2.5f + i * 1.2f) + Vector3.up * 2f, fogMat);
                fog.transform.rotation = rot;
            }

            // ---- 入口的結界：半透明發光的直立面＋兩側的門柱；清完這一波才打開（往下沉、淡出）
            gateColor = pal.glow;
            gateMat = new Material(add) { name = "CorridorGate", renderQueue = 3060 };
            gate = ArtKit.MeshObj(transform, "CorridorGate", Wall(Width + 0.4f, 2.6f), Start + dir * 0.15f + Vector3.up * 1.3f, gateMat);
            gate.transform.rotation = rot;
            gateHome = gate.transform.localPosition;
            SetGateAlpha(0.55f);
            for (int side = -1; side <= 1; side += 2)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(post.GetComponent<Collider>());
                post.name = "CorridorGatePost";
                post.transform.SetParent(transform, false);
                post.transform.SetPositionAndRotation(Start + dir * 0.15f + right * side * (Width * 0.5f + 0.3f) + Vector3.up * 1.5f, rot);
                post.transform.localScale = new Vector3(0.45f, 3f, 0.45f);
                var pr = post.GetComponent<Renderer>(); pr.sharedMaterial = wallMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
            }
        }

        /// <summary>打開結界（清完這一波）：0.8 秒內淡出、往下沉</summary>
        public void OpenGate()
        {
            if (Open) return;
            Open = true; gateOpenAt = Time.time;
        }

        void SetGateAlpha(float a)
        {
            if (!gateMat) return;
            var c = new Color(gateColor.r, gateColor.g, gateColor.b, a);
            if (gateMat.HasProperty("_TintColor")) gateMat.SetColor("_TintColor", c); else gateMat.color = c;
        }

        void Update()
        {
            clock += Time.deltaTime;
            float flick = 0.85f + 0.15f * Mathf.Sin(clock * 9f) * Mathf.Sin(clock * 3.3f);
            foreach (var l in lamps) if (l) l.intensity = 1.4f * flick;
            if (gate && gateMat)
            {
                if (!Open) SetGateAlpha(0.42f + 0.13f * Mathf.Sin(clock * 2.5f));
                else
                {
                    float k = Mathf.Clamp01((Time.time - gateOpenAt) / 0.8f);
                    SetGateAlpha(0.6f * (1f - k));
                    gate.transform.localPosition = gateHome + Vector3.up * (-2.8f * k);   // 用局部座標：運鏡時整個根物件在動
                    if (k >= 1f) { Destroy(gate); gate = null; }
                }
            }
        }

        // ---------------------------------------------------------------- 幾何
        /// <summary>從場地中心沿 dir 射出，和多邊形邊的交點（沒有就離中心 4m）</summary>
        static Vector3 EdgeHit(List<Vector3> poly, Vector3 c, Vector3 dir, float floorY)
        {
            float best = -1f;
            if (poly != null && poly.Count >= 3)
            {
                for (int i = 0; i < poly.Count; i++)
                {
                    var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                    // 解 c + dir*t = a + (b-a)*u
                    float ex = b.x - a.x, ez = b.z - a.z;
                    float den = dir.x * ez - dir.z * ex;
                    if (Mathf.Abs(den) < 1e-5f) continue;
                    float dx = a.x - c.x, dz = a.z - c.z;
                    float t = (dx * ez - dz * ex) / den, u = (dx * dir.z - dz * dir.x) / den;
                    if (t > 0f && u >= 0f && u <= 1f && (best < 0f || t < best)) best = t;
                }
            }
            if (best < 0f) best = 4f;
            var p = c + dir * best; p.y = floorY; return p;
        }

        /// <summary>躺平的長條（局部：x 寬、z 長，中心在原點，朝 +z）</summary>
        static Mesh Strip(float w, float l)
        {
            var m = new Mesh { name = "CorridorStrip" };
            m.vertices = new[] { new Vector3(-w / 2f, 0f, -l / 2f), new Vector3(w / 2f, 0f, -l / 2f), new Vector3(w / 2f, 0f, l / 2f), new Vector3(-w / 2f, 0f, l / 2f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>直立的面（局部：x 寬、y 高，中心在原點，兩面都看得到）</summary>
        static Mesh Wall(float w, float h)
        {
            var m = new Mesh { name = "CorridorWallQuad" };
            m.vertices = new[] { new Vector3(-w / 2f, -h / 2f, 0f), new Vector3(w / 2f, -h / 2f, 0f), new Vector3(w / 2f, h / 2f, 0f), new Vector3(-w / 2f, h / 2f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
