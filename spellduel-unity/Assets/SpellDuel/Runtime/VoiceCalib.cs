using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 語音校正（⚙ 設定 →「🎙 語音校正」）：
    ///   選一個技能 → 按「🎙 開始唸」→ 唸技能名稱 3 次，每次辨識器聽到的字記成那個技能的別名（VoiceAliases）。
    ///   之後戰鬥中聽到很像別名的字也算唸了那個技能。下面的「試試看」直接顯示現在唸會被當成哪個技能。
    /// 畫在 SettingsMenu 上（最上層、吃掉點擊）；麥克風由 SoloBattle.UpdateMic 開（Listening）。
    /// </summary>
    public static class VoiceCalib
    {
        public static bool Open;
        /// <summary>SoloBattle：可以校正的技能（學會的技能）、手機支不支援語音辨識</summary>
        public static System.Func<IList<string>> Skills;
        public static System.Func<bool> Supported;
        /// <summary>麥克風要開著（選了技能在錄，或在「試試看」）</summary>
        public static bool Listening => Open && (rec != null || testing);
        /// <summary>辨識器的候選名稱（和戰鬥中一樣把技能名稱當提示詞，記到的才是平常戰鬥中會聽到的字）</summary>
        public static IList<string> Candidates => Skills?.Invoke();

        public const int Takes = 3;
        /// <summary>這麼久沒有新的字＝這一次唸完了</summary>
        public const float TakeQuiet = 1.1f;

        static string rec;                    // 正在錄的技能
        static readonly List<string> got = new List<string>();
        static string pending; static float pendingAt;
        static string lastTake, pendingFull;               // 上一次記下時的整段文字（iOS 同一個辨識任務會把好幾句接在一起：只取新增的部分）
        static bool testing; static string testHeard, testHit; static float testAt;
        static string note; static float noteUntil;
        static Vector2 scroll;

        public static void Show() { Open = true; rec = null; testing = false; got.Clear(); pending = null; }
        static void Close() { Open = false; rec = null; testing = false; pending = null; }

        /// <summary>SoloBattle.OnSpeechText：辨識器每段文字（中途、最後）</summary>
        public static void Heard(string text)
        {
            if (!Open || string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            if (rec != null)
            {
                string t = lastTake != null && text.StartsWith(lastTake, System.StringComparison.Ordinal) ? text.Substring(lastTake.Length).Trim() : text;
                if (t.Length == 0) return;
                pending = t; pendingFull = text; pendingAt = Time.unscaledTime;
            }
            else if (testing) { testHeard = text.Trim(); testAt = Time.unscaledTime; }
        }

        /// <summary>SoloBattle.OnSpeechSkill（試試看）：比對結果</summary>
        public static void Recognized(string id) { if (Open && testing) { testHit = id; testAt = Time.unscaledTime; } }

        /// <summary>每幀（SettingsMenu.OnGUI）：一次唸完（安靜一下）就記下來</summary>
        static void Tick()
        {
            if (rec == null || pending == null || Time.unscaledTime - pendingAt < TakeQuiet) return;
            string a = VoiceAliases.Add(rec, pending);
            got.Add(a ?? $"（太短或太長，沒記：{pending}）");
            if (a != null)
            {
                var all = Skills?.Invoke();
                string clash = all != null ? VoiceAliases.ClashWith(rec, a, all) : null;
                if (clash != null && SpellDuel.Skills.All.TryGetValue(clash, out var cs)) Note($"⚠「{a}」和「{cs.name}」很像，可能會辨識錯（可以點 ✕ 刪掉）");
            }
            pending = null; lastTake = pendingFull;
            Sfx.Play("select", 0.6f, 1f + 0.1f * got.Count);
            if (got.Count >= Takes) { rec = null; Sfx.Play("charged", 0.6f); Note("✅ 記好了！可以按下面的「🎯 試試看」確認"); }
        }

        static void Note(string s) { note = s; noteUntil = Time.unscaledTime + 4f; }

        public static void Draw()
        {
            Tick();
            float W = Screen.width, H = Screen.height;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.82f);
            GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            var safe = Screen.safeArea;
            float pad = fs * 0.7f, pw = Mathf.Min(W * 0.94f, fs * 26f);
            var r = new Rect((W - pw) / 2f, H - safe.yMax + fs * 0.8f, pw, safe.height - fs * 1.6f);
            UiKit.Card(r, 0.96f, UiKit.Gold, 0.8f);
            float x = r.x + pad, w = r.width - pad * 2, y = r.y + pad * 0.5f;
            UiKit.Text(new Rect(x, y, w, fs * 1.8f), "🎙 語音校正", Mathf.RoundToInt(fs * 1.15f), UiKit.Gold, TextAnchor.MiddleCenter, true);
            y += fs * 1.9f;
            bool ok = Supported != null && Supported();
            var ids = Skills?.Invoke();
            if (!ok || ids == null || ids.Count == 0)
            {
                UiKit.Text(new Rect(x, y, w, fs * 3f), !ok ? "這支手機不支援語音辨識（或還沒允許麥克風）" : "還沒學會技能", fs, new Color(1f, 0.7f, 0.6f), TextAnchor.MiddleCenter);
                if (UiKit.Button(new Rect(r.center.x - fs * 4f, r.yMax - pad - fs * 2.2f, fs * 8f, fs * 2.2f), "關閉", fs, true, new Color(0.5f, 0.8f, 1f))) Close();
                return;
            }
            UiKit.Text(new Rect(x, y, w, fs * 2.6f), "總是被聽錯的技能：點它、按「🎙 開始唸」，用平常戰鬥的方式唸 3 次。程式會記住你唸的時候辨識器聽到的字，之後聽到一樣的字就知道是這個技能。", fsS, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperLeft, false, 0.6f);
            y += fs * 2.8f;

            // 錄音中／試試看：上面一塊狀態
            float stH = fs * 4.6f;
            var st = new Rect(x, y, w, stH);
            UiKit.Card(st, 0.85f, rec != null ? new Color(1f, 0.45f, 0.4f) : testing ? new Color(0.45f, 0.85f, 1f) : (Color?)null, 0.6f);
            if (rec != null && SpellDuel.Skills.All.TryGetValue(rec, out var rs))
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                UiKit.Text(new Rect(st.x + pad, st.y + fs * 0.2f, st.width - pad * 2, fs * 1.6f), $"🔴 請唸「{rs.name}」（第 {Mathf.Min(got.Count + 1, Takes)}/{Takes} 次）", fs, Color.Lerp(Color.white, new Color(1f, 0.5f, 0.45f), pulse), TextAnchor.MiddleLeft, true);
                string g = got.Count > 0 ? "已記下：" + string.Join("、", got) : "唸完停一下，會自動記下";
                UiKit.Text(new Rect(st.x + pad, st.y + fs * 1.8f, st.width - pad * 2, fs * 1.3f), pending != null ? $"聽到：{pending}" : g, fsS, new Color(0.9f, 0.95f, 1f), TextAnchor.MiddleLeft);
                if (UiKit.Button(new Rect(st.xMax - pad - fs * 5f, st.yMax - fs * 1.9f, fs * 5f, fs * 1.6f), "停止", fsS)) rec = null;
            }
            else if (testing)
            {
                string hit = testHit != null && SpellDuel.Skills.All.TryGetValue(testHit, out var hs) ? $"🎯 辨識成「{hs.name}」" : "🎯 唸一個技能名稱看看…";
                UiKit.Text(new Rect(st.x + pad, st.y + fs * 0.2f, st.width - pad * 2, fs * 1.6f), hit, fs, new Color(0.6f, 0.95f, 1f), TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(st.x + pad, st.y + fs * 1.8f, st.width - pad * 2, fs * 1.3f), testHeard != null ? $"辨識器聽到：{testHeard}" : "", fsS, new Color(0.9f, 0.95f, 1f), TextAnchor.MiddleLeft);
                if (UiKit.Button(new Rect(st.xMax - pad - fs * 5f, st.yMax - fs * 1.9f, fs * 5f, fs * 1.6f), "停止", fsS)) testing = false;
            }
            else
            {
                UiKit.Text(new Rect(st.x + pad, st.y, st.width - pad * 2 - fs * 7f, st.height), "點下面的技能開始校正，\n或按右邊試試看現在辨識得準不準", fsS, UiKit.Muted, TextAnchor.MiddleLeft);
                if (UiKit.Button(new Rect(st.xMax - pad - fs * 6.5f, st.center.y - fs * 1.1f, fs * 6.5f, fs * 2.2f), "🎯 試試看", fs, false, new Color(0.45f, 0.85f, 1f))) { testing = true; testHit = null; testHeard = null; }
            }
            y += stH + fs * 0.4f;
            if (Time.unscaledTime < noteUntil) { UiKit.Text(new Rect(x, y, w, fs * 1.3f), note, fsS, new Color(1f, 0.85f, 0.45f), TextAnchor.MiddleCenter, true); }
            y += fs * 1.4f;

            // 技能清單：名稱、記下的字（點 ✕ 刪）、開始唸
            float bottom = r.yMax - pad - fs * 2.6f, rowH = fs * 3.4f;
            var view = new Rect(x, y, w, bottom - y);
            var content = new Rect(0, 0, w - fs * 0.6f, Mathf.Max(view.height, ids.Count * (rowH + fs * 0.3f)));
            scroll = UiKit.DragScroll(view, scroll, content);
            scroll = GUI.BeginScrollView(view, scroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < ids.Count; i++)
            {
                if (!SpellDuel.Skills.All.TryGetValue(ids[i], out var s)) continue;
                var row = new Rect(0, i * (rowH + fs * 0.3f), content.width, rowH);
                UiKit.Card(row, 0.8f, rec == ids[i] ? new Color(1f, 0.45f, 0.4f) : s.color, 0.5f);
                float isz = rowH * 0.62f;
                GUI.DrawTexture(new Rect(row.x + fs * 0.4f, row.y + (rowH - isz) / 2f, isz, isz), SkillIcons.Get(ids[i]));
                float tx = row.x + fs * 0.7f + isz, tw = row.width - (tx - row.x) - fs * 6.4f;
                UiKit.Text(new Rect(tx, row.y + fs * 0.2f, tw, fs * 1.4f), s.name, fs, Color.white, TextAnchor.MiddleLeft, true);
                var al = VoiceAliases.Of(ids[i]);
                // 記下的字：小標籤，點 ✕ 刪掉
                float cx = tx, cy = row.y + fs * 1.75f, ch = fs * 1.3f;
                if (al.Count == 0) UiKit.Text(new Rect(cx, cy, tw, ch), "還沒校正", Mathf.RoundToInt(fsS * 0.85f), UiKit.Muted, TextAnchor.MiddleLeft);
                foreach (var a in new List<string>(al))
                {
                    float cw = Mathf.Min(tw, fsS * (a.Length + 1.8f));
                    if (cx + cw > tx + tw) break;
                    if (UiKit.Button(new Rect(cx, cy, cw, ch), a + " ✕", Mathf.RoundToInt(fsS * 0.8f), false, s.color)) { VoiceAliases.Remove(ids[i], a); Sfx.Play("click", 0.4f); }
                    cx += cw + fs * 0.2f;
                }
                bool busy = rec != null || testing;
                if (UiKit.Button(new Rect(row.xMax - fs * 6f, row.y + rowH * 0.18f, fs * 5.6f, rowH * 0.64f), rec == ids[i] ? "錄音中…" : "🎙 開始唸", fsS, rec == ids[i], new Color(1f, 0.55f, 0.45f), !busy))
                { rec = ids[i]; got.Clear(); pending = null; lastTake = null; Sfx.Play("select", 0.6f); }
            }
            GUI.EndScrollView();
            if (UiKit.Button(new Rect(r.center.x - fs * 4f, r.yMax - pad - fs * 2.2f, fs * 8f, fs * 2.2f), "完成", fs, true, new Color(0.5f, 0.8f, 1f))) Close();
        }
    }
}
