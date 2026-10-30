using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事探索（使用者要求）的畫面：大迷宮（只畫玩家附近）、走動限制（牆、還沒打開的藤蔓門 → 拉回來）、NPC、道具、閃光點、
    /// 對話框、目標與任務清單、小地圖、結算。地圖與任務邏輯在 Explore.cs，戰鬥在 Combat.Explore.cs。
    /// 移動：用衝刺（比 5→握拳），沒有 10m×10m 場地限制，碰到牆才停（SoloBattle.Dash.cs）。
    /// </summary>
    public partial class SoloBattle
    {
        ExploreRun exploreRun, pendingExplore;
        Transform exRoot;
        GameObject exWallTpl, exGateTpl, exSealTpl, exHerbTpl, exSparkTpl, exAltarTpl, exLeverTpl, exKeyTpl, exShardTpl;
        readonly Dictionary<ExTerrain, GameObject> exTerrainTpl = new Dictionary<ExTerrain, GameObject>();
        readonly Dictionary<string, GameObject> exPropTpl = new Dictionary<string, GameObject>();
        readonly Dictionary<Vector2Int, GameObject> exWalls = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<object, GameObject> exProps = new Dictionary<object, GameObject>();
        readonly Dictionary<string, CharacterRig> exNpcRigs = new Dictionary<string, CharacterRig>();
        Vector2Int exLastCell = new Vector2Int(-999, -999);
        Vector3 exLastValid; bool exHasValid;
        float exPushSaidAt = -99f;
        bool exMapBig;
        const int ExViewCells = 8;           // 畫玩家附近幾格（約 24m）
        const float ExSparkleShow = 8f;      // 閃光點在這個距離內才看得到

        /// <summary>從副本入口進來：先放好場地，再開打</summary>
        void StartExplore()
        {
            storyMapOpen = false; storyStage = null;
            void Go()
            {
                if (GateArea(Go)) return;
                pendingExplore = new ExploreRun(JungleMaze.Build());
                StartBossBattle(false, true);
            }
            if (!Story.IsSeen("ex:jungle")) { Story.MarkSeen("ex:jungle"); ShowDialogue(ExLines(ExploreIntro), Go); }
            else Go();
        }

        static readonly string[] ExploreIntro =
        {
            "林語長老：旅人，你終於來了。",
            "林語長老：翠蔭迷林深處的腐化樹王奪走了「古樹之心」，整片叢林都在枯萎。",
            "林語長老：迷林很大，比 5 再握拳就能往前衝；牆擋住就停。小地圖在右上角，點一下可以放大。",
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
            battle.OnExAlert += (m, call) => { if (call) { Sfx.Play("roar", 0.5f, 1.3f); Say($"❗ {m.name}在呼叫同伴！", 2f); } else Sfx.Play("select", 0.4f, 0.6f); };
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
        }

        int exGateHash = -1;
        bool exGatesDirty(ExploreRun run)
        {
            int h = 0; foreach (var a in run.map.arenas) h = h * 2 + (a.cleared ? 1 : 0);
            foreach (var s in run.map.sparkles) h = h * 2 + (s.found ? 1 : 0);
            if (h == exGateHash) return false;
            exGateHash = h; foreach (var w in exWalls.Values) if (w) Destroy(w); exWalls.Clear();
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

        // ---------------------------------------------------------------- 畫面：迷宮、NPC、道具、閃光點
        void BuildExploreTemplates()
        {
            exRoot = new GameObject("Explore maze").transform;
            exWallTpl = Prim(PrimitiveType.Cube, new Color(0.13f, 0.32f, 0.16f)); exWallTpl.transform.SetParent(exRoot, false); exWallTpl.SetActive(false);
            exGateTpl = Prim(PrimitiveType.Cube, new Color(0.45f, 0.3f, 0.12f, 0.85f), true); exGateTpl.transform.SetParent(exRoot, false); exGateTpl.SetActive(false);
            exSealTpl = Prim(PrimitiveType.Cube, new Color(0.55f, 0.3f, 0.75f, 0.85f), true); exSealTpl.transform.SetParent(exRoot, false); exSealTpl.SetActive(false);
            exLeverTpl = Prim(PrimitiveType.Cylinder, new Color(0.85f, 0.7f, 0.3f)); exLeverTpl.transform.SetParent(exRoot, false); exLeverTpl.SetActive(false);
            exKeyTpl = Prim(PrimitiveType.Cube, new Color(0.95f, 0.75f, 0.3f), true); exKeyTpl.transform.SetParent(exRoot, false); exKeyTpl.SetActive(false);
            exShardTpl = Prim(PrimitiveType.Sphere, new Color(0.3f, 1f, 0.7f), true); exShardTpl.transform.SetParent(exRoot, false); exShardTpl.SetActive(false);
            exHerbTpl = Prim(PrimitiveType.Sphere, new Color(0.6f, 1f, 0.75f), true); exHerbTpl.transform.SetParent(exRoot, false); exHerbTpl.SetActive(false);
            exSparkTpl = Prim(PrimitiveType.Sphere, new Color(1f, 1f, 0.75f), true); exSparkTpl.transform.SetParent(exRoot, false); exSparkTpl.SetActive(false);
            exAltarTpl = Prim(PrimitiveType.Cylinder, new Color(0.5f, 1f, 0.6f, 0.8f), true); exAltarTpl.transform.SetParent(exRoot, false); exAltarTpl.SetActive(false);
            // 地形（暫代外觀；Codex 的叢林美術做好後換掉）：樹叢、岩壁、湖水、城寨木牆、古殿石牆、洞窟岩壁
            GameObject T(PrimitiveType t, Color c, bool see = false) { var g = Prim(t, c, see); g.transform.SetParent(exRoot, false); g.SetActive(false); return g; }
            exTerrainTpl[ExTerrain.Thicket] = exWallTpl;
            exTerrainTpl[ExTerrain.Rock] = T(PrimitiveType.Cube, new Color(0.42f, 0.4f, 0.37f));
            exTerrainTpl[ExTerrain.Water] = T(PrimitiveType.Cube, new Color(0.2f, 0.45f, 0.75f, 0.75f), true);
            exTerrainTpl[ExTerrain.Palisade] = T(PrimitiveType.Cube, new Color(0.45f, 0.3f, 0.16f));
            exTerrainTpl[ExTerrain.Ruin] = T(PrimitiveType.Cube, new Color(0.62f, 0.66f, 0.58f));
            exTerrainTpl[ExTerrain.Cave] = T(PrimitiveType.Cube, new Color(0.24f, 0.21f, 0.2f));
            // 景物障礙
            exPropTpl["tree"] = T(PrimitiveType.Cylinder, new Color(0.3f, 0.22f, 0.12f));
            exPropTpl["stump"] = T(PrimitiveType.Cylinder, new Color(0.4f, 0.28f, 0.15f));
            exPropTpl["boulder"] = T(PrimitiveType.Sphere, new Color(0.5f, 0.48f, 0.44f));
            exPropTpl["mushroom"] = T(PrimitiveType.Sphere, new Color(0.85f, 0.45f, 0.55f));
            exPropTpl["tent"] = T(PrimitiveType.Cube, new Color(0.6f, 0.5f, 0.32f));
            exPropTpl["crate"] = T(PrimitiveType.Cube, new Color(0.55f, 0.38f, 0.2f));
            exPropTpl["totem"] = T(PrimitiveType.Cylinder, new Color(0.5f, 0.25f, 0.2f));
            exPropTpl["pillar"] = T(PrimitiveType.Cylinder, new Color(0.7f, 0.74f, 0.66f));
            exPropTpl["statue"] = T(PrimitiveType.Capsule, new Color(0.6f, 0.68f, 0.6f));
            exPropTpl["campfire"] = T(PrimitiveType.Cylinder, new Color(1f, 0.55f, 0.2f), true);
            exPropTpl["altar"] = T(PrimitiveType.Cube, new Color(0.55f, 0.6f, 0.5f));
        }

        /// <summary>只畫玩家附近 ExViewCells 格內、旁邊是走道的牆（整塊牆的內部不用畫）</summary>
        void RefreshExploreCells(ExploreRun run, Vector2Int c)
        {
            var map = run.map;
            var keep = new HashSet<Vector2Int>();
            for (int x = c.x - ExViewCells; x <= c.x + ExViewCells; x++)
                for (int z = c.y - ExViewCells; z <= c.y + ExViewCells; z++)
                {
                    var k = new Vector2Int(x, z);
                    var cell = map.At(x, z);
                    bool gate = cell == ExCell.Gate && !map.GateOpen(x, z);
                    if (cell == ExCell.Floor || cell == ExCell.Gate && !gate) continue;
                    bool edge = false; foreach (var d in ExploreMap.Dirs) if (map.Open(x + d.x, z + d.y)) edge = true;
                    if (!edge && !gate) continue;
                    keep.Add(k);
                    if (exWalls.ContainsKey(k)) continue;
                    var t = map.TerrainAt(x, z);
                    if (!gate)
                    {
                        // Codex 的地形牆：地面中心、自己依格子大小縮放
                        var w = ExploreArt.CreateWall(t, x, z, ExploreMap.CellSize);
                        w.transform.SetParent(exRoot, false);
                        w.transform.localPosition = map.World(x, z);
                        exWalls[k] = w;
                        continue;
                    }
                    var go = Instantiate(gate ? (map.SealOf(x, z) != null ? exSealTpl : exGateTpl) : exTerrainTpl.TryGetValue(t, out var tt) ? tt : exWallTpl, exRoot);
                    go.SetActive(true);
                    float h = gate ? 2.2f : t == ExTerrain.Water ? 0.06f : t == ExTerrain.Palisade ? 2.6f : t == ExTerrain.Ruin ? 2.2f : t == ExTerrain.Cave ? 3f : 2.4f + ((x * 7 + z * 13) % 5) * 0.12f;   // 高低有一點變化；湖水是平的
                    go.transform.localPosition = map.World(x, z) + Vector3.up * h * 0.5f;
                    go.transform.localScale = new Vector3(ExploreMap.CellSize * (gate ? 0.9f : 1f), h, ExploreMap.CellSize * (gate ? 0.9f : 1f));
                    exWalls[k] = go;
                }
            foreach (var kv in new List<KeyValuePair<Vector2Int, GameObject>>(exWalls))
                if (!keep.Contains(kv.Key)) { if (kv.Value) Destroy(kv.Value); exWalls.Remove(kv.Key); }
        }

        void UpdateExploreProps(ExploreRun run)
        {
            var me = Me.Feet; float view = ExViewCells * ExploreMap.CellSize;
            // NPC：用角色模型，面向玩家
            foreach (var n in run.map.npcs)
            {
                bool show = !n.hidden && Battle.FlatDistance(n.pos, me) <= view && !(n.id == "lost" && (run.escortFollowing || battle.exEscort != null));
                exNpcRigs.TryGetValue(n.id, out var rig);
                if (!show) { if (rig) { Destroy(rig.gameObject); exNpcRigs.Remove(n.id); } continue; }
                if (!rig) { rig = CharacterRig.Create(n.look); exNpcRigs[n.id] = rig; SpawnFade.Play(rig.gameObject); }
                var fwd = Fighter.Flat(me - n.pos); if (fwd == Vector3.zero) fwd = Vector3.back;
                rig.SetPose(WorldFrame.FromWorld(n.pos), WorldFrame.DirFromWorld(fwd));
            }
            // 月光草、閃光點、祭壇
            foreach (var it in run.map.items) ExProp(it, exHerbTpl, !it.taken && !it.hidden && Battle.FlatDistance(it.pos, me) <= view, it.pos + Vector3.up * 0.35f, Vector3.one * (0.22f + 0.03f * Mathf.Sin(Time.time * 4f)));
            foreach (var s in run.map.sparkles)
            {
                float twinkle = Mathf.Max(0f, Mathf.Sin(Time.time * 3f + s.pos.x)) ;   // 偶爾閃一下（不是一直亮）
                ExProp(s, exSparkTpl, !s.found && Battle.FlatDistance(s.pos, me) <= ExSparkleShow, s.pos + Vector3.up * 0.5f, Vector3.one * (0.04f + 0.14f * twinkle * twinkle * twinkle));
            }
            // 岔路的機關（石柱）、鑰匙、翠玉碎片（浮在空中、慢慢轉）
            foreach (var sw in run.map.switches)
            {
                var tpl = sw.kind == "lever" ? exLeverTpl : sw.kind == "key" ? exKeyTpl : exShardTpl;
                bool show = !sw.done && Battle.FlatDistance(sw.pos, me) <= view;
                ExProp(sw, tpl, show, sw.pos + Vector3.up * (sw.kind == "lever" ? 0.6f : 1f + 0.08f * Mathf.Sin(Time.time * 2f)), sw.kind == "lever" ? new Vector3(0.35f, 0.6f, 0.35f) : Vector3.one * 0.28f);
                if (show && sw.kind != "lever" && exProps.TryGetValue(sw, out var sg) && sg) sg.transform.localRotation = Quaternion.Euler(0f, Time.time * 90f, 45f);
            }
            // 景物障礙（樹、巨石、巨菇、帳篷、城寨的木箱、古殿石柱…；碰撞在 ExploreMap.Walkable）
            foreach (var p in run.map.props)
            {
                if (!exPropTpl.TryGetValue(p.kind, out var tpl)) continue;
                float hh = p.kind == "tree" ? 3.2f : p.kind == "pillar" ? 2.6f : p.kind == "statue" ? 1.8f : p.kind == "tent" ? 1.6f : p.kind == "totem" ? 2.2f : p.kind == "mushroom" ? 1.4f : p.kind == "boulder" ? p.radius * 1.6f : p.kind == "campfire" ? 0.3f : p.kind == "altar" ? 1f : 0.8f;
                float d = p.radius * 2f;
                bool halfY = p.kind == "tree" || p.kind == "stump" || p.kind == "totem" || p.kind == "pillar" || p.kind == "campfire" || p.kind == "statue";   // 圓柱、膠囊的原始高度是 2
                var scale = halfY ? new Vector3(d, hh * 0.5f, d) : new Vector3(d, hh, d);
                ExProp(p, tpl, Battle.FlatDistance(p.pos, me) <= view, p.pos + Vector3.up * hh * 0.5f, scale);
            }
            ExProp(run, exAltarTpl, run.bossDead && !run.hasHeart, run.map.altar + Vector3.up * 1.2f, new Vector3(0.5f, 1.2f, 0.5f));
        }

        void ExProp(object key, GameObject tpl, bool show, Vector3 at, Vector3 scale)
        {
            exProps.TryGetValue(key, out var go);
            if (!show) { if (go) { Destroy(go); exProps.Remove(key); } return; }
            if (!go) { go = Instantiate(tpl, exRoot); go.SetActive(true); exProps[key] = go; }
            go.transform.localPosition = at; go.transform.localScale = scale;
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
                if (exploreRun.TapSparkle(s.id, Me.Feet) != null) { Sfx.Play("coin", 0.8f); Haptics.Play(Haptics.Kind.Success); Burst(s.pos + Vector3.up * 0.5f, new Color(1f, 0.95f, 0.6f)); }
                break;
            }
        }

        void ClearExplore()
        {
            if (exRoot) Destroy(exRoot.gameObject);
            exRoot = null; exWalls.Clear(); exProps.Clear();
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
                    var q = run.map.Quest(n.quest);
                    string mk = n.id == "guardian" && run.hasHeart ? " ❓" : q != null && q.state == ExQuestState.Offered ? " ❗" : q != null && q.state == ExQuestState.Ready ? " ❓" : "";
                    UiKit.Text(new Rect(sp.x - W * 0.15f, H - sp.y - fsS, W * 0.3f, fsS * 1.4f), n.name + mk, fsS, new Color(1f, 0.95f, 0.7f), TextAnchor.MiddleCenter);
                }
            DrawExploreMinimap(run, W, H);
            // 對話框（走近 NPC 自動跳出；按「好」關掉）
            if (run.talking != null && !string.IsNullOrEmpty(run.talkText))
            {
                var r = new Rect(W * 0.06f, H * 0.6f, W * 0.88f, H * 0.2f);
                UiKit.Card(r, 0.85f, new Color(0.35f, 0.7f, 0.45f), 0.95f);
                UiKit.Text(new Rect(r.x + fs, r.y + fs * 0.4f, r.width - fs * 2, fs * 1.4f), "💬 " + run.talking.name, fs, new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleLeft);
                UiKit.Text(new Rect(r.x + fs, r.y + fs * 1.9f, r.width - fs * 2, r.height - fs * 4.2f), run.talkText, fsS, Color.white, TextAnchor.UpperLeft);
                if (UiKit.Button(new Rect(r.xMax - W * 0.22f - fs * 0.5f, r.yMax - fs * 2.2f, W * 0.22f, fs * 1.8f), "好", fs)) run.CloseTalk();
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
