using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 補師、召喚師、砂靈、輔助型、人形敵人（121～124、141～144、151～165）的外觀：Codex 的 VariantRig（全新骨架、敵方紅環）。
    /// 原本小怪的骨架藏起來，位置、面向照舊由 MinionRig 帶；預備／出手用招式 id（讀條中的招、隊友端從預警找、補師輔助的讀條）。
    /// 輔助效果（治療綠光、泡泡護盾、加速、戰旗、召喚）：房主的 esfx 事件 → EnemySupportFx，合作時送給隊友。
    /// </summary>
    public partial class SoloBattle
    {
        readonly Dictionary<Fighter, VariantRig> variantBodies = new Dictionary<Fighter, VariantRig>();
        readonly Dictionary<Fighter, string> variantMove = new Dictionary<Fighter, string>();
        readonly Dictionary<Fighter, float> variantWindEnd = new Dictionary<Fighter, float>();
        readonly Dictionary<Fighter, int> variantShield = new Dictionary<Fighter, int>();

        static bool HasVariantArt(int v) => v >= 121 && v <= 124 || v >= 141 && v <= 144 || v >= 151 && v <= 165;
        /// <summary>補師、輔助型、召喚師原本的讀條（提案的招式 id）</summary>
        static string CoreMove(int v) => v switch
        {
            121 => "eh_121_sunmend", 122 => "eh_122_herbmend", 123 => "eh_123_twinseals", 141 => "eh_141_rainchorus", 142 => "eh_142_pearlguard",
            143 => "eh_143_marchdrum", 144 => "eh_144_royalstandard", 156 => "eh_156_seedmend", 162 => "eh_162_sanctuarymend", _ => null,
        };

        VariantRig DressVariant(Fighter m, MinionRig rig)
        {
            if (!HasVariantArt(m.variant)) return null;
            foreach (var r in rig.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            var body = VariantRig.Create(m.variant);
            body.transform.SetParent(rig.transform, false);
            body.transform.localPosition = Vector3.zero; body.transform.localRotation = Quaternion.identity;
            variantBodies[m] = body;
            return body;
        }
        VariantRig VariantBody(Fighter m) => variantBodies.TryGetValue(m, out var b) && b ? b : null;

        /// <summary>這一下預備的是哪一招：讀條中的招（房主）→ 這隻的預警（隊友端）→ 補師輔助的讀條 → basic</summary>
        string MoveOf(Fighter m)
        {
            if (m.esCast != null) return m.esCast.id;
            if (battle != null) for (int i = battle.enemyCues.Count - 1; i >= 0; i--) { var c = battle.enemyCues[i]; if (c.owner == m.minionId && !c.judged && battle.now < c.hit + 0.1f) return c.skill; }
            return CoreMove(m.variant) ?? "basic";
        }

        /// <summary>UpdateMinionVisuals 叫：位置跟好之後的每幀狀態</summary>
        void TickVariantBody(Fighter m, MinionRig rig)
        {
            var b = VariantBody(m); if (!b) return;
            b.MoveSpeed = rig.MoveSpeed;
            b.SetFleeing(m.humState == 1);
            bool sneak = m.human == 4 && m.esCast == null && m.humState == 0 && Me != null && Battle.FlatDistance(m.Feet, Me.Feet) > 2.2f;
            b.SetSneak(sneak);
            // 護盾被打破（房主）：碎片
            int sh = m.shieldUntil > battle.now ? m.shieldLeft : 0;
            if (variantShield.TryGetValue(m, out var was) && was > 0 && sh <= 0 && m.shieldUntil > battle.now) EnemySupportFx.ShieldBreak(rig.transform);
            variantShield[m] = sh;
        }
        void VariantWindupStart(Fighter m, float windup)
        {
            var b = VariantBody(m); if (!b) return;
            string move = MoveOf(m);
            variantMove[m] = move; variantWindEnd[m] = m.windupUntil;
            b.Windup(move, Mathf.Max(0.05f, windup));
            if (move == CoreMove(m.variant)) b.SetChannel(true);   // 補師、輔助型的讀條：符印、舉杖
        }
        void VariantWindupEnd(Fighter m)
        {
            var b = VariantBody(m); if (!b) return;
            variantMove.TryGetValue(m, out var move);
            bool broken = variantWindEnd.TryGetValue(m, out var end) && battle.now < end - 0.08f;   // 還沒到時間就結束＝被打斷
            if (broken) b.ChannelBroken(); else b.Strike(move ?? "basic");
            b.SetChannel(false);
        }
        void VariantHit(Fighter m) { var b = VariantBody(m); if (b) b.PlayHit(); }
        void VariantDead(Fighter m)
        {
            if (variantBodies.TryGetValue(m, out var b)) { if (b) b.SetDead(true); variantBodies.Remove(m); }
            variantMove.Remove(m); variantWindEnd.Remove(m); variantShield.Remove(m);
        }
        void ClearVariantBodies() { variantBodies.Clear(); variantMove.Clear(); variantWindEnd.Clear(); variantShield.Clear(); }

        // ---------------------------------------------------------------- 輔助效果
        /// <summary>esfx 事件（「種類|施法者id|目標id|秒數」）：房主自己畫，合作時送給隊友</summary>
        void OnSupportFx(string text, bool relay)
        {
            if (string.IsNullOrEmpty(text) || battle == null) return;
            if (relay && coop && NetIsHost) NetSend?.Invoke(new Msg { t = "esf", k = text });
            var a = text.Split('|'); if (a.Length < 4) return;
            int.TryParse(a[1], out var cid); int.TryParse(a[2], out var tid);
            float.TryParse(a[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var secs);
            var caster = battle.MinionById(cid); var target = battle.MinionById(tid);
            Transform T(Fighter f) => f != null && minionGo.TryGetValue(f, out var r) && r ? r.transform : null;
            Vector3 P(Fighter f) => f != null ? WorldFrame.FromWorld(f.Chest) : Vector3.zero;
            switch (a[0])
            {
                case "heal":
                    if (caster != null && target != null) EnemySupportFx.Heal(P(caster), P(target));
                    // 使用者回報「看不出敵方補師有幫敵人補血」：補到的那一下（第 5 段＝補了多少）在被補的敵人身上爆一圈綠光；隊友端也跳大字
                    if (a.Length >= 5 && target != null && float.TryParse(a[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var amt) && amt > 0f)
                    {
                        SpellFx.Impact("priest", new Color(0.35f, 1f, 0.45f), P(target), 1.6f);
                        if (caster != null) SpellFx.Impact("priest", new Color(0.35f, 1f, 0.45f), P(caster), 0.8f);
                        if (!relay) floaters.Add(new Floater { posMap = target.Chest + Vector3.up * 0.4f, text = $"💚 補血 +{amt:0}", color = new Color(0.4f, 1f, 0.5f), born = Time.time });
                    }
                    break;
                case "shield": { var t = T(target); if (t) EnemySupportFx.Shield(t, secs); break; }
                case "haste": { var t = T(target); if (t) EnemySupportFx.Haste(t, secs); break; }
                case "aura": { var t = T(target); if (t) EnemySupportFx.RallyAura(t, secs); break; }
                case "rally": if (caster != null) EnemySupportFx.Rally(WorldFrame.FromWorld(caster.Feet), secs); break;
            }
        }
    }
}
