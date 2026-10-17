using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色畫面的「技能樹」與「進階」分頁（介面設計交給美術：見 SKILLTREE_UI_BRIEF.md）。
    /// 入口：DrawTree(b, p, fs, fsS, gap, treeId) → 回傳這一幀有沒有點了技能（呼叫者存檔）。
    /// 只能透過 CharacterProfile（Rank／NodeBlock／Learn）改資料；不要自己改 ranks 或技能點。
    /// </summary>
    public partial class CharacterPanel
    {
        // ================================================================ 技能樹
        /// <summary>樹狀圖（v2）：7 欄格子，節點之間畫連線；點過的路線亮起來</summary>
        void DrawGraph(Rect area, CharacterProfile p, int fs, int fsS, float gap, string treeId, List<TreeNode> nodes, Color tcol)
        {
            // 不標示流派：怎麼搭配由玩家自己摸索
            float headH = 0f;
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
                    ? $"✨ {advT.name}的進階技能樹（和基本技能樹共用技能點）。轉職時已學會「{(Skills.All.TryGetValue(advT.skills[0], out var s0) ? s0.name : advT.skills[0])}」；📘 可以習得另外兩個進階技能。有些節點也會強化基本職業的技能，自由搭配。\n核心機制：{advT.core}"
                    : $"點一個節點看說明。樹從最上面往下長：父節點點過 1 級，下面接著的節點才能點。整棵樹的點數比轉職前能拿到的多，轉職前最多點 {CharacterProfile.BaseTreeCap} 點，怎麼點由你決定。\n一開始只會 3 個基本技能：{string.Join("、", NamesOf(SkillTrees.Starters(p.cls)))}；📘＝習得新技能。";
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
    }
}
