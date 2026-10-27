using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本的特殊地點（使用者要求，劇本寫 event = xxx）：只在房主／單人跑邏輯，隊友收 rev 訊息顯示。
    ///   defend   守住目標：場地中央的守護物有血條，小怪優先打它；清完這一波就過關，守護物被打壞＝副本失敗
    ///   survive  撐住 N 秒：怪不停湧出、更痛；最後 10 秒出一隻菁英；時間到就過關
    ///   escort   護送前進：NPC 沿著場地繞一段路走到出口，有怪貼近就停下來；走到＝過關，倒下＝副本失敗
    ///   hunt     斬首：打倒指定的目標就過關；其他怪不斷湧出（打倒一隻 3 秒後補一隻），拖太久目標會叫援軍
    ///   totems   連鎖封印：場邊的圖騰會一直叫怪；打掉所有圖騰、清完怪＝過關
    ///   runes    符文順序：符文照順序亮一遍，照順序踩上去；踩錯出一群怪（越錯越強），打完重新看一次
    ///   chant    咒語之門：用語音唸出謎題的答案；唸錯出一群怪（越錯越強），打完再唸
    ///   elements 對應元素：每座雕像要用對的屬性（火、冰、雷）打它；場邊的屬性球可以讓全隊下一擊帶那個屬性；打錯出怪
    ///   mimic    找出真身：幾個寶箱只有一個是真的（會偶爾閃一下）；打錯的會變成怪物（越錯越強）
    ///   collapse 崩塌倒數：地面一格一格塌陷（先紅框預警），站在塌掉的地方會一直受傷；清完這一波就過關
    ///   dark     黑暗房：看不清楚遠處的敵人（只看得到發光的眼睛）；清完這一波就過關
    /// </summary>
    public partial class Battle
    {
        public static readonly string[] RoomEventKinds = { "defend", "survive", "escort", "hunt", "totems", "runes", "chant", "elements", "mimic", "collapse", "dark" };
        public static bool IsRoomEvent(string kind) => Array.IndexOf(RoomEventKinds, kind) >= 0;

        /// <summary>目前地點的特殊玩法（null＝一般戰鬥）；隊友端由 rev 訊息設定，只用來顯示</summary>
        public string roomEvent;
        Dictionary<string, string> evParam;
        /// <summary>階段：符文 0 看順序、1 踩；咒語／元素／寶箱 1 作答；所有解謎 2 懲罰（打怪中）；3 完成</summary>
        public int evPhase;
        public int evFails;
        public float evEndAt, evPhaseAt;
        /// <summary>場上的點（符文、雕像＋屬性球、寶箱、崩塌格子…）與每個點的狀態；隊友端照這個畫</summary>
        public readonly List<Vector3> evPts = new List<Vector3>();
        public readonly List<int> evState = new List<int>();
        /// <summary>畫面上方的任務說明（房主算好送給隊友）</summary>
        public string evText = "";
        /// <summary>咒語之門的題目、提示（隊友也要看得到）</summary>
        public string evRiddle, evHint;
        string evAnswer;
        int[] evSeq; int evSeqPos;
        readonly Dictionary<Fighter, int> evOnPoint = new Dictionary<Fighter, int>();
        readonly List<float> evQueue = new List<float>();   // 排隊要出的怪（時間）
        readonly List<float> evRespawn = new List<float>(); // 斬首、護送、撐住：打倒的怪幾秒後補
        float evPenaltyMul = 1f, evNextTick, evNextSummon;
        /// <summary>元素：全隊的屬性加持（1 火、2 冰、3 雷）與到期時間</summary>
        public int evCharge; public float evChargeUntil;
        Fighter evWalker, evHunt;
        int EvParty => Mathf.Max(1, dungeonParty > 0 ? dungeonParty : 1 + allies.Count);
        /// <summary>實際上場的玩家數（多人版：撐住時間、守護物／護送的血量、圖騰數量、最後的菁英數跟著人數）</summary>
        int PlayersN => 1 + allies.Count;

        public const float EvStepRadius = 0.45f, EvOrbRadius = 0.6f, EvChargeTime = 12f, EvRespawnDelay = 3f;
        public static readonly string[] ElementNames = { "", "火", "冰", "雷" };

        string P(string k, string d) => evParam != null && evParam.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v) ? v : d;
        int PI(string k, int d) => int.TryParse(P(k, ""), out var v) ? v : d;

        // ------------------------------------------------------------ 場地幾何
        Vector3 ArenaCenter()
        {
            if (arenaPoly == null || arenaPoly.Count < 3) return player.Feet;
            var c = Vector3.zero; foreach (var p in arenaPoly) c += p; c /= arenaPoly.Count; c.y = 0f; return c;
        }
        /// <summary>中心到場地邊的最短距離</summary>
        float ArenaInner()
        {
            if (arenaPoly == null || arenaPoly.Count < 3) return 3f;
            var c = ArenaCenter(); float best = 99f;
            for (int i = 0; i < arenaPoly.Count; i++) best = Mathf.Min(best, DistancePointSegment(c, arenaPoly[i], arenaPoly[(i + 1) % arenaPoly.Count]));
            return Mathf.Clamp(best, 1.5f, 8f);
        }
        Vector3 Ring(float r, float deg) { var c = ArenaCenter(); float a = deg * Mathf.Deg2Rad; return c + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r); }

        // ------------------------------------------------------------ 開始
        /// <summary>房主：進到特殊地點（EnterNode 之後叫）</summary>
        public void StartRoomEvent(string kind, Dictionary<string, string> param)
        {
            CleanupRoomEvent();
            if (!IsRoomEvent(kind)) return;
            roomEvent = kind; evParam = param; evPhase = 1; evFails = 0; evPenaltyMul = 1f;
            evPts.Clear(); evState.Clear(); evQueue.Clear(); evRespawn.Clear(); evOnPoint.Clear();
            evText = ""; evRiddle = evHint = null; evCharge = 0; evWalker = evHunt = null;
            float r = Mathf.Min(1.6f, ArenaInner() * 0.55f);
            var c = ArenaCenter();
            switch (kind)
            {
                case "defend":
                {
                    var core = SpawnNpc("ev_core", P("name", "守護之鐘"), "priest", 0, true, PI("hp", 900) * PlayersN, 0, c);   // 多人：血量 ×人數
                    core.rooted = true; core.lure = 5f; core.bodyRadius = 0.5f;
                    break;
                }
                case "survive":
                    waveToSpawn = 0; evEndAt = waveStartAt + PI("time", 30) + 10f * (PlayersN - 1); evNextSummon = evEndAt - 10f;   // 多人：每多一人 +10 秒
                    for (int i = 0; i < EvCap(); i++) evQueue.Add(waveStartAt + i * 0.5f);
                    break;
                case "escort":
                {
                    waveToSpawn = 0;
                    // 從離玩家最近的那一邊出發，沿著場內的圓繞 240° 到出口
                    float start = Mathf.Atan2(player.Feet.x - c.x, player.Feet.z - c.z) * Mathf.Rad2Deg;
                    float rr = Mathf.Max(1.2f, ArenaInner() * 0.6f);
                    var path = new List<Vector3>();
                    for (int i = 0; i <= 16; i++) path.Add(Ring(rr, start + 240f * i / 16f));
                    evWalker = SpawnNpc("ev_walker", P("name", "受傷的旅人"), P("look", "priest"), 0, true, PI("hp", 500) * PlayersN, 0, path[0]);   // 多人：血量 ×人數
                    evWalker.walkPath = path; evWalker.walkIdx = 1; evWalker.lure = 2f;
                    evPts.Add(path[path.Count - 1]); evState.Add(0);   // 出口
                    for (int i = 0; i < EvCap(); i++) evQueue.Add(waveStartAt + 1f + i * 0.8f);
                    break;
                }
                case "hunt":
                {
                    waveToSpawn = 0;
                    int k = FixedDungeonSpawns ? DungeonMinionKind(4) : PickMinionKind(MinionRole.Back);
                    var at = edgePointAt != null ? edgePointAt(180f) : RandomInsidePoint(Ring(ArenaInner(), 180f));
                    evHunt = SpawnMinion(at, 0, k, HpMul * 1.5f, DmgMul, SpeedMul);
                    MakeElite(evHunt); evHunt.huntTarget = true; evHunt.name = "目標・" + evHunt.name;
                    for (int i = 0; i < EvCap(); i++) evQueue.Add(waveStartAt + 0.5f + i * 0.4f);
                    evNextSummon = waveStartAt + 45f;
                    break;
                }
                case "totems":
                {
                    waveToSpawn = 0;
                    int n = Mathf.Clamp(PI("count", 3) + PlayersN - 1, 2, 6);   // 多人：每多一人多一根
                    float rr = Mathf.Max(1.4f, ArenaInner() * 0.85f);
                    for (int i = 0; i < n; i++)
                    {
                        var t = SpawnNpc("ev_totem_" + i, "封印圖騰", "priest", 1, false, PI("hp", 260), 0, Ring(rr, 360f * i / n + 30f));
                        t.rooted = true; t.bodyRadius = 0.45f;
                    }
                    evNextTick = waveStartAt + 2f;
                    break;
                }
                case "runes":
                {
                    waveToSpawn = 0;
                    int n = Mathf.Clamp(PI("count", 4), 3, 6);
                    for (int i = 0; i < n; i++) { evPts.Add(Ring(r, 360f * i / n + 45f)); evState.Add(0); }
                    NewRuneSequence();
                    break;
                }
                case "chant":
                {
                    waveToSpawn = 0;
                    var q = Riddles[(wave * 7 + (evParam != null ? evParam.Count : 0)) % Riddles.Length];
                    evRiddle = P("riddle", q.riddle); evAnswer = P("answer", q.answer); evHint = P("hint", q.hint);
                    evPts.Add(c + Fighter.Flat(player.forward) * Mathf.Min(2f, ArenaInner())); evState.Add(0);   // 門的位置（畫面用）
                    break;
                }
                case "elements":
                {
                    waveToSpawn = 0;
                    var order = new List<int> { 1, 2, 3 };
                    for (int i = 2; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
                    float rr = Mathf.Max(1.4f, ArenaInner() * 0.8f);
                    for (int i = 0; i < 3; i++)
                    {
                        var st = SpawnNpc("ev_statue_" + i, ElementNames[order[i]] + "之像", "priest", 1, false, 999999, 0, Ring(rr, 120f * i));
                        st.rooted = true; st.damageTakenMul = 0f; st.evElement = order[i]; st.bodyRadius = 0.45f;
                        evPts.Add(st.Feet); evState.Add(0);
                    }
                    for (int i = 0; i < 3; i++) { evPts.Add(Ring(r * 0.8f, 120f * i + 60f)); evState.Add(i + 1); }   // 屬性球：1 火、2 冰、3 雷
                    break;
                }
                case "mimic":
                {
                    waveToSpawn = 0;
                    int n = Mathf.Clamp(PI("count", 5), 3, 7), real = rng.Next(n);
                    float rr = Mathf.Max(1.3f, ArenaInner() * 0.7f);
                    for (int i = 0; i < n; i++)
                    {
                        var ch = SpawnNpc("ev_chest_" + i, "寶箱", "priest", 1, false, 999999, 0, Ring(rr, 360f * i / n));
                        ch.rooted = true; ch.damageTakenMul = 0f; ch.evReal = i == real; ch.bodyRadius = 0.4f;
                    }
                    evNextTick = waveStartAt + 3f;
                    break;
                }
                case "collapse":
                    BuildCollapseCells();
                    evNextTick = waveStartAt + 4f;
                    break;
            }
            Emit("evstart", player.Feet, Color.white, kind);
            RefreshEvText();
        }

        int EvCap() => roomEvent == "survive" ? 4 + EvParty : 3 + EvParty;

        /// <summary>這個地點結束（清完、換地點、副本結束）：拿掉道具、清狀態</summary>
        public void CleanupRoomEvent()
        {
            if (roomEvent == null && evPts.Count == 0) return;
            npcs.RemoveAll(n => n.npcId != null && n.npcId.StartsWith("ev_"));
            roomEvent = null; evPts.Clear(); evState.Clear(); evQueue.Clear(); evRespawn.Clear(); evText = ""; evPhase = 0;
            evWalker = evHunt = null; evCharge = 0;
        }

        /// <summary>過關：剩下的怪消失、拿掉道具、照一般地點的流程結束</summary>
        void FinishRoomEvent(string msg)
        {
            foreach (var m in minions) if (m.Alive) { m.hp = 0f; Emit("miniondie", m.Feet, m.cls.color, null); }
            evPhase = 3;
            Emit("evdone", player.Feet, new Color(0.5f, 1f, 0.6f), msg);
            CleanupRoomEvent();
            waveToSpawn = 0;
            Emit("wavecleared", player.Feet, Color.white, $"{WaveLabel}清空！");
            StageDone();
        }

        // ------------------------------------------------------------ 每幀（UpdateEndless 開頭叫；回傳 true＝這一幀的出怪、過關都由這裡處理）
        bool UpdateRoomEvent()
        {
            if (roomEvent == null || remoteEnemy) return false;
            float dt = Time_dt;
            // 排隊的怪：到時間就出
            for (int i = evQueue.Count - 1; i >= 0; i--) if (now >= evQueue[i]) { evQueue.RemoveAt(i); EvSpawn(); }
            bool handled = true;
            switch (roomEvent)
            {
                case "defend":
                {
                    var core = npcs.Find(n => n.npcId == "ev_core");
                    if (core == null || !core.Alive) { FailStage(P("fail", "守護目標被打壞了…")); return true; }
                    handled = false;   // 出怪、過關照一般流程（清完這一波）
                    break;
                }
                case "dark": handled = false; break;
                case "collapse": TickCollapse(dt); handled = false; break;
                case "survive":
                    RefillEv(1.2f);
                    if (evNextSummon > 0f && now >= evNextSummon) { evNextSummon = 0f; for (int i = 0; i < PlayersN; i++) { var el = EvSpawn(); if (el != null) MakeElite(el); } }   // 多人：最後衝刺的菁英數＝人數
                    if (now >= evEndAt) { FinishRoomEvent("撐住了！"); return true; }
                    break;
                case "escort":
                    if (evWalker == null || !evWalker.Alive) { FailStage(P("fail", $"{P("name", "受傷的旅人")}倒下了…")); return true; }
                    RefillEv(EvRespawnDelay);
                    if (evWalker.walkPath == null) { FinishRoomEvent($"{evWalker.name}平安抵達了！"); return true; }
                    break;
                case "hunt":
                    if (evHunt == null || !evHunt.Alive) { FinishRoomEvent("目標倒下了！"); return true; }
                    RefillEv(EvRespawnDelay);
                    if (now >= evNextSummon) { evNextSummon = now + 30f; for (int i = 0; i < 3; i++) evQueue.Add(now + i * 0.3f); Emit("evinfo", evHunt.Feet, new Color(1f, 0.5f, 0.3f), $"{evHunt.name}叫來了援軍！"); }
                    break;
                case "totems":
                {
                    int alive = 0;
                    foreach (var n in npcs) if (n.Alive && n.npcId != null && n.npcId.StartsWith("ev_totem")) alive++;
                    if (alive > 0 && now >= evNextTick)
                    {
                        evNextTick = now + 6.5f;
                        int cnt = 0; foreach (var m in minions) if (m.Alive) cnt++;
                        foreach (var n in npcs)
                            if (n.Alive && n.npcId != null && n.npcId.StartsWith("ev_totem") && cnt < EvCap() + 1)
                            { var nm = EvSpawn(n.Feet + Fighter.Flat(ArenaCenter() - n.Feet) * 0.8f); if (nm != null) { cnt++; Emit("evinfo", n.Feet, new Color(0.7f, 0.4f, 1f), null); } }
                    }
                    if (alive == 0 && !AnyMinionAlive() && evQueue.Count == 0) { FinishRoomEvent("封印解除了！"); return true; }
                    break;
                }
                case "runes": TickRunes(); break;
                case "chant": TickPenaltyOnly(); break;
                case "elements": TickElements(); break;
                case "mimic": TickMimic(); break;
            }
            RefreshEvText();
            return handled;
        }

        bool AnyMinionAlive() { foreach (var m in minions) if (m.Alive) return true; return false; }

        /// <summary>補怪（斬首、護送、撐住）：場上少於上限 → delay 秒後補一隻</summary>
        void RefillEv(float delay)
        {
            int alive = 0; foreach (var m in minions) if (m.Alive && !m.huntTarget) alive++;
            int pending = evQueue.Count;
            if (alive + pending < EvCap()) evQueue.Add(now + delay);
        }

        /// <summary>出一隻怪（解謎的懲罰也用這個：血量、攻擊乘上 evPenaltyMul）</summary>
        Fighter EvSpawn(Vector3? at = null)
        {
            int ordinal = dungeonSpawnOrdinal++;
            var p = at ?? (FixedDungeonSpawns ? DungeonSpawnPoint(ordinal) : edgePoint != null ? edgePoint() : RandomInsidePoint(ArenaCenter()));
            int kind = FixedDungeonSpawns ? DungeonMinionKind(ordinal) : PickMinionKind();
            float dmg = DmgMul * evPenaltyMul * (roomEvent == "survive" ? 1.2f : 1f);
            var nm = SpawnMinion(p, 0, kind, HpMul * evPenaltyMul * (IsRanged(kind) ? 1f : meleeHpMul), dmg, SpeedMul);
            if (Theme == CanyonTheme && !at.HasValue) StartCliffLeap(nm);
            return nm;
        }

        /// <summary>解謎答錯：出一群怪，錯越多次越多、越強（使用者要求）；打完才回到作答</summary>
        void EvPenalty(string why)
        {
            evFails++;
            evPenaltyMul = 1f + 0.3f * (evFails - 1);
            int n = 3 + 2 * (evFails - 1) + (EvParty - 1);
            for (int i = 0; i < n; i++) evQueue.Add(now + 0.6f + i * 0.35f);
            evPhase = 2;
            Emit("evwrong", player.Feet, new Color(1f, 0.3f, 0.3f), $"{why}（第 {evFails} 次：怪物 {n} 隻、強度 ×{evPenaltyMul:0.0}）");
        }

        /// <summary>懲罰的怪打完了嗎（打完 → true，回到作答）</summary>
        bool PenaltyOver() => evPhase == 2 && evQueue.Count == 0 && !AnyMinionAlive();

        void TickPenaltyOnly()
        {
            if (PenaltyOver()) { evPhase = 1; Emit("evinfo", player.Feet, Color.white, "再試一次！"); }
        }

        // ------------------------------------------------------------ 符文
        void NewRuneSequence()
        {
            int len = Mathf.Clamp(PI("seq", 3) + (evFails >= 2 ? 0 : 0), 2, 7);
            evSeq = new int[len];
            for (int i = 0; i < len; i++) { int k; do k = rng.Next(evPts.Count); while (i > 0 && k == evSeq[i - 1]); evSeq[i] = k; }
            evSeqPos = 0; evPhase = 0; evPhaseAt = now + 1.2f;
            for (int i = 0; i < evState.Count; i++) evState[i] = 0;
            evOnPoint.Clear();
        }

        void TickRunes()
        {
            const float Show = 0.9f;
            for (int i = 0; i < evState.Count; i++) if (evState[i] == 3 && now > evPhaseAt + 0.6f && evPhase != 0) evState[i] = 0;
            if (evPhase == 0)
            {
                // 照順序亮一遍
                int step = Mathf.FloorToInt((now - evPhaseAt) / Show);
                for (int i = 0; i < evState.Count; i++) evState[i] = 0;
                if (now < evPhaseAt) return;
                if (step < evSeq.Length) { if ((now - evPhaseAt) % Show < Show * 0.75f) evState[evSeq[step]] = 1; return; }
                evPhase = 1; evSeqPos = 0;
                foreach (var pl in Players()) evOnPoint[pl] = PointUnder(pl);   // 開始時本來就站在符文上的不算
                Emit("evinfo", player.Feet, Color.white, "照順序踩上符文！");
                return;
            }
            if (evPhase == 2) { if (PenaltyOver()) { NewRuneSequence(); Emit("evinfo", player.Feet, Color.white, "符文重新亮起，看清楚順序！"); } return; }
            foreach (var pl in Players())
            {
                if (!pl.Alive) continue;
                int on = PointUnder(pl);
                evOnPoint.TryGetValue(pl, out var was);
                evOnPoint[pl] = on;
                if (on < 0 || on == was) continue;   // 踏上新的符文才算
                if (on == evSeq[evSeqPos])
                {
                    evState[on] = 2; evSeqPos++;
                    Emit("evok", evPts[on], new Color(0.5f, 1f, 0.8f), $"{evSeqPos}/{evSeq.Length}");
                    if (evSeqPos >= evSeq.Length) { FinishRoomEvent("符文全部對上了，門打開了！"); return; }
                }
                else { evState[on] = 3; evPhaseAt = now; EvPenalty("踩錯符文了！"); return; }
            }
        }

        int PointUnder(Fighter pl)
        {
            for (int i = 0; i < evPts.Count; i++) if (FlatDistance(pl.Feet, evPts[i]) <= EvStepRadius) return i;
            return -1;
        }

        // ------------------------------------------------------------ 咒語之門
        static readonly (string riddle, string answer, string hint)[] Riddles =
        {
            ("沒有腳卻會走，沒有嘴卻會響；掛在高處，每天叫醒整個村子。", "晨鐘", "炎之里的鐘樓上就有一個"),
            ("白天睡覺、晚上醒來；站在海邊，替回家的船照路。", "燈塔", "潮騷港的海角上"),
            ("寫在碑上會被風吹淡，叫出口就會有人回頭。", "名字", "霧燈記名徑的人都在找它"),
            ("風一吹就鼓起來，船靠它才會走。", "白帆", "白帆號的那個「帆」"),
            ("鐘的心，被拿走了鐘就不響。", "鐘舌", "熔岩巨像胸口掉出來的東西"),
            ("夜晚結束的時候，天邊最先亮起來的。", "黎明", "黎明之門的「黎明」"),
        };

        /// <summary>咒語之門：有人唸出一句話（房主判定；隊友的話用 evsay 送來）。回傳是不是唸對</summary>
        public bool EvChant(string text)
        {
            if (roomEvent != "chant" || evPhase != 1 || string.IsNullOrEmpty(text) || string.IsNullOrEmpty(evAnswer)) return false;
            var t = SpeechMatcher.Normalize(text); var a = SpeechMatcher.Normalize(evAnswer);
            if (t.Length == 0) return false;
            if (SpeechMatcher.Score(t, a) >= 0.75f) { FinishRoomEvent($"「{evAnswer}」——門開了！"); return true; }
            if (t.Length < Mathf.Max(2, a.Length - 1)) return false;   // 太短（雜音）不算
            EvPenalty($"「{text}」不是答案！");
            return false;
        }

        // ------------------------------------------------------------ 對應元素
        /// <summary>技能的屬性（0＝沒有）：火、冰、雷</summary>
        public static int ElementOf(SkillDef s)
        {
            if (s == null || s.id == null) return 0;
            string id = s.id;
            if (s.effect == EffectKind.Burn || id.Contains("fire") || id == "meteor" || id.Contains("flame")) return 1;
            if (id.Contains("ice") || id.Contains("frost") || id.Contains("freeze") || id.Contains("blizzard")) return 2;
            if (id.Contains("thunder") || id.Contains("bolt") || id.Contains("raiton") || id == "tjudge" || id.Contains("shock")) return 3;
            return 0;
        }

        void TickElements()
        {
            if (evCharge != 0 && now > evChargeUntil) { evCharge = 0; Emit("evinfo", player.Feet, Color.white, "屬性加持消失了"); }
            if (evPhase == 2) { TickPenaltyOnly(); return; }
            // 屬性球：有人碰到 → 全隊下一擊帶那個屬性（12 秒）
            foreach (var pl in Players())
            {
                if (!pl.Alive) continue;
                for (int i = 3; i < evPts.Count; i++)
                    if (FlatDistance(pl.Feet, evPts[i]) <= EvOrbRadius && (evCharge != evState[i] || now > evChargeUntil - EvChargeTime + 1f))
                    { evCharge = evState[i]; evChargeUntil = now + EvChargeTime; Emit("evcharge", evPts[i], ElementColor(evCharge), $"{ElementNames[evCharge]}之力！下一擊帶{ElementNames[evCharge]}屬性"); }
            }
        }

        public static Color ElementColor(int e) => e == 1 ? new Color(1f, 0.45f, 0.15f) : e == 2 ? new Color(0.45f, 0.8f, 1f) : e == 3 ? new Color(1f, 0.9f, 0.3f) : Color.white;

        // ------------------------------------------------------------ 找出真身
        void TickMimic()
        {
            if (evPhase == 2 && PenaltyOver()) evPhase = 1;
            if (now >= evNextTick)
            {
                evNextTick = now + 3.5f + (float)rng.NextDouble() * 2f;
                var real = npcs.Find(n => n.evReal && n.Alive);
                if (real != null) Emit("evglint", real.Feet, new Color(1f, 0.9f, 0.5f), null);
            }
        }

        // ------------------------------------------------------------ 道具被打（雕像、寶箱）：回傳 true＝這一下被攔下來（不扣血）
        bool EvPropHit(Fighter target, Fighter by)
        {
            if (roomEvent == null) return false;
            if (by != null && !(by.isPlayer || by.isAlly || by == player)) return target.damageTakenMul <= 0f;   // 只有玩家打的才算
            var skill = incoming ?? HitSkill;
            if (target.evElement > 0)
            {
                if (evPhase != 1) return true;
                int e = ElementOf(skill); if (e == 0 && now <= evChargeUntil) e = evCharge;
                int idx = evPts.FindIndex(p => FlatDistance(p, target.Feet) < 0.1f);
                if (idx >= 0 && evState[idx] == 1) return true;   // 已經點亮
                if (e == 0) { Emit("evinfo", target.Chest, Color.white, "沒有屬性：先碰場邊的屬性球，或用火、冰、雷的招式"); return true; }
                if (e == target.evElement)
                {
                    if (idx >= 0) evState[idx] = 1;
                    evCharge = 0;
                    Emit("evok", target.Feet, ElementColor(e), $"{target.name}亮起來了！");
                    bool all = true; for (int i = 0; i < 3 && i < evState.Count; i++) if (evState[i] != 1) all = false;
                    if (all) FinishRoomEvent("三座雕像都亮了！");
                }
                else { evCharge = 0; EvPenalty($"{ElementNames[e]}屬性打錯了！{target.name}要的是{ElementNames[target.evElement]}"); }
                return true;
            }
            if (target.npcId != null && target.npcId.StartsWith("ev_chest"))
            {
                if (evPhase == 3) return true;
                if (target.evReal) { FinishRoomEvent("找到真的寶箱了！"); return true; }
                // 假寶箱：變成怪物（越錯越強）
                evFails++; evPenaltyMul = 1.3f + 0.3f * (evFails - 1); evPhase = 2;
                var at = target.Feet; target.hp = 0f; npcs.Remove(target);
                var m = EvSpawn(at); if (m != null) { m.maxHp = Mathf.RoundToInt(m.maxHp * 1.5f); m.hp = m.maxHp; }
                Emit("evwrong", at, new Color(1f, 0.3f, 0.3f), $"是寶箱怪！（第 {evFails} 次，強度 ×{evPenaltyMul:0.0}）");
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ 崩塌
        void BuildCollapseCells()
        {
            evPts.Clear(); evState.Clear();
            if (arenaPoly == null || arenaPoly.Count < 3) return;
            float minX = 99, maxX = -99, minZ = 99, maxZ = -99;
            foreach (var p in arenaPoly) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
            for (float x = minX + 0.5f; x < maxX; x += 1f)
                for (float z = minZ + 0.5f; z < maxZ; z += 1f)
                {
                    var p = new Vector3(x, 0f, z);
                    if (minionInside != null && !minionInside(p)) continue;
                    evPts.Add(p); evState.Add(0);   // 0 完好、1 預警、2 塌了
                }
        }

        void TickCollapse(float dt)
        {
            var c = ArenaCenter();
            for (int i = 0; i < evState.Count; i++) if (evState[i] == 1 && now >= evPhaseAt) evState[i] = 2;
            if (now >= evNextTick && evPts.Count > 0)
            {
                evNextTick = now + 3.5f;
                int collapsed = 0; foreach (var s in evState) if (s > 0) collapsed++;
                if (collapsed < evPts.Count * 0.6f)
                {
                    // 由外往內：挑最外圈的幾格隨機一格
                    int best = -1; float bestD = -1f;
                    for (int tries = 0; tries < 6; tries++)
                    {
                        int k = rng.Next(evPts.Count);
                        if (evState[k] != 0 || FlatDistance(evPts[k], c) < 1.3f) continue;
                        float d = FlatDistance(evPts[k], c); if (d > bestD) { bestD = d; best = k; }
                    }
                    if (best >= 0) { evState[best] = 1; evPhaseAt = now + 2f; Emit("evinfo", evPts[best], new Color(1f, 0.4f, 0.2f), null); }
                }
            }
            // 站在塌掉的格子：每秒扣最大 HP 的 8%
            if (now >= evPhaseAt - 1.99f && Mathf.Repeat(now, 1f) < dt)
                foreach (var pl in new List<Fighter>(Players()))
                {
                    if (!pl.Alive) continue;
                    for (int i = 0; i < evPts.Count; i++)
                    {
                        if (evState[i] != 2) continue;
                        var d = pl.Feet - evPts[i];
                        if (Mathf.Abs(d.x) > 0.5f || Mathf.Abs(d.z) > 0.5f) continue;
                        int dmg = Mathf.Max(1, Mathf.RoundToInt(pl.maxHp * 0.08f));
                        if (pl.isAlly) { Emit("allyhit", pl.Chest, Color.red, null); OnAllyHit?.Invoke(pl, Skills.All["m_rock"], dmg, pl.Chest, 0); }
                        else { pl.hp = Mathf.Max(0f, pl.hp - dmg); Emit("hit", pl.Chest, Color.red, $"掉進裂縫 -{dmg}"); }
                        break;
                    }
                }
        }

        // ------------------------------------------------------------ 護送：沿路走（UpdateNpcs 叫）
        void EscortWalk(Fighter n, float dt)
        {
            if (n.walkPath == null || n.walkIdx >= n.walkPath.Count) { n.walkPath = null; return; }
            // 有怪貼近（1.3m 內）就停下來
            foreach (var m in minions) if (m.Alive && FlatDistance(m.Feet, n.Feet) < 1.3f) return;
            var goal = n.walkPath[n.walkIdx];
            var to = Fighter.Flat(goal - n.Feet); float d = FlatDistance(goal, n.Feet);
            if (d < 0.1f) { n.walkIdx++; if (n.walkIdx >= n.walkPath.Count) n.walkPath = null; return; }
            float step = Mathf.Min(d, EscortSpeed * dt);
            n.head += to * step; n.forward = to;
        }
        public const float EscortSpeed = 0.4f;

        // ------------------------------------------------------------ 說明文字
        void RefreshEvText()
        {
            switch (roomEvent)
            {
                case "defend": { var core = npcs.Find(n => n.npcId == "ev_core"); evText = core != null ? $"🛡 守住{core.name}！（{Mathf.CeilToInt(core.hp)}/{core.maxHp}）清完這一波就過關" : ""; break; }
                case "survive": evText = $"⏳ 撐住！剩 {Mathf.Max(0f, evEndAt - now):0} 秒"; break;
                case "escort": evText = evWalker != null ? $"🚶 護送{evWalker.name}走到出口（{Mathf.CeilToInt(evWalker.hp)}/{evWalker.maxHp}）：有怪貼近他就會停下來" : ""; break;
                case "hunt": evText = evHunt != null ? $"🎯 斬首：打倒「{evHunt.name}」！其他怪會不斷湧出" : ""; break;
                case "totems": { int a = 0; foreach (var n in npcs) if (n.Alive && n.npcId != null && n.npcId.StartsWith("ev_totem")) a++; evText = a > 0 ? $"🗿 打掉封印圖騰（剩 {a} 根）：圖騰會一直叫怪" : "🗿 圖騰都倒了，清掉剩下的怪！"; break; }
                case "runes": evText = evPhase == 0 ? "👁 看清楚符文亮起的順序…" : evPhase == 1 ? $"👣 照順序踩上符文（{evSeqPos}/{evSeq.Length}）" : $"⚔ 踩錯了！打倒湧出來的怪（錯 {evFails} 次）"; break;
                case "chant": evText = evPhase == 1 ? $"🗣 唸出答案：{evRiddle}" + (evFails > 0 && evHint != null ? $"（提示：{evHint}）" : "") : $"⚔ 唸錯了！打倒湧出來的怪（錯 {evFails} 次）"; break;
                case "elements": evText = evPhase == 1 ? "🔥❄⚡ 用對的屬性打雕像" + (evCharge > 0 ? $"（全隊帶{ElementNames[evCharge]}屬性，剩 {Mathf.Max(0f, evChargeUntil - now):0} 秒）" : "（碰場邊的屬性球，或用火、冰、雷的招式）") : $"⚔ 打錯了！打倒湧出來的怪（錯 {evFails} 次）"; break;
                case "mimic": evText = evPhase == 2 ? $"⚔ 寶箱怪！打倒牠（錯 {evFails} 次）" : "🎁 找出真的寶箱：真的會偶爾閃一下（打錯會變成怪物）"; break;
                case "collapse": evText = "⚠ 地面在崩塌！別站在紅框和裂縫上，清完這一波就過關"; break;
                case "dark": evText = "🌑 一片漆黑…只看得到發光的眼睛，清完這一波就過關"; break;
                default: evText = ""; break;
            }
        }
    }
}
