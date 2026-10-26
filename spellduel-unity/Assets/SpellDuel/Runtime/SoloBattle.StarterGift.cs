using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 轉職完成（新手指引的最後一步）的禮物：小鐵送一把新職業的藍色（稀有）武器（1～10 級、不是套裝、現在就穿得上），
    /// 老焰送 500 金幣；接著一步一步帶玩家穿上武器、到鐵匠鋪強化（SoloBattle.Walkthrough.cs）。每個角色只送一次（sd_gift_job）。
    /// 更新前就已經轉職的角色：回到村子時補送。
    /// </summary>
    public partial class SoloBattle
    {
        public const int GiftGold = 500, GiftStones = 3;
        static bool GiftGiven { get { try { return PlayerPrefs.GetInt(CharacterSlots.Key("sd_gift_job"), 0) == 1; } catch { return true; } } }
        static void MarkGift() { try { PlayerPrefs.SetInt(CharacterSlots.Key("sd_gift_job"), 1); PlayerPrefs.Save(); } catch { } }

        /// <summary>這個職業的藍色武器（初心者：轉職後五個職業都能用）</summary>
        public static Item StarterWeapon(CharacterProfile p, System.Random rng)
        {
            string cls = p.cls == Skills.Novice ? "swordsman" : p.cls;
            int ilvl = Mathf.Clamp(p.level - 2, 1, 10);   // 需求等級＝物品等級＋2：剛好是現在的等級
            Item it = null;
            for (int i = 0; i < 400; i++)
            {
                var c = ItemGen.Make(rng, ilvl, 2, cls);
                if (c.slot == Slot.Weapon && string.IsNullOrEmpty(c.setId) && c.ClassOk(cls)) { it = c; break; }
            }
            if (it == null) return null;
            it.reqLevel = Mathf.Min(it.reqLevel, Mathf.Max(1, p.level));
            if (it.reqValue > 0) it.reqValue = Mathf.Min(it.reqValue, p.StatForReq(it));   // 現在就穿得上
            it.name = "小鐵打的" + it.name;
            return it;
        }

        /// <summary>這一次送的武器（指引玩家穿上、強化它）</summary>
        Item giftWeapon;

        /// <summary>送禮（還沒送過、而且已經轉職）；then：禮物對話結束後</summary>
        bool TryStarterGift(Action then)
        {
            var p = Progress.Char(myClass);
            if (GiftGiven || p == null || p.cls == Skills.Novice) return false;
            var w = StarterWeapon(p, new System.Random());
            if (w == null) return false;   // 產生不出來（不應該發生）：下次回村子再試，不要白白標記送過
            MarkGift();
            giftWeapon = w; GiftUid = w.uid;
            bool inBag = Progress.AddToBagOrMail(w, "小鐵的禮物");
            Economy.Gold += GiftGold;
            Crafting.Give("refine", GiftStones);   // 精煉石：新手指引的強化要用（之後打岩石類敵人掉鐵礦石，5 個找鐵匠合成）
            Sfx.Play("coin", 0.8f, 1.1f); Haptics.Play(Haptics.Kind.Success);
            // 重新出現裝備、鐵匠鋪的說明卡
            foreach (var id in new[] { "char", "char_gear", "smith", "new_item" }) try { PlayerPrefs.DeleteKey(CharacterSlots.Key("sd_tip_" + id)); } catch { }
            string wn = $"［{Item.RarityNames[w.rarity]}］{w.name}";
            var lines = new List<StoryLine>
            {
                new StoryLine { speaker = "小鐵", expr = "smile", text = "喂，{玩家}！聽說你轉職了？這把是我跟師父一起打的——剛好適合你的新職業！" },
                new StoryLine { speaker = "", text = $"🎁 得到了 {wn}" + (inBag ? "（放進背包了）" : "（背包滿了，寄到信箱：「📜 角色」→「🛡 裝備」→「📬 信箱」領取）") },
                new StoryLine { speaker = "老焰", expr = "smile", text = $"恭喜你，新手的日子結束了。這 {GiftGold} 金幣是村裡的心意，拿去把武器強化一下。" },
                new StoryLine { speaker = "", text = $"💰 得到了 {GiftGold} 金幣" },
                new StoryLine { speaker = "小鐵", expr = "serious", text = "先教你怎麼穿：按畫面上的「📜 角色」→ 上面的「🛡 裝備」分頁。" },
                new StoryLine { speaker = "小鐵", expr = "normal", text = "下面是背包。點我送你的那把武器，看一下比較，再按「穿上」就好。穿不上的話，它會寫為什麼。" },
                new StoryLine { speaker = "老焰", expr = "serious", text = "再來是強化。到村裡的「⚒ 鐵匠鋪」，選要強化的裝備，付金幣就能讓它變強。" },
                new StoryLine { speaker = "小鐵", expr = "smile", text = "+5 以前保證不會失敗！再往上就有機會失敗，記得多帶點錢。先把這把武器強化個幾次吧！" },
            };
            ShowDialogue(lines, () =>
            {
                Say($"📜 下一步：「📜 角色」→「🛡 裝備」穿上{wn}，再到「⚒ 鐵匠鋪」強化它", 8f);
                then?.Invoke();
            });
            return true;
        }
    }
}
