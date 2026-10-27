using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>
    /// 變種怪的外觀（Combat.Variants.cs）：掛在小怪骨架上，在 MinionRig 每幀上色之後（LateUpdate）再改顏色。
    ///   刺客怪：全身近乎黑色、發光的部分（熔岩、眼睛）變成血紅；雙手加上匕首或長爪（沒有手的怪＝胸前兩把長爪）。
    ///   天候怪：整隻偏向天候的顏色。
    ///   Sneak＝只看得到發光的部分（刺客繞路中、霧行者離遠了）。
    /// </summary>
    public sealed class MinionLook : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color AssassinGlow = new Color(1f, 0.16f, 0.32f), BladeCol = new Color(0.78f, 0.8f, 0.86f), HiltCol = new Color(0.12f, 0.08f, 0.1f);
        VariantDef def;
        Renderer[] rends;
        readonly Dictionary<Renderer, Color> lastOut = new Dictionary<Renderer, Color>();
        readonly HashSet<Renderer> glow = new HashSet<Renderer>(), weapon = new HashSet<Renderer>();
        MaterialPropertyBlock blk;
        public bool Sneak { get; set; }
        bool shownSneak;

        public static MinionLook Apply(MinionRig rig, VariantDef v)
        {
            var look = rig.gameObject.AddComponent<MinionLook>();
            look.def = v; look.blk = new MaterialPropertyBlock();
            if (v.assassin) look.AddWeapons(rig.transform, v.claws);
            look.rends = rig.GetComponentsInChildren<Renderer>(true);
            return look;
        }

        void AddWeapons(Transform root, bool claws)
        {
            // 找手：Hand／Wrist／Claw／Arm（Rider 底下也算），最多兩隻；找不到就裝在身體正面
            var hands = new List<Transform>();
            string[] keys = { "HandL", "Hand", "Wrist", "ClawL", "ClawR", "ArmL", "ArmR", "ShoulderL", "ShoulderR" };
            foreach (var key in keys)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == key && !hands.Contains(t) && hands.Count < 2) hands.Add(t);
            if (hands.Count == 0)
            {
                var body = root.Find("Body") ?? root;
                for (int s = -1; s <= 1; s += 2) { var p = new GameObject("Assassin claw mount").transform; p.SetParent(body, false); p.localPosition = new Vector3(s * 0.22f, 0.45f, 0.25f); hands.Add(p); }
            }
            foreach (var h in hands)
            {
                var w = new GameObject("AssassinWeapon").transform; w.SetParent(h, false);
                w.localPosition = new Vector3(0f, -0.04f, 0.06f);
                if (claws)
                    for (int i = -1; i <= 1; i++)
                        EnemyArt.Part(w, "Long claw", new Vector3(i * 0.035f, 0f, 0.2f), new Vector3(0.025f, 0.03f, 0.38f), BladeCol, true, new Vector3(90f, i * 8f, 0f));
                else
                {
                    EnemyArt.Part(w, "Dagger grip", Vector3.zero, new Vector3(0.04f, 0.04f, 0.1f), HiltCol);
                    EnemyArt.Part(w, "Dagger guard", new Vector3(0f, 0f, 0.06f), new Vector3(0.12f, 0.025f, 0.025f), HiltCol);
                    EnemyArt.Part(w, "Dagger blade", new Vector3(0f, 0f, 0.2f), new Vector3(0.05f, 0.012f, 0.26f), BladeCol, true, new Vector3(90f, 0f, 0f));
                }
                foreach (var r in w.GetComponentsInChildren<Renderer>(true)) weapon.Add(r);
            }
        }

        Color Current(Renderer r)
        {
            r.GetPropertyBlock(blk);
            if (!blk.isEmpty && blk.HasColor(ColorId)) return blk.GetColor(ColorId);
            var m = r.sharedMaterial;
            return m != null && m.HasProperty(ColorId) ? m.color : Color.white;
        }

        static bool IsGlow(Color c) { float hi = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), lo = Mathf.Min(c.r, Mathf.Min(c.g, c.b)); return hi > 0.75f && hi - lo > 0.3f; }

        Color Recolor(Color c, bool isGlow)
        {
            float hi = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            Color o;
            if (def.assassin) o = isGlow ? AssassinGlow * Mathf.Max(0.75f, hi) : new Color(c.r * 0.2f + 0.05f, c.g * 0.18f + 0.04f, c.b * 0.24f + 0.08f);
            else o = isGlow ? Color.Lerp(c, def.tint, 0.7f) : Color.Lerp(c, def.tint * 0.85f, 0.55f);
            o.a = c.a; return o;
        }

        void LateUpdate()
        {
            if (def == null || rends == null) return;
            bool sneakChanged = Sneak != shownSneak; shownSneak = Sneak;
            foreach (var r in rends)
            {
                if (!r || r is ParticleSystemRenderer) continue;
                if (weapon.Contains(r)) { if (sneakChanged) r.enabled = !Sneak; continue; }
                var c = Current(r);
                if (lastOut.TryGetValue(r, out var was) && was == c) { if (sneakChanged) r.enabled = !Sneak || glow.Contains(r); continue; }   // MinionRig 這幀沒重新上色：保持
                bool g = IsGlow(c); if (g) glow.Add(r); else glow.Remove(r);
                var o = Recolor(c, g);
                blk.SetColor(ColorId, o); r.SetPropertyBlock(blk);
                lastOut[r] = o;
                r.enabled = !Sneak || g;   // 繞路中：只剩發光的眼睛
            }
        }
    }
}
