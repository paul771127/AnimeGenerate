using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>副本的一個房間：打第幾波（決定場景主題、小怪種類與強度；10 的倍數＝Boss 波；51 波起主題重新輪一次；房間可以用 theme 指定場景），進房間時的旁白</summary>
    public class DungeonRoom
    {
        public int wave;
        public int theme = -1;   // 指定場景主題（−1＝照波次算）：用高波次的強度、保留原本的場景
        public string title, line;
        /// <summary>進房間、清完房間、首領剩一半血時的台詞（戰鬥中以字幕卡顯示，不暫停）</summary>
        public readonly List<StoryLine> enter = new List<StoryLine>(), clear = new List<StoryLine>(), bossHalf = new List<StoryLine>();
    }

    /// <summary>
    /// 多人副本（參考魔獸世界的副本：一段故事、幾個房間、最後的首領）。
    ///   ・在同一塊場地打：清完一個房間（一波）就換下一個房間的場景，房間可以跨主題（熔岩、森林、海上）。
    ///   ・浮動難度：最少以 2 人計算，敵人數量、Boss 血量 ×人數（3 人 ×3、4 人 ×4）；人越多掉寶率越高（最多 ×1.5）。
    ///     一個人在同等級幾乎過不了；角色等級比副本高很多時敵人才會變弱（LevelRelief），
    ///     那時候副本掉的套裝等級（＝副本等級）已經遠低於角色等級。
    ///   ・通關：每個房間保底一件稀有以上的裝備（物品等級＝副本等級），最後的首領保底一件史詩套裝。
    /// </summary>
    public class DungeonDef
    {
        public string id, name, desc;
        public int level;                          // 副本等級（掉落的物品等級、難度基準）
        public int minPlayers = 2, maxPlayers = 4; // 建議人數
        public string unlock;                      // 要先通關的故事關卡（例如 2-3；null＝一開始就開放）
        public bool Unlocked => string.IsNullOrEmpty(unlock) || Story.IsCleared(unlock);
        public readonly List<DungeonRoom> rooms = new List<DungeonRoom>();
        public readonly List<StoryLine> before = new List<StoryLine>();
        public readonly List<StoryLine> after = new List<StoryLine>();
        public int[] Waves { get { var w = new int[rooms.Count]; for (int i = 0; i < w.Length; i++) w[i] = rooms[i].wave; return w; } }
        public int BossTheme => ThemeOfRoom(rooms.Count - 1);
        public int ThemeOfRoom(int i) => rooms[i].theme >= 0 ? rooms[i].theme : Battle.ThemeOf(rooms[i].wave);
        /// <summary>有指定主題的房間（波次 → 主題），給 Battle.waveTheme</summary>
        public Dictionary<int, int> ThemeMap()
        {
            Dictionary<int, int> m = null;
            foreach (var r in rooms) if (r.theme >= 0) { m ??= new Dictionary<int, int>(); m[r.wave] = r.theme; }
            return m;
        }
        /// <summary>這一波是第幾個房間（0 起算；不是這個副本的波＝-1）</summary>
        public int RoomOf(int wave) { for (int i = 0; i < rooms.Count; i++) if (rooms[i].wave == wave) return i; return -1; }
    }

    public static class Dungeons
    {
        /// <summary>
        /// 浮動難度（使用者要求）：最低以 2 人計算；3 人＝敵人數量、Boss 血量 ×3，4 人 ×4（1、2 人都是 ×2）。
        /// 人數越多掉寶率越高：2 人以下 ×1、3 人 ×1.25、4 人 ×1.5（稀有度照常擲，越稀有越難掉）。
        /// </summary>
        public static int PartyScale(int players) => Mathf.Max(2, players);
        public static float LootMul(int players) => 1f + 0.25f * Mathf.Clamp(players - 2, 0, 2);
        public const int DesignAllies = 2;
        /// <summary>副本的敵人（小怪與 Boss）血量、攻擊是一般關卡的 2 倍（使用者要求；再乘上人數倍率）</summary>
        public const float DungeonHp = 2f, DungeonDmg = 2f;

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

        /// <summary>全部副本（第一次用到時從 Resources/dungeons.txt 讀；格式見 DUNGEON_FORMAT.md）</summary>
        public static List<DungeonDef> All { get { Load(); return all; } }
        static readonly List<DungeonDef> all = new List<DungeonDef>();
        public static readonly List<string> errors = new List<string>();
        static bool loaded;

        /// <summary>讀副本劇本（遊戲裡從 Resources/dungeons 讀；測試時直接傳文字）</summary>
        public static void Load(string text = null)
        {
            if (loaded && text == null) return;
            if (text == null) { var ta = Resources.Load<TextAsset>("dungeons"); text = ta != null ? ta.text : ""; }
            Parse(text);
            loaded = true;
        }

        /// <summary>
        /// 解析副本劇本（純 C#，可在 Unity 外測試）：
        ///   [dungeon id] 名稱 → 簡介文字、level／players／unlock、&lt;before&gt;／&lt;after&gt; 對話；
        ///   [room] 房間名稱 → wave／theme、&lt;enter&gt;／&lt;clear&gt;／&lt;boss_half&gt; 對話。
        /// </summary>
        public static void Parse(string text)
        {
            all.Clear(); errors.Clear();
            DungeonDef d = null; DungeonRoom room = null; List<StoryLine> lines = null;
            var ids = new HashSet<string>();
            int ln = 0;
            foreach (var raw0 in (text ?? "").Replace("\r", "").Split('\n'))
            {
                ln++;
                string raw = raw0.Trim();
                if (raw.Length == 0 || raw.StartsWith("#") || raw.StartsWith("//")) continue;
                if (raw.StartsWith("["))
                {
                    int close = raw.IndexOf(']');
                    if (close < 0) { errors.Add($"第 {ln} 行：缺少 ]"); continue; }
                    var head = raw.Substring(1, close - 1).Trim().Split(new[] { ' ' }, 2);
                    string kind = head[0].ToLowerInvariant(), title = raw.Substring(close + 1).Trim();
                    lines = null;
                    if (kind == "dungeon")
                    {
                        string id = head.Length > 1 ? head[1].Trim() : "";
                        if (id.Length == 0) { errors.Add($"第 {ln} 行：[dungeon] 後面要寫 id"); d = null; continue; }
                        if (!ids.Add(id)) errors.Add($"第 {ln} 行：副本 id「{id}」重複了");
                        d = new DungeonDef { id = id, name = title.Length > 0 ? title : id, desc = "" };
                        all.Add(d); room = null;
                    }
                    else if (kind == "room")
                    {
                        if (d == null) { errors.Add($"第 {ln} 行：[room] 要放在 [dungeon] 底下"); continue; }
                        room = new DungeonRoom { title = title, wave = 0 };
                        d.rooms.Add(room);
                    }
                    else errors.Add($"第 {ln} 行：不認得的區塊 [{kind}]");
                    continue;
                }
                if (raw.StartsWith("<"))
                {
                    var tag = raw.Trim('<', '>', ' ').ToLowerInvariant();
                    lines = null;
                    if (room != null) lines = tag == "enter" ? room.enter : tag == "clear" ? room.clear : tag == "boss_half" ? room.bossHalf : null;
                    else if (d != null) lines = tag == "before" ? d.before : tag == "after" ? d.after : null;
                    if (lines == null) errors.Add($"第 {ln} 行：<{tag}> 放錯地方（副本用 before／after，房間用 enter／clear／boss_half）");
                    continue;
                }
                if (lines != null) { lines.Add(Story.ParseLine(raw)); continue; }
                int eq = raw.IndexOf('=');
                if (eq > 0)
                {
                    string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                    if (room != null)
                    {
                        if (k == "wave") { if (!int.TryParse(v, out room.wave) || room.wave < 1) errors.Add($"第 {ln} 行：wave 要是 1 以上的整數"); }
                        else if (k == "theme") room.theme = Story.ThemeOf(v);
                        else errors.Add($"第 {ln} 行：房間不認得的設定 {k}");
                    }
                    else if (d != null)
                    {
                        if (k == "level") { if (!int.TryParse(v, out d.level)) errors.Add($"第 {ln} 行：level 要是整數"); }
                        else if (k == "players")
                        {
                            var a = v.Split('-');
                            if (int.TryParse(a[0].Trim(), out var lo)) d.minPlayers = lo;
                            if (a.Length > 1 && int.TryParse(a[1].Trim(), out var hi)) d.maxPlayers = hi;
                        }
                        else if (k == "unlock") d.unlock = v.Length == 0 || v == "none" ? null : v;
                        else errors.Add($"第 {ln} 行：副本不認得的設定 {k}");
                    }
                    continue;
                }
                // 普通文字：副本簡介（房間底下＝房間進場的一句話）
                if (room != null) room.line = string.IsNullOrEmpty(room.line) ? raw : room.line + "\n" + raw;
                else if (d != null) d.desc = d.desc.Length > 0 ? d.desc + "\n" + raw : raw;
            }
            foreach (var dd in all)
            {
                if (dd.rooms.Count == 0) errors.Add($"副本 {dd.id}：沒有房間");
                if (dd.level < 1) errors.Add($"副本 {dd.id}：缺少 level");
                foreach (var r in dd.rooms)
                {
                    if (r.wave < 1) errors.Add($"副本 {dd.id} 房間「{r.title}」：缺少 wave");
                    // 進房間的一句話：沒寫 line 就用 enter 的第一句
                    if (string.IsNullOrEmpty(r.line) && r.enter.Count > 0) r.line = r.enter[0].text;
                }
                if (dd.rooms.Count > 0 && dd.rooms[dd.rooms.Count - 1].wave % 10 != 0) errors.Add($"副本 {dd.id}：最後一個房間要是首領（wave 是 10 的倍數）");
            }
        }

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

    }
}
