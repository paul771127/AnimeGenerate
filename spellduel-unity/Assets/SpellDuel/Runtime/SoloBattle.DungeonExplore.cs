using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本大地圖（使用者要求：副本全部改成像翠蔭迷林那樣，在一張大地圖上探索、打怪，強度照原本的副本等級）。
    /// 地圖由 DungeonMaze（ExploreDungeon.cs）照副本劇本產生；這裡把地圖上的區域接到原本副本的戰鬥流程：
    ///   ・戰鬥地點：區域裡守著的怪（探索的怪），全部打倒＝清除。強度照那個地點的波次（ApplyDungeonStrength：同等級故事關卡 ×2）。
    ///   ・特殊事件、中途首領、最後的首領：走進區域 → 用原本副本的地點流程（EnterNode＋StartRoomEvent），場地＝這塊區域；打完＝清除。
    ///   ・寶箱地點：走進去就打開（一件稀有以上＋金幣）。
    ///   ・浮動難度（最少以 2 人算）、等級差修正、通關獎勵都照原本的副本。
    /// </summary>
    public partial class SoloBattle
    {
        ExArea exNodeArea; float exNodeAt;
        List<Vector3> exSavedPoly;
        readonly Queue<ExArea> exNodeQueue = new Queue<ExArea>();

        /// <summary>這個副本要不要用大地圖：飛毯副本不改；千機迴廊之後改成機關房間。單人、連線都用（地圖每台手機各自產生，長得一樣）</summary>
        bool DungeonExploreOk(DungeonDef d, bool coopMode) => DungeonMaze.Supports(d);

        /// <summary>千機迴廊大地圖：開一個原本的副本進度（DungeonRun），但不馬上開打第一站——走進房間才開打（StartAreaNode）</summary>
        void BeginDungeonExploreRun()
        {
            drun = new DungeonRun(dungeon); exFirstNodeApplied = false;
            BeginGallery();
            chest = null; chatLog.Clear(); ClearNpcRigs();
            if (DungeonDecider) battle.OnNpcDown += OnNpcDown;
            Say($"⚙ {dungeon.name}：每個房間都有機關，過完一個房間才會打開往下一個房間的門", 4f);
        }
        float exAreaSendAt; string exAreaSent; bool exFirstNodeApplied;

        /// <summary>StartBossBattle（副本那一段）叫：把副本大地圖接上戰鬥</summary>
        void SetupDungeonExplore()
        {
            var run = exploreRun;
            exNodeArea = null; exNodeQueue.Clear(); exSavedPoly = null;
            exFxAreaEntered.Clear(); exFxAreaCleared.Clear(); exFxBossRose = false; dgFxKeys.Clear();
            battle.StartExplore(run);
            battle.exMirror = coop && !NetIsHost;   // 連線的隊友：怪物、事件、首領都照主機送來的顯示
            battle.OnExWave = ApplyDungeonMinionStrength;
            battle.themeOverride = dungeon.ThemeOf(run.map.areas[0].node);
            ExHookBattle();
            Ticker(ExLines($"📜 {run.Objective}"));
        }

        /// <summary>地圖上的怪放上戰場時：小怪倍率照那個地點的波次（不動首領的設定）</summary>
        void ApplyDungeonMinionStrength(int w)
        {
            float bh = battle.storyBossHp, bd = battle.storyBossDmg, bm = battle.bossHpMul; int bo = battle.bossBoostOverride;
            ApplyDungeonStrength(w);
            battle.storyBossHp = bh; battle.storyBossDmg = bd; battle.bossHpMul = bm; battle.bossBoostOverride = bo;
        }

        /// <summary>每幀（UpdateExplore 叫）：場景主題、走進區域、地點打完、劇情台詞</summary>
        void UpdateDungeonExplore(ExploreRun run)
        {
            if (dungeon == null || battle == null) return;
            // 場景主題：跟著最近的區域（正在打的地點優先）
            if (exNodeArea == null)
            {
                ExArea near = null; float best = float.MaxValue;
                foreach (var a in run.map.areas) { float d = Battle.FlatDistance(a.center, Me.Feet); if (d < best) { best = d; near = a; } }
                if (near != null) { int th = dungeon.ThemeOf(near.node); if (battle.themeOverride != th) battle.themeOverride = th; }
            }
            if (!DungeonDecider)
            {
                // 連線的隊友端：區域的進度照主機（dxa）；地點的戰鬥由主機開
                if (battle.explore == null) { battle.exMirror = true; battle.StartExplore(run); ExHookBattle(); }   // 牆擋法術、怪物照主機（自己不放怪）
                run.areaStarts.Clear();
                foreach (var a in run.areaCleared) Ticker(a.node.clear);
                run.areaCleared.Clear();
                UpdateRoomEventVisuals();
                return;
            }
            // 主機：隊友走進區域也算（照他們的位置更新）
            if (coop) foreach (var al in battle.allies) if (al != null && al.Alive) run.Update(al.Feet, null);
            foreach (var a in run.areaStarts) exNodeQueue.Enqueue(a);
            run.areaStarts.Clear();
            while (exNodeArea == null && bossIntroArea == null && exNodeQueue.Count > 0) StartAreaNode(run, exNodeQueue.Dequeue());
            // 地點的戰鬥（事件、首領）打完
            bool nodeDone = exNodeArea != null && (run.map.nodeAreas && exNodeArea.IsChest ? chest != null && chest.done && Time.time >= chest.doneAt + 2.5f && GalleryRouteReady
                : battle.awaitingNext && (!run.map.nodeAreas || GalleryRouteReady && (drun == null || drun.pendingChoice == null)));
            if (nodeDone && !battle.Over && Time.time >= exNodeAt + 1f)
            {
                var a = exNodeArea; exNodeArea = null;
                battle.CleanupRoomEvent(); battle.arenaPoly = exSavedPoly; battle.bossPoint = null;
                run.ClearArea(a);
            }
            foreach (var a in run.areaCleared) if (!run.map.nodeAreas) Ticker(a.node.clear);   // 千機迴廊：台詞由原本的副本進度送
            run.areaCleared.Clear();
            // 首領剩一半血
            if (exNodeArea != null && exNodeArea.IsBoss && dungeonHalfSaid != exNodeArea.index && battle.IsBossWave && En.Alive && En.maxHp > 0 && En.hp <= En.maxHp * 0.5f)
            { dungeonHalfSaid = exNodeArea.index; Ticker(exNodeArea.node.bossHalf); }
            UpdateRoomEventVisuals();
            if (coop) { SendRoomEventState(); SendAreaState(run); }
        }

        /// <summary>主機：區域的進度（走進過、清除）送給隊友（有變化馬上送，沒變化每 2 秒再送一次）</summary>
        void SendAreaState(ExploreRun run)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in run.map.areas) sb.Append(a.cleared ? '2' : a.entered ? '1' : '0');
            string st = sb.ToString();
            if (st == exAreaSent && Time.time < exAreaSendAt) return;
            exAreaSent = st; exAreaSendAt = Time.time + 2f;
            NetSend?.Invoke(new Msg { t = "dxa", k = st });
        }

        /// <summary>隊友：收到主機的區域進度（清除的區域打開門、顯示台詞）</summary>
        bool DungeonExploreMsg(Msg m)
        {
            if (m.t != "dxa") return false;
            if (NetIsHost || exploreRun == null || !exploreRun.IsDungeon || string.IsNullOrEmpty(m.k)) return true;
            var areas = exploreRun.map.areas;
            // 千機迴廊：第一個房間（隊友的進度一開始就在這裡，不會收到 dnode）——主機走進去時，隊友也套用一次
            if (exploreRun.map.nodeAreas && drun != null && !exFirstNodeApplied && m.k.Length > 0 && m.k[0] >= '1' && drun.node == dungeon.rooms[0]) { exFirstNodeApplied = true; ApplyNodeNow(); }
            for (int i = 0; i < areas.Count && i < m.k.Length; i++)
            {
                var a = areas[i];
                if (m.k[i] >= '1') a.entered = true;
                if (m.k[i] == '2' && !a.cleared) { if (a.IsChest && battle != null) ChestAreaLoot(a); exploreRun.ClearArea(a); }
            }
            return true;
        }

        /// <summary>走進一個區域：寶箱直接打開；事件、首領照原本副本的地點流程開打（場地＝這塊區域）</summary>
        /// <summary>
        /// 最後的首領房（使用者要求）：走進去先播一段劇情（這個地點的 &lt;enter&gt; 台詞），戰鬥暫停；看完敵人才撲過來。
        /// 單人才停（連線不能停，照舊用字幕卡）。
        /// </summary>
        ExArea bossIntroArea;
        void StartAreaNode(ExploreRun run, ExArea a)
        {
            bool fin = run.map.arenas.Exists(x => x.n == 4 && Vector3.Distance(x.center, a.center) < 0.1f);
            if (fin && !run.map.nodeAreas && !coop && !NetMode && a.node.enter.Count > 0 && bossIntroArea == null)
            {
                bossIntroArea = a;
                Say($"👑 {a.Title}", 3f);
                ShowDialogue(a.node.enter, () => { var ar = bossIntroArea; bossIntroArea = null; if (exploreRun == run && battle != null && !battle.Over) BeginAreaNode(run, ar); });
                return;
            }
            if (!run.map.nodeAreas) Ticker(a.node.enter);   // 千機迴廊：台詞由原本的副本進度送（DungeonRun）
            BeginAreaNode(run, a);
        }

        void BeginAreaNode(ExploreRun run, ExArea a)
        {
            if (a.IsChest && !run.map.nodeAreas) { ChestAreaLoot(a); run.ClearArea(a); return; }
            bool final = run.map.arenas.Exists(x => x.n == 4 && Vector3.Distance(x.center, a.center) < 0.1f);
            exSavedPoly = battle.arenaPoly;
            battle.arenaPoly = AreaPoly(run.map, a);
            if (run.map.nodeAreas && drun != null)
            {
                // 千機迴廊：原本的地點流程（機關房、抉擇、商店、寶箱擲骰），場地＝這個房間
                if (drun.node != a.node) { drun.Enter(a.node); if (coop) NetSend?.Invoke(new Msg { t = "dnode", k = a.node.id }); }   // 連線：隊友跟著換到這個房間（機關、畫面）
                var c0 = a.center; battle.bossPoint = () => c0;
                exNodeArea = a; exNodeAt = Time.time;
                ApplyNodeNow();
                return;
            }
            battle.themeOverride = dungeon.ThemeOf(a.node);
            var center = a.center;
            battle.bossPoint = () => center;
            battle.EnterNode(Mathf.Max(1, a.wave), final, 1.5f);
            if (a.IsEvent) battle.StartRoomEvent(a.node.kind, a.node.param);
            exNodeArea = a; exNodeAt = Time.time;
            Say(final ? $"👑 {a.Title}" : a.IsBoss ? $"💀 {a.Title}" : $"⚠ {a.Title}", 3f);
        }

        /// <summary>寶箱地點：一件稀有以上的裝備＋金幣（每個人各自拿；連線的隊友在收到主機的清除時拿）</summary>
        void ChestAreaLoot(ExArea a)
        {
            ItemGen.SetTheme = dungeon.ThemeOf(a.node);
            pendingItems.Add(ItemGen.Make(dropRng, dungeon.level + battle.levelBoost, Mathf.Max(2, ItemGen.RollRarity(dropRng, 0f, 2)), myClass));
            pendingGold += 40 * dungeon.level;
            floaters.Add(new Floater { text = $"🧰 {a.Title}：裝備＋金幣（通關後獲得）", color = new Color(1f, 0.85f, 0.35f), born = Time.time, screen = true });
            Sfx.Play("coin", 0.9f); Haptics.Play(Haptics.Kind.Success);
        }

        /// <summary>區域的場地（四個角，往內縮一點；事件的道具、出怪位置照這個）</summary>
        static List<Vector3> AreaPoly(ExploreMap m, ExArea a)
        {
            float half = ExploreMap.CellSize * 0.5f, inset = 0.6f;
            var lo = m.World(a.rect.xMin, a.rect.yMin); var hi = m.World(a.rect.xMax, a.rect.yMax);
            float x0 = lo.x - half + inset, z0 = lo.z - half + inset, x1 = hi.x + half - inset, z1 = hi.z + half - inset;
            return new List<Vector3> { new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1) };
        }

        // ---------------------------------------------------------------- 特效影片（影片生成機做的 mp4；Docs/dungeon_fx）
        readonly HashSet<ExArea> exFxAreaEntered = new HashSet<ExArea>(), exFxAreaCleared = new HashSet<ExArea>();
        bool exFxBossRose;
        /// <summary>循環特效的 key 要是同一個物件（ExploreFx.Loop 用參照比對）：每個區域的每個位置一個固定的 key</summary>
        readonly Dictionary<(ExArea, int), object> dgFxKeys = new Dictionary<(ExArea, int), object>();
        static readonly object DgThroneKey = new object();
        object DgKey(ExArea a, int slot) { if (!dgFxKeys.TryGetValue((a, slot), out var k)) dgFxKeys[(a, slot)] = k = new object(); return k; }
        const float DgFxGateRange = 16f, DgFxAmbientRange = 12f, DgFxThroneRange = 22f;

        /// <summary>區域的特效主題：千機迴廊＝clockwork，其他照那個地點的場景主題</summary>
        string DgFxTheme(ExploreRun run, ExArea a)
        {
            if (run.map.nodeAreas) return "clockwork";
            int th = dungeon != null ? dungeon.ThemeOf(a.node) : 1;
            return th == 0 ? "volcano" : th == 1 ? "forest" : th == 2 ? "sea" : th == 3 ? "arena" : "canyon";
        }
        static string DgAmbient(string theme) => theme == "volcano" ? "fx_DG_lava_vent" : theme == "forest" ? "fx_DG_forest_fireflies" : theme == "sea" ? "fx_DG_sea_spray" : theme == "arena" ? "fx_DG_arena_sand" : theme == "canyon" ? "fx_DG_canyon_dust" : "fx_DG_clockwork_steam";

        /// <summary>
        /// 每幀（UpdateExplore 叫）：
        ///   出口門：還沒清除＝封印循環（只播附近的）；清除時每個門播一次碎掉。
        ///   走進區域：戰鬥＝警告圈、事件＝魔法陣、中途首領＝光柱；清除：金光（寶箱地點＝寶物光、千機迴廊的房間＝齒輪光）。
        ///   王座：還沒走進去時有不祥的氣；首領出來時一道光柱。環境：每個區域附近播那個主題的環境特效。
        /// </summary>
        void UpdateDungeonFx(ExploreRun run)
        {
            if (!exRoot || battle == null || Me == null) return;
            var me = Me.Feet; var map = run.map;
            var fin = map.arenas.Find(x => x.n == 4);
            foreach (var a in map.areas)
            {
                string th = DgFxTheme(run, a);
                bool final = fin != null && Vector3.Distance(fin.center, a.center) < 0.1f;
                // 出口門的封印
                for (int i = 0; i < a.exits.Count; i++)
                {
                    var c = a.exits[i]; var at = map.World(c.x, c.y) + Vector3.up * 1.6f;
                    bool near = Battle.FlatDistance(at, me) <= DgFxGateRange;
                    ExploreFx.Loop(exRoot, DgKey(a, i), "fx_DG_gate_idle_" + th, !a.cleared && near, () => at, 3.6f);
                }
                // 走進區域
                if (a.entered && exFxAreaEntered.Add(a))
                {
                    var ac = a.center;
                    string id = a.IsChest ? null : a.IsEvent ? "fx_DG_event_start" : a.IsBoss ? (final ? null : "fx_DG_midboss_rise") : "fx_DG_area_enter";
                    if (id != null) ExploreFx.Once(exRoot, id, () => ac + Vector3.up * (a.IsBoss ? 2f : 0.3f), a.IsBoss ? 6f : 7f);
                }
                // 清除
                if (a.cleared && exFxAreaCleared.Add(a))
                {
                    var ac = a.center;
                    string id = a.IsChest ? "fx_DG_chest_open" : run.map.nodeAreas ? "fx_DG_trap_room_clear" : "fx_DG_area_clear";
                    ExploreFx.Once(exRoot, id, () => ac + Vector3.up * 1f, 7f);
                    foreach (var c in a.exits) { var at = map.World(c.x, c.y) + Vector3.up * 1.6f; ExploreFx.Once(exRoot, "fx_DG_gate_open_" + th, () => at, 3.6f); }
                    Sfx.Play("charged", 0.5f, 1.2f);
                }
                // 王座的不祥氣息
                if (final) { var ac = a.center; ExploreFx.Loop(exRoot, DgThroneKey, "fx_DG_throne_aura", !a.entered && Battle.FlatDistance(ac, me) <= DgFxThroneRange, () => ac + Vector3.up * 2f, 8f); }
                // 環境：區域裡兩個點（不同角落）
                for (int k = 0; k < 2 && run.map.theme != "castle"; k++)   // 城堡：不播（原本照章節主題播熔岩冒煙，在室內牆邊像一團黑影）
                {
                    var off = new Vector3((k == 0 ? -1f : 1f) * a.rect.width * 0.3f, 0f, (k == 0 ? 1f : -1f) * a.rect.height * 0.3f) * ExploreMap.CellSize;
                    var at = a.center + off + Vector3.up * 0.8f;
                    ExploreFx.Loop(exRoot, DgKey(a, 100 + k), DgAmbient(th), Battle.FlatDistance(at, me) <= DgFxAmbientRange, () => at, 2.2f);
                }
            }
            // 最後的首領現身：王座裡首領第一次出現
            if (!exFxBossRose && fin != null && fin.entered && battle.IsBossWave && En.Alive)
            {
                exFxBossRose = true; var bc = En.Feet;
                ExploreFx.Once(exRoot, "fx_DG_boss_rise", () => bc + Vector3.up * 2.5f, 8f);
            }
        }
    }
}
