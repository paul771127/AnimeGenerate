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
        Vector2 craftScroll;
        static readonly string[] AlchemistNames = { "藥婆婆・炭姨", "藥師・青蘿", "藥商・汐", "藥劑官・賽琳", "旅藥師・駝鈴" };

        // ---------------------------------------------------------------- ⚗ 藥劑師
        void DrawAlchemy(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var c = new Color(0.7f, 0.5f, 1f);
            float y = r.y + gap;
            Header(r, $"⚗ 藥劑師（{Towns[town].name}）", c, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, AlchemistNames[Mathf.Clamp(town, 0, AlchemistNames.Length - 1)], "藥劑師", "把這一帶的怪物材料帶來，我幫你熬成強化藥劑。喝了能撐 3 分鐘——在整備畫面喝，或戰鬥中唸藥劑的名字。", c, fs, fsS, gap, ref y);
            var list = Crafting.Potions.FindAll(p => p.town == town);
            float bottom = r.yMax - gap - fs * 2.4f, rowH = fs * 4.2f;
            var view = new Rect(r.x + gap, y, r.width - gap * 2, bottom - y);
            var content = new Rect(0, 0, view.width - 4f, Mathf.Max(view.height, (list.Count + 1) * (rowH + gap * 0.4f) + fs * 6f));
            craftScroll = UiKit.DragScroll(view, craftScroll, content);
            craftScroll = GUI.BeginScrollView(view, craftScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            float yy = 0f;
            foreach (var p in list)
            {
                var rr = new Rect(0, yy, content.width, rowH);
                bool ok = Crafting.CanPay(p.need, p.gold, out var why);
                UiKit.Card(rr, 0.8f, c, 0.6f);
                UiKit.Text(new Rect(rr.x + gap, rr.y + gap * 0.2f, rr.width * 0.62f, fs * 1.4f), $"{p.icon} {p.name}　持有 {Economy.Count(p.id)}", fs, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + gap, rr.y + fs * 1.5f, rr.width * 0.62f, fs * 1.2f), p.desc, fsS, new Color(0.9f, 0.85f, 1f), TextAnchor.MiddleLeft);
                UiKit.Text(new Rect(rr.x + gap, rr.y + fs * 2.7f, rr.width * 0.62f, fs * 1.3f), NeedText(p.need) + $"　💰{p.gold}", Mathf.RoundToInt(fsS * 0.9f), ok ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.55f), TextAnchor.MiddleLeft, false, 0.5f);
                if (UiKit.Button(new Rect(rr.xMax - rr.width * 0.33f, rr.y + rr.height * 0.2f, rr.width * 0.32f - gap * 0.3f, rr.height * 0.6f), "⚗ 製作", fs, ok, c, ok))
                {
                    if (Crafting.BrewPotion(p, out var msg)) { Toast(msg); Sfx.Play("heal", 0.7f); } else Toast(msg);
                }
                yy += rowH + gap * 0.4f;
            }
            yy += gap;
            UiKit.Text(new Rect(0, yy, content.width, fs * 1.3f), "🎒 我的材料", fsS, UiKit.Gold, TextAnchor.MiddleLeft, true);
            yy += fs * 1.4f;
            UiKit.Text(new Rect(0, yy, content.width, fs * 5f), MatsText(), Mathf.RoundToInt(fsS * 0.9f), new Color(0.88f, 0.9f, 1f), TextAnchor.UpperLeft, false, 0.5f);
            GUI.EndScrollView();
            BackToTown(r, fs, gap);
        }

        // ---------------------------------------------------------------- 🛠 工匠
        void DrawCraft(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var c = new Color(0.95f, 0.75f, 0.35f);
            float y = r.y + gap;
            Header(r, "🛠 工匠：特殊裝備", c, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, "工匠・老錘", "工匠", $"做出來的裝備跟著你現在的等級（Lv{prof.level}），數值每次都有點不一樣。等級越高，要的材料越多；最好的東西，得從 Boss 身上拿。", c, fs, fsS, gap, ref y);
            float detailH = craftSel != null ? fs * 7.6f : 0f;
            float bottom = r.yMax - gap - fs * 2.4f - detailH, rowH = fs * 2.6f;
            var view = new Rect(r.x + gap, y, r.width - gap * 2, bottom - y);
            var content = new Rect(0, 0, view.width - 4f, Mathf.Max(view.height, Crafting.Recipes.Count * (rowH + gap * 0.3f)));
            craftScroll = UiKit.DragScroll(view, craftScroll, content);
            craftScroll = GUI.BeginScrollView(view, craftScroll, content, false, false, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < Crafting.Recipes.Count; i++)
            {
                var rc = Crafting.Recipes[i];
                var rr = new Rect(0, i * (rowH + gap * 0.3f), content.width, rowH);
                bool ok = Crafting.CanPay(Crafting.NeedFor(rc, prof.level), Crafting.GoldFor(rc, prof.level), out _);
                if (UiKit.Button(rr, "", fsS, craftSel == rc, Item.RarityColors[rc.rarity])) craftSel = craftSel == rc ? null : rc;
                string diff = rc.minDifficulty >= 2 ? "　（困難才有的材料）" : rc.minDifficulty >= 1 ? "　（普通以上的材料）" : "";
                UiKit.Text(new Rect(rr.x + gap, rr.y, rr.width - gap * 2, rowH * 0.55f), $"{(ok ? "✅" : "⬜")} [{Item.RarityNames[rc.rarity]}] {rc.name}・{Item.SlotNames[(int)rc.slot]}", fsS, Item.RarityColors[rc.rarity], TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + gap, rr.y + rowH * 0.5f, rr.width - gap * 2, rowH * 0.48f), rc.note + diff, Mathf.RoundToInt(fsS * 0.9f), new Color(0.88f, 0.9f, 1f), TextAnchor.MiddleLeft);
            }
            GUI.EndScrollView();
            if (craftSel != null)
            {
                var rc = craftSel;
                var need = Crafting.NeedFor(rc, prof.level); int gold = Crafting.GoldFor(rc, prof.level);
                bool ok = Crafting.CanPay(need, gold, out var why);
                var dr = new Rect(r.x + gap, bottom + gap * 0.3f, r.width - gap * 2, detailH - gap * 0.5f);
                UiKit.Card(dr, 0.9f, Item.RarityColors[rc.rarity], 0.7f);
                var fx = new List<string>();
                foreach (var e in rc.effects) fx.Add(e.hi >= 1f ? $"{Item.AffixText(e.a, e.lo)}～{e.hi:0}（跟著等級變多）" : $"{Item.AffixText(e.a, e.lo)}～{e.hi * 100:0.#}%");
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.3f, dr.width - gap * 2, dr.height - fs * 2.6f),
                    $"特殊效果（數值每次隨機）：\n・{string.Join("\n・", fx)}\n物品等級 Lv{prof.level}（主屬性 ±10%）\n材料：{NeedText(need)}　💰{gold}", Mathf.RoundToInt(fsS * 0.9f), Color.white, TextAnchor.UpperLeft, false, 0.5f);
                if (UiKit.Button(new Rect(dr.x + dr.width * 0.15f, dr.yMax - fs * 2.3f, dr.width * 0.7f, fs * 2f), ok ? "🛠 製作" : why, ok ? fs : fsS, ok, c, ok))
                {
                    var it = Crafting.Craft(rc, prof, rng, out var msg);
                    if (it != null)
                    {
                        bool inBag = Progress.AddToBagOrMail(it, "工匠的作品");
                        Progress.SaveBag();
                        Toast(msg + (inBag ? "" : "（背包滿了，寄到信箱）"));
                        Sfx.Play("anvil", 0.8f); Sfx.Play("win", 0.6f, 1.2f);
                    }
                    else Toast(msg);
                }
            }
            BackToTown(r, fs, gap);
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
