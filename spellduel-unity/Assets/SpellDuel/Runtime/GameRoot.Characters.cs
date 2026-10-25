using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 選角畫面（打開遊戲先到這裡；首頁的「👤 換角色」也會回來）：
    ///   ・三個欄位：有角色＝名字、職業、等級；空的＝「＋ 建立角色」。
    ///   ・上面大大的角色預覽（GearPreview：和遠端連線時別人看到的形象一樣，身上裝備的外觀也一起顯示），慢慢轉。
    ///   ・進入遊戲／刪除角色（要按兩次確認）；建立角色：取名字（最多 8 個字），從初心者 1 級開始。
    /// </summary>
    public partial class GameRoot
    {
        bool charSelect = true;             // 選角畫面開著（一開始一定要先選角色）
        int charPick = -1;                  // 選角畫面目前選中的欄位
        bool charCreating;                  // 正在建立角色（取名字）
        string charName = "";
        float charDeleteArm;                // 刪除：3 秒內再按一次
        readonly CharacterProfile[] charPeek = new CharacterProfile[CharacterSlots.Max];
        static readonly CharacterProfile novicePeek = CharacterProfile.Parse(Skills.Novice, "");

        bool CharSelectOpen => charSelect || CharacterSlots.Current < 0;

        void OpenCharacterSelect()
        {
            charSelect = true; charCreating = false; charDeleteArm = 0f;
            charPick = CharacterSlots.Current >= 0 ? CharacterSlots.Current : CharacterSlots.Last;
            for (int i = 0; i < charPeek.Length; i++) charPeek[i] = null;
        }

        CharacterProfile Peek(int s)
        {
            if (!CharacterSlots.Exists(s)) return null;
            return charPeek[s] ??= CharacterSlots.PeekProfile(s);
        }

        void DrawCharacterSelect(float W, float H, float pad, float top, float lineH, int fs, int fsS)
        {
            if (charPick < 0) charPick = CharacterSlots.Last;
            var safe = Screen.safeArea;
            float bottom = H - safe.y - pad, h = bottom - top;
            LobbyHomeBackground(new Rect(0, 0, W, H));
            UiKit.Title(new Rect(pad, top, W - pad * 2, lineH * 1.2f), charCreating ? "建立角色" : "選擇角色", UiKit.Gold, Mathf.RoundToInt(fs * 1.25f));
            float y = top + lineH * 1.4f;
            if (SaveReset.JustReset) { UiKit.Text(new Rect(pad, y, W - pad * 2, lineH), "🌱 改版：舊存檔已重置，建立角色從「初心者」重新開始（設定、錄好的咒語都保留）", fsS, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleCenter, true); y += lineH * 1.1f; }

            // 角色預覽（選中的欄位；建立中＝初心者）
            bool exists = CharacterSlots.Exists(charPick);
            var p = charCreating ? novicePeek : Peek(charPick);
            var art = p != null ? Story2D.ClassPortrait(p.cls, p.adv) : null;   // 職業的 2D 頭像（Codex 畫的）；沒有就用 3D 預覽
            float pvH = h * (art != null ? 0.46f : 0.38f);
            var pv = new Rect(W * 0.2f, y, W * 0.6f, pvH);
            var cardR = new Rect(pad, y, W - pad * 2, pvH);
            UiKit.Card(cardR, 0.55f, p != null ? Skills.Classes[p.cls].color : UiKit.Muted, 0.6f);
            if (art != null)
            {
                var artR = DrawClassArt(cardR, art, Skills.Classes[p.cls].color);
                // 立繪右下角：實際 3D 模型（含目前裝備），高度約立繪的 1/3
                bool named = !charCreating;
                float mh = artR.height / 3f, mw = mh * 2f / 3f;   // 預覽貼圖是 2:3
                float mb = cardR.yMax - (named ? lineH * 1.25f : pad * 0.4f);
                var mr = new Rect(Mathf.Min(artR.xMax, cardR.xMax - pad * 0.4f) - mw, mb - mh, mw, mh);
                UiKit.Card(mr, 0.55f, Skills.Classes[p.cls].color, 0.6f);
                GearPreview.Draw(mr, p);
            }
            else if (p != null) GearPreview.Draw(pv, p);
            else UiKit.Text(pv, "（空的欄位）\n點下面的「＋ 建立角色」", fs, UiKit.Muted, TextAnchor.MiddleCenter);
            if (p != null && !charCreating)
            {
                string cn = p.Adv != null ? $"{p.Adv.name}（{Skills.Classes[p.cls].name}）" : Skills.Classes[p.cls].name;
                UiKit.Text(new Rect(pad * 1.5f, y + pvH - lineH * 1.2f, W - pad * 3, lineH), $"{CharacterSlots.Name(charPick)}　{cn}　Lv{p.level}", fs, Color.white, TextAnchor.MiddleCenter, true);
            }
            y += pvH + pad * 0.6f;

            if (charCreating)
            {
                // 取名字
                UiKit.Text(new Rect(pad, y, W - pad * 2, lineH), $"角色名字（最多 {CharacterSlots.NameMax} 個字）", fsS, new Color(0.9f, 0.9f, 1f));
                y += lineH;
                float bh = fs * 2.3f;
                UiKit.Card(new Rect(pad, y, W - pad * 2, bh), 0.95f, UiKit.Gold, 0.8f);
                charName = GUI.TextField(new Rect(pad + 4, y + 3, W - pad * 2 - 8, bh - 6), charName ?? "", CharacterSlots.NameMax, LobbyInput(fs));
                y += bh + pad * 0.5f;
                UiKit.Text(new Rect(pad, y, W - pad * 2, lineH * 2.2f), "新角色從「初心者」1 級開始，普攻是木棍敲擊（手掌劃一下）；到炎之里找居民解任務學木棍揮擊、投石、急救。\n10 級到村子的職業工會挑戰轉職試煉，選擇自己的職業。", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperCenter);
                float half = (W - pad * 3) / 2f;
                if (UiKit.Button(new Rect(pad, bottom - bh, half, bh), "取消", fs)) charCreating = false;
                if (UiKit.Button(new Rect(pad * 2 + half, bottom - bh, half, bh), "✔ 建立", fs, true, UiKit.Gold))
                {
                    if (CharacterSlots.Create(charPick, charName)) { charPeek[charPick] = null; charCreating = false; Sfx.Play("levelup", 0.6f); }
                }
                return;
            }

            // 三個欄位
            float ch = fs * 3.2f;
            for (int i = 0; i < CharacterSlots.Max; i++)
            {
                var r = new Rect(pad, y, W - pad * 2, ch);
                var sp = Peek(i);
                bool sel = i == charPick;
                var col = sp != null ? Skills.Classes[sp.cls].color : UiKit.Muted;
                if (UiKit.Button(r, "", fs, sel, col)) { charPick = i; charDeleteArm = 0f; Sfx.Play("select", 0.5f); }
                if (sp != null)
                {
                    var basic = Skills.BasicOf(sp.cls);
                    float bs = ch * 0.82f;
                    var face = Story2D.ClassPortrait(sp.cls, sp.adv);
                    var fr = new Rect(r.x + pad * 0.4f, r.y + (ch - bs) / 2f, bs, bs);
                    if (face != null)
                    {
                        // 頭像的臉部（立繪上方中間）裁成方形，外面一圈職業色
                        Story2D.DrawFace(fr, face, col);
                    }
                    else if (basic != null) UiKit.Badge(new Rect(r.x + pad * 0.5f, r.y + (ch - bs * 0.75f) / 2f, bs * 0.75f, bs * 0.75f), SkillIcons.Get(basic.id), col);
                    string cn = sp.Adv != null ? sp.Adv.name : Skills.Classes[sp.cls].name;
                    UiKit.Text(new Rect(r.x + bs + pad, r.y, r.width - bs - pad * 1.5f, ch), $"{CharacterSlots.Name(i)}\n{cn}　Lv{sp.level}", fs, Color.white, TextAnchor.MiddleLeft, true);
                }
                else UiKit.Text(r, "＋ 建立角色", fs, new Color(0.85f, 0.85f, 0.9f), TextAnchor.MiddleCenter, true);
                y += ch + pad * 0.35f;
            }

            // 按鈕：進入遊戲／刪除；空的欄位＝建立
            float bh2 = fs * 2.3f, hw = (W - pad * 3) / 2f;
            float by = bottom - bh2;
            if (!exists)
            {
                if (UiKit.Button(new Rect(pad, by, W - pad * 2, bh2), "＋ 建立角色", fs, true, UiKit.Gold)) { charCreating = true; charName = $"冒險者{charPick + 1}"; }
            }
            else
            {
                bool armed = Time.time < charDeleteArm;
                if (UiKit.Button(new Rect(pad, by, hw, bh2), armed ? "⚠ 再按一次刪除" : "🗑 刪除角色", armed ? fsS : fs, armed, new Color(1f, 0.45f, 0.4f)))
                {
                    if (armed)
                    {
                        CharacterSlots.Delete(charPick); charPeek[charPick] = null; charDeleteArm = 0f;
                        if (solo.CharacterSlotGone(charPick)) { }   // 刪掉的是正在玩的角色：SoloBattle 也要放掉
                    }
                    else charDeleteArm = Time.time + 3f;
                }
                if (UiKit.Button(new Rect(pad * 2 + hw, by, hw, bh2), "▶ 進入遊戲", fs, true, UiKit.Gold)) EnterWithSlot(charPick);
                if (armed) UiKit.Text(new Rect(pad, by - lineH * 1.1f, W - pad * 2, lineH), "刪除後角色、背包、信箱、進度都會消失（倉庫裡的東西會保留）", fsS, new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter, true);
            }
        }

        /// <summary>選角畫面的大頭像：職業色的光暈＋光線，立繪靠下對齊、上半身放大。回傳立繪在卡片裡實際看得到的範圍</summary>
        static Rect DrawClassArt(Rect r, Texture2D art, Color col)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.6f);
            var c = new Vector2(r.center.x, r.y + r.height * 0.42f);
            float R = r.height * 0.55f;
            UiKit.Disc(new Rect(c.x - R, c.y - R, R * 2, R * 2), new Color(col.r, col.g, col.b, 0.18f + 0.08f * pulse));
            UiKit.Halo(new Rect(c.x - R * 0.8f, c.y - R * 0.8f, R * 1.6f, R * 1.6f), new Color(col.r, col.g, col.b, 0.55f), 0.8f);
            // 立繪：顯示上面 75%（大腿以上 → 腰以上），放大填滿卡片高度
            float show = 0.75f, h = r.height * 1.02f, w = h * art.width / (art.height * show);
            var ar = new Rect(r.center.x - w / 2f, r.yMax - h, w, h);
            GUI.BeginGroup(r);
            GUI.DrawTextureWithTexCoords(new Rect(ar.x - r.x, ar.y - r.y, ar.width, ar.height), art, new Rect(0f, 1f - show, 1f, show), true);
            GUI.EndGroup();
            float x0 = Mathf.Max(ar.x, r.x), x1 = Mathf.Min(ar.xMax, r.xMax), y0 = Mathf.Max(ar.y, r.y);
            return new Rect(x0, y0, x1 - x0, r.yMax - y0);
        }

        void EnterWithSlot(int s)
        {
            solo.UseSlot(s);
            if (CharacterSlots.Current != s) return;
            charSelect = false;
            Sfx.Play("select", 0.8f, 1.2f);
            if (SoloBattle.IntroPending) StartStoryEntry();   // 新角色：直接進故事模式（序章 → 教學 → 炎之里）
        }

        /// <summary>首頁左上：現在的角色＋「換角色」</summary>
        void DrawCurrentCharacter(float W, float pad, float top, float lineH, int fsS)
        {
            var r = new Rect(pad, top, W * 0.62f, lineH * 1.15f);
            if (UiKit.Button(r, $"👤 {solo.CharacterLabel}　▸ 換角色", fsS, false, UiKit.Gold)) OpenCharacterSelect();
        }
    }
}
