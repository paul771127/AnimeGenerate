using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 2D 圖片的城堡敵人（使用者要求的測試版：場景照樣 3D，敵人改用 8 方向的日式 Q 版 2D 圖片，每個動作都有 8 方向）。
    ///   用變種怪的方式做（id 201～208）：AI、碰撞、出招照原本的小怪（baseKind），只換名字、數值、外觀。
    ///   style = castle 的副本裡，生出來的小怪一律換成這 8 種之一（遠程換遠程、近戰換近戰）。
    ///   圖片：Resources/SpriteEnemies/{sheet}.png（2048×2048，128×128 一格），畫面在 SpriteEnemyView.cs。
    /// </summary>
    public sealed class SpriteMonsterDef
    {
        public int id, baseKind, weight = 1;
        public string sheet, name, desc;
        public float hp = 1f, dmg = 1f, speed = 1f, height = 1.6f;   // height＝一格圖對應的世界高度（公尺）
        public float hiHeight;   // 高解析度每動作圖（FF 風、正常比例）的一格高度（公尺）；0＝跟 height 一樣
        public float lift;   // 圖往上浮多少公尺（蝙蝠：AI 在地上衝，圖畫在低空）
        public int kind = -1;   // 第三章起的表：依小怪種類對應（≥0；同一種怪在不同章節有不同的圖，所以照章節分表）
        public bool ranged;
    }

    public static class SpriteMonsters
    {
        public static readonly SpriteMonsterDef[] All =
        {
            new SpriteMonsterDef { id = 201, baseKind = 0,  sheet = "castle_skeleton", name = "骷髏劍兵",   weight = 4, hiHeight = 2.0f, desc = "拿著生鏽短劍和小圓盾的骷髏，喀啦喀啦地衝過來" },
            new SpriteMonsterDef { id = 202, baseKind = 2,  sheet = "castle_knight",   name = "空殼騎士",   weight = 2, hp = 1.2f, height = 2.0f, hiHeight = 2.4f, desc = "裡面沒有人的盔甲，頭盔縫裡亮著兩點藍光" },
            new SpriteMonsterDef { id = 203, baseKind = 1,  sheet = "castle_archer",   name = "骷髏弓手",   weight = 3, hiHeight = 2.0f, ranged = true, desc = "戴著綠色兜帽的骷髏，躲在後面放箭" },
            new SpriteMonsterDef { id = 204, baseKind = 6,  sheet = "castle_ghost",    name = "提燈幽靈",   weight = 2, hiHeight = 2.0f, ranged = true, desc = "提著紫色燈籠飄來飄去的小幽靈，會瞬間移動" },
            new SpriteMonsterDef { id = 205, baseKind = 8,  sheet = "castle_bat",      name = "吸血小蝙蝠", weight = 2, height = 1.4f, hiHeight = 1.6f, lift = 0.5f, desc = "圓滾滾的紫色蝙蝠，低空衝過來咬人" },   // 原本用俯衝海鳥（飛到 3.5～5m 高），室內看起來很怪、還會飛過牆頂 → 改用衝鋒的 AI
            new SpriteMonsterDef { id = 206, baseKind = 3,  sheet = "castle_pumpkin",  name = "南瓜炸彈",   weight = 1, height = 1.4f, hiHeight = 1.6f, desc = "點著引信的南瓜燈，跑到你腳邊就爆炸" },
            new SpriteMonsterDef { id = 207, baseKind = 4,  sheet = "castle_gargoyle", name = "小石像鬼",   weight = 2, hiHeight = 1.8f, ranged = true, desc = "從屋簷上跳下來的石像鬼，把石塊舉過頭丟過來" },
            new SpriteMonsterDef { id = 208, baseKind = 5,  sheet = "castle_mage",     name = "見習黑魔導", weight = 2, hiHeight = 2.0f, ranged = true, desc = "尖帽子底下只看得到兩隻黃色眼睛，法杖的寶珠會發光" },
        };
        /// <summary>
        /// 第二章（森林）的敵人改成 2D 圖（使用者要求：跟月影古堡一樣的精緻日式漫畫風）。不換種類、不換 AI，只換外觀；
        /// 依小怪種類（kind）或變種 id（variant）對應。圖還沒畫好（Resources 裡沒有）就照舊用 3D 模型。圖的規格同城堡（SpriteEnemyView.cs）。
        /// </summary>
        public static readonly SpriteMonsterDef[] Forest =
        {
            // 一般小怪
            new SpriteMonsterDef { id = -7,  sheet = "forest_spitter",    name = "毒液噴花",   hiHeight = 1.8f },
            new SpriteMonsterDef { id = -8,  sheet = "forest_boar",       name = "荊棘衝鋒獸", hiHeight = 1.6f },
            new SpriteMonsterDef { id = -9,  sheet = "forest_vinemage",   name = "藤蔓術士",   hiHeight = 2.0f },
            new SpriteMonsterDef { id = -10, sheet = "forest_chomper",    name = "食人花",     hiHeight = 1.8f },
            new SpriteMonsterDef { id = -11, sheet = "forest_sporecap",   name = "孢子菇",     hiHeight = 1.6f },
            new SpriteMonsterDef { id = -12, sheet = "forest_seedgunner", name = "種子槍手",   hiHeight = 1.8f },
            // 第二章的小 Boss
            new SpriteMonsterDef { id = -19, sheet = "forest_rootbeast",  name = "古根撼地獸", hiHeight = 2.8f },
            // 第二章的菁英怪
            new SpriteMonsterDef { id = -28, sheet = "forest_elite_guardian", name = "苔甲守根者", hiHeight = 2.4f },
            new SpriteMonsterDef { id = -29, sheet = "forest_elite_sniper",   name = "棘莢狙擊手", hiHeight = 2.0f },
            new SpriteMonsterDef { id = -30, sheet = "forest_elite_priest",   name = "夢孢司祭",   hiHeight = 2.0f },
            new SpriteMonsterDef { id = -31, sheet = "forest_elite_brood",    name = "菌冠育巢者", hiHeight = 2.0f },
            new SpriteMonsterDef { id = -32, sheet = "forest_elite_hunter",   name = "翠翼巡獵者", hiHeight = 2.0f },
            // 第二章的稀有怪、人形敵人、輔助型（變種 id）
            new SpriteMonsterDef { id = 173, sheet = "forest_swift_panther", name = "疾風翠豹",   hiHeight = 1.8f },
            new SpriteMonsterDef { id = 174, sheet = "forest_swift_spore",   name = "竄林孢子菇", hiHeight = 1.6f },
            new SpriteMonsterDef { id = 154, sheet = "forest_ranger_scout",  name = "霧林斥候",   hiHeight = 2.0f },
            new SpriteMonsterDef { id = 155, sheet = "forest_ranger_archer", name = "林間弓手",   hiHeight = 2.0f },
            new SpriteMonsterDef { id = 156, sheet = "forest_druid",         name = "森林德魯伊", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 141, sheet = "forest_shaman",        name = "林語薩滿",   hiHeight = 2.0f },
        };

        /// <summary>第三章（海上）：潮騷港一帶的海洋魔物（跟森林同規格，有圖才換）</summary>
        public static readonly SpriteMonsterDef[] Sea =
        {
            new SpriteMonsterDef { kind = 13, sheet = "sea_jumpfish", name = "跳躍魚", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 14, sheet = "sea_seabird", name = "俯衝海鳥", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 15, sheet = "sea_bubblerider", name = "騎魚泡泡怪", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 16, sheet = "sea_hermitcrab", name = "寄居蟹", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 17, sheet = "sea_jellyfish", name = "雷電水母", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 18, sheet = "sea_puffer", name = "河豚", hiHeight = 1.6f },
            // 第三章的小 Boss、菁英怪
            new SpriteMonsterDef { kind = 20, sheet = "sea_whipeel", name = "潮鞭鰻", hiHeight = 2.8f },
            new SpriteMonsterDef { kind = 33, sheet = "sea_elite_crab", name = "珊瑚重鉗", hiHeight = 2.4f },
            new SpriteMonsterDef { kind = 34, sheet = "sea_elite_gunner", name = "帆鰭砲手", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 35, sheet = "sea_elite_weaver", name = "雷傘織潮者", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 36, sheet = "sea_elite_pilot", name = "螺笛領航者", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 37, sheet = "sea_elite_hunter", name = "浪刃追獵者", hiHeight = 2.0f },
            // 稀有怪、人形敵人、輔助型
            new SpriteMonsterDef { id = 142, sheet = "sea_fairy", name = "霧海仙女", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 157, sheet = "sea_pirate_blade", name = "黑潮刀客", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 158, sheet = "sea_tide_mage", name = "潮汐術士", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 159, sheet = "sea_deck_assassin", name = "甲板刺客", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 175, sheet = "sea_swift_fin", name = "浪尖飛鰭", hiHeight = 1.6f },
            new SpriteMonsterDef { id = 176, sheet = "sea_swift_crab", name = "疾行寄居蟹", hiHeight = 1.6f },
        };
        /// <summary>第四章（角鬥場）：被抓來鬥場的各地魔物（戴著鐵項圈、斷掉的鎖鏈、胸前黑晶）＋鬥場的人</summary>
        public static readonly SpriteMonsterDef[] Arena =
        {
            new SpriteMonsterDef { kind = 0, sheet = "arena_imp", name = "熔岩小鬼", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 1, sheet = "arena_fire_archer", name = "火焰射手", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 2, sheet = "arena_rock_charger", name = "岩石衝鋒獸", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 3, sheet = "arena_bomb_imp", name = "自爆小鬼", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 5, sheet = "arena_lava_sniper", name = "熔岩狙擊手", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 7, sheet = "arena_spitter", name = "毒液噴花", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 8, sheet = "arena_boar", name = "荊棘衝鋒獸", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 9, sheet = "arena_vinemage", name = "藤蔓術士", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 11, sheet = "arena_sporecap", name = "孢子菇", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 12, sheet = "arena_seedgunner", name = "種子槍手", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 16, sheet = "arena_hermitcrab", name = "寄居蟹", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 17, sheet = "arena_jellyfish", name = "雷電水母", hiHeight = 1.8f },
            // 第四章的小 Boss、菁英怪
            new SpriteMonsterDef { kind = 21, sheet = "arena_chain_slave", name = "鏈錘鬥奴", hiHeight = 2.6f },
            new SpriteMonsterDef { kind = 38, sheet = "arena_elite_centurion", name = "銅盾百夫長", hiHeight = 2.4f },
            new SpriteMonsterDef { kind = 39, sheet = "arena_elite_crossbow", name = "鎖弩監軍", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 40, sheet = "arena_elite_binder", name = "砂鐘縛鬥士", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 41, sheet = "arena_elite_drummer", name = "戰鼓旗手", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 42, sheet = "arena_elite_blade", name = "赤刃角鬥士", hiHeight = 2.0f },
            // 稀有怪、人形敵人、輔助型
            new SpriteMonsterDef { id = 121, sheet = "arena_priest", name = "鬥場祭司", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 143, sheet = "arena_war_drummer", name = "鬥場戰鼓手", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 160, sheet = "arena_champion", name = "冠軍劍鬥士", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 161, sheet = "arena_javelin", name = "標槍手", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 162, sheet = "arena_temple_priest", name = "鬥場神官", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 177, sheet = "arena_swift_beast", name = "金蹄戰獸", hiHeight = 1.8f },
            new SpriteMonsterDef { id = 178, sheet = "arena_swift_hound", name = "影步鬥犬", hiHeight = 1.6f },
        };
        /// <summary>第五章（黃土山道）：被砂暴磨過的各地魔物（披著砂色圍巾、身上卡著砂粒）＋沙漠盜匪</summary>
        public static readonly SpriteMonsterDef[] Canyon =
        {
            new SpriteMonsterDef { kind = 0, sheet = "canyon_imp", name = "熔岩小鬼", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 1, sheet = "canyon_fire_archer", name = "火焰射手", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 2, sheet = "canyon_rock_charger", name = "岩石衝鋒獸", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 3, sheet = "canyon_bomb_imp", name = "自爆小鬼", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 4, sheet = "canyon_rock_thrower", name = "岩石投擲者", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 5, sheet = "canyon_lava_sniper", name = "熔岩狙擊手", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 7, sheet = "canyon_spitter", name = "毒液噴花", hiHeight = 1.8f },
            new SpriteMonsterDef { kind = 8, sheet = "canyon_boar", name = "荊棘衝鋒獸", hiHeight = 1.6f },
            new SpriteMonsterDef { kind = 9, sheet = "canyon_vinemage", name = "藤蔓術士", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 11, sheet = "canyon_sporecap", name = "孢子菇", hiHeight = 1.6f },
            // 第五章的小 Boss、菁英怪
            new SpriteMonsterDef { kind = 22, sheet = "canyon_crag_lizard", name = "裂岩甲蜥", hiHeight = 2.6f },
            new SpriteMonsterDef { kind = 43, sheet = "canyon_elite_guardian", name = "晶甲守隘獸", hiHeight = 2.4f },
            new SpriteMonsterDef { kind = 44, sheet = "canyon_elite_lizard_gunner", name = "砂晶砲蜥", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 45, sheet = "canyon_elite_sand_witch", name = "流砂咒巫", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 46, sheet = "canyon_elite_herder", name = "石巢牧者", hiHeight = 2.0f },
            new SpriteMonsterDef { kind = 47, sheet = "canyon_elite_hunter", name = "裂風獵蜥", hiHeight = 2.0f },
            // 稀有怪、人形敵人、輔助型、召喚物
            new SpriteMonsterDef { id = 122, sheet = "canyon_apothecary", name = "砂谷藥師", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 123, sheet = "canyon_summoner", name = "砂谷召喚師", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 124, sheet = "canyon_sand_spirit", name = "砂靈", hiHeight = 1.6f },
            new SpriteMonsterDef { id = 144, sheet = "canyon_banner", name = "砂王旗手", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 163, sheet = "canyon_bandit_assassin", name = "沙匪刺客", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 164, sheet = "canyon_sand_mage", name = "砂咒術士", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 165, sheet = "canyon_hunter", name = "沙漠獵手", hiHeight = 2.0f },
            new SpriteMonsterDef { id = 179, sheet = "canyon_swift_fox", name = "砂暴狐魅", hiHeight = 1.6f },
            new SpriteMonsterDef { id = 180, sheet = "canyon_swift_beast", name = "砂塵疾奔獸", hiHeight = 1.8f },
        };
        /// <summary>主題 → 這一章的 2D 敵人表（1 森林、2 海上、3 角鬥場、4 黃土山道）</summary>
        static SpriteMonsterDef[] ListOf(int theme) => theme == 1 ? Forest : theme == 2 ? Sea : theme == 3 ? Arena : theme == 4 ? Canyon : null;

        /// <summary>
        /// 這隻怪要用哪一套 2D 圖（null＝用 3D 模型）：城堡敵人（變種 201～208）一定有；各章的要圖已經放進 Resources 才用。
        /// 變種（稀有怪、人形、輔助型）照變種 id，哪一章都一樣；一般小怪、菁英、小 Boss 照這一場的主題（theme）那一章的表
        /// （角鬥場、山道會出現前面章節的怪，換上那一章的打扮）。
        /// </summary>
        public static SpriteMonsterDef ForFighter(Fighter m, int theme)
        {
            if (m == null) return null;
            if (Has(m.variant)) return Get(m.variant);
            if (m.variant > 0)
            {
                foreach (var l in new[] { Forest, Sea, Arena, Canyon })
                    foreach (var d in l) if (d.kind < 0 && d.id > 0 && d.id == m.variant) return SpriteEnemyView.HasArt(d.sheet) ? d : null;
                return null;
            }
            var list = ListOf(theme); if (list == null) return null;
            foreach (var d in list)
                if (d.kind >= 0 ? m.minionKind == d.kind : d.id < 0 && m.minionKind == -d.id)
                    return SpriteEnemyView.HasArt(d.sheet) ? d : null;
            return null;
        }

        public static SpriteMonsterDef Get(int id) { foreach (var d in All) if (d.id == id) return d; return null; }

        /// <summary>月影古堡的首領：魔劍士惡靈（跟玩家差不多大）。圖 castle_boss 還沒畫好之前先借空殼騎士的圖</summary>
        public static SpriteMonsterDef CastleBoss => SpriteEnemyView.HasArt("castle_boss")
            ? new SpriteMonsterDef { id = 0, sheet = "castle_boss", name = "魔劍士惡靈", height = 2.9f, hiHeight = 2.9f }   // Codex 的圖：角色佔格子 64% 高 → 一格 2.9m，人約 1.85m（跟玩家差不多）
            : new SpriteMonsterDef { id = 0, sheet = "castle_knight", name = "魔劍士惡靈", height = 2.0f, hiHeight = 2.1f };
        public static bool Has(int id) => id >= 201 && id <= 208;

        /// <summary>原本要出的小怪 → 城堡敵人（遠程換遠程、近戰換近戰，照權重）</summary>
        public static SpriteMonsterDef Pick(int kind, System.Random rng)
        {
            bool ranged = Battle.IsRanged(kind);
            int total = 0; foreach (var d in All) if (d.ranged == ranged) total += d.weight;
            int r = rng.Next(Mathf.Max(1, total));
            foreach (var d in All) { if (d.ranged != ranged) continue; if (r < d.weight) return d; r -= d.weight; }
            return All[0];
        }
    }

    public partial class Battle
    {
        /// <summary>style = castle 的副本：生出來的小怪換成 2D 圖片的城堡敵人（SoloBattle 設定，只有房主／單人用）</summary>
        public bool castleSprites;
        bool spawningVariant;   // SpawnVariant 中：照指定的變種，不換
        /// <summary>探索地圖上的怪：用怪物編號決定換成哪一種（≥0）。走遠收起來、走近再放出來時才不會每次換成不同的怪</summary>
        int castleSeed = -1;

        /// <summary>SpawnMinion 開頭叫：要換成城堡敵人就改種類與倍率，回傳變種 id（0＝不換）</summary>
        int CastleRemap(ref int kind, ref float hpMul, ref float dmgMul, ref float speedMul)
        {
            if (!castleSprites || spawningVariant || kind == TreasureKind) return 0;
            var d = SpriteMonsters.Pick(kind, castleSeed >= 0 ? new System.Random(castleSeed * 7919 + 17) : rng);
            kind = d.baseKind; hpMul *= d.hp; dmgMul *= d.dmg; speedMul *= d.speed;
            return d.id;
        }
    }
}
