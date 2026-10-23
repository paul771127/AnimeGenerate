using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 村莊新手導覽（新角色教學結束、進炎之里後開始）：一步一步教，做到了自動下一步，說明型的按「下一步」。
    ///   0 移動（方向鍵／晃手機，走 4m）→ 1 解任務（找頭上有❗的米菈上投石課）→ 2 點能力值（升級拿到的能力點加完）
    ///   → 3 穿裝備（米菈送的旅行布衣）→ 4 技能欄與技能點（可以打開整備看看）→ 5 設施介紹（一頁一個）
    ///   → 6 其他任務怎麼看 → 7 出發（從村口去故事 1-1）→ 完成。
    /// 進度記在角色上（sd_guide；−1＝沒開始、99＝完成），隨時可以「略過導覽」；訓練場可以重看。
    /// </summary>
    public partial class SoloBattle
    {
        public const int GuideDone = 99;
        int GuideStep
        {
            get { try { return PlayerPrefs.GetInt(CharacterSlots.Key("sd_guide"), -1); } catch { return -1; } }
            set { try { PlayerPrefs.SetInt(CharacterSlots.Key("sd_guide"), value); PlayerPrefs.Save(); } catch { } }
        }
        int guidePage;
        float guideBottom;        // 導覽卡片的下緣（任務面板排在它下面）            // 設施介紹：第幾頁
        float guideMovedAt;       // 開始移動這一步時已經走過的距離
        bool GuideActive => GuideStep >= 0 && GuideStep < GuideDone;

        const string StarterGearName = "旅行布衣";

        /// <summary>開始（或重看）導覽</summary>
        void StartGuide()
        {
            GuideStep = 0; guidePage = 0; guideMovedAt = townWalk.Moved;
            Say("🧭 新手導覽開始：照著畫面上的提示做", 3f);
        }

        /// <summary>導覽每完成一步給的經驗（每一步只給一次；略過導覽不給）</summary>
        public const int GuideStepXp = 40;

        void GuideNext(int to)
        {
            int from = GuideStep;
            GuideStep = to; guidePage = 0;
            // 照著做完一步：給經驗（重看導覽不重複給）
            int paid = PlayerPrefs.GetInt(CharacterSlots.Key("sd_guide_xp"), -1);
            if (from >= 0 && from < GuideDone && from > paid && (to != GuideDone || from == 7))
            {
                PlayerPrefs.SetInt(CharacterSlots.Key("sd_guide_xp"), from);
                GrantQuestXp(GuideStepXp, "導覽完成一步");
            }
            if (to == 0) guideMovedAt = townWalk.Moved;
            if (to == 3) GiveStarterGear();
            if (to >= GuideDone) { Say("🎉 新手導覽完成！之後可以在訓練場重看", 4f); Sfx.Play("levelup", 0.7f); }
            else { Sfx.Play("charged", 0.7f, 1.2f); Haptics.Play(Haptics.Kind.Success); }
        }

        /// <summary>米菈送的新手裝備（只送一次）</summary>
        void GiveStarterGear()
        {
            var p = Progress.Char(myClass);
            try { if (PlayerPrefs.GetInt(CharacterSlots.Key("sd_starter_gear"), 0) == 1) return; PlayerPrefs.SetInt(CharacterSlots.Key("sd_starter_gear"), 1); } catch { }
            var it = new Item { name = StarterGearName, slot = Slot.Body, rarity = 0, ilvl = 1, classes = "", reqLevel = 1, reqStat = Stat.Vit, reqValue = 0, uid = System.DateTime.UtcNow.Ticks };
            it.mods.Add(new System.Collections.Generic.KeyValuePair<Affix, float>(Affix.Hp, 15f));
            it.mods.Add(new System.Collections.Generic.KeyValuePair<Affix, float>(Affix.Armor, 0.02f));
            Progress.AddToBagOrMail(it, "米菈");
            if (p.equipped[(int)Slot.Body] != null) GuideNextLater = true;   // 身上已經有衣服：這一步自動過
        }
        bool GuideNextLater;

        /// <summary>做到了沒（自動下一步）</summary>
        void GuideTick()
        {
            if (!GuideActive) return;
            var p = Progress.Char(myClass);
            int st = GuideStep;
            switch (st)
            {
                case 0: if (townWalk.Moved - guideMovedAt >= 4f) GuideNext(1); break;
                case 1: if (!NoviceQuests.StoneLessonPending(p)) GuideNext(p.statPoints > 0 ? 2 : 3); break;
                case 2: if (p.statPoints == 0) GuideNext(3); break;
                case 3:
                    var body = p.equipped[(int)Slot.Body];
                    if (GuideNextLater || body != null) { GuideNextLater = false; GuideNext(4); }   // 身上有衣服就算（重看導覽、布衣賣掉了也不會卡住）
                    break;
                case 7: if (Story.IsCleared("1-1") || storyStage != null && storyStage.id == "1-1") GuideNext(GuideDone); break;
            }
        }

        /// <summary>設施介紹的每一頁：標題、說明</summary>
        static readonly (string title, string text)[] GuideFacilities =
        {
            ("⚒ 鐵匠鋪（鐵匠）", "強化裝備 +1～+10，每一級所有屬性 +8%。+5 之前不會失敗，之後越來越難。"),
            ("🧪 道具店（店員）", "買藥水（戰鬥中回血、回魔）、回城捲軸；用不到的裝備可以賣掉換金幣。出發前在「整備」選要帶哪些藥水。"),
            ("🏛 職業工會（公會長）", "10 級時來這裡挑戰轉職試煉（三輪），通過後選擇職業：劍士、獵人、法師、刺客、牧師。之後還有進階職業。"),
            ("🏰 副本入口（嚮導）", "比故事關卡更難的地城：可以選路線、開寶箱、遇到 NPC。可以一個人，也可以和朋友組隊。"),
            ("📅 訓練場（教官）", "每日挑戰（閃避、輸出、跑步、綜合），拿獎牌換獎勵；也可以再上一次教學、重看這個導覽。"),
            ("🔥 討伐告示板（懸賞官）", "單人挑戰 Boss，打倒拿酬勞。"),
            ("👀 觀戰席（播報員）", "輸入房間代碼，看朋友比賽。"),
            ("🌀 傳送陣・🚪 村口", "站上傳送陣 0.5 秒，傳送到去過的城鎮；走出東邊的村口，選這一章的故事關卡。"),
            ("📜 角色・🏦 倉庫・✉ 信箱", "「📜 角色」裡有能力值、技能樹、裝備；裝備頁下面有三個角色共用的倉庫，背包滿了拿到的東西會寄到信箱（7 天內要領）。"),
        };

        /// <summary>這一步的標題、說明、要不要「下一步」按鈕</summary>
        (string title, string text, bool next) GuideText()
        {
            var p = Progress.Char(myClass);
            switch (GuideStep)
            {
                case 0: return ("🕹 移動", $"按左下的方向鍵走路：▲ 前進、▼ 後退、◀▶ 橫移；右下的 ↺↻ 轉身。也可以上下晃動手機原地踏步（往準心方向走）。\n走一走試試（{Mathf.Min(4f, townWalk.Moved - guideMovedAt):0.0}/4 公尺）", false);
                case 1: return ("📜 解任務", "頭上有 ❗ 的居民有任務給你。走到「米菈」面前說「你好」（或舉起手掌揮一揮）跟她說話，接下投石課。\n（右下「📋 清單」也可以直接點居民說話）", false);
                case 2: return ("📊 點能力值", $"升級了！每升一級得到 {CharacterProfile.StatPerLevel} 能力點。按下方「📜 角色」→「📊 能力值」，按 ＋ 加點（每個能力值的效果寫在旁邊）。\n剩下 {p.statPoints} 點", false);
                case 3: return ("🛡 穿裝備", $"米菈送你一件「{StarterGearName}」，放在背包裡。按「📜 角色」→「🛡 裝備」，點背包裡的{StarterGearName} → 穿上。\n裝備有等級和能力值需求；穿不了的會寫原因。", false);
                case 4: return ("✨ 技能", "學會的招式要放進「技能欄」才能在戰鬥中用（唸技能名稱 → 做放招手勢）。每次出發前的「整備」畫面點技能看說明、換上或移除。\n技能點：每升一級得 1 點。到「📜 角色」→「🌳 技能樹」強化學會的招式（木棍揮擊、投石、急救各 5 級，學會才能點）；轉職時會全部退回。", true);
                case 5:
                    var f = GuideFacilities[Mathf.Clamp(guidePage, 0, GuideFacilities.Length - 1)];
                    return ($"🏘 村子的設施 {guidePage + 1}/{GuideFacilities.Length}：{f.title}", f.text + "\n（走近建築，屋頂上會出現招牌；找門口的人說「你好」就能使用）", true);
                case 6: return ("📜 其他任務", "小鐵（打倒 5 隻小怪 → 學「木棍揮擊」）、老焰（通關故事 1-1 → 學「急救」）也有任務。\n接了的任務進度在「📜 角色」→「🌳 技能樹」看；做完回去找他們說話就能學新招。", true);
                case 7: return ("🚪 出發", "準備好了！走到東邊的村口（🚪 第 1 章）出去，選故事 1-1「灰燼中的歸途」。\n戰鬥中：近的敵人用木棍敲（手掌劃一下），遠的唸「投石」；看到地上的預警線就蹲、側跨或跳開。", false);
            }
            return ("", "", false);
        }

        /// <summary>導覽提示卡：村子裡＝完整的卡片（有按鈕）；角色畫面、整備開著時＝上方一條提示（不能按，不擋操作）</summary>
        void DrawGuide()
        {
            guideBottom = 0f;
            if (!GuideActive || !storyMapOpen || phase != Phase.Setup || dlg != null || infoSkill != null || StoryVideo.Active) return;
            if (Event.current.type == EventType.Repaint) GuideTick();
            if (!GuideActive) return;
            var (title, text, next) = GuideText();
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f);
            float pad = W * 0.03f, gap = fs * 0.4f, topY = H - safe.yMax;
            var col = new Color(0.4f, 1f, 0.75f);
            if (charOpen || prepGo != null)
            {
                // 只畫一條提示（最上面），不放按鈕
                var sr = new Rect(pad, topY + 2f, SettingsMenu.RightEdge(topY, topY + fs * 2.2f, W - pad) - pad, fs * 2.2f);
                UiKit.Card(sr, 0.9f, col, 0.9f);
                UiKit.Text(new Rect(sr.x + gap, sr.y, sr.width - gap * 2, sr.height), $"🧭 {title}：" + text.Split('\n')[0], fsS, Color.white, TextAnchor.MiddleLeft, false, 0.6f);
                return;
            }
            float h = fs * 6.6f, y = topY + fs * 4.4f;
            var r = new Rect(pad, y, W - pad * 2, h);
            guideBottom = r.yMax;
            UiKit.Card(r, 0.88f, col, 1f);
            UiKit.Text(new Rect(r.x + gap, r.y + gap * 0.5f, r.width * 0.7f, fs * 1.4f), $"🧭 新手導覽・{title}", fs, col, TextAnchor.MiddleLeft, true);
            float bw = r.width * 0.24f, bh = fs * 1.5f;
            if (UiKit.Button(new Rect(r.xMax - gap - bw, r.y + gap * 0.5f, bw, fs * 1.3f), "略過導覽", Mathf.RoundToInt(fs * 0.7f))) { GuideNext(GuideDone); return; }
            UiKit.Text(new Rect(r.x + gap, r.y + fs * 1.6f, r.width - gap * 2 - (next ? bw + gap : 0f), h - fs * 1.8f), text, fsS, new Color(0.95f, 0.97f, 1f), TextAnchor.UpperLeft, false, 0.6f);
            if (!next) return;
            var nb = new Rect(r.xMax - gap - bw, r.yMax - gap - bh, bw, bh);
            if (GuideStep == 4 && UiKit.Button(new Rect(nb.x, nb.y - bh - gap * 0.5f, bw, bh), "✨ 打開整備", fsS, false, col))
            { OpenPrep("看看技能欄", false, "（導覽：這裡只是看看，按「✔ 好了」回村子）", () => { }); prepLookOnly = true; return; }
            if (UiKit.Button(nb, "下一步 ▶", fsS, true, col))
            {
                if (GuideStep == 5 && guidePage < GuideFacilities.Length - 1) { guidePage++; Sfx.Play("select", 0.5f); }
                else GuideNext(GuideStep + 1);
            }
        }
    }
}
