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
    public class SoloBattle : MonoBehaviour
    {
        public enum Phase { Hidden, Setup, Fighting, Over }
        public Phase phase { get; private set; } = Phase.Hidden;
        public Action RequestRedraw, RequestChangeMode;
        public HandGesture Hand;          // 手勢（GameRoot 每幀更新）

        // ---------------------------------------------------------------- 雙人連線模式（由 GameRoot 設定）
        // 對手是真人：沒有 AI、沒有角色模型；對手的位置由 GameRoot 每幀提供（鏡頭偵測＋對手回報融合），
        // 判定採「攻擊方判定」：我的法術打到對手 → 送 hit2，對手自己扣血、套用格擋／護盾／反擊。
        public bool NetMode;
        public Action<Msg> NetSend;
        public Func<double> SharedClock;
        public bool NetHasTarget, NetLock;
        public Vector3 NetEnemyHeadW, NetEnemyFwdW;
        public Rect NetLockRect;
        bool localReady, remoteReady;
        string remoteClass; string[] remoteLoadout;
        float nextState;
        Transform remoteAnchor;           // 對手位置的空物件（光效跟著它）

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
        class Floater { public Vector3 posMap; public string text; public Color color; public float born; public bool screen; }
        readonly List<Floater> floaters = new List<Floater>();
        string message = ""; float messageUntil;
        float incomingFlash, hitFlash;
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
            voiceOn = true;   // 一定要唸技能名稱才能詠唱，語音固定開啟
            voice = VoiceTemplates.Load();
            speech = new SpeechInput();
            speech.OnSkill += OnSpeechSkill;
            LoadLoadout();
        }

        void LoadLoadout()
        {
            myLoadout.Clear();
            var saved = PlayerPrefs.GetString("sd_loadout_" + myClass, "");
            var cls = Skills.Classes[myClass];
            foreach (var id in saved.Split(',')) if (Array.IndexOf(cls.skills, id) >= 0 && myLoadout.Count < 3) myLoadout.Add(id);
            if (myLoadout.Count != 3) { myLoadout.Clear(); myLoadout.AddRange(cls.defaultLoadout); }
        }

        void SaveChoices()
        {
            PlayerPrefs.SetString("sd_my_class", myClass);
            PlayerPrefs.SetString("sd_enemy_class", enemyClass);
            PlayerPrefs.SetString("sd_loadout_" + myClass, string.Join(",", myLoadout));
        }

        public void ShowSetup() { EndBattle(); phase = Phase.Setup; localReady = false; }

        // ================================================================ 雙人：準備 → 開戰
        void SendReady()
        {
            SaveChoices();
            localReady = true;
            NetSend?.Invoke(new Msg { t = "ready", k = myClass, ks = myLoadout.ToArray() });
            Say("已準備，等待對手…", 3f);
            TryStartNet();
        }

        void TryStartNet()
        {
            if (!NetMode || !localReady || !remoteReady || remoteClass == null) return;
            localReady = remoteReady = false;
            StartNetBattle();
        }

        void StartNetBattle()
        {
            EndBattle();
            var ec = Skills.Classes.TryGetValue(remoteClass, out var found) ? found : Skills.Classes["mage"];
            enemyClassUsed = ec.id;
            dummyMode = false;
            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            var en = new Fighter($"對手{ec.name}", ec, remoteLoadout ?? new string[0], false);
            en.head = NetEnemyHeadW;
            SyncPlayer(me);
            battle = new Battle(me, en) { remoteEnemy = true };
            battle.OnEvent += OnBattleEvent;
            battle.OnSpawn += p => NetSend?.Invoke(new Msg { t = "cast2", id = p.id, k = p.skill.id, p = p.basePos, d = p.dir, t0 = SharedClock?.Invoke() ?? 0, ok = p.reflected, rad = p.curveAmp, s = p.pathLen });
            battle.OnRemoteBurn += (sk, dmg, at) => NetSend?.Invoke(new Msg { t = "burn2", k = sk.id, dmg = Mathf.RoundToInt(dmg), p = at });
            battle.OnRemoteHit += (sk, dmg, at, id) => NetSend?.Invoke(new Msg { t = "hit2", id = id, k = sk.id, dmg = dmg, p = at });
            battle.OnTrapPlaced += t => NetSend?.Invoke(new Msg { t = "trap2", id = t.id, k = t.skill.id, p = t.pos, s = t.armAt - battle.now });
            battle.OnTrapGone += id => NetSend?.Invoke(new Msg { t = "trapgone", id = id });
            ai = null;
            remoteAnchor = new GameObject("Remote Anchor").transform;
            lastEnemyFeet = en.Feet; lastEnemyHp = en.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            phase = Phase.Fighting;
            Say($"⚔ 對手：{ec.name}（{string.Join("・", Array.ConvertAll(remoteLoadout ?? new string[0], id => Skills.All.TryGetValue(id, out var sd) ? sd.name : id))}）", 4f);
        }

        /// <summary>GameRoot 轉來的對戰訊息</summary>
        public void OnNet(Msg m)
        {
            switch (m.t)
            {
                case "ready":
                    remoteClass = m.k; remoteLoadout = m.ks; remoteReady = true;
                    if (phase != Phase.Fighting) Say("對手已準備好", 2f);
                    TryStartNet();
                    return;
            }
            if (battle == null || !NetMode) return;
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
                        if (isTrap) battle.RemoveRemoteTrap(-m.id); else battle.RemoveRemoteProjectile(m.id);
                        bool countered = battle.ApplyRemoteHit(hs, m.dmg, m.p, isTrap);
                        NetSend?.Invoke(new Msg { t = "hp2", hp = Mathf.CeilToInt(Me.hp), ok = countered });
                    }
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
                    break;
            }
        }

        void SendState()
        {
            if (!NetMode || battle == null || Time.time < nextState) return;
            nextState = Time.time + 0.1f;
            var me = Me; float now = battle.now;
            int e = (me.blockUntil > now ? 1 : 0) | (me.shieldUntil > now ? 2 : 0) | (me.counterUntil > now ? 4 : 0) | (me.armed != null ? 8 : 0);
            NetSend?.Invoke(new Msg { t = "state", k = me.charging != null ? me.charging.id : "", s = battle.ChargeProgress(me), hp = Mathf.CeilToInt(me.hp), e = e });
        }
        public void Hide() { EndBattle(); phase = Phase.Hidden; }

        // ================================================================ 開戰
        void StartBattle()
        {
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
            dummyMode = enemyClass == "dummy";
            dummyDamage = 0f; dummyHits = 0;
            var en = dummyMode
                ? new Fighter("木頭人", DummyClass, new List<string>(), false)
                : new Fighter($"電腦{ec.name}", ec, enemySkills, false);
            // 敵人出生點：場地內、玩家面向的那一側
            float reach = area.ReachInside(area.Origin.forward, 0.6f);
            en.head = new Vector3(0f, 1.6f, Mathf.Clamp(reach, 0f, dummyMode ? 3f : 6f));   // 大場地時離遠一點出場（最遠 6m；木頭人 3m）
            SyncPlayer(me);
            battle = new Battle(me, en);
            battle.OnEvent += OnBattleEvent;
            ai = dummyMode ? null : new EnemyAI(battle, en, p => area.Inside(WorldFrame.FromWorld(p)), p => area.DistanceToEdge(WorldFrame.FromWorld(p)), Environment.TickCount);

            enemyRig = CharacterRig.Create(ec.id);
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
            foreach (var go in projGo.Values) if (go) Destroy(go);
            foreach (var go in trapGo.Values) if (go) Destroy(go);
            foreach (var go in burnGo.Values) if (go) Destroy(go);
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
            if (phase != Phase.Fighting && phase != Phase.Over) return;
            if (battle == null) return;
            SyncPlayer(Me);
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            hitFlash = Mathf.Max(0f, hitFlash - Time.deltaTime * 2.5f);
            frozen = phase == Phase.Fighting && !Tracking.Ok;
            if (NetMode) SyncRemote();
            UpdateLock();
            if (phase == Phase.Fighting && (!frozen || NetMode))   // 雙人：追蹤中斷不暫停（只是暫時不能施法）
            {
                if (ai != null) ai.Update(dt);
                else if (!NetMode) en_FaceMe();
                battle.Update(dt);
                if (!frozen) HandleInput();
                SendState();
                if (battle.Over) phase = Phase.Over;
            }
            UpdateVisuals();
        }

        // 雙人：對手位置（GameRoot 提供：鏡頭偵測＋對手回報）；手機拿在身體前方 → 往後退一點才是身體
        void SyncRemote()
        {
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
        }

        bool InTapZone(Vector2 guiPos) => guiPos.y > Screen.height * 0.2f && guiPos.y < Screen.height * 0.74f;

        void HandleInput()
        {
            if (Hand == null) return;
            bool drawingRune = Me.charging != null && HandGesture.ReleaseOf(Me.charging) == HandGesture.Style.Rune;
            // 符文畫好＝詠唱完成（蓄力直接滿）
            if (drawingRune && Hand.RuneReady && battle.ChargeProgress(Me) < 1f)
            {
                Me.chargeStart = battle.now - Me.charging.charge;
                Say($"✨ {RuneRecognizer.RuneName(RuneRecognizer.RuneOf(Me.charging.id))}符文完成！把準星對準目標，食指往前指發射", 1.5f);
            }
            // 弓箭手：握拳的手拉出畫面外＝拉滿弓＝蓄力完成
            if (Me.charging != null && HandGesture.ReleaseOf(Me.charging) == HandGesture.Style.Bow && Hand.BowDrawn && battle.ChargeProgress(Me) < 1f)
            {
                Me.chargeStart = battle.now - Me.charging.charge;
                Say("🏹 拉滿弓！把準星對準目標，手回到畫面張開放箭", 1.5f);
            }
            // 選技能只能用唸的：每次放技能前都要先唸出技能名稱（手勢只負責放招）
            Hand.ConsumeSelect(out _);
            // 放招動作依詠唱中的技能（陷阱往下壓、治癒收回、格擋舉盾…；其餘依職業）；沒在詠唱就不偵測
            Hand.ReleaseStyle = Me.charging != null ? HandGesture.ReleaseOf(Me.charging) : HandGesture.Style.None;
            Hand.RuneName = Me.charging != null ? RuneRecognizer.RuneOf(Me.charging.id) : null;
            // 手勢放招：往放招動作的瞄準點放
            if (Hand.ConsumeRelease())
            {
                if (frozen) { Say("AR 追蹤中斷，暫時不能施法", 1.5f); return; }
                if (Me.charging == null) Say("🎤 先唸出技能名稱才能放招", 1.5f);
                else if (drawingRune) ReleaseAt(ScreenCenter, true);   // 符文畫好＋食指往前指
                else if (battle.ChargeProgress(Me) < 1f) Say("蓄力還沒完成", 1f);
                else ReleaseAt(ScreenCenter, true);   // 瞄準一律用畫面中央的準星（手只負責觸發）
            }
        }

        static Vector2 ScreenCenter => new Vector2(Screen.width / 2f, Screen.height / 2f);

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
            bool wantSpeech = UseSpeech && voiceOn && phase == Phase.Fighting && recordingSkill == null;
            if (wantSpeech && !speech.Running) { speech.Candidates = myLoadout; speech.Start(); }
            else if (!wantSpeech && speech != null && speech.Running) speech.Stop();
            speech?.Tick();

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

        void OnSpeechSkill(string id)
        {
            if (phase != Phase.Fighting || battle == null || frozen || !Skills.All.TryGetValue(id, out var skill)) return;
            heard = $"🎤 {skill.name}"; heardUntil = Time.time + 1.5f;
            if (Me.charging != skill && !battle.TryChant(Me, skill, out var why)) Say(why, 1.5f);
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
        }

        /// <summary>敵人有沒有出現在我的畫面中（頭、胸、腳任一處），並算出鎖定框</summary>
        void UpdateLock()
        {
            enemyOnScreen = false;
            var en = En;
            if (en == null || !en.Alive || !Tracking.Ok) return;
            if (NetMode)
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
                float halfW = Fighter.BodyRadius / depth * Screen.height / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                float cx = (xMin + xMax) * 0.5f;
                enemyRect = Rect.MinMaxRect(cx - halfW, Screen.height - yMax, cx + halfW, Screen.height - yMin);
            }
        }

        void OnBattleEvent(string kind, Vector3 at, Color c, string text)
        {
            switch (kind)
            {
                case "hit": case "trap": case "mitigate": case "effect": case "counter": case "buff": case "heal": case "trapSet":
                    if (!string.IsNullOrEmpty(text)) floaters.Add(new Floater { posMap = at, text = text, color = c, born = Time.time });
                    if (kind == "hit" || kind == "trap") { Burst(at, c); SpellFx.Impact(kind == "hit" ? myClass : "", c, WorldFrame.FromWorld(at), 1f); }
                    break;
                case "hurt":
                    // 被打中：震動＋整個畫面閃紅＋扣血（血量已在 Battle 裡扣掉）
                    floaters.Add(new Floater { text = text, color = new Color(1f, 0.3f, 0.3f), born = Time.time, screen = true });
                    hitFlash = 1f;
                    Handheld.Vibrate();
                    break;
                case "armed":
                    floaters.Add(new Floater { posMap = at, text = text, color = c, born = Time.time, screen = at == Me.Chest });
                    if (at == Me.Chest) SpellFx.CastBurst(myClass, c, cam.transform.position + cam.transform.forward * 0.5f - cam.transform.up * 0.08f, cam.transform.forward, 0.35f);
                    break;
                case "ambush":
                    // 伏擊出手：在被刺的人身上爆開
                    floaters.Add(new Floater { posMap = at, text = text, color = c, born = Time.time });
                    SpellFx.Impact("assassin", c, WorldFrame.FromWorld(at), 1.2f);
                    break;
                case "armedExpire":
                    if (at == Me.Chest) Say(text + "（5 秒內沒靠近）", 1.5f);
                    break;
                case "burnzone":
                    SpellFx.Impact("mage", c, WorldFrame.FromWorld(at), 2.5f);   // 隕石落地爆炸
                    break;
                case "burn":
                {
                    bool meBurn = Battle.FlatDistance(at, Me.Feet) < 0.5f || at == Me.Chest;
                    floaters.Add(new Floater { posMap = at + Vector3.up * 1.2f, text = "灼燒 " + text, color = new Color(1f, 0.55f, 0.2f), born = Time.time, screen = meBurn });
                    if (meBurn) { hitFlash = Mathf.Max(hitFlash, 0.45f); Handheld.Vibrate(); }
                    break;
                }
                case "dot":
                    bool onMe = at == Me.Chest;
                    floaters.Add(new Floater { posMap = at, text = "毒 " + text, color = Color.green, born = Time.time, screen = onMe });
                    if (onMe) hitFlash = Mathf.Max(hitFlash, 0.4f);   // 中毒扣血：淡一點的紅
                    break;
                case "miss":
                    break;
                case "timeout":
                    if (at == Me.Chest) Say("詠唱逾時", 1.5f);
                    break;
                case "win": Say("🏆 勝利！", 99f); break;
                case "lose": Say("💀 敗北…", 99f); break;
            }
        }

        // ================================================================ 3D 顯示
        void UpdateVisuals()
        {
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
            lastEnemyFeet = en.Feet;
            if (en.hp < lastEnemyHp - 0.01f) { if (enemyRig) enemyRig.PlayHit(); if (dummyMode) { dummyDamage += lastEnemyHp - en.hp; dummyHits++; } }
            lastEnemyHp = en.hp;
            if (!en.Alive && !enemyDeadShown) { if (enemyRig) enemyRig.SetDead(true); enemyDeadShown = true; }
            bool guarded = en.blockUntil > battle.now || en.shieldUntil > battle.now || en.counterUntil > battle.now;
            if (enemyRig) enemyRig.Guarding = guarded && en.Alive;
            enemyShield.SetActive(guarded && en.Alive);
            if (guarded)
            {
                enemyShield.transform.position = WorldFrame.FromWorld(en.Chest);
                enemyShield.transform.localScale = new Vector3(0.9f, 1.9f, 0.9f);
                var col = en.counterUntil > battle.now ? new Color(0.96f, 0.45f, 0.71f, 0.3f) : en.shieldUntil > battle.now ? new Color(0.6f, 0.65f, 0.7f, 0.3f) : new Color(0.38f, 0.65f, 0.98f, 0.3f);
                enemyShield.GetComponent<Renderer>().material.color = col;
            }


            // 法術：球體＋拖尾
            var alive = new HashSet<Projectile>(battle.projectiles);
            foreach (var p in battle.projectiles)
            {
                if (!projGo.TryGetValue(p, out var go))
                {
                    // 依技能的法術造型（箭、火球、冰錐、雷光、劍氣、飛刀…），已含光暈與拖尾
                    string fxCls = p.owner == En ? enemyClassUsed : myClass;
                    go = SpellFx.CreateProjectileVisual(p.skill, fxCls);
                    projGo[p] = go;
                    if (p.owner == En)
                    {
                        if (enemyRig) enemyRig.PlayCast();   // 敵人出招動作＋光效
                        SpellFx.CastBurst(enemyClassUsed, p.skill.color, WorldFrame.FromWorld(p.pos), WorldFrame.DirFromWorld(p.dir), 1f);
                    }
                }
                var dirS = WorldFrame.DirFromWorld(p.dir);
                go.transform.SetPositionAndRotation(WorldFrame.FromWorld(p.pos), dirS.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dirS, Vector3.up) : Quaternion.identity);
                // 敵人的法術逼近時畫面邊框閃紅
                if (p.owner == En && Vector3.Distance(p.pos, Me.Chest) < 2.5f && Vector3.Dot(p.dir, Me.Chest - p.pos) > 0) incomingFlash = Mathf.Max(incomingFlash, 0.6f);
            }
            foreach (var kv in new List<KeyValuePair<Projectile, GameObject>>(projGo))
                if (!alive.Contains(kv.Key)) { Destroy(kv.Value, 0.25f); projGo.Remove(kv.Key); }

            // 火海：地上的燃燒圓盤（閃爍）＋不時冒出火焰
            var liveBurns = new HashSet<BurnZone>(battle.burns);
            foreach (var z in battle.burns)
            {
                if (!burnGo.TryGetValue(z, out var go))
                {
                    go = Prim(PrimitiveType.Cylinder, new Color(1f, 0.35f, 0.05f, 0.45f));
                    go.transform.localScale = new Vector3(z.radius * 2f, 0.004f, z.radius * 2f);
                    burnGo[z] = go;
                }
                go.transform.position = WorldFrame.FromWorld(z.pos + Vector3.up * 0.015f);
                float left = Mathf.Clamp01((z.until - battle.now) / 2f);   // 最後 2 秒淡出
                float flick = 0.35f + 0.15f * Mathf.Sin(Time.time * 13f + z.pos.x * 7f) + 0.1f * Mathf.Sin(Time.time * 29f);
                go.GetComponent<Renderer>().material.color = new Color(1f, 0.3f + 0.15f * flick, 0.05f, flick * left);
                if (UnityEngine.Random.value < Time.deltaTime * 2.2f)
                {
                    var r2 = UnityEngine.Random.insideUnitCircle * z.radius * 0.85f;
                    SpellFx.Impact("mage", new Color(1f, 0.45f, 0.1f), WorldFrame.FromWorld(z.pos + new Vector3(r2.x, 0.05f, r2.y)), 0.6f);
                }
            }
            foreach (var kv in new List<KeyValuePair<BurnZone, GameObject>>(burnGo))
                if (!liveBurns.Contains(kv.Key)) { Destroy(kv.Value); burnGo.Remove(kv.Key); }

            // 陷阱：地板上的扁圓盤（生效前是灰色）
            var liveTraps = new HashSet<Trap>(battle.traps);
            foreach (var t in battle.traps)
            {
                if (!trapGo.TryGetValue(t, out var go))
                {
                    go = Prim(PrimitiveType.Cylinder, Color.gray);
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

        GameObject Prim(PrimitiveType type, Color c)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.material = mat;
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
            if (phase == Phase.Setup) DrawSetup();
            else if (phase == Phase.Fighting || phase == Phase.Over) DrawHud();
        }

        void Panel(Rect r, float alpha = 0.6f)
        {
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void DrawSetup()
        {
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f), fsL = Mathf.RoundToInt(fs * 1.25f);
            float pad = W * 0.035f, gap = pad * 0.5f;
            // 背景：整片暗色
            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.82f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;

            // 依可用高度決定單位行高（內容約 21 行），放得下就用基準行高
            float avail = botY - topY - pad * 2;
            float u = Mathf.Min(fs * 1.75f, avail / 21.5f);
            float y = topY + pad;
            var cls = Skills.Classes[myClass];

            UiKit.Text(new Rect(pad, y, W - pad * 2, u * 1.2f), NetMode ? "職業對戰：選擇職業與技能" : "選擇職業與技能", fsL, Color.white, TextAnchor.MiddleLeft, true);
            y += u * 1.3f;

            // 職業（4 個）
            float cw = (W - pad * 2 - gap * 3) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var id = Skills.ClassOrder[i]; var c = Skills.Classes[id];
                if (UiKit.Button(new Rect(pad + i * (cw + gap), y, cw, u * 1.3f), c.name, fs, id == myClass, c.color) && id != myClass) { myClass = id; LoadLoadout(); }
            }
            y += u * 1.3f + gap;
            cls = Skills.Classes[myClass];
            var descR = new Rect(pad, y, W - pad * 2, u * 1.5f);
            UiKit.Card(descR, 0.5f, cls.color, 0.35f);
            UiKit.Text(new Rect(descR.x + gap, descR.y + gap * 0.4f, descR.width - gap * 2, descR.height - gap * 0.8f), $"{cls.desc}\nHP {cls.maxHp}・MP {cls.maxMp}", fsS, new Color(0.9f, 0.92f, 1f));
            y += u * 1.5f + gap;

            // 技能卡片（2 欄）：圖示＋名稱／手勢→放招／數值
            UiKit.Text(new Rect(pad, y, W - pad * 2, u), $"選 3 個技能（{myLoadout.Count}/3）　唸技能名稱＝詠唱 → 做動作＝放招", fsS, new Color(1f, 0.9f, 0.6f));
            y += u;
            float sw = (W - pad * 2 - gap) / 2f, sh = u * 2.4f;
            for (int i = 0; i < cls.skills.Length; i++)
            {
                var sk = Skills.All[cls.skills[i]];
                bool on = myLoadout.Contains(sk.id);
                var r = new Rect(pad + (i % 2) * (sw + gap), y + (i / 2) * (sh + gap), sw, sh);
                UiKit.Card(r, on ? 0.75f : 0.55f, on ? sk.color : (Color?)null, 0.95f);
                float isz = sh * 0.82f;
                var ir = new Rect(r.x + sh * 0.09f, r.y + sh * 0.09f, isz, isz);
                GUI.color = on ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                GUI.DrawTexture(ir, SkillIcons.Get(sk.id));
                GUI.color = Color.white;
                float tx = ir.xMax + sh * 0.08f, tw = r.xMax - tx - sh * 0.06f;
                string eff = sk.type == SkillType.Self ? (sk.self == SelfKind.Heal ? $"回復{sk.heal}" : "防禦") : sk.damage > 0 ? $"傷害{sk.damage}{(sk.multi > 1 ? $"×{sk.multi}" : "")}" : "效果";
                UiKit.Text(new Rect(tx, r.y + sh * 0.05f, tw, sh * 0.32f), (on ? "✔ " : "") + sk.name, fs, on ? Color.white : new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(tx, r.y + sh * 0.37f, tw, sh * 0.27f), $"🎤唸「{sk.name}」→ {HandGesture.StyleName(HandGesture.ReleaseOf(sk))}", fsS, new Color(1f, 0.85f, 0.5f));
                UiKit.Text(new Rect(tx, r.y + sh * 0.64f, tw, sh * 0.32f), $"MP{sk.cost}・{(sk.charge > 0f ? $"蓄力{sk.charge:0.#}s" : "免蓄力")}・{eff}・{sk.RangeText}", fsS, new Color(0.8f, 0.85f, 0.95f));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (on) myLoadout.Remove(sk.id);
                    else { if (myLoadout.Count >= 3) myLoadout.RemoveAt(0); myLoadout.Add(sk.id); }
                }
            }
            y += ((cls.skills.Length + 1) / 2) * (sh + gap);

            // 敵人（單人：3×2；雙人：顯示對手狀態）
            if (!NetMode)
            {
                UiKit.Text(new Rect(pad, y, W - pad * 2, u), "敵人", fsS, new Color(1f, 0.9f, 0.6f));
                y += u;
                float ew = (W - pad * 2 - gap * 2) / 3f;
                for (int i = 0; i < 6; i++)
                {
                    string id = i == 0 ? "random" : i == 5 ? "dummy" : Skills.ClassOrder[i - 1];
                    string nm = i == 0 ? "隨機" : i == 5 ? "🪵 木頭人" : Skills.Classes[id].name;
                    var c = i == 0 ? new Color(1f, 0.85f, 0.3f) : i == 5 ? new Color(0.75f, 0.55f, 0.3f) : Skills.Classes[id].color;
                    if (UiKit.Button(new Rect(pad + (i % 3) * (ew + gap), y + (i / 3) * (u * 1.15f + gap), ew, u * 1.15f), nm, fsS, id == enemyClass, c)) enemyClass = id;
                }
                y += 2 * (u * 1.15f + gap);
            }
            else
            {
                string rs = remoteReady ? $"對手：{(Skills.Classes.TryGetValue(remoteClass ?? "", out var rc) ? rc.name : "?")}（已準備）" : "對手：還沒準備好";
                UiKit.Text(new Rect(pad, y, W - pad * 2, u * 1.2f), rs, fs, remoteReady ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.8f, 0.5f));
                y += u * 1.3f;
            }

            // 語音
            float vx = pad, vw = W - pad * 2;
            if (UseSpeech) UiKit.Text(new Rect(vx, y, vw, u * 1.15f), "🎤 每次放技能前都要先唸出技能名稱（不用錄音）", fsS, new Color(0.85f, 0.9f, 1f));
            else
            {
                float rw = (vw - gap * 2) / 3f;
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

            // 下方固定：開始／準備、其他按鈕
            float by = botY - pad - u * 1.2f;
            float hw = (W - pad * 2 - gap) / 2f;
            if (!NetMode && UiKit.Button(new Rect(pad, by, hw, u * 1.2f), "重畫場地", fsS)) RequestRedraw?.Invoke();
            if (UiKit.Button(new Rect(NetMode ? pad : pad + hw + gap, by, NetMode ? W - pad * 2 : hw, u * 1.2f), NetMode ? "返回" : "換模式", fsS))
            { if (NetMode) Hide(); else RequestChangeMode?.Invoke(); }
            by -= u * 1.6f + gap;
            bool voiceOk = UseSpeech || VoiceReady;   // 沒有語音辨識時，要先把 3 個技能的咒語錄好
            string startText = !voiceOk ? "🎤 先錄好 3 個技能的咒語" : NetMode ? (localReady ? "等待對手準備…" : "⚔ 準備好了") : (enemyClass == "dummy" ? "🪵 開始練習" : "⚔ 開始戰鬥");
            if (UiKit.Button(new Rect(pad, by, W - pad * 2, u * 1.6f), startText, fsL, true, cls.color, myLoadout.Count == 3 && voiceOk))
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
            UiKit.Text(new Rect(ix, y, iw * 0.52f, lh), $"{en.name}　{battle.Distance:F1}m", fs, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Bar(new Rect(ix + iw * 0.54f, y + lh * 0.15f, iw * 0.46f, lh * 0.7f), en.hp / en.maxHp, new Color(1f, 0.25f, 0.35f), $"{Mathf.CeilToInt(en.hp)}", fsS);
            y += lh;
            string enState = dummyMode ? $"命中 {dummyHits} 次・累計傷害 {Mathf.RoundToInt(dummyDamage)}"
                : en.charging != null ? $"⚠ 詠唱 {en.charging.name}（{Mathf.FloorToInt(battle.ChargeProgress(en) * 100)}%）" : ai.Status;
            if (en.snaredUntil > now) enState = "被定身";
            UiKit.Text(new Rect(ix, y, iw, lh * 0.9f), enState, fsS, en.charging != null ? new Color(1f, 0.75f, 0.4f) : new Color(0.9f, 0.92f, 1f));
            y += lh * 0.9f;
            UiKit.Text(new Rect(ix, y, iw, lh * 0.8f), StatusText(en, now), fsS, new Color(0.75f, 0.85f, 1f));

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
                bool bow = HandGesture.ReleaseOf(me.charging) == HandGesture.Style.Bow && Hand != null;   // 弓箭手：拉滿弓就是蓄力完成
                string st = bow ? (Hand.BowDrawn ? "🏹 拉滿！手回畫面張開放箭" : Hand.BowStage == 1 ? "✊ 握著拳拉出畫面外（拉弓）" : "畫面中握拳（捏弦）") :
                    prog < 1f ? $"蓄力 {Mathf.FloorToInt(prog * 100)}%" :
                    me.charging.releaseNear ? "手刀往前刺 → 5 秒內靠近敵人自動出手" :
                    HandGesture.StyleHint(HandGesture.ReleaseOf(me.charging));
                UiKit.Pill(H / 2 - lh * 2.6f, $"{me.charging.name}　{st}", fs, prog < 1f ? Color.white : me.charging.color, pillW);
            }
            // 伏擊狀態：我在伏擊 → 提示靠近；敵人在伏擊 → 警告保持距離
            if (me.armed != null)
            {
                float leftT = Mathf.Max(0f, me.armedUntil - now), need = me.armed.rangeMax;
                UiKit.Pill(H * 0.31f, $"🗡 {me.armed.name}伏擊中：靠近到 {need:0.#}m 內自動出手（{battle.Distance:F1}m・剩 {leftT:F1} 秒）", fs, me.armed.color, pillW);
            }
            if (en.armed != null && en.Alive)
                UiKit.Pill(H * 0.37f, $"⚠ 敵人伏擊中！保持距離（> {en.armed.rangeMax:0.#}m）", fs, new Color(1f, 0.4f, 0.45f), pillW);

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
                    GUI.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                    var r = enemyRect;
                    // 只畫四個角，比較不擋畫面
                    float cl = Mathf.Min(r.width, r.height) * 0.28f;
                    GUI.DrawTexture(new Rect(r.xMin, r.yMin, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, cl), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - cl, r.yMin, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, cl), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMin, r.yMax - cl, t, cl), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - cl, r.yMax - t, cl, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - cl, t, cl), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    UiKit.Text(new Rect(r.x, r.yMax + 2, Mathf.Max(r.width, W * 0.3f), lh * 0.8f), "🎯 鎖定", fsS, new Color(0.4f, 1f, 0.5f), TextAnchor.UpperLeft, true);
                }
                else
                    UiKit.Pill(H * 0.58f, (enemyScreenSide < 0 ? "◀ 敵人在左邊" : "敵人在右邊 ▶") + "（沒鎖定也能放，朝準星飛）", fs, new Color(1f, 0.45f, 0.45f), pillW);
            }
            if (Time.time < messageUntil) UiKit.Pill(H * 0.64f, message, fs, Color.white, pillW);

            // 下方：我的狀態與技能（由下往上排）
            float sw = (W - pad * 2 - gap * 2) / 3f;
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
            UiKit.Text(new Rect(pad, statY, W - pad * 2, lh * 0.75f), StatusText(me, now), fsS, new Color(0.75f, 0.9f, 1f));

            for (int i = 0; i < me.loadout.Count; i++)
            {
                var s = me.loadout[i];
                var r = new Rect(pad + i * (sw + gap), sy, sw, tileH);
                float cd = me.cooldownUntil.TryGetValue(s.id, out var u) ? Mathf.Max(0, u - now) : 0;
                bool charging = me.charging == s;
                bool usable = me.mp >= s.cost && cd <= 0;
                // 技能卡（只顯示，不能點）：上面圖示、下面名稱／手勢→放招／MP 或冷卻；手正比著這個手勢時亮起來
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
                UiKit.Text(new Rect(tx, ty + rowH, tw, rowH), $"🎤→{HandGesture.StyleName(HandGesture.ReleaseOf(s))}", fsS, usable ? new Color(1f, 0.85f, 0.5f) : tc, TextAnchor.MiddleCenter);
                UiKit.Text(new Rect(tx, ty + rowH * 2, tw, rowH), cd > 0 ? $"冷卻 {cd:F1}s" : $"MP {s.cost}", fsS, cd > 0 ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.6f, 0.8f, 1f), TextAnchor.MiddleCenter);
            }

            if (frozen)
            {
                var fr = new Rect(pad, H * 0.3f, W - pad * 2, H * 0.2f);
                UiKit.Card(fr, 0.85f, new Color(1f, 0.75f, 0.3f), 0.8f);
                UiKit.Text(new Rect(fr.x + gap, fr.y + gap, fr.width - gap * 2, fr.height * 0.45f), NetMode ? "⚠ AR 追蹤中斷，暫時不能施法" : "⏸ AR 追蹤中斷，戰鬥暫停", fsL, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(fr.x + gap, fr.y + fr.height * 0.5f, fr.width - gap * 2, fr.height * 0.45f), Tracking.Reason + (NetMode ? "\n對手仍以你最後的位置判定" : "\n恢復追蹤後自動繼續"), fs, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleCenter);
            }

            if (phase == Phase.Over)
            {
                var orr = new Rect(pad, H * 0.28f, W - pad * 2, H * 0.32f);
                UiKit.Card(orr, 0.85f, me.Alive ? new Color(1f, 0.85f, 0.3f) : new Color(0.7f, 0.7f, 0.8f), 0.9f);
                UiKit.Text(new Rect(orr.x, orr.y + gap, orr.width, orr.height * 0.4f), me.Alive ? "🏆 勝利！" : "💀 敗北…", fs * 2, Color.white, TextAnchor.MiddleCenter, true);
                float bw = (orr.width - gap * 3) / 2f, bh = lh * 1.8f, oby = orr.yMax - gap - bh;
                if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), NetMode ? (localReady ? "等待對手…" : "再來一局") : "再來一局", fs, true, new Color(0.4f, 0.8f, 1f), !(NetMode && localReady)))
                { if (NetMode) SendReady(); else StartBattle(); }
                if (UiKit.Button(new Rect(orr.x + gap * 2 + bw, oby, bw, bh), "換職業", fs)) ShowSetup();
            }
        }

        /// <summary>法師詠唱中：畫面中央畫出要照著畫的符文軌跡（起點有圓點、箭頭表示方向）</summary>
        void DrawRuneGuide(float W, float H)
        {
            var me = Me;
            if (me?.charging == null) return;
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
            if (f.dots.Count > 0) parts.Add("中毒");
            if (f.snaredUntil > now) parts.Add($"定身 {f.snaredUntil - now:F1}s");
            if (f.blindUntil > now) parts.Add("致盲");
            int traps = 0; foreach (var t in battle.traps) if (t.owner == f) traps++;
            if (traps > 0) parts.Add($"陷阱×{traps}");
            return string.Join("　", parts);
        }
    }
}
