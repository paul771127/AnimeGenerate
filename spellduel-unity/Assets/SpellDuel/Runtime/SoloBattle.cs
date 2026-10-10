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
            voiceOn = PlayerPrefs.GetInt("sd_voice_on", 1) == 1;
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
            battle.OnSpawn += p => NetSend?.Invoke(new Msg { t = "cast2", id = p.id, k = p.skill.id, p = p.pos, d = p.dir, t0 = SharedClock?.Invoke() ?? 0, ok = p.reflected });
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
                        battle.AddRemoteProjectile(m.id, cs, m.p, m.d, Mathf.Clamp(elapsed, 0f, 1f), m.ok);
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
                    break;
            }
        }

        void SendState()
        {
            if (!NetMode || battle == null || Time.time < nextState) return;
            nextState = Time.time + 0.1f;
            var me = Me; float now = battle.now;
            int e = (me.blockUntil > now ? 1 : 0) | (me.shieldUntil > now ? 2 : 0) | (me.counterUntil > now ? 4 : 0);
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
            Say(dummyMode ? "🪵 木頭人練習：比出技能手勢詠唱，握拳→張開放招" : $"⚔ 對手：{en.name}（{string.Join("・", enemySkills.ConvertAll(id => Skills.All[id].name))}）", 4f);
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
            projGo.Clear(); trapGo.Clear(); floaters.Clear();
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
            // 比出技能手勢（維持 0.35 秒）＝詠唱該技能（不再用點螢幕選招）；法師畫符文時手指會比出各種形狀，不換招
            if (Hand.ConsumeSelect(out var shape) && !frozen && !drawingRune)
            {
                var skill = Me.loadout.Find(sk => Skills.GestureOf(sk.id) == shape);
                if (skill != null && Me.charging != skill)
                {
                    if (!battle.TryChant(Me, skill, out var why)) Say(why, 1.5f);
                    else Say($"{HandGesture.ShapeName(shape)} → {skill.name}", 1f);
                }
            }
            // 放招動作依詠唱中的技能（陷阱往下壓、治癒收回、格擋舉盾…；其餘依職業）；沒在詠唱就不偵測
            Hand.ReleaseStyle = Me.charging != null ? HandGesture.ReleaseOf(Me.charging) : HandGesture.Style.None;
            Hand.RuneName = Me.charging != null ? RuneRecognizer.RuneOf(Me.charging.id) : null;
            // 手勢放招：往放招動作的瞄準點放
            if (Hand.ConsumeRelease())
            {
                if (frozen) { Say("AR 追蹤中斷，暫時不能施法", 1.5f); return; }
                if (Me.charging == null) Say("先唸技能名稱（或比技能手勢）開始詠唱", 1.5f);
                else if (drawingRune) ReleaseAt(ScreenCenter, true);   // 符文畫好＋食指往前指
                else if (battle.ChargeProgress(Me) < 1f) Say("蓄力還沒完成", 1f);
                else ReleaseAt(ScreenCenter, true);   // 瞄準一律用畫面中央的準星（手只負責觸發）
            }
        }

        static Vector2 ScreenCenter => new Vector2(Screen.width / 2f, Screen.height / 2f);

        void ReleaseAt(Vector2 sp, bool byGesture)
        {
            if (Me.charging == null) { Say("先比出技能手勢（或唸咒語）開始詠唱", 1.5f); return; }
            if (Me.charging.type == SkillType.Projectile && !enemyOnScreen && battle.ChargeProgress(Me) >= 1f)
            { Say("🎯 敵人不在畫面中，轉向敵人才能鎖定", 1.5f); return; }

            // 點擊方向（場地座標）與地板交點（陷阱用）
            var ray = cam.ScreenPointToRay(sp);
            var o = WorldFrame.ToWorld(ray.origin);
            var d = WorldFrame.DirToWorld(ray.direction);
            var floor = d.y < -0.01f ? o + d * (-o.y / d.y) : Me.Feet + Fighter.Flat(Me.forward) * 1.5f;
            if (!battle.TryRelease(Me, d, floor, out var why)) Say(why, 1.5f);
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
            float W = Screen.width, H = Screen.height, pad = W * 0.03f, lh = label.fontSize * 1.7f;
            float top = H - Screen.safeArea.yMax + pad;
            Panel(new Rect(0, 0, W, H), 0.55f);
            float y = top;
            GUI.Label(new Rect(pad, y, W, lh), "我的職業", label); y += lh;
            float bw = (W - pad * 5) / 4f, bh = lh * 1.3f;
            for (int i = 0; i < 4; i++)
            {
                var id = Skills.ClassOrder[i];
                GUI.color = id == myClass ? Skills.Classes[id].color : Color.white;
                if (GUI.Button(new Rect(pad + i * (bw + pad), y, bw, bh), Skills.Classes[id].name, button) && id != myClass) { myClass = id; LoadLoadout(); }
            }
            GUI.color = Color.white; y += bh + pad * 0.5f;
            var cls = Skills.Classes[myClass];
            GUI.Label(new Rect(pad, y, W - pad * 2, lh * 1.4f), $"{cls.desc}　HP {cls.maxHp}・MP {cls.maxMp}\n技能（選招手勢→放招動作）", small); y += lh * 1.9f;

            GUI.Label(new Rect(pad, y, W, lh), $"選 3 個技能（{myLoadout.Count}/3）", label); y += lh;
            float sw = (W - pad * 3) / 2f, sh = lh * 2.1f;
            for (int i = 0; i < cls.skills.Length; i++)
            {
                var s = Skills.All[cls.skills[i]];
                bool on = myLoadout.Contains(s.id);
                GUI.color = on ? s.color : new Color(0.75f, 0.75f, 0.75f);
                var r = new Rect(pad + (i % 2) * (sw + pad), y + (i / 2) * (sh + pad * 0.5f), sw, sh);
                string eff = s.type == SkillType.Self ? (s.self == SelfKind.Heal ? $"回復{s.heal}" : "防禦") : $"傷害{s.damage}{(s.multi > 1 ? $"×{s.multi}" : "")}";
                if (iconButton == null) iconButton = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft };
                iconButton.padding.left = Mathf.RoundToInt(sh * 0.95f);
                if (GUI.Button(r, $"{(on ? "✔ " : "")}{s.name}（{HandGesture.ShapeName(Skills.GestureOf(s.id))}→{HandGesture.StyleName(HandGesture.ReleaseOf(s))}）\nMP{s.cost}・蓄力{s.charge:0.#}s・{eff}・{s.RangeText}", iconButton))
                {
                    if (on) myLoadout.Remove(s.id);
                    else { if (myLoadout.Count >= 3) myLoadout.RemoveAt(0); myLoadout.Add(s.id); }
                }
                var keep = GUI.color;
                GUI.color = on ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                GUI.DrawTexture(new Rect(r.x + sh * 0.06f, r.y + sh * 0.06f, sh * 0.88f, sh * 0.88f), SkillIcons.Get(s.id));
                GUI.color = keep;
            }
            GUI.color = Color.white;
            y += ((cls.skills.Length + 1) / 2) * (sh + pad * 0.5f) + pad * 0.5f;

            float ew = (W - pad * 7) / 6f;
            if (!NetMode) GUI.Label(new Rect(pad, y, W, lh), "敵人職業", label);
            else GUI.Label(new Rect(pad, y, W, lh), remoteReady ? $"對手：{(Skills.Classes.TryGetValue(remoteClass ?? "", out var rc) ? rc.name : "?")}（已準備）" : "對手：還沒準備好", label);
            y += lh;
            for (int i = 0; i < 6 && !NetMode; i++)
            {
                string id = i == 0 ? "random" : i == 5 ? "dummy" : Skills.ClassOrder[i - 1];
                string nm = i == 0 ? "隨機" : i == 5 ? "木頭人" : Skills.Classes[id].name;
                GUI.color = id == enemyClass ? Color.yellow : Color.white;
                if (GUI.Button(new Rect(pad + i * (ew + pad), y, ew, bh), nm, button)) enemyClass = id;
            }
            GUI.color = Color.white; y += bh + pad;

            // 語音詠唱：每個技能錄 2 次咒語（任何語言、任何說法都可以，比對的是你自己的聲音）
            if (GUI.Button(new Rect(pad, y, ew * 1.4f, bh), voiceOn ? "🎤 語音:開" : "🎤 語音:關", button))
            { voiceOn = !voiceOn; PlayerPrefs.SetInt("sd_voice_on", voiceOn ? 1 : 0); }
            float vw = (W - pad * 5 - ew * 1.4f) / 3f;
            if (UseSpeech) GUI.Label(new Rect(pad * 2 + ew * 1.4f, y, W - pad * 3 - ew * 1.4f, bh), "戰鬥中直接唸技能名稱就能詠唱（手機內建語音辨識，不用錄音）", small);
            for (int i = 0; i < myLoadout.Count && !UseSpeech; i++)
            {
                var s = Skills.All[myLoadout[i]];
                bool rec = recordingSkill == s.id;
                GUI.color = rec ? Color.red : voice.Ready(s.id) ? s.color : Color.white;
                GUI.enabled = voiceOn;
                if (GUI.Button(new Rect(pad * 2 + ew * 1.4f + i * (vw + pad), y, vw, bh), rec ? "● 錄音中…" : $"錄「{s.name}」{voice.Count(s.id)}/2", button))
                {
                    if (rec) recordingSkill = null;
                    else { recordingSkill = s.id; if (voice.Count(s.id) >= 2) { voice.ClearSkill(s.id); } Say($"🎤 唸出「{s.name}」的咒語（自己決定怎麼唸）", 3f); }
                }
            }
            GUI.enabled = true; GUI.color = Color.white; y += bh + pad * 0.3f;
            if (recordingSkill != null && mic.Running)
            {
                float lv = Mathf.Clamp01(mic.Spotter.Level / Mathf.Max(0.001f, mic.Spotter.StartThreshold * 3f));
                Bar(new Rect(pad, y, W - pad * 2, lh * 0.35f), lv, mic.Spotter.InSpeech ? Color.green : Color.gray, "");
                y += lh * 0.5f;
            }
            else if (voiceOn && !UseSpeech && !VoiceReady) { GUI.Label(new Rect(pad, y, W - pad * 2, lh), "錄好 3 個技能的咒語後，戰鬥中唸出來就會開始詠唱（點技能按鈕也可以）", small); y += lh; }
            if (Time.time < messageUntil) { GUI.Label(new Rect(pad, y, W - pad * 2, lh), message, label); y += lh; }
            y += pad * 0.5f;

            GUI.enabled = myLoadout.Count == 3;
            if (GUI.Button(new Rect(pad, y, W - pad * 2, bh * 1.3f), NetMode ? (localReady ? "等待對手準備…" : "⚔ 準備好了") : "⚔ 開始戰鬥", button))
            { if (NetMode) SendReady(); else StartBattle(); }
            GUI.enabled = true; y += bh * 1.3f + pad;
            if (!NetMode && GUI.Button(new Rect(pad, y, (W - pad * 3) / 2f, bh), "重畫場地", button)) RequestRedraw?.Invoke();
            if (GUI.Button(new Rect(pad * 2 + (W - pad * 3) / 2f, y, (W - pad * 3) / 2f, bh), NetMode ? "返回" : "換模式", button))
            { if (NetMode) Hide(); else RequestChangeMode?.Invoke(); }
        }

        void DrawHud()
        {
            float W = Screen.width, H = Screen.height, pad = W * 0.03f, lh = label.fontSize * 1.6f;
            float top = H - Screen.safeArea.yMax + pad;
            var me = Me; var en = En; float now = battle.now;

            // 被打中：整個畫面閃紅
            if (hitFlash > 0.01f) { GUI.color = new Color(1f, 0f, 0.05f, hitFlash * 0.45f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }

            // 致盲：畫面蓋上煙霧
            if (me.blindUntil > now) { GUI.color = new Color(0.55f, 0.55f, 0.6f, 0.92f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; GUI.Label(new Rect(0, H * 0.3f, W, lh * 2), $"煙霧中… {me.blindUntil - now:F1}s", big); }
            // 敵人法術逼近：邊框閃紅
            if (incomingFlash > 0.01f)
            {
                GUI.color = new Color(1f, 0.15f, 0.2f, incomingFlash * 0.8f);
                float t = W * 0.025f;
                GUI.DrawTexture(new Rect(0, 0, W, t), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - t, W, t), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, t, H), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(W - t, 0, t, H), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // 上方：敵人資訊
            Panel(new Rect(0, 0, W, top + lh * 3.2f));
            float y = top;
            GUI.Label(new Rect(pad, y, W * 0.6f, lh), $"{en.name}　距離 {battle.Distance:F1}m", label);
            Bar(new Rect(W * 0.6f, y + lh * 0.25f, W * 0.37f, lh * 0.5f), en.hp / en.maxHp, new Color(1f, 0.25f, 0.35f), $"{Mathf.CeilToInt(en.hp)}");
            y += lh;
            string enState = dummyMode ? $"命中 {dummyHits} 次・累計傷害 {Mathf.RoundToInt(dummyDamage)}"
                : en.charging != null ? $"⚠ 詠唱 {en.charging.name}（{Mathf.FloorToInt(battle.ChargeProgress(en) * 100)}%）" : ai.Status;
            if (en.snaredUntil > now) enState = "被定身";
            GUI.Label(new Rect(pad, y, W - pad * 2, lh), enState, label); y += lh;
            GUI.Label(new Rect(pad, y, W - pad * 2, lh), StatusText(en, now), small);

            // 敵人頭上的血條
            var hs = cam.WorldToScreenPoint(WorldFrame.FromWorld(en.head + Vector3.up * 0.3f));
            if (hs.z > 0 && en.Alive)
            {
                var r = new Rect(hs.x - W * 0.12f, H - hs.y, W * 0.24f, lh * 0.4f);
                Bar(r, en.hp / en.maxHp, new Color(1f, 0.25f, 0.35f), "");
                // 敵我距離：蓄力中的技能打得到就綠色，太遠／太近就紅色
                float dist = battle.Distance;
                string rangeNote = ""; Color dc = Color.white;
                if (me.charging != null && me.charging.type == SkillType.Projectile)
                {
                    int rs = battle.RangeState(me, me.charging);
                    dc = rs == 0 ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
                    rangeNote = rs > 0 ? "　太遠" : rs < 0 ? "　太近" : "　射程內";
                }
                GUI.color = dc;
                GUI.Label(new Rect(r.x - W * 0.1f, r.y + lh * 0.45f, r.width + W * 0.2f, lh), $"{dist:F1} m{rangeNote}", center);
                GUI.color = Color.white;
                if (en.charging != null) GUI.Label(new Rect(r.x - W * 0.1f, r.y - lh, r.width + W * 0.2f, lh), $"⚠ {en.charging.name}", center);
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
                    var s = cam.WorldToScreenPoint(WorldFrame.FromWorld(f.posMap));
                    if (s.z <= 0) continue;
                    p = new Vector2(s.x, H - s.y - age * 60f);
                }
                GUI.color = new Color(f.color.r, f.color.g, f.color.b, 1f - age / 1.2f);
                GUI.Label(new Rect(p.x - W * 0.3f, p.y - lh, W * 0.6f, lh * 1.5f), f.text, center);
            }
            GUI.color = Color.white;
            floaters.RemoveAll(f => Time.time - f.born > 1.2f);

            // 準星與蓄力
            if (me.charging != null)
            {
                float prog = battle.ChargeProgress(me);
                int rs = battle.RangeState(me, me.charging);
                string st = prog < 1f ? $"蓄力 {Mathf.FloorToInt(prog * 100)}%" :
                    (me.charging.releaseNear && rs > 0) ? "靠近才能出手" : rs < 0 ? "太近了" :
                    HandGesture.StyleHint(HandGesture.ReleaseOf(me.charging));
                GUI.color = prog < 1f ? Color.white : me.charging.color;
                GUI.Label(new Rect(0, H / 2 - lh * 2.2f, W, lh), $"{me.charging.name}　{st}", center);
                GUI.color = Color.white;
            }
            // 準星（畫面中央）：所有技能都朝這裡放；蓄力完成（或符文畫好）時用技能顏色脈動
            {
                bool ready = me.charging != null && battle.ChargeProgress(me) >= 1f;
                var cc = me.charging != null ? me.charging.color : Color.white;
                float pulse = ready ? 0.6f + 0.4f * Mathf.Sin(Time.time * 10f) : 0.75f;
                float arm = W * (ready ? 0.05f : 0.04f), th = Mathf.Max(3f, W * 0.006f), gap = W * 0.012f;
                GUI.color = new Color(0f, 0f, 0f, 0.45f);
                GUI.DrawTexture(new Rect(W / 2 - arm - 1, H / 2 - th / 2 - 1, arm - gap + 2, th + 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 + gap - 1, H / 2 - th / 2 - 1, arm - gap + 2, th + 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2 - 1, H / 2 - arm - 1, th + 2, arm - gap + 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2 - 1, H / 2 + gap - 1, th + 2, arm - gap + 2), Texture2D.whiteTexture);
                GUI.color = new Color(cc.r, cc.g, cc.b, pulse);
                GUI.DrawTexture(new Rect(W / 2 - arm, H / 2 - th / 2, arm - gap, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 + gap, H / 2 - th / 2, arm - gap, th), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2, H / 2 - arm, th, arm - gap), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2, H / 2 + gap, th, arm - gap), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W / 2 - th / 2, H / 2 - th / 2, th, th), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            DrawRuneGuide(W, H);
            Hand?.DrawGUI(small, me.charging != null ? me.charging.color : Color.white);
            if (Time.time < heardUntil) GUI.Label(new Rect(0, H * 0.24f, W, lh * 1.6f), heard, big);
            else if (voiceOn && VoiceReady && mic.Running && mic.Spotter.InSpeech) GUI.Label(new Rect(0, H * 0.25f, W, lh), "🎤 …", center);

            // 鎖定框／敵人方向提示
            if (phase == Phase.Fighting && en.Alive && !frozen)
            {
                if (enemyOnScreen)
                {
                    float t = Mathf.Max(3f, W * 0.006f);
                    GUI.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                    var r = enemyRect;
                    GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, r.width, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, r.height), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, r.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(r.x, r.yMax, Mathf.Max(r.width, W * 0.3f), lh), "🎯 鎖定", small);
                }
                else
                {
                    string arrow = enemyScreenSide < 0 ? "◀ 敵人在左邊" : "敵人在右邊 ▶";
                    GUI.color = new Color(1f, 0.4f, 0.4f);
                    GUI.Label(new Rect(0, H * 0.56f, W, lh * 1.5f), arrow + "（轉向敵人才能發射）", center);
                    GUI.color = Color.white;
                }
            }
            if (Time.time < messageUntil) GUI.Label(new Rect(0, H * 0.6f, W, lh * 1.5f), message, center);

            // 下方：我的狀態與技能
            float bottomH = lh * 2.4f + W * 0.2f;
            float by = H - (H - Screen.safeArea.yMax) - Screen.safeArea.y - bottomH;
            by = Mathf.Min(by, H * 0.76f);
            Panel(new Rect(0, by - pad * 0.5f - lh, W, H - by + pad + lh));
            bool voiceActive = voiceOn && (UseSpeech || VoiceReady);
            string voiceHint = !voiceOn || voiceActive ? "" : "　（語音：咒語還沒錄完，到選技能畫面錄）";
            GUI.Label(new Rect(pad, by - lh * 1.05f, W - pad * 2 - W * 0.22f, lh),
                (voiceActive ? (UseSpeech ? "唸技能名稱或比手勢＝詠唱" : "唸咒語或比手勢＝詠唱") : "比手勢＝詠唱") + "　蓄滿後做該技能的放招動作" + voiceHint, small);
            if (UseSpeech && voiceOn)
            {
                GUI.Label(new Rect(W - pad - W * 0.21f, by - lh * 1.05f, W * 0.21f, lh), speech.Running ? "🎤 聆聽中" : "🎤 " + speech.Status, small);
                if (!string.IsNullOrEmpty(speech.LastHeard)) GUI.Label(new Rect(pad, by - lh * 2f, W - pad * 2, lh), $"聽到：{speech.LastHeard}", small);
            }
            // 麥克風音量：說話中變綠色
            if (mic.Running)
            {
                float lv = Mathf.Clamp01(mic.Spotter.Level / Mathf.Max(0.001f, mic.Spotter.StartThreshold * 3f));
                GUI.Label(new Rect(W - pad - W * 0.21f, by - lh * 1.05f, W * 0.06f, lh), "🎤", small);
                Bar(new Rect(W - pad - W * 0.15f, by - lh * 0.75f, W * 0.15f, lh * 0.3f), lv, mic.Spotter.InSpeech ? Color.green : Color.gray, "");
            }
            Bar(new Rect(pad, by, W - pad * 2, lh * 0.5f), me.hp / me.maxHp, new Color(1f, 0.3f, 0.35f), $"HP {Mathf.CeilToInt(me.hp)}");
            Bar(new Rect(pad, by + lh * 0.6f, W - pad * 2, lh * 0.5f), me.mp / me.maxMp, new Color(0.3f, 0.6f, 1f), $"MP {Mathf.FloorToInt(me.mp)}");
            GUI.Label(new Rect(pad, by + lh * 1.15f, W - pad * 2, lh), StatusText(me, now), small);
            float sw = (W - pad * 4) / 3f, sy = by + lh * 2.1f, sh = W * 0.17f;
            for (int i = 0; i < me.loadout.Count; i++)
            {
                var s = me.loadout[i];
                var r = new Rect(pad + i * (sw + pad), sy, sw, sh);
                float cd = me.cooldownUntil.TryGetValue(s.id, out var u) ? Mathf.Max(0, u - now) : 0;
                bool charging = me.charging == s;
                var g = Skills.GestureOf(s.id);
                bool showing = Hand != null && Hand.HandVisible && Hand.Current == g;
                // 技能格（只顯示，不能點）：手勢圖示＋名稱＋MP／冷卻；手正比著這個手勢時亮起來
                GUI.color = charging ? WithAlpha(s.color, 0.35f) : showing ? new Color(1f, 1f, 1f, 0.25f) : new Color(0f, 0f, 0f, 0.35f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                bool usable = me.mp >= s.cost && cd <= 0;
                // 技能圖示（發光徽章）：詠唱中外圈脈動發光；冷卻中由上往下蓋暗；MP 不足變灰
                float isz = r.height * 0.92f;
                var ir = new Rect(r.x + r.height * 0.04f, r.y + r.height * 0.04f, isz, isz);
                if (charging)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                    GUI.color = WithAlpha(s.color, 0.35f + 0.4f * pulse);
                    float gx = isz * 0.08f;
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
                // 選招手勢小圖（圖示右下角）
                HandGesture.DrawIcon(new Rect(ir.xMax - isz * 0.36f, ir.yMax - isz * 0.36f, isz * 0.34f, isz * 0.34f), g, usable ? Color.white : new Color(0.6f, 0.6f, 0.6f));
                float tx = ir.xMax + r.height * 0.06f, tw = r.xMax - tx;
                GUI.color = usable ? Color.white : new Color(0.65f, 0.65f, 0.65f);
                GUI.Label(new Rect(tx, r.y, tw, r.height * 0.42f), s.name, label);
                string vtag = (!voiceOn ? "" : UseSpeech ? "🎤" : voice.Ready(s.id) ? "🎤" : "🎤未錄");
                GUI.Label(new Rect(tx, r.y + r.height * 0.36f, tw, r.height * 0.32f), $"{HandGesture.ShapeName(g)}→{HandGesture.StyleName(HandGesture.ReleaseOf(s))}{vtag}", small);
                GUI.Label(new Rect(tx, r.y + r.height * 0.66f, tw, r.height * 0.32f), cd > 0 ? $"冷卻 {cd:F1}s" : $"MP {s.cost}", small);
                GUI.color = Color.white;
            }

            if (frozen)
            {
                Panel(new Rect(0, H * 0.32f, W, H * 0.2f), 0.8f);
                GUI.Label(new Rect(0, H * 0.33f, W, H * 0.08f), NetMode ? "⚠ AR 追蹤中斷，暫時不能施法" : "⏸ AR 追蹤中斷，戰鬥暫停", big);
                GUI.Label(new Rect(pad, H * 0.42f, W - pad * 2, lh * 2), Tracking.Reason + (NetMode ? "\n對手仍以你最後的位置判定" : "\n恢復追蹤後自動繼續"), center);
            }

            if (phase == Phase.Over)
            {
                Panel(new Rect(0, H * 0.3f, W, H * 0.3f), 0.75f);
                GUI.Label(new Rect(0, H * 0.32f, W, H * 0.1f), me.Alive ? "🏆 勝利！" : "💀 敗北…", big);
                float bw = (W - pad * 3) / 2f;
                if (GUI.Button(new Rect(pad, H * 0.47f, bw, lh * 2f), NetMode ? (localReady ? "等待對手…" : "再來一局") : "再來一局", button))
                { if (NetMode) SendReady(); else StartBattle(); }
                if (GUI.Button(new Rect(pad * 2 + bw, H * 0.47f, bw, lh * 2f), "換職業", button)) ShowSetup();
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
            GUI.Label(new Rect(0, cy + R * 1.1f, W, label.fontSize * 1.6f), $"用食指畫「{RuneRecognizer.RuneName(rune)}」符文（大圓點是起點）", center);
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

        void Bar(Rect r, float v, Color c, string text)
        {
            GUI.color = new Color(1, 1, 1, 0.2f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(v), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(r.x + 4, r.y - r.height * 0.6f, r.width, r.height * 2f), text, small);
        }
    }
}
