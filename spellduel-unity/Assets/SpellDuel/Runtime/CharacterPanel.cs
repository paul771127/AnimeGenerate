using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色畫面（無盡模式）：能力值、技能樹、裝備三個分頁。
    /// 由 SoloBattle 在選技能畫面或戰鬥中（點「角色」）呼叫 Draw；有改動時回傳 true（呼叫者重新套用到戰鬥中的角色）。
    /// 技能樹分頁的畫面在 CharacterPanel.Tree.cs（介面設計交給美術：見 SKILLTREE_UI_BRIEF.md）。
    /// </summary>
    public partial class CharacterPanel
    {
        public int tab;                 // 0 能力值、1 技能樹、2 裝備、3 進階技能樹（轉職後）
        string selNode;                 // 技能樹：選中的節點
        Item selItem;                   // 裝備：選中的物品（背包或身上）
        Item discardArm; float discardArmUntil, resetArmUntil;   // 丟掉／重新分配：要按兩次確認
        int page;
        Vector2 statsScroll, gearScroll;
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
            UiKit.Title(new Rect(x, y, w, lh), $"{(adv != null ? $"{adv.name}（{cd.name}）" : cd.name)}　Lv {p.level}", adv != null ? adv.color : cd.color, fsL);
            UiKit.Text(new Rect(x, y+lh, w, fs*1.2f), p.cls == Skills.Novice ? $"能力點 {p.statPoints}　技能點 {p.skillPoints}（{Skills.JobLevel} 級轉職後使用）" : $"能力點 {p.statPoints}　技能點 {p.skillPoints}　職業樹 {p.BaseSpent}/{CharacterProfile.TreeTotal(p.cls)}{(p.adv == null ? $"（轉職前最多 {p.BaseTreeCap}）" : "")}", fsS, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleRight, true);
            y += lh + fs*1.3f;
            UiKit.Bar(new Rect(x, y, w, lh * 0.5f), p.level >= CharacterProfile.MaxLevel ? 1f : (p.xp - cur) / (float)Mathf.Max(1, next - cur), new Color(0.75f, 0.45f, 1f),
                p.level >= CharacterProfile.MaxLevel ? "最高等級" : p.LevelLocked ? $"經驗已滿：到職業工會轉職才能升到 {p.level + 1} 級" : $"經驗 {p.xp - cur} / {next - cur}", fsS);
            y += lh * 0.5f + gap;
            // ---- 分頁
            string[] tabs = adv != null ? new[] { "📊 能力值", "🌳 技能樹", "🛡 裝備", "✨ 進階" } : new[] { "📊 能力值", "🌳 技能樹", "🛡 裝備" };
            if (tab >= tabs.Length) tab = 0;
            float tw = (w - gap * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                if (UiKit.Button(new Rect(x + i * (tw + gap), y, tw, lh * 1.1f), tabs[i], fsS, tab == i, i == 3 ? adv.color : cd.color)) { tab = i; selNode = null; selItem = null; }
            for(int i=0;i<tabs.Length;i++) UiKit.Tab(new Rect(x+i*(tw+gap),y,tw,lh*1.1f),tab==i,i==3?adv.color:cd.color);
            y += lh * 1.1f + gap;
            var body = new Rect(x, y, w, r.yMax - gap - y);
            bool changed = tab == 0 ? DrawBodyPage(body, p, fs, fsS, gap, false) : tab == 1 ? DrawTree(body, p, fs, fsS, gap, p.cls) : tab == 3 ? DrawTree(body, p, fs, fsS, gap, adv.id) : DrawBodyPage(body, p, fs, fsS, gap, true);
            if (Time.time < toastUntil) UiKit.Pill(r.y + r.height * 0.5f, toast, fs, new Color(1f, 0.85f, 0.5f), r.width * 0.9f, 0.9f);
            if (changed) Progress.SaveChar(p);
            return changed;
        }

        // Keep row targets readable on shorter phones; scrolling is presentation state only.
        bool DrawBodyPage(Rect viewport,CharacterProfile p,int fs,int fsS,float gap,bool equipment)
        {
            var content=new Rect(0,0,viewport.width-fs*.85f,Mathf.Max(viewport.height,fs*30));
            var offset=equipment?gearScroll:statsScroll;
            offset=UiKit.DragScroll(viewport,offset,content);   // 手指拖曳捲動
            offset=GUI.BeginScrollView(viewport,offset,content,false,true);
            if(equipment)gearScroll=offset;else statsScroll=offset;
            try{return equipment?DrawGear(content,p,fs,fsS,gap):DrawStats(content,p,fs,fsS,gap);}
            finally{GUI.EndScrollView();}
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
                UiKit.Card(rr, 0.78f, StatInfo.Colors[i], 0.65f);
                UiKit.Divider(new Rect(rr.x+4,rr.y+8,3,rr.height-16),StatInfo.Colors[i]);
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
            bool resetArmed = Time.time < resetArmUntil;
            if (UiKit.Button(new Rect(b.x + b.width * 0.55f, b.yMax - fs * 2f, b.width * 0.45f, fs * 1.9f), resetArmed ? "再按一次確認重新分配" : "↺ 重新分配所有點數", fsS, resetArmed, new Color(1f, 0.5f, 0.45f)))
            if (!resetArmed) resetArmUntil = Time.time + 3f;   // 第一次按：3 秒內再按一次才執行
            else
            {
                resetArmUntil = 0f;
                // 能力值不夠穿的裝備：放回背包（背包滿了也照放，絕不丟掉）
                var off = p.ResetPoints();
                int mailed = 0;
                foreach (var it in off) if (!Progress.AddToBagOrMail(it, "重新分配點數時脫下")) mailed++;
                Progress.SaveChar(p);
                changed = true;
                Toast(off.Count == 0 ? "能力點與技能點都退回了" : $"點數都退回了；能力值不夠穿的 {off.Count} 件裝備（{string.Join("、", off.ConvertAll(x => x.name))}）" + (mailed > 0 ? $"放回背包，背包滿了的 {mailed} 件寄到信箱（{Progress.MailKeepDays} 天內要領）" : "放回背包") + "，加完點再穿上");
            }
            UiKit.Text(new Rect(b.x, b.yMax - fs * 2f, b.width * 0.53f, fs * 1.9f), "能力值越高，再加需要越多點（每 10 點 +1）", fsS, new Color(0.75f, 0.78f, 0.88f), TextAnchor.MiddleLeft, false, 0.55f);
            return changed;
        }

        // ================================================================ 信箱（背包滿了時拿到的裝備；7 天後自動刪除）
        bool mailOpen; int mailPage;
        bool DrawMailbox(Rect r, CharacterProfile p, int fs, int fsS, float gap)
        {
            var mails = Progress.Mailbox;
            UiKit.Card(r, 0.85f, new Color(1f, 0.75f, 0.3f), 0.6f);
            float x = r.x + gap, w = r.width - gap * 2, y = r.y + gap * 0.5f;
            UiKit.Text(new Rect(x, y, w, fs * 2.4f), $"⚠ 背包滿了時拿到的裝備會寄到這裡。信件只保留 {Progress.MailKeepDays} 天，過期會自動刪除，請盡快空出背包領取。", fsS, new Color(1f, 0.8f, 0.5f), TextAnchor.UpperLeft, true, 0.6f);
            y += fs * 2.5f;
            if (mails.Count == 0)
            {
                UiKit.Text(new Rect(x, y, w, fs * 2f), "信箱是空的", fs, Color.gray, TextAnchor.MiddleCenter);
                return false;
            }
            bool full = Progress.BagFull, changed = false;
            float rh = fs * 2.2f, bw = w * 0.24f;
            int per = Mathf.Max(1, Mathf.FloorToInt((r.yMax - y - fs * 2.6f) / (rh + gap * 0.3f)));
            int pages = Mathf.Max(1, (mails.Count + per - 1) / per);
            mailPage = Mathf.Clamp(mailPage, 0, pages - 1);
            for (int k = 0; k < per; k++)
            {
                int i = mailPage * per + k;
                if (i >= mails.Count) break;
                var m = mails[i]; var it = m.item;
                var rr = new Rect(x, y, w, rh);
                int left = Progress.MailDaysLeft(m);
                UiKit.Card(rr, 0.6f, Item.RarityColors[it.rarity], 0.6f);
                UiKit.Text(new Rect(rr.x + gap * 0.5f, rr.y, rr.width - bw - gap * 1.5f, rh * 0.55f), $"[{Item.RarityNames[it.rarity]}] {it.FullName}（{Item.SlotNames[(int)it.slot]}・Lv{it.ilvl}）", fsS, Item.RarityColors[it.rarity], TextAnchor.MiddleLeft, true, 0.6f);
                UiKit.Text(new Rect(rr.x + gap * 0.5f, rr.y + rh * 0.5f, rr.width - bw - gap * 1.5f, rh * 0.48f), (left <= 0 ? "⚠ 今天到期" : left <= 2 ? $"⚠ 剩 {left} 天" : $"剩 {left} 天") + (string.IsNullOrEmpty(m.from) ? "" : $"・{m.from}"), fsS, left <= 2 ? new Color(1f, 0.45f, 0.4f) : new Color(0.8f, 0.85f, 0.95f), TextAnchor.MiddleLeft, false, 0.6f);
                if (UiKit.Button(new Rect(rr.xMax - bw - gap * 0.3f, rr.y + rh * 0.12f, bw, rh * 0.76f), full ? "背包滿" : "領取", fsS, !full, Item.RarityColors[it.rarity], !full))
                {
                    if (Progress.ClaimMail(m)) { Toast($"領取了 {it.name}"); Sfx.Play("coin", 0.6f, 1.2f); changed = true; }
                    break;   // 清單變了，這一幀不再畫
                }
                y += rh + gap * 0.3f;
            }
            float by = r.yMax - fs * 2.2f;
            if (pages > 1)
            {
                if (UiKit.Button(new Rect(x, by, fs * 3f, fs * 1.8f), "◀", fsS, false, null, mailPage > 0)) mailPage--;
                UiKit.Text(new Rect(x + fs * 3.2f, by, fs * 3f, fs * 1.8f), $"{mailPage + 1}/{pages}", fsS, Color.white, TextAnchor.MiddleCenter);
                if (UiKit.Button(new Rect(x + fs * 6.4f, by, fs * 3f, fs * 1.8f), "▶", fsS, false, null, mailPage < pages - 1)) mailPage++;
            }
            if (UiKit.Button(new Rect(r.xMax - gap - w * 0.4f, by, w * 0.4f, fs * 1.8f), full ? "背包滿了" : $"全部領取（空 {Progress.BagFree} 格）", fsS, !full, new Color(1f, 0.8f, 0.35f), !full))
            {
                int n = 0;
                foreach (var m in new List<Progress.Mail>(mails)) { if (!Progress.ClaimMail(m)) break; n++; }
                Toast(n == mails.Count + n ? $"領取了 {n} 件" : $"領取了 {n} 件，背包又滿了，剩下的還在信箱");
                changed |= n > 0;
            }
            return changed;
        }

        // ================================================================ 裝備
        bool DrawGear(Rect b, CharacterProfile p, int fs, int fsS, float gap)
        {
            bool changed = false;
            // 身上的 5 個欄位
            float sh = fs * 2.0f, slotW = b.width * 0.66f;
            // 右邊：穿著這身裝備的角色（慢慢轉）
            GearPreview.Draw(new Rect(b.x + slotW + gap * 0.5f, b.y, b.width - slotW - gap * 0.5f, 5 * (sh + gap * 0.3f) - gap * 0.3f), p);
            for (int i = 0; i < 5; i++)
            {
                var it = p.equipped[i];
                var sr = new Rect(b.x, b.y + i * (sh + gap * 0.3f), slotW, sh);
                if (UiKit.Button(sr, "", fsS, selItem != null && selItem == it, it != null ? Item.RarityColors[it.rarity] : (Color?)null)) selItem = it;
                UiKit.Tab(sr, selItem != null && selItem == it, it != null ? Item.RarityColors[it.rarity] : UiKit.Muted);
                UiKit.Text(new Rect(sr.x + gap, sr.y, sr.width * 0.24f, sr.height), Item.SlotNames[i], fsS, new Color(0.8f, 0.82f, 0.9f), TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(sr.x + sr.width * 0.24f, sr.y, sr.width * 0.74f, sr.height), it != null ? $"[{Item.RarityNames[it.rarity]}] {it.FullName}" : "（空）", fsS, it != null ? Item.RarityColors[it.rarity] : Color.gray, TextAnchor.MiddleLeft, true);
            }
            // 背包（分頁，3×4）
            float by = b.y + 5 * (sh + gap * 0.3f) + gap * 0.5f;
            var bag = Progress.Bag;
            int per = 12, pages = Mathf.Max(1, (bag.Count + per - 1) / per);
            page = Mathf.Clamp(page, 0, pages - 1);
            // 背包容量：一條進度條（快滿了變黃、滿了變紅）＋信箱按鈕（有信才亮，數字＝封數）
            var mails = Progress.Mailbox;
            float capW = b.width * 0.42f, mbW = b.width * 0.24f;
            float fill = bag.Count / (float)Progress.BagSize;
            var capCol = bag.Count >= Progress.BagSize ? new Color(1f, 0.35f, 0.3f) : fill >= 0.8f ? new Color(1f, 0.8f, 0.3f) : new Color(0.45f, 0.85f, 0.5f);
            UiKit.Bar(new Rect(b.x, by + fs * 0.1f, capW, fs * 1.1f), fill, capCol, bag.Count >= Progress.BagSize ? $"背包 {bag.Count}/{Progress.BagSize} 已滿" : $"背包 {bag.Count}/{Progress.BagSize}（空 {Progress.BagFree}）", fsS);
            if (UiKit.Button(new Rect(b.x + capW + gap * 0.5f, by, mbW, fs * 1.3f), mails.Count > 0 ? $"📬 信箱 {mails.Count}" : "📭 信箱", fsS, mailOpen, mails.Count > 0 ? new Color(1f, 0.75f, 0.3f) : (Color?)null))
            { mailOpen = !mailOpen; selItem = null; }
            if (mailOpen) return DrawMailbox(new Rect(b.x, by + fs * 1.4f, b.width, b.yMax - by - fs * 1.4f), p, fs, fsS, gap) || changed;
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
            UiKit.Card(dr, 0.9f, selItem != null ? Item.RarityColors[selItem.rarity] : (Color?)null, 0.5f);
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
                if (UiKit.Button(new Rect(dr.x + gap, byy, bw, bh), "卸下", fsS))
                {
                    if (Progress.Unequip(p, s2.slot)) { changed = true; selItem = null; }
                    else Toast("背包滿了，先賣掉或丟掉一些裝備才能卸下");
                }
            }
            else
            {
                if (UiKit.Button(new Rect(dr.x + gap, byy, bw, bh), can ? "裝備" : "不能裝備", fsS, can, Item.RarityColors[s2.rarity], can))
                {
                    if (Progress.Equip(p, s2, out var w2)) { changed = true; Sfx.Play("block", 0.7f, 1.3f); Toast($"裝備：{s2.name}"); selItem = null; }
                    else Toast(w2);
                }
                // 丟掉：要按兩次（3 秒內）才真的丟，免得誤刪
                bool armed = discardArm == s2 && Time.time < discardArmUntil;
                if (UiKit.Button(new Rect(dr.x + gap * 2 + bw, byy, bw, bh), armed ? "再按一次丟掉" : "丟掉", fsS, armed, new Color(1f, 0.45f, 0.4f)))
                {
                    if (armed) { Progress.Discard(s2); selItem = null; discardArm = null; Toast($"丟掉了 {s2.name}"); }
                    else { discardArm = s2; discardArmUntil = Time.time + 3f; }
                }
            }
            return changed;
        }
    }
}
