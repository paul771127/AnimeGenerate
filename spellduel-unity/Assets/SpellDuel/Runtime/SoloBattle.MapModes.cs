using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 更多故事玩法（使用者要求「點子都先做起來，先不用急著放入關卡」；story.txt 的 mode =、或測試副本 dungeons.txt 的 mode =）：
    ///
    /// 跑道（RaceTrack，和賽跑共用泥沼、加速、身體障礙）：
    ///   escape 崩塌逃脫：岩漿／崩塌從後面追上來（越來越快），被追上就失敗，跑到終點過關
    ///   thief  追捕盜賊：盜賊搶了東西在前面跑；用技能打中讓他暈 5 秒，暈著時追到身邊就抓到；他先跑到終點就失敗
    /// 小地圖（DungeonMaze）：
    ///   ghost  鬼抓人：沒有一般的怪，一隻打不死、會穿牆的鬼一直追你；點亮地圖上全部的燈籠就贏（點亮時鬼會逃開 6 秒）；被抓 3 次失敗
    ///   keys   鑰匙尋寶：地圖上藏著 3 把鑰匙（靠近時手機會震，越近越強）；集滿才能進最後的地點，打贏過關
    ///   bombs  限時拆彈：幾顆計時炸彈，跑過去照指示做（蹲、跳、或說「解除」）來拆；爆炸扣血，爆 2 顆以上就失敗
    ///   dark   黑暗探索：看得到的範圍會隨油燈的油變小，撿油瓶補充；暗處的怪只看得到眼睛
    ///   track  追蹤狩獵：地上的足跡指向稀有魔物的巢穴；打傷牠會逃到下一個巢穴，在最後的巢穴打倒牠
    ///   quiet  噤聲潛行：潛入＋麥克風——太大聲，附近的敵人會轉頭過來看（看到你就失敗）
    /// 10m×10m 戰鬥的變化：
    ///   bossrush Boss 連戰（連打三隻 Boss，中間不回血）、glass 玻璃大砲（你和敵人都一擊倒）、gamble 鬥場賭局（開打前選詛咒卡，選越多獎勵越多）
    /// </summary>
    public partial class SoloBattle
    {
        static readonly string[] TrackModes = { "race", "escape", "thief" };
        static readonly string[] MazeModes = { "explore", "escort", "stealth", "quiet", "ghost", "keys", "bombs", "dark", "track" };
        public static bool IsMapMode(string m) => System.Array.IndexOf(TrackModes, m) >= 0 || System.Array.IndexOf(MazeModes, m) >= 0;
        bool StealthLike => dungeon != null && (dungeon.storyMode == "stealth" || dungeon.storyMode == "quiet");
        /// <summary>這些玩法的地點不開戰（門一開始就開著，走進最後的地點也不打）</summary>
        bool NoAreaBattles => dungeon != null && (StealthLike || dungeon.storyMode == "ghost" || dungeon.storyMode == "bombs");

        Transform mmRoot;
        readonly List<GameObject> mmGo = new List<GameObject>();
        float mmSaidAt;

        void ClearMapModes()
        {
            if (mmRoot) Destroy(mmRoot.gameObject);
            mmRoot = null; mmGo.Clear();
            lanterns.Clear(); keys.Clear(); bombs.Clear(); oils.Clear(); ghostGo = null; trackTarget = null; trackDens.Clear(); trackPrints.Clear();
            darkOn = false; escapeFront = -999f;
        }
        Transform MmRoot { get { if (!mmRoot) { mmRoot = new GameObject("Map mode").transform; mmRoot.SetParent(exRoot ? exRoot : null, false); } return mmRoot; } }
        GameObject MmPart(Transform parent, PrimitiveType t, Color c, Vector3 at, Vector3 size, bool glow = false)
        { var g = Prim(t, c, glow); g.transform.SetParent(parent, false); g.transform.localPosition = at; g.transform.localScale = size; return g; }

        /// <summary>地圖上適合放東西的點（區域外的走道、區域中心），彼此至少 minGap 公尺，離起點至少 12m</summary>
        List<Vector3> MmSpots(ExploreMap map, int n, float minGap, int seed)
        {
            var rng = new System.Random(seed); var cand = new List<Vector3>();
            for (int x = 1; x < map.W - 1; x++) for (int z = 1; z < map.H - 1; z++) if (map.At(x, z) == ExCell.Floor) cand.Add(map.World(x, z));
            var startW = map.World(map.start.x, map.start.y);
            var outl = new List<Vector3>();
            for (int tries = 0; tries < 400 && outl.Count < n && cand.Count > 0; tries++)
            {
                var p = cand[rng.Next(cand.Count)];
                if (Battle.FlatDistance(p, startW) < 12f || map.PropAt(p, 1f) != null) continue;
                bool ok = true; foreach (var q in outl) if (Battle.FlatDistance(p, q) < minGap) { ok = false; break; }
                if (ok) outl.Add(p);
            }
            return outl;
        }

        /// <summary>StoryMazeBegun 叫（新玩法）</summary>
        bool BeginMapMode(string mode)
        {
            ClearMapModes();
            var map = exploreRun.map; int seed = (dungeon.story != null ? dungeon.story.id.GetHashCode() : 1) & 0x7fffffff;
            switch (mode)
            {
                case "ghost":
                    foreach (var p in MmSpots(map, 5, 14f, seed)) lanterns.Add(new MmLantern { pos = p, go = LanternModel(p) });
                    ghostPos = map.World(map.start.x, map.start.y) + new Vector3(40f, 0f, 0f); ghostCatches = 0; ghostAt = Time.time + 5f;
                    Say("👻 鬼抓人：點亮地圖上全部的燈籠（站在旁邊 1.5 秒）！鬼會穿牆追你，點燈時牠會逃開；被抓 3 次就失敗", 6f);
                    return true;
                case "keys":
                    foreach (var p in MmSpots(map, 3, 18f, seed)) { var g = MmPart(MmRoot, PrimitiveType.Cube, new Color(1f, 0.85f, 0.2f), p + Vector3.up * 1f, new Vector3(0.15f, 0.5f, 0.1f), true); keys.Add(new MmKey { pos = p, go = g }); }
                    keysHave = 0;
                    Say("🗝 鑰匙尋寶：地圖上藏著 3 把鑰匙，靠近時手機會震動（越近越強）；集滿 3 把才能進最後的地點", 6f);
                    return true;
                case "bombs":
                {
                    var spots = MmSpots(map, 4, 16f, seed); var rng = new System.Random(seed);
                    for (int i = 0; i < spots.Count; i++)
                    {
                        var p = spots[i]; var g = new GameObject("Bomb"); g.transform.SetParent(MmRoot, false); g.transform.localPosition = p;
                        MmPart(g.transform, PrimitiveType.Sphere, new Color(0.15f, 0.15f, 0.18f), Vector3.up * 0.35f, Vector3.one * 0.7f);
                        MmPart(g.transform, PrimitiveType.Sphere, new Color(1f, 0.3f, 0.2f), Vector3.up * 0.8f, Vector3.one * 0.18f, true);
                        bombs.Add(new MmBomb { pos = p, go = g, explodeAt = Time.time + 75f + 30f * i, need = rng.Next(3) });
                    }
                    bombsBoom = 0;
                    Say("💣 限時拆彈：地圖上有 4 顆計時炸彈！跑過去照指示做（蹲、跳、或說「解除」）來拆；爆 2 顆就失敗", 6f);
                    return true;
                }
                case "dark":
                    darkOn = true; darkOil = 1f;
                    foreach (var p in MmSpots(map, 6, 12f, seed)) { var g = MmPart(MmRoot, PrimitiveType.Cylinder, new Color(1f, 0.75f, 0.3f), p + Vector3.up * 0.25f, new Vector3(0.22f, 0.25f, 0.22f), true); oils.Add(new MmKey { pos = p, go = g }); }
                    Say("🕯 黑暗探索：油燈的油會慢慢燒完，看得到的範圍也會變小；撿起地上的油瓶補充。暗處的怪只看得到眼睛", 6f);
                    return true;
                case "track":
                {
                    // 巢穴：離起點最遠的三個區域（照距離排）
                    var startW = map.World(map.start.x, map.start.y);
                    var areas = new List<ExArea>(map.areas); areas.Sort((a, b) => Battle.FlatDistance(a.center, startW).CompareTo(Battle.FlatDistance(b.center, startW)));
                    for (int i = Mathf.Max(0, areas.Count - 3); i < areas.Count; i++) trackDens.Add(areas[i].center);
                    trackDen = 0; trackFleeing = false;
                    trackTarget = battle.SpawnTrackTarget(trackDens[0]);
                    Say($"🦶 追蹤狩獵：跟著地上的足跡找到「{trackTarget.name}」！打傷牠會逃到下一個巢穴，在最後的巢穴打倒牠", 6f);
                    return true;
                }
                case "quiet":
                    Say("🤫 噤聲潛行：除了別被看到，也別發出聲音！麥克風聽得到現實的聲音，太大聲附近的敵人會轉頭過來看", 6f);
                    return false;   // 其他照潛入
            }
            return false;
        }

        // ---------------------------------------------------------------- 每幀
        void UpdateMapMode(ExploreRun run, float dt)
        {
            if (dungeon == null || battle == null || Me == null || battle.Over) return;
            switch (dungeon.storyMode)
            {
                case "ghost": UpdateGhost(run, dt); break;
                case "keys": UpdateKeys(); break;
                case "bombs": UpdateBombs(); break;
                case "dark": UpdateDarkMode(dt); break;
                case "track": UpdateTrack(run, dt); break;
                case "quiet": UpdateQuiet(); break;
            }
        }

        // ---- 鬼抓人
        class MmLantern { public Vector3 pos; public GameObject go; public bool lit; public float hold; }
        readonly List<MmLantern> lanterns = new List<MmLantern>();
        GameObject ghostGo; SpriteEnemyView ghostView; Vector3 ghostPos; float ghostAt, ghostFleeUntil; int ghostCatches;
        GameObject LanternModel(Vector3 p)
        {
            var g = new GameObject("Lantern"); g.transform.SetParent(MmRoot, false); g.transform.localPosition = p;
            MmPart(g.transform, PrimitiveType.Cylinder, new Color(0.3f, 0.25f, 0.2f), Vector3.up * 0.7f, new Vector3(0.08f, 0.7f, 0.08f));
            MmPart(g.transform, PrimitiveType.Sphere, new Color(0.25f, 0.22f, 0.2f), Vector3.up * 1.5f, Vector3.one * 0.35f, true);
            return g;
        }
        void UpdateGhost(ExploreRun run, float dt)
        {
            float now = Time.time; var me = Me.Feet;
            int lit = 0;
            foreach (var l in lanterns)
            {
                if (!l.lit && Battle.FlatDistance(l.pos, me) < 1.3f)
                {
                    l.hold += dt;
                    if (l.hold >= 1.5f) { l.lit = true; ghostFleeUntil = now + 6f; Say("🏮 燈籠點亮了！鬼逃開了", 2f); Haptics.Play(Haptics.Kind.Success); l.go.transform.GetChild(1).GetComponent<Renderer>().material.color = new Color(1f, 0.6f, 0.25f); }
                }
                else if (!l.lit) l.hold = 0f;
                if (l.lit) lit++;
            }
            if (lit >= lanterns.Count && lanterns.Count > 0) { Say("🏮 全部的燈籠都亮了，鬼消散了！", 4f); battle.stageCleared = true; return; }
            if (!ghostGo)
            {
                ghostGo = new GameObject("Chasing ghost"); ghostGo.transform.SetParent(MmRoot, false);
                var def = SpriteMonsters.Get(204); ghostView = SpriteEnemyView.Create(def, ghostGo.transform);
            }
            if (now >= ghostAt)
            {
                var to = Fighter.Flat(me - ghostPos); float d = Battle.FlatDistance(me, ghostPos);
                bool flee = now < ghostFleeUntil;
                float sp = flee ? -3f : 2.1f + 0.15f * lit;
                ghostPos += to * sp * dt;
                if (!flee && d < 0.8f)
                {
                    ghostCatches++;
                    battle.ApplyRemoteHit(GhostSkill, Mathf.Max(1, Mathf.RoundToInt(Me.maxHp * 0.2f)), Me.Chest, true);
                    Haptics.Play(Haptics.Kind.Heavy); Kick(16f, 0f);
                    if (ghostCatches >= 3) { battle.FailStage("被鬼抓到 3 次了…"); return; }
                    Say($"👻 被鬼抓到了！（{ghostCatches}/3）牠會先退開一下", 2.5f);
                    ghostPos = me - to * 14f; ghostAt = now + 3f;
                }
                if (ghostView) ghostView.MoveSpeed = Mathf.Abs(sp);
                if (to != Vector3.zero) ghostGo.transform.localRotation = Quaternion.LookRotation(flee ? -to : to, Vector3.up);
                if (now > mmSaidAt + 1.2f) { mmSaidAt = now; Say($"👻 燈籠 {lit}/{lanterns.Count}・鬼在 {d:0}m 外" + (flee ? "（逃開中）" : ""), 1.3f); }
            }
            ghostGo.transform.localPosition = ghostPos + Vector3.up * 0.2f;
        }
        static SkillDef ghostSkill; static SkillDef GhostSkill => ghostSkill ??= new SkillDef { id = "mm_ghost", name = "鬼的冷手", color = new Color(0.7f, 0.5f, 1f) };

        // ---- 鑰匙尋寶
        class MmKey { public Vector3 pos; public GameObject go; public bool taken; }
        readonly List<MmKey> keys = new List<MmKey>();
        int keysHave; float keyPulseAt, keyBlockSaidAt;
        void UpdateKeys()
        {
            float now = Time.time; var me = Me.Feet; float nearest = 999f;
            foreach (var k in keys)
            {
                if (k.taken) continue;
                if (k.go) k.go.transform.localRotation = Quaternion.Euler(0f, now * 90f, 0f);
                float d = Battle.FlatDistance(k.pos, me); nearest = Mathf.Min(nearest, d);
                if (d < 1.2f) { k.taken = true; if (k.go) k.go.SetActive(false); keysHave++; Say($"🗝 找到鑰匙了！（{keysHave}/3）", 2.5f); Haptics.Play(Haptics.Kind.Success); }
            }
            if (nearest < 8f && now > keyPulseAt) { keyPulseAt = now + Mathf.Lerp(0.35f, 1.6f, nearest / 8f); Haptics.Play(nearest < 3f ? Haptics.Kind.Medium : Haptics.Kind.Light); }
        }
        /// <summary>鑰匙不夠：最後的地點不能進（StartAreaNode 叫）</summary>
        bool KeysBlockFinal(ExArea a)
        {
            if (keysHave >= 3) return false;
            a.entered = false;
            if (Time.time > keyBlockSaidAt + 3f) { keyBlockSaidAt = Time.time; Say($"🔒 門鎖著：還差 {3 - keysHave} 把鑰匙", 2.5f); }
            return true;
        }

        // ---- 限時拆彈
        class MmBomb { public Vector3 pos; public GameObject go; public float explodeAt; public int need; public bool done, boom; }
        readonly List<MmBomb> bombs = new List<MmBomb>();
        int bombsBoom; bool bombHeardWord;
        static readonly string[] BombNeed = { "蹲下", "跳起來", "說「解除」" };
        void UpdateBombs()
        {
            float now = Time.time; var me = Me.Feet; int left = 0; string near = null; float nextBoom = 999f;
            foreach (var b in bombs)
            {
                if (b.done || b.boom) continue;
                left++;
                float t = b.explodeAt - now; nextBoom = Mathf.Min(nextBoom, t);
                if (b.go) b.go.transform.GetChild(1).GetComponent<Renderer>().material.color = Mathf.Sin(now * (t < 15f ? 14f : 5f)) > 0f ? new Color(1f, 0.25f, 0.2f) : new Color(0.3f, 0.05f, 0.05f);
                if (t <= 0f)
                {
                    b.boom = true; bombsBoom++; if (b.go) b.go.SetActive(false);
                    BossRig.SlamShockwave(WorldFrame.FromWorld(b.pos), 3f, new Color(1f, 0.5f, 0.1f));
                    if (Battle.FlatDistance(b.pos, me) < 6f) battle.ApplyRemoteHit(BombSkill, Mathf.Max(1, Mathf.RoundToInt(Me.maxHp * 0.25f)), Me.Chest, true);
                    Say($"💥 一顆炸彈爆炸了！（{bombsBoom}）", 2.5f); Haptics.Play(Haptics.Kind.Heavy); Kick(18f, 0f);
                    if (bombsBoom >= 2) { battle.FailStage("炸彈爆了 2 顆…"); return; }
                    continue;
                }
                if (Battle.FlatDistance(b.pos, me) < 1.3f)
                {
                    near = BombNeed[b.need];
                    bool ok = b.need == 0 ? Me.crouching : b.need == 1 ? Me.airborneUntil > battle.now : bombHeardWord;
                    if (ok) { b.done = true; if (b.go) b.go.SetActive(false); Say("✅ 拆除成功！", 2f); Haptics.Play(Haptics.Kind.Success); left--; }
                }
            }
            bombHeardWord = false;
            if (left == 0) { Say(bombsBoom == 0 ? "💣 全部拆除！" : "💣 剩下的炸彈都拆完了", 3f); battle.stageCleared = true; return; }
            if (now > mmSaidAt + 1f) { mmSaidAt = now; Say(near != null ? $"💣 拆彈：{near}！" : $"💣 還有 {left} 顆・最快 {nextBoom:0} 秒後爆炸", 1.1f); }
        }
        static SkillDef bombSkill; static SkillDef BombSkill => bombSkill ??= new SkillDef { id = "mm_bomb", name = "炸彈", color = new Color(1f, 0.5f, 0.1f) };
        /// <summary>語音：拆彈的密語（OnSpeechText 叫；回傳 true＝用掉了）</summary>
        bool MapModeSpeech(string text)
        {
            if (dungeon == null || dungeon.storyMode != "bombs" || string.IsNullOrEmpty(text)) return false;
            if (SpeechMatcher.Normalize(text).Contains("解除")) { bombHeardWord = true; return true; }
            return false;
        }

        // ---- 黑暗探索
        readonly List<MmKey> oils = new List<MmKey>();
        bool darkOn; float darkOil = 1f;
        /// <summary>黑暗探索：怪看得到的距離（油越少越近）</summary>
        float MapDarkSight => Mathf.Lerp(1.6f, 6f, darkOil);
        void UpdateDarkMode(float dt)
        {
            darkOil = Mathf.Max(0f, darkOil - dt / 75f);
            foreach (var o in oils)
                if (!o.taken && Battle.FlatDistance(o.pos, Me.Feet) < 1.1f) { o.taken = true; if (o.go) o.go.SetActive(false); darkOil = Mathf.Min(1f, darkOil + 0.5f); Say("🛢 補充了油燈！", 1.5f); Haptics.Play(Haptics.Kind.Light); }
            if (Time.time > mmSaidAt + 2f) { mmSaidAt = Time.time; if (darkOil < 0.25f) Say("🕯 油快用完了！找油瓶", 1.5f); }
        }
        static Texture2D vignetteTex;
        /// <summary>OnGUI：黑暗探索的視野（四周黑、中間透明，油越少洞越小）</summary>
        void DrawDarkVignette(float W, float H)
        {
            if (!darkOn || battle == null || battle.Over) return;
            if (!vignetteTex)
            {
                const int N = 128; vignetteTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float d = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
                    vignetteTex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d))));
                }
                vignetteTex.Apply();
            }
            float s = Mathf.Lerp(0.8f, 2.2f, darkOil) * Mathf.Max(W, H);
            var r = new Rect((W - s) / 2f, (H - s) / 2f, s, s);
            GUI.color = Color.white; GUI.DrawTexture(r, vignetteTex);
            GUI.color = Color.black;   // 洞外面全黑
            GUI.DrawTexture(new Rect(0, 0, W, Mathf.Max(0f, r.y)), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(0, r.yMax, W, Mathf.Max(0f, H - r.yMax)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, 0, Mathf.Max(0f, r.x), H), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.xMax, 0, Mathf.Max(0f, W - r.xMax), H), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ---- 追蹤狩獵
        readonly List<Vector3> trackDens = new List<Vector3>();
        readonly List<GameObject> trackPrints = new List<GameObject>();
        Fighter trackTarget; int trackDen; bool trackFleeing; float trackPrintAt; List<Vector3> trackPath;
        void UpdateTrack(ExploreRun run, float dt)
        {
            if (trackTarget == null) return;
            if (!trackTarget.Alive) { Say($"🦶 打倒了{trackTarget.name}！", 3f); battle.stageCleared = true; return; }
            float now = Time.time;
            // 打傷 → 逃到下一個巢穴（最後一個巢穴不逃）
            float frac = trackTarget.hp / Mathf.Max(1f, trackTarget.maxHp);
            if (!trackFleeing && trackDen < trackDens.Count - 1 && frac < 1f - 0.3f * (trackDen + 1))
            {
                trackDen++; trackFleeing = true; trackPath = MmPath(run.map, trackTarget.Feet, trackDens[trackDen]);
                Say($"🦶 {trackTarget.name}逃走了！跟著足跡追到下一個巢穴", 2.5f);
            }
            if (trackFleeing)
            {
                battle.HoldMinion(trackTarget, 0.3f);
                if (trackPath == null || trackPath.Count == 0) trackFleeing = false;
                else
                {
                    var target = trackPath[0]; var to = Fighter.Flat(target - trackTarget.Feet); float d = Battle.FlatDistance(target, trackTarget.Feet);
                    if (d < 0.5f) trackPath.RemoveAt(0);
                    else { var step = to * Mathf.Min(d, 4.2f * dt); trackTarget.head = new Vector3(trackTarget.head.x + step.x, trackTarget.head.y, trackTarget.head.z + step.z); trackTarget.forward = to; }
                }
            }
            // 足跡：從玩家往獵物（每 2 秒重算），畫最近的 10 格
            if (now > trackPrintAt)
            {
                trackPrintAt = now + 2f;
                foreach (var g in trackPrints) if (g) Destroy(g);
                trackPrints.Clear();
                var path = MmPath(run.map, Me.Feet, trackTarget.Feet);
                if (path != null)
                    for (int i = 0; i < path.Count && i < 10; i++)
                    {
                        var dir = i + 1 < path.Count ? Fighter.Flat(path[i + 1] - path[i]) : Fighter.Flat(trackTarget.Feet - path[i]);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            var right = new Vector3(dir.z, 0f, -dir.x);
                            var g = MmPart(MmRoot, PrimitiveType.Cylinder, new Color(0.35f, 0.22f, 0.12f, 0.8f), path[i] + right * 0.18f * s + dir * 0.4f * s + Vector3.up * 0.02f, new Vector3(0.16f, 0.005f, 0.28f));
                            if (dir != Vector3.zero) g.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                            trackPrints.Add(g);
                        }
                    }
            }
        }

        /// <summary>地圖格子上的最短路（BFS，四方向）：回傳每一格中心的位置（不含起點那格）</summary>
        static List<Vector3> MmPath(ExploreMap map, Vector3 from, Vector3 to)
        {
            var a = map.Cell(from); var b = map.Cell(to);
            if (!map.Open(a.x, a.y) || !map.Open(b.x, b.y)) return null;
            var prev = new Dictionary<Vector2Int, Vector2Int>(); var q = new Queue<Vector2Int>(); q.Enqueue(a); prev[a] = a;
            while (q.Count > 0)
            {
                var c = q.Dequeue(); if (c == b) break;
                foreach (var d in ExploreMap.Dirs) { var n = c + d; if (prev.ContainsKey(n) || !map.Open(n.x, n.y)) continue; prev[n] = c; q.Enqueue(n); }
            }
            if (!prev.ContainsKey(b)) return null;
            var path = new List<Vector3>();
            for (var c = b; c != a; c = prev[c]) path.Add(map.World(c.x, c.y));
            path.Reverse();
            return path;
        }

        // ---- 噤聲潛行
        float quietLevel;
        void UpdateQuiet()
        {
            float lv = UseSpeech ? speech.Level01 : 0f;
            quietLevel = Mathf.Max(lv, quietLevel - Time.deltaTime * 0.8f);
            if (lv > 0.45f)
            {
                int n = battle.ExHear(Me.Feet, 4f + 8f * Mathf.InverseLerp(0.45f, 1f, lv));
                if (n > 0 && Time.time > mmSaidAt + 2f) { mmSaidAt = Time.time; Say($"🤫 太大聲了！附近 {n} 隻敵人轉頭過來", 1.6f); Haptics.Play(Haptics.Kind.Warning); }
            }
        }
        /// <summary>OnGUI：噤聲潛行的音量計</summary>
        void DrawQuietMeter(float W, float H)
        {
            if (dungeon == null || dungeon.storyMode != "quiet" || battle == null || battle.Over) return;
            float w = W * 0.4f, h = Mathf.Max(10f, H * 0.012f), x = (W - w) / 2f, y = H * 0.16f;
            GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = quietLevel > 0.45f ? new Color(1f, 0.3f, 0.2f) : new Color(0.4f, 1f, 0.5f); GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(quietLevel), h), Texture2D.whiteTexture);
            GUI.color = Color.white; GUI.DrawTexture(new Rect(x + w * 0.45f, y - 2f, 2f, h + 4f), Texture2D.whiteTexture);
        }

        // ---------------------------------------------------------------- 10m×10m 戰鬥的變化
        /// <summary>鬥場賭局：開打前選詛咒卡（畫面在 DrawGamble；選的時候戰鬥暫停）</summary>
        public static readonly (string name, string desc)[] GambleCards =
        {
            ("疾風", "敵人移動 +30%"), ("鐵皮", "敵人血量 +50%"), ("利爪", "敵人攻擊 +40%"), ("重腳", "不能衝刺"), ("易傷", "受到的傷害 +30%"),
        };
        bool gambleOpen; readonly bool[] gamblePick = new bool[5]; float gambleBonus;
        bool GambleNoDash => gamblePick[3] && battle != null && !battle.Over && storyStage != null && storyStage.mode == "gamble";

        /// <summary>StartBossBattle（故事關卡那一段，StartStage 之後）叫：套用戰鬥變化玩法</summary>
        void ApplyCombatMode(StoryStage st)
        {
            gambleBonus = 0f;
            switch (st.mode)
            {
                case "glass":
                    battle.enemyHpMul *= 0.001f; battle.storyBossHp *= 0.001f;   // 敵人一擊倒（血量最少 1）
                    Me.damageTakenMul *= 100f;   // 你也一擊倒
                    Say("💎 玻璃大砲：你和敵人都是一擊就倒！看準紅線閃避", 5f);
                    break;
                case "gamble":
                    gambleOpen = true; for (int i = 0; i < gamblePick.Length; i++) gamblePick[i] = false;
                    break;
                case "bossrush":
                    Say("⏱ Boss 連戰：連打三隻 Boss，中間不會回血！", 5f);
                    break;
            }
        }
        /// <summary>Boss 連戰：要打的三個 Boss 波次（這一章與前兩章的 Boss）</summary>
        static int[] BossRushWaves(StoryStage st)
        {
            int last = Mathf.Clamp(st.theme + 1, 3, 5) * 10;
            return new[] { last - 20, last - 10, last };
        }
        void ApplyGamble()
        {
            int n = 0;
            for (int i = 0; i < gamblePick.Length; i++) if (gamblePick[i]) n++;
            if (gamblePick[0]) battle.minionSpeed *= 1.3f;
            if (gamblePick[1]) { battle.enemyHpMul *= 1.5f; battle.storyBossHp *= 1.5f; }
            if (gamblePick[2]) { battle.enemyDmgMul *= 1.4f; battle.storyBossDmg *= 1.4f; }
            if (gamblePick[4]) Me.damageTakenMul *= 1.3f;
            gambleBonus = 0.3f * n;
            gambleOpen = false;
            Say(n == 0 ? "🎰 不賭，照常打" : $"🎰 選了 {n} 張詛咒卡：通關金幣 +{Mathf.RoundToInt(gambleBonus * 100)}%", 4f);
        }
        /// <summary>OnGUI：選詛咒卡（戰鬥暫停中）</summary>
        void DrawGamble(float W, float H)
        {
            if (!gambleOpen) return;
            int fs = UiKit.BaseFont; float pad = W * 0.06f, bw = W - pad * 2f, bh = fs * 2.4f;
            GUI.color = new Color(0f, 0f, 0f, 0.75f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            float y = H * 0.18f;
            UiKit.Text(new Rect(pad, y, bw, fs * 2f), "🎰 鬥場賭局：選詛咒卡（每張通關金幣 +30%）", fs, Color.white, TextAnchor.MiddleCenter, true); y += fs * 2.6f;
            for (int i = 0; i < GambleCards.Length; i++)
            {
                if (UiKit.Button(new Rect(pad, y, bw, bh), (gamblePick[i] ? "✅ " : "⬜ ") + $"{GambleCards[i].name}：{GambleCards[i].desc}", fs)) gamblePick[i] = !gamblePick[i];
                y += bh + fs * 0.4f;
            }
            y += fs;
            if (UiKit.Button(new Rect(pad, y, bw, bh * 1.1f), "⚔ 開打", fs)) ApplyGamble();
        }
    }
}
