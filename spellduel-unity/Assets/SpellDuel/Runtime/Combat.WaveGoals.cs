using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的波次目標（使用者要求：普通關卡每一波都是清怪太無聊）：
    ///   每一關（新手關、試煉、只有 Boss 的關卡以外）挑一波（第一波、小 Boss 波、菁英波、Boss 波以外）換成特殊目標：
    ///     defend  守護水晶：怪會去打場中的水晶，清完這一波＝守住（水晶被打碎＝這個目標失敗，關卡照常繼續）
    ///     capture 佔點：走進發光的圈裡撐 CaptureTime 秒（有敵人在圈裡會暫停；進度一半時圈會換位置），怪會一直湧出
    ///     gates   破壞召喚門：場邊的召喚門會一直出怪，打壞所有門、清完怪＝過關（用副本「圖騰」的流程）
    ///     chase   追獵：這一波照常清怪，另外有一隻扛著寶物的盜寶頭目到處逃跑，RunnerTime 秒內打倒有額外獎勵
    ///     survive 撐住：倒數 30 秒，怪物不斷湧出，時間到就過關（用副本「撐住」的流程）
    ///   達成（evgoal）：通關時多拿這一關 GoalGoldMul 的金幣；追獵另外馬上給賞金（evbounty）。
    ///   邏輯和副本的特殊地點共用（Combat.RoomEvents.cs），只在房主／單人跑；故事合作的隊友收 rev 顯示。
    /// </summary>
    public partial class Battle
    {
        public static readonly string[] WaveGoalKinds = { "defend", "capture", "gates", "chase", "survive" };
        /// <summary>這一關要不要排波次目標（SoloBattle 在故事關卡設定）</summary>
        public bool waveGoals;
        /// <summary>故事的波次目標：失敗不會讓關卡失敗</summary>
        public bool evSoft;
        int goalWave = -1; string goalKind;
        /// <summary>故事關卡的玩法（使用者要求）：survive＝整關就是生存（撐過 surviveTime 秒，怪越出越多）；null＝一般</summary>
        public string stageMode; public int surviveTime = 90;
        /// <summary>生存：每過幾秒，場上同時的怪多一隻（最多多 SurviveRampMax 隻）</summary>
        public const float SurviveRampEvery = 12f; public const int SurviveRampMax = 7;
        static int lastGoalKind = -1;   // 連續兩關不要同一種

        public const float CrystalHpMul = 3f;     // 守護水晶的血量＝玩家最大 HP ×3（×人數）
        public const int GateBaseHp = 120;          // 召喚門的血量＝120 × 這一波小怪的血量倍率（約 6 隻小怪）
        public const float CaptureRadius = 0.9f, CaptureTime = 12f, CaptureDecay = 0.25f;
        public const float RunnerTime = 25f, RunnerSpeed = 1.05f, RunnerScare = 4.5f, RunnerHpMul = 4f;

        /// <summary>StartStage 叫：挑這一關哪一波、哪一種目標</summary>
        void PlanWaveGoal()
        {
            goalWave = -1; goalKind = null;
            if (stageMode == "survive" && stageEnd > 0) { goalWave = stageStart; goalKind = "survive"; return; }   // 生存玩法：第一波就是整關
            if (!waveGoals || stageEnd <= 0) return;
            var ok = new List<int>();
            for (int w = stageStart; w <= stageEnd; w++)
            {
                if (w % BossEvery == 0) continue;                                          // Boss 波
                if (w == stageStart && stageEnd > stageStart) continue;                    // 第一波先暖身
                if (RhythmSpawns && specialKind >= 0 && w == stageStart + 2) continue;     // 小 Boss 波
                if (w == eliteWaveAt) continue;                                            // 菁英波
                ok.Add(w);
            }
            if (ok.Count == 0) return;
            goalWave = ok[rng.Next(ok.Count)];
            int k; do k = rng.Next(WaveGoalKinds.Length); while (k == lastGoalKind);
            lastGoalKind = k; goalKind = WaveGoalKinds[k];
        }

        /// <summary>NextWave 叫：這一波是目標波就開始</summary>
        void MaybeStartWaveGoal()
        {
            if (goalWave < 0 || wave != goalWave || manualWaves || stageEnd <= 0 || goalKind == null) return;
            var p = new Dictionary<string, string>();
            switch (goalKind)
            {
                case "defend":
                    p["name"] = "守護水晶"; p["fail"] = "守護水晶被打碎了…";
                    p["hp"] = Mathf.RoundToInt(Mathf.Max(300f, player.maxHp * CrystalHpMul) / (1f + HpPerWave * Tier)).ToString();   // SpawnNpc 會再乘友方的波次倍率
                    break;
                case "gates":
                    p["name"] = "召喚門"; p["done"] = "召喚門全部毀掉了！"; p["count"] = "2";
                    p["hp"] = GateBaseHp.ToString();   // SpawnNpc 會再乘這一波的血量倍率
                    break;
                case "survive":
                    p["time"] = stageMode == "survive" ? surviveTime.ToString() : "30";
                    if (stageMode == "survive") p["ramp"] = "1";
                    break;
            }
            StartRoomEvent(goalKind == "gates" ? "totems" : goalKind, p);
            evSoft = stageMode != "survive";   // 生存玩法：撐不住（倒下）就是關卡失敗；時間到＝通關
            Emit("evinfo", player.Feet, new Color(1f, 0.9f, 0.5f), stageMode == "survive" ? $"⏳ 生存：撐過 {surviveTime} 秒！敵人會越來越多" : GoalIntro(goalKind));
        }

        static string GoalIntro(string kind) => kind switch
        {
            "defend" => "🛡 這一波：守住場中的守護水晶！",
            "capture" => "⭕ 這一波：走進發光的圈裡佔領！",
            "gates" => "🌀 這一波：打壞召喚門，不然怪會一直出來！",
            "chase" => $"💰 這一波：盜寶頭目要來了，{RunnerTime:0} 秒內打倒牠有賞金！",
            "survive" => "⏳ 這一波：撐住 30 秒！",
            _ => "",
        };

        /// <summary>故事目標失敗（不影響關卡）：拿掉道具，這一波照一般清怪繼續</summary>
        void SoftFailGoal(string why)
        {
            Emit("evfail", player.Feet, new Color(1f, 0.4f, 0.3f), why);
            CleanupRoomEvent();
        }

        // ------------------------------------------------------------ 佔點
        float evProgress;
        void StartCapture()
        {
            waveToSpawn = 0; evProgress = 0f;
            evPts.Add(CapturePoint(player.Feet)); evState.Add(0);
            for (int i = 0; i < EvCap(); i++) evQueue.Add(waveStartAt + 1f + i * 0.8f);
        }

        /// <summary>佔點圈的位置：場內一圈上，離 avoid 最遠的（讓玩家要真的走過去）</summary>
        Vector3 CapturePoint(Vector3 avoid)
        {
            float rr = Mathf.Max(1f, ArenaInner() * 0.6f);
            Vector3 best = ArenaCenter(); float bestD = -1f;
            float start = (float)rng.NextDouble() * 360f;
            for (int i = 0; i < 8; i++)
            {
                var p = Ring(rr, start + 45f * i);
                if (minionInside != null && !minionInside(p)) continue;
                float d = FlatDistance(p, avoid);
                if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>回傳 true＝佔領完成（已經過關）</summary>
        bool TickCapture(float dt)
        {
            RefillEv(EvRespawnDelay);
            if (evPts.Count == 0) return false;
            var c = evPts[0];
            bool mine = false, foe = false;
            foreach (var pl in Players()) if (pl.Alive && FlatDistance(pl.Feet, c) <= CaptureRadius) mine = true;
            foreach (var m in minions) if (m.Alive && !m.hidden && FlatDistance(m.Feet, c) <= CaptureRadius) foe = true;
            if (mine && !foe) evProgress += dt / CaptureTime;
            else if (!mine) evProgress = Mathf.Max(0f, evProgress - dt / CaptureTime * CaptureDecay);
            if (evPhase == 1 && evProgress >= 0.5f)
            {
                evPhase = 2;
                evPts[0] = CapturePoint(c);
                Emit("evinfo", evPts[0], new Color(0.5f, 0.9f, 1f), "光圈移動了！跟過去！");
            }
            evState[0] = Mathf.Clamp(Mathf.FloorToInt(evProgress * 100f), 0, 100) + (mine && foe ? 1000 : mine ? 2000 : 0);
            if (evProgress >= 1f) { FinishRoomEvent("佔領成功！"); return true; }
            return false;
        }

        // ------------------------------------------------------------ 追獵
        Fighter chaseRunner; bool chaseGone, chaseWon;
        void StartChase()
        {
            chaseRunner = null; chaseGone = false; chaseWon = false;
            evNextSummon = waveStartAt + 3f; evEndAt = -1f;
        }

        void TickChase()
        {
            if (chaseRunner == null && !chaseGone && now >= evNextSummon && edgePoint != null)
            {
                int k = 0;
                for (int i = 0; i < 12; i++) { k = PickMinionKind(MinionRole.Front); if (k != 3 && k != TreasureKind && BaseAlt(k) <= 0f && !IsRanged(k)) break; }
                var m = SpawnMinion(edgePoint(), 0, k, HpMul * RunnerHpMul, 0f, SpeedMul);
                m.evRunner = true; m.isElite = true; m.bodyRadius *= 1.3f; m.hidden = false;
                m.dmgMul = 0f; m.accuracy = 0f; m.attackReadyAt = float.MaxValue; m.esBornAt = float.MaxValue;
                m.strafeSign = rng.NextDouble() < 0.5 ? 1 : -1;
                m.name = "盜寶頭目・" + m.name;
                chaseRunner = m; evEndAt = now + RunnerTime;
                Emit("evinfo", m.Feet, new Color(1f, 0.85f, 0.3f), $"💰 {m.name}扛著寶物出現了！");
            }
            if (chaseRunner == null || chaseGone) return;
            if (!chaseRunner.Alive)
            {
                chaseGone = true; chaseWon = true; evPhase = 2;
                Emit("evbounty", chaseRunner.Feet, new Color(1f, 0.85f, 0.3f), "💰 打倒盜寶頭目，搶回寶物了！");
            }
            else if (now >= evEndAt)
            {
                chaseGone = true; evPhase = 2;
                Emit("evfail", chaseRunner.Feet, new Color(1f, 0.6f, 0.3f), "盜寶頭目逃走了…");
                chaseRunner.hp = 0f;   // 逃走：沒有擊倒紀錄、沒有獎勵
            }
        }

        /// <summary>這一波打完了，盜寶頭目還在 → 趁亂逃走</summary>
        void ChaseWaveOver()
        {
            if (roomEvent != "chase" || chaseRunner == null || chaseGone || !chaseRunner.Alive) return;
            chaseGone = true;
            Emit("evfail", chaseRunner.Feet, new Color(1f, 0.6f, 0.3f), "盜寶頭目趁亂逃走了…");
            chaseRunner.hp = 0f;
        }

        /// <summary>盜寶頭目：玩家靠近就往反方向逃（碰到場地邊沿著邊繞），沒人靠近就繞著最近的玩家慢慢晃；不攻擊</summary>
        void UpdateRunner(Fighter m, float dt)
        {
            bool outside = minionInside != null && !minionInside(m.Feet);
            Vector3 away = Vector3.zero; Fighter nearest = null; float nd = float.MaxValue;
            foreach (var pl in Players())
            {
                if (!pl.Alive) continue;
                var v = m.Feet - pl.Feet; v.y = 0f; float d = v.magnitude;
                if (d < nd) { nd = d; nearest = pl; }
                if (d < RunnerScare) away += (d > 0.01f ? v / d : new Vector3(m.strafeSign, 0f, 0f)) * (RunnerScare - d);
            }
            Vector3 dir; float speed = RunnerSpeed;
            if (outside) dir = Fighter.Flat(ArenaCenter() - m.Feet);   // 剛出現：先跑進場地
            else if (away != Vector3.zero) dir = away.normalized;
            else
            {
                dir = nearest != null ? RotateY(Fighter.Flat(nearest.Feet - m.Feet), 90f * m.strafeSign) : m.forward;
                speed = RunnerSpeed * 0.5f;
                if (rng.NextDouble() < dt * 0.3) m.strafeSign = -m.strafeSign;
            }
            if (dir == Vector3.zero) return;
            dir = dir.normalized;
            float step = speed * dt;
            foreach (float turn in new[] { 0f, 50f * m.strafeSign, -50f * m.strafeSign, 100f * m.strafeSign, -100f * m.strafeSign, 150f * m.strafeSign, -150f * m.strafeSign })
            {
                var d2 = turn == 0f ? dir : RotateY(dir, turn);
                var next = m.Feet + d2 * step;
                if (!outside && minionInside != null && !minionInside(next)) continue;
                if (turn != 0f && Mathf.Abs(turn) <= 50f) m.strafeSign = turn > 0f ? 1 : -1;
                m.head = new Vector3(next.x, m.head.y, next.z);
                m.forward = d2;
                return;
            }
        }

        /// <summary>HUD：盜寶頭目還剩幾秒（沒有＝−1）</summary>
        public float RunnerLeft(Fighter m) => m != null && m == chaseRunner && !chaseGone ? Mathf.Max(0f, evEndAt - now) : -1f;
    }
}
