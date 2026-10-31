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
        // 沒發現玩家時的日常（使用者要求：自己做些事、互相互動，自然一點）
        public int exIdleKind;            // 0 沒事、1 張望、2 覓食（低頭咀嚼）、3 和同伴聊天、4 打盹
        public float exIdleUntil, exIdleCd, exEmoteAt;
        public Vector3 exLookBase; public Fighter exTalkTo;
        public bool exSleep;              // 營地裡打盹的：警戒範圍減半，被吵醒或打到就醒
        public bool exWaiting;
        public string[] exConvo; public int exConvoLine; public bool exConvoLead;   // 聊天的台詞（帶頭的那隻負責輪流念）
        public float exAlertAt = -99f;     // 發現玩家的時間：先冒驚嘆號、愣一下才開始追和攻擊            // 在等同伴（兩隻一起巡邏的）：同伴不會反過來等牠（避免互等卡死）
        public bool exForage => exIdleKind == 2;
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
        /// <summary>疾風稀有怪的追擊速度（m/s）：比探索衝刺（2.5×1.5＝3.75）快一點，甩不掉</summary>
        public const float ExSwiftChase = 4.1f;
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
        /// <summary>探索的怪說話（對話框）：說話的怪、內容</summary>
        public event System.Action<Fighter, string> OnExSay;
        public const float ExSurprise = 0.8f;   // 發現玩家後愣住幾秒才開始追／攻擊

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
            var sv = mon.variant > 0 ? MonsterVariants.Get(mon.variant) : null;
            var m = sv != null ? SpawnMinion(mon.home, 0, sv.baseKind, hp * sv.hp, DmgMul * sv.dmg, SpeedMul * sv.speed) : SpawnMinion(mon.home, 0, mon.kind, hp, DmgMul, SpeedMul);
            if (sv != null) ApplyVariant(m, sv.id);
            if (mon.hpFrac < 1f) { m.maxHp = Mathf.RoundToInt(m.maxHp / mon.hpFrac); m.hp = m.maxHp * mon.hpFrac; }
            m.exMon = mon; m.exAware = false; m.exPatrolIdx = 0; m.exPatrolDir = 1;
            m.exIdleKind = 0; m.exIdleUntil = 0f; m.exIdleCd = now + 1f + (float)rng.NextDouble() * 3f; m.exTalkTo = null;
            m.exSleep = mon.camp >= 0 && !mon.caller && mon.patrol == null && rng.Next(4) == 0;   // 營地四隻裡大約一隻在打盹（叫同伴的那隻不睡）
            if (m.exSleep) { m.exIdleKind = 4; m.exIdleUntil = float.MaxValue; }
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
            m.exAware = true; m.exAlertAt = now; m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + ExSurprise + 0.4f);   // 先冒驚嘆號、愣一下才攻擊
            if (m.exSleep) Emit("exemote", m.head + Vector3.up * 0.5f, new Color(1f, 0.9f, 0.5f), "！？");   // 睡夢中被吵醒
            m.exSleep = false; m.exIdleKind = 0; m.exIdleUntil = 0f; m.exTalkTo = null; m.exConvo = null;
            if (rng.Next(3) == 0) ExSay(m, Pick(ExSpotLines));   // 有時候喊一句
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
            if (now - m.exAlertAt < ExSurprise) { var f = Fighter.Flat(tgt.Feet - m.Feet); if (f.sqrMagnitude > 1e-4f) m.forward = f.normalized; m.windupUntil = 0f; return true; }   // 剛發現：轉向玩家愣一下
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
            var step = m.Feet + to.normalized * Mathf.Min(to.magnitude, (m.swift ? ExSwiftChase * Mathf.Min(1f, m.speedMul / 1.5f + 0.34f) : ExChaseSpeed * Mathf.Max(0.5f, m.speedMul)) * dt);
            if (minionInside == null || minionInside(step) || !minionInside(m.Feet)) m.head = new Vector3(step.x, m.head.y, step.z);
            else m.head = new Vector3(m.head.x + (m.exNext.x - m.Feet.x) * 0.2f, m.head.y, m.head.z + (m.exNext.z - m.Feet.z) * 0.2f);   // 貼著牆角：往格子中心滑過去
            return true;
        }

        /// <summary>沒發現玩家：照巡邏路線來回走（營地守衛站著不動，只會轉頭）</summary>
        // ---------------------------------------------------------------- 沒發現玩家時的日常
        static readonly string[] ExChat = { "嘰嘰？", "咕嚕咕嚕", "嘿嘿", "♪", "……", "嘎！", "哼哼", "呼～", "唔？", "嘻嘻" };
        static readonly string[] ExMutter = { "……", "♪", "呼～", "嗯？", "哈啊～" };
        const float ExChatRange = 2.8f, ExEmoteShow = 14f;

        const float ExLineTime = 2.3f;   // 對話框每句停留的時間
        public const float ExSayShow = 8f;   // 對話框：玩家 8m 內才看得到（使用者要求）
        void ExSay(Fighter m, string text)
        {
            if (player == null || FlatDistance(m.Feet, player.Feet) > ExSayShow) return;
            OnExSay?.Invoke(m, text);
        }

        // 對話內容與挑法在 Combat.ExploreTalk.cs（每一區不一樣）

        /// <summary>頭上冒一句（只在玩家附近冒，免得滿地都是字）</summary>
        void ExEmote(Fighter m, string text)
        {
            if (player == null || FlatDistance(m.Feet, player.Feet) > ExEmoteShow) return;
            m.exEmoteAt = now;
            Emit("exemote", m.head + Vector3.up * 0.45f, new Color(0.95f, 0.95f, 0.85f), text);
        }
        string Pick(string[] a) => a[rng.Next(a.Length)];
        static Vector3 TurnToward(Vector3 from, Vector3 to, float k) { var v = Vector3.Lerp(from, to, Mathf.Clamp01(k)); v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : to; }
        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void ExIdle(Fighter m, int kind, float dur)
        {
            m.exIdleKind = kind; m.exIdleUntil = now + dur; m.exLookBase = m.forward; m.exWaiting = false;
            if (kind == 2 && rng.Next(2) == 0) ExEmote(m, "嚼嚼");
            else if (kind == 1 && rng.Next(3) == 0) ExEmote(m, Pick(ExMutter));
        }

        /// <summary>還沒發現玩家：日常（張望、覓食、和同伴聊天、打盹）＋照路線巡邏</summary>
        void ExPatrol(Fighter m, float dt)
        {
            m.windupUntil = 0f;
            // 正在做某件事
            if (now < m.exIdleUntil)
            {
                switch (m.exIdleKind)
                {
                    case 1:   // 張望：左右慢慢轉頭
                    {
                        float a = Mathf.Sin(now * 1.3f + m.minionId) * 70f * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        var b = m.exLookBase.sqrMagnitude > 1e-4f ? m.exLookBase : Vector3.forward;
                        m.forward = new Vector3(b.x * ca + b.z * sa, 0f, -b.x * sa + b.z * ca);   // 繞 Y 軸轉（純 C#，測試跑得動）
                        break;
                    }
                    case 3:   // 聊天：面對面，輪流冒一句
                        if (m.exTalkTo == null || !m.exTalkTo.Alive || m.exTalkTo.exAware) { m.exIdleUntil = 0f; break; }
                        { var d = Fighter.Flat(m.exTalkTo.Feet - m.Feet); if (d.sqrMagnitude > 1e-4f) m.forward = TurnToward(m.forward, d.normalized, dt * 6f); }
                        if (m.exConvoLead && m.exConvo != null && now >= m.exEmoteAt)
                        {
                            // 帶頭的那隻照台詞輪流說：偶數句自己說、奇數句對方說
                            if (m.exConvoLine >= m.exConvo.Length) { m.exIdleUntil = 0f; m.exTalkTo.exIdleUntil = 0f; break; }
                            var who = m.exConvoLine % 2 == 0 ? m : m.exTalkTo;
                            ExSay(who, m.exConvo[m.exConvoLine]);
                            m.exConvoLine++; m.exEmoteAt = now + ExLineTime;
                        }
                        break;
                    case 4:   // 打盹
                        if (now - m.exEmoteAt > 3f) ExEmote(m, "💤");
                        break;
                }
                return;
            }
            if (m.exIdleKind != 0) { m.exIdleKind = 0; m.exTalkTo = null; m.exIdleCd = now + Rand(4f, 9f); }
            // 碰到同伴：停下來聊一下（兩隻一起；隔著牆不算）
            if (now >= m.exIdleCd)
            {
                foreach (var o in exActive.Values)
                {
                    if (o == m || !o.Alive || o.exAware || o.exIdleKind == 3 || o.exIdleKind == 4 || now < o.exIdleCd - 3f || o.exMon == null || o.exMon.guardian > 0) continue;   // 對方在張望、覓食也會被叫過來聊；剛聊完的不會馬上又聊
                    bool mate = m.exMon.camp >= 0 && o.exMon.camp == m.exMon.camp;   // 同一個營地的隔著篝火也能聊
                    bool pair = m.exMon.patrol != null && o.exMon.patrol == m.exMon.patrol;   // 一起巡邏的夥伴
                    if (FlatDistance(o.Feet, m.Feet) > (mate ? 6.5f : pair ? 4.5f : ExChatRange) || !ExSight(o.Feet, m.Feet)) continue;
                    // 挑一段對話：大多是閒聊，有時候聊到劇情
                    var convo = ExPickConvo(m.exMon.zone);
                    float dur = convo.Length * ExLineTime + 0.6f;
                    m.exTalkTo = o; o.exTalkTo = m;
                    m.exIdleKind = o.exIdleKind = 3; m.exIdleUntil = o.exIdleUntil = now + dur;
                    m.exConvo = convo; m.exConvoLine = 0; m.exConvoLead = true; o.exConvoLead = false; o.exConvo = null;
                    m.exEmoteAt = now + 0.3f; o.exEmoteAt = now;
                    return;
                }
            }
            var p = m.exMon.patrol;
            if (p == null || p.Length < 2)
            {
                // 不巡邏的（營地）：轉向營地中間（圍著篝火）、偶爾覓食或張望
                if (m.exMon.camp >= 0)
                {
                    Vector3 c = Vector3.zero; int n = 0;
                    foreach (var o in explore.map.monsters) if (o.camp == m.exMon.camp) { c += o.home; n++; }
                    var toC = Fighter.Flat(c / Mathf.Max(1, n) - m.Feet);
                    if (toC.sqrMagnitude > 0.04f) m.forward = TurnToward(m.forward, toC.normalized, dt * 2f);
                }
                if (now >= m.exIdleCd)
                {
                    int r = rng.Next(3);
                    if (r == 0) ExIdle(m, 2, Rand(3f, 5f)); else if (r == 1) ExIdle(m, 1, Rand(2f, 3.5f)); else { ExEmote(m, Pick(ExMutter)); m.exIdleCd = now + Rand(4f, 8f); }
                }
                return;
            }
            // 兩隻一起巡邏的：同伴停下來（張望、覓食）就等牠，不要自己走掉
            foreach (var o in exActive.Values)
                if (o != m && o.Alive && !o.exAware && o.exMon != null && o.exMon.patrol == p && (o.exIdleKind == 1 || o.exIdleKind == 2) && !o.exWaiting && FlatDistance(o.Feet, m.Feet) < 6f)
                { ExIdle(m, 1, Mathf.Clamp(o.exIdleUntil - now, 0.5f, 5f)); m.exWaiting = true; return; }
            var to = p[Mathf.Clamp(m.exPatrolIdx, 0, p.Length - 1)];
            var dd = Fighter.Flat(to - m.Feet);
            if (dd.magnitude < 0.3f)
            {
                if (m.exPatrolIdx + m.exPatrolDir >= p.Length || m.exPatrolIdx + m.exPatrolDir < 0) m.exPatrolDir = -m.exPatrolDir;   // 走到底就往回走
                m.exPatrolIdx += m.exPatrolDir;
                // 走到一個點：有時候停下來張望或低頭找東西吃
                int r = rng.Next(10);
                if (r < 3) ExIdle(m, 1, Rand(1.5f, 3f)); else if (r < 5) ExIdle(m, 2, Rand(2.5f, 4f));
                return;
            }
            m.forward = dd.normalized;
            var next = m.Feet + dd.normalized * Mathf.Min(dd.magnitude, ExPatrolSpeed * m.speedMul * dt);
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
                    else if (md <= ExDetect * (m.exSleep ? 0.5f : 1f) + m.bodyRadius && ExSight(m.Feet, pf) || hurt) ExAlert(m);   // 打盹的：要走得更近才會發現   // 警戒範圍不穿牆
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
