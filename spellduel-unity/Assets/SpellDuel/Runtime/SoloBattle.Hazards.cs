using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 場景陷阱的顯示與連線（判定在 Combat.Hazards.cs）：
    ///   ・大斧頭：門架＋擺動的斧刃，地上一條淡紅色的帶子標出會被砍到的那條線。
    ///   ・地刺：地上的鐵板，預警時發紅光，刺出時尖刺升起。
    ///   ・落石：從崖頂飛來的大石頭，落點有越來越紅的警示圈；落地時揚起塵土。
    /// 連線合作：主機把每個陷阱送給隊友與觀眾（haz：位置、方向、擺幅、週期、相位、開始時間＝共同時鐘），換波時送 hazclr。
    /// </summary>
    public partial class SoloBattle
    {
        class HazardView
        {
            public GameObject go, warn;
            public Transform part;     // 斧頭：擺動的部分；地刺：尖刺
            public float spikeY = -0.75f, lastSwing;
        }
        readonly Dictionary<int, HazardView> hazGo = new Dictionary<int, HazardView>();

        /// <summary>開打時：告訴戰鬥場地邊界（放陷阱、找崖頂）、副本房間的主題；主機把陷阱送給隊友</summary>
        void WireHazards()
        {
            var poly = new List<Vector3>();
            foreach (var q in area.Polygon) { var w = WorldFrame.ToWorld(q); poly.Add(new Vector3(w.x, 0f, w.z)); }
            battle.arenaPoly = poly.Count >= 3 ? poly : null;
            if (dungeon != null) battle.waveTheme = dungeon.ThemeMap();
            if (coop && NetIsHost)
            {
                battle.OnHazardAdd += h => NetSend?.Invoke(HazardMsg(h));
                battle.OnHazardsClear += () => NetSend?.Invoke(new Msg { t = "hazclr" });
            }
        }

        Msg HazardMsg(Battle.Hazard h)
        {
            double shared = SharedClock?.Invoke() ?? 0;
            return new Msg
            {
                t = "haz", id = h.id, e = (int)h.kind, p = h.a, d = h.b, s = h.amp, rad = h.radius, dmg = h.damage,
                h = h.period, c = h.land - h.start, k = h.phase.ToString("R", CultureInfo.InvariantCulture),
                t0 = shared - (battle.now - h.start),   // 開始時間換成共同時鐘
            };
        }

        void OnHazardMsg(Msg m)
        {
            float.TryParse(m.k ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture, out var phase);
            float start = battle.now - (float)((SharedClock?.Invoke() ?? m.t0) - m.t0);
            battle.AddRemoteHazard(m.id, (Battle.HazardKind)m.e, m.p, m.d, m.s, (float)m.h, phase, m.rad, start, start + (float)m.c, m.dmg);
        }

        void ClearHazardViews()
        {
            foreach (var v in hazGo.Values) { if (v.go) Destroy(v.go); if (v.warn) Destroy(v.warn); }
            hazGo.Clear();
        }

        HazardView MakeHazardView(Battle.Hazard h)
        {
            var v = new HazardView();
            switch (h.kind)
            {
                case Battle.HazardKind.Axe:
                {
                    v.go = HazardArt.Axe(Battle.AxePivotH, Battle.AxeLength, out var swing);
                    v.part = swing;
                    // 地上的危險帶：斧刃會掃過的那條線
                    v.warn = Prim(PrimitiveType.Cube, new Color(1f, 0.15f, 0.1f, 0.18f), true);
                    var mid = h.a; mid.y = 0.015f;
                    v.warn.transform.position = WorldFrame.FromWorld(mid);
                    v.warn.transform.rotation = WorldFrame.RotFromWorld(Quaternion.LookRotation(h.b, Vector3.up));
                    v.warn.transform.localScale = new Vector3(0.75f, 0.004f, h.amp * 2f + 0.9f);
                    break;
                }
                case Battle.HazardKind.Spikes:
                    v.go = HazardArt.SpikeTile(h.radius, out var spikes);
                    v.part = spikes;
                    break;
                default:
                    v.go = HazardArt.Boulder(0.45f);
                    v.warn = Prim(PrimitiveType.Cylinder, new Color(1f, 0.2f, 0.1f, 0.15f), true);
                    var land = h.b; land.y = 0.02f;
                    v.warn.transform.position = WorldFrame.FromWorld(land);
                    v.warn.transform.localScale = new Vector3(h.radius * 2f, 0.003f, h.radius * 2f);
                    break;
            }
            return v;
        }

        void UpdateHazardViews()
        {
            if (battle == null) { if (hazGo.Count > 0) ClearHazardViews(); return; }
            var live = new HashSet<int>();
            float now = battle.now, dt = Mathf.Min(Time.deltaTime, 0.1f);
            foreach (var h in battle.hazards)
            {
                if (h.kind == Battle.HazardKind.Rock && h.landed) continue;   // 落地了：特效由 rockland 事件播
                live.Add(h.id);
                if (!hazGo.TryGetValue(h.id, out var v) || !v.go) { v = MakeHazardView(h); hazGo[h.id] = v; }
                switch (h.kind)
                {
                    case Battle.HazardKind.Axe:
                    {
                        // 門架擺正：本地 +X＝擺動方向
                        v.go.transform.SetPositionAndRotation(WorldFrame.FromWorld(h.a), WorldFrame.RotFromWorld(Quaternion.LookRotation(Vector3.Cross(h.b, Vector3.up), Vector3.up)));
                        float th = Battle.AxeAngle(h, now);
                        if (v.part) v.part.localRotation = HazardArt.AxeSwingRotation(th * Mathf.Rad2Deg);
                        // 斧刃擺過中間：呼的一聲（離我近才聽得到）
                        if (Mathf.Sign(th) != Mathf.Sign(v.lastSwing) && now >= h.start)
                        {
                            float d = Battle.FlatDistance(Battle.AxeBlade(h, now), Me.Feet);
                            if (d < 6f) Sfx.Play("dodge", Mathf.Clamp01(1f - d / 6f) * 0.6f, 0.55f);
                        }
                        v.lastSwing = th;
                        if (v.warn) v.warn.GetComponent<Renderer>().material.color = new Color(1f, 0.15f, 0.1f, now >= h.start ? 0.16f + 0.08f * Mathf.Abs(Mathf.Sin(th * 3f)) : 0.08f);
                        break;
                    }
                    case Battle.HazardKind.Spikes:
                    {
                        var pos = h.a; pos.y = 0f;
                        v.go.transform.position = WorldFrame.FromWorld(pos);
                        float st = Battle.SpikeState(h, now);
                        float want = st >= 1f ? 0f : -0.75f;
                        float before = v.spikeY;
                        v.spikeY = Mathf.MoveTowards(v.spikeY, want, (want > v.spikeY ? 9f : 2.5f) * dt);   // 刺出很快、收回比較慢
                        if (v.part)
                        {
                            v.part.localPosition = new Vector3(0f, v.spikeY, 0f);
                            bool show = v.spikeY > -0.74f;   // 收到底：藏起來（半透明地板下才不會隱約看到）
                            if (v.part.gameObject.activeSelf != show) v.part.gameObject.SetActive(show);
                        }
                        HazardArt.SetSpikeWarn(v.go, st >= 1f ? 1f : st);
                        if (before < -0.5f && v.spikeY >= -0.5f)
                        {
                            float d = Battle.FlatDistance(h.a, Me.Feet);
                            if (d < 6f) Sfx.Play("trap", Mathf.Clamp01(1f - d / 6f) * 0.8f, 1.3f);
                        }
                        break;
                    }
                    default:
                    {
                        // 落石：拋物線飛過來、一邊翻滾；落點的圈越來越紅
                        v.go.transform.position = WorldFrame.FromWorld(Battle.RockPos(h, now));
                        v.go.transform.Rotate(new Vector3(260f, 90f, 40f) * dt, Space.Self);
                        float k = Mathf.Clamp01((now - h.start) / Mathf.Max(0.1f, h.land - h.start));
                        if (v.warn) v.warn.GetComponent<Renderer>().material.color = new Color(1f, 0.2f, 0.1f, 0.12f + 0.35f * k * (0.75f + 0.25f * Mathf.Sin(Time.time * 18f)));
                        break;
                    }
                }
            }
            if (hazGo.Count == live.Count) return;
            foreach (var kv in new List<KeyValuePair<int, HazardView>>(hazGo))
                if (!live.Contains(kv.Key)) { if (kv.Value.go) Destroy(kv.Value.go); if (kv.Value.warn) Destroy(kv.Value.warn); hazGo.Remove(kv.Key); }
        }
    }
}
