using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 法師的符文：用食指尖在空中照著軌跡畫（類似 $1 單筆畫辨識，不需要機器學習）。
    ///   重新取樣成固定點數 → 移到重心、依最大邊縮放（保留長寬比）→ 和範本逐點比距離。
    ///   方向有意義（雷擊由上往下），但圓、三角形、波浪、V 兩個繞行方向都接受。
    /// 純邏輯，可在 Unity 外測試。座標：y 朝上。
    /// </summary>
    public static class RuneRecognizer
    {
        public const int N = 32;
        public const float Threshold = 0.17f;   // 平均點距離（縮放後）小於這個才算畫對

        static readonly Dictionary<string, List<Vector2[]>> templates = new Dictionary<string, List<Vector2[]>>();
        static readonly HashSet<string> closed = new HashSet<string>();
        static readonly Dictionary<string, List<Vector2[]>> templateDirs = new Dictionary<string, List<Vector2[]>>();   // 封閉圖形：可以從任何一點開始畫
        static readonly Dictionary<string, Vector2[]> guides = new Dictionary<string, Vector2[]>();

        /// <summary>技能 → 符文（沒有就回傳 null）</summary>
        public static string RuneOf(string skillId) => skillId switch
        {
            "fire" => "circle", "ice" => "triangle", "thunder" => "bolt", "wind" => "wave", "meteor" => "vee", _ => null,
        };

        public static string RuneName(string rune) => rune switch
        {
            "circle" => "圓", "triangle" => "三角形", "bolt" => "閃電", "wave" => "波浪", "vee" => "V 字", _ => "",
        };

        static RuneRecognizer()
        {
            var circle = new List<Vector2>();
            for (int i = 0; i <= 40; i++) { float a = Mathf.PI / 2f + i / 40f * Mathf.PI * 2f; circle.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a))); }
            Add("circle", circle.ToArray(), true); closed.Add("circle");
            Add("triangle", new[] { new Vector2(0f, 1f), new Vector2(-0.9f, -0.6f), new Vector2(0.9f, -0.6f), new Vector2(0f, 1f) }, true); closed.Add("triangle");
            Add("bolt", new[] { new Vector2(0.35f, 1f), new Vector2(-0.35f, 0.1f), new Vector2(0.35f, -0.1f), new Vector2(-0.35f, -1f) }, false);
            var wave = new List<Vector2>();
            for (int i = 0; i <= 40; i++) { float x = -1f + i / 20f; wave.Add(new Vector2(x, 0.6f * Mathf.Sin(x * Mathf.PI * 1.5f))); }
            Add("wave", wave.ToArray(), true);
            Add("vee", new[] { new Vector2(-0.8f, 1f), new Vector2(0f, -1f), new Vector2(0.8f, 1f) }, true);
        }

        static void Add(string name, Vector2[] path, bool bothDirections)
        {
            guides[name] = Resample(path, N);
            var list = new List<Vector2[]> { Normalize(Resample(path, N)) };
            if (bothDirections)
            {
                var rev = (Vector2[])path.Clone(); System.Array.Reverse(rev);
                list.Add(Normalize(Resample(rev, N)));
            }
            templates[name] = list;
            templateDirs[name] = list.ConvertAll(Directions);
        }

        /// <summary>畫面提示用的軌跡（-1～1）</summary>
        public static Vector2[] Guide(string rune) => rune != null && guides.TryGetValue(rune, out var g) ? g : null;

        /// <summary>
        /// 筆畫和符文的距離（越小越像）：平均點距離＋筆畫方向的差異（分辨圓滑的圓和有尖角的三角形）。
        /// 封閉圖形把起點循環對齊（從哪裡開始畫都可以）。點太少回傳很大的值。
        /// </summary>
        public static float Score(IList<Vector2> stroke, string rune)
        {
            if (rune == null || stroke == null || stroke.Count < 5 || !templates.TryGetValue(rune, out var tps)) return 99f;
            var pts = Normalize(Resample(stroke, N));
            var dirs = Directions(pts);
            bool loop = closed.Contains(rune);
            float best = 99f;
            for (int ti = 0; ti < tps.Count; ti++)
            {
                var tp = tps[ti]; var tdirs = templateDirs[rune][ti];
                int shifts = loop ? N - 1 : 1;   // 封閉圖形的頭尾是同一點，循環 N-1 個位置
                for (int sh = 0; sh < shifts; sh++)
                {
                    float d = 0f, a = 0f;
                    for (int i = 0; i < N; i++)
                    {
                        int j = loop ? (i + sh) % (N - 1) : i;
                        d += Vector2.Distance(pts[i], tp[j]);
                    }
                    for (int i = 0; i < N - 1; i++)
                    {
                        int j = loop ? (i + sh) % (N - 1) : i;
                        a += 1f - Vector2.Dot(dirs[i], tdirs[j]);
                    }
                    best = Mathf.Min(best, d / N + 0.25f * a / (N - 1));
                }
            }
            return best;
        }

        static Vector2[] Directions(Vector2[] p)
        {
            var d = new Vector2[p.Length - 1];
            for (int i = 0; i < d.Length; i++) { var v = p[i + 1] - p[i]; d[i] = v.sqrMagnitude > 1e-12f ? v.normalized : Vector2.zero; }
            return d;
        }

        /// <summary>筆畫最像哪個符文（給「畫的是目標符文嗎」用：要是所有符文裡最像的那個）</summary>
        public static bool IsBest(IList<Vector2> stroke, string rune, float score)
        {
            foreach (var r in templates.Keys)
                if (r != rune && Score(stroke, r) < score) return false;
            return true;
        }

        /// <summary>從軌跡的不同起點往後找：有沒有一段（到最新一點為止）畫出了這個符文</summary>
        public static bool Recognize(IList<Vector2> trail, string rune, float minSize, out float score)
        {
            score = 99f;
            if (trail.Count < 6) return false;
            var seg = new List<Vector2>();
            for (int start = 0; start <= trail.Count - 6; start++)
            {
                seg.Clear();
                for (int i = start; i < trail.Count; i++) seg.Add(trail[i]);
                if (!BigEnough(seg, minSize)) break;   // 越往後越短，再短也不夠大
                float s = Score(seg, rune);
                if (s < score) score = s;
                if (s < Threshold && IsBest(seg, rune, s)) return true;
            }
            return false;
        }

        static bool BigEnough(List<Vector2> p, float minSize)
        {
            Vector2 lo = p[0], hi = p[0];
            foreach (var q in p) { lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
            return Mathf.Max(hi.x - lo.x, hi.y - lo.y) >= minSize;
        }

        static Vector2[] Resample(IList<Vector2> pts, int n)
        {
            float total = 0f;
            for (int i = 1; i < pts.Count; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
            var o = new Vector2[n];
            if (total < 1e-6f) { for (int i = 0; i < n; i++) o[i] = pts[0]; return o; }
            float step = total / (n - 1), acc = 0f;
            int k = 1; o[0] = pts[0];
            var prev = pts[0];
            for (int i = 1; i < pts.Count && k < n; i++)
            {
                var cur = pts[i];
                float d = Vector2.Distance(prev, cur);
                while (acc + d >= step && k < n && d > 1e-9f)
                {
                    float t = (step - acc) / d;
                    var q = prev + (cur - prev) * t;
                    o[k++] = q;
                    prev = q; d = Vector2.Distance(prev, cur); acc = 0f;
                }
                acc += d; prev = cur;
            }
            while (k < n) o[k++] = pts[pts.Count - 1];
            return o;
        }

        static Vector2[] Normalize(Vector2[] p)
        {
            Vector2 c = Vector2.zero, lo = p[0], hi = p[0];
            foreach (var q in p) { c += q; lo = Vector2.Min(lo, q); hi = Vector2.Max(hi, q); }
            c /= p.Length;
            float s = Mathf.Max(hi.x - lo.x, hi.y - lo.y);
            if (s < 1e-6f) s = 1f;
            var o = new Vector2[p.Length];
            for (int i = 0; i < p.Length; i++) o[i] = (p[i] - c) / s;
            return o;
        }
    }
}
