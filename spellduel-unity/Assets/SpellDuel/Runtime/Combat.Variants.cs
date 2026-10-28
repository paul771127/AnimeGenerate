using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>
    /// 變種怪（使用者要求）：用現成怪物的骨架與動作，換配色、武器、名字、數值，算成另一種怪（有自己的精魄）。
    ///   刺客怪（夜晚的副本才出現；每個副本一種）：深色配色＋匕首／長爪，移動速度、傷害 ×2；
    ///     出現在敵人的最後方 → 沿著場地邊緣快速繞到目標玩家的背後 → 從背後衝進來突襲 → 退回邊緣再繞（繞的時候只看得到發光的眼睛）。
    ///   天候怪（故事、副本，只有那種天候才出現）：雷雨精（暴雨）、霧行者（濃霧）、風刃鳶（順風）、月牙狼（月夜）。
    /// 變種的 id 從 101 開始（和 MinionKinds 分開）：minionKind 還是原本的種類（AI、碰撞、外觀骨架照舊），variant 記是哪一種變種。
    /// </summary>
    public sealed class VariantDef
    {
        public int id, baseKind;
        public string name, dungeon, desc;
        public bool assassin, claws, healer, summoner, summon;   // summon＝召喚物（沒有經驗、不掉寶）
        public int theme = -1;   // 補師：哪一章（3 角鬥場、4 黃土山道）
        public int sky = -1;
        public int human;        // 人形敵人（稀有、特定章節＋天候）：1 劍士（繞側）、2 弓手（拉開距離）、3 術士（躲進怪群）、4 刺客（繞背）、5 神官（治療同伴）；0＝不是
        public string band;      // 人形敵人的隊伍名（同一隊一起出現）
        public int support;      // 輔助型（只在那一章、那種天候出現）：1 群體治療、2 護盾、3 加速、4 強力增益；0＝不是     // 天穹飛毯的敵人：SkyEnemyRig／SkyVehicleView 的種類（0 風箏盜賊～4 空賊隊長）；−1＝不是
        public Battle.Weather weather;
        public float hp = 1f, dmg = 1f, speed = 1f;
        public Color tint;
    }

    public static class MonsterVariants
    {
        public static readonly VariantDef[] All =
        {
            // ---- 刺客怪（每個副本一種；千機迴廊在室內，沒有夜晚）
            new VariantDef { id = 101, baseKind = 0,  name = "灰燼匕影",   dungeon = "ash_mine",             assassin = true, desc = "熔岩小鬼的刺客，雙手反握黑曜匕首" },
            new VariantDef { id = 102, baseKind = 4,  name = "鑄爐潛刃",   dungeon = "ember_bell_foundry",   assassin = true, desc = "丟下巨石、改拿匕首的投擲者" },
            new VariantDef { id = 103, baseKind = 9,  name = "腐根割喉者", dungeon = "rot_temple",           assassin = true, claws = true, desc = "藤蔓術士的手指長成了長爪" },
            new VariantDef { id = 104, baseKind = 11, name = "霧徑暗菇",   dungeon = "mist_name_grove",      assassin = true, claws = true, desc = "菇傘下伸出兩把長爪的孢子菇" },
            new VariantDef { id = 105, baseKind = 15, name = "沉淵刺客",   dungeon = "sunken_throne",        assassin = true, desc = "騎魚的小魚人換上了短刃" },
            new VariantDef { id = 106, baseKind = 16, name = "黑礁剪影",   dungeon = "white_sail_echo_reef", assassin = true, claws = true, desc = "螯磨成長爪的寄居蟹" },
            new VariantDef { id = 107, baseKind = 8,  name = "破曉獵影",   dungeon = "dawn_gate",            assassin = true, claws = true, desc = "獠牙換成長爪的荊棘衝鋒獸" },
            // ---- 天候怪
            new VariantDef { id = 111, baseKind = 17, name = "雷雨精",   weather = Battle.Weather.Rain, hp = 1.5f, dmg = 1.3f, speed = 1.3f, tint = new Color(1f, 0.9f, 0.36f), desc = "暴雨裡才會出現的雷水母，放電更痛" },
            new VariantDef { id = 112, baseKind = 6,  name = "霧行者",   weather = Battle.Weather.Fog,  hp = 1.3f, dmg = 1.2f, tint = new Color(0.82f, 0.86f, 0.9f), desc = "濃霧裡的灰白術士，離遠了只看得到眼睛" },
            new VariantDef { id = 113, baseKind = 14, name = "風刃鳶",   weather = Battle.Weather.Wind, hp = 1.2f, dmg = 1.3f, speed = 1.6f, tint = new Color(0.49f, 0.95f, 0.69f), desc = "順風時乘風俯衝的鳶，飛得很快" },
            // ---- 補師（第四、五章；使用者要求）：站在敵陣後方，讀條治療受傷的同伴（被打中會打斷讀條），自己不攻擊
            new VariantDef { id = 121, baseKind = 9, name = "鬥場祭司", healer = true, theme = 3, tint = new Color(1f, 0.85f, 0.45f), desc = "角鬥場的金袍祭司，替受傷的鬥士念治療咒" },
            new VariantDef { id = 122, baseKind = 6, name = "砂谷藥師", healer = true, theme = 4, tint = new Color(0.45f, 0.95f, 0.8f), desc = "黃土山道的藥師，背著藥壺替同伴療傷" },
            // ---- 召喚師（第五章；使用者要求）：躲在後面，讀條召喚兩隻砂靈（血少、攻擊強、沒有經驗也不掉寶）；讀條被打中會打斷
            new VariantDef { id = 123, baseKind = 4, name = "砂谷召喚師", summoner = true, theme = 4, tint = new Color(0.78f, 0.55f, 1f), desc = "背著法陣石板的召喚師，從砂裡喚出砂靈" },
            new VariantDef { id = 124, baseKind = 0, name = "砂靈", summon = true, theme = 4, hp = 0.35f, dmg = 2f, speed = 1.3f, tint = new Color(1f, 0.72f, 0.38f), desc = "砂子聚成的小鬼：一碰就散，但打人很痛" },
            // ---- 輔助型（使用者要求：第二～五章各一種，只在特定天候出現；躲在後面讀條幫同伴，被打中會打斷，自己不攻擊）
            new VariantDef { id = 141, baseKind = 9,  name = "林語薩滿", support = 1, theme = 1, weather = Battle.Weather.Rain, hp = 1.4f, tint = new Color(0.55f, 1f, 0.45f), desc = "暴雨裡的森林薩滿：搖著雨鈴，替身邊所有同伴回血" },
            new VariantDef { id = 142, baseKind = 17, name = "霧海仙女", support = 2, theme = 2, weather = Battle.Weather.Fog, hp = 1.6f, tint = new Color(1f, 0.7f, 0.95f), desc = "濃霧裡的海上仙女：替同伴套上泡泡護盾" },
            new VariantDef { id = 143, baseKind = 21, name = "鬥場戰鼓手", support = 3, theme = 3, weather = Battle.Weather.Rain, hp = 0.3f, tint = new Color(1f, 0.5f, 0.3f), desc = "雷雨中擂鼓的鬥奴：鼓聲讓同伴跑得更快、出手更快" },
            new VariantDef { id = 144, baseKind = 22, name = "砂王旗手", support = 4, theme = 4, weather = Battle.Weather.Wind, hp = 0.3f, tint = new Color(1f, 0.85f, 0.3f), desc = "順風時舉起砂王戰旗：周圍的同伴攻擊大增、受到的傷害大減" },
            // ---- 人形敵人（使用者要求：各章一隊，像玩家一樣有職業；移動快、會用戰術：沒血就跑開回血／躲進怪群、繞側、繞背；稀有，只在特定天候出現）
            new VariantDef { id = 151, baseKind = 0,  name = "傭兵劍士",   human = 1, band = "赤爐傭兵團", theme = 0, weather = Battle.Weather.Wind, hp = 3f,   dmg = 1.3f, speed = 1.5f, tint = new Color(1f, 0.45f, 0.3f), desc = "繞到側面揮劍；沒血就跑到場邊喝藥水" },
            new VariantDef { id = 152, baseKind = 1,  name = "傭兵弩手",   human = 2, band = "赤爐傭兵團", theme = 0, weather = Battle.Weather.Wind, hp = 4f,   dmg = 1.3f, speed = 1.5f, tint = new Color(1f, 0.6f, 0.3f), desc = "被靠近就後退橫移，保持距離射擊" },
            new VariantDef { id = 153, baseKind = 9,  name = "爐火術士",   human = 3, band = "赤爐傭兵團", theme = 0, weather = Battle.Weather.Wind, hp = 3.3f, dmg = 1.3f, speed = 2f, tint = new Color(1f, 0.35f, 0.2f), desc = "躲在怪物群後面施法；沒血就躲進怪群" },
            new VariantDef { id = 154, baseKind = 0,  name = "霧林斥候",   human = 4, band = "霧林遊俠",   theme = 1, weather = Battle.Weather.Fog,  hp = 3f,   dmg = 1.3f, speed = 1.3f, tint = new Color(0.45f, 0.85f, 0.5f), desc = "藏在霧裡繞到背後偷襲" },
            new VariantDef { id = 155, baseKind = 1,  name = "林間弓手",   human = 2, band = "霧林遊俠",   theme = 1, weather = Battle.Weather.Fog,  hp = 4f,   dmg = 1.3f, speed = 1.5f, tint = new Color(0.55f, 0.9f, 0.4f), desc = "邊退邊射，被追就翻滾拉開" },
            new VariantDef { id = 156, baseKind = 6,  name = "森林德魯伊", human = 5, band = "霧林遊俠",   theme = 1, weather = Battle.Weather.Fog,  hp = 3.3f, dmg = 1f,   speed = 2f, tint = new Color(0.4f, 1f, 0.6f), desc = "替隊友治療；被打就躲進怪群" },
            new VariantDef { id = 157, baseKind = 0,  name = "黑潮刀客",   human = 1, band = "黑潮海盜",   theme = 2, weather = Battle.Weather.Rain, hp = 3f,   dmg = 1.3f, speed = 1.5f, tint = new Color(0.35f, 0.55f, 0.9f), desc = "雙彎刀從側面砍；沒血就退到船邊喝蘭姆" },
            new VariantDef { id = 158, baseKind = 9,  name = "潮汐術士",   human = 3, band = "黑潮海盜",   theme = 2, weather = Battle.Weather.Rain, hp = 3.3f, dmg = 1.3f, speed = 2f, tint = new Color(0.3f, 0.8f, 1f), desc = "躲在怪群後面唸潮汐咒" },
            new VariantDef { id = 159, baseKind = 0,  name = "甲板刺客",   human = 4, band = "黑潮海盜",   theme = 2, weather = Battle.Weather.Rain, hp = 3f,   dmg = 1.3f, speed = 1.3f, tint = new Color(0.25f, 0.35f, 0.6f), desc = "趁雨繞到背後捅刀" },
            new VariantDef { id = 160, baseKind = 0,  name = "冠軍劍鬥士", human = 1, band = "冠軍鬥士隊", theme = 3, weather = Battle.Weather.Wind, hp = 3.5f, dmg = 1.4f, speed = 1.5f, tint = new Color(1f, 0.8f, 0.35f), desc = "從側面包抄的老練鬥士" },
            new VariantDef { id = 161, baseKind = 1,  name = "標槍手",     human = 2, band = "冠軍鬥士隊", theme = 3, weather = Battle.Weather.Wind, hp = 4f,   dmg = 1.3f, speed = 1.5f, tint = new Color(0.9f, 0.7f, 0.4f), desc = "順風擲標槍，被靠近就後撤" },
            new VariantDef { id = 162, baseKind = 6,  name = "鬥場神官",   human = 5, band = "冠軍鬥士隊", theme = 3, weather = Battle.Weather.Wind, hp = 3.3f, dmg = 1f,   speed = 2f, tint = new Color(1f, 0.95f, 0.6f), desc = "替冠軍們治療；被打就躲到怪群後面" },
            new VariantDef { id = 163, baseKind = 0,  name = "沙匪刺客",   human = 4, band = "沙漠盜匪",   theme = 4, weather = Battle.Weather.Fog,  hp = 3f,   dmg = 1.4f, speed = 1.3f, tint = new Color(0.75f, 0.55f, 0.35f), desc = "藏在沙霧裡繞背突襲" },
            new VariantDef { id = 164, baseKind = 9,  name = "砂咒術士",   human = 3, band = "沙漠盜匪",   theme = 4, weather = Battle.Weather.Fog,  hp = 3.3f, dmg = 1.3f, speed = 2f, tint = new Color(0.95f, 0.75f, 0.4f), desc = "躲在怪群後面放砂咒" },
            new VariantDef { id = 165, baseKind = 1,  name = "沙漠獵手",   human = 2, band = "沙漠盜匪",   theme = 4, weather = Battle.Weather.Fog,  hp = 4f,   dmg = 1.3f, speed = 1.5f, tint = new Color(0.85f, 0.65f, 0.35f), desc = "保持距離射擊，被追就橫移" },
            // ---- 天穹飛毯（副本 sky_carpet；Codex 的全新骨架 SkyEnemyRig，戰鬥照原本的怪）：搭載具跳上飛毯
            new VariantDef { id = 131, baseKind = 0,  name = "風箏盜賊", sky = 0, hp = 0.9f, desc = "抓著大風箏從側面滑過來，放手跳上飛毯的短刀盜賊" },
            new VariantDef { id = 132, baseKind = 2,  name = "雲鷹騎兵", sky = 1, hp = 0.45f, dmg = 1.1f, desc = "騎巨鷹從正前方俯衝，在飛毯上方鬆開鞍的長槍兵" },
            new VariantDef { id = 133, baseKind = 1,  name = "滑翔翼兵", sky = 2, hp = 1.05f, dmg = 0.8f, desc = "兩三隻一起滑翔降落，用吹箭遠遠射人" },
            new VariantDef { id = 134, baseKind = 16, name = "浮游魔偶", sky = 3, hp = 0.9f, speed = 0.6f, desc = "三葉轉子讓它從飛毯下方浮上來；很硬、很慢" },
            new VariantDef { id = 135, baseKind = 21, name = "空賊隊長", sky = 4, hp = 0.8f, dmg = 1.2f, desc = "空賊的菁英：小飛艇放下繩索，順著繩子滑下來" },
            new VariantDef { id = 114, baseKind = 8,  name = "月牙狼",   weather = Battle.Weather.Moon, hp = 1.6f, dmg = 1.3f, speed = 1.4f, tint = new Color(0.72f, 0.78f, 1f), desc = "月夜才現身的銀色衝鋒獸" },
        };
        static readonly Dictionary<int, VariantDef> byId = new Dictionary<int, VariantDef>();
        static MonsterVariants() { foreach (var v in All) byId[v.id] = v; }
        public static VariantDef Get(int id) => byId.TryGetValue(id, out var v) ? v : null;
        public static bool IsVariant(int id) => id >= 100 && byId.ContainsKey(id);
        public static VariantDef AssassinFor(string dungeonId) { foreach (var v in All) if (v.assassin && v.dungeon == dungeonId) return v; return null; }
        public static VariantDef HealerFor(int theme) { foreach (var v in All) if (v.healer && v.theme == theme) return v; return null; }
        public static VariantDef SummonerFor(int theme) { foreach (var v in All) if (v.summoner && v.theme == theme) return v; return null; }
        public static VariantDef Sky(int skyKind) { foreach (var v in All) if (v.sky == skyKind) return v; return null; }
        public static bool IsSky(int id) => byId.TryGetValue(id, out var v) && v.sky >= 0;
        public static VariantDef SummonFor(int theme) { foreach (var v in All) if (v.summon && v.theme == theme) return v; return null; }
        /// <summary>召喚物（擊倒紀錄的種類）：沒有經驗、不掉寶</summary>
        public static bool IsSummon(int id) => byId.TryGetValue(id, out var v) && v.summon;
        /// <summary>這一章、這種天候的人形敵人隊伍（沒有＝空的）</summary>
        public static List<VariantDef> HumansFor(int theme, Battle.Weather w) { var l = new List<VariantDef>(); foreach (var v in All) if (v.human > 0 && v.theme == theme && v.weather == w && w != Battle.Weather.None) l.Add(v); return l; }
        public static bool IsHuman(int id) => byId.TryGetValue(id, out var v) && v.human > 0;
        /// <summary>人形敵人的外觀（玩家職業的模型）</summary>
        public static string HumanLook(int human) => human == 1 ? "swordsman" : human == 2 ? "archer" : human == 3 ? "mage" : human == 4 ? "assassin" : "priest";
        public static VariantDef ForWeather(Battle.Weather w) { foreach (var v in All) if (!v.assassin && v.support == 0 && v.human == 0 && v.weather == w && w != Battle.Weather.None) return v; return null; }
        /// <summary>這一章、這種天候的輔助型敵人（沒有＝null）</summary>
        public static VariantDef SupportFor(int theme, Battle.Weather w) { foreach (var v in All) if (v.support > 0 && v.theme == theme && v.weather == w && w != Battle.Weather.None) return v; return null; }
        /// <summary>擊倒紀錄的種類換回原本的怪（掉材料用）</summary>
        public static int BaseOf(int kind) => byId.TryGetValue(kind, out var v) ? v.baseKind : kind;
    }

    public partial class Battle
    {
        public const float AssassinSpeed = 2f, AssassinDmg = 2f;      // 使用者要求：移動速度、傷害 ×2
        public const float AssassinSprint = 2f;                         // 沿著邊緣繞、衝進來突襲時再快一倍（繞場一圈不用半分鐘）
        public const float AssassinLurk = 1.5f, AssassinWindup = 0.5f, AssassinReach = 0.9f, AssassinRecover = 1.2f, AssassinApproachMax = 6f;
        public const float WeatherMonsterChance = 0.5f;                 // 有天候的時候，每一波出一隻天候怪的機率
        /// <summary>這一關的刺客怪（夜晚的副本：SoloBattle 設定；null＝不出）</summary>
        public VariantDef assassinDef;
        int assassinWave = -1, weatherWave = -1, supportWave = -1, humanWave = -1; float assassinAt = -1f, weatherAt = -1f, supportAt = -1f, humanAt = -1f;

        /// <summary>擊倒紀錄、精魄用的種類：變種用變種 id</summary>
        public static int LogKind(Fighter m) => m.variant > 0 ? m.variant : m.minionKind;

        /// <summary>生出一隻變種怪（主機）</summary>
        public Fighter SpawnVariant(Vector3 at, VariantDef v)
        {
            float hp = v.hp, dmg = v.dmg, spd = v.speed;
            if (v.assassin) { dmg *= AssassinDmg; spd *= AssassinSpeed; }
            var m = SpawnMinion(at, 0, v.baseKind, HpMul * hp, DmgMul * dmg, SpeedMul * spd);
            ApplyVariant(m, v.id);
            return m;
        }

        /// <summary>套上變種的身分（主機生出來時、隊友端收到代理時都呼叫）</summary>
        public void ApplyVariant(Fighter m, int id)
        {
            var v = MonsterVariants.Get(id);
            if (v == null || m.variant == id) return;
            m.variant = id; m.name = v.name;
            if (EnemySkills.Has(id)) m.esBornAt = now;   // 新招式：進場 2 秒後才出第一招
            if (v.healer) { m.healer = true; m.attackReadyAt = now + HealerFirst; Emit("healer", m.Feet, v.tint, null); return; }
            if (v.summoner) { m.summoner = true; m.attackReadyAt = now + SummonerFirst; Emit("summoner", m.Feet, v.tint, null); return; }
            if (v.summon) { m.summoned = true; return; }
            if (v.human > 0)
            {
                m.human = v.human; m.humSide = rng.Next(2) == 0 ? -1 : 1; m.humSwitchAt = now + 4f;
                m.head = new Vector3(m.head.x, HumanHead, m.head.z); m.bodyRadius = 0.3f;

                if (v.human == 5) { m.healer = true; m.attackReadyAt = now + HealerFirst; }
                return;
            }
            if (v.support > 0)
            {
                m.support = v.support; m.attackReadyAt = now + SupportFirst;
                Emit("weathermonster", m.Feet, v.tint, $"{WeatherName(v.weather)}裡出現了輔助型敵人「{v.name}」：{SupportText(v.support)}——先打倒牠！");
                return;
            }
            if (v.sky >= 0) return;   // 天穹飛毯：登場由載具演出（Combat.Carpet.cs），不另外喊話
            if (v.assassin)
            {
                m.assassin = true; m.asnState = 0; m.asnUntil = now + AssassinLurk;
                m.fuseAt = 0f; m.hidden = false;
                Emit("assassin", m.Feet, new Color(0.9f, 0.2f, 0.35f), $"🗡 {v.name}潛伏在敵陣後方……小心背後！");
            }
            else Emit("weathermonster", m.Feet, v.tint, $"{WeatherName(v.weather)}裡出現了「{v.name}」！");
        }

        // ------------------------------------------------------------ 天候讓原本的怪物表現不同（生出來時套用）
        static readonly int[] FireKinds = { 0, 1, 3, 5 }, WaterKinds = { 13, 15, 17, 18, 20 }, PlantKinds = { 7, 9, 10, 11, 12, 19 }, BeastKinds = { 2, 8, 13, 16 };
        static bool In(int[] a, int k) => System.Array.IndexOf(a, k) >= 0;
        /// <summary>天候對這種怪的影響說明（HUD／提示用）</summary>
        public static string WeatherTraitText(Weather w) => w switch
        {
            Weather.Rain => "火系怪物變慢、變弱，自爆小鬼的引信變長；水裡的怪物更快；植物更耐打",
            Weather.Fog => "近戰怪物趁霧逼近（移動 +15%）",
            Weather.Wind => "飛鳥、跳躍魚乘風更快",
            Weather.Moon => "野獸（衝鋒獸、跳躍魚、寄居蟹）更快、更兇",
            _ => "",
        };
        void ApplyWeatherTraits(Fighter m)
        {
            if (weather == Weather.None || m.variant > 0 || EliteCatalog.IsElite(m.minionKind)) return;
            int k = m.minionKind;
            switch (weather)
            {
                case Weather.Rain:
                    if (In(FireKinds, k)) { m.speedMul *= 0.8f; m.dmgMul *= 0.85f; if (k == 3) m.fuseAt = now + BombFuse * 1.5f; }
                    else if (In(WaterKinds, k)) m.speedMul *= 1.3f;
                    else if (In(PlantKinds, k)) { m.maxHp = Mathf.RoundToInt(m.maxHp * 1.2f); m.hp = m.maxHp; }
                    break;
                case Weather.Fog: if (!IsRanged(k)) m.speedMul *= 1.15f; break;
                case Weather.Wind: if (k == 14) m.speedMul *= 1.5f; else if (k == 13) m.speedMul *= 1.25f; break;
                case Weather.Moon: if (In(BeastKinds, k)) { m.speedMul *= 1.3f; m.dmgMul *= 1.1f; } break;
            }
        }

        // ------------------------------------------------------------ 出現時機（UpdateEndless 每幀呼叫，主機）
        void UpdateVariantSpawns()
        {
            if (remoteEnemy || edgePoint == null || manualWaves && awaitingNext || stageCleared) return;
            // 刺客：這一波開始 2.5 秒後（其他小怪先出來），從敵陣最後方的場邊冒出；副本人多（3 人以上）出兩隻
            if (assassinDef != null)
            {
                if (assassinWave != wave) { assassinWave = wave; assassinAt = now + 2.5f; }
                if (assassinAt > 0f && now >= assassinAt)
                {
                    assassinAt = -1f;
                    int n = dungeonParty >= 3 ? 2 : 1;
                    for (int i = 0; i < n; i++) SpawnVariant(RearPoint(i), assassinDef);
                }
            }
            // 天候怪：每一波一半機率，波次開始後 4～9 秒從場邊出來
            var wv = MonsterVariants.ForWeather(weather);
            if (wv != null)
            {
                if (weatherWave != wave) { weatherWave = wave; weatherAt = rng.NextDouble() < WeatherMonsterChance ? now + 4f + 5f * (float)rng.NextDouble() : -1f; }
                if (weatherAt > 0f && now >= weatherAt) { weatherAt = -1f; SpawnVariant(edgePoint(), wv); }
            }
            // 人形敵人：這一章、這種天候才出現（稀有：每一波 35%）；整隊一起從敵陣後方出來
            var hv = carpetStation < 0 && !Gallery ? MonsterVariants.HumansFor(Theme, weather) : null;
            if (hv != null && hv.Count > 0)
            {
                if (humanWave != wave) { humanWave = wave; humanAt = rng.NextDouble() < HumanChance ? now + HumanSpawnDelay : -1f; }
                if (humanAt > 0f && now >= humanAt)
                {
                    humanAt = -1f;
                    for (int i = 0; i < hv.Count; i++) { SpawnVariant(RearPoint(i), hv[i]); lastMinionCount = minions.Count; }
                    Emit("weathermonster", player.Feet, hv[0].tint, $"⚔ {WeatherName(weather)}裡出現了稀有的人形敵人「{hv[0].band}」！他們會繞側、繞背，沒血就跑去回血——別讓他們逃掉！");
                }
            }
            // 輔助型：這一章、這種天候才出現；每一波開始 3 秒後從敵陣後方出來（副本 3 人以上兩隻）
            var sv = carpetStation < 0 && !Gallery ? MonsterVariants.SupportFor(Theme, weather) : null;
            if (sv != null)
            {
                if (supportWave != wave) { supportWave = wave; supportAt = now + SupportSpawnDelay; }
                if (supportAt > 0f && now >= supportAt)
                {
                    supportAt = -1f;
                    int n = dungeonParty >= 3 ? 2 : 1;
                    for (int i = 0; i < n; i++) { SpawnVariant(RearPoint(i), sv); lastMinionCount = minions.Count; }
                }
            }
        }

        /// <summary>敵陣的最後方：從玩家看過去、其他小怪的後面，場地邊緣外一點</summary>
        Vector3 RearPoint(int i)
        {
            ArenaFrame(out var c, out float R);
            Vector3 mid = Vector3.zero; int n = 0;
            foreach (var o in minions) if (o.Alive && !o.treasure && !o.assassin) { mid += o.Feet; n++; }
            var me = player.Alive ? player : null;
            if (me == null) foreach (var a in allies) if (a.Alive) { me = a; break; }
            Vector3 dir = Vector3.zero;
            if (n > 0 && me != null) dir = Fighter.Flat(mid / n - me.Feet);
            if (dir.sqrMagnitude < 1e-4f && me != null) dir = Fighter.Flat(me.forward);
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir = RotateY(dir.normalized, i == 0 ? 0f : 25f * (i % 2 == 0 ? 1 : -1));
            return c + dir * (R + 0.8f);
        }

        // ------------------------------------------------------------ 刺客的 AI
        /// <summary>刺客在繞路（離目標遠、沒有在出手）：畫面上只看得到眼睛</summary>
        public static bool AssassinSneaking(Fighter m, Fighter viewer) => m.assassin && m.windupUntil <= 0f && (viewer == null || FlatDistance(m.Feet, viewer.Feet) > 3f);

        static Vector3 XZ(Vector3 v) { v.y = 0f; return v; }   // 只去掉高度（Fighter.Flat 會順便變成單位向量）
        void UpdateAssassin(Fighter m, float dt)
        {
            var tgt = m.asnTarget;
            if (tgt == null || !tgt.Alive)
            {
                var list = new List<Fighter>(); foreach (var p in Players()) if (p.Alive) list.Add(p);
                if (list.Count == 0) return;
                tgt = m.asnTarget = list[rng.Next(list.Count)];
            }
            m.target = tgt;
            ArenaFrame(out var c, out float R);
            float ring = Mathf.Max(1.5f, R - 0.6f);
            float speed = minionSpeed * MinionSpeed[m.minionKind] * m.speedMul * EnemySpeedScale, sprint = speed * AssassinSprint;
            var fwd = Fighter.Flat(tgt.forward); if (fwd.sqrMagnitude < 1e-4f) fwd = Fighter.Flat(tgt.Feet - m.Feet); fwd = fwd.normalized;
            // 出手中：面向鎖住，時間到就砍
            if (m.windupUntil > 0f)
            {
                m.forward = m.lockFwd;
                if (now < m.windupUntil) return;
                m.windupUntil = 0f; m.attackReadyAt = now + AssassinRecover;
                var claw = Skills.All["m_claw"];
                if (FlatDistance(m.Feet, tgt.Feet) <= AssassinReach + tgt.bodyRadius + 0.5f) HitFoe(m, tgt, claw, Mathf.RoundToInt(claw.damage * m.dmgMul));
                Emit("minionattack", m.Feet, new Color(0.9f, 0.2f, 0.35f), null);
                m.asnState = 4;
                return;
            }
            switch (m.asnState)
            {
                case 0:   // 潛伏：在敵陣後方停一下
                    m.forward = Fighter.Flat(tgt.Feet - m.Feet);
                    if (now >= m.asnUntil) m.asnState = 1;
                    return;
                case 1:   // 沿著邊緣繞到目標的背後那一面
                {
                    var behind = Fighter.Flat(tgt.Feet - fwd * 3f - c);
                    if (behind.sqrMagnitude < 1e-4f) behind = -fwd;
                    var cur = XZ(m.Feet - c); if (cur.sqrMagnitude < 1e-4f) cur = -behind;
                    float aCur = Mathf.Atan2(cur.x, cur.z) * Mathf.Rad2Deg, aTgt = Mathf.Atan2(behind.x, behind.z) * Mathf.Rad2Deg;
                    float delta = Mathf.DeltaAngle(aCur, aTgt);
                    float r = cur.magnitude;
                    float stepDeg = sprint * dt / Mathf.Max(0.5f, r) * Mathf.Rad2Deg;
                    float a = (aCur + Mathf.Clamp(delta, -stepDeg, stepDeg)) * Mathf.Deg2Rad;
                    float nr = Mathf.MoveTowards(r, ring, sprint * dt);
                    var next = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * nr;
                    var mv = XZ(next - m.Feet);
                    if (mv.sqrMagnitude > 1e-6f) m.forward = mv.normalized;
                    m.head = new Vector3(next.x, m.head.y, next.z);
                    if (Mathf.Abs(delta) < 12f && Mathf.Abs(nr - ring) < 0.3f) { m.asnState = 2; m.asnUntil = now + AssassinApproachMax; }
                    return;
                }
                case 2:   // 從背後衝進來
                {
                    var goal = tgt.Feet - fwd * 0.7f;
                    var to = XZ(goal - m.Feet);
                    float dist = FlatDistance(m.Feet, tgt.Feet);
                    if (dist <= AssassinReach + tgt.bodyRadius && now >= m.attackReadyAt)
                    {
                        m.windupUntil = now + AssassinWindup; m.lockFwd = Fighter.Flat(tgt.Feet - m.Feet).normalized;
                        Emit("minionwindup", m.Feet, new Color(0.9f, 0.2f, 0.35f), null);
                        Emit("assassinwarn", tgt.Feet, new Color(0.9f, 0.2f, 0.35f), tgt == player ? "1" : "0");
                        return;
                    }
                    if (now >= m.asnUntil) { m.asnState = 4; return; }   // 一直追不到：退回去重新繞
                    if (to.magnitude > 0.05f) { m.head += to.normalized * Mathf.Min(sprint * dt, to.magnitude); m.forward = to.normalized; }
                    return;
                }
                default:  // 退回邊緣，再繞一次
                {
                    var away = Fighter.Flat(m.Feet - c); if (away.sqrMagnitude < 1e-4f) away = -fwd;
                    away = away.normalized;
                    var next = m.Feet + away * sprint * dt;
                    m.head = new Vector3(next.x, m.head.y, next.z); m.forward = away;
                    if (FlatDistance(m.Feet, c) >= ring - 0.2f) m.asnState = 1;
                    return;
                }
            }
        }
    
        // ------------------------------------------------------------ 補師的 AI（第四、五章）
        public const float HealerEvery = 5f, HealerFirst = 2.5f, HealerCast = 1.2f, HealerRange = 7f, HealerFrac = 0.25f, HealerKeep = 5f;
        /// <summary>出一隻這一章的補師（輪次出怪的陣形裡）；額外多的，不算在這一波的數量裡</summary>
        Fighter SpawnHealerAt(float ang)
        {
            var def = MonsterVariants.HealerFor(Theme);
            if (def == null) return SpawnRoleAt(MinionRole.Back, ang, false);
            var m = SpawnVariant(edgePointAt != null ? edgePointAt(ang) : edgePoint(), def);
            lastMinionCount = minions.Count;
            return m;
        }

        void UpdateHealer(Fighter m, float dt)
        {
            Fighter near = null; float nd = float.MaxValue;
            foreach (var p in Players()) { if (!p.Alive) continue; float d = FlatDistance(m.Feet, p.Feet); if (d < nd) { nd = d; near = p; } }
            if (near == null) return;
            m.target = near;
            // 讀條中：時間到就補（被打中會在 Damage 裡打斷）
            if (m.windupUntil > 0f)
            {
                m.forward = m.lockFwd;
                if (now < m.windupUntil) return;
                m.windupUntil = 0f; m.attackReadyAt = now + HealerEvery;
                var t = m.healTarget;
                if (t != null && t.Alive && FlatDistance(t.Feet, m.Feet) <= HealerRange + 2f && t.hp < t.maxHp)
                {
                    float amt = Mathf.Min(t.maxHp - t.hp, t.maxHp * HealerFrac);
                    t.hp += amt;
                    Emit("enemyheal", t.Chest, new Color(0.45f, 1f, 0.55f), $"+{amt:0}");
                    Emit("esfx", t.Chest, Color.green, $"heal|{m.minionId}|{t.minionId}|0");
                    if (m.variant == 156) { float self = Mathf.Min(m.maxHp - m.hp, m.maxHp * 0.05f); m.hp += self; }   // 共生返青：自己也回 5%
                    if (m.variant == 162) { m.shieldUntil = now + 2.5f; m.shieldLeft = Mathf.Max(m.shieldLeft, Mathf.RoundToInt(m.maxHp * 0.15f)); Emit("esfx", m.Chest, Color.yellow, $"shield|{m.minionId}|{m.minionId}|2.5"); }   // 護禱續戰：自己 15% 護盾 2.5 秒
                }
                return;
            }
            // 找最需要補的同伴（血量比例最低、低於九成、在範圍內；不補自己、寶箱怪）
            Fighter best = null; float bestK = 0.9f;
            foreach (var o in minions)
            {
                if (o == m || !o.Alive || o.treasure || o.hp >= o.maxHp) continue;
                float k = o.hp / Mathf.Max(1f, o.maxHp);
                if (k < bestK && FlatDistance(o.Feet, m.Feet) <= HealerRange) { bestK = k; best = o; }
            }
            bool inside = minionInside == null || minionInside(m.Feet);
            if (best != null && inside && now >= m.attackReadyAt)
            {
                m.healTarget = best; m.windupUntil = now + HealerCast; m.lockFwd = Fighter.Flat(best.Feet - m.Feet);
                Emit("healcast", m.Feet, new Color(0.45f, 1f, 0.55f), $"⚕ {m.name}在替{best.name}治療！打斷牠");
                return;
            }
            KeepBehind(m, near, nd, inside, dt);
        }

        /// <summary>後排（補師、召喚師）的走位：站在同伴的後面（從最近的玩家看過去），跟玩家保持 5m 以上；玩家靠近就退</summary>
        void KeepBehind(Fighter m, Fighter near, float nd, bool inside, float dt)
        {
            Vector3 mid = Vector3.zero; int n = 0;
            foreach (var o in minions) if (o != m && o.Alive && !o.treasure && !o.healer && !o.summoner && o.support == 0) { mid += o.Feet; n++; }
            Vector3 goal = n > 0 ? mid / n : m.Feet;
            var away = Fighter.Flat(goal - near.Feet); if (away == Vector3.zero) away = Fighter.Flat(m.Feet - near.Feet);
            goal += away * 2f;
            if (FlatDistance(goal, near.Feet) < HealerKeep) goal = near.Feet + away * HealerKeep;
            if (minionInside != null && !minionInside(goal)) goal = Vector3.Lerp(goal, m.Feet, 0.5f);
            var to = XZ(goal - m.Feet);
            float speed = minionSpeed * MinionSpeed[m.minionKind] * m.speedMul * EnemySpeedScale * (nd < 3f ? 1.8f : 1f) * (inside ? 1f : 2f);
            if (to.magnitude > 0.15f)
            {
                var step = to.normalized * Mathf.Min(speed * dt, to.magnitude);
                var next = m.Feet + step;
                if (!inside || minionInside == null || minionInside(next)) m.head += step;
                m.forward = Fighter.Flat(near.Feet - m.Feet);
            }
            else m.forward = Fighter.Flat(near.Feet - m.Feet);
        }

        // ------------------------------------------------------------ 人形敵人的 AI（戰術；出手照原本的怪）
        public const float HumanChance = 0.35f, HumanSpawnDelay = 5f, HumanHead = 1.55f;
        public const float HumanFlee = 0.35f, HumanFleeBack = 0.7f, HumanFleeTime = 5f, HumanRegen = 0.08f, HumanFleeCd = 15f, HumanFleeSpeed = 1.8f;

        public const float HumanAgile = 2.2f;   // 人形敵人走位比一般小怪敏捷得多（繞側、後撤、逃跑）
        float HumanSpeed(Fighter m) => minionSpeed * MinionSpeed[m.minionKind] * m.speedMul * EnemySpeedScale * HumanAgile;
        bool HumanStep(Fighter m, Vector3 goal, float speed, float dt, Fighter face)
        {
            var to = XZ(goal - m.Feet);
            if (to.magnitude < 0.12f) { if (face != null) m.forward = Fighter.Flat(face.Feet - m.Feet); return false; }
            var next = m.Feet + to.normalized * Mathf.Min(speed * dt, to.magnitude);
            if (minionInside != null && !minionInside(next) && minionInside(m.Feet)) return false;
            m.head = new Vector3(next.x, m.head.y, next.z);
            m.forward = face != null ? Fighter.Flat(face.Feet - m.Feet) : to.normalized;
            return true;
        }
        /// <summary>怪群的中心（不算自己、人形、輔助）：沒有＝null</summary>
        Vector3? MobCenter(Fighter m)
        {
            Vector3 mid = Vector3.zero; int n = 0;
            foreach (var o in minions) if (o != m && o.Alive && !o.treasure && o.human == 0 && o.support == 0) { mid += o.Feet; n++; }
            return n >= 2 ? mid / n : (Vector3?)null;
        }

        /// <summary>
        /// 人形敵人每幀（UpdateMinions，在刺客、補師之前）：回傳 true＝這一幀的移動由這裡處理。
        ///   沒血（35% 以下）：劍士、弓手跑到場邊喝藥水，術士、神官躲進怪群後面，刺客隱身逃開；每秒回 8%，回到 70% 或 5 秒後再上（15 秒一次）。
        ///   劍士：繞到目標的側面再砍；弓手：被靠近就後退、平常橫移；術士：躲在怪群後面，準備好才出手；刺客、神官：照刺客、補師的 AI。
        /// </summary>
        bool UpdateHuman(Fighter m, Fighter tgt, float dist, float dt)
        {
            ArenaFrame(out var c, out float R);
            float sp = HumanSpeed(m);
            if (m.humState == 1)
            {
                if (now >= m.humUntil || m.hp >= m.maxHp * HumanFleeBack) { m.humState = 0; m.humCdAt = now + HumanFleeCd; if (m.human == 4) m.asnState = 1; return false; }
                Vector3 goal;
                var mob = MobCenter(m);
                var away = Fighter.Flat(m.Feet - tgt.Feet); if (away == Vector3.zero) away = Fighter.Flat(m.Feet - c); if (away == Vector3.zero) away = Vector3.forward;
                if ((m.human == 3 || m.human == 5) && mob.HasValue) { var a2 = Fighter.Flat(mob.Value - tgt.Feet); goal = mob.Value + (a2 == Vector3.zero ? away : a2) * 1.5f; }
                else goal = c + away * Mathf.Max(1f, R - 0.4f);
                if (minionInside != null && !minionInside(goal)) goal = Vector3.Lerp(c, goal, 0.8f);
                bool moving = HumanStep(m, goal, sp * HumanFleeSpeed, dt, null);
                if (!moving || FlatDistance(m.Feet, goal) < 0.6f)
                {
                    float amt = Mathf.Min(m.maxHp - m.hp, m.maxHp * HumanRegen * dt);
                    if (amt > 0f) { m.hp += amt; m.humHealed += amt; }
                    if (m.humHealed >= m.maxHp * 0.1f) { Emit("enemyheal", m.Chest, new Color(0.45f, 1f, 0.55f), $"+{m.humHealed:0}"); m.humHealed = 0f; }
                }
                return true;
            }
            if (m.hp < m.maxHp * HumanFlee && now >= m.humCdAt && m.windupUntil <= 0f)
            {
                m.humState = 1; m.humUntil = now + HumanFleeTime; m.humHealed = 0f;
                Emit("humanflee", m.Chest, new Color(1f, 0.85f, 0.4f), m.human == 3 || m.human == 5 ? $"{m.name}躲進怪群後面回血了！" : $"{m.name}跑開去喝藥水了！追上去！");
                return true;
            }
            if (m.windupUntil > 0f) return false;   // 出手中：照原本的 AI
            // 有招可放：先放招（Combat.EnemySkills.cs）。劍士要先繞到側面、術士要先躲到怪群後面才出手（繞不過去 3 秒就直接打）
            bool ready = true;
            if (m.human == 1 || m.human == 3)
            {
                var tf0 = Fighter.Flat(tgt.forward); var to0 = Fighter.Flat(m.Feet - tgt.Feet);
                var mob0 = MobCenter(m);
                ready = m.human == 1 ? tf0 == Vector3.zero || to0 == Vector3.zero || Vector3.Angle(tf0, to0) >= 50f
                                     : !mob0.HasValue || dist > FlatDistance(mob0.Value, tgt.Feet) + 0.8f;
                if (ready || m.humWaitFrom <= 0f) m.humWaitFrom = ready ? 0f : now;
                if (!ready && now - m.humWaitFrom > 3f) ready = true;
            }
            if (ready && EnemySkills.Has(m.variant) && UpdateEnemySkill(m, tgt, dist, dt)) { m.humWaitFrom = 0f; return true; }
            if (now >= m.humSwitchAt) { m.humSwitchAt = now + 4f + (float)rng.NextDouble() * 3f; if (rng.NextDouble() < 0.5) m.humSide = -m.humSide; }
            var tf = Fighter.Flat(tgt.forward); if (tf == Vector3.zero) tf = Fighter.Flat(m.Feet - tgt.Feet);
            var tr = new Vector3(tf.z, 0f, -tf.x);
            switch (m.human)
            {
                case 1:   // 劍士：繞到側面（目標右手或左手邊 1.1m）再近身
                {
                    var flank = tgt.Feet + tr * m.humSide * 1.1f;
                    if (FlatDistance(m.Feet, flank) > 0.5f && dist > 0.9f) { if (!HumanStep(m, flank, sp * 1.3f, dt, tgt)) m.humSide = -m.humSide; return true; }
                    return false;
                }
                case 2:   // 弓手：太近就後撤＋橫移；平常一邊橫移一邊射
                {
                    var away = Fighter.Flat(m.Feet - tgt.Feet); if (away == Vector3.zero) away = -tf;
                    var side = new Vector3(away.z, 0f, -away.x) * m.humSide;
                    if (dist < 3.2f) { if (!HumanStep(m, m.Feet + (away + side * 0.6f).normalized, sp * 1.7f, dt, tgt)) m.humSide = -m.humSide; return true; }
                    if (dist < 7f && !HumanStep(m, m.Feet + side * 0.5f, sp * 0.5f, dt, tgt)) m.humSide = -m.humSide;
                    return false;
                }
                case 3:   // 術士：躲在怪群後面；準備好才出手
                {
                    var mob = MobCenter(m);
                    if (mob.HasValue && now < m.attackReadyAt)
                    {
                        var a2 = Fighter.Flat(mob.Value - tgt.Feet); if (a2 == Vector3.zero) a2 = -tf;
                        HumanStep(m, mob.Value + a2 * 1.5f, sp * 1.3f, dt, tgt);
                        return true;
                    }
                    if (dist < 2.5f) { var away = Fighter.Flat(m.Feet - tgt.Feet); if (away == Vector3.zero) away = -tf; HumanStep(m, m.Feet + away, sp * 1.5f, dt, tgt); return true; }
                    return false;
                }
                case 4:   // 刺客：繞到目標背後 1.2m（背襲要在背側才放）
                {
                    var back = tgt.Feet - tf * 1.2f;
                    if (minionInside != null && !minionInside(back)) back = tgt.Feet + tr * m.humSide * 1.2f;
                    // 還在目標前半邊：先繞到側面（不要從正面穿過去）
                    var rel = Fighter.Flat(m.Feet - tgt.Feet);
                    if (rel != Vector3.zero && Vector3.Angle(tf, rel) < 80f) back = tgt.Feet + tr * m.humSide * 1.4f;
                    if (FlatDistance(m.Feet, back) > 0.4f) { if (!HumanStep(m, back, sp * 1.6f, dt, tgt)) m.humSide = -m.humSide; return true; }
                    return false;
                }
                default: return false;   // 神官：照補師的 AI
            }
        }

        // ------------------------------------------------------------ 輔助型的 AI（第二～五章、特定天候）
        public const float SupportFirst = 2.5f, SupportSpawnDelay = 3f;
        public const float ShamanFrac = 0.15f, FairyShield = 0.2f, FairyDur = 6f, HasteSpeed = 1.35f, HasteAtk = 0.5f, HasteDur = 6f, RallyDmg = 1.3f, RallyTaken = 0.7f, RallyDur = 8f;
        public const int FairyTargets = 3;
        /// <summary>每一種：多久放一次、讀條幾秒、範圍</summary>
        static float SupportEvery(int k) => k == 1 ? 7f : k == 2 ? 8f : k == 3 ? 9f : 12f;
        static float SupportCast(int k) => k == 4 ? 2f : k == 1 ? 1.5f : 1.2f;
        static float SupportRange(int k) => k == 1 ? 4.5f : k == 4 ? 6f : 5f;
        public static string SupportText(int k) => k == 1 ? $"讀條 1.5 秒，替周圍 4.5m 的同伴各回 {ShamanFrac * 100f:0}% HP"
            : k == 2 ? $"讀條 1.2 秒，替最近的 {FairyTargets} 個同伴套上 {FairyShield * 100f:0}% HP 的護盾（{FairyDur:0} 秒）"
            : k == 3 ? $"讀條 1.2 秒，周圍同伴移動 +{(HasteSpeed - 1f) * 100f:0}%、出手更快（{HasteDur:0} 秒）"
            : $"讀條 2 秒，周圍 6m 的同伴攻擊 +{(RallyDmg - 1f) * 100f:0}%、受到的傷害 −{(1f - RallyTaken) * 100f:0}%（{RallyDur:0} 秒）";
        static string SupportVerb(int k) => k == 1 ? "治療" : k == 2 ? "套護盾" : k == 3 ? "擂鼓加速" : "舉旗增益";

        bool SupportWants(Fighter m, Fighter o)
        {
            if (o == m || !o.Alive || o.treasure || o.support > 0 || o.capturedPet) return false;
            if (FlatDistance(o.Feet, m.Feet) > SupportRange(m.support)) return false;
            switch (m.support)
            {
                case 1: return o.hp < o.maxHp * 0.9f;
                case 2: return o.shieldUntil <= now || o.shieldLeft <= 0;
                case 3: return o.hasteUntil <= now + 1f;
                default: return o.rallyUntil <= now + 1f;
            }
        }

        void UpdateSupport(Fighter m, float dt)
        {
            Fighter near = null; float nd = float.MaxValue;
            foreach (var p in Players()) { if (!p.Alive) continue; float d = FlatDistance(m.Feet, p.Feet); if (d < nd) { nd = d; near = p; } }
            if (near == null) return;
            m.target = near;
            if (m.windupUntil > 0f)
            {
                m.forward = m.lockFwd;
                if (now < m.windupUntil) return;
                m.windupUntil = 0f; m.attackReadyAt = now + SupportEvery(m.support);
                CastSupport(m);
                return;
            }
            bool inside = minionInside == null || minionInside(m.Feet);
            int want = 0; foreach (var o in minions) if (SupportWants(m, o)) want++;
            if (want > 0 && inside && now >= m.attackReadyAt)
            {
                m.windupUntil = now + SupportCast(m.support); m.lockFwd = Fighter.Flat(near.Feet - m.Feet);
                Emit("healcast", m.Feet, MonsterVariants.Get(m.variant)?.tint ?? Color.green, $"✨ {m.name}在{SupportVerb(m.support)}！打斷牠");
                return;
            }
            KeepBehind(m, near, nd, inside, dt);
        }

        void CastSupport(Fighter m)
        {
            var targets = new List<Fighter>();
            foreach (var o in minions) if (SupportWants(m, o)) targets.Add(o);
            if (m.support == 2) { targets.Sort((a, b) => FlatDistance(a.Feet, m.Feet).CompareTo(FlatDistance(b.Feet, m.Feet))); if (targets.Count > FairyTargets) targets.RemoveRange(FairyTargets, targets.Count - FairyTargets); }
            var col = MonsterVariants.Get(m.variant)?.tint ?? Color.green;
            if (m.support == 4) Emit("esfx", m.Chest, col, System.FormattableString.Invariant($"rally|{m.minionId}|0|{SupportRange(4)}"));   // 旗手插旗：一圈光（範圍）
            foreach (var o in targets)
            {
                switch (m.support)
                {
                    case 1: { float amt = Mathf.Min(o.maxHp - o.hp, o.maxHp * ShamanFrac); o.hp += amt; Emit("enemyheal", o.Chest, new Color(0.45f, 1f, 0.55f), $"+{amt:0}"); Emit("esfx", o.Chest, col, $"heal|{m.minionId}|{o.minionId}|0"); break; }
                    case 2: o.shieldUntil = now + FairyDur; o.shieldLeft = Mathf.Max(o.shieldLeft, Mathf.RoundToInt(o.maxHp * FairyShield)); Emit("enemyheal", o.Chest, col, $"🫧 護盾 {o.shieldLeft}"); Emit("esfx", o.Chest, col, System.FormattableString.Invariant($"shield|{m.minionId}|{o.minionId}|{FairyDur}")); break;
                    case 3: if (o.hasteUntil <= now) o.speedMul *= HasteSpeed; o.hasteUntil = now + HasteDur; Emit("enemyheal", o.Chest, col, "🥁 加速"); Emit("esfx", o.Chest, col, System.FormattableString.Invariant($"haste|{m.minionId}|{o.minionId}|{HasteDur}")); break;
                    default: if (o.rallyUntil <= now) { o.dmgMul *= RallyDmg; o.damageTakenMul *= RallyTaken; } o.rallyUntil = now + RallyDur; Emit("enemyheal", o.Chest, col, "🚩 戰旗增益"); Emit("esfx", o.Chest, col, System.FormattableString.Invariant($"aura|{m.minionId}|{o.minionId}|{RallyDur}")); break;
                }
            }
        }

        /// <summary>每隻小怪每幀（UpdateMinions 開頭）：加速、戰旗的效果到期就拿掉；加速中出手的冷卻跑得比較快</summary>
        void TickSupportBuffs(Fighter m, float dt)
        {
            if (m.hasteUntil > 0f)
            {
                if (now >= m.hasteUntil || !m.Alive) { m.speedMul /= HasteSpeed; m.hasteUntil = 0f; }
                else if (m.windupUntil <= 0f && m.attackReadyAt > now) m.attackReadyAt -= dt * HasteAtk;
            }
            if (m.rallyUntil > 0f && (now >= m.rallyUntil || !m.Alive)) { m.dmgMul /= RallyDmg; m.damageTakenMul /= RallyTaken; m.rallyUntil = 0f; }
        }

        // ------------------------------------------------------------ 召喚師的 AI（第五章）
        public const float SummonerEvery = 9f, SummonerFirst = 3f, SummonerCast = 1.5f;
        public const int SummonPerCast = 2, SummonMaxAlive = 4;
        Fighter SpawnSummonerAt(float ang)
        {
            var def = MonsterVariants.SummonerFor(Theme);
            if (def == null) return null;
            var m = SpawnVariant(edgePointAt != null ? edgePointAt(ang) : edgePoint(), def);
            lastMinionCount = minions.Count;
            return m;
        }

        readonly List<(Vector3 at, VariantDef def, Fighter owner)> pendingVariantSpawns = new List<(Vector3, VariantDef, Fighter)>();
        /// <summary>UpdateMinions 開頭：把上一幀召喚師叫的砂靈生出來</summary>
        void FlushVariantSpawns()
        {
            foreach (var (at, def, owner) in pendingVariantSpawns)
            {
                var s = SpawnVariant(at, def);
                s.summonedBy = owner; s.attackReadyAt = now + 1.0f;
                Emit("summonpop", s.Feet, def.tint, null);
            }
            pendingVariantSpawns.Clear();
        }

        void UpdateSummoner(Fighter m, float dt)
        {
            Fighter near = null; float nd = float.MaxValue;
            foreach (var p in Players()) { if (!p.Alive) continue; float d = FlatDistance(m.Feet, p.Feet); if (d < nd) { nd = d; near = p; } }
            if (near == null) return;
            m.target = near;
            bool inside = minionInside == null || minionInside(m.Feet);
            if (m.windupUntil > 0f)
            {
                m.forward = m.lockFwd;
                if (now < m.windupUntil) return;
                m.windupUntil = 0f; m.attackReadyAt = now + SummonerEvery;
                var def = MonsterVariants.SummonFor(Theme) ?? MonsterVariants.SummonFor(4);
                int alive = 0; foreach (var o in minions) if (o.Alive && o.summonedBy == m) alive++;
                foreach (var q in pendingVariantSpawns) if (q.owner == m) alive++;
                for (int i = 0; i < SummonPerCast && alive < SummonMaxAlive && def != null && minions.Count < MaxMinions * 3; i++, alive++)
                {
                    var side = RotateY(Fighter.Flat(near.Feet - m.Feet), i == 0 ? 60f : -60f);
                    var at = m.Feet + side * 0.9f;
                    if (minionInside != null && !minionInside(at)) at = m.Feet;
                    pendingVariantSpawns.Add((at, def, m));   // 下一幀才生（現在正在走訪 minions）
                }
                return;
            }
            int mine = 0; foreach (var o in minions) if (o.Alive && o.summonedBy == m) mine++;
            if (inside && now >= m.attackReadyAt && mine < SummonMaxAlive)
            {
                m.windupUntil = now + SummonerCast; m.lockFwd = Fighter.Flat(near.Feet - m.Feet);
                Emit("summoncast", m.Feet, new Color(0.78f, 0.55f, 1f), $"🔮 {m.name}在召喚！打斷牠");
                return;
            }
            KeepBehind(m, near, nd, inside, dt);
        }
}
}
