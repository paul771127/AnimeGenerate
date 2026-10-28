using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的天候與小目標（邏輯在 Combat.StageMods.cs）：
    ///   每一關「今天」的天候與小目標固定（用關卡 id＋日期決定 → 故事合作的隊友不用另外同步，也會一樣）；
    ///   新手關（1-1～1-3）、試煉沒有；約四成的關卡是晴天，月夜最少（約 6%，只在夜晚）。
    ///   白天／夜晚：每 3 小時輪替，約四成是夜晚（使用者要求）。
    ///   小目標達成：通關時多拿這一關 30% 的金幣＋試煉幣 1 枚。
    /// </summary>
    public partial class SoloBattle
    {
        StageWeatherArt weatherArt, nightArt;
        bool ModsShown => battle != null && phase == Phase.Fighting && (pendingStage != null || drun != null);
        void UpdateWeatherArt()
        {
            var wanted=ModsShown?battle.weather:Battle.Weather.None;
            if(weatherArt && weatherArt.Weather!=wanted){Destroy(weatherArt.gameObject);weatherArt=null;}
            if(!weatherArt && wanted!=Battle.Weather.None){var poly=area.Polygon;weatherArt=StageWeatherArt.Create(wanted,poly,poly.Count>0?poly[0].y:area.FloorY);}
            // 副本的夜晚：星空、螢光，場景的平行光調暗偏藍
            bool nightWanted=ModsShown&&battle.night;
            if(nightArt && !nightWanted){Destroy(nightArt.gameObject);nightArt=null;}
            if(!nightArt && nightWanted){var poly=area.Polygon;nightArt=StageWeatherArt.CreateNight(poly,poly.Count>0?poly[0].y:area.FloorY);}
            SetNightLight(nightWanted);
        }
        void ClearWeatherArt(){if(weatherArt)Destroy(weatherArt.gameObject);weatherArt=null;if(nightArt)Destroy(nightArt.gameObject);nightArt=null;SetNightLight(false);}

        // 夜晚的燈光：平行光強度 ×0.45、偏藍；環境光調暗（離開就還原）
        bool nightLit; readonly System.Collections.Generic.Dictionary<Light,(float,Color)> dayLights=new System.Collections.Generic.Dictionary<Light,(float,Color)>(); Color dayAmbient;
        void SetNightLight(bool on)
        {
            if(on==nightLit)return;nightLit=on;
            if(on)
            {
                dayLights.Clear();dayAmbient=RenderSettings.ambientLight;
                foreach(var l in FindObjectsOfType<Light>())if(l.type==LightType.Directional){dayLights[l]=(l.intensity,l.color);l.intensity*=NightLightMul;l.color=new Color(.62f,.7f,1f);}
                RenderSettings.ambientLight=dayAmbient*NightLightMul;
            }
            else
            {
                foreach(var kv in dayLights)if(kv.Key){kv.Key.intensity=kv.Value.Item1;kv.Key.color=kv.Value.Item2;}
                dayLights.Clear();RenderSettings.ambientLight=dayAmbient;
            }
        }
        public const float NightLightMul = 0.45f;

        /// <summary>
        /// 副本的白天／夜晚與天候（使用者要求）：
        ///   白天／夜晚：整趟副本一樣，每 3 小時輪替一次（副本 id＋日期＋時段決定，約四成是夜晚）
        ///   天候：每個地點各擲一次，照主題有不同的機率（熔岩多濃煙、森林多霧雨、海上多風雨）；月夜只在夜晚出現
        ///   千機迴廊（室內）沒有天候、沒有夜晚。用固定的種子算 → 合作的隊友不用同步也一樣。
        /// </summary>
        void SetupDungeonMods()
        {
            if (battle == null || drun == null || dungeon == null) return;
            battle.SetBonus(Battle.Bonus.None);
            if (dungeon.id == TrapGallery.Id || dungeon.id == Carpet.DungeonId) { battle.night = false; battle.assassinDef = null; battle.SetWeather(Battle.Weather.None); return; }
            string slot = System.DateTime.Now.ToString("yyyyMMdd") + "_" + (System.DateTime.Now.Hour / 3);
            bool wasNight = battle.night;
            battle.night = new System.Random((dungeon.id + slot).GetHashCode() & 0x7fffffff).NextDouble() < DungeonNightChance;
            battle.assassinDef = battle.night ? MonsterVariants.AssassinFor(dungeon.id) : null;   // 夜晚：這個副本的刺客怪（使用者要求：白天晚上數值沒差，只差刺客）
            var r = new System.Random((dungeon.id + "/" + drun.node.id + slot).GetHashCode() & 0x7fffffff);
            var w = DungeonWeather(drun.Theme, battle.night, r);
            bool changed = w != battle.weather;
            battle.SetWeather(w);
            string wl = w != Battle.Weather.None ? $"{Battle.WeatherName(w)}——{Battle.WeatherDesc(w)}；{Battle.WeatherTraitText(w)}" : "";
            if (drun.Step == 1) Say((battle.night ? $"🌙 夜晚的副本：小心從背後突襲的刺客「{battle.assassinDef?.name}」" : "☀ 白天的副本") + (wl.Length > 0 ? "\n天候：" + wl : ""), 5f);
            else if (changed) Say(wl.Length > 0 ? "天候變了：" + wl : "天氣放晴了", 4f);
        }
        public const double DungeonNightChance = 0.4;
        /// <summary>副本地點的天候（份數）：晴／雨／霧／風；夜晚另外有 1 份月夜</summary>
        static Battle.Weather DungeonWeather(int theme, bool night, System.Random r)
        {
            int[] w = theme == 0 ? new[] { 45, 10, 25, 20 } : theme == 1 ? new[] { 40, 20, 30, 10 } : theme == 2 ? new[] { 40, 25, 10, 25 } : new[] { 50, 15, 15, 20 };
            int moon = night ? 8 : 0, total = w[0] + w[1] + w[2] + w[3] + moon, k = r.Next(total);
            if (k < w[0]) return Battle.Weather.None; k -= w[0];
            if (k < w[1]) return Battle.Weather.Rain; k -= w[1];
            if (k < w[2]) return Battle.Weather.Fog; k -= w[2];
            if (k < w[3]) return Battle.Weather.Wind;
            return Battle.Weather.Moon;
        }

        /// <summary>故事關卡這個時段是不是夜晚（關卡 id＋日期＋3 小時時段決定 → 合作的隊友算出來一樣）</summary>
        public static bool StoryNight(string stageId, System.DateTime t)
            => new System.Random((stageId + "/story/" + t.ToString("yyyyMMdd") + "_" + (t.Hour / 3)).GetHashCode() & 0x7fffffff).NextDouble() < DungeonNightChance;

        public const float BonusGoldMul = 0.3f; public const int BonusCoins = 1;

        void SetupStageMods(StoryStage st)
        {
            battle.SetWeather(Battle.Weather.None); battle.SetBonus(Battle.Bonus.None); battle.night = false;
            if (st == null || st.id == "trial" || st.id == JobTrialStageId || st.id == "1-1" || st.id == "1-2" || st.id == "1-3") return;
            // 白天／夜晚（使用者要求：故事關卡也有夜晚）：和副本一樣每 3 小時輪替，約四成是夜晚；夜晚只是場景變暗＋夜晚限定的人形敵人
            battle.night = StoryNight(st.id, System.DateTime.Now);
            int seed = (st.id + System.DateTime.Now.ToString("yyyyMMdd")).GetHashCode() & 0x7fffffff;
            var r = new System.Random(seed);
            if (r.NextDouble() >= 0.4)
            {
                // 有天候時：白天暴雨、濃霧、順風各 1/3；夜晚再加月夜（月夜只在夜晚，整體約 6%）
                int k = r.Next(12);
                battle.SetWeather(k < 3 ? Battle.Weather.Rain : k < 6 ? Battle.Weather.Fog : k < 9 ? Battle.Weather.Wind : battle.night ? Battle.Weather.Moon : new[] { Battle.Weather.Rain, Battle.Weather.Fog, Battle.Weather.Wind }[k % 3]);
            }
            battle.SetBonus((Battle.Bonus)(1 + r.Next(3)));
            string wl = battle.weather != Battle.Weather.None ? $"\n今天的天候：{Battle.WeatherName(battle.weather)}——{Battle.WeatherDesc(battle.weather)}；{Battle.WeatherTraitText(battle.weather)}" : "";
            Say((battle.night ? "🌙 夜晚的關卡" : "☀ 白天的關卡") + wl, 5f);
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
            if (!ModsShown) return;
            var w = battle.weather;
            if (battle.night) { GUI.color = new Color(0.04f, 0.07f, 0.22f, 0.22f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }   // 夜晚：畫面整個暗一點、偏藍
            Color tint = w == Battle.Weather.Fog ? new Color(0.85f, 0.88f, 0.92f, 0.08f) : w == Battle.Weather.Moon ? new Color(0.1f, 0.15f, 0.4f, 0.1f) : w == Battle.Weather.Rain ? new Color(0.25f, 0.3f, 0.4f, 0.05f) : Color.clear;
            if (tint.a > 0f) { GUI.color = tint; GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            int fs = Mathf.RoundToInt(H * 0.018f);
            bool dayShown = drun != null && dungeon != null && dungeon.id != TrapGallery.Id && dungeon.id != Carpet.DungeonId || pendingStage != null && pendingStage.id != "trial" && pendingStage.id != JobTrialStageId && pendingStage.id != "1-1" && pendingStage.id != "1-2" && pendingStage.id != "1-3";
            string line = (dayShown ? (battle.night ? "🌙 夜晚　" : "☀ 白天　") : "") + (w != Battle.Weather.None ? Battle.WeatherName(w) + "　" : "") + battle.BonusText;
            if (line.Length > 0) UiKit.Text(new Rect(W * 0.04f, H * 0.165f, W * 0.92f, fs * 1.6f), line, fs, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter, true);
        }
    }
}
