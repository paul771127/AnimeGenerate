using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色畫面（無盡模式）：能力值、技能樹、裝備三個分頁。
    /// 由 SoloBattle 在選技能畫面或戰鬥中（點「角色」）呼叫 Draw；有改動時回傳 true（呼叫者重新套用到戰鬥中的角色）。
    /// </summary>
    public class CharacterPanel
    {
        public int tab;                 // 0 能力值、1 技能樹、2 裝備、3 進階技能樹（轉職後）
        string selNode;                 // 技能樹：選中的節點
        Item selItem;                   // 裝備：選中的物品（背包或身上）
        int page;
        string toast; float toastUntil;

        void Toast(string s) { toast = s; toastUntil = Time.time + 2f; }

        /// <summary>畫在 r 裡面；回傳這一幀有沒有改到角色</summary>
        public bool Draw(Rect r, CharacterProfile p)
        {
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.82f), fsL = Mathf.RoundToInt(fs * 1.2f);
            float gap = fs * 0.5f, lh = fs * 1.6f;
            var cd = Skills.Classes[p.cls];
            UiKit.Card(r, 0.95f, cd.color, 0.9f);
            float x = r.x + gap, w = r.width - gap * 2, y = r.y + gap;

            // ---- 標題：職業、等級、經驗、點數
            int cur = CharacterProfile.XpForLevel(p.level), next = CharacterProfile.XpForLevel(p.level + 1);
            var adv = p.Adv;
            UiKit.Text(new Rect(x, y, w, lh), $"{(adv != null ? $"{adv.name}（{cd.name}）" : cd.name)}　Lv {p.level}", fsL, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(x, y, w, lh), $"能力點 {p.statPoints}　技能點 {p.skillPoints}　職業樹 {p.BaseSpent}{(p.adv == null ? "/" + CharacterProfile.BaseTreeCap : "")}", fsS, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleRight, true);
            y += lh;
            UiKit.Bar(new Rect(x, y, w, lh * 0.5f), p.level >= CharacterProfile.MaxLevel ? 1f : (p.xp - cur) / (float)Mathf.Max(1, next - cur), new Color(0.75f, 0.45f, 1f),
                p.level >= CharacterProfile.MaxLevel ? "最高等級" : p.LevelLocked ? $"經驗已滿：到職業工會轉職才能升到 {p.level + 1} 級" : $"經驗 {p.xp - cur} / {next - cur}", fsS);
            y += lh * 0.5f + gap;
            // ---- 分頁
            string[] tabs = adv != null ? new[] { "📊 能力值", "🌳 技能樹", "🛡 裝備", "✨ 進階" } : new[] { "📊 能力值", "🌳 技能樹", "🛡 裝備" };
            if (tab >= tabs.Length) tab = 0;
            float tw = (w - gap * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                if (UiKit.Button(new Rect(x + i * (tw + gap), y, tw, lh * 1.1f), tabs[i], fsS, tab == i, i == 3 ? adv.color : cd.color)) { tab = i; selNode = null; selItem = null; }
            y += lh * 1.1f + gap;
            var body = new Rect(x, y, w, r.yMax - gap - y);
            bool changed = tab == 0 ? DrawStats(body, p, fs, fsS, gap) : tab == 1 ? DrawTree(body, p, fs, fsS, gap, p.cls) : tab == 3 ? DrawTree(body, p, fs, fsS, gap, adv.id) : DrawGear(body, p, fs, fsS, gap);
            if (Time.time < toastUntil) UiKit.Pill(r.y + r.height * 0.5f, toast, fs, new Color(1f, 0.85f, 0.5f), r.width * 0.9f, 0.9f);
            if (changed) Progress.SaveChar(p);
            return changed;
        }

        // ================================================================ 能力值
        bool DrawStats(Rect b, CharacterProfile p, int fs, int fsS, float gap)
        {
            bool changed = false;
            float rowH = Mathf.Min(fs * 3.0f, (b.height * 0.62f) / StatInfo.Count);
            var atk = StatInfo.AttackStat(p.cls);
            for (int i = 0; i < StatInfo.Count; i++)
            {
                var s = (Stat)i;
                var rr = new Rect(b.x, b.y + i * rowH, b.width, rowH - gap * 0.4f);
                UiKit.Card(rr, 0.5f, StatInfo.Colors[i], 0.4f);
                int own = p.OwnStat(s), tree = p.TreeStat(s), gear = p.GearStat(s), cost = StatInfo.Cost(own);
                string extra = (tree > 0 ? $" +{tree}樹" : "") + (gear > 0 ? $" +{gear}裝" : "");
                UiKit.Text(new Rect(rr.x + gap, rr.y, rr.width * 0.42f, rr.height * 0.55f), $"{StatInfo.Icons[i]} {StatInfo.Names[i]}　{p.Total(s)}", fs, StatInfo.Colors[i], TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + rr.width * 0.42f, rr.y, rr.width * 0.36f, rr.height * 0.55f), $"初始{p.BaseStat(s)} +加{p.alloc[i]}{extra}", fsS, new Color(0.8f, 0.82f, 0.9f), TextAnchor.MiddleLeft);
                UiKit.Text(new Rect(rr.x + gap, rr.y + rr.height * 0.5f, rr.width * 0.78f, rr.height * 0.5f), StatInfo.Effect(s, s == atk), fsS, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleLeft, false, 0.6f);
                var br = new Rect(rr.xMax - rr.width * 0.2f, rr.y + rr.height * 0.12f, rr.width * 0.19f, rr.height * 0.76f);
                if (UiKit.Button(br, $"＋\n{cost} 點", fsS, false, StatInfo.Colors[i], p.CanRaise(s)) && p.Raise(s)) { changed = true; Sfx.Play("select"); }
            }
            // 換算後的戰鬥數值
            float y = b.y + StatInfo.Count * rowH + gap * 0.5f;
            var adv = p.Adv;
            var f = new Fighter("preview", Skills.Classes[p.cls], new string[0], true);
            p.ApplyTo(f, Skills.Classes[p.cls], p.KnownSkills());
            string sum = $"HP {f.maxHp}　MP {f.maxMp}（每秒 +{f.regen:0.#}）　攻擊 ×{f.atkMul:0.00}\n" +
                         $"蓄力 ×{f.chargeScale:0.00}　冷卻 ×{f.cdMul:0.00}　爆擊 {f.critChance * 100:0.#}%　減傷 {f.gearReduce * 100:0.#}%" +
                         (f.lifesteal > 0 ? $"　吸血 {f.lifesteal * 100:0.#}%" : "") + (f.hpRegen > 0 ? $"　回血 {f.hpRegen:0.#}/s" : "") +
                         (f.mpOnHit > 0 ? $"　命中回 MP {f.mpOnHit:0}" : "") +
                         (f.evadeChance > 0 ? $"　閃避 {f.evadeChance * 100:0.#}%" : "") + (f.healMul != 1f ? $"　治療 ×{f.healMul:0.0#}" : "") +
                         (adv != null ? $"\n✨ {adv.name}：{adv.core}" : "");
            UiKit.Text(new Rect(b.x, y, b.width, b.yMax - y - fs * 2.2f), sum, fsS, new Color(1f, 0.95f, 0.75f), TextAnchor.UpperLeft, false, 0.55f);
            if (UiKit.Button(new Rect(b.x + b.width * 0.55f, b.yMax - fs * 2f, b.width * 0.45f, fs * 1.9f), "↺ 重新分配所有點數", fsS, false, new Color(1f, 0.5f, 0.45f)))
            { p.ResetPoints(); changed = true; Toast("能力點與技能點都退回了"); }
            UiKit.Text(new Rect(b.x, b.yMax - fs * 2f, b.width * 0.53f, fs * 1.9f), "能力值越高，再加需要越多點（每 10 點 +1）", fsS, new Color(0.75f, 0.78f, 0.88f), TextAnchor.MiddleLeft, false, 0.55f);
            return changed;
        }

        // ================================================================ 技能樹
        /// <summary>樹狀圖（v2）：7 欄格子，節點之間畫連線；點過的路線亮起來</summary>
        void DrawGraph(Rect area, CharacterProfile p, int fs, int fsS, float gap, string treeId, List<TreeNode> nodes, Color tcol)
        {
            var schools = SkillTrees.Schools(treeId);
            float headH = schools != null ? fs * 1.3f : 0f;
            if (schools != null)
            {
                float sw = area.width / 3f;
                for (int i = 0; i < 3; i++) UiKit.Text(new Rect(area.x + i * sw, area.y, sw, headH), schools[i], fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter, true);
            }
            int rows = 0; foreach (var n in nodes) rows = Mathf.Max(rows, n.row + 1);
            float colW = area.width / 7f, rowH = (area.height - headH) / Mathf.Max(1, rows);
            float nw = colW * 0.94f, nh = Mathf.Min(fs * 2.3f, rowH * 0.74f);
            Vector2 Center(TreeNode n) => new Vector2(area.x + (n.col + 0.5f) * colW, area.y + headH + (n.row + 0.5f) * rowH);
            // 連線（父 → 子）
            foreach (var n in nodes)
            {
                if (n.req == null) continue;
                var par = nodes.Find(x => x.id == n.req);
                if (par == null) continue;
                bool lit = p.Rank(par.id) > 0 && p.Rank(n.id) > 0, open = p.Rank(par.id) > 0;
                Line(Center(par) + new Vector2(0f, nh * 0.5f), Center(n) - new Vector2(0f, nh * 0.5f), Mathf.Max(2f, fs * 0.12f),
                    lit ? new Color(1f, 0.85f, 0.3f, 0.95f) : open ? new Color(0.7f, 0.85f, 1f, 0.7f) : new Color(0.45f, 0.45f, 0.5f, 0.6f));
            }
            foreach (var n in nodes)
            {
                var c = Center(n);
                var nr = new Rect(c.x - nw / 2f, c.y - nh / 2f, nw, nh);
                int rank = p.Rank(n.id);
                string block = p.NodeBlock(n);
                bool locked = rank == 0 && block != null && block != "技能點不足";
                var col = rank >= n.max ? new Color(1f, 0.85f, 0.3f) : rank > 0 ? new Color(0.5f, 1f, 0.6f) : locked ? new Color(0.5f, 0.5f, 0.55f) : tcol;
                if (UiKit.Button(nr, "", fsS, selNode == n.id || rank > 0, col)) selNode = n.id;
                int fn = Mathf.RoundToInt(fsS * 0.82f);
                UiKit.Text(new Rect(nr.x + 2, nr.y + 1, nr.width - 4, nr.height * 0.58f), n.name.Replace("習得：", "📘"), fn, locked ? new Color(0.6f, 0.6f, 0.65f) : Color.white, TextAnchor.MiddleCenter, true, 0.4f);
                UiKit.Text(new Rect(nr.x + 2, nr.y + nr.height * 0.55f, nr.width - 4, nr.height * 0.42f), $"{rank}/{n.max}", fn, col, TextAnchor.MiddleCenter);
            }
        }

        static void Line(Vector2 a, Vector2 b, float w, Color c)
        {
            var d = b - a; float len = d.magnitude; if (len < 1f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var m = GUI.matrix; var oc = GUI.color;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y - w / 2f, len, w), Texture2D.whiteTexture);
            GUI.matrix = m; GUI.color = oc;
        }

        bool DrawTree(Rect b, CharacterProfile p, int fs, int fsS, float gap, string treeId)
        {
            bool changed = false;
            var nodes = SkillTrees.Of(treeId);
            if (nodes.Count > 0 && nodes[0].free)
            {
                var advG = Advanced.Get(treeId);
                var tc = advG != null ? advG.color : Skills.Classes.TryGetValue(treeId, out var tcd) ? tcd.color : Color.white;
                DrawGraph(new Rect(b.x, b.y, b.width, b.height * 0.62f), p, fs, fsS, gap, treeId, nodes, tc);
                return DrawNodeDetail(b, p, fs, fsS, gap, treeId, advG);
            }
            var advT = Advanced.Get(treeId);
            int rows = 0; foreach (var n in nodes) rows = Mathf.Max(rows, n.row + 1);
            string[] heads = { "⚔ 強化技能", "✨ 新技能", "💠 能力／特殊" };
            float colW = (b.width - gap * 2) / 3f, headH = fs * 1.4f;
            float areaH = b.height * 0.62f - headH, nodeH = Mathf.Min(fs * 3.2f, areaH / Mathf.Max(1, rows) - gap * 0.5f);
            for (int c = 0; c < 3; c++) UiKit.Text(new Rect(b.x + c * (colW + gap), b.y, colW, headH), heads[c], fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter, true);
            var cd = Skills.Classes[p.cls];
            var tcol = advT != null ? advT.color : cd.color;
            foreach (var n in nodes)
            {
                var nr = new Rect(b.x + n.col * (colW + gap), b.y + headH + n.row * (nodeH + gap * 0.5f), colW, nodeH);
                int rank = p.Rank(n.id);
                string block = p.NodeBlock(n);
                bool locked = rank == 0 && block != null && block != "技能點不足";
                var col = rank >= n.max ? new Color(1f, 0.85f, 0.3f) : rank > 0 ? new Color(0.5f, 1f, 0.6f) : locked ? new Color(0.5f, 0.5f, 0.55f) : tcol;
                if (UiKit.Button(nr, "", fsS, selNode == n.id || rank > 0, col)) selNode = n.id;
                UiKit.Text(new Rect(nr.x + 4, nr.y + 2, nr.width - 8, nr.height * 0.58f), n.name, fsS, locked ? new Color(0.6f, 0.6f, 0.65f) : Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(nr.x + 4, nr.y + nr.height * 0.55f, nr.width - 8, nr.height * 0.4f), n.max > 1 ? $"{rank}/{n.max}" : rank > 0 ? "已學會" : locked ? "🔒" : "未學", fsS, col, TextAnchor.MiddleCenter);
            }
            return DrawNodeDetail(b, p, fs, fsS, gap, treeId, advT);
        }

        /// <summary>選中的節點：說明＋學習按鈕（下方 36%）</summary>
        bool DrawNodeDetail(Rect b, CharacterProfile p, int fs, int fsS, float gap, string treeId, AdvDef advT)
        {
            bool changed = false;
            float dy = b.y + b.height * 0.64f;
            var dr = new Rect(b.x, dy, b.width, b.yMax - dy);
            UiKit.Card(dr, 0.6f);
            var sel = selNode != null ? SkillTrees.Find(treeId, selNode) : null;
            if (sel == null)
            {
                string help = advT != null
                    ? $"✨ {advT.name}的進階技能樹（和基本技能樹共用技能點）。轉職時已學會「{(Skills.All.TryGetValue(advT.skills[0], out var s0) ? s0.name : advT.skills[0])}」；📘 可以習得另外兩個進階技能。\n三個流派：{string.Join("、", SkillTrees.Schools(advT.id) ?? new string[0])}，有些節點會強化基本職業的流派（例如反擊之刃、連斬、過載、聖印）。核心機制：{advT.core}"
                    : $"點一個節點看說明。樹從最上面往下長：父節點點過 1 級，下面接著的節點才能點。每棵樹 40 點，轉職前最多點 20 點，要選好流派。\n三個流派：{string.Join("、", SkillTrees.Schools(p.cls) ?? new string[0])}。一開始只會 3 個基本技能：{string.Join("、", NamesOf(SkillTrees.Starters(p.cls)))}；📘＝習得新技能。";
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap, dr.width - gap * 2, dr.height - gap * 2), help, fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperLeft);
                return changed;
            }
            int rk = p.Rank(sel.id);
            var pre = SkillTrees.Prereqs(treeId, sel);
            string req = pre.Count == 0 ? "前置：無（可以直接點）" : (sel.free ? "前置（要先點過）：" : "前置（要先點滿）：") + string.Join("、", pre.ConvertAll(r => $"{r.name} {p.Rank(r.id)}/{r.max}"));
            string now = rk > 0 ? SkillTrees.Describe(sel, rk) : "（還沒學）";
            string nxt = rk < sel.max ? SkillTrees.Describe(sel, rk + 1) : "（已滿級）";
            string extra = sel.kind == NodeKind.Learn && Skills.All.TryGetValue(sel.skill, out var sk) ? $"\n{sk.desc}" : "";
            UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.5f, dr.width - gap * 2, fs * 1.4f), $"{sel.name}（{rk}/{sel.max}）", fs, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.5f + fs * 1.4f, dr.width - gap * 2, dr.height - fs * 4.4f), $"{req}\n現在：{now}\n下一級：{nxt}{extra}", fsS, new Color(0.85f, 0.9f, 1f), TextAnchor.UpperLeft, false, 0.55f);
            string blk = p.NodeBlock(sel);
            if (UiKit.Button(new Rect(dr.x + dr.width * 0.3f, dr.yMax - fs * 2.3f, dr.width * 0.4f, fs * 2f), blk ?? "學習（1 技能點）", fsS, blk == null, new Color(0.5f, 1f, 0.6f), blk == null))
            {
                if (p.Learn(sel)) { changed = true; Sfx.Play("charged"); Toast($"學會：{sel.name}"); }
            }
            return changed;
        }

        static IEnumerable<string> NamesOf(string[] ids) { foreach (var id in ids) yield return Skills.All.TryGetValue(id, out var s) ? s.name : id; }

        // ================================================================ 裝備
        bool DrawGear(Rect b, CharacterProfile p, int fs, int fsS, float gap)
        {
            bool changed = false;
            // 身上的 5 個欄位
            float sh = fs * 1.9f;
            for (int i = 0; i < 5; i++)
            {
                var it = p.equipped[i];
                var sr = new Rect(b.x, b.y + i * (sh + gap * 0.3f), b.width, sh);
                if (UiKit.Button(sr, "", fsS, selItem != null && selItem == it, it != null ? Item.RarityColors[it.rarity] : (Color?)null)) selItem = it;
                UiKit.Text(new Rect(sr.x + gap, sr.y, sr.width * 0.2f, sr.height), Item.SlotNames[i], fsS, new Color(0.8f, 0.82f, 0.9f), TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(sr.x + sr.width * 0.2f, sr.y, sr.width * 0.78f, sr.height), it != null ? $"[{Item.RarityNames[it.rarity]}] {it.FullName}" : "（空）", fsS, it != null ? Item.RarityColors[it.rarity] : Color.gray, TextAnchor.MiddleLeft, true);
            }
            // 背包（分頁，3×4）
            float by = b.y + 5 * (sh + gap * 0.3f) + gap * 0.5f;
            var bag = Progress.Bag;
            int per = 12, pages = Mathf.Max(1, (bag.Count + per - 1) / per);
            page = Mathf.Clamp(page, 0, pages - 1);
            UiKit.Text(new Rect(b.x, by, b.width * 0.6f, fs * 1.3f), $"背包 {bag.Count}/{Progress.BagSize}", fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true);
            if (pages > 1)
            {
                if (UiKit.Button(new Rect(b.xMax - fs * 6.5f, by, fs * 3f, fs * 1.3f), "◀", fsS, false, null, page > 0)) page--;
                if (UiKit.Button(new Rect(b.xMax - fs * 3.2f, by, fs * 3f, fs * 1.3f), "▶", fsS, false, null, page < pages - 1)) page++;
            }
            by += fs * 1.4f;
            float cw = (b.width - gap * 2) / 3f, ch = fs * 2.2f;
            for (int k = 0; k < per; k++)
            {
                int idx = page * per + k;
                if (idx >= bag.Count) break;
                var it = bag[idx];
                var cr = new Rect(b.x + (k % 3) * (cw + gap), by + (k / 3) * (ch + gap * 0.3f), cw, ch);
                bool ok = p.CanEquip(it, out _);
                if (UiKit.Button(cr, "", fsS, selItem == it, Item.RarityColors[it.rarity])) selItem = it;
                UiKit.Text(new Rect(cr.x + 3, cr.y + 1, cr.width - 6, cr.height * 0.55f), it.FullName, fsS, ok ? Item.RarityColors[it.rarity] : Item.RarityColors[it.rarity] * 0.6f, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(cr.x + 3, cr.y + cr.height * 0.5f, cr.width - 6, cr.height * 0.45f), $"{Item.SlotNames[(int)it.slot]}{(ok ? "" : "・不能用")}", fsS, ok ? new Color(0.8f, 0.85f, 0.95f) : new Color(1f, 0.5f, 0.45f), TextAnchor.MiddleCenter);
            }
            // 選中物品的說明
            float dy = by + 4 * (ch + gap * 0.3f) + gap * 0.3f;
            var dr = new Rect(b.x, dy, b.width, b.yMax - dy);
            UiKit.Card(dr, 0.6f, selItem != null ? Item.RarityColors[selItem.rarity] : (Color?)null, 0.5f);
            if (selItem == null)
            {
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap, dr.width - gap * 2, dr.height - gap * 2), "點裝備看說明。無盡模式打倒小怪有機會掉裝備，Boss 一定掉稀有以上。稀有度：普通→優良→稀有→史詩→傳說，越高越強、限制越嚴。", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperLeft);
                return changed;
            }
            var s2 = selItem;
            bool worn = System.Array.IndexOf(p.equipped, s2) >= 0;
            bool can = p.CanEquip(s2, out var why);
            var lines = new List<string>
            {
                $"{Item.SlotNames[(int)s2.slot]}・物品等級 {s2.ilvl}　限定：{s2.ClassText()}",
                $"需求：等級 {s2.reqLevel}" + (s2.reqValue > 0 ? $"、{StatInfo.Names[(int)s2.reqStat]} {s2.reqValue}" : "") + (worn || can ? "" : $"　❌ {why}"),
            };
            foreach (var kv in s2.mods) lines.Add("・" + Item.AffixText(kv.Key, kv.Value * s2.PlusMul));
            var sd = Economy.SetOf(s2.setId);
            if (sd != null) lines.Add(Economy.SetText(sd, p.equipped));
            var on = p.equipped[(int)s2.slot];
            if (!worn && on != null) lines.Add($"（目前穿著：{on.name}）");
            UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.4f, dr.width - gap * 2, fs * 1.3f), $"[{Item.RarityNames[s2.rarity]}] {s2.FullName}", fs, Item.RarityColors[s2.rarity], TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.4f + fs * 1.3f, dr.width - gap * 2, dr.height - fs * 4f), string.Join("\n", lines), fsS, new Color(0.88f, 0.9f, 1f), TextAnchor.UpperLeft, false, 0.5f);
            float bw = (dr.width - gap * 3) / 2f, bh = fs * 1.9f, byy = dr.yMax - bh - gap * 0.4f;
            if (worn)
            {
                if (UiKit.Button(new Rect(dr.x + gap, byy, bw, bh), "卸下", fsS) && Progress.Unequip(p, s2.slot)) { changed = true; selItem = null; }
            }
            else
            {
                if (UiKit.Button(new Rect(dr.x + gap, byy, bw, bh), can ? "裝備" : "不能裝備", fsS, can, Item.RarityColors[s2.rarity], can))
                {
                    if (Progress.Equip(p, s2, out var w2)) { changed = true; Sfx.Play("block", 0.7f, 1.3f); Toast($"裝備：{s2.name}"); selItem = null; }
                    else Toast(w2);
                }
                if (UiKit.Button(new Rect(dr.x + gap * 2 + bw, byy, bw, bh), "丟掉", fsS, false, new Color(1f, 0.45f, 0.4f))) { Progress.Discard(s2); selItem = null; }
            }
            return changed;
        }
    }
}
