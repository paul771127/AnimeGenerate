using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 新手指引（一步一步按按鈕）：畫面變暗、只有要按的那一顆按鈕在閃，其他地方都按不到（UiLock.cs、UiKit.LockKey），
    /// 說明框寫現在要按什麼、為什麼。進度記在角色上（sd_walk＝哪一段、sd_walk_step＝第幾步），關掉遊戲再開會接著做。
    ///   v11（1-1 第一次通關）：回村子 → 找小鐵學木棍揮擊、找老焰學急救 → 角色 → 技能樹點投石、木棍揮擊、急救
    ///        → 能力值至少點 5 點 → 回大地圖 → 選 1-2 → 出發（1-2 自由戰鬥；衝鋒獸的教學在戰鬥裡，SoloBattle.BattleTutorial.cs）。
    ///   job（1-3 第一次通關、10 級）：回村子轉職 → 職業工會挑戰轉職試煉 → 選職業（自己決定，不鎖）。
    ///   smith（轉職完成、拿到小鐵的武器和 500 金幣，SoloBattle.StarterGift.cs）：角色 → 裝備 → 穿上武器 → 鐵匠鋪 → 強化 → 新手指引完成。
    /// 對話、劇情影片、戰鬥中不鎖（照常操作）。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>指引每一步所在的畫面</summary>
        enum WCtx { Any, Result, Town, Char, Shop, Smith, Guild, Map, Prep }

        class WStep
        {
            public string key, title;
            public Func<string> text;
            public Func<bool> done, skip;
            public bool block = true;
            public Action enter;
            /// <summary>這一步要在哪個畫面做（不在那裡：先把玩家帶過去；帶不過去就不鎖，只提示）</summary>
            public WCtx ctx = WCtx.Any;
            /// <summary>角色畫面的分頁（-1＝不管）、要先選中的裝備 uid（0＝不管）</summary>
            public int tab = -1; public long sel;
        }

        string walkFlow; int walkStep; List<WStep> walkSteps; int walkStatStart, walkPotStart;
        static int SmallPotionPrice => Economy.Find("hp_s")?.price ?? 30;
        bool walkLoaded;
        bool WalkActive { get { LoadWalk(); return walkSteps != null; } }

        static string WalkGet(string k, string def) { try { return PlayerPrefs.GetString(CharacterSlots.Key(k), def); } catch { return def; } }
        static void WalkSet(string k, string v) { try { PlayerPrefs.SetString(CharacterSlots.Key(k), v); PlayerPrefs.Save(); } catch { } }
        long GiftUid { get { long.TryParse(WalkGet("sd_gift_uid", "0"), out var u); return u; } set => WalkSet("sd_gift_uid", value.ToString()); }

        void LoadWalk()
        {
            if (walkLoaded) return;
            walkLoaded = true;
            string f = WalkGet("sd_walk", "");
            if (string.IsNullOrEmpty(f)) { walkFlow = null; walkSteps = null; return; }
            // 老角色被誤送了轉職禮物、卡在「穿武器→強化」的新手指引裡：直接結束（不再鎖按鈕）
            var wp = Progress.Char(myClass);
            if (f == "smith" && wp != null && wp.level > RetroGiftMaxLevel) { walkFlow = f; EndWalk(false); return; }
            int.TryParse(WalkGet("sd_walk_step", "0"), out var st);
            walkFlow = f; walkSteps = BuildWalk(f); walkStep = Mathf.Clamp(st, 0, walkSteps != null ? walkSteps.Count : 0);
            if (walkSteps == null || walkStep >= walkSteps.Count) EndWalk(false);
            else walkSteps[walkStep].enter?.Invoke();
        }
        /// <summary>換角色時重新讀</summary>
        void ReloadWalk() { walkLoaded = false; walkSteps = null; walkFlow = null; }

        void StartWalk(string flow)
        {
            walkLoaded = true;
            walkFlow = flow; walkSteps = BuildWalk(flow); walkStep = 0;
            WalkSet("sd_walk", flow); WalkSet("sd_walk_step", "0");
            if (walkSteps != null && walkSteps.Count > 0) walkSteps[0].enter?.Invoke();
            if (GuideActive) GuideStep = GuideDone;   // 舊的村子導覽：直接結束（改成這個一步一步的指引）
        }

        void EndWalk(bool finished)
        {
            string f = walkFlow;
            walkFlow = null; walkSteps = null; walkStep = 0;
            WalkSet("sd_walk", ""); WalkSet("sd_walk_step", "0");
            UiKit.LockKey = null; UiLock.NextButton = false;
            if (!finished) return;
            if (f == "v11") Say("⚔ 1-2 開始！自由戰鬥到勝利吧（遇到強敵時會再教你）", 4f);
            else if (f == "smith")
            {
                WalkSet("sd_onboard_done", "1");
                Say("🎉 新手指引全部完成！接下來的冒險就交給你了", 6f);
                Sfx.Play("levelup", 0.9f); Haptics.Play(Haptics.Kind.Success);
            }
        }

        /// <summary>每一幀（SoloBattle.DrawGUI 一開始）：檢查這一步做到了沒、設定要鎖的按鈕與說明</summary>
        void TickWalk()
        {
            UiLock.Ensure();
            UiLock.Beat = Time.frameCount;
            if (!WalkActive) { UiKit.LockKey = null; UiLock.NextButton = false; return; }
            var e = Event.current;
            if (e.type == EventType.Layout)
            {
                // 做到了（或這一步不用做）：往下一步；一幀最多跳 6 步
                for (int guard = 0; guard < 6 && walkSteps != null && walkStep < walkSteps.Count; guard++)
                {
                    var s = walkSteps[walkStep];
                    bool next = s.key.StartsWith("@") ? UiLock.NextPressed : (s.skip != null && s.skip()) || (s.done != null && s.done());
                    if (!next) break;
                    UiLock.NextPressed = false;
                    walkStep++;
                    WalkSet("sd_walk_step", walkStep.ToString());
                    if (walkStep >= walkSteps.Count) { EndWalk(true); return; }
                    Sfx.Play("charged", 0.6f, 1.3f); Haptics.Play(Haptics.Kind.Light);
                    walkSteps[walkStep].enter?.Invoke();
                }
            }
            if (walkSteps == null || walkStep >= walkSteps.Count) return;
            var cur = walkSteps[walkStep];
            bool pause = phase == Phase.Fighting || dlg != null || StoryVideo.Active || infoSkill != null || phase == Phase.Hidden;
            UiLock.Hint = null;
            string key = cur.key, title = cur.title, text = cur.text != null ? cur.text() : "";
            if (!pause && cur.key != "@next" && !InCtx(cur.ctx))
            {
                // 不在這一步的畫面（例如中途跳出遊戲、重新進來）：先帶回去；帶不回去就不鎖，只提示
                var (rk, rt) = RouteTo(cur.ctx);
                if (rk != null) { key = rk; title = "先回到要去的地方"; text = rt + $"\n（接著：{cur.title}）"; }
                else { key = null; UiLock.Hint = $"🧭 新手指引：{CtxName(cur.ctx)}，指引會接著繼續（{cur.title}）"; }
            }
            if (!pause && key == cur.key && charOpen && cur.tab >= 0)
            {
                // 角色畫面不在這一步的分頁（例如關掉再打開，回到了「能力值」）：先切分頁；要選的裝備沒選中：先選
                string[] tabKeys = { "📊 能力值", "🌳 技能樹", "🛡 裝備" };
                if (charPanel.tab != cur.tab) { key = tabKeys[cur.tab]; title = "先切到分頁"; text = $"先按上面的「{tabKeys[cur.tab]}」分頁。\n（接著：{cur.title}）"; }
                else if (cur.sel != 0 && (charPanel.SelItem == null || charPanel.SelItem.uid != cur.sel) && FindItem(cur.sel) != null)
                {
                    key = "item:" + cur.sel; title = "先選裝備"; text = $"先在背包裡點被框起來的那一件。\n（接著：{cur.title}）";
                    if (charPanel.SelItem == null && e.type == EventType.Layout) charPanel.FocusUid = cur.sel;   // 翻到那一件所在的背包頁
                }
            }
            UiKit.LockKey = pause || key == "@next" ? null : key;
            UiLock.NextButton = !pause && key != null && key.StartsWith("@");   // @next：說明；@show:xxx：圈起來介紹，按「下一步」
            UiLock.Block = cur.block || key != cur.key;
            UiLock.Title = title; UiLock.Text = text;
            UiLock.StepNo = walkStep + 1; UiLock.StepCount = walkSteps.Count;
        }

        bool InCtx(WCtx c)
        {
            bool village = storyMapOpen && phase == Phase.Setup;
            switch (c)
            {
                case WCtx.Result: return phase == Phase.Over;
                case WCtx.Char: return charOpen;
                case WCtx.Prep: return prepGo != null;
                case WCtx.Town: return village && !charOpen && prepGo == null && world.screen == WorldUI.Screen.Town;
                case WCtx.Shop: return village && !charOpen && world.screen == WorldUI.Screen.Shop;
                case WCtx.Smith: return village && !charOpen && world.screen == WorldUI.Screen.Smith;
                case WCtx.Guild: return (village && !charOpen && world.screen == WorldUI.Screen.Guild) || phase == Phase.Over;   // 轉職試煉打完的結算畫面也算（回職業工會）
                case WCtx.Map: return village && !charOpen && prepGo == null && world.screen == WorldUI.Screen.Map;
                default: return true;
            }
        }
        static string CtxName(WCtx c) => c switch
        {
            WCtx.Result => "先打完這一關", WCtx.Char => "先打開「📜 角色」", WCtx.Prep => "先到出發前的整備畫面",
            WCtx.Shop => "先到村子的「🧪 道具店」", WCtx.Smith => "先到村子的「⚒ 鐵匠鋪」", WCtx.Guild => "先到村子的「🏛 職業工會」",
            WCtx.Map => "先到「🗺 大地圖」", _ => "先回到村子",
        };

        /// <summary>現在在哪、要去哪：這一刻該按哪個按鈕（null＝帶不過去）</summary>
        (string key, string text) RouteTo(WCtx want)
        {
            bool village = storyMapOpen && phase == Phase.Setup;
            if (charOpen) return ("=完成", "先按「完成」關掉角色畫面。");
            if (prepGo != null) return ("取消", "先按「取消」回去。");
            if (!village) return (null, null);
            var sc = world.screen;
            if (sc == WorldUI.Screen.Town)
            {
                if (world.walkMode) return ("🗺 城鎮圖", "先按「🗺 城鎮圖」回到村子的地圖。");
                switch (want)
                {
                    case WCtx.Char: return ("📜 角色", "先按「📜 角色」打開角色畫面。");
                    case WCtx.Shop: return ("🧪 道具店", "先按「🧪 道具店」。");
                    case WCtx.Smith: return ("⚒ 鐵匠鋪", "先按「⚒ 鐵匠鋪」。");
                    case WCtx.Guild: return ("🏛 職業工會", "先按「🏛 職業工會」。");
                    case WCtx.Map: case WCtx.Prep: return ("大地圖", "先按「🗺 回大地圖」。");
                }
                return (null, null);
            }
            if (sc == WorldUI.Screen.Map)
            {
                if (want == WCtx.Map || want == WCtx.Prep) return (null, null);
                return ("town:" + Mathf.Max(0, world.town), "先點地圖上的城鎮，回到村子。");
            }
            // 其他設施畫面：回城鎮（傳送陣是「留在…」）
            return ("🏘 回|🏘 留|🗺 回大地圖", "先按下面的「🏘 回…」回到村子。");
        }

        /// <summary>還有沒有能加的能力值（點數不夠下一點的花費也算沒有：免得卡在這一步）</summary>
        static bool AnyRaise(CharacterProfile p) { foreach (Stat s in Enum.GetValues(typeof(Stat))) if ((int)s < StatInfo.Count && p.CanRaise(s)) return true; return false; }

        Item FindItem(long uid)
        {
            if (uid == 0) return null;
            var p = Progress.Char(myClass);
            foreach (var it in p.equipped) if (it != null && it.uid == uid) return it;
            foreach (var it in Progress.Bag) if (it.uid == uid) return it;
            return null;
        }

        List<WStep> BuildWalk(string flow)
        {
            CharacterProfile P() => Progress.Char(myClass);
            bool Town() => storyMapOpen && phase == Phase.Setup && world.screen == WorldUI.Screen.Town;
            var L = new List<WStep>();
            WCtx at = WCtx.Any;   // 接下來加的步驟在哪個畫面做
            WStep S(string key, string title, Func<string> text, Func<bool> done, Func<bool> skip = null) { var s = new WStep { key = key, title = title, text = text, done = done, skip = skip, ctx = at }; L.Add(s); return s; }
            bool InVillage() => storyMapOpen && phase == Phase.Setup;
            // 角色畫面的步驟：要在哪個分頁、要先選中哪一件（重新打開角色畫面會回到「能力值」分頁、選的裝備也沒了：先帶回去）
            WStep T(int tab, WStep s, long sel = 0) { s.tab = tab; s.sel = sel; return s; }
            // 走路模式（3D 村子）切到清單（設施、居民都在清單裡）
            void ToList(string why) => S("🗺 城鎮圖", "回城鎮圖", () => $"按下方的「🗺 城鎮圖」：回到村子的 2D 地圖，居民和設施都在上面。{why}", () => Town() && !world.walkMode, () => Town() && !world.walkMode);
            void Learn(string npcKey, string npc, string skill)
            {
                string sn = Skills.All[skill].name;
                S(npcKey, $"找{npc}", () => $"點被框起來的「{npc}」（點他本人或下面的名字都可以）。他頭上有 ❗，表示任務做完了，可以回報、學新招式。", () => world.TalkingTo != null && world.TalkingTo.Contains(npcKey), () => P().noviceLearned.Contains(skill));
                S("回報任務", $"學「{sn}」", () => $"對話框下面出現黃色的「✨ 回報任務」按鈕：按它，{npc}會教你「{sn}」（學會的招式會自動放進技能欄）。", () => P().noviceLearned.Contains(skill));
            }
            void Node(string node, string skill)
            {
                string sn = Skills.All[skill].name;
                T(1, S("node:" + node, $"選「{sn}強化」", () => $"點技能樹上的「{sn}強化」，下面會出現說明：每一級傷害（或回復量）提高多少。", () => charPanel.SelNode == node, () => P().AvailIn(Skills.Novice) <= 0 || P().Rank(node) > 0));
                T(1, S("學習", $"點一點{sn}", () => $"按「學習（1 技能點）」，把技能點投到「{sn}」。每升一級得 1 技能點。（剩 {P().AvailIn(Skills.Novice)} 點）", () => P().Rank(node) > 0, () => P().AvailIn(Skills.Novice) <= 0));
            }
            // 圈起來介紹一個名牌／按鈕（只能按「下一步」）
            void Show(string label, string title, string text) => S("@show:" + label, title, () => text, null);
            switch (flow)
            {
                case "intro":
                {
                    // 第一次進村子（教學結束後）：認識居民 → 米菈的投石課 → 能力值 → 穿新手裝備 → 認識每一棟建築 → 出發 1-1
                    GiveStarterGear();   // 旅行布衣（只送一次）：先放進背包，指引才知道要框哪一件
                    long starter() { long.TryParse(WalkGet("sd_starter_uid", "0"), out var u); return u; }
                    long su = starter();
                    bool StoneLeft() => NoviceQuests.StoneLessonPending(P());
                    at = WCtx.Town;
                    S("@next", "炎之里", () => "歡迎來到炎之里！這是村子的地圖：上面有居民（人物＋名字）和建築（名牌）。\n接下來一步一步帶你認識村子：畫面上被框起來、在閃的地方才能點，其他地方暫時不能點。按「下一步 ▶」。", null);
                    S("・老焰", "找村長", () => "先跟村長打招呼：點被框起來的「老焰」（白頭髮、紅色長袍的老爺爺）。點他本人或下面的名字都可以。", () => world.TalkingTo != null && world.TalkingTo.Contains("・老焰"));
                    S("@next", "對話框", () => "畫面下方出現了對話框：左邊是頭像，右邊是他說的話。\n・再點同一個人一次 → 換下一句話\n・有任務的居民，對話框下面會多一個黃色的任務按鈕\n按「下一步 ▶」。", null);
                    S("・小鐵", "認識小鐵", () => "再來認識見習鐵匠「小鐵」（戴護目鏡、紅色圍巾），他在鐵匠鋪門口。點被框起來的小鐵。", () => world.TalkingTo != null && world.TalkingTo.Contains("・小鐵"));
                    S("・米菈", "找米菈", () => "旅行商人「米菈」頭上有 ❗：表示她有任務給你。點被框起來的米菈。", () => world.TalkingTo != null && world.TalkingTo.Contains("・米菈"), () => !StoneLeft());
                    S("投石課", "接任務：投石課", () => "對話框下面的黃色按鈕「🪨 上米菈的投石課」就是任務：按它，米菈會教你遠距離攻擊「投石」，接著到空地上練習。", () => !StoneLeft(), () => !StoneLeft());
                    // 能力值（升級拿到的點數全部加完）
                    S("📜 角色", "打開角色", () => "練習完升級了，拿到了能力點！按下面被框起來的「📜 角色／裝備」。", () => charOpen, () => !AnyRaise(P()));
                    at = WCtx.Char;
                    S("📊 能力值", "能力值分頁", () => "按上面的「📊 能力值」分頁。", () => charOpen && charPanel.tab == 0, () => !AnyRaise(P()));
                    T(0, S("＋", "加能力點", () => $"按能力值旁邊的「＋」加點，把點數全部加完（剩 {P().statPoints} 點）。\n力量＝攻擊更痛、體質＝血更多、敏捷＝普攻集氣更快、智力＝技能更強、精神＝魔力回復更快、幸運＝爆擊與掉寶。", () => !AnyRaise(P())));
                    // 穿新手裝備
                    S("🛡 裝備", "裝備分頁", () => "米菈送你一件「旅行布衣」，放在背包裡。按上面的「🛡 裝備」分頁。", () => charOpen && charPanel.tab == 2, () => P().equipped[(int)Slot.Body] != null);
                    T(2, S("item:" + su, "選衣服", () => "在下面的背包裡，點被框起來的「旅行布衣」。右邊會顯示它的數值、和身上的比較。", () => charPanel.SelItem != null && charPanel.SelItem.uid == starter(), () => P().equipped[(int)Slot.Body] != null || FindItem(starter()) == null))
                        .enter = () => charPanel.FocusUid = su;
                    T(2, S("=裝備", "穿上", () => "按衣服說明下面、閃爍的「裝備」按鈕穿上它（不是最上面的「🛡 裝備」分頁）。裝備有等級和能力值需求；穿不上的話，按鈕會寫原因。", () => P().equipped[(int)Slot.Body] != null, () => FindItem(starter()) == null), su);
                    S("=完成", "完成", () => "好了！按下面的「完成」關掉角色畫面。", () => !charOpen);
                    // 認識每一棟建築（圈起來介紹，不能點進去）
                    at = WCtx.Town;
                    Show("⚒ 鐵匠鋪", "⚒ 鐵匠鋪", "鐵匠鋪：花金幣強化裝備，每 +1 主屬性 +8%。+5 以前一定成功，之後越來越難（失敗只扣金幣）。");
                    Show("🧪 道具店", "🧪 道具店", "道具店：買藥水（戰鬥中說「喝藥水」或比 🤙 喝）、回城捲軸；用不到的裝備可以賣掉換金幣。");
                    Show("🏛 職業工會", "🏛 職業工會", "職業工會：10 級時來這裡挑戰轉職試煉，通過後選職業（劍士、獵人、法師、刺客、牧師）。");
                    Show("🏰 副本入口", "🏰 副本入口", "副本入口：比故事更難的地城，可以選路線、開寶箱；一個人或和朋友組隊都可以。");
                    Show("📅 訓練場", "📅 訓練場", "訓練場：每日挑戰（閃避、輸出、跑步、綜合）拿獎牌換獎勵；也可以再上一次教學。");
                    Show("🔥 討伐告示板", "🔥 討伐告示板", "討伐告示板：單人挑戰 Boss，打倒拿懸賞金。");
                    Show("👀 觀戰席", "👀 觀戰席", "觀戰席：輸入房間代碼，看朋友的比賽。");
                    Show("🌀 傳送陣", "🌀 傳送陣", "傳送陣：傳送到去過的城鎮。");
                    Show("🚪 村口", "🚪 村口", "村口：這一章的故事關卡從這裡出發（也可以從大地圖選）。");
                    Show("📜 角色", "📜 角色／裝備", "角色／裝備：能力值、技能樹、裝備、背包、信箱、倉庫都在這裡。升級後記得來加點。");
                    Show("🚶 逛街", "🚶 逛街", "逛街：進到 3D 的村子裡走走（用方向鍵或晃手機走路）。想回到這張地圖時，按「🗺 城鎮圖」。");
                    Show("大地圖", "🗺 回大地圖", "大地圖：選故事關卡、到其他城鎮、無盡塔。");
                    // 出發 1-1
                    S("大地圖", "前往大地圖", () => "村子都認識了！準備第一場冒險：按「🗺 回大地圖」。", () => storyMapOpen && world.screen == WorldUI.Screen.Map, () => Story.IsCleared("1-1"));
                    at = WCtx.Map;
                    S("stage:1-1", "選關卡 1-1", () => "點地圖上被框起來的「1-1」（灰燼中的歸途），下面會出現這一關的說明。", () => world.SelStageId == "1-1", () => Story.IsCleared("1-1"));
                    S("⚔ 出發", "出發", () => "按「⚔ 出發」。", () => prepGo != null || !storyMapOpen, () => Story.IsCleared("1-1"));
                    at = WCtx.Prep;
                    S("⚔ 出發", "整備", () => "這是出發前的整備：上面是技能欄，下面是要帶的藥水。這次直接按「⚔ 出發」！", () => prepGo == null && (!storyMapOpen || phase == Phase.Fighting), () => Story.IsCleared("1-1"));
                    break;
                }
                case "v11":
                    at = WCtx.Result;
                    S("回炎之里", "回村子", () => "打得好！先回村子：這一戰拿到了經驗和新的任務進度。按「🏘 回炎之里」。", () => Town(), InVillage);   // 重新進遊戲時已經在村子：跳過
                    at = WCtx.Town;
                    ToList("找居民學招式要從這裡點。");
                    Learn("・小鐵", "小鐵", "nv_swing");
                    Learn("・老焰", "老焰", "nv_aid");
                    S("📜 角色", "打開角色", () => "學會新招了！按「📜 角色」：能力值、技能樹、裝備都在這裡。", () => charOpen);
                    at = WCtx.Char;
                    S("🌳 技能樹", "技能樹", () => "按上面的「🌳 技能樹」分頁。", () => charOpen && charPanel.tab == 1);
                    Node("nv_t_stone", "nv_stone");
                    Node("nv_t_swing", "nv_swing");
                    Node("nv_t_aid", "nv_aid");
                    S("📊 能力值", "能力值", () => "再來是能力值：按上面的「📊 能力值」分頁。", () => charOpen && charPanel.tab == 0, () => !AnyRaise(P()))
                        .enter = () => walkStatStart = P().statPoints;
                    T(0, S("＋", "加能力點", () =>
                    {
                        int need = Mathf.Min(5, walkStatStart), did = walkStatStart - P().statPoints;
                        return $"按能力值旁邊的「＋」加點（至少 {need} 點，加哪一項都可以；每項的效果寫在旁邊）。力量＝木棍更痛、體質＝血更多、敏捷＝普攻集氣更快。\n已經加了 {Mathf.Max(0, did)}/{need} 點";
                    }, () => walkStatStart - P().statPoints >= Mathf.Min(5, walkStatStart) || !AnyRaise(P())))
                        .enter = () => walkStatStart = P().statPoints;
                    S("=完成", "完成", () => "點好了！按下面的「完成」關掉角色畫面。", () => !charOpen);
                    // 出發前買小藥水（一定要買）
                    at = WCtx.Town;
                    S("🧪 道具店", "道具店", () => "出門前要準備藥水：按地圖上的「🧪 道具店」。", () => storyMapOpen && world.screen == WorldUI.Screen.Shop)
                        .enter = () => { if (Economy.Gold < SmallPotionPrice) Economy.Gold = SmallPotionPrice; };   // 錢不夠：補到剛好買得起一瓶
                    at = WCtx.Shop;
                    S("buy:hp_s", "買小藥水", () => "按「小治療藥水」的「💰 購買」。戰鬥中血變少時，說「喝藥水」或比 🤙 就能喝（回復 25% HP）。", () => Economy.Count("hp_s") > walkPotStart)
                        .enter = () => walkPotStart = Economy.Count("hp_s");
                    S("回城鎮", "回城鎮", () => "買好了！按「🏘 回城鎮」。", () => Town());
                    at = WCtx.Town;
                    S("大地圖", "前往大地圖", () => "準備出發：按「🗺 大地圖」。", () => storyMapOpen && world.screen == WorldUI.Screen.Map);
                    at = WCtx.Map;
                    S("stage:1-2", "選關卡 1-2", () => "點地圖上的「1-2」，下面會出現這一關的說明。", () => world.SelStageId == "1-2");
                    S("⚔ 出發", "出發", () => "按「⚔ 出發」。", () => prepGo != null || !storyMapOpen);
                    at = WCtx.Prep;
                    S("⚔ 出發", "整備", () => "這是出發前的整備：技能欄、要帶的藥水。這次直接按「⚔ 出發」！", () => prepGo == null && (!storyMapOpen || phase == Phase.Fighting));
                    break;
                case "job":
                    at = WCtx.Result;
                    S("回村子轉職", "回村子轉職", () => "你已經 10 級了！初心者的路走完了，回村子的職業工會轉職。按「🏛 回村子轉職」。", () => storyMapOpen && phase == Phase.Setup && world.screen == WorldUI.Screen.Guild, InVillage);
                    at = WCtx.Guild;
                    S("挑戰|選這條路|繼續試煉|重選|介紹|返回|工會|回炎之里|🗺 城鎮圖", "轉職試煉", () => P().CanChangeJob ? "兩關都通過了！回到「🏛 職業工會」正式轉職。" : P().jobTrial > 0 ? "專業試煉過了！按「挑戰」進實戰試煉：打倒空地上的 5 隻小怪。" : "先選你想走的職業（點「📖 介紹」可以先看），按「選這條路」開始那個職業的專業試煉（30 秒；失敗可以再來）。", () => P().CanChangeJob && storyMapOpen && phase == Phase.Setup && world.screen == WorldUI.Screen.Guild);
                    S("成為|再按一次", "正式轉職", () => "按「✨ 成為…」，再按一次確定，就正式轉職了。", () => P().cls != Skills.Novice).block = false;
                    break;
                case "smith":
                {
                    long uid = GiftUid;
                    bool Have() => FindItem(uid) != null;
                    at = WCtx.Guild;
                    S("回城鎮", "回村子", () => "收下禮物了！先回到村子：按「🏘 回城鎮」。", () => Town(), () => Town() || !(InVillage() && world.screen == WorldUI.Screen.Guild));   // 不在工會（例如重新進遊戲）：跳過，下一步會帶回村子
                    at = WCtx.Town;
                    ToList("鐵匠鋪也在清單裡。");
                    S("📜 角色", "打開角色", () => "穿上小鐵送的武器：按「📜 角色」。", () => charOpen, () => !Have());
                    at = WCtx.Char;
                    S("🛡 裝備", "裝備", () => "按上面的「🛡 裝備」分頁。上面是身上穿的、下面是背包。", () => charOpen && charPanel.tab == 2, () => !Have());
                    T(2, S("item:" + uid, "選武器", () => "在背包裡點小鐵送你的武器（藍色的名字），右邊會比較它和身上的裝備。", () => charPanel.SelItem != null && charPanel.SelItem.uid == uid, () => !Have() || P().equipped[(int)Slot.Weapon]?.uid == uid))
                        .enter = () => charPanel.FocusUid = uid;
                    T(2, S("=裝備", "穿上", () => "按武器說明下面、閃爍的「裝備」按鈕穿上它（不是最上面的「🛡 裝備」分頁）。穿不上的話，按鈕會寫原因（等級、能力值不夠）。", () => P().equipped[(int)Slot.Weapon]?.uid == uid, () => !Have()), uid);
                    S("=完成", "完成", () => "穿好了！按「完成」關掉角色畫面。", () => !charOpen);
                    at = WCtx.Town;
                    S("⚒ 鐵匠鋪", "鐵匠鋪", () => "最後是強化：按「⚒ 鐵匠鋪」。", () => storyMapOpen && world.screen == WorldUI.Screen.Smith, () => !Have());
                    at = WCtx.Smith;
                    S("item:" + uid, "選要強化的裝備", () => "點剛穿上的武器（👕＝身上穿著的）。", () => world.SelItem != null && world.SelItem.uid == uid, () => !Have())
                        .enter = () => world.FocusUid = uid;
                    S("⚒ 強化（", "強化", () => "按「⚒ 強化」：付金幣讓武器變強（每一級主屬性 +8%，其他屬性與機率不變）。", () => (FindItem(uid)?.plus ?? 1) > 0, () => !Have() || Economy.Gold < Economy.EnhanceCost(FindItem(uid)));
                    at = WCtx.Any;
                    S("@next", "強化的規則", () => "+5 之前保證成功；再往上會有機會失敗（失敗不會壞掉，只是金幣花掉了）。打怪拿到的金幣，記得回來強化裝備！", null);
                    break;
                }
                default: return null;
            }
            return L;
        }

        /// <summary>故事關卡第一次通關（ShowResult）：開始對應的新手指引</summary>
        void OnStoryFirstClear(StoryStage st)
        {
            var p = Progress.Char(myClass);
            if (p.cls != Skills.Novice || NetMode || coop) return;
            if (st.id == "1-1")
            {
                // 小鐵（打倒 5 隻小怪）、老焰（通關 1-1）的任務：在 1-1 都做到了，回村子直接回報
                foreach (var q in NoviceQuests.All)
                    if (q.kind != "lesson" && !p.noviceLearned.Contains(q.skill)) p.noviceQuest[q.skill] = q.kind == "kill" ? q.need : 0;
                Progress.SaveChar(p);
                StartWalk("v11");
            }
            else if (st.id == "1-3" && NeedJobChange) StartWalk("job");
        }
    }
}
