using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 互動教學關（第一次玩：序章對話之後自動開始；之後在村子的訓練場可以再上一次）：一步一步教，做對了才進下一步。
    ///   0 站好校正 → 1 走到地上的光圈（兩個）→ 2 橫掃來時蹲下 → 3 火球飛來時側跨 → 4 震地環到腳邊時跳起來
    ///   → 5 唸技能名稱詠唱 → 6 做放招動作把技能放出去 → 7 打中木頭人 3 次 → 完成（第一次完成送金幣與經驗）。
    /// 初心者（還沒有技能）：前五步一樣，接著 8 走到木頭人前面 → 9 用普攻「木棍敲擊」（手掌在鏡頭前劃一下）打中 3 次。
    /// 米菈的投石課（炎之里第一個任務，故事 1-1 之前）：木頭人站在遠處 → 5 唸「投石」→ 6 握拳再張開丟出去 → 10 站在遠處用投石打中 3 次。
    /// 攻擊由「教官木頭人」放出（不會真的扣血；被打到就重來這一步）。每一步都可以「略過」。
    /// </summary>
    public partial class SoloBattle
    {
        bool tutorial;                         // 正在上教學關（結算後也保持，用來畫教學的結算按鈕）
        int tutStep, tutRing, tutHitsAt, tutDummyAt, tutFails;
        float tutStepAt, tutFireAt = -1f, tutPassAt = -1f;
        bool tutFired, tutWasCharging;
        Vector3 tutTarget; GameObject tutRingGo, tutPillarGo;
        string tutMsg; float tutMsgUntil;

        static readonly int[] PlanFull = { 0, 1, 2, 3, 4, 5, 6, 7 };
        static readonly int[] PlanNovice = { 0, 1, 2, 3, 4, 8, 9 };
        static readonly int[] PlanStone = { 0, 5, 6, 10 };
        int[] tutPlan = PlanFull;
        /// <summary>正在上米菈的投石課（不是一般教學）</summary>
        bool stoneLesson;
        int TutSteps => tutPlan.Length;
        /// <summary>這一步要教什麼（TutTitles 的索引）</summary>
        int TutKind => tutPlan[Mathf.Clamp(tutStep, 0, tutPlan.Length - 1)];
        static readonly string[] TutTitles = { "站好校正", "走到光圈", "蹲下躲橫掃", "側跨閃火球", "跳過震地環", "唸技能名稱", "放出技能", "打中木頭人", "走到木頭人前面", "木棍敲擊", "遠距離投石" };
        /// <summary>第一次玩（新角色）要不要強制播序章＋教學（之後在訓練場可以再上）</summary>
        public const bool ForceIntro = true;
        /// <summary>還沒看過序章＋教學、而且要強制播</summary>
        public static bool IntroPending => ForceIntro && !TutorialDone;
        public static bool TutorialDone { get { try { return PlayerPrefs.GetInt(CharacterSlots.Key("sd_tutorial_done"), 0) == 1; } catch { return false; } } }

        void StartTutorial() => StartTutorial(false);
        void StartTutorial(bool stone)
        {
            SaveChoices();
            stoneLesson = stone;
            tutPlan = stone ? PlanStone : myClass == Skills.Novice ? PlanNovice : PlanFull;
            EndBattle();
            storyStage = null; dungeon = null;
            tutorial = true;
            EnsureLoadout();
            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            Advanced.ApplyStats(me, Progress.Char(myClass).Adv);
            me.maxHp = 9999; me.hp = me.maxHp;   // 教學不會倒（被打到只是重來這一步）
            dummyMode = true; dummyDamage = 0f; dummyHits = 0;
            var en = new Fighter("教官木頭人", DummyClass, new List<string>(), false);
            float reach = area.ReachInside(area.Origin.forward, 0.6f);
            en.head = new Vector3(0f, 1.6f, stone ? Mathf.Clamp(reach, 3f, 6f) : Mathf.Clamp(reach, 1.5f, 3f));   // 投石課：木頭人站遠一點（木棍敲不到）
            SyncPlayer(me);
            battle = new Battle(me, en);
            battle.OnEvent += OnBattleEvent; battle.OnSlashLine += ShowSlashLine; battle.OnShotWarn += ShowShotLine;
            ai = null; bossAi = null; bossMode = false; endless = false; coop = false;
            enemyClassUsed = "dummy";
            enemyRig = CharacterRig.Create(CharacterRig.DummyId);
            lastEnemyFeet = en.Feet; lastEnemyHp = en.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            tutStep = 0; tutRing = 0; tutFails = 0; BeginTutStep(0);
            phase = Phase.Fighting; resultShown = false;
            Say(stone ? "🪨 米菈的投石課：照著畫面上方的指示做" : "🎓 教學開始：照著畫面上方的指示一步一步做", 3f);
        }

        void ClearTutorial()
        {
            if (tutRingGo) Destroy(tutRingGo);
            if (tutPillarGo) Destroy(tutPillarGo);
            tutorial = false;
        }

        void BeginTutStep(int s)
        {
            tutStep = s; tutStepAt = battle.now; tutFired = false; tutFireAt = -1f; tutPassAt = -1f; tutFails = 0;
            tutHitsAt = hitsTaken; tutDummyAt = dummyHits; tutWasCharging = false;
            int k = tutPlan[Mathf.Clamp(s, 0, tutPlan.Length - 1)];
            if (k == 1) { tutRing = 0; PlaceTutRing(); }
            else { if (tutRingGo) tutRingGo.SetActive(false); if (tutPillarGo) tutPillarGo.SetActive(false); }
            if (k >= 2 && k <= 4) tutFireAt = battle.now + 2.5f;   // 給玩家 2.5 秒準備
        }

        void TutPass()
        {
            if (tutPassAt > 0f) return;
            tutPassAt = battle.now;
            Sfx.Play("charged", 1f); Haptics.Play(Haptics.Kind.Success);
            floaters.Add(new Floater { text = "✔ 很好！", color = new Color(0.5f, 1f, 0.6f), born = Time.time, screen = true });
        }

        void TutFail(string why)
        {
            tutFails++;
            tutMsg = why; tutMsgUntil = Time.time + 2.5f;
            Sfx.Play("fail", 0.8f);
            BeginTutStepKeepFails(tutStep);
        }
        void BeginTutStepKeepFails(int s) { int f = tutFails; BeginTutStep(s); tutFails = f; }

        /// <summary>每幀（戰鬥中）；教學完成回傳 true</summary>
        bool UpdateTutorial()
        {
            if (!tutorial || battle == null) return false;
            var me = Me; var en = En; float now = battle.now;
            me.hp = me.maxHp;
            if (tutPassAt > 0f)
            {
                if (now - tutPassAt < 1.2f) return false;
                if (tutStep + 1 >= TutSteps) { FinishTutorial(); return true; }
                BeginTutStep(tutStep + 1);
                return false;
            }
            int kind = TutKind;
            switch (kind)
            {
                case 0: if (now > CalibTime + 0.5f) TutPass(); break;
                case 1:
                    if (tutRingGo && tutRingGo.activeSelf)
                    {
                        tutRingGo.transform.position = WorldFrame.FromWorld(tutTarget + Vector3.up * 0.01f);
                        float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 5f);
                        tutRingGo.transform.localScale = new Vector3(0.9f * pulse, 0.004f, 0.9f * pulse);
                    }
                    if (Battle.FlatDistance(me.Feet, tutTarget) < 0.5f)
                    {
                        Sfx.Play("coin", 0.9f);
                        if (++tutRing >= 2) TutPass(); else PlaceTutRing();
                    }
                    break;
                case 2: case 3: case 4:
                    if (!tutFired && now >= tutFireAt) { TutFire(kind); tutFired = true; tutFireAt = now; }
                    if (tutFired)
                    {
                        if (hitsTaken > tutHitsAt) { TutFail(kind == 2 ? "被橫掃打到了：看到地上的橘線就蹲低" : kind == 3 ? "被火球打到了：往旁邊跨一大步" : "被震地環打到了：環到腳邊時往上跳"); break; }
                        bool gone = kind == 4 ? battle.shockwaves.Count == 0 : !battle.projectiles.Exists(p => p.owner == en);
                        if (gone && now - tutFireAt > 0.6f) TutPass();
                    }
                    break;
                case 5: if (me.charging != null) TutPass(); break;
                case 6:
                    if (me.charging != null) tutWasCharging = true;
                    else if (tutWasCharging) { if (battle.projectiles.Exists(p => p.owner == me) || dummyHits > tutDummyAt || me.armed != null) TutPass(); else tutWasCharging = false; }
                    if (dummyHits > tutDummyAt) TutPass();
                    break;
                case 7: case 9: if (dummyHits - tutDummyAt >= 3) TutPass(); break;
                case 8: if (Battle.FlatDistance(me.Feet, en.Feet) < MeleeNear) TutPass(); break;
                case 10:
                    // 要站遠遠的用投石打中：走過去用木棍敲不算
                    if (dummyHits > tutDummyAt)
                    {
                        if (Battle.FlatDistance(me.Feet, en.Feet) < 2f) { tutDummyAt = dummyHits; tutMsg = "這次要站遠一點，用投石丟中它"; tutMsgUntil = Time.time + 2.5f; }
                        else if (dummyHits - tutDummyAt >= 3) TutPass();
                    }
                    break;
            }
            return false;
        }

        /// <summary>教官木頭人放出攻擊（朝你）：2 橫掃、3 火球、4 震地環</summary>
        void TutFire(int step)
        {
            var me = Me; var en = En;
            en.forward = Fighter.Flat(me.Feet - en.Feet);
            if (step == 4) { battle.SpawnShockwave(en, Skills.All["x_quake"]); return; }
            var sk = Skills.All[step == 2 ? "b_sweep" : "m_fireball"];
            en.charging = sk; en.chargeStart = battle.now - 100f;
            var hand = en.head + en.forward * 0.25f;
            int before = battle.projectiles.Count;
            battle.TryRelease(en, (me.Chest - hand).normalized, me.Feet, out _);
            if (step == 3) for (int j = before; j < battle.projectiles.Count; j++) battle.projectiles[j].speedMul = 0.7f;   // 教學：火球慢一點
            if (enemyRig) enemyRig.PlayCast();
            Sfx.Play(step == 2 ? "blade_warn" : "aim_warn", 0.8f);
        }

        /// <summary>光圈：場地內離你 1.2～2.5m（離邊界 0.5m 以上）</summary>
        void PlaceTutRing()
        {
            var me = Me; var poly = area.Polygon;
            Vector3 best = me.Feet + Fighter.Flat(me.forward) * 1.2f; best.y = 0f;
            if (poly != null && poly.Count >= 3)
            {
                var c = Vector3.zero; foreach (var q in poly) c += q; c /= poly.Count;
                for (int tries = 0; tries < 60; tries++)
                {
                    float a = Random.value * Mathf.PI * 2f, r = Random.Range(0f, 4f);
                    var s = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (!area.Inside(s) || area.DistanceToEdge(s) < 0.5f) continue;
                    var w = WorldFrame.ToWorld(s); w.y = 0f;
                    float d = Battle.FlatDistance(w, me.Feet);
                    if (d >= 1.2f && d <= 2.5f) { best = w; break; }
                }
            }
            tutTarget = best;
            if (!tutRingGo)
            {
                tutRingGo = Prim(PrimitiveType.Cylinder, new Color(0.35f, 0.95f, 1f, 0.5f), true);
                tutPillarGo = Prim(PrimitiveType.Cylinder, new Color(0.35f, 0.95f, 1f, 0.2f), true);
                tutPillarGo.transform.localScale = new Vector3(0.1f, 1.2f, 0.1f);
            }
            tutRingGo.SetActive(true); tutPillarGo.SetActive(true);
            tutPillarGo.transform.position = WorldFrame.FromWorld(tutTarget + Vector3.up * 1.2f);
            SpawnFade.Play(tutRingGo, 0.3f);
        }

        /// <summary>走到木頭人前面多近才算（木棍敲擊從頭部算起約 1.6m 搆得到）</summary>
        const float MeleeNear = 1.3f;

        void FinishTutorial()
        {
            phase = Phase.Over; resultShown = true;
            if (tutRingGo) tutRingGo.SetActive(false);
            if (tutPillarGo) tutPillarGo.SetActive(false);
            SaveExercise();
            if (stoneLesson)
            {
                int lvl0 = Progress.Char(myClass).level, lxp = StoneLessonDone();
                BuildVictory("投石課完成！", "tutorial", 180f, lxp, Progress.Char(myClass).level - lvl0, (lxp > 0 ? $"第一次完成：經驗 {lxp}、💰{LessonGold}。" : "") + "學會「投石」：唸「投石」，握拳再張開就會丟出去（遠距攻擊）");
                Sfx.Play("win"); Haptics.Play(Haptics.Kind.Success);
                return;
            }
            bool first = !TutorialDone;
            int xp = 0, ups = 0;
            if (first)
            {
                try { PlayerPrefs.SetInt(CharacterSlots.Key("sd_tutorial_done"), 1); PlayerPrefs.Save(); } catch { }
                Economy.Gold += 100; lootGoldLog += 100;
                var p = Progress.Char(myClass); xp = 100; ups = p.AddXp(xp);
                int xb = p.xp; ups += TopUpLevel(p, TutorialLevel); xp += p.xp - xb;   // 初心者：教學結束至少 5 級
                Progress.SaveChar(p);
                prof = p;
            }
            BuildVictory("教學完成！", "tutorial", 180f, xp, ups, first ? $"第一次完成：經驗 {xp}、💰100（升到 Lv{Progress.Char(myClass).level}，拿到能力點，進村子後導覽會教你怎麼加）" : "之後可以在村子的訓練場再上一次教學");
            Sfx.Play("win"); Haptics.Play(Haptics.Kind.Success);
        }

        /// <summary>教學中的 HUD：蓋在上方敵人卡片的位置（第幾步、要做什麼、進度點、略過）</summary>
        void DrawTutorialHud(Rect topR, float W, float H, float lh, int fs, int fsS, int fsL, float pillW)
        {
            var col = new Color(0.35f, 0.95f, 1f);
            UiKit.Card(topR, 0.92f, col, 0.8f);
            float gap = W * 0.015f, ix = topR.x + gap, iw = topR.width - gap * 2, y = topR.y + gap * 0.5f;
            UiKit.Text(new Rect(ix, y, iw * 0.7f, lh), $"{(stoneLesson ? "🪨 投石課" : "🎓 教學")} {tutStep + 1}/{TutSteps}：{TutTitles[TutKind]}", fs, Color.white, TextAnchor.MiddleLeft, true);
            // 進度點
            float dot = lh * 0.32f;
            for (int i = 0; i < TutSteps; i++)
                UiKit.Gem(new Rect(ix + iw * 0.72f + i * dot * 1.5f, y + lh * 0.35f, dot, dot), i < tutStep ? new Color(0.5f, 1f, 0.6f) : i == tutStep ? col : new Color(0.4f, 0.45f, 0.55f));
            y += lh;
            UiKit.Text(new Rect(ix, y, iw * 0.78f, lh * 1.8f), TutInstruction(), fsS, new Color(1f, 0.95f, 0.75f), TextAnchor.UpperLeft);
            // 略過（做不到的時候，例如手機不支援某個偵測）
            if (tutPassAt < 0f && tutStep > 0 && UiKit.Button(new Rect(ix + iw * 0.8f, y + lh * 0.2f, iw * 0.2f, lh * 1.3f), tutFails >= 2 ? "略過 ⏭" : "略過", fsS))
                TutPass();
            if (Time.time < tutMsgUntil) UiKit.Pill(H * 0.55f, "💥 " + tutMsg + "　再試一次！", fs, new Color(1f, 0.5f, 0.45f), pillW);
            // 攻擊倒數
            if (TutKind >= 2 && TutKind <= 4 && !tutFired && tutPassAt < 0f)
                UiKit.Text(new Rect(0, H * 0.36f, W, H * 0.12f), $"{Mathf.CeilToInt(tutFireAt - battle.now)}", fsL * 3, col, TextAnchor.MiddleCenter, true);
            if (TutKind == 1 && tutRingGo && tutRingGo.activeSelf)
                DrawEdgeArrow(tutTarget + Vector3.up * 0.3f, $"⭕ {Battle.FlatDistance(Me.Feet, tutTarget):F1}m", col, W, H, lh, fsS);
            if (TutKind >= 8 && tutPassAt < 0f)
                DrawEdgeArrow(En.Feet + Vector3.up * 1f, $"🪵 {Battle.FlatDistance(Me.Feet, En.Feet):F1}m", col, W, H, lh, fsS);
            if (TutKind == 9 && tutPassAt < 0f) DrawSwipeDemo(W, H, fs, col);
        }

        /// <summary>示範動畫：手掌從左往右劃過畫面（後面拖著殘影），每 1.6 秒一次</summary>
        void DrawSwipeDemo(float W, float H, int fs, Color col)
        {
            float t = Time.time % 1.6f, k = Mathf.Clamp01(t / 0.7f);
            float e = 1f - (1f - k) * (1f - k);   // 先快後慢
            float y = H * 0.62f, x0 = W * 0.2f, x1 = W * 0.8f, x = Mathf.Lerp(x0, x1, e);
            float a = t < 0.7f ? 1f : Mathf.Clamp01(1f - (t - 0.7f) / 0.5f);
            // 殘影：一串越來越淡的點
            for (int i = 0; i < 14; i++)
            {
                float px = Mathf.Lerp(x0, x, i / 13f), sz = fs * (0.15f + 0.35f * i / 13f);
                GUI.color = new Color(1f, 0.8f, 0.45f, a * (0.15f + 0.6f * i / 13f));
                GUI.DrawTexture(new Rect(px - sz / 2f, y - sz / 2f + Mathf.Sin(i * 0.4f) * fs * 0.15f, sz, sz), Texture2D.whiteTexture);
            }
            GUI.color = new Color(1f, 1f, 1f, a);
            UiKit.Text(new Rect(x - fs * 1.6f, y - fs * 2.2f, fs * 3.2f, fs * 3.2f), "✋", fs * 2, Color.white, TextAnchor.MiddleCenter);
            GUI.color = Color.white;
            UiKit.Text(new Rect(0, y + fs * 1.4f, W, fs * 1.5f), "像這樣：張開手掌，在鏡頭前快速橫劃一下", fs, new Color(1f, 0.9f, 0.6f, Mathf.Max(0.6f, a)), TextAnchor.MiddleCenter, true);
        }

        string TutInstruction()
        {
            string names = string.Join("、", myLoadout.ConvertAll(id => "「" + Skills.All[id].name + "」"));
            float dist = Battle.FlatDistance(Me.Feet, En.Feet);
            switch (TutKind)
            {
                case 0: return "站直、手機拿在眼前對著前方，別動（量你的身高，之後才知道你蹲下、跳起來）";
                case 1: return $"走到地上發光的圓圈裡（{tutRing + 1}/2）。戰鬥中走位可以躲開攻擊";
                case 2: return "木頭人會放出胸口高度的「橫掃」（地上會出現橘色的線）→ 蹲低讓它從頭上飛過";
                case 3: return "木頭人會丟出火球（地上有紅線）→ 往旁邊跨一大步閃開";
                case 4: return "地上會擴散出一圈震地環 → 環快到腳邊時往上跳";
                case 5: return stoneLesson ? "對著手機唸「投石」開始蓄力（技能名稱）" : $"對著手機唸出技能名稱開始詠唱：{names}";
                case 6: return $"詠唱中：{(Me.charging != null ? HandGesture.ReleaseHint(Me.charging) : "先唸技能名稱")}（把技能放出去）";
                case 8: return $"初心者的普攻是近戰：先走到木頭人前面（{MeleeNear:0.#} 公尺內，現在 {dist:F1}m）";
                case 9: return $"普攻「木棍敲擊」：手掌張開，在鏡頭前快速橫劃一下 → 木棍馬上敲下去。畫面上的手掌變紅＝集氣完成可以敲（約 2 秒一次）。打中 3 次（{Mathf.Max(0, dummyHits - tutDummyAt)}/3）"
                        + (dist > MeleeNear + 0.3f ? "　⚠ 太遠了，走近一點" : "");
                case 10: return $"站在遠處（2 公尺外，現在 {dist:F1}m），唸「投石」→ 握拳再張開，用石頭打中木頭人 3 次（{Mathf.Max(0, dummyHits - tutDummyAt)}/3）";
                default: return $"用普攻（手勢）或技能打中木頭人 3 次（{Mathf.Max(0, dummyHits - tutDummyAt)}/3）";
            }
        }

        /// <summary>教學結算：勝利窗格關掉之後的按鈕</summary>
        void DrawTutorialDone(float W, float pad, float top, float botY, float gap, float lh, int fs)
        {
            var orr = new Rect(pad, top + (botY - top) * .3f, W - pad * 2, (botY - top) * .3f);
            UiKit.Result(orr, true);
            UiKit.Text(new Rect(orr.x, orr.y + gap, orr.width, lh * 1.6f), stoneLesson ? "🪨 學會投石了！" : "🎓 教學完成！準備好冒險了", Mathf.RoundToInt(fs * 1.3f), Color.white, TextAnchor.MiddleCenter, true);
            float bw = (orr.width - gap * 4) / 3f, bh = lh * 1.8f, oby = orr.yMax - gap - bh;
            if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), "🏘 進入村子", fs, true, new Color(1f, 0.8f, 0.4f))) AfterTutorial();
            if (UiKit.Button(new Rect(orr.x + gap * 2 + bw, oby, bw, bh), "再練一次", fs, false, new Color(0.35f, 0.95f, 1f))) StartTutorial(stoneLesson);
            if (UiKit.Button(new Rect(orr.x + gap * 3 + bw * 2, oby, bw, bh), villageReturn.HasValue ? "🏘 回訓練場" : "回選單", fs)) { if (villageReturn.HasValue) GoVillage(); else { prologueRun = false; enemyClass = "story"; ShowSetup(); } }
        }
    }
}
