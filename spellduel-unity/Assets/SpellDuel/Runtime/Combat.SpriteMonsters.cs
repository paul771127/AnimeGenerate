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
        public bool ranged;
    }

    public static class SpriteMonsters
    {
        public static readonly SpriteMonsterDef[] All =
        {
            new SpriteMonsterDef { id = 201, baseKind = 0,  sheet = "castle_skeleton", name = "骷髏劍兵",   weight = 4, desc = "拿著生鏽短劍和小圓盾的骷髏，喀啦喀啦地衝過來" },
            new SpriteMonsterDef { id = 202, baseKind = 2,  sheet = "castle_knight",   name = "空殼騎士",   weight = 2, hp = 1.2f, height = 2.0f, desc = "裡面沒有人的盔甲，頭盔縫裡亮著兩點藍光" },
            new SpriteMonsterDef { id = 203, baseKind = 1,  sheet = "castle_archer",   name = "骷髏弓手",   weight = 3, ranged = true, desc = "戴著綠色兜帽的骷髏，躲在後面放箭" },
            new SpriteMonsterDef { id = 204, baseKind = 6,  sheet = "castle_ghost",    name = "提燈幽靈",   weight = 2, ranged = true, desc = "提著紫色燈籠飄來飄去的小幽靈，會瞬間移動" },
            new SpriteMonsterDef { id = 205, baseKind = 14, sheet = "castle_bat",      name = "吸血小蝙蝠", weight = 2, height = 1.4f, desc = "圓滾滾的紫色蝙蝠，從天花板俯衝下來咬人" },
            new SpriteMonsterDef { id = 206, baseKind = 3,  sheet = "castle_pumpkin",  name = "南瓜炸彈",   weight = 1, height = 1.4f, desc = "點著引信的南瓜燈，跑到你腳邊就爆炸" },
            new SpriteMonsterDef { id = 207, baseKind = 4,  sheet = "castle_gargoyle", name = "小石像鬼",   weight = 2, ranged = true, desc = "從屋簷上跳下來的石像鬼，把石塊舉過頭丟過來" },
            new SpriteMonsterDef { id = 208, baseKind = 5,  sheet = "castle_mage",     name = "見習黑魔導", weight = 2, ranged = true, desc = "尖帽子底下只看得到兩隻黃色眼睛，法杖的寶珠會發光" },
        };
        public static SpriteMonsterDef Get(int id) { foreach (var d in All) if (d.id == id) return d; return null; }
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

        /// <summary>SpawnMinion 開頭叫：要換成城堡敵人就改種類與倍率，回傳變種 id（0＝不換）</summary>
        int CastleRemap(ref int kind, ref float hpMul, ref float dmgMul, ref float speedMul)
        {
            if (!castleSprites || spawningVariant || kind == TreasureKind) return 0;
            var d = SpriteMonsters.Pick(kind, rng);
            kind = d.baseKind; hpMul *= d.hp; dmgMul *= d.dmg; speedMul *= d.speed;
            return d.id;
        }
    }
}
