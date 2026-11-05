using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 危險提示（使用者回報「地上的預警看不清楚，要低頭才看得到」）：自己站在敵人正在蓄力的攻擊範圍裡時，
    /// 畫面朝攻擊來的那一邊發紅光（左、右、背後；正前方＝整圈細紅框），上方一行字，剛進入時手機輕震一下。
    /// 判定：近戰小怪蓄力中、而且自己在牠的扇形攻擊範圍內；Boss 蓄力中的範圍招（圓形或扇形）罩到自己。
    /// </summary>
    public partial class SoloBattle
    {
        static Texture2D dangerGrad, dangerGradR, dangerGradB;
        bool dangerWas;

        void DrawDangerHud(float W, float H)
        {
            if (battle == null || Me == null || !Me.Alive || phase != Phase.Fighting || battle.Over || cam == null) { dangerWas = false; return; }
            bool any = false; float sumL = 0f, sumR = 0f, sumB = 0f, sumF = 0f;
            void Add(Vector3 from)
            {
                var d = WorldFrame.DirFromWorld(Fighter.Flat(from - Me.Feet)); d.y = 0f;
                var cf = cam.transform.forward; cf.y = 0f;
                if (d.sqrMagnitude < 1e-6f || cf.sqrMagnitude < 1e-6f) { sumF += 1f; any = true; return; }
                float a = Vector3.SignedAngle(cf.normalized, d.normalized, Vector3.up);
                if (Mathf.Abs(a) <= 50f) sumF += 1f; else if (Mathf.Abs(a) >= 130f) sumB += 1f; else if (a > 0f) sumR += 1f; else sumL += 1f;
                any = true;
            }
            foreach (var m in battle.minions)
            {
                if (m == null || !m.Alive || m.windupUntil <= 0f || Battle.IsRanged(m.minionKind)) continue;
                float r = Battle.MeleeStrikeRadius(m.minionKind) + Me.bodyRadius;
                if (Battle.FlatDistance(m.Feet, Me.Feet) <= r && Battle.InMeleeFan(m, Me)) Add(m.Feet);
            }
            var en = battle.enemy;
            if (bossMode && en != null && en.Alive && en.charging != null && en.charging.areaRadius > 0f)
            {
                float r = en.charging.areaRadius + Me.bodyRadius + Fighter.BodyRadius;
                bool inside = Battle.FlatDistance(en.Feet, Me.Feet) <= r;
                if (inside && en.charging.arcHalf > 0f) inside = Vector3.Angle(Fighter.Flat(en.forward), Fighter.Flat(Me.Feet - en.Feet)) <= en.charging.arcHalf;
                if (inside) Add(en.Feet);
            }
            if (any && !dangerWas) Haptics.Play(Haptics.Kind.Light);
            dangerWas = any;
            if (!any) return;
            if (!dangerGrad)
            {
                Color C(float k) => new Color(1f, 0.1f, 0.05f, Mathf.Pow(1f - k, 1.6f));
                dangerGrad = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };    // 左邊：最左最紅
                dangerGradR = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };   // 右邊：最右最紅
                dangerGradB = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };   // 下面：最下最紅（貼圖第 0 列畫在下面）
                for (int i = 0; i < 64; i++) { dangerGrad.SetPixel(i, 0, C(i / 63f)); dangerGradR.SetPixel(63 - i, 0, C(i / 63f)); dangerGradB.SetPixel(0, i, C(i / 63f)); }
                dangerGrad.Apply(); dangerGradR.Apply(); dangerGradB.Apply();
            }
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 14f);
            float t = Mathf.Min(W, H) * 0.14f;
            GUI.color = new Color(1f, 1f, 1f, pulse);
            if (sumL > 0f) GUI.DrawTexture(new Rect(0, 0, t, H), dangerGrad);
            if (sumR > 0f) GUI.DrawTexture(new Rect(W - t, 0, t, H), dangerGradR);
            if (sumB > 0f) GUI.DrawTexture(new Rect(0, H - t, W, t), dangerGradB);
            if (sumF > 0f)
            {
                float b = t * 0.25f;
                GUI.color = new Color(1f, 0.15f, 0.1f, 0.35f * pulse);
                GUI.DrawTexture(new Rect(0, 0, W, b), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, H - b, W, b), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(0, 0, b, H), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(W - b, 0, b, H), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
            string where = sumB > 0f ? "背後" : sumL > 0f && sumR > 0f ? "兩側" : sumL > 0f ? "左邊" : sumR > 0f ? "右邊" : "前方";
            int fs = UiKit.BaseFont;
            UiKit.Text(new Rect(0, H * 0.24f, W, fs * 1.8f), $"⚠ {where}的攻擊範圍內！快閃開", fs, new Color(1f, 0.85f, 0.8f), TextAnchor.MiddleCenter, true);
        }
    }
}
