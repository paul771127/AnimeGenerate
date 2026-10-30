using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>
    /// 角色畫面的「🎒 背包」分頁（使用者要求）：一次看到身上所有的東西——
    /// 金幣、試煉幣、道具（藥水、捲軸）、藥劑、素材、精魄（這些會疊放，顯示數量）；裝備不疊放，一件一格。
    /// 點任何一格：下面跳出簡介與說明（用途、怎麼拿到、數值）。只看，不能在這裡操作。
    /// </summary>
    public partial class CharacterPanel
    {
        Vector2 invScroll; float invContentH;
        string invSel;                // 選中的那一格（key）
        string invTitle, invBody; Color invColor;

        sealed class InvRow { public string key, icon, name, body; public int n; public Color c; }

        void DrawInventory(Rect view, CharacterProfile p, int fs, int fsS, float gap)
        {
            float lh = fs * 1.6f, cw = view.width - fs * 0.85f;
            bool detail = invSel != null;
            var content = new Rect(0, 0, cw, Mathf.Max(view.height, invContentH));
            if (!detail) invScroll = UiKit.DragScroll(view, invScroll, content);
            if (detail) GUI.enabled = false;   // 說明開著：底下的格子不能按（說明框蓋在上面）
            invScroll = GUI.BeginScrollView(view, invScroll, content, false, true);
            float y = 0f;
            void Header(string t) { UiKit.Text(new Rect(0, y, cw, lh), t, fsS, UiKit.Gold, TextAnchor.MiddleLeft, true); y += lh; }
            void Grid(List<InvRow> rows, string empty)
            {
                if (rows.Count == 0) { UiKit.Text(new Rect(gap, y, cw - gap, lh * 0.9f), empty, fsS, UiKit.Muted); y += lh * 0.9f + gap * 0.5f; return; }
                float w = (cw - gap) / 2f, h = lh * 1.1f;
                for (int i = 0; i < rows.Count; i++)
                {
                    var a = rows[i];
                    var r = new Rect((i % 2) * (w + gap), y + (i / 2) * (h + gap * 0.4f), w, h);
                    bool sel = invSel == a.key;
                    UiKit.Card(r, sel ? 0.85f : 0.5f, a.c, sel ? 0.9f : 0.35f);
                    UiKit.Text(new Rect(r.x + gap * 0.6f, r.y, r.width - fs * 4.8f, r.height), $"{a.icon} {a.name}", fsS, a.c, TextAnchor.MiddleLeft);
                    if (a.n >= 0) UiKit.Text(new Rect(r.xMax - fs * 4.4f, r.y, fs * 4f, r.height), $"×{a.n}", fsS, Color.white, TextAnchor.MiddleRight, true);   // n＜0＝不疊放（裝備）
                    if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { invSel = a.key; invTitle = $"{a.icon} {a.name}" + (a.n >= 0 ? $"　×{a.n}" : ""); invBody = a.body; invColor = a.c; Sfx.Play("click", 0.4f, 1.1f); }
                }
                y += ((rows.Count + 1) / 2) * (h + gap * 0.4f) + gap * 0.6f;
            }

            // ---- 錢
            Header("💰 錢");
            Grid(new List<InvRow>
            {
                new InvRow { key = "gold", icon = "💰", name = "金幣", n = Economy.Gold, c = UiKit.Gold, body = "通用的錢。\n・用途：道具店買藥水和捲軸、鐵匠強化裝備、調配藥劑。\n・取得：打倒敵人、過關（戰鬥中拿到的要過關才入袋）、賣掉裝備。" },
                new InvRow { key = "coin", icon = "🪙", name = "試煉幣", n = TrialCoins.Coins, c = new Color(1f, 0.8f, 0.45f), body = "試煉的獎勵幣。\n・用途：在試煉商店兌換獎勵。\n・取得：每日職業試煉、故事關卡的小目標、世界王。" },
            }, "");

            // ---- 道具（戰鬥中能用的藥水、捲軸）
            var rows = new List<InvRow>();
            foreach (var c in Economy.Consumables)
            {
                int n = Economy.Count(c.id); if (n <= 0) continue;
                string use = c.scroll ? "・用法：故事關卡戰鬥中使用，出發前在整備畫面帶 1 張。" : $"・用法：戰鬥中按畫面上的藥水使用（冷卻 {c.cooldown:0} 秒；一場戰鬥不限瓶數，背包裡的都能喝）。";
                rows.Add(new InvRow { key = "c_" + c.id, icon = c.icon, name = c.name, n = n, c = new Color(0.6f, 0.88f, 1f), body = $"{c.desc}。\n{use}\n・取得：城鎮的道具店（{c.price} 金幣）。" });
            }
            Header("🧪 道具");
            Grid(rows, "沒有藥水或捲軸：到城鎮的道具店買");

            // ---- 藥劑
            rows = new List<InvRow>();
            foreach (var po in Crafting.Potions)
            {
                int n = Economy.Count(po.id); if (n <= 0) continue;
                var need = new List<string>(); foreach (var (mat, k) in po.need) { var md = Crafting.GetMat(mat); need.Add($"{(md != null ? md.name : mat)}×{k}"); }
                rows.Add(new InvRow { key = "p_" + po.id, icon = po.icon, name = po.name, n = n, c = new Color(0.75f, 1f, 0.7f), body = $"效果：{po.desc}。\n・用法：戰鬥中點畫面上的藥劑按鈕，或唸藥劑的名字（「{po.name}」）；效果 {Crafting.PotionTime / 60f:0} 分鐘，同一種再喝＝重新計時。\n・調配：{string.Join("、", need)}＋{po.gold} 金幣（{WorldUI.Towns[Mathf.Clamp(po.town, 0, WorldUI.Towns.Length - 1)].name}的藥劑師）。" });
            }
            Header("⚗ 藥劑");
            Grid(rows, "沒有藥劑：到城鎮用素材調配");

            // ---- 素材
            rows = new List<InvRow>();
            foreach (var m in Crafting.Mats)
            {
                int n = Economy.Count(m.id); if (n <= 0) continue;
                var uses = new List<string>();
                foreach (var po in Crafting.Potions) foreach (var (mat, k) in po.need) { var md = Crafting.GetMat(mat); if (md != null && md.id == m.id) uses.Add($"{po.name}（{k} 個）"); }
                string use = m.id.Contains("refine") ? "鐵匠：強化裝備要用" : m.id.Contains("ore") ? "鐵匠：5 個合成一個精煉石（強化裝備用）" : uses.Count > 0 ? "調配 " + string.Join("、", uses) : "收集用的素材（之後的配方、任務會用到）";
                rows.Add(new InvRow { key = "m_" + m.id, icon = m.icon, name = m.name, n = n, c = new Color(0.9f, 0.82f, 0.65f), body = $"・用途：{use}。\n・取得：{m.from}。" });
            }
            Header("🪨 素材");
            Grid(rows, "沒有素材：打倒敵人有機率掉落");

            // ---- 寵物用品：誘餌、飼料
            rows = new List<InvRow>();
            foreach (var f in Pets.Foods) { int n = Economy.Count(f.id); if (n > 0) rows.Add(new InvRow { key = "f_" + f.id, icon = f.icon, name = f.name, n = n, c = new Color(0.45f, 1f, 0.8f), body = $"寵物飼料：加 {f.xp} 經驗" + (f.star ? $"，有機率學會{(f.pool.HasValue ? Pets.PoolName(f.pool.Value) + "系" : "寵物自己那一系")}的招（同系 {Pets.StarLearnSame * 100f:0}%、不同系 {Pets.StarLearnOther * 100f:0}%）" : "（只加經驗）") + $"。\n・用法：「🐾 寵物」分頁選一隻寵物餵。\n・說明：{f.desc}。" }); }
            foreach (var s in Pets.All)
            {
                int n = Economy.Count(s.bait); if (n <= 0) continue;
                rows.Add(new InvRow { key = "b_" + s.bait, icon = "🍖", name = s.baitName, n = n, c = new Color(1f, 0.85f, 0.5f), body = $"「{s.Name}」的誘餌{(s.elite ? "（菁英）" : "")}。\n・用法：戰鬥中場上有{s.Name}時，畫面上會出現誘餌按鈕；丟出去後牠會吃 3 秒（不攻擊），這時把牠的血壓到 30% 以下就有機會收服。\n・取得：打倒{s.Name}有 {(s.elite ? Pets.EliteBaitDrop : Pets.BaitDrop) * 100f:0.#}% 機率掉" + (s.elite ? $"；或馴獸印片 {Pets.SealsPerEliteBait} 枚＋{s.price} 金調配。" : $"；城鎮的寵物頁也買得到（{s.price} 金）。") });
            }
            Header("🐾 寵物用品");
            Grid(rows, "沒有誘餌和飼料");

            // ---- 精魄（還沒鑲上的）
            rows = new List<InvRow>();
            foreach (var d in Souls.All)
            {
                if (!p.souls.TryGetValue(d.id, out int sn) || sn <= 0) continue;
                rows.Add(new InvRow { key = "s_" + d.id, icon = "💠", name = d.name, n = sn, c = d.color, body = $"效果：{Souls.Desc(d)}。\n・鑲嵌：{Souls.PartText(d.part)}的精魄孔（到「🛡 裝備」分頁選一件裝備 → 💠 精魄；鑲上後不能取出）。\n・取得：打倒「{d.source}」有 {d.chance * 100f:0.##}% 機率掉落。" });
            }
            Header("💠 精魄（還沒鑲上的）");
            Grid(rows, "沒有精魄：打倒敵人有很小的機率掉落");

            // ---- 裝備背包：裝備不疊放（使用者要求），一件一格，照背包裡的順序
            var bag = Progress.Bag;
            rows = new List<InvRow>();
            for (int i = 0; i < bag.Count; i++)
            {
                var it = bag[i]; if (it == null) continue;
                var lines = new List<string>
                {
                    $"{Item.SlotNames[(int)it.slot]}・物品等級 {it.ilvl}　限定：{it.ClassText()}",
                    $"需求：等級 {it.reqLevel}" + (it.reqValue > 0 ? $"、{StatInfo.Names[(int)it.reqStat]} {it.reqValue}" : "") + (p.CanEquip(it, out var why) ? "" : $"　❌ {why}"),
                };
                foreach (var ml in it.ModLines()) lines.Add("・" + ml);
                SoulLines(it, lines);
                var sd = Economy.SetOf(it.setId);
                if (sd != null) lines.Add(Economy.SetText(sd, p.equipped));
                lines.Add("（穿上、卸下、賣掉、鑲精魄：到「🛡 裝備」分頁）");
                rows.Add(new InvRow { key = "i_" + it.uid + "_" + i, icon = "", name = $"[{Item.RarityNames[it.rarity]}] {it.FullName}", n = -1, c = Item.RarityColors[it.rarity], body = string.Join("\n", lines) });
            }
            Header($"🛡 裝備背包（{bag.Count}/{Progress.BagSize}；身上穿的不算）");
            Grid(rows, "裝備背包是空的");

            invContentH = y + gap;
            GUI.EndScrollView();
            GUI.enabled = true;

            // ---- 說明框（蓋在下半部）
            if (invSel != null)
            {
                var dr = new Rect(view.x, view.y + view.height * 0.42f, view.width, view.height * 0.58f);
                UiKit.Card(dr, 0.96f, invColor, 0.9f);
                float bh = fs * 1.8f;
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.5f, dr.width - gap * 2 - fs * 4.5f, fs * 1.5f), invTitle, fs, invColor, TextAnchor.MiddleLeft, true);
                if (UiKit.Button(new Rect(dr.xMax - gap - fs * 4.2f, dr.y + gap * 0.4f, fs * 4.2f, fs * 1.5f), "關閉", fsS)) invSel = null;
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap + fs * 1.6f, dr.width - gap * 2, dr.height - fs * 1.6f - gap * 2), invBody ?? "", fsS, new Color(0.9f, 0.92f, 1f), TextAnchor.UpperLeft, false, 0.5f);
                // 點說明框外面：關閉
                var e = Event.current;
                if (invSel != null && e.type == EventType.MouseDown && view.Contains(e.mousePosition) && !dr.Contains(e.mousePosition)) { invSel = null; e.Use(); }
            }
        }
    }
}
