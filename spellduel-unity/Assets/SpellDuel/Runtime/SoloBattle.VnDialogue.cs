using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 對話介面（《永遠的7日之都》風格）：
    ///   全螢幕 2D 背景（沒有圖就用暗色蓋住 AR 畫面）、左右兩個立繪位置（說話的人在前面、亮的；另一位變暗、縮小一點）、
    ///   下方寬版對話框（名牌斜標籤、打字機效果、▼ 閃爍），右上「自動／紀錄／跳過」。
    ///   點一下：字還在跑＝直接顯示完整一句；跑完＝下一句。劇本裡「背景：bg_id」這一行會換背景（不顯示）。
    /// </summary>
    public partial class SoloBattle
    {
        string vnBg, vnBgPrev; float vnBgAt = -9f;   // 目前／上一張背景、換背景的時間（淡入）
        string vnLeft, vnRight; float vnLeftAt = -9f, vnRightAt = -9f;   // 左右兩個立繪位置的說話者、進場時間
        string vnLeftExpr, vnRightExpr; bool vnActiveLeft = true;
        int vnShownIdx = -1; float vnLineAt; bool vnAuto, vnLog; Vector2 vnLogScroll;
        public const float VnCharsPerSec = 28f;

        void ResetVn()
        {
            vnBg = vnBgPrev = null; vnLeft = vnRight = null; vnShownIdx = -1; vnLog = false;
        }

        /// <summary>沒有指定時，依現在的場合決定背景</summary>
        string ContextBg()
        {
            if (storyStage != null)
            {
                int th = Mathf.Clamp(storyStage.theme, 0, Story2D.ThemeKeys.Length - 1);
                return (storyStage.toWave >= 10 ? "boss_" : "stage_") + Story2D.ThemeKeys[th];
            }
            if (pendingDungeon != null || dungeon != null) return "dungeon";
            switch (world.screen)
            {
                case WorldUI.Screen.Guild: return "guild";
                case WorldUI.Screen.Tower: return "tower";
                case WorldUI.Screen.Smith: case WorldUI.Screen.Shop: case WorldUI.Screen.Town:
                    return Story2D.TownKeys[Mathf.Clamp(world.town, 0, Story2D.TownKeys.Length - 1)];
            }
            return "village";
        }

        static int VisibleChars(string s, float t) => Mathf.Min(s.Length, Mathf.FloorToInt(t * VnCharsPerSec));

        void DrawDialogue()
        {
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y, pad = W * 0.04f;
            int fs = UiKit.BaseFont, fsL = Mathf.RoundToInt(fs * 1.12f), fsS = Mathf.RoundToInt(fs * 0.78f);
            if (dlgIdx >= dlg.Count) { EndDialogue(); return; }
            var line = dlg[dlgIdx];
            // 「背景：xxx」＝換背景（不是台詞）
            if (line.speaker == "背景") { vnBgPrev = vnBg; vnBg = line.text.Trim(); vnBgAt = Time.time; dlgIdx++; return; }
            if (vnBg == null) { vnBg = ContextBg(); vnBgAt = -9f; }
            string text = Story.Fill(line.text, myClass);
            if (vnShownIdx != dlgIdx) { vnShownIdx = dlgIdx; vnLineAt = Time.time; PlaceSpeaker(line); }
            float since = Time.time - vnLineAt;
            bool typing = VisibleChars(text, since) < text.Length;

            // ---- 背景
            var bg = Story2D.Background(vnBg);
            if (bg != null)
            {
                var prevBg = Story2D.Background(vnBgPrev);
                float k = Mathf.Clamp01((Time.time - vnBgAt) / 0.5f);
                if (prevBg != null && k < 1f) GUI.DrawTexture(new Rect(0, 0, W, H), prevBg, ScaleMode.ScaleAndCrop);
                GUI.color = new Color(1f, 1f, 1f, k); GUI.DrawTexture(new Rect(0, 0, W, H), bg, ScaleMode.ScaleAndCrop); GUI.color = Color.white;
            }
            else { GUI.color = new Color(0.02f, 0.03f, 0.07f, 0.62f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white; }
            // 下半部漸暗（立繪腳下、對話框後面）
            for (int i = 0; i < 12; i++) { float a = i / 12f; GUI.color = new Color(0f, 0f, 0.03f, 0.05f + 0.05f * a); GUI.DrawTexture(new Rect(0, H * (0.55f + 0.45f * a), W, H * 0.45f / 12f + 1f), Texture2D.whiteTexture); }
            GUI.color = Color.white;

            // ---- 對話框位置（先算，立繪站在它後面）
            float boxH = Mathf.Min(H * 0.25f, (botY - topY) * 0.3f);
            var box = new Rect(0, botY - boxH - pad * 0.4f, W, boxH);
            bool narr = string.IsNullOrEmpty(line.speaker);

            // ---- 立繪：非說話者先畫（在後面）
            DrawStanding(false, box, W, H, narr);
            DrawStanding(true, box, W, H, narr);

            // ---- 對話框
            GUI.color = new Color(0.03f, 0.04f, 0.09f, 0.9f); GUI.DrawTexture(box, Texture2D.whiteTexture);
            var col = narr ? new Color(0.75f, 0.82f, 1f) : Story.TagColor(line.tag);
            var gold = new Color(0.95f, 0.8f, 0.45f);
            GUI.color = gold; GUI.DrawTexture(new Rect(0, box.y, W, Mathf.Max(2f, fs * 0.09f)), Texture2D.whiteTexture);
            GUI.color = new Color(gold.r, gold.g, gold.b, 0.35f); GUI.DrawTexture(new Rect(0, box.y + fs * 0.25f, W, 1f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            float tx = pad * 1.2f, tw = W - pad * 2.4f, ty = box.y + fs * 0.9f;
            if (!narr)
            {
                // 名牌：斜切的色塊標籤，壓在對話框上緣
                string nm = line.speaker; int dot = nm.LastIndexOf('・'); string role = dot >= 0 ? nm.Substring(0, dot) : null; if (dot >= 0) nm = nm.Substring(dot + 1);
                float nw = Mathf.Max(fs * 5f, fs * (nm.Length + 1.6f)), nh = fs * 1.9f;
                var nr = new Rect(tx - fs * 0.3f, box.y - nh * 0.62f, nw, nh);
                GUI.color = new Color(col.r * 0.55f, col.g * 0.55f, col.b * 0.55f, 0.97f); GUI.DrawTexture(nr, Texture2D.whiteTexture);
                GUI.color = col; GUI.DrawTexture(new Rect(nr.x, nr.y, fs * 0.28f, nr.height), Texture2D.whiteTexture);   // 左邊的色條
                GUI.color = new Color(1f, 1f, 1f, 0.25f); GUI.DrawTexture(new Rect(nr.x, nr.yMax - 2f, nr.width, 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                UiKit.Text(new Rect(nr.x + fs * 0.6f, nr.y, nr.width - fs * 0.7f, nr.height), nm, Mathf.RoundToInt(fs * 1.05f), Color.white, TextAnchor.MiddleLeft, true);
                if (role != null) UiKit.Text(new Rect(nr.xMax + fs * 0.4f, nr.y + nh * 0.25f, W * 0.5f, nh * 0.7f), role, fsS, new Color(0.85f, 0.85f, 0.9f, 0.85f), TextAnchor.MiddleLeft);
            }
            string shown = text.Substring(0, VisibleChars(text, since));
            UiKit.Text(new Rect(tx, ty, tw, box.yMax - ty - fs * 1.4f), shown, fsL, narr ? new Color(0.82f, 0.88f, 1f) : Color.white, narr ? TextAnchor.UpperCenter : TextAnchor.UpperLeft, false, 0.6f);
            if (!typing && Mathf.Repeat(Time.time, 0.9f) < 0.6f)
                UiKit.Text(new Rect(W - pad * 2.2f, box.yMax - fs * 1.6f, pad * 1.4f, fs * 1.3f), "▼", fs, gold, TextAnchor.MiddleCenter);
            UiKit.Text(new Rect(pad, box.yMax - fs * 1.5f, W * 0.4f, fs * 1.2f), $"{dlgIdx + 1}/{dlg.Count}", fsS, new Color(0.6f, 0.62f, 0.7f), TextAnchor.MiddleLeft);

            // ---- 右上：自動／紀錄／跳過
            float bw = W * 0.17f, bh = fs * 1.7f, by = topY + pad * 0.6f, bx = W - pad - bw;
            if (UiKit.Button(new Rect(bx, by, bw, bh), "跳過 ⏭", fsS)) { EndDialogue(); return; }
            if (UiKit.Button(new Rect(bx - bw - pad * 0.3f, by, bw, bh), "紀錄", fsS, vnLog)) { vnLog = !vnLog; vnLogScroll = new Vector2(0, 99999); return; }
            if (UiKit.Button(new Rect(bx - (bw + pad * 0.3f) * 2, by, bw, bh), vnAuto ? "自動 ▶" : "自動", fsS, vnAuto, gold)) { vnAuto = !vnAuto; return; }
            if (vnLog) { DrawVnLog(W, H, topY + pad * 0.6f + bh + pad * 0.4f, botY, pad, fs); return; }

            // ---- 自動播放：字跑完後停一下（依句子長度）就下一句
            if (vnAuto && !typing && since - text.Length / VnCharsPerSec > 1.2f + text.Length * 0.03f) { NextLine(); return; }
            var e = Event.current;
            if (e.type == EventType.MouseUp)
            {
                e.Use();
                if (typing) vnLineAt = Time.time - text.Length / VnCharsPerSec - 0.01f;   // 字還在跑：直接顯示完整一句
                else NextLine();
            }
        }

        void NextLine()
        {
            dlgIdx++; Sfx.Play("talk", 0.6f);
            if (dlgIdx >= dlg.Count) EndDialogue();
        }

        /// <summary>這句的說話者站到哪一邊：已經在場上就換他亮；新的人換掉「上一句不是他」的那一邊</summary>
        void PlaceSpeaker(StoryLine line)
        {
            if (string.IsNullOrEmpty(line.speaker)) return;   // 旁白：兩邊都變暗
            string expr = line.expr ?? Story2D.GuessExpr(line.text);
            if (vnLeft == line.speaker) { vnActiveLeft = true; vnLeftExpr = expr; return; }
            if (vnRight == line.speaker) { vnActiveLeft = false; vnRightExpr = expr; return; }
            bool toLeft = vnLeft == null || (vnRight != null && !vnActiveLeft);   // 第一個人在左邊；之後放到剛才沒說話的那一邊
            if (vnLeft != null && vnRight == null) toLeft = false;
            if (toLeft) { vnLeft = line.speaker; vnLeftExpr = expr; vnLeftAt = Time.time; vnActiveLeft = true; }
            else { vnRight = line.speaker; vnRightExpr = expr; vnRightAt = Time.time; vnActiveLeft = false; }
        }

        /// <summary>畫一邊的立繪（active＝畫說話中的那一邊）。有 Codex 的立繪用立繪，沒有就用程式畫的頭像放在框裡</summary>
        void DrawStanding(bool active, Rect box, float W, float H, bool narr)
        {
            bool left = active ? vnActiveLeft : !vnActiveLeft;
            string who = left ? vnLeft : vnRight;
            if (who == null) return;
            bool lit = active && !narr;
            string expr = left ? vnLeftExpr : vnRightExpr;
            float enter = Mathf.Clamp01((Time.time - (left ? vnLeftAt : vnRightAt)) / 0.3f), ease = 1f - (1f - enter) * (1f - enter);
            var tex = Story2D.Portrait(Story2D.IdOf(who), expr);
            float scale = lit ? 1f : 0.94f;
            float bob = lit ? Mathf.Sin(Time.time * 2.2f) * H * 0.003f : 0f;
            var tint = lit ? Color.white : new Color(0.42f, 0.44f, 0.5f, 1f);
            tint.a = ease;
            float slide = (1f - ease) * W * 0.15f * (left ? -1f : 1f);
            if (tex != null)
            {
                float h = H * 0.66f * scale, w = h * tex.width / Mathf.Max(1f, tex.height);
                float cx = (left ? W * 0.33f : W * 0.67f) + slide;
                var r = new Rect(cx - w / 2f, box.y + box.height * 0.35f - h + bob, w, h);
                GUI.color = tint;
                // 立繪原本臉朝右：右邊的人左右翻轉（臉朝向畫面中間）
                if (left) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
                else GUI.DrawTextureWithTexCoords(r, tex, new Rect(1f, 0f, -1f, 1f), true);
                GUI.color = Color.white;
                return;
            }
            // 沒有立繪：程式畫的頭像放在圓角框裡（暫代）
            float s = Mathf.Min(W * 0.42f, H * 0.26f) * scale;
            float px = (left ? W * 0.3f : W * 0.7f) + slide;
            var pr = new Rect(px - s / 2f, box.y - s * 0.92f + bob, s, s);
            var tc = Story.TagColor(null);
            UiKit.Card(new Rect(pr.x - s * 0.04f, pr.y - s * 0.04f, s * 1.08f, s * 1.08f), lit ? 0.85f : 0.6f, lit ? new Color(0.95f, 0.8f, 0.45f) : (Color?)null, ease);
            GUI.color = tint; GUI.DrawTexture(pr, Portraits.Get(who, null), ScaleMode.ScaleToFit, true); GUI.color = Color.white;
        }

        void DrawVnLog(float W, float H, float top, float botY, float pad, int fs)
        {
            var r = new Rect(pad * 0.5f, top, W - pad, botY - top - pad * 0.5f);
            GUI.color = new Color(0.02f, 0.03f, 0.07f, 0.94f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
            var view = new Rect(0, 0, r.width - fs, 0f);
            var items = new List<(string who, string text)>();
            for (int i = 0; i <= dlgIdx && i < dlg.Count; i++) { var l = dlg[i]; if (l.speaker == "背景") continue; items.Add((l.speaker, Story.Fill(l.text, myClass))); }
            view.height = items.Count * fs * 3.4f + fs;
            vnLogScroll = GUI.BeginScrollView(r, vnLogScroll, view);
            float y = fs * 0.5f;
            foreach (var (who, text) in items)
            {
                if (!string.IsNullOrEmpty(who)) UiKit.Text(new Rect(fs * 0.6f, y, view.width, fs * 1.2f), who, Mathf.RoundToInt(fs * 0.85f), new Color(0.95f, 0.8f, 0.45f), TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(fs * 0.6f, y + fs * 1.2f, view.width - fs, fs * 2f), text, Mathf.RoundToInt(fs * 0.85f), string.IsNullOrEmpty(who) ? new Color(0.8f, 0.86f, 1f) : Color.white, TextAnchor.UpperLeft, false, 0.6f);
                y += fs * 3.4f;
            }
            GUI.EndScrollView();
            var e = Event.current;
            if (e.type == EventType.MouseUp && !r.Contains(e.mousePosition)) { vnLog = false; e.Use(); }
        }
    }
}
