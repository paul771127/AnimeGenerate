using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 人形敵人的外觀（Combat.Variants.cs 的 151～165）：先借玩家職業的角色模型（劍士、弓手、法師、刺客、牧師），
    /// 原本小怪的骨架藏起來；預備動作、受擊、倒下、走路照轉。Codex 做好專屬造型後換掉（CODEX_PENDING_BRIEF 第 16 項）。
    /// </summary>
    public partial class SoloBattle
    {
        readonly Dictionary<Fighter, CharacterRig> humanBodies = new Dictionary<Fighter, CharacterRig>();

        CharacterRig DressHuman(Fighter m, MinionRig rig)
        {
            var v = MonsterVariants.Get(m.variant);
            if (v == null || v.human <= 0) return null;
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var body = CharacterRig.Create(MonsterVariants.HumanLook(v.human), rig.transform);
            body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
            body.InCombat = true;
            // 是敵人：腳下一圈紅環（和隊友、寵物的綠環區分）；Codex 的專屬造型做好後一樣保留
            var ring = Prim(PrimitiveType.Cylinder, new Color(1f, 0.15f, 0.12f, 0.55f), true);
            ring.name = "Enemy human ring";
            ring.transform.SetParent(rig.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f); ring.transform.localScale = new Vector3(0.9f, 0.005f, 0.9f);
            humanBodies[m] = body;
            return body;
        }
        CharacterRig HumanBody(Fighter m) => humanBodies.TryGetValue(m, out var b) && b ? b : null;
        static string HumanAttackKind(int human) => human == 1 ? "slash" : human == 2 ? "shoot" : human == 4 ? "stab" : "magic";

        /// <summary>UpdateMinionVisuals 叫：預備動作開始（windup 秒）</summary>
        void HumanWindup(Fighter m, float windup) { var b = HumanBody(m); if (b) b.PlayAttack(HumanAttackKind(m.human), Mathf.Max(0.05f, windup)); }
        void HumanHit(Fighter m) { var b = HumanBody(m); if (b) b.PlayHit(); }
        void HumanMove(Fighter m, float speed) { var b = HumanBody(m); if (b) b.MoveSpeed = speed; }
        void HumanDead(Fighter m) { if (humanBodies.TryGetValue(m, out var b)) { if (b) b.SetDead(true); humanBodies.Remove(m); } }
    }
}
