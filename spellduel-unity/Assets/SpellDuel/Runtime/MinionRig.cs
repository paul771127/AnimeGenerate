using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 熔岩巨像召喚的小怪，與巨像同樣的視覺語言（深色玄武岩塊＋熔岩光、發光眼睛、發光爪子）。
    ///   座標：腳底在本地 y = 0，面向本地 +Z，真實公尺（父物件縮放請保持 1）。
    ///   種類（SetVariant，Create() 之後立刻呼叫；預設 0）：
    ///     0 熔岩小鬼（近戰）：約 0.9 m，駝背、橘紅熔岩、頭頂小火焰，舉爪→揮爪。
    ///     1 火焰射手（遠程）：約 0.8 m，較瘦，青白色高溫火焰，舉起的手上方漂著一顆火球；預備＝往後仰火球變大，出手＝投擲。
    ///     2 岩石衝鋒獸（坦克）：約 1.35 m，身上覆蓋厚岩甲，暗深紅熔岩，沉重緩慢的步伐；預備＝雙拳高舉，出手＝砸下。
    ///     3 自爆小鬼：約 0.7 m，圓滾滾的膨脹身體，肚子上黃白色核心脈動，預備時越脈動越快、身體鼓脹（之後由呼叫者引爆）。
    ///     4 岩石投擲者（遠程）：約 1.0 m，瘦長的石頭小鬼，背上扛著發光熔岩巨石；預備＝雙手把巨石舉過頭（變亮），出手＝過頭投擲（巨石約 0.8 秒後在背上重新長出）。
    ///     5 熔岩狙擊手（遠程）：約 0.9 m，瘦削駝背，頭上一根發光的長角（砲管），洋紅色熔岩；預備＝蹲低瞄準、角尖的蓄能光越來越大，出手＝後座力。
    ///   效能：每隻 ≤ 18 個 Renderer（含 1 個火星粒子）；材質、網格全部 static 共用，
    ///   各自的顏色／閃色／蓄力發亮／死亡變暗都用 MaterialPropertyBlock（不複製材質、每幀不配置記憶體）。
    /// </summary>
    public class MinionRig : MonoBehaviour
    {
        // ================================================================ 對外介面
        public const float Height = 0.9f;

        /// <summary>移動速度（公尺/秒），驅動步伐；0 = 待機（抽動、東張西望）。</summary>
        public float MoveSpeed { get; set; }

        public static MinionRig Create()
        {
            InitMats();
            var go = new GameObject("Minion LavaImp");
            var rig = go.AddComponent<MinionRig>();
            rig.Build(0);
            return rig;
        }

        /// <summary>切換種類（0 近戰、1 火焰射手、2 坦克、3 自爆、4 岩石投擲者、5 熔岩狙擊手、6 虛空術士），會重建外觀；請在 Create() 之後立刻呼叫。</summary>
        public void SetVariant(int kind)
        {
            kind = Mathf.Clamp(kind, 0, 6);
            if (kind == variantId && body != null) return;
            if (body != null)
            {
                body.gameObject.SetActive(false);
                Destroy(body.gameObject);
                body = null;
            }
            Build(kind);
        }

        /// <summary>攻擊預備姿勢（熔岩越來越亮），duration 秒擺到位後一直維持到 Attack。</summary>
        public void Windup(float duration)
        {
            if (dead || body == null) return;
            phase = PhaseWindup;
            atkT = 0f;
            atkDur = Mathf.Max(0.05f, duration);
        }

        /// <summary>出手（揮爪／投擲火球／砸拳／撲出），約 0.4 秒後回到待機。</summary>
        public void Attack()
        {
            if (dead || body == null) return;
            snap = cur;
            phase = PhaseStrike;
            atkT = 0f;
            if (kind == 1 || kind == 4) ballGone = 0f;   // 火球／巨石丟出去了，稍後重新長出
        }

        public void PlayHit()
        {
            if (dead || body == null) return;
            hitT = HitDur;
        }

        /// <summary>死亡：約 0.8 秒內往前癱倒、縮小陷入地面、熔岩熄滅變暗（呼叫者之後會 Destroy）。</summary>
        public void SetDead(bool d)
        {
            if (d == dead) return;
            dead = d;
            deadT = 0f;
            if (d) { phase = PhaseNone; hitT = 0f; }
            if (embers != null) SpellFx.Rate(embers, d ? 0f : 3f);
        }

        // ================================================================ 各種類的參數
        sealed class Variant
        {
            public float size, stride, legSwing, bob, step;   // 整體縮放、一個步伐循環前進距離（未縮放）、腿擺角、上下起伏、步頻倍率
            public Color rockA, rockB, lava, lavaDim, hot, eye, emberA, emberB;
        }

        static readonly Variant[] Variants =
        {
            new Variant   // 0 熔岩小鬼
            {
                size = 1f, stride = 0.5f, legSwing = 38f, bob = 0.035f, step = 1f,
                rockA = new Color(0.3f, 0.26f, 0.24f), rockB = new Color(0.2f, 0.18f, 0.17f),
                lava = new Color(1f, 0.38f, 0.06f), lavaDim = new Color(0.6f, 0.1f, 0.02f), hot = new Color(1f, 0.9f, 0.55f), eye = new Color(1f, 0.8f, 0.3f),
                emberA = new Color(1f, 0.45f, 0.08f), emberB = new Color(1f, 0.8f, 0.35f),
            },
            new Variant   // 1 火焰射手（青白火焰）
            {
                size = 0.9f, stride = 0.5f, legSwing = 34f, bob = 0.03f, step = 1f,
                rockA = new Color(0.27f, 0.26f, 0.29f), rockB = new Color(0.17f, 0.17f, 0.2f),
                lava = new Color(0.35f, 0.85f, 1f), lavaDim = new Color(0.08f, 0.3f, 0.6f), hot = new Color(0.88f, 1f, 1f), eye = new Color(0.75f, 0.97f, 1f),
                emberA = new Color(0.3f, 0.8f, 1f), emberB = new Color(0.85f, 1f, 1f),
            },
            new Variant   // 2 岩石衝鋒獸（暗紅熔岩、沉重）
            {
                size = 1.5f, stride = 0.6f, legSwing = 26f, bob = 0.05f, step = 0.8f,
                rockA = new Color(0.34f, 0.3f, 0.28f), rockB = new Color(0.22f, 0.19f, 0.18f),
                lava = new Color(0.7f, 0.12f, 0.04f), lavaDim = new Color(0.32f, 0.04f, 0.02f), hot = new Color(1f, 0.42f, 0.14f), eye = new Color(1f, 0.3f, 0.1f),
                emberA = new Color(0.8f, 0.15f, 0.04f), emberB = new Color(1f, 0.4f, 0.12f),
            },
            new Variant   // 3 自爆小鬼（黃白核心）
            {
                size = 0.75f, stride = 0.45f, legSwing = 40f, bob = 0.045f, step = 1.15f,
                rockA = new Color(0.3f, 0.25f, 0.22f), rockB = new Color(0.2f, 0.17f, 0.15f),
                lava = new Color(1f, 0.62f, 0.15f), lavaDim = new Color(0.75f, 0.25f, 0.04f), hot = new Color(1f, 0.97f, 0.75f), eye = new Color(1f, 0.9f, 0.5f),
                emberA = new Color(1f, 0.7f, 0.2f), emberB = new Color(1f, 0.95f, 0.6f),
            },
            new Variant   // 4 岩石投擲者（黯淡的橘色熔岩）
            {
                size = 1.1f, stride = 0.55f, legSwing = 32f, bob = 0.035f, step = 0.9f,
                rockA = new Color(0.33f, 0.29f, 0.26f), rockB = new Color(0.22f, 0.2f, 0.18f),
                lava = new Color(0.85f, 0.4f, 0.1f), lavaDim = new Color(0.4f, 0.12f, 0.03f), hot = new Color(1f, 0.72f, 0.35f), eye = new Color(1f, 0.6f, 0.2f),
                emberA = new Color(0.9f, 0.4f, 0.1f), emberB = new Color(1f, 0.65f, 0.3f),
            },
            new Variant   // 5 熔岩狙擊手（洋紅熔岩）
            {
                size = 1f, stride = 0.5f, legSwing = 34f, bob = 0.03f, step = 1f,
                rockA = new Color(0.26f, 0.23f, 0.25f), rockB = new Color(0.17f, 0.15f, 0.17f),
                lava = new Color(1f, 0.2f, 0.72f), lavaDim = new Color(0.45f, 0.04f, 0.3f), hot = new Color(1f, 0.82f, 0.96f), eye = new Color(1f, 0.6f, 0.95f),
                emberA = new Color(1f, 0.25f, 0.75f), emberB = new Color(1f, 0.7f, 0.95f),
            },
            new Variant   // 6 虛空術士（紫黑色虛空能量，手上漂著虛空球）
            {
                size = 0.95f, stride = 0.5f, legSwing = 30f, bob = 0.04f, step = 0.9f,
                rockA = new Color(0.16f, 0.13f, 0.22f), rockB = new Color(0.09f, 0.07f, 0.13f),
                lava = new Color(0.6f, 0.3f, 1f), lavaDim = new Color(0.2f, 0.06f, 0.4f), hot = new Color(0.92f, 0.8f, 1f), eye = new Color(0.8f, 0.6f, 1f),
                emberA = new Color(0.55f, 0.25f, 1f), emberB = new Color(0.9f, 0.75f, 1f),
            },
        };

        static readonly Color FlashC = new Color(1f, 0.55f, 0.4f);
        static readonly Color Dark = new Color(0.12f, 0.06f, 0.04f);
        static readonly Color RockDead = new Color(0.08f, 0.07f, 0.07f);
        static readonly Color Armor = new Color(0.45f, 0.4f, 0.37f);

        // ================================================================ 共用材質與網格（所有小鬼共用）
        static Shader litShader, glowShader;
        static Material rockMat, glowMat, fxAdd;
        static Mesh rockMesh0, rockMesh1, flameOrange, flameCyan;
        static MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TintId = Shader.PropertyToID("_TintColor");

        static void InitMats()
        {
            if (rockMat != null) return;
            glowShader = Shader.Find("Sprites/Default");
            litShader = Shader.Find("Legacy Shaders/Diffuse");
            if (litShader == null) litShader = Shader.Find("Mobile/Diffuse");
            if (litShader == null) litShader = Shader.Find("Diffuse");
            if (litShader == null) litShader = glowShader;
            rockMat = new Material(litShader) { color = Color.white, name = "ImpRock" };    // 顏色由 PropertyBlock 給
            glowMat = new Material(glowShader) { color = Color.white, name = "ImpGlow" };
            fxAdd = new Material(SpellFx.AddMat(SpellFx.TexSoft).shader) { name = "ImpFxAdd", mainTexture = Texture2D.whiteTexture };
            mpb = new MaterialPropertyBlock();
            rockMesh0 = RockMesh(0);
            rockMesh1 = RockMesh(1);
            flameOrange = FlameMesh("ImpFlameOrange", new Color(1f, 0.75f, 0.1f), new Color(1f, 0.25f, 0.1f), new Color(1f, 0.95f, 0.65f));
            flameCyan = FlameMesh("ImpFlameCyan", new Color(0.6f, 0.95f, 1f), new Color(0.15f, 0.45f, 1f), new Color(0.92f, 1f, 1f));
        }

        static Mesh RockMesh(int variant)
        {
            var m = new PMesh(1);
            const int rings = 5;
            var prof = new float[(rings + 1) * 2];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings;
                prof[i * 2] = -Mathf.Cos(a) * 0.5f;
                prof[i * 2 + 1] = Mathf.Sin(a) * 0.5f;
            }
            float seed = variant * 2.1f + 0.9f;
            m.Lathe(prof, 6, null, 1f, 1f, variant * 23f, null, Vector3.zero, Quaternion.identity, p =>
            {
                float n = Mathf.Sin(p.x * 9.1f + seed * 1.7f) * Mathf.Sin(p.y * 7.3f + seed * 2.9f) * Mathf.Sin(p.z * 8.7f + seed * 0.6f);
                p *= 1f + 0.22f * n;
                p.x = Mathf.Clamp(p.x, -0.46f, 0.46f);
                p.y = Mathf.Clamp(p.y, -0.46f, 0.46f);
                p.z = Mathf.Clamp(p.z, -0.46f, 0.46f);
                return p / 0.92f;
            });
            return m.Build("ImpRock" + variant, true);
        }

        /// <summary>頭頂火焰：沿 +Z 的參差火舌（擺放時轉成朝上），底部 c0、尖端 c1、內芯 core。</summary>
        static Mesh FlameMesh(string name, Color c0, Color c1, Color core)
        {
            var pm = new PMesh(1);
            float[] jag = { 1f, 0.7f, 1.15f, 0.8f, 1.05f, 0.75f, 1.2f };
            pm.Lathe(new[] { -0.02f, 0f, 0.02f, 0.06f, 0.08f, 0.07f, 0.16f, 0.045f, 0.26f, 0f }, 7,
                (z, j) => { var c = Color.Lerp(c0, c1, z / 0.26f); c.a = Mathf.Lerp(0.85f, 0.15f, z / 0.26f); return c; }, 1f, 1f, 0f, jag);
            pm.Lathe(new[] { 0f, 0f, 0.03f, 0.035f, 0.1f, 0.03f, 0.16f, 0f }, 6, (z, j) => SpellFx.WithA(core, 0.8f), 1f, 1f, 25f, jag);
            return pm.Build(name, false);
        }

        // ================================================================ 骨架與狀態
        int kind;            // 外形（虛空術士借用火焰射手的外形）
        int variantId = -1;  // 種類（顏色）
        Variant v;
        Transform body, spine, head, hipL, hipR, shL, shR, wrL, wrR, flame, ball, core, boulder, tip;
        Vector3 flameBase, coreBase;
        float carryK;   // 岩石投擲者：巨石從背上（0）到頭上（1）
        readonly List<Renderer> rockList = new List<Renderer>();
        readonly List<Color> rockCols = new List<Color>();
        readonly List<Renderer> lavaList = new List<Renderer>();
        readonly List<Renderer> hotList = new List<Renderer>();
        readonly List<Renderer> eyeList = new List<Renderer>();
        Renderer[] rockR, lavaR, hotR, eyeR; Color[] rockC;
        Renderer flameR;
        ParticleSystem embers;

        struct Pose
        {
            public float spX, spY, head, headY, crouch, glow;
            public Vector3 shL, shR;   // (前後 x、水平擺 y、往外張 z)
            public float wrL, wrR;

            public static Pose Lerp(in Pose a, in Pose b, float t)
            {
                Pose p;
                p.spX = a.spX + (b.spX - a.spX) * t;
                p.spY = a.spY + (b.spY - a.spY) * t;
                p.head = a.head + (b.head - a.head) * t;
                p.headY = a.headY + (b.headY - a.headY) * t;
                p.crouch = a.crouch + (b.crouch - a.crouch) * t;
                p.glow = a.glow + (b.glow - a.glow) * t;
                p.shL = Vector3.LerpUnclamped(a.shL, b.shL, t);
                p.shR = Vector3.LerpUnclamped(a.shR, b.shR, t);
                p.wrL = a.wrL + (b.wrL - a.wrL) * t;
                p.wrR = a.wrR + (b.wrR - a.wrR) * t;
                return p;
            }
        }

        Pose Idle()
        {
            var p = new Pose { spX = 28f, head = -22f, crouch = 0.03f, wrL = -30f, wrR = -30f };
            p.shL = p.shR = new Vector3(-35f, 0f, 18f);
            switch (kind)
            {
                case 1:   // 右手往前上方舉著火球
                    p.spX = 18f; p.head = -15f;
                    p.shR = new Vector3(-95f, 0f, 14f); p.wrR = -25f;
                    break;
                case 2:   // 壯碩：兩臂往外撐開
                    p.spX = 20f; p.head = -14f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-20f, 0f, 24f); p.wrL = p.wrR = -15f;
                    break;
                case 3:   // 圓滾滾：手短短地張開
                    p.spX = 10f; p.head = -8f;
                    p.shL = p.shR = new Vector3(-30f, 0f, 40f); p.wrL = p.wrR = -20f;
                    break;
                case 4:   // 被背上的巨石壓得很駝，長手垂著
                    p.spX = 34f; p.head = -28f;
                    p.shL = p.shR = new Vector3(-18f, 0f, 14f); p.wrL = p.wrR = -15f;
                    break;
                case 5:   // 駝背、頭往前伸，長角指向前方
                    p.spX = 36f; p.head = -30f;
                    p.shL = p.shR = new Vector3(-40f, 0f, 22f); p.wrL = p.wrR = -35f;
                    break;
            }
            return p;
        }

        Pose WindupPose()
        {
            var p = Idle();
            p.glow = 1f;
            switch (kind)
            {
                case 1:   // 往後仰、火球拉到身後蓄力
                    p.spX = -6f; p.spY = 30f; p.head = -10f; p.crouch = 0.05f;
                    p.shR = new Vector3(40f, 30f, 30f); p.wrR = -60f;
                    p.shL = new Vector3(-70f, 0f, 20f); p.wrL = -20f;
                    break;
                case 2:   // 雙拳高舉過頭
                    p.spX = -8f; p.head = -15f; p.crouch = 0.06f;
                    p.shL = p.shR = new Vector3(-150f, 0f, 22f); p.wrL = p.wrR = -40f;
                    break;
                case 3:   // 雙手大張、身體後仰鼓脹
                    p.spX = -10f; p.head = -20f; p.crouch = 0.08f;
                    p.shL = p.shR = new Vector3(-60f, 0f, 75f); p.wrL = p.wrR = 0f;
                    break;
                case 4:   // 雙手把巨石舉過頭、身體後仰
                    p.spX = -10f; p.head = -18f; p.crouch = 0.06f;
                    p.shL = p.shR = new Vector3(-172f, 0f, 14f); p.wrL = p.wrR = -35f;
                    break;
                case 5:   // 蹲低、雙手撐著，頭壓平瞄準
                    p.spX = 48f; p.head = -46f; p.crouch = 0.1f;
                    p.shL = p.shR = new Vector3(-62f, 0f, 30f); p.wrL = p.wrR = -10f;
                    break;
                default:  // 舉起右爪
                    p.spX = 8f; p.spY = 25f; p.head = -12f; p.crouch = 0.07f;
                    p.shR = new Vector3(-155f, 20f, 25f); p.wrR = -50f;
                    p.shL = new Vector3(-55f, 0f, 30f); p.wrL = -40f;
                    break;
            }
            return p;
        }

        Pose StrikePose()
        {
            var p = Idle();
            p.glow = 0.7f;
            switch (kind)
            {
                case 1:   // 投擲：手臂甩到前上方
                    p.spX = 35f; p.spY = -20f; p.head = -25f; p.crouch = 0.06f;
                    p.shR = new Vector3(-120f, -20f, 10f); p.wrR = 20f;
                    p.shL = new Vector3(-10f, 0f, 30f);
                    break;
                case 2:   // 雙拳往前下方砸
                    p.spX = 42f; p.head = -30f; p.crouch = 0.1f;
                    p.shL = p.shR = new Vector3(-55f, 0f, 8f); p.wrL = p.wrR = 10f;
                    break;
                case 3:   // 往前撲
                    p.spX = 45f; p.head = -35f; p.crouch = 0.02f;
                    p.shL = p.shR = new Vector3(-85f, 0f, 30f); p.wrL = p.wrR = 0f;
                    break;
                case 4:   // 過頭投擲：雙臂甩到前方
                    p.spX = 42f; p.head = -30f; p.crouch = 0.08f;
                    p.shL = p.shR = new Vector3(-70f, 0f, 12f); p.wrL = p.wrR = 15f;
                    break;
                case 5:   // 後座力：頭往上彈、身體往後坐
                    p.spX = 12f; p.head = -62f; p.crouch = 0.05f;
                    p.shL = p.shR = new Vector3(-20f, 0f, 35f); p.wrL = p.wrR = -20f;
                    break;
                default:  // 往前橫揮
                    p.spX = 40f; p.spY = -25f; p.head = -30f; p.crouch = 0.08f;
                    p.shR = new Vector3(-50f, -40f, 10f); p.wrR = 10f;
                    p.shL = new Vector3(-10f, 0f, 30f); p.wrL = -30f;
                    break;
            }
            return p;
        }

        const int PhaseNone = 0, PhaseWindup = 1, PhaseStrike = 2;
        int phase;
        float atkT, atkDur = 0.5f, clock, pulsePh, walkPhase, walkAmt, hitT, deadT, twitchT, lookY, lookTarget, ballGone = 10f;
        Pose cur, snap;
        bool dead;
        const float HitDur = 0.15f, StrikeIn = 0.08f, StrikeHold = 0.1f, StrikeOut = 0.22f, DieDur = 0.8f;

        // ================================================================ 建立
        void Build(int k)
        {
            variantId = k;
            kind = k == 6 ? 1 : k;
            v = Variants[k];
            rockList.Clear(); rockCols.Clear(); lavaList.Clear(); hotList.Clear(); eyeList.Clear();
            flame = null; flameR = null; ball = null; core = null; boulder = null; tip = null; carryK = 0f; ballGone = 10f;
            clock = Random.Range(0f, 10f);   // 每隻的呼吸、脈動錯開
            phase = PhaseNone;

            body = Pivot("Body", transform, Vector3.zero);
            body.localScale = Vector3.one * v.size;
            var ra = v.rockA; var rb = v.rockB;

            // 各種類的比例
            Vector3 legS, torsoS, torsoP, headS, armS, handS;
            switch (k)
            {
                case 1: legS = new Vector3(0.12f, 0.34f, 0.13f); torsoS = new Vector3(0.32f, 0.44f, 0.28f); torsoP = new Vector3(0f, 0.21f, 0f);
                        headS = new Vector3(0.26f, 0.24f, 0.27f); armS = new Vector3(0.09f, 0.3f, 0.09f); handS = new Vector3(0.12f, 0.1f, 0.12f); break;
                case 2: legS = new Vector3(0.2f, 0.34f, 0.22f); torsoS = new Vector3(0.52f, 0.46f, 0.44f); torsoP = new Vector3(0f, 0.2f, 0f);
                        headS = new Vector3(0.3f, 0.22f, 0.28f); armS = new Vector3(0.15f, 0.3f, 0.15f); handS = new Vector3(0.2f, 0.16f, 0.2f); break;
                case 3: legS = new Vector3(0.14f, 0.32f, 0.15f); torsoS = new Vector3(0.56f, 0.52f, 0.52f); torsoP = new Vector3(0f, 0.22f, 0f);
                        headS = new Vector3(0.26f, 0.2f, 0.24f); armS = new Vector3(0.09f, 0.22f, 0.09f); handS = new Vector3(0.11f, 0.1f, 0.11f); break;
                case 4: legS = new Vector3(0.11f, 0.36f, 0.12f); torsoS = new Vector3(0.34f, 0.44f, 0.3f); torsoP = new Vector3(0f, 0.21f, 0f);
                        headS = new Vector3(0.26f, 0.22f, 0.26f); armS = new Vector3(0.09f, 0.38f, 0.09f); handS = new Vector3(0.14f, 0.12f, 0.14f); break;
                case 5: legS = new Vector3(0.11f, 0.34f, 0.12f); torsoS = new Vector3(0.32f, 0.4f, 0.3f); torsoP = new Vector3(0f, 0.2f, 0f);
                        headS = new Vector3(0.26f, 0.2f, 0.3f); armS = new Vector3(0.08f, 0.3f, 0.08f); handS = new Vector3(0.11f, 0.1f, 0.12f); break;
                default: legS = new Vector3(0.15f, 0.34f, 0.17f); torsoS = new Vector3(0.42f, 0.42f, 0.36f); torsoP = new Vector3(0f, 0.2f, 0f);
                        headS = new Vector3(0.3f, 0.24f, 0.28f); armS = new Vector3(0.11f, 0.3f, 0.11f); handS = new Vector3(0.14f, 0.12f, 0.15f); break;
            }

            // ---- 短腿（髖在 0.32 m）
            float hipX = k == 2 ? 0.15f : k == 3 ? 0.14f : 0.12f;
            hipL = Pivot("HipL", body, new Vector3(-hipX, 0.32f, 0f));
            hipR = Pivot("HipR", body, new Vector3(hipX, 0.32f, 0f));
            Rock(hipL, new Vector3(0f, -0.16f, 0.02f), legS, Vector3.zero, rockMesh1, rb);
            Rock(hipR, new Vector3(0f, -0.16f, 0.02f), legS, Vector3.zero, rockMesh0, rb);

            // ---- 軀幹＋肚子裡透出的熔岩（自爆小鬼：大顆的黃白核心）
            spine = Pivot("Spine", body, new Vector3(0f, 0.32f, 0f));
            Rock(spine, torsoP, torsoS, new Vector3(0f, 0f, 8f), rockMesh0, ra);
            if (k == 3)
            {
                core = Pivot("Core", spine, new Vector3(0f, 0.2f, 0.19f));
                coreBase = Vector3.one * 0.3f;
                core.localScale = coreBase;
                Glow(PrimitiveType.Sphere, core, Vector3.zero, Vector3.one, hotList);
                Glow(PrimitiveType.Sphere, spine, new Vector3(0f, 0.2f, 0.14f), new Vector3(0.44f, 0.42f, 0.36f), lavaList);   // 核心外圈的熔岩
            }
            else
                Glow(PrimitiveType.Sphere, spine, new Vector3(0f, 0.16f, torsoS.z * 0.33f), new Vector3(torsoS.x * 0.57f, 0.26f, 0.18f), lavaList);

            // ---- 頭（往前突出）＋斜吊發光眼
            float headY = torsoP.y + torsoS.y * 0.45f;
            head = Pivot("Head", spine, new Vector3(0f, headY, 0.12f));
            Rock(head, new Vector3(0f, 0.06f, 0.06f), headS, Vector3.zero, rockMesh1, ra);
            float eyeZ = 0.06f + headS.z * 0.5f + 0.005f;
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Cube, head, new Vector3(0.065f * s, 0.08f, eyeZ), new Vector3(0.07f, 0.03f, 0.03f), eyeList, new Vector3(0f, 0f, 16f * s));

            // ---- 頭頂火焰（坦克沒有；自爆小鬼的像引信）
            if (k != 2 && k < 4)
            {
                var fl = new GameObject("Flame");
                fl.transform.SetParent(head, false);
                fl.transform.localPosition = new Vector3(0f, 0.06f + headS.y * 0.4f, 0.02f);
                fl.transform.localRotation = Quaternion.Euler(k == 3 ? -60f : -80f, 0f, 0f);
                if (k == 3) fl.transform.localScale = Vector3.one * 0.7f;
                fl.AddComponent<MeshFilter>().sharedMesh = k == 1 ? flameCyan : flameOrange;
                flameR = fl.AddComponent<MeshRenderer>();
                flameR.sharedMaterial = fxAdd;
                NoShadow(flameR);
                flame = fl.transform; flameBase = flame.localScale;
            }

            // ---- 手臂＋岩石手＋發光爪
            float shY = torsoP.y + torsoS.y * 0.3f, shX = torsoS.x * 0.5f;
            shL = Arm(-1f, shX, shY, armS, handS, out wrL);
            shR = Arm(1f, shX, shY, armS, handS, out wrR);

            // ---- 種類專屬零件
            if (k == 1)
            {
                // 漂在手上方的青白火球（外層＋白熱核心）
                ball = Pivot("Fireball", body, Vector3.zero);
                Glow(PrimitiveType.Sphere, ball, Vector3.zero, Vector3.one * 0.13f, lavaList);
                Glow(PrimitiveType.Sphere, ball, new Vector3(0f, 0f, 0.02f), Vector3.one * 0.075f, hotList);
            }
            else if (k == 2)
            {
                // 厚岩甲：兩塊護肩、背甲、頭盔
                Rock(shL, new Vector3(-0.03f, 0.02f, 0f), new Vector3(0.26f, 0.14f, 0.28f), new Vector3(0f, 0f, -22f), rockMesh0, Armor);
                Rock(shR, new Vector3(0.03f, 0.02f, 0f), new Vector3(0.26f, 0.14f, 0.28f), new Vector3(0f, 0f, 22f), rockMesh1, Armor);
                Rock(spine, new Vector3(0f, 0.3f, -0.16f), new Vector3(0.56f, 0.34f, 0.2f), new Vector3(30f, 0f, 0f), rockMesh1, Armor);
                Rock(head, new Vector3(0f, 0.14f, 0.07f), new Vector3(0.36f, 0.12f, 0.34f), new Vector3(-12f, 0f, 0f), rockMesh0, Armor);
            }

            else if (k == 4)
            {
                // 背上的熔岩巨石：岩殼的凹處透出裡面的熔岩
                boulder = Pivot("Boulder", body, Vector3.zero);
                var br = new GameObject("Rock");
                br.transform.SetParent(boulder, false);
                br.transform.localScale = Vector3.one * 0.34f;
                br.transform.localRotation = Quaternion.Euler(20f, 40f, 10f);
                br.AddComponent<MeshFilter>().sharedMesh = rockMesh1;
                var rr = br.AddComponent<MeshRenderer>();
                rr.sharedMaterial = rockMat;
                NoShadow(rr);
                rockList.Add(rr); rockCols.Add(v.rockB);
                Glow(PrimitiveType.Sphere, boulder, Vector3.zero, Vector3.one * 0.3f, lavaList);
            }
            else if (k == 5)
            {
                // 頭上往前伸的長角（砲管）：岩石外殼＋發光內芯＋角尖的蓄能光
                var horn = Pivot("Horn", head, new Vector3(0f, 0.15f, 0.08f));
                horn.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                Rock(horn, new Vector3(0f, 0f, 0.14f), new Vector3(0.08f, 0.08f, 0.32f), Vector3.zero, rockMesh0, v.rockA);
                Glow(PrimitiveType.Cube, horn, new Vector3(0f, 0.025f, 0.17f), new Vector3(0.035f, 0.035f, 0.36f), lavaList);
                tip = Pivot("Tip", horn, new Vector3(0f, 0.01f, 0.36f));
                Glow(PrimitiveType.Sphere, tip, Vector3.zero, Vector3.one, hotList);
                tip.localScale = Vector3.one * 0.04f;
            }

            // ---- 少量火星
            embers = SpellFx.NewPS(spine, "Embers", SpellFx.AddMat(SpellFx.TexSoft), 8, true);
            embers.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.015f * v.size, 0.03f * v.size);
                m.startColor = new ParticleSystem.MinMaxGradient(v.emberA, v.emberB);
                SpellFx.Sphere(embers, 0.25f * v.size);
                SpellFx.FadeInOut(embers);
                SpellFx.Rise(embers, 0.3f);
                SpellFx.Rate(embers, dead ? 0f : 3f);
                embers.Play();
            }

            rockR = rockList.ToArray(); rockC = rockCols.ToArray();
            lavaR = lavaList.ToArray(); hotR = hotList.ToArray(); eyeR = eyeList.ToArray();
            cur = Idle();
            Animate(0f);
        }

        Transform Arm(float s, float shX, float shY, Vector3 armS, Vector3 handS, out Transform wrist)
        {
            var sh = Pivot(s < 0f ? "ShoulderL" : "ShoulderR", spine, new Vector3(shX * s, shY, 0.04f));
            float len = armS.y;
            Rock(sh, new Vector3(0f, -len * 0.47f, 0f), armS, Vector3.zero, rockMesh0, v.rockB);
            wrist = Pivot("Wrist", sh, new Vector3(0f, -len * 0.97f, 0f));
            Rock(wrist, new Vector3(0f, -handS.y * 0.33f, 0.01f), handS, Vector3.zero, rockMesh1, v.rockA);
            // 三根爪子用一個拉長的發光方塊表示（往前下方彎）
            Glow(PrimitiveType.Cube, wrist, new Vector3(0f, -handS.y * 0.85f, handS.z * 0.45f), new Vector3(handS.x * 0.85f, 0.03f, handS.z * 0.85f), lavaList, new Vector3(35f, 0f, 0f));
            return sh;
        }

        // ================================================================ 動作
        void Update() { Animate(Time.deltaTime); }

        void Animate(float dt)
        {
            if (body == null) return;
            clock += dt;
            if (clock > 3600f) clock -= 3600f;
            float hitK = 0f;
            if (hitT > 0f) { hitT -= dt; hitK = Mathf.Clamp01(hitT / HitDur); }
            if (dead) deadT += dt;
            ballGone += dt;

            // ---- 步伐：一個循環前進 stride×size 公尺
            float speed = dead ? 0f : Mathf.Clamp(MoveSpeed, 0f, 4f);
            bool acting = phase != PhaseNone;
            float strideLen = v.stride * v.size;
            walkAmt = Mathf.MoveTowards(walkAmt, acting ? 0f : Mathf.Clamp01(speed / 0.3f), dt * 6f);
            if (speed > 0.01f && !acting) walkPhase += dt * Mathf.PI * 2f * Mathf.Max(speed, 0.3f) / strideLen * v.step;
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 6f);
            if (walkPhase > 1000f) walkPhase -= Mathf.PI * 300f;
            float sinP = Mathf.Sin(walkPhase), cosP = Mathf.Cos(walkPhase);

            // 待機時頭會一下一下地轉向別處（小動物般的抽動；坦克比較慢）
            twitchT -= dt;
            if (twitchT <= 0f) { twitchT = Random.Range(0.4f, 1.4f) * (kind == 2 ? 2f : 1f); lookTarget = Random.Range(-35f, 35f); }
            lookY = Mathf.MoveTowards(lookY, lookTarget * (1f - walkAmt), dt * (kind == 2 ? 90f : 300f));

            float breath = Mathf.Sin(clock * (kind == 2 ? 2f : 3.2f));
            var basePose = Idle();
            basePose.spX += breath * 2f + walkAmt * 10f;
            basePose.headY = lookY;
            basePose.head -= walkAmt * 8f;
            float armSwing = sinP * (kind == 2 ? 20f : 30f) * walkAmt;
            basePose.shL.x += armSwing;
            if (kind != 1) basePose.shR.x -= armSwing;   // 射手的右手一直舉著火球
            basePose.shL.z += breath * 3f; basePose.shR.z += breath * 3f;

            Pose target = basePose;
            float windK = 0f;
            if (phase == PhaseWindup)
            {
                atkT += dt;
                float x = Mathf.Clamp01(atkT / atkDur);
                windK = x;
                target = Pose.Lerp(basePose, WindupPose(), x * x * (3f - 2f * x));
                float tr = Mathf.Sin(clock * 40f) * (1f + 2f * x);
                target.shR.x += tr; target.spY += tr * 0.3f;
            }
            else if (phase == PhaseStrike)
            {
                atkT += dt;
                var sp = StrikePose();
                if (atkT < StrikeIn)
                {
                    float k = atkT / StrikeIn; k = 1f - (1f - k) * (1f - k);
                    target = Pose.Lerp(snap, sp, k);
                }
                else if (atkT < StrikeIn + StrikeHold) target = sp;
                else if (atkT < StrikeIn + StrikeHold + StrikeOut)
                {
                    float k = (atkT - StrikeIn - StrikeHold) / StrikeOut;
                    target = Pose.Lerp(sp, basePose, k * k * (3f - 2f * k));
                }
                else phase = PhaseNone;
            }
            if (hitK > 0f) { target.spX -= (kind == 2 ? 10f : 22f) * hitK; target.head -= 15f * hitK; target.crouch += 0.03f * hitK; }

            float deadE = 0f;
            if (dead)
            {
                deadE = Mathf.Clamp01(deadT / DieDur);
                deadE = deadE * deadE * (3f - 2f * deadE);
                var dp = Idle();
                dp.spX = 75f; dp.head = 20f; dp.crouch = 0.15f;
                dp.shL = dp.shR = new Vector3(-80f, 0f, 40f);
                target = Pose.Lerp(target, dp, deadE);
            }
            cur = target;

            // ---- 套用姿勢
            float bob = walkAmt * v.bob * Mathf.Abs(cosP);
            if (kind == 2) bob -= walkAmt * 0.03f * Mathf.Pow(1f - Mathf.Abs(cosP), 4f);   // 坦克落腳時往下一沉
            body.localPosition = new Vector3(0f, (-cur.crouch + bob - 0.3f * deadE * deadE) * v.size, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, -cosP * (kind == 2 ? 4f : 7f) * walkAmt);
            float swell = kind == 3 ? 1f + 0.12f * windK * (0.6f + 0.4f * Mathf.Sin(pulsePh)) : 1f;
            body.localScale = Vector3.one * (v.size * (1f - 0.45f * deadE) * swell);
            float legSwing = v.legSwing * walkAmt;
            float crouchBend = cur.crouch * 150f;
            hipL.localRotation = Quaternion.Euler(sinP * legSwing - crouchBend * 0.5f - Mathf.Max(0f, cosP) * 15f * walkAmt, 0f, -4f);
            hipR.localRotation = Quaternion.Euler(-sinP * legSwing - crouchBend * 0.5f - Mathf.Max(0f, -cosP) * 15f * walkAmt, 0f, 4f);
            spine.localRotation = Quaternion.Euler(cur.spX, cur.spY + sinP * 8f * walkAmt, 0f);
            head.localRotation = Quaternion.Euler(cur.head, cur.headY, 0f);
            shL.localRotation = Quaternion.Euler(cur.shL.x, -cur.shL.y, -cur.shL.z);
            shR.localRotation = Quaternion.Euler(cur.shR.x, cur.shR.y, cur.shR.z);
            wrL.localRotation = Quaternion.Euler(cur.wrL, 0f, 0f);
            wrR.localRotation = Quaternion.Euler(cur.wrR, 0f, 0f);

            // ---- 脈動（自爆小鬼預備時越來越快）
            float pulseRate = kind == 3 ? Mathf.Lerp(5f, 30f, windK * windK) : 4f;
            pulsePh += dt * pulseRate;
            if (pulsePh > 1000f) pulsePh -= Mathf.PI * 300f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulsePh);
            if (core != null) core.localScale = coreBase * ((1f + 0.12f * pulse + 0.25f * windK * pulse) * (1f - 0.5f * deadE));

            // ---- 頭頂火焰閃爍
            if (flame != null)
            {
                float fk = 1f + 0.18f * Mathf.Sin(clock * 17f) + 0.1f * Mathf.Sin(clock * 41f + 1.3f) + 0.4f * cur.glow;
                float side = (2f - fk) * 0.9f + 0.1f;
                flame.localScale = new Vector3(flameBase.x * side, flameBase.y * side, flameBase.z * fk) * (1f - deadE);
            }

            // ---- 射手的火球：漂在右手上方，預備時變大，丟出後重新長出
            if (ball != null)
            {
                float regrow = Mathf.Clamp01((ballGone - 0.25f) / 0.4f);
                float s = regrow * (1f + 1.2f * windK + 0.08f * Mathf.Sin(clock * 19f)) * (1f - deadE);
                ball.localScale = Vector3.one * s;
                ball.position = wrR.position + Vector3.up * (0.14f * v.size * (0.6f + 0.4f * s)) + wrR.forward * 0.03f;
            }

            // ---- 岩石投擲者的巨石：平時在背上，預備時舉到頭上，丟出後在背上重新長出
            if (boulder != null)
            {
                carryK = Mathf.MoveTowards(carryK, phase == PhaseWindup ? windK : 0f, dt * 4f);
                if (phase == PhaseWindup) carryK = windK;
                float c = carryK * carryK * (3f - 2f * carryK);
                float regrow = Mathf.Clamp01((ballGone - 0.2f) / 0.8f);
                if (phase == PhaseStrike && ballGone < 0.2f) carryK = 0f;
                var back = spine.TransformPoint(new Vector3(0f, 0.48f, -0.2f));
                var over = (wrL.position + wrR.position) * 0.5f + Vector3.up * (0.16f * v.size);
                boulder.position = Vector3.Lerp(back, over, c);
                boulder.rotation = spine.rotation;
                boulder.localScale = Vector3.one * (regrow * (1f + 0.06f * windK * pulse) * (1f - deadE));
            }

            // ---- 狙擊手角尖的蓄能光（出手瞬間閃一下）
            if (tip != null)
            {
                float fl = phase == PhaseStrike && atkT < 0.12f ? 1f - atkT / 0.12f : 0f;
                float ts = 0.04f + 0.12f * windK * (0.85f + 0.15f * Mathf.Sin(clock * 30f)) + 0.1f * fl;
                tip.localScale = Vector3.one * (ts * (1f - deadE));
            }

            UpdateColors(hitK, deadE, pulse, windK);
        }

        void UpdateColors(float hitK, float deadE, float pulse, float windK)
        {
            float g = Mathf.Clamp01(cur.glow);
            var lava = Color.Lerp(v.lavaDim, v.lava, 0.6f + 0.4f * pulse);
            lava = Color.Lerp(lava, v.hot, Mathf.Clamp01(g * 0.85f + hitK * 0.6f));
            lava = Color.Lerp(lava, Dark, deadE);
            var hot = Color.Lerp(v.hot, Color.white, Mathf.Clamp01(0.3f * pulse + 0.5f * g + 0.4f * windK * pulse));
            if (kind == 3) hot = Color.Lerp(Color.Lerp(v.lava, v.hot, 0.5f), Color.white, pulse * (0.5f + 0.5f * windK));
            hot = Color.Lerp(hot, Dark, deadE);
            var eye = Color.Lerp(Color.Lerp(v.eye, Color.white, g * 0.5f), Dark, Mathf.Clamp01(deadE * 1.5f));

            mpb.Clear();
            mpb.SetColor(ColorId, lava);
            for (int i = 0; i < lavaR.Length; i++) lavaR[i].SetPropertyBlock(mpb);
            mpb.SetColor(ColorId, hot);
            for (int i = 0; i < hotR.Length; i++) hotR[i].SetPropertyBlock(mpb);
            mpb.SetColor(ColorId, eye);
            for (int i = 0; i < eyeR.Length; i++) eyeR[i].SetPropertyBlock(mpb);
            for (int i = 0; i < rockR.Length; i++)
            {
                var c = Color.Lerp(rockC[i], FlashC, hitK * 0.7f);
                c = Color.Lerp(c, RockDead, deadE);
                mpb.SetColor(ColorId, c);
                rockR[i].SetPropertyBlock(mpb);
            }
            if (flameR != null)
            {
                mpb.Clear();
                mpb.SetColor(TintId, new Color(0.5f, 0.5f, 0.5f, 0.5f * (1f - deadE)));
                mpb.SetColor(ColorId, new Color(1f, 1f, 1f, 1f - deadE));
                flameR.SetPropertyBlock(mpb);
            }
        }

        // ================================================================ 召喚特效
        /// <summary>一次性的召喚特效：地面熔岩裂口（光環收縮＋擴散）、往上噴的熔岩火花與黑煙，約 1.6 秒後自己清除。</summary>
        public static void SpawnFx(Vector3 worldFeetPos)
        {
            var lava = new Color(1f, 0.4f, 0.06f);
            var hot = new Color(1f, 0.85f, 0.5f);
            var go = new GameObject("Minion Spawn Fx");
            // 本地 XY 平面 = 地面（本地 +Z 朝下）
            go.transform.SetPositionAndRotation(worldFeetPos + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 1.6f;
            var root = go.transform;

            // 熔岩裂口：暗色底＋旋轉的鋸齒光環＋往外擴的光環
            anim.AddRing(root, SpellFx.Circle(32, 1f), true, 0.25f, new Color(0.12f, 0.04f, 0.02f, 0.75f), 0f, 1.0f, 0.3f, 0.45f, false);
            anim.AddRing(root, SpellFx.Zigzag(24, 0.75f, 1f), true, 0.04f, lava, 0f, 0.9f, 0.45f, 0.35f, true, 200f);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.06f, hot, 0.15f, 0.55f, 0.15f, 0.9f);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.1f, lava, 0.25f, 0.6f, 0.2f, 1.1f);

            var flash = SpellFx.NewPS(root, "Flash", SpellFx.AddMat(SpellFx.TexSoft), 2, false);
            {
                var m = flash.main;
                m.startLifetime = 0.6f;
                m.startSize = 1.1f;
                m.startColor = new Color(1f, 0.55f, 0.15f, 0.8f);
                SpellFx.FadeOut(flash);
                SpellFx.SizeCurve(flash, SpellFx.PopCurve);
                flash.Play(); flash.Emit(1);
            }
            var upT = new GameObject("Up").transform;
            upT.SetParent(root, false);
            upT.localRotation = Quaternion.Euler(180f, 0f, 0f);   // +Z 朝上
            var burst = SpellFx.NewPS(upT, "Lava", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            {
                var m = burst.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                m.startColor = new ParticleSystem.MinMaxGradient(lava, hot);
                m.gravityModifier = 0.9f;
                SpellFx.Cone(burst, 25f, 0.2f);
                SpellFx.FadeOut(burst);
                SpellFx.Stretch(burst, 0.05f, 1.5f);
                burst.Play(); burst.Emit(20);
            }
            var smoke = SpellFx.NewPS(upT, "Smoke", SpellFx.BlendMat(SpellFx.TexSoft), 10, true);
            {
                var m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.14f, 0.12f, 0.6f), new Color(0.08f, 0.07f, 0.07f, 0.5f));
                SpellFx.Cone(smoke, 30f, 0.2f);
                SpellFx.FadeOut(smoke);
                SpellFx.SizeCurve(smoke, SpellFx.Grow);
                SpellFx.Drag(smoke, 1.5f);
                smoke.Play(); smoke.Emit(8);
            }
        }

        // ================================================================ 建構小工具
        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static void NoShadow(Renderer r)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void Rock(Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Mesh mesh, Color c)
        {
            var go = new GameObject("Rock");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = rockMat;
            NoShadow(r);
            rockList.Add(r); rockCols.Add(c);
        }

        void Glow(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, List<Renderer> list, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = glowMat;
            NoShadow(r);
            list.Add(r);
        }
    }
}
