using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 神秘盒子（使用者要求）：任務的挑戰通過有機率給；在「🎒 背包」打開。
    ///   ・1%：高級首領掉落（史詩以上的首領裝備，20% 是神話；外加首領核心，50% 再加工匠之魂）
    ///   ・10%：傳說裝備
    ///   ・10%：寵物飼料（旅伴糧、精養糧、各屬性的星露糧）
    ///   ・79%：一般怪的掉落物隨機一樣（怪的素材、鐵礦石，或一件一般掉落的裝備）
    /// </summary>
    public static class MysteryBox
    {
        public const string Id = "mystery_box";
        public const string Name = "神秘盒子", Icon = "🎁";
        public const float BossChance = 0.01f, LegendChance = 0.10f, PetFoodChance = 0.10f;

        public static string Desc =>
            $"打開會隨機得到一樣東西：\n・{BossChance * 100f:0}%：高級首領掉落物（史詩以上的首領裝備＋首領核心）\n・{LegendChance * 100f:0}%：傳說裝備\n・{PetFoodChance * 100f:0}%：寵物飼料\n・其他：一般怪的掉落物（素材或裝備）\n・取得：通過副本、大關裡的任務挑戰有機率拿到。";

        static readonly string[] BossCores = { "mat_core_golem", "mat_core_tree", "mat_core_sea", "mat_core_arena", "mat_core_canyon" };

        /// <summary>打開一個（背包裡要有）；回傳拿到什麼（給畫面顯示）。沒有盒子回傳 null</summary>
        public static string Open(CharacterProfile p, System.Random rng)
        {
            if (p == null || Economy.Count(Id) <= 0) return null;
            Economy.AddItem(Id, -1);
            int lv = Mathf.Max(1, p.level);
            double r = rng.NextDouble();
            var got = new List<string>();
            if (r < BossChance)
            {
                int rar = rng.NextDouble() < 0.2 ? 5 : Mathf.Max(3, ItemGen.RollRarity(rng, 10f, 3));
                got.Add(GiveItem(ItemGen.Make(rng, lv + 5, rar, p.cls)));
                got.Add(GiveMat(BossCores[rng.Next(BossCores.Length)]));
                if (rng.NextDouble() < 0.5) got.Add(GiveMat("mat_soul_smith"));
                return "🌟 大獎！高級首領掉落物：" + string.Join("、", got);
            }
            if (r < BossChance + LegendChance) return "✨ 傳說裝備：" + GiveItem(ItemGen.Make(rng, lv, 4, p.cls));
            if (r < BossChance + LegendChance + PetFoodChance)
            {
                var foods = new List<PetFood>();
                foreach (var f in Pets.Foods) if (f.id != "food_star") foods.Add(f);
                double q = rng.NextDouble();
                PetFood pick = q < 0.5 ? Pets.Food("food_basic") : q < 0.8 ? Pets.Food("food_fine") : null;
                if (pick == null) { var stars = foods.FindAll(f => f.star); pick = stars.Count > 0 ? stars[rng.Next(stars.Count)] : foods[0]; }
                int n = pick.star ? 1 : 2;
                Economy.AddItem(pick.id, n);
                return $"🐾 寵物飼料：{pick.icon}{pick.name} ×{n}";
            }
            // 一般怪的掉落物：七成素材、三成一件一般掉落的裝備
            if (rng.NextDouble() < 0.3) return "🎒 " + GiveItem(ItemGen.Make(rng, lv, ItemGen.RollRarity(rng, 0f), p.cls));
            for (int t = 0; t < 30; t++)
            {
                var mats = Crafting.RollMinion(rng.Next(0, 23), rng.Next(0, 5), 2, rng);
                if (mats.Count > 0) return "🪨 " + GiveMat(mats[rng.Next(mats.Count)]);
            }
            return "🪨 " + GiveMat("mat_ore");
        }

        static string GiveItem(Item it)
        {
            bool bag = Progress.AddToBagOrMail(it, Name);
            return $"[{Item.RarityNames[it.rarity]}] {it.FullName}" + (bag ? "" : "（背包滿了，寄到信箱）");
        }

        static string GiveMat(string id)
        {
            Economy.AddItem(id, 1);
            var m = Crafting.GetMat(id);
            return m != null ? m.icon + m.name : id;
        }
    }
}
