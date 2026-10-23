using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 戰鬥勝利的結算窗格（獨立一個視窗，蓋在畫面中央；底下兩個按鈕：回村莊／繼續冒險）：
    ///   標題、評價（S／A／B／C）、通關時間（和這一關的最佳紀錄）、剩餘血量、被打中次數、
    ///   戰利品清單（每件裝備依稀有度上色、金幣、經驗、升級）。
    /// 評價＝時間分（標準時間內＝滿分）×0.5 ＋ 血量分（剩餘血量）×0.3 ＋ 受擊分（被打中越少越好）×0.2。
    /// </summary>
    public partial class SoloBattle
    {
        class VictoryInfo
        {
            public string title, key, note;
            public float time, par, hpFrac;
            public int hits, gold, xp, ups, bestBefore;
            public readonly List<Item> items = new List<Item>();
            public string rank; public float rankScore;
            public bool newRecord;
        }
        VictoryInfo victory;           // 這一場的勝利結算（null＝沒有勝利或已關閉）
        int hitsTaken;                 // 這一場被打中的次數（OnBattleEvent 的 hurt 累計）
        readonly List<Item> lootLog = new List<Item>();   // GrantLoot 放進背包（或換成金幣）的裝備
        int lootGoldLog;

        public static string RankOf(float score) => score >= 0.9f ? "S" : score >= 0.75f ? "A" : score >= 0.55f ? "B" : "C";
        public static float RankScore(float time, float par, float hpFrac, int hits)
        {
            float timeS = Mathf.Clamp01(par / Mathf.Max(1f, time) * 1.0f);
            float hpS = Mathf.Clamp01(hpFrac);
            float hitS = Mathf.Clamp01(1f - hits / 20f);
            return timeS * 0.5f + hpS * 0.3f + hitS * 0.2f;
        }
        static Color RankColor(string r) => r == "S" ? new Color(1f, 0.82f, 0.25f) : r == "A" ? new Color(0.5f, 0.95f, 0.6f) : r == "B" ? new Color(0.5f, 0.8f, 1f) : new Color(0.8f, 0.8f, 0.85f);

        /// <summary>勝利時呼叫（在 GrantLoot、AddXp 之後）：整理成結算窗格</summary>
        void BuildVictory(string title, string key, float par, int xp, int ups, string note = null)
        {
            var me = Me;
            var v = new VictoryInfo
            {
                title = title, key = key, note = note, par = par,
                time = Mathf.Max(0f, battle.now - CalibTime),   // 不算開場站好校正的 2 秒
                hpFrac = me != null && me.maxHp > 0 ? Mathf.Clamp01(me.hp / me.maxHp) : 0f,
                hits = hitsTaken, gold = lootGoldLog, xp = xp, ups = ups,
            };
            v.items.AddRange(lootLog);
            v.rankScore = RankScore(v.time, par, v.hpFrac, v.hits);
            v.rank = RankOf(v.rankScore);
            // 最佳通關時間（每一關各自記錄）
            string bk = "sd_best_time_" + key;
            int prev = 0; try { prev = PlayerPrefs.GetInt(bk, 0); } catch { }
            v.bestBefore = prev;
            int now = Mathf.CeilToInt(v.time);
            if (prev == 0 || now < prev) { v.newRecord = prev > 0; try { PlayerPrefs.SetInt(bk, now); PlayerPrefs.Save(); } catch { } }
            victory = v;
            lootLog.Clear(); lootGoldLog = 0;
        }

        static string Clock(float sec) { int s = Mathf.CeilToInt(sec); return $"{s / 60}:{s % 60:00}"; }

        /// <summary>結算窗格；回傳 true＝窗格還開著（這一幀不要畫原本的結算按鈕）</summary>
        bool DrawVictory(float W, float H, float pad, float top, float botY, float gap, float lh, int fs, int fsS)
        {
            var v = victory;
            if (v == null || dlg != null) return v != null;   // 結尾對話先播完再顯示
            UiKit.Backdrop(new Rect(0, 0, W, H), RankColor(v.rank), 0.55f);
            var r = new Rect(pad, top + (botY - top) * 0.06f, W - pad * 2, (botY - top) * 0.86f);
            UiKit.Card(r, 0.96f, RankColor(v.rank), 0.9f);
            float x = r.x + gap * 1.5f, w = r.width - gap * 3, y = r.y + gap;
            UiKit.Text(new Rect(x, y, w, lh * 1.6f), "🏆 " + v.title, Mathf.RoundToInt(fs * 1.35f), Color.white, TextAnchor.MiddleCenter, true);
            y += lh * 1.7f;

            // 評價（大字）＋三項數據
            float rk = lh * 3.2f;
            float pulse = 1f + 0.04f * Mathf.Sin(Time.time * 3f);
            var rr = new Rect(x, y, rk, rk);
            UiKit.Halo(rr, RankColor(v.rank), 0.8f);
            UiKit.Text(rr, v.rank, Mathf.RoundToInt(fs * 3.2f * pulse), RankColor(v.rank), TextAnchor.MiddleCenter, true);
            float sx = x + rk + gap, sw = w - rk - gap;
            string best = v.newRecord ? "　🎉 新紀錄！" : v.bestBefore > 0 ? $"（最佳 {Clock(v.bestBefore)}）" : "（首次通關）";
            UiKit.Text(new Rect(sx, y, sw, lh), $"⏱ 通關時間　{Clock(v.time)}{best}", fs, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(sx, y + lh, sw, lh), $"❤ 剩餘血量　{Mathf.RoundToInt(v.hpFrac * 100)}%", fsS, new Color(1f, 0.6f, 0.65f));
            UiKit.Text(new Rect(sx, y + lh * 2f, sw, lh), $"💥 被打中　{v.hits} 次　（標準時間 {Clock(v.par)}）", fsS, new Color(0.85f, 0.88f, 1f));
            y += rk + gap * 0.5f;
            UiKit.Text(new Rect(x, y, w, lh), ExerciseLine(), fsS, new Color(1f, 0.7f, 0.45f), TextAnchor.MiddleCenter, true);   // 運動紀錄
            y += lh + gap * 0.5f;
            UiKit.Divider(new Rect(x, y, w, 2f), RankColor(v.rank));
            y += gap;

            // 戰利品
            UiKit.Text(new Rect(x, y, w, lh), "🎁 戰利品", fs, UiKit.Gold, TextAnchor.MiddleLeft, true);
            y += lh;
            string sum = $"💰 {v.gold}　✨ 經驗 {v.xp}" + (v.ups > 0 ? $"　⬆ 升到 Lv{prof?.level}！" : "");
            UiKit.Text(new Rect(x, y, w, lh), sum, fsS, new Color(1f, 0.92f, 0.6f), TextAnchor.MiddleLeft, true);
            y += lh;
            float listBottom = r.yMax - lh * 2.4f - gap;
            if (v.items.Count == 0) UiKit.Text(new Rect(x, y, w, lh), "（這次沒有掉裝備）", fsS, UiKit.Muted);
            int shown = 0;
            foreach (var it in v.items)
            {
                if (y + lh > listBottom - lh) { UiKit.Text(new Rect(x, y, w, lh), $"…還有 {v.items.Count - shown} 件（在背包裡）", fsS, UiKit.Muted); break; }
                var c = Item.RarityColors[it.rarity];
                UiKit.Gem(new Rect(x, y + lh * 0.3f, lh * 0.4f, lh * 0.4f), c);
                UiKit.Text(new Rect(x + lh * 0.6f, y, w - lh * 0.6f, lh), $"[{Item.RarityNames[it.rarity]}] {it.FullName}　Lv{it.ilvl}", fsS, c, TextAnchor.MiddleLeft, it.rarity >= 3);
                y += lh; shown++;
            }
            if (!string.IsNullOrEmpty(v.note)) UiKit.Text(new Rect(x, listBottom - lh * 0.2f, w, lh), v.note, Mathf.RoundToInt(fsS * 0.9f), UiKit.Muted, TextAnchor.MiddleCenter);

            // 兩個選擇：回村莊／繼續冒險（多人連線：回大廳／準備下一場）
            float bw = (r.width - gap * 3) / 2f, bh = lh * 2.2f, by = r.yMax - bh - gap * 0.5f;
            if (UiKit.Button(new Rect(r.x + gap, by, bw, bh), NetMode ? "回大廳" : $"🏘 回{WorldUI.Towns[VictoryTown()].name}", fs)) VictoryGoTown();
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw, by, bw, bh), VictoryContinueLabel(), fs, true, RankColor(v.rank))) VictoryContinue();
            return true;
        }

        /// <summary>勝利後回哪個村莊：故事＝這一關的地區、轉職試煉＝試煉的地區、其他＝目前所在的村莊</summary>
        int VictoryTown()
        {
            int t = storyStage != null && !IsTrial ? storyStage.theme : IsTrial ? Battle.ThemeOf(Advanced.TrialLastWave) : world.town;
            return Mathf.Clamp(t, 0, WorldUI.Towns.Length - 1);
        }

        void VictoryGoTown()
        {
            victory = null;
            if (NetMode) { ShowSetup(); return; }
            int t = VictoryTown();
            ShowSetup(); storyMapOpen = true; world.OpenTown(t);
        }

        string VictoryContinueLabel()
        {
            if (NetMode && dungeon != null) return "▶ 回隊伍畫面";
            if (NetMode) return localReady ? "等待其他人…" : "▶ 準備下一場";
            if (tutorial) return stoneLesson ? "▶ 回炎之里" : villageReturn.HasValue ? "▶ 回訓練場" : "▶ 進入村子";
            if (IsTrial) return "▶ 去職業工會轉職";
            if (JobChangeNext) return "🏛 回村子轉職";
            if (dungeon != null) return "▶ 挑戰其他副本";
            if (storyStage != null)
            {
                var next = NextStage(storyStage);
                if (next != null && Story.IsUnlocked(next)) return next.theme != storyStage.theme ? $"▶ 前往{WorldUI.Towns[Mathf.Clamp(next.theme, 0, WorldUI.Towns.Length - 1)].name}" : $"▶ 下一關：{next.title}";
                return "▶ 故事地圖";
            }
            return "▶ 再來一場";
        }

        /// <summary>繼續冒險：故事＝下一關（換地區＝先到下一個村莊）、副本＝副本清單、試煉＝職業工會、教學＝故事模式、其他＝再來一場</summary>
        void VictoryContinue()
        {
            if (NetMode && dungeon != null) { victory = null; ShowSetup(); return; }   // 連線副本：回隊伍畫面
            if (NetMode) { if (!localReady) { victory = null; SendReady(); } return; }
            victory = null;
            if (tutorial) { AfterTutorial(); return; }
            if (JobChangeNext) { GoJobChange(); return; }
            if (IsTrial) { ShowSetup(); storyMapOpen = true; world.OpenTown(VictoryTown()); world.OpenGuild(); return; }
            if (dungeon != null) { ShowSetup(); storyMapOpen = true; world.OpenTown(world.town); world.screen = WorldUI.Screen.Dungeon; return; }
            if (storyStage != null)
            {
                var st = storyStage; var next = NextStage(st);
                if (next != null && Story.IsUnlocked(next))
                {
                    if (next.theme != st.theme) { ShowSetup(); storyMapOpen = true; world.OpenTown(Mathf.Clamp(next.theme, 0, WorldUI.Towns.Length - 1)); }
                    else { var n2 = next; ShowSetup(); StartStory(n2); }
                }
                else { ShowSetup(); storyMapOpen = true; world.screen = WorldUI.Screen.Map; }
                return;
            }
            StartBattle();
        }
    }
}
