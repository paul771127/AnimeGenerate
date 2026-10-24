using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本地圖（Resources/dungeons.txt 的 [node]／[npc]，格式見 DUNGEON_FORMAT.md）：
    ///   ・每個地點打完 → 房主選下一站（只有一條路就自動走），場景換成那個地點的主題；
    ///   ・寶箱地點：每個人各拿一件稀有以下的裝備＋金幣；有史詩以上的大獎時，每個人選「擲骰／放棄」，點數最大的拿走（最大點平手重擲）；
    ///   ・劇情人物：保護目標（倒下＝副本失敗）、抉擇（房主選：友軍／敵人／第三勢力）、任務（完成給獎勵）；
    ///   ・對話窗：劇情台詞、事件、大家說的話（語音辨識的文字），最多 5 行、可全關，文字背後透明。
    /// 連線：房主決定一切（dnode 下一站、dclr 清完、dchoice 抉擇、ndie 人物倒下、dq 任務進度、chest／rollres 寶箱、npcs 人物位置），
    ///       隊友在自己的 DungeonRun 套用同樣的事件；roll（擲骰／放棄）、chat（說的話）大家都會送。
    /// </summary>
    public partial class SoloBattle
    {
        DungeonRun drun;
        /// <summary>單人或房主：決定路線、抉擇、寶箱</summary>
        bool DungeonDecider => !coop || NetIsHost;
        float routeAutoAt;          // 只有一條路：幾秒後自動前進
        bool nodeFightStarted;      // 這個地點的戰鬥開始了（有抉擇時要等選完）
        bool nodeClearedSeen;
        readonly System.Random runRng = new System.Random();
        readonly Dictionary<int, CharacterRig> npcRigs = new Dictionary<int, CharacterRig>();
        readonly Dictionary<int, Vector3> npcLastFeet = new Dictionary<int, Vector3>();

        // ---------------------------------------------------------------- 開始、前往地點
        /// <summary>副本開打（房主、隊友、單人都會叫）：從第一個地點開始</summary>
        void BeginDungeonRun()
        {
            drun = new DungeonRun(dungeon);
            chest = null; chatLog.Clear(); ClearNpcRigs();
            if (DungeonDecider) battle.OnNpcDown += OnNpcDown;
            ApplyNode();
        }

        /// <summary>進到 drun.node 之後：換場景、標題、（房主）開打或開寶箱、叫出劇情人物</summary>
        void ApplyNode()
        {
            var r = drun.node;
            battle.themeOverride = drun.Theme;
            dungeonHalfSaid = -1; nodeClearedSeen = false; routeAutoAt = 0f;
            Say($"🏰 {dungeon.name}・{r.title}（第 {drun.Step} 站）", 3.5f);
            ChatEvent($"📍 來到「{r.title}」");
            Sfx.Play("select", 0.7f, 0.9f);
            nodeFightStarted = false;
            if (!DungeonDecider) { battle.awaitingNext = r.IsChest; return; }   // 隊友：等房主的 dclr／寶箱結果
            if (r.IsChest) { HostOpenChest(); return; }
            if (drun.pendingChoice == null) StartNodeFight();   // 有抉擇：等房主選完才開打
        }

        void StartNodeFight()
        {
            if (nodeFightStarted || drun.node.IsChest) return;
            nodeFightStarted = true;
            SyncRunNpcs();
            battle.EnterNode(drun.node.wave, drun.AtEnd, drun.Step == 1 ? 3f : 4f);
        }

        /// <summary>房主：前往下一站</summary>
        void HostGoNode(DungeonRoom next)
        {
            if (next == null || !DungeonDecider || battle == null || !battle.awaitingNext) return;
            NetSend?.Invoke(new Msg { t = "dnode", k = next.id });
            drun.Enter(next);
            ApplyNode();
        }

        /// <summary>每幀（副本中）：清完地點、台詞、任務獎勵、只有一條路自動前進</summary>
        void UpdateDungeonMap()
        {
            if (drun == null || battle == null || phase != Phase.Fighting) return;
            if (battle.themeOverride != drun.Theme) battle.themeOverride = drun.Theme;
            // 房主：這個地點的戰鬥打完
            if (DungeonDecider && nodeFightStarted && !nodeClearedSeen && battle.awaitingNext && !battle.Over)
            {
                nodeClearedSeen = true;
                NetSend?.Invoke(new Msg { t = "dclr" });
                OnNodeCleared();
            }
            // 房主：只有一條路 → 4 秒後自動前進（寶箱開完、沒有抉擇）
            if (DungeonDecider && battle.awaitingNext && !battle.Over && RouteReady && drun.Exits.Count == 1)
            {
                if (routeAutoAt <= 0f) routeAutoAt = Time.time + 4f;
                else if (Time.time >= routeAutoAt) HostGoNode(drun.Exits[0]);
            }
            DrainRun();
            UpdateChest();
        }

        /// <summary>可以選下一站了：這個地點打完（或寶箱開完）、沒有在等抉擇</summary>
        bool RouteReady => drun != null && !drun.AtEnd && drun.pendingChoice == null && (drun.node.IsChest ? chest != null && chest.done && Time.time >= chest.doneAt + 2.5f : nodeClearedSeen);

        void OnNodeCleared()
        {
            drun.Cleared();
            drun.ThirdLeaves();
            SyncRunNpcs();
            if (!drun.AtEnd) ChatEvent(drun.Exits.Count > 1 ? "🧭 打完了！房主選下一站" : $"🧭 前往「{drun.Exits[0].title}」");
        }

        /// <summary>把 DungeonRun 裡的台詞放進字幕卡與對話窗；任務完成給獎勵</summary>
        void DrainRun()
        {
            while (drun.lines.Count > 0)
            {
                var l = drun.lines.Dequeue();
                ticker.Enqueue(l);
                ChatLine(l);
            }
            foreach (var st in drun.justDone)
            {
                var n = st.def;
                string got = "";
                if (n.rewardGold > 0) { pendingGold += n.rewardGold; got += $"💰 {n.rewardGold}"; }
                if (n.rewardRarity > 0)
                {
                    ItemGen.SetTheme = drun.Theme;
                    var it = ItemGen.Make(dropRng, dungeon.level + battle.levelBoost, n.rewardRarity, myClass);
                    pendingItems.Add(it);
                    got += (got.Length > 0 ? "、" : "") + $"[{Item.RarityNames[n.rewardRarity]}] {it.name}";
                }
                Say($"✅ 任務完成：{n.name}" + (got.Length > 0 ? $"\n獎勵：{got}（通關後獲得）" : ""), 3.5f);
                ChatEvent($"✅ 任務完成（{n.name}）");
                Sfx.Play("charged", 0.6f, 1.2f);
            }
            drun.justDone.Clear();
        }

        // ---------------------------------------------------------------- 劇情人物
        static int SideOf(NpcState st) => st.side == "enemy" ? 1 : st.side == "third" ? 2 : 0;

        /// <summary>房主：場上的人物和 DungeonRun 一致（新登場的叫出來、撤退／安全的移走）</summary>
        void SyncRunNpcs()
        {
            if (!DungeonDecider) return;
            foreach (var st in drun.npcs.Values)
            {
                var f = battle.npcs.Find(x => x.npcId == st.def.id);
                if (st.Fights && f == null) battle.SpawnNpc(st.def.id, st.def.name, st.def.look, SideOf(st), st.side == "escort", st.def.hp);
                else if (!st.Fights && f != null) battle.RemoveNpc(st.def.id);
            }
        }

        /// <summary>房主：人物倒下</summary>
        void OnNpcDown(Fighter f)
        {
            if (drun == null || f.npcId == null || f.tamedKind >= 0 || f.localFriend || f.friendOwner >= 0) return;   // 收服的魔物、影分身不是劇情人物
            NetSend?.Invoke(new Msg { t = "ndie", k = f.npcId });
            NpcDied(f.npcId);
        }

        void NpcDied(string id)
        {
            drun.NpcDied(id);
            if (drun.failed) { battle.FailStage(drun.failText); ChatEvent("💀 " + drun.failText); }
        }

        /// <summary>房主：每 0.1 秒把人物的位置、血量送給隊友（跟 SendBossState 一起）</summary>
        void SendNpcState()
        {
            if (drun == null || !coop || !NetIsHost) return;
            var ns = battle.npcs.FindAll(n => !n.localFriend && n.friendOwner < 0);   // 收服的魔物、影分身另外送（SoloBattle.Friends.cs）
            if (ns.Count == 0 && npcSentCount == 0) return;
            npcSentCount = ns.Count;
            var pts = new Vector3[ns.Count]; var ks = new string[ns.Count];
            for (int i = 0; i < ns.Count; i++) { var n = ns[i]; pts[i] = n.head; ks[i] = $"{n.minionId}|{n.npcId}|{n.npcSide}|{(n.npcEscort ? 1 : 0)}|{Mathf.CeilToInt(n.hp)}|{n.maxHp}"; }
            NetSend?.Invoke(new Msg { t = "npcs", pts = pts, ks = ks });
        }
        int npcSentCount;

        void OnNpcsMsg(Msg m)
        {
            if (drun == null || NetIsHost) return;
            var keep = new HashSet<int>();
            for (int i = 0; m.ks != null && i < m.ks.Length && m.pts != null && i < m.pts.Length; i++)
            {
                var a = m.ks[i].Split('|');
                if (a.Length < 6 || !int.TryParse(a[0], out var id)) continue;
                var def = dungeon.Npc(a[1]);
                int.TryParse(a[2], out var side); int.TryParse(a[4], out var hp); int.TryParse(a[5], out var mhp);
                battle.SyncNpc(id, a[1], def != null ? def.name : a[1], def != null ? def.look : "priest", side, a[3] == "1", m.pts[i], hp, mhp);
                keep.Add(id);
            }
            battle.npcs.RemoveAll(n => !n.localFriend && n.friendOwner < 0 && !keep.Contains(n.minionId));
        }

        /// <summary>人物的角色模型：跟著位置走、倒下</summary>
        void UpdateNpcRigs()
        {
            if (battle == null) return;
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            UpdateTamedRigs(dt);
            foreach (var n in battle.npcs)
            {
                if (n.tamedKind >= 0) continue;
                if (!npcRigs.TryGetValue(n.minionId, out var rig) || !rig)
                {
                    rig = CharacterRig.Create(n.cls != null ? n.cls.id : "priest");
                    npcRigs[n.minionId] = rig; npcLastFeet[n.minionId] = n.Feet;
                    SpawnFade.Play(rig.gameObject);
                }
                rig.SetPose(WorldFrame.FromWorld(n.Feet), WorldFrame.DirFromWorld(n.forward));
                npcLastFeet.TryGetValue(n.minionId, out var lf);
                rig.MoveSpeed = Mathf.Lerp(rig.MoveSpeed, Fighter.Flat(n.Feet - lf).magnitude / dt, 0.2f);
                npcLastFeet[n.minionId] = n.Feet;
                rig.SetDead(!n.Alive);
            }
            foreach (var kv in new List<KeyValuePair<int, CharacterRig>>(npcRigs))
                if (battle.npcs.Find(n => n.minionId == kv.Key) == null) { if (kv.Value) Destroy(kv.Value.gameObject); npcRigs.Remove(kv.Key); npcLastFeet.Remove(kv.Key); }
        }

        // 收服的魔物：用小怪的模型，腳下一圈綠色光環（看得出是自己這邊的）
        readonly Dictionary<int, MinionRig> tamedRigs = new Dictionary<int, MinionRig>();
        void UpdateTamedRigs(float dt)
        {
            foreach (var n in battle.npcs)
            {
                if (n.tamedKind < 0) continue;
                if (!tamedRigs.TryGetValue(n.minionId, out var rig) || !rig)
                {
                    rig = MinionRig.Create();
                    rig.SetVariant(n.tamedKind); rig.SetDress(battle.Theme);
                    var ring = SpellFx.Line(rig.transform, "Tamed ring", SpellFx.Circle(32, 0.45f), true, 0.025f, new Color(0.45f, 1f, 0.35f, 0.8f), true, true);
                    ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                    tamedRigs[n.minionId] = rig; npcLastFeet[n.minionId] = n.Feet;
                    MinionRig.SpawnFx(WorldFrame.FromWorld(n.Feet));
                }
                var pos = WorldFrame.FromWorld(n.Feet);
                var f = WorldFrame.DirFromWorld(n.forward); f.y = 0f;
                npcLastFeet.TryGetValue(n.minionId, out var lf);
                rig.MoveSpeed = Mathf.Lerp(rig.MoveSpeed, Fighter.Flat(n.Feet - lf).magnitude / dt, 0.25f);
                npcLastFeet[n.minionId] = n.Feet;
                rig.transform.SetPositionAndRotation(pos, f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : rig.transform.rotation);
            }
            foreach (var kv in new List<KeyValuePair<int, MinionRig>>(tamedRigs))
                if (battle.npcs.Find(n => n.minionId == kv.Key && n.Alive) == null) { if (kv.Value) { MinionRig.SpawnFx(kv.Value.transform.position); Destroy(kv.Value.gameObject); } tamedRigs.Remove(kv.Key); }
        }

        void ClearNpcRigs()
        {
            foreach (var r in tamedRigs.Values) if (r) Destroy(r.gameObject);
            tamedRigs.Clear();
            foreach (var r in npcRigs.Values) if (r) Destroy(r.gameObject);
            npcRigs.Clear(); npcLastFeet.Clear();
        }

        /// <summary>人物頭上的名字與血條（綠＝我方、紅＝敵人、紫＝第三勢力；保護目標加 🛡）</summary>
        void DrawNpcBars(float W, float H, float lh, int fsS)
        {
            if (battle == null || battle.npcs.Count == 0) return;
            foreach (var n in battle.npcs)
            {
                if (!n.Alive) continue;
                var s = cam.WorldToScreenPoint(WorldFrame.FromWorld(n.head + Vector3.up * 0.35f));
                if (s.z <= 0) continue;
                var col = n.npcSide == 0 ? new Color(0.35f, 0.9f, 0.45f) : n.npcSide == 1 ? new Color(1f, 0.3f, 0.3f) : new Color(0.75f, 0.45f, 1f);
                var r = new Rect(s.x - W * 0.1f, H - s.y, W * 0.2f, lh * 0.3f);
                UiKit.Bar(r, n.hp / Mathf.Max(1f, n.maxHp), col, "", fsS);
                UiKit.Text(new Rect(r.x - W * 0.1f, r.y - lh * 0.75f, r.width + W * 0.2f, lh * 0.7f), (n.npcEscort ? "🛡 " : n.npcSide == 2 ? "⚡ " : "") + n.name, fsS, col, TextAnchor.LowerCenter, true);
            }
        }

        // ---------------------------------------------------------------- 寶箱與擲骰子
        class ChestState
        {
            public int id, rarity, ilvl, seed, theme;
            public bool myChoiceSent, done; public float deadline, doneAt;
            public readonly Dictionary<int, bool> choices = new Dictionary<int, bool>();   // 房主收集：pid → 擲骰／放棄
            public readonly List<string> result = new List<string>();
            public int winner = -2;
        }
        ChestState chest;
        int chestSeq;
        public const float RollTime = 20f;
        int PartyCount => 1 + (coop ? PeerIds.Count : 0);

        /// <summary>房主：開寶箱（決定大獎，送給大家）</summary>
        void HostOpenChest()
        {
            int rar = DiceRoll.ChestRarity(runRng);
            int ilvl = dungeon.level + battle.levelBoost;
            int seed = runRng.Next(1, int.MaxValue);
            int id = ++chestSeq + MyPid * 1000;
            NetSend?.Invoke(new Msg { t = "chest", id = id, e = rar, dmg = ilvl, hp = seed });
            OpenChest(id, rar, ilvl, seed);
            if (drun.OnChest()) SendQuest();
        }

        /// <summary>大家：寶箱打開了 → 每人自己的稀有以下裝備＋金幣；有大獎就準備擲骰</summary>
        void OpenChest(int id, int rar, int ilvl, int seed)
        {
            chest = new ChestState { id = id, rarity = rar, ilvl = ilvl, seed = seed, theme = drun.Theme, deadline = Time.time + RollTime };
            ItemGen.SetTheme = drun.Theme;
            int luk = prof != null ? prof.Total(Stat.Luk) : 0;
            int myRar = Mathf.Min(2, ItemGen.RollRarity(dropRng, luk * 0.3f, 1));
            var mine = ItemGen.Make(dropRng, ilvl, myRar, myClass);
            pendingItems.Add(mine);
            int gold = Mathf.RoundToInt(25 * dungeon.level * (1f + battle.levelBoost / (2f * LevelPerDifficulty)));
            pendingGold += gold;
            Sfx.Play("coin", 0.9f); Sfx.Play("charged", 0.5f, 1.3f); Haptics.Play(Haptics.Kind.Success);
            ChatEvent($"🧰 打開寶箱：[{Item.RarityNames[myRar]}] {mine.name}、💰 {gold}");
            if (rar < DiceRoll.MinRollRarity) { chest.done = true; chest.doneAt = Time.time; return; }
            ChatEvent($"✨ 寶箱裡還有一件 [{Item.RarityNames[rar]}] 裝備！要擲骰嗎？");
            if (PartyCount <= 1)
            {
                // 單人：直接拿
                GrantChestPrize();
                chest.result.Add("只有你一個人，直接拿走！");
                chest.winner = MyPid; chest.done = true; chest.doneAt = Time.time;
            }
        }

        /// <summary>我按了擲骰／放棄</summary>
        void SendRollChoice(bool roll)
        {
            if (chest == null || chest.myChoiceSent || chest.done) return;
            chest.myChoiceSent = true;
            ChatEvent(roll ? "🎲 你選擇擲骰" : "🙅 你放棄了");
            if (NetIsHost || !coop) chest.choices[MyPid] = roll;
            else NetSend?.Invoke(new Msg { t = "roll", id = chest.id, ok = roll });
        }

        /// <summary>房主：大家都選了（或時間到）→ 擲骰、公布結果</summary>
        void UpdateChest()
        {
            if (chest == null || chest.done || !coop || !NetIsHost) return;
            if (chest.rarity < DiceRoll.MinRollRarity) return;
            bool all = chest.choices.Count >= PartyCount;
            if (!all && Time.time < chest.deadline + 2f) return;
            var players = new List<int>();
            foreach (var kv in chest.choices) if (kv.Value) players.Add(kv.Key);
            players.Sort();
            var rounds = new List<List<(int pid, int roll)>>();
            int win = DiceRoll.Resolve(players, () => runRng.Next(1, 101), rounds);
            var lines = DiceRoll.Describe(rounds, PlayerLabel);
            lines.Add(win < 0 ? "沒有人擲骰，寶物留在箱子裡" : $"🏆 {PlayerLabel(win)} 拿到了 [{Item.RarityNames[chest.rarity]}] 裝備！");
            NetSend?.Invoke(new Msg { t = "rollres", id = chest.id, e = win, ks = lines.ToArray() });
            ApplyRollResult(win, lines);
        }

        void ApplyRollResult(int win, IList<string> lines)
        {
            if (chest == null || chest.done) return;
            chest.result.Clear(); chest.result.AddRange(lines);
            chest.winner = win; chest.done = true; chest.doneAt = Time.time + 3f;   // 結果多留 3 秒
            foreach (var l in lines) ChatEvent("🎲 " + l);
            if (win == MyPid) GrantChestPrize();
            else Sfx.Play("select", 0.6f, 0.8f);
        }

        void GrantChestPrize()
        {
            ItemGen.SetTheme = chest.theme;
            var it = ItemGen.Make(new System.Random(chest.seed), chest.ilvl, chest.rarity, myClass);
            pendingItems.Add(it);
            Say($"🏆 擲骰勝利！獲得 [{Item.RarityNames[chest.rarity]}] {it.name}（通關後獲得）", 3.5f);
            Sfx.Play("win", 0.8f, 1.2f); Haptics.Play(Haptics.Kind.Success);
        }

        string PlayerLabel(int pid) => pid == MyPid ? "你" : PeerName(pid);

        void SendQuest()
        {
            if (!coop || !NetIsHost || drun == null) return;
            var ks = new List<string>();
            foreach (var st in drun.Quests) ks.Add($"{st.def.id}|{st.have}");
            NetSend?.Invoke(new Msg { t = "dq", ks = ks.ToArray() });
        }

        /// <summary>房主：打倒敵人的任務進度（每幀用擊倒數的差）</summary>
        void QuestKills(int dk)
        {
            if (drun == null || dk <= 0 || !DungeonDecider) return;
            if (drun.OnKills(dk, runRng)) SendQuest();
        }

        // ---------------------------------------------------------------- 連線訊息
        bool OnDungeonMsg(Msg m)
        {
            if (drun == null) return false;
            switch (m.t)
            {
                case "dnode":
                    if (NetIsHost) return true;
                    { var n = dungeon.Node(m.k); if (n != null && n != drun.node) { drun.Enter(n); ApplyNode(); } }
                    return true;
                case "dclr":
                    if (NetIsHost) return true;
                    nodeClearedSeen = true; battle.awaitingNext = true; OnNodeCleared();
                    return true;
                case "dchoice":
                    if (NetIsHost) return true;
                    drun.Choose(m.e);
                    return true;
                case "ndie":
                    if (NetIsHost) return true;
                    NpcDied(m.k);
                    return true;
                case "dq":
                    if (NetIsHost || m.ks == null) return true;
                    foreach (var s in m.ks) { var a = s.Split('|'); if (a.Length == 2 && int.TryParse(a[1], out var h)) drun.SetProgress(a[0], h); }
                    return true;
                case "npcs": OnNpcsMsg(m); return true;
                case "chest":
                    if (NetIsHost) return true;
                    OpenChest(m.id, m.e, m.dmg, m.hp);
                    return true;
                case "roll":
                    if (NetIsHost && chest != null && chest.id == m.id && !chest.done) chest.choices[m.pid] = m.ok;
                    return true;
                case "rollres":
                    if (NetIsHost) return true;
                    if (chest != null && chest.id == m.id) ApplyRollResult(m.e, m.ks ?? new string[0]);
                    return true;
                case "chat":
                    if (!string.IsNullOrEmpty(m.k)) Chat(PeerName(m.pid), m.k, new Color(0.6f, 0.85f, 1f));
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 對話窗（最多 5 行，可全關，文字背後透明）
        struct ChatEntry { public string who, text; public Color col; }
        readonly List<ChatEntry> chatLog = new List<ChatEntry>();
        const int ChatKeep = 30, ChatMax = 5;
        static int chatLines = -1;
        /// <summary>對話窗顯示幾行（0＝關；存在手機上）</summary>
        public static int ChatLines
        {
            get { if (chatLines < 0) chatLines = Mathf.Clamp(PlayerPrefs.GetInt("sd_chat_lines", 3), 0, ChatMax); return chatLines; }
            set { chatLines = Mathf.Clamp(value, 0, ChatMax); PlayerPrefs.SetInt("sd_chat_lines", chatLines); PlayerPrefs.Save(); }
        }

        void Chat(string who, string text, Color col)
        {
            chatLog.Add(new ChatEntry { who = who, text = text, col = col });
            if (chatLog.Count > ChatKeep) chatLog.RemoveAt(0);
        }
        void ChatEvent(string text) => Chat(null, text, new Color(1f, 0.88f, 0.55f));
        void ChatLine(StoryLine l)
        {
            bool narr = string.IsNullOrEmpty(l.speaker) || l.speaker == "旁白";
            Chat(narr ? null : l.speaker, Story.Fill(l.text, myClass), narr ? new Color(0.85f, 0.85f, 0.8f) : Story.TagColor(l.tag));
        }

        /// <summary>副本中說的話（語音辨識）：放進對話窗、送給隊友</summary>
        void DungeonSpeech(string text)
        {
            if (drun == null || string.IsNullOrEmpty(text)) return;
            Chat("你", text, new Color(0.7f, 1f, 0.7f));
            if (coop) NetSend?.Invoke(new Msg { t = "chat", k = text.Length > 40 ? text.Substring(0, 40) : text });
        }

        /// <summary>對話窗：左下（藥水列上方），只有文字＋陰影；右上角的小按鈕切換行數</summary>
        void DrawChatLog(float W, float H, float pad, int fsS, float bottomY, bool showToggle = true)
        {
            if (drun == null) return;
            float lh = fsS * 1.45f, bw = fsS * 3.6f;
            var tog = new Rect(pad, bottomY - lh * 1.15f, bw, lh * 1.05f);
            if (showToggle && UiKit.Button(tog, ChatLines == 0 ? "💬 關" : $"💬 {ChatLines}", fsS, ChatLines > 0)) ChatLines = (ChatLines + 1) % (ChatMax + 1);
            int n = Mathf.Min(ChatLines, chatLog.Count);
            float y = tog.y - lh * n - pad * 0.3f;
            for (int i = chatLog.Count - n; i < chatLog.Count; i++)
            {
                var c = chatLog[i];
                string s = c.who != null ? $"{c.who}：{c.text}" : c.text;
                var r = new Rect(pad, y, W * 0.58f, lh);   // 左邊 58%：右邊留給任務追蹤
                UiKit.Text(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), s, fsS, new Color(0f, 0f, 0f, 0.85f), TextAnchor.MiddleLeft, true, 0.7f);
                UiKit.Text(r, s, fsS, c.col, TextAnchor.MiddleLeft, true, 0.7f);
                y += lh;
            }
        }

        // ---------------------------------------------------------------- 視窗：路線、抉擇、寶箱、任務
        /// <summary>抉擇／寶箱／選下一站的視窗開著</summary>
        bool DungeonPopupOpen => drun != null && battle != null && phase == Phase.Fighting && !battle.Over &&
            (drun.pendingChoice != null || (chest != null && chest.rarity >= DiceRoll.MinRollRarity && (!chest.done || Time.time < chest.doneAt + 2.5f)) || (battle.awaitingNext && RouteReady));

        void DrawDungeonMapUi(float W, float H, float pad, int fs, int fsS, float questY)
        {
            if (drun == null || battle == null || phase != Phase.Fighting || battle.Over) return;
            // 任務追蹤：右邊、字幕卡下面（左邊是對話窗）
            float qy = questY;
            foreach (var st in drun.Quests)
            {
                var r = new Rect(W * 0.6f, qy, W * 0.4f - pad, fsS * 2.6f);
                UiKit.Card(r, 0.45f, st.done ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.4f), 0.6f);
                UiKit.Text(new Rect(r.x + fsS * 0.4f, r.y, r.width - fsS * 0.8f, r.height * 0.5f), (st.done ? "✅ " : "📜 ") + st.def.name, fsS, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(r.x + fsS * 0.4f, r.y + r.height * 0.45f, r.width - fsS * 0.8f, r.height * 0.5f), st.GoalText, fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, false, 0.6f);
                qy += r.height + pad * 0.3f;
            }

            // 抉擇：房主選；隊友等
            if (drun.pendingChoice != null)
            {
                var st = drun.pendingChoice;
                int k = st.def.options.Count;
                float bh = fs * 2.1f, ch = fs * 3.2f + (bh + pad * 0.4f) * k;
                var r = new Rect(pad, H * 0.5f - ch * 0.5f, W - pad * 2, ch);
                UiKit.Card(r, 0.85f, new Color(0.75f, 0.45f, 1f), 0.9f);
                UiKit.Text(new Rect(r.x + pad, r.y + fs * 0.3f, r.width - pad * 2, fs * 1.4f), $"⚖ {st.def.name}", fs, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(r.x + pad, r.y + fs * 1.6f, r.width - pad * 2, fs * 1.2f), DungeonDecider ? "你決定怎麼做？" : "等房主決定…", fsS, new Color(0.9f, 0.85f, 1f), TextAnchor.MiddleCenter);
                float y = r.y + fs * 3f;
                for (int i = 0; i < k; i++)
                {
                    var o = st.def.options[i];
                    var oc = o.result == "ally" ? new Color(0.4f, 0.9f, 0.5f) : o.result == "enemy" ? new Color(1f, 0.4f, 0.35f) : new Color(0.75f, 0.5f, 1f);
                    if (UiKit.Button(new Rect(r.x + pad, y, r.width - pad * 2, bh), o.text, fsS, false, oc, DungeonDecider) && DungeonDecider)
                    {
                        NetSend?.Invoke(new Msg { t = "dchoice", e = i });
                        drun.Choose(i);
                        ChatEvent($"⚖ 選擇：{o.text}");
                        StartNodeFight();
                    }
                    y += bh + pad * 0.4f;
                }
                return;
            }

            // 寶箱：擲骰／放棄、結果
            if (chest != null && (!chest.done || Time.time < chest.doneAt + 2.5f) && chest.rarity >= DiceRoll.MinRollRarity)
            {
                float ch = fs * 7.5f;
                var r = new Rect(pad, H * 0.42f - ch * 0.5f, W - pad * 2, ch);
                var rc = Item.RarityColors[chest.rarity];
                UiKit.Card(r, 0.85f, rc, 0.95f);
                UiKit.Text(new Rect(r.x + pad, r.y + fs * 0.3f, r.width - pad * 2, fs * 1.4f), $"🧰 寶箱的大獎：[{Item.RarityNames[chest.rarity]}] Lv{chest.ilvl} 裝備", fs, rc, TextAnchor.MiddleCenter, true);
                if (!chest.done)
                {
                    float left = Mathf.Max(0f, chest.deadline - Time.time);
                    UiKit.Text(new Rect(r.x + pad, r.y + fs * 1.8f, r.width - pad * 2, fs * 1.2f), chest.myChoiceSent ? $"等其他人選擇…（{left:0} 秒）" : $"擲骰子（1～100）點數最大的拿走，平手重擲（{left:0} 秒內選）", fsS, Color.white, TextAnchor.MiddleCenter);
                    if (!chest.myChoiceSent)
                    {
                        float bw = (r.width - pad * 3) / 2f, by = r.y + fs * 3.6f, bh = fs * 2.4f;
                        if (UiKit.Button(new Rect(r.x + pad, by, bw, bh), "🎲 擲骰", fs, true, new Color(1f, 0.8f, 0.3f))) SendRollChoice(true);
                        if (UiKit.Button(new Rect(r.x + pad * 2 + bw, by, bw, bh), "🙅 放棄", fs, false, new Color(0.6f, 0.6f, 0.65f))) SendRollChoice(false);
                        if (left <= 0f) SendRollChoice(false);   // 時間到＝放棄
                    }
                }
                else
                {
                    float y = r.y + fs * 1.9f;
                    foreach (var l in chest.result) { UiKit.Text(new Rect(r.x + pad, y, r.width - pad * 2, fsS * 1.4f), l, fsS, Color.white, TextAnchor.MiddleCenter, false, 0.6f); y += fsS * 1.4f; }
                }
                return;
            }

            // 選下一站
            if (battle.awaitingNext && RouteReady)
            {
                var ex = drun.Exits;
                float bh = fs * 2.6f, ch = fs * 2.2f + (bh + pad * 0.4f) * (DungeonDecider ? ex.Count : 0) + (DungeonDecider ? 0f : fs * 1.4f);
                var r = new Rect(pad, H * 0.58f - ch * 0.5f, W - pad * 2, ch);
                UiKit.Card(r, 0.7f, new Color(0.5f, 0.8f, 1f), 0.8f);
                string head = DungeonDecider ? (ex.Count > 1 ? "🧭 選擇下一站" : $"🧭 {Mathf.Max(0f, routeAutoAt - Time.time):0} 秒後前進") : "🧭 等房主選擇下一站…";
                UiKit.Text(new Rect(r.x + pad, r.y + fs * 0.3f, r.width - pad * 2, fs * 1.5f), head, fs, Color.white, TextAnchor.MiddleCenter, true);
                if (!DungeonDecider) return;
                float y = r.y + fs * 2f;
                foreach (var nx in ex)
                {
                    string what = nx.IsChest ? "🧰 寶箱" : nx.IsBoss ? "💀 首領" : $"⚔ 戰鬥（第 {nx.wave} 波強度）";
                    if (UiKit.Button(new Rect(r.x + pad, y, r.width - pad * 2, bh), $"{nx.title}\n{what}", fsS, false, nx.IsBoss ? new Color(1f, 0.4f, 0.3f) : nx.IsChest ? new Color(1f, 0.85f, 0.3f) : new Color(0.5f, 0.8f, 1f)))
                    { HostGoNode(nx); return; }
                    y += bh + pad * 0.4f;
                }
            }
        }
    }
}
