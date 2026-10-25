using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>精魄的效果種類</summary>
    public enum SoulOp
    {
        Stat,          // 能力值 +v（stat）
        Atk,           // 攻擊傷害 +v（比例，負的＝降低）
        Hp, Mp,        // HP／MP 上限 +v
        Crit,          // 爆擊率 +v
        CritDmg,       // 爆擊傷害 +v（爆擊原本 ×2）
        Evade,         // 閃避率 +v
        Block,         // 格擋率 +v（格擋＝這一下傷害減半）
        Lifesteal,     // 吸血 +v
        Cdr,           // 技能冷卻 -v
        Reduce,        // 受到的傷害 -v
        Taken,         // 受到的傷害 +v（副作用）
        BasicSpeed,    // 普攻集氣 -v（負的＝變慢）
        ChargeSpeed,   // 技能蓄力 -v
        Regen,         // 每秒回復 v×最大 HP（說明寫成每 10 秒）
        MpRegen,       // MP 回復 +v
        Gold, Xp,      // 金幣、經驗 +v
        Burn, Poison, Freeze,   // 命中附加狀態的機率 +v
        Thorns,        // 被打到時，反彈 v 倍的傷害給攻擊者
        HitSnare,      // 命中時 chance 機率定身 dur 秒
        HitCast,       // 命中時 chance 機率發動 skill（Lv1，不花 MP）
        HurtShield,    // 受到攻擊時 chance 機率得到 v 點護盾（dur 秒）
        HurtInvuln,    // 受到攻擊時 chance 機率 dur 秒無敵
        HurtHeal,      // 受到攻擊時 chance 機率回復 v×最大 HP
        KillHeal,      // 打倒敵人時 chance 機率回復 v×最大 HP
        KillMp,        // 打倒敵人時 chance 機率回復 v MP
        Retaliate,     // 被打到時 chance 機率讓攻擊者中毒（v 每秒、dur 秒）
        Undying,       // 每場戰鬥一次：受到致命傷害時留 1 HP、無敵 3 秒
        TrapReduce, DodgePower,
    }

    /// <summary>精魄能鑲在哪一類裝備：武器（傷害）、防具（頭、身體、鞋子；防禦）、飾品（技能、輔助、特殊）</summary>
    public enum SoulPart { Weapon, Armor, Trinket }

    public struct SoulFx
    {
        public SoulOp op; public Stat stat; public float v, chance, dur; public string skill;
    }

    /// <summary>精魄：打倒怪物有機率掉落（每一種怪物一種）；鑲在裝備的精魄孔上生效</summary>
    public class SoulDef
    {
        public string id, name, source;     // source＝哪一種怪物掉的
        public int kind = -1;               // 小怪種類（Battle.MinionNames 的索引）；Boss＝−1
        public string boss;                 // Boss 的職業 id
        public float chance;                // 掉落機率（打倒一隻）
        public Color color;
        public SoulPart part;
        public SoulFx[] fx;
        public bool IsBoss => boss != null;
    }

    public static class Souls
    {
        /// <summary>精魄發動的技能用這個職業標記（命中時不會再觸發精魄，避免連鎖）</summary>
        public const string SoulCls = "soul";
        public const int MaxSlots = 4;

        static SoulFx F(SoulOp op, float v, float chance = 0f, float dur = 0f, string skill = null, Stat stat = Stat.Str) => new SoulFx { op = op, v = v, chance = chance, dur = dur, skill = skill, stat = stat };
        static SoulFx S(Stat s, float v) => F(SoulOp.Stat, v, stat: s);

        public static readonly List<SoulDef> All = new List<SoulDef>();
        static readonly Dictionary<string, SoulDef> byId = new Dictionary<string, SoulDef>();

        static void M(int kind, float chance, string color, params SoulFx[] fx) => Add(new SoulDef { id = "soul_m" + kind, kind = kind, source = Battle.MinionNames[kind], name = Battle.MinionNames[kind] + "精魄", chance = chance, color = Skills.C(color), fx = fx });
        static void B(string boss, string color, params SoulFx[] fx)
        {
            string n = Skills.Classes.TryGetValue(boss, out var cd) ? cd.name : boss;
            Add(new SoulDef { id = "soul_" + boss, boss = boss, source = n, name = n + "之魂", chance = BossChance, color = Skills.C(color), fx = fx });
        }
        static void Add(SoulDef d) { All.Add(d); byId[d.id] = d; }

        /// <summary>Boss 掉落精魄的機率（0.5%；效果比小怪強）</summary>
        public const float BossChance = 0.005f;

        static Souls()
        {
            // 每一顆精魄只有一種效果（使用者要求）：加能力值／特殊能力值、或機率附加狀態、或機率發動技能、或機率好處、或「好處＋副作用」其中一種
            // ---- 小怪（0.1%～0.5%）：越常見的越難掉
            M(0, 0.001f, "#ff7a3d", S(Stat.Str, 3));                                         // 熔岩小鬼：能力值
            M(1, 0.003f, "#ff9f43", F(SoulOp.HitCast, 0, 0.04f, skill: "fire"));             // 火焰射手：機率發動法師的火球術
            M(2, 0.002f, "#b08968", F(SoulOp.Hp, 40), F(SoulOp.BasicSpeed, -0.10f));        // 岩石衝鋒獸：好處＋副作用（普攻變慢）
            M(3, 0.002f, "#ff4d4d", F(SoulOp.Atk, 0.12f), F(SoulOp.Taken, 0.08f));           // 自爆小鬼：好處＋副作用（更怕痛）
            M(4, 0.003f, "#a8a29e", F(SoulOp.Block, 0.06f));                                  // 岩石投擲者：格擋率
            M(5, 0.004f, "#f97316", F(SoulOp.CritDmg, 0.20f));                                // 熔岩狙擊手：爆擊傷害
            M(6, 0.005f, "#9d6bff", F(SoulOp.HurtInvuln, 0, 0.06f, 1f));                     // 虛空術士：機率好處（虛空閃現）
            M(7, 0.002f, "#84cc16", F(SoulOp.Poison, 0.05f));                                 // 毒液噴花：機率附加中毒
            M(8, 0.003f, "#65a30d", F(SoulOp.Thorns, 0.15f));                                 // 荊棘衝鋒獸：反彈
            M(9, 0.004f, "#22c55e", F(SoulOp.HitSnare, 0, 0.04f, 0.8f));                      // 藤蔓術士：機率定身
            M(10, 0.002f, "#e11d48", F(SoulOp.Lifesteal, 0.02f));                              // 食人花：吸血
            M(11, 0.003f, "#c4b5fd", F(SoulOp.KillHeal, 0.08f, 0.25f));                        // 孢子菇：機率好處（擊殺回血）
            M(12, 0.004f, "#a3e635", F(SoulOp.HitCast, 0, 0.03f, skill: "quickshot"));        // 種子槍手：機率發動獵人的速射
            M(13, 0.002f, "#38bdf8", F(SoulOp.Evade, 0.04f));                                  // 跳躍魚：閃避率
            M(14, 0.003f, "#e0f2fe", S(Stat.Agi, 5), F(SoulOp.Hp, -20));                      // 俯衝海鳥：好處＋副作用（HP 少一點）
            M(15, 0.004f, "#7dd3fc", F(SoulOp.HurtShield, 25, 0.08f, 4f));                    // 騎魚泡泡怪：機率好處（護盾）
            M(16, 0.003f, "#f59e0b", F(SoulOp.Gold, 0.08f), F(SoulOp.Atk, -0.04f));           // 寄居蟹：好處＋副作用（攻擊弱一點）
            M(17, 0.005f, "#facc15", F(SoulOp.HitCast, 0, 0.04f, skill: "thunder"));          // 雷電水母：機率發動法師的雷擊
            M(18, 0.004f, "#fde68a", F(SoulOp.Retaliate, 4f, 0.10f, 4f));                     // 河豚：被打時機率讓攻擊者中毒
            // ---- 第二～五章 x-4、x-5 的稀有怪（2%；一場只會出現幾隻，效果比一般小怪強約一倍）
            M(19, 0.02f, "#a3e635", F(SoulOp.Hp, 150));                                        // 古根撼地獸：HP 上限
            M(20, 0.02f, "#38bdf8", F(SoulOp.Cdr, 0.10f));                                      // 潮鞭鰻：技能冷卻
            M(21, 0.02f, "#f59e0b", F(SoulOp.CritDmg, 0.40f));                                  // 鏈錘鬥奴：爆擊傷害
            M(22, 0.02f, "#d97706", F(SoulOp.Reduce, 0.02f));                                   // 裂岩甲蜥：受到的傷害 −2%（使用者要求：精魄的減傷 2%）
            // ---- Boss（0.5%）：一樣只有一種效果，但更強、更特殊
            B("boss", "#ff5a1f", F(SoulOp.HitCast, 0, 0.05f, skill: "meteor"));               // 熔岩巨像：機率發動隕石
            B("boss_forest", "#4ade80", F(SoulOp.Regen, 0.001f));                              // 腐化樹王：每 10 秒回 1% HP
            B("boss_sea", "#22d3ee", F(SoulOp.HurtShield, 60, 0.12f, 5f));                    // 深淵海妖：機率大護盾
            B("boss_arena", "#d4a24c", F(SoulOp.Undying, 0));                                  // 青銅鬥魂：每場一次不死
            B("boss_canyon", "#c8a165", F(SoulOp.Atk, 0.25f), F(SoulOp.Taken, 0.12f));        // 黃土岩魔：好處＋副作用（攻擊很強，但更怕痛）
            foreach (var e in EliteCatalog.All)
            {
                SoulFx fx = e.role == 0 ? F(SoulOp.Block,.08f) : e.role == 1 ? F(SoulOp.CritDmg,.25f) : e.role == 2 ? F(SoulOp.HitSnare,0,.06f,.8f) : e.role == 3 ? F(SoulOp.KillHeal,.08f,.30f) : F(SoulOp.BasicSpeed,.10f);
                M(e.kind,.01f,ColorUtility.ToHtmlStringRGB(e.color).Insert(0,"#"),fx);
            }
            M(48,.005f,"#d6ae67",F(SoulOp.CritDmg,.15f));
            M(49,.005f,"#aa8854",F(SoulOp.TrapReduce,.20f));
            M(50,.005f,"#38dddd",F(SoulOp.DodgePower,.15f));
            M(51,.005f,"#a5b0b9",F(SoulOp.Block,.06f));
            M(52,.005f,"#91c9e2",F(SoulOp.Evade,.05f));
            AssignParts();
        }

        /// <summary>武器類（傷害）、飾品類（技能、輔助、特殊）；其他都是防具類（防禦）</summary>
        static readonly string[] WeaponSouls = { "soul_m0", "soul_m3", "soul_m5", "soul_m7", "soul_m9", "soul_m10", "soul_m21", "soul_boss_canyon" };
        static readonly string[] TrinketSouls = { "soul_m1", "soul_m11", "soul_m12", "soul_m14", "soul_m16", "soul_m17", "soul_m20", "soul_boss", "soul_boss_forest" };
        static void AssignParts()
        {
            foreach (var d in All)
                d.part = EliteCatalog.IsElite(d.kind) ? (EliteCatalog.Get(d.kind).role == 1 || EliteCatalog.Get(d.kind).role == 4 ? SoulPart.Weapon : EliteCatalog.Get(d.kind).role == 0 ? SoulPart.Armor : SoulPart.Trinket) : System.Array.IndexOf(WeaponSouls, d.id) >= 0 ? SoulPart.Weapon : System.Array.IndexOf(TrinketSouls, d.id) >= 0 ? SoulPart.Trinket : SoulPart.Armor;
        }
        public static readonly string[] PartNames = { "武器", "防具", "飾品" };
        public static readonly string[] PartIcons = { "⚔", "🛡", "💍" };
        /// <summary>這個部位的裝備能鑲哪一類精魄</summary>
        public static SoulPart PartOf(Slot slot) => slot == Slot.Weapon ? SoulPart.Weapon : slot == Slot.Trinket ? SoulPart.Trinket : SoulPart.Armor;
        public static string PartText(SoulPart p) => p == SoulPart.Weapon ? "⚔ 武器" : p == SoulPart.Trinket ? "💍 飾品" : "🛡 防具（頭、身體、鞋子）";

        public static SoulDef Get(string id) => id != null && byId.TryGetValue(id, out var d) ? d : null;
        public static SoulDef OfMinion(int kind) => All.Find(d => d.kind == kind);
        public static SoulDef OfBoss(string cls) => All.Find(d => d.boss == cls);

        // ---------------------------------------------------------------- 說明
        static string Pct(float v) => $"{v * 100f:0.#}%";
        /// <summary>一個效果的說明；副作用（負的能力值、攻擊、HP、MP）前面加 ⚠</summary>
        public static string FxText(SoulFx x) =>
            (x.v < 0 && (x.op == SoulOp.Stat || x.op == SoulOp.Atk || x.op == SoulOp.Hp || x.op == SoulOp.Mp) ? "⚠ " : "") + FxBody(x);
        static string FxBody(SoulFx x)
        {
            string skillName = x.skill != null && Skills.All.TryGetValue(x.skill, out var s) ? s.name : x.skill;
            string sg(float v) => v >= 0 ? "+" : "−";
            switch (x.op)
            {
                case SoulOp.TrapReduce: return $"陷阱傷害 -{x.v*100:0}%";
                case SoulOp.DodgePower: return $"跳／蹲閃避後3秒傷害 +{x.v*100:0}%";
                case SoulOp.Stat: return $"{StatInfo.Names[(int)x.stat]} {sg(x.v)}{Mathf.Abs(x.v):0}";
                case SoulOp.Atk: return $"攻擊傷害 {sg(x.v)}{Pct(Mathf.Abs(x.v))}";
                case SoulOp.Hp: return $"HP 上限 {sg(x.v)}{Mathf.Abs(x.v):0}";
                case SoulOp.Mp: return $"MP 上限 {sg(x.v)}{Mathf.Abs(x.v):0}";
                case SoulOp.Crit: return $"爆擊率 +{Pct(x.v)}";
                case SoulOp.CritDmg: return $"爆擊傷害 +{Pct(x.v)}";
                case SoulOp.Evade: return $"閃避率 +{Pct(x.v)}";
                case SoulOp.Block: return $"格擋率 +{Pct(x.v)}（格擋＝這一下傷害減半）";
                case SoulOp.Lifesteal: return $"吸血 +{Pct(x.v)}";
                case SoulOp.Cdr: return $"技能冷卻 −{Pct(x.v)}";
                case SoulOp.Reduce: return $"受到的傷害 −{Pct(x.v)}";
                case SoulOp.Taken: return $"⚠ 受到的傷害 +{Pct(x.v)}";
                case SoulOp.BasicSpeed: return x.v >= 0 ? $"普攻集氣 −{Pct(x.v)}（更快）" : $"⚠ 普攻集氣 +{Pct(-x.v)}（更慢）";
                case SoulOp.ChargeSpeed: return $"技能蓄力 −{Pct(x.v)}";
                case SoulOp.Regen: return $"每 10 秒回復 {Pct(x.v * 10f)} HP";
                case SoulOp.MpRegen: return $"MP 回復 +{Pct(x.v)}";
                case SoulOp.Gold: return $"金幣 +{Pct(x.v)}";
                case SoulOp.Xp: return $"經驗 +{Pct(x.v)}";
                case SoulOp.Burn: return $"命中 {Pct(x.v)} 機率灼燒";
                case SoulOp.Poison: return $"命中 {Pct(x.v)} 機率中毒";
                case SoulOp.Freeze: return $"命中 {Pct(x.v)} 機率冰凍";
                case SoulOp.Thorns: return $"被打到時反彈 {Pct(x.v)} 傷害";
                case SoulOp.HitSnare: return $"命中 {Pct(x.chance)} 機率定身 {x.dur:0.#} 秒";
                case SoulOp.HitCast: return $"命中 {Pct(x.chance)} 機率發動「{skillName}」Lv1（不花 MP）";
                case SoulOp.HurtShield: return $"受到攻擊 {Pct(x.chance)} 機率得到 {x.v:0} 點護盾（{x.dur:0} 秒）";
                case SoulOp.HurtInvuln: return $"受到攻擊 {Pct(x.chance)} 機率 {x.dur:0.#} 秒無敵（虛空閃現）";
                case SoulOp.HurtHeal: return $"受到攻擊 {Pct(x.chance)} 機率回復 {Pct(x.v)} HP";
                case SoulOp.KillHeal: return $"打倒敵人 {Pct(x.chance)} 機率回復 {Pct(x.v)} HP";
                case SoulOp.KillMp: return $"打倒敵人 {Pct(x.chance)} 機率回復 {x.v:0} MP";
                case SoulOp.Retaliate: return $"被打到時 {Pct(x.chance)} 機率讓攻擊者中毒（每秒 {x.v:0}、{x.dur:0} 秒）";
                case SoulOp.Undying: return "每場戰鬥一次：受到致命傷害時留 1 HP，3 秒無敵";
            }
            return "";
        }
        public static string Desc(SoulDef d)
        {
            var sb = new StringBuilder();
            foreach (var x in d.fx) { if (sb.Length > 0) sb.Append('、'); sb.Append(FxText(x)); }
            return sb.ToString();
        }
        public static string DropText(SoulDef d) => $"{PartIcons[(int)d.part]}{PartNames[(int)d.part]}用・" + (d.IsBoss ? $"{d.source}（Boss）・掉落率 {d.chance * 100f:0.#}%" : $"{d.source}・掉落率 {d.chance * 100f:0.#}%");

        // ---------------------------------------------------------------- 精魄孔
        /// <summary>新裝備的精魄孔數：0～4，越稀有上限越高（普通最多 1、優良 2、稀有 3、史詩 4、傳說 1～4）</summary>
        public static int RollSlots(System.Random rng, int rarity)
        {
            int cap = Mathf.Clamp(rarity + 1, 1, MaxSlots);
            int n = rng.Next(0, cap + 1);
            if (rarity >= 4) n = Mathf.Max(n, rng.Next(1, MaxSlots + 1));
            return Mathf.Clamp(n, 0, MaxSlots);
        }
        /// <summary>舊存檔的裝備（沒有記孔數）：用 uid 固定算出一個孔數（每次讀檔都一樣）</summary>
        public static int SlotsFromUid(long uid, int rarity) => RollSlots(new System.Random((int)(uid ^ (uid >> 32))), rarity);

        // ---------------------------------------------------------------- 套用到角色
        static IEnumerable<SoulFx> Equipped(CharacterProfile p)
        {
            foreach (var it in p.equipped)
                if (it != null)
                    foreach (var id in it.souls) { var d = Get(id); if (d != null) foreach (var x in d.fx) yield return x; }
        }
        /// <summary>身上所有精魄的某一種效果加總</summary>
        public static float Sum(CharacterProfile p, SoulOp op) { float v = 0f; foreach (var x in Equipped(p)) if (x.op == op) v += x.v; return v; }
        /// <summary>一件裝備上的精魄加了多少能力值</summary>
        public static float ItemStat(Item it, Stat s) { float v = 0f; if (it != null) foreach (var id in it.souls) { var d = Get(id); if (d != null) foreach (var x in d.fx) if (x.op == SoulOp.Stat && x.stat == s) v += x.v; } return v; }
        public static int StatSum(CharacterProfile p, Stat s) { float v = 0f; foreach (var x in Equipped(p)) if (x.op == SoulOp.Stat && x.stat == s) v += x.v; return Mathf.RoundToInt(v); }

        /// <summary>CharacterProfile.ApplyTo 最後呼叫（能力值的部分已經算在 Total 裡）</summary>
        public static void ApplyTo(Fighter f, CharacterProfile p)
        {
            f.soulProcs.Clear(); f.blockChance = 0f; f.soulTaken = 1f; f.thorns = 0f;
            float hpRatio = f.maxHp > 0 ? f.hp / f.maxHp : 1f, mpRatio = f.maxMp > 0 ? f.mp / f.maxMp : 1f;
            foreach (var x in Equipped(p))
            {
                switch (x.op)
                {
                    case SoulOp.TrapReduce: f.trapReduce=Mathf.Clamp01(f.trapReduce+x.v);break;
                    case SoulOp.DodgePower: f.evadeBoost+=x.v;break;
                    case SoulOp.Atk: f.atkMul *= Mathf.Max(0.2f, 1f + x.v); break;
                    case SoulOp.Hp: f.maxHp = Mathf.Max(10, f.maxHp + Mathf.RoundToInt(x.v)); break;
                    case SoulOp.Mp: f.maxMp = Mathf.Max(10, f.maxMp + Mathf.RoundToInt(x.v)); break;
                    case SoulOp.Crit: f.critChance = Mathf.Clamp(f.critChance + x.v, 0f, 0.75f); break;
                    case SoulOp.CritDmg: f.critDmg += x.v; break;
                    case SoulOp.Evade: f.evadeChance = Mathf.Clamp(f.evadeChance + x.v, 0f, 0.5f); break;
                    case SoulOp.Block: f.blockChance = Mathf.Min(0.5f, f.blockChance + x.v); break;
                    case SoulOp.Lifesteal: f.lifesteal += x.v; break;
                    case SoulOp.Cdr: f.cdMul = Mathf.Max(0.4f, f.cdMul * (1f - x.v)); break;
                    case SoulOp.Reduce: f.gearReduce = Mathf.Min(Fighter.MaxReduce, f.gearReduce + x.v); f.magicReduce = Mathf.Min(Fighter.MaxReduce, f.magicReduce + x.v); break;
                    case SoulOp.Taken: f.soulTaken *= 1f + x.v; break;
                    case SoulOp.BasicSpeed: f.basicCdMul = Mathf.Clamp(f.basicCdMul * (1f - x.v), 0.4f, 2f); break;
                    case SoulOp.ChargeSpeed: f.chargeScale = Mathf.Max(0.4f, f.chargeScale * (1f - x.v)); break;
                    case SoulOp.Regen: f.hpRegen += f.maxHp * x.v; break;
                    case SoulOp.MpRegen: f.regen *= 1f + x.v; break;
                    case SoulOp.Burn: f.burnChance += x.v; break;
                    case SoulOp.Poison: f.poisonChance += x.v; break;
                    case SoulOp.Freeze: f.freezeChance += x.v; break;
                    case SoulOp.Thorns: f.thorns += x.v; break;
                    case SoulOp.Undying: f.undyingUntil = float.MaxValue; break;
                    case SoulOp.HitSnare: case SoulOp.HitCast: case SoulOp.HurtShield: case SoulOp.HurtInvuln:
                    case SoulOp.HurtHeal: case SoulOp.KillHeal: case SoulOp.KillMp: case SoulOp.Retaliate:
                        f.soulProcs.Add(x); break;
                }
            }
            f.hp = Mathf.Clamp(f.maxHp * hpRatio, 1f, f.maxHp); f.mp = Mathf.Clamp(f.maxMp * mpRatio, 0f, f.maxMp);
        }

        // ---------------------------------------------------------------- 背包裡的精魄（每個角色各自的；還沒鑲上去的）
        public static string SerializeBag(Dictionary<string, int> bag)
        {
            var sb = new StringBuilder();
            foreach (var kv in bag) if (kv.Value > 0) { if (sb.Length > 0) sb.Append(','); sb.Append(kv.Key).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture)); }
            return sb.ToString();
        }
        public static void ParseBag(string s, Dictionary<string, int> bag)
        {
            bag.Clear();
            if (string.IsNullOrEmpty(s)) return;
            foreach (var part in s.Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && Get(kv[0]) != null && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0) bag[kv[0]] = n;
            }
        }

        /// <summary>把背包裡的精魄鑲到裝備的空孔上（成功＝true）。鑲上去就不能取出（跟著裝備走：賣掉、丟掉就沒了）</summary>
        public static bool Socket(CharacterProfile p, Item it, string id, out string why)
        {
            why = null;
            if (it == null || Get(id) == null) { why = "沒有這個精魄"; return false; }
            var d = Get(id);
            if (d.part != PartOf(it.slot)) { why = $"{d.name}只能鑲在{PartText(d.part)}上"; return false; }
            if (it.souls.Count >= it.soulSlots) { why = it.soulSlots == 0 ? "這件裝備沒有精魄孔" : "精魄孔都滿了（先取出一個）"; return false; }
            if (!p.souls.TryGetValue(id, out int n) || n <= 0) { why = "背包裡沒有這個精魄"; return false; }
            if (n == 1) p.souls.Remove(id); else p.souls[id] = n - 1;
            it.souls.Add(id);
            return true;
        }
    }
}
