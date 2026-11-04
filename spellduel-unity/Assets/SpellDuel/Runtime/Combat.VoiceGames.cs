using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 聲控小遊戲（使用者要求：用聲音玩、背誦有用的知識）。兩種特殊地點（副本 event = recite／quiz；故事的波次目標也會抽到）：
    ///
    ///   recite 背誦試煉：電腦唸一句有用的知識（畫面同時顯示），唸完字就消失，玩家要背出來（語音辨識比對，八成像就過）。
    ///          背對 count 句（預設 3）就過關；15 秒內沒背出來＝出一群怪，打完重唸同一句（第二次起顯示開頭幾個字當提示）。
    ///          說「再唸一次」可以只聽聲音再聽一遍（不顯示字，每句一次）。
    ///   quiz   快問快答：電腦唸一題（畫面顯示題目和選項），用說的回答。答對 count 題（預設 3）過關；
    ///          答錯（說了別的選項）或 15 秒沒答＝出一群怪，打完換下一題。
    ///
    /// 電腦唸的聲音：Emit("evspeak") → SoloBattle 叫 Tts.Speak；唸的期間辨識到的字不算（不然會把電腦的聲音當成玩家的答案）。
    /// 判定在房主／單人；隊友說的話用 evsay 送來（同咒語之門）。
    /// </summary>
    public partial class Battle
    {
        public static bool IsVoiceGame(string kind) => kind == "recite" || kind == "quiz";
        /// <summary>這支手機能用語音辨識（SoloBattle 設定）：不能的話故事的波次目標不會抽到聲控小遊戲</summary>
        public bool voiceGoals;

        int vgIdx, vgDone, vgNeed; float vgShowUntil, vgAnswerUntil; bool vgReplayUsed; int vgTries;
        readonly List<int> vgOrder = new List<int>();
        const float VgAnswerTime = 15f;
        public const float ReciteMatch = 0.78f, QuizMatch = 0.8f;

        // 背誦：有用的知識（安全、急救、健康、自然、生活）＋短詩。每句 30 字以內、盡量不要數字（語音辨識常把數字寫成阿拉伯數字）
        public static readonly (string text, string topic)[] ReciteLines =
        {
            ("地震來了要趴下、掩護、穩住", "防災"),
            ("火災逃生要低姿勢，往下逃，不要搭電梯", "防災"),
            ("燙傷要記得沖脫泡蓋送", "急救"),
            ("心肺復甦術要用力壓、快快壓、胸回彈、莫中斷", "急救"),
            ("流鼻血時頭微微往前傾，捏住鼻翼", "急救"),
            ("打雷的時候不要躲在大樹下", "防災"),
            ("洗手的步驟是內外夾弓大立腕", "健康"),
            ("運動前要暖身，運動後要伸展", "健康"),
            ("口渴之前就要喝水，運動時更要補充水分", "健康"),
            ("過馬路要看紅綠燈，走斑馬線", "生活"),
            ("月亮自己不會發光，是反射太陽的光", "自然"),
            ("植物用陽光、水和二氧化碳製造養分，放出氧氣", "自然"),
            ("彩虹的顏色是紅橙黃綠藍靛紫", "自然"),
            ("人體最大的器官是皮膚", "自然"),
            ("地球繞著太陽轉，月亮繞著地球轉", "自然"),
            ("吃飯要細嚼慢嚥，多吃蔬菜水果", "健康"),
            ("床前明月光，疑是地上霜", "詩"),
            ("舉頭望明月，低頭思故鄉", "詩"),
            ("春眠不覺曉，處處聞啼鳥", "詩"),
            ("白日依山盡，黃河入海流", "詩"),
            ("欲窮千里目，更上一層樓", "詩"),
            ("誰知盤中飧，粒粒皆辛苦", "詩"),
        };

        // 快問快答：題目、答案（可以有好幾種說法）、畫面上的選項
        public static readonly (string q, string[] ans, string[] opts)[] QuizItems =
        {
            ("火災和叫救護車，要打幾號？", new[] { "一一九", "119", "幺幺九" }, new[] { "一一零", "一一九", "一六五" }),
            ("報案找警察，要打幾號？", new[] { "一一零", "110", "幺幺零" }, new[] { "一一零", "一一九", "一一三" }),
            ("地震時第一個動作是什麼？", new[] { "趴下" }, new[] { "趴下", "往外跑", "搭電梯" }),
            ("燙傷的第一步要做什麼？", new[] { "沖冷水", "沖水", "沖" }, new[] { "沖冷水", "塗牙膏", "抹醬油" }),
            ("彩虹有幾種顏色？", new[] { "七", "7", "七種" }, new[] { "五種", "七種", "九種" }),
            ("太陽系最大的行星是哪一個？", new[] { "木星" }, new[] { "地球", "木星", "火星" }),
            ("人體最大的器官是什麼？", new[] { "皮膚" }, new[] { "心臟", "皮膚", "肝臟" }),
            ("植物行光合作用會放出什麼氣體？", new[] { "氧氣", "氧" }, new[] { "氧氣", "二氧化碳", "氮氣" }),
            ("台灣最高的山是哪一座？", new[] { "玉山" }, new[] { "雪山", "玉山", "合歡山" }),
            ("地球上最大的海洋是哪一個？", new[] { "太平洋" }, new[] { "大西洋", "太平洋", "印度洋" }),
            ("打雷的時候可以躲在大樹下嗎？", new[] { "不可以", "不行", "不能", "不要" }, new[] { "可以", "不可以" }),
            ("水在攝氏幾度會結冰？", new[] { "零度", "零", "0度", "0" }, new[] { "零度", "十度", "一百度" }),
            ("過馬路要走什麼線？", new[] { "斑馬線" }, new[] { "斑馬線", "黃線", "紅線" }),
            ("月亮會自己發光嗎？", new[] { "不會" }, new[] { "會", "不會" }),
            ("一天有幾個小時？", new[] { "二十四", "24" }, new[] { "十二", "二十四", "三十六" }),
            ("「床前明月光」的下一句是什麼？", new[] { "疑是地上霜" }, new[] { "疑是地上霜", "低頭思故鄉" }),
        };

        void StartVoiceGame(string kind)
        {
            waveToSpawn = 0;
            vgNeed = Mathf.Clamp(PI("count", 3), 1, 6); vgDone = 0; vgTries = 0;
            int n = kind == "recite" ? ReciteLines.Length : QuizItems.Length;
            vgOrder.Clear(); for (int i = 0; i < n; i++) vgOrder.Add(i);
            for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); (vgOrder[i], vgOrder[j]) = (vgOrder[j], vgOrder[i]); }
            vgIdx = 0;
            evPts.Add(ArenaCenter()); evState.Add(0);
            AskVoice(true);
        }

        string VgText => roomEvent == "recite" ? ReciteLines[vgOrder[vgIdx % vgOrder.Count]].text : QuizItems[vgOrder[vgIdx % vgOrder.Count]].q;

        /// <summary>唸出現在這一題（show＝畫面也顯示：背誦的第一次、快問快答每次）</summary>
        void AskVoice(bool show)
        {
            string t = VgText;
            float speak = 0.6f + t.Length * 0.27f;
            vgShowUntil = show ? now + speak + (roomEvent == "recite" ? 1.2f : 999f) : now;
            vgAnswerUntil = now + speak + VgAnswerTime;
            vgReplayUsed = false;
            Emit("evspeak", player.Feet, Color.white, t);
        }

        void TickVoiceGame()
        {
            if (evPhase == 2)
            {
                if (PenaltyOver()) { evPhase = 1; if (roomEvent == "quiz") vgIdx++; AskVoice(true); }
                return;
            }
            if (now > vgAnswerUntil)
            {
                vgTries++;
                EvPenalty(roomEvent == "recite" ? "時間到，沒有背出來！" : "時間到，沒有回答！");
            }
        }

        /// <summary>聽到玩家說的話（房主判定）。回傳 true＝答對</summary>
        public bool EvVoice(string text)
        {
            if (remoteEnemy || !IsVoiceGame(roomEvent) || evPhase != 1 || string.IsNullOrEmpty(text)) return false;
            var t = SpeechMatcher.Normalize(text);
            if (t.Length == 0) return false;
            if (roomEvent == "recite")
            {
                if (now < vgShowUntil) return false;   // 字還在畫面上：不算（要背）
                if (!vgReplayUsed && (t.Contains("再唸") || t.Contains("再念") || t.Contains("再說一次") || t.Contains("再講一次")))
                { vgReplayUsed = true; vgAnswerUntil = now + 0.6f + VgText.Length * 0.27f + VgAnswerTime; Emit("evspeak", player.Feet, Color.white, VgText); return false; }
                var a = SpeechMatcher.Normalize(VgText);
                if (t.Length < a.Length * 0.6f) return false;   // 還沒講完（中途結果）
                if (SpeechMatcher.Score(t, a) >= ReciteMatch) { VoiceRight($"✅ 背對了！「{VgText}」"); return true; }
                return false;   // 背錯不立刻懲罰：可以重講，時間到才算失敗
            }
            var item = QuizItems[vgOrder[vgIdx % vgOrder.Count]];
            foreach (var ans in item.ans)
            {
                var a = SpeechMatcher.Normalize(ans);
                if (a.Length > 0 && (t.Contains(a) || SpeechMatcher.Score(t, a) >= QuizMatch)) { VoiceRight($"✅ 答對了！{item.ans[0]}"); return true; }
            }
            foreach (var o in item.opts)
            {
                bool isAns = false; foreach (var ans in item.ans) if (SpeechMatcher.Normalize(ans) == SpeechMatcher.Normalize(o)) isAns = true;
                if (isAns) continue;
                var on = SpeechMatcher.Normalize(o);
                if (on.Length > 0 && (t == on || SpeechMatcher.Score(t, on) >= 0.9f && t.Length <= on.Length + 2)) { vgTries++; EvPenalty($"「{o}」答錯了！答案是「{item.ans[0]}」"); return false; }
            }
            return false;
        }

        void VoiceRight(string msg)
        {
            vgDone++; vgTries = 0;
            Emit("evinfo", player.Feet, new Color(0.5f, 1f, 0.6f), msg);
            if (vgDone >= vgNeed) { FinishRoomEvent(roomEvent == "recite" ? "📖 全部背出來了！" : "🧠 全部答對了！"); return; }
            vgIdx++;
            AskVoice(true);
        }

        string VoiceEvText()
        {
            if (evPhase == 2) return $"⚔ 打倒湧出來的怪，再挑戰一次（錯 {evFails} 次）";
            if (roomEvent == "recite")
            {
                if (now < vgShowUntil) return $"📖 記住這句（{vgDone + 1}/{vgNeed}）：{VgText}";
                string hint = vgTries > 0 ? $"（提示：{VgText.Substring(0, Mathf.Min(4, VgText.Length))}…）" : "";
                return $"🗣 背出來！（{vgDone + 1}/{vgNeed}，剩 {Mathf.Max(0f, vgAnswerUntil - now):0} 秒）{hint}" + (vgReplayUsed ? "" : "　說「再唸一次」可以再聽一遍");
            }
            var it = QuizItems[vgOrder[vgIdx % vgOrder.Count]];
            return $"🧠 快問快答（{vgDone + 1}/{vgNeed}）：{it.q}　{string.Join("／", it.opts)}（剩 {Mathf.Max(0f, vgAnswerUntil - now):0} 秒，用說的回答）";
        }
    }
}
