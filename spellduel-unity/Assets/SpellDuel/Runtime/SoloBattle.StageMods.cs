using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的天候與小目標（邏輯在 Combat.StageMods.cs）：
    ///   每一關「今天」的天候與小目標固定（用關卡 id＋日期決定 → 故事合作的隊友不用另外同步，也會一樣）；
    ///   新手關（1-1～1-3）、試煉沒有；約四成的關卡是晴天，月夜最少（約 6%）。
    ///   小目標達成：通關時多拿這一關 30% 的金幣＋試煉幣 1 枚。
    /// </summary>
    public partial class SoloBattle
    {
        StageWeatherArt weatherArt;
        void UpdateWeatherArt()
        {
            var wanted=battle!=null && phase==Phase.Fighting && pendingStage!=null?battle.weather:Battle.Weather.None;
            if(weatherArt && weatherArt.Weather!=wanted){Destroy(weatherArt.gameObject);weatherArt=null;}
            if(!weatherArt && wanted!=Battle.Weather.None){var poly=area.Polygon;weatherArt=StageWeatherArt.Create(wanted,poly,poly.Count>0?poly[0].y:area.FloorY);}
        }
        void ClearWeatherArt(){if(weatherArt)Destroy(weatherArt.gameObject);weatherArt=null;}

        public const float BonusGoldMul = 0.3f; public const int BonusCoins = 1;

        void SetupStageMods(StoryStage st)
        {
            battle.SetWeather(Battle.Weather.None); battle.SetBonus(Battle.Bonus.None);
            if (st == null || st.id == "trial" || st.id == JobTrialStageId || st.id == "1-1" || st.id == "1-2" || st.id == "1-3") return;
            int seed = (st.id + System.DateTime.Now.ToString("yyyyMMdd")).GetHashCode() & 0x7fffffff;
            var r = new System.Random(seed);
            if (r.NextDouble() >= 0.4)
            {
                // 有天候時：暴雨、濃霧、順風各 3 份，月夜只有 1 份（使用者要求：月夜少一點）→ 月夜整體約 6%
                int k = r.Next(10);
                battle.SetWeather(k < 3 ? Battle.Weather.Rain : k < 6 ? Battle.Weather.Fog : k < 9 ? Battle.Weather.Wind : Battle.Weather.Moon);
            }
            battle.SetBonus((Battle.Bonus)(1 + r.Next(3)));
            if (battle.weather != Battle.Weather.None) Say($"今天的天候：{Battle.WeatherName(battle.weather)}——{Battle.WeatherDesc(battle.weather)}", 4f);
        }

        /// <summary>通關：小目標達成 → 額外金幣＋試煉幣</summary>
        string GrantStageBonus(StoryStage st, float rmul)
        {
            if (battle == null || battle.bonus == Battle.Bonus.None || !battle.BonusDone) return "";
            int g = Mathf.RoundToInt(Economy.StageGold(st) * rmul * BonusGoldMul);
            pendingGold += g; TrialCoins.Coins += BonusCoins;
            return $"\n⭐ 小目標達成：金幣 +{g}、試煉幣 +{BonusCoins}";
        }

        /// <summary>月夜：掉寶率 ×1.5</summary>
        float WeatherLootMul => battle != null && battle.weather == Battle.Weather.Moon ? 1.5f : 1f;

        /// <summary>HUD：天候的畫面色調＋右上的天候、小目標</summary>
        void DrawStageModsHud(float W, float H)
        {
            if (battle == null || phase != Phase.Fighting || pendingStage == null) return;
            var w = battle.weather;
            Color tint = w == Battle.Weather.Fog ? new Color(0.85f, 0.88f, 0.92f, 0.08f) : w == Battle.Weather.Moon ? new Color(0.1f, 0.15f, 0.4f, 0.1f) : w == Battle.Weather.Rain ? new Color(0.25f, 0.3f, 0.4f, 0.05f) : Color.clear;
            if (tint.a > 0f) { GUI.color = tint; GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            int fs = Mathf.RoundToInt(H * 0.018f);
            string line = (w != Battle.Weather.None ? Battle.WeatherName(w) + "　" : "") + battle.BonusText;
            if (line.Length > 0) UiKit.Text(new Rect(W * 0.04f, H * 0.165f, W * 0.92f, fs * 1.6f), line, fs, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter, true);
        }
    }
}
