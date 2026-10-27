using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的變化（使用者要求，SoloBattle 在開打時決定）：
    ///   天候（每次進關卡隨機；新手關、試煉沒有）：
    ///     暴雨＝火系傷害 −20%、雷系 +20%；濃霧＝畫面起霧、遠程小怪出手前的預備時間 +30%；
    ///     月夜＝敵人的攻擊 +15%、掉寶率 ×1.5；順風＝我方的投射物飛快 25%
    ///   小目標（不影響過關，達成多拿金幣＋試煉幣）：被打中不超過 5 次／施放 3 種不同的技能／30 秒內清完第一波
    /// </summary>
    public partial class Battle
    {
        public enum Weather { None, Rain, Fog, Moon, Wind }
        public Weather weather;
        /// <summary>副本的夜晚（SoloBattle.StageMods.cs 決定）：敵人的攻擊 +10%（獎勵在 SoloBattle：金幣 +20%、掉寶率 ×1.25）</summary>
        public bool night;
        public const float NightDmg = 1.1f;
        public static string WeatherName(Weather w) => w switch { Weather.Rain => "🌧 暴雨", Weather.Fog => "🌫 濃霧", Weather.Moon => "🌕 月夜", Weather.Wind => "🍃 順風", _ => "" };
        public static string WeatherDesc(Weather w) => w switch
        {
            Weather.Rain => "火系傷害 −20%、雷系 +20%",
            Weather.Fog => "看不清楚遠處；遠程小怪出手比較慢",
            Weather.Moon => "敵人的攻擊 +15%，掉寶率 ×1.5",
            Weather.Wind => "我方的法術、箭飛快 25%",
            _ => "",
        };
        public const float RainFire = 0.8f, RainThunder = 1.2f, MoonDmg = 1.15f, WindSpeed = 1.25f, FogWindup = 1.3f;

        /// <summary>開打時套用天候（濃霧：遠程小怪的預備時間）</summary>
        public void SetWeather(Weather w)
        {
            if (weather == Weather.Fog) rangedWindupMul /= FogWindup;   // 副本每個地點換天候：先把上一個濃霧拿掉
            weather = w;
            if (w == Weather.Fog) rangedWindupMul *= FogWindup;
        }

        /// <summary>天候對這一下傷害的倍率</summary>
        float WeatherMul(SkillDef s, Fighter by)
        {
            float k = 1f;
            if (weather == Weather.Rain) { int e = ElementOf(s); if (e == 1) k *= RainFire; else if (e == 3) k *= RainThunder; }
            if (weather == Weather.Moon && by != null && FoeSide(by)) k *= MoonDmg;
            if (night && by != null && FoeSide(by)) k *= NightDmg;
            return k;
        }

        // ------------------------------------------------------------ 小目標
        public enum Bonus { None, FewHits, Variety, FastWave }
        public Bonus bonus;
        public int bonusHits;
        public readonly HashSet<string> bonusSkills = new HashSet<string>();
        public float bonusFirstWave = -1f;   // 清完第一波花了幾秒（−1＝還沒清完）
        float bonusStart = -1f;
        public const int BonusMaxHits = 5, BonusSkillCount = 3;   // 沒轉職只帶 3 個技能
        public const float BonusWaveTime = 30f;

        public void SetBonus(Bonus b) { bonus = b; bonusHits = 0; bonusSkills.Clear(); bonusFirstWave = -1f; bonusStart = -1f; }

        public bool BonusDone => bonus switch
        {
            Bonus.FewHits => bonusHits <= BonusMaxHits,
            Bonus.Variety => bonusSkills.Count >= BonusSkillCount,
            Bonus.FastWave => bonusFirstWave >= 0f && bonusFirstWave <= BonusWaveTime,
            _ => false,
        };

        /// <summary>HUD 用：小目標與目前進度</summary>
        public string BonusText => bonus switch
        {
            Bonus.FewHits => $"⭐ 被打中不超過 {BonusMaxHits} 次（{bonusHits}/{BonusMaxHits}）" + (bonusHits > BonusMaxHits ? " ✗" : ""),
            Bonus.Variety => $"⭐ 施放 {BonusSkillCount} 種不同的技能（{Mathf.Min(bonusSkills.Count, BonusSkillCount)}/{BonusSkillCount}）" + (bonusSkills.Count >= BonusSkillCount ? " ✓" : ""),
            Bonus.FastWave => bonusFirstWave >= 0f ? $"⭐ {BonusWaveTime:0} 秒內清完第一波（{bonusFirstWave:0} 秒）" + (bonusFirstWave <= BonusWaveTime ? " ✓" : " ✗")
                : $"⭐ {BonusWaveTime:0} 秒內清完第一波" + (bonusStart >= 0f ? $"（{Mathf.Max(0f, now - bonusStart):0} 秒）" : ""),
            _ => "",
        };

        void BonusOnChant(Fighter f, SkillDef s) { if (bonus == Bonus.Variety && f == player && s != null && s.cls != "basic") bonusSkills.Add(s.id); }
        void BonusOnHurt(Fighter target, int damage) { if (bonus == Bonus.FewHits && target == player && damage > 0) bonusHits++; }
        void BonusOnWaveStart() { if (bonusStart < 0f) bonusStart = waveStartAt; }
        void BonusOnWaveCleared() { if (bonusFirstWave < 0f && bonusStart >= 0f) bonusFirstWave = now - bonusStart; }
    }
}
