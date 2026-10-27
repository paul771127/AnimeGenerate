using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>
    /// 變種怪（使用者要求）：用現成怪物的骨架與動作，換配色、武器、名字、數值，算成另一種怪（有自己的精魄）。
    ///   刺客怪（夜晚的副本才出現；每個副本一種）：深色配色＋匕首／長爪，移動速度、傷害 ×2；
    ///     出現在敵人的最後方 → 沿著場地邊緣快速繞到目標玩家的背後 → 從背後衝進來突襲 → 退回邊緣再繞（繞的時候只看得到發光的眼睛）。
    ///   天候怪（故事、副本，只有那種天候才出現）：雷雨精（暴雨）、霧行者（濃霧）、風刃鳶（順風）、月牙狼（月夜）。
    /// 變種的 id 從 101 開始（和 MinionKinds 分開）：minionKind 還是原本的種類（AI、碰撞、外觀骨架照舊），variant 記是哪一種變種。
    /// </summary>
    public sealed class VariantDef
    {
        public int id, baseKind;
        public string name, dungeon, desc;
        public bool assassin, claws, healer, summoner, summon;   // summon＝召喚物（沒有經驗、不掉寶）
        public int theme = -1;   // 補師：哪一章（3 角鬥場、4 黃土山道）
        public Battle.Weather weather;
        public float hp = 1f, dmg = 1f, speed = 1f;
        public Color tint;
    }

    public static class MonsterVariants
    {
        public static readonly VariantDef[] All =
        {
            // ---- 刺客怪（每個副本一種；千機迴廊在室內，沒有夜晚）
            new VariantDef { id = 101, baseKind = 0,  name = "灰燼匕影",   dungeon = "ash_mine",             assassin = true, desc = "熔岩小鬼的刺客，雙手反握黑曜匕首" },
            new VariantDef { id = 102, baseKind = 4,  name = "鑄爐潛刃",   dungeon = "ember_bell_foundry",   assassin = true, desc = "丟下巨石、改拿匕首的投擲者" },
            new VariantDef { id = 103, baseKind = 9,  name = "腐根割喉者", dungeon = "rot_temple",           assassin = true, claws = true, desc = "藤蔓術士的手指長成了長爪" },
            new VariantDef { id = 104, baseKind = 11, name = "霧徑暗菇",   dungeon = "mist_name_grove",      assassin = true, claws = true, desc = "菇傘下伸出兩把長爪的孢子菇" },
            new VariantDef { id = 105, baseKind = 15, name = "沉淵刺客",   dungeon = "sunken_throne",        assassin = true, desc = "騎魚的小魚人換上了短刃" },
            new VariantDef { id = 106, baseKind = 16, name = "黑礁剪影",   dungeon = "white_sail_echo_reef", assassin = true, claws = true, desc = "螯磨成長爪的寄居蟹" },
            new VariantDef { id = 107, baseKind = 8,  name = "破曉獵影",   dungeon = "dawn_gate",            assassin = true, claws = true, desc = "獠牙換成長爪的荊棘衝鋒獸" },
            // ---- 天候怪
            new VariantDef { id = 111, baseKind = 17, name = "雷雨精",   weather = Battle.Weather.Rain, hp = 1.5f, dmg = 1.3f, speed = 1.3f, tint = new Color(1f, 0.9f, 0.36f), desc = "暴雨裡才會出現的雷水母，放電更痛" },
            new VariantDef { id = 112, baseKind = 6,  name = "霧行者",   weather = Battle.Weather.Fog,  hp = 1.3f, dmg = 1.2f, tint = new Color(0.82f, 0.86f, 0.9f), desc = "濃霧裡的灰白術士，離遠了只看得到眼睛" },
            new VariantDef { id = 113, baseKind = 14, name = "風刃鳶",   weather = Battle.Weather.Wind, hp = 1.2f, dmg = 1.3f, speed = 1.6f, tint = new Color(0.49f, 0.95f, 0.69f), desc = "順風時乘風俯衝的鳶，飛得很快" },
            // ---- 補師（第四、五章；使用者要求）：站在敵陣後方，讀條治療受傷的同伴（被打中會打斷讀條），自己不攻擊
            new VariantDef { id = 121, baseKind = 9, name = "鬥場祭司", healer = true, theme = 3, tint = new Color(1f, 0.85f, 0.45f), desc = "角鬥場的金袍祭司，替受傷的鬥士念治療咒" },
            new VariantDef { id = 122, baseKind = 6, name = "砂谷藥師", healer = true, theme = 4, tint = new Color(0.45f, 0.95f, 0.8f), desc = "黃土山道的藥師，背著藥壺替同伴療傷" },
            // ---- 召喚師（第五章；使用者要求）：躲在後面，讀條召喚兩隻砂靈（血少、攻擊強、沒有經驗也不掉寶）；讀條被打中會打斷
            new VariantDef { id = 123, baseKind = 4, name = "砂谷召喚師", summoner = true, theme = 4, tint = new Color(0.78f, 0.55f, 1f), desc = "背著法陣石板的召喚師，從砂裡喚出砂靈" },
            new VariantDef { id = 124, baseKind = 0, name = "砂靈", summon = true, theme = 4, hp = 0.35f, dmg = 2f, speed = 1.3f, tint = new Color(1f, 0.72f, 0.38f), desc = "砂子聚成的小鬼：一碰就散，但打人很痛" },
            new VariantDef { id = 114, baseKind = 8,  name = "月牙狼",   weather = Battle.Weather.Moon, hp = 1.6f, dmg = 1.3f, speed = 1.4f, tint = new Color(0.72f, 0.78f, 1f), desc = "月夜才現身的銀色衝鋒獸" },
        };
        static readonly Dictionary<int, VariantDef> byId = new Dictionary<int, VariantDef>();
        static MonsterVariants() { foreach (var v in All) byId[v.id] = v; }
        public static VariantDef Get(int id) => byId.TryGetValue(id, out var v) ? v : null;
        public static bool IsVariant(int id) => id >= 100 && byId.ContainsKey(id);
        public static VariantDef AssassinFor(string dungeonId) { foreach (var v in All) if (v.assassin && v.dungeon == dungeonId) return v; return null; }
        public static VariantDef HealerFor(int theme) { foreach (var v in All) if (v.healer && v.theme == theme) return v; return null; }
        public static VariantDef SummonerFor(int theme) { foreach (var v in All) if (v.summoner && v.theme == theme) return v; return null; }
        public static VariantDef SummonFor(int theme) { foreach (var v in All) if (v.summon && v.theme == theme) return v; return null; }
        /// <summary>召喚物（擊倒紀錄的種類）：沒有經驗、不掉寶</summary>
        public static bool IsSummon(int id) => byId.TryGetValue(id, out var v) && v.summon;
        public static VariantDef ForWeather(Battle.Weather w) { foreach (var v in All) if (!v.assassin && v.weather == w && w != Battle.Weather.None) return v; return null; }
        /// <summary>擊倒紀錄的種類換回原本的怪（掉材料用）</summary>
        public static int BaseOf(int kind) => byId.TryGetValue(kind, out var v) ? v.baseKind : kind;
    }

    public partial class Battle
    {
        public const float AssassinSpeed = 2f, AssassinDmg = 2f;      // 使用者要求：移動速度、傷害 ×2
        public const float AssassinSprint = 2f;                         // 沿著邊緣繞、衝進來突襲時再快一倍（繞場一圈不用半分鐘）
        public const float AssassinLurk = 1.5f, AssassinWindup = 0.5f, AssassinReach = 0.9f, AssassinRecover = 1.2f, AssassinApproachMax = 6f;
        public const float WeatherMonsterChance = 0.5f;                 // 有天候的時候，每一波出一隻天候怪的機率
        /// <summary>這一關的刺客怪（夜晚的副本：SoloBattle 設定；null＝不出）</summary>
        public VariantDef assassinDef;
        int assassinWave = -1, weatherWave = -1; float assassinAt = -1f, weatherAt = -1f;

        /// <summary>擊倒紀錄、精魄用的種類：變種用變種 id</summary>
        public static int LogKind(Fighter m) => m.variant > 0 ? m.variant : m.minionKind;

        /// <summary>生出一隻變種怪（主機）</summary>
        public Fighter SpawnVariant(Vector3 at, VariantDef v)
        {
            float hp = v.hp, dmg = v.dmg, spd = v.speed;
            if (v.assassin) { dmg *= AssassinDmg; spd *= AssassinSpeed; }
            var m = SpawnMinion(at, 0, v.baseKind, HpMul * hp, DmgMul * dmg, SpeedMul * spd);
            ApplyVariant(m, v.id);
            return m;
        }

        /// <summary>套上變種的身分（主機生出來時、隊友端收到代理時都呼叫）</summary>
        public void ApplyVariant(Fighter m, int id)
        {
            var v = MonsterVariants.Get(id);
            if (v == null || m.variant == id) return;
            m.variant = id; m.name = v.name;
            if (v.healer) { m.healer = true; m.attackReadyAt = now + HealerFirst; Emit("healer", m.Feet, v.tint, null); return; }
            if (v.summoner) { m.summoner = true; m.attackReadyAt = now + SummonerFirst; Emit("summoner", m.Feet, v.tint, null); return; }
            if (v.summon) { m.summoned = true; return; }
            if (v.assassin)
            {
                m.assassin = true; m.asnState = 0; m.asnUntil = now + AssassinLurk;
                m.fuseAt = 0f; m.hidden = false;
                Emit("assassin", m.Feet, new Color(0.9f, 0.2f, 0.35f), $"🗡 {v.name}潛伏在敵陣後方……小心背後！");
            }
            else Emit("weathermonster", m.Feet, v.tint, $"{WeatherName(v.weather)}裡出現了「{v.name}」！");
        }

        // ------------------------------------------------------------ 天候讓原本的怪物表現不同（生出來時套用）
        static readonly int[] FireKinds = { 0, 1, 3, 5 }, WaterKinds = { 13, 15, 17, 18, 20 }, PlantKinds = { 7, 9, 10, 11, 12, 19 }, BeastKinds = { 2, 8, 13, 16 };
        static bool In(int[] a, int k) => System.Array.IndexOf(a, k) >= 0;
        /// <summary>天候對這種怪的影響說明（HUD／提示用）</summary>
        public static string WeatherTraitText(Weather w) => w switch
        {
            Weather.Rain => "火系怪物變慢、變弱，自爆小鬼的引信變長；水裡的怪物更快；植物更耐打",
            Weather.Fog => "近戰怪物趁霧逼近（移動 +15%）",
            Weather.Wind => "飛鳥、跳躍魚乘風更快",
            Weather.Moon => "野獸（衝鋒獸、跳躍魚、寄居蟹）更快、更兇",
            _ => "",
        };
        void ApplyWeatherTraits(Fighter m)
        {
            if (weather == Weather.None || m.variant > 0 || EliteCatalog.IsElite(m.minionKind)) return;
            int k = m.minionKind;
            switch (weather)
            {
                case Weather.Rain:
                    if (In(FireKinds, k)) { m.speedMul *= 0.8f; m.dmgMul *= 0.85f; if (k == 3) m.fuseAt = now + BombFuse * 1.5f; }
                    else if (In(WaterKinds, k)) m.speedMul *= 1.3f;
                    else if (In(PlantKinds, k)) { m.maxHp = Mathf.RoundToInt(m.maxHp * 1.2f); m.hp = m.maxHp; }
                    break;
                case Weather.Fog: if (!IsRanged(k)) m.speedMul *= 1.15f; break;
                case Weather.Wind: if (k == 14) m.speedMul *= 1.5f; else if (k == 13) m.speedMul *= 1.25f; break;
                case Weather.Moon: if (In(BeastKinds, k)) { m.speedMul *= 1.3f; m.dmgMul *= 1.1f; } break;
            }
        }

        // ------------------------------------------------------------ 出現時機（UpdateEndless 每幀呼叫，主機）
        void UpdateVariantSpawns()
        {
            if (remoteEnemy || edgePoint == null || manualWaves && awaitingNext || stageCleared) return;
            // 刺客：這一波開始 2.5 秒後（其他小怪先出來），從敵陣最後方的場邊冒出；副本人多（3 人以上）出兩隻
            if (assassinDef != null)
            {
                if (assassinWave != wave) { assassinWave = wave; assassinAt = now + 2.5f; }
                if (assassinAt > 0f && now >= assassinAt)
                {
                    assassinAt = -1f;
                    int n = dungeonParty >= 3 ? 2 : 1;
                    for (int i = 0; i < n; i++) SpawnVariant(RearPoint(i), assassinDef);
                }
            }
            // 天候怪：每一波一半機率，波次開始後 4～9 秒從場邊出來
            var wv = MonsterVariants.ForWeather(weather);
            if (wv != null)
            {
                if (weatherWave != wave) { weatherWave = wave; weatherAt = rng.NextDouble() < WeatherMonsterChance ? now + 4f + 5f * (float)rng.NextDouble() : -1f; }
                if (weatherAt > 0f && now >= weatherAt) { weatherAt = -1f; SpawnVariant(edgePoint(), wv); }
            }
        }

        /// <summary>敵陣的最後方：從玩家看過去、其他小怪的後面，場地邊緣外一點</summary>
        Vector3 RearPoint(int i)
        {
            ArenaFrame(out var c, out float R);
            Vector3 mid = Vector3.zero; int n = 0;
            foreach (var o in minions) if (o.Alive && !o.treasure && !o.assassin) { mid += o.Feet; n++; }
            var me = player.Alive ? player : null;
            if (me == null) foreach (var a in allies) if (a.Alive) { me = a; break; }
            Vector3 dir = Vector3.zero;
            if (n > 0 && me != null) dir = Fighter.Flat(mid / n - me.Feet);
            if (dir.sqrMagnitude < 1e-4f && me != null) dir = Fighter.Flat(me.forward);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir = RotateY(dir.normalized, i == 0 ? 0f : 25f * (i % 2 == 0 ? 1 : -1));
            return c + dir * (R + 0.8f);
        }

        // ------------------------------------------------------------ 刺客的 AI
        /// <summary>刺客在繞路（離目標遠、沒有在出手）：畫面上只看得到眼睛</summary>
        public static bool AssassinSneaking(Fighter m, Fighter viewer) => m.assassin && m.windupUntil <= 0f && (viewer == null || FlatDistance(m.Feet, viewer.Feet) > 3f);

        static Vector3 XZ(Vector3 v) { v.y = 0f; return v; }   // 只去掉高度（Fighter.Flat 會順便變成單位向量）
        void UpdateAssassin(Fighter m, float dt)
        {
            var tgt = m.asnTarget;
            if (tgt == null || !tgt.Alive)
            {
                var list = new List<Fighter>(); foreach (var p in Players()) if (p.Alive) list.Add(p);
                if (list.Count == 0) return;
                tgt = m.asnTarget = list[rng.Next(list.Count)];
            }
            m.target = tgt;
            ArenaFrame(out var c, out float R);
            float ring = Mathf.Max(1.5f, R - 0.6f);
            float speed = minionSpeed * MinionSpeed[m.minionKind] * m.speedMul * EnemySpeedScale, sprint = speed * AssassinSprint;
            var fwd = Fighter.Flat(tgt.forward); if (fwd.sqrMagnitude < 1e-4f) fwd = Fighter.Flat(tgt.Feet - m.Feet); fwd = fwd.normalized;
            // 出手中：面向鎖住，時間到就砍
            if (m.windupUntil > 0f)
            {
                m.forward = m.lockFwd;
                if (now < m.windupUntil) return;
                m.windupUntil = 0f; m.attackReadyAt = now + AssassinRecover;
                var claw = Skills.All["m_claw"];
                if (FlatDistance(m.Feet, tgt.Feet) <= AssassinReach + tgt.bodyRadius + 0.5f) HitFoe(m, tgt, claw, Mathf.RoundToInt(claw.damage * m.dmgMul));
                Emit("minionattack", m.Feet, new Color(0.9f, 0.2f, 0.35f), null);
                m.asnState = 4;
                return;
            }
            switch (m.asnState)
            {
                case 0:   // 潛伏：在敵陣後方停一下
                    m.forward = Fighter.Flat(tgt.Feet - m.Feet);
                    if (now >= m.asnUntil) m.asnState = 1;
                    return;
                case 1:   // 沿著邊緣繞到目標的背後那一面
                {
                    var behind = Fighter.Flat(tgt.Feet - fwd * 3f - c);
                    if (behind.sqrMagnitude < 1e-4f) behind = -fwd;
                    var cur = XZ(m.Feet - c); if (cur.sqrMagnitude < 1e-4f) cur = -behind;
                    float aCur = Mathf.Atan2(cur.x, cur.z) * Mathf.Rad2Deg, aTgt = Mathf.Atan2(behind.x, behind.z) * Mathf.Rad2Deg;
                    float delta = Mathf.DeltaAngle(aCur, aTgt);
                    float r = cur.magnitude;
                    float stepDeg = sprint * dt / Mathf.Max(0.5f, r) * Mathf.Rad2Deg;
                    float a = (aCur + Mathf.Clamp(delta, -stepDeg, stepDeg)) * Mathf.Deg2Rad;
                    float nr = Mathf.MoveTowards(r, ring, sprint * dt);
                    var next = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * nr;
                    var mv = XZ(next - m.Feet);
                    if (mv.sqrMagnitude > 1e-6f) m.forward = mv.normalized;
                    m.head = new Vector3(next.x, m.head.y, next.z);
                    if (Mathf.Abs(delta) < 12f && Mathf.Abs(nr - ring) < 0.3f) { m.asnState = 2; m.asnUntil = now + AssassinApproachMax; }
                    return;
                }
                case 2:   // 從背後衝進來
                {
                    var goal = tgt.Feet - fwd * 0.7f;
                    var to = XZ(goal - m.Feet);
                    float dist = FlatDistance(m.Feet, tgt.Feet);
                    if (dist <= AssassinReach + tgt.bodyRadius && now >= m.attackReadyAt)
                    {
                        m.windupUntil = now + AssassinWindup; m.lockFwd = Fighter.Flat(tgt.Feet - m.Feet).normalized;
                        Emit("minionwindup", m.Feet, new Color(0.9f, 0.2f, 0.35f), null);
                        Emit("assassinwarn", tgt.Feet, new Color(0.9f, 0.2f, 0.35f), tgt == player ? "1" : "0");
                        return;
                    }
                    if (now >= m.asnUntil) { m.asnState = 4; return; }   // 一直追不到：退回去重新繞
                    if (to.magnitude > 0.05f) { m.head += to.normalized * Mathf.Min(sprint * dt, to.magnitude); m.forward = to.normalized; }
                    return;
                }
                default:  // 退回邊緣，再繞一次
                {
                    var away = Fighter.Flat(m.Feet - c); if (away.sqrMagnitude < 1e-4f) away = -fwd;
                    away = away.normalized;
                    var next = m.Feet + away * sprint * dt;
                    m.head = new Vector3(next.x, m.head.y, next.z); m.forward = away;
                    if (FlatDistance(m.Feet, c) >= ring - 0.2f) m.asnState = 1;
                    return;
                }
            }
        }
    
        // ------------------------------------------------------------ 補師的 AI（第四、五章）
        public const float HealerEvery = 5f, HealerFirst = 2.5f, HealerCast = 1.2f, HealerRange = 7f, HealerFrac = 0.25f, HealerKeep = 5f;
        /// <summary>出一隻這一章的補師（輪次出怪的陣形裡）；額外多的，不算在這一波的數量裡</summary>
        Fighter SpawnHealerAt(float ang)
        {
            var def = MonsterVariants.HealerFor(Theme);
            if (def == null) return SpawnRoleAt(MinionRole.Back, ang, false);
            var m = SpawnVariant(edgePointAt != null ? edgePointAt(ang) : edgePoint(), def);
            lastMinionCount = minions.Count;
            return m;
        }

        void UpdateHealer(Fighter m, float dt)
        {
            Fighter near = null; float nd = float.MaxValue;
            foreach (var p in Players()) { if (!p.Alive) continue; float d = FlatDistance(m.Feet, p.Feet); if (d < nd) { nd = d; near = p; } }
            if (near == null) return;
            m.target = near;
            // 讀條中：時間到就補（被打中會在 Damage 裡打斷）
            if (m.windupUntil > 0f)
            {
                m.forward = m.lockFwd;
                if (now < m.windupUntil) return;
                m.windupUntil = 0f; m.attackReadyAt = now + HealerEvery;
                var t = m.healTarget;
                if (t != null && t.Alive && FlatDistance(t.Feet, m.Feet) <= HealerRange + 2f && t.hp < t.maxHp)
                {
                    float amt = Mathf.Min(t.maxHp - t.hp, t.maxHp * HealerFrac);
                    t.hp += amt;
                    Emit("enemyheal", t.Chest, new Color(0.45f, 1f, 0.55f), $"+{amt:0}");
                }
                return;
            }
            // 找最需要補的同伴（血量比例最低、低於九成、在範圍內；不補自己、寶箱怪）
            Fighter best = null; float bestK = 0.9f;
            foreach (var o in minions)
            {
                if (o == m || !o.Alive || o.treasure || o.hp >= o.maxHp) continue;
                float k = o.hp / Mathf.Max(1f, o.maxHp);
                if (k < bestK && FlatDistance(o.Feet, m.Feet) <= HealerRange) { bestK = k; best = o; }
            }
            bool inside = minionInside == null || minionInside(m.Feet);
            if (best != null && inside && now >= m.attackReadyAt)
            {
                m.healTarget = best; m.windupUntil = now + HealerCast; m.lockFwd = Fighter.Flat(best.Feet - m.Feet);
                Emit("healcast", m.Feet, new Color(0.45f, 1f, 0.55f), $"⚕ {m.name}在替{best.name}治療！打斷牠");
                return;
            }
            KeepBehind(m, near, nd, inside, dt);
        }

        /// <summary>後排（補師、召喚師）的走位：站在同伴的後面（從最近的玩家看過去），跟玩家保持 5m 以上；玩家靠近就退</summary>
        void KeepBehind(Fighter m, Fighter near, float nd, bool inside, float dt)
        {
            Vector3 mid = Vector3.zero; int n = 0;
            foreach (var o in minions) if (o != m && o.Alive && !o.treasure && !o.healer && !o.summoner) { mid += o.Feet; n++; }
            Vector3 goal = n > 0 ? mid / n : m.Feet;
            var away = Fighter.Flat(goal - near.Feet); if (away == Vector3.zero) away = Fighter.Flat(m.Feet - near.Feet);
            goal += away * 2f;
            if (FlatDistance(goal, near.Feet) < HealerKeep) goal = near.Feet + away * HealerKeep;
            if (minionInside != null && !minionInside(goal)) goal = Vector3.Lerp(goal, m.Feet, 0.5f);
            var to = XZ(goal - m.Feet);
            float speed = minionSpeed * MinionSpeed[m.minionKind] * m.speedMul * EnemySpeedScale * (nd < 3f ? 1.8f : 1f) * (inside ? 1f : 2f);
            if (to.magnitude > 0.15f)
            {
                var step = to.normalized * Mathf.Min(speed * dt, to.magnitude);
                var next = m.Feet + step;
                if (!inside || minionInside == null || minionInside(next)) m.head += step;
                m.forward = Fighter.Flat(near.Feet - m.Feet);
            }
            else m.forward = Fighter.Flat(near.Feet - m.Feet);
        }

        // ------------------------------------------------------------ 召喚師的 AI（第五章）
        public const float SummonerEvery = 9f, SummonerFirst = 3f, SummonerCast = 1.5f;
        public const int SummonPerCast = 2, SummonMaxAlive = 4;
        Fighter SpawnSummonerAt(float ang)
        {
            var def = MonsterVariants.SummonerFor(Theme);
            if (def == null) return null;
            var m = SpawnVariant(edgePointAt != null ? edgePointAt(ang) : edgePoint(), def);
            lastMinionCount = minions.Count;
            return m;
        }

        readonly List<(Vector3 at, VariantDef def, Fighter owner)> pendingVariantSpawns = new List<(Vector3, VariantDef, Fighter)>();
        /// <summary>UpdateMinions 開頭：把上一幀召喚師叫的砂靈生出來</summary>
        void FlushVariantSpawns()
        {
            foreach (var (at, def, owner) in pendingVariantSpawns)
            {
                var s = SpawnVariant(at, def);
                s.summonedBy = owner; s.attackReadyAt = now + 1.0f;
                Emit("summonpop", s.Feet, def.tint, null);
            }
            pendingVariantSpawns.Clear();
        }

        void UpdateSummoner(Fighter m, float dt)
        {
            Fighter near = null; float nd = float.MaxValue;
            foreach (var p in Players()) { if (!p.Alive) continue; float d = FlatDistance(m.Feet, p.Feet); if (d < nd) { nd = d; near = p; } }
            if (near == null) return;
            m.target = near;
            bool inside = minionInside == null || minionInside(m.Feet);
            if (m.windupUntil > 0f)
            {
                m.forward = m.lockFwd;
                if (now < m.windupUntil) return;
                m.windupUntil = 0f; m.attackReadyAt = now + SummonerEvery;
                var def = MonsterVariants.SummonFor(Theme) ?? MonsterVariants.SummonFor(4);
                int alive = 0; foreach (var o in minions) if (o.Alive && o.summonedBy == m) alive++;
                foreach (var q in pendingVariantSpawns) if (q.owner == m) alive++;
                for (int i = 0; i < SummonPerCast && alive < SummonMaxAlive && def != null && minions.Count < MaxMinions * 3; i++, alive++)
                {
                    var side = RotateY(Fighter.Flat(near.Feet - m.Feet), i == 0 ? 60f : -60f);
                    var at = m.Feet + side * 0.9f;
                    if (minionInside != null && !minionInside(at)) at = m.Feet;
                    pendingVariantSpawns.Add((at, def, m));   // 下一幀才生（現在正在走訪 minions）
                }
                return;
            }
            int mine = 0; foreach (var o in minions) if (o.Alive && o.summonedBy == m) mine++;
            if (inside && now >= m.attackReadyAt && mine < SummonMaxAlive)
            {
                m.windupUntil = now + SummonerCast; m.lockFwd = Fighter.Flat(near.Feet - m.Feet);
                Emit("summoncast", m.Feet, new Color(0.78f, 0.55f, 1f), $"🔮 {m.name}在召喚！打斷牠");
                return;
            }
            KeepBehind(m, near, nd, inside, dt);
        }
}
}
