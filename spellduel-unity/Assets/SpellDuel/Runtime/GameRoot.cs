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
    ///   5. 攻擊方鏡頭偵測畫面中的人（PoseDetector），和對手回報的位置融合；
    ///      鏡頭真的看到對手才能鎖定、施法。點螢幕發射法術：送出「起點、方向、速度、半徑、發射時間」，
    ///      兩邊用同一公式模擬，位置必定一致；由「攻擊方」用融合後的對手位置判定是否命中，
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
        // 雙人（畫場地）：其中一人畫場地，另一人收到場地資料，兩支手機用鏡頭互相看對方來對齊座標
        // 雙人（標記圖）：兩支手機掃同一張標記圖，共用座標
        enum Mode { Choose, Solo, Duo }
        Mode mode = Mode.Choose;
        bool duoUseMarker;                 // 雙人：true＝標記圖對齊；false＝畫場地＋互相校正
        bool remoteMode;                   // 遠端連線：不同地方的人（Unity Relay），每人畫自己的場地，對方是虛擬角色
        bool AreaMode => mode == Mode.Duo && !duoUseMarker && !remoteMode;

        // ---------------------------------------------------------------- 遠端連線：座位
        // 開戰前：每個人的場地中心都對到世界原點；第 i 個人（依 pid 排序，共 n 人）的場地轉 360°×i/n。
        // 開戰那一刻重新定座位：不管站在自己房間的哪裡，「開戰時面向的方向」就是朝向對手（多人＝朝向圓心），
        // 第 i 個人站在離圓心 SeatRadius 公尺、角度 360°×i/n 的位置（兩人＝相距 2×SeatRadius 面對面）。
        // （以前用「畫完場地時手機面向的方向」當前方，玩家常常搞反 → 對手出現在背後）
        public const float SeatRadius = 1.5f;
        string seatKey = "";
        SoloBattle.Phase lastSeatPhase = SoloBattle.Phase.Hidden;

        void SeatOf(out int i, out int n)
        {
            var all = new List<int>(solo.PeerIds); int me = Mathf.Max(0, net.MyPid);
            if (!all.Contains(me)) all.Add(me);
            all.Sort();
            i = all.IndexOf(me); n = all.Count;
        }

        void UpdateSeat()
        {
            var ph = solo.phase; bool started = ph == SoloBattle.Phase.Fighting && lastSeatPhase != SoloBattle.Phase.Fighting;
            lastSeatPhase = ph;
            if (remoteMode && started && Tracking.Ok && playArea.state == PlayArea.State.Done) { AnchorSeat(); return; }
            if (!remoteMode || playArea.state != PlayArea.State.Done || ph == SoloBattle.Phase.Fighting || ph == SoloBattle.Phase.Over) return;   // 結算畫面維持開戰時的座位
            SeatOf(out int i, out int n);
            string key = $"{i}/{n}";
            if (key == seatKey && WorldFrame.Calibrated) return;
            seatKey = key;
            float angle = 360f * i / Mathf.Max(1, n);
            var o = playArea.Origin;
            WorldFrame.SetMarker(new Pose(o.position, o.rotation * Quaternion.Euler(0f, angle, 0f)));
            if (n > 1) Log($"🪑 座位 {i + 1}/{n}：開戰時面向的方向就是對手的方向");
        }

        /// <summary>開戰：以我現在的位置與面向，把我放到座位上（世界座標）</summary>
        void AnchorSeat()
        {
            SeatOf(out int i, out int n);
            float angle = 360f * i / Mathf.Max(1, n);
            var rotA = Quaternion.Euler(0f, angle, 0f);
            var seatW = rotA * new Vector3(0f, 0f, -SeatRadius);          // 我在世界裡的起點（地上）
            var f = Flat(cam.transform.forward);
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            // 世界的「朝圓心」方向 rotA×(+Z) 要對到我現在面向的方向
            var q = Quaternion.LookRotation(f.normalized, Vector3.up) * Quaternion.Inverse(rotA);
            var feet = new Vector3(cam.transform.position.x, playArea.FloorY, cam.transform.position.z);
            WorldFrame.SetMarker(new Pose(feet - q * seatW, q));
            seatKey = "";   // 打完回到準備畫面時重新套用場地座位
            if (n > 1) Log(n == 2 ? "🪑 開戰！對手在你面前約 3 公尺" : $"🪑 開戰！你在第 {i + 1}/{n} 個座位，大家圍著中間");
        }

        // ---------------------------------------------------------------- 雙人「畫場地」模式
        bool areaOwner;                    // 場地是我畫的 → 我的座標就是世界座標
        bool areaReceived;                 // 收到對手畫的場地 → 我是對齊方
        readonly List<Vector3> areaWorld = new List<Vector3>();   // 場地邊界（世界座標）
        readonly DuoAlignment align = new DuoAlignment();
        bool aligned;                      // 對齊方：座標已對齊
        float nextSolve, lastAreaRebuild, nextAlignMsg;
        bool remoteAligned; float remoteAlignRms;
        struct Sample { public double t; public Vector3 p; }
        readonly List<Sample> myFeetHist = new List<Sample>();     // 對齊方自己的腳下位置（AR 座標）
        readonly List<Sample> ownerFeetHist = new List<Sample>();  // 場地主回報的腳下位置（世界座標）
        bool Aligner => AreaMode && areaReceived && !areaOwner;

        /// <summary>這支手機的世界座標可以用了嗎（可以送位置、施法）</summary>
        bool WorldReady => mode == Mode.Duo && (duoUseMarker || areaOwner || remoteMode ? WorldFrame.Calibrated : aligned);

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
        ILink net;
        string codeInput = "";
        double clockOffset;          // 共同時間 = 本機時間 + clockOffset（房主為 0）
        double bestRtt = double.MaxValue;
        float nextPing, nextPose;
        string ipInput = "192.168.";

        static string buildInfo;
        static string BuildInfo => buildInfo ??= (Resources.Load<TextAsset>("buildinfo")?.text ?? "（未標記）");

        static double LocalTime => Time.realtimeSinceStartupAsDouble;
        double SharedTime => LocalTime + clockOffset;

        // ---------------------------------------------------------------- 玩家
        int hp = MaxHp, remoteHp = MaxHp;
        Vector3 remoteHeadW;  Quaternion remoteRotW = Quaternion.identity;
        float remoteSeen = -999f;    // 最後一次收到對手位置的時間（對手追蹤中斷時停在最後的正確位置）
        // 多人（2～4 人）：房間裡的人、誰畫的場地、每個人的對齊狀態與最後位置
        readonly List<int> roomPids = new List<int>();          // 主機：連著的加入者 pid
        int areaOwnerPid = -1;                                    // 收到的場地是誰畫的（只用他的位置來對齊）
        readonly Dictionary<int, bool> peerAligned = new Dictionary<int, bool>();
        readonly Dictionary<int, Vector3> peerFeetW = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> peerSeen = new Dictionary<int, float>();

        /// <summary>送訊息（帶上我的編號）</summary>
        void Send(Msg m) { if (!net.Connected) return; m.pid = net.MyPid; net.Send(m.ToJson()); }

        /// <summary>主機：告訴大家房間裡有誰（含主機 0）</summary>
        void BroadcastPeers()
        {
            var ks = new List<string> { "0" };
            foreach (var p in roomPids) ks.Add(p.ToString());
            Send(new Msg { t = "peers", ks = ks.ToArray() });
            SetSoloPeers(ks.ToArray());
        }

        void SetSoloPeers(string[] ks)
        {
            solo.PeerIds.Clear();
            if (ks != null) foreach (var k in ks) if (int.TryParse(k, out var pid) && pid != net.MyPid && !solo.PeerIds.Contains(pid)) solo.PeerIds.Add(pid);
            solo.PeerIds.Sort();
        }
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
        bool hasTarget;           // 這一幀有沒有可攻擊的目標
        Vector3 tgtHeadW, tgtFwdW; string tgtSrc = "";   // 目標的判定位置（世界座標）與來源

        // ---------------------------------------------------------------- 鏡頭偵測對手
        const float AnkleHeight = 0.08f;   // 腳踝離地高度
        const float MinConf = 0.3f;        // 關鍵點可信度門檻
        PoseDetector poseDet;
        readonly HandGesture hand = new HandGesture();   // 自己的手勢（握拳→張開＝放招）
        ARCameraBackground cameraBackground;
        bool showPoseDebug = true;
        float lastObsTime = -1f;
        // 最近一次鏡頭看到的人（世界座標）
        bool obsSeen, obsFoot;             // 有看到人／有看到腳（可以算出站的位置）
        float obsTime = -999f, obsHeadY;
        Vector3 obsOrigin, obsBearing, obsGround;   // 拍攝時我的位置、往對手的水平方向、對手站的位置
        GameObject obsMarker;

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
            codeInput = PlayerPrefs.GetString("sd_last_code", "");
            Log("請選擇：單人或雙人");
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
            cameraBackground = camGo.AddComponent<ARCameraBackground>();
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

            poseDet = new PoseDetector();
            poseDet.Init(cam, cameraBackground);

            solo = gameObject.AddComponent<SoloBattle>();
            solo.Init(cam, playArea, mat);
            solo.Hand = hand;
            // 雙人職業對戰：SoloBattle 的連線模式（訊息經由這裡收送）
            solo.NetSend = m => { if (net.Connected) Send(m); };
            solo.SharedClock = () => SharedTime;
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
            if (mode != Mode.Duo || !duoUseMarker || img.trackingState != TrackingState.Tracking) return;
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
            if (mode == Mode.Solo || AreaMode || remoteMode) UpdatePlayArea();
            HandleNetwork();
            UpdateSeat();
            if (AreaMode) UpdateDuoArea();
            // 追蹤狀態改變時告訴對手（對手畫面上我的判定框變灰，停在最後的正確位置）
            if (net.Connected && Tracking.Ok != lastSentTracking)
            {
                lastSentTracking = Tracking.Ok;
                Send(new Msg { t = "track", ok = Tracking.Ok });
                Log(Tracking.Ok ? "AR 追蹤恢復" : "⚠ AR 追蹤中斷：暫時不能施法，對手以你最後的位置判定");
            }
            if (net.Connected && !net.IsHost && Time.time >= nextPing)
            {
                nextPing = Time.time + 2f;
                Send(new Msg { t = "ping", c = LocalTime });
            }
            if (net.Connected && WorldReady && Tracking.Ok && Time.time >= nextPose)
            {
                nextPose = Time.time + 0.05f;
                Send(new Msg
                {
                    t = "pose",
                    p = WorldFrame.ToWorld(cam.transform.position),
                    r = WorldFrame.RotToWorld(cam.transform.rotation),
                    hp = hp,
                    t0 = SharedTime,
                });
            }
            UpdateObservation();
            hand.Update(poseDet.Latest, Time.time, Screen.width, Screen.height);
            UpdateTarget();
            if (mode == Mode.Duo)
            {
                // 提供對手位置與鎖定狀態給雙人職業對戰
                solo.NetHasTarget = hasTarget && net.Connected && !DummyActive;
                solo.NetEnemyHeadW = tgtHeadW; solo.NetEnemyFwdW = tgtFwdW;
                solo.NetLock = targetOnScreen; solo.NetLockRect = targetRect;
                solo.NetIsHost = net.IsHost;
                solo.MyPid = Mathf.Max(0, net.MyPid);
            }
            HandleFire();
            UpdateShots();
            UpdateVisuals();
        }

        void LateUpdate()
        {
            // 人體偵測只在雙人模式需要（單人的敵人是虛擬的）；手勢在雙人、單人戰鬥中都要
            bool soloFight = mode == Mode.Solo && solo.phase == SoloBattle.Phase.Fighting;
            poseDet.WantBody = mode == Mode.Duo && !remoteMode;   // 遠端：對方不在鏡頭裡，不用偵測人體
            poseDet.WantHand = mode == Mode.Duo || soloFight;
            poseDet.Enabled = Tracking.Ok && (soloFight || (mode == Mode.Duo && WorldFrame.Calibrated));
            poseDet.Tick();
        }

        // ---------------------------------------------------------------- 模式切換與單人場地
        void ChooseMode(Mode m, bool marker = false, bool remote = false)
        {
            mode = m;
            duoUseMarker = marker;
            solo.NetMode = m == Mode.Duo;
            // 連線方式：遠端＝Unity Relay（房間代碼）；其他＝區域網路（IP）
            if ((remote && !(net is RelayLink)) || (!remote && !(net is NetLink)))
            {
                net?.Dispose();
                net = remote ? (ILink)new RelayLink() : new NetLink();
            }
            remoteMode = m == Mode.Duo && remote;
            solo.RemotePlay = remoteMode;
            seatKey = "";
            if (remoteMode)
            {
                WorldFrame.Reset();
                playArea.Clear();
                playArea.Begin();
                Log("遠端連線：先畫自己的場地，再建立房間或輸入房間代碼加入");
                return;
            }
            if (m == Mode.Solo)
            {
                WorldFrame.Reset();
                playArea.Begin();
                practiceDummy = false;
                Log("單人模式：先掃地板，再畫遊戲場地");
            }
            else if (!marker)
            {
                WorldFrame.Reset();
                playArea.Clear();
                playArea.Begin();
                areaOwner = areaReceived = aligned = remoteAligned = false;
                areaWorld.Clear();
                align.Clear();
                Log("雙人（畫場地）：先連線，再由其中一人畫場地");
            }
            else
            {
                if (!imageTrackingStarted) { imageTrackingStarted = true; StartCoroutine(SetupImageTracking()); }
                Log("雙人模式：把鏡頭對準地上的標記圖來對齊座標");
            }
        }

        float panelBottom;   // 上方操作面板的下緣（GUI 座標），面板以下才是遊戲區
        bool PointerInPlayZone => Screen.height - Input.mousePosition.y >= Mathf.Max(Screen.height * 0.32f, panelBottom);

        void UpdatePlayArea()
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
            if (remoteMode) { seatKey = ""; UpdateSeat(); Log(net.Connected ? "✅ 場地完成" : "✅ 場地完成，接著建立房間或輸入代碼加入"); return; }
            // 場地中心＝世界原點
            if (AreaMode)
            {
                if (areaReceived) return;   // 對齊方不自己畫場地
                // 第一次畫：場地中心＝世界原點。之後重畫只換邊界、世界座標不動（否則對手的對齊會失效）
                if (!areaOwner) { areaOwner = true; WorldFrame.SetMarker(playArea.Origin); }
                SendArea();
                Log(net.Connected ? "✅ 場地完成，已傳給對手" : "✅ 場地完成，連線後會自動傳給對手");
                return;
            }
            WorldFrame.SetMarker(playArea.Origin);
            solo.ShowSetup();
            Log("✅ 場地完成！選職業開始戰鬥");
        }

        // ---------------------------------------------------------------- 雙人「畫場地」：傳送場地、互相校正
        void SendArea()
        {
            if (!net.Connected || !areaOwner || playArea.Polygon.Count < 3) return;
            var pts = new Vector3[playArea.Polygon.Count];
            for (int i = 0; i < pts.Length; i++) { var w = WorldFrame.ToWorld(playArea.Polygon[i]); pts[i] = new Vector3(w.x, 0f, w.z); }
            Send(new Msg { t = "area", pts = pts });
        }

        void OnAreaReceived(Vector3[] pts)
        {
            if (pts == null || pts.Length < 3) return;
            if (areaOwner)
            {
                // 雙方都畫了：以房主的場地為準
                if (net.IsHost) { SendArea(); Log("雙方都畫了場地：以房主的為準"); return; }
                areaOwner = false;
                WorldFrame.Reset();
                align.Clear(); aligned = false;
            }
            bool first = !areaReceived;
            areaReceived = true;
            areaWorld.Clear(); areaWorld.AddRange(pts);
            if (aligned) RebuildArea();
            else playArea.Clear();
            if (first) Log("📥 收到對手的場地：兩人面對面、相距 2～5m，讓鏡頭拍到對方全身（含腳）來對齊");
        }

        /// <summary>把世界座標的場地換到我的 AR 座標，重畫邊界與格子牆</summary>
        void RebuildArea()
        {
            var poly = new List<Vector3>();
            foreach (var w in areaWorld) poly.Add(WorldFrame.FromWorld(new Vector3(w.x, 0f, w.z)));
            playArea.SetPolygon(poly, WorldFrame.Marker);
            lastAreaRebuild = Time.time;
        }

        static Vector3 FeetFromHead(Vector3 head, Quaternion rot, float floorY)
        {
            // 手機拿在身體前方 → 往後退一點才是身體（腳）的位置
            var back = Flat(rot * Vector3.forward);
            back = back.sqrMagnitude > 1e-4f ? -back.normalized * 0.12f : Vector3.zero;
            var f = head + back;
            return new Vector3(f.x, floorY, f.z);
        }

        static void Push(List<Sample> hist, double t, Vector3 p)
        {
            hist.Add(new Sample { t = t, p = p });
            while (hist.Count > 0 && t - hist[0].t > 10.0) hist.RemoveAt(0);
        }

        /// <summary>在時間 t 的位置（內插）；離最近的樣本超過 0.3 秒就不採用</summary>
        static bool Interp(List<Sample> hist, double t, out Vector3 p)
        {
            p = Vector3.zero;
            for (int i = hist.Count - 1; i >= 0; i--)
            {
                if (hist[i].t > t) continue;
                if (i == hist.Count - 1) { if (t - hist[i].t > 0.3) return false; p = hist[i].p; return true; }
                var a = hist[i]; var b = hist[i + 1];
                if (b.t - a.t > 0.6) return false;
                p = Vector3.Lerp(a.p, b.p, (float)((t - a.t) / System.Math.Max(1e-6, b.t - a.t)));
                return true;
            }
            return false;
        }

        void UpdateDuoArea()
        {
            if (!Aligner) return;
            // 對齊前：暫用「自己的 AR 座標、地板高度＝0」當世界座標，鏡頭偵測才算得出對手站的位置
            if (!aligned && playArea.HasFloor)
                WorldFrame.SetMarker(new Pose(new Vector3(0f, playArea.FloorY, 0f), Quaternion.identity));
            if (Tracking.Ok && playArea.HasFloor)
                Push(myFeetHist, SharedTime, FeetFromHead(cam.transform.position, cam.transform.rotation, playArea.FloorY));

            if (Time.time < nextSolve || !playArea.HasFloor) return;
            nextSolve = Time.time + 0.5f;
            if (!align.Solve(Time.time)) return;
            var target = align.MarkerPose(playArea.FloorY);
            if (!aligned)
            {
                WorldFrame.SetMarker(target);
                aligned = true;
                RebuildArea();
                Log($"✅ 座標已對齊（誤差 {align.Rms * 100f:F0}cm），開打後會持續互相校正");
            }
            else
            {
                // 開打後持續校正：慢慢靠過去，避免畫面跳動
                var cur = WorldFrame.Marker;
                float dPos = Vector3.Distance(cur.position, target.position), dAng = Quaternion.Angle(cur.rotation, target.rotation);
                WorldFrame.SetMarker(new Pose(Vector3.Lerp(cur.position, target.position, 0.3f), Quaternion.Slerp(cur.rotation, target.rotation, 0.3f)));
                if ((dPos > 0.02f || dAng > 0.5f) && Time.time - lastAreaRebuild > 1f) RebuildArea();
            }
            if (Time.time >= nextAlignMsg)
            {
                nextAlignMsg = Time.time + 2f;
                Send(new Msg { t = "align", ok = true, s = align.Rms, id = align.Used });
            }
        }

        /// <summary>鏡頭看到對手的腳（世界座標 ground、拍攝時間 captureTime）→ 送給對方或加入對齊樣本</summary>
        void OnFootObservation(Vector3 ground, float captureTime)
        {
            if (!AreaMode || !net.Connected) return;
            double tShared = SharedTime - (Time.time - captureTime);
            if (areaOwner)
            {
                // 多人：只送給「看到的應該是他」的那位（只有一位還沒對齊 → 給他；否則給位置最接近、已對齊的人）
                int to = SeenTarget(ground);
                if (solo.PeerIds.Count <= 1) Send(new Msg { t = "seen", p = new Vector3(ground.x, 0f, ground.z), t0 = tShared });
                else if (to >= 0) Send(new Msg { t = "seen", p = new Vector3(ground.x, 0f, ground.z), t0 = tShared, to = to + 1 });
            }
            else if (Aligner && Interp(ownerFeetHist, tShared, out var ownerW))
            {
                var local = WorldFrame.FromWorld(ground);   // 我的 AR 座標（xz 就是本地座標）
                align.Add(ownerW, new Vector3(local.x, 0f, local.z), Time.time, DuoAlignment.KindISeeOwner);
            }
        }

        /// <summary>場地主看到某人的腳：判斷是誰（−1＝分不出來）</summary>
        int SeenTarget(Vector3 ground)
        {
            int unaligned = -1, nUn = 0;
            foreach (var pid in solo.PeerIds) if (!peerAligned.TryGetValue(pid, out var ok) || !ok) { unaligned = pid; nUn++; }
            if (nUn == 1) return unaligned;
            int best = -1; float bd = 0.8f;
            foreach (var pid in solo.PeerIds)
            {
                if (!peerAligned.TryGetValue(pid, out var ok) || !ok) continue;
                if (!peerFeetW.TryGetValue(pid, out var f) || Time.time - peerSeen[pid] > 0.5f) continue;
                float d = Vector2.Distance(new Vector2(f.x, f.z), new Vector2(ground.x, ground.z));
                if (d < bd) { bd = d; best = pid; }
            }
            return best;
        }

        // ---------------------------------------------------------------- 網路訊息
        void HandleNetwork()
        {
            while (net.TryReceive(out var line))
            {
                Msg m;
                try { m = Msg.FromJson(line); } catch { continue; }
                if (m.to != 0 && m.to != net.MyPid + 1) continue;   // 指定給別人的（主機收到要轉送的那份）
                switch (m.t)
                {
                    case "_hello":
                        Log($"✅ 加入房間（我是 {net.MyPid} 號）");
                        break;
                    case "peers":
                        SetSoloPeers(m.ks);
                        Log($"👥 房間 {solo.PeerIds.Count + 1} 人");
                        break;
                    case "_open":
                        if (net.IsHost)
                        {
                            if (!roomPids.Contains(m.id)) roomPids.Add(m.id);
                            roomPids.Sort();
                            BroadcastPeers();
                            Log($"✅ 隊友{m.id} 加入（房間 {roomPids.Count + 1} 人）");
                        }
                        else Log("✅ 已連線");
                        hp = remoteHp = MaxHp;
                        damaged.Clear();
                        if (AreaMode) SendArea();
                        if (!net.IsHost) Send(new Msg { t = "ping", c = LocalTime });
                        break;
                    case "_close":
                        if (net.IsHost)
                        {
                            roomPids.Remove(m.id); peerAligned.Remove(m.id); peerFeetW.Remove(m.id); peerSeen.Remove(m.id);
                            BroadcastPeers();
                            solo.OnPeerLeft(m.id);
                            Log($"❌ 隊友{m.id} 斷線（房間剩 {roomPids.Count + 1} 人）");
                            if (roomPids.Count == 0 && mode == Mode.Duo && solo.phase != SoloBattle.Phase.Hidden) solo.Hide();
                        }
                        else
                        {
                            Log("❌ 主機已斷線");
                            solo.PeerIds.Clear();
                            if (mode == Mode.Duo && solo.phase != SoloBattle.Phase.Hidden) solo.Hide();
                        }
                        break;
                    case "ping":
                        // 只回給問的那個人
                        net.SendTo(m.pid, new Msg { t = "pong", c = m.c, h = LocalTime, pid = net.MyPid }.ToJson());
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
                    {
                        if (solo.PeerIds.Count <= 1) { remoteHeadW = m.p; remoteRotW = m.r; remoteHp = m.hp; remoteSeen = Time.time; }
                        if (Aligner && (areaOwnerPid < 0 || m.pid == areaOwnerPid)) Push(ownerFeetHist, m.t0, FeetFromHead(m.p, m.r, 0f));
                        // 合作的隊友位置：手機拿在身體前方 → 往後退一點才是身體中心
                        var bk = Flat(m.r * Vector3.forward);
                        var body = m.p + (bk.sqrMagnitude > 1e-4f ? -bk.normalized * 0.12f : Vector3.zero);
                        if (!solo.NetPeers.TryGetValue(m.pid, out var pp)) solo.NetPeers[m.pid] = pp = new SoloBattle.PeerPose();
                        pp.head = body; pp.fwd = m.r * Vector3.forward;
                        peerFeetW[m.pid] = new Vector3(body.x, 0f, body.z); peerSeen[m.pid] = Time.time;
                        break;
                    }
                    case "area":
                        if (AreaMode) { areaOwnerPid = m.pid; OnAreaReceived(m.pts); }
                        break;
                    case "seen":
                        // 場地主的鏡頭看到我：配上我自己在同一時間的位置 → 一組對齊樣本
                        if (Aligner && Interp(myFeetHist, m.t0, out var mine))
                            align.Add(m.p, new Vector3(mine.x, 0f, mine.z), Time.time, DuoAlignment.KindOwnerSeesMe);
                        break;
                    case "align":
                        remoteAligned = m.ok; remoteAlignRms = m.s; peerAligned[m.pid] = m.ok;
                        break;
                    case "shot":
                        SpawnShot(new Shot { id = m.id, mine = false, o = m.p, d = m.d, s = m.s, rad = m.rad, dmg = m.dmg, t0 = m.t0 });
                        break;
                    case "hit":
                        // 攻擊方判定我被打中 → 扣血、震動、閃紅，回報最新血量
                        if (damaged.Add(m.id))
                        {
                            hp = Mathf.Max(0, hp - m.dmg);
                            Send(new Msg { t = "hp", hp = hp });
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
                    default:
                        solo.OnNet(m);   // 對戰／合作的戰鬥訊息（ready、mode、cast2、hit2、boss、mins…）
                        break;
                }
            }
        }

        // ---------------------------------------------------------------- 發射
        void HandleFire()
        {
            if (solo.phase != SoloBattle.Phase.Hidden) return;   // 職業對戰中：由 SoloBattle 處理手勢與語音
            hand.ReleaseStyle = HandGesture.Style.Burst;          // 測試用法術：握拳→張開
            Vector3 sp;
            if (mode == Mode.Duo && hand.ConsumeRelease()) sp = new Vector3(Screen.width / 2f, Screen.height / 2f);   // 手勢放招：朝畫面中央的準星
            else
            {
                if (!Input.GetMouseButtonDown(0) || GUIUtility.hotControl != 0) return;
                sp = Input.mousePosition;
                if (!PointerInPlayZone) return;   // 上方是操作面板
            }
            if (playArea.state == PlayArea.State.Drawing) return;   // 正在手繪場地
            if (mode != Mode.Duo) return;   // 單人模式的操作由 SoloBattle 處理
            if (mode == Mode.Solo && playArea.state != PlayArea.State.Done) return;   // 畫場地時按螢幕是在畫線
            if (!WorldReady) { Log(duoUseMarker ? "請先掃描標記圖對齊座標" : areaReceived ? "還沒對齊：兩人面對面，讓鏡頭拍到對方全身" : "請先畫場地"); return; }
            if (!Tracking.Ok) { Log("AR 追蹤中斷，暫時不能施法"); return; }
            if (!hasTarget) { Log("還沒有目標：請先連線、開練習假人，或讓鏡頭拍到一個人"); return; }
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
            Send(new Msg { t = "shot", id = shot.id, p = shot.o, d = shot.d, s = shot.s, rad = shot.rad, dmg = shot.dmg, t0 = shot.t0 });
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

        // ---------------------------------------------------------------- 鏡頭偵測 → 世界座標
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>把最新的骨架偵測結果換算成世界座標：腳踝射線與地板的交點＝對手站的位置</summary>
        void UpdateObservation()
        {
            var r = poseDet.Latest;
            if (r == null || r.time <= lastObsTime) return;
            lastObsTime = r.time;
            if (!r.found || !WorldFrame.Calibrated) { obsSeen = false; return; }

            Vector3 O(int i, out Vector3 d)
            {
                var ray = r.RayThrough(i);
                d = WorldFrame.DirToWorld(ray.direction).normalized;
                return WorldFrame.ToWorld(ray.origin);
            }
            bool Ok(int i) => r.conf[i] >= MinConf;

            // 方向：優先用軀幹中心（肩、髖的平均），其次鼻子、腳踝
            var origin = WorldFrame.ToWorld(r.camPos);
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var i in new[] { PoseDetector.LShoulder, PoseDetector.RShoulder, PoseDetector.LHip, PoseDetector.RHip })
                if (Ok(i)) { O(i, out var d); sum += d; n++; }
            if (n == 0 && Ok(PoseDetector.Nose)) { O(PoseDetector.Nose, out var d); sum += d; n++; }
            if (n == 0) foreach (var i in new[] { PoseDetector.LAnkle, PoseDetector.RAnkle }) if (Ok(i)) { O(i, out var d); sum += d; n++; }
            var bearing = Flat(sum);
            if (n == 0 || bearing.sqrMagnitude < 1e-6f) { obsSeen = false; return; }

            // 腳踝：射線和地板（y = 腳踝高度）的交點。腳在畫面最下緣時可能被切掉，不採用
            Vector3 ground = Vector3.zero; int g = 0;
            foreach (var i in new[] { PoseDetector.LAnkle, PoseDetector.RAnkle })
            {
                if (!Ok(i) || r.pt[i].y < 0.03f) continue;
                var o = O(i, out var d);
                if (d.y > -0.03f) continue;   // 射線沒有往下，碰不到地板
                float t = (AnkleHeight - o.y) / d.y;
                if (t < 0.3f || t > 15f) continue;
                ground += o + d * t; g++;
            }

            bool fresh = obsSeen && r.time - obsTime < 0.35f;
            obsSeen = true;
            obsTime = r.time;
            obsOrigin = origin;
            obsBearing = bearing.normalized;
            obsFoot = g > 0;
            if (obsFoot)
            {
                var gNew = ground / g;
                // 輕微平滑，減少偵測抖動
                obsGround = fresh && Vector3.Distance(Flat(obsGround), Flat(gNew)) < 0.6f ? Vector3.Lerp(obsGround, gNew, 0.6f) : gNew;
                obsGround.y = 0f;
                OnFootObservation(new Vector3(gNew.x, 0f, gNew.z), r.time);   // 對齊用原始值（不平滑）
                // 頭的高度：鼻子射線走到對手所在距離時的高度
                obsHeadY = 1.55f;
                if (Ok(PoseDetector.Nose))
                {
                    var o = O(PoseDetector.Nose, out var d);
                    float dist = Flat(obsGround - o).magnitude, dh = Flat(d).magnitude;
                    if (dh > 1e-3f) obsHeadY = Mathf.Clamp((o + d * (dist / dh)).y, 1.0f, 2.0f);
                }
            }
        }

        bool ObsFresh => obsSeen && Time.time - obsTime < 0.4f;

        /// <summary>鏡頭看到的人是不是對手回報的那個位置（畫面裡可能有別人）</summary>
        bool ObsMatches(Vector3 bodyW)
        {
            if (obsFoot) return Vector3.Distance(Flat(obsGround), Flat(bodyW)) < 1.0f;
            var rel = Flat(bodyW - obsOrigin);
            float lateral = Mathf.Abs(Vector3.Dot(rel, new Vector3(obsBearing.z, 0f, -obsBearing.x)));
            return Vector3.Dot(rel, obsBearing) > 0f && lateral < 0.8f;
        }

        /// <summary>
        /// 融合：鏡頭判斷「左右方向」很準（角度），對手回報的位置判斷「前後距離」較穩。
        /// 左右以鏡頭為主；前後距離近時多信鏡頭（腳踝落點準），遠時多信回報。
        /// </summary>
        Vector3 Fuse(Vector3 bodyW)
        {
            var u = obsBearing; var v = new Vector3(u.z, 0f, -u.x);
            var rel = Flat(bodyW - obsOrigin);
            float depthR = Vector3.Dot(rel, u), latR = Vector3.Dot(rel, v);
            float depth = depthR;
            if (obsFoot)
            {
                float depthO = Flat(obsGround - obsOrigin).magnitude;
                depth = Mathf.Lerp(depthR, depthO, Mathf.Clamp(1.1f - 0.2f * depthR, 0.2f, 0.8f));
            }
            var f = Flat(obsOrigin) + u * depth + v * (latR * 0.1f);
            return new Vector3(f.x, bodyW.y, f.z);
        }

        // ---------------------------------------------------------------- 鎖定目標
        bool DummyActive => practiceDummy && !net.Connected;

        /// <summary>
        /// 這一幀要攻擊的目標：假人 → 對手（鏡頭＋回報融合，鏡頭沒看到就只用回報）→ 沒連線時鏡頭看到的真人（練習用）
        /// </summary>
        bool TryGetTarget(out Vector3 headW, out Vector3 fwdW, out string src)
        {
            headW = Vector3.zero; fwdW = Vector3.zero; src = "";
            if (!WorldFrame.Calibrated) return false;
            if (DummyActive) { headW = dummyHeadW; src = "假人"; return true; }
            if (net.Connected && remoteSeen > 0f)
            {
                var fwd = remoteRotW * Vector3.forward;
                var back = Flat(fwd);
                back = back.sqrMagnitude > 1e-4f ? -back.normalized * 0.12f : Vector3.zero;
                var body = remoteHeadW + back;   // 手機拿在身體前方 → 往後退一點才是身體中心
                if (ObsFresh && ObsMatches(body)) { headW = Fuse(body); src = "鏡頭＋回報"; return true; }
                headW = remoteHeadW; fwdW = fwd; src = "回報"; return true;
            }
            if (!net.Connected && ObsFresh && obsFoot)
            {
                headW = new Vector3(obsGround.x, obsHeadY, obsGround.z); src = "鏡頭"; return true;
            }
            return false;
        }

        /// <summary>算出目標，並判斷能不能鎖定：鏡頭真的看到對手（偵測器不能用時退回「位置在畫面內」）</summary>
        void UpdateTarget()
        {
            targetOnScreen = false;
            hasTarget = TryGetTarget(out tgtHeadW, out tgtFwdW, out tgtSrc);
            if (mode != Mode.Duo || !hasTarget || !Tracking.Ok || !WorldReady) return;

            var head = WorldFrame.FromWorld(tgtHeadW);
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
            bool detectorWorks = poseDet.Supported && poseDet.Latest != null;
            if (DummyActive || !detectorWorks || remoteMode) targetOnScreen = inView > 0;   // 遠端：對方是虛擬角色    // 假人是虛擬的；偵測器不能用時退回位置推算
            else targetOnScreen = inView > 0 && tgtSrc != "回報";               // 鏡頭真的看到（且是對手本人）
            if (targetOnScreen)
            {
                var c = head - cam.transform.position;
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
                    // 我是攻擊方：全部用世界座標判定（鏡頭看到的位置＋對手回報的位置），和對手手機朝向哪裡無關。
                    // 對手 AR 追蹤中斷時不再回報位置 → 鏡頭看得到就用鏡頭，否則用他最後的正確位置。
                    if (!hasTarget) { }
                    else if (!net.Connected)
                    {
                        // 練習：假人，或鏡頭看到的真人
                        if (HitsBody(posW, tgtHeadW, tgtFwdW, s.rad)) { Explode(s, new Color(1f, 0.3f, 0.2f), DummyActive ? "命中假人！" : "命中！（鏡頭偵測的真人）"); continue; }
                    }
                    else
                    {
                        if (HitsBody(posW, tgtHeadW, tgtFwdW, s.rad))
                        {
                            Send(new Msg { t = "hit", id = s.id, dmg = s.dmg });
                            remoteHp = Mathf.Max(0, remoteHp - s.dmg);   // 先顯示，對手回報 hp 後再校正
                            Explode(s, new Color(1f, 0.3f, 0.2f), "命中！");
                            if (remoteHp <= 0) Log("🏆 勝利");
                            continue;
                        }
                    }
                    if (age > life)
                    {
                        if (net.Connected) Send(new Msg { t = "miss", id = s.id });
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
            obsMarker = MakePrim(PrimitiveType.Cylinder, new Color(0.2f, 1f, 0.3f, 0.6f));   // 鏡頭偵測到的站位
            obsMarker.transform.localScale = new Vector3(BodyRadius * 2f, 0.005f, BodyRadius * 2f);
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
            markerGizmo.SetActive(cal && mode == Mode.Duo && duoUseMarker);
            if (cal)
            {
                markerGizmo.transform.SetPositionAndRotation(WorldFrame.Marker.position, WorldFrame.Marker.rotation);
            }
            // 對手是真人：不在他身上蓋任何模型（只在介面上用方框框起來）
            bool remoteVisible = false;
            PlaceBody(remoteHead, remoteBody, WorldFrame.FromWorld(remoteHeadW), remoteVisible);
            bool stale = !remoteTrackingOk || Time.time - remoteSeen > 1f;
            var rc = stale ? new Color(0.5f, 0.5f, 0.5f, 0.3f) : new Color(0.3f, 0.9f, 1f, 0.35f);
            remoteBody.GetComponent<Renderer>().material.color = rc;
            PlaceBody(dummyHead, dummyBody, WorldFrame.FromWorld(dummyHeadW), cal && practiceDummy && !net.Connected);
            bool showObs = cal && mode == Mode.Duo && showPoseDebug && ObsFresh && obsFoot;
            obsMarker.SetActive(showObs);
            if (showObs) obsMarker.transform.SetPositionAndRotation(WorldFrame.FromWorld(obsGround + Vector3.up * 0.01f), WorldFrame.RotFromWorld(Quaternion.identity));
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

            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f);
            // 一開始：選模式（三張卡片：按鈕＋說明）
            if (mode == Mode.Choose)
            {
                UiKit.Backdrop(new Rect(0,0,W,H), UiKit.Gold);
                float menuBottom = H-safe.y;
                float menuHeight = menuBottom-top;
                float logoSize=Mathf.Min(W*.20f,menuHeight*.105f);
                UiKit.Emblem(new Rect((W-logoSize)*.5f,top+menuHeight*.025f,logoSize,logoSize),0,UiKit.Gold);
                UiKit.Text(new Rect(pad, top+menuHeight*.14f, W-pad*2, menuHeight*.075f), "SpellDuel", fs * 3, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(pad, top+menuHeight*.215f, W-pad*2, lineH), "版本 " + BuildInfo, fsS, new Color(0.7f, 0.75f, 0.85f), TextAnchor.MiddleCenter);
                float cw = W - pad * 4, ch = menuHeight * 0.145f, cx = pad * 2;
                string[] titles = { "🧍 單人練習（畫場地）", "⚔ 多人（同一個地方・畫場地）", "🗺 多人（同一個地方・標記圖）", "🌐 遠端連線（不同地方）" };
                string[] descs = { "在地上畫出遊戲範圍，對戰 AI 或木頭人，不需要標記圖",
                                   "同一個 Wi-Fi、2～6 人：一人畫場地、其他人自動收到；鏡頭互相看對方來對齊",
                                   "同一個 Wi-Fi：手機掃描地上同一張標記圖，共用房間座標",
                                   "2～6 人、不用同一個 Wi-Fi：每人畫自己的場地，用房間代碼加入；其他人以虛擬角色出現" };
                Color[] acc = { new Color(0.4f, 0.85f, 1f), new Color(1f, 0.55f, 0.35f), new Color(0.6f, 0.9f, 0.45f), new Color(0.75f, 0.55f, 1f) };
                for (int i = 0; i < 4; i++)
                {
                    var r = new Rect(cx, top + menuHeight * 0.28f + i * (ch + pad * 0.7f), cw, ch);
                    bool hit = UiKit.Button(r, "", fs, false, acc[i]);
                    UiKit.Emblem(new Rect(r.x+pad*.6f,r.y+ch*.23f,ch*.48f,ch*.48f),i,acc[i]);
                    UiKit.Text(new Rect(r.x + ch*.61f, r.y + ch * 0.09f, r.width - ch*.61f-pad, ch * 0.35f), titles[i], Mathf.RoundToInt(fs * 1.2f), Color.white, TextAnchor.MiddleLeft, true);
                    UiKit.Text(new Rect(r.x + ch*.61f, r.y + ch * 0.46f, r.width - ch*.61f-pad, ch * 0.45f), descs[i], fsS, new Color(0.8f, 0.85f, 0.95f));
                    if (hit) { if (i == 0) ChooseMode(Mode.Solo); else ChooseMode(Mode.Duo, i == 2, i == 3); }
                }
                return;
            }

            // 雙人職業對戰：交給 SoloBattle 畫介面
            if (mode == Mode.Duo && solo.phase != SoloBattle.Phase.Hidden)
            {
                solo.DrawGUI();
                if (AreaMode && playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                    UiKit.Pill(H * 0.68f, "⚠ 回到場地內", fs * 2, new Color(1f, 0.4f, 0.4f), W - pad * 2);
                return;
            }

            // 單人：場地完成後交給 SoloBattle 畫介面
            if (mode == Mode.Solo && solo.phase != SoloBattle.Phase.Hidden)
            {
                solo.DrawGUI();
                if (playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                    UiKit.Pill(H * 0.68f, "⚠ 回到場地內", fs * 2, new Color(1f, 0.4f, 0.4f), W - pad * 2);
                return;
            }

            UiKit.Card(new Rect(pad * 0.5f, -pad, W - pad, Mathf.Max(H * 0.3f, panelBottom) + pad), 0.6f);

            float y = top;
            void Line(string s) { UiKit.Text(new Rect(pad, y, W - pad * 2, lineH), s, fsS, Color.white); y += lineH; }
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
                // 方形場地：從腳下往前方展開（技能射程最遠 15m，建議 10m 以上的空地或戶外）
                float[] sizes = { 3f, 6f, 10f, 15f };
                for (int i = 0; i < sizes.Length; i++)
                    if (UiKit.Button(new Rect(pad + i * (bw + pad), y, bw, bh), $"方形 {sizes[i]:0}m", fs, sizes[i] >= 10f, new Color(0.4f, 0.85f, 1f), canBuild)) { playArea.Clear(); if (playArea.AutoSquare(sizes[i])) OnPlayAreaReady(); }
                y += bh + pad * 0.5f;
                if (UiKit.Button(new Rect(pad, y, bw, bh), "手繪場地", fs, false, null, canBuild)) { WorldFrame.Reset(); playArea.StartDraw(); }
                if (UiKit.Button(new Rect(pad * 2 + bw, y, bw, bh), "換模式", fs)) { mode = Mode.Choose; playArea.Clear(); WorldFrame.Reset(); }
                if (UiKit.Button(new Rect(pad * 3 + bw * 2, y, bw, bh), "HP 重置", fs)) { hp = MaxHp; }
            }
            else
            {
                if (remoteMode)
                    Line(playArea.state == PlayArea.State.Done ? $"場地：✅ 完成（離邊界 {playArea.DistanceToEdge(cam.transform.position):F1}m）　開戰時面向的方向＝對手的方向" : !playArea.HasFloor ? "場地：🔍 先掃地板（慢慢移動手機）" : "場地：畫出自己的遊戲範圍（建議 6m 以上）");
                else if (duoUseMarker)
                    Line(WorldFrame.Calibrated
                        ? $"座標：✅ 已對齊（{Time.time - WorldFrame.LastSeenTime:F0} 秒前看到標記圖）"
                        : "座標：⚠ 請把鏡頭對準地上的標記圖");
                else if (areaOwner)
                    Line("場地：✅ 你畫的場地（以你的座標為準）" + (!net.Connected ? "" : remoteAligned ? $"　對手已對齊（誤差 {remoteAlignRms * 100f:F0}cm）" : "　對手對齊中…"));
                else if (areaReceived)
                    Line(aligned
                        ? $"對齊：✅ 誤差 {align.Rms * 100f:F0}cm（樣本 {align.Used}，持續互相校正）"
                        : $"對齊中（{align.Problem}）：兩人面對面相距 2～5m，鏡頭拍到對方全身含腳");
                else
                    Line(!playArea.HasFloor ? "場地：🔍 先掃地板（慢慢移動手機）" : "場地：由其中一人畫場地，另一人會自動收到");
                Line($"連線：{net.Status}" + (net.Connected && !net.IsHost ? $"　時鐘差 {clockOffset * 1000:F0}ms（來回 {bestRtt * 1000:F0}ms）" : ""));
                if (net.Connected) Line($"👥 房間 {solo.PeerIds.Count + 1} 人（最多 {NetLink.MaxClients + 1} 人；我是{(net.IsHost ? "房主" : $" {net.MyPid} 號")}）" + (solo.PeerIds.Count > 1 ? (remoteMode ? "　多人只能玩合作" : "　多人只能玩合作；每位加入的人都要先面對房主對齊") : ""));
                string dist = "";
                if (WorldFrame.Calibrated && net.Connected && remoteSeen > 0f)
                {
                    var me = WorldFrame.ToWorld(cam.transform.position);
                    dist = $"　距離對手 {Vector3.Distance(new Vector3(me.x, 0, me.z), new Vector3(remoteHeadW.x, 0, remoteHeadW.z)):F2}m";
                }
                Line($"HP 我 {hp}　對手 {remoteHp}{dist}");
                string obsInfo = "";
                if (ObsFresh)
                {
                    obsInfo = obsFoot ? $"站位 {Flat(obsGround - WorldFrame.ToWorld(cam.transform.position)).magnitude:F2}m" : "看不到腳（只用方向）";
                    if (net.Connected && remoteSeen > 0f && obsFoot)
                        obsInfo += $"　與回報差 {Vector3.Distance(Flat(obsGround), Flat(remoteHeadW)) * 100f:F0}cm";
                }
                Line($"鏡頭偵測：{poseDet.Status}　{obsInfo}" + (hasTarget ? $"　目標：{tgtSrc}" : ""));

                y += pad * 0.5f;
                // 畫場地（還沒收到對手的場地時才能畫；重畫會再傳一次給對手）；遠端：每人畫自己的
                if ((AreaMode && !areaReceived) || (remoteMode && solo.phase == SoloBattle.Phase.Hidden))
                {
                    bool canBuild = playArea.HasFloor && playArea.state != PlayArea.State.Drawing;
                    float[] sizes = { 3f, 6f, 10f, 15f };
                    for (int i = 0; i < sizes.Length; i++)
                        if (UiKit.Button(new Rect(pad + i * (bw + pad), y, bw, bh), $"方形 {sizes[i]:0}m", fs, sizes[i] >= 10f, new Color(0.4f, 0.85f, 1f), canBuild)) { playArea.Clear(); if (playArea.AutoSquare(sizes[i])) OnPlayAreaReady(); }
                    y += bh + pad * 0.5f;
                    if (UiKit.Button(new Rect(pad, y, bw, bh), "手繪場地", fs, false, null, canBuild)) { playArea.StartDraw(); }
                    if (playArea.state == PlayArea.State.Drawing) UiKit.Text(new Rect(pad * 2 + bw, y, W - bw - pad * 3, bh), playArea.Hint, fsS, new Color(1f, 0.9f, 0.6f));
                    y += bh + pad * 0.5f;
                }
                if (remoteMode)
                {
                    var rl = net as RelayLink;
                    if (rl != null && rl.IsHost && !string.IsNullOrEmpty(rl.JoinCode))
                    {
                        UiKit.Text(new Rect(pad, y, W - pad * 2, bh), $"房間代碼  {rl.JoinCode}", Mathf.RoundToInt(fs * 1.6f), new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter, true);
                        y += bh + pad * 0.3f;
                        Line("把代碼告訴其他人（最多 6 人）；大家都進來後按下面的「職業對戰／合作」");
                    }
                    else if (!net.Connected && rl != null && !rl.Busy)
                    {
                        if (UiKit.Button(new Rect(pad, y, bw, bh), "建立房間", fs, true, new Color(0.75f, 0.55f, 1f))) rl.Host();
                        codeInput = GUI.TextField(new Rect(pad * 2 + bw, y, bw * 1.4f, bh), codeInput, 8, field).ToUpperInvariant();
                        if (UiKit.Button(new Rect(pad * 3 + bw * 2.4f, y, bw * 0.8f, bh), "加入", fs, true, new Color(0.4f, 0.85f, 1f), codeInput.Trim().Length >= 4))
                        {
                            PlayerPrefs.SetString("sd_last_code", codeInput.Trim());
                            rl.Join(codeInput);
                        }
                    }
                    y += bh + pad * 0.5f;
                    if (net.Connected && WorldReady && UiKit.Button(new Rect(pad, y, W - pad * 2, bh * 1.2f), "⚔ 職業對戰／🤝 合作（選職業與技能）", Mathf.RoundToInt(fs * 1.1f), true, new Color(0.75f, 0.55f, 1f))) solo.ShowSetup();
                    else if (net.Connected && !WorldReady) Line("⚠ 先畫好自己的場地");
                    y += bh * 1.2f + pad * 0.5f;
                    if (UiKit.Button(new Rect(pad, y, bw * 1.3f, bh), "換模式", fs)) { net.Dispose(); net = new NetLink(); remoteMode = false; solo.RemotePlay = false; mode = Mode.Choose; WorldFrame.Reset(); playArea.Clear(); }
                }
                else if (!net.Connected)
                {
                    if (UiKit.Button(new Rect(pad, y, bw, bh), "建立房間", fs, true, new Color(1f, 0.55f, 0.35f))) ((NetLink)net).Host();
                    ipInput = GUI.TextField(new Rect(pad * 2 + bw, y, bw * 1.4f, bh), ipInput, field);
                    if (UiKit.Button(new Rect(pad * 3 + bw * 2.4f, y, bw * 0.8f, bh), "加入", fs, true, new Color(0.4f, 0.85f, 1f)))
                    {
                        PlayerPrefs.SetString("sd_last_ip", ipInput);
                        ((NetLink)net).Join(ipInput);
                    }
                    if (UiKit.Button(new Rect(pad * 4 + bw * 3.2f, y, bw * 0.8f, bh), practiceDummy ? "假人:開" : "假人:關", fsS, practiceDummy))
                        practiceDummy = !practiceDummy;
                }
                if (!remoteMode) {
                y += bh + pad * 0.5f;
                if ((duoUseMarker || Aligner) && UiKit.Button(new Rect(pad, y, bw * 1.3f, bh), "重新對齊", fs))
                {
                    if (duoUseMarker) { WorldFrame.Reset(); Log("請再掃描一次標記圖"); }
                    else { align.Clear(); aligned = false; Log("重新對齊：兩人面對面，讓鏡頭拍到對方全身"); }
                }
                if (UiKit.Button(new Rect(pad * 2 + bw * 1.3f, y, bw * 1.3f, bh), "HP 重置", fs)) { hp = MaxHp; }
                // 連線、對齊完成後：進入職業對戰（選職業、技能、錄咒語 → 雙方都準備好就開打）
                if (net.Connected && WorldReady)
                {
                    float yb = y + bh + pad * 0.5f;
                    if (UiKit.Button(new Rect(pad, yb, W - pad * 2, bh * 1.2f), "⚔ 職業對戰（選職業與技能）", Mathf.RoundToInt(fs * 1.2f), true, new Color(1f, 0.55f, 0.35f))) solo.ShowSetup();
                }
                if (net.Connected && UiKit.Button(new Rect(pad * 3 + bw * 2.6f, y, bw * 1.3f, bh), showPoseDebug ? "骨架:顯示" : "骨架:隱藏", fsS, showPoseDebug)) showPoseDebug = !showPoseDebug;
                if (!net.Connected && UiKit.Button(new Rect(pad * 3 + bw * 2.6f, y, bw * 1.3f, bh), "換模式", fs)) { mode = Mode.Choose; WorldFrame.Reset(); playArea.Clear(); areaOwner = areaReceived = aligned = false; }
                if (!net.Connected) { y += bh + pad * 0.5f; if (UiKit.Button(new Rect(pad, y, bw * 1.3f, bh), showPoseDebug ? "骨架:顯示" : "骨架:隱藏", fsS, showPoseDebug)) showPoseDebug = !showPoseDebug; }
                }
                if ((AreaMode || remoteMode) && playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                    UiKit.Pill(H * 0.68f, "⚠ 回到場地內", fs * 2, new Color(1f, 0.4f, 0.4f), W - pad * 2);
            }
            panelBottom = y + (mode == Mode.Duo && net.Connected && WorldReady ? lineH * 3.5f : lineH * 1.6f);

            // AR 追蹤中斷提示
            if (!Tracking.Ok && ARSession.state != ARSessionState.Unsupported)
            {
                var fr = new Rect(pad, H * 0.36f, W - pad * 2, H * 0.16f);
                UiKit.Card(fr, 0.85f, new Color(1f, 0.75f, 0.3f), 0.8f);
                UiKit.Text(new Rect(fr.x + pad, fr.y + pad * 0.5f, fr.width - pad * 2, fr.height * 0.45f), mode == Mode.Duo ? "⏸ AR 追蹤中斷：暫時不能施法（對手仍以你最後的位置判定）" : "⏸ AR 追蹤中斷", fs, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(fr.x + pad, fr.y + fr.height * 0.5f, fr.width - pad * 2, fr.height * 0.45f), Tracking.Reason, fsS, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleCenter);
            }
            if (mode == Mode.Duo && net.Connected && !remoteTrackingOk)
                UiKit.Pill(H * 0.55f, "⚠ 對手 AR 追蹤中斷：以他最後的位置判定", fsS, new Color(1f, 0.75f, 0.4f), W - pad * 2);

            // 鎖定框：對手在畫面中才能施法
            if (mode == Mode.Duo && hasTarget && Tracking.Ok)
            {
                if (targetOnScreen)
                {
                    DrawFrame(targetRect, new Color(0.3f, 1f, 0.4f, 0.9f), Mathf.Max(3f, W * 0.006f));
                    var meW = WorldFrame.ToWorld(cam.transform.position);
                    float dTarget = Flat(tgtHeadW - meW).magnitude;
                    UiKit.Text(new Rect(targetRect.x, targetRect.y - lineH, Mathf.Max(targetRect.width, W * 0.4f), lineH), $"🎯 鎖定　{dTarget:F1} m", fs, new Color(0.4f, 1f, 0.5f), TextAnchor.LowerLeft, true);
                }
                else UiKit.Pill(H * 0.58f, $"鏡頭沒看到對手（沒鎖定也能放，朝準星飛；距離 {Flat(tgtHeadW - WorldFrame.ToWorld(cam.transform.position)).magnitude:F1} m）", fs, new Color(1f, 0.45f, 0.45f), W - pad * 2);
            }

            if (mode == Mode.Duo) hand.DrawGUI(label, new Color(1f, 0.6f, 0.1f));

            // 骨架偵測結果（除錯）：關鍵點、送去偵測的畫面縮圖
            if (mode == Mode.Duo && showPoseDebug && poseDet.Supported)
            {
                var pr = poseDet.Latest;
                if (pr != null && pr.found && Time.time - pr.time < 0.5f)
                {
                    float dot = Mathf.Max(8f, W * 0.018f);
                    for (int i = 0; i < PoseDetector.Count; i++)
                    {
                        if (pr.conf[i] < MinConf) continue;
                        GUI.color = i >= PoseDetector.LAnkle ? new Color(0.2f, 1f, 0.3f) : new Color(1f, 0.9f, 0.2f);
                        GUI.DrawTexture(new Rect(pr.pt[i].x * W - dot / 2, (1f - pr.pt[i].y) * H - dot / 2, dot, dot), Texture2D.whiteTexture);
                    }
                    GUI.color = Color.white;
                }
                if (poseDet.Preview != null)
                {
                    float pw = W * 0.22f, ph = pw * poseDet.Preview.height / Mathf.Max(1, poseDet.Preview.width);
                    GUI.DrawTexture(new Rect(W - pw - pad, H * 0.34f, pw, ph), poseDet.Preview, ScaleMode.StretchToFill, false);
                }
            }

            // 準星與訊息（手繪場地時準星就是畫筆）
            GUI.Label(new Rect(W / 2 - 50, H / 2 - 50, 100, 100), mode == Mode.Solo && playArea.state == PlayArea.State.Drawing ? (playArea.PenDown ? "●" : "○") : "＋", big);
            float ly = H - safe.y - lineH * (log.Count + 1) - pad;
            if (log.Count > 0) UiKit.Card(new Rect(pad * 0.5f, ly - pad * 0.3f, W - pad, lineH * log.Count + pad * 0.6f), 0.45f);
            foreach (var s in log) { UiKit.Text(new Rect(pad, ly, W - pad * 2, lineH), s, fsS, new Color(0.9f, 0.93f, 1f)); ly += lineH; }
            if (hp <= 0) GUI.Label(new Rect(0, H * 0.4f, W, H * 0.1f), "💀 敗北", big);
            else if (net.Connected && remoteHp <= 0) GUI.Label(new Rect(0, H * 0.4f, W, H * 0.1f), "🏆 勝利", big);
            if (mode == Mode.Solo && playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                UiKit.Pill(H * 0.6f, "⚠ 回到場地內", fs * 2, new Color(1f, 0.4f, 0.4f), W - pad * 2);
        }
    }
}
