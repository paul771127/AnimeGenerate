using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SpatialTracking;

namespace SpellDuel
{
    /// <summary>
    /// 觀戰模式：
    ///   房主端：加入的人先送「join」說自己是玩家還是觀眾。觀眾不算玩家人數（最多 6 位玩家＋4 位觀眾），
    ///           房主在開打、結算、回準備畫面時把戰況送給觀眾（SoloBattle.WatchSnapshot），中途加入也一樣。
    ///   觀眾端：關掉 AR（不用鏡頭），改成斜上方的虛擬視角看整個場地：
    ///           單指拖曳＝移動、雙指捏合＝縮放、雙指轉動＝旋轉、雙指上下滑＝調整俯角；「🎯 跟隨」輪流跟著每位玩家／Boss。
    ///           地上是一片格子地板（無盡／副本有熔岩、森林、海上的場景）。
    /// </summary>
    public partial class GameRoot
    {
        // ================================================================ 房主端
        readonly List<int> watchers = new List<int>();
        SoloBattle.Phase lastWatchPhase = SoloBattle.Phase.Hidden;

        /// <summary>加入的人表明身分：觀眾另外記；玩家滿了（6 人）就拒絕</summary>
        void OnJoin(int pid, bool watch)
        {
            if (watch)
            {
                if (!watchers.Contains(pid)) watchers.Add(pid);
                if (roomPids.Remove(pid)) BroadcastPeers();
                var ks = new List<string> { "0" };
                foreach (var p in roomPids) ks.Add(p.ToString());
                net.SendTo(pid, new Msg { t = "peers", ks = ks.ToArray(), pid = net.MyPid }.ToJson());
                SendWatchSnapshot(pid);
                Log($"👀 有人開始觀戰（{watchers.Count} 人觀戰中）");
                return;
            }
            if (!roomPids.Contains(pid) && roomPids.Count >= NetLink.MaxClients)
            {
                net.SendTo(pid, "{\"t\":\"_full\"}");
                Log($"房間已滿（{NetLink.MaxClients + 1} 位玩家），拒絕了一位加入的人");
                return;
            }
            if (!roomPids.Contains(pid)) roomPids.Add(pid);
            roomPids.Sort();
            BroadcastPeers();
            Log($"✅ 隊友{pid} 加入（房間 {roomPids.Count + 1} 人）");
        }

        /// <summary>房主每幀：戰鬥開始／結算／回準備畫面時通知觀眾</summary>
        void UpdateWatchers()
        {
            var ph = solo.phase;
            if (!net.IsHost || watchers.Count == 0 || mode != Mode.Duo) { lastWatchPhase = ph; return; }
            if (ph == lastWatchPhase) return;
            lastWatchPhase = ph;
            if (ph == SoloBattle.Phase.Fighting) foreach (var w in watchers) SendWatchSnapshot(w);
            else if (ph == SoloBattle.Phase.Over) WatchSendAll(new Msg { t = "wover", k = solo.WatchOverText() });
            else WatchSendAll(new Msg { t = "wend" });
        }

        void WatchSendAll(Msg m) { m.pid = net.MyPid; var j = m.ToJson(); foreach (var w in watchers) net.SendTo(w, j); }

        void SendWatchSnapshot(int pid)
        {
            foreach (var m in solo.WatchSnapshot(WatchAreaWorld())) { m.pid = net.MyPid; net.SendTo(pid, m.ToJson()); }
        }

        /// <summary>房主的場地邊界（世界座標，地上）</summary>
        Vector3[] WatchAreaWorld()
        {
            var pts = new List<Vector3>();
            if (AreaMode && areaReceived) foreach (var w in areaWorld) pts.Add(new Vector3(w.x, 0f, w.z));
            else foreach (var q in playArea.Polygon) { var w = WorldFrame.ToWorld(q); pts.Add(new Vector3(w.x, 0f, w.z)); }
            return pts.ToArray();
        }

