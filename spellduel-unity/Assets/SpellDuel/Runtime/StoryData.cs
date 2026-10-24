using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事模式的資料：章節、關卡、對話。劇本是一個純文字檔（Assets/SpellDuel/Resources/story.txt），
    /// 格式見 spellduel-unity/STORY_FORMAT.md。純 C# 解析（可在 Unity 外測試）。
    /// </summary>
    public class StoryLine
    {
        public string speaker;      // 說話的人（空＝旁白）
        public string tag;          // 括號裡的標記：職業 id（mage…）或任意文字，用來決定名牌顏色
        public string text;
        public string expr;         // 表情（normal／smile／angry／sad／surprised／serious；劇本寫在名字後面：艾琳［笑］）；null＝從台詞猜
    }

    public class StoryStage
    {
        public string id, title, chapterId;
        public int theme;           // 0 熔岩火山、1 森林、2 海上礁石、3 角鬥場、4 黃土山道
        public int fromWave = 1, toWave = 3;   // 這個主題的第幾波到第幾波（1～10；第 10 波＝Boss）
        public int xp = 100;        // 通關獎勵經驗
        public string req;          // 需要先通關的關卡（null＝前一關）
        public string desc = "";
        public readonly List<StoryLine> before = new List<StoryLine>();
        public readonly List<StoryLine> after = new List<StoryLine>();
        /// <summary>實際的波次（三種主題各 10 波：熔岩 1～10、森林 11～20、海上 21～30）</summary>
        public int FirstWave => theme * 10 + Mathf.Clamp(fromWave, 1, 10);
        public int LastWave => theme * 10 + Mathf.Clamp(Mathf.Max(fromWave, toWave), 1, 10);
        public bool HasBoss => toWave >= 10;
    }

    public class StoryChapter
    {
        public string id, title, desc = "";
        public readonly List<StoryStage> stages = new List<StoryStage>();
    }

    public static class Story
    {
        public static readonly List<StoryChapter> chapters = new List<StoryChapter>();
        public static readonly List<string> errors = new List<string>();
        static bool loaded;

        /// <summary>讀取劇本（遊戲裡從 Resources/story 讀；測試時可直接傳文字）</summary>
        public static void Load(string text = null)
        {
            if (loaded && text == null) return;
            if (text == null)
            {
                var ta = Resources.Load<TextAsset>("story");
                text = ta != null ? ta.text : "";
            }
            Parse(text);
            loaded = true;
        }

        public static IEnumerable<StoryStage> AllStages() { foreach (var c in chapters) foreach (var s in c.stages) yield return s; }
        public static StoryStage Find(string id) { foreach (var s in AllStages()) if (s.id == id) return s; return null; }

        internal static int ThemeOf(string v)
        {
            v = v.Trim().ToLowerInvariant();
            if (v == "forest" || v == "森林" || v == "1") return 1;
            if (v == "sea" || v == "海" || v == "海上" || v == "2") return 2;
            if (v == "arena" || v == "角鬥場" || v == "3") return 3;
            if (v == "canyon" || v == "山道" || v == "黃土山道" || v == "4") return 4;
            return 0;
        }

        public static void Parse(string text)
        {
            chapters.Clear(); errors.Clear();
            StoryChapter ch = null; StoryStage st = null; List<StoryLine> lines = null;
            StoryStage prev = null;
            int ln = 0;
            foreach (var raw0 in (text ?? "").Replace("\r", "").Split('\n'))
            {
                ln++;
                var raw = raw0.Trim();
                if (raw.Length == 0 || raw.StartsWith("#") || raw.StartsWith("//")) continue;
                // [chapter 1] 標題 ／ [stage 1-1] 標題
                if (raw.StartsWith("["))
                {
                    int close = raw.IndexOf(']');
                    if (close < 0) { errors.Add($"第 {ln} 行：缺少 ]"); continue; }
                    var head = raw.Substring(1, close - 1).Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    string title = raw.Substring(close + 1).Trim();
                    string kind = head.Length > 0 ? head[0].ToLowerInvariant() : "", id = head.Length > 1 ? head[1].Trim() : "";
                    lines = null;
                    if (kind == "chapter" || kind == "章")
                    {
                        ch = new StoryChapter { id = id, title = title };
                        chapters.Add(ch); st = null;
                    }
                    else if (kind == "stage" || kind == "關")
                    {
                        if (ch == null) { ch = new StoryChapter { id = "1", title = "" }; chapters.Add(ch); }
                        st = new StoryStage { id = id, title = title, chapterId = ch.id, req = prev?.id };
                        ch.stages.Add(st); prev = st;
                    }
                    else errors.Add($"第 {ln} 行：不認得的區塊 [{kind}]");
                    continue;
                }
                // <before> ／ <after>：開始對話
                if (raw.StartsWith("<"))
                {
                    var tag = raw.Trim('<', '>', ' ').ToLowerInvariant();
                    if (st == null) { errors.Add($"第 {ln} 行：<{tag}> 要放在 [stage] 底下"); continue; }
                    lines = tag == "before" || tag == "開場" ? st.before : tag == "after" || tag == "結束" ? st.after : null;
                    if (lines == null) errors.Add($"第 {ln} 行：不認得的 <{tag}>");
                    continue;
                }
                // 對話：名字：台詞（名字後面可以加（標記））；沒有冒號＝旁白
                if (lines != null)
                {
                    lines.Add(ParseLine(raw));
                    continue;
                }
                // 設定：key = value
                int eq = raw.IndexOf('=');
                if (eq > 0)
                {
                    string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                    int hash = v.IndexOf(" #"); if (hash >= 0) v = v.Substring(0, hash).Trim();
                    if (st != null)
                    {
                        switch (k)
                        {
                            case "theme": st.theme = ThemeOf(v); break;
                            case "waves":
                            {
                                var a = v.Split('-');
                                if (int.TryParse(a[0].Trim(), out var f)) { st.fromWave = f; st.toWave = f; }
                                if (a.Length > 1 && int.TryParse(a[1].Trim(), out var t2)) st.toWave = t2;
                                break;
                            }
                            case "xp": int.TryParse(v, out st.xp); break;
                            case "req": st.req = v.Length == 0 || v == "none" ? null : v; break;
                            case "desc": st.desc = v; break;
                            default: errors.Add($"第 {ln} 行：不認得的設定 {k}"); break;
                        }
                    }
                    else if (ch != null && k == "desc") ch.desc = v;
                    continue;
                }
                // 其他文字：章節／關卡簡介
                if (st != null) st.desc = (st.desc.Length > 0 ? st.desc + "\n" : "") + raw;
                else if (ch != null) ch.desc = (ch.desc.Length > 0 ? ch.desc + "\n" : "") + raw;
            }
        }

        public static StoryLine ParseLine(string raw)
        {
            int c = raw.IndexOf('：'); if (c < 0) c = raw.IndexOf(':');
            if (c <= 0 || c > 24) return new StoryLine { speaker = "", text = raw };
            string who = raw.Substring(0, c).Trim(), text = raw.Substring(c + 1).Trim();
            string tag = null, expr = null;
            // 表情：艾琳［笑］／艾琳[smile]
            int eb = who.IndexOfAny(new[] { '［', '[' });
            if (eb > 0)
            {
                int ee = who.IndexOfAny(new[] { '］', ']' }, eb);
                expr = Story2D.NormExpr(who.Substring(eb + 1, (ee > eb ? ee : who.Length) - eb - 1));
                who = (who.Substring(0, eb) + (ee > eb && ee + 1 < who.Length ? who.Substring(ee + 1) : "")).Trim();
            }
            int p = who.IndexOf('（'); if (p < 0) p = who.IndexOf('(');
            if (p > 0)
            {
                int q = who.IndexOfAny(new[] { '）', ')' }, p);
                tag = who.Substring(p + 1, (q > p ? q : who.Length) - p - 1).Trim();
                who = who.Substring(0, p).Trim();
            }
            if (who == "旁白" || who.ToLowerInvariant() == "narrator") who = "";
            return new StoryLine { speaker = who, tag = tag, text = text, expr = expr };
        }

        /// <summary>名牌顏色：標記是職業 id 或職業名稱就用職業色</summary>
        /// <summary>每個地區（章）的等級帶：熔岩 1～10（簡易）、森林 11～20（普通）、海上 21～30（困難）、角鬥場 31～40（艱難）、黃土山道 41～50（極難）；掉落裝備也在這個範圍</summary>
        public static (int lo, int hi) LevelBand(int theme) { int t = Mathf.Clamp(theme, 0, Battle.ThemeCount - 1); return (t * 10 + 1, t * 10 + 10); }
        public static readonly string[] BandNames = { "簡易", "普通", "困難", "艱難", "極難" };
        public static string BandText(int theme) { var (lo, hi) = LevelBand(theme); return $"{BandNames[Mathf.Clamp(theme, 0, BandNames.Length - 1)]}・推薦 Lv{lo}–{hi}"; }

        public static Color TagColor(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return new Color(1f, 0.9f, 0.6f);
            if (Skills.Classes.TryGetValue(tag, out var cd)) return cd.color;
            foreach (var id in Skills.ClassOrder) if (Skills.Classes[id].name == tag) return Skills.Classes[id].color;
            if (tag == "boss" || tag == "敵人" || tag == "反派") return new Color(1f, 0.4f, 0.35f);
            return new Color(0.75f, 0.85f, 1f);
        }

        // ---- 進度（通關的關卡）
        public static Func<string, string> Get = k => "";
        public static Action<string, string> Set = (k, v) => { };
        static HashSet<string> cleared;
        /// <summary>換角色：通關／看過的紀錄重新讀</summary>
        public static void ResetCache() { cleared = null; seen = null; clearedAt = new HashSet<string>[3]; }
        static HashSet<string> Cleared
        {
            get
            {
                if (cleared != null) return cleared;
                cleared = new HashSet<string>();
                foreach (var s in (Get("sd_story_clear") ?? "").Split(',')) if (s.Length > 0) cleared.Add(s);
                return cleared;
            }
        }
        public static bool IsCleared(string id) => Cleared.Contains(id);
        public static bool IsUnlocked(StoryStage s) => (s.req == null || IsCleared(s.req)) && LockReason(s) == null;
        /// <summary>前一關以外的鎖（例如初心者要先上完米菈的投石課才能打 1-1）：null＝沒有</summary>
        public static Func<StoryStage, string> Gate;
        public static string LockReason(StoryStage s) => Gate?.Invoke(s);
        public static void MarkCleared(string id) { if (Cleared.Add(id)) Set("sd_story_clear", string.Join(",", Cleared)); }
        // 普通、困難的通關紀錄（簡單＝上面的 Cleared；以前的紀錄都算簡單）：困難通關也算普通通關
        static HashSet<string>[] clearedAt = new HashSet<string>[3];
        static HashSet<string> ClearedAt(int d)
        {
            d = Mathf.Clamp(d, 1, 2);
            if (clearedAt[d] == null) { clearedAt[d] = new HashSet<string>(); foreach (var s in (Get?.Invoke("sd_story_clear_d" + d) ?? "").Split(',')) if (s.Length > 0) clearedAt[d].Add(s); }
            return clearedAt[d];
        }
        public static bool IsClearedAt(string id, int d) => d <= 0 ? IsCleared(id) : ClearedAt(d).Contains(id);
        public static void MarkCleared(string id, int d)
        {
            MarkCleared(id);
            for (int k = 1; k <= Mathf.Min(2, d); k++) if (ClearedAt(k).Add(id)) Set("sd_story_clear_d" + k, string.Join(",", ClearedAt(k)));
        }
        /// <summary>難度開放了沒：簡單一直開；普通＝所有故事關卡簡單通關；困難＝所有故事關卡普通（或困難）通關</summary>
        public static bool DifficultyOpen(int d)
        {
            if (d <= 0) return true;
            int n = 0;
            foreach (var s in AllStages()) { n++; if (!IsClearedAt(s.id, d - 1)) return false; }
            return n > 0;   // 關卡還沒讀進來：先當作沒開
        }
        /// <summary>現在能選的最高難度</summary>
        public static int MaxOpenDifficulty => DifficultyOpen(2) ? 2 : DifficultyOpen(1) ? 1 : 0;

        // 故事只自動播一次：看過的開場（id）／結尾（id+"!"）記下來，之後要看就按「📖 看故事」
        static HashSet<string> seen;
        static HashSet<string> Seen
        {
            get
            {
                if (seen != null) return seen;
                seen = new HashSet<string>();
                foreach (var s in (Get("sd_story_seen") ?? "").Split(',')) if (s.Length > 0) seen.Add(s);
                return seen;
            }
        }
        public static bool IsSeen(string key) => Seen.Contains(key);
        public static void MarkSeen(string key) { if (Seen.Add(key)) Set("sd_story_seen", string.Join(",", Seen)); }
        public static bool HasStory(StoryStage s) => s != null && ((s.before != null && s.before.Count > 0) || (s.after != null && s.after.Count > 0));

        /// <summary>把台詞裡的 {玩家} 換成現在的職業名稱</summary>
        public static string Fill(string text, string cls) =>
            (text ?? "").Replace("{玩家}", Skills.Classes.TryGetValue(cls, out var cd) ? cd.name : "勇者").Replace("{職業}", Skills.Classes.TryGetValue(cls, out var cd2) ? cd2.name : "");
    }
}
