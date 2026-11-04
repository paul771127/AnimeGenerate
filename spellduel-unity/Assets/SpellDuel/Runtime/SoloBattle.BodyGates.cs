using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 身體障礙（使用者要求：這是運動遊戲，跑步時要又蹲又跳又左右移動）：走道上橫跨整條路的障礙，穿過去的那一刻要做對動作。
    ///   ・低欄（紅）：跳過去（JumpHold 偵測到起跳）
    ///   ・橫樑（藍）：蹲下鑽過去（偵測到蹲姿）
    ///   ・半邊牆（橘）：擋住左半或右半，要從另一邊過（側步或轉向）
    /// 做對：身體閃避的加成（BodyDodge：傷害加成、減冷卻…），賽跑中再加速 1 秒；做錯：絆倒 1.2 秒不能衝刺（探索地圖另外扣一點血）。
    /// 用在賽跑（跑道上很多）和副本／故事探索的大地圖（走道上零星幾個；潛入、千機迴廊不放）。
    /// 判定只看自己（每支手機各自跑），不用連線。
    /// </summary>
    public partial class SoloBattle
    {
        class BodyGate { public Vector3 pos, fwd; public int kind; public float lastSide, coolUntil; public bool seenPose, hinted; public GameObject go; }
        const int GateJump = 0, GateDuck = 1, GateWallLeft = 2, GateWallRight = 3;
        const float GateHalfW = 2f, GatePoseRange = 1.4f, GateShow = 22f;
        readonly List<BodyGate> bodyGates = new List<BodyGate>();
        ExploreRun bodyGatesRun; Transform bodyGateRoot;
        float stumbleUntil; readonly bool[] gateHintSaid = new bool[4];
        static SkillDef gateHitSkill;

        bool BodyGatesOn(ExploreRun run) => run != null && run.IsDungeon && dungeon != null && !run.map.nodeAreas && !StealthLike;

        void BuildBodyGates(ExploreRun run)
        {
            ClearBodyGates();
            bodyGatesRun = run;
            if (!exRoot) return;
            bodyGateRoot = new GameObject("Body gates").transform; bodyGateRoot.SetParent(exRoot, false);
            var map = run.map;
            var rng = new System.Random(map.name != null ? map.name.GetHashCode() : 7);
            var spots = new List<(Vector3 pos, Vector3 fwd)>();
            if (race != null)
            {
                // 賽跑：主線上每 4～6 格一個
                for (int i = 6; i < race.main.Count - 6; i += rng.Next(4, 7))
                {
                    var a = race.main[i - 1]; var b = race.main[i + 1];
                    if ((race.main[i] - a) != (b - race.main[i])) { i -= 3; continue; }   // 轉角不放
                    var w = map.World(race.main[i].x, race.main[i].y);
                    if (map.PropAt(w, 1.2f) != null) continue;
                    spots.Add((w, new Vector3(b.x - a.x, 0f, b.y - a.y).normalized));
                }
            }
            else
            {
                // 副本、故事探索：區域外的直走道，每 12m 以上一個，最多 14 個
                var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(0, 1) };
                for (int x = 1; x < map.W - 1; x++) for (int z = 1; z < map.H - 1; z++)
                {
                    if (!map.Open(x, z) || map.At(x, z) != ExCell.Floor) continue;
                    bool inArea = false; foreach (var ar in map.areas) { var r = ar.rect; if (x >= r.xMin - 1 && x <= r.xMax + 1 && z >= r.yMin - 1 && z <= r.yMax + 1) { inArea = true; break; } }
                    if (inArea || Mathf.Abs(x - map.start.x) + Mathf.Abs(z - map.start.y) < 4) continue;
                    foreach (var d in dirs)
                    {
                        var p = new Vector2Int(-d.y, d.x);
                        if (map.Open(x + d.x, z + d.y) && map.Open(x - d.x, z - d.y) && !map.Open(x + p.x, z + p.y) && !map.Open(x - p.x, z - p.y) && rng.Next(100) < 14)
                        {
                            var w = map.World(x, z);
                            if (map.PropAt(w, 1.2f) != null) continue;
                            bool far = true; foreach (var s in spots) if (Battle.FlatDistance(s.pos, w) < 12f) { far = false; break; }
                            if (far && spots.Count < 14) spots.Add((w, new Vector3(d.x, 0f, d.y)));
                        }
                    }
                }
            }
            foreach (var s in spots)
            {
                int r = rng.Next(100);
                int kind = r < 38 ? GateJump : r < 72 ? GateDuck : rng.Next(2) == 0 ? GateWallLeft : GateWallRight;
                var g = new BodyGate { pos = s.pos, fwd = s.fwd, kind = kind };
                g.go = GateVisual(kind); g.go.transform.SetParent(bodyGateRoot, false);
                g.go.transform.localPosition = s.pos; g.go.transform.localRotation = Quaternion.LookRotation(s.fwd, Vector3.up);
                g.go.SetActive(false);
                bodyGates.Add(g);
            }
        }

        GameObject GateVisual(int kind)
        {
            var root = new GameObject(kind == GateJump ? "Hurdle (jump)" : kind == GateDuck ? "Beam (duck)" : "Half wall (side)");
            void Box(Vector3 at, Vector3 size, Color c) { var b = Prim(PrimitiveType.Cube, c); b.transform.SetParent(root.transform, false); b.transform.localPosition = at; b.transform.localScale = size; }
            float span = GateHalfW * 2f - 0.2f;
            if (kind == GateJump)
            {
                Box(new Vector3(0f, 0.32f, 0f), new Vector3(span, 0.12f, 0.12f), new Color(0.95f, 0.25f, 0.15f));
                for (int s = -1; s <= 1; s += 2) Box(new Vector3(s * span * 0.5f, 0.2f, 0f), new Vector3(0.1f, 0.4f, 0.1f), new Color(0.35f, 0.25f, 0.2f));
                Box(new Vector3(0f, 0.01f, -1.2f), new Vector3(span, 0.01f, 0.15f), new Color(1f, 0.4f, 0.3f));   // 地上的提示線：在這裡起跳
            }
            else if (kind == GateDuck)
            {
                float h = (StandH > 0.5f ? StandH : 1.55f) - 0.25f;   // 比站著的頭低一點：要蹲下才過得去
                Box(new Vector3(0f, h, 0f), new Vector3(span, 0.2f, 0.2f), new Color(0.25f, 0.5f, 1f));
                for (int s = -1; s <= 1; s += 2) Box(new Vector3(s * span * 0.5f, (h + 0.1f) * 0.5f, 0f), new Vector3(0.12f, h + 0.1f, 0.12f), new Color(0.3f, 0.3f, 0.38f));
                Box(new Vector3(0f, 0.01f, -1.2f), new Vector3(span, 0.01f, 0.15f), new Color(0.4f, 0.6f, 1f));
            }
            else
            {
                float side = kind == GateWallLeft ? -1f : 1f;   // 牆在左（-）或右（+）邊（從前進方向看）
                Box(new Vector3(side * GateHalfW * 0.5f, 0.95f, 0f), new Vector3(GateHalfW + 0.1f, 1.9f, 0.25f), new Color(1f, 0.55f, 0.15f));
                Box(new Vector3(-side * GateHalfW * 0.5f, 0.01f, -1.2f), new Vector3(GateHalfW - 0.3f, 0.01f, 0.4f), new Color(0.4f, 1f, 0.5f));   // 地上綠色：從這邊過
            }
            return root;
        }

        /// <summary>UpdateExplore（戰鬥中）每幀叫</summary>
        void UpdateBodyGates(ExploreRun run)
        {
            if (!BodyGatesOn(run) || Me == null || !Me.Alive) return;
            if (bodyGatesRun != run || !bodyGateRoot) BuildBodyGates(run);
            float now = Time.time;
            var me = Me.Feet;
            foreach (var g in bodyGates)
            {
                float far = Battle.FlatDistance(g.pos, me);
                if (g.go && g.go.activeSelf != (far < GateShow)) g.go.SetActive(far < GateShow);
                if (far > 6f) { g.lastSide = 0f; g.seenPose = false; continue; }
                var right = new Vector3(g.fwd.z, 0f, -g.fwd.x);
                float s = Vector3.Dot(me - g.pos, g.fwd), lat = Vector3.Dot(me - g.pos, right);
                bool inLane = Mathf.Abs(lat) < GateHalfW + 0.2f;
                if (!g.hinted && Mathf.Abs(s) < 5f && inLane && !gateHintSaid[g.kind])
                {
                    g.hinted = true; gateHintSaid[g.kind] = true;
                    Say(g.kind == GateJump ? "🟥 紅色低欄：衝過去的時候跳起來！" : g.kind == GateDuck ? "🟦 藍色橫樑：蹲下鑽過去！" : "🟧 半邊牆：從綠色那一邊過（往旁邊跨一步）！", 2.5f);
                }
                if (Mathf.Abs(s) < GatePoseRange)
                {
                    if (g.kind == GateJump && Me.airborneUntil > battle.now) g.seenPose = true;
                    if (g.kind == GateDuck && Me.crouching) g.seenPose = true;
                }
                else if (Mathf.Abs(s) > GatePoseRange + 0.5f) g.seenPose = false;
                float side = Mathf.Sign(s);
                if (g.lastSide != 0f && side != g.lastSide && inLane && now >= g.coolUntil)
                {
                    g.coolUntil = now + 1.5f;
                    bool ok = g.kind == GateJump || g.kind == GateDuck ? g.seenPose
                        : g.kind == GateWallLeft ? lat > 0.15f : lat < -0.15f;
                    GateResult(g, ok);
                    g.seenPose = false;
                }
                g.lastSide = side;
            }
        }

        void GateResult(BodyGate g, bool ok)
        {
            if (ok)
            {
                battle.BodyDodge(Me, g.kind == GateDuck);   // 身體閃避的加成（傷害加成、減冷卻…）
                Say(g.kind == GateJump ? "✨ 跳過了！" : g.kind == GateDuck ? "✨ 蹲下躲過！" : "✨ 閃開了！", 1f);
                Haptics.Play(Haptics.Kind.Light);
                if (race != null) raceBoostUntil = Mathf.Max(raceBoostUntil, Time.time + 1f);   // 賽跑：漂亮通過，小加速
            }
            else
            {
                stumbleUntil = Time.time + 1.2f;
                Say(g.kind == GateJump ? "💥 被低欄絆倒了！" : g.kind == GateDuck ? "💥 撞上橫樑了！" : "💥 撞上牆了！", 1.5f);
                Haptics.Play(Haptics.Kind.Heavy); Kick(10f, 0f);
                if (race == null)
                {
                    gateHitSkill ??= new SkillDef { id = "gate_hit", name = "障礙", color = new Color(1f, 0.5f, 0.3f) };
                    battle.ApplyRemoteHit(gateHitSkill, Mathf.Max(1, Mathf.RoundToInt(Me.maxHp * 0.04f)), Me.Chest, false);
                }
            }
        }

        void ClearBodyGates()
        {
            if (bodyGateRoot) Destroy(bodyGateRoot.gameObject);
            bodyGateRoot = null; bodyGates.Clear(); bodyGatesRun = null; stumbleUntil = 0f;
            for (int i = 0; i < gateHintSaid.Length; i++) gateHintSaid[i] = false;
        }
    }
}
