using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 電腦敵人：在場地內走動、照玩家同樣的規則施法。全部用場地座標（公尺）。
    ///   走位：依職業保持距離（刺客/劍士近、法師/弓箭手遠），並左右繞圈；
    ///         蓄力中的技能打不到時，會主動走近或退開；永遠留在場地內。
    ///   出招：思考間隔隨機；血少時補血；看到玩家的攻擊飛來時有機率開防禦。
    ///   瞄準：朝玩家胸口，帶一點誤差（被致盲時誤差很大）。玩家真的走開就閃得掉。
    /// </summary>
    public class EnemyAI
    {
        public float walkSpeed = 1.0f;
        public float thinkMin = 2.5f, thinkMax = 4.5f;
        public float aimErrorDeg = 4f, blindAimErrorDeg = 18f;
        public float guardChance = 0.5f;
        public string Status { get; private set; } = "";

        readonly Battle b;
        readonly Fighter me;
        readonly Func<Vector3, bool> inside;
        readonly Func<Vector3, float> edgeDistance;
        readonly System.Random rnd;
        float nextThink, strafePhase;
        int lastSeenProjectile;

        static readonly Dictionary<string, float> PreferredDistance = new Dictionary<string, float>
        { { "assassin", 2.2f }, { "swordsman", 1.9f }, { "mage", 6f }, { "archer", 8f } };   // 場地太小時 FindInside 會自動縮短

        public EnemyAI(Battle battle, Fighter enemy, Func<Vector3, bool> insideArea, Func<Vector3, float> distanceToEdge, int seed)
        {
            b = battle; me = enemy; inside = insideArea; edgeDistance = distanceToEdge;
            rnd = new System.Random(seed);
            nextThink = 2.5f;   // 開場給玩家一點準備時間
            strafePhase = (float)rnd.NextDouble() * 10f;
        }

        float Rand(float a, float c) => a + (float)rnd.NextDouble() * (c - a);

        public void Update(float dt)
        {
            var player = b.player;
            me.forward = Fighter.Flat(player.head - me.head);
            if (b.Over) return;
            Move(dt);
            if (me.snaredUntil > b.now) { Status = "被定身"; return; }

            if (me.charging != null) { TryRelease(); return; }

            // 看到玩家的新攻擊：一半機率立刻開防禦
            foreach (var p in b.projectiles)
            {
                if (p.owner != player || p.id <= lastSeenProjectile) continue;
                lastSeenProjectile = p.id;
                var guard = me.loadout.Find(s => s.type == SkillType.Self && s.self != SelfKind.Heal && Ready(s));
                if (guard != null && rnd.NextDouble() < guardChance && b.TryChant(me, guard, out _))
                {
                    me.chargeDelay = Rand(0f, 0.12f);
                    Status = $"詠唱 {guard.name}";
                    return;
                }
            }

            if (b.now < nextThink) return;
            nextThink = b.now + Rand(thinkMin, thinkMax);

            SkillDef pick = null;
            if (me.hp < me.maxHp * 0.4f) pick = me.loadout.Find(s => s.self == SelfKind.Heal && Ready(s));
            if (pick == null)
            {
                var attacks = me.loadout.FindAll(s => s.type != SkillType.Self && Ready(s));
                var inRange = attacks.FindAll(s => s.type == SkillType.Trap || b.RangeState(me, s) == 0);
                var list = inRange.Count > 0 && rnd.NextDouble() < 0.85 ? inRange : attacks;   // 偶爾選射程外的，會走過去再放
                if (list.Count > 0) pick = list[rnd.Next(list.Count)];
            }
            if (pick != null && b.TryChant(me, pick, out _))
            {
                me.chargeDelay = Rand(0.2f, 0.9f);
                Status = $"詠唱 {pick.name}";
            }
        }

        bool Ready(SkillDef s) => (!me.cooldownUntil.TryGetValue(s.id, out var cd) || cd <= b.now) && me.mp >= s.cost;

        void TryRelease()
        {
            var s = me.charging;
            if (b.now - me.chargeStart < s.charge + me.chargeDelay) return;
            var player = b.player;
            if (s.type == SkillType.Projectile && !s.releaseNear && b.RangeState(me, s) != 0) { Status = $"{s.name}：調整距離中"; return; }   // 走位會處理（近身技能不限距離，放了再衝過去）

            var floor = player.Feet + new Vector3(Rand(-0.15f, 0.15f), 0, Rand(-0.15f, 0.15f));
            if (s.type == SkillType.Trap && Battle.FlatDistance(me.Feet, floor) > s.trapRange) { Status = $"{s.name}：靠近中"; return; }

            var hand = me.head + Fighter.Flat(me.forward) * 0.25f + Vector3.down * 0.25f;
            var dir = (player.Chest - hand).normalized;
            float err = me.blindUntil > b.now ? blindAimErrorDeg : aimErrorDeg;
            dir = Battle.RotateY(dir, Rand(-err, err));
            dir.y += Rand(-err, err) * Mathf.Deg2Rad * 0.5f;
            if (b.TryRelease(me, dir, floor, out _)) Status = "";
        }

        // ------------------------------------------------------------ 走位
        void Move(float dt)
        {
            var player = b.player;
            var toMe = Fighter.Flat(me.head - player.head);
            if (toMe == Vector3.zero) toMe = Vector3.forward;
            float cur = Battle.FlatDistance(me.head, player.head);

            // 想站的距離：蓄力中的技能決定（打不到就調整）；否則依職業習慣
            float want = PreferredDistance.TryGetValue(me.cls.id, out var pref) ? pref : 3f;
            var c = me.charging;
            if (me.armed != null) want = me.armed.rangeMax * 0.6f;   // 伏擊中：衝向玩家
            else if (c != null && c.releaseNear) want = Mathf.Max(cur, 2.5f);   // 近身技能蓄力時保持距離
            else if (c != null && c.type == SkillType.Projectile)
                want = Mathf.Clamp(cur, Mathf.Max(0.8f, c.rangeMin * 1.15f), c.rangeMax * 0.85f);
            else if (c != null && c.type == SkillType.Trap)
                want = Mathf.Min(cur, c.trapRange * 0.8f);

            // 繞著玩家左右移動（約 ±35 度），讓玩家不容易瞄準
            strafePhase += dt * 0.6f;
            var dir = Battle.RotateY(toMe, Mathf.Sin(strafePhase) * 35f);
            var target = FindInside(player.Feet, dir, want);
            if (target == null) return;

            var feet = me.Feet;
            var delta = target.Value - feet;
            float dist = delta.magnitude;
            if (dist < 0.05f) return;
            var next = feet + delta / dist * Mathf.Min(dist, walkSpeed * dt);
            if (inside(next) && edgeDistance(next) >= 0.3f) me.head = new Vector3(next.x, me.head.y, next.z);
            else if (!inside(feet))   // 不小心在場地外：直接往場地中心走
            {
                var home = -feet; home.y = 0;
                me.head += (home.sqrMagnitude > 1e-4f ? home.normalized : Vector3.zero) * walkSpeed * dt;
            }
        }

        /// <summary>從玩家位置沿 dir 走 want 公尺的點；不在場地內就左右轉角度找，找不到就縮短距離</summary>
        Vector3? FindInside(Vector3 playerFeet, Vector3 dir, float want)
        {
            for (float d = want; d >= 0.6f; d -= 0.4f)
            {
                for (int k = 0; k <= 9; k++)
                {
                    foreach (int sign in new[] { 1, -1 })
                    {
                        var p = playerFeet + Battle.RotateY(dir, sign * k * 20f) * d;
                        p.y = 0;
                        if (inside(p) && edgeDistance(p) >= 0.45f) return p;
                        if (k == 0) break;
                    }
                }
            }
            return null;
        }
    }
}
