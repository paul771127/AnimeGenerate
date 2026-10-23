using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本的劇情人物（DungeonRun 決定誰在場上、哪一邊；這裡負責在戰場上走路、打架）：
    ///   0 我方：保護目標（跟在玩家旁邊、不出手；小怪會優先撲向他）、友軍（幫忙打怪）；
    ///   1 敵方：和小怪一起打玩家（小怪不打他），要打倒才算清完這個地點；
    ///   2 第三勢力：誰都打、誰都打他；這個地點打完就撤退。
    /// 單人／房主跑 AI；連線隊友端是代理（位置、血量由房主送來），打到 → OnMinionHit（編號和小怪共用）。
    /// </summary>
    public partial class Battle
    {
        public readonly List<Fighter> npcs = new List<Fighter>();
        /// <summary>房主：劇情人物倒下（SoloBattle 套用到 DungeonRun、送給隊友）</summary>
        public event Action<Fighter> OnNpcDown;
        public const float NpcSpeed = 1.3f, NpcAttackGap = 1.6f;
        static bool NpcMelee(Fighter n) => n.cls != null && (n.cls.id == "swordsman" || n.cls.id == "assassin");

        /// <summary>還有活著的敵方人物（第三勢力不算：打完這波就撤退）</summary>
        public bool HostileNpcAlive { get { foreach (var n in npcs) if (n.Alive && n.npcSide == 1) return true; return false; } }

        /// <summary>
        /// 叫出一個劇情人物：side 0 我方、1 敵方、2 第三勢力；baseHp 是劇本寫的 hp。
        /// 敵方／第三勢力的血量照這一波的敵人倍率長；我方只照波次長（小怪的攻擊也會變強）。
        /// </summary>
        public Fighter SpawnNpc(string npcId, string name, string look, int side, bool escort, int baseHp, int id = 0, Vector3? at = null)
        {
            var cls = Skills.Classes.TryGetValue(look ?? "", out var c) && Skills.BasicOf(look) != null ? c : Skills.Classes["priest"];
            var n = new Fighter(name, cls, new string[0], false)
            {
                npcSide = side, npcId = npcId, npcEscort = escort, minionId = id > 0 ? id : nextId++,
            };
            float mul = side == 0 ? 1f + HpPerWave * Tier : HpMul;
            n.maxHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * mul)); n.hp = n.maxHp;
            n.dmgMul = side == 0 ? 1f + 0.03f * Tier : DmgMul;
            Vector3 p;
            if (at.HasValue) p = at.Value;
            else if (side == 0) { var r = Fighter.Flat(player.forward); p = player.Feet + new Vector3(r.z, 0f, -r.x) * 1.5f - r * 0.5f; }
            else p = RandomInsidePoint(player.Feet + Fighter.Flat(player.forward) * 5f);
            n.head = new Vector3(p.x, 1.6f, p.z);
            n.forward = Fighter.Flat(player.Feet - n.Feet);
            n.npcNextAtk = now + 1.5f;
            npcs.Add(n);
            Emit("npcspawn", n.Feet, cls.color, null);
            return n;
        }

        /// <summary>隊友端：房主送來的人物狀態（沒有就建一個代理）</summary>
        public Fighter SyncNpc(int id, string npcId, string name, string look, int side, bool escort, Vector3 head, int hp, int maxHp)
        {
            var n = npcs.Find(x => x.minionId == id);
            if (n == null) { n = SpawnNpc(npcId, name, look, side, escort, Mathf.Max(1, maxHp), id, head); }
            var mv = Fighter.Flat(head - n.head);
            if (mv.sqrMagnitude > 1e-4f) n.forward = mv;
            n.head = head; n.hp = hp; n.maxHp = maxHp; n.npcSide = side;
            return n;
        }

        /// <summary>人物離場（撤退、抵達安全地點、隊友端房主說他不在了）</summary>
        public void RemoveNpc(string npcId) { npcs.RemoveAll(n => n.npcId == npcId); }
        public void ClearNpcs() => npcs.Clear();

        /// <summary>這一發會打到誰：小怪＋敵方／第三勢力的人物</summary>
        IEnumerable<Fighter> HittableBy(Fighter owner)
        {
            foreach (var m in minions) yield return m;
            foreach (var n in npcs) if (n.npcSide >= 1 || (owner != null && owner.npcSide == 2 && n.npcSide == 0)) yield return n;
        }

        /// <summary>劇情人物的敵人（活著的）</summary>
        List<Fighter> NpcFoes(Fighter f)
        {
            var list = new List<Fighter>();
            bool hitsEnemies = f.npcSide == 0 || f.npcSide == 2, hitsPlayers = f.npcSide == 1 || f.npcSide == 2;
            if (hitsEnemies) { if (enemy != null && enemy.Alive) list.Add(enemy); foreach (var m in minions) if (m.Alive) list.Add(m); }
            if (hitsPlayers) foreach (var pl in Players()) if (pl.Alive) list.Add(pl);
            foreach (var n in npcs)
            {
                if (n == f || !n.Alive || n.npcSide == f.npcSide) continue;
                list.Add(n);
            }
            return list;
        }

        void UpdateNpcs(float dt)
        {
            // 倒下：房主通知（隊友端的倒下由房主的訊息處理）
            for (int i = npcs.Count - 1; i >= 0; i--)
            {
                var n = npcs[i];
                if (n.Alive) continue;
                npcs.RemoveAt(i);
                if (!remoteEnemy) OnNpcDown?.Invoke(n);
            }
            if (remoteEnemy) return;
            foreach (var n in npcs)
            {
                // 身上的持續傷害
                for (int i = n.dots.Count - 1; i >= 0; i--)
                {
                    var d = n.dots[i]; d.acc += dt;
                    while (d.acc >= 1f && n.Alive) { d.acc -= 1f; Damage(n, d.dps, "dot", n.Chest); }
                    if (d.until <= now) n.dots.RemoveAt(i);
                }
                if (!n.Alive || n.snaredUntil > now) continue;
                Vector3 goal; float stop;
                Fighter tgt = null; float best = float.MaxValue;
                if (!n.npcEscort)
                    foreach (var v in NpcFoes(n)) { float d0 = FlatDistance(n.Feet, v.Feet); if (d0 < best) { best = d0; tgt = v; } }
                n.target = tgt;
                if (tgt != null)
                {
                    goal = tgt.Feet; stop = NpcMelee(n) ? 1.1f : 5f;
                    n.forward = Fighter.Flat(tgt.head - n.head);
                    if (best <= stop + 0.4f + tgt.bodyRadius && now >= n.npcNextAtk) NpcStrike(n, tgt, best);
                }
                else
                {
                    // 保護目標（或沒有敵人時）：跟在最近的玩家旁邊
                    Fighter pl = null; float pd = float.MaxValue;
                    foreach (var p in Players()) { if (!p.Alive) continue; float d0 = FlatDistance(n.Feet, p.Feet); if (d0 < pd) { pd = d0; pl = p; } }
                    if (pl == null) continue;
                    goal = pl.Feet; stop = 1.6f; best = pd;
                    if (pd > 0.3f) n.forward = Fighter.Flat(pl.Feet - n.Feet);
                }
                if (best <= stop) continue;
                var step = Fighter.Flat(goal - n.Feet) * Mathf.Min(NpcSpeed * dt, best - stop);
                foreach (var o in npcs)
                {
                    if (o == n) continue;
                    var away = n.Feet - o.Feet; away.y = 0f; float od = away.magnitude;
                    if (od < 0.7f) step += (od > 1e-3f ? away / od : new Vector3(0.1f, 0f, 0f)) * (0.7f - od) * 2f * dt;
                }
                var next = n.Feet + step;
                if (minionInside == null || minionInside(next) || !minionInside(n.Feet)) n.head = new Vector3(next.x, n.head.y, next.z);
            }
        }

        /// <summary>出手：劍士、刺客的樣子＝近身砍；其他＝射出自己職業的普攻</summary>
        void NpcStrike(Fighter n, Fighter tgt, float d)
        {
            n.npcNextAtk = now + NpcAttackGap;
            var s = Skills.BasicOf(n.cls.id) ?? Skills.All["na_holy"];
            int dmg = Mathf.Max(1, Mathf.RoundToInt((n.npcSide == 0 ? 12f : 10f) * n.dmgMul));
            if (NpcMelee(n))
            {
                Emit("minionattack", n.Feet, s.color, null);
                if (tgt.isAlly) { Emit("allyhit", tgt.Chest, s.color, null); OnAllyHit?.Invoke(tgt, s, dmg, tgt.Chest, 0); }
                else ApplyDamageAndEffect(tgt, dmg, EffectKind.None, 0f, 0f, tgt.Chest);
                return;
            }
            var from = n.head + Fighter.Flat(n.forward) * 0.3f;
            Spawn(n, s, from, (tgt.Chest - from).normalized, dmg, EffectKind.None, 0f, 0f, false);
        }
    }
}
