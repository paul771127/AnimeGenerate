using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 每日訓練與試煉幣：
    ///   訓練場可以每天做一次五種專業試煉（劍術、念咒、射擊、閃避、觀察；SoloBattle.ClassTrial.cs 的每日模式：30 秒做滿、算分數）。
    ///   依分數給試煉幣（三個門檻＝1／2／3 枚）；五種當天都拿到幣再多 5 枚。
    ///   試煉幣在潮騷港酒館老闆娘・凪的「試煉商店」買特殊物品。存檔沿用 Progress.Get／Set（每個角色自己的）。
    /// </summary>
    public static class TrialCoins
    {
        public static readonly string[] Trainings = { "swordsman", "mage", "archer", "assassin", "priest" };
        public const int AllBonus = 5;
        /// <summary>各訓練的分數門檻（1／2／3 枚）</summary>
        public static int[] Tiers(string cls) => cls switch
        {
            "swordsman" => new[] { 5, 8, 12 },
            "mage" => new[] { 15, 20, 25 },
            "archer" => new[] { 8, 12, 16 },
            "assassin" => new[] { 10, 20, 30 },
            "priest" => new[] { 1, 2, 3 },
            _ => new[] { 1, 2, 3 },
        };
        public static string ScoreText(string cls, int score) => cls switch
        {
            "swordsman" => $"打中 {score} 次", "mage" => $"唸對 {score} 組", "archer" => $"命中 {score} 個", "assassin" => $"撐了 {score} 秒", "priest" => $"找出 {score} 隻", _ => $"{score}",
        };
        public static int CoinsFor(string cls, int score) { var t = Tiers(cls); return score >= t[2] ? 3 : score >= t[1] ? 2 : score >= t[0] ? 1 : 0; }

        static string Today => DateTime.Now.ToString("yyyyMMdd");
        static int GetInt(string k) { var s = Progress.Get(k); return int.TryParse(s, out var v) ? v : 0; }
        static void SetInt(string k, int v) => Progress.Set(k, v.ToString());

        public static int Coins { get => GetInt("sd_trial_coins"); set => SetInt("sd_trial_coins", Mathf.Max(0, value)); }
        /// <summary>今天做過了嗎（−1＝還沒；否則＝分數）</summary>
        public static int DoneToday(string cls) { var s = Progress.Get($"sd_train_{Today}_{cls}"); return string.IsNullOrEmpty(s) ? -1 : int.Parse(s); }
        public static bool AllDoneToday() { foreach (var c in Trainings) if (DoneToday(c) < 0) return false; return true; }
        public static bool BonusTaken => GetInt("sd_train_bonus_" + Today) > 0;

        /// <summary>記下今天這個訓練的分數、發幣；回傳（這次的幣、全部做完的加碼）。同一天第二次不給</summary>
        public static (int coins, int bonus) Record(string cls, int score)
        {
            if (DoneToday(cls) >= 0) return (0, 0);
            Progress.Set($"sd_train_{Today}_{cls}", score.ToString());
            int c = CoinsFor(cls, score), b = 0;
            if (AllDoneToday() && !BonusTaken) { b = AllBonus; SetInt("sd_train_bonus_" + Today, 1); }
            Coins += c + b;
            return (c, b);
        }

        // ---------------------------------------------------------------- 試煉商店（潮騷港・凪）
        public class Ware { public string id, name, icon, desc; public int price; public Func<CharacterProfile, System.Random, string> give; }
        static readonly string[] Cores = { "core_golem", "core_tree", "core_sea", "core_arena", "core_canyon" };
        public static readonly List<Ware> Wares = new List<Ware>
        {
            new Ware { id = "refine3", name = "精煉石 ×3", icon = "🔷", price = 3, desc = "強化 +1～+5 用", give = (p, r) => { Crafting.Give("refine", 3); return "拿到精煉石 ×3"; } },
            new Ware { id = "refinehi", name = "高級精煉石 ×1", icon = "🔶", price = 6, desc = "強化 +6 以上用", give = (p, r) => { Crafting.Give("refine_hi", 1); return "拿到高級精煉石 ×1"; } },
            new Ware { id = "elite", name = "菁英徽記 ×1", icon = "🏅", price = 5, desc = "工匠做特殊裝備的材料", give = (p, r) => { Crafting.Give("elite", 1); return "拿到菁英徽記 ×1"; } },
            new Ware { id = "core", name = "隨機 Boss 核心", icon = "💠", price = 10, desc = "五種 Boss 核心隨機一個（工匠最好的裝備要用）", give = (p, r) => { var c = Cores[r.Next(Cores.Length)]; Crafting.Give(c, 1); return "拿到 " + Crafting.MatName("mat_" + c); } },
            new Ware { id = "potion", name = "隨機強化藥劑 ×2", icon = "⚗", price = 4, desc = "十種強化藥劑隨機一種，戰鬥中喝 3 分鐘有效", give = (p, r) => { var pt = Crafting.Potions[r.Next(Crafting.Potions.Count)]; Economy.AddItem(pt.id, 2); return $"拿到 {pt.icon}{pt.name} ×2"; } },
            new Ware { id = "hpl", name = "大治療藥水 ×3", icon = "🍷", price = 2, desc = "回復最大 HP 的 60%", give = (p, r) => { Economy.AddItem("hp_l", 3); return "拿到大治療藥水 ×3"; } },
            new Ware { id = "box", name = "試煉寶箱", icon = "🎁", price = 15, desc = "一件史詩以上的裝備（物品等級＝你的等級，優先你的職業能用的）", give = (p, r) => Chest(p, r, 3) },
            new Ware { id = "kingbox", name = "王者寶箱", icon = "👑", price = 40, desc = "一件傳說以上的裝備（物品等級＝你的等級）", give = (p, r) => Chest(p, r, 4) },
        };

        static string Chest(CharacterProfile p, System.Random r, int minRarity)
        {
            var it = ItemGen.Make(r, p.level, ItemGen.RollRarity(r, 30f, minRarity), p.cls);
            bool inBag = Progress.AddToBagOrMail(it, "試煉商店");
            return $"開出 [{Item.RarityNames[it.rarity]}] {it.name}" + (inBag ? "" : "（背包滿了，寄到信箱）");
        }

        /// <summary>買：扣幣、給東西；回傳訊息（買不起＝null）</summary>
        public static string Buy(Ware w, CharacterProfile p, System.Random r)
        {
            if (Coins < w.price) return null;
            Coins -= w.price;
            return w.give(p, r);
        }
    }
}
