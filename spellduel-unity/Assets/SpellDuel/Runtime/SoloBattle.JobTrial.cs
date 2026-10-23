using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 初心者的轉職試煉（10 級，在村子的職業工會找公會長）：三輪，一輪一輪來，全部通過才能選職業。
    ///   第一輪 攻擊訓練：每日挑戰的「輸出挑戰」（打木頭人），拿到銅牌就通過。
    ///   第二輪 閃避訓練：每日挑戰的「閃避挑戰」，拿到銅牌就通過。
    ///   第三輪 實戰：熔岩地帶連打 3 波小怪（第 6～8 波強度），全部打倒就通過。
    /// 試煉的每日挑戰不算進每日紀錄、也不發每日獎勵；通過幾輪記在角色上（CharacterProfile.jobTrial）。
    /// </summary>
    public partial class SoloBattle
    {
        public static readonly string[] JobTrialNames = { "第一輪：攻擊訓練", "第二輪：閃避訓練", "第三輪：實戰" };
        public static readonly string[] JobTrialDesc =
        {
            "打木頭人：限時內造成足夠的傷害（拿到銅牌）",
            "躲開飛來的攻擊：限時內閃過足夠的次數（拿到銅牌）",
            "熔岩地帶連續三波小怪：全部打倒（可以用藥水）",
        };
        const string JobTrialStageId = "jobtrial";
        /// <summary>正在進行的試煉輪（−1＝不是試煉）：前兩輪用每日挑戰</summary>
        int jobTrialRound = -1;
        bool IsJobTrial => storyStage != null && storyStage.id == JobTrialStageId;
        bool InJobTrialDaily => jobTrialRound >= 0 && jobTrialRound < 2 && daily.HasValue;

        /// <summary>開始第 round 輪（0 起算）</summary>
        void StartJobTrial(int round)
        {
            var p = Progress.Char(myClass);
            if (p.cls != Skills.Novice || p.level < Skills.JobLevel || round != p.jobTrial || round >= Skills.JobTrialRounds) return;
            storyMapOpen = false;
            jobTrialRound = round;
            villageReturn = WorldUI.Screen.Guild;
            if (round < 2)
            {
                enemyClass = "daily";
                StartDaily(round == 0 ? DailyChallenge.Kind.Damage : DailyChallenge.Kind.Dodge);
                Say($"🎓 轉職試煉・{JobTrialNames[round]}：{JobTrialDesc[round]}", 4f);
                return;
            }
            var st = new StoryStage { id = JobTrialStageId, title = "🎓 轉職試煉・實戰", theme = 0, fromWave = 6, toWave = 8, xp = 150, desc = JobTrialDesc[2] };
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = "最後一輪。熔岩地帶的小怪會一波接一波來，三波都打倒就算通過。" });
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = "藥水可以用。記住：活著回來，比打倒幾隻怪更重要。" });
            storyStage = st;
            ShowDialogue(st.before, StartStageBattle);
        }

        /// <summary>這一輪通過了：記在角色上</summary>
        void JobTrialPassed(int round)
        {
            var p = Progress.Char(myClass);
            if (p.jobTrial < round + 1) { p.jobTrial = round + 1; Progress.SaveChar(p); }
            Say(p.jobTrial >= Skills.JobTrialRounds ? "🎉 轉職試煉全部通過！回職業工會選擇你的職業" : $"✔ 通過{JobTrialNames[round]}！回職業工會挑戰下一輪", 5f);
        }

        /// <summary>試煉的每日挑戰結束：不記每日紀錄、不發每日獎勵，銅牌以上＝通過</summary>
        void FinishJobTrialDaily(DailyChallenge.Kind k)
        {
            int medal = DailyChallenge.MedalOf(k, dailyScore, dailyLevel);
            dailyResult = (0, 0, medal, false);
            if (medal > 0) JobTrialPassed(jobTrialRound);
        }

        /// <summary>回職業工會</summary>
        void GoGuild()
        {
            jobTrialRound = -1; villageReturn = null;
            ShowSetup();
            enemyClass = "story"; daily = null; tutorial = false; storyStage = null;
            storyMapOpen = true; world.OpenTown(world.town); world.OpenGuild();
        }
    }
}
