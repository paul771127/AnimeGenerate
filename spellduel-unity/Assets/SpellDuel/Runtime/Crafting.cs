using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 材料、精煉石、特殊藥劑、特殊裝備（使用者要求）。
    ///   ・材料放在道具欄（Economy.Items，id 開頭 mat_）。敵人倒下時：精魄 → 裝備 → 材料，最多只掉一樣（SoloBattle.DropOne）。
    ///   ・岩石／大地類敵人 10% 掉鐵礦石；普通以上難度另有機率掉高級鐵礦石。5 個鐵礦石 → 精煉石（強化 +1～+5 每次 1 個）；
    ///     5 個高級鐵礦石 → 高級精煉石（+6～+8 每次 1 個；+9、+10 每次 2 個）。
    ///   ・每個城鎮的藥劑師用當地敵人掉的材料做強化藥劑（每罐 3 分鐘）；材料一樣 10% 從對應的敵人身上掉。
    ///   ・工匠做特殊裝備：固定的特殊效果（數值有點隨機）、物品等級＝自己的等級（數值 ±10%）；等級越高要的材料越多；
    ///     強的配方要普通／困難才掉的材料，最強的要 Boss 才掉的核心。
    /// </summary>
    public static class Crafting
    {
        // ================================================================ 材料
        public class Mat { public string id, name, icon, from; }
        public static readonly List<Mat> Mats = new List<Mat>
        {
            M("ore", "鐵礦石", "🪨", "岩石、大地類的敵人（10%）"),
            M("ore_hi", "高級鐵礦石", "💎", "普通以上難度的岩石、大地類敵人"),
            M("refine", "精煉石", "🔷", "鐵匠：5 個鐵礦石合成"),
            M("refine_hi", "高級精煉石", "🔶", "鐵匠：5 個高級鐵礦石合成"),
            // 第一章：熔岩
            M("fire", "火焰精華", "🔥", "火焰射手、熔岩狙擊手"),
            M("horn", "小鬼角", "😈", "熔岩小鬼、自爆小鬼"),
            M("void", "虛空碎片", "🌀", "虛空術士"),
            // 第二章：森林
            M("gland", "毒腺", "🧪", "毒液噴花"),
            M("fiber", "藤蔓纖維", "🌿", "荊棘衝鋒獸、藤蔓術士"),
            M("cap", "菇傘", "🍄", "孢子菇"),
            M("fang", "食人花牙", "🦷", "食人花"),
            M("seed", "魔種", "🌰", "種子槍手"),
            // 第三章：海港
            M("scale", "魚鱗", "🐟", "跳躍魚"),
            M("feather", "海鳥羽毛", "🪶", "俯衝海鳥"),
            M("gel", "泡泡膠", "🫧", "騎魚泡泡怪"),
            M("shell", "蟹殼", "🦀", "寄居蟹"),
            M("tendril", "水母觸鬚", "🪼", "雷電水母"),
            M("spine", "河豚刺", "🐡", "河豚"),
            // 第四章：角鬥場、第五章：峽谷
            M("badge", "鬥技徽章", "🏅", "角鬥場的敵人、鏈錘鬥奴"),
            M("powder", "炸藥粉", "💥", "自爆小鬼"),
            M("crystal", "砂晶", "🔸", "峽谷的敵人、裂岩甲蜥"),
            // 菁英、Boss
            M("elite", "菁英徽記", "🎖", "菁英怪（普通以上難度）"),
            M("core_golem", "熔岩之心", "❤‍🔥", "Boss 熔岩巨像"),
            M("core_tree", "樹王之種", "🌱", "Boss 腐化樹王"),
            M("core_sea", "海妖之淚", "💧", "Boss 深海巨妖"),
            M("core_arena", "鬥魂徽記", "⚜", "Boss 青銅鬥魂"),
            M("core_canyon", "岩魔之核", "🟤", "Boss 黃土岩魔"),
            M("tame_seal", "馴獸印片", "🔰", "各章菁英怪 20%（8 枚＋金幣調配菁英誘餌）"),
            M("soul_smith", "神匠之魂", "✨", "困難難度的 Boss（很少）"),
        };
        static Mat M(string id, string name, string icon, string from) => new Mat { id = "mat_" + id, name = name, icon = icon, from = from };
        public static Mat GetMat(string id) => Mats.Find(m => m.id == id || m.id == "mat_" + id);
        public static string MatName(string id) { var m = GetMat(id); return m != null ? m.icon + m.name : id; }
        public static int Have(string id) => Economy.Count(id.StartsWith("mat_") ? id : "mat_" + id);
        public static void Give(string id, int n) => Economy.AddItem(id.StartsWith("mat_") ? id : "mat_" + id, n);

        public const float MatChance = 0.10f, HiOreChance = 0.05f, EliteMatChance = 0.25f, BossCoreChance = 0.25f, SmithSoulChance = 0.05f;
        /// <summary>岩石、大地類的敵人（掉鐵礦石）：岩石衝鋒獸、岩石投擲者、古根撼地獸、裂岩甲蜥、晶甲守隘獸、砂晶砲蜥、石巢牧者</summary>
        public static bool IsRocky(int kind) => kind == 2 || kind == 4 || kind == 19 || kind == 22 || kind == 43 || kind == 44 || kind == 46;

        /// <summary>這種小怪可能掉的材料（不含鐵礦石）</summary>
        static string[] KindMats(int kind, int theme)
        {
            var l = new List<string>();
            switch (kind)
            {
                case 0: l.Add("horn"); break;
                case 1: case 5: l.Add("fire"); break;
                case 3: l.Add("horn"); l.Add("powder"); break;
                case 6: l.Add("void"); break;
                case 7: l.Add("gland"); break;
                case 8: case 9: l.Add("fiber"); break;
                case 10: l.Add("fang"); break;
                case 11: l.Add("cap"); break;
                case 12: l.Add("seed"); break;
                case 13: l.Add("scale"); break;
                case 14: l.Add("feather"); break;
                case 15: l.Add("gel"); break;
                case 16: l.Add("shell"); break;
                case 17: l.Add("tendril"); break;
                case 18: l.Add("spine"); break;
                case 21: l.Add("badge"); break;
                case 22: l.Add("crystal"); break;
            }
            // 地區：角鬥場的敵人都可能掉鬥技徽章；峽谷的敵人都可能掉砂晶
            if (theme == 3 && !l.Contains("badge")) l.Add("badge");
            if (theme == 4 && !l.Contains("crystal")) l.Add("crystal");
            // 菁英怪：所屬章節的材料
            if (EliteCatalog.IsElite(kind))
            {
                var d = EliteCatalog.Get(kind);
                l.Add(d.chapter == 0 ? "fire" : d.chapter == 1 ? "fiber" : d.chapter == 2 ? "shell" : d.chapter == 3 ? "badge" : "crystal");
            }
            return l.ToArray();
        }

        /// <summary>小怪倒下：擲材料（沒掉＝null）。difficulty：0 簡單、1 普通、2 困難</summary>
        public static string RollMinion(int kind, int theme, int difficulty, System.Random rng)
        {
            if (EliteCatalog.IsElite(kind) && difficulty >= 1 && rng.NextDouble() < EliteMatChance) return "mat_elite";   // 菁英徽記：普通以上
            bool rocky = IsRocky(kind);
            if (rocky && difficulty >= 1 && rng.NextDouble() < HiOreChance) return "mat_ore_hi";
            if (rng.NextDouble() >= MatChance) return null;
            var list = new List<string>(KindMats(kind, theme));
            if (rocky) list.Add("ore");
            if (list.Count == 0) return null;
            return "mat_" + list[rng.Next(list.Count)];
        }

        /// <summary>Boss 倒下：擲 Boss 材料（沒掉＝null，照常掉裝備）</summary>
        public static string RollBoss(string bossCls, int difficulty, System.Random rng)
        {
            if (difficulty >= 2 && rng.NextDouble() < SmithSoulChance) return "mat_soul_smith";
            if (rng.NextDouble() >= BossCoreChance) return null;
            return bossCls switch
            {
                "boss" => "mat_core_golem",
                "boss_forest" => "mat_core_tree",
                "boss_sea" => "mat_core_sea",
                "boss_arena" => "mat_core_arena",
                "boss_canyon" => "mat_core_canyon",
                _ => null,
            };
        }

        // ================================================================ 精煉石（鐵匠）
        public const int OrePerStone = 5;
        /// <summary>強化到下一級要的精煉石（id、數量）：+1～+5 精煉石 1 個；+6～+8 高級精煉石 1 個；+9、+10 高級精煉石 2 個</summary>
        public static (string id, int n) StoneFor(Item it)
        {
            int next = it.plus + 1;
            return next <= 5 ? ("mat_refine", 1) : next <= 8 ? ("mat_refine_hi", 1) : ("mat_refine_hi", 2);
        }
        public static bool RefineStone(bool hi, out string msg)
        {
            string ore = hi ? "mat_ore_hi" : "mat_ore", stone = hi ? "mat_refine_hi" : "mat_refine";
            if (Have(ore) < OrePerStone) { msg = $"{MatName(ore)}不夠（要 {OrePerStone} 個，有 {Have(ore)} 個）"; return false; }
            Give(ore, -OrePerStone); Give(stone, 1);
            msg = $"合成了 {MatName(stone)}！（剩 {MatName(ore)} {Have(ore)} 個）";
            return true;
        }

        // ================================================================ 強化藥劑（每個城鎮的藥劑師）
        public enum BuffKind { Atk, Crit, Regen, Charge, Cdr, MagicRed, Lifesteal, PhysRed, MaxHp, Evade }
        public class Potion
        {
            public string id, name, icon, desc; public int town; public BuffKind kind; public float amount;
            public (string mat, int n)[] need; public int gold;
        }
        public const float PotionTime = 180f;   // 每罐 3 分鐘
        public static readonly List<Potion> Potions = new List<Potion>
        {
            P("bp_str", "力量藥劑", "💪", 0, BuffKind.Atk, 0.12f, "攻擊傷害 +12%", 60, ("fire", 3), ("horn", 2)),
            P("bp_eye", "鷹眼藥劑", "🎯", 0, BuffKind.Crit, 0.10f, "爆擊率 +10%", 60, ("horn", 3), ("void", 2)),
            P("bp_regen", "再生藥劑", "🌿", 1, BuffKind.Regen, 0.01f, "每秒回復最大 HP 的 1%", 80, ("fiber", 3), ("cap", 2)),
            P("bp_swift", "迅捷藥劑", "⚡", 1, BuffKind.Charge, 0.12f, "蓄力時間 -12%", 80, ("seed", 3), ("fang", 2)),
            P("bp_wise", "智慧藥劑", "📘", 2, BuffKind.Cdr, 0.12f, "技能冷卻 -12%", 100, ("tendril", 3), ("gel", 2)),
            P("bp_ward", "抗魔藥劑", "🔮", 2, BuffKind.MagicRed, 0.10f, "魔法減傷 +10%", 100, ("scale", 3), ("spine", 2)),
            P("bp_blood", "嗜血藥劑", "🩸", 3, BuffKind.Lifesteal, 0.05f, "吸血 +5%", 120, ("badge", 3), ("gland", 2)),
            P("bp_iron", "鐵壁藥劑", "🛡", 3, BuffKind.PhysRed, 0.10f, "物理減傷 +10%", 120, ("badge", 2), ("shell", 3)),
            P("bp_vigor", "活力藥劑", "❤", 4, BuffKind.MaxHp, 0.15f, "最大 HP +15%", 150, ("crystal", 3), ("ore", 2)),
            P("bp_wind", "疾風藥劑", "🌪", 4, BuffKind.Evade, 0.08f, "閃避率 +8%", 150, ("crystal", 2), ("feather", 3)),
        };
        static Potion P(string id, string name, string icon, int town, BuffKind k, float amt, string desc, int gold, params (string, int)[] need)
            => new Potion { id = id, name = name, icon = icon, town = town, kind = k, amount = amt, desc = desc + "（3 分鐘）", gold = gold, need = need };
        public static Potion GetPotion(string id) => Potions.Find(p => p.id == id);

        public static bool CanPay((string mat, int n)[] need, int gold, out string why)
        {
            why = null;
            if (Economy.Gold < gold) { why = $"金幣不夠（要 💰 {gold}）"; return false; }
            foreach (var (m, n) in need) if (Have(m) < n) { why = $"{MatName(m)}不夠（要 {n} 個，有 {Have(m)} 個）"; return false; }
            return true;
        }
        static void Pay((string mat, int n)[] need, int gold) { Economy.Gold -= gold; foreach (var (m, n) in need) Give(m, -n); }

        public static bool BrewPotion(Potion p, out string msg)
        {
            if (!CanPay(p.need, p.gold, out msg)) return false;
            Pay(p.need, p.gold); Economy.AddItem(p.id, 1);
            msg = $"做好了 {p.icon}{p.name}！（持有 {Economy.Count(p.id)}）";
            return true;
        }

        // ================================================================ 特殊裝備（工匠）
        public class Effect { public Affix a; public float lo, hi; }
        public class Recipe
        {
            public string id, name, note; public Slot slot; public int rarity;
            public int minDifficulty;            // 材料要在這個難度以上才掉（說明用）
            public Effect[] effects;
            public (string mat, int n)[] baseNeed; public int gold;
        }
        public static readonly List<Recipe> Recipes = new List<Recipe>
        {
            // ---- 一般（簡單就有材料）：稀有
            R("rc_ember", "炎心護符", Slot.Trinket, 2, 0, "灼燒機率、攻擊", 300, new[] { E(Affix.Burn, 0.03f, 0.05f), E(Affix.Atk, 0.03f, 0.06f) }, ("fire", 5), ("horn", 5), ("ore", 3)),
            R("rc_vine", "藤編長靴", Slot.Feet, 2, 0, "中毒機率、敏捷", 300, new[] { E(Affix.Poison, 0.03f, 0.05f), E(Affix.Agi, 4f, 8f) }, ("fiber", 5), ("gland", 3), ("ore", 3)),
            R("rc_tide", "潮紋法袍", Slot.Body, 2, 0, "魔法減傷、MP", 350, new[] { E(Affix.MagicArmor, 0.02f, 0.03f), E(Affix.Mp, 20f, 40f) }, ("shell", 5), ("scale", 5), ("ore", 5)),
            R("rc_shell", "蟹殼重甲", Slot.Body, 2, 0, "物理減傷、HP", 350, new[] { E(Affix.Armor, 0.02f, 0.03f), E(Affix.Hp, 40f, 80f) }, ("shell", 6), ("ore", 6)),
            // ---- 進階（普通以上的材料：高級鐵礦石、菁英徽記）：史詩
            R("rc_glad", "鬥魂戰刃", Slot.Weapon, 3, 1, "攻擊、吸血", 900, new[] { E(Affix.Atk, 0.06f, 0.10f), E(Affix.Lifesteal, 0.02f, 0.04f) }, ("badge", 8), ("elite", 3), ("ore_hi", 5)),
            R("rc_hawk", "鷹眼指環", Slot.Trinket, 3, 1, "爆擊率、冷卻", 900, new[] { E(Affix.Crit, 0.05f, 0.08f), E(Affix.Cdr, 0.03f, 0.05f) }, ("feather", 8), ("elite", 3), ("ore_hi", 3)),
            R("rc_sand", "砂晶頭盔", Slot.Head, 3, 1, "物理與魔法減傷", 900, new[] { E(Affix.Armor, 0.02f, 0.03f), E(Affix.MagicArmor, 0.02f, 0.03f) }, ("crystal", 8), ("elite", 3), ("ore_hi", 5)),
            // ---- 傳奇（Boss 核心；困難的神匠之魂）：傳說
            R("rc_lava", "熔岩之心鎧", Slot.Body, 4, 1, "雙減傷、HP、灼燒", 2500, new[] { E(Affix.Armor, 0.025f, 0.03f), E(Affix.MagicArmor, 0.025f, 0.03f), E(Affix.Hp, 90f, 150f), E(Affix.Burn, 0.04f, 0.05f) }, ("core_golem", 2), ("core_canyon", 1), ("elite", 5), ("ore_hi", 10)),
            R("rc_tear", "海妖淚墜", Slot.Trinket, 4, 1, "冷卻、MP、冰凍", 2500, new[] { E(Affix.Cdr, 0.06f, 0.10f), E(Affix.Mp, 50f, 90f), E(Affix.Freeze, 0.04f, 0.05f) }, ("core_sea", 2), ("core_tree", 1), ("elite", 5), ("ore_hi", 8)),
            R("rc_god", "神匠之刃", Slot.Weapon, 4, 2, "攻擊、爆擊、閃避後增傷", 5000, new[] { E(Affix.Atk, 0.10f, 0.15f), E(Affix.Crit, 0.06f, 0.10f), E(Affix.DodgePower, 0.08f, 0.12f) }, ("soul_smith", 1), ("core_arena", 2), ("elite", 8), ("ore_hi", 12)),
        };
        static Effect E(Affix a, float lo, float hi) => new Effect { a = a, lo = lo, hi = hi };
        static Recipe R(string id, string name, Slot slot, int rarity, int minDiff, string note, int gold, Effect[] fx, params (string, int)[] need)
            => new Recipe { id = id, name = name, slot = slot, rarity = rarity, minDifficulty = minDiff, note = note, gold = gold, effects = fx, baseNeed = need };

        /// <summary>材料數量：等級越高要越多（每 10 級 +50%）</summary>
        public static (string mat, int n)[] NeedFor(Recipe r, int level)
        {
            float k = 1f + 0.05f * Mathf.Max(0, level - 1);
            var l = new (string, int)[r.baseNeed.Length];
            for (int i = 0; i < l.Length; i++) { var (m, n) = r.baseNeed[i]; bool rare = m.StartsWith("core") || m == "soul_smith"; l[i] = (m, rare ? n : Mathf.CeilToInt(n * k)); }
            return l;
        }
        public static int GoldFor(Recipe r, int level) => Mathf.RoundToInt(r.gold * (1f + 0.05f * Mathf.Max(0, level - 1)));

        /// <summary>做出裝備：物品等級＝角色等級；主屬性照一般裝備公式 ×0.9～1.1；特殊效果在範圍內隨機</summary>
        public static Item Craft(Recipe r, CharacterProfile p, System.Random rng, out string msg)
        {
            var need = NeedFor(r, p.level); int gold = GoldFor(r, p.level);
            if (!CanPay(need, gold, out msg)) return null;
            Pay(need, gold);
            float roll() => 0.9f + 0.2f * (float)rng.NextDouble();
            int ilvl = Mathf.Max(1, p.level);
            float power = ilvl * (1f + 0.35f * r.rarity);
            string cls = p.cls == Skills.Novice ? "" : p.cls;
            var it = new Item { slot = r.slot, rarity = r.rarity, ilvl = ilvl, uid = System.DateTime.UtcNow.Ticks + rng.Next(100000), soulSlots = Souls.RollSlots(rng, r.rarity), name = "工匠・" + r.name };
            it.classes = r.slot == Slot.Weapon ? cls : "";
            switch (r.slot)
            {
                case Slot.Weapon: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Atk, (0.04f + 0.012f * power) * roll())); break;
                case Slot.Head: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Mp, Mathf.Round((4f + 1.6f * power) * roll()))); break;
                case Slot.Body: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Hp, Mathf.Round((8f + 3f * power) * roll()))); break;
                case Slot.Feet: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Agi, Mathf.Round((1f + 0.35f * power) * roll()))); break;
                default: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Luk, Mathf.Round((1f + 0.3f * power) * roll()))); break;
            }
            foreach (var e in r.effects)
            {
                float v = Mathf.Lerp(e.lo, e.hi, (float)rng.NextDouble());
                v = e.hi >= 1f ? Mathf.Round(v * (1f + 0.02f * ilvl)) : Mathf.Round(v * 1000f) / 1000f;   // 能力值、HP、MP 跟著等級變多；百分比照範圍
                it.mods.Add(new KeyValuePair<Affix, float>(e.a, v));
            }
            it.reqLevel = ilvl; it.reqStat = Stat.Vit; it.reqValue = 0;
            msg = $"🛠 做好了 [{Item.RarityNames[r.rarity]}] {it.name}（Lv{ilvl}）！";
            return it;
        }
    }
}
