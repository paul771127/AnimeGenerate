using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事 Boss 戰的場上互動：震地波的地面衝擊環（跳起來躲）、地上的結晶（走過去蹲下撿）、跳躍偵測。
    /// </summary>
    public partial class SoloBattle
    {
        // ---------------------------------------------------------------- 跳躍偵測
        /// <summary>手機比站立高度高出 JumpRise 公尺、而且正以 JumpSpeed 以上往上移動＝跳起來（之後 JumpHold 秒內算在空中）</summary>
        public const float JumpRise = 0.1f, JumpSpeed = 0.6f, JumpHold = 0.45f;
        float lastHeadY = float.NaN;

        void UpdateJump(Fighter me, float y, float dt)
        {
            if (float.IsNaN(lastHeadY)) lastHeadY = y;
            float vy = (y - lastHeadY) / Mathf.Max(1e-3f, dt);
            lastHeadY = y;
            if (y - standY > JumpRiseNow && vy > JumpSpeed)
            {
                if (me.airborneUntil < battle.now) Haptics.Play(Haptics.Kind.Light);
                me.airborneUntil = battle.now + JumpHold;
            }
        }

        // ---------------------------------------------------------------- 身高校正
        /// <summary>
        /// 開打後前 CalibTime 秒站直、手機拿在眼前對著前方：記下手機離地的高度（站姿）。
        /// 第一次還會請玩家蹲下（手機一樣對著前方）再站起來：記下蹲姿高度（比例存起來，之後不用再蹲）。
        /// 蹲下、跳躍的門檻依這兩個高度調整；連線時也送給對方，對方畫面上的角色才能照著真人蹲下站起。
        /// </summary>
        public const float CalibTime = 2f, CrouchCalibTimeout = 15f;
        const string CrouchRatioKey = "SD_CrouchRatio";
        /// <summary>沒有校正過蹲姿時，蹲姿手機高度＝站姿的這個比例</summary>
        public const float DefaultCrouchRatio = 0.62f;
        float calibSum; int calibN; float standH = -1f;
        int crouchCalib;                 // 0 不用／完成、1 等蹲下、2 等站起來
        float crouchCalibMin, crouchCalibUntil;
        bool Calibrating => battle != null && battle.now < CalibTime;
        static float SavedCrouchRatio { get { try { return PlayerPrefs.GetFloat(CrouchRatioKey, -1f); } catch { return -1f; } } }
        float CrouchRatio => SavedCrouchRatio > 0.3f && SavedCrouchRatio < 0.9f ? SavedCrouchRatio : DefaultCrouchRatio;
        /// <summary>站姿手機高度（m）；還沒校正＝-1</summary>
        public float StandH => standH;
        /// <summary>蹲姿手機高度（m）；還沒校正站姿＝-1</summary>
        public float CrouchH => standH > 0f ? standH * CrouchRatio : -1f;
        /// <summary>蹲下的門檻：站姿與蹲姿高度差的 45%（夾在 18～40 公分）；還沒校正用預設值</summary>
        float CrouchDropNow => standH > 0f ? Mathf.Clamp((standH - CrouchH) * 0.45f, 0.18f, 0.4f) : CrouchDrop;
        /// <summary>跳起來的門檻：站立高度的 7%（1.5m → 10.5cm）</summary>
        float JumpRiseNow => standH > 0f ? Mathf.Clamp(standH * 0.07f, 0.06f, 0.12f) : JumpRise;

        /// <summary>站姿校正中回傳 true（這段時間不判斷蹲下／跳躍）</summary>
        bool UpdateCalibration(float y)
        {
            if (Calibrating) { calibSum += y; calibN++; return true; }
            if (calibN > 0)
            {
                float h = calibSum / calibN; calibN = 0;
                if (h > 0.6f && h < 2.3f)
                {
                    standH = h; standY = h;
                    if (SavedCrouchRatio <= 0f) { crouchCalib = 1; crouchCalibMin = h; crouchCalibUntil = battle.now + CrouchCalibTimeout; }
                }
            }
            UpdateCrouchCalibration(y);
            return false;
        }

        /// <summary>第一次的蹲姿校正：蹲到最低點、再站起來，記下最低的手機高度</summary>
        void UpdateCrouchCalibration(float y)
        {
            if (crouchCalib == 0 || standH <= 0f) return;
            if (battle.now > crouchCalibUntil) { crouchCalib = 0; return; }   // 沒蹲：先用預設比例，下一場再問
            crouchCalibMin = Mathf.Min(crouchCalibMin, y);
            if (crouchCalib == 1 && y < standH * 0.85f) crouchCalib = 2;
            else if (crouchCalib == 2 && y > standH * 0.92f)
            {
                float r = crouchCalibMin / standH;
                if (r > 0.3f && r < 0.85f) { try { PlayerPrefs.SetFloat(CrouchRatioKey, r); PlayerPrefs.Save(); } catch { } Say("📏 蹲姿校正完成", 1.2f); }
                crouchCalib = 0;
            }
        }

        void ResetCalibration() { calibSum = 0f; calibN = 0; standH = -1f; crouchCalib = 0; }

        // ---------------------------------------------------------------- 震地波的環
        readonly Dictionary<Battle.Shockwave, LineRenderer> quakeGo = new Dictionary<Battle.Shockwave, LineRenderer>();
        const int QuakeSegs = 64;

        void UpdateQuakeVisuals()
        {
            if (battle == null) return;
            var live = new HashSet<Battle.Shockwave>(battle.shockwaves);
            foreach (var w in battle.shockwaves)
            {
                if (!quakeGo.TryGetValue(w, out var lr) || !lr)
                {
                    var go = new GameObject("Quake Ring");
                    lr = go.AddComponent<LineRenderer>();
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.loop = true; lr.positionCount = QuakeSegs; lr.useWorldSpace = true;
                    lr.widthMultiplier = 0.14f;
                    quakeGo[w] = lr;
                }
                float r = Mathf.Max(0.05f, (battle.now - w.born) * w.speed);
                float fade = Mathf.Clamp01(1f - r / (w.maxR + 1f));
                var c = w.skill.color; c.a = 0.35f + 0.6f * fade;
                lr.startColor = lr.endColor = c;
                for (int i = 0; i < QuakeSegs; i++)
                {
                    float a = i * Mathf.PI * 2f / QuakeSegs;
                    lr.SetPosition(i, WorldFrame.FromWorld(w.center + new Vector3(Mathf.Cos(a) * r, 0.05f, Mathf.Sin(a) * r)));
                }
            }
            foreach (var kv in new List<KeyValuePair<Battle.Shockwave, LineRenderer>>(quakeGo))
                if (!live.Contains(kv.Key)) { if (kv.Value) Destroy(kv.Value.gameObject); quakeGo.Remove(kv.Key); }
        }

        // ---------------------------------------------------------------- 劍士的刀線
        /// <summary>劍士揮砍的刀線：出手＝亮白一閃；電腦出手前的預警＝紅線（蹲下或退開）</summary>
        void ShowSlashLine(Fighter f, Vector3 a, Vector3 b, Vector3 o, bool warn)
        {
            const int N = 16;
            var go = new GameObject(warn ? "Slash Warning" : "Slash Line");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.positionCount = N + 1; lr.useWorldSpace = true;
            lr.widthMultiplier = warn ? 0.035f : 0.06f;
            float reach = (a - o).magnitude;
            Vector3 da = (a - o).normalized, db = (b - o).normalized;
            for (int i = 0; i <= N; i++) lr.SetPosition(i, WorldFrame.FromWorld(o + Vector3.Lerp(da, db, i / (float)N).normalized * reach));
            // 敵人的預警＝紅色；自己的預警＝職業色半透明（告訴你刀線在哪、還沒砍下）
            var c = warn ? (f == Me ? new Color(f.cls.color.r, f.cls.color.g, f.cls.color.b, 0.5f) : new Color(1f, 0.25f, 0.2f, 0.85f)) : Color.Lerp(f.cls.color, Color.white, 0.6f);
            lr.startColor = lr.endColor = c;
            go.AddComponent<SlashLineFade>().Begin(lr, warn ? Mathf.Max(0.2f, f.windupUntil - battle.now) : 0.25f, c);
            if (warn) { Sfx.Play("cast_swordsman", 0.3f, 0.7f); if (f != Me) Sfx.Play("blade_warn", 0.7f); }   // 敵人的預警：拔刀聲
            // 敵人的預警：同時擺出攻擊架勢（預備的長度＝預警時間，出手的瞬間＝判定）
            if (warn && f == En && enemyRig) enemyRig.PlayAttack(f.cls != null && f.cls.id == "assassin" ? "stab" : "slash", Mathf.Max(0.05f, f.windupUntil - battle.now));
            else Sfx.Play("cast_swordsman", 0.45f, 1.1f);
            // 雙人對戰：我的刀線也送給對手（對手看到紅色預警才躲得掉）
            if (f == Me && battle.remoteEnemy && !coop) NetSend?.Invoke(new Msg { t = "slash", pts = new[] { a, b, o }, ok = warn, s = Mathf.Max(0f, f.windupUntil - battle.now) });
        }

        /// <summary>
        /// 遠程普攻的瞄準預警：整條半透明射線（射手細、遠端粗），加上射線到地面的半透明「垂簾」和地上的投影線。
        /// 射線正對著你時也看得出高低：垂簾會變成從射線垂到地板的一條直帶，地上的線看得出左右方向。
        /// 敵人＝紅色，自己＝職業色。
        /// </summary>
        void ShowShotLine(Fighter f, Vector3 from, Vector3 to)
        {
            var go = new GameObject("Shot Warning");
            var col = f == Me ? f.cls.color : new Color(1f, 0.25f, 0.2f);
            var sh = Shader.Find("Sprites/Default");
            var fx = go.AddComponent<ShotWarnFx>();

            // 射線：起點細、遠端粗，往遠處淡出
            var beam = go.AddComponent<LineRenderer>();
            beam.material = new Material(sh); beam.useWorldSpace = true; beam.positionCount = 2;
            beam.SetPosition(0, WorldFrame.FromWorld(from)); beam.SetPosition(1, WorldFrame.FromWorld(to));
            beam.widthCurve = new AnimationCurve(new Keyframe(0f, 0.02f), new Keyframe(1f, 0.07f));
            fx.Add(beam, new Color(col.r, col.g, col.b, 0.65f), new Color(col.r, col.g, col.b, 0.25f));

            // 地上的投影線
            var gl = new GameObject("Shot Ground").AddComponent<LineRenderer>();
            gl.transform.SetParent(go.transform, false);
            gl.material = new Material(sh); gl.useWorldSpace = true; gl.positionCount = 2; gl.widthMultiplier = 0.04f;
            gl.SetPosition(0, WorldFrame.FromWorld(new Vector3(from.x, 0.02f, from.z))); gl.SetPosition(1, WorldFrame.FromWorld(new Vector3(to.x, 0.02f, to.z)));
            fx.Add(gl, new Color(col.r, col.g, col.b, 0.45f), new Color(col.r, col.g, col.b, 0.15f));

            // 垂簾：射線到地面的半透明面（雙面），越遠越淡
            const int N = 12;
            var v = new Vector3[(N + 1) * 2]; var c = new Color[(N + 1) * 2]; var tri = new int[N * 6];
            for (int i = 0; i <= N; i++)
            {
                float k = i / (float)N; var p = Vector3.Lerp(from, to, k);
                v[i * 2] = WorldFrame.FromWorld(p); v[i * 2 + 1] = WorldFrame.FromWorld(new Vector3(p.x, 0.02f, p.z));
                c[i * 2] = new Color(col.r, col.g, col.b, Mathf.Lerp(0.22f, 0.06f, k)); c[i * 2 + 1] = new Color(col.r, col.g, col.b, Mathf.Lerp(0.08f, 0.02f, k));
                if (i < N) { int t = i * 6, b = i * 2; tri[t] = b; tri[t + 1] = b + 2; tri[t + 2] = b + 1; tri[t + 3] = b + 1; tri[t + 4] = b + 2; tri[t + 5] = b + 3; }
            }
            var mesh = new Mesh { name = "Shot Curtain" }; mesh.vertices = v; mesh.colors = c; mesh.triangles = tri; mesh.RecalculateBounds();
            var cg = new GameObject("Shot Curtain"); cg.transform.SetParent(go.transform, false);
            cg.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = cg.AddComponent<MeshRenderer>(); mr.material = new Material(sh);   // Sprites/Default 不剔除背面：兩面都看得到
            fx.Add(mr, mesh);

            fx.Begin(Battle.ShotWindup + 0.15f);
            if (f != Me) Sfx.Play("aim_warn", 0.6f);   // 敵人在瞄準：上升的蜂鳴
            if (f == En && enemyRig) enemyRig.PlayAttack(f.cls != null && f.cls.id == "archer" ? "shoot" : "magic", Battle.ShotWindup);   // 拉弓／舉杖，射出時放開
            if (f == Me && battle.remoteEnemy && !coop) NetSend?.Invoke(new Msg { t = "shotwarn", pts = new[] { from, to } });
        }

        /// <summary>震地波的環快到我腳邊（1.2 秒內）</summary>
        bool QuakeComing(out float eta)
        {
            eta = 99f;
            if (battle == null || Me == null) return false;
            foreach (var w in battle.shockwaves)
            {
                if (w.done.Contains(Me)) continue;
                float r = (battle.now - w.born) * w.speed, d = Battle.FlatDistance(w.center, Me.Feet);
                if (d < r - Battle.QuakeBand) continue;
                eta = Mathf.Min(eta, (d - r) / w.speed);
            }
            return eta < 1.2f;
        }

        // ---------------------------------------------------------------- 地上的結晶（故事 Boss 戰）
        class Pickup { public Vector3 pos; public int kind; public float born, until; public GameObject go; }
        readonly List<Pickup> pickups = new List<Pickup>();
        float nextPickupAt;
        public const float PickupLife = 14f, PickupReach = 0.8f;
        /// <summary>結晶出現的間隔：深海巨妖最多（逼你到處撿），熔岩巨像最少</summary>
        static float PickupEveryFor(string bossCls) => bossCls == "boss_sea" ? 3.5f : bossCls == "boss_forest" ? 5f : 6.5f;
        public const int PickupMax = 3;
        static readonly string[] PickupNames = { "💚 生命結晶", "💙 魔力結晶", "💛 時之結晶" };
        static readonly Color[] PickupColors = { new Color(0.4f, 1f, 0.5f), new Color(0.4f, 0.7f, 1f), new Color(1f, 0.85f, 0.3f) };

        bool StoryBossFight => battle != null && endless && storyStage != null && battle.IsBossWave && En != null && En.Alive && !battle.Over;

        void UpdatePickups()
        {
            if (battle == null) return;
            if (StoryBossFight && pickups.Count < PickupMax && battle.now >= nextPickupAt)
            {
                nextPickupAt = battle.now + PickupEveryFor(En.cls.id);
                if (FindPickupSpot(out var p))
                {
                    int kind = Me.hp < Me.maxHp * 0.5f ? 0 : Random.Range(0, 3);
                    var go = Prim(PrimitiveType.Sphere, PickupColors[kind], false);
                    go.transform.localScale = Vector3.one * 0.22f;
                    pickups.Add(new Pickup { pos = p, kind = kind, born = Time.time, until = battle.now + PickupLife, go = go });
                    SpawnFade.Play(go, 0.5f);
                }
            }
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                var k = pickups[i];
                bool gone = battle.now > k.until || !StoryBossFight;
                // 走到旁邊、蹲下就撿起來
                if (!gone && Me.Alive && Battle.FlatDistance(Me.Feet, k.pos) < PickupReach && Me.crouching)
                {
                    ApplyPickup(k.kind);
                    gone = true;
                }
                if (gone) { if (k.go) Destroy(k.go); pickups.RemoveAt(i); continue; }
                float bob = 0.18f + 0.05f * Mathf.Sin((Time.time - k.born) * 3f);
                k.go.transform.position = WorldFrame.FromWorld(k.pos + Vector3.up * bob);
                k.go.transform.Rotate(0f, 90f * Time.deltaTime, 0f);
            }
        }

        bool FindPickupSpot(out Vector3 p)
        {
            p = Vector3.zero;
            var poly = area.Polygon;
            if (poly == null || poly.Count < 3) return false;
            var c = Vector3.zero; foreach (var q in poly) c += q; c /= poly.Count;
            for (int tries = 0; tries < 20; tries++)
            {
                float a = Random.value * Mathf.PI * 2f, r = Random.Range(0.5f, 4.5f);
                var s = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!area.Inside(s) || area.DistanceToEdge(s) < 0.8f) continue;
                var w = WorldFrame.ToWorld(s); w.y = 0f;
                float d = Battle.FlatDistance(w, Me.Feet);
                if (d < 2f || d > 6.5f) continue;
                p = w; return true;
            }
            return false;
        }

        void ApplyPickup(int kind)
        {
            var me = Me;
            switch (kind)
            {
                case 0: me.hp = Mathf.Min(me.maxHp, me.hp + me.maxHp * 0.2f); Sfx.Play("heal", 0.8f); break;
                case 1: me.mp = Mathf.Min(me.maxMp, me.mp + 40f); Sfx.Play("potion", 0.8f); break;
                default:
                    foreach (var id in new List<string>(me.cooldownUntil.Keys)) me.cooldownUntil[id] -= 4f;
                    Sfx.Play("coin", 0.8f); break;
            }
            Haptics.Play(Haptics.Kind.Success);
            floaters.Add(new Floater { text = PickupNames[kind] + (kind == 0 ? " +20% HP" : kind == 1 ? " +40 MP" : " 冷卻 −4 秒"), color = PickupColors[kind], born = Time.time, screen = true });
        }

        void ClearArena()
        {
            foreach (var lr in quakeGo.Values) if (lr) Destroy(lr.gameObject);
            quakeGo.Clear();
            foreach (var k in pickups) if (k.go) Destroy(k.go);
            pickups.Clear();
            nextPickupAt = 0f; lastHeadY = float.NaN;
        }

        /// <summary>戰鬥畫面上的提示：震地波要跳、地上有結晶</summary>
        void DrawArenaHints(float H, int fs, int fsS, float pillW)
        {
            if (battle == null || Me == null) return;
            var ec = En != null ? En.charging : null;
            if (QuakeComing(out _)) UiKit.Pill(H * 0.40f, "⬆ 跳！", fs * 2, new Color(1f, 0.85f, 0.3f), pillW);
            else if (Battle.IsQuake(ec)) UiKit.Pill(H * 0.40f, $"⚠ {ec.name}：環到腳邊時跳起來", fs, ec.color, pillW);
            if (Me.airborneUntil > battle.now) UiKit.Pill(H * 0.235f, "⬆ 跳躍", fsS, new Color(0.6f, 1f, 0.8f), pillW);
            if (Calibrating) UiKit.Pill(H * 0.3f, "📏 站直，手機拿在眼前對著前方（2 秒）", fsS, new Color(0.8f, 0.9f, 1f), pillW);
            else if (crouchCalib == 1) UiKit.Pill(H * 0.3f, "📏 手機一樣對著前方，蹲下", fsS, new Color(0.8f, 0.9f, 1f), pillW);
            else if (crouchCalib == 2) UiKit.Pill(H * 0.3f, "📏 好，再站起來", fsS, new Color(0.8f, 0.9f, 1f), pillW);
            if (pickups.Count > 0) UiKit.Pill(H * 0.53f, "✨ 地上有結晶：走過去蹲下撿起", fsS, new Color(0.7f, 1f, 0.8f), pillW);
        }
    }

    /// <summary>刀線淡出後刪掉</summary>
    public class SlashLineFade : MonoBehaviour
    {
        LineRenderer lr; float life, t; Color c;
        public void Begin(LineRenderer l, float d, Color col) { lr = l; life = Mathf.Max(0.05f, d); c = col; }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            if (lr) { var x = c; x.a = c.a * (1f - k * k); lr.startColor = lr.endColor = x; }
            if (k >= 1f) { if (lr) Destroy(lr.material); Destroy(gameObject); }
        }
    }

    /// <summary>瞄準預警的淡出：射線、地上的線、垂簾一起淡出後刪掉</summary>
    public class ShotWarnFx : MonoBehaviour
    {
        readonly System.Collections.Generic.List<(LineRenderer lr, Color a, Color b)> lines = new System.Collections.Generic.List<(LineRenderer, Color, Color)>();
        MeshRenderer curtain; Mesh mesh; Color[] baseCols;
        float life = 0.5f, t;
        public void Add(LineRenderer lr, Color a, Color b) { lines.Add((lr, a, b)); lr.startColor = a; lr.endColor = b; }
        public void Add(MeshRenderer mr, Mesh m) { curtain = mr; mesh = m; baseCols = m.colors; }
        public void Begin(float d) { life = Mathf.Max(0.05f, d); }
        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life), a = 1f - k * k;
            foreach (var (lr, ca, cb) in lines) if (lr) { lr.startColor = new Color(ca.r, ca.g, ca.b, ca.a * a); lr.endColor = new Color(cb.r, cb.g, cb.b, cb.a * a); }
            if (mesh != null && baseCols != null)
            {
                var cs = new Color[baseCols.Length];
                for (int i = 0; i < cs.Length; i++) { var c = baseCols[i]; cs[i] = new Color(c.r, c.g, c.b, c.a * a); }
                mesh.colors = cs;
            }
            if (k >= 1f) Destroy(gameObject);
        }
        void OnDestroy()
        {
            foreach (var (lr, _, _) in lines) if (lr) Destroy(lr.material);
            if (curtain) Destroy(curtain.material);
            if (mesh != null) Destroy(mesh);
        }
    }
}
