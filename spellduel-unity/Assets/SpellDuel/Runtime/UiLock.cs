using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 新手指引的最上層（GUI.depth 最前面）：
    ///   UiKit.LockKey 有值時，畫面變暗、只留下要按的按鈕（閃爍＋手指），旁邊的說明框寫現在要按什麼、為什麼；
    ///   要按的按鈕以外的點擊全部吃掉（手指拖曳、方向鍵也不行）。說明型的步驟有「下一步 ▶」。
    /// 內容由 SoloBattle.Walkthrough.cs 每幀設定。
    /// </summary>
    public class UiLock : MonoBehaviour
    {
        static UiLock I;
        public static string Title, Text;
        /// <summary>這一步要不要擋其他點擊（選職業這種自己決定的步驟＝不擋，只提示）</summary>
        public static bool Block = true;
        /// <summary>說明型的步驟：畫「下一步」按鈕；按了 NextPressed＝true（由指引讀走）</summary>
        public static bool NextButton, NextPressed;
        /// <summary>第幾步／共幾步（顯示用）</summary>
        public static int StepNo, StepCount;
        /// <summary>指引每一幀都要回報（SoloBattle 沒在畫＝回到首頁等：鎖自動解除）</summary>
        public static int Beat = -99;
        static readonly List<Rect> rects = new List<Rect>();

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("Ui Lock");
            DontDestroyOnLoad(go);
            I = go.AddComponent<UiLock>();
        }

        public static bool Active => UiKit.LockKey != null || NextButton;
        /// <summary>現在有沒有在擋點擊（方向鍵、觸控也要看這個）</summary>
        public static bool Blocking => Active && Block && !UiKit.LockBypass;

        static bool IsPointer(EventType t) => t == EventType.MouseDown || t == EventType.MouseUp || t == EventType.MouseDrag || t == EventType.ScrollWheel || t == EventType.ContextClick;

        void OnGUI()
        {
            GUI.depth = -1000;
            var e = Event.current;
            if (Time.frameCount - Beat > 3) { UiKit.LockKey = null; NextButton = false; }
            if (!Active) { UiKit.LockRectsBuilding.Clear(); UiKit.LockBypass = false; return; }
            if (e.type == EventType.Repaint)
            {
                rects.Clear();
                foreach (var r in UiKit.LockRectsBuilding)
                {
                    var a = GUIUtility.ScreenToGUIPoint(new Vector2(r.x, r.y)); var b = GUIUtility.ScreenToGUIPoint(new Vector2(r.xMax, r.yMax));
                    rects.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
                }
                UiKit.LockRectsBuilding.Clear();
            }
            UiKit.LockBypass = !Block;   // 找不到按鈕也不放行：完全不准按其他地方

            float W = Screen.width, H = Screen.height;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.85f);
            var safe = Screen.safeArea;
            float pad = W * 0.04f, topY = H - safe.yMax, botY = H - safe.y;
            bool haveTarget = rects.Count > 0;
            var tgt = haveTarget ? rects[0] : new Rect(W / 2f, H / 2f, 0f, 0f);
            // 說明框：放在目標的另一半邊（目標在下半部 → 說明在上面）
            float ch = fs * 6.2f, cw = W - pad * 2;
            bool cardTop = !haveTarget || tgt.center.y > H * 0.5f;
            var card = new Rect(pad, cardTop ? topY + fs * 3.2f : botY - ch - fs * 1.2f, cw, ch);
            if (haveTarget && card.Overlaps(tgt)) card.y = cardTop ? Mathf.Max(topY + 4f, tgt.y - ch - fs) : Mathf.Min(botY - ch - 4f, tgt.yMax + fs);
            Rect nb = NextButton ? new Rect(card.xMax - fs * 7.5f, card.yMax - fs * 2.3f, fs * 6.9f, fs * 1.9f) : default;

            // 擋點擊：不在目標按鈕（或「下一步」）上的點擊全部吃掉
            if (Blocking && IsPointer(e.type))
            {
                bool ok = NextButton && nb.Contains(e.mousePosition);
                foreach (var r in rects) if (r.Contains(e.mousePosition)) { ok = true; break; }
                if (!ok) { e.Use(); return; }
            }

            if (e.type == EventType.Repaint && Block && haveTarget)
            {
                // 畫面變暗，目標的位置挖一個洞
                var hole = new Rect(tgt.x - 6f, tgt.y - 6f, tgt.width + 12f, tgt.height + 12f);
                var dim = new Color(0f, 0f, 0.02f, 0.62f);
                Fill(new Rect(0, 0, W, hole.y), dim); Fill(new Rect(0, hole.yMax, W, H - hole.yMax), dim);
                Fill(new Rect(0, hole.y, hole.x, hole.height), dim); Fill(new Rect(hole.xMax, hole.y, W - hole.xMax, hole.height), dim);
            }
            else if (e.type == EventType.Repaint && Block && NextButton && UiKit.LockKey == null) Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0.02f, 0.5f));
            if (haveTarget)
            {
                // 手指：在目標旁邊上下跳
                float bob = Mathf.Sin(Time.unscaledTime * 6f) * fs * 0.35f;
                bool below = tgt.yMax + fs * 2.6f < botY;
                var fr = below ? new Rect(tgt.center.x - fs, tgt.yMax + fs * 0.2f + bob, fs * 2f, fs * 2f) : new Rect(tgt.center.x - fs, tgt.y - fs * 2.2f - bob, fs * 2f, fs * 2f);
                UiKit.Text(fr, below ? "👆" : "👇", Mathf.RoundToInt(fs * 1.6f), Color.white, TextAnchor.MiddleCenter);
                foreach (var r in rects)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                    if (e.type == EventType.Repaint) { var g = new Rect(r.x - 5f, r.y - 5f, r.width + 10f, r.height + 10f); Frame(g, new Color(1f, 0.9f, 0.3f, 0.5f + 0.5f * k), Mathf.Max(3f, fs * 0.16f)); }
                }
            }
            var col = new Color(0.4f, 1f, 0.75f);
            UiKit.Card(card, 0.96f, col, 1f);
            float gap = fs * 0.45f;
            string head = "🧭 新手指引" + (StepCount > 0 ? $" {StepNo}/{StepCount}" : "") + (string.IsNullOrEmpty(Title) ? "" : "・" + Title);
            UiKit.Text(new Rect(card.x + gap, card.y + gap * 0.4f, card.width - gap * 2, fs * 1.4f), head, fs, col, TextAnchor.MiddleLeft, true);
            string body = Text ?? "";
            UiKit.Text(new Rect(card.x + gap, card.y + fs * 1.6f, card.width - gap * 2 - (NextButton ? fs * 7.5f : 0f), card.height - fs * 1.8f), body, fsS, new Color(0.95f, 0.97f, 1f), TextAnchor.UpperLeft, false, 0.6f);
            if (NextButton)
            {
                var keep = UiKit.LockKey; UiKit.LockKey = null;   // 「下一步」本身不受鎖
                if (UiKit.Button(nb, "下一步 ▶", fsS, true, col)) NextPressed = true;
                UiKit.LockKey = keep;
            }
        }

        static void Fill(Rect r, Color c)
        {
            if (r.width <= 0f || r.height <= 0f) return;
            var keep = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = keep;
        }
        static void Frame(Rect r, Color c, float w)
        {
            Fill(new Rect(r.x, r.y, r.width, w), c); Fill(new Rect(r.x, r.yMax - w, r.width, w), c);
            Fill(new Rect(r.x, r.y, w, r.height), c); Fill(new Rect(r.xMax - w, r.y, w, r.height), c);
        }
    }
}
