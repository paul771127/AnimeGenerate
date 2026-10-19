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
        /// <summary>Boss 招式冷卻倍率（所有 Boss 共通 0.75）、每多一位玩家的施法頻率／攻擊力加成</summary>
        public const float BaseCdMul = 0.75f, PartyCast = 0.25f, PartyAtk = 0.2f;
        public bool Enraged { get; private set; }
        /// <summary>
        /// 故事關卡的 Boss：出招更頻繁、更常用大範圍招式（橫掃要蹲、震地波要跳、地裂與落石要跑開），
        /// 讓玩家一直動起來。由 SoloBattle 設定（並把「震地波」加進 Boss 的招式）。
        /// </summary>
        public bool storyIntense;
        string Last = "";   // 上一招（樹王：藤鞭之後常接樹根波 → 蹲完馬上要跳）

        /// <summary>故事 Boss 的動作風格：哪一類招式出得多（蹲／跳／跑／召喚）</summary>
        public struct MoveProfile { public float duck, jump, run, summon; }
        /// <summary>這招屬於哪一類動作（蹲／跳／跑／召喚），在這隻 Boss 的偏好倍率</summary>
        float CategoryMul(SkillDef s)
        {
            var p = Profile;
            if (s.id.EndsWith("_summon")) return p.summon;
            if (s.duckable) return p.duck;
            if (Battle.IsQuake(s)) return p.jump;
            if (s.areaRadius > 0f || s.multi > 1 || s.fromAbove || s.atTarget) return p.run;
            return 1f;
        }

        public MoveProfile Profile => me.cls.id switch
        {
            "boss_forest" => new MoveProfile { duck = 3f, jump = 1.5f, run = 1f, summon = 0.5f },   // 腐化樹王：蹲、跳交替
            "boss_sea" => new MoveProfile { duck = 1f, jump = 0.8f, run = 3f, summon = 0.4f },      // 深海巨妖：一直換位置
            _ => new MoveProfile { duck = 1f, jump = 3f, run = 2f, summon = 0.3f },                 // 熔岩巨像：跳＋跑
        };
        public const float StoryThinkMul = 0.55f, StoryCdMul = 0.7f;
        float walkBase = -1f;

        /// <summary>無盡模式：Boss 重新出現（不再狂暴、清掉冷卻；越後面的 Boss 蓄力越快）</summary>
        public void Respawn(float chargeMul)
        {
            if (walkBase < 0f) walkBase = walkSpeed;
            Enraged = false; walkSpeed = walkBase;
            me.chargeScale = baseCharge * chargeMul;
            nextThink = b.now + 2.5f;
            me.target = null;
            b.ClearThreat();   // 新的 Boss：仇恨重新算
        }
        public event Action OnEnrage;

        readonly Battle b;
        readonly Fighter me;
        readonly Func<Vector3, bool> inside;
        readonly Func<Vector3, float> edgeDistance;
        readonly System.Random rnd;
        float nextThink = 3f, nextRetarget;
        // 熔岩躍擊：蓄滿後在 LeapTime 秒內跳到落點，落地才判定傷害
        public const float LeapTime = 0.6f;
        float leapT = -1f; Vector3 leapFrom;

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
            // 人數越多：施法越頻繁（冷卻、思考間隔 ÷(1＋0.25×多的人數)）、攻擊力越高（＋20%／人）
            int extra = b.ScaleAllies;   // 副本：固定以 3 人計
            me.cdMul = BaseCdMul / (1f + PartyCast * extra) * (storyIntense ? StoryCdMul : 1f);
            me.atkMul = 1f + PartyAtk * extra;
            PickTarget();
            var tgt = me.target ?? b.player;
            me.forward = Fighter.Flat(tgt.head - me.head);
            if (me.snaredUntil > b.now) { Status = "被定身"; return; }
            if (leapT >= 0f)
            {
                // 跳躍中：沿直線飛到落點，落地＝重擊
                leapT += dt;
                float k = Mathf.Clamp01(leapT / LeapTime);
                var p = Vector3.Lerp(leapFrom, me.aimPoint, k);
                me.head = new Vector3(p.x, me.head.y, p.z);
                if (k >= 1f)
                {
                    leapT = -1f;
                    if (b.TryRelease(me, me.forward, me.Feet, out _)) Status = "";
                    me.charging = null;
                    nextThink = b.now + 0.8f * thinkScale;   // 落地後很快接近身攻擊
                }
                return;
            }
            Move(dt, tgt);

            if (me.charging != null) { TryRelease(tgt); return; }
            if (b.now < nextThink) return;
            nextThink = b.now + (Enraged ? Rand(1.2f, 2f) : Rand(2f, 3f)) * thinkScale / (1f + PartyCast * extra) * (storyIntense ? StoryThinkMul : 1f);

            // 依距離挑招：近身多用地裂／橫掃，遠距離用熔岩彈／落石雨
            float d = Battle.FlatDistance(me.Feet, tgt.Feet);
            var options = new List<SkillDef>();
            foreach (var s in me.loadout)
            {
                // 故事 Boss 偏好的動作：冷卻也跟著縮短（偏好 ×3 → 冷卻剩 1/3）
                if (storyIntense && me.cooldownUntil.TryGetValue(s.id, out var cdu) && cdu > b.now)
                {
                    float m = CategoryMul(s);
                    if (m > 1f && cdu - b.now <= s.cooldown * me.cdMul * (1f - 1f / m)) me.cooldownUntil[s.id] = b.now;
                }
                if (!Ready(s)) continue;
                float w = s.id switch
                {
                    "b_slam" => d < 3.6f ? 6f : 0f,
                    "b_sweep" => d < 5.5f ? 3.5f : 0.3f,
                    "b_bolt" => d > 2.5f ? 2.5f : 1f,
                    "b_rain" => 3.5f,
                    "b_summon" => b.minions.Count < 2 ? 5f : 0f,   // 小怪少於 2 隻就常召喚
                    "b_leap" => d > 4f ? 5f : 0f,                   // 離得遠：跳過去
                    // 腐化樹王：近（踐踏、藤鞭）／遠（荊棘、毒孢彈、樹根爆發）
                    "p_stomp" => d < 3.4f ? 6f : 0f,
                    "p_whip" => d < 5.5f ? 3f : 0.3f,
                    "p_thorn" => d > 2.5f ? 3f : 1f,
                    "p_spore" => d > 3f ? 2.5f : 0.5f,
                    "p_roots" => d > 3f ? 4.5f : 1.5f,
                    "p_summon" => b.minions.Count < 2 ? 5f : 0f,
                    // 深淵海妖：觸手（任何距離）、巨浪／泡泡（遠）、漩渦、墨汁
                    "s_tentacle" => d < 6f ? 5f : 3f,
                    "s_wave" => d > 3f ? 3.5f : 1.5f,
                    "s_bubbles" => d > 4f ? 3f : 1f,
                    "s_whirl" => d > 2f ? 4f : 1.2f,
                    "s_ink" => 1.5f,
                    "s_summon" => b.minions.Count < 2 ? 5f : 0f,
                    // 跳躍招：任何距離都會用；熔岩巨像最常用、樹根波接在藤鞭之後、潮汐環偶爾
                    "x_quake" => 6f,
                    "x_rootwave" => Last == "p_whip" ? 9f : 2.5f,
                    "x_ripple" => 2f,
                    _ => 1f,
                };
                // 故事 Boss：大範圍、逼人動的招式更常出（橫掃→蹲、地裂／落石／觸手→跑、震地波→跳）；召喚少一點
                if (storyIntense && w > 0f) w *= CategoryMul(s);
                for (int i = 0; i < Mathf.RoundToInt(w * 2f); i++) options.Add(s);
            }
            if (options.Count == 0) return;
            var pick = options[rnd.Next(options.Count)];
            if (b.TryChant(me, pick, out _))
            {
                Last = pick.id;
                Status = $"準備 {pick.name}";
                if (pick.id == "b_leap" || pick.atTarget) { var at = tgt.Feet; at.y = 0f; me.aimPoint = at; }   // 落點／樹根爆發位置＝目標現在的位置
            }
        }

        /// <summary>
        /// 挑目標（仇恨值）：只有一位玩家就打他；多人時打「仇恨分數」最高的人。
        ///   分數＝仇恨值（對 Boss 的傷害＋治療量）＋ 基礎 15 ＋ 距離加成（越近越高，最多 +25）。
        ///   換目標要明顯超過現在的目標（×1.2）才換，蓄力中不換（避免來回跳）。
        /// </summary>
        void PickTarget()
        {
            var cands = new List<Fighter>();
            if (b.player.Alive) cands.Add(b.player);
            foreach (var a in b.allies) if (a.Alive) cands.Add(a);
            if (cands.Count == 0) { me.target = null; return; }
            if (cands.Count == 1) { me.target = cands[0]; return; }
            Fighter best = null; float bestS = float.MinValue;
            foreach (var c in cands) { float s = ThreatScore(c); if (s > bestS) { bestS = s; best = c; } }
            if (me.target == null || !me.target.Alive || !cands.Contains(me.target)) { me.target = best; return; }
            if (me.charging != null || best == me.target) return;
            if (bestS > ThreatScore(me.target) * 1.2f) { me.target = best; Status = $"轉向 {best.name}（仇恨最高）"; }
        }

        float ThreatScore(Fighter f) => b.Threat(f) + 15f + 25f / (1f + Battle.FlatDistance(me.Feet, f.Feet));

        void TryRelease(Fighter tgt)
        {
            var s = me.charging;
            if (b.now - me.chargeStart < s.charge * me.chargeScale) return;
            if (Battle.IsQuake(s)) { b.SpawnShockwave(me, s); Status = $"{s.name}！"; return; }
            if (s.id == "b_leap") { leapT = 0f; leapFrom = me.Feet; me.forward = Fighter.Flat(me.aimPoint - me.Feet); Status = $"{s.name}！"; return; }
            // 射程外：繼續蓄著、走過去（地裂除外：時間到就砸，玩家已經走出紅圈就打空）
            if (s.type == SkillType.Projectile && s.areaRadius <= 0f && Battle.FlatDistance(me.Feet, tgt.Feet) > s.rangeMax) { Status = $"{s.name}：逼近中"; return; }
            var hand = me.head + Fighter.Flat(me.forward) * 0.25f + Vector3.down * 0.25f;
            var dir = (tgt.Chest - hand).normalized;
            dir = Battle.RotateY(dir, Rand(-3f, 3f));
            if (b.TryRelease(me, dir, tgt.Feet, out _)) Status = "";
        }

        void Move(float dt, Fighter tgt)
        {
            // 從場地外淡入出現：先走進場地（往場地中心，走到離邊界夠遠才開始正常走位）
            if (!inside(me.Feet) || edgeDistance(me.Feet) < 0.6f) { var home = -me.Feet; home.y = 0; me.head += (home.sqrMagnitude > 1e-4f ? home.normalized : Vector3.zero) * walkSpeed * 1.5f * dt; return; }
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
                me.head += (home.sqrMagnitude > 1e-4f ? home.normalized : Vector3.zero) * walkSpeed * (b.endless ? 2f : 1f) * dt;   // 無盡／故事：場地外 2 倍速走進來
            }
        }
    }
}
