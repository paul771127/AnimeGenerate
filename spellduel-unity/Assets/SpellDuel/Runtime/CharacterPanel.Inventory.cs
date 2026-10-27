using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>
    /// 角色畫面的「🎒 背包」分頁（使用者要求）：一次看到身上所有的東西和數量——
    /// 金幣、試煉幣、道具（藥水、捲軸）、藥劑、素材、精魄（這些會疊放，顯示數量）；裝備不疊放，一件一格。只看，不能在這裡操作。
    /// </summary>
    public partial class CharacterPanel
    {
        Vector2 invScroll; float invContentH;

        void DrawInventory(Rect view, CharacterProfile p, int fs, int fsS, float gap)
        {
            float lh = fs * 1.6f, cw = view.width - fs * 0.85f;
            var content = new Rect(0, 0, cw, Mathf.Max(view.height, invContentH));
            invScroll = UiKit.DragScroll(view, invScroll, content);
            invScroll = GUI.BeginScrollView(view, invScroll, content, false, true);
            float y = 0f;
            void Header(string t) { UiKit.Text(new Rect(0, y, cw, lh), t, fsS, UiKit.Gold, TextAnchor.MiddleLeft, true); y += lh; }
            void Grid(List<(string icon, string name, int n, Color c)> rows, string empty)
            {
                if (rows.Count == 0) { UiKit.Text(new Rect(gap, y, cw - gap, lh * 0.9f), empty, fsS, UiKit.Muted); y += lh * 0.9f + gap * 0.5f; return; }
                float w = (cw - gap) / 2f, h = lh * 1.1f;
                for (int i = 0; i < rows.Count; i++)
                {
                    var a = rows[i];
                    var r = new Rect((i % 2) * (w + gap), y + (i / 2) * (h + gap * 0.4f), w, h);
                    UiKit.Card(r, 0.5f, a.c, 0.35f);
                    UiKit.Text(new Rect(r.x + gap * 0.6f, r.y, r.width - fs * 4.8f, r.height), $"{a.icon} {a.name}", fsS, a.c, TextAnchor.MiddleLeft);
                    if (a.n >= 0) UiKit.Text(new Rect(r.xMax - fs * 4.4f, r.y, fs * 4f, r.height), $"×{a.n}", fsS, Color.white, TextAnchor.MiddleRight, true);   // n＜0＝不疊放（裝備）
                }
                y += ((rows.Count + 1) / 2) * (h + gap * 0.4f) + gap * 0.6f;
            }

            // ---- 錢
            Header("💰 錢");
            Grid(new List<(string, string, int, Color)> { ("💰", "金幣", Economy.Gold, UiKit.Gold), ("🪙", "試煉幣", TrialCoins.Coins, new Color(1f, 0.8f, 0.45f)) }, "");

            // ---- 道具（戰鬥中能用的藥水、捲軸）
            var rows = new List<(string, string, int, Color)>();
            foreach (var c in Economy.Consumables) { int n = Economy.Count(c.id); if (n > 0) rows.Add((c.icon, c.name, n, new Color(0.6f, 0.88f, 1f))); }
            Header("🧪 道具");
            Grid(rows, "沒有藥水或捲軸：到城鎮的道具店買");

            // ---- 藥劑、素材
            rows = new List<(string, string, int, Color)>();
            foreach (var po in Crafting.Potions) { int n = Economy.Count(po.id); if (n > 0) rows.Add((po.icon, po.name, n, new Color(0.75f, 1f, 0.7f))); }
            Header("⚗ 藥劑");
            Grid(rows, "沒有藥劑：到城鎮用素材調配");
            rows = new List<(string, string, int, Color)>();
            foreach (var m in Crafting.Mats) { int n = Economy.Count(m.id); if (n > 0) rows.Add((m.icon, m.name, n, new Color(0.9f, 0.82f, 0.65f))); }
            Header("🪨 素材");
            Grid(rows, "沒有素材：打倒敵人有機率掉落");

            // ---- 精魄（還沒鑲上的）
            rows = new List<(string, string, int, Color)>();
            foreach (var d in Souls.All) if (p.souls.TryGetValue(d.id, out int sn) && sn > 0) rows.Add(("💠", d.name, sn, d.color));
            Header("💠 精魄（還沒鑲上的）");
            Grid(rows, "沒有精魄：打倒敵人有很小的機率掉落");

            // ---- 裝備背包：裝備不疊放（使用者要求），一件一格，照背包裡的順序
            var bag = Progress.Bag;
            rows = new List<(string, string, int, Color)>();
            foreach (var it in bag) if (it != null) rows.Add(("", $"[{Item.RarityNames[it.rarity]}] {it.FullName}", -1, Item.RarityColors[it.rarity]));
            Header($"🛡 裝備背包（{bag.Count}/{Progress.BagSize}；身上穿的不算）");
            Grid(rows, "裝備背包是空的");

            invContentH = y + gap;
            GUI.EndScrollView();
        }
    }
}
