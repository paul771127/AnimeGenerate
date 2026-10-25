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
        class WStep
        {
            public string key, title;
            public Func<string> text;
            public Func<bool> done, skip;
            public bool block = true;
            public Action enter;
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
                    bool next = s.key == "@next" ? UiLock.NextPressed : (s.skip != null && s.skip()) || (s.done != null && s.done());
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
            UiKit.LockKey = pause || cur.key == "@next" ? null : cur.key;
            UiLock.NextButton = !pause && cur.key == "@next";
            UiLock.Block = cur.block;
            UiLock.Title = cur.title; UiLock.Text = cur.text != null ? cur.text() : "";
            UiLock.StepNo = walkStep + 1; UiLock.StepCount = walkSteps.Count;
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
            WStep S(string key, string title, Func<string> text, Func<bool> done, Func<bool> skip = null) { var s = new WStep { key = key, title = title, text = text, done = done, skip = skip }; L.Add(s); return s; }
            // 走路模式（3D 村子）切到清單（設施、居民都在清單裡）
            void ToList(string why) => S("🗺 城鎮圖", "回城鎮圖", () => $"按下方的「🗺 城鎮圖」：回到村子的 2D 地圖，居民和設施都在上面。{why}", () => Town() && !world.walkMode, () => Town() && !world.walkMode);
            void Learn(string npcKey, string npc, string skill)
            {
                string sn = Skills.All[skill].name;
                S("npc:" + npcKey, $"找{npc}", () => $"點「{npc}」（地圖上的名字）跟他說話。有任務的居民會教你新招式。", () => world.TalkingTo != null && world.TalkingTo.Contains(npcKey), () => P().noviceLearned.Contains(skill));
                S("回報任務", $"學「{sn}」", () => $"按「✨ 回報任務」，{npc}會教你「{sn}」（學會的招式會自動放進技能欄）。", () => P().noviceLearned.Contains(skill));
            }
            void Node(string node, string skill)
            {
                string sn = Skills.All[skill].name;
                S("node:" + node, $"選「{sn}強化」", () => $"點技能樹上的「{sn}強化」，下面會出現說明：每一級傷害（或回復量）提高多少。", () => charPanel.SelNode == node, () => P().AvailIn(Skills.Novice) <= 0 || P().Rank(node) > 0);
                S("學習", $"點一點{sn}", () => $"按「學習（1 技能點）」，把技能點投到「{sn}」。每升一級得 1 技能點。（剩 {P().AvailIn(Skills.Novice)} 點）", () => P().Rank(node) > 0, () => P().AvailIn(Skills.Novice) <= 0);
            }
            switch (flow)
            {
                case "v11":
                    S("回炎之里", "回村子", () => "打得好！先回村子：這一戰拿到了經驗和新的任務進度。按「🏘 回炎之里」。", () => Town());
                    ToList("找居民學招式要從這裡點。");
                    Learn("・小鐵", "小鐵", "nv_swing");
                    Learn("・老焰", "老焰", "nv_aid");
                    S("📜 角色", "打開角色", () => "學會新招了！按「📜 角色」：能力值、技能樹、裝備都在這裡。", () => charOpen);
                    S("🌳 技能樹", "技能樹", () => "按上面的「🌳 技能樹」分頁。", () => charOpen && charPanel.tab == 1);
                    Node("nv_t_stone", "nv_stone");
                    Node("nv_t_swing", "nv_swing");
                    Node("nv_t_aid", "nv_aid");
                    S("📊 能力值", "能力值", () => "再來是能力值：按上面的「📊 能力值」分頁。", () => charOpen && charPanel.tab == 0, () => !AnyRaise(P()))
                        .enter = () => walkStatStart = P().statPoints;
                    S("＋", "加能力點", () =>
                    {
                        int need = Mathf.Min(5, walkStatStart), did = walkStatStart - P().statPoints;
                        return $"按能力值旁邊的「＋」加點（至少 {need} 點，加哪一項都可以；每項的效果寫在旁邊）。力量＝木棍更痛、體質＝血更多、敏捷＝普攻集氣更快。\n已經加了 {Mathf.Max(0, did)}/{need} 點";
                    }, () => walkStatStart - P().statPoints >= Mathf.Min(5, walkStatStart) || !AnyRaise(P()))
                        .enter = () => walkStatStart = P().statPoints;
                    S("=完成", "完成", () => "點好了！按下面的「完成」關掉角色畫面。", () => !charOpen);
                    // 出發前買小藥水（一定要買）
                    S("🧪 道具店", "道具店", () => "出門前要準備藥水：按地圖上的「🧪 道具店」。", () => storyMapOpen && world.screen == WorldUI.Screen.Shop)
                        .enter = () => { if (Economy.Gold < SmallPotionPrice) Economy.Gold = SmallPotionPrice; };   // 錢不夠：補到剛好買得起一瓶
                    S("buy:hp_s", "買小藥水", () => "按「小治療藥水」的「💰 購買」。戰鬥中血變少時，說「喝藥水」或比 🤙 就能喝（回復 25% HP）。", () => Economy.Count("hp_s") > walkPotStart)
                        .enter = () => walkPotStart = Economy.Count("hp_s");
                    S("回城鎮", "回城鎮", () => "買好了！按「🏘 回城鎮」。", () => Town());
                    S("大地圖", "前往大地圖", () => "準備出發：按「🗺 大地圖」。", () => storyMapOpen && world.screen == WorldUI.Screen.Map);
                    S("stage:1-2", "選關卡 1-2", () => "點地圖上的「1-2」，下面會出現這一關的說明。", () => world.SelStageId == "1-2");
                    S("⚔ 出發", "出發", () => "按「⚔ 出發」。", () => prepGo != null || !storyMapOpen);
                    S("⚔ 出發", "整備", () => "這是出發前的整備：技能欄、要帶的藥水。這次直接按「⚔ 出發」！", () => prepGo == null && (!storyMapOpen || phase == Phase.Fighting));
                    break;
                case "job":
                    S("回村子轉職", "回村子轉職", () => "你已經 10 級了！初心者的路走完了，回村子的職業工會轉職。按「🏛 回村子轉職」。", () => storyMapOpen && phase == Phase.Setup && world.screen == WorldUI.Screen.Guild);
                    S("挑戰|工會|回炎之里|🗺 城鎮圖", "轉職試煉", () => P().CanChangeJob ? "通過試煉了！回到「🏛 職業工會」選職業。" : "按「挑戰」開始轉職試煉：打倒空地上的 5 隻小怪就通過（失敗可以再挑戰）。", () => P().CanChangeJob && storyMapOpen && phase == Phase.Setup && world.screen == WorldUI.Screen.Guild);
                    S("選這個|再按一次", "選職業", () => "選一個職業（這一步你自己決定）：點職業卡片可以看介紹；按「選這個」，再按一次確定。選定就不能換了。", () => P().cls != Skills.Novice).block = false;
                    break;
                case "smith":
                {
                    long uid = GiftUid;
                    bool Have() => FindItem(uid) != null;
                    S("回城鎮", "回村子", () => "收下禮物了！先回到村子：按「🏘 回城鎮」。", () => Town(), () => Town());
                    ToList("鐵匠鋪也在清單裡。");
                    S("📜 角色", "打開角色", () => "穿上小鐵送的武器：按「📜 角色」。", () => charOpen, () => !Have());
                    S("🛡 裝備", "裝備", () => "按上面的「🛡 裝備」分頁。上面是身上穿的、下面是背包。", () => charOpen && charPanel.tab == 2, () => !Have());
                    S("item:" + uid, "選武器", () => "在背包裡點小鐵送你的武器（藍色的名字），右邊會比較它和身上的裝備。", () => charPanel.SelItem != null && charPanel.SelItem.uid == uid, () => !Have() || P().equipped[(int)Slot.Weapon]?.uid == uid)
                        .enter = () => charPanel.FocusUid = uid;
                    S("=裝備", "穿上", () => "按「裝備」穿上它！穿不上的話，按鈕會寫原因（等級、能力值不夠）。", () => P().equipped[(int)Slot.Weapon]?.uid == uid, () => !Have());
                    S("=完成", "完成", () => "穿好了！按「完成」關掉角色畫面。", () => !charOpen);
                    S("⚒ 鐵匠鋪", "鐵匠鋪", () => "最後是強化：按「⚒ 鐵匠鋪」。", () => storyMapOpen && world.screen == WorldUI.Screen.Smith, () => !Have());
                    S("item:" + uid, "選要強化的裝備", () => "點剛穿上的武器（👕＝身上穿著的）。", () => world.SelItem != null && world.SelItem.uid == uid, () => !Have())
                        .enter = () => world.FocusUid = uid;
                    S("⚒ 強化（", "強化", () => "按「⚒ 強化」：付金幣讓武器變強（每一級主屬性 +8%，其他屬性與機率不變）。", () => (FindItem(uid)?.plus ?? 1) > 0, () => !Have() || Economy.Gold < Economy.EnhanceCost(FindItem(uid)));
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
