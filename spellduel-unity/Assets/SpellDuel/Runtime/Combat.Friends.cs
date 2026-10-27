using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 自己這邊的「友方人物」與障壁：
    ///   影舞者的影分身、召喚師收服的魔物（npcs 裡 localFriend＝這台手機在跑 AI）；
    ///   合作時每台手機把自己的友方人物送給隊友（friendOwner＝哪台手機的代理，被怪打到 → 傷害轉給那台）；
    ///   主教的聖域障壁（範圍內的友軍被攻擊不受傷，抵擋次數用完就消失；連線同步）。
    /// </summary>
    public partial class Battle
    {
        // ------------------------------------------------------------ 影舞者：影分身
        public const float CloneLife = 30f;
        /// <summary>影分身倒下（或時間到）：主人的 HP 上限還回來（受的傷不會回來）</summary>
        void SpawnShadowClone(Fighter f, SkillDef s)
        {
            if (f.shadowClone != null && f.shadowClone.Alive) EndClone(f.shadowClone, "影分身換了一個");
            int lend = f.maxHp / 2;
            if (lend < 2) { Emit("fail", f.Chest, s.color, "HP 上限太低，分不出影分身"); return; }
            float curLend = Mathf.Floor(f.hp / 2f);
            f.maxHp -= lend; f.cloneLent += lend;
            f.hp = Mathf.Clamp(f.hp - curLend, 1f, f.maxHp);
            var side = Vector3.Cross(Vector3.up, Fighter.Flat(f.forward)); if (side == Vector3.zero) side = Vector3.right;
            var at = f.Feet + side.normalized * 1.0f;
            if (minionInside != null && !minionInside(at)) at = f.Feet - side.normalized * 1.0f;
            var body = SpawnNpc("clone_" + nextId, "👤 影分身", f.cls != null ? f.cls.id : "assassin", 0, false, lend, 0, at);
            body.maxHp = lend; body.hp = Mathf.Max(1f, curLend);
            body.dmgMul = f.atkMul; body.cloneOf = f; body.localFriend = true; body.friendUntil = now + CloneLife;
            body.critChance = f.critChance; body.npcNextAtk = now + 0.6f;
            f.shadowClone = body;
            Emit("petspawn", at, s.color, "clone");
            Emit("buff", f.Chest, s.color, $"{s.name}！HP 上限分一半給它（{lend}）");
        }

        void EndClone(Fighter body, string why)
        {
            var o = body.cloneOf;
            if (body.Alive) body.hp = 0f;
            body.cloneOf = null;
            if (o == null || o.shadowClone != body) return;
            o.shadowClone = null;
            o.maxHp += o.cloneLent; o.cloneLent = 0;
            Emit("buff", o.Chest, new Color(0.75f, 0.5f, 1f), $"{why}：HP 上限恢復到 {o.maxHp}");
        }

        /// <summary>影分身出手：近身刺擊；每 3 次放一次影殺（×3）、每 4 次讓目標中毒（可疊）</summary>
        void CloneStrike(Fighter n, Fighter tgt)
        {
            var o = n.cloneOf;
            n.cloneStrikes++;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(10f * n.dmgMul));
            var s = Skills.All["na_dagger"];
            if (n.cloneStrikes % 3 == 0 && Skills.All.TryGetValue("shadowkill", out var sk))
            {
                s = sk; dmg *= 3;
                Emit("ambush", tgt.Chest, sk.color, "影分身・影殺！");
            }
            else Emit("minionattack", n.Feet, s.color, null);
            if (n.cloneStrikes % 4 == 0 && Skills.All.TryGetValue("deadlypoison", out var dp))
                HitFoe(o, tgt, dp, dmg, EffectKind.Dot, dp.effectDps * n.dmgMul, dp.effectDur);
            else HitFoe(o, tgt, s, dmg);
        }

        // ------------------------------------------------------------ 影舞者：毒爆
        /// <summary>引爆目標身上所有的毒：剩下的毒傷 ×1.5 一次打完</summary>
        public const float PlagueMul = 1.5f;
        void Plague(Fighter target)
        {
            if (target == null || !target.Alive || target.dots.Count == 0) return;
            float sum = 0f;
            foreach (var d in target.dots) sum += d.dps * Mathf.Max(0f, d.until - now);
            target.dots.Clear();
            int dmg = Mathf.RoundToInt(sum * PlagueMul);
            if (dmg <= 0) return;
            Emit("crit", target.Chest, new Color(0.3f, 0.9f, 0.3f), "萬毒引爆！");
            Damage(target, dmg, "hit", target.Chest);
        }

        // ------------------------------------------------------------ 審判者：傷害轉護盾
        public const float DmgShieldCap = 0.4f, DmgShieldLife = 6f;
        void GainDmgShield(Fighter a, float dmg)
        {
            if (a == null || a.dmgShield <= 0f || dmg <= 0f || !a.Alive) return;
            a.dmgShieldAcc += dmg * a.dmgShield;
            int whole = Mathf.FloorToInt(a.dmgShieldAcc);
            if (whole <= 0) return;
            a.dmgShieldAcc -= whole;
            if (a.shieldUntil <= now) a.shieldLeft = 0;
            a.shieldLeft = Mathf.Min(Mathf.RoundToInt(a.maxHp * DmgShieldCap), a.shieldLeft + whole);
            a.shieldUntil = now + DmgShieldLife;
        }

        // ------------------------------------------------------------ 主教：普攻打中友軍＝治療
        /// <summary>主教的神聖飛彈飛過隊友、友方人物、召喚物 → 幫他回血（打中就消失）</summary>
        public event Action<Fighter, int> OnAllyHeal;
        public const float BishopHealMul = 2f;
        bool TryHealFriend(Projectile pr)
        {
            var o = pr.owner;
            if (o == null || o.adv != "bishop" || pr.skill == null || pr.skill.id != "na_holy" || pr.traveled < 0.6f) return false;
            int amt = Mathf.Max(1, Mathf.RoundToInt(pr.skill.damage * BishopHealMul * o.healMul * o.atkMul));
            foreach (var a in allies)
            {
                if (a == o || !a.Alive || !Touches(pr, a)) continue;
                pr.alive = false;
                o.healDone += amt;
                Emit("heal", a.Chest, new Color(1f, 0.95f, 0.6f), $"+{amt}");
                OnAllyHeal?.Invoke(a, amt);
                return true;
            }
            foreach (var n in npcs)
            {
                if (n.npcSide != 0 || !n.Alive || !Touches(pr, n)) continue;
                pr.alive = false;
                if (n.friendOwner >= 0) OnFriendHeal?.Invoke(n, amt);
                else n.hp = Mathf.Min(n.maxHp, n.hp + amt);
                Emit("heal", n.Chest, new Color(1f, 0.95f, 0.6f), $"+{amt}");
                return true;
            }
            foreach (var p in pets)
            {
                if (!p.alive || p.remote || p.body != null) continue;
                float r = 0.25f + pr.skill.radius;
                if ((pr.pos - p.pos).sqrMagnitude > r * r) continue;
                pr.alive = false;
                p.hp = Mathf.Min(p.maxHp, p.hp + amt);
                Emit("heal", p.pos, new Color(1f, 0.95f, 0.6f), $"+{amt}");
                return true;
            }
            return false;
        }
        /// <summary>隊友（主教）幫我回血</summary>
        public void HealFromAlly(int amt)
        {
            if (!player.Alive || amt <= 0) return;
            player.hp = Mathf.Min(player.maxHp, player.hp + amt);
            Emit("heal", player.Chest, new Color(1f, 0.95f, 0.6f), $"+{amt}");
        }

        // ------------------------------------------------------------ 合作：友方人物同步
        /// <summary>別台手機的友方人物被怪打到（傷害交給那台手機算）／被主教補血</summary>
        public event Action<Fighter, float> OnFriendHurt;
        public event Action<Fighter, int> OnFriendHeal;
        /// <summary>隊友端收服了小怪：請主機把那隻小怪拿掉</summary>
        public event Action<int> OnCaptured;
        public readonly HashSet<int> captured = new HashSet<int>();
        const int FriendIdBase = 900000;

        /// <summary>這台手機在跑的友方人物（影分身、收服的魔物）</summary>
        public List<Fighter> LocalFriends() => npcs.FindAll(n => n.localFriend && n.Alive);

        /// <summary>收到別台手機（pid）的友方人物清單：建立／更新代理，不在清單上的移除</summary>
        public void SyncFriends(int pid, Vector3[] pts, string[] ks)
        {
            var keep = new HashSet<int>();
            for (int i = 0; ks != null && pts != null && i < ks.Length && i < pts.Length; i++)
            {
                var a = ks[i].Split('|');
                if (a.Length < 6 || !int.TryParse(a[0], out var lid)) continue;
                int.TryParse(a[1], out var kind); int.TryParse(a[2], out var hp); int.TryParse(a[3], out var mhp);
                int id = FriendIdBase + pid * 10000 + (lid % 10000);
                keep.Add(id);
                var n = npcs.Find(x => x.minionId == id);
                if (n == null)
                {
                    n = SpawnNpc("friend_" + id, a[4], a[5], 0, false, Mathf.Max(1, mhp), id, pts[i]);
                    n.friendOwner = pid; n.friendLocalId = lid; n.tamedKind = kind;
                    if (kind >= 0) n.bodyRadius = Mathf.Max(n.bodyRadius, 0.3f);
                }
                var mv = Fighter.Flat(pts[i] - n.head);
                if (mv.sqrMagnitude > 1e-4f) n.forward = mv;
                n.head = pts[i]; n.hp = hp; n.maxHp = Mathf.Max(1, mhp);
            }
            npcs.RemoveAll(n => n.friendOwner == pid && !keep.Contains(n.minionId));
        }
        /// <summary>隊友斷線：他的友方人物代理都拿掉</summary>
        public void DropFriends(int pid) => npcs.RemoveAll(n => n.friendOwner == pid);

        /// <summary>我的友方人物在別台被打到／被補血</summary>
        public void HurtLocalFriend(int lid, float dmg)
        {
            var n = npcs.Find(x => x.localFriend && x.minionId == lid);
            if (n != null && n.Alive) Damage(n, dmg, "hit", n.Chest);
        }
        public void HealLocalFriend(int lid, int amt)
        {
            var n = npcs.Find(x => x.localFriend && x.minionId == lid);
            if (n != null && n.Alive) { n.hp = Mathf.Min(n.maxHp, n.hp + amt); Emit("heal", n.Chest, new Color(1f, 0.95f, 0.6f), $"+{amt}"); }
        }

        /// <summary>主機：隊友收服了這隻小怪 → 拿掉（不算擊倒）</summary>
        public void ReleaseMinion(int id) { minions.RemoveAll(m => m.minionId == id); }

        // ------------------------------------------------------------ 主教：聖域障壁
        public class Barrier
        {
            public int id; public string key;   // key：連線用（「手機編號:id」）
            public Fighter owner; public Vector3 pos; public float radius, until; public int charges, maxCharges;
            public bool remote, alive = true;
            public SkillDef skill;
        }
        public readonly List<Barrier> barriers = new List<Barrier>();
        /// <summary>我放了障壁（送給隊友）；障壁擋下一次攻擊（剩幾次，送給所有人）</summary>
        public event Action<Barrier> OnBarrier, OnBarrierUse;

        void AddBarrier(Fighter f, SkillDef s)
        {
            int n = Mathf.Max(1, s.buffAmount);
            var b = new Barrier { id = nextId++, owner = f, pos = new Vector3(f.Feet.x, 0f, f.Feet.z), radius = s.areaRadius, until = now + s.buffDur, charges = n, maxCharges = n, skill = s };
            barriers.Add(b);
            Emit("barrier", b.pos, s.color, $"{s.name}：範圍內的友軍可抵擋 {n} 次攻擊（{s.buffDur:0} 秒）");
            if (f == player) OnBarrier?.Invoke(b);
        }
        public void AddRemoteBarrier(string key, Vector3 pos, float radius, float dur, int charges)
        {
            if (barriers.Exists(x => x.key == key)) return;
            var s = Skills.All.TryGetValue("holybarrier", out var hb) ? hb : null;
            barriers.Add(new Barrier { id = nextId++, key = key, pos = pos, radius = radius, until = now + dur, charges = charges, maxCharges = charges, remote = true, skill = s });
            Emit("barrier", pos, s != null ? s.color : Color.yellow, null);
        }
        public void SyncBarrierUse(string key, int left)
        {
            var b = barriers.Find(x => x.key == key && x.alive);
            if (b == null) return;
            b.charges = Mathf.Min(b.charges, left);
            if (b.charges <= 0) { b.alive = false; Emit("barrierbreak", b.pos, Color.white, null); }
        }

        /// <summary>f 在障壁內被攻擊：扣一次抵擋次數，這一下不受傷。回傳有沒有擋下</summary>
        bool TryBarrier(Fighter f, Vector3? at)
        {
            if (barriers.Count == 0) return false;
            foreach (var b in barriers)
            {
                if (!b.alive || b.until < now || b.charges <= 0) continue;
                if (FlatDistance(f.Feet, b.pos) > b.radius + f.bodyRadius) continue;
                b.charges--;
                Emit("mitigate", at ?? f.Chest, new Color(1f, 0.92f, 0.55f), b.charges > 0 ? $"障壁抵擋！（剩 {b.charges} 次）" : "障壁抵擋！障壁碎了");
                if (b.charges <= 0) { b.alive = false; Emit("barrierbreak", b.pos, Color.white, null); }
                OnBarrierUse?.Invoke(b);
                return true;
            }
            return false;
        }
        /// <summary>障壁保護的對象：自己、我方人物（本機在算傷害的）</summary>
        bool BarrierGuards(Fighter f) => f == player || (f.npcSide == 0 && f.friendOwner < 0);

        void UpdateBarriers()
        {
            foreach (var b in barriers) if (b.alive && b.until < now) b.alive = false;
            barriers.RemoveAll(b => !b.alive);
        }

        void UpdateFriends()
        {
            foreach (var n in npcs)
            {
                if (!n.localFriend || !n.Alive) continue;
                if (n.cloneOf != null && (!n.cloneOf.Alive || (n.friendUntil > 0f && now > n.friendUntil))) EndClone(n, n.cloneOf.Alive ? "影分身時間到" : "影分身消失了");
            }
        }
    
        // ------------------------------------------------------------ 牧師的治療術：補自己＋放出治療光球給隊友（使用者要求）
        public const string HealOrbId = "x_healorb";
        public const float HealOrbRange = 10f, HealOrbAim = 30f;

        void CastPrayHeal(Fighter f, SkillDef s)
        {
            if (f.healMul != 1f) { s = s.Clone(); s.heal = Mathf.RoundToInt(s.heal * f.healMul); }
            ApplySupport(f, s, null);   // 自己
            var t = HealOrbTarget(f);
            AddThreat(f, s.heal * (t != null ? 2 : 1));
            if (t == null) return;
            var orb = Skills.All[HealOrbId];
            var from = f.Chest + Fighter.Flat(f.forward) * 0.3f;
            // 直接建立（不走 Spawn：那裡會套攻擊加成、爆擊、吃掉聖印／信仰）
            projectiles.Add(new Projectile { owner = f, skill = orb, pos = from, basePos = from, dir = (t.Chest - from).normalized, damage = s.heal, target = t, id = nextId++ });
            Emit("heal", f.Chest, s.color, $"治療光球 → {t.name}");
        }

        /// <summary>治療光球要飛給誰：準星（面向）30° 內最近的隊友／友軍；沒有就挑 10m 內血量比例最低（沒滿血）的</summary>
        Fighter HealOrbTarget(Fighter f)
        {
            var cands = new List<Fighter>();
            foreach (var a in allies) if (a != f && a.Alive) cands.Add(a);
            foreach (var n in npcs) if (n.npcSide == 0 && n.Alive && n.hp > 0f) cands.Add(n);
            Fighter aimed = null, hurt = null; float bestD = float.MaxValue, bestK = 1f;
            var fw = Fighter.Flat(f.forward);
            foreach (var c in cands)
            {
                float d = FlatDistance(f.Feet, c.Feet);
                if (d > HealOrbRange) continue;
                var to = Fighter.Flat(c.Feet - f.Feet);
                if (fw != Vector3.zero && to != Vector3.zero && Vector3.Angle(fw, to) <= HealOrbAim && d < bestD) { bestD = d; aimed = c; }
                float k = c.hp / Mathf.Max(1f, c.maxHp);
                if (k < bestK) { bestK = k; hurt = c; }
            }
            return aimed ?? hurt;
        }

        /// <summary>治療光球：追著目標飛，碰到就治療（不會打到敵人）</summary>
        void StepHealOrb(Projectile p, float dt)
        {
            var t = p.target;
            if (t == null || !t.Alive) { p.alive = false; return; }
            var to = t.Chest - p.pos; float d = to.magnitude, step = p.skill.speed * p.speedMul * dt;
            if (d <= step + t.bodyRadius + 0.1f || p.traveled > p.skill.rangeMax + 4f)
            {
                p.alive = false;
                int amt = Mathf.Max(1, p.damage);
                if (t.isAlly) { p.owner.healDone += amt; Emit("heal", t.Chest, p.skill.color, $"+{amt}"); OnAllyHeal?.Invoke(t, amt); return; }   // 合作隊友：送給他的手機補
                if (t.friendOwner >= 0) { OnFriendHeal?.Invoke(t, amt); Emit("heal", t.Chest, p.skill.color, $"+{amt}"); return; }
                float got = Mathf.Min(amt, t.maxHp - t.hp); t.hp += got; p.owner.healDone += got;
                Emit("heal", t.Chest, p.skill.color, $"+{amt}");
                return;
            }
            p.dir = to / d; p.pos += p.dir * step; p.traveled += step;
        }
}
}
