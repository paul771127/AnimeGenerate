using System.Text;

namespace SpellDuel
{
    /// <summary>
    /// 技能的詳細說明（選技能畫面、戰鬥中點技能卡時顯示）：
    /// 全部由技能數值自動產生，數值改了說明就跟著變。純 C#，可在 Unity 外測試。
    /// </summary>
    public static class SkillInfo
    {
        public static string TypeName(SkillDef s) => s.cls == "basic" ? (Skills.IsMeleeBasic(s) ? "普通攻擊・近身" : "普通攻擊・遠程") : s.type switch
        {
            SkillType.Trap => "陷阱",
            SkillType.Self => s.self switch
            {
                SelfKind.Heal or SelfKind.Cleanse or SelfKind.Mana => "回復",
                SelfKind.Block or SelfKind.Shield or SelfKind.Counter or SelfKind.Armor or SelfKind.Taunt => "防禦",
                SelfKind.Dispel => "控制",
                _ => "增益",
            },
            _ => s.areaRadius > 0f ? "範圍攻擊" : s.fromAbove ? "落下攻擊" : s.releaseNear ? "近身伏擊" : "投射攻擊",
        };

        /// <summary>一行摘要：MP・蓄力・冷卻</summary>
        public static string CostLine(SkillDef s) => s.cls == "basic" ? $"不花 MP　不用唸　每 {s.cooldown:0.#} 秒一次" :
            $"MP {s.cost}　{(s.charge > 0f ? $"蓄力 {s.charge:0.#} 秒" : "免蓄力")}　冷卻 {s.cooldown:0.#} 秒";

        /// <summary>普通攻擊的手勢</summary>
        public static string BasicGesture(SkillDef s) => s?.id switch
        {
            "na_mage" => "比 1（只伸食指）往前一點", "na_arrow" => "握拳（拉弦）再張開（放箭）", "na_pebble" => "比 5（張開手掌）", "na_holy" => "比 2（食指＋中指）往上一舉",
            "na_dagger" => "手往前突刺（手遠離手機）", "na_sword" => "手掌在鏡頭前橫劃一下", "na_stick" => "手掌在鏡頭前橫劃一下", _ => "",
        };

        static void BasicDetail(SkillDef s, StringBuilder sb)
        {
            void Line(string t) => sb.Append("・").Append(t).Append('\n');
            bool melee = Skills.IsMeleeBasic(s);
            sb.Append("【怎麼用】\n");
            Line("普通攻擊：不用唸技能名稱、不花 MP，也不佔技能欄");
            if (Skills.IsChargedBasic(s)) Line($"畫面上的手掌集氣完成（{(s.id == "na_sword" ? "泛起藍白劍光" : s.id == "na_dagger" ? "凝聚紅光" : s.id == "na_mage" ? "浮現紫色法陣" : s.id == "na_arrow" ? "弓拉滿" : s.id == "na_holy" ? "聖光環亮起" : "變紅")}）後，{BasicGesture(s)}就出手；出手後重新集氣（約 {s.cooldown:0.#} 秒，敏捷越高越快）");
            else Line(Skills.IsSwipeBasic(s) ? $"{BasicGesture(s)}就出手（每 {s.cooldown:0.#} 秒最多一次）" : $"手{BasicGesture(s)}並維持，每 {s.cooldown:0.#} 秒自動攻擊一次（放下手勢就停）");
            Line("正在詠唱技能時不會普攻（唸完、放完招才會繼續）");
            if (!melee) Line($"朝畫面中央的準星方向飛出；先出現瞄準預警線，約 {Battle.ShotWindup:0.##} 秒後才射出（對手也看得到）");
            if (Skills.IsSwipeBasic(s)) Line("只砍到手掌劃過的那一條線：手要劃過畫面上的敵人才打得到（一刀可以劃過好幾隻）");
            if (s.id == "na_stick") Line("不會砍掉飛來的法球（劍士的「斬擊」技能才砍得掉）");
            if (s.id == "na_sword") Line($"劃完先出現紅色預警線，約 {Battle.SlashWindup:0.##} 秒後劍光沿線閃過砍下（對手也看得到，會躲）");
            if (s.id == "na_dagger") Line($"往準星方向刺：準星左右約 {Battle.StabHalfAngle:0}° 內、{s.rangeMax:0.#}m 內的敵人都刺得到；刺出時眼前一道紅光");
            sb.Append("\n【數值】\n");
            Line($"傷害 {s.damage}（會吃攻擊力加成{(melee ? "" : "，也會爆擊")}）");
            Line($"冷卻 {s.cooldown:0.#} 秒");
            if (melee) Line($"範圍：面向的前方半球，半徑 {s.rangeMax:0.#}m（背後打不到）");
            else
            {
                Line($"射程 {s.rangeMax:0.#}m；飛行速度 {s.speed:0.#} m/s");
                Line("飛超過射程會淡出消失");
            }
            sb.Append("\n【對手怎麼躲】\n");
            Line(melee ? "退到 1m 外，或繞到它背後" : "看準飛來的方向移開身體（側跨沒有無敵時間）");
        }

        /// <summary>完整說明（多行）</summary>
        public static string Detail(SkillDef s)
        {
            var sb = new StringBuilder();
            void Line(string t) => sb.Append("・").Append(t).Append('\n');

            sb.Append(s.desc).Append("\n\n");
            if (s.cls == "basic") { BasicDetail(s, sb); return sb.ToString().TrimEnd('\n'); }
            var adv = Advanced.Get(s.adv);
            if (adv != null) sb.Append($"✨ {adv.name}的進階技能（技能欄的第 4 格）\n\n");

            // ---- 怎麼放
            sb.Append("【怎麼放】\n");
            Line($"唸出「{s.name}」開始詠唱{(s.charge > 0f ? $"，蓄力 {s.charge:0.#} 秒" : "（不用蓄力）")}");
            Line(HandGesture.ReleaseHint(s));
            if (s.type == SkillType.Projectile && !s.fromAbove && s.areaRadius <= 0f && !s.releaseNear) Line("朝畫面中央的準星方向飛出；沒鎖定敵人也能放（不一定打中）");
            if (s.self == SelfKind.Cleanse) Line("被定身時也能唸");
            if (s.stillRadius > 0f) Line($"蓄力期間要站著不動：離開原地超過直徑 {s.stillRadius * 2f:0.#}m 就失敗；蓄滿後可以自由移動");

            // ---- 數值
            sb.Append("\n【數值】\n");
            Line(CostLine(s));
            if (s.damage > 0) Line(s.multi > 1 ? $"傷害 {s.damage} × {s.multi} 發" : $"傷害 {s.damage}");
            switch (s.type)
            {
                case SkillType.Projectile when s.areaRadius > 0f:
                    Line(s.atTarget ? $"打在準星指的位置（最遠 {s.rangeMax:0.#}m；鎖定時打在敵人腳下），半徑 {s.areaRadius:0.#}m" : $"以自己為中心半徑 {s.areaRadius:0.#}m 的範圍");
                    if (s.hits > 1) Line($"連續 {s.hits} 段，每段間隔 {s.hitGap:0.##} 秒");
                    break;
                case SkillType.Projectile when s.fromAbove:
                    if (s.selfCenter) Line($"以自己為中心：光柱落在自己腳下，半徑 {s.areaRadius:0.#}m 內的敵人立刻受傷");
                    else Line($"從目標正上方落下（射程 {s.rangeMax:0.#}m）；沒鎖定時落在準星指的地面");
                    break;
                case SkillType.Projectile when s.releaseNear:
                    Line($"出手距離 {s.rangeMax:0.#}m 以內（伏擊：靠近就自動命中）");
                    break;
                case SkillType.Projectile:
                    Line(s.rangeMin > 0f ? $"射程 {s.rangeMin:0.#}～{s.rangeMax:0.#}m（{s.rangeMin:0.#}m 內打不到）" : $"射程 {s.rangeMax:0.#}m");
                    Line($"飛行速度 {s.speed:0.#} m/秒（{SpeedWord(s.speed)}）；判定半徑 {s.radius * 100f:0} 公分");
                    break;
                case SkillType.Trap:
                    Line($"設在自己 {s.trapRange:0.#}m 內的地上；{s.trapArm:0.#} 秒後生效，持續 {s.trapLife:0} 秒");
                    Line($"同一種陷阱最多 {s.trapMax} 個（多設會移除最舊的）；觸發範圍半徑 {s.radius * 100f:0} 公分");
                    break;
            }

            // ---- 效果
            var eff = new StringBuilder();
            void E(string t) => eff.Append("・").Append(t).Append('\n');
            switch (s.effect)
            {
                case EffectKind.Dot: E($"中毒：每秒 {s.effectDps:0.#} 傷害，持續 {s.effectDur:0.#} 秒"); break;
                case EffectKind.Blind: E($"致盲：對手畫面被煙霧遮住 {s.effectDur:0.#} 秒"); break;
                case EffectKind.Snare: E($"定身 {s.effectDur:0.#} 秒：打斷對手正在蓄力的招式，這段時間不能唸新的技能（牧師的淨化除外）；電腦、Boss、小怪會站著不動"); break;
                case EffectKind.Mark: E($"懲戒 {s.effectDur:0.#} 秒：目標受到的所有傷害 +20%"); break;
                case EffectKind.Burn: E($"灼燒：每秒 {s.effectDps:0.#} 傷害，持續 {s.effectDur:0.#} 秒"); break;
            }
            if (s.pierce) E($"穿透：一路穿過所有小怪（每隻打一次），最後打到 Boss／對手；穿透第一個敵人後，之後的傷害 −{Battle.PierceFalloff * 100:0}%");
            if (s.id == "na_arrow" && s.multi > 1) E($"一次射 {s.multi} 支箭：每多一支箭攻擊速度 −{SkillTrees.ExtraArrowSlow * 100:0}%（集氣約 {s.cooldown:0.#} 秒一次）");
            if (s.execute) E("處決：目標 HP 低於 25% 時傷害 ×3");
            if (s.self == SelfKind.Pet) E($"召喚物存在 {s.petLife:0} 秒；每 {s.petInterval:0.#} 秒攻擊 {s.petRange:0.#}m 內最近的敵人（傷害 {s.damage}）" + (s.petHp > 0 ? $"；HP {s.petHp}" : "") + $"；同時最多 {Battle.MaxPets} 隻");
            if (s.id == "meteor") E($"落地留下直徑 {Battle.BurnRadius * 2f:0.#}m 的火海，持續 {Battle.BurnDuration:0} 秒，站在裡面每秒 {Battle.BurnDps:0.#} 傷害");
            if (s.id == "wind") E("隨機從左或右側繞弧線飛向目標");
            if (s.groundZone) E($"落地（或砸中）留下直徑 {s.areaRadius * 2f:0.#}m 的光圈，持續 {s.buffDur:0.#} 秒；經過的敵人每秒 {s.effectDps:0.#} 傷害");
            if (s.id == "plague") E($"引爆目標身上所有中毒／灼燒：剩下的持續傷害 ×{Battle.PlagueMul:0.#} 一次打完");
            if (s.id == "na_kunai") E("技能樹「火遁／冰遁／雷遁苦無」：每發普攻有機率同時多射出燃燒、冰凍、帶電的苦無");
            if (s.id == "slash") E($"點了劍士技能樹的「斬開法球」：揮出後 {Battle.SliceWindow:0.#} 秒內，斬開正面 {Battle.SliceRange:0.#}m 內飛來的法球（從天而降、地裂、橫掃等範圍攻擊無效）");
            if (s.id == "nv_swing") E("不會斬開飛來的法球");
            if (s.releaseNear)
            {
                E("蓄力條件：手掌伸到鏡頭前（敵人貼身也能蓄力；蓄滿時敵人已經在身邊就直接出手）");
                E($"蓄滿自動進入伏擊 {Battle.AmbushWindow:0} 秒（不用放招動作）：靠近敵人到直徑 {Battle.AmbushReach * 2f:0.#}m 內自動出手；時間到沒靠近就落空");
                E($"點了技能樹的「暗殺術」：離開敵人視線累計 {Battle.StealthTime:0} 秒（可以分好幾次，被看見只是暫停）就完成蓄力（對戰時＝離開對手手機的畫面），伏擊維持 {Battle.StealthAmbushWindow:0} 秒，靠近就出手");
                E("伏擊中被打到會取消、要重新蓄力；但敵人已經在出手距離內時不會取消（直接出手）");
            }
            switch (s.self)
            {
                case SelfKind.Heal: E($"回復 {s.heal} HP"); break;
                case SelfKind.Cleanse: E($"解除中毒、定身、致盲{(s.heal > 0 ? $"，並回復 {s.heal} HP" : "")}"); break;
                case SelfKind.Block: E($"{s.buffDur:0.#} 秒內下一次受到的傷害 -{s.buffReduce * 100f:0}%（擋一次就結束）"); break;
                case SelfKind.Shield: E($"護盾 {s.buffDur:0.#} 秒，最多吸收 {s.buffAmount} 傷害"); break;
                case SelfKind.Counter: E($"{s.buffDur:0.#} 秒內被攻擊打中：不受傷，並把那一發原樣打回去（陷阱無效）"); break;
                case SelfKind.Armor: E($"{s.buffDur:0.#} 秒內受到的所有傷害 -{s.buffReduce * 100f:0}%"); break;
                case SelfKind.Taunt:
                    E($"{s.buffDur:0.#} 秒內受到的所有傷害 -{s.buffReduce * 100f:0}%");
                    E($"合作時：Boss 的仇恨變成「目前最高的人＋{s.buffAmount}」，Boss 沒在蓄力就立刻轉向你（保護隊友）");
                    break;
                case SelfKind.Prayer: E($"所有技能目前剩下的冷卻時間減半（祈禱自己除外），並回復 {s.buffAmount} MP"); break;
                case SelfKind.Dispel: E($"以自己為中心直徑 {s.areaRadius * 2f:0.#}m：範圍內敵人的蓄力、蓄滿待放、伏擊全部取消，那一招重新冷卻"); break;
                case SelfKind.Mana: E($"回復 {s.buffAmount} MP"); break;
                case SelfKind.Focus: E($"{s.buffDur:0} 秒內所有技能的冷卻時間減半；放出當下正在冷卻的也立刻減半"); break;
                case SelfKind.Prep: E($"蓄滿放出後 {s.buffDur:0} 秒內，所有攻擊傷害 +{s.buffReduce * 100f:0}%；這段時間受到任何傷害就被打斷、加成取消"); break;
                case SelfKind.Oath: E($"{s.buffDur:0} 秒內：自己受到的傷害 -20%；合作時，在你 {Battle.AuraRange:0}m 內的隊友受到的傷害有 {s.buffReduce * 100f:0}% 轉到你身上"); break;
                case SelfKind.Frenzy: E($"扣除目前 HP 的 {s.buffReduce * 100f:0}%；{s.buffDur:0} 秒內技能與普攻的冷卻變快（攻速 ×1.5）"); break;
                case SelfKind.Undying: E($"{s.buffDur:0} 秒內受到致命傷害：保留 1 HP，並無敵 3 秒（只觸發一次）"); break;
                case SelfKind.DeadMark: E($"{s.buffDur:0} 秒內射出的下一箭必定爆擊（傷害 ×2）"); break;
                case SelfKind.RollShot: E($"{s.buffDur:0} 秒內：每次側跨閃避自動朝最近的敵人射一箭（傷害 {Skills.All["rollarrow"].damage}）"); break;
                case SelfKind.TeamAtk: E($"自己與隊友 {s.buffDur:0} 秒內攻擊傷害 +{s.buffAmount}%"); break;
                case SelfKind.TeamHaste: E($"自己與隊友 {s.buffDur:0} 秒內普攻集氣、技能蓄力加快 {s.buffAmount}%"); break;
                case SelfKind.TeamRegen: E($"自己與隊友 {s.buffDur:0} 秒內每秒回復 {s.buffAmount} HP（共 {s.buffAmount * s.buffDur:0}）"); break;
                case SelfKind.TeamCrit: E($"自己與隊友 {s.buffDur:0} 秒內爆擊率 +{s.buffAmount}%"); break;
                case SelfKind.ManaShield: E($"{s.buffDur:0} 秒內受到的傷害先扣 MP（1 MP 擋 1 點），MP 用完才扣 HP"); break;
                case SelfKind.Detonate: E($"每隻召喚物在周圍 {s.areaRadius:0.#}m 爆炸（傷害 {Skills.All["pet_blast"].damage}），召喚物消失"); break;
                case SelfKind.Substitute: E($"{s.buffDur:0.#} 秒內下一次被打中：完全不受傷害（陷阱、範圍攻擊也算）"); break;
                case SelfKind.Clone: E($"分身持續 {s.buffDur:0} 秒：受到的攻擊有 50% 機率打到分身（完全無效）"); break;
                case SelfKind.Dance: E($"{s.buffDur:0} 秒內：伏擊技能蓄力時間減半；伏擊出手距離加倍"); break;
                case SelfKind.Fog: E($"腳下直徑 {s.areaRadius * 2f:0.#}m 的毒霧，持續 {s.buffDur:0} 秒；範圍內每個敵人每秒 {s.effectDps:0.#} 傷害"); break;
                case SelfKind.Revive: E($"倒下的隊友以 {s.buffAmount}% HP 復活；活著的人（含自己）獲得 {s.buffDur:0} 秒復活祝福：倒下時自動以 30% HP 站起來"); break;
                case SelfKind.HealPct: E($"回復最大 HP 的 {s.buffAmount}%"); break;
                case SelfKind.TeamShield: E($"護盾 {s.buffDur:0} 秒，吸收 {s.buffAmount} 傷害"); break;
                case SelfKind.ShadowClone: E($"影分身（電腦操作，最多 {Battle.CloneLife:0} 秒）：自己的 HP 上限、目前 HP 各分一半給它；每 3 次攻擊放一次影殺（×3）、每 4 次讓目標中毒；倒下或時間到 HP 上限恢復（受的傷不會回來）"); break;
                case SelfKind.Barrier: E($"腳下半徑 {s.areaRadius:0.#}m 的障壁 {s.buffDur:0} 秒：裡面的友軍（含隊友、召喚獸）被攻擊時不受傷，共抵擋 {s.buffAmount} 次（持續傷害不算）"); break;
            }
            if (s.cls == "priest" && s.type == SkillType.Self && s.self != SelfKind.Barrier) E("作用在所有己方單位：自己＋合作模式的隊友（不限距離）");
            if (eff.Length > 0) sb.Append("\n【效果】\n").Append(eff);

            // ---- 怎麼躲（攻擊技能）
            if (s.type != SkillType.Self && s.damage > 0)
            {
                sb.Append("\n【對手怎麼躲】\n");
                if (s.type == SkillType.Trap) Line("看得到地上的陷阱，繞過去就不會觸發");
                else if (s.areaRadius > 0f) Line("走出紅圈");
                else if (s.fromAbove) Line("落下時離開地上的紅圈");
                else if (s.releaseNear)
                {
                    Line($"伏擊一靠近就命中、閃不掉：保持距離 {s.rangeMax:0.#}m 以上，撐過 {Battle.AmbushWindow:0} 秒（點了暗殺術是 {Battle.StealthAmbushWindow:0} 秒）就落空；或趁它還沒靠近時打它一下就能取消");
                    Line("刺客點了「近身本能」時，2m 內打它傷害會降低、它的閃避也會提高");
                    Line("法師的抗拒法環可以取消伏擊");
                }
                else if (Skills.IsSwipeBasic(s)) Line($"揮砍前會先出現紅色刀線（約 {Battle.SlashWindup:0.##} 秒）：蹲下躲過、或退到 {s.rangeMax + Battle.SlashReach:0.#}m 外");
                else if (s.id == "na_dagger") Line($"刺之前會先出現紅色扇形（約 {Battle.StabWindup:0.#} 秒）：退到 {s.rangeMax:0.#}m 外或閃到它側面");
                else if (s.id == "slash" || Skills.IsMeleeBasic(s)) Line($"只砍前方半球（半徑 {s.rangeMax:0.#}m）：退到範圍外或繞到它背後");
                else
                {
                    Line("側跨沒有無敵時間：要真的移開，讓法術碰不到身體才算閃過");
                    if (s.crescent) Line("新月形：只有碰到新月本身才會受傷，看準弧線的位置閃開");
                    if (s.duckable) Line("胸口高度的橫掃：蹲下也能躲過");
                    Line($"飛超過射程 {s.rangeMax:0.#}m 就會淡出消失");
                    if (Battle.Sliceable(s)) Line("劍士的斬擊可以把它斬開");
                }
            }
            return sb.ToString().TrimEnd('\n');
        }

        static string SpeedWord(float v) => v <= 6f ? "慢，容易閃" : v <= 9f ? "中等" : v < 12f ? "快" : "非常快，很難閃";
    }
}
