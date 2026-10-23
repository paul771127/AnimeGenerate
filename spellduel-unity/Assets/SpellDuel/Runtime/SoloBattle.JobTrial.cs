using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 初心者的轉職試煉（10 級，在村子的職業工會找公會長）：只有一項，不難——
    ///   4m×4m 的場地裡有 5 隻最弱的近戰小怪（第 1 波強度、不加難度等級），全部打倒就通過；可以用藥水，失敗可以重來。
    /// 通過記在角色上（CharacterProfile.jobTrial）。（舊的每日挑戰型試煉程式保留，Skills.JobTrialRounds＝1 時不會用到）
    /// </summary>
    public partial class SoloBattle
    {
        public static readonly string[] JobTrialNames = { "轉職試煉：打倒 5 隻小怪" };
        public static readonly string[] JobTrialDesc =
        {
            "4m×4m 的場地，5 隻最弱的近戰小怪，全部打倒就通過（可以用藥水，失敗可以重來）",
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
            if (round < 2 && Skills.JobTrialRounds > 1)
            {
                enemyClass = "daily";
                StartDaily(round == 0 ? DailyChallenge.Kind.Damage : DailyChallenge.Kind.Dodge);
                Say($"🎓 轉職試煉・{JobTrialNames[round]}：{JobTrialDesc[round]}", 4f);
                return;
            }
            var st = new StoryStage { id = JobTrialStageId, title = "🎓 轉職試煉", theme = 0, fromWave = 1, toWave = 1, xp = 150, desc = JobTrialDesc[0] };
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = "轉職試煉很簡單：工會後面的空地放了 5 隻小怪，全部打倒就算通過。" });
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = "近的用木棍敲，遠的丟石頭，受傷了就喝藥水。去吧！" });
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
