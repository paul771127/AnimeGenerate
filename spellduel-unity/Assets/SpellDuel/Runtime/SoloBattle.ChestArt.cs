using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 副本大地圖的寶箱（使用者要求）：
    ///   ・看得到的寶箱模型：木箱＋金邊＋鎖頭；打開後箱蓋往後掀、裡面發金光（走道藏的寶箱、寶箱地點都是）。
    ///   ・打開時跳出「拿到什麼」的視窗：裝備名稱（稀有度顏色）＋金幣。
    ///   ・小地圖的圖示：任務點、NPC、沒打開的門、存檔點（脫出點）、寶箱、試煉、首領（DrawDungeonMapIcons）。
    /// </summary>
    public partial class SoloBattle
    {
        const float ChestView = 28f;

        // ---------------------------------------------------------------- 寶箱模型
        GameObject CreateChestModel()
        {
            var root = new GameObject("Treasure chest");
            Color wood = new Color(0.55f, 0.32f, 0.16f), woodDark = new Color(0.38f, 0.21f, 0.1f), gold = new Color(1f, 0.8f, 0.3f);
            void Box(Transform p, string n, Vector3 pos, Vector3 size, Color c)
            {
                var g = Prim(PrimitiveType.Cube, c); g.name = n;
                g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = size;
            }
            const float W = 0.9f, D = 0.6f, H = 0.45f;
            Box(root.transform, "Body", new Vector3(0f, H * 0.5f, 0f), new Vector3(W, H, D), wood);
            Box(root.transform, "Inside", new Vector3(0f, H - 0.02f, 0f), new Vector3(W - 0.08f, 0.02f, D - 0.08f), woodDark);
            Box(root.transform, "Band L", new Vector3(-W * 0.32f, H * 0.5f, 0f), new Vector3(0.07f, H + 0.01f, D + 0.02f), gold);
            Box(root.transform, "Band R", new Vector3(W * 0.32f, H * 0.5f, 0f), new Vector3(0.07f, H + 0.01f, D + 0.02f), gold);
            Box(root.transform, "Rim", new Vector3(0f, 0.03f, 0f), new Vector3(W + 0.02f, 0.06f, D + 0.02f), gold);
            // 箱蓋：繞後緣的軸掀開
            var pivot = new GameObject("LidPivot").transform; pivot.SetParent(root.transform, false); pivot.localPosition = new Vector3(0f, H, D * 0.5f);
            Box(pivot, "Lid", new Vector3(0f, 0.11f, -D * 0.5f), new Vector3(W + 0.02f, 0.22f, D + 0.02f), wood);
            Box(pivot, "Lid band L", new Vector3(-W * 0.32f, 0.11f, -D * 0.5f), new Vector3(0.07f, 0.23f, D + 0.04f), gold);
            Box(pivot, "Lid band R", new Vector3(W * 0.32f, 0.11f, -D * 0.5f), new Vector3(0.07f, 0.23f, D + 0.04f), gold);
            Box(pivot, "Lock", new Vector3(0f, 0.02f, -D - 0.02f), new Vector3(0.14f, 0.16f, 0.05f), gold);
            // 打開後：裡面的金光、金幣堆
            var glow = new GameObject("Glow").transform; glow.SetParent(root.transform, false);
            Box(glow, "Coins", new Vector3(0f, H - 0.05f, 0f), new Vector3(W - 0.16f, 0.08f, D - 0.16f), new Color(1f, 0.85f, 0.35f));
            GlowHalo.Create(glow, new Vector3(0f, H + 0.25f, 0f), new Color(1f, 0.85f, 0.4f, 0.75f), 1.6f, 0.3f);
            glow.gameObject.SetActive(false);
            // 沒打開：上面一圈淡淡的光，遠遠看得出是寶箱
            GlowHalo.Create(root.transform, new Vector3(0f, 0.5f, 0f), new Color(1f, 0.9f, 0.5f, 0.35f), 1.4f, 0.2f).name = "Hint";
            return root;
        }

        /// <summary>每幀：箱蓋往目標角度轉；打開後金光</summary>
        static void SetChestOpen(GameObject g, bool open)
        {
            if (!g) return;
            var pivot = g.transform.Find("LidPivot");
            if (pivot) pivot.localRotation = Quaternion.RotateTowards(pivot.localRotation, Quaternion.Euler(open ? -110f : 0f, 0f, 0f), Time.deltaTime * 260f);
            var glow = g.transform.Find("Glow"); if (glow && glow.gameObject.activeSelf != open) glow.gameObject.SetActive(open);
            var hint = g.transform.Find("Hint"); if (hint && hint.gameObject.activeSelf == open) hint.gameObject.SetActive(!open);
        }

        /// <summary>副本大地圖：走道藏的寶箱、寶箱地點的模型（UpdateExploreProps 之後叫）</summary>
        void UpdateChestArt(ExploreRun run)
        {
            if (!run.IsDungeon || run.map.nodeAreas || Me == null) return;
            var me = Me.Feet;
            foreach (var sp in run.map.sparkles)
            {
                if (sp.kind != "chest") continue;
                var g = ExArt("chest:" + sp.id, CreateChestModel, Battle.FlatDistance(sp.pos, me) <= ChestView, sp.pos);
                if (g) { if (!g.name.EndsWith("*")) { g.name += "*"; FaceChest(g, run.map, sp.pos); } SetChestOpen(g, sp.found); }
                // 走到寶箱旁邊就自動打開（也可以點）
                if (!sp.found && Battle.FlatDistance(sp.pos, me) <= 1.3f && run.TapSparkle(sp.id, me) != null) OnDungeonChestOpened(run, sp);
            }
            foreach (var a in run.map.areas)
            {
                if (!a.IsChest) continue;
                var g = ExArt("chestA:" + a.index, CreateChestModel, Battle.FlatDistance(a.center, me) <= ChestView, a.center);
                if (g) { if (!g.name.EndsWith("*")) { g.name += "*"; FaceChest(g, run.map, a.center); } SetChestOpen(g, a.cleared); }
            }
        }

        /// <summary>箱子正面朝向走得到的那一邊</summary>
        static void FaceChest(GameObject g, ExploreMap map, Vector3 at)
        {
            var c = map.Cell(at);
            foreach (var d in ExploreMap.Dirs)
                if (map.Open(c.x + d.x, c.y + d.y)) { g.transform.localRotation = Quaternion.LookRotation(-new Vector3(d.x, 0f, d.y), Vector3.up); return; }
        }

        // ---------------------------------------------------------------- 拿到什麼
        string chestPopTitle; readonly List<(string text, Color c)> chestPopLines = new List<(string, Color)>();
        float chestPopUntil = -1f;

        void ShowChestLoot(string title, Item it, int gold, bool kept)
        {
            chestPopTitle = title; chestPopLines.Clear();
            if (it != null) chestPopLines.Add(($"[{Item.RarityNames[it.rarity]}] {it.FullName}", Item.RarityColors[it.rarity]));
            if (gold > 0) chestPopLines.Add(($"💰 {gold} 金幣", UiKit.Gold));
            chestPopLines.Add((kept ? "（已放進背包）" : "（通關後獲得）", UiKit.Muted));
            chestPopUntil = Time.time + 4f;
            ChatEvent($"🧰 {title}：" + (it != null ? $"[{Item.RarityNames[it.rarity]}] {it.FullName}" : "") + (gold > 0 ? $"、💰 {gold}" : ""));
            Sfx.Play("win", 0.7f, 1.25f); Haptics.Play(Haptics.Kind.Success);
        }

        void DrawChestPopup(float W, float H)
        {
            if (Time.time > chestPopUntil || chestPopTitle == null) return;
            int fs = UiKit.BaseFont;
            float w = Mathf.Min(W * 0.84f, fs * 20f), h = fs * (3.2f + 1.6f * chestPopLines.Count);
            float k = Mathf.Clamp01((chestPopUntil - Time.time) / 0.4f) * Mathf.Clamp01((Time.time - (chestPopUntil - 4f)) / 0.25f);
            var r = new Rect((W - w) * 0.5f, H * 0.3f, w, h);
            var old = GUI.color; GUI.color = new Color(1f, 1f, 1f, k);
            UiKit.MenuCard(r, 0.94f, UiKit.Gold, 0.9f);
            UiKit.Text(new Rect(r.x, r.y + fs * 0.4f, r.width, fs * 2f), "🧰 " + chestPopTitle, Mathf.RoundToInt(fs * 1.15f), UiKit.Gold, TextAnchor.MiddleCenter, true);
            float y = r.y + fs * 2.6f;
            foreach (var (text, c) in chestPopLines) { UiKit.Text(new Rect(r.x + fs, y, r.width - fs * 2f, fs * 1.5f), text, fs, c, TextAnchor.MiddleCenter, true); y += fs * 1.6f; }
            GUI.color = old;
        }

        /// <summary>走道藏的寶箱打開了（副本）：當場決定裝備，跳出拿到什麼</summary>
        void OnDungeonChestOpened(ExploreRun run, ExSparkle s)
        {
            if (!run.IsDungeon || dungeon == null) return;
            if (run.chestsFound > 0) run.chestsFound--;   // 裝備改成這裡直接給（不用等結算再擲）
            ItemGen.SetTheme = battle.Theme;
            var it = ItemGen.Make(dropRng, dungeon.level + battle.levelBoost, Mathf.Max(2, ItemGen.RollRarity(dropRng, 0f, 2)), myClass);
            pendingItems.Add(it);
            ShowChestLoot(s.title ?? "寶箱", it, run.DungeonChestGold, ChapterMode);
        }

        // ---------------------------------------------------------------- 小地圖圖示
        /// <summary>副本大地圖的小地圖：任務點、NPC、沒打開的門、存檔點、寶箱、試煉、首領（走過看到的才標）</summary>
        void DrawDungeonMapIcons(ExploreRun run, Rect box, int x0, int z0, int span, float cs)
        {
            var map = run.map; if (map.dungeon == null) return;
            int ifs = Mathf.RoundToInt(Mathf.Clamp(cs * 1.7f, UiKit.BaseFont * 0.62f, UiKit.BaseFont * 1.05f));
            void Icon(Vector2Int cell, string icon, bool needSeen = true)
            {
                if (cell.x < x0 || cell.y < z0 || cell.x >= x0 + span || cell.y >= z0 + span) return;
                if (needSeen && !run.explored.Contains(cell)) return;
                float px = box.x + (cell.x - x0 + 0.5f) * cs, py = box.y + (span - 1 - (cell.y - z0) + 0.5f) * cs;
                GUI.color = Color.white;
                UiKit.Text(new Rect(px - ifs, py - ifs * 0.75f, ifs * 2f, ifs * 1.5f), icon, ifs, Color.white, TextAnchor.MiddleCenter, true);
            }
            // 沒打開的門
            for (int x = x0; x < x0 + span; x++)
                for (int z = z0; z < z0 + span; z++)
                    if (map.InGrid(x, z) && map.At(x, z) == ExCell.Gate && !map.GateOpen(x, z)) Icon(new Vector2Int(x, z), "🔒");
            // 地點：試煉、首領、寶箱
            foreach (var a in map.areas)
            {
                var c = map.Cell(a.center);
                if (a.IsBoss && !a.cleared) Icon(c, "💀");
                else if (a.IsChest && !a.cleared) Icon(c, "🧰");
                else if (a.IsEvent && !a.cleared) Icon(c, "⭐");
            }
            // 走道藏的寶箱
            foreach (var sp in map.sparkles) if (sp.kind == "chest" && !sp.found) Icon(map.Cell(sp.pos), "🧰");
            // 故事小關的機關：巨石、深坑、石門、火把／石板（SoloBattle.StagePuzzles.cs）
            if (dungeon != null && dungeon.style == "stage") foreach (var (pos, icon) in StagePuzzleIcons()) Icon(map.Cell(pos), icon);
            // 存檔點（脫出點）：清完那個地點才會出現
            foreach (var e in chExits) if (e.area.cleared) Icon(map.Cell(e.pos), "🌀");
            // NPC：有任務可接／可交＝❗，其他＝💬
            foreach (var n in map.npcs)
            {
                if (n.hidden) continue;
                var q = map.Quest(n.quest);
                bool mark = q != null && (q.state == ExQuestState.Offered || q.state == ExQuestState.Ready);
                Icon(map.Cell(n.pos), mark ? "❗" : "💬");
            }
            GUI.color = Color.white;
        }
    }
}
