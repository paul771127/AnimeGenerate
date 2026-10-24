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
        Vector2 treeScroll, detailScroll;
        string detailKey;
        string visibleTree;
        readonly Dictionary<string,int> seenRanks = new Dictionary<string,int>();
        readonly Dictionary<string,float> rankFlash = new Dictionary<string,float>();
        // All state below is transient presentation state; Rank/NodeBlock remain authoritative.
        void DrawGraph(Rect area, CharacterProfile p, int fs, int fsS, float gap, string treeId, List<TreeNode> nodes, Color tcol)
        {
            if(visibleTree!=treeId){visibleTree=treeId;treeScroll=Vector2.zero;seenRanks.Clear();rankFlash.Clear();}
            // A single growing tree: no school names, lane banners or recommended builds.
            float headH=fs*.35f,footerH=fs*2.35f;
            int rows=0;foreach(var n in nodes)rows=Mathf.Max(rows,n.row+1);
            float contentW=Mathf.Max(area.width-fs*.85f,fs*2.3f*7),colW=contentW/7,rowH=fs*4.4f;
            float nw=Mathf.Max(fs*2.2f,colW*.86f),nh=fs*2.35f;
            var viewport=new Rect(area.x,area.y+headH+gap*.3f,area.width,Mathf.Max(fs*3,area.height-headH-footerH-gap*.3f));
            var bounds=new Rect(0,0,contentW,Mathf.Max(viewport.height,rows*rowH+gap));
            treeScroll.y=Mathf.Clamp(treeScroll.y,0,Mathf.Max(0,bounds.height-viewport.height));
            treeScroll=UiKit.DragScroll(viewport,treeScroll,bounds);   // 手指拖曳捲動
            treeScroll=GUI.BeginScrollView(viewport,treeScroll,bounds,false,true);
            UiKit.Card(new Rect(0,0,contentW,bounds.height),.38f,tcol,.25f);
            Vector2 Center(TreeNode n)=>new Vector2((n.col+.5f)*colW,(n.row+.5f)*rowH);
            foreach(var n in nodes)
            {
                if(n.req==null)continue;var par=nodes.Find(x=>x.id==n.req);if(par==null)continue;
                bool lit=p.Rank(par.id)>0&&p.Rank(n.id)>0,open=p.Rank(par.id)>0;
                var ac=lit?UiKit.Gold:open?Color.Lerp(tcol,Color.white,.45f):new Color(.34f,.39f,.49f,.65f);
                var start=Center(par)+new Vector2(0,nh*.5f);var end=Center(n)-new Vector2(0,nh*.5f);
                var bend=new Vector2(start.x,(start.y+end.y)*.5f);var bend2=new Vector2(end.x,bend.y);
                Line(start,bend,Mathf.Max(2,fs*.09f),ac);Line(bend,bend2,Mathf.Max(2,fs*.09f),ac);Line(bend2,end,Mathf.Max(2,fs*.09f),ac);
                if(rankFlash.TryGetValue(n.id,out var flash)&&Time.time-flash<.3f)Line(start,end,fs*.13f,new Color(1,.95f,.65f,1-(Time.time-flash)/.3f));
            }
            foreach(var n in nodes)
            {
                var c=Center(n);var nr=new Rect(c.x-nw*.5f,c.y-nh*.5f,nw,nh);
                int rank=p.Rank(n.id);string block=p.NodeBlock(n);
                bool locked=rank==0&&block!=null&&block!="技能點不足";
                var col=rank>=n.max?UiKit.Gold:rank>0?new Color(.5f,1,.68f):locked?new Color(.45f,.49f,.57f):tcol;
                if(seenRanks.TryGetValue(n.id,out var before)&&rank>before)rankFlash[n.id]=Time.time;
                seenRanks[n.id]=rank;
                UiKit.NextTag("node:" + n.id);
                if (UiKit.Button(nr, "", fsS, selNode == n.id || rank > 0, col)) selNode = n.id;
                float side=n.kind==NodeKind.Learn?nh*.92f:n.kind==NodeKind.Stat?nh*.69f:nh*.83f;
                var iconRect=new Rect(c.x-side*.5f,c.y-side*.5f,side,side);
                bool pulse=block==null;float glow=pulse?.75f+.2f*Mathf.Sin(Time.time*3):1;
                if(rankFlash.TryGetValue(n.id,out var at)&&Time.time-at<.3f)glow=1;
                Texture2D icon=null;if((n.kind==NodeKind.Learn||n.kind==NodeKind.Mod)&&!string.IsNullOrEmpty(n.skill))icon=SkillIcons.Get(n.skill);
                if(n.kind==NodeKind.Mod)
                {
                    var oldColor=GUI.color;
                    GUI.color=locked?new Color(.4f,.4f,.4f,1):Color.white;
                    if(icon!=null)GUI.DrawTexture(iconRect,icon,ScaleMode.ScaleToFit,true);
                    GUI.color=oldColor;
                    UiKit.Text(new Rect(nr.xMax-fs*.65f,nr.y,fs*.7f,fs*.8f),"＋",fsS,UiKit.Gold,TextAnchor.MiddleCenter,true);
                }
                else
                {
                    var typeCol=n.kind==NodeKind.Stat?StatInfo.Colors[(int)n.stat]:n.kind==NodeKind.Passive?UiKit.Gold:col;
                    if(locked)typeCol=UiKit.Muted;
                    UiKit.Badge(iconRect,icon,new Color(typeCol.r,typeCol.g,typeCol.b,glow),n.kind==NodeKind.Passive?1:0,locked?.4f:1);
                    if(icon==null)
                    {
                        if(n.kind==NodeKind.Stat)UiKit.StatGlyph(iconRect,(int)n.stat,typeCol);
                        else UiKit.Star(new Rect(iconRect.x+side*.32f,iconRect.y+side*.32f,side*.36f,side*.36f),typeCol);
                    }
                }
                if(locked)UiKit.Lock(new Rect(nr.xMax-fs*.48f,nr.yMax-fs*.52f,fs*.35f,fs*.38f),UiKit.Muted);
                if(rankFlash.TryGetValue(n.id,out var learnedAt)&&Time.time-learnedAt<.3f)
                {
                    float fade=1-(Time.time-learnedAt)/.3f;
                    UiKit.Halo(new Rect(nr.x-4,nr.y-4,nr.width+8,nr.height+8),Color.white,fade);
                }
                UiKit.Text(new Rect(nr.x-gap*.15f,nr.yMax+2,nr.width+gap*.3f,fs*1.05f),n.name.Replace("習得：",""),fsS,locked?UiKit.Muted:UiKit.Paper,TextAnchor.MiddleCenter,true,.65f);
                float pipW=Mathf.Min(fs*.22f,(nw-gap)/Mathf.Max(1,n.max));
                for(int k=0;k<n.max;k++)UiKit.Gem(new Rect(c.x-(n.max*(pipW+3)-3)*.5f+k*(pipW+3),nr.yMax+fs*1.12f,pipW,pipW),k<rank?UiKit.Gold:new Color(.29f,.35f,.45f));
                UiKit.Text(new Rect(nr.x,nr.y-fs*.55f,nr.width,fs*.55f),$"{rank}/{n.max}",Mathf.RoundToInt(fsS*.7f),col,TextAnchor.MiddleCenter);
            }
            GUI.EndScrollView();
            float fy=area.yMax-footerH;UiKit.Text(new Rect(area.x,fy,area.width-fs*4.8f,footerH),"點選節點查看效果・上下捲動",fsS,UiKit.Muted);
            if(UiKit.Button(new Rect(area.xMax-fs*4.6f,fy,fs*2.2f,fs*2.2f),"▲",fsS,false,tcol,treeScroll.y>0))treeScroll.y=Mathf.Max(0,treeScroll.y-rowH*1.5f);
            if(UiKit.Button(new Rect(area.xMax-fs*2.2f,fy,fs*2.2f,fs*2.2f),"▼",fsS,false,tcol,treeScroll.y<bounds.height-viewport.height))treeScroll.y=Mathf.Min(bounds.height-viewport.height,treeScroll.y+rowH*1.5f);
        }

        void DrawTreeParagraph(Rect viewport,string key,string text,int fs,Color color)
        {
            if(detailKey!=key){detailKey=key;detailScroll=Vector2.zero;}
            float width=viewport.width-fs*.85f;
            var content=new Rect(0,0,width,Mathf.Max(viewport.height,UiKit.TextHeight(text,width,fs)+4));
            detailScroll=UiKit.DragScroll(viewport,detailScroll,content);
            detailScroll=GUI.BeginScrollView(viewport,detailScroll,content,false,true);
            UiKit.Text(content,text,fs,color,TextAnchor.UpperLeft,false,1f);
            GUI.EndScrollView();
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
                UiKit.NextTag("node:" + n.id);
                if (UiKit.Button(nr, "", fsS, selNode == n.id || rank > 0, col)) selNode = n.id;
                UiKit.Text(new Rect(nr.x + 4, nr.y + 2, nr.width - 8, nr.height * 0.58f), n.name, fsS, locked ? new Color(0.6f, 0.6f, 0.65f) : Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(nr.x + 4, nr.y + nr.height * 0.55f, nr.width - 8, nr.height * 0.4f), n.max > 1 ? $"{rank}/{n.max}" : rank > 0 ? "已學會" : locked ? "🔒" : "未學", fsS, col, TextAnchor.MiddleCenter);
            }
            return DrawNodeDetail(b, p, fs, fsS, gap, treeId, advT);
        }

        /// <summary>初心者技能樹的說明：3 招各 5 級、學會才能點；任務進度；轉職時退點</summary>
        static string NoviceHelp(CharacterProfile p)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("🌱 初心者的技能樹：木棍揮擊、投石、急救各可以強化 5 級（每升一級得 1 技能點）。招式要先到「炎之里」找居民解任務學會，才能點：\n");
            foreach (var q in NoviceQuests.All)
                sb.Append($"・{Skills.All[q.skill].name}（{q.npc}「{q.title}」）：{NoviceQuests.Progress(q, p, Story.IsCleared)}\n");
            sb.Append($"這棵樹只能用 1～{Skills.JobLevel} 級拿到的技能點（剩 {p.AvailIn(Skills.Novice)} 點）。轉職後這棵樹會保留、不能重置，點數也不能拿去點新職業的技能樹。");
            return sb.ToString();
        }

        /// <summary>選中的節點：說明＋學習按鈕（下方 36%）</summary>
        bool DrawNodeDetail(Rect b, CharacterProfile p, int fs, int fsS, float gap, string treeId, AdvDef advT)
        {
            bool changed = false;
            float dy = b.y + b.height * 0.64f;
            var dr = new Rect(b.x, dy, b.width, b.yMax - dy);
            UiKit.Card(dr,.94f,advT!=null?advT.color:Skills.Classes[p.cls].color,.75f);
            var sel = selNode != null ? SkillTrees.Find(treeId, selNode) : null;
            if (sel == null)
            {
                string help = treeId == Skills.Novice ? NoviceHelp(p) : advT != null
                    ? $"✨ {advT.name}的進階技能樹（和基本技能樹共用技能點）。轉職時已學會「{(Skills.All.TryGetValue(advT.skills[0], out var s0) ? s0.name : advT.skills[0])}」；📘 可以習得另外兩個進階技能。有些節點也會強化基本職業的技能，自由搭配。\n核心機制：{advT.core}"
                    : $"點一個節點看說明。樹從最上面往下長：父節點點過 1 級，下面接著的節點才能點。整棵樹的點數比能拿到的多，怎麼點由你決定。這棵樹只能用 {(p.novicePast ? Skills.JobLevel + 1 : 1)}～{Advanced.Level} 級拿到的技能點（目前剩 {p.AvailIn(treeId)} 點；初心者、進階職業的點數各自分開，不能挪）。\n一開始只會 3 個基本技能：{string.Join("、", NamesOf(SkillTrees.Starters(p.cls)))}；📘＝習得新技能。";
                DrawTreeParagraph(new Rect(dr.x+gap,dr.y+gap,dr.width-gap*2,dr.height-gap*2),treeId+" help",help,fsS,UiKit.Muted);
                return changed;
            }
            int rk = p.Rank(sel.id);
            var pre = SkillTrees.Prereqs(treeId, sel);
            string req = pre.Count == 0 ? "前置：無（可以直接點）" : (sel.free ? "前置（要先點過）：" : "前置（要先點滿）：") + string.Join("、", pre.ConvertAll(r => $"{r.name} {p.Rank(r.id)}/{r.max}"));
            string now = rk > 0 ? SkillTrees.Describe(sel, rk) : "（還沒學）";
            string nxt = rk < sel.max ? SkillTrees.Describe(sel, rk + 1) : "（已滿級）";
            string extra = sel.kind == NodeKind.Learn && Skills.All.TryGetValue(sel.skill, out var sk) ? $"\n{sk.desc}" : "";
            var detailIcon=new Rect(dr.x+gap,dr.y+gap*.5f,fs*1.5f,fs*1.5f);
            UiKit.Badge(detailIcon,(sel.kind==NodeKind.Learn||sel.kind==NodeKind.Mod)&&!string.IsNullOrEmpty(sel.skill)?SkillIcons.Get(sel.skill):null,UiKit.Gold,sel.kind==NodeKind.Passive?1:0);
            UiKit.Text(new Rect(detailIcon.xMax+gap,dr.y+gap*.5f,dr.xMax-detailIcon.xMax-gap*2,fs*1.5f),$"{sel.name}（{rk}/{sel.max}）",fs,UiKit.Paper,TextAnchor.MiddleLeft,true);
            UiKit.Divider(new Rect(dr.x+gap,dr.y+fs*1.9f,dr.width-gap*2,1),UiKit.Gold);
            DrawTreeParagraph(new Rect(dr.x+gap,dr.y+fs*2.1f,dr.width-gap*2,Mathf.Max(fs,dr.height-fs*4.8f)),sel.id,$"{req}\n現在：{now}\n下一級：{nxt}{extra}",fsS,new Color(.85f,.9f,1f));
            string blk = p.NodeBlock(sel);
            if (UiKit.Button(new Rect(dr.x + gap, dr.yMax - fs * 2.3f, dr.width-gap*2, fs * 2f), blk ?? "學習（1 技能點）", fsS, blk == null, new Color(0.5f, 1f, 0.6f), blk == null))
            {
                if (p.Learn(sel)) { changed = true; Sfx.Play("charged"); Toast($"學會：{sel.name}"); }
            }
            return changed;
        }

        static IEnumerable<string> NamesOf(string[] ids) { foreach (var id in ids) yield return Skills.All.TryGetValue(id, out var s) ? s.name : id; }
    }
}
