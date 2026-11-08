using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 任務的挑戰（特殊地點：靶場、守護、護送、彈幕…）改成不強迫（使用者要求）：
    /// 沒過＝沒有獎勵，關卡照常繼續（Combat.FinishRoomEvent failed）；通過＝馬上給獎勵（直接放進背包，不會因為之後沒過關就不見）：
    ///   ・稀有道具：一半機率是神秘盒子（MysteryBox.cs），一半是一件稀有以上的裝備
    ///   ・素材 2 樣（一般怪的素材、鐵礦石）
    ///   ・金錢（跟著等級變多）
    /// </summary>
    public partial class SoloBattle
    {
        public const float ChallengeBoxChance = 0.5f;
        float lastChallengeRewardAt = -10f;

        void GrantChallengeReward()
        {
            if (prof == null || battle == null) return;
            if (Time.time - lastChallengeRewardAt < 1f) return;   // 同一個挑戰重複的通知只給一次
            lastChallengeRewardAt = Time.time;
            var got = new List<string>();
            int ilvl = dungeon != null ? dungeon.level + battle.levelBoost : Mathf.Max(1, prof.level);
            // 稀有道具
            if (dropRng.NextDouble() < ChallengeBoxChance) { Economy.AddItem(MysteryBox.Id, 1); got.Add($"{MysteryBox.Icon}{MysteryBox.Name}"); }
            else
            {
                ItemGen.SetTheme = battle.Theme;
                var it = ItemGen.Make(dropRng, ilvl, ItemGen.RollRarity(dropRng, prof.Total(Stat.Luk) * 0.3f, 2), myClass);
                bool bag = Progress.AddToBagOrMail(it, "挑戰獎勵");
                got.Add($"[{Item.RarityNames[it.rarity]}] {it.FullName}" + (bag ? "" : "（背包滿了，寄到信箱）"));
            }
            // 素材 2 樣
            for (int k = 0; k < 2; k++)
            {
                string mat = "mat_ore";
                for (int t = 0; t < 30; t++) { var l = Crafting.RollMinion(dropRng.Next(0, 23), battle.Theme, 2, dropRng); if (l.Count > 0) { mat = l[dropRng.Next(l.Count)]; break; } }
                Economy.AddItem(mat, 1);
                var md = Crafting.GetMat(mat); got.Add(md != null ? md.icon + md.name : mat);
            }
            // 金錢
            int gold = 60 + 8 * Mathf.Max(1, prof.level);
            Economy.Gold += gold; got.Add($"💰{gold}");
            string line = "🎁 挑戰獎勵（已放進背包）：" + string.Join("、", got);
            Say(line, 4f); ChatEvent(line);
            Sfx.Play("win", 0.6f, 1.3f); Haptics.Play(Haptics.Kind.Success);
        }
    }
}