        // ================================================================ 觀眾端
        TrackedPoseDriver poseDriver;      // AR 鏡頭的追蹤（觀戰時關掉，改用自己的視角）
        GameObject watchGround;
        bool watchHint = true;
        // 視角：看著 wcTarget（地上），從 wcYaw 方向、俯角 wcPitch、距離 wcDist
        Vector3 wcTarget; float wcYaw, wcPitch = 52f, wcDist = 13f;
        public const float WatchMinDist = 3f, WatchMaxDist = 35f, WatchMinPitch = 20f, WatchMaxPitch = 88f;
        int wcFollow;              // 0＝不跟隨；i＝SoloBattle.WatchFocus(i)
        bool wcGesture; int wcPrevTouches; float wcPrevPinch, wcPrevAngle; Vector2 wcPrevMid, wcLastMouse;

        void EnterWatch()
        {
            if (!(net is RelayLink)) { net?.Dispose(); net = new RelayLink(); }
            mode = Mode.Watch;
            remoteMode = false; solo.RemotePlay = false; duoUseMarker = false;
            watchers.Clear();
            solo.BeginWatch();
            SetWatchView(true);
            ResetWatchCam();
            Log("觀戰：輸入朋友的房間代碼（玩家在選職業或戰鬥中都可以進來看）");
        }

        void ExitWatch()
        {
            solo.StopWatch();
            net.Dispose(); net = new NetLink();
            SetWatchView(false);
            WorldFrame.Reset(); playArea.Clear();
            mode = Mode.Choose;
        }

        /// <summary>觀戰：關掉 AR（鏡頭畫面、追蹤），改用自己控制的虛擬視角；離開時恢復</summary>
        void SetWatchView(bool on)
        {
            if (session) session.enabled = !on;
            if (cameraBackground) cameraBackground.enabled = !on;
            if (poseDriver != null) poseDriver.enabled = !on;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = on ? new Color(0.16f, 0.2f, 0.28f) : Color.black;
            cam.farClipPlane = on ? 120f : 60f;
            if (on) { cam.ResetProjectionMatrix(); cam.fieldOfView = 55f; }
            if (on) EnsureWatchGround();
            if (watchGround) watchGround.SetActive(on);
        }

        /// <summary>格子地板（80m×80m，每格 1m）：沒有場景的模式（對戰、合作 Boss）也看得到地面</summary>
        void EnsureWatchGround()
        {
            if (watchGround) return;
            const float half = 40f, tiles = 80f;
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-half, -0.03f, -half), new Vector3(-half, -0.03f, half), new Vector3(half, -0.03f, half), new Vector3(half, -0.03f, -half) },
                uv = new[] { new Vector2(0, 0), new Vector2(0, tiles), new Vector2(tiles, tiles), new Vector2(tiles, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[64 * 64];
            var baseC = new Color(0.2f, 0.25f, 0.3f); var line = new Color(0.36f, 0.43f, 0.5f);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) px[y * 64 + x] = x < 2 || y < 2 ? line : baseC;
            tex.SetPixels(px); tex.Apply(true);
            watchGround = new GameObject("Watch Ground");
            watchGround.AddComponent<MeshFilter>().sharedMesh = mesh;
            watchGround.AddComponent<MeshRenderer>().material = new Material(mat) { mainTexture = tex };
        }

        void ResetWatchCam() { wcTarget = new Vector3(0f, 0.8f, 0f); wcYaw = 0f; wcPitch = 52f; wcDist = 13f; wcFollow = 0; }

        void UpdateWatchMode()
        {
            if (net.Connected && !net.IsHost && Time.time >= nextPing)
            {
                nextPing = Time.time + 2f;
                Send(new Msg { t = "ping", c = LocalTime });
            }
            solo.NetIsHost = false;
            solo.MyPid = Mathf.Max(0, net.MyPid);
            UpdateWatchCam();
        }

