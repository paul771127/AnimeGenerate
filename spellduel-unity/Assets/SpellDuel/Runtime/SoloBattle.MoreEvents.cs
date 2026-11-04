using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 更多特殊地點的畫面與輸入（邏輯在 Combat.MoreEvents.cs）：場上道具照 LabProp.kind 畫成簡單的模型、元素護盾的光環、
    /// 節奏的拍子聲、砲台的射擊閃光；運送易碎品讀手機的晃動（Input.acceleration）。
    /// </summary>
    public partial class SoloBattle
    {
        readonly Dictionary<LabProp, GameObject> labGo = new Dictionary<LabProp, GameObject>();
        readonly Dictionary<Fighter, GameObject> labAura = new Dictionary<Fighter, GameObject>();
        readonly List<(GameObject go, float until)> labFlashes = new List<(GameObject, float)>();
        Vector3 labLastAcc; bool labAccInit;

        /// <summary>UpdateRoomEventVisuals 每幀叫</summary>
        void UpdateLabVisuals()
        {
            if (battle == null) { ClearLabVisuals(); return; }
            float now = battle.now;
            // 運送易碎品：手機晃動
            if (battle.roomEvent == "fragile")
            {
                var a = Input.acceleration;
                if (labAccInit) { float j = (a - labLastAcc).magnitude; if (j > 0.22f) battle.labShake += (j - 0.22f) * 0.5f; }
                labLastAcc = a; labAccInit = true;
            }
            else labAccInit = false;
            // 道具
            foreach (var p in battle.labProps)
            {
                if (!labGo.TryGetValue(p, out var go) || !go) { go = LabModel(p); labGo[p] = go; }
                if (go.name != "lab:" + p.kind) { Destroy(go); go = LabModel(p); labGo[p] = go; }   // 砲台座變成砲台
                LabPose(p, go, now);
            }
            foreach (var kv in new List<KeyValuePair<LabProp, GameObject>>(labGo))
                if (!battle.labProps.Contains(kv.Key)) { if (kv.Value) Destroy(kv.Value); labGo.Remove(kv.Key); }
            // 元素護盾的光環
            foreach (var kv in battle.labElem)
            {
                if (!labAura.TryGetValue(kv.Key, out var ring) || !ring)
                {
                    ring = Prim(PrimitiveType.Cylinder, Battle.ElementColor(kv.Value), true);
                    ring.transform.localScale = new Vector3(1.1f, 0.01f, 1.1f); labAura[kv.Key] = ring;
                }
                ring.transform.position = WorldFrame.FromWorld(kv.Key.Feet + Vector3.up * 0.03f);
                var c = Battle.ElementColor(kv.Value); c.a = 0.45f + 0.25f * Mathf.Sin(Time.time * 5f);
                ring.GetComponent<Renderer>().material.color = c;
            }
            foreach (var kv in new List<KeyValuePair<Fighter, GameObject>>(labAura))
                if (!battle.labElem.ContainsKey(kv.Key)) { if (kv.Value) Destroy(kv.Value); labAura.Remove(kv.Key); }
            for (int i = labFlashes.Count - 1; i >= 0; i--) if (Time.time > labFlashes[i].until) { if (labFlashes[i].go) Destroy(labFlashes[i].go); labFlashes.RemoveAt(i); }
        }

        GameObject LabModel(LabProp p)
        {
            var root = new GameObject("lab:" + p.kind);
            GameObject Part(PrimitiveType t, Color c, Vector3 at, Vector3 size, bool glow = false)
            { var g = Prim(t, c, glow); g.transform.SetParent(root.transform, false); g.transform.localPosition = at; g.transform.localScale = size; return g; }
            float duckH = (StandH > 0.5f ? StandH : 1.55f) - 0.25f;
            switch (p.kind)
            {
                case "cue_low": Part(PrimitiveType.Cube, new Color(0.95f, 0.25f, 0.15f), new Vector3(0f, 0.32f, 0f), new Vector3(3.2f, 0.14f, 0.14f), true); break;
                case "cue_high": Part(PrimitiveType.Cube, new Color(0.25f, 0.5f, 1f), new Vector3(0f, duckH, 0f), new Vector3(3.2f, 0.2f, 0.2f), true); break;
                case "cue_wall": Part(PrimitiveType.Cube, new Color(1f, 0.55f, 0.15f, 0.8f), new Vector3(p.side * 0.9f, 0.95f, 0f), new Vector3(1.8f, 1.9f, 0.15f), true); break;
                case "target":
                    Part(PrimitiveType.Cylinder, new Color(0.95f, 0.95f, 0.95f), new Vector3(0f, 1.2f, 0f), new Vector3(0.7f, 0.03f, 0.7f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Part(PrimitiveType.Cylinder, new Color(0.9f, 0.15f, 0.1f), new Vector3(0f, 1.2f, -0.02f), new Vector3(0.4f, 0.035f, 0.4f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Part(PrimitiveType.Cylinder, new Color(0.4f, 0.3f, 0.2f), new Vector3(0f, 0.45f, 0f), new Vector3(0.06f, 0.45f, 0.06f));
                    break;
                case "civ":
                    Part(PrimitiveType.Capsule, new Color(0.35f, 0.85f, 0.4f), new Vector3(0f, 0.8f, 0f), new Vector3(0.45f, 0.8f, 0.45f));
                    Part(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.7f), new Vector3(0f, 1.75f, 0f), Vector3.one * 0.32f);
                    break;
                case "box": Part(PrimitiveType.Cube, new Color(0.55f, 0.38f, 0.2f), new Vector3(0f, 0.4f, 0f), Vector3.one * 0.8f); break;
                case "plate": Part(PrimitiveType.Cylinder, new Color(0.4f, 0.4f, 0.45f), new Vector3(0f, 0.02f, 0f), new Vector3(1f, 0.02f, 1f), true); break;
                case "point": Part(PrimitiveType.Cylinder, Color.white, new Vector3(0f, 0.02f, 0f), new Vector3(2.4f, 0.01f, 2.4f), true); Part(PrimitiveType.Cylinder, Color.white, new Vector3(0f, 1f, 0f), new Vector3(0.08f, 1f, 0.08f)); break;
                case "pad": Part(PrimitiveType.Cylinder, new Color(0.5f, 0.55f, 0.65f), new Vector3(0f, 0.03f, 0f), new Vector3(1.2f, 0.03f, 1.2f), true); break;
                case "turret":
                    Part(PrimitiveType.Cylinder, new Color(0.35f, 0.4f, 0.5f), new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.5f, 0.6f));
                    Part(PrimitiveType.Sphere, new Color(0.5f, 0.9f, 1f), new Vector3(0f, 1.2f, 0f), Vector3.one * 0.4f, true);
                    break;
                case "lamp":
                    Part(PrimitiveType.Cylinder, new Color(0.25f, 0.22f, 0.2f), new Vector3(0f, 0.8f, 0f), new Vector3(0.08f, 0.8f, 0.08f));
                    Part(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.4f), new Vector3(0f, 1.7f, 0f), Vector3.one * 0.35f, true);
                    break;
                case "orb": Part(PrimitiveType.Sphere, Battle.ElementColor(p.state), Vector3.zero, Vector3.one * 0.4f, true); break;
                case "jar":
                    Part(PrimitiveType.Cylinder, new Color(0.5f, 0.75f, 1f), new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.25f, 0.3f), true);
                    Part(PrimitiveType.Cylinder, new Color(1f, 1f, 1f, 0.3f), new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.01f, 1.4f), true);
                    break;
                case "goal": Part(PrimitiveType.Cylinder, new Color(0.4f, 1f, 0.5f, 0.6f), new Vector3(0f, 0.02f, 0f), new Vector3(1.8f, 0.01f, 1.8f), true); break;
            }
            return root;
        }

        void LabPose(LabProp p, GameObject go, float now)
        {
            var pos = p.pos;
            if (p.kind.StartsWith("cue_")) pos = p.pos + p.dir * 4.5f * (now - p.t0);
            go.transform.position = WorldFrame.FromWorld(new Vector3(pos.x, p.kind == "orb" ? pos.y + 0.1f * Mathf.Sin(Time.time * 2.5f) : 0f, pos.z));
            if (p.kind.StartsWith("cue_")) go.transform.rotation = Quaternion.LookRotation(WorldFrame.DirFromWorld(p.dir), Vector3.up);
            if (p.kind == "target" || p.kind == "civ") { var f = Fighter.Flat(Me.Feet - pos); if (f != Vector3.zero) go.transform.rotation = Quaternion.LookRotation(WorldFrame.DirFromWorld(f), Vector3.up); }
            if (p.kind == "jar") go.SetActive(p.state == 0);
            var rs = go.GetComponentsInChildren<Renderer>();
            switch (p.kind)
            {
                case "plate": rs[0].material.color = p.state == 1 ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.85f, 0.3f, 0.6f + 0.3f * Mathf.Sin(Time.time * 4f)); break;
                case "point": { var c = p.val >= 0f ? Color.Lerp(Color.white, new Color(0.3f, 0.7f, 1f), p.val) : Color.Lerp(Color.white, new Color(1f, 0.3f, 0.25f), -p.val); c.a = 0.55f; rs[0].material.color = c; rs[1].material.color = c; break; }
                case "lamp": rs[1].material.color = p.state == 1 ? Color.Lerp(new Color(0.4f, 0.25f, 0.1f), new Color(1f, 0.9f, 0.5f), p.val) : new Color(0.12f, 0.1f, 0.1f); break;
                case "cue_low": case "cue_high": case "cue_wall": if (p.state == 2) foreach (var r in rs) r.material.color = new Color(1f, 0.2f, 0.2f, 0.5f); else if (p.state == 1) foreach (var r in rs) r.material.color = new Color(0.5f, 1f, 0.6f, 0.4f); break;
            }
        }

        /// <summary>OnBattleEvent 叫：新的特殊地點的一次性事件（回傳 true＝處理掉了）</summary>
        bool LabBattleEvent(string kind, Vector3 at)
        {
            switch (kind)
            {
                case "evbeat": Sfx.Play("heartbeat", 0.7f); Haptics.Play(Haptics.Kind.Light); return true;
                case "labshot":
                {
                    var g = Prim(PrimitiveType.Sphere, new Color(0.5f, 0.9f, 1f), true);
                    g.transform.position = WorldFrame.FromWorld(at); g.transform.localScale = Vector3.one * 0.5f;
                    labFlashes.Add((g, Time.time + 0.15f));
                    return true;
                }
            }
            return false;
        }

        void ClearLabVisuals()
        {
            foreach (var g in labGo.Values) if (g) Destroy(g);
            labGo.Clear();
            foreach (var g in labAura.Values) if (g) Destroy(g);
            labAura.Clear();
            foreach (var f in labFlashes) if (f.go) Destroy(f.go);
            labFlashes.Clear();
        }
    }
}
