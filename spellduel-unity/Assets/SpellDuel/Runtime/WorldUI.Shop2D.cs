using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 道具店、職業工會（初心者選職業）的 2D 畫面（Codex 的 Village2D 第三批；版面照 Docs/village2d/batch3/*_layout.json，1080×1920 設計座標）。
    /// 共用框架：全螢幕背景、上方標題（名稱＋金幣）、右邊對話泡泡＋NPC 半身像、下方「🏘 回城鎮」；左邊是半透明直排列表（上下拖曳）。
    /// 買賣、試煉、選職業的邏輯都沿用原本的；沒有素材就照舊用原本的畫面。
    /// </summary>
    public partial class WorldUI
    {
        static readonly Dictionary<string, Texture2D> facBg = new Dictionary<string, Texture2D>();
        static Dictionary<string, string[]> shopLines;
        Vector2 shopScroll, guildScroll2;
        string shopExpr = "normal", shopLine, guildLine;

        Texture2D FacilityBg(string key)
        {
            string locale=TownMapKeys[Mathf.Clamp(town,0,TownMapKeys.Length-1)],cacheKey=key+"_"+locale;
            if(!facBg.TryGetValue(cacheKey,out var t)){t=Resources.Load<Texture2D>("Village2D/in_"+cacheKey)??Resources.Load<Texture2D>("Village2D/in_"+key+"_flame");facBg[cacheKey]=t;}
            return t;
        }

        static string ShopLine(string group, System.Random rng)
        {
            if (shopLines == null)
            {
                shopLines = new Dictionary<string, string[]>();
                var ta = Resources.Load<TextAsset>("Village2D/lines_shop");
                string cur = null; var acc = new Dictionary<string, List<string>>();
                foreach (var raw in (ta != null ? ta.text : "").Split('\n'))
                {
                    var l = raw.Trim();
                    if (l.StartsWith("[") && l.EndsWith("]")) { cur = l.Substring(1, l.Length - 2); if (!acc.ContainsKey(cur)) acc[cur] = new List<string>(); }
                    else if (l.Length > 0 && cur != null) acc[cur].Add(l);
                }
                foreach (var kv in acc) shopLines[kv.Key] = kv.Value.ToArray();
            }
            return shopLines.TryGetValue(group, out var arr) && arr.Length > 0 ? arr[rng.Next(arr.Length)] : null;
        }

        /// <summary>設計座標 → 螢幕座標（寬照螢幕寬、高照安全區）</summary>
        static Rect Design(float x, float y, float w, float h)
        {
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            var safe = UnityEngine.Screen.safeArea;
            float top = H - safe.yMax, bot = H - safe.y, sx = W / 1080f, sy = (bot - top) / 1920f;
            return new Rect(x * sx, top + y * sy, w * sx, h * sy);
        }

        /// <summary>共用框架：背景、標題、NPC、對話泡泡、回城鎮。回傳有沒有按「回城鎮」</summary>
        bool Facility2DFrame(Texture2D bg, string title, Color col, string npcId, string expr, string npcName, string line, int fs, int fsS, int fsL, bool drawReturn = true)
        {
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            GUI.DrawTexture(new Rect(0, 0, W, H), bg, ScaleMode.ScaleAndCrop);
            var hr = Design(30, 45, 1020, 115);
            UiKit.MenuCard(hr, 0.92f, col, 0.8f);
            UiKit.Text(new Rect(hr.x + fs * 0.6f, hr.y, hr.width * 0.55f, hr.height), title, fsL, Color.white, TextAnchor.MiddleLeft, true);
            float rx = SettingsMenu.RightEdge(hr.y, hr.yMax, hr.xMax - fs * 0.6f);
            UiKit.Text(new Rect(hr.x + hr.width * 0.55f, hr.y, rx - hr.x - hr.width * 0.55f, hr.height), $"💰 {Economy.Gold}", fs, new Color(1f, 0.88f, 0.4f), TextAnchor.MiddleRight, true);
            var tex = Story2D.Portrait(npcId, expr);
            if (tex != null) GUI.DrawTexture(Design(660, 990, 390, 730), tex, ScaleMode.ScaleToFit, true);
            var br = Design(675, 730, 365, 190);
            UiKit.MenuCard(br, 0.9f, col, 0.7f);
            UiKit.Text(new Rect(br.x + fs * 0.5f, br.y + fs * 0.3f, br.width - fs, br.height - fs * 0.6f), $"{npcName}：\n{line}", fsS, Color.white, TextAnchor.UpperLeft, false, 0.5f);
            return drawReturn && UiKit.Button(Design(270, 1795, 540, 92), "🏘 回城鎮", fs, false, new Color(1f, 0.55f, 0.2f));
        }

        // ---------------------------------------------------------------- 第四批：六個設施共用的框架
        bool frame2D; string frameLine;
        readonly Dictionary<string, string> frameLines = new Dictionary<string, string>();
        /// <summary>
        /// 有 2D 背景（in_{key}_flame）就畫框架，再把原本的功能畫在左邊（標題、NPC 介紹在框架模式下不畫；介紹的話放進對話泡泡）。
        /// 回城鎮等按鈕沿用原本功能畫面裡的。回傳 false＝沒有素材。
        /// </summary>
        bool DrawFramed(string key, string title, Color col, string npcId, string npcName, string hello, System.Action<Rect> body, int fs, int fsS, int fsL)
        {
            var bg = FacilityBg(key);
            if (bg == null) return false;
            frameLines.TryGetValue(key, out var line);
            Facility2DFrame(bg, title, col, npcId, "normal", npcName, line ?? hello, fs, fsS, fsL, false);
            frame2D = true; frameLine = null;
            try { body(Design(30, 190, 610, 1710)); }
            finally { frame2D = false; }
            if (frameLine != null) frameLines[key] = frameLine;
            return true;
        }

        // ---------------------------------------------------------------- 道具店
        bool DrawShop2D(CharacterProfile prof, int fs, int fsS, int fsL)
        {
            var bg = FacilityBg("shop");
            if (bg == null) return false;
            var col = new Color(0.5f, 0.85f, 1f);
            if (Facility2DFrame(bg, "🧪 道具店・" + Towns[town].name, col, "clerk", shopExpr, "店員", shopLine ?? "旅途的補給準備好了。", fs, fsS, fsL))
            { screen = Screen.Town; shopLine = null; shopExpr = "normal"; return true; }
            if (UiKit.Button(Design(40, 195, 285, 85), "買道具", fs, shopTab == 0, col)) { shopTab = 0; selItem = null; shopScroll = Vector2.zero; }
            if (UiKit.Button(Design(335, 195, 285, 85), "賣裝備", fs, shopTab == 1, UiKit.Gold)) { shopTab = 1; selItem = null; shopScroll = Vector2.zero; }
            // 藥劑師：用材料做強化藥劑（WorldUI.Craft.cs）
            var view = Design(30, 290, 610, 850);
            UiKit.MenuCard(view, 0.55f, null, 0.5f);
            var row = Design(45, 300, 580, 255);
            float rowH = shopTab == 0 ? row.height : row.height * 0.62f, rowGap = Design(0, 0, 0, 15).height, pad = row.x - view.x;
            var bag = new List<Item>(Progress.Bag);
            var shopList = Economy.Consumables.FindAll(c => c.price > 0);   // 藥劑師做的強化藥劑不在店裡賣
            int n = shopTab == 0 ? shopList.Count : bag.Count;
            var content = new Rect(0, 0, view.width - 4f, Mathf.Max(view.height, n * (rowH + rowGap) + rowGap));
            shopScroll = UiKit.DragScroll(view, shopScroll, content);
            shopScroll = GUI.BeginScrollView(view, shopScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < n; i++)
            {
                var rr = new Rect(pad, rowGap + i * (rowH + rowGap), content.width - pad * 2, rowH);
                UiKit.MenuCard(rr, 0.82f, shopTab == 0 ? col : (Color?)Item.RarityColors[bag[i].rarity], 0.6f);
                if (shopTab == 0)
                {
                    var c = shopList[i];
                    UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.06f, rr.width - fs * 1.2f, rr.height * 0.28f), $"{c.icon} {c.name}　持有 {Economy.Count(c.id)}", fs, Color.white, TextAnchor.MiddleLeft, true);
                    UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.34f, rr.width - fs * 1.2f, rr.height * 0.26f), c.desc, fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleLeft, false, 0.5f);
                    var bb = new Rect(rr.xMax - rr.width * 0.52f, rr.y + rr.height * 0.62f, rr.width * 0.5f - fs * 0.3f, rr.height * 0.33f);
                    UiKit.NextTag("buy:" + c.id);   // 新手指引：買小藥水
                    if (UiKit.Button(bb, $"💰 {c.price} 購買", fsS, false, new Color(1f, 0.55f, 0.2f)))
                    {
                        if (Economy.Gold < c.price) { shopExpr = "surprised"; shopLine = ShopLine("insufficient", rng) ?? "金幣好像還差一些。"; Sfx.Play("fail", 0.5f); }
                        else if (Economy.Buy(c, out var why)) { shopExpr = "smile"; shopLine = ShopLine("sale", rng) ?? "謝謝惠顧！"; Toast($"買了 {c.name}"); Sfx.Play("coin", 0.8f); }
                        else { shopExpr = "surprised"; shopLine = why; }
                    }
                }
                else
                {
                    var it = bag[i];
                    UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.06f, rr.width * 0.55f, rr.height * 0.5f), it.FullName, fsS, Item.RarityColors[it.rarity], TextAnchor.MiddleLeft, true);
                    UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.52f, rr.width * 0.55f, rr.height * 0.42f), $"{Item.RarityNames[it.rarity]}・Lv{it.reqLevel}", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleLeft);
                    var sb = new Rect(rr.xMax - rr.width * 0.4f, rr.y + rr.height * 0.18f, rr.width * 0.38f, rr.height * 0.64f);
                    UiKit.NextTag("item:" + it.uid);
                    if (UiKit.Button(sb, $"賣 💰 {Economy.SellPrice(it)}", fsS, false, UiKit.Gold))
                    {
                        Economy.Gold += Economy.SellPrice(it);
                        Progress.Bag.Remove(it); Progress.SaveBag();
                        shopExpr = "smile"; shopLine = ShopLine("sell", rng) ?? "我收下了。";
                        Toast($"賣掉了 {it.FullName}"); Sfx.Play("coin", 0.8f, 0.9f);
                        break;
                    }
                }
            }
            if (shopTab == 1 && n == 0) UiKit.Text(new Rect(0, 0, content.width, rowH), "背包是空的\n（身上穿的要先卸下才能賣）", fsS, Color.gray, TextAnchor.MiddleCenter);
            GUI.EndScrollView();
            if (shopTab == 0)
                UiKit.Text(Design(40, 1150, 600, 200), "藥水在戰鬥中用唸的（「喝藥水」「大藥水」「魔力藥水」）或比 🤙 使用，不限瓶數，小藥水 5 秒、大藥水 10 秒冷卻；回城捲軸說「回城」。", fsS, new Color(0.95f, 0.95f, 1f), TextAnchor.UpperLeft);
            else
                UiKit.Bar(Design(40, 1150, 600, 50), bag.Count / (float)Progress.BagSize, bag.Count >= Progress.BagSize ? new Color(1f, 0.35f, 0.3f) : new Color(0.45f, 0.85f, 0.5f), $"背包 {bag.Count}/{Progress.BagSize}", fsS);
            return true;
        }

        // ---------------------------------------------------------------- 職業工會（初心者選職業）
        bool DrawGuild2D(CharacterProfile prof, int fs, int fsS, int fsL)
        {
            var bg = FacilityBg("guild");
            if (bg == null || prof.cls != Skills.Novice || jobInfo != null) return false;   // 進階轉職、職業介紹頁：照舊
            bool ok = prof.CanChangeJob, lvOk = prof.level >= Skills.JobLevel;
            string chosen = prof.jobTrialCls; var cc = chosen != null && Skills.Classes.TryGetValue(chosen, out var cd0) ? cd0 : null;
            var col = new Color(0.85f, 0.6f, 1f);
            string line = guildLine ?? GuildNoviceLine(prof);
            if (Facility2DFrame(bg, "🏛 職業工會", col, "guildmaster", ok ? "smile" : "serious", "公會長", line, fs, fsS, fsL))
            { screen = Screen.Town; guildLine = null; return true; }
            UiKit.Text(Design(45, 195, 600, 80), ok ? "正式轉職" : prof.jobTrial > 0 ? "轉職試煉進行中" : "選擇你想走的路", fsL, Color.white, TextAnchor.MiddleLeft, true);

            // 右上：轉職試煉（選了職業才出題：第 1 關專業試煉、第 2 關實戰；通過打勾、下一關可以挑戰）
            var tr = Design(680, 205, 360, 260);
            UiKit.MenuCard(tr, 0.9f, new Color(1f, 0.55f, 0.2f), 0.9f);
            UiKit.Text(new Rect(tr.x + fs * 0.5f, tr.y + fs * 0.2f, tr.width - fs, fs * 1.5f), cc != null ? $"轉職試煉・{cc.name}" : "轉職試煉", fs, Color.white, TextAnchor.MiddleLeft, true);
            int rounds = Mathf.Max(1, Skills.JobTrialRounds);
            float th = (tr.height - fs * 2f) / rounds;
            for (int i = 0; i < Skills.JobTrialRounds; i++)
            {
                bool done = prof.jobTrial > i, next = lvOk && chosen != null && prof.jobTrial == i;
                var rr = new Rect(tr.x + fs * 0.4f, tr.y + fs * 1.8f + i * th, tr.width - fs * 0.8f, th - fs * 0.2f);
                string nm = i == 0 && chosen != null ? SoloBattle.ClassTrialName(chosen) : JobTrialNames != null && i < JobTrialNames.Length ? JobTrialNames[i] : $"第 {i + 1} 關";
                string bt = done ? $"✔ {nm}：已通過" : next ? $"挑戰{nm}" : !lvOk ? $"Lv{Skills.JobLevel} 才能挑戰" : chosen == null ? "先選職業" : "先過前一關";
                if (UiKit.Button(rr, bt, fsS, next, new Color(1f, 0.6f, 0.3f), next)) { OnJobTrial?.Invoke(i); return true; }
            }

            // 左邊：五個職業（臉部頭像、一句介紹；還沒選＝「選這條路」開始那個職業的試煉；都過了＝「成為…」正式轉職）
            var view = Design(30, 290, 610, 1190);
            UiKit.MenuCard(view, 0.55f, null, 0.5f);
            var row = Design(45, 300, 580, 215);
            float rowGap = Design(0, 0, 0, 15).height, pad = row.x - view.x;
            int n = Skills.ClassOrder.Length;
            var content = new Rect(0, 0, view.width - 4f, Mathf.Max(view.height, n * (row.height + rowGap) + rowGap));
            guildScroll2 = UiKit.DragScroll(view, guildScroll2, content);
            guildScroll2 = GUI.BeginScrollView(view, guildScroll2, content, false, false, GUIStyle.none, GUIStyle.none);
            string pick = null, info = null;
            for (int i = 0; i < n; i++)
            {
                var id = Skills.ClassOrder[i]; var c = Skills.Classes[id];
                bool mine = chosen == id;
                var rr = new Rect(pad, rowGap + i * (row.height + rowGap), content.width - pad * 2, row.height);
                UiKit.MenuCard(rr, 0.85f, c.color, mine || jobArm == id ? 1f : 0.6f);
                float fsz = rr.height * 0.5f;
                Story2D.DrawFace(new Rect(rr.xMax - fsz - fs * 0.5f, rr.y + fs * 0.4f, fsz, fsz), Story2D.ClassPortrait(id, null), c.color);
                UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.05f, rr.width - fsz - fs * 1.6f, rr.height * 0.28f), c.name + (mine ? "　✔ 你選的路" : ""), fsL, c.color, TextAnchor.MiddleLeft, true);
                string intro = c.desc; int cut = intro.IndexOfAny(new[] { '：', '，', '。' }); if (cut > 0) intro = intro.Substring(0, cut);
                UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.33f, rr.width - fsz - fs * 1.6f, rr.height * 0.3f), intro, fsS, new Color(0.88f, 0.9f, 1f), TextAnchor.MiddleLeft, false, 0.5f);
                bool armed = jobArm == id && Time.time < jobArmUntil;
                var cb = new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.66f, rr.width * 0.45f, rr.height * 0.3f);
                string bt; bool can;
                if (!lvOk) { bt = $"Lv{Skills.JobLevel} 後"; can = false; }
                else if (ok) { bt = mine ? (armed ? "再按一次確定" : $"✨ 成為{c.name}") : "你選的不是這條"; can = mine; }
                else if (prof.jobTrial == 0 || !mine && chosen == null) { bt = mine ? "挑戰試煉" : "選這條路"; can = true; }
                else { bt = mine ? "繼續試煉" : "重選（試煉重來）"; can = true; }
                if (UiKit.Button(cb, bt, fsS, armed || (mine && !ok), ok ? new Color(1f, 0.55f, 0.2f) : c.color, can)) pick = id;
                if (UiKit.Button(new Rect(cb.xMax + fs * 0.4f, cb.y, rr.width * 0.4f, cb.height), "📖 介紹", fsS)) info = id;
            }
            GUI.EndScrollView();
            if (info != null) { jobInfo = info; jobInfoScroll = Vector2.zero; Sfx.Play("select", 0.5f); return true; }
            if (pick != null)
            {
                var c = Skills.Classes[pick];
                if (!ok) { guildLine = null; OnJobTrialPick?.Invoke(pick); return true; }   // 選職業／繼續或重來：開始試煉
                if (jobArm == pick && Time.time < jobArmUntil) { jobArm = null; guildLine = null; OnJobChange?.Invoke(pick); return true; }
                jobArm = pick; jobArmUntil = Time.time + 3f;
                guildLine = $"成為「{c.name}」？選定便不能更換。3 秒內再按一次確定。";
            }
            return true;
        }

        /// <summary>公會長對初心者說的話（照轉職進度）</summary>
        static string GuildNoviceLine(CharacterProfile prof)
        {
            bool lvOk = prof.level >= Skills.JobLevel;
            var cc = prof.jobTrialCls != null && Skills.Classes.TryGetValue(prof.jobTrialCls, out var c) ? c : null;
            if (!lvOk) return $"初心者，先用木棍和石頭保護好自己。到了 Lv{Skills.JobLevel} 再來（還差 {Skills.JobLevel - prof.level} 級）。";
            if (prof.CanChangeJob && cc != null) return $"兩關都過了。按下去，你就是{cc.name}了——選定便不能更換。";
            if (prof.jobTrial > 0 && cc != null) return $"你通過了{cc.name}的{SoloBattle.ClassTrialName(prof.jobTrialCls)}。接下來是實戰：打倒空地上的 5 隻小怪。";
            return "到了 10 級了。五條路各有各的試煉——先選你想走的路，我會告訴你那條路要通過什麼。失敗了再來找我就好。";
        }
    }
}
