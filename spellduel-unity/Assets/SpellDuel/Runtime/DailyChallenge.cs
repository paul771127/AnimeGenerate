using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 每日挑戰（單人，60 秒）：
    ///   閃避挑戰：四面八方的攻擊輪流飛來（直線彈→側跨、橫掃→蹲下、震地環→跳、落石→走出紅圈），越來越密；
    ///             5 顆心，被打中扣一顆，扣完提早結束。分數＝閃過的攻擊數。
    ///   輸出挑戰：對木頭人 60 秒，分數＝總傷害（獎牌門檻依角色等級）。
    ///   跑步挑戰：場地內離你遠的隨機位置出現金幣，跑過去吃，吃到才出現下一個。分數＝金幣數。
    /// 每天的題目（攻擊順序、方向、金幣位置）用日期當種子，同一天每個人都一樣。
    /// 每個挑戰每天依「今天最好的獎牌」領一次獎勵（之後拿到更高的獎牌補發差額）。
    /// </summary>
    public static class DailyChallenge
    {
        public enum Kind { Dodge = 0, Damage = 1, Run = 2 }
        public static readonly string[] Names = { "🛡 閃避挑戰", "⚔ 輸出挑戰", "🏃 跑步挑戰" };
        public static readonly string[] Descs =
        {
            "60 秒內閃過四面八方的攻擊：直線彈側跨、橫掃蹲下、震地環跳起、落石走出紅圈。5 顆心，被打中扣一顆。",
            "60 秒內對木頭人打出最多傷害。技能、普攻、連段都算。",
            "60 秒內跑去吃場地裡的金幣：金幣會出現在離你遠的地方，吃到才出現下一個。",
        };
        public static readonly Color[] Colors = { new Color(0.45f, 0.8f, 1f), new Color(1f, 0.5f, 0.35f), new Color(1f, 0.85f, 0.3f) };
        public static readonly string[] MedalNames = { "—", "🥉 銅牌", "🥈 銀牌", "🥇 金牌" };

        public const float Duration = 60f, Countdown = 3f;
        public const int Hearts = 5;

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
                case Kind.Dodge: return new[] { 12, 20, 26 };   // 一場約 27～28 波
                case Kind.Run: return new[] { 6, 10, 14 };
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
        static int Get(string key) { try { return PlayerPrefs.GetInt(key, 0); } catch { return 0; } }
        static void Set(string key, int v) { try { PlayerPrefs.SetInt(key, v); } catch { } }

        // ---------------------------------------------------------------- 閃避挑戰的出題
        /// <summary>攻擊種類：直線彈（側跨）、橫掃（蹲）、震地環（跳）、落石（走出紅圈）</summary>
        public enum Attack { Bolt, Sweep, Quake, Rain }
        public static readonly string[] AttackSkill = { "m_fireball", "b_sweep", "x_quake", "b_rain" };   // 直線彈用單發（扇形三發一步閃不掉）
        public static readonly string[] AttackHint = { "↔ 側跨閃開！", "↓ 蹲下！", "⤴ 跳起來！", "◎ 走出紅圈！" };

        /// <summary>
        /// 閃避挑戰的題目：依序產生每一波攻擊（第幾秒出現、種類、從哪個方向來（相對你面向的角度）、距離）。
        /// 越後面間隔越短；橫掃 8 秒後、落石 15 秒後、震地環 25 秒後才會出現。
        /// </summary>
        public class DodgeDirector
        {
            readonly System.Random rnd;
            float nextAt = 1.5f;
            public DodgeDirector(int seed) { rnd = new System.Random(seed); }
            /// <summary>預備時間（從方向出現光門到攻擊放出）：越後面越短</summary>
            public static float Windup(float t) => Mathf.Lerp(1.1f, 0.7f, Mathf.Clamp01(t / Duration));
            public static float Interval(float t) => Mathf.Lerp(3.0f, 1.5f, Mathf.Clamp01(t / Duration));

            /// <summary>到 t 秒時有沒有新的攻擊要出（有＝回傳 true 與內容）</summary>
            public bool Next(float t, out Attack kind, out float angle, out float dist)
            {
                kind = Attack.Bolt; angle = 0f; dist = 5f;
                if (t < nextAt) return false;
                nextAt = t + Interval(t) * (0.85f + 0.3f * (float)rnd.NextDouble());
                var pool = new System.Collections.Generic.List<Attack> { Attack.Bolt, Attack.Bolt };
                if (t >= 8f) pool.Add(Attack.Sweep);
                if (t >= 15f) pool.Add(Attack.Rain);
                if (t >= 25f) pool.Add(Attack.Quake);
                kind = pool[rnd.Next(pool.Count)];
                angle = (float)(rnd.NextDouble() * 140.0 - 70.0);   // 前方 ±70°（看得到從哪裡來）
                dist = kind == Attack.Quake ? 4.5f : 4.5f + (float)rnd.NextDouble() * 1.5f;
                return true;
            }
        }
    }
}
