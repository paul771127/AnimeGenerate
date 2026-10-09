using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SpellDuel
{
    /// <summary>
    /// 單人模式的「遊戲場地」——像 Meta Quest 的守護者邊界：
    ///   1. 掃描地板：AR 偵測水平面，取最低的那片當地板高度。
    ///   2. 畫場地：「快速方形」在玩家前方放一個正方形；或「手繪」——按住螢幕把準星沿地板畫一圈。
    ///   3. 場地完成後，以場地中心為世界原點（+Z＝玩家當時面向的方向）。
    ///   4. 玩家靠近邊界時浮出半透明格子牆，走出場地時警告。
    /// 所有座標都在 AR session 空間（Y 朝上＝重力反方向）。
    /// </summary>
    public class PlayArea : MonoBehaviour
    {
        public enum State { Off, Scanning, Ready, Drawing, Done }

        const float MinPlaneArea = 0.3f;        // 小於這個面積的水平面不當地板（避免誤抓桌面碎片）
        const float DrawStep = 0.10f;           // 手繪時每 10 公分取一個點
        const float SimplifyTolerance = 0.06f;  // 手繪線簡化的容許誤差
        const float MinAreaM2 = 1.0f;           // 場地至少 1 平方公尺
        const float WallHeight = 2.2f;
        const float WallShowDistance = 1.0f;    // 離邊界這麼近時開始顯示格子牆

        public State state { get; private set; } = State.Off;
        public bool HasFloor { get; private set; }
        public float FloorY { get; private set; }
        public Vector3? Reticle { get; private set; }     // 準星在地板上的點
        public readonly List<Vector3> Polygon = new List<Vector3>();   // 場地邊界（地板高度）
        public Pose Origin { get; private set; } = Pose.identity;
        public string Hint { get; private set; } = "";
        public bool PenDown { get; set; }           // 手繪時：手指是否按著螢幕（由 GameRoot 設定）
        public int DrawnPoints => drawPts.Count;

        Camera cam;
        ARPlaneManager planes;
        Material lineMat, wallMat;
        LineRenderer outline, drawLine, reticleRing;
        readonly List<Vector3> drawPts = new List<Vector3>();
        readonly List<MeshRenderer> walls = new List<MeshRenderer>();

        public void Init(Camera camera, ARPlaneManager planeManager, Material baseMat)
        {
            cam = camera;
            planes = planeManager;
            lineMat = new Material(baseMat);
            wallMat = new Material(baseMat) { mainTexture = MakeGridTexture() };
            outline = MakeLine("PlayArea Outline", new Color(0.3f, 0.9f, 1f, 0.9f), 0.03f, true);
            drawLine = MakeLine("PlayArea Drawing", new Color(1f, 0.85f, 0.3f, 0.9f), 0.03f, false);
            reticleRing = MakeLine("Floor Reticle", new Color(1f, 1f, 1f, 0.8f), 0.012f, true);
        }

        public void Begin()
        {
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.enabled = true;
            if (state == State.Off) state = State.Scanning;
        }

        public void Clear()
        {
            Polygon.Clear();
            drawPts.Clear();
            outline.positionCount = 0;
            drawLine.positionCount = 0;
            foreach (var w in walls) if (w != null) Destroy(w.gameObject);
            walls.Clear();
            state = HasFloor ? State.Ready : State.Scanning;
        }

        // ---------------------------------------------------------------- 每幀：更新地板、準星、手繪、格子牆
        void Update()
        {
            if (state == State.Off || cam == null) return;
            UpdateFloor();
            UpdateReticle();

            if (state == State.Scanning)
                Hint = "慢慢移動手機，讓鏡頭掃過地板…";
            else if (state == State.Ready)
                Hint = "選方形場地大小（技能射程最遠 15m，建議 10m 以上的空地）或手繪場地";
            else if (state == State.Drawing)
            {
                Hint = $"按住螢幕，把準星沿著場地邊緣畫一圈，放開完成（{drawPts.Count} 點）";
                if (PenDown && Reticle.HasValue && (drawPts.Count == 0 || Flat(drawPts[drawPts.Count - 1] - Reticle.Value).magnitude >= DrawStep))
                {
                    drawPts.Add(Reticle.Value);
                    SetLine(drawLine, drawPts);
                }
            }
            else if (state == State.Done)
            {
                UpdateWalls();
                var me = Flat(cam.transform.position);
                Hint = Inside(me) ? "" : "⚠ 你在場地外面！請回到場地內";
            }
        }

        void UpdateFloor()
        {
            // 取「夠大的水平面」中最低的一片當地板（桌面、椅面會比地板高）
            bool found = false;
            float lowest = float.MaxValue;
            foreach (var p in planes.trackables)
            {
                if (p.alignment != PlaneAlignment.HorizontalUp || p.trackingState == TrackingState.None) continue;
                if (p.size.x * p.size.y < MinPlaneArea) continue;
                if (p.transform.position.y < lowest) { lowest = p.transform.position.y; found = true; }
            }
            if (!found) return;
            // 地板必須在手機下方 0.5 公尺以上
            if (lowest > cam.transform.position.y - 0.5f) return;
            FloorY = HasFloor ? Mathf.Lerp(FloorY, lowest, 0.1f) : lowest;
            if (!HasFloor) { HasFloor = true; if (state == State.Scanning) state = State.Ready; }
        }

        void UpdateReticle()
        {
            Reticle = null;
            if (!HasFloor) { reticleRing.positionCount = 0; return; }
            var ray = cam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f));
            // 射線與地板（水平面 y = FloorY）的交點
            if (ray.direction.y < -0.05f)
            {
                float t = (FloorY - ray.origin.y) / ray.direction.y;
                if (t > 0 && t < 8f) Reticle = ray.origin + ray.direction * t;
            }
            if (Reticle.HasValue && state != State.Done)
            {
                var pts = new List<Vector3>();
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f;
                    pts.Add(Reticle.Value + new Vector3(Mathf.Cos(a), 0.005f, Mathf.Sin(a)) * 0.08f);
                }
                SetLine(reticleRing, pts);
            }
            else reticleRing.positionCount = 0;
        }

        // ---------------------------------------------------------------- 建立場地
        /// <summary>在玩家腳下前方放一個正方形場地（玩家站在靠近自己這一側的邊內 0.5 公尺）</summary>
        public bool AutoSquare(float size)
        {
            if (!HasFloor) return false;
            var fwd = Flat(cam.transform.forward);
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var me = new Vector3(cam.transform.position.x, FloorY, cam.transform.position.z);
            var center = me + fwd * (size / 2f - 0.5f);
            float h = size / 2f;
            Polygon.Clear();
            Polygon.Add(center - right * h - fwd * h);
            Polygon.Add(center + right * h - fwd * h);
            Polygon.Add(center + right * h + fwd * h);
            Polygon.Add(center - right * h + fwd * h);
            Finish(fwd);
            return true;
        }

        public void StartDraw()
        {
            if (!HasFloor) return;
            Clear();
            state = State.Drawing;
        }

        /// <summary>放開手指：簡化手繪線、檢查面積，成功就建立場地</summary>
        public bool FinishDraw(out string error)
        {
            error = null;
            drawLine.positionCount = 0;
            if (drawPts.Count < 6) { error = "畫的範圍太小，請再畫一次"; state = State.Ready; return false; }
            var simplified = Simplify(drawPts, SimplifyTolerance);
            if (simplified.Count < 3) { error = "畫的形狀太細，請再畫一次"; state = State.Ready; return false; }
            float area = SignedArea(simplified);
            if (Mathf.Abs(area) < MinAreaM2) { error = $"場地太小（{Mathf.Abs(area):F1}㎡），至少要 {MinAreaM2}㎡"; state = State.Ready; return false; }
            if (area < 0) simplified.Reverse();   // 統一成逆時針（從上往下看）
            Polygon.Clear();
            foreach (var p in simplified) Polygon.Add(new Vector3(p.x, FloorY, p.z));
            var fwd = Flat(cam.transform.forward);
            Finish(fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward);
            return true;
        }

        void Finish(Vector3 forward)
        {
            // 原點＝場地中心（多邊形重心），+Z＝玩家完成時面向的方向
            Origin = new Pose(Centroid(Polygon), Quaternion.LookRotation(forward, Vector3.up));
            var loop = new List<Vector3>(Polygon);
            for (int i = 0; i < loop.Count; i++) loop[i] += Vector3.up * 0.01f;
            SetLine(outline, loop);
            BuildWalls();
            state = State.Done;
            reticleRing.positionCount = 0;
        }

        // ---------------------------------------------------------------- 查詢
        public bool Inside(Vector3 p)
        {
            if (Polygon.Count < 3) return true;
            bool inside = false;
            for (int i = 0, j = Polygon.Count - 1; i < Polygon.Count; j = i++)
            {
                var a = Polygon[i]; var b = Polygon[j];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x) inside = !inside;
            }
            return inside;
        }

        public float DistanceToEdge(Vector3 p)
        {
            float best = float.MaxValue;
            p = Flat(p);
            for (int i = 0; i < Polygon.Count; i++)
            {
                var a = Flat(Polygon[i]); var b = Flat(Polygon[(i + 1) % Polygon.Count]);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector3.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>從場地中心沿 dir 方向，最遠能走多遠還在場地內（留 margin）</summary>
        public float ReachInside(Vector3 dir, float margin)
        {
            dir = Flat(dir).normalized;
            var c = Flat(Origin.position);
            float d = 0f;
            for (float s = 0.1f; s < 30f; s += 0.1f)
            {
                var p = c + dir * s;
                if (!Inside(p) || DistanceToEdge(p) < margin) break;
                d = s;
            }
            return d;
        }

        // ---------------------------------------------------------------- 格子牆（靠近邊界才浮現）
        void BuildWalls()
        {
            foreach (var w in walls) if (w != null) Destroy(w.gameObject);
            walls.Clear();
            for (int i = 0; i < Polygon.Count; i++)
            {
                var a = Polygon[i]; var b = Polygon[(i + 1) % Polygon.Count];
                float len = Vector3.Distance(a, b);
                var mesh = new Mesh();
                mesh.vertices = new[] { a, b, b + Vector3.up * WallHeight, a + Vector3.up * WallHeight };
                // UV 依實際長度設定，讓格子每 0.25 公尺一格
                mesh.uv = new[] { new Vector2(0, 0), new Vector2(len / 0.25f, 0), new Vector2(len / 0.25f, WallHeight / 0.25f), new Vector2(0, WallHeight / 0.25f) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                var go = new GameObject("Boundary Wall " + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.material = new Material(wallMat);
                walls.Add(mr);
            }
        }

        void UpdateWalls()
        {
            var me = cam.transform.position;
            for (int i = 0; i < walls.Count; i++)
            {
                var a = Flat(Polygon[i]); var b = Flat(Polygon[(i + 1) % Polygon.Count]);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(Flat(me) - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float d = Vector3.Distance(Flat(me), a + ab * t);
                float alpha = Mathf.Clamp01(1f - d / WallShowDistance) * 0.85f;
                if (!Inside(Flat(me))) alpha = 0.85f;
                walls[i].enabled = alpha > 0.02f;
                walls[i].material.color = new Color(0.3f, 0.85f, 1f, alpha);
            }
        }

        // ---------------------------------------------------------------- 小工具
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 Centroid(List<Vector3> poly)
        {
            // 多邊形面積重心（比頂點平均更準，手繪點不均勻也沒關係）
            float a = 0, cx = 0, cz = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                float cross = p.x * q.z - q.x * p.z;
                a += cross; cx += (p.x + q.x) * cross; cz += (p.z + q.z) * cross;
            }
            if (Mathf.Abs(a) < 1e-6f)
            {
                var avg = Vector3.zero;
                foreach (var p in poly) avg += p;
                return avg / Mathf.Max(1, poly.Count);
            }
            a *= 0.5f;
            return new Vector3(cx / (6f * a), poly[0].y, cz / (6f * a));
        }

        static float SignedArea(List<Vector3> poly)
        {
            float s = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                s += p.x * q.z - q.x * p.z;
            }
            return s * 0.5f;
        }

        // Ramer–Douglas–Peucker（XZ 平面）：去掉手抖造成的多餘點
        static List<Vector3> Simplify(List<Vector3> pts, float tol)
        {
            if (pts.Count < 3) return new List<Vector3>(pts);
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                var (s, e) = stack.Pop();
                float maxD = 0; int idx = -1;
                var a = Flat(pts[s]); var b = Flat(pts[e]); var ab = b - a;
                for (int i = s + 1; i < e; i++)
                {
                    var p = Flat(pts[i]);
                    float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    float d = Vector3.Distance(p, a + ab * t);
                    if (d > maxD) { maxD = d; idx = i; }
                }
                if (idx >= 0 && maxD > tol) { keep[idx] = true; stack.Push((s, idx)); stack.Push((idx, e)); }
            }
            var result = new List<Vector3>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) result.Add(pts[i]);
            // 頭尾很近時（畫成一圈）去掉最後一點，避免重複
            if (result.Count > 3 && Vector3.Distance(Flat(result[0]), Flat(result[result.Count - 1])) < 0.3f) result.RemoveAt(result.Count - 1);
            return result;
        }

        LineRenderer MakeLine(string name, Color c, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.widthMultiplier = width;
            lr.material = lineMat;
            lr.startColor = lr.endColor = c;
            lr.positionCount = 0;
            lr.numCornerVertices = 2;
            return lr;
        }

        static void SetLine(LineRenderer lr, List<Vector3> pts)
        {
            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
        }

        static Texture2D MakeGridTexture()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    bool line = x < 3 || y < 3;
                    px[y * N + x] = line ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 40);
                }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }
}
