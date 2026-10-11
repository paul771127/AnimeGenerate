using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// Boss（熔岩巨像）的 AI：全部用場地座標（公尺）。
    ///   目標：單人打玩家；合作模式在兩位活著的玩家之間挑（近的優先，每 6～10 秒可能換人）。
    ///   走位：慢慢逼近目標，保持在這次想用的招式射程內；永遠留在場地內。
    ///   出招：依距離挑招（近：地裂、橫掃；遠：熔岩彈、落石雨），蓄力時間就是預警時間（畫面有紅圈／紅線提示）。
    ///   狂暴：血量低於一半 → 蓄力變快（chargeScale 0.7）、走路變快、出招間隔變短。
    /// </summary>
    public class BossAI
    {
        public float walkSpeed = 0.55f;
        float thinkScale = 1f, baseCharge = 1f;

        /// <summary>難度：0 簡單、1 普通、2 困難（蓄力＝預警時間越長越好躲、出招間隔越長）</summary>
        public void SetDifficulty(int d)
        {
            thinkScale = d == 0 ? 1.6f : d == 1 ? 1.25f : 1f;
            baseCharge = d == 0 ? 1.35f : d == 1 ? 1.15f : 1f;
            walkSpeed = d == 0 ? 0.4f : d == 1 ? 0.5f : 0.55f;
            me.chargeScale = baseCharge;
        }
        public string Status { get; private set; } = "";
        public bool Enraged { get; private set; }
        float walkBase = -1f;

        /// <summary>無盡模式：Boss 重新出現（不再狂暴、清掉冷卻；越後面的 Boss 蓄力越快）</summary>
        public void Respawn(float chargeMul)
        {
            if (walkBase < 0f) walkBase = walkSpeed;
            Enraged = false; walkSpeed = walkBase;
            me.chargeScale = baseCharge * chargeMul;
            nextThink = b.now + 2.5f;
            me.target = null;
        }
        public event Action OnEnrage;

        readonly Battle b;
        readonly Fighter me;
        readonly Func<Vector3, bool> inside;
        readonly Func<Vector3, float> edgeDistance;
        readonly System.Random rnd;
        float nextThink = 3f, nextRetarget;

        public BossAI(Battle battle, Fighter boss, Func<Vector3, bool> insideArea, Func<Vector3, float> distanceToEdge, int seed)
        {
            b = battle; me = boss; inside = insideArea; edgeDistance = distanceToEdge;
            rnd = new System.Random(seed);
        }

        float Rand(float a, float c) => a + (float)rnd.NextDouble() * (c - a);
        bool Ready(SkillDef s) => !me.cooldownUntil.TryGetValue(s.id, out var cd) || cd <= b.now;

        public void Update(float dt)
        {
            if (b.Over || !me.Alive) return;
            if (!Enraged && me.hp < me.maxHp * 0.5f)
            {
                if (walkBase < 0f) walkBase = walkSpeed;
                Enraged = true; me.chargeScale *= 0.7f; walkSpeed = walkBase * 1.45f;
                OnEnrage?.Invoke();
            }
            PickTarget();
            var tgt = me.target ?? b.player;
            me.forward = Fighter.Flat(tgt.head - me.head);
            if (me.snaredUntil > b.now) { Status = "被定身"; return; }
            Move(dt, tgt);

            if (me.charging != null) { TryRelease(tgt); return; }
            if (b.now < nextThink) return;
            nextThink = b.now + (Enraged ? Rand(1.8f, 3f) : Rand(2.8f, 4.2f)) * thinkScale;

            // 依距離挑招：近身多用地裂／橫掃，遠距離用熔岩彈／落石雨
            float d = Battle.FlatDistance(me.Feet, tgt.Feet);
            var options = new List<SkillDef>();
            foreach (var s in me.loadout)
            {
                if (!Ready(s)) continue;
                float w = s.id switch
                {
                    "b_slam" => d < 3.6f ? 4f : 0f,
                    "b_sweep" => d < 5.5f ? 3f : 0.3f,
                    "b_bolt" => d > 2.5f ? 3f : 1f,
                    "b_rain" => 2f,
                    "b_summon" => b.minions.Count < 2 ? 5f : 0f,   // 小怪少於 2 隻就常召喚
                    _ => 1f,
                };
                for (int i = 0; i < Mathf.RoundToInt(w * 2f); i++) options.Add(s);
            }
            if (options.Count == 0) return;
            var pick = options[rnd.Next(options.Count)];
            if (b.TryChant(me, pick, out _)) Status = $"準備 {pick.name}";
        }

        /// <summary>挑目標：只有一位玩家就打他；兩位時打近的，偶爾換人（被打很痛的也會被盯上）</summary>
        void PickTarget()
        {
            var cands = new List<Fighter>();
            if (b.player.Alive) cands.Add(b.player);
            if (b.ally != null && b.ally.Alive) cands.Add(b.ally);
            if (cands.Count == 0) { me.target = null; return; }
            bool keep = me.target != null && me.target.Alive && cands.Contains(me.target) && b.now < nextRetarget;
            if (keep || me.charging != null) return;
            nextRetarget = b.now + Rand(6f, 10f);
            if (cands.Count == 1) { me.target = cands[0]; return; }
            float d0 = Battle.FlatDistance(me.Feet, cands[0].Feet), d1 = Battle.FlatDistance(me.Feet, cands[1].Feet);
            // 近的機率高一點
            float p0 = d1 / Mathf.Max(0.1f, d0 + d1);
            me.target = rnd.NextDouble() < p0 ? cands[0] : cands[1];
        }

        void TryRelease(Fighter tgt)
        {
            var s = me.charging;
            if (b.now - me.chargeStart < s.charge * me.chargeScale) return;
            // 射程外：繼續蓄著、走過去（地裂除外：時間到就砸，玩家已經走出紅圈就打空）
            if (s.type == SkillType.Projectile && s.areaRadius <= 0f && Battle.FlatDistance(me.Feet, tgt.Feet) > s.rangeMax) { Status = $"{s.name}：逼近中"; return; }
            var hand = me.head + Fighter.Flat(me.forward) * 0.25f + Vector3.down * 0.25f;
            var dir = (tgt.Chest - hand).normalized;
            dir = Battle.RotateY(dir, Rand(-3f, 3f));
            if (b.TryRelease(me, dir, tgt.Feet, out _)) Status = "";
        }

        void Move(float dt, Fighter tgt)
        {
            var c = me.charging;
            float want = c != null ? (c.areaRadius > 0f ? 0f : c.rangeMax * 0.7f) : 3.2f;
            if (c != null && c.areaRadius > 0f) return;   // 地裂蓄力時站著不動（紅圈固定）
            var feet = me.Feet;
            var toBoss = Fighter.Flat(feet - tgt.Feet);
            if (toBoss == Vector3.zero) toBoss = Vector3.forward;
            float cur = Battle.FlatDistance(feet, tgt.Feet);
            if (cur <= want + 0.2f && cur >= Mathf.Min(want, 2.2f)) return;
            float goal = Mathf.Max(2.2f, Mathf.Min(want, cur));
            var dest = tgt.Feet + toBoss * goal;
            var delta = dest - feet; delta.y = 0;
            float dist = delta.magnitude;
            if (dist < 0.05f) return;
            var next = feet + delta / dist * Mathf.Min(dist, walkSpeed * Battle.EnemySpeedScale * dt);
            if (inside(next) && edgeDistance(next) >= 0.5f) me.head = new Vector3(next.x, me.head.y, next.z);
            else if (!inside(feet))
            {
                var home = -feet; home.y = 0;
                me.head += (home.sqrMagnitude > 1e-4f ? home.normalized : Vector3.zero) * walkSpeed * dt;
            }
        }
    }
}
