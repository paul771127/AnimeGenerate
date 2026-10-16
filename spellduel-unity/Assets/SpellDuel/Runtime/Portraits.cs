using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 對話框旁的說話者頭像：全部用程式即時畫出來（不需要圖檔）。
    ///   Get(speaker, tag) 依說話者名字（劇本 story.txt、城鎮居民、公會長…）取得頭像；
    ///   還沒有專屬畫法的人用「預設頭像」：名牌顏色（Story.TagColor）的圓角方框＋半身剪影。
    /// 專屬頭像：在 Render() 的 switch 加上 case "名字": 畫法(…); break;（美術由 Codex 補）。
    /// Render() 是純 C# 計算，可在 Unity 外測試；Get() 才建立 Texture2D 並快取。像素第 0 列是最下面。
    /// </summary>
    public static class Portraits
    {
        public const int Size = 128;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>居民名字「村長・老焰」→「老焰」（和劇本裡同一個人共用頭像）</summary>
        public static string Key(string speaker)
        {
            speaker = speaker ?? "";
            int i = speaker.LastIndexOf('・');
            return i >= 0 ? speaker.Substring(i + 1).Trim() : speaker.Trim();
        }

        public static Texture2D Get(string speaker, string tag)
        {
            speaker = Key(speaker);
            string key = (speaker ?? "") + "|" + (tag ?? "");
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Portrait " + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var c = Story.TagColor(tag);
            t.SetPixels32(Render(speaker ?? "", c.r, c.g, c.b, Size));
            t.Apply(false, true);
            cache[key] = t;
            return t;
        }

        /// <summary>畫出頭像像素（size×size，第 0 列在下）；r,g,b＝名牌顏色</summary>
        public static Color32[] Render(string speaker, float r, float g, float b, int size)
        {
            var px = new Color32[size * size];
            switch (speaker)
            {
                // 專屬頭像放這裡，例如：case "艾琳": Erin(px, size); return px;
                default: Default(px, size, r, g, b, Seed(speaker)); break;
            }
            return px;
        }

        static int Seed(string s) { int h = 17; foreach (var ch in s) h = h * 31 + ch; return h & 0x7fffffff; }

        /// <summary>預設頭像：圓角方框（名牌色漸層）＋半身剪影（頭、肩），髮色依名字變化</summary>
        static void Default(Color32[] px, int n, float r, float g, float b, int seed)
        {
            float hue = (seed % 360) / 360f;
            HsvToRgb(hue, 0.45f, 0.35f, out float hr, out float hg, out float hb);   // 髮色
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;   // -1..1，v 往上
                    // 圓角方框
                    float qx = Math.Abs(u) - 0.8f, qy = Math.Abs(v) - 0.8f;
                    float box = (float)Math.Sqrt(Math.Max(qx, 0f) * Math.Max(qx, 0f) + Math.Max(qy, 0f) * Math.Max(qy, 0f)) - 0.18f;
                    if (box > 0.02f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    float k = 0.35f + 0.35f * (v + 1f) * 0.5f;
                    float cr = r * k, cg = g * k, cb = b * k;
                    // 邊框
                    if (box > -0.05f) { cr = Lerp(cr, 1f, 0.55f); cg = Lerp(cg, 0.92f, 0.55f); cb = Lerp(cb, 0.7f, 0.55f); }
                    else
                    {
                        // 肩膀（下方的半橢圓）
                        float sh = (u * u) / 0.55f + ((v + 1.05f) * (v + 1.05f)) / 0.45f;
                        // 頭（圓）
                        float hd = (u * u + (v - 0.12f) * (v - 0.12f)) / 0.16f;
                        // 頭髮（頭的上半＋瀏海）
                        bool hair = hd < 1.12f && (v > 0.2f || Math.Abs(u) > 0.3f) && v > -0.15f;
                        if (sh < 1f) { cr = r * 0.55f; cg = g * 0.55f; cb = b * 0.55f; }
                        if (hd < 1f) { cr = 1f; cg = 0.86f; cb = 0.74f; }
                        if (hair) { cr = hr; cg = hg; cb = hb; }
                        // 眼睛
                        if (hd < 1f && !hair && Math.Abs(v - 0.08f) < 0.06f && Math.Abs(Math.Abs(u) - 0.15f) < 0.045f) { cr = 0.15f; cg = 0.12f; cb = 0.2f; }
                    }
                    float a = box > -0.0f ? Math.Max(0f, 1f - box / 0.02f) : 1f;
                    px[y * n + x] = new Color32(B(cr), B(cg), B(cb), B(a));
                }
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static byte B(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
        static void HsvToRgb(float h, float s, float v, out float r, out float g, out float b)
        {
            float i = (float)Math.Floor(h * 6f), f = h * 6f - i, p = v * (1f - s), q = v * (1f - f * s), t = v * (1f - (1f - f) * s);
            switch (((int)i) % 6)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
        }
    }
}
