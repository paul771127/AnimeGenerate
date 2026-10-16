using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SpellDuel
{
    // ======================================================================================
    // 角色養成（目前只在無盡模式使用；其他模式維持原本的數值與技能）
    //   能力值：力量、智力、體質、敏捷、精神、幸運（各職業初始不同）。升一級 +8 能力點、+1 技能點。
    //   能力值越高，再往上加需要的點數越多（每 10 點多 1）。
    //   技能樹：每個職業一開始只有 3 個基本技能，用技能點習得新技能、強化技能、加能力值、學特殊能力。
    //   裝備：武器、頭部、身體、鞋子、飾品；有職業與能力值限制，稀有度越高越強、限制越嚴。
    // 純 C#（不呼叫 Unity 原生功能），可以在 Unity 外測試。存檔用 PlayerPrefs（由 Store 委派）。
    // ======================================================================================

    public enum Stat { Str, Int, Vit, Agi, Spi, Luk }

    public static class StatInfo
    {
        public const int Count = 6;
        public static readonly string[] Names = { "力量", "智力", "體質", "敏捷", "精神", "幸運" };
        public static readonly string[] Icons = { "💪", "📘", "❤", "⚡", "🔮", "🍀" };
        public static readonly Color[] Colors =
        {
            new Color(1f, 0.5f, 0.4f), new Color(0.55f, 0.7f, 1f), new Color(0.45f, 0.95f, 0.5f),
            new Color(1f, 0.85f, 0.3f), new Color(0.8f, 0.6f, 1f), new Color(0.4f, 0.95f, 0.85f),
        };

        /// <summary>各職業的初始能力值（力、智、體、敏、精、運）</summary>
        public static int[] Base(string cls) => cls switch
        {
            "swordsman" => new[] { 12, 3, 12, 6, 4, 3 },
            "archer" => new[] { 10, 3, 7, 11, 5, 5 },
            "assassin" => new[] { 11, 3, 5, 12, 4, 6 },
            "mage" => new[] { 2, 14, 5, 6, 9, 4 },
            "priest" => new[] { 3, 11, 8, 5, 10, 3 },
            _ => new[] { 6, 6, 6, 6, 6, 6 },
        };

        /// <summary>這個職業的攻擊靠哪個能力值（物理職業＝力量、法術職業＝智力）</summary>
        public static Stat AttackStat(string cls) => cls == "mage" || cls == "priest" ? Stat.Int : Stat.Str;

        /// <summary>把能力值從 v 加到 v+1 需要的點數：1～9 點 1 點、10～19 點 2 點、20～29 點 3 點…</summary>
        public static int Cost(int v) => 1 + Mathf.Max(0, v) / 10;

        /// <summary>每個能力值的具體效果說明（atk＝這個職業是不是靠這個能力值攻擊）</summary>
        public static string Effect(Stat s, bool atk) => s switch
        {
            Stat.Str => atk ? "攻擊傷害 +2%／點" : "（不是這個職業的攻擊能力值）HP +1／點",
            Stat.Int => atk ? "攻擊傷害 +2%／點、治療量 +2%／點、MP 上限 +2／點" : "MP 上限 +2／點、治療量 +2%／點",
            Stat.Vit => "HP 上限 +5／點、受到的傷害 -0.4%／點（最多 -30%）",
            Stat.Agi => "蓄力時間 -1%／點（最多 -40%）、側跨無敵時間 +1%／點",
            Stat.Spi => "MP 回復 +3%／點、冷卻時間 -0.8%／點（最多 -35%）",
            _ => "爆擊率 +0.6%／點（爆擊＝傷害 ×2）、掉寶機率與稀有度提升",
        };
    }

    // ---------------------------------------------------------------- 技能樹
    public enum NodeKind { Learn, Mod, Stat, Passive }
    public enum ModField { Damage, Cooldown, Charge, Multi, EffectDur, EffectDps, Heal, BuffAmount, BuffReduce, BuffDur, Cost }
    public enum PassiveKind { Lifesteal, Crit, Dodge, MpOnHit, Regen, Burn, Poison, LastStand, Freeze, Evade, FastCast, Overcharge, NaturalRegen, QuickCd, Atk, Armor, Hp }

    public class TreeNode
    {
        public string id, name, desc;
        public NodeKind kind;
        public string skill;            // Learn / Mod 的技能
        public ModField field; public float per;   // Mod：每級加多少（Damage／Heal 為比例、Cooldown／Charge／Cost 為減少比例）
        public Stat stat;               // Stat：每級 +per 點
        public PassiveKind passive;     // Passive：每級 +per
        public int max = 1, level = 1;  // 最高等級、需要的角色等級
        public string req;              // 前置節點（要至少 1 級）
        public int col, row;            // 畫面上的位置（3 欄）
    }

    public static class SkillTrees
    {
        /// <summary>每個職業一開始會的 3 個基本技能</summary>
        public static string[] Starters(string cls) => cls switch
        {
            "swordsman" => new[] { "slash", "thrust", "block" },
            "archer" => new[] { "quickshot", "triple", "snaretrap" },
            "mage" => new[] { "fire", "wind", "ice" },
            "assassin" => new[] { "knife", "shadow", "smoke" },
            "priest" => new[] { "holy", "prayheal", "cleanse" },
            _ => new string[0],
        };

        static readonly Dictionary<string, List<TreeNode>> trees = new Dictionary<string, List<TreeNode>>();

        public static List<TreeNode> Of(string cls)
        {
            if (trees.TryGetValue(cls, out var t)) return t;
            t = Build(cls);
            trees[cls] = t;
            return t;
        }

        public static TreeNode Find(string cls, string id) => Of(cls).Find(n => n.id == id);

        /// <summary>角色所有的技能樹節點：基本職業＋進階職業（轉職後）</summary>
        public static IEnumerable<TreeNode> AllOf(string cls, string adv)
        {
            foreach (var n in Of(cls)) yield return n;
            if (!string.IsNullOrEmpty(adv)) foreach (var n in Of(adv)) yield return n;
        }

        /// <summary>前置節點（要先點滿）：同一欄的上一格＋另外指定的前置；不再看等級</summary>
        public static List<TreeNode> Prereqs(string cls, TreeNode n)
        {
            var list = new List<TreeNode>();
            TreeNode above = null;
            foreach (var o in Of(cls)) if (o.col == n.col && o.row < n.row && (above == null || o.row > above.row)) above = o;
            if (above != null) list.Add(above);
            if (n.req != null) { var r = Find(cls, n.req); if (r != null && r != above) list.Add(r); }
            return list;
        }

        static TreeNode Mod(string id, string name, string skill, ModField f, float per, int max, int col, int row, string req = null, int lv = 1) =>
            new TreeNode { id = id, name = name, kind = NodeKind.Mod, skill = skill, field = f, per = per, max = max, col = col, row = row, req = req, level = lv };
        static TreeNode Learn(string id, string skill, int col, int row, string req, int lv) =>
            new TreeNode { id = id, name = "習得：" + (Skills.All.TryGetValue(skill, out var s) ? s.name : skill), kind = NodeKind.Learn, skill = skill, max = 1, col = col, row = row, req = req, level = lv };
        static TreeNode StatN(string id, string name, Stat st, int col, int row, string req = null, int lv = 1) =>
            new TreeNode { id = id, name = name, kind = NodeKind.Stat, stat = st, per = 2, max = 5, col = col, row = row, req = req, level = lv };
        static TreeNode Pas(string id, string name, PassiveKind p, float per, int max, int col, int row, string req = null, int lv = 1) =>
            new TreeNode { id = id, name = name, kind = NodeKind.Passive, passive = p, per = per, max = max, col = col, row = row, req = req, level = lv };

        static List<TreeNode> Build(string cls)
        {
            // 三欄：左＝攻擊、中＝新技能、右＝能力／特殊能力；越下面越後期
            switch (cls)
            {
                case "swordsman": return new List<TreeNode>
                {
                    Mod("sw_slash", "斬擊強化", "slash", ModField.Damage, 0.12f, 3, 0, 0),
                    Mod("sw_thrust", "突刺強化", "thrust", ModField.Damage, 0.12f, 3, 0, 1, "sw_slash"),
                    Mod("sw_block", "格擋精通", "block", ModField.BuffReduce, 0.05f, 3, 0, 2),
                    Learn("sw_wave", "wave", 1, 0, "sw_slash", 3),
                    Mod("sw_wave2", "劍氣分裂", "wave", ModField.Multi, 1f, 2, 1, 1, "sw_wave", 6),
                    Learn("sw_iron", "ironwall", 1, 2, "sw_block", 5),
                    Mod("sw_iron2", "鐵壁強化", "ironwall", ModField.BuffAmount, 15f, 3, 1, 3, "sw_iron", 8),
                    Learn("sw_counter", "counter", 1, 4, "sw_iron", 10),
                    StatN("sw_str", "力量訓練", Stat.Str, 2, 0),
                    StatN("sw_vit", "體魄訓練", Stat.Vit, 2, 1),
                    Pas("sw_steal", "吸血劍", PassiveKind.Lifesteal, 0.03f, 3, 2, 2, "sw_str", 8),
                    Pas("sw_last", "不屈", PassiveKind.LastStand, 0.2f, 1, 2, 3, "sw_vit", 12),
                    Pas("sw_regen", "自然回復", PassiveKind.NaturalRegen, 0.01f, 5, 0, 3),
                    Learn("sw_taunt", "taunt", 2, 4, "sw_last", 1),
                };
                case "archer": return new List<TreeNode>
                {
                    Mod("ar_quick", "速射強化", "quickshot", ModField.Damage, 0.12f, 3, 0, 0),
                    Mod("ar_triple", "多重箭", "triple", ModField.Multi, 1f, 2, 0, 1, "ar_quick", 4),
                    Mod("ar_snare", "捕獸夾強化", "snaretrap", ModField.EffectDur, 0.5f, 3, 0, 2),
                    Learn("ar_snipe", "snipe", 1, 0, "ar_quick", 3),
                    Mod("ar_snipe2", "狙擊強化", "snipe", ModField.Damage, 0.15f, 3, 1, 1, "ar_snipe", 6),
                    Learn("ar_blast", "blasttrap", 1, 2, "ar_snare", 5),
                    Mod("ar_blast2", "陷阱大師", "blasttrap", ModField.Damage, 0.15f, 3, 1, 3, "ar_blast", 8),
                    Learn("ar_focus", "focus", 1, 4, "ar_snipe2", 10),
                    StatN("ar_agi", "鷹眼", Stat.Agi, 2, 0),
                    StatN("ar_str", "臂力", Stat.Str, 2, 1),
                    Pas("ar_crit", "銳利箭頭", PassiveKind.Crit, 0.03f, 3, 2, 2, "ar_agi", 6),
                    Pas("ar_dodge", "獵人直覺", PassiveKind.Dodge, 0.1f, 2, 2, 3, "ar_crit", 10),
                    Pas("ar_over", "蓄力強化", PassiveKind.Overcharge, 1f, 1, 0, 3),
                };
                case "mage": return new List<TreeNode>
                {
                    Mod("mg_fire", "火球強化", "fire", ModField.Damage, 0.12f, 3, 0, 0),
                    Mod("mg_ice", "冰槍強化", "ice", ModField.Damage, 0.12f, 3, 0, 1, "mg_fire"),
                    Mod("mg_wind", "風刃強化", "wind", ModField.Damage, 0.12f, 3, 0, 2),
                    Learn("mg_med", "meditate", 1, 0, null, 2),
                    Mod("mg_med2", "冥想強化", "meditate", ModField.BuffAmount, 10f, 3, 1, 1, "mg_med", 4),
                    Learn("mg_meteor", "meteor", 1, 2, "mg_fire", 6),
                    Mod("mg_meteor2", "隕石強化", "meteor", ModField.Damage, 0.15f, 3, 1, 3, "mg_meteor", 9),
                    Learn("mg_dispel", "dispel", 1, 4, "mg_wind", 10),
                    StatN("mg_int", "智慧", Stat.Int, 2, 0),
                    StatN("mg_spi", "冥思", Stat.Spi, 2, 1),
                    Pas("mg_burn", "元素灼燒", PassiveKind.Burn, 0.05f, 3, 2, 2, "mg_int", 6),
                    Pas("mg_siphon", "法力虹吸", PassiveKind.MpOnHit, 2f, 3, 2, 3, "mg_spi", 10),
                    Pas("mg_fast", "快速詠唱", PassiveKind.FastCast, 0.08f, 3, 0, 3),
                };
                case "assassin": return new List<TreeNode>
                {
                    Mod("as_knife", "飛刀連發", "knife", ModField.Multi, 1f, 2, 0, 0),
                    Mod("as_shadow", "影襲強化", "shadow", ModField.Damage, 0.12f, 3, 0, 1, "as_knife"),
                    Mod("as_smoke", "濃煙", "smoke", ModField.EffectDur, 1f, 2, 0, 2),
                    Learn("as_poison", "poison", 1, 0, null, 3),
                    Mod("as_poison2", "劇毒", "poison", ModField.EffectDps, 2f, 3, 1, 1, "as_poison", 5),
                    Learn("as_backstab", "backstab", 1, 2, "as_shadow", 6),
                    Mod("as_backstab2", "刺殺強化", "backstab", ModField.Damage, 0.15f, 3, 1, 3, "as_backstab", 9),
                    Learn("as_prep", "prep", 1, 4, "as_backstab", 12),
                    StatN("as_str", "力量", Stat.Str, 2, 0),
                    StatN("as_agi", "身手", Stat.Agi, 2, 1),
                    Pas("as_crit", "致命一擊", PassiveKind.Crit, 0.04f, 3, 2, 2, "as_agi", 6),
                    Pas("as_dodge", "暗影步", PassiveKind.Dodge, 0.1f, 2, 2, 3, "as_crit", 10),
                    Pas("as_evade", "殘影", PassiveKind.Evade, 0.04f, 3, 0, 3, "as_smoke", 4),
                    Pas("as_evade2", "幻影身法", PassiveKind.Evade, 0.05f, 2, 2, 4, "as_dodge", 14),
                };
                case "priest": return new List<TreeNode>
                {
                    Mod("pr_holy", "聖光強化", "holy", ModField.Damage, 0.12f, 3, 0, 0),
                    Mod("pr_heal", "治療強化", "prayheal", ModField.Heal, 0.15f, 3, 0, 1),
                    Mod("pr_cleanse", "淨化強化", "cleanse", ModField.Cooldown, 0.15f, 2, 0, 2),
                    Learn("pr_judge", "judgement", 1, 0, "pr_holy", 3),
                    Mod("pr_judge2", "審判強化", "judgement", ModField.Damage, 0.15f, 3, 1, 1, "pr_judge", 6),
                    Learn("pr_guard", "guard", 1, 2, "pr_heal", 5),
                    Mod("pr_guard2", "守護強化", "guard", ModField.BuffReduce, 0.05f, 3, 1, 3, "pr_guard", 8),
                    Learn("pr_prayer", "prayer", 1, 4, "pr_guard", 10),
                    StatN("pr_int", "虔誠", Stat.Int, 2, 0),
                    StatN("pr_vit", "堅毅", Stat.Vit, 2, 1),
                    Pas("pr_regen", "神聖庇護", PassiveKind.Regen, 0.5f, 3, 2, 2, "pr_vit", 6),
                    Pas("pr_faith", "信仰", PassiveKind.MpOnHit, 2f, 3, 2, 3, "pr_int", 10),
                    Pas("pr_quick", "快速施法", PassiveKind.QuickCd, 0.08f, 3, 0, 3),
                };
                // ======================================================== 進階職業的技能樹（轉職時已學會第一個技能）
                case "paladin": return new List<TreeNode>
                {
                    Mod("pa_bash", "聖盾強化", "shieldbash", ModField.Damage, 0.15f, 3, 0, 0),
                    Mod("pa_bash2", "震懾", "shieldbash", ModField.EffectDur, 0.3f, 2, 0, 1),
                    Mod("pa_bash3", "衝鋒熟練", "shieldbash", ModField.Cooldown, 0.12f, 2, 0, 2),
                    Learn("pa_oath", "oath", 1, 0, null, 1),
                    Mod("pa_oath2", "誓言延長", "oath", ModField.BuffDur, 2f, 2, 1, 1),
                    Learn("pa_sanc", "sanction", 1, 2, null, 1),
                    Mod("pa_sanc2", "制裁強化", "sanction", ModField.Damage, 0.15f, 3, 1, 3),
                    StatN("pa_vit", "聖騎士之軀", Stat.Vit, 2, 0),
                    Pas("pa_armor", "神聖護甲", PassiveKind.Armor, 0.03f, 3, 2, 1),
                    Pas("pa_hp", "堅守", PassiveKind.Hp, 15f, 3, 2, 2),
                };
                case "berserker": return new List<TreeNode>
                {
                    Mod("be_whirl", "旋風強化", "whirlwind", ModField.Damage, 0.15f, 3, 0, 0),
                    Mod("be_whirl2", "狂風", "whirlwind", ModField.Cooldown, 0.12f, 2, 0, 1),
                    Learn("be_rage", "bloodrage", 1, 0, null, 1),
                    Mod("be_rage2", "狂暴延長", "bloodrage", ModField.BuffDur, 2f, 2, 1, 1),
                    Learn("be_undy", "undying", 1, 2, null, 1),
                    Mod("be_undy2", "不死強化", "undying", ModField.Cooldown, 0.15f, 2, 1, 3),
                    StatN("be_str", "蠻力", Stat.Str, 2, 0),
                    Pas("be_atk", "嗜血", PassiveKind.Atk, 0.04f, 3, 2, 1),
                    Pas("be_steal", "血祭", PassiveKind.Lifesteal, 0.03f, 3, 2, 2),
                };
                case "marksman": return new List<TreeNode>
                {
                    Mod("mk_pierce", "貫穿強化", "pierce", ModField.Damage, 0.15f, 3, 0, 0),
                    Mod("mk_pierce2", "快速上弦", "pierce", ModField.Charge, 0.1f, 2, 0, 1),
                    Learn("mk_mark", "deadmark", 1, 0, null, 1),
                    Mod("mk_mark2", "標記熟練", "deadmark", ModField.Cooldown, 0.15f, 2, 1, 1),
                    Learn("mk_rain", "arrowrain", 1, 2, null, 1),
                    Mod("mk_rain2", "密集箭雨", "arrowrain", ModField.Multi, 1f, 3, 1, 3),
                    StatN("mk_luk", "神射", Stat.Luk, 2, 0),
                    Pas("mk_crit", "要害瞄準", PassiveKind.Crit, 0.04f, 3, 2, 1),
                    Pas("mk_atk", "強弓", PassiveKind.Atk, 0.04f, 3, 2, 2),
                };
                case "ranger": return new List<TreeNode>
                {
                    Mod("rg_falcon", "獵鷹強化", "falcon", ModField.Damage, 0.2f, 3, 0, 0),
                    Mod("rg_falcon2", "獵鷹長駐", "falcon", ModField.BuffDur, 4f, 2, 0, 1),
                    Learn("rg_roll", "rollshot", 1, 0, null, 1),
                    Mod("rg_roll2", "翻滾熟練", "rollshot", ModField.Cooldown, 0.15f, 2, 1, 1),
                    Learn("rg_chain", "trapchain", 1, 2, null, 1),
                    Mod("rg_chain2", "陷阱專家", "trapchain", ModField.Damage, 0.2f, 3, 1, 3),
                    StatN("rg_agi", "疾行", Stat.Agi, 2, 0),
                    Pas("rg_dodge", "身輕如燕", PassiveKind.Dodge, 0.1f, 2, 2, 1),
                    Pas("rg_evade", "游擊步法", PassiveKind.Evade, 0.04f, 3, 2, 2),
                };
                case "archmage": return new List<TreeNode>
                {
                    Mod("am_tj", "雷霆強化", "tjudge", ModField.Damage, 0.15f, 3, 0, 0),
                    Mod("am_tj2", "雷霆連擊", "tjudge", ModField.Multi, 1f, 2, 0, 1),
                    Learn("am_frost", "frostfield", 1, 0, null, 1),
                    Mod("am_frost2", "永凍", "frostfield", ModField.EffectDur, 0.5f, 2, 1, 1),
                    Learn("am_shield", "manashield", 1, 2, null, 1),
                    Mod("am_shield2", "護盾延長", "manashield", ModField.BuffDur, 3f, 2, 1, 3),
                    StatN("am_int", "大智慧", Stat.Int, 2, 0),
                    Pas("am_atk", "魔力增幅", PassiveKind.Atk, 0.04f, 3, 2, 1),
                    Pas("am_fast", "高速詠唱", PassiveKind.FastCast, 0.05f, 3, 2, 2),
                };
                case "summoner": return new List<TreeNode>
                {
                    Mod("su_fire", "火靈強化", "firespirit", ModField.Damage, 0.2f, 3, 0, 0),
                    Mod("su_fire2", "火靈長駐", "firespirit", ModField.BuffDur, 5f, 2, 0, 1),
                    Learn("su_golem", "golem", 1, 0, null, 1),
                    Mod("su_golem2", "堅固石像", "golem", ModField.BuffAmount, 30f, 3, 1, 1),
                    Learn("su_det", "detonate", 1, 2, null, 1),
                    Mod("su_det2", "爆裂熟練", "detonate", ModField.Cooldown, 0.15f, 2, 1, 3),
                    StatN("su_spi", "契約之力", Stat.Spi, 2, 0),
                    Pas("su_hp", "共生", PassiveKind.Hp, 12f, 3, 2, 1),
                    Pas("su_mp", "魔力循環", PassiveKind.MpOnHit, 2f, 3, 2, 2),
                };
                case "ninja": return new List<TreeNode>
                {
                    Mod("nj_fire", "火遁強化", "fireseal", ModField.Damage, 0.15f, 3, 0, 0),
                    Mod("nj_fire2", "火遁擴散", "fireseal", ModField.Multi, 1f, 2, 0, 1),
                    Learn("nj_clone", "clone", 1, 0, null, 1),
                    Mod("nj_clone2", "分身延長", "clone", ModField.BuffDur, 2f, 2, 1, 1),
                    Learn("nj_sub", "substitute", 1, 2, null, 1),
                    Mod("nj_sub2", "替身熟練", "substitute", ModField.Cooldown, 0.15f, 2, 1, 3),
                    StatN("nj_agi", "忍者身手", Stat.Agi, 2, 0),
                    Pas("nj_evade", "隱遁", PassiveKind.Evade, 0.03f, 3, 2, 1),
                    Pas("nj_dodge", "瞬身", PassiveKind.Dodge, 0.1f, 2, 2, 2),
                };
                case "shadowdancer": return new List<TreeNode>
                {
                    Mod("sd_exe", "處決強化", "execute", ModField.Damage, 0.15f, 3, 0, 0),
                    Mod("sd_exe2", "快速處決", "execute", ModField.Charge, 0.1f, 2, 0, 1),
                    Learn("sd_dance", "shadowdance", 1, 0, null, 1),
                    Mod("sd_dance2", "影舞延長", "shadowdance", ModField.BuffDur, 2f, 2, 1, 1),
                    Learn("sd_fog", "poisonfog", 1, 2, null, 1),
                    Mod("sd_fog2", "劇毒之霧", "poisonfog", ModField.EffectDps, 2f, 3, 1, 3),
                    StatN("sd_str", "暗殺之力", Stat.Str, 2, 0),
                    Pas("sd_crit", "致命舞步", PassiveKind.Crit, 0.04f, 3, 2, 1),
                    Pas("sd_atk", "影之刃", PassiveKind.Atk, 0.04f, 3, 2, 2),
                };
                case "bishop": return new List<TreeNode>
                {
                    Mod("bi_heal", "群體治療強化", "massheal", ModField.BuffAmount, 5f, 3, 0, 0),
                    Mod("bi_heal2", "治療熟練", "massheal", ModField.Cooldown, 0.12f, 2, 0, 1),
                    Learn("bi_sanc", "sanctuary", 1, 0, null, 1),
                    Mod("bi_sanc2", "庇護強化", "sanctuary", ModField.BuffAmount, 15f, 3, 1, 1),
                    Learn("bi_rev", "revive", 1, 2, null, 1),
                    Mod("bi_rev2", "復活熟練", "revive", ModField.Cooldown, 0.15f, 2, 1, 3),
                    StatN("bi_spi", "虔誠之心", Stat.Spi, 2, 0),
                    Pas("bi_regen", "聖靈", PassiveKind.Regen, 0.5f, 3, 2, 1),
                    Pas("bi_quick", "神速禱告", PassiveKind.QuickCd, 0.05f, 3, 2, 2),
                };
                case "inquisitor": return new List<TreeNode>
                {
                    Mod("iq_lance", "聖槍強化", "holylance", ModField.Damage, 0.15f, 3, 0, 0),
                    Mod("iq_lance2", "聖槍連射", "holylance", ModField.Multi, 1f, 2, 0, 1),
                    Learn("iq_chast", "chastise", 1, 0, null, 1),
                    Mod("iq_chast2", "懲戒延長", "chastise", ModField.EffectDur, 2f, 2, 1, 1),
                    Learn("iq_heaven", "heavenpunish", 1, 2, null, 1),
                    Mod("iq_heaven2", "天罰強化", "heavenpunish", ModField.Damage, 0.15f, 3, 1, 3),
                    StatN("iq_int", "審判之力", Stat.Int, 2, 0),
                    Pas("iq_atk", "聖戰", PassiveKind.Atk, 0.04f, 3, 2, 1),
                    Pas("iq_crit", "裁決", PassiveKind.Crit, 0.03f, 3, 2, 2),
                };
            }
            return new List<TreeNode>();
        }

        /// <summary>節點每一級的效果文字</summary>
        public static string Describe(TreeNode n, int rank)
        {
            string skill = n.skill != null && Skills.All.TryGetValue(n.skill, out var s) ? s.name : n.skill;
            float v = n.per * Mathf.Max(1, rank);
            switch (n.kind)
            {
                case NodeKind.Learn: return $"學會新技能「{skill}」";
                case NodeKind.Stat: return $"{StatInfo.Names[(int)n.stat]} +{v:0}";
                case NodeKind.Mod:
                    return n.field switch
                    {
                        ModField.Damage => $"{skill} 傷害 +{v * 100:0}%",
                        ModField.Heal => $"{skill} 回復量 +{v * 100:0}%",
                        ModField.Cooldown => $"{skill} 冷卻 -{v * 100:0}%",
                        ModField.Charge => $"{skill} 蓄力 -{v * 100:0}%",
                        ModField.Cost => $"{skill} MP -{v * 100:0}%",
                        ModField.Multi => $"{skill} 多 {v:0} 發",
                        ModField.EffectDur => $"{skill} 效果時間 +{v:0.#} 秒",
                        ModField.EffectDps => $"{skill} 中毒每秒 +{v:0}",
                        ModField.BuffAmount => $"{skill} 效果量 +{v:0}",
                        ModField.BuffReduce => $"{skill} 減傷 +{v * 100:0}%",
                        _ => $"{skill} 持續 +{v:0.#} 秒",
                    };
                default:
                    return n.passive switch
                    {
                        PassiveKind.Lifesteal => $"造成傷害的 {v * 100:0}% 回復成 HP",
                        PassiveKind.Crit => $"爆擊率 +{v * 100:0}%",
                        PassiveKind.Dodge => $"側跨無敵時間 +{v:0.0#} 秒",
                        PassiveKind.MpOnHit => $"每次命中回復 {v:0} MP",
                        PassiveKind.Regen => $"每秒回復 {v:0.#} HP",
                        PassiveKind.Burn => $"命中時 {v * 100:0}% 機率灼燒",
                        PassiveKind.Poison => $"命中時 {v * 100:0}% 機率中毒",
                        PassiveKind.Freeze => $"命中時 {v * 100:0}% 機率冰凍",
                        PassiveKind.FastCast => $"所有技能蓄力時間 -{v * 100:0}%",
                        PassiveKind.Overcharge => "蓄滿後繼續拉弓可以蓄到 200%：蓄力越多傷害越高，200% 時傷害 +150%",
                        PassiveKind.NaturalRegen => $"每 10 秒回復最大 HP 的 {v * 100:0}%",
                        PassiveKind.QuickCd => $"所有技能冷卻時間 -{v * 100:0}%",
                        PassiveKind.Evade => $"閃避率 +{v * 100:0}%（被攻擊時有機率完全閃開）",
                        PassiveKind.Atk => $"攻擊傷害 +{v * 100:0}%",
                        PassiveKind.Armor => $"受到的傷害 -{v * 100:0}%",
                        PassiveKind.Hp => $"HP 上限 +{v:0}",
                        _ => $"HP 低於 30% 時受到的傷害 -{v * 100:0}%",
                    };
            }
        }
    }

    // ---------------------------------------------------------------- 裝備
    public enum Slot { Weapon, Head, Body, Feet, Trinket }
    public enum Affix { Str, Int, Vit, Agi, Spi, Luk, Hp, Mp, Crit, Cdr, Lifesteal, Burn, Poison, Freeze, Atk, Armor }

    public class Item
    {
        public string name; public Slot slot; public int rarity, ilvl;
        public string classes;          // 可以裝備的職業（逗號分隔；空＝全部）
        public int reqLevel; public Stat reqStat; public int reqValue;
        public readonly List<KeyValuePair<Affix, float>> mods = new List<KeyValuePair<Affix, float>>();
        public long uid;
        public int plus;                // 鐵匠鋪強化等級（+1～+10；每級所有屬性 +8%）
        public string setId = "";       // 套裝（Economy.Sets）；空＝不是套裝
        /// <summary>顯示用名稱：+強化等級</summary>
        public string FullName => (plus > 0 ? $"+{plus} " : "") + name;

        public static readonly string[] SlotNames = { "武器", "頭部", "身體", "鞋子", "飾品" };
        public static readonly string[] RarityNames = { "普通", "優良", "稀有", "史詩", "傳說" };
        public static readonly Color[] RarityColors =
        {
            new Color(0.85f, 0.85f, 0.85f), new Color(0.4f, 0.95f, 0.45f), new Color(0.4f, 0.65f, 1f),
            new Color(0.78f, 0.45f, 1f), new Color(1f, 0.65f, 0.15f),
        };

        public bool ClassOk(string cls) => string.IsNullOrEmpty(classes) || Array.IndexOf(classes.Split(','), cls) >= 0;

        public string ClassText()
        {
            if (string.IsNullOrEmpty(classes)) return "所有職業";
            var parts = classes.Split(',');
            var names = new List<string>();
            foreach (var c in parts) if (Skills.Classes.TryGetValue(c, out var cd)) names.Add(cd.name);
            return string.Join("、", names);
        }

        public static string AffixText(Affix a, float v) => a switch
        {
            Affix.Str => $"力量 +{v:0}", Affix.Int => $"智力 +{v:0}", Affix.Vit => $"體質 +{v:0}",
            Affix.Agi => $"敏捷 +{v:0}", Affix.Spi => $"精神 +{v:0}", Affix.Luk => $"幸運 +{v:0}",
            Affix.Hp => $"HP +{v:0}", Affix.Mp => $"MP +{v:0}",
            Affix.Crit => $"爆擊率 +{v * 100:0.#}%", Affix.Cdr => $"冷卻 -{v * 100:0.#}%",
            Affix.Lifesteal => $"吸血 {v * 100:0.#}%",
            Affix.Burn => $"灼燒機率 +{v * 100:0}%", Affix.Poison => $"中毒機率 +{v * 100:0}%", Affix.Freeze => $"冰凍機率 +{v * 100:0}%",
            Affix.Atk => $"攻擊傷害 +{v * 100:0}%", _ => $"受到的傷害 -{v * 100:0.#}%",
        };

        public float Sum(Affix a) { float s = 0f; foreach (var kv in mods) if (kv.Key == a) s += kv.Value; return s * PlusMul; }
        public float PlusMul => 1f + Economy.EnhancePerLevel * plus;

        // ---- 存檔：一行文字
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(uid).Append('|').Append(Esc(name)).Append('|').Append((int)slot).Append('|').Append(rarity).Append('|').Append(ilvl).Append('|')
              .Append(classes ?? "").Append('|').Append(reqLevel).Append('|').Append((int)reqStat).Append('|').Append(reqValue).Append('|');
            for (int i = 0; i < mods.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append((int)mods[i].Key).Append(':').Append(mods[i].Value.ToString("0.####", CultureInfo.InvariantCulture));
            }
            sb.Append('|').Append(plus).Append('|').Append(setId ?? "");
            return sb.ToString();
        }

        public static Item Parse(string line)
        {
            try
            {
                var p = line.Split('|');
                if (p.Length < 10) return null;
                var it = new Item
                {
                    uid = long.Parse(p[0], CultureInfo.InvariantCulture), name = Unesc(p[1]), slot = (Slot)int.Parse(p[2]), rarity = int.Parse(p[3]),
                    ilvl = int.Parse(p[4]), classes = p[5], reqLevel = int.Parse(p[6]), reqStat = (Stat)int.Parse(p[7]), reqValue = int.Parse(p[8]),
                };
                if (p[9].Length > 0)
                    foreach (var m in p[9].Split(';'))
                    {
                        var kv = m.Split(':');
                        if (kv.Length == 2) it.mods.Add(new KeyValuePair<Affix, float>((Affix)int.Parse(kv[0]), float.Parse(kv[1], CultureInfo.InvariantCulture)));
                    }
                if (p.Length >= 12) { int.TryParse(p[10], out it.plus); it.setId = p[11]; }   // 新版：強化等級、套裝
                return it;
            }
            catch (Exception) { return null; }
        }

        static string Esc(string s) => (s ?? "").Replace("|", "／").Replace("\n", " ");
        static string Unesc(string s) => s;
    }

    public static class ItemGen
    {
        // 各部位、各類型的名稱與限定職業
        static readonly (string name, string cls, Affix main)[] Weapons =
        {
            ("長劍", "swordsman", Affix.Atk), ("大劍", "swordsman", Affix.Atk),
            ("長弓", "archer", Affix.Atk), ("十字弩", "archer", Affix.Atk),
            ("法杖", "mage", Affix.Atk), ("魔導書", "mage", Affix.Atk),
            ("雙匕首", "assassin", Affix.Atk), ("短刃", "assassin", Affix.Atk),
            ("權杖", "priest", Affix.Atk), ("聖典", "priest", Affix.Atk),
        };
        static readonly (string name, string cls)[] Heads = { ("頭盔", "swordsman"), ("皮帽", "archer,assassin"), ("兜帽", "mage,priest"), ("頭環", "") };
        static readonly (string name, string cls)[] Bodies = { ("板甲", "swordsman"), ("皮甲", "archer,assassin"), ("法袍", "mage,priest"), ("鎖子甲", "swordsman,archer") };
        static readonly (string name, string cls)[] Feets = { ("鐵靴", "swordsman"), ("皮靴", "archer,assassin"), ("布鞋", "mage,priest"), ("旅行靴", "") };
        static readonly string[] Trinkets = { "戒指", "項鍊", "護符", "耳環" };
        static readonly string[][] Prefix =
        {
            new[] { "破舊的", "簡單的", "粗糙的" },
            new[] { "精鋼", "堅固的", "優質" },
            new[] { "符文", "秘銀", "星光" },
            new[] { "熔岩之心", "古樹之魂", "深海之怒", "龍鱗" },
            new[] { "傳說・烈焰王", "傳說・世界樹", "傳說・海神", "傳說・天命" },
        };
        static readonly Affix[] Pool = { Affix.Str, Affix.Int, Affix.Vit, Affix.Agi, Affix.Spi, Affix.Luk, Affix.Hp, Affix.Mp, Affix.Crit, Affix.Cdr, Affix.Burn, Affix.Poison, Affix.Freeze, Affix.Lifesteal };

        /// <summary>擲稀有度：基本 普通 55%、優良 28%、稀有 12%、史詩 4%、傳說 1%；幸運與加成會往上推</summary>
        public static int RollRarity(System.Random rng, float luckBonus, int minRarity = 0)
        {
            double r = rng.NextDouble() * 100.0;
            float b = Mathf.Clamp(luckBonus, 0f, 30f);
            float[] th = { 55f - b, 83f - b * 0.8f, 95f - b * 0.4f, 99f - b * 0.15f };
            int rar = r < th[0] ? 0 : r < th[1] ? 1 : r < th[2] ? 2 : r < th[3] ? 3 : 4;
            return Mathf.Max(rar, minRarity);
        }

        /// <summary>目前地圖的主題（0 熔岩、1 森林、2 海上；−1＝不限），決定掉哪一套套裝</summary>
        public static int SetTheme = -1;

        /// <summary>產生一件裝備（ilvl＝物品等級；prefer＝優先掉落這個職業能用的）</summary>
        public static Item Make(System.Random rng, int ilvl, int rarity, string prefer)
        {
            ilvl = Mathf.Max(1, ilvl);
            var slot = (Slot)rng.Next(5);
            var it = new Item { slot = slot, rarity = rarity, ilvl = ilvl, uid = DateTime.UtcNow.Ticks + rng.Next(100000) };
            float power = ilvl * (1f + 0.35f * rarity);          // 稀有度越高越強
            string baseName;
            switch (slot)
            {
                case Slot.Weapon:
                {
                    var opts = new List<(string name, string cls, Affix main)>(Weapons);
                    if (prefer != null && rng.NextDouble() < 0.65) opts = opts.FindAll(w => w.cls == prefer);
                    if (opts.Count == 0) opts = new List<(string, string, Affix)>(Weapons);
                    var w = opts[rng.Next(opts.Count)];
                    baseName = w.name; it.classes = w.cls;
                    it.mods.Add(new KeyValuePair<Affix, float>(Affix.Atk, 0.04f + 0.012f * power));
                    break;
                }
                case Slot.Head: { var h = Pick(rng, Heads, prefer); baseName = h.name; it.classes = h.cls; it.mods.Add(new KeyValuePair<Affix, float>(Affix.Mp, Mathf.Round(4f + 1.6f * power))); break; }
                case Slot.Body: { var b = Pick(rng, Bodies, prefer); baseName = b.name; it.classes = b.cls; it.mods.Add(new KeyValuePair<Affix, float>(Affix.Hp, Mathf.Round(8f + 3f * power))); it.mods.Add(new KeyValuePair<Affix, float>(Affix.Armor, Mathf.Min(0.2f, 0.01f + 0.004f * power))); break; }
                case Slot.Feet: { var f = Pick(rng, Feets, prefer); baseName = f.name; it.classes = f.cls; it.mods.Add(new KeyValuePair<Affix, float>(Affix.Agi, Mathf.Round(1f + 0.35f * power))); break; }
                default: baseName = Trinkets[rng.Next(Trinkets.Length)]; it.classes = ""; it.mods.Add(new KeyValuePair<Affix, float>(Affix.Luk, Mathf.Round(1f + 0.3f * power))); break;
            }
            // 稀有度越高：附加屬性越多、限制越嚴（傳說只能單一職業）
            int affixes = rarity;
            for (int i = 0; i < affixes; i++)
            {
                var a = Pool[rng.Next(Pool.Length)];
                if (a == Affix.Lifesteal && rarity < 3) a = Affix.Hp;
                it.mods.Add(new KeyValuePair<Affix, float>(a, AffixValue(a, power, rng)));
            }
            if (rarity >= 4 && it.classes != null && it.classes.Contains(","))
            {
                var cs = it.classes.Split(',');
                it.classes = prefer != null && Array.IndexOf(cs, prefer) >= 0 ? prefer : cs[rng.Next(cs.Length)];
            }
            if (rarity >= 3 && string.IsNullOrEmpty(it.classes) && prefer != null) it.classes = prefer;
            // 需求：等級（稀有越高要求越高）＋主要能力值
            it.reqLevel = Mathf.Max(1, ilvl - 2 + rarity * 2);
            string mainCls = !string.IsNullOrEmpty(it.classes) ? it.classes.Split(',')[0] : prefer ?? "swordsman";
            it.reqStat = slot == Slot.Weapon || slot == Slot.Trinket ? StatInfo.AttackStat(mainCls) : slot == Slot.Feet ? Stat.Agi : Stat.Vit;
            int baseV = StatInfo.Base(mainCls)[(int)it.reqStat];
            it.reqValue = rarity == 0 ? 0 : baseV + Mathf.RoundToInt(ilvl * (0.4f + 0.25f * rarity));
            var pre = Prefix[rarity];
            it.name = pre[rng.Next(pre.Length)] + baseName;
            // 稀有以上有機會是套裝（主題地圖掉對應的套裝）
            if (rarity >= 2 && rng.NextDouble() < Economy.SetChance)
            {
                var set = Economy.RollSet(rng, SetTheme);
                it.setId = set.id; it.name = set.name + "・" + baseName;
            }
            return it;
        }

        static (string name, string cls) Pick(System.Random rng, (string name, string cls)[] arr, string prefer)
        {
            var list = new List<(string name, string cls)>(arr);
            if (prefer != null && rng.NextDouble() < 0.65)
            {
                var f = list.FindAll(x => string.IsNullOrEmpty(x.cls) || Array.IndexOf(x.cls.Split(','), prefer) >= 0);
                if (f.Count > 0) list = f;
            }
            return list[rng.Next(list.Count)];
        }

        static float AffixValue(Affix a, float power, System.Random rng)
        {
            float k = 0.8f + 0.4f * (float)rng.NextDouble();
            return a switch
            {
                Affix.Hp => Mathf.Round((5f + 2f * power) * k),
                Affix.Mp => Mathf.Round((3f + 1.2f * power) * k),
                Affix.Crit => Mathf.Min(0.15f, (0.01f + 0.003f * power) * k),
                Affix.Cdr => Mathf.Min(0.12f, (0.01f + 0.0025f * power) * k),
                Affix.Lifesteal => Mathf.Min(0.08f, (0.01f + 0.0015f * power) * k),
                Affix.Burn or Affix.Poison or Affix.Freeze => Mathf.Min(0.15f, (0.02f + 0.003f * power) * k),
                _ => Mathf.Max(1f, Mathf.Round((1f + 0.3f * power) * k)),
            };
        }
    }

    // ---------------------------------------------------------------- 角色資料（每個職業獨立）
    public class CharacterProfile
    {
        public string cls;
        public int level = 1, xp, statPoints, skillPoints;
        public readonly int[] alloc = new int[StatInfo.Count];        // 自己加的能力點（不含初始值、技能樹、裝備）
        public readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
        public readonly Item[] equipped = new Item[5];
        public string adv;              // 進階職業（Advanced.All）；null＝還沒轉職
        public bool trialPassed;        // 轉職試煉通過了（可以選進階職業）
        public AdvDef Adv => Advanced.Get(adv);
        /// <summary>這個角色所有的技能樹節點（基本＋進階）</summary>
        public IEnumerable<TreeNode> Nodes => SkillTrees.AllOf(cls, adv);
        /// <summary>節點屬於哪一棵樹（基本職業 id 或進階職業 id）</summary>
        public string TreeOf(TreeNode n) => adv != null && SkillTrees.Of(adv).Contains(n) ? adv : cls;
        public bool CanAdvance => adv == null && level >= Advanced.Level && trialPassed;

        /// <summary>轉職（選了不能換）：第一個進階技能直接學會</summary>
        public bool Advance(string advId)
        {
            var a = Advanced.Get(advId);
            if (!CanAdvance || a == null || a.baseCls != cls) return false;
            adv = a.id;
            return true;
        }

        public const int MaxLevel = 60, StatPerLevel = 8, SkillPerLevel = 1;

        /// <summary>升到 level+1 需要的累積經驗：75、200、375、600…</summary>
        public static int XpForLevel(int level) => 50 * (level - 1) + 25 * (level - 1) * (level - 1);

        public int Rank(string id) => ranks.TryGetValue(id, out var r) ? r : 0;

        /// <summary>加經驗，回傳升了幾級</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0 || level >= MaxLevel) return 0;
            xp += amount;
            int ups = 0;
            while (level < MaxLevel && xp >= XpForLevel(level + 1)) { level++; statPoints += StatPerLevel; skillPoints += SkillPerLevel; ups++; }
            return ups;
        }

        // ---- 能力值
        public int BaseStat(Stat s) => StatInfo.Base(cls)[(int)s];
        public int TreeStat(Stat s)
        {
            int v = 0;
            foreach (var n in Nodes) if (n.kind == NodeKind.Stat && n.stat == s) v += Mathf.RoundToInt(n.per * Rank(n.id));
            var a = Adv; if (a != null) v += a.stats[(int)s];   // 轉職的能力值加成
            return v;
        }
        public int GearStat(Stat s)
        {
            float v = 0f; var a = (Affix)(int)s;
            foreach (var it in equipped) if (it != null) v += it.Sum(a);
            return Mathf.RoundToInt(v);
        }
        /// <summary>自己的能力值（初始＋自己加的）：決定加點的花費</summary>
        public int OwnStat(Stat s) => BaseStat(s) + alloc[(int)s];
        public int Total(Stat s) => OwnStat(s) + TreeStat(s) + GearStat(s);

        public bool CanRaise(Stat s) => statPoints >= StatInfo.Cost(OwnStat(s));
        public bool Raise(Stat s)
        {
            int c = StatInfo.Cost(OwnStat(s));
            if (statPoints < c) return false;
            statPoints -= c; alloc[(int)s]++;
            return true;
        }

        /// <summary>重新分配：全部能力點、技能點退回（技能樹清空）</summary>
        public void ResetPoints()
        {
            for (int i = 0; i < StatInfo.Count; i++) alloc[i] = 0;
            ranks.Clear();
            statPoints = (level - 1) * StatPerLevel;
            skillPoints = (level - 1) * SkillPerLevel;
            for (int i = 0; i < equipped.Length; i++) if (equipped[i] != null && !CanEquip(equipped[i], out _)) equipped[i] = null;
        }

        // ---- 技能樹
        public string NodeBlock(TreeNode n)
        {
            if (Rank(n.id) >= n.max) return "已滿級";
            foreach (var r in SkillTrees.Prereqs(TreeOf(n), n))
                if (Rank(r.id) < r.max) return $"需要先點滿「{r.name}」";
            if (skillPoints <= 0) return "技能點不足";
            return null;
        }
        public bool Learn(TreeNode n)
        {
            if (NodeBlock(n) != null) return false;
            skillPoints--; ranks[n.id] = Rank(n.id) + 1;
            return true;
        }

        /// <summary>會的技能：初始 3 個＋技能樹習得的</summary>
        public List<string> KnownSkills()
        {
            var list = new List<string>(SkillTrees.Starters(cls));
            var a = Adv; if (a != null) list.Add(a.skills[0]);   // 轉職時學會第一個進階技能
            foreach (var n in Nodes) if (n.kind == NodeKind.Learn && Rank(n.id) > 0 && !list.Contains(n.skill)) list.Add(n.skill);
            return list;
        }

        public float Passive(PassiveKind p)
        {
            float v = 0f;
            foreach (var n in Nodes) if (n.kind == NodeKind.Passive && n.passive == p) v += n.per * Rank(n.id);
            return v;
        }
        public float GearSum(Affix a) { float v = 0f; foreach (var it in equipped) if (it != null) v += it.Sum(a); return v + Economy.SetBonus(equipped, a); }

        /// <summary>技能樹強化後的技能（複製一份改數值；id 不變）</summary>
        public SkillDef ModdedSkill(string id)
        {
            if (!Skills.All.TryGetValue(id, out var b)) return null;
            var s = b.Clone();
            float heal = 1f + 0.02f * (Total(Stat.Int) - BaseStat(Stat.Int));
            foreach (var n in Nodes)
            {
                if (n.kind != NodeKind.Mod || n.skill != id) continue;
                int r = Rank(n.id); if (r <= 0) continue;
                float v = n.per * r;
                switch (n.field)
                {
                    case ModField.Damage: s.damage = Mathf.RoundToInt(s.damage * (1f + v)); break;
                    case ModField.Heal: s.heal = Mathf.RoundToInt(s.heal * (1f + v)); break;
                    case ModField.Cooldown: s.cooldown *= Mathf.Max(0.3f, 1f - v); break;
                    case ModField.Charge: s.charge *= Mathf.Max(0.3f, 1f - v); break;
                    case ModField.Cost: s.cost = Mathf.RoundToInt(s.cost * Mathf.Max(0.3f, 1f - v)); break;
                    case ModField.Multi: s.multi = Mathf.Max(1, s.multi) + Mathf.RoundToInt(v); if (s.spreadDeg <= 0f) s.spreadDeg = 8f; break;
                    case ModField.EffectDur: s.effectDur += v; break;
                    case ModField.EffectDps: s.effectDps += v; break;
                    case ModField.BuffAmount: if (s.self == SelfKind.Pet) s.petHp += Mathf.RoundToInt(v); else s.buffAmount += Mathf.RoundToInt(v); break;
                    case ModField.BuffReduce: s.buffReduce = Mathf.Min(0.9f, s.buffReduce + v); break;
                    case ModField.BuffDur: if (s.self == SelfKind.Pet) s.petLife += v; else s.buffDur += v; break;
                }
            }
            if (s.heal > 0) s.heal = Mathf.RoundToInt(s.heal * heal);   // 智力：治療量
            return s;
        }

        // ---- 裝備
        public bool CanEquip(Item it, out string why)
        {
            why = null;
            if (it == null) { why = "沒有裝備"; return false; }
            if (!it.ClassOk(cls)) { why = $"限定職業：{it.ClassText()}"; return false; }
            if (level < it.reqLevel) { why = $"需要等級 {it.reqLevel}"; return false; }
            if (it.reqValue > 0 && Total(it.reqStat) - GearStat(it.reqStat) < it.reqValue) { why = $"需要{StatInfo.Names[(int)it.reqStat]} {it.reqValue}"; return false; }
            return true;
        }

        /// <summary>把角色的能力值、技能樹、裝備套用到戰鬥中的角色身上（保留目前 HP 比例）</summary>
        public void ApplyTo(Fighter f, ClassDef cd, IList<string> loadoutIds)
        {
            float hpRatio = f.maxHp > 0 ? f.hp / f.maxHp : 1f, mpRatio = f.maxMp > 0 ? f.mp / f.maxMp : 1f;
            int d(Stat s) => Total(s) - BaseStat(s);
            var atkStat = StatInfo.AttackStat(cls);
            // 攻擊：攻擊能力值每點 +2%，裝備的攻擊加成
            f.atkMul = Mathf.Max(0.3f, 1f + 0.02f * d(atkStat)) * (1f + GearSum(Affix.Atk));
            // HP：體質每點 +5、非攻擊用的力量每點 +1、裝備
            f.maxHp = Mathf.Max(10, cd.maxHp + 5 * d(Stat.Vit) + (atkStat != Stat.Str ? d(Stat.Str) : 0) + Mathf.RoundToInt(GearSum(Affix.Hp)));
            // 受到的傷害：體質每點 -0.4%（最多 -30%）、裝備護甲
            f.gearReduce = Mathf.Min(0.5f, Mathf.Clamp(0.004f * d(Stat.Vit), 0f, 0.3f) + GearSum(Affix.Armor));
            // MP：智力每點 +2、裝備；MP 回復：精神每點 +3%
            f.maxMp = Mathf.Max(10, cd.maxMp + 2 * d(Stat.Int) + Mathf.RoundToInt(GearSum(Affix.Mp)));
            f.regen = cd.mpRegen * Mathf.Max(0.3f, 1f + 0.03f * d(Stat.Spi));
            // 冷卻：精神每點 -0.8%（最多 -35%）、裝備
            f.cdMul = Mathf.Clamp(1f - 0.008f * d(Stat.Spi) - GearSum(Affix.Cdr), 0.5f, 1.5f);
            // 蓄力：敏捷每點 -1%（最多 -40%）；側跨無敵時間：敏捷每點 +1%＋特殊能力
            f.chargeScale = Mathf.Clamp(1f - 0.01f * d(Stat.Agi), 0.6f, 1.5f);
            f.dodgeBonus = 0.35f * Mathf.Max(0f, 0.01f * d(Stat.Agi)) + Passive(PassiveKind.Dodge);
            // 幸運：爆擊率；技能樹與裝備的特殊能力
            f.critChance = Mathf.Clamp(0.006f * Total(Stat.Luk) + Passive(PassiveKind.Crit) + GearSum(Affix.Crit), 0f, 0.75f);
            f.lifesteal = Passive(PassiveKind.Lifesteal) + GearSum(Affix.Lifesteal);
            f.mpOnHit = Passive(PassiveKind.MpOnHit);
            f.hpRegen = Passive(PassiveKind.Regen);
            f.lastStand = Passive(PassiveKind.LastStand);
            f.burnChance = Passive(PassiveKind.Burn) + GearSum(Affix.Burn);
            f.poisonChance = Passive(PassiveKind.Poison) + GearSum(Affix.Poison);
            f.freezeChance = Passive(PassiveKind.Freeze) + GearSum(Affix.Freeze);
            f.evadeChance = Mathf.Clamp(Passive(PassiveKind.Evade), 0f, 0.5f);
            f.chargeScale = Mathf.Max(0.4f, f.chargeScale * (1f - Passive(PassiveKind.FastCast)));   // 法師：快速詠唱
            f.overcharge = Passive(PassiveKind.Overcharge) > 0f;                                   // 獵人：蓄力強化
            f.hpRegen += f.maxHp * Passive(PassiveKind.NaturalRegen) / 10f;                         // 劍士：自然回復（每 10 秒 x%）
            f.cdMul = Mathf.Max(0.4f, f.cdMul * (1f - Passive(PassiveKind.QuickCd)));               // 牧師：快速施法
            f.rangeMul = 1f;
            // 進階技能樹的特殊能力：攻擊、減傷、HP
            f.atkMul *= 1f + Passive(PassiveKind.Atk);
            f.gearReduce = Mathf.Min(0.6f, f.gearReduce + Passive(PassiveKind.Armor));
            f.maxHp += Mathf.RoundToInt(Passive(PassiveKind.Hp));
            // 技能：只帶會的技能，數值換成技能樹強化後的版本
            f.loadout.Clear();
            var known = KnownSkills();
            foreach (var id in loadoutIds) if (known.Contains(id)) { var s = ModdedSkill(id); if (s != null) f.loadout.Add(s); }
            if (f.loadout.Count == 0) foreach (var id in known) { if (f.loadout.Count >= 3) break; f.loadout.Add(ModdedSkill(id)); }
            f.hp = Mathf.Max(1f, f.maxHp * hpRatio); f.mp = f.maxMp * mpRatio;
            Advanced.ApplyStats(f, Adv);   // 進階職業：HP、MP、攻擊、減傷、爆擊、閃避、治療量
        }

        // ---- 存檔（一行一個欄位）
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("level=").Append(level).Append('\n');
            sb.Append("xp=").Append(xp).Append('\n');
            sb.Append("sp=").Append(statPoints).Append('\n');
            sb.Append("kp=").Append(skillPoints).Append('\n');
            sb.Append("alloc=").Append(string.Join(",", alloc)).Append('\n');
            if (!string.IsNullOrEmpty(adv)) sb.Append("adv=").Append(adv).Append('\n');
            if (trialPassed) sb.Append("trial=1\n");
            foreach (var kv in ranks) sb.Append("rank=").Append(kv.Key).Append(':').Append(kv.Value).Append('\n');
            for (int i = 0; i < equipped.Length; i++) if (equipped[i] != null) sb.Append("eq").Append(i).Append('=').Append(equipped[i].Serialize()).Append('\n');
            return sb.ToString();
        }

        public static CharacterProfile Parse(string cls, string text)
        {
            var p = new CharacterProfile { cls = cls };
            if (string.IsNullOrEmpty(text)) return p;
            foreach (var raw in text.Split('\n'))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string k = raw.Substring(0, eq), v = raw.Substring(eq + 1);
                try
                {
                    switch (k)
                    {
                        case "level": p.level = Mathf.Clamp(int.Parse(v), 1, MaxLevel); break;
                        case "xp": p.xp = int.Parse(v); break;
                        case "sp": p.statPoints = int.Parse(v); break;
                        case "kp": p.skillPoints = int.Parse(v); break;
                        case "alloc": { var a = v.Split(','); for (int i = 0; i < a.Length && i < p.alloc.Length; i++) p.alloc[i] = int.Parse(a[i]); break; }
                        case "rank": { var a = v.Split(':'); if (a.Length == 2) p.ranks[a[0]] = int.Parse(a[1]); break; }
                        case "adv": if (Advanced.Get(v) != null && Advanced.Get(v).baseCls == cls) p.adv = v; break;
                        case "trial": p.trialPassed = v == "1"; break;
                        default:
                            if (k.StartsWith("eq") && int.TryParse(k.Substring(2), out var slot) && slot >= 0 && slot < p.equipped.Length) p.equipped[slot] = Item.Parse(v);
                            break;
                    }
                }
                catch (Exception) { }
            }
            return p;
        }
    }

    /// <summary>存取：角色（每個職業一份）、背包（全部職業共用）。實際讀寫交給 Get/Set（遊戲裡是 PlayerPrefs）</summary>
    public static class Progress
    {
        public static Func<string, string> Get = k => "";
        public static Action<string, string> Set = (k, v) => { };
        public const int BagSize = 40;

        static readonly Dictionary<string, CharacterProfile> cache = new Dictionary<string, CharacterProfile>();
        static List<Item> bag;

        public static CharacterProfile Char(string cls)
        {
            if (!cache.TryGetValue(cls, out var p)) { p = CharacterProfile.Parse(cls, Get("sd_char_" + cls)); cache[cls] = p; }
            return p;
        }
        public static void SaveChar(CharacterProfile p) => Set("sd_char_" + p.cls, p.Serialize());

        public static List<Item> Bag
        {
            get
            {
                if (bag != null) return bag;
                bag = new List<Item>();
                foreach (var line in (Get("sd_bag") ?? "").Split('\n')) { var it = Item.Parse(line); if (it != null) bag.Add(it); }
                return bag;
            }
        }
        public static void SaveBag()
        {
            var sb = new StringBuilder();
            foreach (var it in Bag) sb.Append(it.Serialize()).Append('\n');
            Set("sd_bag", sb.ToString());
        }

        /// <summary>放進背包（滿了回傳 false）</summary>
        public static bool AddToBag(Item it)
        {
            if (it == null || Bag.Count >= BagSize) return false;
            Bag.Add(it); SaveBag();
            return true;
        }

        /// <summary>穿上背包裡的裝備（原本那件放回背包）</summary>
        public static bool Equip(CharacterProfile p, Item it, out string why)
        {
            if (!p.CanEquip(it, out why)) return false;
            int s = (int)it.slot;
            var old = p.equipped[s];
            Bag.Remove(it);
            if (old != null) Bag.Add(old);
            p.equipped[s] = it;
            SaveBag(); SaveChar(p);
            return true;
        }

        public static bool Unequip(CharacterProfile p, Slot slot)
        {
            var it = p.equipped[(int)slot];
            if (it == null || Bag.Count >= BagSize) return false;
            p.equipped[(int)slot] = null;
            Bag.Add(it);
            SaveBag(); SaveChar(p);
            return true;
        }

        public static void Discard(Item it) { if (Bag.Remove(it)) SaveBag(); }
    }
}
