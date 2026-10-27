using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 進階職業（30 級在職業工會完成轉職試煉後選一個，選了不能換）。
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
        public const int Level = 30;                 // 轉職等級
        public const int TrialFirstWave = 27, TrialLastWave = 30;   // 轉職試煉：海上礁石第 27～30 波（最後是 Boss 波）
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
                desc = "以聖盾接住風暴，替同伴守住歸途", core = "守護光環：自己與 3m 內的隊友受到的傷害 −15%", weak = "攻擊 −10%",
                skills = new[] { "shieldbash", "oath", "sanction" } });
            // 劍仙（內部 id 沿用 berserker：存檔、外觀資料都用這個 id）：以氣御劍的輸出職業
            Add(new AdvDef { id = "berserker", baseCls = "swordsman", name = "劍仙", color = C("#7dd3fc"), hpAdd = 0, atkMul = 1.25f, armor = -0.1f, crit = 0.05f, stats = S(str: 4, agi: 2),
                desc = "以氣御劍，萬劍隨心——專精輸出的劍士", core = "劍意：攻擊 +25%、爆擊 +5%。技能樹三個流派：輸出（御劍術、萬劍歸宗）、防禦（劍罡護體）、輔助（劍陣）", weak = "受到的傷害 +10%",
                skills = new[] { "jianlai", "yujian", "wanjian" } });
            // ---------------------------------------------------------------- 獵人
            Add(new AdvDef { id = "marksman", baseCls = "archer", name = "神射手", color = C("#facc15"), hpAdd = -5, crit = 0.1f, stats = S(luk: 6),
                desc = "屏息守望遠方，以一箭貫穿戰局", core = "鷹眼：站著不動每 2 秒疊 1 層（最多 3 層），每層爆擊率 +5%、射程 +20%；一移動就清空", weak = "HP −5；移動就失去鷹眼",
                skills = new[] { "pierce", "deadmark", "arrowrain" } });
            Add(new AdvDef { id = "ranger", baseCls = "archer", name = "遊俠", color = C("#34d399"), hpAdd = 15, atkMul = 0.9f, stats = S(agi: 5, vit: 2),
                desc = "與獵鷹追風而行，用箭與陷阱掌握獵場", core = "游擊：蓄力時可以自由移動（不受直徑 1m 限制）；陷阱上限加倍", weak = "攻擊 −10%",
                skills = new[] { "falcon", "rollshot", "trapchain" } });
            // ---------------------------------------------------------------- 法師
            Add(new AdvDef { id = "archmage", baseCls = "mage", name = "大魔導士", color = C("#a855f7"), hpAdd = -5, mpAdd = 50, atkMul = 1.1f, stats = S(intl: 8),
                desc = "延長古老咒文，喚來雷霆與冰封之境", core = "咒文延長：唸完咒語後再唸增幅咒「" + AmpChant + "」→ 這一招傷害 +50%", weak = "HP −5；念錯咒語就放不出來",
                skills = new[] { "tjudge", "frostfield", "manashield" } });
            Add(new AdvDef { id = "summoner", baseCls = "mage", name = "召喚師", color = C("#fb923c"), hpAdd = 15, mpAdd = 20, atkMul = 0.7f, stats = S(spi: 6),
                desc = "與火靈和石像締約，讓元素並肩作戰", core = "契約：最多同時 2 隻召喚物（火靈、石像、冰靈、治癒妖精、收服的魔物），自動作戰；有召喚物在場時自己受到的傷害 −15%。用自己的攻擊打中召喚物＝餵養：幫牠回血，並讓牠的攻擊、血量 +1%（召喚物消失後重新召喚就歸零）", weak = "本人的直接傷害 −30%",
                skills = new[] { "firespirit", "golem", "detonate" } });
            // ---------------------------------------------------------------- 刺客
            Add(new AdvDef { id = "ninja", baseCls = "assassin", name = "忍者", color = C("#64748b"), hpAdd = -10, regenMul = 0.75f, evade = 0.15f, stats = S(agi: 6),
                desc = "苦無與忍術交錯，在一瞬間連續出手", core = "快速施放：普攻改成遠距射苦無（技能樹可讓苦無機率附帶火／冰／雷）；手裏劍、雷遁幾乎不用蓄力；忍術（替身、分身、火遁）要比手印；物理閃避率 +15%", weak = "HP −10、MP 回復 −25%：不擅長持久戰",
                skills = new[] { "substitute", "clone", "fireseal" } });
            Add(new AdvDef { id = "shadowdancer", baseCls = "assassin", name = "影舞者", color = C("#c026d3"), hpAdd = -5, crit = 0.15f, stats = S(str: 6),
                desc = "以連擊追逐破綻，在毒霧間舞出致命一瞬", core = "連殺：伏擊命中後 3 秒內，伏擊技能冷卻歸零、蓄力時間 −70%。技能樹學影分身（電腦操作、分走一半 HP）、影殺（強力伏擊）、致命劇毒與萬毒引爆（疊毒再一次引爆）", weak = "HP −5，防禦很脆",
                skills = new[] { "execute", "shadowdance", "poisonfog" } });
            // ---------------------------------------------------------------- 牧師
            Add(new AdvDef { id = "bishop", baseCls = "priest", name = "主教", color = C("#fef9c3"), hpAdd = 5, mpAdd = 40, atkMul = 0.7f, healMul = 1.2f, stats = S(spi: 8),
                desc = "為同伴留下燈火，以祈禱喚回將熄的生命", core = "祈禱領域：站著不動時，自己與 3m 內的隊友每秒回復最大 HP 的 1.5%。普攻神聖飛彈打中隊友（含召喚物）會幫他回血；技能樹學聖域障壁（擋 3 次攻擊）", weak = "攻擊 −30%",
                skills = new[] { "revive", "massheal", "sanctuary" } });
            Add(new AdvDef { id = "inquisitor", baseCls = "priest", name = "審判者", color = C("#f59e0b"), hpAdd = 20, atkMul = 1.25f, healMul = 0.6f, stats = S(str: 3, intl: 4),
                desc = "將信仰凝成聖槍，迎向吞沒光明的敵人", core = "信仰：每次命中累積 1 點信仰（最多 5 點），滿了下一招攻擊傷害 ×2。強化審判：技能樹降低審判冷卻、提高審判之地傷害；天罰＝範圍與傷害都是審判 2 倍的審判之地；造成的傷害可以轉成護盾", weak = "治療量 −40%",
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
            f.gearReduce = Mathf.Clamp(f.gearReduce + a.armor, -0.5f, Fighter.MaxReduce);
            f.magicReduce = Mathf.Clamp(f.magicReduce + a.armor, -0.5f, Fighter.MaxReduce);
            f.critChance = Mathf.Clamp(f.critChance + a.crit, 0f, 0.75f);
            f.evadeChance = Mathf.Max(0f, f.evadeChance + a.evade);   // 閃避不設上限
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