        /// <summary>觸控：單指拖曳＝移動；雙指捏合＝縮放、轉動＝旋轉、一起上下滑＝俯角。滑鼠（編輯器）：左鍵移動、右鍵旋轉、滾輪縮放</summary>
        void UpdateWatchCam()
        {
            float H = Screen.height;
            bool InView(Vector2 p) => p.y > H * 0.14f && p.y < H * 0.8f && GUIUtility.hotControl == 0;   // 上方血條、下方按鈕區不算
            if (Input.touchCount >= 2)
            {
                var t0 = Input.GetTouch(0); var t1 = Input.GetTouch(1);
                float dist = Vector2.Distance(t0.position, t1.position);
                float ang = Mathf.Atan2(t1.position.y - t0.position.y, t1.position.x - t0.position.x) * Mathf.Rad2Deg;
                var mid = (t0.position + t1.position) / 2f;
                if (wcPrevTouches == 2 && wcGesture)
                {
                    if (dist > 1f && wcPrevPinch > 1f) wcDist = Mathf.Clamp(wcDist * wcPrevPinch / dist, WatchMinDist, WatchMaxDist);
                    wcYaw += Mathf.DeltaAngle(wcPrevAngle, ang);
                    wcPitch = Mathf.Clamp(wcPitch - (mid.y - wcPrevMid.y) / H * 120f, WatchMinPitch, WatchMaxPitch);
                }
                else wcGesture = InView(mid);
                wcPrevPinch = dist; wcPrevAngle = ang; wcPrevMid = mid; wcPrevTouches = 2;
            }
            else if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began || wcPrevTouches != 1) wcGesture = InView(t.position);   // 從雙指放開一指：重新判斷
                else if (wcGesture && t.phase == TouchPhase.Moved) WatchPan(t.deltaPosition);
                wcPrevTouches = 1;
            }
            else
            {
                wcPrevTouches = 0;
                Vector2 mp = Input.mousePosition;
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { wcGesture = InView(mp); wcLastMouse = mp; }
                var d = mp - wcLastMouse; wcLastMouse = mp;
                if (wcGesture && Input.GetMouseButton(0)) WatchPan(d);
                else if (wcGesture && Input.GetMouseButton(1)) { wcYaw += d.x * 0.3f; wcPitch = Mathf.Clamp(wcPitch - d.y * 0.2f, WatchMinPitch, WatchMaxPitch); }
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) wcDist = Mathf.Clamp(wcDist * (1f - wheel * 0.1f), WatchMinDist, WatchMaxDist);
            }
            // 跟隨：視角中心慢慢移到那位玩家／Boss
            if (wcFollow > 0)
            {
                if (wcFollow >= solo.WatchFocusCount || !solo.WatchFocus(wcFollow, out var fw, out _)) wcFollow = 0;
                else wcTarget = Vector3.Lerp(wcTarget, new Vector3(fw.x, 0.8f, fw.z), 1f - Mathf.Exp(-Time.deltaTime * 4f));
            }
            var rot = Quaternion.Euler(wcPitch, wcYaw, 0f);
            cam.transform.SetPositionAndRotation(WorldFrame.FromWorld(wcTarget) + rot * new Vector3(0f, 0f, -wcDist), rot);
        }

        /// <summary>拖曳移動：手指往右＝畫面往右（視角中心往左），每像素的距離跟著縮放</summary>
        void WatchPan(Vector2 d)
        {
            wcFollow = 0;   // 自己移動就不跟隨了
            float k = 2f * wcDist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1f, Screen.height);
            var q = Quaternion.Euler(0f, wcYaw, 0f);
            wcTarget -= (q * Vector3.right * d.x + q * Vector3.forward * d.y) * k;
            var flat = new Vector2(wcTarget.x, wcTarget.z);
            if (flat.magnitude > 30f) { flat = flat.normalized * 30f; wcTarget = new Vector3(flat.x, wcTarget.y, flat.y); }
        }

        void DrawWatch(float W, float H, float pad, float top, float lineH, int fs, int fsS)
        {
            var rl = net as RelayLink;
            float bh = fs * 2.3f, full = W - pad * 2, half = (full - pad) / 2f;
            if (!net.Connected)
            {
                // ---- 輸入房間代碼
                UiKit.Backdrop(new Rect(0, 0, W, H), new Color(0.55f, 0.9f, 1f), 0.5f);
                float y = top + lineH;
                var card = new Rect(pad * 0.5f, y - pad * 0.5f, W - pad, lineH * 12f);
                UiKit.Card(card, 0.9f, new Color(0.55f, 0.9f, 1f), 0.9f);
                UiKit.Title(new Rect(pad, y, full, lineH * 1.2f), "👀 觀戰", new Color(0.55f, 0.9f, 1f), Mathf.RoundToInt(fs * 1.2f));
                y += lineH * 1.5f;
                UiKit.Text(new Rect(pad, y, full, lineH * 2f), "輸入朋友的房間代碼：從斜上方看整場戰鬥（對戰、合作打 Boss、無盡、副本都可以；打到一半也能進來）。觀眾不算玩家人數，最多 4 人。", fsS, new Color(0.85f, 0.9f, 1f));
                y += lineH * 2.4f;
                bool busy = rl != null && rl.Busy;
                UiKit.Card(new Rect(pad, y, half, bh), 0.95f, new Color(0.55f, 0.9f, 1f), 0.8f);
                codeInput = GUI.TextField(new Rect(pad + 4, y + 3, half - 8, bh - 6), codeInput, 8, LobbyInput(fs)).ToUpperInvariant();
                if (UiKit.Button(new Rect(pad * 2 + half, y, half, bh), "加入觀戰", fs, true, new Color(0.55f, 0.9f, 1f), !busy && codeInput.Trim().Length >= 4))
                { PlayerPrefs.SetString("sd_last_code", codeInput.Trim()); rl?.Join(codeInput); }
                y += bh + pad * 0.5f;
                if (UiKit.Button(new Rect(pad, y, full, bh), "📋 貼上朋友的代碼並加入", fsS, false, new Color(0.55f, 0.9f, 1f), !busy))
                {
                    var code = FindJoinCode(GUIUtility.systemCopyBuffer);
                    if (code == null) Log("剪貼簿裡沒有房間代碼：請先複製朋友傳來的代碼");
                    else { codeInput = code; PlayerPrefs.SetString("sd_last_code", code); rl?.Join(code); }
                }
                y += bh + pad * 0.5f;
                UiKit.Text(new Rect(pad, y, full, lineH), $"⏳ {net.Status}", fsS, new Color(1f, 0.9f, 0.6f));
                y += lineH * 1.3f;
                if (UiKit.Button(new Rect(pad, y, full, bh * 0.85f), "← 回首頁", fsS)) ExitWatch();
                return;
            }

            // ---- 觀戰中：戰況＋下方按鈕
            solo.DrawWatchHud(W, H, pad, top, lineH, fs, fsS, cam);
            var safe = Screen.safeArea;
            float by = H - safe.y - pad - bh;
            float bw = (full - pad * 2) / 3f;
            if (UiKit.Button(new Rect(pad, by, bw, bh), "離開觀戰", fsS)) { ExitWatch(); return; }
            string fname = "全場";
            if (wcFollow > 0 && solo.WatchFocus(wcFollow, out _, out var fn)) fname = fn;
            if (UiKit.Button(new Rect(pad * 2 + bw, by, bw, bh), "🎯 " + fname, fsS, wcFollow > 0, new Color(1f, 0.85f, 0.4f)))
            {
                int n = Mathf.Max(1, solo.WatchFocusCount);
                wcFollow = (wcFollow + 1) % n;
                if (wcFollow == 0 && solo.WatchFocus(0, out var cw, out _)) wcTarget = new Vector3(cw.x, 0.8f, cw.z);
            }
            if (UiKit.Button(new Rect(pad * 3 + bw * 2, by, bw, bh), "↺ 重設視角", fsS)) { ResetWatchCam(); if (solo.WatchFocus(0, out var c0, out _)) wcTarget = new Vector3(c0.x, 0.8f, c0.z); }
            if (watchHint)
            {
                UiKit.Pill(by - lineH * 0.9f, "單指拖曳移動・雙指縮放／轉動・雙指上下滑調整角度", Mathf.RoundToInt(fsS * 0.9f), Color.white, full);
                if (Input.touchCount >= 2 || Input.GetMouseButton(1)) watchHint = false;
            }
        }
    }
}
