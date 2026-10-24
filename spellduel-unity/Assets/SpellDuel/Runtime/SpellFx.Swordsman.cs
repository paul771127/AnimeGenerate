using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 劍士招式的華麗版特效（全部程式產生）：每一招的出手、命中都不一樣，再加上瞬間的點光源照亮周圍。
    ///   斬擊：金色雙重新月（主刀＋兩道殘影）＋交叉第二刀、正面衝擊環；命中＝X 字斬痕＋殘留的刀光。
    ///   突刺：沿著出手方向射出的光槍（白芯＋金色外光＋速度線）＋一節一節的螺旋光環；命中＝放射光芒＋內縮再外擴的環。
    ///   劍氣：大片藍白新月（三層殘影）＋羽狀光點；命中＝寬弧衝擊＋三圈漣漪。
    ///   格擋／鐵壁：正面六角光盾；反擊：旋轉的粉紅鋸齒環＋放射短刀光；嘲諷：紅色怒吼衝擊環。
    /// </summary>
    public static partial class SpellFx
    {
        /// <summary>劍士技能的出手特效樣式（CastBurst 的 classId）；不是基本劍士招式＝"swordsman"（原本的）</summary>
        public static string SwordStyle(string skillId) => skillId switch
        {
            "slash" => "sword_slash", "thrust" => "sword_thrust", "wave" => "sword_wave",
            "block" => "sword_guard", "ironwall" => "sword_guard", "counter" => "sword_counter", "taunt" => "sword_taunt",
            _ => "swordsman",
        };
        static bool IsSwordStyle(string id) => id != null && id.StartsWith("sword_");

        static readonly Color SwGold = new Color(1f, 0.84f, 0.42f), SwSteel = new Color(0.75f, 0.88f, 1f), SwBlue = new Color(0.45f, 0.7f, 1f);

        /// <summary>瞬間點光源：照亮附近的 3D 物件，life 秒內淡掉</summary>
        static void LightFlash(Transform root, Color c, float range, float intensity, float life)
        {
            var go = new GameObject("Flash Light");
            go.transform.SetParent(root, false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
            go.AddComponent<LightPulse>().Init(l, intensity, life);
        }

        /// <summary>沿著本地 Z（出手方向）的一道光（面向鏡頭），長度靠縮放 s0 → s1</summary>
        static readonly Vector3[] ForwardLine = { Vector3.zero, new Vector3(0f, 0f, 1f) };

        static void SwordCast(FxAnim anim, Transform root, string style, Color skillColor, float s)
        {
            var light = Color.Lerp(skillColor, Color.white, 0.5f);
            var core = NewPS(root, "Flash", AddMat(TexSoft), 3, false);
            Flash(core, light, 0.6f * s, 0.18f);
            core.Emit(2);
            switch (style)
            {
                case "sword_slash":
                {
                    var tilt = new GameObject("SlashTilt").transform; tilt.SetParent(root, false); tilt.localRotation = Quaternion.Euler(0f, 0f, -18f);
                    // 主刀：金色寬弧＋白芯；兩道殘影（晚一點、大一點、淡一點）
                    anim.AddArc(tilt, Arc(28, 1f, 160f), 0.12f * s, Color.Lerp(SwGold, skillColor, 0.3f), 0f, 0.32f, 0.35f * s, 0.75f * s, 70f);
                    anim.AddArc(tilt, Arc(28, 0.95f, 140f), 0.045f * s, Color.white, 0f, 0.26f, 0.35f * s, 0.75f * s, 70f);
                    anim.AddArc(tilt, Arc(28, 1f, 150f), 0.08f * s, WithA(SwGold, 0.55f), 0.04f, 0.3f, 0.42f * s, 0.85f * s, 70f);
                    anim.AddArc(tilt, Arc(28, 1f, 140f), 0.06f * s, WithA(SwSteel, 0.35f), 0.08f, 0.3f, 0.5f * s, 0.95f * s, 70f);
                    // 交叉的第二刀
                    var tilt2 = new GameObject("SlashTilt2").transform; tilt2.SetParent(root, false); tilt2.localRotation = Quaternion.Euler(0f, 0f, 28f);
                    anim.AddArc(tilt2, Arc(28, 1f, 150f), 0.1f * s, Color.Lerp(SwGold, Color.white, 0.3f), 0.07f, 0.3f, 0.35f * s, 0.8f * s, -70f);
                    anim.AddArc(tilt2, Arc(28, 0.95f, 130f), 0.04f * s, Color.white, 0.07f, 0.24f, 0.35f * s, 0.8f * s, -70f);
                    anim.AddRing(root, Circle(48, 1f), true, 0.03f * s, SwGold, 0.02f, 0.28f, 0.1f * s, 0.8f * s);
                    SwordSparks(root, SwGold, s, 30, 35f);
                    Stars(root, Color.Lerp(SwGold, Color.white, 0.4f), s, 7, 0.3f, 0.14f, 0.26f);
                    LightFlash(root, SwGold, 2.5f * s, 3f, 0.25f);
                    break;
                }
                case "sword_thrust":
                {
                    // 光槍：外光、白芯、四條速度線
                    anim.AddArc(root, ForwardLine, 0.16f * s, WithA(SwGold, 0.8f), 0f, 0.26f, 0.3f * s, 1.6f * s, 0f);
                    anim.AddArc(root, ForwardLine, 0.05f * s, Color.white, 0f, 0.22f, 0.3f * s, 1.7f * s, 0f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f + 0.4f;
                        var off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.09f * s;
                        var sl = new GameObject("SpeedLine").transform; sl.SetParent(root, false); sl.localPosition = off;
                        anim.AddArc(sl, ForwardLine, 0.012f * s, WithA(Color.white, 0.8f), 0.02f + 0.015f * i, 0.18f, 0.2f * s, 1.1f * s, 0f);
                    }
                    // 一節一節往前的螺旋光環
                    for (int i = 0; i < 4; i++)
                    {
                        var h = new GameObject("SpiralRing").transform; h.SetParent(root, false); h.localPosition = new Vector3(0f, 0f, (0.25f + 0.3f * i) * s);
                        anim.AddRing(h, Zigzag(20, 0.8f, 1f), true, 0.016f * s, Color.Lerp(SwGold, Color.white, 0.25f * i), 0.03f * i, 0.22f, 0.06f * s, 0.2f * s, true, 120f);
                    }
                    var tip = NewPS(root, "TipSparks", AddMat(TexSoft), 24, true);
                    var m = tip.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.28f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(4f * s, 9f * s);
                    m.startSize = 0.014f * s;
                    m.startColor = new ParticleSystem.MinMaxGradient(SwGold, Color.white);
                    Cone(tip, 10f, 0.03f * s); FadeOut(tip); Stretch(tip, 0.05f, 2f);
                    tip.Play(); tip.Emit(22);
                    LightFlash(root, SwGold, 2f * s, 2.6f, 0.2f);
                    break;
                }
                case "sword_wave":
                {
                    var blue = Color.Lerp(skillColor, SwBlue, 0.4f);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = 1f - 0.3f * i;
                        anim.AddArc(root, Arc(32, 1f, 170f), (0.16f - 0.03f * i) * s, WithA(Color.Lerp(blue, Color.white, 0.2f * i), a), 0.035f * i, 0.36f, (0.4f + 0.1f * i) * s, (1.1f + 0.15f * i) * s, 30f);
                    }
                    anim.AddArc(root, Arc(32, 0.96f, 150f), 0.05f * s, Color.white, 0f, 0.28f, 0.4f * s, 1.1f * s, 30f);
                    anim.AddRing(root, Circle(48, 1f), true, 0.025f * s, blue, 0.03f, 0.32f, 0.15f * s, 1f * s);
                    // 羽狀光點往前散開
                    var fe = NewPS(root, "Feathers", AddMat(TexSoft), 40, true);
                    var m = fe.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * s, 4f * s);
                    m.startSize = new ParticleSystem.MinMaxCurve(0.015f * s, 0.035f * s);
                    m.startColor = new ParticleSystem.MinMaxGradient(blue, Color.white);
                    Cone(fe, 55f, 0.1f * s); FadeOut(fe); Drag(fe, 2f);
                    fe.Play(); fe.Emit(36);
                    LightFlash(root, SwBlue, 2.5f * s, 2.5f, 0.3f);
                    break;
                }
                case "sword_guard":
                {
                    // 正面六角光盾：外框、內框、六道放射線
                    var c = Color.Lerp(skillColor, SwSteel, 0.5f);
                    anim.AddRing(root, Polygon(6, 1f, 90f), true, 0.03f * s, c, 0f, 0.5f, 0.2f * s, 0.6f * s);
                    anim.AddRing(root, Polygon(6, 0.7f, 90f), true, 0.018f * s, Color.white, 0.04f, 0.45f, 0.2f * s, 0.6f * s);
                    for (int i = 0; i < 6; i++) anim.AddSlash(root, new[] { Vector3.zero, new Vector3(1f, 0f, 0f) }, 0.02f * s, WithA(c, 0.8f), 0.05f, 0.35f, 0.1f * s, 0.55f * s, 60f * i + 30f);
                    Stars(root, Color.white, s, 4, 0.35f, 0.12f, 0.22f);
                    LightFlash(root, SwSteel, 2f * s, 2.2f, 0.35f);
                    break;
                }
                case "sword_counter":
                {
                    var pink = new Color(0.98f, 0.45f, 0.7f);
                    anim.AddRing(root, Zigzag(36, 0.82f, 1f), true, 0.022f * s, pink, 0f, 0.45f, 0.25f * s, 0.6f * s, true, 240f);
                    anim.AddRing(root, Circle(40, 1f), true, 0.012f * s, Color.white, 0.05f, 0.4f, 0.2f * s, 0.5f * s);
                    for (int i = 0; i < 8; i++) anim.AddSlash(root, new[] { new Vector3(0.6f, 0f, 0f), new Vector3(1.1f, 0f, 0f) }, 0.03f * s, i % 2 == 0 ? Color.white : pink, 0.08f, 0.25f, 0.3f * s, 0.65f * s, 45f * i);
                    LightFlash(root, pink, 2f * s, 2.2f, 0.3f);
                    break;
                }
                case "sword_taunt":
                {
                    var red = new Color(1f, 0.3f, 0.2f);
                    for (int i = 0; i < 3; i++) anim.AddRing(root, Circle(48, 1f), true, (0.05f - 0.012f * i) * s, i == 0 ? Color.white : red, 0.08f * i, 0.45f, 0.2f * s, (1.3f + 0.3f * i) * s);
                    SwordSparks(root, red, s, 26, 180f);
                    LightFlash(root, red, 3f * s, 3f, 0.4f);
                    break;
                }
            }
        }

        static void SwordSparks(Transform root, Color c, float s, int n, float cone)
        {
            var sp = NewPS(root, "SwordSparks", AddMat(TexSoft), n + 4, true);
            var m = sp.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * s, 6f * s);
            m.startSize = 0.016f * s;
            m.startColor = new ParticleSystem.MinMaxGradient(c, Color.white);
            m.gravityModifier = 0.4f;
            Cone(sp, cone, 0.06f * s); FadeOut(sp); Stretch(sp, 0.05f, 1.6f); Drag(sp, 1.5f);
            sp.Play(); sp.Emit(n);
        }

        /// <summary>劍士招式的命中（面向鏡頭的 XY 平面）</summary>
        static void SwordHit(FxAnim anim, Transform root, string style, Color color, float s)
        {
            var gold = Color.Lerp(color, SwGold, 0.5f);
            switch (style)
            {
                case "sword_slash":
                {
                    var pts = SlashPts();
                    // X 字：兩刀交叉（第二刀晚一點），外金內白
                    anim.AddSlash(root, pts, 0.18f * s, gold, 0f, 0.26f, 0.35f * s, 0.7f * s, -35f);
                    anim.AddSlash(root, pts, 0.07f * s, Color.white, 0f, 0.2f, 0.37f * s, 0.68f * s, -35f);
                    anim.AddSlash(root, pts, 0.16f * s, gold, 0.06f, 0.26f, 0.35f * s, 0.7f * s, 35f);
                    anim.AddSlash(root, pts, 0.06f * s, Color.white, 0.06f, 0.2f, 0.37f * s, 0.68f * s, 35f);
                    // 殘留的細刀光（慢慢淡掉）
                    anim.AddSlash(root, pts, 0.03f * s, WithA(SwGold, 0.7f), 0.1f, 0.6f, 0.68f * s, 0.72f * s, -35f);
                    anim.AddSlash(root, pts, 0.03f * s, WithA(SwGold, 0.7f), 0.12f, 0.6f, 0.68f * s, 0.72f * s, 35f);
                    Sparks(root, new Color(1f, 0.9f, 0.6f), s, 50, 3f, 8f);
                    anim.AddRing(root, Circle(48, 1f), true, 0.05f * s, gold, 0.02f, 0.3f, 0.1f * s, 0.95f * s);
                    Stars(root, new Color(1f, 0.92f, 0.65f), s, 5, 0.3f, 0.16f, 0.3f);
                    LightFlash(root, SwGold, 2.5f * s, 3.5f, 0.25f);
                    break;
                }
                case "sword_thrust":
                {
                    var ray = new[] { Vector3.zero, new Vector3(1f, 0f, 0f) };
                    for (int i = 0; i < 10; i++)
                    {
                        float len = i % 2 == 0 ? 0.75f : 0.45f;
                        anim.AddSlash(root, ray, 0.05f * s, i % 2 == 0 ? Color.white : gold, 0f, 0.2f, 0.1f * s * len, 1.1f * s * len, 36f * i);
                    }
                    anim.AddRing(root, Circle(40, 1f), true, 0.04f * s, gold, 0f, 0.14f, 0.6f * s, 0.1f * s);         // 先往內縮（刺進去）
                    anim.AddRing(root, Circle(40, 1f), true, 0.03f * s, Color.white, 0.12f, 0.25f, 0.1f * s, 0.9f * s); // 再往外爆
                    Sparks(root, gold, s, 34, 3f, 7f);
                    LightFlash(root, SwGold, 2.2f * s, 3f, 0.22f);
                    break;
                }
                case "sword_wave":
                {
                    var blue = Color.Lerp(color, SwBlue, 0.4f);
                    var pts = SlashPts(0.38f);
                    anim.AddSlash(root, pts, 0.22f * s, blue, 0f, 0.3f, 0.45f * s, 0.95f * s, 0f);
                    anim.AddSlash(root, pts, 0.08f * s, Color.white, 0f, 0.22f, 0.47f * s, 0.93f * s, 0f);
                    for (int i = 0; i < 3; i++) anim.AddRing(root, Circle(48, 1f), true, (0.04f - 0.01f * i) * s, i == 0 ? Color.white : blue, 0.06f * i, 0.32f, 0.12f * s, (0.8f + 0.25f * i) * s);
                    Sparks(root, Color.Lerp(blue, Color.white, 0.4f), s, 40, 2.5f, 6f);
                    Stars(root, Color.white, s, 4, 0.3f, 0.14f, 0.24f);
                    LightFlash(root, SwBlue, 2.5f * s, 3f, 0.3f);
                    break;
                }
            }
        }
    }

    /// <summary>點光源的淡出（照亮周圍一下）</summary>
    internal sealed class LightPulse : MonoBehaviour
    {
        Light l; float i0, life, t;
        public void Init(Light light, float intensity, float duration) { l = light; i0 = intensity; life = Mathf.Max(0.05f, duration); }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            if (l) l.intensity = i0 * (1f - k) * (1f - k);
            if (k >= 1f) { if (l) Destroy(l); Destroy(this); }
        }
    }
}
