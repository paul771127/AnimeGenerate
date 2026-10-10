using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 戰鬥核心（純邏輯，不碰畫面）：玩家與敵人用完全相同的規則。
    /// 所有座標都是「場地座標」（公尺）：原點＝場地中心，地板 y = 0，Y 朝上。
    /// 因為不依賴 Unity 的原生功能，可以在編輯器外直接模擬測試。
    /// </summary>
    public class Fighter
    {
        public const float BodyRadius = 0.28f;

        public string name;
        public ClassDef cls;
        public List<SkillDef> loadout = new List<SkillDef>();
        public float hp, mp, regen;
        public int maxHp, maxMp;
        public Vector3 head;                  // 頭（玩家＝手機）的位置
        public Vector3 forward = Vector3.forward;   // 面向（水平）
        public bool isPlayer;

        public readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();
        public SkillDef charging; public float chargeStart, chargeDelay;
        public float blockUntil, blockReduce, shieldUntil, counterUntil, snaredUntil, blindUntil;
        public int shieldLeft;
        public readonly List<Dot> dots = new List<Dot>();

        public class Dot { public float dps, until, acc; }

        public Fighter(string name, ClassDef cls, IEnumerable<string> skillIds, bool isPlayer)
        {
            this.name = name; this.cls = cls; this.isPlayer = isPlayer;
            maxHp = cls.maxHp; maxMp = cls.maxMp; regen = cls.mpRegen;
            hp = maxHp; mp = maxMp;
            foreach (var id in skillIds) if (Skills.All.TryGetValue(id, out var s)) loadout.Add(s);
        }

        public bool Alive => hp > 0;
        public Vector3 Feet => new Vector3(head.x, 0f, head.z);
        public Vector3 Chest => new Vector3(head.x, Mathf.Max(0.3f, head.y - 0.45f), head.z);

        /// <summary>身體判定：從頭到地板的膠囊；玩家的身體在手機後方一點</summary>
        public void BodySegment(out Vector3 top, out Vector3 bottom)
        {
            var back = isPlayer ? -Flat(forward) * 0.12f : Vector3.zero;
            top = head + back + Vector3.up * 0.1f;
            bottom = new Vector3(head.x, 0.1f, head.z) + back;
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero; }
    }

    public class Projectile
    {
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos, dir;
        public float traveled;
        public int damage;
        public EffectKind effect; public float effectDps, effectDur;
        public bool reflected, alive = true;
        public int id;
        public bool remote;          // 雙人：對手的法術（只顯示，命中由對手判定）
    }

    public class Trap
    {
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos;          // 地板上的點
        public float armAt, until;
        public bool alive = true;
        public int id;
        public bool remote;          // 雙人：對手的陷阱（只顯示，觸發由對手判定）
        public bool Armed(float now) => now >= armAt;
    }

    public class Battle
    {
        public float now;
        public readonly Fighter player, enemy;
        public readonly List<Projectile> projectiles = new List<Projectile>();
        public readonly List<Trap> traps = new List<Trap>();
        public float chargeTimeout = 7f;
        int nextId = 1;

        /// <summary>事件（給畫面做特效與文字）：種類、位置、顏色、文字</summary>
        public event Action<string, Vector3, Color, string> OnEvent;
        void Emit(string kind, Vector3 at, Color c, string text) => OnEvent?.Invoke(kind, at, c, text);

        public Battle(Fighter player, Fighter enemy) { this.player = player; this.enemy = enemy; }

        // ------------------------------------------------------------ 雙人連線模式
        // enemy 是「對手的代理」：位置、血量由網路更新；對手的法術與陷阱只顯示。
        // 判定規則（攻擊方判定）：我的法術／陷阱打到對手 → OnRemoteHit 送給對手，由對手自己扣血與套用格擋、護盾、反擊；
        // 對手判定打到我 → ApplyRemoteHit。
        public bool remoteEnemy;
        public event Action<Projectile> OnSpawn;                 // 我方新增的法術（要送給對手）
        public event Action<Trap> OnTrapPlaced;                  // 我方新設的陷阱
        public event Action<SkillDef, int, Vector3, int> OnRemoteHit;   // 打到對手：技能、傷害、位置、法術或陷阱 id
        public event Action<int> OnTrapGone;                     // 我方陷阱消失（觸發或過期）

        /// <summary>顯示對手的法術（從對手送來的起點、方向；elapsed＝已經飛了幾秒）</summary>
        public Projectile AddRemoteProjectile(int id, SkillDef s, Vector3 from, Vector3 dir, float elapsed, bool reflected)
        {
            var p = new Projectile { owner = enemy, skill = s, pos = from, dir = dir.normalized, damage = s.damage, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur, reflected = reflected, id = id, remote = true };
            float d = s.speed * Mathf.Max(0f, elapsed);
            p.pos += p.dir * d; p.traveled = d;
            projectiles.Add(p);
            return p;
        }

        public void RemoveRemoteProjectile(int id)
        {
            foreach (var p in projectiles) if (p.remote && p.id == id) p.alive = false;
        }

        public void AddRemoteTrap(int id, SkillDef s, Vector3 at, float armIn)
        {
            at.y = 0;
            traps.Add(new Trap { owner = enemy, skill = s, pos = at, armAt = now + armIn, until = now + s.trapLife, id = id, remote = true });
        }

        public void RemoveRemoteTrap(int id)
        {
            foreach (var t in traps) if (t.remote && t.id == id) t.alive = false;
        }

        /// <summary>對手判定打到我：反擊中就把攻擊打回去（回傳 true），否則套用格擋、護盾、傷害與效果</summary>
        public bool ApplyRemoteHit(SkillDef s, int damage, Vector3 at, bool isTrap)
        {
            if (!isTrap && player.counterUntil > now)
            {
                player.counterUntil = 0;
                var from = player.Chest + Fighter.Flat(player.forward) * 0.4f;
                var dir = (enemy.Chest - from).normalized;
                var p = Spawn(player, s, from, dir, damage, s.effect, s.effectDps, s.effectDur, true);
                OnSpawn?.Invoke(p);
                Emit("counter", at, new Color(0.96f, 0.45f, 0.71f), "反擊！打回去");
                return true;
            }
            if (isTrap) Emit("trap", at, s.color, $"{s.name}！");
            ApplyDamageAndEffect(player, damage, s.effect, s.effectDps, s.effectDur, at);
            return false;
        }

        public Fighter Opponent(Fighter f) => f == player ? enemy : player;
        public static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
        public float Distance => FlatDistance(player.head, enemy.head);
        public bool Over => !player.Alive || !enemy.Alive;

        // ------------------------------------------------------------ 詠唱
        public bool TryChant(Fighter f, SkillDef s, out string why)
        {
            why = null;
            if (Over) { why = "戰鬥結束"; return false; }
            if (f.cooldownUntil.TryGetValue(s.id, out var cd) && cd > now) { why = $"{s.name} 冷卻中"; return false; }
            if (f.mp < s.cost) { why = "MP 不足"; return false; }
            if (f.snaredUntil > now) { why = "被困住，暫時不能施法"; return false; }
            f.charging = s; f.chargeStart = now;
            return true;
        }

        public float ChargeProgress(Fighter f) => f.charging == null ? 0f : Mathf.Clamp01((now - f.chargeStart) / Mathf.Max(0.01f, f.charging.charge));

        /// <summary>射程狀態：0＝可以、1＝太遠、-1＝太近</summary>
        public int RangeState(Fighter f, SkillDef s)
        {
            if (s.type != SkillType.Projectile) return 0;
            float d = FlatDistance(f.head, Opponent(f).head);
            if (d > s.rangeMax) return 1;
            if (d < s.rangeMin) return -1;
            return 0;
        }

        // ------------------------------------------------------------ 出手
        /// <param name="aimDir">投射物的方向（場地座標）</param>
        /// <param name="floorPoint">陷阱的位置（地板上）</param>
        public bool TryRelease(Fighter f, Vector3 aimDir, Vector3 floorPoint, out string why)
        {
            why = null;
            var s = f.charging;
            if (s == null || Over) { why = "沒有在詠唱"; return false; }
            if (f.mp < s.cost) { f.charging = null; why = "MP 不足"; return false; }
            if (now - f.chargeStart < s.charge) { why = $"蓄力中 {Mathf.FloorToInt(ChargeProgress(f) * 100)}%"; return false; }
            int rs = RangeState(f, s);
            if (s.releaseNear && rs > 0) { why = $"再靠近！{Distance:F1}m → ≤{s.rangeMax:0.#}m"; return false; }
            if (s.type == SkillType.Projectile && rs < 0) { why = $"太近了！要拉開到 {s.rangeMin:0.#}m 以上"; return false; }
            if (s.type == SkillType.Trap && FlatDistance(f.Feet, floorPoint) > s.trapRange)
            { why = $"陷阱只能設在 {s.trapRange:0.#}m 內"; return false; }

            f.mp -= s.cost;
            f.cooldownUntil[s.id] = now + s.cooldown;
            f.charging = null;

            switch (s.type)
            {
                case SkillType.Self: CastSelf(f, s); break;
                case SkillType.Trap: PlaceTrap(f, s, floorPoint); break;
                default:
                    var hand = f.head + Fighter.Flat(f.forward) * 0.25f + Vector3.down * 0.25f;
                    aimDir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Fighter.Flat(f.forward);
                    for (int i = 0; i < s.multi; i++)
                    {
                        float ang = s.multi > 1 ? (i - (s.multi - 1) / 2f) * s.spreadDeg : 0f;
                        Spawn(f, s, hand, RotateY(aimDir, ang), s.damage, s.effect, s.effectDps, s.effectDur, false);
                    }
                    Emit("cast", hand, s.color, null);
                    break;
            }
            return true;
        }

        void CastSelf(Fighter f, SkillDef s)
        {
            switch (s.self)
            {
                case SelfKind.Heal: f.hp = Mathf.Min(f.maxHp, f.hp + s.heal); Emit("heal", f.Chest, s.color, $"+{s.heal}"); return;
                case SelfKind.Block: f.blockUntil = now + s.buffDur; f.blockReduce = s.buffReduce; break;
                case SelfKind.Shield: f.shieldUntil = now + s.buffDur; f.shieldLeft = s.buffAmount; break;
                case SelfKind.Counter: f.counterUntil = now + s.buffDur; break;
            }
            Emit("buff", f.Chest, s.color, s.name);
        }

        void PlaceTrap(Fighter f, SkillDef s, Vector3 at)
        {
            at.y = 0;
            // 同一種陷阱最多 trapMax 個，超過就移除最舊的
            int count = 0;
            for (int i = traps.Count - 1; i >= 0; i--)
                if (traps[i].alive && traps[i].owner == f && traps[i].skill == s && ++count >= s.trapMax) traps[i].alive = false;
            var trap = new Trap { owner = f, skill = s, pos = at, armAt = now + s.trapArm, until = now + s.trapLife, id = nextId++ };
            traps.Add(trap);
            Emit("trapSet", at, s.color, $"{s.name} 設置");
            if (f == player) OnTrapPlaced?.Invoke(trap);
        }

        Projectile Spawn(Fighter owner, SkillDef s, Vector3 from, Vector3 dir, int dmg, EffectKind eff, float dps, float dur, bool reflected)
        {
            var p = new Projectile { owner = owner, skill = s, pos = from, dir = dir.normalized, damage = dmg, effect = eff, effectDps = dps, effectDur = dur, reflected = reflected, id = nextId++ };
            projectiles.Add(p);
            if (owner == player && remoteEnemy) OnSpawn?.Invoke(p);
            return p;
        }

        // ------------------------------------------------------------ 每幀
        public void Update(float dt)
        {
            now += dt;
            foreach (var f in new[] { player, enemy })
            {
                if (remoteEnemy && f == enemy) continue;   // 對手的 MP、詠唱、持續傷害由對手自己算
                f.mp = Mathf.Min(f.maxMp, f.mp + f.regen * dt);
                if (f.charging != null && now - f.chargeStart > f.charging.charge + chargeTimeout + (f.charging.releaseNear ? 8f : 0f))
                { f.charging = null; Emit("timeout", f.Chest, Color.gray, "詠唱逾時"); }
                // 持續傷害：每滿 1 秒扣一次
                for (int i = f.dots.Count - 1; i >= 0; i--)
                {
                    var d = f.dots[i];
                    d.acc += dt;
                    while (d.acc >= 1f && f.Alive) { d.acc -= 1f; Damage(f, d.dps, "dot"); }
                    if (d.until <= now) f.dots.RemoveAt(i);
                }
            }
            UpdateProjectiles(dt);
            UpdateTraps();
        }

        void UpdateProjectiles(float dt)
        {
            // 用索引走訪：反擊會在迴圈中新增「打回去的法術」，不能用 foreach（會丟例外）；新增的下一幀才開始飛
            int count = projectiles.Count;
            for (int i = 0; i < count; i++)
            {
                var p = projectiles[i];
                if (!p.alive) continue;
                var target = Opponent(p.owner);
                float step = p.skill.speed * dt;
                // 分小段前進，快的箭也不會穿過身體
                int sub = Mathf.Max(1, Mathf.CeilToInt(step / 0.08f));
                for (int k = 0; k < sub && p.alive; k++)
                {
                    p.pos += p.dir * (step / sub);
                    p.traveled += step / sub;
                    target.BodySegment(out var a, out var b);
                    if (p.remote)
                    {
                        // 對手的法術：命中由對手判定（收到結果時移除）；這裡只讓它飛，飛完消失
                        if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel) p.alive = false;
                    }
                    else if (DistancePointSegment(p.pos, a, b) < p.skill.radius + Fighter.BodyRadius)
                    {
                        p.alive = false;
                        if (remoteEnemy && target == enemy) { Emit("hit", p.pos, p.skill.color, null); OnRemoteHit?.Invoke(p.skill, p.damage, p.pos, p.id); }
                        else Hit(target, p);
                    }
                    else if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel) { p.alive = false; Emit("miss", p.pos, p.skill.color, null); }
                }
            }
            projectiles.RemoveAll(x => !x.alive);
        }

        void UpdateTraps()
        {
            foreach (var t in traps)
            {
                if (!t.alive) continue;
                if (now > t.until) { t.alive = false; if (!t.remote && remoteEnemy) OnTrapGone?.Invoke(t.id); continue; }
                if (t.remote || !t.Armed(now)) continue;   // 對手的陷阱由對手判定
                var victim = Opponent(t.owner);
                if (FlatDistance(victim.Feet, t.pos) < t.skill.radius + 0.15f)
                {
                    t.alive = false;
                    Emit("trap", t.pos, t.skill.color, $"{t.skill.name}！");
                    if (remoteEnemy && victim == enemy) { OnRemoteHit?.Invoke(t.skill, t.skill.damage, t.pos, -t.id); OnTrapGone?.Invoke(t.id); }
                    else ApplyDamageAndEffect(victim, t.skill.damage, t.skill.effect, t.skill.effectDps, t.skill.effectDur, t.pos);
                }
            }
            traps.RemoveAll(x => !x.alive);
        }

        // ------------------------------------------------------------ 命中：反擊 → 格擋 → 護盾 → 傷害 → 附加效果
        void Hit(Fighter target, Projectile p)
        {
            if (target.counterUntil > now)
            {
                target.counterUntil = 0;
                // 把攻擊原樣打回去，朝施法者目前的胸口飛（施法者移動就能閃開）
                var from = target.Chest + Fighter.Flat(target.forward) * 0.4f;
                var dir = (p.owner.Chest - from).normalized;
                Spawn(target, p.skill, from, dir, p.damage, p.effect, p.effectDps, p.effectDur, true);
                Emit("counter", p.pos, new Color(0.96f, 0.45f, 0.71f), "反擊！打回去");
                return;
            }
            ApplyDamageAndEffect(target, p.damage, p.effect, p.effectDps, p.effectDur, p.pos);
        }

        void ApplyDamageAndEffect(Fighter target, int damage, EffectKind eff, float dps, float dur, Vector3 at)
        {
            float dmg = damage;
            string note = null;
            if (target.blockUntil > now && dmg > 0) { dmg = Mathf.Round(dmg * (1f - target.blockReduce)); target.blockUntil = 0; note = "格擋"; }
            if (target.shieldUntil > now && target.shieldLeft > 0 && dmg > 0)
            {
                int absorb = Mathf.Min(target.shieldLeft, Mathf.RoundToInt(dmg));
                target.shieldLeft -= absorb; dmg -= absorb;
                if (target.shieldLeft <= 0) target.shieldUntil = 0;
                note = note == null ? "護盾吸收" : note + "＋護盾";
            }
            if (note != null) Emit("mitigate", at, new Color(0.58f, 0.77f, 0.99f), note);
            switch (eff)
            {
                case EffectKind.Dot: target.dots.Add(new Fighter.Dot { dps = dps, until = now + dur }); Emit("effect", at, Color.green, "中毒"); break;
                case EffectKind.Blind: target.blindUntil = now + dur; Emit("effect", at, Color.gray, "致盲"); break;
                case EffectKind.Snare: target.snaredUntil = now + dur; target.charging = null; Emit("effect", at, new Color(1f, 0.6f, 0.2f), "定身"); break;
            }
            if (dmg > 0 || note == null) Damage(target, dmg, "hit", at);
        }

        void Damage(Fighter f, float dmg, string kind, Vector3? at = null)
        {
            if (!f.Alive) return;
            f.hp = Mathf.Max(0, f.hp - dmg);
            Emit(kind == "dot" ? "dot" : (f.isPlayer ? "hurt" : "hit"), at ?? f.Chest, Color.red, dmg > 0 ? $"-{dmg:0}" : "");
            if (!f.Alive) Emit(f.isPlayer ? "lose" : "win", f.Chest, Color.white, null);
        }

        // ------------------------------------------------------------ 小工具
        public static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>繞 Y 軸旋轉（不用 Quaternion，讓純邏輯可在 Unity 外測試）</summary>
        public static Vector3 RotateY(Vector3 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
        }
    }
}
