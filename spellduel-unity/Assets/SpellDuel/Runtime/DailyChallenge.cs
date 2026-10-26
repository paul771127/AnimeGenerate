using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 每日挑戰（單人，60 秒）：
    ///   閃避挑戰：四面八方（含身後）的火球隨時飛來、時間越久越多，橫掃（蹲）、落石（走出紅圈）、震地環（跳）會同時出現，越來越密；
    ///             5 顆心，被打中扣一顆，扣完提早結束。分數＝閃過的攻擊數。
    ///   輸出挑戰：對木頭人 60 秒，分數＝總傷害（獎牌門檻依角色等級）。
    ///   跑步挑戰：場地內離你遠的隨機位置出現金幣，金幣會到處亂跑，追上去吃，吃到才出現下一個。分數＝金幣數。
    ///   綜合挑戰（90 秒）：每 30 秒出現一個會逃跑的木頭人（打倒＋20 分，沒打倒會跑掉換下一個），
    ///             同時有閃避挑戰的攻擊（比閃避挑戰鬆一點）；5 顆心。分數＝閃過的攻擊數＋20×擊倒數。
    /// 每天的題目（攻擊順序、方向、金幣位置）用日期當種子，同一天每個人都一樣。
    /// 每個挑戰每天依「今天最好的獎牌」領一次獎勵（之後拿到更高的獎牌補發差額）。
    /// </summary>
    public static class DailyChallenge
    {
        public enum Kind { Dodge = 0, Damage = 1, Run = 2, Combo = 3 }
        public const int KindCount = 4;
        public static readonly string[] Names = { "🛡 閃避挑戰", "⚔ 輸出挑戰", "🏃 跑步挑戰", "🎯 綜合挑戰" };
        public static readonly string[] Descs =
        {
            "60 秒內閃過四面八方（身後也有）飛來的火球，時間越久越多；還會同時來橫掃（蹲）、落石（走出紅圈）、震地環（跳）。5 顆心，被打中扣一顆。",
            "60 秒內對木頭人打出最多傷害。技能、普攻、連段都算。",
            "60 秒內追著吃場地裡的金幣：金幣會出現在離你遠的地方、還會到處亂跑（越吃越快），吃到才出現下一個。",
            "90 秒：每 30 秒出現一個會到處逃跑的木頭人，追上去打倒它（＋20 分；30 秒內沒打倒就跑掉了）。同時四面八方會飛來火球、橫掃（蹲）、震地環（跳），閃過一次＋1 分。5 顆心，被打中扣一顆。",
        };
        public static readonly Color[] Colors = { new Color(0.45f, 0.8f, 1f), new Color(1f, 0.5f, 0.35f), new Color(1f, 0.85f, 0.3f), new Color(0.75f, 0.55f, 1f) };
        public static readonly string[] MedalNames = { "—", "🥉 銅牌", "🥈 銀牌", "🥇 金牌" };

        public const float Duration = 60f, Countdown = 3f;
        public const int Hearts = 5;
        /// <summary>這個挑戰多長（綜合挑戰 90 秒，其他 60 秒）</summary>
        public static float DurationOf(Kind k) => k == Kind.Combo ? 90f : Duration;

        // ---------------------------------------------------------------- 綜合挑戰的木頭人
        /// <summary>每隔幾秒出現一個木頭人（第 0、30、60 秒）</summary>
        public const float TargetEvery = 30f;
        public const int TargetCount = 3, KillPoints = 20;
        /// <summary>木頭人的血量：隨角色等級提高（和輸出挑戰的門檻一樣的倍率）；一般輸出約 15 秒打倒</summary>
        public static int TargetHp(int level) => Mathf.RoundToInt(100f * (1f + 0.12f * Mathf.Max(0, level - 1)));
        /// <summary>木頭人逃跑速度（m/s）：平常閒晃，你靠近（2.5m 內）就加速跑開</summary>
        public const float TargetWalk = 0.5f, TargetFlee = 0.9f, TargetFleeRange = 2.5f;

        /// <summary>今天的日期（yyyyMMdd，手機本地時間）</summary>
        public static string Today => DateTime.Now.ToString("yyyyMMdd");
        /// <summary>今天這個挑戰的種子（同一天大家一樣）</summary>
        public static int Seed(Kind k, string date = null) { unchecked { int h = 17; foreach (char c in (date ?? Today) + (int)k) h = h * 31 + c; return h & 0x7fffffff; } }

        // ---------------------------------------------------------------- 獎牌與獎勵
        /// <summary>獎牌門檻（銅、銀、金）；輸出挑戰依角色等級提高</summary>
        public static int[] Thresholds(Kind k, int level)
        {
            switch (k)
            {
                case Kind.Dodge: return new[] { 25, 45, 60 };   // 一場約 70 發（火球約 58＋橫掃／震地環約 11）
                case Kind.Run: return new[] { 5, 8, 12 };   // 金幣會到處亂跑（0.5→1.1 m/s），比以前難追一點
                case Kind.Combo: return new[] { 40, 70, 100 };   // 90 秒約 70 發攻擊＋最多 3 隻木頭人（每隻 20 分）：只閃不打最多銅牌，金牌要打倒木頭人
                default:
                    float m = 1f + 0.12f * Mathf.Max(0, level - 1);
                    return new[] { Mathf.RoundToInt(200 * m), Mathf.RoundToInt(400 * m), Mathf.RoundToInt(650 * m) };
            }
        }
        public static int MedalOf(Kind k, int score, int level)
        {
            var t = Thresholds(k, level); int m = 0;
            for (int i = 0; i < t.Length; i++) if (score >= t[i]) m = i + 1;
            return m;
        }
        /// <summary>這個獎牌的獎勵（金幣、經驗），隨等級提高</summary>
        public static (int gold, int xp) RewardOf(int medal, int level)
        {
            float m = 1f + 0.1f * Mathf.Max(0, level - 1);
            int[] g = { 0, 40, 80, 150 }, x = { 0, 60, 120, 220 };
            medal = Mathf.Clamp(medal, 0, 3);
            return (Mathf.RoundToInt(g[medal] * m), Mathf.RoundToInt(x[medal] * m));
        }
        /// <summary>今天已經領過的獎牌 got、這次拿到 medal：要補發多少（沒有更高＝0）</summary>
        public static (int gold, int xp) RewardDelta(int got, int medal, int level)
        {
            if (medal <= got) return (0, 0);
            var a = RewardOf(medal, level); var b = RewardOf(got, level);
            return (a.gold - b.gold, a.xp - b.xp);
        }

        // ---------------------------------------------------------------- 紀錄（PlayerPrefs；讀不到就當 0）
        static string Key(Kind k, string what) => $"sd_daily_{(int)k}_{what}";
        public static int BestToday(Kind k) => Get(Key(k, "best_" + Today));
        public static int BestEver(Kind k) => Get(Key(k, "best"));
        public static int MedalToday(Kind k) => Get(Key(k, "medal_" + Today));
        /// <summary>記錄這次的分數；回傳要發的獎勵（今天第一次拿到這個等級以上的獎牌才有）</summary>
        public static (int gold, int xp, int medal, bool newBest) Record(Kind k, int score, int level)
        {
            int medal = MedalOf(k, score, level);
            var d = RewardDelta(MedalToday(k), medal, level);
            bool newBest = score > BestEver(k);
            if (score > BestToday(k)) Set(Key(k, "best_" + Today), score);
            if (newBest) Set(Key(k, "best"), score);
            if (medal > MedalToday(k)) Set(Key(k, "medal_" + Today), medal);
            try { PlayerPrefs.Save(); } catch { }
            return (d.gold, d.xp, medal, newBest);
        }
        // 每個角色自己的紀錄（CharacterSlots.Key 加上角色欄位的前綴）
        static int Get(string key) { try { return PlayerPrefs.GetInt(CharacterSlots.Key(key), 0); } catch { return 0; } }
        static void Set(string key, int v) { try { PlayerPrefs.SetInt(CharacterSlots.Key(key), v); } catch { } }

        // ---------------------------------------------------------------- 閃避挑戰的出題
        /// <summary>攻擊種類：直線彈（側跨）、橫掃（蹲）、震地環（跳）、落石（走出紅圈）</summary>
        public enum Attack { Bolt, Sweep, Quake, Rain }
        /// <summary>閃避挑戰火球的飛行速度（公尺／秒）</summary>
        public const float BoltSpeed = 1.4f;
        public static readonly string[] AttackSkill = { "m_fireball", "b_sweep", "x_quake", "b_rain" };   // 直線彈用單發（扇形三發一步閃不掉）
        public static readonly string[] AttackHint = { "↔ 側跨閃開！", "↓ 蹲下！", "⤴ 跳起來！", "◎ 走出紅圈！" };

        /// <summary>一發攻擊：種類、從哪個方向來（相對你面向的角度，0＝正前方，可以在身後）、距離、飛行速度倍率</summary>
        public struct Shot { public Attack kind; public float angle, dist, speed; }

        /// <summary>
        /// 閃避挑戰的題目（兩條獨立的出題線，會重疊）：
        ///   ①遠程火球：四面八方（360°，身後也會來），速度都一樣；每 BoltEvery 秒射一輪，
        ///     每過 15 秒一輪就多一發（0～15 秒 1 發 …… 45～60 秒 4 發），各自從不同方向。
        ///   ②範圍招：橫掃（蹲）、震地環（跳），第 6 秒起、間隔從 6 秒縮到 4 秒；落石（走出紅圈）目前不出（RainAttacks＝false）。
        /// </summary>
        public class DodgeDirector
        {
            readonly System.Random rnd;
            readonly bool combo;   // 綜合挑戰：一邊追木頭人，攻擊鬆一點
            float nextBolt = 1.5f, nextArea;
            public const float AreaFrom = 6f, QuakeFrom = 6f;   // 橫掃和震地環一起從第 6 秒開始
            /// <summary>範圍招開關；落石先不出（只有橫掃＋震地環）</summary>
            public const bool AreaAttacks = true, RainAttacks = false;
            public DodgeDirector(int seed, bool combo = false) { rnd = new System.Random(seed); this.combo = combo; nextArea = combo ? ComboAreaFrom : AreaFrom; }
            /// <summary>綜合挑戰：每 3 秒一輪火球、每 30 秒多一發（最多 3 發）；橫掃／震地環第 10 秒起、每 8→6 秒</summary>
            public const float ComboBoltEvery = 3f, ComboAreaFrom = 10f;
            public static int ComboVolleyOf(float t) => 1 + Mathf.Clamp(Mathf.FloorToInt(t / 30f), 0, 2);
            public static float ComboAreaInterval(float t) => Mathf.Lerp(8f, 6f, Mathf.Clamp01(t / 90f));
            /// <summary>預備時間（光門出現到攻擊放出）：越後面越短</summary>
            public static float Windup(float t) => Mathf.Lerp(1.2f, 0.85f, Mathf.Clamp01(t / Duration));
            /// <summary>火球每一輪的間隔（固定）</summary>
            public const float BoltEvery = 2.5f;
            public static float BoltInterval(float t) => BoltEvery;
            public static float AreaInterval(float t) => Mathf.Lerp(6f, 4f, Mathf.Clamp01(t / Duration));
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            /// <summary>一輪射幾發：每過 15 秒多一發（0～15 秒 1 發 … 45～60 秒 4 發）</summary>
            public static int VolleyOf(float t, double roll = 0) => 1 + Mathf.Clamp(Mathf.FloorToInt(t / 15f), 0, 3);

            /// <summary>到 t 秒為止該出的攻擊（可能 0～3 發）</summary>
            public List<Shot> Poll(float t)
            {
                var list = new List<Shot>();
                if (t >= nextBolt)
                {
                    nextBolt = t + (combo ? ComboBoltEvery : BoltInterval(t));
                    float a = R(-180f, 180f);
                    int n = combo ? ComboVolleyOf(t) : VolleyOf(t, rnd.NextDouble());
                    for (int i = 0; i < n; i++)   // 齊射：每一發從不同方向（平均分開再加一點隨機）
                        list.Add(new Shot { kind = Attack.Bolt, angle = a + i * 360f / n + R(-25f, 25f), dist = R(4.5f, 6.5f), speed = 1f });
                }
                if (AreaAttacks && t >= nextArea)
                {
                    nextArea = t + (combo ? ComboAreaInterval(t) : AreaInterval(t)) * R(0.8f, 1.2f);
                    var pool = new List<Attack> { Attack.Sweep };
                    if (RainAttacks) pool.Add(Attack.Rain);
                    if (t >= QuakeFrom) pool.Add(Attack.Quake);
                    var k = pool[rnd.Next(pool.Count)];
                    list.Add(new Shot { kind = k, angle = k == Attack.Sweep ? R(-180f, 180f) : R(-60f, 60f), dist = k == Attack.Quake ? 4.5f : R(4.5f, 5.5f), speed = 1f });
                }
                return list;
            }
        }
    }
}
