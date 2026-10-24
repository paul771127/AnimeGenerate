using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 首頁只剩「故事模式」「練習模式」（加上連線的附近／遠距對戰、設定）；其他玩法都搬進村子：
    ///   副本入口（嚮導）、訓練場（每日挑戰、再上一次教學）、討伐告示板（Boss 挑戰）、觀戰席；大地圖上的無盡塔＝無盡模式。
    /// 從村子出發的玩法打完，結算按「回村子」回到原本的設施（villageReturn）。
    /// 第一次玩：選好職業 → 序章對話 → 教學 → 結尾對話 → 進入炎之里。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>從首頁哪一張卡片進來：故事（村子）或練習（對電腦、木頭人）</summary>
        public enum Entry { Story, Practice }
        public Entry entry = Entry.Story;
        /// <summary>從村子的設施出發：打完回到這個畫面（null＝不是從村子出發）</summary>
        WorldUI.Screen? villageReturn;
        bool prologueRun;   // 這次教學是第一次玩的序章接過來的（教學結束接結尾對話、進村子）

        /// <summary>練習模式能選的對手</summary>
        static bool PracticeEnemy(string id) => id == "random" || id == "dummy" || Skills.Classes.ContainsKey(id);

        /// <summary>首頁選了故事／練習（GameRoot 在放場地前呼叫）</summary>
        public void SetEntry(Entry e)
        {
            entry = e; villageReturn = null; prologueRun = false;
            if (e == Entry.Story) enemyClass = "story";
            else if (!PracticeEnemy(enemyClass)) enemyClass = "random";
        }

        /// <summary>故事模式按「進入村子」：第一次玩先播序章＋教學</summary>
        void EnterStory()
        {
            SaveChoices();
            if (IntroPending) { StartPrologue(); return; }
            villageReturn = null;
            storyMapOpen = true; world.OpenTown(world.town);
        }

        /// <summary>回到村子（從村子出發的玩法打完）：回到原本的設施</summary>
        void GoVillage()
        {
            var back = villageReturn;
            villageReturn = null; jobTrialRound = -1;
            ShowSetup();
            enemyClass = "story"; daily = null; tutorial = false;
            storyMapOpen = true; world.OpenTown(world.town);
            if (back.HasValue && back.Value != WorldUI.Screen.Town) world.screen = back.Value;
        }

        // ---------------------------------------------------------------- 村子的設施（WorldUI 呼叫）
        void WireVillage()
        {
            world.GetDifficulty = () => difficulty;
            world.SetDifficulty = d => { difficulty = Mathf.Clamp(d, 0, 2); PlayerPrefs.SetInt("sd_difficulty", difficulty); };
            world.OnStartDaily = k =>
            {
                villageReturn = WorldUI.Screen.Training; enemyClass = "daily"; dailyPick = k; PlayerPrefs.SetInt("sd_daily_pick", k);
                OpenPrep(DailyChallenge.Names[k], false, "每日挑戰不能使用道具", () => { storyMapOpen = false; StartDaily((DailyChallenge.Kind)k); });
            };
            world.OnStartGuide = () => { world.screen = WorldUI.Screen.Town; StartGuide(); };
            world.OnStartTutorial = () => { villageReturn = WorldUI.Screen.Training; enemyClass = "tutorial"; storyMapOpen = false; prologueRun = false; StartTutorial(); };
            world.OnStartBounty = () =>
            {
                villageReturn = WorldUI.Screen.Bounty; enemyClass = "boss";
                OpenPrep("🔥 討伐 Boss", false, "討伐 Boss 不能使用道具", () => { storyMapOpen = false; StartBossBattle(false, false); });
            };
            world.OnStartTower = restart =>
            {
                villageReturn = WorldUI.Screen.Tower; enemyClass = "endless"; towerRestart = restart;
                int from = (restart ? 0 : TowerBlocks) * TowerBlock + 1;
                OpenPrep($"🗼 無盡塔 第 {from}～{from + TowerBlock - 1} 波", true, null, () => { storyMapOpen = false; StartBossBattle(false, true); });
            };
            world.TowerBlocks = () => TowerBlocks;
            world.TowerBest = () => PlayerPrefs.GetInt(CharacterSlots.Key("sd_tower_best"), 0);
            world.OnNetDungeon = remote => RequestNetDungeon?.Invoke(remote);
            world.OnWatch = () => RequestWatch?.Invoke();
            world.EndlessBest = () => PlayerPrefs.GetInt(CharacterSlots.Key("sd_endless_best"), 0);
        }

        /// <summary>村子的副本嚮導：和朋友組隊（GameRoot 斷開單人、改走連線大廳；false＝身邊的朋友、true＝遠方的朋友）</summary>
        public System.Action<bool> RequestNetDungeon;
        /// <summary>村子的觀戰席：進觀戰模式（GameRoot）</summary>
        public System.Action RequestWatch;

        // ---------------------------------------------------------------- 第一次玩：序章 → 教學 → 結尾 → 炎之里
        static List<StoryLine> Lines(params (string who, string tag, string text)[] ls)
        {
            var l = new List<StoryLine>();
            foreach (var (who, tag, text) in ls) l.Add(new StoryLine { speaker = who, tag = tag, text = text });
            return l;
        }

        static readonly List<StoryLine> PrologueLines = Lines(
            ("", "", "黃昏的山路上，晨鐘的方向一片安靜。你背著行囊，走進了炎之里的村口。"),
            ("小鐵", "", "喂！那邊的旅人！天快黑了還往山裡走？最近山上不太平，連礦工都回不來。"),
            ("老焰", "", "小鐵，別嚇著客人。……不過他說的是真的。灰燼裡冒出了不該有的東西。"),
            ("老焰", "", "{玩家}，你身上有咒語的氣息。要上山，先讓村口的教官木頭人看看你的本事吧。"),
            ("小鐵", "", "那個木頭人可是師父做的！會丟火球、會橫掃，還會震地喔。要是被打中……就再來一次嘛。"),
            ("", "", "教官木頭人立在空地中央。你深吸一口氣，握緊了手中的力量。"));

        static readonly List<StoryLine> TutorialOutroLines = Lines(
            ("小鐵", "", "哇……木頭人被打得團團轉！你真的會用咒語啊！"),
            ("老焰", "", "好身手。炎之里歡迎你，{玩家}。村裡有鐵匠鋪、道具店，還有能接副本和懸賞的地方，先去逛逛吧。"),
            ("老焰", "", "準備好了，就從東邊的村口出發。採礦隊……就拜託你了。"),
            ("米菈", "", "等等等等！就靠一根木棍上山？太亂來了。到村子裡來，我先教你一招保命的。"));

        void StartPrologue()
        {
            storyMapOpen = false; villageReturn = null;
            ShowDialogue(PrologueLines, () => { prologueRun = true; enemyClass = "tutorial"; StartTutorial(); });
        }

        /// <summary>教學結束（勝利窗格「繼續」或結算按鈕）：第一次玩接結尾對話、進炎之里；從訓練場來的回訓練場</summary>
        void AfterTutorial()
        {
            victory = null;
            if (stoneLesson) { AfterStoneLesson(); return; }
            if (prologueRun)
            {
                prologueRun = false;
                ShowSetup(); enemyClass = "story"; tutorial = false;
                // 結尾 → 進炎之里 → 新手導覽（第一個任務：米菈的投石課；上完故事 1-1 才會開）
                ShowDialogue(TutorialOutroLines, () =>
                {
                    villageReturn = null; storyMapOpen = true; world.OpenTown(0);
                    StartGuide();   // 村莊新手導覽：走路 → 找米菈上投石課 → 能力值 → 裝備 → 技能 → 設施 → 任務 → 出發（SoloBattle.Guide.cs）
                });
                return;
            }
            if (villageReturn.HasValue || entry == Entry.Story) { GoVillage(); return; }
            enemyClass = "story"; ShowSetup(); EnterStory();
        }
    }
}
