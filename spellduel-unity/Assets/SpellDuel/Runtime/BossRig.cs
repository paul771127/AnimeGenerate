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
    ///
    /// 主題 1「腐化樹王」（無盡模式第 20 波）：同樣的骨架與動作系統，換成一棵會走路的腐化巨樹。
    ///   外型：粗壯扭曲的樹皮軀幹（橢圓、帶螺旋樹皮稜）、樹根腳（腳掌是往四周爬開的樹根）、細長的樹枝手臂與爪狀枝指、
    ///   頭是樹幹上段挖空的臉（深陷的眼窩＋發光的綠眼、空洞的嘴），頭頂是一叢暗色樹葉與鹿角般的枝椏；
    ///   胸口樹皮裂開露出紫色的腐化心臟，樹皮上有紫色發光的脈絡、發光的紫色層菇、苔蘚、垂掛的藤蔓、背上綠色的毒囊。
    ///   動作：同一套走路／待機／預備／出手；p_stomp 會抬起右腳樹根重踏。狂暴：紫光變強、樹葉轉紅、落葉變多。
    ///   死亡：搖晃一下後像被砍倒的大樹整棵往前倒下（落地衝擊＋落葉），之後光芒熄滅、樹葉枯黃。
    ///   效能：本體約 69 個 Renderer（含粒子），材質 11 個。
    ///
    /// 主題 2「深淵海妖」（海洋主題頭目）：站在礁石上的巨大海怪，同樣的上半身骨架，下半身換成八條觸手。
    ///   外型：球根狀的章魚頭套膜（往後上方隆起、發光的青色斜眼、頭頂一圈珊瑚尖刺）、下巴垂著一圈小觸鬚、
    ///   深青色鱗皮的人形軀幹（腹部橫紋、背鰭棘、胸口一顆被兩片貝殼夾住的發光深海珍珠）、兩條帶鰭棘的長手臂與長爪；
    ///   腰下是一團肉質底座，八條粗觸手往四周攤在地上、尖端不停捲曲扭動（蒙皮網格＋骨骼，每條只用一個 Renderer）；
    ///   身上有藤壺、垂掛的海帶、水藍色發光符文（三叉戟、珍珠外圈）與生物螢光斑點。
    ///   動作：觸手爬行（MoveSpeed）、s_tentacle 抬起一條觸手高高翹起再往前砸下、s_wave 雙臂上揚再前推、
    ///   s_bubbles 吸氣（頭套膜鼓脹）後從嘴吐出一串泡泡、s_whirl 雙臂在頭上畫圈＋符文大亮、s_ink 後仰往上吐墨團、
    ///   s_summon 舉臂、眼睛放大發亮、貝殼打開。狂暴：符文轉洋紅、觸手扭得更快。
    ///   死亡：癱倒前撲、觸手失去力氣攤平、頭套膜洩氣、光芒熄滅、珊瑚白化。
    ///   效能：本體約 60 個 Renderer（含粒子；golem 約 55），材質 12 個。
    /// </summary>
    public class BossRig : MonoBehaviour
    {
        // ================================================================ 對外介面
        /// <summary>頭頂高度（公尺），血條與瞄準用。</summary>
        public const float Height = 3.2f;

        /// <summary>外觀主題：0 = 熔岩巨像，1 = 腐化樹王，2 = 深淵海妖。</summary>
        public int Theme { get; private set; }

        /// <summary>移動速度（公尺/秒），驅動沉重的踏步動作；0 = 待機呼吸搖晃。</summary>
        public float MoveSpeed { get; set; }

        /// <summary>胸口核心（熔岩核心／腐化心臟／深海珍珠）相對於根物件的本地座標（特效發射點）。</summary>
        public Vector3 ChestLocal => core != null ? transform.InverseTransformPoint(core.position) : new Vector3(0f, 2.15f, 0.65f);

        /// <summary>熔岩巨像（同 Create(0)）。</summary>
        public static BossRig Create() => Create(0);

        /// <summary>theme：0 = 熔岩巨像，1 = 腐化樹王，2 = 深淵海妖；超出範圍的值夾到 0..2。</summary>
        public static BossRig Create(int theme)
        {
            InitShaders();
            EnsureLight();
            theme = Mathf.Clamp(theme, 0, 2);
            var go = new GameObject(theme == 2 ? "Boss AbyssalKraken" : theme == 1 ? "Boss CorruptedTreant" : "Boss LavaGolem");
            var rig = go.AddComponent<BossRig>();
            rig.Theme = theme;
            rig.Build();
            BlobShadow.Attach(go.transform, theme == 2 ? 1.5f : 1.15f, theme == 2 ? 1.3f : 0.95f);   // 腳下的接觸陰影
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
            if (Theme == 0)
            {
                if (atk == "b_bolt" && spew != null) spew.Emit(28);
                return;
            }
            if (Theme == 2) { KrakenAttackFx(); return; }
            // 樹王出手的小特效
            switch (atk)
            {
                case "p_spore": if (spew != null) spew.Emit(30); break;                     // 背上毒囊噴出孢子
                case "p_summon": if (embers != null) embers.Emit(30); break;                // 紫色孢子光點爆開
                case "p_stomp": StepDust(GroundUnder(anR)); StepDust(GroundUnder(anR)); if (leafFall != null) leafFall.Emit(10); break;
                case "p_roots": StepDust(GroundUnder(wrL)); StepDust(GroundUnder(wrR)); break;
                case "p_whip": if (leafFall != null) leafFall.Emit(6); break;
            }
        }

        public void PlayHit()
        {
            if (dead || body == null) return;
            hitT = HitDur;
            if (Theme == 1 && leafFall != null) leafFall.Emit(5);   // 受擊時掉落葉
            if (Theme == 2 && drips != null && visible) drips.Emit(8);   // 海妖：甩出水滴
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
            litShader = ToonStyle.LitShader();   // 賽璐珞風格（黑邊＋兩階明暗）；不支援時退回 Legacy Diffuse
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
        static readonly Color RockAC = new Color(0.58f, 0.34f, 0.24f);     // 暖赭色玄武岩，與橘色熔岩分開
        static readonly Color RockBC = new Color(0.78f, 0.49f, 0.29f);     // 上方受光的大岩塊（肩、駝背、頭、拳）
        static readonly Color RockCC = new Color(0.37f, 0.24f, 0.34f);     // 最暗的岩塊（四肢、下巴）
        static readonly Color FlashC = new Color(1f, 0.45f, 0.3f);
        static readonly Color LavaN = new Color(1f, 0.36f, 0.05f);        // 平常的熔岩（橘紅）
        static readonly Color LavaE = new Color(1f, 0.8f, 0.35f);         // 狂暴的熔岩（白黃）
        static readonly Color LavaDim = new Color(0.55f, 0.09f, 0.02f);   // 脈動暗部
        static readonly Color HotN = new Color(1f, 0.78f, 0.35f);
        static readonly Color HotE = new Color(1f, 0.98f, 0.82f);
        static readonly Color EyeN = new Color(1f, 0.72f, 0.2f);
        static readonly Color EyeE = new Color(1f, 1f, 0.85f);
        static readonly Color DeadEmber = new Color(0.16f, 0.05f, 0.03f); // 熄滅後的餘燼

        // 腐化樹王的顏色
        static readonly Color BarkAC = new Color(0.59f, 0.31f, 0.17f);    // 赭褐樹皮，暗部帶紫色
        static readonly Color BarkBC = new Color(0.8f, 0.48f, 0.22f);     // 受光的樹瘤、肩、胸口樹皮門
        static readonly Color BarkCC = new Color(0.39f, 0.22f, 0.28f);    // 最暗的枝指、樹根、眉骨
        static readonly Color VeinN = new Color(0.62f, 0.2f, 0.95f);      // 腐化脈絡（紫）
        static readonly Color VeinE = new Color(0.95f, 0.32f, 1f);        // 狂暴（更亮的洋紫）
        static readonly Color VeinDim = new Color(0.2f, 0.05f, 0.3f);
        static readonly Color SapN = new Color(0.55f, 1f, 0.3f);          // 毒囊汁液（病態的綠）
        static readonly Color SapE = new Color(0.8f, 1f, 0.45f);
        static readonly Color TEyeN = new Color(0.72f, 1f, 0.3f);
        static readonly Color TEyeE = new Color(1f, 0.6f, 1f);
        static readonly Color TDead = new Color(0.08f, 0.07f, 0.06f);
        static readonly Color LeafN = new Color(0.27f, 0.64f, 0.17f);     // 飽和綠葉
        static readonly Color LeafE = new Color(0.86f, 0.24f, 0.12f);     // 狂暴：轉紅
        static readonly Color LeafDead = new Color(0.67f, 0.45f, 0.18f);   // 枯黃
        static readonly Color MossC = new Color(0.43f, 0.72f, 0.16f);
        static readonly Color HollowC = new Color(0.24f, 0.13f, 0.33f);   // 眼窩、嘴、胸口裂縫的深洞
        static readonly Color WitherC = new Color(0.42f, 0.36f, 0.32f);   // 死後樹皮褪成灰

        /// <summary>每個主題的一組顏色（UpdateGlow 用）。</summary>
        sealed class Palette
        {
            public Color a, b, c, glowN, glowE, glowDim, hotN, hotE, eyeN, eyeE, dead, heatN, heatE;
            public float emberLo, emberHi;
        }

        static readonly Palette GolemPal = new Palette
        {
            a = RockAC, b = RockBC, c = RockCC, glowN = LavaN, glowE = LavaE, glowDim = LavaDim, hotN = HotN, hotE = HotE,
            eyeN = EyeN, eyeE = EyeE, dead = DeadEmber, heatN = new Color(0.5f, 0.17f, 0.04f), heatE = new Color(0.5f, 0.32f, 0.04f),
            emberLo = 5f, emberHi = 16f,
        };

        static readonly Palette TreantPal = new Palette
        {
            a = BarkAC, b = BarkBC, c = BarkCC, glowN = VeinN, glowE = VeinE, glowDim = VeinDim, hotN = SapN, hotE = SapE,
            eyeN = TEyeN, eyeE = TEyeE, dead = TDead, heatN = new Color(0.2f, 0.1f, 0.3f), heatE = new Color(0.42f, 0.1f, 0.46f),
            emberLo = 4f, emberHi = 12f,
        };

        // ================================================================ 骨架與狀態
        Transform body, spine, neck, head, jaw, core, plateL, plateR;
        Transform hipL, hipR, knL, knR, anL, anR;
        Transform shL, shR, elL, elR, wrL, wrR;
        Vector3 plateRestL, plateRestR;
        Material rockA, rockB, rockC, lavaM, hotM, coreM, eyeM, heatM;
        ParticleSystem embers, halo, stepDust, spew;
        Renderer[] rends;
        Palette pal = GolemPal;

        // 樹王專用：樹葉／苔蘚／深洞材質、毒囊（脈動）、藤蔓（永遠往下垂）、落葉、倒下
        Material leafM, mossM, hollowM;
        readonly List<Transform> sacList = new List<Transform>(), vineList = new List<Transform>();
        readonly List<float> vineLenList = new List<float>();
        Transform[] sacs, vines;
        float[] vineLen;
        Vector3[] sacBase;
        ParticleSystem leafFall;
        float fallAng, leafRate = -1f;
        bool fallen;

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
            public float lift;              // 右腳抬膝（度，樹王重踏用）
            public float open, glow;        // 胸甲打開 0..1、熔岩額外亮度 0..1
            public Vector3 shL, shR;        // 肩：(前後 x、水平擺 y、往外張 z)，左右鏡像
            public float elL, elR, wrL, wrR;
            public float tent, smash, swell; // 海妖：攻擊觸手翹起 0..1、往前砸下 0..1、頭套膜鼓脹（負＝洩氣）

            public static Pose Lerp(in Pose a, in Pose b, float t)
            {
                Pose p;
                p.spX = a.spX + (b.spX - a.spX) * t;
                p.spY = a.spY + (b.spY - a.spY) * t;
                p.spZ = a.spZ + (b.spZ - a.spZ) * t;
                p.head = a.head + (b.head - a.head) * t;
                p.jaw = a.jaw + (b.jaw - a.jaw) * t;
                p.crouch = a.crouch + (b.crouch - a.crouch) * t;
                p.lift = a.lift + (b.lift - a.lift) * t;
                p.open = a.open + (b.open - a.open) * t;
                p.glow = a.glow + (b.glow - a.glow) * t;
                p.shL = Vector3.LerpUnclamped(a.shL, b.shL, t);
                p.shR = Vector3.LerpUnclamped(a.shR, b.shR, t);
                p.elL = a.elL + (b.elL - a.elL) * t;
                p.elR = a.elR + (b.elR - a.elR) * t;
                p.wrL = a.wrL + (b.wrL - a.wrL) * t;
                p.wrR = a.wrR + (b.wrR - a.wrR) * t;
                p.tent = a.tent + (b.tent - a.tent) * t;
                p.smash = a.smash + (b.smash - a.smash) * t;
                p.swell = a.swell + (b.swell - a.swell) * t;
                return p;
            }
        }

        /// <summary>待機：駝背、雙臂垂在身側微微往外張（像猩猩）。</summary>
        static Pose GolemIdle()
        {
            var p = new Pose { spX = 12f, head = -10f, crouch = 0.06f, elL = -25f, elR = -25f, wrL = -10f, wrR = -10f };
            p.shL = p.shR = new Vector3(-12f, 0f, 14f);
            return p;
        }

        static Pose GolemWindup(string id)
        {
            var p = GolemIdle();
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

        static Pose GolemStrike(string id)
        {
            var p = GolemIdle();
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

        static Pose GolemDead()
        {
            var p = GolemIdle();
            p.spX = 42f; p.head = 30f; p.jaw = 0.25f; p.crouch = 0.5f;
            p.shL = p.shR = new Vector3(5f, 0f, 6f); p.elL = p.elR = -5f; p.wrL = p.wrR = 0f;
            return p;
        }

        // 依主題分派
        Pose Idle() => Theme == 2 ? KrakenIdle() : Theme == 1 ? TreantIdle() : GolemIdle();
        Pose WindupPose(string id) => Theme == 2 ? KrakenWindup(id) : Theme == 1 ? TreantWindup(id) : GolemWindup(id);
        Pose StrikePose(string id) => Theme == 2 ? KrakenStrike(id) : Theme == 1 ? TreantStrike(id) : GolemStrike(id);
        Pose DeadPose() => Theme == 2 ? KrakenDead() : Theme == 1 ? TreantDead() : GolemDead();

        /// <summary>樹王待機：微微前傾、長長的樹枝手臂垂到膝下、爪子幾乎拖地。</summary>
        static Pose TreantIdle()
        {
            var p = new Pose { spX = 8f, head = -6f, crouch = 0.05f, elL = -18f, elR = -18f, wrL = -10f, wrR = -10f };
            p.shL = p.shR = new Vector3(-8f, 0f, 16f);
            return p;
        }

        static Pose TreantWindup(string id)
        {
            var p = TreantIdle();
            switch (id)
            {
                case "p_stomp":   // 抬起右腳樹根、雙臂張開保持平衡、往後仰
                    p.spX = -10f; p.spZ = 7f; p.head = -14f; p.jaw = 0.4f; p.crouch = 0.03f; p.lift = 80f;
                    p.shL = p.shR = new Vector3(-110f, 0f, 55f); p.elL = p.elR = -40f; p.wrL = p.wrR = -20f;
                    p.glow = 0.5f;
                    break;
                case "p_whip":    // 右臂往後側大幅拉開、身體往右扭（藤鞭蓄力）
                    p.spX = 6f; p.spY = 40f; p.spZ = -6f; p.head = -6f; p.crouch = 0.14f;
                    p.shR = new Vector3(-10f, 60f, 80f); p.elR = -30f; p.wrR = -25f;
                    p.shL = new Vector3(-45f, 0f, 25f); p.elL = -60f;
                    p.glow = 0.35f;
                    break;
                case "p_thorn":   // 過肩投擲：右臂往上往後拉、身體右轉
                    p.spX = -6f; p.spY = 32f; p.spZ = -8f; p.head = -8f; p.crouch = 0.1f;
                    p.shR = new Vector3(-200f, 0f, 22f); p.elR = -55f; p.wrR = 30f;
                    p.shL = new Vector3(-70f, 0f, 30f); p.elL = -25f;
                    p.glow = 0.4f;
                    break;
                case "p_spore":   // 彎腰縮起、背上毒囊鼓脹
                    p.spX = 30f; p.head = 22f; p.jaw = 0.2f; p.crouch = 0.22f;
                    p.shL = p.shR = new Vector3(-20f, 0f, 38f); p.elL = p.elR = -45f; p.wrL = p.wrR = -20f;
                    p.glow = 1f;
                    break;
                case "p_roots":   // 雙臂高舉，準備把手插進地面
                    p.spX = -14f; p.head = -18f; p.jaw = 0.4f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-165f, 0f, 20f); p.elL = p.elR = -40f; p.wrL = p.wrR = -10f;
                    p.glow = 0.5f;
                    break;
                case "p_summon":  // 雙臂往兩側斜上舉、胸口樹皮微開
                    p.spX = -8f; p.head = -20f; p.jaw = 0.5f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-140f, 0f, 45f); p.elL = p.elR = -20f; p.wrL = p.wrR = -10f;
                    p.glow = 0.7f; p.open = 0.5f;
                    break;
                default:          // 未知招式：雙臂舉起
                    p.spX = -12f; p.head = -14f; p.jaw = 0.3f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-150f, 0f, 20f); p.elL = p.elR = -45f; p.wrL = p.wrR = -15f;
                    p.glow = 0.4f;
                    break;
            }
            return p;
        }

        static Pose TreantStrike(string id)
        {
            var p = TreantIdle();
            switch (id)
            {
                case "p_stomp":   // 右腳重重踏下、蹲低、雙臂往下甩
                    p.spX = 24f; p.head = 10f; p.jaw = 0.8f; p.crouch = 0.3f; p.lift = 0f;
                    p.shL = p.shR = new Vector3(-35f, 0f, 42f); p.elL = p.elR = -20f;
                    p.glow = 0.7f;
                    break;
                case "p_whip":    // 右臂在胸口高度橫掃到左前方
                    p.spX = 10f; p.spY = -45f; p.spZ = 6f; p.head = 0f; p.crouch = 0.16f;
                    p.shR = new Vector3(-10f, -75f, 82f); p.elR = -10f; p.wrR = 5f;
                    p.shL = new Vector3(-20f, 0f, 35f); p.elL = -40f;
                    p.glow = 0.3f;
                    break;
                case "p_thorn":   // 右臂往前甩出、枝指張開
                    p.spX = 18f; p.spY = -25f; p.spZ = 4f; p.head = 2f; p.crouch = 0.14f;
                    p.shR = new Vector3(-80f, -15f, 22f); p.elR = -8f; p.wrR = 25f;
                    p.shL = new Vector3(-10f, 0f, 30f); p.elL = -30f;
                    p.glow = 0.4f;
                    break;
                case "p_spore":   // 猛然挺身後仰、雙臂往後甩，毒囊噴發
                    p.spX = -20f; p.head = -32f; p.jaw = 1f; p.crouch = 0.06f;
                    p.shL = p.shR = new Vector3(30f, 0f, 55f); p.elL = p.elR = -15f; p.wrL = p.wrR = 0f;
                    p.glow = 0.8f;
                    break;
                case "p_roots":   // 彎腰下蹲，雙手插進正前方地面
                    p.spX = 30f; p.head = 14f; p.jaw = 0.7f; p.crouch = 0.3f;
                    p.shL = p.shR = new Vector3(-50f, 0f, 16f); p.elL = p.elR = -10f; p.wrL = p.wrR = 10f;
                    p.glow = 0.6f;
                    break;
                case "p_summon":  // 雙臂高舉向天、仰頭、胸口樹皮完全打開露出心臟
                    p.spX = -14f; p.head = -30f; p.jaw = 1f; p.crouch = 0.02f;
                    p.shL = p.shR = new Vector3(-168f, 0f, 50f); p.elL = p.elR = -5f; p.wrL = p.wrR = -30f;
                    p.glow = 1f; p.open = 1f;
                    break;
                default:          // 未知招式：雙臂往前下方砸
                    p.spX = 28f; p.head = 10f; p.jaw = 0.6f; p.crouch = 0.26f;
                    p.shL = p.shR = new Vector3(-60f, 0f, 14f); p.elL = p.elR = -12f; p.wrL = p.wrR = 0f;
                    p.glow = 0.5f;
                    break;
            }
            return p;
        }

        /// <summary>樹王死亡：樹幹打直、手臂往兩側攤開（之後整棵往前倒，見 UpdateFall）。</summary>
        static Pose TreantDead()
        {
            var p = TreantIdle();
            // 手臂往兩側張開：倒下後平躺在地上（往前伸的手臂倒下時會插進地面）
            p.spX = 0f; p.head = 14f; p.jaw = 0.7f; p.crouch = 0.12f;
            p.shL = p.shR = new Vector3(-12f, 0f, 65f); p.elL = p.elR = -10f; p.wrL = p.wrR = 0f;
            return p;
        }

        // ================================================================ 建立
        void Build()
        {
            pal = Theme == 2 ? KrakenPal : Theme == 1 ? TreantPal : GolemPal;
            if (Theme == 2) BuildKraken(); else if (Theme == 1) BuildTreant(); else BuildGolem();

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
            sacs = sacList.ToArray();
            sacBase = new Vector3[sacs.Length];
            for (int i = 0; i < sacs.Length; i++) sacBase[i] = sacs[i].localScale;
            vines = vineList.ToArray();
            vineLen = vineLenList.ToArray();

            cur = Idle();
            Animate(0f);
        }

        void BuildGolem()
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
            BuildHeatGlow();
        }

        /// <summary>腳下的地面光（熔岩熱光／樹王的毒霧光），顏色在 UpdateGlow 設定。</summary>
        void BuildHeatGlow()
        {
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

        // ================================================================ 建立：腐化樹王
        // 軀幹樹幹的剖面（脊椎本地座標：高度、中心前後、半徑），物件再壓成橢圓（寬 TrunkSX、深 TrunkSZ）
        static readonly float[] TrunkY = { -0.25f, 0.2f, 0.6f, 0.95f, 1.2f, 1.4f };
        static readonly float[] TrunkZ = { 0f, 0.02f, 0.04f, 0.03f, 0f, -0.03f };
        static readonly float[] TrunkR = { 0.5f, 0.52f, 0.6f, 0.62f, 0.52f, 0.34f };
        const float TrunkSX = 1.15f, TrunkSZ = 0.85f;
        static readonly float[] BarkJag = { 1f, 0.84f, 1.08f, 0.9f, 1.04f, 0.82f, 1.09f, 0.88f };
        static readonly float[] TwigJag = { 1f, 0.85f, 1.1f, 0.9f, 1.05f };
        static readonly float[] LeafJag = { 1.2f, 0.85f, 1.15f, 0.8f, 1.25f, 0.9f, 1.1f, 0.82f, 1.18f };

        void BuildTreant()
        {
            // 每隻頭目自己的材質（閃色、脈動、狂暴變色、枯萎）
            rockA = new Material(litShader) { color = BarkAC, name = "TreantBarkA" };
            rockB = new Material(litShader) { color = BarkBC, name = "TreantBarkB" };
            rockC = new Material(litShader) { color = BarkCC, name = "TreantBarkC" };
            leafM = new Material(litShader) { color = LeafN, name = "TreantLeaf" };
            mossM = new Material(litShader) { color = MossC, name = "TreantMoss" };
            lavaM = new Material(glowShader) { color = VeinN, name = "TreantVein" };
            hotM = new Material(glowShader) { color = SapN, name = "TreantSap" };
            coreM = new Material(glowShader) { color = VeinN, name = "TreantHeart" };
            eyeM = new Material(glowShader) { color = TEyeN, name = "TreantEye" };
            hollowM = new Material(glowShader) { color = HollowC, name = "TreantHollow" };

            body = Pivot("Body", transform, Vector3.zero);

            // ---- 樹根腳（髖 → 膝 → 腳踝），腳掌是往四周爬開的樹根，根尖剛好碰地
            hipL = BuildRootLeg(-1f, out knL, out anL);
            hipR = BuildRootLeg(1f, out knR, out anR);

            // ---- 骨盆樹瘤＋垂在腰間的氣根
            RockPart(body, new Vector3(0f, 1.12f, -0.02f), new Vector3(1f, 0.5f, 0.72f), rockA, Vector3.zero, 0);
            MeshPart(body, TreantMesh("skirt", SkirtRoots), rockC, Vector3.zero, Vector3.zero, Vector3.one);

            // ---- 樹幹軀幹（樞紐在腰）：橢圓、螺旋樹皮稜、紫色腐化脈絡
            spine = Pivot("Spine", body, new Vector3(0f, 1.3f, 0f));
            MeshPart(spine, TreantMesh("trunk", TrunkMesh), rockA, Vector3.zero, Vector3.zero, new Vector3(TrunkSX, 1f, TrunkSZ));
            MeshPart(spine, TreantMesh("veins", ChestVeins), lavaM, Vector3.zero, Vector3.zero, Vector3.one);

            // 胸口裂開的深洞＋腐化心臟（紫殼綠芯）＋兩扇會打開的樹皮門
            Part(PrimitiveType.Sphere, spine, new Vector3(0f, 0.9f, 0.44f), new Vector3(0.44f, 0.62f, 0.24f), hollowM);
            core = Pivot("Core", spine, new Vector3(0f, 0.9f, 0.5f));
            Part(PrimitiveType.Sphere, core, Vector3.zero, Vector3.one * 0.34f, coreM);
            Part(PrimitiveType.Sphere, core, new Vector3(0f, 0f, 0.1f), Vector3.one * 0.17f, hotM);
            plateL = RockPart(spine, new Vector3(-0.17f, 0.9f, 0.6f), new Vector3(0.3f, 0.62f, 0.2f), rockB, Vector3.zero, 1).transform;
            plateR = RockPart(spine, new Vector3(0.17f, 0.9f, 0.6f), new Vector3(0.3f, 0.62f, 0.2f), rockB, Vector3.zero, 2).transform;
            plateRestL = plateL.localPosition; plateRestR = plateR.localPosition;

            // 背上的大樹瘤、綠色毒囊（兩顆在背、一顆在左胸）、苔蘚、發光層菇
            RockPart(spine, new Vector3(0f, 1f, -0.36f), new Vector3(1.1f, 0.7f, 0.6f), rockB, new Vector3(20f, 0f, 0f), 1);
            Sac(spine, new Vector3(-0.3f, 1.12f, -0.62f), 0.3f);
            Sac(spine, new Vector3(0.34f, 0.95f, -0.62f), 0.26f);
            Sac(spine, new Vector3(-0.46f, 1.02f, 0.4f), 0.22f);
            RockPart(spine, new Vector3(0f, 1.28f, -0.5f), new Vector3(0.75f, 0.2f, 0.45f), mossM, new Vector3(30f, 0f, 0f), 0);
            RockPart(spine, new Vector3(0.42f, 0.3f, 0.33f), new Vector3(0.42f, 0.32f, 0.18f), mossM, new Vector3(0f, 40f, 0f), 2);
            MeshPart(spine, TreantMesh("fungus", FungusMesh), lavaM, new Vector3(-0.5f, 0.45f, 0.3f), new Vector3(0f, -55f, 0f), Vector3.one);
            MeshPart(spine, TreantMesh("fungus", FungusMesh), lavaM, new Vector3(0.48f, 0.72f, -0.32f), new Vector3(0f, 125f, 0f), Vector3.one * 0.85f);
            // 從兩肩前緣垂下的藤蔓
            Vine(spine, new Vector3(-0.72f, 1.25f, 0.28f), 0.8f, 11);
            Vine(spine, new Vector3(0.74f, 1.2f, 0.25f), 0.65f, 12);

            // ---- 頭＝樹幹上段挖空的臉（脖子 → 頭 → 下巴），埋在兩肩樹瘤之間
            neck = Pivot("Neck", spine, new Vector3(0f, 1.08f, 0.06f));
            head = Pivot("Head", neck, new Vector3(0f, 0.06f, 0.04f));
            MeshPart(head, TreantMesh("head", HeadMesh), rockA, Vector3.zero, Vector3.zero, new Vector3(1.05f, 1f, 0.95f));
            RockPart(head, new Vector3(0f, 0.36f, 0.3f), new Vector3(0.66f, 0.13f, 0.2f), rockC, new Vector3(20f, 0f, 0f), 2);   // 眉骨
            for (int s = -1; s <= 1; s += 2)
            {
                Part(PrimitiveType.Sphere, head, new Vector3(0.14f * s, 0.24f, 0.33f), new Vector3(0.17f, 0.12f, 0.12f), hollowM, new Vector3(0f, 0f, -12f * s));   // 深陷的眼窩
                Part(PrimitiveType.Sphere, head, new Vector3(0.14f * s, 0.235f, 0.39f), new Vector3(0.085f, 0.045f, 0.04f), eyeM, new Vector3(0f, 0f, 14f * s));  // 斜吊的綠眼
            }
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.04f, 0.33f), new Vector3(0.3f, 0.12f, 0.14f), hollowM);   // 空洞的嘴（出招時透出紫光）
            jaw = Pivot("Jaw", head, new Vector3(0f, 0f, 0.12f));
            RockPart(jaw, new Vector3(0f, -0.08f, 0.2f), new Vector3(0.44f, 0.16f, 0.26f), rockC, Vector3.zero, 2);

            // 頭冠：鹿角般的枝椏＋五叢暗色樹葉＋兩顆發光的紫色果莢
            MeshPart(head, TreantMesh("crown", CrownBranches), rockC, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(head, LeafMesh(0), leafM, new Vector3(0f, 0.62f, -0.08f), new Vector3(0f, 20f, 0f), new Vector3(0.85f, 0.42f, 0.7f));
            MeshPart(head, LeafMesh(1), leafM, new Vector3(-0.4f, 0.52f, -0.12f), new Vector3(0f, 70f, 0f), new Vector3(0.55f, 0.4f, 0.55f));
            MeshPart(head, LeafMesh(2), leafM, new Vector3(0.42f, 0.54f, -0.08f), new Vector3(0f, -40f, 0f), new Vector3(0.52f, 0.38f, 0.5f));
            MeshPart(head, LeafMesh(1), leafM, new Vector3(-0.12f, 0.57f, -0.45f), new Vector3(0f, 160f, 0f), new Vector3(0.6f, 0.42f, 0.48f));
            MeshPart(head, LeafMesh(0), leafM, new Vector3(0.25f, 0.46f, -0.42f), new Vector3(0f, -120f, 0f), new Vector3(0.5f, 0.36f, 0.45f));
            Part(PrimitiveType.Sphere, head, new Vector3(-0.42f, 0.36f, 0.12f), Vector3.one * 0.12f, lavaM);
            Part(PrimitiveType.Sphere, head, new Vector3(0.45f, 0.33f, 0.02f), Vector3.one * 0.1f, lavaM);

            // ---- 手臂（肩 → 肘 → 手腕）：細長的樹枝、爪狀枝指，垂到膝下
            shL = BuildBranchArm(-1f, out elL, out wrL);
            shR = BuildBranchArm(1f, out elR, out wrR);

            // ---- 特效：腐化孢子光點、心臟光暈、腳步揚塵、背上毒囊噴發、落葉、腳下毒霧光
            embers = SpellFx.NewPS(spine, "Motes", SpellFx.AddMat(SpellFx.TexSoft), 60, true);
            embers.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.3f, 1f), new Color(0.6f, 1f, 0.35f));
                SpellFx.Sphere(embers, 1.1f);
                SpellFx.FadeInOut(embers);
                SpellFx.SizeCurve(embers, SpellFx.Shrink);
                SpellFx.Rise(embers, 0.2f);
                SpellFx.Noise(embers, 0.35f, 0.9f);
                SpellFx.Rate(embers, 4f);
                embers.Play();
            }
            halo = SpellFx.NewPS(core, "HeartHalo", SpellFx.AddMat(SpellFx.TexSoft), 8, false);
            {
                var m = halo.main;
                m.startLifetime = 0.7f;
                m.startSize = 1f;
                m.startColor = new Color(0.6f, 0.2f, 0.95f, 0.5f);
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
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.36f, 0.3f, 0.22f, 0.5f), new Color(0.24f, 0.2f, 0.15f, 0.4f));
                SpellFx.FadeOut(stepDust);
                SpellFx.SizeCurve(stepDust, SpellFx.Grow);
                SpellFx.Drag(stepDust, 3f);
                stepDust.Play();
            }
            var sporeT = Pivot("SporeVent", spine, new Vector3(0f, 1.15f, -0.55f));
            sporeT.localRotation = Quaternion.Euler(-105f, 0f, 0f);   // 往上偏後噴
            spew = SpellFx.NewPS(sporeT, "SporeBurst", SpellFx.AddMat(SpellFx.TexSoft), 40, true);
            {
                var m = spew.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.3f, 0.8f), new Color(0.7f, 0.25f, 1f, 0.7f));
                m.gravityModifier = 0.35f;
                SpellFx.Cone(spew, 30f, 0.25f);
                SpellFx.FadeOut(spew);
                SpellFx.SizeCurve(spew, SpellFx.Grow);
                SpellFx.Drag(spew, 1.5f);
                spew.Play();
            }
            leafFall = SpellFx.NewPS(head, "LeafFall", SpellFx.BlendMat(SpellFx.TexLeaf), 50, true);
            leafFall.transform.localPosition = new Vector3(0f, 0.55f, -0.15f);
            {
                var m = leafFall.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.gravityModifier = 0.06f;
                SpellFx.Sphere(leafFall, 0.5f);
                SpellFx.FadeOut(leafFall);
                SpellFx.Spin(leafFall, 3f);
                SpellFx.Noise(leafFall, 0.4f, 0.8f);
                SpellFx.Drag(leafFall, 1.2f);
                leafFall.Play();
            }
            BuildHeatGlow();
        }

        Transform BuildRootLeg(float s, out Transform knee, out Transform ankle)
        {
            string side = s < 0f ? "L" : "R";
            var hip = Pivot("Hip" + side, body, new Vector3(0.38f * s, HipY, 0f));
            MeshPart(hip, TreantMesh("thigh" + side, m => Thigh(m, s)), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            knee = Pivot("Knee" + side, hip, new Vector3(0f, -L1, 0f));
            RockPart(knee, new Vector3(0f, 0f, 0.03f), new Vector3(0.42f, 0.38f, 0.44f), rockB, new Vector3(0f, 20f * s, 0f), 1);   // 膝上樹瘤
            MeshPart(knee, TreantMesh("shin", Shin), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            ankle = Pivot("Ankle" + side, knee, new Vector3(0f, -L2, 0f));
            RockPart(ankle, new Vector3(0f, 0f, 0.04f), new Vector3(0.5f, 0.22f, 0.55f), rockC, Vector3.zero, 2);
            MeshPart(ankle, TreantMesh("foot", FootRoots), rockC, Vector3.zero, new Vector3(0f, 8f * s, 0f), Vector3.one);
            return hip;
        }

        Transform BuildBranchArm(float s, out Transform elbow, out Transform wrist)
        {
            string side = s < 0f ? "L" : "R";
            var sh = Pivot("Shoulder" + side, spine, new Vector3(0.8f * s, 1.15f, -0.02f));
            RockPart(sh, new Vector3(0.1f * s, 0.04f, 0f), new Vector3(0.6f, 0.5f, 0.6f), rockB, new Vector3(0f, 0f, 15f * s), 1);   // 肩上樹瘤
            RockPart(sh, new Vector3(0.12f * s, 0.26f, 0f), new Vector3(0.5f, 0.14f, 0.5f), mossM, new Vector3(0f, 0f, 12f * s), 0); // 苔蘚
            MeshPart(sh, TreantMesh("upper" + side, m => UpperArm(m, s)), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            elbow = Pivot("Elbow" + side, sh, new Vector3(0f, -0.86f, 0f));
            RockPart(elbow, Vector3.zero, new Vector3(0.34f, 0.32f, 0.34f), rockB, new Vector3(0f, 30f * s, 0f), 2);
            MeshPart(elbow, TreantMesh("fore" + side, m => Forearm(m, s, false)), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(elbow, TreantMesh("foreVein" + side, m => Forearm(m, s, true)), lavaM, Vector3.zero, Vector3.zero, Vector3.one);
            Vine(elbow, new Vector3(0.1f * s, -0.22f, -0.1f), 0.55f, s < 0f ? 13 : 14);
            wrist = Pivot("Wrist" + side, elbow, new Vector3(0f, -0.9f, 0.05f));
            RockPart(wrist, new Vector3(0f, -0.05f, 0.02f), new Vector3(0.28f, 0.24f, 0.28f), rockC, Vector3.zero, 0);
            MeshPart(wrist, TreantMesh("claw" + side, m => Claws(m, s)), rockC, Vector3.zero, Vector3.zero, Vector3.one);
            return sh;
        }

        /// <summary>發光的毒囊（脈動、蓄力時鼓脹，見 UpdateTreantBits）。</summary>
        void Sac(Transform parent, Vector3 pos, float size)
        {
            sacList.Add(Part(PrimitiveType.Sphere, parent, pos, Vector3.one * size, hotM).transform);
        }

        /// <summary>垂掛的藤蔓：樞紐每幀轉成「永遠往下垂」再輕輕搖晃（見 UpdateTreantBits）。</summary>
        void Vine(Transform parent, Vector3 pos, float len, int seed)
        {
            var piv = Pivot("Vine", parent, pos);
            MeshPart(piv, TreantMesh("vine" + seed, m => VineMesh(m, len, seed)), mossM, Vector3.zero, Vector3.zero, Vector3.one);
            vineList.Add(piv);
            vineLenList.Add(len);
        }

        GameObject MeshPart(Transform parent, Mesh mesh, Material mat, Vector3 localPos, Vector3 euler, Vector3 localScale)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            partList?.Add(go.transform);
            return go;
        }

        /// <summary>有光照的程式網格（平面著色），依 key 快取共用。</summary>
        static Mesh TreantMesh(string key, System.Action<PMesh> build)
        {
            key = "treant." + key;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var pm = new PMesh(1);
            build(pm);
            var mesh = pm.Build("Boss " + key, true);
            meshCache[key] = mesh;
            return mesh;
        }

        // ---------------------------------------------------------------- 樹王的網格
        /// <summary>
        /// 沿路徑掃出的管子（樹幹、樹枝、樹根、藤蔓、爪）：rad＝每點半徑（0＝收成尖），面朝外。
        /// jag 每邊半徑倍率（樹皮稜）、twistDeg 每一圈多轉幾度（螺旋紋）、capStart/capEnd 封住兩端。
        /// </summary>
        static void Tube(PMesh m, Vector3[] path, float[] rad, int sides, float[] jag = null, float twistDeg = 0f, bool capStart = false, bool capEnd = false)
        {
            int n = path.Length;
            var ring = new Vector3[n * sides];
            var nrm = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var T = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                // 平行移動的參考軸，避免管子自己扭轉
                if (i == 0) nrm = Vector3.Cross(T, Mathf.Abs(T.y) < 0.9f ? Vector3.up : Vector3.right);
                else nrm -= T * Vector3.Dot(nrm, T);
                if (nrm.sqrMagnitude < 1e-8f) nrm = Vector3.Cross(T, Vector3.right);
                nrm.Normalize();
                var bin = Vector3.Cross(T, nrm);   // nrm × bin = T（與 PMesh.Lathe 的 x、y、z 同向，面朝外）
                for (int j = 0; j < sides; j++)
                {
                    float a = (twistDeg * i + 360f * j / sides) * Mathf.Deg2Rad;
                    float k = jag != null ? jag[j % jag.Length] : 1f;
                    ring[i * sides + j] = path[i] + (nrm * Mathf.Cos(a) + bin * Mathf.Sin(a)) * (rad[i] * k);
                }
            }
            var white = Color.white;
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    var p00 = ring[i * sides + j]; var p01 = ring[i * sides + j1];
                    var p10 = ring[(i + 1) * sides + j]; var p11 = ring[(i + 1) * sides + j1];
                    if (rad[i + 1] > 0f) m.Tri(p00, p11, p10, white);
                    if (rad[i] > 0f) m.Tri(p00, p01, p11, white);
                }
            if (capStart && rad[0] > 0f)
                for (int j = 0; j < sides; j++) m.Tri(path[0], ring[(j + 1) % sides], ring[j], white);
            if (capEnd && rad[n - 1] > 0f)
            {
                int b = (n - 1) * sides;
                for (int j = 0; j < sides; j++) m.Tri(ring[b + j], ring[b + (j + 1) % sides], path[n - 1], white);
            }
        }

        /// <summary>從 a 到 b 的彎曲路徑（二次曲線，bend＝中點的偏移量），中間點加一點固定亂數的抖動（樹枝的節瘤感）。</summary>
        static Vector3[] Gnarl(Vector3 a, Vector3 b, Vector3 bend, int n, int seed, float jit)
        {
            var r = new System.Random(seed);
            var c = (a + b) * 0.5f + bend * 2f;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / (n - 1), u = 1f - t;
                var p = a * (u * u) + c * (2f * u * t) + b * (t * t);
                if (i > 0 && i < n - 1) p += new Vector3(Rnd(r, -jit, jit), Rnd(r, -jit, jit) * 0.5f, Rnd(r, -jit, jit));
                pts[i] = p;
            }
            return pts;
        }

        static float[] Taper(int n, float r0, float r1)
        {
            var r = new float[n];
            for (int i = 0; i < n; i++) r[i] = Mathf.Lerp(r0, r1, (float)i / (n - 1));
            return r;
        }

        /// <summary>從 from 往 dir 長出一根逐漸變尖的細枝（爪、刺、分岔）。</summary>
        static void Twig(PMesh m, Vector3 from, Vector3 dir, float len, float r0, Vector3 bend, int seed, int sides = 5)
        {
            var path = Gnarl(from, from + dir.normalized * len, bend, 4, seed, len * 0.04f);
            Tube(m, path, new[] { r0, r0 * 0.72f, r0 * 0.4f, 0f }, sides, TwigJag, 0f, true);
        }

        static void TrunkMesh(PMesh m)
        {
            int n = TrunkY.Length;
            var path = new Vector3[n];
            for (int i = 0; i < n; i++) path[i] = new Vector3(0f, TrunkY[i], TrunkZ[i]);
            Tube(m, path, TrunkR, 8, BarkJag, 14f, true, true);
        }

        /// <summary>樹幹表面（脊椎本地座標，已含橢圓壓扁）：高度 y、繞軸角度 deg（0＝正前方、正＝往 +X），out 往外推的倍率。</summary>
        static Vector3 TrunkSurface(float y, float deg, float outK)
        {
            int i = 0;
            while (i < TrunkY.Length - 2 && y > TrunkY[i + 1]) i++;
            float t = Mathf.Clamp01((y - TrunkY[i]) / (TrunkY[i + 1] - TrunkY[i]));
            float r = Mathf.Lerp(TrunkR[i], TrunkR[i + 1], t) * outK;
            float zc = Mathf.Lerp(TrunkZ[i], TrunkZ[i + 1], t);
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * r * TrunkSX, y, (zc + Mathf.Cos(a) * r) * TrunkSZ);
        }

        /// <summary>從胸口裂縫往外蔓延的紫色脈絡（貼在樹幹表面外一點點）。</summary>
        static void ChestVeins(PMesh m)
        {
            // (起點角度, 終點角度, 起點高度, 終點高度)
            var v = new[]
            {
                new Vector4(18f, 75f, 1.0f, 1.3f), new Vector4(-18f, -78f, 1.02f, 1.28f),
                new Vector4(15f, 60f, 0.75f, 0.15f), new Vector4(-15f, -55f, 0.72f, 0.05f),
                new Vector4(4f, -8f, 0.62f, -0.1f), new Vector4(25f, 100f, 0.9f, 0.85f),
            };
            const int N = 7;
            for (int k = 0; k < v.Length; k++)
            {
                var path = new Vector3[N];
                var rad = new float[N];
                for (int i = 0; i < N; i++)
                {
                    float t = (float)i / (N - 1);
                    float deg = Mathf.Lerp(v[k].x, v[k].y, t) + 6f * Mathf.Sin(t * Mathf.PI * 3f + k) * (1f - t * 0.5f);
                    float y = Mathf.Lerp(v[k].z, v[k].w, t);
                    path[i] = TrunkSurface(y, deg, 1.07f);
                    rad[i] = i == N - 1 ? 0f : Mathf.Lerp(0.035f, 0.012f, t);
                }
                Tube(m, path, rad, 4, null, 45f);
            }
        }

        static void HeadMesh(PMesh m)
        {
            var path = new[] { new Vector3(0f, -0.18f, 0f), new Vector3(0f, 0.1f, 0.02f), new Vector3(0f, 0.35f, 0f), new Vector3(0f, 0.55f, -0.04f) };
            Tube(m, path, new[] { 0.36f, 0.4f, 0.38f, 0.3f }, 8, BarkJag, -10f, true, true);
        }

        /// <summary>頭冠：從頭頂往上、往外岔開的鹿角狀枝椏，每根中段再分一小岔。</summary>
        static void CrownBranches(PMesh m)
        {
            var starts = new[]
            {
                new Vector3(-0.12f, 0.5f, 0f), new Vector3(0.12f, 0.5f, 0.02f), new Vector3(-0.18f, 0.45f, -0.12f), new Vector3(0.18f, 0.45f, -0.1f),
                new Vector3(0f, 0.5f, -0.15f), new Vector3(-0.05f, 0.52f, 0.08f), new Vector3(0.1f, 0.42f, -0.2f),
            };
            var dirs = new[]
            {
                new Vector3(-0.45f, 1f, 0.15f), new Vector3(0.5f, 1f, 0.1f), new Vector3(-1f, 0.55f, -0.2f), new Vector3(1f, 0.6f, -0.15f),
                new Vector3(0.05f, 1f, -0.6f), new Vector3(-0.1f, 1f, 0.45f), new Vector3(0.6f, 0.7f, -0.8f),
            };
            var lens = new[] { 0.45f, 0.45f, 0.6f, 0.58f, 0.48f, 0.36f, 0.5f };
            for (int i = 0; i < starts.Length; i++)
            {
                var d = dirs[i].normalized;
                var side = Vector3.Cross(d, Vector3.forward);
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                side.Normalize();
                var path = Gnarl(starts[i], starts[i] + d * lens[i], side * 0.05f, 5, 40 + i, 0.02f);
                Tube(m, path, new[] { 0.075f, 0.06f, 0.042f, 0.022f, 0f }, 5, TwigJag, 0f, true);
                // 分岔
                var fd = (d + side * ((i & 1) == 0 ? 0.9f : -0.9f) + Vector3.up * 0.3f).normalized;
                Twig(m, path[2], fd, lens[i] * 0.45f, 0.035f, Vector3.up * 0.02f, 60 + i, 4);
            }
        }

        static void SkirtRoots(PMesh m)
        {
            var angs = new[] { -18f, 18f, 155f, 180f, 205f };
            for (int i = 0; i < angs.Length; i++)
            {
                float a = angs[i] * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var s = new Vector3(d.x * 0.42f, 1.0f, d.z * 0.32f);
                var e = s + new Vector3(d.x * 0.15f, -0.34f - 0.06f * (i % 2), d.z * 0.12f);
                Tube(m, Gnarl(s, e, d * 0.05f, 5, 80 + i, 0.02f), new[] { 0.07f, 0.06f, 0.045f, 0.025f, 0f }, 5, TwigJag, 0f, true);
            }
        }

        static void Thigh(PMesh m, float s)
        {
            var path = Gnarl(new Vector3(0f, 0.08f, 0f), new Vector3(0.03f * s, -0.58f, 0.02f), new Vector3(0.04f * s, 0f, 0.03f), 5, 20, 0.015f);
            Tube(m, path, Taper(5, 0.25f, 0.19f), 8, BarkJag, 12f, true, true);
        }

        static void Shin(PMesh m)
        {
            var path = Gnarl(new Vector3(0f, 0.04f, 0f), new Vector3(0f, -0.47f, 0f), new Vector3(0f, 0f, 0.03f), 5, 21, 0.015f);
            Tube(m, path, new[] { 0.18f, 0.18f, 0.2f, 0.23f, 0.27f }, 8, BarkJag, -12f, true, true);
        }

        /// <summary>腳掌：七條從腳踝往四周爬開、根尖貼地（腳踝本地 y = -0.1 為地面）的樹根。</summary>
        static void FootRoots(PMesh m)
        {
            var angs = new[] { -14f, 14f, -48f, 48f, -105f, 105f, 180f };
            var lens = new[] { 0.58f, 0.58f, 0.48f, 0.48f, 0.38f, 0.38f, 0.32f };
            for (int i = 0; i < angs.Length; i++)
            {
                float a = angs[i] * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float L = lens[i];
                var path = new[]
                {
                    d * 0.06f + new Vector3(0f, 0.06f, 0f), d * (L * 0.35f) + new Vector3(0f, 0.02f, 0f),
                    d * (L * 0.7f) + new Vector3(0f, -0.06f, 0f), d * L + new Vector3(0f, -0.095f, 0f),
                };
                Tube(m, path, new[] { 0.1f, 0.085f, 0.05f, 0f }, 5, TwigJag, 0f, true);
            }
        }

        static void UpperArm(PMesh m, float s)
        {
            var path = Gnarl(new Vector3(0f, 0.06f, 0f), new Vector3(0.02f * s, -0.88f, 0f), new Vector3(0.05f * s, 0f, -0.03f), 5, 30, 0.015f);
            Tube(m, path, Taper(5, 0.21f, 0.15f), 8, BarkJag, 12f, true, true);
        }

        /// <summary>前臂樹枝（兩根往外岔的細枝）；vein = true 時改做貼在前臂表面、螺旋繞下的紫色脈絡。</summary>
        static void Forearm(PMesh m, float s, bool vein)
        {
            const int N = 6;
            var path = Gnarl(new Vector3(0f, 0.02f, 0f), new Vector3(0f, -0.9f, 0.05f), new Vector3(-0.04f * s, 0f, 0.04f), N, 31, 0.015f);
            var rad = Taper(N, 0.16f, 0.11f);
            if (!vein)
            {
                Tube(m, path, rad, 7, BarkJag, 15f, true, true);
                Twig(m, path[2], new Vector3(0.7f * s, -0.4f, -0.2f), 0.28f, 0.05f, new Vector3(0f, 0.03f, 0f), 32);
                Twig(m, path[4], new Vector3(0.3f * s, -0.3f, 0.6f), 0.22f, 0.04f, new Vector3(0f, 0.02f, 0f), 33);
                return;
            }
            var vp = new Vector3[N];
            var vr = new float[N];
            for (int i = 0; i < N; i++)
            {
                float t = (float)i / (N - 1);
                float a = Mathf.Lerp(-10f, 80f, t) * s * Mathf.Deg2Rad;   // 從前面螺旋繞到外側
                vp[i] = path[i] + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (rad[i] * 1.05f + 0.006f);
                vr[i] = i == N - 1 ? 0f : Mathf.Lerp(0.024f, 0.012f, t);
            }
            Tube(m, vp, vr, 4, null, 45f);
        }

        /// <summary>爪狀枝指：四根往下再往前勾的長指＋一根往內前方的拇指（手腕本地座標）。</summary>
        static void Claws(PMesh m, float s)
        {
            var lenK = new[] { 0.95f, 1.08f, 1.05f, 0.92f };
            for (int i = 0; i < 4; i++)
            {
                float bx = (-0.09f + 0.06f * i) * s;
                float k = lenK[i];
                var path = new[]
                {
                    new Vector3(bx, -0.1f, 0.03f), new Vector3(bx * 1.5f, -0.1f - 0.18f * k, 0.08f),
                    new Vector3(bx * 1.8f, -0.1f - 0.34f * k, 0.08f + 0.1f * k), new Vector3(bx * 1.9f, -0.1f - 0.43f * k, 0.08f + 0.24f * k),
                };
                Tube(m, path, new[] { 0.045f, 0.038f, 0.025f, 0f }, 5, TwigJag, 0f, true);
            }
            var th = new[] { new Vector3(-0.11f * s, -0.06f, 0.08f), new Vector3(-0.2f * s, -0.16f, 0.17f), new Vector3(-0.24f * s, -0.26f, 0.28f), new Vector3(-0.24f * s, -0.32f, 0.38f) };
            Tube(m, th, new[] { 0.045f, 0.036f, 0.022f, 0f }, 5, TwigJag, 0f, true);
        }

        /// <summary>層菇：三片疊在一起、往外突出的半圓菇傘（本地 +Z＝離開樹幹的方向）。</summary>
        static void FungusMesh(PMesh m)
        {
            var caps = new[] { new Vector3(0f, 0f, 0.11f), new Vector3(-0.06f, -0.12f, 0.085f), new Vector3(0.05f, 0.1f, 0.07f) };
            var up = Quaternion.Euler(-90f, 0f, 0f);   // 剖面的 +Z 轉成 +Y（菇傘朝上）
            for (int i = 0; i < caps.Length; i++)
            {
                float R = caps[i].z;
                var prof = new[] { -0.02f, 0f, -0.02f, R * 0.9f, 0f, R, 0.025f, R * 0.7f, 0.04f, 0f };
                m.Lathe(prof, 9, null, 1f, 1f, 0f, null, new Vector3(caps[i].x, caps[i].y, R * 0.6f), up);
            }
        }

        /// <summary>藤蔓：兩條往下垂、微微彎曲的細藤＋幾片小葉子（樞紐在頂端，往 -Y 垂）。</summary>
        static void VineMesh(PMesh m, float len, int seed)
        {
            var r = new System.Random(seed);
            for (int k = 0; k < 2; k++)
            {
                float L = k == 0 ? len : len * 0.7f;
                var top = k == 0 ? Vector3.zero : new Vector3(0.05f, 0f, -0.03f);
                const int N = 7;
                var path = new Vector3[N];
                var rad = new float[N];
                for (int i = 0; i < N; i++)
                {
                    float t = (float)i / (N - 1);
                    path[i] = top + new Vector3(0.035f * Mathf.Sin(t * 7f + seed + k * 2f), -L * t, 0.025f * Mathf.Sin(t * 5f + seed * 0.7f));
                    rad[i] = i == N - 1 ? 0f : Mathf.Lerp(0.022f, 0.009f, t);
                }
                Tube(m, path, rad, 4, null, 45f, true);
                // 小葉子：扁平的葉片（剖面往 X 壓扁）
                for (int i = 1; i < N - 1; i += 2)
                {
                    float sgn = ((i / 2 + k) & 1) == 0 ? 1f : -1f;
                    var dir = new Vector3(0.7f * sgn, -0.45f, Rnd(r, -0.3f, 0.3f));
                    var rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    m.Lathe(new[] { 0f, 0f, 0.025f, 0.024f, 0.06f, 0.028f, 0.1f, 0.014f, 0.12f, 0f }, 5, null, 0.22f, 1f, 0f, null, path[i], rot);
                }
            }
        }

        /// <summary>樹葉團：凹凸不平、邊緣有葉狀起伏的團塊（約 1 單位大小，靠縮放決定尺寸）。</summary>
        static Mesh LeafMesh(int variant)
        {
            string key = "treant.leaf" + variant;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = new PMesh(1);
            const int rings = 6;
            var prof = new float[(rings + 1) * 2];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings;
                prof[i * 2] = -Mathf.Cos(a) * 0.42f;
                prof[i * 2 + 1] = Mathf.Sin(a) * 0.42f;
            }
            float seed = variant * 2.1f + 0.3f;
            m.Lathe(prof, 9, null, 1f, 1f, variant * 23f, LeafJag, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), p =>
            {
                float n = Mathf.Sin(p.x * 13f + seed) * Mathf.Sin(p.y * 11f + seed * 2.3f) * Mathf.Sin(p.z * 12f + seed * 0.7f);
                return p * (1f + 0.3f * n);
            });
            var mesh = m.Build("Boss " + key, true);
            meshCache[key] = mesh;
            return mesh;
        }

        // ================================================================ 深淵海妖（主題 2）
        // 顏色
        static readonly Color HideAC = new Color(0.12f, 0.58f, 0.62f);    // 飽和青色鱗皮
        static readonly Color HideBC = new Color(0.38f, 0.82f, 0.72f);    // 較淺的腹面、肩甲、貝殼
        static readonly Color HideCC = new Color(0.18f, 0.29f, 0.52f);    // 最暗：爪、鰭棘、眉骨
        static readonly Color RuneN = new Color(0.15f, 1f, 0.88f);        // 水藍色符文與螢光斑點
        static readonly Color RuneE = new Color(1f, 0.16f, 0.58f);        // 狂暴：洋紅
        static readonly Color RuneDim = new Color(0.02f, 0.2f, 0.24f);
        static readonly Color PearlHotN = new Color(0.8f, 1f, 1f);
        static readonly Color PearlHotE = new Color(1f, 0.72f, 0.88f);
        static readonly Color KEyeN = new Color(0.45f, 1f, 1f);
        static readonly Color KEyeE = new Color(1f, 0.3f, 0.42f);
        static readonly Color KDead = new Color(0.03f, 0.06f, 0.07f);
        static readonly Color KelpC = new Color(0.34f, 0.58f, 0.16f);       // 海帶
        static readonly Color BarnC = new Color(0.97f, 0.85f, 0.6f);       // 藤壺、吸盤
        static readonly Color CoralN = new Color(1f, 0.36f, 0.29f);    // 珊瑚
        static readonly Color CoralE = new Color(0.91f, 0.18f, 0.34f);     // 狂暴：深紅
        static readonly Color CoralBleach = new Color(0.82f, 0.8f, 0.74f); // 死後珊瑚白化
        static readonly Color MawC = new Color(0.18f, 0.16f, 0.34f);      // 嘴、眼窩的深洞
        static readonly Color KWitherC = new Color(0.4f, 0.48f, 0.52f);  // 死後鱗皮褪成灰
        static readonly Color SeaAqua = new Color(0.25f, 0.85f, 0.95f);   // 水花、衝擊波

        static readonly Palette KrakenPal = new Palette
        {
            a = HideAC, b = HideBC, c = HideCC, glowN = RuneN, glowE = RuneE, glowDim = RuneDim, hotN = PearlHotN, hotE = PearlHotE,
            eyeN = KEyeN, eyeE = KEyeE, dead = KDead, heatN = new Color(0.04f, 0.3f, 0.34f), heatE = new Color(0.4f, 0.04f, 0.24f),
            emberLo = 4f, emberHi = 12f,
        };

        // 海妖專用：珊瑚材質、頭套膜（鼓脹）、眼睛（放大）、嘴（泡泡／墨汁發射點）、觸手骨骼
        Material coralM;
        Transform mantle, mouthT;
        readonly List<Transform> eyeList = new List<Transform>();
        Transform[] eyes;
        Vector3[] eyeBase;
        Transform[] tentRoot, tentBones;   // tentBones[i * TentBones + k]
        readonly Vector2[] tentAng = new Vector2[TentBones];
        ParticleSystem inkPs, drips;
        float frameDt, writheP, bubbleT, bubbleAcc;
        bool slamPending;

        // 觸手：八條、每條 7 節骨頭（每節 0.28 m），根部從腰下的肉質底座伸出
        const int TentN = 8, TentBones = 7, AtkTent = 2;   // 攻擊觸手＝右側那條
        const float TentSeg = 0.28f, TentTip = 0.13f, TentRootY = 0.74f;
        static readonly float[] TentYawDeg = { 22f, -22f, 64f, -64f, 110f, -110f, 155f, -155f };
        static readonly float[] TentScaleK = { 1.16f, 1.16f, 1.1f, 1.1f, 1.04f, 1.04f, 0.96f, 0.96f };
        static readonly float[] TentJointR = { 0.2f, 0.185f, 0.165f, 0.14f, 0.11f, 0.08f, 0.055f, 0.035f };
        static readonly float[] CurlPitch = { 12f, 20f, 30f }, CurlYaw = { 6f, 12f, 20f };       // 尖端三節的捲曲量
        static readonly float[] RearPitch = { -55f, -10f, -10f, -8f, -10f, -18f, -30f };          // s_tentacle 在右側高高豎起、尖端往後捲
        static readonly float[] ScaleJag = { 1f, 0.93f, 1.05f, 0.95f, 1.02f, 0.92f, 1.06f, 0.96f };
        static readonly float[] KelpJag = { 1f, 0.2f };

        /// <summary>海妖待機：微微前傾、長臂半舉、爪子朝前（張牙舞爪）。</summary>
        static Pose KrakenIdle()
        {
            var p = new Pose { spX = 6f, head = -6f, crouch = 0.04f, elL = -40f, elR = -40f, wrL = -15f, wrR = -15f };
            p.shL = p.shR = new Vector3(-22f, 0f, 20f);
            return p;
        }

        static Pose KrakenWindup(string id)
        {
            var p = KrakenIdle();
            switch (id)
            {
                case "s_tentacle":   // 右側的觸手在身旁高高豎起、尖端往後捲；身體往右扭、右臂讓開、左爪往前比
                    p.tent = 1f; p.spX = -6f; p.spY = 18f; p.spZ = -5f; p.head = -10f; p.jaw = 0.3f; p.crouch = 0.02f;
                    p.shR = new Vector3(-10f, 0f, 55f); p.elR = -35f;
                    p.shL = new Vector3(-70f, 0f, 25f); p.elL = -40f;
                    p.glow = 0.4f;
                    break;
                case "s_wave":       // 雙臂往兩側高高揚起（掀起浪）
                    p.spX = -12f; p.head = -16f; p.jaw = 0.4f; p.crouch = 0.02f;
                    p.shL = p.shR = new Vector3(-150f, 0f, 50f); p.elL = p.elR = -20f; p.wrL = p.wrR = -10f;
                    p.glow = 0.5f;
                    break;
                case "s_bubbles":    // 後仰吸氣、頭套膜鼓脹
                    p.spX = -16f; p.head = -22f; p.jaw = 0.15f; p.crouch = 0.06f; p.swell = 1f;
                    p.shL = p.shR = new Vector3(-35f, 0f, 45f); p.elL = p.elR = -50f;
                    p.glow = 0.5f;
                    break;
                case "s_whirl":      // 雙臂舉過頭（Animate 另外讓雙臂畫圈），符文大亮
                    p.spX = -8f; p.head = -20f; p.jaw = 0.4f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-165f, 0f, 25f); p.elL = p.elR = -35f; p.wrL = p.wrR = -10f;
                    p.glow = 1f;
                    break;
                case "s_ink":        // 大幅後仰、頭套膜鼓起、張嘴
                    p.spX = -24f; p.head = -34f; p.jaw = 0.5f; p.crouch = 0.1f; p.swell = 0.6f;
                    p.shL = p.shR = new Vector3(-10f, 0f, 50f); p.elL = p.elR = -60f;
                    p.glow = 0.3f;
                    break;
                case "s_summon":     // 舉起雙臂、貝殼微開、眼睛放大
                    p.spX = -10f; p.head = -22f; p.jaw = 0.5f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-150f, 0f, 50f); p.elL = p.elR = -20f; p.wrL = p.wrR = -20f;
                    p.glow = 0.8f; p.open = 0.6f;
                    break;
                default:             // 未知招式：雙臂舉起
                    p.spX = -12f; p.head = -14f; p.jaw = 0.3f; p.crouch = 0.04f;
                    p.shL = p.shR = new Vector3(-150f, 0f, 25f); p.elL = p.elR = -45f; p.wrL = p.wrR = -15f;
                    p.glow = 0.4f;
                    break;
            }
            return p;
        }

        static Pose KrakenStrike(string id)
        {
            var p = KrakenIdle();
            switch (id)
            {
                case "s_tentacle":   // 觸手往前重重砸下、身體跟著前傾
                    p.smash = 1f; p.spX = 18f; p.spY = -10f; p.head = 6f; p.jaw = 0.7f; p.crouch = 0.14f;
                    p.shR = new Vector3(-50f, 0f, 30f); p.elR = -20f;
                    p.shL = new Vector3(-20f, 0f, 35f); p.elL = -40f;
                    p.glow = 0.4f;
                    break;
                case "s_wave":       // 雙臂往前推出、掌心朝前
                    p.spX = 18f; p.head = 4f; p.jaw = 0.8f; p.crouch = 0.14f;
                    p.shL = p.shR = new Vector3(-82f, 0f, 14f); p.elL = p.elR = -5f; p.wrL = p.wrR = 20f;
                    p.glow = 0.6f;
                    break;
                case "s_bubbles":    // 往前吐出一串泡泡、頭套膜消下去
                    p.spX = 16f; p.head = 6f; p.jaw = 1f; p.crouch = 0.08f; p.swell = -0.15f;
                    p.shL = p.shR = new Vector3(10f, 0f, 40f); p.elL = p.elR = -30f;
                    p.glow = 0.6f;
                    break;
                case "s_whirl":      // 雙臂往前下方一甩，把漩渦丟向目標
                    p.spX = 22f; p.head = 8f; p.jaw = 0.7f; p.crouch = 0.12f;
                    p.shL = p.shR = new Vector3(-60f, 0f, 20f); p.elL = p.elR = -10f;
                    p.glow = 1f;
                    break;
                case "s_ink":        // 往前上方吐出墨團（拋物線），頭仍然仰著
                    p.spX = 4f; p.head = -24f; p.jaw = 1f; p.crouch = 0.04f; p.swell = -0.1f;
                    p.shL = p.shR = new Vector3(-40f, 0f, 35f); p.elL = p.elR = -30f;
                    p.glow = 0.4f;
                    break;
                case "s_summon":     // 雙臂高舉張開、仰頭、貝殼完全打開露出珍珠
                    p.spX = -16f; p.head = -32f; p.jaw = 1f; p.crouch = 0.02f;
                    p.shL = p.shR = new Vector3(-170f, 0f, 60f); p.elL = p.elR = -5f; p.wrL = p.wrR = -30f;
                    p.glow = 1f; p.open = 1f;
                    break;
                default:             // 未知招式：雙臂往前下方砸
                    p.spX = 26f; p.head = 10f; p.jaw = 0.6f; p.crouch = 0.22f;
                    p.shL = p.shR = new Vector3(-60f, 0f, 15f); p.elL = p.elR = -12f; p.wrL = p.wrR = 0f;
                    p.glow = 0.5f;
                    break;
            }
            return p;
        }

        /// <summary>海妖死亡：整個癱倒前撲、雙臂往兩側攤在地上、頭套膜洩氣（觸手在 UpdateTentacles 失去力氣攤平）。</summary>
        static Pose KrakenDead()
        {
            var p = KrakenIdle();
            p.spX = 46f; p.head = 30f; p.jaw = 0.6f; p.crouch = 0.42f; p.swell = -0.35f;
            p.shL = p.shR = new Vector3(-25f, 0f, 70f); p.elL = p.elR = -12f; p.wrL = p.wrR = 0f;
            return p;
        }

        void BuildKraken()
        {
            InitSeaMats();
            // 每隻頭目自己的材質（閃色、脈動、狂暴變色、死亡褪色）
            rockA = new Material(litShader) { color = HideAC, name = "KrakenHide" };
            rockB = new Material(litShader) { color = HideBC, name = "KrakenBelly" };
            rockC = new Material(litShader) { color = HideCC, name = "KrakenDark" };
            leafM = new Material(litShader) { color = KelpC, name = "KrakenKelp" };
            mossM = new Material(litShader) { color = BarnC, name = "KrakenBarnacle" };
            coralM = new Material(litShader) { color = CoralN, name = "KrakenCoral" };
            lavaM = new Material(glowShader) { color = RuneN, name = "KrakenRune" };
            hotM = new Material(glowShader) { color = PearlHotN, name = "KrakenGlowHot" };
            coreM = new Material(glowShader) { color = RuneN, name = "KrakenPearl" };
            eyeM = new Material(glowShader) { color = KEyeN, name = "KrakenEye" };
            hollowM = new Material(glowShader) { color = MawC, name = "KrakenMaw" };

            body = Pivot("Body", transform, Vector3.zero);

            // ---- 腰下的肉質底座（觸手從這裡長出）＋藤壺＋八條觸手
            MeshPart(body, KrakenMesh("base", BaseMesh), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(body, KrakenMesh("baseBarn", BaseBarnacles), mossM, Vector3.zero, Vector3.zero, Vector3.one);
            BuildTentacles();

            // ---- 人形軀幹（樞紐在腰）：鱗皮、腹部橫紋、水藍符文與螢光斑點、背鰭棘
            spine = Pivot("Spine", body, new Vector3(0f, 1.15f, 0f));
            MeshPart(spine, KrakenMesh("torso", TorsoMesh), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(spine, KrakenMesh("belly", BellyBands), rockB, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(spine, KrakenMesh("runes", TorsoRunes), lavaM, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(spine, KrakenMesh("fin", DorsalFin), rockC, Vector3.zero, Vector3.zero, Vector3.one);

            // 胸口的深海珍珠（ChestLocal）＋兩片會打開的貝殼
            core = Pivot("Core", spine, new Vector3(0f, 0.5f, 0.44f));
            Part(PrimitiveType.Sphere, core, Vector3.zero, Vector3.one * 0.3f, coreM);
            Part(PrimitiveType.Sphere, core, new Vector3(-0.04f, 0.05f, 0.09f), Vector3.one * 0.12f, hotM);
            plateL = RockPart(spine, new Vector3(-0.2f, 0.5f, 0.5f), new Vector3(0.22f, 0.44f, 0.14f), rockB, Vector3.zero, 1).transform;
            plateR = RockPart(spine, new Vector3(0.2f, 0.5f, 0.5f), new Vector3(0.22f, 0.44f, 0.14f), rockB, Vector3.zero, 2).transform;
            plateRestL = plateL.localPosition; plateRestR = plateR.localPosition;

            // 腰間垂掛的海帶
            KelpVine(spine, new Vector3(-0.5f, -0.12f, 0.28f), 0.75f, 21);
            KelpVine(spine, new Vector3(0.44f, -0.08f, -0.3f), 0.7f, 22);

            // ---- 頭（脖子 → 頭 → 下巴）：頭套膜（可鼓脹）、珊瑚冠、斜吊的青色眼睛、嘴與觸鬚
            neck = Pivot("Neck", spine, new Vector3(0f, 0.92f, 0.22f));
            head = Pivot("Head", neck, new Vector3(0f, 0.1f, 0.12f));
            mantle = Pivot("Mantle", head, Vector3.zero);
            MeshPart(mantle, KrakenMesh("mantle", MantleMesh), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(mantle, KrakenMesh("mantleGlow", MantleGlow), lavaM, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(mantle, KrakenMesh("coral", m => CoralCrown(m, false)), coralM, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(mantle, KrakenMesh("coralTips", m => CoralCrown(m, true)), lavaM, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(mantle, KrakenMesh("mantleBarn", MantleBarnacles), mossM, Vector3.zero, Vector3.zero, Vector3.one);
            var browAt = MantlePoint(0.3f, 0f, 1f);
            RockPart(mantle, new Vector3(0f, browAt.y - 0.02f, browAt.z - 0.03f), new Vector3(0.6f, 0.1f, 0.18f), rockC, new Vector3(14f, 0f, 0f), 2);   // 眉骨
            for (int s = -1; s <= 1; s += 2)
            {
                Part(PrimitiveType.Sphere, mantle, MantlePoint(0.2f, 32f * s, 0.93f), new Vector3(0.17f, 0.12f, 0.12f), hollowM, new Vector3(0f, 32f * s, 0f));   // 眼窩
                var eye = Part(PrimitiveType.Sphere, mantle, MantlePoint(0.2f, 32f * s, 1.1f), new Vector3(0.12f, 0.07f, 0.05f), eyeM, new Vector3(0f, 32f * s, 16f * s));   // 斜吊的怒眼
                eyeList.Add(eye.transform);
                Sac(mantle, MantlePoint(0.42f, 118f * s, 1f), 0.09f);   // 兩側的發光囊
            }
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.1f, 0.26f), new Vector3(0.26f, 0.11f, 0.12f), hollowM);   // 嘴（出招時透出符文光）
            jaw = Pivot("Jaw", head, new Vector3(0f, -0.13f, 0.08f));
            RockPart(jaw, new Vector3(0f, -0.03f, 0.12f), new Vector3(0.32f, 0.1f, 0.26f), rockC, Vector3.zero, 2);
            MeshPart(jaw, KrakenMesh("beard", Beard), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            mouthT = Pivot("Mouth", head, new Vector3(0f, -0.1f, 0.34f));

            // ---- 手臂（肩 → 肘 → 手腕）：長長的鱗皮手臂、前臂鰭棘、長爪
            shL = BuildKrakenArm(-1f, out elL, out wrL);
            shR = BuildKrakenArm(1f, out elR, out wrR);
            KelpVine(shL, new Vector3(-0.16f, 0.02f, 0.14f), 0.55f, 23);
            KelpVine(elR, new Vector3(0.1f, -0.2f, -0.08f), 0.45f, 24);

            eyes = eyeList.ToArray();
            eyeBase = new Vector3[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) eyeBase[i] = eyes[i].localScale;

            // ---- 特效：浮游光點、珍珠光暈、觸手拍水的水花、嘴裡的泡泡流與墨汁、身上滴落的水、腳下的水光
            embers = SpellFx.NewPS(spine, "Plankton", SpellFx.AddMat(SpellFx.TexSoft), 60, true);
            embers.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 1f, 0.85f), new Color(0.6f, 0.9f, 1f));
                SpellFx.Sphere(embers, 1.2f);
                SpellFx.FadeInOut(embers);
                SpellFx.SizeCurve(embers, SpellFx.Shrink);
                SpellFx.Rise(embers, 0.25f);
                SpellFx.Noise(embers, 0.35f, 0.9f);
                SpellFx.Rate(embers, 4f);
                embers.Play();
            }
            halo = SpellFx.NewPS(core, "PearlHalo", SpellFx.AddMat(SpellFx.TexSoft), 8, false);
            {
                var m = halo.main;
                m.startLifetime = 0.7f;
                m.startSize = 1f;
                m.startColor = new Color(0.2f, 1f, 0.9f, 0.5f);
                SpellFx.FadeInOut(halo);
                SpellFx.Rate(halo, 6f);
                halo.Play();
            }
            stepDust = SpellFx.NewPS(transform, "Splash", SpellFx.BlendMat(SpellFx.TexSoft), 40, true);
            {
                var m = stepDust.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.95f, 1f, 0.6f), new Color(0.45f, 0.8f, 0.88f, 0.5f));
                m.gravityModifier = 0.3f;
                SpellFx.FadeOut(stepDust);
                SpellFx.SizeCurve(stepDust, SpellFx.Grow);
                SpellFx.Drag(stepDust, 2.5f);
                stepDust.Play();
            }
            spew = SpellFx.NewPS(mouthT, "BubbleStream", bubbleMat, 70, true);
            {
                var m = spew.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.17f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 1f, 1f, 0.9f), new Color(0.5f, 0.9f, 1f, 0.8f));
                m.gravityModifier = -0.06f;
                SpellFx.Cone(spew, 14f, 0.05f);
                SpellFx.FadeOut(spew);
                SpellFx.Drag(spew, 1.2f);
                SpellFx.Noise(spew, 0.35f, 2f);
                spew.Play();
            }
            inkPs = SpellFx.NewPS(mouthT, "InkSpit", SpellFx.BlendMat(SpellFx.TexSoft), 40, true);
            {
                var m = inkPs.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.04f, 0.16f, 0.9f), new Color(0.25f, 0.08f, 0.32f, 0.85f));
                m.gravityModifier = 0.9f;
                SpellFx.Cone(inkPs, 18f, 0.05f);
                SpellFx.FadeOut(inkPs);
                SpellFx.SizeCurve(inkPs, SpellFx.Grow);
                inkPs.Play();
            }
            drips = SpellFx.NewPS(spine, "Drips", SpellFx.BlendMat(SpellFx.TexSoft), 30, true);
            drips.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            {
                var m = drips.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 0.88f, 0.95f, 0.65f), new Color(0.4f, 0.75f, 0.85f, 0.55f));
                m.gravityModifier = 1f;
                SpellFx.Sphere(drips, 0.8f);
                SpellFx.FadeOut(drips);
                SpellFx.Stretch(drips, 0.05f, 1.2f);
                SpellFx.Rate(drips, 3f);
                drips.Play();
            }
            BuildHeatGlow();
        }

        Transform BuildKrakenArm(float s, out Transform elbow, out Transform wrist)
        {
            string side = s < 0f ? "L" : "R";
            var sh = Pivot("Shoulder" + side, spine, new Vector3(0.74f * s, 0.86f, -0.02f));
            RockPart(sh, new Vector3(0.06f * s, 0.07f, 0f), new Vector3(0.46f, 0.36f, 0.48f), rockB, new Vector3(0f, 0f, 18f * s), 1);   // 肩上的硬殼
            MeshPart(sh, KrakenMesh("pauldronBarn" + side, m => PauldronBarnacles(m, s)), mossM, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(sh, KrakenMesh("upper" + side, m => KUpperArm(m, s)), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            elbow = Pivot("Elbow" + side, sh, new Vector3(0f, -0.78f, 0f));
            MeshPart(elbow, KrakenMesh("fore" + side, m => KForearm(m, s)), rockA, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(elbow, KrakenMesh("foreFin" + side, m => KForeFin(m, s)), rockC, Vector3.zero, Vector3.zero, Vector3.one);
            MeshPart(elbow, KrakenMesh("foreGlow" + side, m => KForeGlow(m, s)), lavaM, Vector3.zero, Vector3.zero, Vector3.one);
            wrist = Pivot("Wrist" + side, elbow, new Vector3(0f, -0.78f, 0.03f));
            RockPart(wrist, new Vector3(0f, -0.07f, 0.03f), new Vector3(0.22f, 0.2f, 0.26f), rockA, Vector3.zero, 2);   // 手掌
            MeshPart(wrist, KrakenMesh("claw" + side, m => KClaws(m, s)), rockC, Vector3.zero, Vector3.zero, Vector3.one);
            return sh;
        }

        /// <summary>垂掛的海帶：跟藤蔓一樣永遠往下垂、輕輕搖晃（見 UpdateTreantBits）。</summary>
        void KelpVine(Transform parent, Vector3 pos, float len, int seed)
        {
            var piv = Pivot("Kelp", parent, pos);
            MeshPart(piv, KrakenMesh("kelp" + seed, m => KelpMesh(m, len, seed)), leafM, Vector3.zero, Vector3.zero, Vector3.one);
            vineList.Add(piv);
            vineLenList.Add(len);
        }

        /// <summary>八條觸手：每條一個 SkinnedMeshRenderer（共用同一個蒙皮網格）＋ 7 節骨頭。</summary>
        void BuildTentacles()
        {
            var mesh = TentacleMesh();
            var mats = new[] { rockA, rockB, mossM, lavaM };
            tentRoot = new Transform[TentN];
            tentBones = new Transform[TentN * TentBones];
            for (int i = 0; i < TentN; i++)
            {
                float a = TentYawDeg[i] * Mathf.Deg2Rad;
                var r = Pivot("Tentacle" + i, body, new Vector3(Mathf.Sin(a) * 0.4f, TentRootY, Mathf.Cos(a) * 0.38f));
                r.localRotation = Quaternion.Euler(0f, TentYawDeg[i], 0f);
                r.localScale = Vector3.one * TentScaleK[i];
                tentRoot[i] = r;
                var bones = new Transform[TentBones];
                Transform parent = r;
                for (int k = 0; k < TentBones; k++)
                {
                    bones[k] = Pivot("T" + i + "_" + k, parent, k == 0 ? Vector3.zero : new Vector3(0f, 0f, TentSeg));
                    parent = bones[k];
                    tentBones[i * TentBones + k] = bones[k];
                }
                var go = new GameObject("TentacleSkin");
                go.transform.SetParent(r, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = bones[0];
                smr.sharedMaterials = mats;
                smr.quality = SkinQuality.Bone2;
                smr.updateWhenOffscreen = false;
                smr.localBounds = new Bounds(new Vector3(0f, 0f, 1f), new Vector3(4.6f, 4.6f, 4.6f));
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
            }
            // 「腳」＝前方兩條觸手中段（爬行時在這裡濺起水花）
            anR = tentBones[0 * TentBones + 4];
            anL = tentBones[1 * TentBones + 4];
        }

        // ---------------------------------------------------------------- 海妖：觸手動作
        static float DropSum(float a) => Mathf.Sin(a) + Mathf.Sin(a * 0.9f) + Mathf.Sin(a * 0.5f);

        /// <summary>前三節往下彎多少度（第 0、1、2 節的絕對俯角＝a、0.9a、0.5a），才能讓觸手中段剛好貼地。x＝要下降的高度／每節長度。</summary>
        static float SolveDrop(float x)
        {
            if (x <= 0f) return 0f;
            float lo = 0f, hi = 88f * Mathf.Deg2Rad;
            if (x >= DropSum(hi)) return 88f;
            for (int it = 0; it < 12; it++)
            {
                float mid = (lo + hi) * 0.5f;
                if (DropSum(mid) < x) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 每條觸手：根部依身體高度往下彎到貼地（下蹲、癱倒時自動攤平），中段貼地、尖端三節不停捲起並左右扭動（行進波）；
        /// 爬行時單雙數兩組交替往前伸、抬起中段；攻擊觸手依 Pose.tent／smash 混合到翹起／砸下的姿勢。死亡時扭動與捲曲漸漸停止。
        /// </summary>
        void UpdateTentacles(in Pose p, float sinP, float cosP, float wAmt)
        {
            writheP += frameDt * Mathf.Lerp(1.7f, 3.4f, enr);   // 狂暴時扭得更快（累加相位，速度改變時不會跳動）
            if (writheP > 1000f) writheP -= Mathf.PI * 200f;
            float life = dead ? 1f - Mathf.Clamp01(deadT / 1.2f) : 1f;
            var up = transform.up;
            var gp = transform.position;
            float rigScale = Mathf.Max(0.01f, transform.lossyScale.y);
            for (int i = 0; i < TentN; i++)
            {
                var root = tentRoot[i];
                bool isAtk = i == AtkTent;
                float rear = isAtk ? Mathf.Clamp01(p.tent) : 0f, smash = isAtk ? Mathf.Clamp01(p.smash) : 0f;
                float sc = TentScaleK[i] * (1f + 0.25f * Mathf.Max(rear, smash));
                root.localScale = Vector3.one * sc;
                float h = Vector3.Dot(root.position - gp, up) / rigScale;
                float a = SolveDrop((h - 0.12f * sc) / (TentSeg * sc));
                float ph = i * 2.39f;
                float side = (i & 1) == 0 ? 1f : -1f;
                float yawSign = TentYawDeg[i] > 0f ? 1f : -1f;
                float gS = (i & 1) == 0 ? sinP : -sinP, gC = (i & 1) == 0 ? cosP : -cosP;
                float lift = Mathf.Max(0f, gC) * wAmt;
                float curl = (0.55f + 0.45f * Mathf.Sin(writheP * 0.6f + ph * 1.7f)) * life;
                float prev = 0f;
                for (int k = 0; k < TentBones; k++)
                {
                    float abs = k == 0 ? a : k == 1 ? a * 0.9f : k == 2 ? a * 0.5f : 0f;
                    float fit = abs - prev;
                    prev = abs;
                    float pitch = fit;
                    float yaw = k >= 1 ? (4f + 4.5f * k) * life * Mathf.Sin(writheP + ph - 0.9f * k) : 0f;
                    if (k >= 4) { pitch -= curl * CurlPitch[k - 4]; yaw += curl * side * CurlYaw[k - 4]; }
                    if (k == 0) yaw -= yawSign * 14f * gS * wAmt;   // 爬行：往前伸、往後划
                    else if (k == 1) pitch -= 18f * lift;           // 往前伸時抬起中段
                    else if (k == 2) pitch += 18f * lift;
                    var ang = new Vector2(pitch, yaw);
                    if (rear > 0.001f || smash > 0.001f)
                    {
                        var r = new Vector2(RearPitch[k] + (k > 0 ? 3f * Mathf.Sin(clock * 9f - k) : 0f),
                                            k == 0 ? 16f * yawSign : 4f * Mathf.Sin(clock * 7f - k));
                        var sm = new Vector2(fit + (k == TentBones - 1 ? -10f : 0f), k == 0 ? -TentYawDeg[i] : 0f);
                        ang = Vector2.Lerp(Vector2.Lerp(ang, r, rear), sm, smash);
                    }
                    tentAng[k] = ang;
                }
                for (int k = 0; k < TentBones; k++)
                    tentBones[i * TentBones + k].localRotation = Quaternion.Euler(tentAng[k].x, tentAng[k].y, 0f);
            }
        }

        /// <summary>出手時的小特效。</summary>
        void KrakenAttackFx()
        {
            switch (atk)
            {
                case "s_tentacle": slamPending = true; break;                                   // 觸手落地時（UpdateKrakenBits）濺起水花
                case "s_wave": StepDust(GroundUnder(wrL)); StepDust(GroundUnder(wrR)); if (drips != null && visible) drips.Emit(16); break;
                case "s_bubbles": bubbleT = 0.5f; bubbleAcc = 0f; break;                         // 一串泡泡（持續 0.5 秒）
                case "s_whirl": if (embers != null && visible) embers.Emit(30); break;
                case "s_ink": if (inkPs != null && visible) inkPs.Emit(26); break;
                case "s_summon": if (embers != null && visible) embers.Emit(40); break;
            }
        }

        /// <summary>泡泡流、攻擊觸手落地的水花。</summary>
        void UpdateKrakenBits(float dt)
        {
            if (bubbleT > 0f)
            {
                bubbleT -= dt;
                bubbleAcc += dt * 60f;
                int n = (int)bubbleAcc;
                if (n > 0)
                {
                    bubbleAcc -= n;
                    if (spew != null && visible) spew.Emit(n);
                }
            }
            if (slamPending && (phase != PhaseStrike || atkT >= StrikeIn))
            {
                slamPending = false;
                if (phase == PhaseStrike && visible)
                {
                    var gpos = GroundUnder(tentBones[AtkTent * TentBones + 5]);
                    StepDust(gpos); StepDust(gpos);
                    WaterSplash(gpos, 0.7f);
                }
            }
        }

        /// <summary>海妖癱倒到地上時：一圈水花衝擊波。</summary>
        void UpdateKrakenDeath()
        {
            if (fallen || deadT < 0.85f) return;
            fallen = true;
            if (!visible) return;
            var c = transform.position + transform.forward * 0.7f;
            SlamShockwave(c, 2.2f, SeaAqua);
            WaterSplash(c, 1.4f);
            if (drips != null) drips.Emit(20);
        }

        // ---------------------------------------------------------------- 海妖的網格
        /// <summary>有光照的程式網格（平面著色），依 key 快取共用。</summary>
        static Mesh KrakenMesh(string key, System.Action<PMesh> build)
        {
            key = "kraken." + key;
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var pm = new PMesh(1);
            build(pm);
            var mesh = pm.Build("Boss " + key, true);
            meshCache[key] = mesh;
            return mesh;
        }

        /// <summary>
        /// 參數曲面 f(u, deg)：u 由 u0 到 u1（由下往上），deg 0..360 繞一圈（0＝+Z、90＝+X），面朝外；
        /// 兩端各用一個中心點封口（半徑為 0 的那端三角形退化、不影響）。
        /// </summary>
        static void Surface(PMesh m, System.Func<float, float, Vector3> f, int nu, int nv, float u0, float u1, Vector3 capLo, Vector3 capHi)
        {
            var white = Color.white;
            for (int i = 0; i < nu; i++)
            {
                float ua = Mathf.Lerp(u0, u1, (float)i / nu), ub = Mathf.Lerp(u0, u1, (float)(i + 1) / nu);
                for (int j = 0; j < nv; j++)
                {
                    float da = 360f * j / nv, db = 360f * (j + 1) / nv;
                    var p00 = f(ua, da); var p01 = f(ua, db); var p10 = f(ub, da); var p11 = f(ub, db);
                    m.Tri(p00, p11, p10, white);
                    m.Tri(p00, p01, p11, white);
                }
            }
            for (int j = 0; j < nv; j++)
            {
                float da = 360f * j / nv, db = 360f * (j + 1) / nv;
                m.Tri(capLo, f(u0, db), f(u0, da), white);
                m.Tri(f(u1, da), f(u1, db), capHi, white);
            }
        }

        /// <summary>藤壺：小火山狀的硬殼（中間凹下去的開口），pos 為底部中心、n 為朝外方向。</summary>
        static void Barnacle(PMesh m, Vector3 pos, Vector3 n, float size, float phase)
        {
            var prof = new[] { -0.2f, 1f, 0.35f, 0.82f, 0.62f, 0.52f, 0.5f, 0.34f, 0.25f, 0f };
            for (int i = 0; i < prof.Length; i++) prof[i] *= size;
            m.Lathe(prof, 7, null, 1f, 1f, phase, null, pos, Quaternion.LookRotation(n.normalized));
        }

        /// <summary>貼在表面上的小圓點（螢光斑點、吸盤以外的小突起）。</summary>
        static void Dot(PMesh m, Vector3 pos, Vector3 n, float r)
        {
            m.Lathe(new[] { -0.3f * r, 0f, -0.1f * r, r, 0.25f * r, r * 0.75f, 0.45f * r, 0f }, 6, null, 1f, 1f, 0f, null, pos, Quaternion.LookRotation(n.normalized));
        }

        /// <summary>沿著一串點的等粗細管（符文線條），兩端封口。</summary>
        static void Line3(PMesh m, Vector3[] pts, float r)
        {
            var rad = new float[pts.Length];
            for (int i = 0; i < rad.Length; i++) rad[i] = r;
            Tube(m, pts, rad, 4, null, 45f, true, true);
        }

        // 腰下的肉質底座（身體座標）：剖面 (高度, 半徑)
        static readonly float[] BaseProf = { 0.42f, 0f, 0.45f, 0.3f, 0.56f, 0.5f, 0.75f, 0.6f, 1.0f, 0.58f, 1.25f, 0.5f, 1.48f, 0.4f, 1.6f, 0f };

        static void BaseMesh(PMesh m)
        {
            m.Lathe(BaseProf, 12, null, 1.05f, 0.95f, 0f, null, Vector3.zero, Quaternion.Euler(-90f, 0f, 0f),
                p => p * (1f + 0.05f * Mathf.Sin(p.x * 11f + p.z * 3f) * Mathf.Sin(p.y * 9f + 1.3f)));
        }

        static void BaseBarnacles(PMesh m)
        {
            var angs = new[] { 35f, 80f, 140f, 200f, 250f, 300f, 330f };
            var ys = new[] { 1.15f, 1.0f, 1.2f, 1.05f, 1.25f, 1.1f, 0.98f };
            for (int i = 0; i < angs.Length; i++)
            {
                float a = angs[i] * Mathf.Deg2Rad;
                float r = ys[i] > 1.12f ? 0.53f : 0.57f;
                var pos = new Vector3(Mathf.Sin(a) * r * 1.05f, ys[i], Mathf.Cos(a) * r * 0.95f);
                Barnacle(m, pos, new Vector3(Mathf.Sin(a), 0.35f, Mathf.Cos(a)), 0.05f + 0.012f * (i % 3), i * 40f);
            }
        }

        // 軀幹剖面（脊椎本地座標：高度、中心前後、半徑），再壓成橢圓（寬 KTorsoSX、深 KTorsoSZ）
        static readonly float[] KTorsoY = { -0.38f, -0.05f, 0.3f, 0.62f, 0.86f, 1.02f, 1.12f };
        static readonly float[] KTorsoZ = { 0f, 0.03f, 0.05f, 0.05f, 0.02f, 0f, 0.02f };
        static readonly float[] KTorsoR = { 0.46f, 0.4f, 0.46f, 0.56f, 0.56f, 0.42f, 0.22f };
        const float KTorsoSX = 1.22f, KTorsoSZ = 0.78f;

        /// <summary>軀幹表面（脊椎本地座標）：高度 y、繞軸角度 deg（0＝正前方、正＝往 +X），outK 往外推的倍率。</summary>
        static Vector3 TorsoPoint(float y, float deg, float outK)
        {
            int i = 0;
            while (i < KTorsoY.Length - 2 && y > KTorsoY[i + 1]) i++;
            float t = Mathf.Clamp01((y - KTorsoY[i]) / (KTorsoY[i + 1] - KTorsoY[i]));
            float r = Mathf.Lerp(KTorsoR[i], KTorsoR[i + 1], t) * outK;
            float zc = Mathf.Lerp(KTorsoZ[i], KTorsoZ[i + 1], t);
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * r * KTorsoSX, y, zc + Mathf.Cos(a) * r * KTorsoSZ);
        }

        static Vector3 TorsoNormal(float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) / KTorsoSX, 0f, Mathf.Cos(a) / KTorsoSZ).normalized;
        }

        static void TorsoMesh(PMesh m)
        {
            float y0 = KTorsoY[0], y1 = KTorsoY[KTorsoY.Length - 1];
            Surface(m, (y, deg) =>
            {
                float a = deg * Mathf.Deg2Rad;
                // 細細的鱗片起伏＋胸肌隆起
                float k = 1f + 0.025f * Mathf.Sin(a * 10f + y * 16f) * Mathf.Sin(a * 4f - y * 11f);
                float pec = Mathf.Exp(-Mathf.Pow((y - 0.74f) / 0.12f, 2f)) * Mathf.Max(0f, Mathf.Cos((Mathf.Abs(deg > 180f ? deg - 360f : deg) - 32f) * Mathf.Deg2Rad * 3f));
                return TorsoPoint(y, deg, k + 0.06f * pec);
            }, 14, 12, y0, y1, new Vector3(0f, y0, KTorsoZ[0]), new Vector3(0f, y1, KTorsoZ[KTorsoZ.Length - 1]));
        }

        /// <summary>腹部的橫紋（淺色鱗甲帶）。</summary>
        static void BellyBands(PMesh m)
        {
            var ys = new[] { -0.2f, -0.06f, 0.08f, 0.22f };
            for (int b = 0; b < ys.Length; b++)
            {
                const int N = 9;
                var path = new Vector3[N];
                float span = 40f - 3f * b;
                for (int i = 0; i < N; i++) path[i] = TorsoPoint(ys[b], Mathf.Lerp(-span, span, (float)i / (N - 1)), 1.0f);
                var rad = new float[N];
                for (int i = 0; i < N; i++) { float t = (float)i / (N - 1); rad[i] = 0.035f * (0.5f + 0.5f * Mathf.Sin(Mathf.PI * t)); }
                Tube(m, path, rad, 5, null, 0f, true, true);
            }
        }

        /// <summary>軀幹上的水藍符文（珍珠外圈、胸口的波紋）與兩側／背上成排的螢光斑點。</summary>
        static void TorsoRunes(PMesh m)
        {
            // 珍珠外圈
            const int R = 18;
            var ring = new Vector3[R + 1];
            for (int i = 0; i <= R; i++)
            {
                float f = 2f * Mathf.PI * i / R;
                ring[i] = TorsoPoint(0.5f + 0.21f * Mathf.Sin(f), 30f * Mathf.Cos(f), 1.05f);
            }
            Line3(m, ring, 0.017f);
            // 胸口兩側的波紋
            for (int s = -1; s <= 1; s += 2)
            {
                const int N = 9;
                var w = new Vector3[N];
                for (int i = 0; i < N; i++)
                {
                    float t = (float)i / (N - 1);
                    w[i] = TorsoPoint(0.8f + 0.045f * Mathf.Sin(t * Mathf.PI * 3f), s * Mathf.Lerp(36f, 74f, t), 1.08f);
                }
                Line3(m, w, 0.016f);
                // 從珍珠往下斜的鉤紋
                Line3(m, new[] { TorsoPoint(0.36f, 18f * s, 1.05f), TorsoPoint(0.28f, 30f * s, 1.05f), TorsoPoint(0.3f, 44f * s, 1.05f) }, 0.015f);
                // 兩側成排的螢光斑點
                for (int row = 0; row < 3; row++)
                {
                    float deg = s * (82f + 22f * row);
                    for (int i = 0; i < 6; i++)
                    {
                        float y = -0.12f + 0.17f * i + 0.07f * row;
                        if (y > 0.98f) continue;
                        Dot(m, TorsoPoint(y, deg, 1.0f), TorsoNormal(deg), 0.026f - 0.004f * row);
                    }
                }
            }
            // 背上兩排
            for (int i = 0; i < 6; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float deg = 180f + s * 24f;
                    Dot(m, TorsoPoint(0.02f + 0.16f * i, deg, 1.0f), TorsoNormal(deg), 0.024f);
                }
        }

        /// <summary>背鰭棘：沿背脊一排往後上方翹的尖棘，加上兩根肩胛棘。</summary>
        static void DorsalFin(PMesh m)
        {
            for (int i = 0; i < 7; i++)
            {
                float y = 0.05f + 0.15f * i;
                float len = 0.16f + 0.14f * Mathf.Sin(Mathf.PI * (i + 0.5f) / 7f);
                Twig(m, TorsoPoint(y, 180f, 0.95f), new Vector3(0f, 0.55f, -1f), len, 0.042f, new Vector3(0f, 0.02f, 0f), 140 + i);
            }
            for (int s = -1; s <= 1; s += 2)
                Twig(m, TorsoPoint(0.84f, 180f - 30f * s, 0.95f), new Vector3(-0.4f * s, 0.6f, -0.8f), 0.24f, 0.04f, new Vector3(0f, 0.02f, 0f), 150 + s);
        }

        // 頭套膜：沿中心線（往上、往後彎）的橢圓剖面，t 0..1 由下巴往頭頂
        static Vector3 MantleC(float t) => new Vector3(0f, -0.14f + 1.0f * t, 0.04f - 0.5f * t * t);
        static float MantleR(float t) => 0.47f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * (0.08f + 0.92f * t))), 0.6f) * (1f - 0.12f * t);

        /// <summary>頭套膜表面（頭的本地座標）：t 由下往上、deg（0＝正前方）、outK 往外推的倍率。</summary>
        static Vector3 MantlePoint(float t, float deg, float outK)
        {
            var c = MantleC(t);
            float r = MantleR(t) * outK;
            float a = deg * Mathf.Deg2Rad;
            return c + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r * 1.06f);
        }

        static Vector3 MantleNormal(float t, float deg) => (MantlePoint(t, deg, 1.1f) - MantlePoint(t, deg, 1f)).normalized;

        static void MantleMesh(PMesh m)
        {
            Surface(m, (t, deg) =>
            {
                float a = deg * Mathf.Deg2Rad;
                float k = 1f + 0.045f * Mathf.Sin(a * 7f + t * 9f) * Mathf.Sin(t * 14f + a * 2f);   // 肉瘤般的起伏
                return MantlePoint(t, deg, k);
            }, 12, 14, 0f, 1f, MantleC(0f), MantleC(1f));
        }

        /// <summary>頭套膜上的發光：額頭的三叉戟符文＋兩側與背後成排的螢光斑點。</summary>
        static void MantleGlow(PMesh m)
        {
            const float K = 1.06f;
            // 三叉戟：中柄、橫桿、三根尖
            Line3(m, new[] { MantlePoint(0.33f, 0f, K), MantlePoint(0.45f, 0f, K), MantlePoint(0.58f, 0f, K), MantlePoint(0.66f, 0f, K) }, 0.018f);
            Line3(m, new[] { MantlePoint(0.5f, -13f, K), MantlePoint(0.49f, 0f, K), MantlePoint(0.5f, 13f, K) }, 0.016f);
            for (int s = -1; s <= 1; s += 2)
                Line3(m, new[] { MantlePoint(0.5f, 13f * s, K), MantlePoint(0.56f, 14f * s, K), MantlePoint(0.63f, 11f * s, K) }, 0.015f);
            // 斑點
            var degs = new[] { 70f, 95f, 140f };
            for (int s = -1; s <= 1; s += 2)
                for (int row = 0; row < degs.Length; row++)
                    for (int i = 0; i < 5; i++)
                    {
                        float t = 0.26f + 0.12f * i + 0.05f * row;
                        if (t > 0.86f) continue;
                        float deg = s * degs[row];
                        Dot(m, MantlePoint(t, deg, 1.0f), MantleNormal(t, deg), 0.03f - 0.005f * row - 0.002f * i);
                    }
        }

        static void MantleBarnacles(PMesh m)
        {
            var ts = new[] { 0.35f, 0.42f, 0.5f, 0.38f, 0.56f, 0.46f };
            var ds = new[] { 150f, 165f, 145f, 130f, 160f, -165f };
            for (int i = 0; i < ts.Length; i++)
            {
                var n = MantleNormal(ts[i], ds[i]) + Vector3.up * 0.2f;
                Barnacle(m, MantlePoint(ts[i], ds[i], 0.98f), n, 0.04f + 0.01f * (i % 3), i * 50f);
            }
        }

        /// <summary>珊瑚冠：從頭套膜上半部往上、往外長的分岔珊瑚；tips = true 時改做每根尖端的發光小球。</summary>
        static void CoralCrown(PMesh m, bool tips)
        {
            // (t, deg, 長度)
            var defs = new[]
            {
                new Vector3(0.62f, 0f, 0.3f), new Vector3(0.7f, 48f, 0.36f), new Vector3(0.7f, -48f, 0.34f), new Vector3(0.78f, 105f, 0.32f),
                new Vector3(0.78f, -105f, 0.3f), new Vector3(0.86f, 160f, 0.28f), new Vector3(0.9f, -20f, 0.26f), new Vector3(0.82f, -160f, 0.24f),
            };
            for (int i = 0; i < defs.Length; i++)
            {
                float t = defs[i].x, deg = defs[i].y, len = defs[i].z;
                var start = MantlePoint(t, deg, 0.95f);
                float a = deg * Mathf.Deg2Rad;
                var outDir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var dir = (outDir * 0.6f + Vector3.up).normalized;
                var side = Vector3.Cross(dir, Vector3.up).normalized;
                var end = start + dir * len;
                var path = Gnarl(start, end, side * 0.04f, 5, 120 + i, 0.015f);
                var f1d = (dir + side * 0.8f + Vector3.up * 0.2f).normalized;
                var f2d = (dir - side * 0.9f + Vector3.up * 0.1f).normalized;
                float f1l = len * 0.45f, f2l = len * 0.35f;
                if (!tips)
                {
                    Tube(m, path, new[] { 0.05f, 0.042f, 0.032f, 0.02f, 0f }, 5, TwigJag, 0f, true);
                    Twig(m, path[2], f1d, f1l, 0.026f, Vector3.up * 0.015f, 130 + i, 4);
                    Twig(m, path[1], f2d, f2l, 0.024f, Vector3.up * 0.015f, 160 + i, 4);
                }
                else
                {
                    GlowBulb(m, end, 0.03f);
                    GlowBulb(m, path[2] + f1d * f1l, 0.022f);
                    GlowBulb(m, path[1] + f2d * f2l, 0.02f);
                }
            }
        }

        static void GlowBulb(PMesh m, Vector3 pos, float r)
        {
            m.Lathe(new[] { -r, 0f, -r * 0.6f, r * 0.8f, 0f, r, r * 0.6f, r * 0.8f, r, 0f }, 6, null, 1f, 1f, 0f, null, pos, Quaternion.identity);
        }

        /// <summary>下巴的觸鬚：六條往下垂、尖端往前捲的小觸手（下巴本地座標）。</summary>
        static void Beard(PMesh m)
        {
            for (int i = 0; i < 6; i++)
            {
                float u = -1f + 2f * i / 5f;
                float x = u * 0.15f, z0 = 0.12f + 0.06f * (1f - u * u);
                float L = 0.26f + 0.1f * (1f - Mathf.Abs(u));
                float sg = u < 0f ? -1f : 1f;
                var path = new[]
                {
                    new Vector3(x, -0.06f, z0), new Vector3(x * 1.1f, -0.06f - L * 0.35f, z0 + 0.03f),
                    new Vector3(x * 1.25f, -0.06f - L * 0.7f, z0 + 0.05f), new Vector3(x * 1.3f + 0.02f * sg, -0.06f - L * 0.92f, z0 + 0.11f),
                    new Vector3(x * 1.25f, -0.06f - L * 0.95f, z0 + 0.19f),
                };
                Tube(m, path, new[] { 0.035f, 0.03f, 0.022f, 0.012f, 0f }, 5, null, 0f, true);
            }
        }

        static void PauldronBarnacles(PMesh m, float s)
        {
            var xs = new[] { -0.1f, 0.05f, 0.14f, -0.02f, 0.1f };
            var ys = new[] { 0.22f, 0.25f, 0.2f, 0.24f, 0.18f };
            var zs = new[] { 0.05f, -0.08f, 0.1f, 0.14f, -0.12f };
            for (int i = 0; i < xs.Length; i++)
            {
                var pos = new Vector3((0.06f + xs[i]) * s, ys[i], zs[i]);
                Barnacle(m, pos, new Vector3(xs[i] * s * 3f + 0.2f * s, 1f, zs[i] * 3f), 0.045f + 0.01f * (i % 3), i * 33f);
            }
        }

        static void KUpperArm(PMesh m, float s)
        {
            var path = Gnarl(new Vector3(0f, 0.04f, 0f), new Vector3(0.02f * s, -0.78f, 0f), new Vector3(0.03f * s, 0f, -0.02f), 5, 70, 0.01f);
            Tube(m, path, Taper(5, 0.165f, 0.125f), 8, ScaleJag, 10f, true, true);
        }

        static void KForearm(PMesh m, float s)
        {
            var path = Gnarl(new Vector3(0f, 0.03f, 0f), new Vector3(0f, -0.78f, 0.03f), new Vector3(-0.02f * s, 0f, 0.03f), 6, 71, 0.01f);
            Tube(m, path, Taper(6, 0.14f, 0.1f), 8, ScaleJag, -10f, true, true);
        }

        /// <summary>前臂外後側的一排鰭棘＋手肘的骨刺。</summary>
        static void KForeFin(PMesh m, float s)
        {
            var ys = new[] { -0.12f, -0.28f, -0.44f, -0.58f };
            var ls = new[] { 0.22f, 0.2f, 0.16f, 0.12f };
            for (int i = 0; i < ys.Length; i++)
                Twig(m, new Vector3(0.06f * s, ys[i], -0.07f), new Vector3(0.45f * s, 0.3f, -0.85f), ls[i], 0.028f, new Vector3(0f, 0.015f, 0f), 170 + i, 4);
            Twig(m, new Vector3(0f, 0.02f, -0.08f), new Vector3(0f, 0.2f, -1f), 0.2f, 0.05f, new Vector3(0f, 0.02f, 0f), 175);
        }

        /// <summary>前臂的發光：手腕上方一圈符文環＋外側三個斑點。</summary>
        static void KForeGlow(PMesh m, float s)
        {
            const int N = 14;
            var ring = new Vector3[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float f = 2f * Mathf.PI * i / N;
                ring[i] = new Vector3(Mathf.Cos(f) * 0.118f, -0.6f + 0.02f * Mathf.Sin(f * 2f), 0.025f + Mathf.Sin(f) * 0.118f);
            }
            Line3(m, ring, 0.014f);
            for (int i = 0; i < 3; i++)
            {
                float y = -0.16f - 0.15f * i;
                var n = new Vector3(s, 0f, 0.35f);
                Dot(m, new Vector3(0.13f * s - 0.008f * i * s, y, 0.04f + 0.004f * i), n, 0.022f);
            }
        }

        /// <summary>長爪：四根往下再往前勾的長指＋一根往內前方的拇指（手腕本地座標）。</summary>
        static void KClaws(PMesh m, float s)
        {
            var lenK = new[] { 0.95f, 1.08f, 1.04f, 0.9f };
            for (int i = 0; i < 4; i++)
            {
                float bx = (-0.075f + 0.05f * i) * s;
                float k = lenK[i];
                var path = new[]
                {
                    new Vector3(bx, -0.13f, 0.06f), new Vector3(bx * 1.4f, -0.13f - 0.18f * k, 0.1f),
                    new Vector3(bx * 1.6f, -0.13f - 0.33f * k, 0.18f), new Vector3(bx * 1.7f, -0.13f - 0.41f * k, 0.32f),
                };
                Tube(m, path, new[] { 0.036f, 0.03f, 0.018f, 0f }, 5, TwigJag, 0f, true);
            }
            var th = new[] { new Vector3(-0.1f * s, -0.08f, 0.1f), new Vector3(-0.18f * s, -0.18f, 0.2f), new Vector3(-0.2f * s, -0.28f, 0.32f), new Vector3(-0.19f * s, -0.3f, 0.42f) };
            Tube(m, th, new[] { 0.036f, 0.028f, 0.016f, 0f }, 5, TwigJag, 0f, true);
        }

        /// <summary>海帶：三條往下垂、扭轉的扁平葉片（樞紐在頂端，往 -Y 垂）。</summary>
        static void KelpMesh(PMesh m, float len, int seed)
        {
            for (int k = 0; k < 3; k++)
            {
                float L = len * (1f - 0.18f * k);
                var top = new Vector3(0.04f * (k - 1), 0f, 0.02f * (k % 2));
                const int N = 7;
                var path = new Vector3[N];
                var rad = new float[N];
                for (int i = 0; i < N; i++)
                {
                    float t = (float)i / (N - 1);
                    path[i] = top + new Vector3(0.06f * Mathf.Sin(t * 6f + seed + k * 2.1f) * t, -L * t, 0.05f * Mathf.Sin(t * 4.5f + seed * 0.7f + k) * t);
                    rad[i] = i == N - 1 ? 0f : Mathf.Lerp(0.045f, 0.028f, t);
                }
                Tube(m, path, rad, 4, KelpJag, 30f, true);
            }
        }

        // ---------------------------------------------------------------- 觸手蒙皮網格
        /// <summary>
        /// 觸手：沿 +Z 的錐狀管（7 節，每節 TentSeg），斷面略扁；上面青色鱗皮、下面淺色腹面，
        /// 兩排吸盤在腹面、一排螢光斑點在背面。子網格：0 鱗皮、1 腹面、2 吸盤、3 螢光。
        /// 骨骼權重依靜止姿勢的 z 計算（關節上兩節各半、節中央全給該節），所以彎曲平滑。
        /// </summary>
        static Mesh TentacleMesh()
        {
            const string key = "kraken.tentacle";
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var b = new SkinBuilder(4);
            const int sides = 8, rings = TentBones * 2 + 1;
            const float flat = 0.85f;
            var zs = new float[rings + 1];
            var rs = new float[rings + 1];
            for (int q = 0; q < rings; q++)
            {
                zs[q] = q * TentSeg * 0.5f;
                rs[q] = (q & 1) == 0 ? TentJointR[q / 2] : (TentJointR[q / 2] + TentJointR[q / 2 + 1]) * 0.52f;
            }
            zs[rings] = TentBones * TentSeg + TentTip; rs[rings] = 0f;   // 尖端
            for (int q = 0; q < rings; q++)
            {
                float z0 = zs[q], r0 = rs[q], z1 = zs[q + 1], r1 = rs[q + 1];
                for (int j = 0; j < sides; j++)
                {
                    float a0 = (22.5f + 45f * j) * Mathf.Deg2Rad, a1 = (22.5f + 45f * (j + 1)) * Mathf.Deg2Rad;
                    var p00 = new Vector3(Mathf.Cos(a0) * r0, Mathf.Sin(a0) * r0 * flat, z0);
                    var p01 = new Vector3(Mathf.Cos(a1) * r0, Mathf.Sin(a1) * r0 * flat, z0);
                    var p10 = new Vector3(Mathf.Cos(a0) * r1, Mathf.Sin(a0) * r1 * flat, z1);
                    var p11 = new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1 * flat, z1);
                    b.sub = Mathf.Sin((a0 + a1) * 0.5f) < -0.38f ? 1 : 0;
                    if (r1 > 0f) b.Tri(p00, p11, p10);
                    if (r0 > 0f) b.Tri(p00, p01, p11);
                    if (q == 0) { b.sub = 0; b.Tri(Vector3.zero, p01, p00); }
                }
            }
            // 吸盤：腹面兩排，越往尖端越小
            b.sub = 2;
            float zEnd = TentBones * TentSeg - 0.08f;
            int idx = 0;
            for (float z = 0.16f; z < zEnd; z += 0.15f, idx++)
                for (int row = 0; row < 2; row++)
                {
                    float zz = z + row * 0.075f;
                    if (zz > zEnd) continue;
                    float r = TentRadiusAt(zz);
                    float a = (row == 0 ? 240f : 300f) * Mathf.Deg2Rad;
                    var pos = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * flat, zz);
                    var n = new Vector3(Mathf.Cos(a), Mathf.Sin(a) / flat, 0f).normalized;
                    float rsk = 0.3f * r;
                    var prof = new[] { -0.3f, 1f, 0.32f, 0.9f, 0.36f, 0.45f, 0.1f, 0f };
                    for (int i = 0; i < prof.Length; i++) prof[i] *= rsk;
                    b.Lathe(prof, 5, 1f, 1f, idx * 20f, pos, Quaternion.LookRotation(n, Vector3.forward));
                }
            // 背面的螢光斑點
            b.sub = 3;
            idx = 0;
            for (float z = 0.25f; z < zEnd - 0.1f; z += 0.24f, idx++)
            {
                float r = TentRadiusAt(z);
                float a = (90f + ((idx & 1) == 0 ? 24f : -24f)) * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * flat, z);
                var n = new Vector3(Mathf.Cos(a), Mathf.Sin(a) / flat, 0f).normalized;
                float rd = 0.28f * r;
                b.Lathe(new[] { -0.4f * rd, 0f, -0.1f * rd, rd, 0.3f * rd, 0.7f * rd, 0.5f * rd, 0f }, 5, 1f, 1f, 0f, pos, Quaternion.LookRotation(n, Vector3.forward));
            }
            var mesh = b.Build("Boss " + key, TentBones, TentSeg);
            meshCache[key] = mesh;
            return mesh;
        }

        static float TentRadiusAt(float z)
        {
            float q = z / TentSeg;
            int k = Mathf.Clamp(Mathf.FloorToInt(q), 0, TentBones - 1);
            return Mathf.Lerp(TentJointR[k], TentJointR[k + 1], Mathf.Clamp01(q - k));
        }

        /// <summary>蒙皮網格的小工具：收集三角形（各自獨立的頂點＝平面著色），最後依靜止姿勢的 z 算骨骼權重。</summary>
        sealed class SkinBuilder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<int>[] t;
            public int sub;

            public SkinBuilder(int subs) { t = new List<int>[subs]; for (int i = 0; i < subs; i++) t[i] = new List<int>(); }

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                var list = t[sub];
                list.Add(i); list.Add(i + 1); list.Add(i + 2);
            }

            /// <summary>同 PMesh.Lathe（剖面 (z, r) 對，由後往前；面朝外）。</summary>
            public void Lathe(float[] prof, int sides, float sx, float sy, float phaseDeg, Vector3 off, Quaternion rot)
            {
                int rings = prof.Length / 2;
                for (int i = 0; i < rings - 1; i++)
                {
                    float z0 = prof[i * 2], r0 = prof[i * 2 + 1], z1 = prof[i * 2 + 2], r1 = prof[i * 2 + 3];
                    for (int j = 0; j < sides; j++)
                    {
                        var p00 = P(z0, r0, j, sides, sx, sy, phaseDeg, off, rot);
                        var p01 = P(z0, r0, j + 1, sides, sx, sy, phaseDeg, off, rot);
                        var p10 = P(z1, r1, j, sides, sx, sy, phaseDeg, off, rot);
                        var p11 = P(z1, r1, j + 1, sides, sx, sy, phaseDeg, off, rot);
                        if (r1 > 0f) Tri(p00, p11, p10);
                        if (r0 > 0f) Tri(p00, p01, p11);
                    }
                }
            }

            static Vector3 P(float z, float r, int j, int sides, float sx, float sy, float phaseDeg, Vector3 off, Quaternion rot)
            {
                float a = (phaseDeg + 360f * j / sides) * Mathf.Deg2Rad;
                return off + rot * new Vector3(Mathf.Cos(a) * r * sx, Mathf.Sin(a) * r * sy, z);
            }

            public Mesh Build(string name, int bones, float seg)
            {
                var m = new Mesh { name = name };
                m.SetVertices(v);
                m.subMeshCount = t.Length;
                for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i, true);
                var bw = new BoneWeight[v.Count];
                for (int i = 0; i < v.Count; i++)
                {
                    // 第 k 個關節（z = k·seg）前後各半節：骨頭 k-1 與 k 線性混合
                    float x = v[i].z / seg + 0.5f;
                    int k = Mathf.FloorToInt(x);
                    float f = Mathf.Clamp01(x - k);
                    int lo = Mathf.Clamp(k - 1, 0, bones - 1), hi = Mathf.Clamp(k, 0, bones - 1);
                    var w = new BoneWeight();
                    if (lo == hi) { w.boneIndex0 = hi; w.weight0 = 1f; }
                    else if (f >= 0.5f) { w.boneIndex0 = hi; w.weight0 = f; w.boneIndex1 = lo; w.weight1 = 1f - f; }
                    else { w.boneIndex0 = lo; w.weight0 = 1f - f; w.boneIndex1 = hi; w.weight1 = f; }
                    bw[i] = w;
                }
                m.boneWeights = bw;
                var bp = new Matrix4x4[bones];
                for (int k = 0; k < bones; k++) bp[k] = Matrix4x4.Translate(new Vector3(0f, 0f, -k * seg));
                m.bindposes = bp;
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        // ================================================================ 動作
        void Update() { Animate(Time.deltaTime); }

        void Animate(float dt)
        {
            if (body == null) return;
            frameDt = dt;
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
                // 海妖 s_whirl：雙臂在頭上畫圈（兩手反相，像在攪動海水）
                if (Theme == 2 && atk == "s_whirl")
                {
                    float c = Mathf.Sin(clock * 7f), c2 = Mathf.Cos(clock * 7f);
                    target.shL.y += 32f * c * w; target.shR.y -= 32f * c * w;
                    target.shL.z += 14f * c2 * w; target.shR.z += 14f * c2 * w;
                    target.shL.x += 10f * c2 * w; target.shR.x += 10f * c2 * w;
                    target.spY += 7f * c * w;
                }
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
                    if (atk != "b_sweep" && atk != "p_whip" && atk != "p_thorn") shake = Mathf.Sin(atkT * 90f) * 0.025f * (1f - (atkT - StrikeIn) / StrikeHold);
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
                if (Theme == 1) UpdateFall();
                else if (Theme == 2) UpdateKrakenDeath();
                else if (deadT >= CollapseAt && !crumbled) Crumble();
            }
            cur = target;

            if (!crumbled)
            {
                float bob = walkAmt * 0.06f * cosP * cosP - jolt * 0.025f * walkAmt;
                ApplyPose(cur, bob + shake, -cosP * 3.5f * walkAmt, walkAmt, sinP, cosP);
            }
            else UpdateRubble(dt);

            if (Theme != 0) UpdateTreantBits(sinP);   // 樹王的藤蔓／毒囊；海妖的海帶／發光囊
            if (Theme == 2) UpdateKrakenBits(dt);
            UpdateGlow(hitK);
        }

        // ================================================================ 樹王：倒下、毒囊、藤蔓
        const float FallStart = 0.7f, FallDur = 0.85f, FallAngle = 84f;
        static readonly Vector3 FallPivot = new Vector3(0f, 0f, 0.55f);

        /// <summary>樹王死亡：先往後晃一下，再像被砍倒的樹整棵往前倒（越倒越快），落地彈一下。</summary>
        void UpdateFall()
        {
            if (deadT < FallStart) fallAng = -3f * Mathf.Sin(Mathf.PI * deadT / FallStart);
            else if (deadT < FallStart + FallDur)
            {
                float x = (deadT - FallStart) / FallDur;
                fallAng = FallAngle * x * x;
            }
            else
            {
                if (!fallen) { fallen = true; TreantLand(); }
                float t2 = deadT - FallStart - FallDur;
                fallAng = FallAngle - 4f * Mathf.Sin(t2 * 11f) * Mathf.Exp(-t2 * 5f);
            }
        }

        void TreantLand()
        {
            var c = transform.position;
            var fwd = transform.forward;
            SlamShockwave(c + fwd * 2f, 1.8f, new Color(0.55f, 0.2f, 0.85f));
            for (int k = 0; k < 3; k++) StepDust(c + fwd * (0.8f + 0.8f * k));
            if (leafFall != null && visible) leafFall.Emit(35);
        }

        /// <summary>毒囊脈動（蓄力時鼓脹、死後乾癟）、藤蔓永遠往下垂並輕輕搖晃。</summary>
        void UpdateTreantBits(float sinP)
        {
            float g = Mathf.Clamp01(cur.glow);
            float shrink = dead ? Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(deadT / 3f)) : 1f;
            for (int i = 0; i < sacs.Length; i++)
            {
                float k = (1f + 0.1f * Mathf.Sin(clock * (2.6f + 1.5f * enr) + i * 1.9f) + 0.35f * g) * shrink;
                sacs[i].localScale = sacBase[i] * k;
            }
            var root = transform.rotation;
            var up = transform.up;
            var groundP = transform.position;
            for (int i = 0; i < vines.Length; i++)
            {
                float sx = 4f * Mathf.Sin(clock * 1.1f + i * 1.7f) + 6f * sinP * walkAmt;
                float sz = 5f * Mathf.Sin(clock * 0.9f + i * 2.3f);
                vines[i].rotation = root * Quaternion.Euler(sx, 0f, sz);
                // 掛點太低（蹲下、倒地）時藤蔓縮短，像垂到地上堆起來，不穿進地面
                float h = Vector3.Dot(vines[i].position - groundP, up);
                float k = Mathf.Clamp(h / vineLen[i], 0.12f, 1f);
                vines[i].localScale = new Vector3(1f, k, 1f);
            }
        }

        /// <summary>某個部位正下方的地面點（根物件的地面高度）。</summary>
        Vector3 GroundUnder(Transform t)
        {
            var p = t.position;
            var up = transform.up;
            return p - up * Vector3.Dot(p - transform.position, up);
        }

        void ApplyPose(in Pose p, float bob, float roll, float wAmt, float sinP, float cosP)
        {
            // fallAng：樹王倒下，以樹幹前緣貼地的點（FallPivot）為軸往前倒，倒地後正面平貼地面、樹根腳翹起
            var bodyRot = Quaternion.Euler(fallAng, 0f, roll);
            var piv = Theme == 1 ? FallPivot : Vector3.zero;
            body.localPosition = new Vector3(0f, -p.crouch + bob, 0f) + piv - bodyRot * piv;
            body.localRotation = bodyRot;

            if (Theme == 2) UpdateTentacles(p, sinP, cosP, wAmt);   // 海妖沒有腿：觸手貼地扭動、爬行
            else
            {
                // 腿：依下蹲量解兩節 IK（腳踝留在髖正下方），再加走路的前後擺與抬膝
                float T = L1 + L2 - p.crouch + bob;
                float swing = 22f * wAmt;
                LegIK(hipL, knL, anL, T, sinP * swing, Mathf.Max(0f, cosP) * 30f * wAmt);
                LegIK(hipR, knR, anR, T, -sinP * swing, Mathf.Max(0f, -cosP) * 30f * wAmt + p.lift);
            }

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

            if (Theme == 2)
            {
                // 頭套膜：慢慢呼吸、吸氣鼓脹（s_bubbles／s_ink）、死後洩氣；眼睛在蓄力（召喚）時放大發亮
                float sw = 1f + 0.2f * p.swell + 0.018f * Mathf.Sin(clock * 1.7f);
                mantle.localScale = new Vector3(sw, 1f + 0.12f * p.swell, sw);
                float ek = 1f + 0.55f * Mathf.Clamp01(p.glow);
                for (int i = 0; i < eyes.Length; i++) eyes[i].localScale = eyeBase[i] * ek;
            }
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

        /// <summary>
        /// 熔岩（樹王：紫色脈絡）、核心、眼睛的顏色（脈動、狂暴、蓄力、受擊、死亡熄滅），以及火星（孢子光點）量與腳下熱光（毒霧光）。
        /// 樹王另外：樹葉狂暴轉紅、死後枯黃，樹皮褪灰，嘴與眼窩在出招時透出紫光，落葉量。
        /// </summary>
        void UpdateGlow(float hitK)
        {
            bool tr = Theme == 1, kr = Theme == 2;
            float pulseRate = Mathf.Lerp(2.2f, 6.5f, enr);
            float pulse = 0.5f + 0.5f * Mathf.Sin(clock * pulseRate) + 0.08f * Mathf.Sin(clock * 13.7f);
            float dim = dead ? Mathf.Clamp01(deadT / 2.5f) : 0f;
            float g = Mathf.Clamp01(cur.glow);

            var lavaBase = Color.Lerp(pal.glowN, pal.glowE, enr);
            var hotBase = Color.Lerp(pal.hotN, pal.hotE, enr);
            // 提亮的方向：熔岩往高溫的黃白；樹王的紫光往白紫（紫綠直接混會變灰）
            var lavaHi = tr || kr ? Color.Lerp(lavaBase, Color.white, 0.45f) : hotBase;
            var lava = Color.Lerp(pal.glowDim, lavaBase, Mathf.Clamp01(0.55f + 0.45f * pulse + 0.5f * g));
            lava = Color.Lerp(lava, lavaHi, Mathf.Clamp01(g * 0.35f + hitK * 0.5f + (tr ? 0.25f * enr : 0f)));
            var hot = Color.Lerp(hotBase, Color.white, Mathf.Clamp01(g * 0.5f + hitK * 0.4f));
            var coreC = Color.Lerp(lavaBase, lavaHi, Mathf.Clamp01(0.3f + 0.4f * pulse + 0.6f * g));
            var eye = Color.Lerp(Color.Lerp(pal.eyeN, pal.eyeE, enr), Color.white, g * 0.4f);
            if (dim > 0f)
            {
                lava = Color.Lerp(lava, pal.dead, dim);
                hot = Color.Lerp(hot, pal.dead, dim);
                coreC = Color.Lerp(coreC, pal.dead, dim);
                eye = Color.Lerp(eye, pal.dead, Mathf.Clamp01(dim * 2f));
            }
            lavaM.color = lava; hotM.color = hot; coreM.color = coreC; eyeM.color = eye;

            if (core != null && !crumbled) core.localScale = Vector3.one * (1f + 0.08f * pulse + 0.3f * g);

            // 受擊時岩石（樹皮）泛紅；樹王死後樹皮慢慢褪灰
            float wither = (tr || kr) && dead ? Mathf.Clamp01((deadT - 1.2f) / 2.5f) : 0f;
            float f = hitK * 0.75f;
            if (hitK > 0f || flashing || wither > 0f)
            {
                flashing = hitK > 0f || wither > 0f;   // 枯萎過的話，復活後的下一幀再刷回原色
                var witherC = kr ? KWitherC : WitherC;
                rockA.color = Color.Lerp(Color.Lerp(pal.a, witherC, wither * 0.5f), FlashC, f);
                rockB.color = Color.Lerp(Color.Lerp(pal.b, witherC, wither * 0.5f), FlashC, f);
                rockC.color = Color.Lerp(Color.Lerp(pal.c, witherC, wither * 0.5f), FlashC, f);
                if (tr) mossM.color = Color.Lerp(Color.Lerp(MossC, LeafDead, wither), FlashC, f * 0.6f);
            }
            if (kr)
            {
                // 珊瑚：狂暴轉深紅、死後白化；藤壺跟著閃色；嘴裡在出招時透出符文光
                var coral = Color.Lerp(Color.Lerp(CoralN, CoralE, enr), CoralBleach, wither);
                coralM.color = Color.Lerp(coral, FlashC, f * 0.5f);
                mossM.color = Color.Lerp(BarnC, FlashC, f * 0.5f);
                var glowMaw = lavaBase * 0.45f; glowMaw.a = 1f;
                hollowM.color = Color.Lerp(MawC, glowMaw, g * 0.7f * (1f - dim));
            }

            var leafC = Color.Lerp(Color.Lerp(LeafN, LeafE, enr), LeafDead, wither);
            if (tr)
            {
                leafM.color = Color.Lerp(leafC, FlashC, f * 0.5f);
                // 嘴與深洞：出招時透出紫光
                var glowHole = lavaBase * 0.4f; glowHole.a = 1f;
                hollowM.color = Color.Lerp(HollowC, glowHole, g * 0.6f * (1f - dim));
            }

            // 核心光暈
            if (halo != null)
            {
                var m = halo.main;
                m.startSize = (0.9f + 0.9f * g + 0.3f * enr + 0.15f * pulse) * (1f - dim);
                var hc = Color.Lerp(lavaBase, lavaHi, g);
                hc.a = (0.35f + 0.25f * pulse + 0.3f * g) * (1f - dim);
                m.startColor = hc;
            }

            // 火星（孢子光點）量：狂暴時變多，死亡時停止
            float rate = dead ? 0f : Mathf.Lerp(pal.emberLo, pal.emberHi, enr);
            if (embers != null && !Mathf.Approximately(rate, emberRate))
            {
                emberRate = rate;
                SpellFx.Rate(embers, rate);
            }

            // 落葉：平常零星飄落、狂暴變多（紅葉）、倒下後一陣落葉然後停止
            if (leafFall != null)
            {
                float lr = dead ? (deadT < 3.5f ? 4f : 0f) : Mathf.Lerp(0.4f, 3f, enr);
                if (!Mathf.Approximately(lr, leafRate))
                {
                    leafRate = lr;
                    SpellFx.Rate(leafFall, lr);
                }
                var lm = leafFall.main;
                var la = leafC * 1.6f; la.a = 1f;
                var lb = leafC * 1.1f; lb.a = 1f;
                lm.startColor = new ParticleSystem.MinMaxGradient(la, lb);
            }

            // 腳下熱光（毒霧光）
            if (heatM != null)
            {
                float a = (0.2f + 0.12f * enr + 0.06f * pulse + 0.15f * g) * (1f - dim);
                var c = Color.Lerp(pal.heatN, pal.heatE, enr);
                c.a = a;
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
            fallAng = 0f;
            fallen = false;
            bubbleT = 0f;
            slamPending = false;
            if (sacs != null) for (int i = 0; i < sacs.Length; i++) sacs[i].localScale = sacBase[i];
            cur = Idle();
        }

        void OnDestroy()
        {
            DestroyMat(rockA); DestroyMat(rockB); DestroyMat(rockC);
            DestroyMat(lavaM); DestroyMat(hotM); DestroyMat(coreM); DestroyMat(eyeM); DestroyMat(heatM);
            DestroyMat(leafM); DestroyMat(mossM); DestroyMat(hollowM); DestroyMat(coralM);
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
            if (type == PrimitiveType.Cube && !unlit && mat.shader == litShader)
            {
                var filter = go.GetComponent<MeshFilter>();
                filter.sharedMesh = CharacterRig.SmoothCube(filter.sharedMesh);
            }
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
        ///   p_thorn：腐化樹王丟出的約 0.6 m 黑色尖刺（帶倒鉤、發光的綠色尖端、綠色拖尾），沿 +Z 自轉。
        ///   p_spore：約 0.35 m 會晃動變形的紫綠毒液團（發光芯、紫色外膜、滴落的毒液、孢子拖尾）。
        ///   m_venom：毒液小怪吐出的小毒液團（p_spore 縮小到約 0.2 m）。
        ///   m_seed：種子射手的硬種子彈（約 0.12 m 的木質種子＋兩片葉翼，高速自轉）。
        ///   s_bubble：深淵海妖吐出的約 0.8 m 大泡泡（面向鏡頭的肥皂泡：彩虹薄邊＋反光點，會晃動變形，後面一串小泡泡）。
        ///   m_bubble：海洋小怪的泡泡（同 s_bubble，約 0.5 m）。
        ///   m_spine：河豚小怪射出的細長骨白色尖刺（約 0.4 m、發光的水藍尖端），沿 +Z 自轉。
        ///   s_wave：約 2.4 m 寬、1 m 高的弧形水牆（浪頭白色泡沫往前捲、水花、底部泡沫），原點在底部中央，往 +Z 前進。
        ///   s_ink：約 0.4 m 的深紫黑色墨團（黏稠的小團、濕亮反光、往下滴墨、暗色拖尾），會晃動變形。
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
                case "p_thorn": return BuildThorn();
                case "p_spore": return BuildGlob("p_spore", 1f);
                case "m_venom": return BuildGlob("m_venom", 0.58f);
                case "m_seed": return BuildSeed();
                case "s_bubble": return BuildBubble("s_bubble", 0.8f);
                case "m_bubble": return BuildBubble("m_bubble", 0.5f);
                case "m_spine": return BuildSpine();
                case "s_wave": return BuildWave();
                case "s_ink": return BuildInk();
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

        // ---------------------------------------------------------------- 森林系飛行物（腐化樹王與植物小怪）
        static Material projThorn, projSeed, projLeaf, projVenom, projVenomHot, projPurple;

        static void InitForestProjMats()
        {
            InitProjMats();
            if (projThorn == null) projThorn = new Material(litShader) { color = new Color(0.16f, 0.11f, 0.12f), name = "BossProjThorn" };
            if (projSeed == null) projSeed = new Material(litShader) { color = new Color(0.45f, 0.32f, 0.17f), name = "BossProjSeed" };
            if (projLeaf == null) projLeaf = new Material(litShader) { color = new Color(0.32f, 0.58f, 0.2f), name = "BossProjLeaf" };
            if (projVenom == null) projVenom = new Material(glowShader) { color = new Color(0.45f, 0.95f, 0.25f), name = "BossProjVenom" };
            if (projVenomHot == null) projVenomHot = new Material(glowShader) { color = new Color(0.85f, 1f, 0.6f), name = "BossProjVenomHot" };
            if (projPurple == null) projPurple = new Material(glowShader) { color = new Color(0.65f, 0.25f, 1f), name = "BossProjPurple" };
        }

        /// <summary>實心程式網格子物件（有光照、平面著色），網格依 key 快取共用。</summary>
        static Transform SolidChild(Transform parent, string key, Material mat, System.Action<PMesh> build)
        {
            var mesh = TreantMesh(key, build);
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        static readonly float[] ThornJag = { 1f, 0.8f, 1.1f, 0.85f, 1.05f, 0.9f };

        /// <summary>黑色尖刺：後粗前尖的六稜刺身＋三根往後勾的倒鉤，尖端罩著綠色光。</summary>
        static GameObject BuildThorn()
        {
            InitForestProjMats();
            var root = new GameObject("Boss Projectile p_thorn");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;

            var spin = Pivot("Spin", rt, Vector3.zero);
            SolidChild(spin, "proj.thorn", projThorn, m =>
            {
                m.Lathe(new[] { -0.3f, 0f, -0.28f, 0.045f, -0.12f, 0.062f, 0.05f, 0.046f, 0.2f, 0.022f, 0.32f, 0f }, 6, null, 1f, 1f, 0f, ThornJag);
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120f + 20f) * Mathf.Deg2Rad;
                    var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    var dir = (radial * 0.75f + Vector3.back * 0.66f).normalized;    // 往外往後勾
                    var at = radial * 0.04f + new Vector3(0f, 0f, -0.04f + 0.08f * i);
                    m.Lathe(new[] { 0f, 0.022f, 0.06f, 0.012f, 0.11f, 0f }, 4, null, 1f, 1f, 45f, null, at, Quaternion.LookRotation(dir));
                }
            });
            // 綠色發光的尖端
            GlowMesh(spin, "thorn.tip", m =>
            {
                m.Lathe(new[] { 0.08f, 0f, 0.14f, 0.05f, 0.24f, 0.042f, 0.32f, 0.02f, 0.37f, 0f }, 8,
                    (z, j) => new Color(0.45f, 1f, 0.3f, Mathf.Lerp(0.2f, 0.85f, Mathf.InverseLerp(0.08f, 0.32f, z))));
            });
            var tip = Pivot("Tip", rt, new Vector3(0f, 0f, 0.26f));
            Halo(tip, new Color(0.45f, 1f, 0.3f, 0.7f), 0.28f);

            var trail = SpellFx.NewPS(rt, "Trail", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 1f, 0.3f, 0.7f), new Color(0.6f, 0.25f, 1f, 0.5f));
                SpellFx.Sphere(trail, 0.03f);
                SpellFx.FadeOut(trail);
                SpellFx.SizeCurve(trail, SpellFx.Shrink);
                SpellFx.Rate(trail, 15f);
                var em = trail.emission; em.rateOverDistance = 22f;
                trail.Play();
            }
            anim.Setup(new[] { spin }, new[] { Vector3.forward }, new[] { 540f }, null, 0f, 0f);
            return root;
        }

        /// <summary>毒液團：發光的綠色芯＋偏一邊的亮點＋紫色半透明外膜＋繞著轉的小泡泡；會像果凍一樣晃動變形。</summary>
        static GameObject BuildGlob(string id, float k)
        {
            InitForestProjMats();
            var root = new GameObject("Boss Projectile " + id);
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;

            var glob = Pivot("Glob", rt, Vector3.zero);
            glob.localScale = Vector3.one * k;
            ProjPart(PrimitiveType.Sphere, glob, Vector3.zero, Vector3.one * 0.26f, projVenom);
            ProjPart(PrimitiveType.Sphere, glob, new Vector3(0.04f, 0.05f, 0.07f), Vector3.one * 0.11f, projVenomHot);
            GlowMesh(glob, "glob.shell", m =>
            {
                var prof = new float[14];
                for (int i = 0; i <= 6; i++)
                {
                    float a = Mathf.PI * i / 6f;
                    prof[i * 2] = -Mathf.Cos(a) * 0.19f;
                    prof[i * 2 + 1] = Mathf.Sin(a) * 0.19f;
                }
                m.Lathe(prof, 10, (z, j) => new Color(0.6f, 0.2f, 1f, 0.35f), 1f, 1f, 0f, JagF);
                // 往 -Z 拖長的淡綠尾巴
                m.Lathe(new[] { -0.5f, 0f, -0.3f, 0.07f, -0.12f, 0.12f, 0.02f, 0.13f, 0.1f, 0f }, 9,
                    (z, j) => new Color(0.4f, 1f, 0.25f, Mathf.Lerp(0.03f, 0.35f, Mathf.InverseLerp(-0.5f, 0.05f, z))), 1f, 1f, 15f, JagF);
            });
            // 繞著毒液團轉的小泡泡
            var orbit = Pivot("Orbit", glob, Vector3.zero);
            ProjPart(PrimitiveType.Sphere, orbit, new Vector3(0.17f, 0.05f, 0f), Vector3.one * 0.06f, projPurple);
            ProjPart(PrimitiveType.Sphere, orbit, new Vector3(-0.12f, -0.1f, 0.06f), Vector3.one * 0.045f, projVenom);
            Halo(rt, new Color(0.5f, 0.9f, 0.3f, 0.6f), 0.75f * k);

            var drip = SpellFx.NewPS(rt, "Drips", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            {
                var m = drip.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f * k + 0.01f, 0.06f * k + 0.01f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 1f, 0.25f), new Color(0.7f, 0.3f, 1f));
                m.gravityModifier = 0.7f;
                SpellFx.Sphere(drip, 0.12f * k, 0.2f);
                SpellFx.FadeOut(drip);
                SpellFx.Stretch(drip, 0.06f, 1.4f);
                SpellFx.Rate(drip, 12f);
                drip.Play();
            }
            var trail = SpellFx.NewPS(rt, "SporeTrail", SpellFx.BlendMat(SpellFx.TexSoft), 20, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.14f * k, 0.24f * k);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.15f, 0.45f, 0.45f), new Color(0.25f, 0.4f, 0.15f, 0.4f));
                SpellFx.Sphere(trail, 0.08f * k);
                SpellFx.FadeOut(trail);
                SpellFx.SizeCurve(trail, SpellFx.Grow);
                SpellFx.Rate(trail, 10f);
                var em = trail.emission; em.rateOverDistance = 8f;
                trail.Play();
            }
            anim.Setup(new[] { orbit }, new[] { new Vector3(0.2f, 1f, 0.3f).normalized }, new[] { 280f }, null, 0f, 0f);
            anim.Wobble(glob, 0.13f, 9f);
            return root;
        }

        /// <summary>硬種子：前尖後圓的木質種子＋後方上下兩片葉翼（像楓樹翅果），高速自轉。</summary>
        static GameObject BuildSeed()
        {
            InitForestProjMats();
            var root = new GameObject("Boss Projectile m_seed");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;

            var spin = Pivot("Spin", rt, Vector3.zero);
            SolidChild(spin, "proj.seed", projSeed, m =>
                m.Lathe(new[] { -0.06f, 0f, -0.05f, 0.026f, -0.02f, 0.04f, 0.02f, 0.036f, 0.055f, 0.014f, 0.075f, 0f }, 7, null, 1f, 0.85f));
            SolidChild(spin, "proj.seedFin", projLeaf, m =>
            {
                // 葉翼剖面沿 +Z，往 X 壓扁成薄片（YZ 平面），再轉成往後上／往後下
                var prof = new[] { 0f, 0f, 0.03f, 0.024f, 0.08f, 0.032f, 0.13f, 0.018f, 0.16f, 0f };
                m.Lathe(prof, 6, null, 0.18f, 1f, 0f, null, new Vector3(0f, 0.01f, -0.035f), Quaternion.Euler(204f, 0f, 0f));
                m.Lathe(prof, 6, null, 0.18f, 1f, 0f, null, new Vector3(0f, -0.01f, -0.035f), Quaternion.Euler(0f, 0f, 180f) * Quaternion.Euler(204f, 0f, 0f));
            });
            Halo(rt, new Color(0.6f, 1f, 0.35f, 0.35f), 0.22f);

            var trail = SpellFx.NewPS(rt, "Trail", SpellFx.AddMat(SpellFx.TexSoft), 16, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 1f, 0.35f, 0.6f), new Color(0.9f, 1f, 0.6f, 0.5f));
                SpellFx.Sphere(trail, 0.015f);
                SpellFx.FadeOut(trail);
                SpellFx.SizeCurve(trail, SpellFx.Shrink);
                SpellFx.Rate(trail, 10f);
                var em = trail.emission; em.rateOverDistance = 25f;
                trail.Play();
            }
            anim.Setup(new[] { spin }, new[] { Vector3.forward }, new[] { 900f }, null, 0f, 0f);
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
        // ================================================================ 樹根爆發
        /// <summary>
        /// 一次性的樹根爆發：在 worldFeetPos 周圍半徑 radius 的圓上（加上中間幾根）竄出尖銳的樹根，
        /// 地面有紫色光環與裂紋、泥土與孢子往上噴，約 0.8 秒後樹根縮回地面，約 1.2 秒播完、1.4 秒自己清除。
        /// 樹根從地面「長出」（縮放），不會有埋在地面下的部分，AR 沒有遮擋也不會穿幫。
        /// </summary>
        public static void RootEruption(Vector3 worldFeetPos, float radius)
        {
            InitForestProjMats();
            radius = Mathf.Max(0.3f, radius);
            var go = new GameObject("Boss Root Eruption");
            go.transform.position = worldFeetPos;
            var fx = go.AddComponent<FxAnim>();
            fx.killAt = 1.4f;
            var spikesAnim = go.AddComponent<RootSpikeAnim>();
            var root = go.transform;

            // 地面：本地 XY 平面 = 地面
            var ground = Pivot("Ground", root, new Vector3(0f, 0.03f, 0f));
            ground.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var purple = new Color(0.62f, 0.22f, 1f);
            fx.AddRing(ground, SpellFx.Circle(56, 1f), true, 0.16f, purple, 0f, 0.55f, radius * 0.4f, radius * 1.05f);
            fx.AddRing(ground, SpellFx.Zigzag(22, 0.55f, 1f), true, 0.07f, new Color(0.45f, 1f, 0.3f), 0.02f, 0.9f, radius * 0.6f, radius * 0.95f, true, 20f);
            fx.AddRing(ground, SpellFx.Circle(48, 1f), true, 0.3f, new Color(0.22f, 0.16f, 0.12f, 0.6f), 0.04f, 1.1f, radius * 0.7f, radius * 1.1f, false);

            // 尖刺樹根：外圈一圈（往中心微傾，像要合攏的牢籠）＋中間幾根
            float hk = Mathf.Clamp(radius / 1.2f, 0.7f, 1.3f);
            int ringN = Mathf.Clamp(Mathf.RoundToInt(radius * 7f), 7, 14);
            int total = ringN + 4;
            var rnd = new System.Random(Random.Range(0, 100000));
            for (int i = 0; i < total; i++)
            {
                bool outer = i < ringN;
                float a = outer ? (360f * i / ringN + Rnd(rnd, -10f, 10f)) : (90f * (i - ringN) + 45f + Rnd(rnd, -20f, 20f));
                float d = outer ? radius * Rnd(rnd, 0.78f, 0.95f) : radius * Rnd(rnd, 0.15f, 0.4f);
                float ar = a * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Cos(ar) * d, 0f, Mathf.Sin(ar) * d);
                var sp = new GameObject("RootSpike").transform;
                sp.SetParent(root, false);
                sp.localPosition = pos;
                // 讓根尖往圓心傾斜（外圈 15~30 度、中間隨意小角度）
                var inward = -pos.normalized;
                var tiltAxis = Vector3.Cross(Vector3.up, inward);
                float tilt = outer ? Rnd(rnd, 15f, 30f) : Rnd(rnd, -12f, 12f);
                sp.localRotation = (tiltAxis.sqrMagnitude > 1e-4f ? Quaternion.AngleAxis(tilt, tiltAxis.normalized) : Quaternion.identity)
                                   * Quaternion.Euler(0f, Rnd(rnd, 0f, 360f), 0f);
                var mf = sp.gameObject.AddComponent<MeshFilter>();
                mf.sharedMesh = RootSpikeMesh(i % 3);
                var r = sp.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = projThorn;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                float h = (outer ? Rnd(rnd, 0.75f, 1.15f) : Rnd(rnd, 0.5f, 0.8f)) * hk;
                float w = h * Rnd(rnd, 0.85f, 1.1f);
                spikesAnim.Add(sp, new Vector3(w, h, w), (outer ? Rnd(rnd, 0f, 0.12f) : Rnd(rnd, 0.06f, 0.18f)));
            }

            // 往上噴的泥土與紫綠孢子（子物件翻轉成 +Z 朝上）
            var upT = Pivot("Up", root, Vector3.zero);
            upT.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var dirt = SpellFx.NewPS(upT, "Dirt", SpellFx.BlendMat(SpellFx.TexSoft), 30, true);
            {
                var m = dirt.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.22f, 0.15f, 0.85f), new Color(0.18f, 0.13f, 0.1f, 0.8f));
                m.gravityModifier = 1.2f;
                SpellFx.Cone(dirt, 25f, radius * 0.85f);
                SpellFx.FadeOut(dirt);
                SpellFx.Spin(dirt, 5f);
                dirt.Play(); dirt.Emit(26);
            }
            var spores = SpellFx.NewPS(upT, "Spores", SpellFx.AddMat(SpellFx.TexSoft), 30, true);
            {
                var m = spores.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.65f, 0.25f, 1f, 0.9f), new Color(0.45f, 1f, 0.3f, 0.8f));
                SpellFx.Cone(spores, 35f, radius * 0.9f);
                SpellFx.FadeInOut(spores);
                SpellFx.Drag(spores, 2f);
                spores.Play(); spores.Emit(24);
            }
            var flash = SpellFx.NewPS(ground, "Flash", SpellFx.AddMat(SpellFx.TexSoft), 2, false);
            {
                var m = flash.main;
                m.startLifetime = 0.4f;
                m.startSize = radius * 1.6f;
                m.startColor = SpellFx.WithA(purple, 0.55f);
                SpellFx.FadeOut(flash);
                flash.Play(); flash.Emit(1);
            }
        }

        /// <summary>樹根尖刺（高 1 單位、底半徑約 0.15，從 y = 0 往上）：微彎、帶兩根小側刺，靠縮放決定大小。</summary>
        static Mesh RootSpikeMesh(int variant)
        {
            return TreantMesh("rootSpike" + variant, m =>
            {
                float bx = (variant - 1) * 0.06f;
                var path = Gnarl(Vector3.zero, new Vector3(bx, 1f, 0.04f * variant), new Vector3(0.05f, 0f, -0.03f), 6, 90 + variant, 0.02f);
                Tube(m, path, new[] { 0.15f, 0.12f, 0.09f, 0.06f, 0.03f, 0f }, 6, BarkJag, 10f, true);
                Twig(m, path[2], new Vector3(0.8f, 0.6f, 0.1f * variant), 0.22f, 0.04f, Vector3.up * 0.02f, 95 + variant, 4);
                Twig(m, path[3], new Vector3(-0.6f, 0.7f, -0.3f), 0.16f, 0.03f, Vector3.up * 0.02f, 97 + variant, 4);
            });
        }

        // ================================================================ 海洋系飛行物與範圍特效（深淵海妖與海洋小怪）
        static Material seaHide, seaBelly, seaSucker, seaGlow, seaInk, seaSpine, seaGlint, fxBlend, bubbleMat;
        static Texture2D bubbleTex;

        static void InitSeaMats()
        {
            InitProjMats();
            if (seaHide == null) seaHide = new Material(litShader) { color = HideAC, name = "BossSeaHide" };
            if (seaBelly == null) seaBelly = new Material(litShader) { color = HideBC, name = "BossSeaBelly" };
            if (seaSucker == null) seaSucker = new Material(litShader) { color = BarnC, name = "BossSeaSucker" };
            if (seaGlow == null) seaGlow = new Material(glowShader) { color = RuneN, name = "BossSeaGlow" };
            if (seaInk == null) seaInk = new Material(litShader) { color = new Color(0.1f, 0.05f, 0.13f), name = "BossSeaInk" };
            if (seaSpine == null) seaSpine = new Material(litShader) { color = new Color(0.86f, 0.8f, 0.64f), name = "BossSeaSpine" };
            if (seaGlint == null) seaGlint = new Material(glowShader) { color = new Color(0.92f, 0.85f, 1f), name = "BossSeaGlint" };
            // 頂點色半透明網格（水牆、漩渦）：Legacy Particles/Alpha Blended＋白貼圖
            if (fxBlend == null) fxBlend = new Material(SpellFx.BlendMat(SpellFx.TexSoft).shader) { name = "BossFxBlend", mainTexture = Texture2D.whiteTexture };
            if (bubbleMat == null) bubbleMat = new Material(SpellFx.AddMat(SpellFx.TexSoft).shader) { name = "BossBubble", mainTexture = BubbleTex() };
        }

        /// <summary>
        /// 肥皂泡貼圖（64×64）：彩虹色的薄邊（沿圓周換色相）＋幾乎透明的內部＋左上一大一小的反光點。
        /// 用在面向鏡頭的四邊形與泡泡粒子上（疊加材質，看起來像透明的泡泡）。
        /// </summary>
        static Texture2D BubbleTex()
        {
            if (bubbleTex != null) return bubbleTex;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "BossBubbleTex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    if (d >= 1f) { px[y * N + x] = new Color32(255, 255, 255, 0); continue; }
                    float rim = Mathf.Exp(-Mathf.Pow((d - 0.86f) / 0.075f, 2f)) * 0.9f;
                    float fill = 0.1f * d * d;
                    float hl = Mathf.Exp(-((u + 0.36f) * (u + 0.36f) + (v - 0.4f) * (v - 0.4f)) / 0.012f)
                             + 0.5f * Mathf.Exp(-((u - 0.34f) * (u - 0.34f) + (v + 0.38f) * (v + 0.38f)) / 0.005f);
                    float ang = Mathf.Atan2(v, u);
                    var rb = new Color(0.5f + 0.5f * Mathf.Cos(ang * 2f), 0.5f + 0.5f * Mathf.Cos(ang * 2f + 2.1f), 0.5f + 0.5f * Mathf.Cos(ang * 2f + 4.2f));
                    rb = Color.Lerp(rb, Color.white, 0.4f);
                    var fillC = new Color(0.55f, 0.9f, 1f);
                    float a = Mathf.Clamp01(rim + fill + hl);
                    var c = (rb * rim + fillC * fill + Color.white * hl) / Mathf.Max(1e-3f, rim + fill + hl);
                    a *= Mathf.Clamp01((1f - d) / 0.04f);
                    px[y * N + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(true, false);
            bubbleTex = t;
            return t;
        }

        /// <summary>XY 平面的單位四邊形（±0.5），面向鏡頭的泡泡用。</summary>
        static Mesh BubbleQuad()
        {
            const string key = "bubbleQuad";
            if (meshCache.TryGetValue(key, out var cached) && cached != null) return cached;
            var m = new Mesh { name = "BossBubbleQuad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            meshCache[key] = m;
            return m;
        }

        /// <summary>頂點色網格（給定材質：fxAdd 發光／fxBlend 半透明），網格依 key 快取共用。</summary>
        static Renderer SeaMesh(Transform parent, string key, Material mat, System.Action<PMesh> build)
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
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>實心程式網格子物件（有光照），網格依 key 快取共用。</summary>
        static Transform SeaSolid(Transform parent, string key, Material mat, System.Action<PMesh> build)
        {
            var go = new GameObject(key);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = KrakenMesh(key, build);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        /// <summary>大泡泡：面向鏡頭、會像果凍一樣晃動的肥皂泡（彩虹薄邊＋反光點）＋淡淡的水光＋後面一串小泡泡。</summary>
        static GameObject BuildBubble(string id, float diameter)
        {
            InitSeaMats();
            var root = new GameObject("Boss Projectile " + id);
            var rt = root.transform;
            var quad = new GameObject("Bubble");
            quad.transform.SetParent(rt, false);
            quad.AddComponent<MeshFilter>().sharedMesh = BubbleQuad();
            var qr = quad.AddComponent<MeshRenderer>();
            qr.sharedMaterial = bubbleMat;
            qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            qr.receiveShadows = false;
            root.AddComponent<BossBubbleAnim>().Setup(quad.transform, diameter / 0.9f, 0.09f, 6.5f);
            Halo(rt, new Color(0.35f, 0.85f, 1f, 0.22f), diameter * 1.25f);

            var trail = SpellFx.NewPS(rt, "TinyBubbles", bubbleMat, 24, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSize = new ParticleSystem.MinMaxCurve(diameter * 0.06f, diameter * 0.14f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 1f, 1f, 0.9f), new Color(0.5f, 0.85f, 1f, 0.8f));
                m.gravityModifier = -0.05f;
                SpellFx.Sphere(trail, diameter * 0.35f, 0.2f);
                SpellFx.FadeOut(trail);
                SpellFx.Noise(trail, 0.2f, 2f);
                SpellFx.Rate(trail, 6f);
                var em = trail.emission; em.rateOverDistance = 5f;
                trail.Play();
            }
            return root;
        }

        /// <summary>河豚刺：細長的骨白色尖刺（兩圈暗紋、發光的水藍尖端），沿 +Z 高速自轉。</summary>
        static GameObject BuildSpine()
        {
            InitSeaMats();
            var root = new GameObject("Boss Projectile m_spine");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;
            var spin = Pivot("Spin", rt, Vector3.zero);
            SeaSolid(spin, "proj.spine", seaSpine, m =>
                m.Lathe(new[] { -0.2f, 0f, -0.19f, 0.014f, -0.05f, 0.018f, 0.1f, 0.011f, 0.2f, 0f }, 6, null));
            SeaSolid(spin, "proj.spineBand", seaInk, m =>
            {
                m.Lathe(new[] { -0.15f, 0.017f, -0.12f, 0.019f, -0.09f, 0.017f }, 6, null, 1f, 1f, 0f, null, Vector3.zero, Quaternion.identity);
                m.Lathe(new[] { -0.02f, 0.018f, 0.01f, 0.019f, 0.04f, 0.016f }, 6, null, 1f, 1f, 0f, null, Vector3.zero, Quaternion.identity);
            });
            GlowMesh(spin, "spine.tip", m =>
                m.Lathe(new[] { 0.08f, 0f, 0.12f, 0.016f, 0.19f, 0.008f, 0.23f, 0f }, 6,
                    (z, j) => new Color(0.3f, 1f, 0.9f, Mathf.Lerp(0.2f, 0.9f, Mathf.InverseLerp(0.08f, 0.2f, z)))));
            var tip = Pivot("Tip", rt, new Vector3(0f, 0f, 0.18f));
            Halo(tip, new Color(0.3f, 1f, 0.9f, 0.6f), 0.18f);

            var trail = SpellFx.NewPS(rt, "Trail", SpellFx.AddMat(SpellFx.TexSoft), 16, true);
            {
                var m = trail.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 1f, 0.9f, 0.6f), new Color(0.8f, 1f, 1f, 0.5f));
                SpellFx.Sphere(trail, 0.015f);
                SpellFx.FadeOut(trail);
                SpellFx.SizeCurve(trail, SpellFx.Shrink);
                SpellFx.Rate(trail, 10f);
                var em = trail.emission; em.rateOverDistance = 25f;
                trail.Play();
            }
            anim.Setup(new[] { spin }, new[] { Vector3.forward }, new[] { 720f }, null, 0f, 0f);
            return root;
        }

        // 浪的剖面（z, y）：從後方底部往上越過浪頭、浪唇往前捲，再沿凹下去的浪面回到前方底部
        static readonly Vector2[] WaveProf =
        {
            new Vector2(-0.55f, 0f), new Vector2(-0.42f, 0.25f), new Vector2(-0.28f, 0.52f), new Vector2(-0.14f, 0.78f), new Vector2(0.02f, 0.96f),
            new Vector2(0.18f, 1f), new Vector2(0.3f, 0.92f), new Vector2(0.36f, 0.8f), new Vector2(0.33f, 0.7f), new Vector2(0.24f, 0.65f),
            new Vector2(0.13f, 0.58f), new Vector2(0.06f, 0.44f), new Vector2(0.04f, 0.24f), new Vector2(0.1f, 0f),
        };

        /// <summary>浪上的點：u -1..1 橫向（寬 2.4 m、中央往前凸、兩端變矮收尖），j＝剖面索引（可帶小數）。</summary>
        static Vector3 WavePoint(float u, float j)
        {
            int i = Mathf.Clamp(Mathf.FloorToInt(j), 0, WaveProf.Length - 2);
            var pr = Vector2.Lerp(WaveProf[i], WaveProf[i + 1], j - i);
            float hf = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 5f)));
            return new Vector3(1.2f * u, pr.y * hf, pr.x * (0.5f + 0.5f * hf) + 0.28f * (1f - u * u));
        }

        static Color WaveColor(int j, float hf)
        {
            Color c;
            if (j <= 3) c = Color.Lerp(new Color(0.03f, 0.25f, 0.35f, 0.75f), new Color(0.1f, 0.5f, 0.6f, 0.75f), j / 3f);          // 背面：深青
            else if (j <= 8) c = Color.Lerp(new Color(0.35f, 0.8f, 0.88f, 0.8f), new Color(0.85f, 0.97f, 1f, 0.85f), (j - 4) / 4f); // 浪頭到浪唇：亮
            else c = Color.Lerp(new Color(0.3f, 0.72f, 0.82f, 0.7f), new Color(0.05f, 0.3f, 0.42f, 0.8f), (j - 9) / 4f);           // 浪面：由亮往下變深
            c.a *= Mathf.Clamp01(hf * 1.6f);
            return c;
        }

        /// <summary>水牆：約 2.4 m 寬、1 m 高的弧形浪（浪頭白色泡沫往前捲），原點在底部中央，往 +Z 前進。</summary>
        static GameObject BuildWave()
        {
            InitSeaMats();
            var root = new GameObject("Boss Projectile s_wave");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;
            var wave = Pivot("Wave", rt, Vector3.zero);
            SeaMesh(wave, "wave.body", fxBlend, m =>
            {
                const int NU = 19;
                int NJ = WaveProf.Length;
                for (int i = 0; i < NU - 1; i++)
                {
                    float ua = -1f + 2f * i / (NU - 1), ub = -1f + 2f * (i + 1) / (NU - 1);
                    var pts = new Vector3[NJ]; var half = new Vector3[NJ]; var cols = new Color[NJ];
                    float hfA = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs((ua + ub) * 0.5f), 5f)));
                    for (int j = 0; j < NJ; j++)
                    {
                        var pa = WavePoint(ua, j); var pb = WavePoint(ub, j);
                        pts[j] = (pa + pb) * 0.5f; half[j] = (pb - pa) * 0.5f;
                        cols[j] = WaveColor(j, hfA);
                    }
                    m.Ribbon(pts, half, cols);
                }
                // 浪頭的白色泡沫帶（沿浪唇）
                const int NC = 23;
                var cp = new Vector3[NC]; var ch = new Vector3[NC]; var cc = new Color[NC];
                for (int i = 0; i < NC; i++)
                {
                    float u = -0.97f + 1.94f * i / (NC - 1);
                    float hf = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 5f)));
                    var a = WavePoint(u, 4.6f); var b = WavePoint(u, 7.4f);
                    cp[i] = (a + b) * 0.5f + new Vector3(0f, 0.015f, 0.01f); ch[i] = (b - a) * 0.55f + new Vector3(0f, 0.03f * Mathf.Sin(i * 2.3f), 0f);
                    cc[i] = new Color(0.95f, 1f, 1f, 0.9f * Mathf.Clamp01(hf * 2f));
                }
                m.Ribbon(cp, ch, cc);
            });
            // 浪頭與浪面上的水光（疊加，暗的地方也看得到）
            GlowMesh(wave, "wave.glow", m =>
            {
                const int NC = 21;
                var cp = new Vector3[NC]; var ch = new Vector3[NC]; var cc = new Color[NC];
                for (int i = 0; i < NC; i++)
                {
                    float u = -0.95f + 1.9f * i / (NC - 1);
                    float hf = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 5f)));
                    cp[i] = WavePoint(u, 5f) + new Vector3(0f, 0.02f, 0f);
                    ch[i] = new Vector3(0f, 0.045f, 0f);
                    cc[i] = new Color(0.5f, 0.95f, 1f, 0.7f * hf);
                }
                m.Ribbon(cp, ch, cc);
                for (int i = 0; i < NC; i++)
                {
                    float u = -0.95f + 1.9f * i / (NC - 1);
                    float hf = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 5f)));
                    var a = WavePoint(u, 10f); var b = WavePoint(u, 12.4f);
                    cp[i] = (a + b) * 0.5f + new Vector3(0f, 0f, 0.01f); ch[i] = (b - a) * 0.5f;
                    cc[i] = new Color(0.15f, 0.55f, 0.65f, 0.35f * hf);
                }
                m.Ribbon(cp, ch, cc);
            });

            // 浪頭噴起的水花、底部翻滾的白色泡沫、往下滴的水珠
            var spray = SpellFx.NewPS(rt, "Spray", SpellFx.BlendMat(SpellFx.TexSoft), 50, true);
            spray.transform.localPosition = new Vector3(0f, 0.98f, 0.38f);
            {
                var m = spray.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 1f, 1f, 0.75f), new Color(0.65f, 0.9f, 0.95f, 0.6f));
                m.gravityModifier = 0.5f;
                var sh = spray.shape; sh.shapeType = ParticleSystemShapeType.SingleSidedEdge; sh.radius = 0.95f;
                SpellFx.FadeOut(spray);
                SpellFx.SizeCurve(spray, SpellFx.Grow);
                SpellFx.Rise(spray, 0.5f);
                SpellFx.Rate(spray, 45f);
                spray.Play();
            }
            var foam = SpellFx.NewPS(rt, "Foam", SpellFx.BlendMat(SpellFx.TexSoft), 30, true);
            foam.transform.localPosition = new Vector3(0f, 0.06f, 0.25f);
            {
                var m = foam.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.38f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.98f, 1f, 0.65f), new Color(0.6f, 0.85f, 0.92f, 0.5f));
                var sh = foam.shape; sh.shapeType = ParticleSystemShapeType.SingleSidedEdge; sh.radius = 1.15f;
                SpellFx.FadeOut(foam);
                SpellFx.SizeCurve(foam, SpellFx.Grow);
                SpellFx.Rate(foam, 32f);
                foam.Play();
            }
            var drops = SpellFx.NewPS(rt, "Drops", SpellFx.AddMat(SpellFx.TexSoft), 30, true);
            drops.transform.localPosition = new Vector3(0f, 0.85f, 0.4f);
            {
                var m = drops.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 0.95f, 1f, 0.8f), new Color(0.9f, 1f, 1f, 0.8f));
                m.gravityModifier = 1.2f;
                var sh = drops.shape; sh.shapeType = ParticleSystemShapeType.SingleSidedEdge; sh.radius = 1f;
                SpellFx.FadeOut(drops);
                SpellFx.Stretch(drops, 0.05f, 1.2f);
                SpellFx.Rate(drops, 24f);
                drops.Play();
            }
            anim.Setup(new Transform[0], new Vector3[0], new float[0], wave, 0.025f, 7f);
            return root;
        }

        /// <summary>墨團：約 0.4 m 的深紫黑色黏稠團塊（幾個鼓起的小團、濕亮的反光點、半透明外膜），往下滴墨、拖著暗色煙。</summary>
        static GameObject BuildInk()
        {
            InitSeaMats();
            var root = new GameObject("Boss Projectile s_ink");
            var anim = root.AddComponent<BossFxSpin>();
            var rt = root.transform;
            var glob = Pivot("Glob", rt, Vector3.zero);
            ProjPart(PrimitiveType.Sphere, glob, Vector3.zero, Vector3.one * 0.34f, seaInk);
            ProjPart(PrimitiveType.Sphere, glob, new Vector3(0.11f, -0.07f, -0.05f), Vector3.one * 0.19f, seaInk);
            ProjPart(PrimitiveType.Sphere, glob, new Vector3(-0.1f, 0.07f, -0.09f), Vector3.one * 0.16f, seaInk);
            ProjPart(PrimitiveType.Sphere, glob, new Vector3(0.02f, -0.11f, 0.09f), Vector3.one * 0.14f, seaInk);
            ProjPart(PrimitiveType.Sphere, glob, new Vector3(-0.06f, 0.1f, 0.12f), new Vector3(0.07f, 0.045f, 0.035f), seaGlint);   // 濕亮反光
            SeaMesh(glob, "ink.shell", fxBlend, m =>
            {
                var prof = new float[14];
                for (int i = 0; i <= 6; i++)
                {
                    float a = Mathf.PI * i / 6f;
                    prof[i * 2] = -Mathf.Cos(a) * 0.22f;
                    prof[i * 2 + 1] = Mathf.Sin(a) * 0.22f;
                }
                m.Lathe(prof, 10, (z, j) => new Color(0.16f, 0.06f, 0.22f, 0.5f), 1f, 1f, 0f, JagF);
                // 往 -Z 拖長的墨痕
                m.Lathe(new[] { -0.6f, 0f, -0.36f, 0.08f, -0.14f, 0.14f, 0.02f, 0.15f, 0.12f, 0f }, 9,
                    (z, j) => new Color(0.1f, 0.04f, 0.14f, Mathf.Lerp(0.05f, 0.55f, Mathf.InverseLerp(-0.6f, 0.05f, z))), 1f, 1f, 15f, JagF);
            });
            Halo(rt, new Color(0.55f, 0.2f, 0.8f, 0.3f), 0.75f);

            var drip = SpellFx.NewPS(rt, "InkDrips", SpellFx.BlendMat(SpellFx.TexSoft), 24, true);
            {
                var m = drip.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.12f, 0.04f, 0.16f, 0.95f), new Color(0.25f, 0.08f, 0.32f, 0.9f));
                m.gravityModifier = 0.9f;
                SpellFx.Sphere(drip, 0.15f, 0.2f);
                SpellFx.FadeOut(drip);
                SpellFx.Stretch(drip, 0.06f, 1.5f);
                SpellFx.Rate(drip, 14f);
                drip.Play();
            }
            var smoke = SpellFx.NewPS(rt, "InkTrail", SpellFx.BlendMat(SpellFx.TexSoft), 20, true);
            {
                var m = smoke.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.14f, 0.06f, 0.18f, 0.55f), new Color(0.06f, 0.03f, 0.08f, 0.45f));
                SpellFx.Sphere(smoke, 0.1f);
                SpellFx.FadeOut(smoke);
                SpellFx.SizeCurve(smoke, SpellFx.Grow);
                SpellFx.Rate(smoke, 10f);
                var em = smoke.emission; em.rateOverDistance = 8f;
                smoke.Play();
            }
            anim.Setup(new Transform[0], new Vector3[0], new float[0], null, 0f, 0f);
            anim.Wobble(glob, 0.13f, 8f);
            return root;
        }

        // ================================================================ 水花
        /// <summary>一次性的水花：擴散的水藍光環＋白色泡沫環＋往上噴的水花與水珠＋貼地的水霧，播完自己清除。</summary>
        internal static void WaterSplash(Vector3 worldFeetPos, float radius)
        {
            radius = Mathf.Max(0.2f, radius);
            var go = new GameObject("Boss Water Splash");
            go.transform.position = worldFeetPos + Vector3.up * 0.02f;
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 1.5f;
            var root = go.transform;
            var ground = Pivot("Ground", root, Vector3.zero);
            ground.localRotation = Quaternion.Euler(90f, 0f, 0f);
            anim.AddRing(ground, SpellFx.Circle(56, 1f), true, 0.18f, new Color(0.35f, 0.9f, 1f), 0f, 0.6f, radius * 0.3f, radius * 1.1f);
            anim.AddRing(ground, SpellFx.Circle(48, 1f), true, 0.12f, new Color(0.9f, 0.98f, 1f, 0.7f), 0.04f, 0.8f, radius * 0.2f, radius * 0.9f, false);

            var upT = Pivot("Up", root, Vector3.zero);
            upT.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // +Z 朝上
            var spray = SpellFx.NewPS(upT, "Spray", SpellFx.BlendMat(SpellFx.TexSoft), 34, true);
            {
                var m = spray.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 1f, 1f, 0.8f), new Color(0.55f, 0.85f, 0.95f, 0.7f));
                m.gravityModifier = 1.3f;
                SpellFx.Cone(spray, 30f, radius * 0.5f);
                SpellFx.FadeOut(spray);
                SpellFx.SizeCurve(spray, SpellFx.Grow);
                spray.Play(); spray.Emit(30);
            }
            var drops = SpellFx.NewPS(upT, "Drops", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            {
                var m = drops.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.95f, 1f, 0.9f), new Color(0.9f, 1f, 1f, 0.9f));
                m.gravityModifier = 1.4f;
                SpellFx.Cone(drops, 40f, radius * 0.4f);
                SpellFx.FadeOut(drops);
                SpellFx.Stretch(drops, 0.05f, 1.3f);
                drops.Play(); drops.Emit(22);
            }
            var mist = SpellFx.NewPS(ground, "Mist", SpellFx.BlendMat(SpellFx.TexSoft), 12, true);
            {
                var m = mist.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.6f);
                m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.25f, radius * 0.45f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.95f, 1f, 0.45f), new Color(0.6f, 0.85f, 0.92f, 0.35f));
                SpellFx.Disc(mist, radius * 0.2f);
                SpellFx.FadeOut(mist);
                SpellFx.SizeCurve(mist, SpellFx.Grow);
                SpellFx.Drag(mist, 3f);
                mist.Play(); mist.Emit(10);
            }
        }

        // ================================================================ 觸手重擊
        /// <summary>
        /// 一次性的觸手重擊：一條巨大的觸手在目標旁邊（玩家鏡頭前方，左右偏一點）從地面竄出、高高翹起往後捲，
        /// 約 0.42 秒時朝 worldFeetPos 重重砸下（水花環＋水花），躺一下之後縮回地面；約 1.3 秒播完、1.4 秒自己清除。
        /// 觸手用縮放「長出／縮回」，沒有埋在地面下的部分，AR 沒有遮擋也不會穿幫。
        /// </summary>
        public static void TentacleSlam(Vector3 worldFeetPos, float radius)
        {
            InitSeaMats();
            radius = Mathf.Max(0.3f, radius);
            float sc = 1.45f * Mathf.Clamp(radius / 1.2f, 0.85f, 1.25f);
            float len = (TentBones * TentSeg + TentTip) * sc;
            // 砸下的方向：從鏡頭前方往鏡頭砸（看得最清楚），左右隨機偏 15~30 度；沒有鏡頭就隨機
            Vector3 dir;
            var cam = Camera.main;
            if (cam != null)
            {
                var f = cam.transform.forward; f.y = 0f;
                dir = f.sqrMagnitude > 1e-4f ? -f.normalized : Vector3.forward;
                dir = Quaternion.Euler(0f, Random.Range(15f, 30f) * (Random.value < 0.5f ? -1f : 1f), 0f) * dir;
            }
            else dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;

            var go = new GameObject("Boss Tentacle Slam");
            var rootPos = worldFeetPos - dir * (len * 0.6f);
            go.transform.SetPositionAndRotation(rootPos + Vector3.up * (TentJointR[0] * 0.85f * sc), Quaternion.LookRotation(dir, Vector3.up));
            var bones = new Transform[TentBones];
            Transform parent = go.transform;
            var scaleT = Pivot("Scale", parent, Vector3.zero);
            parent = scaleT;
            for (int k = 0; k < TentBones; k++)
            {
                bones[k] = Pivot("T" + k, parent, k == 0 ? Vector3.zero : new Vector3(0f, 0f, TentSeg));
                parent = bones[k];
            }
            var skin = new GameObject("TentacleSkin");
            skin.transform.SetParent(scaleT, false);
            var smr = skin.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = TentacleMesh();
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterials = new[] { seaHide, seaBelly, seaSucker, seaGlow };
            smr.quality = SkinQuality.Bone2;
            smr.localBounds = new Bounds(new Vector3(0f, 0f, 1f), new Vector3(4.6f, 4.6f, 4.6f));
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.receiveShadows = false;
            go.AddComponent<TentacleSlamAnim>().Setup(scaleT, bones, sc, worldFeetPos, radius);
            WaterSplash(rootPos, 0.55f * sc);   // 從地面竄出的水花
        }

        // ================================================================ 漩渦
        /// <summary>
        /// 地面的漩渦：深色中心往外變淺的水面圓盤＋白色泡沫螺旋＋水藍發光螺旋（三層以不同速度旋轉）＋外圈往上噴、繞圈的水花，
        /// 以及被吸進中心的光點。0.3 秒內長出，持續 life 秒，最後 0.45 秒淡出，之後自己清除。
        /// </summary>
        public static void Whirlpool(Vector3 worldFeetPos, float radius, float life)
        {
            InitSeaMats();
            radius = Mathf.Max(0.3f, radius);
            life = Mathf.Max(0.6f, life);
            var go = new GameObject("Boss Whirlpool");
            go.transform.position = worldFeetPos;
            var root = go.transform;
            var flat = Quaternion.Euler(90f, 0f, 0f);   // Annulus 的 XY 平面 → 地面 XZ

            var disc = Pivot("Disc", root, new Vector3(0f, 0.012f, 0f));
            var rDisc = SeaMesh(disc, "whirl.disc", fxBlend, m =>
            {
                var deep = new Color(0.01f, 0.07f, 0.11f, 0.85f);
                var deep2 = new Color(0.03f, 0.2f, 0.28f, 0.75f);
                var mid = new Color(0.1f, 0.48f, 0.56f, 0.55f);
                var rim = new Color(0.5f, 0.85f, 0.9f, 0f);
                m.Annulus(0f, 0.3f, 40, deep, deep2, Vector3.zero, flat);
                m.Annulus(0.3f, 0.72f, 40, deep2, mid, Vector3.zero, flat);
                m.Annulus(0.72f, 1f, 40, mid, rim, Vector3.zero, flat);
                m.Annulus(0.8f, 0.9f, 48, new Color(0.9f, 1f, 1f, 0f), new Color(0.9f, 1f, 1f, 0.5f), new Vector3(0f, 0.004f, 0f), flat);
                m.Annulus(0.9f, 0.99f, 48, new Color(0.9f, 1f, 1f, 0.5f), new Color(0.9f, 1f, 1f, 0f), new Vector3(0f, 0.004f, 0f), flat);
            });
            var foam = Pivot("Foam", root, new Vector3(0f, 0.022f, 0f));
            var rFoam = SeaMesh(foam, "whirl.foam", fxBlend, m => WhirlSpiral(m, 5, 0f, 0.035f, new Color(0.95f, 1f, 1f, 0.75f)));
            var glint = Pivot("Glint", root, new Vector3(0f, 0.03f, 0f));
            var rGlint = SeaMesh(glint, "whirl.glint", fxAdd, m => WhirlSpiral(m, 3, 36f, 0.018f, new Color(0.3f, 0.95f, 1f, 0.7f)));

            var upT = Pivot("Up", root, new Vector3(0f, 0.05f, 0f));
            upT.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // +Z 朝上；圓盤形狀在本地 XY＝地面
            var spray = SpellFx.NewPS(upT, "Spray", SpellFx.BlendMat(SpellFx.TexSoft), 70, false);
            {
                var m = spray.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                m.startSize = new ParticleSystem.MinMaxCurve(radius * 0.09f, radius * 0.18f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.92f, 1f, 1f, 0.7f), new Color(0.55f, 0.85f, 0.95f, 0.55f));
                m.gravityModifier = 0.45f;
                SpellFx.Disc(spray, radius * 0.95f, 0.15f);
                var v = spray.velocityOverLifetime;
                v.enabled = true;
                v.space = ParticleSystemSimulationSpace.Local;
                v.x = 0f; v.y = 0f; v.z = 0.9f;
                v.orbitalX = 0f; v.orbitalY = 0f; v.orbitalZ = -2.6f;
                v.radial = -0.15f;
                SpellFx.FadeOut(spray);
                SpellFx.SizeCurve(spray, SpellFx.Grow);
                SpellFx.Rate(spray, 28f * Mathf.Clamp(radius, 0.6f, 2f));
                spray.Play();
            }
            var motes = SpellFx.NewPS(upT, "Suction", SpellFx.AddMat(SpellFx.TexSoft), 40, false);
            {
                var m = motes.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 1f, 0.9f, 0.9f), new Color(0.8f, 1f, 1f, 0.8f));
                SpellFx.Disc(motes, radius * 0.9f, 0.3f);
                SpellFx.Swirl(motes, -4f, -radius * 1.1f);
                SpellFx.FadeInOut(motes);
                SpellFx.Rate(motes, 22f);
                motes.Play();
            }
            go.AddComponent<WhirlpoolAnim>().Setup(new[] { disc, foam, glint }, new[] { 50f, 120f, 170f }, new[] { rDisc, rFoam, rGlint },
                new[] { spray, motes }, radius, life);
        }

        /// <summary>漩渦的螺旋臂（XZ 平面、單位半徑）：從外圈往中心捲 1.7π，越往內越細越淡。</summary>
        static void WhirlSpiral(PMesh m, int arms, float phaseDeg, float width, Color c)
        {
            const int N = 20;
            for (int arm = 0; arm < arms; arm++)
            {
                float th0 = (phaseDeg + 360f * arm / arms) * Mathf.Deg2Rad;
                var pts = new Vector3[N]; var half = new Vector3[N]; var cols = new Color[N];
                for (int i = 0; i < N; i++)
                {
                    float f = (float)i / (N - 1);
                    float r = 0.95f - 0.82f * f;
                    float th = th0 + f * 1.7f * Mathf.PI;
                    var radial = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                    pts[i] = radial * r;
                    half[i] = radial * (width * (0.4f + 1.2f * (1f - f)));
                    cols[i] = SpellFx.WithA(c, c.a * Mathf.Sqrt(Mathf.Sin(Mathf.PI * Mathf.Min(1f, f * 1.15f + 0.08f))) * (1f - 0.5f * f));
                }
                m.Ribbon(pts, half, cols);
            }
        }
    }

    /// <summary>頭目飛行物的小動畫：多個零件自轉＋一個零件閃爍縮放（每幀不配置記憶體）。</summary>
    internal class BossFxSpin : MonoBehaviour
    {
        Transform[] spins; Vector3[] axes; float[] speeds;
        Transform flick; Vector3 flickBase = Vector3.one; float flickAmt, flickRate = 20f;
        Transform wobble; Vector3 wobbleBase = Vector3.one; float wobbleAmt, wobbleRate = 8f;
        float clock;

        /// <summary>果凍般的晃動：三個軸以不同相位伸縮（拉長一軸時另一軸變扁）。</summary>
        public void Wobble(Transform t, float amt, float rate)
        {
            wobble = t; wobbleAmt = amt; wobbleRate = rate;
            if (t != null) wobbleBase = t.localScale;
        }

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
            if (wobble != null && wobbleAmt > 0f)
            {
                float a = Mathf.Sin(clock * wobbleRate), b = Mathf.Sin(clock * wobbleRate * 1.37f + 2.1f);
                wobble.localScale = Vector3.Scale(wobbleBase, new Vector3(1f + wobbleAmt * a, 1f - wobbleAmt * (0.6f * a + 0.4f * b), 1f + wobbleAmt * b));
            }
        }
    }

    /// <summary>樹根爆發的動畫：每根尖刺依延遲從地面長出（稍微衝過頭再回彈）、停留、再縮回地面；整組由 FxAnim 到時清除。</summary>
    internal class RootSpikeAnim : MonoBehaviour
    {
        readonly List<Transform> spikes = new List<Transform>();
        readonly List<Vector3> full = new List<Vector3>();
        readonly List<float> delays = new List<float>();
        readonly List<Renderer> rs = new List<Renderer>();
        float time;

        const float Rise = 0.16f, SinkAt = 0.78f, SinkDur = 0.32f;

        public void Add(Transform t, Vector3 fullScale, float delay)
        {
            spikes.Add(t); full.Add(fullScale); delays.Add(delay);
            rs.Add(t.GetComponentInChildren<Renderer>());
            t.localScale = Vector3.one * 0.001f;
            if (rs[rs.Count - 1] != null) rs[rs.Count - 1].enabled = false;
        }

        void Update()
        {
            time += Time.deltaTime;
            for (int i = 0; i < spikes.Count; i++)
            {
                var t = spikes[i];
                if (t == null) continue;
                float lt = time - delays[i];
                float h;   // 高度比例 0..1（> 1 衝過頭）
                if (lt <= 0f) h = 0f;
                else if (lt < Rise)
                {
                    float x = lt / Rise;
                    h = 1f - (1f - x) * (1f - x);   // 迅速竄出
                    h *= 1.15f;
                }
                else if (lt < Rise + 0.1f) h = Mathf.Lerp(1.15f, 1f, (lt - Rise) / 0.1f);
                else if (lt < SinkAt) h = 1f;
                else
                {
                    float x = Mathf.Clamp01((lt - SinkAt) / SinkDur);
                    h = 1f - x * x;   // 慢慢縮回地面
                }
                bool on = h > 0.01f;
                if (rs[i] != null && rs[i].enabled != on) rs[i].enabled = on;
                var f = full[i];
                float w = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(h));   // 長出時根部也跟著變粗
                t.localScale = on ? new Vector3(f.x * w, f.y * h, f.z * w) : Vector3.one * 0.001f;
            }
        }
    }

    /// <summary>大泡泡：四邊形每幀轉向鏡頭（反光點固定在左上），並像果凍一樣在兩軸上交替伸縮。</summary>
    internal class BossBubbleAnim : MonoBehaviour
    {
        Transform quad;
        float size = 1f, amt, rate = 6f, clock;

        public void Setup(Transform q, float s, float a, float r)
        {
            quad = q; size = s; amt = a; rate = r;
            clock = Random.Range(0f, 10f);
            if (q != null) q.localScale = new Vector3(s, s, 1f);
        }

        void LateUpdate()
        {
            if (quad == null) return;
            clock += Time.deltaTime;
            if (clock > 1000f) clock -= 1000f;
            var cam = Camera.main;
            if (cam != null)
            {
                var d = quad.position - cam.transform.position;
                if (d.sqrMagnitude > 1e-6f) quad.rotation = Quaternion.LookRotation(d, cam.transform.up);
            }
            float a = Mathf.Sin(clock * rate), b = Mathf.Sin(clock * rate * 1.37f + 2.1f);
            quad.localScale = new Vector3(size * (1f + amt * a), size * (1f - amt * (0.6f * a + 0.4f * b)), 1f);
        }
    }

    /// <summary>
    /// 觸手重擊的動畫：竄出（縮放 0 → 1、往上翹）→ 往後捲蓄力 → 加速砸下（ImpactT 時在目標噴水花）→ 躺著彈一下 → 縮回地面。
    /// 每節骨頭只設定相對前一節的俯仰／左右角度（每幀不配置記憶體）。
    /// </summary>
    internal class TentacleSlamAnim : MonoBehaviour
    {
        Transform scaleT;
        Transform[] bones;
        float scale, radius, time;
        Vector3 target;
        bool hit;

        const float RiseEnd = 0.2f, HoldEnd = 0.32f, ImpactT = 0.42f, LieEnd = 0.85f, SinkEnd = 1.3f, KillAt = 1.4f;
        static readonly float[] RiseP = { -88f, -4f, -6f, -8f, -10f, -14f, -18f };
        static readonly float[] RearP = { -102f, -8f, -12f, -14f, -18f, -22f, -28f };
        static readonly float[] SlamP = { 0f, -1f, -1f, -1f, -2f, -5f, -12f };

        public void Setup(Transform s, Transform[] b, float sc, Vector3 tgt, float r)
        {
            scaleT = s; bones = b; scale = sc; target = tgt; radius = r;
            scaleT.localScale = Vector3.one * 0.001f;
            Pose(RiseP, RiseP, 0f, 0f);
        }

        void Pose(float[] a, float[] b, float t, float wig)
        {
            for (int k = 0; k < bones.Length; k++)
            {
                float p = Mathf.LerpUnclamped(a[k], b[k], t);
                float y = k > 0 ? wig * (3f + 1.5f * k) * Mathf.Sin(time * 9f - k * 0.9f) : 0f;
                bones[k].localRotation = Quaternion.Euler(p, y, 0f);
            }
        }

        void Update()
        {
            time += Time.deltaTime;
            if (bones == null) return;
            float s;
            if (time < RiseEnd)
            {
                float x = time / RiseEnd;
                s = 1f - (1f - x) * (1f - x);          // 迅速竄出
                Pose(RiseP, RiseP, 0f, 1f);
            }
            else if (time < HoldEnd)
            {
                float x = (time - RiseEnd) / (HoldEnd - RiseEnd);
                s = 1f;
                Pose(RiseP, RearP, x * x * (3f - 2f * x), 1f);   // 往後捲蓄力
            }
            else if (time < ImpactT)
            {
                float x = (time - HoldEnd) / (ImpactT - HoldEnd);
                s = 1f;
                Pose(RearP, SlamP, x * x, 0.3f);                  // 越砸越快
            }
            else if (time < LieEnd)
            {
                if (!hit)
                {
                    hit = true;
                    BossRig.WaterSplash(target, radius);
                }
                float x = (time - ImpactT) / (LieEnd - ImpactT);
                s = 1f;
                // 砸地後的反彈：整條微微彈起再落下
                float bounce = -6f * Mathf.Sin(x * Mathf.PI * 2.5f) * Mathf.Exp(-x * 4f);
                for (int k = 0; k < bones.Length; k++)
                    bones[k].localRotation = Quaternion.Euler(SlamP[k] + (k == 0 ? bounce : -bounce * 0.3f), 0f, 0f);
            }
            else if (time < SinkEnd)
            {
                float x = (time - LieEnd) / (SinkEnd - LieEnd);
                s = 1f - x * x;                           // 縮回地面
                Pose(SlamP, RiseP, x * 0.15f, 0f);
            }
            else s = 0f;
            bool on = s > 0.002f;
            scaleT.localScale = on ? Vector3.one * (scale * s) : Vector3.one * 0.001f;
            if (time >= KillAt) Destroy(gameObject);
        }
    }

    /// <summary>漩渦的動畫：三層各自旋轉、0.3 秒長出、最後 0.45 秒淡出（MaterialPropertyBlock 改透明度，共用材質不受影響）並停止水花。</summary>
    internal class WhirlpoolAnim : MonoBehaviour
    {
        Transform[] layers; float[] speeds; Renderer[] rends; ParticleSystem[] systems;
        float radius, life, time;
        bool stopped;
        MaterialPropertyBlock mpb;
        static readonly int TintId = Shader.PropertyToID("_TintColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        const float GrowT = 0.3f, FadeT = 0.45f;

        public void Setup(Transform[] l, float[] sp, Renderer[] r, ParticleSystem[] ps, float rad, float lf)
        {
            layers = l; speeds = sp; rends = r; systems = ps; radius = rad; life = lf;
            mpb = new MaterialPropertyBlock();
            Apply(0f, 0f);
        }

        void Apply(float grow, float alpha)
        {
            float s = radius * Mathf.Lerp(0.35f, 1f, grow);
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                layers[i].localScale = new Vector3(s, 1f, s);
                layers[i].localRotation = Quaternion.Euler(0f, speeds[i] * time, 0f);   // 正角度＝俯視順時針：螺旋臂看起來往中心流
            }
            mpb.SetColor(TintId, new Color(0.5f, 0.5f, 0.5f, 0.5f * alpha));
            mpb.SetColor(ColorId, new Color(1f, 1f, 1f, alpha));
            for (int i = 0; i < rends.Length; i++) if (rends[i] != null) rends[i].SetPropertyBlock(mpb);
        }

        void Update()
        {
            time += Time.deltaTime;
            if (layers == null) return;
            float x = Mathf.Clamp01(time / GrowT);
            float grow = 1f - (1f - x) * (1f - x);
            float fade = Mathf.Clamp01((life - time) / FadeT);
            Apply(grow, grow * fade);
            if (!stopped && time >= life - FadeT)
            {
                stopped = true;
                for (int i = 0; i < systems.Length; i++) if (systems[i] != null) systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (time >= life + 1f) Destroy(gameObject);
        }
    }
}
