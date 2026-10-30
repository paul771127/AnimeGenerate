using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 第 1～3 章的場景機關（使用者要求；第 4、5 章是斧頭、地刺、落石，見 Combat.Hazards.cs）：
    ///   熔岩噴口（熔岩）：地上的裂口定時冒煙（預警）→ 噴出熔岩柱；站在上面受傷＋灼燒
    ///   孢子雲（森林）：一團毒孢子在場上來回飄；在裡面每 1.2 秒受傷＋中毒
    ///   纏人樹根（森林）：地上的樹根發綠光（預警）→ 竄出；站在上面受傷＋定身 1 秒
    ///   潮浪（海上）：場地一邊先出現預警線 → 一道低浪橫掃過整個場地；跳起來就躲得過
    ///   漩渦（海上）：定時出現的漩渦，在裡面每 1.2 秒受傷（漩渦消失時沒事）
    /// 位置、時間都是純計算（主機放、用 haz 訊息送給隊友），每位玩家自己判定打到自己；不傷怪物（使用者要求）。
    /// 第一章前兩關（1-1、1-2）是新手關：第 7 波以後才有熔岩噴口。
    /// </summary>
    public partial class Battle
    {
        public const float LavaRadius = 0.6f, RootRadius = 0.65f, SporeRadius = 0.95f, WhirlRadius = 0.85f, TideHalfWidth = 0.45f;
        const int LavaBase = 8, SporeBase = 3, RootBase = 5, TideBase = 9, WhirlBase = 4;
        /// <summary>潮浪：一個週期裡，0.55～0.8 預警、0.8～1 橫掃</summary>
        public const float TideWarnFrom = 0.55f, TideSweepFrom = 0.8f;
        /// <summary>漩渦：一個週期裡 0～0.45 沒有、0.45～0.55 預警、0.55～1 轉</summary>
        public const float WhirlWarnFrom = 0.45f, WhirlOnFrom = 0.55f;

        void SetupNatureHazards()
        {
            if (remoteEnemy || !endless) return;
            ArenaFrame(out var c, out float R);
            int local = (wave - 1) % 10 + 1;
            bool boss = IsBossWave;
            float start = waveStartAt + 1.5f;
            int theme = Theme;
            var placed = new List<Vector3>();
            Vector3 Spot(float minR, float maxR)
            {
                Vector3 p = c;
                for (int tries = 0; tries < 30; tries++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Lerp(minR, maxR, Mathf.Sqrt((float)rng.NextDouble()));
                    p = c + new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                    bool ok = true; foreach (var q in placed) if (FlatDistance(p, q) < 1.6f) ok = false;
                    if (ok) break;
                }
                placed.Add(p); return p;
            }
            if (theme == 0)
            {
                if (wave < 7) return;   // 新手關（1-1、1-2）沒有機關
                int n = boss ? 2 : local >= 7 ? 3 : local >= 4 ? 2 : 1;
                for (int i = 0; i < n; i++)
                    AddHazard(new Hazard { kind = HazardKind.Lava, a = Spot(0.8f, R * 0.8f), radius = LavaRadius, period = 4.5f - 0.15f * Mathf.Min(local, 8), phase = (float)rng.NextDouble(), start = start, damage = Mathf.RoundToInt(LavaBase * DmgMul) });
            }
            else if (theme == 1)
            {
                int spores = boss ? 1 : local >= 6 ? 2 : local >= 2 ? 1 : 0;
                for (int i = 0; i < spores; i++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    AddHazard(new Hazard { kind = HazardKind.Spore, a = c + new Vector3(-dir.z, 0f, dir.x) * ((i - (spores - 1) * 0.5f) * 1.8f), b = dir, amp = Mathf.Max(1f, R * 0.75f), radius = SporeRadius, period = 9f + 2f * i, phase = i * 0.5f, start = start, damage = Mathf.RoundToInt(SporeBase * DmgMul) });
                }
                int roots = boss ? 2 : local >= 5 ? 3 : local >= 1 ? 2 : 0;
                for (int i = 0; i < roots; i++)
                    AddHazard(new Hazard { kind = HazardKind.Roots, a = Spot(0.6f, R * 0.85f), radius = RootRadius, period = 5f, phase = (float)rng.NextDouble(), start = start, damage = Mathf.RoundToInt(RootBase * DmgMul) });
            }
            else if (theme == 2)
            {
                if (local >= 2 || boss)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float len = EdgeAlong(c, -dir, R) + EdgeAlong(c, dir, R);
                    var from = c - dir * EdgeAlong(c, -dir, R);
                    AddHazard(new Hazard { kind = HazardKind.Tide, a = from, b = dir, amp = len, radius = TideHalfWidth, period = boss ? 12f : Mathf.Max(7f, 11f - 0.4f * local), phase = 0.2f, start = start, damage = Mathf.RoundToInt(TideBase * DmgMul) });
                }
                int whirls = boss ? 1 : local >= 6 ? 2 : local >= 3 ? 1 : 0;
                for (int i = 0; i < whirls; i++)
                    AddHazard(new Hazard { kind = HazardKind.Whirl, a = Spot(0.8f, R * 0.8f), radius = WhirlRadius, period = 7f, phase = (float)rng.NextDouble(), start = start, damage = Mathf.RoundToInt(WhirlBase * DmgMul) });
            }
        }

        // ------------------------------------------------------------ 位置與狀態（純計算，主機、隊友都一樣）
        /// <summary>週期內的位置 0～1</summary>
        public static float HazardPhase(Hazard h, float t) => Mathf.Repeat((t - h.start) / Mathf.Max(0.5f, h.period) + h.phase, 1f);
        /// <summary>孢子雲的中心：沿 b 方向來回飄</summary>
        public static Vector3 SporeCenter(Hazard h, float t) => h.a + h.b * (h.amp * Mathf.Sin(2f * Mathf.PI * ((t - h.start) / Mathf.Max(1f, h.period) + h.phase)));
        /// <summary>潮浪：預警 0～1（0＝還沒、1＝開始掃）；掃的位置（從 a 沿 b 走了幾公尺；沒有在掃＝−1）</summary>
        public static float TideWarn(Hazard h, float t) { float u = HazardPhase(h, t); return u < TideWarnFrom ? 0f : u < TideSweepFrom ? (u - TideWarnFrom) / (TideSweepFrom - TideWarnFrom) : 1f; }
        public static float TideFront(Hazard h, float t) { float u = HazardPhase(h, t); return u < TideSweepFrom ? -1f : (u - TideSweepFrom) / (1f - TideSweepFrom) * h.amp; }
        /// <summary>漩渦：0＝沒有；0～1＝預警；1＝在轉</summary>
        public static float WhirlState(Hazard h, float t) { float u = HazardPhase(h, t); return u < WhirlWarnFrom ? 0f : u < WhirlOnFrom ? (u - WhirlWarnFrom) / (WhirlOnFrom - WhirlWarnFrom) : 1f; }

        void NatureHazardHits(Hazard h, List<Fighter> victims)
        {
            foreach (var f in victims)
            {
                if (!f.Alive) continue;
                bool hit; string label; EffectKind eff = EffectKind.None; float dps = 0f, dur = 0f, cd = HazardHitCooldown;
                switch (h.kind)
                {
                    case HazardKind.Lava: hit = SpikeHits(h, now, f.Feet, f.bodyRadius); label = "熔岩噴發"; eff = EffectKind.Burn; dps = 3f; dur = 2f; break;
                    case HazardKind.Roots: hit = SpikeHits(h, now, f.Feet, f.bodyRadius); label = "被樹根纏住"; eff = EffectKind.Snare; dur = 1f; break;
                    case HazardKind.Spore: hit = FlatDistance(f.Feet, SporeCenter(h, now)) < h.radius + f.bodyRadius * 0.5f; label = "孢子雲"; eff = EffectKind.Dot; dps = 2f; dur = 3f; break;
                    case HazardKind.Whirl: hit = WhirlState(h, now) >= 1f && FlatDistance(f.Feet, h.a) < h.radius + f.bodyRadius * 0.5f; label = "漩渦"; break;
                    case HazardKind.Tide:
                    {
                        float front = TideFront(h, now);
                        if (front < 0f) { hit = false; label = ""; break; }
                        float along = Vector3.Dot(f.Feet - h.a, h.b);
                        hit = Mathf.Abs(along - front) < h.radius + f.bodyRadius;
                        label = "被浪打到"; cd = h.period * 0.5f;   // 一道浪只打一次
                        if (hit && f.airborneUntil > now) { if (f.isPlayer && (!h.nextHit.TryGetValue(f, out var nj) || now >= nj)) { h.nextHit[f] = now + cd; Emit("evade", f.Chest, new Color(0.5f, 0.85f, 1f), "跳過浪了！"); } hit = false; }
                        break;
                    }
                    default: hit = false; label = ""; break;
                }
                if (!hit) continue;
                if (h.nextHit.TryGetValue(f, out var nt) && now < nt) continue;
                h.nextHit[f] = now + cd;
                if (PlayerSide(f)) { if (h.kind == HazardKind.Lava) dps = f.maxHp * LavaBurnPct; else if (h.kind == HazardKind.Spore) dps = f.maxHp * SporeDotPct; }   // 持續傷害也看最大 HP
                HazardDamage(f, HazardDmg(h, f), f.Chest, label, eff, dps, dur);
            }
        }
    }
}
