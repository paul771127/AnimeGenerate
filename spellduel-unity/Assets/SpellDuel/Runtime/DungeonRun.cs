using System;
using System.Collections.Generic;

namespace SpellDuel
{
    /// <summary>一個劇情人物在這次副本裡的狀態</summary>
    public class NpcState
    {
        public DungeonNpc def;
        /// <summary>escort 保護目標、ally 友軍、enemy 敵人、third 第三勢力、quest 任務人物、pending 等房主做抉擇</summary>
        public string side;
        public bool dead, done, safe;
        public int have;   // 任務進度
        /// <summary>會在戰場上出現、會打架（任務人物只在旁邊說話）</summary>
        public bool Fights => !dead && !safe && (side == "escort" || side == "ally" || side == "enemy" || side == "third");
        public string GoalText
        {
            get
            {
                var n = def;
                if (n.goal == "kill") return $"打倒 {n.need} 隻敵人（{have}/{n.need}）";
                if (n.goal == "collect") return $"收集{n.goalItem}（{have}/{n.need}）";
                if (n.goal == "reach") return $"前往指定的地點（{(done ? "已抵達" : "未抵達")}）";
                return "";
            }
        }
    }

    /// <summary>
    /// 一次副本的進行狀態（純邏輯，不碰 Unity，可離線測試）：
    ///   現在在哪個地點、走過的路、劇情人物（保護目標、抉擇、任務）的狀態、要顯示的台詞。
    ///   連線時房主決定（下一站、抉擇、任務進度、人物倒下），隊友收到訊息後在自己的 DungeonRun 套用同樣的事件。
    /// </summary>
    public class DungeonRun
    {
        public readonly DungeonDef def;
        public DungeonRoom node;
        public readonly List<DungeonRoom> path = new List<DungeonRoom>();
        public readonly Dictionary<string, NpcState> npcs = new Dictionary<string, NpcState>();
        /// <summary>要顯示的台詞（SoloBattle 拿去當字幕卡、放進對話窗）</summary>
        public readonly Queue<StoryLine> lines = new Queue<StoryLine>();
        /// <summary>剛完成的任務（給獎勵）</summary>
        public readonly List<NpcState> justDone = new List<NpcState>();
        /// <summary>等房主做抉擇的人物（null＝沒有）</summary>
        public NpcState pendingChoice;
        public bool failed; public string failText;
        int theme;

        public DungeonRun(DungeonDef d)
        {
            def = d;
            if (d.rooms.Count > 0) Enter(d.rooms[0]);
        }

        public List<DungeonRoom> Exits => def.ExitsOf(node);
        public bool AtEnd => Exits.Count == 0;
        /// <summary>這個地點的場景主題（寶箱沒指定就沿用上一個地點的場景）</summary>
        public int Theme => theme;
        public int Step => path.Count;

        /// <summary>進到一個地點：台詞、保護目標安全抵達、抵達型任務、新登場的人物</summary>
        public void Enter(DungeonRoom r)
        {
            if (r == null) return;
            node = r; path.Add(r);
            theme = r.theme >= 0 || !r.IsChest || path.Count == 1 ? def.ThemeOf(r) : theme;
            if (r.enter.Count > 0) foreach (var l in r.enter) lines.Enqueue(l);
            else if (!string.IsNullOrEmpty(r.line)) lines.Enqueue(new StoryLine { speaker = "", text = r.line });
            foreach (var st in npcs.Values)
            {
                if (st.side == "escort" && !st.dead && !st.safe && st.def.until == r.id)
                { st.safe = true; foreach (var l in st.def.safe) lines.Enqueue(l); }
                if (st.side == "quest" && !st.done && st.def.goal == "reach" && st.def.goalNode == r.id) Progress(st, 1);
            }
            foreach (var id in r.npcs)
            {
                if (npcs.ContainsKey(id)) continue;
                var n = def.Npc(id);
                if (n == null) continue;
                var st = new NpcState { def = n, side = n.role == "choice" ? "pending" : n.role };
                npcs[id] = st;
                foreach (var l in n.meet) lines.Enqueue(l);
                if (n.role == "choice") pendingChoice = st;
            }
        }

        /// <summary>房主選了抉擇的選項（第幾個）</summary>
        public void Choose(int option)
        {
            var st = pendingChoice;
            if (st == null) return;
            pendingChoice = null;
            option = Math.Max(0, Math.Min(st.def.options.Count - 1, option));
            st.side = st.def.options.Count > 0 ? st.def.options[option].result : "ally";
            var ls = st.def.LinesOf(st.side);
            if (ls != null) foreach (var l in ls) lines.Enqueue(l);
        }

        /// <summary>清完這個地點的戰鬥</summary>
        public void Cleared() { foreach (var l in node.clear) lines.Enqueue(l); }

