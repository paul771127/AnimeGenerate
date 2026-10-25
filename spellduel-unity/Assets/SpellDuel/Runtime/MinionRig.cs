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
    ///   海洋小怪（另一套外形／動作，見「海洋小怪」區段；飛行／漂浮的高度、跳躍弧線由呼叫者移動 transform）：
    ///    13 跳躍魚：約 0.9 m 長的尖牙梭魚，藍銀色身體＋深色背、紅眼；本地原點＝肚子正中央。待機／移動＝身體擺動＋尾巴拍打；
    ///       預備＝身體像彈簧一樣弓起（頭尾上翹），出手＝身體打直、大嘴張開（躍出水面飛撲中）。
    ///    14 俯衝海鳥：翼展約 1.4 m 的兇猛賊鷗，鉤狀喙；本地原點＝身體中心。永遠在拍翅飛行；預備＝雙翅高舉、張嘴尖叫，出手＝雙翅往後收的俯衝姿勢、利爪往前。
    ///    15 騎魚泡泡怪：約 1.2 m，小魚人騎在圓滾滾的大魚坐騎上（坐騎身體中心約離地 0.4 m，原點＝水面／腳底），手拿海螺號角；
    ///       預備＝舉起海螺、雙頰鼓起，出手＝海螺往前一推吹出一串泡泡。坐騎的尾巴與鰭一直擺動。
    ///    16 寄居蟹：約 0.8 m 高、寬扁，背著帶刺的大螺旋殼、兩隻螯（右螯特大）；預備＝高舉大螯張開，出手＝往下猛砸並夾緊。
    ///    17 雷電水母：約 0.7 m 的半透明紫青色傘＋約 0.8 m 的觸手；本地原點＝傘的中心（漂浮）。待機＝傘一縮一放、觸手擺動；
    ///       預備＝越來越亮、劈啪冒電花，出手＝一次強烈的放電閃光。
    ///    18 河豚：約 0.6 m 的圓魚、全身短刺、大眼睛；本地原點＝身體中心（漂浮）。預備＝吸水鼓成 1.6 倍大的刺球（刺伸長），出手＝瞬間洩氣抖一下（射出尖刺）。
    ///   效能：每隻 ≤ 18 個 Renderer（含 1 個火星／花粉粒子）；材質、網格全部 static 共用，
    ///   各自的顏色／閃色／蓄力發亮／死亡變暗都用 MaterialPropertyBlock（不複製材質、每幀不配置記憶體）。
    /// </summary>
    public partial class MinionRig : MonoBehaviour
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
            rig.Build(0);rig.FitHumanoidShoulders();
            BlobShadow.Attach(go.transform, 0.3f, 0f, true);   // 腳下的接觸陰影（飄在空中的投到地板；大小在 SetVariant 依體型調整）
            return rig;
        }

        /// <summary>
        /// 切換種類（0 近戰、1 火焰射手、2 坦克、3 自爆、4 岩石投擲者、5 熔岩狙擊手、6 虛空術士；
        /// 森林：7 毒液噴花、8 荊棘衝鋒獸、9 藤蔓術士、10 食人花、11 孢子菇、12 種子槍手；
        /// 海洋：13 跳躍魚、14 俯衝海鳥、15 騎魚泡泡怪、16 寄居蟹、17 雷電水母、18 河豚），會重建外觀；請在 Create() 之後立刻呼叫。
        /// </summary>
        /// <summary>
        /// 依關卡主題加裝飾（第十一輪美術：3＝角鬥場的黑晶項圈與鐵鍊、4＝黃土山道的沙塵與頭巾）；其他主題不加。
        /// 每次 SetVariant 之後呼叫一次（換外觀會重建身體）。目前是空的，等美術實作。
        /// </summary>
        public void SetDress(int theme)
        {
            ApplyDress(theme);
            dressTheme = theme;
        }
        int dressTheme = -1;

        public void SetVariant(int kind)
        {
            kind = Mathf.Clamp(kind, 0, 22);
            if (kind == variantId && body != null) return;
            if (body != null)
            {
                body.gameObject.SetActive(false);
                Destroy(body.gameObject);
                body = null;
            }
            dressRoot=null;
            Build(kind);FitHumanoidShoulders();
            BlobShadow.Attach(transform, 0.3f * (kind >= 19 ? 1.8f : Variants[kind].size), 0f, true);
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
            if (sea) SeaAttack();
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
                rockA = new Color(0.56f, 0.32f, 0.22f), rockB = new Color(0.36f, 0.2f, 0.28f),
                lava = new Color(1f, 0.38f, 0.06f), lavaDim = new Color(0.6f, 0.1f, 0.02f), hot = new Color(1f, 0.9f, 0.55f), eye = new Color(1f, 0.8f, 0.3f),
                emberA = new Color(1f, 0.45f, 0.08f), emberB = new Color(1f, 0.8f, 0.35f),
            },
            new Variant   // 1 火焰射手（青白火焰）
            {
                size = 0.9f, stride = 0.5f, legSwing = 34f, bob = 0.03f, step = 1f,
                rockA = new Color(0.34f, 0.48f, 0.68f), rockB = new Color(0.25f, 0.3f, 0.5f),
                lava = new Color(0.35f, 0.85f, 1f), lavaDim = new Color(0.08f, 0.3f, 0.6f), hot = new Color(0.88f, 1f, 1f), eye = new Color(0.75f, 0.97f, 1f),
                emberA = new Color(0.3f, 0.8f, 1f), emberB = new Color(0.85f, 1f, 1f),
            },
            new Variant   // 2 岩石衝鋒獸（暗紅熔岩、沉重）
            {
                size = 1.5f, stride = 0.6f, legSwing = 26f, bob = 0.05f, step = 0.8f,
                rockA = new Color(0.66f, 0.28f, 0.2f), rockB = new Color(0.42f, 0.2f, 0.28f),
                lava = new Color(0.7f, 0.12f, 0.04f), lavaDim = new Color(0.32f, 0.04f, 0.02f), hot = new Color(1f, 0.42f, 0.14f), eye = new Color(1f, 0.3f, 0.1f),
                emberA = new Color(0.8f, 0.15f, 0.04f), emberB = new Color(1f, 0.4f, 0.12f),
            },
            new Variant   // 3 自爆小鬼（黃白核心）
            {
                size = 0.75f, stride = 0.45f, legSwing = 40f, bob = 0.045f, step = 1.15f,
                rockA = new Color(0.72f, 0.45f, 0.18f), rockB = new Color(0.47f, 0.27f, 0.18f),
                lava = new Color(1f, 0.62f, 0.15f), lavaDim = new Color(0.75f, 0.25f, 0.04f), hot = new Color(1f, 0.97f, 0.75f), eye = new Color(1f, 0.9f, 0.5f),
                emberA = new Color(1f, 0.7f, 0.2f), emberB = new Color(1f, 0.95f, 0.6f),
            },
            new Variant   // 4 岩石投擲者（黯淡的橘色熔岩）
            {
                size = 1.1f, stride = 0.55f, legSwing = 32f, bob = 0.035f, step = 0.9f,
                rockA = new Color(0.62f, 0.4f, 0.24f), rockB = new Color(0.38f, 0.25f, 0.34f),
                lava = new Color(0.85f, 0.4f, 0.1f), lavaDim = new Color(0.4f, 0.12f, 0.03f), hot = new Color(1f, 0.72f, 0.35f), eye = new Color(1f, 0.6f, 0.2f),
                emberA = new Color(0.9f, 0.4f, 0.1f), emberB = new Color(1f, 0.65f, 0.3f),
            },
            new Variant   // 5 熔岩狙擊手（洋紅熔岩）
            {
                size = 1f, stride = 0.5f, legSwing = 34f, bob = 0.03f, step = 1f,
                rockA = new Color(0.6f, 0.3f, 0.49f), rockB = new Color(0.37f, 0.21f, 0.42f),
                lava = new Color(1f, 0.2f, 0.72f), lavaDim = new Color(0.45f, 0.04f, 0.3f), hot = new Color(1f, 0.82f, 0.96f), eye = new Color(1f, 0.6f, 0.95f),
                emberA = new Color(1f, 0.25f, 0.75f), emberB = new Color(1f, 0.7f, 0.95f),
            },
            new Variant   // 6 虛空術士（紫黑色虛空能量，手上漂著虛空球）
            {
                size = 0.95f, stride = 0.5f, legSwing = 30f, bob = 0.04f, step = 0.9f,
                rockA = new Color(0.46f, 0.29f, 0.72f), rockB = new Color(0.29f, 0.19f, 0.49f),
                lava = new Color(0.6f, 0.3f, 1f), lavaDim = new Color(0.2f, 0.06f, 0.4f), hot = new Color(0.92f, 0.8f, 1f), eye = new Color(0.8f, 0.6f, 1f),
                emberA = new Color(0.55f, 0.25f, 1f), emberB = new Color(0.9f, 0.75f, 1f),
            },
            // ---- 森林植物（rockA＝莖／主體、rockB＝根／樹皮、lava＝發光、accent/2/3 見各自註解）
            new Variant   // 7 毒液噴花：accent＝毒囊、accent2＝紫色花瓣、accent3＝葉
            {
                size = 1f, stride = 0.45f, legSwing = 0f, bob = 0.07f, step = 1f,
                rockA = new Color(0.32f, 0.72f, 0.18f), rockB = new Color(0.59f, 0.32f, 0.14f),
                lava = new Color(0.55f, 1f, 0.2f), lavaDim = new Color(0.2f, 0.45f, 0.05f), hot = new Color(0.88f, 1f, 0.6f), eye = new Color(0.7f, 1f, 0.3f),
                emberA = new Color(0.5f, 0.95f, 0.25f), emberB = new Color(0.8f, 0.5f, 1f),
                accent = new Color(0.53f, 0.86f, 0.14f), accent2 = new Color(0.78f, 0.24f, 0.9f), accent3 = new Color(0.22f, 0.6f, 0.23f),
            },
            new Variant   // 8 荊棘衝鋒獸：accent＝切面木頭、accent2＝獠牙／荊棘、accent3＝苔蘚
            {
                size = 1f, stride = 0.6f, legSwing = 30f, bob = 0.04f, step = 0.85f,
                rockA = new Color(0.66f, 0.36f, 0.16f), rockB = new Color(0.44f, 0.23f, 0.15f),
                lava = new Color(1f, 0.6f, 0.15f), lavaDim = new Color(0.5f, 0.2f, 0.04f), hot = new Color(1f, 0.9f, 0.5f), eye = new Color(1f, 0.72f, 0.2f),
                emberA = new Color(0.55f, 0.8f, 0.3f), emberB = new Color(0.9f, 0.85f, 0.4f),
                accent = new Color(0.96f, 0.68f, 0.3f), accent2 = new Color(0.98f, 0.87f, 0.55f), accent3 = new Color(0.32f, 0.67f, 0.18f),
            },
            new Variant   // 9 藤蔓術士：accent＝兜帽葉、accent2＝深色臉、accent3＝披風葉
            {
                size = 1f, stride = 0.5f, legSwing = 30f, bob = 0.03f, step = 0.95f,
                rockA = new Color(0.24f, 0.65f, 0.32f), rockB = new Color(0.18f, 0.43f, 0.35f),
                lava = new Color(0.4f, 1f, 0.35f), lavaDim = new Color(0.1f, 0.45f, 0.1f), hot = new Color(0.85f, 1f, 0.75f), eye = new Color(0.55f, 1f, 0.4f),
                emberA = new Color(0.35f, 1f, 0.35f), emberB = new Color(0.8f, 1f, 0.6f),
                accent = new Color(0.42f, 0.78f, 0.2f), accent2 = new Color(0.2f, 0.29f, 0.38f), accent3 = new Color(0.16f, 0.56f, 0.32f),
            },
            new Variant   // 10 食人花：accent＝顎外側、accent2＝紅色口腔、accent3＝葉
            {
                size = 1f, stride = 0.45f, legSwing = 0f, bob = 0.06f, step = 1.05f,
                rockA = new Color(0.34f, 0.76f, 0.18f), rockB = new Color(0.59f, 0.32f, 0.14f),
                lava = new Color(1f, 0.3f, 0.35f), lavaDim = new Color(0.45f, 0.06f, 0.1f), hot = new Color(1f, 0.8f, 0.7f), eye = new Color(1f, 0.92f, 0.3f),
                emberA = new Color(0.6f, 0.95f, 0.3f), emberB = new Color(1f, 0.6f, 0.6f),
                accent = new Color(0.64f, 0.86f, 0.18f), accent2 = new Color(0.9f, 0.16f, 0.26f), accent3 = new Color(0.24f, 0.63f, 0.23f),
            },
            new Variant   // 11 孢子菇：rockA＝菇柄、rockB＝菌褶、accent＝菇傘、lava＝發光斑點
            {
                size = 1f, stride = 0.4f, legSwing = 34f, bob = 0.04f, step = 1.1f,
                rockA = new Color(0.98f, 0.86f, 0.62f), rockB = new Color(0.72f, 0.45f, 0.32f),
                lava = new Color(0.75f, 1f, 0.35f), lavaDim = new Color(0.35f, 0.55f, 0.1f), hot = new Color(0.95f, 1f, 0.75f), eye = new Color(0.9f, 1f, 0.5f),
                emberA = new Color(0.75f, 1f, 0.35f), emberB = new Color(0.8f, 0.5f, 1f),
                accent = new Color(0.76f, 0.23f, 0.66f), accent2 = new Color(0.98f, 0.86f, 0.62f), accent3 = new Color(0.28f, 0.67f, 0.22f),
            },
            new Variant   // 12 種子槍手：accent＝種莢膛、accent2＝莢管／種子、accent3＝葉
            {
                size = 1f, stride = 0.45f, legSwing = 0f, bob = 0.06f, step = 1f,
                rockA = new Color(0.4f, 0.72f, 0.18f), rockB = new Color(0.59f, 0.32f, 0.14f),
                lava = new Color(1f, 0.82f, 0.3f), lavaDim = new Color(0.5f, 0.32f, 0.05f), hot = new Color(1f, 0.97f, 0.7f), eye = new Color(1f, 0.92f, 0.4f),
                emberA = new Color(0.6f, 0.95f, 0.3f), emberB = new Color(1f, 0.9f, 0.45f),
                accent = new Color(0.82f, 0.76f, 0.2f), accent2 = new Color(0.62f, 0.4f, 0.14f), accent3 = new Color(0.26f, 0.64f, 0.22f),
            },
            // ---- 海洋（step＝游動／拍翅的基本頻率（弧度/秒）；emberA/B＝泡泡、羽毛或電花；其餘見各自註解）
            new Variant   // 13 跳躍魚：rockA＝藍銀色身體、rockB＝深色背、accent＝鰭、accent2＝尖牙、eye＝紅眼
            {
                size = 1f, stride = 0.5f, legSwing = 0f, bob = 0f, step = 9f,
                rockA = new Color(0.52f, 0.81f, 0.96f), rockB = new Color(0.18f, 0.35f, 0.65f),
                lava = new Color(0.45f, 0.85f, 1f), lavaDim = new Color(0.15f, 0.35f, 0.6f), hot = new Color(0.85f, 0.97f, 1f), eye = new Color(1f, 0.12f, 0.08f),
                emberA = new Color(0.6f, 0.85f, 1f), emberB = new Color(0.95f, 1f, 1f),
                accent = new Color(0.18f, 0.62f, 0.88f), accent2 = new Color(0.99f, 0.94f, 0.79f), accent3 = new Color(0.76f, 0.15f, 0.23f),
            },
            new Variant   // 14 俯衝海鳥：rockA＝白灰身體、rockB＝深灰褐翅膀／眉、accent＝黃色鉤喙、accent2＝黑色翼尖、accent3＝腳
            {
                size = 1f, stride = 0.5f, legSwing = 0f, bob = 0f, step = 10f,
                rockA = new Color(0.94f, 0.97f, 1f), rockB = new Color(0.37f, 0.46f, 0.65f),
                lava = new Color(1f, 0.6f, 0.3f), lavaDim = new Color(0.5f, 0.2f, 0.05f), hot = new Color(1f, 0.9f, 0.7f), eye = new Color(1f, 0.25f, 0.08f),
                emberA = new Color(0.9f, 0.9f, 0.88f, 0.8f), emberB = new Color(0.55f, 0.52f, 0.5f, 0.8f),
                accent = new Color(1f, 0.75f, 0.12f), accent2 = new Color(0.23f, 0.29f, 0.48f), accent3 = new Color(0.96f, 0.48f, 0.14f),
            },
            new Variant   // 15 騎魚泡泡怪：rockA＝橘紅色坐騎、rockB＝米白肚子、accent＝魚人的青綠皮膚、accent2＝粉色海螺、accent3＝鰭
            {
                size = 1f, stride = 0.5f, legSwing = 0f, bob = 0f, step = 5f,
                rockA = new Color(1f, 0.4f, 0.14f), rockB = new Color(1f, 0.91f, 0.72f),
                lava = new Color(0.45f, 0.9f, 1f), lavaDim = new Color(0.15f, 0.4f, 0.65f), hot = new Color(0.85f, 1f, 1f), eye = new Color(1f, 0.92f, 0.3f),
                emberA = new Color(0.55f, 0.85f, 1f), emberB = new Color(0.95f, 1f, 1f),
                accent = new Color(0.16f, 0.76f, 0.62f), accent2 = new Color(1f, 0.61f, 0.65f), accent3 = new Color(1f, 0.7f, 0.14f),
            },
            new Variant   // 16 寄居蟹：rockA＝紅色蟹身、rockB＝米褐色螺殼、accent2＝螯尖、lava＝大螯蓄力色
            {
                size = 1f, stride = 0.42f, legSwing = 0f, bob = 0.025f, step = 1.1f,
                rockA = new Color(0.98f, 0.27f, 0.14f), rockB = new Color(0.97f, 0.77f, 0.49f),
                lava = new Color(1f, 0.55f, 0.2f), lavaDim = new Color(0.5f, 0.15f, 0.05f), hot = new Color(1f, 0.9f, 0.6f), eye = new Color(1f, 0.9f, 0.3f),
                emberA = new Color(0.6f, 0.85f, 1f), emberB = new Color(0.95f, 1f, 1f),
                accent = new Color(0.82f, 0.48f, 0.32f), accent2 = new Color(0.57f, 0.2f, 0.17f), accent3 = new Color(0.97f, 0.77f, 0.49f),
            },
            new Variant   // 17 雷電水母：lava＝傘緣光點／內芯、hot＝放電白光、eye 不用；傘與觸手是頂點色（青→紫）的半透明網格
            {
                size = 1f, stride = 0.5f, legSwing = 0f, bob = 0f, step = 3.8f,
                rockA = new Color(0.6f, 0.6f, 0.9f), rockB = new Color(0.5f, 0.4f, 0.8f),
                lava = new Color(0.55f, 0.92f, 1f), lavaDim = new Color(0.35f, 0.25f, 0.85f), hot = new Color(0.9f, 0.97f, 1f), eye = new Color(0.85f, 0.65f, 1f),
                emberA = new Color(0.45f, 0.9f, 1f), emberB = new Color(0.85f, 0.6f, 1f),
                accent = new Color(0.55f, 0.9f, 1f), accent2 = new Color(0.8f, 0.5f, 1f), accent3 = new Color(1f, 0.6f, 0.9f),
            },
            new Variant   // 18 河豚：rockA＝黃褐色身體、rockB＝白肚子、accent＝鰭、accent2＝刺、accent3＝嘴唇
            {
                size = 1f, stride = 0.5f, legSwing = 0f, bob = 0f, step = 8f,
                rockA = new Color(1f, 0.77f, 0.22f), rockB = new Color(1f, 0.97f, 0.83f),
                lava = new Color(1f, 0.6f, 0.25f), lavaDim = new Color(0.5f, 0.25f, 0.05f), hot = new Color(1f, 0.92f, 0.6f), eye = new Color(1f, 0.9f, 0.4f),
                emberA = new Color(0.6f, 0.85f, 1f), emberB = new Color(0.95f, 1f, 1f),
                accent = new Color(1f, 0.65f, 0.16f), accent2 = new Color(0.68f, 0.4f, 0.15f), accent3 = new Color(0.93f, 0.38f, 0.23f),
            },
        };

        static readonly Color FlashC = new Color(1f, 0.55f, 0.4f);
        static readonly Color Dark = new Color(0.12f, 0.06f, 0.04f);
        static readonly Color RockDead = new Color(0.27f, 0.24f, 0.32f);
        static readonly Color Armor = new Color(0.76f, 0.57f, 0.32f);

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
            litShader = ToonStyle.LitShader();   // 賽璐珞風格（黑邊＋兩階明暗）；不支援時退回 Legacy Diffuse
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
            if (k >= 19) { BuildAreaEnemy(k); return; }
            sea = k >= 13;
            plant = k >= 7 && !sea;
            if (sea) { BuildSea(k); return; }
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
            if (variantId >= 19) { AnimateAreaEnemy(dt); return; }
            if (sea) { AnimateSea(dt); return; }
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

        // ================================================================ 海洋小怪（種類 13–18）
        // 跟植物小怪同樣的作法：rockMat（有光照的實體）＋ glowMat（發光／半透明，Sprites/Default 會乘上頂點色），
        //   預備／出手＝「基本動作 ＋ wW×預備 ＋ sW×出手」的疊加；共用的網格全部 static、一次建立。
        //   飛行／漂浮的高度、跳躍弧線由呼叫者移動本物件的 transform，這裡只做原點附近的小幅起伏。

        // 海洋專用的共用網格
        static Mesh sFish, sFin, sTail, sWing, sWingTip, sShell, sLeg, sSpines, sBell, sRim, sTent, sFrill;

        // 海洋骨架
        bool sea;
        Transform segB, segC, tailT, finL, finR, finD, cheek, spikesT, zap;
        Vector3 cheekBase;
        Transform[] tents;          // 水母觸手：[2i]＝上段的關節、[2i+1]＝下段的關節
        Vector3[] tentAx;           // 觸手往外張的旋轉軸（傘緣的切線）
        readonly List<Renderer> veilList = new List<Renderer>();   // 半透明的傘／觸手（頂點色 × PropertyBlock 的亮度與透明度）
        readonly List<float> veilAs = new List<float>();
        Renderer[] veilR; float[] veilA;
        Renderer zapR;
        ParticleSystem spikePS;
        float swimPh, blowT, crackT, zapK;
        static readonly Color SeaFlash = new Color(1f, 0.95f, 0.9f);
        static readonly Color SeaDead = new Color(0.34f, 0.35f, 0.37f);
        static readonly Color SeaDark = new Color(0.05f, 0.06f, 0.1f);
        static readonly Color EyeWhite = new Color(0.97f, 0.97f, 0.94f);

        static void InitSea()
        {
            InitPlant();
            if (sFish != null) return;
            var up = Quaternion.Euler(-90f, 0f, 0f);   // Lathe 的 +Z → +Y

            // 魚身：沿 +Z 的紡錘（長 1、最粗直徑 1），頭（+Z）收尖、尾端封口
            {
                var m = new PMesh(1);
                m.Lathe(new[] { -0.5f, 0f, -0.47f, 0.2f, -0.3f, 0.4f, -0.05f, 0.5f, 0.2f, 0.46f, 0.36f, 0.33f, 0.46f, 0.15f, 0.5f, 0f }, 8, null);
                sFish = m.Build("SeaFish", true);
            }
            // 鰭：YZ 平面上的薄片（雙面），鰭根沿 Z（-0.5..0.5）、往 +Y 長 1、往後掃
            {
                var m = new PMesh(1);
                FlatFan(m, new Vector3(0f, 0.25f, -0.1f), new[]
                {
                    new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0.5f, 0.2f), new Vector3(0f, 0.92f, -0.3f), new Vector3(0f, 1f, -0.55f),
                    new Vector3(0f, 0.62f, -0.48f), new Vector3(0f, 0.3f, -0.5f), new Vector3(0f, 0f, -0.5f),
                });
                sFin = m.Build("SeaFin", true);
            }
            // 分叉的尾鰭：YZ 平面，從原點往 -Z 長 1、上下兩葉各高 0.55
            {
                var m = new PMesh(1);
                FlatFan(m, new Vector3(0f, 0f, -0.18f), new[]
                {
                    new Vector3(0f, 0.12f, 0f), new Vector3(0f, 0.32f, -0.5f), new Vector3(0f, 0.56f, -1f), new Vector3(0f, 0.3f, -0.84f),
                    new Vector3(0f, 0f, -0.56f), new Vector3(0f, -0.3f, -0.84f), new Vector3(0f, -0.56f, -1f), new Vector3(0f, -0.32f, -0.5f),
                    new Vector3(0f, -0.12f, 0f), new Vector3(0f, 0.12f, 0f),
                });
                sTail = m.Build("SeaTail", true);
            }
            // 翅膀：XZ 平面往 +X 長 1（內翼：寬、後緣羽毛參差；外翼：收尖、飛羽分開）
            sWing = WingMesh("SeaWing", false);
            sWingTip = WingMesh("SeaWingTip", true);
            // 螺旋貝殼（尖刺）：沿 +Y，底部（殼口那一圈）在 y≈0.3、殼頂在 y≈0.95，最寬直徑約 1.1
            {
                var m = new PMesh(1);
                const int N = 36;
                const float turns = 2.4f;
                var pts = new Vector3[N + 1];
                var rad = new float[N + 1];
                for (int i = 0; i < N; i++)
                {
                    float s = (float)i / (N - 1), th = 2f * Mathf.PI * turns * s;
                    float R = 0.26f * Mathf.Pow(1f - s, 1.2f);
                    pts[i + 1] = new Vector3(Mathf.Cos(th) * R, 0.3f + 0.65f * Mathf.Pow(s, 0.85f), Mathf.Sin(th) * R);
                    rad[i + 1] = 0.3f * Mathf.Pow(1f - s, 1.15f) + 0.012f;
                }
                rad[N] = 0f;                                                      // 殼頂收尖
                pts[0] = pts[1] - (pts[2] - pts[1]).normalized * 0.05f; rad[0] = 0f;   // 殼口封起來
                Tube(m, pts, rad, 8, 0f);
                for (int i = 2; i < N - 9; i += 3)
                {
                    float s = (float)i / (N - 1), th = 2f * Mathf.PI * turns * s;
                    var d = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
                    var b0 = pts[i + 1] + d * rad[i + 1] * 0.8f + Vector3.up * rad[i + 1] * 0.35f;
                    var dir = (d + Vector3.up * 0.7f).normalized;
                    Tube(m, new[] { b0, b0 + dir * (0.06f + 0.16f * (1f - s)) }, new[] { 0.025f + 0.04f * (1f - s), 0f }, 4, 45f);
                }
                sShell = m.Build("SeaShell", true);
            }
            // 蟹腳：XY 平面往 +X 伸出、膝蓋拱起、腳尖往下（腳尖在 (1, -0.62)）
            {
                var m = new PMesh(1);
                Tube(m, new[] { new Vector3(-0.05f, 0f, 0f), new Vector3(0.3f, 0.32f, 0f), new Vector3(0.62f, 0.3f, 0f), new Vector3(0.86f, -0.1f, 0f), new Vector3(1f, -0.62f, 0f) },
                    new[] { 0.1f, 0.09f, 0.075f, 0.05f, 0f }, 5, 0f);
                sLeg = m.Build("SeaLeg", true);
            }
            // 河豚的刺：單位球（半徑 0.5）上往外的短錐，臉正前方與尾巴不長
            {
                var m = new PMesh(1);
                const int N = 52;
                for (int i = 0; i < N; i++)
                {
                    float y = 1f - 2f * (i + 0.5f) / N, r = Mathf.Sqrt(1f - y * y), a = i * 2.39996f;
                    var d = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    if (d.z > 0.78f || d.z < -0.88f) continue;
                    Tube(m, new[] { d * 0.36f, d * 0.63f }, new[] { 0.04f, 0f }, 4, i * 29f);
                }
                sSpines = m.Build("SeaSpines", true);
            }
            // 水母的傘：沿 +Y 的圓頂（底部開口、半徑 0.5、高 0.5），頂點色：傘緣青 → 頂端紫
            {
                var m = new PMesh(1);
                var cyan = new Color(0.45f, 0.95f, 1f); var violet = new Color(0.72f, 0.42f, 1f);
                m.Lathe(new[] { 0f, 0.5f, 0.1f, 0.49f, 0.24f, 0.43f, 0.36f, 0.32f, 0.45f, 0.17f, 0.5f, 0f }, 12,
                    (z, j) => Color.Lerp(cyan, violet, z / 0.5f), 1f, 1f, 0f, null, Vector3.zero, up);
                sBell = m.Build("SeaBell", false);
            }
            // 傘緣一圈光點：半徑 0.5 的 16 顆小菱形（十字交叉的兩片，任何角度都看得到）
            {
                var m = new PMesh(1);
                const float h = 0.035f;
                for (int i = 0; i < 16; i++)
                {
                    float a = 2f * Mathf.PI * i / 16f;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var t = new Vector3(-d.z, 0f, d.x);
                    var c = d * 0.5f;
                    m.Quad(c + Vector3.up * h, c + t * h, c - Vector3.up * h, c - t * h, Color.white);
                    m.Quad(c + d * h, c + t * h, c - d * h, c - t * h, Color.white);
                }
                sRim = m.Build("SeaRim", false);
            }
            // 觸手：沿 -Y 長 1 的細管，微微彎曲，往下變細
            {
                var m = new PMesh(1);
                const int n = 8;
                var pts = new Vector3[n + 1];
                var rad = new float[n + 1];
                for (int i = 0; i <= n; i++)
                {
                    float s = (float)i / n;
                    pts[i] = new Vector3(0.12f * Mathf.Sin(s * Mathf.PI * 1.6f) * s, -s, 0f);
                    rad[i] = 0.5f * (1f - 0.65f * s);
                }
                Tube(m, pts, rad, 5, 0f);
                sTent = m.Build("SeaTentacle", false);
            }
            // 口腕：四條往下垂、邊緣起伏的半透明帶子（頂點色：粉紫，往下漸淡）
            {
                var m = new PMesh(1);
                const int n = 8;
                var top = new Color(1f, 0.65f, 0.95f, 0.95f); var bot = new Color(0.6f, 0.5f, 1f, 0.2f);
                for (int k = 0; k < 4; k++)
                {
                    float a = (90f * k + 45f) * Mathf.Deg2Rad;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var t = new Vector3(-d.z, 0f, d.x);
                    var pts = new Vector3[n + 1]; var half = new Vector3[n + 1]; var cols = new Color[n + 1];
                    for (int i = 0; i <= n; i++)
                    {
                        float s = (float)i / n;
                        pts[i] = d * (0.12f + 0.18f * s) + t * (0.14f * Mathf.Sin(s * 9f + k)) - Vector3.up * s;
                        half[i] = d * (0.2f * (1f - 0.7f * s)) + Vector3.up * 0.02f * Mathf.Sin(s * 13f);
                        cols[i] = Color.Lerp(top, bot, s);
                    }
                    m.Ribbon(pts, half, cols);
                }
                sFrill = m.Build("SeaFrill", false);
            }
        }

        /// <summary>雙面的扇形薄片（從 c 往 pts 的折線扇開）。</summary>
        static void FlatFan(PMesh m, Vector3 c, Vector3[] pts)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                m.Tri(c, pts[i], pts[i + 1], Color.white);
                m.Tri(c, pts[i + 1], pts[i], Color.white);
            }
        }

        static Mesh WingMesh(string name, bool tip)
        {
            var m = new PMesh(1);
            const int n = 8;
            for (int i = 0; i < n; i++)
            {
                var a0 = WingPt(i, n, tip, true); var a1 = WingPt(i + 1, n, tip, true);
                var b0 = WingPt(i, n, tip, false); var b1 = WingPt(i + 1, n, tip, false);
                m.Quad(a0, a1, b1, b0, Color.white);
                m.Quad(a0, b0, b1, a1, Color.white);
            }
            return m.Build(name, true);
        }

        /// <summary>翅膀輪廓：s＝沿翼展 0..1，lead＝前緣／後緣；後緣每隔一點往後凸（羽毛）。</summary>
        static Vector3 WingPt(int i, int n, bool tip, bool lead)
        {
            float s = (float)i / n;
            float y = 0.05f * Mathf.Sin(Mathf.PI * s);   // 微微拱起
            bool feather = (i & 1) == 1;
            if (!tip)
            {
                float le = 0.28f - 0.08f * s, te = -0.6f + 0.1f * s - (feather ? 0.07f : 0f);
                return new Vector3(s, lead ? y : y * 0.3f, lead ? le : te);
            }
            float c = 0.72f * (1f - Mathf.Pow(s, 1.6f)) + 0.03f;
            float l = 0.2f - 0.3f * s;
            float t = l - c - (feather ? 0.1f * (1f - s) : 0f);
            return new Vector3(s, lead ? y : y * 0.3f, lead ? l : t);
        }

        // ---------------------------------------------------------------- 海洋：建立
        /// <summary>發光材質（Sprites/Default，會乘上網格的頂點色）的半透明零件，顏色／透明度由 UpdateSeaColors 每幀給。</summary>
        Transform Veil(Transform parent, Mesh mesh, Vector3 pos, Vector3 scale, Vector3 euler, float alpha)
        {
            var go = new GameObject("Veil");
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = glowMat;
            NoShadow(r);
            veilList.Add(r); veilAs.Add(alpha);
            return t;
        }

        void BuildSea(int k)
        {
            InitSea();
            variantId = k;
            kind = k;
            v = Variants[k];
            rockList.Clear(); rockCols.Clear(); rockGlows.Clear(); lavaList.Clear(); hotList.Clear(); eyeList.Clear();
            swayT.Clear(); swayQ.Clear(); veilList.Clear(); veilAs.Clear();
            flame = null; flameR = null; ball = null; core = null; boulder = null; tip = null; carryK = 0f; ballGone = 10f;
            neck = jaw = jawU = barrel = hipBL = hipBR = rootT = handL = handR = null; petals = null;
            hipL = hipR = shL = shR = wrL = wrR = null; spine = head = null;
            segB = segC = tailT = finL = finR = finD = cheek = spikesT = zap = null; zapR = null;
            tents = null; tentAx = null; spikePS = null;
            clock = Random.Range(0f, 10f);
            swimPh = Random.Range(0f, 2f * Mathf.PI);
            phase = PhaseNone;
            snapW = plantW = 0f; blowT = 0f; crackT = 0f; zapK = 0f;

            body = Pivot("Body", transform, Vector3.zero);
            switch (k)
            {
                case 13: BuildLeaper(); break;
                case 14: BuildSeabird(); break;
                case 15: BuildRider(); break;
                case 16: BuildHermit(); break;
                case 17: BuildJelly(); break;
                default: BuildPuffer(); break;
            }

            // ---- 少量泡泡（海鳥：飄落的羽毛；水母：電花）
            bool air = k == 14, elec = k == 17;
            embers = SpellFx.NewPS(body, elec ? "Sparks" : air ? "Feathers" : "Bubbles",
                air ? SpellFx.BlendMat(SpellFx.TexLeaf) : SpellFx.AddMat(SpellFx.TexSoft), elec ? 48 : air ? 8 : 40, true);
            embers.transform.localPosition = new Vector3(0f, k == 13 ? 0.1f : k == 15 ? 0.3f : k == 16 ? 0.4f : 0f, 0f);
            {
                var m = embers.main;
                m.startLifetime = elec ? new ParticleSystem.MinMaxCurve(0.25f, 0.6f) : new ParticleSystem.MinMaxCurve(1f, 1.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                m.startSize = air ? new ParticleSystem.MinMaxCurve(0.04f, 0.07f) : elec ? new ParticleSystem.MinMaxCurve(0.012f, 0.03f) : new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
                m.startColor = new ParticleSystem.MinMaxGradient(v.emberA, v.emberB);
                if (air) m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                SpellFx.Sphere(embers, k == 15 ? 0.35f : 0.3f);
                SpellFx.FadeInOut(embers);
                if (air) { m.gravityModifier = 0.04f; SpellFx.Spin(embers, 3f); SpellFx.Noise(embers, 0.3f, 1f); }
                else if (elec) { SpellFx.Drag(embers, 3f); SpellFx.Noise(embers, 0.4f, 3f); }
                else { SpellFx.Rise(embers, 0.15f); SpellFx.Noise(embers, 0.08f, 1.5f); }
                SpellFx.Rate(embers, dead ? 0f : air ? 0.6f : 3f);
                embers.Play();
            }
            // 河豚出手時射出的尖刺（拉長的亮點）
            if (k == 18)
            {
                spikePS = SpellFx.NewPS(body, "Spikes", SpellFx.AddMat(SpellFx.TexSoft), 32, true);
                var m = spikePS.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.03f);
                m.startColor = new ParticleSystem.MinMaxGradient(v.hot, v.accent);
                m.gravityModifier = 0.3f;
                SpellFx.FadeOut(spikePS);
                SpellFx.Stretch(spikePS, 0.06f, 2f);
                spikePS.Play();
            }

            rockR = rockList.ToArray(); rockC = rockCols.ToArray(); rockG = rockGlows.ToArray();
            lavaR = lavaList.ToArray(); hotR = hotList.ToArray(); eyeR = eyeList.ToArray();
            veilR = veilList.ToArray(); veilA = veilAs.ToArray();
            AnimateSea(0f);
        }

        /// <summary>13 跳躍魚：前半身（藍銀＋深色背、上排尖牙、紅眼、背鰭、胸鰭）＋可張開的下顎＋後半身＋尾柄與分叉尾鰭（兩節擺動）。</summary>
        void BuildLeaper()
        {
            spine = Pivot("Front", body, new Vector3(0f, 0.1f, 0f));   // 原點＝肚子，身體中心離地 0.1
            Part(spine, sFish, new Vector3(0f, 0f, 0.17f), new Vector3(0.15f, 0.19f, 0.5f), Vector3.zero, v.rockA, 0.35f);
            Part(spine, sFish, new Vector3(0f, 0.028f, 0.155f), new Vector3(0.11f, 0.15f, 0.46f), Vector3.zero, v.rockB);   // 深色背
            Part(spine, pTeeth, new Vector3(0f, -0.022f, 0.31f), new Vector3(0.085f, 0.04f, 0.12f), new Vector3(0f, 0f, 180f), v.accent2);   // 上排尖牙
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Sphere, spine, new Vector3(0.05f * s, 0.028f, 0.3f), new Vector3(0.03f, 0.03f, 0.035f), eyeList, new Vector3(0f, 0f, 20f * s));
            Part(spine, sFin, new Vector3(0f, 0.07f, 0.03f), new Vector3(1f, 0.11f, 0.17f), new Vector3(-6f, 0f, 0f), v.accent);   // 背鰭
            finL = Pivot("FinL", spine, new Vector3(-0.065f, -0.035f, 0.15f));
            finR = Pivot("FinR", spine, new Vector3(0.065f, -0.035f, 0.15f));
            Part(finL, sFin, Vector3.zero, new Vector3(1f, 0.09f, 0.09f), Vector3.zero, v.accent);
            Part(finR, sFin, Vector3.zero, new Vector3(1f, 0.09f, 0.09f), Vector3.zero, v.accent);
            // 下顎（張嘴時往下轉）
            jaw = Pivot("Jaw", spine, new Vector3(0f, -0.035f, 0.2f));
            Part(jaw, sFish, new Vector3(0f, -0.012f, 0.1f), new Vector3(0.1f, 0.06f, 0.26f), Vector3.zero, v.rockA);
            Part(jaw, pTeeth, new Vector3(0f, 0.008f, 0.12f), new Vector3(0.075f, 0.035f, 0.11f), Vector3.zero, v.accent2);
            // 後半身＋尾
            segB = Pivot("Rear", spine, new Vector3(0f, 0f, -0.06f));
            Part(segB, sFish, new Vector3(0f, 0f, -0.12f), new Vector3(0.11f, 0.14f, 0.34f), Vector3.zero, v.rockA, 0.35f);
            Part(segB, sFish, new Vector3(0f, 0.02f, -0.12f), new Vector3(0.08f, 0.11f, 0.32f), Vector3.zero, v.rockB);
            Part(segB, sFin, new Vector3(0f, -0.05f, -0.14f), new Vector3(1f, 0.07f, 0.11f), new Vector3(0f, 0f, 180f), v.accent);   // 臀鰭
            segC = Pivot("Tail", segB, new Vector3(0f, 0f, -0.27f));
            Part(segC, sTail, Vector3.zero, new Vector3(1f, 0.17f, 0.17f), Vector3.zero, v.accent);
        }

        /// <summary>14 俯衝海鳥：白灰身體、深色眉、紅眼、黃色鉤喙（上喙＋喙鉤＋可張開的下喙）、兩節翅膀（黑色翼尖）、分叉尾羽、腳＋爪。</summary>
        void BuildSeabird()
        {
            spine = Pivot("Torso", body, Vector3.zero);
            Part(spine, pBlob, Vector3.zero, new Vector3(0.17f, 0.16f, 0.38f), Vector3.zero, v.rockA);
            head = Pivot("Head", spine, new Vector3(0f, 0.06f, 0.16f));
            Part(head, pBlob, new Vector3(0f, 0.03f, 0.03f), new Vector3(0.13f, 0.12f, 0.15f), Vector3.zero, v.rockA);
            Part(head, pBlob, new Vector3(0f, 0.068f, 0.07f), new Vector3(0.125f, 0.035f, 0.07f), new Vector3(18f, 0f, 0f), v.rockB);   // 兇狠的眉
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Sphere, head, new Vector3(0.048f * s, 0.048f, 0.08f), new Vector3(0.028f, 0.02f, 0.02f), eyeList, new Vector3(0f, 0f, -18f * s));
            Part(head, pCone, new Vector3(0f, 0.02f, 0.085f), new Vector3(0.042f, 0.13f, 0.036f), new Vector3(96f, 0f, 0f), v.accent, 0.3f);   // 上喙
            Part(head, pCone, new Vector3(0f, 0.01f, 0.205f), new Vector3(0.026f, 0.045f, 0.026f), new Vector3(155f, 0f, 0f), v.accent);      // 喙鉤
            jaw = Pivot("Jaw", head, new Vector3(0f, 0.002f, 0.085f));
            Part(jaw, pCone, Vector3.zero, new Vector3(0.034f, 0.105f, 0.028f), new Vector3(100f, 0f, 0f), v.accent);
            // 翅膀：肩（內翼）＋腕（外翼、黑色翼尖）
            shL = Pivot("WingL", spine, new Vector3(-0.06f, 0.045f, 0.05f));
            shR = Pivot("WingR", spine, new Vector3(0.06f, 0.045f, 0.05f));
            for (int s = -1; s <= 1; s += 2)
            {
                var sh = s < 0 ? shL : shR;
                var flip = new Vector3(0f, s < 0 ? 180f : 0f, 0f);   // 左翼：繞 Y 轉 180 度（往 -X 伸、前緣仍朝前）
                Part(sh, sWing, Vector3.zero, new Vector3(0.3f, 1f, s < 0 ? -0.36f : 0.36f), flip, v.rockB);
                var wr = Pivot("Wrist", sh, new Vector3(0.3f * s, 0f, 0f));
                Part(wr, sWingTip, Vector3.zero, new Vector3(0.34f, 1f, s < 0 ? -0.36f : 0.36f), flip, v.accent2);
                if (s < 0) wrL = wr; else wrR = wr;
            }
            tailT = Pivot("Tail", spine, new Vector3(0f, 0.015f, -0.16f));
            Part(tailT, sTail, Vector3.zero, new Vector3(1f, 0.11f, 0.15f), new Vector3(0f, 0f, 90f), v.rockA);   // 平放的分叉尾羽
            // 腳＋爪（平常往後收，俯衝時往前伸）
            hipL = Pivot("LegL", spine, new Vector3(-0.04f, -0.06f, -0.02f));
            hipR = Pivot("LegR", spine, new Vector3(0.04f, -0.06f, -0.02f));
            foreach (var h in new[] { hipL, hipR })
            {
                Part(h, pCone, Vector3.zero, new Vector3(0.028f, 0.12f, 0.028f), new Vector3(180f, 0f, 0f), v.accent3);
                Part(h, pTeeth, new Vector3(0f, -0.1f, 0.01f), new Vector3(0.05f, 0.04f, 0.055f), new Vector3(0f, 0f, 180f), v.accent2);
            }
        }

        /// <summary>15 騎魚泡泡怪：圓滾滾的大魚坐騎（米白肚子、厚嘴唇、黃眼、擺動的尾鰭與胸鰭）上坐著青綠色魚人（頭冠鰭、鼓脹的臉頰、右手拿海螺）。</summary>
        void BuildRider()
        {
            spine = Pivot("Mount", body, new Vector3(0f, 0.4f, 0f));
            Part(spine, pBlob, Vector3.zero, new Vector3(0.46f, 0.46f, 0.62f), Vector3.zero, v.rockA);
            Part(spine, pBlob, new Vector3(0f, -0.1f, 0.03f), new Vector3(0.39f, 0.3f, 0.52f), Vector3.zero, v.rockB);   // 肚子
            Part(spine, pBlob, new Vector3(0f, -0.04f, 0.29f), new Vector3(0.17f, 0.1f, 0.09f), Vector3.zero, v.accent3 * 0.85f);   // 厚嘴唇
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Sphere, spine, new Vector3(0.165f * s, 0.07f, 0.19f), new Vector3(0.07f, 0.075f, 0.07f), eyeList);
            tailT = Pivot("Tail", spine, new Vector3(0f, 0.02f, -0.28f));
            Part(tailT, sTail, Vector3.zero, new Vector3(1f, 0.3f, 0.28f), Vector3.zero, v.accent3);
            finL = Pivot("FinL", spine, new Vector3(-0.2f, -0.08f, 0.06f));
            finR = Pivot("FinR", spine, new Vector3(0.2f, -0.08f, 0.06f));
            Part(finL, sFin, Vector3.zero, new Vector3(1f, 0.2f, 0.18f), Vector3.zero, v.accent3);
            Part(finR, sFin, Vector3.zero, new Vector3(1f, 0.2f, 0.18f), Vector3.zero, v.accent3);
            // 騎士（坐在坐騎背上，跟著坐騎起伏）
            neck = Pivot("Rider", spine, new Vector3(0f, 0.2f, -0.04f));
            Part(neck, pBlob, new Vector3(0f, 0.11f, 0f), new Vector3(0.2f, 0.26f, 0.17f), Vector3.zero, v.accent);
            head = Pivot("Head", neck, new Vector3(0f, 0.24f, 0.01f));
            Part(head, pBlob, new Vector3(0f, 0.09f, 0f), new Vector3(0.24f, 0.21f, 0.21f), Vector3.zero, v.accent);
            Part(head, sFin, new Vector3(0f, 0.16f, -0.02f), new Vector3(1f, 0.11f, 0.15f), Vector3.zero, v.accent3);   // 頭冠鰭
            for (int s = -1; s <= 1; s += 2)
                Glow(PrimitiveType.Sphere, head, new Vector3(0.052f * s, 0.12f, 0.09f), new Vector3(0.05f, 0.045f, 0.03f), eyeList, new Vector3(0f, 0f, 12f * s));
            cheek = Pivot("Cheeks", head, new Vector3(0f, 0.055f, 0.065f));
            Part(cheek, pBlob, Vector3.zero, new Vector3(0.2f, 0.09f, 0.1f), Vector3.zero, Color.Lerp(v.accent, v.accent2, 0.45f));
            cheekBase = cheek.localScale;
            // 右手＋海螺（殼頂朝嘴、殼口朝前）、左手
            shR = Pivot("ArmR", neck, new Vector3(0.11f, 0.19f, 0f));
            Part(shR, pBlob, new Vector3(0f, -0.09f, 0f), new Vector3(0.06f, 0.2f, 0.06f), Vector3.zero, v.accent);
            tip = Pivot("Conch", shR, new Vector3(0f, -0.18f, 0.02f));
            Part(tip, sShell, new Vector3(0f, 0f, 0.15f), new Vector3(0.12f, 0.24f, 0.12f), new Vector3(-90f, 0f, 0f), v.accent2, 0.3f);
            shL = Pivot("ArmL", neck, new Vector3(-0.11f, 0.19f, 0f));
            Part(shL, pBlob, new Vector3(0f, -0.09f, 0f), new Vector3(0.06f, 0.2f, 0.06f), Vector3.zero, v.accent);
        }

        /// <summary>16 寄居蟹：紅色蟹身背著帶刺的螺旋大殼、一對眼柄、特大的右螯（螯掌＋固定指＋可動指）與小左螯、四條步足。</summary>
        void BuildHermit()
        {
            spine = Pivot("Torso", body, new Vector3(0f, 0.2f, 0f));
            Part(spine, pBlob, new Vector3(0f, 0f, 0.08f), new Vector3(0.3f, 0.17f, 0.26f), Vector3.zero, v.rockA);
            Part(spine, sShell, new Vector3(0f, -0.13f, -0.13f), new Vector3(0.74f, 0.74f, 0.74f), new Vector3(-28f, 30f, 0f), v.rockB);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(spine, pStem, new Vector3(0.045f * s, 0.05f, 0.17f), new Vector3(0.03f, 0.13f, 0.03f), new Vector3(-10f, 0f, -12f * s), v.rockA);   // 眼柄
                Glow(PrimitiveType.Sphere, spine, new Vector3(0.072f * s, 0.18f, 0.19f), new Vector3(0.045f, 0.045f, 0.045f), eyeList);
            }
            // 右邊的大螯
            shR = Pivot("ClawR", spine, new Vector3(0.15f, 0f, 0.16f));
            Part(shR, pBlob, new Vector3(0f, 0f, 0.07f), new Vector3(0.08f, 0.08f, 0.18f), Vector3.zero, v.rockA);
            wrR = Pivot("Hand", shR, new Vector3(0f, 0f, 0.15f));
            Part(wrR, pBlob, new Vector3(0f, 0f, 0.09f), new Vector3(0.2f, 0.16f, 0.22f), Vector3.zero, v.rockA, 0.6f);
            Part(wrR, pCone, new Vector3(0.015f, -0.035f, 0.18f), new Vector3(0.075f, 0.17f, 0.06f), new Vector3(90f, 0f, 0f), v.accent2);   // 固定指
            jaw = Pivot("Finger", wrR, new Vector3(0.015f, 0.035f, 0.17f));
            Part(jaw, pCone, Vector3.zero, new Vector3(0.065f, 0.18f, 0.05f), new Vector3(90f, 0f, 0f), v.accent2);   // 可動指
            // 左邊的小螯
            shL = Pivot("ClawL", spine, new Vector3(-0.13f, -0.01f, 0.16f));
            Part(shL, pBlob, new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 0.05f, 0.12f), Vector3.zero, v.rockA);
            wrL = Pivot("HandL", shL, new Vector3(0f, 0f, 0.1f));
            Part(wrL, pBlob, new Vector3(0f, 0f, 0.05f), new Vector3(0.08f, 0.07f, 0.13f), Vector3.zero, v.rockA);
            // 四條步足（從蟹身兩側伸出、腳尖著地）
            hipR = Pivot("LegFR", body, new Vector3(0.12f, 0.185f, 0.1f));
            hipBR = Pivot("LegBR", body, new Vector3(0.12f, 0.185f, 0.01f));
            hipL = Pivot("LegFL", body, new Vector3(-0.12f, 0.185f, 0.1f));
            hipBL = Pivot("LegBL", body, new Vector3(-0.12f, 0.185f, 0.01f));
            foreach (var h in new[] { hipL, hipR, hipBL, hipBR })
                Part(h, sLeg, Vector3.zero, Vector3.one * 0.3f, Vector3.zero, v.rockA);
        }

        /// <summary>17 雷電水母：半透明的青紫色傘＋發光內芯＋傘緣一圈光點、中央四條口腕、六條兩節的觸手；放電時的閃光球。</summary>
        void BuildJelly()
        {
            head = Pivot("Bell", body, Vector3.zero);   // 原點＝傘的中心
            spine = head;
            Veil(head, sBell, new Vector3(0f, -0.2f, 0f), new Vector3(0.7f, 0.9f, 0.7f), Vector3.zero, 0.5f);
            Glow(PrimitiveType.Sphere, head, new Vector3(0f, -0.02f, 0f), new Vector3(0.34f, 0.24f, 0.34f), lavaList);   // 內芯
            var rim = new GameObject("Rim");
            rim.transform.SetParent(head, false);
            rim.transform.localPosition = new Vector3(0f, -0.195f, 0f);
            rim.transform.localScale = Vector3.one * 0.7f;
            rim.AddComponent<MeshFilter>().sharedMesh = sRim;
            var rr = rim.AddComponent<MeshRenderer>();
            rr.sharedMaterial = glowMat;
            NoShadow(rr);
            eyeList.Add(rr);   // 傘緣光點（顏色另外算，見 UpdateSeaColors）
            Veil(head, sFrill, new Vector3(0f, -0.15f, 0f), new Vector3(0.22f, 0.42f, 0.22f), Vector3.zero, 0.8f);
            tents = new Transform[12];
            tentAx = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (60f * i + 30f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var t0 = Pivot("Tentacle", head, d * 0.27f + Vector3.down * 0.18f);
                tentAx[i] = new Vector3(-d.z, 0f, d.x);
                Veil(t0, sTent, Vector3.zero, new Vector3(0.035f, 0.42f, 0.035f), Vector3.zero, 0.6f);
                var t1 = Pivot("Tentacle2", t0, new Vector3(0f, -0.41f, 0f));
                Veil(t1, sTent, Vector3.zero, new Vector3(0.024f, 0.42f, 0.024f), new Vector3(0f, 180f, 0f), 0.45f);
                tents[i * 2] = t0; tents[i * 2 + 1] = t1;
            }
            zap = Pivot("Zap", head, new Vector3(0f, -0.05f, 0f));
            var tmp = new List<Renderer>(1);
            Glow(PrimitiveType.Sphere, zap, Vector3.zero, Vector3.one, tmp);
            zapR = tmp[0];
            zap.localScale = Vector3.zero;
        }

        /// <summary>18 河豚：黃褐色圓身體＋白肚子、全身短刺、一對大眼睛（眼白＋瞳孔）、嘟嘴、尾鰭、背鰭、兩片胸鰭。</summary>
        void BuildPuffer()
        {
            spine = Pivot("Torso", body, Vector3.zero);   // 原點＝身體中心
            Part(spine, pBlob, Vector3.zero, new Vector3(0.4f, 0.36f, 0.44f), Vector3.zero, v.rockA, 0.35f);
            Part(spine, pBlob, new Vector3(0f, -0.07f, 0.01f), new Vector3(0.36f, 0.25f, 0.38f), Vector3.zero, v.rockB);
            spikesT = Pivot("Spines", spine, Vector3.zero);
            Part(spikesT, sSpines, Vector3.zero, new Vector3(0.4f, 0.36f, 0.44f), Vector3.zero, v.accent2);
            for (int s = -1; s <= 1; s += 2)
            {
                Part(spine, pBlob, new Vector3(0.12f * s, 0.07f, 0.13f), new Vector3(0.11f, 0.11f, 0.1f), Vector3.zero, EyeWhite);
                Part(spine, pBlob, new Vector3(0.14f * s, 0.075f, 0.165f), new Vector3(0.055f, 0.06f, 0.045f), Vector3.zero, SeaDark);
            }
            Part(spine, pBlob, new Vector3(0f, -0.03f, 0.215f), new Vector3(0.08f, 0.06f, 0.05f), Vector3.zero, v.accent3);   // 嘟嘴
            tailT = Pivot("Tail", spine, new Vector3(0f, 0.01f, -0.2f));
            Part(tailT, sTail, Vector3.zero, new Vector3(1f, 0.14f, 0.13f), Vector3.zero, v.accent);
            finD = Pivot("Dorsal", spine, new Vector3(0f, 0.14f, -0.1f));
            Part(finD, sFin, Vector3.zero, new Vector3(1f, 0.07f, 0.09f), new Vector3(-15f, 0f, 0f), v.accent);
            finL = Pivot("FinL", spine, new Vector3(-0.185f, -0.01f, 0.03f));
            finR = Pivot("FinR", spine, new Vector3(0.185f, -0.01f, 0.03f));
            Part(finL, sFin, Vector3.zero, new Vector3(1f, 0.08f, 0.08f), Vector3.zero, v.accent);
            Part(finR, sFin, Vector3.zero, new Vector3(1f, 0.08f, 0.08f), Vector3.zero, v.accent);
        }

        // ---------------------------------------------------------------- 海洋：動作
        void SeaAttack()
        {
            switch (kind)
            {
                case 15:   // 吹海螺：一串泡泡（之後 blowT 秒內持續冒出）
                    blowT = 0.5f;
                    EmitBubbles(8);
                    break;
                case 17:   // 放電：四散的電花
                    zapK = 1f;
                    if (embers != null && head != null)
                    {
                        var ep = new ParticleSystem.EmitParams();
                        var c = head.position;
                        for (int i = 0; i < 26; i++)
                        {
                            var d = Random.onUnitSphere;
                            ep.position = c + d * 0.2f;
                            ep.velocity = d * Random.Range(1.2f, 2.8f);
                            ep.startSize = Random.Range(0.02f, 0.045f);
                            ep.startLifetime = Random.Range(0.2f, 0.45f);
                            ep.startColor = Color.Lerp(v.hot, v.emberB, Random.value * 0.6f);
                            embers.Emit(ep, 1);
                        }
                    }
                    break;
                case 18:   // 洩氣：尖刺往四面八方射出
                    if (spikePS != null && spine != null)
                    {
                        var ep = new ParticleSystem.EmitParams();
                        var c = spine.position;
                        for (int i = 0; i < 26; i++)
                        {
                            var d = Random.onUnitSphere;
                            if (d.z > 0.8f) d.z = -d.z;   // 不從臉正前方射
                            ep.position = c + d * 0.3f;
                            ep.velocity = d * Random.Range(2.2f, 3.6f);
                            spikePS.Emit(ep, 1);
                        }
                    }
                    break;
            }
        }

        /// <summary>騎魚泡泡怪：從海螺口往前冒出 n 顆泡泡。</summary>
        void EmitBubbles(int n)
        {
            if (embers == null || tip == null) return;
            var ep = new ParticleSystem.EmitParams();
            var fwd = tip.forward;
            var mouth = tip.position + fwd * 0.16f;
            for (int i = 0; i < n; i++)
            {
                ep.position = mouth + Random.insideUnitSphere * 0.03f;
                ep.velocity = fwd * Random.Range(0.8f, 1.8f) + Random.insideUnitSphere * 0.3f + Vector3.up * 0.15f;
                ep.startSize = Random.Range(0.03f, 0.07f);
                ep.startLifetime = Random.Range(0.6f, 1.1f);
                ep.startColor = Color.Lerp(v.emberA, v.emberB, Random.value);
                embers.Emit(ep, 1);
            }
        }

        void AnimateSea(float dt)
        {
            clock += dt;
            if (clock > 3600f) clock -= 3600f;
            float hitK = 0f;
            if (hitT > 0f) { hitT -= dt; hitK = Mathf.Clamp01(hitT / HitDur); }
            if (dead) deadT += dt;

            // ---- 移動量（寄居蟹用步伐相位；其他用游動／拍翅相位）
            float speed = dead ? 0f : Mathf.Clamp(MoveSpeed, 0f, 4f);
            bool acting = phase != PhaseNone;
            walkAmt = Mathf.MoveTowards(walkAmt, acting ? 0f : Mathf.Clamp01(speed / 0.3f), dt * 6f);
            if (speed > 0.01f && !acting) walkPhase += dt * Mathf.PI * 2f * Mathf.Max(speed, 0.3f) / v.stride * v.step;
            else walkPhase = Mathf.MoveTowards(walkPhase, Mathf.Round(walkPhase / Mathf.PI) * Mathf.PI, dt * 6f);
            if (walkPhase > 1000f) walkPhase -= Mathf.PI * 300f;

            twitchT -= dt;
            if (twitchT <= 0f) { twitchT = Random.Range(0.6f, 1.8f); lookTarget = Random.Range(-30f, 30f); }
            lookY = Mathf.MoveTowards(lookY, lookTarget * (1f - walkAmt), dt * 90f);
            float breath = Mathf.Sin(clock * 2.2f);

            // ---- 預備／出手的權重（同植物）
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

            // ---- 游動／拍翅相位（移動、預備時加快；死亡後停下）
            float rate = v.step * (1f + 0.5f * walkAmt + 0.3f * wW) * (1f - 0.9f * deadE);
            swimPh += dt * rate;
            if (swimPh > 1000f) swimPh -= Mathf.PI * 300f;
            pulsePh += dt * (4f + 12f * wW);
            if (pulsePh > 1000f) pulsePh -= Mathf.PI * 300f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulsePh);
            float glow = Mathf.Max(wW, sW * 0.8f);

            switch (kind)
            {
                case 13: AnimLeaper(wW, sW, hitK, deadE); break;
                case 14: AnimSeabird(wW, sW, hitK, deadE); break;
                case 15: AnimRider(dt, wW, sW, hitK, deadE, breath); break;
                case 16: AnimHermit(wW, sW, hitK, deadE, breath); break;
                case 17: glow = AnimJelly(dt, wW, sW, hitK, deadE); break;
                default: AnimPuffer(wW, sW, hitK, deadE, breath); break;
            }

            UpdateSeaColors(hitK, deadE, pulse, glow);
        }

        void AnimLeaper(float wW, float sW, float hitK, float deadE)
        {
            float amp = (1f - wW) * (1f - 0.85f * sW) * (1f - deadE);
            float sw = 0.65f + 0.35f * walkAmt;   // 游動時擺得更大
            float s1 = Mathf.Sin(swimPh), s2 = Mathf.Sin(swimPh - 1.1f), s3 = Mathf.Sin(swimPh - 2.1f);
            float trem = Mathf.Sin(clock * 42f) * 2.5f * wW;   // 弓起時像壓緊的彈簧一樣發抖
            // 待機時偶爾「啪」地甩一下身體（撲騰）
            float flop = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(clock * 1.3f)), 12f) * (1f - walkAmt) * amp;
            body.localPosition = new Vector3(0f, 0.03f * flop, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, s1 * 5f * amp + 100f * deadE);   // 死亡：翻肚側躺
            body.localScale = new Vector3(1f, 1f, 1f - 0.08f * wW + 0.07f * sW) * (1f - 0.3f * deadE);
            spine.localRotation = Quaternion.Euler(-28f * wW + trem * 0.5f - 12f * hitK - 8f * flop, s1 * 7f * sw * amp + lookY * 0.12f * amp, 0f);
            segB.localRotation = Quaternion.Euler(38f * wW + trem + 18f * flop + 8f * hitK, -s2 * 18f * sw * amp, 0f);
            segC.localRotation = Quaternion.Euler(50f * wW + trem + 22f * flop, -s3 * 34f * sw * amp + 15f * deadE, 0f);
            float chomp = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(clock * 2.3f)), 4f);
            jaw.localRotation = Quaternion.Euler(4f + 10f * chomp * amp + 14f * wW + 26f * sW + 12f * hitK + 20f * deadE, 0f, 0f);
            // 胸鰭：平常撥水，出手時往身體收
            float fin = 108f + Mathf.Sin(swimPh * 1.4f) * 18f * amp + 45f * sW - 20f * wW;
            finL.localRotation = Quaternion.Euler(-10f * sW, 0f, fin);
            finR.localRotation = Quaternion.Euler(-10f * sW, 0f, -fin);
        }

        void AnimSeabird(float wW, float sW, float hitK, float deadE)
        {
            float fly = (1f - wW) * (1f - sW) * (1f - deadE);
            float fl = Mathf.Sin(swimPh), fl2 = Mathf.Sin(swimPh - 0.9f);
            float scream = 0.75f + 0.25f * Mathf.Sin(clock * 28f);
            // 翅膀：拍動（Z）＋往後收（Y）；預備＝高舉並抖動，出手＝收到身後俯衝
            float flap = (12f + 46f * fl) * fly + (72f + 5f * Mathf.Sin(clock * 22f)) * wW + 6f * sW - 50f * deadE;
            float outer = (6f + 26f * fl2) * fly + 22f * wW - 6f * sW - 20f * deadE;
            float sweep = 6f + 5f * fl * fly + 8f * wW + 70f * sW + 15f * deadE;
            float fold = 50f * sW;
            shR.localRotation = Quaternion.Euler(0f, sweep, flap);
            shL.localRotation = Quaternion.Euler(0f, -sweep, -flap);
            wrR.localRotation = Quaternion.Euler(0f, fold, outer);
            wrL.localRotation = Quaternion.Euler(0f, -fold, -outer);
            // 身體：每拍一下就往上一頓（往下拍時身體上升）；死亡＝失速往下掉、翻轉
            body.localPosition = new Vector3(0f, -0.035f * fl * fly - 0.35f * deadE * deadE, 0f);
            body.localRotation = Quaternion.Euler(-10f * wW + 40f * sW + 12f * hitK + 50f * deadE, lookY * 0.25f * fly, fl * 2.5f * fly + 40f * deadE);
            spine.localRotation = Quaternion.Euler(-2f * fl * fly, 0f, 0f);
            head.localRotation = Quaternion.Euler(-22f * wW + 10f * sW - 15f * hitK + 30f * deadE, lookY * 0.6f * fly, 0f);
            jaw.localRotation = Quaternion.Euler(3f + 36f * wW * scream + 12f * hitK + 15f * deadE, 0f, 0f);
            tailT.localRotation = Quaternion.Euler(-4f * fl * fly - 8f * wW + 12f * sW, 0f, 0f);
            // 腳：平常收在後面，俯衝時利爪往前伸
            float leg = 68f - 130f * sW + 10f * wW + 20f * deadE;
            hipL.localRotation = Quaternion.Euler(leg, 0f, -6f * sW);
            hipR.localRotation = Quaternion.Euler(leg, 0f, 6f * sW);
        }

        void AnimRider(float dt, float wW, float sW, float hitK, float deadE, float breath)
        {
            float live = 1f - deadE;
            float sw = Mathf.Sin(swimPh), bob = Mathf.Sin(swimPh * 0.5f);
            // 坐騎：在水面上一起一伏、尾巴與胸鰭不停擺動
            spine.localPosition = new Vector3(0f, 0.4f + 0.025f * bob * live - 0.18f * deadE * deadE, 0.06f * sW);
            spine.localRotation = Quaternion.Euler(3f * Mathf.Sin(swimPh * 0.5f + 1f) * live + 5f * sW - 8f * hitK + 15f * deadE, sw * 4f * live, 2f * bob * live + 35f * deadE);
            body.localScale = Vector3.one * (1f - 0.3f * deadE);
            tailT.localRotation = Quaternion.Euler(0f, sw * (24f + 10f * walkAmt) * live, 0f);
            float fin = 112f + Mathf.Sin(swimPh * 1.3f) * 25f * live;
            finL.localRotation = Quaternion.Euler(-15f, 0f, fin);
            finR.localRotation = Quaternion.Euler(-15f, 0f, -fin);
            // 騎士：預備＝身體後仰、把海螺舉到嘴邊、雙頰鼓起；出手＝往前一推
            neck.localRotation = Quaternion.Euler(breath * 2f - 12f * wW + 14f * sW - 15f * hitK + 45f * deadE, lookY * 0.4f * (1f - wW), -sw * 3f * live);
            head.localRotation = Quaternion.Euler(-10f * wW + 5f * sW + 15f * deadE, lookY * 0.3f * (1f - wW), 0f);
            float puff = 1f + 0.9f * wW * (0.92f + 0.08f * Mathf.Sin(clock * 9f)) + 0.15f * sW;
            cheek.localScale = cheekBase * puff;
            float armX = -35f + breath * 3f;
            armX += (-122f - armX) * wW;
            armX += (-92f - armX) * sW;
            armX = Mathf.Lerp(armX, -10f, deadE);
            shR.localRotation = Quaternion.Euler(armX, 0f, 10f - 32f * wW - 20f * sW);
            float aim = 25f * (1f - wW) * (1f - sW) - 15f * wW;   // 海螺口的仰角
            tip.localRotation = Quaternion.Euler(-armX + aim, 0f, 0f);   // 海螺保持水平朝前（殼頂靠嘴）
            shL.localRotation = Quaternion.Euler(-45f + breath * 2f + 15f * wW, 0f, -18f - 10f * wW);
            if (blowT > 0f && !dead)
            {
                blowT -= dt;
                if (Random.value < 0.6f) EmitBubbles(1);
            }
        }

        void AnimHermit(float wW, float sW, float hitK, float deadE, float breath)
        {
            float wa = walkAmt;
            float sinP = Mathf.Sin(walkPhase), cosP = Mathf.Cos(walkPhase);
            float bob = wa * v.bob * Mathf.Abs(cosP);
            body.localPosition = new Vector3(0f, bob - 0.02f * wW - 0.03f * sW - 0.1f * deadE * deadE, -0.03f * wW + 0.08f * sW);
            body.localRotation = Quaternion.Euler(0f, 0f, cosP * 3f * wa);
            body.localScale = Vector3.one * (1f - 0.3f * deadE);
            spine.localRotation = Quaternion.Euler(breath * 1.2f - 10f * wW + 4f * sW - 8f * hitK + 12f * deadE, lookY * 0.2f + sinP * 4f * wa, 0f);
            // 步足：左前＋右後一組、右前＋左後一組交替（往前後擺＋抬腳）
            float swing = 22f * wa, lift = 16f * wa, splay = 35f * deadE;
            hipR.localRotation = Quaternion.Euler(0f, -28f + sinP * swing, Mathf.Max(0f, cosP) * lift + splay);
            hipBL.localRotation = Quaternion.Euler(0f, 180f - 22f - sinP * swing, Mathf.Max(0f, cosP) * lift + splay);
            hipL.localRotation = Quaternion.Euler(0f, 180f + 28f + sinP * swing, Mathf.Max(0f, -cosP) * lift + splay);
            hipBR.localRotation = Quaternion.Euler(0f, 22f - sinP * swing, Mathf.Max(0f, -cosP) * lift + splay);
            // 大螯：預備＝高舉張開（發抖），出手＝往下砸並夾緊
            float armX = 6f + breath * 2f;
            armX += (-68f - armX) * wW;
            armX += (6f - armX) * sW;
            armX = Mathf.Lerp(armX, 25f, deadE);
            float trem = Mathf.Sin(clock * 36f) * 2.5f * wW;
            shR.localRotation = Quaternion.Euler(armX + trem, -18f + 8f * wW, -6f * wW);
            wrR.localRotation = Quaternion.Euler(-22f * wW + 12f * sW + 10f * hitK, 0f, 0f);
            float snip = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(clock * 1.7f)), 6f);
            float open = Mathf.Lerp(6f + 16f * snip, 42f + 4f * Mathf.Sin(clock * 30f), wW);
            open *= 1f - sW;
            open = Mathf.Lerp(open, 25f, deadE);
            jaw.localRotation = Quaternion.Euler(-open, 0f, 0f);
            shL.localRotation = Quaternion.Euler(12f + sinP * 10f * wa + 5f * Mathf.Sin(clock * 2f) - 10f * wW + 15f * deadE, 12f, 0f);
            wrL.localRotation = Quaternion.Euler(-8f * Mathf.Max(0f, Mathf.Sin(clock * 2.6f + 1f)), 0f, 0f);
        }

        /// <summary>水母：回傳「發亮程度」（含放電閃光）。</summary>
        float AnimJelly(float dt, float wW, float sW, float hitK, float deadE)
        {
            float live = 1f - deadE;
            // 傘一縮一放（收縮快、放鬆慢）：c＝收縮量 0..1
            float pr = Mathf.Sin(swimPh);
            float c = Mathf.Max(0f, pr); c *= c;
            float crack = wW * Mathf.Sin(clock * 47f) * 0.04f;
            body.localPosition = new Vector3(0f, (0.035f * pr - 0.03f * hitK) * live - 0.3f * deadE * deadE, 0f);
            body.localRotation = Quaternion.Euler(walkAmt * 12f + 15f * hitK + 25f * deadE, 0f, Mathf.Sin(clock * 0.7f) * 4f * live);
            float flat = 0.35f * deadE;
            head.localScale = new Vector3((1f - 0.13f * c + 0.08f * wW + 0.1f * sW + crack) * (1f + flat), (1f + 0.1f * c + 0.05f * wW - 0.15f * sW) * (1f - flat), (1f - 0.13f * c + 0.08f * wW + 0.1f * sW + crack) * (1f + flat));
            // 觸手：擺動、移動時往後拖、收縮時往外張；預備時抽搐，出手時大張
            for (int i = 0; i < 6; i++)
            {
                float ph = clock * 1.6f + i * 1.05f;
                float twitch = wW * Mathf.Sin(clock * 31f + i * 2.3f) * 6f;
                float a = (Mathf.Sin(ph) * 9f + twitch) * live;
                float spread = (14f * c + 26f * sW + 10f * wW) * live;
                tents[i * 2].localRotation = Quaternion.Euler(walkAmt * 22f * live + a, 0f, Mathf.Cos(ph * 0.8f) * 6f * live)
                    * Quaternion.AngleAxis(9f + spread, tentAx[i]) * Quaternion.Euler(0f, -60f * i, 0f);
                tents[i * 2 + 1].localRotation = Quaternion.Euler(Mathf.Sin(ph - 1.2f) * 16f * live + walkAmt * 14f * live + twitch, 0f, Mathf.Sin(ph * 0.7f + 2f) * 10f * live - 12f * c * live);
            }
            // 放電閃光：出手瞬間膨脹的白光球，約 0.35 秒內淡掉
            zapK = Mathf.MoveTowards(zapK, 0f, dt / 0.35f);
            float zk = zapK * zapK;
            zap.localScale = Vector3.one * ((0.3f + 1f * (1f - zapK)) * (zapK > 0f ? 1f : 0f) + 0.12f * wW * (0.6f + 0.4f * Mathf.Sin(clock * 40f))) * live;
            // 預備時劈啪冒電花
            crackT -= dt;
            if (wW > 0.15f && crackT <= 0f && !dead && embers != null)
            {
                crackT = Random.Range(0.03f, 0.12f) / Mathf.Max(0.3f, wW);
                var ep = new ParticleSystem.EmitParams();
                var ctr = head.position;
                int n = Random.Range(2, 5);
                for (int i = 0; i < n; i++)
                {
                    var d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f - 0.1f;
                    ep.position = ctr + Vector3.Scale(d.normalized, new Vector3(0.36f, 0.25f, 0.36f));
                    ep.velocity = d * Random.Range(0.3f, 0.9f);
                    ep.startSize = Random.Range(0.015f, 0.035f);
                    ep.startLifetime = Random.Range(0.12f, 0.3f);
                    ep.startColor = Color.Lerp(v.hot, v.emberA, Random.value);
                    embers.Emit(ep, 1);
                }
            }
            return Mathf.Max(Mathf.Max(wW, sW * 0.8f), zk);
        }

        void AnimPuffer(float wW, float sW, float hitK, float deadE, float breath)
        {
            float live = 1f - deadE;
            float shud = sW * Mathf.Sin(clock * 70f);   // 洩氣時的抖動
            float wob = wW * 0.03f * Mathf.Sin(clock * 11f);
            float s = 1f + 0.6f * wW + wob + 0.03f * breath * (1f - wW) + 0.07f * shud - 0.05f * sW;
            spine.localScale = Vector3.one * (s * (1f - 0.3f * deadE));
            spikesT.localScale = Vector3.one * (1f + 0.28f * wW + 0.12f * sW);
            body.localPosition = new Vector3(0f, 0.03f * Mathf.Sin(clock * 1.8f) * live - 0.25f * deadE * deadE, 0f);
            body.localRotation = Quaternion.Euler(walkAmt * 8f + shud * 6f + 12f * hitK, lookY * 0.3f * (1f - wW) * live, Mathf.Sin(clock * 1.1f) * 4f * live + shud * 10f + 170f * deadE);   // 死亡：翻肚
            float flick = 1f + wW;   // 鼓起時鰭拍得更快更急
            tailT.localRotation = Quaternion.Euler(0f, Mathf.Sin(swimPh) * 22f * flick * live, 0f);
            finD.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(swimPh * 1.3f) * 10f * live);
            float fin = 95f + Mathf.Sin(swimPh * 2.2f) * 28f * flick * live;
            finL.localRotation = Quaternion.Euler(0f, -20f, fin);
            finR.localRotation = Quaternion.Euler(0f, 20f, -fin);
        }

        void UpdateSeaColors(float hitK, float deadE, float pulse, float g)
        {
            bool elec = kind == 17;
            var lava = Color.Lerp(v.lavaDim, v.lava, 0.55f + 0.45f * pulse);
            lava = Color.Lerp(lava, v.hot, Mathf.Clamp01(g * 0.8f + hitK * 0.5f));
            if (elec && g > 0.05f && Random.value < 0.3f * g) lava = Color.white;   // 劈啪閃爍
            lava = Color.Lerp(lava, SeaDark, deadE);
            if (elec) lava.a = 0.8f;
            var hot = Color.Lerp(v.hot, Color.white, Mathf.Clamp01(0.3f * pulse + 0.5f * g));
            hot = Color.Lerp(hot, SeaDark, deadE);
            Color eye;
            if (elec)
            {
                // 傘緣光點：平常紫青交替脈動，預備時越來越白、閃爍
                float k = 0.5f + 0.5f * Mathf.Sin(clock * 5f);
                eye = Color.Lerp(Color.Lerp(v.lava, v.eye, k), Color.white, Mathf.Clamp01(g * (0.6f + 0.4f * Random.value) + hitK * 0.6f));
                eye = Color.Lerp(eye, SeaDark, deadE);
            }
            else eye = Color.Lerp(Color.Lerp(v.eye, Color.white, g * 0.4f), SeaDark, Mathf.Clamp01(deadE * 1.5f));

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
                c = Color.Lerp(c, SeaFlash, hitK * 0.6f);
                c = Color.Lerp(c, Color.Lerp(SeaDead, c, 0.25f), deadE * 0.85f);
                c.a = 1f;
                mpb.SetColor(ColorId, c);
                rockR[i].SetPropertyBlock(mpb);
            }
            // 半透明的傘與觸手：亮度隨脈動與蓄力變化，死亡時變暗變透明
            if (veilR.Length > 0)
            {
                float b = Mathf.Clamp01(0.72f + 0.12f * pulse + 0.3f * g + 0.3f * hitK) * (1f - 0.55f * deadE);
                float aK = (1f + 0.4f * g) * (1f - 0.6f * deadE);
                for (int i = 0; i < veilR.Length; i++)
                {
                    mpb.SetColor(ColorId, new Color(b, b, Mathf.Clamp01(b + 0.05f), Mathf.Clamp01(veilA[i] * aK)));
                    veilR[i].SetPropertyBlock(mpb);
                }
            }
            if (zapR != null)
            {
                float zk = Mathf.Max(zapK, 0.5f * plantW);
                mpb.SetColor(ColorId, new Color(v.hot.r, v.hot.g, v.hot.b, Mathf.Clamp01(zk * 0.85f) * (1f - deadE)));
                zapR.SetPropertyBlock(mpb);
            }
        }

        // ---------------------------------------------------------------- 海洋：召喚特效
        /// <summary>
        /// 海洋版召喚／浮出特效：地面湧出一圈深藍水面與白色泡沫環、往上噴的水花與水滴，再冒出一串泡泡，約 1.6 秒後自己清除。
        /// </summary>
        public static void SpawnFxSea(Vector3 worldFeetPos)
        {
            InitMats();
            SeaFx.Splash(worldFeetPos, 1.1f);
            var foam = new Color(0.92f, 0.97f, 1f);
            var water = new Color(0.3f, 0.7f, 1f);
            var go = new GameObject("Minion Spawn Fx Sea");
            // 本地 XY 平面 = 地面（本地 +Z 朝下）
            go.transform.SetPositionAndRotation(worldFeetPos + Vector3.up * 0.015f, Quaternion.Euler(90f, 0f, 0f));
            var anim = go.AddComponent<FxAnim>();
            anim.killAt = 1.6f;
            var root = go.transform;
            // 湧出的深藍水面＋旋轉的浪花鋸齒環＋往外擴的泡沫環
            anim.AddRing(root, SpellFx.Circle(32, 1f), true, 0.28f, new Color(0.06f, 0.2f, 0.38f, 0.7f), 0f, 1.1f, 0.3f, 0.5f, false);
            anim.AddRing(root, SpellFx.Zigzag(26, 0.78f, 1f), true, 0.045f, foam, 0f, 0.9f, 0.4f, 0.4f, false, 140f);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.05f, water, 0.1f, 0.7f, 0.2f, 0.95f);
            anim.AddRing(root, SpellFx.Circle(40, 1f), true, 0.07f, foam, 0.3f, 0.8f, 0.25f, 1.2f, false);

            var upT = new GameObject("Up").transform;
            upT.SetParent(root, false);
            upT.localRotation = Quaternion.Euler(180f, 0f, 0f);   // +Z 朝上
            var bubbles = SpellFx.NewPS(upT, "Bubbles", SpellFx.AddMat(SpellFx.TexSoft), 30, true);
            {
                var m = bubbles.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.85f, 1f, 0.8f), new Color(0.95f, 1f, 1f, 0.9f));
                SpellFx.Cone(bubbles, 20f, 0.3f);
                SpellFx.FadeInOut(bubbles);
                SpellFx.Drag(bubbles, 1.2f);
                SpellFx.Noise(bubbles, 0.25f, 2f);
                bubbles.Play(); bubbles.Emit(24);
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
