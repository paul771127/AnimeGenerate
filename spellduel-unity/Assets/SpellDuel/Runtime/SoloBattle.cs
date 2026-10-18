using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 單人對戰的畫面與操作（邏輯在 Battle / EnemyAI，這裡只負責顯示與輸入）。
    ///   開戰前：選我的職業與 3 個技能、敵人職業（可隨機）。
    ///   戰鬥中：點技能按鈕開始詠唱 → 蓄力完成後點畫面朝該方向發射（陷阱設在點到的地板）。
    ///   敵人、法術、陷阱都放在場地座標裡，用 AR 相機看到的是它們在房間裡的真實位置。
    /// </summary>
    public partial class SoloBattle : MonoBehaviour
    {
        public enum Phase { Hidden, Setup, Fighting, Over }
        public Phase phase { get; private set; } = Phase.Hidden;
        public Action RequestRedraw, RequestChangeMode;
        public HandGesture Hand;          // 手勢（GameRoot 每幀更新）

        // ---------------------------------------------------------------- 雙人連線模式（由 GameRoot 設定）
        // 對手是真人：沒有 AI、沒有角色模型；對手的位置由 GameRoot 每幀提供（鏡頭偵測＋對手回報融合），
        // 判定採「攻擊方判定」：我的法術打到對手 → 送 hit2，對手自己扣血、套用格擋／護盾／反擊。
        public bool NetMode;
        /// <summary>遠端連線（不同地方）：對手／隊友不在鏡頭裡 → 用職業角色顯示他們的位置</summary>
        public bool RemotePlay;
        readonly Dictionary<int, CharacterRig> allyRigs = new Dictionary<int, CharacterRig>();
        readonly Dictionary<int, Vector3> allyLastFeet = new Dictionary<int, Vector3>();

        /// <summary>遠端合作：每位隊友的虛擬角色跟著他回報的位置走</summary>
        void UpdateAllyRigs()
        {
            if (!RemotePlay || battle == null) return;
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            foreach (var a in battle.allies)
            {
                if (!allyRigs.TryGetValue(a.pid, out var rig) || !rig)
                {
                    rig = CharacterRig.Create(a.cls != null ? a.cls.id : "mage", a.adv);   // 進階職業有自己的外觀
                    allyRigs[a.pid] = rig; allyLastFeet[a.pid] = a.Feet;
                }
                rig.SetPose(WorldFrame.FromWorld(a.Feet), WorldFrame.DirFromWorld(a.forward));
                allyLastFeet.TryGetValue(a.pid, out var lf);
                rig.MoveSpeed = Mathf.Lerp(rig.MoveSpeed, Fighter.Flat(a.Feet - lf).magnitude / dt, 0.2f);
                allyLastFeet[a.pid] = a.Feet;
                rig.SetDead(!a.Alive);
            }
            foreach (var kv in new List<KeyValuePair<int, CharacterRig>>(allyRigs))
                if (battle.AllyByPid(kv.Key) == null) { if (kv.Value) Destroy(kv.Value.gameObject); allyRigs.Remove(kv.Key); }
        }
        public Action<Msg> NetSend;
        public Func<double> SharedClock;
        public bool NetHasTarget, NetLock;
        public Vector3 NetEnemyHeadW, NetEnemyFwdW;
        public Rect NetLockRect;
        bool localReady, remoteReady;
        string remoteClass; string[] remoteLoadout;
        // 多人（2～4 人合作）：我的編號、房間裡其他人的編號與位置（GameRoot 設定）、每個人的準備狀態
        public int MyPid;
        public readonly List<int> PeerIds = new List<int>();
        public class PeerPose { public Vector3 head, fwd; }
        public readonly Dictionary<int, PeerPose> NetPeers = new Dictionary<int, PeerPose>();
        class PeerInfo { public string cls, adv; public string[] loadout; public bool ready; public int mode; }
        readonly Dictionary<int, PeerInfo> peerInfo = new Dictionary<int, PeerInfo>();
        const int IdSpace = 1000000;                         // 遠端法術／陷阱的編號：發送者 pid × IdSpace ＋ 對方的編號（多人時不會撞號）
        static int RId(int pid, int id) => pid * IdSpace + id;
        void SendTo(int pid, Msg m) { m.to = pid + 1; NetSend?.Invoke(m); }

        /// <summary>進階職業的連線同步：召喚物（給別人顯示）、守護誓言（把傷害轉給聖騎士）</summary>
        void WireAdvNet()
        {
            battle.OnPetSpawn += p => NetSend?.Invoke(new Msg { t = "pet", id = p.id, k = p.kind, p = p.pos, s = p.until - battle.now });
            battle.OnPetGone += p => NetSend?.Invoke(new Msg { t = "petgone", id = p.id });
            battle.OnOathShare += (by, amt) => { if (by.isAlly) SendTo(by.pid, new Msg { t = "oath", dmg = Mathf.RoundToInt(amt) }); };
        }
        /// <summary>這位玩家在訊息裡的代碼：0＝沒有、否則 pid＋1（主機用來告訴大家 Boss／小怪在打誰）</summary>
        int TargetCode(Fighter f) => f == null ? 0 : f == Me ? MyPid + 1 : f.isAlly ? f.pid + 1 : 0;
        Fighter FromTargetCode(int code) => code <= 0 ? null : code - 1 == MyPid ? Me : battle?.AllyByPid(code - 1);
        /// <summary>GameRoot：有人離開房間</summary>
        public void OnPeerLeft(int pid)
        {
            peerInfo.Remove(pid); NetPeers.Remove(pid);
            var a = battle?.AllyByPid(pid);
            if (a != null && a.Alive) { a.hp = 0f; Say($"⚠ {a.name} 斷線了", 3f); }
        }
        float nextState;
        Transform remoteAnchor;           // 對手位置的空物件（光效跟著它）
        public bool NetIsHost;            // 這支手機是主機（合作模式由主機跑 Boss）

        // ---------------------------------------------------------------- Boss 模式（單人，或雙人合作）
        // 合作：主機跑 Boss AI 與 Boss 血量，定時送 "boss" 狀態；隊友的法術打到 Boss → hit2 給主機；Boss 打到隊友 → 主機送 hit2。
        bool bossMode, coop, remoteCoop, resultShown, bossSeen;
        // 無盡模式：endless＝目前這場；netEndless／remoteEndless＝雙人選擇畫面上兩邊選的
        bool endless, netEndless, remoteEndless;
        float bossHideAt = -1f; int lastWaveSeen; bool bossAliveShown; int clientWaveRemaining;
        int WaveRemainingShown => battle == null ? 0 : (NetMode && !NetIsHost ? clientWaveRemaining : battle.WaveRemaining);

        // ---------------------------------------------------------------- 無盡模式：角色養成（能力值、技能樹、裝備；存檔保留）
        CharacterProfile prof;                    // 這一場用的角色（無盡模式）
        readonly CharacterPanel charPanel = new CharacterPanel();
        bool charOpen;                            // 角色畫面開著（單人無盡：戰鬥暫停）
        int lastXpSeen, lastKillsSeen, lastBossKillsSeen;
        bool levelLockSaid;
        string lastHitCls;   // 這次命中是哪個職業的特效／音效   // 這一場已經提示過「要轉職才能升級」
        readonly System.Random dropRng = new System.Random();
        int PendingUpgrades => endless && prof != null ? prof.statPoints + prof.UsableSkillPoints : 0;
        /// <summary>選技能畫面：這次是不是無盡模式（只有無盡模式用角色養成）</summary>
        bool EndlessSetup => NetMode ? netEndless : enemyClass == "endless" || enemyClass == "story";   // 故事模式也用角色養成
        /// <summary>選技能畫面可以選的技能：無盡模式＝角色會的技能；其他模式＝職業全部技能</summary>
        string[] SetupSkills => EndlessSetup ? Progress.Char(myClass).KnownSkills().ToArray() : AllowedSkills;
        /// <summary>其他模式可以選的技能：職業全部技能＋（轉職後）進階職業的全部技能</summary>
        string[] AllowedSkills
        {
            get
            {
                var list = new List<string>(Skills.Classes[myClass].skills);
                var a = Progress.Char(myClass).Adv;
                if (a != null) list.AddRange(a.skills);
                return list.ToArray();
            }
        }
        /// <summary>技能欄：基本 3 格＋（轉職後）進階技能 1 格</summary>
        bool AdvSlot => Array.Exists(SetupSkills, Advanced.IsAdvSkill);
        int SlotCount => AdvSlot ? 4 : 3;
        int BaseCount => myLoadout.FindAll(id => !Advanced.IsAdvSkill(id)).Count;
        int AdvCount => myLoadout.FindAll(Advanced.IsAdvSkill).Count;
        bool LoadoutReady => BaseCount == 3 && AdvCount == (AdvSlot ? 1 : 0);

        /// <summary>選一個技能：進階技能只有一格（換掉原本的）；基本技能最多 3 個（換掉最早選的）</summary>
        void PickSkill(string id)
        {
            if (myLoadout.Contains(id)) return;
            bool adv = Advanced.IsAdvSkill(id);
            if (adv) myLoadout.RemoveAll(Advanced.IsAdvSkill);
            else if (BaseCount >= 3) myLoadout.Remove(myLoadout.Find(x => !Advanced.IsAdvSkill(x)));
            myLoadout.Add(id);
        }

        /// <summary>技能配置只保留能選的，不足就從頭補（基本 3 個＋進階 1 個）</summary>
        void FixLoadout()
        {
            var list = SetupSkills;
            myLoadout.RemoveAll(id => Array.IndexOf(list, id) < 0);
            while (BaseCount > 3) myLoadout.Remove(myLoadout.Find(x => !Advanced.IsAdvSkill(x)));
            while (AdvCount > 1) myLoadout.Remove(myLoadout.Find(Advanced.IsAdvSkill));
            foreach (var id in list) { if (BaseCount >= 3) break; if (!Advanced.IsAdvSkill(id) && !myLoadout.Contains(id)) myLoadout.Add(id); }
            if (AdvSlot && AdvCount == 0) { var first = Array.Find(list, Advanced.IsAdvSkill); if (first != null) myLoadout.Add(first); }
        }

        /// <summary>無盡模式：經驗值存進角色、升級提示、打倒敵人掉裝備</summary>
        void TrackProgress()
        {
            if (prof == null || battle == null) return;
            int dx = battle.xp - lastXpSeen;
            if (dx > 0)
            {
                lastXpSeen = battle.xp;
                int ups = prof.AddXp(dx);
                if (prof.LevelLocked && !levelLockSaid) { levelLockSaid = true; Say($"已達 {Advanced.Level} 級：到職業工會完成轉職試煉並轉職，才能繼續升級", 3.5f); }
                if (ups > 0)
                {
                    Progress.SaveChar(prof);
                    Say($"⬆ 等級 {prof.level}！獲得 {ups * CharacterProfile.StatPerLevel} 能力點、{ups * CharacterProfile.SkillPerLevel} 技能點（點「角色」分配）", 3f);
                    floaters.Add(new Floater { text = $"⬆ Lv {prof.level}", color = new Color(0.8f, 0.55f, 1f), born = Time.time, screen = true });
                    Sfx.Play("levelup", 0.8f); Haptics.Play(Haptics.Kind.Success);
                }
            }
            int dk = battle.kills - lastKillsSeen; lastKillsSeen = battle.kills;
            for (int i = 0; i < dk; i++) { TryDrop(false); pendingGold += Economy.MinionGold(battle.wave); }
            if (dk > 0) { Sfx.Play("poof", 0.6f); Sfx.Play("coin", 0.35f, 1.1f); }   // 小怪倒下＋掉金幣
            int db = battle.bossKills - lastBossKillsSeen; lastBossKillsSeen = battle.bossKills;
            for (int i = 0; i < db; i++) { TryDrop(true); pendingGold += Economy.BossGold(battle.wave); }   // 每個敵人最多掉一件
            // 無盡模式（不是故事關卡）：清完一波＝過關，這一波的戰利品入袋
            if (storyStage == null && battle.wave > lootWave)
            {
                if (lootWave > 0) GrantLoot($"第 {lootWave} 波過關");
                lootWave = battle.wave;
            }
        }

        // ---------------------------------------------------------------- 戰利品：過關才拿得到（故事＝通關、無盡＝清完一波；倒下就沒了）
        readonly List<Item> pendingItems = new List<Item>();
        int pendingGold, lootWave;

        void ResetLoot() { pendingItems.Clear(); pendingGold = 0; lootWave = 0; }

        // ---------------------------------------------------------------- 消耗道具（戰鬥中點按鈕使用）
        float potionReadyAt;

        void UseConsumable(Economy.Consumable c)
        {
            if (battle == null || Me == null || !Me.Alive || Economy.Count(c.id) <= 0) return;
            if (IsTrial) { Say("⚔ 轉職試煉中不能使用道具", 2f); return; }
            if (c.scroll)
            {
                // 回城捲軸：帶著這關已經拿到的戰利品回城鎮（關卡不算通關）
                if (storyStage == null || NetMode) { Say("回城捲軸只能在故事關卡使用", 2f); return; }
                scrollRequested = true;   // 畫面繪製中不能結束戰鬥：下一幀在 Update 處理
                return;
            }
            UsePotion(c);
        }

        bool scrollRequested;
        void DoReturnScroll()
        {
            scrollRequested = false;
            if (battle == null || storyStage == null || Economy.Count("scroll") <= 0) return;
            {
                Economy.AddItem("scroll", -1);
                int theme = storyStage.theme;
                string loot = GrantLoot("使用回城捲軸", false);
                EndBattle(); ShowSetup();
                storyMapOpen = true; world.OpenTown(theme);
                Say("📜 回到城鎮" + (loot.Length > 0 ? "：" + loot : ""), 4f);
                Sfx.Play("heal", 0.8f);
            }
        }

        void UsePotion(Economy.Consumable c)
        {
            if (Time.time < potionReadyAt) { Say($"藥水冷卻中（{potionReadyAt - Time.time:0.0} 秒）", 1f); return; }
            Economy.AddItem(c.id, -1);
            potionReadyAt = Time.time + Economy.PotionCooldown;
            if (c.hpFrac > 0f) Me.hp = Mathf.Min(Me.maxHp, Me.hp + Me.maxHp * c.hpFrac);
            if (c.mpFlat > 0f) Me.mp = Mathf.Min(Me.maxMp, Me.mp + c.mpFlat);
            floaters.Add(new Floater { text = $"{c.icon} {c.name}", color = new Color(0.5f, 1f, 0.6f), born = Time.time, screen = true });
            Sfx.Play("potion", 0.8f); Sfx.Play("heal", 0.5f);
            if (NetMode && coop) NetSend?.Invoke(new Msg { t = "hp2", hp = Mathf.CeilToInt(Me.hp) });
        }

        /// <summary>把暫存的戰利品放進背包、金幣入帳（背包滿了就自動換成金幣）</summary>
        string GrantLoot(string why, bool say = true)
        {
            if (pendingItems.Count == 0 && pendingGold == 0) return "";
            int got = 0, sold = 0, gold = pendingGold;
            foreach (var it in pendingItems)
            {
                if (Progress.AddToBag(it)) got++;
                else { gold += Economy.SellPrice(it); sold++; }
            }
            Economy.Gold += gold;
            string msg = $"🎁 獲得裝備 {got} 件" + (sold > 0 ? $"（背包滿了，{sold} 件換成金幣）" : "") + $"、💰 {gold}";
            if (say) { Say($"{why}！{msg}", 3f); Sfx.Play("coin", 0.8f); Sfx.Play("charged", 0.5f, 1.2f); }
            pendingItems.Clear(); pendingGold = 0;
            return msg;
        }

        /// <summary>失敗：暫存的戰利品沒了</summary>
        void LoseLoot()
        {
            if (pendingItems.Count == 0 && pendingGold == 0) return;
            Say($"💨 沒有過關，這次的戰利品（裝備 {pendingItems.Count} 件、💰 {pendingGold}）沒拿到", 4f);
            pendingItems.Clear(); pendingGold = 0;
        }

        /// <summary>掉裝備（每個敵人最多一件）：小怪約 6%（幸運越高越多），Boss 一定掉一件稀有以上</summary>
        void TryDrop(bool boss)
        {
            int luk = prof.Total(Stat.Luk);
            if (!boss && dropRng.NextDouble() > 0.06 + 0.002 * luk) return;
            int rar = ItemGen.RollRarity(dropRng, luk * 0.3f, boss ? 2 : 0);
            int ilvl = Mathf.Max(1, prof.level + dropRng.Next(-1, 3) + battle.wave / 10);   // 無盡：物品等級跟著角色等級（越後面的波次略高）
            if (storyStage != null)
            {
                // 故事：照地區的等級帶掉（熔岩 1～10、森林 11～20、海上 21～30），越後面的關卡越高
                var (lo, hi) = Story.LevelBand(storyStage.theme);
                ilvl = Mathf.Clamp(battle.wave + dropRng.Next(-2, 2), lo, hi);
            }
            ItemGen.SetTheme = battle.Theme;   // 主題地圖掉對應的套裝
            var it = ItemGen.Make(dropRng, ilvl, rar, myClass);
            pendingItems.Add(it);   // 先暫存：過關才拿得到
            floaters.Add(new Floater { text = $"🎁 [{Item.RarityNames[rar]}] {it.name}（過關後獲得）", color = Item.RarityColors[rar], born = Time.time, screen = true });
            Sfx.Play(rar >= 3 ? "win" : "select", rar >= 3 ? 0.7f : 0.8f, 1.2f);
        }

        /// <summary>角色畫面（全螢幕）：能力值、技能樹、裝備；有改動就重新套用到戰鬥中的自己</summary>
        void DrawCharacter()
        {
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y, pad = W * 0.03f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            int fs = UiKit.BaseFont;
            float bh = fs * 2.2f;
            var p = Progress.Char(myClass);
            var r = new Rect(pad, topY + pad, W - pad * 2, botY - topY - pad * 3 - bh);
            if (charPanel.Draw(r, p) && battle != null && endless && phase != Phase.Setup)
            {
                var ids = new List<string>(); foreach (var s in Me.loadout) ids.Add(s.id);
                p.ApplyTo(Me, Skills.Classes[myClass], ids);
            }
            if (UiKit.Button(new Rect(W * 0.25f, botY - pad - bh, W * 0.5f, bh), phase == Phase.Setup ? "完成" : (coop ? "回到戰鬥" : "回到戰鬥（暫停中）"), fs, true, new Color(0.5f, 0.8f, 1f)))
            { charOpen = false; Progress.SaveChar(p); if (phase == Phase.Setup) FixLoadout(); }
        }

        // ---------------------------------------------------------------- 故事模式（劇本：Resources/story.txt，格式見 STORY_FORMAT.md）
        StoryStage storyStage, pendingStage;      // 正在玩的關卡
        bool storyMapOpen; int storyChapter;
        List<StoryLine> dlg; int dlgIdx; Action dlgDone;   // 對話中

        void ShowDialogue(List<StoryLine> lines, Action done)
        {
            if (lines == null || lines.Count == 0) { done?.Invoke(); return; }
            dlg = lines; dlgIdx = 0; dlgDone = done;
        }

        void EndDialogue() { var d = dlgDone; dlg = null; dlgDone = null; d?.Invoke(); }

        // ---------------------------------------------------------------- 轉職試煉（職業工會）：海上礁石第 27～30 波，敵人更硬更痛、不能用道具
        bool IsTrial => storyStage != null && storyStage.id == "trial";

        void StartTrial()
        {
            int tth = Battle.ThemeOf(Advanced.TrialLastWave);
            var st = new StoryStage { id = "trial", title = "⚔ 轉職試煉", theme = tth, fromWave = Advanced.TrialFirstWave - 10 * tth, toWave = Advanced.TrialLastWave - 10 * tth, xp = 400, desc = WorldUI.TrialText };
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = "新的道路先從這場試煉開始。地點在海上礁石，敵人更強，不能使用藥水。" });
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = "撐過四波，打倒最後的深海巨妖。考的不是逞強，是危急時，你願意替誰留一條退路。" });
            st.after.Add(new StoryLine { speaker = "公會長", tag = "guild", text = "我看見你的答案了。回工會選擇進階職業吧；選定便不能更換，往後要守護誰，由你決定。" });
            storyMapOpen = false; storyStage = st;
            ShowDialogue(st.before, StartStageBattle);
        }

        /// <summary>點地圖上的關卡：開場對話 → 開打</summary>
        void StartStory(StoryStage st)
        {
            storyMapOpen = false; storyStage = st;
            ShowDialogue(st.before, StartStageBattle);
        }

        void StartStageBattle()
        {
            if (storyStage == null) return;
            pendingStage = storyStage;
            StartBossBattle(false, true);
            pendingStage = null;
            Say($"📖 {storyStage.title}", 3f);
        }

        StoryStage NextStage(StoryStage st)
        {
            bool found = false;
            foreach (var s in Story.AllStages()) { if (found) return s; if (s == st) found = true; }
            return null;
        }

        /// <summary>故事模式：大地圖與城鎮（WorldUI）</summary>
        readonly WorldUI world = new WorldUI();

        void DrawStoryMap()
        {
            if (world.OnStartStage == null)
            {
                world.OnStartStage = st => { storyMapOpen = false; StartStory(st); };
                world.OnBack = () => storyMapOpen = false;
                world.OnCharacter = () => charOpen = true;
                world.OnStartTrial = StartTrial;
            }
            world.Draw(Progress.Char(myClass));
        }

        // ---------------------------------------------------------------- 可走動的 3D 城鎮
        readonly TownWalk townWalk = new TownWalk();
        static readonly List<string> NoSkills = new List<string>();
        bool speechTown;   // 語音辨識目前是用在城鎮（聽招呼語）還是戰鬥（聽技能名稱）

        /// <summary>正在城鎮裡走動（可以打招呼）</summary>
        bool TownWalking => world.Walking && dlg == null && !charOpen && storyMapOpen;

        void UpdateTownWalk()
        {
            if (world.Walk == null)
            {
                world.Walk = townWalk;
                townWalk.OnGreet = GreetNpc;
                townWalk.OnExit = th => { world.OpenGate(th); Sfx.Play("select", 0.7f); };
                townWalk.OnWarp = () => { world.OpenWarp(); Sfx.Play("summon", 0.6f); };
            }
            world.Cam = cam;
            bool want = storyMapOpen && phase == Phase.Setup && world.walkMode && world.screen != WorldUI.Screen.Map && cam != null;
            if (want && (!townWalk.Active || townWalk.Town != world.town)) townWalk.Enter(world.town, cam, cam.transform.position.y - 1.4f);
            else if (!want && townWalk.Active) townWalk.Leave();
            if (!townWalk.Active) return;
            if (TownWalking) townWalk.Update(cam, Mathf.Min(Time.deltaTime, 0.1f), Time.time - world.holdAt < 0.15f);
            world.SpeechNote = !UseSpeech ? "這支手機沒有語音辨識：走近居民後按「👋 打招呼」" : speech.Running ? null : $"語音：{speech.Status}";
        }

        void OnSpeechText(string text)
        {
            if (!TownWalking) return;
            if (Greeting.Find(text) != null) { world.Heard = text; world.HeardAt = Time.time; }
            townWalk.Heard(text);
        }

        /// <summary>打招呼：居民說完自己的話；店員說完打開設施</summary>
        void GreetNpc(TownWalk.Npc n)
        {
            var lines = new List<StoryLine>();
            int k = n.lines.Length;
            for (int i = 0; i < k; i++) lines.Add(new StoryLine { speaker = n.name, tag = "npc", text = n.lines[(n.lineAt + i) % k] });
            n.lineAt = (n.lineAt + 1) % Mathf.Max(1, k);
            Sfx.Play("talk", 0.7f); Haptics.Play(Haptics.Kind.Light);
            string opens = n.opens;
            ShowDialogue(lines, () => { if (opens != null) world.OpenFacility(opens); });
        }

        /// <summary>對話框：畫面下方，點一下下一句；右上「跳過」</summary>
        void DrawDialogue()
        {
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y, pad = W * 0.04f, gap = pad * 0.5f;
            int fs = UiKit.BaseFont, fsL = Mathf.RoundToInt(fs * 1.15f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            if (dlgIdx >= dlg.Count) { EndDialogue(); return; }
            var line = dlg[dlgIdx];
            var col = Story.TagColor(line.tag);
            var r = new Rect(pad, botY - pad - Mathf.Min(H*.32f,(botY-topY)*.42f), W - pad * 2, Mathf.Min(H*.32f,(botY-topY)*.42f));
            UiKit.Card(r, 0.93f, string.IsNullOrEmpty(line.speaker) ? (Color?)null : col, 0.9f);
            float y = r.y + gap*1.5f, tx = r.x + gap * 1.5f;
            if (!string.IsNullOrEmpty(line.speaker))
            {
                // 說話者的頭像（左邊）＋名字（頭像下方）；台詞在右邊
                float ps = Mathf.Min(r.height - fs * 2.6f - gap * 2f, r.width * 0.28f);
                var pr = new Rect(r.x + gap * 1.5f, r.y + gap * 1.2f, ps, ps);
                GUI.DrawTexture(pr, Portraits.Get(line.speaker, line.tag), ScaleMode.ScaleToFit, true);
                UiKit.Title(new Rect(pr.x-gap*.3f,pr.yMax+gap*.4f,ps+gap*.6f,fs*1.6f),line.speaker,col,Mathf.RoundToInt(fs*.85f));
                tx = pr.xMax + gap * 1.5f;
            }
            UiKit.Text(new Rect(tx, y, r.xMax - tx - gap * 1.5f, r.yMax - y - fs * 1.6f), Story.Fill(line.text, myClass), fsL,
                string.IsNullOrEmpty(line.speaker) ? new Color(0.85f, 0.88f, 1f) : Color.white, TextAnchor.UpperLeft, false, 0.6f);
            UiKit.Text(new Rect(r.x, r.yMax - fs * 1.6f, r.width - gap * 1.5f, fs * 1.4f), $"{dlgIdx + 1}/{dlg.Count}　▶ 點一下繼續", Mathf.RoundToInt(fs * 0.75f), new Color(0.8f, 0.8f, 0.85f), TextAnchor.MiddleRight);
            if (UiKit.Button(new Rect(W - pad - W * 0.22f, topY + pad, W * 0.22f, fs * 1.8f), "跳過 ⏭", Mathf.RoundToInt(fs * 0.8f))) { EndDialogue(); return; }
            var e = Event.current;
            if (e.type == EventType.MouseUp) { dlgIdx++; Sfx.Play("talk", 0.6f); e.Use(); if (dlgIdx >= dlg.Count) EndDialogue(); }
        }

        int NetModeCode => coop ? (netEndless ? 2 : 1) : 0;
        BossRig bossRig; BossAI bossAi; GameObject slamGo;
        VolcanoScene volcano;   // 無盡模式第 1～10 波的熔岩火山場景
        ForestScene forest;     // 第 11～20 波的森林場景
        SeaScene sea;           // 第 21～30 波的海上礁石場景
        readonly Dictionary<Fighter, MinionRig> minionGo = new Dictionary<Fighter, MinionRig>();
        readonly Dictionary<Fighter, float> minionLastHp = new Dictionary<Fighter, float>();
        readonly HashSet<Fighter> minionWinding = new HashSet<Fighter>();
        readonly Dictionary<Fighter, GameObject> aimLines = new Dictionary<Fighter, GameObject>();   // （已不用：狙擊手改成射出後才畫紅線）
        readonly Dictionary<Projectile, GameObject> projLine = new Dictionary<Projectile, GameObject>();   // 敵人已放出的攻擊：飛行路線紅線
        readonly Dictionary<Battle.Lane, GameObject> laneGo = new Dictionary<Battle.Lane, GameObject>();   // 藤蔓／衝刺路線
        string bossWindup, bossChargingId; float nextBossState, lastBossHp;
        public const int BossHpSolo = 200;   // 合作時再乘上人數倍率（每多一人 +30%）
        Fighter Ally => battle?.ally;

        // 電腦難度：0 簡單、1 普通、2 困難（電腦出招慢／瞄得不準／少防禦，玩家受到的傷害也打折）
        int difficulty = 1;
        static readonly string[] DifficultyNames = { "簡單", "普通", "困難" };
        static float DamageTakenFor(int d) => d == 0 ? 0.5f : d == 1 ? 0.75f : 1f;
        static float BossHpFor(int d) => d == 0 ? 0.7f : d == 1 ? 0.85f : 1f;

        // 語音詠唱：唸出咒語就開始詠唱（本機比對自己錄的樣本）
        readonly MicInput mic = new MicInput();
        SpeechInput speech;               // 手機內建語音辨識：直接唸技能名稱（不用錄音）；不支援時改用錄音比對
        bool UseSpeech => speech != null && speech.Supported;
        VoiceTemplates voice;
        bool voiceOn;
        string recordingSkill;            // 正在錄哪個技能的咒語樣本
        string heard = ""; float heardUntil;

        Camera cam;
        PlayArea area;
        Material mat;
        Battle battle;
        EnemyAI ai;

        // 選擇（存在 PlayerPrefs）
        string myClass, enemyClass;           // enemyClass = "random" 或職業 id
        readonly List<string> myLoadout = new List<string>();
        string enemyClassUsed;

        // 顯示物件
        GameObject enemyShield;
        CharacterRig enemyRig;            // 敵人外觀（依職業造型，會走路、詠唱、被打中、倒地）
        Vector3 lastEnemyFeet; float lastEnemyHp; bool enemyDeadShown;
        ChargeFx myChargeFx, enemyChargeFx;   // 詠唱光效（依職業）
        SkillDef myFxSkill, enemyFxSkill;
        bool dummyMode;                   // 木頭人練習：不會動、不會攻擊、打不死，統計傷害
        float dummyDamage; int dummyHits;
        readonly Dictionary<Projectile, GameObject> projGo = new Dictionary<Projectile, GameObject>();
        readonly Dictionary<Trap, GameObject> trapGo = new Dictionary<Trap, GameObject>();
        readonly Dictionary<BurnZone, GameObject> burnGo = new Dictionary<BurnZone, GameObject>();
        // 攻擊預警：敵人蓄力時地上的紅線、隕石落點的紅圈
        GameObject warnLine; readonly Dictionary<Projectile, GameObject> landGo = new Dictionary<Projectile, GameObject>();
        // 身體閃避：站立時手機高度（基準）、最近的位置（判斷快速側跨）
        float standY = -1f, nextDodgeOk;
        readonly Queue<KeyValuePair<float, Vector3>> moveHist = new Queue<KeyValuePair<float, Vector3>>();
        public const float CrouchDrop = 0.32f, SidestepDist = 0.55f, SidestepWindow = 0.35f, DodgeTime = 0.35f, DodgeCooldown = 1.8f;
        class Floater { public Vector3 posMap; public string text; public Color color; public float born; public bool screen; }
        readonly List<Floater> floaters = new List<Floater>();
        string message = ""; float messageUntil;
        float incomingFlash, hitFlash;
        // 打擊感：畫面震動（像素）、命中停頓（重擊時戰鬥時間停一下下）、蓄滿提示音只響一次
        float shake, hitStopUntil, lastWarn = -9f;
        bool chargeWasFull;
        void Kick(float px, float stop) { shake = Mathf.Max(shake, px); if (!NetMode && stop > 0f) hitStopUntil = Mathf.Max(hitStopUntil, Time.time + stop); }
        static float DmgOf(string text) => text != null && text.StartsWith("-") && float.TryParse(text.Substring(1), out var d) ? d : 0f;
        // AR 追蹤中斷（對著白牆、遮住鏡頭…）：單人練習整場暫停（敵人、法術、判定都停住），恢復後繼續。
        // 只是把鏡頭轉開、看不到敵人時不暫停，敵人照樣從畫面外攻擊。
        bool frozen;
        // 鎖定：敵人出現在我的畫面中才能發射攻擊法術（和雙人模式相同）
        bool enemyOnScreen; Rect enemyRect; float enemyScreenSide;   // enemyScreenSide：敵人在左(<0)／右(>0)
        GUIStyle label, small, button, big, center, iconButton;

        Fighter Me => battle?.player;
        Fighter En => battle?.enemy;

        public void Init(Camera camera, PlayArea playArea, Material baseMat)
        {
            cam = camera; area = playArea; mat = baseMat;
            myClass = PlayerPrefs.GetString("sd_my_class", "mage");
            if (!Skills.Classes.ContainsKey(myClass)) myClass = "mage";
            enemyClass = PlayerPrefs.GetString("sd_enemy_class", "random");
            difficulty = Mathf.Clamp(PlayerPrefs.GetInt("sd_difficulty", 1), 0, 2);
            voiceOn = true;   // 一定要唸技能名稱才能詠唱，語音固定開啟
            voice = VoiceTemplates.Load();
            speech = new SpeechInput();
            speech.OnSkill += OnSpeechSkill;
            speech.OnText += OnSpeechText;
            Progress.Get = k => PlayerPrefs.GetString(k, "");
            Progress.Set = (k, v) => { PlayerPrefs.SetString(k, v); PlayerPrefs.Save(); };
            Story.Get = Progress.Get; Story.Set = Progress.Set;
            LoadLoadout();
            Sfx.Ensure();
        }

        void LoadLoadout()
        {
            myLoadout.Clear();
            var saved = PlayerPrefs.GetString("sd_loadout_" + myClass, "");
            var cls = Skills.Classes[myClass];
            var allowed = AllowedSkills;
            foreach (var id in saved.Split(',')) if (Array.IndexOf(allowed, id) >= 0 && !myLoadout.Contains(id)) myLoadout.Add(id);
            if (BaseCount < 3) { myLoadout.RemoveAll(x => !Advanced.IsAdvSkill(x)); myLoadout.AddRange(cls.defaultLoadout); }
            FixLoadout();
        }

        void SaveChoices()
        {
            PlayerPrefs.SetString("sd_my_class", myClass);
            PlayerPrefs.SetString("sd_enemy_class", enemyClass);
            PlayerPrefs.SetInt("sd_difficulty", difficulty);
            PlayerPrefs.SetString("sd_loadout_" + myClass, string.Join(",", myLoadout));
        }

        public void ShowSetup() { EndBattle(); phase = Phase.Setup; localReady = false; }

        // ================================================================ 雙人：準備 → 開戰
        void SendReady()
        {
            SaveChoices();
            localReady = true;
            NetSend?.Invoke(new Msg { t = "ready", k = myClass, ks = myLoadout.ToArray(), e = NetModeCode, a = Progress.Char(myClass).adv ?? "" });
            Say("已準備，等待對手…", 3f);
            TryStartNet();
        }

        void TryStartNet()
        {
            if (!NetMode || !localReady || PeerIds.Count == 0) return;
            foreach (var pid in PeerIds) if (!peerInfo.TryGetValue(pid, out var pi) || !pi.ready) return;
            localReady = remoteReady = false;
            foreach (var pi in peerInfo.Values) pi.ready = false;
            foreach (var pid in PeerIds) if (peerInfo[pid].mode != NetModeCode) { Say("大家選的模式不同（對戰／合作打 Boss／無盡），請選一樣的再按準備", 4f); return; }
            if (!coop && PeerIds.Count > 1) { Say($"⚔ 對戰只能兩個人（現在房間有 {PeerIds.Count + 1} 人），請選合作模式", 4f); return; }
            if (coop) StartBossBattle(true, netEndless); else StartNetBattle();
        }

        void StartNetBattle()
        {
            EndBattle();
            var ec = Skills.Classes.TryGetValue(remoteClass, out var found) ? found : Skills.Classes["mage"];
            enemyClassUsed = ec.id;
            dummyMode = false;
            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            Advanced.ApplyStats(me, Progress.Char(myClass).Adv);   // 轉職後的能力（對戰不另外平衡）
            var radv = peerInfo.TryGetValue(PeerIds.Count > 0 ? PeerIds[0] : 0, out var rpi0) ? Advanced.Get(rpi0.adv) : null;
            var en = new Fighter($"對手{(radv != null ? radv.name : ec.name)}", ec, remoteLoadout ?? new string[0], false);
            Advanced.ApplyStats(en, radv);
            en.head = NetEnemyHeadW;
            SyncPlayer(me);
            battle = new Battle(me, en) { remoteEnemy = true };
            battle.OnEvent += OnBattleEvent; battle.OnSlashLine += ShowSlashLine; battle.OnShotWarn += ShowShotLine;
            battle.OnSpawn += p => NetSend?.Invoke(new Msg { t = "cast2", id = p.id, k = p.skill.id, p = p.basePos, d = p.dir, t0 = SharedClock?.Invoke() ?? 0, ok = p.reflected, rad = p.curveAmp, s = p.pathLen });
            battle.OnRemoteBurn += (sk, dmg, at) => NetSend?.Invoke(new Msg { t = "burn2", k = sk.id, dmg = Mathf.RoundToInt(dmg), p = at });
            battle.OnRemoteHit += (sk, dmg, at, id) => NetSend?.Invoke(new Msg { t = "hit2", id = id, k = sk.id, dmg = dmg, p = at, e = battle.LastProcs });
            battle.OnTrapPlaced += t => NetSend?.Invoke(new Msg { t = "trap2", id = t.id, k = t.skill.id, p = t.pos, s = t.armAt - battle.now });
            battle.OnTrapGone += id => NetSend?.Invoke(new Msg { t = "trapgone", id = id });
            battle.OnDispel += (c, r) => NetSend?.Invoke(new Msg { t = "dispel", p = c, rad = r });
            battle.OnSlice += (id, theirs) => NetSend?.Invoke(new Msg { t = "slice", id = id, ok = theirs });
            WireAdvNet();
            ai = null;
            if (RemotePlay) enemyRig = CharacterRig.Create(ec.id, en.adv);   // 遠端對戰：對手是虛擬角色（進階職業有自己的外觀）
            remoteAnchor = new GameObject("Remote Anchor").transform;
            lastEnemyFeet = en.Feet; lastEnemyHp = en.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            phase = Phase.Fighting;
            Say($"⚔ 對手：{ec.name}（{string.Join("・", Array.ConvertAll(remoteLoadout ?? new string[0], id => Skills.All.TryGetValue(id, out var sd) ? sd.name : id))}）", 4f);
        }

        /// <summary>選技能畫面：電腦難度（單人、合作打 Boss）</summary>
        void DrawDifficultyRow(ref float y, float pad, float gap, float u, int fsS, float W)
        {
            float lw = W * 0.2f, bw = (W - pad * 2 - lw - gap * 3) / 3f;
            UiKit.Text(new Rect(pad, y, lw, u * 1.1f), "難度", fsS, new Color(1f, 0.9f, 0.6f));
            Color[] cols = { new Color(0.4f, 0.9f, 0.5f), new Color(1f, 0.8f, 0.3f), new Color(1f, 0.35f, 0.35f) };
            for (int i = 0; i < 3; i++)
                if (UiKit.Button(new Rect(pad + lw + gap + i * (bw + gap), y, bw, u * 1.1f), DifficultyNames[i], fsS, difficulty == i, cols[i]))
                { difficulty = i; PlayerPrefs.SetInt("sd_difficulty", difficulty); }
            y += u * 1.1f + gap;
        }

        // ================================================================ Boss 模式
        /// <summary>開打 Boss：單人（coopMode=false）或雙人合作（主機跑 Boss，隊友端的 Boss 由主機的狀態驅動）</summary>
        void StartBossBattle(bool coopMode, bool endlessMode = false)
        {
            SaveChoices();
            EndBattle();
            bossMode = true; coop = coopMode; endless = endlessMode; dummyMode = false; enemyClassUsed = "boss";
            dummyDamage = 0f; dummyHits = 0; lastWaveSeen = 0; bossHideAt = -1f; bossAliveShown = false;
            ResetLoot(); potionReadyAt = 0f;
            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            prof = null; charOpen = false;
            if (endlessMode)
            {
                // 無盡模式：用角色養成的能力值、技能樹、裝備
                prof = Progress.Char(myClass);
                prof.ApplyTo(me, Skills.Classes[myClass], myLoadout);
                lastXpSeen = 0; lastKillsSeen = 0; lastBossKillsSeen = 0; levelLockSaid = false;
            }
            else Advanced.ApplyStats(me, Progress.Char(myClass).Adv);
            var bc = Skills.Classes["boss"];
            // 無盡模式的 Boss 不召喚小怪（小怪由場地邊緣陸續冒出）
            var bossSkills = new List<string>(bc.defaultLoadout);
            if (endlessMode) bossSkills.Remove("b_summon");
            var boss = new Fighter(bc.name, bc, bossSkills, false) { bodyRadius = 0.75f };
            int partyN = coopMode ? 1 + PeerIds.Count : 1;
            float party = 1f + 0.5f * (partyN - 1);   // 參與人數倍率：每多一人，Boss 血量 +50%（攻擊力 +20%、施法頻率 +25% 在 BossAI）
            boss.maxHp = Mathf.RoundToInt(BossHpSolo * party * BossHpFor(difficulty)); boss.hp = boss.maxHp;
            me.damageTakenMul = DamageTakenFor(difficulty);
            SyncPlayer(me);
            battle = new Battle(me, boss);
            battle.OnEvent += OnBattleEvent; battle.OnSlashLine += ShowSlashLine; battle.OnShotWarn += ShowShotLine;
            bool host = !coopMode || NetIsHost;
            if (coopMode)
            {
                foreach (var pid in PeerIds)
                {
                    peerInfo.TryGetValue(pid, out var pi);
                    var rc = Skills.Classes.TryGetValue(pi?.cls ?? "", out var found) ? found : Skills.Classes["mage"];
                    var head = NetPeers.TryGetValue(pid, out var pp) ? pp.head : NetEnemyHeadW;
                    var aadv = Advanced.Get(pi?.adv);
                    string an = aadv != null ? aadv.name : rc.name;
                    var af = new Fighter(PeerIds.Count > 1 ? $"{(pid == 0 ? "房主" : $"隊友{pid}")}・{an}" : $"隊友{an}", rc, pi?.loadout ?? new string[0], false) { head = head, isAlly = true, pid = pid };
                    Advanced.ApplyStats(af, aadv);
                    battle.allies.Add(af);
                }
                battle.coopHost = NetIsHost;
                battle.remoteEnemy = !NetIsHost;   // 隊友端：Boss 是主機的代理
                battle.OnSpawn += p => NetSend?.Invoke(new Msg { t = "cast2", id = p.id, k = p.skill.id, p = p.basePos, d = p.dir, t0 = SharedClock?.Invoke() ?? 0, ok = p.reflected, rad = p.curveAmp, s = p.pathLen, e = p.owner == battle.enemy || p.owner.isMinion ? 1 : 0 });
                battle.OnAllyHit += (who, sk, dmg, at, id) => SendTo(who.pid, new Msg { t = "hit2", id = id, k = sk.id, dmg = dmg, p = at });   // 主機：只送給被打到的那位
                battle.OnTeamBuff += sk => NetSend?.Invoke(new Msg { t = "tbuff", k = sk.id });
                battle.OnTaunt += sk => SendTo(0, new Msg { t = "taunt", k = sk.id });   // 隊友端：嘲諷交給主機算仇恨
                battle.OnRemoteHit += (sk, dmg, at, id) => NetSend?.Invoke(new Msg { t = "hit2", id = id, k = sk.id, dmg = dmg, p = at, e = battle.LastProcs });
                battle.OnRemoteBurn += (sk, dmg, at) => SendTo(0, new Msg { t = "burn2", k = sk.id, dmg = Mathf.RoundToInt(dmg), p = at });
                battle.OnTrapPlaced += t => NetSend?.Invoke(new Msg { t = "trap2", id = t.id, k = t.skill.id, p = t.pos, s = t.armAt - battle.now });
                battle.OnTrapGone += id => NetSend?.Invoke(new Msg { t = "trapgone", id = id });
                battle.OnDispel += (c, r) => NetSend?.Invoke(new Msg { t = "dispel", p = c, rad = r });
                // 斬開法球：送「發射者 pid 空間」的編號（自己的法術換成 RId(我, id)；遠端的本來就是）
                battle.OnSlice += (id, theirs) => NetSend?.Invoke(new Msg { t = "slice", id = theirs ? id : RId(MyPid, id) });
                WireAdvNet();
                battle.OnMinionHit += (id, dmg, sk) => SendTo(0, new Msg { t = "mhit", id = id, dmg = dmg, k = sk.id, e = battle.LastProcs });
                // 主機：藤蔓／衝刺路線送給隊友顯示（傷害由主機判定）
                battle.OnLane += ln => { if (!ln.remote && NetIsHost) NetSend?.Invoke(new Msg { t = "lane", k = ln.kind, pts = new[] { ln.a, ln.b }, rad = ln.grow, s = ln.until - battle.now }); };
            }
            battle.OnLane += ShowLane;
            battle.OnPool += pl => PlantFx.PoisonPool(WorldFrame.FromWorld(pl.pos), pl.radius, Mathf.Max(0.1f, pl.until - battle.now));
            // Boss 出生點：場地內、離玩家們夠遠的地方（隊友端先放在遠處，等主機的位置）
            battle_MeForSpawn = me;
            boss.head = host && !RemotePlay ? BossStartPoint(me) : new Vector3(0f, BossRig.Height, 0f);   // 遠端：Boss 在大家圍成的圓中間（開戰時才定座位）
            boss.head.y = BossRig.Height;
            boss.forward = Fighter.Flat(me.head - boss.head);
            bossSeen = host;
            if (host)
            {
                bossAi = new BossAI(battle, boss, p => area.Inside(WorldFrame.FromWorld(p)), p => area.DistanceToEdge(WorldFrame.FromWorld(p)), Environment.TickCount);
                bossAi.OnEnrage += OnBossEnrage;
                bossAi.SetDifficulty(difficulty);
                bossAi.storyIntense = endlessMode && pendingStage != null;   // 故事 Boss：出招更頻繁、更多大範圍招式
                if (endlessMode)
                {
                    battle.bossBaseHp = 150f * BossHpFor(difficulty);
                    battle.edgePoint = () => OutsideEdgePoint();          // 小怪：場地邊緣外 1～2m 出現（淡入），再走進來
                    battle.bossPoint = () => OutsideEdgePoint(true);      // Boss 也一樣（優先在我面前）
                    battle.OnBossSpawn += w => bossAi.Respawn(Mathf.Max(0.7f, 1f - 0.06f * w));
                    if (pendingStage != null)
                    {
                        battle.stageEnemyMul = pendingStage.theme == 0 ? 0.5f : 1f;   // 初級地圖（熔岩地帶）：敵人數量減半
                        if (pendingStage.id == "trial") { battle.hpBoost = Advanced.TrialHpMul; me.damageTakenMul *= Advanced.TrialDmgMul; }   // 轉職試煉
                        battle.StartStage(pendingStage.FirstWave, pendingStage.LastWave);   // 故事關卡
                    }
                    else battle.StartEndless();
                }
                battle.minionInside = p => area.Inside(WorldFrame.FromWorld(p)) && area.DistanceToEdge(WorldFrame.FromWorld(p)) >= 0.3f;
                battle.minionSpeed = difficulty == 0 ? 0.75f : difficulty == 1 ? 0.95f : 1.15f;
            }
            ai = null;
            bossRig = BossRig.Create();
            bossRig.SetVisible(bossSeen);
            if (bossSeen && !endlessMode) SpawnFade.Play(bossRig.gameObject);   // 淡入出現
            lastEnemyFeet = boss.Feet; lastEnemyHp = boss.hp; lastBossHp = boss.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            enemyShield.SetActive(false);
            phase = Phase.Fighting;
            Sfx.Play("roar");
            Haptics.Play(Haptics.Kind.Heavy);
            if (endlessMode)
            {
                if (!host) battle.endless = true;   // 隊友端：只用來判斷結束（兩人都倒下才結束）
                bossRig.SetVisible(false);
                Say("♾ 無盡模式：小怪會從場地邊緣不斷出現，每 60 秒來一隻 Boss！撐越久越好", 4f);
            }
            else Say(coopMode ? $"🔥 合作討伐：熔岩巨像（HP {boss.maxHp}）— 和隊友一起打倒它！" : $"🔥 Boss：熔岩巨像（HP {boss.maxHp}）— 看紅圈、紅線閃避！", 4f);
        }

        /// <summary>
        /// 敵人出生點：場地邊緣外 1～2m 的隨機點（世界座標，地上），離玩家至少 3m；出生後自己走進場地。
        /// preferFront：開場的對手／Boss 優先出現在我面前那一側（一開打就看得到）。
        /// </summary>
        Vector3 OutsideEdgePoint(bool preferFront = false)
        {
            var poly = area.Polygon;
            var pm = Me ?? battle_MeForSpawn;
            var fwd = pm != null ? Fighter.Flat(pm.forward) : Vector3.forward;
            var fallback = (pm != null ? pm.Feet : Vector3.zero) + fwd * 6f;
            if (poly == null || poly.Count < 3) return fallback;
            var c = Vector3.zero; foreach (var q in poly) c += q; c /= poly.Count;
            Vector3 best = fallback; float bestScore = float.MinValue;
            for (int tries = 0; tries < 24; tries++)
            {
                int i = UnityEngine.Random.Range(0, poly.Count);
                var ps = Vector3.Lerp(poly[i], poly[(i + 1) % poly.Count], UnityEngine.Random.value);
                var outward = ps - c; outward.y = 0f;
                if (outward.sqrMagnitude < 1e-4f) continue;
                ps += outward.normalized * (SpawnOutsideMin + UnityEngine.Random.value * (SpawnOutsideMax - SpawnOutsideMin));
                var pw = WorldFrame.ToWorld(ps); pw.y = 0f;
                float d = MinPartyDist(pw);
                float score = Mathf.Min(d, 6f) + (preferFront && pm != null ? 2f * Vector3.Dot(Fighter.Flat(pw - pm.Feet).normalized, fwd) : 0f) + UnityEngine.Random.value * 0.5f;
                if (d < 3f) score -= 10f;
                if (score > bestScore) { bestScore = score; best = pw; }
            }
            return best;
        }
        public const float SpawnOutsideMin = 1f, SpawnOutsideMax = 2f;

        /// <summary>所有玩家（我＋隊友）的中心</summary>
        Vector3 PartyCenter()
        {
            var c = Me.Feet; int n = 1;
            if (battle != null) foreach (var a in battle.allies) { c += a.Feet; n++; }
            return c / n;
        }

        /// <summary>點 p 離最近的玩家多遠</summary>
        float MinPartyDist(Vector3 p)
        {
            var pm = Me ?? battle_MeForSpawn;
            if (pm == null) return 99f;
            float d = Battle.FlatDistance(p, pm.Feet);
            if (battle != null) foreach (var a in battle.allies) d = Mathf.Min(d, Battle.FlatDistance(p, a.Feet));
            return d;
        }

        Fighter battle_MeForSpawn;   // 開打前 battle 還沒建好：出生點用的「我」
        /// <summary>Boss 開場位置：場地邊緣外 1～2m、我面前那一側（淡入後走進來）</summary>
        Vector3 BossStartPoint(Fighter me)
        {
            var p = OutsideEdgePoint(true);
            return new Vector3(p.x, BossRig.Height, p.z);
        }

        void OnBossEnrage()
        {
            if (bossRig) bossRig.SetEnraged(true);
            Sfx.Play("roar"); Haptics.Play(Haptics.Kind.Heavy); Kick(18f, 0f);
            Say($"🔥 {En.name}狂暴了！出招變快！", 3f);
        }

        /// <summary>主機：每 0.1 秒送 Boss 的位置、血量、蓄力、狂暴、目標給隊友</summary>
        void SendBossState()
        {
            if (!coop || !NetIsHost || battle == null || Time.time < nextBossState) return;
            nextBossState = Time.time + 0.1f;
            var b = battle.enemy;
            int e = bossAi != null && bossAi.Enraged ? 1 : 0;
            NetSend?.Invoke(new Msg { t = "boss", id = TargetCode(b.target), p = b.head, d = b.forward, hp = Mathf.CeilToInt(b.hp), dmg = b.maxHp, k = b.charging != null ? b.charging.id : "", s = battle.ChargeProgress(b), e = e, pts = b.charging != null && (b.charging.id == "b_leap" || b.charging.atTarget) ? new[] { b.aimPoint } : null,
                ks = endless ? new[] { battle.wave.ToString(), battle.WaveRemaining.ToString(), battle.kills.ToString(), battle.bossKills.ToString(), battle.xp.ToString(), battle.waveTotal.ToString() } : null });
            // 小怪：位置與「編號,血量,最大血量,預備中」
            var ms = battle.minions;
            var pts = new Vector3[ms.Count]; var ks = new string[ms.Count];
            for (int i = 0; i < ms.Count; i++) { pts[i] = ms[i].head; ks[i] = $"{ms[i].minionId},{Mathf.CeilToInt(ms[i].hp)},{ms[i].maxHp},{(ms[i].windupUntil > 0f ? 1 : 0)},{ms[i].minionKind},{TargetCode(ms[i].target)},{ms[i].alt.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}"; }
            NetSend?.Invoke(new Msg { t = "mins", pts = pts, ks = ks });
        }

        /// <summary>合作模式的網路訊息</summary>
        void OnNetCoop(Msg m)
        {
            var ally = battle.AllyByPid(m.pid);   // 送這則訊息的隊友
            switch (m.t)
            {
                case "lane":
                    // 主機的藤蔓／衝刺路線（只顯示）
                    if (!NetIsHost && m.pts != null && m.pts.Length >= 2) battle.AddRemoteLane(m.k, m.pts[0], m.pts[1], m.rad, m.s);
                    break;
                case "taunt":
                    // 隊友嘲諷：主機把 Boss 的仇恨拉到他身上
                    if (NetIsHost && ally != null && Skills.All.TryGetValue(m.k ?? "", out var tsk)) battle.Taunt(ally, tsk.buffAmount);
                    break;
                case "pet":
                    battle.AddRemotePet(RId(m.pid, m.id), m.k, ally, m.p, m.s);
                    break;
                case "petgone":
                    battle.RemoveRemotePet(RId(m.pid, m.id));
                    break;
                case "oath":
                    // 我是聖騎士：隊友受到的傷害有一部分轉到我身上
                    battle.TakeShared(m.dmg, Me.Chest);
                    NetSend?.Invoke(new Msg { t = "hp2", hp = Mathf.CeilToInt(Me.hp) });
                    break;
                case "tbuff":
                    // 隊友（牧師）的治療／淨化／守護／祈禱也作用在我身上
                    if (Skills.All.TryGetValue(m.k, out var tb))
                    {
                        bool wasDown = !Me.Alive;
                        battle.ApplyTeamBuff(tb, ally != null ? ally.name : "隊友", ally);
                        if (wasDown && Me.Alive) { Say($"✨ {(ally != null ? ally.name : "隊友")}的復活術！你站起來了", 3f); Sfx.Play("heal"); Haptics.Play(Haptics.Kind.Success); }
                        // 主機：隊友（牧師）的治療算仇恨（補到的人數 × 治療量）
                        if (NetIsHost && ally != null && tb.heal > 0) { int n = 1; foreach (var a in battle.AllPlayers) if (a.Alive && a != ally) n++; battle.AddThreat(ally, tb.heal * n); }
                        NetSend?.Invoke(new Msg { t = "hp2", hp = Mathf.CeilToInt(Me.hp) });
                    }
                    break;
                case "cast2":
                    // 隊友（或主機的 Boss）的法術：只顯示
                    if (Skills.All.TryGetValue(m.k, out var cs))
                    {
                        float elapsed = (float)((SharedClock?.Invoke() ?? m.t0) - m.t0);
                        battle.AddRemoteProjectile(RId(m.pid, m.id), cs, m.p, m.d, Mathf.Clamp(elapsed, 0f, 1f), m.ok, m.rad, m.s, m.e == 1 ? battle.enemy : ally);
                    }
                    break;
                case "hit2":
                    if (!Skills.All.TryGetValue(m.k, out var hs)) break;
                    {
                        bool isTrap = m.id < 0;
                        int rid = RId(m.pid, isTrap ? -m.id : m.id);
                        if (m.pid != 0)
                        {
                            // 隊友打到 Boss：主機扣血；其他隊友只把那發的畫面移除
                            if (isTrap) battle.RemoveRemoteTrap(rid); else battle.RemoveRemoteProjectile(rid);
                            if (NetIsHost)
                            {
                                battle.ApplyHitOnEnemy(hs, m.dmg, m.p, isTrap, ally);
                                battle.ApplyProcs(battle.enemy, m.e, m.p);
                            }
                        }
                        else if (!NetIsHost)
                        {
                            // 主機判定 Boss 打到我（我已經斬開的那發不算）
                            if (!isTrap && battle.WasSliced(rid)) break;
                            battle.RemoveRemoteProjectile(rid);
                            battle.ApplyRemoteHit(hs, m.dmg, m.p, isTrap);
                            NetSend?.Invoke(new Msg { t = "hp2", hp = Mathf.CeilToInt(Me.hp) });
                        }
                    }
                    break;
                case "burn2":
                    if (NetIsHost) battle.ApplyBurnOnEnemy(m.dmg, m.p, ally);
                    break;
                case "hp2":
                    if (ally != null) ally.hp = m.hp;
                    break;
                case "trap2":
                    if (Skills.All.TryGetValue(m.k, out var ts)) battle.AddRemoteTrap(RId(m.pid, m.id), ts, m.p, m.s);
                    break;
                case "trapgone":
                    battle.RemoveRemoteTrap(RId(m.pid, m.id));
                    break;
                case "dispel":
                    // 隊友的抗拒法環：主機打斷 Boss 的蓄力（Boss 在圈內時）
                    SpellFx.DispelRing(WorldFrame.FromWorld(m.p), m.rad, Skills.All["dispel"].color);
                    Sfx.Play("block", 0.8f, 0.7f);
                    if (NetIsHost) battle.DispelAt(battle.enemy, m.p, m.rad);
                    break;
                case "state":
                    // 隊友的狀態：血量、蹲下／閃避（主機判定 Boss 打不打得到他）
                    if (ally == null) break;
                    ally.hp = m.hp;
                    ally.crouching = (m.e & 16) != 0;
                    if ((m.e & 32) != 0) ally.dodgeUntil = battle.now + 0.15f;
                    ally.armorUntil = (m.e & 64) != 0 ? battle.now + 0.4f : 0f;
                    break;
                case "boss":
                    if (NetIsHost) break;
                    {
                        var b = battle.enemy;
                        b.head = m.p; b.forward = m.d;
                        if (m.pts != null && m.pts.Length > 0) b.aimPoint = m.pts[0];   // 熔岩躍擊的落點
                        b.hp = m.hp; if (m.dmg > 0) b.maxHp = m.dmg;
                        if (m.ks != null && m.ks.Length >= 4)
                        {
                            // 無盡模式：波次、這波剩幾隻、擊倒數（清完一波自己回 10% 血；打倒 Boss 回 30%）
                            int.TryParse(m.ks[0], out var wv);
                            int.TryParse(m.ks[3], out var bk);
                            if (Me.Alive && bk > battle.bossKills) Me.hp = Mathf.Min(Me.maxHp, Me.hp + Me.maxHp * 0.3f);
                            else if (Me.Alive && wv > battle.wave && battle.wave > 0) Me.hp = Mathf.Min(Me.maxHp, Me.hp + Me.maxHp * 0.1f);
                            battle.wave = wv; battle.bossKills = bk;
                            int.TryParse(m.ks[1], out clientWaveRemaining);
                            if (m.ks.Length >= 6) int.TryParse(m.ks[5], out battle.waveTotal);
                            int.TryParse(m.ks[2], out battle.kills);
                            if (m.ks.Length >= 5) int.TryParse(m.ks[4], out battle.xp);   // 隊友共用經驗值
                        }
                        if (battle.endless)
                        {
                            // 隊友端：依波次主題換 Boss 名稱（熔岩巨像／腐化樹王）
                            var bcls = Skills.Classes[Battle.BossClassOf(battle.Theme)];
                            if (b.cls != bcls) { b.cls = bcls; b.name = bcls.name; }
                        }
                        if (!string.IsNullOrEmpty(m.k) && Skills.All.TryGetValue(m.k, out var bs))
                        {
                            if (b.charging != bs) b.charging = bs;
                            b.chargeScale = (m.e & 1) != 0 ? 0.7f : 1f;
                            b.chargeStart = battle.now - m.s * bs.charge * b.chargeScale;
                        }
                        else b.charging = null;
                        b.target = FromTargetCode(m.id);
                        if ((m.e & 1) != 0 && bossRig && !bossEnragedShown) { bossEnragedShown = true; OnBossEnrage(); }
                        if (!bossSeen) { bossSeen = true; lastEnemyFeet = b.Feet; if (bossRig && !endless) { bossRig.SetVisible(true); SpawnFade.Play(bossRig.gameObject); } }
                    }
                    break;
                case "mhit":
                    // 隊友打到小怪（主機扣血）
                    if (NetIsHost)
                    {
                        battle.ApplyHitOnMinion(m.id, m.dmg, Skills.All.TryGetValue(m.k ?? "", out var mk) ? mk : null);
                        var mm = battle.MinionById(m.id); if (mm != null) battle.ApplyProcs(mm, m.e, mm.Chest);   // 隊友升級的附加效果
                    }
                    break;
                case "mins":
                    if (NetIsHost || m.ks == null || m.pts == null) break;
                    {
                        // 隊友端：更新小怪代理（新的就生出來，沒有的就移除）
                        var seen = new HashSet<int>();
                        for (int i = 0; i < m.ks.Length && i < m.pts.Length; i++)
                        {
                            var parts = m.ks[i].Split(',');
                            if (parts.Length < 4 || !int.TryParse(parts[0], out var mid)) continue;
                            seen.Add(mid);
                            int mkind = parts.Length >= 5 && int.TryParse(parts[4], out var mk2) ? mk2 : 0;
                            var mf = battle.MinionById(mid) ?? battle.SpawnMinion(new Vector3(m.pts[i].x, 0f, m.pts[i].z), mid, mkind);
                            mf.forward = Fighter.Flat(m.pts[i] - mf.head) != Vector3.zero ? Fighter.Flat(m.pts[i] - mf.head) : mf.forward;
                            mf.head = m.pts[i];
                            if (float.TryParse(parts[1], out var mhp)) mf.hp = mhp;
                            if (int.TryParse(parts[2], out var mmax)) mf.maxHp = mmax;
                            mf.windupUntil = parts[3] == "1" ? battle.now + 1f : 0f;
                            if (parts.Length >= 6 && int.TryParse(parts[5], out var tcode)) mf.target = FromTargetCode(tcode);
                            if (parts.Length >= 7 && float.TryParse(parts[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var malt)) mf.alt = malt;   // 飛行／浮空高度
                        }
                        foreach (var mf in battle.minions) if (!seen.Contains(mf.minionId)) mf.hp = 0f;
                    }
                    break;
                case "bfx":
                    // 主機的 Boss 出手：播動作與特效
                    if (bossRig) bossRig.Attack(m.k);
                    if (m.k == "b_slam") BossSlamFx(m.p);
                    Sfx.Play(m.k == "b_slam" ? "boom" : "cast_mage", m.k == "b_slam" ? 1f : 0.6f, 0.6f);
                    break;
            }
        }
        bool bossEnragedShown;

        /// <summary>藤蔓：長出藤蔓模型；衝刺路線：地上一條紅線（衝刺開始才畫，畫到衝刺結束）</summary>
        void ShowLane(Battle.Lane ln)
        {
            float life = Mathf.Max(0.1f, ln.until - battle.now);
            if (ln.kind == "vine")
            {
                PlantFx.Vine(WorldFrame.FromWorld(ln.a), WorldFrame.FromWorld(ln.b), ln.grow, life);
                Sfx.Play("trap", 0.6f, 0.7f);
                return;
            }
            var go = Prim(PrimitiveType.Cube, new Color(1f, 0.15f, 0.15f, 0.45f), true);
            var a = ln.a; a.y = 0.02f; var b = ln.b; b.y = 0.02f; var d = b - a; float len = Mathf.Max(0.1f, d.magnitude);
            go.transform.position = WorldFrame.FromWorld((a + b) / 2f);
            go.transform.rotation = WorldFrame.RotFromWorld(Quaternion.LookRotation(d / len, Vector3.up));
            go.transform.localScale = new Vector3(Mathf.Max(0.3f, ln.width * 2f), 0.004f, len);
            laneGo[ln] = go;
            if (Battle.FlatDistance(ln.a, Me.Feet) < 7f) Sfx.Play("warn", 0.5f, 0.9f);
        }

        /// <summary>場景：無盡模式第 1～10 波是熔岩火山（地板＋遠方背景＋火星）</summary>
        void UpdateScene()
        {
            // 主題：第 1～10 波熔岩火山、11～20 波森林、21～30 波海上礁石，之後輪流
            int theme = endless && battle.wave >= 1 ? Battle.ThemeOf(battle.wave) : -1;
            var poly = area.Polygon;
            float fy = poly.Count > 0 ? poly[0].y : area.FloorY;
            // 三個場景在無盡模式一開始（第 1 波倒數時）就全部建好：貼圖產生要花一點時間，避免換主題時卡頓
            if (theme >= 0 && volcano == null) volcano = VolcanoScene.Create(poly, fy);
            if (theme >= 0 && forest == null) forest = ForestScene.Create(poly, fy);
            if (theme >= 0 && sea == null) sea = SeaScene.Create(poly, fy);
            if (volcano) volcano.SetVisible(theme == 0);
            if (forest) forest.SetVisible(theme == 1);
            if (sea) sea.SetVisible(theme == 2);
        }

        /// <summary>小怪的外觀：新的就生出來、跟著位置走、預備動作／出手／被打／倒下</summary>
        void UpdateMinionVisuals(float dt)
        {
            if (battle == null) return;
            var live = new HashSet<Fighter>();
            foreach (var m in battle.minions)
            {
                if (!m.Alive) continue;
                live.Add(m);
                if (!minionGo.TryGetValue(m, out var rig) || !rig)
                {
                    rig = MinionRig.Create();
                    rig.SetVariant(m.minionKind);
                    minionGo[m] = rig; minionLastHp[m] = m.hp;
                    rig.transform.position = WorldFrame.FromWorld(m.Feet + Vector3.up * m.alt);
                    SpawnFade.Play(rig.gameObject);   // 淡入出現
                }
                var oldPos = rig.transform.position;
                var newPos = WorldFrame.FromWorld(m.Feet + Vector3.up * m.alt);   // 飛在空中／浮著／跳起來的高度
                var f = WorldFrame.DirFromWorld(m.forward); f.y = 0f;
                rig.transform.SetPositionAndRotation(newPos, f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : rig.transform.rotation);
                bool jumped = (newPos - oldPos).magnitude > 2f;   // 虛空術士瞬移（隊友端也看得到）：兩邊都冒出傳送光
                if (jumped) { MinionRig.SpawnFx(oldPos); MinionRig.SpawnFx(newPos); Sfx.Play("dodge", 0.5f, 0.6f); }
                rig.MoveSpeed = jumped ? 0f : Mathf.Lerp(rig.MoveSpeed, (newPos - oldPos).magnitude / dt, 0.25f);
                bool winding = m.windupUntil > 0f;
                if (winding && !minionWinding.Contains(m)) { minionWinding.Add(m); rig.Windup(Skills.All["m_claw"].charge); }
                else if (!winding && minionWinding.Contains(m)) { minionWinding.Remove(m); rig.Attack(); }
                if (minionLastHp.TryGetValue(m, out var lh) && m.hp < lh - 0.01f) rig.PlayHit();
                minionLastHp[m] = m.hp;
            }
            foreach (var kv in new List<KeyValuePair<Fighter, MinionRig>>(minionGo))
                if (!live.Contains(kv.Key))
                {
                    if (kv.Value) { kv.Value.SetDead(true); Destroy(kv.Value.gameObject, 1.2f); }
                    minionGo.Remove(kv.Key); minionLastHp.Remove(kv.Key); minionWinding.Remove(kv.Key);
                    if (aimLines.TryGetValue(kv.Key, out var dl)) { if (dl) Destroy(dl); aimLines.Remove(kv.Key); }
                }
        }

        /// <summary>地裂重擊的特效：地面衝擊波、爆炸聲、離得近就震動</summary>
        void BossSlamFx(Vector3 feetW)
        {
            bool forestBoss = battle.Theme == 1;
            BossRig.SlamShockwave(WorldFrame.FromWorld(feetW), Skills.All[forestBoss ? "p_stomp" : "b_slam"].areaRadius, forestBoss ? new Color(0.45f, 0.75f, 0.2f) : new Color(1f, 0.35f, 0.1f));
            float d = Battle.FlatDistance(feetW, Me.Feet);
            if (d < 6f) { Haptics.Play(Haptics.Kind.Heavy); Kick(Mathf.Lerp(24f, 8f, d / 6f), 0f); }
        }

        /// <summary>戰鬥結束時顯示一次結果（合作模式：Boss 倒下＝勝利；兩人都倒下＝失敗）</summary>
        void ShowResult()
        {
            if (resultShown) return;
            resultShown = true;
            if (endless && IsTrial)
            {
                // 轉職試煉：通過 → 可以在職業工會選進階職業
                var st = storyStage;
                if (battle.stageCleared)
                {
                    string loot = GrantLoot("轉職試煉通過", false);
                    if (prof != null) { prof.trialPassed = true; prof.AddXp(st.xp); Progress.SaveChar(prof); }
                    Say($"🏆 通過轉職試煉！回職業工會選擇進階職業" + (loot.Length > 0 ? "\n" + loot : ""), 99f);
                    Sfx.Play("win"); Haptics.Play(Haptics.Kind.Success);
                    ShowDialogue(st.after, null);
                }
                else { LoseLoot(); Say("💀 試煉失敗…準備好再來挑戰（可以直接再挑戰，不用再付報名費）", 99f); Sfx.Play("lose"); Haptics.Play(Haptics.Kind.Warning); }
                return;
            }
            if (endless && storyStage != null)
            {
                // 故事關卡：通關 → 記錄、給經驗、結束對話；失敗 → 可以再挑戰
                var st = storyStage;
                if (battle.stageCleared)
                {
                    bool first = !Story.IsCleared(st.id);
                    Story.MarkCleared(st.id);
                    pendingGold += Economy.StageGold(st);
                    string loot = GrantLoot($"{st.title} 通關", false);
                    if (prof != null)
                    {
                        int ups = prof.AddXp(st.xp);
                        Progress.SaveChar(prof);
                        Say($"🏆 {st.title} 完成！獲得經驗 {st.xp}" + (ups > 0 ? $"，升到 Lv{prof.level}！" : "") + (first ? "" : "（再次通關）") + (loot.Length > 0 ? "\n" + loot : ""), 99f);
                    }
                    Sfx.Play("win"); Haptics.Play(Haptics.Kind.Success);
                    ShowDialogue(st.after, null);
                }
                else { LoseLoot(); Say($"💀 {st.title} 失敗…再挑戰一次吧（戰利品沒拿到）", 99f); Sfx.Play("lose"); Haptics.Play(Haptics.Kind.Warning); }
                return;
            }
            if (endless)
            {
                // 無盡模式：結算（波次、擊倒數、存活時間），記錄最佳；倒下時這一波還沒過關的戰利品沒了
                if (pendingItems.Count > 0 || pendingGold > 0) { pendingItems.Clear(); pendingGold = 0; }
                int score = battle.wave * 100 + battle.bossKills * 500 + battle.kills * 10;
                int best = PlayerPrefs.GetInt("sd_endless_best", 0);
                bool record = score > best;
                if (record) PlayerPrefs.SetInt("sd_endless_best", score);
                int sec = Mathf.FloorToInt(battle.now);
                Say($"♾ 遊戲結束：撐到第 {battle.wave} 波・擊倒小怪 {battle.kills}、Boss {battle.bossKills}・存活 {sec / 60}:{sec % 60:00}・分數 {score}" + (record ? "　🎉 新紀錄！" : $"（最佳 {best}）"), 99f);
                Sfx.Play(record ? "win" : "lose"); Haptics.Play(Haptics.Kind.Warning);
                return;
            }
            bool win = !En.Alive;
            Say(win ? (bossMode ? "🏆 討伐成功！熔岩巨像倒下了" : "🏆 勝利！") : (coop ? "💀 全滅…" : "💀 敗北…"), 99f);
            Sfx.Play(win ? "win" : "lose");
            Haptics.Play(win ? Haptics.Kind.Success : Haptics.Kind.Warning);
        }

        /// <summary>GameRoot 轉來的對戰訊息</summary>
        public void OnNet(Msg m)
        {
            switch (m.t)
            {
                case "mode":
                    coop = remoteCoop = m.e >= 1; netEndless = remoteEndless = m.e == 2; localReady = false;
                    Say(m.e == 2 ? "對方選了：♾ 無盡（合作）" : coop ? "對方選了：🤝 合作打 Boss" : "對方選了：⚔ 對戰", 2.5f);
                    return;
                case "ready":
                    remoteClass = m.k; remoteLoadout = m.ks; remoteReady = true; remoteCoop = m.e >= 1; remoteEndless = m.e == 2;
                    if (!peerInfo.TryGetValue(m.pid, out var rpi)) peerInfo[m.pid] = rpi = new PeerInfo();
                    rpi.cls = m.k; rpi.loadout = m.ks; rpi.ready = true; rpi.mode = m.e; rpi.adv = string.IsNullOrEmpty(m.a) ? null : m.a;
                    if (phase != Phase.Fighting) Say(PeerIds.Count > 1 ? $"{PeerName(m.pid)}已準備好（{ReadyCount()}/{PeerIds.Count}）" : "對手已準備好", 2f);
                    TryStartNet();
                    return;
            }
            if (battle == null || !NetMode) return;
            if (m.t == "slice")
            {
                // 劍士斬開了法術。合作：id＝發射者 pid 空間的編號；是我的就移除我自己的那發，否則移除遠端的那發
                if (coop) { if (m.id / IdSpace == MyPid) battle.ApplyRemoteSlice(m.id % IdSpace, true); else battle.ApplyRemoteSlice(m.id, false); }
                else battle.ApplyRemoteSlice(m.id, m.ok);
                return;
            }
            if (coop) { OnNetCoop(m); return; }
            switch (m.t)
            {
                case "cast2":
                    if (Skills.All.TryGetValue(m.k, out var cs))
                    {
                        float elapsed = (float)((SharedClock?.Invoke() ?? m.t0) - m.t0);
                        battle.AddRemoteProjectile(m.id, cs, m.p, m.d, Mathf.Clamp(elapsed, 0f, 1f), m.ok, m.rad, m.s);
                    }
                    break;
                case "hit2":
                    if (Skills.All.TryGetValue(m.k, out var hs))
                    {
                        bool isTrap = m.id < 0;
                        if (!isTrap && battle.WasSliced(m.id)) break;   // 我已經斬開的那發不算
                        if (isTrap) battle.RemoveRemoteTrap(-m.id); else battle.RemoveRemoteProjectile(m.id);
                        bool countered = battle.ApplyRemoteHit(hs, m.dmg, m.p, isTrap);
                        if (!countered) battle.ApplyProcs(Me, m.e, m.p);   // 對手的箭附加的出血／中毒／沉默
                        NetSend?.Invoke(new Msg { t = "hp2", hp = Mathf.CeilToInt(Me.hp), ok = countered });
                    }
                    break;
                case "pet":
                    battle.AddRemotePet(m.id, m.k, En, m.p, m.s);
                    break;
                case "petgone":
                    battle.RemoveRemotePet(m.id);
                    break;
                case "burn2":
                    // 對手的火海燒到我
                    battle.ApplyRemoteBurn(m.dmg, m.p);
                    NetSend?.Invoke(new Msg { t = "hp2", hp = Mathf.CeilToInt(Me.hp) });
                    break;
                case "hp2":
                    En.hp = m.hp;
                    if (m.ok) Say("對手反擊！法術被打回來了", 2f);
                    break;
                case "trap2":
                    if (Skills.All.TryGetValue(m.k, out var ts)) battle.AddRemoteTrap(m.id, ts, m.p, m.s);
                    break;
                case "trapgone":
                    battle.RemoveRemoteTrap(m.id);
                    break;
                case "shotwarn":
                    // 對手遠程普攻的瞄準預警線
                    if (m.pts != null && m.pts.Length >= 2 && En != null) ShowShotLine(En, m.pts[0], m.pts[1]);
                    break;
                case "slash":
                    // 對手（劍士、刺客）的刀線：預警＝紅線，出手＝白光（傷害由對手那邊判定）
                    if (m.pts != null && m.pts.Length >= 3 && En != null)
                    {
                        if (m.ok) En.windupUntil = battle.now + m.s;
                        ShowSlashLine(En, m.pts[0], m.pts[1], m.pts[2], m.ok);
                    }
                    break;
                case "dispel":
                    // 對手的抗拒法環：我站在圈內就被打斷
                    SpellFx.DispelRing(WorldFrame.FromWorld(m.p), m.rad, Skills.All["dispel"].color);
                    Sfx.Play("block", 0.8f, 0.7f);
                    battle.DispelAt(Me, m.p, m.rad);
                    break;
                case "state":
                    // 對手的詠唱、狀態（顯示用）
                    En.hp = m.hp;
                    if (!string.IsNullOrEmpty(m.k) && Skills.All.TryGetValue(m.k, out var chs))
                    {
                        if (En.charging != chs) En.charging = chs;
                        En.chargeStart = battle.now - m.s * chs.charge;
                    }
                    else En.charging = null;
                    float hold = battle.now + 0.4f;
                    En.blockUntil = (m.e & 1) != 0 ? hold : 0f;
                    En.shieldUntil = (m.e & 2) != 0 ? hold : 0f;
                    En.counterUntil = (m.e & 4) != 0 ? hold : 0f;
                    // 對手伏擊中（顯示警告用；出手由對手判定）
                    if ((m.e & 8) != 0)
                    {
                        if (En.armed == null) En.armed = En.loadout.Find(x => x.releaseNear) ?? Skills.All["backstab"];
                        En.armedUntil = hold;
                    }
                    else En.armed = null;
                    // 對手蹲下／側跨閃避（我判定命中時要算進去）
                    En.crouching = (m.e & 16) != 0;
                    remoteSeesMe = (m.e & 128) != 0; remoteSeenAt = Time.time;
                    En.armorUntil = (m.e & 64) != 0 ? hold : 0f;
                    if ((m.e & 32) != 0) En.dodgeUntil = battle.now + 0.15f;
                    break;
            }
        }

        string PeerName(int pid) => pid == 0 ? "房主" : $"隊友{pid}";
        int ReadyCount() { int n = 0; foreach (var pid in PeerIds) if (peerInfo.TryGetValue(pid, out var pi) && pi.ready) n++; return n; }

        void SendState()
        {
            if (!NetMode || battle == null || Time.time < nextState) return;
            nextState = Time.time + 0.1f;
            var me = Me; float now = battle.now;
            int e = (me.blockUntil > now ? 1 : 0) | (me.shieldUntil > now ? 2 : 0) | (me.counterUntil > now ? 4 : 0) | (me.armed != null ? 8 : 0)
                | (me.crouching ? 16 : 0) | (me.dodgeUntil > now ? 32 : 0) | (me.armorUntil > now ? 64 : 0)
                | (enemyOnScreen ? 128 : 0);   // 我的鏡頭拍到對手（對手的刺殺流派用：被看見就不算躲開）
            NetSend?.Invoke(new Msg { t = "state", k = me.charging != null ? me.charging.id : "", s = battle.ChargeProgress(me), hp = Mathf.CeilToInt(me.hp), e = e });
        }
        public void Hide() { EndBattle(); phase = Phase.Hidden; }

        // ================================================================ 開戰
        void StartBattle()
        {
            if (enemyClass == "story")
            {
                // 故事模式：在結算畫面按「再挑戰」＝重打這一關；否則打開故事地圖
                if (phase == Phase.Over && storyStage != null) StartStageBattle();
                else { SaveChoices(); storyMapOpen = true; world.screen = WorldUI.Screen.Map; }
                return;
            }
            storyStage = null;
            if (enemyClass == "boss" || enemyClass == "endless") { StartBossBattle(false, enemyClass == "endless"); return; }
            SaveChoices();
            EndBattle();
            enemyClassUsed = enemyClass == "random" ? Skills.ClassOrder[UnityEngine.Random.Range(0, Skills.ClassOrder.Length)] : enemyClass;
            var ec = Skills.Classes.TryGetValue(enemyClassUsed, out var found) ? found : DummyClass;
            // 敵人隨機帶 3 個技能（至少 2 個攻擊）
            var attacks = new List<string>(); var guards = new List<string>();
            foreach (var id in ec.skills) (Skills.All[id].type == SkillType.Self ? guards : attacks).Add(id);
            Shuffle(attacks); Shuffle(guards);
            var enemySkills = new List<string>(attacks.GetRange(0, Mathf.Min(2, attacks.Count)));
            var rest = new List<string>(attacks.GetRange(enemySkills.Count, attacks.Count - enemySkills.Count)); rest.AddRange(guards); Shuffle(rest);
            if (rest.Count > 0) enemySkills.Add(rest[0]);

            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            Advanced.ApplyStats(me, Progress.Char(myClass).Adv);
            dummyMode = enemyClass == "dummy";
            dummyDamage = 0f; dummyHits = 0;
            var en = dummyMode
                ? new Fighter("木頭人", DummyClass, new List<string>(), false)
                : new Fighter($"電腦{ec.name}", ec, enemySkills, false);
            // 敵人出生點：電腦對手從場地邊緣外 1～2m（我面前那一側）淡入出現再走進來；木頭人在場地內 3m
            float reach = area.ReachInside(area.Origin.forward, 0.6f);
            en.head = new Vector3(0f, 1.6f, Mathf.Clamp(reach, 0f, 3f));
            SyncPlayer(me);
            battle_MeForSpawn = me;
            if (!dummyMode) { var sp = OutsideEdgePoint(true); en.head = new Vector3(sp.x, 1.6f, sp.z); }
            battle = new Battle(me, en);
            battle.OnEvent += OnBattleEvent; battle.OnSlashLine += ShowSlashLine; battle.OnShotWarn += ShowShotLine;
            ai = dummyMode ? null : new EnemyAI(battle, en, p => area.Inside(WorldFrame.FromWorld(p)), p => area.DistanceToEdge(WorldFrame.FromWorld(p)), Environment.TickCount);
            ai?.SetDifficulty(difficulty);
            me.damageTakenMul = DamageTakenFor(difficulty);

            enemyRig = CharacterRig.Create(ec.id);
            if (!dummyMode) SpawnFade.Play(enemyRig.gameObject);   // 電腦對手從場地外淡入出現
            lastEnemyFeet = en.Feet; lastEnemyHp = en.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            phase = Phase.Fighting;
            Say(dummyMode ? "🪵 木頭人練習：唸技能名稱詠唱，再做放招動作" : $"⚔ 對手：{en.name}（{string.Join("・", enemySkills.ConvertAll(id => Skills.All[id].name))}）", 4f);
        }

        /// <summary>木頭人：不會動、不會攻擊；血量很多，打不死</summary>
        static readonly ClassDef DummyClass = new ClassDef
        {
            id = "dummy", name = "木頭人", color = new Color(0.72f, 0.52f, 0.3f), maxHp = 99999, maxMp = 0, mpRegen = 0,
            skills = new string[0], defaultLoadout = new string[0], desc = "練習用，不會攻擊",
        };

        void EndBattle()
        {
            if (prof != null) Progress.SaveChar(prof);
            charOpen = false;
            ClearArena();
            foreach (var go in projGo.Values) if (go) Destroy(go);
            foreach (var go in trapGo.Values) if (go) Destroy(go);
            foreach (var go in burnGo.Values) if (go) Destroy(go);
            foreach (var rig in petGo.Values) if (rig) Destroy(rig.gameObject);
            foreach (var ar in allyRigs.Values) if (ar) Destroy(ar.gameObject);
            allyRigs.Clear(); allyLastFeet.Clear();
            petGo.Clear();
            foreach (var go in landGo.Values) if (go) Destroy(go);
            landGo.Clear();
            if (warnLine) Destroy(warnLine);
            if (slamGo) Destroy(slamGo);
            if (volcano) Destroy(volcano.gameObject);
            if (forest) Destroy(forest.gameObject);
            if (sea) Destroy(sea.gameObject);
            foreach (var go in projLine.Values) if (go) Destroy(go);
            projLine.Clear();
            foreach (var go in laneGo.Values) if (go) Destroy(go);
            laneGo.Clear();
            if (bossRig) Destroy(bossRig.gameObject);
            foreach (var mr in minionGo.Values) if (mr) Destroy(mr.gameObject);
            minionGo.Clear(); minionLastHp.Clear(); minionWinding.Clear();
            foreach (var al in aimLines.Values) if (al) Destroy(al);
            aimLines.Clear();
            bossRig = null; bossAi = null; bossMode = false; bossWindup = null; resultShown = false; bossSeen = false;
            standY = -1f; moveHist.Clear(); ResetCalibration();
            projGo.Clear(); trapGo.Clear(); burnGo.Clear(); floaters.Clear();
            if (enemyShield) Destroy(enemyShield);
            if (enemyRig) Destroy(enemyRig.gameObject);
            if (remoteAnchor) Destroy(remoteAnchor.gameObject);
            if (myChargeFx) myChargeFx.Stop();
            if (enemyChargeFx) enemyChargeFx.Stop();
            myChargeFx = enemyChargeFx = null; myFxSkill = enemyFxSkill = null;
            battle = null; ai = null;
        }

        static void Shuffle<T>(List<T> l) { for (int i = l.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); } }

        // ================================================================ 每幀
        void Update()
        {
            UpdateMic();
            UpdateTownWalk();
            if (phase != Phase.Fighting && phase != Phase.Over) return;
            if (battle == null) return;
            SyncPlayer(Me);
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (Time.time < hitStopUntil) dt = 0f;   // 命中停頓
            if (endless) TrackProgress();
            if (scrollRequested) { DoReturnScroll(); return; }
            if (endless && charOpen && !coop) dt = 0f;   // 單人無盡：角色畫面開著時暫停
            hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 2.5f);
            shake = Mathf.Max(0f, shake - Time.deltaTime * Mathf.Max(40f, shake * 8f));
            // 蓄滿：叮一聲＋輕震動
            bool full = Me.charging != null && battle.ChargeProgress(Me) >= 1f;
            if (full && !chargeWasFull && Me.charging.charge > 0f) { Sfx.Play("charged", 0.8f); Haptics.Play(Haptics.Kind.Light); }
            chargeWasFull = full;
            frozen = phase == Phase.Fighting && !Tracking.Ok;
            UpdateScene();
            if (NetMode) SyncRemote();
            UpdateLock();
            if (phase == Phase.Fighting && (!frozen || NetMode))   // 雙人：追蹤中斷不暫停（只是暫時不能施法）
            {
                if (ai != null) ai.Update(dt);
                else if (bossAi != null) bossAi.Update(dt);
                else if (!NetMode) en_FaceMe();
                battle.Update(dt);
                if (!frozen) HandleInput();
                SendState();
                SendBossState();
                if (battle.Over) { phase = Phase.Over; ShowResult(); }
            }
            UpdateVisuals();
        }

        // 雙人：對手位置（GameRoot 提供：鏡頭偵測＋對手回報）；手機拿在身體前方 → 往後退一點才是身體
        void SyncRemote()
        {
            if (coop && battle.allies.Count > 0)
            {
                // 合作：其他玩家是隊友（位置由各自的手機回報；Boss 的位置由主機送來）
                foreach (var a in battle.allies)
                    if (NetPeers.TryGetValue(a.pid, out var pp)) { a.head = pp.head; var f = Fighter.Flat(pp.fwd); if (f != Vector3.zero) a.forward = f; }
                return;
            }
            if (!NetHasTarget) return;
            var fwd = Fighter.Flat(NetEnemyFwdW);
            En.head = NetEnemyHeadW - fwd * 0.12f;
            var toMe = Fighter.Flat(Me.head - En.head);
            En.forward = fwd != Vector3.zero ? fwd : (toMe != Vector3.zero ? toMe : Vector3.forward);
            if (remoteAnchor) remoteAnchor.SetPositionAndRotation(WorldFrame.FromWorld(En.Feet), WorldFrame.RotFromWorld(Quaternion.LookRotation(En.forward, Vector3.up)));
        }

        // 木頭人永遠面向玩家
        void en_FaceMe()
        {
            var d = Fighter.Flat(Me.head - En.head);
            if (d.sqrMagnitude > 1e-4f) En.forward = d.normalized;
            if (En.hp < En.maxHp * 0.5f) En.hp = En.maxHp;   // 打不死：血量過半就補滿
        }

        // 玩家的位置＝手機位置（換成場地座標）；追蹤中斷時位置不可信 → 停在最後的正確位置
        void SyncPlayer(Fighter me)
        {
            if (!Tracking.Ok) return;
            me.head = WorldFrame.ToWorld(cam.transform.position);
            me.forward = WorldFrame.DirToWorld(cam.transform.forward);
            UpdateBodyDodge(me);
        }

        /// <summary>
        /// 身體閃避：手機比站立高度低 32cm 以上＝蹲下（躲得過橫向攻擊）；
        /// 0.35 秒內水平移動 55cm 以上＝快速側跨（沒有無敵時間，只用來觸發遊俠的翻滾射擊；1.8 秒才能再算一次）。
        /// </summary>
        void UpdateBodyDodge(Fighter me)
        {
            if (battle == null) return;
            float dt = Mathf.Max(1e-3f, Time.deltaTime), y = me.head.y;
            if (standY < 0f) standY = y;
            // 站起來時基準很快跟上；蹲著時基準慢慢往下飄（蹲太久就不算閃避了）
            standY = y > standY ? Mathf.Lerp(standY, y, 1f - Mathf.Exp(-dt * 4f)) : Mathf.Lerp(standY, y, 1f - Mathf.Exp(-dt * 0.1f));
            bool was = me.crouching;
            if (UpdateCalibration(y)) { me.crouching = false; return; }
            me.crouching = standY - y > CrouchDropNow;
            if (me.crouching && !was) Haptics.Play(Haptics.Kind.Light);

            float now = battle.now;
            UpdateJump(me, y, dt);
            moveHist.Enqueue(new KeyValuePair<float, Vector3>(now, me.Feet));
            while (moveHist.Count > 0 && now - moveHist.Peek().Key > SidestepWindow) moveHist.Dequeue();
            if (moveHist.Count > 2 && now >= nextDodgeOk && Battle.FlatDistance(moveHist.Peek().Value, me.Feet) > SidestepDist)
            {
                me.dodgeUntil = now + DodgeTime; nextDodgeOk = now + DodgeCooldown;
                floaters.Add(new Floater { text = "💨 閃避", color = new Color(0.6f, 0.9f, 1f), born = Time.time, screen = true });
                Sfx.Play("dodge", 0.7f);
                moveHist.Clear();
            }
        }

        bool InTapZone(Vector2 guiPos) => guiPos.y > Screen.height * 0.2f && guiPos.y < Screen.height * 0.74f;

        void HandleInput()
        {
            if (Me != null) Me.palmVisible = Hand == null || Hand.HandVisible;   // 刺客伏擊技能：手掌在鏡頭前才蓄力
            if (Me != null && battle != null) Me.seenByFoe = SeenByFoe();       // 刺殺流派：有沒有被敵人看見
            if (Hand == null) return;
            bool drawingRune = Me.charging != null && HandGesture.ReleaseOf(Me.charging) == HandGesture.Style.Rune;
            // 符文畫好＝詠唱完成（蓄力直接滿）
            if (drawingRune && Hand.RuneReady && battle.ChargeProgress(Me) < 1f)
            {
                Me.chargeStart = battle.now - Me.charging.charge;
                Say($"✨ {RuneRecognizer.RuneName(RuneRecognizer.RuneOf(Me.charging.id))}符文完成！把準星對準目標，食指往前指發射", 1.5f);
            }
            // 獵人：握拳的手拉出畫面外＝拉滿弓＝蓄力完成
            if (Me.charging != null && HandGesture.ReleaseOf(Me.charging) == HandGesture.Style.Bow && Hand.BowDrawn && battle.ChargeProgress(Me) < 1f)
            {
                Me.chargeStart = battle.now - Me.charging.charge;
                Say("🏹 拉滿弓！把準星對準目標，手回到畫面張開放箭", 1.5f);
            }
            // 選技能只能用唸的：每次放技能前都要先唸出技能名稱（手勢只負責放招）
            Hand.ConsumeSelect(out _);
            // 放招動作依詠唱中的技能（陷阱往下壓、治癒收回、格擋舉盾…；其餘依職業）；沒在詠唱就不偵測
            Hand.ReleaseStyle = Me.charging != null ? HandGesture.ReleaseOf(Me.charging) : HandGesture.Style.None;
            Hand.RuneName = Me.charging != null && Hand.ReleaseStyle == HandGesture.Style.Rune ? RuneRecognizer.RuneOf(Me.charging.id) : null;
            Hand.RequiredNumber = Me.charging != null ? HandGesture.NumberOf(Me.charging.id) : 0;
            // 法師（唸咒語）：咒語唸完之前手勢不放招；唸完後比 5 放招（比著 5 等蓄滿也會自動放）
            if (Hand.ReleaseStyle == HandGesture.Style.Chant)
            {
                Hand.ReleaseStyle = ChantDone ? HandGesture.Style.Number : HandGesture.Style.None;
                Hand.RequiredNumber = ChantDone ? 5 : 0;
            }
            // 忍者：手印（依序比出數字）
            var seal = Me.charging != null ? Advanced.SealOf(Me.charging.id) : null;
            if (Me.charging != sealFor) { sealFor = Me.charging; sealStep = 0; }
            if (seal != null) { Hand.ReleaseStyle = HandGesture.Style.Number; Hand.RequiredNumber = seal[Mathf.Min(sealStep, seal.Length - 1)]; }
            // 普通攻擊：沒在詠唱時，手勢舉著就自動攻擊（法師比 1、獵人比 5、牧師比 2、刺客舉掌、劍士手掌劃一下）
            var basic = Me.basic ?? Skills.BasicOf(myClass);   // 技能樹強化後的普攻
            Hand.BasicSwipe = basic != null && basic.id == "na_sword" && Me.charging == null;
            bool swiped = Hand.ConsumeSwipe();
            if (basic != null && Me.charging == null && !frozen && phase == Phase.Fighting && Hand.HandVisible)
            {
                bool pose = basic.id switch
                {
                    "na_mage" => Hand.Current == HandGesture.Shape.One,
                    "na_arrow" => Hand.Current == HandGesture.Shape.Open,
                    "na_holy" => Hand.Current == HandGesture.Shape.Two,
                    "na_dagger" => Hand.Current == HandGesture.Shape.Open,
                    _ => false,
                };
                if (pose || (basic.id == "na_sword" && swiped)) DoBasic(basic);
            }
            // 手勢放招：往放招動作的瞄準點放
            if (Hand.ConsumeRelease())
            {
                if (frozen) { Say("AR 追蹤中斷，暫時不能施法", 1.5f); return; }
                if (seal != null && sealStep < seal.Length - 1)
                {
                    sealStep++;
                    Say($"🥷 手印 {sealStep}/{seal.Length}：接著比 {seal[sealStep]}", 1.2f);
                    Haptics.Play(Haptics.Kind.Light); Sfx.Play("select", 0.5f, 1.2f + 0.15f * sealStep);
                    return;
                }
                if (Me.charging == null) Say("🎤 先唸出技能名稱才能放招", 1.5f);
                else if (drawingRune) ReleaseAt(ScreenCenter, true);   // 符文畫好＋食指往前指
                else if (battle.ChargeProgress(Me) < 1f) { if (Hand.ReleaseStyle != HandGesture.Style.Number) Say("蓄力還沒完成", 1f); }   // 法師比著數字等蓄滿就會自動放
                else ReleaseAt(ScreenCenter, true);   // 瞄準一律用畫面中央的準星（手只負責觸發）
            }
        }

        // ---- 刺殺流派：我有沒有被敵人看見
        bool remoteSeesMe = true; float remoteSeenAt = -99f;
        public const float FoeFovDeg = 55f, FoeSightRange = 12f;   // 電腦／Boss／小怪的視野：正前方左右 55°、12m 內
        bool SeenByFoe()
        {
            // 雙人對戰：對手的手機鏡頭有沒有拍到我（每 0.1 秒同步；斷線太久就當作看得到）
            if (NetMode && !coop) return Time.time - remoteSeenAt > 1.5f || remoteSeesMe;
            bool Sees(Fighter f)
            {
                if (f == null || !f.Alive) return false;
                var to = Me.head - f.head; to.y = 0f;
                if (to.magnitude > FoeSightRange) return false;
                var fw = Fighter.Flat(f.forward);
                return fw == Vector3.zero || Vector3.Angle(fw, to) <= FoeFovDeg;
            }
            if (!battle.remoteEnemy && Sees(battle.enemy)) return true;
            foreach (var m in battle.minions) if (Sees(m)) return true;
            return false;
        }

        static Vector2 ScreenCenter => new Vector2(Screen.width / 2f, Screen.height / 2f);
        SkillDef sealFor; int sealStep;   // 忍者的手印：正在比第幾個

        /// <summary>普通攻擊：朝畫面中央的準星（近身＝砍身邊的敵人）</summary>
        void DoBasic(SkillDef s)
        {
            var ray = cam.ScreenPointToRay(ScreenCenter);
            var o = WorldFrame.ToWorld(ray.origin);
            var d = WorldFrame.DirToWorld(ray.direction);
            var floor = d.y < -0.01f ? o + d * (-o.y / d.y) : Me.Feet + Fighter.Flat(d) * 15f;
            // 劍士：刀線＝手掌在畫面上劃過的起點→終點
            Vector3 from = default, to = default;
            if (s.id == "na_sword")
            {
                from = WorldFrame.DirToWorld(cam.ScreenPointToRay(Hand.SwipeFrom).direction);
                to = WorldFrame.DirToWorld(cam.ScreenPointToRay(Hand.SwipeTo).direction);
            }
            if (!battle.BasicAttack(Me, s, d, floor, enemyOnScreen, from, to)) return;
            if (!Skills.IsMeleeBasic(s))
                SpellFx.CastBurst(myClass, s.color, cam.transform.position + cam.transform.forward * 0.5f - cam.transform.up * 0.08f, ray.direction, 0.2f);
        }

        void ReleaseAt(Vector2 sp, bool byGesture)
        {
            if (Me.charging == null) { Say("🎤 先唸出技能名稱才能放招", 1.5f); return; }

            // 點擊方向（場地座標）與地板交點（陷阱用）
            var ray = cam.ScreenPointToRay(sp);
            var o = WorldFrame.ToWorld(ray.origin);
            var d = WorldFrame.DirToWorld(ray.direction);
            // 準星朝上（指不到地面）時，取準星方向的遠處（隕石用）
            var floor = d.y < -0.01f ? o + d * (-o.y / d.y) : Me.Feet + Fighter.Flat(d) * 15f;
            // 沒鎖定敵人也能放招：一律朝準星方向（打不打得到看瞄準）
            if (!battle.TryRelease(Me, d, floor, out var why, enemyOnScreen)) Say(why, 1.5f);
            else
            {
                if (byGesture) Say("🖐️ 放招！", 0.8f);
                // 放招光效：在手機前方爆開，朝法術飛行方向
                SpellFx.CastBurst(myClass, Me.charging != null ? Me.charging.color : Skills.Classes[myClass].color,
                    cam.transform.position + cam.transform.forward * 0.5f - cam.transform.up * 0.08f, ray.direction, 0.35f);
            }
        }

        // ================================================================ 語音
        bool VoiceReady => myLoadout.TrueForAll(id => voice.Ready(id));

        void UpdateMic()
        {
            // 手機內建語音辨識：戰鬥中語音開啟就聽，唸技能名稱即詠唱（不能和下面的錄音比對同時用麥克風）
            // 城鎮裡走動時也開著：聽「你好」之類的招呼語
            bool town = TownWalking && UseSpeech;
            bool wantSpeech = UseSpeech && (voiceOn && phase == Phase.Fighting || town) && recordingSkill == null;
            if (wantSpeech && (!speech.Running || town != speechTown))
            {
                speech.Candidates = town ? NoSkills : SpeechCandidates;
                speech.ExtraHints = town ? Greeting.Words : null;
                speechTown = town;
                if (!speech.Running) speech.Start();
            }
            else if (!wantSpeech && speech != null && speech.Running) speech.Stop();
            speech?.Tick();
            HandGesture.MageChant = UseSpeech;   // 法師：有語音辨識就唸咒語放招，沒有就比數字
            UpdateChant();

            // 錄樣本中、或（不支援內建辨識時）戰鬥中語音開啟且咒語都錄好了，才開麥克風做錄音比對
            bool want = recordingSkill != null || (!UseSpeech && voiceOn && phase == Phase.Fighting && VoiceReady);
            if (want && !mic.Running)
            {
                if (mic.Start()) mic.Spotter.OnUtterance += OnUtterance;
                else { Say(mic.Error, 2f); recordingSkill = null; }
            }
            else if (!want && mic.Running) mic.Stop();
            mic.Tick();
        }

        /// <summary>語音辨識要聽的：技能名稱（＋咒語）；大魔導士多聽增幅咒</summary>
        List<string> SpeechCandidates
        {
            get
            {
                var l = new List<string>(myLoadout);
                if (Me != null && Me.adv == "archmage") l.Add("amp");
                return l;
            }
        }

        // 法師：唸技能名稱 → 唸咒語（chantPending＝咒語已唸完的技能）→ 手在畫面比 5 放招
        SkillDef chantPending;
        bool ChantDone => chantPending != null && Me != null && Me.charging != null && Me.charging.id == chantPending.id;

        void UpdateChant()
        {
            if (chantPending == null) return;
            if (phase != Phase.Fighting || battle == null || Me == null || Me.charging == null || Me.charging.id != chantPending.id) { chantPending = null; if (Me != null) Me.ampNext = false; }
        }

        void OnChant(string skillId)
        {
            if (phase != Phase.Fighting || battle == null || !Skills.All.TryGetValue(skillId, out var skill)) return;
            heard = $"🎤 {skill.chant}"; heardUntil = Time.time + 1.5f;
            if (skillId == "amp")
            {
                // 大魔導士的增幅咒：唸完技能咒語之後再唸 → 這一招傷害 +50%
                if (!ChantDone) { Say("✨ 增幅咒要在唸完技能咒語之後唸", 1.5f); return; }
                Me.ampNext = true;
                Sfx.Play("charged", 0.8f, 1.3f); Haptics.Play(Haptics.Kind.Medium);
                Say("🔮 增幅！這一招傷害 +50%，手在畫面比 5 放招", 1.5f);
                return;
            }
            if (Me.charging == null || Me.charging.id != skillId)
            {
                Say(Me.charging == null ? $"🎤 先唸技能名稱「{skill.name}」，再唸咒語" : $"現在詠唱的是「{Me.charging.name}」，咒語是「{Me.charging.chant}」", 1.5f);
                return;
            }
            if (frozen) { Say("AR 追蹤中斷，暫時不能施法", 1.5f); return; }
            Haptics.Play(Haptics.Kind.Light);
            chantPending = Me.charging;
            Sfx.Play("select");
            Say($"✨ {skill.chant}！手在畫面比 5（五指張開）放招", 1.5f);
        }

        void OnSpeechSkill(string id)
        {
            if (id.StartsWith(SpeechInput.ChantPrefix)) { OnChant(id.Substring(SpeechInput.ChantPrefix.Length)); return; }
            if (phase != Phase.Fighting || battle == null || frozen || !Skills.All.TryGetValue(id, out var skill)) return;
            heard = $"🎤 {skill.name}"; heardUntil = Time.time + 1.5f;
            if (Me.charging == skill) return;
            if (!battle.TryChant(Me, skill, out var why)) Say(why, 1.5f);
            else { Sfx.Play("select"); Haptics.Play(Haptics.Kind.Light); }
        }

        void OnUtterance(float[][] seq, float peak)
        {
            if (recordingSkill != null)
            {
                voice.AddSample(recordingSkill, seq, peak);
                int n = voice.Count(recordingSkill);
                Say($"🎤 {Skills.All[recordingSkill].name}：樣本 {n}/2" + (n >= 2 ? " ✔" : "，再唸一次"), 2f);
                if (n >= 2) { voice.Save(); recordingSkill = null; }
                return;
            }
            if (phase != Phase.Fighting || battle == null || frozen) return;
            var r = voice.Classify(seq, peak, myLoadout);
            if (r.id == null) { heard = $"🎤 {r.reason}"; heardUntil = Time.time + 1.5f; return; }
            var skill = Skills.All[r.id];
            heard = $"🎤 {skill.name}"; heardUntil = Time.time + 1.5f;
            if (!battle.TryChant(Me, skill, out var why)) Say(why, 1.5f);
            else { Sfx.Play("select"); Haptics.Play(Haptics.Kind.Light); }
        }

        /// <summary>敵人有沒有出現在我的畫面中（頭、胸、腳任一處），並算出鎖定框</summary>
        void UpdateLock()
        {
            enemyOnScreen = false;
            var en = En;
            if (en == null || !en.Alive || !Tracking.Ok) return;
            if (NetMode && !coop)
            {
                // 雙人：鏡頭真的看到對手才算鎖定（GameRoot 的人體偵測）
                enemyOnScreen = NetLock; enemyRect = NetLockRect;
                enemyScreenSide = Vector3.Dot(WorldFrame.FromWorld(en.Chest) - cam.transform.position, cam.transform.right);
                return;
            }
            var pts = new[] { en.head + Vector3.up * 0.15f, en.Chest, en.Feet };
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            int inView = 0;
            foreach (var pm in pts)
            {
                var p = WorldFrame.FromWorld(pm);
                var v = cam.WorldToViewportPoint(p);
                if (v.z < 0.2f) continue;
                if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) inView++;
                var sp = cam.WorldToScreenPoint(p);
                xMin = Mathf.Min(xMin, sp.x); xMax = Mathf.Max(xMax, sp.x);
                yMin = Mathf.Min(yMin, sp.y); yMax = Mathf.Max(yMax, sp.y);
            }
            enemyOnScreen = inView > 0;
            // 不在畫面中：敵人在我的左邊還是右邊
            var toEn = WorldFrame.FromWorld(en.Chest) - cam.transform.position;
            enemyScreenSide = Vector3.Dot(toEn, cam.transform.right);
            if (enemyOnScreen)
            {
                float depth = Mathf.Max(0.3f, Vector3.Dot(toEn, cam.transform.forward));
                float halfW = en.bodyRadius / depth * Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                float cx = (xMin + xMax) * 0.5f;
                enemyRect = Rect.MinMaxRect(cx - halfW, Screen.height - yMax, cx + halfW, Screen.height - yMin);
            }
        }

        void OnBattleEvent(string kind, Vector3 at, Color c, string text)
        {
            switch (kind)
            {
                case "hit": case "trap": case "mitigate": case "effect": case "counter": case "buff": case "heal": case "trapSet": case "slice":
                    if (!string.IsNullOrEmpty(text)) floaters.Add(new Floater { posMap = at, text = text, color = c, born = Time.time });
                    if (kind == "hit" || kind == "trap")
                    {
                        // 命中特效：依打中的技能挑職業（普攻、召喚物也算；不認得就用自己的職業），傷害越高越大
                        var hs = battle.HitSkill;
                        string fxCls = SpellFx.HitClassOf(hs) ?? (kind == "trap" ? "archer" : myClass);
                        lastHitCls = fxCls;
                        float hd = DmgOf(text); if (hd <= 0f && hs != null) hd = hs.damage;
                        SpellFx.Hit(fxCls, hs != null ? hs.color : c, WorldFrame.FromWorld(at), 0.8f + hd / 60f, battle.HitCrit);
                    }
                    // 音效＋震動：打中敵人（傷害越高越重）、格擋、回復、增益、陷阱
                    switch (kind)
                    {
                        case "hit": case "trap":
                        {
                            float d = DmgOf(text);
                            float hv = Mathf.Clamp(0.55f + d / 60f, 0.55f, 1f);
                            Sfx.Play(kind == "trap" ? "trap" : "hit", hv * 0.6f, d >= 40f ? 0.8f : 1f);
                            if (kind == "hit") Sfx.Play("hit_" + (lastHitCls ?? myClass), hv);   // 各職業的命中聲
                            if (kind == "trap") Sfx.Play("hit", 0.7f);
                            Haptics.Play(d >= 30f ? Haptics.Kind.Medium : Haptics.Kind.Light);
                            bool heavy = d >= 30f || battle.HitCrit;   // 重擊、爆擊：畫面晃更大、停頓更久
                            Kick(heavy ? 11f : 6f, heavy ? 0.1f : 0.05f);
                            break;
                        }
                        case "slice": Burst(at, c); Sfx.Play("block", 1f, 1.3f); Sfx.Play("cast_swordsman", 0.6f); Haptics.Play(Haptics.Kind.Medium); Kick(4f, 0.04f); break;
                        case "mitigate": case "counter": Sfx.Play("block"); Haptics.Play(Haptics.Kind.Medium); Kick(5f, 0.05f); break;
                        case "heal": Sfx.Play("heal"); break;
                        case "buff": Sfx.Play("cast_priest", 0.6f); break;
                        case "trapSet": Sfx.Play("trap", 0.5f); break;
                    }
                    break;
                case "cast":
                {
                    if (bossMode && Vector3.Distance(at, Me.head) >= 0.8f)
                    {
                        // Boss 出手：出招動作；合作主機告訴隊友播動作／特效
                        if (bossRig && bossWindup != null) bossRig.Attack(bossWindup);
                        if (coop && NetIsHost) NetSend?.Invoke(new Msg { t = "bfx", k = bossWindup ?? "", p = En.Feet });
                        if (bossWindup != "b_slam") Sfx.Play("cast_mage", 0.6f, 0.6f);
                        break;
                    }
                    // 放招：自己的放招聲比較大；對手的小一點（聽得出來敵人出手了）
                    bool mine = Vector3.Distance(at, Me.head) < 0.8f;
                    Sfx.Cast(mine ? myClass : enemyClassUsed, mine ? 1f : 0.55f);
                    if (mine) Haptics.Play(Haptics.Kind.Medium);
                    break;
                }
                case "hurt":
                    // 被打中：震動＋整個畫面閃紅＋扣血（血量已在 Battle 裡扣掉）
                    floaters.Add(new Floater { text = text, color = new Color(1f, 0.3f, 0.3f), born = Time.time, screen = true });
                    hitFlash = 1f;
                    {
                        float d = DmgOf(text);
                        Sfx.Play("hurt", Mathf.Clamp(0.6f + d / 50f, 0.6f, 1f));
                        Haptics.Play(Haptics.Kind.Heavy);
                        Kick(d >= 30f ? 22f : 12f, d >= 30f ? 0.12f : 0.05f);
                    }
                    break;
                case "armed":
                    floaters.Add(new Floater { posMap = at, text = text, color = c, born = Time.time, screen = at == Me.Chest });
                    if (at == Me.Chest) { SpellFx.CastBurst(myClass, c, cam.transform.position + cam.transform.forward * 0.5f - cam.transform.up * 0.08f, cam.transform.forward, 0.35f); Sfx.Cast(myClass); Haptics.Play(Haptics.Kind.Medium); }
                    break;
                case "ambush":
                    // 伏擊出手：在被刺的人身上爆開
                    floaters.Add(new Floater { posMap = at, text = text, color = c, born = Time.time });
                    SpellFx.Impact("assassin", c, WorldFrame.FromWorld(at), 1.2f);
                    Sfx.Play("hit", 1f, 1.2f); Kick(8f, 0.08f);
                    break;
                case "armedExpire":
                    if (at == Me.Chest) Say(text + "（5 秒內沒靠近）", 1.5f);
                    break;
                case "burnzone":
                    SpellFx.Impact("mage", c, WorldFrame.FromWorld(at), 2.5f);   // 隕石落地爆炸
                    Sfx.Play("boom", Mathf.Clamp01(1.2f - Battle.FlatDistance(at, Me.Feet) / 15f));
                    if (Battle.FlatDistance(at, Me.Feet) < 4f) { Haptics.Play(Haptics.Kind.Heavy); Kick(16f, 0f); }
                    break;
                case "burn":
                {
                    bool meBurn = Battle.FlatDistance(at, Me.Feet) < 0.5f || at == Me.Chest;
                    floaters.Add(new Floater { posMap = at + Vector3.up * 1.2f, text = "灼燒 " + text, color = new Color(1f, 0.55f, 0.2f), born = Time.time, screen = meBurn });
                    if (meBurn) { hitFlash = Mathf.Max(hitFlash, 0.45f); Haptics.Play(Haptics.Kind.Medium); Kick(4f, 0f); }
                    break;
                }
                case "dot":
                    bool onMe = at == Me.Chest;
                    floaters.Add(new Floater { posMap = at, text = "毒 " + text, color = Color.green, born = Time.time, screen = onMe });
                    if (onMe) hitFlash = Mathf.Max(hitFlash, 0.4f);   // 中毒扣血：淡一點的紅
                    break;
                case "miss":
                    if (Vector3.Distance(at, Me.Chest) < 6f) Sfx.Play("fizz", 0.35f);   // 法術飛超過射程淡出
                    break;
                case "crit":
                    floaters.Add(new Floater { posMap = at + Vector3.up * 0.3f, text = "💥 " + text, color = new Color(1f, 0.85f, 0.3f), born = Time.time });
                    Sfx.Play("crit", 0.9f);
                    break;
                case "evade":
                {
                    bool meEvade = Vector3.Distance(at, Me.Chest) < 0.6f;
                    floaters.Add(new Floater { posMap = at + Vector3.up * 0.4f, text = (meEvade ? "✨ " : "") + text, color = meEvade ? new Color(0.5f, 1f, 1f) : new Color(1f, 0.8f, 0.4f), born = Time.time, screen = meEvade });
                    if (meEvade) { Sfx.Play("dodge"); Haptics.Play(Haptics.Kind.Success); }
                    break;
                }
                case "timeout":
                    if (at == Me.Chest) Say("詠唱逾時", 1.5f);
                    break;
                case "fail":
                    // 刺殺準備：移動太遠失敗／被打斷
                    if (at == Me.Chest) { Say(text, 2f); Sfx.Play("block", 0.6f, 0.6f); Haptics.Play(Haptics.Kind.Warning); }
                    else floaters.Add(new Floater { posMap = at + Vector3.up * 0.3f, text = text, color = new Color(0.8f, 0.8f, 0.85f), born = Time.time });
                    break;
                case "win": case "lose": ShowResult(); break;
                case "down":
                    // 合作：我倒下了，隊友還活著
                    Say("💀 你倒下了！等隊友打倒 Boss…", 99f); Sfx.Play("lose", 0.6f); Haptics.Play(Haptics.Kind.Warning);
                    break;
                case "allyhit":
                    floaters.Add(new Floater { posMap = at + Vector3.up * 0.3f, text = "隊友受傷", color = new Color(1f, 0.5f, 0.4f), born = Time.time });
                    Sfx.Play("hit", 0.4f, 0.7f);
                    break;
                case "slam":
                    BossSlamFx(at);
                    break;
                case "roots":
                {
                    // 打在目標位置的範圍攻擊：樹根爆發／觸手重擊／漩渦
                    var rs = Skills.All.TryGetValue(text ?? "", out var rsk) ? rsk : Skills.All["p_roots"];
                    if (rs.id == "s_tentacle") BossRig.TentacleSlam(WorldFrame.FromWorld(at), rs.areaRadius);
                    else if (rs.id == "s_whirl") BossRig.Whirlpool(WorldFrame.FromWorld(at), rs.areaRadius, 1.5f);
                    else BossRig.RootEruption(WorldFrame.FromWorld(at), rs.areaRadius);
                    float dd = Battle.FlatDistance(at, Me.Feet);
                    Sfx.Play("boom", Mathf.Clamp01(1f - dd / 12f), 0.8f);
                    if (dd < 4f) { Haptics.Play(Haptics.Kind.Heavy); Kick(10f, 0f); }
                    break;
                }
                case "poisonsplash":
                    PlantFx.PoisonSplash(WorldFrame.FromWorld(at), Battle.PoolRadius);
                    Sfx.Play("hit", 0.4f, 1.5f);
                    break;
                case "inksplat":
                    SeaFx.InkSplat(WorldFrame.FromWorld(at), Skills.All["s_ink"].radius);
                    Sfx.Play("hit", 0.5f, 0.7f);
                    break;
                case "shock":
                    SeaFx.Shock(WorldFrame.FromWorld(at), Skills.All["m_shock"].areaRadius);
                    Sfx.Play("cast_mage", 0.5f, 1.6f);
                    if (Battle.FlatDistance(at, Me.Feet) < 2.5f) Haptics.Play(Haptics.Kind.Medium);
                    break;
                case "splash":
                    SeaFx.Splash(WorldFrame.FromWorld(at), 1f);
                    Sfx.Play("dodge", 0.5f, 0.5f);
                    break;
                case "sporeburst":
                    PlantFx.SporeBurst(WorldFrame.FromWorld(at), Skills.All["m_spore"].areaRadius);
                    Sfx.Play("boom", 0.35f, 1.8f);
                    break;
                case "poolstep": case "vinehit":
                    if (!string.IsNullOrEmpty(text)) floaters.Add(new Floater { posMap = at + Vector3.up * 0.5f, text = text, color = c, born = Time.time });
                    break;
                case "summon":
                    Sfx.Play("roar", 0.8f, 1.2f);
                    if (!string.IsNullOrEmpty(text)) Say("🔥 " + text + "（小怪會追著你抓，先清掉牠們！）", 3f);
                    break;
                case "minionspawn":
                    if (battle.Theme == 1) MinionRig.SpawnFxPlant(WorldFrame.FromWorld(at));
                    else if (battle.Theme == 2) MinionRig.SpawnFxSea(WorldFrame.FromWorld(at));
                    else MinionRig.SpawnFx(WorldFrame.FromWorld(at));
                    Sfx.Play("boom", 0.4f, 1.6f);
                    break;
                case "miniondie":
                    floaters.Add(new Floater { posMap = at + Vector3.up * 0.9f, text = "擊倒小怪", color = new Color(1f, 0.7f, 0.3f), born = Time.time });
                    Sfx.Play("trap", 0.7f, 0.8f);
                    break;
                case "minionwindup":
                    if (Battle.FlatDistance(at, Me.Feet) < 2f) { Sfx.Play("warn", 0.4f, 1.3f); Haptics.Play(Haptics.Kind.Light); }
                    break;
                case "minionattack":
                    Sfx.Play("cast_assassin", 0.5f, 0.8f);
                    break;
                case "minionexplode":
                {
                    // 自爆小鬼爆炸
                    BossRig.SlamShockwave(WorldFrame.FromWorld(at), Skills.All["m_bomb"].areaRadius, c);
                    float dd = Battle.FlatDistance(at, Me.Feet);
                    Sfx.Play("boom", Mathf.Clamp01(1f - dd / 10f), 1.2f);
                    if (dd < 3f) { Haptics.Play(Haptics.Kind.Heavy); Kick(12f, 0f); }
                    break;
                }
                case "bossarrive":
                    Say("🔥 " + text, 3.5f);
                    break;
                case "bossincoming":
                    Say("⚠ " + text, 2.5f);
                    Sfx.Play("roar", 0.7f); Haptics.Play(Haptics.Kind.Heavy); Kick(14f, 0f);
                    break;
                case "quake":
                    Sfx.Play("boom", 0.9f); Haptics.Play(Haptics.Kind.Medium); Kick(8f, 0f);
                    break;
                case "wavestart":
                    Say("♾ " + text, 2.5f);
                    Sfx.Play("horn", 0.8f, text != null && text.Contains("Boss") ? 0.75f : 1f);
                    break;
                case "wavecleared":
                    Say("✨ " + text + " 回復 10% 血量", 2.5f);
                    Sfx.Play("heal", 0.8f); Haptics.Play(Haptics.Kind.Success);
                    break;
                case "bossdown":
                    Say("🏆 " + text + "（回復 30% 血量）", 3.5f);
                    Sfx.Play("win", 0.8f); Haptics.Play(Haptics.Kind.Success);
                    break;
                case "dispel":
                    // 抗拒法環：地上的法陣＋衝擊波
                    SpellFx.DispelRing(WorldFrame.FromWorld(at), Skills.All["dispel"].areaRadius, c);
                    Sfx.Play("cast_mage", 0.9f, 0.7f); Sfx.Play("block", 0.6f, 0.8f);
                    Haptics.Play(Haptics.Kind.Medium); Kick(6f, 0f);
                    break;
                case "aoe":
                {
                    // Presentation only: skill-specific shapes use the existing event center and radius.
                    float rad = Skills.All.TryGetValue(text ?? "", out var asd) ? asd.areaRadius : 1.5f;
                    AdvancedFx.Area(text, WorldFrame.FromWorld(at), rad, c);
                    Sfx.Play(text == "frostfield" ? "cast_mage" : text == "detonate" ? "boom" : "cast_swordsman", 0.8f, 0.8f);
                    Haptics.Play(Haptics.Kind.Medium); Kick(6f, 0.04f);
                    break;
                }
                case "fogzone":
                    AdvancedFx.FogCast(WorldFrame.FromWorld(at));
                    Sfx.Play("cast_assassin", 0.7f, 0.7f);
                    break;
                case "petspawn":
                    AdvancedFx.Summon(text, WorldFrame.FromWorld(at), c);
                    Sfx.Play("summon", 0.7f, text == "golem" ? 0.6f : 1.3f);
                    break;
                case "petgone":
                    AdvancedFx.Dismiss(text, WorldFrame.FromWorld(at));
                    break;
                case "basicmelee":
                    // 近身揮砍（普攻、斬擊）：往前的新月斬擊弧
                    {
                        bool mineSwing = Battle.FlatDistance(at, Me.Feet) < 0.7f;
                        var swingDir = mineSwing ? cam.transform.forward : WorldFrame.DirFromWorld(Fighter.Flat(Me.Chest - at));
                        string swingCls = text == "na_dagger" ? "assassin" : "swordsman";
                        SpellFx.CastBurst(swingCls, c, WorldFrame.FromWorld(at + Vector3.up * 1f), swingDir, text == "slash" ? 1.4f : 0.9f);
                    }
                    Sfx.Play("cast_swordsman", 0.45f, text == "na_dagger" ? 1.4f : 1.1f);
                    break;
                case "taunt":
                    Say("😤 " + text, 1.8f);
                    Sfx.Play("roar", 0.5f, 1.2f);
                    break;
                case "dispelled":
                {
                    bool meHit = Vector3.Distance(at, Me.Chest) < 0.6f;
                    floaters.Add(new Floater { posMap = at + Vector3.up * 0.4f, text = meHit ? "蓄力被打斷！" : "打斷！", color = c, born = Time.time, screen = meHit });
                    if (meHit) { Haptics.Play(Haptics.Kind.Warning); Sfx.Play("warn", 0.6f); }
                    break;
                }
            }
        }

        // ================================================================ 3D 顯示
        // 召喚物的外觀（獵鷹、火靈、石像、分身）
        readonly Dictionary<Pet, PetRig> petGo = new Dictionary<Pet, PetRig>();

        void UpdatePetViews()
        {
            var live = new HashSet<Pet>(battle.pets);
            foreach (var p in battle.pets)
            {
                if (!p.alive) continue;
                if (!petGo.TryGetValue(p, out var rig) || rig == null)
                {
                    var ownerCol = p.owner != null && p.owner.cls != null ? p.owner.cls.color : Color.white;
                    rig = PetRig.Create(p.kind, ownerCol);
                    petGo[p] = rig;
                }
                var forward = p.owner != null ? p.owner.forward : Vector3.forward;
                forward.y = 0f;
                var rotation = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward.normalized, Vector3.up) : Quaternion.identity;
                rig.SetPose(WorldFrame.FromWorld(p.pos), WorldFrame.RotFromWorld(rotation),
                    Time.time + p.id * 0.37f, p.maxHp > 0f ? p.hp / p.maxHp : 1f);
            }
            foreach (var kv in new List<KeyValuePair<Pet, PetRig>>(petGo))
                if (!live.Contains(kv.Key) || !kv.Key.alive)
                {
                    if (kv.Value != null) Destroy(kv.Value.gameObject);
                    petGo.Remove(kv.Key);
                }
        }

        void UpdateVisuals()
        {
            UpdatePetViews();
            UpdateQuakeVisuals();
            UpdatePickups();
            UpdateAllyRigs();
            var en = En;
            var feetS = WorldFrame.FromWorld(en.Feet);
            UpdateChargeFx();
            // 敵人角色：腳的位置、面向、走路速度、詠唱、被打中、倒地
            var fwdS = WorldFrame.DirFromWorld(en.forward);
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            if (enemyRig)
            {
                enemyRig.SetPose(feetS, fwdS);
                enemyRig.MoveSpeed = Mathf.Lerp(enemyRig.MoveSpeed, Fighter.Flat(en.Feet - lastEnemyFeet).magnitude / dt, 0.2f);
                enemyRig.Charge = en.charging != null ? Mathf.Max(0.01f, battle.ChargeProgress(en)) : 0f;
            }
            if (bossRig)
            {
                // Boss：位置、面向、走路；開始蓄力＝預備動作（時間＝蓄力時間）
                var rot = fwdS.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(new Vector3(fwdS.x, 0f, fwdS.z).normalized, Vector3.up) : bossRig.transform.rotation;
                bossRig.transform.SetPositionAndRotation(feetS, rot);
                bossRig.MoveSpeed = Mathf.Lerp(bossRig.MoveSpeed, Fighter.Flat(en.Feet - lastEnemyFeet).magnitude / dt, 0.2f);
                string cid = en.charging != null ? en.charging.id : null;
                if (cid != null && cid != bossChargingId)
                {
                    bossWindup = cid;
                    bossRig.Windup(cid, Mathf.Max(0.3f, en.charging.charge * en.chargeScale * (1f - battle.ChargeProgress(en))));
                    if (cid == "b_rain") Sfx.Play("roar", 0.7f);
                }
                bossChargingId = cid;
                if (en.hp < lastBossHp - 0.01f) bossRig.PlayHit();
                lastBossHp = en.hp;
                if (!endless && !en.Alive && !enemyDeadShown) bossRig.SetDead(true);
                if (endless)
                {
                    // 無盡模式：Boss 出現（從地底冒出）／倒下（崩塌，3 秒後隱藏）
                    if (en.Alive && !bossAliveShown && bossRig.Theme != battle.Theme)
                    {
                        // 換主題的 Boss（熔岩巨像 ↔ 腐化樹王）：重建外觀
                        Destroy(bossRig.gameObject);
                        bossRig = BossRig.Create(battle.Theme);
                        bossRig.transform.SetPositionAndRotation(feetS, rot);
                    }
                    if (en.Alive && !bossAliveShown)
                    {
                        bossAliveShown = true; bossHideAt = -1f; bossEnragedShown = false;
                        bossRig.SetDead(false); bossRig.SetEnraged(false); bossRig.SetVisible(true);
                        bossRig.transform.position = feetS;
                        SpawnFade.Play(bossRig.gameObject);   // 淡入出現
                        if (battle.Theme == 1) BossRig.RootEruption(feetS, 2.5f);
                        else if (battle.Theme == 2) SeaFx.Splash(feetS, 3f);
                        else BossRig.SlamShockwave(feetS, 2.5f, new Color(1f, 0.35f, 0.1f));
                        Sfx.Play("roar"); Haptics.Play(Haptics.Kind.Heavy); Kick(14f, 0f);
                    }
                    else if (!en.Alive && bossAliveShown)
                    {
                        bossAliveShown = false; bossRig.SetDead(true); bossHideAt = Time.time + 3f;
                    }
                    if (bossHideAt > 0f && Time.time > bossHideAt) { bossRig.SetVisible(false); bossHideAt = -1f; }
                }
                // 地裂重擊蓄力中：Boss 周圍的紅圈（站在圈裡會被打）
                bool slamming = en.charging != null && en.charging.areaRadius > 0f && en.Alive;
                if (slamming)
                {
                    if (!slamGo) slamGo = Prim(PrimitiveType.Cylinder, new Color(1f, 0.1f, 0.05f, 0.3f), true);
                    float rr = en.charging.areaRadius + Fighter.BodyRadius;
                    slamGo.SetActive(true);
                    var slamC = en.charging.id == "b_leap" || en.charging.atTarget ? en.aimPoint : en.Feet;   // 熔岩躍擊／樹根爆發：紅圈在落點
                    slamGo.transform.position = WorldFrame.FromWorld(new Vector3(slamC.x, 0.02f, slamC.z));
                    slamGo.transform.localScale = new Vector3(rr * 2f, 0.003f, rr * 2f);
                    float pr = battle.ChargeProgress(en), k = 0.5f + 0.5f * Mathf.Sin(Time.time * (6f + 12f * pr));
                    slamGo.GetComponent<Renderer>().material.color = new Color(1f, 0.12f, 0.05f, (0.15f + 0.3f * pr) * (0.6f + 0.4f * k));
                }
                else if (slamGo) slamGo.SetActive(false);
            }
            UpdateMinionVisuals(dt);
            lastEnemyFeet = en.Feet;
            if (en.hp < lastEnemyHp - 0.01f) { if (enemyRig) enemyRig.PlayHit(); if (dummyMode) { dummyDamage += lastEnemyHp - en.hp; dummyHits++; } }
            lastEnemyHp = en.hp;
            if (!en.Alive && !enemyDeadShown) { if (enemyRig) enemyRig.SetDead(true); enemyDeadShown = true; }
            bool guarded = en.blockUntil > battle.now || en.shieldUntil > battle.now || en.counterUntil > battle.now || en.armorUntil > battle.now;
            if (enemyRig) enemyRig.Guarding = guarded && en.Alive;
            enemyShield.SetActive(guarded && en.Alive && !NetMode);   // 雙人：對手是真人，不蓋護盾球（改用方框顏色表示）
            if (guarded)
            {
                enemyShield.transform.position = WorldFrame.FromWorld(en.Chest);
                enemyShield.transform.localScale = new Vector3(0.9f, 1.9f, 0.9f);
                var col = en.armorUntil > battle.now ? new Color(0.99f, 0.83f, 0.3f, 0.28f) : en.counterUntil > battle.now ? new Color(0.96f, 0.45f, 0.71f, 0.3f) : en.shieldUntil > battle.now ? new Color(0.6f, 0.65f, 0.7f, 0.3f) : new Color(0.38f, 0.65f, 0.98f, 0.3f);
                enemyShield.GetComponent<Renderer>().material.color = col;
            }


            // 法術：球體＋拖尾
            var alive = new HashSet<Projectile>(battle.projectiles);
            foreach (var p in battle.projectiles)
            {
                if (!projGo.TryGetValue(p, out var go))
                {
                    // 依技能的法術造型（箭、火球、冰錐、雷光、劍氣、飛刀…），已含光暈與拖尾
                    string fxCls = p.owner == En ? enemyClassUsed : (p.owner.isAlly ? p.owner.cls.id : myClass);
                    go = (bossMode && (p.owner == En || p.owner.isMinion || p.skill.cls == "minion" || p.skill.cls == "boss") ? BossRig.CreateProjectileVisual(p.skill.id) : null) ?? SpellFx.CreateProjectileVisual(p.skill, fxCls);
                    projGo[p] = go;
                    if (p.owner == En && !bossMode)
                    {
                        if (enemyRig) enemyRig.PlayCast();   // 敵人出招動作＋光效
                        SpellFx.CastBurst(enemyClassUsed, p.skill.color, WorldFrame.FromWorld(p.pos), WorldFrame.DirFromWorld(p.dir), 1f);
                        if (NetMode) Sfx.Cast(enemyClassUsed, 0.55f);   // 雙人：對手的法術不會經過 Battle 的 cast 事件
                    }
                }
                var dirS = WorldFrame.DirFromWorld(p.Heading);   // 弧線（風刃）朝著路徑的切線
                go.transform.SetPositionAndRotation(WorldFrame.FromWorld(p.pos), dirS.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dirS, Vector3.up) : Quaternion.identity);
                go.transform.localScale = Vector3.one * p.FadeK;   // 飛超過射程：縮小淡出
                // 敵人的法術逼近時畫面邊框閃紅
                if ((p.owner == En || p.owner.isMinion) && Vector3.Distance(p.pos, Me.Chest) < 2.5f && Vector3.Dot(p.dir, Me.Chest - p.pos) > 0)
                {
                    // 法術逼近：警告音＋震動（同一波只響一次）
                    if (incomingFlash < 0.05f && Time.time - lastWarn > 0.8f) { lastWarn = Time.time; Sfx.Play("warn", 0.7f); Haptics.Play(Haptics.Kind.Warning); }
                    incomingFlash = Mathf.Max(incomingFlash, 0.6f);
                }
            }
            foreach (var kv in new List<KeyValuePair<Projectile, GameObject>>(projGo))
                if (!alive.Contains(kv.Key))
                {
                    // Boss 的落石落地：小型衝擊波＋悶響
                    if (kv.Key.skill.id == "b_rain" && kv.Key.pos.y < 0.6f)
                    {
                        BossRig.SlamShockwave(WorldFrame.FromWorld(new Vector3(kv.Key.pos.x, 0f, kv.Key.pos.z)), 0.9f, kv.Key.skill.color);
                        Sfx.Play("boom", Mathf.Clamp01(0.9f - Battle.FlatDistance(kv.Key.pos, Me.Feet) / 12f), 1.3f);
                    }
                    Destroy(kv.Value, 0.25f); projGo.Remove(kv.Key);
                }

            // 火海：地上的燃燒圓盤（閃爍）＋不時冒出火焰
            var liveBurns = new HashSet<BurnZone>(battle.burns);
            foreach (var z in battle.burns)
            {
                if (!burnGo.TryGetValue(z, out var go))
                {
                    if (z.skill != null && z.skill.self == SelfKind.Fog)
                        go = PoisonFogView.Create(z.radius);
                    else
                    {
                        go = Prim(PrimitiveType.Cylinder, new Color(1f, 0.35f, 0.05f, 0.45f), true);
                        go.transform.localScale = new Vector3(z.radius * 2f, 0.004f, z.radius * 2f);
                    }
                    burnGo[z] = go;
                }
                go.transform.position = WorldFrame.FromWorld(z.pos + Vector3.up * 0.015f);
                float left = Mathf.Clamp01((z.until - battle.now) / 2f);   // 最後 2 秒淡出
                float flick = 0.35f + 0.15f * Mathf.Sin(Time.time * 13f + z.pos.x * 7f) + 0.1f * Mathf.Sin(Time.time * 29f);
                if (z.skill != null && z.skill.self == SelfKind.Fog)
                {
                    // 影舞者的毒霧：綠色、慢慢翻騰
                    go.transform.rotation = WorldFrame.RotFromWorld(Quaternion.identity);
                    go.GetComponent<PoisonFogView>().SetFade(left);
                    continue;
                }
                go.GetComponent<Renderer>().material.color = new Color(1f, 0.3f + 0.15f * flick, 0.05f, flick * left);
                if (UnityEngine.Random.value < Time.deltaTime * 2.2f)
                {
                    var r2 = UnityEngine.Random.insideUnitCircle * z.radius * 0.85f;
                    SpellFx.Impact("mage", new Color(1f, 0.45f, 0.1f), WorldFrame.FromWorld(z.pos + new Vector3(r2.x, 0.05f, r2.y)), 0.6f);
                }
            }
            foreach (var kv in new List<KeyValuePair<BurnZone, GameObject>>(burnGo))
                if (!liveBurns.Contains(kv.Key)) { Destroy(kv.Value); burnGo.Remove(kv.Key); }

            // 攻擊預警①：敵人的攻擊已經放出來（方向不會再變）→ 地上畫出它的飛行路線（紅線）
            {
                var liveLines = new HashSet<Projectile>();
                foreach (var p in battle.projectiles)
                {
                    bool hostile = p.owner == En || p.owner.isMinion || (p.remote && !p.owner.isAlly);
                    if (!p.alive || p.Fading || !hostile || p.lob || p.skill.fromAbove || p.Curved || p.skill.areaRadius > 0f) continue;
                    if (p.target != null && p.target != Me) continue;   // 合作：打隊友的不畫
                    liveLines.Add(p);
                    if (!projLine.TryGetValue(p, out var ln) || !ln) { ln = Prim(PrimitiveType.Cube, new Color(1f, 0.15f, 0.15f, 0.4f), true); projLine[p] = ln; }
                    var a = p.pos; a.y = 0.02f;
                    float left = Mathf.Max(0.1f, p.skill.rangeMax * p.rangeMul - p.traveled);
                    var d = Fighter.Flat(p.dir); if (d == Vector3.zero) d = Vector3.forward;
                    float len = left * Mathf.Max(0.2f, new Vector2(p.dir.x, p.dir.z).magnitude);
                    var b = a + d * len;
                    ln.transform.position = WorldFrame.FromWorld((a + b) / 2f);
                    ln.transform.rotation = WorldFrame.RotFromWorld(Quaternion.LookRotation(d, Vector3.up));
                    ln.transform.localScale = new Vector3(p.skill.duckable ? 0.6f : Mathf.Max(0.12f, p.skill.radius * 2f), 0.004f, len);
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                    ln.GetComponent<Renderer>().material.color = new Color(1f, p.skill.duckable ? 0.5f : 0.15f, 0.1f, 0.3f + 0.25f * pulse);
                }
                foreach (var kv in new List<KeyValuePair<Projectile, GameObject>>(projLine))
                    if (!liveLines.Contains(kv.Key)) { if (kv.Value) Destroy(kv.Value); projLine.Remove(kv.Key); }
                // 衝刺路線（荊棘衝鋒獸開始衝的那一刻才畫）
                foreach (var kv in new List<KeyValuePair<Battle.Lane, GameObject>>(laneGo))
                    if (!kv.Key.alive || battle.now > kv.Key.until) { if (kv.Value) Destroy(kv.Value); laneGo.Remove(kv.Key); }
            }
            // 攻擊預警②：隕石下落中 → 落點的紅圈（走出紅圈就閃得掉）
            var liveLand = new HashSet<Projectile>();
            foreach (var p in battle.projectiles)
            {
                // 從天而降（往下飛）或拋物線毒液彈（已經放出，落點固定）→ 落點紅圈
                if (!(p.lob || (p.skill.fromAbove && p.dir.y <= -0.5f))) continue;
                liveLand.Add(p);
                if (!landGo.TryGetValue(p, out var lg))
                {
                    lg = Prim(PrimitiveType.Cylinder, p.lob ? new Color(0.6f, 1f, 0.2f, 0.35f) : new Color(1f, 0.1f, 0.1f, 0.35f), true);
                    float lr = p.skill.id == "meteor" ? Battle.BurnRadius : p.skill.radius + Fighter.BodyRadius;
                    lg.transform.localScale = new Vector3(lr * 2f, 0.003f, lr * 2f);
                    landGo[p] = lg;
                }
                var lp = p.lob ? p.landAt : p.pos;
                lg.transform.position = WorldFrame.FromWorld(new Vector3(lp.x, 0.025f, lp.z));
                float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                lg.GetComponent<Renderer>().material.color = new Color(1f, 0.1f, 0.1f, 0.2f + 0.25f * k);
            }
            foreach (var kv in new List<KeyValuePair<Projectile, GameObject>>(landGo))
                if (!liveLand.Contains(kv.Key)) { Destroy(kv.Value); landGo.Remove(kv.Key); }

            // 陷阱：地板上的扁圓盤（生效前是灰色）
            var liveTraps = new HashSet<Trap>(battle.traps);
            foreach (var t in battle.traps)
            {
                if (!trapGo.TryGetValue(t, out var go))
                {
                    go = Prim(PrimitiveType.Cylinder, Color.gray, true);
                    go.transform.localScale = new Vector3(t.skill.radius * 2f, 0.005f, t.skill.radius * 2f);
                    trapGo[t] = go;
                }
                go.transform.position = WorldFrame.FromWorld(t.pos + Vector3.up * 0.01f);
                go.GetComponent<Renderer>().material.color = t.Armed(battle.now) ? WithAlpha(t.skill.color, 0.6f) : new Color(0.6f, 0.6f, 0.6f, 0.4f);
            }
            foreach (var kv in new List<KeyValuePair<Trap, GameObject>>(trapGo))
                if (!liveTraps.Contains(kv.Key)) { Burst(kv.Key.pos, kv.Key.skill.color); Destroy(kv.Value); trapGo.Remove(kv.Key); }

            incomingFlash = Mathf.Max(0f, incomingFlash - Time.deltaTime * 2f);
        }

        /// <summary>詠唱光效：我的在手機前方（小），敵人的在角色手邊；換招或結束就收掉</summary>
        void UpdateChargeFx()
        {
            var mine = Me.charging;
            if (mine != myFxSkill)
            {
                if (myChargeFx) myChargeFx.Stop();
                myChargeFx = mine != null ? SpellFx.StartCharge(myClass, mine.color, cam.transform, new Vector3(0f, -0.15f, 0.45f), 0.35f) : null;
                myFxSkill = mine;
            }
            if (myChargeFx) myChargeFx.Progress = battle.ChargeProgress(Me);

            var theirs = En.Alive ? En.charging : null;
            if (theirs != enemyFxSkill)
            {
                if (enemyChargeFx) enemyChargeFx.Stop();
                var anchor = enemyRig ? enemyRig.transform : remoteAnchor;
                enemyChargeFx = theirs != null && anchor ? SpellFx.StartCharge(enemyClassUsed, theirs.color, anchor, new Vector3(0f, 1.2f, 0.4f), 1f) : null;
                enemyFxSkill = theirs;
            }
            if (enemyChargeFx) enemyChargeFx.Progress = battle.ChargeProgress(En);
        }

        void Burst(Vector3 posMap, Color c)
        {
            var go = Prim(PrimitiveType.Sphere, WithAlpha(c, 0.8f));
            go.transform.position = WorldFrame.FromWorld(posMap);
            StartCoroutine(BurstAnim(go, c));
        }

        System.Collections.IEnumerator BurstAnim(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            for (float t = 0; t < 0.35f && go; t += Time.deltaTime)
            {
                go.transform.localScale = Vector3.one * (0.1f + t * 2.2f);
                r.material.color = WithAlpha(c, 0.8f * (1f - t / 0.35f));
                yield return null;
            }
            if (go) Destroy(go);
        }

        /// <summary>預警用（紅線、紅圈、陷阱、火海、瞄準線）：不做深度測試、最後才畫 → 敵人靠近時不會被敵人模型擋住</summary>
        Material hintMat;
        Material HintMat()
        {
            if (hintMat) return hintMat;
            var sh = Shader.Find("UI/Default");
            if (sh != null)
            {
                hintMat = new Material(sh);
                hintMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            else hintMat = new Material(mat);
            hintMat.renderQueue = 3500;
            return hintMat;
        }

        GameObject Prim(PrimitiveType type, Color c, bool onTop = false)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = onTop ? HintMat() : mat;
            r.material.color = c;
            return go;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
        void Say(string s, float secs) { message = s; messageUntil = Time.time + secs; }

        // ================================================================ 介面（GameRoot 在單人場地完成後呼叫）
        void Styles()
        {
            if (label != null) return;
            int fs = Mathf.RoundToInt(Screen.height / 46f);
            label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true }; label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = Mathf.RoundToInt(fs * 0.8f) };
            button = new GUIStyle(GUI.skin.button) { fontSize = fs, wordWrap = true };
            big = new GUIStyle(label) { fontSize = fs * 2, alignment = TextAnchor.MiddleCenter };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
        }

        public void DrawGUI()
        {
            Styles();
            // 技能說明視窗開著：底下的畫面照畫但不能按（GUI.enabled=false），說明視窗畫在最上面
            bool infoOpen = infoSkill != null && Skills.All.ContainsKey(infoSkill);
            bool talking = dlg != null;
            if (infoOpen || charOpen || storyMapOpen || talking) GUI.enabled = false;
            if (!(storyMapOpen && townWalk.Active)) DrawScreens();   // 3D 城鎮：不畫底下的選擇畫面（才看得到城鎮）
            GUI.enabled = true;
            if (talking) DrawDialogue();
            else if (charOpen) DrawCharacter();
            else if (storyMapOpen) DrawStoryMap();
            else if (infoOpen) DrawSkillInfo();
        }

        // ---------------------------------------------------------------- 技能詳細說明（點技能卡打開）
        string infoSkill;          // 正在看說明的技能（null＝沒開）

        void DrawSkillInfo()
        {
            var s = endless && phase != Phase.Setup || phase == Phase.Setup && EndlessSetup ? (Progress.Char(myClass).ModdedSkill(infoSkill) ?? Skills.All[infoSkill]) : Skills.All[infoSkill];   // 無盡模式：顯示技能樹強化後的數值
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.82f), fsL = Mathf.RoundToInt(fs * 1.3f);
            float pad = W * 0.04f, gap = pad * 0.5f;
            // 背景變暗；點視窗外面也會關掉
            GUI.color = new Color(0f, 0f, 0f, 0.6f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            var r = new Rect(pad, topY + pad, W - pad * 2, botY - topY - pad * 2);
            UiKit.Card(r, 0.94f, s.color, 0.9f);
            float isz = Mathf.Min(fs*4.5f,r.width*.27f);
            var ir = new Rect(r.x + gap * 1.5f, r.y + gap * 1.5f, isz, isz);
            UiKit.Badge(ir,SkillIcons.Get(s.id),s.color,2);
            float tx = ir.xMax + gap * 1.5f, tw = r.xMax - tx - gap * 1.5f;
            string clsName = Advanced.Get(s.adv)?.name ?? (Skills.Classes.TryGetValue(Skills.BasicClassOf(s) ?? s.cls, out var cd) ? cd.name : "");
            UiKit.Text(new Rect(tx, ir.y, tw, isz * 0.48f), s.name, fsL, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(tx, ir.y + isz * 0.48f, tw, isz * 0.26f), $"{clsName}・{SkillInfo.TypeName(s)}", fsS, s.color, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(tx, ir.y + isz * 0.74f, tw, isz * 0.26f), SkillInfo.CostLine(s), fsS, new Color(0.75f, 0.85f, 1f));

            bool inSetup = phase == Phase.Setup;
            float bh = fs * 2.4f, by = r.yMax - gap * 1.5f - bh;
            var body = new Rect(r.x + gap * 1.5f, ir.yMax + gap, r.width - gap * 3, by - gap - (ir.yMax + gap));
            UiKit.Card(body,.72f,s.color,.4f);
            body = new Rect(body.x+gap,body.y+gap,body.width-gap*2,body.height-gap*2);
            UiKit.Text(body, SkillInfo.Detail(s), fsS, new Color(0.92f, 0.94f, 1f), TextAnchor.UpperLeft, false, 0.5f);

            if (inSetup && SetupSkills is var list && Array.IndexOf(list, s.id) >= 0)
            {
                bool on = myLoadout.Contains(s.id);
                float bw = (r.width - gap * 4) / 2f;
                bool advSk = Advanced.IsAdvSkill(s.id);
                bool full = advSk ? AdvCount >= 1 : BaseCount >= 3;
                if (UiKit.Button(new Rect(r.x + gap * 1.5f, by, bw, bh), on ? "移除這個技能" : full ? (advSk ? "換到進階技能格" : "換上這個技能") : "✔ 選這個技能", fs, !on, on ? new Color(1f, 0.5f, 0.45f) : s.color))
                {
                    if (on) myLoadout.Remove(s.id);
                    else PickSkill(s.id);
                    infoSkill = null;
                }
                if (UiKit.Button(new Rect(r.x + gap * 2.5f + bw, by, bw, bh), "關閉", fs)) infoSkill = null;
            }
            else if (UiKit.Button(new Rect(r.x + gap * 1.5f, by, r.width - gap * 3, bh), "關閉", fs)) infoSkill = null;
            // 點視窗外面：關閉
            var e = Event.current;
            if (e.type == EventType.MouseDown && !r.Contains(e.mousePosition)) { infoSkill = null; e.Use(); }
        }

        void DrawScreens()
        {
            if (phase == Phase.Setup) DrawSetup();
            else if (phase == Phase.Fighting || phase == Phase.Over)
            {
                // 畫面震動：整個介面跟著抖
                var keep = GUI.matrix;
                if (shake > 0.5f)
                {
                    float sx = (Mathf.PerlinNoise(Time.time * 40f, 0f) - 0.5f) * 2f * shake, sy = (Mathf.PerlinNoise(0f, Time.time * 40f) - 0.5f) * 2f * shake;
                    GUI.matrix = Matrix4x4.Translate(new Vector3(sx, sy, 0f)) * keep;
                }
                // 自己的手：骨架畫在最底層（場景背景會蓋住相機畫面裡真正的手）
                if (phase == Phase.Fighting && Hand != null && Hand.HandVisible)
                    Hand.DrawSkeleton(Skills.Classes[myClass].color, Hand.CurrentNumber > 0 ? $"✋ {Hand.CurrentNumber}" : null);
                DrawHud();
                GUI.matrix = keep;
            }
        }

        void Panel(Rect r, float alpha = 0.6f)
        {
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        Vector2 setupScroll;
        float setupContentHeight;
        void DrawSetup()
        {
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f), fsL = Mathf.RoundToInt(fs * 1.25f);
            float pad = W * 0.035f, gap = pad * 0.5f;
            // 背景：整片暗色
            UiKit.Backdrop(new Rect(0,0,W,H),Skills.Classes[myClass].color);

            // 依可用高度決定單位行高（內容約 21 行），放得下就用基準行高
            float avail = botY - topY - pad * 2;
            FixLoadout();
            var setupSkills = SetupSkills;
            int skillRows = (setupSkills.Length + 1) / 2;
            float u = fs * 1.65f; // Keep readable cards; the preparation sheet scrolls above the fixed actions.
            float y = 0f;
            float viewH=Mathf.Max(fs*4,avail-u*2.8f-gap*2);
            float contentWidth=W-Mathf.Max(18f,fs*.45f);
            setupScroll=GUI.BeginScrollView(new Rect(0,topY+pad,W,viewH),setupScroll,new Rect(0,0,contentWidth,Mathf.Max(viewH,setupContentHeight)),false,true);
            W=contentWidth;
            var cls = Skills.Classes[myClass];

            UiKit.Title(new Rect(pad,y,W-pad*2,u*1.2f),NetMode ? "職業對戰：選擇職業與技能" : "選擇職業與技能",cls.color,fsL);
            y += u * 1.3f;

            // 職業（5 個）
            int nc = Skills.ClassOrder.Length;
            float cw = (W - pad * 2 - gap * (nc - 1)) / nc;
            for (int i = 0; i < nc; i++)
            {
                var id = Skills.ClassOrder[i]; var c = Skills.Classes[id];
                var classR=new Rect(pad+i*(cw+gap),y,cw,u*1.3f);
                if (UiKit.Button(classR, "", fs, id == myClass, c.color) && id != myClass) { myClass = id; LoadLoadout(); }
                float badgeSize=classR.height*.66f;
                var classBasic=Skills.BasicOf(id);
                UiKit.Badge(new Rect(classR.x+gap*.3f,classR.y+classR.height*.17f,badgeSize,badgeSize),classBasic!=null?SkillIcons.Get(classBasic.id):null,c.color);
                UiKit.Text(new Rect(classR.x+badgeSize+gap*.4f,classR.y,classR.width-badgeSize-gap*.6f,classR.height),c.name,fs,c.color,TextAnchor.MiddleCenter,true);
            }
            for(int i=0;i<nc;i++) UiKit.Tab(new Rect(pad+i*(cw+gap),y,cw,u*1.3f),Skills.ClassOrder[i]==myClass,Skills.Classes[Skills.ClassOrder[i]].color);
            y += u * 1.3f + gap;
            cls = Skills.Classes[myClass];
            var descR = new Rect(pad, y, W - pad * 2, u * 1.5f);
            UiKit.Card(descR, 0.5f, cls.color, 0.35f);
            var myAdv = Progress.Char(myClass).Adv;
            float bbw = descR.width * 0.3f;
            UiKit.Text(new Rect(descR.x + gap, descR.y + gap * 0.4f, descR.width - bbw - gap * 3, descR.height - gap * 0.8f), (myAdv != null ? $"✨ 進階：{myAdv.name}（{myAdv.core}）" : cls.desc) + $"\nHP {cls.maxHp + (myAdv?.hpAdd ?? 0)}・MP {cls.maxMp + (myAdv?.mpAdd ?? 0)}", fsS, new Color(0.9f, 0.92f, 1f));
            // 普攻：點了看詳細說明（和技能卡一樣）
            var basicSk = Skills.BasicOf(myClass);
            if (basicSk != null && UiKit.Button(new Rect(descR.xMax - bbw - gap, descR.y + gap * 0.4f, bbw, descR.height - gap * 0.8f), $"🗡 普攻\n{basicSk.name}", fsS, false, basicSk.color)) infoSkill = basicSk.id;
            y += u * 1.5f + gap;

            // 技能卡片（2 欄）：圖示＋名稱／手勢→放招／數值
            UiKit.Text(new Rect(pad, y, EndlessSetup?W*.51f-pad:W-pad*2, u), AdvSlot ? $"選 3 個技能＋1 個進階技能（{BaseCount}/3・{AdvCount}/1）" : $"選 3 個技能（{myLoadout.Count}/3）　點技能看詳細說明", fsS, new Color(1f, 0.9f, 0.6f));
            if (EndlessSetup)
            {
                // 無盡／故事模式：角色養成（能力值、技能樹、裝備）
                var pf = Progress.Char(myClass);
                float cbw = W * 0.4f;
                if (UiKit.Button(new Rect(W - pad - cbw, y - u * 0.15f, cbw, u * 1.1f), $"📜 角色 Lv{pf.level}" + (pf.statPoints + pf.UsableSkillPoints > 0 ? "（可加點）" : ""), fsS, pf.statPoints + pf.UsableSkillPoints > 0, new Color(0.8f, 0.55f, 1f)))
                    charOpen = true;
            }
            y += u;
            float sw = (W - pad * 2 - gap) / 2f, sh = u * 2.4f;
            for (int i = 0; i < setupSkills.Length; i++)
            {
                var sk = Skills.All[setupSkills[i]];
                bool on = myLoadout.Contains(sk.id);
                var r = new Rect(pad + (i % 2) * (sw + gap), y + (i / 2) * (sh + gap), sw, sh);
                UiKit.Card(r,on?.92f:.72f,sk.color,on?1f:.45f);
                float isz = sh * 0.82f;
                var ir = new Rect(r.x + sh * 0.09f, r.y + sh * 0.09f, isz, isz);
                GUI.color = on ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                GUI.DrawTexture(ir, SkillIcons.Get(sk.id));
                GUI.color = Color.white;
                float tx = ir.xMax + sh * 0.08f, tw = r.xMax - tx - sh * 0.06f;
                string eff = sk.type == SkillType.Self ? (sk.self == SelfKind.Heal ? $"回復{sk.heal}" : sk.self == SelfKind.Cleanse ? "解異常" : sk.self == SelfKind.Armor ? $"減傷{sk.buffReduce * 100f:0}%" : sk.self == SelfKind.Prayer ? $"冷卻-{sk.buffDur:0}s" : "防禦") : sk.damage > 0 ? $"傷害{sk.damage}{(sk.multi > 1 ? $"×{sk.multi}" : "")}" : "效果";
                UiKit.Text(new Rect(tx, r.y + sh * 0.05f, tw, sh * 0.32f), (on ? "✔ " : "") + (Advanced.IsAdvSkill(sk.id) ? "✨" : "") + sk.name, fs, on ? Color.white : new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(tx, r.y + sh * 0.37f, tw, sh * 0.27f), $"🎤唸「{sk.name}」→ {HandGesture.ReleaseLabel(sk)}", fsS, new Color(1f, 0.85f, 0.5f));
                UiKit.Text(new Rect(tx, r.y + sh * 0.64f, tw, sh * 0.32f), $"MP{sk.cost}・{(sk.charge > 0f ? $"蓄力{sk.charge:0.#}s" : "免蓄力")}・{eff}・{sk.RangeText}", fsS, new Color(0.8f, 0.85f, 0.95f));
                // 點技能卡：打開詳細說明（在說明裡選／移除）
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) infoSkill = sk.id;
            }
            y += ((setupSkills.Length + 1) / 2) * (sh + gap);

            // 敵人（單人：4×2＝隨機、5 職業、木頭人、Boss；雙人：顯示對手狀態）
            if (!NetMode)
            {
                UiKit.Text(new Rect(pad, y, W - pad * 2, u), "敵人", fsS, new Color(1f, 0.9f, 0.6f));
                y += u;
                float ew = (W - pad * 2 - gap * 3) / 4f;
                int nOpt = nc + 5;
                for (int i = 0; i < nOpt; i++)
                {
                    string id = i == 0 ? "random" : i == nc + 1 ? "dummy" : i == nc + 2 ? "boss" : i == nc + 3 ? "endless" : i == nc + 4 ? "story" : Skills.ClassOrder[i - 1];
                    string nm = id == "random" ? "隨機" : id == "dummy" ? "🪵 木頭人" : id == "boss" ? "🔥 Boss" : id == "endless" ? "♾ 無盡" : id == "story" ? "📖 故事" : Skills.Classes[id].name;
                    var c = id == "random" ? new Color(1f, 0.85f, 0.3f) : id == "dummy" ? new Color(0.75f, 0.55f, 0.3f) : id == "boss" ? new Color(1f, 0.35f, 0.1f) : id == "endless" ? new Color(0.75f, 0.45f, 1f) : id == "story" ? new Color(1f, 0.8f, 0.4f) : Skills.Classes[id].color;
                    if (UiKit.Button(new Rect(pad + (i % 4) * (ew + gap), y + (i / 4) * (u * 1.15f + gap), ew, u * 1.15f), nm, fsS, id == enemyClass, c)) enemyClass = id;
                }
                y += ((nOpt + 3) / 4) * (u * 1.15f + gap);
                DrawDifficultyRow(ref y, pad, gap, u, fsS, W);
                if (enemyClass == "endless")
                    UiKit.Text(new Rect(pad, y - gap, W - pad * 2, u * 0.9f), $"♾ 無盡模式最佳分數：{PlayerPrefs.GetInt("sd_endless_best", 0)}", fsS, new Color(0.85f, 0.75f, 1f));
            }
            else
            {
                // 雙人模式：對戰或合作打 Boss（切換時告訴對方，兩邊一起切）
                float mw = (W - pad * 2 - gap * 2) / 3f;
                string[] modeNames = { "⚔ 對戰", "🤝 合作 Boss", "♾ 無盡合作" };
                Color[] modeCols = { new Color(1f, 0.55f, 0.35f), new Color(1f, 0.35f, 0.1f), new Color(0.75f, 0.45f, 1f) };
                for (int mi = 0; mi < 3; mi++)
                    if (UiKit.Button(new Rect(pad + mi * (mw + gap), y, mw, u * 1.15f), modeNames[mi], fsS, NetModeCode == mi, modeCols[mi]) && NetModeCode != mi)
                    {
                        coop = mi >= 1; netEndless = mi == 2; localReady = false;
                        NetSend?.Invoke(new Msg { t = "mode", e = mi });
                    }
                y += u * 1.15f + gap;
                if (coop) DrawDifficultyRow(ref y, pad, gap, u, fsS, W);
                string who = coop ? "隊友" : "對手";
                if (PeerIds.Count <= 1)
                {
                    bool rr = PeerIds.Count == 1 && peerInfo.TryGetValue(PeerIds[0], out var p0) && p0.ready;
                    string rs = rr ? $"{who}：{(Skills.Classes.TryGetValue(peerInfo[PeerIds[0]].cls ?? "", out var rc) ? rc.name : "?")}（已準備）" : PeerIds.Count == 0 ? "等待其他人加入…" : $"{who}：還沒準備好";
                    UiKit.Text(new Rect(pad, y, W - pad * 2, u * 1.2f), rs, fs, rr ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f));
                    y += u * 1.3f;
                }
                else
                {
                    // 多人：房間裡每個人的準備狀態
                    UiKit.Text(new Rect(pad, y, W - pad * 2, u), $"👥 房間 {PeerIds.Count + 1} 人" + (coop ? "（敵人血量依人數增加）" : "（⚠ 對戰只能兩人，請選合作）"), fsS, coop ? new Color(0.85f, 0.9f, 1f) : new Color(1f, 0.6f, 0.4f));
                    y += u;
                    foreach (var pid in PeerIds)
                    {
                        bool rr = peerInfo.TryGetValue(pid, out var pi) && pi.ready;
                        string cn = pi != null && Skills.Classes.TryGetValue(pi.cls ?? "", out var rc2) ? rc2.name : "";
                        UiKit.Text(new Rect(pad, y, W - pad * 2, u * 0.95f), $"{PeerName(pid)}：" + (rr ? $"{cn}（已準備）" : "還沒準備好"), fsS, rr ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f));
                        y += u * 0.95f;
                    }
                    y += u * 0.2f;
                }
            }

            // 語音
            float vx = pad, vw = W - pad * 2;
            if (UseSpeech) UiKit.Text(new Rect(vx, y, vw, u * 1.15f), "🎤 每次放技能前都要先唸出技能名稱（不用錄音）", fsS, new Color(0.85f, 0.9f, 1f));
            else
            {
                float rw = (vw - gap * (myLoadout.Count - 1)) / Mathf.Max(1, myLoadout.Count);
                for (int i = 0; i < myLoadout.Count; i++)
                {
                    var sk = Skills.All[myLoadout[i]];
                    bool rec = recordingSkill == sk.id;
                    if (UiKit.Button(new Rect(vx + i * (rw + gap), y, rw, u * 1.15f), rec ? "● 錄音中" : $"錄{sk.name} {voice.Count(sk.id)}/2", fsS, rec || voice.Ready(sk.id), rec ? Color.red : sk.color, voiceOn))
                    {
                        if (rec) recordingSkill = null;
                        else { recordingSkill = sk.id; if (voice.Count(sk.id) >= 2) voice.ClearSkill(sk.id); Say($"🎤 唸出「{sk.name}」的咒語（自己決定怎麼唸）", 3f); }
                    }
                }
            }
            y += u * 1.15f + gap;
            if (recordingSkill != null && mic.Running)
            {
                float lv = Mathf.Clamp01(mic.Spotter.Level / Mathf.Max(0.001f, mic.Spotter.StartThreshold * 3f));
                UiKit.Bar(new Rect(pad, y, W - pad * 2, u * 0.4f), lv, mic.Spotter.InSpeech ? new Color(0.3f, 1f, 0.4f) : Color.gray, "", fsS);
                y += u * 0.5f;
            }
            if (Time.time < messageUntil) UiKit.Text(new Rect(pad, y, W - pad * 2, u), message, fsS, new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleCenter);

            setupContentHeight=Mathf.Max(viewH,y+u+gap);
            GUI.EndScrollView();
            W=Screen.width;
            // 下方固定：開始／準備、其他按鈕
            float by = botY - pad - u * 1.2f;
            float hw = (W - pad * 2 - gap) / 2f;
            if (!NetMode && UiKit.Button(new Rect(pad, by, hw, u * 1.2f), "重新放置場地", fsS)) RequestRedraw?.Invoke();
            if (UiKit.Button(new Rect(NetMode ? pad : pad + hw + gap, by, NetMode ? W - pad * 2 : hw, u * 1.2f), NetMode ? "返回" : "換模式", fsS))
            { if (NetMode) Hide(); else RequestChangeMode?.Invoke(); }
            by -= u * 1.6f + gap;
            bool voiceOk = UseSpeech || VoiceReady;   // 沒有語音辨識時，要先把 3 個技能的咒語錄好
            string startText = !voiceOk ? "🎤 先錄好 3 個技能的咒語" : NetMode ? (localReady ? "等待對方準備…" : netEndless && coop ? "♾ 準備好了（無盡合作）" : coop ? "🤝 準備好了（合作打 Boss）" : "⚔ 準備好了") : (enemyClass == "dummy" ? "🪵 開始練習" : enemyClass == "boss" ? "🔥 挑戰 Boss" : enemyClass == "endless" ? "♾ 開始無盡模式" : enemyClass == "story" ? "📖 打開故事地圖" : "⚔ 開始戰鬥");
            if (UiKit.Button(new Rect(pad, by, W - pad * 2, u * 1.6f), startText, fsL, true, cls.color, LoadoutReady && voiceOk))
            { if (NetMode) SendReady(); else StartBattle(); }
        }

        void DrawHud()
        {
            float W = Screen.width, H = Screen.height, pad = W * 0.03f, gap = pad * 0.5f;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f), fsL = Mathf.RoundToInt(fs * 1.25f);
            float lh = fs * 1.6f;
            float top = H - Screen.safeArea.yMax + pad * 0.5f, botY = H - Screen.safeArea.y;
            var me = Me; var en = En; float now = battle.now;
            float pillW = W - pad * 2;
            string chargeTip = null; Color chargeTipColor = Color.white;

            // 被打中：整個畫面閃紅
            if (hitFlash > 0.01f) { GUI.color = new Color(1f, 0f, 0.05f, hitFlash * 0.45f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }

            // 致盲：畫面蓋上煙霧
            if (me.blindUntil > now) { GUI.color = new Color(0.55f, 0.55f, 0.6f, 0.92f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; UiKit.Pill(H * 0.32f, $"煙霧中… {me.blindUntil - now:F1}s", fsL, Color.white, pillW); }
            // 敵人法術逼近：邊框閃紅
            if (incomingFlash > 0.01f)
            {
                GUI.color = new Color(1f, 0.15f, 0.2f, incomingFlash * 0.8f);
                float t = W * 0.025f;
                GUI.DrawTexture(new Rect(0, 0, W, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - t, W, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, t, H), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(W - t, 0, t, H), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // 上方：敵人資訊卡
            var enColor = en.cls != null ? en.cls.color : new Color(1f, 0.4f, 0.4f);
            var topR = new Rect(pad, top, W - pad * 2, lh * 2.9f);
            UiKit.Card(topR, 0.6f, enColor, 0.5f);
            float ix = topR.x + gap, iw = topR.width - gap * 2, y = topR.y + gap * 0.5f;
            if (endless && !en.Alive)
            {
                // 無盡模式、Boss 還沒來：顯示倒數
                // 無盡模式：這一波的進度（剩幾隻）
                int remain = WaveRemainingShown, total = Mathf.Max(1, battle.waveTotal);
                UiKit.Text(new Rect(ix, y, iw * 0.6f, lh), $"♾ 第 {battle.wave} 波", fs, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Bar(new Rect(ix + iw * 0.54f, y + lh * 0.15f, iw * 0.46f, lh * 0.7f), 1f - remain / (float)total, new Color(1f, 0.5f, 0.15f), $"剩 {remain} 隻", fsS);
            }
            else
            {
                UiKit.Text(new Rect(ix, y, iw * 0.52f, lh), (endless ? $"第 {battle.wave} 波 " : "") + $"{en.name}　{battle.Distance:F1}m", fs, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Bar(new Rect(ix + iw * 0.54f, y + lh * 0.15f, iw * 0.46f, lh * 0.7f), en.hp / en.maxHp, new Color(1f, 0.25f, 0.35f), $"{Mathf.CeilToInt(en.hp)}", fsS);
            }
            y += lh;
            string enState = dummyMode ? $"命中 {dummyHits} 次・累計傷害 {Mathf.RoundToInt(dummyDamage)}"
                : en.charging != null ? $"⚠ 詠唱 {en.charging.name}（{Mathf.FloorToInt(battle.ChargeProgress(en) * 100)}%）" : (ai?.Status ?? bossAi?.Status ?? "");
            if (bossMode && (bossAi != null ? bossAi.Enraged : bossEnragedShown)) enState = "🔥 狂暴　" + enState;
            if (en.snaredUntil > now) enState = "被定身";
            if (endless) enState = $"擊倒小怪 {battle.kills}・Boss {battle.bossKills}　" + (en.Alive ? enState : $"Boss 在第 {battle.NextBossWave} 波（還有 {battle.NextBossWave - battle.wave} 波）");
            if (coop && en.Alive && en.target != null && battle.allies.Count > 0) enState = (en.target == me ? "🎯 盯上你！　" : $"🎯 盯著 {en.target.name}　") + enState;   // 仇恨：Boss 現在的目標
            UiKit.Text(new Rect(ix, y, iw, lh * 0.9f), enState, fsS, en.charging != null ? new Color(1f, 0.75f, 0.4f) : new Color(0.9f, 0.92f, 1f));
            y += lh * 0.9f;
            UiKit.Text(new Rect(ix, y, iw, lh * 0.8f), StatusText(en, now), fsS, new Color(0.75f, 0.85f, 1f));
            // 合作：隊友的血量（在敵人卡片下方）
            if (coop)
            {
                // 每位隊友一行（最多 3 位）
                float ay = topR.yMax + gap * 0.5f, ah = battle.allies.Count > 1 ? lh * 0.7f : lh * 0.9f;
                foreach (var al in battle.allies)
                {
                    var ar = new Rect(topR.x, ay, topR.width, ah);
                    UiKit.Card(ar, 0.5f, al.cls.color, 0.5f);
                    UiKit.Text(new Rect(ar.x + gap, ar.y, iw * 0.42f, ar.height), al.Alive ? $"{(en.target == al ? "🎯" : "🤝")} {al.name}" : $"💀 {al.name}（倒下）", fsS, Color.white, TextAnchor.MiddleLeft, true);
                    UiKit.Bar(new Rect(ar.x + gap + iw * 0.44f, ar.y + ar.height * 0.2f, iw * 0.56f, ar.height * 0.6f), al.hp / al.maxHp, new Color(0.3f, 0.85f, 0.45f), $"{Mathf.CeilToInt(al.hp)}", fsS);
                    ay += ah + gap * 0.3f;
                }
            }

            // 敵人頭上的血條
            var hs = cam.WorldToScreenPoint(WorldFrame.FromWorld(en.head + Vector3.up * 0.3f));
            if (hs.z > 0 && en.Alive)
            {
                var r = new Rect(hs.x - W * 0.12f, H - hs.y, W * 0.24f, lh * 0.35f);
                UiKit.Bar(r, en.hp / en.maxHp, new Color(1f, 0.25f, 0.35f), "", fsS);
                // 敵我距離：蓄力中的技能射程到得了就綠色，太遠就紅色（太遠也能放，只是飛不到）
                float dist = battle.Distance;
                string rangeNote = ""; Color dc = Color.white;
                if (me.charging != null && me.charging.type == SkillType.Projectile)
                {
                    bool far = battle.RangeState(me, me.charging) > 0;
                    dc = far ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.5f);
                    rangeNote = far ? "　超出射程" : "　射程內";
                }
                UiKit.Text(new Rect(r.x - W * 0.12f, r.yMax + 2, r.width + W * 0.24f, lh), $"{dist:F1} m{rangeNote}", fs, dc, TextAnchor.UpperCenter, true);
                if (en.charging != null) UiKit.Text(new Rect(r.x - W * 0.12f, r.y - lh, r.width + W * 0.24f, lh * 0.95f), $"⚠ {en.charging.name}", fs, new Color(1f, 0.75f, 0.4f), TextAnchor.LowerCenter, true);
            }

            // 最近的敵人（小怪或 Boss）：在畫面中 → 頭上標「▼ 最近 3.2m」；不在畫面中 → 畫面邊緣的箭頭指出方位與距離
            if (bossMode && phase == Phase.Fighting) DrawNearestEnemy(W, H, lh, fsS);

            // 小怪頭上的小血條
            if (battle.minions.Count > 0)
                foreach (var mn in battle.minions)
                {
                    if (!mn.Alive) continue;
                    var ms = cam.WorldToScreenPoint(WorldFrame.FromWorld(mn.Feet + Vector3.up * (MinionRig.Height + 0.2f)));
                    if (ms.z <= 0) continue;
                    UiKit.Bar(new Rect(ms.x - W * 0.06f, H - ms.y, W * 0.12f, lh * 0.22f), mn.hp / mn.maxHp, mn.windupUntil > 0f ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.55f, 0.2f), "", fsS);
                }

            // 飄字
            foreach (var f in floaters)
            {
                float age = Time.time - f.born;
                if (age > 1.2f) continue;
                Vector2 p;
                if (f.screen) p = new Vector2(W / 2, H * 0.45f - age * 60f);
                else
                {
                    var sp = cam.WorldToScreenPoint(WorldFrame.FromWorld(f.posMap));
                    if (sp.z <= 0) continue;
                    p = new Vector2(sp.x, H - sp.y - age * 60f);
                }
                UiKit.Text(new Rect(p.x - W * 0.3f, p.y - lh, W * 0.6f, lh * 1.5f), f.text, fsL, new Color(f.color.r, f.color.g, f.color.b, 1f - age / 1.2f), TextAnchor.MiddleCenter, true);
            }
            floaters.RemoveAll(f => Time.time - f.born > 1.2f);

            // 蓄力狀態
            if (me.charging != null)
            {
                float prog = battle.ChargeProgress(me);
                bool bow = HandGesture.ReleaseOf(me.charging) == HandGesture.Style.Bow && Hand != null;   // 獵人：拉滿弓就是蓄力完成
                string st = bow ? (Hand.BowDrawn ? "🏹 拉滿！手回畫面張開放箭" : Hand.BowStage == 1 ? "✊ 握著拳拉出畫面外（拉弓）" : "畫面中握拳（捏弦）") :
                    me.charging.releaseNear && prog < 1f && Battle.StealthAmbush(me) ? $"{(me.seenByFoe ? "👁 被看見（暫停）" : "🫥 躲開視線中")}　累計 {Mathf.Min(me.hiddenTime, Battle.StealthTime):0.0}／{Battle.StealthTime:0} 秒" :
                    me.charging.releaseNear && prog < 1f ? $"蓄力 {Mathf.FloorToInt(prog * 100)}%　" +
                        (!me.palmVisible ? "✋ 手掌伸到鏡頭前才會蓄力" : "蓄滿自動伏擊（敵人在身邊就直接出手）") :
                    prog < 1f && me.charging.stillRadius > 0f ? $"蓄力 {Mathf.FloorToInt(prog * 100)}%　站著別動！（離原地 {Battle.FlatDistance(me.Feet, me.chargeFrom):0.0}／{me.charging.stillRadius:0.0}m）" :
                    HandGesture.ReleaseOf(me.charging) == HandGesture.Style.Chant ?
                        (prog < 1f ? $"蓄力 {Mathf.FloorToInt(prog * 100)}%　" : "") +
                        (ChantDone ? "✨ 咒語完成！手在畫面比 5（五指張開）放招" + (Hand != null && Hand.HandVisible ? $"（目前：{(Hand.CurrentNumber > 0 ? Hand.CurrentNumber.ToString() : "—")}）" : "")
                                   : $"唸咒語「{me.charging.chant}」") :
                    prog < 1f ? $"蓄力 {Mathf.FloorToInt(prog * 100)}%（蓄滿後{HandGesture.ReleaseLabel(me.charging)}）" :
                    me.charging.releaseNear ? "進入伏擊…" :
                    HandGesture.ReleaseHint(me.charging) + (HandGesture.ReleaseOf(me.charging) == HandGesture.Style.Number && Hand != null && Hand.HandVisible ? $"（目前：{(Hand.CurrentNumber > 0 ? Hand.CurrentNumber.ToString() : "—")}）" : "");
                if (bow && me.charging.stillRadius > 0f)
                {
                    float mv = Battle.FlatDistance(me.Feet, me.chargeFrom);
                    if (mv > me.charging.stillRadius * 0.4f) st += $"　⚠ 站著別動！（{mv:0.0}／{me.charging.stillRadius:0.0}m）";
                }
                float ocf = battle.OverchargeFrac(me);
                if (ocf > 0f) st += $"　⚡蓄力 {100f + 100f * ocf:0}%（傷害 +{Battle.OverchargeDamage * 100f * ocf:0}%）";
                else if (me.overcharge && prog >= 1f) st += "　（繼續拉著可以蓄到 200%）";
                chargeTip = $"{me.charging.name}　{st}"; chargeTipColor = prog < 1f ? Color.white : me.charging.color;   // 最後才畫（不被準星、鎖定框蓋住）
            }
            // 伏擊狀態：我在伏擊 → 提示靠近；敵人在伏擊 → 警告保持距離
            if (me.armed != null)
            {
                float leftT = Mathf.Max(0f, me.armedUntil - now), need = Battle.AmbushReach;
                var nf = battle.NearestFoe(me, out var nfDist);
                UiKit.Pill(H * 0.31f, $"🗡 {me.armed.name}伏擊中：靠近敵人 {need:0.#}m 內自動出手（" + (nf != null ? $"最近 {nfDist:F1}m・" : "") + $"剩 {leftT:F1} 秒・被打到會取消）", fs, me.armed.color, pillW);
            }
            bool pvp = NetMode && !coop;   // 真人對戰：不提示怎麼躲
            if (en.armed != null && en.Alive && !pvp)
                UiKit.Pill(H * 0.37f, $"⚠ 敵人伏擊中！保持距離（> {en.armed.rangeMax:0.#}m）", fs, new Color(1f, 0.4f, 0.45f), pillW);
            // 閃避提示：敵人攻擊蓄力過半時告訴你怎麼躲
            if (!pvp && en.charging != null && en.Alive && en.charging.type == SkillType.Projectile && battle.ChargeProgress(en) >= 0.5f)
            {
                var ecs = en.charging;
                string tip = ecs.id == "b_leap" ? $"⚠ {ecs.name}：Boss 要跳過來了，離開落點紅圈！" : ecs.atTarget ? $"⚠ {ecs.name}：離開腳下的紅圈！" : ecs.lob ? $"⚠ {ecs.name}：看落點紅圈閃開！" : ecs.areaRadius > 0f ? $"⚠ {ecs.name}：走出 Boss 周圍的紅圈！" : ecs.fromAbove ? $"⚠ {ecs.name}：落下時離開紅圈！" : ecs.duckable ? $"⚠ {ecs.name}：↓ 蹲下躲！" : $"⚠ {ecs.name}：快速側跨閃開！";
                if (coop && en.target != null && en.target != me) tip = $"⚠ Boss 準備對隊友使出 {ecs.name}";
                UiKit.Pill(H * 0.43f, tip, fs, ecs.duckable ? new Color(1f, 0.7f, 0.3f) : new Color(1f, 0.45f, 0.45f), pillW);
            }
            // 增益狀態：精神集中、刺殺準備
            {
                string buffs = "";
                if (me.focusUntil > now) buffs += $"🎯 精神集中 冷卻減半 {me.focusUntil - now:0}s　";
                if (me.prepUntil > now) buffs += $"🗡 刺殺準備 傷害 +{me.prepBonus * 100:0}% {me.prepUntil - now:0.0}s（被打到就取消）";
                if (buffs.Length > 0) UiKit.Pill(H * 0.145f, buffs.Trim(), fsS, new Color(1f, 0.85f, 0.4f), pillW);
            }
            if (me.crouching) UiKit.Pill(H * 0.19f, "↓ 蹲下中", fsS, new Color(0.6f, 0.9f, 1f), pillW);
            DrawArenaHints(H, fs, fsS, pillW);
            foreach (var mn in battle.minions)
                if (mn.Alive && mn.minionKind == 5 && mn.windupUntil > 0f && (mn.target == null || mn.target == me))
                { UiKit.Pill(H * 0.49f, "⚠ 狙擊手瞄準你了！準備側跨", fs, new Color(1f, 0.35f, 0.75f), pillW); break; }
            if (en.charging != null && en.charging.self == SelfKind.Summon && en.Alive) UiKit.Pill(H * 0.43f, "⚠ Boss 準備召喚小怪！", fs, new Color(1f, 0.55f, 0.2f), pillW);
            int nMin = 0; foreach (var mn in battle.minions) if (mn.Alive) nMin++;
            if (nMin > 0) UiKit.Pill(top + lh * 3.6f + (coop ? lh : 0f) + (endless ? lh * 2.0f : 0f), $"👹 小怪 ×{nMin}", fsS, new Color(1f, 0.6f, 0.3f), W * 0.4f);

            // 準星（畫面中央）：所有技能都朝這裡放；蓄力完成（或符文畫好）時用技能顏色脈動
            {
                bool ready = me.charging != null && battle.ChargeProgress(me) >= 1f;
                var cc = me.charging != null ? me.charging.color : Color.white;
                float pulse = ready ? 0.6f + 0.4f * Mathf.Sin(Time.time * 10f) : 0.75f;
                float arm = W * (ready ? 0.05f : 0.04f), th = Mathf.Max(3f, W * 0.006f), cg = W * 0.012f;
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(W / 2 - arm - 1, H / 2 - th / 2 - 1, arm - cg + 2, th + 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 + cg - 1, H / 2 - th / 2 - 1, arm - cg + 2, th + 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2 - 1, H / 2 - arm - 1, th + 2, arm - cg + 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2 - 1, H / 2 + cg - 1, th + 2, arm - cg + 2), Texture2D.whiteTexture);
                GUI.color = new Color(cc.r, cc.g, cc.b, pulse);
                GUI.DrawTexture(new Rect(W / 2 - arm, H / 2 - th / 2, arm - cg, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 + cg, H / 2 - th / 2, arm - cg, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2, H / 2 - arm, th, arm - cg), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2, H / 2 + cg, th, arm - cg), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2, H / 2 - th / 2, th, th), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            DrawRuneGuide(W, H);
            Hand?.DrawGUI(small, me.charging != null ? me.charging.color : Color.white);
            if (Time.time < heardUntil) UiKit.Pill(H * 0.24f, heard, fsL, new Color(1f, 0.95f, 0.6f), pillW);
            else if (voiceOn && VoiceReady && mic.Running && mic.Spotter.InSpeech) UiKit.Pill(H * 0.25f, "🎤 …", fs, Color.white, pillW);

            // 鎖定框／敵人方向提示
            if (phase == Phase.Fighting && en.Alive && !frozen)
            {
                if (enemyOnScreen)
                {
                    float t = Mathf.Max(3f, W * 0.006f);
                    // 方框顏色：一般綠色；對手開了格擋／護盾／反擊／守護時變藍色並標示
                    bool enGuard = en.blockUntil > now || en.shieldUntil > now || en.counterUntil > now || en.armorUntil > now;
                    var frameC = enGuard ? new Color(0.4f, 0.7f, 1f, 0.95f) : new Color(0.3f, 1f, 0.4f, 0.9f);
                    GUI.color = frameC;
                    var r = enemyRect;
                    // 只畫四個角，比較不擋畫面
                    float cl = Mathf.Min(r.width, r.height) * 0.28f;
                    GUI.DrawTexture(new Rect(r.xMin, r.yMin, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, cl), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - cl, r.yMin, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, cl), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMin, r.yMax - cl, t, cl), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - cl, r.yMax - t, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - cl, t, cl), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    string guardTxt = en.counterUntil > now ? "　反擊中" : en.shieldUntil > now ? "　護盾" : en.blockUntil > now ? "　格擋中" : en.armorUntil > now ? "　守護" : "";
                    UiKit.Text(new Rect(r.x, r.yMax + 2, Mathf.Max(r.width, W * 0.45f), lh * 0.8f), "🎯 鎖定" + guardTxt, fsS, frameC, TextAnchor.UpperLeft, true);
                }
                else if (!bossMode)   // Boss／無盡模式改用「最近的敵人」方位標示
                    UiKit.Pill(H * 0.58f, (enemyScreenSide < 0 ? "◀ 敵人在左邊" : "敵人在右邊 ▶") + "（沒鎖定也能放，朝準星飛）", fs, new Color(1f, 0.45f, 0.45f), pillW);
            }

            // 無盡／故事：遠距離小怪正在瞄準我 → 頭上閃 ⚠（畫面外就在畫面邊緣畫箭頭）＋上方提示
            if (endless && battle != null && cam != null)
            {
                Fighter aimer = null; float aimerLeft = 99f; int aimers = 0;
                foreach (var m in battle.minions)
                {
                    if (!m.Alive || m.windupUntil <= 0f || !Battle.IsRangedMinion(m.minionKind)) continue;
                    if (m.target != null && m.target != me) continue;   // 合作：瞄準隊友的不提示
                    aimers++;
                    float left = Mathf.Max(0f, m.windupUntil - now);
                    if (left < aimerLeft) { aimerLeft = left; aimer = m; }
                    var wp = WorldFrame.FromWorld(m.head + Vector3.up * 0.3f);
                    var v = cam.WorldToViewportPoint(wp);
                    float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
                    var warnC = new Color(1f, 0.25f, 0.2f, blink);
                    float sz = fs * 2.2f;
                    if (v.z > 0.1f && v.x > 0.03f && v.x < 0.97f && v.y > 0.03f && v.y < 0.97f)
                        UiKit.Text(new Rect(v.x * W - sz, (1f - v.y) * H - sz * 1.1f, sz * 2f, sz), "⚠", Mathf.RoundToInt(fs * 1.8f), warnC, TextAnchor.MiddleCenter, true);
                    else
                    {
                        // 畫面外：沿著敵人方向貼在畫面邊緣
                        var rel = cam.transform.InverseTransformPoint(wp);
                        var d2 = new Vector2(rel.x, rel.y); if (d2.sqrMagnitude < 1e-6f) d2 = Vector2.right;
                        d2.Normalize();
                        float k = Mathf.Min((W * 0.5f - sz) / Mathf.Max(1e-3f, Mathf.Abs(d2.x)), (H * 0.5f - sz * 2f) / Mathf.Max(1e-3f, Mathf.Abs(d2.y)));
                        var c2 = new Vector2(W * 0.5f + d2.x * k, H * 0.5f - d2.y * k);
                        string arrow = Mathf.Abs(d2.x) > Mathf.Abs(d2.y) ? (d2.x < 0 ? "◀⚠" : "⚠▶") : (d2.y > 0 ? "▲⚠" : "▼⚠");
                        UiKit.Text(new Rect(c2.x - sz, c2.y - sz * 0.5f, sz * 2f, sz), arrow, Mathf.RoundToInt(fs * 1.5f), warnC, TextAnchor.MiddleCenter, true);
                    }
                }
                if (aimer != null)
                {
                    float side = Vector3.Dot(WorldFrame.FromWorld(aimer.Feet) - cam.transform.position, cam.transform.right);
                    string nm = Battle.MinionNames[Mathf.Clamp(aimer.minionKind, 0, Battle.MinionNames.Length - 1)];
                    UiKit.Pill(H * 0.25f, $"⚠ {nm}正在瞄準你（{(side < 0 ? "左邊" : "右邊")}・{Battle.FlatDistance(aimer.Feet, me.Feet):0}m）" + (aimers > 1 ? $"　共 {aimers} 隻" : "") + "　準備閃避！", fs, new Color(1f, 0.3f, 0.25f), pillW, 0.7f);
                }
            }

            // 下方：我的狀態與技能（由下往上排）
            int ncol = Mathf.Max(3, me.loadout.Count);
            float sw = (W - pad * 2 - gap * (ncol - 1)) / ncol;
            float isz = Mathf.Min(sw * 0.5f, lh * 2.2f);
            float rowH = lh * 0.72f;
            float tileH = gap * 0.6f + isz + rowH * 3 + gap * 0.4f;
            float sy = botY - pad * 0.5f - tileH;
            float statY = sy - gap * 0.5f - lh * 0.75f;
            float mpY = statY - lh * 0.72f, hpY = mpY - lh * 0.8f;
            float hintY = hpY - gap * 0.5f - lh * 1.4f;
            bool hasHeard = UseSpeech && voiceOn && !string.IsNullOrEmpty(speech.LastHeard);
            float panelY = hintY - (hasHeard ? lh * 0.8f : 0f) - gap;
            UiKit.Card(new Rect(gap * 0.5f, panelY, W - gap, botY - panelY + H), 0.55f);

            bool voiceActive = voiceOn && (UseSpeech || VoiceReady);
            string voiceHint = !voiceOn || voiceActive ? "" : "（語音：咒語還沒錄完，到選技能畫面錄）";
            float micW = W * 0.24f;
            UiKit.Text(new Rect(pad, hintY, W - pad * 2 - micW - gap, lh * 1.4f),
                (UseSpeech ? "🎤 先唸技能名稱＝詠唱" : "🎤 先唸咒語＝詠唱") + "，蓄滿後做該技能的放招動作" + voiceHint, fsS, new Color(1f, 0.92f, 0.65f));
            if (hasHeard) UiKit.Text(new Rect(pad, hintY - lh * 0.8f, W - pad * 2, lh * 0.8f), $"聽到：{speech.LastHeard}", fsS, new Color(0.8f, 0.9f, 1f));
            float mx = W - pad - micW;
            if (UseSpeech && voiceOn)
                UiKit.Text(new Rect(mx, hintY, micW, lh * 0.75f), speech.Running ? "🎤 聆聽中" : "🎤 " + speech.Status, fsS, speech.Running ? new Color(0.5f, 1f, 0.6f) : new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleRight);
            // 麥克風音量：說話中變綠色
            if (mic.Running)
            {
                float lv = Mathf.Clamp01(mic.Spotter.Level / Mathf.Max(0.001f, mic.Spotter.StartThreshold * 3f));
                UiKit.Bar(new Rect(mx, hintY + lh * 0.85f, micW, lh * 0.3f), lv, mic.Spotter.InSpeech ? new Color(0.3f, 1f, 0.4f) : Color.gray, "", fsS);
            }
            UiKit.Bar(new Rect(pad, hpY, W - pad * 2, lh * 0.68f), me.hp / me.maxHp, new Color(1f, 0.3f, 0.35f), $"HP {Mathf.CeilToInt(me.hp)} / {Mathf.CeilToInt(me.maxHp)}", fsS);
            UiKit.Bar(new Rect(pad, mpY, W - pad * 2, lh * 0.6f), me.mp / me.maxMp, new Color(0.3f, 0.6f, 1f), $"MP {Mathf.FloorToInt(me.mp)} / {Mathf.FloorToInt(me.maxMp)}", fsS);
            // 普攻小卡（右邊）：手勢＋冷卻；點了看說明。狀態文字在左邊
            var nb = me.basic ?? Skills.BasicOf(myClass);
            float nbw = nb != null ? (W - pad * 2) * 0.36f : 0f;
            UiKit.Text(new Rect(pad, statY, W - pad * 2 - nbw - (nb != null ? gap : 0f), lh * 0.75f), StatusText(me, now), fsS, new Color(0.75f, 0.9f, 1f));
            if (nb != null)
            {
                var nr = new Rect(W - pad - nbw, statY, nbw, lh * 0.75f);
                float ncd = me.cooldownUntil.TryGetValue(nb.id, out var nu) ? Mathf.Max(0, nu - now) : 0;
                UiKit.Card(nr, 0.55f, nb.color, 0.6f);
                if (ncd > 0) { GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(new Rect(nr.x, nr.y, nr.width * Mathf.Clamp01(ncd / Mathf.Max(0.01f, nb.cooldown)), nr.height), Texture2D.whiteTexture); GUI.color = Color.white; }
                UiKit.Text(nr, $"🗡 普攻：{SkillInfo.BasicGesture(nb)}", Mathf.RoundToInt(fsS * 0.85f), Color.white, TextAnchor.MiddleCenter);
                if (GUI.Button(nr, GUIContent.none, GUIStyle.none)) infoSkill = nb.id;
            }

            for (int i = 0; i < me.loadout.Count; i++)
            {
                var s = me.loadout[i];
                var r = new Rect(pad + i * (sw + gap), sy, sw, tileH);
                float cd = me.cooldownUntil.TryGetValue(s.id, out var u) ? Mathf.Max(0, u - now) : 0;
                bool charging = me.charging == s;
                bool usable = me.mp >= s.cost && cd <= 0;
                // 技能卡（點了看詳細說明）：上面圖示、下面名稱／手勢→放招／MP 或冷卻；手正比著這個手勢時亮起來
                UiKit.Card(r, charging ? 0.8f : 0.5f, charging ? s.color : (Color?)null, charging ? 1f : 0.6f);
                var ir = new Rect(r.center.x - isz / 2, r.y + gap * 0.6f, isz, isz);
                // 技能圖示（發光徽章）：詠唱中外圈脈動發光；冷卻中由上往下蓋暗；MP 不足變灰
                if (charging)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                    GUI.color = WithAlpha(s.color, 0.35f + 0.4f * pulse);
                    float gx = isz * 0.1f;
                    GUI.DrawTexture(new Rect(ir.x - gx, ir.y - gx, ir.width + gx * 2, ir.height + gx * 2), SkillIcons.Get(s.id));
                }
                GUI.color = usable ? Color.white : new Color(0.45f, 0.45f, 0.45f);
                GUI.DrawTexture(ir, SkillIcons.Get(s.id));
                if (cd > 0)
                {
                    float frac = Mathf.Clamp01(cd / Mathf.Max(0.01f, s.cooldown));
                    GUI.color = new Color(0f, 0f, 0f, 0.55f);
                    GUI.DrawTexture(new Rect(ir.x, ir.y, ir.width, ir.height * frac), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;
                float tx = r.x + gap * 0.4f, tw = r.width - gap * 0.8f, ty = ir.yMax + gap * 0.2f;
                var tc = usable ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                string vtag = UseSpeech || voice.Ready(s.id) ? "" : " 🎤未錄";
                UiKit.Text(new Rect(tx, ty, tw, rowH), s.name + vtag, fs, tc, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(tx, ty + rowH, tw, rowH), $"🎤→{HandGesture.ReleaseLabel(s)}", fsS, usable ? new Color(1f, 0.85f, 0.5f) : tc, TextAnchor.MiddleCenter);
                UiKit.Text(new Rect(tx, ty + rowH * 2, tw, rowH), cd > 0 ? $"冷卻 {cd:F1}s" : $"MP {s.cost}", fsS, cd > 0 ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.6f, 0.8f, 1f), TextAnchor.MiddleCenter);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) infoSkill = s.id;   // 點技能卡：看詳細說明（戰鬥不會暫停）
            }

            // 無盡模式：等級與經驗條；可以升級時顯示按鈕（單人會自動打開選單）
            if (endless && phase == Phase.Fighting)
            {
                var pf = prof ?? Progress.Char(myClass);
                int lv = pf.level;
                int cur = CharacterProfile.XpForLevel(lv), next = CharacterProfile.XpForLevel(lv + 1);
                float xy = top + lh * 2.9f + gap + (coop && battle.allies.Count > 0 ? battle.allies.Count * ((battle.allies.Count > 1 ? lh * 0.7f : lh * 0.9f) + gap * 0.3f) + gap * 0.2f : 0f);
                UiKit.Bar(new Rect(pad, xy, W - pad * 2, lh * 0.45f), (pf.xp - cur) / (float)Mathf.Max(1, next - cur), new Color(0.75f, 0.45f, 1f), pf.LevelLocked ? $"Lv {lv}　要轉職才能繼續升級" : $"Lv {lv}　{pf.xp - cur}/{next - cur}", fsS);
                float bw2 = W * 0.5f;
                if (UiKit.Button(new Rect((W - bw2) / 2f, xy + lh * 0.55f, bw2, lh * 1.1f), PendingUpgrades > 0 ? $"📜 角色（可加點 ×{PendingUpgrades}）" : "📜 角色／裝備", fsS, PendingUpgrades > 0, new Color(0.75f, 0.45f, 1f)))
                    charOpen = true;
                // 消耗道具：藥水（冷卻 8 秒）、回城捲軸（故事關卡）；暫存戰利品
                float iy = xy + lh * 1.75f, ibw = (W - pad * 2 - gap * 3) / 4f;
                int ci = 0;
                foreach (var c in Economy.Consumables)
                {
                    if (c.scroll && storyStage == null) continue;
                    int n = Economy.Count(c.id);
                    bool cdOk = c.scroll || Time.time >= potionReadyAt;
                    if (UiKit.Button(new Rect(pad + ci * (ibw + gap), iy, ibw, lh * 1f), $"{c.icon}×{n}" + (!cdOk && n > 0 ? $" {potionReadyAt - Time.time:0}s" : ""), fsS, false, new Color(0.5f, 0.85f, 1f), n > 0 && cdOk))
                        UseConsumable(c);
                    ci++;
                }
                if (pendingItems.Count > 0 || pendingGold > 0)
                    UiKit.Text(new Rect(pad, iy + lh * 1.05f, W - pad * 2, lh * 0.7f), $"🎁 待領取：裝備 {pendingItems.Count} 件・💰 {pendingGold}（{(storyStage != null ? "通關" : "清完這一波")}後才拿得到）", fsS, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleCenter);
            }

            // 蓄力／怎麼施法的提示：放在下方面板正上方（不在畫面中央，敵人靠近也不會被敵人和鎖定框擋住），底色較不透明、最後才畫
            {
                float chargeY = panelY - gap - fs * 1.9f;
                if (chargeTip != null) UiKit.Pill(chargeY, chargeTip, fs, chargeTipColor, pillW, 0.88f);
                if (Time.time < messageUntil) UiKit.Pill(chargeTip != null ? chargeY - fs * 3.6f : H * 0.64f, message, fs, Color.white, pillW);
            }

            if (frozen)
            {
                var fr = new Rect(pad, H * 0.3f, W - pad * 2, H * 0.2f);
                UiKit.Card(fr, 0.85f, new Color(1f, 0.75f, 0.3f), 0.8f);
                UiKit.Text(new Rect(fr.x + gap, fr.y + gap, fr.width - gap * 2, fr.height * 0.45f), NetMode ? "⚠ AR 追蹤中斷，暫時不能施法" : "⏸ AR 追蹤中斷，戰鬥暫停", fsL, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(fr.x + gap, fr.y + fr.height * 0.5f, fr.width - gap * 2, fr.height * 0.45f), Tracking.Reason + (NetMode ? "\n對手仍以你最後的位置判定" : "\n恢復追蹤後自動繼續"), fs, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleCenter);
            }

            if (phase == Phase.Over && storyStage != null)
            {
                var st = storyStage; bool won = battle.stageCleared;
                var orr = new Rect(pad, top+(botY-top)*.24f, W-pad*2, (botY-top)*.36f);
                UiKit.Result(orr,won);
                UiKit.Text(new Rect(orr.x, orr.y + gap, orr.width, orr.height * 0.4f), won ? $"🏆 {st.title}" : "💀 失敗…", fs * 2, Color.white, TextAnchor.MiddleCenter, true);
                var next = won && !IsTrial ? NextStage(st) : null;
                float bw = (orr.width - gap * 3) / 2f, bh = lh * 1.8f, oby = orr.yMax - gap - bh;
                if (IsTrial && won)
                {
                    if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), "🏛 回職業工會", fs, true, new Color(0.85f, 0.6f, 1f)))
                    { ShowSetup(); storyMapOpen = true; world.OpenTown(Battle.ThemeOf(Advanced.TrialLastWave)); world.OpenGuild(); }
                }
                // 這一章全部打通：下一關在下一個城鎮之後 → 先抵達下一個城鎮
                else if (won && next != null && next.theme != st.theme && Story.IsUnlocked(next))
                {
                    int nt = Mathf.Clamp(next.theme, 0, WorldUI.Towns.Length - 1);
                    if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), $"🏘 前往{WorldUI.Towns[nt].name}", fs, true, WorldUI.Towns[nt].color))
                    { ShowSetup(); storyMapOpen = true; world.OpenTown(nt); }
                }
                else if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), won ? (next != null && Story.IsUnlocked(next) ? "▶ 下一關" : "再打一次") : "再挑戰", fs, true, new Color(0.4f, 0.8f, 1f)))
                { if (won && next != null && Story.IsUnlocked(next)) { var n2 = next; ShowSetup(); StartStory(n2); } else StartStageBattle(); }
                if (!IsTrial && UiKit.Button(new Rect(orr.x + gap * 2 + bw, oby, bw, bh), $"🏘 回{WorldUI.Towns[Mathf.Clamp(st.theme, 0, WorldUI.Towns.Length - 1)].name}", fs))
                { ShowSetup(); storyMapOpen = true; world.OpenTown(Mathf.Clamp(st.theme, 0, WorldUI.Towns.Length - 1)); }
                else if (IsTrial && UiKit.Button(new Rect(orr.x + gap * 2 + bw, oby, bw, bh), "🗺 大地圖", fs)) { ShowSetup(); storyMapOpen = true; world.screen = WorldUI.Screen.Map; }
            }
            else if (phase == Phase.Over)
            {
                var orr = new Rect(pad, top+(botY-top)*.24f, W-pad*2, (botY-top)*.36f);
                UiKit.Result(orr,!en.Alive&&!endless);
                UiKit.Text(new Rect(orr.x, orr.y + gap, orr.width, orr.height * 0.4f), endless ? $"♾ 撐到第 {battle.wave} 波" : !en.Alive ? (bossMode ? "🏆 討伐成功！" : "🏆 勝利！") : "💀 敗北…", fs * 2, Color.white, TextAnchor.MiddleCenter, true);
                float bw = (orr.width - gap * 3) / 2f, bh = lh * 1.8f, oby = orr.yMax - gap - bh;
                if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), NetMode ? (localReady ? "等待對手…" : "再來一局") : "再來一局", fs, true, new Color(0.4f, 0.8f, 1f), !(NetMode && localReady)))
                { if (NetMode) SendReady(); else StartBattle(); }
                if (UiKit.Button(new Rect(orr.x + gap * 2 + bw, oby, bw, bh), "換職業", fs)) ShowSetup();
            }
        }

        /// <summary>標示離我最近的敵人在哪個方位（Boss 模式、無盡模式）</summary>
        void DrawNearestEnemy(float W, float H, float lh, int fsS)
        {
            Fighter near = null; float best = float.MaxValue;
            foreach (var mn in battle.minions) { if (!mn.Alive) continue; float d = Battle.FlatDistance(mn.Feet, Me.Feet); if (d < best) { best = d; near = mn; } }
            if (En.Alive)
            { float d = Battle.FlatDistance(En.Feet, Me.Feet); if (d < best) { best = d; near = En; } }
            if (near == null) return;
            var col = near == En ? new Color(1f, 0.35f, 0.15f) : new Color(1f, 0.75f, 0.3f);
            string nm = near == En ? "Boss" : near.name;
            var wp = WorldFrame.FromWorld(near.Chest);
            var sp = cam.WorldToScreenPoint(wp);
            bool onScreen = sp.z > 0.2f && sp.x > 0 && sp.x < W && sp.y > 0 && sp.y < H;
            if (onScreen)
            {
                // 在畫面中：頭上的小標籤
                var hp = cam.WorldToScreenPoint(WorldFrame.FromWorld(near.head + Vector3.up * (near == En ? 0.6f : 0.45f)));
                if (hp.z > 0f) UiKit.Text(new Rect(hp.x - W * 0.2f, H - hp.y - lh * 1.1f, W * 0.4f, lh * 0.9f), $"▼ 最近 {best:F1}m", fsS, col, TextAnchor.LowerCenter, true);
                return;
            }
            // 不在畫面中：用鏡頭座標算方位（背後的也能指），貼在畫面邊緣
            var local = cam.transform.InverseTransformPoint(wp);
            var dir2 = new Vector2(local.x, local.y);
            if (local.z < 0f && dir2.sqrMagnitude < 0.01f) dir2 = new Vector2(0f, -1f);   // 正後方：指下方
            if (dir2.sqrMagnitude < 1e-6f) dir2 = Vector2.right;
            dir2.Normalize();
            float margin = W * 0.12f;
            var c = new Vector2(W / 2f, H / 2f);
            float kx = (W / 2f - margin) / Mathf.Max(1e-4f, Mathf.Abs(dir2.x)), ky = (H / 2f - margin * 1.6f) / Mathf.Max(1e-4f, Mathf.Abs(dir2.y));
            var edge = c + new Vector2(dir2.x, -dir2.y) * Mathf.Min(kx, ky);   // GUI 的 y 向下
            float ang = Mathf.Atan2(-dir2.y, dir2.x) * Mathf.Rad2Deg;
            string arrow = ang > -22.5f && ang <= 22.5f ? "➡" : ang > 22.5f && ang <= 67.5f ? "↘" : ang > 67.5f && ang <= 112.5f ? "⬇" : ang > 112.5f && ang <= 157.5f ? "↙"
                : ang > 157.5f || ang <= -157.5f ? "⬅" : ang > -157.5f && ang <= -112.5f ? "↖" : ang > -112.5f && ang <= -67.5f ? "⬆" : "↗";
            string back = local.z < 0f ? "（身後）" : "";
            var r = new Rect(edge.x - W * 0.17f, edge.y - lh * 0.75f, W * 0.34f, lh * 1.5f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            UiKit.Card(r, 0.6f * pulse, col, 0.9f);
            UiKit.Text(new Rect(r.x + 4, r.y + 2, r.width - 8, r.height * 0.55f), $"{arrow} {nm}", fsS, col, TextAnchor.MiddleCenter, true);
            UiKit.Text(new Rect(r.x + 4, r.y + r.height * 0.5f, r.width - 8, r.height * 0.45f), $"{best:F1}m{back}", fsS, Color.white, TextAnchor.MiddleCenter);
        }

        /// <summary>法師詠唱中：畫面中央畫出要照著畫的符文軌跡（起點有圓點、箭頭表示方向）</summary>
        void DrawRuneGuide(float W, float H)
        {
            var me = Me;
            if (me?.charging == null) return;
            if (HandGesture.ReleaseOf(me.charging) != HandGesture.Style.Rune) return;   // 法師現在用比數字，不畫符文
            var rune = RuneRecognizer.RuneOf(me.charging.id);
            var g = RuneRecognizer.Guide(rune);
            if (g == null || (Hand != null && Hand.RuneReady)) return;   // 畫好了就不再顯示軌跡
            float R = Mathf.Min(W, H) * 0.22f, cx = W / 2f, cy = H * 0.45f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            var c = me.charging.color;
            for (int i = 0; i < g.Length; i++)
            {
                var p = new Vector2(cx + g[i].x * R, cy - g[i].y * R);
                float sz = i == 0 ? R * 0.12f : R * 0.045f;
                GUI.color = new Color(c.r, c.g, c.b, i == 0 ? 0.9f : 0.35f + 0.25f * pulse);
                GUI.DrawTexture(new Rect(p.x - sz / 2, p.y - sz / 2, sz, sz), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            UiKit.Pill(cy + R * 1.1f + UiKit.BaseFont, $"用食指畫「{RuneRecognizer.RuneName(rune)}」符文（大圓點是起點）", UiKit.BaseFont, new Color(c.r, c.g, c.b, 1f), W * 0.94f);
        }

        string StatusText(Fighter f, float now)
        {
            var parts = new List<string>();
            if (f.blockUntil > now) parts.Add($"格擋 {f.blockUntil - now:F1}s");
            if (f.shieldUntil > now) parts.Add($"護盾 {f.shieldLeft}");
            if (f.counterUntil > now) parts.Add($"反擊 {f.counterUntil - now:F1}s");
            if (f.armorUntil > now) parts.Add($"守護 {f.armorUntil - now:F1}s");
            if (f.dots.Count > 0) parts.Add("中毒");
            if (f.snaredUntil > now) parts.Add($"定身 {f.snaredUntil - now:F1}s");
            if (f.blindUntil > now) parts.Add("致盲");
            int traps = 0; foreach (var t in battle.traps) if (t.owner == f) traps++;
            if (traps > 0) parts.Add($"陷阱×{traps}");
            // 進階職業
            if (f.adv == "berserker" && f.RageMul > 1.01f) parts.Add($"怒氣 +{(f.RageMul - 1f) * 100f:0}%");
            if (f.adv == "marksman") { int st = battle.EagleStacks(f); parts.Add(st > 0 ? $"鷹眼 {st}/3" : "鷹眼（站著不動）"); }
            if (f.adv == "inquisitor") parts.Add($"信仰 {f.faith}/{Battle.FaithMax}");
            if (f.adv == "bishop" && battle.Still(f)) parts.Add("祈禱領域");
            if (f.adv == "archmage" && f.ampNext) parts.Add("增幅中");
            if (f.comboUntil > now) parts.Add($"連殺 {f.comboUntil - now:F1}s");
            if (f.frenzyUntil > now) parts.Add($"狂暴 {f.frenzyUntil - now:F1}s");
            if (f.undyingUntil > now) parts.Add("不死之軀");
            if (f.invulnUntil > now) parts.Add($"無敵 {f.invulnUntil - now:F1}s");
            if (f.manaShieldUntil > now) parts.Add($"魔力護盾 {f.manaShieldUntil - now:F1}s");
            if (f.selfOathUntil > now) parts.Add("守護誓言");
            if (f.oathUntil > now) parts.Add("受誓言守護");
            if (f.subUntil > now) parts.Add("替身");
            if (f.cloneUntil > now) parts.Add($"分身 {f.cloneUntil - now:F1}s");
            if (f.danceUntil > now) parts.Add($"影舞 {f.danceUntil - now:F1}s");
            if (f.rollUntil > now) parts.Add($"翻滾射擊 {f.rollUntil - now:F1}s");
            if (f.markNextUntil > now) parts.Add("致命標記");
            if (f.blessUntil > now) parts.Add("復活祝福");
            if (f.markedUntil > now) parts.Add("被懲戒");
            int pets = 0; foreach (var pt in battle.pets) if (pt.owner == f && pt.kind != "clone" && !pt.remote) pets++;
            if (pets > 0) parts.Add($"召喚物×{pets}");
            return string.Join("　", parts);
        }
    }
}
