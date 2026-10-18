using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 電腦敵人：在場地內走動、照玩家同樣的規則施法。全部用場地座標（公尺）。
    ///   走位：依職業保持距離（刺客/劍士近、法師/獵人遠），並左右繞圈；
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
        public float delayScale = 1f;      // 蓄滿後多等一下才放（難度越低越慢）

        /// <summary>難度：0 簡單、1 普通、2 困難（原本的強度）</summary>
        public void SetDifficulty(int d)
        {
            switch (d)
            {
                case 0: thinkMin = 4.5f; thinkMax = 7f; aimErrorDeg = 12f; guardChance = 0.15f; walkSpeed = 0.6f; delayScale = 2.2f; break;
                case 1: thinkMin = 3.4f; thinkMax = 5.5f; aimErrorDeg = 7f; guardChance = 0.3f; walkSpeed = 0.8f; delayScale = 1.5f; break;
                default: thinkMin = 2.5f; thinkMax = 4.5f; aimErrorDeg = 4f; guardChance = 0.5f; walkSpeed = 1.0f; delayScale = 1f; break;
            }
        }
        public string Status { get; private set; } = "";

        readonly Battle b;
        readonly Fighter me;
        readonly Func<Vector3, bool> inside;
        readonly Func<Vector3, float> edgeDistance;
        readonly System.Random rnd;
        float nextThink, strafePhase;
        int lastSeenProjectile;

        static readonly Dictionary<string, float> PreferredDistance = new Dictionary<string, float>
        { { "assassin", 2.2f }, { "swordsman", 1.9f }, { "mage", 6f }, { "archer", 8f }, { "priest", 6.5f } };   // 場地太小時 FindInside 會自動縮短

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
            if (me.snaredUntil > b.now) { Status = "被定身"; return; }   // 定身：站著不動、不能施法
            Move(dt);

            if (me.charging != null) { TryRelease(); return; }

            // 看到玩家的新攻擊：一半機率立刻開防禦
            foreach (var p in b.projectiles)
            {
                if (p.owner != player || p.id <= lastSeenProjectile) continue;
                lastSeenProjectile = p.id;
                var guard = me.loadout.Find(s => s.type == SkillType.Self && (s.self == SelfKind.Block || s.self == SelfKind.Shield || s.self == SelfKind.Counter || s.self == SelfKind.Armor || s.self == SelfKind.Taunt) && Ready(s));
                if (guard != null && rnd.NextDouble() < guardChance && b.TryChant(me, guard, out _))
                {
                    me.chargeDelay = Rand(0f, 0.12f);
                    Status = $"詠唱 {guard.name}";
                    return;
                }
            }

            // 普攻：沒在詠唱時，射程內就打（不耗 MP、有自己的冷卻）；MP 不夠放技能時主要靠普攻
            var basic = me.basic ?? Skills.BasicOf(me.cls.id);
            if (basic != null && b.now >= nextBasic && player.Alive && b.RangeState(me, basic) == 0)
            {
                var bh = me.head + Fighter.Flat(me.forward) * 0.25f + Vector3.down * 0.25f;
                var bd = Battle.RotateY((player.Chest - bh).normalized, Rand(-aimErrorDeg, aimErrorDeg));
                if (b.BasicAttack(me, basic, bd, player.Feet, true))
                {
                    nextBasic = b.now + Rand(0.5f, 1.2f) * delayScale;
                    if (!Skills.IsMeleeBasic(basic)) shotPendingUntil = b.now + Battle.ShotWindup + 0.05f;
                }
            }

            if (b.now < shotPendingUntil) return;   // 遠程普攻預警中：等射出去再唸技能（不然這一發會被取消）
            if (b.now < nextThink) return;
            nextThink = b.now + Rand(thinkMin, thinkMax);

            SkillDef pick = null;
            if (me.hp < me.maxHp * 0.4f) pick = me.loadout.Find(s => s.self == SelfKind.Heal && Ready(s));
            // 法師：玩家在 1.5m 內蓄力或伏擊 → 抗拒法環打斷
            if (pick == null && (b.player.charging != null || b.player.armed != null) && Battle.FlatDistance(me.Feet, b.player.Feet) < 1.6f)
                pick = me.loadout.Find(s => s.self == SelfKind.Dispel && Ready(s));
            // 牧師：中毒、定身、致盲 → 淨化；兩個以上技能在冷卻 → 祈禱
            if (pick == null && (me.dots.Count > 0 || me.blindUntil > b.now)) pick = me.loadout.Find(s => s.self == SelfKind.Cleanse && Ready(s));
            // 法師：MP 低 → 冥想；獵人：精神集中；刺客：離玩家遠時先刺殺準備
            if (pick == null && me.mp < me.maxMp * 0.35f) pick = me.loadout.Find(s => s.self == SelfKind.Mana && Ready(s));
            if (pick == null && rnd.NextDouble() < 0.3) pick = me.loadout.Find(s => s.self == SelfKind.Focus && Ready(s));
            if (pick == null && me.prepUntil <= b.now && Battle.FlatDistance(me.Feet, b.player.Feet) > 3f && rnd.NextDouble() < 0.4)
                pick = me.loadout.Find(s => s.self == SelfKind.Prep && Ready(s));
            if (pick == null)
            {
                int cooling = 0; foreach (var kv in me.cooldownUntil) if (kv.Value > b.now + 2f) cooling++;
                if (cooling >= 2) pick = me.loadout.Find(s => s.self == SelfKind.Prayer && Ready(s));
            }
            if (pick == null)
            {
                var attacks = me.loadout.FindAll(s => s.type != SkillType.Self && Ready(s));
                var inRange = attacks.FindAll(s => s.type == SkillType.Trap ? Battle.FlatDistance(me.Feet, b.player.Feet) <= s.trapRange * 0.9f : b.RangeState(me, s) == 0);
                var list = inRange.Count > 0 && rnd.NextDouble() < 0.85 ? inRange : attacks;   // 偶爾選射程外的，會走過去再放
                list = list.FindAll(s => s.stillRadius <= 0f || inRange.Contains(s));            // 要站著蓄力的（獵人）：蓄力中不能走，只選現在就打得到的
                if (list.Count > 0) pick = list[rnd.Next(list.Count)];
            }
            if (pick != null && b.TryChant(me, pick, out _))
            {
                me.chargeDelay = Rand(0.2f, 0.9f) * delayScale;
                Status = $"詠唱 {pick.name}";
            }
        }

        float nextBasic = 1.5f, shotPendingUntil;
        bool Ready(SkillDef s) => (!me.cooldownUntil.TryGetValue(s.id, out var cd) || cd <= b.now) && me.mp >= s.cost;

        /// <summary>和 Battle 的規則一樣：刺殺準備蓄滿前、獵人整個拉弓期間（遊俠除外）離開原地就失敗</summary>
        bool MustStandStill => me.charging != null && me.charging.stillRadius > 0f && !(me.adv == "ranger" && me.charging.cls == "archer")
            && (b.ChargeProgress(me) < 1f || me.charging.cls == "archer");

        void TryRelease()
        {
            var s = me.charging;
            if (b.now - me.chargeStart < s.charge + me.chargeDelay) return;
            // 蓄滿後站著不能動又一直打不到（玩家跑出射程）：放棄這一招，重新選
            if (MustStandStill && b.now - me.chargeStart > s.charge + me.chargeDelay + 2.5f) { me.charging = null; Status = ""; return; }
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
            // 從場地外淡入出現：先走進場地（往場地中心，走到離邊界夠遠才開始正常走位）
            if (!inside(me.Feet) || edgeDistance(me.Feet) < 0.4f) { var home = -me.Feet; home.y = 0; me.head += (home.sqrMagnitude > 1e-4f ? home.normalized : Vector3.zero) * walkSpeed * 1.5f * dt; return; }
            if (MustStandStill || me.windupUntil > b.now) return;   // 普攻預警中也站定   // 刺殺準備蓄力中、獵人拉弓中：站著不動
            var player = b.player;
            var toMe = Fighter.Flat(me.head - player.head);
            if (toMe == Vector3.zero) toMe = Vector3.forward;
            float cur = Battle.FlatDistance(me.head, player.head);

            // 想站的距離：蓄力中的技能決定（打不到就調整）；否則依職業習慣
            float want = PreferredDistance.TryGetValue(me.cls.id, out var pref) ? pref : 3f;
            var c = me.charging;
            // 近戰普攻（劍士、刺客）：沒在詠唱時貼近到普攻射程內
            var nb = me.basic ?? Skills.BasicOf(me.cls.id);
            if (c == null && nb != null && Skills.IsMeleeBasic(nb)) want = Mathf.Min(want, nb.rangeMax * 0.8f);
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
