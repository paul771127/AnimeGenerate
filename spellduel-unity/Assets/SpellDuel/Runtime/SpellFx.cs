using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 各職業的施法光效（全部用程式即時產生：ParticleSystem＋LineRenderer＋程式畫的粒子貼圖，不需要任何資源檔）。
    ///   單位是真實公尺（AR 世界座標，Y 朝上）。
    ///   法師：腳下／手前的旋轉魔法陣（內外圈、符文鋸齒帶、六芒星）＋往上飄的星光。
    ///   弓箭手：綠色風系，風痕往手（弓）的方向旋轉收束、葉片／羽毛環繞；放箭時銳利的爆環＋速度線。
    ///   刺客：暗紫色煙霧＋閃爍的紫色火花；出手時暗影爆散。
    ///   劍士：金色／鋼色光柱（手前則是劍芒十字）＋閃光星芒；出手時明亮的新月斬擊弧。
    ///   所有效果都會自己清除（播完 Destroy）。粒子數量壓在每個效果約 150 顆以內，適合手機。
    /// </summary>
    public static class SpellFx
    {
        // ================================================================ 對外介面
        /// <summary>詠唱中的光效：跟著 anchor 走，progress 0..1（蓄力進度），完成時（progress>=1）要明顯更亮/脈動。回傳物件由呼叫者每幀更新、結束時 Stop。</summary>
        public static ChargeFx StartCharge(string classId, Color skillColor, Transform anchor, Vector3 localOffset, float scale = 1f)
        {
            var go = new GameObject("SpellFx Charge " + classId);
            var fx = go.AddComponent<ChargeFx>();
            fx.Init(classId, skillColor, anchor, localOffset, scale);
            return fx;
        }

        /// <summary>放招瞬間的爆發光效（world position, direction the spell travels）</summary>
        public static void CastBurst(string classId, Color skillColor, Vector3 position, Vector3 direction, float scale = 1f)
        {
            float s = Mathf.Max(0.05f, scale);
            skillColor.a = 1f;
            if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
            direction.Normalize();
            var up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            var anim = NewAnim("SpellFx Cast " + classId, position, Quaternion.LookRotation(direction, up), 1.3f);
            var root = anim.transform;
            var light = Color.Lerp(skillColor, Color.white, 0.5f);

            // 共用：中心閃光
            var core = NewPS(root, "Flash", AddMat(TexSoft), 3, false);
            Flash(core, light, 0.45f * s, 0.16f);
            core.Emit(2);

            switch (classId)
            {
                case "archer":
                {
                    var wind = Color.Lerp(skillColor, new Color(0.75f, 1f, 0.6f), 0.4f);
                    anim.AddRing(root, Circle(40, 1f), true, 0.014f * s, Color.Lerp(wind, Color.white, 0.4f), 0f, 0.18f, 0.03f * s, 0.45f * s);
                    anim.AddRing(root, Circle(40, 1f), true, 0.008f * s, wind, 0.05f, 0.2f, 0.02f * s, 0.3f * s);
                    // 速度線：沿飛行方向噴出的細長粒子
                    var sp = NewPS(root, "SpeedLines", AddMat(TexSoft), 30, true);
                    var m = sp.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(6f * s, 11f * s);
                    m.startSize = 0.012f * s;
                    m.startColor = new ParticleSystem.MinMaxGradient(Color.white, wind);
                    Cone(sp, 7f, 0.1f * s);
                    FadeOut(sp);
                    Stretch(sp, 0.035f, 2f);
                    sp.Play(); sp.Emit(24);
                    Leaves(root, skillColor, s, 9, 1.4f, true);
                    break;
                }
                case "assassin":
                {
                    var violet = Color.Lerp(skillColor, new Color(0.62f, 0.25f, 1f), 0.5f);
                    // 暗影煙霧爆散（一般混色，才能是「暗」的）
                    var smoke = NewPS(root, "ShadowSmoke", BlendMat(TexSoft), 16, true);
                    var m = smoke.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * s, 1.4f * s);
                    m.startSize = new ParticleSystem.MinMaxCurve(0.14f * s, 0.26f * s);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.03f, 0.16f, 0.75f), new Color(0.22f, 0.08f, 0.32f, 0.6f));
                    Sphere(smoke, 0.05f * s);
                    FadeOut(smoke);
                    SizeCurve(smoke, Grow);
                    Drag(smoke, 2.5f);
                    smoke.Play(); smoke.Emit(14);
                    Sparks(root, violet, s, 28, 2f, 4.5f);
                    anim.AddRing(root, Circle(36, 1f), true, 0.016f * s, violet, 0f, 0.25f, 0.05f * s, 0.5f * s);
                    anim.AddRing(root, Circle(36, 1f), true, 0.03f * s, new Color(0.08f, 0f, 0.14f, 0.7f), 0.03f, 0.35f, 0.05f * s, 0.62f * s, false);
                    break;
                }
                case "swordsman":
                {
                    var gold = new Color(1f, 0.86f, 0.45f);
                    var steel = Color.Lerp(skillColor, Color.white, 0.6f);
                    // 新月斬擊弧：在行進方向前方的水平弧，微微斜切，一邊放大一邊掃過
                    var tilt = new GameObject("SlashTilt").transform;
                    tilt.SetParent(root, false);
                    tilt.localRotation = Quaternion.Euler(0f, 0f, -18f);
                    anim.AddArc(tilt, Arc(24, 1f, 150f), 0.09f * s, Color.Lerp(gold, skillColor, 0.35f), 0f, 0.3f, 0.32f * s, 0.6f * s, 50f);
                    anim.AddArc(tilt, Arc(24, 0.94f, 130f), 0.035f * s, Color.white, 0f, 0.24f, 0.32f * s, 0.6f * s, 50f);
                    Stars(root, Color.Lerp(gold, steel, 0.3f), s, 5, 0.25f, 0.12f, 0.2f);
                    var sp = NewPS(root, "Sparks", AddMat(TexSoft), 24, true);
                    var m = sp.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(2f * s, 4.5f * s);
                    m.startSize = 0.014f * s;
                    m.startColor = new ParticleSystem.MinMaxGradient(gold, Color.white);
                    Cone(sp, 35f, 0.08f * s);
                    FadeOut(sp);
                    Stretch(sp, 0.05f, 1.5f);
                    sp.Play(); sp.Emit(20);
                    break;
                }
                default: // mage（未知職業也用這個）
                {
                    anim.AddRing(root, Circle(48, 1f), true, 0.02f * s, Color.Lerp(skillColor, Color.white, 0.2f), 0f, 0.35f, 0.08f * s, 0.55f * s);
                    anim.AddRing(root, Circle(48, 1f), true, 0.01f * s, light, 0.06f, 0.3f, 0.05f * s, 0.4f * s);
                    var star = new GameObject("Hexagram").transform;
                    star.SetParent(root, false);
                    anim.AddRing(star, Polygon(3, 1f, 90f), true, 0.01f * s, skillColor, 0f, 0.4f, 0.18f * s, 0.36f * s, true, 140f);
                    anim.AddRing(star, Polygon(3, 1f, 270f), true, 0.01f * s, skillColor, 0f, 0.4f, 0.18f * s, 0.36f * s, true, 140f);
                    var sp = NewPS(root, "Sparkles", AddMat(TexSoft), 40, true);
                    var m = sp.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * s, 2.6f * s);
                    m.startSize = new ParticleSystem.MinMaxCurve(0.025f * s, 0.06f * s);
                    m.startColor = new ParticleSystem.MinMaxGradient(skillColor, light);
                    Cone(sp, 40f, 0.06f * s);
                    FadeOut(sp);
                    SizeCurve(sp, Shrink);
                    Drag(sp, 3f);
                    sp.Play(); sp.Emit(36);
                    break;
                }
            }
        }

        /// <summary>給飛行中的法術物件加上職業風格的光暈/拖尾粒子（go 是已存在的球體物件，會被呼叫者移動與銷毀）</summary>
        public static void DecorateProjectile(GameObject go, string classId, Color skillColor, float radius)
        {
            if (go == null) return;
            skillColor.a = 1f;
            float r = Mathf.Max(0.02f, radius);
            // 物件可能被縮放（球體 localScale = 直徑），光效掛在不受縮放影響的子物件上
            var holder = new GameObject("SpellFx Deco").transform;
            holder.SetParent(go.transform, false);
            var ls = go.transform.lossyScale;
            holder.localScale = new Vector3(ls.x > 1e-4f ? 1f / ls.x : 1f, ls.y > 1e-4f ? 1f / ls.y : 1f, ls.z > 1e-4f ? 1f / ls.z : 1f);
            var light = Color.Lerp(skillColor, Color.white, 0.45f);

            // 光暈：跟著球體的柔光（本地空間）
            var halo = NewPS(holder, "Halo", AddMat(TexSoft), 8, false);
            var hm = halo.main;
            hm.startLifetime = 0.22f;
            hm.startSize = new ParticleSystem.MinMaxCurve(r * 3.2f, r * 4.2f);
            hm.startColor = WithA(skillColor, classId == "assassin" ? 0.4f : 0.55f);
            Sphere(halo, 0.001f);
            FadeInOut(halo);
            Rate(halo, 30f);
            halo.Play();

            // 拖尾
            var trail = holder.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = classId == "assassin" ? BlendMat(TexLine) : AddMat(TexLine);
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 2;
            trail.minVertexDistance = 0.03f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.widthCurve = Taper;
            Color tc;
            switch (classId)
            {
                case "archer": trail.time = 0.14f; trail.widthMultiplier = r * 1.4f; tc = Color.Lerp(skillColor, new Color(0.8f, 1f, 0.7f), 0.4f); break;
                case "assassin": trail.time = 0.3f; trail.widthMultiplier = r * 2.2f; tc = new Color(0.25f, 0.08f, 0.38f, 0.8f); break;
                case "swordsman": trail.time = 0.2f; trail.widthMultiplier = r * 2.4f; tc = Color.Lerp(skillColor, new Color(1f, 0.9f, 0.6f), 0.4f); break;
                default: trail.time = 0.3f; trail.widthMultiplier = r * 2f; tc = skillColor; break;
            }
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.Lerp(tc, Color.white, 0.4f), 0f), new GradientColorKey(tc, 1f) },
                      new[] { new GradientAlphaKey(tc.a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;

            // 職業粒子（世界空間，留在飛行路徑上）
            switch (classId)
            {
                case "archer":
                {
                    var lv = NewPS(holder, "Leaves", AddMat(TexLeaf), 14, true);
                    var m = lv.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                    m.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(skillColor, new Color(0.45f, 0.9f, 0.3f), 0.5f), light);
                    Sphere(lv, r);
                    FadeOut(lv);
                    Spin(lv, 6f);
                    RateDist(lv, 5f);
                    lv.Play();
                    break;
                }
                case "assassin":
                {
                    var sm = NewPS(holder, "Smoke", BlendMat(TexSoft), 24, true);
                    var m = sm.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                    m.startSize = new ParticleSystem.MinMaxCurve(r * 2.5f, r * 4f);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.03f, 0.16f, 0.6f), new Color(0.24f, 0.1f, 0.34f, 0.5f));
                    Sphere(sm, r * 0.6f);
                    FadeOut(sm);
                    SizeCurve(sm, Grow);
                    RateDist(sm, 14f);
                    sm.Play();
                    var sk = NewPS(holder, "Sparks", AddMat(TexSoft), 20, true);
                    var k = sk.main;
                    k.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                    k.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
                    k.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f);
                    k.startColor = new ParticleSystem.MinMaxGradient(light, Color.Lerp(skillColor, new Color(0.7f, 0.3f, 1f), 0.5f));
                    Sphere(sk, r);
                    FadeOut(sk);
                    SizeCurve(sk, Flicker);
                    RateDist(sk, 12f);
                    sk.Play();
                    break;
                }
                case "swordsman":
                {
                    var st = NewPS(holder, "Glints", AddMat(TexStar), 10, true);
                    var m = st.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                    m.startSize = new ParticleSystem.MinMaxCurve(r * 1.5f, r * 2.5f);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.6f), Color.white);
                    Sphere(st, r * 1.2f);
                    SizeCurve(st, PopCurve);
                    RateDist(st, 4f);
                    st.Play();
                    break;
                }
                default:
                {
                    var sp = NewPS(holder, "Sparkles", AddMat(TexSoft), 40, true);
                    var m = sp.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
                    m.startSize = new ParticleSystem.MinMaxCurve(r * 0.25f + 0.01f, r * 0.45f + 0.015f);
                    m.startColor = new ParticleSystem.MinMaxGradient(skillColor, light);
                    Sphere(sp, r * 0.9f);
                    FadeOut(sp);
                    SizeCurve(sp, Shrink);
                    Rise(sp, 0.25f);
                    RateDist(sp, 25f);
                    sp.Play();
                    break;
                }
            }
        }

        /// <summary>命中/爆炸光效</summary>
        public static void Impact(string classId, Color color, Vector3 position, float scale = 1f)
        {
            float s = Mathf.Max(0.05f, scale);
            color.a = 1f;
            var cam = Camera.main;
            var rot = Quaternion.identity;
            if (cam != null)
            {
                var d = cam.transform.position - position;
                if (d.sqrMagnitude > 1e-6f) rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            }
            var anim = NewAnim("SpellFx Impact " + classId, position, rot, 1.3f);
            var root = anim.transform;
            var light = Color.Lerp(color, Color.white, 0.45f);

            var core = NewPS(root, "Flash", AddMat(TexSoft), 3, false);
            Flash(core, light, 0.55f * s, 0.15f);
            core.Emit(2);
            Sparks(root, light, s, 26, 1.5f, 3.8f);
            anim.AddRing(root, Circle(40, 1f), true, 0.02f * s, color, 0f, 0.3f, 0.05f * s, 0.6f * s);

            switch (classId)
            {
                case "archer":
                    Leaves(root, color, s, 12, 1.2f, false);
                    break;
                case "assassin":
                {
                    var smoke = NewPS(root, "Smoke", BlendMat(TexSoft), 12, true);
                    var m = smoke.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * s, 0.9f * s);
                    m.startSize = new ParticleSystem.MinMaxCurve(0.15f * s, 0.28f * s);
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.03f, 0.16f, 0.7f), new Color(0.22f, 0.08f, 0.32f, 0.55f));
                    Sphere(smoke, 0.05f * s);
                    FadeOut(smoke);
                    SizeCurve(smoke, Grow);
                    Drag(smoke, 2f);
                    smoke.Play(); smoke.Emit(10);
                    break;
                }
                case "swordsman":
                    Stars(root, new Color(1f, 0.9f, 0.6f), s, 6, 0.28f, 0.1f, 0.2f);
                    break;
                case "mage":
                {
                    var sp = NewPS(root, "Sparkles", AddMat(TexSoft), 24, true);
                    var m = sp.main;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                    m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f * s, 1f * s);
                    m.startSize = new ParticleSystem.MinMaxCurve(0.02f * s, 0.05f * s);
                    m.startColor = new ParticleSystem.MinMaxGradient(color, light);
                    Sphere(sp, 0.1f * s);
                    FadeOut(sp);
                    SizeCurve(sp, Shrink);
                    Rise(sp, 0.5f * s);
                    sp.Play(); sp.Emit(20);
                    break;
                }
            }
        }

        // ================================================================ 飛行中的法術外觀
        /// <summary>
        /// 依技能建立飛行中法術的外觀（取代單純的彩色球）：回傳的物件由呼叫者每幀設定位置、朝向（+Z＝飛行方向），用完 Destroy。
        ///   造型以「法術直徑」D = max(0.06, 2×radius) 為單位（箭、冰槍等細長物沿 +Z 較長，箭頭在判定球的前緣）。
        ///   內部已呼叫 DecorateProjectile（光暈＋拖尾＋職業粒子），呼叫者不用再加。
        ///   網格依技能快取共用；發光部分用疊加材質＋頂點色，實體部分（岩石、刀刃、箭桿）用 Legacy Diffuse（吃場景主光）。
        /// </summary>
        public static GameObject CreateProjectileVisual(SkillDef s, string classId)
        {
            string id = s != null ? s.id : "";
            Color sc = s != null ? s.color : Color.white; sc.a = 1f;
            float radius = s != null ? s.radius : 0.1f;
            float D = Mathf.Max(0.06f, radius * 2f);
            var root = new GameObject("Projectile " + id);
            var body = NewChild(root.transform, "Body");
            body.localScale = Vector3.one * D;
            var anim = root.AddComponent<ProjectileAnim>();
            var light = Color.Lerp(sc, Color.white, 0.55f);

            switch (id)
            {
                case "quickshot":
                case "triple":
                case "snipe":
                {
                    bool snipe = id == "snipe";
                    float len = snipe ? 3.2f : 2.6f;
                    GlowPart(body, id + ".arrowGlow", m => ArrowGlow(m, sc, len, snipe));
                    SolidPart(body, id + ".arrowSolid", m => ArrowSolid(m, len), new[] { Color.Lerp(sc, new Color(0.8f, 0.68f, 0.5f), 0.55f), new Color(0.9f, 0.92f, 0.98f) });
                    CoreGlow(root.transform, light, D * (snipe ? 1.4f : 1.1f), new Vector3(0f, 0f, D * 0.35f));
                    break;
                }
                case "fire":
                {
                    var spin = NewChild(body, "Spin");
                    GlowPart(spin, "fire", m =>
                    {
                        m.Lathe(Prof(-1.6f, 0f, -1f, 0.17f, -0.5f, 0.34f, -0.05f, 0.5f, 0.25f, 0.47f, 0.45f, 0.3f, 0.56f, 0f), 9, (z, j) => new Color(1f, 0.16f, 0.04f, Mathf.Lerp(0.04f, 0.4f, Mathf.InverseLerp(-1.6f, 0.2f, z))), 1.2f, 1.2f, 0f, Jag9);
                        m.Lathe(Prof(-1.3f, 0f, -0.8f, 0.15f, -0.4f, 0.3f, -0.05f, 0.42f, 0.2f, 0.4f, 0.4f, 0.26f, 0.5f, 0f), 9, (z, j) => new Color(1f, 0.5f, 0.08f, Mathf.Lerp(0.1f, 0.8f, Mathf.InverseLerp(-1.3f, 0.2f, z))), 1f, 1f, 20f, Jag9);
                        m.Lathe(SphereProf(0.27f, 6), 8, (z, j) => new Color(1f, 0.93f, 0.62f, 1f));
                    });
                    anim.spin = spin; anim.spinAxis = Vector3.forward; anim.spinSpeed = 260f;
                    anim.flick = spin; anim.flickAmt = 0.12f; anim.flickRate = 23f;
                    CoreGlow(root.transform, new Color(1f, 0.75f, 0.3f), D * 1.5f, Vector3.zero);
                    break;
                }
                case "ice":
                {
                    var spin = NewChild(body, "Spin");
                    GlowPart(spin, "ice", m =>
                    {
                        var p = Prof(-1.4f, 0f, -0.95f, 0.15f, 0.45f, 0.22f, 1.05f, 0f);
                        m.Lathe(p, 6, (z, j) => (j & 1) == 0 ? new Color(0.6f, 0.92f, 1f, 0.85f) : new Color(0.15f, 0.5f, 0.95f, 0.55f));
                        m.Lathe(p, 6, (z, j) => new Color(0.9f, 1f, 1f, 0.75f), 0.4f, 0.4f, 30f);
                        for (int k = 0; k < 3; k++)
                        {
                            float a = k * 120f + 30f;
                            var dir = Quaternion.AngleAxis(a, Vector3.forward) * Vector3.right;
                            var rot = Quaternion.AngleAxis(28f, Vector3.Cross(Vector3.forward, dir)) ;
                            m.Lathe(Prof(-0.45f, 0f, -0.25f, 0.07f, 0.2f, 0.09f, 0.45f, 0f), 5, (z, j) => (j & 1) == 0 ? new Color(0.55f, 0.9f, 1f, 0.8f) : new Color(0.2f, 0.55f, 1f, 0.5f), 1f, 1f, 0f, null, dir * 0.16f + new Vector3(0f, 0f, -0.75f), rot);
                        }
                    });
                    anim.spin = spin; anim.spinAxis = Vector3.forward; anim.spinSpeed = 120f;
                    CoreGlow(root.transform, new Color(0.6f, 0.9f, 1f), D * 1.3f, new Vector3(0f, 0f, D * 0.3f));
                    break;
                }
                case "thunder":
                {
                    var core = NewChild(body, "Core");
                    GlowPart(core, "thunder.core", m => m.Lathe(Prof(-2f, 0f, -1.2f, 0.07f, 0.2f, 0.13f, 0.75f, 0f), 4, (z, j) => new Color(0.95f, 0.9f, 1f, Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(-2f, 0f, z)))));
                    var bolt = NewChild(body, "Bolt");
                    GlowPart(bolt, "thunder.bolt", m => BoltZigzag(m, Color.Lerp(sc, new Color(0.7f, 0.45f, 1f), 0.4f)));
                    anim.flick = core; anim.flickAmt = 0.3f; anim.flickRate = 47f;
                    anim.jitter = bolt; anim.jitterEvery = 0.045f;
                    CoreGlow(root.transform, new Color(0.85f, 0.7f, 1f), D * 1.8f, Vector3.zero);
                    break;
                }
                case "wind":
                {
                    var spin = NewChild(body, "Spin");
                    GlowPart(spin, "wind", m =>
                    {
                        var g = Color.Lerp(sc, new Color(0.6f, 1f, 0.75f), 0.3f);
                        for (int k = 0; k < 3; k++)
                        {
                            float a0 = k * 120f;
                            Whirl(m, 0.42f, a0, 150f, 0.12f, WithA(g, 0.75f), 0f);
                            Whirl(m, 0.42f, a0, 150f, 0.045f, new Color(0.92f, 1f, 0.95f, 0.9f), 0.01f);
                            Whirl(m, 0.22f, a0 + 60f, 130f, 0.07f, WithA(g, 0.6f), -0.1f);
                        }
                    });
                    anim.spin = spin; anim.spinAxis = Vector3.forward; anim.spinSpeed = -720f;
                    CoreGlow(root.transform, sc, D * 1.2f, Vector3.zero);
                    break;
                }
                case "meteor":
                {
                    GlowPart(body, "meteor.tail", m =>
                    {
                        m.Lathe(Prof(-2.6f, 0f, -1.5f, 0.3f, -0.4f, 0.55f, 0.2f, 0.56f, 0.55f, 0f), 9, (z, j) => new Color(1f, 0.2f, 0.06f, Mathf.Lerp(0.03f, 0.45f, Mathf.InverseLerp(-2.6f, 0.2f, z))), 1f, 1f, 0f, Jag9);
                        m.Lathe(Prof(-1.9f, 0f, -1f, 0.24f, -0.2f, 0.45f, 0.3f, 0.45f, 0.52f, 0f), 9, (z, j) => new Color(1f, 0.55f, 0.1f, Mathf.Lerp(0.05f, 0.7f, Mathf.InverseLerp(-1.9f, 0.3f, z))), 1f, 1f, 20f, Jag9);
                    });
                    var rock = NewChild(body, "Rock");
                    SolidPart(rock, "meteor.rock", m => m.Lathe(SphereProf(0.42f, 7), 9, null, 1f, 1f, 0f, null, Vector3.zero, Quaternion.identity, RockWarp), new[] { new Color(0.4f, 0.26f, 0.2f) });
                    GlowPart(rock, "meteor.lava", m => m.Lathe(SphereProf(0.42f, 7), 9, (z, j) => new Color(1f, 0.4f, 0.08f, (j % 3) == 0 ? 0.5f : 0.18f), 1.07f, 1.07f, 0f, null, Vector3.zero, Quaternion.identity, RockWarp));
                    anim.spin = rock; anim.spinAxis = new Vector3(1f, 0.7f, 0.3f).normalized; anim.spinSpeed = 160f;
                    CoreGlow(root.transform, new Color(1f, 0.45f, 0.15f), D * 1.6f, Vector3.zero);
                    break;
                }
                case "backstab":
                case "knife":
                {
                    bool knife = id == "knife";
                    var spin = NewChild(body, "Blade");
                    if (knife) spin.localScale = Vector3.one * 1.6f;   // 飛刀的判定小，刀身相對放大才看得見
                    var edge = knife ? Color.Lerp(sc, new Color(0.8f, 0.5f, 1f), 0.5f) : new Color(1f, 0.12f, 0.25f);
                    GlowPart(spin, id + ".glow", m => DaggerGlow(m, edge, knife));
                    SolidPart(spin, id + ".solid", m => DaggerSolid(m, knife), new[]
                    {
                        new Color(0.88f, 0.9f, 0.97f),
                        knife ? new Color(0.5f, 0.5f, 0.56f) : new Color(0.8f, 0.6f, 0.25f),
                        knife ? new Color(0.22f, 0.12f, 0.26f) : new Color(0.3f, 0.1f, 0.32f),
                        sc,
                    });
                    if (knife) { anim.spin = spin; anim.spinAxis = Vector3.right; anim.spinSpeed = 1080f; }
                    CoreGlow(root.transform, edge, D * 1.1f, Vector3.zero);
                    break;
                }
                case "shadow":
                {
                    BlendPart(body, "shadow.core", m => m.Lathe(SphereProf(0.36f, 7), 10, (z, j) => new Color(0.05f, 0.015f, 0.09f, 0.93f)));
                    var violet = Color.Lerp(sc, new Color(0.55f, 0.25f, 1f), 0.35f);
                    GlowPart(body, "shadow.shell", m => m.Lathe(SphereProf(0.5f, 7), 10, (z, j) => WithA(violet, 0.22f)));
                    var spin = NewChild(body, "Rings");
                    GlowPart(spin, "shadow.rings", m =>
                    {
                        m.Annulus(0.5f, 0.6f, 28, WithA(violet, 0.9f), WithA(violet, 0f), Vector3.zero, Quaternion.Euler(70f, 0f, 0f));
                        m.Annulus(0.44f, 0.52f, 28, WithA(Color.Lerp(violet, Color.white, 0.4f), 0.8f), WithA(violet, 0f), Vector3.zero, Quaternion.Euler(-20f, 60f, 0f));
                    });
                    anim.spin = spin; anim.spinAxis = new Vector3(0.3f, 1f, 0.2f).normalized; anim.spinSpeed = 420f;
                    CoreGlow(root.transform, violet, D * 1.4f, Vector3.zero);
                    break;
                }
                case "poison":
                {
                    var wob = NewChild(body, "Glob");
                    BlendPart(wob, "poison.glob", m => m.Lathe(Prof(-0.95f, 0f, -0.55f, 0.2f, -0.12f, 0.36f, 0.2f, 0.38f, 0.42f, 0.22f, 0.5f, 0f), 10, (z, j) => new Color(0.22f, 0.78f, 0.32f, 0.85f)));
                    GlowPart(wob, "poison.glow", m =>
                    {
                        m.Lathe(SphereProf(0.2f, 5), 8, (z, j) => new Color(0.65f, 1f, 0.45f, 0.75f), 1f, 1f, 0f, null, new Vector3(-0.06f, 0.08f, 0.1f), Quaternion.identity);
                        var pur = new Color(0.72f, 0.35f, 1f, 0.9f);
                        m.Lathe(SphereProf(0.09f, 4), 6, (z, j) => pur, 1f, 1f, 0f, null, new Vector3(0.36f, 0.12f, -0.2f), Quaternion.identity);
                        m.Lathe(SphereProf(0.07f, 4), 6, (z, j) => pur, 1f, 1f, 0f, null, new Vector3(-0.3f, -0.2f, -0.45f), Quaternion.identity);
                        m.Lathe(SphereProf(0.06f, 4), 6, (z, j) => new Color(0.6f, 1f, 0.5f, 0.9f), 1f, 1f, 0f, null, new Vector3(0.05f, 0.34f, -0.6f), Quaternion.identity);
                    });
                    anim.flick = wob; anim.flickAmt = 0.08f; anim.flickRate = 11f;
                    anim.spin = wob; anim.spinAxis = Vector3.forward; anim.spinSpeed = 90f;
                    CoreGlow(root.transform, sc, D * 1.2f, Vector3.zero);
                    break;
                }
                case "smoke":
                {
                    var spin = NewChild(body, "Spin");
                    BlendPart(spin, "smoke.core", m => m.Lathe(SphereProf(0.2f, 5), 8, (z, j) => new Color(0.16f, 0.13f, 0.22f, 0.95f)));
                    var puffs = NewPS(root.transform, "Puffs", BlendMat(TexSoft), 8, false);
                    var pm = puffs.main;
                    pm.startLifetime = 1e5f;
                    pm.startSize = new ParticleSystem.MinMaxCurve(D * 0.6f, D * 0.95f);
                    pm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    pm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.53f, 0.62f, 0.85f), new Color(0.32f, 0.28f, 0.42f, 0.8f));
                    Sphere(puffs, D * 0.28f);
                    Spin(puffs, 1.5f);
                    puffs.Play(); puffs.Emit(7);
                    anim.spin = spin; anim.spinAxis = Vector3.up; anim.spinSpeed = 300f;
                    CoreGlow(root.transform, new Color(0.6f, 0.45f, 0.85f), D * 0.9f, Vector3.zero);
                    break;
                }
                case "slash":
                case "wave":
                {
                    bool wave = id == "wave";
                    var tilt = NewChild(body, "Tilt");
                    tilt.localRotation = Quaternion.Euler(0f, 0f, wave ? 0f : -22f);
                    var blue = wave ? Color.Lerp(sc, new Color(0.35f, 0.6f, 1f), 0.3f) : Color.Lerp(new Color(0.55f, 0.75f, 1f), sc, 0.3f);
                    GlowPart(tilt, id, m =>
                    {
                        Crescent(m, 0.55f, wave ? 170f : 150f, 0.13f, 0.1f, WithA(blue, 0.8f), 0f);
                        Crescent(m, 0.55f, wave ? 160f : 140f, 0.05f, 0.035f, new Color(1f, 1f, 1f, 0.95f), 0.02f);
                        if (wave)
                        {
                            Crescent(m, 0.42f, 150f, 0.08f, 0.06f, WithA(blue, 0.45f), -0.28f);
                            Crescent(m, 0.3f, 130f, 0.06f, 0.045f, WithA(blue, 0.25f), -0.5f);
                        }
                    });
                    anim.flick = tilt; anim.flickAmt = 0.06f; anim.flickRate = 30f;
                    CoreGlow(root.transform, blue, D * 1.0f, new Vector3(0f, 0f, D * 0.2f));
                    break;
                }
                case "thrust":
                {
                    var gold = Color.Lerp(sc, new Color(1f, 0.85f, 0.4f), 0.3f);
                    GlowPart(body, "thrust.glow", m =>
                    {
                        m.Lathe(Prof(-0.4f, 0f, -0.32f, 0.27f, 1.0f, 0f), 4, (z, j) => WithA(gold, 0.5f));
                        m.Annulus(0.26f, 0.42f, 24, WithA(gold, 0.75f), WithA(gold, 0f), new Vector3(0f, 0f, 0.4f), Quaternion.identity);
                        m.Annulus(0.4f, 0.52f, 24, WithA(Color.Lerp(gold, Color.white, 0.4f), 0.4f), WithA(gold, 0f), new Vector3(0f, 0f, 0.05f), Quaternion.identity);
                    });
                    SolidPart(body, "thrust.solid", m =>
                    {
                        m.sub = 0; m.Lathe(Prof(-0.32f, 0f, -0.26f, 0.18f, 0.88f, 0f), 4, null);
                        m.sub = 1; m.Lathe(Prof(-1.6f, 0.04f, -0.3f, 0.04f), 6, null);
                    }, new[] { new Color(0.95f, 0.88f, 0.62f), new Color(0.45f, 0.28f, 0.15f) });
                    anim.flick = body; anim.flickAmt = 0.04f; anim.flickRate = 40f;
                    CoreGlow(root.transform, gold, D * 1.2f, new Vector3(0f, 0f, D * 0.5f));
                    break;
                }
                default:
                {
                    // 其他（含自身/陷阱技能，正常不會飛）：發光球
                    GlowPart(body, "orb", m =>
                    {
                        m.Lathe(SphereProf(0.5f, 7), 10, (z, j) => new Color(1f, 1f, 1f, 0.35f));
                        m.Lathe(SphereProf(0.3f, 6), 8, (z, j) => new Color(1f, 1f, 1f, 0.9f));
                    }, sc);
                    CoreGlow(root.transform, sc, D * 1.4f, Vector3.zero);
                    break;
                }
            }
            if (anim.flick != null) anim.flickBase = anim.flick.localScale;
            if (anim.spin == null && anim.flick == null && anim.jitter == null) anim.enabled = false;
            DecorateProjectile(root, string.IsNullOrEmpty(classId) ? (s != null ? s.cls : "mage") : classId, sc, Mathf.Max(0.03f, radius));
            return root;
        }

        // ---------------------------------------------------------------- 法術造型的零件
        static readonly float[] Jag9 = { 1f, 0.78f, 1.12f, 0.85f, 1.05f, 0.74f, 1.15f, 0.9f, 0.8f };

        static float[] Prof(params float[] zr) => zr;

        static Transform NewChild(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>球的旋轉剖面（z, r 對，由後往前）。</summary>
        static float[] SphereProf(float r, int rings)
        {
            var p = new float[(rings + 1) * 2];
            for (int i = 0; i <= rings; i++)
            {
                float a = Mathf.PI * i / rings;
                p[i * 2] = -Mathf.Cos(a) * r;
                p[i * 2 + 1] = Mathf.Sin(a) * r;
            }
            return p;
        }

        /// <summary>岩石：依方向固定的凹凸（同一位置永遠同樣位移，所以面與面之間不會裂開）。</summary>
        static Vector3 RockWarp(Vector3 p)
        {
            float k = 1f + 0.16f * Mathf.Sin(p.x * 11f + 1f) * Mathf.Cos(p.y * 9f + 2f) * Mathf.Sin(p.z * 8f + 0.5f)
                         + 0.08f * Mathf.Sin(p.x * 23f + p.y * 17f + p.z * 13f);
            return p * k;
        }

        static void ArrowGlow(PMesh m, Color sc, float len, bool snipe)
        {
            var hot = Color.Lerp(sc, Color.white, 0.35f);
            // 箭頭外的光殼
            m.Lathe(Prof(-0.14f, 0f, -0.1f, 0.3f, 0.68f, 0f), 4, (z, j) => WithA(hot, snipe ? 0.65f : 0.5f), 1f, 0.55f);
            // 沿箭桿的淡光
            m.Lathe(Prof(-len, 0.0f, -len + 0.2f, 0.09f, -0.1f, 0.1f, 0f, 0f), 6, (z, j) => WithA(sc, Mathf.Lerp(0.05f, 0.35f, Mathf.InverseLerp(-len, 0f, z))));
            // 箭羽（三片）
            for (int k = 0; k < 3; k++)
            {
                var u = Quaternion.AngleAxis(k * 120f + 90f, Vector3.forward) * Vector3.right;
                float z0 = -len;
                var a = u * 0.04f + Vector3.forward * z0;
                var b = u * 0.27f + Vector3.forward * (z0 - 0.06f);
                var c = u * 0.27f + Vector3.forward * (z0 + 0.42f);
                var d = u * 0.04f + Vector3.forward * (z0 + 0.62f);
                m.Quad(a, b, c, d, WithA(sc, 0.9f));
            }
            if (snipe) m.Annulus(0.18f, 0.3f, 20, WithA(hot, 0.8f), WithA(hot, 0f), new Vector3(0f, 0f, -0.2f), Quaternion.identity);
        }

        static void ArrowSolid(PMesh m, float len)
        {
            m.sub = 0; m.Lathe(Prof(-len, 0.045f, 0f, 0.045f), 6, null);
            m.sub = 1; m.Lathe(Prof(-0.06f, 0f, -0.05f, 0.18f, 0.52f, 0f), 4, null, 1f, 0.4f);
        }

        static void DaggerSolid(PMesh m, bool knife)
        {
            // 刀刃：扁菱形剖面；護手；握柄；柄頭（飛刀是苦無的圓環）
            m.sub = 0; m.Lathe(knife ? Prof(-0.3f, 0.06f, -0.18f, 0.15f, 0.5f, 0f) : Prof(-0.34f, 0.1f, -0.2f, 0.17f, 0.35f, 0.1f, 0.82f, 0f), 4, null, 1f, 0.24f);
            m.sub = 1;
            if (knife) m.Lathe(Prof(-0.36f, 0f, -0.36f, 0.08f, -0.3f, 0.08f, -0.3f, 0f), 6, null);
            else m.Lathe(Prof(-0.43f, 0f, -0.43f, 0.32f, -0.35f, 0.32f, -0.35f, 0f), 4, null, 1f, 0.28f, 45f);
            m.sub = 2; m.Lathe(knife ? Prof(-0.72f, 0.035f, -0.34f, 0.035f) : Prof(-0.86f, 0.055f, -0.43f, 0.055f), 6, null);
            m.sub = 3;
            if (knife) m.Lathe(Prof(-0.95f, 0f, -0.9f, 0.1f, -0.78f, 0.1f, -0.72f, 0f), 8, null, 1f, 0.3f);
            else m.Lathe(SphereProf(0.08f, 4), 6, null, 1f, 1f, 0f, null, new Vector3(0f, 0f, -0.92f), Quaternion.identity);
        }

        static void DaggerGlow(PMesh m, Color edge, bool knife)
        {
            var p = knife ? Prof(-0.36f, 0f, -0.2f, 0.22f, 0.62f, 0f) : Prof(-0.38f, 0f, -0.22f, 0.25f, 0.35f, 0.15f, 0.95f, 0f);
            m.Lathe(p, 4, (z, j) => WithA(edge, Mathf.Lerp(0.25f, 0.7f, Mathf.InverseLerp(-0.4f, 0.9f, z))), 1f, 0.32f);
        }

        /// <summary>雷擊的鋸齒閃電：兩條互相垂直的帶子，沿 -Z 往後左右折返。</summary>
        static void BoltZigzag(PMesh m, Color c)
        {
            const int N = 9;
            var pts = new Vector3[N];
            var cols = new Color[N];
            float[] off = { 0f, 0.22f, -0.16f, 0.26f, -0.2f, 0.18f, -0.24f, 0.12f, 0f };
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < N; i++)
                {
                    float z = Mathf.Lerp(0.6f, -2.2f, i / (N - 1f));
                    float o = off[i] * (pass == 0 ? 1f : -0.8f);
                    pts[i] = pass == 0 ? new Vector3(o, 0f, z) : new Vector3(0f, o, z);
                    cols[i] = WithA(c, Mathf.Lerp(0.95f, 0.15f, i / (N - 1f)));
                }
                m.Ribbon(pts, pass == 0 ? Vector3.up * 0.06f : Vector3.right * 0.06f, cols);
                m.Ribbon(pts, pass == 0 ? Vector3.right * 0.05f : Vector3.up * 0.05f, cols);
            }
        }

        /// <summary>迎著飛行方向的新月刃：弧在 XZ 平面往前凸（兩端往後、微微下垂），兩條交叉的帶子讓正面、側面都看得到。</summary>
        static void Crescent(PMesh m, float R, float spanDeg, float h, float w, Color c, float zOff)
        {
            const int N = 17;
            var pts = new Vector3[N];
            var hv = new Vector3[N];
            var wv = new Vector3[N];
            var cols = new Color[N];
            for (int i = 0; i < N; i++)
            {
                float t = i / (N - 1f);
                float a = (t - 0.5f) * spanDeg * Mathf.Deg2Rad;
                float taper = Mathf.Sin(t * Mathf.PI);
                pts[i] = new Vector3(Mathf.Sin(a) * R, -0.3f * R * (1f - Mathf.Cos(a)), Mathf.Cos(a) * R - R * 0.6f + zOff);
                hv[i] = Vector3.up * (h * taper);
                wv[i] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (w * taper);
                cols[i] = WithA(c, c.a * Mathf.Lerp(0.3f, 1f, taper));
            }
            m.Ribbon(pts, hv, cols);
            m.Ribbon(pts, wv, cols);
        }

        /// <summary>風刃的旋渦：XY 平面（正對飛行方向）上的彎刀形弧，中間粗兩端尖，整體繞 Z 旋轉。</summary>
        static void Whirl(PMesh m, float R, float a0Deg, float spanDeg, float w, Color c, float z)
        {
            const int N = 13;
            var pts = new Vector3[N];
            var wv = new Vector3[N];
            var cols = new Color[N];
            for (int i = 0; i < N; i++)
            {
                float t = i / (N - 1f);
                float a = (a0Deg + t * spanDeg) * Mathf.Deg2Rad;
                float r = R * (0.55f + 0.45f * t);   // 往外捲的螺旋
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                pts[i] = dir * r + Vector3.forward * (z - 0.15f * t);
                wv[i] = dir * (w * Mathf.Sin(Mathf.Pow(t, 0.7f) * Mathf.PI));
                cols[i] = WithA(c, c.a * Mathf.Lerp(0.2f, 1f, t));
            }
            m.Ribbon(pts, wv, cols);
        }

        // ---------------------------------------------------------------- 零件快取與材質
        static readonly Dictionary<string, Mesh> projMeshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<int, Material> solidMats = new Dictionary<int, Material>();
        static Material meshAddMat, meshBlendMat;
        static Shader solidShader;

        static Mesh CachedMesh(string key, System.Action<PMesh> build, int subs, bool normals)
        {
            if (projMeshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var pm = new PMesh(subs);
            build(pm);
            mesh = pm.Build(key, normals);
            projMeshes[key] = mesh;
            return mesh;
        }

        static MeshRenderer MeshChild(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return r;
        }

        /// <summary>發光（疊加）零件：顏色全在頂點色裡。tint 不是白色時整體再乘上 tint（用於共用網格、不同顏色的情況，會另外快取一份）。</summary>
        static void GlowPart(Transform parent, string key, System.Action<PMesh> build, Color? tint = null)
        {
            if (tint.HasValue)
            {
                var t = tint.Value;
                key += "#" + ColorKey(t);
                var inner = build;
                build = m => { inner(m); m.Tint(t); };
            }
            MeshChild(parent, "Glow", CachedMesh(key, build, 1, false)).sharedMaterial = MeshMat(true);
        }

        /// <summary>半透明（一般混色）零件：用於暗色物體（疊加無法變暗）。只適合單一顏色的凸形，因為不寫深度。</summary>
        static void BlendPart(Transform parent, string key, System.Action<PMesh> build)
        {
            MeshChild(parent, "Blend", CachedMesh(key, build, 1, false)).sharedMaterial = MeshMat(false);
        }

        /// <summary>實體零件（Legacy Diffuse，吃場景主光，有深度）：每個 submesh 一個顏色。</summary>
        static void SolidPart(Transform parent, string key, System.Action<PMesh> build, Color[] colors)
        {
            var mesh = CachedMesh(key, build, colors.Length, true);
            var r = MeshChild(parent, "Solid", mesh);
            var mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) mats[i] = SolidMat(colors[i]);
            r.sharedMaterials = mats;
        }

        static int ColorKey(Color c)
        {
            Color32 k = c;
            return (k.r << 24) | (k.g << 16) | (k.b << 8) | k.a;
        }

        static Material MeshMat(bool additive)
        {
            if (additive && meshAddMat != null) return meshAddMat;
            if (!additive && meshBlendMat != null) return meshBlendMat;
            var proto = additive ? AddMat(TexSoft) : BlendMat(TexSoft);   // 共用同一個（含備援）著色器
            var m = new Material(proto.shader) { name = additive ? "SpellFxMeshAdd" : "SpellFxMeshBlend", mainTexture = Texture2D.whiteTexture };
            if (additive) meshAddMat = m; else meshBlendMat = m;
            return m;
        }

        static Material SolidMat(Color c)
        {
            int key = ColorKey(c);
            if (solidMats.TryGetValue(key, out var m) && m != null) return m;
            if (solidShader == null)
            {
                solidShader = Shader.Find("Legacy Shaders/Diffuse");
                if (solidShader == null) solidShader = Shader.Find("Mobile/Diffuse");
                if (solidShader == null) solidShader = Shader.Find("Sprites/Default");
            }
            m = new Material(solidShader) { name = "SpellFxSolid", color = c };
            solidMats[key] = m;
            return m;
        }

        /// <summary>柔光核心：一顆一直存在的本地粒子（永遠面向鏡頭的光暈 billboard）。</summary>
        static void CoreGlow(Transform parent, Color c, float size, Vector3 localPos)
        {
            var ps = NewPS(parent, "CoreGlow", AddMat(TexSoft), 1, false);
            ps.transform.localPosition = localPos;
            var m = ps.main;
            m.startLifetime = 1e5f;
            m.startSize = size;
            m.startColor = WithA(c, 0.8f);
            ps.Play();
            ps.Emit(1);
        }

        // ================================================================ 共用資源（貼圖、材質、曲線，全部快取）
        internal const int TexSoft = 0, TexStar = 1, TexLeaf = 2, TexLine = 3;
        static readonly Texture2D[] texs = new Texture2D[4];
        static readonly Material[] addMats = new Material[4], blendMats = new Material[4];

        static Texture2D Tex(int kind)
        {
            if (texs[kind] != null) return texs[kind];
            int w = 64, h = kind == TexLine ? 32 : 64;
            if (kind == TexLine) w = 4;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "SpellFxTex" + kind };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                    float a;
                    switch (kind)
                    {
                        case TexStar:
                        {
                            // 四道光芒＋柔和核心（劍芒閃光）
                            float d = Mathf.Sqrt(u * u + v * v);
                            float core = Mathf.Clamp01(1f - d / 0.35f); core *= core;
                            float rayX = Mathf.Clamp01(1f - Mathf.Abs(u)) * Mathf.Exp(-Mathf.Abs(v) * 28f);
                            float rayY = Mathf.Clamp01(1f - Mathf.Abs(v)) * Mathf.Exp(-Mathf.Abs(u) * 28f);
                            float glow = Mathf.Clamp01(1f - d) * 0.25f;
                            a = Mathf.Clamp01(core + rayX + rayY + glow * glow);
                            break;
                        }
                        case TexLeaf:
                        {
                            // 兩端尖的葉片（羽毛）形狀＋中間的葉脈
                            float half = 0.38f * (1f - v * v);
                            float edge = Mathf.Clamp01((half - Mathf.Abs(u)) / 0.08f);
                            float vein = 1f - 0.35f * Mathf.Exp(-u * u * 900f);
                            a = edge * vein * Mathf.Clamp01((1f - Mathf.Abs(v)) * 4f);
                            break;
                        }
                        case TexLine:
                        {
                            // 線條橫截面：中央亮芯＋柔邊
                            float d = Mathf.Abs(v);
                            float soft = Mathf.Clamp01(1f - d); soft *= soft;
                            a = Mathf.Clamp01(soft * 0.75f + Mathf.Exp(-d * d * 30f) * 0.6f);
                            break;
                        }
                        default:
                        {
                            float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                            float f = 1f - d;
                            a = f * f * (3f - 2f * f);
                            a *= a;
                            break;
                        }
                    }
                    byte b = (byte)Mathf.RoundToInt(a * 255f);
                    px[y * w + x] = new Color32(255, 255, 255, b);
                }
            t.SetPixels32(px);
            t.Apply(true, false);
            texs[kind] = t;
            return t;
        }

        static Shader addShader, blendShader;

        /// <summary>疊加（發光）材質：Legacy Particles/Additive → Mobile → Sprites/Default。</summary>
        internal static Material AddMat(int kind)
        {
            if (addMats[kind] != null) return addMats[kind];
            if (addShader == null)
            {
                addShader = Shader.Find("Legacy Shaders/Particles/Additive");
                if (addShader == null) addShader = Shader.Find("Mobile/Particles/Additive");
                if (addShader == null) addShader = Shader.Find("Sprites/Default");
            }
            var m = new Material(addShader) { name = "SpellFxAdd" + kind, mainTexture = Tex(kind) };
            addMats[kind] = m;
            return m;
        }

        /// <summary>一般半透明材質（煙霧等「暗色」效果；疊加無法變暗）。</summary>
        internal static Material BlendMat(int kind)
        {
            if (blendMats[kind] != null) return blendMats[kind];
            if (blendShader == null)
            {
                blendShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                if (blendShader == null) blendShader = Shader.Find("Mobile/Particles/Alpha Blended");
                if (blendShader == null) blendShader = Shader.Find("Sprites/Default");
            }
            var m = new Material(blendShader) { name = "SpellFxBlend" + kind, mainTexture = Tex(kind) };
            blendMats[kind] = m;
            return m;
        }

        static Gradient fadeOutG, fadeInOutG;
        static Gradient FadeOutGrad => fadeOutG ?? (fadeOutG = AlphaGrad(0.05f, 0.55f));
        static Gradient FadeInOutGrad => fadeInOutG ?? (fadeInOutG = AlphaGrad(0.3f, 0.65f));

        static Gradient AlphaGrad(float inT, float holdT)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inT), new GradientAlphaKey(1f, holdT), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        internal static readonly AnimationCurve Shrink = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        internal static readonly AnimationCurve Grow = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.4f));
        internal static readonly AnimationCurve PopCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
        internal static readonly AnimationCurve Flicker = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(0.25f, 0.2f),
            new Keyframe(0.4f, 1f), new Keyframe(0.55f, 0.15f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f));
        internal static readonly AnimationCurve Taper = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        internal static readonly AnimationCurve ArcTaper = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
        internal static readonly AnimationCurve HeadTaper = new AnimationCurve(new Keyframe(0f, 0.05f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.2f));

        internal static Color WithA(Color c, float a) { c.a = a; return c; }

        // ================================================================ 粒子系統小工具
        /// <summary>建立一個空白、已停止的粒子系統；設定好之後呼叫 Play()。</summary>
        internal static ParticleSystem NewPS(Transform parent, string name, Material mat, int maxParticles, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = true;
            m.duration = 1f;
            m.maxParticles = maxParticles;
            m.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            m.scalingMode = ParticleSystemScalingMode.Local;
            m.startSpeed = 0f;
            m.startLifetime = 0.5f;
            m.gravityModifier = 0f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.rateOverDistance = 0f;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.001f;
            sh.radiusThickness = 1f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 0.25f;   // 粒子貼近鏡頭時最多佔畫面 1/4，避免擋住視線
            return ps;
        }

        internal static void Sphere(ParticleSystem ps, float radius, float thickness = 1f)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = Mathf.Max(0.0005f, radius);
            sh.radiusThickness = thickness;
        }

        internal static void Cone(ParticleSystem ps, float angle, float radius)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;   // 沿本地 +Z 噴出
            sh.angle = angle;
            sh.radius = Mathf.Max(0.0005f, radius);
            sh.radiusThickness = 1f;
        }

        /// <summary>圓盤（本地 XY 平面）上隨機位置。</summary>
        internal static void Disc(ParticleSystem ps, float radius, float thickness = 1f)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = Mathf.Max(0.0005f, radius);
            sh.radiusThickness = thickness;
            sh.arc = 360f;
        }

        internal static void FadeOut(ParticleSystem ps)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            c.color = new ParticleSystem.MinMaxGradient(FadeOutGrad);
        }

        internal static void FadeInOut(ParticleSystem ps)
        {
            var c = ps.colorOverLifetime;
            c.enabled = true;
            c.color = new ParticleSystem.MinMaxGradient(FadeInOutGrad);
        }

        internal static void SizeCurve(ParticleSystem ps, AnimationCurve curve)
        {
            var so = ps.sizeOverLifetime;
            so.enabled = true;
            so.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        internal static void Rate(ParticleSystem ps, float perSecond)
        {
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = perSecond;
        }

        static void RateDist(ParticleSystem ps, float perMeter)
        {
            var em = ps.emission;
            em.enabled = true;
            em.rateOverDistance = perMeter;
        }

        internal static void Stretch(ParticleSystem ps, float velocityScale, float lengthScale)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = velocityScale;
            r.lengthScale = lengthScale;
        }

        internal static void Spin(ParticleSystem ps, float radPerSec)
        {
            var ro = ps.rotationOverLifetime;
            ro.enabled = true;
            ro.z = new ParticleSystem.MinMaxCurve(-radPerSec, radPerSec);
        }

        /// <summary>世界座標往上飄（不受 Local 模擬空間的旋轉影響）。</summary>
        internal static void Rise(ParticleSystem ps, float speed)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.World;
            v.x = 0f; v.y = speed; v.z = 0f;
        }

        /// <summary>繞本地 Z 軸旋轉＋往中心收束（風的漩渦）。</summary>
        internal static void Swirl(ParticleSystem ps, float orbital, float radial)
        {
            var v = ps.velocityOverLifetime;
            v.enabled = true;
            v.space = ParticleSystemSimulationSpace.Local;
            v.x = 0f; v.y = 0f; v.z = 0f;
            v.orbitalX = 0f; v.orbitalY = 0f; v.orbitalZ = orbital;
            v.radial = radial;
        }

        internal static void Drag(ParticleSystem ps, float dampen)
        {
            var l = ps.limitVelocityOverLifetime;
            l.enabled = true;
            l.limit = 100f;
            l.dampen = 0f;
            l.drag = dampen;   // 空氣阻力（1/秒）
            l.multiplyDragByParticleSize = false;
            l.multiplyDragByParticleVelocity = false;
        }

        internal static void Noise(ParticleSystem ps, float strength, float freq)
        {
            var n = ps.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = freq;
            n.scrollSpeed = 0.5f;
            n.quality = ParticleSystemNoiseQuality.Low;
        }

        static void Flash(ParticleSystem ps, Color c, float size, float life)
        {
            var m = ps.main;
            m.startLifetime = life;
            m.startSize = size;
            m.startColor = WithA(c, 0.9f);
            FadeOut(ps);
            SizeCurve(ps, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.8f)));
            ps.Play();
        }

        static void Sparks(Transform root, Color c, float s, int count, float vMin, float vMax)
        {
            var sp = NewPS(root, "Sparks", AddMat(TexSoft), count + 4, true);
            var m = sp.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(vMin * s, vMax * s);
            m.startSize = 0.016f * s;
            m.startColor = new ParticleSystem.MinMaxGradient(c, Color.white);
            Sphere(sp, 0.03f * s);
            FadeOut(sp);
            Stretch(sp, 0.04f, 1.5f);
            Drag(sp, 2f);
            sp.Play(); sp.Emit(count);
        }

        static void Leaves(Transform root, Color c, float s, int count, float speed, bool forward)
        {
            var lv = NewPS(root, "Leaves", AddMat(TexLeaf), count + 2, true);
            var m = lv.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f * s, speed * s);
            m.startSize = new ParticleSystem.MinMaxCurve(0.04f * s, 0.07f * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(c, new Color(0.45f, 0.9f, 0.3f), 0.5f), Color.Lerp(c, Color.white, 0.4f));
            if (forward) Cone(lv, 40f, 0.05f * s); else Sphere(lv, 0.05f * s);
            FadeOut(lv);
            Spin(lv, 8f);
            Drag(lv, 3f);
            Noise(lv, 0.4f * s, 2f);
            lv.Play(); lv.Emit(count);
        }

        static void Stars(Transform root, Color c, float s, int count, float life, float sMin, float sMax)
        {
            var st = NewPS(root, "Glints", AddMat(TexStar), count + 2, true);
            var m = st.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
            m.startSize = new ParticleSystem.MinMaxCurve(sMin * s, sMax * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
            m.startColor = new ParticleSystem.MinMaxGradient(c, Color.white);
            Sphere(st, 0.25f * s);
            SizeCurve(st, PopCurve);
            st.Play(); st.Emit(count);
        }

        // ================================================================ 線條小工具（本地 XY 平面）
        internal static Vector3[] Circle(int n, float r, float phaseDeg = 0f)
        {
            var p = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (phaseDeg + 360f * i / n) * Mathf.Deg2Rad;
                p[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            return p;
        }

        internal static Vector3[] Polygon(int n, float r, float phaseDeg) => Circle(n, r, phaseDeg);

        /// <summary>符文鋸齒帶：在兩個半徑之間來回的封閉折線。</summary>
        internal static Vector3[] Zigzag(int n, float r0, float r1)
        {
            var p = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n, r = (i & 1) == 0 ? r0 : r1;
                p[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            return p;
        }

        /// <summary>弧線：以本地 +Z 為中心、在 XZ 平面（水平）左右各展開 spanDeg/2。</summary>
        internal static Vector3[] Arc(int n, float r, float spanDeg)
        {
            var p = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = (-spanDeg * 0.5f + spanDeg * i / (n - 1)) * Mathf.Deg2Rad;
                p[i] = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
            }
            return p;
        }

        /// <summary>部分圓弧（本地 XY 平面），從 0 度到 spanDeg，用來做旋轉的風痕。</summary>
        internal static Vector3[] PartialCircle(int n, float r, float spanDeg)
        {
            var p = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = spanDeg * i / (n - 1) * Mathf.Deg2Rad;
                p[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            return p;
        }

        internal static LineRenderer Line(Transform parent, string name, Vector3[] pts, bool loop, float width, Color c, bool additive, bool flat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
            lr.widthMultiplier = width;
            lr.alignment = flat ? LineAlignment.TransformZ : LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCornerVertices = 1;
            lr.sharedMaterial = additive ? AddMat(TexLine) : BlendMat(TexLine);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = c; lr.endColor = c;
            return lr;
        }

        static FxAnim NewAnim(string name, Vector3 pos, Quaternion rot, float life)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            var a = go.AddComponent<FxAnim>();
            a.killAt = life;
            return a;
        }
    }

    /// <summary>一次性光效：擴散的光環／斬擊弧（放大＋淡出），時間到自己 Destroy。</summary>
    internal class FxAnim : MonoBehaviour
    {
        struct Item
        {
            public LineRenderer lr; public Transform t; public Quaternion baseRot;
            public Color c; public float w, delay, life, s0, s1, spin;
        }
        readonly List<Item> items = new List<Item>();
        public float killAt = 1f;
        float time;

        /// <summary>擴散光環（點用單位半徑，靠縮放 s0 → s1 長大）。</summary>
        public void AddRing(Transform parent, Vector3[] unitPts, bool loop, float width, Color c, float delay, float life, float s0, float s1, bool additive = true, float spinDeg = 0f)
        {
            var holder = new GameObject("Ring").transform;
            holder.SetParent(parent, false);
            var lr = SpellFx.Line(holder, "Line", unitPts, loop, width, c, additive, true);
            lr.enabled = false;
            items.Add(new Item { lr = lr, t = holder, baseRot = holder.localRotation, c = c, w = width, delay = delay, life = life, s0 = s0, s1 = s1, spin = spinDeg });
        }

        /// <summary>斬擊弧（面向鏡頭的帶狀線，兩端尖），一邊放大一邊繞本地 Y 掃過 sweepDeg。</summary>
        public void AddArc(Transform parent, Vector3[] unitPts, float width, Color c, float delay, float life, float s0, float s1, float sweepDeg)
        {
            var holder = new GameObject("Arc").transform;
            holder.SetParent(parent, false);
            var lr = SpellFx.Line(holder, "Line", unitPts, false, width, c, true, false);
            lr.widthCurve = SpellFx.ArcTaper;
            lr.numCapVertices = 0;
            lr.enabled = false;
            items.Add(new Item { lr = lr, t = holder, baseRot = Quaternion.Euler(0f, sweepDeg * 0.5f, 0f), c = c, w = width, delay = delay, life = life, s0 = s0, s1 = s1, spin = -sweepDeg, });
        }

        void Update()
        {
            time += Time.deltaTime;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.lr == null) continue;
                float k = (time - it.delay) / it.life;
                if (k < 0f || k >= 1f) { if (it.lr.enabled) it.lr.enabled = false; continue; }
                if (!it.lr.enabled) it.lr.enabled = true;
                float e = 1f - (1f - k) * (1f - k) * (1f - k);
                it.t.localScale = Vector3.one * Mathf.Lerp(it.s0, it.s1, e);
                if (it.spin != 0f) it.t.localRotation = it.baseRot * Quaternion.AngleAxis(it.spin * e, it.lr.alignment == LineAlignment.View ? Vector3.up : Vector3.forward);
                float fade = (1f - k); fade *= fade;
                var c = it.c; c.a *= fade;
                it.lr.startColor = c; it.lr.endColor = c;
                it.lr.widthMultiplier = it.w * (1f - 0.5f * k);
            }
            if (time >= killAt) Destroy(gameObject);
        }
    }

    /// <summary>飛行中法術的小動畫：零件自轉、閃爍縮放、雷電的隨機折角（每幀不配置記憶體）。</summary>
    internal class ProjectileAnim : MonoBehaviour
    {
        public Transform spin; public Vector3 spinAxis = Vector3.forward; public float spinSpeed;
        public Transform flick; public Vector3 flickBase = Vector3.one; public float flickAmt, flickRate = 20f;
        public Transform jitter; public float jitterEvery = 0.05f;
        float clock, jt;

        void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;
            if (clock > 1000f) clock -= 1000f;
            if (spin != null && spinSpeed != 0f) spin.Rotate(spinAxis, spinSpeed * dt, Space.Self);
            if (flick != null && flickAmt > 0f)
            {
                float k = 1f + flickAmt * (0.6f * Mathf.Sin(clock * flickRate) + 0.4f * Mathf.Sin(clock * flickRate * 2.37f + 1.7f));
                flick.localScale = flickBase * k;
            }
            if (jitter != null)
            {
                jt -= dt;
                if (jt <= 0f)
                {
                    jt = jitterEvery;
                    jitter.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                    jitter.localScale = new Vector3(Random.Range(0.7f, 1.35f), Random.Range(0.7f, 1.35f), Random.Range(0.85f, 1.15f));
                }
            }
        }
    }

    /// <summary>程式網格小工具：旋轉體（Lathe）、帶子、圓環、四邊形。每個面都有自己的頂點（平面著色、面的顏色各自獨立）。</summary>
    internal sealed class PMesh
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<int>[] t;
        public int sub;

        public PMesh(int subs) { t = new List<int>[Mathf.Max(1, subs)]; for (int i = 0; i < t.Length; i++) t[i] = new List<int>(); }

        public void Tri(Vector3 a, Vector3 b, Vector3 d, Color col)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(d);
            c.Add(col); c.Add(col); c.Add(col);
            var list = t[Mathf.Clamp(sub, 0, t.Length - 1)];
            list.Add(i); list.Add(i + 1); list.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col) { Tri(a, b, d, col); Tri(a, d, e, col); }

        /// <summary>
        /// 旋轉體：prof = (z, r) 對，由後（-Z）往前（+Z）；sides 邊數；col(z, 第幾邊) 給每個面的顏色（null＝白）。
        /// sx/sy 壓扁剖面、phaseDeg 旋轉剖面、jag 每邊半徑倍率（火焰的參差）、off/rot 擺放、warp 頂點變形。
        /// 面朝外（順時針），可用於有背面剔除的實體材質。
        /// </summary>
        public void Lathe(float[] prof, int sides, System.Func<float, int, Color> col, float sx = 1f, float sy = 1f, float phaseDeg = 0f,
                          float[] jag = null, Vector3 off = default, Quaternion rot = default, System.Func<Vector3, Vector3> warp = null)
        {
            if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f) rot = Quaternion.identity;
            int rings = prof.Length / 2;
            for (int i = 0; i < rings - 1; i++)
            {
                float z0 = prof[i * 2], r0 = prof[i * 2 + 1], z1 = prof[i * 2 + 2], r1 = prof[i * 2 + 3];
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    var p00 = P(z0, r0, j, sides, sx, sy, phaseDeg, jag, off, rot, warp);
                    var p01 = P(z0, r0, j1, sides, sx, sy, phaseDeg, jag, off, rot, warp);
                    var p10 = P(z1, r1, j, sides, sx, sy, phaseDeg, jag, off, rot, warp);
                    var p11 = P(z1, r1, j1, sides, sx, sy, phaseDeg, jag, off, rot, warp);
                    var col0 = col != null ? col((z0 + z1) * 0.5f, j) : Color.white;
                    // 半徑為 0 的那一圈縮成一點，對應的三角形會退化，跳過
                    if (r1 > 0f) Tri(p00, p11, p10, col0);
                    if (r0 > 0f) Tri(p00, p01, p11, col0);
                }
            }
        }

        static Vector3 P(float z, float r, int j, int sides, float sx, float sy, float phaseDeg, float[] jag, Vector3 off, Quaternion rot, System.Func<Vector3, Vector3> warp)
        {
            float a = (phaseDeg + 360f * j / sides) * Mathf.Deg2Rad;
            float k = jag != null ? jag[j % jag.Length] : 1f;
            var p = new Vector3(Mathf.Cos(a) * r * sx * k, Mathf.Sin(a) * r * sy * k, z);
            if (warp != null) p = warp(p);
            return off + rot * p;
        }

        /// <summary>帶子：沿 pts，每點往 ±half 展開（無背面剔除的材質用）。</summary>
        public void Ribbon(Vector3[] pts, Vector3 half, Color[] cols)
        {
            for (int i = 0; i < pts.Length - 1; i++) RibbonSeg(pts[i], pts[i + 1], half, half, cols[i], cols[i + 1]);
        }

        public void Ribbon(Vector3[] pts, Vector3[] half, Color[] cols)
        {
            for (int i = 0; i < pts.Length - 1; i++) RibbonSeg(pts[i], pts[i + 1], half[i], half[i + 1], cols[i], cols[i + 1]);
        }

        void RibbonSeg(Vector3 a, Vector3 b, Vector3 ha, Vector3 hb, Color ca, Color cb)
        {
            int i = v.Count;
            v.Add(a - ha); v.Add(a + ha); v.Add(b + hb); v.Add(b - hb);
            c.Add(ca); c.Add(ca); c.Add(cb); c.Add(cb);
            var list = t[Mathf.Clamp(sub, 0, t.Length - 1)];
            list.Add(i); list.Add(i + 1); list.Add(i + 2);
            list.Add(i); list.Add(i + 2); list.Add(i + 3);
        }

        /// <summary>平面圓環（本地 XY 平面），內圈 cIn 到外圈 cOut 的漸層。</summary>
        public void Annulus(float r0, float r1, int n, Color cIn, Color cOut, Vector3 off, Quaternion rot)
        {
            var list = t[Mathf.Clamp(sub, 0, t.Length - 1)];
            int start = v.Count;
            for (int i = 0; i < n; i++)
            {
                float a = 2f * Mathf.PI * i / n;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                v.Add(off + rot * (d * r0)); c.Add(cIn);
                v.Add(off + rot * (d * r1)); c.Add(cOut);
            }
            for (int i = 0; i < n; i++)
            {
                int a0 = start + i * 2, a1 = start + ((i + 1) % n) * 2;
                list.Add(a0); list.Add(a0 + 1); list.Add(a1 + 1);
                list.Add(a0); list.Add(a1 + 1); list.Add(a1);
            }
        }

        public void Tint(Color k)
        {
            for (int i = 0; i < c.Count; i++) c[i] = c[i] * k;
        }

        public Mesh Build(string name, bool normals)
        {
            var m = new Mesh { name = "SpellFx " + name };
            m.SetVertices(v);
            m.SetColors(c);
            m.subMeshCount = t.Length;
            for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i, true);
            if (normals) m.RecalculateNormals();
            m.RecalculateBounds();
            m.UploadMeshData(true);
            return m;
        }
    }

    /// <summary>
    /// 詠唱中的光效：每幀跟著 anchor（位置＋旋轉）走。
    ///   「地面模式」：anchor 不是鏡頭、且 localOffset 很低（腳底）→ 法陣平躺在地上、只跟著水平朝向轉。
    ///   「手前模式」：其他情況（手的位置、或玩家的 AR 鏡頭）→ 法陣立在 anchor 前方、面向 anchor 的前方。
    ///   Progress 0..1 控制大小、亮度與粒子量；≥ 1 時加上脈動與一圈圈擴散的「蓄力完成」光環。
    /// </summary>
    public class ChargeFx : MonoBehaviour
    {
        public float Progress { get; set; }        // 0..1

        Transform anchor;
        Vector3 offset;
        bool ground, firstPerson, stopping;
        float clock, stopT, shownP, readyAmt, alphaMul = 1f;

        // 每條線的基本顏色與寬度（每幀依亮度調整，不配置記憶體）
        readonly List<LineRenderer> lines = new List<LineRenderer>();
        readonly List<Color> lineCol = new List<Color>();
        readonly List<float> lineW = new List<float>();
        // 旋轉的物件
        readonly List<Transform> spinT = new List<Transform>();
        readonly List<Vector3> spinAxis = new List<Vector3>();
        readonly List<float> spinSpeed = new List<float>();
        readonly List<float> spinAngle = new List<float>();
        readonly List<Quaternion> spinBase = new List<Quaternion>();
        // 隨蓄力長大的物件
        readonly List<Transform> growT = new List<Transform>();
        readonly List<Vector3> growMin = new List<Vector3>();
        readonly List<Vector3> growMax = new List<Vector3>();
        // 粒子系統（基本發射率）
        readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        readonly List<float> baseRate = new List<float>();
        // 蓄力完成時擴散的光環
        Transform readyRing; LineRenderer readyLine; Color readyCol; float readyW, readyR;

        const float StopFade = 0.3f, StopKill = 0.5f;

        /// <summary>停止：粒子停止發射，線條約 0.3 秒淡出，之後自己 Destroy。</summary>
        public void Stop()
        {
            if (this == null || stopping) return;
            stopping = true;
            stopT = 0f;
            for (int i = 0; i < systems.Count; i++)
                if (systems[i] != null) systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ================================================================ 建立
        internal void Init(string cls, Color col, Transform anchorT, Vector3 localOffset, float scale)
        {
            anchor = anchorT;
            offset = localOffset;
            col.a = 1f;
            float s = Mathf.Max(0.05f, scale);
            firstPerson = anchorT != null && anchorT.GetComponent<Camera>() != null;
            ground = !firstPerson && localOffset.y < 0.3f;
            if (firstPerson) alphaMul = 0.85f;
            Follow();

            // 法陣平面：地面模式平躺（本地 XY → 水平），手前模式立著面向前方
            var disc = new GameObject("Disc").transform;
            disc.SetParent(transform, false);
            if (ground) { disc.localPosition = new Vector3(0f, 0.015f, 0f); disc.localRotation = Quaternion.Euler(90f, 0f, 0f); }

            switch (cls)
            {
                case "archer": BuildArcher(disc, col, s); break;
                case "assassin": BuildAssassin(disc, col, s); break;
                case "swordsman": BuildSwordsman(disc, col, s); break;
                default: BuildMage(disc, col, s); break;
            }

            for (int i = 0; i < systems.Count; i++) systems[i].Play();
            LateUpdate();
        }

        void BuildMage(Transform disc, Color col, float s)
        {
            float R = (ground ? 0.55f : 0.26f) * s;
            var light = Color.Lerp(col, Color.white, 0.35f);
            var grow = Child(disc, "Grow");
            AddGrow(grow, Vector3.one * 0.35f, Vector3.one);

            var outer = Child(grow, "SpinOuter");
            AddSpin(outer, Vector3.forward, 35f);
            AddLine(SpellFx.Line(outer, "Outer", SpellFx.Circle(56, R), true, 0.014f * s, light, true, true), SpellFx.WithA(light, 0.95f));
            AddLine(SpellFx.Line(outer, "Runes", SpellFx.Zigzag(48, R * 0.84f, R * 0.95f), true, 0.007f * s, col, true, true), SpellFx.WithA(col, 0.8f));
            AddLine(SpellFx.Line(outer, "Center", SpellFx.Circle(24, R * 0.18f), true, 0.008f * s, light, true, true), SpellFx.WithA(light, 0.8f));

            var inner = Child(grow, "SpinInner");
            AddSpin(inner, Vector3.forward, -55f);
            AddLine(SpellFx.Line(inner, "Inner", SpellFx.Circle(44, R * 0.66f), true, 0.009f * s, col, true, true), SpellFx.WithA(col, 0.9f));
            AddLine(SpellFx.Line(inner, "TriA", SpellFx.Polygon(3, R * 0.66f, 90f), true, 0.008f * s, light, true, true), SpellFx.WithA(light, 0.85f));
            AddLine(SpellFx.Line(inner, "TriB", SpellFx.Polygon(3, R * 0.66f, 270f), true, 0.008f * s, light, true, true), SpellFx.WithA(light, 0.85f));

            // 柔光（法陣中心）
            var glow = SpellFx.NewPS(disc, "Glow", SpellFx.AddMat(SpellFx.TexSoft), 6, false);
            var gm = glow.main;
            gm.startLifetime = 0.5f;
            gm.startSize = R * (ground ? 1.1f : 1.4f);
            gm.startColor = SpellFx.WithA(col, 0.3f * alphaMul);
            SpellFx.FadeInOut(glow);
            AddPS(glow, 8f);

            // 往上飄的星光
            var sp = SpellFx.NewPS(disc, "Sparkles", SpellFx.AddMat(SpellFx.TexSoft), 70, false);
            var m = sp.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.025f * s, 0.055f * s);
            m.startColor = new ParticleSystem.MinMaxGradient(SpellFx.WithA(col, alphaMul), SpellFx.WithA(light, alphaMul));
            SpellFx.Disc(sp, R);
            SpellFx.FadeInOut(sp);
            SpellFx.SizeCurve(sp, SpellFx.Shrink);
            SpellFx.Rise(sp, (ground ? 0.6f : 0.35f) * s);
            AddPS(sp, 50f);

            SetReady(disc, R * 1.05f, 0.012f * s, light);
        }

        void BuildArcher(Transform disc, Color col, float s)
        {
            var wind = Color.Lerp(col, new Color(0.7f, 1f, 0.55f), 0.35f);
            var light = Color.Lerp(wind, Color.white, 0.45f);
            float R;
            Transform center;
            if (ground)
            {
                // 腳下：繞著身體旋轉、往上捲的風
                R = 0.45f * s;
                center = Child(transform, "Center");
                center.localPosition = new Vector3(0f, 0.9f * s, 0f);
                center.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 本地 Z 朝下 → 粒子繞垂直軸旋轉
                float[] h = { 0.2f, 0.7f, 1.2f }, rr = { 0.5f, 0.42f, 0.34f }, sp = { 260f, -320f, 380f };
                for (int i = 0; i < 3; i++)
                {
                    var lvl = Child(transform, "Gust" + i);
                    lvl.localPosition = new Vector3(0f, h[i] * s, 0f);
                    lvl.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    AddSpin(lvl, Vector3.forward, sp[i]);
                    WindArc(lvl, rr[i] * s, 0.025f * s, i == 1 ? light : wind);
                    WindArc(Spun(lvl, 180f), rr[i] * s * 0.92f, 0.016f * s, wind);
                }
            }
            else
            {
                // 手前：三道傾斜的風痕繞著手（弓）快速旋轉
                R = 0.22f * s;
                center = disc;
                var gr = Child(disc, "GrowGust");
                AddGrow(gr, Vector3.one * 1.5f, Vector3.one * 0.75f);   // 蓄力越久風捲得越緊
                float[] tilt = { 0f, 60f, -60f }, sp = { 420f, -380f, 340f };
                for (int i = 0; i < 3; i++)
                {
                    var t = Child(gr, "Gust" + i);
                    t.localRotation = Quaternion.Euler(tilt[i], i * 40f, 0f);
                    AddSpin(t, Vector3.forward, sp[i]);
                    WindArc(t, R * (1f - i * 0.12f), 0.016f * s, i == 0 ? light : wind);
                }
            }

            // 往中心收束的風痕粒子
            var st = SpellFx.NewPS(center, "Streaks", SpellFx.AddMat(SpellFx.TexSoft), 40, false);
            var m = st.main;
            float life = 0.35f, rad = R * 1.3f;
            m.startLifetime = life;
            m.startSpeed = -rad / life * 0.85f;
            m.startSize = 0.012f * s;
            m.startColor = new ParticleSystem.MinMaxGradient(SpellFx.WithA(light, 0.9f * alphaMul), SpellFx.WithA(wind, 0.7f * alphaMul));
            SpellFx.Sphere(st, rad, 0f);
            SpellFx.FadeInOut(st);
            SpellFx.Swirl(st, 3f, 0f);
            SpellFx.Stretch(st, 0.05f, 2f);
            AddPS(st, 50f);

            // 環繞的葉片／羽毛
            var lv = SpellFx.NewPS(center, "Leaves", SpellFx.AddMat(SpellFx.TexLeaf), 16, false);
            var lm = lv.main;
            lm.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            lm.startSpeed = -R * 0.4f;
            lm.startSize = new ParticleSystem.MinMaxCurve(0.035f * s, 0.06f * s);
            lm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            lm.startColor = new ParticleSystem.MinMaxGradient(SpellFx.WithA(Color.Lerp(col, new Color(0.45f, 0.9f, 0.3f), 0.5f), alphaMul), SpellFx.WithA(light, alphaMul));
            SpellFx.Sphere(lv, R, 0.3f);
            SpellFx.FadeInOut(lv);
            SpellFx.Swirl(lv, 2.5f, 0f);
            SpellFx.Spin(lv, 5f);
            AddPS(lv, 10f);

            // 收束點的光
            var glow = SpellFx.NewPS(center, "Glow", SpellFx.AddMat(SpellFx.TexSoft), 6, false);
            var gm = glow.main;
            gm.startLifetime = 0.3f;
            gm.startSize = (ground ? 0.35f : 0.13f) * s;
            gm.startColor = SpellFx.WithA(wind, 0.45f * alphaMul);
            SpellFx.FadeInOut(glow);
            AddPS(glow, 12f);

            SetReady(ground ? disc : center, ground ? 0.55f * s : R * 0.9f, 0.01f * s, light);
        }

        void WindArc(Transform parent, float r, float w, Color c)
        {
            var lr = SpellFx.Line(parent, "Wind", SpellFx.PartialCircle(16, r, 130f), false, w, c, true, true);
            lr.widthCurve = SpellFx.HeadTaper;   // 尾端細、頭端粗，像一道風痕
            AddLine(lr, SpellFx.WithA(c, 0.85f));
        }

        Transform Spun(Transform parent, float zDeg)
        {
            var t = Child(parent, "Rot");
            t.localRotation = Quaternion.Euler(0f, 0f, zDeg);
            return t;
        }

        void BuildAssassin(Transform disc, Color col, float s)
        {
            var violet = Color.Lerp(col, new Color(0.6f, 0.25f, 1f), 0.5f);
            var light = Color.Lerp(violet, Color.white, 0.35f);
            float R = (ground ? 0.42f : 0.14f) * s;

            // 暗影煙霧（一般混色）
            var smoke = SpellFx.NewPS(disc, "Smoke", SpellFx.BlendMat(SpellFx.TexSoft), 24, false);
            var m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
            m.startSize = new ParticleSystem.MinMaxCurve((ground ? 0.2f : 0.1f) * s, (ground ? 0.34f : 0.16f) * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            float sa = firstPerson ? 0.35f : 0.55f;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.03f, 0.16f, sa), new Color(0.24f, 0.09f, 0.34f, sa * 0.85f));
            if (ground) SpellFx.Disc(smoke, R); else SpellFx.Sphere(smoke, R);
            SpellFx.FadeInOut(smoke);
            SpellFx.SizeCurve(smoke, SpellFx.Grow);
            SpellFx.Rise(smoke, (ground ? 0.3f : 0.08f) * s);
            SpellFx.Noise(smoke, 0.15f * s, 1f);
            SpellFx.Spin(smoke, 1f);
            AddPS(smoke, 16f);

            // 閃爍的紫色火花
            var sk = SpellFx.NewPS(disc, "Sparks", SpellFx.AddMat(SpellFx.TexSoft), 40, false);
            var k = sk.main;
            k.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            k.startSpeed = new ParticleSystem.MinMaxCurve(0.1f * s, 0.4f * s);
            k.startSize = new ParticleSystem.MinMaxCurve(0.02f * s, 0.04f * s);
            k.startColor = new ParticleSystem.MinMaxGradient(SpellFx.WithA(light, alphaMul), SpellFx.WithA(violet, alphaMul));
            if (ground) SpellFx.Disc(sk, R); else SpellFx.Sphere(sk, R * 1.3f);
            SpellFx.SizeCurve(sk, SpellFx.Flicker);
            SpellFx.Noise(sk, 0.5f * s, 3f);
            SpellFx.Rise(sk, (ground ? 0.5f : 0.1f) * s);
            AddPS(sk, 35f);

            // 暗影核心（手前）或地上的暗紫光圈
            if (!ground)
            {
                var core = SpellFx.NewPS(disc, "Core", SpellFx.BlendMat(SpellFx.TexSoft), 6, false);
                var cm = core.main;
                cm.startLifetime = 0.4f;
                cm.startSize = 0.1f * s;
                cm.startColor = new Color(0.05f, 0f, 0.09f, firstPerson ? 0.5f : 0.75f);
                SpellFx.FadeInOut(core);
                AddPS(core, 10f);
            }
            var grow = Child(disc, "Grow");
            AddGrow(grow, Vector3.one * 0.5f, Vector3.one);
            var spin = Child(grow, "Spin");
            AddSpin(spin, Vector3.forward, -90f);
            AddLine(SpellFx.Line(spin, "Ring", SpellFx.Zigzag(10, R * 1.15f, R * 0.9f), true, 0.008f * s, violet, true, true), SpellFx.WithA(violet, 0.6f));
            AddLine(SpellFx.Line(grow, "Shadow", SpellFx.Circle(36, R * 1.2f), true, 0.03f * s, new Color(0.08f, 0f, 0.14f, 1f), false, true), new Color(0.08f, 0f, 0.14f, firstPerson ? 0.35f : 0.6f));

            SetReady(disc, R * 1.3f, 0.012f * s, light);
        }

        void BuildSwordsman(Transform disc, Color col, float s)
        {
            var gold = new Color(1f, 0.85f, 0.42f);
            var steel = Color.Lerp(col, Color.white, 0.6f);
            var bright = Color.Lerp(gold, Color.white, 0.5f);
            float H = (ground ? 1.8f : 0.36f) * s;

            // 光柱（地面）／劍芒（手前）：沿世界上方的直線，隨蓄力往上長
            var pillar = Child(transform, "Pillar");
            if (!ground) pillar.localPosition = new Vector3(0f, -H * 0.5f, 0f);
            AddGrow(pillar, new Vector3(1f, 0.15f, 1f), Vector3.one);
            var pts = new[] { Vector3.zero, new Vector3(0f, H, 0f) };
            var outer = SpellFx.Line(pillar, "Glow", pts, false, (ground ? 0.55f : 0.07f) * s, gold, true, false);
            outer.widthCurve = ground ? SpellFx.Taper : SpellFx.ArcTaper;
            AddLine(outer, SpellFx.WithA(Color.Lerp(gold, col, 0.3f), ground ? 0.35f : 0.5f));
            var coreL = SpellFx.Line(pillar, "Core", pts, false, (ground ? 0.08f : 0.018f) * s, bright, true, false);
            coreL.widthCurve = ground ? SpellFx.Taper : SpellFx.ArcTaper;
            AddLine(coreL, SpellFx.WithA(bright, 0.9f));
            if (!ground)
            {
                // 手前：十字劍芒（短橫線）
                var cross = Child(transform, "Cross");
                AddGrow(cross, new Vector3(0.2f, 1f, 1f), Vector3.one);
                var cp = new[] { new Vector3(-H * 0.32f, 0f, 0f), new Vector3(H * 0.32f, 0f, 0f) };
                var cl = SpellFx.Line(cross, "Cross", cp, false, 0.014f * s, bright, true, false);
                cl.widthCurve = SpellFx.ArcTaper;
                AddLine(cl, SpellFx.WithA(bright, 0.8f));
            }
            else
            {
                var ring = Child(disc, "Spin");
                AddSpin(ring, Vector3.forward, 40f);
                AddLine(SpellFx.Line(ring, "Ring", SpellFx.Circle(48, 0.45f * s), true, 0.016f * s, gold, true, true), SpellFx.WithA(gold, 0.8f));
                AddLine(SpellFx.Line(ring, "Ticks", SpellFx.Zigzag(24, 0.37f * s, 0.43f * s), true, 0.006f * s, steel, true, true), SpellFx.WithA(steel, 0.6f));
            }

            // 閃光星芒
            var gl = SpellFx.NewPS(transform, "Glints", SpellFx.AddMat(SpellFx.TexStar), 12, false);
            var m = gl.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            m.startSize = new ParticleSystem.MinMaxCurve((ground ? 0.08f : 0.035f) * s, (ground ? 0.16f : 0.06f) * s);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 0.5f);
            m.startColor = new ParticleSystem.MinMaxGradient(SpellFx.WithA(bright, alphaMul), SpellFx.WithA(steel, alphaMul));
            if (ground)
            {
                var sh = gl.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(0.6f * s, 1.5f * s, 0.6f * s);
                sh.position = new Vector3(0f, 0.9f * s, 0f);
            }
            else SpellFx.Sphere(gl, H * 0.45f);
            SpellFx.SizeCurve(gl, SpellFx.PopCurve);
            AddPS(gl, ground ? 9f : 6f);

            // 往上飄的金色光點
            var mo = SpellFx.NewPS(disc, "Motes", SpellFx.AddMat(SpellFx.TexSoft), 45, false);
            var mm = mo.main;
            mm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            mm.startSize = new ParticleSystem.MinMaxCurve((ground ? 0.025f : 0.012f) * s, (ground ? 0.045f : 0.022f) * s);
            mm.startColor = new ParticleSystem.MinMaxGradient(SpellFx.WithA(gold, alphaMul), SpellFx.WithA(steel, alphaMul));
            if (ground) SpellFx.Disc(mo, 0.4f * s); else SpellFx.Sphere(mo, H * 0.25f);
            SpellFx.FadeInOut(mo);
            SpellFx.SizeCurve(mo, SpellFx.Shrink);
            SpellFx.Rise(mo, (ground ? 1f : 0.3f) * s);
            AddPS(mo, 35f);

            SetReady(disc, ground ? 0.5f * s : H * 0.5f, 0.012f * s, bright);
        }

        // ---------------------------------------------------------------- 建構小工具
        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        void AddLine(LineRenderer lr, Color c) { lines.Add(lr); lineCol.Add(c); lineW.Add(lr.widthMultiplier); }
        void AddSpin(Transform t, Vector3 axis, float degPerSec) { spinT.Add(t); spinAxis.Add(axis); spinSpeed.Add(degPerSec); spinAngle.Add(Random.Range(0f, 360f)); spinBase.Add(t.localRotation); }
        void AddGrow(Transform t, Vector3 min, Vector3 max) { growT.Add(t); growMin.Add(min); growMax.Add(max); }
        void AddPS(ParticleSystem ps, float rate)
        {
            SpellFx.Rate(ps, rate);
            systems.Add(ps); baseRate.Add(rate);
        }

        void SetReady(Transform parent, float r, float w, Color c)
        {
            readyRing = Child(parent, "Ready");
            readyLine = SpellFx.Line(readyRing, "Line", SpellFx.Circle(48, 1f), true, w, c, true, true);
            readyLine.enabled = false;
            readyCol = c; readyW = w; readyR = r;
        }

        // ================================================================ 每幀更新（不配置記憶體）
        void Follow()
        {
            if (anchor == null) return;
            var pos = anchor.TransformPoint(offset);
            Quaternion rot;
            if (ground)
            {
                var f = anchor.forward; f.y = 0f;
                rot = f.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(f.normalized, Vector3.up) : Quaternion.identity;
            }
            else rot = anchor.rotation;
            transform.SetPositionAndRotation(pos, rot);
        }

        void LateUpdate()
        {
            if (anchor == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            clock += dt;
            Follow();

            float p = Mathf.Clamp01(Progress);
            shownP = Mathf.MoveTowards(shownP, p, dt * 4f);
            bool ready = !stopping && Progress >= 1f;
            readyAmt = Mathf.MoveTowards(readyAmt, ready ? 1f : 0f, dt * 5f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(clock * 12f);
            float ease = 1f - (1f - shownP) * (1f - shownP);

            float fade;
            if (stopping)
            {
                stopT += dt;
                fade = Mathf.Clamp01(1f - stopT / StopFade);
                if (stopT >= StopKill) { Destroy(gameObject); return; }
            }
            else fade = Mathf.Clamp01(clock / 0.15f);

            float inten = Mathf.Lerp(0.4f, 0.85f, ease) + readyAmt * (0.15f + 0.25f * pulse);
            float whiten = readyAmt * 0.45f * pulse;
            float wMul = 0.85f + 0.15f * ease + readyAmt * 0.35f * pulse;
            for (int i = 0; i < lines.Count; i++)
            {
                var lr = lines[i];
                if (lr == null) continue;
                var c = lineCol[i];
                float a = Mathf.Clamp01(c.a * inten * fade * alphaMul);
                if (c.maxColorComponent > 0.3f) c = Color.Lerp(c, Color.white, whiten);   // 暗色線條不變白
                c.a = a;
                lr.startColor = c; lr.endColor = c;
                lr.widthMultiplier = lineW[i] * wMul;
            }

            float spinMul = 0.6f + 0.6f * ease + readyAmt * 0.8f;
            for (int i = 0; i < spinT.Count; i++)
            {
                float ang = spinAngle[i] + spinSpeed[i] * spinMul * dt;
                if (ang > 3600f) ang -= 3600f; else if (ang < -3600f) ang += 3600f;
                spinAngle[i] = ang;
                spinT[i].localRotation = spinBase[i] * Quaternion.AngleAxis(ang, spinAxis[i]);
            }

            float beat = 1f + readyAmt * 0.06f * pulse;
            for (int i = 0; i < growT.Count; i++)
                growT[i].localScale = Vector3.LerpUnclamped(growMin[i], growMax[i], ease) * beat;

            if (!stopping)
            {
                float rateMul = (0.25f + 0.75f * ease) * (1f + 0.7f * readyAmt);
                for (int i = 0; i < systems.Count; i++)
                {
                    if (systems[i] == null) continue;
                    var em = systems[i].emission;
                    em.rateOverTimeMultiplier = baseRate[i] * rateMul;
                }
            }

            // 蓄力完成：一圈圈往外擴散的光環
            if (readyLine != null)
            {
                bool show = readyAmt > 0.01f && fade > 0.01f;
                if (readyLine.enabled != show) readyLine.enabled = show;
                if (show)
                {
                    float k = Mathf.Repeat(clock * 1.6f, 1f);
                    readyRing.localScale = Vector3.one * (readyR * Mathf.Lerp(0.55f, 1.35f, k));
                    var c = readyCol; c.a = (1f - k) * readyAmt * fade * alphaMul;
                    readyLine.startColor = c; readyLine.endColor = c;
                    readyLine.widthMultiplier = readyW * (1.5f - k);
                }
            }
        }
    }
}
