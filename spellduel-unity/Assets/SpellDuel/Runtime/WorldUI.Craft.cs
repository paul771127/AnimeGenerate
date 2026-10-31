using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 藥劑師（每個城鎮：用當地敵人掉的材料做強化藥劑，每罐 3 分鐘）、工匠（特殊裝備）。資料在 Crafting.cs。
    /// 入口：道具店的「⚗ 藥劑師」、鐵匠鋪的「🛠 工匠」。
    /// </summary>
    public partial class WorldUI
    {
        Crafting.Recipe craftSel;
        Blueprints.Blueprint bpSel;
        Vector2 craftScroll;
        static readonly string[] AlchemistNames = { "藥婆婆・炭姨", "藥師・青蘿", "藥商・汐", "藥劑官・賽琳", "旅藥師・駝鈴" };

        // ---------------------------------------------------------------- ⚗ 藥劑師
        void DrawAlchemyBody(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var c = new Color(0.7f, 0.5f, 1f);
            float y = r.y + gap;
            Header(r, $"⚗ 藥劑師（{Towns[town].name}）", c, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, AlchemistNames[Mathf.Clamp(town, 0, AlchemistNames.Length - 1)], "藥劑師", "把這一帶的怪物材料帶來，我幫你熬成強化藥劑。喝了能撐 3 分鐘——戰鬥中點藥劑按鈕，或唸藥劑的名字就會喝。", c, fs, fsS, gap, ref y);
            var list = Crafting.Potions.FindAll(p => p.town == town);
            float bottom = r.yMax - gap - fs * 2.4f, rowH = fs * 4.2f;
            var view = new Rect(r.x + gap, y, r.width - gap * 2, bottom - y);
            var content = new Rect(0, 0, view.width - 4f, Mathf.Max(view.height, (list.Count + 1) * (rowH + gap * 0.4f) + fs * (6f + Crafting.Mats.Count*1.1f)));
            craftScroll = UiKit.DragScroll(view, craftScroll, content);
            craftScroll = GUI.BeginScrollView(view, craftScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            float yy = 0f;
            foreach (var p in list)
            {
                var rr = new Rect(0, yy, content.width, rowH);
                bool ok = Crafting.CanPay(p.need, p.gold, out var why);
                UiKit.MenuCard(rr, 0.8f, c, 0.6f);
                SupplyArt.Draw(new Rect(rr.x+gap,rr.y+gap*.2f,fs*1.35f,fs*1.35f),p.id);
                UiKit.Text(new Rect(rr.x + gap+fs*1.45f, rr.y + gap * 0.2f, rr.width * 0.62f-fs*1.45f, fs * 1.4f), $"{p.name}　持有 {Economy.Count(p.id)}", fs, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + gap, rr.y + fs * 1.5f, rr.width * 0.62f, fs * 1.2f), p.desc, fsS, new Color(0.9f, 0.85f, 1f), TextAnchor.MiddleLeft);
                DrawCraftNeeds(new Rect(rr.x+gap,rr.y+fs*2.7f,rr.width*.62f,fs*1.3f),p.need,fsS);
                UiKit.Text(new Rect(rr.xMax-rr.width*.33f,rr.y,rr.width*.32f,fs),$"💰{p.gold}",fsS,UiKit.Gold);
                if(!ok&&Event.current.type==EventType.MouseDown&&rr.Contains(Event.current.mousePosition))CraftArtTalk("shortage");
                if (UiKit.Button(new Rect(rr.xMax - rr.width * 0.33f, rr.y + rr.height * 0.2f, rr.width * 0.32f - gap * 0.3f, rr.height * 0.6f), "⚗ 製作", fs, ok, c, ok))
                {
                    if (Crafting.BrewPotion(p, out var msg)) { Toast(msg); Sfx.Play("heal", 0.7f); CraftArtDone(true,p.id,null); } else { Toast(msg); CraftArtDone(false,p.id,null); }
                }
                yy += rowH + gap * 0.4f;
            }
            yy += gap;
            UiKit.Text(new Rect(0, yy, content.width, fs * 1.3f), "🎒 我的材料", fsS, UiKit.Gold, TextAnchor.MiddleLeft, true);
            yy += fs * 1.4f;
            DrawCraftMats(new Rect(0,yy,content.width,fs*(5f+Crafting.Mats.Count*1.1f)),fsS);
            GUI.EndScrollView();
            BackToTown(r, fs, gap);
        }

        // ---------------------------------------------------------------- 🛠 工匠
        void DrawCraftBody(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var c = new Color(0.95f, 0.75f, 0.35f);
            float y = r.y + gap;
            Header(r, "🛠 工匠：特殊裝備", c, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, "工匠・老錘", "工匠", $"做出來的裝備跟著你現在的等級（Lv{prof.level}），數值每次都有點不一樣。等級越高，要的材料越多；最好的東西，得從 Boss 身上拿。", c, fs, fsS, gap, ref y);
            // 特殊裝備配方（人形敵人掉的）：身上有配方、或已經學會的才列出來，排在最前面
            var bps = Blueprints.All.FindAll(b => b.Learned || b.Held);
            if (bpSel != null && !bps.Contains(bpSel)) bpSel = null;
            float detailH = bpSel != null ? fs * 13.2f : craftSel != null ? fs * 10.3f : 0f;
            float bottom = r.yMax - gap - fs * 2.4f - detailH, rowH = fs * 2.6f;
            var view = new Rect(r.x + gap, y, r.width - gap * 2, bottom - y);
            int rows = bps.Count + Crafting.Recipes.Count + 1;
            var content = new Rect(0, 0, view.width - 4f, Mathf.Max(view.height, rows * (rowH + gap * 0.3f)));
            craftScroll = UiKit.DragScroll(view, craftScroll, content);
            craftScroll = GUI.BeginScrollView(view, craftScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < bps.Count; i++)
            {
                var b = bps[i];
                var rr = new Rect(0, i * (rowH + gap * 0.3f), content.width, rowH);
                bool ok = b.Learned && Crafting.CanPay(b.need, b.gold, out _);
                if (UiKit.Button(rr, "", fsS, bpSel == b, Item.RarityColors[b.rarity])) { bpSel = bpSel == b ? null : b; craftSel = null; }
                string state = !b.Learned ? "📜 新配方：交給工匠" : ok ? "✅" : "⬜";
                UiKit.Text(new Rect(rr.x + gap, rr.y, rr.width - gap * 2, rowH * 0.55f), $"{state} [{Item.RarityNames[b.rarity]}] {b.name}・{Item.SlotNames[(int)b.slot]}・Lv{b.level}", fsS, Item.RarityColors[b.rarity], TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + gap, rr.y + rowH * 0.5f, rr.width - gap * 2, rowH * 0.48f), b.look, Mathf.RoundToInt(fsS * 0.85f), new Color(1f, 0.92f, 0.75f), TextAnchor.MiddleLeft);
            }
            float y0 = bps.Count * (rowH + gap * 0.3f);
            if (bps.Count == 0) { UiKit.Text(new Rect(0, 0, content.width, rowH), "📜 特殊裝備配方：打倒人形敵人、四隻稀有怪、疾風稀有怪、翠蔭迷林的守衛有機會掉（Lv50～90）", fsS, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleLeft, true); y0 = rowH + gap * 0.3f; }
            for (int i = 0; i < Crafting.Recipes.Count; i++)
            {
                var rc = Crafting.Recipes[i];
                var rr = new Rect(0, y0 + i * (rowH + gap * 0.3f), content.width, rowH);
                bool ok = Crafting.CanPay(Crafting.NeedFor(rc, prof.level), Crafting.GoldFor(rc, prof.level), out _);
                if (UiKit.Button(rr, "", fsS, craftSel == rc, Item.RarityColors[rc.rarity])) { craftSel = craftSel == rc ? null : rc; bpSel = null; }
                string diff = rc.minDifficulty >= 2 ? "　（困難才有的材料）" : rc.minDifficulty >= 1 ? "　（普通以上的材料）" : "";
                UiKit.Text(new Rect(rr.x + gap, rr.y, rr.width - gap * 2, rowH * 0.55f), $"{(ok ? "✅" : "⬜")} [{Item.RarityNames[rc.rarity]}] {rc.name}・{Item.SlotNames[(int)rc.slot]}", fsS, Item.RarityColors[rc.rarity], TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + gap, rr.y + rowH * 0.5f, rr.width - gap * 2, rowH * 0.48f), rc.note + diff, Mathf.RoundToInt(fsS * 0.9f), new Color(0.88f, 0.9f, 1f), TextAnchor.MiddleLeft);
            }
            GUI.EndScrollView();
            if (bpSel != null) DrawBlueprintDetail(new Rect(r.x + gap, bottom + gap * 0.3f, r.width - gap * 2, detailH - gap * 0.5f), bpSel, prof, c, fs, fsS, gap);
            if (craftSel != null)
            {
                var rc = craftSel;
                var need = Crafting.NeedFor(rc, prof.level); int gold = Crafting.GoldFor(rc, prof.level);
                bool ok = Crafting.CanPay(need, gold, out var why);
                var dr = new Rect(r.x + gap, bottom + gap * 0.3f, r.width - gap * 2, detailH - gap * 0.5f);
                UiKit.RarityCard(dr,rc.rarity);
                var fx = new List<string>();
                foreach (var e in rc.effects) fx.Add(e.hi >= 1f ? $"{Item.AffixText(e.a, e.lo)}～{e.hi:0}（跟著等級變多）" : $"{Item.AffixText(e.a, e.lo)}～{e.hi * 100:0.#}%");
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.3f, dr.width - gap * 2, dr.height - fs * 4.1f),
                    $"特殊效果（數值每次隨機）：\n・{string.Join("\n・", fx)}\n物品等級 Lv{prof.level}（主屬性 ±10%）\n💰{gold}", Mathf.RoundToInt(fsS * 0.9f), Color.white, TextAnchor.UpperLeft, false, 0.5f);
                DrawCraftNeeds(new Rect(dr.x+gap,dr.yMax-fs*3.7f,dr.width-gap*2,fs*1.25f),need,fsS);
                if(!ok&&Event.current.type==EventType.MouseDown&&dr.Contains(Event.current.mousePosition))CraftArtTalk("shortage");
                if (UiKit.Button(new Rect(dr.x + dr.width * 0.15f, dr.yMax - fs * 2.3f, dr.width * 0.7f, fs * 2f), ok ? "🛠 製作" : why, ok ? fs : fsS, ok, c, ok))
                {
                    var it = Crafting.Craft(rc, prof, rng, out var msg);
                    if (it != null)
                    {
                        bool inBag = Progress.AddToBagOrMail(it, "工匠的作品");
                        Progress.SaveBag();
                        Toast(msg + (inBag ? "" : "（背包滿了，寄到信箱）"));
                        Sfx.Play("anvil", 0.8f); Sfx.Play("win", 0.6f, 1.2f); CraftArtDone(true,null,it);
                    }
                    else { Toast(msg); CraftArtDone(false,null,null); }
                }
            }
            BackToTown(r, fs, gap);
        }

        void DrawBlueprintDetail(Rect dr, Blueprints.Blueprint b, CharacterProfile prof, Color c, int fs, int fsS, float gap)
        {
            UiKit.RarityCard(dr,b.rarity);
            var fx = new List<string>();
            foreach (var e in b.affixes) fx.Add(e.hi >= 1f ? $"{Item.AffixText(e.a, e.lo)}～{e.hi:0}" : $"{Item.AffixText(e.a, e.lo)}～{e.hi * 100:0.#}%");
            foreach (var x in b.specials) fx.Add("✨ " + Souls.FxText(x));
            string lvNote = prof.level < b.level ? $"（你現在 Lv{prof.level}，要 Lv{b.level} 才能穿）" : "";
            UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.3f, dr.width - gap * 2, dr.height - fs * 5.6f),
                $"外型：{b.look}\n・{string.Join("\n・", fx)}\n物品等級固定 Lv{b.level}{lvNote}　💰{b.gold}", Mathf.RoundToInt(fsS * 0.85f), Color.white, TextAnchor.UpperLeft, false, 0.5f);
            // 材料最多 7 種：分兩行
            int half = (b.need.Length + 1) / 2;
            var n1 = new (string, int)[half]; var n2 = new (string, int)[b.need.Length - half];
            for (int i = 0; i < b.need.Length; i++) { if (i < half) n1[i] = b.need[i]; else n2[i - half] = b.need[i]; }
            DrawCraftNeeds(new Rect(dr.x + gap, dr.yMax - fs * 5.2f, dr.width - gap * 2, fs * 1.25f), n1, fsS);
            if (n2.Length > 0) DrawCraftNeeds(new Rect(dr.x + gap, dr.yMax - fs * 3.8f, dr.width * (n2.Length / (float)half) - gap * 2, fs * 1.25f), n2, fsS);
            var btn = new Rect(dr.x + dr.width * 0.15f, dr.yMax - fs * 2.3f, dr.width * 0.7f, fs * 2f);
            if (!b.Learned)
            {
                if (UiKit.Button(btn, "📜 把配方交給工匠", fs, true, c, true))
                {
                    if (Blueprints.Learn(b, out var lm)) { Toast(lm); Sfx.Play("win", 0.7f, 1.1f); } else Toast(lm);
                }
                return;
            }
            bool ok = Crafting.CanPay(b.need, b.gold, out var why);
            if (!ok && Event.current.type == EventType.MouseDown && dr.Contains(Event.current.mousePosition)) CraftArtTalk("shortage");
            if (UiKit.Button(btn, ok ? "🛠 製作" : why, ok ? fs : fsS, ok, c, ok))
            {
                var it = Blueprints.Craft(b, prof, rng, out var msg);
                if (it != null)
                {
                    bool inBag = Progress.AddToBagOrMail(it, "工匠的作品");
                    Progress.SaveBag();
                    Toast(msg + (inBag ? "" : "（背包滿了，寄到信箱）"));
                    Sfx.Play("anvil", 0.8f); Sfx.Play("win", 0.6f, 1.2f); CraftArtDone(true, null, it);
                }
                else { Toast(msg); CraftArtDone(false, null, null); }
            }
        }

        static string NeedText((string mat, int n)[] need)
        {
            var l = new List<string>();
            foreach (var (m, n) in need) l.Add($"{Crafting.MatName(m)} {Crafting.Have(m)}/{n}");
            return string.Join("、", l);
        }

        static string MatsText()
        {
            var l = new List<string>();
            foreach (var m in Crafting.Mats) { int n = Economy.Count(m.id); if (n > 0) l.Add($"{m.icon}{m.name}×{n}"); }
            return l.Count > 0 ? string.Join("　", l) : "還沒有材料：打倒敵人有 10% 機率掉（每隻敵人最多掉一樣東西）";
        }
    }
}
