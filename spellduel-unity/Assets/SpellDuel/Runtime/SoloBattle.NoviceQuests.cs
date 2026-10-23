using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 初心者的招式任務（資料在 Progression.cs 的 NoviceQuests）：在炎之里找居民說話
    ///   沒接 → 接任務；做到一半 → 提醒還差多少；完成 → 學會那一招（技能欄有空就直接放上去）；學會了 → 平常的聊天。
    /// 3D 村子（走過去打招呼）和村子清單（點居民）都會走到這裡。只有初心者有這些任務。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>和居民說話時要不要先處理任務：回傳 true＝這次說話是任務對話（已經開了對話框）</summary>
        bool NoviceQuestTalk(string npcName)
        {
            var q = NoviceQuests.ByNpc(npcName);
            var p = Progress.Char(myClass);
            if (q == null || !NoviceQuests.Pending(q, p)) return false;
            string who = npcName;
            List<StoryLine> L(string[] lines) { var l = new List<StoryLine>(); foreach (var t in lines) l.Add(new StoryLine { speaker = who, tag = "npc", text = t }); return l; }
            Sfx.Play("talk", 0.7f); Haptics.Play(Haptics.Kind.Light);
            if (q.kind == "lesson")
            {
                // 上課型：說完就直接去空地上課（沒上完下次再來會再上一次）
                ShowDialogue(L(p.noviceQuest.ContainsKey(q.skill) ? q.wait : q.offer), () => BeginLesson(q));
                return true;
            }
            if (!p.noviceQuest.ContainsKey(q.skill))
            {
                // 接任務
                ShowDialogue(L(q.offer), () =>
                {
                    p.noviceQuest[q.skill] = 0; Progress.SaveChar(p);
                    Say($"📜 接下任務「{q.title}」：{NoviceQuests.Progress(q, p, Story.IsCleared)}（完成獎勵：經驗 {q.xp}；右上任務面板看進度）", 4f);
                    Sfx.Play("select", 0.8f, 1.2f);
                });
                return true;
            }
            if (!NoviceQuests.Complete(q, p, Story.IsCleared))
            {
                var w = L(q.wait); w.Add(new StoryLine { speaker = "", text = $"任務進度：{NoviceQuests.Progress(q, p, Story.IsCleared)}" });
                ShowDialogue(w, null);
                return true;
            }
            // 完成：學會那一招
            ShowDialogue(L(q.done), () => LearnNoviceSkill(q));
            return true;
        }

        /// <summary>米菈的投石課：先學會投石（放上技能欄），再到空地練習（木頭人站在遠處）</summary>
        void BeginLesson(NoviceQuests.Quest q)
        {
            var p = Progress.Char(myClass);
            p.noviceQuest[q.skill] = 0;
            if (!p.noviceLearned.Contains(q.skill)) p.noviceLearned.Add(q.skill);
            Progress.SaveChar(p);
            if (!myLoadout.Contains(q.skill)) { if (myLoadout.Count < 3) myLoadout.Add(q.skill); else myLoadout[myLoadout.Count - 1] = q.skill; }
            SaveChoices(); PlayerPrefs.Save();
            villageReturn = WorldUI.Screen.Town; enemyClass = "tutorial"; storyMapOpen = false; prologueRun = false;
            StartTutorial(true);
        }

        /// <summary>投石課練習過了：任務完成（故事 1-1 解鎖）</summary>
        /// <summary>投石課練習過了：任務完成（故事 1-1 解鎖）。第一次通過回傳拿到的經驗</summary>
        int StoneLessonDone()
        {
            var p = Progress.Char(myClass);
            foreach (var q in NoviceQuests.All)
                if (q.kind == "lesson" && p.noviceQuest.Remove(q.skill))
                {
                    lessonDoneLines = q;
                    p.AddXp(q.xp); Economy.Gold += LessonGold; lootGoldLog += LessonGold;
                    Progress.SaveChar(p); prof = p;
                    return q.xp;
                }
            return 0;
        }
        const int LessonGold = 50;
        NoviceQuests.Quest lessonDoneLines;   // 練習結束回村子後要說的話（第一次通過）

        /// <summary>投石課結束回村子：第一次通過＝米菈說完成的話</summary>
        void AfterStoneLesson()
        {
            stoneLesson = false; tutorial = false;
            var q = lessonDoneLines; lessonDoneLines = null;
            GoVillage();
            if (q == null) return;
            var l = new List<StoryLine>(); foreach (var t in q.done) l.Add(new StoryLine { speaker = q.npc, tag = "npc", text = t });
            ShowDialogue(l, () => Say("📜 下一步：故事 1-1「灰燼中的歸途」已解鎖（村口 → 地圖）", 5f));
        }

        void LearnNoviceSkill(NoviceQuests.Quest q)
        {
            var p = Progress.Char(myClass);
            p.noviceLearned.Add(q.skill); p.noviceQuest.Remove(q.skill);
            Progress.SaveChar(p);
            GrantQuestXp(q.xp, q.title);
            if (myLoadout.Count < 3 && !myLoadout.Contains(q.skill)) { myLoadout.Add(q.skill); SaveChoices(); PlayerPrefs.Save(); }   // 技能欄有空：直接放上去
            var s = Skills.All[q.skill];
            Say($"✨ 學會了「{s.name}」！{HandGesture.ReleaseLabel(s)} 出招", 5f);
            Sfx.Play("levelup", 0.8f); Haptics.Play(Haptics.Kind.Success);
        }

        /// <summary>戰鬥中任務進度變了：完成的那一刻提醒一次</summary>
        void NoviceQuestTick()
        {
            var p = Progress.Char(myClass);
            foreach (var q in NoviceQuests.All)
                if (p.noviceQuest.TryGetValue(q.skill, out var have) && q.kind != "clear" && q.kind != "lesson" && have >= q.need && !noviceQuestSaid.Contains(q.skill))
                {
                    noviceQuestSaid.Add(q.skill);
                    floaters.Add(new Floater { text = $"📜 任務完成：{q.title}（回炎之里找{q.npc}）", color = new Color(1f, 0.85f, 0.4f), born = Time.time, screen = true });
                    Sfx.Play("charged", 0.6f, 1.3f);
                }
        }
        readonly HashSet<string> noviceQuestSaid = new HashSet<string>();

        /// <summary>村子清單點居民時的任務按鈕文字（沒有任務＝null）</summary>
        string NoviceQuestLabel(string npcName)
        {
            var q = NoviceQuests.ByNpc(npcName);
            var p = Progress.Char(myClass);
            if (q == null || !NoviceQuests.Pending(q, p)) return null;
            if (q.kind == "lesson") return $"🪨 上{q.title}（學投石）";
            if (!p.noviceQuest.ContainsKey(q.skill)) return $"📜 接任務：{q.title}";
            return NoviceQuests.Complete(q, p, Story.IsCleared) ? $"✨ 回報任務：學「{Skills.All[q.skill].name}」" : $"📜 {NoviceQuests.Progress(q, p, Story.IsCleared)}";
        }
    }
}
