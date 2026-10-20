using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 出發前的整備畫面（故事關卡、副本、轉職試煉、選技能畫面按「開始」時）：
    ///   ・技能：點技能卡換上／移除（基本 3 格＋轉職後進階 1 格）。
    ///   ・裝備：目前穿的 5 件，點「更換裝備」打開角色畫面。
    ///   ・道具：每種藥水決定要帶幾瓶（最多 5 瓶；回城捲軸 1 張）。戰鬥中只能用帶出來的，用掉的才從背包扣。
    /// 按「⚔ 出發」才開打；「取消」回到原本的畫面。結算後的「再挑戰／再來一場」不再經過整備（沿用上次的選擇）。
    /// </summary>
    public partial class SoloBattle
    {
        Action prepGo;               // 整備完要做什麼（null＝沒在整備）
        string prepTitle;
        bool prepEndless;            // 整備的是用角色養成的模式（故事、副本、試煉）：技能從技能樹已學會的選
        string prepNoItems;          // 這個模式不能用道具的原因（null＝可以帶）
        Vector2 prepScroll; float prepContentH;
        public const int CarryMax = 5;

        // ---- 帶出來的道具（PlayerPrefs 記住上次的選擇；戰鬥開始時依背包數量夾住）
        readonly Dictionary<string, int> carry = new Dictionary<string, int>();
        readonly Dictionary<string, int> carryLeft = new Dictionary<string, int>();
        bool carryLoaded;
        static int CarryCap(Economy.Consumable c) => c.scroll ? 1 : CarryMax;

        void LoadCarry()
        {
            if (carryLoaded) return;
            carryLoaded = true;
            string s = ""; try { s = PlayerPrefs.GetString("sd_carry", ""); } catch { }
            if (string.IsNullOrEmpty(s))
                foreach (var c in Economy.Consumables) carry[c.id] = c.scroll ? 1 : 3;   // 第一次：每種藥水帶 3 瓶、捲軸 1 張
            else
                foreach (var kv in s.Split(','))
                {
                    var p = kv.Split(':');
                    if (p.Length == 2 && int.TryParse(p[1], out var n)) carry[p[0]] = n;
                }
        }
        void SaveCarry()
        {
            var parts = new List<string>();
            foreach (var kv in carry) parts.Add(kv.Key + ":" + kv.Value);
            try { PlayerPrefs.SetString("sd_carry", string.Join(",", parts)); PlayerPrefs.Save(); } catch { }
        }
        int CarryOf(string id) { LoadCarry(); return carry.TryGetValue(id, out var n) ? n : 0; }

        /// <summary>戰鬥開始：這一場能用的道具數＝帶的數量（不超過背包裡有的）</summary>
        void ResetCarryLeft()
        {
            LoadCarry(); carryLeft.Clear();
            foreach (var c in Economy.Consumables) carryLeft[c.id] = Mathf.Min(CarryOf(c.id), Economy.Count(c.id));
        }
        /// <summary>這一場還能用幾個</summary>
        int CarryLeftOf(string id) => Mathf.Min(carryLeft.TryGetValue(id, out var n) ? n : 0, Economy.Count(id));
        void SpendCarry(string id) { if (carryLeft.TryGetValue(id, out var n)) carryLeft[id] = Mathf.Max(0, n - 1); }

        /// <summary>打開整備畫面；go＝按「出發」後要做的事</summary>
        void OpenPrep(string title, bool endlessLike, string noItems, Action go)
        {
            prepTitle = title; prepEndless = endlessLike; prepNoItems = noItems; prepGo = go; prepScroll = Vector2.zero;
            LoadCarry(); FixLoadout();
        }

        void DrawPrep()
        {
            float W = Screen.width, H = Screen.height;
            var safe = Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y, pad = W * 0.04f, gap = pad * 0.5f;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f), fsL = Mathf.RoundToInt(fs * 1.2f);
            float u = fs * 1.65f;
            var cls = Skills.Classes[myClass];
            UiKit.Backdrop(new Rect(0, 0, W, H), cls.color, 0.97f);
            float bh = u * 1.5f, btnY = botY - pad - bh;
            var view = new Rect(0, topY + pad, W, btnY - gap - (topY + pad));
            float cw = W - Mathf.Max(18f, fs * 0.45f);
            var content = new Rect(0, 0, cw, Mathf.Max(view.height, prepContentH));
            prepScroll = UiKit.DragScroll(view, prepScroll, content);
            prepScroll = GUI.BeginScrollView(view, prepScroll, content, false, true);
            float y = 0f;
            UiKit.Title(new Rect(pad, y, cw - pad * 2, u * 1.2f), "⚔ 整備：" + prepTitle, cls.color, fsL);
            y += u * 1.35f;

            // ---- 技能
            UiKit.Text(new Rect(pad, y, cw - pad * 2, u), $"✨ 技能（{myLoadout.Count}/{SlotCount}）：點技能看說明、換上或移除", fsS, UiKit.Gold, TextAnchor.MiddleLeft, true);
            y += u;
            var skills = SetupSkills;
            float sw = (cw - pad * 2 - gap) / 2f, sh = u * 1.25f;
            for (int i = 0; i < skills.Length; i++)
            {
                var sk = Skills.All[skills[i]];
                bool on = myLoadout.Contains(sk.id);
                var r = new Rect(pad + (i % 2) * (sw + gap), y + (i / 2) * (sh + gap * 0.6f), sw, sh);
                UiKit.Card(r, on ? 0.9f : 0.55f, sk.color, on ? 1f : 0.35f);
                float isz = sh * 0.78f;
                GUI.color = on ? Color.white : new Color(0.55f, 0.55f, 0.55f);
                GUI.DrawTexture(new Rect(r.x + sh * 0.11f, r.y + sh * 0.11f, isz, isz), SkillIcons.Get(sk.id));
                GUI.color = Color.white;
                UiKit.Text(new Rect(r.x + isz + sh * 0.25f, r.y, r.width - isz - sh * 0.3f, sh), (on ? "✔ " : "") + (Advanced.IsAdvSkill(sk.id) ? "✨" : "") + sk.name, fsS, on ? Color.white : new Color(0.75f, 0.75f, 0.8f), TextAnchor.MiddleLeft, on);
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) infoSkill = sk.id;
            }
            y += ((skills.Length + 1) / 2) * (sh + gap * 0.6f) + gap;

            // ---- 裝備
            var pf = Progress.Char(myClass);
            UiKit.Text(new Rect(pad, y, cw - pad * 2, u), $"🛡 裝備（Lv{pf.level}）", fsS, UiKit.Gold, TextAnchor.MiddleLeft, true);
            y += u;
            for (int i = 0; i < pf.equipped.Length; i++)
            {
                var it = pf.equipped[i];
                var r = new Rect(pad, y, cw - pad * 2, u * 0.95f);
                UiKit.Card(r, 0.5f, it != null ? Item.RarityColors[it.rarity] : UiKit.Muted, 0.4f);
                UiKit.Text(new Rect(r.x + gap, r.y, r.width - gap * 2, r.height), $"{Item.SlotNames[i]}：" + (it != null ? $"[{Item.RarityNames[it.rarity]}] {it.FullName}" : "（空）"), fsS, it != null ? Item.RarityColors[it.rarity] : Color.gray, TextAnchor.MiddleLeft, it != null && it.rarity >= 3);
                y += u * 0.95f + gap * 0.4f;
            }
            if (UiKit.Button(new Rect(pad, y, cw - pad * 2, u * 1.1f), "🛡 更換裝備・分配點數（角色畫面）", fsS, false, new Color(0.75f, 0.45f, 1f))) charOpen = true;
            y += u * 1.1f + gap * 1.5f;

            // ---- 道具
            UiKit.Text(new Rect(pad, y, cw - pad * 2, u), prepNoItems != null ? "🧪 道具：" + prepNoItems : $"🧪 帶出去的道具（每種最多 {CarryMax} 瓶；戰鬥中只能用帶出來的）", fsS, UiKit.Gold, TextAnchor.MiddleLeft, true);
            y += u;
            if (prepNoItems == null)
                foreach (var c in Economy.Consumables)
                {
                    int own = Economy.Count(c.id), cap = Mathf.Min(CarryCap(c), own), n = Mathf.Min(CarryOf(c.id), cap);
                    var r = new Rect(pad, y, cw - pad * 2, u * 1.15f);
                    UiKit.Card(r, 0.5f, new Color(0.5f, 0.85f, 1f), own > 0 ? 0.5f : 0.15f);
                    float bw = r.height * 1.1f;
                    UiKit.Text(new Rect(r.x + gap, r.y, r.width - bw * 3 - gap * 3, r.height), $"{c.icon} {c.name}　（背包 {own}）", fsS, own > 0 ? Color.white : Color.gray, TextAnchor.MiddleLeft);
                    if (UiKit.Button(new Rect(r.xMax - bw * 3 - gap, r.y + 2, bw, r.height - 4), "−", fs, false, null, n > 0)) { carry[c.id] = n - 1; SaveCarry(); }
                    UiKit.Text(new Rect(r.xMax - bw * 2 - gap, r.y, bw, r.height), $"{n}", fs, n > 0 ? Color.white : Color.gray, TextAnchor.MiddleCenter, true);
                    if (UiKit.Button(new Rect(r.xMax - bw - gap, r.y + 2, bw, r.height - 4), "＋", fs, false, null, n < cap)) { carry[c.id] = n + 1; SaveCarry(); }
                    y += u * 1.15f + gap * 0.4f;
                }
            if (prepNoItems == null && Economy.Consumables.TrueForAll(c => Economy.Count(c.id) == 0))
            { UiKit.Text(new Rect(pad, y, cw - pad * 2, u), "背包裡沒有藥水：到城鎮的道具店買", fsS, UiKit.Muted); y += u; }
            prepContentH = y + gap;
            GUI.EndScrollView();

            // ---- 取消／出發
            float hw = (W - pad * 2 - gap) / 2f;
            if (UiKit.Button(new Rect(pad, btnY, hw * 0.7f, bh), "取消", fs)) prepGo = null;
            if (UiKit.Button(new Rect(pad + hw * 0.7f + gap, btnY, W - pad * 2 - hw * 0.7f - gap, bh), LoadoutReady ? "⚔ 出發" : "先選好技能", fsL, true, cls.color, LoadoutReady))
            {
                SaveChoices(); SaveCarry();
                var go = prepGo; prepGo = null; prepEndless = false;
                go?.Invoke();
            }
        }
    }
}
