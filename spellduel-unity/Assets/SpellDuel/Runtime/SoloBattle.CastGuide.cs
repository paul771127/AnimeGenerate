using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>放招提示的開關（預設開；⚙ 設定、提示卡上的 ✕ 都可以關）</summary>
    public static class CastGuidePref
    {
        const string Key = "SD_CastGuide";
        static int on = -1;
        public static bool On { get { if (on < 0) on = PlayerPrefs.GetInt(Key, 1); return on == 1; } }
        public static void Set(bool v) { on = v ? 1 : 0; PlayerPrefs.SetInt(Key, on); PlayerPrefs.Save(); }
    }

    /// <summary>
    /// 放招的分步驟提示：唸技能名稱之後，畫面上一張大卡寫「第幾步／下一步要做什麼」；
    /// 每做完一步整個畫面邊緣閃一下＋「✓ 完成」，再換下一步，直到放出去（或取消）。
    /// 正在放的那一格技能卡會一直閃（不管提示開不開）。
    /// </summary>
    public partial class SoloBattle
    {
        SkillDef guideSkill; int guideStep; float guideFlashAt = -9f; string guideFlashText;
        bool guideArmed;   // 伏擊技能蓄滿後：進入伏擊（charging 變 null、armed 有值）

        /// <summary>這一招現在的步驟清單（第 0 步＝唸技能名稱，已完成）與目前做到第幾步</summary>
        List<string> GuideSteps(SkillDef s, out int cur, out string now)
        {
            var steps = new List<string> { $"🎤 唸「{s.name}」" };
            float prog = battle.ChargeProgress(Me);
            string pct = $"{Mathf.FloorToInt(Mathf.Clamp01(prog) * 100)}%";
            var st = HandGesture.ReleaseOf(s);
            var seal = Advanced.SealOf(s.id);
            if (seal != null)
            {
                for (int i = 0; i < seal.Length; i++) steps.Add($"✋ 比 {seal[i]}");
                cur = 1 + Mathf.Min(sealStep, seal.Length - 1);
                now = $"比出數字 {seal[cur - 1]}（第 {cur} 個手印，共 {seal.Length} 個；每個數字停一下）";
            }
            else if (s.releaseNear)
            {
                steps.Add("✋ 手掌對鏡頭蓄力"); steps.Add("🏃 靠近敵人");
                cur = guideArmed ? 2 : 1;
                now = guideArmed ? $"靠近敵人 {Battle.AmbushReach:0.#}m 內，會自動出手" : !Me.palmVisible ? "把手掌伸到鏡頭前（才會蓄力）" : $"手掌停在鏡頭前蓄力 {pct}";
            }
            else if (st == HandGesture.Style.Chant)
            {
                steps.Add("📜 唸咒語"); steps.Add("✋ 比 5 放招");
                cur = ChantDone ? 2 : 1;
                now = ChantDone ? "把手伸到鏡頭前比 5（五指張開）" + (prog < 1f ? $"（蓄力 {pct}，比著等蓄滿就放）" : "") : $"唸出咒語「{s.chant}」";
            }
            else if (st == HandGesture.Style.Bow && Hand != null)
            {
                steps.Add("✊ 畫面中握拳"); steps.Add("↔ 握拳拉出畫面"); steps.Add("🖐 回畫面張開");
                cur = 1 + Mathf.Clamp(Hand.BowStage, 0, 2);
                now = Hand.BowStage == 0 ? "手伸到鏡頭前握拳（捏住弓弦）" : Hand.BowStage == 1 ? "握著拳，把手拉出畫面外（拉弓）" : prog < 1f ? $"拉滿了！等蓄力 {pct}，再把手放回畫面張開" : "手回到畫面張開五指：放箭！";
            }
            else if (st == HandGesture.Style.Rune && Hand != null)
            {
                steps.Add("☝ 畫符文"); steps.Add("👉 食指往前指");
                cur = Hand.RuneReady ? 2 : 1;
                now = Hand.RuneReady ? "符文完成！食指往前指向目標發射" : $"伸出食指，照畫面上的軌跡畫「{RuneRecognizer.RuneName(RuneRecognizer.RuneOf(s.id))}」";
            }
            else
            {
                string act = st == HandGesture.Style.Number ? $"✋ 比 {HandGesture.NumberOf(s.id)}" : s.cls == "archer" ? "✊→🖐 握拳再張開" : "✋ " + HandGesture.StyleName(st);
                steps.Add("⏳ 蓄力"); steps.Add(act);
                cur = prog < 1f ? 1 : 2;
                now = prog < 1f ? $"蓄力中 {pct}…" + (s.stillRadius > 0f ? "（站著別動）" : "") + (st == HandGesture.Style.Number ? $"（可以先比好 {HandGesture.NumberOf(s.id)} 等著）" : "")
                                : HandGesture.ReleaseHint(s);
                if (cur == 2 && st == HandGesture.Style.Number && Hand != null && Hand.HandVisible) now += $"（現在比的是：{(Hand.CurrentNumber > 0 ? Hand.CurrentNumber.ToString() : "—")}）";
            }
            if (s.stillRadius > 0f && Battle.FlatDistance(Me.Feet, Me.chargeFrom) > s.stillRadius * 0.4f) now += $"　⚠ 站著別動！";
            return steps;
        }

        /// <summary>每幀：步驟往前 → 閃一下；放出去 → 「施放成功」；取消 → 清掉</summary>
        void UpdateCastGuide()
        {
            if (battle == null || Me == null) return;
            var s = Me.charging;
            if (s == null && guideSkill != null && guideSkill.releaseNear && Me.armed == guideSkill) { s = guideSkill; if (!guideArmed) { guideArmed = true; GuideFlash("✓ 蓄滿了！"); guideStep = 2; } }
            if (s != guideSkill)
            {
                if (guideSkill != null && s == null)
                {
                    bool cast = Me.cooldownUntil.TryGetValue(guideSkill.id, out var cd) && cd > battle.now + 0.2f;
                    if (cast) GuideFlash($"✨ {guideSkill.name} 施放成功！");
                }
                guideSkill = s; guideArmed = false; guideStep = s != null ? 1 : 0;
                if (s != null) GuideFlash("✓ 詠唱成功！");
                return;
            }
            if (s == null) return;
            GuideSteps(s, out int cur, out _);
            if (cur > guideStep) { GuideFlash("✓ 完成！"); }
            guideStep = cur;
        }

        void GuideFlash(string text)
        {
            guideFlashAt = Time.time; guideFlashText = text;
            if (!CastGuidePref.On) return;
            Haptics.Play(Haptics.Kind.Light); Sfx.Play("select", 0.6f, 1.5f);
        }

        /// <summary>提示卡（下方面板上方）；回傳卡片頂端的 y（後面的訊息往上放）。關掉提示時回傳 -1</summary>
        float DrawCastGuide(float bottomY, float W, float pillW, int fs, int fsS, int fsL, string detail)
        {
            if (!CastGuidePref.On || phase != Phase.Fighting || Me == null) return -1f;
            float since = Time.time - guideFlashAt;
            var s = guideSkill;
            bool flashing = since < 0.9f && guideFlashText != null;
            if (s == null && !flashing) return -1f;
            var col = s != null ? s.color : new Color(1f, 0.9f, 0.5f); col.a = 1f;
            if (flashing && since < 0.45f) EdgeFlash(col, 1f - since / 0.45f);   // 做到了：畫面四邊亮一下
            float lh = fs * 1.6f, h = lh * 3.4f, w = pillW;
            var r = new Rect((W - w) / 2f, bottomY - h, w, h);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            UiKit.Card(r, flashing && since < 0.3f ? 0.95f : 0.85f, col, 0.6f + 0.4f * pulse);
            if (s == null)
            {
                // 剛放完：只顯示「施放成功」
                UiKit.Text(r, guideFlashText, fsL, col, TextAnchor.MiddleCenter, true);
                return r.y;
            }
            var steps = GuideSteps(s, out int cur, out string now);
            float x = r.x + fs * 0.6f, iw = r.width - fs * 1.2f, y = r.y + fs * 0.3f;
            // 第一列：技能名稱＋步驟 ●●○
            UiKit.Text(new Rect(x, y, iw * 0.55f, lh), $"⚡ {s.name}　步驟 {Mathf.Min(cur + 1, steps.Count)}/{steps.Count}", fs, Color.white, TextAnchor.MiddleLeft, true);
            float bw = fs * 3.2f;
            if (UiKit.Button(new Rect(r.xMax - bw - fs * 0.3f, y, bw, lh * 0.9f), "✕ 提示", fsS))
            { CastGuidePref.Set(false); Say("已關閉放招提示（⚙ 設定可以再打開）", 2.5f); }
            float dx = x + iw * 0.55f, dw = iw * 0.45f - bw - fs * 0.4f, ds = Mathf.Min(fs * 1.1f, dw / Mathf.Max(1, steps.Count));
            for (int i = 0; i < steps.Count; i++)
            {
                var dc = i < cur ? new Color(0.45f, 1f, 0.5f) : i == cur ? new Color(col.r, col.g, col.b, 0.5f + 0.5f * pulse) : new Color(1f, 1f, 1f, 0.3f);
                UiKit.Text(new Rect(dx + i * ds, y, ds, lh), i < cur ? "✔" : "●", fs, dc, TextAnchor.MiddleCenter, true);
            }
            y += lh;
            // 第二列：現在要做的事（大字，閃爍）
            var nc = Color.Lerp(Color.white, col, 0.35f + 0.35f * pulse);
            string big = cur < steps.Count ? $"👉 {steps[cur]}：{now}" : now;
            if (flashing && since < 0.5f) { big = guideFlashText; nc = new Color(0.5f, 1f, 0.55f); }
            UiKit.Text(new Rect(x, y, iw, lh * 1.3f), big, fsL, nc, TextAnchor.MiddleCenter, true);
            y += lh * 1.3f;
            // 第三列：做過的步驟＋蓄力等細節
            var done = new List<string>();
            for (int i = 0; i < cur && i < steps.Count; i++) done.Add("✔" + steps[i]);
            string tail = string.Join("　", done);
            if (!string.IsNullOrEmpty(detail)) tail += (tail.Length > 0 ? "　｜　" : "") + detail;
            UiKit.Text(new Rect(x, y, iw, lh * 0.9f), tail, fsS, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);
            return r.y;
        }

        /// <summary>技能格：正在放的那一招一直閃（邊框＋亮光＋「施放中」）</summary>
        static void DrawCastingTile(Rect r, Color c, int fsS)
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            float t = Mathf.Max(3f, r.width * 0.05f);
            GUI.color = new Color(c.r, c.g, c.b, 0.15f + 0.3f * blink);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(Mathf.Lerp(c.r, 1f, 0.5f), Mathf.Lerp(c.g, 1f, 0.5f), Mathf.Lerp(c.b, 1f, 0.5f), 0.55f + 0.45f * blink);
            GUI.DrawTexture(new Rect(r.x - t, r.y - t, r.width + t * 2, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x - t, r.yMax, r.width + t * 2, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            UiKit.Text(new Rect(r.x - r.width * 0.25f, r.y - fsS * 1.9f, r.width * 1.5f, fsS * 1.6f), "▼ 施放中", fsS, new Color(1f, 1f, 1f, 0.6f + 0.4f * blink), TextAnchor.MiddleCenter, true);
        }
    }
}
