using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 介面小工具（IMGUI）：圓角半透明卡片、圓角按鈕、圓角血條、會自動縮小字級／換行的文字（不會被格子切掉）、
    /// 中央訊息的底色膠囊。所有貼圖都在程式中產生。
    /// </summary>
    public static class UiKit
    {
        static Texture2D round, roundBorder;
        static GUIStyle cardStyle, borderStyle, textStyle;
        static readonly GUIContent content = new GUIContent();

        /// <summary>基準字級：依螢幕大小（窄螢幕不會太大）</summary>
        public static int BaseFont => Mathf.Max(18, Mathf.RoundToInt(Mathf.Min(Screen.height / 46f, Screen.width / 21f)));

        static void Init()
        {
            if (round != null) return;
            round = MakeRound(64, 18, false);
            roundBorder = MakeRound(64, 18, true);
            cardStyle = new GUIStyle { normal = { background = round }, border = new RectOffset(20, 20, 20, 20) };
            borderStyle = new GUIStyle { normal = { background = roundBorder }, border = new RectOffset(20, 20, 20, 20) };
            textStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, clipping = TextClipping.Overflow };
            textStyle.padding = new RectOffset(0, 0, 0, 0);
            textStyle.margin = new RectOffset(0, 0, 0, 0);
        }

        // 圓角方塊貼圖（白色，用 GUI.color 上色）；border＝只有外框
        static Texture2D MakeRound(int n, float r, bool border)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);                    // 圓角邊緣反鋸齒
                    if (border) a *= Mathf.Clamp01(d - (r - 3.5f) + 0.5f);    // 只留 3 像素寬的外框
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>圓角卡片：深色半透明底＋（可選）彩色外框</summary>
        public static void Card(Rect r, float alpha = 0.62f, Color? accent = null, float accentAlpha = 0.9f)
        {
            if (Event.current.type != EventType.Repaint) return;
            Init();
            var keep = GUI.color;
            GUI.color = new Color(0.06f, 0.07f, 0.11f, alpha);
            cardStyle.Draw(r, false, false, false, false);
            if (accent.HasValue)
            {
                var a = accent.Value; a.a = accentAlpha;
                GUI.color = a;
                borderStyle.Draw(r, false, false, false, false);
            }
            GUI.color = keep;
        }

        /// <summary>
        /// 文字：放不下就自動縮小字級（最小到 55%），並自動換行；附淡淡的陰影，在相機畫面上也看得清楚。
        /// </summary>
        public static void Text(Rect r, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false, float minScale = 0.55f)
        {
            if (string.IsNullOrEmpty(text)) return;
            Init();
            content.text = text;
            textStyle.alignment = anchor;
            textStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            int fs = size, min = Mathf.Max(10, Mathf.RoundToInt(size * minScale));
            textStyle.fontSize = fs;
            while (fs > min && textStyle.CalcHeight(content, r.width) > r.height + 1f) { fs--; textStyle.fontSize = fs; }
            var keep = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f * color.a);
            float o = Mathf.Max(1f, fs * 0.06f);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), content, textStyle);
            GUI.color = color;
            GUI.Label(r, content, textStyle);
            GUI.color = keep;
        }

        /// <summary>圓角按鈕：選中時用強調色填底＋外框；文字自動縮放。回傳是否被按下</summary>
        public static bool Button(Rect r, string text, int size, bool selected = false, Color? accent = null, bool enabled = true)
        {
            var ac = accent ?? new Color(0.45f, 0.65f, 1f);
            if (Event.current.type == EventType.Repaint)
            {
                Init();
                var keep = GUI.color;
                bool hover = enabled && r.Contains(Event.current.mousePosition) && Input.GetMouseButton(0);
                GUI.color = selected ? new Color(ac.r * 0.55f, ac.g * 0.55f, ac.b * 0.55f, 0.85f)
                          : hover ? new Color(0.25f, 0.27f, 0.35f, 0.85f) : new Color(0.1f, 0.11f, 0.16f, enabled ? 0.78f : 0.45f);
                cardStyle.Draw(r, false, false, false, false);
                GUI.color = selected ? new Color(ac.r, ac.g, ac.b, 1f) : new Color(1f, 1f, 1f, enabled ? 0.22f : 0.1f);
                borderStyle.Draw(r, false, false, false, false);
                GUI.color = keep;
            }
            float pad = Mathf.Min(r.height * 0.12f, size * 0.4f);
            Text(new Rect(r.x + pad, r.y + pad * 0.5f, r.width - pad * 2, r.height - pad), text, size,
                enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, selected);
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>圓角血條／能量條：文字在條內置中</summary>
        public static void Bar(Rect r, float v, Color c, string text, int size)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Init();
                var keep = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                cardStyle.Draw(r, false, false, false, false);
                float w = Mathf.Max(r.height, r.width * Mathf.Clamp01(v));
                if (v > 0.001f)
                {
                    GUI.color = c;
                    cardStyle.Draw(new Rect(r.x, r.y, w, r.height), false, false, false, false);
                    GUI.color = new Color(1f, 1f, 1f, 0.18f);   // 上半部亮一點
                    cardStyle.Draw(new Rect(r.x + 2, r.y + 2, w - 4, r.height * 0.45f), false, false, false, false);
                }
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                borderStyle.Draw(r, false, false, false, false);
                GUI.color = keep;
            }
            if (!string.IsNullOrEmpty(text)) Text(r, text, size, Color.white, TextAnchor.MiddleCenter, true, 0.5f);
        }

        /// <summary>置中的訊息膠囊：依文字寬度加深色圓角底（太長自動換行，最多兩行）</summary>
        public static void Pill(float centerY, string text, int size, Color color, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return;
            Init();
            content.text = text;
            textStyle.fontSize = size; textStyle.fontStyle = FontStyle.Bold;
            var sz = textStyle.CalcSize(content);
            float w = Mathf.Min(maxWidth, sz.x + size * 1.4f);
            float h = textStyle.CalcHeight(content, w - size * 1.4f) + size * 0.7f;
            h = Mathf.Min(h, size * 3.4f);
            var r = new Rect((Screen.width - w) / 2f, centerY - h / 2f, w, h);
            Card(r, 0.55f, color, 0.55f);
            Text(new Rect(r.x + size * 0.7f, r.y + size * 0.25f, r.width - size * 1.4f, r.height - size * 0.5f), text, size, color, TextAnchor.MiddleCenter, true);
        }
    }
}
