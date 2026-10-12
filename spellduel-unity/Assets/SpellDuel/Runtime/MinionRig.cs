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
    ///     6 虛空術士（遠程）：火焰射手外形的紫色版本。
    ///   森林植物小怪（另一套外形／動作，見「森林植物小怪」區段）：
    ///     7 毒液噴花（遠程）：約 1.1 m，根鬚底座＋彎曲花莖，頂端綠色毒囊＋紫色花瓣大嘴；預備＝花往後仰、毒囊鼓脹，出手＝往前上方噴吐（拋物線）。
    ///     8 荊棘衝鋒獸：約 0.8 m 高、1.3 m 長，四腳樹樁野豬，背上長滿荊棘與苔蘚、獠牙；預備＝低頭、前蹄刨地，出手＝往前猛衝的姿勢。
    ///     9 藤蔓術士（遠程）：約 1.1 m，扭曲藤蔓編成的瘦長人形，葉子兜帽＋綠色發光眼睛；預備＝雙手高舉引導，出手＝雙手往前推。
    ///    10 食人花（近戰）：約 1.05 m，粗花莖＋根鬚腳，頂端帶尖牙的上下顎（外綠內紅）；預備＝大嘴張開，出手＝猛然咬合。
    ///    11 孢子菇：約 0.8 m，矮胖的菇柄＋短腿，紫色菇傘上有發光斑點；預備＝菇傘脈動鼓起，出手＝菇傘壓扁噴出孢子。
    ///    12 種子槍手（遠程）：約 0.95 m，根鬚底座的球莖上架著種莢砲塔與長長的莢管；預備＝瞄準、種莢鼓脹、管口發光，出手＝後座力。
    ///   效能：每隻 ≤ 18 個 Renderer（含 1 個火星／花粉粒子）；材質、網格全部 static 共用，
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

        /// <summary>
        /// 切換種類（0 近戰、1 火焰射手、2 坦克、3 自爆、4 岩石投擲者、5 熔岩狙擊手、6 虛空術士；
        /// 森林：7 毒液噴花、8 荊棘衝鋒獸、9 藤蔓術士、10 食人花、11 孢子菇、12 種子槍手），會重建外觀；請在 Create() 之後立刻呼叫。
        /// </summary>
        public void SetVariant(int kind)
        {
            kind = Mathf.Clamp(kind, 0, 12);
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
            snapW = plantW;
            phase = PhaseStrike;
            atkT = 0f;
            if (kind == 1 || kind == 4 || kind == 7) ballGone = 0f;   // 火球／巨石／毒液丟出去了，稍後重新長出
            if (kind == 11) PuffSpores();
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
            public Color accent, accent2, accent3;            // 植物用：主要特徵色、次要特徵色、葉子綠
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
            // ---- 森林植物（rockA＝莖／主體、rockB＝根／樹皮、lava＝發光、accent/2/3 見各自註解）
            new Variant   // 7 毒液噴花：accent＝毒囊、accent2＝紫色花瓣、accent3＝葉
            {
                size = 1f, stride = 0.45f, legSwing = 0f, bob = 0.07f, step = 1f,
                rockA = new Color(0.24f, 0.46f, 0.16f), rockB = new Color(0.33f, 0.23f, 0.13f),
                lava = new Color(0.55f, 1f, 0.2f), lavaDim = new Color(0.2f, 0.45f, 0.05f), hot = new Color(0.88f, 1f, 0.6f), eye = new Color(0.7f, 1f, 0.3f),
                emberA = new Color(0.5f, 0.95f, 0.25f), emberB = new Color(0.8f, 0.5f, 1f),
                accent = new Color(0.36f, 0.62f, 0.18f), accent2 = new Color(0.58f, 0.22f, 0.72f), accent3 = new Color(0.3f, 0.56f, 0.18f),
            },
            new Variant   // 8 荊棘衝鋒獸：accent＝切面木頭、accent2＝獠牙／荊棘、accent3＝苔蘚
            {
                size = 1f, stride = 0.6f, legSwing = 30f, bob = 0.04f, step = 0.85f,
                rockA = new Color(0.38f, 0.27f, 0.17f), rockB = new Color(0.25f, 0.17f, 0.1f),
                lava = new Color(1f, 0.6f, 0.15f), lavaDim = new Color(0.5f, 0.2f, 0.04f), hot = new Color(1f, 0.9f, 0.5f), eye = new Color(1f, 0.72f, 0.2f),
                emberA = new Color(0.55f, 0.8f, 0.3f), emberB = new Color(0.9f, 0.85f, 0.4f),
                accent = new Color(0.78f, 0.62f, 0.4f), accent2 = new Color(0.82f, 0.77f, 0.56f), accent3 = new Color(0.3f, 0.5f, 0.15f),
            },
            new Variant   // 9 藤蔓術士：accent＝兜帽葉、accent2＝深色臉、accent3＝披風葉
            {
                size = 1f, stride = 0.5f, legSwing = 30f, bob = 0.03f, step = 0.95f,
                rockA = new Color(0.2f, 0.4f, 0.15f), rockB = new Color(0.14f, 0.28f, 0.1f),
                lava = new Color(0.4f, 1f, 0.35f), lavaDim = new Color(0.1f, 0.45f, 0.1f), hot = new Color(0.85f, 1f, 0.75f), eye = new Color(0.55f, 1f, 0.4f),
                emberA = new Color(0.35f, 1f, 0.35f), emberB = new Color(0.8f, 1f, 0.6f),
                accent = new Color(0.28f, 0.55f, 0.18f), accent2 = new Color(0.07f, 0.09f, 0.05f), accent3 = new Color(0.22f, 0.46f, 0.14f),
            },
            new Variant   // 10 食人花：accent＝顎外側、accent2＝紅色口腔、accent3＝葉
            {
                size = 1f, stride = 0.45f, legSwing = 0f, bob = 0.06f, step = 1.05f,
                rockA = new Color(0.25f, 0.47f, 0.15f), rockB = new Color(0.33f, 0.23f, 0.13f),
                lava = new Color(1f, 0.3f, 0.35f), lavaDim = new Color(0.45f, 0.06f, 0.1f), hot = new Color(1f, 0.8f, 0.7f), eye = new Color(1f, 0.92f, 0.3f),
                emberA = new Color(0.6f, 0.95f, 0.3f), emberB = new Color(1f, 0.6f, 0.6f),
                accent = new Color(0.42f, 0.6f, 0.17f), accent2 = new Color(0.72f, 0.12f, 0.15f), accent3 = new Color(0.3f, 0.56f, 0.18f),
            },
            new Variant   // 11 孢子菇：rockA＝菇柄、rockB＝菌褶、accent＝菇傘、lava＝發光斑點
            {
                size = 1f, stride = 0.4f, legSwing = 34f, bob = 0.04f, step = 1.1f,
                rockA = new Color(0.86f, 0.8f, 0.66f), rockB = new Color(0.6f, 0.48f, 0.38f),
                lava = new Color(0.75f, 1f, 0.35f), lavaDim = new Color(0.35f, 0.55f, 0.1f), hot = new Color(0.95f, 1f, 0.75f), eye = new Color(0.9f, 1f, 0.5f),
                emberA = new Color(0.75f, 1f, 0.35f), emberB = new Color(0.8f, 0.5f, 1f),
                accent = new Color(0.52f, 0.17f, 0.48f), accent2 = new Color(0.86f, 0.8f, 0.66f), accent3 = new Color(0.3f, 0.56f, 0.18f),
            },
            new Variant   // 12 種子槍手：accent＝種莢膛、accent2＝莢管／種子、accent3＝葉
            {
                size = 1f, stride = 0.45f, legSwing = 0f, bob = 0.06f, step = 1f,
                rockA = new Color(0.27f, 0.48f, 0.17f), rockB = new Color(0.33f, 0.23f, 0.13f),
                lava = new Color(1f, 0.82f, 0.3f), lavaDim = new Color(0.5f, 0.32f, 0.05f), hot = new Color(1f, 0.97f, 0.7f), eye = new Color(1f, 0.92f, 0.4f),
                emberA = new Color(0.6f, 0.95f, 0.3f), emberB = new Color(1f, 0.9f, 0.45f),
                accent = new Color(0.62f, 0.6f, 0.22f), accent2 = new Color(0.4f, 0.32f, 0.15f), accent3 = new Color(0.32f, 0.58f, 0.2f),
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
            plant = k >= 7;
            if (plant) { BuildPlant(k); return; }
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
            if (plant) { AnimatePlant(dt); return; }
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

        /// <summary>
        /// 森林版召喚／傳送特效：地面冒出一圈翠綠光環與泥土，八株嫩芽從地裡竄出再縮回去，
        /// 葉片與花粉往上噴散，約 1.6 秒後自己清除。
        /// </summary>
        public static void SpawnFxPlant(Vector3 worldFeetPos)
        {
            InitPlant();
            var leaf = new Color(0.35f, 0.85f, 0.25f);
            var pale = new Color(0.75f, 1f, 0.55f);
            var go = new GameObject("Minion Spawn Fx Plant");
            // 本地 XY 平面 = 地面（本地 +Z 朝下）
            go.transform.SetPositionAndRotation(worldFeetPos + Vector3.up * 0.02f, Quaternion.Euler(90f, 0f, 0f));
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 1.6f;
            var root = go.transform;

            // 翻起的泥土（暗色）＋旋轉的葉形鋸齒光環＋往外擴的光環
            anim.AddRing(root, SpellFx.Circle(32, 1f), true, 0.25f, new Color(0.16f, 0.11f, 0.06f, 0.7f), 0f, 1.0f, 0.3f, 0.45f, false);
            anim.AddRing(root, SpellFx.Zigzag(20, 0.7f, 1f), true, 0.04f, leaf, 0f, 0.9f, 0.45f, 0.38f, true, -160f);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.06f, pale, 0.12f, 0.55f, 0.15f, 0.9f);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.1f, leaf, 0.22f, 0.6f, 0.2f, 1.1f);

            var flash = SpellFx.NewPS(root, "Flash", SpellFx.AddMat(SpellFx.TexSoft), 2, false);
            {
                var m = flash.main;
                m.startLifetime = 0.6f;
                m.startSize = 1f;
                m.startColor = new Color(0.4f, 1f, 0.3f, 0.6f);
                SpellFx.FadeOut(flash);
                SpellFx.SizeCurve(flash, SpellFx.PopCurve);
                flash.Play(); flash.Emit(1);
            }
            var upT = new GameObject("Up").transform;
            upT.SetParent(root, false);
            upT.localRotation = Quaternion.Euler(180f, 0f, 0f);   // +Z 朝上
            var leaves = SpellFx.NewPS(upT, "Leaves", SpellFx.BlendMat(SpellFx.TexLeaf), 22, true);
            {
                var m = leaves.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.55f, 0.12f), new Color(0.55f, 0.85f, 0.25f));
                m.gravityModifier = 0.5f;
                SpellFx.Cone(leaves, 35f, 0.2f);
                SpellFx.FadeOut(leaves);
                SpellFx.Spin(leaves, 6f);
                SpellFx.Drag(leaves, 1.5f);
                SpellFx.Noise(leaves, 0.5f, 1.5f);
                leaves.Play(); leaves.Emit(18);
            }
            var motes = SpellFx.NewPS(upT, "Pollen", SpellFx.AddMat(SpellFx.TexSoft), 24, true);
            {
                var m = motes.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                m.startColor = new ParticleSystem.MinMaxGradient(leaf, pale);
                SpellFx.Cone(motes, 30f, 0.25f);
                SpellFx.FadeInOut(motes);
                SpellFx.Drag(motes, 2f);
                SpellFx.Noise(motes, 0.3f, 2f);
                motes.Play(); motes.Emit(20);
            }
            var dirt = SpellFx.NewPS(upT, "Dirt", SpellFx.BlendMat(SpellFx.TexSoft), 10, true);
            {
                var m = dirt.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.22f, 0.12f, 0.5f), new Color(0.18f, 0.14f, 0.08f, 0.45f));
                SpellFx.Cone(dirt, 40f, 0.25f);
                SpellFx.FadeOut(dirt);
                SpellFx.SizeCurve(dirt, SpellFx.Grow);
                SpellFx.Drag(dirt, 2f);
                dirt.Play(); dirt.Emit(8);
            }

            // 從地裡竄出的嫩芽（一根細莖＋一片捲起的葉），長出 → 停一下 → 縮回地裡
            var sprouts = new GameObject("Sprouts").transform;
            sprouts.SetParent(root, false);
            sprouts.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 回到世界朝向（+Y 朝上）
            var grow = go.AddComponent<PlantGrowAnim>();
            grow.killAt = 0f;   // 由 FxAnim 負責清除
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f + Random.Range(-15f, 15f), a = ang * Mathf.Deg2Rad, r = Random.Range(0.2f, 0.4f);
                var piv = Pivot("Sprout", sprouts, new Vector3(Mathf.Sin(a) * r, -0.02f, Mathf.Cos(a) * r));
                piv.localRotation = Quaternion.Euler(0f, ang, 0f) * Quaternion.Euler(Random.Range(10f, 30f), 0f, 0f);   // 往外倒
                float h = Random.Range(0.18f, 0.32f);
                var green = Color.Lerp(new Color(0.2f, 0.5f, 0.12f), new Color(0.45f, 0.8f, 0.25f), Random.value);
                FxPart(piv, pCone, Vector3.zero, new Vector3(0.03f, h, 0.03f), Vector3.zero, green * 0.8f);
                FxPart(piv, pLeaf, new Vector3(0f, h * 0.7f, 0f), new Vector3(0.09f, 0.08f, 0.17f), new Vector3(-40f, (i & 1) == 0 ? 0f : 180f, 0f), green);
                piv.localScale = Vector3.zero;
                grow.Add(piv, i * 0.03f, 0.25f, 0.55f, 0.45f);
            }
        }

        /// <summary>特效用的一次性實體零件（顏色一次設定好，不進小怪的顏色清單）。</summary>
        static void FxPart(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Vector3 euler, Color c)
        {
            var go = new GameObject("Part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = rockMat;
            NoShadow(r);
            c.a = 1f;
            mpb.Clear();
            mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(mpb);
        }

        // ================================================================ 森林植物小怪（種類 7–12）
        // 跟熔岩小鬼共用材質（rockMat＝有光照的實體、glowMat＝發光），但網格、骨架與動作是另一套：
        //   預備／出手不用 Pose 內插，而是「基本姿勢 ＋ wW×預備偏移 ＋ sW×出手偏移」的疊加（wW、sW 皆 0..1），
        //   每一種在 AnimPlant 裡直接寫出各關節的角度。根鬚類（7、10、12）沒有腿，靠小跳步移動。

        // 植物專用的共用網格（全部 static、一次建立）
        static Mesh pBlob, pStem, pCone, pLeaf, pCap, pSpots, pThorns, pTeeth, pRoots, pVine;

        // 植物骨架
        bool plant;
        Transform neck, jaw, jawU, barrel, hipBL, hipBR, rootT, handL, handR;
        Transform[] petals;
        Vector3 barrelBase;
        float snapW, plantW;   // 出手瞬間的預備程度（讓預備姿勢在出手時快速收掉）、目前的預備程度
        readonly List<float> rockGlows = new List<float>();
        float[] rockG;
        readonly List<Transform> swayT = new List<Transform>();
        readonly List<Quaternion> swayQ = new List<Quaternion>();
        static readonly Color Withered = new Color(0.3f, 0.23f, 0.13f);
        static readonly Color WitherDark = new Color(0.12f, 0.1f, 0.06f);
        static readonly Color PlantFlash = new Color(1f, 0.85f, 0.6f);

        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        static void InitPlant()
        {
            InitMats();
            if (pBlob != null) return;
            var up = Quaternion.Euler(-90f, 0f, 0f);   // Lathe 的 +Z → +Y

            // 圓潤團塊（直徑 1），輕微起伏
            {
                var m = new PMesh(1);
                const int rings = 6;
                var prof = new float[(rings + 1) * 2];
                for (int i = 0; i <= rings; i++)
                {
                    float a = Mathf.PI * i / rings;
                    prof[i * 2] = -Mathf.Cos(a) * 0.5f;
                    prof[i * 2 + 1] = Mathf.Sin(a) * 0.5f;
                }
                m.Lathe(prof, 9, null, 1f, 1f, 0f, null, Vector3.zero, Quaternion.identity,
                    p => p * (1f + 0.05f * Mathf.Sin(p.x * 11f + 1.3f) * Mathf.Sin(p.y * 9f + 0.4f) * Mathf.Sin(p.z * 10f + 2.1f)));
                pBlob = m.Build("PlantBlob", true);
            }
            // 莖：沿 +Y 0..1，底部半徑 0.5 往上收到 0.38，輕微彎曲，兩端封口
            {
                var m = new PMesh(1);
                m.Lathe(new[] { 0f, 0f, 0f, 0.5f, 0.3f, 0.46f, 0.65f, 0.42f, 1f, 0.38f, 1f, 0f }, 7, null, 1f, 1f, 0f, null, Vector3.zero, up,
                    p => { p.x += 0.04f * Mathf.Sin(p.z * 6f); return p; });
                pStem = m.Build("PlantStem", true);
            }
            // 尖刺／獠牙：沿 +Y，底半徑 0.5、長 1
            {
                var m = new PMesh(1);
                m.Lathe(new[] { 0f, 0f, 0f, 0.5f, 1f, 0f }, 5, null, 1f, 1f, 0f, null, Vector3.zero, up);
                pCone = m.Build("PlantCone", true);
            }
            // 葉子：沿 +Z 長 1、寬 1（X），中肋凹摺、邊緣往 +Y 翹、尖端往 -Y 垂；正反兩面
            {
                var m = new PMesh(1);
                const int n = 7;
                for (int i = 0; i < n; i++)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var a = LeafPt(i, 0, n); var b = LeafPt(i + 1, 0, n);
                        var c = LeafPt(i + 1, s, n); var d = LeafPt(i, s, n);
                        m.Quad(a, b, c, d, Color.white);
                        m.Quad(a, d, c, b, Color.white);
                    }
                pLeaf = m.Build("PlantLeaf", true);
            }
            // 菇傘：沿 +Y，底面平、直徑約 1.06、高 0.45
            {
                var m = new PMesh(1);
                m.Lathe(CapProf, 11, null, 1f, 1f, 0f, null, Vector3.zero, up);
                pCap = m.Build("PlantCap", true);
            }
            // 菇傘上的斑點（發光材質，雙面不剔除）：貼在 CapProf 的表面上
            {
                var m = new PMesh(1);
                float[] us = { 0.97f, 0.62f, 0.62f, 0.62f, 0.62f, 0.62f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f };
                float[] ph = { 0f, 10f, 82f, 154f, 226f, 298f, 45f, 97f, 148f, 200f, 251f, 303f, 355f };
                float[] sz = { 0.09f, 0.08f, 0.065f, 0.085f, 0.07f, 0.08f, 0.06f, 0.07f, 0.055f, 0.065f, 0.06f, 0.07f, 0.055f };
                for (int i = 0; i < us.Length; i++) CapSpot(m, us[i], ph[i], sz[i]);
                pSpots = m.Build("PlantSpots", false);
            }
            // 荊棘叢：單位球（半徑 0.5）上半部與後方，往外並往後掃的尖刺
            {
                var m = new PMesh(1);
                const int N = 48;
                for (int i = 0; i < N; i++)
                {
                    float y = 1f - 2f * (i + 0.5f) / N, r = Mathf.Sqrt(1f - y * y), a = i * 2.39996f;
                    var d = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    if (d.y < -0.15f || d.z > 0.65f) continue;
                    float len = 0.3f + 0.12f * Mathf.Abs(Mathf.Sin(i * 7.31f));
                    var b0 = d * 0.42f;
                    Tube(m, new[] { b0, b0 + d * len + new Vector3(0f, 0.03f, -0.1f) }, new[] { 0.055f, 0f }, 4, i * 37f);
                }
                pThorns = m.Build("PlantThorns", true);
            }
            // 一排牙齒：沿著半徑 0.45 的 U 形（前方 ±100°），往 +Y 長、略往內彎
            {
                var m = new PMesh(1);
                const int N = 9;
                for (int i = 0; i < N; i++)
                {
                    float a = (-100f + 200f * i / (N - 1)) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var b0 = dir * 0.45f;
                    float len = i == N / 2 ? 0.8f : (i & 1) == 0 ? 1f : 0.75f;
                    Tube(m, new[] { b0 - Vector3.up * 0.1f, b0 + Vector3.up * len - dir * 0.15f }, new[] { 0.08f, 0f }, 4, 45f);
                }
                pTeeth = m.Build("PlantTeeth", true);
            }
            // 根鬚底座：中央的根團＋五條往外彎到地面的根（半徑 1、高 0.4）
            {
                var m = new PMesh(1);
                m.Lathe(new[] { 0f, 0f, 0f, 0.24f, 0.28f, 0.16f, 0.4f, 0f }, 6, null, 1f, 1f, 0f, null, Vector3.zero, up);
                for (int k = 0; k < 5; k++)
                {
                    float a = (72f * k + 18f) * Mathf.Deg2Rad, w = 0.12f * Mathf.Sin(k * 2.3f + 0.5f);
                    var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    var sd = new Vector3(d.z, 0f, -d.x) * w;
                    Tube(m, new[]
                    {
                        new Vector3(0f, 0.4f, 0f), d * 0.04f + new Vector3(0f, 0.33f, 0f), d * 0.24f + new Vector3(0f, 0.27f, 0f) + sd * 0.3f,
                        d * 0.52f + new Vector3(0f, 0.15f, 0f) + sd, d * 0.8f + new Vector3(0f, 0.05f, 0f) + sd * 0.6f, d * 1f + new Vector3(0f, 0.015f, 0f),
                    }, new[] { 0f, 0.15f, 0.13f, 0.09f, 0.05f, 0f }, 5, k * 20f);
                }
                pRoots = m.Build("PlantRoots", true);
            }
            // 扭曲的藤蔓（四肢、軀幹）：三股螺旋交纏，沿 +Y 0..1，總半徑約 0.35
            {
                var m = new PMesh(1);
                const int n = 10;
                for (int k = 0; k < 3; k++)
                {
                    var pts = new Vector3[n + 3];
                    var rad = new float[n + 3];
                    for (int i = 0; i <= n; i++)
                    {
                        float s = (float)i / n, th = 2f * Mathf.PI * (s * 0.85f + k / 3f);
                        pts[i + 1] = new Vector3(Mathf.Cos(th) * 0.18f, s, Mathf.Sin(th) * 0.18f);
                        rad[i + 1] = 0.17f * (1f - 0.2f * s);
                    }
                    pts[0] = pts[1] - Vector3.up * 0.04f; rad[0] = 0f;
                    pts[n + 2] = pts[n + 1] + Vector3.up * 0.04f; rad[n + 2] = 0f;
                    Tube(m, pts, rad, 5, k * 40f);
                }
                pVine = m.Build("PlantVine", true);
            }
        }

        static readonly float[] CapProf = { 0f, 0f, 0f, 0.5f, 0.05f, 0.53f, 0.14f, 0.5f, 0.25f, 0.42f, 0.35f, 0.3f, 0.42f, 0.16f, 0.45f, 0f };

        static Vector3 LeafPt(int i, int side, int n)
        {
            float z = (float)i / n;
            float w = 0.5f * Mathf.Sin(Mathf.PI * Mathf.Pow(z, 0.75f));
            float yMid = -0.22f * z * z;
            return side == 0 ? new Vector3(0f, yMid, z) : new Vector3(side * w, yMid + 0.3f * w, z);
        }

        /// <summary>在菇傘表面放一個六角形斑點：u＝沿剖面從傘緣（0）到頂點（1），ph＝繞 Y 的角度。</summary>
        static void CapSpot(PMesh m, float u, float phDeg, float size)
        {
            // CapProf 第 2 點（傘緣最外）到最後一點（頂點）的折線
            const int first = 2, last = 7;
            float f = Mathf.Clamp(u, 0f, 0.999f) * (last - first);
            int seg = first + Mathf.FloorToInt(f);
            float t = f - Mathf.Floor(f);
            float h0 = CapProf[seg * 2], r0 = CapProf[seg * 2 + 1], h1 = CapProf[seg * 2 + 2], r1 = CapProf[seg * 2 + 3];
            float h = Mathf.Lerp(h0, h1, t), r = Mathf.Lerp(r0, r1, t);
            var n2 = new Vector2(h1 - h0, -(r1 - r0)).normalized;   // 剖面平面上的外法線（r, h）
            float a = phDeg * Mathf.Deg2Rad;
            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var nrm = (radial * n2.x + Vector3.up * n2.y).normalized;
            var c = radial * r + Vector3.up * h + nrm * 0.012f;
            var e1 = Vector3.Cross(nrm, Mathf.Abs(nrm.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
            var e2 = Vector3.Cross(nrm, e1);
            for (int j = 0; j < 6; j++)
            {
                float b0 = j * Mathf.PI / 3f, b1 = (j + 1) * Mathf.PI / 3f;
                m.Tri(c, c + (e1 * Mathf.Cos(b0) + e2 * Mathf.Sin(b0)) * size, c + (e1 * Mathf.Cos(b1) + e2 * Mathf.Sin(b1)) * size, Color.white);
            }
        }

        /// <summary>沿折線的管子（半徑 0 的端點會封成尖頭）；面朝外，可用於有背面剔除的材質。</summary>
        static void Tube(PMesh m, Vector3[] pts, float[] rad, int sides, float phaseDeg = 0f)
        {
            int n = pts.Length;
            var ring = new Vector3[n * sides];
            for (int i = 0; i < n; i++)
            {
                var t = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                var n1 = Vector3.Cross(t, Mathf.Abs(t.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
                var n2 = Vector3.Cross(t, n1);   // Cross(n1, n2) = t → 跟 Lathe 同樣的繞向（面朝外）
                for (int j = 0; j < sides; j++)
                {
                    float a = (phaseDeg + 360f * j / sides) * Mathf.Deg2Rad;
                    ring[i * sides + j] = pts[i] + (n1 * Mathf.Cos(a) + n2 * Mathf.Sin(a)) * rad[i];
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    var p00 = ring[i * sides + j]; var p01 = ring[i * sides + j1];
                    var p10 = ring[(i + 1) * sides + j]; var p11 = ring[(i + 1) * sides + j1];
                    if (rad[i + 1] > 0f) m.Tri(p00, p11, p10, Color.white);
                    if (rad[i] > 0f) m.Tri(p00, p01, p11, Color.white);
                }
        }

        // ---------------------------------------------------------------- 植物：建立
        Transform Part(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Vector3 euler, Color c, float charge = 0f, bool sway = false)
        {
            var go = new GameObject("Part");
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = rockMat;
            NoShadow(r);
            rockList.Add(r); rockCols.Add(c); rockGlows.Add(charge);
            if (sway) { swayT.Add(t); swayQ.Add(t.localRotation); }
            return t;
        }

        void BuildPlant(int k)
        {
            InitPlant();
            variantId = k;
            kind = k;
            v = Variants[k];
            rockList.Clear(); rockCols.Clear(); rockGlows.Clear(); lavaList.Clear(); hotList.Clear(); eyeList.Clear();
            swayT.Clear(); swayQ.Clear();
            flame = null; flameR = null; ball = null; core = null; boulder = null; tip = null; carryK = 0f; ballGone = 10f;
            neck = jaw = jawU = barrel = hipBL = hipBR = rootT = handL = handR = null; petals = null;
            hipL = hipR = shL = shR = wrL = wrR = null;
            clock = Random.Range(0f, 10f);
            phase = PhaseNone;
            snapW = plantW = 0f;

            body = Pivot("Body", transform, Vector3.zero);
            switch (k)
            {
                case 7: BuildSpitter(); break;
                case 8: BuildCharger(); break;
                case 9: BuildCaster(); break;
                case 10: BuildChomper(); break;
                case 11: BuildShroom(); break;
                default: BuildGunner(); break;
            }

            // ---- 少量花粉／孢子（孢子菇出手時會多噴一團）
            embers = SpellFx.NewPS(spine, "Pollen", SpellFx.AddMat(SpellFx.TexSoft), k == 11 ? 30 : 8, true);
            embers.transform.localPosition = new Vector3(0f, k == 11 ? 0.4f : 0.3f, 0f);
            {
                var m = embers.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
                m.startColor = new ParticleSystem.MinMaxGradient(v.emberA, v.emberB);
                SpellFx.Sphere(embers, 0.3f);
                SpellFx.FadeInOut(embers);
                SpellFx.Rise(embers, 0.12f);
                SpellFx.Noise(embers, 0.15f, 1.5f);
                SpellFx.Rate(embers, dead ? 0f : 3f);
                embers.Play();
            }

            rockR = rockList.ToArray(); rockC = rockCols.ToArray(); rockG = rockGlows.ToArray();
            lavaR = lavaList.ToArray(); hotR = hotList.ToArray(); eyeR = eyeList.ToArray();
            AnimatePlant(0f);
        }

        /// <summary>7 毒液噴花：根鬚＋葉叢底座、兩節彎曲花莖、頂端綠色毒囊＋五片紫色花瓣圍成的大嘴（嘴裡發綠光、蓄著一滴毒液）。</summary>
        void BuildSpitter()
        {
            rootT = Part(body, pRoots, Vector3.zero, new Vector3(0.32f, 0.36f, 0.32f), Vector3.zero, v.rockB);
            Part(body, pBlob, new Vector3(0f, 0.1f, 0f), new Vector3(0.3f, 0.2f, 0.3f), Vector3.zero, v.rockA);
            for (int i = 0; i < 3; i++)
                Part(body, pLeaf, new Vector3(0f, 0.14f, 0f), new Vector3(0.16f, 0.12f, 0.34f), new Vector3(16f, 60f + 120f * i, 0f), v.accent3, 0f, true);
            spine = Pivot("Stem", body, new Vector3(0f, 0.16f, 0f));
            Part(spine, pStem, Vector3.zero, new Vector3(0.1f, 0.37f, 0.1f), Vector3.zero, v.rockA);
            neck = Pivot("Neck", spine, new Vector3(0f, 0.36f, 0f));
            Part(neck, pStem, Vector3.zero, new Vector3(0.082f, 0.33f, 0.082f), Vector3.zero, v.rockA);
            head = Pivot("Head", neck, new Vector3(0f, 0.32f, 0f));
            core = Part(head, pBlob, new Vector3(0f, 0.09f, -0.02f), new Vector3(0.22f, 0.24f, 0.22f), Vector3.zero, v.accent, 0.7f);   // 毒囊（蓄力時鼓脹發光）
            coreBase = core.localScale;
            jaw = Pivot("Mouth", head, new Vector3(0f, 0.13f, 0.08f));
            jaw.localRotation = Quaternion.Euler(-25f, 0f, 0f);   // 嘴朝前上方
            petals = new Transform[5];
            for (int i = 0; i < 5; i++)
            {
                var rz = Quaternion.AngleAxis(72f * i + 36f, Vector3.forward);
                petals[i] = Pivot("Petal", jaw, rz * new Vector3(0f, 0.035f, 0f));
                Part(petals[i], pLeaf, Vector3.zero, new Vector3(0.2f, 0.17f, 0.24f), new Vector3(0f, 0f, 180f), v.accent2);
            }
            Glow(PrimitiveType.Sphere, jaw, new Vector3(0f, 0f, 0.012f), new Vector3(0.12f, 0.12f, 0.05f), lavaList);   // 發光的喉嚨
            tip = Pivot("Venom", jaw, new Vector3(0f, 0f, 0.035f));
            Glow(PrimitiveType.Sphere, tip, Vector3.zero, Vector3.one, hotList);
        }

        /// <summary>8 荊棘衝鋒獸：四腳樹樁野豬。樹皮身體＋背上荊棘叢與苔蘚、切面木頭鼻子、兩根獠牙、兩根樹枝角、尾巴一片葉子。</summary>
        void BuildCharger()
        {
            spine = Pivot("Torso", body, new Vector3(0f, 0.38f, 0f));
            var torsoP = new Vector3(0f, 0.02f, -0.04f); var torsoS = new Vector3(0.56f, 0.46f, 0.84f);
            Part(spine, pBlob, torsoP, torsoS, Vector3.zero, v.rockA);
            Part(spine, pThorns, torsoP, torsoS, Vector3.zero, v.accent2);
            Part(spine, pBlob, new Vector3(0f, 0.2f, -0.08f), new Vector3(0.42f, 0.14f, 0.6f), new Vector3(-4f, 0f, 0f), v.accent3);   // 背上的苔蘚
            Part(spine, pLeaf, new Vector3(0f, 0.12f, -0.42f), new Vector3(0.12f, 0.1f, 0.22f), new Vector3(-35f, 180f, 0f), v.accent3, 0f, true);   // 尾巴
            head = Pivot("Head", spine, new Vector3(0f, 0f, 0.36f));
            Part(head, pBlob, new Vector3(0f, -0.02f, 0.1f), new Vector3(0.38f, 0.32f, 0.34f), Vector3.zero, v.rockA);
            Part(head, pStem, new Vector3(0f, -0.06f, 0.22f), new Vector3(0.22f, 0.08f, 0.18f), new Vector3(90f, 0f, 0f), v.accent);   // 樹樁切面的鼻子
            for (int s = -1; s <= 1; s += 2)
            {
                Part(head, pCone, new Vector3(0.1f * s, -0.1f, 0.22f), new Vector3(0.045f, 0.17f, 0.045f), new Vector3(60f, 0f, -20f * s), v.accent2);   // 獠牙
                Part(head, pCone, new Vector3(0.08f * s, 0.1f, 0.06f), new Vector3(0.045f, 0.2f, 0.045f), new Vector3(-22f, 0f, -28f * s), v.rockB);    // 樹枝角
                Glow(PrimitiveType.Cube, head, new Vector3(0.09f * s, 0.05f, 0.235f), new Vector3(0.065f, 0.024f, 0.03f), eyeList, new Vector3(0f, 0f, 16f * s));
            }
            // 四條短粗的樹根腿（腳底寬）
            const float lx = 0.15f;
            hipL = Pivot("LegFL", body, new Vector3(-lx, 0.28f, 0.18f));
            hipR = Pivot("LegFR", body, new Vector3(lx, 0.28f, 0.18f));
            hipBL = Pivot("LegBL", body, new Vector3(-lx, 0.28f, -0.24f));
            hipBR = Pivot("LegBR", body, new Vector3(lx, 0.28f, -0.24f));
            foreach (var h in new[] { hipL, hipR, hipBL, hipBR })
                Part(h, pStem, new Vector3(0f, -0.29f, 0f), new Vector3(0.17f, 0.31f, 0.17f), Vector3.zero, v.rockB);
        }

        /// <summary>9 藤蔓術士：三股扭曲藤蔓編成的瘦長人形，背後一片葉披風、葉子兜帽、綠色發光眼睛、手上的藤爪與引導光球。</summary>
        void BuildCaster()
        {
            hipL = Pivot("HipL", body, new Vector3(-0.075f, 0.47f, 0f));
            hipR = Pivot("HipR", body, new Vector3(0.075f, 0.47f, 0f));
            Part(hipL, pVine, new Vector3(0f, -0.47f, 0f), new Vector3(0.11f, 0.48f, 0.11f), Vector3.zero, v.rockB);
            Part(hipR, pVine, new Vector3(0f, -0.47f, 0f), new Vector3(0.11f, 0.48f, 0.11f), new Vector3(0f, 120f, 0f), v.rockB);
            spine = Pivot("Spine", body, new Vector3(0f, 0.45f, 0f));
            Part(spine, pVine, new Vector3(0f, -0.03f, 0f), new Vector3(0.26f, 0.44f, 0.2f), new Vector3(0f, 40f, 0f), v.rockA);
            Part(spine, pLeaf, new Vector3(0f, 0.38f, -0.08f), new Vector3(0.3f, 0.12f, 0.46f), new Vector3(100f, 0f, 0f), v.accent3, 0f, true);   // 葉披風
            head = Pivot("Head", spine, new Vector3(0f, 0.41f, 0.02f));
            Part(head, pBlob, new Vector3(0f, 0.08f, 0f), new Vector3(0.15f, 0.17f, 0.15f), Vector3.zero, v.accent2);
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Sphere, head, new Vector3(0.035f * s, 0.09f, 0.068f), new Vector3(0.042f, 0.024f, 0.02f), eyeList, new Vector3(0f, 0f, 14f * s));
            // 兜帽：頭頂往前蓋的大葉＋兩側包住臉的葉
            Part(head, pLeaf, new Vector3(0f, 0.13f, -0.11f), new Vector3(0.26f, 0.14f, 0.3f), new Vector3(-22f, 0f, 180f), v.accent);
            for (int s = -1; s <= 1; s += 2)
                Part(head, pLeaf, new Vector3(0.085f * s, 0.02f, -0.07f), new Vector3(0.14f, 0.1f, 0.26f), new Vector3(-62f, 14f * s, 90f * s), v.accent);
            // 手臂：藤蔓＋往下的藤爪＋引導光球
            shL = Pivot("ShoulderL", spine, new Vector3(-0.1f, 0.35f, 0f));
            shR = Pivot("ShoulderR", spine, new Vector3(0.1f, 0.35f, 0f));
            foreach (var sh in new[] { shL, shR })
            {
                Part(sh, pVine, new Vector3(0f, -0.38f, 0f), new Vector3(0.075f, 0.38f, 0.075f), Vector3.zero, v.rockA);
                var wr = Pivot("Wrist", sh, new Vector3(0f, -0.38f, 0f));
                Part(wr, pCone, new Vector3(0f, 0.02f, 0f), new Vector3(0.06f, 0.12f, 0.06f), new Vector3(180f, 0f, 0f), v.rockB);
                var hg = Pivot("HandGlow", wr, new Vector3(0f, -0.05f, 0.02f));
                Glow(PrimitiveType.Sphere, hg, Vector3.zero, Vector3.one, lavaList);
                if (sh == shL) { wrL = wr; handL = hg; } else { wrR = wr; handR = hg; }
            }
        }

        /// <summary>10 食人花：根鬚腳、兩片大葉、粗的兩節花莖，頂端上下兩片顎（外綠內紅、尖牙一圈）、顎上一對黃眼、頸後兩片葉領。</summary>
        void BuildChomper()
        {
            rootT = Part(body, pRoots, Vector3.zero, new Vector3(0.36f, 0.4f, 0.36f), Vector3.zero, v.rockB);
            for (int s = -1; s <= 1; s += 2)
                Part(body, pLeaf, new Vector3(0.06f * s, 0.12f, 0f), new Vector3(0.17f, 0.12f, 0.38f), new Vector3(22f, 90f * s, 0f), v.accent3, 0f, true);
            spine = Pivot("Stem", body, new Vector3(0f, 0.1f, 0f));
            Part(spine, pStem, Vector3.zero, new Vector3(0.13f, 0.43f, 0.13f), Vector3.zero, v.rockA);
            neck = Pivot("Neck", spine, new Vector3(0f, 0.42f, 0f));
            Part(neck, pStem, Vector3.zero, new Vector3(0.11f, 0.31f, 0.11f), Vector3.zero, v.rockA);
            head = Pivot("Head", neck, new Vector3(0f, 0.3f, 0f));
            for (int s = -1; s <= 1; s += 2)
                Part(head, pLeaf, new Vector3(0.07f * s, 0f, -0.03f), new Vector3(0.15f, 0.1f, 0.22f), new Vector3(-10f, 140f * s, 0f), v.accent3, 0f, true);   // 葉領
            Glow(PrimitiveType.Sphere, head, new Vector3(0f, 0f, 0.13f), new Vector3(0.2f, 0.05f, 0.24f), lavaList);   // 喉嚨的光
            jawU = Pivot("JawUpper", head, new Vector3(0f, 0.02f, -0.02f));
            Part(jawU, pBlob, new Vector3(0f, 0.07f, 0.17f), new Vector3(0.36f, 0.2f, 0.4f), Vector3.zero, v.accent);
            Part(jawU, pBlob, new Vector3(0f, 0.025f, 0.17f), new Vector3(0.32f, 0.07f, 0.36f), Vector3.zero, v.accent2, 0.4f);
            Part(jawU, pTeeth, new Vector3(0f, 0.03f, 0.17f), new Vector3(0.32f, 0.07f, 0.38f), new Vector3(0f, 0f, 180f), new Color(0.93f, 0.9f, 0.74f));
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Sphere, jawU, new Vector3(0.075f * s, 0.16f, 0.1f), new Vector3(0.065f, 0.04f, 0.05f), eyeList, new Vector3(0f, 0f, 18f * s));
            jaw = Pivot("JawLower", head, new Vector3(0f, -0.02f, -0.02f));
            Part(jaw, pBlob, new Vector3(0f, -0.06f, 0.16f), new Vector3(0.32f, 0.16f, 0.38f), Vector3.zero, v.accent);
            Part(jaw, pBlob, new Vector3(0f, -0.02f, 0.16f), new Vector3(0.29f, 0.06f, 0.33f), Vector3.zero, v.accent2, 0.4f);
            Part(jaw, pTeeth, new Vector3(0f, -0.025f, 0.16f), new Vector3(0.3f, 0.065f, 0.36f), Vector3.zero, new Color(0.93f, 0.9f, 0.74f));
        }

        /// <summary>11 孢子菇：兩條短胖腿、粗菇柄（臉上一對發光眼）、短短的小手、菌環、紫色菇傘＋發光斑點、傘下菌褶。</summary>
        void BuildShroom()
        {
            hipL = Pivot("HipL", body, new Vector3(-0.09f, 0.15f, 0f));
            hipR = Pivot("HipR", body, new Vector3(0.09f, 0.15f, 0f));
            Part(hipL, pBlob, new Vector3(0f, -0.07f, 0.02f), new Vector3(0.13f, 0.17f, 0.16f), Vector3.zero, v.rockA);
            Part(hipR, pBlob, new Vector3(0f, -0.07f, 0.02f), new Vector3(0.13f, 0.17f, 0.16f), Vector3.zero, v.rockA);
            spine = Pivot("Stalk", body, new Vector3(0f, 0.12f, 0f));
            Part(spine, pStem, Vector3.zero, new Vector3(0.3f, 0.37f, 0.3f), Vector3.zero, v.rockA);
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Sphere, spine, new Vector3(0.05f * s, 0.21f, 0.125f), new Vector3(0.05f, 0.07f, 0.025f), eyeList, new Vector3(0f, 0f, -10f * s));
            Part(spine, pBlob, new Vector3(0f, 0.28f, 0f), new Vector3(0.33f, 0.04f, 0.33f), Vector3.zero, v.rockB);   // 菌環
            shL = Pivot("ArmL", spine, new Vector3(-0.13f, 0.2f, 0f));
            shR = Pivot("ArmR", spine, new Vector3(0.13f, 0.2f, 0f));
            Part(shL, pBlob, new Vector3(0f, -0.05f, 0f), new Vector3(0.08f, 0.13f, 0.08f), Vector3.zero, v.rockA);
            Part(shR, pBlob, new Vector3(0f, -0.05f, 0f), new Vector3(0.08f, 0.13f, 0.08f), Vector3.zero, v.rockA);
            head = Pivot("Cap", spine, new Vector3(0f, 0.34f, 0f));
            Part(head, pCone, new Vector3(0f, 0.02f, 0f), new Vector3(0.68f, 0.1f, 0.68f), new Vector3(180f, 0f, 0f), v.rockB);   // 菌褶
            Part(head, pCap, new Vector3(0f, 0.02f, 0f), new Vector3(0.76f, 0.7f, 0.76f), Vector3.zero, v.accent, 0.25f);
            var sp = new GameObject("Spots");
            sp.transform.SetParent(head, false);
            sp.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            sp.transform.localScale = new Vector3(0.76f, 0.7f, 0.76f);
            sp.AddComponent<MeshFilter>().sharedMesh = pSpots;
            var spr = sp.AddComponent<MeshRenderer>();
            spr.sharedMaterial = glowMat;
            NoShadow(spr);
            lavaList.Add(spr);
        }

        /// <summary>12 種子槍手：根鬚＋圓球莖身、三片尖葉，上面架著種莢膛（兩顆種子、頂上一片瞄準葉）、往前伸的長莢管＋喇叭口＋管口光、一對眼睛。</summary>
        void BuildGunner()
        {
            rootT = Part(body, pRoots, Vector3.zero, new Vector3(0.34f, 0.4f, 0.34f), Vector3.zero, v.rockB);
            spine = Pivot("Bulb", body, new Vector3(0f, 0.1f, 0f));
            Part(spine, pBlob, new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.42f, 0.4f), Vector3.zero, v.rockA);
            for (int i = 0; i < 3; i++)
            {
                float yaw = 60f + 120f * i, a = yaw * Mathf.Deg2Rad;
                Part(spine, pLeaf, new Vector3(Mathf.Sin(a) * 0.1f, 0.08f, Mathf.Cos(a) * 0.1f), new Vector3(0.13f, 0.1f, 0.34f), new Vector3(-48f, yaw, 0f), v.accent3, 0f, true);
            }
            head = Pivot("Turret", spine, new Vector3(0f, 0.44f, 0f));
            core = Part(head, pBlob, new Vector3(0f, 0.1f, -0.02f), new Vector3(0.32f, 0.3f, 0.36f), Vector3.zero, v.accent, 0.5f);   // 種莢膛
            coreBase = core.localScale;
            for (int s = -1; s <= 1; s += 2)
            {
                Part(head, pBlob, new Vector3(0.07f * s, 0.23f, -0.07f), new Vector3(0.09f, 0.07f, 0.11f), Vector3.zero, v.accent2);   // 種子
                Glow(PrimitiveType.Sphere, head, new Vector3(0.085f * s, 0.17f, 0.115f), new Vector3(0.06f, 0.035f, 0.03f), eyeList, new Vector3(0f, 0f, 14f * s));
            }
            Part(head, pLeaf, new Vector3(0f, 0.24f, -0.02f), new Vector3(0.08f, 0.06f, 0.18f), new Vector3(-70f, 0f, 0f), v.accent3, 0f, true);   // 頂上的瞄準葉
            barrelBase = new Vector3(0f, 0.11f, 0.1f);
            barrel = Pivot("Barrel", head, barrelBase);
            Part(barrel, pStem, Vector3.zero, new Vector3(0.12f, 0.38f, 0.12f), new Vector3(90f, 0f, 0f), v.accent2);
            Part(barrel, pCone, new Vector3(0f, 0f, 0.43f), new Vector3(0.17f, 0.09f, 0.17f), new Vector3(-90f, 0f, 0f), v.accent3, 0.6f);   // 喇叭口（像花萼）
            tip = Pivot("Muzzle", barrel, new Vector3(0f, 0f, 0.43f));
            Glow(PrimitiveType.Sphere, tip, Vector3.zero, Vector3.one, hotList);
        }

        // ---------------------------------------------------------------- 植物：動作
        void PuffSpores()
        {
            if (embers == null || head == null) return;
            var ep = new ParticleSystem.EmitParams();
            var c = head.position;
            for (int i = 0; i < 16; i++)
            {
                var d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.5f + 0.15f;
                ep.position = c + new Vector3(d.x, 0f, d.z) * 0.22f + Vector3.up * 0.08f;
                ep.velocity = d.normalized * Random.Range(0.4f, 1.1f);
                ep.startSize = Random.Range(0.03f, 0.065f);
                ep.startLifetime = Random.Range(0.6f, 1.2f);
                ep.startColor = Color.Lerp(v.emberA, v.emberB, Random.value);
                embers.Emit(ep, 1);
            }
        }

        void AnimatePlant(float dt)
        {
            clock += dt;
            if (clock > 3600f) clock -= 3600f;
            float hitK = 0f;
            if (hitT > 0f) { hitT -= dt; hitK = Mathf.Clamp01(hitT / HitDur); }
            if (dead) deadT += dt;
            ballGone += dt;

            // ---- 步伐（根鬚類：每半個循環跳一下）
            float speed = dead ? 0f : Mathf.Clamp(MoveSpeed, 0f, 4f);
            bool acting = phase != PhaseNone;
            walkAmt = Mathf.MoveTowards(walkAmt, acting ? 0f : Mathf.Clamp01(speed / 0.3f), dt * 6f);
            if (speed > 0.01f && !acting) walkPhase += dt * Mathf.PI * 2f * Mathf.Max(speed, 0.3f) / v.stride * v.step;
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 6f);
            if (walkPhase > 1000f) walkPhase -= Mathf.PI * 300f;
            float sinP = Mathf.Sin(walkPhase), cosP = Mathf.Cos(walkPhase);

            twitchT -= dt;
            if (twitchT <= 0f) { twitchT = Random.Range(0.6f, 1.8f) * (kind == 8 ? 1.6f : 1f); lookTarget = Random.Range(-30f, 30f); }
            lookY = Mathf.MoveTowards(lookY, lookTarget * (1f - walkAmt), dt * (kind == 12 ? 160f : 70f));
            float breath = Mathf.Sin(clock * 2.4f);

            // ---- 預備／出手的權重
            float wW = 0f, sW = 0f;
            if (phase == PhaseWindup)
            {
                atkT += dt;
                wW = Smooth01(atkT / atkDur);
            }
            else if (phase == PhaseStrike)
            {
                atkT += dt;
                if (atkT < StrikeIn) { float k = atkT / StrikeIn; sW = 1f - (1f - k) * (1f - k); }
                else if (atkT < StrikeIn + StrikeHold) sW = 1f;
                else if (atkT < StrikeIn + StrikeHold + StrikeOut) sW = 1f - Smooth01((atkT - StrikeIn - StrikeHold) / StrikeOut);
                else phase = PhaseNone;
                wW = snapW * (1f - Mathf.Clamp01(atkT / StrikeIn));
            }
            plantW = wW;
            float deadE = dead ? Smooth01(deadT / DieDur) : 0f;

            pulsePh += dt * (kind == 11 ? Mathf.Lerp(4f, 24f, wW * wW) : 4f + 10f * wW);
            if (pulsePh > 1000f) pulsePh -= Mathf.PI * 300f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulsePh);
            float glow = Mathf.Max(wW, sW * 0.8f);
            float hop = Mathf.Abs(sinP) * walkAmt;          // 根鬚類的小跳
            float sway = sinP * walkAmt;
            float shrink = 1f - 0.45f * deadE;
            float sink = 0.25f * deadE * deadE;

            switch (kind)
            {
                case 7:   // 毒液噴花
                {
                    float sq = walkAmt * 0.08f * (1f - Mathf.Abs(sinP));
                    body.localPosition = new Vector3(0f, hop * v.bob - sink, 0f);
                    body.localRotation = Quaternion.Euler(walkAmt * 5f, 0f, sway * 4f);
                    body.localScale = new Vector3(1f + sq * 0.5f, 1f - sq, 1f + sq * 0.5f) * shrink;
                    rootT.localRotation = Quaternion.Euler(0f, sinP * 10f * walkAmt, 0f);
                    spine.localRotation = Quaternion.Euler(6f + breath * 2f - 18f * wW + 18f * sW - 15f * hitK + 45f * deadE, lookY * 0.3f, sway * 6f);
                    neck.localRotation = Quaternion.Euler(14f + breath * 3f - 25f * wW + 20f * sW - 12f * hitK + 40f * deadE, lookY * 0.5f, -sway * 8f + 15f * deadE);
                    float trem = Mathf.Sin(clock * 38f) * 2.5f * wW;
                    head.localRotation = Quaternion.Euler(-20f - 25f * wW - 60f * sW + 25f * hitK + 35f * deadE + trem, lookY * 0.3f, trem);
                    // 毒囊：預備時鼓脹（一抽一抽），噴出時一縮
                    float swell = 1f + 0.35f * wW * (0.85f + 0.15f * Mathf.Sin(clock * 14f)) - 0.22f * sW + 0.03f * breath;
                    core.localScale = coreBase * (swell * (1f - 0.3f * deadE));
                    // 花瓣：平常半開，預備時稍微收攏，噴出時大開，死亡時垂下
                    float open = 55f + 4f * breath - 18f * wW + 30f * sW + 10f * hitK + 45f * deadE;
                    for (int i = 0; i < petals.Length; i++)
                    {
                        float fl = open + 5f * Mathf.Sin(clock * 3f + i * 1.9f);
                        petals[i].localRotation = Quaternion.AngleAxis(72f * i + 36f, Vector3.forward) * Quaternion.Euler(-fl, 0f, 0f);
                    }
                    float regrow = Mathf.Clamp01((ballGone - 0.3f) / 0.5f);
                    tip.localScale = Vector3.one * (0.045f * regrow * (0.5f + 1.1f * wW + 0.1f * Mathf.Sin(clock * 17f)) * (1f - deadE));
                    break;
                }
                case 8:   // 荊棘衝鋒獸
                {
                    float bob = walkAmt * v.bob * Mathf.Abs(cosP) - walkAmt * 0.02f * Mathf.Pow(1f - Mathf.Abs(cosP), 4f);
                    body.localPosition = new Vector3(0f, bob - 0.05f * wW - 0.02f * sW - 0.12f * deadE * deadE, -0.06f * wW + 0.28f * sW);
                    body.localRotation = Quaternion.Euler(0f, 0f, -cosP * 3f * walkAmt + 80f * deadE);
                    body.localScale = Vector3.one * (1f - 0.3f * deadE);
                    spine.localRotation = Quaternion.Euler(breath * 1.5f + 7f * wW + 10f * sW - 10f * hitK + 8f * deadE, sinP * 4f * walkAmt + lookY * 0.15f, 0f);
                    head.localRotation = Quaternion.Euler(5f + 28f * wW + 6f * sW - 18f * hitK + 25f * deadE + breath * 2f, lookY * 0.6f * (1f - wW), 0f);
                    float sw = v.legSwing * walkAmt;
                    float paw = wW * (22f * Mathf.Sin(clock * 13f) - 8f);   // 右前腳刨地
                    float splay = 5f * wW + 25f * deadE;
                    hipL.localRotation = Quaternion.Euler(sinP * sw - 40f * sW - 10f * wW, 0f, -splay);
                    hipR.localRotation = Quaternion.Euler(-sinP * sw - 40f * sW + paw, 0f, splay);
                    hipBL.localRotation = Quaternion.Euler(-sinP * sw + 40f * sW + 8f * wW, 0f, -splay);
                    hipBR.localRotation = Quaternion.Euler(sinP * sw + 40f * sW + 8f * wW, 0f, splay);
                    break;
                }
                case 9:   // 藤蔓術士
                {
                    float crouch = 0.03f * wW + 0.04f * sW + 0.03f * hitK;
                    float bob = walkAmt * v.bob * Mathf.Abs(cosP);
                    body.localPosition = new Vector3(0f, -crouch + bob - 0.3f * deadE * deadE, 0f);
                    body.localRotation = Quaternion.Euler(0f, 0f, -cosP * 5f * walkAmt);
                    body.localScale = Vector3.one * (1f - 0.4f * deadE);
                    float legSwing = v.legSwing * walkAmt;
                    hipL.localRotation = Quaternion.Euler(sinP * legSwing - crouch * 75f, 0f, -3f);
                    hipR.localRotation = Quaternion.Euler(-sinP * legSwing - crouch * 75f, 0f, 3f);
                    spine.localRotation = Quaternion.Euler(6f + breath * 2f + walkAmt * 6f - 16f * wW + 22f * sW - 20f * hitK + 70f * deadE, sinP * 6f * walkAmt + lookY * 0.2f, 0f);
                    head.localRotation = Quaternion.Euler(-4f - 12f * wW - 8f * sW - 12f * hitK + 25f * deadE, lookY * 0.6f, 0f);
                    // 手臂：平常垂著擺動；預備＝高舉過頭（V 字）引導、微微顫抖；出手＝雙手往前推
                    float swingA = sinP * 25f * walkAmt;
                    float trem = Mathf.Sin(clock * 33f) * 3f * wW;
                    var baseA = new Vector3(-12f + breath * 2f, 0f, 10f + breath * 3f);
                    var wind = new Vector3(-153f + trem, 0f, 18f);
                    var thrust = new Vector3(-78f, 6f, -6f);
                    var deadA = new Vector3(-50f, 0f, 30f);
                    var aL = baseA + wind * wW + thrust * sW; aL.x += swingA;
                    var aR = baseA + wind * wW + thrust * sW; aR.x -= swingA;
                    aL = Vector3.Lerp(aL, deadA, deadE); aR = Vector3.Lerp(aR, deadA, deadE);
                    shL.localRotation = Quaternion.Euler(aL.x, -aL.y, -aL.z);
                    shR.localRotation = Quaternion.Euler(aR.x, aR.y, aR.z);
                    float wr = -15f + 5f * wW + 35f * sW;
                    wrL.localRotation = Quaternion.Euler(wr, 0f, 0f);
                    wrR.localRotation = Quaternion.Euler(wr, 0f, 0f);
                    float hs = 0.06f * (1f + 1.4f * wW * (0.85f + 0.15f * Mathf.Sin(clock * 21f)) + 0.6f * sW + 0.1f * Mathf.Sin(clock * 5f)) * (1f - deadE);
                    handL.localScale = handR.localScale = Vector3.one * hs;
                    break;
                }
                case 10:  // 食人花
                {
                    float sq = walkAmt * 0.07f * (1f - Mathf.Abs(sinP));
                    body.localPosition = new Vector3(0f, hop * v.bob - sink, 0.12f * sW);
                    body.localRotation = Quaternion.Euler(walkAmt * 4f, 0f, sway * 4f);
                    body.localScale = new Vector3(1f + sq * 0.5f, 1f - sq, 1f + sq * 0.5f) * shrink;
                    rootT.localRotation = Quaternion.Euler(0f, sinP * 10f * walkAmt, 0f);
                    spine.localRotation = Quaternion.Euler(-10f + breath * 2f - 8f * wW + 10f * sW - 12f * hitK + 45f * deadE, lookY * 0.3f, sway * 5f);
                    neck.localRotation = Quaternion.Euler(35f + breath * 2f - 12f * wW + 20f * sW - 10f * hitK + 30f * deadE, lookY * 0.5f, -sway * 6f);
                    float trem = Mathf.Sin(clock * 30f) * 2f * wW;
                    head.localRotation = Quaternion.Euler(-25f - 8f * wW - 10f * sW + 10f * deadE + trem, lookY * 0.3f, 0f);
                    // 下顎：平常微張（偶爾嚼一下），預備＝張到最大，出手＝猛然咬合
                    float chew = Mathf.Max(0f, Mathf.Sin(clock * 1.7f));
                    float open = 0.12f + 0.15f * chew * chew * chew * chew;
                    open = Mathf.Lerp(open, 1f + 0.05f * Mathf.Sin(clock * 30f), wW);
                    open *= 1f - sW;
                    open = Mathf.Max(open, 0.35f * hitK);
                    open = Mathf.Lerp(open, 0.45f, deadE);
                    jawU.localRotation = Quaternion.Euler(-40f * open + 3f * sW, 0f, 0f);
                    jaw.localRotation = Quaternion.Euler(32f * open, 0f, 0f);
                    break;
                }
                case 11:  // 孢子菇
                {
                    float crouch = 0.04f * wW + 0.05f * sW + 0.03f * hitK;
                    float bob = walkAmt * v.bob * Mathf.Abs(cosP);
                    body.localPosition = new Vector3(0f, -crouch + bob - 0.2f * deadE * deadE, 0f);
                    body.localRotation = Quaternion.Euler(0f, 0f, -cosP * 10f * walkAmt);
                    body.localScale = Vector3.one * (1f - 0.4f * deadE);
                    float legSwing = v.legSwing * walkAmt;
                    hipL.localRotation = Quaternion.Euler(sinP * legSwing, 0f, -4f);
                    hipR.localRotation = Quaternion.Euler(-sinP * legSwing, 0f, 4f);
                    spine.localRotation = Quaternion.Euler(breath * 2f + walkAmt * 4f - 6f * wW + 8f * sW - 15f * hitK + 60f * deadE, lookY * 0.25f, cosP * 4f * walkAmt);
                    spine.localScale = new Vector3(1f + 0.1f * sW, 1f - 0.05f * wW - 0.15f * sW, 1f + 0.1f * sW);
                    float flap = Mathf.Sin(clock * 20f) * 12f * wW;
                    shL.localRotation = Quaternion.Euler(-sinP * 30f * walkAmt, 0f, -(20f + 30f * wW + flap + 30f * sW));
                    shR.localRotation = Quaternion.Euler(sinP * 30f * walkAmt, 0f, 20f + 30f * wW + flap + 30f * sW);
                    // 菇傘：預備時一鼓一鼓地膨脹，出手時壓扁
                    float puff = 1f + 0.24f * wW * (0.75f + 0.25f * pulse) + 0.025f * breath;
                    head.localScale = new Vector3(puff * (1f + 0.22f * sW), puff * (1f - 0.35f * sW), puff * (1f + 0.22f * sW)) * (1f - 0.2f * deadE);
                    head.localRotation = Quaternion.Euler(-8f * wW + 6f * hitK, lookY * 0.4f, -cosP * 4f * walkAmt + 20f * deadE);
                    break;
                }
                default:  // 12 種子槍手
                {
                    float sq = walkAmt * 0.07f * (1f - Mathf.Abs(sinP));
                    body.localPosition = new Vector3(0f, hop * v.bob - sink, 0f);
                    body.localRotation = Quaternion.Euler(walkAmt * 5f, 0f, sway * 4f);
                    body.localScale = new Vector3(1f + sq * 0.5f, 1f - sq, 1f + sq * 0.5f) * shrink;
                    rootT.localRotation = Quaternion.Euler(0f, sinP * 10f * walkAmt, 0f);
                    spine.localRotation = Quaternion.Euler(breath * 1.5f + walkAmt * 4f - 7f * sW - 10f * hitK + 50f * deadE, 0f, sway * 5f);
                    float aim = 1f - Mathf.Max(wW, sW);
                    head.localRotation = Quaternion.Euler(-3f + breath - 10f * wW - 18f * sW + 10f * hitK + 35f * deadE, lookY * aim, 0f);
                    core.localScale = coreBase * ((1f + 0.24f * wW * (0.8f + 0.2f * pulse) - 0.12f * sW + 0.02f * breath) * (1f - 0.3f * deadE));
                    barrel.localPosition = barrelBase + new Vector3(0f, 0f, 0.02f * wW - 0.09f * sW);
                    barrel.localScale = new Vector3(1f + 0.1f * wW, 1f + 0.1f * wW, 1f - 0.12f * sW);
                    float fl = phase == PhaseStrike && atkT < 0.12f ? 1f - atkT / 0.12f : 0f;
                    tip.localScale = Vector3.one * ((0.03f + 0.09f * wW * (0.85f + 0.15f * Mathf.Sin(clock * 30f)) + 0.1f * fl) * (1f - deadE));
                    break;
                }
            }

            // ---- 葉子隨風輕擺（走路、被打時擺得更大；死亡時垂下）
            for (int i = 0; i < swayT.Count; i++)
            {
                float a = Mathf.Sin(clock * 2.1f + i * 1.7f) * (3f + 8f * walkAmt) + 15f * hitK + 35f * deadE;
                swayT[i].localRotation = swayQ[i] * Quaternion.Euler(a, 0f, Mathf.Sin(clock * 1.3f + i) * 3f);
            }

            UpdatePlantColors(hitK, deadE, pulse, glow);
        }

        void UpdatePlantColors(float hitK, float deadE, float pulse, float g)
        {
            var lava = Color.Lerp(v.lavaDim, v.lava, 0.55f + 0.45f * pulse);
            lava = Color.Lerp(lava, v.hot, Mathf.Clamp01(g * 0.8f + hitK * 0.5f));
            lava = Color.Lerp(lava, WitherDark, deadE);
            var hot = Color.Lerp(v.hot, Color.white, Mathf.Clamp01(0.3f * pulse + 0.5f * g));
            hot = Color.Lerp(hot, WitherDark, deadE);
            var eye = Color.Lerp(Color.Lerp(v.eye, Color.white, g * 0.5f), WitherDark, Mathf.Clamp01(deadE * 1.5f));

            mpb.Clear();
            mpb.SetColor(ColorId, lava);
            for (int i = 0; i < lavaR.Length; i++) lavaR[i].SetPropertyBlock(mpb);
            mpb.SetColor(ColorId, hot);
            for (int i = 0; i < hotR.Length; i++) hotR[i].SetPropertyBlock(mpb);
            mpb.SetColor(ColorId, eye);
            for (int i = 0; i < eyeR.Length; i++) eyeR[i].SetPropertyBlock(mpb);
            float charge = Mathf.Clamp01(g * (0.55f + 0.45f * pulse));
            for (int i = 0; i < rockR.Length; i++)
            {
                var c = rockC[i];
                if (rockG[i] > 0f) c = Color.Lerp(c, v.lava, rockG[i] * charge);
                c = Color.Lerp(c, PlantFlash, hitK * 0.6f);
                c = Color.Lerp(c, Withered, deadE * 0.85f);
                mpb.SetColor(ColorId, c);
                rockR[i].SetPropertyBlock(mpb);
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
