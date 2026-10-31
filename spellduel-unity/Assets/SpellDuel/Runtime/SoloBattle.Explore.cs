using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事探索（使用者要求）的畫面：大迷宮（只畫玩家附近）、走動限制（牆、還沒打開的藤蔓門 → 拉回來）、NPC、道具、閃光點、
    /// 對話框、目標與任務清單、小地圖、結算。地圖與任務邏輯在 Explore.cs，戰鬥在 Combat.Explore.cs。
    /// 移動：用衝刺（按住畫面中間），沒有 10m×10m 場地限制，碰到牆才停（SoloBattle.Dash.cs）。
    /// </summary>
    public partial class SoloBattle
    {
        ExploreRun exploreRun, pendingExplore;
        Transform exRoot;
        readonly HashSet<ExSwitch> exSwitchShownDone = new HashSet<ExSwitch>();
        readonly Dictionary<Fighter, (string text, float until)> exBubbles = new Dictionary<Fighter, (string, float)>();
        readonly List<(Transform mark, float until)> exAlertMarks = new List<(Transform, float)>();
        readonly HashSet<ExLandmark> exBannerSeen = new HashSet<ExLandmark>();
        float exBannerUntil = -1f;
        readonly Dictionary<Vector2Int, GameObject> exWalls = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<Vector2Int, GameObject> exUnder = new Dictionary<Vector2Int, GameObject>();   // 牆格底下的森林地面（樹底下不要透出真實地板）
        readonly Dictionary<object, GameObject> exProps = new Dictionary<object, GameObject>();
        readonly Dictionary<string, CharacterRig> exNpcRigs = new Dictionary<string, CharacterRig>();
        Vector2Int exLastCell = new Vector2Int(-999, -999);
        Vector3 exLastValid; bool exHasValid;
        float exPushSaidAt = -99f, exSparkleTapAt = -99f;
        bool exMapBig;
        const int ExViewCells = 6;           // 畫玩家附近幾格（一格 4m，約 24m）
        const float ExSparkleShow = 8f;      // 閃光點在這個距離內才看得到

        /// <summary>從副本入口進來：先放好場地，再開打</summary>
        void StartExplore()
        {
            storyMapOpen = false; storyStage = null;
            void Go()
            {
                if (GateArea(Go)) return;
                pendingExplore = new ExploreRun(JungleMaze.Build());
                ExploreFx.Prefetch();   // 特效影片：背景下載（第一次約 5MB）
                StartBossBattle(false, true);
            }
            if (!Story.IsSeen("ex:jungle")) { Story.MarkSeen("ex:jungle"); ShowDialogue(ExLines(ExploreIntro), Go); }
            else Go();
        }

        static readonly string[] ExploreIntro =
        {
            "林語長老：旅人，你終於來了。",
            "林語長老：翠蔭迷林深處的腐化樹王奪走了「古樹之心」，整片叢林都在枯萎。",
            "林語長老：迷林很大，按住畫面中間就會往你面向的方向衝，放開就停；牆擋住也會停。小地圖在右上角，點一下可以放大。",
            "林語長老：林子裡的怪一開始不會注意你，走太近、打牠、或打到牠旁邊的同伴，牠們就會撲過來。",
            "林語長老：留意地上偶爾閃一下的光點——走近點一下，也許會有意外的發現。",
        };

        /// <summary>「人物：台詞」→ 對話／字幕卡用的格式</summary>
        static List<StoryLine> ExLines(params string[] lines)
        {
            var l = new List<StoryLine>();
            foreach (var s in lines) { int i = s.IndexOf('：'); l.Add(i > 0 && i < 8 ? new StoryLine { speaker = s.Substring(0, i), text = s.Substring(i + 1) } : new StoryLine { text = s }); }
            return l;
        }

        /// <summary>StartBossBattle 叫：把這次探索接上戰鬥（敵人強度＝第二章叢林）</summary>
        void SetupExploreBattle()
        {
            var run = exploreRun;
            battle.enemyDmgMul = StoryMinionDmg.Length > 1 ? StoryMinionDmg[1] : 1f;
            battle.enemyHpMul = StoryMinionHpOf(1, battle.exBaseWave);
            battle.storyBossDmg = StoryBossDmg.Length > 1 ? StoryBossDmg[1] * LateBossMul : 1f;
            battle.exBossHp = (StoryBossHp.Length > 1 ? StoryBossHp[1] : 1600f) * LateBossMul;
            battle.enemyAccuracy = StoryEnemyAccuracy(1, difficulty);
            battle.StartExplore(run);
            if (area != null) area.BoundsOff = true;   // 不要 10m×10m 的場地框、不提示場地外（高度照樣檢查）
            battle.OnExSay += (m, text) => { exBubbles[m] = (text, Time.time + 2.2f); };   // 怪物的對話框（聊天、發現玩家時喊一句）
            battle.OnExAlert += (m, call) =>
            {
                if (call) { Sfx.Play("roar", 0.5f, 1.3f); Say($"❗ {m.name}在呼叫同伴！", 2f); } else Sfx.Play("select", 0.4f, 0.6f);
                // 頭上的「！」（Codex 的 AlertMark；會叫同伴的多一圈聲波）：顯示一下就收起來（動畫影片之後接）
                if (exRoot) { var mm = m; ExploreFx.Once(exRoot, call ? "fx_E2_roar_wave" : "fx_E2_alert", () => mm.head + Vector3.up * (call ? 0.2f : 0.7f), call ? 2f : 0.9f); }   // 影片版的「！」／聲波
                if (minionGo.TryGetValue(m, out var mr) && mr) { ExploreArt.AlertMark(mr.transform, call); var t = mr.transform.Find("AlertMark"); if (t) exAlertMarks.Add((t, Time.time + (call ? 1.8f : 1.1f))); }
            };
            exHasValid = false; exLastCell = new Vector2Int(-999, -999);
            Ticker(ExLines("📜 和林語長老說話，接下主線任務"));
        }

        void UpdateExplore(float dt)
        {
            var run = exploreRun;
            if (run == null || battle == null || Me == null) return;
            if (!exRoot) BuildExploreTemplates();
            // 迷宮跟著世界原點（衝刺時世界平移，這裡每幀對齊）
            exRoot.SetPositionAndRotation(WorldFrame.Marker.position, WorldFrame.Marker.rotation);
            if (phase == Phase.Fighting && Me.Alive)
            {
                ExKeepInside();
                run.Update(Me.Feet, battle.exEscort != null && battle.exEscort.Alive ? battle.exEscort.Feet : (Vector3?)null);
                ExTapSparkles();
            }
            foreach (var s in run.log) { Say(s, 3.5f); ChatLine(new StoryLine { text = s }); }
            run.log.Clear();
            var cell = run.map.Cell(Me.Feet);
            if (cell != exLastCell || exGatesDirty(run)) { exLastCell = cell; RefreshExploreCells(run, cell); }
            UpdateExploreProps(run);
            UpdateExploreFx(run);
        }

        // ---------------------------------------------------------------- 特效影片（影片生成機做的 mp4；ExploreFx.cs）
        readonly HashSet<object> exFxSeen = new HashSet<object>();
        readonly Dictionary<ExSeal, int> exFxSealHave = new Dictionary<ExSeal, int>();
        bool exFxInit, exFxBoss, exFxHeart;
        const float ExFxLoopRange = 12f;   // 循環的特效（火、孢子、閃光點、符文）只在這個距離內播

        void UpdateExploreFx(ExploreRun run)
        {
            var map = run.map; var me = Me.Feet;
            if (!exFxInit)
            {
                // 剛進來：已經發生過的事不補播
                exFxInit = true; exFxBoss = run.bossDead; exFxHeart = run.hasHeart;
                foreach (var a in map.arenas) if (a.cleared) exFxSeen.Add(a);
                foreach (var se in map.seals) { exFxSealHave[se] = se.have; if (se.open) exFxSeen.Add(se); }
                foreach (var sw in map.switches) if (sw.done) exFxSeen.Add(sw);
            }
            // 藤蔓門、封印打開：腐化的紫光熄滅、綠色光點與葉子爆開
            foreach (var a in map.arenas)
                if (a.cleared && exFxSeen.Add(a)) foreach (var c in a.gate) { var at = map.World(c.x, c.y) + Vector3.up * 1.3f; ExploreFx.Once(exRoot, "fx_A_unseal_burst", () => at, 3f); }
            foreach (var se in map.seals)
            {
                var ctr = Vector3.zero; foreach (var c in se.cells) ctr += map.World(c.x, c.y); if (se.cells.Count > 0) ctr /= se.cells.Count;
                var at = ctr + Vector3.up * 1.3f;
                if (exFxSealHave.TryGetValue(se, out int had) && se.have > had && se.id == "seal4") ExploreFx.Once(exRoot, "fx_A5_socket_pulse", () => at + Vector3.up * 0.3f, 1.4f);   // 翠玉碎片放進凹槽
                exFxSealHave[se] = se.have;
                if (se.open && exFxSeen.Add(se)) ExploreFx.Once(exRoot, "fx_A_unseal_burst", () => at, 3.2f);
            }
            // 岔路：石雕機關的符文（平常呼吸亮暗、啟動時光環＋光束）、鑰匙／碎片撿起來
            foreach (var sw in map.switches)
            {
                var at = sw.pos;
                if (sw.kind == "lever") ExploreFx.Loop(exRoot, sw, "fx_B1_rune_idle", !sw.done && Battle.FlatDistance(at, me) <= ExFxLoopRange, () => at + Vector3.up * 0.55f, 0.7f);
                if (sw.done && exFxSeen.Add(sw)) ExploreFx.Once(exRoot, sw.kind == "lever" ? "fx_B1_activate" : "fx_B2_pickup", () => at + Vector3.up * (sw.kind == "lever" ? 0.5f : 1f), sw.kind == "lever" ? 2.2f : 1.3f);
            }
            // 隱藏閃光點：偶爾閃一下（依種類：寶藏金光、樹洞綠光、小動物的眼睛、藤牆縫隙的細白光）
            foreach (var sp in map.sparkles)
            {
                string id = sp.kind == "spirit" ? "fx_C1_glint_green" : sp.kind == "beast" ? "fx_C1_glint_eyes" : sp.kind == "passage" ? "fx_C1_glint_white" : "fx_C1_glint_gold";
                var at = sp.pos;
                ExploreFx.Loop(exRoot, sp, id, !sp.found && Battle.FlatDistance(at, me) <= ExSparkleShow, () => at + Vector3.up * (sp.kind == "passage" ? 0.9f : 0.45f), sp.kind == "passage" ? 1.4f : 0.9f);
            }
            // 篝火的火焰、巨菇的孢子
            foreach (var p in map.props)
            {
                if (p.kind != "campfire" && p.kind != "mushroom") continue;
                var at = p.pos; bool fire = p.kind == "campfire";
                ExploreFx.Loop(exRoot, p, fire ? "fx_D3_fire" : "fx_D4_spores", Battle.FlatDistance(at, me) <= ExFxLoopRange, () => at + Vector3.up * (fire ? 0.45f : 1.4f), fire ? 1.0f : 2.2f);
            }
            // 古樹之心：Boss 倒下時從祭壇浮出、拿走時化成光
            if (run.bossDead && !exFxBoss) { exFxBoss = true; var at = map.altar; ExploreFx.Once(exRoot, "fx_C4_heart_rise", () => at + Vector3.up * 1.1f, 2.4f); }
            if (run.hasHeart && !exFxHeart) { exFxHeart = true; var at = map.altar; ExploreFx.Once(exRoot, "fx_C4_heart_take", () => at + Vector3.up * 1.2f, 1.6f); }
        }

        int exGateHash = -1;
        bool exGatesDirty(ExploreRun run)
        {
            int h = 0; foreach (var a in run.map.arenas) h = h * 2 + (a.cleared ? 1 : 0);
            foreach (var s in run.map.sparkles) h = h * 2 + (s.found ? 1 : 0);
            foreach (var se in run.map.seals) h = h * 7 + se.have + (se.open ? 5 : 0);
            if (h == exGateHash) return false;
            exGateHash = h; foreach (var w in exWalls.Values) if (w) Destroy(w); exWalls.Clear(); foreach (var w in exUnder.Values) if (w) Destroy(w); exUnder.Clear();
            return true;
        }

        /// <summary>
        /// 人真的走進牆裡、或走過還沒打開的藤蔓門：把世界推回去（角色回到上一個能站的位置）。
        /// AR 裡手機就是角色，所以「拉回來」是平移世界（和衝刺一樣），看起來就是被擋住、退回原處。
        /// </summary>
        void ExKeepInside()
        {
            var map = exploreRun.map; var p = Me.Feet;
            if (map.Walkable(p, 0.3f)) { exLastValid = p; exHasValid = true; return; }
            if (!exHasValid) { exLastValid = Vector3.zero; exHasValid = true; }
            var back = exLastValid - p; back.y = 0f;
            if (back.sqrMagnitude < 1e-6f) return;
            ShiftWorld(-WorldFrame.DirFromWorld(back));
            Me.head += back; dashMoveW += back;
            StopDash();
            if (Time.time - exPushSaidAt > 2.5f)
            {
                exPushSaidAt = Time.time;
                var gate = map.ClosedGateAt(p); var seal = map.ClosedSealAt(p);
                Say(seal != null ? $"🔒 {seal.name}：{seal.hint}（{seal.have}/{seal.need}）" : gate != null ? $"🌿 藤蔓擋住了去路——先打倒「{gate.name}」的守衛" : "🌳 過不去", seal != null ? 4f : 2f);
                Haptics.Play(Haptics.Kind.Light);
            }
        }

        // ---------------------------------------------------------------- 畫面：迷宮、NPC、道具、閃光點（Codex 的 ExploreArt）
        void BuildExploreTemplates()
        {
            exRoot = new GameObject("Explore maze").transform;
            // 天空、太陽、雲、遠方的森林：只建一次（不跟著 24m 串流）
            var sky = ExploreArt.CreateBackdrop(exploreRun.map);
            if (sky) sky.transform.SetParent(exRoot, false);
        }

        /// <summary>藤蔓門、封印要橫跨走道：走道是南北向就不轉，東西向轉 90°</summary>
        static Quaternion ExGateRot(ExploreMap map, int x, int z) => map.Open(x, z + 1) || map.Open(x, z - 1) ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

        /// <summary>只畫玩家附近 ExViewCells 格內：地面、旁邊是走道的牆（整塊牆的內部不用畫）、藤蔓門與封印</summary>
        void RefreshExploreCells(ExploreRun run, Vector2Int c)
        {
            var map = run.map; float cs = ExploreMap.CellSize;
            var keep = new HashSet<Vector2Int>(); var keepUnder = new HashSet<Vector2Int>();
            for (int x = c.x - ExViewCells; x <= c.x + ExViewCells; x++)
                for (int z = c.y - ExViewCells; z <= c.y + ExViewCells; z++)
                {
                    var k = new Vector2Int(x, z);
                    var cell = map.At(x, z);
                    // 牆格底下鋪森林地面（使用者回報樹底下會透出地板）；湖水格不用（整片湖面在地標裡）
                    if (cell == ExCell.Wall && map.InGrid(x, z) && map.TerrainAt(x, z) != ExTerrain.Water)
                    {
                        keepUnder.Add(k);
                        if (!exUnder.ContainsKey(k))
                        {
                            var t = map.TerrainAt(x, z);
                            var g = ExploreArt.CreateGround(t == ExTerrain.Cave || t == ExTerrain.Rock ? ExGround.Cave : t == ExTerrain.Ruin ? ExGround.Stone : t == ExTerrain.Palisade ? ExGround.Dirt : ExGround.Grass, x, z, cs);
                            if (g) { g.transform.SetParent(exRoot, false); g.transform.localPosition = map.World(x, z) + Vector3.down * 0.004f; exUnder[k] = g; }
                        }
                    }
                    if (cell == ExCell.Wall)
                    {
                        bool edge = false; foreach (var d in ExploreMap.Dirs) if (map.Open(x + d.x, z + d.y)) edge = true;
                        if (!edge) continue;
                    }
                    keep.Add(k);
                    if (exWalls.ContainsKey(k)) continue;
                    GameObject go;
                    if (cell == ExCell.Floor) go = ExploreArt.CreatePath(map, x, z);   // 明顯的踩實小徑，轉角照 PathFillet 導圓角（會看四周哪邊通）
                    else if (cell == ExCell.Gate)
                    {
                        var seal = map.SealOf(x, z);
                        if (seal != null) go = ExploreArt.CreateSeal(seal, x, z, cs);   // 會依 seal.have 亮凹槽、open 時只剩框
                        else
                        {
                            int an = 1; foreach (var a in map.arenas) if (a.gate.Contains(k)) an = a.n;
                            go = ExploreArt.CreateGate(an, cs);
                            ExploreArt.SetGateOpen(go, map.GateOpen(x, z));
                        }
                        if (go) go.transform.localRotation = ExGateRot(map, x, z);
                    }
                    else go = ExploreArt.CreateWall(map, x, z);   // 兩面臨路的凸牆角會導圓；湖水格不畫方塊（整片圓弧湖面在地標裡）
                    if (!go) continue;
                    var rot = go.transform.localRotation;
                    go.transform.SetParent(exRoot, false);
                    go.transform.localPosition = map.World(x, z); go.transform.localRotation = rot;
                    exWalls[k] = go;
                }
            foreach (var kv in new List<KeyValuePair<Vector2Int, GameObject>>(exWalls))
                if (!keep.Contains(kv.Key)) { if (kv.Value) Destroy(kv.Value); exWalls.Remove(kv.Key); }
            foreach (var kv in new List<KeyValuePair<Vector2Int, GameObject>>(exUnder))
                if (!keepUnder.Contains(kv.Key)) { if (kv.Value) Destroy(kv.Value); exUnder.Remove(kv.Key); }
        }

        void UpdateExploreProps(ExploreRun run)
        {
            var me = Me.Feet; float view = ExViewCells * ExploreMap.CellSize; var map = run.map;
            // NPC：用角色模型＋Codex 的服裝，面向玩家；頭上的任務標記（1 ❗ 有任務、2 ❓ 可以回報）
            foreach (var n in map.npcs)
            {
                bool show = !n.hidden && Battle.FlatDistance(n.pos, me) <= view && !(n.id == "lost" && (run.escortFollowing || battle.exEscort != null));
                exNpcRigs.TryGetValue(n.id, out var rig);
                if (rig && Battle.FlatDistance(WorldFrame.ToWorld(rig.transform.position), n.pos) > 3f) { Destroy(rig.gameObject); exNpcRigs.Remove(n.id); rig = null; }   // 換位置了（守林人到祭壇旁）：在新位置淡入
                if (!show) { if (rig) { Destroy(rig.gameObject); exNpcRigs.Remove(n.id); } continue; }
                if (!rig) { rig = CharacterRig.Create(n.look); rig.InCombat = false; ExploreArt.DressNpc(rig, n.id); if (n.look == "archer") SlingBow(rig); exNpcRigs[n.id] = rig; SpawnFade.Play(rig.gameObject); }
                var fwd = Fighter.Flat(me - n.pos); if (fwd == Vector3.zero) fwd = Vector3.back;
                rig.SetPose(WorldFrame.FromWorld(n.pos), WorldFrame.DirFromWorld(fwd));
                ExploreArt.QuestMark(rig.transform, ExNpcMark(run, n));
            }
            // 月光草、閃光點
            foreach (var it in map.items) ExArt(it, () => ExploreArt.CreateHerb(), !it.taken && !it.hidden && Battle.FlatDistance(it.pos, me) <= view, it.pos);
            foreach (var sp in map.sparkles)
            {
                var g = ExArt(sp, () => ExploreArt.CreateSparkle(sp.kind), !sp.found && Battle.FlatDistance(sp.pos, me) <= ExSparkleShow, sp.pos);
                if (g) { float tw = Mathf.Max(0f, Mathf.Sin(Time.time * 3f + sp.pos.x)); g.transform.localScale = Vector3.one * (0.5f + 2f * tw * tw * tw); }   // 偶爾閃一下（不是一直亮）
            }
            // 岔路的石雕機關、古銅鑰匙、翠玉碎片（拿走／啟動後切狀態；機關留在原地）
            foreach (var sw in map.switches)
            {
                var g = ExArt(sw, () => ExploreArt.CreateSwitch(sw), Battle.FlatDistance(sw.pos, me) <= view, sw.pos);
                if (!g) { exSwitchShownDone.Remove(sw); continue; }
                if (sw.done && exSwitchShownDone.Add(sw)) ExploreArt.SetSwitchDone(g);
                if (!sw.done && sw.kind != "lever")
                {
                    var t = g.transform.Find(sw.kind == "key" ? "BronzeKey" : "JadeShard");   // 浮著慢慢轉（正式動畫之後接）
                    if (t) t.localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                }
            }
            // 景物障礙（樹、巨石、巨菇、帳篷、木箱、圖騰、石柱、雕像、篝火、祭壇；碰撞在 ExploreMap.Walkable）
            foreach (var p in map.props)
            {
                var g = ExArt(p, () => ExploreArt.CreateProp(p), Battle.FlatDistance(p.pos, me) <= view, p.pos);
                if (g && p.kind == "altar") ExploreArt.SetAltarGlow(g, run.bossDead && !run.hasHeart);   // Boss 倒下後浮出古樹之心，拿走就熄
            }
            // 地標（營地、湖、城寨、洞窟、古殿…）與競技場：整塊在附近才建
            foreach (var l in map.landmarks)
            {
                var ctr = map.World(l.rect.x, l.rect.y) + (map.World(l.rect.xMax, l.rect.yMax) - map.World(l.rect.x, l.rect.y)) * 0.5f;
                float r = Mathf.Max(l.rect.width, l.rect.height) * ExploreMap.CellSize * 0.5f;
                ExArt(l, () => ExploreArt.CreateLandmark(l, map), Battle.FlatDistance(ctr, me) <= view + r, null);
                if (l.found && l.kind != "start" && l.kind != "arena" && exBannerSeen.Add(l)) { ExploreArt.LandmarkFound(l); exBannerUntil = Time.time + 2.5f; }
            }
            foreach (var a in map.arenas) ExArt(a, () => ExploreArt.CreateArena(a.n, a.radius), Battle.FlatDistance(a.center, me) <= view + a.radius, a.center);
            // 地名橫幅、怪物頭上的「！」：顯示一下就收
            if (exBannerUntil > 0f && Time.time > exBannerUntil) { exBannerUntil = -1f; ExploreArt.LandmarkFound(null); }
            for (int i = exAlertMarks.Count - 1; i >= 0; i--)
                if (!exAlertMarks[i].mark || Time.time > exAlertMarks[i].until) { if (exAlertMarks[i].mark) exAlertMarks[i].mark.gameObject.SetActive(false); exAlertMarks.RemoveAt(i); }
        }

        /// <summary>NPC 頭上的標記：0 沒有、1 ❗ 有任務可以接、2 ❓ 可以回報</summary>
        static int ExNpcMark(ExploreRun run, ExNpc n)
        {
            if (n.id == "guardian" && run.hasHeart) return 2;
            var q = run.map.Quest(n.quest);
            if (q != null && q.state == ExQuestState.Offered) return 1;
            if (q != null && q.state == ExQuestState.Ready) return 2;
            foreach (var dq in run.map.quests) if (dq.kind == "deliver" && dq.state == ExQuestState.Active && dq.target == n.id) return 2;   // 送東西的對象
            return 0;
        }

        /// <summary>附近才建、離開就拆（at＝null：模型自己已經放在正確位置）</summary>
        GameObject ExArt(object key, System.Func<GameObject> make, bool show, Vector3? at)
        {
            exProps.TryGetValue(key, out var go);
            if (!show) { if (go) { Destroy(go); exProps.Remove(key); } return null; }
            if (!go)
            {
                go = make(); if (!go) return null;
                var lp = go.transform.localPosition;
                go.transform.SetParent(exRoot, false);
                go.transform.localPosition = at ?? lp;
                exProps[key] = go;
            }
            return go;
        }

        /// <summary>點畫面上的閃光點（手指離它的螢幕位置 12% 畫面寬以內、人在 6m 內）</summary>
        void ExTapSparkles()
        {
            if (!cam || exploreRun.talking != null) return;
            bool tap = Input.GetMouseButtonDown(0); Vector2 tp = Input.mousePosition;
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) { tap = true; tp = Input.GetTouch(0).position; }
            if (!tap) return;
            foreach (var s in exploreRun.map.sparkles)
            {
                if (s.found || Battle.FlatDistance(s.pos, Me.Feet) > ExploreRun.SparkleTapRange) continue;
                var sp = cam.WorldToScreenPoint(WorldFrame.FromWorld(s.pos + Vector3.up * 0.5f));
                if (sp.z <= 0f || Vector2.Distance(new Vector2(sp.x, sp.y), tp) > Screen.width * 0.12f) continue;
                if (exploreRun.TapSparkle(s.id, Me.Feet) != null) { exSparkleTapAt = Time.time; Sfx.Play("coin", 0.8f); Haptics.Play(Haptics.Kind.Success); Burst(s.pos + Vector3.up * 0.5f, new Color(1f, 0.95f, 0.6f)); var sat = s.pos; ExploreFx.Once(exRoot, "fx_C2_discover", () => sat + Vector3.up * 0.6f, 1.8f); }
                break;
            }
        }

        void ClearExplore()
        {
            if (exRoot) Destroy(exRoot.gameObject);
            if (area != null) area.BoundsOff = false;
            ExploreFx.Clear(); exFxSeen.Clear(); exFxSealHave.Clear(); exFxInit = false;
            exRoot = null; exWalls.Clear(); exUnder.Clear(); exBubbles.Clear(); exProps.Clear(); exSwitchShownDone.Clear(); exAlertMarks.Clear(); exBannerSeen.Clear();
            if (exBannerUntil > 0f || exploreRun != null) ExploreArt.LandmarkFound(null);
            exBannerUntil = -1f;
            foreach (var r in exNpcRigs.Values) if (r) Destroy(r.gameObject);
            exNpcRigs.Clear(); exGateHash = -1; exLastCell = new Vector2Int(-999, -999);
            exploreRun = null;
        }

        // ---------------------------------------------------------------- 介面：目標、任務、小地圖、對話框、NPC 名牌
        void DrawExploreHud(float W, float H)
        {
            var run = exploreRun;
            if (run == null || battle == null || phase != Phase.Fighting) return;
            int fs = Mathf.RoundToInt(H * 0.022f), fsS = Mathf.RoundToInt(H * 0.018f);
            // 目標（上方中間）
            UiKit.Pill(H * 0.115f, "🎯 " + run.Objective, fs, new Color(0.7f, 1f, 0.75f), W * 0.5f);
            // 任務清單（左側）
            float y = H * 0.2f, lw = W * 0.34f;
            foreach (var q in run.VisibleQuests())
            {
                if (y > H * 0.42f) break;
                string mark = q.state == ExQuestState.Done ? "✔" : q.state == ExQuestState.Ready ? "❗" : q.state == ExQuestState.Failed ? "✖" : "•";
                var col = q.state == ExQuestState.Done ? new Color(0.6f, 0.75f, 0.6f) : q.state == ExQuestState.Ready ? new Color(1f, 0.85f, 0.4f) : Color.white;
                UiKit.Text(new Rect(W * 0.02f, y, lw, fsS * 1.4f), $"{mark} {(q.hiddenQuest ? "✨" : "")}{q.title} {q.Progress}", fsS, col, TextAnchor.MiddleLeft);
                y += fsS * 1.45f;
            }
            // NPC 名牌與任務標記（! 有任務、? 可以回報）
            if (cam)
                foreach (var n in run.map.npcs)
                {
                    if (n.hidden || Battle.FlatDistance(n.pos, Me.Feet) > 14f) continue;
                    var sp = cam.WorldToScreenPoint(WorldFrame.FromWorld(n.pos + Vector3.up * 2.1f));
                    if (sp.z <= 0f) continue;
                    UiKit.Text(new Rect(sp.x - W * 0.15f, H - sp.y - fsS, W * 0.3f, fsS * 1.4f), n.name, fsS, new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter);
                }
            DrawExploreBubbles(W, H, fsS);
            DrawExploreMinimap(run, W, H);
        }

        /// <summary>怪物頭上的對話框：白底圓角框＋下面的小尖角，跟著怪物移動</summary>
        void DrawExploreBubbles(float W, float H, int fsS)
        {
            if (!cam || exBubbles.Count == 0) return;
            List<Fighter> gone = null;
            foreach (var kv in exBubbles)
            {
                var m = kv.Key; var (text, until) = kv.Value;
                if (m == null || !m.Alive || Time.time > until) { (gone ??= new List<Fighter>()).Add(m); continue; }
                if (Battle.FlatDistance(m.Feet, Me.Feet) > Battle.ExSayShow) continue;   // 走遠了（8m 外）就看不到
                var sp = cam.WorldToScreenPoint(WorldFrame.FromWorld(m.head + Vector3.up * 0.55f));
                if (sp.z <= 0f) continue;
                float fade = Mathf.Clamp01((until - Time.time) / 0.3f) * Mathf.Clamp01((Time.time - (until - 2.2f)) / 0.15f);
                // 大小：中文字約 1 個字寬，超過 W*0.42 就換行
                float maxW = W * 0.42f, oneLine = text.Length * fsS * 1.02f;
                int lines = Mathf.Max(1, Mathf.CeilToInt(oneLine / maxW));
                var size = new Vector2(Mathf.Min(oneLine, maxW), lines * fsS * 1.35f);
                float bw = size.x + fsS * 1.2f, bh = size.y + fsS * 0.8f;
                var r = new Rect(Mathf.Clamp(sp.x - bw * 0.5f, 4f, W - bw - 4f), Mathf.Clamp(H - sp.y - bh - fsS * 0.6f, 4f, H - bh - 4f), bw, bh);
                GUI.color = new Color(0f, 0f, 0f, 0.35f * fade); GUI.DrawTexture(new Rect(r.x + 2, r.y + 3, r.width, r.height), Texture2D.whiteTexture);   // 影子
                GUI.color = new Color(1f, 0.98f, 0.92f, 0.95f * fade); GUI.DrawTexture(r, Texture2D.whiteTexture);
                float tx = Mathf.Clamp(sp.x, r.x + fsS, r.xMax - fsS);
                for (int i = 0; i < 4; i++) GUI.DrawTexture(new Rect(tx - (4 - i) * 2f, r.yMax + i * 2f, (4 - i) * 4f, 2f), Texture2D.whiteTexture);   // 小尖角
                GUI.color = Color.white;
                UiKit.Text(new Rect(r.x + fsS * 0.6f, r.y + fsS * 0.4f, size.x, size.y), text, fsS, new Color(0.15f, 0.12f, 0.1f, fade), TextAnchor.MiddleCenter, false);
            }
            if (gone != null) foreach (var g in gone) exBubbles.Remove(g);
        }

        // ---------------------------------------------------------------- NPC 對話框（使用者要求：不透明、最上層；說「好／繼續」或點畫面跳下一段；
        //                                                                    要接任務時問「要接受嗎？」，說「是／要」「否／不要」或點按鈕）
        bool ExDialogOpen => exploreRun != null && exploreRun.talking != null && phase == Phase.Fighting;
        float exDialogLock;

        void ExDialogRects(float W, float H, out Rect card, out Rect yes, out Rect no)
        {
            card = new Rect(W * 0.05f, H * 0.52f, W * 0.9f, H * 0.3f);
            float bw = card.width * 0.4f, bh = H * 0.06f, by = card.yMax - bh - H * 0.02f;
            yes = new Rect(card.x + card.width * 0.07f, by, bw, bh);
            no = new Rect(card.xMax - card.width * 0.07f - bw, by, bw, bh);
        }

        /// <summary>DrawHud 之前：對話框開著時先吃掉點擊（不會點到後面的技能卡），點了就跳下一段／選是否</summary>
        void ExDialogInput()
        {
            if (!ExDialogOpen) return;
            var e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseUp) return;
            if (e.type == EventType.MouseUp && Time.time >= exDialogLock)
            {
                var run = exploreRun;
                ExDialogRects(Screen.width, Screen.height, out _, out var yes, out var no);
                if (!run.asking) { run.NextTalk(); Sfx.Play("click", 0.5f); exDialogLock = Time.time + 0.25f; }
                else if (yes.Contains(e.mousePosition)) ExAnswer(true);
                else if (no.Contains(e.mousePosition)) ExAnswer(false);
            }
            e.Use();
        }

        /// <summary>語音：「好／繼續」下一段；問任務時「是／要／接受」或「否／不要／算了」。有用到回傳 true</summary>
        bool ExDialogSpeech(string text)
        {
            if (!ExDialogOpen || string.IsNullOrEmpty(text) || Time.time < exDialogLock) return false;
            var run = exploreRun;
            if (run.asking)
            {
                foreach (var w in new[] { "不要", "不用", "不接", "不好", "不是", "否", "拒絕", "拒绝", "算了", "下次" }) if (text.Contains(w)) { ExAnswer(false); return true; }
                foreach (var w in new[] { "是", "要", "接受", "好", "可以", "沒問題", "没问题", "OK", "ok" }) if (text.Contains(w)) { ExAnswer(true); return true; }
                return true;   // 問問題時其他的話不當指令
            }
            foreach (var w in new[] { "好", "繼續", "继续", "下一", "嗯", "知道", "OK", "ok" })
                if (text.Contains(w)) { run.NextTalk(); Sfx.Play("click", 0.5f); exDialogLock = Time.time + 0.8f; return true; }
            return true;   // 對話中：不放招、不喝藥
        }

        void ExAnswer(bool yes)
        {
            exploreRun.Accept(yes);
            exDialogLock = Time.time + 0.8f;
            Sfx.Play(yes ? "charged" : "click", 0.6f);
            if (yes) Haptics.Play(Haptics.Kind.Success);
        }

        /// <summary>DrawHud 之後畫（最上層）：不透明的對話框</summary>
        /// <summary>NPC 不打架：弓從左手拿下來斜揹在背上（使用者回饋：獵人拿弓的位置怪怪的）</summary>
        static void SlingBow(CharacterRig rig)
        {
            Transform hand = null, spine = null;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) { if (t.name == "HandL") hand = t; else if (t.name == "Spine") spine = t; }
            var bow = hand ? hand.Find("Bow") : null;
            if (!bow || !spine) return;
            bow.SetParent(spine, false);
            bow.localPosition = new Vector3(0.02f, 0.3f, -0.26f);
            bow.localRotation = Quaternion.LookRotation(new Vector3(-0.45f, 0.9f, 0f), Vector3.back);   // 弓身從右下斜到左上，弓背貼著背、弦朝外
            bow.localScale = new Vector3(0.85f, 0.85f / 1.1f, 0.85f);   // Spine 有拉長 1.1 倍
        }

        void DrawExploreDialog()
        {
            if (!ExDialogOpen) return;
            var run = exploreRun;
            if (string.IsNullOrEmpty(run.talkText) && !run.asking) { run.CloseTalk(); return; }
            float W = Screen.width, H = Screen.height;
            int fs = Mathf.RoundToInt(H * 0.024f), fsS = Mathf.RoundToInt(H * 0.02f);
            ExDialogRects(W, H, out var card, out var yes, out var no);
            GUI.color = new Color(0.08f, 0.14f, 0.1f, 1f); GUI.DrawTexture(card, Texture2D.whiteTexture);   // 不透明底
            GUI.color = new Color(0.45f, 0.8f, 0.5f, 1f);
            float b = Mathf.Max(2f, H * 0.003f);
            GUI.DrawTexture(new Rect(card.x, card.y, card.width, b), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(card.x, card.yMax - b, card.width, b), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(card.x, card.y, b, card.height), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(card.xMax - b, card.y, b, card.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            UiKit.Text(new Rect(card.x + fs, card.y + fs * 0.5f, card.width - fs * 2, fs * 1.5f), "💬 " + run.talking.name, fs, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleLeft, true);
            var body = new Rect(card.x + fs, card.y + fs * 2.2f, card.width - fs * 2, yes.y - card.y - fs * 2.6f);
            if (!run.asking)
            {
                UiKit.Text(body, run.talkText, fsS, Color.white, TextAnchor.UpperLeft);
                UiKit.Text(new Rect(card.x, yes.y, card.width - fs, yes.height), "🎤 說「好」「繼續」或點畫面 ▶", fsS, new Color(0.75f, 0.95f, 0.8f), TextAnchor.MiddleRight);
            }
            else
            {
                var q = run.offering;
                UiKit.Text(body, $"要接受任務「{q.title}」嗎？\n{q.desc}" + (q.gold > 0 || q.xp > 0 ? $"\n獎勵：💰{q.gold}・經驗 {q.xp}" : ""), fs, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Card(yes, 1f, new Color(0.35f, 0.85f, 0.45f), 1f); UiKit.Text(yes, "✔ 接受（是／要）", fs, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Card(no, 1f, new Color(0.85f, 0.45f, 0.4f), 1f); UiKit.Text(no, "✖ 不要（否）", fs, Color.white, TextAnchor.MiddleCenter, true);
            }
        }

        /// <summary>小地圖（右上角；點一下放大）：走過的地方、牆、藤蔓門、NPC、任務道具、自己的方向</summary>
        void DrawExploreMinimap(ExploreRun run, float W, float H)
        {
            var map = run.map;
            float size = exMapBig ? Mathf.Min(W, H) * 0.8f : Mathf.Min(W, H) * 0.3f;
            int span = exMapBig ? map.W : 25;
            var box = exMapBig ? new Rect((W - size) / 2f, (H - size) / 2f, size, size) : new Rect(W - size - W * 0.02f, H * 0.16f, size, size);
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(box, Texture2D.whiteTexture);
            var pc = map.Cell(Me.Feet);
            int x0 = exMapBig ? 0 : pc.x - span / 2, z0 = exMapBig ? 0 : pc.y - span / 2;
            float cs = size / span;
            for (int x = x0; x < x0 + span; x++)
                for (int z = z0; z < z0 + span; z++)
                {
                    if (!map.InGrid(x, z) || !run.explored.Contains(new Vector2Int(x, z))) continue;
                    var cell = map.At(x, z);
                    Color c = cell == ExCell.Gate ? (map.GateOpen(x, z) ? new Color(0.55f, 0.8f, 0.5f, 0.9f) : new Color(0.9f, 0.35f, 0.25f, 0.95f))
                        : cell == ExCell.Wall ? MiniTerrainColor(map.TerrainAt(x, z)) : map.GroundAt(x, z) == ExGround.Stone ? new Color(0.85f, 0.85f, 0.8f, 0.85f) : map.GroundAt(x, z) == ExGround.Cave ? new Color(0.55f, 0.5f, 0.48f, 0.85f) : new Color(0.75f, 0.85f, 0.7f, 0.85f);
                    GUI.color = c;
                    GUI.DrawTexture(new Rect(box.x + (x - x0) * cs, box.y + (span - 1 - (z - z0)) * cs, cs + 0.5f, cs + 0.5f), Texture2D.whiteTexture);
                }
            void Dot(Vector3 p, Color c, float k)
            {
                var cc = map.Cell(p); if (cc.x < x0 || cc.y < z0 || cc.x >= x0 + span || cc.y >= z0 + span || !run.explored.Contains(cc)) return;
                float px = box.x + (p.x / ExploreMap.CellSize + map.start.x - x0 + 0.5f) * cs, py = box.y + (span - 1 - (p.z / ExploreMap.CellSize + map.start.y - z0) + 0.5f) * cs;
                GUI.color = c; GUI.DrawTexture(new Rect(px - cs * k / 2f, py - cs * k / 2f, cs * k, cs * k), Texture2D.whiteTexture);
            }
            foreach (var n in map.npcs) if (!n.hidden) { var q = map.Quest(n.quest); Dot(n.pos, q != null && (q.state == ExQuestState.Offered || q.state == ExQuestState.Ready) ? new Color(1f, 0.85f, 0.2f) : new Color(0.4f, 0.8f, 1f), 1.4f); }
            foreach (var it in map.items) if (!it.taken) Dot(it.pos, new Color(0.5f, 1f, 0.7f), 1f);
            foreach (var sw in map.switches) if (!sw.done) Dot(sw.pos, sw.kind == "shard" ? new Color(0.3f, 1f, 0.75f) : new Color(1f, 0.8f, 0.3f), 1.3f);   // 走過看到才會標
            foreach (var a in map.arenas) if (!a.cleared) Dot(a.center, new Color(1f, 0.3f, 0.3f), 1.8f);
            if (run.bossDead && !run.hasHeart) Dot(map.altar, new Color(0.4f, 1f, 0.5f), 1.6f);
            // 自己：白點＋面向
            Dot(Me.Feet, Color.white, 1.6f);
            Dot(Me.Feet + Fighter.Flat(Me.forward) * ExploreMap.CellSize, new Color(1f, 1f, 1f, 0.6f), 0.9f);
            GUI.color = Color.white;
            // 放大時：發現過的地標名字
            if (exMapBig)
                foreach (var l in map.landmarks)
                {
                    if (!l.found || l.kind == "arena") continue;
                    float lx = box.x + ((l.rect.xMin + l.rect.xMax) * 0.5f - x0 + 0.5f) * cs, ly = box.y + (span - 1 - ((l.rect.yMin + l.rect.yMax) * 0.5f - z0) + 0.5f) * cs;
                    UiKit.Text(new Rect(lx - size * 0.15f, ly - cs, size * 0.3f, cs * 2f), l.name, Mathf.RoundToInt(H * 0.016f), new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter);
                }
            if (GUI.Button(box, GUIContent.none, GUIStyle.none)) exMapBig = !exMapBig;
        }

        static Color MiniTerrainColor(ExTerrain t) => t switch
        {
            ExTerrain.Water => new Color(0.25f, 0.5f, 0.85f, 0.9f), ExTerrain.Rock => new Color(0.35f, 0.33f, 0.3f, 0.9f), ExTerrain.Palisade => new Color(0.45f, 0.3f, 0.15f, 0.95f),
            ExTerrain.Ruin => new Color(0.4f, 0.45f, 0.38f, 0.9f), ExTerrain.Cave => new Color(0.18f, 0.15f, 0.14f, 0.9f), _ => new Color(0.1f, 0.25f, 0.12f, 0.9f),
        };

        // ---------------------------------------------------------------- 結算
        void ExploreResult()
        {
            var run = exploreRun;
            if (battle.stageCleared && run != null)
            {
                bool first = !Story.IsSeen("ex!jungle");
                Story.MarkSeen("ex!jungle");
                float rmul = 1f + battle.levelBoost / (2f * LevelPerDifficulty);
                int lv = prof != null ? prof.level : 20;
                ItemGen.SetTheme = 1;
                pendingItems.Add(ItemGen.Make(dropRng, lv + battle.levelBoost, 3, myClass));                 // 保底一件史詩
                int done = 0; foreach (var q in run.map.quests) if (q.state == ExQuestState.Done && q.kind != "main") done++;
                for (int i = 0; i < done / 2; i++) pendingItems.Add(ItemGen.Make(dropRng, lv + battle.levelBoost, Mathf.Max(2, ItemGen.RollRarity(dropRng, 0f, 2)), myClass));   // 支線、隱藏任務：每兩個多一件
                pendingGold += Mathf.RoundToInt(run.gold * rmul);
                string loot = GrantLoot("翠蔭迷林 探索完成", false);
                int xp = Mathf.RoundToInt(run.xp * rmul);
                int ups = prof != null ? prof.AddXp(xp) : 0;
                if (prof != null) Progress.SaveChar(prof);
                BuildVictory("翠蔭迷林 探索完成", "ex_jungle", 900f, xp, ups, $"完成任務 {done + 1} 個" + (first ? "・首次通關！" : ""));
                Say($"🌿 翠蔭迷林 探索完成！獲得經驗 {xp}" + (ups > 0 ? $"，升到 Lv{prof.level}！" : "") + (loot.Length > 0 ? "\n" + loot : ""), 99f);
                Sfx.Play("win"); Haptics.Play(Haptics.Kind.Success);
            }
            else { LoseLoot(); Say("💀 你在迷林中倒下了……整理好裝備再來挑戰吧（地圖每次都一樣，可以記住路線）", 99f); Sfx.Play("lose"); Haptics.Play(Haptics.Kind.Warning); }
        }
    }
}
