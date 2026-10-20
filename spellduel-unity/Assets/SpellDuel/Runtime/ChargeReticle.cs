using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 畫面中央的準星＋蓄力進度（合在一起）：所有技能都朝準星放，蓄力進度畫成準星外圈的一圈刻度。
    ///   ・沒在蓄力：白色十字準星。
    ///   ・蓄力中：外圈刻度依進度順時針亮起（技能顏色）。
    ///   ・蓄滿：整圈閃爍（白色 ↔ 技能顏色），準星張大一點。
    ///   ・超蓄（獵人蓄力強化 100%→200%）：外面再一圈金色刻度。
    /// 美術（Codex）可以只改這個檔的畫法；呼叫端只用 Draw()。
    /// </summary>
    public static class ChargeReticle
    {
        /// <summary>
        /// 畫準星。center：畫面座標（GUI，y 向下）；size：基準大小（像素，約畫面寬 4%）；
        /// charging：有沒有在蓄力；progress：0～1；color：技能顏色；overcharge：超蓄 0～1。
        /// </summary>
        public static void Draw(Vector2 center, float size, bool charging, float progress, Color color, float overcharge = 0f)
        {
            bool ready = charging && progress >= 1f;
            float t = Time.time;
            float flash = ready ? 0.5f + 0.5f * Mathf.Sin(t * 14f) : 0f;            // 蓄滿：快速閃爍
            var main = !charging ? Color.white : ready ? Color.Lerp(color, Color.white, flash) : color;
            float arm = size * (ready ? 1.2f + 0.08f * flash : 1f), th = Mathf.Max(3f, size * 0.15f), gap = size * 0.3f;

            // 十字（黑邊＋顏色）
            Cross(center, arm, th + 2f, gap - 1f, new Color(0f, 0f, 0f, 0.45f));
            Cross(center, arm, th, gap, new Color(main.r, main.g, main.b, charging ? 0.95f : 0.8f));
            Rect(center, th, th, new Color(main.r, main.g, main.b, 0.95f));

            if (!charging) return;
            // 外圈刻度：已蓄的亮、沒蓄的暗
            const int N = 32;
            float radius = size * 1.75f, tick = Mathf.Max(4f, size * 0.32f), tw = Mathf.Max(3f, size * 0.12f);
            int lit = Mathf.FloorToInt(Mathf.Clamp01(progress) * N + 0.0001f);
            for (int i = 0; i < N; i++)
            {
                float ang = i * 360f / N;
                bool on = i < lit;
                var c = on ? (ready ? Color.Lerp(color, Color.white, flash) : color) : new Color(1f, 1f, 1f, 0.22f);
                if (on && !ready && i == lit - 1) c = Color.Lerp(c, Color.white, 0.5f);   // 進度的前端亮一點
                Tick(center, ang, radius, tick, tw, new Color(0f, 0f, 0f, on ? 0.5f : 0.25f), 2f);
                Tick(center, ang, radius, tick, tw, c, 0f);
            }
            // 超蓄：外面一圈金色
            if (overcharge > 0f)
            {
                var gold = new Color(1f, 0.82f, 0.3f);
                int litO = Mathf.FloorToInt(Mathf.Clamp01(overcharge) * N);
                for (int i = 0; i < litO; i++) Tick(center, i * 360f / N, radius + tick * 1.3f, tick * 0.7f, tw, gold, 0f);
            }
            // 蓄滿：外圈往外擴散的光圈
            if (ready)
            {
                float k = (t * 1.6f) % 1f;
                var rc = new Color(color.r, color.g, color.b, (1f - k) * 0.6f);
                for (int i = 0; i < N; i += 2) Tick(center, i * 360f / N + 5f, radius + tick + k * size * 1.2f, tick * 0.5f, tw * 0.8f, rc, 0f);
            }
        }

        static void Cross(Vector2 c, float arm, float th, float gap, Color col)
        {
            float len = arm - gap;
            Rect(new Vector2(c.x - gap - len / 2f, c.y), len, th, col);
            Rect(new Vector2(c.x + gap + len / 2f, c.y), len, th, col);
            Rect(new Vector2(c.x, c.y - gap - len / 2f), th, len, col);
            Rect(new Vector2(c.x, c.y + gap + len / 2f), th, len, col);
        }

        static void Rect(Vector2 c, float w, float h, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - w / 2f, c.y - h / 2f, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>一根刻度：從圓心往外、角度 ang（0＝正上方，順時針），在半徑 r 處長 len、寬 w；grow＝黑邊加粗</summary>
        static void Tick(Vector2 c, float ang, float r, float len, float w, Color col, float grow)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, c);
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - (w + grow) / 2f, c.y - r - len / 2f - grow / 2f, w + grow, len + grow), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.matrix = m;
        }
    }
}
