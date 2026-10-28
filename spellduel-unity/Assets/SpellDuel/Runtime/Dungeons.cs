using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>副本的一個房間：打第幾波（決定場景主題、小怪種類與強度；10 的倍數＝Boss 波；51 波起主題重新輪一次；房間可以用 theme 指定場景），進房間時的旁白</summary>
    public class DungeonRoom
    {
        /// <summary>特殊地點可以寫的設定</summary>
        public static readonly string[] EventKeysAll = { "name", "hp", "time", "count", "seq", "riddle", "answer", "hint", "look", "fail", "carpet" };
        /// <summary>地點 id（地圖用；[room] 沒寫就是 r1、r2…）</summary>
        public string id;
        /// <summary>事件：battle 戰鬥、chest 寶箱（不用打）、boss 首領（wave 是 10 的倍數就自動是首領）</summary>
        public string kind = "battle";
        /// <summary>打完可以前往的地點（房主選）；空的＝線性副本照檔案順序走下一個房間</summary>
        public readonly List<string> next = new List<string>();
        /// <summary>進到這個地點時出現的劇情人物（[npc] 的 id）</summary>
        public readonly List<string> npcs = new List<string>();
        public bool IsChest => kind == "chest";
        public bool IsBoss => kind == "boss" || (kind == "battle" && wave > 0 && wave % 10 == 0);
        public int wave;
        public int theme = -1;   // 指定場景主題（−1＝照波次算）：用高波次的強度、保留原本的場景
        /// <summary>特殊地點的設定（event = defend／survive／… 用：name、hp、time、count、seq、riddle、answer、hint、look、fail）</summary>
        public readonly Dictionary<string, string> param = new Dictionary<string, string>();
        public bool IsEvent => Battle.IsRoomEvent(kind);
        /// <summary>飛毯副本的站別（carpet = 0～4；−1＝不是飛毯）：Combat.Carpet.cs</summary>
        public int CarpetStation => param.TryGetValue("carpet", out var c) && int.TryParse(c, out var n) ? Mathf.Clamp(n, 0, 4) : -1;
        public string title, line;
        /// <summary>進房間、清完房間、首領剩一半血時的台詞（戰鬥中以字幕卡顯示，不暫停）</summary>
        public readonly List<StoryLine> enter = new List<StoryLine>(), clear = new List<StoryLine>(), bossHalf = new List<StoryLine>();
    }

    /// <summary>
    /// 副本裡的劇情人物：
    ///   escort 保護目標（怪物會攻擊他，他倒下＝副本失敗）；
    ///   choice 抉擇人物（房主選：加入我方 ally／變成敵人 enemy／第三勢力 third，誰都打）；
    ///   quest 任務人物（打倒幾隻敵人 kill、收集東西 collect、抵達某地點 reach；完成給獎勵）。
    /// </summary>
    public class DungeonNpc
    {
        public string id, name, role = "escort", look = "priest", tag;
        public int hp = 400;
        /// <summary>保護目標：抵達這個地點就安全了（null＝撐到通關）</summary>
        public string until;
        public bool optionalEscort;
        public readonly List<StoryLine> meet = new List<StoryLine>(), die = new List<StoryLine>(), done = new List<StoryLine>(), safe = new List<StoryLine>();
        public readonly List<StoryLine> ally = new List<StoryLine>(), enemy = new List<StoryLine>(), third = new List<StoryLine>();
        /// <summary>抉擇的選項：文字 → ally／enemy／third</summary>
        public readonly List<(string text, string result)> options = new List<(string, string)>();
        /// <summary>任務：kill N、collect N 物品名、reach 地點id</summary>
        public string goal, goalItem, goalNode; public int need;
        /// <summary>任務獎勵：金幣、每人一件這個稀有度的裝備（0＝沒有）</summary>
        public int rewardGold, rewardRarity;
        public List<StoryLine> LinesOf(string result) => result == "correct" ? done : result == "wrong" ? enemy : result == "neutral" ? third : result == "ally" ? ally : result == "enemy" ? enemy : result == "third" ? third : null;
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
        public int timeLimit; public bool traps;
        public int level;                          // 副本等級（掉落的物品等級、難度基準）
        public int minPlayers = 2, maxPlayers = 4; // 建議人數
        public string unlock;                      // 要先通關的故事關卡（例如 2-3；null＝一開始就開放）
        public bool Unlocked => string.IsNullOrEmpty(unlock) || Story.IsCleared(unlock);
        public readonly List<DungeonRoom> rooms = new List<DungeonRoom>();
        public readonly List<StoryLine> before = new List<StoryLine>();
        public readonly List<StoryLine> after = new List<StoryLine>();
        public readonly List<DungeonNpc> npcs = new List<DungeonNpc>();
        /// <summary>有固定地圖（有任何地點寫了 next）：打完一個地點由房主選下一站</summary>
        public bool IsMap { get { foreach (var r in rooms) if (r.next.Count > 0) return true; return false; } }
        public DungeonRoom Node(string id) => rooms.Find(r => r.id == id);
        public DungeonNpc Npc(string id) => npcs.Find(n => n.id == id);
        /// <summary>從這個地點可以去哪裡：地圖照 next；線性副本＝檔案裡的下一個房間；空的＝終點（通關）</summary>
        public List<DungeonRoom> ExitsOf(DungeonRoom r)
        {
            var list = new List<DungeonRoom>();
            if (r == null) return list;
            if (IsMap) { foreach (var id in r.next) { var n = Node(id); if (n != null) list.Add(n); } }
            else { int i = rooms.IndexOf(r); if (i >= 0 && i + 1 < rooms.Count) list.Add(rooms[i + 1]); }
            return list;
        }
        /// <summary>從起點到首領最少要打幾個戰鬥地點（副本清單顯示用）</summary>
        public int ShortestFights()
        {
            int best = int.MaxValue;
            void Walk(DungeonRoom r, int n, int depth)
            {
                if (depth > 40) return;
                if (!r.IsChest) n++;
                var ex = ExitsOf(r);
                if (ex.Count == 0) { best = System.Math.Min(best, n); return; }
                foreach (var e in ex) Walk(e, n, depth + 1);
            }
            if (rooms.Count > 0) Walk(rooms[0], 0, 0);
            return best == int.MaxValue ? rooms.Count : best;
        }
        /// <summary>清單上的規模說明：線性「4 房」；地圖「地圖 6 地點（最少 4 戰）」</summary>
        public string SizeText => IsMap ? $"地圖 {rooms.Count} 地點（最少 {ShortestFights()} 戰）" : $"{rooms.Count} 房";
        public int[] Waves { get { var w = new int[rooms.Count]; for (int i = 0; i < w.Length; i++) w[i] = rooms[i].wave; return w; } }
        public int BossTheme => ThemeOfRoom(rooms.Count - 1);
        public int ThemeOfRoom(int i) => ThemeOf(rooms[i]);
        public int ThemeOf(DungeonRoom r) => r.theme >= 0 ? r.theme : Battle.ThemeOf(Mathf.Max(1, r.wave));
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
        ///   [room] 名稱 或 [node id] 名稱 → event／wave／theme／next／npc、&lt;enter&gt;／&lt;clear&gt;／&lt;boss_half&gt; 對話；
        ///   [npc id] 名字 → role／hp／look／until／option／goal／reward、&lt;meet&gt;／&lt;die&gt;／&lt;done&gt;／&lt;safe&gt;／&lt;ally&gt;／&lt;enemy&gt;／&lt;third&gt; 對話。
        /// </summary>
        public static void Parse(string text)
        {
            all.Clear(); errors.Clear();
            DungeonDef d = null; DungeonRoom room = null; DungeonNpc npc = null; List<StoryLine> lines = null;
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
                    string hid = head.Length > 1 ? head[1].Trim() : "";
                    lines = null;
                    if (kind == "dungeon")
                    {
                        if (hid.Length == 0) { errors.Add($"第 {ln} 行：[dungeon] 後面要寫 id"); d = null; continue; }
                        if (!ids.Add(hid)) errors.Add($"第 {ln} 行：副本 id「{hid}」重複了");
                        d = new DungeonDef { id = hid, name = title.Length > 0 ? title : hid, desc = "" };
                        all.Add(d); room = null; npc = null;
                    }
                    else if (kind == "room" || kind == "node")
                    {
                        if (d == null) { errors.Add($"第 {ln} 行：[{kind}] 要放在 [dungeon] 底下"); continue; }
                        if (kind == "node" && hid.Length == 0) errors.Add($"第 {ln} 行：[node] 後面要寫地點 id");
                        room = new DungeonRoom { id = hid.Length > 0 ? hid : "r" + (d.rooms.Count + 1), title = title, wave = 0 };
                        if (d.Node(room.id) != null) errors.Add($"第 {ln} 行：地點 id「{room.id}」重複了");
                        d.rooms.Add(room); npc = null;
                    }
                    else if (kind == "npc")
                    {
                        if (d == null) { errors.Add($"第 {ln} 行：[npc] 要放在 [dungeon] 底下"); continue; }
                        if (hid.Length == 0) { errors.Add($"第 {ln} 行：[npc] 後面要寫 id"); continue; }
                        if (d.Npc(hid) != null) errors.Add($"第 {ln} 行：人物 id「{hid}」重複了");
                        npc = new DungeonNpc { id = hid, name = title.Length > 0 ? title : hid };
                        d.npcs.Add(npc); room = null;
                    }
                    else errors.Add($"第 {ln} 行：不認得的區塊 [{kind}]");
                    continue;
                }
                if (raw.StartsWith("<"))
                {
                    var tag = raw.Trim('<', '>', ' ').ToLowerInvariant();
                    lines = null;
                    if (npc != null) lines = tag == "meet" ? npc.meet : tag == "die" ? npc.die : tag == "done" ? npc.done : tag == "safe" ? npc.safe : npc.LinesOf(tag);
                    else if (room != null) lines = tag == "enter" ? room.enter : tag == "clear" ? room.clear : tag == "boss_half" ? room.bossHalf : null;
                    else if (d != null) lines = tag == "before" ? d.before : tag == "after" ? d.after : null;
                    if (lines == null) errors.Add($"第 {ln} 行：<{tag}> 放錯地方（副本用 before／after，地點用 enter／clear／boss_half，人物用 meet／die／done／safe／ally／enemy／third）");
                    continue;
                }
                if (lines != null && !IsKeyLine(raw)) { lines.Add(Story.ParseLine(raw)); continue; }
                lines = null;   // 「英文 key = 值」：對話區塊結束（例如寫在 <meet> 後面的 option）
                int eq = raw.IndexOf('=');
                if (eq > 0)
                {
                    string k = raw.Substring(0, eq).Trim().ToLowerInvariant(), v = raw.Substring(eq + 1).Trim();
                    if (npc != null) ParseNpcKey(npc, k, v, ln);
                    else if (room != null)
                    {
                        if (k == "wave") { if (!int.TryParse(v, out room.wave) || room.wave < 1) errors.Add($"第 {ln} 行：wave 要是 1 以上的整數"); }
                        else if (k == "theme") room.theme = v == "vault" ? 3 : Story.ThemeOf(v);
                        else if (k == "event") { room.kind = v.ToLowerInvariant(); if (room.kind != "battle" && room.kind != "chest" && room.kind != "boss" && !Battle.IsRoomEvent(room.kind)) errors.Add($"第 {ln} 行：event 只能是 battle／chest／boss／" + string.Join("／", Battle.RoomEventKinds)); }
                        else if (Array.IndexOf(DungeonRoom.EventKeysAll, k) >= 0) room.param[k] = v;
                        else if (k == "next") { foreach (var x in v.Split(',', '，', '、')) if (x.Trim().Length > 0) room.next.Add(x.Trim()); }
                        else if (k == "npc") { foreach (var x in v.Split(',', '，', '、')) if (x.Trim().Length > 0) room.npcs.Add(x.Trim()); }
                        else errors.Add($"第 {ln} 行：地點不認得的設定 {k}");
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
                        else if (k == "time_limit") { if (!int.TryParse(v, out d.timeLimit) || d.timeLimit < 1) errors.Add($"第 {ln} 行：time_limit 為分鐘正整數"); }
                        else if (k == "traps") d.traps = v == "on";
                        else if (k == "unlock") d.unlock = v.Length == 0 || v == "none" ? null : v;
                        else errors.Add($"第 {ln} 行：副本不認得的設定 {k}");
                    }
                    continue;
                }
                // 普通文字：副本簡介（房間底下＝房間進場的一句話）
                if (room != null) room.line = string.IsNullOrEmpty(room.line) ? raw : room.line + "\n" + raw;
                else if (d != null && npc == null) d.desc = d.desc.Length > 0 ? d.desc + "\n" + raw : raw;
            }
            foreach (var dd in all) Validate(dd);
        }

        /// <summary>「英文小寫 key = 值」的設定行（台詞不會這樣開頭）</summary>
        static bool IsKeyLine(string raw)
        {
            int eq = raw.IndexOf('=');
            if (eq <= 0) return false;
            var k = raw.Substring(0, eq).Trim();
            if (k.Length == 0) return false;
            foreach (var ch in k) if (!(ch >= 'a' && ch <= 'z' || ch == '_')) return false;
            return true;
        }

        static void ParseNpcKey(DungeonNpc n, string k, string v, int ln)
        {
            var parts = v.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (k == "role") { n.role = v.ToLowerInvariant(); if (n.role != "escort" && n.role != "choice" && n.role != "quest" && n.role != "lever" && n.role != "shop") errors.Add($"第 {ln} 行：role 只能是 escort／choice／quest"); }
            else if (k == "hp") { if (!int.TryParse(v, out n.hp) || n.hp < 1) errors.Add($"第 {ln} 行：hp 要是 1 以上的整數"); }
            else if (k == "look") n.look = v;
            else if (k == "tag") n.tag = v;
            else if (k == "until") n.until = v;
            else if (k == "optional") n.optionalEscort = v == "on";
            else if (k == "option")
            {
                int arrow = v.IndexOf("->", System.StringComparison.Ordinal);
                string res = arrow > 0 ? v.Substring(arrow + 2).Trim().ToLowerInvariant() : "";
                if (arrow <= 0 || (res != "ally" && res != "enemy" && res != "third" && !(n.role == "lever" && (res == "correct" || res == "wrong" || res == "neutral")))) errors.Add($"第 {ln} 行：option 的格式是「選項文字 -> ally／enemy／third」");
                else n.options.Add((v.Substring(0, arrow).Trim(), res));
            }
            else if (k == "goal")
            {
                n.goal = parts.Length > 0 ? parts[0].ToLowerInvariant() : "";
                if (n.goal == "kill" && parts.Length >= 2 && int.TryParse(parts[1], out n.need)) { }
                else if (n.goal == "collect" && parts.Length >= 3 && int.TryParse(parts[1], out n.need)) n.goalItem = string.Join(" ", parts, 2, parts.Length - 2);
                else if (n.goal == "reach" && parts.Length >= 2) { n.goalNode = parts[1]; n.need = 1; }
                else errors.Add($"第 {ln} 行：goal 的格式是「kill 數量」「collect 數量 物品名」或「reach 地點id」");
            }
            else if (k == "reward")
            {
                if (parts.Length >= 2 && parts[0] == "gold" && int.TryParse(parts[1], out var g)) n.rewardGold = g;
                else if (parts.Length >= 2 && parts[0] == "item" && int.TryParse(parts[1], out var r) && r >= 1 && r <= 4) n.rewardRarity = r;
                else errors.Add($"第 {ln} 行：reward 的格式是「gold 金額」或「item 稀有度(1～4)」");
            }
            else errors.Add($"第 {ln} 行：人物不認得的設定 {k}");
        }

        /// <summary>檢查一個副本：房間、地圖連線（每條路都通到首領、沒有繞圈）、劇情人物</summary>
        static void Validate(DungeonDef dd)
        {
            if (dd.rooms.Count == 0) { errors.Add($"副本 {dd.id}：沒有房間"); return; }
            if (dd.level < 1) errors.Add($"副本 {dd.id}：缺少 level");
            foreach (var r in dd.rooms)
            {
                if (!r.IsChest && r.wave < 1) errors.Add($"副本 {dd.id} 地點「{r.title}」：缺少 wave");
                if (r.kind == "boss" && r.wave % 10 != 0) errors.Add($"副本 {dd.id} 地點「{r.title}」：首領的 wave 要是 10 的倍數");
                // 進房間的一句話：沒寫 line 就用 enter 的第一句
                if (string.IsNullOrEmpty(r.line) && r.enter.Count > 0) r.line = r.enter[0].text;
                foreach (var x in r.next) if (dd.Node(x) == null) errors.Add($"副本 {dd.id} 地點「{r.title}」：next 指到不存在的地點 {x}");
                foreach (var x in r.npcs) if (dd.Npc(x) == null) errors.Add($"副本 {dd.id} 地點「{r.title}」：npc 指到不存在的人物 {x}");
            }
            if (!dd.IsMap)
            {
                if (dd.rooms[dd.rooms.Count - 1].wave % 10 != 0) errors.Add($"副本 {dd.id}：最後一個房間要是首領（wave 是 10 的倍數）");
            }
            else
            {
                // 地圖：從第一個地點出發，每個終點都要是首領；不能繞圈；每個地點都走得到
                var seen = new HashSet<DungeonRoom>(); var stack = new HashSet<DungeonRoom>();
                void Walk(DungeonRoom r)
                {
                    if (stack.Contains(r)) { errors.Add($"副本 {dd.id}：地圖繞圈了（{r.id}）"); return; }
                    if (!seen.Add(r)) return;
                    stack.Add(r);
                    var ex = dd.ExitsOf(r);
                    if (ex.Count == 0 && !r.IsBoss) errors.Add($"副本 {dd.id}：地點 {r.id} 沒有 next，但不是首領（終點一定要是首領）");
                    foreach (var e in ex) Walk(e);
                    stack.Remove(r);
                }
                Walk(dd.rooms[0]);
                foreach (var r in dd.rooms) if (!seen.Contains(r)) errors.Add($"副本 {dd.id}：地點 {r.id} 從起點走不到");
                if (dd.rooms[0].IsChest) errors.Add($"副本 {dd.id}：第一個地點不能是寶箱");
            }
            foreach (var n in dd.npcs)
            {
                if ((n.role == "choice" || n.role == "lever") && n.options.Count < 2) errors.Add($"副本 {dd.id} 人物 {n.id}：抉擇人物至少要 2 個 option");
                if (n.role == "quest" && string.IsNullOrEmpty(n.goal)) errors.Add($"副本 {dd.id} 人物 {n.id}：任務人物要寫 goal");
                if (n.goalNode != null && dd.Node(n.goalNode) == null) errors.Add($"副本 {dd.id} 人物 {n.id}：goal reach 指到不存在的地點 {n.goalNode}");
                if (n.until != null && dd.Node(n.until) == null) errors.Add($"副本 {dd.id} 人物 {n.id}：until 指到不存在的地點 {n.until}");
                if (n.meet.Count == 0) errors.Add($"副本 {dd.id} 人物 {n.id}：要有 <meet>（登場台詞）");
                bool used = false; foreach (var r in dd.rooms) if (r.npcs.Contains(n.id)) used = true;
                if (!used) errors.Add($"副本 {dd.id} 人物 {n.id}：沒有任何地點寫 npc = {n.id}");
            }
        }

        static HashSet<string> cleared;
        public static void ResetCache() => cleared = null;   // 換角色：通關紀錄重新讀
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
