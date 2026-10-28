using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 劍士系列的「傷害轉護盾」（劍氣護體、劍氣凝盾）：Codex 的 SpellFx.SwordShieldGain／SwordShieldAura。
    ///   護盾變多 → Gain（劍氣聚攏，美術自己 0.3 秒節流）；其他時候每幀 Aura（fill＝護盾／最大 HP 的 40%，0 就消失）。
    ///   自己：跟著腳下的空物件；隊友：掛在隊友的角色上。只有劍士系列（點了這兩個被動）才顯示，牧師的聖血護盾不用劍氣外觀。
    /// </summary>
    public partial class SoloBattle
    {
        Transform swordShieldAnchor;
        readonly Dictionary<Fighter, int> swordShieldLast = new Dictionary<Fighter, int>();

        static bool HasSwordShield(Fighter f) => f != null && f.cls != null && f.cls.id == "swordsman" && (f.basicShield > 0f || f.hitShield > 0f);

        void UpdateSwordShieldVisuals()
        {
            if (battle == null || Me == null) { ClearSwordShieldVisuals(); return; }
            if (!swordShieldAnchor) swordShieldAnchor = new GameObject("Sword shield anchor").transform;
            swordShieldAnchor.position = WorldFrame.FromWorld(new Vector3(Me.Feet.x, 0f, Me.Feet.z));
            TickSwordShield(Me, swordShieldAnchor);
            foreach (var a in battle.allies)
                if (allyRigs.TryGetValue(a.pid, out var rig) && rig) TickSwordShield(a, rig.transform);
        }

        void TickSwordShield(Fighter f, Transform owner)
        {
            bool on = phase == Phase.Fighting && f.Alive && HasSwordShield(f);
            int sh = on && f.shieldUntil > battle.now ? f.shieldLeft : 0;
            float fill = sh > 0 ? Mathf.Clamp01(sh / Mathf.Max(1f, f.maxHp * Battle.DmgShieldCap)) : 0f;
            swordShieldLast.TryGetValue(f, out int last);
            if (sh > last && fill > 0f) SpellFx.SwordShieldGain(owner, fill);
            else SpellFx.SwordShieldAura(owner, fill);
            swordShieldLast[f] = sh;
        }

        void ClearSwordShieldVisuals()
        {
            if (swordShieldAnchor) Destroy(swordShieldAnchor.gameObject);
            swordShieldAnchor = null; swordShieldLast.Clear();
        }
    }
}
