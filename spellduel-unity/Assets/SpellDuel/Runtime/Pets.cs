using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 寵物系統的資料（設計：Docs/PET_SYSTEM_PROPOSAL.md，含使用者定案修改）：
    ///   44 種可收服的怪（一般 0～18、菁英 23～47），每種一個專屬誘餌、一個技能池（8 系 × 4 招）；
    ///   3 種飼料（加經驗、機率學新招）；等級 1～50（出戰時不超過主人等級）；菁英 HP ×1.5、攻擊 ×1.3；
    ///   出戰拿主人本場經驗的 40%；每個城鎮只賣該章怪物的誘餌。
    /// 戰鬥邏輯在 Combat.Pets.cs，畫面在 CharacterPanel.Pets.cs、SoloBattle.Pets.cs。
    /// </summary>
    public enum PetPool { Fire, Rock, Shadow, Plant, Wing, Tide, Thunder, Shell }

    public sealed class PetSpecies
    {
        public int kind;                 // 小怪種類（Battle.MinionNames／EliteCatalog）
        public PetPool pool;
        public string start;             // 一開始會的技能 id
        public string bait, baitName;    // 誘餌 id、名字
        public int price;                // 一般：商店價；菁英：調配金幣
        public int chapter;              // 第幾章（0～4）：一般＝哪個城鎮賣；菁英＝EliteCatalog 的章
        public bool elite;
        public string Name => kind >= 0 && kind < Battle.MinionNames.Length ? Battle.MinionNames[kind] : "?";
    }

    public enum PetFx { Melee, Ranged, Burn, SelfAoe, SelfShield, SelfReduce, Snare, Leech, ZoneDot, Double, Line, Slow, Heal, OwnerShield, Dash }

    public sealed class PetSkill
    {
        public string id, name; public PetPool pool; public PetFx fx;
        public float mul, range, cd, extra, dur;   // mul＝攻擊倍率（或護盾／治療比例）；extra＝附加數值（灼燒每秒、減速比例、定身秒數…）
        public bool magic;
        public string Desc => fx switch
        {
            PetFx.Melee => $"近身單體 {mul:0.#}倍攻擊",
            PetFx.Ranged => $"{range:0}m 單體 {mul:0.#}倍攻擊",
            PetFx.Burn => $"{range:0}m 單體 {mul:0.#}倍，附灼燒 {dur:0} 秒",
            PetFx.SelfAoe => $"自身 {range:0.#}m 內敵人各 {mul:0.#}倍",
            PetFx.SelfShield => $"HP 低於 70% 時給自己 {mul * 100:0}% HP 的護盾 {dur:0} 秒",
            PetFx.SelfReduce => $"被敵人盯上時，自己受到的傷害 −{mul * 100:0}%（{dur:0} 秒）",
            PetFx.Snare => $"{range:0}m 單體 {mul:0.#}倍，定身 {extra:0.#} 秒",
            PetFx.Leech => $"近身單體 {mul:0.#}倍，回復自己 {extra:0.##}倍攻擊的 HP",
            PetFx.ZoneDot => $"{range:0}m 內目標周圍每秒 {mul:0.##}倍，持續 {dur:0} 秒",
            PetFx.Double => $"{range:0}m 同一目標連射 2 發，各 {mul:0.##}倍",
            PetFx.Line => $"往 {range:0}m 內目標發一道直線，路上敵人各 {mul:0.#}倍",
            PetFx.Slow => $"{range:0}m 單體 {mul:0.#}倍，減速 {extra * 100:0}%（{dur:0} 秒）",
            PetFx.Heal => $"回復自己或 3m 內主人 {mul:0.#}倍攻擊的 HP（選血量比例最低、低於 70% 的）",
            PetFx.OwnerShield => $"主人 HP 低於 70% 時，給 3m 內主人寵物 {mul * 100:0}% HP 的護盾 {dur:0} 秒",
            PetFx.Dash => $"往 {range:0}m 內敵人短衝 {mul:0.#}倍，減速 {extra * 100:0}%",
            _ => "",
        } + $"・冷卻 {cd:0} 秒";
    }

    public sealed class PetFood { public string id, name, icon, desc; public int xp, price; public float learn; }

    /// <summary>一隻養的寵物（存檔）</summary>
    public sealed class PetData
    {
        public long uid; public int kind, level = 1, xp; public string nick;
        public readonly List<string> skills = new List<string>();
        public string pendingLearn;      // 學滿 3 招時抽到的新招（等玩家選替換或放棄；關掉畫面也保留）
        public PetSpecies Species => Pets.Get(kind);
        public string Name => string.IsNullOrEmpty(nick) ? Species?.Name ?? "?" : nick;
    }

    public static class Pets
    {
        public const int MaxLevel = 50, MaxSkills = 3, MaxRoster = 30;
        public const float XpShare = 0.4f, EliteHp = 1.5f, EliteAtk = 1.3f, CaptureHp = 0.3f, LureTime = 3f;
        public const float BaitDrop = 0.08f, EliteBaitDrop = 0.015f, SealDrop = 0.2f;
        public const int SealsPerEliteBait = 8;
        public const string SealId = "mat_tame_seal";

        // ---------------------------------------------------------------- 44 種
        public static readonly List<PetSpecies> All = new List<PetSpecies>();
        static readonly Dictionary<int, PetSpecies> byKind = new Dictionary<int, PetSpecies>();
        public static PetSpecies Get(int kind) => byKind.TryGetValue(kind, out var s) ? s : null;
        public static PetSpecies ByBait(string bait) { foreach (var s in All) if (s.bait == bait) return s; return null; }
        public static bool Capturable(int kind) => byKind.ContainsKey(kind);

        static void N(int kind, string bait, int price, PetPool pool, string start, int chapter) => Add(new PetSpecies { kind = kind, baitName = bait, price = price, pool = pool, start = start, chapter = chapter });
        static void E(int kind, int chapter, string bait, int gold, PetPool pool, string start) => Add(new PetSpecies { kind = kind, baitName = bait, price = gold, pool = pool, start = start, chapter = chapter, elite = true });
        static void Add(PetSpecies s) { s.bait = "bait_m" + s.kind; All.Add(s); byKind[s.kind] = s; }

        // ---------------------------------------------------------------- 技能池（8 系 × 4 招）
        public static readonly Dictionary<string, PetSkill> Skills = new Dictionary<string, PetSkill>();
        static void K(string id, string name, PetPool pool, PetFx fx, float mul, float range, float cd, float extra = 0f, float dur = 0f, bool magic = false)
            => Skills[id] = new PetSkill { id = id, name = name, pool = pool, fx = fx, mul = mul, range = range, cd = cd, extra = extra, dur = dur, magic = magic };
        public static List<PetSkill> PoolOf(PetPool p) { var l = new List<PetSkill>(); foreach (var s in Skills.Values) if (s.pool == p) l.Add(s); return l; }
        public static PetSkill Skill(string id) => id != null && Skills.TryGetValue(id, out var s) ? s : null;

        // ---------------------------------------------------------------- 飼料
        public static readonly PetFood[] Foods =
        {
            new PetFood { id = "food_basic", name = "旅伴糧", icon = "🥣", xp = 80, learn = 0.08f, price = 120, desc = "城鎮買得到；故事關卡過關有 20% 機率掉 1 份" },
            new PetFood { id = "food_fine", name = "精養糧", icon = "🍯", xp = 320, learn = 0.25f, price = 300, desc = "副本通關 25% 機率掉；或旅伴糧 3 份＋300 金調配" },
            new PetFood { id = "food_star", name = "星露糧", icon = "✨", xp = 1000, learn = 0.55f, price = 800, desc = "副本通關 8% 機率掉；或精養糧 4 份＋馴獸印片 2＋800 金調配" },
        };
        public static PetFood Food(string id) => Array.Find(Foods, f => f.id == id);

        // 每一系的基礎能力：HP1、每級 HP、ATK1、每級 ATK、普攻間隔、普攻射程
        static readonly float[,] PoolStats =
        {
            { 70, 7, 8, 0.8f, 2f, 2f },      // 火
            { 110, 11, 6, 0.6f, 2.5f, 1f },  // 岩
            { 65, 6, 9, 0.9f, 2f, 3f },      // 影
            { 80, 8, 7, 0.7f, 2.2f, 3f },    // 植
            { 65, 6, 8, 0.8f, 1.8f, 2f },    // 翼
            { 85, 8, 7, 0.7f, 2.2f, 3f },    // 潮
            { 65, 6, 9, 0.9f, 2.2f, 3f },    // 雷
            { 120, 12, 5, 0.6f, 2.5f, 1f },  // 甲
        };
        public static string PoolName(PetPool p) => p switch { PetPool.Fire => "火", PetPool.Rock => "岩", PetPool.Shadow => "影", PetPool.Plant => "植", PetPool.Wing => "翼", PetPool.Tide => "潮", PetPool.Thunder => "雷", _ => "甲" };

        public static int MaxHp(PetSpecies s, int lv) => Mathf.RoundToInt((PoolStats[(int)s.pool, 0] + PoolStats[(int)s.pool, 1] * (lv - 1)) * (s.elite ? EliteHp : 1f));
        public static int Atk(PetSpecies s, int lv) => Mathf.RoundToInt((PoolStats[(int)s.pool, 2] + PoolStats[(int)s.pool, 3] * (lv - 1)) * (s.elite ? EliteAtk : 1f));
        public static float AttackGap(PetSpecies s) => PoolStats[(int)s.pool, 4];
        public static float AttackRange(PetSpecies s) => PoolStats[(int)s.pool, 5];
        /// <summary>從 lv 升到 lv+1 要的經驗：80＋20L＋2L²</summary>
        public static int XpToNext(int lv) => 80 + 20 * lv + 2 * lv * lv;
        /// <summary>出戰時的有效等級（不超過主人等級）</summary>
        public static int EffectiveLevel(PetData p, int ownerLevel) => Mathf.Clamp(Mathf.Min(p.level, ownerLevel), 1, MaxLevel);

        /// <summary>收服機率：一般 35%～90%（血越低越高、幸運加最多 15%）；菁英 ×0.5、最高 45%</summary>
        public static float CaptureChance(float hpFrac, int luck, bool elite)
        {
            float p = 0.35f + 0.40f * (1f - Mathf.Clamp01(hpFrac / CaptureHp)) + Mathf.Min(0.15f, 0.002f * Mathf.Max(0, luck));
            p = Mathf.Clamp(p, 0.35f, 0.90f);
            return elite ? Mathf.Min(0.45f, p * 0.5f) : p;
        }

        /// <summary>這個城鎮（章節）賣哪些誘餌：熔岩鎮第 1 章、森林第 2 章、海港第 3 章、角鬥場、山道</summary>
        public static List<PetSpecies> ShopBaits(int town)
        {
            var l = new List<PetSpecies>();
            foreach (var s in All) if (!s.elite && s.chapter == town) l.Add(s);
            return l;
        }

        /// <summary>加經驗（到 50 級為止）；回傳升了幾級</summary>
        public static int AddXp(PetData p, int xp)
        {
            if (p.level >= MaxLevel || xp <= 0) return 0;
            p.xp += xp; int ups = 0;
            while (p.level < MaxLevel && p.xp >= XpToNext(p.level)) { p.xp -= XpToNext(p.level); p.level++; ups++; }
            if (p.level >= MaxLevel) p.xp = 0;
            return ups;
        }

        /// <summary>餵食：加經驗，再擲學新招（從同池還沒學的抽）；學滿 3 招就先放進 pendingLearn 等玩家選</summary>
        public static (int ups, string learned, bool pending) Feed(PetData p, PetFood f, System.Random rng)
        {
            int ups = AddXp(p, f.xp);
            var s = p.Species; if (s == null) return (ups, null, false);
            var cand = new List<string>();
            foreach (var k in PoolOf(s.pool)) if (!p.skills.Contains(k.id) && k.id != p.pendingLearn) cand.Add(k.id);
            if (cand.Count == 0 || rng.NextDouble() >= f.learn) return (ups, null, false);
            string got = cand[rng.Next(cand.Count)];
            if (p.skills.Count < MaxSkills) { p.skills.Add(got); return (ups, got, false); }
            p.pendingLearn = got; return (ups, got, true);
        }

        // ---------------------------------------------------------------- 存檔（每個角色一份）
        public static List<PetData> Load()
        {
            var list = new List<PetData>();
            foreach (var line in (Progress.Get("sd_pets") ?? "").Split('\n'))
            {
                var p = line.Split('|');
                if (p.Length < 6 || !long.TryParse(p[0], out var uid) || !int.TryParse(p[1], out var kind) || !Capturable(kind)) continue;
                var d = new PetData { uid = uid, kind = kind };
                int.TryParse(p[2], out d.level); d.level = Mathf.Clamp(d.level, 1, MaxLevel);
                int.TryParse(p[3], out d.xp);
                foreach (var k in p[4].Split(',')) if (Skill(k) != null && d.skills.Count < MaxSkills && !d.skills.Contains(k)) d.skills.Add(k);
                d.pendingLearn = string.IsNullOrEmpty(p[5]) ? null : p[5];
                if (p.Length >= 7) d.nick = p[6];
                if (d.skills.Count == 0) d.skills.Add(d.Species.start);
                list.Add(d);
            }
            return list;
        }
        public static void Save(List<PetData> list)
        {
            var sb = new StringBuilder();
            foreach (var d in list)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(d.uid).Append('|').Append(d.kind).Append('|').Append(d.level).Append('|').Append(d.xp).Append('|')
                  .Append(string.Join(",", d.skills)).Append('|').Append(d.pendingLearn ?? "").Append('|').Append((d.nick ?? "").Replace("|", "").Replace("\n", ""));
            }
            Progress.Set("sd_pets", sb.ToString());
        }
        public static long ActiveUid { get { long.TryParse(Progress.Get("sd_pet_active") ?? "0", out var u); return u; } set => Progress.Set("sd_pet_active", value.ToString(CultureInfo.InvariantCulture)); }
        /// <summary>新收服的寵物（Lv1、會一開始的那招）；收藏滿了回傳 null</summary>
        public static PetData AddNew(int kind)
        {
            var s = Get(kind); if (s == null) return null;
            var list = Load(); if (list.Count >= MaxRoster) return null;
            var d = new PetData { uid = DateTime.UtcNow.Ticks, kind = kind };
            d.skills.Add(s.start);
            list.Add(d); Save(list);
            if (ActiveUid == 0) ActiveUid = d.uid;   // 第一隻：自動設為出戰
            return d;
        }

        static Pets()
        {
            // 一般 19 種（chapter＝哪個城鎮賣：0 熔岩、1 森林、2 海港）
            N(0, "熔糖肉串", 180, PetPool.Fire, "F1", 0); N(1, "炙芯果", 200, PetPool.Fire, "F2", 0); N(2, "赤礦鹽塊", 240, PetPool.Rock, "R1", 0);
            N(3, "暖爐糖球", 180, PetPool.Fire, "F3", 0); N(4, "磁砂餅", 220, PetPool.Rock, "R2", 0); N(5, "焰角堅果", 240, PetPool.Fire, "F2", 0);
            N(6, "夜晶蜜糖", 260, PetPool.Shadow, "S1", 0);
            N(7, "露蜜花包", 200, PetPool.Plant, "P1", 1); N(8, "嫩根鹽糰", 240, PetPool.Rock, "R3", 1); N(9, "月露葉捲", 240, PetPool.Plant, "P2", 1);
            N(10, "鮮肉花苞", 220, PetPool.Plant, "P3", 1); N(11, "發酵孢餅", 220, PetPool.Plant, "P4", 1); N(12, "油籽脆粒", 240, PetPool.Plant, "P1", 1);
            N(13, "跳浪魚乾", 240, PetPool.Tide, "T1", 2); N(14, "銀鱗羽籽", 260, PetPool.Wing, "W1", 2); N(15, "泡泡蝦糰", 280, PetPool.Tide, "T2", 2);
            N(16, "螺香蟹餅", 300, PetPool.Shell, "A1", 2); N(17, "電露凝膠", 300, PetPool.Thunder, "L1", 2); N(18, "藻鹽酥球", 280, PetPool.Shell, "A2", 2);
            // 菁英 25 種（chapter 照 EliteCatalog）
            E(23, 0, "熔冠礦心", 1000, PetPool.Rock, "R4"); E(24, 0, "焰弩蜜核", 1000, PetPool.Fire, "F2"); E(25, 0, "黑曜星糖", 1000, PetPool.Shadow, "S2");
            E(26, 0, "爐心溫餅", 1000, PetPool.Fire, "F4"); E(27, 0, "灰燼肉脯", 1000, PetPool.Shadow, "S3");
            E(28, 1, "苔甲根糰", 1250, PetPool.Rock, "R4"); E(29, 1, "棘莢珍籽", 1250, PetPool.Plant, "P1"); E(30, 1, "夢孢露餅", 1250, PetPool.Plant, "P4");
            E(31, 1, "菌冠酵蜜", 1250, PetPool.Plant, "P2"); E(32, 1, "翠翼嫩芽", 1250, PetPool.Wing, "W2");
            E(33, 2, "珊瑚螺宴", 1500, PetPool.Shell, "A3"); E(34, 2, "帆鰭蝦糕", 1500, PetPool.Tide, "T3"); E(35, 2, "雷傘珠露", 1500, PetPool.Thunder, "L2");
            E(36, 2, "螺笛海蜜", 1500, PetPool.Tide, "T4"); E(37, 2, "浪刃鮮脯", 1500, PetPool.Tide, "T1");
            E(38, 3, "銅盾鹽酪", 1750, PetPool.Rock, "R4"); E(39, 3, "鎖弩烤果", 1750, PetPool.Wing, "W3"); E(40, 3, "砂鐘晶糖", 1750, PetPool.Shadow, "S2");
            E(41, 3, "戰鼓麥餅", 1750, PetPool.Shell, "A4"); E(42, 3, "赤刃香肉", 1750, PetPool.Shadow, "S3");
            E(43, 4, "晶甲礦酥", 2000, PetPool.Rock, "R4"); E(44, 4, "砂晶蟲餅", 2000, PetPool.Rock, "R2"); E(45, 4, "流砂蜜晶", 2000, PetPool.Shadow, "S4");
            E(46, 4, "石巢菌包", 2000, PetPool.Plant, "P4"); E(47, 4, "裂風蜥脯", 2000, PetPool.Wing, "W2");

            // 技能池
            K("F1", "火爪", PetPool.Fire, PetFx.Melee, 1.3f, 1.2f, 5f);
            K("F2", "火種彈", PetPool.Fire, PetFx.Burn, 1.2f, 4f, 7f, 0.15f, 3f, true);
            K("F3", "爆焰鼓腹", PetPool.Fire, PetFx.SelfAoe, 1.1f, 1f, 10f, 0f, 0f, true);
            K("F4", "暖焰膜", PetPool.Fire, PetFx.SelfShield, 0.12f, 0f, 14f, 0f, 5f);
            K("R1", "岩拳", PetPool.Rock, PetFx.Melee, 1.5f, 1.2f, 6f);
            K("R2", "拋礫", PetPool.Rock, PetFx.Ranged, 1.3f, 4f, 7f);
            K("R3", "根甲衝", PetPool.Rock, PetFx.Dash, 1.2f, 2f, 10f, 0.2f, 2f);
            K("R4", "固守", PetPool.Rock, PetFx.SelfReduce, 0.2f, 0f, 14f, 0f, 4f);
            K("S1", "暗晶", PetPool.Shadow, PetFx.Ranged, 1.4f, 4f, 6f, 0f, 0f, true);
            K("S2", "束影", PetPool.Shadow, PetFx.Snare, 0.8f, 3f, 12f, 0.6f, 0f, true);
            K("S3", "影切", PetPool.Shadow, PetFx.Dash, 1.6f, 2f, 9f);
            K("S4", "薄暮", PetPool.Shadow, PetFx.SelfShield, 0.10f, 0f, 14f, 0f, 4f);
            K("P1", "種莢", PetPool.Plant, PetFx.Ranged, 1.2f, 4f, 5f);
            K("P2", "纏根", PetPool.Plant, PetFx.Snare, 0.7f, 3f, 12f, 0.8f, 0f, true);
            K("P3", "花噬", PetPool.Plant, PetFx.Leech, 1.3f, 1.2f, 9f, 0.25f);
            K("P4", "孢霧", PetPool.Plant, PetFx.ZoneDot, 0.25f, 3f, 11f, 0.8f, 3f, true);
            K("W1", "掠擊", PetPool.Wing, PetFx.Melee, 1.3f, 2f, 6f);
            K("W2", "盤旋", PetPool.Wing, PetFx.SelfReduce, 0.15f, 0f, 12f, 0f, 3f);
            K("W3", "羽針", PetPool.Wing, PetFx.Double, 0.65f, 4f, 8f);
            K("W4", "風掠", PetPool.Wing, PetFx.Line, 1.0f, 3f, 11f, 1f);
            K("T1", "躍浪拍擊", PetPool.Tide, PetFx.Melee, 1.3f, 2f, 6f);
            K("T2", "泡泡彈", PetPool.Tide, PetFx.Slow, 1.0f, 4f, 8f, 0.2f, 2f, true);
            K("T3", "潮線", PetPool.Tide, PetFx.Line, 1.2f, 3f, 10f, 0.8f, 0f, true);
            K("T4", "海露", PetPool.Tide, PetFx.Heal, 0.6f, 3f, 16f);
            K("L1", "電觸", PetPool.Thunder, PetFx.Melee, 1.4f, 2f, 6f, 0f, 0f, true);
            K("L2", "雷珠", PetPool.Thunder, PetFx.Ranged, 1.6f, 4f, 9f, 0f, 0f, true);
            K("L3", "微電網", PetPool.Thunder, PetFx.ZoneDot, 0.8f, 3f, 12f, 1f, 0f, true);
            K("L4", "蓄電膜", PetPool.Thunder, PetFx.SelfShield, 0.10f, 0f, 14f, 0f, 4f);
            K("A1", "鉗擊", PetPool.Shell, PetFx.Melee, 1.5f, 1.2f, 7f);
            K("A2", "棘刺", PetPool.Shell, PetFx.SelfAoe, 1.0f, 1f, 9f);
            K("A3", "縮殼", PetPool.Shell, PetFx.SelfReduce, 0.25f, 0f, 14f, 0f, 4f);
            K("A4", "護主甲光", PetPool.Shell, PetFx.OwnerShield, 0.10f, 3f, 16f, 0f, 4f);
        }

        /// <summary>角鬥場（第 4 章）、山道（第 5 章）的城鎮賣哪些一般怪的誘餌：挑那一章常出現的 4 種</summary>
        public static readonly int[] ArenaBaits = { 2, 12, 16, 17 }, CanyonBaits = { 4, 5, 8, 9 };   // 每間店約 4 種（使用者定案：只賣該章的）
        public static List<PetSpecies> TownBaits(int town)
        {
            if (town <= 2) return ShopBaits(town);
            var l = new List<PetSpecies>();
            foreach (var k in town == 3 ? ArenaBaits : CanyonBaits) l.Add(Get(k));
            return l;
        }
    }
}
