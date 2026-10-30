using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 鐵匠鋪的 2D 畫面（Codex 的 Village2D 第二批；版面照 Docs/village2d/batch2/smith_layout.json，1080×1920 設計座標）：
    ///   左上「要強化的裝備」＋半透明直排裝備列表（上下拖曳）；右上「⚒ 強化」；下面是強化前→強化後（只加主屬性）；
    ///   右邊鐵匠的對話泡泡＋半身像，右下鐵砧。按強化 → 鐵匠專心敲 3 輪（1.5 秒）→ 成功得意、失敗尷尬，各說一句。
    ///   炎之里是小鐵，其他城鎮是鐵匠師父。沒有素材就照舊用原本的畫面。
    /// </summary>
    public partial class WorldUI
    {
        const float SmithAnimTime = 1.5f;
        static Texture2D smithBg; static Texture2D[] anvilFrames; static string[] smithWin, smithLose; static bool smithLoaded;
        Vector2 smithScroll;
        float smithAnimAt = -99f; Item smithAnimItem;
        string smithExpr = "normal", smithLine; int smithHits;

        static void LoadSmith2D()
        {
            if (smithLoaded) return;
            smithLoaded = true;
            smithBg = Resources.Load<Texture2D>("Village2D/in_smith_flame");
            var fr = new List<Texture2D>();
            for (int i = 0; i < 6; i++) { var t = Resources.Load<Texture2D>("Village2D/anvil_" + i); if (t != null) fr.Add(t); }
            anvilFrames = fr.ToArray();
            var win = new List<string>(); var lose = new List<string>(); List<string> cur = null;
            var ta = Resources.Load<TextAsset>("Village2D/lines_smith");
            foreach (var raw in (ta != null ? ta.text : "").Split('\n'))
            {
                var l = raw.Trim();
                if (l == "[success]") cur = win; else if (l == "[failure]") cur = lose; else if (l.Length > 0) cur?.Add(l);
            }
            smithWin = win.Count > 0 ? win.ToArray() : new[] { "哼哼，看到沒？這火候，全村只有我抓得準！" };
            smithLose = lose.Count > 0 ? lose.ToArray() : new[] { "呃……這塊鐵今天心情不好。不是我的問題喔！" };
        }

        /// <summary>有素材就畫 2D 鐵匠鋪（回傳 true）</summary>
        bool DrawSmith2D(CharacterProfile prof, int fs, int fsS, int fsL)
        {
            LoadSmith2D();
            if (smithBg == null) return false;
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            var safe = UnityEngine.Screen.safeArea;
            float top = H - safe.yMax, bot = H - safe.y, sx = W / 1080f, sy = (bot - top) / 1920f;
            Rect D(float x, float y, float w, float h) => new Rect(x * sx, top + y * sy, w * sx, h * sy);
            GUI.DrawTexture(new Rect(0, 0, W, H), smithBg, ScaleMode.ScaleAndCrop);
            string npcId = town == 0 ? "xiaotie" : "smith";
            string npcName = town == 0 ? "小鐵" : "鐵匠師父";
            bool anim = Time.time - smithAnimAt < SmithAnimTime;
            if (!anim && smithAnimItem != null) FinishSmithAnim(prof);

            // 標題列：鐵匠鋪、金幣
            var hr = D(30, 45, 1020, 115);
            UiKit.Card(hr, 0.92f, new Color(1f, 0.6f, 0.3f), 0.8f);
            UiKit.Text(new Rect(hr.x + fs * 0.6f, hr.y, hr.width * 0.5f, hr.height), $"⚒ 鐵匠鋪・{Towns[town].name}", fsL, Color.white, TextAnchor.MiddleLeft, true);
            float rx = SettingsMenu.RightEdge(hr.y, hr.yMax, hr.xMax - fs * 0.6f);
            UiKit.Text(new Rect(hr.x + hr.width * 0.5f, hr.y, rx - hr.x - hr.width * 0.5f, hr.height), $"💰 {Economy.Gold}", fs, new Color(1f, 0.88f, 0.4f), TextAnchor.MiddleRight, true);

            // 左上：要強化的裝備＋直排列表（上下拖曳）；洗鍊中：同一塊換成洗鍊面板
            UiKit.Text(D(45, 195, 600, 80), rerollMode ? "🔁 洗鍊副屬性" : "要強化的裝備", fsL, Color.white, TextAnchor.MiddleLeft, true);
            var items = AllItems(prof);
            var view = D(30, 290, 610, 880);
            UiKit.Card(view, 0.55f, null, 0.5f);
            if (rerollMode && selItem != null && items.Contains(selItem)) { DrawRerollPanel(view, selItem, prof, fs, fsS); return true; }
            rerollMode = false;
            float rowH = 195f * sy, rowGap = 15f * sy;
            var content = new Rect(0, 0, view.width - 4f, Mathf.Max(view.height, items.Count * (rowH + rowGap) + rowGap));
            if (FocusUid != 0) { int fi = items.FindIndex(x => x.uid == FocusUid); if (fi >= 0) smithScroll.y = Mathf.Max(0f, fi * (rowH + rowGap) - rowGap); FocusUid = 0; }   // 新手指引：捲到要點的那一件
            smithScroll = UiKit.DragScroll(view, smithScroll, content);
            smithScroll = GUI.BeginScrollView(view, smithScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var rr = new Rect(15f * sx, rowGap + i * (rowH + rowGap), content.width - 30f * sx, rowH);
                if (rr.yMax < smithScroll.y || rr.y > smithScroll.y + view.height) continue;
                bool worn = System.Array.IndexOf(prof.equipped, it) >= 0;
                UiKit.NextTag("item:" + it.uid);
                if (UiKit.Button(rr, "", fsS, selItem == it, Item.RarityColors[it.rarity], !anim) && !anim) { selItem = it; smithExpr = "normal"; smithLine = it.plus >= Economy.MaxPlus ? "這件已經是 +10 了，再敲就要變成藝術品啦。" : "這件啊……交給我，保證比原本更亮！"; Sfx.Play("select", 0.5f); }
                UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.08f, rr.width - fs * 1.2f, rr.height * 0.42f), it.FullName, fs, Item.RarityColors[it.rarity], TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + fs * 0.6f, rr.y + rr.height * 0.48f, rr.width - fs * 1.2f, rr.height * 0.42f),
                    $"{(worn ? "👕 穿著中" : "未穿著")}　{Item.RarityNames[it.rarity]}　{(it.plus >= Economy.MaxPlus ? "已滿級" : $"→+{it.plus + 1}")}", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleLeft);
            }
            if (items.Count == 0) UiKit.Text(new Rect(0, 0, content.width, rowH), "沒有裝備可以強化\n（過關才拿得到裝備）", fsS, Color.gray, TextAnchor.MiddleCenter);
            GUI.EndScrollView();

            // 右上：強化按鈕（文字保留「⚒ 強化（」：新手指引用它找按鈕）
            var sel = selItem != null && items.Contains(selItem) ? selItem : null;
            var er = D(680, 205, 360, 185);
            int cost = sel != null ? Economy.EnhanceCost(sel) : 0;
            var (stone, sn) = sel != null ? Crafting.StoneFor(sel) : ("mat_refine", 1);
            bool maxed = sel != null && sel.plus >= Economy.MaxPlus, poor = sel != null && Economy.Gold < cost, noStone = sel != null && sn > 0 && Crafting.Have(stone) < sn;
            int enhScrolls = Economy.Count(Economy.ScrollEnh);
            bool useEnh = useEnhScroll && enhScrolls > 0 && sel != null && Economy.EnhanceRate(sel) < 1f;
            string el = sel == null ? "先選裝備" : maxed ? "已經是 +10" : poor ? $"金幣不夠\n（要 💰 {cost}）" : noStone ? $"{Crafting.MatName(stone)}不夠\n（要 {sn} 個，有 {Crafting.Have(stone)}）"
                : $"⚒ 強化（💰 {cost}{(sn > 0 ? "＋" + Crafting.MatName(stone) + "×" + sn : "・不用礦石")}・成功率 {Economy.EnhanceRate(sel, useEnh) * 100:0}%{(useEnh ? "📜" : "")}）";
            poor |= noStone;
            if (UiKit.Button(er, el, fs, sel != null && !maxed && !poor, new Color(1f, 0.55f, 0.2f), sel != null && !maxed && !poor && !anim))
            {
                smithAnimAt = Time.time; smithAnimItem = sel; smithAnimScroll = useEnh; smithExpr = "focus"; smithLine = useEnh ? "有卷軸墊著，這次穩一點——" : "看好了——"; smithHits = 0;
            }
            // 試煉商店的卷軸：強化卷（開關，一次一張 +10%）、洗鍊卷（換成洗鍊面板）
            bool canEnhScroll = enhScrolls > 0 && sel != null && !maxed && Economy.EnhanceRate(sel) < 1f;
            if (UiKit.Button(D(680, 400, 175, 70), $"📜 強化卷 {enhScrolls}{(useEnh ? " ✔" : "")}", Mathf.RoundToInt(fsS * 0.85f), useEnh, new Color(1f, 0.8f, 0.35f), canEnhScroll && !anim)) useEnhScroll = !useEnhScroll;
            int rerolls = Economy.Count(Economy.ScrollReroll);
            if (UiKit.Button(D(865, 400, 175, 70), $"🔁 洗鍊卷 {rerolls}", Mathf.RoundToInt(fsS * 0.85f), false, new Color(0.6f, 0.85f, 1f), rerolls > 0 && sel != null && ItemGen.SubCount(sel) > 0 && !anim)) { rerollMode = true; rerollLock = -1; smithExpr = "focus"; smithLine = "要洗哪一件？想留的那條先鎖起來。"; }
            // 強化前 → 強化後（只加主屬性）
            var sr = D(680, 480, 360, 230);
            UiKit.Card(sr, 0.85f, null, 0.5f);
            string st = "點左邊的裝備\n看強化後的數值";
            if (sel != null && sel.mods.Count > 0)
            {
                var m0 = sel.mods[0];
                string now = Item.AffixText(m0.Key, m0.Value * sel.PlusMul);
                string nxt = maxed ? "（已滿級）" : Item.AffixText(m0.Key, m0.Value * Economy.EnhanceMul(sel.plus + 1));
                st = $"強化前 → 強化後\n{now}\n→ {nxt}\n（只加主屬性，其他屬性不變）";
            }
            UiKit.Text(new Rect(sr.x + fs * 0.5f, sr.y + fs * 0.3f, sr.width - fs, sr.height - fs * 0.6f), st, fsS, Color.white, TextAnchor.UpperLeft, false, 0.5f);

            // 鐵匠：半身像、對話泡泡、鐵砧
            var tex = Story2D.Portrait(npcId, smithExpr) ?? Story2D.Portrait(npcId, "normal");
            if (tex != null) GUI.DrawTexture(D(660, 990, 390, 730), tex, ScaleMode.ScaleToFit, true);
            var br = D(675, 730, 365, 190);
            UiKit.Card(br, 0.9f, new Color(1f, 0.75f, 0.4f), 0.7f);
            UiKit.Text(new Rect(br.x + fs * 0.5f, br.y + fs * 0.3f, br.width - fs, br.height - fs * 0.6f), $"{npcName}：\n{smithLine ?? "火候到了，交給我吧！"}", fsS, Color.white, TextAnchor.UpperLeft, false, 0.5f);
            if (anvilFrames.Length > 0)
            {
                int f = 0;
                if (anim)
                {
                    float k = (Time.time - smithAnimAt) / SmithAnimTime * 3f;   // 3 輪
                    f = Mathf.Clamp(Mathf.FloorToInt((k - Mathf.Floor(k)) * anvilFrames.Length), 0, anvilFrames.Length - 1);
                    int hits = Mathf.Min(3, Mathf.FloorToInt(k + 0.5f));
                    if (hits > smithHits) { smithHits = hits; Sfx.Play("anvil", 0.8f, 0.9f + 0.1f * hits); Haptics.Play(Haptics.Kind.Light); }
                }
                GUI.DrawTexture(D(675, 1460, 365, 274), anvilFrames[f], ScaleMode.ScaleToFit, true);
            }

            // 精煉石合成、工匠（特殊裝備）（Crafting.cs）
            int ore = Crafting.Have("ore"), oreHi = Crafting.Have("ore_hi");
            if (UiKit.Button(D(40, 1190, 600, 88), $"🔷 合成精煉石（🪨鐵礦石 {ore}/{Crafting.OrePerStone}）　持有 {Crafting.Have("refine")}", fsS, false, new Color(0.45f, 0.75f, 1f), !anim && ore >= Crafting.OrePerStone))
            { Crafting.RefineStone(false, out var rm); Toast(rm); smithExpr = "proud"; smithLine = "礦石敲一敲、燒一燒——精煉石來了！"; Sfx.Play("anvil", 0.7f); }
            if (UiKit.Button(D(40, 1290, 600, 88), $"🔶 合成高級精煉石（💎高級鐵礦石 {oreHi}/{Crafting.OrePerStone}）　持有 {Crafting.Have("refine_hi")}", fsS, false, new Color(1f, 0.7f, 0.3f), !anim && oreHi >= Crafting.OrePerStone))
            { Crafting.RefineStone(true, out var rm2); Toast(rm2); smithExpr = "proud"; smithLine = "好礦！+8 以上就靠它了。"; Sfx.Play("anvil", 0.8f, 1.2f); }
            UiKit.Text(D(40, 1385, 600, 120), "+1～+3 不用礦石（每級 +4%）；+4～+7 精煉石（每級 +8%）；+8～+10 高級精煉石（每級 +12%，+10 要 2 個）。\n鐵礦石：岩石、大地類的敵人；高級鐵礦石：普通以上難度。", fsS, new Color(0.9f, 0.92f, 1f), TextAnchor.UpperLeft, false, 0.5f);
            if (UiKit.Button(D(270, 1795, 540, 92), "🏘 回城鎮", fs, false, new Color(1f, 0.55f, 0.2f), !anim)) { screen = Screen.Town; smithLine = null; smithExpr = "normal"; }
            return true;
        }

        bool useEnhScroll, smithAnimScroll, rerollMode; int rerollLock = -1;

        /// <summary>洗鍊面板（占用左邊的裝備列表）：列出副屬性，有鎖定卷可以鎖一條，確定就用一張洗鍊卷重洗其他的</summary>
        void DrawRerollPanel(Rect view, Item it, CharacterProfile prof, int fs, int fsS)
        {
            float gap = fs * 0.5f, y = view.y + gap;
            int locks = Economy.Count(Economy.ScrollLock), rerolls = Economy.Count(Economy.ScrollReroll);
            UiKit.Text(new Rect(view.x + gap, y, view.width - gap * 2, fs * 1.4f), it.FullName, fs, Item.RarityColors[it.rarity], TextAnchor.MiddleLeft, true);
            y += fs * 1.5f;
            UiKit.Text(new Rect(view.x + gap, y, view.width - gap * 2, fs * 2.4f), $"主屬性不變；下面的副屬性會全部重洗（數值照物品等級、稀有度）。\n🔁 洗鍊卷 {rerolls}　🔒 鎖定卷 {locks}（鎖一條不洗）", Mathf.RoundToInt(fsS * 0.9f), new Color(0.9f, 0.92f, 1f), TextAnchor.UpperLeft, false, 0.5f);
            y += fs * 2.6f;
            for (int i = 0; i < ItemGen.SubStart(it); i++) { UiKit.Text(new Rect(view.x + gap, y, view.width - gap * 2, fs * 1.2f), $"◆ {Item.AffixText(it.mods[i].Key, it.ModValue(i))}（不洗）", fsS, UiKit.Muted, TextAnchor.MiddleLeft); y += fs * 1.25f; }
            for (int i = ItemGen.SubStart(it); i < it.mods.Count; i++)
            {
                var rr = new Rect(view.x + gap, y, view.width - gap * 2, fs * 1.9f);
                bool locked = rerollLock == i;
                UiKit.Card(rr, locked ? 0.8f : 0.5f, locked ? new Color(1f, 0.8f, 0.35f) : null, locked ? 0.9f : 0.4f);
                UiKit.Text(new Rect(rr.x + gap * 0.5f, rr.y, rr.width * 0.62f, rr.height), $"{(locked ? "🔒 " : "◇ ")}{Item.AffixText(it.mods[i].Key, it.ModValue(i))}", fsS, Color.white, TextAnchor.MiddleLeft, locked);
                if (UiKit.Button(new Rect(rr.xMax - rr.width * 0.34f, rr.y + fs * 0.2f, rr.width * 0.33f, rr.height - fs * 0.4f), locked ? "取消鎖定" : "🔒 鎖定", Mathf.RoundToInt(fsS * 0.85f), locked, new Color(1f, 0.8f, 0.35f), locked || locks > 0)) rerollLock = locked ? -1 : i;
                y += fs * 2.1f;
            }
            float bw = (view.width - gap * 3) / 2f, bh = fs * 2.1f, by = view.yMax - gap - bh;
            if (UiKit.Button(new Rect(view.x + gap, by, bw, bh), "◀ 返回", fs)) { rerollMode = false; rerollLock = -1; }
            string bt = rerollLock >= 0 ? "🔁 洗鍊（用洗鍊卷＋鎖定卷）" : "🔁 洗鍊（用一張洗鍊卷）";
            if (UiKit.Button(new Rect(view.x + gap * 2 + bw, by, bw, bh), bt, fsS, true, new Color(0.6f, 0.85f, 1f), rerolls > 0 && (rerollLock < 0 || locks > 0)))
            {
                Economy.AddItem(Economy.ScrollReroll, -1);
                if (rerollLock >= 0) Economy.AddItem(Economy.ScrollLock, -1);
                ItemGen.RerollSubs(it, rng, rerollLock);
                Progress.SaveBag(); Progress.SaveChar(prof);
                rerollLock = -1;
                var l = new System.Collections.Generic.List<string>();
                for (int i = ItemGen.SubStart(it); i < it.mods.Count; i++) l.Add(Item.AffixText(it.mods[i].Key, it.ModValue(i)));
                Toast("🔁 洗好了：" + string.Join("、", l));
                smithExpr = "proud"; smithLine = "洗好了！不滿意再洗一次——卷軸夠的話。";
                Sfx.Play("anvil", 0.8f, 1.1f); Haptics.Play(Haptics.Kind.Success);
            }
        }

        /// <summary>鐵砧敲完：真正強化（成功率照原本）、鐵匠的表情與台詞</summary>
        void FinishSmithAnim(CharacterProfile prof)
        {
            var it = smithAnimItem; smithAnimItem = null;
            bool ok = Economy.Enhance(it, rng, smithAnimScroll, out var msg); smithAnimScroll = false;
            Toast(msg);
            Progress.SaveBag(); Progress.SaveChar(prof);
            bool paid = !msg.StartsWith("金幣不夠") && !msg.StartsWith("已經是");
            if (ok) { smithExpr = "proud"; smithLine = smithWin[rng.Next(smithWin.Length)]; Sfx.Play("charged", 0.7f, 1.2f); Haptics.Play(Haptics.Kind.Success); }
            else if (paid) { smithExpr = "awkward"; smithLine = smithLose[rng.Next(smithLose.Length)]; Sfx.Play("fail", 0.7f); Haptics.Play(Haptics.Kind.Warning); }
            else { smithExpr = "normal"; smithLine = msg; }
        }
    }
}
