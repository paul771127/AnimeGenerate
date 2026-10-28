using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 補師、輔助型、人形敵人招式的畫面與連線（邏輯在 Combat.EnemySkills.cs）：
    ///   房主送 esq（一招的預警＋命中時間，共同時鐘）、esx（取消）；每支手機判定自己的玩家。
    ///   預警先用簡單的地面標示（紅＝移開、橘地環＝跳、藍弧＝蹲），Codex 的 EnemySkillFx 做好後換掉。
    ///   背後突襲：被盯上的人畫面中央「後方突襲・側步！」＋震動。
    /// </summary>
    public partial class SoloBattle
    {
        sealed class CueView { public GameObject ground, shot; public bool warned; }
        readonly Dictionary<int, CueView> cueViews = new Dictionary<int, CueView>();

        void WireEnemySkillNet()
        {
            battle.OnEnemyCue += c => { if (coop && NetIsHost) NetSend?.Invoke(new Msg { t = "esq", k = JsonUtility.ToJson(c), t0 = (SharedClock?.Invoke() ?? 0) + c.hit - battle.now }); };
            battle.OnEnemyCueCancel += c => { if (coop && NetIsHost) NetSend?.Invoke(new Msg { t = "esx", id = c.id }); };
        }

        bool EnemySkillMessage(Msg m)
        {
            switch (m.t)
            {
                case "esq":
                    if (NetIsHost || battle == null) return true;
                    { var c = JsonUtility.FromJson<EnemyCue>(m.k); if (c != null) battle.AddRemoteEnemyCue(c, battle.now + (float)(m.t0 - (SharedClock?.Invoke() ?? 0))); }
                    return true;
                case "esx":
                    if (!NetIsHost && battle != null) battle.CancelRemoteEnemyCue(m.id);
                    return true;
            }
            return false;
        }

        bool CueMine(EnemyCue c) => c.targetPid < 0 || c.targetPid == (coop ? MyPid : 0) || (!coop && c.targetPid == 0);

        void UpdateEnemyCueViews()
        {
            if (battle == null) return;
            var keep = new HashSet<int>();
            float now = battle.now;
            foreach (var c in battle.enemyCues)
            {
                if (c.judged || c.cancelled) continue;
                keep.Add(c.id);
                if (!cueViews.TryGetValue(c.id, out var v)) { v = new CueView(); cueViews[c.id] = v; }
                var shape = (EsShape)c.shape;
                var dir = WorldFrame.DirFromWorld(c.dir); dir.y = 0f; if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward; dir.Normalize();
                var at = WorldFrame.FromWorld(new Vector3(c.origin.x, 0.02f, c.origin.z));
                bool jump = shape == EsShape.JumpRing, duck = shape == EsShape.DuckArc;
                var col = jump ? new Color(1f, 0.55f, 0.15f) : duck ? new Color(0.3f, 0.65f, 1f) : new Color(1f, 0.15f, 0.1f);
                float pr = Mathf.Clamp01((now - c.warnStart) / Mathf.Max(0.1f, c.hit - c.warnStart));
                if (!v.ground)
                {
                    if (shape == EsShape.Fan || shape == EsShape.DuckArc) { v.ground = Prim(PrimitiveType.Quad, col, true); v.ground.GetComponent<MeshFilter>().sharedMesh = FanMesh(c.angle * 0.5f); }
                    else if (shape == EsShape.Circle || shape == EsShape.JumpRing) v.ground = Prim(PrimitiveType.Cylinder, col, true);
                    else v.ground = Prim(PrimitiveType.Cube, col, true);
                }
                var g = v.ground;
                bool launched = c.speed > 0f && now >= c.hit;
                g.SetActive(!launched || now < c.contact);
                if (shape == EsShape.Fan || shape == EsShape.DuckArc)
                {
                    g.transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir, Vector3.up));
                    g.transform.localScale = new Vector3(c.size, 1f, c.size);
                    if (duck) g.transform.position += Vector3.up * 1.1f;   // 蹲：胸口高度的藍弧
                }
                else if (shape == EsShape.Circle) { g.transform.position = at; g.transform.localScale = new Vector3(c.size * 2f, 0.01f, c.size * 2f); }
                else if (shape == EsShape.JumpRing)
                {
                    // 預警中：整圈淡淡的範圍；出手後：地環從中心往外擴（到腳下時跳）
                    float r = now < c.hit ? c.size : c.size * Mathf.Clamp01((now - c.hit) / EnemySkills.RingTime);
                    g.transform.position = at; g.transform.localScale = new Vector3(Mathf.Max(0.05f, r) * 2f, 0.01f, Mathf.Max(0.05f, r) * 2f);
                }
                else
                {
                    g.transform.SetPositionAndRotation(at + dir * c.size * 0.5f, Quaternion.LookRotation(dir, Vector3.up));
                    g.transform.localScale = new Vector3(c.width, 0.01f, c.size);
                }
                var mat = g.GetComponent<Renderer>().material; col.a = (0.18f + 0.4f * pr) * (0.7f + 0.3f * Mathf.Sin(Time.time * (6f + 10f * pr))); mat.color = col;
                // 投射物：發射後沿線飛
                if (launched)
                {
                    if (!v.shot) { v.shot = Prim(PrimitiveType.Sphere, new Color(1f, 0.85f, 0.6f)); v.shot.transform.localScale = Vector3.one * 0.14f; }
                    float d = Mathf.Min(c.size, (now - c.hit) * c.speed);
                    v.shot.transform.position = WorldFrame.FromWorld(c.origin + Vector3.up * 1.2f + c.dir.normalized * d);
                }
                // 給被盯上的人：提示＋震動（一次）
                if (!v.warned && CueMine(c))
                {
                    v.warned = true;
                    if (c.back) { Haptics.Play(Haptics.Kind.Heavy); Sfx.Play("warn", 1f, 0.7f); }
                    else if (jump || duck) Sfx.Play("aim_warn", 0.8f);
                }
            }
            List<int> gone = null;
            foreach (var kv in cueViews) if (!keep.Contains(kv.Key)) { (gone ??= new List<int>()).Add(kv.Key); if (kv.Value.ground) Destroy(kv.Value.ground); if (kv.Value.shot) Destroy(kv.Value.shot); }
            if (gone != null) foreach (var id in gone) cueViews.Remove(id);
        }

        void ClearEnemyCueViews() { foreach (var v in cueViews.Values) { if (v.ground) Destroy(v.ground); if (v.shot) Destroy(v.shot); } cueViews.Clear(); }

        /// <summary>畫面中央：盯上我的招式要我做什麼（後方突襲、跳、蹲）</summary>
        void DrawEnemySkillHud()
        {
            if (battle == null || phase != Phase.Fighting || Me == null || !Me.Alive) return;
            EnemyCue next = null;
            foreach (var c in battle.enemyCues)
                if (!c.judged && !c.cancelled && CueMine(c) && (c.back || c.shape == (int)EsShape.JumpRing || c.shape == (int)EsShape.DuckArc) && (next == null || c.hit < next.hit)) next = c;
            if (next == null) return;
            string text = next.back ? "⚠ 後方突襲・側步！" : next.shape == (int)EsShape.JumpRing ? "⬆ 地波：到腳下時跳！" : "⬇ 橫掃：蹲下！";
            var col = next.back ? new Color(0.95f, 0.25f, 0.35f) : next.shape == (int)EsShape.JumpRing ? new Color(1f, 0.55f, 0.15f) : new Color(0.35f, 0.7f, 1f);
            UiKit.Pill(Screen.height * 0.55f, text, Mathf.RoundToInt(UiKit.BaseFont * 1.4f), col, Screen.width * 0.9f, 0.6f);
        }
    }
}
