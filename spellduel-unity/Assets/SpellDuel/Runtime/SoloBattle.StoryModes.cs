using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的玩法（使用者要求，從第二章開始）：
    ///   ・死鬥（battle）：原本的 10m×10m 場地清怪。
    ///   ・探索（explore）：像副本那樣的大地圖，但比副本短（3 個地點＋一個寶箱），場景互動（符文、圖騰）和陷阱多一點。
    ///   ・護送（escort）：小地圖，一位 NPC 跟著玩家走；敵人主要攻擊 NPC，NPC 倒下＝失敗。
    ///   ・生存（survive）：一塊區域，敵人越出越多，撐過時間就贏（Combat.WaveGoals.cs 的 stageMode）。
    /// 探索與護送：用程式產生一份小副本（DungeonDef.story 指回這一關），走副本大地圖的流程（DungeonMaze、SoloBattle.DungeonExplore.cs），
    /// 但強度照故事關卡（不 ×2、不以 2 人計），通關、獎勵、結尾對話都照故事關卡。目前只有單人；故事合作改打死鬥。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>生存要撐幾秒：越後面的關卡越久</summary>
        public static int SurviveSecondsOf(StoryStage st) => st == null ? 90 : 75 + 5 * Mathf.Clamp(st.fromWave - 1, 0, 9);

        bool StoryMazeMode(StoryStage st) => st != null && (st.mode == "explore" || st.mode == "escort" || st.mode == "race" || st.mode == "stealth") && !StoryCoop && !NetMode;

        /// <summary>這一關的小地圖：地點名稱、事件照關卡挑；沒寫的關卡用通用的名稱</summary>
        static DungeonDef StoryMazeDef(StoryStage st)
        {
            var d = new DungeonDef { id = "story_" + st.id, name = st.title, level = Story.LevelBand(st.theme).lo, story = st, storyMode = st.mode, minPlayers = 1, maxPlayers = 1 };
            int w0 = st.FirstWave, w1 = Mathf.Max(st.FirstWave, st.LastWave), wm = (w0 + w1) / 2;
            bool escort = st.mode == "escort";
            if (st.mode == "race")
            {
                // 賽跑：只有起點、終點兩塊（RaceTrack 自己做跑道）
                d.rooms.Add(new DungeonRoom { id = "start", title = "起跑線", kind = "battle", wave = w0, theme = st.theme });
                d.rooms.Add(new DungeonRoom { id = "goal", title = "終點", kind = "battle", wave = w1, theme = st.theme });
                return d;
            }
            (string a, string b, string chest, string fin, string ev) names = st.id switch
            {
                "2-1" => ("毒花叢", "葉燈小徑", "採藥人的箱子", "聚落外的林道", "runes"),
                "2-3" => ("祭道入口", "倒下的石燈籠", "散落的行囊", "樹心祭道", null),
                "2-5" => ("黑晶根網", "黑晶圖騰", "翠留下的補給", "祭壇外圍", "totems"),
                _ => ("林間空地", "古老的遺跡", "藏起來的箱子", "最深處", escort ? null : "runes"),
            };
            if (st.mode == "stealth") names = ("外圍哨站", "巡邏的走廊", "沒人看守的貨箱", "目的地", null);
            void Room(string id, string title, string kind, int wave, string line, params (string k, string v)[] ps)
            {
                var r = new DungeonRoom { id = id, title = title, kind = kind, wave = wave, theme = st.theme };
                foreach (var p in ps) r.param[p.k] = p.v;
                if (!string.IsNullOrEmpty(line)) r.enter.Add(new StoryLine { text = line });
                d.rooms.Add(r);
            }
            bool sneak = st.mode == "stealth";
            Room("s1", names.a, "battle", w0, sneak ? "" : escort ? "敵人盯上了同伴！擋在他前面！" : "這裡的魔物守著路，清開牠們！");
            if (names.ev == "runes") Room("s2", names.b, "runes", wm, "地上的葉燈照順序亮了一遍——照同樣的順序踩上去！", ("count", "4"), ("seq", "3"));
            else if (names.ev == "totems") Room("s2", names.b, "totems", wm, "黑晶圖騰會一直叫出魔物，先把圖騰打碎！", ("count", "3"));
            else Room("s2", names.b, "battle", wm, sneak ? "" : escort ? "伏兵！別讓牠們碰到同伴！" : "又有一群魔物擋路！");
            Room("s3", names.chest, "chest", wm, "找到補給了！");
            Room("s4", names.fin, "battle", w1, sneak ? "" : escort ? "最後一段路——把同伴平安送到！" : "最後的守衛！打倒牠們就能通過！");
            return d;
        }

        /// <summary>StartStageBattle 叫：探索、護送玩法開打</summary>
        void StartStoryMaze()
        {
            var d = StoryMazeDef(storyStage);
            pendingDungeon = d;
            ClearRace(true);
            if (d.storyMode == "race") pendingExplore = new ExploreRun(BuildRaceMap(d, storyStage));   // 賽跑：跑道（SoloBattle.Race.cs）
            else
            {
                var map = DungeonMaze.Build(d);
                if (d.storyMode == "stealth") ThinForStealth(map);   // 潛入：怪少一點
                pendingExplore = new ExploreRun(map);
            }
            ExploreFx.Prefetch();
            StartBossBattle(false, true);
        }

        // ---------------------------------------------------------------- 護送：同伴跟著玩家
        Fighter storyEscort; float storyEscortSaidAt;
        readonly List<Vector3> storyTrail = new List<Vector3>();
        const float EscortLure = 4f, EscortCatchUp = 7f;

        /// <summary>StartBossBattle（故事的小地圖開好之後）叫</summary>
        void StoryMazeBegun()
        {
            storyEscort = null; storyTrail.Clear();
            var st = dungeon.story;
            if (dungeon.storyMode == "race") { BeginRace(); return; }
            if (dungeon.storyMode == "stealth") { BeginStealth(); return; }
            Say($"{StoryStage.ModeName(dungeon.storyMode)}：{st.title}\n" + (dungeon.storyMode == "escort"
                ? "同伴會跟著你走；敵人會優先撲向他，他倒下就失敗。走到最後的地點並清除就過關"
                : "在地圖上探索：清除每個地點才會打開下一扇門；小心地上的陷阱"), 5f);
            if (dungeon.storyMode == "escort")
            {
                // 同伴：艾琳（血量約是玩家的 2.5 倍；敵人多算 3＋EscortLure 公尺的優勢 → 幾乎都先打她）
                int hp = Mathf.RoundToInt(Mathf.Max(200, Me.maxHp * 2.5f));
                storyEscort = battle.SpawnNpc("story_escort", "艾琳", "mage", 0, true, hp);
                storyEscort.lure = EscortLure;
            }
        }

        /// <summary>UpdateExplore 每幀叫（戰鬥中）：護送的同伴跟丟了就拉回玩家身後；倒下＝失敗</summary>
        void UpdateStoryMaze(ExploreRun run)
        {
            if (dungeon == null || dungeon.story == null || battle == null || Me == null) return;
            if (dungeon.storyMode == "race") { UpdateRace(Time.deltaTime); return; }
            if (storyTrail.Count == 0 || Battle.FlatDistance(storyTrail[storyTrail.Count - 1], Me.Feet) > 0.5f)
            {
                storyTrail.Add(Me.Feet);
                if (storyTrail.Count > 40) storyTrail.RemoveAt(0);
            }
            if (storyEscort == null) return;
            if (!storyEscort.Alive) { battle.FailStage($"{storyEscort.name}倒下了…"); return; }
            float d = Battle.FlatDistance(storyEscort.Feet, Me.Feet);
            if (d > EscortCatchUp && storyTrail.Count >= 3)
            {
                // 跟丟了（玩家衝太快、被牆卡住）：放到玩家走過的路上、身後幾步的地方
                var p = storyTrail[Mathf.Max(0, storyTrail.Count - 4)];
                if (run.map.Walkable(p, 0.45f)) storyEscort.head = new Vector3(p.x, storyEscort.head.y, p.z);
            }
            if (storyEscort.hp < storyEscort.maxHp * 0.35f && Time.time > storyEscortSaidAt + 8f)
            {
                storyEscortSaidAt = Time.time;
                Say($"⚠ {storyEscort.name}快撐不住了！（{Mathf.CeilToInt(storyEscort.hp)}/{storyEscort.maxHp}）", 2f);
                Haptics.Play(Haptics.Kind.Warning);
            }
        }

        /// <summary>故事關卡失敗時多一句原因</summary>
        string StoryFailNote() => storyEscort != null && !storyEscort.Alive ? $"{storyEscort.name}倒下了。" : "";
    }
}
