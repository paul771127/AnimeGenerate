using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 各職業普攻的強化光影（照劍士那一套）：
    ///   集氣中：職業色的光絲從四周往掌心收攏；集氣完成：白光炸開＋旋轉光芒＋大字。
    ///   出手：畫面上一道三層的光（外暈、亮色、白芯）＋邊緣閃一下＋震動。
    ///     刺客＝紅色往前刺（從手刺向準星，尖端爆開）、忍者＝銀色苦無射出、法師＝紫色光束＋準星雙圈、
    ///     獵人＝綠色箭光直射、牧師＝金色光柱往上衝、初心者＝木頭色斜劃。劍士用自己的（SwipeTrail.cs）。
    /// </summary>
    public partial class SoloBattle
    {
        float basicFxAt = -9f; string basicFxId; Vector2 basicFxFrom;

        static string BasicFxLabel(string id) => id switch
        {
            "na_dagger" => "🗡 刺！", "na_kunai" => "🥷 射！", "na_mage" => "✨ 點！", "na_arrow" => "🏹 放！", "na_holy" => "☀ 舉！", _ => "✋ 揮！",
        };

        /// <summary>普攻出手的那一刻（玩家）：記下來，畫面效果在 OnGUI 畫</summary>
        void TriggerBasicFx(SkillDef s)
        {
            if (s == null || s.id == "na_sword") return;
            basicFxAt = Time.time; basicFxId = s.id;
            float W = Screen.width, H = Screen.height;
            basicFxFrom = Hand != null && Hand.HandVisible ? new Vector2(Hand.Palm.x, H - Hand.Palm.y) : new Vector2(W * 0.72f, H * 0.82f);
            Haptics.Play(Haptics.Kind.Medium); Kick(s.id == "na_dagger" ? 7f : 4f, 0f);
        }

        /// <summary>出手的畫面光影（0.35 秒）</summary>
        void DrawBasicScreenFx()
        {
            float t = Time.time - basicFxAt, life = 0.35f;
            if (t < 0f || t > life || basicFxId == null) return;
            float W = Screen.width, H = Screen.height, m = Mathf.Min(W, H), e = t / life;
            var col = ChargeColor(basicFxId); var light = Color.Lerp(col, Color.white, 0.55f);
            var center = new Vector2(W * 0.5f, H * 0.5f);
            float grow = Mathf.Clamp01(e / 0.3f), fade = 1f - Mathf.Clamp01((e - 0.3f) / 0.7f);
            void Beam(Vector2 a, Vector2 b, float scale)
            {
                var tip = Vector2.Lerp(a, b, grow);
                Seg(a, tip, m * 0.08f * scale * fade, new Color(col.r, col.g, col.b, 0.28f * fade));
                Seg(a, tip, m * 0.032f * scale * fade, new Color(light.r, light.g, light.b, 0.65f * fade));
                Seg(a, tip, m * 0.01f * scale, new Color(1f, 1f, 1f, 0.95f * fade));
            }
            void Burst(Vector2 c, float r, int n)
            {
                if (grow < 1f) return;
                float k = Mathf.Clamp01((e - 0.3f) / 0.5f);
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n + basicFxAt * 3f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Seg(c + d * r * (0.2f + 0.6f * k), c + d * r * (0.5f + 0.9f * k), m * 0.012f, new Color(light.r, light.g, light.b, 0.9f * fade));
                }
                UiKit.Halo(new Rect(c.x - r * (0.4f + k), c.y - r * (0.4f + k), r * (0.8f + 2f * k), r * (0.8f + 2f * k)), new Color(light.r, light.g, light.b, 0.8f * fade), 1f);
            }
            switch (basicFxId)
            {
                case "na_dagger":   // 刺客：紅光往前刺（從手刺到準星），尖端爆開
                    Beam(basicFxFrom, center, 1.15f); Burst(center, m * 0.09f, 10);
                    break;
                case "na_kunai":    // 忍者：銀色苦無直線射出，尾巴一條細光
                    Beam(basicFxFrom, center, 0.7f); Burst(center, m * 0.06f, 6);
                    break;
                case "na_mage":     // 法師：紫色光束＋準星外兩圈魔法環擴散
                {
                    Beam(basicFxFrom, center, 0.8f);
                    float r = m * (0.05f + 0.12f * e);
                    UiKit.Halo(new Rect(center.x - r, center.y - r, r * 2, r * 2), new Color(light.r, light.g, light.b, 0.9f * fade), 1f);
                    float r2 = r * 1.5f; UiKit.Halo(new Rect(center.x - r2, center.y - r2, r2 * 2, r2 * 2), new Color(col.r, col.g, col.b, 0.5f * fade), 0.6f);
                    break;
                }
                case "na_arrow":    // 獵人：綠色箭光（細長、很快），準星處一圈
                    Beam(basicFxFrom, center, 0.6f); Burst(center, m * 0.05f, 4);
                    break;
                case "na_holy":     // 牧師：金色光柱從手往上衝出畫面，準星一圈光環
                    Beam(basicFxFrom, new Vector2(basicFxFrom.x, -m * 0.1f), 1.3f);
                    Burst(center, m * 0.07f, 8);
                    break;
                default:            // 初心者：木頭色斜劃
                    Beam(new Vector2(W * 0.8f, H * 0.3f), new Vector2(W * 0.2f, H * 0.7f), 0.9f);
                    break;
            }
            if (e < 0.45f) EdgeFlash(col, 1f - e / 0.45f);
        }

        /// <summary>集氣中／集氣完成的光環（劍士以外的職業，疊在各自的手掌圖上）</summary>
        void DrawChargeAura(string id, Vector2 c, float r, float k, bool ready, float since, int fs)
        {
            var col = ChargeColor(id); var light = Color.Lerp(col, Color.white, 0.55f);
            Rect Sq(float rad) => new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2);
            if (!ready)
            {
                int n = 4 + Mathf.RoundToInt(8 * k);
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n + Time.time * 1.7f;
                    float ph = Mathf.Repeat(Time.time * 1.6f + i * 0.37f, 1f);
                    float outR = r * Mathf.Lerp(2.4f, 0.5f, ph), inR = outR - r * 0.5f;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Seg(c + d * outR, c + d * Mathf.Max(r * 0.2f, inR), r * 0.06f, new Color(light.r, light.g, light.b, (0.25f + 0.6f * k) * (1f - ph * 0.6f)));
                }
                UiKit.Halo(Sq(r * Mathf.Lerp(2.2f, 1.1f, k)), new Color(col.r, col.g, col.b, 0.25f + 0.5f * k), 0.7f);
                return;
            }
            if (since < 0.5f) { float e = since / 0.5f; UiKit.Halo(Sq(r * (1.1f + 2.4f * e)), new Color(light.r, light.g, light.b, 1f - e), 1f); UiKit.Disc(Sq(r * (1.5f - 0.5f * e)), new Color(1f, 1f, 1f, 0.5f * (1f - e))); }
            UiKit.Halo(Sq(r * 1.5f), new Color(light.r, light.g, light.b, 0.8f + 0.2f * Mathf.Sin(Time.time * 7f)), 1f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + Time.time * 2.2f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float len = r * (0.5f + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 6f + i)));
                Seg(c + d * r * 1.55f, c + d * (r * 1.55f + len), r * 0.07f, new Color(light.r, light.g, light.b, 0.85f));
            }
            UiKit.Text(new Rect(c.x - r * 1.5f, c.y + r * 1.8f, r * 3, fs * 2f), BasicFxLabel(id), Mathf.RoundToInt(fs * 1.5f), light, TextAnchor.MiddleCenter, true);
        }
    }
}
