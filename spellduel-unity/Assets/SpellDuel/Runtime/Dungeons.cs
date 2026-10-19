using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>副本的一個房間：打第幾波（決定場景主題、小怪種類與強度；10 的倍數＝Boss 波；31 波起主題重新輪一次），進房間時的旁白</summary>
    public class DungeonRoom
    {
        public int wave;
        public string title, line;
    }

    /// <summary>
    /// 多人副本（參考魔獸世界的副本：一段故事、幾個房間、最後的首領）。
    ///   ・在同一塊場地打：清完一個房間（一波）就換下一個房間的場景，房間可以跨主題（熔岩、森林、海上）。
    ///   ・難度固定以「等級＝副本等級的 3 人隊伍」設計（不隨實際人數增減）：4 人輕鬆、3 人中等、2 人有一點機會。
    ///     一個人在同等級幾乎過不了；角色等級比副本高很多時敵人才會變弱（LevelRelief），
    ///     那時候副本掉的套裝等級（＝副本等級）已經遠低於角色等級。
    ///   ・通關：每個房間保底一件稀有以上的裝備（物品等級＝副本等級），最後的首領保底一件史詩套裝。
    /// </summary>
    public class DungeonDef
    {
        public string id, name, desc;
        public int level;                          // 副本等級（掉落的物品等級、難度基準）
        public int minPlayers = 2, maxPlayers = 4; // 建議人數（2 人吃力、3 人中等、4 人輕鬆）
        public readonly List<DungeonRoom> rooms = new List<DungeonRoom>();
        public readonly List<StoryLine> before = new List<StoryLine>();
        public readonly List<StoryLine> after = new List<StoryLine>();
        public int[] Waves { get { var w = new int[rooms.Count]; for (int i = 0; i < w.Length; i++) w[i] = rooms[i].wave; return w; } }
        public int BossTheme => Battle.ThemeOf(rooms[rooms.Count - 1].wave);
        /// <summary>這一波是第幾個房間（0 起算；不是這個副本的波＝-1）</summary>
        public int RoomOf(int wave) { for (int i = 0; i < rooms.Count; i++) if (rooms[i].wave == wave) return i; return -1; }
    }

    public static class Dungeons
    {
        /// <summary>
        /// 難度固定以「3 人隊伍」計算（敵人數量、血量、Boss 出招頻率都照 3 人，不隨實際人數增減）：
        /// 4 人輕鬆、3 人中等、2 人有一點機會、1 人幾乎不可能（除非等級遠高於副本）。
        /// </summary>
        public const int DesignAllies = 2;
        /// <summary>在 3 人難度之上，敵人的血量、玩家受到的傷害再加一點（副本比一般合作硬）</summary>
        public const float DungeonHp = 1.15f, DungeonDmg = 1.1f;

        /// <summary>
        /// 等級差的修正：角色比副本高 3 級以上，每多 1 級敵人弱 4.5%（最低 30%）；比副本低，每低 1 級強 5%（最高 150%）。
        /// 例：高 12 級＝0.60（一個人有機會過關，但掉的裝備比角色低 12 級）。
        /// </summary>
        public static float LevelRelief(int playerLevel, int dungeonLevel)
        {
            int over = playerLevel - dungeonLevel;
            if (over < 0) return Mathf.Min(1.5f, 1f + 0.05f * -over);
            return Mathf.Clamp(1f - 0.045f * Mathf.Max(0, over - 2), 0.3f, 1f);
        }

        public static readonly List<DungeonDef> All = new List<DungeonDef>();

        static HashSet<string> cleared;
        static HashSet<string> Cleared
        {
            get
            {
                if (cleared != null) return cleared;
                cleared = new HashSet<string>();
                foreach (var s in (Story.Get("sd_dungeon_clear") ?? "").Split(',')) if (s.Length > 0) cleared.Add(s);
                return cleared;
            }
        }
        public static bool IsCleared(string id) => Cleared.Contains(id);
        public static void MarkCleared(string id) { if (Cleared.Add(id)) Story.Set("sd_dungeon_clear", string.Join(",", Cleared)); }
        public static DungeonDef Get(string id) => All.Find(d => d.id == id);

        static StoryLine L(string speaker, string text, string tag = null) => new StoryLine { speaker = speaker, tag = tag, text = text };
        static StoryLine N(string text) => new StoryLine { speaker = null, text = text };

        static Dungeons()
        {
            var d = new DungeonDef { id = "ash_mine", name = "灰燼礦坑", level = 12, desc = "炎之里地底的廢礦。礦工說，坑道最深處有東西在模仿晨鐘的聲音。" };
            d.rooms.Add(new DungeonRoom { wave = 7, title = "崩落坑道", line = "坑道的支架全燒焦了。熔岩小鬼從裂縫裡一隻接一隻爬出來。" });
            d.rooms.Add(new DungeonRoom { wave = 13, title = "地下菌林", line = "誰會在礦坑裡種樹？發光的菌絲纏住了礦車軌道……" });
            d.rooms.Add(new DungeonRoom { wave = 9, title = "赤爐大廳", line = "大廳中央的熔爐還是熱的。有人一直在這裡燒東西。" });
            d.rooms.Add(new DungeonRoom { wave = 10, title = "首領：熔岩巨像", line = "爐心站了起來——牠的胸口嵌著一截斷掉的鐘舌！" });
            d.before.Add(N("炎之里的礦工失蹤了第二批。最後一個逃回來的人，只說聽見了「不該響的鐘聲」。"));
            d.before.Add(L("老焰", "這不是一個人能下去的地方。找幾個信得過的夥伴，一起把人帶回來。"));
            d.before.Add(L("小鐵", "坑道我熟！崩落坑道、地下菌林、赤爐大廳……最裡面那間，我沒敢進去。"));
            d.after.Add(N("巨像倒下，鐘舌從牠胸口滾落，發出一聲短短的、真正的鐘響。"));
            d.after.Add(L("老焰", "失蹤的礦工都找到了。這截鐘舌……有人故意把它餵給了怪物。"));
            All.Add(d);

            d = new DungeonDef { id = "rot_temple", name = "腐根神殿", level = 22, desc = "森林深處被根吞沒的古神殿。翠說，種子在那裡哭。" };
            d.rooms.Add(new DungeonRoom { wave = 15, title = "荊棘迴廊", line = "迴廊的石柱被根撐裂，荊棘衝鋒獸在暗處刨地。" });
            d.rooms.Add(new DungeonRoom { wave = 25, title = "淹沒的祭壇", line = "地板塌進了地下水脈——這裡竟然通到海？" });
            d.rooms.Add(new DungeonRoom { wave = 18, title = "孢子聖所", line = "空氣甜得發膩。別吸太多，往前走。" });
            d.rooms.Add(new DungeonRoom { wave = 20, title = "首領：腐化樹王", line = "神殿的主人醒了。牠的根，扎在每一個失蹤者的影子裡。" });
            d.before.Add(N("森林的路一夜之間改了方向，進去採藥的人再也沒有出來。"));
            d.before.Add(L("翠", "神殿裡的種子在哭。它被什麼東西從裡面吃掉了……請你們，替它結束痛苦。"));
            d.before.Add(L("楓", "我在入口留了燈。看到燈熄了，就快點撤出來——不要一個人逞強。"));
            d.after.Add(N("樹王的根一條條鬆開，神殿的天井重新照進了光。"));
            d.after.Add(L("翠", "種子不哭了。謝謝你們……它說，海的那一邊，還有一個同樣的聲音。"));
            All.Add(d);

            d = new DungeonDef { id = "sunken_throne", name = "沉沒王座", level = 32, desc = "潮汐退去時才會浮出的海底王城。漁民說，王座上坐著不屬於海的東西。" };
            d.rooms.Add(new DungeonRoom { wave = 24, title = "退潮的城門", line = "城門只會開一個退潮的時間。快！" });
            d.rooms.Add(new DungeonRoom { wave = 38, title = "被燒乾的庫房", line = "海底的庫房裡居然有熔岩……是誰把火帶到這裡的？" });
            d.rooms.Add(new DungeonRoom { wave = 28, title = "王的長廊", line = "牆上的壁畫，是三座城鎮的晨鐘。每一座都被畫上了裂痕。" });
            d.rooms.Add(new DungeonRoom { wave = 30, title = "首領：深海巨妖", line = "王座翻了過來——底下纏著的，是巨妖的觸手。" });
            d.before.Add(N("大潮將至。今晚之後，沉沒王座又要在海底睡上十年。"));
            d.before.Add(L("海斗", "船我來開，但城裡我不會陪你們進去。進去的人，要彼此看好背後。"));
            d.before.Add(L("凪", "王城的長廊上畫著三座鐘……如果你們看見了，請把它記下來。"));
            d.after.Add(N("巨妖沉回深淵，王座下露出一塊刻著鐘文的石板。"));
            d.after.Add(L("凪", "三座鐘、三截鐘舌……有人想讓黎明永遠不要來。"));
            All.Add(d);

            d = new DungeonDef { id = "dawn_gate", name = "黎明之門", level = 45, minPlayers = 3, desc = "三座晨鐘的鐘文拼出的位置。門後是讓黎明停止的人——三位守門的首領會依序出現。" };
            d.rooms.Add(new DungeonRoom { wave = 40, title = "第一道門：熔岩巨像", line = "門後的熱浪撲面而來。第一個守門者，用的是灰燼礦坑那截鐘舌。" });
            d.rooms.Add(new DungeonRoom { wave = 50, title = "第二道門：腐化樹王", line = "地板長出了根。第二個守門者，在神殿裡被你們打倒過——牠又回來了。" });
            d.rooms.Add(new DungeonRoom { wave = 60, title = "最後的門：深海巨妖", line = "整個大廳開始進水。撐過去，黎明就在門後。" });
            d.before.Add(N("三段鐘文拼在一起，指向一座不在任何地圖上的門。"));
            d.before.Add(L("公會長", "這一戰，我無法給你們任何建議。只有一句：誰倒下了，就去把他扶起來。"));
            d.before.Add(L("艾琳", "我會在門外敲鐘。只要鐘聲還在，就代表外面的人還等著你們回來。", "mage"));
            d.after.Add(N("最後一扇門打開了。門外——三座城鎮的晨鐘，同時響起。"));
            d.after.Add(L("艾琳", "聽到了嗎？這一次，是你們把黎明帶回來的。", "mage"));
            All.Add(d);
        }
    }
}
