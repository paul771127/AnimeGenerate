using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 天穹飛毯（副本 sky_carpet，地點寫 carpet = 0～4）：特殊地點「carpet」。資料與判定公式在 Carpet.cs。
    ///   房主：排障礙（照站別的比例、間隔）、排登毯的敵人（載具演出 4 秒，第 3.2 秒落地才生出小怪）、飛艇 Boss 的招式、俯衝窗口、彈幕。
    ///   每支手機：判定自己的玩家有沒有躲過（跳／蹲看判定窗內的姿勢，半邊／缺口看碰到那一刻站的位置），撞到扣自己的血。
    ///   隊友端收到房主排好的障礙、登毯（SoloBattle.Carpet.cs 的 skyq／skyb 訊息），換成自己的時間。
    /// </summary>
    public partial class Battle
    {
        /// <summary>目前地點的飛毯站別（−1＝不是飛毯）；房主、隊友進地點時都會設定</summary>
        public int carpetStation = -1;
        /// <summary>飛毯的「前方」（世界座標、水平）：這一站開始時房主面對的方向，整站不變</summary>
        public Vector3 carpetFwd = Vector3.forward;
        public readonly List<SkyCue> skyCues = new List<SkyCue>();
        public readonly List<SkyBoard> skyBoards = new List<SkyBoard>();
        public event Action<SkyCue> OnSkyCue;
        public event Action<SkyBoard> OnSkyBoard;
        /// <summary>飛艇壓低的程度（0～Carpet.SwoopMax）與階段（2＝彈幕）：房主算，隊友收 skys 訊息</summary>
        public float carpetSwoop; public int carpetPhase = 1;
        public bool CarpetBoss => carpetStation == Carpet.BossStation;
        public bool CarpetSmall { get { CarpetFrame(out _, out float hw, out _, out _); return Carpet.Small(hw); } }

        int skyIds, skyWave, skyLastKind = -1, skyRepeats, skyCueCount, skyRound, skyBossTurn;
        float skyNextCue, skyNextBoard, skyStart, skyNextSwoop, skySwoopStart = -99f, skyNextPirate, skyNextBarrage, skyGroupPirateAt = -1f;
        bool skyBossSpawned;
        readonly List<(float at, int kind)> skySubQueue = new List<(float, int)>();
        readonly HashSet<int> skyGroupHit = new HashSet<int>(), skyGroupDodged = new HashSet<int>();
        int skyGroupIds;

        // ------------------------------------------------------------ 場地
        public void CarpetFrame(out Vector3 center, out float halfW, out float front, out float back) => Carpet.Frame(arenaPoly, carpetFwd, out center, out halfW, out front, out back);
        /// <summary>飛艇的根（世界座標）：前緣往前 9.1m、上 2.7m</summary>
        public Vector3 ShipRoot { get { CarpetFrame(out var c, out _, out var f, out _); return c + carpetFwd * (f + Carpet.ShipAhead) + Vector3.up * Carpet.ShipUp; } }
        public float CarpetLateral(Vector3 p) { CarpetFrame(out var c, out _, out _, out _); return Vector3.Dot(p - c, Carpet.Right(carpetFwd)); }

        /// <summary>進到飛毯地點（房主、隊友都叫）：站別、前方；換站時清掉還在飛的東西</summary>
        public void SetCarpetStation(int station, Vector3 fwd)
        {
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 1e-4f) carpetFwd = fwd.normalized;
            if (station != carpetStation) ClearSky();
            carpetStation = station;
            if (station < 0) { carpetSwoop = 0f; carpetPhase = 1; }
        }

        public void ClearSky()
        {
            skyCues.Clear(); skyBoards.Clear(); skySubQueue.Clear(); skyGroupHit.Clear(); skyGroupDodged.Clear();
        }

        // ------------------------------------------------------------ 房主：開始
        void StartCarpet()
        {
            waveToSpawn = 0;
            ClearHazards(); nextRockAt = -1f;   // 飛毯上沒有主題的機關（浪、漩渦…）
            var f = Fighter.Flat(player.forward);
            SetCarpetStation(Mathf.Clamp(PI("carpet", 0), 0, 4), f == Vector3.zero ? carpetFwd : f);
            ClearSky();
            skyWave = 0; skyLastKind = -1; skyRepeats = 0; skyCueCount = 0; skyRound = 0; skyBossTurn = 0; skyBossSpawned = false;
            skyStart = waveStartAt; skyNextCue = waveStartAt + Carpet.FirstObstacle; skyNextBoard = waveStartAt + Carpet.FirstBoard;
            skySwoopStart = -99f; carpetSwoop = 0f; carpetPhase = 1; skyGroupPirateAt = -1f;
        }

        // ------------------------------------------------------------ 房主：每幀（UpdateRoomEvent 叫）
        void TickCarpet()
        {
            var st = Carpet.Stations[Mathf.Clamp(carpetStation, 0, Carpet.Stations.Length - 1)];
            CarpetFrame(out var c, out float halfW, out float front, out float back);
            bool small = Carpet.Small(halfW);
            LandBoards();
            if (CarpetBoss) { TickAirship(small, halfW); RefreshCarpetText(st); return; }
            if (now >= skyNextCue)
            {
                QueueObstacle(Carpet.PickKind(rng, st.weights, small, skyLastKind, skyRepeats), false);
                skyNextCue = now + st.every;
            }
            int pending = 0; foreach (var b in skyBoards) if (!b.landed) pending++;
            if (skyWave < st.waves.Length && pending == 0 && now >= skyNextBoard)
            {
                var wave = st.waves[skyWave];
                int n = Mathf.Max(wave.Length, Mathf.RoundToInt(wave.Length * Mathf.Max(1f, dungeonParty / 2f)));   // 副本人數 3、4 人：每波多幾隻
                int alive = AliveMinions(), cap = Carpet.MaxFoes + PlayersN - 1;
                if (alive == 0 || alive + n <= cap)
                {
                    float last = now;
                    for (int i = 0; i < n; i++) last = QueueBoard(wave[i % wave.Length], now + i * 0.7f, false, 0).start;
                    skyWave++;
                    skyNextBoard = last + Carpet.LandDelay + Carpet.NextBoardGap;
                }
            }
            RefreshCarpetText(st);
            if (skyWave >= st.waves.Length && pending == 0 && !AnyMinionAlive()) FinishRoomEvent($"穿過了「{st.name}」！");
        }

        int AliveMinions() { int n = 0; foreach (var m in minions) if (m.Alive) n++; return n; }

        void RefreshCarpetText(Carpet.Station st)
        {
            string tips = CarpetSmall ? "小場地模式：只有跳、蹲" : "低的⬆跳、高的⬇蹲、紅的那半邊別站、站進綠框";
            if (CarpetBoss)
                evText = !skyBossSpawned ? "🛩 逐風號追上來了！" : carpetSwoop > 0.01f ? "⚓ 逐風號壓低了！靠近了，快打！" : carpetPhase >= 2 ? "💥 彈幕：橘色⬆跳、藍色⬇蹲（越來越多）" : $"🛩 打倒逐風號！它會不時壓低到飛毯前緣（{tips}）";
            else evText = $"🪂 {st.name}：登毯的敵人 {Mathf.Min(skyWave, st.waves.Length)}/{st.waves.Length} 波（{tips}）";
        }

        // ------------------------------------------------------------ 房主：排障礙
        /// <summary>避開其他障礙（抵達至少隔 1.5 秒）和敵人落地（至少隔 1.2 秒）</summary>
        float ReserveSky(float desired)
        {
            float at = desired;
            for (int guard = 0; guard < 60; guard++)
            {
                bool moved = false;
                foreach (var q in skyCues)
                    if (q.group < 0 && !q.judged && Mathf.Abs(q.hit - at) < Carpet.SameGap) { at = q.hit + Carpet.SameGap; moved = true; }
                foreach (var b in skyBoards)
                    if (!b.landed && Mathf.Abs(b.LandAt - at) < Carpet.LandClear) { at = b.LandAt + Carpet.LandClear; moved = true; }
                if (!moved) break;
            }
            return at;
        }

        SkyCue QueueObstacle(int kind, bool boss)
        {
            CarpetFrame(out _, out float halfW, out _, out _);
            if (Carpet.Small(halfW) && kind >= Carpet.Half) kind -= 2;   // 小場地：半邊→低、缺口→高
            float lead = carpetStation == 0 && skyCueCount < 2 ? Carpet.TutorialLead : Carpet.Lead(kind);
            float hit = ReserveSky(now + lead + (boss ? 0.5f : Carpet.Visible));
            var q = new SkyCue
            {
                id = ++skyIds, kind = kind, station = carpetStation, boss = boss, lead = lead, hit = hit, travel = hit - now,
                power = Carpet.Power(kind, boss) * Carpet.DifficultyMul(levelBoost),
            };
            if (kind == Carpet.Half) q.side = rng.Next(2) == 0 ? -1 : 1;
            if (kind == Carpet.Gap) { float off = Carpet.SafeOffset(halfW); q.gap = (rng.Next(3) - 1) * off; q.gapW = Carpet.GapWidth; }
            skyRepeats = kind == skyLastKind ? skyRepeats + 1 : 1; skyLastKind = kind; skyCueCount++;
            skyCues.Add(q); OnSkyCue?.Invoke(q);
            return q;
        }

        void QueueShot(int kind, float lane, float delay, int group, int round, float spawnAt)
        {
            var q = new SkyCue
            {
                id = ++skyIds, kind = kind, station = carpetStation, boss = true, group = group, lane = lane,
                lead = Carpet.ShotTravel, hit = spawnAt + Carpet.ShotTravel + delay, speed = Carpet.ShotSpeed(round),
                power = Carpet.Power(kind, true) * Carpet.DifficultyMul(levelBoost),
            };
            q.travel = q.hit - now;
            skyCues.Add(q); OnSkyCue?.Invoke(q);
        }

        // ------------------------------------------------------------ 房主：登毯
        SkyBoard QueueBoard(int variant, float start, bool ship, int deckSide)
        {
            var v = MonsterVariants.Get(variant);
            CarpetFrame(out var c, out float halfW, out float front, out float back);
            var right = Carpet.Right(carpetFwd);
            var land = PickLandPoint(c, halfW, front, back);
            int s = Vector3.Dot(land - c, right) >= 0f ? 1 : -1;
            Vector3 from;
            if (ship) from = ShipRoot + Vector3.up * 1.2f + right * deckSide * 1.5f;
            else switch (v != null ? v.sky : 0)
            {
                case 0: from = land + right * s * 9f + carpetFwd * 2f + Vector3.up * 3f; break;              // 風箏：從側面滑過來
                case 1: from = c + carpetFwd * (front + 14f) + Vector3.up * 6f; break;                      // 巨鷹：從正前方俯衝
                case 2: from = c + carpetFwd * (front + 10f) + right * s * 4f + Vector3.up * 5f; break;     // 滑翔翼：前方偏側
                case 3: from = land + carpetFwd * (front + 3f) - Vector3.up * 3f; break;                    // 魔偶：從下方浮上來
                default: from = land + right * s * 10f + carpetFwd * 3f + Vector3.up * 4f; break;           // 隊長的小飛艇：側面
            }
            // 落地不要和障礙抵達同時（至少隔 1.2 秒）
            float landAt = start + Carpet.LandDelay;
            for (int guard = 0; guard < 40; guard++)
            {
                bool moved = false;
                foreach (var q in skyCues) if (!q.judged && Mathf.Abs(q.hit - landAt) < Carpet.LandClear) { landAt = q.hit + Carpet.LandClear; moved = true; }
                if (!moved) break;
            }
            var b = new SkyBoard { id = ++skyIds, variant = variant, sky = v != null ? v.sky : 0, from = from, land = land, start = landAt - Carpet.LandDelay, ship = ship, side = ship ? deckSide : s };
            skyBoards.Add(b); OnSkyBoard?.Invoke(b);
            return b;
        }

        /// <summary>落點：飛毯內、離每個玩家和其他落點至少 1m（找不到就挑最遠的）</summary>
        Vector3 PickLandPoint(Vector3 c, float halfW, float front, float back)
        {
            var right = Carpet.Right(carpetFwd);
            Vector3 best = c; float bestD = -1f;
            for (int i = 0; i < 24; i++)
            {
                float lat = Mathf.Lerp(-halfW + 0.25f, halfW - 0.25f, (float)rng.NextDouble());
                float along = Mathf.Lerp(-back + 0.4f, front - 0.4f, (float)rng.NextDouble());
                var p = c + right * lat + carpetFwd * along;
                if (!GalleryInside(p)) continue;
                float d = 99f;
                foreach (var pl in Players()) if (pl.Alive) d = Mathf.Min(d, FlatDistance(pl.Feet, p));
                foreach (var b in skyBoards) if (!b.landed) d = Mathf.Min(d, FlatDistance(b.land, p));
                if (d >= 1f) return p;
                if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        void LandBoards()
        {
            foreach (var b in skyBoards)
            {
                if (b.landed || now < b.LandAt) continue;
                b.landed = true;
                var v = MonsterVariants.Get(b.variant);
                if (v == null) continue;
                var m = SpawnVariant(b.land, v);
                if (v.sky == 4) MakeElite(m);   // 空賊隊長：菁英
                Fighter near = null; float nd = 99f;
                foreach (var pl in Players()) if (pl.Alive && FlatDistance(pl.Feet, b.land) < nd) { nd = FlatDistance(pl.Feet, b.land); near = pl; }
                if (near != null) { var f = Fighter.Flat(near.Feet - b.land); if (f != Vector3.zero) m.forward = f; }
                m.attackReadyAt = Mathf.Max(m.attackReadyAt, now + 0.8f);   // 剛落地：站穩才出手
            }
            skyBoards.RemoveAll(b => b.landed && now > b.start + Carpet.BoardTime + 0.3f);
        }

        // ------------------------------------------------------------ 房主：飛艇
        void TickAirship(bool small, float halfW)
        {
            if (!skyBossSpawned)
            {
                if (now < skyStart + 1.5f) return;
                SpawnBoss(); bossPending = false; skyBossSpawned = true;
                skyNextSwoop = now + Carpet.SwoopEvery; skyNextPirate = now + 6f; skyNextCue = now + 3f;
            }
            if (!enemy.Alive) { ClearSky(); carpetSwoop = 0f; return; }
            enemy.bodyRadius = 0.9f; enemy.snaredUntil = 0f;
            if (carpetPhase == 1 && enemy.hp <= enemy.maxHp * 0.5f)
            {
                carpetPhase = 2; skyRound = 0; skyNextBarrage = now + 2.5f;
                Emit("evsky", enemy.Chest, new Color(1f, 0.55f, 0.2f), "逐風號：全砲門打開！彈幕來了——橘色⬆跳、藍色⬇蹲！");
            }
            // 俯衝窗口：每 24 秒壓低到飛毯前緣停 3.5 秒（近戰的輸出時間）；空中還有障礙、彈幕就等它們過去
            float t = now - skySwoopStart;
            bool swooping = t >= 0f && t < Carpet.SwoopLength;
            bool swoopDue = !swooping && now >= skyNextSwoop;   // 該俯衝了：不再出新招，等空中的障礙、彈幕過去
            if (swoopDue && SkyQuiet())
            {
                skySwoopStart = now; t = 0f; swooping = true;
                skyNextSwoop = now + Carpet.SwoopLength + Carpet.SwoopEvery;
                Emit("evsky", enemy.Chest, new Color(1f, 0.85f, 0.35f), "⚓ 逐風號壓低了！靠近了，快打！");
            }
            carpetSwoop = swooping ? Carpet.SwoopAt(t) : 0f;
            PlaceAirship();
            if (swoopDue && !swooping) return;
            if (swooping)
            {
                // 窗口結束後再開火（延後，不補射）
                skyNextCue = Mathf.Max(skyNextCue, skySwoopStart + Carpet.SwoopLength + 1f);
                skyNextBarrage = Mathf.Max(skyNextBarrage, skySwoopStart + Carpet.SwoopLength + 1f);
                skyNextPirate = Mathf.Max(skyNextPirate, skySwoopStart + Carpet.SwoopLength + 2f);
                return;
            }
            int cap = Carpet.BossMaxFoes + PlayersN - 1;
            if (carpetPhase == 1)
            {
                if (now >= skyNextCue)
                {
                    int[] order = { Carpet.Low, Carpet.High, Carpet.Half, Carpet.Gap };
                    QueueObstacle(order[skyBossTurn++ % 4], true);
                    skyNextCue = now + Carpet.BossEvery;
                }
                if (now >= skyNextPirate)
                {
                    skyNextPirate = now + Carpet.PirateEvery;
                    int n = 1 + rng.Next(2), alive = AliveMinions();
                    for (int i = 0; i < n && alive + i < cap; i++) QueueBoard(rng.Next(2) == 0 ? 131 : 133, now + i * 0.8f, true, i == 0 ? -1 : 1);
                }
                return;
            }
            // 第二階段：每 6 秒一組彈幕（0、1.8、3.6 秒三個子波，每個子波只要一種動作），之後最多一名空賊跳下來
            if (skySubQueue.Count == 0 && now >= skyNextBarrage)
            {
                int first = rng.Next(2);
                for (int i = 0; i < 3; i++) skySubQueue.Add((now + i * Carpet.BarrageSub, (first + i) % 2 == 0 ? Carpet.ShotLow : Carpet.ShotHigh));
                skyGroupPirateAt = now + 2 * Carpet.BarrageSub + 1.2f;
                skyNextBarrage = now + Carpet.BarrageEvery;
            }
            for (int i = 0; i < skySubQueue.Count; i++)
            {
                var (at, kind) = skySubQueue[i];
                if (now < at) continue;
                int group = ++skyGroupIds;
                foreach (var (lane, delay) in Carpet.BarrageLanes(rng, Carpet.BarrageCount(skyRound), halfW)) QueueShot(kind, lane, delay, group, skyRound, now);
                skySubQueue.RemoveAt(i--);
                if (skySubQueue.Count == 0) skyRound++;   // 一組打完：下一組更多、更快
            }
            if (skyGroupPirateAt > 0f && now >= skyGroupPirateAt)
            {
                skyGroupPirateAt = -1f;
                if (AliveMinions() < cap) QueueBoard(rng.Next(2) == 0 ? 131 : 133, now, true, rng.Next(2) == 0 ? -1 : 1);
            }
        }

        bool SkyQuiet()
        {
            if (skySubQueue.Count > 0) return false;
            foreach (var q in skyCues) if (!q.judged) return false;
            return true;
        }

        /// <summary>飛艇（Boss）的判定位置＝錨台：平常在前緣前 5m、高處；俯衝時貼到前緣</summary>
        void PlaceAirship()
        {
            var a = Carpet.Anchor(ShipRoot, carpetFwd, carpetSwoop);
            enemy.head = new Vector3(a.x, Mathf.Max(1.2f, a.y + 0.8f), a.z);
            enemy.forward = -carpetFwd;
        }

        // ------------------------------------------------------------ 隊友端：收到房主排好的
        public void AddRemoteSkyCue(SkyCue q, float localHit)
        {
            if (q == null || skyCues.Exists(x => x.id == q.id)) return;
            q.hit = localHit;
            skyCues.Add(q);
        }
        public void AddRemoteSkyBoard(SkyBoard b, float localStart)
        {
            if (b == null || skyBoards.Exists(x => x.id == b.id)) return;
            b.start = localStart; b.landed = true;   // 隊友端不生怪（房主落地時生，小怪照一般方式同步）
            skyBoards.Add(b);
        }

        // ------------------------------------------------------------ 每支手機：判定自己有沒有躲過
        void UpdateCarpet(float dt)
        {
            if (carpetStation < 0) return;
            if (remoteEnemy) skyBoards.RemoveAll(b => now > b.start + Carpet.BoardTime + 0.5f);
            if (skyCues.Count == 0) return;
            CarpetFrame(out var c, out float halfW, out float front, out float back);
            var me = player;
            bool alive = me != null && me.Alive && !stageCleared && !stageFailed;
            float lat = me != null ? Vector3.Dot(me.Feet - c, Carpet.Right(carpetFwd)) : 0f;
            float depth = me != null ? Mathf.Clamp(front - Vector3.Dot(me.Feet - c, carpetFwd), 0f, front + back) : 0f;
            for (int i = 0; i < skyCues.Count; i++)
            {
                var q = skyCues[i];
                if (q.judged) continue;
                q.contact = q.hit + depth / Mathf.Max(1f, q.speed);
                if (now < q.contact - Carpet.PoseBefore) continue;
                if (alive && Carpet.NeedsJump(q.kind) && me.airborneUntil > now) q.sawPose = true;
                if (alive && Carpet.NeedsDuck(q.kind) && me.crouching) q.sawPose = true;
                if (!q.gotPos && now >= q.contact)
                {
                    q.gotPos = true;
                    // 位置的判定（碰到那一刻）：半邊、缺口；砲彈要在自己那一條線上才算
                    q.result = q.kind == Carpet.Half ? (Carpet.HalfHits(lat, q.side) ? 2 : 1)
                        : q.kind == Carpet.Gap ? (Carpet.GapHits(lat, q.gap, q.gapW) ? 2 : 1)
                        : q.kind >= Carpet.ShotLow ? (Carpet.LaneHits(lat, q.lane) ? 2 : 0) : 0;
                }
                if (now < q.contact + Carpet.PoseAfter) continue;
                q.judged = true;
                if (!alive) { q.result = 1; continue; }
                bool hit; bool counted = true;
                if (q.kind == Carpet.Low || q.kind == Carpet.High) hit = !q.sawPose;
                else if (q.kind >= Carpet.ShotLow) { counted = q.result == 2; hit = counted && !q.sawPose; }
                else hit = q.result == 2;
                q.result = hit ? 2 : 1;
                if (!counted) continue;   // 砲彈沒有飛在我這一條線上
                if (q.group >= 0)
                {
                    if (hit) { if (!skyGroupHit.Add(q.group)) continue; }   // 同一個子波最多扣一次
                    else { if (skyGroupHit.Contains(q.group) || !skyGroupDodged.Add(q.group)) continue; }
                }
                if (hit)
                {
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(me.maxHp * q.power));
                    HazardDamage(me, dmg, me.Chest, SkyLabel(q.kind));
                }
                else
                {
                    BodyDodge(me, Carpet.NeedsDuck(q.kind));
                    Emit("evade", me.Chest, new Color(0.5f, 0.95f, 1f), q.kind == Carpet.Low || q.kind == Carpet.ShotLow ? "跳過了！" : q.kind == Carpet.High || q.kind == Carpet.ShotHigh ? "蹲下躲過！" : q.kind == Carpet.Half ? "閃開了！" : "穿過缺口！");
                }
            }
            skyCues.RemoveAll(q => q.judged && now > q.contact + 2f);
        }

        static string SkyLabel(int kind) => kind == Carpet.Low ? "撞上低空橫木" : kind == Carpet.High ? "撞上高空橫樑" : kind == Carpet.Half ? "被浮島撞到" : kind == Carpet.Gap ? "撞上岩壁" : "被砲彈打中";

        /// <summary>
        /// 身體閃避成功（跳／蹲躲過攻擊、閃過飛毯的障礙）：3 秒傷害加成（DodgePower）、
        /// 技能冷卻減少（逐風結精魄、天穹航裝 3 件；8 秒一次）、蹲避護盾（天穹舵魂；12 秒一次）
        /// </summary>
        public void BodyDodge(Fighter f, bool duck)
        {
            if (f == null || !f.Alive) return;
            f.evadeBoostUntil = now + 3f;
            if (f.dodgeCdr > 0f && now >= f.dodgeCdrAt)
            {
                f.dodgeCdrAt = now + 8f;
                foreach (var k in new List<string>(f.cooldownUntil.Keys)) f.cooldownUntil[k] -= f.dodgeCdr;
                Emit("buff", f.Chest, new Color(0.5f, 0.95f, 0.85f), $"🪶 技能冷卻 −{f.dodgeCdr:0.#} 秒");
            }
            if (duck && f.duckShield > 0f && now >= f.duckShieldAt)
            {
                f.duckShieldAt = now + 12f;
                int sh = Mathf.Max(1, Mathf.RoundToInt(f.maxHp * f.duckShield));
                f.shieldUntil = now + 4f; f.shieldLeft = Mathf.Max(f.shieldLeft, sh);
                Emit("buff", f.Chest, new Color(0.95f, 0.8f, 0.35f), $"💠 天穹舵魂：護盾 {sh}");
            }
        }
    }
}
