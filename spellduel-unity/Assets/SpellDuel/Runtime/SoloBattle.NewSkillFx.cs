using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// Codex 交的新技能特效接線（CODEX_TASK_NEW_SKILL_FX.md）：
    ///   增益的施放／持續（TeamBuffCast／TeamBuffAura）、流派被動（連鎖、反傷、斬殺、先發、追獵、吸魂、連擊層數）、獵鷹俯衝、
    ///   新技能的命中與範圍特效（SkillImpact、ArcaneBreakHit、WyrmPierce、AreaBurst、StarfallZone）。只有畫面，不改戰鬥。
    ///   位置一律先用 WorldFrame.FromWorld 換成場景座標；自己的特效掛在腳下的空物件，隊友掛在隊友的角色上。
    /// </summary>
    public partial class SoloBattle
    {
        Transform fxAnchor;
        Battle fxBattle;
        /// <summary>美術有做專屬增益外觀的技能（其他增益不叫 TeamBuff 介面）</summary>
        static readonly HashSet<string> BuffArtIds = new HashSet<string>
        {
            "arcanehaste", "elementward", "rangerhaste", "hawkeye", "layhands", "pactcheer", "nightveil", "divinegrace",
            "mindfocus", "meditate", "bless", "haste", "spring", "focus",
            "retribution", "swordfervor", "killinstinct", "huntfrenzy", "chainstorm", "soulharvest", "renjutsu", "reaperdance", "graceflow", "martyrflame",
        };
        static readonly HashSet<string> ImpactIds = new HashSet<string> { "headshot", "poisondart", "verdict" };

        Transform FxOwner(Fighter f)
        {
            if (f == null || battle == null) return null;
            if (f == Me) return fxAnchor;
            if (f.isAlly && allyRigs.TryGetValue(f.pid, out var rig) && rig) return rig.transform;
            if (f.isMinion && minionGo.TryGetValue(f, out var mr) && mr) return mr.transform;
            return null;
        }

        void UpdateNewSkillFx()
        {
            if (!fxAnchor) fxAnchor = new GameObject("New skill fx anchor").transform;
            fxAnchor.position = WorldFrame.FromWorld(new Vector3(Me.Feet.x, 0f, Me.Feet.z));
            if (fxBattle != battle)
            {
                UnhookNewSkillFx();
                fxBattle = battle;
                battle.OnStyleFx += OnStyleFx; battle.OnFalconDive += OnFalconDive;
            }
            // 增益：第一次出現播施放特效，之後每幀更新剩餘比例；到期送 0 收掉
            for (int i = Me.buffFx.Count - 1; i >= 0; i--)
            {
                var b = Me.buffFx[i];
                if (!BuffArtIds.Contains(b.id)) { Me.buffFx.RemoveAt(i); continue; }
                if (!b.shown) { b.shown = true; SpellFx.TeamBuffCast(b.id, fxAnchor); }
                float remain = Mathf.Clamp01((b.until - battle.now) / Mathf.Max(0.1f, b.until - b.start));
                bool on = phase == Phase.Fighting && Me.Alive && remain > 0f;
                SpellFx.TeamBuffAura(fxAnchor, b.id, on ? remain : 0f);
                if (!on) Me.buffFx.RemoveAt(i);
            }
            // 連擊疊層：自己的層數
            SpellFx.StackAura(fxAnchor, phase == Phase.Fighting && Me.stackUntil > battle.now ? Me.stackCount : 0);
        }

        void OnFalconDive(Fighter o, Fighter t, float sec) => SpellFx.FalconDive(WorldFrame.FromWorld(o.head + Vector3.up * 0.4f), WorldFrame.FromWorld(t.Chest), sec);
        void UnhookNewSkillFx() { if (fxBattle != null) { fxBattle.OnStyleFx -= OnStyleFx; fxBattle.OnFalconDive -= OnFalconDive; } fxBattle = null; }

        void OnStyleFx(string kind, Fighter a, Fighter b, Vector3 at)
        {
            if (battle == null) return;
            switch (kind)
            {
                case "chain": if (a != null && b != null) SpellFx.ChainArc(WorldFrame.FromWorld(a.Chest), WorldFrame.FromWorld(b.Chest), new Color(0.75f, 0.85f, 1f)); break;
                case "thorns": { var v = FxOwner(a); if (v != null && b != null) SpellFx.Thorns(v, WorldFrame.FromWorld(b.Chest)); break; }
                case "execute": SpellFx.ExecuteHit(WorldFrame.FromWorld(at)); break;
                case "first": SpellFx.FirstStrikeHit(WorldFrame.FromWorld(at)); break;
                case "killrush": { var o = FxOwner(a); if (o != null) SpellFx.KillRush(o); break; }
                case "soul": { var o = FxOwner(a); if (o != null) SpellFx.SoulDrain(WorldFrame.FromWorld(at), o); break; }
            }
        }

        /// <summary>新技能的命中特效（OnBattleEvent 的 hit／trap 呼叫）</summary>
        void NewSkillHitFx(SkillDef hs, Vector3 at)
        {
            if (hs == null) return;
            var p = WorldFrame.FromWorld(at);
            if (hs.id == "arcanewyrm") SpellFx.WyrmPierce(p);
            else if (hs.id == "arcanebreak") SpellFx.ArcaneBreakHit(p);
            else if (ImpactIds.Contains(hs.id)) SpellFx.SkillImpact(hs.id, p);
        }

        /// <summary>新技能的範圍特效；不是新技能回 false（照舊用 AdvancedFx）</summary>
        bool NewSkillAreaFx(string id, Vector3 at, float radius, bool first)
        {
            if (string.IsNullOrEmpty(id) || !AreaSkillArt.Known(id)) return false;
            var c = WorldFrame.FromWorld(new Vector3(at.x, 0f, at.z));
            if (first && id == "starfall" && Skills.All.TryGetValue(id, out var s)) SpellFx.StarfallZone(c, radius, Mathf.Max(1, s.hits) * s.hitGap + 0.4f);   // 隕星：地上的星陣維持到最後一波
            SpellFx.AreaBurst(id, c, radius);
            return true;
        }

        void ClearNewSkillFx()
        {
            if (fxAnchor) Destroy(fxAnchor.gameObject);
            fxAnchor = null; UnhookNewSkillFx();
        }
    }
}
