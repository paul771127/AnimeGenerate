using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 裝備分頁的精魄（Souls.cs）：選中裝備時可以打開「💠 精魄」——上面是這件裝備的精魄孔，
    /// 下面是背包裡的精魄（點兩下＝鑲到空孔上；鑲上後不能取出）。沒選裝備時可以打開「精魄圖鑑」：所有精魄的來源、掉落率、效果、擁有幾個。
    /// </summary>
    public partial class CharacterPanel
    {
        bool soulOpen;           // 精魄視窗開著（取代裝備說明那一塊）
        Item soulFor;            // 正在鑲嵌的裝備（null＝圖鑑）
        Vector2 soulScroll;
        string soulArm; float soulArmUntil;   // 鑲嵌要按兩次確認（鑲上後不能取出）

        static string SlotDots(Item it)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < it.soulSlots; i++) sb.Append(i < it.souls.Count ? "◆" : "◇");
            return sb.Length > 0 ? sb.ToString() : "（無）";
        }

        /// <summary>裝備說明裡的精魄那幾行</summary>
        static void SoulLines(Item it, List<string> lines)
        {
            lines.Add($"💠 精魄孔 {it.souls.Count}/{it.soulSlots}　{SlotDots(it)}（鑲{Souls.PartNames[(int)Souls.PartOf(it.slot)]}類精魄）");
            foreach (var id in it.souls) { var d = Souls.Get(id); if (d != null) lines.Add($"　◆ {d.name}：{Souls.Desc(d)}"); }
        }

        /// <summary>精魄視窗（畫在裝備說明那一塊）；有改動回傳 true</summary>
        bool DrawSouls(Rect dr, CharacterProfile p, int fs, int fsS, float gap)
        {
            bool changed = false;
            var it = soulFor;
            UiKit.Card(dr, 0.95f, new Color(0.55f, 0.75f, 1f), 0.8f);
            float bh = fs * 1.6f;
            if (UiKit.Button(new Rect(dr.xMax - gap - fs * 5f, dr.y + gap * 0.4f, fs * 5f, fs * 1.3f), "◀ 返回", fsS)) { soulOpen = false; return false; }
            UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.4f, dr.width - fs * 6f, fs * 1.3f),
                it != null ? $"💠 精魄：{it.FullName}" : "💠 精魄圖鑑（打倒怪物有機率掉落）", fs, new Color(0.7f, 0.85f, 1f), TextAnchor.MiddleLeft, true);
            float y = dr.y + fs * 1.8f;
            if (it != null)
            {
                // 精魄孔：已鑲的（不能取出）／空的
                UiKit.Text(new Rect(dr.x + gap, y, dr.width - gap * 2, fs * 1.1f), it.soulSlots == 0 ? "這件裝備沒有精魄孔（孔數是掉落時隨機的，越稀有的裝備上限越高）" : $"精魄孔 {it.souls.Count}/{it.soulSlots}　只能鑲{Souls.PartText(Souls.PartOf(it.slot))}類精魄　⚠ 鑲上後不能取出", fsS, new Color(0.85f, 0.88f, 1f));
                y += fs * 1.2f;
                if (it.soulSlots > 0)
                {
                    float sw = (dr.width - gap * (it.soulSlots + 1)) / it.soulSlots;
                    for (int i = 0; i < it.soulSlots; i++)
                    {
                        var sr = new Rect(dr.x + gap + i * (sw + gap), y, sw, bh);
                        if (i < it.souls.Count)
                        {
                            var d = Souls.Get(it.souls[i]);
                            UiKit.Card(sr, 0.9f, d != null ? d.color : UiKit.Muted, 1f);
                            UiKit.Text(sr, "◆ " + (d != null ? d.name : "?"), fsS, Color.white, TextAnchor.MiddleCenter, true);
                        }
                        else { UiKit.Card(sr, 0.4f, UiKit.Muted, 0.4f); UiKit.Text(sr, "◇ 空", fsS, Color.gray, TextAnchor.MiddleCenter); }
                    }
                    y += bh + gap * 0.5f;
                }
            }
            // 背包裡的精魄（鑲嵌模式：點＝鑲上）／圖鑑（全部，沒有的寫來源）
            var list = new List<SoulDef>();
            foreach (var d in Souls.All) if (it == null || (p.souls.TryGetValue(d.id, out int n) && n > 0)) list.Add(d);
            if (it != null) list.Sort((x, y) => (x.part == Souls.PartOf(it.slot) ? 0 : 1).CompareTo(y.part == Souls.PartOf(it.slot) ? 0 : 1));   // 能鑲的排前面
            else list.Sort((x, y) => x.part.CompareTo(y.part));   // 圖鑑：武器、防具、飾品分組
            var view = new Rect(dr.x + gap, y, dr.width - gap * 2, dr.yMax - y - gap * 0.4f);
            if (it != null && list.Count == 0)
            {
                UiKit.Text(view, "背包裡還沒有精魄。打倒怪物有機率掉落（小怪 0.1～0.5%、Boss 0.5%，幸運越高越容易），過關後拿到。", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperLeft);
                return changed;
            }
            float rowH = fs * 2.6f, cw = view.width - fs * 0.85f;
            var content = new Rect(0, 0, cw, Mathf.Max(view.height, list.Count * (rowH + gap * 0.3f)));
            soulScroll = UiKit.DragScroll(view, soulScroll, content);
            soulScroll = GUI.BeginScrollView(view, soulScroll, content, false, true);
            for (int k = 0; k < list.Count; k++)
            {
                var d = list[k];
                int have = p.souls.TryGetValue(d.id, out int c) ? c : 0;
                var rr = new Rect(0, k * (rowH + gap * 0.3f), cw, rowH);
                bool partOk = it == null || d.part == Souls.PartOf(it.slot);
                bool canPut = it != null && have > 0 && partOk && it.souls.Count < it.soulSlots;
                if (it != null)
                {
                    bool armed = soulArm == d.id && Time.time < soulArmUntil;
                    if (UiKit.Button(rr, "", fsS, armed, d.color, canPut))
                    {
                        if (!armed) { soulArm = d.id; soulArmUntil = Time.time + 3f; Toast($"再按一次「{d.name}」確定鑲上（鑲上後不能取出）"); }
                        else if (Souls.Socket(p, it, d.id, out var why)) { soulArm = null; changed = true; Toast($"鑲上了 {d.name}"); Sfx.Play("charged", 0.7f, 1.2f); }
                        else Toast(why);
                    }
                }
                else UiKit.Card(rr, have > 0 ? 0.75f : 0.4f, d.color, have > 0 ? 0.8f : 0.3f);
                string title = (d.IsBoss ? "👑 " : "") + d.name + (have > 0 ? $" ×{have}" : "") + (partOk ? "" : $"　（只能鑲{Souls.PartNames[(int)d.part]}）") + (it != null && soulArm == d.id && Time.time < soulArmUntil ? "　← 再按一次確定" : "");
                UiKit.Text(new Rect(rr.x + gap * 0.5f, rr.y, rr.width - gap, fs * 1.2f), title, fsS, (have > 0 && partOk) || it == null ? d.color : Color.gray, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(rr.x + gap * 0.5f, rr.y + fs * 1.1f, rr.width - gap, rowH - fs * 1.15f), Souls.Desc(d) + "　（" + Souls.DropText(d) + "）", Mathf.RoundToInt(fsS * 0.9f), new Color(0.85f, 0.88f, 1f), TextAnchor.UpperLeft, false, 0.5f);
            }
            GUI.EndScrollView();
            return changed;
        }
    }
}
