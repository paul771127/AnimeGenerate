using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 第一大關（使用者要求：第一章整章做成一張大地圖，約副本 3 倍大；dungeons.txt 的 ch1_volcano，style = chapter）：
    ///   ・劇情、教學：故事 1-1～1-4 的台詞放在各地點（dungeons.txt）；影片沿用故事 1-1、1-4 的（StoryVideo.ForDungeon）。
    ///   ・脫出點（地點寫 exit = yes）：清完那個地點後出現發光的法陣；走上去問「要存檔回村嗎？」→ 回村，出現在傳送陣旁邊；
    ///     補給完從傳送陣（🌀）選「回到炎之山」，就從那個脫出點繼續（清過的地點保持清除、找過的寶物不再出現）。
    ///   ・掉落：打倒怪、開寶箱拿到的東西馬上放進背包（不用通關，倒下也不會不見）。
    ///   ・倒下：損失這一級經驗條的 30%（不夠扣就降級；技能點、能力點不會少，升回去也不會再給），存檔清掉，下次從入口重新開始、敵人全部復活。
    ///   ・支線：三位 NPC 站在岔路的支線地點門口，走近會派任務（靶場、運送冷泉水、背誦守則）；接了才開始，失敗可以再接。
    ///   ・岩漿：路上、地點裡不定時從地面噴發（先冒煙發紅 1.4 秒），被噴到扣血；走道上偶爾落石（先出現黑影）。
    ///   ・身體障礙（倒下的岩柱、橫的岩梁、崩落的石牆）比一般副本多很多（SoloBattle.BodyGates.cs）。
    /// 只有單人（連線時照一般副本）。
    /// </summary>
    public partial class SoloBattle
    {
        const string ChapterId = "ch1_volcano", ChapterSaveKey = "sd_ch1_save";
        const float ChXpLoss = 0.3f;

        class ChSide { public string node, npc, name, look, title, desc, offer, thanks; public int gold, xp, rarity; }
        /// <summary>支線：哪個地點、誰派的、獎勵</summary>
        static readonly ChSide[] ChSides =
        {
            new ChSide { node = "quarry", npc = "cq_ayan", name = "礦工阿岩", look = "swordsman", title = "採石場的打靶練習", desc = "在舊採石場打中 12 個靶子（綠色稻草人別打）",
                offer = "我在練習打靶，可是熔岩小鬼的靶子冒得太快了……幫我打下 12 個好不好？綠色的稻草人是村民做的，別打到喔！", thanks = "好準！這是礦工的護符，帶著吧。", gold = 250, xp = 120, rarity = 2 },
            new ChSide { node = "cart", npc = "cq_mira", name = "米菈", look = "archer", title = "運送冷泉水", desc = "把冷泉水穩穩送到光圈，別晃、別被打到",
                offer = "我的貨車翻了……這罐冷泉水是要給礦工降溫的。你走路穩嗎？幫我送到前面的光圈！", thanks = "一滴都沒灑！這些是謝禮，收下吧。", gold = 300, xp = 150, rarity = 2 },
            new ChSide { node = "archive", npc = "cq_elder", name = "老礦工", look = "priest", title = "礦工的保命守則", desc = "聽老礦工唸守則，用說的背出來",
                offer = "年輕人，石壁上刻的是礦工的保命守則。我唸一句，你背一句——背得出來，我就把珍藏的東西給你。", thanks = "記住了就好。這是我年輕時用的東西，給你。", gold = 350, xp = 200, rarity = 3 },
        };

        class ChExit { public ExArea area; public Vector3 pos; public GameObject go; }
        class ChVent { public Vector3 pos; public float next, warnAt = -1f, eruptAt = -1f; public bool hitDone, painted; public GameObject go, ring, column, core; }
        class ChRock { public Vector3 pos; public float hitAt; public bool done; public GameObject go, shadow, rock, rubble; }

        readonly List<ChExit> chExits = new List<ChExit>();
        readonly List<ChVent> chVents = new List<ChVent>();
        readonly List<ChRock> chRocks = new List<ChRock>();
        readonly List<ExArea> chHeld = new List<ExArea>();
        readonly Dictionary<string, ExArea> chSideArea = new Dictionary<string, ExArea>();
        Transform chRoot; ExploreRun chRun;
        ChExit chExitNear, chExitDeclined;
        Vector3? chResumeAt; bool chVentSaid, chRockSaid;
        float chNextRock, chGateHintAt; Vector3 chLastFeet; Vector3 chMoveDir = Vector3.forward;
        static SkillDef chLavaSkill, chRockSkill;

        bool ChapterMode => dungeon != null && dungeon.IsChapter && exploreRun != null && exploreRun.IsDungeon && !coop && !NetMode;

        // ---------------------------------------------------------------- 存檔
        struct ChSave { public int exit; public HashSet<int> cleared, sparkles, entered, dead, explored; public Dictionary<string, ExQuestState> quests; }

        static ChSave? LoadChSave()
        {
            string s = Progress.Get?.Invoke(ChapterSaveKey);
            if (string.IsNullOrEmpty(s)) return null;
            var sv = new ChSave { exit = -1, cleared = new HashSet<int>(), sparkles = new HashSet<int>(), entered = new HashSet<int>(), dead = new HashSet<int>(), explored = new HashSet<int>(), quests = new Dictionary<string, ExQuestState>() };
            foreach (var part in s.Split(';'))
            {
                int eq = part.IndexOf('='); if (eq <= 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                if (k == "a") int.TryParse(v, out sv.exit);
                else if (k == "c" || k == "s" || k == "n" || k == "d" || k == "x")
                {
                    var set = k == "c" ? sv.cleared : k == "s" ? sv.sparkles : k == "n" ? sv.entered : k == "d" ? sv.dead : sv.explored;
                    foreach (var x in v.Split(',')) if (int.TryParse(x, out var n)) set.Add(n);
                }
                else if (k == "q") foreach (var x in v.Split(',')) { var kv = x.Split(':'); if (kv.Length == 2 && int.TryParse(kv[1], out var st)) sv.quests[kv[0]] = (ExQuestState)st; }
            }
            return sv.exit >= 0 ? sv : (ChSave?)null;
        }

        static void ClearChSave() => Progress.Set?.Invoke(ChapterSaveKey, "");

        void WriteChSave(ExploreRun run, ExArea at)
        {
            var c = new List<string>(); foreach (var a in run.map.areas) if (a.cleared) c.Add(a.index.ToString());
            var sp = new List<string>(); for (int i = 0; i < run.map.sparkles.Count; i++) if (run.map.sparkles[i].found) sp.Add(i.ToString());
            var q = new List<string>(); foreach (var cs in ChSides) { var qq = run.map.Quest(cs.npc); if (qq != null) q.Add($"{cs.npc}:{(int)(qq.state == ExQuestState.Active || qq.state == ExQuestState.Failed ? ExQuestState.Offered : qq.state)}"); }
            // 使用者要求：地圖探索程度、已通過的任務、已擊殺的敵人都要存（回村再回來接著打）
            var n = new List<string>(); foreach (var a in run.map.areas) if (a.entered) n.Add(a.index.ToString());
            var dd = new List<string>(); foreach (var mon in run.map.monsters) if (mon.dead) dd.Add(mon.id.ToString());
            var x = new List<string>(); foreach (var cell in run.explored) x.Add(((cell.x + 500) * 1000 + cell.y + 500).ToString());
            Progress.Set?.Invoke(ChapterSaveKey, $"a={at.index};c={string.Join(",", c)};s={string.Join(",", sp)};q={string.Join(",", q)};n={string.Join(",", n)};d={string.Join(",", dd)};x={string.Join(",", x)}");
        }

        /// <summary>傳送陣的「回到炎之山」：有存檔才有（回傳脫出點的名字）</summary>
        string ChapterSaveInfo()
        {
            var sv = LoadChSave(); var d = Dungeons.Get(ChapterId);
            if (sv == null || d == null || sv.Value.exit >= d.rooms.Count) return null;
            return d.rooms[sv.Value.exit].title;
        }

        void ChapterResumeFromWarp()
        {
            var d = Dungeons.Get(ChapterId);
            if (d != null) world.OnStartDungeon?.Invoke(d);
        }

        // ---------------------------------------------------------------- 開場
        /// <summary>StartDungeonRun：第一次先播 1-1 的開場影片＋開場劇情；有存檔＝直接回到脫出點</summary>
        void ChapterIntro(DungeonDef d, System.Action go)
        {
            if (LoadChSave() != null) { Say("🌀 回到脫出點，繼續冒險！", 3f); go(); return; }
            if (!Story.IsSeen("dg:" + d.id))
            {
                Story.MarkSeen("dg:" + d.id);
                var vclip = StoryVideo.ForDungeonIntro(d.id);
                StoryVideo.Play(vclip, () => ShowDialogue(d.before, go));
                return;
            }
            go(); Ticker(d.before);
        }

        /// <summary>SetupDungeonExplore 叫：放脫出點、支線 NPC、岩漿口；有存檔就套用</summary>
        void ChapterBegin(ExploreRun run)
        {
            ClearChapter();
            if (!ChapterMode) return;
            chRun = run;
            var map = run.map;
            if (exRoot == null) BuildExploreTemplates();
            chRoot = new GameObject("Chapter props").transform; chRoot.SetParent(exRoot, false);
            battle.failFilter = ChapterFailFilter;
            // 脫出點
            foreach (var a in map.areas)
            {
                if (!a.node.param.TryGetValue("exit", out var ev) || ev == "no") continue;
                var p = a.center + new Vector3(a.radius * 0.42f, 0f, -a.radius * 0.42f);
                if (!map.Walkable(p, 0.5f)) p = a.center + new Vector3(0f, 0f, -a.radius * 0.4f);
                var ex = new ChExit { area = a, pos = p, go = ExitVisual() };
                ex.go.transform.SetParent(chRoot, false); ex.go.transform.localPosition = p; ex.go.SetActive(false);
                chExits.Add(ex);
            }
            // 支線 NPC：站在支線地點裡、靠進來的那一邊
            foreach (var cs in ChSides)
            {
                var a = map.areas.Find(x => x.node.id == cs.node); if (a == null) continue;
                chSideArea[cs.node] = a;
                ExArea from = map.areas.Find(x => x.next.Contains(a.index));
                var dir = from != null ? Fighter.Flat(from.center - a.center).normalized : Vector3.left;
                var pos = a.center + dir * a.radius * 0.55f;
                map.npcs.Add(new ExNpc { id = cs.npc, name = cs.name, look = cs.look, quest = cs.npc, pos = pos });
                map.quests.Add(new ExQuest { id = cs.npc, title = "支線：" + cs.title, giver = cs.npc, kind = "side", desc = cs.desc, offer = cs.offer, thanks = cs.thanks, state = ExQuestState.Offered, gold = cs.gold, xp = cs.xp });
            }
            BuildVents(map);
            StageKit.Decorate(map, chRoot);   // 首領的岩洞入口、遠景岩塊（場景生成器的 3D 零件）
            // 存檔：清過的地點、找過的寶物、支線進度；位置回到脫出點
            var sv = LoadChSave();
            if (sv != null)
            {
                var s = sv.Value;
                foreach (var a in map.areas) if (s.cleared.Contains(a.index)) { a.entered = true; a.cleared = true; }
                foreach (var a in map.areas) if (s.entered.Contains(a.index)) a.entered = true;
                foreach (var mon in map.monsters) if (mon.area >= 0 && s.cleared.Contains(mon.area) || s.dead.Contains(mon.id)) mon.dead = true;
                foreach (var e in s.explored) run.explored.Add(new Vector2Int(e / 1000 - 500, e % 1000 - 500));
                for (int i = 0; i < map.sparkles.Count; i++) if (s.sparkles.Contains(i)) map.sparkles[i].found = true;
                foreach (var kv in s.quests) { var q = map.Quest(kv.Key); if (q != null) q.state = kv.Value; }
                int nc = 0; foreach (var a in map.areas) if (a.cleared) nc++;
                Say($"💾 讀取存檔：已清除 {nc} 個地點、打倒 {s.dead.Count} 隻敵人、地圖探索 {s.explored.Count} 格", 4f);
                var at = chExits.Find(e => e.area.index == s.exit);
                if (at != null) { chResumeAt = at.pos + new Vector3(-1.6f, 0f, 0f); chExitDeclined = at; }
            }
            chNextRock = Time.time + 12f; chLastFeet = Me != null ? Me.Feet : Vector3.zero;
            Say("🌋 第一大關・炎之山：清完發光的脫出點可以存檔回村；倒下會損失 30% 經驗，並從入口重新開始", 5f);
        }

        void ClearChapter()
        {
            if (chRoot) Destroy(chRoot.gameObject);
            chRoot = null; chRun = null; chExits.Clear(); chVents.Clear(); chRocks.Clear(); chHeld.Clear(); chSideArea.Clear();
            chExitNear = null; chExitDeclined = null; chResumeAt = null;
        }

        // ---------------------------------------------------------------- 每幀
        void UpdateChapter(ExploreRun run, float dt)
        {
            if (!ChapterMode || run != chRun || battle == null || Me == null) return;
            if (chResumeAt.HasValue)
            {
                // 回到脫出點：平移世界（跟衝刺一樣）
                var back = chResumeAt.Value - Me.Feet; back.y = 0f; chResumeAt = null;
                if (back.sqrMagnitude > 1e-4f) { ShiftWorld(-WorldFrame.DirFromWorld(back)); Me.head += back; dashMoveW += back; StopDash(); }
                chLastFeet = Me.Feet;
            }
            GrantChapterLootNow(run);
            UpdateSideQuests(run);
            // 脫出點：清完那個地點才出現；站上去（沒有在打仗）才問
            chExitNear = null;
            foreach (var e in chExits)
            {
                bool show = e.area.cleared && Battle.FlatDistance(e.pos, Me.Feet) < 40f;
                if (e.go && e.go.activeSelf != show) e.go.SetActive(show);
                if (!show) continue;
                float d = Battle.FlatDistance(e.pos, Me.Feet);
                if (chExitDeclined == e && d > 3f) chExitDeclined = null;
                if (d <= 1.6f && chExitDeclined != e && exNodeArea == null && bossIntroArea == null && !ChEnemyNear(7f)) chExitNear = e;
            }
            var mv = Me.Feet - chLastFeet; mv.y = 0f;
            if (mv.sqrMagnitude > 0.0004f) chMoveDir = Vector3.Lerp(chMoveDir, mv.normalized, 0.2f).normalized;
            chLastFeet = Me.Feet;
            bool calm = exploreRun.talking != null || bossIntroArea != null || chExitNear != null;
            UpdateVents(run, dt, calm);
            UpdateRocks(run, calm);
            UpdateChapterGates(run);
            FacePaintedChapter();
        }

        /// <summary>
        /// 門打不開時（使用者回報清完敵人後紅色的門過不去）：
        ///   ・守衛都倒了卻還沒清除的區域，補清除（巡邏的怪不算，見 ExploreRun.AreaAlive）。
        ///   ・站在關著的門前面，說明還差什麼（事件沒完成、還有幾隻守衛、支線的出口）。
        /// </summary>
        void UpdateChapterGates(ExploreRun run)
        {
            var map = run.map;
            foreach (var a in map.areas)
                if (a.entered && !a.cleared && !a.IsEvent && !a.IsBoss && !a.IsChest && a != exNodeArea && run.AreaAlive(a) == 0) run.ClearArea(a);
            if (Time.time < chGateHintAt) return;
            var pc = map.Cell(Me.Feet);
            for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++)
            {
                int x = pc.x + dx, z = pc.y + dz;
                if (map.At(x, z) != ExCell.Gate || map.GateOpen(x, z) || Battle.FlatDistance(map.World(x, z), Me.Feet) > 5.5f) continue;
                var c = new Vector2Int(x, z);
                var a = map.areas.Find(q => q.exits.Contains(c)); if (a == null) continue;
                chGateHintAt = Time.time + 6f;
                var side = System.Array.Find(ChSides, q => q.node == a.node.id);
                string why = side != null ? $"這是支線「{side.title}」的出口：跟{side.name}接任務、完成之後才會打開（主線走另一扇門）"
                    : !a.entered ? $"先到「{a.Title}」"
                    : a.IsEvent ? $"完成「{a.Title}」的試煉才會打開" + (a == exNodeArea ? "（試煉進行中）" : "")
                    : $"「{a.Title}」還有 {run.AreaAlive(a)} 隻守衛沒打倒（看小地圖的紅點）";
                Say("🚪 門還關著：" + why, 4f);
                return;
            }
        }

        bool ChEnemyNear(float r)
        {
            foreach (var m in battle.minions) if (m != null && m.Alive && Battle.FlatDistance(m.Feet, Me.Feet) < r) return true;
            return false;
        }

        /// <summary>掉落馬上放進背包（不用通關）；走道寶藏的金幣、支線的經驗也馬上拿到</summary>
        void GrantChapterLootNow(ExploreRun run)
        {
            if (run.chestsFound > 0)
            {
                for (int c = 0; c < run.chestsFound; c++) pendingItems.Add(ItemGen.Make(dropRng, dungeon.level + battle.levelBoost, Mathf.Max(2, ItemGen.RollRarity(dropRng, 0f, 2)), myClass));
                run.chestsFound = 0;
            }
            if (run.gold > 0) { pendingGold += run.gold; run.gold = 0; }
            if (run.xp > 0 && prof != null) { int ups = prof.AddXp(run.xp); run.xp = 0; Progress.SaveChar(prof); if (ups > 0) Say($"⬆ 升到 Lv{prof.level}！", 3f); }
            if (pendingItems.Count > 0 || pendingGold > 0 || pendingSouls.Count > 0 || pendingMats.Count > 0)
            {
                string got = GrantLoot("炎之山", false);
                if (!string.IsNullOrEmpty(got)) ChatLine(new StoryLine { text = "🎒 " + got.Replace("\n", "、") });
            }
            // 「通關後獲得」的字樣改掉（這一關是馬上拿到）
            foreach (var f in floaters) if (f.text != null && (f.text.Contains("過關後獲得") || f.text.Contains("通關後獲得"))) f.text = f.text.Replace("（過關後獲得）", "（已放進背包）").Replace("（通關後獲得）", "（已放進背包）").Replace("通關後獲得", "已放進背包");
            for (int i = 0; i < run.log.Count; i++) run.log[i] = run.log[i].Replace("（通關後獲得）", "（已放進背包）");
        }

        // ---------------------------------------------------------------- 支線
        /// <summary>UpdateDungeonExplore：支線地點在接任務之前不開始（等玩家跟 NPC 說話）</summary>
        void ChapterHoldStarts(ExploreRun run)
        {
            if (!ChapterMode) return;
            for (int i = run.areaStarts.Count - 1; i >= 0; i--)
            {
                var a = run.areaStarts[i];
                var cs = System.Array.Find(ChSides, x => x.node == a.node.id); if (cs == null) continue;
                var q = run.map.Quest(cs.npc);
                if (q != null && q.state == ExQuestState.Active) continue;
                run.areaStarts.RemoveAt(i);
                if (!chHeld.Contains(a)) chHeld.Add(a);
                if (q != null && q.state != ExQuestState.Done) Say($"💬 {cs.name}好像有事要拜託你（走到他旁邊）", 3f);
            }
        }

        void UpdateSideQuests(ExploreRun run)
        {
            foreach (var cs in ChSides)
            {
                if (!chSideArea.TryGetValue(cs.node, out var a)) continue;
                var q = run.map.Quest(cs.npc); if (q == null) continue;
                if (q.state == ExQuestState.Active && chHeld.Contains(a) && exNodeArea == null && !exNodeQueue.Contains(a))
                { chHeld.Remove(a); exNodeQueue.Enqueue(a); }   // 接了任務：開始
                if (q.state == ExQuestState.Active && a.cleared)
                {
                    q.state = ExQuestState.Done;
                    Economy.Gold += cs.gold;
                    int ups = prof != null ? prof.AddXp(cs.xp) : 0;
                    ItemGen.SetTheme = 0;
                    var it = ItemGen.Make(dropRng, dungeon.level + battle.levelBoost, cs.rarity, myClass);
                    Progress.AddToBagOrMail(it, q.title);
                    if (prof != null) Progress.SaveChar(prof);
                    Say($"✅ 完成「{q.title}」！+{cs.gold} 金幣、+{cs.xp} 經驗、[{Item.RarityNames[it.rarity]}] {it.name}" + (ups > 0 ? $"\n⬆ 升到 Lv{prof.level}！" : ""), 4.5f);
                    Sfx.Play("win", 0.7f); Haptics.Play(Haptics.Kind.Success);
                }
            }
        }

        /// <summary>支線進行中失敗（靶場沒打夠、核心被打壞…）：只算支線失敗，可以再跟 NPC 接一次；整個關卡不結束</summary>
        bool ChapterFailFilter(string why)
        {
            if (!ChapterMode || exNodeArea == null || Me == null || !Me.Alive) return false;
            var a = exNodeArea;
            var cs = System.Array.Find(ChSides, x => x.node == a.node.id); if (cs == null) return false;
            var q = exploreRun.map.Quest(cs.npc); if (q != null) q.state = ExQuestState.Failed;
            exNodeArea = null; battle.CleanupRoomEvent(); battle.arenaPoly = exSavedPoly; battle.bossPoint = null;
            if (!chHeld.Contains(a)) chHeld.Add(a);
            Say($"❌ 支線失敗：{why}\n再跟{cs.name}說一次話就能重來", 4f);
            Haptics.Play(Haptics.Kind.Warning);
            return true;
        }

        // ---------------------------------------------------------------- 脫出點
        GameObject ExitVisual()
        {
            var pe = PaintedExit(); if (pe) return pe;   // 手繪圖（SoloBattle.ChapterArt.cs）
            var root = new GameObject("Exit point");
            void Add(PrimitiveType t, Vector3 at, Vector3 s, Color c) { var g = Prim(t, c); g.transform.SetParent(root.transform, false); g.transform.localPosition = at; g.transform.localScale = s; }
            Add(PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f), new Vector3(2.4f, 0.01f, 2.4f), new Color(0.35f, 0.75f, 1f));
            Add(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1.7f, 0.01f, 1.7f), new Color(0.75f, 0.92f, 1f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Add(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 1.35f, 0.25f, Mathf.Sin(a) * 1.35f), new Vector3(0.22f, 0.5f, 0.22f), new Color(0.3f, 0.3f, 0.36f));
            }
            Add(PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), new Color(0.6f, 0.9f, 1f));   // 光柱：遠遠就看得到
            GlowHalo.Create(root.transform, new Vector3(0f, 1.2f, 0f), new Color(0.45f, 0.8f, 1f, 0.8f), 3.2f, 0.25f);
            return root;
        }

        void ChapterGoVillage(bool save)
        {
            var e = chExitNear; chExitNear = null;
            if (!save) { chExitDeclined = e; return; }
            if (e == null || exploreRun == null) return;
            WriteChSave(exploreRun, e.area);
            GrantChapterLootNow(exploreRun);
            if (prof != null) Progress.SaveChar(prof);
            Sfx.Play("summon", 0.8f); Haptics.Play(Haptics.Kind.Success);
            ShowSetup(); storyMapOpen = true; world.OpenTown(0); world.walkMode = true;
            TownWalk.SpawnNearWarp = true;   // 出現在傳送陣旁邊
            Say($"💾 已存檔（{e.area.Title}）。補給完，站上傳送陣選「🌋 回到炎之山」就能繼續", 6f);
        }

        void DrawChapterHud(float W, float H)
        {
            if (!ChapterMode || phase != Phase.Fighting || chExitNear == null) return;
            int fs = UiKit.BaseFont;
            float w = Mathf.Min(W * 0.86f, fs * 22f), h = fs * 8.2f;
            var r = new Rect((W - w) * 0.5f, H * 0.56f, w, h);
            UiKit.MenuCard(r, 0.92f, new Color(0.4f, 0.75f, 1f));
            UiKit.Text(new Rect(r.x + fs, r.y + fs * 0.4f, r.width - fs * 2, fs * 3.6f), $"🌀 脫出點：{chExitNear.area.Title}\n要存檔，回到炎之里補給嗎？（之後從村子的傳送陣回來，從這裡繼續）", fs, Color.white, TextAnchor.MiddleCenter, true);
            float bw = (r.width - fs * 3f) * 0.5f, by = r.yMax - fs * 3.4f;
            if (UiKit.Button(new Rect(r.x + fs, by, bw, fs * 2.6f), "🏘 存檔回村", fs, false, new Color(0.45f, 0.8f, 1f))) ChapterGoVillage(true);
            if (UiKit.Button(new Rect(r.x + fs * 2f + bw, by, bw, fs * 2.6f), "⚔ 繼續冒險", fs)) ChapterGoVillage(false);
        }

        // ---------------------------------------------------------------- 結算
        /// <summary>通關：故事 1-1～1-4 算通過（第二章照常開放），存檔清掉</summary>
        void ChapterWon()
        {
            ClearChSave();
            foreach (var id in new[] { "1-1", "1-2", "1-3", "1-4" }) Story.MarkCleared(id);
            if (exploreRun != null) GrantChapterLootNow(exploreRun);
        }

        /// <summary>倒下：損失 30% 經驗（可能降級），存檔清掉；撿到的東西都留著</summary>
        void ChapterDied()
        {
            ClearChSave();
            pendingItems.Clear(); pendingGold = 0; pendingSouls.Clear(); pendingMats.Clear();   // 該拿的已經放進背包了
            int lost = 0, downs = 0;
            if (prof != null) { downs = prof.LoseXp(ChXpLoss, out lost); Progress.SaveChar(prof); }
            Say($"💀 倒下了……損失 {lost} 經驗" + (downs > 0 ? $"，降到 Lv{prof.level}" : "") + "\n撿到的東西都還在背包裡。下次會從礦道口重新開始，敵人也全部復活了", 99f);
            Sfx.Play("lose"); Haptics.Play(Haptics.Kind.Warning);
        }

        // ---------------------------------------------------------------- 岩漿噴發
        void BuildVents(ExploreMap map)
        {
            var rng = new System.Random(4711);
            var startW = map.World(map.start.x, map.start.y);
            for (int x = 1; x < map.W - 1; x++) for (int z = 1; z < map.H - 1; z++)
            {
                if (!map.Open(x, z) || map.At(x, z) != ExCell.Floor) continue;
                var ar = map.AreaAt(new Vector2Int(x, z));
                if (ar != null && (ar.index < 2 || ar.IsBoss || ar.IsChest || chSideArea.ContainsValue(ar))) continue;   // 教學、首領、寶箱、支線地點不放
                if (rng.Next(100) >= (ar == null ? 7 : 5)) continue;
                var p = map.World(x, z) + new Vector3((float)rng.NextDouble() * 2f - 1f, 0f, (float)rng.NextDouble() * 2f - 1f);
                if (!map.Walkable(p, 0.4f) || Battle.FlatDistance(p, startW) < 14f) continue;
                bool ok = true;
                foreach (var v in chVents) if (Battle.FlatDistance(v.pos, p) < 7f) { ok = false; break; }
                foreach (var e in chExits) if (Battle.FlatDistance(e.pos, p) < 4f) ok = false;
                if (!ok || chVents.Count >= 140) continue;
                var vent = new ChVent { pos = p, next = Time.time + 4f + (float)rng.NextDouble() * 10f };
                if (!PaintedVent(vent)) VentVisual(vent);   // 手繪圖（SoloBattle.ChapterArt.cs）
                chVents.Add(vent);
            }
        }

        void VentVisual(ChVent v)
        {
            v.go = new GameObject("Lava vent"); v.go.transform.SetParent(chRoot, false); v.go.transform.localPosition = v.pos;
            GameObject Add(PrimitiveType t, Vector3 at, Vector3 s, Color c) { var g = Prim(t, c); g.transform.SetParent(v.go.transform, false); g.transform.localPosition = at; g.transform.localScale = s; return g; }
            Add(PrimitiveType.Sphere, new Vector3(0f, -0.02f, 0f), new Vector3(1.3f, 0.16f, 1.3f), new Color(0.13f, 0.1f, 0.1f));   // 焦黑的噴口
            v.core = Add(PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.55f, 0.01f, 0.55f), new Color(1f, 0.42f, 0.1f));
            v.ring = Add(PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(2.8f, 0.005f, 2.8f), new Color(1f, 0.15f, 0.08f)); v.ring.SetActive(false);
            v.column = Add(PrimitiveType.Cylinder, new Vector3(0f, 1.4f, 0f), new Vector3(0.9f, 1.4f, 0.9f), new Color(1f, 0.55f, 0.15f)); v.column.SetActive(false);
            v.go.SetActive(false);
        }

        void UpdateVents(ExploreRun run, float dt, bool calm)
        {
            float now = Time.time; const float Warn = 1.4f, Erupt = 0.9f, HitR = 1.4f;
            foreach (var v in chVents)
            {
                float d = Battle.FlatDistance(v.pos, Me.Feet);
                bool near = d < 26f;
                if (v.go.activeSelf != near) v.go.SetActive(near);
                if (!near) { v.warnAt = v.eruptAt = -1f; continue; }
                if (v.warnAt < 0f && v.eruptAt < 0f && now >= v.next && !calm) { v.warnAt = now; v.hitDone = false; }
                if (v.warnAt >= 0f)
                {
                    float k = (now - v.warnAt) / Warn;
                    if (v.painted) v.ring.SetActive(Mathf.Sin(now * (10f + 18f * k)) > -0.4f);   // 手繪：發紅的噴口越閃越快
                    else
                    {
                        v.ring.SetActive(true);
                        float s = 0.55f + 0.5f * k + 0.1f * Mathf.Sin(now * 30f);
                        v.core.transform.localScale = new Vector3(s, 0.01f, s);
                    }
                    if (!chVentSaid && d < 6f) { chVentSaid = true; Say("⚠ 地面冒煙發紅＝岩漿要噴了！快離開紅圈", 3f); }
                    if (k >= 1f) { v.warnAt = -1f; v.eruptAt = now; v.column.SetActive(true); Sfx.Play("boom", 0.5f); }
                }
                else if (v.eruptAt >= 0f)
                {
                    float k = (now - v.eruptAt) / Erupt;
                    if (v.painted) { int f = Mathf.Clamp(Mathf.FloorToInt(k * 4f), 0, 3); ObSetU(v.column, f * 0.25f, (f + 1) * 0.25f); v.ring.SetActive(true); }   // 4 格：剛噴、最高、最高、落下
                    else
                    {
                        float h = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 1.6f + 0.2f;
                        v.column.transform.localScale = new Vector3(0.9f, h, 0.9f); v.column.transform.localPosition = new Vector3(0f, h, 0f);
                    }
                    if (!v.hitDone && k > 0.1f)
                    {
                        v.hitDone = true;
                        if (d <= HitR + Me.bodyRadius && Me.Alive)
                        {
                            chLavaSkill ??= new SkillDef { id = "ch_lava", name = "岩漿噴發", color = new Color(1f, 0.45f, 0.1f) };
                            battle.ApplyRemoteHit(chLavaSkill, Mathf.Max(1, Mathf.RoundToInt(Me.maxHp * 0.09f)), Me.Chest, false);
                            Haptics.Play(Haptics.Kind.Heavy); Kick(8f, 0f);
                        }
                    }
                    if (k >= 1f) { v.eruptAt = -1f; v.column.SetActive(false); v.ring.SetActive(false); if (!v.painted) v.core.transform.localScale = new Vector3(0.55f, 0.01f, 0.55f); v.next = now + Random.Range(5f, 13f); }
                }
            }
        }

        // ---------------------------------------------------------------- 落石（走道上）
        void UpdateRocks(ExploreRun run, bool calm)
        {
            float now = Time.time; const float Fall = 1.6f, HitR = 1.25f;
            var map = run.map;
            bool corridor = map.AreaAt(map.Cell(Me.Feet)) == null;
            if (now >= chNextRock && corridor && !calm && Me.Alive)
            {
                chNextRock = now + Random.Range(7f, 14f);
                var p = Me.Feet + chMoveDir * Random.Range(1.5f, 3.5f); p.y = 0f;
                if (!map.Walkable(p, 0.4f)) p = Me.Feet;
                p.y = 0f;
                var rk = new ChRock { pos = p, hitAt = now + Fall };
                rk.go = new GameObject("Falling rock"); rk.go.transform.SetParent(chRoot, false); rk.go.transform.localPosition = p;
                rk.shadow = Prim(PrimitiveType.Cylinder, new Color(0.05f, 0.03f, 0.03f)); rk.shadow.transform.SetParent(rk.go.transform, false); rk.shadow.transform.localPosition = new Vector3(0f, 0.015f, 0f); rk.shadow.transform.localScale = new Vector3(0.4f, 0.005f, 0.4f);
                if (!PaintedRock(rk)) { rk.rock = Prim(PrimitiveType.Sphere, new Color(0.32f, 0.26f, 0.24f)); rk.rock.transform.SetParent(rk.go.transform, false); rk.rock.transform.localScale = new Vector3(1.1f, 0.85f, 1f); rk.rock.SetActive(false); }
                chRocks.Add(rk);
                if (!chRockSaid) { chRockSaid = true; Say("⚠ 頭上掉石頭！看到地上的黑影，快往旁邊閃", 3f); }
            }
            for (int i = chRocks.Count - 1; i >= 0; i--)
            {
                var rk = chRocks[i];
                float k = 1f - (rk.hitAt - now) / Fall;
                if (!rk.done)
                {
                    float s = Mathf.Lerp(0.4f, HitR * 2f, Mathf.Clamp01(k));
                    rk.shadow.transform.localScale = new Vector3(s, 0.005f, s);
                    if (k > 0.75f) { rk.rock.SetActive(true); rk.rock.transform.localPosition = new Vector3(0f, Mathf.Lerp(7f, 0.4f, (k - 0.75f) / 0.25f), 0f); }
                    if (now >= rk.hitAt)
                    {
                        rk.done = true; rk.rock.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                        if (rk.rubble != null) { rk.rock.SetActive(false); rk.rubble.SetActive(true); }   // 手繪：落地變成碎石
                        Sfx.Play("boom", 0.4f);
                        if (Battle.FlatDistance(rk.pos, Me.Feet) <= HitR + Me.bodyRadius && Me.Alive)
                        {
                            chRockSkill ??= new SkillDef { id = "ch_rock", name = "落石", color = new Color(0.6f, 0.5f, 0.4f) };
                            battle.ApplyRemoteHit(chRockSkill, Mathf.Max(1, Mathf.RoundToInt(Me.maxHp * 0.1f)), Me.Chest, false);
                            Haptics.Play(Haptics.Kind.Heavy); Kick(10f, 0f);
                        }
                    }
                }
                else if (now > rk.hitAt + 2.5f) { Destroy(rk.go); chRocks.RemoveAt(i); }
                else rk.shadow.SetActive(false);
            }
        }
    }
}
