using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>敵人招式的判定形狀</summary>
    public enum EsShape { Fan, Line, Circle, JumpRing, DuckArc, Shot, Volley, Dash, Back, Retreat, Cross, Counter, SelfShield, SelfGuard, HealAlly }

    /// <summary>補師、輔助型、人形敵人的招式（Docs/SUPPORT_HUMAN_SKILLS_PROPOSAL.md，使用者已確認）</summary>
    public sealed class EnemySkillDef
    {
        public string id, name;
        public EsShape shape;
        public float minD, maxD = 99f, cd, warn, size, width = 0.25f, angle = 90f, mul;
        public float hpBelow = 1.01f;   // 自己的 HP 低於這個比例才放（保命技）
        public bool breakable;          // 讀條被打中會中斷（招牌技、保命技）；一般攻擊不會
        public bool basic;              // 一般攻擊
        // 一般小怪、菁英的第二招（Docs/ENEMY_MOVES_PROPOSAL.md）
        public int pose;                // 直線／投射物也可以用跳（1）或蹲（2）躲
        public int count, aKind = -1;   // 連射幾發（0＝預設 3）；aKind≥0：傷害以這種小怪原本那一招為基準
        public float gap, speed, hpAbove; // 連射間隔、投射物速度（0＝預設）、自己 HP 高於這個比例才放
        public float healPct, healBelow = 1f, healRange = 4f; public int healN = 1, healMin = 1;   // HealAlly：回幾成、同伴 HP 低於多少、範圍、最多幾隻、至少幾隻受傷才放
        public int React => shape == EsShape.JumpRing || pose == 1 ? 1 : shape == EsShape.DuckArc || pose == 2 ? 2 : shape == EsShape.SelfShield || shape == EsShape.SelfGuard || shape == EsShape.Counter || shape == EsShape.HealAlly ? -1 : 0;   // 要玩家做什麼：0 移動、1 跳、2 蹲、−1 不用
    }

    /// <summary>一次招式的命中（房主排好，連線時送給隊友；每支手機判定自己的玩家）</summary>
    [Serializable]
    public sealed class EnemyCue
    {
        public int id, owner, shape, targetPid = -1;
        public string skill;
        public Vector3 origin, dir;
        public float size, width, angle, hit, warnStart, speed;   // speed＞0：投射物（hit＝發射時間，碰到的時間看玩家站在線上哪裡）
        public int dmg, pose;   // pose：1 跳、2 蹲也能躲（直線、投射物）
        public bool back;   // 背後突襲：被盯上的人畫面上有方向提示
        [NonSerialized] public bool judged, sawPose, cancelled;
        [NonSerialized] public float contact = -1f;
        public float Launch => hit;
    }

    public static class EnemySkills
    {
        static EnemySkillDef S(string id, string name, EsShape shape, float minD, float maxD, float cd, float warn, float size, float mul, bool breakable = false, bool basic = false, float angle = 90f, float hpBelow = 1.01f)
            => new EnemySkillDef { id = id, name = name, shape = shape, minD = minD, maxD = maxD, cd = cd, warn = warn, size = size, mul = mul, breakable = breakable, basic = basic, angle = angle, hpBelow = hpBelow };

        /// <summary>每一種敵人的招式（不含治療、護盾、加速、戰旗、召喚這些原本就有的讀條）</summary>
        public static readonly Dictionary<int, EnemySkillDef[]> ByVariant = new Dictionary<int, EnemySkillDef[]>
        {
            [121] = new[] { S("eh_121_sunward", "折光護身", EsShape.SelfShield, 0f, 2f, 12f, 1.4f, 0.15f, 0f, breakable: true) },
            [122] = new[] { S("eh_122_dustmantle", "藥霧披身", EsShape.SelfGuard, 0f, 2f, 12f, 1.4f, 0.25f, 0f, breakable: true, hpBelow: 0.5f) },
            [123] = new[] { S("eh_123_sandseal", "落砂封印", EsShape.Circle, 1f, 4f, 10f, 1.6f, 0.35f, 0.6f, breakable: true) },
            [124] = new[] { S("eh_124_sandlunge", "砂爪撲咬", EsShape.Line, 0f, 1.4f, 3f, 1.2f, 1.4f, 1f, basic: true) },
            [141] = new[] { S("eh_141_rootlash", "垂枝掃線", EsShape.Line, 0f, 2f, 10f, 1.4f, 1.8f, 0.5f, breakable: true) },
            [142] = new[] { S("eh_142_burstbubble", "逆潮泡彈", EsShape.Shot, 2f, 5f, 8f, 1.4f, 5.5f, 0.6f, breakable: true) },
            [143] = new[] { S("eh_143_groundbeat", "震地重拍", EsShape.JumpRing, 0f, 2f, 11f, 1.8f, 2f, 0.8f, breakable: true) },
            [144] = new[] { S("eh_144_polecleave", "旗桿橫拒", EsShape.DuckArc, 0f, 1.6f, 11f, 1.6f, 1.6f, 0.8f, breakable: true, angle: 90f) },
            [151] = new[] { S("eh_151_swordcut", "傭兵短斬", EsShape.Fan, 0f, 1.1f, 2.6f, 1.2f, 1.1f, 1f, basic: true), S("eh_151_flankrush", "赤爐側進", EsShape.Dash, 1.2f, 2.5f, 9f, 1.6f, 1.8f, 1.35f) },
            [152] = new[] { S("eh_152_bolt", "定弦弩矢", EsShape.Shot, 2f, 6f, 3f, 1.2f, 6.5f, 1f, basic: true), S("eh_152_triplebolt", "鎖道三連弩", EsShape.Volley, 3f, 6f, 10f, 1.6f, 6.5f, 0.45f) },
            [153] = new[] { S("eh_153_emberorb", "爐芯火珠", EsShape.Shot, 2f, 6f, 3f, 1.3f, 6.5f, 0.85f, basic: true), S("eh_153_kilncircle", "灰燼爐印", EsShape.Circle, 1f, 4f, 10f, 1.8f, 0.4f, 1.4f, breakable: true) },
            [154] = new[] { S("eh_154_dagger", "斥候試刃", EsShape.Fan, 0f, 1f, 2.7f, 1.2f, 1f, 0.9f, basic: true), S("eh_154_mistbackstab", "霧葉背襲", EsShape.Back, 0f, 2f, 11f, 1.8f, 1.4f, 1.4f, breakable: true) },
            [155] = new[] { S("eh_155_arrow", "林間直矢", EsShape.Shot, 2f, 6f, 3f, 1.2f, 6.5f, 1f, basic: true), S("eh_155_retreatshot", "退葉返射", EsShape.Retreat, 0f, 2.5f, 9f, 1.6f, 6.5f, 1.1f) },
            [156] = new[] { S("eh_156_thornstaff", "藤杖輕拒", EsShape.Fan, 0f, 1.1f, 3f, 1.2f, 1.1f, 0.6f, basic: true) },
            [157] = new[] { S("eh_157_sabercut", "浪口單斬", EsShape.Fan, 0f, 1.1f, 2.6f, 1.2f, 1.1f, 1f, basic: true), S("eh_157_decksweep", "甲板平掃", EsShape.DuckArc, 0f, 1.5f, 10f, 1.6f, 1.5f, 1.25f, breakable: true, angle: 180f) },
            [158] = new[] { S("eh_158_waterlance", "貝音水槍", EsShape.Shot, 2f, 6f, 3f, 1.3f, 6.5f, 0.9f, basic: true), S("eh_158_tidalring", "退潮地輪", EsShape.JumpRing, 0f, 2f, 11f, 1.8f, 2f, 1.2f, breakable: true) },
            [159] = new[] { S("eh_159_dagger", "索鉤試刺", EsShape.Line, 0f, 1f, 2.7f, 1.2f, 1.1f, 0.9f, basic: true), S("eh_159_smokepierce", "雨幕穿刺", EsShape.Back, 0f, 2f, 11f, 1.8f, 1.4f, 1.3f, breakable: true) },
            [160] = new[] { S("eh_160_swordthrust", "冠軍直劍", EsShape.Line, 0f, 1.2f, 2.8f, 1.2f, 1.3f, 1f, basic: true), S("eh_160_guardcounter", "桂冠應戰", EsShape.Counter, 0f, 1.8f, 11f, 1.4f, 1.8f, 1.4f) },
            [161] = new[] { S("eh_161_javelin", "順風標槍", EsShape.Shot, 2f, 6f, 3.2f, 1.3f, 6.5f, 1f, basic: true), S("eh_161_highcast", "越盾高投", EsShape.Circle, 2f, 4f, 10f, 1.8f, 0.35f, 1.3f, breakable: true) },
            [162] = new[] { S("eh_162_staffjab", "聖徽杖擊", EsShape.Fan, 0f, 1.1f, 3f, 1.2f, 1.1f, 0.6f, basic: true) },
            [163] = new[] { S("eh_163_dagger", "沙匪短刃", EsShape.Line, 0f, 1f, 2.7f, 1.2f, 1.1f, 0.9f, basic: true), S("eh_163_sandtrailstrike", "沙痕越襲", EsShape.Back, 0f, 2f, 11f, 1.8f, 1.4f, 1.3f, breakable: true) },
            [164] = new[] { S("eh_164_sandbolt", "漏砂咒矢", EsShape.Shot, 2f, 6f, 3f, 1.3f, 6.5f, 0.9f, basic: true), S("eh_164_hourglass", "倒砂刻印", EsShape.Circle, 1f, 4f, 10f, 1.8f, 0.4f, 1.25f, breakable: true) },
            [165] = new[] { S("eh_165_shortbow", "砂原平射", EsShape.Shot, 2f, 6f, 3f, 1.2f, 6.5f, 1f, basic: true), S("eh_165_crossstep", "換位雙矢", EsShape.Cross, 2f, 5f, 10f, 1.6f, 6.5f, 0.5f) },
        };
        public static bool Has(int variant) => ByVariant.ContainsKey(variant);
        public static EnemySkillDef Find(string id)
        {
            foreach (var l in ByVariant.Values) foreach (var s in l) if (s.id == id) return s;
            foreach (var l in ByKind.Values) foreach (var s in l) if (s.id == id) return s;
            return null;
        }

        // ------------------------------------------------------------ 一般小怪、特殊敵人、菁英的第二招（使用者確認：菁英是另外加的第三招）
        static EnemySkillDef K(int kind, string slug, string name, EsShape shape, float minD, float maxD, float cd, float warn, float size, float mul,
            float angle = 120f, int pose = 0, int count = 0, float gap = 0f, float speed = 0f, bool breakable = false, float hpAbove = 0f)
            => new EnemySkillDef { id = $"ek_{kind}_{slug}", name = name, shape = shape, minD = minD, maxD = maxD, cd = cd, warn = warn, size = size, mul = mul, width = 0.24f,
                angle = angle, pose = pose, count = count, gap = gap, speed = speed, breakable = breakable, hpAbove = hpAbove, aKind = kind };
        static EnemySkillDef H(int kind, string slug, string name, float cd, float warn, float pct, float below, float range, int n = 1, int min = 1)
            => new EnemySkillDef { id = $"ek_{kind}_{slug}", name = name, shape = EsShape.HealAlly, maxD = 99f, cd = cd, warn = warn, breakable = true, healPct = pct, healBelow = below, healRange = range, healN = n, healMin = min, aKind = kind };

        /// <summary>依小怪種類（Battle.MinionNames 0～22、EliteCatalog 23～47）的第二招；數值照提案（A＝這種怪原本那一招的傷害）</summary>
        public static readonly Dictionary<int, EnemySkillDef[]> ByKind = new Dictionary<int, EnemySkillDef[]>
        {
            [0] = new[] { K(0, "emberclaw", "熾爪掠火", EsShape.Line, 0f, 1.6f, 9f, 1.5f, 1.8f, 0.85f) },
            [1] = new[] { K(1, "lowring", "低空火輪", EsShape.Shot, 1.5f, 5f, 11f, 1.8f, 5f, 0.9f, pose: 1) },
            [2] = new[] { K(2, "hornrake", "側角挑地", EsShape.Line, 0f, 2f, 12f, 1.6f, 2f, 1f) },
            [3] = new[] { K(3, "powdertoss", "火藥試拋", EsShape.Circle, 1f, 4f, 12f, 1.7f, 0.3f, 0.6f, hpAbove: 0.4f) },
            [4] = new[] { K(4, "rollstone", "滾石碾線", EsShape.Shot, 2f, 5f, 11f, 1.7f, 5f, 1f, pose: 1, speed: 2.5f) },
            [5] = new[] { K(5, "twinmark", "雙點校射", EsShape.Volley, 2f, 5f, 13f, 1.9f, 5f, 0.6f, count: 2, gap: 0.35f) },
            [6] = new[] { K(6, "riftseal", "裂隙落印", EsShape.Circle, 1f, 4f, 13f, 1.8f, 0.3f, 1.05f, breakable: true) },
            [7] = new[] { K(7, "leafsweep", "橫葉掃露", EsShape.DuckArc, 0f, 2.2f, 12f, 1.7f, 2.2f, 0.85f) },
            [8] = new[] { K(8, "rootwave", "挑根波", EsShape.JumpRing, 0f, 2.8f, 13f, 1.8f, 2.8f, 1f) },
            [9] = new[] { K(9, "sproutseal", "纏足芽印", EsShape.Circle, 1f, 4f, 12f, 1.8f, 0.3f, 0.9f, breakable: true) },
            [10] = new[] { K(10, "tonguepin", "探舌釘線", EsShape.Line, 1.2f, 3f, 10f, 1.6f, 3f, 1f) },
            [11] = new[] { K(11, "capgust", "冠蓋掀風", EsShape.DuckArc, 0f, 2.4f, 13f, 1.8f, 2.4f, 0.8f, angle: 360f) },
            [12] = new[] { K(12, "podburst", "釘莢連珠", EsShape.Volley, 2f, 5f, 12f, 1.7f, 5f, 0.575f, count: 2, gap: 0.3f) },
            [13] = new[] { K(13, "finslide", "背鰭滑切", EsShape.Dash, 1f, 2.5f, 10f, 1.6f, 2.5f, 0.95f, pose: 1) },
            [14] = new[] { K(14, "wingskim", "平翼掠肩", EsShape.Line, 1.5f, 4f, 12f, 1.8f, 4f, 1f, pose: 2) },
            [15] = new[] { K(15, "bubbledrop", "破泡落潮", EsShape.Circle, 1.5f, 4f, 13f, 1.8f, 0.3f, 1.05f) },
            [16] = new[] { K(16, "shellclamp", "橫鉗護殼", EsShape.DuckArc, 0f, 2f, 11f, 1.7f, 2f, 1f) },
            [17] = new[] { K(17, "boltpoint", "落雷觸點", EsShape.Circle, 1f, 4f, 13f, 1.8f, 0.3f, 1.1f, breakable: true) },
            [18] = new[] { K(18, "spinering", "低潮刺輪", EsShape.JumpRing, 0f, 2.6f, 13f, 1.8f, 2.6f, 0.95f) },
            [19] = new[] { K(19, "rootspear", "根脈穿刺", EsShape.Line, 1f, 3.5f, 14f, 2f, 3.5f, 1.1f, breakable: true) },
            [20] = new[] { K(20, "tidelance", "潮槍吐息", EsShape.Line, 1.5f, 5f, 14f, 1.9f, 5f, 1.15f) },
            [21] = new[] { K(21, "chainpin", "鎖錘釘地", EsShape.Circle, 1.5f, 3f, 12f, 1.7f, 0.35f, 1.2f) },
            [22] = new[] { K(22, "platevolley", "裂板直射", EsShape.Volley, 1.5f, 5f, 12f, 1.8f, 5f, 0.4167f, count: 3, gap: 0.25f) },
            [23] = new[] { K(23, "crownstomp", "冠爐震步", EsShape.JumpRing, 0f, 2.8f, 12f, 1.9f, 2.8f, 1.05f) },
            [24] = new[] { K(24, "twinbolt", "熔栓雙發", EsShape.Volley, 2f, 5f, 12f, 1.8f, 5f, 0.6f, count: 2, gap: 0.35f) },
            [25] = new[] { K(25, "shardseal", "黑晶碎印", EsShape.Circle, 1f, 4f, 13f, 1.9f, 0.3f, 1.1f, breakable: true) },
            [26] = new[] { H(26, "kilnmend", "餘火回爐", 16f, 2f, 0.18f, 0.65f, 4f) },
            [27] = new[] { K(27, "ashshoulder", "灰刃探肩", EsShape.DuckArc, 0f, 2.2f, 12f, 1.7f, 2.2f, 1.05f) },
            [28] = new[] { K(28, "mossroot", "苔甲落根", EsShape.JumpRing, 0f, 3f, 13f, 1.9f, 3f, 1.1f) },
            [29] = new[] { K(29, "thornpoint", "棘釘釘點", EsShape.Circle, 2f, 5f, 12f, 1.8f, 0.3f, 1.15f) },
            [30] = new[] { K(30, "dreamsweep", "夢傘平掃", EsShape.DuckArc, 0f, 2.5f, 13f, 1.9f, 2.5f, 1f, breakable: true) },
            [31] = new[] { H(31, "mycelmend", "菌絲接生", 16f, 2f, 0.2f, 0.6f, 4f) },
            [32] = new[] { K(32, "wingcut", "翼下切風", EsShape.Line, 1.5f, 4f, 12f, 1.8f, 4f, 1.1f, pose: 2) },
            [33] = new[] { K(33, "reefknock", "礁鉗叩浪", EsShape.JumpRing, 0f, 3f, 13f, 1.9f, 3f, 1.1f) },
            [34] = new[] { K(34, "sailspear", "帆壓水矛", EsShape.Line, 2f, 5f, 12f, 1.8f, 5f, 1.2f) },
            [35] = new[] { K(35, "tideorb", "潮電落珠", EsShape.Circle, 1f, 4f, 13f, 1.9f, 0.3f, 1.1f, breakable: true) },
            [36] = new[] { H(36, "tidechorus", "回潮合音", 17f, 2.1f, 0.12f, 0.7f, 3f, n: 2, min: 2) },
            [37] = new[] { K(37, "tideslash", "潮刃平斬", EsShape.DuckArc, 0f, 2.4f, 11f, 1.8f, 2.4f, 1.15f, angle: 180f) },
            [38] = new[] { K(38, "shieldstomp", "盾足擂場", EsShape.JumpRing, 0f, 3f, 11f, 1.8f, 3f, 1.2f) },
            [39] = new[] { K(39, "tripleshot", "鎖弩三拍", EsShape.Volley, 2f, 5f, 11f, 1.8f, 5f, 0.4333f, count: 3, gap: 0.25f) },
            [40] = new[] { K(40, "sanddrop", "砂鐘墜印", EsShape.Circle, 1f, 4f, 12f, 1.8f, 0.35f, 1.2f, breakable: true) },
            [41] = new[] { H(41, "drumheal", "穩陣鼓療", 16f, 2f, 0.14f, 0.65f, 3f, n: 2, min: 2) },
            [42] = new[] { K(42, "redshoulder", "赤刃抹肩", EsShape.DuckArc, 0f, 2.5f, 10f, 1.7f, 2.5f, 1.2f, angle: 180f) },
            [43] = new[] { K(43, "crystalstomp", "晶脊踏裂", EsShape.JumpRing, 0f, 3f, 11f, 1.8f, 3f, 1.25f) },
            [44] = new[] { K(44, "crystalline", "砂晶穿線", EsShape.Volley, 2f, 5f, 11f, 1.8f, 5f, 0.65f, count: 2, gap: 0.3f) },
            [45] = new[] { K(45, "sandseal", "砂漏埋印", EsShape.Circle, 1f, 4f, 12f, 1.8f, 0.35f, 1.2f, breakable: true) },
            [46] = new[] { H(46, "stonemend", "石巢縫補", 16f, 2f, 0.22f, 0.55f, 4f) },
            [47] = new[] { K(47, "galetail", "裂風尾刀", EsShape.DuckArc, 0f, 2.6f, 10f, 1.7f, 2.6f, 1.25f, angle: 180f) },
        };

        /// <summary>這隻敵人用哪一組新招式：補師／輔助／人形看種類 id；其他（含天候怪換色版）看小怪種類；刺客、寶箱怪、天空敵人不用</summary>
        public static EnemySkillDef[] For(Fighter m)
        {
            if (m == null) return null;
            if (m.variant > 0 && ByVariant.TryGetValue(m.variant, out var l)) return l;
            if (m.assassin || m.treasure || m.variant > 0 && MonsterVariants.IsSky(m.variant)) return null;
            return ByKind.TryGetValue(m.minionKind, out var k) ? k : null;
        }
        /// <summary>A：這種小怪原本那一招的傷害（菁英用原型的傷害，再乘菁英自己的傷害倍率＝m.dmgMul）</summary>
        public static int KindBaseDamage(int kind)
        {
            if (EliteCatalog.IsElite(kind)) return EliteCatalog.BaseDamage(EliteCatalog.Get(kind).baseKind);
            if (Battle.IsSpecial(kind)) return Skills.All[Battle.SpecialSkillId(kind)].damage;
            string id = kind == 1 ? "m_fireball" : kind == 3 ? "m_bomb" : kind == 7 ? "m_venom" : kind == 14 ? "m_dive" : null;
            return id != null && Skills.All.TryGetValue(id, out var sd) ? sd.damage : EliteCatalog.BaseDamage(kind);
        }

        public const float ShotSpeed = 3f, RingTime = 0.9f, BodyR = 0.25f, PoseBefore = 0.3f, PoseAfter = 0.15f;
        public const float SameReactGap = 2f, OppositeGap = 1f, Recover = 0.6f, MinFirst = 2f;

        /// <summary>這一招在 hitTime 會不會打到站在 feet 的人（不含跳／蹲的姿勢判定）</summary>
        public static bool Inside(EnemyCue c, Vector3 feet)
        {
            var d = feet - c.origin; d.y = 0f;
            var dir = c.dir; dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward; dir.Normalize();
            switch ((EsShape)c.shape)
            {
                case EsShape.Fan: case EsShape.DuckArc:
                    return d.magnitude <= c.size + BodyR && (d.magnitude < 0.15f || Vector3.Angle(dir, d) <= c.angle * 0.5f + 8f);
                case EsShape.Circle: return d.magnitude <= c.size + BodyR * 0.8f;
                case EsShape.JumpRing: return d.magnitude <= c.size;
                default:
                {
                    float along = Vector3.Dot(d, dir);
                    if (along < -BodyR || along > c.size + BodyR) return false;
                    return Mathf.Abs(Vector3.Dot(d, new Vector3(dir.z, 0f, -dir.x))) <= c.width * 0.5f + BodyR;
                }
            }
        }
        /// <summary>這個人碰到的時間：投射物看他站在線上哪裡、地波看離中心多遠</summary>
        public static float ContactTime(EnemyCue c, Vector3 feet)
        {
            var d = feet - c.origin; d.y = 0f;
            if (c.speed > 0f) { var dir = c.dir; dir.y = 0f; dir.Normalize(); return c.hit + Mathf.Clamp(Vector3.Dot(d, dir), 0f, c.size) / c.speed; }
            if (c.shape == (int)EsShape.JumpRing) return c.hit + RingTime * Mathf.Clamp01(d.magnitude / Mathf.Max(0.1f, c.size));
            return c.hit;
        }
        public static bool NeedsJump(EnemyCue c) => c.shape == (int)EsShape.JumpRing || c.pose == 1;
        public static bool NeedsDuck(EnemyCue c) => c.shape == (int)EsShape.DuckArc || c.pose == 2;
    }

    public partial class Battle
    {
        public readonly List<EnemyCue> enemyCues = new List<EnemyCue>();
        public event Action<EnemyCue> OnEnemyCue, OnEnemyCueCancel;
        int enemyCueIds;
        public static bool HasEnemySkills(int variant) => EnemySkills.Has(variant);
        /// <summary>新手關（1-1～1-3）：一般小怪不放第二招</summary>
        public bool secondMovesOff;
        /// <summary>這隻正在放新招式（房主看讀條；隊友看還沒出手的預警）</summary>
        public bool EsCasting(Fighter m)
        {
            if (m == null) return false;
            if (m.esCast != null) return true;
            foreach (var c in enemyCues) if (c.owner == m.minionId && !c.judged && !c.cancelled && now < c.hit + 0.05f) return true;
            return false;
        }

        // ------------------------------------------------------------ 房主：選招、讀條、出手
        /// <summary>這個人還能不能排一個要身體反應的招式（全場統一：同時只有一個；兩次跳／兩次蹲隔 2 秒；相反動作隔 1 秒）</summary>
        bool CanSchedule(Fighter t, int react, float hitAt)
        {
            if (t == null) return false;
            if (now < t.esBusyUntil) return false;
            if (react == 1) return hitAt - t.esLastJump >= EnemySkills.SameReactGap && hitAt - t.esLastDuck >= EnemySkills.OppositeGap;
            if (react == 2) return hitAt - t.esLastDuck >= EnemySkills.SameReactGap && hitAt - t.esLastJump >= EnemySkills.OppositeGap;
            return true;
        }
        void Reserve(Fighter t, int react, float hitAt)
        {
            if (t == null || react < 0) return;
            t.esBusyUntil = hitAt + EnemySkills.Recover;
            if (react == 1) t.esLastJump = hitAt; else if (react == 2) t.esLastDuck = hitAt;
        }

        int EsDamage(Fighter m, EnemySkillDef s) => Mathf.Max(1, Mathf.RoundToInt((s.aKind >= 0 ? EnemySkills.KindBaseDamage(s.aKind) : Skills.All["m_claw"].damage) * m.dmgMul * s.mul));

        /// <summary>HealAlly：範圍內 HP 低於門檻的同伴（不含自己），最傷的排前面</summary>
        List<Fighter> HealTargets(Fighter m, EnemySkillDef s)
        {
            var l = new List<Fighter>();
            foreach (var o in minions)
                if (o != m && o.Alive && !o.treasure && o.hp < o.maxHp * s.healBelow && FlatDistance(o.Feet, m.Feet) <= s.healRange) l.Add(o);
            l.Sort((a, b) => (a.hp / Mathf.Max(1f, a.maxHp)).CompareTo(b.hp / Mathf.Max(1f, b.maxHp)));
            return l;
        }

        /// <summary>目標在施法者的背側（目標面向的反方向 ±70°）</summary>
        static bool BehindOf(Fighter m, Fighter tgt)
        {
            var tf = Fighter.Flat(tgt.forward); if (tf == Vector3.zero) return false;
            var to = Fighter.Flat(m.Feet - tgt.Feet); if (to == Vector3.zero) return false;
            return Vector3.Angle(-tf, to) <= 70f;
        }

        /// <summary>每隻有招式的敵人每幀先叫這裡：讀條中就處理讀條（回傳 true）；有招可放就開始讀條（回傳 true）</summary>
        bool UpdateEnemySkill(Fighter m, Fighter tgt, float dist, float dt)
        {
            if (m.esCast != null) { TickEnemyCast(m, tgt); return true; }
            if (m.windupUntil > 0f || tgt == null || m.humState == 1) return false;
            if (now - m.esBornAt < EnemySkills.MinFirst) return false;
            var list = EnemySkills.For(m); if (list == null) return false;
            bool inside = minionInside == null || minionInside(m.Feet);
            if (!inside) return false;
            EnemySkillDef pick = null;
            // 招牌技優先，再來一般攻擊
            for (int pass = 0; pass < 2 && pick == null; pass++)
                foreach (var s in list)
                {
                    if (s.basic != (pass == 1)) continue;
                    if (!m.esCd.TryGetValue(s.id, out var ready)) { if (s.aKind >= 0) { m.esCd[s.id] = now + 2f + (float)rng.NextDouble() * s.cd * 0.5f; continue; } }   // 第二招：剛看到時先等一下（不會一進場就放）
                    else if (now < ready) continue;
                    if (s.hpAbove > 0f && m.hp < m.maxHp * s.hpAbove) continue;
                    if (s.shape == EsShape.HealAlly) { if (HealTargets(m, s).Count < s.healMin) continue; pick = s; break; }
                    if (dist < s.minD || dist > s.maxD) continue;
                    if (m.hp > m.maxHp * s.hpBelow) continue;
                    if (s.shape == EsShape.Back && !BehindOf(m, tgt)) continue;
                    if (s.shape == EsShape.SelfShield && dist > s.maxD && m.hp > m.maxHp * 0.5f) continue;
                    if (s.React >= 0 && !CanSchedule(tgt, s.React, now + s.warn + (s.shape == EsShape.JumpRing ? EnemySkills.RingTime : 0f))) continue;
                    pick = s; break;
                }
            if (pick == null) return false;
            StartEnemyCast(m, tgt, pick);
            return true;
        }

        void StartEnemyCast(Fighter m, Fighter tgt, EnemySkillDef s)
        {
            m.esCast = s; m.esTarget = tgt; m.esCastAt = now; m.esCues.Clear(); m.esCountered = false;
            m.windupUntil = now + s.warn;
            var dir = Fighter.Flat(tgt.Feet - m.Feet); if (dir == Vector3.zero) dir = Fighter.Flat(m.forward); if (dir == Vector3.zero) dir = Vector3.forward;
            m.lockFwd = dir; m.forward = dir;
            float hit = now + s.warn;
            int dmg = EsDamage(m, s);
            switch (s.shape)
            {
                case EsShape.SelfShield: case EsShape.SelfGuard:
                    Emit("eskill", m.Chest, new Color(1f, 0.9f, 0.5f), $"✨ {m.name}：{s.name}！打斷牠");
                    return;
                case EsShape.Counter:
                    // 先架盾 0.8 秒（減傷 50%）；這段時間被打中才反擊（另給完整預警）
                    m.windupUntil = now + 0.8f; m.esGuardUntil = now + 0.8f;
                    Emit("eskill", m.Chest, new Color(1f, 0.85f, 0.3f), $"🛡 {m.name}架盾了——別打牠，打了會反擊！");
                    return;
                case EsShape.Circle: AddCue(m, s, tgt.Feet, dir, hit, dmg, tgt); break;
                case EsShape.JumpRing: AddCue(m, s, m.Feet, dir, hit, dmg, tgt); break;
                case EsShape.Fan: case EsShape.DuckArc: case EsShape.Line: case EsShape.Dash: case EsShape.Back:
                    AddCue(m, s, m.Feet, dir, hit, dmg, tgt); break;
                case EsShape.Shot: case EsShape.Retreat: AddCue(m, s, m.Feet, dir, hit, dmg, tgt, s.speed > 0f ? s.speed : EnemySkills.ShotSpeed); break;
                case EsShape.Volley: for (int i = 0, n = s.count > 0 ? s.count : 3; i < n; i++) AddCue(m, s, m.Feet, dir, hit + (s.gap > 0f ? s.gap : 0.35f) * i, dmg, tgt, s.speed > 0f ? s.speed : EnemySkills.ShotSpeed); break;
                case EsShape.HealAlly:
                {
                    var hs = HealTargets(m, s);
                    if (hs.Count > 0) Emit("esfx", hs[0].Chest, new Color(0.55f, 1f, 0.5f), $"heal|{m.minionId}|{hs[0].minionId}|0");
                    Emit("eskill", m.Chest, new Color(0.6f, 1f, 0.6f), $"✨ {m.name}：{s.name}！打斷牠");
                    return;
                }
                case EsShape.Cross:
                {
                    // 敵人橫移 0.8m 邊換位邊射：兩條箭都瞄準同一個舊位置（一次側步躲兩條）
                    var side = new Vector3(dir.z, 0f, -dir.x) * (rng.Next(2) == 0 ? 0.8f : -0.8f);
                    var o2 = m.Feet + side;
                    AddCue(m, s, m.Feet, dir, hit, dmg, tgt, EnemySkills.ShotSpeed);
                    var d2 = Fighter.Flat(tgt.Feet - o2); if (d2 == Vector3.zero) d2 = dir;
                    AddCue(m, s, o2, d2, hit + 0.7f, dmg, tgt, EnemySkills.ShotSpeed);
                    m.esCrossTo = o2;
                    break;
                }
            }
            float last = 0f; foreach (var c in m.esCues) last = Mathf.Max(last, EnemySkills.ContactTime(c, tgt.Feet));
            Reserve(tgt, s.React, last);
            if (s.shape == EsShape.Back) Emit("eskill", m.Chest, new Color(0.9f, 0.2f, 0.35f), null);
        }

        void AddCue(Fighter m, EnemySkillDef s, Vector3 origin, Vector3 dir, float hit, int dmg, Fighter tgt, float speed = 0f)
        {
            var c = new EnemyCue
            {
                id = ++enemyCueIds, owner = m.minionId, skill = s.id, shape = (int)s.shape, origin = new Vector3(origin.x, 0f, origin.z), dir = dir,
                size = s.size, width = s.width, angle = s.angle, hit = hit, warnStart = now, speed = speed, dmg = dmg,
                back = s.shape == EsShape.Back, pose = s.pose, targetPid = tgt != null ? (tgt == player ? (player.pid >= 0 ? player.pid : 0) : tgt.pid) : -1,
            };
            if (c.shape == (int)EsShape.Retreat || c.shape == (int)EsShape.Cross || c.shape == (int)EsShape.Volley) c.shape = (int)EsShape.Shot;
            if (c.shape == (int)EsShape.Dash || c.shape == (int)EsShape.Back) c.shape = (int)EsShape.Line;
            enemyCues.Add(c); m.esCues.Add(c); OnEnemyCue?.Invoke(c);
        }

        void TickEnemyCast(Fighter m, Fighter tgt)
        {
            var s = m.esCast;
            m.forward = m.lockFwd;
            float t = now - m.esCastAt;
            if (s.shape == EsShape.Counter)
            {
                if (!m.esCountered)
                {
                    if (now < m.esGuardUntil) return;
                    EndEnemyCast(m, s);   // 沒被打：收盾，不反擊
                    return;
                }
                if (now < m.windupUntil) return;
                EndEnemyCast(m, s); return;
            }
            if (s.shape == EsShape.Retreat && t >= s.warn - 0.4f && !m.esMoved)
            {
                // 最後 0.4 秒往後滾 0.8m（只動敵人；箭道不變）
                var back = m.Feet - m.lockFwd * 0.8f;
                if (minionInside == null || minionInside(back)) m.head = new Vector3(back.x, m.head.y, back.z);
                m.esMoved = true;
            }
            if (s.shape == EsShape.Cross && t >= s.warn && !m.esMoved)
            {
                if (minionInside == null || minionInside(m.esCrossTo)) m.head = new Vector3(m.esCrossTo.x, m.head.y, m.esCrossTo.z);
                m.esMoved = true;
            }
            if (now < m.windupUntil) return;
            switch (s.shape)
            {
                case EsShape.SelfShield:
                    m.shieldUntil = now + 4f; m.shieldLeft = Mathf.Max(m.shieldLeft, Mathf.RoundToInt(m.maxHp * s.size));
                    Emit("enemyheal", m.Chest, new Color(1f, 0.9f, 0.5f), $"🛡 護盾 {m.shieldLeft}");
                    Emit("esfx", m.Chest, new Color(1f, 0.9f, 0.5f), $"shield|{m.minionId}|{m.minionId}|4");
                    break;
                case EsShape.SelfGuard:
                    if (m.esGuardBuffUntil <= now) m.damageTakenMul *= 1f - s.size;
                    m.esGuardBuffUntil = now + 3f;
                    Emit("enemyheal", m.Chest, new Color(0.7f, 1f, 0.8f), "🌫 減傷");
                    break;
                case EsShape.HealAlly:
                {
                    int healed = 0;
                    foreach (var o in HealTargets(m, s))
                    {
                        if (healed >= s.healN) break;
                        float amt = Mathf.Min(o.maxHp - o.hp, o.maxHp * s.healPct); o.hp += amt; healed++;
                        Emit("enemyheal", o.Chest, new Color(0.45f, 1f, 0.55f), $"+{amt:0}");
                        Emit("esfx", o.Chest, new Color(0.55f, 1f, 0.5f), $"heal|{m.minionId}|{o.minionId}|0");
                    }
                    break;
                }
                case EsShape.Dash:
                {
                    // 短衝到線的盡頭（不穿過目標）
                    var end = m.Feet + m.lockFwd * Mathf.Max(0f, Mathf.Min(s.size, tgt != null ? FlatDistance(tgt.Feet, m.Feet) - 0.5f : s.size));
                    if (minionInside == null || minionInside(end)) m.head = new Vector3(end.x, m.head.y, end.z);
                    break;
                }
            }
            Emit("minionattack", m.Feet, Color.white, null);
            if (m.esCues.Count == 0 || m.esCues.TrueForAll(c => now >= c.hit)) EndEnemyCast(m, s);
        }

        void EndEnemyCast(Fighter m, EnemySkillDef s)
        {
            m.esCd[s.id] = now + s.cd;
            m.esCast = null; m.esTarget = null; m.esMoved = false; m.esGuardUntil = 0f; m.esCountered = false;
            m.windupUntil = 0f; m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + 0.6f);
            m.esCues.Clear();
        }

        /// <summary>讀條被打斷、施法者死亡：取消還沒打到的預警（也通知隊友）</summary>
        void CancelEnemyCast(Fighter m, string why)
        {
            var s = m.esCast; if (s == null) return;
            foreach (var c in m.esCues) if (!c.judged && now < c.hit) { c.cancelled = true; OnEnemyCueCancel?.Invoke(c); }
            enemyCues.RemoveAll(c => c.cancelled);
            if (m.esTarget != null) m.esTarget.esBusyUntil = Mathf.Min(m.esTarget.esBusyUntil, now);
            EndEnemyCast(m, s);
            if (why != null) Emit("healbreak", m.Chest, new Color(1f, 0.85f, 0.4f), why);
        }

        /// <summary>Damage 叫：讀條中被打（招牌技會被打斷；桂冠應戰架盾時被打 → 反擊）</summary>
        void OnSkillUserHurt(Fighter f, float dmg)
        {
            if (f.esCast == null || dmg <= 0f) return;
            if (f.esCast.shape == EsShape.Counter)
            {
                if (!f.esCountered && now < f.esGuardUntil)
                {
                    f.esCountered = true;
                    var tgt = f.esTarget ?? player;
                    var dir = Fighter.Flat(tgt.Feet - f.Feet); if (dir == Vector3.zero) dir = f.lockFwd;
                    f.lockFwd = dir; f.windupUntil = now + f.esCast.warn;
                    AddCue(f, f.esCast, f.Feet, dir, now + f.esCast.warn, EsDamage(f, f.esCast), tgt);
                    Reserve(tgt, 0, now + f.esCast.warn);
                    Emit("eskill", f.Chest, new Color(1f, 0.5f, 0.3f), $"⚔ {f.name}撤盾反擊！側步閃開");
                }
                return;
            }
            if (f.esCast.breakable && f.windupUntil > 0f) CancelEnemyCast(f, $"{f.esCast.name}被打斷！");
        }

        /// <summary>每隻有招式的敵人：增益到期（藥霧減傷）、死掉時取消預警（UpdateMinions 叫）</summary>
        void TickEnemySkillState(Fighter m)
        {
            if (m.esGuardBuffUntil > 0f && (now >= m.esGuardBuffUntil || !m.Alive)) { m.damageTakenMul /= 0.75f; m.esGuardBuffUntil = 0f; }
            if (!m.Alive && m.esCast != null) CancelEnemyCast(m, null);
        }

        /// <summary>沒有招可放時的走位（人形、砂靈不再用原本小怪的攻擊）：近戰貼到 0.9m、遠程保持 3～5m</summary>
        void EnemySkillMove(Fighter m, Fighter tgt, float dist, float dt)
        {
            if (!EnemySkills.ByVariant.TryGetValue(m.variant, out var list)) return;
            float want = 0.9f; foreach (var s in list) if (s.basic) want = s.maxD <= 1.5f ? 0.9f : 4f;
            float speed = minionSpeed * MinionSpeed[m.minionKind] * m.speedMul * EnemySpeedScale * (m.human > 0 ? HumanAgile : 1f);
            var to = Fighter.Flat(tgt.Feet - m.Feet);
            if (to == Vector3.zero) return;
            float diff = dist - want;
            if (Mathf.Abs(diff) < 0.2f) { m.forward = to; return; }
            var step = to * Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff), speed * dt);
            var next = m.Feet + step;
            if (minionInside == null || minionInside(next) || !minionInside(m.Feet)) m.head += step;
            m.forward = to;
        }

        // ------------------------------------------------------------ 隊友端：收到房主排好的
        public void AddRemoteEnemyCue(EnemyCue c, float localHit)
        {
            if (c == null || enemyCues.Exists(x => x.id == c.id)) return;
            float shift = localHit - c.hit;
            c.hit = localHit; c.warnStart += shift;
            enemyCues.Add(c);
        }
        public void CancelRemoteEnemyCue(int id) { foreach (var c in enemyCues) if (c.id == id) c.cancelled = true; enemyCues.RemoveAll(c => c.cancelled); }

        // ------------------------------------------------------------ 每支手機：判定自己的玩家
        void UpdateEnemyCues(float dt)
        {
            if (enemyCues.Count == 0) return;
            var me = player;
            bool alive = me != null && me.Alive && !stageCleared;
            for (int i = 0; i < enemyCues.Count; i++)
            {
                var c = enemyCues[i];
                if (c.judged) continue;
                // 施法者已經不在了（隊友端：代理消失）：還沒發射的取消；已經飛出去的投射物照飛
                var owner = MinionById(c.owner);
                if ((owner == null || !owner.Alive) && now < c.hit) { c.cancelled = true; continue; }
                c.contact = alive ? EnemySkills.ContactTime(c, me.Feet) : c.hit;
                if (now < c.contact - EnemySkills.PoseBefore) continue;
                if (alive && EnemySkills.NeedsJump(c) && me.airborneUntil > now) c.sawPose = true;
                if (alive && EnemySkills.NeedsDuck(c) && me.crouching) c.sawPose = true;
                bool needPose = EnemySkills.NeedsJump(c) || EnemySkills.NeedsDuck(c);
                if (now < c.contact + (needPose ? EnemySkills.PoseAfter : 0f)) continue;
                c.judged = true;
                if (!alive) continue;
                if (!EnemySkills.Inside(c, me.Feet)) continue;
                if (needPose && c.sawPose)
                {
                    BodyDodge(me, EnemySkills.NeedsDuck(c));
                    Emit("evade", me.Chest, new Color(0.6f, 1f, 0.8f), EnemySkills.NeedsJump(c) ? "跳過了！" : "蹲下躲過！");
                    continue;
                }
                var sk = Skills.All["m_claw"];
                incoming = sk;
                ApplyDamageAndEffect(me, c.dmg, EffectKind.None, 0f, 0f, me.Chest, owner);
                incoming = null;
                Emit("eskillhit", me.Chest, Color.red, null);
            }
            enemyCues.RemoveAll(c => c.cancelled || c.judged && now > c.contact + 1.5f);
        }
    }
}
