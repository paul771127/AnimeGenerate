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
            if (y - standY > JumpRise && vy > JumpSpeed)
            {
                if (me.airborneUntil < battle.now) Haptics.Play(Haptics.Kind.Light);
                me.airborneUntil = battle.now + JumpHold;
            }
        }

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
        public const float PickupEvery = 5f, PickupLife = 14f, PickupReach = 0.8f;
        public const int PickupMax = 3;
        static readonly string[] PickupNames = { "💚 生命結晶", "💙 魔力結晶", "💛 時之結晶" };
        static readonly Color[] PickupColors = { new Color(0.4f, 1f, 0.5f), new Color(0.4f, 0.7f, 1f), new Color(1f, 0.85f, 0.3f) };

        bool StoryBossFight => battle != null && endless && storyStage != null && battle.IsBossWave && En != null && En.Alive && !battle.Over;

        void UpdatePickups()
        {
            if (battle == null) return;
            if (StoryBossFight && pickups.Count < PickupMax && battle.now >= nextPickupAt)
            {
                nextPickupAt = battle.now + PickupEvery;
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
            else if (ec != null && ec.id == "x_quake") UiKit.Pill(H * 0.40f, "⚠ 震地波：環到腳邊時跳起來", fs, new Color(1f, 0.8f, 0.35f), pillW);
            if (Me.airborneUntil > battle.now) UiKit.Pill(H * 0.235f, "⬆ 跳躍", fsS, new Color(0.6f, 1f, 0.8f), pillW);
            if (pickups.Count > 0) UiKit.Pill(H * 0.53f, "✨ 地上有結晶：走過去蹲下撿起", fsS, new Color(0.7f, 1f, 0.8f), pillW);
        }
    }
}
