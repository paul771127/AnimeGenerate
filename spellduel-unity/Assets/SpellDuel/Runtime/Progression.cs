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
            Stat.Agi => "蓄力時間 -1%／點（最多 -40%）、普攻速度：冷卻 -0.8%／點（最多 -50%）、閃避率 +0.1%／點",
            Stat.Spi => "MP 回復（基礎只有 30%，精神 99 點回到 100%）、冷卻時間 -0.8%／點（最多 -35%）",
            _ => "爆擊率 +0.6%／點（爆擊＝傷害 ×2）、掉寶機率與稀有度提升",
        };
    }

    // ---------------------------------------------------------------- 技能樹
    public enum NodeKind { Learn, Mod, Stat, Passive }
    public enum ModField { Damage, Cooldown, Charge, Multi, EffectDur, EffectDps, Heal, BuffAmount, BuffReduce, BuffDur, Cost, Pierce }
    public enum PassiveKind
    {
        Lifesteal, Crit, Dodge, MpOnHit, Regen, Burn, Poison, LastStand, Freeze, Evade, FastCast, Overcharge, NaturalRegen, QuickCd, Atk, Armor, Hp,
        // 流派（技能樹 v2）
        SpellEcho, BasicMp, Overload, LowMp, Melt, IceFreeze,          // 法師
        LongShot, SteadyAim, TrapCrit, ArrowBleed, ArrowPoison, ArrowSilence,   // 獵人
        ComboStrike, BasicLeech, WaveFlow, Riposte, OrbSlice,          // 劍士（OrbSlice＝斬擊能斬開法球）
        Venom, Assassinate, Vanish, KnifeCdr,                          // 刺客
        HolySeal, Homing, Martyr, Zeal,                                // 牧師
        // 進階職業（技能樹 v3）
        BashGuard, SanctionCombo, CritDmg, FalconMark, PetMp,
        CloseInstinct,
    }

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
        public int col, row;            // 畫面上的位置（舊的 3 欄排法；free＝樹狀圖的 7 欄格子）
        public bool free;               // 樹狀圖節點：前置只看 req（父節點），父節點點過 1 級就能點
    }

    public static class SkillTrees
    {
        /// <summary>「側跨」類特殊能力（舊的側跨無敵秒數）換算成閃避率：每 0.1 → +3%</summary>
        public const float DodgeToEvade = 0.3f;
        /// <summary>樹狀技能樹的三個流派名稱（左＝普攻流派、中、右）</summary>
        public static string[] Schools(string cls) => cls switch
        {
            "mage" => new[] { "🔮 奧術連射（普攻）", "💥 過載爆發", "❄🔥 元素反應" },
            "archer" => new[] { "🏹 速射（普攻）", "🎯 狙擊", "🪤 陷阱" },
            "swordsman" => new[] { "⚔ 連斬（普攻）", "🌙 劍氣", "🛡 鐵壁反擊" },
            "assassin" => new[] { "🧪 劇毒連刺（普攻）", "🗡 刺殺", "🔪 飛刀" },
            "priest" => new[] { "✨ 聖印（普攻）", "💚 治療守護", "🩸 殉道" },
            "paladin" => new[] { "🛡 聖盾", "🤝 守護", "⚖ 制裁" },
            "berserker" => new[] { "⚔ 輸出", "🛡 防禦", "✨ 輔助" },
            "marksman" => new[] { "🎯 貫穿", "☠ 標記", "🌧 箭雨" },
            "ranger" => new[] { "🦅 獵鷹", "🤸 翻滾", "🪤 連環陷阱" },
            "archmage" => new[] { "⚡ 雷霆", "❄ 冰封", "🔷 魔力護盾" },
            "summoner" => new[] { "🔥 火靈", "🗿 石像", "💥 爆裂" },
            "ninja" => new[] { "🍃 替身", "👥 分身", "🔥 火遁" },
            "shadowdancer" => new[] { "🗡 處決", "💃 影舞", "☁ 毒霧" },
            "bishop" => new[] { "🕊 復活", "💚 群療", "⛪ 庇護" },
            "inquisitor" => new[] { "🔱 聖槍", "⛓ 懲戒", "☀ 天罰" },
            "novice" => new[] { "🪵 木棍揮擊", "🪨 投石", "🩹 急救" },
            _ => null,
        };

        /// <summary>每個職業一開始會的 3 個基本技能</summary>
        public static string[] Starters(string cls) => cls switch
        {
            "swordsman" => new[] { "slash", "thrust", "block" },
            "archer" => new[] { "quickshot", "triple", "snaretrap" },
            "mage" => new[] { "fire", "wind", "ice" },
            "assassin" => new[] { "knife", "shadow", "smoke" },
            "priest" => new[] { "holy", "prayheal", "cleanse" },
            "novice" => new[] { "nv_swing", "nv_stone", "nv_aid" },   // 初心者：3 招要到炎之里解任務學（KnownSkills 只列學會的）；技能樹只有這 3 招的強化（各 5 級，學會才能點）
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
            if (n.free) { if (n.req != null) { var pr = Find(cls, n.req); if (pr != null) list.Add(pr); } return list; }
            TreeNode above = null;
            foreach (var o in Of(cls)) if (o.col == n.col && o.row < n.row && (above == null || o.row > above.row)) above = o;
            if (above != null) list.Add(above);
            if (n.req != null) { var r = Find(cls, n.req); if (r != null && r != above) list.Add(r); }
            return list;
        }

        // 樹狀圖節點（v2）：x、y 是格子位置，p 是父節點
        static TreeNode FMod(string id, string name, string skill, ModField f, float per, int max, int x, int y, string p) =>
            new TreeNode { id = id, name = name, kind = NodeKind.Mod, skill = skill, field = f, per = per, max = max, col = x, row = y, req = p, free = true };
        static TreeNode FLearn(string id, string skill, int x, int y, string p) =>
            new TreeNode { id = id, name = "習得：" + (Skills.All.TryGetValue(skill, out var s) ? s.name : skill), kind = NodeKind.Learn, skill = skill, max = 1, col = x, row = y, req = p, free = true };
        static TreeNode FStat(string id, string name, Stat st, int max, int x, int y, string p) =>
            new TreeNode { id = id, name = name, kind = NodeKind.Stat, stat = st, per = 2, max = max, col = x, row = y, req = p, free = true };
        static TreeNode FPas(string id, string name, PassiveKind k, float per, int max, int x, int y, string p) =>
            new TreeNode { id = id, name = name, kind = NodeKind.Passive, passive = k, per = per, max = max, col = x, row = y, req = p, free = true };

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
                // ======================================================== 樹狀技能樹（v3）：可升級的節點最高 5 級
                // 格子：x 0～6（左／中／右三個流派），y 由上往下；最後一個參數＝父節點（點過 1 級才能點）
                // 初心者：解任務學會的 3 招各一個強化節點（5 級；沒學會的不能點）。轉職時全部退點，拿去新職業的技能樹
                case "novice": return new List<TreeNode>
                {
                    FMod("nv_t_swing", "木棍揮擊強化", "nv_swing", ModField.Damage, 0.12f, 5, 1, 0, null),
                    FMod("nv_t_stone", "投石強化", "nv_stone", ModField.Damage, 0.12f, 5, 3, 0, null),
                    FMod("nv_t_aid", "急救強化", "nv_aid", ModField.Heal, 0.1f, 5, 5, 0, null),   // 特別算法（AidAt）：回復 20→30 HP、MP 18→30
                };
                case "mage": return new List<TreeNode>
                {
                    FStat("mg_root", "魔力親和", Stat.Int, 5, 3, 0, null),
                    // 奧術連射（普攻）
                    FMod("mg_bspd", "奧術速射", "na_mage", ModField.Cooldown, 0.08f, 5, 1, 1, "mg_root"),
                    FMod("mg_bdmg", "奧術強化", "na_mage", ModField.Damage, 0.15f, 5, 0, 2, "mg_bspd"),
                    FPas("mg_echo", "奧術回響", PassiveKind.SpellEcho, 0.1f, 5, 2, 2, "mg_bspd"),
                    FPas("mg_flow", "奧術迴路", PassiveKind.BasicMp, 1.5f, 5, 2, 3, "mg_echo"),
                    FPas("mg_fast", "快速詠唱", PassiveKind.FastCast, 0.06f, 5, 2, 4, "mg_flow"),
                    // 過載爆發
                    FMod("mg_fire", "火球強化", "fire", ModField.Damage, 0.1f, 5, 3, 1, "mg_root"),
                    FPas("mg_over", "過載", PassiveKind.Overload, 0.8f, 5, 3, 2, "mg_fire"),
                    FPas("mg_low", "孤注一擲", PassiveKind.LowMp, 0.1f, 5, 3, 3, "mg_over"),
                    FLearn("mg_med", "meditate", 3, 4, "mg_low"),
                    FLearn("mg_meteor", "meteor", 4, 3, "mg_over"),
                    FMod("mg_meteor2", "隕石強化", "meteor", ModField.Damage, 0.12f, 5, 4, 4, "mg_meteor"),
                    // 元素反應
                    FMod("mg_ice", "冰槍強化", "ice", ModField.Damage, 0.14f, 5, 5, 1, "mg_root"),
                    FPas("mg_frost", "寒冰", PassiveKind.IceFreeze, 0.15f, 5, 5, 2, "mg_ice"),
                    FPas("mg_melt", "融化", PassiveKind.Melt, 0.4f, 5, 5, 3, "mg_frost"),
                    FPas("mg_burn", "元素灼燒", PassiveKind.Burn, 0.05f, 5, 5, 4, "mg_melt"),
                    FMod("mg_wind", "風刃強化", "wind", ModField.Damage, 0.1f, 5, 6, 2, "mg_ice"),
                    FLearn("mg_dispel", "dispel", 6, 3, "mg_wind"),
                };
                case "archer": return new List<TreeNode>
                {
                    FStat("ar_root", "鷹眼", Stat.Agi, 5, 3, 0, null),
                    // 速射（普攻）
                    FMod("ar_bspd", "連弓", "na_arrow", ModField.Cooldown, 0.08f, 5, 1, 1, "ar_root"),
                    FMod("ar_bmulti", "分裂箭", "na_arrow", ModField.Multi, 1f, 2, 0, 2, "ar_bspd"),
                    FMod("ar_bdmg", "精製箭頭", "na_arrow", ModField.Damage, 0.12f, 5, 0, 3, "ar_bmulti"),
                    FMod("ar_bpierce", "穿透箭", "na_arrow", ModField.Pierce, 1f, 1, 1, 2, "ar_bspd"),
                    // 箭的附加效果（機率）
                    FPas("ar_bleed", "撕裂箭", PassiveKind.ArrowBleed, 0.06f, 5, 1, 3, "ar_bpierce"),
                    FPas("ar_venom", "毒箭", PassiveKind.ArrowPoison, 0.06f, 5, 1, 4, "ar_bleed"),
                    FPas("ar_silence", "封喉箭", PassiveKind.ArrowSilence, 0.04f, 5, 2, 4, "ar_bleed"),
                    FMod("ar_quick", "速射強化", "quickshot", ModField.Damage, 0.1f, 5, 2, 2, "ar_bspd"),
                    FMod("ar_triple", "多重箭", "triple", ModField.Multi, 1f, 2, 2, 3, "ar_quick"),
                    // 狙擊
                    FLearn("ar_snipe", "snipe", 3, 1, "ar_root"),
                    FMod("ar_snipe2", "狙擊強化", "snipe", ModField.Damage, 0.12f, 5, 3, 2, "ar_snipe"),
                    FPas("ar_steady", "穩定瞄準", PassiveKind.SteadyAim, 0.04f, 5, 3, 3, "ar_snipe2"),
                    FPas("ar_long", "遠射", PassiveKind.LongShot, 0.1f, 5, 4, 3, "ar_snipe2"),
                    FPas("ar_over", "蓄力強化", PassiveKind.Overcharge, 1f, 1, 4, 4, "ar_long"),
                    FPas("ar_crit", "銳利箭頭", PassiveKind.Crit, 0.03f, 5, 4, 5, "ar_over"),
                    // 陷阱
                    FMod("ar_snare", "捕獸夾強化", "snaretrap", ModField.EffectDur, 0.4f, 5, 5, 1, "ar_root"),
                    FLearn("ar_blast", "blasttrap", 5, 2, "ar_snare"),
                    FMod("ar_blast2", "陷阱大師", "blasttrap", ModField.Damage, 0.12f, 5, 5, 3, "ar_blast"),
                    FPas("ar_hunt", "獵殺時刻", PassiveKind.TrapCrit, 1f, 5, 6, 2, "ar_snare"),
                    FLearn("ar_focus", "focus", 6, 3, "ar_hunt"),
                    FPas("ar_dodge", "獵人直覺", PassiveKind.Dodge, 0.1f, 5, 6, 4, "ar_focus"),
                };
                case "swordsman": return new List<TreeNode>
                {
                    FStat("sw_root", "力量訓練", Stat.Str, 5, 3, 0, null),
                    // 連斬（普攻）
                    FMod("sw_bspd", "疾風劍", "na_sword", ModField.Cooldown, 0.08f, 5, 1, 1, "sw_root"),
                    FPas("sw_combo", "連斬", PassiveKind.ComboStrike, 0.25f, 5, 0, 2, "sw_bspd"),
                    FMod("sw_bdmg", "劍術精通", "na_sword", ModField.Damage, 0.1f, 5, 0, 3, "sw_combo"),
                    FPas("sw_leech", "飲血", PassiveKind.BasicLeech, 0.06f, 5, 1, 2, "sw_bspd"),
                    FPas("sw_regen", "自然回復", PassiveKind.NaturalRegen, 0.008f, 5, 1, 3, "sw_leech"),
                    // 劍氣
                    FMod("sw_slash", "斬擊強化", "slash", ModField.Damage, 0.1f, 5, 3, 1, "sw_root"),
                    FPas("sw_orb", "斬開法球", PassiveKind.OrbSlice, 1f, 1, 4, 2, "sw_slash"),   // 點了才會：斬擊斬開飛來的法球
                    FLearn("sw_wave", "wave", 3, 2, "sw_slash"),
                    FMod("sw_wave2", "劍氣分裂", "wave", ModField.Multi, 1f, 2, 2, 3, "sw_wave"),
                    FMod("sw_wavep", "劍氣穿透", "wave", ModField.Pierce, 1f, 1, 3, 3, "sw_wave"),
                    FPas("sw_flow", "劍意", PassiveKind.WaveFlow, 0.6f, 5, 4, 3, "sw_wave"),
                    FMod("sw_thrust", "突刺強化", "thrust", ModField.Damage, 0.1f, 5, 4, 4, "sw_flow"),
                    // 鐵壁反擊
                    FMod("sw_block", "格擋精通", "block", ModField.BuffReduce, 0.04f, 5, 5, 1, "sw_root"),
                    FPas("sw_rip", "反擊之刃", PassiveKind.Riposte, 0.3f, 5, 5, 2, "sw_block"),
                    FLearn("sw_counter", "counter", 5, 3, "sw_rip"),
                    FLearn("sw_iron", "ironwall", 6, 2, "sw_block"),
                    FMod("sw_iron2", "鐵壁強化", "ironwall", ModField.BuffAmount, 10f, 5, 6, 3, "sw_iron"),
                    FStat("sw_vit", "體魄訓練", Stat.Vit, 5, 6, 4, "sw_iron2"),
                    FPas("sw_last", "不屈", PassiveKind.LastStand, 0.06f, 5, 6, 5, "sw_vit"),
                    FLearn("sw_taunt", "taunt", 6, 6, "sw_last"),
                };
                case "assassin": return new List<TreeNode>
                {
                    FStat("as_root", "身手", Stat.Agi, 5, 3, 0, null),
                    // 劇毒連刺（普攻）
                    FMod("as_bspd", "疾刺", "na_dagger", ModField.Cooldown, 0.08f, 5, 1, 1, "as_root"),
                    FMod("as_bdmg", "淬毒匕首", "na_dagger", ModField.Damage, 0.12f, 5, 0, 2, "as_bspd"),
                    FPas("as_venom", "劇毒連刺", PassiveKind.Venom, 8f, 5, 1, 2, "as_bspd"),
                    FLearn("as_poison", "poison", 1, 3, "as_venom"),
                    FMod("as_poison2", "劇毒", "poison", ModField.EffectDps, 1.5f, 5, 1, 4, "as_poison"),
                    // 刺殺
                    FMod("as_shadow", "影襲強化", "shadow", ModField.Damage, 0.1f, 5, 3, 1, "as_root"),
                    FLearn("as_backstab", "backstab", 3, 2, "as_shadow"),
                    FPas("as_close", "近身本能", PassiveKind.CloseInstinct, 1f, 5, 2, 2, "as_shadow"),
                    FPas("as_assn", "暗殺術", PassiveKind.Assassinate, 0.15f, 5, 3, 3, "as_backstab"),
                    FMod("as_backstab2", "刺殺強化", "backstab", ModField.Damage, 0.12f, 5, 2, 4, "as_assn"),
                    FPas("as_vanish", "遁影", PassiveKind.Vanish, 0.15f, 5, 3, 4, "as_assn"),
                    FLearn("as_prep", "prep", 4, 3, "as_backstab"),
                    // 飛刀
                    FMod("as_knife", "飛刀連發", "knife", ModField.Multi, 1f, 1, 5, 1, "as_root"),
                    FPas("as_rhythm", "飛刀節奏", PassiveKind.KnifeCdr, 0.3f, 5, 5, 2, "as_knife"),
                    FPas("as_crit", "致命一擊", PassiveKind.Crit, 0.03f, 5, 5, 3, "as_rhythm"),
                    FMod("as_smoke", "濃煙", "smoke", ModField.EffectDur, 0.6f, 5, 6, 2, "as_knife"),
                    FPas("as_evade", "殘影", PassiveKind.Evade, 0.03f, 5, 6, 3, "as_smoke"),
                };
                case "priest": return new List<TreeNode>
                {
                    FStat("pr_root", "虔誠", Stat.Int, 5, 3, 0, null),
                    // 聖印（普攻）
                    FMod("pr_bspd", "聖光連射", "na_holy", ModField.Cooldown, 0.08f, 5, 1, 1, "pr_root"),
                    FPas("pr_home", "聖光導引", PassiveKind.Homing, 18f, 5, 0, 2, "pr_bspd"),
                    FPas("pr_seal", "聖印", PassiveKind.HolySeal, 0.8f, 5, 1, 2, "pr_bspd"),
                    FLearn("pr_judge", "judgement", 1, 3, "pr_seal"),
                    FMod("pr_judge2", "審判強化", "judgement", ModField.Damage, 0.12f, 5, 1, 4, "pr_judge"),
                    // 治療守護
                    FMod("pr_heal", "治療強化", "prayheal", ModField.Heal, 0.12f, 5, 3, 1, "pr_root"),
                    FLearn("pr_guard", "guard", 2, 2, "pr_heal"),
                    FMod("pr_guard2", "守護強化", "guard", ModField.BuffReduce, 0.04f, 5, 2, 3, "pr_guard"),
                    FLearn("pr_prayer", "prayer", 2, 4, "pr_guard2"),
                    FPas("pr_regen", "神聖庇護", PassiveKind.Regen, 0.4f, 5, 3, 2, "pr_heal"),
                    FStat("pr_vit", "堅毅", Stat.Vit, 5, 3, 3, "pr_regen"),
                    FMod("pr_cleanse", "淨化強化", "cleanse", ModField.Cooldown, 0.1f, 5, 4, 3, "pr_regen"),
                    // 殉道
                    FMod("pr_holy", "聖光強化", "holy", ModField.Damage, 0.1f, 5, 5, 1, "pr_root"),
                    FPas("pr_martyr", "殉道", PassiveKind.Martyr, 1f, 1, 5, 2, "pr_holy"),
                    FPas("pr_zeal", "狂信", PassiveKind.Zeal, 0.2f, 5, 5, 3, "pr_martyr"),
                    FPas("pr_leech", "聖血", PassiveKind.Lifesteal, 0.03f, 5, 6, 3, "pr_martyr"),
                    FPas("pr_quick", "快速施法", PassiveKind.QuickCd, 0.06f, 5, 6, 2, "pr_holy"),
                };
                // ======================================================== 進階職業（轉職時已學會第一個進階技能）：三個流派，可以和基本職業的流派搭配
                case "paladin": return new List<TreeNode>
                {
                    FStat("pa_root", "聖騎士之軀", Stat.Vit, 5, 3, 0, null),
                    FMod("pa_bash", "聖盾強化", "shieldbash", ModField.Damage, 0.12f, 5, 1, 1, "pa_root"),
                    FMod("pa_bash2", "震懾", "shieldbash", ModField.EffectDur, 0.2f, 5, 0, 2, "pa_bash"),
                    FMod("pa_bash3", "衝鋒熟練", "shieldbash", ModField.Cooldown, 0.08f, 5, 0, 3, "pa_bash2"),
                    FPas("pa_guard", "盾擊格擋", PassiveKind.BashGuard, 0.4f, 5, 2, 2, "pa_bash"),
                    FPas("pa_rip", "聖光反擊", PassiveKind.Riposte, 0.2f, 5, 2, 3, "pa_guard"),
                    FLearn("pa_oath", "oath", 3, 1, "pa_root"),
                    FMod("pa_oath2", "誓言延長", "oath", ModField.BuffDur, 1f, 5, 3, 2, "pa_oath"),
                    FPas("pa_last", "堅守", PassiveKind.LastStand, 0.05f, 5, 3, 3, "pa_oath2"),
                    FPas("pa_armor", "神聖護甲", PassiveKind.Armor, 0.02f, 5, 4, 2, "pa_oath"),
                    FPas("pa_hp", "聖騎士之心", PassiveKind.Hp, 10f, 5, 4, 3, "pa_armor"),
                    FLearn("pa_sanc", "sanction", 5, 1, "pa_root"),
                    FMod("pa_sanc2", "制裁強化", "sanction", ModField.Damage, 0.12f, 5, 5, 2, "pa_sanc"),
                    FMod("pa_sanc3", "制裁熟練", "sanction", ModField.Cooldown, 0.08f, 5, 5, 3, "pa_sanc2"),
                    FPas("pa_scombo", "制裁連斬", PassiveKind.SanctionCombo, 1f, 1, 6, 2, "pa_sanc"),
                    FPas("pa_atk", "聖戰之力", PassiveKind.Atk, 0.03f, 5, 6, 3, "pa_scombo"),
                };
                // 劍仙：三個流派——輸出（劍來、御劍術、萬劍歸宗）／防禦（劍罡護體）／輔助（劍陣）
                case "berserker": return new List<TreeNode>
                {
                    FStat("sx_root", "劍心", Stat.Str, 5, 3, 0, null),
                    // ⚔ 輸出
                    FMod("sx_jl", "劍來延長", "jianlai", ModField.BuffDur, 2f, 5, 1, 1, "sx_root"),
                    FPas("sx_crit", "劍意", PassiveKind.Crit, 0.02f, 5, 0, 2, "sx_jl"),
                    FLearn("sx_yj", "yujian", 1, 2, "sx_jl"),
                    FPas("sx_combo", "連斬", PassiveKind.ComboStrike, 0.2f, 5, 2, 2, "sx_jl"),
                    FMod("sx_yj2", "御劍數量", "yujian", ModField.Multi, 1f, 5, 1, 3, "sx_yj"),
                    FMod("sx_yj3", "飛劍強化", "yujian", ModField.Damage, 0.12f, 5, 2, 3, "sx_yj"),
                    FLearn("sx_wj", "wanjian", 0, 3, "sx_crit"),
                    FMod("sx_wj2", "萬劍強化", "wanjian", ModField.Damage, 0.12f, 5, 0, 4, "sx_wj"),
                    FPas("sx_atk", "劍氣縱橫", PassiveKind.Atk, 0.03f, 5, 1, 4, "sx_yj2"),
                    FLearn("sx_yjkt", "yijian", 2, 4, "sx_yj3"),
                    FMod("sx_yjkt2", "開天強化", "yijian", ModField.Damage, 0.12f, 5, 2, 5, "sx_yjkt"),
                    FMod("sx_yjkt3", "開天蓄勢", "yijian", ModField.Charge, 0.08f, 5, 1, 5, "sx_yjkt"),
                    // 🛡 防禦
                    FLearn("sx_jg", "jiangang", 3, 1, "sx_root"),
                    FMod("sx_jg2", "劍罡加厚", "jiangang", ModField.BuffAmount, 15f, 5, 3, 2, "sx_jg"),
                    FPas("sx_armor", "劍骨", PassiveKind.Armor, 0.03f, 5, 4, 2, "sx_jg"),
                    FPas("sx_hp", "養氣", PassiveKind.Hp, 10f, 5, 3, 3, "sx_jg2"),
                    FPas("sx_last", "劍心不滅", PassiveKind.LastStand, 0.05f, 5, 4, 3, "sx_armor"),
                    // ✨ 輔助
                    FLearn("sx_jz", "jianzhen", 5, 1, "sx_root"),
                    FMod("sx_jz2", "劍陣加厚", "jianzhen", ModField.BuffAmount, 10f, 5, 5, 2, "sx_jz"),
                    FPas("sx_leech", "劍氣吸血", PassiveKind.Lifesteal, 0.02f, 5, 6, 2, "sx_jz"),
                    FMod("sx_jzcd", "陣法回氣", "jianzhen", ModField.Cooldown, 0.08f, 5, 5, 3, "sx_jz2"),
                    FPas("sx_mp", "劍氣回流", PassiveKind.MpOnHit, 1f, 5, 6, 3, "sx_leech"),
                };
                case "marksman": return new List<TreeNode>
                {
                    FStat("mk_root", "神射", Stat.Luk, 5, 3, 0, null),
                    FMod("mk_pierce", "貫穿強化", "pierce", ModField.Damage, 0.12f, 5, 1, 1, "mk_root"),
                    FMod("mk_pierce2", "快速上弦", "pierce", ModField.Charge, 0.08f, 5, 0, 2, "mk_pierce"),
                    FPas("mk_long", "超遠射", PassiveKind.LongShot, 0.08f, 5, 1, 2, "mk_pierce"),
                    FPas("mk_critd", "要害一擊", PassiveKind.CritDmg, 0.2f, 5, 2, 2, "mk_pierce"),
                    FPas("mk_crit", "要害瞄準", PassiveKind.Crit, 0.03f, 5, 2, 3, "mk_critd"),
                    FLearn("mk_mark", "deadmark", 3, 1, "mk_root"),
                    FMod("mk_mark2", "標記熟練", "deadmark", ModField.Cooldown, 0.08f, 5, 3, 2, "mk_mark"),
                    FPas("mk_atk", "強弓", PassiveKind.Atk, 0.03f, 5, 3, 3, "mk_mark2"),
                    FPas("mk_steady", "鷹之靜", PassiveKind.SteadyAim, 0.03f, 5, 4, 2, "mk_mark"),
                    FLearn("mk_rain", "arrowrain", 5, 1, "mk_root"),
                    FMod("mk_rain2", "密集箭雨", "arrowrain", ModField.Multi, 1f, 3, 5, 2, "mk_rain"),
                    FMod("mk_rain3", "箭雨強化", "arrowrain", ModField.Damage, 0.12f, 5, 6, 2, "mk_rain"),
                    FMod("mk_bspd", "連珠箭", "na_arrow", ModField.Cooldown, 0.06f, 5, 6, 3, "mk_rain3"),
                };
                case "ranger": return new List<TreeNode>
                {
                    FStat("rg_root", "疾行", Stat.Agi, 5, 3, 0, null),
                    FMod("rg_falcon", "獵鷹強化", "falcon", ModField.Damage, 0.15f, 5, 1, 1, "rg_root"),
                    FMod("rg_falcon2", "獵鷹長駐", "falcon", ModField.BuffDur, 3f, 5, 0, 2, "rg_falcon"),
                    FPas("rg_fmark", "鷹眼標記", PassiveKind.FalconMark, 0.06f, 5, 1, 2, "rg_falcon"),
                    FMod("rg_fhp", "強健獵鷹", "falcon", ModField.BuffAmount, 10f, 5, 2, 2, "rg_falcon"),
                    FLearn("rg_roll", "rollshot", 3, 1, "rg_root"),
                    FMod("rg_roll2", "翻滾熟練", "rollshot", ModField.Cooldown, 0.08f, 5, 3, 2, "rg_roll"),
                    FMod("rg_bspd", "游擊連射", "na_arrow", ModField.Cooldown, 0.06f, 5, 3, 3, "rg_roll2"),
                    FPas("rg_evade", "游擊步法", PassiveKind.Evade, 0.03f, 5, 4, 2, "rg_roll"),
                    FLearn("rg_chain", "trapchain", 5, 1, "rg_root"),
                    FMod("rg_chain2", "陷阱專家", "trapchain", ModField.Damage, 0.15f, 5, 5, 2, "rg_chain"),
                    FPas("rg_hunt", "圍獵", PassiveKind.TrapCrit, 0.6f, 5, 6, 2, "rg_chain"),
                    FMod("rg_snare", "捕獸夾改良", "snaretrap", ModField.EffectDur, 0.3f, 5, 6, 3, "rg_hunt"),
                };
                case "archmage": return new List<TreeNode>
                {
                    FStat("am_root", "大智慧", Stat.Int, 5, 3, 0, null),
                    FMod("am_tj", "雷霆強化", "tjudge", ModField.Damage, 0.12f, 5, 1, 1, "am_root"),
                    FMod("am_tj2", "雷霆連擊", "tjudge", ModField.Multi, 1f, 2, 0, 2, "am_tj"),
                    FPas("am_over", "雷霆過載", PassiveKind.Overload, 0.5f, 5, 1, 2, "am_tj"),
                    FPas("am_fast", "高速詠唱", PassiveKind.FastCast, 0.04f, 5, 2, 2, "am_tj"),
                    FLearn("am_frost", "frostfield", 3, 1, "am_root"),
                    FMod("am_frost2", "永凍", "frostfield", ModField.EffectDur, 0.3f, 5, 3, 2, "am_frost"),
                    FPas("am_freeze", "極寒", PassiveKind.IceFreeze, 0.08f, 5, 3, 3, "am_frost2"),
                    FPas("am_melt", "冰火交融", PassiveKind.Melt, 0.25f, 5, 4, 2, "am_frost"),
                    FLearn("am_shield", "manashield", 5, 1, "am_root"),
                    FMod("am_shield2", "護盾延長", "manashield", ModField.BuffDur, 1.5f, 5, 5, 2, "am_shield"),
                    FPas("am_low", "背水之陣", PassiveKind.LowMp, 0.08f, 5, 6, 2, "am_shield"),
                    FPas("am_mp", "魔力虹吸", PassiveKind.MpOnHit, 1f, 5, 6, 3, "am_low"),
                };
                case "summoner": return new List<TreeNode>
                {
                    FStat("su_root", "契約之力", Stat.Spi, 5, 3, 0, null),
                    FMod("su_fire", "火靈強化", "firespirit", ModField.Damage, 0.15f, 5, 1, 1, "su_root"),
                    FMod("su_fire2", "火靈長駐", "firespirit", ModField.BuffDur, 3f, 5, 0, 2, "su_fire"),
                    FPas("su_petmp", "契約魔力", PassiveKind.PetMp, 1f, 5, 1, 2, "su_fire"),
                    FPas("su_burn", "火靈灼燒", PassiveKind.Burn, 0.04f, 5, 2, 2, "su_fire"),
                    FLearn("su_golem", "golem", 3, 1, "su_root"),
                    FMod("su_golem2", "堅固石像", "golem", ModField.BuffAmount, 20f, 5, 3, 2, "su_golem"),
                    FPas("su_hp", "共生", PassiveKind.Hp, 10f, 5, 3, 3, "su_golem2"),
                    FPas("su_armor", "石之守護", PassiveKind.Armor, 0.02f, 5, 4, 2, "su_golem"),
                    FLearn("su_det", "detonate", 5, 1, "su_root"),
                    FMod("su_det2", "爆裂熟練", "detonate", ModField.Cooldown, 0.08f, 5, 5, 2, "su_det"),
                    FPas("su_atk", "元素之力", PassiveKind.Atk, 0.03f, 5, 6, 2, "su_det"),
                    FPas("su_echo", "元素共鳴", PassiveKind.SpellEcho, 0.06f, 5, 6, 3, "su_atk"),
                };
                case "ninja": return new List<TreeNode>
                {
                    FStat("nj_root", "忍者身手", Stat.Agi, 5, 3, 0, null),
                    FMod("nj_sub", "替身熟練", "substitute", ModField.Cooldown, 0.08f, 5, 1, 1, "nj_root"),
                    FPas("nj_vanish", "隱遁", PassiveKind.Vanish, 0.1f, 5, 0, 2, "nj_sub"),
                    FPas("nj_evade", "殘像", PassiveKind.Evade, 0.03f, 5, 1, 2, "nj_sub"),
                    FPas("nj_dodge", "瞬身", PassiveKind.Dodge, 0.1f, 5, 2, 2, "nj_sub"),
                    FLearn("nj_clone", "clone", 3, 1, "nj_root"),
                    FMod("nj_clone2", "分身延長", "clone", ModField.BuffDur, 1f, 5, 3, 2, "nj_clone"),
                    FPas("nj_crit", "影分身・要害", PassiveKind.Crit, 0.03f, 5, 3, 3, "nj_clone2"),
                    FPas("nj_knife", "苦無連發", PassiveKind.KnifeCdr, 0.2f, 5, 4, 2, "nj_clone"),
                    FLearn("nj_fire", "fireseal", 5, 1, "nj_root"),
                    FMod("nj_fire2", "火遁強化", "fireseal", ModField.Damage, 0.12f, 5, 5, 2, "nj_fire"),
                    FMod("nj_fire3", "火遁擴散", "fireseal", ModField.Multi, 1f, 2, 6, 2, "nj_fire"),
                    FPas("nj_burn", "業火", PassiveKind.Burn, 0.05f, 5, 6, 3, "nj_fire3"),
                };
                case "shadowdancer": return new List<TreeNode>
                {
                    FStat("sd_root", "暗殺之力", Stat.Str, 5, 3, 0, null),
                    FMod("sd_exe", "處決強化", "execute", ModField.Damage, 0.12f, 5, 1, 1, "sd_root"),
                    FMod("sd_exe2", "快速處決", "execute", ModField.Charge, 0.08f, 5, 0, 2, "sd_exe"),
                    FPas("sd_assn", "影之暗殺", PassiveKind.Assassinate, 0.08f, 5, 1, 2, "sd_exe"),
                    FPas("sd_crit", "致命舞步", PassiveKind.Crit, 0.03f, 5, 2, 2, "sd_exe"),
                    FLearn("sd_dance", "shadowdance", 3, 1, "sd_root"),
                    FMod("sd_dance2", "影舞延長", "shadowdance", ModField.BuffDur, 1f, 5, 3, 2, "sd_dance"),
                    FPas("sd_atk", "影之刃", PassiveKind.Atk, 0.03f, 5, 3, 3, "sd_dance2"),
                    FPas("sd_vanish", "舞影", PassiveKind.Vanish, 0.1f, 5, 4, 2, "sd_dance"),
                    FLearn("sd_fog", "poisonfog", 5, 1, "sd_root"),
                    FMod("sd_fog2", "劇毒之霧", "poisonfog", ModField.EffectDps, 1.5f, 5, 5, 2, "sd_fog"),
                    FPas("sd_venom", "毒霧疊毒", PassiveKind.Venom, 6f, 5, 6, 2, "sd_fog"),
                    FMod("sd_bspd", "毒舞連刺", "na_dagger", ModField.Cooldown, 0.06f, 5, 6, 3, "sd_venom"),
                };
                case "bishop": return new List<TreeNode>
                {
                    FStat("bi_root", "虔誠之心", Stat.Spi, 5, 3, 0, null),
                    FMod("bi_rev", "復活熟練", "revive", ModField.Cooldown, 0.08f, 5, 1, 1, "bi_root"),
                    FPas("bi_hp", "生命之光", PassiveKind.Hp, 10f, 5, 0, 2, "bi_rev"),
                    FPas("bi_regen", "聖靈", PassiveKind.Regen, 0.3f, 5, 1, 2, "bi_rev"),
                    FPas("bi_nat", "恩典", PassiveKind.NaturalRegen, 0.008f, 5, 2, 2, "bi_rev"),
                    FLearn("bi_heal", "massheal", 3, 1, "bi_root"),
                    FMod("bi_heal2", "群體治療強化", "massheal", ModField.BuffAmount, 4f, 5, 3, 2, "bi_heal"),
                    FMod("bi_heal3", "治療熟練", "massheal", ModField.Cooldown, 0.08f, 5, 3, 3, "bi_heal2"),
                    FPas("bi_quick", "神速禱告", PassiveKind.QuickCd, 0.05f, 5, 4, 2, "bi_heal"),
                    FLearn("bi_sanc", "sanctuary", 5, 1, "bi_root"),
                    FMod("bi_sanc2", "庇護強化", "sanctuary", ModField.BuffAmount, 10f, 5, 5, 2, "bi_sanc"),
                    FPas("bi_seal", "聖域之印", PassiveKind.HolySeal, 0.6f, 5, 6, 2, "bi_sanc"),
                    FPas("bi_armor", "神聖帷幕", PassiveKind.Armor, 0.02f, 5, 6, 3, "bi_seal"),
                };
                case "inquisitor": return new List<TreeNode>
                {
                    FStat("iq_root", "審判之力", Stat.Int, 5, 3, 0, null),
                    FMod("iq_lance", "聖槍強化", "holylance", ModField.Damage, 0.12f, 5, 1, 1, "iq_root"),
                    FMod("iq_lance2", "聖槍連射", "holylance", ModField.Multi, 1f, 2, 0, 2, "iq_lance"),
                    FPas("iq_critd", "裁決之刃", PassiveKind.CritDmg, 0.15f, 5, 1, 2, "iq_lance"),
                    FPas("iq_crit", "裁決", PassiveKind.Crit, 0.03f, 5, 2, 2, "iq_lance"),
                    FLearn("iq_chast", "chastise", 3, 1, "iq_root"),
                    FMod("iq_chast2", "懲戒延長", "chastise", ModField.EffectDur, 1f, 5, 3, 2, "iq_chast"),
                    FPas("iq_atk", "聖戰", PassiveKind.Atk, 0.03f, 5, 3, 3, "iq_chast2"),
                    FPas("iq_seal", "審判之印", PassiveKind.HolySeal, 0.6f, 5, 4, 2, "iq_chast"),
                    FLearn("iq_heaven", "heavenpunish", 5, 1, "iq_root"),
                    FMod("iq_heaven2", "天罰強化", "heavenpunish", ModField.Damage, 0.12f, 5, 5, 2, "iq_heaven"),
                    FPas("iq_zeal", "殉教之怒", PassiveKind.Zeal, 0.15f, 5, 6, 2, "iq_heaven"),
                    FPas("iq_leech", "聖血契約", PassiveKind.Lifesteal, 0.02f, 5, 6, 3, "iq_zeal"),
                };
            }
            return new List<TreeNode>();
        }

        /// <summary>急救強化：每級回復 +2 HP（20→30）、MP 消耗也變多（18→30）</summary>
        public const string AidNode = "nv_t_aid";
        public const int AidMaxHeal = 30, AidMaxCost = 30;
        public static (int heal, int cost) AidAt(int rank)
        {
            var b = Skills.All["nv_aid"];
            float t = Mathf.Clamp01(rank / 5f);
            return (Mathf.RoundToInt(Mathf.Lerp(b.heal, AidMaxHeal, t)), Mathf.RoundToInt(Mathf.Lerp(b.cost, AidMaxCost, t)));
        }

        /// <summary>節點每一級的效果文字</summary>
        public static string Describe(TreeNode n, int rank)
        {
            string skill = n.skill != null && Skills.All.TryGetValue(n.skill, out var s) ? s.name : n.skill;
            float v = n.per * Mathf.Max(1, rank);
            if (n.id == AidNode) { var (h, c) = AidAt(Mathf.Max(1, rank)); return $"急救 回復 {h} HP、消耗 {c} MP"; }
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
                        ModField.Pierce => $"{skill} 可以穿透敵人（打到小怪後繼續飛）",
                        _ => $"{skill} 持續 +{v:0.#} 秒",
                    };
                default:
                    return n.passive switch
                    {
                        PassiveKind.Lifesteal => $"造成傷害的 {v * 100:0}% 回復成 HP",
                        PassiveKind.Crit => $"爆擊率 +{v * 100:0}%",
                        PassiveKind.Dodge => $"閃避率 +{v * DodgeToEvade * 100f:0.#}%",
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
                        PassiveKind.SpellEcho => $"奧術飛彈命中時 {v * 100:0}% 機率追加一發小火球或小冰錐（小冰錐冰凍 0.8 秒）",
                        PassiveKind.BasicMp => $"普攻命中回復 {v:0} MP",
                        PassiveKind.Overload => $"法師技能傷害 +{v * 15:0}%，但 MP 消耗 +{v * 20:0}%",
                        PassiveKind.LowMp => $"MP 低於 30% 時，所有傷害 +{v * 100:0}%",
                        PassiveKind.Melt => $"火球、隕石、回響火球打中被冰凍的敵人：傷害 +{v * 100:0}%，並解除冰凍",
                        PassiveKind.IceFreeze => $"冰槍命中時 {v * 100:0}% 機率冰凍 1.5 秒",
                        PassiveKind.LongShot => $"箭飛超過 {Battle.LongShotDist:0}m 才命中：傷害 +{v * 100:0}%",
                        PassiveKind.SteadyAim => $"站著不動 2 秒以上，射出的箭爆擊率 +{v * 100:0}%",
                        PassiveKind.TrapCrit => $"陷阱觸發後 {v:0} 秒內，下一個弓箭技能必定爆擊",
                        PassiveKind.ArrowBleed => $"箭命中時 {v * 100:0}% 機率出血：每秒 {Battle.BleedDps:0} 傷害、{Battle.BleedDur:0} 秒（可疊加）",
                        PassiveKind.ArrowPoison => $"箭命中時 {v * 100:0}% 機率中毒：每秒 3 傷害、6 秒（可疊加）",
                        PassiveKind.ArrowSilence => $"箭命中時 {v * 100:0}% 機率沉默 {Battle.SilenceDur:0} 秒：不能唸技能、正在唸的被打斷（Boss 減半）",
                        PassiveKind.ComboStrike => $"普攻每第 3 下是重擊：傷害 +{v * 100:0}%",
                        PassiveKind.OrbSlice => $"「斬擊」揮出後 {Battle.SliceWindow:0.#} 秒內，斬開正面 {Battle.SliceRange:0.#}m 內飛來的法球（從天而降、地裂、橫掃等範圍攻擊無效）",
                        PassiveKind.BasicLeech => $"普攻造成傷害的 {v * 100:0}% 回復成 HP",
                        PassiveKind.WaveFlow => $"斬擊後 {v:0} 秒內，劍氣不花 MP",
                        PassiveKind.Riposte => $"格擋成功後 4 秒內，下一擊傷害 +{v * 100:0}%",
                        PassiveKind.Venom => $"普攻在敵人身上疊毒（毒刃一次 2 層），疊滿 {Battle.VenomMax} 層爆開造成 {v:0} 傷害",
                        PassiveKind.Assassinate => $"離開敵人視線累計 1 秒（可以分好幾次）就完成伏擊蓄力（身邊有敵人也能開始），伏擊維持 {Battle.StealthAmbushWindow:0} 秒、靠近就出手；伏擊技能傷害 +{v * 100:0}%，目標 HP 低於 40% 時再 +{v * 100:0}%",
                        PassiveKind.Vanish => $"伏擊出手後 2 秒內閃避率 +{v * 100:0}%",
                        PassiveKind.KnifeCdr => $"飛刀命中時，其他技能冷卻 -{v:0.#} 秒",
                        PassiveKind.HolySeal => $"神聖飛彈命中累積聖印，{Mathf.Max(2f, 6f - v):0} 層時下一個攻擊技能傷害 ×2",
                        PassiveKind.Homing => $"神聖飛彈會追蹤最近的敵人（每秒轉 {v:0} 度）",
                        PassiveKind.Martyr => "攻擊技能改用 HP 施放（MP 的一半），不花 MP",
                        PassiveKind.Zeal => $"HP 越低傷害越高：HP 接近 0 時傷害 +{v * 100:0}%",
                        PassiveKind.BashGuard => $"聖盾衝鋒命中後 {v:0.#} 秒內自動格擋（可觸發反擊之刃）",
                        PassiveKind.SanctionCombo => "神聖制裁命中後，下一下普攻直接是重擊（搭配連斬）",
                        PassiveKind.CritDmg => $"爆擊傷害 +{v * 100:0}%（原本 ×2）",
                        PassiveKind.FalconMark => $"獵鷹攻擊時 {v * 100:0}% 機率標記：4 秒內下一個弓箭技能必定爆擊",
                        PassiveKind.PetMp => $"召喚物命中時回復 {v:0.#} MP",
                        PassiveKind.CloseInstinct => $"2m 內有敵人時：受到的傷害 −{v * Battle.CloseReducePer * 100:0}%、閃避率 +{v * Battle.CloseEvadePer * 100:0}%（持續傷害不減）",
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
        /// <summary>精魄孔數（0～4，掉落時隨機，越稀有上限越高）與鑲上去的精魄（Souls.cs）</summary>
        public int soulSlots;
        public readonly List<string> souls = new List<string>();
        /// <summary>顯示用名稱：+強化等級</summary>
        public string FullName => (plus > 0 ? $"+{plus} " : "") + name;

        public static readonly string[] SlotNames = { "武器", "頭部", "身體", "鞋子", "飾品" };
        public static readonly string[] RarityNames = { "普通", "優良", "稀有", "史詩", "傳說" };
        public static readonly Color[] RarityColors =
        {
            new Color(0.85f, 0.85f, 0.85f), new Color(0.4f, 0.95f, 0.45f), new Color(0.4f, 0.65f, 1f),
            new Color(0.78f, 0.45f, 1f), new Color(1f, 0.65f, 0.15f),
        };

        public bool ClassOk(string cls)
        {
            if (string.IsNullOrEmpty(classes)) return true;
            var cs = classes.Split(',');
            if (Array.IndexOf(cs, cls) >= 0) return true;
            // 初心者：可以穿劍士、刺客的裝備（拿木棍近戰，和他們同一路）
            return cls == Skills.Novice && (Array.IndexOf(cs, "swordsman") >= 0 || Array.IndexOf(cs, "assassin") >= 0);
        }

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
            sb.Append('|').Append(soulSlots).Append('|').Append(string.Join(",", souls));
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
                if (p.Length >= 14)
                {
                    int.TryParse(p[12], out it.soulSlots); it.soulSlots = Mathf.Clamp(it.soulSlots, 0, Souls.MaxSlots);
                    if (p[13].Length > 0) foreach (var id in p[13].Split(',')) if (Souls.Get(id) != null && it.souls.Count < it.soulSlots) it.souls.Add(id);
                }
                else it.soulSlots = Souls.SlotsFromUid(it.uid, it.rarity);   // 精魄之前的舊裝備：照 uid 固定給孔數
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
            if (prefer == Skills.Novice) prefer = null;   // 初心者：什麼職業的都可能掉（轉職後用得到），不鎖成初心者專用
            ilvl = Mathf.Max(1, ilvl);
            var slot = (Slot)rng.Next(5);
            var it = new Item { slot = slot, rarity = rarity, ilvl = ilvl, uid = DateTime.UtcNow.Ticks + rng.Next(100000), soulSlots = Souls.RollSlots(rng, rarity) };
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
        public int level = 1, xp, statPoints, skillPoints = SkillPerLevel;   // 技能點：1 級就有 1 點，之後每升一級 +1（30 級共 30 點）
        public readonly int[] alloc = new int[StatInfo.Count];        // 自己加的能力點（不含初始值、技能樹、裝備）
        public readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
        public readonly Item[] equipped = new Item[5];
        public string adv;              // 進階職業（Advanced.All）；null＝還沒轉職
        public bool trialPassed;        // 轉職試煉通過了（可以選進階職業）
        public AdvDef Adv => Advanced.Get(adv);
        /// <summary>從初心者轉職來的：初心者的技能樹保留（點過的不能重置，也不能把點數拿去別的樹）</summary>
        public bool novicePast;
        /// <summary>這個角色所有的技能樹節點（初心者（轉職前的）＋基本＋進階）</summary>
        public IEnumerable<TreeNode> Nodes
        {
            get
            {
                if (novicePast && cls != Skills.Novice) foreach (var n in SkillTrees.Of(Skills.Novice)) yield return n;
                foreach (var n in SkillTrees.AllOf(cls, adv)) yield return n;
            }
        }
        /// <summary>節點屬於哪一棵樹（初心者、基本職業 id 或進階職業 id）</summary>
        public string TreeOf(TreeNode n) => SkillTrees.Of(Skills.Novice).Contains(n) ? Skills.Novice : adv != null && SkillTrees.Of(adv).Contains(n) ? adv : cls;
        /// <summary>這個角色有的技能樹（依階段：初心者 → 基本職業 → 進階職業）</summary>
        public List<string> Trees
        {
            get
            {
                var l = new List<string>();
                if (cls == Skills.Novice || novicePast) l.Add(Skills.Novice);
                if (cls != Skills.Novice) l.Add(cls);
                if (!string.IsNullOrEmpty(adv)) l.Add(adv);
                return l;
            }
        }
        /// <summary>
        /// 這棵樹能用的技能點（每個階段的點數只能點那個階段的樹，不能挪）：
        ///   初心者＝1～10 級拿到的（10 點）；基本職業＝11～30 級（從初心者轉職來的；20 點）；進階職業＝31 級以後。
        /// </summary>
        public int EarnedIn(string tree)
        {
            if (tree == Skills.Novice) return cls == Skills.Novice || novicePast ? Mathf.Min(level, Skills.JobLevel) * SkillPerLevel : 0;
            if (tree == cls) return Mathf.Max(0, Mathf.Min(level, Advanced.Level) - (novicePast ? Skills.JobLevel : 0)) * SkillPerLevel;
            if (!string.IsNullOrEmpty(adv) && tree == adv) return Mathf.Max(0, level - Advanced.Level) * SkillPerLevel;
            return 0;
        }
        public int SpentIn(string tree) { int v = 0; foreach (var n in SkillTrees.Of(tree)) v += Rank(n.id); return v; }
        public int AvailIn(string tree) => Mathf.Max(0, EarnedIn(tree) - SpentIn(tree));
        /// <summary>重新算剩下的技能點（各棵樹剩下的加起來）</summary>
        void RecalcSkillPoints() { int v = 0; foreach (var t in Trees) v += AvailIn(t); skillPoints = v; }
        static string TreeName(string tree) => Advanced.Get(tree)?.name ?? (Skills.Classes.TryGetValue(tree, out var c) ? c.name : tree);
        public bool CanAdvance => adv == null && level >= Advanced.Level && trialPassed;
        /// <summary>轉職等級（30 級）還沒轉職：不能再升級（經驗停在滿格），要先完成轉職試煉並轉職</summary>
        public bool LevelLocked => cls == Skills.Novice ? level >= Skills.JobLevel : adv == null && level >= Advanced.Level;
        /// <summary>初心者到 10 級、轉職試煉通過：可以到職業工會選職業</summary>
        public bool CanChangeJob => cls == Skills.Novice && level >= Skills.JobLevel && jobTrial >= Skills.JobTrialRounds;
        /// <summary>初心者的轉職試煉通過了幾輪（0～3）</summary>
        public int jobTrial;
        /// <summary>初心者：解任務學會的招式、接了還沒完成的任務（技能 id → 進度）</summary>
        public readonly HashSet<string> noviceLearned = new HashSet<string>();
        public readonly Dictionary<string, int> noviceQuest = new Dictionary<string, int>();
        /// <summary>背包裡還沒鑲上去的精魄（id → 個數）</summary>
        public readonly Dictionary<string, int> souls = new Dictionary<string, int>();

        /// <summary>打倒了 n 隻敵人：打倒型任務 +n、收集型任務每隻有機率掉一個。回傳有沒有進度變化</summary>
        public bool NoviceKills(int n, System.Random rng)
        {
            if (cls != Skills.Novice || n <= 0) return false;
            bool changed = false;
            foreach (var q in NoviceQuests.All)
            {
                if (!noviceQuest.TryGetValue(q.skill, out var have) || have >= q.need) continue;
                if (q.kind == "kill") { noviceQuest[q.skill] = Mathf.Min(q.need, have + n); changed = true; }
                else if (q.kind == "collect") for (int i = 0; i < n && noviceQuest[q.skill] < q.need; i++) if (rng.NextDouble() < NoviceQuests.CollectChance) { noviceQuest[q.skill]++; changed = true; }
            }
            return changed;
        }

        /// <summary>轉職（選了不能換）：第一個進階技能直接學會</summary>
        public bool Advance(string advId)
        {
            var a = Advanced.Get(advId);
            if (!CanAdvance || a == null || a.baseCls != cls) return false;
            adv = a.id; RecalcSkillPoints();   // 進階職業的技能點從 31 級開始拿（基本職業剩下的點不能挪過去）
            return true;
        }

        public const int MaxLevel = 60, StatPerLevel = 8, SkillPerLevel = 1;
        /// <summary>
        /// 職業（基本）技能樹：樹上總共 40 點；轉職前最多只能點其中 30 點（＝30 級拿到的點數），要取捨。
        /// 轉職後可以回來繼續點職業樹（最多到 40 點），也可以點進階職業的技能樹。
        /// </summary>
        /// <summary>轉職前職業技能樹最多能點的點數＝整棵樹的 1/5（使用者：要有取捨；約 15～17 點）</summary>
        public const float BaseTreeShare = 0.2f;
        public static int TreeTotal(string cls) { int t = 0; foreach (var n in SkillTrees.Of(cls)) t += n.max; return t; }
        public static int BaseTreeCapOf(string cls) => cls == Skills.Novice ? TreeTotal(cls) : Mathf.Max(1, Mathf.RoundToInt(TreeTotal(cls) * BaseTreeShare));   // 初心者的小樹沒有上限
        public int BaseTreeCap => BaseTreeCapOf(cls);
        /// <summary>技能樹版本：2＝樹狀圖＋流派（舊存檔讀進來時退回職業樹點數）</summary>
        public const int TreeVersion = 3;
        public int treeVersion = TreeVersion;   // 新角色直接是最新版；讀檔時沒有 tv＝舊版

        /// <summary>基本職業技能樹已經點了幾點</summary>
        public int BaseSpent { get { int v = 0; foreach (var n in SkillTrees.Of(cls)) v += Rank(n.id); return v; } }
        /// <summary>全部技能樹已經點了幾點</summary>
        public int SpentSkill { get { int v = 0; foreach (var n in Nodes) v += Rank(n.id); return v; } }
        /// <summary>到目前等級總共拿到的技能點</summary>
        public int EarnedSkill => level * SkillPerLevel;
        /// <summary>現在點得出去的技能點（基本樹點滿 30 點、又還沒轉職時，剩下的點要等轉職才能用）</summary>
        public int UsableSkillPoints => skillPoints;
        /// <summary>職業樹現在最多能點幾點：轉職前 30、轉職後全部</summary>
        public int BaseCapNow => adv == null ? BaseTreeCap : int.MaxValue;

        /// <summary>讀檔後校正：基本技能樹超過上限（舊規則點的）就整棵退點；剩餘點數＝拿到的－已點的</summary>
        public void NormalizeSkillPoints()
        {
            // 技能樹改版（v3：樹狀圖、最高 5 級、進階職業也改成三流派）：舊的點數全部退回
            if (treeVersion < TreeVersion) { ranks.Clear(); treeVersion = TreeVersion; }
            // 不存在的節點、超過上限的等級
            var all = new Dictionary<string, TreeNode>();
            foreach (var n in Nodes) all[n.id] = n;
            foreach (var k in new List<string>(ranks.Keys))
                if (!all.TryGetValue(k, out var nd)) ranks.Remove(k);
                else if (ranks[k] > nd.max) ranks[k] = nd.max;
            // 某一棵樹點超過這個階段拿到的點數（舊規則點的）：那棵樹退點
            foreach (var t in Trees) if (SpentIn(t) > EarnedIn(t)) foreach (var n in SkillTrees.Of(t)) ranks.Remove(n.id);
            RecalcSkillPoints();
        }

        /// <summary>升到 level+1 需要的累積經驗：75、200、375、600…</summary>
        public static int XpForLevel(int level) => 50 * (level - 1) + 25 * (level - 1) * (level - 1);

        public int Rank(string id) => ranks.TryGetValue(id, out var r) ? r : 0;

        /// <summary>加經驗，回傳升了幾級</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0 || level >= MaxLevel) return 0;
            xp += amount;
            int ups = 0;
            while (level < MaxLevel && !LevelLocked && xp >= XpForLevel(level + 1)) { level++; statPoints += StatPerLevel; ups++; }
            RecalcSkillPoints();
            if (LevelLocked) xp = Mathf.Min(xp, XpForLevel(level + 1));   // 經驗條停在滿格；轉職後下一次拿到經驗就升級
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
            return Mathf.RoundToInt(v) + Souls.StatSum(this, s);   // 精魄加的能力值也算裝備的
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

        /// <summary>重新分配：全部能力點、技能點退回（技能樹清空）。能力值不夠穿的裝備會脫下來，回傳脫下的裝備（呼叫的人要放回背包，不能丟掉）</summary>
        public List<Item> ResetPoints()
        {
            for (int i = 0; i < StatInfo.Count; i++) alloc[i] = 0;
            // 技能樹清空；轉職前的初心者技能樹不能重置（點數固定在上面）
            foreach (var k in new List<string>(ranks.Keys))
                if (!(novicePast && cls != Skills.Novice && SkillTrees.Find(Skills.Novice, k) != null)) ranks.Remove(k);
            statPoints = (level - 1) * StatPerLevel;
            RecalcSkillPoints();
            var off = new List<Item>();
            for (int i = 0; i < equipped.Length; i++) if (equipped[i] != null && !CanEquip(equipped[i], out _)) { off.Add(equipped[i]); equipped[i] = null; }
            return off;
        }

        // ---- 技能樹
        public string NodeBlock(TreeNode n)
        {
            if (Rank(n.id) >= n.max) return "已滿級";
            if (TreeOf(n) == Skills.Novice && n.skill != null && !noviceLearned.Contains(n.skill))
                return $"先到炎之里解任務學會「{(Skills.All.TryGetValue(n.skill, out var ns) ? ns.name : n.skill)}」";
            foreach (var r in SkillTrees.Prereqs(TreeOf(n), n))
            {
                if (n.free) { if (Rank(r.id) < 1) return $"需要先點「{r.name}」"; }
                else if (Rank(r.id) < r.max) return $"需要先點滿「{r.name}」";
            }
            var tree = TreeOf(n);
            if (AvailIn(tree) <= 0)
                return tree == Skills.Novice ? (cls == Skills.Novice ? "技能點不足" : "初心者的技能點用完了（1～10 級拿到的點只能點這裡）")
                    : tree == cls ? (EarnedIn(tree) == 0 ? $"「{TreeName(tree)}」的技能點從 {(novicePast ? Skills.JobLevel + 1 : 1)} 級開始拿（每升一級 1 點）" : $"「{TreeName(tree)}」的技能點用完了（{(novicePast ? Skills.JobLevel + 1 : 1)}～{Advanced.Level} 級拿到的點只能點這棵樹）")
                    : $"「{TreeName(tree)}」的技能點用完了（{Advanced.Level + 1} 級以後拿到的點才能點這棵樹）";
            return null;
        }
        public bool Learn(TreeNode n)
        {
            if (NodeBlock(n) != null) return false;
            ranks[n.id] = Rank(n.id) + 1; RecalcSkillPoints();
            return true;
        }

        /// <summary>會的技能：初始 3 個＋技能樹習得的</summary>
        public List<string> KnownSkills()
        {
            var list = new List<string>(SkillTrees.Starters(cls));
            if (cls == Skills.Novice) list.RemoveAll(id => !noviceLearned.Contains(id));   // 初心者：只會在炎之里解任務學到的招式
            else if (novicePast) foreach (var id in noviceLearned) if (Skills.All.ContainsKey(id) && !list.Contains(id)) list.Add(id);   // 轉職後：初心者學會的招式還會用
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
                if (n.id == SkillTrees.AidNode) { var (h, c) = SkillTrees.AidAt(r); s.heal = h; s.cost = c; continue; }
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
                    case ModField.Pierce: s.pierce = true; break;
                }
            }
            // 法師「過載」：法師技能傷害 +15%／級，MP 消耗 +20%／級
            float ov = Passive(PassiveKind.Overload);
            if (ov > 0f && s.cls == cls && s.damage > 0) { s.damage = Mathf.RoundToInt(s.damage * (1f + 0.15f * ov)); s.cost = Mathf.CeilToInt(s.cost * (1f + 0.2f * ov)); }
            if (s.heal > 0) s.heal = Mathf.RoundToInt(s.heal * heal);   // 智力：治療量
            if (id == "nv_aid") s.heal = Mathf.Min(s.heal, SkillTrees.AidMaxHeal);   // 急救最多回復 30 HP（加了智力也一樣）
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
            // MP：智力每點 +2、裝備；MP 回復：基礎 30%，精神加到 99 點才回到 100%（線性；裝備把精神加超過 99 會再更快）
            f.maxMp = Mathf.Max(10, cd.maxMp + 2 * d(Stat.Int) + Mathf.RoundToInt(GearSum(Affix.Mp)));
            float spiFrac = Mathf.Max(0f, d(Stat.Spi) / (float)Mathf.Max(1, Fighter.MpRegenFullSpi - BaseStat(Stat.Spi)));
            f.regen = cd.mpRegen * (Fighter.MpRegenBase + (1f - Fighter.MpRegenBase) * spiFrac);
            // 冷卻：精神每點 -0.8%（最多 -35%）、裝備
            f.cdMul = Mathf.Clamp(1f - 0.008f * d(Stat.Spi) - GearSum(Affix.Cdr), 0.5f, 1.5f);
            // 蓄力：敏捷每點 -1%（最多 -40%）；側跨沒有無敵時間了 → 敏捷與「側跨」類特殊能力改成閃避率（下面 evadeChance）
            f.chargeScale = Mathf.Clamp(1f - 0.01f * d(Stat.Agi), 0.6f, 1.5f);
            // 普攻速度：敏捷每點冷卻 -0.8%（最多 -50%）
            f.basicCdMul = Mathf.Clamp(1f - 0.008f * d(Stat.Agi), 0.5f, 1.5f);
            f.dodgeBonus = 0f;
            // 幸運：爆擊率；技能樹與裝備的特殊能力
            f.critChance = Mathf.Clamp(0.006f * Total(Stat.Luk) + Passive(PassiveKind.Crit) + GearSum(Affix.Crit), 0f, 0.75f);
            f.lifesteal = Passive(PassiveKind.Lifesteal) + GearSum(Affix.Lifesteal);
            f.mpOnHit = Passive(PassiveKind.MpOnHit);
            f.hpRegen = Passive(PassiveKind.Regen);
            f.lastStand = Passive(PassiveKind.LastStand);
            f.burnChance = Passive(PassiveKind.Burn) + GearSum(Affix.Burn);
            f.poisonChance = Passive(PassiveKind.Poison) + GearSum(Affix.Poison);
            f.freezeChance = Passive(PassiveKind.Freeze) + GearSum(Affix.Freeze);
            f.evadeChance = Mathf.Clamp(Passive(PassiveKind.Evade) + SkillTrees.DodgeToEvade * Passive(PassiveKind.Dodge) + 0.001f * Mathf.Max(0, d(Stat.Agi)), 0f, 0.5f);
            f.chargeScale = Mathf.Max(0.4f, f.chargeScale * (1f - Passive(PassiveKind.FastCast)));   // 法師：快速詠唱
            f.overcharge = Passive(PassiveKind.Overcharge) > 0f;                                   // 獵人：蓄力強化
            f.hpRegen += f.maxHp * Passive(PassiveKind.NaturalRegen) / 10f;                         // 劍士：自然回復（每 10 秒 x%）
            f.cdMul = Mathf.Max(0.4f, f.cdMul * (1f - Passive(PassiveKind.QuickCd)));               // 牧師：快速施法
            f.rangeMul = 1f;
            // 進階技能樹的特殊能力：攻擊、減傷、HP
            f.atkMul *= 1f + Passive(PassiveKind.Atk);
            f.gearReduce = Mathf.Min(0.6f, f.gearReduce + Passive(PassiveKind.Armor));
            f.maxHp += Mathf.RoundToInt(Passive(PassiveKind.Hp));
            // 技能樹 v2 的流派效果
            f.spellEcho = Passive(PassiveKind.SpellEcho); f.basicMp = Passive(PassiveKind.BasicMp); f.lowMpBonus = Passive(PassiveKind.LowMp);
            f.melt = Passive(PassiveKind.Melt); f.iceFreeze = Passive(PassiveKind.IceFreeze);
            f.longShot = Passive(PassiveKind.LongShot); f.steadyAim = Passive(PassiveKind.SteadyAim); f.trapCrit = Passive(PassiveKind.TrapCrit);
            f.arrowBleed = Passive(PassiveKind.ArrowBleed); f.arrowPoison = Passive(PassiveKind.ArrowPoison); f.arrowSilence = Passive(PassiveKind.ArrowSilence);
            f.comboStrike = Passive(PassiveKind.ComboStrike); f.basicLeech = Passive(PassiveKind.BasicLeech); f.waveFlow = Passive(PassiveKind.WaveFlow); f.riposte = Passive(PassiveKind.Riposte); f.orbSlice = Passive(PassiveKind.OrbSlice);
            f.venom = Passive(PassiveKind.Venom); f.assassinate = Passive(PassiveKind.Assassinate); f.vanish = Passive(PassiveKind.Vanish); f.knifeCdr = Passive(PassiveKind.KnifeCdr);
            f.holySeal = Passive(PassiveKind.HolySeal); f.basicHoming = Passive(PassiveKind.Homing); f.martyr = Passive(PassiveKind.Martyr); f.zeal = Passive(PassiveKind.Zeal);
            f.bashGuard = Passive(PassiveKind.BashGuard); f.sanctionCombo = Passive(PassiveKind.SanctionCombo); f.critDmg = Passive(PassiveKind.CritDmg);
            f.falconMark = Passive(PassiveKind.FalconMark); f.petMp = Passive(PassiveKind.PetMp); f.closeInstinct = Passive(PassiveKind.CloseInstinct);
            var bs = Skills.BasicOf(cls); f.basic = bs != null ? ModdedSkill(bs.id) : null;   // 普攻也吃技能樹
            // 技能：只帶會的技能，數值換成技能樹強化後的版本
            f.loadout.Clear();
            var known = KnownSkills();
            foreach (var id in loadoutIds) if (known.Contains(id)) { var s = ModdedSkill(id); if (s != null) f.loadout.Add(s); }
            if (f.loadout.Count == 0) foreach (var id in known) { if (f.loadout.Count >= 3) break; f.loadout.Add(ModdedSkill(id)); }
            f.hp = Mathf.Max(1f, f.maxHp * hpRatio); f.mp = f.maxMp * mpRatio;
            Advanced.ApplyStats(f, Adv);   // 進階職業：HP、MP、攻擊、減傷、爆擊、閃避、治療量
            Souls.ApplyTo(f, this);        // 裝備上的精魄
        }

        // ---- 存檔（一行一個欄位）
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("level=").Append(level).Append('\n');
            sb.Append("xp=").Append(xp).Append('\n');
            sb.Append("sp=").Append(statPoints).Append('\n');
            sb.Append("kp=").Append(skillPoints).Append('\n');
            sb.Append("tv=").Append(TreeVersion).Append('\n');
            sb.Append("alloc=").Append(string.Join(",", alloc)).Append('\n');
            if (!string.IsNullOrEmpty(adv)) sb.Append("adv=").Append(adv).Append('\n');
            if (trialPassed) sb.Append("trial=1\n");
            if (jobTrial > 0) sb.Append("jt=").Append(jobTrial).Append('\n');
            if (novicePast) sb.Append("np=1\n");
            if (souls.Count > 0) sb.Append("souls=").Append(Souls.SerializeBag(souls)).Append('\n');
            foreach (var id in noviceLearned) sb.Append("nl=").Append(id).Append('\n');
            foreach (var kv in noviceQuest) sb.Append("nq=").Append(kv.Key).Append(':').Append(kv.Value).Append('\n');
            foreach (var kv in ranks) sb.Append("rank=").Append(kv.Key).Append(':').Append(kv.Value).Append('\n');
            for (int i = 0; i < equipped.Length; i++) if (equipped[i] != null) sb.Append("eq").Append(i).Append('=').Append(equipped[i].Serialize()).Append('\n');
            return sb.ToString();
        }

        public static CharacterProfile Parse(string cls, string text)
        {
            var p = new CharacterProfile { cls = cls, treeVersion = string.IsNullOrEmpty(text) ? TreeVersion : 1 };
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
                        case "tv": p.treeVersion = int.Parse(v); break;
                        case "alloc": { var a = v.Split(','); for (int i = 0; i < a.Length && i < p.alloc.Length; i++) p.alloc[i] = int.Parse(a[i]); break; }
                        case "rank": { var a = v.Split(':'); if (a.Length == 2) p.ranks[a[0]] = int.Parse(a[1]); break; }
                        case "adv": if (Advanced.Get(v) != null && Advanced.Get(v).baseCls == cls) p.adv = v; break;
                        case "trial": p.trialPassed = v == "1"; break;
                        case "jt": p.jobTrial = Mathf.Clamp(int.Parse(v), 0, Skills.JobTrialRounds); break;
                        case "nl": p.noviceLearned.Add(v); break;
                        case "np": p.novicePast = v == "1"; break;
                        case "souls": Souls.ParseBag(v, p.souls); break;
                        case "nq": { var a = v.Split(':'); if (a.Length == 2) p.noviceQuest[a[0]] = int.Parse(a[1]); break; }
                        default:
                            if (k.StartsWith("eq") && int.TryParse(k.Substring(2), out var slot) && slot >= 0 && slot < p.equipped.Length) p.equipped[slot] = Item.Parse(v);
                            break;
                    }
                }
                catch (Exception) { }
            }
            p.NormalizeSkillPoints();
            return p;
        }
    }

    /// <summary>
    /// 初心者的招式任務（炎之里的居民教）：接任務 → 完成 → 回去找他 → 學會那一招。
    ///   小鐵 教 木棍揮擊（打倒 5 隻小怪）；米菈 教 投石（炎之里第一個任務：上投石課，故事 1-1 之前一定要上）；老焰 教 急救（通關故事 1-1）。
    /// 狀態存在角色上（CharacterProfile.noviceQuest／noviceLearned）。
    /// </summary>
    public static class NoviceQuests
    {
        public class Quest
        {
            public string skill, npc, title, kind, item, stage; public int need;
            public int xp;                // 完成時給的經驗（讓初心者做完教學與任務時剛好到 10 級）
            public string[] offer, wait, done;
        }
        public const double CollectChance = 0.35;
        public static readonly Quest[] All =
        {
            new Quest { skill = "nv_swing", npc = "小鐵", title = "小鐵的試刀", kind = "kill", need = 5, xp = 250,
                offer = new[] { "你就是新來的冒險者？拿根木棍就想出門，太危險了！", "這樣吧：去外面打倒 5 隻小怪，回來我教你怎麼把木棍揮得像樣一點。" },
                wait = new[] { "還差一點！打倒 5 隻小怪再回來。" },
                done = new[] { "不錯嘛！來，木棍要這樣揮——用手刀往下劈，力氣從腰帶過來。", "學會「木棍揮擊」了！手刀劈下去就會出招。" } },
            // 投石課（kind "lesson"）：接任務就先學會投石，再上一堂練習（木頭人站在遠處）；練習過了任務才算完成，故事 1-1 才會開
            new Quest { skill = "nv_stone", npc = "米菈", title = "米菈的投石課", kind = "lesson", need = 1, xp = 150,
                offer = new[] { "等等，新來的！你只會拿木棍近身敲？碰到站得遠遠的、會丟火球的怪怎麼辦？", "我跑商路全靠這招：撿顆石頭，丟得又遠又準。來，我教你。", "先唸「投石」把力氣集中在手上，然後握拳，再一口氣張開手——石頭就飛出去了。", "去空地上試試：木頭人站在遠處，用投石丟中它 3 次！" },
                wait = new[] { "投石課還沒上完喔。再去空地練一次：站遠遠的，用投石丟中木頭人 3 次。" },
                done = new[] { "漂亮！離得遠也打得到，這就是投石。", "記住：近身用木棍敲（手掌劃一下），遠的就唸「投石」、握拳再張開。", "準備好了就去村口，走一趟故事 1-1「灰燼中的歸途」吧。小鐵和老焰也有事要拜託你喔。" } },
            new Quest { skill = "nv_aid", npc = "老焰", title = "老焰的第一課", kind = "clear", stage = "1-1", need = 1, xp = 250,
                offer = new[] { "孩子，出門前先學會照顧自己。", "先去走一趟「灰燼中的歸途」（故事 1-1），平安回來，我就教你包紮傷口。" },
                wait = new[] { "還沒走完「灰燼中的歸途」嗎？不急，平安最重要。" },
                done = new[] { "你回來了。來，傷口要先壓住，再纏緊——手掌收回自己身上就對了。", "學會「急救」了！手掌往自己收回來就會回血。" } },
        };
        public static Quest ByNpc(string npcName) => Array.Find(All, q => !string.IsNullOrEmpty(npcName) && npcName.Contains(q.npc));
        /// <summary>任務有沒有完成（打倒／收集看進度；通關看故事紀錄）</summary>
        public static bool Complete(Quest q, CharacterProfile p, Func<string, bool> stageCleared)
            => q.kind == "lesson" ? p.noviceLearned.Contains(q.skill) && !p.noviceQuest.ContainsKey(q.skill)
             : p.noviceQuest.TryGetValue(q.skill, out var have) && (q.kind == "clear" ? stageCleared != null && stageCleared(q.stage) : have >= q.need);
        /// <summary>這個任務還在進行（上課型：學會了但練習還沒過也算）</summary>
        public static bool Pending(Quest q, CharacterProfile p) => p.cls == Skills.Novice && !(p.noviceLearned.Contains(q.skill) && !p.noviceQuest.ContainsKey(q.skill));
        /// <summary>初心者還沒上完米菈的投石課（故事 1-1 鎖住）</summary>
        public static bool StoneLessonPending(CharacterProfile p) { var q = Array.Find(All, x => x.kind == "lesson"); return q != null && Pending(q, p); }
        public static string Progress(Quest q, CharacterProfile p, Func<string, bool> stageCleared)
        {
            if (p.noviceLearned.Contains(q.skill) && !p.noviceQuest.ContainsKey(q.skill)) return "✔ 已學會";
            if (!p.noviceQuest.TryGetValue(q.skill, out var have)) return "還沒接（到炎之里找" + q.npc + "）";
            if (q.kind == "lesson") return "到炎之里找" + q.npc + "，上完投石課";
            if (q.kind == "clear") return stageCleared != null && stageCleared(q.stage) ? "完成！回去找" + q.npc : $"通關故事 {q.stage}";
            return have >= q.need ? "完成！回去找" + q.npc : q.kind == "kill" ? $"打倒小怪 {have}/{q.need}" : $"{q.item} {have}/{q.need}";
        }
    }

    /// <summary>存取：角色（每個職業一份）、背包（全部職業共用）。實際讀寫交給 Get/Set（遊戲裡是 PlayerPrefs）</summary>
    public static class Progress
    {
        public static Func<string, string> Get = k => "";
        public static Action<string, string> Set = (k, v) => { };
        /// <summary>三個角色共用的存檔（倉庫）：不加角色欄位的前綴</summary>
        public static Func<string, string> GetShared = k => Get(k);
        public static Action<string, string> SetShared = (k, v) => Set(k, v);
        /// <summary>存檔接好了沒（SoloBattle 把 Get／Set 換成手機的 PlayerPrefs 之後才是 true）</summary>
        public static bool Ready { get; private set; }
        /// <summary>存檔接好：清掉接好之前讀到的（空白的）角色、背包、信箱，之後重新從手機讀</summary>
        public static void MarkReady() { Ready = true; ReloadStorage(); }
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

        // ---- 背包容量與信箱：背包滿了，新拿到的裝備寄到信箱（保留 7 天，過期自動刪除）
        public static bool BagFull => Bag.Count >= BagSize;
        public static int BagFree => Mathf.Max(0, BagSize - Bag.Count);
        public const int MailKeepDays = 7;
        /// <summary>現在時間（Unix 秒；測試時可以換掉）</summary>
        public static Func<long> Now = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public class Mail { public Item item; public long sentAt; public string from; }
        static List<Mail> mail;
        /// <summary>信箱（讀的時候順便刪掉過期的信）</summary>
        public static List<Mail> Mailbox
        {
            get
            {
                if (mail == null)
                {
                    mail = new List<Mail>();
                    foreach (var line in (Get("sd_mail") ?? "").Split('\n'))
                    {
                        var a = line.Split(new[] { '\t' }, 3);
                        if (a.Length < 3 || !long.TryParse(a[0], out var t)) continue;
                        var it = Item.Parse(a[2]);
                        if (it != null) mail.Add(new Mail { sentAt = t, from = a[1], item = it });
                    }
                }
                if (mail.RemoveAll(Expired) > 0) SaveMail();
                return mail;
            }
        }
        public static bool Expired(Mail m) => Now() - m.sentAt >= MailKeepDays * 86400L;
        /// <summary>還剩幾天（不到 1 天＝0，畫面寫「今天到期」）</summary>
        public static int MailDaysLeft(Mail m) => (int)Math.Max(0L, (m.sentAt + MailKeepDays * 86400L - Now()) / 86400L);
        public static void SaveMail()
        {
            var sb = new StringBuilder();
            foreach (var m in mail ?? new List<Mail>()) sb.Append(m.sentAt).Append('\t').Append((m.from ?? "").Replace('\t', ' ').Replace('\n', ' ')).Append('\t').Append(m.item.Serialize()).Append('\n');
            Set("sd_mail", sb.ToString());
        }
        public static void SendMail(Item it, string from)
        {
            if (it == null) return;
            Mailbox.Add(new Mail { item = it, sentAt = Now(), from = from ?? "" });
            SaveMail();
        }
        /// <summary>放進背包；背包滿了就寄到信箱。回傳 true＝進背包、false＝寄到信箱</summary>
        public static bool AddToBagOrMail(Item it, string from)
        {
            if (AddToBag(it)) return true;
            SendMail(it, from);
            return false;
        }
        /// <summary>從信箱領到背包（背包滿了回傳 false）</summary>
        public static bool ClaimMail(Mail m)
        {
            if (m == null || BagFull || !Mailbox.Contains(m)) return false;
            Bag.Add(m.item); mail.Remove(m);
            SaveBag(); SaveMail();
            return true;
        }
        /// <summary>
        /// 初心者轉職（10 級，在職業工會選）：等級、經驗、加過的能力點、身上的裝備都帶過去，職業換成 cls；
        /// 新職業穿不了的裝備放回背包（滿了寄信箱）。初心者的存檔刪掉。回傳新職業的角色（不能轉職＝null）
        /// </summary>
        public static CharacterProfile ChangeJob(string cls)
        {
            var old = Char(Skills.Novice);
            if (!old.CanChangeJob || cls == Skills.Novice || !Skills.Classes.ContainsKey(cls) || Array.IndexOf(Skills.ClassOrder, cls) < 0) return null;
            var np = CharacterProfile.Parse(cls, old.Serialize());
            np.novicePast = true;
            foreach (var kv in old.ranks) if (SkillTrees.Find(Skills.Novice, kv.Key) != null) np.ranks[kv.Key] = kv.Value;   // 讀新職業時初心者節點被當成不存在的清掉了：照抄回來
            np.NormalizeSkillPoints();   // 初心者的技能樹保留（點數固定在上面）；新職業的技能點從 11 級開始拿
            for (int i = 0; i < np.equipped.Length; i++)
                if (np.equipped[i] != null && !np.CanEquip(np.equipped[i], out _)) { AddToBagOrMail(np.equipped[i], "轉職時脫下"); np.equipped[i] = null; }
            cache[cls] = np; SaveChar(np);
            cache.Remove(Skills.Novice); Set("sd_char_" + Skills.Novice, "");
            return np;
        }

        /// <summary>測試用：清掉記憶體裡的背包與信箱（重新從存檔讀）</summary>
        public static void ReloadStorage() { bag = null; mail = null; stash = null; cache.Clear(); }

        // ---- 倉庫：三個角色共用（存進去的裝備，任何一個角色都能拿出來）
        public const int StashSize = 60;
        static List<Item> stash;
        public static List<Item> Stash
        {
            get
            {
                if (stash != null) return stash;
                stash = new List<Item>();
                foreach (var line in (GetShared("sd_stash") ?? "").Split('\n')) { var it = Item.Parse(line); if (it != null) stash.Add(it); }
                return stash;
            }
        }
        static void SaveStash()
        {
            var sb = new StringBuilder();
            foreach (var it in Stash) sb.Append(it.Serialize()).Append('\n');
            SetShared("sd_stash", sb.ToString());
        }
        /// <summary>背包 → 倉庫（倉庫滿了回傳 false）</summary>
        public static bool Deposit(Item it)
        {
            if (it == null || Stash.Count >= StashSize || !Bag.Contains(it)) return false;
            Bag.Remove(it); Stash.Add(it);
            SaveStash(); SaveBag();   // 先存倉庫再存背包：中途斷掉最多變成兩邊都有，不會兩邊都沒有
            return true;
        }
        /// <summary>倉庫 → 背包（背包滿了回傳 false）</summary>
        public static bool Withdraw(Item it)
        {
            if (it == null || BagFull || !Stash.Contains(it)) return false;
            Bag.Add(it); SaveBag();
            Stash.Remove(it); SaveStash();
            return true;
        }
    }
}
