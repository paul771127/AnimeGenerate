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
            LobbyHomeBackground(new Rect(0,0,W,H));
            LobbyDust(new Rect(0,top,W,h),UiKit.Gold);
            LobbyLogo(new Rect(pad,top+h*.012f,W-pad*2,h*.225f),fs);
            DrawCurrentCharacter(W, pad, top, lineH, fsS);   // 左上：現在的角色＋換角色
            UiKit.Text(new Rect(pad,top+h*.238f,W-pad*2,lineH),LobbyCaption("唸出咒語，在真實世界施法對決"),fsS,new Color(.90f,.87f,.81f),TextAnchor.MiddleCenter);

            // 首頁：故事模式、附近對戰、遠距對戰、練習模式、觀戰、設定（其他玩法都在故事模式的村子裡）
            bool newbie = SoloBattle.IntroPending;
            string[] titles = { "📖 故事模式", "👥 附近對戰", "🌐 遠距對戰", "🎯 練習模式", "👀 觀戰", "⚙ 設定" };
            string[] descs =
            {
                newbie ? "新手從這裡開始：序章、教學，然後進入炎之里" : "大地圖、村子、關卡；副本、每日挑戰、討伐、觀戰、無盡塔都在這裡",
                "和身邊的朋友：建立房間、輸入代碼，對戰或合作",
                "和遠方的朋友：把代碼傳給朋友，對方以虛擬角色出現",
                "對電腦對手練習，也可以打木頭人",
                "輸入朋友的房間代碼，從斜上方看比賽（中途也能進）",
                "音效、音樂、震動、環境光",
            };
            Color[] acc = { new Color(.94f,.76f,.43f), new Color(.94f,.56f,.45f), new Color(.72f,.61f,.94f), new Color(.40f,.77f,.85f), new Color(.51f,.73f,.93f), new Color(.73f,.78f,.85f) };
            int[] art = { 0, 1, 2, 4, 6, 5 };
            int n = titles.Length;
            float cw=W-pad*3,cx=pad*1.5f,cy=top+h*.29f;
            float ch=Mathf.Min(h*.12f,(bottom-lineH*1.4f-cy-pad*.7f*(n-1))/(n+.12f));
            float cardY=cy;
            for (int i = 0; i < n; i++)
            {
                float cardH=ch*(i==0?1.12f:1f);
                var r = new Rect(cx, cardY, cw, cardH);
                cardY+=cardH+pad*.7f;
                bool hit = UiKit.Button(r, LobbyCaption(""), fs, newbie && i == 0, acc[i]);
                float artS=cardH*.61f;
                float press=r.Contains(Event.current.mousePosition)&&Input.GetMouseButton(0)?2f:0f;
                LobbyHomeCard(r,acc[i],i==0,press);
                if(press>0||(newbie&&i==0))UiKit.Halo(r,acc[i],1);
                LobbyModeArt(new Rect(r.x+pad*.45f,r.y+cardH*.19f+press,artS,artS),art[i],acc[i]);
                UiKit.Divider(new Rect(r.x+pad*.3f,r.y+cardH*.20f,2,cardH*.60f),acc[i]);
                UiKit.Gem(new Rect(r.xMax-pad*.8f,r.center.y-4,8,8),acc[i]);
                float tx=r.x+cardH*.61f+pad*.8f;
                UiKit.Text(new Rect(tx, r.y + cardH * 0.08f+press, r.xMax - tx - pad*1.2f, cardH * 0.37f), LobbyCaption(titles[i]), Mathf.RoundToInt(fs * 1.12f), i==0?new Color(1f,.94f,.78f):new Color(.96f,.96f,1f), TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(tx, r.y + cardH * 0.48f+press, r.xMax - tx - pad*1.2f, cardH * 0.45f), LobbyCaption(descs[i]), fsS, new Color(.78f,.83f,.91f));
                if (hit)
                {
                    if (i == 0) StartStoryEntry();
                    else if (i == 1) ChooseMode(Mode.Duo);
                    else if (i == 2) ChooseMode(Mode.Duo, false, true);
                    else if (i == 3) { solo.SetEntry(SoloBattle.Entry.Practice); ChooseMode(Mode.Solo); }
                    else if (i == 4) EnterWatch();
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
        // Home-only art: one opaque landscape and two nine-sliced panel textures.
        static Texture2D homeLandscape;
        static GUIStyle homeGlass,homeEdge;
        static Color HomeColor(uint rgb)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f);
        sealed class HomePainter
        {
            internal readonly int width,height;internal readonly Color32[] pixels;
            internal HomePainter(int w,int h){width=w;height=h;pixels=new Color32[w*h];}
            internal void Dot(int x,int y,Color color)
            {
                int i=(height-1-y)*width+x;var old=pixels[i];float a=color.a;
                pixels[i]=new Color32((byte)Mathf.Clamp(old.r*(1-a)+color.r*255*a,0,255),(byte)Mathf.Clamp(old.g*(1-a)+color.g*255*a,0,255),(byte)Mathf.Clamp(old.b*(1-a)+color.b*255*a,0,255),255);
            }
            internal void Poly(Color color,params float[] points)
            {
                float x0=1,x1=0,y0=1,y1=0;
                for(int i=0;i<points.Length;i+=2){x0=Mathf.Min(x0,points[i]);x1=Mathf.Max(x1,points[i]);y0=Mathf.Min(y0,points[i+1]);y1=Mathf.Max(y1,points[i+1]);}
                for(int y=Mathf.Max(0,Mathf.FloorToInt(y0*height));y<Mathf.Min(height,Mathf.CeilToInt(y1*height));y++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt(x0*width));x<Mathf.Min(width,Mathf.CeilToInt(x1*width));x++)
                {
                    float px=(x+.5f)/width,py=(y+.5f)/height;bool inside=false;
                    for(int i=0,j=points.Length-2;i<points.Length;j=i,i+=2)
                        if((points[i+1]>py)!=(points[j+1]>py)&&px<(points[j]-points[i])*(py-points[i+1])/(points[j+1]-points[i+1])+points[i])inside=!inside;
                    if(inside)Dot(x,y,color);
                }
            }
            internal void Oval(float cx,float cy,float rx,float ry,Color color)
            {
                for(int y=Mathf.Max(0,Mathf.FloorToInt((cy-ry)*height));y<Mathf.Min(height,Mathf.CeilToInt((cy+ry)*height));y++)
                for(int x=Mathf.Max(0,Mathf.FloorToInt((cx-rx)*width));x<Mathf.Min(width,Mathf.CeilToInt((cx+rx)*width));x++)
                {float dx=((x+.5f)/width-cx)/rx,dy=((y+.5f)/height-cy)/ry;if(dx*dx+dy*dy<1)Dot(x,y,color);}
            }
            internal void Box(float x,float y,float w,float h,Color c)=>Poly(c,x,y,x+w,y,x+w,y+h,x,y+h);
            internal Texture2D Finish(string name)
            {
                var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
            }
        }
        static void LobbyHomeArtInit()
        {
            if(homeLandscape!=null)return;
            var p=new HomePainter(540,1170);
            Color night=HomeColor(0x172340),dusk=HomeColor(0x65556f),horizon=HomeColor(0xc58c69),low=HomeColor(0x182b40),gold=HomeColor(0xf1c986);
            for(int y=0;y<p.height;y++)for(int x=0;x<p.width;x++)
            {
                float u=(x+.5f)/p.width,v=(y+.5f)/p.height;
                Color c=v<.20f?Color.Lerp(night,dusk,v/.20f):v<.34f?Color.Lerp(dusk,horizon,(v-.20f)/.14f):Color.Lerp(horizon,low,Mathf.Clamp01((v-.34f)/.28f));
                float dx=(u-.72f)/.40f,dy=(v-.28f)/.15f,light=Mathf.Exp(-3*(dx*dx+dy*dy))*.40f;
                c=Color.Lerp(c,gold,light);c=Color.Lerp(c,night,Mathf.Clamp01(Mathf.Abs(u-.5f)*.5f));p.Dot(x,y,c);
            }
            // Five chapter silhouettes, with clear, deliberately broad cel shapes.
            p.Oval(.79f,.105f,.027f,.0125f,HomeColor(0xf2ddae));p.Oval(.801f,.100f,.025f,.012f,HomeColor(0x2a3350));
            for(int i=0;i<32;i++){float x=(i*.618034f)%1,y=.014f+((i*.317f)%.15f);p.Oval(x,y,.0009f+i%2*.0004f,.00045f+i%2*.0002f,new Color(.88f,.87f,1,.20f+i%3*.10f));}
            p.Poly(HomeColor(0x55536b),-.04f,.37f,.04f,.276f,.11f,.30f,.19f,.213f,.27f,.279f,.35f,.24f,.45f,.36f,.61f,.31f,.72f,.36f,.84f,.27f,.95f,.30f,1.04f,.26f,1.04f,.48f,-.04f,.48f);
            // Split volcanic summit and a single ember-coloured face.
            p.Poly(HomeColor(0x3d435e),-.05f,.39f,.105f,.255f,.143f,.268f,.183f,.251f,.36f,.403f,.39f,.47f,-.05f,.47f);
            p.Poly(HomeColor(0x9d6a69),.143f,.268f,.183f,.251f,.252f,.342f,.199f,.317f,.182f,.293f,.154f,.285f);
            p.Poly(new Color(.89f,.66f,.48f,.26f),.120f,.242f,.149f,.222f,.177f,.227f,.203f,.213f,.256f,.223f,.215f,.234f,.176f,.235f,.148f,.250f);
            // Sea and a line of gold reflected between the headlands.
            p.Poly(HomeColor(0x526374),.40f,.344f,.77f,.331f,1.02f,.356f,1.02f,.45f,.40f,.45f);
            for(int i=0;i<8;i++)p.Box(.56f-i*.013f,.35f+i*.009f,.18f+i*.016f,.0010f,new Color(.95f,.77f,.55f,.36f-i*.025f));
            // Loess mesas and the distant red sandstone amphitheatre.
            p.Poly(HomeColor(0x746070),.77f,.345f,.82f,.291f,.94f,.291f,.97f,.319f,1.06f,.319f,1.06f,.47f,.77f,.47f);
            p.Poly(HomeColor(0xa07775),.82f,.291f,.94f,.291f,.954f,.305f,.83f,.305f);
            p.Poly(HomeColor(0x51465f),.63f,.355f,.67f,.333f,.83f,.333f,.87f,.355f,.85f,.406f,.65f,.406f);
            p.Box(.67f,.329f,.16f,.006f,HomeColor(0xaf8380));p.Box(.651f,.357f,.20f,.005f,HomeColor(0x98747c));
            for(int i=0;i<8;i++){float x=.672f+i*.021f;p.Oval(x,.352f,.006f,.009f,HomeColor(0x28384e));p.Box(x-.006f,.352f,.012f,.016f,HomeColor(0x28384e));}
            // Layered forest against the mountains.
            for(int i=0;i<19;i++)
            {
                float x=-.02f+i*.030f,y=.345f+Mathf.Sin(i*.78f)*.016f,treeHeight=.025f+(i%4)*.007f;
                p.Poly(HomeColor(0x33485b),x,y-treeHeight,x-.019f,y,x-.012f,y,x-.025f,y+.018f,x+.025f,y+.018f,x+.012f,y,x+.019f,y);
            }
            p.Poly(HomeColor(0x263b50),0,.41f,.10f,.386f,.22f,.407f,.38f,.389f,.54f,.418f,.73f,.397f,1,.433f,1,1,0,1);
            // The village bell tower: roof, timber belfry, hanging bell and warm windows.
            Color tower=HomeColor(0x25334b),roof=HomeColor(0x34344c),rim=HomeColor(0xba9271);
            p.Poly(tower,.073f,.290f,.156f,.290f,.169f,.488f,.060f,.488f);
            p.Box(.076f,.294f,.011f,.074f,rim);p.Box(.142f,.294f,.011f,.074f,rim);
            p.Box(.087f,.300f,.055f,.063f,HomeColor(0x141f36));
            p.Box(.112f,.297f,.005f,.026f,rim);p.Oval(.114f,.334f,.015f,.014f,gold);p.Poly(gold,.099f,.333f,.129f,.333f,.136f,.351f,.092f,.351f);p.Box(.090f,.351f,.048f,.004f,rim);
            p.Poly(roof,.040f,.297f,.115f,.255f,.189f,.297f);p.Poly(rim,.051f,.296f,.115f,.261f,.124f,.267f,.064f,.299f);
            p.Box(.055f,.299f,.120f,.008f,HomeColor(0x18253c));p.Box(.065f,.369f,.101f,.008f,rim);
            for(int i=0;i<3;i++)p.Box(.095f,.392f+i*.028f,.032f,.011f,new Color(.96f,.76f,.47f,.85f));
            p.Poly(HomeColor(0x23334a),-.04f,.50f,.09f,.448f,.21f,.50f,.34f,.458f,.47f,.512f,.65f,.46f,.82f,.525f,1.04f,.47f,1.04f,.72f,-.04f,.72f);
            // Readability veil is baked once, darkest behind the lower six menu rows.
            for(int y=0;y<p.height;y++)for(int x=0;x<p.width;x++)
            {
                float v=(y+.5f)/p.height,a=Mathf.Clamp01((v-.265f)/.53f)*.68f;
                p.Dot(x,y,new Color(.035f,.060f,.11f,a));
            }
            homeLandscape=p.Finish("Home dusk landscape / opaque cached");
            const int w=192,h=96;var glass=new Color32[w*h];var frame=new Color32[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float bevel=Mathf.Min(Mathf.Min(x,w-1-x),Mathf.Min(y,h-1-y));bool cut=(x<9&&y<9&&x+y<9)||(x>w-10&&y<9&&(w-1-x)+y<9)||(x<9&&y>h-10&&x+(h-1-y)<9)||(x>w-10&&y>h-10&&(w-1-x)+(h-1-y)<9);
                if(cut)continue;int i=y*w+x;float t=y/(float)(h-1);Color c=Color.Lerp(HomeColor(0x121e35),HomeColor(0x26324e),t);c.a=.91f;glass[i]=c;
                float alpha=bevel<1.5f?.84f:bevel<3?.12f:bevel>5&&bevel<6?.16f:0;frame[i]=new Color(1,1,1,alpha);
            }
            Texture2D panel=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Home glass nine slice",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};panel.SetPixels32(glass);panel.Apply(false,true);
            Texture2D trim=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Home metallic edge nine slice",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};trim.SetPixels32(frame);trim.Apply(false,true);
            homeGlass=new GUIStyle{border=new RectOffset(12,12,12,12)};homeGlass.normal.background=panel;
            homeEdge=new GUIStyle{border=new RectOffset(12,12,12,12)};homeEdge.normal.background=trim;
        }
        static void LobbyHomeBackground(Rect r)
        {
            LobbyHomeArtInit();var prior=GUI.color;GUI.color=Color.white;
            GUI.DrawTexture(r,homeLandscape,ScaleMode.StretchToFill,false);GUI.color=prior;
        }
        static void LobbyHomeCard(Rect r,Color accent,bool primary,float press)
        {
            if(Event.current.type!=EventType.Repaint)return;
            var prior=GUI.color;GUI.color=Color.white;
            // Repaint the exact landscape crop over UiKit's opaque button skin, while
            // keeping its original hit testing and sound. The glass below is home-only.
            GUI.DrawTextureWithTexCoords(r,homeLandscape,new Rect(r.x/Screen.width,1-r.yMax/Screen.height,r.width/Screen.width,r.height/Screen.height),false);
            var face=new Rect(r.x,r.y+press,r.width,r.height-2);homeGlass.Draw(face,false,false,false,false);
            GUI.color=new Color(accent.r,accent.g,accent.b,primary?1f:.72f);homeEdge.Draw(face,false,false,false,false);GUI.color=prior;
            float corner=Mathf.Min(r.height*.15f,12),inset=4;Color metal=Color.Lerp(accent,UiKit.Gold,.40f);metal.a=primary?.95f:.65f;
            for(int side=-1;side<=1;side+=2)for(int row=-1;row<=1;row+=2)
            {
                var at=new Vector2(side<0?r.x+inset:r.xMax-inset,row<0?face.y+inset:face.yMax-inset);
                LobbyStroke(at+new Vector2(-side*corner,0),at,1,metal);LobbyStroke(at,at+new Vector2(0,-row*corner),1,metal);
            }
            UiKit.Divider(new Rect(r.x+12,face.y+1,r.width-24,1),new Color(accent.r,accent.g,accent.b,primary?.26f:.10f));
        }
        static void LobbyDust(Rect r,Color c)
        {
            float unit=r.width/540f;
            // The background itself is cached; only these tiny glyphs move.
            for(int i=0;i<22;i++)
            {
                float phase=(i*.137f+Time.time*(.004f+i%3*.0008f))%1f;
                float x=r.x+r.width*(.05f+.90f*((i*.381966f)%1f))+Mathf.Sin(Time.time*.17f+i*2.3f)*r.width*.016f;
                float y=r.y+r.height*(1-phase),size=(1.4f+i%3)*unit;
                float alpha=(.20f+.16f*Mathf.Sin(Time.time*.65f+i))*Mathf.Sin(phase*Mathf.PI);
                UiKit.Gem(new Rect(x-size*.5f,y,size,size),new Color(c.r,c.g,c.b,alpha));
            }
            var ring=new Vector2(r.x+r.width*.79f,r.y+r.height*.18f);
            LobbyRing(ring,r.width*.32f,new Color(.93f,.76f,.47f,.075f),Mathf.Max(1,unit*.55f),56,Time.time*.012f);
            LobbyRing(ring,r.width*.27f,new Color(.63f,.74f,.95f,.060f),Mathf.Max(1,unit*.45f),48,-Time.time*.009f);
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f+Time.time*.012f;var at=ring+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r.width*.32f;
                UiKit.Gem(new Rect(at.x-2*unit,at.y-2*unit,4*unit,4*unit),new Color(.96f,.81f,.54f,.14f));
            }
        }
        static void LobbyLogo(Rect r,int fs)
        {
            float radius=Mathf.Min(r.width*.105f,r.height*.24f);
            var center=new Vector2(r.center.x,r.y+radius*1.20f);
            LobbyRing(center,radius,new Color(.95f,.78f,.47f,.72f),Mathf.Max(1,fs*.025f),48,Time.time*.022f);
            LobbyRing(center,radius*.78f,new Color(.77f,.82f,1,.30f),Mathf.Max(1,fs*.021f),36,-Time.time*.018f);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI*.5f;var at=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                UiKit.Gem(new Rect(at.x-3,at.y-3,6,6),UiKit.Gold);
            }
            float blade=radius*.67f;
            LobbyStroke(center+new Vector2(-blade*.45f,blade*.5f),center+new Vector2(blade*.45f,-blade*.6f),fs*.085f,UiKit.Paper);
            LobbyStroke(center+new Vector2(blade*.45f,blade*.5f),center+new Vector2(-blade*.45f,-blade*.6f),fs*.085f,UiKit.Gold);
            UiKit.Star(new Rect(center.x-radius*.25f,center.y-radius*.25f,radius*.5f,radius*.5f),UiKit.Gold);
            var title=new Rect(r.x,r.y+r.height*.53f,r.width,r.height*.40f);
            UiKit.Text(new Rect(title.x,title.y+fs*.065f,title.width,title.height),LobbyCaption("SpellDuel"),Mathf.RoundToInt(fs*2.75f),new Color(.25f,.18f,.25f),TextAnchor.MiddleCenter,true);
            UiKit.Text(title,LobbyCaption("SpellDuel"),Mathf.RoundToInt(fs*2.75f),new Color(1f,.94f,.78f),TextAnchor.MiddleCenter,true);
            float lineW=r.width*.55f;
            UiKit.Divider(new Rect(r.center.x-lineW*.5f,title.yMax+3,lineW,1),new Color(.94f,.76f,.43f,.45f));
            UiKit.Gem(new Rect(r.center.x-4,title.yMax,8,8),UiKit.Gold);
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
                UiKit.PracticeArt(r,c);
            }
            else if(kind==5)
            {
                UiKit.SettingsArt(r,c);
            }
            else if(kind==6)
            {
                // 觀戰：眼睛（外框、虹膜、亮點）＋下方的看台
                LobbyRing(center+new Vector2(0,-u*.05f),u*.26f,c,Mathf.Max(1,u*.035f),28);
                UiKit.Gem(new Rect(center.x-u*.12f,center.y-u*.17f,u*.24f,u*.24f),c);
                UiKit.Gem(new Rect(center.x-u*.05f,center.y-u*.10f,u*.08f,u*.08f),UiKit.Paper);
                for(int t=0;t<3;t++)LobbyStroke(new Vector2(r.x+u*(.22f+t*.04f),r.y+u*(.70f+t*.07f)),new Vector2(r.x+u*(.78f-t*.04f),r.y+u*(.70f+t*.07f)),u*.03f,t==0?UiKit.Gold:UiKit.Paper);
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
