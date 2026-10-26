using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 初心者轉職的「專業試煉」（職業工會先選職業，第一關，30 秒；用初心者的木棍與投石）：
    ///   劍士・劍術訓練：靶人在場上快速亂跑，30 秒內用木棍打中 5 次（站太遠丟石頭不算）。
    ///   法師・念咒訓練：畫面給一串咒文，唸出來（讀音八成像就算，字不同沒關係），30 秒內唸對 15 組。
    ///   弓箭手・射擊訓練：離你 6m 的空中／地面出現規律移動的靶子，用投石打中就換地方再出現，30 秒內打中 8 個。
    ///   刺客・閃避訓練：每 5 秒進來一隻不同的敵人（順序固定、打不死），30 秒內一下都不能被打中。
    ///   牧師・觀察訓練：一群一模一樣的小鬼到處跑，其中一隻有個小配件；30 秒內打中牠一下＝通過，打錯＝失敗。
    /// 失敗就回職業工會，重新跟公會長對話再來。通過才進「實戰試煉」（打倒 5 隻小怪，SoloBattle.JobTrial.cs），都過了才能轉職。
    /// </summary>
    public partial class SoloBattle
    {
        public const float ClassTrialTime = 30f;
        public const int SwordHitsNeed = 5, ChantNeed = 15, ShotHitsNeed = 8, WatchCount = 7;
        public const float ChantPass = 0.8f, SwordHitRange = 2.2f, ShotTargetDist = 6f, ShotHitRadius = 0.55f;
        public const float RunnerSpeed = 1.5f, WatchWalk = 0.9f;
        /// <summary>閃避訓練：火球從場地的一面來，第一面 10 顆、每面多 1 顆；速度第一面 2 m/s、每面 +0.1；顆與顆之間 0.25 秒；換面先預警 1.2 秒</summary>
        public const int DodgeBalls0 = 10; public const float DodgeSpeed0 = 2f, DodgeSpeedStep = 0.1f, DodgeGap = 0.25f, DodgeWarn = 1.2f, DodgeLowY = 0.3f, DodgeHighY = 1.45f;
        static readonly string[] SideNames = { "前面", "右邊", "後面", "左邊" };
        /// <summary>念咒訓練的咒文字庫：每組 1～3 個詞接起來（2～7 個字）</summary>
        public static readonly string[] ChantWords =
        {
            "炎", "冰", "雷", "風", "光", "影", "焰之壁", "霜之牙", "雷鳴", "疾風", "聖光", "暗影", "大地", "星辰", "幻影", "烈焰",
            "寒冰", "狂雷", "旋風", "淨化", "守護", "破邪", "流星", "月華", "蒼穹", "深淵", "燃燒", "凍結", "飛舞", "降臨", "咆哮", "低語",
        };

        string classTrial;                               // 正在進行哪個職業的專業試煉（null＝沒有）
        bool trialDaily;                                 // 每日訓練（訓練場）：30 秒做滿算分數換試煉幣，不是轉職
        int watchFound; (int coins, int bonus) trialReward;
        bool classTrialWon; string classTrialNote;       // 結算：通過了嗎、原因
        System.Random trialRng;
        float trialLastMeHp;
        float ClassTrialStart => CalibTime + DailyChallenge.Countdown;   // 站好校正 2 秒＋倒數 3 秒
        float ClassTrialElapsed => battle != null ? battle.now - ClassTrialStart : 0f;
        /// <summary>沒有看得見的敵人（不畫鎖定框、敵人方向）</summary>
        bool ClassTrialNoEnemy => classTrial != null && classTrial != "swordsman";
        public static string ClassTrialName(string cls) => cls switch
        {
            "swordsman" => "劍術訓練", "mage" => "念咒訓練", "archer" => "射擊訓練", "assassin" => "閃避訓練", "priest" => "觀察訓練", _ => "專業試煉",
        };
        public static string ClassTrialDesc(string cls) => cls switch
        {
            "swordsman" => $"靶人會在場上快速亂跑：{ClassTrialTime:0} 秒內用木棍打中 {SwordHitsNeed} 次",
            "mage" => $"照畫面唸出咒文（讀音像就算）：{ClassTrialTime:0} 秒內唸對 {ChantNeed} 組",
            "archer" => $"6m 外的空中與地面會出現會動的靶子：{ClassTrialTime:0} 秒內用投石打中 {ShotHitsNeed} 個",
            "assassin" => $"火球一次從場地的一面飛來 {DodgeBalls0} 顆（低的跳、高的蹲），一面接一面越來越快越多：撐過 {ClassTrialTime:0} 秒，一下都不能被打中",
            "priest" => $"一群長得一樣的小鬼裡有一隻不一樣：{ClassTrialTime:0} 秒內找出牠、打牠一下（打錯＝失敗）",
            _ => "",
        };

        /// <summary>公會長在試煉前說的話（各職業）</summary>
        static string[] ClassTrialIntro(string cls) => cls switch
        {
            "swordsman" => new[]
            {
                "想當劍士？記住：一位好的劍士，眼睛要比劍快。敵人不會站在原地等你砍。",
                $"空地上有個靶人，牠會到處亂跑。{ClassTrialTime:0} 秒內，用木棍打中牠 {SwordHitsNeed} 次。站遠遠的丟石頭不算，劍士的距離是一步之內。",
                "追不上就先看牠往哪跑，再堵過去。去吧。",
            },
            "mage" => new[]
            {
                "想當法師？法師的力量來自準確的咒文。唸錯一個音，火球就會在自己手裡炸開。",
                $"接下來畫面會一組一組給你咒文，看到就唸出來。讀音像就算，寫法不用管。{ClassTrialTime:0} 秒內唸對 {ChantNeed} 組。",
                "別急著快，先求準。準了，自然就快。",
            },
            "archer" => new[]
            {
                "想當弓箭手？弓箭手的敵人在天上、在地上，而且不會停下來讓你瞄。",
                $"離你六公尺的地方會出現靶子，空中的、地上的都有，牠們會照固定的路線移動。用投石打中一個，下一個就換地方出現。{ClassTrialTime:0} 秒內打中 {ShotHitsNeed} 個。",
                "別追著靶子丟，看清楚牠的路線，丟在牠要經過的地方。",
            },
            "assassin" => new[]
            {
                "想當刺客？刺客不靠硬扛。被碰到一下，就是死。",
                $"接下來火球會從場地的一面飛過來，一次 {DodgeBalls0} 顆，前後錯開、高低左右都不一定。貼地的跳起來躲，胸口高的蹲下去。{ClassTrialTime:0} 秒內，一下都不能被打中。",
                "一面飛完馬上換另一面，而且一面比一面快、一面比一面多。先看預警的方向，再看每一顆的高度。去吧。",
            },
            "priest" => new[]
            {
                "想當牧師？一位好的牧師，必須有優秀的觀察力。戰場上誰快倒下、誰在假裝沒事，你得一眼看出來，才能即時把治療送到需要的人身上。",
                $"空地上有一群小鬼，看起來一模一樣，但其中一隻身上多了個小東西。{ClassTrialTime:0} 秒內找出牠、打牠一下就算通過。",
                "打錯了，就等於治錯了人。那就是失敗。看仔細再出手。",
            },
            _ => new[] { "去完成試煉吧。" },
        };

        /// <summary>結算時公會長的話</summary>
        static string ClassTrialVerdict(string cls, bool won) => cls switch
        {
            "swordsman" => won ? "打得好。眼睛跟得上，劍就跟得上。" : "追不上牠。劍士的腳要比敵人快，休息一下再來。",
            "mage" => won ? "咒文唸得很穩。這雙嘴，配得上法杖。" : "咒文還不夠準。先把字看清楚、一個音一個音唸，再來一次。",
            "archer" => won ? "準頭不錯。你已經懂得看路線，而不是追靶子。" : "打太少了。別追著靶子丟，預判牠要去的地方。再來一次。",
            "assassin" => won ? "一下都沒被碰到。這就是刺客的生存方式。" : "被打中了。對刺客來說，那一下就是全部。再來一次。",
            "priest" => won ? "看出來了。這雙眼睛，會救很多人。" : "看錯了。治錯人，比沒治更糟。再仔細看一次。",
            _ => won ? "通過了。" : "沒有通過。",
        };

        // ---------------------------------------------------------------- 開始
        void StartClassTrial(string cls)
        {
            if (!Skills.Classes.ContainsKey(cls)) return;
            if (GateArea(() => StartClassTrial(cls))) return;   // 先重新放置場地
            storyMapOpen = false; villageReturn = WorldUI.Screen.Guild; jobTrialRound = 0;
            var lines = new List<StoryLine>();
            foreach (var t in ClassTrialIntro(cls)) lines.Add(new StoryLine { speaker = "公會長", tag = "guild", text = t });
            ShowDialogue(lines, () => BeginClassTrial(cls));
        }

        /// <summary>訓練場的每日訓練：同一套試煉，30 秒做滿算分數（一天一次）</summary>
        void StartDailyTraining(string cls)
        {
            if (!Skills.Classes.ContainsKey(cls) || TrialCoins.DoneToday(cls) >= 0) return;
            if (GateArea(() => StartDailyTraining(cls))) return;
            storyMapOpen = false; villageReturn = WorldUI.Screen.Training; jobTrialRound = -1;
            BeginClassTrial(cls, true);
        }

        void BeginClassTrial(string cls, bool dailyMode = false)
        {
            SaveChoices();
            EndBattle();
            storyStage = null; dungeon = null; daily = null;
            classTrial = cls; classTrialWon = false; classTrialNote = null; trialDaily = dailyMode; watchFound = 0; trialReward = (0, 0);
            prof = Progress.Char(myClass);
            EnsureLoadout();
            var me = new Fighter("我", Skills.Classes[myClass], myLoadout, true);
            prof.ApplyTo(me, Skills.Classes[myClass], myLoadout);
            bool sword = cls == "swordsman";
            dummyMode = sword; dummyDamage = 0f; dummyHits = 0;
            var en = new Fighter(sword ? "靶人" : "出題者", DummyClass, new List<string>(), false);
            en.head = new Vector3(0f, 1.6f, 40f);   // 先放在很遠的地方（靶人出現時再搬過來）
            if (sword) { en.maxHp = 99999; en.hp = en.maxHp; }
            me.maxHp = 9999; me.hp = me.maxHp;   // 試煉不會倒（刺客：被打中直接失敗）
            SyncPlayer(me);
            battle = new Battle(me, en);
            battle.OnEvent += OnBattleEvent; battle.OnSlashLine += ShowSlashLine; battle.OnShotWarn += ShowShotLine;
            ai = null; bossAi = null; bossMode = false; endless = false; coop = false;
            me.damageTakenMul = 1f;
            enemyClassUsed = "dummy";
            if (sword) { enemyRig = CharacterRig.Create(CharacterRig.DummyId); enemyRig.SetVisible(false); }
            lastEnemyFeet = en.Feet; lastEnemyHp = en.hp; enemyDeadShown = false;
            enemyShield = Prim(PrimitiveType.Sphere, new Color(0.4f, 0.7f, 1f, 0.25f));
            trialRng = new System.Random(cls == "assassin" ? 7 : System.Environment.TickCount);   // 閃避訓練固定（出場順序、位置都一樣）
            dailyRng = trialRng;   // FarSpot／WanderDir 用
            dailyCountdownShown = -1f; trialLastMeHp = me.hp;
            swordHits = 0; swordSeenHits = 0; runnerUp = false;
            chantDone = 0; chantCur = null; chantLastFull = null; chantHeard = null; chantMsgUntil = 0f;
            shotHits = 0; shotUp = false;
            dodgeWave = -1; dodgeSide = -1; dodgeFired = 0; dodgeNextAt = 0f; dodgeFwd = Fighter.Flat(me.forward); if (dodgeFwd == Vector3.zero) dodgeFwd = Vector3.forward; dodgeCenter = me.Feet; dodgeWarnUntil = 0f;
            watchOdd = null; watchMarker = null; watchDirs.Clear();
            phase = Phase.Fighting; resultShown = false;
            Say($"{(dailyMode ? "📅 每日訓練・" : "🎓 ")}{ClassTrialName(cls)}：站好、手機對著前方，倒數後開始", 3f);
        }

        void ClearClassTrial()
        {
            if (shotGo) Destroy(shotGo);
            if (shotCore) Destroy(shotCore);
            if (watchMarker) Destroy(watchMarker);
            shotGo = null; shotCore = null; watchMarker = null; watchOdd = null; watchDirs.Clear();
            runnerUp = false; shotUp = false;
            classTrial = null; trialDaily = false;
        }

        /// <summary>每幀（戰鬥中、Battle 更新之後）；試煉結束回傳 true</summary>
        bool UpdateClassTrial()
        {
            if (classTrial == null || battle == null) return false;
            float now = battle.now;
            if (now < ClassTrialStart)
            {
                int sec = Mathf.CeilToInt(ClassTrialStart - now);
                if (now >= CalibTime && sec != (int)dailyCountdownShown) { dailyCountdownShown = sec; Sfx.Play("tick", 0.9f); }
                if (dummyMode) { dummyDamage = 0f; dummyHits = 0; }
                Me.hp = Me.maxHp; trialLastMeHp = Me.hp;
                return false;
            }
            if (dailyCountdownShown > 0f) { dailyCountdownShown = 0f; Sfx.Play("go", 1f); Haptics.Play(Haptics.Kind.Medium); }
            float t = ClassTrialElapsed, dt = Mathf.Min(Time.deltaTime, 0.1f);
            bool done = false;
            switch (classTrial)
            {
                case "swordsman": done = UpdateSwordTrial(now, t, dt); break;
                case "mage": done = UpdateChantTrial(t); break;
                case "archer": done = UpdateShotTrial(t, dt); break;
                case "assassin": done = UpdateDodgeTrial(now, t); break;
                case "priest": done = UpdateWatchTrial(t, dt); break;
            }
            if (!done && t >= ClassTrialTime) done = FinishClassTrial(ClassTrialTimeUpWon(), trialDaily ? "時間到" : "時間到，沒有達標");
            return done;
        }

        /// <summary>時間到的時候算通過嗎（刺客撐到最後＝通過；其他要先達標，達標時已經結束）。每日訓練：做滿就算完成</summary>
        bool ClassTrialTimeUpWon() => trialDaily || classTrial == "assassin";

        /// <summary>每日訓練的分數</summary>
        int TrialScore() => classTrial switch
        {
            "swordsman" => swordHits, "mage" => chantDone, "archer" => shotHits, "priest" => watchFound,
            "assassin" => Mathf.Clamp(Mathf.FloorToInt(ClassTrialElapsed), 0, Mathf.RoundToInt(ClassTrialTime)), _ => 0,
        };

        bool FinishClassTrial(bool won, string note)
        {
            phase = Phase.Over; resultShown = true;
            classTrialWon = won; classTrialNote = note;
            SaveExercise();
            // 場上的東西收掉
            if (runnerUp && enemyRig) enemyRig.SetVisible(false);
            runnerUp = false;
            if (shotGo) shotGo.SetActive(false);
            if (shotCore) shotCore.SetActive(false);
            shotUp = false;
            foreach (var m in battle.minions) m.hp = 0f;
            battle.minionHold = null;
            if (trialDaily) { trialReward = TrialCoins.Record(classTrial, TrialScore()); won = trialReward.coins > 0; classTrialWon = won; }
            else if (won) JobTrialPassed(0);
            Sfx.Play(won ? "win" : "lose"); Haptics.Play(won ? Haptics.Kind.Success : Haptics.Kind.Warning);
            return true;
        }

        // ---------------------------------------------------------------- 劍士：追著打亂跑的靶人
        int swordHits, swordSeenHits; bool runnerUp; Vector3 runnerDir; float runnerTurnAt;

        bool UpdateSwordTrial(float now, float t, float dt)
        {
            var en = En;
            if (!runnerUp)
            {
                if (!FarSpot(out var at)) at = Me.Feet + Fighter.Flat(Me.forward) * 2f;
                en.head = new Vector3(at.x, 1.6f, at.z); lastEnemyFeet = en.Feet;
                en.forward = Fighter.Flat(Me.Feet - at);
                runnerUp = true; runnerTurnAt = 0f;
                if (enemyRig) { enemyRig.SetVisible(true); enemyRig.SetPose(WorldFrame.FromWorld(en.Feet), WorldFrame.DirFromWorld(en.forward)); SpawnFade.Play(enemyRig.gameObject, 0.3f); }
                Sfx.Play("summon", 0.8f);
            }
            // 到處亂跑：每 0.5～1.2 秒換方向，碰到邊界轉回場內
            var pos = en.Feet;
            if (Time.time >= runnerTurnAt || runnerDir.sqrMagnitude < 0.01f) { runnerDir = WanderDir(pos, false); runnerTurnAt = Time.time + 0.5f + (float)trialRng.NextDouble() * 0.7f; }
            var next = pos + runnerDir * RunnerSpeed * dt;
            var ns = WorldFrame.FromWorld(next);
            if (!area.Inside(ns) || area.DistanceToEdge(ns) < 0.5f)
            {
                runnerDir = WanderDir(pos, true); runnerTurnAt = Time.time + 0.8f;
                next = pos + runnerDir * RunnerSpeed * dt; ns = WorldFrame.FromWorld(next);
                if (!area.Inside(ns) || area.DistanceToEdge(ns) < 0.3f) next = pos;
            }
            en.head = new Vector3(next.x, en.head.y, next.z); en.forward = runnerDir;
            // 打中：只算木棍（距離一步之內）
            if (dummyHits > swordSeenHits)
            {
                swordSeenHits = dummyHits;
                if (Battle.FlatDistance(Me.Feet, en.Feet) <= SwordHitRange)
                {
                    swordHits++;
                    Sfx.Play("hit", 1f, 1.1f); Haptics.Play(Haptics.Kind.Success);
                    floaters.Add(new Floater { text = $"⚔ 打中！{swordHits}/{SwordHitsNeed}", color = new Color(1f, 0.85f, 0.4f), born = Time.time, screen = true });
                    if (!trialDaily && swordHits >= SwordHitsNeed) return FinishClassTrial(true, $"{ClassTrialTime - t:0.#} 秒內打中 {SwordHitsNeed} 次");
                }
                else floaters.Add(new Floater { text = "太遠了：要用木棍打中才算", color = new Color(1f, 0.6f, 0.5f), born = Time.time, screen = true });
            }
            return false;
        }

        // ---------------------------------------------------------------- 法師：照畫面唸咒文
        int chantDone; string chantCur, chantLastFull, chantHeard; float chantMsgUntil, chantHeardScore;

        string NextChant()
        {
            int n = 1 + trialRng.Next(3);
            var picked = new List<string>(); string s = "";
            for (int tries = 0; picked.Count < n && tries < 20; tries++)
            {
                var w = ChantWords[trialRng.Next(ChantWords.Length)];
                if (picked.Contains(w) || s.Length + w.Length > 7) continue;
                picked.Add(w); s += w;
            }
            return s.Length >= 2 ? s : ChantWords[6 + trialRng.Next(ChantWords.Length - 6)];
        }

        bool UpdateChantTrial(float t)
        {
            if (chantCur == null) chantCur = NextChant();
            return !trialDaily && chantDone >= ChantNeed && FinishClassTrial(true, $"{ClassTrialTime - t:0.#} 秒內唸對 {ChantNeed} 組");
        }

        /// <summary>辨識器的每段文字：和現在的咒文比讀音（SpeechMatcher：拼音相似度），八成像就算這組唸對</summary>
        bool ClassTrialSpeech(string text)
        {
            if (classTrial != "mage" || phase != Phase.Fighting || chantCur == null || battle == null || battle.now < ClassTrialStart || string.IsNullOrWhiteSpace(text)) return classTrial != null;
            // iOS 同一個辨識任務會把好幾句接在一起：只看上一組唸對之後新增的部分
            string t = chantLastFull != null && text.StartsWith(chantLastFull, System.StringComparison.Ordinal) ? text.Substring(chantLastFull.Length) : text;
            string n = SpeechMatcher.Normalize(t);
            chantHeard = t.Trim(); chantMsgUntil = Time.time + 1.5f;
            if (n.Length == 0) return true;
            float s = SpeechMatcher.Score(n, SpeechMatcher.Normalize(chantCur));
            chantHeardScore = s;
            if (s >= ChantPass)
            {
                chantDone++; chantLastFull = text; chantCur = NextChant();
                Sfx.Play("charged", 0.7f, 1f + 0.03f * chantDone); Haptics.Play(Haptics.Kind.Light);
                floaters.Add(new Floater { text = $"✨ 唸對！{chantDone}/{ChantNeed}（{s * 100:0}%）", color = new Color(0.7f, 0.8f, 1f), born = Time.time, screen = true });
            }
            return true;
        }

        // ---------------------------------------------------------------- 弓箭手：打會動的靶子
        int shotHits, shotPattern; bool shotUp, shotAir; Vector3 shotCenter, shotSide; float shotBorn; GameObject shotGo, shotCore;
        Vector3 shotPos;

        /// <summary>靶子現在的位置：0 左右來回（地面）、1 上下來回（空中）、2 繞圈（空中）、3 左右來回（低空）</summary>
        Vector3 ShotTargetAt(float age)
        {
            float ph = age * 1.6f;
            switch (shotPattern)
            {
                case 0: return shotCenter + shotSide * Mathf.Sin(ph) * 1.3f + Vector3.up * 0.5f;
                case 1: return shotCenter + Vector3.up * (1.3f + 0.9f * Mathf.Sin(ph));
                case 2: return shotCenter + shotSide * Mathf.Cos(ph) * 0.9f + Vector3.up * (1.5f + 0.9f * Mathf.Sin(ph));
                default: return shotCenter + shotSide * Mathf.Sin(ph * 1.3f) * 1.5f + Vector3.up * 0.9f;
            }
        }

        void SpawnShotTarget()
        {
            // 離玩家 6m（場地不夠就到邊界前 0.6m）、隨機方向
            var me = Me.Feet; Vector3 best = Vector3.zero; float bestD = -1f;
            for (int tries = 0; tries < 24; tries++)
            {
                float a = (float)(trialRng.NextDouble() * Mathf.PI * 2.0);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float reach = 0f;
                for (float s = 0.5f; s <= ShotTargetDist; s += 0.25f)
                {
                    var p = WorldFrame.FromWorld(me + d * s);
                    if (!area.Inside(p) || area.DistanceToEdge(p) < 0.6f) break;
                    reach = s;
                }
                if (reach > bestD) { bestD = reach; best = me + d * reach; }
                if (reach >= ShotTargetDist - 0.01f) break;
            }
            if (bestD < 1.5f) best = me + Fighter.Flat(Me.forward) * Mathf.Max(1.5f, bestD);
            shotCenter = new Vector3(best.x, 0f, best.z);
            var toMe = Fighter.Flat(me - shotCenter).normalized;
            shotSide = Vector3.Cross(Vector3.up, toMe).normalized;   // 和你垂直的方向來回
            shotPattern = (shotHits + trialRng.Next(2)) % 4;
            shotAir = shotPattern == 1 || shotPattern == 2;
            shotBorn = Time.time; shotUp = true;
            if (!shotGo)
            {
                shotGo = Prim(PrimitiveType.Sphere, new Color(1f, 0.55f, 0.2f, 0.35f), true);
                shotGo.transform.localScale = Vector3.one * ShotHitRadius * 2f;
                shotCore = Prim(PrimitiveType.Sphere, new Color(1f, 0.9f, 0.4f), false);
                shotCore.transform.localScale = Vector3.one * 0.22f;
            }
            shotGo.SetActive(true); shotCore.SetActive(true);
            shotPos = ShotTargetAt(0f);
            shotGo.transform.position = WorldFrame.FromWorld(shotPos); shotCore.transform.position = shotGo.transform.position;
            SpawnFade.Play(shotGo, 0.25f);
            Sfx.Play("summon", 0.6f, 1.2f);
        }

        bool UpdateShotTrial(float t, float dt)
        {
            if (!shotUp) SpawnShotTarget();
            shotPos = ShotTargetAt(Time.time - shotBorn);
            var sp = WorldFrame.FromWorld(shotPos);
            shotGo.transform.position = sp; shotCore.transform.position = sp;
            shotCore.transform.localScale = Vector3.one * (0.22f + 0.03f * Mathf.Sin(Time.time * 8f));
            // 我丟的石頭碰到靶子
            foreach (var p in battle.projectiles)
            {
                if (!p.alive || p.owner != Me) continue;
                if (Vector3.Distance(p.pos, shotPos) > ShotHitRadius) continue;
                p.alive = false;
                shotHits++;
                SpellFx.Impact("archer", new Color(1f, 0.8f, 0.3f), sp, 0.9f);
                Sfx.Play("hit", 1f, 1.2f); Haptics.Play(Haptics.Kind.Success);
                floaters.Add(new Floater { text = $"🎯 命中！{shotHits}/{ShotHitsNeed}", color = new Color(1f, 0.85f, 0.4f), born = Time.time, screen = true });
                if (!trialDaily && shotHits >= ShotHitsNeed) return FinishClassTrial(true, $"{ClassTrialTime - t:0.#} 秒內打中 {ShotHitsNeed} 個靶子");
                shotUp = false;
                SpawnShotTarget();
                break;
            }
            return false;
        }

        // ---------------------------------------------------------------- 刺客：撐 30 秒不被火球打中
        int dodgeWave, dodgeSide, dodgeFired; float dodgeNextAt, dodgeWarnUntil;
        Vector3 dodgeFwd, dodgeCenter, dodgeOrigin, dodgeDir, dodgeLat; float dodgeHalfW;
        int DodgeBalls => DodgeBalls0 + Mathf.Max(0, dodgeWave);
        float DodgeSpeed => DodgeSpeed0 + DodgeSpeedStep * Mathf.Max(0, dodgeWave);
        /// <summary>場地的四面：0 前、1 右、2 後、3 左（以開始時面向的方向為準）</summary>
        Vector3 SideDir(int side) => side == 0 ? dodgeFwd : side == 1 ? Vector3.Cross(Vector3.up, dodgeFwd).normalized : side == 2 ? -dodgeFwd : -Vector3.Cross(Vector3.up, dodgeFwd).normalized;
        /// <summary>從 from 往 dir 走，場地內最遠到哪（最多 maxD）</summary>
        float ReachIn(Vector3 from, Vector3 dir, float maxD)
        {
            float r = 0f;
            for (float d = 0.25f; d <= maxD; d += 0.25f) { var p = WorldFrame.FromWorld(from + dir * d); if (!area.Inside(p) || area.DistanceToEdge(p) < 0.3f) break; r = d; }
            return r;
        }

        /// <summary>換一面：發射線＝場地那一面的邊（離玩家起點最遠處），先預警</summary>
        void NextDodgeSide(float now)
        {
            dodgeWave++;
            int side; do side = trialRng.Next(4); while (side == dodgeSide && dodgeWave > 0);
            dodgeSide = side;
            dodgeDir = -SideDir(side);   // 火球飛的方向：從那一面往對面
            dodgeLat = Vector3.Cross(Vector3.up, dodgeDir).normalized;
            float reach = Mathf.Max(2.5f, ReachIn(dodgeCenter, SideDir(side), 8f));
            dodgeOrigin = dodgeCenter + SideDir(side) * reach;
            dodgeHalfW = Mathf.Max(0.8f, Mathf.Min(ReachIn(dodgeOrigin - SideDir(side) * 0.3f, dodgeLat, 4f), ReachIn(dodgeOrigin - SideDir(side) * 0.3f, -dodgeLat, 4f)));
            dodgeFired = 0; dodgeNextAt = now + DodgeWarn; dodgeWarnUntil = now + DodgeWarn + 0.6f;
            Sfx.Play("aim_warn", 0.9f); Haptics.Play(Haptics.Kind.Warning);
            floaters.Add(new Floater { text = $"⚠ 第 {dodgeWave + 1} 面：火球從{SideNames[side]}來！{DodgeBalls} 顆・{DodgeSpeed:0.0} m/s", color = new Color(1f, 0.6f, 0.4f), born = Time.time, screen = true });
        }

        void FireDodgeBall(float now)
        {
            var en = En;
            bool low = trialRng.NextDouble() < 0.5;
            float x = ((float)trialRng.NextDouble() * 2f - 1f) * dodgeHalfW;
            var from = dodgeOrigin + dodgeLat * x;
            en.head = new Vector3(from.x, (low ? DodgeLowY : DodgeHighY) + 0.25f, from.z);   // 出手點＝頭的位置往下 0.25
            en.forward = dodgeDir;
            var sk = Skills.All[low ? "x_fire_low" : "x_fire_high"];
            en.charging = sk; en.chargeStart = now - 100f;
            int before = battle.projectiles.Count;
            battle.TryRelease(en, dodgeDir, from + dodgeDir * 10f, out _);
            for (int j = before; j < battle.projectiles.Count; j++) { battle.projectiles[j].speedMul = DodgeSpeed; battle.projectiles[j].hitMul = 1f; }
            en.head = new Vector3(0f, 1.6f, 40f);   // 出題者回到遠處（不會被鎖定）
            dodgeFired++;
            Sfx.Play("shot", 0.5f, low ? 0.8f : 1.1f);
        }

        bool UpdateDodgeTrial(float now, float t)
        {
            var me = Me;
            if (me.hp < trialLastMeHp - 0.01f)
            {
                me.hp = me.maxHp;
                Sfx.Play("hurt", 1f); Haptics.Play(Haptics.Kind.Heavy); Kick(14f, 0.08f);
                return FinishClassTrial(false, $"第 {t:0.#} 秒被火球打中了（第 {dodgeWave + 1} 面）");
            }
            me.hp = me.maxHp; trialLastMeHp = me.hp;
            if (dodgeWave < 0) NextDodgeSide(now);
            // 這一面：每 DodgeGap 秒一顆；最後一顆射完馬上換下一面（先預警，不停頓）
            while (dodgeFired < DodgeBalls && now >= dodgeNextAt)
            {
                FireDodgeBall(now);
                dodgeNextAt += DodgeGap;
                if (dodgeFired >= DodgeBalls) NextDodgeSide(now);
            }
            return false;
        }

        // ---------------------------------------------------------------- 牧師：找出不一樣的那隻
        Fighter watchOdd; GameObject watchMarker;
        readonly Dictionary<Fighter, (Vector3 dir, float turnAt)> watchDirs = new Dictionary<Fighter, (Vector3, float)>();

        bool UpdateWatchTrial(float t, float dt)
        {
            if (watchOdd == null)
            {
                battle.minionHold = m => 1;   // 定住：不追、不攻擊（位置由這裡移動）
                int odd = trialRng.Next(WatchCount);
                for (int i = 0; i < WatchCount; i++)
                {
                    if (!FarSpot(out var at)) at = Me.Feet + Fighter.Flat(Me.forward) * (2f + i * 0.4f);
                    at += new Vector3((float)trialRng.NextDouble() - 0.5f, 0f, (float)trialRng.NextDouble() - 0.5f) * 0.8f;
                    var m = battle.SpawnMinion(at, 0, 0);
                    m.damageTakenMul = 0.05f;   // 打一下只掉一點血（看得出被打中、不會死）
                    m.attackReadyAt = float.MaxValue;
                    if (i == odd) watchOdd = m;
                }
                Sfx.Play("summon", 0.8f);
                return false;
            }
            // 小配件：等牠的造型出來再掛上去（右耳一個暗紅色的小方塊）
            if (!watchMarker && minionGo.TryGetValue(watchOdd, out var rig) && rig)
            {
                watchMarker = Prim(PrimitiveType.Cube, new Color(0.55f, 0.1f, 0.12f), false);
                watchMarker.transform.SetParent(rig.transform, false);
                watchMarker.transform.localPosition = new Vector3(0.11f, 0.72f, 0.04f);
                watchMarker.transform.localScale = Vector3.one * 0.05f;
            }
            // 到處走（不離開場地）
            foreach (var m in battle.minions)
            {
                if (!m.Alive) continue;
                watchDirs.TryGetValue(m, out var w);
                var pos = m.Feet;
                if (Time.time >= w.turnAt || w.dir.sqrMagnitude < 0.01f) w = (WanderDir(pos, false), Time.time + 0.8f + (float)trialRng.NextDouble() * 1.4f);
                var next = pos + w.dir * WatchWalk * dt;
                var ns = WorldFrame.FromWorld(next);
                if (!area.Inside(ns) || area.DistanceToEdge(ns) < 0.5f || Battle.FlatDistance(next, Me.Feet) < 1.2f)
                {
                    w = (WanderDir(pos, true), Time.time + 1f);
                    next = pos + w.dir * WatchWalk * dt; ns = WorldFrame.FromWorld(next);
                    if (!area.Inside(ns) || area.DistanceToEdge(ns) < 0.3f) next = pos;
                }
                m.head = new Vector3(next.x, m.head.y, next.z); m.forward = w.dir;
                watchDirs[m] = w;
            }
            // 打到哪一隻
            foreach (var m in battle.minions)
            {
                if (!m.Alive || m.hp >= m.maxHp - 0.01f) continue;
                bool right = m == watchOdd;
                if (right) SpellFx.Impact("priest", new Color(1f, 0.9f, 0.5f), WorldFrame.FromWorld(m.Feet + Vector3.up * 0.6f), 1f);
                if (trialDaily && right)
                {
                    // 每日訓練：找到了＋1，換一群再找
                    watchFound++;
                    Sfx.Play("charged", 0.8f); Haptics.Play(Haptics.Kind.Success);
                    floaters.Add(new Floater { text = $"👀 找到了！{watchFound} 隻　換下一群", color = new Color(1f, 0.9f, 0.5f), born = Time.time, screen = true });
                    foreach (var x in battle.minions) x.hp = 0f;
                    if (watchMarker) Destroy(watchMarker);
                    watchMarker = null; watchOdd = null; watchDirs.Clear();
                    return false;
                }
                return FinishClassTrial(right, right ? $"{ClassTrialTime - t:0.#} 秒內找出不一樣的那隻" : "打錯了：那隻和別人一樣");
            }
            return false;
        }

        // ---------------------------------------------------------------- HUD、結算
        void DrawClassTrialHud(Rect topR, float W, float H, float lh, int fs, int fsS, int fsL, float pillW)
        {
            var c = Skills.Classes.TryGetValue(classTrial, out var cd) ? cd : null;
            var col = c != null ? c.color : UiKit.Gold;
            UiKit.Card(topR, 0.92f, col, 0.8f);
            float gap = W * 0.015f, ix = topR.x + gap, iw = topR.width - gap * 2, y = topR.y + gap * 0.5f;
            float t = Mathf.Clamp(ClassTrialElapsed, 0f, ClassTrialTime);
            UiKit.Text(new Rect(ix, y, iw * 0.55f, lh), (trialDaily ? "📅 " : "🎓 ") + ClassTrialName(classTrial), fs, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Bar(new Rect(ix + iw * 0.56f, y + lh * 0.15f, iw * 0.44f, lh * 0.7f), 1f - t / ClassTrialTime, col, $"{ClassTrialTime - t:0} 秒", fsS);
            y += lh;
            string prog = classTrial switch
            {
                "swordsman" => $"⚔ 木棍打中 {swordHits}/{SwordHitsNeed}（靶人在場上亂跑，追上去打）",
                "mage" => $"📖 唸對 {chantDone}/{ChantNeed}（讀音像 {ChantPass * 100:0}% 就算）",
                "archer" => $"🎯 打中 {shotHits}/{ShotHitsNeed}（唸「投石」、握拳再張開丟出去）",
                "assassin" => $"👣 第 {dodgeWave + 1} 面・火球從{(dodgeSide >= 0 ? SideNames[dodgeSide] : "…")}來 {dodgeFired}/{DodgeBalls} 顆・{DodgeSpeed:0.0} m/s　低的⬆跳、高的⬇蹲",
                "priest" => $"👀 找出身上多了小東西的那隻小鬼，打牠一下（打錯＝失敗）",
                _ => "",
            };
            UiKit.Text(new Rect(ix, y, iw, lh * 0.9f), prog, fsS, new Color(1f, 0.92f, 0.6f));
            y += lh * 0.9f;
            if (trialDaily)
            {
                var tt = TrialCoins.Tiers(classTrial); int sc = TrialScore();
                UiKit.Text(new Rect(ix, y, iw, lh * 0.8f), $"目前 {TrialCoins.ScoreText(classTrial, sc)}・試煉幣 {TrialCoins.CoinsFor(classTrial, sc)}／3　（{tt[0]}＝1　{tt[1]}＝2　{tt[2]}＝3）", Mathf.RoundToInt(fsS * 0.9f), new Color(1f, 0.85f, 0.5f));
            }
            else UiKit.Text(new Rect(ix, y, iw, lh * 0.8f), ClassTrialDesc(classTrial), Mathf.RoundToInt(fsS * 0.9f), new Color(0.8f, 0.88f, 1f));

            if (battle.now < ClassTrialStart)
            {
                string txt = battle.now < CalibTime ? "站好別動（校正身高）" : $"{Mathf.CeilToInt(ClassTrialStart - battle.now)}";
                UiKit.Text(new Rect(0, H * 0.35f, W, H * 0.15f), txt, battle.now < CalibTime ? fsL : fsL * 3, col, TextAnchor.MiddleCenter, true);
                return;
            }
            if (ClassTrialElapsed < 1f) UiKit.Text(new Rect(0, H * 0.35f, W, H * 0.15f), "開始！", fsL * 2, col, TextAnchor.MiddleCenter, true);
            switch (classTrial)
            {
                case "mage":
                    if (chantCur != null)
                    {
                        // 咒文：畫面中央大字；下面是辨識器聽到的字
                        var cr = new Rect(W * 0.08f, H * 0.3f, W * 0.84f, lh * 3.2f);
                        UiKit.Card(cr, 0.75f, col, 0.9f);
                        UiKit.Text(new Rect(cr.x, cr.y + lh * 0.2f, cr.width, lh * 0.8f), $"第 {chantDone + 1} 組・唸出來：", fsS, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);
                        UiKit.Text(new Rect(cr.x, cr.y + lh * 1f, cr.width, lh * 2f), chantCur, fsL * 2, Color.white, TextAnchor.MiddleCenter, true);
                        if (Time.time < chantMsgUntil && !string.IsNullOrEmpty(chantHeard))
                            UiKit.Pill(cr.yMax + lh * 0.3f, $"🎤 {chantHeard}（{chantHeardScore * 100:0}%）", fsS, chantHeardScore >= ChantPass ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.85f, 0.6f), pillW);
                    }
                    break;
                case "archer":
                    if (shotUp) DrawEdgeArrow(shotPos, $"🎯 {(shotAir ? "空中" : "地面")} {Battle.FlatDistance(Me.Feet, shotPos):F1}m", new Color(1f, 0.8f, 0.3f), W, H, lh, fsS);
                    break;
                case "assassin":
                    if (dodgeSide >= 0)
                    {
                        DrawEdgeArrow(dodgeOrigin + Vector3.up * 1f, $"🔥 火球從{SideNames[dodgeSide]}來", new Color(1f, 0.55f, 0.3f), W, H, lh, fsS);
                        if (battle.now < dodgeWarnUntil) UiKit.Pill(H * 0.62f, $"⚠ 第 {dodgeWave + 1} 面：火球從{SideNames[dodgeSide]}來！低的跳、高的蹲", fsL, new Color(1f, 0.6f, 0.35f), pillW);
                    }
                    break;
                case "swordsman":
                    if (runnerUp) DrawEdgeArrow(En.Feet + Vector3.up * 1.9f, $"🎯 靶人 {Battle.FlatDistance(Me.Feet, En.Feet):F1}m", new Color(1f, 0.85f, 0.4f), W, H, lh, fsS);
                    break;
            }
        }

        void DrawClassTrialResult(float W, float pad, float top, float botY, float gap, float lh, int fs, int fsS)
        {
            var c = Skills.Classes.TryGetValue(classTrial, out var cd) ? cd : null;
            var col = c != null ? c.color : UiKit.Gold;
            var orr = new Rect(pad, top + (botY - top) * .2f, W - pad * 2, (botY - top) * .46f);
            UiKit.Result(orr, classTrialWon);
            float y = orr.y + gap;
            if (trialDaily)
            {
                int sc = TrialScore(); var tt = TrialCoins.Tiers(classTrial);
                UiKit.Text(new Rect(orr.x, y, orr.width, lh * 1.5f), $"📅 {ClassTrialName(classTrial)}　{TrialCoins.ScoreText(classTrial, sc)}", Mathf.RoundToInt(fs * 1.4f), col, TextAnchor.MiddleCenter, true);
                y += lh * 1.6f;
                UiKit.Text(new Rect(orr.x, y, orr.width, lh), trialReward.coins > 0 ? $"🏆 試煉幣 +{trialReward.coins}" + (trialReward.bonus > 0 ? $"　五種都完成 +{trialReward.bonus}！" : "") : "沒有達到門檻，明天再來", fs, Color.white, TextAnchor.MiddleCenter, true);
                y += lh * 1.1f;
                UiKit.Text(new Rect(orr.x, y, orr.width, lh * 0.9f), $"門檻：{tt[0]}＝1 枚　{tt[1]}＝2 枚　{tt[2]}＝3 枚　（持有 {TrialCoins.Coins} 枚）", fsS, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleCenter);
                y += lh;
                UiKit.Text(new Rect(orr.x, y, orr.width, lh * 0.9f), "試煉幣可以在潮騷港酒館老闆娘・凪那裡換特殊物品；每種訓練一天一次", fsS, new Color(0.8f, 1f, 0.85f), TextAnchor.MiddleCenter);
                float bw0 = orr.width * 0.6f, bh0 = lh * 1.8f;
                if (UiKit.Button(new Rect(orr.x + (orr.width - bw0) / 2f, orr.yMax - gap - bh0, bw0, bh0), "🏘 回訓練場", fs, true, new Color(0.45f, 0.9f, 0.7f))) GoVillage();
                return;
            }
            UiKit.Text(new Rect(orr.x, y, orr.width, lh * 1.5f), $"🎓 {ClassTrialName(classTrial)}　{(classTrialWon ? "通過！" : "未通過")}", Mathf.RoundToInt(fs * 1.4f), col, TextAnchor.MiddleCenter, true);
            y += lh * 1.6f;
            UiKit.Text(new Rect(orr.x, y, orr.width, lh), classTrialNote ?? "", fs, Color.white, TextAnchor.MiddleCenter, true);
            y += lh * 1.1f;
            GUI.DrawTexture(new Rect(orr.x + gap, y, lh * 2.2f, lh * 2.2f), Portraits.Get("公會長", "guild"), ScaleMode.ScaleToFit, true);
            UiKit.Text(new Rect(orr.x + gap * 2 + lh * 2.2f, y, orr.width - gap * 3 - lh * 2.2f, lh * 2.2f), $"公會長：「{ClassTrialVerdict(classTrial, classTrialWon)}」", fsS, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleLeft, false, 0.5f);
            y += lh * 2.4f;
            UiKit.Text(new Rect(orr.x, y, orr.width, lh * 0.9f), classTrialWon ? "接下來是實戰試煉：打倒空地上的 5 隻小怪，通過就能轉職" : "回職業工會，再跟公會長對話一次就能重新挑戰", fsS, new Color(0.8f, 1f, 0.85f), TextAnchor.MiddleCenter);
            float bw = (orr.width - gap * 3) / 2f, bh = lh * 1.8f, oby = orr.yMax - gap - bh;
            if (classTrialWon)
            {
                if (UiKit.Button(new Rect(orr.x + gap, oby, bw, bh), "▶ 實戰試煉", fs, true, col)) { ClearClassTrialAndBattle(); StartJobTrial(1); }
                if (UiKit.Button(new Rect(orr.x + gap * 2 + bw, oby, bw, bh), "🏛 回職業工會", fs, false, new Color(0.85f, 0.6f, 1f))) GoGuild();
            }
            else if (UiKit.Button(new Rect(orr.x + gap, oby, orr.width - gap * 2, bh), "🏛 回職業工會", fs, true, new Color(0.85f, 0.6f, 1f))) GoGuild();
        }

        /// <summary>結算後直接接實戰試煉：先把這場收掉</summary>
        void ClearClassTrialAndBattle()
        {
            ShowSetup();
            enemyClass = "story"; daily = null; tutorial = false;
        }
    }
}
