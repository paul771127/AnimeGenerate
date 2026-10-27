using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 第 1～3 章場景機關的畫面（邏輯在 Combat.NatureHazards.cs）。目前用基本形狀＋顏色，正式美術請 Codex 做（CODEX_PENDING_BRIEF.md）。
    /// </summary>
    public partial class SoloBattle
    {
        HazardView MakeNatureView(Battle.Hazard h)
        {
            var v = new HazardView();
            var root = new GameObject("NatureHazard_" + h.kind);
            v.go = root;
            GameObject Part(PrimitiveType t, Color c, Vector3 pos, Vector3 scale, bool onTop = false)
            {
                var p = Prim(t, c, onTop); p.transform.SetParent(root.transform, false); p.transform.localPosition = pos; p.transform.localScale = scale; return p;
            }
            float d = h.radius * 2f;
            switch (h.kind)
            {
                case Battle.HazardKind.Lava:
                    v.warn = Part(PrimitiveType.Cylinder, new Color(0.25f, 0.06f, 0.02f, 0.75f), new Vector3(0f, 0.01f, 0f), new Vector3(d, 0.004f, d), true);   // 裂口（越接近噴發越紅）
                    v.part = Part(PrimitiveType.Cylinder, new Color(1f, 0.45f, 0.08f, 0.85f), new Vector3(0f, 1f, 0f), new Vector3(d * 0.75f, 1f, d * 0.75f)).transform;   // 熔岩柱
                    break;
                case Battle.HazardKind.Roots:
                    v.warn = Part(PrimitiveType.Cylinder, new Color(0.25f, 0.18f, 0.08f, 0.6f), new Vector3(0f, 0.01f, 0f), new Vector3(d, 0.004f, d), true);
                    var tuft = new GameObject("Roots"); tuft.transform.SetParent(root.transform, false); v.part = tuft.transform;
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        var r = Prim(PrimitiveType.Cylinder, new Color(0.35f, 0.25f, 0.1f)); r.transform.SetParent(tuft.transform, false);
                        r.transform.localPosition = new Vector3(Mathf.Cos(a) * h.radius * 0.5f, 0.35f, Mathf.Sin(a) * h.radius * 0.5f);
                        r.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f); r.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 20f, 0f, Mathf.Cos(a) * 20f);
                    }
                    break;
                case Battle.HazardKind.Spore:
                    v.part = Part(PrimitiveType.Sphere, new Color(0.65f, 0.4f, 0.85f, 0.35f), new Vector3(0f, 0.6f, 0f), new Vector3(d, 1.1f, d)).transform;
                    break;
                case Battle.HazardKind.Whirl:
                    v.warn = Part(PrimitiveType.Cylinder, new Color(0.2f, 0.5f, 0.9f, 0.5f), new Vector3(0f, 0.015f, 0f), new Vector3(d, 0.004f, d), true);
                    v.part = Part(PrimitiveType.Cube, new Color(0.85f, 0.95f, 1f, 0.6f), new Vector3(0f, 0.03f, 0f), new Vector3(d * 0.9f, 0.01f, 0.08f), true).transform;   // 轉動的白線
                    break;
                case Battle.HazardKind.Tide:
                    // 預警線（出發的那一邊）＋浪
                    v.warn = Prim(PrimitiveType.Cube, new Color(0.3f, 0.6f, 1f, 0.25f), true);
                    v.part = Part(PrimitiveType.Cube, new Color(0.45f, 0.75f, 1f, 0.6f), Vector3.zero, new Vector3(9f, 0.45f, h.radius * 2f)).transform;
                    break;
            }
            return v;
        }

        void UpdateNatureView(Battle.Hazard h, HazardView v, float now)
        {
            switch (h.kind)
            {
                case Battle.HazardKind.Lava:
                case Battle.HazardKind.Roots:
                {
                    v.go.transform.position = WorldFrame.FromWorld(new Vector3(h.a.x, 0f, h.a.z));
                    float st = now < h.start ? 0f : Battle.SpikeState(h, now);
                    bool up = st >= 1f;
                    if (v.part && v.part.gameObject.activeSelf != up) v.part.gameObject.SetActive(up);
                    if (v.part && up && h.kind == Battle.HazardKind.Lava) v.part.localScale = new Vector3(h.radius * 1.5f, 1f + 0.15f * Mathf.Sin(Time.time * 20f), h.radius * 1.5f);
                    if (v.warn)
                    {
                        var c = h.kind == Battle.HazardKind.Lava ? Color.Lerp(new Color(0.25f, 0.06f, 0.02f, 0.75f), new Color(1f, 0.35f, 0.05f, 0.9f), st) : Color.Lerp(new Color(0.25f, 0.18f, 0.08f, 0.6f), new Color(0.3f, 1f, 0.3f, 0.8f), st);
                        v.warn.GetComponent<Renderer>().material.color = c;
                    }
                    if (up && v.spikeY < 0.5f && Battle.FlatDistance(h.a, Me.Feet) < 6f) Sfx.Play(h.kind == Battle.HazardKind.Lava ? "boom" : "trap", 0.5f, h.kind == Battle.HazardKind.Lava ? 0.7f : 1.4f);
                    v.spikeY = up ? 1f : 0f;
                    break;
                }
                case Battle.HazardKind.Spore:
                {
                    var c = Battle.SporeCenter(h, now);
                    v.go.transform.position = WorldFrame.FromWorld(new Vector3(c.x, 0f, c.z));
                    if (v.part) v.part.localScale = new Vector3(h.radius * 2f, 1.1f, h.radius * 2f) * (1f + 0.06f * Mathf.Sin(Time.time * 2.3f));
                    break;
                }
                case Battle.HazardKind.Whirl:
                {
                    v.go.transform.position = WorldFrame.FromWorld(new Vector3(h.a.x, 0f, h.a.z));
                    float st = now < h.start ? 0f : Battle.WhirlState(h, now);
                    v.go.SetActive(st > 0f);
                    if (v.warn) v.warn.GetComponent<Renderer>().material.color = new Color(0.2f, 0.5f, 0.9f, 0.2f + 0.45f * st);
                    if (v.part) { v.part.gameObject.SetActive(st >= 1f); v.part.localRotation = Quaternion.Euler(0f, Time.time * 240f, 0f); }
                    break;
                }
                case Battle.HazardKind.Tide:
                {
                    var rot = WorldFrame.RotFromWorld(Quaternion.LookRotation(h.b, Vector3.up));
                    float warn = now < h.start ? 0f : Battle.TideWarn(h, now), front = now < h.start ? -1f : Battle.TideFront(h, now);
                    if (v.warn)
                    {
                        var p = h.a + h.b * 0.3f; p.y = 0.02f;
                        v.warn.transform.SetPositionAndRotation(WorldFrame.FromWorld(p), rot);
                        v.warn.transform.localScale = new Vector3(9f, 0.004f, 0.6f);
                        v.warn.SetActive(warn > 0f && front < 0f);
                        v.warn.GetComponent<Renderer>().material.color = new Color(0.3f, 0.6f, 1f, 0.15f + 0.4f * warn * (0.7f + 0.3f * Mathf.Sin(Time.time * 14f)));
                    }
                    if (v.part)
                    {
                        bool on = front >= 0f;
                        if (v.part.gameObject.activeSelf != on)
                        {
                            v.part.gameObject.SetActive(on);
                            if (on) { Sfx.Play("dodge", 0.7f, 0.5f); Say("🌊 浪來了！跳起來！", 1.2f); }
                        }
                        var p = h.a + h.b * Mathf.Max(0f, front); p.y = 0.22f;
                        v.go.transform.SetPositionAndRotation(WorldFrame.FromWorld(p), rot);
                    }
                    break;
                }
            }
        }
    }
}
