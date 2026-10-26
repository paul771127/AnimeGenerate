using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 世界 Boss（大地圖的大型討伐：晨鐘熔爐泰坦、記名林冠龍、白帆淵鯨）的 AI：三個固定階段、固定出招順序（有跡可循）。
    ///   第 1 階段（100%～70%）站樁：待在場地中間不動，照順序出招：貫穿衝刺 → 巨臂橫掃 → 貫穿衝刺 → 震地環，每 3 秒一招，預警 2 秒。
    ///   第 2 階段（70%～35%）落地：先放一次「階段爆發」（全場衝擊環，預警 3 秒，跳起來躲），之後會走向你（保持約 4.5m），攻擊 ×1.3，
    ///       順序：橫掃 → 震地環 → 衝刺 → 雷印落擊（落在你放招時站的位置，走開），每 2.2 秒一招、預警 1.8 秒。
    ///   第 3 階段（35% 以下）狂暴：再一次階段爆發，之後不停繞著你走、攻擊 ×1.6，順序：衝刺 → 橫掃 → 雷印 → 震地環 → 衝刺 → 橫掃，每 1.4 秒一招、預警 1.5 秒。
    /// 招式：w_lance（側步）、w_sweep（蹲）、w_quake／w_transition（跳）、w_scatter（走出落點）。全部用場地座標。
    /// </summary>
    public class WorldBossAI
    {
        public const float Phase2At = 0.7f, Phase3At = 0.35f;
        static readonly string[] P1 = { "w_lance", "w_sweep", "w_lance", "w_quake" };
        static readonly string[] P2 = { "w_sweep", "w_quake", "w_lance", "w_scatter" };
        static readonly string[] P3 = { "w_lance", "w_sweep", "w_scatter", "w_quake", "w_lance", "w_sweep" };
        public static string PhaseName(int p) => p == 1 ? "站樁" : p == 2 ? "落地" : "狂暴";
        /// <summary>每個階段的出招順序（HUD 顯示「下一招」用）</summary>
        public static string[] Pattern(int p) => p == 1 ? P1 : p == 2 ? P2 : P3;

        public int Phase { get; private set; } = 1;
        public string Status { get; private set; } = "";
        public event Action<int> OnPhase;
        /// <summary>下一招的技能 id（預告）</summary>
        public string Next => Pattern(Phase)[step % Pattern(Phase).Length];

        readonly Battle b; readonly Fighter me;
        readonly Func<Vector3, bool> inside; readonly Func<Vector3, float> edgeDistance;
        int step; float nextAt; float orbitDir = 1f, orbitFlipAt;
        float Interval => Phase == 1 ? 3f : Phase == 2 ? 2.2f : 1.4f;
        float ChargeScale => Phase == 1 ? 1.25f : Phase == 2 ? 1.1f : 0.9f;   // 招式的 charge（預警時間）倍率
        float Walk => Phase == 1 ? 0f : Phase == 2 ? 0.5f : 0.85f;
        float Atk => Phase == 1 ? 1f : Phase == 2 ? 1.3f : 1.6f;
        public const float Keep2 = 4.5f, Keep3 = 3.2f, Leash = 2.8f;

        public WorldBossAI(Battle battle, Fighter boss, Func<Vector3, bool> insideArea, Func<Vector3, float> distanceToEdge)
        {
            b = battle; me = boss; inside = insideArea; edgeDistance = distanceToEdge;
            nextAt = battle.now + 3f;
            me.chargeScale = ChargeScale;
        }

        void EnterPhase(int p)
        {
            Phase = p; step = 0;
            me.charging = null; me.chargeScale = ChargeScale;
            // 換階段：階段爆發（全場衝擊環，預警 3 秒）
            var tr = Skills.All["w_transition"];
            me.cooldownUntil[tr.id] = b.now;
            if (b.TryChant(me, tr, out _)) Status = "階段爆發！";
            nextAt = b.now + tr.charge * me.chargeScale + 1.5f;
            OnPhase?.Invoke(p);
        }

        public void Update(float dt)
        {
            if (b.Over || !me.Alive) return;
            if (Phase == 1 && me.hp < me.maxHp * Phase2At) EnterPhase(2);
            if (Phase == 2 && me.hp < me.maxHp * Phase3At) EnterPhase(3);
            me.atkMul = Atk * b.BossDmgMul;
            me.cdMul = 1f;
            var tgt = b.player;
            if (!tgt.Alive) return;
            me.target = tgt;
            var c = me.charging;
            // 面向：蓄力中鎖住（預警線不跟著轉），除了雷印（落點已經定了）
            if (c == null || c.id == "w_scatter" || Phase >= 3) me.forward = Fighter.Flat(tgt.head - me.head);
            if (me.snaredUntil > b.now) { Status = "被定身"; return; }
            // 走位：第 1 階段站樁；第 2 階段走向你保持距離；第 3 階段一直繞著你走（蓄力震地環時停下）
            bool quaking = c != null && Battle.IsQuake(c);
            if (Walk > 0f && !quaking && (c == null || Phase >= 3)) Move(dt, tgt);
            if (c != null) { TryRelease(tgt); return; }
            if (b.now < nextAt) return;
            // 照順序出下一招
            var pat = Pattern(Phase);
            var s = Skills.All[pat[step % pat.Length]];
            me.cooldownUntil[s.id] = b.now;
            if (b.TryChant(me, s, out _))
            {
                step++;
                Status = $"準備 {s.name}";
                if (s.id == "w_scatter") { var at = tgt.Feet; at.y = 0f; me.aimPoint = at; }   // 雷印：落在你現在站的位置
            }
            else nextAt = b.now + 0.5f;
        }

        void TryRelease(Fighter tgt)
        {
            var s = me.charging;
            if (b.now - me.chargeStart < s.charge * me.chargeScale) return;
            if (Battle.IsQuake(s)) { b.SpawnShockwave(me, s); Status = $"{s.name}！"; }
            else if (s.id == "w_scatter") { if (b.TryRelease(me, Vector3.down, me.aimPoint, out _, false)) Status = $"{s.name}！"; }
            else
            {
                var hand = me.head + Fighter.Flat(me.forward) * 0.25f + Vector3.down * 0.25f;
                var dir = s.originHeight > 0f ? Fighter.Flat(tgt.Feet - me.Feet) : (tgt.Chest - hand).normalized;
                if (b.TryRelease(me, dir, tgt.Feet, out _)) Status = $"{s.name}！";
            }
            me.charging = null;
            nextAt = b.now + Interval;
        }

        void Move(float dt, Fighter tgt)
        {
            var feet = me.Feet; feet.y = 0f;
            float keep = Phase >= 3 ? Keep3 : Keep2;
            var toBoss = Fighter.Flat(feet - tgt.Feet); if (toBoss == Vector3.zero) toBoss = Vector3.forward;
            float cur = Battle.FlatDistance(feet, tgt.Feet);
            Vector3 dest;
            if (Phase >= 3)
            {
                // 繞著你走：先把距離拉到 keep，再往側邊繞（每 3～5 秒換方向）
                if (b.now >= orbitFlipAt) { orbitDir = -orbitDir; orbitFlipAt = b.now + 3f + (float)(me.hp % 2f); }
                var side = Vector3.Cross(Vector3.up, toBoss).normalized * orbitDir;
                var radial = toBoss * (keep - cur);   // 太近往外、太遠往內
                dest = feet + side * 1.5f + radial;
            }
            else dest = tgt.Feet + toBoss * keep;   // 第 2 階段：保持距離面對你
            dest.y = 0f;
            if (dest.magnitude > Leash) dest = dest.normalized * Leash;   // 不離開場地中間太遠
            var delta = dest - feet; float dist = delta.magnitude;
            if (dist < 0.05f) return;
            var next = feet + delta / dist * Mathf.Min(dist, Walk * dt);
            if (inside(next) && edgeDistance(next) >= 0.8f) me.head = new Vector3(next.x, me.head.y, next.z);
            else orbitFlipAt = b.now;   // 撞到邊：換個方向繞
        }
    }
}
