using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事模式的多人合作：
    ///   ・房主在關卡的整備畫面按「👥 邀請隊友」→ 選附近連線或遠距連線 → 進連線大廳（模式＝故事，關卡、難度跟房主）。
    ///   ・隊友加入、選好技能按「準備好了」；房主按「開始」大家一起開打（同一關、同一個難度）。
    ///   ・高度以房主為準：隊友的手機站姿高度校正到和房主一樣（地板跟著移），敵人在每個人眼中的高度都一樣。
    ///   ・掉落：史詩（紫）以上的裝備、精魄由房主擲出來，大家選「擲骰／放棄」，點數最大的拿走；稀有以下每個人各自拿自己的。
    ///   ・只有房主的故事進度會推進（通關紀錄、劇情、第一次通關的流程）；隊友拿得到經驗、金幣、戰利品。
    ///   ・合作時畫面右上角：每位隊友的頭像、名字、血量、每秒傷害、每秒治療（每個人每秒把自己的數字送給大家）。
    /// 訊息：sstart（房主開打：k 關卡、e 難度、s 房主站姿高度）、pst（每秒：dmg 每秒傷害、hp 每秒治療）、
    ///       lroll（房主擲出寶物：id、e 稀有度（−1＝精魄）、dmg 物品等級、hp 種子、k 精魄 id、a 主題）、lpick（我的選擇：ok 擲骰）、lres（結果：e 贏家 pid、ks 經過）。
    /// </summary>
    public partial class SoloBattle
    {
        bool netStory; string netStoryId;
        /// <summary>故事出怪：每人場上最多 5 隻、打倒後 5 秒補滿；第二章以後的 Boss ×1.3</summary>
        public const int StoryMaxAlive = 5; public const float StoryRefillDelay = 5f, LateBossMul = 1.3f;
        /// <summary>首頁／村子：故事關卡的整備畫面按「邀請隊友」（GameRoot 進連線大廳）</summary>
        public System.Action<bool> RequestNetStory;
        StoryStage prepStage;   // 整備畫面是哪一關（邀請隊友用）
        bool inviteAsk;         // 「邀請隊友」：選附近連線／遠距連線

        /// <summary>邀請隊友時選的關卡與難度（GameRoot 回首頁、進大廳後再設回來）</summary>
        public string InviteStageId => netStoryId;
        public int InviteDifficulty => difficulty;
        /// <summary>新手關（1-1～1-3、轉職試煉）不開放邀請：有教學流程</summary>
        static bool IsGuideStage(StoryStage st) => st == null || st.id == "1-1" || st.id == "1-2" || st.id == "1-3" || st.id == "trial" || st.id == JobTrialStageId;

        /// <summary>連線大廳：預設故事合作模式（關卡、難度跟房主）</summary>
        public void PresetNetStory(string stageId, int diff)
        {
            coop = true; netEndless = false; netDungeon = false; netStory = true; netStoryId = stageId; difficulty = Mathf.Clamp(diff, 0, 2); localReady = false;
        }

        /// <summary>這一場是故事合作（掉落要擲骰、只有房主的進度會推進）</summary>
        bool StoryCoop => NetMode && coop && storyStage != null && dungeon == null;

        // ---------------------------------------------------------------- 整備畫面：邀請隊友
        /// <summary>整備畫面的「👥 邀請隊友」：回傳用掉的高度</summary>
        float DrawInviteButton(float x, float y, float w, float u, int fsS)
        {
            if (prepStage == null || NetMode || RequestNetStory == null) return 0f;
            var col = new Color(0.4f, 0.85f, 0.75f);
            if (!inviteAsk)
            {
                if (UiKit.Button(new Rect(x, y, w, u * 1.1f), "👥 邀請隊友一起打（附近／遠距連線）", fsS, false, col)) inviteAsk = true;
                return u * 1.1f;
            }
            float gap = u * 0.25f, hw = (w - gap * 2) / 3f;
            if (UiKit.Button(new Rect(x, y, hw, u * 1.1f), "📡 附近連線", fsS, true, col)) GoInvite(false);
            if (UiKit.Button(new Rect(x + hw + gap, y, hw, u * 1.1f), "🌐 遠距連線", fsS, true, col)) GoInvite(true);
            if (UiKit.Button(new Rect(x + (hw + gap) * 2, y, hw, u * 1.1f), "取消", fsS)) inviteAsk = false;
            UiKit.Text(new Rect(x, y + u * 1.15f, w, u * 1.3f), "附近：大家在同一個空間、同一個 Wi-Fi；遠距：各自在家，用房間代碼加入。只有你（房主）的故事進度會推進。", Mathf.RoundToInt(fsS * 0.9f), new Color(0.85f, 0.92f, 1f), TextAnchor.UpperLeft);
            return u * 2.5f;
        }

        void GoInvite(bool remote)
        {
            inviteAsk = false;
            var st = prepStage; prepGo = null; prepStage = null;
            netStoryId = st.id; netStory = true;
            RequestNetStory?.Invoke(remote);   // GameRoot：回首頁 → 連線大廳 → PresetNetStory
        }

        // ---------------------------------------------------------------- 連線大廳：隊伍畫面（房主開始）
        void DrawStoryParty(ref float y, float pad, float gap, float u, int fs, int fsS, float W)
        {
            var st = Story.Find(netStoryId ?? "");
            int n = PeerIds.Count + 1;
            var col = new Color(0.4f, 0.85f, 0.75f);
            float rowH = u * 0.95f;
            var card = new Rect(pad, y, W - pad * 2, u * 1.1f + rowH * Mathf.Max(n, 2) + u * 2.6f);
            UiKit.Card(card, 0.7f, col, 0.8f);
            float x = card.x + gap, w = card.width - gap * 2, cy = card.y + gap * 0.4f;
            UiKit.Text(new Rect(x, cy, w, u), $"📖 {(st != null ? st.id + " " + st.title : "故事關卡")}（{n} 人）・{DiffNames[Mathf.Clamp(difficulty, 0, 2)]}", fs, Color.white, TextAnchor.MiddleLeft, true);
            cy += u * 1.05f;
            UiKit.Text(new Rect(x, cy, w, rowH), $"⭐ 你{(NetIsHost ? "（房主）" : "")}：{Skills.Classes[myClass].name}　" + (NetIsHost ? "由你開始" : localReady ? "✔ 已準備" : "還沒準備"), fsS, NetIsHost || localReady ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f));
            cy += rowH;
            foreach (var pid in PeerIds)
            {
                bool rr = peerInfo.TryGetValue(pid, out var pi) && pi.ready;
                string cn = pi != null && Skills.Classes.TryGetValue(pi.cls ?? "", out var rc) ? rc.name : "（選職業中）";
                bool hostRow = pid == 0;
                UiKit.Text(new Rect(x, cy, w, rowH), $"👤 {PeerName(pid)}：{cn}　" + (hostRow ? "等你準備好後開始" : rr ? "✔ 已準備" : "還沒準備"), fsS, hostRow || rr ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f));
                cy += rowH;
            }
            if (n < 2) { UiKit.Text(new Rect(x, cy, w, rowH), "　（等隊友加入：把房間代碼傳給朋友）", fsS, UiKit.Muted); cy += rowH; }
            string hint = NetIsHost
                ? (PeerIds.Count == 0 ? "等待隊友加入…（一個人也可以直接開始）" : StoryPartyReady ? "大家都準備好了：按下方「開始」" : "等所有人按「準備好了」，再由你按「開始」")
                : localReady ? "已準備：等房主按開始（關卡與難度跟房主一樣）" : "選好技能後按下方「準備好了」";
            hint += "\n📌 只有房主的故事進度會推進；史詩以上的裝備和精魄要擲骰決定（可以放棄）。高度以房主為準。";
            UiKit.Text(new Rect(x, cy + gap * 0.3f, w, u * 2.4f), hint, fsS, new Color(0.9f, 0.95f, 1f), TextAnchor.UpperLeft);
            y = card.yMax + gap;
        }
        static readonly string[] DiffNames = { "簡單", "普通", "困難" };

        bool StoryPartyReady => PeerIds.TrueForAll(pid => peerInfo.TryGetValue(pid, out var pi) && pi.ready && pi.mode == 4);

        /// <summary>房主按「開始」：先送自己的職業與技能，再通知大家一起開打</summary>
        void HostStartStory()
        {
            if (!NetIsHost || !(coop && netStory) || !StoryPartyReady) return;
            var st = Story.Find(netStoryId ?? "");
            if (st == null) { Say("找不到這一關", 2f); return; }
            SaveChoices();
            NetSend?.Invoke(new Msg { t = "ready", k = myClass, ks = myLoadout.ToArray(), e = NetModeCode, a = Progress.Char(myClass).adv ?? "", g = MyGearCode, hp = MyMaxHp });
            NetSend?.Invoke(new Msg { t = "sstart", k = st.id, e = difficulty, s = StandH });
            localReady = remoteReady = false;
            foreach (var pi in peerInfo.Values) pi.ready = false;
            StartStoryCoop(st);
        }

        /// <summary>大家：開打故事關卡（合作；劇情改成戰鬥中的字幕卡，大家都看得到）</summary>
        void StartStoryCoop(StoryStage st)
        {
            if (st == null) return;
            storyMapOpen = false; storyStage = st; pendingDungeon = null;
            heightSynced = NetIsHost; lootRolls.Clear(); partyStats.Clear(); myDealtHist.Clear(); myHealHist.Clear();
            pendingStage = st;
            StartBossBattle(true, true);
            pendingStage = null;
            Ticker(st.before);
            Say($"📖 {st.title}（{PeerIds.Count + 1} 人合作）", 3f);
        }

        /// <summary>故事合作的訊息；處理了回傳 true</summary>
        bool OnNetStoryCoop(Msg m)
        {
            switch (m.t)
            {
                case "sstart":
                {
                    if (NetIsHost) return true;
                    var st = Story.Find(m.k ?? "");
                    if (st == null) { Say("房主開的關卡你的版本沒有，請更新遊戲", 3f); return true; }
                    netStoryId = st.id; netStory = true; coop = true; netDungeon = false; netEndless = false;
                    difficulty = Mathf.Clamp(m.e, 0, 2); hostStandH = m.s;
                    localReady = remoteReady = false;
                    foreach (var pi0 in peerInfo.Values) pi0.ready = false;
                    StartStoryCoop(st);
                    return true;
                }
                case "ewarn":
                    if (!NetIsHost && battle != null && coop) battle.RemoteEliteWarn(m.id, m.k, m.p, m.d, m.s);
                    return true;
                case "spx":   // 稀有怪出招（主機 → 隊友）：在我這台重現震環／橫掃，只判定我自己
                    if (!NetIsHost && battle != null && coop) battle.RemoteSpecialStrike(m.id, m.k, m.p, m.d, m.dmg);
                    return true;
                case "dmgn":
                    // 隊友打出的傷害數字（主機本來就看得到大家的命中，不重複）
                    if (!NetIsHost && battle != null && m.dmg > 0)
                    {
                        var col = peerInfo.TryGetValue(m.pid, out var dpi) && Skills.Classes.TryGetValue(dpi.cls ?? "", out var dcls) ? Color.Lerp(dcls.color, Color.white, 0.35f) : new Color(0.85f, 0.9f, 1f);
                        floaters.Add(new Floater { posMap = m.p + Vector3.up * 0.15f, text = (m.ok ? "💥" : "") + m.dmg, color = col, born = Time.time });
                    }
                    return true;
                case "pst":
                    partyStats[m.pid] = (m.dmg, m.hp);
                    return true;
                case "lroll":
                    OpenLootRoll(m.id, m.e, m.dmg, m.hp, m.k, int.TryParse(m.a, out var th) ? th : 0);
                    return true;
                case "lpick":
                    if (NetIsHost) { var lr = lootRolls.Find(x => x.id == m.id); if (lr != null) lr.choices[m.pid] = m.ok; }
                    return true;
                case "lres":
                    ApplyLootResult(m.id, m.e, m.ks);
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 高度：以房主為準
        float hostStandH = -1f; bool heightSynced;
        /// <summary>隊友：我的站姿高度量好了 → 地板移到讓我的站姿高度＝房主的（敵人在大家眼中一樣高）</summary>
        void UpdateHeightSync()
        {
            if (heightSynced || !StoryCoop || NetIsHost || phase != Phase.Fighting) return;
            if (NetPeers.TryGetValue(0, out var hp) && hp.standH > 0.5f) hostStandH = hp.standH;   // 房主每 0.05 秒送一次站姿高度
            if (hostStandH < 0.5f || standH < 0.5f) return;
            heightSynced = true;
            float d = standH - hostStandH;
            if (Mathf.Abs(d) < 0.03f || RequestRelevel == null) return;
            RequestRelevel(d);
            foreach (var sc in new Component[] { volcano, forest, sea, arenaScene, canyon }) if (sc) sc.transform.position += Vector3.up * d;
            standH -= d; standY -= d; lastHeadY = float.NaN;
            if (readyStand > 0f) { readyStand -= d; readyMin -= d; }
            Say($"📏 高度以房主為準：地板{(d > 0 ? "往上" : "往下")}移了 {Mathf.Abs(d) * 100f:0} 公分", 2.5f);
        }

        // ---------------------------------------------------------------- 每秒傷害／每秒治療
        readonly Dictionary<int, (int dps, int hps)> partyStats = new Dictionary<int, (int, int)>();
        readonly Queue<(float t, float v)> myDealtHist = new Queue<(float, float)>(), myHealHist = new Queue<(float, float)>();
        float lastDealt, lastHealed, nextPstAt;
        public const float StatWindow = 10f;   // 每秒傷害／治療：最近 10 秒的平均
        int myDps, myHps;

        void UpdatePartyStats()
        {
            if (battle == null || Me == null || !coop || phase != Phase.Fighting) return;
            float now = Time.time;
            if (Me.dealtDone < lastDealt) lastDealt = 0f;   // 新的一場
            if (Me.healDone < lastHealed) lastHealed = 0f;
            if (Me.dealtDone > lastDealt) { myDealtHist.Enqueue((now, Me.dealtDone - lastDealt)); lastDealt = Me.dealtDone; }
            if (Me.healDone > lastHealed) { myHealHist.Enqueue((now, Me.healDone - lastHealed)); lastHealed = Me.healDone; }
            while (myDealtHist.Count > 0 && myDealtHist.Peek().t < now - StatWindow) myDealtHist.Dequeue();
            while (myHealHist.Count > 0 && myHealHist.Peek().t < now - StatWindow) myHealHist.Dequeue();
            if (now < nextPstAt) return;
            nextPstAt = now + 1f;
            float sd = 0f, sh = 0f; foreach (var x in myDealtHist) sd += x.v; foreach (var x in myHealHist) sh += x.v;
            myDps = Mathf.RoundToInt(sd / StatWindow); myHps = Mathf.RoundToInt(sh / StatWindow);
            NetSend?.Invoke(new Msg { t = "pst", dmg = myDps, hp = myHps });
        }

        /// <summary>隊伍框的高度（自己＋每位隊友一行）</summary>
        float PartyFrameH(float lh) => battle == null ? 0f : (battle.allies.Count + 1) * (lh * 1.25f + lh * 0.15f);

        /// <summary>每幀：房主在隊伍畫面廣播模式（後加入的隊友也知道是哪一關）、高度同步、每秒傷害／治療、擲骰</summary>
        float nextStoryBeacon;
        void UpdateStoryCoop()
        {
            if (!NetMode) return;
            if (NetIsHost && coop && netStory && phase != Phase.Fighting && Time.time >= nextStoryBeacon)
            {
                nextStoryBeacon = Time.time + 2f;
                NetSend?.Invoke(new Msg { t = "mode", e = 4, k = netStoryId ?? "", hp = difficulty });
            }
            UpdateHeightSync();
            UpdatePartyStats();
            UpdateLootRolls();
        }

        /// <summary>合作：畫面右上角的隊伍（自己＋每位隊友：頭像、名字、血量、每秒傷害、每秒治療）。回傳畫了多高</summary>
        float DrawPartyFrame(float top, float W, float lh, int fsS, Fighter en)
        {
            if (battle == null || !coop) return 0f;
            float gap = lh * 0.15f, rowH = lh * 1.25f, w = W * 0.5f, x = W - w - W * 0.02f, y = top;
            void Row(string name, string cls, string adv, Color col, float hp, float maxHp, bool alive, bool targeted, int dps, int hps)
            {
                var r = new Rect(x, y, w, rowH);
                UiKit.Card(r, 0.55f, col, 0.6f);
                float fsz = rowH - gap * 2;
                var fr = new Rect(r.x + gap, r.y + gap, fsz, fsz);
                if (!Story2D.DrawFace(fr, Story2D.ClassPortrait(cls, adv), col)) UiKit.Disc(fr, col);
                if (!alive) UiKit.Text(fr, "💀", fsS, Color.white, TextAnchor.MiddleCenter);
                float tx = fr.xMax + gap, tw = r.xMax - tx - gap;
                UiKit.Text(new Rect(tx, r.y, tw, rowH * 0.42f), (targeted ? "🎯 " : "") + name, Mathf.RoundToInt(fsS * 0.85f), alive ? Color.white : Color.gray, TextAnchor.MiddleLeft, true);
                UiKit.Bar(new Rect(tx, r.y + rowH * 0.42f, tw, rowH * 0.26f), maxHp > 0f ? hp / maxHp : 0f, new Color(0.3f, 0.85f, 0.45f), $"{Mathf.CeilToInt(hp)}", Mathf.RoundToInt(fsS * 0.75f));
                UiKit.Text(new Rect(tx, r.y + rowH * 0.68f, tw, rowH * 0.32f), $"⚔ {dps}/秒　✚ {hps}/秒", Mathf.RoundToInt(fsS * 0.75f), new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleLeft);
                y += rowH + gap;
            }
            var me = Me;
            Row("你", myClass, Progress.Char(myClass).adv, Skills.Classes[myClass].color, me.hp, me.maxHp, me.Alive, en != null && en.target == me, myDps, myHps);
            foreach (var al in battle.allies)
            {
                peerInfo.TryGetValue(al.pid, out var pi);
                partyStats.TryGetValue(al.pid, out var ps);
                Row(al.name, al.cls.id, pi?.adv, al.cls.color, al.hp, al.maxHp, al.Alive, en != null && en.target == al, ps.dps, ps.hps);
            }
            return y - top;
        }

        // ---------------------------------------------------------------- 擲骰：史詩以上的裝備、精魄
        class LootRoll
        {
            public int id, rarity, ilvl, seed, theme; public string soul;
            public bool myChoiceSent, done; public float deadline, doneAt; public int winner = -2;
            public readonly Dictionary<int, bool> choices = new Dictionary<int, bool>();
            public readonly List<string> result = new List<string>();
            public string Label => soul != null ? $"💠 {Souls.Get(soul)?.name ?? soul}" : $"[{Item.RarityNames[Mathf.Clamp(rarity, 0, Item.RarityNames.Length - 1)]}] 裝備（Lv{ilvl}）";
        }
        readonly List<LootRoll> lootRolls = new List<LootRoll>();
        int lootSeq;
        public const float LootRollTime = 20f;

        /// <summary>故事合作：這件掉落要擲骰嗎（史詩以上）。房主：開一個擲骰（回傳 true＝不放進自己的戰利品）；隊友：降成稀有（大獎只由房主擲出來）</summary>
        bool CoopRollItem(ref int rar, int ilvl, int theme)
        {
            if (!StoryCoop || rar < DiceRoll.MinRollRarity) return false;
            if (!NetIsHost) { rar = DiceRoll.MinRollRarity - 1; return false; }
            HostLootRoll(rar, ilvl, null, theme);
            return true;
        }

        /// <summary>故事合作：精魄掉落。房主：開一個擲骰；隊友：不擲（只由房主擲）。回傳 true＝已處理（不要放進自己的戰利品）</summary>
        bool CoopRollSoul(string soulId)
        {
            if (!StoryCoop) return false;
            if (NetIsHost) HostLootRoll(-1, 0, soulId, battle != null ? battle.Theme : 0);
            return true;
        }

        void HostLootRoll(int rar, int ilvl, string soul, int theme)
        {
            int id = ++lootSeq;
            int seed = dropRng.Next(1, int.MaxValue);
            NetSend?.Invoke(new Msg { t = "lroll", id = id, e = rar, dmg = ilvl, hp = seed, k = soul ?? "", a = theme.ToString() });
            OpenLootRoll(id, rar, ilvl, seed, soul, theme);
        }

        void OpenLootRoll(int id, int rar, int ilvl, int seed, string soul, int theme)
        {
            if (lootRolls.Exists(x => x.id == id)) return;
            var lr = new LootRoll { id = id, rarity = rar, ilvl = ilvl, seed = seed, soul = string.IsNullOrEmpty(soul) ? null : soul, theme = theme, deadline = Time.time + LootRollTime };
            lootRolls.Add(lr);
            Sfx.Play("win", 0.7f, 1.2f); Haptics.Play(Haptics.Kind.Success);
            if (PeerIds.Count == 0)
            {
                // 只有自己：直接拿
                lr.result.Add("只有你一個人，直接拿走！");
                lr.done = true; lr.doneAt = Time.time; lr.winner = MyPid;
                GrantLootRoll(lr);
            }
        }

        void SendLootChoice(LootRoll lr, bool roll)
        {
            if (lr == null || lr.myChoiceSent || lr.done) return;
            lr.myChoiceSent = true;
            if (NetIsHost) lr.choices[MyPid] = roll;
            else NetSend?.Invoke(new Msg { t = "lpick", id = lr.id, ok = roll });
            Say(roll ? "🎲 你選擇擲骰" : "🙅 你放棄了", 1.2f);
        }

        /// <summary>房主：大家都選了（或時間到）→ 擲骰、公布結果</summary>
        void UpdateLootRolls()
        {
            for (int i = lootRolls.Count - 1; i >= 0; i--)
            {
                var lr = lootRolls[i];
                if (lr.done && Time.time > lr.doneAt + 3f) { lootRolls.RemoveAt(i); continue; }
                if (lr.done || !NetIsHost) continue;
                bool all = lr.choices.Count >= PeerIds.Count + 1;
                if (!all && Time.time < lr.deadline + 2f) continue;
                var players = new List<int>();
                foreach (var kv in lr.choices) if (kv.Value) players.Add(kv.Key);
                players.Sort();
                var rounds = new List<List<(int pid, int roll)>>();
                int win = DiceRoll.Resolve(players, () => dropRng.Next(1, 101), rounds);
                var lines = DiceRoll.Describe(rounds, PlayerLabel);
                lines.Add(win < 0 ? "沒有人擲骰，寶物消失了" : $"🏆 {PlayerLabel(win)} 拿到了 {lr.Label}！");
                NetSend?.Invoke(new Msg { t = "lres", id = lr.id, e = win, ks = lines.ToArray() });
                ApplyLootResult(lr.id, win, lines.ToArray());
            }
        }

        void ApplyLootResult(int id, int win, string[] lines)
        {
            var lr = lootRolls.Find(x => x.id == id);
            if (lr == null || lr.done) return;
            lr.result.Clear();
            if (lines != null) foreach (var l in lines) lr.result.Add(l.Replace(PeerName(MyPid), "你"));
            lr.winner = win; lr.done = true; lr.doneAt = Time.time;
            if (win == MyPid) GrantLootRoll(lr);
            else Sfx.Play("select", 0.6f, 0.8f);
        }

        void GrantLootRoll(LootRoll lr)
        {
            if (lr.soul != null)
            {
                var d = Souls.Get(lr.soul);
                if (d == null) return;
                pendingSouls.Add(d.id);
                Say($"🏆 擲骰勝利！獲得精魄「{d.name}」（過關後獲得）", 3.5f);
            }
            else
            {
                ItemGen.SetTheme = lr.theme;
                var it = ItemGen.Make(new System.Random(lr.seed), lr.ilvl, lr.rarity, myClass);
                if (storyStage != null && battle != null) { var (blo, bhi) = Story.LevelBand(storyStage.theme); it.reqLevel = Mathf.Clamp(it.reqLevel, blo + battle.levelBoost, bhi + battle.levelBoost); }
                pendingItems.Add(it);
                Say($"🏆 擲骰勝利！獲得 [{Item.RarityNames[lr.rarity]}] {it.name}（過關後獲得）", 3.5f);
            }
            Sfx.Play("win", 0.8f, 1.2f); Haptics.Play(Haptics.Kind.Success);
        }

        /// <summary>擲骰視窗（畫面中下方）：一次顯示最前面的一件</summary>
        void DrawLootRoll(float W, float H, int fs, int fsS)
        {
            if (lootRolls.Count == 0 || phase != Phase.Fighting) return;
            var lr = lootRolls[0];
            float w = W * 0.9f, h = fs * 6.4f, pad = fs * 0.5f;
            var r = new Rect((W - w) / 2f, H * 0.52f, w, h);
            var col = lr.soul != null ? (Souls.Get(lr.soul)?.color ?? Color.cyan) : Item.RarityColors[Mathf.Clamp(lr.rarity, 0, Item.RarityColors.Length - 1)];
            UiKit.Card(r, 0.92f, col, 1f);
            UiKit.Text(new Rect(r.x + pad, r.y + pad * 0.5f, r.width - pad * 2, fs * 1.4f), $"✨ 掉落 {lr.Label}" + (lootRolls.Count > 1 ? $"（還有 {lootRolls.Count - 1} 件）" : ""), fs, col, TextAnchor.MiddleCenter, true);
            if (lr.done)
            {
                string res = lr.result.Count > 0 ? lr.result[lr.result.Count - 1] : "";
                UiKit.Text(new Rect(r.x + pad, r.y + fs * 1.9f, r.width - pad * 2, h - fs * 2.2f), res, fsS, Color.white, TextAnchor.UpperCenter);
                return;
            }
            float left = Mathf.Max(0f, lr.deadline - Time.time);
            UiKit.Text(new Rect(r.x + pad, r.y + fs * 1.8f, r.width - pad * 2, fs * 1.2f), lr.myChoiceSent ? $"等其他人選擇…（{left:0} 秒）" : $"擲骰子（1～100）點數最大的拿走，平手重擲（{left:0} 秒內選）", fsS, Color.white, TextAnchor.MiddleCenter);
            if (!lr.myChoiceSent)
            {
                float bw = (r.width - pad * 3) / 2f, bh = fs * 2f, by = r.yMax - pad - bh;
                if (UiKit.Button(new Rect(r.x + pad, by, bw, bh), "🎲 擲骰", fs, true, new Color(1f, 0.8f, 0.3f))) SendLootChoice(lr, true);
                if (UiKit.Button(new Rect(r.x + pad * 2 + bw, by, bw, bh), "🙅 放棄", fs)) SendLootChoice(lr, false);
            }
        }
    }
}
