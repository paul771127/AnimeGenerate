using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SpellDuel
{
    /// <summary>
    /// 第 1 階段：共用房間座標的雙人 AR 對戰骨架。
    ///   1. 用程式建立 AR 鏡頭（ARKit / ARCore 由 AR Foundation 自動選擇）。
    ///   2. 掃描地上的標記圖 → 以它為世界原點（WorldFrame）。
    ///   3. 區域網路連線、對時（房主時鐘為共同時間）。
    ///   4. 每秒 20 次交換雙方手機在世界座標的位置 → 顯示對手的身體判定框。
    ///   5. 對手出現在畫面中才能鎖定、施法。點螢幕發射法術：送出「起點、方向、速度、半徑、發射時間」，
    ///      兩邊用同一公式模擬，位置必定一致；由「攻擊方」用對手回報的世界座標判定是否命中，
    ///      再廣播結果——對手手機轉向哪裡、追蹤有沒有中斷，都不影響判定。
    /// 場景裡不需要任何物件：遊戲啟動後由這支程式建立一切。
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<GameRoot>() != null) return;
            var go = new GameObject("SpellDuel");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        // ---------------------------------------------------------------- 參數
        const float MarkerWidthMeters = 0.20f;   // 印出來的標記圖寬度（A4 列印版是 20 公分）
        const float RecalibrateMaxDistance = 1.5f; // 離標記圖這麼近時才持續修正原點（越近越準）
        const float BodyRadius = 0.28f;           // 玩家身體判定半徑
        const float BodyHeight = 1.45f;           // 從手機往下的身體長度
        const float ShotSpeed = 6f, ShotRadius = 0.15f, ShotRange = 15f, ShotCooldown = 0.5f;
        const int ShotDamage = 10, MaxHp = 100;

        // ---------------------------------------------------------------- 模式
        // 單人：畫場地（像 Meta Quest 的邊界），不需要標記圖
        // 雙人：兩支手機掃同一張標記圖，共用座標
        enum Mode { Choose, Solo, Duo }
        Mode mode = Mode.Choose;

        // ---------------------------------------------------------------- AR
        ARSession session;
        Camera cam;
        ARTrackedImageManager images;
        ARPlaneManager planeManager;
        PlayArea playArea;
        SoloBattle solo;
        bool imageTrackingStarted;
        float lastBoundaryBuzz;
        string arNote = "";

        // ---------------------------------------------------------------- 連線與時間
        NetLink net;
        double clockOffset;          // 共同時間 = 本機時間 + clockOffset（房主為 0）
        double bestRtt = double.MaxValue;
        float nextPing, nextPose;
        string ipInput = "192.168.";

        static double LocalTime => Time.realtimeSinceStartupAsDouble;
        double SharedTime => LocalTime + clockOffset;

        // ---------------------------------------------------------------- 玩家
        int hp = MaxHp, remoteHp = MaxHp;
        Vector3 remoteHeadW;  Quaternion remoteRotW = Quaternion.identity;
        float remoteSeen = -999f;    // 最後一次收到對手位置的時間（對手追蹤中斷時停在最後的正確位置）
        bool remoteTrackingOk = true, lastSentTracking = true;
        float hitFlash;   // 被打中時全畫面閃紅
        bool practiceDummy;
        Vector3 dummyHeadW = new Vector3(0f, 1.6f, 0f);   // 練習假人頭部（世界座標）：雙人模式站在標記圖上，單人模式站在場地內玩家對面

        // ---------------------------------------------------------------- 法術
        class Shot
        {
            public int id; public bool mine;
            public Vector3 o, d; public float s, rad; public int dmg; public double t0;
            public bool resolved; public GameObject go;
            public Vector3 WorldAt(double t) => o + d * (s * (float)(t - t0));
        }
        readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();
        int shotCounter;
        float lastShot = -999f;
        readonly HashSet<int> damaged = new HashSet<int>();   // 已扣過血的法術（避免重複扣）
        bool targetOnScreen;      // 對手（或假人）有沒有出現在我的畫面中
        Rect targetRect;          // 對手在螢幕上的範圍（GUI 座標），畫鎖定框用

        // ---------------------------------------------------------------- 顯示
        Material mat;
        GameObject remoteHead, remoteBody, dummyHead, dummyBody, markerGizmo;
        readonly List<string> log = new List<string>();
        GUIStyle label, button, field, big;

        // ================================================================ 啟動
        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            mat = new Material(Shader.Find("Sprites/Default"));
            BuildArRig();
            BuildVisuals();
            net = new NetLink();
            ipInput = PlayerPrefs.GetString("sd_last_ip", ipInput);
            Log("請選擇：單人（畫場地）或雙人（標記圖）");
        }

        void OnDestroy() => net?.Dispose();

        void BuildArRig()
        {
            var sessionGo = new GameObject("AR Session");
            session = sessionGo.AddComponent<ARSession>();
            sessionGo.AddComponent<ARInputManager>();

            var originGo = new GameObject("XR Origin");
            originGo.SetActive(false);   // 全部設定好再啟用，避免元件在設定前就啟動
            var origin = originGo.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGo.transform, false);

            var camGo = new GameObject("AR Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(offset.transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            camGo.AddComponent<ARCameraManager>();
            camGo.AddComponent<ARCameraBackground>();
            var tpd = camGo.AddComponent<TrackedPoseDriver>();
            tpd.SetPoseSource(TrackedPoseDriver.DeviceType.GenericXRDevice, TrackedPoseDriver.TrackedPose.ColorCamera);
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset;

            images = originGo.AddComponent<ARTrackedImageManager>();
            images.enabled = false;      // 參考圖庫準備好才啟用
            images.trackedImagesChanged += OnTrackedImagesChanged;

            planeManager = originGo.AddComponent<ARPlaneManager>();
            planeManager.enabled = false; // 單人模式才偵測地板

            originGo.SetActive(true);

            playArea = new GameObject("Play Area").AddComponent<PlayArea>();
            playArea.Init(cam, planeManager, mat);

            solo = gameObject.AddComponent<SoloBattle>();
            solo.Init(cam, playArea, mat);
            solo.RequestRedraw = () => { solo.Hide(); playArea.Clear(); WorldFrame.Reset(); Log("請重新畫場地"); };
            solo.RequestChangeMode = () => { solo.Hide(); playArea.Clear(); WorldFrame.Reset(); mode = Mode.Choose; };
        }

        // 執行期建立「可變參考圖庫」，把內建的標記圖加進去（不需要在編輯器裡建圖庫資產）
        IEnumerator SetupImageTracking()
        {
            while (ARSession.state < ARSessionState.Ready)
            {
                if (ARSession.state == ARSessionState.Unsupported) { arNote = "這支手機不支援 AR"; yield break; }
                yield return null;
            }

            var bytes = Resources.Load<TextAsset>("marker");
            if (bytes == null) { arNote = "找不到內建標記圖"; yield break; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes.bytes);

            RuntimeReferenceImageLibrary lib = null;
            string err = null;
            try { lib = images.CreateRuntimeLibrary(); }
            catch (System.Exception e) { err = e.Message; }
            if (lib == null) { arNote = "無法建立圖庫：" + err; yield break; }

            if (lib is MutableRuntimeReferenceImageLibrary mutable)
            {
                AddReferenceImageJobState job = default;
                try { job = mutable.ScheduleAddImageWithValidationJob(tex, "spellduel-marker", MarkerWidthMeters); }
                catch (System.Exception e) { err = e.Message; }
                if (err != null) { arNote = "標記圖加入失敗：" + err; yield break; }
                while (!job.jobHandle.IsCompleted) yield return null;
                job.jobHandle.Complete();
                if (job.status != AddReferenceImageJobStatus.Success) { arNote = "標記圖品質檢查失敗：" + job.status; yield break; }
            }
            else { arNote = "這支手機不支援執行期圖庫"; yield break; }

            images.referenceLibrary = lib;
            images.requestedMaxNumberOfMovingImages = 1;
            images.enabled = true;
            arNote = "標記圖已就緒";
        }

        void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs e)
        {
            foreach (var img in e.added) ConsiderMarker(img);
            foreach (var img in e.updated) ConsiderMarker(img);
        }

        void ConsiderMarker(ARTrackedImage img)
        {
            if (mode != Mode.Duo || img.trackingState != TrackingState.Tracking) return;
            // 第一次看到就對齊；之後只在靠近標記圖時才更新（距離越近估得越準）
            float dist = Vector3.Distance(cam.transform.position, img.transform.position);
            if (WorldFrame.Calibrated && dist > RecalibrateMaxDistance) return;
            bool first = !WorldFrame.Calibrated;
            WorldFrame.SetMarker(new Pose(img.transform.position, img.transform.rotation));
            if (first) Log("✅ 座標已對齊");
        }

        // ================================================================ 每幀
        void Update()
        {
            Tracking.Tick(Time.time);
            hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 2.5f);
            if (mode == Mode.Solo) UpdateSolo();
            HandleNetwork();
            // 追蹤狀態改變時告訴對手（對手畫面上我的判定框變灰，停在最後的正確位置）
            if (net.Connected && Tracking.Ok != lastSentTracking)
            {
                lastSentTracking = Tracking.Ok;
                net.Send(new Msg { t = "track", ok = Tracking.Ok }.ToJson());
                Log(Tracking.Ok ? "AR 追蹤恢復" : "⚠ AR 追蹤中斷：暫時不能施法，對手以你最後的位置判定");
            }
            if (net.Connected && !net.IsHost && Time.time >= nextPing)
            {
                nextPing = Time.time + 2f;
                net.Send(new Msg { t = "ping", c = LocalTime }.ToJson());
            }
            if (net.Connected && WorldFrame.Calibrated && Tracking.Ok && Time.time >= nextPose)
            {
                nextPose = Time.time + 0.05f;
                net.Send(new Msg
                {
                    t = "pose",
                    p = WorldFrame.ToWorld(cam.transform.position),
                    r = WorldFrame.RotToWorld(cam.transform.rotation),
                    hp = hp,
                }.ToJson());
            }
            UpdateTarget();
            HandleFire();
            UpdateShots();
            UpdateVisuals();
        }

        // ---------------------------------------------------------------- 模式切換與單人場地
        void ChooseMode(Mode m)
        {
            mode = m;
            if (m == Mode.Solo)
            {
                WorldFrame.Reset();
                playArea.Begin();
                practiceDummy = false;
                Log("單人模式：先掃地板，再畫遊戲場地");
            }
            else
            {
                if (!imageTrackingStarted) { imageTrackingStarted = true; StartCoroutine(SetupImageTracking()); }
                Log("雙人模式：把鏡頭對準地上的標記圖來對齊座標");
            }
        }

        bool PointerInPlayZone => Screen.height - Input.mousePosition.y >= Screen.height * 0.32f;

        void UpdateSolo()
        {
            // 手繪：按住螢幕下方區域時記錄準星軌跡，放開就完成
            if (playArea.state == PlayArea.State.Drawing)
            {
                bool held = Input.GetMouseButton(0) && PointerInPlayZone && GUIUtility.hotControl == 0;
                playArea.PenDown = held;
                if (!held && playArea.DrawnPoints > 0)
                {
                    if (playArea.FinishDraw(out var err)) OnPlayAreaReady();
                    else Log(err);
                }
            }
            // 走出場地：每秒震動一次
            if (playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position) && Time.time - lastBoundaryBuzz > 1f)
            {
                lastBoundaryBuzz = Time.time;
                Handheld.Vibrate();
            }
        }

        void OnPlayAreaReady()
        {
            // 場地中心＝世界原點；假人站在場地內、玩家面向的那一側（離邊界留 0.6 公尺，最遠 3 公尺）
            WorldFrame.SetMarker(playArea.Origin);
            solo.ShowSetup();
            Log("✅ 場地完成！選職業開始戰鬥");
        }

        // ---------------------------------------------------------------- 網路訊息
        void HandleNetwork()
        {
            while (net.TryReceive(out var line))
            {
                Msg m;
                try { m = Msg.FromJson(line); } catch { continue; }
                switch (m.t)
                {
                    case "_open":
                        Log("✅ 已連線");
                        hp = remoteHp = MaxHp;
                        damaged.Clear();
                        if (!net.IsHost) net.Send(new Msg { t = "ping", c = LocalTime }.ToJson());
                        break;
                    case "_close":
                        Log("❌ 對手已斷線");
                        break;
                    case "ping":
                        net.Send(new Msg { t = "pong", c = m.c, h = LocalTime }.ToJson());
                        break;
                    case "pong":
                    {
                        double now = LocalTime, rtt = now - m.c;
                        // 取來回時間最短的樣本最準；舊樣本慢慢放寬，避免時鐘漂移後卡在舊值
                        bestRtt *= 1.05;
                        if (rtt <= bestRtt)
                        {
                            bestRtt = rtt;
                            clockOffset = m.h + rtt / 2 - now;
                        }
                        break;
                    }
                    case "pose":
                        remoteHeadW = m.p; remoteRotW = m.r; remoteHp = m.hp; remoteSeen = Time.time;
                        break;
                    case "shot":
                        SpawnShot(new Shot { id = m.id, mine = false, o = m.p, d = m.d, s = m.s, rad = m.rad, dmg = m.dmg, t0 = m.t0 });
                        break;
                    case "hit":
                        // 攻擊方判定我被打中 → 扣血、震動、閃紅，回報最新血量
                        if (damaged.Add(m.id))
                        {
                            hp = Mathf.Max(0, hp - m.dmg);
                            net.Send(new Msg { t = "hp", hp = hp }.ToJson());
                            if (shots.TryGetValue(m.id, out var hs)) Explode(hs, new Color(1f, 0.2f, 0.2f), $"被打中 -{m.dmg}");
                            else Log($"被打中 -{m.dmg}");
                            hitFlash = 1f;
                            Handheld.Vibrate();
                            if (hp <= 0) Log("💀 敗北");
                        }
                        break;
                    case "miss":
                        if (shots.TryGetValue(m.id, out var ms)) Fizzle(ms, "閃過了！");
                        break;
                    case "hp":
                        remoteHp = m.hp;
                        break;
                    case "track":
                        remoteTrackingOk = m.ok;
                        Log(m.ok ? "對手 AR 追蹤恢復" : "⚠ 對手 AR 追蹤中斷（以最後位置判定）");
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- 發射
        void HandleFire()
        {
            if (!Input.GetMouseButtonDown(0) || GUIUtility.hotControl != 0) return;
            var sp = Input.mousePosition;
            if (!PointerInPlayZone) return;   // 上方是操作面板
            if (mode != Mode.Duo) return;   // 單人模式的操作由 SoloBattle 處理
            if (mode == Mode.Solo && playArea.state != PlayArea.State.Done) return;   // 畫場地時按螢幕是在畫線
            if (!WorldFrame.Calibrated) { Log(mode == Mode.Solo ? "請先畫好場地" : "請先掃描標記圖對齊座標"); return; }
            if (!Tracking.Ok) { Log("AR 追蹤中斷，暫時不能施法"); return; }
            if (!HasTarget) { Log("還沒有對手：請先連線（或開練習假人）"); return; }
            if (!targetOnScreen) { Log("🎯 對手不在畫面中，無法鎖定"); return; }
            if (Time.time - lastShot < ShotCooldown) return;
            lastShot = Time.time;

            var ray = cam.ScreenPointToRay(sp);
            var originS = cam.transform.position + cam.transform.forward * 0.3f - cam.transform.up * 0.08f;
            var shot = new Shot
            {
                id = (net.IsHost ? 1 : 2) * 100000 + (++shotCounter),
                mine = true,
                o = WorldFrame.ToWorld(originS),
                d = WorldFrame.DirToWorld(ray.direction).normalized,
                s = ShotSpeed, rad = ShotRadius, dmg = ShotDamage,
                t0 = SharedTime,
            };
            SpawnShot(shot);
            net.Send(new Msg { t = "shot", id = shot.id, p = shot.o, d = shot.d, s = shot.s, rad = shot.rad, dmg = shot.dmg, t0 = shot.t0 }.ToJson());
        }

        void SpawnShot(Shot s)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * s.rad * 2f;
            var r = go.GetComponent<Renderer>();
            r.material = mat;
            r.material.color = s.mine ? new Color(1f, 0.6f, 0.1f, 0.9f) : new Color(0.7f, 0.4f, 1f, 0.9f);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f; trail.startWidth = s.rad * 1.6f; trail.endWidth = 0f;
            trail.material = mat; trail.startColor = r.material.color; trail.endColor = new Color(1, 1, 1, 0);
            s.go = go;
            shots[s.id] = s;
            go.transform.position = WorldFrame.FromWorld(s.WorldAt(SharedTime));
        }

        // ---------------------------------------------------------------- 鎖定目標
        bool DummyActive => practiceDummy && !net.Connected;
        bool HasTarget => WorldFrame.Calibrated && (DummyActive || (net.Connected && remoteSeen > 0f));
        Vector3 TargetHeadW => DummyActive ? dummyHeadW : remoteHeadW;

        /// <summary>對手（或假人）的身體有沒有出現在我的畫面中；同時算出畫鎖定框的範圍</summary>
        void UpdateTarget()
        {
            targetOnScreen = false;
            if (mode != Mode.Duo || !HasTarget || !Tracking.Ok) return;
            var head = WorldFrame.FromWorld(TargetHeadW);
            var pts = new[] { head + Vector3.up * 0.15f, head + Vector3.down * (BodyHeight * 0.5f), head + Vector3.down * BodyHeight };
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            int inView = 0;
            foreach (var p in pts)
            {
                var v = cam.WorldToViewportPoint(p);
                if (v.z < 0.2f) continue;   // 在背後或太近
                if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) inView++;
                var sp = cam.WorldToScreenPoint(p);
                xMin = Mathf.Min(xMin, sp.x); xMax = Mathf.Max(xMax, sp.x);
                yMin = Mathf.Min(yMin, sp.y); yMax = Mathf.Max(yMax, sp.y);
            }
            // 頭、胸、腳至少一處在畫面內就算看得到
            targetOnScreen = inView > 0;
            if (targetOnScreen)
            {
                var c = WorldFrame.FromWorld(TargetHeadW) - cam.transform.position;
                float halfW = BodyRadius / Mathf.Max(0.3f, Vector3.Dot(c, cam.transform.forward)) * Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                float cx = (xMin + xMax) * 0.5f;
                targetRect = Rect.MinMaxRect(cx - halfW, Screen.height - yMax, cx + halfW, Screen.height - yMin);
            }
        }

        // ---------------------------------------------------------------- 法術模擬與命中判定
        void UpdateShots()
        {
            double now = SharedTime;
            var remove = new List<int>();
            foreach (var s in shots.Values)
            {
                if (s.go == null) { remove.Add(s.id); continue; }
                var posW = s.WorldAt(now);
                if (!s.resolved) s.go.transform.position = WorldFrame.FromWorld(posW);
                double age = now - s.t0, life = ShotRange / s.s;
                if (s.resolved) continue;

                if (s.mine)
                {
                    // 我是攻擊方：全部用世界座標判定（對手回報的位置），和任何一方手機朝向哪裡無關。
                    // 對手 AR 追蹤中斷時不再回報位置 → 用他最後的正確位置判定。
                    if (DummyActive)
                    {
                        if (HitsBody(posW, dummyHeadW, Vector3.zero, s.rad)) { Explode(s, new Color(1f, 0.3f, 0.2f), "命中假人！"); continue; }
                    }
                    else if (net.Connected && remoteSeen > 0f)
                    {
                        var remoteFwd = remoteRotW * Vector3.forward;
                        if (HitsBody(posW, remoteHeadW, remoteFwd, s.rad))
                        {
                            net.Send(new Msg { t = "hit", id = s.id, dmg = s.dmg }.ToJson());
                            remoteHp = Mathf.Max(0, remoteHp - s.dmg);   // 先顯示，對手回報 hp 後再校正
                            Explode(s, new Color(1f, 0.3f, 0.2f), "命中！");
                            if (remoteHp <= 0) Log("🏆 勝利");
                            continue;
                        }
                    }
                    if (age > life)
                    {
                        if (net.Connected) net.Send(new Msg { t = "miss", id = s.id }.ToJson());
                        Fizzle(s, "沒打中");
                    }
                }
                else if (age > life + 2.0)
                {
                    // 對手的法術：結果由對手判定；太久沒收到結果就自行消失
                    Fizzle(s, "");
                }
            }
            foreach (var id in remove) shots.Remove(id);
        }

        /// <summary>球體是否碰到玩家身體（膠囊：從手機往下，沿重力方向；略往後退一點）。世界座標或本機座標皆可，只要三個參數同一座標系</summary>
        static bool HitsBody(Vector3 p, Vector3 headPos, Vector3 headForward, float rad)
        {
            var back = Vector3.ProjectOnPlane(headForward, Vector3.up);
            back = back.sqrMagnitude > 1e-4f ? -back.normalized * 0.12f : Vector3.zero;
            var a = headPos + back + Vector3.up * 0.1f;
            var b = headPos + back + Vector3.down * BodyHeight;
            return DistancePointSegment(p, a, b) < rad + BodyRadius;
        }

        static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t);
        }

        void Explode(Shot s, Color c, string text)
        {
            s.resolved = true;
            if (!string.IsNullOrEmpty(text)) Log(text);
            if (s.go != null) StartCoroutine(Burst(s.go, c));
        }

        void Fizzle(Shot s, string text)
        {
            s.resolved = true;
            if (!string.IsNullOrEmpty(text)) Log(text);
            if (s.go != null) Destroy(s.go);
            s.go = null;   // 下一輪 UpdateShots 會把它移除（不能在走訪字典時直接刪）
        }

        IEnumerator Burst(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            float t = 0f, baseScale = go.transform.localScale.x;
            while (t < 0.4f && go != null)
            {
                t += Time.deltaTime;
                go.transform.localScale = Vector3.one * baseScale * (1f + t * 8f);
                r.material.color = new Color(c.r, c.g, c.b, 1f - t / 0.4f);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ---------------------------------------------------------------- 顯示對手、假人、標記圖
        void BuildVisuals()
        {
            remoteBody = MakePrim(PrimitiveType.Capsule, new Color(0.3f, 0.9f, 1f, 0.35f));
            remoteHead = MakePrim(PrimitiveType.Sphere, new Color(0.3f, 0.9f, 1f, 0.6f));
            dummyBody = MakePrim(PrimitiveType.Capsule, new Color(1f, 0.75f, 0.3f, 0.45f));
            dummyHead = MakePrim(PrimitiveType.Sphere, new Color(1f, 0.75f, 0.3f, 0.7f));
            markerGizmo = MakePrim(PrimitiveType.Cube, new Color(0.2f, 1f, 0.4f, 0.35f));
            markerGizmo.transform.localScale = new Vector3(MarkerWidthMeters, 0.005f, MarkerWidthMeters);
            var arrow = MakePrim(PrimitiveType.Cube, new Color(0.2f, 1f, 0.4f, 0.8f));   // 標記圖的「前方」（Z）
            arrow.transform.SetParent(markerGizmo.transform, false);
            arrow.transform.localScale = new Vector3(0.1f, 2f, 1.5f);
            arrow.transform.localPosition = new Vector3(0f, 0f, 1.2f);
        }

        GameObject MakePrim(PrimitiveType type, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = mat;
            r.material.color = c;
            go.SetActive(false);
            return go;
        }

        void PlaceBody(GameObject head, GameObject body, Vector3 headS, bool show)
        {
            head.SetActive(show); body.SetActive(show);
            if (!show) return;
            head.transform.position = headS;
            head.transform.localScale = Vector3.one * 0.24f;
            // Unity 膠囊預設高 2、半徑 0.5 → 縮放成身體判定的大小
            body.transform.position = headS + Vector3.down * (BodyHeight / 2f + 0.05f);
            body.transform.rotation = Quaternion.identity;
            body.transform.localScale = new Vector3(BodyRadius * 2f, BodyHeight / 2f, BodyRadius * 2f);
        }

        void UpdateVisuals()
        {
            bool cal = WorldFrame.Calibrated;
            markerGizmo.SetActive(cal && mode == Mode.Duo);
            if (cal)
            {
                markerGizmo.transform.SetPositionAndRotation(WorldFrame.Marker.position, WorldFrame.Marker.rotation);
            }
            bool remoteVisible = cal && net.Connected && remoteSeen > 0f;   // 對手追蹤中斷時停在最後位置（變灰）
            PlaceBody(remoteHead, remoteBody, WorldFrame.FromWorld(remoteHeadW), remoteVisible);
            bool stale = !remoteTrackingOk || Time.time - remoteSeen > 1f;
            var rc = stale ? new Color(0.5f, 0.5f, 0.5f, 0.3f) : new Color(0.3f, 0.9f, 1f, 0.35f);
            remoteBody.GetComponent<Renderer>().material.color = rc;
            PlaceBody(dummyHead, dummyBody, WorldFrame.FromWorld(dummyHeadW), cal && practiceDummy && !net.Connected);
        }

        // ================================================================ 介面（IMGUI，不需要場景資產）
        void Log(string s)
        {
            log.Add(s);
            if (log.Count > 5) log.RemoveAt(0);
        }

        void EnsureStyles()
        {
            if (label != null) return;
            int fs = Mathf.RoundToInt(Screen.height / 46f);
            label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
            label.normal.textColor = Color.white;
            button = new GUIStyle(GUI.skin.button) { fontSize = fs };
            field = new GUIStyle(GUI.skin.textField) { fontSize = fs };
            big = new GUIStyle(label) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter };
        }

        static void DrawFrame(Rect r, Color c, float t)
        {
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            EnsureStyles();
            float W = Screen.width, H = Screen.height, pad = W * 0.03f, lineH = label.fontSize * 1.6f;
            var safe = Screen.safeArea;
            float top = H - safe.yMax + pad;

            if (hitFlash > 0.01f) { GUI.color = new Color(1f, 0f, 0.05f, hitFlash * 0.45f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }

            // 一開始：選模式
            if (mode == Mode.Choose)
            {
                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, H * 0.18f, W, H * 0.1f), "SpellDuel", big);
                float bw0 = W * 0.8f, bh0 = H * 0.09f;
                if (GUI.Button(new Rect((W - bw0) / 2, H * 0.35f, bw0, bh0), "單人練習（畫場地）", button)) ChooseMode(Mode.Solo);
                GUI.Label(new Rect((W - bw0) / 2, H * 0.35f + bh0, bw0, lineH * 2), "像 Meta Quest 一樣在地上畫出遊戲範圍，不需要標記圖", label);
                if (GUI.Button(new Rect((W - bw0) / 2, H * 0.55f, bw0, bh0), "雙人對戰（標記圖）", button)) ChooseMode(Mode.Duo);
                GUI.Label(new Rect((W - bw0) / 2, H * 0.55f + bh0, bw0, lineH * 2), "兩支手機掃描地上同一張標記圖，共用房間座標", label);
                return;
            }

            // 單人：場地完成後交給 SoloBattle 畫介面
            if (mode == Mode.Solo && solo.phase != SoloBattle.Phase.Hidden)
            {
                solo.DrawGUI();
                if (playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                    GUI.Label(new Rect(0, H * 0.62f, W, H * 0.08f), "⚠ 回到場地內", big);
                return;
            }

            GUI.color = new Color(0, 0, 0, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, W, H * 0.32f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = top;
            void Line(string s) { GUI.Label(new Rect(pad, y, W - pad * 2, lineH), s, label); y += lineH; }
            float bw = (W - pad * 5) / 4f, bh = lineH * 1.5f;

            Line($"AR：{ARSession.state}　{arNote}");
            if (mode == Mode.Solo)
            {
                string st = playArea.state switch
                {
                    PlayArea.State.Scanning => "場地：🔍 尋找地板中",
                    PlayArea.State.Ready => "場地：地板已找到，請建立場地",
                    PlayArea.State.Drawing => "場地：✏️ 手繪中",
                    PlayArea.State.Done => $"場地：✅ 完成　離邊界 {playArea.DistanceToEdge(cam.transform.position):F1}m",
                    _ => "",
                };
                Line(st);
                Line(playArea.Hint);
                Line($"HP 我 {hp}");
                y += pad * 0.5f;
                bool canBuild = playArea.HasFloor && playArea.state != PlayArea.State.Drawing;
                GUI.enabled = canBuild;
                if (GUI.Button(new Rect(pad, y, bw, bh), "方形 2.5m", button)) { playArea.Clear(); if (playArea.AutoSquare(2.5f)) OnPlayAreaReady(); }
                if (GUI.Button(new Rect(pad * 2 + bw, y, bw, bh), "方形 3.5m", button)) { playArea.Clear(); if (playArea.AutoSquare(3.5f)) OnPlayAreaReady(); }
                if (GUI.Button(new Rect(pad * 3 + bw * 2, y, bw, bh), "手繪場地", button)) { WorldFrame.Reset(); playArea.StartDraw(); }
                GUI.enabled = true;
                if (GUI.Button(new Rect(pad * 4 + bw * 3, y, bw, bh), "換模式", button)) { mode = Mode.Choose; playArea.Clear(); WorldFrame.Reset(); }
                y += bh + pad * 0.5f;
                if (GUI.Button(new Rect(pad, y, bw * 1.3f, bh), "HP 重置", button)) { hp = MaxHp; }
            }
            else
            {
                Line(WorldFrame.Calibrated
                    ? $"座標：✅ 已對齊（{Time.time - WorldFrame.LastSeenTime:F0} 秒前看到標記圖）"
                    : "座標：⚠ 請把鏡頭對準地上的標記圖");
                Line($"連線：{net.Status}" + (net.Connected && !net.IsHost ? $"　時鐘差 {clockOffset * 1000:F0}ms（來回 {bestRtt * 1000:F0}ms）" : ""));
                string dist = "";
                if (WorldFrame.Calibrated && net.Connected && remoteSeen > 0f)
                {
                    var me = WorldFrame.ToWorld(cam.transform.position);
                    dist = $"　距離對手 {Vector3.Distance(new Vector3(me.x, 0, me.z), new Vector3(remoteHeadW.x, 0, remoteHeadW.z)):F2}m";
                }
                Line($"HP 我 {hp}　對手 {remoteHp}{dist}");

                y += pad * 0.5f;
                if (!net.Connected)
                {
                    if (GUI.Button(new Rect(pad, y, bw, bh), "建立房間", button)) net.Host();
                    ipInput = GUI.TextField(new Rect(pad * 2 + bw, y, bw * 1.4f, bh), ipInput, field);
                    if (GUI.Button(new Rect(pad * 3 + bw * 2.4f, y, bw * 0.8f, bh), "加入", button))
                    {
                        PlayerPrefs.SetString("sd_last_ip", ipInput);
                        net.Join(ipInput);
                    }
                    if (GUI.Button(new Rect(pad * 4 + bw * 3.2f, y, bw * 0.8f, bh), practiceDummy ? "假人:開" : "假人:關", button))
                        practiceDummy = !practiceDummy;
                }
                y += bh + pad * 0.5f;
                if (GUI.Button(new Rect(pad, y, bw * 1.3f, bh), "重新對齊", button))
                {
                    WorldFrame.Reset();
                    Log("請再掃描一次標記圖");
                }
                if (GUI.Button(new Rect(pad * 2 + bw * 1.3f, y, bw * 1.3f, bh), "HP 重置", button)) { hp = MaxHp; }
                if (!net.Connected && GUI.Button(new Rect(pad * 3 + bw * 2.6f, y, bw * 1.3f, bh), "換模式", button)) { mode = Mode.Choose; WorldFrame.Reset(); }
            }

            // AR 追蹤中斷提示
            if (!Tracking.Ok && ARSession.state != ARSessionState.Unsupported)
            {
                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(new Rect(0, H * 0.36f, W, H * 0.16f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(pad, H * 0.37f, W - pad * 2, lineH * 1.4f), mode == Mode.Duo ? "⏸ AR 追蹤中斷：暫時不能施法（對手仍以你最後的位置判定）" : "⏸ AR 追蹤中斷", label);
                GUI.Label(new Rect(pad, H * 0.37f + lineH * 1.5f, W - pad * 2, lineH * 2), Tracking.Reason, label);
            }
            if (mode == Mode.Duo && net.Connected && !remoteTrackingOk)
                GUI.Label(new Rect(pad, H * 0.53f, W - pad * 2, lineH), "⚠ 對手 AR 追蹤中斷：以他最後的位置判定", label);

            // 鎖定框：對手在畫面中才能施法
            if (mode == Mode.Duo && HasTarget && Tracking.Ok)
            {
                if (targetOnScreen)
                {
                    DrawFrame(targetRect, new Color(0.3f, 1f, 0.4f, 0.9f), Mathf.Max(3f, W * 0.006f));
                    GUI.Label(new Rect(targetRect.x, targetRect.y - lineH, Mathf.Max(targetRect.width, W * 0.3f), lineH), "🎯 鎖定", label);
                }
                else GUI.Label(new Rect(0, H * 0.58f, W, lineH * 1.5f), "對手不在畫面中，轉向對手才能施法", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            }

            // 準星與訊息（手繪場地時準星就是畫筆）
            GUI.Label(new Rect(W / 2 - 50, H / 2 - 50, 100, 100), mode == Mode.Solo && playArea.state == PlayArea.State.Drawing ? (playArea.PenDown ? "●" : "○") : "＋", big);
            float ly = H - safe.y - lineH * (log.Count + 1) - pad;
            foreach (var s in log) { GUI.Label(new Rect(pad, ly, W - pad * 2, lineH), s, label); ly += lineH; }
            if (hp <= 0) GUI.Label(new Rect(0, H * 0.4f, W, H * 0.1f), "💀 敗北", big);
            else if (net.Connected && remoteHp <= 0) GUI.Label(new Rect(0, H * 0.4f, W, H * 0.1f), "🏆 勝利", big);
            if (mode == Mode.Solo && playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                GUI.Label(new Rect(0, H * 0.55f, W, H * 0.08f), "⚠ 回到場地內", big);
        }
    }
}
