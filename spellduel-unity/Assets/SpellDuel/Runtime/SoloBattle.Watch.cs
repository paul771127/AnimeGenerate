using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 觀戰模式（觀戰的那支手機）：用房間代碼加入，但不算玩家人數，只看不打。
    ///   ・房主一開打（或觀眾中途加入時）送「wply」（每位玩家的職業、技能、進階職業）＋「wstart」（模式、副本、難度、場地）。
    ///   ・觀眾照「合作的隊友端」建一場代理戰鬥：Boss、小怪、法術、陷阱、召喚物都由房主／玩家的訊息驅動；
    ///     每位玩家都是「隊友」（用職業角色顯示，位置來自 pose）。對戰（兩人）也一樣：兩位玩家都是隊友，敵人是看不見的空位。
    ///   ・觀眾自己是「隱形的我」：一開始就倒下、放在很遠的地方，不會被當目標、不送任何戰鬥訊息、不拿獎勵。
    ///   ・房主結算時送「wover」（勝負文字），回到準備畫面時送「wend」。
    /// 視角（斜上方、可縮放旋轉滑動）在 GameRoot.Watch.cs。
    /// </summary>
    public partial class SoloBattle
    {
        public bool Watching { get; private set; }
        string watchResult;      // 房主送來的結算文字（null＝沒有）
        int watchMode = -1;      // 正在看的模式：0 對戰、1 合作 Boss、2 無盡、3 副本

        // ================================================================ 觀戰端
        public void BeginWatch()
        {
            EndBattle();
            Watching = true; NetMode = true; NetIsHost = false; RemotePlay = false;
            phase = Phase.Hidden; watchResult = null; watchMode = -1;
            peerInfo.Clear(); NetPeers.Clear(); PeerIds.Clear();
        }

        public void StopWatch()
        {
            EndBattle();
            Watching = false; NetMode = false; coop = false;
            phase = Phase.Hidden; watchResult = null; watchMode = -1;
            peerInfo.Clear(); NetPeers.Clear(); PeerIds.Clear();
        }

        /// <summary>房主離開：結束這一場（留在觀戰模式，可以輸入別的代碼）</summary>
        public void WatchHostLeft()
        {
            EndBattle();
            phase = Phase.Hidden; watchResult = "房主已離開房間"; watchMode = -1;
            PeerIds.Clear();
        }

        void SetPeerInfo(int pid, string cls, string[] loadout, string adv)
        {
            if (!peerInfo.TryGetValue(pid, out var pi)) peerInfo[pid] = pi = new PeerInfo();
            pi.cls = cls; pi.loadout = loadout; pi.adv = string.IsNullOrEmpty(adv) ? null : adv;
        }

        /// <summary>觀戰端的網路訊息（OnNet 開頭轉過來）</summary>
        void OnNetWatch(Msg m)
        {
            switch (m.t)
            {
                case "wply": SetPeerInfo(m.id, m.k, m.ks, m.a); return;
                case "ready": SetPeerInfo(m.pid, m.k, m.ks, m.a); return;
                case "wstart": StartWatchBattle(m); return;
                case "wend": EndBattle(); phase = Phase.Hidden; watchMode = -1; return;
                case "wover":
                    watchResult = m.k;
                    Sfx.Play(string.IsNullOrEmpty(m.k) || m.k.StartsWith("💀") ? "lose" : "win", 0.8f);
                    return;
                // 玩家之間的準備、治療、仇恨、判定：觀眾用不到
                case "mode": case "dstart": case "tbuff": case "oath": case "taunt": case "burn2": case "mhit": case "shotwarn": case "slash":
                    return;
            }
            if (battle == null) return;
            switch (m.t)
            {
                case "slice":
                    // 合作：id 已經是「發射者 pid 空間」的編號；對戰：ok＝斬開的是對手的法術
                    if (watchMode >= 1) battle.ApplyRemoteSlice(m.id, false);
                    else battle.ApplyRemoteSlice(RId(m.ok ? OtherDuelist(m.pid) : m.pid, m.id), false);
                    return;
                case "hit2":
                {
                    // 打中了：把那一發（或陷阱）移除、在命中點爆一下（傷害由玩家自己算，血量看 state／hp2）
                    if (!Skills.All.TryGetValue(m.k ?? "", out var hs)) return;
                    bool trap = m.id < 0; int raw = trap ? -m.id : m.id;
                    if (raw != 0) { if (trap) battle.RemoveRemoteTrap(RId(m.pid, raw)); else battle.RemoveRemoteProjectile(RId(m.pid, raw)); }
                    Burst(m.p, hs.color);
                    Sfx.Play("hit", 0.5f);
                    return;
                }
            }
            OnNetCoop(m);   // Boss、小怪、法術、陷阱、召喚物、隊友狀態：和合作的隊友端一樣
        }

        int OtherDuelist(int pid) { foreach (var a in battle.allies) if (a.pid != pid) return a.pid; return pid; }

        /// <summary>房主開打（或中途加入）：照房主的模式建一場代理戰鬥</summary>
        void StartWatchBattle(Msg w)
        {
            watchResult = null;
            // 觀戰端沒有 AR：世界座標＝畫面座標，場地＝房主的場地
            WorldFrame.SetMarker(Pose.identity);
            if (w.pts != null && w.pts.Length >= 3)
            {
                var poly = new List<Vector3>();
                foreach (var p in w.pts) poly.Add(new Vector3(p.x, 0f, p.z));
                area.SetPolygon(poly, Pose.identity);
            }
            difficulty = Mathf.Clamp(w.hp, 0, 2);
            watchMode = w.e;
            NetIsHost = false;
            if (w.e >= 1)
            {
                coop = true; netEndless = w.e == 2; netDungeon = w.e == 3;
                if (netDungeon) { netDungeonId = w.k; pendingDungeon = Dungeons.Get(w.k); }
                StartBossBattle(true, w.e >= 2);
                prof = null;   // 觀眾不拿經驗、不存角色
            }
            else StartWatchDuel();
            // 隱形的我：倒下、放在很遠的地方（不會被當目標，也不會觸發靠近時的音效與震動）
            Me.hp = 0f;
            Me.head = new Vector3(0f, 1.6f, -500f);
        }

        /// <summary>對戰（兩人）：兩位玩家都當成隊友顯示，敵人是看不見、打不死的空位</summary>
        void StartWatchDuel()
        {
            EndBattle();
            bossMode = false; coop = true; endless = false; dungeon = null; dummyMode = false; enemyClassUsed = "watch";
            netEndless = netDungeon = false;
            var me = new Fighter("觀眾", Skills.Classes["mage"], new string[0], true);
            var en = new Fighter("—", DummyClass, new string[0], false) { head = new Vector3(0f, -50f, 0f) };
            battle = new Battle(me, en) { remoteEnemy = true };
            foreach (var pid in PeerIds) battle.allies.Add(WatchFighter(pid));
            battle.OnEvent += OnBattleEvent;
            battle.OnLane += ShowLane;
            lastEnemyFeet = en.Feet; lastEnemyHp = en.hp; enemyDeadShown = false;
            phase = Phase.Fighting;
            Say("⚔ 對戰開始！", 2.5f);
        }

        Fighter WatchFighter(int pid)
        {
            peerInfo.TryGetValue(pid, out var pi);
            var rc = Skills.Classes.TryGetValue(pi?.cls ?? "", out var found) ? found : Skills.Classes["mage"];
            var adv = Advanced.Get(pi?.adv);
            var head = NetPeers.TryGetValue(pid, out var pp) ? pp.head : new Vector3(0f, 1.6f, 0f);
            var f = new Fighter(PlayerLabel(pid, rc.id, pi?.adv), rc, pi?.loadout ?? new string[0], false) { head = head, isAlly = true, pid = pid };
            Advanced.ApplyStats(f, adv);
            return f;
        }

        /// <summary>「房主・大法師」「玩家2・遊俠」</summary>
        static string PlayerLabel(int pid, string cls, string adv)
        {
            var a = Advanced.Get(adv);
            string n = a != null ? a.name : Skills.Classes.TryGetValue(cls ?? "", out var c) ? c.name : "?";
            return (pid == 0 ? "房主" : $"玩家{pid}") + "・" + n;
        }

        /// <summary>觀戰端每幀：只跑顯示（法術飛行、召喚物、小怪外觀、場景），不跑 AI、不判定、不拿獎勵</summary>
        void UpdateWatchBattle()
        {
            if (battle == null || phase != Phase.Fighting) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            UpdateScene();
            SyncRemote();
            Me.hp = 0f;
            battle.Update(dt);
            UpdateVisuals();
        }

        // ---------------------------------------------------------------- 視角跟隨的對象（GameRoot 的「🎯 跟隨」按鈕）
        /// <summary>第 i 個跟隨對象：0＝全場（場地中心），之後是每位玩家，最後是 Boss（看得到時）</summary>
        public int WatchFocusCount => 1 + (battle != null ? battle.allies.Count : 0) + (WatchBossVisible ? 1 : 0);
        bool WatchBossVisible => battle != null && bossRig && bossRig.gameObject.activeInHierarchy && En.Alive && bossMode;

        public bool WatchFocus(int i, out Vector3 world, out string name)
        {
            world = Vector3.zero; name = "全場";
            if (i <= 0 || battle == null)
            {
                var poly = area.Polygon;
                if (poly.Count > 0) { foreach (var q in poly) world += q; world /= poly.Count; world.y = 0f; }
                return true;
            }
            if (i - 1 < battle.allies.Count) { var a = battle.allies[i - 1]; world = a.Feet; name = a.name; return true; }
            if (WatchBossVisible) { world = En.Feet; name = En.name; return true; }
            return false;
        }

        // ---------------------------------------------------------------- 觀戰畫面
        public void DrawWatchHud(float W, float H, float pad, float top, float lineH, int fs, int fsS, Camera view)
        {
            Styles();
            string modeName = watchMode == 0 ? "⚔ 對戰" : watchMode == 1 ? "🤝 合作打 Boss" : watchMode == 2 ? "♾ 無盡（合作）" : watchMode == 3 ? $"🏰 副本「{dungeon?.name ?? Dungeons.Get(netDungeonId)?.name}」" : "";
            UiKit.Pill(top + lineH * 0.5f, "👀 觀戰中" + (modeName != "" ? "・" + modeName : ""), fs, new Color(0.55f, 0.9f, 1f), W - pad * 2);
            float y = top + lineH * 1.4f;

            if (phase != Phase.Fighting || battle == null)
            {
                // 還沒開打：房間裡的玩家
                var r = new Rect(pad, y, W - pad * 2, lineH * (3.2f + PeerIds.Count));
                UiKit.Card(r, 0.8f, new Color(0.55f, 0.9f, 1f), 0.7f);
                UiKit.Text(new Rect(r.x + pad, r.y + lineH * 0.2f, r.width - pad * 2, lineH), "⏳ 等待房主開始戰鬥…", fs, Color.white, TextAnchor.MiddleLeft, true);
                float yy = r.y + lineH * 1.3f;
                if (PeerIds.Count == 0) { UiKit.Text(new Rect(r.x + pad, yy, r.width - pad * 2, lineH), "（房間裡還沒有玩家資料）", fsS, UiKit.Muted); yy += lineH; }
                foreach (var pid in PeerIds)
                {
                    peerInfo.TryGetValue(pid, out var pi);
                    UiKit.Text(new Rect(r.x + pad, yy, r.width - pad * 2, lineH), "・" + (pi != null ? PlayerLabel(pid, pi.cls, pi.adv) : (pid == 0 ? "房主" : $"玩家{pid}") + "（還沒選職業）"), fsS, new Color(0.85f, 0.9f, 1f));
                    yy += lineH;
                }
                if (!string.IsNullOrEmpty(watchResult)) UiKit.Text(new Rect(r.x + pad, yy, r.width - pad * 2, lineH), "上一場：" + watchResult, fsS, UiKit.Gold, TextAnchor.MiddleLeft, true);
                return;
            }

            // ---- Boss 血條（合作）
            if (WatchBossVisible)
            {
                string extra = bossShellShown ? "　🛡 護盾" : bossEnragedShown ? "　🔥 狂暴" : "";
                UiKit.Bar(new Rect(pad, y, W - pad * 2, lineH * 0.8f), En.hp / Mathf.Max(1f, En.maxHp), new Color(1f, 0.35f, 0.25f), $"{En.name}　{Mathf.CeilToInt(En.hp)}/{En.maxHp}{extra}", fsS);
                y += lineH * 0.95f;
            }
            if (endless && battle.wave > 0)
            {
                string info = dungeon != null ? $"第 {battle.wave} 波／共 {battle.waveTotal} 波" : $"第 {battle.wave} 波";
                UiKit.Text(new Rect(pad, y, W - pad * 2, lineH * 0.8f), $"{info}　這波剩 {WaveRemainingShown}　擊倒 {battle.kills}", fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter, true);
                y += lineH * 0.85f;
            }
            // ---- 玩家血條
            float bw = (W - pad * 3) / 2f;
            for (int i = 0; i < battle.allies.Count; i++)
            {
                var a = battle.allies[i];
                var rr = new Rect(pad + (i % 2) * (bw + pad), y + (i / 2) * lineH * 0.9f, bw, lineH * 0.75f);
                UiKit.Bar(rr, a.Alive ? a.hp / Mathf.Max(1f, a.maxHp) : 0f, a.Alive ? a.cls.color : Color.gray, (a.Alive ? "" : "💀 ") + a.name, Mathf.RoundToInt(fsS * 0.9f));
            }
            y += ((battle.allies.Count + 1) / 2) * lineH * 0.9f;

            // ---- 名牌：每位玩家頭上
            if (view != null)
                foreach (var a in battle.allies)
                {
                    var sp = view.WorldToScreenPoint(WorldFrame.FromWorld(a.Feet + Vector3.up * 2.1f));
                    if (sp.z <= 0f) continue;
                    float tw = fsS * 7f, th = fsS * 1.4f;
                    var tr = new Rect(sp.x - tw / 2f, H - sp.y - th, tw, th);
                    UiKit.Text(tr, a.name, Mathf.RoundToInt(fsS * 0.85f), a.Alive ? Color.white : Color.gray, TextAnchor.MiddleCenter, true);
                    UiKit.Bar(new Rect(tr.x + tw * 0.2f, tr.yMax, tw * 0.6f, th * 0.35f), a.Alive ? a.hp / Mathf.Max(1f, a.maxHp) : 0f, a.cls.color, "", 1);
                }

            if (Time.time < messageUntil) UiKit.Pill(H * 0.62f, message, fsS, new Color(1f, 0.95f, 0.6f), W - pad * 2);
            if (!string.IsNullOrEmpty(watchResult))
            {
                var rr = new Rect(pad * 2, H * 0.36f, W - pad * 4, lineH * 3f);
                UiKit.Card(rr, 0.9f, UiKit.Gold, 0.9f);
                UiKit.Text(new Rect(rr.x + pad, rr.y, rr.width - pad * 2, lineH * 1.9f), watchResult, Mathf.RoundToInt(fs * 1.2f), Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(rr.x + pad, rr.y + lineH * 1.8f, rr.width - pad * 2, lineH), "等待下一場…", fsS, UiKit.Muted, TextAnchor.MiddleCenter);
            }
        }

        // ================================================================ 房主端
        /// <summary>給觀眾的戰況：每位玩家的職業與技能、這一場的模式與場地（世界座標）</summary>
        public List<Msg> WatchSnapshot(Vector3[] areaWorld)
        {
            var l = new List<Msg>();
            l.Add(new Msg { t = "wply", id = MyPid, k = myClass, ks = myLoadout.ToArray(), a = Progress.Char(myClass).adv ?? "" });
            foreach (var pid in PeerIds)
                if (peerInfo.TryGetValue(pid, out var pi)) l.Add(new Msg { t = "wply", id = pid, k = pi.cls, ks = pi.loadout, a = pi.adv ?? "" });
            if (phase == Phase.Fighting && battle != null && NetMode)
                l.Add(new Msg { t = "wstart", e = NetModeCode, k = netDungeonId ?? "", hp = difficulty, pts = areaWorld });
            else
            {
                l.Add(new Msg { t = "wend" });
                if (phase == Phase.Over && battle != null && NetMode) l.Add(new Msg { t = "wover", k = WatchOverText() });
            }
            return l;
        }

        /// <summary>結算時給觀眾看的一行字</summary>
        public string WatchOverText()
        {
            if (battle == null) return "";
            if (!coop)
            {
                string meName = PlayerLabel(MyPid, myClass, Progress.Char(myClass).adv);
                int op = PeerIds.Count > 0 ? PeerIds[0] : 1;
                string opName = peerInfo.TryGetValue(op, out var pi) ? PlayerLabel(op, pi.cls, pi.adv) : $"玩家{op}";
                if (Me.Alive && !En.Alive) return $"🏆 {meName} 獲勝！";
                if (!Me.Alive && En.Alive) return $"🏆 {opName} 獲勝！";
                return "⚔ 對戰結束";
            }
            bool win = battle.stageCleared || (!endless && !En.Alive);
            if (win) return dungeon != null ? $"🎉 通關副本「{dungeon.name}」！" : "🎉 隊伍打倒了 Boss！";
            return endless ? $"💀 全員倒下（撐到第 {battle.wave} 波，擊倒 {battle.kills}）" : "💀 全員倒下";
        }
    }
}
