using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 特殊裝備配方（使用者要求）：
    ///   ・配方只有人形敵人（151～165）會掉，每一隻掉自己那一張（RecipeChance；已經學會或身上已經有就不掉）。
    ///   ・拿到配方（道具欄 rcp_＜id＞）交給鐵匠鋪的工匠才學會（bpl_＜id＞），學會後才能製作。
    ///   ・裝備等級固定 Lv50～90（第一章的人形 50、第二章 60…第五章 90），穿上要到那個等級。
    ///   ・材料：一部分是稀有怪（人形、天候怪、夜晚刺客、輔助型）或 Boss 才掉的，其他是一般小怪的材料；
    ///     越難拿（Boss 核心、困難的神匠之魂）→ 稀有度越高（史詩／傳說／神話）、效果越強。
    ///   ・每一件除了能力值，還有固定的特殊效果（用精魄的效果系統：機率發動技能、被打出護盾、反彈…），
    ///     外型搶眼或很 kuso（Item.special → GearLook.Piece.special；美術交給 Codex）。
    /// </summary>
    public static class Blueprints
    {
        public class Blueprint
        {
            public string id, name, look;          // look：外型描述（給 Codex 與說明）
            public int human, level, rarity; public Slot slot;
            public int[] from;                     // 第二批：掉這張配方的怪（小怪種類或變種 id）；人形敵人用 human
            public float chance = RecipeChance;
            public string source;                  // 說明：誰會掉
            public Crafting.Effect[] affixes;      // 能力值（數值在範圍內隨機）
            public SoulFx[] specials;              // 固定的特殊效果
            public (string mat, int n)[] need; public int gold;
            public Color main, accent;
            public string ScrollId => "rcp_" + id;
            public string LearnedId => "bpl_" + id;
            public bool Learned => Economy.Count(LearnedId) > 0;
            public bool Held => Economy.Count(ScrollId) > 0;
        }

        public const float RecipeChance = 0.25f;

        static Crafting.Effect E(Affix a, float lo, float hi) => new Crafting.Effect { a = a, lo = lo, hi = hi };
        static SoulFx F(SoulOp op, float v, float chance = 0f, float dur = 0f, string skill = null) => new SoulFx { op = op, v = v, chance = chance, dur = dur, skill = skill };
        static Blueprint B(string id, string name, int human, Slot slot, int rarity, string look, Color main, Color accent, int gold,
            Crafting.Effect[] affixes, SoulFx[] specials, params (string, int)[] need)
            => new Blueprint { id = "bp_" + id, name = name, human = human, level = 50 + 10 * ((human - 151) / 3), slot = slot, rarity = rarity, look = look,
                main = main, accent = accent, gold = gold, affixes = affixes, specials = specials, need = need };
        static Color C(string hex) { int v = System.Convert.ToInt32(hex.TrimStart('#'), 16); return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f); }   // 不用 ColorUtility（邏輯測試在 Unity 外跑）

        /// <summary>第二批特殊配方（使用者要求）：稀有怪、疾風稀有怪、故事探索的守衛掉；等級另外指定</summary>
        static Blueprint X(string id, string name, int level, string source, float chance, int[] from, Slot slot, int rarity, string look, Color main, Color accent, int gold,
            Crafting.Effect[] affixes, SoulFx[] specials, params (string, int)[] need)
            => new Blueprint { id = "bp_" + id, name = name, level = level, source = source, chance = chance, from = from, slot = slot, rarity = rarity, look = look,
                main = main, accent = accent, gold = gold, affixes = affixes, specials = specials, need = need };
        static readonly int[] SwiftIds = { 171, 172, 173, 174, 175, 176, 177, 178, 179, 180 };

        public static readonly List<Blueprint> All = new List<Blueprint>
        {
            // ---- Lv50：赤爐傭兵團（第一章）
            B("skewer", "燒肉串串大劍", 151, Slot.Weapon, 3, "一支巨大的烤肉串當劍：肉、青椒、洋蔥一層一層，劍尖一直滴油冒火", C("#b8461d"), C("#ffb547"), 3000,
                new[] { E(Affix.Atk, 0.08f, 0.12f), E(Affix.Burn, 0.04f, 0.05f) },
                new[] { F(SoulOp.HitCast, 0, 0.08f, skill: "fire") },
                ("horn", 12), ("fire", 10), ("band_crest", 2), ("ore_hi", 6)),
            B("chicken", "咕咕警報頭盔", 152, Slot.Head, 4, "一整隻公雞造型的頭盔，被打的時候雞冠豎起、張嘴咕咕叫", C("#f4efe6"), C("#e8322b"), 6000,
                new[] { E(Affix.Accuracy, 0.05f, 0.08f), E(Affix.Armor, 0.02f, 0.03f) },
                new[] { F(SoulOp.HurtShield, 120f, 0.12f, 4f) },
                ("fire", 8), ("void", 6), ("band_crest", 3), ("storm_gem", 2), ("core_golem", 1)),
            B("furnace", "行動暖爐鎧", 153, Slot.Body, 5, "整件鎧甲是一座會冒煙的鑄鐵暖爐，胸口小窗看得到爐火，背後煙囪噴火星", C("#3a3330"), C("#ff6a1a"), 12000,
                new[] { E(Affix.Armor, 0.025f, 0.03f), E(Affix.MagicArmor, 0.025f, 0.03f), E(Affix.Vit, 12f, 18f) },
                new[] { F(SoulOp.Thorns, 0.12f), F(SoulOp.HurtInvuln, 0, 0.05f, 1f) },
                ("fire", 15), ("powder", 10), ("band_crest", 4), ("night_edge", 3), ("core_golem", 2), ("soul_smith", 1)),
            // ---- Lv60：霧林遊俠（第二章）
            B("mushroom", "香菇大頭帽", 154, Slot.Head, 3, "超大顆的紅底白點香菇戴在頭上，走路會一直撒出亮晶晶的孢子", C("#d23a3a"), C("#fff4e0"), 3600,
                new[] { E(Affix.Cdr, 0.04f, 0.06f), E(Affix.Spi, 10f, 15f) },
                new[] { F(SoulOp.KillHeal, 0.08f, 0.30f) },
                ("cap", 14), ("gland", 8), ("band_crest", 2), ("ore_hi", 8)),
            B("bamboo", "竹筍風笛弓", 155, Slot.Weapon, 4, "竹筍做的弓，弓身開了笛孔，拉弓會吹出一段走音的笛聲、射出時有風捲葉片", C("#7fae3a"), C("#e9f7b0"), 7200,
                new[] { E(Affix.Atk, 0.10f, 0.14f), E(Affix.Poison, 0.04f, 0.05f) },
                new[] { F(SoulOp.HitCast, 0, 0.10f, skill: "wind") },
                ("fiber", 12), ("seed", 12), ("band_crest", 3), ("storm_gem", 3), ("core_tree", 1)),
            B("planter", "會走路的盆栽靴", 156, Slot.Feet, 5, "兩個陶土花盆當靴子，盆裡長著會隨腳步搖擺的小樹，偶爾開花", C("#a0522d"), C("#5fd068"), 14400,
                new[] { E(Affix.Agi, 14f, 20f), E(Affix.DodgeCdr, 1f, 1f) },
                new[] { F(SoulOp.Regen, 0.001f), F(SoulOp.HurtHeal, 0.06f, 0.10f) },
                ("fiber", 15), ("fang", 10), ("band_crest", 4), ("aid_sigil", 3), ("core_tree", 2), ("soul_smith", 1)),
            // ---- Lv70：黑潮海盜（第三章）
            B("rumbarrel", "蘭姆酒桶鎧", 157, Slot.Body, 3, "把整個木酒桶套在身上，只露出頭和四肢，桶口還插著一根吸管", C("#7a4a24"), C("#d9a441"), 4200,
                new[] { E(Affix.Armor, 0.02f, 0.03f), E(Affix.Vit, 14f, 20f) },
                new[] { F(SoulOp.HurtHeal, 0.05f, 0.12f), F(SoulOp.Gold, 0.08f) },
                ("shell", 14), ("scale", 10), ("band_crest", 2), ("ore_hi", 10)),
            B("jelly", "水母果凍法杖", 158, Slot.Weapon, 4, "杖頭是一隻半透明、會抖動的發光水母，觸鬚一直劈啪放電", C("#64d3ff"), C("#e27bff"), 8400,
                new[] { E(Affix.Atk, 0.11f, 0.15f), E(Affix.Freeze, 0.04f, 0.05f) },
                new[] { F(SoulOp.HitCast, 0, 0.10f, skill: "thunder") },
                ("tendril", 14), ("gel", 10), ("band_crest", 3), ("storm_gem", 3), ("core_sea", 1)),
            B("shark", "鯊魚咬咬鞋", 159, Slot.Feet, 5, "兩隻鯊魚頭拖鞋，鞋口是鯊魚嘴，跳起來時會喀喀咬合，背鰭會發光", C("#4f6d8f"), C("#ffffff"), 16800,
                new[] { E(Affix.Agi, 16f, 22f), E(Affix.DodgePower, 0.10f, 0.14f) },
                new[] { F(SoulOp.AirReduce, 0.12f), F(SoulOp.DuckShield, 0.06f) },
                ("scale", 15), ("spine", 12), ("band_crest", 4), ("night_edge", 3), ("core_sea", 2), ("soul_smith", 1)),
            // ---- Lv80：冠軍鬥士隊（第四章）
            B("trophy", "冠軍獎盃頭盔", 160, Slot.Head, 3, "一個倒過來戴的金色大獎盃，兩邊把手當角，上面刻著「第一名」", C("#e8b923"), C("#fff3b0"), 4800,
                new[] { E(Affix.Armor, 0.02f, 0.03f), E(Affix.Str, 14f, 20f) },
                new[] { F(SoulOp.Block, 0.06f) },
                ("badge", 16), ("elite", 4), ("band_crest", 2), ("ore_hi", 12)),
            B("corn", "巨型烤玉米標槍", 161, Slot.Weapon, 4, "一根比人還高的烤玉米當標槍，表面焦香、射中時玉米粒爆成爆米花", C("#f2c230"), C("#6aa84f"), 9600,
                new[] { E(Affix.Atk, 0.12f, 0.16f), E(Affix.Accuracy, 0.04f, 0.06f) },
                new[] { F(SoulOp.HitSnare, 0, 0.07f, 1f), F(SoulOp.HitCast, 0, 0.08f, skill: "wave") },
                ("badge", 14), ("elite", 5), ("band_crest", 3), ("aid_sigil", 3), ("core_arena", 1)),
            B("neonhalo", "霓虹天使光環", 162, Slot.Trinket, 5, "頭上浮著一圈會跑馬燈、一直換色的霓虹光環，旁邊還有小小的「SAINT」燈牌", C("#ff4fd8"), C("#4ff0ff"), 19200,
                new[] { E(Affix.Cdr, 0.06f, 0.09f), E(Affix.Spi, 16f, 22f) },
                new[] { F(SoulOp.KillHeal, 0.08f, 0.30f), F(SoulOp.Undying, 1f) },
                ("badge", 18), ("elite", 6), ("band_crest", 4), ("aid_sigil", 3), ("core_arena", 2), ("soul_smith", 1)),
            // ---- Lv90：沙漠盜匪（第五章）
            B("luckycat", "招財沙漠貓護符", 163, Slot.Trinket, 3, "一隻戴頭巾的招財貓，舉起的那隻手會一直招，身上掛滿小金幣", C("#f5e6c8"), C("#e0a100"), 5400,
                new[] { E(Affix.Luk, 18f, 24f), E(Affix.Lifesteal, 0.02f, 0.03f) },
                new[] { F(SoulOp.Gold, 0.15f), F(SoulOp.Xp, 0.08f) },
                ("crystal", 16), ("ore_hi", 14), ("band_crest", 2), ("night_edge", 2)),
            B("hourglass", "倒轉沙漏護符", 164, Slot.Trinket, 4, "掛在胸前的大沙漏，沙子往上流，周圍漂浮著幾個時鐘指針", C("#c9a46a"), C("#7ad7ff"), 10800,
                new[] { E(Affix.Cdr, 0.06f, 0.09f), E(Affix.Int, 16f, 22f) },
                new[] { F(SoulOp.ChargeSpeed, 0.08f), F(SoulOp.DodgeCdr, 1f) },
                ("crystal", 14), ("elite", 5), ("band_crest", 3), ("storm_gem", 3), ("core_canyon", 1)),
            B("camel", "駱駝雙峰重甲", 165, Slot.Body, 5, "背上兩個毛茸茸的駝峰（其實是水袋），胸前一顆駱駝頭會嚼草、偶爾吐口水", C("#c8a26b"), C("#7b4b2a"), 21600,
                new[] { E(Affix.Armor, 0.025f, 0.03f), E(Affix.MagicArmor, 0.025f, 0.03f), E(Affix.Vit, 20f, 28f) },
                new[] { F(SoulOp.Reduce, 0.06f), F(SoulOp.HurtShield, 250f, 0.15f, 4f), F(SoulOp.KillMp, 10f, 0.30f) },
                ("crystal", 20), ("ore_hi", 16), ("band_crest", 4), ("aid_sigil", 3), ("storm_gem", 3), ("core_canyon", 2), ("soul_smith", 1)),
            // ---- 第二批：稀有怪、疾風稀有怪、故事探索
            X("bonsai", "盆景森林頭盔", 60, "古根撼地獸", 0.15f, new[] { 19 }, Slot.Head, 4, "頭上頂著一整座小盆景：一棵歪脖子松、苔蘚、小石燈籠，下雨時會自己澆水", C("#6b4a2b"), C("#7fd36b"), 7200,
                new[] { E(Affix.Spi, 14f, 20f), E(Affix.MagicArmor, 0.02f, 0.03f) },
                new[] { F(SoulOp.Regen, 0.0008f), F(SoulOp.HurtHeal, 0.05f, 0.08f) },
                ("root_ring", 6), ("fiber", 12), ("core_tree", 1), ("ore_hi", 8)),
            X("eelwhip", "電鰻跳繩鞭", 70, "潮鞭鰻", 0.15f, new[] { 20 }, Slot.Weapon, 4, "一條活的電鰻當鞭子，揮動時像跳繩一樣甩圈，尾巴劈啪放電，偶爾回頭瞪主人", C("#2a6fb0"), C("#7ff6ff"), 8400,
                new[] { E(Affix.Atk, 0.11f, 0.15f), E(Affix.Freeze, 0.03f, 0.05f) },
                new[] { F(SoulOp.HitCast, 0, 0.10f, skill: "thunder"), F(SoulOp.HitSnare, 0, 0.05f, 1f) },
                ("whip_fin", 6), ("tendril", 10), ("core_sea", 1), ("ore_hi", 10)),
            X("dumbbell", "啞鈴流星錘護符", 80, "鏈錘鬥奴", 0.15f, new[] { 21 }, Slot.Trinket, 4, "腰上掛著一顆用鐵鏈拴住的大啞鈴，走路時會晃來晃去，被打的時候自己甩出去", C("#55606b"), C("#ff5a3c"), 9600,
                new[] { E(Affix.Str, 16f, 22f), E(Affix.Lifesteal, 0.02f, 0.03f) },
                new[] { F(SoulOp.Thorns, 0.12f), F(SoulOp.Block, 0.05f) },
                ("shackle", 6), ("badge", 14), ("core_arena", 1), ("ore_hi", 12)),
            X("griddle", "熔岩烤盤胸甲", 90, "裂岩甲蜥", 0.15f, new[] { 22 }, Slot.Body, 5, "胸前是一塊滾燙的鐵烤盤，上面一直煎著荷包蛋和香腸，背後岩板縫隙冒橘光", C("#3b2b24"), C("#ff7a1f"), 21600,
                new[] { E(Affix.Armor, 0.025f, 0.03f), E(Affix.MagicArmor, 0.025f, 0.03f), E(Affix.Vit, 20f, 28f) },
                new[] { F(SoulOp.Thorns, 0.15f), F(SoulOp.HurtShield, 250f, 0.12f, 4f), F(SoulOp.Reduce, 0.05f) },
                ("rift_shard", 8), ("crystal", 18), ("core_canyon", 2), ("soul_smith", 1)),
            X("rocketskate", "火箭溜冰鞋", 60, "疾風稀有怪", 0.10f, SwiftIds, Slot.Feet, 4, "鞋底是溜冰輪，鞋跟綁著兩支小火箭，衝刺時噴火、停下來冒煙", C("#e63946"), C("#ffd23f"), 7200,
                new[] { E(Affix.Agi, 16f, 22f), E(Affix.DodgeCdr, 1f, 1f) },
                new[] { F(SoulOp.DodgeCdr, 1f), F(SoulOp.AirReduce, 0.10f) },
                ("swift_mane", 8), ("feather", 8), ("elite", 3), ("ore_hi", 8)),
            X("jadesprite", "翠玉樹精耳環", 60, "翠蔭迷林的守衛", 0.20f, new[] { 28, 30, 32 }, Slot.Trinket, 4, "耳邊坐著一隻翠玉做的小樹精，會晃腳、打哈欠，戰鬥時幫你吹口哨加油", C("#2fbf71"), C("#e8fff0"), 7200,
                new[] { E(Affix.Cdr, 0.05f, 0.08f), E(Affix.Int, 14f, 20f) },
                new[] { F(SoulOp.KillMp, 10f, 0.25f), F(SoulOp.Regen, 0.0006f) },
                ("jade_chip", 10), ("core_tree", 1), ("elite", 3), ("ore_hi", 6)),
        };

        public static Blueprint Get(string id) => string.IsNullOrEmpty(id) ? null : All.Find(b => b.id == id || b.ScrollId == id);
        public static Blueprint OfHuman(int variant) => All.Find(b => b.human == variant);
        /// <summary>這種怪會掉的配方（人形敵人、稀有怪、疾風稀有怪、探索守衛）</summary>
        public static Blueprint OfSource(int kind) => All.Find(b => b.human == kind || b.from != null && System.Array.IndexOf(b.from, kind) >= 0);
        public static string SourceText(Blueprint b) => b.source ?? "人形敵人";
        public static bool IsScroll(string itemId) => itemId != null && itemId.StartsWith("rcp_");

        /// <summary>人形敵人倒下：擲這一隻的配方（已經學會、身上已經有、這場已經掉過就不掉）；沒掉＝null</summary>
        public static string RollRecipe(int variant, System.Random rng, ICollection<string> pending = null)
        {
            var b = OfSource(variant);
            if (b == null || b.Learned || b.Held || pending != null && pending.Contains(b.ScrollId)) return null;
            return rng.NextDouble() < b.chance ? b.ScrollId : null;
        }

        /// <summary>把配方交給工匠：學會（配方用掉）</summary>
        public static bool Learn(Blueprint b, out string msg)
        {
            if (b.Learned) { msg = $"已經會做「{b.name}」了"; return false; }
            if (!b.Held) { msg = $"還沒有「{b.name}」的配方（{SourceText(b)}才會掉）"; return false; }
            Economy.AddItem(b.ScrollId, -1); Economy.AddItem(b.LearnedId, 1);
            msg = $"📜 工匠學會了「{b.name}」的做法！";
            return true;
        }

        /// <summary>做出裝備：物品等級固定＝配方等級；主屬性照工匠公式 ×0.9～1.1；能力值在範圍內隨機；特殊效果固定</summary>
        public static Item Craft(Blueprint b, CharacterProfile p, System.Random rng, out string msg)
        {
            if (!b.Learned) { msg = "工匠還不會做這個（先把配方交給他）"; return null; }
            if (!Crafting.CanPay(b.need, b.gold, out msg)) return null;
            Economy.Gold -= b.gold; foreach (var (m, n) in b.need) Crafting.Give(m, -n);
            float roll() => 0.9f + 0.2f * (float)rng.NextDouble();
            int ilvl = b.level;
            float power = ilvl * (1f + 0.35f * b.rarity);
            string cls = p == null || p.cls == Skills.Novice ? "" : p.cls;
            var it = new Item { slot = b.slot, rarity = b.rarity, ilvl = ilvl, uid = System.DateTime.UtcNow.Ticks + rng.Next(100000), soulSlots = Souls.RollSlots(rng, b.rarity), name = b.name, special = b.id };
            it.classes = b.slot == Slot.Weapon ? cls : "";
            switch (b.slot)
            {
                case Slot.Weapon: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Atk, (0.04f + 0.012f * power) * roll())); break;
                case Slot.Head: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Mp, Mathf.Round((4f + 1.6f * power) * roll()))); break;
                case Slot.Body: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Hp, Mathf.Round((8f + 3f * power) * roll()))); break;
                case Slot.Feet: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Agi, Mathf.Round((1f + 0.35f * power) * roll()))); break;
                default: it.mods.Add(new KeyValuePair<Affix, float>(Affix.Luk, Mathf.Round((1f + 0.3f * power) * roll()))); break;
            }
            foreach (var e in b.affixes)
            {
                float v = Mathf.Lerp(e.lo, e.hi, (float)rng.NextDouble());
                v = e.hi >= 1f ? Mathf.Round(v) : Mathf.Round(v * 1000f) / 1000f;
                it.mods.Add(new KeyValuePair<Affix, float>(e.a, v));
            }
            it.reqLevel = ilvl; it.reqStat = Stat.Vit; it.reqValue = 0;
            msg = $"🛠 做好了 [{Item.RarityNames[b.rarity]}] {it.name}（Lv{ilvl}）！";
            return it;
        }

        /// <summary>裝備說明：特殊效果（每一行）</summary>
        public static List<string> SpecialLines(Item it)
        {
            var l = new List<string>();
            var b = it != null ? Get(it.special) : null;
            if (b != null) foreach (var x in b.specials) l.Add("✨ " + Souls.FxText(x));
            return l;
        }
    }
}
