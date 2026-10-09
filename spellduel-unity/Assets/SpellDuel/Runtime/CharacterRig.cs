using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色外觀：用 Unity 基本形狀＋少量程式產生的網格拼出各職業的 Q 版人形，並用程式做簡單動作。
    ///   不需要任何模型、動畫或資源檔；所有部位都在 Create() 裡即時建立。
    ///   座標：腳底在本地 y = 0，面向本地 +Z，總高約 1.75 m（法師帽尖、法杖略高），
    ///   身體寬度大致在戰鬥判定半徑（Fighter.BodyRadius ≈ 0.28 m）內。
    ///   動作：走路（腿、手擺動＋上下起伏）、待機呼吸、詠唱（舉起武器＋腳下光圈）、
    ///   防禦（劍士舉盾，其他職業雙手交叉）、出手、受擊（閃色＋後仰）、倒地。
    ///   關節都有獨立的樞紐物件（肩、肘、手腕、髖、膝），旋轉樞紐即可讓肢體繞關節轉動。
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        // ================================================================ 對外介面
        /// <summary>移動速度（公尺/秒），驅動走路動作；0 = 待機呼吸。</summary>
        public float MoveSpeed { get; set; }
        /// <summary>詠唱進度 0..1：舉起武器、腳下出現職業色光圈；≤ 0 表示沒有在詠唱。</summary>
        public float Charge { get; set; }
        /// <summary>防禦中：劍士舉盾，其他職業雙手交叉護身。</summary>
        public bool Guarding { get; set; }

        public string ClassId => classId;

        // ================================================================ 共用材質（依顏色快取）
        static Shader litShader, glowShader;
        static bool unlit;      // 找不到有光照的 shader 時用 Sprites/Default，並以「下方、側面較暗」假裝陰影
        static readonly Dictionary<Color32, Material> matCache = new Dictionary<Color32, Material>();
        static readonly Dictionary<Color32, Material> glowCache = new Dictionary<Color32, Material>();
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        static readonly Color Skin = new Color(1f, 0.85f, 0.72f);
        static readonly Color Cloth = new Color(0.2f, 0.2f, 0.25f);
        static readonly Color DarkCloth = new Color(0.12f, 0.11f, 0.16f);
        static readonly Color Metal = new Color(0.68f, 0.71f, 0.76f);
        static readonly Color Wood = new Color(0.47f, 0.31f, 0.17f);
        static readonly Color Leather = new Color(0.4f, 0.26f, 0.15f);
        static readonly Color Gold = new Color(0.96f, 0.78f, 0.3f);
        static readonly Color EyeCol = new Color(0.1f, 0.1f, 0.16f);

        static void InitShaders()
        {
            if (glowShader != null) return;
            glowShader = Shader.Find("Sprites/Default");
            litShader = Shader.Find("Legacy Shaders/Diffuse");
            if (litShader == null) litShader = Shader.Find("Mobile/Diffuse");
            if (litShader == null) litShader = Shader.Find("Diffuse");
            unlit = litShader == null;
            if (unlit) litShader = glowShader;
        }

        /// <summary>一般部位的材質。shade：沒有光照時用來假裝陰影的亮度倍率（有光照時忽略）。</summary>
        static Material Mat(Color c, float shade = 1f)
        {
            InitShaders();
            if (unlit) c = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
            Color32 key = c;
            if (matCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(litShader) { color = c, name = "Rig " + ColorUtility.ToHtmlStringRGB(c) };
            matCache[key] = m;
            return m;
        }

        /// <summary>發光部位（不受光照，永遠是亮色）。</summary>
        static Material Glow(Color c)
        {
            InitShaders();
            Color32 key = c;
            if (glowCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(glowShader) { color = c, name = "RigGlow " + ColorUtility.ToHtmlStringRGBA(c) };
            glowCache[key] = m;
            return m;
        }

        /// <summary>有光照的 shader 需要光源；AR 場景若沒有平行光就補一盞（只補一次）。</summary>
        static void EnsureLight()
        {
            if (unlit) return;
            foreach (var l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional && l.enabled) return;
            var go = new GameObject("CharacterRig Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            if (RenderSettings.ambientLight.maxColorComponent < 0.35f)
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        // ================================================================ 骨架
        string classId;
        Color clsColor;
        Transform body, spine, neck, head;
        Transform shL, shR, elL, elR, handL, handR;
        Transform hipL, hipR, knL, knR;
        Transform cape, scarfTail, shield, orb;
        Vector3 shieldIdlePos; Quaternion shieldIdleRot;
        Vector3 orbBaseScale;
        GameObject aura; Material auraMat;

        Renderer[] rends; Material[] origMats;
        Material flashMat;
        bool visible = true, flashing;

        // 動作狀態
        float walkPhase, walkAmt, guardAmt, chargeAmt, castT = -1f, hitT, deadAmt, clock;
        bool dead;
        float legAmpScale = 1f;

        struct Arm
        {
            public float x, zOut, el, wr;
            public Arm(float x, float zOut, float el, float wr) { this.x = x; this.zOut = zOut; this.el = el; this.wr = wr; }
            public static Arm Lerp(Arm a, Arm b, float t) =>
                new Arm(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.zOut, b.zOut, t), Mathf.Lerp(a.el, b.el, t), Mathf.Lerp(a.wr, b.wr, t));
        }
        Arm baseL, baseR, guardL, guardR, chargeL, chargeR, castL, castR;
        float castMaskL, castMaskR, swingL = 1f, swingR = 1f;
        static readonly Arm Relaxed = new Arm(0f, 25f, -10f, 0f);

        const float CastDur = 0.3f, HitDur = 0.15f, DieDur = 0.6f;

        // ================================================================ 建立
        public static CharacterRig Create(string classId, Transform parent = null)
        {
            InitShaders();
            EnsureLight();
            if (string.IsNullOrEmpty(classId) || !Skills.Classes.ContainsKey(classId)) classId = "mage";
            var go = new GameObject($"Character {classId}");
            if (parent != null) go.transform.SetParent(parent, false);
            var rig = go.AddComponent<CharacterRig>();
            rig.Build(classId);
            return rig;
        }

        void Build(string id)
        {
            classId = id;
            clsColor = Skills.Classes[id].color; clsColor.a = 1f;
            bool slim = id == "assassin";
            float sw = slim ? 0.85f : 1f;

            // 各職業的配色
            Color torsoC, armC, pantsC, bootC;
            switch (id)
            {
                case "mage": torsoC = clsColor; armC = clsColor; pantsC = Cloth; bootC = Leather; break;
                case "archer": torsoC = Mul(clsColor, 0.8f); armC = Mul(clsColor, 0.8f); pantsC = Leather; bootC = Mul(Leather, 0.7f); break;
                case "assassin": torsoC = DarkCloth; armC = DarkCloth; pantsC = DarkCloth; bootC = Mul(DarkCloth, 0.8f); break;
                default: torsoC = Metal; armC = Cloth; pantsC = Cloth; bootC = Metal; break;
            }

            body = Pivot("Body", transform, Vector3.zero);

            // ---- 腿（髖 → 膝）
            hipL = Leg("L", -1f, sw, pantsC, bootC, out knL);
            hipR = Leg("R", 1f, sw, pantsC, bootC, out knR);

            // ---- 軀幹（以髖部高度為樞紐，前傾 / 後仰都繞這裡轉）
            spine = Pivot("Spine", body, new Vector3(0f, 0.85f, 0f));
            Part(PrimitiveType.Sphere, spine, new Vector3(0f, 0.03f, 0f), new Vector3(0.3f * sw, 0.18f, 0.2f), Mat(pantsC, 0.85f));
            Part(PrimitiveType.Capsule, spine, new Vector3(0f, 0.3f, 0f), new Vector3(0.34f * sw, 0.27f, 0.22f), Mat(torsoC));
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.08f, 0f), new Vector3(0.35f * sw, 0.03f, 0.24f), Mat(id == "mage" ? Gold : Leather, 0.9f));
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.56f, 0f), new Vector3(0.08f, 0.04f, 0.08f), Mat(Skin, 0.85f));

            // ---- 頭（脖子樞紐 → 頭中心）
            neck = Pivot("Neck", spine, new Vector3(0f, 0.57f, 0f));
            head = Pivot("Head", neck, new Vector3(0f, 0.14f, 0f));
            Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.28f, 0.28f, 0.28f), Mat(Skin));
            var eyeMat = id == "assassin" ? Glow(Color.Lerp(clsColor, Color.white, 0.3f)) : Mat(EyeCol);
            for (int s = -1; s <= 1; s += 2)
                Part(PrimitiveType.Sphere, head, new Vector3(0.05f * s, 0.005f, 0.13f), new Vector3(0.035f, 0.055f, 0.02f), eyeMat);

            // ---- 手臂（肩 → 肘 → 手腕）
            shL = ArmChain("L", -1f, sw, armC, out elL, out handL);
            shR = ArmChain("R", 1f, sw, armC, out elR, out handR);

            // 預設姿勢：手自然下垂
            var hang = new Arm(0f, 8f, -15f, 0f);
            baseL = baseR = hang;
            var cross = new Arm(-70f, -30f, -75f, 0f);
            guardL = guardR = cross;
            chargeL = chargeR = new Arm(-60f, 10f, -40f, 0f);
            castL = castR = new Arm(-85f, 0f, 0f, 60f);
            castMaskL = 0f; castMaskR = 1f;

            switch (id)
            {
                case "mage": BuildMage(); break;
                case "archer": BuildArcher(); break;
                case "assassin": BuildAssassin(); break;
                default: BuildSwordsman(); break;
            }

            // ---- 詠唱光圈（不跟身體倒下，固定在腳底）
            auraMat = new Material(glowShader) { color = new Color(clsColor.r, clsColor.g, clsColor.b, 0f), name = "RigAura" };
            aura = new GameObject("Aura");
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            aura.AddComponent<MeshFilter>().sharedMesh = RingMesh(0.36f, 0.45f, 40);
            var ar = aura.AddComponent<MeshRenderer>();
            ar.sharedMaterial = auraMat;
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aura.SetActive(false);

            // 受擊閃色用：記住每個部位原本的材質
            flashMat = Glow(new Color(1f, 0.55f, 0.5f));
            var all = body.GetComponentsInChildren<Renderer>(true);
            rends = all;
            origMats = new Material[all.Length];
            for (int i = 0; i < all.Length; i++) origMats[i] = all[i].sharedMaterial;

            Animate(0f);
        }

        Transform Leg(string side, float s, float sw, Color pantsC, Color bootC, out Transform knee)
        {
            var hip = Pivot("Hip" + side, body, new Vector3(0.085f * sw * s, 0.85f, 0f));
            Part(PrimitiveType.Capsule, hip, new Vector3(0f, -0.2f, 0f), new Vector3(0.12f * sw, 0.22f, 0.12f * sw), Mat(pantsC, 0.8f));
            knee = Pivot("Knee" + side, hip, new Vector3(0f, -0.42f, 0f));
            Part(PrimitiveType.Capsule, knee, new Vector3(0f, -0.2f, 0f), new Vector3(0.1f * sw, 0.21f, 0.1f * sw), Mat(bootC, 0.72f));
            Part(PrimitiveType.Cube, knee, new Vector3(0f, -0.4f, 0.04f), new Vector3(0.1f * sw, 0.06f, 0.2f), Mat(bootC, 0.62f));
            return hip;
        }

        Transform ArmChain(string side, float s, float sw, Color armC, out Transform elbow, out Transform hand)
        {
            var sh = Pivot("Shoulder" + side, spine, new Vector3(0.2f * sw * s, 0.5f, 0f));
            Part(PrimitiveType.Capsule, sh, new Vector3(0f, -0.14f, 0f), new Vector3(0.085f * sw, 0.15f, 0.085f * sw), Mat(armC, 0.9f));
            elbow = Pivot("Elbow" + side, sh, new Vector3(0f, -0.28f, 0f));
            Part(PrimitiveType.Capsule, elbow, new Vector3(0f, -0.12f, 0f), new Vector3(0.075f * sw, 0.13f, 0.075f * sw), Mat(armC, 0.85f));
            hand = Pivot("Hand" + side, elbow, new Vector3(0f, -0.25f, 0f));
            Part(PrimitiveType.Sphere, hand, new Vector3(0f, -0.03f, 0f), new Vector3(0.085f, 0.085f, 0.085f), Mat(Skin, 0.9f));
            return sh;
        }

        // ---------------------------------------------------------------- 法師：尖帽、長袍、法杖
        void BuildMage()
        {
            var hatC = Mul(clsColor, 0.7f);
            // 帽子：帽沿＋圓錐（帽尖稍微往後彎）
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.07f, -0.01f), new Vector3(0.46f, 0.008f, 0.46f), Mat(hatC, 0.85f));
            var cone = Pivot("HatCone", head, new Vector3(0f, 0.075f, -0.01f));
            cone.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            MeshPart("Hat", cone, Frustum(0.165f, 0f, 0.27f, 20), Vector3.zero, Mat(hatC));
            MeshPart("HatBand", cone, Frustum(0.168f, 0.15f, 0.04f, 20), new Vector3(0f, 0.005f, 0f), Mat(Gold, 0.9f));
            // 頭髮（後腦）
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.02f, -0.045f), new Vector3(0.29f, 0.26f, 0.24f), Mat(new Color(0.92f, 0.92f, 0.95f), 0.9f));
            // 長袍：從腰到腳踝的喇叭狀圓台，袍擺鑲金邊
            MeshPart("Robe", spine, Frustum(0.25f, 0.16f, 0.95f, 24), new Vector3(0f, -0.8f, 0f), Mat(clsColor, 0.8f));
            MeshPart("RobeHem", spine, Frustum(0.255f, 0.245f, 0.05f, 24), new Vector3(0f, -0.81f, 0f), Mat(Gold, 0.75f));
            // 袖口
            MeshPart("CuffL", elL, Frustum(0.07f, 0.05f, 0.08f, 12), new Vector3(0f, -0.25f, 0f), Mat(Gold, 0.85f));
            MeshPart("CuffR", elR, Frustum(0.07f, 0.05f, 0.08f, 12), new Vector3(0f, -0.25f, 0f), Mat(Gold, 0.85f));
            // 法杖（沿手的 +Z，前臂水平時法杖直立）＋頂端發光寶珠
            var staff = Pivot("Staff", handR, new Vector3(0f, -0.04f, 0f));
            Part(PrimitiveType.Cylinder, staff, new Vector3(0f, 0f, 0.2f), new Vector3(0.035f, 0.65f, 0.035f), Mat(Wood, 0.85f), new Vector3(90f, 0f, 0f));
            MeshPart("StaffHead", staff, Frustum(0.025f, 0.07f, 0.08f, 12), new Vector3(0f, 0f, 0.82f), Mat(Gold), new Vector3(90f, 0f, 0f));
            orb = Part(PrimitiveType.Sphere, staff, new Vector3(0f, 0f, 0.95f), new Vector3(0.11f, 0.11f, 0.11f),
                Glow(Color.Lerp(clsColor, Color.white, 0.45f))).transform;
            orbBaseScale = orb.localScale;

            baseR = new Arm(-10f, 6f, -80f, 0f); swingR = 0.35f;
            chargeR = new Arm(-70f, 5f, -20f, 0f);
            chargeL = new Arm(-75f, -5f, -30f, 0f);
            castR = new Arm(-95f, 0f, -5f, 60f);
            legAmpScale = 0.6f;
        }

        // ---------------------------------------------------------------- 弓箭手：兜帽披風、弓、箭袋
        void BuildArcher()
        {
            var hoodC = Mul(clsColor, 0.55f);
            // 頭髮瀏海＋兜帽（往後偏，臉露出來）＋帽尖
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.075f, 0.02f), new Vector3(0.27f, 0.17f, 0.25f), Mat(new Color(0.55f, 0.35f, 0.2f)));
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.025f, -0.05f), new Vector3(0.33f, 0.34f, 0.33f), Mat(hoodC));
            var tip = Pivot("HoodTip", head, new Vector3(0f, 0.04f, -0.17f));
            tip.localRotation = Quaternion.Euler(-120f, 0f, 0f);
            MeshPart("HoodTipCone", tip, Frustum(0.08f, 0f, 0.14f, 12), Vector3.zero, Mat(hoodC, 0.85f));
            // 披風（掛在肩膀後方，走路時往後飄）
            cape = Pivot("Cape", spine, new Vector3(0f, 0.52f, -0.12f));
            Part(PrimitiveType.Cube, cape, new Vector3(0f, -0.36f, 0f), new Vector3(0.36f, 0.72f, 0.02f), Mat(hoodC, 0.8f));
            // 短裙擺
            MeshPart("Skirt", spine, Frustum(0.2f, 0.175f, 0.22f, 20), new Vector3(0f, -0.15f, 0f), Mat(clsColor, 0.8f));
            // 箭袋（背上斜背）＋三支箭
            var q = Pivot("Quiver", spine, new Vector3(0.08f, 0.33f, -0.17f));
            q.localRotation = Quaternion.Euler(-10f, 0f, -22f);
            Part(PrimitiveType.Cylinder, q, Vector3.zero, new Vector3(0.1f, 0.17f, 0.1f), Mat(Leather, 0.85f));
            Part(PrimitiveType.Cylinder, q, new Vector3(0f, 0.16f, 0f), new Vector3(0.11f, 0.015f, 0.11f), Mat(Gold, 0.9f));
            for (int i = 0; i < 3; i++)
            {
                var off = new Vector3((i - 1) * 0.025f, 0f, (i == 1 ? 0.015f : -0.01f));
                Part(PrimitiveType.Cylinder, q, off + new Vector3(0f, 0.2f, 0f), new Vector3(0.012f, 0.1f, 0.012f), Mat(Wood));
                Part(PrimitiveType.Cube, q, off + new Vector3(0f, 0.29f, 0f), new Vector3(0.006f, 0.07f, 0.035f), Mat(Color.white, 0.95f));
            }
            // 弓（左手）：前臂水平時弓直立；弓身由小段圓柱拼成弧形，握把在手上，弓梢往後彎，弦在後方
            var bow = Pivot("Bow", handL, new Vector3(0f, -0.04f, 0f));
            const int N = 10; const float half = 0.52f, bend = 0.13f;
            var woodMat = Mat(Wood, 0.9f);
            Vector3 prev = BowPoint(-1f, half, bend);
            for (int i = 1; i <= N; i++)
            {
                var p = BowPoint(-1f + 2f * i / N, half, bend);
                Seg(bow, prev, p, 0.028f, woodMat);
                prev = p;
            }
            Part(PrimitiveType.Cylinder, bow, Vector3.zero, new Vector3(0.04f, 0.06f, 0.04f), Mat(Leather), new Vector3(90f, 0f, 0f));
            Seg(bow, BowPoint(-1f, half, bend), BowPoint(1f, half, bend), 0.006f, Glow(new Color(0.95f, 0.95f, 0.9f)));

            baseL = new Arm(-15f, 8f, -60f, 0f); swingL = 0.5f;
            chargeL = new Arm(-88f, 0f, 0f, 0f);
            chargeR = new Arm(-85f, -25f, -120f, 0f);
            castL = chargeL; castR = new Arm(-40f, 30f, -30f, 0f);
            castMaskL = 1f; castMaskR = 1f;
        }

        static Vector3 BowPoint(float t, float half, float bend) => new Vector3(0f, bend * t * t, half * t);

        // ---------------------------------------------------------------- 刺客：深色兜帽、面罩、雙匕首、圍巾
        void BuildAssassin()
        {
            var hoodC = Mul(DarkCloth, 1.3f);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.025f, -0.05f), new Vector3(0.33f, 0.34f, 0.33f), Mat(hoodC));
            var tip = Pivot("HoodTip", head, new Vector3(0f, 0.08f, -0.12f));
            tip.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            MeshPart("HoodTipCone", tip, Frustum(0.09f, 0f, 0.13f, 12), Vector3.zero, Mat(hoodC, 0.9f));
            // 面罩：蓋住鼻子以下
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.065f, 0.012f), new Vector3(0.29f, 0.17f, 0.28f), Mat(Mul(DarkCloth, 0.8f)));
            // 圍巾：脖子一圈＋往後飄的尾巴（職業色）
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.55f, 0f), new Vector3(0.2f, 0.045f, 0.2f), Mat(clsColor));
            scarfTail = Pivot("ScarfTail", spine, new Vector3(0.05f, 0.55f, -0.09f));
            Part(PrimitiveType.Cube, scarfTail, new Vector3(0f, -0.2f, 0f), new Vector3(0.08f, 0.4f, 0.015f), Mat(clsColor, 0.85f));
            // 腰帶（職業色）
            Part(PrimitiveType.Cylinder, spine, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.025f, 0.21f), Mat(clsColor, 0.8f));
            // 雙匕首
            Dagger(handL); Dagger(handR);

            baseL = baseR = new Arm(-25f, 10f, -60f, 20f);
            chargeL = chargeR = new Arm(-40f, 20f, -105f, 10f);
            castR = new Arm(-90f, -5f, 0f, 85f);
            castL = new Arm(-60f, 10f, -40f, 20f);
            castMaskL = 1f; castMaskR = 1f;
            swingL = swingR = 0.6f;
        }

        void Dagger(Transform hand)
        {
            var d = Pivot("Dagger", hand, new Vector3(0f, -0.04f, 0f));
            Part(PrimitiveType.Cylinder, d, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.045f, 0.03f), Mat(clsColor, 0.8f), new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cube, d, new Vector3(0f, 0f, 0.05f), new Vector3(0.025f, 0.09f, 0.02f), Mat(Metal, 0.8f));
            Part(PrimitiveType.Cube, d, new Vector3(0f, 0f, 0.17f), new Vector3(0.012f, 0.04f, 0.22f), Mat(Color.Lerp(Metal, Color.white, 0.4f)));
        }

        // ---------------------------------------------------------------- 劍士：頭盔羽飾、護肩、胸甲、劍、圓盾
        void BuildSwordsman()
        {
            // 頭盔（往後偏，露出臉）＋護鼻＋職業色羽飾
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.045f, -0.025f), new Vector3(0.31f, 0.25f, 0.31f), Mat(Metal));
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.06f, -0.025f), new Vector3(0.32f, 0.012f, 0.32f), Mat(Metal, 0.8f));
            Part(PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.14f), new Vector3(0.022f, 0.08f, 0.02f), Mat(Metal, 0.9f));
            Part(PrimitiveType.Cube, head, new Vector3(0f, 0.17f, -0.03f), new Vector3(0.035f, 0.09f, 0.28f), Mat(clsColor));
            // 胸甲（金屬）＋職業色罩袍＋護肩
            Part(PrimitiveType.Sphere, spine, new Vector3(0f, 0.38f, 0.03f), new Vector3(0.36f, 0.3f, 0.24f), Mat(Color.Lerp(Metal, Color.white, 0.25f)));
            Part(PrimitiveType.Cube, spine, new Vector3(0f, 0.1f, 0.095f), new Vector3(0.2f, 0.3f, 0.03f), Mat(clsColor, 0.85f));
            Part(PrimitiveType.Cube, spine, new Vector3(0f, 0.1f, -0.095f), new Vector3(0.2f, 0.3f, 0.03f), Mat(clsColor, 0.75f));
            Part(PrimitiveType.Sphere, shL, new Vector3(-0.015f, -0.01f, 0f), new Vector3(0.15f, 0.1f, 0.15f), Mat(clsColor));
            Part(PrimitiveType.Sphere, shR, new Vector3(0.015f, -0.01f, 0f), new Vector3(0.15f, 0.1f, 0.15f), Mat(clsColor));
            // 劍（右手）：刀身沿手的 +Z
            var sword = Pivot("Sword", handR, new Vector3(0f, -0.04f, 0f));
            Part(PrimitiveType.Cylinder, sword, new Vector3(0f, 0f, -0.01f), new Vector3(0.035f, 0.07f, 0.035f), Mat(Leather), new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Sphere, sword, new Vector3(0f, 0f, -0.09f), new Vector3(0.05f, 0.05f, 0.05f), Mat(Gold));
            Part(PrimitiveType.Cube, sword, new Vector3(0f, 0f, 0.07f), new Vector3(0.035f, 0.2f, 0.035f), Mat(Gold, 0.9f));
            Part(PrimitiveType.Cube, sword, new Vector3(0f, 0f, 0.42f), new Vector3(0.015f, 0.06f, 0.66f), Mat(Color.Lerp(Metal, Color.white, 0.45f)));
            // 圓盾（左前臂外側）：防禦時轉到身體正前方
            shield = Pivot("Shield", elL, new Vector3(-0.07f, -0.14f, 0f));
            shield.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cylinder, shield, Vector3.zero, new Vector3(0.4f, 0.015f, 0.4f), Mat(Metal, 0.8f));
            Part(PrimitiveType.Cylinder, shield, Vector3.zero, new Vector3(0.34f, 0.025f, 0.34f), Mat(clsColor));
            Part(PrimitiveType.Sphere, shield, Vector3.zero, new Vector3(0.09f, 0.06f, 0.09f), Mat(Gold));
            shieldIdlePos = shield.localPosition; shieldIdleRot = shield.localRotation;

            baseR = new Arm(-10f, 8f, -40f, 0f); swingR = 0.6f;
            baseL = new Arm(-10f, 10f, -45f, 0f); swingL = 0.4f;
            guardL = new Arm(-70f, -25f, -60f, 0f); guardR = baseR;
            chargeR = new Arm(-150f, 10f, -30f, 0f);
            chargeL = new Arm(-30f, 10f, -50f, 0f);
            castR = new Arm(-45f, -10f, 0f, 45f);
        }

        // ================================================================ 對外操作
        public void SetPose(Vector3 feetPosition, Vector3 forward)
        {
            transform.position = feetPosition;
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        public void PlayCast() { if (!dead) castT = 0f; }

        public void PlayHit()
        {
            if (dead) return;
            hitT = HitDur;
            SetFlash(true);
        }

        public void SetDead(bool d)
        {
            dead = d;
            if (d) { castT = -1f; }
        }

        public void SetVisible(bool v)
        {
            visible = v;
            if (rends == null) return;
            for (int i = 0; i < rends.Length; i++) if (rends[i]) rends[i].enabled = v;
            if (!v && aura) aura.SetActive(false);
        }

        void SetFlash(bool on)
        {
            if (flashing == on || rends == null) return;
            flashing = on;
            for (int i = 0; i < rends.Length; i++)
                if (rends[i]) rends[i].sharedMaterial = on ? flashMat : origMats[i];
        }

        // ================================================================ 動作
        void Update() { Animate(Time.deltaTime); }

        void Animate(float dt)
        {
            if (body == null) return;
            clock += dt;

            // 平滑的狀態量
            float speed = dead ? 0f : Mathf.Clamp(MoveSpeed, 0f, 3f);
            walkAmt = Mathf.MoveTowards(walkAmt, Mathf.Clamp01(speed / 0.8f), dt * 4f);
            if (speed > 0.01f) walkPhase += dt * Mathf.PI * 2f * Mathf.Max(speed, 0.6f) / 1.1f;
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 3f);
            if (walkPhase > 1000f) walkPhase -= Mathf.PI * 300f;
            guardAmt = Mathf.MoveTowards(guardAmt, Guarding && !dead ? 1f : 0f, dt * 8f);
            float ch = dead ? 0f : Mathf.Clamp01(Charge);
            float chargeTarget = (!dead && Charge > 0f) ? 1f : 0f;
            chargeAmt = Mathf.MoveTowards(chargeAmt, chargeTarget, dt * 6f);
            deadAmt = Mathf.MoveTowards(deadAmt, dead ? 1f : 0f, dt / DieDur);
            float castK = 0f;
            if (castT >= 0f)
            {
                castT += dt;
                if (castT >= CastDur) castT = -1f;
                else castK = Mathf.Sin(Mathf.PI * castT / CastDur);
            }
            float hitLean = 0f;
            if (hitT > 0f)
            {
                hitT -= dt;
                hitLean = Mathf.Clamp01(hitT / HitDur);
                if (hitT <= 0f) SetFlash(false);
            }

            float sinP = Mathf.Sin(walkPhase), cosP = Mathf.Cos(walkPhase);
            float amp = 32f * walkAmt * legAmpScale;
            float breath = Mathf.Sin(clock * 2.2f);
            float deadE = deadAmt * deadAmt * (3f - 2f * deadAmt);   // smoothstep

            // ---- 身體：走路起伏、呼吸、倒地
            float bob = walkAmt * Mathf.Abs(cosP) * 0.035f + (1f - walkAmt) * breath * 0.004f;
            body.localPosition = new Vector3(0f, bob * (1f - deadE) + 0.11f * deadE, 0f);
            body.localRotation = Quaternion.Euler(-88f * deadE, 0f, 0f);

            // ---- 腿
            float kneeAmp = amp * 1.3f;
            hipL.localRotation = Quaternion.Euler(sinP * amp * (1f - deadE), 0f, 0f);
            hipR.localRotation = Quaternion.Euler(-sinP * amp * (1f - deadE), 0f, 0f);
            knL.localRotation = Quaternion.Euler(Mathf.Max(0f, cosP) * kneeAmp + 3f, 0f, 0f);
            knR.localRotation = Quaternion.Euler(Mathf.Max(0f, -cosP) * kneeAmp + 3f, 0f, 0f);

            // ---- 軀幹、頭：走路前傾、詠唱挺胸、受擊後仰、呼吸
            float lean = walkAmt * 7f - hitLean * 16f - chargeAmt * 3f + breath * 0.8f * (1f - walkAmt);
            spine.localRotation = Quaternion.Euler(lean * (1f - deadE), Mathf.Sin(walkPhase) * 4f * walkAmt, 0f);
            neck.localRotation = Quaternion.Euler(-lean * 0.5f + Mathf.Sin(clock * 0.7f) * 2f, Mathf.Sin(clock * 0.43f) * 6f * (1f - walkAmt), 0f);

            // ---- 手臂：基本姿勢 → 加走路擺動 → 防禦 → 詠唱 → 出手 → 倒地
            float armSwing = sinP * amp * 0.8f;
            var aL = baseL; aL.x -= armSwing * swingL;
            var aR = baseR; aR.x += armSwing * swingR;
            aL.zOut += breath * 1.5f; aR.zOut += breath * 1.5f;
            aL = Arm.Lerp(aL, guardL, guardAmt); aR = Arm.Lerp(aR, guardR, guardAmt);
            aL = Arm.Lerp(aL, chargeL, chargeAmt); aR = Arm.Lerp(aR, chargeR, chargeAmt);
            if (chargeAmt > 0f)
            {
                // 蓄力時手微微顫動
                float tremble = Mathf.Sin(clock * 37f) * 1.5f * ch;
                aL.x += tremble; aR.x -= tremble;
            }
            aL = Arm.Lerp(aL, castL, castK * castMaskL); aR = Arm.Lerp(aR, castR, castK * castMaskR);
            aL = Arm.Lerp(aL, Relaxed, deadE); aR = Arm.Lerp(aR, Relaxed, deadE);
            ApplyArm(shL, elL, handL, aL, -1f);
            ApplyArm(shR, elR, handR, aR, 1f);

            // ---- 盾：防禦時轉到正前方
            if (shield != null)
            {
                float g = guardAmt * (1f - deadE);
                Vector3 idlePos = elL.TransformPoint(shieldIdlePos);
                Quaternion idleRot = elL.rotation * shieldIdleRot;
                Vector3 guardPos = body.TransformPoint(new Vector3(-0.03f, 1.2f, 0.32f));
                Quaternion guardRot = body.rotation * Quaternion.Euler(90f, 0f, 0f);
                shield.position = Vector3.Lerp(idlePos, guardPos, g);
                shield.rotation = Quaternion.Slerp(idleRot, guardRot, g);
            }

            // ---- 披風、圍巾：走路時往後飄
            float flutter = Mathf.Sin(clock * 5f + walkPhase) * (2f + 4f * walkAmt);
            if (cape != null) cape.localRotation = Quaternion.Euler(4f + walkAmt * 28f + flutter, 0f, 0f);
            if (scarfTail != null) scarfTail.localRotation = Quaternion.Euler(15f + walkAmt * 45f + flutter * 1.5f, 0f, Mathf.Sin(clock * 3.1f) * 8f);

            // ---- 法杖寶珠：詠唱時變亮變大
            if (orb != null)
            {
                float s = 1f + chargeAmt * (0.4f + 0.6f * ch) + Mathf.Sin(clock * (4f + 10f * ch)) * (0.05f + 0.12f * chargeAmt) + castK * 0.5f;
                orb.localScale = orbBaseScale * s;
            }

            // ---- 腳下光圈
            if (aura != null)
            {
                bool show = visible && !dead && Charge > 0f;
                if (aura.activeSelf != show) aura.SetActive(show);
                if (show)
                {
                    float pulse = Mathf.Sin(clock * (5f + 8f * ch));
                    float sc = 0.85f + 0.35f * ch + pulse * 0.05f;
                    aura.transform.localScale = new Vector3(sc, 1f, sc);
                    aura.transform.localRotation = Quaternion.Euler(0f, clock * 60f, 0f);
                    auraMat.color = new Color(clsColor.r, clsColor.g, clsColor.b, 0.35f + 0.45f * ch + pulse * 0.1f);
                }
            }
        }

        static void ApplyArm(Transform sh, Transform el, Transform hand, Arm a, float side)
        {
            sh.localRotation = Quaternion.Euler(a.x, 0f, a.zOut * side);
            el.localRotation = Quaternion.Euler(a.el, 0f, 0f);
            hand.localRotation = Quaternion.Euler(a.wr, 0f, 0f);
        }

        void OnDestroy()
        {
            if (auraMat != null) Destroy(auraMat);
        }

        // ================================================================ 建構小工具
        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = localScale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static GameObject MeshPart(string name, Transform parent, Mesh mesh, Vector3 localPos, Material mat, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        /// <summary>從 a 到 b 的細圓柱（本地座標）。</summary>
        static void Seg(Transform parent, Vector3 a, Vector3 b, float width, Material mat)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            var go = Part(PrimitiveType.Cylinder, parent, (a + b) * 0.5f, new Vector3(width, len * 0.5f, width), mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d / len);
        }

        /// <summary>圓台（r1 = 0 時是圓錐），底面在 y = 0、往 +Y 長 h，上下都封口。結果依尺寸快取。</summary>
        static Mesh Frustum(float r0, float r1, float h, int seg)
        {
            string key = $"F{r0:F3}_{r1:F3}_{h:F3}_{seg}";
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            int ring = seg + 1;
            var v = new Vector3[ring * 2 + (ring + 1) * 2];
            var n = new Vector3[v.Length];
            var tris = new int[seg * 6 + seg * 3 * 2];
            float slope = (r0 - r1) / h;
            for (int i = 0; i < ring; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                var nn = new Vector3(c, slope, s).normalized;
                v[i] = new Vector3(c * r0, 0f, s * r0); n[i] = nn;
                v[ring + i] = new Vector3(c * r1, h, s * r1); n[ring + i] = nn;
            }
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int b0 = i, b1 = i + 1, t0 = ring + i, t1 = ring + i + 1;
                tris[t++] = t0; tris[t++] = t1; tris[t++] = b1;
                tris[t++] = t0; tris[t++] = b1; tris[t++] = b0;
            }
            // 底面（朝下）
            int bc = ring * 2;
            v[bc] = Vector3.zero; n[bc] = Vector3.down;
            for (int i = 0; i < ring; i++) { v[bc + 1 + i] = v[i]; n[bc + 1 + i] = Vector3.down; }
            for (int i = 0; i < seg; i++) { tris[t++] = bc; tris[t++] = bc + 1 + i; tris[t++] = bc + 2 + i; }
            // 頂面（朝上）
            int tc = bc + ring + 1;
            v[tc] = new Vector3(0f, h, 0f); n[tc] = Vector3.up;
            for (int i = 0; i < ring; i++) { v[tc + 1 + i] = v[ring + i]; n[tc + 1 + i] = Vector3.up; }
            for (int i = 0; i < seg; i++) { tris[t++] = tc; tris[t++] = tc + 2 + i; tris[t++] = tc + 1 + i; }

            var m = new Mesh { name = key, vertices = v, normals = n, triangles = tris };
            m.RecalculateBounds();
            meshCache[key] = m;
            return m;
        }

        /// <summary>水平圓環（XZ 平面），用在腳下的詠唱光圈。</summary>
        static Mesh RingMesh(float rIn, float rOut, int seg)
        {
            string key = $"R{rIn:F3}_{rOut:F3}_{seg}";
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            int ring = seg + 1;
            var v = new Vector3[ring * 2];
            var n = new Vector3[v.Length];
            var tris = new int[seg * 6];
            for (int i = 0; i < ring; i++)
            {
                float a = 2f * Mathf.PI * i / seg;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i] = new Vector3(c * rIn, 0f, s * rIn); n[i] = Vector3.up;
                v[ring + i] = new Vector3(c * rOut, 0f, s * rOut); n[ring + i] = Vector3.up;
            }
            int t = 0;
            for (int i = 0; i < seg; i++)
            {
                int i0 = i, i1 = i + 1, o0 = ring + i, o1 = ring + i + 1;
                tris[t++] = i0; tris[t++] = i1; tris[t++] = o1;
                tris[t++] = i0; tris[t++] = o1; tris[t++] = o0;
            }
            var m = new Mesh { name = key, vertices = v, normals = n, triangles = tris };
            m.RecalculateBounds();
            meshCache[key] = m;
            return m;
        }
    }
}
