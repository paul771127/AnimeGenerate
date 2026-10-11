using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 熔岩巨像召喚的小怪「熔岩小鬼」：約 0.9 m 高、駝背的小型熔岩生物，與巨像同樣的視覺語言
    ///   （深色玄武岩塊＋橘紅熔岩光、發光的眼睛、發光的小爪子、頭頂一小撮火焰）。
    ///   座標：腳底在本地 y = 0，面向本地 +Z，真實公尺（父物件縮放請保持 1）。
    ///   效能：每隻 14 個網格 Renderer＋1 個火星粒子；所有小鬼共用同一組 static 材質，
    ///   個別的閃色／蓄力發亮／死亡變暗用 MaterialPropertyBlock 處理（不複製材質、每幀不配置記憶體）。
    /// </summary>
    public class MinionRig : MonoBehaviour
    {
        // ================================================================ 對外介面
        public const float Height = 0.9f;

        /// <summary>移動速度（公尺/秒），驅動快速的小碎步；0 = 待機（抽動、東張西望）。</summary>
        public float MoveSpeed { get; set; }

        public static MinionRig Create()
        {
            InitMats();
            var go = new GameObject("Minion LavaImp");
            var rig = go.AddComponent<MinionRig>();
            rig.Build();
            return rig;
        }

        /// <summary>舉起右爪預備（熔岩越來越亮），duration 秒擺到位後一直維持到 Attack。</summary>
        public void Windup(float duration)
        {
            if (dead || body == null) return;
            phase = PhaseWindup;
            atkT = 0f;
            atkDur = Mathf.Max(0.05f, duration);
        }

        /// <summary>快速往前揮爪，約 0.4 秒後回到待機。</summary>
        public void Attack()
        {
            if (dead || body == null) return;
            snap = cur;
            phase = PhaseStrike;
            atkT = 0f;
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

        // ================================================================ 共用材質與網格（所有小鬼共用）
        static Shader litShader, glowShader;
        static Material rockMat, lavaMat, eyeMat, fxAdd;
        static Mesh rockMesh0, rockMesh1, flameMesh;
        static MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int TintId = Shader.PropertyToID("_TintColor");

        static readonly Color RockA = new Color(0.3f, 0.26f, 0.24f);
        static readonly Color RockB = new Color(0.2f, 0.18f, 0.17f);
        static readonly Color FlashC = new Color(1f, 0.55f, 0.4f);
        static readonly Color LavaN = new Color(1f, 0.38f, 0.06f);
        static readonly Color LavaDim = new Color(0.6f, 0.1f, 0.02f);
        static readonly Color LavaHot = new Color(1f, 0.9f, 0.55f);
        static readonly Color EyeN = new Color(1f, 0.8f, 0.3f);
        static readonly Color Dark = new Color(0.12f, 0.06f, 0.04f);
        static readonly Color RockDead = new Color(0.08f, 0.07f, 0.07f);

        static void InitMats()
        {
            if (rockMat != null) return;
            glowShader = Shader.Find("Sprites/Default");
            litShader = Shader.Find("Legacy Shaders/Diffuse");
            if (litShader == null) litShader = Shader.Find("Mobile/Diffuse");
            if (litShader == null) litShader = Shader.Find("Diffuse");
            if (litShader == null) litShader = glowShader;
            rockMat = new Material(litShader) { color = Color.white, name = "ImpRock" };      // 顏色由 PropertyBlock 給
            lavaMat = new Material(glowShader) { color = LavaN, name = "ImpLava" };
            eyeMat = new Material(glowShader) { color = EyeN, name = "ImpEye" };
            fxAdd = new Material(SpellFx.AddMat(SpellFx.TexSoft).shader) { name = "ImpFxAdd", mainTexture = Texture2D.whiteTexture };
            mpb = new MaterialPropertyBlock();
            rockMesh0 = RockMesh(0);
            rockMesh1 = RockMesh(1);
            // 頭頂火焰：沿 +Z 的參差火舌（擺放時轉成朝上）
            var pm = new PMesh(1);
            float[] jag = { 1f, 0.7f, 1.15f, 0.8f, 1.05f, 0.75f, 1.2f };
            pm.Lathe(new[] { -0.02f, 0f, 0.02f, 0.06f, 0.08f, 0.07f, 0.16f, 0.045f, 0.26f, 0f }, 7,
                (z, j) => new Color(1f, Mathf.Lerp(0.75f, 0.25f, z / 0.26f), 0.1f, Mathf.Lerp(0.85f, 0.15f, z / 0.26f)), 1f, 1f, 0f, jag);
            pm.Lathe(new[] { 0f, 0f, 0.03f, 0.035f, 0.1f, 0.03f, 0.16f, 0f }, 6,
                (z, j) => new Color(1f, 0.95f, 0.65f, 0.8f), 1f, 1f, 25f, jag);
            flameMesh = pm.Build("ImpFlame", false);
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

        // ================================================================ 骨架與狀態
        Transform body, spine, head, hipL, hipR, shL, shR, wrL, wrR, flame;
        Vector3 flameBase;
        readonly List<Renderer> rockList = new List<Renderer>();
        readonly List<Color> rockCols = new List<Color>();
        readonly List<Renderer> lavaList = new List<Renderer>();
        Renderer[] rockR, lavaR; Color[] rockC;
        Renderer[] eyeR = new Renderer[2];
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

        static Pose Idle()
        {
            var p = new Pose { spX = 28f, head = -22f, crouch = 0.03f, wrL = -30f, wrR = -30f };
            p.shL = p.shR = new Vector3(-35f, 0f, 18f);
            return p;
        }

        static Pose WindupPose()
        {
            var p = Idle();
            p.spX = 8f; p.spY = 25f; p.head = -12f; p.crouch = 0.07f; p.glow = 1f;
            p.shR = new Vector3(-155f, 20f, 25f); p.wrR = -50f;
            p.shL = new Vector3(-55f, 0f, 30f); p.wrL = -40f;
            return p;
        }

        static Pose StrikePose()
        {
            var p = Idle();
            p.spX = 40f; p.spY = -25f; p.head = -30f; p.crouch = 0.08f; p.glow = 0.7f;
            p.shR = new Vector3(-50f, -40f, 10f); p.wrR = 10f;
            p.shL = new Vector3(-10f, 0f, 30f); p.wrL = -30f;
            return p;
        }

        const int PhaseNone = 0, PhaseWindup = 1, PhaseStrike = 2;
        int phase;
        float atkT, atkDur = 0.5f, clock, walkPhase, walkAmt, hitT, deadT, twitchT, lookY, lookTarget;
        Pose cur, snap;
        bool dead;
        const float HitDur = 0.15f, StrikeIn = 0.08f, StrikeHold = 0.1f, StrikeOut = 0.22f, DieDur = 0.8f;

        // ================================================================ 建立
        void Build()
        {
            clock = Random.Range(0f, 10f);   // 每隻的呼吸、脈動錯開
            body = Pivot("Body", transform, Vector3.zero);

            // ---- 短腿（髖在 0.32 m）
            hipL = Pivot("HipL", body, new Vector3(-0.12f, 0.32f, 0f));
            hipR = Pivot("HipR", body, new Vector3(0.12f, 0.32f, 0f));
            Rock(hipL, new Vector3(0f, -0.16f, 0.02f), new Vector3(0.15f, 0.34f, 0.17f), Vector3.zero, rockMesh1, RockB);
            Rock(hipR, new Vector3(0f, -0.16f, 0.02f), new Vector3(0.15f, 0.34f, 0.17f), Vector3.zero, rockMesh0, RockB);

            // ---- 駝背的軀幹＋肚子裡透出的熔岩
            spine = Pivot("Spine", body, new Vector3(0f, 0.32f, 0f));
            Rock(spine, new Vector3(0f, 0.2f, 0f), new Vector3(0.42f, 0.42f, 0.36f), new Vector3(0f, 0f, 8f), rockMesh0, RockA);
            Glow(PrimitiveType.Sphere, spine, new Vector3(0f, 0.16f, 0.12f), new Vector3(0.24f, 0.26f, 0.18f), lavaMat, true);

            // ---- 頭（往前突出）＋斜吊發光眼＋頭頂小火焰
            head = Pivot("Head", spine, new Vector3(0f, 0.38f, 0.12f));
            Rock(head, new Vector3(0f, 0.06f, 0.06f), new Vector3(0.3f, 0.24f, 0.28f), Vector3.zero, rockMesh1, RockA);
            for (int s = -1; s <= 1; s += 2)
                eyeR[(s + 1) / 2] = Glow(PrimitiveType.Cube, head, new Vector3(0.065f * s, 0.08f, 0.195f), new Vector3(0.07f, 0.03f, 0.03f), eyeMat, false, new Vector3(0f, 0f, 16f * s));
            var fl = new GameObject("Flame");
            fl.transform.SetParent(head, false);
            fl.transform.localPosition = new Vector3(0f, 0.15f, 0.02f);
            fl.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            fl.AddComponent<MeshFilter>().sharedMesh = flameMesh;
            flameR = fl.AddComponent<MeshRenderer>();
            flameR.sharedMaterial = fxAdd;
            NoShadow(flameR);
            flame = fl.transform; flameBase = flame.localScale;

            // ---- 細手臂＋岩石手＋發光爪
            shL = Arm(-1f, out wrL);
            shR = Arm(1f, out wrR);

            // ---- 少量火星
            embers = SpellFx.NewPS(spine, "Embers", SpellFx.AddMat(SpellFx.TexSoft), 8, true);
            embers.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.08f), new Color(1f, 0.8f, 0.35f));
                SpellFx.Sphere(embers, 0.25f);
                SpellFx.FadeInOut(embers);
                SpellFx.Rise(embers, 0.3f);
                SpellFx.Rate(embers, 3f);
                embers.Play();
            }

            rockR = rockList.ToArray(); rockC = rockCols.ToArray(); lavaR = lavaList.ToArray();
            cur = Idle();
            Animate(0f);
        }

        Transform Arm(float s, out Transform wrist)
        {
            var sh = Pivot(s < 0f ? "ShoulderL" : "ShoulderR", spine, new Vector3(0.21f * s, 0.32f, 0.04f));
            Rock(sh, new Vector3(0f, -0.14f, 0f), new Vector3(0.11f, 0.3f, 0.11f), Vector3.zero, rockMesh0, RockB);
            wrist = Pivot("Wrist", sh, new Vector3(0f, -0.29f, 0f));
            Rock(wrist, new Vector3(0f, -0.04f, 0.01f), new Vector3(0.14f, 0.12f, 0.15f), Vector3.zero, rockMesh1, RockA);
            // 三根爪子用一個拉長的發光方塊表示（往前下方彎）
            Glow(PrimitiveType.Cube, wrist, new Vector3(0f, -0.1f, 0.07f), new Vector3(0.12f, 0.03f, 0.13f), lavaMat, true, new Vector3(35f, 0f, 0f));
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

            // ---- 小碎步：一個循環約前進 0.5 m，步頻快
            float speed = dead ? 0f : Mathf.Clamp(MoveSpeed, 0f, 4f);
            bool acting = phase != PhaseNone;
            walkAmt = Mathf.MoveTowards(walkAmt, acting ? 0f : Mathf.Clamp01(speed / 0.3f), dt * 6f);
            if (speed > 0.01f && !acting) walkPhase += dt * Mathf.PI * 2f * Mathf.Max(speed, 0.3f) / 0.5f;
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 6f);
            if (walkPhase > 1000f) walkPhase -= Mathf.PI * 300f;
            float sinP = Mathf.Sin(walkPhase), cosP = Mathf.Cos(walkPhase);

            // 待機時頭會一下一下地轉向別處（小動物般的抽動）
            twitchT -= dt;
            if (twitchT <= 0f) { twitchT = Random.Range(0.4f, 1.4f); lookTarget = Random.Range(-35f, 35f); }
            lookY = Mathf.MoveTowards(lookY, lookTarget * (1f - walkAmt), dt * 300f);

            float breath = Mathf.Sin(clock * 3.2f);
            var basePose = Idle();
            basePose.spX += breath * 2f + walkAmt * 10f;
            basePose.headY = lookY;
            basePose.head -= walkAmt * 8f;
            float armSwing = sinP * 30f * walkAmt;
            basePose.shL.x += armSwing; basePose.shR.x -= armSwing;
            basePose.shL.z += breath * 3f; basePose.shR.z += breath * 3f;

            Pose target = basePose;
            if (phase == PhaseWindup)
            {
                atkT += dt;
                float x = Mathf.Clamp01(atkT / atkDur);
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
            if (hitK > 0f) { target.spX -= 22f * hitK; target.head -= 15f * hitK; target.crouch += 0.03f * hitK; }

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
            float bob = walkAmt * 0.035f * Mathf.Abs(cosP);
            body.localPosition = new Vector3(0f, -cur.crouch + bob - 0.3f * deadE * deadE, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, -cosP * 7f * walkAmt);
            body.localScale = Vector3.one * (1f - 0.45f * deadE);
            float legSwing = 38f * walkAmt;
            float crouchBend = cur.crouch * 150f;
            hipL.localRotation = Quaternion.Euler(sinP * legSwing - crouchBend * 0.5f - Mathf.Max(0f, cosP) * 15f * walkAmt, 0f, -4f);
            hipR.localRotation = Quaternion.Euler(-sinP * legSwing - crouchBend * 0.5f - Mathf.Max(0f, -cosP) * 15f * walkAmt, 0f, 4f);
            spine.localRotation = Quaternion.Euler(cur.spX, cur.spY + sinP * 8f * walkAmt, 0f);
            head.localRotation = Quaternion.Euler(cur.head, cur.headY, 0f);
            shL.localRotation = Quaternion.Euler(cur.shL.x, -cur.shL.y, -cur.shL.z);
            shR.localRotation = Quaternion.Euler(cur.shR.x, cur.shR.y, cur.shR.z);
            wrL.localRotation = Quaternion.Euler(cur.wrL, 0f, 0f);
            wrR.localRotation = Quaternion.Euler(cur.wrR, 0f, 0f);

            // ---- 頭頂火焰閃爍
            float fk = 1f + 0.18f * Mathf.Sin(clock * 17f) + 0.1f * Mathf.Sin(clock * 41f + 1.3f) + 0.4f * cur.glow;
            flame.localScale = new Vector3(flameBase.x * (2f - fk) * 0.9f + 0.1f, flameBase.y * (2f - fk) * 0.9f + 0.1f, flameBase.z * fk) * (1f - deadE);

            UpdateColors(hitK, deadE);
        }

        void UpdateColors(float hitK, float deadE)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(clock * 4f);
            float g = Mathf.Clamp01(cur.glow);
            var lava = Color.Lerp(LavaDim, LavaN, 0.6f + 0.4f * pulse);
            lava = Color.Lerp(lava, LavaHot, Mathf.Clamp01(g * 0.85f + hitK * 0.6f));
            lava = Color.Lerp(lava, Dark, deadE);
            var eye = Color.Lerp(Color.Lerp(EyeN, Color.white, g * 0.5f), Dark, Mathf.Clamp01(deadE * 1.5f));

            mpb.Clear();
            mpb.SetColor(ColorId, lava);
            for (int i = 0; i < lavaR.Length; i++) lavaR[i].SetPropertyBlock(mpb);
            mpb.SetColor(ColorId, eye);
            for (int i = 0; i < eyeR.Length; i++) eyeR[i].SetPropertyBlock(mpb);
            for (int i = 0; i < rockR.Length; i++)
            {
                var c = Color.Lerp(rockC[i], FlashC, hitK * 0.7f);
                c = Color.Lerp(c, RockDead, deadE);
                mpb.SetColor(ColorId, c);
                rockR[i].SetPropertyBlock(mpb);
            }
            mpb.Clear();
            float fa = 0.5f * (1f - deadE);
            mpb.SetColor(TintId, new Color(0.5f, 0.5f, 0.5f, fa));
            mpb.SetColor(ColorId, new Color(1f, 1f, 1f, 1f - deadE));
            flameR.SetPropertyBlock(mpb);
        }

        // ================================================================ 召喚特效
        /// <summary>一次性的召喚特效：地面熔岩裂口（光環收縮＋擴散）、往上噴的熔岩火花與黑煙，約 1.5 秒後自己清除。</summary>
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

        Renderer Glow(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool isLava, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            NoShadow(r);
            if (isLava) lavaList.Add(r);
            return r;
        }
    }
}
