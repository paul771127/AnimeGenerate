using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 命中特效（打中敵人的瞬間）：每個職業一眼就認得出來、要有「打到了」的重量感。
    ///   劍士：金白色大斬痕＋鋼鐵火花＋衝擊波。
    ///   獵人：貫穿的放射星芒＋雙層緊縮環＋羽葉噴散。
    ///   法師：技能色的魔力爆炸＋旋轉六芒星法陣＋魔力碎光。
    ///   刺客：先後兩刀的 X 字斬痕＋暗影煙霧＋血紅火花。
    ///   牧師：從天而降的聖光柱＋十字星芒＋上飄的光點。
    /// 爆擊時更大、多一道星芒與衝擊環。全部面向鏡頭（本地 XY 平面＝畫面）。
    /// </summary>
    public static partial class SpellFx
    {
        /// <summary>技能屬於哪個職業的命中特效（普攻、召喚物也對應到職業）；不認得＝null</summary>
        public static string HitClassOf(SkillDef s)
        {
            if (s == null) return null;
            switch (s.id)
            {
                case "na_mage": case "pet_fire": case "pet_blast": return "mage";
                case "na_sword": case "pet_golem": return "swordsman";
                case "na_arrow": case "pet_falcon": return "archer";
                case "na_holy": return "priest";
                case "na_dagger": case "fogtick": return "assassin";
            }
            foreach (var c in Skills.ClassOrder) if (s.cls == c) return c;
            return null;
        }

        /// <summary>命中特效。power：傷害造成的大小（約 0.8～1.6）</summary>
        public static void Hit(string classId, Color color, Vector3 position, float power, bool crit)
        {
            float s = Mathf.Clamp(power, 0.6f, 2f) * (crit ? 1.35f : 1f);
            color.a = 1f;
            var cam = Camera.main;
            var rot = Quaternion.identity;
            if (cam != null)
            {
                var d = cam.transform.position - position;
                if (d.sqrMagnitude > 1e-6f) rot = Quaternion.LookRotation(d.normalized, cam.transform.up);
            }
            // 往鏡頭方向挪一點，特效才不會被角色身體擋住
            if (cam != null) position += (cam.transform.position - position).normalized * 0.25f;
            var anim = NewAnim("SpellFx Hit " + classId, position, rot, 1.4f);
            var root = anim.transform;
            var light = Color.Lerp(color, Color.white, 0.5f);

            // 共通：極短的白色閃光（打擊瞬間）
            var pop = NewPS(root, "Pop", AddMat(TexSoft), 2, false);
            Flash(pop, Color.white, 0.55f * s, 0.09f);
            pop.Emit(1);

            switch (classId)
            {
                case "swordsman": Sword(anim, root, color, s); break;
                case "archer": Arrow(anim, root, color, s); break;
                case "mage": Arcane(anim, root, color, s); break;
                case "assassin": Shadow(anim, root, color, s); break;
                case "priest": Holy(anim, root, color, s); break;
                default:
                    Sparks(root, light, s, 26, 1.5f, 3.8f);
                    anim.AddRing(root, Circle(40, 1f), true, 0.025f * s, color, 0f, 0.3f, 0.05f * s, 0.6f * s);
                    break;
            }

            if (crit)
            {
                // 爆擊：大顆十字星芒＋白色衝擊環
                Stars(root, Color.white, s, 2, 0.35f, 0.5f, 0.7f);
                anim.AddRing(root, Circle(48, 1f), true, 0.04f * s, Color.white, 0.03f, 0.3f, 0.1f * s, 1.1f * s);
            }
        }

        /// <summary>微彎的斬痕（本地 X 方向，長度 2，中間往上拱）</summary>
        static Vector3[] SlashPts(float bow = 0.22f, int n = 16)
        {
            var p = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float x = -1f + 2f * i / (n - 1);
                p[i] = new Vector3(x, bow * (1f - x * x), 0f);
            }
            return p;
        }

        static void Sword(FxAnim anim, Transform root, Color color, float s)
        {
            var gold = Color.Lerp(color, new Color(1f, 0.85f, 0.4f), 0.5f);
            var pts = SlashPts();
            // 大斬痕：外層金光＋內層白芯，從右上往左下劈
            anim.AddSlash(root, pts, 0.16f * s, gold, 0f, 0.24f, 0.32f * s, 0.62f * s, -32f);
            anim.AddSlash(root, pts, 0.06f * s, Color.white, 0f, 0.18f, 0.34f * s, 0.6f * s, -32f);
            // 鋼鐵火花（又多又快）＋衝擊波
            Sparks(root, new Color(1f, 0.9f, 0.6f), s, 42, 3f, 7f);
            anim.AddRing(root, Circle(48, 1f), true, 0.045f * s, gold, 0.02f, 0.28f, 0.08f * s, 0.85f * s);
            Stars(root, new Color(1f, 0.92f, 0.65f), s, 4, 0.25f, 0.14f, 0.26f);
        }

        static void Arrow(FxAnim anim, Transform root, Color color, float s)
        {
            var green = Color.Lerp(color, new Color(0.6f, 1f, 0.5f), 0.4f);
            var line = new[] { Vector3.zero, new Vector3(1f, 0f, 0f) };
            // 放射星芒（8 道，長短交錯）：箭頭扎進去的感覺
            for (int i = 0; i < 8; i++)
            {
                float len = (i & 1) == 0 ? 0.55f : 0.32f;
                anim.AddSlash(root, line, 0.05f * s, i % 2 == 0 ? Color.white : green, 0f, 0.16f, 0.1f * s * len, 1f * s * len, 45f * i + 22.5f);
            }
            // 兩圈往內縮的環（貫穿）
            anim.AddRing(root, Circle(36, 1f), true, 0.03f * s, green, 0f, 0.18f, 0.55f * s, 0.08f * s);
            anim.AddRing(root, Circle(36, 1f), true, 0.02f * s, Color.white, 0.06f, 0.18f, 0.4f * s, 0.05f * s);
            Sparks(root, green, s, 18, 2.5f, 5f);
            Leaves(root, color, s, 12, 1.6f, false);
        }

        static void Arcane(FxAnim anim, Transform root, Color color, float s)
        {
            var light = Color.Lerp(color, Color.white, 0.45f);
            // 魔力爆炸：技能色的大光球
            var boom = NewPS(root, "Boom", AddMat(TexSoft), 3, false);
            Flash(boom, color, 1.1f * s, 0.32f);
            boom.Emit(2);
            // 旋轉六芒星法陣（兩個三角形＋外圈），一邊放大一邊轉
            anim.AddRing(root, Polygon(3, 1f, 90f), true, 0.022f * s, light, 0f, 0.42f, 0.15f * s, 0.6f * s, true, 120f);
            anim.AddRing(root, Polygon(3, 1f, -90f), true, 0.022f * s, light, 0f, 0.42f, 0.15f * s, 0.6f * s, true, 120f);
            anim.AddRing(root, Circle(48, 1f), true, 0.03f * s, color, 0f, 0.42f, 0.18f * s, 0.68f * s, true, -60f);
            anim.AddRing(root, Zigzag(32, 0.86f, 1f), true, 0.014f * s, light, 0.05f, 0.4f, 0.2f * s, 0.8f * s, true, -90f);
            // 魔力碎光
            Sparks(root, light, s, 26, 1.5f, 4f);
            var sp = NewPS(root, "Sparkles", AddMat(TexStar), 20, true);
            var m = sp.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * s, 1.4f * s);
            m.startSize = new ParticleSystem.MinMaxCurve(0.04f * s, 0.09f * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            m.startColor = new ParticleSystem.MinMaxGradient(color, light);
            Sphere(sp, 0.1f * s);
            SizeCurve(sp, PopCurve);
            Drag(sp, 2f);
            sp.Play(); sp.Emit(16);
        }

        static void Shadow(FxAnim anim, Transform root, Color color, float s)
        {
            var purple = Color.Lerp(color, new Color(0.75f, 0.3f, 1f), 0.4f);
            var blood = new Color(1f, 0.15f, 0.3f);
            var pts = SlashPts(0.08f);
            // X 字兩刀（第二刀晚一點）
            anim.AddSlash(root, pts, 0.12f * s, purple, 0f, 0.2f, 0.3f * s, 0.55f * s, 40f);
            anim.AddSlash(root, pts, 0.04f * s, Color.white, 0f, 0.15f, 0.32f * s, 0.55f * s, 40f);
            anim.AddSlash(root, pts, 0.12f * s, blood, 0.08f, 0.2f, 0.3f * s, 0.55f * s, -40f);
            anim.AddSlash(root, pts, 0.04f * s, Color.white, 0.08f, 0.15f, 0.32f * s, 0.55f * s, -40f);
            // 血紅火花＋暗影煙霧
            Sparks(root, blood, s, 30, 2.5f, 6f);
            var smoke = NewPS(root, "Smoke", BlendMat(TexSoft), 14, true);
            var m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * s, 1f * s);
            m.startSize = new ParticleSystem.MinMaxCurve(0.16f * s, 0.3f * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.03f, 0.16f, 0.7f), new Color(0.25f, 0.06f, 0.3f, 0.55f));
            Sphere(smoke, 0.06f * s);
            FadeOut(smoke);
            SizeCurve(smoke, Grow);
            Drag(smoke, 2f);
            smoke.Play(); smoke.Emit(12);
        }

        static void Holy(FxAnim anim, Transform root, Color color, float s)
        {
            var gold = Color.Lerp(color, new Color(1f, 0.88f, 0.5f), 0.45f);
            var pillar = new[] { new Vector3(0f, 2.2f, 0f), new Vector3(0f, 1.1f, 0f), new Vector3(0f, -0.25f, 0f) };
            // 從天而降的聖光柱（寬金光＋白芯）
            anim.AddSlash(root, pillar, 0.32f * s, gold, 0f, 0.35f, 1f, 1f, 0f);
            anim.AddSlash(root, pillar, 0.1f * s, Color.white, 0f, 0.28f, 1f, 1f, 0f);
            // 十字星芒
            var bar = new[] { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f) };
            anim.AddSlash(root, bar, 0.07f * s, Color.white, 0.04f, 0.3f, 0.15f * s, 0.45f * s, 0f);
            anim.AddSlash(root, bar, 0.07f * s, Color.white, 0.04f, 0.3f, 0.2f * s, 0.6f * s, 90f);
            var soft = NewPS(root, "SoftBloom", AddMat(TexSoft), 2, false);
            Flash(soft, gold, 0.9f * s, 0.4f);
            soft.Emit(1);
            anim.AddRing(root, Zigzag(28, 0.6f, 1f), true, 0.014f * s, Color.Lerp(gold, Color.white, 0.5f), 0.02f, 0.35f, 0.08f * s, 0.6f * s, true, -30f);
            var sp = NewPS(root, "Motes", AddMat(TexSoft), 28, true);
            var m = sp.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.2f * s, 0.8f * s);
            m.startSize = new ParticleSystem.MinMaxCurve(0.025f * s, 0.06f * s);
            m.startColor = new ParticleSystem.MinMaxGradient(gold, Color.Lerp(gold, Color.white, 0.6f));
            Sphere(sp, 0.15f * s);
            FadeOut(sp);
            SizeCurve(sp, Shrink);
            Rise(sp, 0.7f * s);
            Drag(sp, 1.5f);
            sp.Play(); sp.Emit(24);
        }
    }
}
