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
        sealed class CueView { public int handle; public bool cast, warned; public EnemyShotView shot; }
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
                case "esf":   // 房主的輔助效果（治療、護盾、加速、戰旗、召喚）
                    if (NetIsHost || battle == null) return true;
                    if (m.k != null && m.k.StartsWith("summon")) EnemySupportFx.Summon(WorldFrame.FromWorld(m.p)); else OnSupportFx(m.k, false);
                    return true;
                case "esx":
                    if (!NetIsHost && battle != null) battle.CancelRemoteEnemyCue(m.id);
                    return true;
            }
            return false;
        }

        bool CueMine(EnemyCue c) => c.targetPid < 0 || c.targetPid == (coop ? MyPid : 0) || (!coop && c.targetPid == 0);

        /// <summary>
        /// 每一招的畫面（Codex 的 EnemySkillFx／EnemyShotView）：預警（鎖定的位置與方向，到命中前）→ 出手特效；
        /// 投射物發射後沿線飛；讀條被打斷、施法者倒下 → 取消那一次的預警（handle）。
        /// </summary>
        void UpdateEnemyCueViews()
        {
            if (battle == null) return;
            var keep = new HashSet<int>();
            float now = battle.now;
            foreach (var c in battle.enemyCues)
            {
                if (c.cancelled) continue;
                keep.Add(c.id);
                if (!cueViews.TryGetValue(c.id, out var v))
                {
                    v = new CueView(); cueViews[c.id] = v;
                    if (now < c.hit) v.handle = EnemySkillFx.Telegraph(c.skill, WorldFrame.FromWorld(c.origin), WorldFrame.DirFromWorld(c.dir), c.hit - now, c.size);
                }
                var dirW = c.dir; dirW.y = 0f; if (dirW.sqrMagnitude < 1e-6f) dirW = Vector3.forward; dirW.Normalize();
                if (!v.cast && now >= c.hit)
                {
                    v.cast = true; v.handle = 0;   // 預警到期自己收掉
                    var owner = battle.MinionById(c.owner);
                    var from = owner != null ? WorldFrame.FromWorld(owner.Chest) : WorldFrame.FromWorld(c.origin + Vector3.up);
                    if (c.speed > 0f) v.shot = EnemyShotView.Create(c.skill);
                    else
                    {
                        var shape = (EsShape)c.shape;
                        var to = shape == EsShape.Circle || shape == EsShape.JumpRing ? c.origin : c.origin + dirW * c.size;
                        EnemySkillFx.Cast(c.skill, shape == EsShape.Circle ? WorldFrame.FromWorld(c.origin) : from, WorldFrame.FromWorld(to), c.size);
                    }
                }
                if (v.shot)
                {
                    float d = (now - c.hit) * c.speed;
                    if (d > c.size + 0.5f) { Destroy(v.shot.gameObject); v.shot = null; }
                    else v.shot.transform.SetPositionAndRotation(WorldFrame.FromWorld(c.origin + Vector3.up * 1.2f + dirW * d), Quaternion.LookRotation(WorldFrame.DirFromWorld(dirW)));
                }
                // 給被盯上的人：提示＋震動（一次）
                if (!v.warned && CueMine(c))
                {
                    v.warned = true;
                    if (c.back) { Haptics.Play(Haptics.Kind.Heavy); Sfx.Play("warn", 1f, 0.7f); }
                    else if (c.shape == (int)EsShape.JumpRing || c.shape == (int)EsShape.DuckArc) Sfx.Play("aim_warn", 0.8f);
                }
            }
            List<int> gone = null;
            foreach (var kv in cueViews)
                if (!keep.Contains(kv.Key))
                {
                    (gone ??= new List<int>()).Add(kv.Key);
                    if (!kv.Value.cast && kv.Value.handle != 0) EnemySkillFx.Cancel(kv.Value.handle);   // 讀條被打斷、施法者倒下
                    if (kv.Value.shot) Destroy(kv.Value.shot.gameObject);
                }
            if (gone != null) foreach (var id in gone) cueViews.Remove(id);
        }

        void ClearEnemyCueViews() { foreach (var v in cueViews.Values) { if (!v.cast && v.handle != 0) EnemySkillFx.Cancel(v.handle); if (v.shot) Destroy(v.shot.gameObject); } cueViews.Clear(); }

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
