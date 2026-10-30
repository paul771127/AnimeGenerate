using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    // ============================================================================================
    // 故事探索（使用者要求）：一張固定的超大迷宮地圖（第一張：叢林），沒有 10m×10m 場地限制，
    // 用「比 5→握拳」衝刺在迷宮裡移動。地圖上有固定分布的怪物（巡邏、營地、會叫同伴的）、NPC、小任務、
    // 隱藏的閃光點；一定要打過 3 個小 Boss（打贏才打開擋路的藤蔓）、最後打倒 Boss，拿到東西交給 NPC 才過關。
    // 這個檔案是純資料＋純邏輯（不碰 Unity 物件），可以離線測試；戰鬥接線在 Combat.Explore.cs，畫面在 SoloBattle.Explore.cs。
    // ============================================================================================

    public enum ExCell : byte { Wall, Floor, Gate }
    /// <summary>牆（走不過去的格子）是什麼地形：Codex 依這個畫（樹叢、岩壁、湖水、城寨木牆、古殿石牆、洞窟岩壁）</summary>
    public enum ExTerrain : byte { Thicket, Rock, Water, Palisade, Ruin, Cave }
    /// <summary>地面是什麼（走得過去的格子）：草地、泥土、湖岸、洞窟、古殿石板</summary>
    public enum ExGround : byte { Grass, Dirt, Shore, Cave, Stone }

    /// <summary>
    /// 景物障礙（使用者要求：植物、城寨…等場景都是障礙物）：放在房間、地標裡的圓形障礙，玩家和怪物都走不過去。
    /// kind：tree 大樹、boulder 巨石、mushroom 巨菇、tent 帳篷、crate 木箱、totem 圖騰、pillar 石柱、statue 雕像、campfire 篝火、altar 祭壇、stump 樹樁
    /// </summary>
    public class ExProp { public string kind; public Vector3 pos; public float radius; public int variant; }

    /// <summary>地標：走進去會「發現」（小地圖上標名字）；Codex 依 kind 佈置整個區域的場景</summary>
    public class ExLandmark
    {
        public string id, name, kind;   // kind：camp 營地、lake 湖泊、fort 城寨、rocks 岩石地形、cave 山洞、grove 植物林、palace 森林宮殿、start 起點
        public RectInt rect;            // 格子範圍（含）
        public bool found;
        public bool Contains(Vector2Int c) => c.x >= rect.xMin && c.x <= rect.xMax && c.y >= rect.yMin && c.y <= rect.yMax;
    }

    /// <summary>地圖上的一隻怪（固定分布）：活著／死了、血量、有沒有發現玩家都記在這裡（離開附近時從戰場收起來）</summary>
    public class ExMonster
    {
        public int id, kind, zone;
        public Vector3 home;
        public Vector3[] patrol;          // 巡邏點（含 home）；null＝站在原地（營地的守衛）
        public int camp = -1;             // 營地編號：同一個營地，一隻發現就全部發現
        public bool caller;               // 發現玩家時呼朋引伴（附近的怪都叫過來）
        public int guardian;              // 1～3：小 Boss（打倒後打開對應的藤蔓門）；0＝一般
        public bool rare;                 // 稀有怪（閃光點叫出來的）
        public bool hidden;               // 還沒被叫出來（稀有怪）
        public bool dead;
        public float hpFrac = 1f;         // 收起來時剩的血量比例
    }

    public class ExNpc
    {
        public string id, name, look, quest;
        public Vector3 pos;
        public bool hidden;               // 閃光點觸發後才出現
    }

    public class ExItem { public string id, name, quest; public Vector3 pos; public bool taken, hidden; }

    public class ExSparkle
    {
        public string id, kind, title, text;   // kind：rare 稀有怪、chest 寶藏、spirit 森林之靈、passage 密道、beast 受傷的野獸
        public Vector3 pos;
        public bool found;
        public int[] opens;                    // 密道：打開的牆（格子 x,z 成對）
    }

    /// <summary>
    /// 封印（使用者要求：岔路解謎）：把一個房間（小 Boss 競技場、王座）的入口封起來，要在岔路深處找到機關／鑰匙／碎片才會打開。
    /// cells 是房間邊上通往外面的格子（變成 Gate），need 是要幾個開關。
    /// </summary>
    public class ExSeal
    {
        public string id, name, hint;
        public RectInt rect;
        public readonly List<Vector2Int> cells = new List<Vector2Int>();
        public int need = 1, have;
        public bool open;
    }

    /// <summary>岔路裡的機關／鑰匙／碎片：走到旁邊就啟動／撿起來</summary>
    public class ExSwitch { public string id, kind, name, seal; public Vector3 pos; public bool done; public int zone; }

    public enum ExQuestState { Hidden, Offered, Active, Ready, Done, Failed }

    public class ExQuest
    {
        public string id, title, giver, kind;  // kind：main 主線、escort 護送、collect 收集、camp 清營地、deliver 送東西、hidden 隱藏
        public string target;                  // deliver：送給哪個 NPC
        public string desc, offer, thanks;
        public int need, have;
        public ExQuestState state;
        public int gold, xp;
        public bool hiddenQuest;
        public string Progress => kind == "escort" ? (state == ExQuestState.Ready || state == ExQuestState.Done ? "已送達" : "護送中") : need > 0 ? $"{have}/{need}" : "";
    }

    /// <summary>小 Boss 的競技場與後面的藤蔓門</summary>
    public class ExArena
    {
        public int n;                // 1～3 小 Boss、4 最終 Boss
        public Vector3 center; public float radius;
        public string name;
        public List<Vector2Int> gate = new List<Vector2Int>();   // 擋路的藤蔓（打贏前是牆）
        public bool cleared, entered;
    }

    public class ExploreMap
    {
        public const float CellSize = 3f;
        public readonly int W, H;
        public readonly ExCell[,] cells;
        public Vector2Int start;
        public string name, theme;
        public readonly List<ExMonster> monsters = new List<ExMonster>();
        public readonly List<ExNpc> npcs = new List<ExNpc>();
        public readonly List<ExItem> items = new List<ExItem>();
        public readonly List<ExSparkle> sparkles = new List<ExSparkle>();
        public readonly List<ExArena> arenas = new List<ExArena>();
        public readonly List<ExQuest> quests = new List<ExQuest>();
        public Vector3 altar;        // 最終 Boss 身後：古樹之心
        public readonly ExTerrain[,] terrain;
        public readonly ExGround[,] ground;
        public readonly List<ExProp> props = new List<ExProp>();
        public readonly List<ExLandmark> landmarks = new List<ExLandmark>();
        public readonly List<ExSeal> seals = new List<ExSeal>();
        public readonly List<ExSwitch> switches = new List<ExSwitch>();
        readonly Dictionary<Vector2Int, List<ExProp>> propCells = new Dictionary<Vector2Int, List<ExProp>>();

        public ExploreMap(int w, int h) { W = w; H = h; cells = new ExCell[w, h]; terrain = new ExTerrain[w, h]; ground = new ExGround[w, h]; }

        public ExTerrain TerrainAt(int x, int z) => InGrid(x, z) ? terrain[x, z] : ExTerrain.Thicket;
        public ExGround GroundAt(int x, int z) => InGrid(x, z) ? ground[x, z] : ExGround.Grass;

        public void AddProp(string kind, Vector3 pos, float radius, int variant = 0)
        {
            var p = new ExProp { kind = kind, pos = pos, radius = radius, variant = variant };
            props.Add(p);
            var c = Cell(pos);
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                var k = new Vector2Int(c.x + dx, c.y + dz);
                if (!propCells.TryGetValue(k, out var l)) propCells[k] = l = new List<ExProp>();
                l.Add(p);
            }
        }

        /// <summary>這一點離景物障礙不到 margin 公尺（站不進去）</summary>
        public ExProp PropAt(Vector3 p, float margin)
        {
            if (!propCells.TryGetValue(Cell(p), out var l)) return null;
            foreach (var o in l) if (Battle.FlatDistance(o.pos, p) < o.radius + margin) return o;
            return null;
        }

        /// <summary>格子中心的戰場座標（起點格＝原點，+x 東、+z 北）</summary>
        public Vector3 World(int x, int z) => new Vector3((x - start.x) * CellSize, 0f, (z - start.y) * CellSize);
        public Vector2Int Cell(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / CellSize) + start.x, Mathf.RoundToInt(p.z / CellSize) + start.y);
        public bool InGrid(int x, int z) => x >= 0 && z >= 0 && x < W && z < H;
        public ExCell At(int x, int z) => InGrid(x, z) ? cells[x, z] : ExCell.Wall;
        public bool Open(int x, int z) { var c = At(x, z); return c == ExCell.Floor || c == ExCell.Gate && GateOpen(x, z); }
        public bool GateOpen(int x, int z)
        {
            var c = new Vector2Int(x, z);
            foreach (var a in arenas) if (a.gate.Contains(c)) return a.cleared;
            foreach (var s in seals) if (s.cells.Contains(c)) return s.open;
            return true;
        }
        /// <summary>這一點在哪個還沒打開的封印上；沒有＝null</summary>
        public ExSeal ClosedSealAt(Vector3 p) { var c = Cell(p); foreach (var s in seals) if (!s.open && s.cells.Contains(c)) return s; return null; }
        public ExSeal SealOf(int x, int z) { var c = new Vector2Int(x, z); foreach (var s in seals) if (s.cells.Contains(c)) return s; return null; }

        /// <summary>
        /// 這一點能不能站（離牆 margin 公尺以內也算不行）：玩家用 0.35m、怪物用 0.45m。
        /// 牆是格子：點落在牆格、或離牆格的邊不到 margin 都不行。
        /// </summary>
        public bool Walkable(Vector3 p, float margin = 0.35f)
        {
            var c = Cell(p);
            if (!Open(c.x, c.y)) return false;
            var center = World(c.x, c.y); float half = CellSize * 0.5f;
            float dx = p.x - center.x, dz = p.z - center.z;
            if (dx > half - margin && !Open(c.x + 1, c.y)) return false;
            if (dx < -half + margin && !Open(c.x - 1, c.y)) return false;
            if (dz > half - margin && !Open(c.x, c.y + 1)) return false;
            if (dz < -half + margin && !Open(c.x, c.y - 1)) return false;
            // 角落（斜對角是牆）
            if (dx > half - margin && dz > half - margin && !Open(c.x + 1, c.y + 1)) return false;
            if (dx > half - margin && dz < -half + margin && !Open(c.x + 1, c.y - 1)) return false;
            if (dx < -half + margin && dz > half - margin && !Open(c.x - 1, c.y + 1)) return false;
            if (dx < -half + margin && dz < -half + margin && !Open(c.x - 1, c.y - 1)) return false;
            if (PropAt(p, margin) != null) return false;   // 景物障礙
            return true;
        }

        /// <summary>這一點在哪個藤蔓門上（還關著的）；沒有＝null</summary>
        public ExArena ClosedGateAt(Vector3 p)
        {
            var c = Cell(p);
            foreach (var a in arenas) if (!a.cleared && a.gate.Contains(c)) return a;
            return null;
        }

        /// <summary>從 a 到 b 走得到嗎（格子 BFS；藤蔓門照現在開關）</summary>
        public bool Reachable(Vector2Int a, Vector2Int b)
        {
            var seen = new bool[W, H]; var q = new Queue<Vector2Int>(); q.Enqueue(a); seen[a.x, a.y] = true;
            while (q.Count > 0)
            {
                var c = q.Dequeue(); if (c == b) return true;
                foreach (var d in Dirs) { int x = c.x + d.x, z = c.y + d.y; if (!InGrid(x, z) || seen[x, z] || !Open(x, z)) continue; seen[x, z] = true; q.Enqueue(new Vector2Int(x, z)); }
            }
            return false;
        }
        public static readonly Vector2Int[] Dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        public ExQuest Quest(string id) => quests.Find(q => q.id == id);
        public ExNpc Npc(string id) => npcs.Find(n => n.id == id);
    }

    /// <summary>
    /// 第一張探索地圖：叢林迷宮「翠蔭迷林」。41×41 格（每格 3m，約 123m 見方），分四個區域，
    /// 區域之間只有小 Boss 競技場後面的藤蔓門相通：西南（起點）→ 東南 → 東北 → 西北（最終 Boss）。
    /// 迷宮用固定種子產生，每次都一樣（地圖是固定的）。
    /// </summary>
    public static class JungleMaze
    {
        public const int N = 41, Mid = 20;
        public const int Seed = 20261030;

        public static ExploreMap Build()
        {
            var m = new ExploreMap(N, N) { name = "翠蔭迷林", theme = "jungle", start = new Vector2Int(3, 3) };
            var rng = new System.Random(Seed);
            // 四個區域各自是一個迷宮（區域之間是整排的牆）
            Carve(m, rng, 1, 1);     // 西南
            Carve(m, rng, 21, 1);    // 東南
            Carve(m, rng, 21, 21);   // 東北
            Carve(m, rng, 1, 21);    // 西北
            // 房間：起點、營地、競技場、最終 Boss
            Room(m, 1, 1, 5, 5);                 // 起點
            Room(m, 3, 13, 7, 17);               // 西南營地
            Room(m, 31, 3, 35, 7);               // 東南營地
            Room(m, 33, 23, 37, 27);             // 東北營地 1
            Room(m, 33, 31, 37, 35);             // 東北營地 2
            Room(m, 11, 23, 15, 27);             // 西北營地
            Room(m, 13, 7, 19, 11);              // 競技場 1（西南 → 東南）
            Room(m, 27, 13, 35, 19);             // 競技場 2（東南 → 東北）
            Room(m, 21, 27, 27, 35);             // 競技場 3（東北 → 西北）
            Room(m, 1, 31, 9, 39);               // 最終 Boss
            // 藤蔓門：競技場後面那一格牆打通成「門」（打贏才開）
            m.cells[20, 9] = ExCell.Gate; m.cells[21, 9] = ExCell.Floor;
            m.cells[31, 20] = ExCell.Gate; m.cells[31, 21] = ExCell.Floor;
            m.cells[20, 31] = ExCell.Gate; m.cells[19, 31] = ExCell.Floor;
            m.arenas.Add(new ExArena { n = 1, center = m.World(16, 9), radius = 7f, name = "苔甲守根者的巢穴", gate = { new Vector2Int(20, 9) } });
            m.arenas.Add(new ExArena { n = 2, center = m.World(31, 16), radius = 9f, name = "夢孢司祭的祭壇", gate = { new Vector2Int(31, 20) } });
            m.arenas.Add(new ExArena { n = 3, center = m.World(24, 31), radius = 9f, name = "翠翼巡獵者的獵場", gate = { new Vector2Int(20, 31) } });
            m.arenas.Add(new ExArena { n = 4, center = m.World(5, 34), radius = 11f, name = "腐化樹王的王座" });
            m.altar = m.World(5, 39);
            BuildLandmarks(m);

            // ---- NPC
            m.npcs.Add(new ExNpc { id = "elder", name = "林語長老", look = "priest", pos = m.World(4, 5), quest = "main" });
            m.npcs.Add(new ExNpc { id = "hunter", name = "獵人巴克", look = "archer", pos = m.World(9, 17), quest = "camp" });
            m.npcs.Add(new ExNpc { id = "lost", name = "迷路的探險家", look = "mage", pos = m.World(17, 17), quest = "escort" });
            m.npcs.Add(new ExNpc { id = "outpost", name = "前哨站守衛", look = "swordsman", pos = m.World(11, 9) });
            m.npcs.Add(new ExNpc { id = "herbalist", name = "草藥師莉亞", look = "priest", pos = m.World(23, 3), quest = "herbs" });
            m.npcs.Add(new ExNpc { id = "guardian", name = "守林人", look = "swordsman", pos = m.World(17, 33), quest = "main" });
            m.npcs.Add(new ExNpc { id = "spirit", name = "森林之靈", look = "priest", pos = m.World(39, 39), hidden = true });
            foreach (var n in m.npcs) Ensure(m, n.pos);

            // ---- 道具：月光草
            int hi = 0;
            foreach (var c in new[] { new Vector2Int(37, 11), new Vector2Int(23, 17), new Vector2Int(39, 3) })
                { m.items.Add(new ExItem { id = "herb" + (++hi), name = "月光草", quest = "herbs", pos = m.World(c.x, c.y) }); Ensure(m, m.items[m.items.Count - 1].pos); }

            // ---- 隱藏的閃光點
            m.sparkles.Add(new ExSparkle { id = "sp_chest", kind = "chest", pos = m.World(19, 1), title = "埋起來的寶箱", text = "土裡有東西在發光……挖出了一個古老的寶箱！" });
            m.sparkles.Add(new ExSparkle { id = "sp_rare", kind = "rare", pos = m.World(39, 19), title = "沉睡的古根", text = "地面在震動……古根撼地獸被吵醒了！" });
            m.sparkles.Add(new ExSparkle { id = "sp_spirit", kind = "spirit", pos = m.World(39, 37), title = "發光的樹洞", text = "樹洞裡飄出一團綠光——是森林之靈！" });
            m.sparkles.Add(new ExSparkle { id = "sp_beast", kind = "beast", pos = m.World(21, 39), title = "受傷的小獸", text = "一隻受傷的小獸縮在草叢裡，你替牠包紮了傷口。" });
            m.sparkles.Add(new ExSparkle { id = "sp_passage", kind = "passage", pos = m.World(17, 21), title = "奇怪的藤牆", text = "撥開藤蔓——後面是一條密道！" });
            foreach (var s in m.sparkles) Ensure(m, s.pos);
            // 密道：西北區通往起點附近的捷徑（打開兩格牆；打過第三個小 Boss 才找得到，回程用）
            var sp = m.sparkles.Find(s => s.id == "sp_passage"); sp.opens = new[] { 17, 20 };

            // ---- 任務
            m.quests.Add(new ExQuest { id = "main", kind = "main", giver = "elder", title = "古樹之心", state = ExQuestState.Offered,
                desc = "打倒三個守衛（打開藤蔓門）→ 打倒腐化樹王 → 取得古樹之心 → 交給守林人",
                offer = "旅人啊，迷林深處的腐化樹王奪走了「古樹之心」，整片叢林正在枯萎。三個被腐化的守衛擋住了去路，打倒牠們藤蔓就會退開。拿回古樹之心，交給在王座前守候的守林人吧。",
                thanks = "古樹之心回來了……叢林會重新長出綠意。謝謝你，勇者。", gold = 1500, xp = 3000 });
            m.quests.Add(new ExQuest { id = "camp", kind = "camp", giver = "hunter", title = "清除藤蔓營地", need = 4, state = ExQuestState.Offered,
                desc = "打倒西邊營地的 4 隻怪物", offer = "西邊那個營地的怪物一直襲擊獵人小屋。幫我把牠們全部打倒！小心，裡面的藤蔓術士會叫同伴。",
                thanks = "太好了，終於可以安心打獵了。這些拿去吧！", gold = 300, xp = 400 });
            m.quests.Add(new ExQuest { id = "escort", kind = "escort", giver = "lost", title = "護送探險家", state = ExQuestState.Offered,
                desc = "把迷路的探險家平安送到前哨站", offer = "救、救命！我迷路了……可以帶我到前哨站嗎？我會緊緊跟著你的！",
                thanks = "（前哨站守衛）多虧你把他帶回來。這是前哨站的謝禮。", gold = 350, xp = 450 });
            m.quests.Add(new ExQuest { id = "herbs", kind = "collect", giver = "herbalist", title = "月光草", need = 3, state = ExQuestState.Offered,
                desc = "在東南區找到 3 株月光草，帶回給草藥師", offer = "月光草只在這片林子裡長，我需要 3 株來做解毒藥。你在路上看到發光的草就幫我摘吧！",
                thanks = "就是這個！這瓶藥水送你，危急時喝下吧。", gold = 300, xp = 400 });
            m.quests.Add(new ExQuest { id = "antidote", kind = "deliver", giver = "herbalist", target = "hunter", title = "送解毒藥", state = ExQuestState.Hidden,
                desc = "把解毒藥送回西南區給獵人巴克", offer = "對了——獵人巴克前幾天被毒液噴花傷到，一直沒好。這瓶解毒藥麻煩你送回去給他，他在西邊的營地附近。",
                thanks = "（獵人巴克）這是……莉亞的解毒藥！得救了。這把我年輕時用的弓弦送你。", gold = 250, xp = 500 });
            // 隱藏任務（閃光點觸發）
            m.quests.Add(new ExQuest { id = "h_chest", kind = "hidden", hiddenQuest = true, title = "埋藏的寶箱", desc = "發現埋起來的寶箱", state = ExQuestState.Hidden, gold = 400, xp = 200 });
            m.quests.Add(new ExQuest { id = "h_rare", kind = "hidden", hiddenQuest = true, title = "沉睡的古根", desc = "打倒被吵醒的古根撼地獸", need = 1, state = ExQuestState.Hidden, gold = 600, xp = 800 });
            m.quests.Add(new ExQuest { id = "h_spirit", kind = "hidden", hiddenQuest = true, title = "森林之靈的祝福", desc = "和森林之靈說話", state = ExQuestState.Hidden, gold = 200, xp = 500,
                offer = "人類啊，你身上有古樹的氣息。收下我的祝福吧——你的傷會痊癒，力量也會增強。", thanks = "願森林守護你。" });
            m.quests.Add(new ExQuest { id = "h_beast", kind = "hidden", hiddenQuest = true, title = "受傷的小獸", desc = "替受傷的小獸包紮", state = ExQuestState.Hidden, gold = 100, xp = 300 });
            m.quests.Add(new ExQuest { id = "h_passage", kind = "hidden", hiddenQuest = true, title = "藤牆後的密道", desc = "發現通往起點的密道", state = ExQuestState.Hidden, gold = 150, xp = 300 });

            // ---- 怪物
            int id = 0;
            // 小 Boss（叢林菁英）：站在競技場中央
            int[] guardKinds = { 28, 30, 32 };
            for (int g = 0; g < 3; g++) m.monsters.Add(new ExMonster { id = ++id, kind = guardKinds[g], zone = g, home = m.arenas[g].center, guardian = g + 1 });
            // 稀有怪（閃光點叫出來）
            m.monsters.Add(new ExMonster { id = ++id, kind = 19, zone = 1, home = m.World(37, 19), rare = true, hidden = true });
            // 營地（第一隻會叫同伴）
            void Camp(int camp, int zone, int cx, int cz, int[] kinds)
            {
                var offs = new[] { new Vector2Int(0, 0), new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(0, -1), new Vector2Int(1, -1) };
                for (int i = 0; i < kinds.Length; i++)
                    m.monsters.Add(new ExMonster { id = ++id, kind = kinds[i], zone = zone, camp = camp, caller = i == 0, home = m.World(cx + offs[i].x, cz + offs[i].y) });
            }
            Camp(0, 0, 5, 15, new[] { 9, 8, 10, 11 });       // 西南營地（獵人的任務：4 隻）
            Camp(1, 1, 33, 5, new[] { 9, 12, 7, 8 });
            Camp(2, 2, 35, 25, new[] { 9, 8, 12, 11, 10 });
            Camp(3, 2, 35, 33, new[] { 9, 7, 8, 12 });
            Camp(4, 3, 13, 25, new[] { 9, 12, 8, 11, 7 });
            // 巡邏的怪：每區固定數量，從迷宮走道挑（離起點、NPC、房間遠一點），在附近兩三格之間來回走
            int[][] zoneKinds = { new[] { 8, 10, 11, 12 }, new[] { 7, 9, 12, 8 }, new[] { 8, 9, 11, 12, 7 }, new[] { 7, 9, 10, 12, 8 } };
            int[] perZone = { 7, 8, 9, 8 };
            var zoneOrigin = new[] { new Vector2Int(1, 1), new Vector2Int(21, 1), new Vector2Int(21, 21), new Vector2Int(1, 21) };
            for (int z = 0; z < 4; z++)
            {
                int placed = 0, guard = 0;
                while (placed < perZone[z] && guard++ < 2000)
                {
                    int x = zoneOrigin[z].x + rng.Next(10) * 2, y = zoneOrigin[z].y + rng.Next(10) * 2;
                    if (m.At(x, y) != ExCell.Floor) continue;
                    var p = m.World(x, y);
                    if (p.magnitude < 15f || m.PropAt(p, 1f) != null) continue;
                    bool near = false;
                    foreach (var n in m.npcs) if (Vector3.Distance(n.pos, p) < 7f) near = true;
                    foreach (var o in m.monsters) if (Vector3.Distance(o.home, p) < 8f) near = true;
                    foreach (var a in m.arenas) if (Vector3.Distance(a.center, p) < a.radius + 4f) near = true;
                    if (near) continue;
                    var path = new List<Vector3> { p };
                    var cur = new Vector2Int(x, y);
                    for (int s = 0; s < 4; s++)
                    {
                        var opts = new List<Vector2Int>();
                        foreach (var d in ExploreMap.Dirs) { var nx = cur + d; if (m.At(nx.x, nx.y) == ExCell.Floor && !path.Contains(m.World(nx.x, nx.y)) && m.PropAt(m.World(nx.x, nx.y), 0.6f) == null) opts.Add(nx); }
                        if (opts.Count == 0) break;
                        cur = opts[rng.Next(opts.Count)]; path.Add(m.World(cur.x, cur.y));
                    }
                    m.monsters.Add(new ExMonster { id = ++id, kind = zoneKinds[z][placed % zoneKinds[z].Length], zone = z, home = p, patrol = path.ToArray() });
                    placed++;
                }
            }
            BuildSeals(m);
            return m;
        }

        /// <summary>
        /// 岔路解謎（使用者要求）：三個競技場、王座的入口加封印；機關／鑰匙／翠玉碎片放在各區「離入口最遠的死路」，
        /// 要探索岔路才找得到。翠玉碎片前三區各一片，湊齊才能進王座（漏掉就要回頭找）。
        /// </summary>
        static void BuildSeals(ExploreMap m)
        {
            ExSeal Seal(string id, string name, string hint, int x0, int z0, int x1, int z1, int need)
            {
                var s = new ExSeal { id = id, name = name, hint = hint, need = need, rect = new RectInt(x0, z0, x1 - x0, z1 - z0) };
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                {
                    if (x != x0 && x != x1 && z != z0 && z != z1) continue;   // 房間的邊
                    bool door = false;
                    foreach (var d in ExploreMap.Dirs) { int nx = x + d.x, nz = z + d.y; bool outside = nx < x0 || nx > x1 || nz < z0 || nz > z1; if (outside && m.At(nx, nz) == ExCell.Floor) door = true; }
                    if (!door || m.cells[x, z] != ExCell.Floor) continue;
                    m.cells[x, z] = ExCell.Gate; s.cells.Add(new Vector2Int(x, z));
                }
                m.seals.Add(s); return s;
            }
            Seal("seal1", "苔根封印", "巢穴入口被粗大的苔根封住了，根連著一條石槽……岔路深處應該有機關。", 13, 7, 19, 11, 1);
            Seal("seal2", "寨主之鎖", "祭壇的入口掛著一把古銅大鎖。鑰匙應該被藏在這一區的岔路裡。", 27, 13, 35, 19, 1);
            Seal("seal3", "雙獵封印", "獵場被兩道藤蔓結封住，要找到兩個機關才打得開。", 21, 27, 27, 35, 2);
            Seal("seal4", "翠玉封印", "王座的大門刻著三個凹槽——要放進三片翠玉碎片。前面三個區域各藏著一片。", 1, 31, 9, 39, 3);

            // 各區離入口最遠的死路（避開 NPC、道具、閃光點、景物、房間）
            List<Vector2Int> DeadEnds(Vector2Int entry, int x0, int z0, int x1, int z1)
            {
                var dist = new Dictionary<Vector2Int, int>(); var q = new Queue<Vector2Int>(); q.Enqueue(entry); dist[entry] = 0;
                while (q.Count > 0) { var c = q.Dequeue(); foreach (var d in ExploreMap.Dirs) { var n = c + d; if (n.x < x0 || n.y < z0 || n.x > x1 || n.y > z1 || dist.ContainsKey(n) || m.At(n.x, n.y) != ExCell.Floor) continue; dist[n] = dist[c] + 1; q.Enqueue(n); } }
                var list = new List<Vector2Int>();
                foreach (var kv in dist)
                {
                    var c = kv.Key; int open = 0; bool nearGate = false;
                    foreach (var d in ExploreMap.Dirs) { if (m.Open(c.x + d.x, c.y + d.y)) open++; if (m.At(c.x + d.x, c.y + d.y) == ExCell.Gate) nearGate = true; }
                    if (open != 1 || nearGate) continue;   // 真正的死路（不是被封印擋住的入口）
                    var p = m.World(c.x, c.y); bool busy = m.PropAt(p, 1.5f) != null;
                    foreach (var n in m.npcs) if (Battle.FlatDistance(n.pos, p) < 4f) busy = true;
                    foreach (var it in m.items) if (Battle.FlatDistance(it.pos, p) < 4f) busy = true;
                    foreach (var sp in m.sparkles) if (Battle.FlatDistance(sp.pos, p) < 4f) busy = true;
                    foreach (var l in m.landmarks) if (l.Contains(c)) busy = true;
                    foreach (var sw in m.switches) if (Battle.FlatDistance(sw.pos, p) < 12f) busy = true;
                    if (!busy) list.Add(c);
                }
                list.Sort((a, b) => dist[b].CompareTo(dist[a]));
                return list;
            }
            void Switch(string id, string kind, string name, string seal, int zone, Vector2Int c) => m.switches.Add(new ExSwitch { id = id, kind = kind, name = name, seal = seal, zone = zone, pos = m.World(c.x, c.y) });
            Vector2Int Next(List<Vector2Int> l) { foreach (var c in l) { var p = m.World(c.x, c.y); bool used = false; foreach (var sw in m.switches) if (Battle.FlatDistance(sw.pos, p) < 12f) used = true; if (!used) return c; } return l[0]; }
            var z1 = DeadEnds(m.start, 1, 1, 19, 19);
            Switch("lever1", "lever", "石雕機關", "seal1", 0, Next(z1));
            Switch("shard1", "shard", "翠玉碎片", "seal4", 0, Next(z1));
            var z2 = DeadEnds(new Vector2Int(21, 9), 21, 1, 39, 19);
            Switch("key2", "key", "古銅鑰匙", "seal2", 1, Next(z2));
            Switch("shard2", "shard", "翠玉碎片", "seal4", 1, Next(z2));
            var z3 = DeadEnds(new Vector2Int(31, 21), 21, 21, 39, 39);
            Switch("lever3a", "lever", "左獵石機關", "seal3", 2, Next(z3));
            Switch("lever3b", "lever", "右獵石機關", "seal3", 2, Next(z3));
            Switch("shard3", "shard", "翠玉碎片", "seal4", 2, Next(z3));
        }

        /// <summary>某個區域（19×19 格，左下角 ox,oz）用回溯法挖迷宮，再多打通一些牆做成環路（不會只有死路）</summary>
        static void Carve(ExploreMap m, System.Random rng, int ox, int oz)
        {
            const int Cells = 10;
            var seen = new bool[Cells, Cells];
            var stack = new Stack<Vector2Int>();
            stack.Push(new Vector2Int(0, 0)); seen[0, 0] = true; m.cells[ox, oz] = ExCell.Floor;
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                var opts = new List<Vector2Int>();
                foreach (var d in ExploreMap.Dirs) { var n = c + d; if (n.x >= 0 && n.y >= 0 && n.x < Cells && n.y < Cells && !seen[n.x, n.y]) opts.Add(n); }
                if (opts.Count == 0) { stack.Pop(); continue; }
                var nx = opts[rng.Next(opts.Count)]; seen[nx.x, nx.y] = true;
                m.cells[ox + nx.x * 2, oz + nx.y * 2] = ExCell.Floor;
                m.cells[ox + c.x * 2 + (nx.x - c.x), oz + c.y * 2 + (nx.y - c.y)] = ExCell.Floor;
                stack.Push(nx);
            }
            // 環路：每區多打通 14 面牆
            for (int k = 0, tries = 0; k < 14 && tries < 500; tries++)
            {
                int x = ox + rng.Next(19), z = oz + rng.Next(19);
                if (m.cells[x, z] != ExCell.Wall) continue;
                bool h = (x - ox) % 2 == 1 && (z - oz) % 2 == 0, v = (x - ox) % 2 == 0 && (z - oz) % 2 == 1;
                if (!h && !v) continue;
                m.cells[x, z] = ExCell.Floor; k++;
            }
        }

        /// <summary>
        /// 地標（使用者要求：湖泊、營地、城寨、山洞、岩石地形、森林宮殿、各種植物、祭壇；景物都是障礙物）：
        /// 在迷宮上疊地形（牆變成岩壁、湖水、木牆、石牆…）、地面、景物障礙。每個地標都要保持通路（測試會檢查）。
        /// </summary>
        static void BuildLandmarks(ExploreMap m)
        {
            void Mark(string id, string name, string kind, int x0, int z0, int x1, int z1) => m.landmarks.Add(new ExLandmark { id = id, name = name, kind = kind, rect = new RectInt(x0, z0, x1 - x0, z1 - z0) });
            void Terrain(int x0, int z0, int x1, int z1, ExTerrain t, ExGround g)
            {
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                {
                    if (!m.InGrid(x, z)) continue;
                    if (m.cells[x, z] == ExCell.Wall) m.terrain[x, z] = t; else m.ground[x, z] = g;
                }
            }
            void Block(int x, int z, ExTerrain t) { m.cells[x, z] = ExCell.Wall; m.terrain[x, z] = t; }
            void Prop(string kind, int x, int z, float r, float ox = 0f, float oz = 0f, int v = 0) => m.AddProp(kind, m.World(x, z) + new Vector3(ox, 0f, oz), r, v);

            // 起點：林語營地（篝火、木箱）
            Mark("start", "林語營地", "start", 1, 1, 5, 5);
            Terrain(1, 1, 5, 5, ExTerrain.Thicket, ExGround.Dirt);
            Prop("campfire", 2, 2, 0.45f); Prop("crate", 1, 1, 0.45f, -0.3f, -0.3f); Prop("crate", 5, 1, 0.45f, 0.3f, -0.3f); Prop("tent", 1, 5, 1f, -0.2f, 0.2f);
            // 月影湖：湖面（走不過去）＋一圈湖岸
            Room(m, 9, 1, 17, 5);
            Mark("lake", "月影湖", "lake", 9, 1, 17, 5);
            Terrain(9, 1, 17, 5, ExTerrain.Thicket, ExGround.Shore);
            for (int x = 11; x <= 15; x++) for (int z = 2; z <= 4; z++) Block(x, z, ExTerrain.Water);
            Prop("tree", 9, 1, 0.6f, -0.4f, -0.4f, 1); Prop("tree", 17, 5, 0.6f, 0.4f, 0.4f, 2); Prop("stump", 17, 1, 0.4f, 0.4f, -0.4f);
            // 藤蔓營地（獵人的任務）
            Mark("camp0", "藤蔓營地", "camp", 3, 13, 7, 17);
            Terrain(3, 13, 7, 17, ExTerrain.Thicket, ExGround.Dirt);
            Prop("tent", 3, 17, 1f, -0.3f, 0.3f); Prop("tent", 7, 17, 1f, 0.3f, 0.3f); Prop("totem", 3, 13, 0.3f, -0.6f, -0.6f); Prop("campfire", 7, 13, 0.4f, 0.4f, -0.4f);
            // 碎岩谷：岩石地形（四周是岩壁、地上散落巨石）
            Room(m, 21, 10, 25, 15);
            Mark("rocks", "碎岩谷", "rocks", 21, 9, 25, 15);
            Terrain(20, 8, 26, 16, ExTerrain.Rock, ExGround.Dirt);
            Prop("boulder", 22, 11, 0.7f, 0.2f, 0f, 0); Prop("boulder", 24, 13, 0.8f, 0f, 0.3f, 1); Prop("boulder", 22, 15, 0.6f, -0.3f, 0.3f, 2); Prop("boulder", 25, 10, 0.5f, 0.6f, -0.2f, 1);
            // 藤牙寨：敵人的城寨（木樁圍牆，西、北兩個寨門）
            Room(m, 29, 1, 37, 9);
            Mark("fort", "藤牙寨", "fort", 29, 1, 37, 9);
            Terrain(29, 1, 37, 9, ExTerrain.Palisade, ExGround.Dirt);
            for (int x = 30; x <= 36; x++) { Block(x, 2, ExTerrain.Palisade); Block(x, 8, ExTerrain.Palisade); }
            for (int z = 2; z <= 8; z++) { Block(30, z, ExTerrain.Palisade); Block(36, z, ExTerrain.Palisade); }
            m.cells[30, 5] = ExCell.Floor; m.cells[33, 8] = ExCell.Floor;   // 寨門
            Prop("tent", 31, 7, 0.9f, -0.2f, 0.2f); Prop("crate", 35, 3, 0.45f, 0.3f, -0.3f); Prop("totem", 35, 7, 0.3f, 0.5f, 0.5f);
            // 巨菇林（東北營地）
            Mark("grove", "巨菇林", "grove", 33, 23, 37, 27);
            Terrain(33, 23, 37, 27, ExTerrain.Thicket, ExGround.Grass);
            Prop("mushroom", 33, 23, 0.6f, -0.4f, -0.4f, 0); Prop("mushroom", 37, 23, 0.7f, 0.4f, -0.4f, 1); Prop("mushroom", 33, 27, 0.5f, -0.4f, 0.4f, 2); Prop("mushroom", 37, 27, 0.6f, 0.4f, 0.4f, 0);
            // 回音洞窟：一整片岩壁圍成的洞窟（走道就是洞窟通道，營地是洞窟大廳）
            Mark("cave", "回音洞窟", "cave", 29, 29, 39, 39);
            Terrain(28, 28, 40, 40, ExTerrain.Cave, ExGround.Cave);
            Prop("boulder", 33, 35, 0.5f, -0.5f, 0.5f, 2); Prop("boulder", 37, 31, 0.5f, 0.5f, -0.5f, 0);
            // 枯骨營地（西北）
            Mark("camp4", "枯骨營地", "camp", 11, 23, 15, 27);
            Terrain(11, 23, 15, 27, ExTerrain.Thicket, ExGround.Dirt);
            Prop("totem", 11, 27, 0.3f, -0.6f, 0.6f); Prop("totem", 15, 27, 0.3f, 0.6f, 0.6f); Prop("tent", 11, 23, 0.9f, -0.3f, -0.3f);
            // 翠玉古殿：森林宮殿的廢墟（石牆、石板地、石柱），最裡面是王座與祭壇
            Mark("palace", "翠玉古殿", "palace", 1, 29, 13, 39);
            Terrain(0, 28, 13, 40, ExTerrain.Ruin, ExGround.Stone);
            Prop("pillar", 2, 32, 0.45f); Prop("pillar", 8, 32, 0.45f); Prop("pillar", 2, 37, 0.45f); Prop("pillar", 8, 37, 0.45f);
            Prop("statue", 1, 39, 0.5f, -0.4f, 0.4f); Prop("statue", 9, 39, 0.5f, 0.4f, 0.4f);
            Prop("altar", 5, 39, 0.6f, 0f, 0.4f);
            // 小 Boss 競技場：四個角落的大樹
            foreach (var r in new[] { new RectInt(13, 7, 6, 4), new RectInt(27, 13, 8, 6), new RectInt(21, 27, 6, 8) })
            {
                Prop("tree", r.xMin, r.yMin, 0.6f, -0.4f, -0.4f, 1); Prop("tree", r.xMax, r.yMin, 0.6f, 0.4f, -0.4f, 2);
                Prop("tree", r.xMin, r.yMax, 0.6f, -0.4f, 0.4f, 0); Prop("tree", r.xMax, r.yMax, 0.6f, 0.4f, 0.4f, 1);
            }
            Mark("arena1", "苔甲守根者的巢穴", "arena", 13, 7, 19, 11);
            Mark("arena2", "夢孢司祭的祭壇", "arena", 27, 13, 35, 19);
            Mark("arena3", "翠翼巡獵者的獵場", "arena", 21, 27, 27, 35);
        }

        static void Room(ExploreMap m, int x0, int z0, int x1, int z1)
        {
            for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) m.cells[x, z] = ExCell.Floor;
        }
        static void Ensure(ExploreMap m, Vector3 p) { var c = m.Cell(p); m.cells[c.x, c.y] = ExCell.Floor; }
    }

    /// <summary>
    /// 一次探索的進行狀態（純邏輯）：任務、門、拿到的東西、要說的台詞。SoloBattle 每幀把玩家位置丟進來，
    /// 戰鬥（Combat.Explore.cs）通知擊倒、小 Boss、Boss；這裡回傳事件給畫面顯示。
    /// </summary>
    public class ExploreRun
    {
        public readonly ExploreMap map;
        public readonly List<string> log = new List<string>();      // 這一幀新的訊息（畫面拿走後清掉）
        public readonly List<string> rewards = new List<string>();  // 已完成任務的獎勵說明
        public bool hasHeart, bossDead, finished, escortFollowing;
        public int gold, xp;
        public ExNpc talking;                // 現在正在說話的 NPC（畫面跳對話框）
        public string talkText;
        public bool spiritBlessing;          // 森林之靈的祝福（戰鬥那邊補血、加攻擊）
        public readonly HashSet<Vector2Int> explored = new HashSet<Vector2Int>();   // 小地圖：走過的格子附近
        public const float TalkRange = 2.2f, PickRange = 1.4f, SparkleTapRange = 6f, EscortArrive = 3f;
        readonly HashSet<string> talkedAway = new HashSet<string>();   // 離開說話範圍後才會再跳一次

        public ExploreRun(ExploreMap m) { map = m; }

        public ExQuest Main => map.Quest("main");
        public int GuardiansDown { get { int n = 0; foreach (var a in map.arenas) if (a.n <= 3 && a.cleared) n++; return n; } }

        void Say(string s) => log.Add(s);

        /// <summary>每幀：玩家位置（戰場座標）、護送對象的位置（沒有＝null）</summary>
        public void Update(Vector3 player, Vector3? escortPos)
        {
            // 小地圖：走過的地方附近兩格
            var pc = map.Cell(player);
            for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++) explored.Add(new Vector2Int(pc.x + dx, pc.y + dz));
            // NPC：走近就說話（離開再回來才會再說）
            foreach (var n in map.npcs)
            {
                if (n.hidden) continue;
                if (n.id == "lost" && escortFollowing) continue;
                float d = Battle.FlatDistance(player, n.pos);
                if (d > TalkRange + 1f) talkedAway.Remove(n.id);
                if (d <= TalkRange && !talkedAway.Contains(n.id)) { talkedAway.Add(n.id); Talk(n); }
            }
            // 撿東西
            foreach (var it in map.items)
            {
                if (it.taken || it.hidden || Battle.FlatDistance(player, it.pos) > PickRange) continue;
                it.taken = true;
                var q = map.Quest(it.quest);
                if (q != null) { q.have = Mathf.Min(q.need, q.have + 1); Say($"🌿 撿到{it.name}（{q.have}/{q.need}）"); if (q.state == ExQuestState.Active && q.have >= q.need) { q.state = ExQuestState.Ready; Say($"「{q.title}」可以回報了！"); } }
            }
            // 古樹之心
            if (bossDead && !hasHeart && Battle.FlatDistance(player, map.altar) <= PickRange + 0.6f) { hasHeart = true; Say("💚 取得「古樹之心」！交給守林人吧"); var m = Main; if (m.state == ExQuestState.Active) m.state = ExQuestState.Ready; }
            // 護送：探險家到前哨站附近
            var esc = map.Quest("escort");
            if (esc.state == ExQuestState.Active && escortPos.HasValue && Battle.FlatDistance(escortPos.Value, map.Npc("outpost").pos) <= EscortArrive)
            { escortFollowing = false; Complete(esc); map.Npc("lost").pos = map.Npc("outpost").pos + new Vector3(1.2f, 0f, 0f); }
            // 岔路的機關、鑰匙、翠玉碎片：走到旁邊就啟動／撿起來
            foreach (var sw in map.switches)
            {
                if (sw.done || Battle.FlatDistance(player, sw.pos) > PickRange + 0.3f) continue;
                sw.done = true;
                var seal = map.seals.Find(s => s.id == sw.seal);
                seal.have++;
                Say(sw.kind == "lever" ? $"⚙ 拉動了「{sw.name}」！（{seal.name} {seal.have}/{seal.need}）" : $"🔑 拿到「{sw.name}」！（{seal.name} {seal.have}/{seal.need}）");
                if (seal.have >= seal.need && !seal.open) { seal.open = true; Say($"🔓 {seal.name}解開了！"); }
            }
            // 地標：走進去＝發現（小地圖標名字）
            foreach (var l in map.landmarks) if (!l.found && l.Contains(pc)) { l.found = true; if (l.kind != "start" && l.kind != "arena") Say($"📍 發現：{l.name}"); }
            // 競技場：走進去
            foreach (var a in map.arenas) if (!a.entered && Battle.FlatDistance(player, a.center) <= a.radius - 1f) { a.entered = true; Say(a.n <= 3 ? $"⚔ {a.name}——守衛發現你了！" : $"👑 {a.name}——腐化樹王醒來了！"); }
        }

        /// <summary>和 NPC 說話：接任務、回報任務</summary>
        public void Talk(ExNpc n)
        {
            talking = n; talkText = null;
            if (n.id == "outpost") { var e = map.Quest("escort"); talkText = e.state == ExQuestState.Done ? e.thanks : "這裡是前哨站。林子裡很危險，迷路的人常常回不來……"; return; }
            if (n.id == "spirit") { var q = map.Quest("h_spirit"); if (q.state != ExQuestState.Done) { talkText = q.offer; spiritBlessing = true; Complete(q); } else talkText = q.thanks; return; }
            if (n.id == "guardian")
            {
                var m = Main;
                if (hasHeart && m.state != ExQuestState.Done) { talkText = m.thanks; Complete(m); finished = true; Say("🏆 探索完成！"); return; }
                talkText = bossDead ? "古樹之心就在王座後面的祭壇上，快去拿！" : GuardiansDown < 3 ? "前面的藤蔓門被守衛的力量封住了，先打倒三個守衛。" : "腐化樹王就在前面的王座。打倒牠，把古樹之心帶給我。";
                return;
            }
            // 送東西：送到的人
            foreach (var dq in map.quests)
                if (dq.kind == "deliver" && dq.state == ExQuestState.Active && dq.target == n.id) { talkText = dq.thanks; Complete(dq); return; }
            var qq = map.Quest(n.quest);
            if (qq == null) return;
            switch (qq.state)
            {
                case ExQuestState.Offered:
                    qq.state = ExQuestState.Active; talkText = qq.offer;
                    Say($"📜 接受任務「{qq.title}」：{qq.desc}");
                    if (qq.kind == "escort") escortFollowing = true;
                    if (qq.kind == "collect" && qq.have >= qq.need) qq.state = ExQuestState.Ready;   // 先撿好了
                    break;
                case ExQuestState.Active: talkText = $"「{qq.title}」：{qq.desc}（{qq.Progress}）"; break;
                case ExQuestState.Ready: talkText = qq.thanks; Complete(qq); break;
                case ExQuestState.Failed: qq.state = ExQuestState.Active; qq.have = 0; talkText = qq.offer; if (qq.kind == "escort") escortFollowing = true; break;
                case ExQuestState.Done: talkText = qq.thanks; break;
            }
        }

        public void CloseTalk() { talking = null; talkText = null; }

        void Complete(ExQuest q)
        {
            if (q.state == ExQuestState.Done) return;
            q.state = ExQuestState.Done; gold += q.gold; xp += q.xp;
            rewards.Add($"{q.title}：{q.gold} 金幣、{q.xp} 經驗");
            Say($"✅ 完成「{q.title}」（+{q.gold} 金幣、+{q.xp} 經驗）");
            // 草藥做好了：接著請你把解毒藥送回西南區（跨區送東西）
            if (q.id == "herbs") { var a = map.Quest("antidote"); if (a != null && a.state == ExQuestState.Hidden) { a.state = ExQuestState.Active; Say($"📜 接受任務「{a.title}」：{a.desc}"); if (talkText != null) talkText += "\n\n" + a.offer; } }
        }

        /// <summary>戰鬥：這隻怪被打倒</summary>
        public void OnMonsterKilled(ExMonster mon)
        {
            if (mon.dead) return;
            mon.dead = true;
            var camp = map.Quest("camp");
            if (mon.camp == 0) { camp.have = Mathf.Min(camp.need, camp.have + 1); if (camp.state == ExQuestState.Active && camp.have >= camp.need) { camp.state = ExQuestState.Ready; Say("營地清除了！回去找獵人巴克"); } }
            if (mon.guardian > 0)
            {
                var a = map.arenas.Find(x => x.n == mon.guardian); a.cleared = true;
                Say($"🌿 守衛倒下了，擋路的藤蔓退開了！（守衛 {GuardiansDown}/3）");
            }
            if (mon.rare) { var q = map.Quest("h_rare"); q.have = 1; Complete(q); }
        }

        /// <summary>戰鬥：最終 Boss 被打倒</summary>
        public void OnBossKilled()
        {
            if (bossDead) return;
            bossDead = true; map.arenas.Find(x => x.n == 4).cleared = true;
            Say("👑 腐化樹王倒下了！王座後面的祭壇在發光……");
        }

        /// <summary>護送對象倒下：任務失敗，回去找他重新開始</summary>
        public void OnEscortDown()
        {
            var e = map.Quest("escort");
            if (e.state != ExQuestState.Active) return;
            e.state = ExQuestState.Failed; escortFollowing = false;
            Say("❌ 探險家倒下了……（回到他原本的地方可以重新接任務）");
        }

        /// <summary>點閃光點（距離夠近才算）：回傳發現的閃光點；沒有＝null</summary>
        public ExSparkle TapSparkle(string id, Vector3 player)
        {
            var s = map.sparkles.Find(x => x.id == id);
            if (s == null || s.found || Battle.FlatDistance(player, s.pos) > SparkleTapRange) return null;
            s.found = true;
            Say($"✨ {s.title}：{s.text}");
            switch (s.kind)
            {
                case "chest": Complete(map.Quest("h_chest")); break;
                case "beast": Complete(map.Quest("h_beast")); break;
                case "rare": { var r = map.monsters.Find(x => x.rare); r.hidden = false; var q = map.Quest("h_rare"); q.state = ExQuestState.Active; Say("📜 隱藏任務「沉睡的古根」：打倒古根撼地獸"); break; }
                case "spirit": { map.Npc("spirit").hidden = false; map.Quest("h_spirit").state = ExQuestState.Active; break; }
                case "passage":
                    for (int i = 0; i + 1 < s.opens.Length; i += 2) map.cells[s.opens[i], s.opens[i + 1]] = ExCell.Floor;
                    Complete(map.Quest("h_passage"));
                    break;
            }
            return s;
        }

        /// <summary>任務清單（畫面顯示）：進行中、可回報的在前面；隱藏任務被發現前不顯示</summary>
        public IEnumerable<ExQuest> VisibleQuests()
        {
            foreach (var q in map.quests) if (q.state == ExQuestState.Active || q.state == ExQuestState.Ready || q.state == ExQuestState.Failed) yield return q;
            foreach (var q in map.quests) if (q.state == ExQuestState.Done) yield return q;
        }

        /// <summary>主線現在要做什麼（畫面上方的目標）</summary>
        public string Objective
        {
            get
            {
                var m = Main;
                if (m.state == ExQuestState.Offered) return "和林語長老說話";
                if (m.state == ExQuestState.Done) return "探索完成！";
                if (GuardiansDown < 3)
                {
                    var next = map.seals.Find(s => s.id == "seal" + (GuardiansDown + 1));
                    if (next != null && !next.open) return $"找到打開「{next.name}」的{(next.need > 1 ? $"機關（{next.have}/{next.need}）" : next.id == "seal2" ? "鑰匙" : "機關")}——在這一區的岔路深處";
                    return $"打倒守衛，打開藤蔓門（{GuardiansDown}/3）";
                }
                var jade = map.seals.Find(s => s.id == "seal4");
                if (jade != null && !jade.open) return $"湊齊翠玉碎片，解開王座的封印（{jade.have}/3）——前面三個區域的岔路各藏一片";
                if (!bossDead) return "前往王座，打倒腐化樹王";
                if (!hasHeart) return "到王座後面的祭壇取得古樹之心";
                return "把古樹之心交給守林人";
            }
        }
    }
}
