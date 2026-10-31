using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 戰鬥畫面右上角：我身上所有持續狀態的小圖示（用技能圖示）＋倒數秒數。
    ///   正面狀態（輔助、護盾、劍來…）綠框；負面狀態（中毒、定身、致盲、沉默、被懲戒）紅框。
    /// </summary>
    public partial class SoloBattle
    {
        struct StatusIcon { public string icon; public float left; public bool good; public string label; }
        readonly List<StatusIcon> statusIcons = new List<StatusIcon>();

        void CollectStatus(Fighter f, float now)
        {
            var l = statusIcons; l.Clear();
            void Add(string icon, float until, bool good, string label = null) { if (until > now) l.Add(new StatusIcon { icon = icon, left = until - now, good = good, label = label }); }
            void AddOn(string icon, bool good, string label) => l.Add(new StatusIcon { icon = icon, left = -1f, good = good, label = label });
            // ---- 正面
            Add("block", f.blockUntil, true);
            if (f.shieldUntil > now && f.shieldLeft > 0) Add("ironwall", f.shieldUntil, true, $"{f.shieldLeft}");
            Add("counter", f.counterUntil, true);
            Add("guard", f.armorUntil, true);
            Add("bless", f.teamAtkUntil, true);
            Add("haste", f.teamHasteUntil, true);
            Add("spring", f.teamRegenUntil, true);
            Add("focus", f.teamCritUntil, true);
            Add("jianlai", f.swordUntil, true);
            Add("yujian", f.yujianUntil, true);
            Add("bloodrage", f.frenzyUntil, true);
            if (f.undyingUntil > 1e8f) AddOn("undying", true, "鬥魂"); else Add("undying", f.undyingUntil, true);
            Add("substitute", f.invulnUntil, true, "無敵");
            Add("manashield", f.manaShieldUntil, true);
            if (f.manaCoat) AddOn("manashield", true, "MP");
            Add("oath", f.selfOathUntil, true);
            Add("oath", f.oathUntil, true);
            Add("substitute", f.subUntil, true);
            Add("clone", f.cloneUntil, true);
            Add("shadowdance", f.danceUntil, true);
            Add("rollshot", f.rollUntil, true);
            Add("revive", f.blessUntil, true);
            foreach (var pb in f.potionBuffs) Add(pb.Key, pb.Value, true);   // 強化藥劑
            // ---- 負面
            float dotUntil = 0f; foreach (var d in f.dots) dotUntil = Mathf.Max(dotUntil, d.until);
            Add("m_venom", dotUntil, false);
            Add("m_vine", f.snaredUntil, false);
            Add("s_ink", f.blindUntil, false);
            Add("dispel", f.silencedUntil, false, "沉默");
            Add("chastise", f.markedUntil, false);
        }

        /// <summary>畫在 right（右邊界）往左排、y 往下；一排放不下就換行</summary>
        void DrawStatusIcons(Fighter f, float now, float right, float y, int fs)
        {
            if (f == null || battle == null || phase != Phase.Fighting) return;
            CollectStatus(f, now);
            if (statusIcons.Count == 0) return;
            float sz = fs * 2.1f, gap = fs * 0.25f, minX = Screen.width * 0.45f, x = right;
            int small = Mathf.RoundToInt(fs * 0.62f);
            foreach (var st in statusIcons)
            {
                if (x - sz < minX) { x = right; y += sz + gap + small; }   // 換行
                var r = new Rect(x - sz, y, sz, sz);
                UiKit.StatusFrame(r, SkillIcons.Get(st.icon), st.good);
                // 倒數（或標籤）
                string t = st.left >= 0f ? (st.left >= 10f ? $"{st.left:0}" : $"{st.left:0.0}") : (st.label ?? "");
                if (st.left >= 0f && st.label != null) t = st.label + " " + t;
                UiKit.Text(new Rect(r.x - gap, r.yMax, r.width + gap * 2, small * 1.2f), t, small, st.good ? new Color(0.75f, 1f, 0.8f) : new Color(1f, 0.7f, 0.7f), TextAnchor.UpperCenter, true);
                x -= sz + gap;
            }
        }
    }
}
