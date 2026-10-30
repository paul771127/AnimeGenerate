using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 寵物系統接到戰鬥畫面（資料 Pets.cs、戰鬥 Combat.Pets.cs、角色畫面 CharacterPanel.Pets.cs）：
    ///   開打時叫出出戰的寵物；寵物活著時分到主人經驗的 40%；收服成功存進收藏；
    ///   誘餌、馴獸印片從對應的怪掉，飼料在通關時掉；戰鬥畫面上的誘餌按鈕。
    /// </summary>
    public partial class SoloBattle
    {
        Battle petBattle;          // 已經處理過寵物出戰的那一場
        float petXpAcc;            // 還沒存進寵物的經驗
        long petXpUid;             // 那一份經驗是哪一隻的
        bool petDownSaid;

        /// <summary>這個模式可以帶寵物（故事、副本、無盡、無盡塔；試煉、教學、每日挑戰不行）</summary>
        bool PetsAllowed => prof != null && endless && classTrial == null && !tutorial && !IsTrial && !IsJobTrial && !Watching;

        /// <summary>城鎮裡跟著走的寵物＝出戰中的那一隻（每秒重讀一次寵物收藏，換出戰、放生後會跟著變）</summary>
        float townPetNext; int townPetKind = -1; bool townPetElite;
        void UpdateTownPet()
        {
            if (Time.time >= townPetNext)
            {
                townPetNext = Time.time + 1f;
                townPetKind = -1; townPetElite = false;
                long act = prof != null ? Pets.ActiveUid : 0;
                if (act != 0) { var d = Pets.Load().Find(x => x.uid == act); if (d?.Species != null) { townPetKind = d.kind; townPetElite = d.Species.elite; } }
            }
            townWalk.PetKind = townPetKind; townWalk.PetElite = townPetElite;
        }

        /// <summary>每幀（戰鬥中）：新的一場就叫出寵物；寵物倒下提示一次</summary>
        void UpdatePets()
        {
            if (battle == null || phase != Phase.Fighting) return;
            if (petBattle != battle)
            {
                FlushPetXp();
                petBattle = battle; petDownSaid = false;
                battle.OnPetCaught += OnPetCaught;
                if (!PetsAllowed) return;
                battle.captureLuck = prof.Total(Stat.Luk);
                long act = Pets.ActiveUid;
                var d = act != 0 ? Pets.Load().Find(x => x.uid == act) : null;
                if (d != null) battle.SpawnPet(d, prof.level);
                return;
            }
            var pb = battle.petBody;
            if (pb != null && !pb.Alive && !petDownSaid)
            {
                petDownSaid = true;
                Say($"🐾 {pb.name.Replace("🐾 ", "")}倒下了：這一場不能再出戰（下一場會回來）", 3f);
                FlushPetXp();
            }
        }

        /// <summary>主人拿到經驗時（TrackProgress）：寵物活著就分 40%</summary>
        void PetShareXp(int dx)
        {
            var pb = battle != null ? battle.petBody : null;
            if (pb == null || !pb.Alive || dx <= 0) return;
            if (petXpUid != pb.petUid) { FlushPetXp(); petXpUid = pb.petUid; }
            petXpAcc += dx * Pets.XpShare;
            if (petXpAcc >= 30f) FlushPetXp();
        }

        /// <summary>把累積的寵物經驗存進存檔（升級就提示）</summary>
        void FlushPetXp()
        {
            int add = Mathf.FloorToInt(petXpAcc);
            if (add <= 0 || petXpUid == 0) return;
            petXpAcc -= add;
            var list = Pets.Load(); var d = list.Find(x => x.uid == petXpUid);
            if (d == null) return;
            int ups = Pets.AddXp(d, add);
            Pets.Save(list);
            if (ups > 0) floaters.Add(new Floater { text = $"🐾 {d.Name} 升到 Lv{d.level}！", color = new Color(0.45f, 1f, 0.8f), born = Time.time, screen = true });
        }

        void OnPetCaught(int kind, Vector3 at)
        {
            var d = Pets.AddNew(kind, dropRng);
            if (d == null) { Say($"🐾 寵物收藏滿了（{Pets.MaxRoster} 隻）：先到「📜 角色」→「🐾 寵物」放生一些", 3f); return; }
            Say($"🐾 收服了{d.Name}！到「📜 角色」→「🐾 寵物」設為出戰（下一場起）", 3.5f);
            Sfx.Play("levelup", 0.7f, 1.2f); Haptics.Play(Haptics.Kind.Success);
            MinionRig.SpawnFx(WorldFrame.FromWorld(at));
        }

        /// <summary>打倒一隻怪：對應的誘餌（一般 8%、菁英 1.5%）、菁英掉馴獸印片（20%）；跟其他掉落分開擲</summary>
        void RollPetDrops(int kind)
        {
            var sp = Pets.Get(kind);
            if (sp == null || prof == null) return;
            if (dropRng.NextDouble() < (sp.elite ? Pets.EliteBaitDrop : Pets.BaitDrop))
            {
                Economy.AddItem(sp.bait, 1);
                floaters.Add(new Floater { text = $"🍖 得到 {sp.baitName}（{sp.Name}的誘餌）", color = new Color(1f, 0.85f, 0.5f), born = Time.time, screen = true });
            }
            if (sp.elite && dropRng.NextDouble() < Pets.SealDrop) { Economy.AddItem(Pets.SealId, 1); floaters.Add(new Floater { text = "🔰 得到 馴獸印片", color = new Color(1f, 0.85f, 0.5f), born = Time.time, screen = true }); }
        }

        /// <summary>通關掉飼料：故事關卡 20% 旅伴糧；副本 25% 精養糧、8% 星露糧</summary>
        void RollFoodDrops()
        {
            if (battle == null || !battle.stageCleared || prof == null) return;
            void Give(string id) { var f = Pets.Food(id); Economy.AddItem(id, 1); floaters.Add(new Floater { text = $"{f.icon} 得到 {f.name}（寵物飼料）", color = new Color(0.45f, 1f, 0.8f), born = Time.time, screen = true }); }
            if (dungeon != null) { if (dropRng.NextDouble() < 0.25) Give("food_fine"); if (dropRng.NextDouble() < 0.08) Give(Pets.StarFood((PetPool)dropRng.Next(8)).id); }   // 星露糧：屬性隨機
            else if (storyStage != null && dropRng.NextDouble() < 0.2) Give("food_basic");
        }

        /// <summary>戰鬥畫面：身上有、而且場上有對象的誘餌，一排按鈕；回傳用掉的高度</summary>
        float DrawBaitButtons(Rect row, float gap, int fsS)
        {
            if (battle == null || !PetsAllowed || battle.remoteEnemy) return 0f;
            var list = new List<PetSpecies>();
            foreach (var sp in Pets.All) if (Economy.Count(sp.bait) > 0 && battle.CanLure(sp.kind)) list.Add(sp);
            if (list.Count == 0) return 0f;
            float bw = Mathf.Min((row.width - gap * (list.Count - 1)) / list.Count, row.width * 0.32f), x = row.x;
            foreach (var sp in list)
            {
                var r = new Rect(x, row.y, bw, row.height);
                if (UiKit.Button(r, $"🍖 {sp.Name}×{Economy.Count(sp.bait)}", Mathf.RoundToInt(fsS * 0.85f), false, new Color(1f, 0.8f, 0.45f)))
                {
                    if (battle.ThrowBait(sp.kind)) { Economy.AddItem(sp.bait, -1); Sfx.Play("dodge", 0.6f, 1.3f); }
                    else Say("場上沒有可以用這個誘餌的對象", 1.5f);
                }
                x += bw + gap;
            }
            return row.height + gap * 0.3f;
        }
    }
}
