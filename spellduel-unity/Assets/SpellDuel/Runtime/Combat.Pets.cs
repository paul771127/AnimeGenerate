using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 寵物系統的戰鬥部分（資料在 Pets.cs）：
    ///   誘餌：ThrowBait → 那一種怪被吸引去吃 3 秒（不攻擊）；吃餌期間第一次 HP 低於 30% 時擲收服（一份餌只擲一次）。
    ///     成功：從敵人移走（不算擊倒、沒有經驗和掉寶）→ OnPetCaught；失敗：掙脫。
    ///   出戰：SpawnPet → 友方單位（沿用召喚師收服魔物的 tamedKind／localFriend），能力照寵物等級，會自動放技能；倒下這一場就不能再出。
    /// </summary>
    public partial class Battle
    {
        /// <summary>玩家的幸運（收服機率用；SoloBattle 設定）</summary>
        public int captureLuck;
        public event Action<int, Vector3> OnPetCaught;   // 收服成功（種類、位置）

        /// <summary>這種怪現在能不能被誘餌吸引（場上正常生成的那一種，不是變種、召喚物、寶箱、小 Boss）</summary>
        static bool Lurable(Fighter m, int kind) => m.Alive && m.minionKind == kind && m.variant == 0 && !m.summoned && !m.treasure && !m.miniBoss && !m.hidden && m.lureUntil <= 0f && Pets.Capturable(kind);

        /// <summary>場上有沒有這種怪可以丟餌</summary>
        public bool CanLure(int kind) { if (remoteEnemy) return false; foreach (var m in minions) if (Lurable(m, kind)) return true; return false; }

        /// <summary>丟誘餌：最近的那一隻被吸引過去吃 3 秒；沒有對象回傳 false（不扣餌）</summary>
        public bool ThrowBait(int kind)
        {
            if (remoteEnemy || !player.Alive) return false;
            Fighter best = null; float bd = float.MaxValue;
            foreach (var m in minions) { if (!Lurable(m, kind)) continue; float d = FlatDistance(m.Feet, player.Feet); if (d < bd) { bd = d; best = m; } }
            if (best == null) return false;
            var toMe = Fighter.Flat(player.Feet - best.Feet);
            best.lureAt = best.Feet + toMe * Mathf.Min(0.8f, bd * 0.3f);
            best.lureUntil = now + Pets.LureTime; best.lureRolled = false;
            best.windupUntil = 0f; best.attackReadyAt = Mathf.Max(best.attackReadyAt, now + Pets.LureTime);
            Emit("bait", best.lureAt, new Color(1f, 0.85f, 0.5f), $"🍖 {best.name}被誘餌吸引了！趁牠吃的時候把血壓到 30% 以下");
            if (best.hp < best.maxHp * Pets.CaptureHp) TryCapture(best);   // 本來就殘血：開始吃就擲
            return true;
        }

        /// <summary>吃餌中：往誘餌走、不攻擊；時間到沒收服就算了（UpdateMinions 呼叫，回傳 true＝這一幀不做別的事）</summary>
        bool UpdateLure(Fighter m, float dt)
        {
            if (m.lureUntil <= 0f) return false;
            if (now >= m.lureUntil)
            {
                m.lureUntil = 0f;
                if (!m.lureRolled) Emit("lurefail", m.Chest, new Color(0.8f, 0.8f, 0.8f), $"{m.name}吃完誘餌走了（沒有收服）");
                return false;
            }
            m.windupUntil = 0f;
            var to = Fighter.Flat(m.lureAt - m.Feet);
            float d = FlatDistance(m.lureAt, m.Feet);
            if (d > 0.1f) { var step = to * Mathf.Min(d, 1.0f * dt); m.head += step; m.forward = to; }
            return true;
        }

        /// <summary>收服判定（一份餌只擲一次）；成功回傳 true（這隻已經離開戰場）</summary>
        bool TryCapture(Fighter m)
        {
            if (m.lureRolled) return false;
            m.lureRolled = true;
            var sp = Pets.Get(m.minionKind);
            float p = Pets.CaptureChance(m.hp / Mathf.Max(1f, m.maxHp), captureLuck, sp != null && sp.elite);
            if (rng.NextDouble() < p)
            {
                m.capturedPet = true; m.hp = 0f; m.lureUntil = 0f;   // 不算擊倒：沒有經驗、掉寶（Damage 收到就直接返回）
                Emit("petcaught", m.Feet, new Color(0.45f, 1f, 0.8f), $"🐾 收服了{m.name}！下一場可以出戰");
                OnPetCaught?.Invoke(m.minionKind, m.Feet);
                return true;
            }
            m.lureUntil = 0f;
            Emit("lurefail", m.Chest, new Color(1f, 0.6f, 0.5f), $"{m.name}掙脫了！（收服機率 {p * 100f:0}%）");
            return false;
        }

        // ------------------------------------------------------------ 出戰
        /// <summary>這一場出戰的寵物（友方單位）；倒下後還是指著牠（Alive＝false）</summary>
        public Fighter petBody;

        public Fighter SpawnPet(PetData d, int ownerLevel)
        {
            var s = d.Species; if (s == null || petBody != null) return null;
            int lv = Pets.EffectiveLevel(d, ownerLevel);
            int hp = Pets.MaxHp(s, lv);
            var r = Fighter.Flat(player.forward); if (r == Vector3.zero) r = Vector3.forward;
            var at = player.Feet + new Vector3(r.z, 0f, -r.x) * 0.9f - r * 0.3f;
            var n = SpawnNpc("pet_" + d.uid, $"🐾 {d.Name}", "priest", 0, false, hp, 0, at);
            n.maxHp = hp; n.hp = hp; n.dmgMul = 1f;
            n.tamedKind = s.kind; n.localFriend = true;
            n.petUid = d.uid; n.petAtk = Pets.Atk(s, lv); n.petGap = Pets.AttackGap(s); n.petLevel = lv;
            n.petSkills = new List<string>(d.skills);
            n.bodyRadius = MinionRadius[Mathf.Clamp(s.kind, 0, MinionRadius.Length - 1)];
            n.head = new Vector3(at.x, MinionHead[Mathf.Clamp(s.kind, 0, MinionHead.Length - 1)] + Mathf.Min(BaseAlt(s.kind), 1.2f), at.z);
            petBody = n;
            Emit("petout", n.Feet, new Color(0.45f, 1f, 0.8f), $"🐾 {d.Name}（Lv{lv}）出戰！");
            return n;
        }

        /// <summary>寵物的技能（UpdateNpcs 每幀呼叫）：生存／護主 → 控制 → 輸出；共用 1 秒動作鎖</summary>
        void PetSkillTick(Fighter n, Fighter tgt)
        {
            if (n.petSkills == null || n.petSkills.Count == 0 || now < n.petActLock) return;
            bool targeted = false; foreach (var m in minions) if (m.Alive && m.target == n) { targeted = true; break; }
            PetSkill pick = null; int bestPri = -1;
            foreach (var id in n.petSkills)
            {
                var k = Pets.Skill(id); if (k == null) continue;
                if (n.petCd.TryGetValue(id, out var ready) && now < ready) continue;
                int pri = -1;
                switch (k.fx)
                {
                    case PetFx.SelfShield: if (n.hp < n.maxHp * 0.7f) pri = 3; break;
                    case PetFx.SelfReduce: if (targeted) pri = 3; break;
                    case PetFx.Heal: if (n.hp < n.maxHp * 0.7f || (player.Alive && FlatDistance(player.Feet, n.Feet) <= 3f && player.hp < player.maxHp * 0.7f)) pri = 3; break;
                    case PetFx.OwnerShield: if (player.Alive && FlatDistance(player.Feet, n.Feet) <= k.range && player.hp < player.maxHp * 0.7f) pri = 3; break;
                    case PetFx.Snare: case PetFx.Slow: if (tgt != null && FlatDistance(tgt.Feet, n.Feet) <= k.range + tgt.bodyRadius) pri = 2; break;
                    case PetFx.SelfAoe: { int c = 0; foreach (var m in minions) if (m.Alive && FlatDistance(m.Feet, n.Feet) <= k.range + m.bodyRadius) c++; if (c > 0) pri = 1; break; }
                    default: if (tgt != null && FlatDistance(tgt.Feet, n.Feet) <= Mathf.Max(k.range, 1.2f) + tgt.bodyRadius) pri = 1; break;
                }
                if (pri > bestPri) { bestPri = pri; pick = k; }
            }
            if (pick == null || bestPri < 0) return;
            n.petCd[pick.id] = now + pick.cd; n.petActLock = now + 1f;
            CastPetSkill(n, pick, tgt);
        }

        void CastPetSkill(Fighter n, PetSkill k, Fighter tgt)
        {
            int A = Mathf.Max(1, n.petAtk);
            var sk = Skills.All["pet_tamed"];
            var col = new Color(0.45f, 1f, 0.8f);
            void Hit(Fighter v, float mul, EffectKind eff = EffectKind.None, float dps = 0f, float dur = 0f)
            {
                if (v == null || !v.Alive) return;
                HitFoe(player, v, sk, Mathf.Max(1, Mathf.RoundToInt(A * mul)), eff, dps, dur);
            }
            Emit("petskill", n.Chest, col, $"🐾 {k.name}");
            switch (k.fx)
            {
                case PetFx.Melee: case PetFx.Ranged: Hit(tgt, k.mul); break;
                case PetFx.Burn: Hit(tgt, k.mul, EffectKind.Burn, A * k.extra, k.dur); break;
                case PetFx.Double: Hit(tgt, k.mul); timers.Add(new KeyValuePair<float, Action>(now + 0.3f, () => Hit(tgt, k.mul))); break;
                case PetFx.SelfAoe: foreach (var m in new List<Fighter>(minions)) if (m.Alive && FlatDistance(m.Feet, n.Feet) <= k.range + m.bodyRadius) Hit(m, k.mul); break;
                case PetFx.Snare: Hit(tgt, k.mul, EffectKind.Snare, 0f, CcImmune(tgt) ? k.extra * 0.5f : k.extra); break;
                case PetFx.Slow: Hit(tgt, k.mul); SlowFoe(tgt, k.extra, k.dur); break;
                case PetFx.Dash:
                    if (tgt != null) { var to = Fighter.Flat(tgt.Feet - n.Feet); float d = FlatDistance(tgt.Feet, n.Feet); var p = n.Feet + to * Mathf.Max(0f, d - tgt.bodyRadius - 0.5f); n.head = new Vector3(p.x, n.head.y, p.z); }
                    Hit(tgt, k.mul); if (k.extra > 0f) SlowFoe(tgt, k.extra, k.dur); break;
                case PetFx.Leech: Hit(tgt, k.mul); n.hp = Mathf.Min(n.maxHp, n.hp + A * k.extra); break;
                case PetFx.Line:
                    if (tgt != null)
                    {
                        var a = n.Feet; var b = a + Fighter.Flat(tgt.Feet - a) * Mathf.Max(k.range, FlatDistance(tgt.Feet, a));
                        foreach (var m in new List<Fighter>(minions)) if (m.Alive && DistancePointSegment(m.Feet, a, b) <= k.extra * 0.5f + m.bodyRadius) Hit(m, k.mul);
                    }
                    break;
                case PetFx.ZoneDot:
                    if (tgt != null)
                    {
                        var c = tgt.Feet; float r = k.extra > 0f ? k.extra : 0.8f;
                        if (k.dur > 0f) for (int i = 0; i < Mathf.RoundToInt(k.dur); i++) timers.Add(new KeyValuePair<float, Action>(now + i + 0.5f, () => { foreach (var m in new List<Fighter>(minions)) if (m.Alive && FlatDistance(m.Feet, c) <= r + m.bodyRadius) Hit(m, k.mul); }));
                        else { foreach (var m in new List<Fighter>(minions)) if (m.Alive && FlatDistance(m.Feet, c) <= r + m.bodyRadius) { Hit(m, k.mul); SlowFoe(m, 0.25f, 2f); } }
                    }
                    break;
                case PetFx.SelfShield: n.shieldUntil = now + k.dur; n.shieldLeft = Mathf.RoundToInt(n.maxHp * k.mul); Emit("buff", n.Chest, col, $"{k.name}（護盾 {n.shieldLeft}）"); break;
                case PetFx.SelfReduce: n.armorUntil = now + k.dur; n.armorReduce = k.mul; Emit("buff", n.Chest, col, $"{k.name}：減傷 {k.mul * 100f:0}%"); break;
                case PetFx.Heal:
                {
                    var who = player.Alive && FlatDistance(player.Feet, n.Feet) <= 3f && player.hp / player.maxHp < n.hp / n.maxHp ? player : n;
                    float amt = A * k.mul; who.hp = Mathf.Min(who.maxHp, who.hp + amt);
                    Emit("heal", who.Chest, col, $"{k.name} +{amt:0}");
                    break;
                }
                case PetFx.OwnerShield:
                    player.shieldUntil = now + k.dur; player.shieldLeft = Mathf.Max(player.shieldLeft, Mathf.RoundToInt(n.maxHp * k.mul));
                    Emit("buff", player.Chest, col, $"{k.name}（護盾 {player.shieldLeft}）"); break;
            }
        }

        /// <summary>減速：速度乘上 (1−frac)，dur 秒後還原（Boss 不吃控制的話減半）</summary>
        void SlowFoe(Fighter v, float frac, float dur)
        {
            if (v == null || !v.Alive || frac <= 0f) return;
            if (CcImmune(v)) dur *= 0.5f;
            float k = 1f - frac; v.speedMul *= k;
            timers.Add(new KeyValuePair<float, Action>(now + dur, () => v.speedMul /= k));
        }
    }
}
