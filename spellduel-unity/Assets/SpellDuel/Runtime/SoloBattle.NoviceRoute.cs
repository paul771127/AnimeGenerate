using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 初心者的成長路線（第一章前半）：
    ///   教學結束 → 至少 5 級；故事 1-1 → 至少 7 級、1-2 → 至少 9 級、1-3 → 10 級（初心者上限，經驗不夠時補到）。
    ///   1-1～1-3：場地縮成 4m×4m（使用者的場地比較小就照原本的）；1-1、1-2 全部是近戰小怪，1-3 才開始有遠程。
    ///   1-3 打完 → 回村子職業工會轉職（試煉：4m×4m 打倒 5 隻小怪）→ 轉職完成才能打 1-4（之後場地與遠程小怪恢復正常）。
    /// 右上角的任務面板（背景透明、可以收起來）列出：導覽、主線、轉職、居民的任務。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>小場地（4m×4m）的關卡</summary>
        static bool SmallArenaStage(string id) => id == "1-1" || id == "1-2" || id == "1-3" || id == JobTrialStageId;
        /// <summary>只有近戰小怪的關卡</summary>
        static bool MeleeOnlyStage(string id) => id == "1-1" || id == "1-2" || id == JobTrialStageId;
        public const float SmallArena = 4f, StoryArena = 6f;
        public const int TutorialLevel = 5;
        /// <summary>初心者通關這一關至少要到幾級（0＝不補）</summary>
        static int StageLevelFloor(string id) => id == "1-1" ? 7 : id == "1-2" ? 9 : id == "1-3" ? Skills.JobLevel : 0;

        /// <summary>把經驗補到某一級（初心者；已經超過就不動）。回傳升了幾級</summary>
        static int TopUpLevel(CharacterProfile p, int level)
        {
            if (p == null || p.cls != Skills.Novice || level <= 0) return 0;
            int need = CharacterProfile.XpForLevel(level) - p.xp;
            return need > 0 ? p.AddXp(need) : 0;
        }

        /// <summary>任務完成的經驗：加上去、存檔、提示（升級時特別說）</summary>
        void GrantQuestXp(int xp, string what)
        {
            if (xp <= 0) return;
            var p = Progress.Char(myClass);
            int ups = p.AddXp(xp); Progress.SaveChar(p);
            Say($"📜 {what}：經驗 +{xp}" + (ups > 0 ? $"　⬆ 升到 Lv{p.level}！（能力點、技能點 +{ups * CharacterProfile.StatPerLevel}／+{ups}）" : ""), 4f);
            if (ups > 0) { Sfx.Play("levelup", 0.8f); Haptics.Play(Haptics.Kind.Success); }
        }

        // ---------------------------------------------------------------- 小場地
        List<Vector3> fullArea; Pose fullOrigin; bool areaShrunk;

        /// <summary>把場地縮成以中心為準的 size×size 正方形（原本的場地比較窄的方向照原本的）</summary>
        void ShrinkArea(float size)
        {
            if (area == null || area.state != PlayArea.State.Done || area.Polygon.Count < 3 || areaShrunk) return;
            var o = area.Origin;
            var rt = o.rotation * Vector3.right; rt.y = 0f; rt.Normalize();
            var fw = o.rotation * Vector3.forward; fw.y = 0f; fw.Normalize();
            float h = size / 2f;
            float hxL = Mathf.Min(h, area.ReachInside(-rt, 0f)), hxR = Mathf.Min(h, area.ReachInside(rt, 0f));
            float hzB = Mathf.Min(h, area.ReachInside(-fw, 0f)), hzF = Mathf.Min(h, area.ReachInside(fw, 0f));
            if (hxL >= h - 0.01f && hxR >= h - 0.01f && hzB >= h - 0.01f && hzF >= h - 0.01f && SmallerThan(area.Polygon, size + 0.3f)) return;   // 本來就差不多 4m 以內
            fullArea = new List<Vector3>(area.Polygon); fullOrigin = o;
            float y = area.Polygon[0].y;
            var c = new Vector3(o.position.x, y, o.position.z);
            var sq = new List<Vector3> { c - rt * hxL - fw * hzB, c + rt * hxR - fw * hzB, c + rt * hxR + fw * hzF, c - rt * hxL + fw * hzF };
            area.SetPolygon(sq, o);
            areaShrunk = true;
            Say($"📏 這一關的場地是 {size:0}m×{size:0}m（地上的新框線）", 3f);
        }

        static bool SmallerThan(List<Vector3> poly, float size)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var q in poly) { minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); minZ = Mathf.Min(minZ, q.z); maxZ = Mathf.Max(maxZ, q.z); }
            return maxX - minX <= size && maxZ - minZ <= size;
        }

        /// <summary>戰鬥結束：場地恢復原本的大小</summary>
        void RestoreArea()
        {
            if (!areaShrunk) return;
            areaShrunk = false;
            if (area != null && fullArea != null && fullArea.Count >= 3) area.SetPolygon(fullArea, fullOrigin);
            fullArea = null;
        }

        /// <summary>故事關卡開打前：初心者路線的場地與小怪設定</summary>
        void ApplyNoviceRoute(StoryStage st)
        {
            if (st == null) return;
            if (SmallArenaStage(st.id)) ShrinkArea(SmallArena);
            else ShrinkArea(StoryArena);   // 其他故事關卡（含進階轉職試煉）：6m×6m
            if (MeleeOnlyStage(st.id) && battle != null) { battle.rangedCountMul = 0f; battle.meleeCountMul = 1f / (1f - Battle.RangedShare); }   // 全部近戰（總數不變）
            // 轉職試煉：剛好 5 隻最弱的小怪（第 1 波：7 隻 ×0.7），不加難度等級
            if (st.id == JobTrialStageId && battle != null) { battle.stageEnemyMul = 0.7f; battle.levelBoost = 0; }
        }

        /// <summary>1-3 打完的初心者：該回村子轉職了</summary>
        bool NeedJobChange
        {
            get { var p = Progress.Char(myClass); return p.cls == Skills.Novice && Story.IsCleared("1-3"); }
        }

        /// <summary>結算「繼續」：初心者打完 1-3 → 回炎之里的職業工會</summary>
        bool JobChangeNext => storyStage != null && storyStage.id == "1-3" && battle != null && battle.stageCleared && NeedJobChange;

        void GoJobChange()
        {
            ShowSetup(); storyMapOpen = true; world.OpenTown(0); world.OpenGuild();
            ShowDialogue(new List<StoryLine>
            {
                new StoryLine { speaker = "公會長", tag = "guild", text = "你已經 10 級了，初心者能學的都學會了。" },
                new StoryLine { speaker = "公會長", tag = "guild", text = "接下來的熔岩巨像不是初心者對付得了的。挑戰轉職試煉：打倒空地上的 5 隻小怪就好，通過後選一個職業吧。" },
            }, null);
        }

        // ---------------------------------------------------------------- 右上角的任務面板
        bool questPanelOpen = true; bool questPanelLoaded;

        /// <summary>現在的任務（標題、進度）：導覽 → 主線 → 轉職 → 居民的任務</summary>
        List<(string title, string detail, Color color)> CurrentQuests()
        {
            var list = new List<(string, string, Color)>();
            var p = Progress.Char(myClass);
            if (GuideActive) { var g = GuideText(); list.Add(("🧭 導覽：" + g.title.Substring(g.title.IndexOf(' ') + 1), null, new Color(0.4f, 1f, 0.75f))); }
            if (p.cls == Skills.Novice && Story.IsCleared("1-3"))
            {
                string d = p.level < Skills.JobLevel ? $"先升到 {Skills.JobLevel} 級（現在 {p.level}）" : p.CanChangeJob ? "試煉通過了：到職業工會選職業" : "到職業工會找公會長：打倒 5 隻小怪";
                list.Add(("🏛 轉職（打 1-4 之前）", d, new Color(0.85f, 0.6f, 1f)));
            }
            // 主線：下一個沒打過的故事關卡
            StoryStage next = null;
            foreach (var s in Story.AllStages()) if (!Story.IsCleared(s.id)) { next = s; break; }
            if (next != null)
            {
                string why = Story.LockReason(next);
                bool ok = Story.IsUnlocked(next);
                list.Add(($"📖 主線 {next.id}　{next.title}", ok ? null : why ?? "先過前一關", new Color(1f, 0.85f, 0.4f)));
            }
            if (p.cls == Skills.Novice)
                foreach (var q in NoviceQuests.All)
                {
                    if (!NoviceQuests.Pending(q, p)) continue;
                    if (!p.noviceQuest.ContainsKey(q.skill) && q.kind != "lesson") { list.Add(($"❗ {q.title}（經驗 +{q.xp}）", $"可以接：到炎之里找{q.npc}", new Color(0.8f, 0.85f, 0.9f))); continue; }
                    list.Add(($"📜 {q.title}（經驗 +{q.xp}）", NoviceQuests.Progress(q, p, Story.IsCleared), NoviceQuests.Complete(q, p, Story.IsCleared) ? new Color(0.5f, 1f, 0.6f) : Color.white));
                }
            return list;
        }

        /// <summary>任務面板：畫面右上（⚙ 下面），背景透明；點標題收起／打開</summary>
        void DrawQuestPanel()
        {
            if (!storyMapOpen || phase != Phase.Setup || dlg != null || charOpen || prepGo != null || infoSkill != null || StoryVideo.Active) return;
            if (!questPanelLoaded) { questPanelLoaded = true; questPanelOpen = PlayerPrefs.GetInt("sd_quest_panel", 1) == 1; }
            float W = Screen.width, H = Screen.height;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.72f);
            float pad = W * 0.03f, w = W * 0.5f, x = W - pad - w;
            float y = Mathf.Max(SettingsMenu.BelowGear, guideBottom + fs * 0.3f);
            var quests = CurrentQuests();
            var hr = new Rect(x + w * 0.45f, y, w * 0.55f, fs * 1.5f);
            if (UiKit.Button(hr, (questPanelOpen ? "📜 任務 ▾" : $"📜 任務（{quests.Count}）▸"), fsS, false, new Color(1f, 0.85f, 0.4f)))
            { questPanelOpen = !questPanelOpen; PlayerPrefs.SetInt("sd_quest_panel", questPanelOpen ? 1 : 0); Sfx.Play("select", 0.4f); }
            if (!questPanelOpen) return;
            y = hr.yMax + fs * 0.2f;
            foreach (var (title, detail, color) in quests)
            {
                // 背景透明：字加陰影（UiKit.Text 的粗體帶陰影）讓 AR 畫面上也看得清楚
                UiKit.Text(new Rect(x, y, w, fs * 1.2f), title, fsS, color, TextAnchor.MiddleRight, true);
                y += fs * 1.15f;
                if (!string.IsNullOrEmpty(detail)) { UiKit.Text(new Rect(x, y, w, fs * 1.1f), detail, Mathf.RoundToInt(fsS * 0.92f), new Color(0.92f, 0.94f, 1f), TextAnchor.MiddleRight, true); y += fs * 1.1f; }
                y += fs * 0.2f;
            }
            if (quests.Count == 0) UiKit.Text(new Rect(x, y, w, fs * 1.2f), "目前沒有任務", fsS, UiKit.Muted, TextAnchor.MiddleRight, true);
        }
    }
}
