using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 消耗品的小圖示（使用者要求：戰鬥畫面上方的藥水要有圖示）。emoji 在手機的 IMGUI 字型常常顯示不出來，
    /// 所以用程式畫：小治療藥水＝小圓瓶紅、大治療藥水＝大圓瓶紅、魔力藥水＝藍、回城捲軸＝羊皮紙卷。第一次用時產生、之後快取。
    /// </summary>
    public static class ConsumableIcons
    {
        const int S = 64;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string id)
        {
            if (cache.TryGetValue(id, out var t) && t) return t;
            t = id switch
            {
                "hp_s" => Flask(new Color(0.95f, 0.22f, 0.25f), 0.62f),
                "hp_l" => Flask(new Color(0.85f, 0.1f, 0.2f), 0.86f),
                "mp_s" => Flask(new Color(0.25f, 0.55f, 1f), 0.62f),
                "scroll" => Scroll(),
                _ => Flask(new Color(0.6f, 0.9f, 0.5f), 0.62f),
            };
            cache[id] = t;
            return t;
        }

        static Texture2D New()
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "consumable icon" };
            return t;
        }

        static float Cov(float sd) => Mathf.Clamp01(0.5f - sd * S * 0.5f);   // 有號距離（0～1 座標）→ 覆蓋率（反鋸齒一像素）
        static Color Over(Color dst, Color src, float a) { a *= src.a; var o = Color.Lerp(dst, src, a); o.a = dst.a + a * (1f - dst.a); return o; }
        static float Circle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;
        static float Box(Vector2 p, Vector2 c, Vector2 h, float round = 0f)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x) - h.x + round, Mathf.Abs(p.y - c.y) - h.y + round);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        /// <summary>圓底藥瓶：size＝瓶身大小（0～1）</summary>
        static Texture2D Flask(Color liquid, float size)
        {
            var t = New(); var px = new Color[S * S];
            float r = 0.3f * size + 0.04f;
            var body = new Vector2(0.5f, 0.08f + r);
            float neckH = 0.12f + 0.05f * size, neckW = 0.07f + 0.02f * size;
            var neck = new Vector2(0.5f, body.y + r * 0.8f + neckH * 0.5f);
            var cork = new Vector2(0.5f, neck.y + neckH * 0.5f + 0.045f);
            var glass = new Color(0.85f, 0.95f, 1f, 1f); var outline = new Color(0.08f, 0.06f, 0.1f, 1f);
            var corkC = new Color(0.62f, 0.42f, 0.24f, 1f);
            float level = body.y + r * 0.25f;   // 液面高度
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                var p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                float shape = Mathf.Min(Circle(p, body, r), Box(p, neck, new Vector2(neckW, neckH * 0.5f + 0.01f)));
                float corkD = Box(p, cork, new Vector2(neckW + 0.02f, 0.045f), 0.015f);
                var c = new Color(0, 0, 0, 0);
                c = Over(c, outline, Cov(Mathf.Min(shape, corkD) - 0.035f));     // 黑描邊
                c = Over(c, glass, Cov(shape));                                   // 玻璃
                float liq = Mathf.Max(Circle(p, body, r - 0.035f), p.y - level);
                var lc = Color.Lerp(liquid, Color.white, Mathf.Clamp01((p.y - body.y + r) / (2f * r)) * 0.25f);
                c = Over(c, lc, Cov(liq));                                        // 藥水
                c = Over(c, new Color(1f, 1f, 1f, 0.75f), Cov(Circle(p, body + new Vector2(-r * 0.4f, r * 0.35f), r * 0.16f)));   // 反光
                c = Over(c, corkC, Cov(corkD));                                   // 軟木塞
                px[y * S + x] = c;
            }
            t.SetPixels(px); t.Apply(false, true); return t;
        }

        static Texture2D Scroll()
        {
            var t = New(); var px = new Color[S * S];
            var paper = new Color(0.96f, 0.86f, 0.62f, 1f); var roll = new Color(0.8f, 0.62f, 0.36f, 1f); var outline = new Color(0.1f, 0.07f, 0.04f, 1f);
            var ink = new Color(0.45f, 0.25f, 0.12f, 1f); var seal = new Color(0.85f, 0.15f, 0.15f, 1f);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                var p = new Vector2((x + 0.5f) / S, (y + 0.5f) / S);
                float sheet = Box(p, new Vector2(0.5f, 0.5f), new Vector2(0.28f, 0.3f));
                float top = Box(p, new Vector2(0.5f, 0.82f), new Vector2(0.36f, 0.07f), 0.07f);
                float bot = Box(p, new Vector2(0.5f, 0.18f), new Vector2(0.36f, 0.07f), 0.07f);
                float all = Mathf.Min(sheet, Mathf.Min(top, bot));
                var c = new Color(0, 0, 0, 0);
                c = Over(c, outline, Cov(all - 0.035f));
                c = Over(c, paper, Cov(sheet));
                for (int i = 0; i < 3; i++) c = Over(c, ink, Cov(Box(p, new Vector2(0.47f, 0.62f - i * 0.11f), new Vector2(0.17f - i * 0.03f, 0.012f))));
                c = Over(c, roll, Cov(top)); c = Over(c, roll, Cov(bot));
                c = Over(c, seal, Cov(Circle(p, new Vector2(0.66f, 0.34f), 0.075f)));
                px[y * S + x] = c;
            }
            t.SetPixels(px); t.Apply(false, true); return t;
        }
    }
}
