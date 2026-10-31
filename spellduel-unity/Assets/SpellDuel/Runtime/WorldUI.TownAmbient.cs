using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 城鎮 2D 俯瞰圖的環境動態（使用者回報城鎮看起來太靜態）：全部程式畫，不用新圖片。
    ///   ・居民：呼吸（腳底不動、身體微微伸縮）、輕輕晃，偶爾轉頭看另一邊。
    ///   ・雲影：幾片很淡的影子慢慢飄過整張地圖。
    ///   ・每個城鎮的氛圍粒子：炎之里鍛爐火星、森之里落葉與螢火蟲、潮騷港海鷗、赤砂城與風沙驛飄沙；鐵匠鋪冒煙。
    /// 粒子位置由時間與編號算出來（不存狀態、不配置記憶體）；只畫在看得到的範圍。
    /// </summary>
    public partial class WorldUI
    {
        static float Hash01(int i, int salt) { unchecked { uint h = (uint)(i * 374761393 + salt * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h & 65535) / 65535f; } }

        /// <summary>居民的小圖：呼吸、晃動、偶爾轉頭（取代原本的靜止圖）</summary>
        static void DrawTownNpc(Rect body, Texture2D spr, int i)
        {
            float t = Time.unscaledTime;
            float breath = Mathf.Sin(t * 2.1f + i * 1.7f);
            float sway = Mathf.Sin(t * 0.9f + i * 2.3f) * body.width * 0.015f;
            // 照比例放進框裡、腳底對齊框底
            float aspect = spr.width / (float)Mathf.Max(1, spr.height);
            float h = body.height, w = h * aspect;
            if (w > body.width) { w = body.width; h = w / aspect; }
            h *= 1f + 0.022f * breath; w *= 1f - 0.008f * breath;
            var r = new Rect(body.center.x - w / 2f + sway, body.yMax - h, w, h);
            // 每 7～11 秒轉頭看另一邊 2.5 秒
            float period = 7f + Hash01(i, 3) * 4f, ph = Mathf.Repeat(t + Hash01(i, 5) * period, period);
            bool flip = ph < 2.5f;
            GUI.DrawTextureWithTexCoords(r, spr, flip ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f), true);
        }

        /// <summary>地圖上的環境動態（畫在底圖上面、名牌與居民下面）。visible＝看得到的範圍（地圖內容座標）</summary>
        void DrawTownAmbient(int town, TownMapData d, float s, Rect content, Rect visible)
        {
            if (Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime;
            var prev = GUI.color;
            // ---- 雲影：3 片，很慢地從左往右飄（跨過整張地圖）
            for (int k = 0; k < 3; k++)
            {
                float cw = content.width * (0.35f + 0.15f * Hash01(k, 11)), ch = cw * 0.45f;
                float speed = content.width / (90f + 40f * Hash01(k, 12));
                float x = Mathf.Repeat(t * speed + Hash01(k, 13) * (content.width + cw), content.width + cw) - cw;
                float y = content.height * (0.15f + 0.6f * Hash01(k, 14));
                var cr = new Rect(x, y, cw, ch);
                if (!cr.Overlaps(visible)) continue;
                UiKit.Disc(cr, new Color(0.05f, 0.08f, 0.15f, 0.07f));
                UiKit.Disc(new Rect(cr.x + cw * 0.2f, cr.y - ch * 0.15f, cw * 0.55f, ch * 0.8f), new Color(0.05f, 0.08f, 0.15f, 0.05f));
            }
            float u = Mathf.Max(6f, visible.width * 0.012f);   // 粒子的基本大小
            switch (town)
            {
                case 0:   // 炎之里：鍛爐飄出的火星（往上飄、一閃一閃）
                    for (int k = 0; k < 26; k++)
                    {
                        float life = 3f + 2f * Hash01(k, 21), a = Mathf.Repeat(t / life + Hash01(k, 22), 1f);
                        float x = visible.x + visible.width * Hash01(k, 23) + Mathf.Sin(t * 1.5f + k) * u * 2f;
                        float y = visible.yMax - visible.height * a * 1.1f;
                        float tw = 0.5f + 0.5f * Mathf.Sin(t * 9f + k * 3f), sz = u * (0.35f + 0.3f * Hash01(k, 24));
                        UiKit.Disc(new Rect(x - sz / 2f, y - sz / 2f, sz, sz), new Color(1f, 0.55f + 0.3f * tw, 0.15f, 0.75f * Mathf.Sin(a * Mathf.PI)));
                    }
                    break;
                case 1:   // 森之里：落葉（斜斜飄下、左右擺）＋螢火蟲（慢慢晃、一明一暗）
                    for (int k = 0; k < 18; k++)
                    {
                        float life = 7f + 4f * Hash01(k, 31), a = Mathf.Repeat(t / life + Hash01(k, 32), 1f);
                        float x = visible.x + visible.width * Hash01(k, 33) + a * visible.width * 0.25f + Mathf.Sin(t * 2f + k) * u * 2.5f;
                        float y = visible.y - u * 2f + (visible.height + u * 4f) * a;
                        float rot = t * 90f * (Hash01(k, 34) - 0.5f) + k * 40f, sz = u * 1.1f;
                        var lr = new Rect(x - sz / 2f, y - sz * 0.3f, sz, sz * 0.6f);
                        var m = GUI.matrix; GUIUtility.RotateAroundPivot(rot, lr.center);
                        UiKit.Disc(lr, Hash01(k, 35) < 0.5f ? new Color(0.45f, 0.72f, 0.25f, 0.85f) : new Color(0.85f, 0.65f, 0.2f, 0.85f));
                        GUI.matrix = m;
                    }
                    for (int k = 0; k < 14; k++)
                    {
                        float x = visible.x + visible.width * (Hash01(k, 36) + 0.04f * Mathf.Sin(t * 0.7f + k));
                        float y = visible.y + visible.height * (0.3f + 0.65f * Hash01(k, 37) + 0.03f * Mathf.Sin(t * 1.1f + k * 2f));
                        float glow = Mathf.Max(0f, Mathf.Sin(t * 1.6f + k * 1.3f)), sz = u * (0.6f + glow * 0.9f);
                        UiKit.Disc(new Rect(x - sz, y - sz, sz * 2f, sz * 2f), new Color(0.85f, 1f, 0.45f, 0.18f * glow));
                        UiKit.Disc(new Rect(x - sz * 0.3f, y - sz * 0.3f, sz * 0.6f, sz * 0.6f), new Color(0.95f, 1f, 0.6f, 0.9f * glow));
                    }
                    break;
                case 2:   // 潮騷港：海鷗飛過（V 字、翅膀拍動）
                    for (int k = 0; k < 6; k++)
                    {
                        float life = 12f + 6f * Hash01(k, 41), a = Mathf.Repeat(t / life + Hash01(k, 42), 1f);
                        float dir = Hash01(k, 43) < 0.5f ? 1f : -1f;
                        float x = dir > 0 ? visible.x - u * 4f + (visible.width + u * 8f) * a : visible.xMax + u * 4f - (visible.width + u * 8f) * a;
                        float y = visible.y + visible.height * (0.1f + 0.5f * Hash01(k, 44)) + Mathf.Sin(t * 0.8f + k) * u * 3f;
                        float flap = Mathf.Sin(t * 7f + k * 2f) * 25f, span = u * 2.2f;
                        DrawWing(new Vector2(x, y), span, -20f - flap, new Color(0.97f, 0.97f, 1f, 0.95f));
                        DrawWing(new Vector2(x, y), span, 200f + flap, new Color(0.97f, 0.97f, 1f, 0.95f));
                    }
                    break;
                default:  // 赤砂城、風沙驛：飄沙（橫向的細線，一陣一陣）
                    float gust = 0.5f + 0.5f * Mathf.Sin(t * 0.4f);
                    for (int k = 0; k < 22; k++)
                    {
                        float life = 2.5f + 2f * Hash01(k, 51), a = Mathf.Repeat(t / life + Hash01(k, 52), 1f);
                        float x = visible.x - u * 6f + (visible.width + u * 12f) * a;
                        float y = visible.y + visible.height * Hash01(k, 53) + Mathf.Sin(t * 2f + k) * u;
                        float len = u * (3f + 4f * Hash01(k, 54));
                        GUI.color = new Color(0.95f, 0.82f, 0.55f, (0.18f + 0.3f * gust) * Mathf.Sin(a * Mathf.PI));
                        GUI.DrawTexture(new Rect(x, y, len, Mathf.Max(1.5f, u * 0.18f)), Texture2D.whiteTexture);
                    }
                    break;
            }
            GUI.color=Color.white;DrawTownLife(town,d,s,visible,t);
            GUI.color = prev;
        }

        static void DrawWing(Vector2 at, float len, float angle, Color c)
        {
            var m = GUI.matrix; var prev = GUI.color;
            GUIUtility.RotateAroundPivot(angle, at);
            GUI.color = c; GUI.DrawTexture(new Rect(at.x, at.y - len * 0.07f, len, len * 0.14f), Texture2D.whiteTexture);
            GUI.matrix = m; GUI.color = prev;
        }
    }
}
