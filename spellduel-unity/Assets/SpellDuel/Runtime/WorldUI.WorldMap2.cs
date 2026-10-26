using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 新的大地圖（Codex 的 WorldMap 素材；WORLD_MAP_BRIEF.md）：由左到右的冒險路線圖，左右拖曳。
    ///   ・城鎮、每一關、無盡塔都是地圖上的點；還沒開的被雲霧蓋住，過一關右邊才出現下一關。
    ///   ・Q 版主角站在目前的位置；點哪裡就沿著路走過去（最多 1.5 秒），到了：
    ///     關卡＝下方出現說明與「⚔ 出發」（按了畫面往那一點放大，進整備）；城鎮＝放大進那個城鎮的 2D 城鎮圖。
    ///   ・關卡點下方的星星：簡單／普通／困難各通關一次一顆。
    ///   ・新開一關的演出：雲霧往右退、路一段一段亮起來、新的旗子彈出來。
    ///   ・通往 Boss 的那段路是暗紅色。
    /// 素材：Resources/WorldMap/region_{1..5}.jpg（每張 2160×1920，接成 10800×1920）、nodes.json、ui/、hero_{職業}_side/idle。
    /// 還沒交付的區域：如果玩家的進度已經超過有素材的區域，自動改用原本的大地圖（也可以手動切換）。
    /// </summary>
    public partial class WorldUI
    {
        class WMNode { public string id, type, name; public Vector2 pos, label; public int region; public bool reserved; }
        class WMPath { public string from, to; public List<Vector2> pts = new List<Vector2>(); }

        const float WMH = 1920f, WMRegionW = 2160f;
        static Dictionary<string, WMNode> wmNodes;
        static List<WMPath> wmPaths;
        static Texture2D[] wmRegions;
        static readonly Dictionary<string, Texture2D> wmTex = new Dictionary<string, Texture2D>();
        static int wmMaxRegion;
        static bool wmLoaded;

        /// <summary>玩家手動切到原本的大地圖</summary>
        public bool useOldMap;
        float wmScrollX = -1f;
        string heroAt;                       // 主角站在哪個點
        List<Vector2> heroPts; float heroT, heroDur; string heroDest; Vector2 heroPos; bool heroLeft;
        float zoomAt = -1f; Vector2 zoomCenter; Action zoomDone;
        HashSet<string> wmSeen; string revealNode; float revealAt; float fogFromX, fogToX;
        const float WalkMax = 1.5f, ZoomTime = 0.6f, RevealTime = 1.6f;

        static Texture2D WMTex(string path)
        {
            if (!wmTex.TryGetValue(path, out var t)) { t = Resources.Load<Texture2D>("WorldMap/" + path); wmTex[path] = t; }
            return t;
        }

        static void LoadWorldMap()
        {
            if (wmLoaded) return;
            wmLoaded = true;
            var ta = Resources.Load<TextAsset>("WorldMap/nodes");
            if (ta == null) return;
            string js = ta.text;
            float Num(string body, string k) { var m = Regex.Match(body, "\"" + k + "\"\\s*:\\s*(-?[0-9.]+)"); return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f; }
            string Str(string body, string k) { var m = Regex.Match(body, "\"" + k + "\"\\s*:\\s*\"([^\"]*)\""); return m.Success ? m.Groups[1].Value : null; }
            bool Bool(string body, string k) { var m = Regex.Match(body, "\"" + k + "\"\\s*:\\s*(true|false)"); return m.Success && m.Groups[1].Value == "true"; }
            int ni = js.IndexOf("\"nodes\""), pi = js.IndexOf("\"paths\"");
            if (ni < 0 || pi < 0) return;
            wmNodes = new Dictionary<string, WMNode>(); wmPaths = new List<WMPath>();
            string nodesPart = pi > ni ? js.Substring(ni, pi - ni) : js.Substring(ni);
            foreach (Match m in Regex.Matches(nodesPart, "\"([^\"]+)\"\\s*:\\s*\\{([^{}]*)\\}"))
            {
                string b = m.Groups[2].Value;
                var n = new WMNode { id = m.Groups[1].Value, type = Str(b, "type") ?? "stage", name = Str(b, "name") ?? "", pos = new Vector2(Num(b, "x"), Num(b, "y")),
                    label = new Vector2(Num(b, "label_x"), Num(b, "label_y")), region = Mathf.Max(1, (int)Num(b, "region")), reserved = Bool(b, "reserved") };
                wmNodes[n.id] = n;
            }
            int pe = js.IndexOf(']', pi); int depth = 0; pe = pi;
            for (int i = js.IndexOf('[', pi); i < js.Length; i++) { if (js[i] == '[') depth++; else if (js[i] == ']') { depth--; if (depth == 0) { pe = i; break; } } }
            string pathsPart = js.Substring(pi, Mathf.Max(0, pe - pi));
            foreach (Match m in Regex.Matches(pathsPart, "\\{([^{}]*)\\}"))
            {
                string b = m.Groups[1].Value;
                if (!Bool(b, "enabled")) continue;   // 預留的第 5 關路線：之後擴關才用
                var p = new WMPath { from = Str(b, "from"), to = Str(b, "to") };
                foreach (Match q in Regex.Matches(b, "\\[\\s*(-?[0-9.]+)\\s*,\\s*(-?[0-9.]+)\\s*\\]"))
                    p.pts.Add(new Vector2(float.Parse(q.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), float.Parse(q.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
                if (p.from != null && p.to != null && p.pts.Count >= 2) wmPaths.Add(p);
            }
            // 區域圖：從 1 開始連續有幾張
            var regs = new List<Texture2D>();
            for (int r = 1; r <= 5; r++) { var t = Resources.Load<Texture2D>("WorldMap/region_" + r); if (t == null) break; regs.Add(t); }
            wmRegions = regs.ToArray(); wmMaxRegion = regs.Count;
        }

        static string TownNode(int t) => "town:" + t;
        bool WMNodeOpen(WMNode n)
        {
            if (n.reserved || n.region > wmMaxRegion) return false;
            if (n.id.StartsWith("town:")) { int.TryParse(n.id.Substring(5), out var t); return TownOpen(t); }
            if (n.type == "tower" || n.id == "tower") return Story.IsCleared("2-5");   // 無盡塔：2-5 下方的岔路
            var st = Story.Find(n.id);
            return st != null && (Story.IsUnlocked(st) || Story.IsCleared(st.id));
        }

        /// <summary>新地圖能不能用：有素材，而且玩家走到的最遠一關在有素材的區域裡</summary>
        public bool WorldMap2Usable
        {
            get
            {
                LoadWorldMap();
                if (wmNodes == null || wmMaxRegion == 0) return false;
                foreach (var st in Story.AllStages())
                    if (Story.IsUnlocked(st) || Story.IsCleared(st.id))
                        if (!wmNodes.TryGetValue(st.id, out var n) || n.region > wmMaxRegion) return false;
                return true;
            }
        }

        void LoadSeen()
        {
            if (wmSeen != null) return;
            wmSeen = new HashSet<string>();
            string s = ""; try { s = PlayerPrefs.GetString(CharacterSlots.Key("sd_map_seen"), ""); } catch { }
            bool fresh = string.IsNullOrEmpty(s);
            foreach (var x in s.Split(',')) if (x.Length > 0) wmSeen.Add(x);
            if (fresh) { foreach (var n in wmNodes.Values) if (WMNodeOpen(n)) wmSeen.Add(n.id); SaveSeen(); }   // 第一次看到新地圖：現有的都當作看過（不播演出）
            try { heroAt = PlayerPrefs.GetString(CharacterSlots.Key("sd_map_at"), TownNode(town)); } catch { heroAt = TownNode(town); }
            if (!wmNodes.ContainsKey(heroAt)) heroAt = TownNode(0);
            heroPos = wmNodes.TryGetValue(heroAt, out var hn) ? hn.pos : Vector2.zero;
        }
        void SaveSeen() { try { PlayerPrefs.SetString(CharacterSlots.Key("sd_map_seen"), string.Join(",", wmSeen)); PlayerPrefs.Save(); } catch { } }
        /// <summary>換角色：重新讀主角位置、看過的點</summary>
        public void ResetWorldMapState() { wmSeen = null; heroPts = null; zoomAt = -1f; revealNode = null; wmScrollX = -1f; }

        /// <summary>從 a 走到 b 的路（沿著路線，只走開放的點）；找不到回傳 null</summary>
        List<Vector2> WMRoute(string a, string b)
        {
            if (a == b || !wmNodes.ContainsKey(a) || !wmNodes.ContainsKey(b)) return null;
            var prev = new Dictionary<string, (string node, WMPath path, bool fwd)>();
            var q = new Queue<string>(); q.Enqueue(a); prev[a] = (null, null, true);
            while (q.Count > 0)
            {
                var c = q.Dequeue(); if (c == b) break;
                foreach (var p in wmPaths)
                {
                    string nx = p.from == c ? p.to : p.to == c ? p.from : null;
                    if (nx == null || prev.ContainsKey(nx) || !wmNodes.TryGetValue(nx, out var nn) || !WMNodeOpen(nn)) continue;
                    prev[nx] = (c, p, p.from == c); q.Enqueue(nx);
                }
            }
            if (!prev.ContainsKey(b)) return null;
            var segs = new List<(WMPath p, bool fwd)>();
            for (var c = b; prev[c].node != null; c = prev[c].node) segs.Add((prev[c].path, prev[c].fwd));
            segs.Reverse();
            var pts = new List<Vector2> { wmNodes[a].pos };
            foreach (var (p, fwd) in segs)
            {
                var l = new List<Vector2>(p.pts); if (!fwd) l.Reverse();
                pts.AddRange(l);
            }
            pts.Add(wmNodes[b].pos);
            return pts;
        }
        static float PathLen(List<Vector2> pts) { float l = 0f; for (int i = 1; i < pts.Count; i++) l += Vector2.Distance(pts[i - 1], pts[i]); return l; }
        static Vector2 PathAt(List<Vector2> pts, float d)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float sl = Vector2.Distance(pts[i - 1], pts[i]);
                if (d <= sl) return Vector2.Lerp(pts[i - 1], pts[i], sl > 1e-3f ? d / sl : 1f);
                d -= sl;
            }
            return pts[pts.Count - 1];
        }

        /// <summary>點了一個點：主角走過去（最多 1.5 秒），到了再做事</summary>
        void WMGoTo(string id)
        {
            if (heroPts != null || zoomAt > 0f) return;
            Sfx.Play("select", 0.6f);
            var route = WMRoute(heroAt, id);
            if (route == null) { heroAt = id; heroPos = wmNodes[id].pos; WMArrive(id); return; }
            heroPts = route; heroT = 0f; heroDest = id;
            heroDur = Mathf.Clamp(PathLen(route) / 700f, 0.25f, WalkMax);   // 太遠就加速：最多 1.5 秒
        }

        void WMArrive(string id)
        {
            heroAt = id; heroPts = null;
            try { PlayerPrefs.SetString(CharacterSlots.Key("sd_map_at"), id); PlayerPrefs.Save(); } catch { }
            if (id.StartsWith("town:"))
            {
                int.TryParse(id.Substring(5), out var t);
                WMZoom(wmNodes[id].pos, () => OpenTown(t));   // 放大進城鎮
                return;
            }
            if (id == "tower") { WMZoom(wmNodes[id].pos, () => { screen = Screen.Tower; sel = null; }); return; }
            sel = Story.Find(id);
        }

        void WMZoom(Vector2 at, Action done) { zoomAt = Time.unscaledTime; zoomCenter = at; zoomDone = done; Sfx.Play("charged", 0.5f, 1.4f); }

        /// <summary>畫新的大地圖；回傳 false＝不能用（改畫原本的）</summary>
        bool DrawWorldMap2(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            useOldMap = false;   // 「📋 關卡清單」拿掉了（使用者要求）：只用路線圖；路線圖不能用時才退回舊的清單
            if (!WorldMap2Usable) return false;
            if(worldBossNotice>=0){DrawWorldBossPoster(fs,fsS);return true;}
            if (wbPick && DrawWorldBossPick(r, prof, fs, fsS, fsL, gap)) return true;
            LoadSeen();
            float now = Time.unscaledTime;
            float y = r.y + gap;
            Header(r, "🗺 大地圖", new Color(1f, 0.85f, 0.45f), prof, fs, fsS, fsL, gap, ref y);
            // 難度（「📋 關卡清單」按鈕已拿掉）
            DifficultyRow(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.8f), fsS, true);
            y += fs * 2.1f;
            float btnH = fs * 2.1f, detH = sel != null ? fs * 6.4f : 0f;
            float bottom = r.yMax - gap - btnH - (detH > 0f ? detH + gap * 0.5f : 0f) - gap * 0.4f;
            var view = new Rect(r.x, y, r.width, Mathf.Max(fs * 6f, bottom - y));
            float s = view.height / WMH, cw = wmMaxRegion * WMRegionW * s;
            var content = new Rect(0f, 0f, cw, view.height);

            // 主角走路
            if (heroPts != null && Event.current.type == EventType.Repaint)
            {
                heroT += Time.unscaledDeltaTime;
                float L = PathLen(heroPts), k = Mathf.Clamp01(heroT / heroDur);
                var np = PathAt(heroPts, L * Mathf.SmoothStep(0f, 1f, k));   // 起步、到點都放慢一點
                if (Mathf.Abs(np.x - heroPos.x) > 0.5f) heroLeft = np.x < heroPos.x;
                heroPos = np;
                if (k >= 1f) WMArrive(heroDest);
            }
            if (heroPts == null && wmNodes.TryGetValue(heroAt, out var hn0) && zoomAt < 0f) heroPos = hn0.pos;

            // 新開的點：一個一個播演出
            var open = new List<WMNode>();
            foreach (var n in wmNodes.Values) if (WMNodeOpen(n)) open.Add(n);
            open.Sort((a, b) => a.pos.x.CompareTo(b.pos.x));
            float seenFront = 0f; foreach (var n in open) if (wmSeen.Contains(n.id)) seenFront = Mathf.Max(seenFront, n.pos.x);
            if (revealNode == null)
                foreach (var n in open) if (!wmSeen.Contains(n.id)) { revealNode = n.id; revealAt = now; fogFromX = seenFront; fogToX = n.pos.x; Sfx.Play("charged", 0.7f, 1.1f); break; }
            float rk = revealNode != null ? Mathf.Clamp01((now - revealAt) / RevealTime) : 1f;
            if (revealNode != null && rk >= 1f) { wmSeen.Add(revealNode); SaveSeen(); revealNode = null; Sfx.Play("coin", 0.6f, 1.3f); }
            float front = revealNode != null ? Mathf.Lerp(fogFromX, fogToX, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rk * 1.4f))) : seenFront;

            // 捲動：第一次置中在主角；新手指引要點的點、播演出的點在畫面外 → 自動捲過去
            float maxX = Mathf.Max(0f, cw - view.width);
            if (wmScrollX < 0f) wmScrollX = Mathf.Clamp(heroPos.x * s - view.width / 2f, 0f, maxX);
            float? want = null;
            if (revealNode != null) want = wmNodes[revealNode].pos.x * s;
            else if (heroPts != null) want = heroPos.x * s;
            else if (UiKit.LockKey != null && !UiLock.RecentPan)
                foreach (var n in open) if (UiKit.LockMatch(UiKit.LockKey, "", n.id.StartsWith("town:") ? n.id : "stage:" + n.id)) { want = n.pos.x * s; break; }
            if (want is float wx && (wx < wmScrollX + view.width * 0.2f || wx > wmScrollX + view.width * 0.8f))
                wmScrollX = Mathf.Lerp(wmScrollX, Mathf.Clamp(wx - view.width / 2f, 0f, maxX), 0.15f);
            var sc = new Vector2(wmScrollX, 0f);
            if (heroPts == null && zoomAt < 0f) sc = UiKit.DragScroll(view, sc, content);   // 新手指引中也能拖曳（UiLock 轉過來）
            wmScrollX = Mathf.Clamp(sc.x, 0f, maxX);
            sc = GUI.BeginScrollView(view, new Vector2(wmScrollX, 0f), content, false, false, GUIStyle.none, GUIStyle.none);

            var keepM = GUI.matrix;
            float zk = zoomAt > 0f ? Mathf.Clamp01((now - zoomAt) / ZoomTime) : 0f;
            if (zoomAt > 0f) GUIUtility.ScaleAroundPivot(Vector2.one * (1f + 2.2f * zk * zk), zoomCenter * s);   // 放大進關卡／城鎮
            for (int i = 0; i < wmRegions.Length; i++) GUI.DrawTexture(new Rect(i * WMRegionW * s, 0f, WMRegionW * s + 1f, view.height), wmRegions[i], ScaleMode.StretchToFill);

            // 路：兩端都開了才畫；通往 Boss 的是暗紅色；剛開的那段一點一點亮起來
            foreach (var p in wmPaths)
            {
                if (!wmNodes.TryGetValue(p.from, out var a) || !wmNodes.TryGetValue(p.to, out var b) || !WMNodeOpen(a) || !WMNodeOpen(b)) continue;
                bool boss = b.type == "boss" || a.type == "boss" && b.pos.x < a.pos.x;
                var col = boss ? new Color(0.55f, 0.08f, 0.06f, 0.95f) : new Color(1f, 0.9f, 0.55f, 0.95f);
                float L = PathLen(p.pts), upto = L;
                if (revealNode != null && (p.to == revealNode || p.from == revealNode)) upto = L * Mathf.Clamp01(rk * 1.6f);
                float stepD = 34f;
                for (float d = 0f; d <= upto; d += stepD)
                {
                    var q = PathAt(p.pts, d) * s; float dot = Mathf.Max(4f, fs * 0.28f);
                    UiKit.Disc(new Rect(q.x - dot / 2f, q.y - dot / 2f, dot, dot), col);
                }
            }

            // 雲霧：還沒開的地方（前線右邊）全部蓋住，邊緣是雲
            float fx = (front + 220f) * s;
            if (fx < cw)
            {
                var fogC = new Color(0.93f, 0.93f, 0.97f, 1f);
                var keepC = GUI.color; GUI.color = fogC; GUI.DrawTexture(new Rect(fx + fs * 3f, 0f, cw - fx, view.height), Texture2D.whiteTexture); GUI.color = keepC;
                for (int i = 0; i < 3; i++)
                {
                    var ft = WMTex("ui/fog_" + i); if (ft == null) continue;
                    float fsz = view.height * (0.75f + 0.15f * i), drift = Mathf.Sin(now * 0.2f + i * 2f) * fs;
                    for (float yy = -fsz * 0.3f; yy < view.height; yy += fsz * 0.55f)
                        GUI.DrawTexture(new Rect(fx - fsz * 0.35f + drift + i * fs, yy + i * fsz * 0.2f, fsz, fsz), ft, ScaleMode.ScaleToFit, true);
                }
            }

            DrawWorldBossMapMarks(s,fsS);

            // 點：圖示、名牌、星星；點了主角走過去
            string click = null;
            bool blocking = UiLock.Blocking;
            foreach (var n in open)
            {
                if (n.pos.x > front + 1f) continue;   // 還在霧裡
                var st = n.type == "town" || n.id.StartsWith("town:") || n.id == "tower" ? null : Story.Find(n.id);
                bool clear = st != null && Story.IsCleared(st.id), boss = n.type == "boss";
                string icon = n.id.StartsWith("town:") ? "ui/node_town" : n.id == "tower" ? "ui/node_tower" : boss ? (clear ? "ui/node_boss_clear" : "ui/node_boss") : clear ? "ui/node_clear" : "ui/node_open";
                float isz = Mathf.Max(fs * 2.4f, (boss || st == null ? 160f : 128f) * s * 1.25f);
                if (n.id == revealNode) isz *= 0.4f + 0.6f * Mathf.Clamp01((rk - 0.5f) * 2.5f) + Mathf.Sin(Mathf.Clamp01((rk - 0.5f) * 2f) * Mathf.PI) * 0.25f;   // 新旗子彈出來
                var c = n.pos * s; var ir = new Rect(c.x - isz / 2f, c.y - isz * 0.85f, isz, isz);
                if (n.id == revealNode && rk < 0.5f) continue;
                var it = WMTex(icon); if (it != null) GUI.DrawTexture(ir, it, ScaleMode.ScaleToFit, true);
                bool selN = sel != null && sel.id == n.id;
                if (selN) { float pz = 1f + 0.06f * Mathf.Sin(now * 5f); var rr = new Rect(c.x - isz * 0.6f * pz, c.y - isz * 0.25f * pz, isz * 1.2f * pz, isz * 0.5f * pz); UiKit.Disc(rr, new Color(1f, 0.9f, 0.4f, 0.35f)); }
                // 名牌
                float pw = Mathf.Max(fs * 6.2f, 320f * s * 1.1f), ph = fs * 2.1f;
                var lr = new Rect(n.label.x * s - pw / 2f, n.label.y * s - ph / 2f, pw, ph);
                var plate = WMTex("ui/label_plate");
                if (plate != null) GUI.DrawTexture(lr, plate, ScaleMode.StretchToFill, true); else UiKit.Card(lr, 0.85f, null, 0.6f);
                string nm = st != null ? $"{st.id}\n{st.title}" : n.id.StartsWith("town:") ? n.name : "🗼 無盡塔";
                UiKit.Text(new Rect(lr.x + fs * 0.4f, lr.y, lr.width - fs * 0.8f, lr.height), nm, Mathf.RoundToInt(fsS * 0.85f), new Color(0.25f, 0.15f, 0.08f), TextAnchor.MiddleCenter, true, 0.5f);
                // 星星
                if (st != null)
                {
                    var stx = WMTex("ui/stars_" + Story.Stars(st.id));
                    if (stx != null) GUI.DrawTexture(new Rect(c.x - fs * 1.8f, c.y + isz * 0.12f, fs * 3.6f, fs * 1.2f), stx, ScaleMode.ScaleToFit, true);
                }
                // 點擊（圖示＋名牌）：新手指引用 stage:1-1、town:0 找
                string tag = n.id.StartsWith("town:") ? n.id : n.id == "tower" ? "tower" : "stage:" + n.id;
                var hit = Rect.MinMaxRect(Mathf.Min(ir.x, lr.x), Mathf.Min(ir.y, lr.y), Mathf.Max(ir.xMax, lr.xMax), Mathf.Max(ir.yMax, lr.yMax));
                bool target = UiKit.LockTarget(hit, nm, tag);
                if ((!blocking || target) && heroPts == null && zoomAt < 0f && revealNode == null && GUI.Button(hit, GUIContent.none, GUIStyle.none)) click = n.id;
            }

            // 主角（腳底錨點）
            string hcls = prof != null && Skills.Classes.ContainsKey(prof.cls) ? prof.cls : Skills.Novice;
            bool walking = heroPts != null;
            int fr = walking ? Mathf.FloorToInt(now * 8f) % 4 : Mathf.FloorToInt(now * 1.6f) % 2;
            var htex = WMTex($"hero_{hcls}_{(walking ? "side" : "idle")}_{fr}") ?? WMTex($"hero_novice_{(walking ? "side" : "idle")}_{fr}");
            if (htex != null)
            {
                float hh = Mathf.Max(fs * 4f, 256f * s * 1.15f), hw = hh * 0.75f;
                var hp = heroPos * s;
                var hr = new Rect(hp.x - hw * (96f / 192f), hp.y - hh * (252f / 256f), hw, hh);
                if (heroLeft && walking) GUI.DrawTextureWithTexCoords(hr, htex, new Rect(1f, 0f, -1f, 1f), true);
                else GUI.DrawTexture(hr, htex, ScaleMode.StretchToFill, true);
            }
            GUI.matrix = keepM;
            GUI.EndScrollView();
            if (zoomAt > 0f)
            {
                var kc = GUI.color; GUI.color = new Color(0f, 0f, 0f, zk * 0.9f); GUI.DrawTexture(view, Texture2D.whiteTexture); GUI.color = kc;
                if (zk >= 1f) { zoomAt = -1f; var d = zoomDone; zoomDone = null; d?.Invoke(); return true; }
            }
            if (click != null) WMGoTo(click);

            // 下方：選中的關卡＋按鈕
            float by = r.yMax - gap - btnH;
            if (sel != null)
            {
                var dr = new Rect(r.x + gap, by - gap * 0.5f - detH, r.width - gap * 2, detH);
                UiKit.Card(dr, 0.93f, null, 0.8f);
                bool ok = Story.IsUnlocked(sel), story = Story.HasStory(sel) && Story.IsSeen(sel.id);
                string waves = sel.HasBoss && sel.fromWave >= 10 ? "Boss 戰" : $"{sel.toWave - sel.fromWave + 1} 波" + (sel.HasBoss ? "＋Boss" : "");
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.3f, dr.width - gap * 2, fs * 1.5f), $"{sel.id}　{sel.title}　{Story.StarText(sel.id)}", fs, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(dr.x + gap, dr.y + fs * 1.8f, dr.width - gap * 2, fs * 2.2f),
                    $"{waves}・推薦 Lv{Mathf.Max(1, sel.FirstWave - 1) + Boost}+・掉落裝備 Lv{Story.LevelBand(sel.theme).lo + Boost}–{Story.LevelBand(sel.theme).hi + Boost}\n經驗 {Mathf.RoundToInt(sel.xp * SoloBattle.RewardMulFor(Difficulty))}・💰 {Mathf.RoundToInt(Economy.StageGold(sel) * SoloBattle.RewardMulFor(Difficulty))}" + (Story.LockReason(sel) is string why ? $"\n🔒 {why}" : ""),
                    fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperLeft, false, 0.55f);
                float bw = (dr.width - gap * 3) / 2f, bh = fs * 2f, bby = dr.yMax - bh - gap * 0.4f;
                if (UiKit.Button(new Rect(dr.x + gap, bby, bw, bh), Story.IsCleared(sel.id) ? "⚔ 出發（再次挑戰）" : "⚔ 出發", fsS, true, new Color(1f, 0.55f, 0.2f), ok && zoomAt < 0f && heroPts == null))
                { var st = sel; WMZoom(wmNodes.TryGetValue(st.id, out var zn) ? zn.pos : heroPos, () => { sel = null; OnStartStage?.Invoke(st); }); }
                if (story && UiKit.Button(new Rect(dr.x + gap * 2 + bw, bby, bw, bh), "📖 看故事", fsS)) { var st = sel; OnWatchStory?.Invoke(st); return true; }
            }
            // 下方只剩「角色」「回城鎮」：無盡塔只能從地圖上 2-5 下方的岔路進去（使用者要求：打完 2-5 才能進無盡塔）
            float bw2 = (r.width - gap * 4) / 3f;
            int pts = prof.statPoints + prof.UsableSkillPoints;
            if (UiKit.Button(new Rect(r.x + gap, by, bw2, btnH), pts > 0 ? $"📜 角色 ×{pts}" : "📜 角色", fsS, pts > 0, new Color(0.75f, 0.45f, 1f))) OnCharacter?.Invoke();
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw2, by, bw2, btnH), "🐲 世界 Boss", fsS, false, new Color(1f, 0.5f, 0.3f))) { sel = null; wbPick = true; }
            if (UiKit.Button(new Rect(r.x + gap * 3 + bw2 * 2, by, bw2, btnH), $"🏘 回{Towns[Mathf.Clamp(town, 0, Towns.Length - 1)].name}", fsS)) { sel = null; OpenTown(town); }
            return true;
        }

        bool wbPick;
        static readonly string[] WbIntro =
        {
            "晨鐘熔爐裡站起來的石鐵巨人。胸口的金色熔爐是弱點。",
            "記名林的古樹化成的龍。六根樹根腳，背上開著琥珀之冠。",
            "環抱燈塔殘骸的巨大鰭鯨。額頭的潮晶會發光。",
        };
        /// <summary>大地圖的世界 Boss 選單：三隻選一隻出發（單人；三個固定階段）</summary>
        bool DrawWorldBossPick(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var col = new Color(1f, 0.5f, 0.3f);
            UiKit.Card(r, 0.92f, col, 0.8f);
            float y = r.y + gap;
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.8f), "🐲 世界 Boss 討伐", fsL, col, TextAnchor.MiddleCenter, true);
            y += fs * 2f;
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2.6f), "三個固定階段：站樁（原地出招）→ 落地（會走動、攻擊變強）→ 狂暴（不停繞著你、連續出招）。每一招都有預警、順序固定：衝刺側步、橫掃蹲、震地環跳、雷印走開。血量是一般討伐 Boss 的 8 倍，打贏保底史詩以上裝備＋試煉幣 10。", fsS, new Color(0.92f, 0.94f, 1f), TextAnchor.UpperLeft, false, 0.5f);
            y += fs * 2.9f;
            float bottom = r.yMax - gap * 2 - fs * 2.1f;
            float ch = Mathf.Min(fs * 5.2f, (bottom - y) / WorldBossArt.Ids.Length - gap * 0.4f);
            for (int i = 0; i < WorldBossArt.Ids.Length; i++)
            {
                var cr = new Rect(r.x + gap, y, r.width - gap * 2, ch);
                var c = Skills.Classes.TryGetValue("boss_" + WorldBossArt.Ids[i], out var cd) ? cd.color : col;
                UiKit.Card(cr, 0.7f, c, 0.8f);
                var poster = WorldBossPoster(i);
                float pw = poster != null ? ch * 0.66f : 0f;
                if (poster != null) GUI.DrawTexture(new Rect(cr.x + gap * 0.5f, cr.y + gap * 0.3f, pw, ch - gap * 0.6f), poster, ScaleMode.ScaleToFit, true);
                float bw = cr.width * 0.24f;
                UiKit.Text(new Rect(cr.x + pw + gap, cr.y + gap * 0.3f, cr.width - pw - bw - gap * 2.5f, fs * 1.4f), WorldBossArt.Names[i], fs, c, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(cr.x + pw + gap, cr.y + fs * 1.6f, cr.width - pw - bw - gap * 2.5f, ch - fs * 1.8f), WbIntro[i], Mathf.RoundToInt(fsS * 0.92f), new Color(0.9f, 0.92f, 1f), TextAnchor.UpperLeft, false, 0.5f);
                if (UiKit.Button(new Rect(cr.xMax - bw - gap * 0.4f, cr.y + gap * 0.5f, bw, ch - gap), "⚔ 出發", fsS, true, c)) { var id = WorldBossArt.Ids[i]; wbPick = false; OnStartWorldBoss?.Invoke(id); return true; }
                y += ch + gap * 0.4f;
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), "◀ 回大地圖", fs)) wbPick = false;
            return true;
        }
    }
}
