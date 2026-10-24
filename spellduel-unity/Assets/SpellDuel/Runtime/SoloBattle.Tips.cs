using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 情境教學：第一次碰到某個畫面或某件事的時候，才跳出一張說明卡（按「知道了」關掉，之後不再出現）。
    /// 例如第一次打開「📋 清單」、走到村口選關卡、出發前的整備、角色畫面的三個分頁、第一次拿到裝備、
    /// 學會新招式、有技能點、轉職完成、第一次看結算。
    /// 每個角色各自記（sd_tip_*）；「不再顯示提示」是整支手機的設定（sd_tips_off）；重看新手導覽會把提示也重置。
    /// 說明卡畫在其他介面的最上面，卡片範圍內的點擊不會穿透到底下的按鈕。戰鬥中、對話、影片時不出現。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>所有提示的 id（重置用）</summary>
        static readonly string[] TipIds = { "list", "gate", "prep", "char", "char_stats", "char_tree", "char_gear", "new_item", "skill_pt", "stat_pt", "new_skill", "job", "result", "dungeon", "smith", "shop", "guild", "soul" };

        string tipId, tipTitle, tipText;   // 正在顯示的提示
        int tipPress = -1;                  // 按下去的按鈕（0 知道了、1 不再顯示）
        int tipBagSeen = -1, tipKnownSeen = -1;   // 上次看到的背包件數、學會的招式數（變多＝拿到新東西）
        Rect tipRect, tipOkRect, tipOffRect;

        static bool TipsOff
        {
            get { try { return PlayerPrefs.GetInt("sd_tips_off", 0) == 1; } catch { return false; } }
            set { try { PlayerPrefs.SetInt("sd_tips_off", value ? 1 : 0); PlayerPrefs.Save(); } catch { } }
        }
        static bool TipSeen(string id) { try { return PlayerPrefs.GetInt(CharacterSlots.Key("sd_tip_" + id), 0) == 1; } catch { return true; } }
        static void MarkTip(string id) { try { PlayerPrefs.SetInt(CharacterSlots.Key("sd_tip_" + id), 1); PlayerPrefs.Save(); } catch { } }

        /// <summary>重看新手導覽時：情境提示也重新出現</summary>
        static void ResetTips()
        {
            try { foreach (var id in TipIds) PlayerPrefs.DeleteKey(CharacterSlots.Key("sd_tip_" + id)); PlayerPrefs.DeleteKey("sd_tips_off"); PlayerPrefs.Save(); } catch { }
        }

        /// <summary>現在的畫面該出現哪一張提示（沒有＝null）。照順序找第一張還沒看過、條件成立的。</summary>
        (string id, string title, string text)? CurrentTip()
        {
            if (TipsOff || StoryVideo.Active || dlg != null || infoSkill != null) return null;
            if (phase == Phase.Over && !NetMode)
                return Want("result", "🏁 結算畫面",
                    "・通關才拿得到這一關的戰利品（裝備、金幣）；中途輸了或離開，打到一半的東西都不算。\n" +
                    "・「⚔ 下一關」繼續故事；「再挑戰」重打同一關（打過的關卡可以重打刷裝備）；「回村子」整理裝備、加點。\n" +
                    "・升級拿到的能力點、技能點，回村子按「📜 角色」分配。");
            if (phase != Phase.Setup) return null;
            var p = Progress.Char(myClass);
            if (charOpen)
            {
                var t = Want("char", "📜 角色畫面",
                    "上面有三個分頁：\n・📊 能力值：升級拿到的能力點在這裡加。\n・🌳 技能樹：用技能點強化招式、學新招。\n・🛡 裝備：穿脫裝備、整理背包、設定戰鬥要帶的技能。\n分頁按鈕上的數字＝還沒用掉的點數。右上「✖」回去。");
                if (t != null) return t;
                switch (charPanel.tab)
                {
                    case 0: return Want("char_stats", "📊 能力值",
                        $"每升一級得到 {CharacterProfile.StatPerLevel} 能力點，按「＋」加到想要的能力值（旁邊寫著加了會變怎樣）：\n" +
                        "・💪力量＝劍士、獵人、刺客的攻擊；📘智力＝法師、牧師的攻擊與治療；❤體質＝血量、減傷；⚡敏捷＝蓄力、普攻集氣變快、閃避；🔮精神＝回魔、技能冷卻；🍀幸運＝爆擊、掉寶。\n" +
                        "不知道怎麼點：先加職業的攻擊能力值，再補一點體質。");
                    case 1: return Want("char_tree", "🌳 技能樹",
                        "・下面的小分頁：初心者／職業／進階職業，每一棵樹有自己的技能點（不能挪到別棵）。\n" +
                        "・點一個節點看說明，按「學習／升級」花技能點。有線連著的節點要先點前面的才能點。\n" +
                        "・「學會○○」的節點＝新招式；學會後記得到「🛡 裝備」頁下面的技能欄（或出發前的整備）把它放進去才用得到。");
                    case 2: return Want("char_gear", "🛡 裝備",
                        "・上面是身上的 5 個部位；下面是背包。點背包裡的裝備 → 看能力比較 → 「穿上」。穿不了的會寫原因（等級、能力值不夠）。\n" +
                        "・再往下是「技能欄」：戰鬥中能用的招式。點技能換上或拿掉，按 ⓘ 或長按看說明。\n" +
                        "・倉庫（三個角色共用）也在這頁；背包滿了，新的東西會寄到信箱。");
                }
                return null;
            }
            if (prepGo != null)
                return prepLookOnly ? null : Want("prep", "⚔ 出發前的整備",
                    "每次出發前都會先到這裡：\n" +
                    "・✨ 技能：亮著（✔）的是這場要帶的招式。點技能看說明，可以換上或移除；格子要填滿才出發得了。\n" +
                    "・🧪 藥水：用＋／−選這場要帶幾瓶（戰鬥中按畫面上的藥水按鈕喝）。\n" +
                    "・🛡 裝備：想換裝備或加點，按「更換裝備・分配點數」。\n" +
                    "都好了按「⚔ 出發」：（有劇情先看劇情）開打前會請你重新放置場地，站到空地中間按確定才開始戰鬥。");
            if (!storyMapOpen) return null;
            switch (world.screen)
            {
                case WorldUI.Screen.Gate: return Want("gate", "🚪 怎麼挑戰關卡",
                    "・這裡列出這一章的故事關卡，照順序解鎖（🔒＝前一關還沒通關）。\n" +
                    "・上面可以選難度：普通／困難的敵人等級更高、掉的裝備也更好。\n" +
                    "・點一個關卡看說明（建議等級、會遇到的敵人），按「⚔ 出發」→ 整備 → 劇情 → 放置場地 → 開打。\n" +
                    "・打過的關卡可以「再次挑戰」刷裝備；等級不夠就先回去刷前面的關卡。");
                case WorldUI.Screen.Dungeon: return Want("dungeon", "🏰 副本",
                    "比故事關卡更難的地城：一路上可以選路線、開寶箱、遇到 NPC。點一個副本看說明 → 出發。\n想和朋友一起打：回首頁選連線，在大廳選「🏰 副本」。");
                case WorldUI.Screen.Smith: return Want("smith", "⚒ 鐵匠鋪",
                    "點一件裝備 → 「強化」：每 +1 所有屬性 +8%。+5 以前不會失敗；之後越來越難，失敗會掉級。強化要花金幣。");
                case WorldUI.Screen.Shop: return Want("shop", "🧪 道具店",
                    "上面買藥水（回血、回魔）和回城捲軸；下面點背包裡的裝備可以賣掉換金幣。\n買的藥水在出發前的整備選要帶幾瓶。");
                case WorldUI.Screen.Guild: return Want("guild", "🏛 職業工會",
                    "10 級時在這裡挑戰轉職試煉（不難），通過後選職業。點職業可以看介紹、特色，還有它之後的兩個進階職業。\n30 級後再回來挑戰進階轉職。");
                case WorldUI.Screen.Town:
                    // 剛轉職：新的職業有什麼不一樣
                    if (p.cls != Skills.Novice && p.novicePast)
                    {
                        var t = Want("job", "🎉 轉職完成",
                            $"・普攻變成{Skills.Classes[p.cls].name}的普攻（招式說明在「📜 角色」→「🛡 裝備」的技能欄按 ⓘ）。\n" +
                            "・「🌳 技能樹」多了「職業」分頁：11 級以後拿到的技能點點這棵。初心者的樹保留，不能重置。\n" +
                            "・新招式學會後放進技能欄才用得到。1-4 開始敵人變多、會出現遠攻，記得整理裝備再出發。");
                        if (t != null) return t;
                    }
                    if (!world.walkMode)
                    {
                        var t = Want("list", "📋 清單",
                            "清單＝不用走路也能用村子的一切：\n" +
                            "・居民：點頭像聊天，再點一次換下一句；有任務的會出現黃色按鈕（接任務、回報）。\n" +
                            "・設施：鐵匠鋪、道具店、職業工會、角色／裝備，以及副本、訓練場、討伐、觀戰。\n" +
                            "・下面「🚶 走進城鎮」回到 3D 村子，「🗺 回大地圖」去村口選關卡或換城鎮。");
                        if (t != null) return t;
                    }
                    if (GuideActive || charOpen) return null;   // 新手導覽還在教：等導覽做完再說
                    // 拿到新東西
                    int bag = Progress.Bag.Count, known = p.KnownSkills().Count;
                    if (tipBagSeen < 0 || bag < tipBagSeen) tipBagSeen = bag;          // 賣掉、丟掉：從現在的件數重新算
                    if (tipKnownSeen < 0 || known < tipKnownSeen) tipKnownSeen = known;
                    if (known > tipKnownSeen)
                    {
                        var t = Want("new_skill", "✨ 學會新招式",
                            "新招式不會自動出現在戰鬥裡：按「📜 角色」→「🛡 裝備」，在下面的技能欄把它換上（或出發前的整備換）。\n戰鬥中唸出技能名稱 → 做放招手勢就能用。");
                        if (t != null) return t;
                        tipKnownSeen = known;
                    }
                    if (bag > tipBagSeen)
                    {
                        var t = Want("new_item", "🎁 拿到裝備了",
                            "新的裝備放在背包裡，要穿上才有效果：按「📜 角色」→「🛡 裝備」，點背包裡的裝備 → 比較能力 → 「穿上」。\n用不到的可以到道具店賣掉換金幣。");
                        if (t != null) return t;
                        tipBagSeen = bag;
                    }
                    if (p.souls.Count > 0)
                    {
                        var t = Want("soul", "💠 拿到精魄了",
                            "精魄是打倒怪物時很少掉的寶物（小怪 0.1～0.5%、Boss 2%），每一種怪物的效果都不一樣：加能力值、爆擊、閃避、格擋，命中時機率附加狀態或發動別的職業的招式……有些很強但有副作用。\n" +
                            "裝備有 0～4 個精魄孔（越稀有的裝備上限越高）。按「📜 角色」→「🛡 裝備」→ 點一件裝備 →「💠 精魄」→ 點背包裡的精魄兩下鑲上。⚠ 鑲上後不能取出，會一直跟著那件裝備（賣掉、丟掉就沒了），選好再鑲。\n沒選裝備時按「💠 精魄圖鑑」看所有精魄的來源和效果。");
                        if (t != null) return t;
                    }
                    if (p.UsableSkillPoints > 0)
                    {
                        var t = Want("skill_pt", "🌳 有技能點可以用",
                            $"你有 {p.UsableSkillPoints} 點技能點：按「📜 角色」→「🌳 技能樹」，點節點 → 學習／升級。\n「📜 角色」按鈕上的數字＝還沒用掉的能力點＋技能點。");
                        if (t != null) return t;
                    }
                    if (p.statPoints > 0)
                        return Want("stat_pt", "📊 有能力點可以用",
                            $"你有 {p.statPoints} 點能力點：按「📜 角色」→「📊 能力值」，按「＋」加點。");
                    return null;
            }
            return null;
        }

        (string, string, string)? Want(string id, string title, string text) => TipSeen(id) ? null : (id, title, text);

        void LayoutTip()
        {
            float W = Screen.width, H = Screen.height;
            int fs = UiKit.BaseFont;
            float pad = W * 0.05f, gap = fs * 0.5f;
            float h = Mathf.Min(H * 0.55f, fs * 12.5f);
            tipRect = new Rect(pad, H * 0.5f - h * 0.45f, W - pad * 2, h);
            float bh = fs * 2f, bw = (tipRect.width - gap * 3) / 2f;
            tipOffRect = new Rect(tipRect.x + gap, tipRect.yMax - gap - bh, bw, bh);
            tipOkRect = new Rect(tipRect.x + gap * 2 + bw, tipRect.yMax - gap - bh, bw, bh);
        }

        /// <summary>DrawGUI 一開始呼叫：決定要顯示哪張提示，並吃掉落在提示卡上的點擊（底下的按鈕才不會被按到）</summary>
        void TipInput()
        {
            var e = Event.current;
            if (e.type == EventType.Layout)
            {
                var t = CurrentTip();
                if (t == null) { tipId = null; tipPress = -1; }
                else if (t.Value.id != tipId) { tipId = t.Value.id; tipTitle = t.Value.title; tipText = t.Value.text; tipPress = -1; Sfx.Play("select", 0.5f, 1.3f); }
            }
            if (tipId == null) return;
            LayoutTip();
            if (!tipRect.Contains(e.mousePosition)) return;
            if (e.type == EventType.MouseDown)
            {
                tipPress = tipOkRect.Contains(e.mousePosition) ? 0 : tipOffRect.Contains(e.mousePosition) ? 1 : -1;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                int hit = tipOkRect.Contains(e.mousePosition) ? 0 : tipOffRect.Contains(e.mousePosition) ? 1 : -1;
                if (hit >= 0 && hit == tipPress)
                {
                    MarkTip(tipId);
                    if (hit == 1) { TipsOff = true; Say("之後不再顯示提示（重看新手導覽會再打開）", 3f); }
                    Sfx.Play("select", 0.6f);
                    tipId = null;
                }
                tipPress = -1;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel) e.Use();
        }

        /// <summary>DrawGUI 最後呼叫：畫在所有介面上面</summary>
        void DrawTip()
        {
            if (tipId == null) return;
            LayoutTip();
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f);
            float gap = fs * 0.5f;
            var col = new Color(1f, 0.85f, 0.35f);
            UiKit.Card(tipRect, 0.95f, col, 1f);
            UiKit.Text(new Rect(tipRect.x + gap, tipRect.y + gap * 0.5f, tipRect.width - gap * 2, fs * 1.6f), "💡 " + tipTitle, fs, col, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(tipRect.x + gap, tipRect.y + fs * 2f, tipRect.width - gap * 2, tipOkRect.y - tipRect.y - fs * 2.2f), tipText, fsS, Color.white, TextAnchor.UpperLeft, false, 0.55f);
            // 按鈕只畫外觀（點擊在 TipInput 處理）
            UiKit.Card(tipOffRect, tipPress == 1 ? 0.9f : 0.6f, UiKit.Muted, 0.6f);
            UiKit.Text(tipOffRect, "不再顯示提示", fsS, new Color(0.8f, 0.82f, 0.9f), TextAnchor.MiddleCenter);
            UiKit.Card(tipOkRect, tipPress == 0 ? 1f : 0.85f, col, 1f);
            UiKit.Text(tipOkRect, "知道了 ✔", fs, Color.white, TextAnchor.MiddleCenter, true);
        }
    }
}
