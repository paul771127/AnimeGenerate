using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 天穹飛毯副本（sky_carpet；Docs/CARPET_DUNGEON_PROPOSAL.md）的資料與純計算：五站的節奏、登毯波次、障礙判定、飛艇時間表。
    ///   場地＝飛毯：開戰時面對的方向是「前方」，整站不變；障礙只從前方飛來。
    ///   障礙 4 種：0 低空掃過（跳）、1 高空掃過（蹲）、2 半邊衝撞（側步到另一半）、3 整片＋缺口（站進缺口）；
    ///   飛艇第二階段的彈幕：4 低砲彈（跳）、5 高砲彈（蹲），一個子波只要求一種動作。
    ///   判定只算本機玩家（每支手機判定自己），傷害＝最大 HP 的比例（Combat.Carpet.cs）。
    /// </summary>
    public static class Carpet
    {
        public const string DungeonId = "sky_carpet";
        public const string BossClass = "boss_airship";
        public const int BossStation = 4;
        public const int Low = 0, High = 1, Half = 2, Gap = 3, ShotLow = 4, ShotHigh = 5;

        public static void Register()
        {
            Skills.Classes[BossClass] = new ClassDef { id = BossClass, name = "空賊飛艇・逐風號", color = Skills.C("#f2c14e"), maxHp = 500, maxMp = 999, mpRegen = 100, skills = new string[0], defaultLoadout = new string[0], desc = "天穹航路 Boss" };
        }

        public sealed class Station
        {
            public string name;
            public float every;          // 每幾秒一個障礙
            public int[] weights;        // 低／高／半邊／缺口的比例
            public int[][] waves;        // 每一波登毯的敵人（變種 id）
        }
        public static readonly Station[] Stations =
        {
            new Station { name = "起飛・晨曦雲海", every = 8f, weights = new[] { 50, 50, 0, 0 }, waves = new[] { new[] { 131 }, new[] { 132 }, new[] { 131, 131 } } },
            new Station { name = "懸鏈浮島", every = 6f, weights = new[] { 30, 30, 40, 0 }, waves = new[] { new[] { 131, 131 }, new[] { 132 }, new[] { 134 }, new[] { 131, 134 } } },
            new Station { name = "風蝕天隘", every = 5.5f, weights = new[] { 20, 20, 30, 30 }, waves = new[] { new[] { 133, 133 }, new[] { 133, 133 }, new[] { 132, 133 }, new[] { 134, 133, 133 } } },
            new Station { name = "雷雲航標", every = 5f, weights = new[] { 25, 25, 25, 25 }, waves = new[] { new[] { 131, 131 }, new[] { 133, 133 }, new[] { 134, 132 }, new[] { 135 } } },
            new Station { name = "首領・破曉攔截", every = 5f, weights = new[] { 25, 25, 25, 25 }, waves = new int[0][] },
        };

        // ---------------------------------------------------------------- 時間
        /// <summary>預警（抵達飛毯前緣前幾秒亮出提示）：低／高 1.8、半邊 2.0、缺口 2.2；第一站的前兩個 3 秒（教學）</summary>
        public static float Lead(int kind) => kind == Gap ? 2.2f : kind == Half ? 2f : 1.8f;
        public const float TutorialLead = 3f, Visible = 2.4f;   // 預警之前，從遠方飛近的時間
        public const float Sweep = 8f;                          // 過了前緣之後掠過飛毯的速度（m/s）：站得越後面越晚碰到
        public const float SameGap = 1.5f;                      // 兩個障礙抵達的間隔至少 1.5 秒（AR 安全）
        public const float LandClear = 1.2f;                    // 敵人落地和障礙抵達至少錯開 1.2 秒
        public const float BoardTime = 4f, BoardLand = 0.8f, BoardRing = 0.55f;   // 載具演出 4 秒：0.55～0.8 落點圈（1 秒）、0.8 落地
        public static float LandDelay => BoardTime * BoardLand;
        public const int MaxFoes = 3, BossMaxFoes = 2;
        public const float FirstObstacle = 2.5f, FirstBoard = 4f, NextBoardGap = 5f;

        // ---------------------------------------------------------------- 飛艇
        public const float BossEvery = 5f, SwoopEvery = 9f, SwoopIn = 1.5f, SwoopHold = 8f, SwoopOut = 1.5f, SwoopMax = 0.6f;   // 使用者要求：飛艇約一半時間貼在飛毯旁（近戰要打得到）：每輪遠處 9 秒 → 壓低 1.5 秒＋貼近 8 秒＋拉起 1.5 秒
        public const float PirateEvery = 18f, BarrageEvery = 6f, BarrageSub = 1.8f, ShotTravel = 2f;
        public const float ShipAhead = 9.1f, ShipUp = 2.7f;     // 飛艇的根：飛毯前緣往前 9.1m、上 2.7m（船首朝玩家）
        public static int BarrageCount(int round) => Mathf.Min(12, 4 + 2 * Mathf.Max(0, round));   // 定案：4 枚起、每輪 +2、最多 12
        public static float ShotSpeed(int round) => Mathf.Lerp(10f, 14f, Mathf.Clamp01(round / 4f));
        public static float SwoopAt(float t)
        {
            if (t < 0f) return 0f;
            if (t < SwoopIn) return SwoopMax * Mathf.SmoothStep(0f, 1f, t / SwoopIn);
            if (t < SwoopIn + SwoopHold) return SwoopMax;
            if (t < SwoopIn + SwoopHold + SwoopOut) return SwoopMax * (1f - Mathf.SmoothStep(0f, 1f, (t - SwoopIn - SwoopHold) / SwoopOut));
            return 0f;
        }
        public static float SwoopLength => SwoopIn + SwoopHold + SwoopOut;
        /// <summary>飛艇的錨台（近戰打得到的地方；Boss 的判定位置）：swoop 0＝前緣前 5m、高 2m；最低時貼著前緣</summary>
        public static Vector3 Anchor(Vector3 shipRoot, Vector3 fwd, float swoop) => shipRoot - fwd * (4.1f + 8f * swoop) + Vector3.up * (-0.7f - 2f * swoop);

        // ---------------------------------------------------------------- 傷害（最大 HP 的比例；難度越高越痛）
        public static float Power(int kind, bool boss) => kind >= ShotLow ? 0.08f : (kind >= Half ? 0.12f : 0.10f) + (boss ? 0.01f : 0f);
        public static float DifficultyMul(int levelBoost) => 1f + levelBoost / 80f;

        // ---------------------------------------------------------------- 場地
        /// <summary>飛毯的範圍（世界座標）：中心、往前／往後到邊多遠、左右可用的半寬（扣掉邊緣 0.2m）</summary>
        public static void Frame(List<Vector3> poly, Vector3 fwd, out Vector3 center, out float halfW, out float front, out float back)
        {
            center = Vector3.zero; halfW = 1f; front = 1.5f; back = 1.5f;
            if (poly == null || poly.Count < 3) return;
            foreach (var p in poly) center += p;
            center /= poly.Count; center.y = 0f;
            var right = Right(fwd);
            float maxF = float.MinValue, minF = float.MaxValue, maxR = float.MinValue, minR = float.MaxValue;
            foreach (var p in poly)
            {
                var d = p - center;
                float f = Vector3.Dot(d, fwd), r = Vector3.Dot(d, right);
                maxF = Mathf.Max(maxF, f); minF = Mathf.Min(minF, f); maxR = Mathf.Max(maxR, r); minR = Mathf.Min(minR, r);
            }
            front = Mathf.Max(0.5f, maxF); back = Mathf.Max(0.5f, -minF);
            halfW = Mathf.Max(0.3f, Mathf.Min(maxR, -minR) - 0.2f);
        }
        public static Vector3 Right(Vector3 fwd) => Vector3.Cross(Vector3.up, fwd).normalized;
        /// <summary>小場地（左右放不下兩個安全區）：不出半邊、缺口，改出跳蹲</summary>
        public static bool Small(float halfW) => halfW < 0.55f;
        /// <summary>側步的目標點：中心左右各 min(0.4, 半寬/2)（一步不超過 1m）</summary>
        public static float SafeOffset(float halfW) => Mathf.Min(0.4f, halfW * 0.5f);
        public const float GapWidth = 0.7f;

        // ---------------------------------------------------------------- 判定（lateral＝玩家在中線右邊幾公尺）
        public static bool HalfHits(float lateral, int side) => lateral * side > -0.12f;
        public static bool GapHits(float lateral, float gapCenter, float gapWidth) => Mathf.Abs(lateral - gapCenter) > gapWidth * 0.5f - 0.05f;
        public static bool LaneHits(float lateral, float lane) => Mathf.Abs(lateral - lane) < 0.32f;
        /// <summary>跳／蹲的判定窗：碰到前 0.3 秒～碰到後 0.12 秒內有跳起來（蹲下）過就算躲過</summary>
        public const float PoseBefore = 0.3f, PoseAfter = 0.12f;
        public static bool NeedsJump(int kind) => kind == Low || kind == ShotLow;
        public static bool NeedsDuck(int kind) => kind == High || kind == ShotHigh;

        /// <summary>挑一種障礙（照比例；小場地換成跳蹲；不連續三次同一種）</summary>
        public static int PickKind(System.Random rng, int[] weights, bool small, int last, int repeats)
        {
            int total = 0;
            for (int i = 0; i < 4; i++) if (!small || i < 2) total += weights[i];
            if (total <= 0) return rng.Next(2);
            int k = 0;
            for (int guard = 0; guard < 6; guard++)
            {
                int r = rng.Next(total);
                for (k = 0; k < 4; k++) { if (small && k >= 2) continue; r -= weights[k]; if (r < 0) break; }
                if (k > 3) k = 0;
                if (!(k == last && repeats >= 2)) break;
            }
            return k;
        }

        /// <summary>彈幕的一個子波：count 枚，左右散開（每枚一段、段內隨機），前後錯開 0～0.3 秒</summary>
        public static List<(float lane, float delay)> BarrageLanes(System.Random rng, int count, float halfW)
        {
            var l = new List<(float, float)>();
            float w = halfW + 0.2f;
            for (int i = 0; i < count; i++)
            {
                float x = ((i + (float)rng.NextDouble()) / count) * 2f - 1f;
                l.Add((x * w, (float)rng.NextDouble() * 0.3f));
            }
            return l;
        }
    }

    /// <summary>一個飛來的障礙／砲彈（房主排好，連線時送給隊友；hit＝抵達飛毯前緣的時間，各自換成本機的 battle.now）</summary>
    [Serializable]
    public sealed class SkyCue
    {
        public int id, kind, side = 1, group = -1, station;
        public float hit, travel, lead, gap, gapW = Carpet.GapWidth, lane, power, speed = Carpet.Sweep;
        public bool boss;
        [NonSerialized] public bool judged, sawPose, gotPos;
        [NonSerialized] public int result;   // 0 還沒、1 躲過、2 撞到
        [NonSerialized] public float contact = -1f;
        public float Spawn => hit - travel;
    }

    /// <summary>一隻敵人搭載具上飛毯（房主排好；start＝演出開始，start＋3.2 秒落地，房主這時才生出小怪）</summary>
    [Serializable]
    public sealed class SkyBoard
    {
        public int id, variant, sky;
        public Vector3 from, land;
        public float start;
        public bool ship;    // 從飛艇甲板跳下來的空賊（AirshipRig.DropPirate）
        public int side;     // 甲板的哪一側（−1 左、0 中、1 右）
        [NonSerialized] public bool landed, sent;
        public float LandAt => start + Carpet.LandDelay;
    }
}
