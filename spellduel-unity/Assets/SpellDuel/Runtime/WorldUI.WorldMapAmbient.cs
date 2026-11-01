using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 世界地圖的環境動態（使用者回報世界地圖看起來是純靜態、很無聊）：全部程式畫，不用新圖片。
    /// 座標是地圖單位（每個區域 2160 寬、整張 1920 高），乘上 s 變成畫面；只畫看得到、而且雲霧還沒蓋住的範圍。
    ///   ・全圖：雲影慢慢飄過、偶爾一群鳥飛過。
    ///   ・區域一（火山）：火山口冒煙、熔岩地飄火星。
    ///   ・區域二（森林）：螢火蟲、落葉、紫色大樹的光點。
    ///   ・區域三（海港）：海面的反光一閃一閃、漩渦轉圈、海鷗。
    ///   ・區域四、五（角鬥場、峽谷）：一陣一陣的風沙。
    /// 粒子位置都由時間與編號算出來（不存狀態、不配置記憶體）。
    /// </summary>
    public partial class WorldUI
    {
        // 地圖上的位置（地圖單位，照底圖畫的）
        static readonly Vector2[] WmVolcanoes = { new Vector2(450f, 330f), new Vector2(960f, 300f), new Vector2(1860f, 250f) };
        static readonly Rect WmLava = new Rect(1350f, 700f, 700f, 600f);
        static readonly Rect WmForest = new Rect(2300f, 500f, 1900f, 1300f);
        static readonly Vector2 WmPurpleTree = new Vector2(3790f, 680f);
        static readonly Rect WmSea = new Rect(4350f, 450f, 2000f, 1300f);
        static readonly Vector2 WmWhirlpool = new Vector2(5950f, 1060f);
        static readonly Rect WmDesert = new Rect(6500f, 400f, 4300f, 1500f);

        /// <summary>畫在底圖與路上面、雲霧與地點下面。viewX／viewW：看得到的範圍（畫面座標）；front：雲霧前線（地圖單位）</summary>
        void DrawWorldMapAmbient(float s, float viewX, float viewW, float front, float now, int fs)
        {
            if (Event.current.type != EventType.Repaint) return;
            float x0 = viewX / s - 300f, x1 = (viewX + viewW) / s + 300f;   // 看得到的範圍（地圖單位，多留一點邊）
            float fogX = front + 220f;                                        // 這裡右邊是雲霧
            float hi = Mathf.Min(x1, fogX);
            if (hi <= x0) return;
            var keep = GUI.color;
            float u = Mathf.Max(3f, fs * 0.22f);   // 粒子的基本大小（畫面像素）
            bool Vis(float x) => x >= x0 && x <= hi;

            // ---- 雲影：大片很淡的影子，慢慢從左往右飄
            for (int k = 0; k < 7; k++)
            {
                float span = 10800f + 1600f, speed = 22f + 10f * Hash01(k, 61);
                float x = Mathf.Repeat(now * speed + Hash01(k, 62) * span, span) - 800f;
                if (!Vis(x) && !Vis(x + 700f)) continue;
                float y = 300f + 1300f * Hash01(k, 63), w = 700f + 400f * Hash01(k, 64);
                UiKit.Disc(new Rect(x * s, y * s, w * s, w * 0.42f * s), new Color(0.05f, 0.08f, 0.16f, 0.08f));
                UiKit.Disc(new Rect((x + w * 0.25f) * s, (y - w * 0.08f) * s, w * 0.55f * s, w * 0.3f * s), new Color(0.05f, 0.08f, 0.16f, 0.06f));
            }

            // ---- 區域一：火山的煙（一團一團往上冒、越高越大越淡）、熔岩地的火星
            foreach (var v in WmVolcanoes)
            {
                if (!Vis(v.x)) continue;
                for (int k = 0; k < 9; k++)
                {
                    float life = 6f + 2f * Hash01(k, 71), a = Mathf.Repeat(now / life + k / 9f, 1f);
                    float sz = (60f + 220f * a) * s, drift = (40f + 120f * Hash01(k, 72)) * a;
                    var c = new Vector2(v.x + drift + Mathf.Sin(now * 0.6f + k) * 20f, v.y - 420f * a) * s;
                    float al = 0.32f * Mathf.Sin(Mathf.Clamp01(a * 1.3f) * Mathf.PI) * (1f - a * 0.5f);
                    UiKit.Disc(new Rect(c.x - sz / 2f, c.y - sz / 2f, sz, sz), new Color(0.32f, 0.29f, 0.3f, al));
                }
            }
            if (Vis(WmLava.x) || Vis(WmLava.xMax))
                for (int k = 0; k < 22; k++)
                {
                    float life = 2.5f + 2f * Hash01(k, 73), a = Mathf.Repeat(now / life + Hash01(k, 74), 1f);
                    float x = WmLava.x + WmLava.width * Hash01(k, 75) + Mathf.Sin(now * 2f + k) * 12f, y = WmLava.yMax - WmLava.height * a * 1.2f;
                    if (!Vis(x)) continue;
                    float tw = 0.5f + 0.5f * Mathf.Sin(now * 10f + k * 3f), sz = u * (0.6f + 0.6f * Hash01(k, 76));
                    UiKit.Disc(new Rect(x * s - sz / 2f, y * s - sz / 2f, sz, sz), new Color(1f, 0.5f + 0.35f * tw, 0.12f, 0.9f * Mathf.Sin(a * Mathf.PI)));
                }

            // ---- 區域二：螢火蟲、落葉、紫色大樹的光點
            if (Vis(WmForest.x) || Vis(WmForest.xMax))
            {
                for (int k = 0; k < 26; k++)
                {
                    float x = WmForest.x + WmForest.width * Hash01(k, 81) + Mathf.Sin(now * 0.5f + k) * 40f;
                    float y = WmForest.y + WmForest.height * Hash01(k, 82) + Mathf.Sin(now * 0.7f + k * 2f) * 30f;
                    if (!Vis(x)) continue;
                    float glow = Mathf.Max(0f, Mathf.Sin(now * 1.7f + k * 1.3f)), sz = u * (1f + glow * 1.4f);
                    UiKit.Disc(new Rect(x * s - sz, y * s - sz, sz * 2f, sz * 2f), new Color(0.8f, 1f, 0.45f, 0.2f * glow));
                    UiKit.Disc(new Rect(x * s - sz * 0.3f, y * s - sz * 0.3f, sz * 0.6f, sz * 0.6f), new Color(0.95f, 1f, 0.6f, 0.9f * glow));
                }
                for (int k = 0; k < 14; k++)
                {
                    float life = 8f + 4f * Hash01(k, 83), a = Mathf.Repeat(now / life + Hash01(k, 84), 1f);
                    float x = WmForest.x + WmForest.width * Hash01(k, 85) + a * 260f + Mathf.Sin(now * 2f + k) * 25f, y = WmForest.y + WmForest.height * a;
                    if (!Vis(x)) continue;
                    float rot = now * 120f * (Hash01(k, 86) - 0.5f) + k * 40f, sz = u * 2.2f;
                    var lr = new Rect(x * s - sz / 2f, y * s - sz * 0.3f, sz, sz * 0.55f);
                    var m = GUI.matrix; GUIUtility.RotateAroundPivot(rot, lr.center);
                    UiKit.Disc(lr, Hash01(k, 87) < 0.5f ? new Color(0.45f, 0.72f, 0.25f, 0.85f * Mathf.Sin(a * Mathf.PI)) : new Color(0.9f, 0.62f, 0.2f, 0.85f * Mathf.Sin(a * Mathf.PI)));
                    GUI.matrix = m;
                }
                if (Vis(WmPurpleTree.x))
                    for (int k = 0; k < 12; k++)
                    {
                        float life = 3f + 2f * Hash01(k, 88), a = Mathf.Repeat(now / life + Hash01(k, 89), 1f);
                        float x = WmPurpleTree.x + (Hash01(k, 90) - 0.5f) * 360f, y = WmPurpleTree.y + 120f - 300f * a;
                        float sz = u * (1f + Hash01(k, 91));
                        UiKit.Disc(new Rect(x * s - sz / 2f, y * s - sz / 2f, sz, sz), new Color(0.85f, 0.55f, 1f, 0.8f * Mathf.Sin(a * Mathf.PI)));
                    }
            }

            // ---- 區域三：海面反光、漩渦、海鷗
            if (Vis(WmSea.x) || Vis(WmSea.xMax))
            {
                for (int k = 0; k < 34; k++)
                {
                    float x = WmSea.x + WmSea.width * Hash01(k, 101), y = WmSea.y + WmSea.height * Hash01(k, 102);
                    if (!Vis(x)) continue;
                    float tw = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(now * (1.2f + Hash01(k, 103)) + k * 2.1f)), 6f);
                    if (tw < 0.05f) continue;
                    float len = u * 3f * (0.6f + tw);
                    GUI.color = new Color(1f, 1f, 1f, 0.85f * tw);
                    GUI.DrawTexture(new Rect(x * s - len / 2f, y * s, len, Mathf.Max(1.5f, u * 0.35f)), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(x * s - u * 0.18f, y * s - len * 0.25f, Mathf.Max(1.5f, u * 0.35f), len * 0.5f), Texture2D.whiteTexture);
                }
                GUI.color = keep;
                if (Vis(WmWhirlpool.x))
                    for (int k = 0; k < 18; k++)
                    {
                        float r = (60f + 170f * Hash01(k, 104)), ang = now * (1.6f - r / 260f) + k * 0.9f;
                        var p = WmWhirlpool + new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r * 0.45f);
                        float sz = u * 1.4f;
                        UiKit.Disc(new Rect(p.x * s - sz / 2f, p.y * s - sz / 2f, sz, sz * 0.6f), new Color(0.9f, 1f, 1f, 0.75f));
                    }
                for (int k = 0; k < 5; k++)
                {
                    float life = 14f + 6f * Hash01(k, 105), a = Mathf.Repeat(now / life + Hash01(k, 106), 1f);
                    float dir = Hash01(k, 107) < 0.5f ? 1f : -1f;
                    float x = dir > 0 ? WmSea.x + WmSea.width * a : WmSea.xMax - WmSea.width * a, y = WmSea.y + WmSea.height * (0.15f + 0.6f * Hash01(k, 108)) + Mathf.Sin(now * 0.8f + k) * 40f;
                    if (!Vis(x)) continue;
                    float flap = Mathf.Sin(now * 7f + k * 2f) * 25f, span = u * 4f;
                    DrawWing(new Vector2(x * s, y * s), span, -20f - flap, new Color(1f, 1f, 1f, 0.95f));
                    DrawWing(new Vector2(x * s, y * s), span, 200f + flap, new Color(1f, 1f, 1f, 0.95f));
                }
            }

            // ---- 區域四、五：一陣一陣的風沙（橫向的細線）
            if (Vis(WmDesert.x) || Vis(WmDesert.xMax))
            {
                float gust = 0.5f + 0.5f * Mathf.Sin(now * 0.35f);
                for (int k = 0; k < 40; k++)
                {
                    float life = 3f + 2f * Hash01(k, 111), a = Mathf.Repeat(now / life + Hash01(k, 112), 1f);
                    float x = WmDesert.x + WmDesert.width * Hash01(k, 113) + a * 500f, y = WmDesert.y + WmDesert.height * Hash01(k, 114) + Mathf.Sin(now * 2f + k) * 10f;
                    if (!Vis(x)) continue;
                    float len = u * (5f + 6f * Hash01(k, 115));
                    GUI.color = new Color(1f, 0.86f, 0.6f, (0.15f + 0.3f * gust) * Mathf.Sin(a * Mathf.PI));
                    GUI.DrawTexture(new Rect(x * s, y * s, len, Mathf.Max(1.5f, u * 0.3f)), Texture2D.whiteTexture);
                }
                GUI.color = keep;
            }

            // ---- 全圖：偶爾一群鳥（V 字隊形）從左飛到右
            for (int f = 0; f < 2; f++)
            {
                float life = 26f + 8f * f, a = Mathf.Repeat(now / life + f * 0.5f, 1f);
                float bx = x0 + (hi - x0) * a, by = 250f + 500f * Hash01(f, 121) + Mathf.Sin(now * 0.5f + f) * 50f;
                for (int b = 0; b < 5; b++)
                {
                    float off = (b + 1) / 2 * (b % 2 == 0 ? 1f : -1f);
                    var p = new Vector2((bx - Mathf.Abs(off) * 70f) * s, (by + off * 45f) * s);
                    float flap = Mathf.Sin(now * 8f + b * 1.3f) * 25f, span = u * 2.6f;
                    DrawWing(p, span, -20f - flap, new Color(0.15f, 0.12f, 0.12f, 0.85f));
                    DrawWing(p, span, 200f + flap, new Color(0.15f, 0.12f, 0.12f, 0.85f));
                }
            }
            GUI.color = keep;
        }
    }
}
