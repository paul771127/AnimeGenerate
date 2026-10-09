using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 雙人「畫場地」模式的座標對齊（不需要標記圖）：
    ///   場地主（畫場地的人）的座標＝世界座標。另一支手機（對齊方）要算出「自己的 AR 座標 → 世界座標」的轉換。
    ///   兩支手機都用鏡頭偵測對方站的位置（PoseDetector：腳踝射線打到地板），得到成對的點：
    ///     A. 場地主看到對齊方 → 世界座標；對齊方同一時間自己的位置 → 本地座標
    ///     B. 對齊方看到場地主 → 本地座標；場地主同一時間回報的位置 → 世界座標
    ///   兩邊地板都是水平的（AR 的 Y 軸都朝上），所以只需要解「水平旋轉＋平移」（2D 剛體轉換），
    ///   用加權最小平方（2D Kabsch）求解，並剔除離群點（例如鏡頭拍到路人）。
    ///   開打後持續收集新的點、持續重算 → 雙方互相校正，修正 AR 慢慢累積的偏移。
    /// 本地座標＝對齊方的 AR 座標往下平移到地板高度 0（y 不需要轉換）。
    /// </summary>
    public class DuoAlignment
    {
        public const int KindOwnerSeesMe = 0, KindISeeOwner = 1;

        struct Pair { public Vector2 world, local; public float time; public int kind; }

        readonly List<Pair> pairs = new List<Pair>();
        public float Window = 45f;            // 只用最近這幾秒的點（AR 偏移會隨時間改變）
        public int MinPairs = 8;
        public float MaxRms = 0.35f;          // 殘差超過這個就不採用
        public float OutlierDist = 0.7f;      // 離群點門檻（公尺）

        public bool Solved { get; private set; }
        public float YawRad { get; private set; }     // 本地 → 世界的水平旋轉（數學正方向：從 +X 轉向 +Z）
        public Vector2 T { get; private set; }        // 本地 → 世界的平移（x, z）
        public float Rms { get; private set; }
        public int Used { get; private set; }
        public int CountA { get; private set; }
        public int CountB { get; private set; }
        public string Problem { get; private set; } = "";

        public int Count => pairs.Count;

        public void Clear() { pairs.Clear(); Solved = false; Problem = ""; }

        public void Add(Vector3 world, Vector3 local, float time, int kind)
        {
            pairs.Add(new Pair { world = new Vector2(world.x, world.z), local = new Vector2(local.x, local.z), time = time, kind = kind });
            if (pairs.Count > 600) pairs.RemoveRange(0, pairs.Count - 600);
        }

        static Vector2 Rot(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public Vector3 LocalToWorld(Vector3 local)
        {
            var w = Rot(new Vector2(local.x, local.z), YawRad) + T;
            return new Vector3(w.x, local.y, w.y);
        }

        /// <summary>
        /// 給 WorldFrame 用的「世界原點在 AR 座標中的位置與朝向」。
        /// floorY：對齊方 AR 座標中的地板高度（本地座標＝AR 座標往下平移 floorY）。
        /// </summary>
        public Pose MarkerPose(float floorY)
        {
            // 世界 → 本地：local = R(−Yaw)(world − T)；AR = local + (0, floorY, 0)
            // Unity 繞 Y 軸的四元數方向與數學正方向相反：數學角度 −Yaw ＝ Unity 角度 +Yaw
            var rot = new Quaternion(0f, Mathf.Sin(YawRad / 2f), 0f, Mathf.Cos(YawRad / 2f));
            var tl = Rot(-T, -YawRad);
            return new Pose(new Vector3(tl.x, floorY, tl.y), rot);
        }

        /// <summary>用最近的點重算轉換；成功（點夠多、分布夠開、殘差夠小）回傳 true</summary>
        public bool Solve(float now)
        {
            var use = new List<Pair>();
            int a = 0, b = 0;
            foreach (var p in pairs)
                if (now - p.time <= Window) { use.Add(p); if (p.kind == KindOwnerSeesMe) a++; else b++; }
            CountA = a; CountB = b;
            if (use.Count < MinPairs) { Problem = $"樣本 {use.Count}/{MinPairs}"; return false; }

            // 第一次求解 → 剔除離群點 → 再求一次
            if (!Fit(use, out float yaw, out Vector2 t, out float rms, out float spread)) { Problem = "樣本分布太集中"; return false; }
            var kept = new List<Pair>();
            foreach (var p in use)
                if ((Rot(p.local, yaw) + t - p.world).magnitude <= OutlierDist) kept.Add(p);
            if (kept.Count < MinPairs) { Problem = $"可用樣本不足（{kept.Count}/{MinPairs}，可能拍到別人）"; return false; }
            if (!Fit(kept, out yaw, out t, out rms, out spread)) { Problem = "樣本分布太集中"; return false; }
            // 旋轉要準，點必須分散：兩個人至少相距 1 公尺（A、B 兩類點各在一人腳下）
            if (spread < 0.7f) { Problem = "請兩人相距 1.5 公尺以上、或稍微走動"; return false; }
            if (rms > MaxRms) { Problem = $"誤差太大（{rms * 100f:F0}cm）"; return false; }

            YawRad = yaw; T = t; Rms = rms; Used = kept.Count; Solved = true; Problem = "";
            return true;
        }

        /// <summary>加權 2D Kabsch：越新的點權重越高</summary>
        static bool Fit(List<Pair> ps, out float yaw, out Vector2 t, out float rms, out float spread)
        {
            yaw = 0f; t = Vector2.zero; rms = 0f; spread = 0f;
            float tMax = float.MinValue;
            foreach (var p in ps) tMax = Mathf.Max(tMax, p.time);
            float W = 0f; Vector2 cw = Vector2.zero, cl = Vector2.zero;
            var w = new float[ps.Count];
            for (int i = 0; i < ps.Count; i++)
            {
                w[i] = Mathf.Exp(-(tMax - ps[i].time) / 20f);   // 20 秒前的點權重剩約 1/3
                W += w[i]; cw += ps[i].world * w[i]; cl += ps[i].local * w[i];
            }
            if (W <= 1e-6f) return false;
            cw /= W; cl /= W;
            float dot = 0f, cross = 0f, var = 0f;
            for (int i = 0; i < ps.Count; i++)
            {
                var l = ps[i].local - cl; var g = ps[i].world - cw;
                dot += w[i] * (l.x * g.x + l.y * g.y);
                cross += w[i] * (l.x * g.y - l.y * g.x);
                var += w[i] * l.sqrMagnitude;
            }
            spread = Mathf.Sqrt(var / W) * 2f;   // 約等於點分布的寬度
            if (spread < 1e-3f) return false;
            yaw = Mathf.Atan2(cross, dot);
            t = cw - Rot(cl, yaw);
            float e = 0f;
            for (int i = 0; i < ps.Count; i++) e += w[i] * (Rot(ps[i].local, yaw) + t - ps[i].world).sqrMagnitude;
            rms = Mathf.Sqrt(e / W);
            return true;
        }
    }
}
