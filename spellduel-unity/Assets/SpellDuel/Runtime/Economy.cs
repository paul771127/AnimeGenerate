using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 經濟系統（故事／無盡模式）：金幣、消耗道具、鐵匠鋪強化、套裝。（進階職業見 Advanced.cs）
    /// 存檔沿用 Progress.Get／Set（PlayerPrefs）：sd_gold、sd_items。
    /// </summary>
    public static class Economy
    {
        /// <summary>套裝 4 件效果的附加狀態機率（使用者：套裝最高到 15% 沒問題；單件裝備的詞綴仍是 2～5%）</summary>
        public const float SetStatusChance = 0.15f;
        // ---------------------------------------------------------------- 金幣
        public static int Gold
        {
            get
            {
                var s = Progress.Get("sd_gold");
                if (string.IsNullOrEmpty(s)) return StartGold;   // 第一次玩：給一點起始金幣
                int.TryParse(s, out var g); return Mathf.Max(0, g);
            }
            set => Progress.Set("sd_gold", Mathf.Max(0, value).ToString(CultureInfo.InvariantCulture));
        }

        public const int StartGold = 150;

        /// <summary>打倒小怪的金幣（過關後才拿得到）</summary>
        public static int MinionGold(int wave) => 3 + wave / 3;
        public static int BossGold(int wave) => 40 + wave * 3;
        /// <summary>故事關卡通關獎勵</summary>
        public static int StageGold(StoryStage st) => 50 + st.xp / 2;

        // ---------------------------------------------------------------- 消耗道具
        public class Consumable
        {
            public string id, name, icon, desc;
            public int price;
            public float hpFrac, mpFlat;       // 回復：最大 HP 的比例、MP
            public bool scroll;                // 回城捲軸
        }

        public static readonly List<Consumable> Consumables = new List<Consumable>
        {
            new Consumable { id = "hp_s", name = "小治療藥水", icon = "🧪", price = 30, hpFrac = 0.25f, desc = "回復最大 HP 的 25%" },
            new Consumable { id = "hp_l", name = "大治療藥水", icon = "🍷", price = 90, hpFrac = 0.6f, desc = "回復最大 HP 的 60%" },
            new Consumable { id = "mp_s", name = "魔力藥水", icon = "💧", price = 40, mpFlat = 50f, desc = "回復 50 MP" },
            new Consumable { id = "scroll", name = "回城捲軸", icon = "📜", price = 120, scroll = true, desc = "故事關卡中使用：帶著這關已經拿到的戰利品回到城鎮（不用打完）" },
        };
        public static Consumable Find(string id) => Consumables.Find(c => c.id == id);
        /// <summary>藥水共用冷卻：30 秒只能喝一瓶（治療、魔力都算）</summary>
        public const float PotionCooldown = 30f;

        static Dictionary<string, int> items;
        public static void ResetCache() => items = null;   // 換角色：道具重新讀
        static Dictionary<string, int> Items
        {
            get
            {
                if (items != null) return items;
                items = new Dictionary<string, int>();
                foreach (var kv in (Progress.Get("sd_items") ?? "").Split(','))
                {
                    var p = kv.Split(':');
                    if (p.Length == 2 && int.TryParse(p[1], out var n) && n > 0) items[p[0]] = n;
                }
                return items;
            }
        }
        public static int Count(string id) => Items.TryGetValue(id, out var n) ? n : 0;
        public static void AddItem(string id, int n)
        {
            Items[id] = Mathf.Max(0, Count(id) + n);
            var sb = new StringBuilder();
            foreach (var kv in Items) if (kv.Value > 0) { if (sb.Length > 0) sb.Append(','); sb.Append(kv.Key).Append(':').Append(kv.Value); }
            Progress.Set("sd_items", sb.ToString());
        }

        public static bool Buy(Consumable c, out string why)
        {
            why = null;
            if (Gold < c.price) { why = "金幣不夠"; return false; }
            Gold -= c.price; AddItem(c.id, 1);
            return true;
        }

        /// <summary>賣掉裝備的價格</summary>
        public static int SellPrice(Item it) => 8 * (it.rarity + 1) * (it.rarity + 1) + it.ilvl * 3 + it.plus * 15;

        // ---------------------------------------------------------------- 鐵匠鋪：強化
        public const int MaxPlus = 10;
        public const float EnhancePerLevel = 0.08f;   // 每強化 1 級，主屬性 +8%（其他屬性、機率不變）
        public static int EnhanceCost(Item it) => (it.plus + 1) * 50 + it.ilvl * 5 + it.rarity * 30;
        /// <summary>成功率：+1～+5 一定成功，之後越來越難</summary>
        public static float EnhanceRate(Item it) => it.plus < 5 ? 1f : it.plus switch { 5 => 0.8f, 6 => 0.65f, 7 => 0.5f, 8 => 0.4f, _ => 0.3f };

        /// <summary>強化：失敗只扣金幣，裝備不會壞</summary>
        public static bool Enhance(Item it, System.Random rng, out string msg)
        {
            if (it.plus >= MaxPlus) { msg = "已經是最高等級"; return false; }
            int cost = EnhanceCost(it);
            if (Gold < cost) { msg = $"金幣不夠（需要 {cost}）"; return false; }
            // 精煉石（使用者要求）：+1～+5 每次 1 個精煉石、+6～+8 每次 1 個高級精煉石、+9～+10 每次 2 個（成功失敗都會用掉）
            var (stone, sn) = Crafting.StoneFor(it);
            if (Crafting.Have(stone) < sn) { msg = $"{Crafting.MatName(stone)}不夠（要 {sn} 個，有 {Crafting.Have(stone)} 個）：鐵匠可以用 {Crafting.OrePerStone} 個礦石合成"; return false; }
            Gold -= cost; Crafting.Give(stone, -sn);
            if (rng.NextDouble() <= EnhanceRate(it)) { it.plus++; msg = $"✨ 強化成功！{it.FullName}"; return true; }
            msg = "💥 強化失敗…（金幣和精煉石用掉了，裝備沒事）";
            return false;
        }

        // ---------------------------------------------------------------- 套裝
        public class SetDef
        {
            public string id, name, desc;
            public int theme = -1; public bool exclusive;                       // 哪個主題的地圖掉（−1＝到處都會掉）
            public KeyValuePair<Affix, float>[] two, four;
        }

        static KeyValuePair<Affix, float> A(Affix a, float v) => new KeyValuePair<Affix, float>(a, v);

        public static readonly List<SetDef> Sets = new List<SetDef>
        {
            new SetDef {id="clockwork",name="千機",theme=3,exclusive=true,desc="千機迴廊專屬",two=new[]{A(Affix.TrapReduce,.12f)},four=new[]{A(Affix.DodgePower,.15f)}},
            new SetDef { id = "flame", name = "炎龍", theme = 0, desc = "熔岩地帶掉落",
                two = new[] { A(Affix.Atk, 0.08f) }, four = new[] { A(Affix.Burn, Economy.SetStatusChance), A(Affix.Crit, 0.08f) } },
            new SetDef { id = "grove", name = "森羅", theme = 1, desc = "森林掉落",
                two = new[] { A(Affix.Hp, 60f) }, four = new[] { A(Affix.Poison, Economy.SetStatusChance), A(Affix.Lifesteal, 0.05f) } },
            new SetDef { id = "tide", name = "海神", theme = 2, desc = "海上礁石掉落",
                two = new[] { A(Affix.Mp, 40f), A(Affix.Cdr, 0.06f) }, four = new[] { A(Affix.Freeze, Economy.SetStatusChance), A(Affix.MagicArmor, 0.03f) } },
            new SetDef { id = "gladiator", name = "鬥魂", theme = 3, desc = "角鬥場掉落",
                two = new[] { A(Affix.Atk, 0.1f) }, four = new[] { A(Affix.Lifesteal, 0.06f), A(Affix.Crit, 0.1f) } },
            new SetDef { id = "loess", name = "黃沙", theme = 4, desc = "黃土山道掉落",
                two = new[] { A(Affix.Armor, 0.02f) }, four = new[] { A(Affix.Hp, 200f), A(Affix.Agi, 10f) } },
            new SetDef { id = "gale", name = "疾風", desc = "各地都會掉",
                two = new[] { A(Affix.Agi, 8f) }, four = new[] { A(Affix.Crit, 0.1f), A(Affix.Cdr, 0.08f) } },
            new SetDef { id = "guard", name = "守護者", desc = "各地都會掉",
                two = new[] { A(Affix.Armor, 0.02f) }, four = new[] { A(Affix.Hp, 150f), A(Affix.Armor, 0.03f) } },
        };
        public const double SetChance = 0.3;           // 稀有以上的裝備有 30% 是套裝

        public static SetDef SetOf(string id) => string.IsNullOrEmpty(id) ? null : Sets.Find(s => s.id == id);

        /// <summary>挑一套：主題地圖 60% 掉主題套裝，其他時候從通用套裝挑</summary>
        public static SetDef RollSet(System.Random rng, int theme)
        {
            var themed = Sets.Find(s => !s.exclusive && s.theme == theme && theme >= 0);
            if (themed != null && rng.NextDouble() < 0.6) return themed;
            var common = Sets.FindAll(s => !s.exclusive && s.theme < 0);
            return common[rng.Next(common.Count)];
        }

        /// <summary>穿著幾件同一套</summary>
        public static int SetCount(Item[] equipped, string setId)
        {
            int n = 0;
            foreach (var it in equipped) if (it != null && it.setId == setId) n++;
            return n;
        }

        /// <summary>套裝效果加總（2 件、4 件）</summary>
        public static float SetBonus(Item[] equipped, Affix a)
        {
            float v = 0f;
            foreach (var s in Sets)
            {
                int n = SetCount(equipped, s.id);
                if (n >= 2) foreach (var kv in s.two) if (kv.Key == a) v += kv.Value;
                if (n >= 4) foreach (var kv in s.four) if (kv.Key == a) v += kv.Value;
            }
            return v;
        }

        public static string BonusText(KeyValuePair<Affix, float>[] list)
        {
            var parts = new List<string>();
            foreach (var kv in list) parts.Add(Item.AffixText(kv.Key, kv.Value));
            return string.Join("、", parts);
        }

        /// <summary>套裝說明（含目前穿幾件、哪些效果已經啟動）</summary>
        public static string SetText(SetDef s, Item[] equipped)
        {
            int n = equipped != null ? SetCount(equipped, s.id) : 0;
            return $"【{s.name}套裝】{n}/4　2 件：{BonusText(s.two)}{(n >= 2 ? " ✔" : "")}　4 件：{BonusText(s.four)}{(n >= 4 ? " ✔" : "")}";
        }
    }
}
