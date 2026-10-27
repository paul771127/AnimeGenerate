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
        static bool NpcMelee(Fighter n) => n.tamedKind >= 0 ? !IsRanged(n.tamedKind) : n.cls != null && (n.cls.id == "swordsman" || n.cls.id == "assassin");

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

        // ------------------------------------------------------------ 召喚師：魔物捕捉、餵養召喚物
        /// <summary>魔物捕捉：面前 6m 內、血量剩一半以下的小怪（不含 Boss）→ 變成自己的召喚獸</summary>
        void Capture(Fighter f, SkillDef s)
        {
            Fighter best = null; float bestScore = float.MaxValue;
            var fw = Fighter.Flat(f.forward);
            foreach (var m in minions)
            {
                if (!m.Alive || m.tamedKind >= 0 || m.treasure) continue;   // 寶箱怪抓不了
                var to = m.Feet - f.Feet; to.y = 0f;
                float d = to.magnitude;
                if (d > CaptureRange + m.bodyRadius) continue;
                if (d > 0.5f && fw != Vector3.zero && Vector3.Angle(fw, to) > 45f) continue;
                float score = m.hp / Mathf.Max(1f, m.maxHp) + d * 0.02f;
                if (score < bestScore) { bestScore = score; best = m; }
            }
            if (best == null) { Emit("fail", f.Chest, s.color, "魔物捕捉失敗：面前 6m 內沒有能收服的敵人（Boss 不行）"); return; }
            if (best.hp > best.maxHp * TamedHpNeed) { Emit("fail", best.Chest, s.color, $"{best.name}還太有精神（血量要剩一半以下）"); return; }
            // 成功：從敵人裡移走，變成友方的召喚獸（血量補滿，攻擊照牠原本的強度）
            minions.Remove(best);
            if (remoteEnemy) { captured.Add(best.minionId); OnCaptured?.Invoke(best.minionId); }   // 隊友端：請主機把那隻拿掉
            var mine = pets.FindAll(x => x.alive && !x.remote && x.owner == f && x.kind != "clone");
            for (int i = 0; i <= mine.Count - MaxPets; i++) KillPet(mine[i]);
            var body = SpawnNpc("tamed_" + nextId, "🐾 " + best.name.Replace("精銳・", ""), "priest", 0, false, Mathf.RoundToInt(best.maxHp), 0, best.Feet);
            body.tamedKind = best.minionKind; body.maxHp = best.maxHp; body.hp = body.maxHp; body.dmgMul = best.dmgMul;
            body.bodyRadius = best.bodyRadius; body.head = best.head; body.speedMul = best.speedMul; body.localFriend = true;
            var p = new Pet { id = nextId++, owner = f, kind = "tamed", until = now + s.petLife, body = body, pos = body.Chest, hp = body.hp, maxHp = body.maxHp };
            pets.Add(p);
            Emit("buff", body.Chest, s.color, $"收服了{best.name}！（{s.petLife:0} 秒）");   // 收服的魔物在自己這台跑 AI，位置血量送給隊友顯示
        }

        /// <summary>收服的魔物出手：近戰的直接打，遠程的射出綠色的召喚獸攻擊</summary>
        void TamedStrike(Fighter n, Fighter tgt)
        {
            var pet = pets.Find(x => x.alive && x.body == n);
            float pow = pet != null ? pet.power : 1f;
            int dmg = n.petAtk > 0 ? n.petAtk : Mathf.Max(1, Mathf.RoundToInt(9f * n.dmgMul * pow));   // 出戰的寵物：照寵物的攻擊
            var s = Skills.All["pet_tamed"];
            var owner = pet != null ? pet.owner : player;
            if (NpcMelee(n))
            {
                Emit("minionattack", n.Feet, s.color, null);
                HitFoe(owner, tgt, s, dmg);
                return;
            }
            var from = n.head + Fighter.Flat(n.forward) * 0.3f;
            var pr = Spawn(owner, s, from, (tgt.Chest - from).normalized, dmg, EffectKind.None, 0f, 0f, false);
            pr.damage = dmg; pr.crit = false;
        }

        /// <summary>
        /// 召喚師的攻擊（不是召喚物自己射的）飛過自己的召喚物 → 餵養：回血（＝這一發的傷害），攻擊、血量各 +1%（重新召喚就歸零）。
        /// 剛出手的 0.6m 內不算（火靈、冰靈就浮在肩膀旁邊）。
        /// </summary>
        bool TryFeedPet(Projectile pr)
        {
            var o = pr.owner;
            if (o == null || o.adv != "summoner" || pr.skill == null || pr.skill.cls == "pet" || pr.skill.cls == Souls.SoulCls || pr.traveled < 0.6f) return false;
            foreach (var p in pets)
            {
                if (!p.alive || p.remote || p.owner != o || p.kind == "clone") continue;
                float r = (p.body != null ? p.body.bodyRadius : 0.22f) + pr.skill.radius;
                if ((pr.pos - p.pos).sqrMagnitude > r * r) continue;
                pr.alive = false;
                p.feeds++;
                p.power *= 1f + FeedGrow;
                float heal = Mathf.Max(1f, pr.damage);
                if (p.body != null)
                {
                    p.body.maxHp = Mathf.RoundToInt(p.body.maxHp * (1f + FeedGrow)) + 1;
                    p.body.hp = Mathf.Min(p.body.maxHp, p.body.hp + heal);
                }
                else
                {
                    p.maxHp *= 1f + FeedGrow;
                    p.hp = Mathf.Min(p.maxHp, p.hp + heal);
                }
                Emit("heal", p.pos, new Color(0.55f, 1f, 0.5f), $"+{heal:0}　攻擊・血量 +{(p.power - 1f) * 100f:0}%");
                return true;
            }
            return false;
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
                if (n.cloneOf != null) EndClone(n, "影分身倒下了");
                if (!remoteEnemy && n.friendOwner < 0 && !n.localFriend) OnNpcDown?.Invoke(n);
            }
            UpdateFriends();
            foreach (var n in npcs)
            {
                if (n.friendOwner >= 0 || (remoteEnemy && !n.localFriend)) continue;   // 別台手機的代理、隊友端的劇情人物：不跑 AI
                // 身上的持續傷害
                for (int i = n.dots.Count - 1; i >= 0; i--)
                {
                    var d = n.dots[i]; d.acc += dt;
                    while (d.acc >= 1f && n.Alive) { d.acc -= 1f; Damage(n, d.dps, "dot", n.Chest); }
                    if (d.until <= now) n.dots.RemoveAt(i);
                }
                if (!n.Alive || n.snaredUntil > now || n.rooted) continue;   // 守護物、圖騰、雕像、寶箱：不動也不出手
                if (n.walkPath != null) { EscortWalk(n, dt); continue; }   // 護送：沿著路走
                Vector3 goal; float stop;
                Fighter tgt = null; float best = float.MaxValue;
                if (!n.npcEscort)
                    foreach (var v in NpcFoes(n)) { float d0 = FlatDistance(n.Feet, v.Feet); if (d0 < best) { best = d0; tgt = v; } }
                n.target = tgt;
                if (tgt != null)
                {
                    goal = tgt.Feet; stop = NpcMelee(n) ? 1.1f : 5f;
                    n.forward = Fighter.Flat(tgt.head - n.head);
                    if (n.petUid != 0) PetSkillTick(n, tgt);   // 出戰的寵物：自動放技能（Combat.Pets.cs）
                    if (best <= stop + 0.4f + tgt.bodyRadius && now >= n.npcNextAtk) { NpcStrike(n, tgt, best); if (n.petUid != 0) n.npcNextAtk = now + n.petGap; }
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
            if (n.tamedKind >= 0) { TamedStrike(n, tgt); return; }
            if (n.cloneOf != null) { CloneStrike(n, tgt); n.npcNextAtk = now + 1.1f; return; }
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
