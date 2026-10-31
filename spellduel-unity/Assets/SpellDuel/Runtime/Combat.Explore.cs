using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    public partial class Fighter
    {
        /// <summary>故事探索：這隻小怪對應地圖上的哪一隻（null＝不是探索的怪）</summary>
        public ExMonster exMon;
        /// <summary>發現玩家了沒（沒發現＝照巡邏路線走、不攻擊）</summary>
        public bool exAware;
        public int exPatrolIdx, exPatrolDir = 1;
        public float exLastHp = -1f;
        public float exPathAt; public Vector3 exNext; public bool exHasNext;
    }

    /// <summary>
    /// 故事探索的戰鬥接線（使用者要求）：
    ///   地圖上的怪只在玩家附近（ExActivate 公尺內）才放上戰場，離開後收起來（記住血量）；
    ///   一開始都沒發現玩家：照巡邏路線走、不攻擊；玩家走進 2.5m（或打牠）才發現，開始主動攻擊；
    ///   會叫同伴的（營地的第一隻）發現玩家時，附近 ExCallRange 內的怪全部一起發現；同一個營地一隻發現就全部發現；
    ///   小 Boss 在競技場中央，玩家走進競技場才醒；最終 Boss 走進王座才出現。
    ///   怪物只能走在迷宮的走道上（minionInside＝ExploreMap.Walkable）。
    /// </summary>
    public partial class Battle
    {
        public ExploreRun explore;
        /// <summary>被擊中或被擊殺時，這個範圍內的其他怪也會發現玩家（使用者要求）</summary>
        public const float ExNoiseRange = 4f;
        public const float ExActivate = 16f, ExDespawn = 24f, ExDetect = 2.5f, ExCallRange = 9f, ExPatrolSpeed = 0.55f;
        /// <summary>追擊：離玩家超過 ExChaseNear（或中間隔著牆）就沿迷宮走道追，速度 ExChaseSpeed（比衝刺慢，玩家停下來就會被追上）</summary>
        public const float ExChaseSpeed = 1.6f, ExChaseNear = 4f, ExRangedNear = 6.5f;
        /// <summary>每一區的敵人強度（波次）：越深越強</summary>
        public int exBaseWave = 8;
        public const int ExWavePerZone = 3;
        public const float GuardianHpMul = 2.5f, RareHpMul = 2f;
        /// <summary>最終 Boss 的血量（SoloBattle 照第二章 Boss 設定）</summary>
        public float exBossHp = 900f;
        readonly Dictionary<ExMonster, Fighter> exActive = new Dictionary<ExMonster, Fighter>();
        public Fighter exEscort;
        bool exBossSpawned;
        /// <summary>畫面：怪物發現玩家（呼朋引伴時 true）</summary>
        public event System.Action<Fighter, bool> OnExAlert;

        /// <summary>開始一次探索：不自動出怪（手動模式），怪物只能走走道，主題＝叢林</summary>
        public void StartExplore(ExploreRun run)
        {
            explore = run;
            StartDungeonMap();          // 手動模式：不自動出怪、沒有 Boss
            finalRoom = false;
            themeOverride = 1;           // 叢林（第二章的森林）
            minionInside = p => run.map.Walkable(p, 0.45f);
            attackHalf = 0f;
            exActive.Clear(); exEscort = null; exBossSpawned = false;
        }

        Fighter ExSpawn(ExMonster mon)
        {
            wave = exBaseWave + mon.zone * ExWavePerZone;
            float hp = HpMul * (mon.guardian > 0 ? GuardianHpMul : mon.rare ? RareHpMul : 1f) * mon.hpFrac;
            var m = SpawnMinion(mon.home, 0, mon.kind, hp, DmgMul, SpeedMul);
            if (mon.hpFrac < 1f) { m.maxHp = Mathf.RoundToInt(m.maxHp / mon.hpFrac); m.hp = m.maxHp * mon.hpFrac; }
            m.exMon = mon; m.exAware = false; m.exPatrolIdx = 0; m.exPatrolDir = 1;
            m.attackReadyAt = now + 1f;
            if (mon.guardian > 0) m.name = (EliteCatalog.Get(mon.kind)?.name ?? m.name) + "（守衛）";
            exActive[mon] = m;
            return m;
        }

        void ExStow(ExMonster mon, Fighter m)
        {
            mon.hpFrac = m.maxHp > 0 ? Mathf.Clamp01(m.hp / m.maxHp) : 1f;
            minions.Remove(m); exActive.Remove(mon);
        }

        /// <summary>發現玩家：caller＝會叫同伴（附近的怪、同一營地的全部一起發現）</summary>
        void ExAlert(Fighter m, bool spread = true)
        {
            if (m.exAware) return;
            m.exAware = true; m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + 0.6f);
            var mon = m.exMon;
            bool call = spread && mon != null && mon.caller;
            Emit("exalert", m.Chest + Vector3.up * 0.3f, new Color(1f, 0.4f, 0.3f), call ? "❗ 呼叫同伴！" : "❗");
            OnExAlert?.Invoke(m, call);
            if (!spread || mon == null) return;
            foreach (var o in explore.map.monsters)
            {
                if (o == mon || o.dead || o.hidden || o.guardian > 0) continue;
                bool same = mon.camp >= 0 && o.camp == mon.camp;
                bool near = call && FlatDistance(o.home, m.Feet) <= ExCallRange && ExSight(exActive.TryGetValue(o, out var oa) ? oa.Feet : o.home, m.Feet);   // 叫同伴：隔著牆聽不到
                if (!same && !near) continue;
                if (!exActive.TryGetValue(o, out var om)) om = ExSpawn(o);
                ExAlert(om, false);
            }
        }

        /// <summary>m 被打中或被打倒：ExNoiseRange 內的其他怪也發現玩家（還沒放上戰場的先放上來；守衛、稀有怪不算）</summary>
        void ExNoise(Fighter m)
        {
            var at = m.Feet;
            foreach (var o in explore.map.monsters)
            {
                if (o == m.exMon || o.dead || o.hidden || o.guardian > 0) continue;
                exActive.TryGetValue(o, out var om);
                var p = om != null ? om.Feet : o.home;
                if (FlatDistance(p, at) > ExNoiseRange || !ExSight(p, at)) continue;   // 隔著牆不會被吵醒
                if (om == null) om = ExSpawn(o);
                if (!om.exAware) ExAlert(om, true);   // 被吵醒的如果是會叫同伴的，一樣會叫
            }
        }

        /// <summary>
        /// 視線：a 看不看得到 b（警戒、叫同伴、被吵醒、攻擊打不打得到都用這個；使用者要求不能穿牆）。
        /// 只看牆格、還關著的門／封印、景物本體（不算走路用的離牆緩衝，貼著牆打旁邊的怪照樣打得到）；頭尾 0.4m 是身體不算。
        /// </summary>
        public bool ExSight(Vector3 a, Vector3 b)
        {
            if (explore == null) return true;
            var map = explore.map; float d = FlatDistance(a, b);
            for (float t = 0.4f; t < d - 0.4f; t += 0.25f)
            {
                var p = Vector3.Lerp(a, b, t / d); p.y = 0f; var c = map.Cell(p);
                if (!map.Open(c.x, c.y) || map.PropAt(p, 0f) != null) return false;
            }
            return true;
        }

        /// <summary>a 到 b 之間有沒有牆擋著（每 0.5m 取樣）</summary>
        bool ExLineClear(Vector3 a, Vector3 b)
        {
            var map = explore.map; float d = FlatDistance(a, b);
            for (float t = 0.5f; t < d; t += 0.5f) if (!map.Walkable(Vector3.Lerp(a, b, t / d), 0.2f)) return false;
            return true;
        }

        /// <summary>迷宮格子上從 from 走到 to 的下一格（BFS；走不到＝false）</summary>
        bool ExNextStep(Vector2Int from, Vector2Int to, out Vector2Int next)
        {
            var map = explore.map; next = from;
            if (from == to) return true;
            var prev = new Dictionary<Vector2Int, Vector2Int>(); var q = new Queue<Vector2Int>();
            q.Enqueue(to); prev[to] = to;   // 從目標往回找：找到 from 時，prev[from] 就是下一步
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == from) { next = prev[c]; return true; }
                foreach (var d in ExploreMap.Dirs) { var n = c + d; if (prev.ContainsKey(n) || !(map.Open(n.x, n.y) || n == from)) continue; prev[n] = c; q.Enqueue(n); }
            }
            return false;
        }

        /// <summary>
        /// 發現玩家的怪：離得遠或中間隔著牆時，沿著迷宮走道追過來（回傳 true＝這一幀由這裡移動，不跑原本的 AI）；
        /// 夠近而且看得到玩家就交給原本的 AI（走位、出手）。
        /// </summary>
        bool ExChase(Fighter m, Fighter tgt, float dt)
        {
            if (tgt == null || m.leapEnd > 0f) return false;
            float d = FlatDistance(m.Feet, tgt.Feet);
            float near = IsRanged(m.minionKind) ? ExRangedNear : ExChaseNear;
            if (d <= near && ExLineClear(m.Feet, tgt.Feet)) return false;
            if (now >= m.exPathAt || !m.exHasNext)
            {
                m.exPathAt = now + 0.4f;
                var map = explore.map;
                m.exHasNext = ExNextStep(map.Cell(m.Feet), map.Cell(tgt.Feet), out var nc);
                m.exNext = m.exHasNext ? (nc == map.Cell(m.Feet) ? tgt.Feet : map.World(nc.x, nc.y)) : tgt.Feet;
            }
            var to = Fighter.Flat(m.exNext - m.Feet);
            if (to.magnitude < 0.25f) { m.exPathAt = 0f; return true; }   // 到了這一格的中心：下一幀重算
            m.windupUntil = 0f;
            m.forward = to.normalized;
            var step = m.Feet + to.normalized * Mathf.Min(to.magnitude, ExChaseSpeed * Mathf.Max(0.5f, m.speedMul) * dt);
            if (minionInside == null || minionInside(step) || !minionInside(m.Feet)) m.head = new Vector3(step.x, m.head.y, step.z);
            else m.head = new Vector3(m.head.x + (m.exNext.x - m.Feet.x) * 0.2f, m.head.y, m.head.z + (m.exNext.z - m.Feet.z) * 0.2f);   // 貼著牆角：往格子中心滑過去
            return true;
        }

        /// <summary>沒發現玩家：照巡邏路線來回走（營地守衛站著不動，只會轉頭）</summary>
        void ExPatrol(Fighter m, float dt)
        {
            m.windupUntil = 0f;
            var p = m.exMon.patrol;
            if (p == null || p.Length < 2) return;
            var to = p[Mathf.Clamp(m.exPatrolIdx, 0, p.Length - 1)];
            var d = Fighter.Flat(to - m.Feet);
            if (d.magnitude < 0.3f)
            {
                if (m.exPatrolIdx + m.exPatrolDir >= p.Length || m.exPatrolIdx + m.exPatrolDir < 0) m.exPatrolDir = -m.exPatrolDir;   // 走到底就往回走
                m.exPatrolIdx += m.exPatrolDir;
                return;
            }
            m.forward = d.normalized;
            var next = m.Feet + d.normalized * Mathf.Min(d.magnitude, ExPatrolSpeed * m.speedMul * dt);
            if (minionInside == null || minionInside(next)) m.head = new Vector3(next.x, m.head.y, next.z);
        }

        void UpdateExplore(float dt)
        {
            var run = explore; if (run == null || player == null) return;
            var pf = player.Feet;
            foreach (var mon in run.map.monsters)
            {
                if (mon.dead || mon.hidden) continue;
                float d = FlatDistance(mon.home, pf);
                exActive.TryGetValue(mon, out var m);
                if (m == null)
                {
                    if (d <= ExActivate) m = ExSpawn(mon);
                    else continue;
                }
                if (!m.Alive) { exActive.Remove(mon); run.OnMonsterKilled(mon); ExNoise(m); continue; }
                if (m.exLastHp >= 0f && m.hp < m.exLastHp - 0.01f) ExNoise(m);   // 被打中：附近的怪聽到動靜
                m.exLastHp = m.hp;
                float md = FlatDistance(m.Feet, pf);
                if (!m.exAware)
                {
                    if (md > ExDespawn && d > ExDespawn) { ExStow(mon, m); continue; }
                    bool hurt = m.hp < m.maxHp - 0.01f;
                    if (mon.guardian > 0) { var a = run.map.arenas.Find(x => x.n == mon.guardian); if (a.entered || hurt) ExAlert(m); }
                    else if (md <= ExDetect + m.bodyRadius && ExSight(m.Feet, pf) || hurt) ExAlert(m);   // 警戒範圍不穿牆
                }
                // 發現玩家之後就一直追，直到追上為止（使用者要求）：不會放棄、也不會收起來
            }
            // 護送：接了任務就叫探險家出來跟著；送到或倒下就收起來
            if (run.escortFollowing && (exEscort == null || !exEscort.Alive))
            {
                if (exEscort != null && !exEscort.Alive) { npcs.Remove(exEscort); exEscort = null; run.OnEscortDown(); }
                else { var n = run.map.Npc("lost"); exEscort = SpawnNpc("lost", n.name, n.look, 0, true, 60, 0, n.pos); }
            }
            else if (!run.escortFollowing && exEscort != null) { npcs.Remove(exEscort); exEscort = null; }
            // 最終 Boss：走進王座才出現
            var throne = run.map.arenas.Find(x => x.n == 4);
            if (!exBossSpawned && throne.entered)
            {
                exBossSpawned = true;
                wave = BossEvery * (1 + exBaseWave / BossEvery + 3);
                bossPoint = () => throne.center + Vector3.forward * 3f;
                bossAfterMinions = true;
                storyBossHp = exBossHp;   // 最終 Boss：指定血量（難度再照等級加成）
                SpawnBoss();
            }
            if (exBossSpawned && !enemy.Alive && !run.bossDead) run.OnBossKilled();
            // 森林之靈的祝福：補滿血、攻擊 +25%（2 分鐘）
            if (run.spiritBlessing)
            {
                run.spiritBlessing = false;
                player.hp = player.maxHp; player.mp = player.maxMp;
                player.teamAtkUntil = now + 120f; player.teamAtk = 0.25f;
                Emit("heal", player.Chest, new Color(0.5f, 1f, 0.6f), "森林之靈的祝福：HP、MP 全滿，攻擊 +25%");
            }
            if (run.finished && !stageCleared) { stageCleared = true; Emit("stageclear", player.Feet, Color.white, "探索完成！"); }
        }

        /// <summary>探索中，畫面要知道某個地圖怪現在在戰場上的樣子（沒放上戰場＝null）</summary>
        public Fighter ExFighter(ExMonster mon) => exActive.TryGetValue(mon, out var m) ? m : null;
    }
}
