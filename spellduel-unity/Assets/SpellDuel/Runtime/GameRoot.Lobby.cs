using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace SpellDuel
{
    /// <summary>
    /// 首頁（選模式）與開戰前的連線／場地畫面。
    ///   首頁：三張大卡片（單人／同一個地方／不同地方），標記圖模式收在下面的小按鈕。
    ///   連線畫面：上方標題＋「步驟清單」（✅ 完成、▶ 現在這一步、○ 還沒），現在這一步的按鈕放在清單下面；
    ///             除錯資訊（AR 狀態、時鐘、鏡頭偵測…）收在「ℹ 詳細」裡。
    /// 外觀交給美術（見 LOBBY_UI_BRIEF.md）：只改這個檔的畫法；每個按鈕做的事（大括號裡的程式）不能改。
    /// </summary>
    public partial class GameRoot
    {
        bool lobbyDetails;   // 「ℹ 詳細」展開

        enum StepState { Todo, Now, Done }

        // ================================================================ 首頁
        void DrawHome(float W, float H, float pad, float top, float lineH, int fs, int fsS)
        {
            var safe = Screen.safeArea;
            float bottom = H - safe.y - pad, h = bottom - top;
            UiKit.Backdrop(new Rect(0, 0, W, H), UiKit.Gold);
            float logo = Mathf.Min(W * 0.22f, h * 0.11f);
            UiKit.Emblem(new Rect((W - logo) * 0.5f, top + h * 0.02f, logo, logo), 0, UiKit.Gold);
            UiKit.Text(new Rect(pad, top + h * 0.14f, W - pad * 2, h * 0.08f), "SpellDuel", fs * 3, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, true);
            UiKit.Text(new Rect(pad, top + h * 0.22f, W - pad * 2, lineH), "唸出咒語，在真實世界施法對決", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleCenter);

            string[] titles = { "🧍 單人冒險", "👥 和身邊的朋友對戰", "🌐 和遠方的朋友連線" };
            string[] descs =
            {
                "故事模式、無盡模式、Boss 與木頭人練習",
                "建立房間、輸入代碼、手機並排一按就開打",
                "把代碼傳給朋友；對方以虛擬角色出現在你的房間",
            };
            Color[] acc = { new Color(0.4f, 0.85f, 1f), new Color(1f, 0.55f, 0.35f), new Color(0.75f, 0.55f, 1f) };
            int[] emblem = { 0, 1, 3 };
            float cw = W - pad * 4, ch = h * 0.15f, cx = pad * 2, cy = top + h * 0.3f;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(cx, cy + i * (ch + pad), cw, ch);
                bool hit = UiKit.Button(r, "", fs, false, acc[i]);
                UiKit.Emblem(new Rect(r.x + pad * 0.6f, r.y + ch * 0.2f, ch * 0.6f, ch * 0.6f), emblem[i], acc[i]);
                float tx = r.x + ch * 0.6f + pad * 1.2f;
                UiKit.Text(new Rect(tx, r.y + ch * 0.1f, r.xMax - tx - pad, ch * 0.42f), titles[i], Mathf.RoundToInt(fs * 1.25f), Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(tx, r.y + ch * 0.5f, r.xMax - tx - pad, ch * 0.42f), descs[i], fsS, new Color(0.82f, 0.86f, 0.96f));
                if (hit)
                {
                    if (i == 0) ChooseMode(Mode.Solo);
                    else if (i == 1) ChooseMode(Mode.Duo);
                    else ChooseMode(Mode.Duo, false, true);
                }
            }
            // 進階：標記圖模式（同一個 Wi-Fi、掃描地上的標記圖）
            var mr = new Rect(W * 0.2f, cy + 3 * (ch + pad) + pad * 0.5f, W * 0.6f, lineH * 1.4f);
            if (UiKit.Button(mr, "🗺 進階：標記圖模式（同一個 Wi-Fi）", fsS)) ChooseMode(Mode.Duo, true, false);
            UiKit.Text(new Rect(pad, bottom - lineH, W - pad * 2, lineH), "版本 " + BuildInfo, Mathf.RoundToInt(fsS * 0.85f), new Color(0.6f, 0.65f, 0.75f), TextAnchor.MiddleCenter);
        }

        // ================================================================ 連線／場地畫面
        void DrawLobby(float W, float H, float pad, float top, float lineH, int fs, int fsS)
        {
            float y = top;
            float bh = lineH * 1.6f, full = W - pad * 2, half = (full - pad) / 2f;
            var card = new Rect(pad * 0.5f, -pad, W - pad, Mathf.Max(H * 0.3f, panelBottom) + pad);
            UiKit.Card(card, 0.82f, ModeColor, 0.9f);

            // ---- 標題
            UiKit.Title(new Rect(pad, y, full, lineH * 1.5f), ModeTitle, ModeColor, Mathf.RoundToInt(fs * 1.15f));
            y += lineH * 1.7f;

            // ---- 步驟清單＋現在這一步的按鈕
            if (mode == Mode.Solo) LobbySolo(ref y, pad, full, half, bh, lineH, fs, fsS);
            else if (remoteMode) LobbyRemote(ref y, pad, full, half, bh, lineH, fs, fsS);
            else if (duoUseMarker) LobbyMarker(ref y, pad, full, half, bh, lineH, fs, fsS);
            else LobbyArea(ref y, pad, full, half, bh, lineH, fs, fsS);

            // ---- 下方：返回、詳細
            y += pad * 0.3f;
            bool canLeave = mode == Mode.Solo || !net.Connected || remoteMode;
            if (canLeave && UiKit.Button(new Rect(pad, y, half, bh * 0.85f), "← 換模式", fsS)) LeaveLobby();
            if (UiKit.Button(new Rect(canLeave ? pad * 2 + half : pad, y, canLeave ? half : full, bh * 0.85f), lobbyDetails ? "ℹ 收起詳細" : "ℹ 詳細", fsS, lobbyDetails)) lobbyDetails = !lobbyDetails;
            y += bh * 0.85f + pad * 0.4f;
            if (lobbyDetails) LobbyDetails(ref y, pad, full, half, bh, lineH, fs, fsS);

            if (mode == Mode.Duo && (AreaMode || remoteMode) && playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                UiKit.Pill(H * 0.68f, "⚠ 回到場地內", fs * 2, new Color(1f, 0.4f, 0.4f), W - pad * 2);
            panelBottom = y + pad * 0.5f;
        }

        string ModeTitle => mode == Mode.Solo ? "🧍 單人冒險" : remoteMode ? "🌐 和遠方的朋友連線" : duoUseMarker ? "🗺 標記圖模式" : "👥 和身邊的朋友對戰";
        Color ModeColor => mode == Mode.Solo ? new Color(0.4f, 0.85f, 1f) : remoteMode ? new Color(0.75f, 0.55f, 1f) : duoUseMarker ? new Color(0.6f, 0.9f, 0.45f) : new Color(1f, 0.55f, 0.35f);

        void LeaveLobby()
        {
            if (remoteMode || net is RelayLink) { net.Dispose(); net = new NetLink(); }
            remoteMode = false; solo.RemotePlay = false;
            mode = Mode.Choose; WorldFrame.Reset(); playArea.Clear();
            areaOwner = areaReceived = aligned = quickAligned = false;
        }

        /// <summary>一行步驟：編號／狀態圖示、標題、（現在這一步）提示</summary>
        void Step(ref float y, float pad, float full, float lineH, int fs, int fsS, int n, string title, StepState st, string hint = null)
        {
            var c = st == StepState.Done ? new Color(0.45f, 1f, 0.6f) : st == StepState.Now ? new Color(1f, 0.9f, 0.5f) : new Color(0.6f, 0.65f, 0.75f);
            string mark = st == StepState.Done ? "✅" : st == StepState.Now ? "▶" : "○";
            UiKit.Text(new Rect(pad, y, full, lineH * 1.1f), $"{mark} {n}. {title}", st == StepState.Now ? fs : fsS, c, TextAnchor.MiddleLeft, st == StepState.Now);
            y += lineH * 1.15f;
            if (st == StepState.Now && !string.IsNullOrEmpty(hint))
            {
                UiKit.Text(new Rect(pad * 2.2f, y, full - pad * 1.2f, lineH * 1.6f), hint, fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperLeft);
                y += lineH * 1.7f;
            }
        }

        static StepState S(bool done, bool now) => done ? StepState.Done : now ? StepState.Now : StepState.Todo;

        bool HasPeer => net.Connected && (net.IsHost ? solo.PeerIds.Count > 0 : net.MyPid > 0);

        // ---- 單人
        void LobbySolo(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            bool floor = playArea.HasFloor, area = playArea.state == PlayArea.State.Done;
            Step(ref y, pad, full, lineH, fs, fsS, 1, "找到地板", S(floor, !floor), "面向空曠的地方，慢慢左右移動手機、鏡頭對著地板");
            Step(ref y, pad, full, lineH, fs, fsS, 2, $"建立 {AutoAreaSize:0}m 場地（自動）", S(area, floor && !area), "場地會建在你前方");
            if (floor && UiKit.Button(new Rect(pad, y, full, bh), $"↺ 在我前方重新放置 {AutoAreaSize:0}m 場地", fs, false, new Color(0.4f, 0.85f, 1f), playArea.state != PlayArea.State.Drawing))
            { WorldFrame.Reset(); playArea.Clear(); if (playArea.AutoSquare(AutoAreaSize)) OnPlayAreaReady(); }
            if (floor) y += bh + pad * 0.4f;
        }

        // ---- 房間代碼（Relay）：建立／加入／複製／貼上
        void RoomCodeUI(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS, bool remote)
        {
            var rl = net as RelayLink;
            if (rl == null) return;
            if (rl.IsHost && !string.IsNullOrEmpty(rl.JoinCode))
            {
                UiKit.Text(new Rect(pad, y, full * 0.6f, bh), $"房間代碼  {rl.JoinCode}", Mathf.RoundToInt(fs * 1.5f), new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter, true);
                if (UiKit.Button(new Rect(pad + full * 0.62f, y, full * 0.38f, bh), "📋 複製代碼", fsS, true, new Color(1f, 0.85f, 0.4f)))
                {
                    GUIUtility.systemCopyBuffer = $"來玩 SpellDuel！房間代碼：{rl.JoinCode}";
                    Log("📋 已複製，貼到 LINE 傳給朋友；朋友按「📋 貼上朋友的代碼並加入」");
                }
                y += bh + pad * 0.4f;
            }
            else if (!net.Connected && !rl.Busy)
            {
                if (UiKit.Button(new Rect(pad, y, full, bh), "🏠 建立房間", fs, true, ModeColor)) rl.Host();
                y += bh + pad * 0.4f;
                UiKit.Text(new Rect(pad, y, full, lineH), "或加入朋友的房間：", fsS, new Color(0.85f, 0.88f, 1f));
                y += lineH;
                codeInput = GUI.TextField(new Rect(pad, y, half, bh), codeInput, 8, field).ToUpperInvariant();
                if (UiKit.Button(new Rect(pad * 2 + half, y, half, bh), "加入", fs, true, new Color(0.4f, 0.85f, 1f), codeInput.Trim().Length >= 4))
                {
                    PlayerPrefs.SetString("sd_last_code", codeInput.Trim());
                    rl.Join(codeInput);
                }
                y += bh + pad * 0.4f;
                if (remote && UiKit.Button(new Rect(pad, y, full, bh), "📋 貼上朋友的代碼並加入", fsS, false, new Color(0.4f, 0.85f, 1f)))
                {
                    var code = FindJoinCode(GUIUtility.systemCopyBuffer);
                    if (code == null) Log("剪貼簿裡沒有房間代碼：請先複製朋友傳來的代碼");
                    else { codeInput = code; PlayerPrefs.SetString("sd_last_code", code); rl.Join(code); }
                }
                if (remote) y += bh + pad * 0.4f;
            }
            else
            {
                UiKit.Text(new Rect(pad, y, full, lineH), $"⏳ {net.Status}", fsS, new Color(1f, 0.9f, 0.6f));
                y += lineH * 1.2f;
            }
        }

        // ---- 不同地方（遠端）
        void LobbyRemote(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            bool area = playArea.state == PlayArea.State.Done;
            Step(ref y, pad, full, lineH, fs, fsS, 1, $"找到地板、建立 {AutoAreaSize:0}m 場地（自動）", S(area, !area), "面向房間裡最空的方向，慢慢移動手機、鏡頭對著地板");
            if (area && solo.phase == SoloBattle.Phase.Hidden && UiKit.Button(new Rect(pad, y, full, bh * 0.85f), $"↺ 在我前方重新放置 {AutoAreaSize:0}m 場地", fsS))
            { playArea.Clear(); if (playArea.AutoSquare(AutoAreaSize)) OnPlayAreaReady(); }
            if (area && solo.phase == SoloBattle.Phase.Hidden) y += bh * 0.85f + pad * 0.4f;
            Step(ref y, pad, full, lineH, fs, fsS, 2, HasPeer ? $"已連線（房間 {solo.PeerIds.Count + 1} 人）" : "連線：建立房間，或貼上朋友的代碼", S(HasPeer, !HasPeer),
                net.Connected ? "等朋友加入…把代碼複製給他" : "一人建立房間、把代碼傳給朋友；朋友複製訊息後按「貼上並加入」");
            if (!HasPeer) RoomCodeUI(ref y, pad, full, half, bh, lineH, fs, fsS, true);
            Step(ref y, pad, full, lineH, fs, fsS, 3, "選職業開打（連上會自動打開）", S(false, HasPeer && WorldReady), "開戰那一刻你面向的方向就是對手的方向");
            if (net.Connected && WorldReady && UiKit.Button(new Rect(pad, y, full, bh * 1.1f), "⚔ 選職業與技能", fs, true, ModeColor)) solo.ShowSetup();
            if (net.Connected && WorldReady) y += bh * 1.1f + pad * 0.4f;
        }

        // ---- 同一個地方（畫場地＋並排對齊）
        void LobbyArea(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            bool floor = playArea.HasFloor, conn = HasPeer;
            bool area = areaOwner || (areaReceived && aligned);
            Step(ref y, pad, full, lineH, fs, fsS, 1, "找到地板", S(floor, !floor), "慢慢左右移動手機、鏡頭對著地板");
            Step(ref y, pad, full, lineH, fs, fsS, 2, conn ? $"已連線（房間 {solo.PeerIds.Count + 1} 人）" : "連線：一人建立房間，另一人輸入代碼", S(conn, floor && !conn),
                net.Connected ? "請朋友在他的手機輸入上面的代碼按「加入」" : "朋友就在旁邊：看建立房間那支手機上的代碼，輸入後按「加入」");
            if (!conn) RoomCodeUI(ref y, pad, full, half, bh, lineH, fs, fsS, false);
            Step(ref y, pad, full, lineH, fs, fsS, 3, "📱 並排對齊（兩人各按一次）", S(quickAligned, conn && !quickAligned), "兩人站在一起，手機側邊貼緊並排、鏡頭朝同一個方向，各按一次");
            if (conn || quickAligned)
            {
                if (UiKit.Button(new Rect(pad, y, full, bh * 1.1f), quickAligned ? "📱 已對齊（站回一起可以再按一次）" : "📱 並排對齊", fs, !quickAligned, new Color(0.45f, 1f, 0.6f), Tracking.Ok)) QuickAlign();
                y += bh * 1.1f + pad * 0.4f;
            }
            Step(ref y, pad, full, lineH, fs, fsS, 4, net.IsHost ? $"{AutoAreaSize:0}m 場地（自動建在你前方）" : $"收到房主的 {AutoAreaSize:0}m 場地", S(area, quickAligned && !area),
                net.IsHost ? "面向空地；要換位置就按下面的「重新放置」" : "等房主的場地傳過來");
            if (net.IsHost && floor && !areaReceived && UiKit.Button(new Rect(pad, y, full, bh * 0.85f), $"↺ 在我前方重新放置 {AutoAreaSize:0}m 場地", fsS, false, null, playArea.state != PlayArea.State.Drawing))
            { playArea.Clear(); if (playArea.AutoSquare(AutoAreaSize)) OnPlayAreaReady(); }
            if (net.IsHost && floor && !areaReceived) y += bh * 0.85f + pad * 0.4f;
            Step(ref y, pad, full, lineH, fs, fsS, 5, "選職業開打（對齊好會自動打開）", S(false, conn && WorldReady && area), "分開站好，相距 2～5 公尺");
            if (net.Connected && WorldReady && UiKit.Button(new Rect(pad, y, full, bh * 1.1f), "⚔ 選職業與技能", fs, true, ModeColor)) solo.ShowSetup();
            if (net.Connected && WorldReady) y += bh * 1.1f + pad * 0.4f;
        }

        // ---- 標記圖模式（區域網路 IP）
        void LobbyMarker(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            bool conn = net.Connected;
            Step(ref y, pad, full, lineH, fs, fsS, 1, conn ? "已連線" : "連線（同一個 Wi-Fi）", S(conn, !conn), "一人建立房間（畫面會顯示 IP），另一人輸入那個 IP 按「加入」");
            if (!conn)
            {
                if (UiKit.Button(new Rect(pad, y, full, bh), "🏠 建立房間", fs, true, ModeColor)) ((NetLink)net).Host();
                y += bh + pad * 0.4f;
                ipInput = GUI.TextField(new Rect(pad, y, half, bh), ipInput, field);
                if (UiKit.Button(new Rect(pad * 2 + half, y, half, bh), "加入", fs, true, new Color(0.4f, 0.85f, 1f)))
                {
                    PlayerPrefs.SetString("sd_last_ip", ipInput);
                    ((NetLink)net).Join(ipInput);
                }
                y += bh + pad * 0.4f;
                UiKit.Text(new Rect(pad, y, full, lineH), net.Status, fsS, new Color(1f, 0.9f, 0.6f));
                y += lineH * 1.2f;
            }
            bool cal = WorldFrame.Calibrated;
            Step(ref y, pad, full, lineH, fs, fsS, 2, cal ? "已對齊標記圖" : "掃描地上的標記圖", S(cal, conn && !cal), "兩支手機都把鏡頭對準地上同一張標記圖");
            if (cal && UiKit.Button(new Rect(pad, y, full, bh * 0.85f), "重新掃描標記圖", fsS)) { WorldFrame.Reset(); Log("請再掃描一次標記圖"); }
            if (cal) y += bh * 0.85f + pad * 0.4f;
            Step(ref y, pad, full, lineH, fs, fsS, 3, "選職業開打", S(false, conn && WorldReady));
            if (conn && WorldReady && UiKit.Button(new Rect(pad, y, full, bh * 1.1f), "⚔ 選職業與技能", fs, true, ModeColor)) solo.ShowSetup();
            if (conn && WorldReady) y += bh * 1.1f + pad * 0.4f;
        }

        // ---- 詳細（除錯資訊與進階按鈕）
        void LobbyDetails(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            float yy = y;
            void Line(string s) { UiKit.Text(new Rect(pad, yy, full, lineH), s, fsS, new Color(0.85f, 0.88f, 1f)); yy += lineH; }
            Line($"AR：{ARSession.state}　{arNote}");
            if (playArea.HasFloor && playArea.state == PlayArea.State.Done) Line($"場地：離邊界 {playArea.DistanceToEdge(cam.transform.position):F1}m");
            if (mode == Mode.Duo)
            {
                Line($"連線：{net.Status}" + (net.Connected && !net.IsHost ? $"　時鐘差 {clockOffset * 1000:F0}ms（來回 {bestRtt * 1000:F0}ms）" : ""));
                if (net.Connected) Line($"👥 房間 {solo.PeerIds.Count + 1} 人（最多 {NetLink.MaxClients + 1} 人；我是{(net.IsHost ? "房主" : $" {net.MyPid} 號")}）" + (solo.PeerIds.Count > 1 ? "　多人只能玩合作" : ""));
                if (areaOwner && net.Connected) Line(remoteAligned ? $"對手已對齊（誤差 {remoteAlignRms * 100f:F0}cm）" : "對手對齊中…");
                if (areaReceived) Line(quickAligned ? (quickRefined > 0 ? $"對齊：並排＋鏡頭微調（誤差約 {align.Rms * 100f:F0}cm）" : "對齊：並排對齊")
                    : aligned ? $"對齊：鏡頭 誤差 {align.Rms * 100f:F0}cm（樣本 {align.Used}）" : $"鏡頭對齊：{align.Problem}");
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
                    if (net.Connected && remoteSeen > 0f && obsFoot) obsInfo += $"　與回報差 {Vector3.Distance(Flat(obsGround), Flat(remoteHeadW)) * 100f:F0}cm";
                }
                Line($"鏡頭偵測：{poseDet.Status}　{obsInfo}" + (hasTarget ? $"　目標：{tgtSrc}" : ""));
            }
            else Line($"HP 我 {hp}");
            yy += pad * 0.3f;
            float bw = (full - pad * 2) / 3f;
            if (UiKit.Button(new Rect(pad, yy, bw, bh * 0.8f), "HP 重置", fsS)) hp = MaxHp;
            if (mode == Mode.Duo && UiKit.Button(new Rect(pad * 2 + bw, yy, bw, bh * 0.8f), showPoseDebug ? "骨架:顯示" : "骨架:隱藏", fsS, showPoseDebug)) showPoseDebug = !showPoseDebug;
            if (mode == Mode.Duo && duoUseMarker && !net.Connected && UiKit.Button(new Rect(pad * 3 + bw * 2, yy, bw, bh * 0.8f), practiceDummy ? "假人:開" : "假人:關", fsS, practiceDummy)) practiceDummy = !practiceDummy;
            if (Aligner && UiKit.Button(new Rect(pad * 3 + bw * 2, yy, bw, bh * 0.8f), "鏡頭重新對齊", fsS))
            { align.Clear(); aligned = false; quickAligned = false; Log("重新對齊：按「📱 並排對齊」，或兩人面對面讓鏡頭拍到對方全身"); }
            yy += bh * 0.8f + pad * 0.4f;
            y = yy;
        }
    }
}
