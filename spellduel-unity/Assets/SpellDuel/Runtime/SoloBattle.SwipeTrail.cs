using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 揮砍殘影（劍士、初心者的手掌劃線普攻）：手在鏡頭前移動的當下，眼前約 0.6m 跟著拖出一道殘影（像真的在揮刀），
    /// 不用等劃完才看到刀線。只畫最近 0.22 秒、手移動夠快的部分；手停下來、握拳、詠唱中就慢慢淡掉。
    /// 判定不變（照舊是劃完後的刀線）。
    /// </summary>
    public partial class SoloBattle
    {
        LineRenderer swipeTrail;
        readonly List<(Vector3 p, float t)> trailPts = new List<(Vector3, float)>();
        Vector2 trailLastPalm; float trailLastT;
        const float TrailLife = 0.22f, TrailDist = 0.6f, TrailMinSpeed = 0.6f;   // 最短速度：每秒移動螢幕寬的幾倍

        void UpdateSwipeTrail(bool active)
        {
            float now = Time.time;
            if (active && cam != null)
            {
                var palm = Hand.Palm;
                float dt = Mathf.Max(1e-3f, now - trailLastT);
                float speed = (palm - trailLastPalm).magnitude / Mathf.Max(1f, Screen.width) / dt;
                trailLastPalm = palm; trailLastT = now;
                if (speed >= TrailMinSpeed || trailPts.Count > 0 && now - trailPts[trailPts.Count - 1].t < 0.06f)
                {
                    var ray = cam.ScreenPointToRay(palm);
                    trailPts.Add((ray.origin + ray.direction * TrailDist, now));
                }
            }
            trailPts.RemoveAll(q => now - q.t > TrailLife);
            if (trailPts.Count < 2) { if (swipeTrail) swipeTrail.enabled = false; return; }
            if (!swipeTrail)
            {
                var go = new GameObject("Swipe Trail");
                swipeTrail = go.AddComponent<LineRenderer>();
                swipeTrail.material = new Material(Shader.Find("Sprites/Default"));
                swipeTrail.useWorldSpace = true; swipeTrail.numCapVertices = 3;
                swipeTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));   // 尾巴細、手的位置粗
            }
            var col = myClass == Skills.Novice ? new Color(0.85f, 0.62f, 0.35f) : Color.Lerp(Skills.Classes[myClass].color, Color.white, 0.5f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(col, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 1f) });
            swipeTrail.colorGradient = g;
            swipeTrail.widthMultiplier = 0.035f;
            swipeTrail.enabled = true;
            swipeTrail.positionCount = trailPts.Count;
            for (int i = 0; i < trailPts.Count; i++) swipeTrail.SetPosition(i, trailPts[i].p);
        }

        // ---------------------------------------------------------------- 集氣制普攻（初心者、劍士、刺客）的集氣指示
        // 畫面上的手掌：集氣中慢慢累積；集氣完成＝閃一下（輕震、音效），之後持續發光＝可以出手。三個職業的樣子不同：
        //   初心者：灰色圓圈中間長出紅色，完成後紅色脈動「揮！」
        //   劍士：掌心一道藍白劍芒由下往上凝聚，完成後藍白劍光＋星芒閃爍「斬！」
        //   刺客：暗紅的圈從外往掌心收攏，完成後掌心一點紅光跳動「刺！」
        float palmCdStart, palmCdEnd, palmReadyAt = -9f; bool palmWasReady = true;

        static string ChargeReadyHint(SkillDef s) => s.id == "na_sword" ? "手掌泛起劍光才能斬" : s.id == "na_dagger" ? "手掌凝聚紅光才能刺" : "手掌變紅才能敲";

        void DrawPalmCharge(int fs)
        {
            var basic = Me?.basic ?? Skills.BasicOf(myClass);
            if (!Skills.IsChargedBasic(basic) || battle == null || Me == null || !Me.Alive) return;
            float now = battle.now;
            Me.cooldownUntil.TryGetValue(basic.id, out var cd);
            if (cd > now && cd != palmCdEnd) { palmCdStart = now; palmCdEnd = cd; }   // 剛出手：開始集氣
            bool ready = cd <= now && Me.charging == null;
            if (ready && !palmWasReady && Event.current.type == EventType.Repaint)
            {
                palmReadyAt = Time.time;   // 集氣完成的那一刻：閃一下
                Sfx.Play("charged", 0.45f, basic.id == "na_sword" ? 1.7f : basic.id == "na_dagger" ? 1.1f : 1.4f); Haptics.Play(Haptics.Kind.Light);
            }
            if (Event.current.type == EventType.Repaint) palmWasReady = ready;
            if (Hand == null || !Hand.HandVisible) return;
            float H = Screen.height;
            var c = new Vector2(Hand.Palm.x, H - Hand.Palm.y);
            float r = fs * 2.6f;
            float k = ready ? 1f : palmCdEnd > palmCdStart ? Mathf.Clamp01((now - palmCdStart) / (palmCdEnd - palmCdStart)) : 1f;
            float since = Time.time - palmReadyAt;
            Rect Sq(float rad) => new Rect(c.x - rad, c.y - rad, rad * 2, rad * 2);
            switch (basic.id)
            {
                case "na_sword": DrawSwordCharge(c, r, k, ready, since, fs, Sq); break;
                case "na_dagger": DrawDaggerCharge(c, r, k, ready, since, fs, Sq); break;
                default: DrawStickCharge(c, r, k, ready, since, fs, Sq); break;
            }
        }

        static void DrawStickCharge(Vector2 c, float r, float k, bool ready, float since, int fs, System.Func<float, Rect> Sq)
        {
            if (!ready)
            {
                UiKit.Disc(Sq(r), new Color(0.6f, 0.6f, 0.65f, 0.25f));
                UiKit.Disc(Sq(r * Mathf.Lerp(0.15f, 0.9f, k)), new Color(1f, 0.25f, 0.2f, 0.25f + 0.25f * k));
                return;
            }
            if (since < 0.35f) { float e = since / 0.35f; UiKit.Disc(Sq(r * (1f + 0.8f * e)), new Color(1f, 0.95f, 0.85f, 0.75f * (1f - e))); }   // 閃一下：白色光圈往外擴散
            UiKit.Disc(Sq(r), new Color(1f, 0.15f, 0.1f, 0.45f + 0.15f * Mathf.Sin(Time.time * 9f)));
            UiKit.Text(new Rect(c.x - r, c.y + r * 0.9f, r * 2, fs * 1.3f), "揮！", fs, Color.white, TextAnchor.MiddleCenter, true);
        }

        /// <summary>劍士：掌心一道藍白劍芒（直立的細長光條）由下往上凝聚；完成後劍光脈動＋星芒</summary>
        static void DrawSwordCharge(Vector2 c, float r, float k, bool ready, float since, int fs, System.Func<float, Rect> Sq)
        {
            var steel = new Color(0.62f, 0.85f, 1f);
            float bladeH = r * 2.2f, bladeW = r * 0.16f;
            UiKit.Disc(Sq(r * 0.95f), new Color(0.35f, 0.55f, 0.8f, ready ? 0.22f : 0.15f));
            var prev = GUI.color;
            // 劍芒的底（暗）＋已凝聚的部分（由下往上）
            GUI.color = new Color(0.45f, 0.6f, 0.8f, 0.25f);
            GUI.DrawTexture(new Rect(c.x - bladeW / 2, c.y - bladeH / 2, bladeW, bladeH), Texture2D.whiteTexture);
            float filled = bladeH * (ready ? 1f : k);
            GUI.color = ready ? new Color(0.9f, 0.97f, 1f, 0.85f + 0.15f * Mathf.Sin(Time.time * 10f)) : new Color(steel.r, steel.g, steel.b, 0.45f + 0.4f * k);
            GUI.DrawTexture(new Rect(c.x - bladeW / 2, c.y + bladeH / 2 - filled, bladeW, filled), Texture2D.whiteTexture);
            // 護手（橫的一小條）
            GUI.color = new Color(steel.r, steel.g, steel.b, 0.6f);
            GUI.DrawTexture(new Rect(c.x - r * 0.45f, c.y + bladeH * 0.28f, r * 0.9f, bladeW * 0.8f), Texture2D.whiteTexture);
            GUI.color = prev;
            if (!ready) return;
            if (since < 0.35f) { float e = since / 0.35f; UiKit.Halo(Sq(r * (1.1f + 1.2f * e)), new Color(0.85f, 0.95f, 1f, 1f - e), 1f); }   // 集氣完成：劍光往外一閃
            UiKit.Halo(Sq(r * 1.1f), new Color(0.6f, 0.85f, 1f, 0.55f + 0.2f * Mathf.Sin(Time.time * 7f)), 0.8f);
            float tw = r * (0.55f + 0.2f * Mathf.Abs(Mathf.Sin(Time.time * 5f)));   // 劍尖的星芒
            UiKit.Star(new Rect(c.x - tw / 2, c.y - bladeH / 2 - tw / 2, tw, tw), new Color(1f, 1f, 1f, 0.9f));
            UiKit.Text(new Rect(c.x - r, c.y + r * 1.15f, r * 2, fs * 1.3f), "斬！", fs, new Color(0.85f, 0.95f, 1f), TextAnchor.MiddleCenter, true);
        }

        /// <summary>刺客：暗紅的圈從外往掌心收攏（凝聚殺氣）；完成後掌心一點紅光跳動</summary>
        static void DrawDaggerCharge(Vector2 c, float r, float k, bool ready, float since, int fs, System.Func<float, Rect> Sq)
        {
            var blood = new Color(1f, 0.12f, 0.18f);
            if (!ready)
            {
                UiKit.Disc(Sq(r * 0.9f), new Color(0.15f, 0.02f, 0.05f, 0.3f));
                UiKit.Halo(Sq(r * Mathf.Lerp(2.0f, 0.55f, k)), new Color(blood.r, blood.g, blood.b, 0.35f + 0.45f * k), 0.6f);
                UiKit.Disc(Sq(r * 0.12f * (1f + k)), new Color(blood.r, blood.g, blood.b, 0.3f + 0.5f * k));
                return;
            }
            if (since < 0.3f) { float e = since / 0.3f; UiKit.Halo(Sq(r * Mathf.Lerp(1.6f, 0.3f, e)), new Color(1f, 0.6f, 0.6f, 1f - e), 1f); }   // 集氣完成：紅光往掌心一收
            float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 6f)), 4f);   // 像心跳一樣一下一下
            UiKit.Disc(Sq(r * 0.85f), new Color(0.2f, 0.02f, 0.05f, 0.35f));
            UiKit.Halo(Sq(r * (0.7f + 0.25f * beat)), new Color(blood.r, blood.g, blood.b, 0.6f + 0.3f * beat), 1f);
            UiKit.Disc(Sq(r * (0.28f + 0.1f * beat)), new Color(1f, 0.35f, 0.35f, 0.95f));
            UiKit.Text(new Rect(c.x - r, c.y + r * 0.95f, r * 2, fs * 1.3f), "刺！", fs, new Color(1f, 0.75f, 0.75f), TextAnchor.MiddleCenter, true);
        }

        // ---------------------------------------------------------------- 劍士的劍光一閃、刺客的紅光突刺
        /// <summary>劍士砍下去：沿著刀線（自己眼前那一段）一道細白劍光從頭掃到尾，掃完刀尖亮一下星芒</summary>
        void ShowSwordGleam(Vector3[] pts)
        {
            var go = new GameObject("Sword Gleam");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace = true; lr.numCapVertices = 4;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0.2f));
            lr.widthMultiplier = 0.045f;
            go.AddComponent<SwordGleamFx>().Begin(lr, pts);
            Sfx.Play("cast_swordsman", 0.4f, 1.8f);
        }

        /// <summary>刺客突刺：從畫面上手的位置往準星方向刺出一道紅光（外層暗紅、中間亮），尖端爆一下</summary>
        void ShowStabFlash(Vector3 tip, Color c)
        {
            var red = new Color(1f, 0.15f, 0.2f);
            bool mine = Me != null && Battle.FlatDistance(tip, Me.Chest) < 2.2f && cam != null;
            var tipV = WorldFrame.FromWorld(tip);
            if (mine)
            {
                var t = cam.transform;
                Vector3 from = Hand != null && Hand.HandVisible
                    ? cam.ScreenPointToRay(Hand.Palm).GetPoint(0.3f)
                    : t.position + t.forward * 0.3f + t.right * 0.1f - t.up * 0.12f;
                // 尖端：往準星方向（不是往地上）
                var dir = t.forward; float len = Mathf.Max(0.8f, Vector3.Distance(from, tipV));
                var to = from + (Vector3.Lerp(dir, (tipV - from).normalized, 0.3f)).normalized * len;
                var go = new GameObject("Stab Flash");
                LineRenderer Make(float w, Color col)
                {
                    var child = new GameObject("Beam"); child.transform.SetParent(go.transform, false);
                    var lr = child.AddComponent<LineRenderer>();
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.useWorldSpace = true; lr.positionCount = 2; lr.numCapVertices = 2;
                    lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));   // 尖端收細＝刺出去的樣子
                    lr.widthMultiplier = w; lr.startColor = lr.endColor = col;
                    return lr;
                }
                var outer = Make(0.11f, new Color(0.7f, 0f, 0.08f, 0.55f));
                var core = Make(0.035f, new Color(1f, 0.75f, 0.75f, 1f));
                go.AddComponent<StabFlashFx>().Begin(from, to, outer, core);
                SpellFx.CastBurst("assassin", red, to, dir, 0.9f);
                Haptics.Play(Haptics.Kind.Light); Kick(3f, 0f);
            }
            else SpellFx.CastBurst("assassin", red, tipV, Vector3.up, 0.8f);
            Sfx.Play("cast_swordsman", 0.45f, 1.5f);
        }

        void ClearSwipeTrail() { trailPts.Clear(); if (swipeTrail) swipeTrail.enabled = false; }
    }

    /// <summary>劍光一閃：一小段亮光沿著刀線從頭掃到尾（約 0.12 秒），再淡掉</summary>
    public class SwordGleamFx : MonoBehaviour
    {
        LineRenderer lr; Vector3[] pts; float t;
        const float Sweep = 0.12f, Fade = 0.15f;
        public void Begin(LineRenderer l, Vector3[] p) { lr = l; pts = p; Update(); }
        void Update()
        {
            t += Time.deltaTime;
            if (lr == null || pts == null || pts.Length < 2) { Destroy(gameObject); return; }
            int n = pts.Length - 1;
            float head = Mathf.Clamp01(t / Sweep) * n, tail = Mathf.Max(0f, head - n * 0.45f);
            const int M = 10;
            lr.positionCount = M + 1;
            for (int i = 0; i <= M; i++) lr.SetPosition(i, At(Mathf.Lerp(tail, head, i / (float)M)));
            float a = t <= Sweep ? 1f : 1f - Mathf.Clamp01((t - Sweep) / Fade);
            lr.startColor = new Color(0.7f, 0.9f, 1f, 0f); lr.endColor = new Color(1f, 1f, 1f, a);
            if (t > Sweep + Fade) { Destroy(lr.material); Destroy(gameObject); }
        }
        Vector3 At(float x) { int i = Mathf.Min(pts.Length - 2, Mathf.FloorToInt(x)); return Vector3.Lerp(pts[i], pts[i + 1], x - i); }
    }

    /// <summary>紅光突刺：0.07 秒從手刺到尖端，停一下後 0.22 秒淡掉</summary>
    public class StabFlashFx : MonoBehaviour
    {
        Vector3 from, to; LineRenderer outer, core; Color oc, cc; float t;
        const float Extend = 0.07f, Hold = 0.05f, Fade = 0.22f;
        public void Begin(Vector3 a, Vector3 b, LineRenderer o, LineRenderer c) { from = a; to = b; outer = o; core = c; oc = o.startColor; cc = c.startColor; Update(); }
        void Update()
        {
            t += Time.deltaTime;
            var tip = Vector3.Lerp(from, to, Mathf.Clamp01(t / Extend));
            float a = 1f - Mathf.Clamp01((t - Extend - Hold) / Fade);
            foreach (var (lr, col) in new[] { (outer, oc), (core, cc) })
            {
                if (!lr) continue;
                lr.SetPosition(0, from); lr.SetPosition(1, tip);
                var x = col; x.a *= a; lr.startColor = lr.endColor = x;
            }
            if (t > Extend + Hold + Fade) { if (outer) Destroy(outer.material); if (core) Destroy(core.material); Destroy(gameObject); }
        }
    }
}
