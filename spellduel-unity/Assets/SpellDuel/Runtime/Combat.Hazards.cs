using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 場景陷阱（第 4 章角鬥場、第 5 章黃土山道）：
    ///   ・大斧頭（角鬥場）：吊在高處來回擺動，刃經過的直線上誰在都會被砍（玩家、小怪都算；蹲下躲不過，要離開那條線）。
    ///   ・地刺（角鬥場）：地上的鐵板定時預警（發紅光）→ 刺出 → 收回；刺出時站在上面會受傷。
    ///   ・落石（黃土山道）：從場地外的崖頂飛向場內某位玩家附近，落點先出現警示圈；落地時圈內的人受傷。
    ///   ・小怪從崖頂跳進場內（黃土山道）：先在崖上蹲 0.6 秒，再花 0.85 秒跳下來。
    /// 角鬥場每一波的陷阱配置都不一樣（依這個主題的第幾波）。Boss 不怕陷阱。
    /// 判定：主機（或單人）擺陷阱、丟落石、判定陷阱打到小怪；每位玩家自己判定陷阱打到自己（和 Boss 的招式一樣，隊友回報血量）。
    /// 連線時主機用 OnHazardAdd 把陷阱送給隊友與觀眾（SoloBattle 轉成 haz 訊息），隊友端用 AddRemoteHazard 加進來。
    /// </summary>
    public partial class Battle
    {
        public enum HazardKind { Axe = 0, Spikes = 1, Rock = 2 }

        public class Hazard
        {
            public int id; public HazardKind kind;
            public Vector3 a;            // 斧頭：擺動中心（地上）；地刺：中心；落石：發射點（崖頂）
            public Vector3 b;            // 斧頭：擺動方向（水平單位向量）；落石：落點（地上）
            public float amp, period, phase, radius;
            public float start;          // 開始動作的時間（battle.now）；落石＝發射時間
            public float land;           // 落石：落地時間
            public int damage;
            public bool landed;
            public readonly Dictionary<Fighter, float> nextHit = new Dictionary<Fighter, float>();
        }

        public readonly List<Hazard> hazards = new List<Hazard>();
        /// <summary>場地邊界（世界座標，地上）：擺陷阱、找崖頂的發射點用（SoloBattle 開打時設定）</summary>
        public List<Vector3> arenaPoly;
        public event Action<Hazard> OnHazardAdd;   // 主機／單人放了新的陷阱或落石
        public event Action OnHazardsClear;        // 主機／單人清掉全部陷阱（換波）
        int nextHazardId = 1;
        public bool HazardHit;   // 正在套用陷阱的傷害（OnEvent 期間為 true）
        float nextRockAt = -1f;

        public const float AxePivotH = 4.8f, AxeLength = 4.0f, AxeHitAlong = 0.45f, AxeHitPerp = 0.35f;
        public const float SpikeWarnFrom = 0.55f, SpikeUpFrom = 0.75f, SpikeRadius = 0.75f;
        public const float RockFlight = 1.7f, RockRadius = 1.1f, RockArc = 1.5f, CliffH = 4.5f;
        public const float HazardHitCooldown = 1.2f, LeapWait = 0.6f, LeapTime = 0.85f;
        const int AxeBase = 9, SpikeBase = 7, RockBase = 10;   // 傷害（×這一波的敵人攻擊倍率）

        // ------------------------------------------------------------ 位置與判定（純計算，主機、隊友、觀眾都用同一套）
        /// <summary>斧頭的擺角（弧度）：中心往 b 方向擺為正</summary>
        public static float AxeAngle(Hazard h, float t)
        {
            float max = Mathf.Asin(Mathf.Clamp(h.amp / AxeLength, 0f, 0.95f));
            return max * Mathf.Sin(2f * Mathf.PI * (t - h.start) / Mathf.Max(0.5f, h.period) + h.phase);
        }
        /// <summary>刃的位置（世界座標）</summary>
        public static Vector3 AxeBlade(Hazard h, float t)
        {
            float th = AxeAngle(h, t);
            return h.a + h.b * (AxeLength * Mathf.Sin(th)) + Vector3.up * (AxePivotH - AxeLength * Mathf.Cos(th));
        }
        /// <summary>刃有沒有砍到站在 feet 的人：在擺動的那條線上（左右 0.35m 內），而且刃剛好擺到他那裡</summary>
        public static bool AxeHits(Hazard h, float t, Vector3 feet, float bodyR)
        {
            if (t < h.start) return false;
            var rel = feet - h.a; rel.y = 0f;
            float along = Vector3.Dot(rel, h.b);
            float perp = (rel - h.b * along).magnitude;
            float off = AxeLength * Mathf.Sin(AxeAngle(h, t));
            return perp < AxeHitPerp + bodyR && Mathf.Abs(along - off) < AxeHitAlong + bodyR * 0.5f;
        }
        /// <summary>地刺的狀態：0＝收著；0～1＝預警（越來越亮）；1＝刺出</summary>
        public static float SpikeState(Hazard h, float t)
        {
            if (t < h.start) return 0f;
            float u = Mathf.Repeat((t - h.start) / Mathf.Max(0.5f, h.period) + h.phase, 1f);
            if (u < SpikeWarnFrom) return 0f;
            if (u < SpikeUpFrom) return Mathf.Min(0.99f, (u - SpikeWarnFrom) / (SpikeUpFrom - SpikeWarnFrom));
            return 1f;
        }
        public static bool SpikeHits(Hazard h, float t, Vector3 feet, float bodyR) =>
            SpikeState(h, t) >= 1f && FlatDistance(feet, h.a) < h.radius + bodyR * 0.5f;
        /// <summary>落石在空中的位置（拋物線）</summary>
        public static Vector3 RockPos(Hazard h, float t)
        {
            float u = Mathf.Clamp01((t - h.start) / Mathf.Max(0.1f, h.land - h.start));
            var p = Vector3.Lerp(h.a, h.b, u);
            p.y += 4f * RockArc * u * (1f - u);
            return p;
        }

        // ------------------------------------------------------------ 每波的配置（主機／單人）
        void SetupHazards()
        {
            if (remoteEnemy || !endless) return;
            if (hazards.Exists(h => h.kind != HazardKind.Rock)) ClearHazards();
            nextRockAt = Theme == CanyonTheme ? waveStartAt + 3f : -1f;
            if (Theme != ArenaTheme) return;
            ArenaFrame(out var c, out float R);
            int local = (wave - 1) % 10 + 1;
            float baseA = (float)rng.NextDouble() * Mathf.PI;
            var u = new Vector3(Mathf.Cos(baseA), 0f, Mathf.Sin(baseA));   // 斧頭擺動的方向
            var v = new Vector3(-u.z, 0f, u.x);                            // 斧頭之間排開的方向
            float amp = Mathf.Clamp(R * 0.7f, 1.6f, 2.6f);
            float lane = Mathf.Clamp(R * 0.45f, 1.8f, 2.6f);
            // 依這個主題的第幾波：幾把斧頭（排成平行的幾條線）、幾個地刺
            int axes, spikes; bool ring = false;
            switch (local)
            {
                case 1: axes = 1; spikes = 0; break;
                case 2: axes = 0; spikes = 4; break;
                case 3: axes = 1; spikes = 3; break;
                case 4: axes = 2; spikes = 0; break;
                case 5: axes = 0; spikes = 6; ring = true; break;
                case 6: axes = 2; spikes = 2; break;
                case 7: axes = 1; spikes = 6; break;
                case 8: axes = 3; spikes = 0; break;
                case 9: axes = 2; spikes = 4; break;
                default: axes = 2; spikes = 3; break;   // Boss 波：兩側各一把，中間留給 Boss
            }
            float start = waveStartAt + 1f;
            for (int i = 0; i < axes; i++)
            {
                float off = axes == 1 ? (local == 10 ? lane : 0f) : (i - (axes - 1) * 0.5f) * (local == 10 ? lane * 2f : lane);
                AddHazard(new Hazard
                {
                    kind = HazardKind.Axe, a = c + v * off, b = u, amp = amp,
                    period = local == 10 ? 3.6f : 2.6f + (float)rng.NextDouble() * 0.8f,
                    phase = i * Mathf.PI * 0.9f, start = start, damage = Mathf.RoundToInt(AxeBase * DmgMul),
                });
            }
            var placed = new List<Vector3>();
            for (int i = 0; i < spikes; i++)
            {
                Vector3 p = c;
                if (ring) { float ang = baseA + i * Mathf.PI * 2f / spikes; p = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Mathf.Min(R * 0.55f, 2.6f); }
                else
                {
                    for (int tries = 0; tries < 30; tries++)
                    {
                        float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble()) * R * 0.8f;
                        p = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                        bool ok = true;
                        foreach (var q in placed) if (FlatDistance(p, q) < 1.8f) ok = false;
                        foreach (var h in hazards) if (h.kind == HazardKind.Axe && Mathf.Abs(Vector3.Dot(p - h.a, v)) < 0.9f) ok = false;   // 不要擺在斧頭的線上
                        if (ok) break;
                    }
                }
                placed.Add(p);
                AddHazard(new Hazard
                {
                    kind = HazardKind.Spikes, a = p, radius = SpikeRadius,
                    period = 3f, phase = ring ? i / (float)spikes : (float)rng.NextDouble(),   // 環狀：一個接一個刺出（像波浪）
                    start = start, damage = Mathf.RoundToInt(SpikeBase * DmgMul),
                });
            }
        }

        void AddHazard(Hazard h)
        {
            h.id = nextHazardId++;
            hazards.Add(h);
            OnHazardAdd?.Invoke(h);
        }

        /// <summary>換波：清掉斧頭和地刺（已經飛在空中的落石照樣落下）</summary>
        void ClearHazards()
        {
            hazards.RemoveAll(h => h.kind != HazardKind.Rock);
            OnHazardsClear?.Invoke();
        }

        /// <summary>隊友端／觀眾：主機送來的陷阱（start、land 已經換成這台的 battle.now）</summary>
        public void AddRemoteHazard(int id, HazardKind kind, Vector3 a, Vector3 b, float amp, float period, float phase, float radius, float start, float land, int damage)
        {
            hazards.RemoveAll(x => x.id == id);
            hazards.Add(new Hazard { id = id, kind = kind, a = a, b = b, amp = amp, period = period, phase = phase, radius = radius, start = start, land = land, damage = damage });
        }
        public void ClearRemoteHazards() => hazards.RemoveAll(h => h.kind != HazardKind.Rock);

        /// <summary>場地中心與大約的半徑（中心到最近的邊）</summary>
        void ArenaFrame(out Vector3 c, out float R)
        {
            c = Vector3.zero; R = 4f;
            if (arenaPoly == null || arenaPoly.Count < 3)
            {
                int n = 0; foreach (var p in Players()) { c += p.Feet; n++; }
                if (n > 0) c /= n;
                c.y = 0f;
                return;
            }
            foreach (var p in arenaPoly) c += p;
            c /= arenaPoly.Count; c.y = 0f;
            float best = float.MaxValue;
            for (int i = 0; i < arenaPoly.Count; i++)
            {
                var a = arenaPoly[i]; var b = arenaPoly[(i + 1) % arenaPoly.Count];
                var ab = b - a; ab.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                var q = a + ab * t; q.y = 0f;
                best = Mathf.Min(best, FlatDistance(c, q));
            }
            R = Mathf.Clamp(best, 2.5f, 5f);
        }

        /// <summary>從中心沿 dir 走到場地邊界的距離（找不到就用 R）</summary>
        float EdgeAlong(Vector3 c, Vector3 dir, float fallback)
        {
            if (arenaPoly == null || arenaPoly.Count < 3) return fallback;
            float best = float.MaxValue;
            for (int i = 0; i < arenaPoly.Count; i++)
            {
                var a = arenaPoly[i]; var b = arenaPoly[(i + 1) % arenaPoly.Count];
                // 射線 c + dir·t 與線段 a→b 的交點（XZ 平面）
                float ex = b.x - a.x, ez = b.z - a.z;
                float den = dir.x * ez - dir.z * ex;
                if (Mathf.Abs(den) < 1e-6f) continue;
                float t = ((a.x - c.x) * ez - (a.z - c.z) * ex) / den;
                float s = ((a.x - c.x) * dir.z - (a.z - c.z) * dir.x) / den;
                if (t > 0f && s >= 0f && s <= 1f) best = Mathf.Min(best, t);
            }
            return best < float.MaxValue ? best : fallback;
        }

        // ------------------------------------------------------------ 黃土山道：落石、從崖頂跳下的小怪
        float RockInterval()
        {
            int local = (wave - 1) % 10 + 1;
            return IsBossWave ? 4.5f : Mathf.Max(2f, 6.5f - 0.45f * local);
        }

        void LaunchRocks()
        {
            var pls = new List<Fighter>(); foreach (var p in Players()) if (p.Alive) pls.Add(p);
            if (pls.Count == 0) return;
            ArenaFrame(out var c, out float R);
            int local = (wave - 1) % 10 + 1;
            int n = local >= 6 ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                var tgt = pls[rng.Next(pls.Count)];
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = (float)rng.NextDouble() * 1.2f;
                var land = tgt.Feet + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r); land.y = 0f;
                var dir = land - c; dir.y = 0f;
                if (dir.sqrMagnitude < 0.04f) dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                dir.Normalize();
                // 落石從落點那一側的崖頂飛出（偏一點角度，看起來不會每顆都從正後方來）
                float tilt = ((float)rng.NextDouble() - 0.5f) * 0.9f;
                float ct = Mathf.Cos(tilt), stl = Mathf.Sin(tilt);
                var d2 = new Vector3(dir.x * ct + dir.z * stl, 0f, -dir.x * stl + dir.z * ct);   // 繞 Y 轉 tilt（不用 Quaternion，方便在 Unity 外測試）
                var from = c + d2 * (EdgeAlong(c, d2, R) + 1.2f); from.y = CliffH;
                AddHazard(new Hazard
                {
                    kind = HazardKind.Rock, a = from, b = land, radius = RockRadius,
                    start = now, land = now + RockFlight + i * 0.35f, damage = Mathf.RoundToInt(RockBase * DmgMul),
                });
            }
        }

        /// <summary>小怪剛出生（在場地邊緣外）：先站上崖頂，等一下再跳進場內</summary>
        void StartCliffLeap(Fighter m)
        {
            if (m == null || FreeRoam(m.minionKind)) return;
            ArenaFrame(out var c, out float R);
            var from = m.Feet; from.y = 0f;
            var dir = c - from; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            // 落點：從邊界往內 1.5～2.5m
            float toEdge = EdgeAlong(c, -dir, R);
            var to = c - dir * Mathf.Max(0.5f, toEdge - 1.5f - (float)rng.NextDouble());
            m.leapFrom = from; m.leapTo = to;
            m.leapStart = now + LeapWait; m.leapEnd = m.leapStart + LeapTime;
            m.alt = CliffH;
            m.head = new Vector3(from.x, MinionHead[m.minionKind] + m.alt, from.z);
            m.forward = dir;
            m.attackReadyAt = Mathf.Max(m.attackReadyAt, m.leapEnd + 0.6f);
        }

        void UpdateCliffLeap(Fighter m)
        {
            if (now < m.leapStart) return;   // 在崖頂上蹲著（看得到牠要跳了）
            float u = Mathf.Clamp01((now - m.leapStart) / LeapTime);
            var p = Vector3.Lerp(m.leapFrom, m.leapTo, u);
            m.alt = Mathf.Max(0f, Mathf.Lerp(CliffH, BaseAlt(m.minionKind), u) + 4f * 1.2f * u * (1f - u));
            m.head = new Vector3(p.x, MinionHead[m.minionKind] + m.alt, p.z);
            if (u >= 1f)
            {
                m.leapEnd = 0f; m.alt = BaseAlt(m.minionKind);
                m.head.y = MinionHead[m.minionKind] + m.alt;
                Emit("minionland", m.Feet, new Color(0.85f, 0.7f, 0.45f), null);
            }
        }

        // ------------------------------------------------------------ 每幀
        void UpdateHazards()
        {
            if (endless && !remoteEnemy && Theme == CanyonTheme && nextRockAt > 0f && now >= nextRockAt && !stageCleared && (player.Alive || AnyAllyAlive))
            {
                LaunchRocks();
                nextRockAt = now + RockInterval();
            }
            if (hazards.Count == 0) return;
            if (stageCleared && !remoteEnemy) { hazards.Clear(); OnHazardsClear?.Invoke(); return; }
            // 會被陷阱打到的：我自己（每位玩家自己判定），主機／單人再加上小怪（Boss 不怕陷阱、隊友由他自己判定）
            var victims = new List<Fighter>();
            if (player.Alive) victims.Add(player);
            if (!remoteEnemy) foreach (var m in minions) if (m.Alive && m.alt < 0.6f && m.leapEnd <= 0f) victims.Add(m);
            for (int i = hazards.Count - 1; i >= 0; i--)
            {
                var h = hazards[i];
                if (h.kind == HazardKind.Rock)
                {
                    if (!h.landed && now >= h.land)
                    {
                        h.landed = true;
                        Emit("rockland", h.b, new Color(0.8f, 0.62f, 0.35f), null);
                        foreach (var f in victims) if (f.Alive && FlatDistance(f.Feet, h.b) < h.radius + f.bodyRadius) HazardDamage(f, h.damage, f.Chest, "落石");
                    }
                    if (h.landed && now > h.land + 0.3f) hazards.RemoveAt(i);
                    continue;
                }
                if (now < h.start) continue;
                foreach (var f in victims)
                {
                    if (!f.Alive) continue;
                    bool hit = h.kind == HazardKind.Axe ? AxeHits(h, now, f.Feet, f.bodyRadius) : SpikeHits(h, now, f.Feet, f.bodyRadius);
                    if (!hit) continue;
                    if (h.nextHit.TryGetValue(f, out var nt) && now < nt) continue;
                    h.nextHit[f] = now + HazardHitCooldown;
                    HazardDamage(f, h.damage, f.Chest, h.kind == HazardKind.Axe ? "大斧頭" : "地刺");
                }
            }
        }

        void HazardDamage(Fighter f, int dmg, Vector3 at, string label)
        {
            Emit("hazard", at, new Color(1f, 0.55f, 0.3f), f.isPlayer ? $"{label}！" : null);
            HazardHit = true;   // 這一下的 hit／hurt 事件是陷阱造成的（畫面不要當成我打中了而震動）
            ApplyDamageAndEffect(f, dmg, EffectKind.None, 0f, 0f, at);
            HazardHit = false;
        }
    }
}
