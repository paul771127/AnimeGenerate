using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本大地圖：卡點保護（使用者回報第一大關說 100 多 m 外還有 3 隻怪，門過不去）。
    /// 玩家在一個還沒清除的區域裡時，這個區域的守衛離玩家超過 RecallFar 公尺、而且持續 RecallAfter 秒，
    /// 就把牠叫到玩家附近（區域裡、走得到的地方），讓玩家打得到、區域清得完。
    /// </summary>
    public partial class SoloBattle
    {
        const float RecallFar = 20f, RecallAfter = 30f;
        readonly Dictionary<ExMonster, float> exFarSince = new Dictionary<ExMonster, float>();
        ExploreRun exRecallRun;

        void ExRecallStuck(ExploreRun run)
        {
            if (exRecallRun != run) { exRecallRun = run; exFarSince.Clear(); }
            if (battle == null || Me == null || !Me.Alive || coop && !NetIsHost) return;
            var map = run.map; var pf = Me.Feet;
            ExArea here = null;
            foreach (var a in map.areas)
                if (a.entered && !a.cleared && !a.IsBoss && !a.IsChest && new Vector2(pf.x - a.center.x, pf.z - a.center.z).magnitude <= a.radius + 4f) { here = a; break; }
            if (here == null) { exFarSince.Clear(); return; }
            int recalled = 0;
            foreach (var mon in map.monsters)
            {
                if (mon.dead || mon.hidden || mon.area != here.index || mon.patrol != null || mon.wander) continue;
                var p = run.MonPos(mon);
                if (Battle.FlatDistance(p, pf) <= RecallFar) { exFarSince.Remove(mon); continue; }
                if (!exFarSince.TryGetValue(mon, out var since)) { exFarSince[mon] = Time.time; continue; }
                if (Time.time - since < RecallAfter) continue;
                exFarSince.Remove(mon);
                if (RecallSpot(map, here, pf, out var at)) { battle.ExRecall(mon, at); recalled++; }
            }
            if (recalled > 0) Say($"⚠ 躲在遠處的 {recalled} 隻守衛衝回「{here.Title}」了！", 3f);
        }

        /// <summary>玩家附近 5～9m、區域裡、走得到的位置</summary>
        bool RecallSpot(ExploreMap map, ExArea a, Vector3 pf, out Vector3 at)
        {
            for (int t = 0; t < 24; t++)
            {
                float ang = Random.value * Mathf.PI * 2f, r = Random.Range(5f, 9f);
                var p = pf + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                if (!map.Walkable(p, 0.5f)) continue;
                if (new Vector2(p.x - a.center.x, p.z - a.center.z).magnitude > a.radius + 2f) continue;
                at = p; return true;
            }
            // 找不到：放在區域中心（一定走得到）
            at = a.center;
            return map.Walkable(a.center, 0.3f);
        }
    }
}