        /// <summary>房主：打倒了 n 隻敵人（打倒型任務、收集型任務每隻 30% 掉一個）。回傳有沒有進度變化</summary>
        public bool OnKills(int n, Random rng)
        {
            bool changed = false;
            foreach (var st in npcs.Values)
            {
                if (st.side != "quest" || st.done) continue;
                if (st.def.goal == "kill") { Progress(st, n); changed = true; }
                else if (st.def.goal == "collect") for (int i = 0; i < n; i++) if (rng.NextDouble() < CollectChance) { Progress(st, 1); changed = true; }
            }
            return changed;
        }
        public const double CollectChance = 0.3;

        /// <summary>打開寶箱：收集型任務 +1</summary>
        public bool OnChest()
        {
            bool changed = false;
            foreach (var st in npcs.Values) if (st.side == "quest" && !st.done && st.def.goal == "collect") { Progress(st, 1); changed = true; }
            return changed;
        }

        /// <summary>隊友端：套用房主送來的任務進度</summary>
        public void SetProgress(string id, int have)
        {
            if (npcs.TryGetValue(id, out var st) && have > st.have) Progress(st, have - st.have);
        }

        void Progress(NpcState st, int add)
        {
            if (st.done) return;
            st.have = Math.Min(st.def.need, st.have + add);
            if (st.have < st.def.need) return;
            st.done = true; justDone.Add(st);
            foreach (var l in st.def.done) lines.Enqueue(l);
        }

        /// <summary>劇情人物倒下：保護目標（還沒抵達安全地點）＝副本失敗</summary>
        public void NpcDied(string id)
        {
            if (!npcs.TryGetValue(id, out var st) || st.dead) return;
            st.dead = true;
            foreach (var l in st.def.die) lines.Enqueue(l);
            if (st.side == "escort" && !st.safe) { failed = true; failText = $"{st.def.name}倒下了…副本失敗"; }
        }

        /// <summary>第三勢力：這個地點打完就撤退（不用打倒）</summary>
        public void ThirdLeaves() { foreach (var st in npcs.Values) if (st.side == "third" && !st.dead) st.safe = true; }

        public IEnumerable<NpcState> Fighting { get { foreach (var st in npcs.Values) if (st.Fights) yield return st; } }
        public IEnumerable<NpcState> Quests { get { foreach (var st in npcs.Values) if (st.side == "quest") yield return st; } }
    }

    /// <summary>
    /// 寶箱的擲骰子：只有稀有以上（史詩、傳說）的物品要擲；每個人選「擲骰」或「放棄」，
    /// 擲 1～100，點數最大的拿走；最大點平手的人重擲，直到分出勝負。全部放棄＝沒人拿。
    /// </summary>
    public static class DiceRoll
    {
        public const int MinRollRarity = 3;   // 史詩以上才要擲骰（稀有以下每個人各自拿自己的）
        /// <summary>寶箱裡的大獎：45% 有一件（85% 史詩、15% 傳說）；0＝沒有</summary>
        public static int ChestRarity(Random rng) => rng.NextDouble() < 0.45 ? (rng.NextDouble() < 0.15 ? 4 : 3) : 0;

        /// <summary>擲到分出勝負：回傳贏家（沒人參加＝-1），rounds 記錄每一輪每個人擲的點數</summary>
        public static int Resolve(List<int> players, Func<int> roll, List<List<(int pid, int roll)>> rounds = null)
        {
            var left = new List<int>(players);
            for (int guard = 0; left.Count > 0 && guard < 50; guard++)
            {
                if (left.Count == 1 && guard > 0) return left[0];
                var r = new List<(int, int)>();
                int best = -1;
                foreach (var p in left) { int v = roll(); r.Add((p, v)); if (v > best) best = v; }
                rounds?.Add(r);
                var top = new List<int>();
                foreach (var (p, v) in r) if (v == best) top.Add(p);
                if (top.Count == 1) return top[0];
                left = top;   // 最大點平手：這幾個人重擲
            }
            return left.Count > 0 ? left[0] : -1;
        }

        /// <summary>把擲骰紀錄變成一行一行的文字：「第 1 輪：A 87、B 87 → 平手重擲」</summary>
        public static List<string> Describe(List<List<(int pid, int roll)>> rounds, Func<int, string> name)
        {
            var o = new List<string>();
            for (int i = 0; i < rounds.Count; i++)
            {
                var parts = new List<string>();
                foreach (var (p, v) in rounds[i]) parts.Add($"{name(p)} {v}");
                o.Add($"{(rounds.Count > 1 ? $"第 {i + 1} 輪：" : "")}{string.Join("、", parts)}{(i < rounds.Count - 1 ? " → 平手重擲" : "")}");
            }
            return o;
        }
    }
}
