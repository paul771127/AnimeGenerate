using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    public enum SkillType { Projectile, Self, Trap }
    public enum SelfKind { None, Heal, Block, Shield, Counter, Armor, Cleanse, Prayer, Dispel, Summon, Mana, Focus, Prep }
    public enum EffectKind { None, Dot, Blind, Snare }

    /// <summary>
    /// 技能資料（從網頁版 spellbattle/js/skills.js 移植）。
    /// 網頁版用「螢幕比例、飛行毫秒」，這裡全部改成真實空間單位：
    ///   speed 公尺/秒、radius 公尺（法術球體半徑）、rangeMin/Max 公尺。
    /// 法術最多飛到 rangeMax + 1 公尺就消失，所以近戰技能真的只打得到近處。
    /// </summary>
    public class SkillDef
    {
        public string id, cls, name;
        public SkillType type;
        public Color color;
        public int cost, damage;
        public float charge;            // 蓄力秒數：詠唱後至少要等這麼久才能發射
        public float cooldown;
        public float speed = 8f, radius = 0.1f, rangeMin, rangeMax = 8f;
        public bool lob;                // 拋物線投擲（毒液彈）：落地才判定，落點周圍 radius 公尺
        public bool atTarget;           // 範圍攻擊打在目標位置（樹根爆發），不是施放者周圍
        public float stillRadius;       // 蓄力時要站著不動：離開詠唱位置超過這個半徑（公尺）就失敗（刺殺準備）
        public bool releaseNear;        // 距離不在射程內時不能出手（保留蓄力，靠近再放）
        public bool duckable;           // 橫向的攻擊（斬擊、劍氣、風刃、三連矢）：蹲下就躲得過
        public bool fromAbove; public float dropHeight = 6f;   // 從目標正上方落下（隕石、審判）
        public float areaRadius;        // 範圍攻擊：放招當下目標在施放者周圍這個半徑內就命中（Boss 的地裂重擊）
        public float originHeight;      // 法術從離地這個高度水平射出（Boss 的橫掃）；0＝從手的位置
        public int multi = 1;           // 一次射出幾發（扇形散開）
        public float spreadDeg;
        public EffectKind effect; public float effectDps, effectDur;
        public SelfKind self; public int heal; public float buffDur, buffReduce; public int buffAmount;
        public float trapLife = 30f, trapArm = 1.2f, trapRange = 3f; public int trapMax = 2;
        public string desc;

        public float MaxTravel => rangeMax + 1f;
        public string RangeText => type == SkillType.Projectile ? $"≤{rangeMax:0.#}m" : type == SkillType.Trap ? $"設置≤{trapRange:0.#}m" : "自身";
    }

    public class ClassDef
    {
        public string id, name;
        public Color color;
        public int maxHp, maxMp;
        public float mpRegen;
        public string[] skills, defaultLoadout;
        public string desc;
    }

    public static class Skills
    {
        public static readonly Dictionary<string, SkillDef> All = new Dictionary<string, SkillDef>();
        public static readonly Dictionary<string, ClassDef> Classes = new Dictionary<string, ClassDef>();
        public static readonly string[] ClassOrder = { "archer", "mage", "assassin", "swordsman", "priest" };

        /// <summary>每個技能的手勢：依職業技能順序分配（同職業不重複）</summary>
        public static HandGesture.Shape GestureOf(string skillId)
        {
            if (!All.TryGetValue(skillId, out var s) || !Classes.TryGetValue(s.cls, out var c)) return HandGesture.Shape.None;
            int i = System.Array.IndexOf(c.skills, skillId);
            return i >= 0 && i < HandGesture.SkillShapes.Length ? HandGesture.SkillShapes[i] : HandGesture.Shape.None;
        }

        // 自己解析 #rrggbb（不用 ColorUtility，讓純邏輯可在 Unity 外測試）
        static Color C(string hex)
        {
            int v = System.Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }

        static void P(SkillDef s) => All[s.id] = s;

        static Skills()
        {
            // ---------------------------------------------------------- 獵人：蓄力長、箭速快、遠距離、判定小；近距離只有速射
            P(new SkillDef { id = "quickshot", cls = "archer", name = "速射", type = SkillType.Projectile, color = C("#d9f99d"), cost = 12, damage = 12, charge = 0.6f, cooldown = 0.5f, speed = 14f, radius = 0.05f, rangeMax = 12f, desc = "唯一的近距離技能，貼身也能射" });
            P(new SkillDef { id = "snipe", cls = "archer", name = "狙擊", type = SkillType.Projectile, color = C("#fde047"), cost = 30, damage = 34, charge = 1.4f, cooldown = 1.8f, speed = 16f, radius = 0.04f, rangeMin = 3f, rangeMax = 15f, desc = "蓄力最久的重箭，判定最小；3m 內打不到" });
            P(new SkillDef { duckable = true, id = "triple", cls = "archer", name = "三連矢", type = SkillType.Projectile, color = C("#a3e635"), cost = 24, damage = 9, charge = 1.0f, cooldown = 1.2f, speed = 12f, radius = 0.05f, rangeMin = 2f, rangeMax = 10f, multi = 3, spreadDeg = 8f, desc = "三箭扇形散開；2m 內打不到" });
            P(new SkillDef { id = "focus", cls = "archer", name = "精神集中", type = SkillType.Self, self = SelfKind.Focus, color = C("#facc15"), cost = 25, charge = 1.0f, cooldown = 40f, buffDur = 20f, desc = "20 秒內所有技能冷卻時間減半" });
            P(new SkillDef { id = "snaretrap", cls = "archer", name = "捕獸夾", type = SkillType.Trap, color = C("#fb923c"), cost = 18, damage = 12, charge = 0.8f, cooldown = 2.5f, radius = 0.35f, trapArm = 1.2f, effect = EffectKind.Snare, effectDur = 2.5f, desc = "設在自己 3m 內的地上，踩到受傷並定身 2.5 秒" });
            P(new SkillDef { id = "blasttrap", cls = "archer", name = "爆裂陷阱", type = SkillType.Trap, color = C("#f87171"), cost = 28, damage = 28, charge = 1.0f, cooldown = 3.5f, radius = 0.45f, trapArm = 1.5f, desc = "設在自己 3m 內的地上，踩到爆炸" });

            // ---------------------------------------------------------- 法師：攻擊力最強、蓄力最長；近距離只有風刃
            P(new SkillDef { id = "fire", cls = "mage", name = "火球術", type = SkillType.Projectile, color = C("#ff7a1a"), cost = 40, damage = 32, charge = 1.5f, cooldown = 1.2f, speed = 4f, radius = 0.15f, rangeMin = 2f, rangeMax = 6f, desc = "中速火球；2m 內打不到" });
            P(new SkillDef { id = "ice", cls = "mage", name = "冰槍", type = SkillType.Projectile, color = C("#5fd7ff"), cost = 50, damage = 36, charge = 1.4f, cooldown = 1.5f, speed = 6f, radius = 0.09f, rangeMin = 2f, rangeMax = 7f, desc = "快速穿刺，判定較窄；2m 內打不到" });
            P(new SkillDef { id = "thunder", cls = "mage", name = "雷擊", type = SkillType.Projectile, color = C("#c08bff"), cost = 70, damage = 48, charge = 2.2f, cooldown = 2.5f, speed = 14f, radius = 0.07f, rangeMin = 3f, rangeMax = 10f, desc = "幾乎瞬發，判定極窄；3m 內打不到" });
            P(new SkillDef { duckable = true, id = "wind", cls = "mage", name = "風刃", type = SkillType.Projectile, color = C("#7dffb0"), cost = 24, damage = 22, charge = 1.0f, cooldown = 0.8f, speed = 5f, radius = 0.25f, rangeMax = 2.5f, desc = "近距離；隨機從左或右側繞弧線飛向目標" });
            P(new SkillDef { fromAbove = true, id = "meteor", cls = "mage", name = "隕石", type = SkillType.Projectile, color = C("#ff3b3b"), cost = 100, damage = 72, charge = 3.0f, cooldown = 4f, speed = 7f, radius = 0.3f, rangeMin = 3f, rangeMax = 8f, desc = "從敵人正上方砸下（可閃），落地留下直徑 3m 燃燒 10 秒的火海；3m 內打不到" });
            P(new SkillDef { id = "dispel", cls = "mage", name = "抗拒法環", type = SkillType.Self, self = SelfKind.Dispel, color = C("#f0abfc"), cost = 50, charge = 0.8f, cooldown = 12f, areaRadius = 1.5f, desc = "以自己為中心直徑 3m：打斷範圍內所有敵人的蓄力，被打斷的技能重新冷卻" });
            P(new SkillDef { id = "meditate", cls = "mage", name = "冥想", type = SkillType.Self, self = SelfKind.Mana, color = C("#93c5fd"), cost = 1, buffAmount = 30, charge = 2.0f, cooldown = 10f, desc = "回復 30 MP（只消耗 1 MP）" });
            P(new SkillDef { id = "heal", cls = "mage", name = "治癒", type = SkillType.Self, self = SelfKind.Heal, color = C("#3dff7a"), cost = 60, heal = 25, charge = 1.5f, cooldown = 5f, desc = "回復 25 HP" });

            // ---------------------------------------------------------- 刺客：遠處蓄力、靠近才能出手、判定小
            P(new SkillDef { id = "backstab", cls = "assassin", name = "刺殺", type = SkillType.Projectile, color = C("#e879f9"), cost = 25, damage = 45, charge = 2.0f, cooldown = 5f, speed = 10f, radius = 0.08f, rangeMax = 1.5f, releaseNear = true, desc = "1.5m 內才能出手" });
            P(new SkillDef { id = "shadow", cls = "assassin", name = "影襲", type = SkillType.Projectile, color = C("#a78bfa"), cost = 20, damage = 38, charge = 1.0f, cooldown = 3f, speed = 9f, radius = 0.08f, rangeMax = 2.5f, releaseNear = true, desc = "2.5m 內才能出手" });
            P(new SkillDef { id = "poison", cls = "assassin", name = "毒刃", type = SkillType.Projectile, color = C("#4ade80"), cost = 22, damage = 16, charge = 1.1f, cooldown = 5.5f, speed = 9f, radius = 0.08f, rangeMax = 2f, releaseNear = true, effect = EffectKind.Dot, effectDps = 5f, effectDur = 5f, desc = "2m 內出手，中毒 5 秒（每秒 5）" });
            P(new SkillDef { id = "knife", cls = "assassin", name = "飛刀", type = SkillType.Projectile, color = C("#cbd5e1"), cost = 10, damage = 14, charge = 0.7f, cooldown = 1.6f, speed = 10f, radius = 0.05f, rangeMax = 5f, desc = "遠程牽制" });
            P(new SkillDef { id = "prep", cls = "assassin", name = "刺殺準備", type = SkillType.Self, self = SelfKind.Prep, color = C("#f43f5e"), cost = 20, charge = 5f, cooldown = 15f, buffDur = 8f, buffReduce = 0.8f, stillRadius = 0.5f,
                desc = "蓄力 5 秒（期間離開原地超過直徑 1m 就失敗）→ 8 秒內傷害 +80%；這段時間受到傷害就被打斷、加成取消" });
            P(new SkillDef { id = "smoke", cls = "assassin", name = "煙霧彈", type = SkillType.Projectile, color = C("#9ca3af"), cost = 18, damage = 0, charge = 0.6f, cooldown = 12f, speed = 5f, radius = 0.25f, rangeMax = 6f, effect = EffectKind.Blind, effectDur = 4f, desc = "命中後對手視線被遮住 4 秒" });

            // ---------------------------------------------------------- 劍士：近遠攻擊都有、多種防禦
            P(new SkillDef { duckable = true, id = "slash", cls = "swordsman", name = "斬擊", type = SkillType.Projectile, color = C("#f8fafc"), cost = 15, damage = 26, charge = 0f, cooldown = 1f, speed = 8f, radius = 0.3f, rangeMax = 2f, desc = "近身大範圍橫斬；不用蓄力，手刀劈過就出招；同時斬開正面 1m 內飛來的法球（範圍攻擊無效）" });
            P(new SkillDef { id = "thrust", cls = "swordsman", name = "突刺", type = SkillType.Projectile, color = C("#fcd34d"), cost = 15, damage = 22, charge = 0.7f, cooldown = 1.2f, speed = 9f, radius = 0.08f, rangeMax = 3f, desc = "中距離直刺" });
            P(new SkillDef { duckable = true, id = "wave", cls = "swordsman", name = "劍氣", type = SkillType.Projectile, color = C("#93c5fd"), cost = 20, damage = 16, charge = 0f, cooldown = 1.5f, speed = 6f, radius = 0.2f, rangeMax = 7f, desc = "遠程劍氣；不用蓄力，手刀劈過就出招" });
            P(new SkillDef { id = "block", cls = "swordsman", name = "格擋", type = SkillType.Self, self = SelfKind.Block, color = C("#60a5fa"), cost = 12, charge = 0.2f, cooldown = 4f, buffDur = 3f, buffReduce = 0.7f, desc = "3 秒內下一次傷害 -70%" });
            P(new SkillDef { id = "ironwall", cls = "swordsman", name = "鐵壁", type = SkillType.Self, self = SelfKind.Shield, color = C("#94a3b8"), cost = 30, charge = 0.6f, cooldown = 12f, buffDur = 8f, buffAmount = 35, desc = "8 秒護盾，吸收 35 傷害" });
            // ---------------------------------------------------------- 牧師：回復、淨化、守護、減冷卻；也有聖光攻擊
            P(new SkillDef { id = "holy", cls = "priest", name = "聖光彈", type = SkillType.Projectile, color = C("#fff3b0"), cost = 15, damage = 18, charge = 1.0f, cooldown = 1.0f, speed = 9f, radius = 0.12f, rangeMax = 9f, desc = "聖光球，射程 9m" });
            P(new SkillDef { fromAbove = true, dropHeight = 4f, id = "judgement", cls = "priest", name = "審判", type = SkillType.Projectile, color = C("#fbbf24"), cost = 30, damage = 30, charge = 2.0f, cooldown = 3.5f, speed = 9f, radius = 0.45f, rangeMax = 10f, desc = "光柱從敵人正上方落下（可閃）" });
            P(new SkillDef { id = "prayheal", cls = "priest", name = "治療術", type = SkillType.Self, self = SelfKind.Heal, color = C("#86efac"), cost = 30, heal = 35, charge = 1.5f, cooldown = 6f, desc = "自己與隊友各回復 35 HP" });
            P(new SkillDef { id = "cleanse", cls = "priest", name = "淨化", type = SkillType.Self, self = SelfKind.Cleanse, color = C("#a5f3fc"), cost = 15, heal = 8, charge = 0.6f, cooldown = 6f, desc = "自己與隊友：解除中毒、定身、致盲，並回復 8 HP" });
            P(new SkillDef { id = "guard", cls = "priest", name = "守護", type = SkillType.Self, self = SelfKind.Armor, color = C("#fcd34d"), cost = 25, charge = 1.0f, cooldown = 10f, buffDur = 6f, buffReduce = 0.4f, desc = "自己與隊友 6 秒內受到的傷害 -40%" });
            P(new SkillDef { id = "prayer", cls = "priest", name = "祈禱", type = SkillType.Self, self = SelfKind.Prayer, color = C("#c4b5fd"), cost = 0, charge = 1.5f, cooldown = 15f, buffAmount = 20, buffDur = 4f, desc = "自己與隊友：所有技能目前的冷卻時間減半，並回復 20 MP" });

            P(new SkillDef { id = "counter", cls = "swordsman", name = "反擊", type = SkillType.Self, self = SelfKind.Counter, color = C("#f472b6"), cost = 18, charge = 0.2f, cooldown = 6f, buffDur = 1.5f, desc = "1.5 秒內被打中：無傷，並把攻擊打回去" });

            Classes["archer"] = new ClassDef { id = "archer", name = "獵人", color = C("#a3e635"), maxHp = 100, maxMp = 100, mpRegen = 3.5f, skills = new[] { "quickshot", "snipe", "triple", "snaretrap", "blasttrap", "focus" }, defaultLoadout = new[] { "quickshot", "snipe", "snaretrap" }, desc = "箭速快、射程遠；近距離只有速射；可設陷阱" };
            Classes["mage"] = new ClassDef { id = "mage", name = "法師", color = C("#c084fc"), maxHp = 90, maxMp = 120, mpRegen = 4.9f, skills = new[] { "fire", "ice", "wind", "meteor", "dispel", "meditate" }, defaultLoadout = new[] { "fire", "wind", "meteor" }, desc = "攻擊力最強、蓄力最長；近距離只有風刃" };
            Classes["assassin"] = new ClassDef { id = "assassin", name = "刺客", color = C("#e879f9"), maxHp = 60, maxMp = 100, mpRegen = 4, skills = new[] { "backstab", "shadow", "poison", "knife", "smoke", "prep" }, defaultLoadout = new[] { "backstab", "poison", "smoke" }, desc = "遠處蓄力、靠近才能出手" };
            // ---------------------------------------------------------- Boss：熔岩巨像（不在職業選單裡）
            P(new SkillDef { id = "b_bolt", cls = "boss", name = "熔岩彈", type = SkillType.Projectile, color = C("#ff6a00"), cost = 0, damage = 9, charge = 1.4f, cooldown = 4f, speed = 6f, radius = 0.18f, rangeMax = 12f, multi = 3, spreadDeg = 12f, desc = "三顆熔岩彈扇形射出（側跨閃開）" });
            P(new SkillDef { duckable = true, originHeight = 1.15f, id = "b_sweep", cls = "boss", name = "橫掃", type = SkillType.Projectile, color = C("#ff9f43"), cost = 0, damage = 18, charge = 1.6f, cooldown = 6f, speed = 7f, radius = 0.6f, rangeMax = 6f, desc = "胸口高度的橫向衝擊波（蹲下躲）" });
            P(new SkillDef { areaRadius = 3.2f, id = "b_slam", cls = "boss", name = "地裂重擊", type = SkillType.Projectile, color = C("#ef4444"), cost = 0, damage = 24, charge = 2.0f, cooldown = 8f, speed = 1f, radius = 0f, rangeMax = 3.2f, desc = "雙拳砸地，周圍 3.2m 都會受傷（走出紅圈）" });
            P(new SkillDef { fromAbove = true, dropHeight = 7f, id = "b_rain", cls = "boss", name = "落石雨", type = SkillType.Projectile, color = C("#f97316"), cost = 0, damage = 15, charge = 2.2f, cooldown = 10f, speed = 8f, radius = 0.5f, rangeMax = 14f, multi = 4, desc = "四顆巨石從天而降（看紅圈閃開）" });
            P(new SkillDef { areaRadius = 2.0f, id = "b_leap", cls = "boss", name = "熔岩躍擊", type = SkillType.Projectile, color = C("#dc2626"), cost = 0, damage = 20, charge = 1.4f, cooldown = 9f, speed = 1f, radius = 0f, rangeMax = 14f, desc = "跳到遠處目標的位置砸下，落點周圍 2m 受傷（蓄力時落點有紅圈，離開紅圈）" });
            P(new SkillDef { id = "b_summon", cls = "boss", name = "召喚小怪", type = SkillType.Self, self = SelfKind.Summon, color = C("#ff7b29"), cost = 0, charge = 1.8f, cooldown = 16f, multi = 4, desc = "召喚熔岩小鬼（合作時多一隻）" });
            P(new SkillDef { id = "m_fireball", cls = "minion", name = "火焰射手的火球", type = SkillType.Projectile, color = C("#67e8f9"), cost = 0, damage = 8, charge = 0.8f, cooldown = 3f, speed = 6f, radius = 0.12f, rangeMax = 9f, desc = "遠程小怪的火球" });
            P(new SkillDef { fromAbove = true, dropHeight = 5f, id = "m_rock", cls = "minion", name = "投石", type = SkillType.Projectile, color = C("#f59e0b"), cost = 0, damage = 12, charge = 1.2f, cooldown = 4.5f, speed = 7f, radius = 0.45f, rangeMax = 11f, desc = "岩石投擲者：石頭砸在你腳下（看紅圈閃開）" });
            P(new SkillDef { id = "m_void", cls = "minion", name = "虛空彈", type = SkillType.Projectile, color = C("#a855f7"), cost = 0, damage = 10, charge = 1.0f, cooldown = 4f, speed = 7f, radius = 0.13f, rangeMax = 14f, desc = "虛空術士：超遠距離（超過 10m）的虛空彈；出手後停留 3 秒，再瞬移到場內隨機位置" });
            P(new SkillDef { id = "m_snipe", cls = "minion", name = "熔岩狙擊", type = SkillType.Projectile, color = C("#ff4fd8"), cost = 0, damage = 15, charge = 1.6f, cooldown = 5f, speed = 16f, radius = 0.08f, rangeMax = 14f, desc = "熔岩狙擊手：瞄準 1.6 秒（有紅線）後高速射擊（側跨閃）" });
            P(new SkillDef { id = "m_bomb", cls = "minion", name = "自爆", type = SkillType.Projectile, color = C("#fde047"), cost = 0, damage = 18, charge = 0.5f, cooldown = 1f, areaRadius = 1.8f, radius = 0f, rangeMax = 1.3f, desc = "自爆小鬼的爆炸" });
            // ---------------------------------------------------------- 森林（無盡模式第 11～20 波）：植物小怪
            P(new SkillDef { lob = true, id = "m_venom", cls = "minion", name = "毒液彈", type = SkillType.Projectile, color = C("#a3e635"), cost = 0, damage = 8, charge = 1.0f, cooldown = 5f, speed = 6f, radius = 0.5f, rangeMax = 8f, effect = EffectKind.Dot, effectDps = 3f, effectDur = 4f, desc = "毒液噴花：往上拋物線射出毒液彈，落地傷害＋中毒，並留下直徑 1m 的毒液區 5 秒" });
            P(new SkillDef { id = "m_pool", cls = "minion", name = "毒液區", type = SkillType.Projectile, color = C("#84cc16"), cost = 0, damage = 3, charge = 0f, cooldown = 0f, radius = 0.5f, effect = EffectKind.Dot, effectDps = 2f, effectDur = 3f, desc = "踩到毒液區：受傷並中毒（每秒最多一次）" });
            P(new SkillDef { id = "m_dash", cls = "minion", name = "荊棘衝撞", type = SkillType.Projectile, color = C("#a16207"), cost = 0, damage = 14, charge = 0.8f, cooldown = 4f, speed = 5f, radius = 0.35f, rangeMax = 5f, desc = "荊棘衝鋒獸：往玩家方向直線衝刺撞擊（看地上的紅線閃開）" });
            P(new SkillDef { id = "m_vine", cls = "minion", name = "纏繞藤蔓", type = SkillType.Projectile, color = C("#15803d"), cost = 0, damage = 8, charge = 1.0f, cooldown = 6f, radius = 0.18f, rangeMax = 6f, desc = "藤蔓術士：從自己長到玩家當下位置的藤蔓，持續 3 秒，碰到就受傷；施放期間術士不能移動" });
            P(new SkillDef { id = "m_bite", cls = "minion", name = "食人花咬", type = SkillType.Projectile, color = C("#dc2626"), cost = 0, damage = 9, charge = 0.7f, cooldown = 1.8f, radius = 0f, rangeMax = 1.1f, desc = "食人花的近身啃咬" });
            P(new SkillDef { id = "m_spore", cls = "minion", name = "孢子爆發", type = SkillType.Projectile, color = C("#c084fc"), cost = 0, damage = 5, charge = 0.8f, cooldown = 4f, areaRadius = 1.5f, radius = 0f, rangeMax = 1.3f, effect = EffectKind.Dot, effectDps = 3f, effectDur = 3f, desc = "孢子菇：周圍 1.5m 的孢子雲，受傷並中毒" });
            P(new SkillDef { id = "m_seed", cls = "minion", name = "種子連射", type = SkillType.Projectile, color = C("#ca8a04"), cost = 0, damage = 5, charge = 0.9f, cooldown = 3.5f, speed = 11f, radius = 0.06f, rangeMax = 9f, multi = 3, spreadDeg = 8f, desc = "種子槍手：三顆種子扇形射出" });
            // 森林 Boss：腐化樹王
            P(new SkillDef { areaRadius = 3.0f, id = "p_stomp", cls = "boss", name = "樹根踐踏", type = SkillType.Projectile, color = C("#65a30d"), cost = 0, damage = 22, charge = 2.0f, cooldown = 8f, speed = 1f, radius = 0f, rangeMax = 3f, desc = "腳下樹根一起砸地，周圍 3m 都會受傷（走出紅圈）" });
            P(new SkillDef { duckable = true, originHeight = 1.05f, id = "p_whip", cls = "boss", name = "藤鞭橫掃", type = SkillType.Projectile, color = C("#16a34a"), cost = 0, damage = 17, charge = 1.5f, cooldown = 6f, speed = 7f, radius = 0.55f, rangeMax = 6f, desc = "胸口高度的藤鞭橫掃（蹲下躲）" });
            P(new SkillDef { id = "p_thorn", cls = "boss", name = "荊棘飛刺", type = SkillType.Projectile, color = C("#84cc16"), cost = 0, damage = 7, charge = 1.4f, cooldown = 4f, speed = 8f, radius = 0.1f, rangeMax = 12f, multi = 5, spreadDeg = 10f, desc = "五根荊棘扇形射出（側跨閃開）" });
            P(new SkillDef { lob = true, id = "p_spore", cls = "boss", name = "毒孢彈", type = SkillType.Projectile, color = C("#a855f7"), cost = 0, damage = 10, charge = 2.0f, cooldown = 9f, speed = 6f, radius = 0.5f, rangeMax = 12f, multi = 3, effect = EffectKind.Dot, effectDps = 3f, effectDur = 4f, desc = "三顆毒孢彈拋向你周圍，落地留下毒液區（看落點紅圈閃開）" });
            P(new SkillDef { atTarget = true, areaRadius = 1.5f, id = "p_roots", cls = "boss", name = "樹根爆發", type = SkillType.Projectile, color = C("#78350f"), cost = 0, damage = 18, charge = 1.6f, cooldown = 8f, speed = 1f, radius = 0f, rangeMax = 14f, effect = EffectKind.Snare, effectDur = 1.2f, desc = "你腳下出現紅圈，樹根從地底爆出（受傷並定身 1.2 秒）→ 離開紅圈" });
            P(new SkillDef { id = "p_summon", cls = "boss", name = "召喚植物", type = SkillType.Self, self = SelfKind.Summon, color = C("#4ade80"), cost = 0, charge = 1.8f, cooldown = 16f, multi = 4, desc = "召喚食人花與毒液噴花" });
            P(new SkillDef { id = "m_claw", cls = "minion", name = "小鬼爪擊", type = SkillType.Projectile, color = C("#ff8c42"), cost = 0, damage = 7, charge = 0.7f, cooldown = 1.6f, radius = 0f, rangeMax = 1.3f, desc = "小怪的近身爪擊" });
            Classes["boss_forest"] = new ClassDef { id = "boss_forest", name = "腐化樹王", color = C("#4ade80"), maxHp = 500, maxMp = 999, mpRegen = 100, skills = new[] { "p_thorn", "p_whip", "p_stomp", "p_spore", "p_roots", "p_summon" }, defaultLoadout = new[] { "p_thorn", "p_whip", "p_stomp", "p_spore", "p_roots", "p_summon" }, desc = "森林 Boss" };
            Classes["boss"] = new ClassDef { id = "boss", name = "熔岩巨像", color = C("#ff5a1f"), maxHp = 500, maxMp = 999, mpRegen = 100, skills = new[] { "b_bolt", "b_sweep", "b_slam", "b_rain", "b_leap", "b_summon" }, defaultLoadout = new[] { "b_bolt", "b_sweep", "b_slam", "b_rain", "b_leap", "b_summon" }, desc = "Boss" };
            Classes["minion"] = new ClassDef { id = "minion", name = "熔岩小鬼", color = C("#ff8c42"), maxHp = 40, maxMp = 0, mpRegen = 0, skills = new string[0], defaultLoadout = new string[0], desc = "Boss 召喚的小怪" };

            Classes["priest"] = new ClassDef { id = "priest", name = "牧師", color = C("#fde68a"), maxHp = 105, maxMp = 130, mpRegen = 4, skills = new[] { "holy", "judgement", "prayheal", "cleanse", "guard", "prayer" }, defaultLoadout = new[] { "holy", "prayheal", "guard" }, desc = "回復、淨化、守護、減冷卻；聖光攻擊較弱" };
            Classes["swordsman"] = new ClassDef { id = "swordsman", name = "劍士", color = C("#60a5fa"), maxHp = 130, maxMp = 90, mpRegen = 3, skills = new[] { "slash", "thrust", "wave", "block", "ironwall", "counter" }, defaultLoadout = new[] { "slash", "wave", "block" }, desc = "近遠攻擊都有，多種防禦" };
        }
    }
}
