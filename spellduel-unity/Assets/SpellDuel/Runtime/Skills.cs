using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    public enum SkillType { Projectile, Self, Trap }
    public enum SelfKind { None, Heal, Block, Shield, Counter }
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
        public bool releaseNear;        // 距離不在射程內時不能出手（保留蓄力，靠近再放）
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
        public static readonly string[] ClassOrder = { "archer", "mage", "assassin", "swordsman" };

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
            // ---------------------------------------------------------- 弓箭手：蓄力長、箭速快、遠距離、判定小；近距離只有速射
            P(new SkillDef { id = "quickshot", cls = "archer", name = "速射", type = SkillType.Projectile, color = C("#d9f99d"), cost = 12, damage = 12, charge = 1.0f, cooldown = 0.9f, speed = 14f, radius = 0.05f, rangeMax = 12f, desc = "唯一的近距離技能，貼身也能射" });
            P(new SkillDef { id = "snipe", cls = "archer", name = "狙擊", type = SkillType.Projectile, color = C("#fde047"), cost = 30, damage = 34, charge = 2.2f, cooldown = 3f, speed = 16f, radius = 0.04f, rangeMin = 3f, rangeMax = 15f, desc = "蓄力最久的重箭，判定最小；3m 內打不到" });
            P(new SkillDef { id = "triple", cls = "archer", name = "三連矢", type = SkillType.Projectile, color = C("#a3e635"), cost = 24, damage = 9, charge = 1.6f, cooldown = 2f, speed = 12f, radius = 0.05f, rangeMin = 2f, rangeMax = 10f, multi = 3, spreadDeg = 8f, desc = "三箭扇形散開；2m 內打不到" });
            P(new SkillDef { id = "snaretrap", cls = "archer", name = "捕獸夾", type = SkillType.Trap, color = C("#fb923c"), cost = 18, damage = 12, charge = 1.2f, cooldown = 4f, radius = 0.35f, trapArm = 1.2f, effect = EffectKind.Snare, effectDur = 2.5f, desc = "設在自己 3m 內的地上，踩到受傷並定身 2.5 秒" });
            P(new SkillDef { id = "blasttrap", cls = "archer", name = "爆裂陷阱", type = SkillType.Trap, color = C("#f87171"), cost = 28, damage = 28, charge = 1.5f, cooldown = 6f, radius = 0.45f, trapArm = 1.5f, desc = "設在自己 3m 內的地上，踩到爆炸" });

            // ---------------------------------------------------------- 法師：攻擊力最強、蓄力最長；近距離只有風刃
            P(new SkillDef { id = "fire", cls = "mage", name = "火球術", type = SkillType.Projectile, color = C("#ff7a1a"), cost = 20, damage = 24, charge = 1.5f, cooldown = 1.2f, speed = 4f, radius = 0.15f, rangeMin = 2f, rangeMax = 6f, desc = "中速火球；2m 內打不到" });
            P(new SkillDef { id = "ice", cls = "mage", name = "冰槍", type = SkillType.Projectile, color = C("#5fd7ff"), cost = 25, damage = 28, charge = 1.4f, cooldown = 1.5f, speed = 6f, radius = 0.09f, rangeMin = 2f, rangeMax = 7f, desc = "快速穿刺，判定較窄；2m 內打不到" });
            P(new SkillDef { id = "thunder", cls = "mage", name = "雷擊", type = SkillType.Projectile, color = C("#c08bff"), cost = 35, damage = 38, charge = 2.2f, cooldown = 2.5f, speed = 14f, radius = 0.07f, rangeMin = 3f, rangeMax = 10f, desc = "幾乎瞬發，判定極窄；3m 內打不到" });
            P(new SkillDef { id = "wind", cls = "mage", name = "風刃", type = SkillType.Projectile, color = C("#7dffb0"), cost = 12, damage = 16, charge = 1.0f, cooldown = 0.8f, speed = 5f, radius = 0.25f, rangeMax = 2.5f, desc = "近距離；隨機從左或右側繞弧線飛向目標" });
            P(new SkillDef { id = "meteor", cls = "mage", name = "隕石", type = SkillType.Projectile, color = C("#ff3b3b"), cost = 50, damage = 58, charge = 3.0f, cooldown = 4f, speed = 7f, radius = 0.3f, rangeMin = 3f, rangeMax = 8f, desc = "從敵人正上方砸下（可閃），落地留下直徑 3m 燃燒 10 秒的火海；3m 內打不到" });
            P(new SkillDef { id = "heal", cls = "mage", name = "治癒", type = SkillType.Self, self = SelfKind.Heal, color = C("#3dff7a"), cost = 30, heal = 25, charge = 1.5f, cooldown = 5f, desc = "回復 25 HP" });

            // ---------------------------------------------------------- 刺客：遠處蓄力、靠近才能出手、判定小
            P(new SkillDef { id = "backstab", cls = "assassin", name = "背刺", type = SkillType.Projectile, color = C("#e879f9"), cost = 25, damage = 42, charge = 1.3f, cooldown = 2.5f, speed = 10f, radius = 0.08f, rangeMax = 1.5f, releaseNear = true, desc = "1.5m 內才能出手" });
            P(new SkillDef { id = "shadow", cls = "assassin", name = "影襲", type = SkillType.Projectile, color = C("#a78bfa"), cost = 20, damage = 28, charge = 1.0f, cooldown = 1.5f, speed = 9f, radius = 0.08f, rangeMax = 2.5f, releaseNear = true, desc = "2.5m 內才能出手" });
            P(new SkillDef { id = "poison", cls = "assassin", name = "毒刃", type = SkillType.Projectile, color = C("#4ade80"), cost = 22, damage = 12, charge = 1.1f, cooldown = 3f, speed = 9f, radius = 0.08f, rangeMax = 2f, releaseNear = true, effect = EffectKind.Dot, effectDps = 4f, effectDur = 5f, desc = "2m 內出手，中毒 5 秒（每秒 4）" });
            P(new SkillDef { id = "knife", cls = "assassin", name = "飛刀", type = SkillType.Projectile, color = C("#cbd5e1"), cost = 10, damage = 10, charge = 0.7f, cooldown = 0.8f, speed = 10f, radius = 0.05f, rangeMax = 5f, desc = "遠程牽制" });
            P(new SkillDef { id = "smoke", cls = "assassin", name = "煙霧彈", type = SkillType.Projectile, color = C("#9ca3af"), cost = 18, damage = 0, charge = 0.6f, cooldown = 8f, speed = 5f, radius = 0.25f, rangeMax = 6f, effect = EffectKind.Blind, effectDur = 4f, desc = "命中後對手視線被遮住 4 秒" });

            // ---------------------------------------------------------- 劍士：近遠攻擊都有、多種防禦
            P(new SkillDef { id = "slash", cls = "swordsman", name = "斬擊", type = SkillType.Projectile, color = C("#f8fafc"), cost = 15, damage = 26, charge = 0f, cooldown = 1f, speed = 8f, radius = 0.3f, rangeMax = 2f, desc = "近身大範圍橫斬；不用蓄力，手刀劈過就出招" });
            P(new SkillDef { id = "thrust", cls = "swordsman", name = "突刺", type = SkillType.Projectile, color = C("#fcd34d"), cost = 15, damage = 22, charge = 0.7f, cooldown = 1.2f, speed = 9f, radius = 0.08f, rangeMax = 3f, desc = "中距離直刺" });
            P(new SkillDef { id = "wave", cls = "swordsman", name = "劍氣", type = SkillType.Projectile, color = C("#93c5fd"), cost = 20, damage = 16, charge = 0f, cooldown = 1.5f, speed = 6f, radius = 0.2f, rangeMax = 7f, desc = "遠程劍氣；不用蓄力，手刀劈過就出招" });
            P(new SkillDef { id = "block", cls = "swordsman", name = "格擋", type = SkillType.Self, self = SelfKind.Block, color = C("#60a5fa"), cost = 12, charge = 0.2f, cooldown = 4f, buffDur = 3f, buffReduce = 0.7f, desc = "3 秒內下一次傷害 -70%" });
            P(new SkillDef { id = "ironwall", cls = "swordsman", name = "鐵壁", type = SkillType.Self, self = SelfKind.Shield, color = C("#94a3b8"), cost = 30, charge = 0.6f, cooldown = 12f, buffDur = 8f, buffAmount = 35, desc = "8 秒護盾，吸收 35 傷害" });
            P(new SkillDef { id = "counter", cls = "swordsman", name = "反擊", type = SkillType.Self, self = SelfKind.Counter, color = C("#f472b6"), cost = 18, charge = 0.2f, cooldown = 6f, buffDur = 1.5f, desc = "1.5 秒內被打中：無傷，並把攻擊打回去" });

            Classes["archer"] = new ClassDef { id = "archer", name = "弓箭手", color = C("#a3e635"), maxHp = 100, maxMp = 100, mpRegen = 7, skills = new[] { "quickshot", "snipe", "triple", "snaretrap", "blasttrap" }, defaultLoadout = new[] { "quickshot", "snipe", "snaretrap" }, desc = "箭速快、射程遠；近距離只有速射；可設陷阱" };
            Classes["mage"] = new ClassDef { id = "mage", name = "法師", color = C("#c084fc"), maxHp = 90, maxMp = 120, mpRegen = 7, skills = new[] { "fire", "ice", "thunder", "wind", "meteor", "heal" }, defaultLoadout = new[] { "fire", "wind", "meteor" }, desc = "攻擊力最強、蓄力最長；近距離只有風刃" };
            Classes["assassin"] = new ClassDef { id = "assassin", name = "刺客", color = C("#e879f9"), maxHp = 90, maxMp = 100, mpRegen = 8, skills = new[] { "backstab", "shadow", "poison", "knife", "smoke" }, defaultLoadout = new[] { "backstab", "poison", "smoke" }, desc = "遠處蓄力、靠近才能出手" };
            Classes["swordsman"] = new ClassDef { id = "swordsman", name = "劍士", color = C("#60a5fa"), maxHp = 130, maxMp = 90, mpRegen = 6, skills = new[] { "slash", "thrust", "wave", "block", "ironwall", "counter" }, defaultLoadout = new[] { "slash", "wave", "block" }, desc = "近遠攻擊都有，多種防禦" };
        }
    }
}
