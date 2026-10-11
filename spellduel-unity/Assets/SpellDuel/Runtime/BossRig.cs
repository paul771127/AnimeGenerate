using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 頭目「熔岩巨像」的外觀與動作：全部用基本形狀＋程式產生的網格＋少量粒子即時拼出，不需要任何資源檔。
    ///   座標：腳底在本地 y = 0，面向本地 +Z，頭頂約 3.2 m（真實公尺，AR 場景請保持父物件縮放 = 1）。
    ///   外型：深色玄武岩塊組成的駝背巨人（胸口、腰、關節、前臂有發光的熔岩裂縫），胸口有會脈動的熔岩核心、
    ///   兩顆發光的眼睛、巨大的前臂與拳頭；身邊飄著火星，腳下有淡淡的熱光。
    ///   動作：沉重的踏步走路（MoveSpeed）、待機呼吸搖晃、四種攻擊的預備姿勢（Windup）與出手（Attack）、
    ///   受擊（後仰＋岩石閃紅）、狂暴（熔岩轉為白黃、裂縫脈動變快、火星變多）、死亡（跪倒後崩解成一地碎石、熔岩熄滅）。
    ///   效能：本體約 55 個 Renderer（含粒子），材質每隻頭目共用 7 個，每幀不配置記憶體。
    /// </summary>
    public class BossRig : MonoBehaviour
    {
        // ================================================================ 對外介面
        /// <summary>頭頂高度（公尺），血條與瞄準用。</summary>
        public const float Height = 3.2f;

        /// <summary>移動速度（公尺/秒），驅動沉重的踏步動作；0 = 待機呼吸搖晃。</summary>
        public float MoveSpeed { get; set; }

        /// <summary>胸口熔岩核心相對於根物件的本地座標（特效發射點）。</summary>
        public Vector3 ChestLocal => core != null ? transform.InverseTransformPoint(core.position) : new Vector3(0f, 2.15f, 0.65f);

        public static BossRig Create()
        {
            InitShaders();
            EnsureLight();
            var go = new GameObject("Boss LavaGolem");
            var rig = go.AddComponent<BossRig>();
            rig.Build();
            return rig;
        }

        /// <summary>攻擊預備姿勢：duration 秒內擺到位，之後一直維持（微微顫抖）直到呼叫 Attack。</summary>
        public void Windup(string attack, float duration)
        {
            if (dead || body == null) return;
            atk = attack ?? "";
            phase = PhaseWindup;
            atkT = 0f;
            atkDur = Mathf.Max(0.05f, duration);
        }

        /// <summary>出手：從目前姿勢快速打到出手姿勢，約 0.65 秒後回到待機。</summary>
        public void Attack(string attack)
        {
            if (dead || body == null) return;
            atk = attack ?? "";
            snap = cur;
            phase = PhaseStrike;
            atkT = 0f;
            if (atk == "b_bolt" && spew != null) spew.Emit(28);
        }

        public void PlayHit()
        {
            if (dead || body == null) return;
            hitT = HitDur;
        }

        public void SetEnraged(bool on) { enraged = on; }

        public void SetDead(bool d)
        {
            if (d == dead) return;
            dead = d;
            if (d)
            {
                deadT = 0f;
                phase = PhaseNone;
                hitT = 0f;
            }
            else Revive();
        }

        public void SetVisible(bool v)
        {
            visible = v;
            if (rends == null) return;
            for (int i = 0; i < rends.Length; i++) if (rends[i]) rends[i].enabled = v;
        }

        // ================================================================ 共用 shader／網格
        static Shader litShader, glowShader;
        static bool unlit;
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static readonly int TintId = Shader.PropertyToID("_TintColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

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

        // 顏色
        static readonly Color RockAC = new Color(0.3f, 0.26f, 0.24f);     // 主體玄武岩
        static readonly Color RockBC = new Color(0.4f, 0.35f, 0.31f);     // 上方受光的大岩塊（肩、駝背、頭、拳）
        static readonly Color RockCC = new Color(0.2f, 0.18f, 0.17f);     // 最暗的岩塊（四肢、下巴）
        static readonly Color FlashC = new Color(1f, 0.45f, 0.3f);
        static readonly Color LavaN = new Color(1f, 0.36f, 0.05f);        // 平常的熔岩（橘紅）
        static readonly Color LavaE = new Color(1f, 0.8f, 0.35f);         // 狂暴的熔岩（白黃）
        static readonly Color LavaDim = new Color(0.55f, 0.09f, 0.02f);   // 脈動暗部
        static readonly Color HotN = new Color(1f, 0.78f, 0.35f);
        static readonly Color HotE = new Color(1f, 0.98f, 0.82f);
        static readonly Color EyeN = new Color(1f, 0.72f, 0.2f);
        static readonly Color EyeE = new Color(1f, 1f, 0.85f);
        static readonly Color DeadEmber = new Color(0.16f, 0.05f, 0.03f); // 熄滅後的餘燼

        // ================================================================ 骨架與狀態
        Transform body, spine, neck, head, jaw, core, plateL, plateR;
        Transform hipL, hipR, knL, knR, anL, anR;
        Transform shL, shR, elL, elR, wrL, wrR;
        Vector3 plateRestL, plateRestR;
        Material rockA, rockB, rockC, lavaM, hotM, coreM, eyeM, heatM;
        ParticleSystem embers, halo, stepDust, spew;
        Renderer[] rends;

        // 死亡崩解用：每個岩塊的原始本地姿態與落下中的速度
        List<Transform> partList = new List<Transform>();
        Transform[] parts;
        Vector3[] partPos, partScale, vel, angVel;
        Quaternion[] partRot;
        float[] partRad;
        bool[] landed;

        const int PhaseNone = 0, PhaseWindup = 1, PhaseStrike = 2;
        int phase;
        string atk = "";
        float atkT, atkDur = 1f;
        Pose cur, snap;

        float clock, walkPhase, walkAmt, hitT, enr, deadT, jolt, lastCos, emberRate = -1f;
        bool dead, enraged, visible = true, crumbled, flashing;

        const float HitDur = 0.2f;
        const float StrikeIn = 0.14f, StrikeHold = 0.18f, StrikeOut = 0.33f;
        const float CollapseAt = 1.0f;
        const float HipY = 1.1f, L1 = 0.55f, L2 = 0.45f;   // 髖高、大腿、小腿長（腳踝離地 0.1）

        // ================================================================ 姿勢
        struct Pose
        {
            public float spX, spY, spZ;     // 軀幹：前傾(+)、轉身(+ = 往右轉)、側傾
            public float head, jaw;         // 頭：低頭(+)；下巴張開 0..1
            public float crouch;            // 下蹲（公尺）
            public float open, glow;        // 胸甲打開 0..1、熔岩額外亮度 0..1
            public Vector3 shL, shR;        // 肩：(前後 x、水平擺 y、往外張 z)，左右鏡像
            public float elL, elR, wrL, wrR;

            public static Pose Lerp(in Pose a, in Pose b, float t)
            {
                Pose p;
                p.spX = a.spX + (b.spX - a.spX) * t;
                p.spY = a.spY + (b.spY - a.spY) * t;
                p.spZ = a.spZ + (b.spZ - a.spZ) * t;
                p.head = a.head + (b.head - a.head) * t;
                p.jaw = a.jaw + (b.jaw - a.jaw) * t;
                p.crouch = a.crouch + (b.crouch - a.crouch) * t;
                p.open = a.open + (b.open - a.open) * t;
                p.glow = a.glow + (b.glow - a.glow) * t;
                p.shL = Vector3.LerpUnclamped(a.shL, b.shL, t);
                p.shR = Vector3.LerpUnclamped(a.shR, b.shR, t);
                p.elL = a.elL + (b.elL - a.elL) * t;
                p.elR = a.elR + (b.elR - a.elR) * t;
                p.wrL = a.wrL + (b.wrL - a.wrL) * t;
                p.wrR = a.wrR + (b.wrR - a.wrR) * t;
                return p;
            }
        }

        /// <summary>待機：駝背、雙臂垂在身側微微往外張（像猩猩）。</summary>
        static Pose Idle()
        {
            var p = new Pose { spX = 12f, head = -10f, crouch = 0.06f, elL = -25f, elR = -25f, wrL = -10f, wrR = -10f };
            p.shL = p.shR = new Vector3(-12f, 0f, 14f);
            return p;
        }

        static Pose WindupPose(string id)
        {
            var p = Idle();
            switch (id)
            {
                case "b_sweep":   // 右臂往後側大幅拉開、身體往右扭
                    p.spX = 6f; p.spY = 40f; p.spZ = -6f; p.head = -6f; p.crouch = 0.14f;
                    p.shR = new Vector3(-10f, 60f, 80f); p.elR = -35f; p.wrR = -10f;
                    p.shL = new Vector3(-45f, 0f, 25f); p.elL = -70f;
                    p.glow = 0.35f;
                    break;
                case "b_bolt":    // 挺胸後仰、雙臂往後拉，胸甲打開露出核心
                    p.spX = -16f; p.head = -14f; p.jaw = 0.5f; p.crouch = 0.08f;
                    p.shL = p.shR = new Vector3(30f, 0f, 40f); p.elL = p.elR = -60f;
                    p.open = 1f; p.glow = 1f;
                    break;
                case "b_rain":    // 雙臂高舉向天、仰頭怒吼
                    p.spX = -18f; p.head = -38f; p.jaw = 1f; p.crouch = 0.02f;
                    p.shL = p.shR = new Vector3(-165f, 0f, 35f); p.elL = p.elR = -10f; p.wrL = p.wrR = 0f;
                    p.glow = 0.6f; p.open = 0.3f;
                    break;
                default:          // b_slam：雙拳高舉過頭（拳頭在腦後）
                    p.spX = -14f; p.head = -16f; p.jaw = 0.3f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-165f, 0f, 12f); p.elL = p.elR = -55f; p.wrL = p.wrR = -20f;
                    p.glow = 0.4f;
                    break;
            }
            return p;
        }

        static Pose StrikePose(string id)
        {
            var p = Idle();
            switch (id)
            {
                case "b_sweep":   // 右臂在胸口高度橫掃到左前方
                    p.spX = 10f; p.spY = -45f; p.spZ = 6f; p.head = 0f; p.crouch = 0.16f;
                    p.shR = new Vector3(-10f, -75f, 82f); p.elR = -15f; p.wrR = 0f;
                    p.shL = new Vector3(-20f, 0f, 35f); p.elL = -40f;
                    p.glow = 0.3f;
                    break;
                case "b_bolt":    // 胸口往前頂、雙臂前推張開，核心噴出熔岩
                    p.spX = 20f; p.head = 0f; p.jaw = 1f; p.crouch = 0.12f;
                    p.shL = p.shR = new Vector3(-70f, 0f, 38f); p.elL = p.elR = -10f;
                    p.open = 1f; p.glow = 1f;
                    break;
                case "b_rain":    // 雙臂猛力往下砸
                    p.spX = 24f; p.head = 8f; p.jaw = 0.6f; p.crouch = 0.28f;
                    p.shL = p.shR = new Vector3(-30f, 0f, 30f); p.elL = p.elR = -35f;
                    p.glow = 0.4f;
                    break;
                default:          // b_slam：彎腰下蹲，雙拳砸進正前方地面
                    p.spX = 34f; p.head = 12f; p.jaw = 0.7f; p.crouch = 0.32f;
                    p.shL = p.shR = new Vector3(-58f, 0f, 10f); p.elL = p.elR = -8f; p.wrL = p.wrR = 0f;
                    p.glow = 0.5f;
                    break;
            }
            return p;
        }

        static Pose DeadPose()
        {
            var p = Idle();
            p.spX = 42f; p.head = 30f; p.jaw = 0.25f; p.crouch = 0.5f;
            p.shL = p.shR = new Vector3(5f, 0f, 6f); p.elL = p.elR = -5f; p.wrL = p.wrR = 0f;
            return p;
        }

        // ================================================================ 建立
        void Build()
        {
            // 每隻頭目自己的材質（要做閃色、脈動、狂暴變色）
            rockA = new Material(litShader) { color = RockAC, name = "BossRockA" };
            rockB = new Material(litShader) { color = RockBC, name = "BossRockB" };
            rockC = new Material(litShader) { color = RockCC, name = "BossRockC" };
            lavaM = new Material(glowShader) { color = LavaN, name = "BossLava" };
            hotM = new Material(glowShader) { color = HotN, name = "BossLavaHot" };
            coreM = new Material(glowShader) { color = LavaN, name = "BossCore" };
            eyeM = new Material(glowShader) { color = EyeN, name = "BossEye" };

            body = Pivot("Body", transform, Vector3.zero);

            // ---- 腿（髖 → 膝 → 腳踝），腳底剛好在 y = 0
            hipL = BuildLeg(-1f, out knL, out anL);
            hipR = BuildLeg(1f, out knR, out anR);

            // ---- 骨盆
            RockPart(body, new Vector3(0f, 1.12f, -0.02f), new Vector3(1.15f, 0.5f, 0.78f), rockA, Vector3.zero, 0);

            // ---- 軀幹（樞紐在腰）
            spine = Pivot("Spine", body, new Vector3(0f, 1.3f, 0f));
            Part(PrimitiveType.Cube, spine, new Vector3(0f, 0.2f, 0.02f), new Vector3(1f, 0.5f, 0.72f), rockC, new Vector3(6f, 0f, 0f));
            Part(PrimitiveType.Cube, spine, new Vector3(0f, 0.45f, 0.02f), new Vector3(1.05f, 0.1f, 0.76f), lavaM);        // 腰間熔岩縫
            var chest = Part(PrimitiveType.Cube, spine, new Vector3(0f, 0.95f, 0f), new Vector3(1.6f, 1f, 1.05f), rockA, new Vector3(-6f, 0f, 0f));
            CrackOn(chest.transform, CrackMesh("chest", 11, true, 8, 0.045f), new Vector3(0f, 0f, 0.503f), Vector3.zero);
            RockPart(spine, new Vector3(0f, 1.38f, -0.3f), new Vector3(1.35f, 0.7f, 0.85f), rockB, new Vector3(25f, 0f, 0f), 1);   // 駝背
            for (int s = -1; s <= 1; s += 2)
            {
                RockPart(spine, new Vector3(0.95f * s, 1.42f, -0.02f), new Vector3(0.78f, 0.66f, 0.85f), rockB, new Vector3(0f, 0f, 18f * s), 2);   // 肩上巨岩
                RockPart(spine, new Vector3(0.38f * s, 1.78f, -0.45f), new Vector3(0.26f, 0.62f, 0.28f), rockC, new Vector3(-35f, 0f, 14f * s), 0); // 背上岩刺
            }
            // 胸口核心＋兩片會打開的胸甲
            core = Pivot("Core", spine, new Vector3(0f, 0.9f, 0.5f));
            Part(PrimitiveType.Sphere, core, Vector3.zero, Vector3.one * 0.42f, coreM);
            Part(PrimitiveType.Sphere, core, new Vector3(0f, 0f, 0.12f), Vector3.one * 0.22f, hotM);
            plateL = RockPart(spine, new Vector3(-0.2f, 0.9f, 0.56f), new Vector3(0.3f, 0.55f, 0.22f), rockB, Vector3.zero, 1).transform;
            plateR = RockPart(spine, new Vector3(0.2f, 0.9f, 0.56f), new Vector3(0.3f, 0.55f, 0.22f), rockB, Vector3.zero, 2).transform;
            plateRestL = plateL.localPosition; plateRestR = plateR.localPosition;

            // ---- 頭（脖子 → 頭 → 下巴），頭低低地埋在兩肩之間
            neck = Pivot("Neck", spine, new Vector3(0f, 1.5f, 0.4f));
            head = Pivot("Head", neck, new Vector3(0f, 0.1f, 0.12f));
            RockPart(head, new Vector3(0f, 0.1f, 0f), new Vector3(0.62f, 0.5f, 0.6f), rockB, Vector3.zero, 0);
            Part(PrimitiveType.Cube, head, new Vector3(0f, 0.24f, 0.22f), new Vector3(0.74f, 0.14f, 0.24f), rockC, new Vector3(18f, 0f, 0f));   // 眉骨
            for (int s = -1; s <= 1; s += 2)
                Part(PrimitiveType.Cube, head, new Vector3(0.15f * s, 0.14f, 0.31f), new Vector3(0.15f, 0.065f, 0.06f), eyeM, new Vector3(0f, 0f, 14f * s));   // 斜吊的怒眼
            Part(PrimitiveType.Cube, head, new Vector3(0f, -0.06f, 0.16f), new Vector3(0.42f, 0.08f, 0.3f), lavaM);   // 嘴裡的熔岩（張嘴才看得到）
            jaw = Pivot("Jaw", head, new Vector3(0f, -0.06f, 0.04f));
            RockPart(jaw, new Vector3(0f, -0.1f, 0.1f), new Vector3(0.56f, 0.22f, 0.5f), rockC, Vector3.zero, 2);

            // ---- 手臂（肩 → 肘 → 手腕），前臂與拳頭特別巨大
            shL = BuildArm(-1f, out elL, out wrL);
            shR = BuildArm(1f, out elR, out wrR);

            // ---- 特效：火星、核心光暈、腳步揚塵、熔岩噴吐、腳下熱光
            embers = SpellFx.NewPS(spine, "Embers", SpellFx.AddMat(SpellFx.TexSoft), 60, true);
            embers.transform.localPosition = new Vector3(0f, 0.7f, 0.1f);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.08f), new Color(1f, 0.8f, 0.35f));
                SpellFx.Sphere(embers, 1.0f);
                SpellFx.FadeInOut(embers);
                SpellFx.SizeCurve(embers, SpellFx.Shrink);
                SpellFx.Rise(embers, 0.45f);
                SpellFx.Noise(embers, 0.3f, 1.2f);
                SpellFx.Rate(embers, 5f);
                embers.Play();
            }
            halo = SpellFx.NewPS(core, "CoreHalo", SpellFx.AddMat(SpellFx.TexSoft), 8, false);
            {
                var m = halo.main;
                m.startLifetime = 0.7f;
                m.startSize = 1f;
                m.startColor = new Color(1f, 0.45f, 0.1f, 0.5f);
                SpellFx.FadeInOut(halo);
                SpellFx.Rate(halo, 6f);
                halo.Play();
            }
            stepDust = SpellFx.NewPS(transform, "StepDust", SpellFx.BlendMat(SpellFx.TexSoft), 30, true);
            {
                var m = stepDust.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.36f, 0.32f, 0.5f), new Color(0.3f, 0.26f, 0.24f, 0.4f));
                SpellFx.FadeOut(stepDust);
                SpellFx.SizeCurve(stepDust, SpellFx.Grow);
                SpellFx.Drag(stepDust, 3f);
                stepDust.Play();
            }
            spew = SpellFx.NewPS(core, "Spew", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            {
                var m = spew.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.06f), new Color(1f, 0.85f, 0.4f));
                m.gravityModifier = 0.5f;
                SpellFx.Cone(spew, 25f, 0.12f);
                SpellFx.FadeOut(spew);
                SpellFx.SizeCurve(spew, SpellFx.Shrink);
                SpellFx.Stretch(spew, 0.04f, 1.5f);
                spew.Play();
            }
            heatM = new Material(SpellFx.AddMat(SpellFx.TexSoft)) { name = "BossHeat" };
            var heat = new GameObject("HeatGlow");
            heat.transform.SetParent(transform, false);
            heat.transform.localPosition = new Vector3(0f, 0.02f, 0.1f);
            heat.transform.localScale = new Vector3(1.9f, 1f, 1.9f);
            heat.AddComponent<MeshFilter>().sharedMesh = GroundQuad();
            var hr = heat.AddComponent<MeshRenderer>();
            hr.sharedMaterial = heatM;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hr.receiveShadows = false;

            rends = GetComponentsInChildren<Renderer>(true);

            parts = partList.ToArray();
            partList = null;
            int n = parts.Length;
            partPos = new Vector3[n]; partScale = new Vector3[n]; partRot = new Quaternion[n];
            vel = new Vector3[n]; angVel = new Vector3[n]; partRad = new float[n]; landed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                partPos[i] = parts[i].localPosition;
                partRot[i] = parts[i].localRotation;
                partScale[i] = parts[i].localScale;
            }

            cur = Idle();
            Animate(0f);
        }

        Transform BuildLeg(float s, out Transform knee, out Transform ankle)
        {
            string side = s < 0f ? "L" : "R";
            var hip = Pivot("Hip" + side, body, new Vector3(0.42f * s, HipY, 0f));
            var thigh = Part(PrimitiveType.Cube, hip, new Vector3(0.02f * s, -0.27f, 0f), new Vector3(0.5f, 0.6f, 0.52f), rockC, new Vector3(0f, 8f * s, 4f * s));
            CrackOn(thigh.transform, CrackMesh("thigh", 21, false, 1, 0.07f), new Vector3(0.503f * s, 0f, 0f), new Vector3(0f, 90f * s, 0f));
            knee = Pivot("Knee" + side, hip, new Vector3(0f, -L1, 0f));
            Part(PrimitiveType.Sphere, knee, new Vector3(0f, 0f, 0.03f), Vector3.one * 0.34f, lavaM);
            RockPart(knee, new Vector3(0f, -0.22f, 0.02f), new Vector3(0.56f, 0.55f, 0.58f), rockA, new Vector3(0f, 15f * s, 0f), 1);
            ankle = Pivot("Ankle" + side, knee, new Vector3(0f, -L2, 0f));
            RockPart(ankle, new Vector3(0f, 0.01f, 0.1f), new Vector3(0.7f, 0.24f, 0.9f), rockC, Vector3.zero, 2);
            Part(PrimitiveType.Cube, ankle, new Vector3(0f, -0.04f, 0.5f), new Vector3(0.62f, 0.16f, 0.28f), rockA, new Vector3(12f, 0f, 0f));
            return hip;
        }

        Transform BuildArm(float s, out Transform elbow, out Transform wrist)
        {
            string side = s < 0f ? "L" : "R";
            var sh = Pivot("Shoulder" + side, spine, new Vector3(1f * s, 1.25f, 0f));
            Part(PrimitiveType.Sphere, sh, new Vector3(0f, -0.1f, 0f), Vector3.one * 0.4f, lavaM);
            Part(PrimitiveType.Cube, sh, new Vector3(0f, -0.4f, 0f), new Vector3(0.46f, 0.78f, 0.5f), rockC, new Vector3(0f, 10f * s, 0f));
            elbow = Pivot("Elbow" + side, sh, new Vector3(0f, -0.8f, 0f));
            Part(PrimitiveType.Sphere, elbow, Vector3.zero, Vector3.one * 0.4f, lavaM);
            var fore = Part(PrimitiveType.Cube, elbow, new Vector3(0f, -0.46f, 0.02f), new Vector3(0.8f, 0.9f, 0.8f), rockA, new Vector3(0f, 14f * s, 0f));
            CrackOn(fore.transform, CrackMesh("foreF", 31, false, 2, 0.06f), new Vector3(0f, 0f, 0.503f), Vector3.zero);
            CrackOn(fore.transform, CrackMesh("foreO", 37, false, 2, 0.06f), new Vector3(0.503f * s, 0f, 0f), new Vector3(0f, 90f * s, 0f));
            wrist = Pivot("Wrist" + side, elbow, new Vector3(0f, -0.92f, 0f));
            Part(PrimitiveType.Cube, wrist, new Vector3(0f, 0.02f, 0f), new Vector3(0.72f, 0.1f, 0.72f), lavaM);   // 手腕熔岩縫
            RockPart(wrist, new Vector3(0f, -0.27f, 0.03f), new Vector3(0.92f, 0.72f, 0.92f), rockB, Vector3.zero, 1);
            Part(PrimitiveType.Cube, wrist, new Vector3(0f, -0.42f, 0.33f), new Vector3(0.78f, 0.26f, 0.28f), rockC, new Vector3(25f, 0f, 0f));   // 指節
            return sh;
        }

        // ================================================================ 動作
        void Update() { Animate(Time.deltaTime); }

        void Animate(float dt)
        {
            if (body == null) return;
            clock += dt;
            if (clock > 3600f) clock -= 3600f;

            enr = Mathf.MoveTowards(enr, enraged && !dead ? 1f : 0f, dt * 0.8f);
            float hitK = 0f;
            if (hitT > 0f) { hitT -= dt; hitK = Mathf.Clamp01(hitT / HitDur); }
            if (dead) deadT += dt;

            // ---- 走路相位：一個循環（左右各一步）約前進 1.6 m
            float speed = dead ? 0f : Mathf.Clamp(MoveSpeed, 0f, 3f);
            bool acting = phase != PhaseNone;
            walkAmt = Mathf.MoveTowards(walkAmt, acting ? 0f : Mathf.Clamp01(speed / 0.5f), dt * 2.5f);
            if (speed > 0.01f && !acting) walkPhase += dt * Mathf.PI * 2f * Mathf.Max(speed, 0.35f) / 1.6f;
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 2f);
            if (walkPhase > 1000f) walkPhase -= Mathf.PI * 300f;
            float sinP = Mathf.Sin(walkPhase), cosP = Mathf.Cos(walkPhase);

            // 腳落地（擺到最前／最後）時：揚塵＋身體一震
            if (walkAmt > 0.35f && (cosP > 0f) != (lastCos > 0f) && !dead)
            {
                jolt = 1f;
                var foot = sinP > 0f ? anL : anR;
                StepDust(foot.position + foot.forward * 0.15f);
            }
            lastCos = cosP;
            jolt = Mathf.MoveTowards(jolt, 0f, dt * 6f);

            // ---- 基本姿勢：待機呼吸＋走路擺動
            float breathRate = Mathf.Lerp(1.4f, 2.4f, enr);
            float breath = Mathf.Sin(clock * breathRate);
            var basePose = Idle();
            basePose.spX += breath * 1.5f + enr * 4f + jolt * 3f + walkAmt * 4f;
            basePose.spY = sinP * 7f * walkAmt + Mathf.Sin(clock * 0.37f) * 3f * (1f - walkAmt);
            basePose.head += -breath * 1.5f + Mathf.Sin(clock * 0.53f) * 2f;
            float armSwing = sinP * 16f * walkAmt;
            basePose.shL.x += armSwing; basePose.shR.x -= armSwing;
            basePose.shL.z += breath * 2.5f; basePose.shR.z += breath * 2.5f;
            basePose.glow = enr * 0.2f;

            // ---- 攻擊：預備 → 維持（顫抖）→ 出手 → 回待機
            Pose target = basePose;
            float shake = 0f;
            if (phase == PhaseWindup)
            {
                atkT += dt;
                float x = Mathf.Clamp01(atkT / atkDur);
                float w = x * x * (3f - 2f * x);
                target = Pose.Lerp(basePose, WindupPose(atk), w);
                // 蓄力中越來越抖，提示快出手了
                float tr = Mathf.Sin(clock * 29f) * (0.4f + 1.4f * x);
                target.shL.x += tr; target.shR.x -= tr; target.spZ += tr * 0.4f;
            }
            else if (phase == PhaseStrike)
            {
                atkT += dt;
                var sp = StrikePose(atk);
                if (atkT < StrikeIn)
                {
                    float k = atkT / StrikeIn;
                    k = 1f - (1f - k) * (1f - k) * (1f - k);
                    target = Pose.Lerp(snap, sp, k);
                }
                else if (atkT < StrikeIn + StrikeHold)
                {
                    target = sp;
                    if (atk != "b_sweep") shake = Mathf.Sin(atkT * 90f) * 0.025f * (1f - (atkT - StrikeIn) / StrikeHold);
                }
                else if (atkT < StrikeIn + StrikeHold + StrikeOut)
                {
                    float k = (atkT - StrikeIn - StrikeHold) / StrikeOut;
                    target = Pose.Lerp(sp, basePose, k * k * (3f - 2f * k));
                }
                else phase = PhaseNone;
            }

            // ---- 受擊：後仰
            if (hitK > 0f) { target.spX -= 9f * hitK; target.head -= 8f * hitK; target.shL.z += 6f * hitK; target.shR.z += 6f * hitK; }

            // ---- 死亡：跪倒前撲，之後崩解
            if (dead)
            {
                float k = Mathf.Clamp01(deadT / CollapseAt);
                target = Pose.Lerp(target, DeadPose(), k * k * (3f - 2f * k));
                if (deadT >= CollapseAt && !crumbled) Crumble();
            }
            cur = target;

            if (!crumbled)
            {
                float bob = walkAmt * 0.06f * cosP * cosP - jolt * 0.025f * walkAmt;
                ApplyPose(cur, bob + shake, -cosP * 3.5f * walkAmt, walkAmt, sinP, cosP);
            }
            else UpdateRubble(dt);

            UpdateGlow(hitK);
        }

        void ApplyPose(in Pose p, float bob, float roll, float wAmt, float sinP, float cosP)
        {
            body.localPosition = new Vector3(0f, -p.crouch + bob, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, roll);

            // 腿：依下蹲量解兩節 IK（腳踝留在髖正下方），再加走路的前後擺與抬膝
            float T = L1 + L2 - p.crouch + bob;
            float swing = 22f * wAmt;
            LegIK(hipL, knL, anL, T, sinP * swing, Mathf.Max(0f, cosP) * 30f * wAmt);
            LegIK(hipR, knR, anR, T, -sinP * swing, Mathf.Max(0f, -cosP) * 30f * wAmt);

            spine.localRotation = Quaternion.Euler(p.spX, p.spY, p.spZ);
            neck.localRotation = Quaternion.Euler(p.head, -p.spY * 0.3f, 0f);
            jaw.localRotation = Quaternion.Euler(p.jaw * 28f, 0f, 0f);

            shL.localRotation = Quaternion.Euler(p.shL.x, -p.shL.y, -p.shL.z);
            shR.localRotation = Quaternion.Euler(p.shR.x, p.shR.y, p.shR.z);
            elL.localRotation = Quaternion.Euler(p.elL, 0f, 0f);
            elR.localRotation = Quaternion.Euler(p.elR, 0f, 0f);
            wrL.localRotation = Quaternion.Euler(p.wrL, 0f, 0f);
            wrR.localRotation = Quaternion.Euler(p.wrR, 0f, 0f);

            // 胸甲像兩扇門往外打開
            float o = p.open;
            plateL.localPosition = plateRestL + new Vector3(-0.22f * o, 0f, 0.04f * o);
            plateR.localPosition = plateRestR + new Vector3(0.22f * o, 0f, 0.04f * o);
            plateL.localRotation = Quaternion.Euler(0f, -40f * o, 0f);
            plateR.localRotation = Quaternion.Euler(0f, 40f * o, 0f);
        }

        static void LegIK(Transform hip, Transform knee, Transform ankle, float T, float swing, float lift)
        {
            T = Mathf.Clamp(T, 0.3f, (L1 + L2) * 0.998f);
            float cg = Mathf.Clamp((L1 * L1 + L2 * L2 - T * T) / (2f * L1 * L2), -1f, 1f);
            float bend = 180f - Mathf.Acos(cg) * Mathf.Rad2Deg;
            float ca = Mathf.Clamp((L1 * L1 + T * T - L2 * L2) / (2f * L1 * T), -1f, 1f);
            float alpha = Mathf.Acos(ca) * Mathf.Rad2Deg;
            float hx = -alpha + swing - lift * 0.5f;
            float kx = bend + lift;
            hip.localRotation = Quaternion.Euler(hx, 0f, 0f);
            knee.localRotation = Quaternion.Euler(kx, 0f, 0f);
            ankle.localRotation = Quaternion.Euler(-(hx + kx), 0f, 0f);   // 腳掌保持平貼
        }

        /// <summary>熔岩、核心、眼睛的顏色（脈動、狂暴、蓄力、受擊、死亡熄滅），以及火星量與腳下熱光。</summary>
        void UpdateGlow(float hitK)
        {
            float pulseRate = Mathf.Lerp(2.2f, 6.5f, enr);
            float pulse = 0.5f + 0.5f * Mathf.Sin(clock * pulseRate) + 0.08f * Mathf.Sin(clock * 13.7f);
            float dim = dead ? Mathf.Clamp01(deadT / 2.5f) : 0f;
            float g = Mathf.Clamp01(cur.glow);

            var lavaBase = Color.Lerp(LavaN, LavaE, enr);
            var hotBase = Color.Lerp(HotN, HotE, enr);
            var lava = Color.Lerp(LavaDim, lavaBase, Mathf.Clamp01(0.55f + 0.45f * pulse + 0.5f * g));
            lava = Color.Lerp(lava, hotBase, Mathf.Clamp01(g * 0.35f + hitK * 0.5f));
            var hot = Color.Lerp(hotBase, Color.white, Mathf.Clamp01(g * 0.5f + hitK * 0.4f));
            var coreC = Color.Lerp(lavaBase, hotBase, Mathf.Clamp01(0.3f + 0.4f * pulse + 0.6f * g));
            var eye = Color.Lerp(Color.Lerp(EyeN, EyeE, enr), Color.white, g * 0.4f);
            if (dim > 0f)
            {
                lava = Color.Lerp(lava, DeadEmber, dim);
                hot = Color.Lerp(hot, DeadEmber, dim);
                coreC = Color.Lerp(coreC, DeadEmber, dim);
                eye = Color.Lerp(eye, DeadEmber, Mathf.Clamp01(dim * 2f));
            }
            lavaM.color = lava; hotM.color = hot; coreM.color = coreC; eyeM.color = eye;

            if (core != null && !crumbled) core.localScale = Vector3.one * (1f + 0.08f * pulse + 0.3f * g);

            // 受擊時岩石泛紅
            if (hitK > 0f || flashing)
            {
                flashing = hitK > 0f;
                float f = hitK * 0.75f;
                rockA.color = Color.Lerp(RockAC, FlashC, f);
                rockB.color = Color.Lerp(RockBC, FlashC, f);
                rockC.color = Color.Lerp(RockCC, FlashC, f);
            }

            // 核心光暈
            if (halo != null)
            {
                var m = halo.main;
                m.startSize = (0.9f + 0.9f * g + 0.3f * enr + 0.15f * pulse) * (1f - dim);
                var hc = Color.Lerp(lavaBase, hotBase, g);
                hc.a = (0.35f + 0.25f * pulse + 0.3f * g) * (1f - dim);
                m.startColor = hc;
            }

            // 火星量：狂暴時變多，死亡時停止
            float rate = dead ? 0f : Mathf.Lerp(5f, 16f, enr);
            if (embers != null && !Mathf.Approximately(rate, emberRate))
            {
                emberRate = rate;
                SpellFx.Rate(embers, rate);
            }

            // 腳下熱光
            if (heatM != null)
            {
                float a = (0.2f + 0.12f * enr + 0.06f * pulse + 0.15f * g) * (1f - dim);
                var c = new Color(0.5f, Mathf.Lerp(0.17f, 0.32f, enr), 0.04f, a);
                heatM.SetColor(TintId, c);
                heatM.SetColor(ColorId, c);
            }
        }

        void StepDust(Vector3 pos)
        {
            if (stepDust == null || !visible) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < 6; i++)
            {
                float a = i * (Mathf.PI * 2f / 6f) + Random.Range(-0.3f, 0.3f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ep.position = pos + d * 0.2f + Vector3.up * 0.05f;
                ep.velocity = d * Random.Range(0.6f, 1.2f) + Vector3.up * 0.15f;
                stepDust.Emit(ep, 1);
            }
        }

        // ================================================================ 死亡崩解
        void Crumble()
        {
            crumbled = true;
            var c = transform.position;
            var up = transform.up;
            for (int i = 0; i < parts.Length; i++)
            {
                var t = parts[i];
                var d = t.position - c;
                d -= up * Vector3.Dot(d, up);
                if (d.sqrMagnitude < 1e-4f) d = transform.forward;
                vel[i] = d.normalized * Random.Range(0.3f, 1.3f) + up * Random.Range(0f, 1.4f);
                angVel[i] = Random.insideUnitSphere * 260f;
                var ls = t.lossyScale;
                partRad[i] = Mathf.Min(ls.x, Mathf.Min(ls.y, ls.z)) * 0.42f;
                landed[i] = false;
            }
            SlamShockwave(c, 2.4f, new Color(0.9f, 0.3f, 0.06f));
            for (int k = 0; k < 3; k++) StepDust(c + transform.forward * (0.6f * k - 0.3f));
        }

        void UpdateRubble(float dt)
        {
            float h = Mathf.Min(dt, 0.05f);
            float ground = transform.position.y;
            float settle = Mathf.Clamp01((deadT - CollapseAt - 2f) / 3f);   // 落地後慢慢下沉一點（陷進碎石堆）
            for (int i = 0; i < parts.Length; i++)
            {
                var t = parts[i];
                if (t == null) continue;
                var p = t.position;
                float floorY = ground + partRad[i] * (1f - 0.5f * settle);
                if (!landed[i])
                {
                    vel[i].y -= 9.8f * h;
                    p += vel[i] * h;
                    var av = angVel[i];
                    float mag = av.magnitude;
                    if (mag > 1e-3f) t.rotation = Quaternion.AngleAxis(mag * h, av / mag) * t.rotation;
                    if (p.y <= floorY)
                    {
                        p.y = floorY;
                        if (vel[i].y < -2f)
                        {
                            vel[i] = new Vector3(vel[i].x * 0.4f, -vel[i].y * 0.25f, vel[i].z * 0.4f);
                            angVel[i] *= 0.4f;
                        }
                        else landed[i] = true;
                    }
                    t.position = p;
                }
                else if (p.y > floorY + 1e-4f)
                {
                    p.y = Mathf.MoveTowards(p.y, floorY, h * 0.1f);
                    t.position = p;
                }
            }
        }

        void Revive()
        {
            deadT = 0f;
            if (crumbled)
            {
                crumbled = false;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == null) continue;
                    parts[i].localPosition = partPos[i];
                    parts[i].localRotation = partRot[i];
                    parts[i].localScale = partScale[i];
                }
            }
            cur = Idle();
        }

        void OnDestroy()
        {
            DestroyMat(rockA); DestroyMat(rockB); DestroyMat(rockC);
            DestroyMat(lavaM); DestroyMat(hotM); DestroyMat(coreM); DestroyMat(eyeM); DestroyMat(heatM);
        }

        static void DestroyMat(Material m) { if (m != null) Destroy(m); }

        // ================================================================ 建構小工具
        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler = default)
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
            partList?.Add(go.transform);
            return go;
        }

        /// <summary>圓潤但有稜角的岩塊（程式產生的扭曲多面體，大小約 1×1×1，靠縮放決定尺寸）。</summary>
        GameObject RockPart(Transform parent, Vector3 localPos, Vector3 localScale, Material mat, Vector3 euler, int variant)
        {
            var go = new GameObject("Rock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = RockMesh(variant);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            partList?.Add(go.transform);
            return go;
        }

        /// <summary>把裂縫網格貼在方塊的某一面上（方塊的單位座標，面在 ±0.5）。外圈用熔岩色、內芯用高溫色。</summary>
        void CrackOn(Transform cube, Mesh mesh, Vector3 localPos, Vector3 euler)
        {
            var go = new GameObject("Crack");
            go.transform.SetParent(cube, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { lavaM, hotM };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static Mesh RockMesh(int variant)
        {
            string key = "rock" + variant;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = new PMesh(1);
            const int rings = 6;
            var prof = new float[(rings + 1) * 2];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings;
                prof[i * 2] = -Mathf.Cos(a) * 0.5f;
                prof[i * 2 + 1] = Mathf.Sin(a) * 0.5f;
            }
            float seed = variant * 1.37f + 0.5f;
            m.Lathe(prof, 7, null, 1f, 1f, variant * 17f, null, Vector3.zero, Quaternion.identity, p => RockWarp(p, seed));
            var mesh = m.Build("BossRock" + variant, true);
            meshCache[key] = mesh;
            return mesh;
        }

        /// <summary>球面扭曲＋各軸截平：做出凹凸、有平面切面的岩塊，最後約為 1 單位大小。</summary>
        static Vector3 RockWarp(Vector3 p, float seed)
        {
            float n = Mathf.Sin(p.x * 9.1f + seed * 1.7f) * Mathf.Sin(p.y * 7.3f + seed * 2.9f) * Mathf.Sin(p.z * 8.7f + seed * 0.6f);
            float n2 = Mathf.Sin((p.x + p.y - p.z) * 13f + seed * 3.1f);
            p *= 1f + 0.24f * n + 0.07f * n2;
            p.x = Mathf.Clamp(p.x, -0.46f, 0.46f);
            p.y = Mathf.Clamp(p.y, -0.46f, 0.46f);
            p.z = Mathf.Clamp(p.z, -0.46f, 0.46f);
            return p / 0.92f;
        }

        /// <summary>
        /// 熔岩裂縫網格（本地 XY 平面、在 [-0.5, 0.5] 內，面向 +Z）。radial：從中心（胸口核心）往外放射；否則為上下貫穿的縱向裂縫。
        /// 子網格 0 = 外圈（熔岩色），子網格 1 = 內芯（高溫色，稍微往前浮一點）。
        /// </summary>
        static Mesh CrackMesh(string key, int seed, bool radial, int lines, float w)
        {
            key = "crack_" + key;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var r = new System.Random(seed);
            var m = new PMesh(2);
            if (radial)
            {
                var c = new Vector2(0f, -0.05f);
                for (int i = 0; i < lines; i++)
                {
                    float a = (360f * i / lines + 12f + Rnd(r, -14f, 14f)) * Mathf.Deg2Rad;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float t = RayToBox(c, d, 0.47f);
                    var pts = Jagged(r, c + d * 0.24f, c + d * t, 6, 0.035f);
                    Crack(m, pts, w, true);
                    if ((i & 1) == 0)
                    {
                        float ba = a + (Rnd(r, 0f, 1f) > 0.5f ? 0.6f : -0.6f);
                        var bd = new Vector2(Mathf.Cos(ba), Mathf.Sin(ba));
                        var b0 = pts[2];
                        var b1 = b0 + bd * Mathf.Min(0.16f, RayToBox(b0, bd, 0.47f));
                        Crack(m, Jagged(r, b0, b1, 3, 0.02f), w * 0.6f, true);
                    }
                }
            }
            else
            {
                for (int i = 0; i < lines; i++)
                {
                    float x = lines == 1 ? Rnd(r, -0.1f, 0.1f) : -0.2f + 0.4f * i / (lines - 1) + Rnd(r, -0.06f, 0.06f);
                    var pts = Jagged(r, new Vector2(x + Rnd(r, -0.1f, 0.1f), 0.48f), new Vector2(x + Rnd(r, -0.1f, 0.1f), -0.48f), 7, 0.07f);
                    Crack(m, pts, w, false);
                    var b0 = pts[2 + (i & 1) * 2];
                    float side = b0.x > 0f ? -1f : 1f;
                    var b1 = b0 + new Vector2(side * Rnd(r, 0.15f, 0.25f), Rnd(r, -0.15f, 0.05f));
                    b1.x = Mathf.Clamp(b1.x, -0.47f, 0.47f);
                    Crack(m, Jagged(r, b0, b1, 3, 0.03f), w * 0.6f, true);
                }
            }
            var mesh = m.Build("BossCrack " + key, false);
            meshCache[key] = mesh;
            return mesh;
        }

        static float Rnd(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static float RayToBox(Vector2 c, Vector2 d, float half)
        {
            float t = 10f;
            if (Mathf.Abs(d.x) > 1e-4f) t = Mathf.Min(t, ((d.x > 0f ? half : -half) - c.x) / d.x);
            if (Mathf.Abs(d.y) > 1e-4f) t = Mathf.Min(t, ((d.y > 0f ? half : -half) - c.y) / d.y);
            return Mathf.Max(0f, t);
        }

        static Vector2[] Jagged(System.Random r, Vector2 a, Vector2 b, int n, float jit)
        {
            var pts = new Vector2[n];
            var d = b - a;
            var perp = new Vector2(-d.y, d.x).normalized;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1);
                pts[i] = a + d * t + (i == 0 || i == n - 1 ? Vector2.zero : perp * Rnd(r, -jit, jit));
            }
            return pts;
        }

        /// <summary>一條裂縫：外圈寬 w、內芯寬 0.35w；taperOut＝從起點往終點變細，否則兩端細中間粗。</summary>
        static void Crack(PMesh m, Vector2[] pts, float w, bool taperOut)
        {
            int n = pts.Length;
            for (int layer = 0; layer < 2; layer++)
            {
                m.sub = layer;
                float lw = layer == 0 ? w : w * 0.35f;
                float z = layer == 0 ? 0f : 0.004f;
                for (int i = 0; i < n - 1; i++)
                {
                    var a = pts[i]; var b = pts[i + 1];
                    var na = Normal(pts, i) * lw * Width(i, n, taperOut) * 0.5f;
                    var nb = Normal(pts, i + 1) * lw * Width(i + 1, n, taperOut) * 0.5f;
                    m.Quad(new Vector3(a.x - na.x, a.y - na.y, z), new Vector3(a.x + na.x, a.y + na.y, z),
                           new Vector3(b.x + nb.x, b.y + nb.y, z), new Vector3(b.x - nb.x, b.y - nb.y, z), Color.white);
                }
            }
            m.sub = 0;
        }

        static float Width(int i, int n, bool taperOut)
        {
            float t = (float)i / (n - 1);
            return taperOut ? Mathf.Lerp(1f, 0.25f, t) : 0.3f + 0.7f * Mathf.Sin(Mathf.PI * t);
        }

        static Vector2 Normal(Vector2[] pts, int i)
        {
            var d = pts[Mathf.Min(i + 1, pts.Length - 1)] - pts[Mathf.Max(i - 1, 0)];
            return new Vector2(-d.y, d.x).normalized;
        }

        /// <summary>水平四邊形（XZ 平面，±1），給腳下的熱光用。</summary>
        static Mesh GroundQuad()
        {
            const string key = "groundQuad";
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = new Mesh { name = "BossGroundQuad" };
            m.vertices = new[] { new Vector3(-1f, 0f, -1f), new Vector3(-1f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 0f, -1f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            meshCache[key] = m;
            return m;
        }

        // ================================================================ 頭目的飛行物外觀
        static Material projRock, projLava, projHot, fxAdd;
        static readonly float[] JagF = { 1f, 0.78f, 1.12f, 0.85f, 1.05f, 0.74f, 1.15f, 0.9f, 0.8f };

        static void InitProjMats()
        {
            InitShaders();
            if (projRock == null) projRock = new Material(litShader) { color = new Color(0.26f, 0.21f, 0.19f), name = "BossProjRock" };
            if (projLava == null) projLava = new Material(glowShader) { color = new Color(1f, 0.42f, 0.08f), name = "BossProjLava" };
            if (projHot == null) projHot = new Material(glowShader) { color = new Color(1f, 0.86f, 0.5f), name = "BossProjHot" };
            if (fxAdd == null) fxAdd = new Material(SpellFx.AddMat(SpellFx.TexSoft).shader) { name = "BossFxAdd", mainTexture = Texture2D.whiteTexture };
        }

        /// <summary>
        /// 頭目招式的飛行物外觀（+Z＝飛行方向）。呼叫者每幀設定位置與旋轉，結束時 Destroy。未知的 id 回傳 null。
        ///   b_bolt：約 0.35 m 的熔岩球（暗色岩殼碎塊、發光核心、火焰拖尾、滴落的火星）。
        ///   b_sweep：約 2.4 m 寬、0.5 m 高的水平新月衝擊波（火焰＋翻滾岩塊），一看就知道要蹲下。
        ///   b_rain：約 0.6 m 的燃燒巨石（煙＋火拖尾），+Z 朝下落下；外型對 Z 軸對稱，翻滾角度不影響外觀。
        ///   m_fireball：火焰射手小鬼的青白色小火球（約 0.18 m）＋短拖尾。
        ///   m_rock：岩石投擲者丟出的約 0.35 m 燃燒岩塊（與 b_rain 同造型、縮小；+Z 朝下落下，翻滾角度不影響外觀）。
        ///   m_snipe：熔岩狙擊手的細長洋紅光彈（約 0.5 m 沿 +Z）＋短拖尾。
        /// </summary>
        public static GameObject CreateProjectileVisual(string skillId)
        {
            switch (skillId)
            {
                case "b_bolt": return BuildBolt();
                case "b_sweep": return BuildSweep();
                case "b_rain": return BuildRain("b_rain", 1f);
                case "m_rock": return BuildRain("m_rock", 0.58f);
                case "m_snipe": return BuildSnipe();
                case "m_fireball": return BuildImpFireball();
                default: return null;
            }
        }

        static GameObject BuildBolt()
        {
            InitProjMats();
            var root = new GameObject("Boss Projectile b_bolt");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;

            // 自轉的熔岩球＋岩殼碎塊
            var spin = Pivot("Spin", rt, Vector3.zero);
            ProjPart(PrimitiveType.Sphere, spin, Vector3.zero, Vector3.one * 0.3f, projLava);
            ProjPart(PrimitiveType.Sphere, spin, new Vector3(0f, 0f, 0.07f), Vector3.one * 0.18f, projHot);
            var dirs = new[] { new Vector3(0.8f, 0.5f, 0.2f), new Vector3(-0.7f, 0.6f, -0.3f), new Vector3(0.1f, -0.9f, 0.3f), new Vector3(-0.5f, -0.4f, -0.7f), new Vector3(0.6f, -0.2f, -0.7f) };
            for (int i = 0; i < dirs.Length; i++)
            {
                var d = dirs[i].normalized;
                var go = new GameObject("Crust");
                go.transform.SetParent(spin, false);
                go.transform.localPosition = d * 0.135f;
                go.transform.localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(0f, 0f, i * 47f);
                go.transform.localScale = new Vector3(0.15f, 0.13f, 0.08f) * (0.85f + 0.08f * i);
                go.AddComponent<MeshFilter>().sharedMesh = RockMesh(i % 3);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = projRock;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // 火焰尾巴（往 -Z 拖）
            var tail = Pivot("Tail", rt, Vector3.zero);
            GlowMesh(tail, "bolt.tail", m =>
            {
                m.Lathe(new[] { -1.0f, 0f, -0.55f, 0.1f, -0.2f, 0.17f, 0.05f, 0.21f, 0.18f, 0.15f, 0.25f, 0f }, 9,
                    (z, j) => new Color(1f, 0.22f, 0.04f, Mathf.Lerp(0.05f, 0.45f, Mathf.InverseLerp(-1f, 0.1f, z))), 1f, 1f, 0f, JagF);
                m.Lathe(new[] { -0.7f, 0f, -0.35f, 0.08f, -0.1f, 0.13f, 0.08f, 0.15f, 0.2f, 0f }, 9,
                    (z, j) => new Color(1f, 0.6f, 0.15f, Mathf.Lerp(0.1f, 0.75f, Mathf.InverseLerp(-0.7f, 0.1f, z))), 1f, 1f, 20f, JagF);
            });
            Halo(rt, new Color(1f, 0.5f, 0.12f, 0.75f), 0.85f);

            var trail = SpellFx.NewPS(rt, "FireTrail", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.15f, 0.8f), new Color(1f, 0.25f, 0.05f, 0.7f));
                SpellFx.Sphere(trail, 0.08f);
                SpellFx.FadeOut(trail);
                SpellFx.SizeCurve(trail, SpellFx.Shrink);
                SpellFx.Rate(trail, 14f);
                var em = trail.emission; em.rateOverDistance = 18f;
                trail.Play();
            }
            var drip = SpellFx.NewPS(rt, "Drips", SpellFx.AddMat(SpellFx.TexSoft), 20, true);
            {
                var m = drip.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.5f, 0.1f), new Color(1f, 0.85f, 0.4f));
                m.gravityModifier = 0.6f;
                SpellFx.Sphere(drip, 0.14f, 0.2f);
                SpellFx.FadeOut(drip);
                SpellFx.Stretch(drip, 0.06f, 1.2f);
                SpellFx.Rate(drip, 14f);
                drip.Play();
            }
            var smoke = SpellFx.NewPS(rt, "Smoke", SpellFx.BlendMat(SpellFx.TexSoft), 12, true);
            {
                var m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.16f, 0.14f, 0.55f), new Color(0.1f, 0.08f, 0.08f, 0.45f));
                SpellFx.Sphere(smoke, 0.1f);
                SpellFx.FadeOut(smoke);
                SpellFx.SizeCurve(smoke, SpellFx.Grow);
                SpellFx.Rise(smoke, 0.4f);
                SpellFx.Rate(smoke, 8f);
                smoke.Play();
            }

            anim.Setup(new[] { spin }, new[] { new Vector3(0.3f, 0.2f, 1f).normalized }, new[] { 300f }, tail, 0.12f, 21f);
            return root;
        }

        static GameObject BuildSweep()
        {
            InitProjMats();
            var root = new GameObject("Boss Projectile b_sweep");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;

            // 火焰新月：直立的光幕（中心最亮、上下淡出）＋水平的新月薄片＋後方殘影
            var wave = Pivot("Wave", rt, Vector3.zero);
            GlowMesh(wave, "sweep.wave", m =>
            {
                const int N = 17;
                SweepCurtain(m, N, 0f, 1f, 1f, new Color(1f, 0.22f, 0.04f), 0.45f);
                SweepCurtain(m, N, 0f, 1f, 0.6f, new Color(1f, 0.55f, 0.12f), 0.7f);
                SweepCurtain(m, N, 0.01f, 1f, 0.22f, new Color(1f, 0.93f, 0.65f), 0.9f);
                SweepCurtain(m, N, -0.35f, 0.85f, 0.7f, new Color(1f, 0.3f, 0.06f), 0.28f);
                SweepCurtain(m, N, -0.7f, 0.65f, 0.5f, new Color(1f, 0.25f, 0.05f), 0.14f);
                // 水平新月薄片（從上方或斜上方看也一目了然是「橫的」）
                var pts = new Vector3[N]; var half = new Vector3[N]; var cols = new Color[N];
                for (int i = 0; i < N; i++)
                {
                    float u = -1f + 2f * i / (N - 1);
                    float k = 1f - u * u;
                    pts[i] = new Vector3(1.2f * u, 0f, 0.35f * k - 0.22f);
                    half[i] = new Vector3(0f, 0f, 0.16f * k + 0.01f);
                    cols[i] = new Color(1f, 0.42f, 0.08f, 0.55f * Mathf.Sqrt(k));
                }
                m.Ribbon(pts, half, cols);
            });

            // 衝擊波前緣翻滾的岩塊
            const int R = 7;
            var rocks = new Transform[R]; var axes = new Vector3[R]; var speeds = new float[R];
            for (int i = 0; i < R; i++)
            {
                float u = -0.85f + 1.75f * i / (R - 1);
                float k = 1f - u * u;
                var go = new GameObject("Rock");
                go.transform.SetParent(rt, false);
                go.transform.localPosition = new Vector3(1.2f * u, ((i * 37) % 7 - 3) * 0.05f, 0.35f * k - 0.05f);
                go.transform.localRotation = Quaternion.Euler(i * 53f, i * 97f, i * 31f);
                go.transform.localScale = Vector3.one * (0.16f + 0.1f * k + 0.02f * (i % 3));
                go.AddComponent<MeshFilter>().sharedMesh = RockMesh(i % 3);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = projRock;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                rocks[i] = go.transform;
                axes[i] = new Vector3(1f, (i % 3) * 0.4f - 0.4f, (i & 1) * 0.5f).normalized;
                speeds[i] = 260f + 60f * (i % 4);
            }

            // 沿著前緣冒出的火焰與塵土（世界座標，自然拖在後面）
            var fire = SpellFx.NewPS(rt, "Fire", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            fire.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            {
                var m = fire.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.15f, 0.8f), new Color(1f, 0.25f, 0.05f, 0.7f));
                var sh = fire.shape; sh.shapeType = ParticleSystemShapeType.SingleSidedEdge; sh.radius = 1.1f;
                SpellFx.FadeOut(fire);
                SpellFx.SizeCurve(fire, SpellFx.Shrink);
                SpellFx.Rise(fire, 0.6f);
                SpellFx.Rate(fire, 45f);
                fire.Play();
            }
            var dust = SpellFx.NewPS(rt, "Dust", SpellFx.BlendMat(SpellFx.TexSoft), 16, true);
            dust.transform.localPosition = new Vector3(0f, -0.1f, -0.1f);
            {
                var m = dust.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.28f, 0.24f, 0.5f), new Color(0.2f, 0.16f, 0.14f, 0.45f));
                var sh = dust.shape; sh.shapeType = ParticleSystemShapeType.SingleSidedEdge; sh.radius = 1.1f;
                SpellFx.FadeOut(dust);
                SpellFx.SizeCurve(dust, SpellFx.Grow);
                SpellFx.Rate(dust, 18f);
                dust.Play();
            }

            anim.Setup(rocks, axes, speeds, wave, 0.06f, 27f);
            return root;
        }

        /// <summary>新月形直立光幕：沿弧線（中央往前凸），高度在中央最高、兩端收尖。</summary>
        static void SweepCurtain(PMesh m, int n, float zOff, float widthK, float hK, Color c, float alpha)
        {
            var pts = new Vector3[n]; var half = new Vector3[n]; var cols = new Color[n];
            for (int i = 0; i < n; i++)
            {
                float u = -1f + 2f * i / (n - 1);
                float k = 1f - u * u;
                pts[i] = new Vector3(1.2f * widthK * u, 0f, 0.35f * k * widthK - 0.1f + zOff);
                half[i] = new Vector3(0f, (0.25f * Mathf.Pow(k, 0.4f) + 0.01f) * hK, 0f);
                cols[i] = SpellFx.WithA(c, alpha * Mathf.Sqrt(k));
            }
            m.Ribbon(pts, half, cols);
        }

        static GameObject BuildRain(string id, float k)
        {
            InitProjMats();
            var root = new GameObject("Boss Projectile " + id);
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;

            // 翻滾的巨石：岩塊的凹處透出裡面的熔岩球（像龜裂的燃燒隕石）
            var tumble = Pivot("Tumble", rt, Vector3.zero);
            var rock = new GameObject("Boulder");
            rock.transform.SetParent(tumble, false);
            rock.transform.localScale = Vector3.one * (0.6f * k);
            rock.AddComponent<MeshFilter>().sharedMesh = RockMesh(1);
            var rr = rock.AddComponent<MeshRenderer>();
            rr.sharedMaterial = projRock;
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rr.receiveShadows = false;
            ProjPart(PrimitiveType.Sphere, tumble, Vector3.zero, Vector3.one * (0.5f * k), projLava);

            // 火焰罩：沿 -Z（下落時朝上）拖長，對 Z 軸對稱
            var flame = Pivot("Flame", rt, Vector3.zero);
            flame.localScale = Vector3.one * k;
            GlowMesh(flame, "rain.flame", m =>
            {
                m.Lathe(new[] { -1.6f, 0f, -0.9f, 0.18f, -0.35f, 0.32f, 0.05f, 0.4f, 0.28f, 0.32f, 0.42f, 0f }, 9,
                    (z, j) => new Color(1f, 0.22f, 0.04f, Mathf.Lerp(0.04f, 0.4f, Mathf.InverseLerp(-1.6f, 0.2f, z))), 1f, 1f, 0f, JagF);
                m.Lathe(new[] { -1.1f, 0f, -0.6f, 0.14f, -0.2f, 0.26f, 0.1f, 0.34f, 0.3f, 0.25f, 0.38f, 0f }, 9,
                    (z, j) => new Color(1f, 0.55f, 0.12f, Mathf.Lerp(0.06f, 0.6f, Mathf.InverseLerp(-1.1f, 0.2f, z))), 1f, 1f, 20f, JagF);
            });
            Halo(rt, new Color(1f, 0.45f, 0.12f, 0.7f), 1.4f * k);

            var fire = SpellFx.NewPS(rt, "FireTrail", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            {
                var m = fire.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.3f * k, 0.5f * k);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0.15f, 0.8f), new Color(1f, 0.25f, 0.05f, 0.7f));
                SpellFx.Sphere(fire, 0.2f * k);
                SpellFx.FadeOut(fire);
                SpellFx.SizeCurve(fire, SpellFx.Shrink);
                SpellFx.Rate(fire, 30f);
                var em = fire.emission; em.rateOverDistance = 10f;
                fire.Play();
            }
            var smoke = SpellFx.NewPS(rt, "Smoke", SpellFx.BlendMat(SpellFx.TexSoft), 20, true);
            {
                var m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.3f * k, 0.45f * k);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.22f, 0.18f, 0.16f, 0.6f), new Color(0.1f, 0.09f, 0.09f, 0.5f));
                SpellFx.Sphere(smoke, 0.2f * k);
                SpellFx.FadeOut(smoke);
                SpellFx.SizeCurve(smoke, SpellFx.Grow);
                SpellFx.Rate(smoke, 12f);
                smoke.Play();
            }

            anim.Setup(new[] { tumble }, new[] { new Vector3(1f, 0.6f, 0.3f).normalized }, new[] { 200f }, flame, 0.1f, 19f);
            return root;
        }

        /// <summary>熔岩狙擊手的光彈：細長的洋紅光束（白熱內芯）＋前端光暈＋短拖尾。</summary>
        static GameObject BuildSnipe()
        {
            InitProjMats();
            var root = new GameObject("Boss Projectile m_snipe");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;
            var beam = Pivot("Beam", rt, Vector3.zero);
            GlowMesh(beam, "imp.snipe", m =>
            {
                m.Lathe(new[] { -0.36f, 0f, -0.2f, 0.028f, 0.08f, 0.04f, 0.13f, 0.03f, 0.16f, 0f }, 8,
                    (z, j) => new Color(1f, 0.15f, 0.7f, Mathf.Lerp(0.1f, 0.7f, Mathf.InverseLerp(-0.36f, 0.1f, z))));
                m.Lathe(new[] { -0.3f, 0f, -0.15f, 0.012f, 0.1f, 0.016f, 0.15f, 0f }, 6,
                    (z, j) => new Color(1f, 0.85f, 0.97f, Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(-0.3f, 0.1f, z))), 1f, 1f, 30f);
            });
            Halo(rt, new Color(1f, 0.3f, 0.8f, 0.8f), 0.3f);

            var trail = SpellFx.NewPS(rt, "Trail", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.8f, 0.8f), new Color(1f, 0.75f, 0.95f, 0.8f));
                SpellFx.Sphere(trail, 0.015f);
                SpellFx.FadeOut(trail);
                SpellFx.SizeCurve(trail, SpellFx.Shrink);
                SpellFx.Rate(trail, 20f);
                var em = trail.emission; em.rateOverDistance = 40f;
                trail.Play();
            }
            anim.Setup(new Transform[0], new Vector3[0], new float[0], beam, 0.12f, 37f);
            return root;
        }

        static Material projCyan, projWhite;

        /// <summary>火焰射手的青白小火球：發光球＋白熱核心＋往 -Z 的短火舌＋光暈＋短拖尾粒子。</summary>
        static GameObject BuildImpFireball()
        {
            InitProjMats();
            if (projCyan == null) projCyan = new Material(glowShader) { color = new Color(0.45f, 0.88f, 1f), name = "BossProjCyan" };
            if (projWhite == null) projWhite = new Material(glowShader) { color = new Color(0.9f, 1f, 1f), name = "BossProjWhite" };
            var root = new GameObject("Boss Projectile m_fireball");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;

            ProjPart(PrimitiveType.Sphere, rt, Vector3.zero, Vector3.one * 0.15f, projCyan);
            ProjPart(PrimitiveType.Sphere, rt, new Vector3(0f, 0f, 0.035f), Vector3.one * 0.085f, projWhite);
            var tail = Pivot("Tail", rt, Vector3.zero);
            GlowMesh(tail, "imp.fireball.tail", m =>
            {
                m.Lathe(new[] { -0.42f, 0f, -0.24f, 0.05f, -0.08f, 0.085f, 0.04f, 0.1f, 0.1f, 0.07f, 0.13f, 0f }, 9,
                    (z, j) => new Color(0.2f, 0.55f, 1f, Mathf.Lerp(0.05f, 0.5f, Mathf.InverseLerp(-0.42f, 0.05f, z))), 1f, 1f, 0f, JagF);
                m.Lathe(new[] { -0.28f, 0f, -0.14f, 0.04f, -0.03f, 0.065f, 0.06f, 0.07f, 0.11f, 0f }, 9,
                    (z, j) => new Color(0.75f, 0.97f, 1f, Mathf.Lerp(0.1f, 0.8f, Mathf.InverseLerp(-0.28f, 0.05f, z))), 1f, 1f, 20f, JagF);
            });
            Halo(rt, new Color(0.4f, 0.85f, 1f, 0.75f), 0.45f);

            var trail = SpellFx.NewPS(rt, "Trail", SpellFx.AddMat(SpellFx.TexSoft), 20, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.28f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.9f, 1f, 0.8f), new Color(0.15f, 0.45f, 1f, 0.7f));
                SpellFx.Sphere(trail, 0.04f);
                SpellFx.FadeOut(trail);
                SpellFx.SizeCurve(trail, SpellFx.Shrink);
                SpellFx.Rate(trail, 20f);
                var em = trail.emission; em.rateOverDistance = 25f;
                trail.Play();
            }

            anim.Setup(new Transform[0], new Vector3[0], new float[0], tail, 0.15f, 25f);
            return root;
        }

        static void ProjPart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>發光網格（疊加材質＋頂點色），網格依 key 快取共用。</summary>
        static void GlowMesh(Transform parent, string key, System.Action<PMesh> build)
        {
            if (!meshCache.TryGetValue(key, out var mesh) || mesh == null)
            {
                var pm = new PMesh(1);
                build(pm);
                mesh = pm.Build("Boss " + key, false);
                meshCache[key] = mesh;
            }
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = fxAdd;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>柔光光暈：一顆永遠存在的本地粒子（面向鏡頭）。</summary>
        static void Halo(Transform parent, Color c, float size)
        {
            var ps = SpellFx.NewPS(parent, "Halo", SpellFx.AddMat(SpellFx.TexSoft), 1, false);
            var m = ps.main;
            m.startLifetime = 1e5f;
            m.startSize = size;
            m.startColor = c;
            ps.Play();
            ps.Emit(1);
        }

        // ================================================================ 落地衝擊波
        /// <summary>一次性的地面衝擊波：擴散到 radius 的熔岩光環＋塵土環＋往上噴的熔岩與碎石，播完自己清除。</summary>
        public static void SlamShockwave(Vector3 worldFeetPos, float radius, Color color)
        {
            radius = Mathf.Max(0.2f, radius);
            color.a = 1f;
            var hot = Color.Lerp(color, new Color(1f, 0.95f, 0.7f), 0.55f);
            var go = new GameObject("Boss Slam Shockwave");
            // 本地 XY 平面 = 地面（本地 +Z 朝下）
            go.transform.SetPositionAndRotation(worldFeetPos + Vector3.up * 0.03f, Quaternion.Euler(90f, 0f, 0f));
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 1.8f;
            var root = go.transform;

            anim.AddRing(root, SpellFx.Circle(64, 1f), true, 0.22f, color, 0f, 0.65f, 0.25f, radius);
            anim.AddRing(root, SpellFx.Circle(64, 1f), true, 0.08f, hot, 0f, 0.5f, 0.2f, radius * 0.92f);
            anim.AddRing(root, SpellFx.Circle(48, 1f), true, 0.35f, new Color(0.25f, 0.2f, 0.17f, 0.55f), 0.05f, 0.9f, 0.2f, radius * 0.8f, false);

            // 地面閃光
            var flash = SpellFx.NewPS(root, "Flash", SpellFx.AddMat(SpellFx.TexSoft), 2, false);
            {
                var m = flash.main;
                m.startLifetime = 0.35f;
                m.startSize = radius * 0.9f;
                m.startColor = SpellFx.WithA(hot, 0.8f);
                SpellFx.FadeOut(flash);
                flash.Play(); flash.Emit(1);
            }
            // 往外推開的塵土環（圓盤在本地 XY = 地面，徑向噴出）
            var dust = SpellFx.NewPS(root, "Dust", SpellFx.BlendMat(SpellFx.TexSoft), 24, true);
            {
                var m = dust.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.2f, radius * 2.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.12f, radius * 0.22f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.42f, 0.35f, 0.3f, 0.6f), new Color(0.25f, 0.2f, 0.18f, 0.5f));
                SpellFx.Disc(dust, radius * 0.15f);
                SpellFx.FadeOut(dust);
                SpellFx.SizeCurve(dust, SpellFx.Grow);
                SpellFx.Drag(dust, 3f);
                dust.Play(); dust.Emit(22);
            }
            // 往上噴的熔岩火花與碎石（子物件翻轉成 +Z 朝上）
            var upT = Pivot("Up", root, Vector3.zero);
            upT.localRotation = Quaternion.Euler(180f, 0f, 0f);
            var lava = SpellFx.NewPS(upT, "Lava", SpellFx.AddMat(SpellFx.TexSoft), 28, true);
            {
                var m = lava.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                m.startColor = new ParticleSystem.MinMaxGradient(color, hot);
                m.gravityModifier = 1f;
                SpellFx.Cone(lava, 40f, radius * 0.25f);
                SpellFx.FadeOut(lava);
                SpellFx.Stretch(lava, 0.05f, 1.5f);
                lava.Play(); lava.Emit(24);
            }
            var debris = SpellFx.NewPS(upT, "Debris", SpellFx.BlendMat(SpellFx.TexSoft), 16, true);
            {
                var m = debris.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.13f, 0.11f, 1f), new Color(0.28f, 0.22f, 0.19f, 1f));
                m.gravityModifier = 1.4f;
                SpellFx.Cone(debris, 50f, radius * 0.25f);
                SpellFx.FadeOut(debris);
                SpellFx.Spin(debris, 6f);
                debris.Play(); debris.Emit(14);
            }
        }
    }

    /// <summary>頭目飛行物的小動畫：多個零件自轉＋一個零件閃爍縮放（每幀不配置記憶體）。</summary>
    internal class BossFxSpin : MonoBehaviour
    {
        Transform[] spins; Vector3[] axes; float[] speeds;
        Transform flick; Vector3 flickBase = Vector3.one; float flickAmt, flickRate = 20f;
        float clock;

        public void Setup(Transform[] spins, Vector3[] axes, float[] speeds, Transform flick, float flickAmt, float flickRate)
        {
            this.spins = spins; this.axes = axes; this.speeds = speeds;
            this.flick = flick; this.flickAmt = flickAmt; this.flickRate = flickRate;
            if (flick != null) flickBase = flick.localScale;
            clock = Random.Range(0f, 10f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;
            if (clock > 1000f) clock -= 1000f;
            if (spins != null)
                for (int i = 0; i < spins.Length; i++)
                    if (spins[i] != null) spins[i].Rotate(axes[i], speeds[i] * dt, Space.Self);
            if (flick != null && flickAmt > 0f)
            {
                float k = 1f + flickAmt * (0.6f * Mathf.Sin(clock * flickRate) + 0.4f * Mathf.Sin(clock * flickRate * 2.37f + 1.7f));
                flick.localScale = flickBase * k;
            }
        }
    }
}
