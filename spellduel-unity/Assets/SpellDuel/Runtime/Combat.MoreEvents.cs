using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>場上的道具（新的特殊地點用；畫面在 SoloBattle.MoreEvents.cs 照 kind 畫）</summary>
    public class LabProp
    {
        public string kind;          // cue_low／cue_high／cue_wall／target／civ／box／plate／point／pad／turret／lamp／orb／jar／goal
        public Vector3 pos, dir;     // 位置；方向（彈幕的前進方向、半邊牆擋哪邊）
        public int state, side;      // 狀態（亮／滅、我方／敵方…）；半邊牆在左（-1）或右（1）
        public float val, t0, until; // 數值（據點的佔領度、燈的油量…）；出現時間；消失時間
        public bool dead;
    }

    /// <summary>
    /// 更多特殊地點（使用者要求「點子都先做起來，先不用急著放入關卡」；副本 event = 這些 kind、故事 mode 之後再接）：
    ///   mirror    鏡像試煉：電腦示範一串動作（蹲、跳、往左、往右），照順序做出來；每輪多一個動作
    ///   barrage   彈幕閃避：不能攻擊，低的跳、高的蹲、半邊牆側步；撐到時間到（被打中扣一點血、計分）
    ///   rhythm    節奏戰鬥：跟著心跳般的節拍，在拍點做出提示的動作，連續成功就累積攻擊加成；成功次數夠就過關
    ///   fragile   運送易碎品：拿起聖水罐（走到發光處），穩穩走到另一個點；手機晃太大力、跳、被打都會灑出來
    ///   parry     只能反擊：菁英只有在你身體閃避成功（跳、蹲躲過）或反擊之後的 3 秒內才打得傷
    ///   targets   靶場：靶子在場邊冒出來幾秒就收回，打中得分；打到平民（綠色）扣分
    ///   push      推箱機關：走過去把箱子推到發光的壓力板上，全部壓住就開門
    ///   koth      據點爭奪：三個據點，站在上面佔領；佔越多分數越快，先到 60 分贏（敵人也會搶）
    ///   towers    塔防：守住中央的核心；站在砲台座上 2 秒（耗 30 MP）蓋砲台，自動射擊；撐過時間
    ///   vigil     守夜：四盞燈的油會慢慢燒完，站在燈旁 1.5 秒重新點亮；燈滅越多怪越強；撐到天亮
    ///   elemshield 元素調和：敵人帶火／冰／雷護盾，要用同屬性打（碰場邊的屬性球讓攻擊帶屬性）；打錯屬性反而回血
    ///   tame      捕獲大賽：限時收服指定數量的魔物（會自動丟誘餌）
    /// 共用：evPhase 1＝進行中、2＝懲罰（出怪）；FinishRoomEvent 過關；FailStage／EvPenalty 失敗或懲罰。
    /// </summary>
    public partial class Battle
    {
        public readonly List<LabProp> labProps = new List<LabProp>();
        /// <summary>元素調和：帶護盾的敵人 → 屬性（1 火、2 冰、3 雷）</summary>
        public readonly Dictionary<Fighter, int> labElem = new Dictionary<Fighter, int>();
        public static readonly string[] LabKinds = { "mirror", "barrage", "rhythm", "fragile", "parry", "targets", "push", "koth", "towers", "vigil", "elemshield", "tame" };
        public static bool IsLabEvent(string kind) => System.Array.IndexOf(LabKinds, kind) >= 0;

        int labScore, labMiss, labNeed, labRound, labStep, labCombo, labTamed;
        float labEndAt, labNext, labMoveAt, labOpenUntil, labBeat, labBeatAt, labHoldStart = -1f, labEnemyScore;
        int labCharge; float labChargeUntil;
        Vector3 labRef, labRight; bool labWasCrouch, labWasAir, labHolding;
        readonly List<int> labSeq = new List<int>();
        LabProp labJar, labGoal;
        Fighter labCore;
        /// <summary>運送易碎品：手機晃動（SoloBattle 每幀送來，0～1 一次的量）</summary>
        public float labShake;
        public float LabSpill { get; private set; }
        public int LabRhythmCue { get; private set; } = -1;   // 節奏：下一個目標拍要做的動作（畫面用）
        public float LabRhythmCueAt { get; private set; }

        const int MvSquat = 0, MvJump = 1, MvLeft = 2, MvRight = 3;
        // ---- 鏡像試煉的畫面提示（使用者回報：連續多個動作，要明確標出該做哪些、完成哪些）：SoloBattle.ActionCue.cs 畫
        float labDemoAt;
        public bool MirrorActive => roomEvent == "mirror" && evPhase != 0;
        public bool MirrorPenalty => roomEvent == "mirror" && evPhase == 2;
        public IReadOnlyList<int> MirrorSeq => labSeq;
        /// <summary>-1＝還在示範；0 起＝玩家正在做第幾個</summary>
        public int MirrorStep => labStep;
        public int MirrorRound => labRound;
        public int MirrorRounds => labNeed;
        /// <summary>示範時現在亮到第幾個（每個 1.1 秒）；玩家回合：這個動作還剩幾秒</summary>
        public int MirrorDemoIndex => Mathf.FloorToInt((now - labDemoAt - 0.6f) / 1.1f);
        public float MirrorTimeLeft => Mathf.Max(0f, labMoveAt - now);
        public float MirrorDemoLeft => Mathf.Max(0f, labMoveAt - now);
        public static readonly string[] MoveNames = { "蹲下", "跳起來", "往左一步", "往右一步" };

        // ------------------------------------------------------------ 開始
        void StartLabEvent(string kind)
        {
            waveToSpawn = 0; labProps.Clear(); labElem.Clear(); labSeq.Clear();
            evLinked.Clear();
            labScore = labMiss = labRound = labStep = labCombo = labTamed = 0; labEnemyScore = 0f;
            labNext = now + 1.5f; labOpenUntil = 0f; LabSpill = 0f; labJar = labGoal = null; labCore = null; LabRhythmCue = -1;
            var c = ArenaCenter(); float r = Mathf.Max(1.2f, ArenaInner() * 0.7f);
            switch (kind)
            {
                case "mirror": labNeed = Mathf.Clamp(PI("count", 3), 1, 6); NewMirrorRound(); break;
                case "barrage": labEndAt = waveStartAt + PI("time", 60); break;
                case "rhythm": labNeed = Mathf.Clamp(PI("count", 10), 3, 30); labBeat = 0.75f; labBeatAt = now + 2f; for (int i = 0; i < 2; i++) evQueue.Add(now + 2f + i); break;
                case "fragile":
                    labNeed = Mathf.Clamp(PI("count", 3), 1, 5);
                    labJar = new LabProp { kind = "jar", pos = Ring(r, 0f) }; labGoal = new LabProp { kind = "goal", pos = Ring(r, 180f) };
                    labProps.Add(labJar); labProps.Add(labGoal);
                    for (int i = 0; i < 2; i++) evQueue.Add(now + 3f + i * 1.2f);
                    break;
                case "parry":
                {
                    int n = Mathf.Clamp(PI("count", 1), 1, 3);
                    for (int i = 0; i < n; i++)
                    {
                        var e = SpawnMinion(Ring(r, 180f + i * 40f), 0, PickMinionKind(MinionRole.Front), HpMul * 2f, DmgMul, SpeedMul);
                        MakeElite(e); e.name = "鐵壁・" + e.name; evLinked.Add(e);
                    }
                    break;
                }
                case "targets": labNeed = Mathf.Clamp(PI("count", 12), 3, 40); labEndAt = waveStartAt + PI("time", 45); break;
                case "push":
                {
                    labLastFeet = player.Feet;
                    int n = Mathf.Clamp(PI("count", 2), 1, 3);
                    for (int i = 0; i < n; i++)
                    {
                        float a = 360f * i / n;
                        labProps.Add(new LabProp { kind = "box", pos = Ring(r * 0.35f, a + 90f) });
                        labProps.Add(new LabProp { kind = "plate", pos = Ring(r, a) });
                    }
                    for (int i = 0; i < 2; i++) evQueue.Add(now + 4f + i * 2f);
                    break;
                }
                case "koth":
                    labMoveAt = 0f;
                    labNeed = Mathf.Clamp(PI("count", 60), 20, 200);
                    for (int i = 0; i < 3; i++) labProps.Add(new LabProp { kind = "point", pos = Ring(r, 120f * i) });
                    for (int i = 0; i < EvCap(); i++) evQueue.Add(now + 1.5f + i * 0.8f);
                    break;
                case "towers":
                    labEndAt = waveStartAt + PI("time", 70);
                    labCore = SpawnNpc("ev_core", P("name", "守護核心"), "priest", 0, true, PI("hp", 700) * PlayersN, 0, c);
                    labCore.rooted = true; labCore.lure = 6f; labCore.bodyRadius = 0.5f;
                    for (int i = 0; i < 4; i++) labProps.Add(new LabProp { kind = "pad", pos = Ring(r * 0.55f, 45f + 90f * i) });
                    break;
                case "vigil":
                    labEndAt = waveStartAt + PI("time", 90);
                    for (int i = 0; i < 4; i++) labProps.Add(new LabProp { kind = "lamp", pos = Ring(r, 45f + 90f * i), val = 1f - 0.18f * i, state = 1 });
                    break;
                case "elemshield":
                    labNeed = Mathf.Clamp(PI("count", 8), 3, 30);
                    for (int i = 0; i < 3; i++) labProps.Add(new LabProp { kind = "orb", pos = Ring(r, 120f * i + 60f) + Vector3.up * 0.9f, state = i + 1 });
                    for (int i = 0; i < EvCap(); i++) evQueue.Add(now + 1.5f + i * 0.9f);
                    break;
                case "tame":
                    labNeed = Mathf.Clamp(PI("count", 2), 1, 5); labEndAt = waveStartAt + PI("time", 120);
                    for (int i = 0; i < EvCap(); i++) evQueue.Add(now + 1.5f + i * 0.8f);
                    break;
            }
        }

        // ------------------------------------------------------------ 每幀
        void TickLabEvent(float dt)
        {
            var me = player;
            bool airNow = me.airborneUntil > now, crouchNow = me.crouching;
            bool jumped = airNow && !labWasAir, squatted = crouchNow && !labWasCrouch;
            labWasAir = airNow; labWasCrouch = crouchNow;
            foreach (var p in labProps) if (p.until > 0f && now > p.until) p.dead = true;
            labProps.RemoveAll(p => p.dead);
            switch (roomEvent)
            {
                case "mirror": TickMirror(jumped, squatted); break;
                case "barrage": TickBarrage(); break;
                case "rhythm": TickRhythm(jumped, squatted); break;
                case "fragile": TickFragile(dt, jumped); break;
                case "parry":
                    if (evLinked.TrueForAll(e => e == null || !e.Alive)) { FinishRoomEvent("🛡 鐵壁倒下了！"); return; }
                    RefillEv(6f);
                    break;
                case "targets": TickTargets(); break;
                case "push": TickPush(); break;
                case "koth": TickKoth(dt); break;
                case "towers": TickTowers(dt); break;
                case "vigil": TickVigil(dt); break;
                case "elemshield": TickElem(); break;
                case "tame":
                    if (labTamed >= labNeed) { FinishRoomEvent($"🐾 收服了 {labTamed} 隻！"); return; }
                    if (now > labEndAt) { FinishRoomEvent($"⏰ 時間到，收服了 {labTamed}/{labNeed} 隻"); return; }
                    RefillEv(2f);
                    if (now >= labNext)
                    {
                        labNext = now + 6f;   // 每 6 秒自動丟一次誘餌給最近、可以收服的怪
                        Fighter best = null; float bd = float.MaxValue;
                        foreach (var m in minions) if (m.Alive && Pets.Capturable(m.minionKind) && m.lureUntil <= 0f) { float d = FlatDistance(m.Feet, me.Feet); if (d < bd) { bd = d; best = m; } }
                        if (best != null) ThrowBait(best.minionKind);
                    }
                    break;
            }
        }

        // ---- 鏡像試煉
        void NewMirrorRound()
        {
            labSeq.Clear();
            int len = 2 + labRound;
            for (int i = 0; i < len; i++) labSeq.Add(rng.Next(4));
            labStep = -1; labDemoAt = now; labMoveAt = now + 1.2f + len * 1.1f;   // 先示範
            Emit("evspeak", player.Feet, Color.white, "看好了：" + string.Join("，", labSeq.ConvertAll(k => MoveNames[k])));
        }
        void TickMirror(bool jumped, bool squatted)
        {
            if (evPhase == 2) { if (PenaltyOver()) { evPhase = 1; NewMirrorRound(); } return; }
            if (labStep < 0)
            {
                if (now < labMoveAt) return;
                labStep = 0; labRef = player.Feet; labRight = Fighter.Flat(new Vector3(player.forward.z, 0f, -player.forward.x)); labMoveAt = now + 8f;   // 每個動作 8 秒（原本 6 秒太趕）
                Emit("evinfo", player.Feet, Color.white, "換你了！");
                return;
            }
            int want = labSeq[labStep];
            float lat = Vector3.Dot(player.Feet - labRef, labRight);
            bool done = want == MvSquat ? squatted : want == MvJump ? jumped : want == MvLeft ? lat < -0.5f : lat > 0.5f;
            if (done)
            {
                labStep++; labRef = player.Feet; labMoveAt = now + 8f;
                Emit("evinfo", player.Feet, new Color(0.5f, 1f, 0.6f), $"✓ {MoveNames[want]}");
                if (labStep >= labSeq.Count)
                {
                    labRound++;
                    if (labRound >= labNeed) { FinishRoomEvent("🪞 全部做對了！"); return; }
                    NewMirrorRound();
                }
            }
            else if (now > labMoveAt) EvPenalty($"太慢了！要「{MoveNames[want]}」");
        }

        // ---- 彈幕閃避
        void TickBarrage()
        {
            if (now > labEndAt) { FinishRoomEvent($"🌀 撐過了！閃過 {labScore} 發、被打中 {labMiss} 發"); return; }
            float elapsed = now - waveStartAt;
            if (now >= labNext)
            {
                labNext = now + Mathf.Lerp(2.4f, 1.1f, Mathf.Clamp01(elapsed / 50f));
                int t = rng.Next(4);
                var toMe = Fighter.Flat(player.Feet - ArenaCenter()); if (toMe == Vector3.zero) toMe = Vector3.forward;
                var dir = Battle.RotateY(-toMe, (float)(rng.NextDouble() - 0.5) * 60f);   // 從玩家前方大概的方向飛過來
                var start = player.Feet - dir * 7f;
                labProps.Add(new LabProp { kind = t == 0 ? "cue_low" : t == 1 ? "cue_high" : "cue_wall", pos = start, dir = dir, side = t == 2 ? -1 : 1, t0 = now, until = now + 2.6f, val = 7f / 4.5f });
            }
            foreach (var p in labProps)
            {
                if (!p.kind.StartsWith("cue_") || p.state != 0) continue;
                float hit = p.t0 + p.val;   // 碰到玩家的時間
                var at = p.pos + p.dir * 4.5f * (now - p.t0);
                if (now >= hit - 0.4f && now <= hit + 0.15f)
                {
                    if (p.kind == "cue_low" && player.airborneUntil > now) p.side = 99;
                    if (p.kind == "cue_high" && player.crouching) p.side = 99;
                }
                if (now < hit) continue;
                bool ok;
                if (p.kind == "cue_wall")
                {
                    var right = new Vector3(p.dir.z, 0f, -p.dir.x);
                    float lat = Vector3.Dot(player.Feet - at, right);
                    ok = p.side < 0 ? lat > 0.2f : lat < -0.2f;   // 牆擋在左邊 → 要在右邊
                }
                else ok = p.side == 99;
                p.state = ok ? 1 : 2;
                if (ok) { labScore++; BodyDodge(player, p.kind == "cue_high"); Emit("evade", player.Chest, new Color(0.5f, 0.95f, 1f), p.kind == "cue_low" ? "跳過了！" : p.kind == "cue_high" ? "蹲下躲過！" : "閃開了！"); }
                else { labMiss++; HazardDamage(player, Mathf.Max(1, Mathf.RoundToInt(player.maxHp * 0.04f)), player.Chest, p.kind == "cue_low" ? "沒跳起來" : p.kind == "cue_high" ? "沒蹲下" : "被牆掃到"); }
            }
        }

        // ---- 節奏戰鬥
        void TickRhythm(bool jumped, bool squatted)
        {
            if (labScore >= labNeed) { FinishRoomEvent($"🥁 節奏大師！成功 {labScore} 次、最高連擊 {labCombo}"); return; }
            RefillEv(3f);
            if (now >= labBeatAt)
            {
                labBeatAt += labBeat; labStep++;
                Emit("evbeat", player.Feet, Color.white, null);
                if (labStep % 4 == 0 && LabRhythmCue < 0) { LabRhythmCue = rng.Next(2); LabRhythmCueAt = labBeatAt + labBeat; Emit("evinfo", player.Feet, Color.white, $"🥁 下一拍：{MoveNames[LabRhythmCue]}！"); }
            }
            if (LabRhythmCue >= 0)
            {
                bool did = LabRhythmCue == MvSquat ? squatted : jumped;
                if (did && Mathf.Abs(now - LabRhythmCueAt) <= 0.35f)
                {
                    labScore++; labCombo++; LabRhythmCue = -1;
                    player.teamAtkUntil = now + 6f; player.teamAtk = Mathf.Min(0.5f, 0.1f * labCombo);
                    Emit("evade", player.Chest, new Color(1f, 0.85f, 0.3f), $"🎵 完美！連擊 {labCombo}（攻擊 +{Mathf.RoundToInt(player.teamAtk * 100)}%）");
                }
                else if (now > LabRhythmCueAt + 0.35f) { LabRhythmCue = -1; labCombo = 0; Emit("evwrong", player.Feet, new Color(1f, 0.5f, 0.4f), "沒跟上節拍…連擊中斷"); }
            }
        }

        // ---- 運送易碎品
        void TickFragile(float dt, bool jumped)
        {
            if (evPhase == 2) { if (PenaltyOver()) { evPhase = 1; LabSpill = 0f; labJar.state = 0; labJar.pos = labJar.val > 0f ? labJar.pos : Ring(ArenaInner() * 0.7f, 0f); } return; }
            RefillEv(5f);
            if (labJar.state == 0)
            {
                if (FlatDistance(player.Feet, labJar.pos) < 0.8f) { labJar.state = 1; LabSpill = 0f; Emit("evinfo", player.Feet, Color.white, "🍶 拿起聖水罐了！穩穩地走到發光的地方，不要晃、不要跳"); }
                return;
            }
            labJar.pos = player.Feet;
            LabSpill += labShake * 100f; labShake = 0f;
            if (jumped) LabSpill += 25f;
            if (LabSpill >= 100f) { labJar.state = 0; labJar.pos = Ring(ArenaInner() * 0.7f, 0f); EvPenalty("聖水灑光了！"); return; }
            if (FlatDistance(player.Feet, labGoal.pos) < 0.9f)
            {
                labScore++; labJar.state = 0; LabSpill = 0f;
                if (labScore >= labNeed) { FinishRoomEvent("🍶 聖水全部送到了！"); return; }
                // 下一趟：起點、終點互換
                (labJar.pos, labGoal.pos) = (labGoal.pos, Ring(ArenaInner() * 0.7f, rng.Next(360)));
                Emit("evinfo", player.Feet, new Color(0.5f, 1f, 0.6f), $"送到了！（{labScore}/{labNeed}）再拿一罐");
            }
        }
        /// <summary>被打到（運送中）：灑出一些</summary>
        void LabOnPlayerHit() { if (roomEvent == "fragile" && labJar != null && labJar.state == 1) LabSpill += 20f; }

        // ---- 靶場
        void TickTargets()
        {
            if (labScore >= labNeed) { FinishRoomEvent($"🎯 命中 {labScore} 個！"); return; }
            if (now > labEndAt) { if (labScore >= labNeed / 2) FinishRoomEvent($"⏰ 時間到，命中 {labScore}/{labNeed}"); else { FailStage($"靶場失敗：只命中 {labScore}/{labNeed}"); } return; }
            if (now >= labNext)
            {
                labNext = now + Mathf.Lerp(1.4f, 0.7f, Mathf.Clamp01((now - waveStartAt) / 40f));
                bool civ = rng.Next(5) == 0;
                var at = Ring(ArenaInner() * (0.6f + 0.35f * (float)rng.NextDouble()), rng.Next(360));
                var n = SpawnNpc(civ ? "lab_civ" : "lab_tgt", civ ? "平民（別打）" : "靶子", "priest", 1, false, 1, 0, at);
                n.rooted = true; n.maxHp = 1000; n.hp = 1000;
                n.walkIdx = rng.Next(3) == 0 ? 1 : 0;   // 1＝會橫移
                labProps.Add(new LabProp { kind = civ ? "civ" : "target", pos = at, until = now + Mathf.Lerp(3.5f, 2.2f, Mathf.Clamp01((now - waveStartAt) / 40f)), t0 = now, side = n.minionId });
            }
            foreach (var p in labProps)
            {
                var n = npcs.Find(x => x.minionId == p.side); if (n == null) { p.dead = true; continue; }
                if (n.walkIdx == 1) { var d = Fighter.Flat(new Vector3(-(n.Feet - ArenaCenter()).z, 0f, (n.Feet - ArenaCenter()).x)); n.head += d * 1.2f * Time.deltaTime; p.pos = n.Feet; }
                if (n.hp < n.maxHp - 0.5f)
                {
                    if (p.kind == "civ") { labScore = Mathf.Max(0, labScore - 2); Emit("evwrong", n.Feet, new Color(1f, 0.4f, 0.3f), "打到平民了！-2"); }
                    else { labScore++; Emit("evade", n.Chest, new Color(1f, 0.85f, 0.3f), $"命中！{labScore}/{labNeed}"); }
                    p.dead = true;
                }
            }
            foreach (var p in labProps) if (p.dead || now > p.until) { p.dead = true; npcs.RemoveAll(x => x.minionId == p.side); }
        }

        // ---- 推箱
        Vector3 labLastFeet;
        void TickPush()
        {
            var mv = Fighter.Flat(player.Feet - labLastFeet) * (player.Feet - labLastFeet).magnitude; labLastFeet = player.Feet;
            int pressed = 0, plates = 0;
            foreach (var b in labProps)
            {
                if (b.kind != "box") continue;
                var to = b.pos - player.Feet; to.y = 0f;
                if (to.magnitude < 0.85f && mv.sqrMagnitude > 1e-6f && Vector3.Dot(mv, to) > 0f)
                {
                    var next = b.pos + mv;
                    if (minionInside == null || minionInside(next)) b.pos = next;
                }
            }
            foreach (var pl in labProps)
            {
                if (pl.kind != "plate") continue; plates++;
                pl.state = labProps.Exists(b => b.kind == "box" && FlatDistance(b.pos, pl.pos) < 0.55f) ? 1 : 0;
                pressed += pl.state;
            }
            RefillEv(8f);
            if (plates > 0 && pressed == plates) FinishRoomEvent("🧱 機關啟動，門開了！");
        }

        // ---- 據點爭奪
        void TickKoth(float dt)
        {
            RefillEv(1.5f);
            int mine = 0, theirs = 0;
            foreach (var p in labProps)
            {
                if (p.kind != "point") continue;
                bool meOn = false; foreach (var pl in Players()) if (pl.Alive && FlatDistance(pl.Feet, p.pos) < 1.2f) meOn = true;
                bool foe = false; foreach (var m in minions) if (m.Alive && FlatDistance(m.Feet, p.pos) < 3f) foe = true;
                if (meOn && !foe) p.val = Mathf.Min(1f, p.val + 0.5f * dt);
                else if (foe && !meOn) p.val = Mathf.Max(-1f, p.val - 0.25f * dt);
                p.state = p.val >= 0.99f ? 1 : p.val <= -0.99f ? -1 : 0;
                if (p.state == 1) mine++; else if (p.state == -1) theirs++;
            }
            labScore += 0; labEnemyScore += theirs * dt * 1.2f; labMoveAt += mine * dt * 1.2f;
            if (labMoveAt >= labNeed) { FinishRoomEvent("🏰 據點爭奪勝利！"); return; }
            if (labEnemyScore >= labNeed) { if (evSoft) { SoftFailGoal("據點被敵人搶走了…"); } else FailStage("據點被敵人搶走了…"); }
        }

        // ---- 塔防
        void TickTowers(float dt)
        {
            if (labCore == null || !labCore.Alive) { if (evSoft) SoftFailGoal("核心被打壞了…"); else FailStage("守護核心被打壞了…"); return; }
            if (now > labEndAt) { FinishRoomEvent("🗼 守住了！"); return; }
            // 三路出怪
            if (now >= labNext)
            {
                labNext = now + Mathf.Lerp(3.2f, 1.6f, Mathf.Clamp01((now - waveStartAt) / 60f));
                int lane = rng.Next(3);
                if (FixedDungeonSpawns) EvSpawn(); else EvSpawn(edgePointAt != null ? edgePointAt(120f * lane) : RandomInsidePoint(Ring(ArenaInner(), 120f * lane)));   // 副本：照副本的出怪點
            }
            // 蓋砲台：站在空的砲台座上 2 秒（30 MP）
            LabProp on = null; foreach (var p in labProps) if (p.kind == "pad" && FlatDistance(player.Feet, p.pos) < 0.7f) on = p;
            if (on != null && on.state == 0)
            {
                if (labHoldStart < 0f) labHoldStart = now;
                if (now - labHoldStart >= 2f)
                {
                    labHoldStart = -1f;
                    if (player.mp >= 30) { player.mp -= 30; on.state = 1; on.kind = "turret"; on.t0 = now; Emit("evinfo", on.pos, new Color(0.5f, 0.9f, 1f), "🗼 砲台蓋好了！"); }
                    else Emit("evwrong", on.pos, new Color(1f, 0.5f, 0.4f), "MP 不夠（要 30）");
                }
            }
            else labHoldStart = -1f;
            // 砲台射擊
            foreach (var t in labProps)
            {
                if (t.kind != "turret" || now < t.until) continue;
                Fighter best = null; float bd = 6.5f;
                foreach (var m in minions) if (m.Alive) { float d = FlatDistance(m.Feet, t.pos); if (d < bd) { bd = d; best = m; } }
                if (best == null) continue;
                t.until = now + 1.1f; t.dir = best.Chest;
                int dmg = Mathf.Max(1, Mathf.RoundToInt(best.maxHp * 0.18f));
                ApplyDamageAndEffect(best, dmg, EffectKind.None, 0f, 0f, best.Chest, player);
                Emit("labshot", t.pos + Vector3.up * 1.2f, new Color(0.5f, 0.9f, 1f), null);
            }
        }

        // ---- 守夜
        void TickVigil(float dt)
        {
            if (now > labEndAt) { FinishRoomEvent("🌅 天亮了！撐過這一夜"); return; }
            int lit = 0;
            foreach (var l in labProps)
            {
                if (l.kind != "lamp") continue;
                if (l.state == 1) { l.val -= dt / 28f; if (l.val <= 0f) { l.val = 0f; l.state = 0; Emit("evwrong", l.pos, new Color(1f, 0.6f, 0.3f), "🕯 一盞燈熄了！黑暗裡的東西變多了"); } }
                bool near = FlatDistance(player.Feet, l.pos) < 1.0f;
                if (near && l.val < 0.85f) { l.t0 += dt; if (l.t0 >= 1.5f) { l.t0 = 0f; l.val = 1f; l.state = 1; Emit("evinfo", l.pos, new Color(1f, 0.9f, 0.5f), "🕯 燈重新點亮了"); } }
                else l.t0 = 0f;
                lit += l.state;
            }
            int dark = 4 - lit;
            int cap = 2 + dark * 2 + (EvParty - 1);
            int alive = 0; foreach (var m in minions) if (m.Alive) alive++;
            if (alive + evQueue.Count < cap && now >= labNext)
            {
                labNext = now + 1.5f;
                var m = EvSpawn();
                if (m != null && dark >= 3 && rng.Next(3) == 0) MakeElite(m);
            }
        }

        // ---- 元素調和
        void TickElem()
        {
            if (labScore >= labNeed) { FinishRoomEvent("🧩 全部的元素護盾都破了！"); return; }
            RefillEv(1.5f);
            foreach (var m in minions) if (m.Alive && !labElem.ContainsKey(m)) labElem[m] = 1 + rng.Next(3);
            foreach (var kv in new List<KeyValuePair<Fighter, int>>(labElem)) if (!kv.Key.Alive) { labElem.Remove(kv.Key); labScore++; }
            foreach (var p in labProps)
                if (p.kind == "orb" && FlatDistance(player.Feet, p.pos) < 0.8f && (labCharge != p.state || now > labChargeUntil - 9f))
                { labCharge = p.state; labChargeUntil = now + 12f; Emit("evcharge", p.pos, ElementColor(labCharge), $"{ElementNames[labCharge]}之力！攻擊帶{ElementNames[labCharge]}屬性（12 秒）"); }
            if (labCharge != 0 && now > labChargeUntil) labCharge = 0;
        }

        /// <summary>
        /// ApplyDamageAndEffect 開頭叫：新的特殊地點改傷害（只能反擊、元素調和）。回傳 true＝這一下完全不算
        /// </summary>
        bool LabDamageFilter(Fighter target, ref int damage, Fighter by)
        {
            if (target == player || target.isAlly) { if (damage > 0) LabOnPlayerHit(); return false; }
            if (roomEvent == "parry" && evLinked.Contains(target) && (by == null || by == player || by.isAlly))
            {
                bool open = now < labOpenUntil || (hitProj != null && hitProj.reflected);
                if (!open) { if (now > labNext) { labNext = now + 1.5f; Emit("evwrong", target.Chest, new Color(0.7f, 0.8f, 1f), "🛡 鐵壁擋住了！先閃過牠的攻擊（跳、蹲）再反擊"); } return true; }
                damage = Mathf.RoundToInt(damage * 2f);
            }
            if (roomEvent == "elemshield" && labElem.TryGetValue(target, out var el))
            {
                int atk = HitSkill != null ? ElementOf(HitSkill) : 0; if (atk == 0) atk = labCharge;
                if (atk == el) { damage = Mathf.RoundToInt(damage * 1.5f); return false; }
                if (atk != 0) { target.hp = Mathf.Min(target.maxHp, target.hp + target.maxHp * 0.08f); Emit("evwrong", target.Chest, ElementColor(el), $"屬性不對！{ElementNames[el]}護盾吸收了攻擊"); }
                else if (now > labNext) { labNext = now + 1.5f; Emit("evwrong", target.Chest, ElementColor(el), $"要用{ElementNames[el]}屬性打！（碰場邊的{ElementNames[el]}球）"); }
                return true;
            }
            return false;
        }
        /// <summary>只能反擊：身體閃避成功 → 3 秒破綻</summary>
        void LabOnDodge() { if (roomEvent == "parry") { labOpenUntil = now + 3f; Emit("evinfo", player.Chest, new Color(1f, 0.85f, 0.3f), "⚡ 破綻！3 秒內打得傷鐵壁（傷害 ×2）"); } }
        /// <summary>捕獲大賽：收服一隻</summary>
        void LabOnTamed() { if (roomEvent == "tame") { labTamed++; Emit("evinfo", player.Feet, new Color(0.5f, 1f, 0.8f), $"🐾 {labTamed}/{labNeed}"); } }

        // ------------------------------------------------------------ 地圖玩法用（SoloBattle.MapModes.cs）
        /// <summary>追蹤狩獵的獵物：這一區的菁英（血量 ×4）</summary>
        public Fighter SpawnTrackTarget(Vector3 at)
        {
            var m = SpawnMinion(at, 0, PickMinionKind(MinionRole.Front), HpMul * 4f, DmgMul, SpeedMul);
            MakeElite(m); m.name = "稀有・" + m.name;
            return m;
        }
        /// <summary>讓一隻怪這段時間不自己走、不出手（逃跑由外面移動）</summary>
        public void HoldMinion(Fighter m, float t) { if (m != null) m.recoverUntil = Mathf.Max(m.recoverUntil, now + t); }
        /// <summary>噤聲潛行：聲音傳到 at 附近 → 還沒發現玩家的怪轉頭過來張望（看到就算發現）。回傳轉頭的數量</summary>
        public int ExHear(Vector3 at, float radius)
        {
            int n = 0;
            foreach (var kv in exActive)
            {
                var m = kv.Value; if (m == null || !m.Alive || m.exAware || FlatDistance(m.Feet, at) > radius) continue;
                var d = Fighter.Flat(at - m.Feet); if (d == Vector3.zero) continue;
                m.exSleep = false; m.forward = d; ExIdle(m, 1, 3f); n++;
            }
            return n;
        }

        string LabEvText()
        {
            switch (roomEvent)
            {
                case "mirror":
                    if (evPhase == 2) return "⚔ 打倒湧出來的怪，再看一次示範";
                    if (labStep < 0) return $"🪞 看好了（第 {labRound + 1}/{labNeed} 輪）：" + string.Join(" → ", labSeq.ConvertAll(k => MoveNames[k]));
                    return $"🪞 換你（第 {labRound + 1}/{labNeed} 輪）：第 {labStep + 1}/{labSeq.Count} 個動作";
                case "barrage": return $"🌀 彈幕閃避！剩 {Mathf.Max(0f, labEndAt - now):0} 秒・閃過 {labScore}・被打中 {labMiss}（紅＝跳、藍＝蹲、橘牆＝往另一邊）";
                case "rhythm": return $"🥁 跟著節拍做動作（{labScore}/{labNeed}）連擊 {labCombo}" + (LabRhythmCue >= 0 ? $"　下一拍：{MoveNames[LabRhythmCue]}" : "");
                case "fragile": return evPhase == 2 ? "⚔ 聖水灑了！打倒湧出來的怪" : labJar != null && labJar.state == 1 ? $"🍶 穩穩走到發光處（{labScore}/{labNeed}）灑出 {Mathf.Min(100f, LabSpill):0}%" : $"🍶 去拿聖水罐（{labScore}/{labNeed}）";
                case "parry": return now < labOpenUntil ? $"⚡ 破綻！快攻擊（剩 {labOpenUntil - now:0.0} 秒）" : "🛡 只能反擊：跳過或蹲過鐵壁的攻擊，才會露出破綻";
                case "targets": return $"🎯 靶場：命中 {labScore}/{labNeed}・剩 {Mathf.Max(0f, labEndAt - now):0} 秒（綠色平民別打）";
                case "push": { int a = 0, b = 0; foreach (var p in labProps) if (p.kind == "plate") { b++; a += p.state; } return $"🧱 把箱子推到發光的壓力板上（{a}/{b}）"; }
                case "koth": return $"🏰 據點爭奪：我方 {labMoveAt:0}／敵方 {labEnemyScore:0}（先到 {labNeed}）";
                case "towers": return $"🗼 塔防：守住核心（{(labCore != null ? Mathf.CeilToInt(labCore.hp) : 0)}）剩 {Mathf.Max(0f, labEndAt - now):0} 秒・站在砲台座上 2 秒蓋砲台（30 MP）";
                case "vigil": { int lit = 0; foreach (var l in labProps) if (l.kind == "lamp") lit += l.state; return $"🌗 守夜：撐到天亮（剩 {Mathf.Max(0f, labEndAt - now):0} 秒）・亮著的燈 {lit}/4（站在燈旁重新點亮）"; }
                case "elemshield": return $"🧩 元素調和（{labScore}/{labNeed}）" + (labCharge != 0 ? $"・目前帶{ElementNames[labCharge]}屬性" : "・碰屬性球讓攻擊帶屬性");
                case "tame": return $"🐾 捕獲大賽：{labTamed}/{labNeed}・剩 {Mathf.Max(0f, labEndAt - now):0} 秒（把血壓到三成以下，會自動丟誘餌）";
            }
            return "";
        }
    }
}
