using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 城鎮的 2D 俯瞰圖（Codex 的 Village2D 素材；VILLAGE_2D_BRIEF.md）：取代原本的「📋 清單」畫面。
    ///   地圖放大（比螢幕寬，MapZoom），上下左右拖曳；每棟建築上方是程式畫的名牌（自動避開居民，點名牌或建築就進去）；
    ///   居民的小圖站在門口，有任務的頭上有 ❗，點人、❗或名字就聊天／接任務。新手指引要按的東西在畫面外會自動捲過去。
    ///   下方：聊天框（居民台詞＋任務按鈕）、角色、走進城鎮、回大地圖。
    /// 素材：Resources/Village2D/map_{town}.png、map_{town}.json（2048×3072 的像素座標，左上原點）、npc_{id}.png（腳底錨點）。
    /// 沒有素材的城鎮照舊用清單。
    /// </summary>
    public partial class WorldUI
    {
        static readonly string[] TownMapKeys = { "flame", "forest", "harbor", "desert", "snow" };
        const float MapW = 2048f, MapH = 3072f;

        class TownMapData
        {
            public Texture2D tex;
            public readonly Dictionary<string, Rect> hit = new Dictionary<string, Rect>();
            public readonly Dictionary<string, Vector2> label = new Dictionary<string, Vector2>();
            public readonly Dictionary<string, Vector2> npc = new Dictionary<string, Vector2>();
        }
        static readonly Dictionary<int, TownMapData> townMaps = new Dictionary<int, TownMapData>();
        static readonly Dictionary<string, Texture2D> npcSprites = new Dictionary<string, Texture2D>();
        Vector2 townMapScroll;
        int townMapInit = -1;   // 地圖捲動位置是哪個城鎮的（換城鎮＝重新置中）
        /// <summary>城鎮地圖放大倍率（1＝剛好螢幕寬）；比螢幕大的部分用拖曳看</summary>
        const float MapZoom = 1.6f;
        /// <summary>名牌被擋住時依序試的位置（以名牌寬、高為單位）</summary>
        static readonly Vector2[] PlateTries =
        {
            new Vector2(0f, 0f), new Vector2(0f, -1f), new Vector2(-0.35f, 0f), new Vector2(0.35f, 0f), new Vector2(0f, 1f),
            new Vector2(-0.35f, -1f), new Vector2(0.35f, -1f), new Vector2(0f, -2f), new Vector2(-0.7f, 0f), new Vector2(0.7f, 0f), new Vector2(0f, 2f),
        };

        /// <summary>建築 key → 名牌文字（新手指引用文字找按鈕：保留「⚒ 鐵匠鋪」「🏛 職業工會」等）、顏色</summary>
        static readonly Dictionary<string, (string label, Color color)> MapSites = new Dictionary<string, (string, Color)>
        {
            ["smith"] = ("⚒ 鐵匠鋪", new Color(1f, 0.6f, 0.3f)),
            ["shop"] = ("🧪 道具店", new Color(0.5f, 0.85f, 1f)),
            ["guild"] = ("🏛 職業工會", new Color(0.85f, 0.6f, 1f)),
            ["dungeon"] = ("🏰 副本入口", new Color(0.4f, 0.85f, 0.75f)),
            ["training"] = ("📅 訓練場", new Color(0.45f, 0.9f, 0.7f)),
            ["bounty"] = ("🔥 討伐告示板", new Color(1f, 0.45f, 0.2f)),
            ["watch"] = ("👀 觀戰席", new Color(0.55f, 0.9f, 1f)),
            ["warp"] = ("🌀 傳送陣", new Color(0.55f, 0.65f, 1f)),
            ["gate"] = ("🚪 村口", new Color(1f, 0.85f, 0.45f)),
            ["mayorhome"] = ("🏠 村長家", new Color(1f, 0.75f, 0.55f)),
            ["belltower"] = ("🔔 晨鐘塔", new Color(1f, 0.9f, 0.5f)),
        };

        static TownMapData MapFor(int t)
        {
            if (townMaps.TryGetValue(t, out var d)) return d;
            d = null;
            if (t >= 0 && t < TownMapKeys.Length)
            {
                var tex = Resources.Load<Texture2D>("Village2D/map_" + TownMapKeys[t]);
                var js = Resources.Load<TextAsset>("Village2D/map_" + TownMapKeys[t]);
                if (tex != null && js != null) { d = new TownMapData { tex = tex }; ParseTownMap(js.text, d); }
            }
            townMaps[t] = d;
            return d;
        }

        /// <summary>讀 map_{town}.json（只有一層：建築 key → x,y,w,h,label_x,label_y；npc → id → x,y）</summary>
        static void ParseTownMap(string json, TownMapData d)
        {
            float Num(string body, string k) { var m = Regex.Match(body, "\"" + k + "\"\\s*:\\s*(-?[0-9.]+)"); return m.Success ? float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f; }
            int npcAt = json.IndexOf("\"npc\"");
            string sites = npcAt >= 0 ? json.Substring(0, npcAt) : json, npcs = npcAt >= 0 ? json.Substring(npcAt + 5) : "";
            foreach (Match m in Regex.Matches(sites, "\"(\\w+)\"\\s*:\\s*\\{([^{}]*)\\}"))
            {
                string k = m.Groups[1].Value, b = m.Groups[2].Value;
                d.hit[k] = new Rect(Num(b, "x"), Num(b, "y"), Num(b, "w"), Num(b, "h"));
                d.label[k] = new Vector2(Num(b, "label_x"), Num(b, "label_y"));
            }
            foreach (Match m in Regex.Matches(npcs, "\"(\\w+)\"\\s*:\\s*\\{([^{}]*)\\}"))
                d.npc[m.Groups[1].Value] = new Vector2(Num(m.Groups[2].Value, "x"), Num(m.Groups[2].Value, "y"));
        }

        static Texture2D NpcSprite(string id)
        {
            if (npcSprites.TryGetValue(id, out var t)) return t;
            t = Resources.Load<Texture2D>("Village2D/npc_" + id);
            npcSprites[id] = t;
            return t;
        }

        /// <summary>有俯瞰圖就畫俯瞰圖（回傳 true）；沒有回傳 false（照舊畫清單）</summary>
        bool DrawTownMap(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var d = MapFor(town);
            if (d == null) return false;
            var t = Towns[town];
            float y = r.y + gap;
            Header(r, $"🏘 {t.name}", t.color, prof, fs, fsS, fsL, gap, ref y);
            // 下方：聊天框＋按鈕
            float btnH = fs * 2.1f, talkH = npcTalk >= 0 ? fs * 4.6f : 0f;
            float bottom = r.yMax - gap - btnH - (talkH > 0f ? talkH + gap * 0.5f : 0f) - gap * 0.5f;
            var view = new Rect(r.x, y, r.width, Mathf.Max(fs * 4f, bottom - y));
            // 地圖放大（比螢幕寬）：上下左右拖曳移動
            float s = view.width / MapW * MapZoom, cw = MapW * s, ch = MapH * s;
            var content = new Rect(0f, 0f, cw, ch);
            Rect M(Rect a) => new Rect(a.x * s, a.y * s, a.width * s, a.height * s);

            // 居民：先算好位置（腳底錨點），名牌要避開他們，點擊也優先給居民
            var npcs = new List<(int i, Rect body, Rect tag, Rect mark, bool quest)>();
            for (int i = 0; i < t.npcs.Length; i++)
            {
                string id = Story2D.IdOf(t.npcs[i].name);
                if (id == null || !d.npc.TryGetValue(id, out var feet)) continue;
                float h = Mathf.Max(fs * 4.2f, 240f * s * 1.25f), w = h * 160f / 240f;
                var body = new Rect(feet.x * s - w / 2f, feet.y * s - h, w, h);
                var tag = new Rect(body.x - fs * 0.6f, body.yMax - fs * 0.2f, body.width + fs * 1.2f, fs * 1.3f);
                bool quest = NpcQuestLabel?.Invoke(t.npcs[i].name) != null;
                float ms = fs * 1.7f;
                var mark = new Rect(body.center.x - ms / 2f, body.y - ms * 0.9f, ms, ms);
                npcs.Add((i, body, tag, mark, quest));
            }
            // ❗ 撞到別的居民（身體或名字）：改放頭的右上、左上
            for (int a = 0; a < npcs.Count; a++)
            {
                var n = npcs[a];
                if (!n.quest) continue;
                float ms = n.mark.width;
                foreach (var cand in new[] { n.mark, new Rect(n.body.xMax - ms * 0.35f, n.body.y - ms * 0.4f, ms, ms), new Rect(n.body.x - ms * 0.65f, n.body.y - ms * 0.4f, ms, ms) })
                {
                    bool hit = false;
                    for (int b = 0; b < npcs.Count; b++) if (b != a && (npcs[b].tag.Overlaps(cand) || npcs[b].body.Overlaps(cand))) { hit = true; break; }
                    if (!hit) { n.mark = cand; break; }
                }
                npcs[a] = n;
            }

            // 名牌：從設定的位置開始，撞到居民或別的名牌就挪開
            var plates = new List<(string key, Rect rect)>();
            var taken = new List<Rect>();
            foreach (var n in npcs) { taken.Add(n.body); taken.Add(n.tag); if (n.quest) taken.Add(n.mark); }
            foreach (var kv in d.label)
            {
                if (!MapSites.TryGetValue(kv.Key, out var site)) continue;
                float pw = fs * 5.4f, ph = fs * 1.4f;
                var at = new Vector2(kv.Value.x * s, kv.Value.y * s);
                Rect best = default; bool found = false;
                foreach (var o in PlateTries)
                {
                    var c = new Rect(at.x - pw / 2f + o.x * pw, at.y - ph / 2f + o.y * ph, pw, ph);
                    c.x = Mathf.Clamp(c.x, 0f, cw - pw); c.y = Mathf.Clamp(c.y, 0f, ch - ph);
                    bool hit = false;
                    foreach (var q in taken) if (q.Overlaps(c)) { hit = true; break; }
                    if (!hit) { best = c; found = true; break; }
                }
                if (!found) { best = new Rect(Mathf.Clamp(at.x - pw / 2f, 0f, cw - pw), Mathf.Clamp(at.y - ph / 2f, 0f, ch - ph), pw, ph); }
                plates.Add((kv.Key, best)); taken.Add(best);
            }

            // 捲動：第一次進來置中；新手指引要按的東西在畫面外 → 自動捲過去（指引中不能拖曳）
            if (townMapInit != town) { townMapInit = town; townMapScroll = new Vector2((cw - view.width) / 2f, 0f); }
            if (UiKit.LockKey != null)
            {
                Rect? want = null;
                foreach (var pl in plates) if (UiKit.LockMatch(UiKit.LockKey, MapSites[pl.key].label, null)) { want = pl.rect; break; }
                if (want == null) foreach (var n in npcs) if (UiKit.LockMatch(UiKit.LockKey, Portraits.Key(t.npcs[n.i].name), "npc:" + t.npcs[n.i].name)) { want = Rect.MinMaxRect(n.body.x, n.mark.y, n.body.xMax, n.tag.yMax); break; }
                if (want is Rect wr)
                {
                    var vis = new Rect(townMapScroll.x, townMapScroll.y, view.width, view.height);
                    var inner = new Rect(vis.x + fs, vis.y + fs, vis.width - fs * 2f, vis.height - fs * 2f);
                    if (!inner.Contains(wr.min) || !inner.Contains(wr.max))
                        townMapScroll = Vector2.Lerp(townMapScroll, new Vector2(wr.center.x - view.width / 2f, wr.center.y - view.height / 2f), 0.2f);
                }
            }
            townMapScroll = UiKit.DragScroll(view, townMapScroll, content);
            townMapScroll.x = Mathf.Clamp(townMapScroll.x, 0f, Mathf.Max(0f, cw - view.width));
            townMapScroll.y = Mathf.Clamp(townMapScroll.y, 0f, Mathf.Max(0f, ch - view.height));
            townMapScroll = GUI.BeginScrollView(view, townMapScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            GUI.DrawTexture(content, d.tex, ScaleMode.StretchToFill);
            string open = null;
            var mp = Event.current.mousePosition;
            bool onNpc = false;
            foreach (var n in npcs) if (n.body.Contains(mp) || n.tag.Contains(mp) || (n.quest && n.mark.Contains(mp))) { onNpc = true; break; }
            // 建築本體（透明按鈕）：點了也能進去（點在居民身上時讓給居民）
            if (!onNpc)
                foreach (var kv in d.hit)
                    if (MapSites.ContainsKey(kv.Key) && GUI.Button(M(kv.Value), GUIContent.none, GUIStyle.none) && !UiLock.Blocking) open = kv.Key;
            // 名牌（程式畫：新手指引照名牌文字找按鈕）
            foreach (var pl in plates)
            {
                var site = MapSites[pl.key];
                if (UiKit.Button(pl.rect, site.label, Mathf.RoundToInt(fsS * 0.9f), false, site.color)) open = pl.key;
            }
            // 居民：站在自己的位置；有任務的頭上有會跳的 ❗；點人、點 ❗、點名字都能聊天
            foreach (var n in npcs)
            {
                int i = n.i;
                var spr = NpcSprite(Story2D.IdOf(t.npcs[i].name));
                if (spr != null) GUI.DrawTexture(n.body, spr, ScaleMode.ScaleToFit, true);
                bool tap = GUI.Button(n.body, GUIContent.none, GUIStyle.none) && !UiLock.Blocking;
                if (n.quest)
                {
                    float bob = Mathf.Sin(Time.unscaledTime * 4f + i) * fs * 0.18f;
                    var mr = new Rect(n.mark.x, n.mark.y + bob, n.mark.width, n.mark.height);
                    UiKit.Disc(new Rect(mr.x - 3f, mr.y - 3f, mr.width + 6f, mr.height + 6f), new Color(0.25f, 0.12f, 0f, 0.85f));
                    UiKit.Disc(mr, new Color(1f, 0.82f, 0.25f));
                    UiKit.Text(mr, "!", Mathf.RoundToInt(fs * 1.2f), new Color(0.35f, 0.12f, 0f), TextAnchor.MiddleCenter, true);
                    if (GUI.Button(mr, GUIContent.none, GUIStyle.none) && !UiLock.Blocking) tap = true;
                }
                UiKit.NextTag("npc:" + t.npcs[i].name);
                if (UiKit.Button(n.tag, Portraits.Key(t.npcs[i].name), Mathf.RoundToInt(fsS * 0.85f), npcTalk == i, t.color)) tap = true;
                if (tap)
                {
                    if (npcTalk == i) npcLine = (npcLine + 1) % t.npcs[i].lines.Length; else { npcTalk = i; npcLine = 0; }
                    Sfx.Play("talk", 0.6f);
                }
            }
            GUI.EndScrollView();
            if (open != null) { OpenMapSite(open); return true; }

            // 聊天框（點了居民才出現）
            float by = r.yMax - gap - btnH;
            if (npcTalk >= 0 && npcTalk < t.npcs.Length)
            {
                var talkR = new Rect(r.x + gap, by - gap * 0.5f - talkH, r.width - gap * 2, talkH);
                UiKit.Card(talkR, 0.9f, t.color, 0.8f);
                float ps = talkR.height - fs * 1.5f - gap;
                var pr = new Rect(talkR.x + gap, talkR.y + gap * 0.5f, ps, ps);
                GUI.DrawTexture(pr, Portraits.Get(t.npcs[npcTalk].name, "npc"), ScaleMode.ScaleToFit, true);
                UiKit.Text(new Rect(pr.x - gap * 0.5f, pr.yMax, ps + gap, fs * 1.4f), t.npcs[npcTalk].name, fsS, t.color, TextAnchor.MiddleCenter, true);
                string ql = NpcQuestLabel?.Invoke(t.npcs[npcTalk].name);
                float qh = ql != null ? fs * 1.6f : 0f;
                UiKit.Text(new Rect(pr.xMax + gap, talkR.y, talkR.xMax - pr.xMax - gap * 2 - fs * 1.6f, talkR.height - qh), $"「{t.npcs[npcTalk].lines[npcLine]}」", fsS, Color.white, TextAnchor.MiddleLeft);
                if (UiKit.Button(new Rect(talkR.xMax - fs * 1.6f, talkR.y + gap * 0.3f, fs * 1.3f, fs * 1.3f), "✕", fsS)) npcTalk = -1;
                else if (ql != null && UiKit.Button(new Rect(pr.xMax + gap, talkR.yMax - qh - gap * 0.3f, talkR.xMax - pr.xMax - gap * 2, qh), ql, fsS, true, new Color(1f, 0.8f, 0.35f)))
                    OnNpcQuest?.Invoke(t.npcs[npcTalk].name);
            }
            float bw = (r.width - gap * 4) / 3f;
            if (UiKit.Button(new Rect(r.x + gap, by, bw, btnH), "📜 角色／裝備", fsS, false, new Color(0.75f, 0.45f, 1f))) OnCharacter?.Invoke();
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw, by, bw, btnH), "🚶 走進城鎮", fsS, false, t.color)) walkMode = true;
            if (UiKit.Button(new Rect(r.x + gap * 3 + bw * 2, by, bw, btnH), "🗺 回大地圖", fsS)) screen = Screen.Map;
            return true;
        }

        void OpenMapSite(string key)
        {
            Sfx.Play("select", 0.6f);
            var t = Towns[town];
            int Npc(string who) { for (int i = 0; i < t.npcs.Length; i++) if (t.npcs[i].name.Contains(who)) return i; return -1; }
            switch (key)
            {
                case "smith": screen = Screen.Smith; selItem = null; page = 0; break;
                case "shop": screen = Screen.Shop; selItem = null; page = 0; break;
                case "guild": screen = Screen.Guild; break;
                case "gate": OpenGate(town); break;
                case "warp": OpenWarp(); break;
                case "mayorhome": { int i = Npc("老焰"); if (i >= 0) { npcTalk = i; npcLine = 0; } break; }
                case "belltower": { int i = Npc("老焰"); if (i >= 0) { npcTalk = i; npcLine = 0; } break; }   // 晨鐘塔：村長守的鐘（之後有專屬對話再換）
                default: OpenFacility(key); break;
            }
        }
    }
}
