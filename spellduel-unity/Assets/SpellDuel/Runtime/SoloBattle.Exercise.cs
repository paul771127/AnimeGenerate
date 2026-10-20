using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 運動紀錄（每場戰鬥）：蹲下幾次、跳幾次、閃避幾次、走了幾公尺、消耗約幾大卡。結算畫面顯示，並累計到總紀錄。
    ///   閃避＝蹲過／跳過／側跨閃過的攻擊（evade 事件）＋從身邊 1.2m 內飛過卻沒打中的攻擊。
    ///   大卡（約 60～70 公斤的估算）＝活動分鐘 × 3.5 ＋ 蹲 × 0.4 ＋ 跳 × 0.3 ＋ 公尺 × 0.07。
    /// </summary>
    public partial class SoloBattle
    {
        int exSquats, exJumps, exDodges; float exMeters, exActive;
        bool exWasCrouch, exWasAir; Vector3 exLastFeet; bool exHasFeet;
        readonly Dictionary<Projectile, float> exNear = new Dictionary<Projectile, float>();
        int exHitsAtNear;

        public static float Kcal(float minutes, int squats, int jumps, float meters) => minutes * 3.5f + squats * 0.4f + jumps * 0.3f + meters * 0.07f;
        float ExKcal => Kcal(exActive / 60f, exSquats, exJumps, exMeters);

        void ResetExercise()
        {
            exSquats = exJumps = exDodges = 0; exMeters = exActive = 0f;
            exWasCrouch = exWasAir = false; exHasFeet = false; exNear.Clear();
        }

        /// <summary>戰鬥中每幀：蹲下、跳起的次數（剛開始的那一下才算）、移動距離、近距離閃過的攻擊</summary>
        void UpdateExercise(float dt)
        {
            var me = Me;
            if (me == null || !me.Alive || battle.now < CalibTime) return;
            exActive += dt;
            if (me.crouching && !exWasCrouch) exSquats++;
            exWasCrouch = me.crouching;
            bool air = me.airborneUntil > battle.now;
            if (air && !exWasAir) exJumps++;
            exWasAir = air;
            var f = me.Feet;
            if (exHasFeet)
            {
                float d = Battle.FlatDistance(f, exLastFeet);
                if (d > 0.01f && d < 1f) exMeters += d;   // 太小＝手機抖動；太大＝重新定位
            }
            exLastFeet = f; exHasFeet = true;
            // 攻擊從身邊飛過：記錄最近距離，消失時沒有打中我＝閃過
            var live = new HashSet<Projectile>();
            foreach (var p in battle.projectiles)
            {
                if (p.owner == me || p.owner.isAlly || !p.alive) continue;
                live.Add(p);
                float dd = Vector3.Distance(p.pos, me.Chest);
                if (!exNear.TryGetValue(p, out var m) || dd < m) exNear[p] = dd;
            }
            foreach (var kv in new List<KeyValuePair<Projectile, float>>(exNear))
            {
                if (live.Contains(kv.Key)) continue;
                exNear.Remove(kv.Key);
                float hitR = kv.Key.skill.radius + me.bodyRadius;
                if (kv.Value < 1.2f && kv.Value > hitR && hitsTaken == exHitsAtNear) exDodges++;
            }
            exHitsAtNear = hitsTaken;
        }

        /// <summary>這場的運動紀錄（一行文字）</summary>
        string ExerciseLine() => $"🔥 約 {ExKcal:0} 大卡　🦵 蹲 {exSquats} 次　⤴ 跳 {exJumps} 次　💨 閃避 {exDodges} 次　👣 {exMeters:0} 公尺";

        /// <summary>結算時：加到總紀錄（只加一次）</summary>
        void SaveExercise()
        {
            if (exActive < 5f) return;
            try
            {
                PlayerPrefs.SetFloat("sd_ex_kcal", PlayerPrefs.GetFloat("sd_ex_kcal", 0f) + ExKcal);
                PlayerPrefs.SetInt("sd_ex_squats", PlayerPrefs.GetInt("sd_ex_squats", 0) + exSquats);
                PlayerPrefs.SetInt("sd_ex_jumps", PlayerPrefs.GetInt("sd_ex_jumps", 0) + exJumps);
                PlayerPrefs.SetInt("sd_ex_dodges", PlayerPrefs.GetInt("sd_ex_dodges", 0) + exDodges);
                PlayerPrefs.SetFloat("sd_ex_minutes", PlayerPrefs.GetFloat("sd_ex_minutes", 0f) + exActive / 60f);
                PlayerPrefs.Save();
            }
            catch { }
        }
        static string ExerciseTotalLine()
        {
            try { return $"累計：🔥 {PlayerPrefs.GetFloat("sd_ex_kcal", 0f):0} 大卡・{PlayerPrefs.GetFloat("sd_ex_minutes", 0f):0} 分鐘・蹲 {PlayerPrefs.GetInt("sd_ex_squats", 0)} 次・閃避 {PlayerPrefs.GetInt("sd_ex_dodges", 0)} 次"; }
            catch { return ""; }
        }

        /// <summary>結算畫面上方的運動紀錄卡（勝利窗格以外的結算：失敗、無盡、每日挑戰…）</summary>
        void DrawExerciseCard(float W, float y, float pad, float lh, int fsS)
        {
            var r = new Rect(pad, y, W - pad * 2, lh * 1.9f);
            UiKit.Card(r, 0.8f, new Color(1f, 0.55f, 0.3f), 0.7f);
            UiKit.Text(new Rect(r.x + 8, r.y + 2, r.width - 16, lh), ExerciseLine(), fsS, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Text(new Rect(r.x + 8, r.y + lh * 0.9f, r.width - 16, lh), ExerciseTotalLine(), Mathf.RoundToInt(fsS * 0.85f), new Color(1f, 0.85f, 0.7f), TextAnchor.MiddleCenter);
        }
    }
}
