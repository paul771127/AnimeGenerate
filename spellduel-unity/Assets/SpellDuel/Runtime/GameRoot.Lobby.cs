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
        float lobbyMeasuredBottom, lobbyRouteY;
        GUIStyle lobbyInput;
        bool lobbyDetails;   // 「ℹ 詳細」展開

        enum StepState { Todo, Now, Done }

        // ================================================================ 首頁
        /// <summary>首頁「故事模式」（第一次打開遊戲也自動走這裡）：放好場地 → 選職業 → 序章／村子</summary>
        void StartStoryEntry() { solo.SetEntry(SoloBattle.Entry.Story); ChooseMode(Mode.Solo); }

        void DrawHome(float W, float H, float pad, float top, float lineH, int fs, int fsS)
        {
            var safe = Screen.safeArea;
            float bottom = H - safe.y - pad, h = bottom - top;
            UiKit.Backdrop(new Rect(0,0,W,H),UiKit.Gold,.48f);
            LobbyDust(new Rect(0,top,W,h),UiKit.Gold);
            LobbyLogo(new Rect(pad,top+h*.015f,W-pad*2,h*.235f),fs);
            UiKit.Text(new Rect(pad,top+h*.25f,W-pad*2,lineH),LobbyCaption("唸出咒語，在真實世界施法對決"),fsS,UiKit.Paper,TextAnchor.MiddleCenter);

            // 首頁只留：故事模式、附近對戰、遠距對戰、練習模式、設定（其他玩法都在故事模式的村子裡）
            bool newbie = SoloBattle.IntroPending;
            string[] titles = { "📖 故事模式", "👥 附近對戰", "🌐 遠距對戰", "🎯 練習模式", "⚙ 設定" };
            string[] descs =
            {
                newbie ? "新手從這裡開始：序章、教學，然後進入炎之里" : "大地圖、村子、關卡；副本、每日挑戰、討伐、觀戰、無盡塔都在這裡",
                "和身邊的朋友：建立房間、輸入代碼，對戰或合作",
                "和遠方的朋友：把代碼傳給朋友，對方以虛擬角色出現",
                "對電腦對手練習，也可以打木頭人",
                "音效、音樂、震動、環境光",
            };
            Color[] acc = { new Color(1f, 0.8f, 0.4f), new Color(1f, 0.55f, 0.35f), new Color(0.75f, 0.55f, 1f), new Color(0.4f, 0.85f, 1f), new Color(0.7f, 0.75f, 0.85f) };
            int[] art = { 0, 1, 2, 4, 5 };
            int n = titles.Length;
            float cw=W-pad*3,cx=pad*1.5f,cy=top+h*.29f;
            float ch=Mathf.Min(h*.125f,(bottom-lineH*1.4f-cy)/n-pad*.7f);
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(cx, cy + i * (ch + pad * 0.7f), cw, ch);
                bool hit = UiKit.Button(r, LobbyCaption(""), fs, newbie && i == 0, acc[i]);
                float artS=ch*.63f;
                float press=r.Contains(Event.current.mousePosition)&&Input.GetMouseButton(0)?2f:0f;
                if(press>0||(newbie&&i==0))UiKit.Halo(r,acc[i],1);
                LobbyModeArt(new Rect(r.x+pad*.45f,r.y+ch*.18f+press,artS,artS),art[i],acc[i]);
                UiKit.Divider(new Rect(r.x+pad*.3f,r.y+ch*.18f,2,ch*.64f),acc[i]);
                UiKit.Gem(new Rect(r.xMax-pad*.8f,r.center.y-4,8,8),acc[i]);
                float tx=r.x+ch*.63f+pad*.8f;
                UiKit.Text(new Rect(tx, r.y + ch * 0.1f+press, r.xMax - tx - pad*1.2f, ch * 0.38f), LobbyCaption(titles[i]), Mathf.RoundToInt(fs * 1.15f), Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(tx, r.y + ch * 0.49f+press, r.xMax - tx - pad*1.2f, ch * 0.43f), LobbyCaption(descs[i]), fsS, new Color(0.82f, 0.86f, 0.96f));
                if (hit)
                {
                    if (i == 0) StartStoryEntry();
                    else if (i == 1) ChooseMode(Mode.Duo);
                    else if (i == 2) ChooseMode(Mode.Duo, false, true);
                    else if (i == 3) { solo.SetEntry(SoloBattle.Entry.Practice); ChooseMode(Mode.Solo); }
                    else SettingsMenu.Show();
                }
            }
            UiKit.Text(new Rect(pad, bottom - lineH, W - pad * 2, lineH), LobbyCaption("版本 " + BuildInfo), Mathf.RoundToInt(fsS * 0.85f), new Color(0.6f, 0.65f, 0.75f), TextAnchor.MiddleCenter);
        }

        // ================================================================ 連線／場地畫面
        void DrawLobby(float W, float H, float pad, float top, float lineH, int fs, int fsS)
        {
            float y = top;
            float bh = fs*2.25f, full = W - pad * 2, half = (full - pad) / 2f;
            float cardTop=top-pad*.45f;
            var card=new Rect(pad*.5f,cardTop,W-pad,Mathf.Max(lineH*3,lobbyMeasuredBottom-cardTop));
            UiKit.Card(card,.86f,ModeColor,.95f);

            // ---- 標題
            UiKit.Title(new Rect(pad, y, full, lineH * 1.12f), LobbyCaption(ModeTitle), ModeColor, Mathf.RoundToInt(fs * 1.15f));
            y += lineH*1.32f;

            // ---- 步驟清單＋現在這一步的按鈕
            if (mode == Mode.Solo) LobbySolo(ref y, pad, full, half, bh, lineH, fs, fsS);
            else if (remoteMode) LobbyRemote(ref y, pad, full, half, bh, lineH, fs, fsS);
            else if (duoUseMarker) LobbyMarker(ref y, pad, full, half, bh, lineH, fs, fsS);
            else LobbyArea(ref y, pad, full, half, bh, lineH, fs, fsS);

            // ---- 下方：返回、詳細
            y += pad * 0.3f;
            bool canLeave = mode == Mode.Solo || !net.Connected || remoteMode;
            if (canLeave && UiKit.Button(new Rect(pad, y, half, bh * 0.85f), LobbyCaption("← 換模式"), fsS)) LeaveLobby();
            if (UiKit.Button(new Rect(canLeave ? pad * 2 + half : pad, y, canLeave ? half : full, bh * 0.85f), LobbyCaption(lobbyDetails ? "ℹ 收起詳細" : "ℹ 詳細"), fsS, lobbyDetails)) lobbyDetails = !lobbyDetails;
            y += bh * 0.85f + pad * 0.4f;
            if (lobbyDetails) LobbyDetails(ref y, pad, full, half, bh, lineH, fs, fsS);

            if (mode == Mode.Duo && (AreaMode || remoteMode) && playArea.state == PlayArea.State.Done && !playArea.Inside(cam.transform.position))
                UiKit.Pill(H * 0.68f, "⚠ 回到場地內", fs * 2, new Color(1f, 0.4f, 0.4f), W - pad * 2);
            lobbyMeasuredBottom=y+pad*.5f;
            panelBottom=card.yMax;
        }

        string ModeTitle => mode == Mode.Solo ? (solo.entry == SoloBattle.Entry.Story ? "📖 故事模式" : "🎯 練習模式") : remoteMode ? "🌐 和遠方的朋友連線" : duoUseMarker ? "🗺 標記圖模式" : "👥 和身邊的朋友對戰";
        Color ModeColor => mode == Mode.Solo ? new Color(0.4f, 0.85f, 1f) : remoteMode ? new Color(0.75f, 0.55f, 1f) : duoUseMarker ? new Color(0.6f, 0.9f, 0.45f) : new Color(1f, 0.55f, 0.35f);

        void LeaveLobby()
        {
            if (remoteMode || net is RelayLink) { net.Dispose(); net = new NetLink(); }
            remoteMode = false; solo.RemotePlay = false;
            mode = Mode.Choose; WorldFrame.Reset(); playArea.Clear();
            areaOwner = areaReceived = aligned = quickAligned = false;
            watchers.Clear();
        }

        /// <summary>一行步驟：編號／狀態圖示、標題、（現在這一步）提示</summary>
        void Step(ref float y, float pad, float full, float lineH, int fs, int fsS, int n, string title, StepState st, string hint = null)
        {
            float rowH=lineH*.96f,diameter=fs*1.28f;
            float cx=pad+diameter*.55f,cy=y+rowH*.5f;
            var c=st==StepState.Done?new Color(.5f,.92f,.72f):st==StepState.Now?ModeColor:UiKit.Muted;
            if(n>1)LobbyStroke(new Vector2(cx,lobbyRouteY),new Vector2(cx,cy-diameter*.5f),Mathf.Max(2,fs*.055f),st==StepState.Todo?new Color(.35f,.42f,.55f,.5f):c);
            lobbyRouteY=cy+diameter*.5f;
            var medal=new Rect(cx-diameter*.5f,cy-diameter*.5f,diameter,diameter);
            if(st==StepState.Now)
            {
                float pulse=.65f+.25f*Mathf.Sin(Time.time*2.7f);
                UiKit.Card(new Rect(pad+diameter+fs*.2f,y,full-diameter-fs*.2f,rowH),.7f,c,pulse);
                UiKit.Halo(new Rect(medal.x-4,medal.y-4,medal.width+8,medal.height+8),c,pulse);
            }
            UiKit.Badge(medal,null,c,1);
            if(st==StepState.Done)
            {
                LobbyStroke(new Vector2(cx-diameter*.20f,cy),new Vector2(cx-diameter*.04f,cy+diameter*.15f),Mathf.Max(2,fs*.07f),UiKit.Paper);
                LobbyStroke(new Vector2(cx-diameter*.04f,cy+diameter*.15f),new Vector2(cx+diameter*.23f,cy-diameter*.16f),Mathf.Max(2,fs*.07f),UiKit.Paper);
            }
            else UiKit.Text(medal,LobbyCaption(n.ToString()),fsS,c,TextAnchor.MiddleCenter,true);
            float tx=pad+diameter+fs*.38f;
            UiKit.Text(new Rect(tx,y,pad+full-tx-fs*.22f,rowH),LobbyCaption(title),st==StepState.Now?fs:fsS,st==StepState.Todo?UiKit.Muted:UiKit.Paper,TextAnchor.MiddleLeft,st==StepState.Now);
            y+=rowH;
            if(st==StepState.Now&&!string.IsNullOrEmpty(hint))
            {
                float hintH=Mathf.Max(lineH,UiKit.TextHeight(hint,pad+full-tx,fsS)+fs*.18f);
                UiKit.Text(new Rect(tx,y,pad+full-tx,hintH),LobbyCaption(hint),fsS,UiKit.Muted,TextAnchor.UpperLeft);
                LobbyStroke(new Vector2(cx,y),new Vector2(cx,y+hintH),Mathf.Max(2,fs*.055f),new Color(c.r,c.g,c.b,.5f));
                y+=hintH;
            }
            y+=lineH*.13f;
        }

        static StepState S(bool done, bool now) => done ? StepState.Done : now ? StepState.Now : StepState.Todo;

        bool HasPeer => net.Connected && (net.IsHost ? solo.PeerIds.Count > 0 : net.MyPid > 0);

        // ---- 單人
        void LobbySolo(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            bool floor = playArea.HasFloor, area = playArea.state == PlayArea.State.Done;
            Step(ref y, pad, full, lineH, fs, fsS, 1, "找到地板", S(floor, !floor), "面向空曠的地方，慢慢左右移動手機、鏡頭對著地板");
            Step(ref y, pad, full, lineH, fs, fsS, 2, $"建立 {AutoAreaSize:0}m 場地（自動）", S(area, floor && !area), "場地會建在你前方");
            if (floor && UiKit.Button(new Rect(pad, y, full, bh), LobbyCaption($"↺ 在我前方重新放置 {AutoAreaSize:0}m 場地"), fs, false, new Color(0.4f, 0.85f, 1f), playArea.state != PlayArea.State.Drawing))
            BeginAreaPreview(() => { WorldFrame.Reset(); playArea.Clear(); if (playArea.AutoSquare(AutoAreaSize)) OnPlayAreaReady(); });
            if (floor) y += bh + pad * 0.4f;
        }

        // ---- 房間代碼（Relay）：建立／加入／複製／貼上
        void RoomCodeUI(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS, bool remote)
        {
            var rl = net as RelayLink;
            if (rl == null) return;
            if (rl.IsHost && !string.IsNullOrEmpty(rl.JoinCode))
            {
                float codeH=bh*1.35f;
                UiKit.Card(new Rect(pad,y,full,codeH),.95f,UiKit.Gold,.85f);
                UiKit.Text(new Rect(pad+fs*.4f,y+fs*.12f,full*.6f-fs*.6f,fs*.75f),LobbyCaption("房間代碼"),Mathf.RoundToInt(fsS*.82f),UiKit.Muted,TextAnchor.MiddleLeft,true);
                UiKit.Text(new Rect(pad+fs*.3f,y+fs*.8f,full*.6f-fs*.5f,codeH-fs*.85f),LobbyCaption(rl.JoinCode),Mathf.RoundToInt(fs*1.85f),UiKit.Gold,TextAnchor.MiddleCenter,true,.8f);
                if (UiKit.Button(new Rect(pad+full*.64f,y+fs*.2f,full*.34f,codeH-fs*.4f), LobbyCaption("📋 複製代碼"), fsS, true, new Color(1f, 0.85f, 0.4f)))
                {
                    GUIUtility.systemCopyBuffer = $"來玩 SpellDuel！房間代碼：{rl.JoinCode}";
                    Log("📋 已複製，貼到 LINE 傳給朋友；朋友按「📋 貼上朋友的代碼並加入」");
                }
                y+=codeH+pad*.4f;
            }
            else if (!net.Connected && !rl.Busy)
            {
                if (UiKit.Button(new Rect(pad, y, full, bh), LobbyCaption("🏠 建立房間"), fs, true, ModeColor)) rl.Host();
                y += bh + pad * 0.4f;
                UiKit.Text(new Rect(pad, y, full, lineH), LobbyCaption("或加入朋友的房間："), fsS, new Color(0.85f, 0.88f, 1f));
                y += lineH;
                UiKit.Card(new Rect(pad,y,half,bh),.95f,ModeColor,.8f);
                codeInput = GUI.TextField(new Rect(pad+4,y+3,half-8,bh-6), codeInput, 8, LobbyInput(fs)).ToUpperInvariant();
                if (UiKit.Button(new Rect(pad * 2 + half, y, half, bh), LobbyCaption("加入"), fs, true, new Color(0.4f, 0.85f, 1f), codeInput.Trim().Length >= 4))
                {
                    PlayerPrefs.SetString("sd_last_code", codeInput.Trim());
                    rl.Join(codeInput);
                }
                y += bh + pad * 0.4f;
                if (remote && UiKit.Button(new Rect(pad, y, full, bh), LobbyCaption("📋 貼上朋友的代碼並加入"), fsS, false, new Color(0.4f, 0.85f, 1f)))
                {
                    var code = FindJoinCode(GUIUtility.systemCopyBuffer);
                    if (code == null) Log("剪貼簿裡沒有房間代碼：請先複製朋友傳來的代碼");
                    else { codeInput = code; PlayerPrefs.SetString("sd_last_code", code); rl.Join(code); }
                }
                if (remote) y += bh + pad * 0.4f;
            }
            else
            {
                UiKit.Text(new Rect(pad, y, full, lineH), LobbyCaption($"⏳ {net.Status}"), fsS, new Color(1f, 0.9f, 0.6f));
                y += lineH * 1.2f;
            }
        }

        // ---- 不同地方（遠端）
        void LobbyRemote(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            bool area = playArea.state == PlayArea.State.Done;
            Step(ref y, pad, full, lineH, fs, fsS, 1, $"找到地板、建立 {AutoAreaSize:0}m 場地（自動）", S(area, !area), "面向房間裡最空的方向，慢慢移動手機、鏡頭對著地板");
            if (area && solo.phase == SoloBattle.Phase.Hidden && UiKit.Button(new Rect(pad, y, full, bh * 0.85f), LobbyCaption($"↺ 在我前方重新放置 {AutoAreaSize:0}m 場地"), fsS))
            BeginAreaPreview(() => { playArea.Clear(); if (playArea.AutoSquare(AutoAreaSize)) OnPlayAreaReady(); });
            if (area && solo.phase == SoloBattle.Phase.Hidden) y += bh * 0.85f + pad * 0.4f;
            Step(ref y, pad, full, lineH, fs, fsS, 2, HasPeer ? $"已連線（房間 {solo.PeerIds.Count + 1} 人）" : "連線：建立房間，或貼上朋友的代碼", S(HasPeer, !HasPeer),
                net.Connected ? "等朋友加入…把代碼複製給他" : "一人建立房間、把代碼傳給朋友；朋友複製訊息後按「貼上並加入」");
            if (!HasPeer) RoomCodeUI(ref y, pad, full, half, bh, lineH, fs, fsS, true);
            Step(ref y, pad, full, lineH, fs, fsS, 3, "選職業開打（連上會自動打開）", S(false, HasPeer && WorldReady), "開戰那一刻你面向的方向就是對手的方向");
            if (net.Connected && WorldReady && UiKit.Button(new Rect(pad, y, full, bh * 1.1f), LobbyCaption("⚔ 選職業與技能"), fs, true, ModeColor)) solo.ShowSetup();
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
            // 進階：標記圖模式（同一個 Wi-Fi、掃描地上的標記圖）——原本在首頁，現在收在附近對戰這裡
            if (!conn && !net.Connected && UiKit.Button(new Rect(pad, y, full, bh * 0.8f), LobbyCaption("🗺 進階：改用標記圖模式（同一個 Wi-Fi）"), fsS)) { ChooseMode(Mode.Duo, true, false); return; }
            if (!conn && !net.Connected) y += bh * 0.8f + pad * 0.4f;
            Step(ref y, pad, full, lineH, fs, fsS, 3, "📱 並排對齊（兩人各按一次）", S(quickAligned, conn && !quickAligned), "兩人站在一起，手機側邊貼緊並排、鏡頭朝同一個方向，各按一次");
            if (conn || quickAligned)
            {
                if (UiKit.Button(new Rect(pad, y, full, bh * 1.1f), LobbyCaption(quickAligned ? "📱 已對齊（站回一起可以再按一次）" : "📱 並排對齊"), fs, !quickAligned, new Color(0.45f, 1f, 0.6f), Tracking.Ok)) QuickAlign();
                y += bh * 1.1f + pad * 0.4f;
            }
            Step(ref y, pad, full, lineH, fs, fsS, 4, net.IsHost ? $"{AutoAreaSize:0}m 場地（自動建在你前方）" : $"收到房主的 {AutoAreaSize:0}m 場地", S(area, quickAligned && !area),
                net.IsHost ? "面向空地；要換位置就按下面的「重新放置」" : "等房主的場地傳過來");
            if (net.IsHost && floor && !areaReceived && UiKit.Button(new Rect(pad, y, full, bh * 0.85f), LobbyCaption($"↺ 在我前方重新放置 {AutoAreaSize:0}m 場地"), fsS, false, null, playArea.state != PlayArea.State.Drawing))
            BeginAreaPreview(() => { playArea.Clear(); if (playArea.AutoSquare(AutoAreaSize)) OnPlayAreaReady(); });
            if (net.IsHost && floor && !areaReceived) y += bh * 0.85f + pad * 0.4f;
            Step(ref y, pad, full, lineH, fs, fsS, 5, "選職業開打（對齊好會自動打開）", S(false, conn && WorldReady && area), "分開站好，相距 2～5 公尺");
            if (net.Connected && WorldReady && UiKit.Button(new Rect(pad, y, full, bh * 1.1f), LobbyCaption("⚔ 選職業與技能"), fs, true, ModeColor)) solo.ShowSetup();
            if (net.Connected && WorldReady) y += bh * 1.1f + pad * 0.4f;
        }

        // ---- 標記圖模式（區域網路 IP）
        void LobbyMarker(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            bool conn = net.Connected;
            Step(ref y, pad, full, lineH, fs, fsS, 1, conn ? "已連線" : "連線（同一個 Wi-Fi）", S(conn, !conn), "一人建立房間（畫面會顯示 IP），另一人輸入那個 IP 按「加入」");
            if (!conn)
            {
                if (UiKit.Button(new Rect(pad, y, full, bh), LobbyCaption("🏠 建立房間"), fs, true, ModeColor)) ((NetLink)net).Host();
                y += bh + pad * 0.4f;
                UiKit.Card(new Rect(pad,y,half,bh),.95f,ModeColor,.8f);
                ipInput = GUI.TextField(new Rect(pad+4,y+3,half-8,bh-6), ipInput, LobbyInput(fsS));
                if (UiKit.Button(new Rect(pad * 2 + half, y, half, bh), LobbyCaption("加入"), fs, true, new Color(0.4f, 0.85f, 1f)))
                {
                    PlayerPrefs.SetString("sd_last_ip", ipInput);
                    ((NetLink)net).Join(ipInput);
                }
                y += bh + pad * 0.4f;
                UiKit.Text(new Rect(pad, y, full, lineH), LobbyCaption(net.Status), fsS, new Color(1f, 0.9f, 0.6f));
                y += lineH * 1.2f;
            }
            bool cal = WorldFrame.Calibrated;
            Step(ref y, pad, full, lineH, fs, fsS, 2, cal ? "已對齊標記圖" : "掃描地上的標記圖", S(cal, conn && !cal), "兩支手機都把鏡頭對準地上同一張標記圖");
            if (cal && UiKit.Button(new Rect(pad, y, full, bh * 0.85f), LobbyCaption("重新掃描標記圖"), fsS)) { WorldFrame.Reset(); Log("請再掃描一次標記圖"); }
            if (cal) y += bh * 0.85f + pad * 0.4f;
            Step(ref y, pad, full, lineH, fs, fsS, 3, "選職業開打", S(false, conn && WorldReady));
            if (conn && WorldReady && UiKit.Button(new Rect(pad, y, full, bh * 1.1f), LobbyCaption("⚔ 選職業與技能"), fs, true, ModeColor)) solo.ShowSetup();
            if (conn && WorldReady) y += bh * 1.1f + pad * 0.4f;
        }

        // ---- 詳細（除錯資訊與進階按鈕）
        void LobbyDetails(ref float y, float pad, float full, float half, float bh, float lineH, int fs, int fsS)
        {
            float yy=y,debugLine=lineH*.8f;
            UiKit.Divider(new Rect(pad,yy,full,1),new Color(.5f,.57f,.68f,.45f));
            yy+=fs*.25f;
            void Line(string s) { UiKit.Text(new Rect(pad+fs*.25f,yy,full-fs*.5f,debugLine),LobbyCaption(s),Mathf.RoundToInt(fsS*.84f),new Color(.6f,.67f,.77f),TextAnchor.MiddleLeft); yy+=debugLine; }
            Line($"AR：{ARSession.state}　{arNote}");
            if (playArea.HasFloor && playArea.state == PlayArea.State.Done) Line($"場地：離邊界 {playArea.DistanceToEdge(cam.transform.position):F1}m");
            if (mode == Mode.Duo)
            {
                Line($"連線：{net.Status}" + (net.Connected && !net.IsHost ? $"　時鐘差 {clockOffset * 1000:F0}ms（來回 {bestRtt * 1000:F0}ms）" : ""));
                if (net.Connected) Line($"👥 房間 {solo.PeerIds.Count + 1} 人（最多 {NetLink.MaxClients + 1} 人；我是{(net.IsHost ? "房主" : $" {net.MyPid} 號")}）" + (solo.PeerIds.Count > 1 ? "　多人只能玩合作" : "") + (watchers.Count > 0 ? $"　👀 {watchers.Count} 人觀戰" : ""));
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
            if (UiKit.Button(new Rect(pad, yy, bw, bh * 0.8f), LobbyCaption("HP 重置"), fsS)) hp = MaxHp;
            if (mode == Mode.Duo && UiKit.Button(new Rect(pad * 2 + bw, yy, bw, bh * 0.8f), LobbyCaption(showPoseDebug ? "骨架:顯示" : "骨架:隱藏"), fsS, showPoseDebug)) showPoseDebug = !showPoseDebug;
            if (mode == Mode.Duo && duoUseMarker && !net.Connected && UiKit.Button(new Rect(pad * 3 + bw * 2, yy, bw, bh * 0.8f), LobbyCaption(practiceDummy ? "假人:開" : "假人:關"), fsS, practiceDummy)) practiceDummy = !practiceDummy;
            if (Aligner && UiKit.Button(new Rect(pad * 3 + bw * 2, yy, bw, bh * 0.8f), LobbyCaption("鏡頭重新對齊"), fsS))
            { align.Clear(); aligned = false; quickAligned = false; Log("重新對齊：按「📱 並排對齊」，或兩人面對面讓鏡頭拍到對方全身"); }
            yy += bh * 0.8f + pad * 0.4f;
            y = yy;
        }
        // -------------------------- Cached input chrome and procedural lobby illustrations
        static string LobbyCaption(string text)
        {
            if(string.IsNullOrEmpty(text))return text;
            int end=text.IndexOf(' ');
            bool pictogram=char.IsSurrogate(text[0])||text[0]=='ℹ'||text[0]=='↺'||text[0]=='⚔';
            return pictogram&&end>0&&end<=4?text.Substring(end+1):text;
        }
        GUIStyle LobbyInput(int fs)
        {
            if(lobbyInput==null)
            {
                lobbyInput=new GUIStyle(field){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,padding=new RectOffset(8,8,2,2)};
                lobbyInput.normal.background=null;lobbyInput.focused.background=null;lobbyInput.hover.background=null;lobbyInput.active.background=null;
                lobbyInput.normal.textColor=UiKit.Paper;lobbyInput.focused.textColor=UiKit.Paper;lobbyInput.hover.textColor=UiKit.Paper;lobbyInput.active.textColor=UiKit.Paper;
            }
            lobbyInput.fontSize=Mathf.RoundToInt(fs*1.15f);return lobbyInput;
        }
        static void LobbyStroke(Vector2 a,Vector2 b,float width,Color c)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var d=b-a;if(d.magnitude<.1f)return;
            var old=GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);
            UiKit.Divider(new Rect(a.x,a.y-width*.5f,d.magnitude,width),c);
            GUI.matrix=old;
        }
        static void LobbyRing(Vector2 center,float radius,Color c,float width,int count=32,float turn=0)
        {
            for(int k=0;k<count;k++)
            {
                float a=k*Mathf.PI*2/count+turn,b=(k+1)*Mathf.PI*2/count+turn;
                LobbyStroke(center+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius),center+new Vector2(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius),width,c);
            }
        }
        static void LobbyDust(Rect r,Color c)
        {
            for(int i=0;i<14;i++)
            {
                float x=r.x+r.width*(i%2==0?.055f:.945f)+Mathf.Sin(i*2.3f)*r.width*.025f;
                float y=r.y+r.height*((i*.173f+Time.time*.007f)%1f);
                float size=3+i%3*2;
                UiKit.Gem(new Rect(x-size*.5f,y,size,size),new Color(c.r,c.g,c.b,.18f+.10f*Mathf.Sin(Time.time+i)));
            }
        }
        static void LobbyLogo(Rect r,int fs)
        {
            float radius=Mathf.Min(r.width*.14f,r.height*.25f);
            var center=new Vector2(r.center.x,r.y+radius*1.25f);
            LobbyRing(center,radius,new Color(.94f,.76f,.43f,.50f),Mathf.Max(1,fs*.025f),40);
            LobbyRing(center,radius*.80f,new Color(.55f,.73f,1,.24f),Mathf.Max(1,fs*.025f),32);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f+Time.time*.035f;
                UiKit.Gem(new Rect(center.x+Mathf.Cos(a)*radius-3,center.y+Mathf.Sin(a)*radius-3,6,6),UiKit.Gold);
            }
            float blade=radius*.62f;
            LobbyStroke(center+new Vector2(-blade*.45f,blade*.5f),center+new Vector2(blade*.45f,-blade*.6f),fs*.09f,UiKit.Paper);
            LobbyStroke(center+new Vector2(blade*.45f,blade*.5f),center+new Vector2(-blade*.45f,-blade*.6f),fs*.09f,UiKit.Gold);
            UiKit.Star(new Rect(center.x-radius*.25f,center.y-radius*.25f,radius*.5f,radius*.5f),UiKit.Gold);
            var title=new Rect(r.x,r.y+r.height*.57f,r.width,r.height*.36f);
            UiKit.Text(new Rect(title.x,title.y+3,title.width,title.height),LobbyCaption("SpellDuel"),Mathf.RoundToInt(fs*2.75f),new Color(.40f,.29f,.19f),TextAnchor.MiddleCenter,true);
            UiKit.Text(title,LobbyCaption("SpellDuel"),Mathf.RoundToInt(fs*2.75f),UiKit.Paper,TextAnchor.MiddleCenter,true);
            float lineW=r.width*.55f;
            UiKit.Divider(new Rect(r.center.x-lineW*.5f,title.yMax+3,lineW,1),new Color(.94f,.76f,.43f,.6f));
            UiKit.Gem(new Rect(r.center.x-5,title.yMax-1,10,10),UiKit.Gold);
        }
        static void LobbyPhone(Rect r,Color c)
        {
            UiKit.Card(r,.94f,c,.95f);
            UiKit.Divider(new Rect(r.x+r.width*.18f,r.y+r.height*.20f,r.width*.64f,r.height*.54f),new Color(c.r*.4f,c.g*.4f,c.b*.4f,.85f));
            UiKit.Gem(new Rect(r.center.x-r.width*.08f,r.y+r.height*.83f,r.width*.16f,r.width*.16f),UiKit.Gold);
        }
        static void LobbyModeArt(Rect r,int kind,Color c)
        {
            float u=r.width;var center=r.center;
            LobbyRing(center,u*.45f,new Color(c.r,c.g,c.b,.35f),Mathf.Max(1,u*.012f),24);
            if(kind==0)
            {
                // A little robed traveller, an open grimoire and a crystal-tipped staff.
                UiKit.Badge(new Rect(r.x+u*.29f,r.y+u*.18f,u*.25f,u*.25f),null,UiKit.Paper);
                UiKit.Gem(new Rect(r.x+u*.22f,r.y+u*.41f,u*.40f,u*.43f),c);
                LobbyStroke(new Vector2(r.x+u*.72f,r.y+u*.27f),new Vector2(r.x+u*.64f,r.y+u*.84f),u*.035f,UiKit.Gold);
                UiKit.Star(new Rect(r.x+u*.59f,r.y+u*.13f,u*.23f,u*.23f),c);
                UiKit.Divider(new Rect(r.x+u*.18f,r.y+u*.63f,u*.35f,u*.17f),UiKit.Paper);
                LobbyStroke(new Vector2(r.x+u*.355f,r.y+u*.63f),new Vector2(r.x+u*.355f,r.y+u*.80f),u*.013f,c);
            }
            else if(kind==4)
            {
                // 練習：靶心＋木樁
                LobbyRing(center,u*.30f,c,Mathf.Max(1,u*.03f),24);
                LobbyRing(center,u*.17f,UiKit.Paper,Mathf.Max(1,u*.03f),20);
                UiKit.Gem(new Rect(center.x-u*.06f,center.y-u*.06f,u*.12f,u*.12f),UiKit.Gold);
                LobbyStroke(new Vector2(r.x+u*.78f,r.y+u*.22f),new Vector2(center.x+u*.05f,center.y-u*.05f),u*.03f,UiKit.Paper);
            }
            else if(kind==5)
            {
                // 設定：齒輪
                LobbyRing(center,u*.22f,c,Mathf.Max(1,u*.07f),20);
                for(int t=0;t<8;t++){float a=t*Mathf.PI/4f;var d=new Vector2(Mathf.Cos(a),Mathf.Sin(a));LobbyStroke(center+d*u*.24f,center+d*u*.34f,u*.08f,c);}
                UiKit.Gem(new Rect(center.x-u*.07f,center.y-u*.07f,u*.14f,u*.14f),UiKit.Paper);
            }
            else if(kind==3)
            {
                // 副本：三座城塔＋頂上的星（組隊挑戰）
                for(int t=0;t<3;t++){float x=r.x+u*(.25f+t*.25f),top=r.y+u*(t==1?.22f:.36f);LobbyStroke(new Vector2(x,r.y+u*.84f),new Vector2(x,top),u*(t==1?.09f:.07f),t==1?c:UiKit.Paper);}
                LobbyStroke(new Vector2(r.x+u*.18f,r.y+u*.84f),new Vector2(r.x+u*.82f,r.y+u*.84f),u*.04f,UiKit.Gold);
                UiKit.Star(new Rect(center.x-u*.11f,r.y+u*.02f,u*.22f,u*.22f),UiKit.Gold);
            }
            else if(kind==1)
            {
                LobbyPhone(new Rect(r.x+u*.15f,r.y+u*.22f,u*.29f,u*.56f),c);
                LobbyPhone(new Rect(r.x+u*.56f,r.y+u*.22f,u*.29f,u*.56f),c);
                LobbyStroke(new Vector2(r.x+u*.44f,center.y),new Vector2(r.x+u*.56f,center.y),u*.025f,UiKit.Gold);
                UiKit.Star(new Rect(center.x-u*.11f,r.y+u*.06f,u*.22f,u*.22f),UiKit.Gold);
            }
            else
            {
                // Two portals and a dotted constellation between distant rooms.
                var a=new Vector2(r.x+u*.29f,r.y+u*.61f);var b=new Vector2(r.x+u*.70f,r.y+u*.35f);
                LobbyRing(a,u*.19f,c,u*.035f,24);LobbyRing(b,u*.19f,c,u*.035f,24);
                for(int i=0;i<5;i++){float t=(i+1)/6f;var q=a+(b-a)*t;UiKit.Gem(new Rect(q.x-u*.018f,q.y-u*.018f,u*.036f,u*.036f),UiKit.Gold);}
                UiKit.Star(new Rect(b.x-u*.08f,b.y-u*.08f,u*.16f,u*.16f),UiKit.Paper);
                UiKit.Gem(new Rect(a.x-u*.08f,a.y-u*.12f,u*.16f,u*.24f),UiKit.Paper);
            }
        }
    }
}
