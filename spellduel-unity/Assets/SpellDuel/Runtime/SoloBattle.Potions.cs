using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 強化藥劑（藥劑師做的，Crafting.Potions）：喝了 3 分鐘有效。
    ///   ・只在戰鬥中喝：點畫面上的藥劑按鈕，或唸藥劑的名字（「力量藥劑」）。同一種再喝＝重新算 3 分鐘。
    /// 效果直接加在自己的戰鬥數值上；角色畫面重新套用能力值時再補回來；時間到就扣掉。
    /// </summary>
    public partial class SoloBattle
    {
        class ActiveBuff { public Crafting.Potion p; public float until; public bool applied; public float delta; }
        readonly List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
        Battle buffBattle;

        /// <summary>戰鬥中喝一罐（扣存貨）</summary>
        bool DrinkBuffPotion(Crafting.Potion p)
        {
            if (Economy.Count(p.id) <= 0) { Say($"沒有{p.name}了（到{WorldUI.Towns[Mathf.Clamp(p.town, 0, WorldUI.Towns.Length - 1)].name}的藥劑師做）", 2f); return false; }
            if (battle == null || phase != Phase.Fighting || Me == null) return false;   // 只在戰鬥中喝
            Economy.AddItem(p.id, -1);
            Sfx.Play("potion", 0.8f, 1.1f);
            StartBuff(p);
            floaters.Add(new Floater { text = $"{p.icon} {p.name}：{p.desc}", color = new Color(0.8f, 0.6f, 1f), born = Time.time, screen = true });
            return true;
        }

        void StartBuff(Crafting.Potion p)
        {
            var old = activeBuffs.Find(b => b.p.id == p.id);
            if (old != null) { old.until = battle.now + Crafting.PotionTime; SyncBuffIcons(); return; }   // 同一種：重新算時間
            var nb = new ActiveBuff { p = p, until = battle.now + Crafting.PotionTime };
            activeBuffs.Add(nb);
            ApplyBuff(Me, nb, true);
            SyncBuffIcons();
        }

        /// <summary>加上（on）或扣掉效果；delta 記下實際加了多少，扣的時候照扣</summary>
        static void ApplyBuff(Fighter f, ActiveBuff b, bool on)
        {
            if (f == null || b.applied == on) return;
            float a = b.p.amount;
            switch (b.p.kind)
            {
                case Crafting.BuffKind.Atk: if (on) f.atkMul *= 1f + a; else f.atkMul /= 1f + a; break;
                case Crafting.BuffKind.Crit: f.critChance += on ? a : -a; break;
                case Crafting.BuffKind.Regen: if (on) b.delta = f.maxHp * a; f.hpRegen += on ? b.delta : -b.delta; break;
                case Crafting.BuffKind.Charge: if (on) f.chargeScale *= 1f - a; else f.chargeScale /= 1f - a; break;
                case Crafting.BuffKind.Cdr: if (on) f.cdMul *= 1f - a; else f.cdMul /= 1f - a; break;
                case Crafting.BuffKind.MagicRed: f.magicReduce += on ? a : -a; break;
                case Crafting.BuffKind.PhysRed: f.gearReduce += on ? a : -a; break;
                case Crafting.BuffKind.Lifesteal: f.lifesteal += on ? a : -a; break;
                case Crafting.BuffKind.Evade: f.evadeChance += on ? a : -a; break;
                case Crafting.BuffKind.MaxHp:
                    if (on) { b.delta = Mathf.Round(f.maxHp * a); f.maxHp += Mathf.RoundToInt(b.delta); if (f.Alive) f.hp += b.delta; }
                    else { f.maxHp -= Mathf.RoundToInt(b.delta); f.hp = Mathf.Min(f.hp, f.maxHp); }
                    break;
            }
            b.applied = on;
        }

        /// <summary>每幀：換場清掉；時間到的扣掉</summary>
        void UpdateBuffPotions()
        {
            if (battle == null || Me == null) return;
            if (buffBattle != battle)
            {
                buffBattle = battle; activeBuffs.Clear();
            }
            bool changed = false;
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
                if (battle.now >= activeBuffs[i].until)
                {
                    ApplyBuff(Me, activeBuffs[i], false);
                    floaters.Add(new Floater { text = $"{activeBuffs[i].p.icon} {activeBuffs[i].p.name} 效果結束", color = new Color(0.7f, 0.7f, 0.8f), born = Time.time, screen = true });
                    activeBuffs.RemoveAt(i); changed = true;
                }
            if (changed) SyncBuffIcons();
        }

        /// <summary>角色畫面重新套用了能力值（數值重算）：還有效的藥劑再加回去</summary>
        void ReapplyBuffPotions()
        {
            foreach (var b in activeBuffs) { b.applied = false; ApplyBuff(Me, b, true); }
        }

        /// <summary>右上角的狀態圖示（SoloBattle.StatusIcons.cs）</summary>
        void SyncBuffIcons()
        {
            if (Me == null) return;
            Me.potionBuffs.Clear();
            foreach (var b in activeBuffs) Me.potionBuffs.Add(new KeyValuePair<string, float>(b.p.id, b.until));
        }

        /// <summary>戰鬥中唸藥劑的名字：喝下去</summary>
        bool PotionByVoice(string text)
        {
            if (string.IsNullOrEmpty(text) || !ItemsUsable) return false;
            foreach (var p in Crafting.Potions)
            {
                string shortName = p.name.Replace("藥劑", "");
                if (!(text.Contains(p.name) || text.Contains(shortName + "藥") || text.Contains(shortName + "药"))) continue;
                if (Economy.Count(p.id) <= 0) { Say($"沒有{p.name}", 1.5f); return true; }
                DrinkBuffPotion(p);
                return true;
            }
            return false;
        }

        /// <summary>戰鬥中：持有的強化藥劑一排按鈕，點了就喝（3 分鐘）；回傳用掉的高度（沒有藥劑＝0）</summary>
        float DrawBattlePotions(Rect row, float gap, int fsS)
        {
            int k = 0; foreach (var p in Crafting.Potions) if (Economy.Count(p.id) > 0) k++;
            if (k == 0) return 0f;
            float bw = Mathf.Min((row.width - gap * (k - 1)) / k, row.width * 0.32f), x = row.x;
            foreach (var p in Crafting.Potions)
            {
                int n = Economy.Count(p.id); if (n <= 0) continue;
                var r = new Rect(x, row.y, bw, row.height);
                var on = activeBuffs.Find(b => b.p.id == p.id);
                string t = $"{p.icon}{p.name.Replace("藥劑", "")}×{n}" + (on != null ? $"\n{on.until - battle.now:0}s" : "");
                if (UiKit.Button(r, t, Mathf.RoundToInt(fsS * 0.85f), on != null, new Color(0.7f, 0.5f, 1f))) DrinkBuffPotion(p);
                x += bw + gap;
            }
            return row.height + gap * 0.3f;
        }
    }
}
