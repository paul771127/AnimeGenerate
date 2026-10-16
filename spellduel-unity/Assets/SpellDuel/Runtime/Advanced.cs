using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 進階職業（20 級在職業工會完成轉職試煉後選一個，選了不能換）。
    /// 每個基本職業有兩個方向差很多的進階職業：
    ///   能力變化（HP、MP、攻擊、減傷、爆擊、閃避、治療量）＋一個專屬的核心機制＋一欄全新的進階技能樹（3 個新技能）。
    /// 進階技能的 cls 仍是基本職業（沿用職業的放招方式：法師唸咒語、牧師比數字、獵人握拳→比五…），adv 欄位標示屬於哪個進階職業。
    /// 純 C#，可以在 Unity 外測試。
    /// </summary>
    public class AdvDef
    {
        public string id, baseCls, name, desc, core, weak;
        public Color color;
        public int hpAdd, mpAdd;
        public float atkMul = 1f, armor, crit, evade, healMul = 1f, regenMul = 1f;
        public string[] skills;          // 3 個進階技能（第一個轉職時直接學會）
        public int[] stats = new int[6];   // 轉職時能力值加成（力、智、體、敏、精、運；同 StatInfo 的順序）
    }

    public static class Advanced
    {
        public const int Level = 20;                 // 轉職等級
        public const int TrialFirstWave = 17, TrialLastWave = 20;   // 轉職試煉：熔岩地帶第 17～20 波（最後是 Boss 波）
        public const float TrialHpMul = 1.6f, TrialDmgMul = 1.4f;   // 試煉的敵人更硬、更痛
        public const int TrialGold = 500;            // 參加試煉的費用

        public static readonly Dictionary<string, AdvDef> All = new Dictionary<string, AdvDef>();
        static readonly Dictionary<string, AdvDef[]> byBase = new Dictionary<string, AdvDef[]>();

        public static AdvDef Get(string id) => id != null && All.TryGetValue(id, out var a) ? a : null;
        public static AdvDef[] Of(string cls) => byBase.TryGetValue(cls ?? "", out var a) ? a : new AdvDef[0];
        /// <summary>技能屬於哪個進階職業（不是進階技能＝null）</summary>
        public static AdvDef OfSkill(string skillId) => Skills.All.TryGetValue(skillId ?? "", out var s) ? Get(s.adv) : null;
        public static bool IsAdvSkill(string skillId) => OfSkill(skillId) != null;

        static Color C(string hex)
        {
            int v = System.Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        static void Add(AdvDef a)
        {
            All[a.id] = a;
            var list = new List<AdvDef>(Of(a.baseCls)) { a };
            byBase[a.baseCls] = list.ToArray();
        }

        static int[] S(int str = 0, int intl = 0, int vit = 0, int agi = 0, int spi = 0, int luk = 0) => new[] { str, intl, vit, agi, spi, luk };

        static Advanced()
        {
            // ---------------------------------------------------------------- 劍士
            Add(new AdvDef { id = "paladin", baseCls = "swordsman", name = "聖騎士", color = C("#fde047"), hpAdd = 35, atkMul = 0.9f, armor = 0.15f, stats = S(vit: 6),
                desc = "守護隊友的坦克", core = "守護光環：自己與 3m 內的隊友受到的傷害 −15%", weak = "攻擊 −10%",
                skills = new[] { "shieldbash", "oath", "sanction" } });
            Add(new AdvDef { id = "berserker", baseCls = "swordsman", name = "狂戰士", color = C("#ef4444"), hpAdd = 10, atkMul = 1.2f, armor = -0.15f, stats = S(str: 6),
                desc = "以血換傷的狂暴戰士", core = "怒氣：HP 越低傷害越高（HP 30% 以下傷害 +60%）", weak = "受到的傷害 +15%；格擋、鐵壁效果減半",
                skills = new[] { "whirlwind", "bloodrage", "undying" } });
            // ---------------------------------------------------------------- 獵人
            Add(new AdvDef { id = "marksman", baseCls = "archer", name = "神射手", color = C("#facc15"), hpAdd = -5, crit = 0.1f, stats = S(luk: 6),
                desc = "站樁狙擊的神射手", core = "鷹眼：站著不動每 2 秒疊 1 層（最多 3 層），每層爆擊率 +10%、射程 +20%；一移動就清空", weak = "HP −5；移動就失去鷹眼",
                skills = new[] { "pierce", "deadmark", "arrowrain" } });
            Add(new AdvDef { id = "ranger", baseCls = "archer", name = "遊俠", color = C("#34d399"), hpAdd = 15, atkMul = 0.9f, stats = S(agi: 5, vit: 2),
                desc = "機動與陷阱專家", core = "游擊：蓄力時可以自由移動（不受直徑 1m 限制）；陷阱上限加倍", weak = "攻擊 −10%",
                skills = new[] { "falcon", "rollshot", "trapchain" } });
            // ---------------------------------------------------------------- 法師
            Add(new AdvDef { id = "archmage", baseCls = "mage", name = "大魔導士", color = C("#a855f7"), hpAdd = -5, mpAdd = 50, atkMul = 1.1f, stats = S(intl: 8),
                desc = "超長咒語、超高傷害", core = "咒文延長：唸完咒語後再唸增幅咒「" + AmpChant + "」→ 這一招傷害 +50%", weak = "HP −5；念錯咒語就放不出來",
                skills = new[] { "tjudge", "frostfield", "manashield" } });
            Add(new AdvDef { id = "summoner", baseCls = "mage", name = "召喚師", color = C("#fb923c"), hpAdd = 15, mpAdd = 20, atkMul = 0.7f, stats = S(spi: 6),
                desc = "靠召喚物作戰", core = "契約：最多同時 2 隻召喚物，召喚物自動攻擊；有召喚物在場時自己受到的傷害 −15%", weak = "本人的直接傷害 −30%",
                skills = new[] { "firespirit", "golem", "detonate" } });
            // ---------------------------------------------------------------- 刺客
            Add(new AdvDef { id = "ninja", baseCls = "assassin", name = "忍者", color = C("#64748b"), evade = 0.15f, stats = S(agi: 6),
                desc = "手印與替身", core = "忍術手印：忍術要依序比出手印（數字序列）才能放出；閃避率 +15%", weak = "手印操作難，傷害普通",
                skills = new[] { "substitute", "clone", "fireseal" } });
            Add(new AdvDef { id = "shadowdancer", baseCls = "assassin", name = "影舞者", color = C("#c026d3"), hpAdd = -5, crit = 0.15f, stats = S(str: 6),
                desc = "連擊與爆發", core = "連殺：伏擊命中後 3 秒內，伏擊技能冷卻歸零、蓄力時間 −70%", weak = "HP −5，防禦很脆",
                skills = new[] { "execute", "shadowdance", "poisonfog" } });
            // ---------------------------------------------------------------- 牧師
            Add(new AdvDef { id = "bishop", baseCls = "priest", name = "主教", color = C("#fef9c3"), hpAdd = 5, mpAdd = 40, atkMul = 0.7f, healMul = 1.2f, stats = S(spi: 8),
                desc = "全隊治療與復活", core = "祈禱領域：站著不動時，自己與 3m 內的隊友每秒回復最大 HP 的 1.5%", weak = "攻擊 −30%",
                skills = new[] { "revive", "massheal", "sanctuary" } });
            Add(new AdvDef { id = "inquisitor", baseCls = "priest", name = "審判者", color = C("#f59e0b"), hpAdd = 20, atkMul = 1.25f, healMul = 0.6f, stats = S(str: 3, intl: 4),
                desc = "聖光攻擊特化的戰鬥牧師", core = "信仰：每次命中累積 1 點信仰（最多 5 點），滿了下一招攻擊傷害 ×2", weak = "治療量 −40%",
                skills = new[] { "holylance", "chastise", "heavenpunish" } });
        }

        /// <summary>大魔導士的增幅咒（唸完技能咒語後再唸）</summary>
        public const string AmpChant = "以吾之名增幅吧";

        /// <summary>
        /// 套用進階職業的能力變化（在基本數值算好之後呼叫）。
        /// 能力值加成由角色養成（CharacterProfile.TreeStat）處理；這裡只處理 HP、MP、攻擊、減傷、爆擊、閃避、治療量。
        /// </summary>
        public static void ApplyStats(Fighter f, AdvDef a)
        {
            f.adv = a?.id;
            if (a == null) return;
            float hpRatio = f.maxHp > 0 ? f.hp / f.maxHp : 1f, mpRatio = f.maxMp > 0 ? f.mp / f.maxMp : 1f;
            f.maxHp = Mathf.Max(10, f.maxHp + a.hpAdd);
            f.maxMp = Mathf.Max(10, f.maxMp + a.mpAdd);
            f.atkMul *= a.atkMul;
            f.gearReduce = Mathf.Clamp(f.gearReduce + a.armor, -0.5f, 0.6f);
            f.critChance = Mathf.Clamp(f.critChance + a.crit, 0f, 0.75f);
            f.evadeChance = Mathf.Clamp(f.evadeChance + a.evade, 0f, 0.5f);
            f.healMul = a.healMul;
            f.regen *= a.regenMul;
            f.hp = Mathf.Max(1f, f.maxHp * hpRatio); f.mp = f.maxMp * mpRatio;
        }

        /// <summary>忍術手印（數字序列）；不是忍術＝null</summary>
        public static int[] SealOf(string skillId) => skillId switch
        {
            "substitute" => new[] { 3, 2 },
            "clone" => new[] { 2, 3, 4 },
            "fireseal" => new[] { 2, 4, 5 },
            _ => null,
        };
        public static string SealText(int[] seq) => seq == null ? "" : string.Join("→", System.Array.ConvertAll(seq, x => x.ToString()));
    }
}
