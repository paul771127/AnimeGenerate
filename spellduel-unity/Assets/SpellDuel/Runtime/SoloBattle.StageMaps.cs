using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事小關的地圖（使用者要求：第一大關太長，拆回 1-1～1-4 的小關；每一關都是一張可以走的短地圖，
    /// 參考守望傳說的關卡：有 NPC、支線任務、隱藏彩蛋、小遊戲挑戰、劇情）。
    ///   ・地圖寫在 Resources/stage_maps.txt（格式同 dungeons.txt，[dungeon stage_1-1]，style = stage）；
    ///     wave 寫 1～3＝這一關的第一～最後一波（照 story.txt 的 waves 換算），event 是小遊戲試煉（打靶、符文、鏡像…）。
    ///   ・支線：地點寫 side＝任務名稱、giver／giverlook／offer／thanks／sidedesc／gold／xp／rarity
    ///     → 那個地點門口站一位 NPC，跟他說話接任務才開始；沒過可以再接，不影響主線（SoloBattle.Chapter.cs 的支線流程）。
    ///   ・隱藏的星星：每關 3 顆，藏在岔路盡頭、地點角落（走過去自動撿）；存在 sd_star_關卡（撿過的不再出現）。
    ///     3 顆都找到：神秘盒子＋300 金幣（每關一次）。
    ///   ・古老的石碑：地點寫 secret／secret2＝碑文（彩蛋、世界觀）；走近就讀，第一次讀給一點金幣。
    /// 只有單人（故事合作照原本的死鬥）。新手第一次打 1-1、1-2 的戰鬥教學還是在原本的小場地。
    /// </summary>
    public partial class SoloBattle
    {
        public const int StageStars = 3;
        const float StarView = 10f, StarPick = 1.4f, TabletRead = 2f;

        class StageStar { public int bit; public Vector3 pos; public GameObject go; public bool got; }
        class StageTablet { public string key, text, title; public Vector3 pos; public GameObject go; public bool read; }

        readonly List<StageStar> stStars = new List<StageStar>();
        readonly List<StageTablet> stTablets = new List<StageTablet>();
        string stStageId; float stHudUntil;

        static string StarKey(string id) => "sd_star_" + id;
        public static int StarBits(string stageId) { int.TryParse(Progress.Get?.Invoke(StarKey(stageId)) ?? "", out var b); return b; }
        public static int StarCount(string stageId) { int b = StarBits(stageId), n = 0; for (int i = 0; i < StageStars; i++) if ((b & (1 << i)) != 0) n++; return n; }

        /// <summary>這一關有沒有小關地圖（新手第一次的戰鬥教學照原本的小場地）</summary>
        DungeonDef StageMapFor(StoryStage st)
        {
            if (st == null || NetMode || StoryCoop) return null;
            var d = Dungeons.StageMap(st.id); if (d == null) return null;
            var p = Progress.Char(myClass);
            bool tut = p != null && p.cls == Skills.Novice && difficulty == 0 && (st.id == "1-1" && !BtFlag("11") || st.id == "1-2" && !BtFlag("12") && myLoadout.Contains("nv_swing"));
            return tut ? null : d;
        }

        /// <summary>把 stage_maps.txt 的地圖套上這一關：波次換算、強度照故事關卡</summary>
        static DungeonDef StageMapDef(StoryStage st, DungeonDef src)
        {
            var d = new DungeonDef
            {
                id = src.id, name = st.title, desc = src.desc, level = Story.LevelBand(st.theme).lo, story = st,
                storyMode = st.mode == "explore" ? "explore" : "stage", minPlayers = 1, maxPlayers = 1, style = "stage",
            };
            int w0 = st.FirstWave, w1 = Mathf.Max(st.FirstWave, st.LastWave);
            foreach (var r in src.rooms)
            {
                var n = new DungeonRoom { id = r.id, kind = r.kind, title = r.title, line = r.line, theme = st.theme };
                n.wave = r.wave <= 0 ? 0 : w0 + Mathf.RoundToInt(Mathf.Clamp(r.wave - 1, 0, 2) * (w1 - w0) * 0.5f);
                n.next.AddRange(r.next); n.npcs.AddRange(r.npcs);
                foreach (var kv in r.param) n.param[kv.Key] = kv.Value;
                n.enter.AddRange(r.enter); n.clear.AddRange(r.clear); n.bossHalf.AddRange(r.bossHalf);
                d.rooms.Add(n);
            }
            return d;
        }

        static int PInt(DungeonRoom r, string k, int def) => r.param.TryGetValue(k, out var v) && int.TryParse(v, out var n) ? n : def;
        static string PStr(DungeonRoom r, string k, string def) => r.param.TryGetValue(k, out var v) && !string.IsNullOrEmpty(v) ? v : def;

        /// <summary>SetupDungeonExplore 叫：支線 NPC、星星、石碑</summary>
        void StageMapBegin(ExploreRun run)
        {
            ClearChapter(); stStars.Clear(); stTablets.Clear();
            if (!SideQuestMode || dungeon.story == null) return;
            chRun = run; stStageId = dungeon.story.id;
            var map = run.map;
            if (exRoot == null) BuildExploreTemplates();
            chRoot = new GameObject("Stage map props").transform; chRoot.SetParent(exRoot, false);
            battle.failFilter = ChapterFailFilter;
            // 支線
            foreach (var r in dungeon.rooms)
            {
                if (!r.param.TryGetValue("side", out var title) || string.IsNullOrEmpty(title)) continue;
                chSides.Add(new ChSide
                {
                    node = r.id, npc = "sq_" + r.id, name = PStr(r, "giver", "旅人"), look = PStr(r, "giverlook", "priest"), title = title,
                    desc = PStr(r, "sidedesc", title), offer = PStr(r, "offer", "可以幫我一個忙嗎？"), thanks = PStr(r, "thanks", "謝謝你！這是謝禮。"),
                    gold = PInt(r, "gold", 150 + 50 * dungeon.story.theme), xp = PInt(r, "xp", 80 + 40 * dungeon.story.theme), rarity = Mathf.Clamp(PInt(r, "rarity", 2), 1, 4),
                });
            }
            PlaceSideNpcs(map);
            PlaceStars(map);
            PlaceTablets(map);
        }

        void StageMapHello()
        {
            var st = dungeon.story;
            int have = StarCount(st.id);
            Say($"🗺 {st.title}：清除地點會打開下一扇門\n❗ NPC 有支線、⭐ 是小遊戲試煉（不強制）\n岔路和角落還藏著 {StageStars} 顆星星與古老的石碑（已找到 {have}/{StageStars}）", 6f);
            stHudUntil = Time.time + 8f;
        }

        // ---------------------------------------------------------------- 星星
        void PlaceStars(ExploreMap map)
        {
            int bits = StarBits(stStageId);
            var startW = map.World(map.start.x, map.start.y);
            var cand = new List<Vector3>();
            // 岔路、走道的盡頭（只有一個出口的格子）
            for (int x = 1; x < map.W - 1; x++) for (int z = 1; z < map.H - 1; z++)
            {
                if (!map.Open(x, z) || map.At(x, z) != ExCell.Floor || map.AreaAt(new Vector2Int(x, z)) != null) continue;
                int open = 0; foreach (var dd in ExploreMap.Dirs) if (map.Open(x + dd.x, z + dd.y)) open++;
                if (open == 1) cand.Add(map.World(x, z));
            }
            // 地點的角落（不夠時）
            foreach (var a in map.areas)
            {
                if (a.IsBoss || map.arenas.Exists(q => q.n == 4 && Vector3.Distance(q.center, a.center) < 0.1f)) continue;
                var rc = a.rect;
                foreach (var c in new[] { new Vector2Int(rc.xMin, rc.yMin), new Vector2Int(rc.xMax, rc.yMax), new Vector2Int(rc.xMax, rc.yMin), new Vector2Int(rc.xMin, rc.yMax) })
                    cand.Add(map.World(c.x, c.y));
            }
            var rng = new System.Random(stStageId.GetHashCode() & 0x7fffffff);
            var picked = new List<Vector3>();
            for (int pass = 0; pass < 2 && picked.Count < StageStars; pass++)
            {
                float minGap = pass == 0 ? 14f : 6f;
                var pool = new List<Vector3>(cand);
                while (pool.Count > 0 && picked.Count < StageStars)
                {
                    int i = rng.Next(pool.Count); var p = pool[i]; pool.RemoveAt(i);
                    if (Battle.FlatDistance(p, startW) < 8f || !map.Walkable(p, 0.3f)) continue;
                    bool ok = true;
                    foreach (var q in picked) if (Battle.FlatDistance(q, p) < minGap) { ok = false; break; }
                    foreach (var s in map.sparkles) if (Battle.FlatDistance(s.pos, p) < 1.5f) ok = false;
                    foreach (var n in map.npcs) if (Battle.FlatDistance(n.pos, p) < 2f) ok = false;
                    if (ok) picked.Add(p);
                }
            }
            for (int i = 0; i < picked.Count; i++)
                if ((bits & (1 << i)) == 0) stStars.Add(new StageStar { bit = i, pos = picked[i] });
        }

        GameObject StarVisual()
        {
            var root = new GameObject("Hidden star");
            var gem = Prim(PrimitiveType.Cube, new Color(1f, 0.85f, 0.25f)); gem.name = "Gem";
            gem.transform.SetParent(root.transform, false); gem.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            gem.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f); gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var gem2 = Prim(PrimitiveType.Cube, new Color(1f, 0.95f, 0.6f)); gem2.transform.SetParent(gem.transform, false);
            gem2.transform.localScale = Vector3.one * 0.8f; gem2.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            GlowHalo.Create(root.transform, new Vector3(0f, 0.7f, 0f), new Color(1f, 0.85f, 0.35f, 0.7f), 1.3f, 0.3f);
            return root;
        }

        void UpdateStars()
        {
            var me = Me.Feet;
            foreach (var s in stStars)
            {
                if (s.got) continue;
                bool show = Battle.FlatDistance(s.pos, me) <= StarView;
                if (show && !s.go) { s.go = StarVisual(); s.go.transform.SetParent(chRoot, false); s.go.transform.localPosition = s.pos; SpawnFade.Play(s.go); }
                if (s.go)
                {
                    if (s.go.activeSelf != show) s.go.SetActive(show);
                    var gem = s.go.transform.Find("Gem");
                    if (gem) { gem.localRotation = Quaternion.Euler(45f, Time.time * 120f, 45f); gem.localPosition = new Vector3(0f, 0.7f + Mathf.Sin(Time.time * 2.5f) * 0.08f, 0f); }
                }
                if (Battle.FlatDistance(s.pos, me) > StarPick) continue;
                s.got = true; if (s.go) Destroy(s.go);
                int bits = StarBits(stStageId) | (1 << s.bit);
                Progress.Set?.Invoke(StarKey(stStageId), bits.ToString());
                int have = StarCount(stStageId);
                Sfx.Play("coin", 0.9f, 1.3f); Haptics.Play(Haptics.Kind.Success);
                Burst(s.pos + Vector3.up * 0.7f, new Color(1f, 0.9f, 0.4f));
                ChatEvent($"⭐ 找到隱藏的星星！（{have}/{StageStars}）");
                stHudUntil = Time.time + 5f;
                if (have >= StageStars && Progress.Get?.Invoke("sd_starrw_" + stStageId) != "1")
                {
                    Progress.Set?.Invoke("sd_starrw_" + stStageId, "1");
                    Economy.AddItem(MysteryBox.Id, 1); Economy.Gold += 300;
                    Say($"🌟 {StageStars} 顆星星全部找到了！\n獎勵：{MysteryBox.Icon} {MysteryBox.Name} ×1、💰 300 金幣（在背包打開盒子）", 5f);
                    Sfx.Play("levelup", 0.8f);
                }
                else Say($"⭐ 找到隱藏的星星！（這一關 {have}/{StageStars}）", 3f);
            }
        }

        // ---------------------------------------------------------------- 石碑
        void PlaceTablets(ExploreMap map)
        {
            foreach (var a in map.areas)
            {
                int k = 0;
                foreach (var key in new[] { "secret", "secret2" })
                {
                    if (!a.node.param.TryGetValue(key, out var text) || string.IsNullOrEmpty(text)) continue;
                    var rc = a.rect;
                    var corners = new[] { new Vector2Int(rc.xMax, rc.yMax), new Vector2Int(rc.xMin, rc.yMin), new Vector2Int(rc.xMin, rc.yMax), new Vector2Int(rc.xMax, rc.yMin) };
                    Vector3? at = null;
                    for (int c = k; c < corners.Length && at == null; c++)
                    {
                        var p = map.World(corners[c].x, corners[c].y);
                        if (!map.Walkable(p, 0.3f)) continue;
                        bool ok = true;
                        foreach (var n in map.npcs) if (Battle.FlatDistance(n.pos, p) < 2.5f) ok = false;
                        foreach (var t in stTablets) if (Battle.FlatDistance(t.pos, p) < 2.5f) ok = false;
                        foreach (var s in stStars) if (Battle.FlatDistance(s.pos, p) < 2f) ok = false;
                        if (ok) { at = p; k = c + 1; }
                    }
                    if (at == null) continue;
                    string title = "古老的石碑", body = text;
                    int sep = text.IndexOf('｜');
                    if (sep > 0) { title = text.Substring(0, sep); body = text.Substring(sep + 1); }
                    stTablets.Add(new StageTablet { key = a.node.id + ":" + key, title = title, text = body.Replace("／", "\n"), pos = at.Value });
                }
            }
        }

        GameObject TabletVisual()
        {
            var root = new GameObject("Lore tablet");
            var stone = Prim(PrimitiveType.Cube, new Color(0.45f, 0.45f, 0.5f)); stone.transform.SetParent(root.transform, false);
            stone.transform.localPosition = new Vector3(0f, 0.5f, 0f); stone.transform.localScale = new Vector3(0.65f, 1f, 0.16f);
            var face = Prim(PrimitiveType.Cube, new Color(0.55f, 0.85f, 1f)); face.transform.SetParent(root.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.6f, -0.085f); face.transform.localScale = new Vector3(0.45f, 0.55f, 0.01f);
            var bas = Prim(PrimitiveType.Cube, new Color(0.32f, 0.32f, 0.36f)); bas.transform.SetParent(root.transform, false);
            bas.transform.localPosition = new Vector3(0f, 0.06f, 0f); bas.transform.localScale = new Vector3(0.85f, 0.12f, 0.35f);
            GlowHalo.Create(root.transform, new Vector3(0f, 0.7f, 0f), new Color(0.5f, 0.85f, 1f, 0.45f), 1.2f, 0.2f).name = "Hint";
            return root;
        }

        void UpdateTablets(ExploreRun run)
        {
            var me = Me.Feet;
            foreach (var t in stTablets)
            {
                float d = Battle.FlatDistance(t.pos, me);
                bool show = d <= ChestView;
                if (show && !t.go)
                {
                    t.go = TabletVisual(); t.go.transform.SetParent(chRoot, false); t.go.transform.localPosition = t.pos;
                    var c = run.map.Cell(t.pos);
                    foreach (var dd in ExploreMap.Dirs) if (run.map.Open(c.x + dd.x, c.y + dd.y) && run.map.AreaAt(new Vector2Int(c.x + dd.x, c.y + dd.y)) != null) { t.go.transform.localRotation = Quaternion.LookRotation(-new Vector3(dd.x, 0f, dd.y), Vector3.up); break; }
                }
                if (t.go && t.go.activeSelf != show) t.go.SetActive(show);
                if (t.read || d > TabletRead || exNodeArea != null && ChEnemyNear(6f)) continue;
                t.read = true;
                var hint = t.go ? t.go.transform.Find("Hint") : null; if (hint) hint.gameObject.SetActive(false);
                bool first = Progress.Get?.Invoke("sd_lore_" + stStageId + "_" + t.key) != "1";
                if (first) { Progress.Set?.Invoke("sd_lore_" + stStageId + "_" + t.key, "1"); Economy.Gold += 50; }
                Say($"📜 {t.title}\n{t.text}" + (first ? "\n（第一次讀到：💰 +50）" : ""), 6f);
                ChatLine(new StoryLine { text = $"📜 {t.title}：{t.text.Replace("\n", " ")}" });
                Sfx.Play("click", 0.7f, 0.8f);
            }
        }

        // ---------------------------------------------------------------- 每幀
        void UpdateStageMap(ExploreRun run, float dt)
        {
            if (!SideQuestMode || run != chRun || battle == null || Me == null || !chRoot) return;
            UpdateSideQuests(run);
            UpdateChapterGates(run);
            UpdateStars();
            UpdateTablets(run);
        }

        /// <summary>右上角：這一關找到幾顆星星（剛進關、撿到時顯示一下）</summary>
        void DrawStageMapHud(float W, float H)
        {
            if (!SideQuestMode || dungeon.style != "stage" || phase != Phase.Fighting || Time.time > stHudUntil || stStageId == null) return;
            int fs = UiKit.BaseFont, have = StarCount(stStageId);
            var r = new Rect(W - fs * 9.5f, H * 0.2f, fs * 8.5f, fs * 2.2f);
            UiKit.MenuCard(r, 0.75f, UiKit.Gold);
            string stars = ""; for (int i = 0; i < StageStars; i++) stars += i < have ? "★" : "☆";
            UiKit.Text(r, $"{stars}  {have}/{StageStars}", fs, UiKit.Gold, TextAnchor.MiddleCenter, true);
        }
    }
}
