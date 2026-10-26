using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 初心者的轉職試煉（10 級，在村子的職業工會找公會長）——先選職業，再過兩關：
    ///   第 1 關・專業試煉：依選的職業出題（劍術／念咒／射擊／閃避／觀察，30 秒；SoloBattle.ClassTrial.cs），失敗就回工會重新對話再來。
    ///   第 2 關・實戰試煉：4m×4m 的場地裡有 5 隻最弱的近戰小怪（第 1 波強度、不加難度等級），全部打倒就通過；可以用藥水，失敗可以重來。
    /// 都通過才能在工會正式轉職（只能轉成試煉時選的職業；換職業＝試煉重來）。進度記在角色上（CharacterProfile.jobTrial／jobTrialCls）。
    /// </summary>
    public partial class SoloBattle
    {
        public static readonly string[] JobTrialNames = { "專業試煉", "實戰試煉：打倒 5 隻小怪" };
        public static readonly string[] JobTrialDesc =
        {
            "依你選的職業出題（30 秒）：劍術、念咒、射擊、閃避或觀察",
            "4m×4m 的場地，5 隻最弱的近戰小怪，全部打倒就通過（可以用藥水，失敗可以重來）",
        };
        const string JobTrialStageId = "jobtrial";
        /// <summary>正在進行的試煉輪（−1＝不是試煉）</summary>
        int jobTrialRound = -1;
        bool IsJobTrial => storyStage != null && storyStage.id == JobTrialStageId;
        bool InJobTrialDaily => false;   // 舊的每日挑戰型試煉已經不用

        /// <summary>職業工會：選了想走的職業 → 記在角色上、開始（或繼續）試煉。換職業＝試煉重來</summary>
        void PickJobTrial(string cls)
        {
            var p = Progress.Char(myClass);
            if (p.cls != Skills.Novice || p.level < Skills.JobLevel || !Skills.Classes.ContainsKey(cls)) return;
            if (p.jobTrialCls != cls) { p.jobTrial = 0; p.jobTrialCls = cls; Progress.SaveChar(p); }
            StartJobTrial(Mathf.Min(p.jobTrial, Skills.JobTrialRounds - 1));
        }

        /// <summary>開始第 round 關（0 起算）</summary>
        void StartJobTrial(int round)
        {
            var p = Progress.Char(myClass);
            if (p.cls != Skills.Novice || p.level < Skills.JobLevel || round != p.jobTrial || round >= Skills.JobTrialRounds || string.IsNullOrEmpty(p.jobTrialCls)) return;
            storyMapOpen = false;
            jobTrialRound = round;
            villageReturn = WorldUI.Screen.Guild;
            if (round == 0) { StartClassTrial(p.jobTrialCls); return; }
            string cn = Skills.Classes[p.jobTrialCls].name;
            var st = new StoryStage { id = JobTrialStageId, title = "🎓 實戰試煉", theme = 0, fromWave = 1, toWave = 1, xp = 150, desc = JobTrialDesc[1] };
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = $"{SoloBattle.ClassTrialName(p.jobTrialCls)}過了，接下來是實戰。工會後面的空地放了 5 隻小怪，全部打倒就算通過。" });
            st.before.Add(new StoryLine { speaker = "公會長", tag = "guild", text = $"近的用木棍敲，遠的丟石頭，受傷了就喝藥水。打完回來找我，我就正式讓你成為{cn}。去吧！" });
            storyStage = st;
            ShowDialogue(st.before, StartStageBattle);
        }

        /// <summary>這一關通過了：記在角色上</summary>
        void JobTrialPassed(int round)
        {
            var p = Progress.Char(myClass);
            if (p.jobTrial < round + 1) { p.jobTrial = round + 1; Progress.SaveChar(p); }
            string cn = p.jobTrialCls != null && Skills.Classes.TryGetValue(p.jobTrialCls, out var c) ? c.name : "新職業";
            Say(p.jobTrial >= Skills.JobTrialRounds ? $"🎉 轉職試煉全部通過！回職業工會正式成為{cn}" : $"✔ 通過{JobTrialNames[round]}！接下來是{JobTrialNames[round + 1]}", 5f);
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
