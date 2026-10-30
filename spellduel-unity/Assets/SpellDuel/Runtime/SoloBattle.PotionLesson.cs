using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 喝藥水教學（故事 1-2、簡單難度、初心者、第一次）：血量第一次低於 1/3 時
    ///   → 所有敵人暫停（小怪定住、不出怪、Boss 不動、飛過來的攻擊消失）
    ///   → 玩家只能喝藥水（說「喝藥水」或比 🤙；畫面上也有按鈕）：不能攻擊、放招
    ///   → 喝完一瓶 → 倒數 3 秒 → 繼續戰鬥。
    /// 沒帶藥水的話，教學開始前送一瓶小藥水（並帶進這一場）。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>0＝沒有；1＝等著觸發；2＝暫停中、要喝藥水；3＝倒數；4＝完成</summary>
        int potStage;
        int potCountAt;
        float potResumeAt;
        const float PotResumeTime = 3f;
        bool PotLessonFreeze => potStage == 2 || potStage == 3;

        /// <summary>1-2 開打時（SetupNoviceBattle）：準備喝藥水教學</summary>
        void SetupPotionLesson(StoryStage st)
        {
            potStage = 0;
            if (st == null || st.id != "1-2" || difficulty != 0 || NetMode || coop || Progress.Char(myClass).cls != Skills.Novice || BtFlag("pot")) return;
            potStage = 1;
        }

        int HealPotionsLeft => CarryLeftOf("hp_s") + CarryLeftOf("hp_l");

        /// <summary>每幀（戰鬥中）</summary>
        void UpdatePotionLesson()
        {
            if (potStage == 0 || potStage == 4 || battle == null || Me == null || !Me.Alive || battle.stageCleared) return;
            if (potStage == 1)
            {
                if (Me.hp >= Me.maxHp / 3f) return;
                // 開始：沒帶藥水就送一瓶（帶進這一場）
                if (HealPotionsLeft <= 0)
                {
                    if (Economy.Count("hp_s") <= 0) Economy.AddItem("hp_s", 1);
                    carryLeft["hp_s"] = Mathf.Max(1, carryLeft.TryGetValue("hp_s", out var n) ? n : 0);
                }
                potStage = 2; potCountAt = HealPotionsLeft;
                potionReady.Clear();   // 不用等冷卻
                Me.charging = null;
                battle.projectiles.RemoveAll(p => p.owner != null && !p.owner.isPlayer && !p.owner.isAlly);   // 飛過來的攻擊消失
                battle.minionHold = BtHold; battle.holdSpawns = true;
                Say("⏸ 血量很低！所有敵人暫停——先喝藥水", 3f);
                Sfx.Play("warn", 0.9f); Haptics.Play(Haptics.Kind.Heavy);
                return;
            }
            Me.hp = Mathf.Max(Me.hp, 1f);
            if (potStage == 2)
            {
                if (HealPotionsLeft < potCountAt)
                {
                    potStage = 3; potResumeAt = Time.time + PotResumeTime;
                    floaters.Add(new Floater { text = "✔ 喝下去了！", color = new Color(0.5f, 1f, 0.6f), born = Time.time, screen = true });
                    Sfx.Play("charged", 1f); Haptics.Play(Haptics.Kind.Success);
                }
                return;
            }
            if (potStage == 3 && Time.time >= potResumeAt)
            {
                potStage = 4; BtMark("pot");
                bool tutHold = btStage == 12 && btStep != BtFree;   // 衝鋒獸教學還沒結束：照它的規則
                battle.minionHold = tutHold ? BtHold : null;
                battle.holdSpawns = tutHold && btStep != Bt12Wait;
                Say("⚔ 繼續戰鬥！之後血少了就說「喝藥水」或比 🤙（不限瓶數；小藥水 5 秒、大藥水 10 秒冷卻）", 4f);
                Sfx.Play("roar", 0.5f, 1.3f);
            }
        }

        /// <summary>喝藥水教學的畫面：說明、喝藥水按鈕、倒數</summary>
        void DrawPotionLesson(float W, float H, int fs, int fsS, int fsL)
        {
            if (!PotLessonFreeze || phase != Phase.Fighting) return;
            GUI.color = new Color(0f, 0f, 0.05f, 0.35f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            var col = new Color(0.5f, 1f, 0.6f);
            var r = new Rect(W * 0.06f, H * 0.28f, W * 0.88f, fs * 7.5f);
            UiKit.Card(r, 0.94f, col, 1f);
            if (potStage == 3)
            {
                int left = Mathf.CeilToInt(potResumeAt - Time.time);
                UiKit.Text(new Rect(r.x, r.y + fs * 0.4f, r.width, fs * 1.6f), "✔ 血量回復了！", fsL, col, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(r.x, r.y + fs * 2.2f, r.width, fs * 4.5f), $"{Mathf.Max(1, left)}", fsL * 3, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(r.x, r.yMax - fs * 1.6f, r.width, fs * 1.4f), "準備繼續戰鬥…", fsS, Color.white, TextAnchor.MiddleCenter);
                return;
            }
            UiKit.Text(new Rect(r.x + fs * 0.6f, r.y + fs * 0.3f, r.width - fs * 1.2f, fs * 1.6f), "🧪 喝藥水教學：所有敵人暫停中", fsL, col, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(r.x + fs * 0.6f, r.y + fs * 2.1f, r.width - fs * 1.2f, fs * 3.4f),
                $"血量剩不到 1/3（{Mathf.CeilToInt(Me.hp)}/{Me.maxHp}）。現在只能喝藥水，喝完才能繼續戰鬥：\n" +
                "① 對著手機說「喝藥水」　② 或比 🤙（拇指＋小指，像拿瓶子喝）停一下\n" +
                $"帶出來的治療藥水：{HealPotionsLeft} 瓶", fsS, Color.white, TextAnchor.UpperLeft, false, 0.55f);
            float bw = r.width * 0.5f;
            var c = PickItem("hp");
            if (c != null && UiKit.Button(new Rect(r.center.x - bw / 2f, r.yMax - fs * 2f, bw, fs * 1.7f), $"{c.icon} 喝{c.name}", fsS, true, col))
                UseConsumable(c);
        }
    }
}
