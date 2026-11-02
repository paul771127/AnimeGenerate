using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的小隊戰術與場地機關（使用者要求：普通關卡太無聊）。只在房主／單人跑邏輯；畫面、連線在 SoloBattle.ArenaProps.cs。
    ///
    /// 小隊戰術（storyTactics）：
    ///   ・隊長：輪次出怪（第二章以後）每一輪 CaptainChance 機率由一隻隊長帶隊（血量 ×2.5、攻擊 ×1.2）。
    ///     打倒隊長 → 同一隊的怪士氣崩潰：先逃 RoutFlee 秒，之後攻擊 ×0.6、受到的傷害 ×1.3。
    ///   ・增援突襲：每一波 ReinforceChance 機率，打到一半時從背後或側面冒出一小隊增援；出現前 ReinforceWarn 秒先提示方向。
    ///
    /// 場地機關（storyProps，每一關開打時依章節擺 4～5 個；玩家這一方的攻擊才會觸發）：
    ///   ・爆炸桶（npc pr_barrel）：火系打到馬上爆，其他攻擊打 3 下爆；爆炸範圍內的怪重傷，太近的玩家也會受傷；會引爆旁邊的桶、點燃草叢
    ///   ・石柱（npc pr_pillar）：打 4 下倒下，朝「打它的人 → 石柱」的方向倒，壓到的怪重傷＋暈眩
    ///   ・草叢（地上的區域）：火系招式打到站在草叢裡的怪 → 燒起來 6 秒，裡面的人每 0.5 秒受傷；火會延燒到旁邊的草叢；燒完變焦土
    ///   ・水窪（地上的區域）：雷系招式打到水窪裡的怪 → 導電：裡面的怪重傷＋暈眩；冰系 → 結凍 4 秒：裡面的怪動不了；有冷卻
    /// 機關造成的傷害照目標的最大 HP 算（Boss 只吃很少一部分）。
    /// </summary>
    public partial class Battle
    {
        /// <summary>故事關卡：小隊戰術、場地機關（SoloBattle 設定）</summary>
        public bool storyTactics, storyProps;

        // ------------------------------------------------------------ 隊長
        public const float CaptainChance = 0.5f, CaptainHp = 2.5f, CaptainDmg = 1.2f;
        public const float RoutFlee = 3f, RoutDmg = 0.6f, RoutTaken = 1.3f;
        int troopSeq; float captainAt = -1f; int captainTroop;
        readonly List<Fighter> captains = new List<Fighter>();

        /// <summary>GroupSpawn 排好這一輪時叫：要不要有隊長（回傳要當隊長的那一格的出生時間）</summary>
        void PlanCaptain(List<(float at, MinionRole role, float ang, bool ambush, bool healer, Vector3 pos, MarchGroup march)> queue, int from, int n)
        {
            troopSeq++; captainAt = -1f; captainTroop = troopSeq;
            if (!storyTactics || n < 3 || rng.NextDouble() >= CaptainChance) return;
            for (int i = from; i < from + n && i < queue.Count; i++)
                if (queue[i].role == MinionRole.Front && !queue[i].healer) { captainAt = queue[i].at; return; }
        }

        /// <summary>GroupSpawn 出了一隻：記是哪一隊；是隊長就變強</summary>
        void TagTroop(Fighter m, float at)
        {
            if (m == null || !storyTactics) return;
            m.troop = captainTroop;
            if (captainAt < 0f || Mathf.Abs(at - captainAt) > 1e-4f) return;
            captainAt = -1f;
            m.troopCaptain = true; m.isElite = true;
            m.maxHp = Mathf.RoundToInt(m.maxHp * CaptainHp); m.hp = m.maxHp;
            m.dmgMul *= CaptainDmg; m.bodyRadius *= 1.2f;
            m.name = "隊長・" + m.name;
            captains.Add(m);
            Emit("evcaptain", m.Feet, new Color(1f, 0.8f, 0.3f), $"⚑ {m.name}帶隊！先打倒隊長，整隊會潰散");
        }

        void TickCaptains()
        {
            for (int i = captains.Count - 1; i >= 0; i--)
            {
                var c = captains[i];
                if (c.Alive) continue;
                captains.RemoveAt(i);
                int n = 0;
                foreach (var m in minions)
                {
                    if (!m.Alive || m.troop != c.troop || m.troopCaptain) continue;
                    m.routUntil = now + RoutFlee; m.dmgMul *= RoutDmg; m.damageTakenMul *= RoutTaken;
                    m.charging = null; m.windupUntil = 0f; n++;
                }
                if (n > 0) Emit("evrout", c.Feet, new Color(1f, 0.85f, 0.4f), $"隊長倒下了！敵人士氣崩潰（{n} 隻：攻擊 −40%、受到的傷害 +30%）");
            }
        }

        /// <summary>潰散：往遠離最近玩家的方向逃（碰到場地邊就沿著邊）</summary>
        void UpdateRout(Fighter m, float dt)
        {
            Fighter near = null; float nd = float.MaxValue;
            foreach (var pl in Players()) { if (!pl.Alive) continue; float d = FlatDistance(pl.Feet, m.Feet); if (d < nd) { nd = d; near = pl; } }
            var dir = near != null ? Fighter.Flat(m.Feet - near.Feet) : m.forward;
            if (dir == Vector3.zero) dir = Vector3.forward;
            float step = 1.1f * m.speedMul * dt;
            foreach (float turn in new[] { 0f, 60f, -60f, 110f, -110f })
            {
                var d2 = turn == 0f ? dir : RotateY(dir, turn);
                var next = m.Feet + d2 * step;
                if (minionInside != null && !minionInside(next)) continue;
                m.head = new Vector3(next.x, m.head.y, next.z); m.forward = d2;
                return;
            }
        }

        // ------------------------------------------------------------ 增援突襲
        public const float ReinforceChance = 0.45f, ReinforceWarn = 2.5f;
        float reinfPlanAt = -1f, reinfAt = -1f; Vector3 reinfPos; int reinfWave = -1;

        /// <summary>NextWave 叫：這一波要不要增援、什麼時候</summary>
        void PlanReinforce()
        {
            reinfPlanAt = reinfAt = -1f;
            if (!storyTactics || IsBossWave || MiniBossWave || roomEvent != null || rng.NextDouble() >= ReinforceChance) return;
            reinfWave = wave;
            reinfPlanAt = waveStartAt + 10f + (float)rng.NextDouble() * 10f;
        }

        void TickReinforce()
        {
            if (reinfWave != wave || edgePointAt == null) return;
            if (reinfPlanAt > 0f && now >= reinfPlanAt)
            {
                reinfPlanAt = -1f;
                if (BlockingMinions == 0 && waveToSpawn == 0) return;   // 這一波已經快打完了：不來
                float ang = rng.NextDouble() < 0.5 ? 180f + ((float)rng.NextDouble() - 0.5f) * 50f : (rng.NextDouble() < 0.5 ? 1f : -1f) * (95f + (float)rng.NextDouble() * 45f);
                reinfPos = edgePointAt(ang);
                reinfAt = now + ReinforceWarn;
                Emit("evreinf", reinfPos, new Color(1f, 0.45f, 0.3f), $"⚠ {DirName(ang)}有增援逼近！");
            }
            if (reinfAt > 0f && now >= reinfAt)
            {
                reinfAt = -1f;
                int n = 2 + Mathf.Min(2, allies.Count);
                for (int i = 0; i < n; i++)
                {
                    int kind = PickMinionKind(i == 0 ? MinionRole.Front : (MinionRole?)null);
                    var off = new Vector3(((float)rng.NextDouble() - 0.5f) * 1.2f, 0f, ((float)rng.NextDouble() - 0.5f) * 1.2f);
                    var nm = SpawnMinion(reinfPos + off, 0, kind, HpMul * (IsRanged(kind) ? 1f : meleeHpMul), DmgMul, SpeedMul);
                    if (kind == 10) BuryChomper(nm);
                    if (Theme == CanyonTheme) StartCliffLeap(nm);
                }
                Emit("evinfo", reinfPos, new Color(1f, 0.45f, 0.3f), "增援到了！");
            }
        }

        /// <summary>相對玩家面向的方位（0 正前、90 右、180 後、−90 左）</summary>
        static string DirName(float ang)
        {
            float a = Mathf.DeltaAngle(0f, ang), x = Mathf.Abs(a);
            string side = a > 0f ? "右" : "左";
            if (x <= 30f) return "正前方";
            if (x <= 75f) return side + "前方";
            if (x <= 120f) return side + "側";
            if (x <= 155f) return side + "後方";
            return "正後方";
        }

        // ------------------------------------------------------------ 場地機關
        public class ArenaZone { public int kind; public Vector3 at; public float r; public int state; public float until, cdUntil, tickAt, spreadAt = -1f; }
        public const int ZoneGrass = 1, ZonePuddle = 2;
        /// <summary>草叢：0 完好、1 燃燒、2 焦土；水窪：0 平靜、1 導電、2 結凍</summary>
        public readonly List<ArenaZone> zones = new List<ArenaZone>();
        public const float BarrelR = 1.8f, BarrelNearPlayer = 1.2f, PillarLen = 2.4f, PillarWidth = 0.45f, PillarFallDelay = 0.6f;
        public const float GrassR = 1.3f, PuddleR = 1.15f, GrassBurn = 6f, FreezeTime = 4f;
        public const int BarrelHits = 3, PillarHits = 4;
        bool propBusy;
        readonly List<(float at, Fighter barrel)> barrelChain = new List<(float, Fighter)>();
        readonly List<(float at, Vector3 from, Vector3 dir)> pillarFalls = new List<(float, Vector3, Vector3)>();

        /// <summary>StartStage 叫：依章節擺機關（場地中央、玩家附近不擺）</summary>
        void PlaceArenaProps()
        {
            npcs.RemoveAll(n => n.npcId != null && n.npcId.StartsWith("pr_"));
            zones.Clear(); barrelChain.Clear(); pillarFalls.Clear();
            if (!storyProps || arenaPoly == null || arenaPoly.Count < 3) return;
            int th = Mathf.Clamp((stageStart - 1) / BossEvery, 0, 4);
            // 每一章：爆炸桶、石柱、草叢、水窪各幾個
            int[][] plan = { new[] { 2, 1, 0, 1 }, new[] { 1, 1, 2, 1 }, new[] { 1, 1, 0, 2 }, new[] { 2, 2, 0, 1 }, new[] { 2, 1, 1, 0 } };
            var want = new List<int>();
            for (int k = 0; k < 4; k++) for (int i = 0; i < plan[th][k]; i++) want.Add(k);
            var placed = new List<Vector3>();
            var c = ArenaCenter(); float inner = ArenaInner();
            int idx = 0;
            foreach (int k in want)
            {
                Vector3 p = Vector3.zero; bool ok = false;
                for (int t = 0; t < 30 && !ok; t++)
                {
                    float r = Mathf.Lerp(1.3f, Mathf.Max(1.5f, Mathf.Min(inner * 0.85f, AttackZoneHalf - 0.3f)), (float)rng.NextDouble());   // 擺在小怪會打過來的攻擊區裡
                    p = Ring(r, (float)rng.NextDouble() * 360f);
                    ok = minionInside == null || minionInside(p);
                    if (ok && FlatDistance(p, player.Feet) < 1.4f) ok = false;
                    if (ok) foreach (var q in placed) if (FlatDistance(p, q) < 1.8f) { ok = false; break; }
                }
                if (!ok) continue;
                placed.Add(p); idx++;
                switch (k)
                {
                    case 0:
                    {
                        var b = SpawnNpc("pr_barrel_" + idx, "爆炸桶", "priest", 1, false, 1, 0, p);
                        b.rooted = true; b.bodyRadius = 0.32f; b.maxHp = BarrelHits; b.hp = BarrelHits;
                        break;
                    }
                    case 1:
                    {
                        var s = SpawnNpc("pr_pillar_" + idx, "石柱", "priest", 1, false, 1, 0, p);
                        s.rooted = true; s.bodyRadius = 0.35f; s.maxHp = PillarHits; s.hp = PillarHits;
                        break;
                    }
                    case 2: zones.Add(new ArenaZone { kind = ZoneGrass, at = p, r = GrassR }); break;
                    case 3: zones.Add(new ArenaZone { kind = ZonePuddle, at = p, r = PuddleR }); break;
                }
            }
        }

        static bool IsArenaProp(Fighter f) => f != null && f.npcId != null && f.npcId.StartsWith("pr_", System.StringComparison.Ordinal);
        static bool ByPlayers(Fighter by) => by == null || by.isPlayer || by.isAlly;   // null＝隊友的命中（房主收到 mhit）

        /// <summary>ApplyDamageAndEffect 開頭叫：爆炸桶、石柱被打（回傳 true＝攔下來，不照一般扣血）</summary>
        bool ArenaPropHit(Fighter t, Fighter by)
        {
            if (!IsArenaProp(t)) return false;
            if (remoteEnemy)
            {
                // 隊友端：直接打到的（近戰、範圍）也要送給房主判定；飛行物在別的地方已經送了
                var contact = incoming ?? HitSkill; if (contact != null && t.Alive) OnMinionHit?.Invoke(t.minionId, 0, contact);
                return true;
            }
            if (!t.Alive || !ByPlayers(by)) return true;
            int e = ElementOf(incoming ?? HitSkill);
            if (t.npcId.StartsWith("pr_barrel"))
            {
                t.hp = e == 1 ? 0f : t.hp - 1f;
                if (t.hp <= 0f) ExplodeBarrel(t);
                else Emit("evprophit", t.Feet, new Color(1f, 0.6f, 0.2f), null);
            }
            else if (t.npcId.StartsWith("pr_pillar"))
            {
                t.hp -= 1f;
                if (t.hp <= 0f) TopplePillar(t, by ?? player);
                else Emit("evprophit", t.Feet, new Color(0.8f, 0.75f, 0.7f), null);
            }
            return true;
        }

        /// <summary>ApplyDamageAndEffect 叫：玩家的招式打中敵人 → 看那個位置有沒有草叢、水窪</summary>
        void ArenaZoneHit(Fighter target, Fighter by)
        {
            if (zones.Count == 0 || propBusy || remoteEnemy || target == null || !FoeSide(target) || !ByPlayers(by)) return;
            int e = ElementOf(incoming ?? HitSkill);
            if (e == 0) return;
            foreach (var z in zones)
            {
                if (FlatDistance(target.Feet, z.at) > z.r + 0.3f) continue;
                if (z.kind == ZoneGrass && e == 1 && z.state == 0) Ignite(z);
                else if (z.kind == ZonePuddle && z.state == 0 && now >= z.cdUntil)
                {
                    if (e == 3) Shock(z);
                    else if (e == 2) Freeze(z);
                }
            }
        }

        /// <summary>機關打到敵人：照最大 HP 的比例（菁英、小 Boss 減半；Boss 另外算）</summary>
        void EnvHit(Fighter f, float frac, float bossFrac)
        {
            if (f == null || !f.Alive) return;
            float dmg = f == enemy ? f.maxHp * bossFrac : f.maxHp * frac * (f.isElite || f.miniBoss ? 0.5f : 1f);
            if (dmg <= 0f) return;
            propBusy = true;
            Damage(f, Mathf.Max(1f, dmg), "prop", f.Chest);
            propBusy = false;
        }

        /// <summary>機關打到玩家（和崩塌一樣：自己扣，隊友交給隊友的手機）</summary>
        void PlayerEnvHit(Fighter pl, float frac, string why)
        {
            if (pl == null || !pl.Alive) return;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(pl.maxHp * frac));
            if (pl.isAlly) { Emit("allyhit", pl.Chest, Color.red, null); OnAllyHit?.Invoke(pl, Skills.All["m_rock"], dmg, pl.Chest, 0); }
            else { pl.hp = Mathf.Max(0f, pl.hp - dmg); Emit("hit", pl.Chest, Color.red, $"{why} -{dmg}"); }
        }

        IEnumerable<Fighter> Foes()
        {
            foreach (var m in minions) if (m.Alive && !m.hidden) yield return m;
            if (enemy != null && enemy.Alive) yield return enemy;
        }

        void ExplodeBarrel(Fighter b)
        {
            var p = b.Feet; b.hp = 0f;   // 從 npcs 拿掉留到下一幀（現在可能在命中判定的迴圈裡）
            Emit("evboom", p, new Color(1f, 0.5f, 0.15f), null);
            foreach (var f in new List<Fighter>(Foes())) if (FlatDistance(f.Feet, p) <= BarrelR + f.bodyRadius * 0.5f) EnvHit(f, 0.5f, 0.04f);
            foreach (var pl in new List<Fighter>(Players())) if (FlatDistance(pl.Feet, p) <= BarrelNearPlayer) PlayerEnvHit(pl, 0.08f, "爆炸");
            foreach (var n in npcs) if (n.Alive && n.npcId != null && n.npcId.StartsWith("pr_barrel") && FlatDistance(n.Feet, p) <= BarrelR && !barrelChain.Exists(x => x.barrel == n)) barrelChain.Add((now + 0.25f, n));
            foreach (var z in zones) if (z.kind == ZoneGrass && z.state == 0 && FlatDistance(z.at, p) <= BarrelR + z.r) Ignite(z);
        }

        void TopplePillar(Fighter s, Fighter by)
        {
            var p = s.Feet;
            var dir = Fighter.Flat(p - by.Feet); if (dir == Vector3.zero) dir = Fighter.Flat(player.forward); if (dir == Vector3.zero) dir = Vector3.forward;
            s.hp = 0f;
            pillarFalls.Add((now + PillarFallDelay, p, dir));
            Emit("evfall", p, new Color(0.8f, 0.75f, 0.7f), dir.x.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + "," + dir.z.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }

        static bool OnSegment(Vector3 q, Vector3 a, Vector3 dir, float len, float width)
        {
            var v = q - a; v.y = 0f;
            float along = Vector3.Dot(v, dir);
            if (along < 0f || along > len) return false;
            return (v - dir * along).magnitude <= width;
        }

        void Ignite(ArenaZone z)
        {
            z.state = 1; z.until = now + GrassBurn; z.tickAt = now; z.spreadAt = now + 1.5f;
            Emit("evgrass", z.at, new Color(1f, 0.45f, 0.1f), "草叢燒起來了！");
        }

        void Shock(ArenaZone z)
        {
            z.state = 1; z.until = now + 0.8f; z.cdUntil = now + 4f;
            int n = 0;
            foreach (var f in new List<Fighter>(Foes()))
            {
                if (FlatDistance(f.Feet, z.at) > z.r + f.bodyRadius * 0.5f || f.alt > 0.6f) continue;   // 飛在空中的不算
                EnvHit(f, 0.35f, 0.03f); n++;
                if (f != enemy && f.Alive) { f.snaredUntil = now + 1.5f; f.charging = null; f.windupUntil = 0f; }
            }
            foreach (var pl in new List<Fighter>(Players())) if (FlatDistance(pl.Feet, z.at) <= z.r) PlayerEnvHit(pl, 0.06f, "觸電");
            Emit("evshock", z.at, new Color(1f, 0.92f, 0.35f), n > 0 ? $"⚡ 水窪導電！電到 {n} 隻" : "⚡ 水窪導電！");
        }

        void Freeze(ArenaZone z)
        {
            z.state = 2; z.until = now + FreezeTime; z.cdUntil = now + FreezeTime + 4f; z.tickAt = now;
            Emit("evfreeze", z.at, new Color(0.6f, 0.9f, 1f), "❄ 水窪結凍了！裡面的怪動不了");
        }

        /// <summary>UpdateEndless 叫（房主／單人）</summary>
        void TickArenaProps()
        {
            if (storyTactics) { TickCaptains(); TickReinforce(); }
            npcs.RemoveAll(n => !n.Alive && IsArenaProp(n));   // 炸掉、倒下的機關
            for (int i = barrelChain.Count - 1; i >= 0; i--)
                if (now >= barrelChain[i].at) { var b = barrelChain[i].barrel; barrelChain.RemoveAt(i); if (b.Alive) ExplodeBarrel(b); }
            for (int i = pillarFalls.Count - 1; i >= 0; i--)
            {
                if (now < pillarFalls[i].at) continue;
                var (_, a, dir) = pillarFalls[i]; pillarFalls.RemoveAt(i);
                int n = 0;
                foreach (var f in new List<Fighter>(Foes()))
                {
                    if (!OnSegment(f.Feet, a, dir, PillarLen, PillarWidth + f.bodyRadius) || f.alt > 0.6f) continue;
                    EnvHit(f, 0.6f, 0.05f); n++;
                    if (f != enemy && f.Alive) { f.snaredUntil = now + 1f; f.charging = null; f.windupUntil = 0f; }
                }
                foreach (var pl in new List<Fighter>(Players())) if (OnSegment(pl.Feet, a, dir, PillarLen, PillarWidth)) PlayerEnvHit(pl, 0.1f, "被石柱壓到");
                foreach (var nb in new List<Fighter>(npcs)) if (nb.Alive && nb.npcId != null && nb.npcId.StartsWith("pr_barrel") && OnSegment(nb.Feet, a, dir, PillarLen, PillarWidth + 0.3f)) ExplodeBarrel(nb);
                Emit("evcrash", a + dir * PillarLen * 0.5f, new Color(0.8f, 0.75f, 0.7f), n > 0 ? $"石柱壓倒了 {n} 隻！" : null);
            }
            foreach (var z in zones)
            {
                if (z.state == 0) continue;
                if (z.kind == ZoneGrass && z.state == 1)
                {
                    if (now >= z.tickAt) { z.tickAt = now + 0.5f; BurnTick(z); }
                    if (z.spreadAt > 0f && now >= z.spreadAt) { z.spreadAt = -1f; foreach (var o in zones) if (o != z && o.kind == ZoneGrass && o.state == 0 && FlatDistance(o.at, z.at) <= z.r + o.r + 1.5f) Ignite(o); }
                    if (now >= z.until) { z.state = 2; Emit("evzone", z.at, Color.gray, null); }
                }
                else if (z.kind == ZonePuddle)
                {
                    if (z.state == 2 && now >= z.tickAt)
                    {
                        z.tickAt = now + 0.5f;
                        foreach (var f in Foes()) if (f != enemy && f.alt <= 0.6f && FlatDistance(f.Feet, z.at) <= z.r + f.bodyRadius * 0.5f) { f.snaredUntil = Mathf.Max(f.snaredUntil, now + 0.6f); f.charging = null; }
                    }
                    if (now >= z.until) { z.state = 0; Emit("evzone", z.at, Color.gray, null); }
                }
            }
        }

        void BurnTick(ArenaZone z)
        {
            foreach (var f in new List<Fighter>(Foes())) if (f.alt <= 0.6f && FlatDistance(f.Feet, z.at) <= z.r + f.bodyRadius * 0.5f) EnvHit(f, 0.06f, 0.008f);
            foreach (var pl in new List<Fighter>(Players())) if (FlatDistance(pl.Feet, z.at) <= z.r) PlayerEnvHit(pl, 0.025f, "灼燒");
        }

        /// <summary>隊友端：房主送來的區域狀態</summary>
        public void SetRemoteZones(Vector3[] pts, string[] ks)
        {
            zones.Clear();
            if (pts == null || ks == null) return;
            for (int i = 0; i < pts.Length && i < ks.Length; i++)
            {
                var a = ks[i].Split(',');
                if (a.Length < 2 || !int.TryParse(a[0], out var k) || !int.TryParse(a[1], out var st)) continue;
                zones.Add(new ArenaZone { kind = k, at = pts[i], r = k == ZoneGrass ? GrassR : PuddleR, state = st });
            }
        }
    }
}
