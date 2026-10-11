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
        // 刺客的近身技能：放招後進入「伏擊」，時限內靠近到射程內就自動出手
        public SkillDef armed; public float armedUntil;
        // 身體閃避：蹲下（躲橫向攻擊）、快速側跨（短暫無敵）
        public bool crouching; public float dodgeUntil;
        // 體型（Boss 比較大）、蓄力倍率（Boss 狂暴時變快）、目前攻擊的目標（合作模式 Boss 會換目標）
        public float bodyRadius = BodyRadius, chargeScale = 1f;
        public Fighter target;
        public float damageTakenMul = 1f;   // 受到的傷害倍率（單人模式的電腦難度：簡單／普通時玩家受傷較少）
        // Boss 召喚的小怪：編號、攻擊狀態（預備到 windupUntil 出手，attackReadyAt 之後才能再攻擊）
        public bool isMinion; public int minionId; public float windupUntil, attackReadyAt;
        public int minionKind;                 // 0 近戰小鬼、1 火焰射手、2 岩石衝鋒獸、3 自爆小鬼
        public float dmgMul = 1f, speedMul = 1f;   // 無盡模式：越後面的波次越強
        // 無盡模式的玩家升級：攻擊力、冷卻、爆擊率、攻擊距離、命中時附加灼燒／中毒／冰凍的機率
        public float atkMul = 1f, cdMul = 1f, critChance, rangeMul = 1f, burnChance, poisonChance, freezeChance;
        // 牧師的守護：時限內受到的直接傷害按比例減少
        public float armorUntil, armorReduce;
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
        public Vector3 Chest => new Vector3(head.x, Mathf.Max(0.3f, bodyRadius > 0.5f ? head.y * 0.6f : head.y - 0.45f), head.z);   // Boss 的胸口在身高 60% 處

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
        public bool evaded;             // 已經被閃過（只提示一次，法術繼續飛）
        public Fighter target;          // 這一發要打誰（合作模式 Boss 會打隊友）；null＝對手
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos, dir;
        public float traveled;
        public int damage;
        public EffectKind effect; public float effectDps, effectDur;
        public bool reflected, alive = true;
        public bool crit;               // 爆擊（傷害 ×2）
        public float rangeMul = 1f;     // 飛行距離倍率（無盡模式升級）
        public int id;
        public bool remote;          // 雙人：對手的法術（只顯示，命中由對手判定）
        // 弧線飛行（風刃）：沿直線前進的「基準點」＋往側邊的偏移 sin(π×進度)
        public Vector3 basePos, curveSide;   // curveSide：側向單位向量 × 振幅（公尺，可為負）
        public float curveAmp, pathLen;
        public bool Curved => pathLen > 0.01f && Mathf.Abs(curveAmp) > 0.01f;
    }

    /// <summary>隕石落地後的火海：範圍內的敵人每秒受到灼燒傷害</summary>
    public class BurnZone
    {
        public Fighter owner;
        public SkillDef skill;
        public Vector3 pos;            // 地板上的圓心
        public float radius, until, dps, acc;
        public bool remote;            // 雙人：對手的火海（只顯示，傷害由對手判定）
        public bool alive = true;
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
        public readonly List<BurnZone> burns = new List<BurnZone>();
        public const float MeteorDropHeight = 6f, BurnRadius = 1.5f, BurnDuration = 10f, BurnDps = 6.5f;
        readonly System.Random rng = new System.Random();
        public float chargeTimeout = 7f;
        public const float AmbushWindow = 5f;   // 伏擊狀態持續秒數
        int nextId = 1;

        /// <summary>事件（給畫面做特效與文字）：種類、位置、顏色、文字</summary>
        public event Action<string, Vector3, Color, string> OnEvent;
        void Emit(string kind, Vector3 at, Color c, string text) => OnEvent?.Invoke(kind, at, c, text);

        public Battle(Fighter player, Fighter enemy) { this.player = player; this.enemy = enemy; }

        // ------------------------------------------------------------ 合作模式（兩人打 Boss）
        // ally＝另一位玩家的代理（位置、血量、蹲下／閃避由網路更新）。
        // 主機（coopHost）跑 Boss：Boss 打到隊友 → OnAllyHit 送給隊友，由隊友自己扣血；隊友打 Boss → ApplyHitOnEnemy。
        // 隊友端：Boss 是 remoteEnemy（位置、血量由主機送來），Boss 與主機的法術都只顯示。
        public Fighter ally;
        public bool coopHost;
        public event Action<SkillDef, int, Vector3, int> OnAllyHit;      // Boss 打到隊友：技能、傷害、位置、法術 id
        public event Action<Vector3, float> OnDispel;                    // 我放了抗拒法環（中心、半徑）：要告訴另一支手機
        IEnumerable<Fighter> Players() { yield return player; if (ally != null) yield return ally; }

        /// <summary>隊友（或主機）判定打到 Boss：套用到 Boss 身上（主機端）</summary>
        public void ApplyHitOnEnemy(SkillDef s, int damage, Vector3 at, bool isTrap)
        {
            if (isTrap) Emit("trap", at, s.color, $"{s.name}！");
            ApplyDamageAndEffect(enemy, damage, s.effect, s.effectDps, s.effectDur, at);
        }
        public void ApplyBurnOnEnemy(float dmg, Vector3 at) => Damage(enemy, dmg, "burn", at);

        // ------------------------------------------------------------ Boss 的小怪（熔岩小鬼）
        // 單人／合作主機：小怪由這裡的 UpdateMinions 驅動（走向最近的玩家、預備 0.7 秒後爪擊）。
        // 合作隊友端：minions 是主機送來的代理（只顯示、只判定我的法術有沒有打到），打到 → OnMinionHit 送主機。
        public readonly List<Fighter> minions = new List<Fighter>();
        public Func<Vector3, bool> minionInside;           // 場地內（場地座標）
        public float minionSpeed = 1.0f;
        public const int MaxMinions = 5;
        public event Action<int, int, SkillDef> OnMinionHit;   // 隊友端：我的法術打到小怪（編號、傷害、技能）

        public static readonly string[] MinionNames = { "熔岩小鬼", "火焰射手", "岩石衝鋒獸", "自爆小鬼" };
        // 各種小怪的基本數值：血量、移動速度、身體半徑、頭的高度
        static readonly int[] MinionHp = { 40, 30, 120, 20 };
        static readonly float[] MinionSpeed = { 1.0f, 0.9f, 0.6f, 1.7f }, MinionRadius = { 0.25f, 0.23f, 0.4f, 0.22f }, MinionHead = { 0.85f, 0.8f, 1.3f, 0.65f };

        public Fighter SpawnMinion(Vector3 feet, int id = 0, int kind = 0, float hpMul = 1f, float dmgMul = 1f, float speedMul = 1f)
        {
            kind = Mathf.Clamp(kind, 0, 3);
            var c = Skills.Classes["minion"];
            var m = new Fighter(MinionNames[kind], c, new string[0], false)
            {
                isMinion = true, minionKind = kind, bodyRadius = MinionRadius[kind], minionId = id > 0 ? id : nextId++,
                dmgMul = dmgMul, speedMul = speedMul,
            };
            m.maxHp = Mathf.RoundToInt(MinionHp[kind] * hpMul); m.hp = m.maxHp;
            m.head = new Vector3(feet.x, MinionHead[kind], feet.z);
            m.attackReadyAt = now + 1.2f;
            minions.Add(m);
            Emit("minionspawn", m.Feet, c.color, null);
            return m;
        }

        // ------------------------------------------------------------ 無盡模式
        // 場地邊緣陸續冒出小怪；每 60 秒出現一隻 Boss（Boss 在場時不再冒小怪）；每打倒一隻 Boss 進入下一波：
        // 敵人更強（血量、傷害、速度），而且解鎖新的小怪種類（火焰射手 → 岩石衝鋒獸 → 自爆小鬼）。
        public bool endless;
        public int wave, kills, bossKills;
        public float nextBossAt, nextMinionAt, bossBaseHp = 400f;
        public const float BossInterval = 60f;
        public Func<Vector3> edgePoint;          // 小怪出生點：離所有玩家 15m 以上（可以在場地外，再走進來）
        public Func<Vector3> bossPoint;          // Boss 出生點：場地邊緣附近（沒設定就用 edgePoint）
        public const float MinionSpawnDistance = 15f;
        public event Action<int> OnBossSpawn;    // 無盡模式 Boss 出現（波次）

        public void StartEndless()
        {
            endless = true; wave = 0; kills = 0; bossKills = 0;
            enemy.hp = 0f;                        // 一開始沒有 Boss
            nextBossAt = now + BossInterval; nextMinionAt = now + 4f;
        }
        // 經驗值：小怪依種類、Boss 依波次；合作時兩人共用同一份經驗（各自選升級）
        public int xp;
        static readonly int[] MinionXp = { 10, 12, 25, 12 };
        /// <summary>升到下一級需要的累積經驗：40、110、210、340…</summary>
        public static int XpForLevel(int level) => 40 * (level - 1) + 15 * (level - 1) * (level - 1) + 0;
        public static int LevelOf(int xp) { int l = 1; while (xp >= XpForLevel(l + 1)) l++; return l; }
        public float HpMul => 1f + 0.35f * wave;
        public float DmgMul => 0.7f + 0.2f * wave;
        public const float EndlessRegen = 0.5f;   // 無盡模式：玩家每秒自然回血
        public float SpeedMul => Mathf.Min(1.5f, 1f + 0.05f * wave);

        float Time_dt;
        void UpdateEndless()
        {
            if (endless && player.Alive) player.hp = Mathf.Min(player.maxHp, player.hp + EndlessRegen * Time_dt);
            if (!endless || remoteEnemy || edgePoint == null) return;
            if (!player.Alive && (ally == null || !ally.Alive)) return;
            if (!enemy.Alive && now >= nextBossAt) SpawnBoss();
            if (enemy.Alive) return;   // Boss 在場：小怪不再增加
            int maxAlive = Mathf.Min(10, 3 + wave * 2) + (ally != null ? 2 : 0);
            if (now < nextMinionAt || minions.Count >= maxAlive) return;
            SpawnMinion(edgePoint(), 0, PickMinionKind(), HpMul, DmgMul, SpeedMul);
            // 出怪間隔：第 1 波 8 秒一隻，之後每波快 0.7 秒，最快 4 秒（合作快 25%）
            nextMinionAt = now + Mathf.Max(4f, 8f - wave * 0.7f) * (ally != null ? 0.75f : 1f);
        }

        int PickMinionKind()
        {
            int unlocked = Mathf.Min(3, wave);
            float[] w = { 3f, 2f, 1.5f, 1.5f };
            float sum = 0f; for (int k = 0; k <= unlocked; k++) sum += w[k];
            float r = (float)rng.NextDouble() * sum;
            for (int k = 0; k <= unlocked; k++) { r -= w[k]; if (r <= 0f) return k; }
            return 0;
        }

        void SpawnBoss()
        {
            var at = (bossPoint ?? edgePoint)();
            enemy.maxHp = Mathf.RoundToInt(bossBaseHp * (1f + 0.5f * wave) * (ally != null ? 1.6f : 1f));
            enemy.hp = enemy.maxHp;
            enemy.head = new Vector3(at.x, enemy.head.y > 0.5f ? enemy.head.y : 3.2f, at.z);
            enemy.charging = null; enemy.cooldownUntil.Clear(); enemy.dots.Clear(); enemy.snaredUntil = 0f; enemy.blindUntil = 0f;
            Emit("bossarrive", enemy.Feet, enemy.cls.color, $"第 {wave + 1} 波 Boss 出現！");
            OnBossSpawn?.Invoke(wave);
        }

        void OnBossDown()
        {
            bossKills++; wave++;
            xp += 80 * wave;
            // 獎勵：打倒 Boss 回復 30% 血量（隊友端看到波次增加時自己回）
            if (player.Alive) player.hp = Mathf.Min(player.maxHp, player.hp + player.maxHp * 0.3f);
            nextBossAt = now + BossInterval; nextMinionAt = now + 5f;
            Emit("bossdown", enemy.Feet, enemy.cls.color, $"打倒 Boss！進入第 {wave + 1} 波（敵人變強了）");
        }
        public Fighter MinionById(int id) => minions.Find(m => m.minionId == id);
        /// <summary>主機：隊友打到小怪</summary>
        public void ApplyHitOnMinion(int id, int damage, SkillDef s)
        {
            var m = MinionById(id);
            if (m != null && m.Alive) ApplyDamageAndEffect(m, damage, s != null ? s.effect : EffectKind.None, s != null ? s.effectDps : 0f, s != null ? s.effectDur : 0f, m.Chest);
        }

        void UpdateMinions(float dt)
        {
            if (!enemy.Alive && !endless) { foreach (var m in minions) if (m.Alive) { m.hp = 0; Emit("miniondie", m.Feet, m.cls.color, null); } }
            minions.RemoveAll(m => !m.Alive);
            if (remoteEnemy) return;   // 隊友端：代理，不跑 AI
            var claw = Skills.All["m_claw"];
            foreach (var m in minions)
            {
                if (!m.Alive) continue;
                // 目標：最近的活著的玩家
                Fighter tgt = null; float best = float.MaxValue;
                foreach (var pl in Players()) { if (!pl.Alive) continue; float d0 = FlatDistance(m.Feet, pl.Feet); if (d0 < best) { best = d0; tgt = pl; } }
                if (tgt == null) continue;
                m.target = tgt;
                m.forward = Fighter.Flat(tgt.head - m.head);
                float d = best;
                int kind = m.minionKind;
                if (m.windupUntil > 0f)
                {
                    if (now < m.windupUntil) continue;   // 預備中站著不動
                    m.windupUntil = 0f;
                    MinionStrike(m, tgt, d);
                    continue;
                }
                if (m.snaredUntil > now) continue;
                // 出手條件：近戰／衝鋒獸／自爆要貼近；射手在 9m 內
                float reach = kind == 1 ? 9f : kind == 3 ? 1.3f : kind == 2 ? 1.2f : 0.95f;
                float windup = kind == 1 ? 0.8f : kind == 2 ? 1.0f : kind == 3 ? 0.5f : claw.charge;
                if (d <= reach + tgt.bodyRadius && now >= m.attackReadyAt) { m.windupUntil = now + windup; Emit("minionwindup", m.Feet, claw.color, null); continue; }
                // 走位：射手保持 3.5～6m，其他走向目標（停在 0.8m）；小怪之間互相推開
                float speed = minionSpeed * MinionSpeed[kind] * m.speedMul;
                Vector3 step;
                if (kind == 1)
                {
                    float want = d > 6f ? d - 6f : d < 3.5f ? -(3.5f - d) : 0f;
                    step = Fighter.Flat(tgt.Feet - m.Feet) * Mathf.Clamp(want, -speed * dt, speed * dt);
                }
                else step = Fighter.Flat(tgt.Feet - m.Feet) * Mathf.Min(speed * dt, Mathf.Max(0f, d - 0.8f));
                foreach (var o in minions)
                {
                    if (o == m) continue;
                    var away = m.Feet - o.Feet; away.y = 0f;
                    float od = away.magnitude;
                    if (od < 0.6f) step += (od > 1e-3f ? away / od : new Vector3(0.1f, 0f, 0f)) * (0.6f - od) * 2f * dt;
                }
                var next = m.Feet + step;
                // 場地外出生的小怪：還在場地外時也可以走（朝玩家走進場地）；進到場地後就不會再走出去
                if (minionInside == null || minionInside(next) || !minionInside(m.Feet)) m.head = new Vector3(next.x, m.head.y, next.z);
            }
        }

        /// <summary>小怪預備完成、出手：近戰爪擊、射手丟火球、衝鋒獸重擊、自爆</summary>
        void MinionStrike(Fighter m, Fighter tgt, float d)
        {
            var claw = Skills.All["m_claw"];
            switch (m.minionKind)
            {
                case 1:
                {
                    var fb = Skills.All["m_fireball"];
                    m.attackReadyAt = now + fb.cooldown;
                    var from = m.head + Fighter.Flat(m.forward) * 0.3f;
                    Spawn(m, fb, from, (tgt.Chest - from).normalized, Mathf.RoundToInt(fb.damage * m.dmgMul), EffectKind.None, 0f, 0f, false);
                    Emit("minionattack", m.Feet, fb.color, null);
                    return;
                }
                case 3:
                {
                    // 自爆：周圍 1.8m 內的玩家受傷（側跨閃得掉），自己消失
                    var bomb = Skills.All["m_bomb"];
                    Emit("minionexplode", m.Feet, bomb.color, null);
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (!v.Alive || FlatDistance(v.Feet, m.Feet) > bomb.areaRadius + v.bodyRadius) continue;
                        int dmg = Mathf.RoundToInt(bomb.damage * m.dmgMul);
                        if (v.dodgeUntil > now) Emit("evade", v.Chest, bomb.color, "閃避！");
                        else if (v == ally) { Emit("allyhit", v.Chest, bomb.color, null); OnAllyHit?.Invoke(bomb, dmg, v.Chest, 0); }
                        else ApplyDamageAndEffect(v, dmg, EffectKind.None, 0f, 0f, v.Chest);
                    }
                    m.hp = 0f;
                    return;
                }
                default:
                {
                    // 近戰小鬼 7、岩石衝鋒獸 16（再乘上波次倍率）
                    int dmg = Mathf.RoundToInt((m.minionKind == 2 ? 16 : claw.damage) * m.dmgMul);
                    m.attackReadyAt = now + (m.minionKind == 2 ? 2.2f : claw.cooldown);
                    Emit("minionattack", m.Feet, claw.color, null);
                    float reach = m.minionKind == 2 ? 1.6f : claw.rangeMax;
                    if (d <= reach + tgt.bodyRadius)
                    {
                        if (tgt.dodgeUntil > now) Emit("evade", tgt.Chest, claw.color, "閃避！");
                        else if (tgt == ally) { Emit("allyhit", tgt.Chest, claw.color, null); OnAllyHit?.Invoke(claw, dmg, tgt.Chest, 0); }
                        else ApplyDamageAndEffect(tgt, dmg, EffectKind.None, 0f, 0f, tgt.Chest);
                    }
                    return;
                }
            }
        }

        /// <summary>法術有沒有打到小怪（玩家方的法術才會）；打到回傳 true</summary>
        bool HitMinions(Projectile p)
        {
            if (minions.Count == 0 || p.owner == enemy || p.owner.isMinion) return false;
            foreach (var m in minions)
            {
                if (!m.Alive) continue;
                m.BodySegment(out var a, out var b);
                if (DistancePointSegment(p.pos, a, b) >= p.skill.radius + m.bodyRadius) continue;
                p.alive = false;
                if (p.crit) Emit("crit", p.pos, p.skill.color, "爆擊！");
                LastProcs = RollProcs(p.owner);
                if (remoteEnemy) { Emit("hit", p.pos, p.skill.color, null); OnMinionHit?.Invoke(m.minionId, p.damage, p.skill); }
                else { ApplyDamageAndEffect(m, p.damage, p.effect, p.effectDps, p.effectDur, p.pos); ApplyProcs(m, LastProcs, p.pos); }
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ 雙人連線模式
        // enemy 是「對手的代理」：位置、血量由網路更新；對手的法術與陷阱只顯示。
        // 判定規則（攻擊方判定）：我的法術／陷阱打到對手 → OnRemoteHit 送給對手，由對手自己扣血與套用格擋、護盾、反擊；
        // 對手判定打到我 → ApplyRemoteHit。
        public bool remoteEnemy;
        public event Action<Projectile> OnSpawn;                 // 我方新增的法術（要送給對手）
        public event Action<Trap> OnTrapPlaced;                  // 我方新設的陷阱
        public event Action<SkillDef, int, Vector3, int> OnRemoteHit;   // 打到對手：技能、傷害、位置、法術或陷阱 id
        public event Action<int> OnTrapGone;                     // 我方陷阱消失（觸發或過期）
        public event Action<SkillDef, float, Vector3> OnRemoteBurn;     // 我方火海燒到對手：技能、傷害、位置

        /// <summary>顯示對手的法術（從對手送來的起點、方向；elapsed＝已經飛了幾秒）</summary>
        public Projectile AddRemoteProjectile(int id, SkillDef s, Vector3 from, Vector3 dir, float elapsed, bool reflected, float curveAmp = 0f, float pathLen = 0f, Fighter owner = null)
        {
            var p = new Projectile { owner = owner ?? enemy, skill = s, pos = from, basePos = from, dir = dir.normalized, damage = s.damage, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur, reflected = reflected, id = id, remote = true };
            SetCurve(p, curveAmp, pathLen);
            Advance(p, s.speed * Mathf.Max(0f, elapsed));
            projectiles.Add(p);
            return p;
        }

        /// <summary>對手的火海燒到我（對手判定）</summary>
        public void ApplyRemoteBurn(float dmg, Vector3 at) { Damage(player, dmg, "burn", at); }

        static void SetCurve(Projectile p, float amp, float len)
        {
            p.curveAmp = amp; p.pathLen = len;
            var side = Vector3.Cross(Vector3.up, p.dir); side.y = 0;
            p.curveSide = side.sqrMagnitude > 1e-6f ? side.normalized * amp : Vector3.zero;
        }

        /// <summary>沿路徑前進 d 公尺（弧線：基準點走直線，再加側向偏移）</summary>
        static void Advance(Projectile p, float d)
        {
            p.basePos += p.dir * d;
            p.traveled += d;
            p.pos = p.Curved ? p.basePos + p.curveSide * Mathf.Sin(Mathf.PI * Mathf.Clamp01(p.traveled / p.pathLen)) : p.basePos;
        }

        public void RemoveRemoteProjectile(int id)
        {
            foreach (var p in projectiles)
                if (p.remote && p.id == id && p.alive)
                {
                    p.alive = false;
                    if (p.skill.id == "meteor" && !p.reflected && p.owner == enemy) AddBurn(p, player.Feet);   // 對手的隕石砸中我：顯示火海
                }
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

        /// <summary>對手：玩家（和隊友）打 enemy；enemy 打它目前的目標（沒有就打玩家）</summary>
        public Fighter Opponent(Fighter f) => f == enemy || f.isMinion ? (f.target != null && f.target.Alive ? f.target : player) : enemy;
        public static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }
        public float Distance => FlatDistance(player.head, enemy.head);
        public bool Over => (!endless && !enemy.Alive) || (!player.Alive && (ally == null || !ally.Alive));

        // ------------------------------------------------------------ 詠唱
        public bool TryChant(Fighter f, SkillDef s, out string why)
        {
            why = null;
            if (Over) { why = "戰鬥結束"; return false; }
            if (!f.Alive) { why = "你已倒下，等隊友"; return false; }
            if (f.cooldownUntil.TryGetValue(s.id, out var cd) && cd > now) { why = $"{s.name} 冷卻中"; return false; }
            if (f.mp < s.cost) { why = "MP 不足"; return false; }
            if (f.snaredUntil > now && s.self != SelfKind.Cleanse) { why = "被困住，暫時不能施法（牧師可以淨化）"; return false; }
            f.charging = s; f.chargeStart = now;
            return true;
        }

        public float ChargeProgress(Fighter f) => f.charging == null ? 0f : f.charging.charge <= 0f ? 1f : Mathf.Clamp01((now - f.chargeStart) / Mathf.Max(0.01f, f.charging.charge * f.chargeScale));

        /// <summary>射程狀態：0＝可以、1＝太遠、-1＝太近</summary>
        public int RangeState(Fighter f, SkillDef s)
        {
            if (s.type != SkillType.Projectile) return 0;
            float d = FlatDistance(f.head, Opponent(f).head);
            if (d > s.rangeMax * f.rangeMul) return 1;
            if (d < s.rangeMin) return -1;
            return 0;
        }

        // ------------------------------------------------------------ 出手
        /// <param name="aimDir">投射物的方向（場地座標）</param>
        /// <param name="floorPoint">陷阱的位置（地板上）；沒鎖定時隕石也落在這裡</param>
        /// <param name="locked">有沒有鎖定敵人。沒鎖定也能放：朝準星方向飛，隕石落在準星指的地面</param>
        public bool TryRelease(Fighter f, Vector3 aimDir, Vector3 floorPoint, out string why, bool locked = true)
        {
            why = null;
            var s = f.charging;
            if (s == null || Over) { why = "沒有在詠唱"; return false; }
            if (f.mp < s.cost) { f.charging = null; why = "MP 不足"; return false; }
            if (now - f.chargeStart < s.charge * f.chargeScale) { why = $"蓄力中 {Mathf.FloorToInt(ChargeProgress(f) * 100)}%"; return false; }
            // 蓄力完成就一定放得出去（不管有沒有瞄到敵人、距離多遠），打不打得中看瞄準與距離。
            // 陷阱：準星指得太遠時，放在同方向的最遠設置距離上
            if (s.type == SkillType.Trap)
            {
                var off = floorPoint - f.Feet; off.y = 0f;
                if (off.sqrMagnitude < 1e-6f) off = Fighter.Flat(f.forward) * 1.5f;
                if (off.magnitude > s.trapRange) off = off.normalized * s.trapRange;
                floorPoint = f.Feet + off; floorPoint.y = 0f;
            }

            f.mp -= s.cost;
            f.cooldownUntil[s.id] = now + s.cooldown * f.cdMul;
            f.charging = null;

            if (s.releaseNear)
            {
                f.armed = s; f.armedUntil = now + AmbushWindow;
                Emit("armed", f.Chest, s.color, $"{s.name} 伏擊中");
                return true;
            }

            switch (s.type)
            {
                case SkillType.Self: CastSelf(f, s); break;
                case SkillType.Trap: PlaceTrap(f, s, floorPoint); break;
                case SkillType.Projectile when s.areaRadius > 0f:
                {
                    // 範圍重擊（Boss 地裂）：放招當下站在半徑內的玩家都受傷；側跨的無敵時間內閃得掉
                    Emit("slam", f.Feet, s.color, null);
                    Emit("cast", f.head, s.color, null);
                    foreach (var v in new List<Fighter>(Players()))
                    {
                        if (v == f || !v.Alive || FlatDistance(f.Feet, v.Feet) > s.areaRadius + v.bodyRadius) continue;
                        if (v.dodgeUntil > now) { Emit("evade", v.Chest, s.color, "閃避！"); continue; }
                        if (v == ally) { Emit("allyhit", v.Chest, s.color, null); OnAllyHit?.Invoke(s, s.damage, v.Chest, 0); }
                        else ApplyDamageAndEffect(v, s.damage, s.effect, s.effectDps, s.effectDur, v.Chest);
                    }
                    break;
                }
                case SkillType.Projectile when s.fromAbove:
                {
                    // 隕石、審判：從敵人（施放當下的位置）正上方砸下，敵人可以趁下落時移動閃開；
                    // 沒鎖定時落在準星指的地面（最遠到射程）
                    var target = Opponent(f).Feet;
                    if (!locked)
                    {
                        var off = floorPoint - f.Feet; off.y = 0f;
                        if (off.magnitude > s.rangeMax) off = off.normalized * s.rangeMax;
                        target = f.Feet + off; target.y = 0f;
                    }
                    Spawn(f, s, target + Vector3.up * s.dropHeight, Vector3.down, s.damage, s.effect, s.effectDps, s.effectDur, false);
                    // 一次落下好幾顆（Boss 落石雨）：其餘落在目標周圍 2.5m 內，錯開一點高度（陸續落下）
                    for (int i = 1; i < s.multi; i++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 0.8f + (float)rng.NextDouble() * 1.7f;
                        var at = target + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        Spawn(f, s, at + Vector3.up * (s.dropHeight + i * 1.2f), Vector3.down, s.damage, s.effect, s.effectDps, s.effectDur, false);
                    }
                    Emit("cast", f.head, s.color, null);
                    break;
                }
                default:
                    var hand = f.head + Fighter.Flat(f.forward) * 0.25f + Vector3.down * 0.25f;
                    aimDir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Fighter.Flat(f.forward);
                    if (s.originHeight > 0f)
                    {
                        // 水平射出（Boss 橫掃）：從身體前方、固定高度出發
                        hand = new Vector3(f.head.x, s.originHeight, f.head.z) + Fighter.Flat(f.forward) * (f.bodyRadius + 0.3f);
                        aimDir = Fighter.Flat(aimDir); if (aimDir == Vector3.zero) aimDir = Fighter.Flat(f.forward);
                    }
                    if (s.id == "wind")
                    {
                        // 風刃：隨機從左或右側繞弧線飛向目標（終點仍是準星方向、敵人的距離）
                        float dist = Mathf.Max(1f, locked ? FlatDistance(f.head, Opponent(f).head) : s.rangeMax * 0.6f);
                        float amp = Mathf.Max(0.5f, dist * 0.45f) * (rng.Next(2) == 0 ? -1f : 1f);
                        var wp = Spawn(f, s, hand, aimDir, s.damage, s.effect, s.effectDps, s.effectDur, false, amp, dist);
                        Emit("cast", hand, s.color, null);
                        break;
                    }
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
                case SelfKind.Armor: f.armorUntil = now + s.buffDur; f.armorReduce = s.buffReduce; break;
                case SelfKind.Cleanse:
                    // 淨化：解除中毒、定身、致盲，回一點血
                    f.dots.Clear(); f.snaredUntil = 0f; f.blindUntil = 0f;
                    if (s.heal > 0) f.hp = Mathf.Min(f.maxHp, f.hp + s.heal);
                    Emit("heal", f.Chest, s.color, s.heal > 0 ? $"淨化 +{s.heal}" : "淨化");
                    return;
                case SelfKind.Dispel:
                {
                    // 抗拒法環：以自己為中心，打斷範圍內所有敵人的蓄力（含刺客的伏擊），被打斷的技能重新冷卻
                    Emit("dispel", f.Feet, s.color, s.name);
                    foreach (var v in new[] { Opponent(f) })
                        if (!(remoteEnemy && v == enemy)) DispelAt(v, f.Feet, s.areaRadius);
                    if (f == player) OnDispel?.Invoke(f.Feet, s.areaRadius);
                    return;
                }
                case SelfKind.Summon:
                {
                    // Boss 召喚小怪：在身邊 1.6～2.4m 冒出來（合作時多一隻），最多同時 MaxMinions 隻
                    int n = Mathf.Min(s.multi + (ally != null ? 1 : 0), MaxMinions - minions.Count);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 at = f.Feet;
                        for (int tries = 0; tries < 8; tries++)
                        {
                            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.6f + (float)rng.NextDouble() * 0.8f;
                            at = f.Feet + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                            if (minionInside == null || minionInside(at)) break;
                        }
                        SpawnMinion(at);
                    }
                    Emit("summon", f.Feet, s.color, n > 0 ? $"召喚了 {n} 隻小怪！" : null);
                    return;
                }
                case SelfKind.Prayer:
                {
                    // 祈禱：其他技能的冷卻減少，回復 MP
                    var keys = new List<string>(f.cooldownUntil.Keys);
                    foreach (var k in keys) if (k != s.id) f.cooldownUntil[k] = Mathf.Max(now, f.cooldownUntil[k] - s.buffDur);
                    f.mp = Mathf.Min(f.maxMp, f.mp + s.buffAmount);
                    Emit("buff", f.Chest, s.color, $"祈禱：冷卻 -{s.buffDur:0}s・MP +{s.buffAmount}");
                    return;
                }
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

        Projectile Spawn(Fighter owner, SkillDef s, Vector3 from, Vector3 dir, int dmg, EffectKind eff, float dps, float dur, bool reflected, float curveAmp = 0f, float pathLen = 0f)
        {
            var p = new Projectile { owner = owner, skill = s, pos = from, basePos = from, dir = dir.normalized, damage = dmg, effect = eff, effectDps = dps, effectDur = dur, reflected = reflected, id = nextId++ };
            if (owner.atkMul != 1f || owner.critChance > 0f || owner.rangeMul != 1f)
            {
                p.damage = Mathf.RoundToInt(dmg * owner.atkMul);
                if (owner.critChance > 0f && rng.NextDouble() < owner.critChance) { p.crit = true; p.damage *= 2; }
                p.rangeMul = owner.rangeMul;
            }
            if ((owner == enemy || owner.isMinion) && owner.target != null) p.target = owner.target;
            SetCurve(p, curveAmp, pathLen);
            projectiles.Add(p);
            // 要給另一支手機顯示的法術：雙人對戰的我方法術；合作模式的我方法術、主機上 Boss 的法術
            if ((owner == player && (remoteEnemy || ally != null)) || ((owner == enemy || owner.isMinion) && coopHost)) OnSpawn?.Invoke(p);
            return p;
        }

        // ------------------------------------------------------------ 每幀
        public void Update(float dt)
        {
            now += dt;
            foreach (var f in new[] { player, enemy })
            {
                if (remoteEnemy && f == enemy) continue;   // 對手的 MP、詠唱、持續傷害由對手自己算
                UpdateAmbush(f);
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
            // 小怪身上的持續傷害（灼燒、中毒）
            if (!remoteEnemy)
                foreach (var m in minions)
                    for (int i = m.dots.Count - 1; i >= 0; i--)
                    {
                        var d = m.dots[i];
                        d.acc += dt;
                        while (d.acc >= 1f && m.Alive) { d.acc -= 1f; Damage(m, d.dps, "dot", m.Chest); }
                        if (d.until <= now) m.dots.RemoveAt(i);
                    }
            UpdateMinions(dt);
            Time_dt = dt;
            UpdateEndless();
            UpdateTraps();
            UpdateBurns(dt);
        }

        void UpdateProjectiles(float dt)
        {
            // 用索引走訪：反擊會在迴圈中新增「打回去的法術」，不能用 foreach（會丟例外）；新增的下一幀才開始飛
            int count = projectiles.Count;
            for (int i = 0; i < count; i++)
            {
                var p = projectiles[i];
                if (!p.alive) continue;
                var target = p.target != null && p.target.Alive ? p.target : Opponent(p.owner);
                float step = p.skill.speed * dt;
                // 分小段前進，快的箭也不會穿過身體
                int sub = Mathf.Max(1, Mathf.CeilToInt(step / 0.08f));
                for (int k = 0; k < sub && p.alive; k++)
                {
                    Advance(p, step / sub);
                    // 隕石落地：留下火海（我方的才判定傷害；對手的只顯示）
                    if (p.skill.id == "meteor" && p.pos.y <= 0.05f)
                    {
                        p.alive = false;
                        AddBurn(p, p.pos);
                        break;
                    }
                    target.BodySegment(out var a, out var b);
                    if (!p.remote && HitMinions(p)) break;   // 先擋在前面的小怪會吃到這一發
                    if (!target.Alive)
                    {
                        // 目標不在了（無盡模式 Boss 還沒出現）：只飛，飛完消失
                        if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel * p.rangeMul) p.alive = false;
                    }
                    else if (p.remote)
                    {
                        // 對手的法術：命中由對手判定（收到結果時移除）；這裡只讓它飛，飛完消失
                        if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel) p.alive = false;
                    }
                    else if (DistancePointSegment(p.pos, a, b) < p.skill.radius + target.bodyRadius)
                    {
                        // 閃避：蹲下躲過橫向攻擊、側跨的無敵時間內 → 法術穿過去
                        if (Evades(target, p))
                        {
                            if (!p.evaded) { p.evaded = true; Emit("evade", target.Chest, p.skill.color, target.dodgeUntil > now ? "閃避！" : "蹲下躲過！"); }
                            continue;
                        }
                        p.alive = false;
                        if (p.skill.id == "meteor" && !p.reflected) AddBurn(p, target.Feet);   // 直接砸中也留下火海
                        if (p.crit) Emit("crit", p.pos, p.skill.color, "爆擊！");
                        LastProcs = RollProcs(p.owner);
                        if (remoteEnemy && target == enemy) { Emit("hit", p.pos, p.skill.color, null); OnRemoteHit?.Invoke(p.skill, p.damage, p.pos, p.id); }
                        else if (target == ally) { Emit("allyhit", p.pos, p.skill.color, null); OnAllyHit?.Invoke(p.skill, p.damage, p.pos, p.id); }   // 主機：Boss 打到隊友
                        else Hit(target, p);
                    }
                    else if (p.pos.y < 0f || p.traveled > p.skill.MaxTravel * p.rangeMul) { p.alive = false; Emit("miss", p.pos, p.skill.color, null); }
                }
            }
            projectiles.RemoveAll(x => !x.alive);
        }

        /// <summary>抗拒法環打到 v（v 的腳在 center 半徑 r 內）：取消蓄力／伏擊，那個技能重新冷卻。回傳有沒有打斷</summary>
        public bool DispelAt(Fighter v, Vector3 center, float r)
        {
            if (v == null || !v.Alive || FlatDistance(v.Feet, center) > r + v.bodyRadius) return false;
            bool any = false;
            if (v.charging != null) { v.cooldownUntil[v.charging.id] = now + v.charging.cooldown; v.charging = null; any = true; }
            if (v.armed != null) { v.cooldownUntil[v.armed.id] = now + v.armed.cooldown; v.armed = null; any = true; }
            if (any) Emit("dispelled", v.Chest, new Color(0.94f, 0.67f, 0.99f), "被打斷！");
            return any;
        }

        /// <summary>這一發會不會被閃過：側跨的無敵時間內全部閃過；蹲著時橫向攻擊（duckable）打不到</summary>
        public bool Evades(Fighter target, Projectile p) => target.dodgeUntil > now || (target.crouching && p.skill.duckable);

        /// <summary>伏擊：時限內和對手的水平距離 ≤ 技能射程 → 自動出手一次</summary>
        void UpdateAmbush(Fighter f)
        {
            var s = f.armed;
            if (s == null) return;
            if (now > f.armedUntil || !f.Alive) { f.armed = null; Emit("armedExpire", f.Chest, Color.gray, $"{s.name} 落空"); return; }
            var target = Opponent(f);
            if (!target.Alive || FlatDistance(f.head, target.head) > s.rangeMax) return;
            f.armed = null;
            Emit("ambush", target.Chest, s.color, s.name + "！");
            if (remoteEnemy && target == enemy) { Emit("hit", target.Chest, s.color, null); OnRemoteHit?.Invoke(s, s.damage, target.Chest, 0); }
            else Hit(target, new Projectile { owner = f, skill = s, pos = target.Chest, dir = Fighter.Flat(target.head - f.head), damage = s.damage, effect = s.effect, effectDps = s.effectDps, effectDur = s.effectDur });
        }

        void AddBurn(Projectile p, Vector3 at)
        {
            var c = new Vector3(at.x, 0f, at.z);
            burns.Add(new BurnZone { owner = p.owner, skill = p.skill, pos = c, radius = BurnRadius, until = now + BurnDuration, dps = BurnDps, remote = p.remote });
            Emit("burnzone", c, p.skill.color, null);
        }

        void UpdateBurns(float dt)
        {
            foreach (var z in burns)
            {
                if (!z.alive) continue;
                if (now > z.until) { z.alive = false; continue; }
                if (z.remote) continue;   // 對手的火海由對手判定
                var victim = Opponent(z.owner);
                if (!victim.Alive || FlatDistance(victim.Feet, z.pos) > z.radius) { z.acc = 0f; continue; }
                z.acc += dt;
                while (z.acc >= 1f)
                {
                    z.acc -= 1f;
                    if (remoteEnemy && victim == enemy) { Emit("hit", victim.Chest, z.skill.color, null); OnRemoteBurn?.Invoke(z.skill, z.dps, victim.Feet); }
                    else Damage(victim, z.dps, "burn");
                }
            }
            burns.RemoveAll(x => !x.alive);
        }

        void UpdateTraps()
        {
            foreach (var t in traps)
            {
                if (!t.alive) continue;
                if (now > t.until) { t.alive = false; if (!t.remote && (remoteEnemy || ally != null)) OnTrapGone?.Invoke(t.id); continue; }
                if (t.remote || !t.Armed(now)) continue;   // 對手的陷阱由對手判定
                var victim = Opponent(t.owner);
                if (FlatDistance(victim.Feet, t.pos) < t.skill.radius + 0.15f)
                {
                    t.alive = false;
                    Emit("trap", t.pos, t.skill.color, $"{t.skill.name}！");
                    if (remoteEnemy && victim == enemy) { OnRemoteHit?.Invoke(t.skill, t.skill.damage, t.pos, -t.id); OnTrapGone?.Invoke(t.id); }
                    else
                    {
                        ApplyDamageAndEffect(victim, t.skill.damage, t.skill.effect, t.skill.effectDps, t.skill.effectDur, t.pos);
                        if (ally != null) OnTrapGone?.Invoke(t.id);   // 合作模式：告訴隊友陷阱沒了
                    }
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
            ApplyProcs(target, LastProcs, p.pos);
        }

        // ------------------------------------------------------------ 命中附加效果（無盡模式升級）
        /// <summary>最近一次命中擲出的附加效果（1 灼燒、2 中毒、4 冰凍）；合作隊友端送給主機用</summary>
        public int LastProcs;
        int RollProcs(Fighter attacker)
        {
            if (attacker == null) return 0;
            int r = 0;
            if (attacker.burnChance > 0f && rng.NextDouble() < attacker.burnChance) r |= 1;
            if (attacker.poisonChance > 0f && rng.NextDouble() < attacker.poisonChance) r |= 2;
            if (attacker.freezeChance > 0f && rng.NextDouble() < attacker.freezeChance) r |= 4;
            return r;
        }
        /// <summary>套用附加效果：灼燒（每秒 5、4 秒）、中毒（每秒 3、6 秒）、冰凍（定身 1.5 秒，打斷蓄力）</summary>
        public void ApplyProcs(Fighter target, int procs, Vector3 at)
        {
            if (procs == 0 || target == null || !target.Alive) return;
            if ((procs & 1) != 0) { target.dots.Add(new Fighter.Dot { dps = 5f, until = now + 4f }); Emit("effect", at, new Color(1f, 0.5f, 0.15f), "灼燒"); }
            if ((procs & 2) != 0) { target.dots.Add(new Fighter.Dot { dps = 3f, until = now + 6f }); Emit("effect", at, Color.green, "中毒"); }
            if ((procs & 4) != 0) { target.snaredUntil = now + 1.5f; target.charging = null; target.windupUntil = 0f; Emit("effect", at, new Color(0.6f, 0.9f, 1f), "冰凍"); }
        }

        void ApplyDamageAndEffect(Fighter target, int damage, EffectKind eff, float dps, float dur, Vector3 at)
        {
            float dmg = damage;
            string note = null;
            if (target.blockUntil > now && dmg > 0) { dmg = Mathf.Round(dmg * (1f - target.blockReduce)); target.blockUntil = 0; note = "格擋"; }
            if (target.armorUntil > now && dmg > 0) { dmg = Mathf.Round(dmg * (1f - target.armorReduce)); note = note == null ? "守護" : note + "＋守護"; }
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
            dmg = Mathf.Round(dmg * f.damageTakenMul);
            f.hp = Mathf.Max(0, f.hp - dmg);
            Emit(kind == "dot" || kind == "burn" ? kind : (f.isPlayer ? "hurt" : "hit"), at ?? f.Chest, Color.red, dmg > 0 ? $"-{dmg:0}" : "");
            if (!f.Alive)
            {
                if (f.isMinion) { kills++; if (endless) xp += Mathf.RoundToInt(MinionXp[f.minionKind] * (1f + 0.2f * wave)); Emit("miniondie", f.Chest, Color.white, null); }
                else if (f.isPlayer) Emit(ally != null && ally.Alive ? "down" : "lose", f.Chest, Color.white, null);
                else if (endless && f == enemy) OnBossDown();
                else Emit("win", f.Chest, Color.white, null);
            }
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
