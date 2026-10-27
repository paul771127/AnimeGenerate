using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色畫面的「🐾 寵物」分頁（資料 Pets.cs）：
    ///   上面：寵物收藏（點一隻看詳細）；中間：選中那隻的能力、經驗、技能、餵食、設為出戰、放生；
    ///   學滿 3 招又抽到新招：選要替換哪一招或放棄；下面：在城鎮裡可以買這一章的誘餌和飼料、調配高級飼料和菁英誘餌。
    ///   戰鬥中只能看（不能換出戰、不能餵）。
    /// </summary>
    public partial class CharacterPanel
    {
        /// <summary>現在在哪個城鎮（0～4；不在城鎮＝−1：不能買）——SoloBattle 接上</summary>
        public Func<int> PetTown;
        /// <summary>可以改寵物（不在戰鬥中）——SoloBattle 接上</summary>
        public Func<bool> PetsEditable;

        Vector2 petScroll; float petContentH;
        long petSel; long releaseArm; float releaseArmUntil;
        readonly System.Random petRng = new System.Random();

        void DrawPets(Rect view, CharacterProfile p, int fs, int fsS, float gap)
        {
            var list = Pets.Load();
            bool edit = PetsEditable == null || PetsEditable();
            int town = PetTown != null ? PetTown() : -1;
            long act = Pets.ActiveUid;
            float lh = fs * 1.85f, cw = view.width - fs * 0.85f;
            var content = new Rect(0, 0, cw, Mathf.Max(view.height, petContentH));
            petScroll = UiKit.DragScroll(view, petScroll, content);
            petScroll = GUI.BeginScrollView(view, petScroll, content, false, true);
            float y = 0f;
            void Line(string t, Color c, bool bold = false, float h = 1f)
            {
                float pad=bold?gap:gap*.35f, height=Mathf.Max(lh*h,PetPanelArt.TextHeight(t,fsS,cw-pad*2)+gap*.6f);
                if(bold){y+=gap*.5f;UiKit.Card(new Rect(0,y,cw,height),.55f,c,.35f);}
                UiKit.Text(new Rect(pad,y,cw-pad*2,height),t,fsS,c,TextAnchor.MiddleLeft,bold);y+=height+gap*.2f;
            }
            bool changed = false;

            // ---- 收藏
            Line($"🐾 寵物收藏 {list.Count}/{Pets.MaxRoster}" + (edit ? "" : "　（戰鬥中只能看）"), UiKit.Gold, true);
            if (list.Count == 0)
            {
                Line("還沒有寵物。戰鬥中丟出對應的誘餌，趁怪物吃餌的 3 秒把牠的血壓到 30% 以下，就有機會收服。", UiKit.Muted, false, 2f);
                Line("誘餌：打倒那種怪有機率掉，城鎮的這一頁也買得到（只賣那一章的）。", UiKit.Muted, false, 1.2f);
            }
            float w = (cw - gap) / 2f, h = lh * 1.8f;
            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i]; var sp = d.Species;
                var r = new Rect((i % 2) * (w + gap), y + (i / 2) * (h + gap * 0.4f), w, h);
                bool sel = petSel == d.uid, on = act == d.uid;
                var col = sp.elite ? new Color(1f, 0.82f, 0.4f) : new Color(0.45f, 1f, 0.8f);
                UiKit.Card(r, sel ? 0.85f : 0.5f, col, sel ? 0.9f : 0.35f);
                UiKit.Text(new Rect(r.x + gap * 0.5f, r.y, r.width - gap, r.height), $"{(on ? "⚔ " : "")}{d.Name}　Lv{d.level}\n{(sp.elite ? "菁英・" : "")}{Pets.PoolName(sp.pool)}系" + (d.pendingLearn != null ? "　✨有新招" : ""), fsS, col, TextAnchor.MiddleLeft, on);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) petSel = d.uid;
            }
            y += ((list.Count + 1) / 2) * (h + gap * 0.4f) + gap;

            // ---- 選中的那隻
            var cur = list.Find(x => x.uid == petSel);
            if (cur != null)
            {
                var sp = cur.Species;
                int lvNow = Pets.EffectiveLevel(cur, p.level);
                PetPanelArt.Hero(new Rect(0,y,cw,lh*3.6f),cur.kind,cur.Name,$"{(sp.elite ? "菁英旅伴" : "冒險旅伴")} · {Pets.PoolName(sp.pool)}\n{(act==cur.uid ? "已設為出戰" : "休息中")} · Lv{cur.level}",fs,sp.elite?UiKit.Gold:new Color(.45f,1,.8f));
                y+=lh*3.6f+gap;
                Line($"{(sp.elite ? "🌟 菁英　" : "")}{cur.Name}（{sp.Name}・{Pets.PoolName(sp.pool)}系）", sp.elite ? new Color(1f, 0.82f, 0.4f) : new Color(0.45f, 1f, 0.8f), true);
                Line($"Lv{cur.level}" + (lvNow < cur.level ? $"（出戰時 Lv{lvNow}：不超過主人等級）" : "") + $"　HP {Pets.MaxHp(sp, lvNow)}　攻擊 {Pets.Atk(sp, lvNow)}　普攻每 {Pets.AttackGap(sp):0.#} 秒", Color.white);
                if (cur.level < Pets.MaxLevel) { UiKit.Bar(new Rect(0, y, cw, lh * 0.5f), cur.xp / (float)Pets.XpToNext(cur.level), new Color(0.45f, 1f, 0.8f), $"經驗 {cur.xp}/{Pets.XpToNext(cur.level)}（出戰時分到主人經驗的 40%）", fsS); y += lh * 0.6f; }
                else Line("已經最高等級（餵食只會學技能）", UiKit.Muted);
                Line($"技能 {cur.skills.Count}/{Pets.MaxSkills}（自動施放）", UiKit.Gold, true);
                foreach (var id in cur.skills) { var k = Pets.Skill(id); if (k != null) Line($"・{k.name}：{k.Desc}", new Color(0.88f, 0.92f, 1f), false, 1.1f); }
                var unlearned = Pets.PoolOf(sp.pool).FindAll(k => !cur.skills.Contains(k.id));
                if (unlearned.Count > 0) Line("還能學：" + string.Join("、", unlearned.ConvertAll(k => k.name)), UiKit.Muted);

                // 學滿又抽到新招：替換哪一招或放棄
                var nk = Pets.Skill(cur.pendingLearn);
                if (nk != null)
                {
                    Line($"✨ 想學「{nk.name}」：{nk.Desc}", new Color(1f, 0.9f, 0.5f), true, 1.2f);
                    Line("技能已經 3 招：選一招換掉，或放棄這一招", new Color(1f, 0.9f, 0.5f));
                    float bw = (cw - gap * cur.skills.Count) / (cur.skills.Count + 1);
                    for (int i = 0; i < cur.skills.Count; i++)
                    {
                        var old = Pets.Skill(cur.skills[i]);
                        if (UiKit.Button(new Rect(i * (bw + gap), y, bw, lh * 1.2f), $"換掉{old?.name}", fsS, false, new Color(1f, 0.8f, 0.45f), edit))
                        { cur.skills[i] = nk.id; cur.pendingLearn = null; changed = true; Toast($"{cur.Name} 學會了「{nk.name}」"); }
                    }
                    if (UiKit.Button(new Rect(cur.skills.Count * (bw + gap), y, bw, lh * 1.2f), "放棄", fsS, false, null, edit)) { cur.pendingLearn = null; changed = true; }
                    y += lh * 1.3f;
                }

                // 餵食
                Line("餵食（加經驗，有機率學新招）", UiKit.Gold, true);
                float fw = (cw - gap * 2) / 3f;
                for (int i = 0; i < Pets.Foods.Length; i++)
                {
                    var f = Pets.Foods[i]; int n = Economy.Count(f.id);
                    bool can = edit && n > 0 && cur.pendingLearn == null;
                    if (PetPanelArt.ItemButton(new Rect(i * (fw + gap), y, fw, lh * 2.5f), f.id, $"{f.name}×{n}\n+{f.xp}經驗\n學招 {f.learn * 100f:0}%", Mathf.RoundToInt(fsS * 0.85f), new Color(0.45f, 1f, 0.8f), can))
                    {
                        Economy.AddItem(f.id, -1);
                        var (ups, got, pend) = Pets.Feed(cur, f, petRng);
                        changed = true;
                        Toast($"{cur.Name} 吃了{f.name}" + (ups > 0 ? $"，升到 Lv{cur.level}！" : "") + (got != null ? (pend ? $"　✨ 想學「{Pets.Skill(got).name}」（選要換掉哪一招）" : $"　✨ 學會了「{Pets.Skill(got).name}」！") : ""));
                        Sfx.Play(got != null ? "levelup" : "heal", 0.6f, 1.2f);
                    }
                }
                y += lh * 2.6f;
                if (cur.pendingLearn != null) Line("先決定要不要學新招，才能再餵", UiKit.Muted);

                // 出戰、放生
                float hw = (cw - gap) / 2f;
                bool isOn = act == cur.uid;
                if (UiKit.Button(new Rect(0, y, hw, lh * 1.3f), isOn ? "取消出戰" : "⚔ 設為出戰", fsS, isOn, new Color(0.45f, 1f, 0.8f), edit))
                { Pets.ActiveUid = isOn ? 0 : cur.uid; Toast(isOn ? "沒有帶寵物出戰" : $"{cur.Name} 下一場出戰"); }
                bool armed = releaseArm == cur.uid && Time.time < releaseArmUntil;
                if (UiKit.Button(new Rect(hw + gap, y, hw, lh * 1.3f), armed ? "再按一次：放生" : "放生", fsS, armed, new Color(1f, 0.5f, 0.45f), edit))
                {
                    if (armed) { list.Remove(cur); if (isOn) Pets.ActiveUid = 0; petSel = 0; changed = true; Toast($"放生了{cur.Name}"); }
                    else { releaseArm = cur.uid; releaseArmUntil = Time.time + 3f; }
                }
                y += lh * 1.5f;
            }
            if (changed) Pets.Save(list);

            // ---- 身上的誘餌
            var baits = Pets.All.FindAll(s => Economy.Count(s.bait) > 0);
            Line($"🍖 身上的誘餌（{baits.Count} 種）", UiKit.Gold, true);
            if (baits.Count == 0) Line("沒有誘餌", UiKit.Muted);
            else Line(string.Join("　", baits.ConvertAll(s => $"{s.baitName}（{s.Name}）×{Economy.Count(s.bait)}")), new Color(1f, 0.88f, 0.6f), false, Mathf.Ceil(baits.Count / 2f));

            // ---- 城鎮：買誘餌、飼料，調配
            if (town >= 0 && edit)
            {
                y += gap;
                Line($"🛒 這個城鎮賣的（💰 {Economy.Gold}）", UiKit.Gold, true);
                var shop = Pets.TownBaits(town);
                float bw2 = (cw - gap) / 2f;
                for (int i = 0; i < shop.Count; i++)
                {
                    var s = shop[i];
                    var r = new Rect((i % 2) * (bw2 + gap), y + (i / 2) * (lh * 2.1f + gap * 0.3f), bw2, lh * 2.1f);
                    if (PetPanelArt.ItemButton(r, s.bait, $"{s.baitName}\n{s.Name}\n{s.price} 金", Mathf.RoundToInt(fsS * 0.85f), new Color(1f, 0.8f, 0.45f), Economy.Gold >= s.price))
                    { Economy.Gold -= s.price; Economy.AddItem(s.bait, 1); Toast($"買了 {s.baitName}"); Sfx.Play("coin", 0.6f); }
                }
                y += ((shop.Count + 1) / 2) * (lh * 2.1f + gap * 0.3f) + gap * 0.5f;
                var basic = Pets.Food("food_basic"); var fine = Pets.Food("food_fine"); var star = Pets.Food("food_star");
                float tw = (cw - gap * 2) / 3f;
                if (UiKit.Button(new Rect(0, y, tw, lh * 1.5f), $"{basic.icon}買{basic.name}\n{basic.price} 金", Mathf.RoundToInt(fsS * 0.85f), false, new Color(0.45f, 1f, 0.8f), Economy.Gold >= basic.price))
                { Economy.Gold -= basic.price; Economy.AddItem(basic.id, 1); Toast($"買了 {basic.name}"); Sfx.Play("coin", 0.6f); }
                bool canFine = Economy.Count(basic.id) >= 3 && Economy.Gold >= 300;
                if (UiKit.Button(new Rect(tw + gap, y, tw, lh * 1.5f), $"⚗調配{fine.name}\n旅伴糧3＋300金", Mathf.RoundToInt(fsS * 0.85f), false, new Color(0.45f, 1f, 0.8f), canFine))
                { Economy.AddItem(basic.id, -3); Economy.Gold -= 300; Economy.AddItem(fine.id, 1); Toast($"調配了 {fine.name}"); Sfx.Play("heal", 0.6f); }
                bool canStar = Economy.Count(fine.id) >= 4 && Economy.Count(Pets.SealId) >= 2 && Economy.Gold >= 800;
                if (UiKit.Button(new Rect((tw + gap) * 2, y, tw, lh * 1.5f), $"⚗調配{star.name}\n精養糧4＋印片2＋800金", Mathf.RoundToInt(fsS * 0.85f), false, new Color(0.45f, 1f, 0.8f), canStar))
                { Economy.AddItem(fine.id, -4); Economy.AddItem(Pets.SealId, -2); Economy.Gold -= 800; Economy.AddItem(star.id, 1); Toast($"調配了 {star.name}"); Sfx.Play("heal", 0.6f); }
                y += lh * 1.6f;
                // 菁英誘餌：這一章的菁英，馴獸印片 8 枚＋金幣
                var elites = Pets.All.FindAll(s => s.elite && s.chapter == town);
                if (elites.Count > 0)
                {
                    Line($"🔰 調配菁英誘餌（馴獸印片 {Economy.Count(Pets.SealId)} 枚；每份 {Pets.SealsPerEliteBait} 枚＋金幣）", UiKit.Gold, true);
                    for (int i = 0; i < elites.Count; i++)
                    {
                        var s = elites[i];
                        var r = new Rect((i % 2) * (bw2 + gap), y + (i / 2) * (lh * 2.1f + gap * 0.3f), bw2, lh * 2.1f);
                        bool can = Economy.Count(Pets.SealId) >= Pets.SealsPerEliteBait && Economy.Gold >= s.price;
                        if (PetPanelArt.ItemButton(r, s.bait, $"{s.baitName}\n{s.Name}\n{s.price} 金", Mathf.RoundToInt(fsS * 0.85f), new Color(1f, 0.82f, 0.4f), can))
                        { Economy.AddItem(Pets.SealId, -Pets.SealsPerEliteBait); Economy.Gold -= s.price; Economy.AddItem(s.bait, 1); Toast($"調配了 {s.baitName}"); Sfx.Play("heal", 0.6f); }
                    }
                    y += ((elites.Count + 1) / 2) * (lh * 2.1f + gap * 0.3f);
                }
            }
            else if (edit) Line("（在城鎮裡打開這一頁，可以買誘餌、飼料）", UiKit.Muted);

            petContentH = y + gap;
            GUI.EndScrollView();
        }
    }
}
