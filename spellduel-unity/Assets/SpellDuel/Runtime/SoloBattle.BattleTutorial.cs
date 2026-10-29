using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 新手關的戰鬥中教學（只有第一次打、單人）：
    ///   1-1：第一批熔岩小鬼出現 → 進到攻擊區就全部定住 → 走到第一隻旁邊 → 照 3 步驟用普攻（木棍敲擊）打倒
    ///        → 第二隻：用衝刺（比 5 → 握拳）衝過去，再一樣用普攻打倒 → 第三隻放開讓牠攻擊：教「紅色預警範圍」→ 走出紅圈閃過一次 → 自由戰鬥。
    ///   1-2：自由戰鬥；第一隻岩石（熔岩）衝鋒獸進場 → 所有敵人定住 → 走到牠旁邊 → 照 3 步驟放「木棍揮擊」打倒 → 自由戰鬥。
    /// 1-1～1-3（初心者）：熔岩小鬼的血量＝普攻一下就倒；1-2 的衝鋒獸＝木棍揮擊一發就倒（普攻要兩下）。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>0＝沒有；11＝1-1 教學；12＝1-2 衝鋒獸教學</summary>
        int btStage, btStep, btHitsAt, btSpawned, btDodged;
        Fighter btTarget;
        float btStepAt, btPrevWindup, btMsgUntil, btDashFrom; bool btDashNeed;
        /// <summary>衝刺教學：一直不用衝刺、走過去也行（這麼久之後）</summary>
        const float BtDashWalkOk = 25f;
        string btMsg;

        static bool BtFlag(string id) { try { return PlayerPrefs.GetInt(CharacterSlots.Key("sd_bt" + id), 0) == 1; } catch { return true; } }
        static void BtMark(string id) { try { PlayerPrefs.SetInt(CharacterSlots.Key("sd_bt" + id), 1); PlayerPrefs.Save(); } catch { } }

        // 1-1 的步驟
        const int Bt11Wait = 0, Bt11Approach1 = 1, Bt11Hit1 = 2, Bt11Approach2 = 3, Bt11Hit2 = 4, Bt11Dodge = 5, BtFree = 9;
        // 1-2 的步驟
        const int Bt12Wait = 0, Bt12Approach = 1, Bt12Swing = 2;
        /// <summary>走到敵人旁邊多近才算進入攻擊範圍（普攻木棍約 1m＋手臂；木棍揮擊 1.8m）</summary>
        const float BtNearBasic = 1.4f, BtNearSwing = 1.6f;

        float StickDamage => Mathf.Max(1f, (Me?.basic ?? Skills.All["na_stick"]).damage * (Me != null ? Me.atkMul : 1f));
        float SwingDamage
        {
            get
            {
                var sk = Me?.loadout.Find(s => s.id == "nv_swing") ?? Skills.All["nv_swing"];
                return Mathf.Max(1f, sk.damage * (Me != null ? Me.atkMul : 1f));
            }
        }

        /// <summary>故事關卡開打時（ApplyNoviceRoute 之後）：新手關的血量與戰鬥教學</summary>
        void SetupNoviceBattle(StoryStage st)
        {
            btStage = 0; btStep = 0; btTarget = null; btSpawned = 0; btDodged = 0;
            if (battle == null || st == null) return;
            SetupPotionLesson(st);   // 1-2：血量第一次低於 1/3 時教喝藥水
            // 衝刺（SoloBattle.Dash.cs）：還沒衝過的玩家（1-1 教學以外）開打時提示一次
            if (!NetMode && !coop && dungeon == null && st.id != "1-1" && !BtFlag("dash")) Say("💨 新動作：手掌比 5 → 馬上握拳＝往前衝刺（握著一直衝，鬆開就停；唸技能時不能衝）", 7f);
            var p = Progress.Char(myClass);
            bool early = (st.id == "1-1" || st.id == "1-2" || st.id == "1-3") && difficulty == 0;   // 只有簡單難度是新手設計
            if (early && p.cls == Skills.Novice)
            {
                string sid = st.id;
                // 熔岩小鬼：普攻一下就倒；1-2 的衝鋒獸：木棍揮擊一發就倒（普攻打兩下）
                battle.minionHpOverride = k => k == 0 ? Mathf.Floor(StickDamage * 0.9f)
                    : k == 2 && sid == "1-2" ? Mathf.Max(StickDamage + 1f, Mathf.Floor(SwingDamage * 0.95f)) : 0f;
            }
            if (NetMode || coop || p.cls != Skills.Novice || difficulty != 0) return;
            if (st.id == "1-1" && !BtFlag("11"))
            {
                btStage = 11; BeginBt(Bt11Wait);
                battle.kindOverride = () => { btSpawned++; return btStep < BtFree ? 0 : -1; };   // 教學中都是熔岩小鬼
                battle.minionHold = BtHold;
            }
            else if (st.id == "1-2" && !BtFlag("12") && myLoadout.Contains("nv_swing"))
            {
                btStage = 12; BeginBt(Bt12Wait);
                battle.kindOverride = () => btSpawned++ == 1 ? 2 : -1;   // 第二隻一定是衝鋒獸
                battle.minionHold = BtHold;
            }
        }

        void BeginBt(int step)
        {
            btStep = step; btStepAt = battle != null ? battle.now : 0f; btHitsAt = hitsTaken; btPrevWindup = 0f; btDashFrom = dashDistance;
            btDashNeed = btTarget == null || Me == null || Battle.FlatDistance(Me.Feet, btTarget.Feet) > 2f;   // 第二隻本來就在旁邊：不用衝
            if (step == BtFree && battle != null)
            {
                battle.minionHold = null; battle.holdSpawns = false; battle.kindOverride = null;
                BtMark(btStage == 11 ? "11" : "12");
                Say(btStage == 11 ? "⚔ 教學完成！接下來自由戰鬥，把小怪全部打倒吧" : "⚔ 很好！強敵用技能對付。繼續戰鬥到勝利吧", 4f);
                Sfx.Play("levelup", 0.7f); Haptics.Play(Haptics.Kind.Success);
            }
        }

        /// <summary>小怪要不要定住：0 照常、1 定住、2 只走位</summary>
        int BtHold(Fighter m)
        {
            if (PotLessonFreeze) return 1;   // 喝藥水教學：全部定住（SoloBattle.PotionLesson.cs）
            if (btStage == 11)
            {
                if (btStep == Bt11Dodge && m == btTarget) return battle.MinionInZone(m) ? 0 : 2;   // 閃避教學：只放這一隻出手
                return battle.MinionInZone(m) ? 1 : 2;   // 先走進來，進了攻擊區就定住
            }
            if (btStage == 12 && (btStep == Bt12Approach || btStep == Bt12Swing)) return 1;   // 衝鋒獸教學：全部定住
            return 0;
        }

        Fighter BtFirstAlive(int kind = -1)
        {
            foreach (var m in battle.minions) if (m.Alive && (kind < 0 || m.minionKind == kind)) return m;
            return null;
        }

        void BtPass(string text)
        {
            floaters.Add(new Floater { text = "✔ " + text, color = new Color(0.5f, 1f, 0.6f), born = Time.time, screen = true });
            Sfx.Play("charged", 1f); Haptics.Play(Haptics.Kind.Success);
        }

        /// <summary>每幀（戰鬥中）</summary>
        void UpdateBattleTut()
        {
            if (btStage == 0 || btStep == BtFree || battle == null || Me == null) return;
            if (battle.stageCleared || !Me.Alive) return;
            Me.hp = Mathf.Max(Me.hp, Me.maxHp * 0.3f);   // 教學中不會倒
            float now = battle.now;
            if (btStage == 11)
            {
                switch (btStep)
                {
                    case Bt11Wait:
                        btTarget = BtFirstAlive();
                        if (btTarget != null)
                        {
                            battle.holdSpawns = true;
                            Say("⏸ 敵人出現了！先別急——教你怎麼打", 3f);
                            Sfx.Play("warn", 0.6f); BeginBt(Bt11Approach1);
                        }
                        break;
                    case Bt11Approach1: case Bt11Approach2:
                        if (btTarget == null || !btTarget.Alive) { btTarget = BtNextTarget(); break; }
                        if (battle.MinionInZone(btTarget) && Battle.FlatDistance(Me.Feet, btTarget.Feet) <= BtNearBasic)
                        {
                            // 第二隻教衝刺：要衝過去才算（一直不衝、走過去，等久了也放行）
                            bool dashed = dashDistance - btDashFrom >= 0.3f;
                            if (btStep == Bt11Approach2 && btDashNeed && !dashed && now - btStepAt < BtDashWalkOk && !dashing) break;
                            BtPass(btStep == Bt11Approach2 && dashed ? "衝刺成功！" : "進入攻擊範圍！"); StopDash(); BeginBt(btStep + 1);
                        }
                        break;
                    case Bt11Hit1: case Bt11Hit2:
                        if (btTarget == null || !btTarget.Alive)
                        {
                            BtPass(btStep == Bt11Hit1 ? "打倒了！再一隻" : "打倒了！");
                            btTarget = BtNextTarget();
                            BeginBt(btStep == Bt11Hit1 ? Bt11Approach2 : Bt11Dodge);
                        }
                        break;
                    case Bt11Dodge:
                        if (btTarget == null || !btTarget.Alive) { btTarget = BtNextTarget(); btPrevWindup = 0f; break; }
                        // 這一隻出手了（預備結束）：被打到＝再一次；沒被打到＝閃過
                        if (btPrevWindup > 0f && btTarget.windupUntil <= 0f)
                        {
                            if (hitsTaken > btHitsAt) { btMsg = "被打到了：紅色扇形出現時，走到扇形外面（側邊或後面）"; btMsgUntil = Time.time + 3f; Sfx.Play("fail", 0.7f); btHitsAt = hitsTaken; }
                            else if (++btDodged >= 1) { BtPass("閃過了！"); BeginBt(BtFree); return; }
                        }
                        btPrevWindup = btTarget.windupUntil;
                        break;
                }
                return;
            }
            // 1-2：衝鋒獸
            switch (btStep)
            {
                case Bt12Wait:
                {
                    var c = BtFirstAlive(2);
                    if (c != null && battle.MinionInZone(c))
                    {
                        btTarget = c; battle.holdSpawns = true;
                        Say("⚠ 強敵出現：熔岩衝鋒獸！敵人全部暫停——教你用技能對付牠", 4f);
                        Sfx.Play("warn", 0.8f); Haptics.Play(Haptics.Kind.Heavy);
                        BeginBt(Bt12Approach);
                    }
                    break;
                }
                case Bt12Approach:
                    if (btTarget == null || !btTarget.Alive) { BeginBt(BtFree); break; }
                    if (Battle.FlatDistance(Me.Feet, btTarget.Feet) <= BtNearSwing) { BtPass("進入攻擊範圍！"); BeginBt(Bt12Swing); }
                    break;
                case Bt12Swing:
                    if (btTarget == null || !btTarget.Alive) { BtPass("一招打倒強敵！"); BeginBt(BtFree); }
                    break;
            }
        }

        /// <summary>下一隻教學目標：場上活著的第一隻；沒有就放一隻出來</summary>
        Fighter BtNextTarget()
        {
            var t = BtFirstAlive();
            battle.holdSpawns = t != null;
            return t;
        }

        /// <summary>教學的 HUD（蓋在上方的敵人卡片上）</summary>
        void DrawBattleTutHud(Rect topR, float W, float H, float lh, int fs, int fsS, float pillW)
        {
            if (btStage == 0 || btStep == BtFree || battle == null || Me == null || phase != Phase.Fighting) return;
            var col = new Color(0.35f, 0.95f, 1f);
            string title, text; string[] steps = null; bool[] done = null;
            float dist = btTarget != null ? Battle.FlatDistance(Me.Feet, btTarget.Feet) : 0f;
            var basic = Me.basic ?? Skills.BasicOf(myClass);
            if (btStage == 11)
            {
                switch (btStep)
                {
                    case Bt11Wait: title = "⏳ 敵人要來了"; text = "小怪會從場地外面走進來，注意四周。"; break;
                    case Bt11Approach1:
                        title = "🚶 靠近敵人（1/2）";
                        text = $"敵人暫停中。初心者的普攻是近戰：走到熔岩小鬼旁邊 {BtNearBasic:0.#} 公尺內（現在 {dist:F1}m）。"; break;
                    case Bt11Approach2:
                    {
                        title = "💨 衝刺靠近（2/2）";
                        int ds = DashGestureStage; bool moved = dashDistance - btDashFrom >= 0.3f;
                        steps = new[] { "① 面向小鬼，手掌張開比 5，伸到鏡頭前", "② 馬上握拳 → 角色往前衝（拳頭握著就一直衝）", "③ 衝到小鬼旁邊，拳頭鬆開或移開就停下" };
                        done = new[] { ds >= 1 || moved, ds == 2 || moved, false };
                        text = $"房間小也不用走過去：用衝刺一口氣衝到牠旁邊（現在 {dist:F1}m）。正在唸技能時不能衝。";
                        break;
                    }
                    case Bt11Hit1: case Bt11Hit2:
                    {
                        title = $"🪵 普攻：木棍敲擊（{(btStep == Bt11Hit1 ? 1 : 2)}/2）";
                        Me.cooldownUntil.TryGetValue(basic.id, out var cd);
                        bool hand = Hand != null && Hand.HandVisible, ready = cd <= battle.now;
                        steps = new[] { "① 張開手掌，伸到鏡頭前（畫面上出現手掌圈）", "② 等手掌圈變紅＝集氣完成（手機會震一下）", "③ 對著小鬼，手掌在鏡頭前快速橫劃一下 → 木棍敲下去" };
                        done = new[] { hand, hand && ready, false };
                        text = dist > BtNearBasic + 0.4f ? "⚠ 太遠了，再走近一點" : "照著下面 3 個步驟做：";
                        break;
                    }
                    default:
                        title = "🔴 閃避：紅色預警範圍";
                        text = "這隻小鬼會攻擊你：出手前約 1.5 秒，牠前方的地上會出現紅色扇形（受擊範圍）。扇形出現時，往旁邊或後面走出扇形，就不會受傷！"; break;
                }
            }
            else
            {
                var sw = Skills.All["nv_swing"];
                switch (btStep)
                {
                    case Bt12Approach:
                        title = "⚠ 強敵：熔岩衝鋒獸";
                        text = $"牠很硬，普攻要打很多下——這時候用技能「木棍揮擊」！先走到牠旁邊 {BtNearSwing:0.#} 公尺內（現在 {dist:F1}m）。"; break;
                    default:
                    {
                        title = "✨ 技能：木棍揮擊";
                        bool chanting = Me.charging != null && Me.charging.id == "nv_swing";
                        var to = btTarget != null ? Fighter.Flat(btTarget.Feet - Me.Feet) : Vector3.zero;
                        bool facing = to != Vector3.zero && Vector3.Angle(Fighter.Flat(Me.forward), to) < 30f;
                        steps = new[] { $"① 對著手機唸「{sw.name}」（技能格開始閃）", "② 面向衝鋒獸，讓畫面中央的準星對準牠", $"③ {HandGesture.ReleaseHint(sw)}" };
                        done = new[] { chanting, chanting && facing, false };
                        text = dist > BtNearSwing + 0.4f ? "⚠ 太遠了，再走近一點" : "照著下面 3 個步驟做：";
                        break;
                    }
                }
            }
            float gap = W * 0.015f;
            int rows = steps != null ? steps.Length : 0;
            var r = new Rect(topR.x, topR.y, topR.width, lh * (2.2f + rows * 0.95f));
            UiKit.Card(r, 0.93f, col, 0.9f);
            float ix = r.x + gap, iw = r.width - gap * 2, y = r.y + gap * 0.4f;
            UiKit.Text(new Rect(ix, y, iw, lh), "🎓 " + title, fs, Color.white, TextAnchor.MiddleLeft, true);
            y += lh;
            UiKit.Text(new Rect(ix, y, iw, lh * 1.1f), text, fsS, new Color(1f, 0.95f, 0.75f), TextAnchor.UpperLeft);
            y += lh * 1.1f;
            for (int i = 0; i < rows; i++)
            {
                bool ok = done[i], cur = !ok && (i == 0 || done[i - 1]);
                float blink = cur ? 0.6f + 0.4f * Mathf.Sin(Time.time * 8f) : 1f;
                var c = ok ? new Color(0.5f, 1f, 0.6f) : cur ? new Color(1f, 0.9f, 0.4f, blink) : new Color(0.75f, 0.78f, 0.85f);
                UiKit.Text(new Rect(ix, y, iw, lh * 0.95f), (ok ? "✔ " : cur ? "▶ " : "　 ") + steps[i], fsS, c, TextAnchor.MiddleLeft, cur);
                y += lh * 0.95f;
            }
            if (Time.time < btMsgUntil) UiKit.Pill(H * 0.55f, "💥 " + btMsg + "　再試一次！", fs, new Color(1f, 0.5f, 0.45f), pillW);
            if (btTarget != null && btTarget.Alive) DrawEdgeArrow(btTarget.Feet + Vector3.up * 0.8f, $"🎯 {dist:F1}m", col, W, H, lh, fsS);
            if (btStage == 11 && (btStep == Bt11Hit1 || btStep == Bt11Hit2)) DrawSwipeDemo(W, H, fs, col);
        }
    }
}
