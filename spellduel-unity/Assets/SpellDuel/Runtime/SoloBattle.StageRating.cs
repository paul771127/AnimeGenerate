using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事關卡的連殺與評等（使用者要求：讓打過的關卡也有重打的理由）：
    ///   連殺：上一隻倒下後 ComboWindow 秒內又打倒一隻就接下去；畫面右側顯示「🔥 連殺 ×N」。
    ///     連殺越高掉寶率越高：5 連 ×1.1、10 連 ×1.2、20 連 ×1.35（只算小怪的裝備掉落）。
    ///   評等（通關時算，新手關、試煉不算）：用時、平均每波被打中幾次、最高連殺各給分，加總 100 分：
    ///     S ≥ 85、A ≥ 65、B ≥ 45、其他 C。S：金幣 +50%、多一件精良以上的裝備、試煉幣 +1；A：金幣 +20%。
    ///   每一關每個難度記最好的評等（PlayerPrefs），結算畫面顯示。每支手機各自算（隊友也一樣）。
    /// </summary>
    public partial class SoloBattle
    {
        public const float ComboWindow = 4f;
        int combo, maxCombo, myHitsTaken; float lastKillAt = -99f, stageT0, lastHurtAt = -99f, lastMyHp = -1f;

        void ResetStageRating()
        {
            combo = maxCombo = myHitsTaken = 0; lastKillAt = -99f; lastHurtAt = -99f; lastMyHp = -1f;
            stageT0 = Time.time;
        }

        bool RatedStage => storyStage != null && !IsGuideStage(storyStage) && dungeon == null && exploreRun == null;

        /// <summary>TrackProgress 叫：這一幀打倒了 dk 隻</summary>
        void ComboOnKills(int dk)
        {
            if (dk <= 0 || !RatedStage) return;
            combo = Time.time - lastKillAt <= ComboWindow ? combo + dk : dk;
            lastKillAt = Time.time;
            if (combo > maxCombo) maxCombo = combo;
            if (combo == 5 || combo == 10 || combo == 20 || combo > 20 && combo % 10 == 0) { Sfx.Play("charged", 0.6f, 1f + Mathf.Min(0.5f, combo * 0.02f)); Haptics.Play(Haptics.Kind.Light); }
        }

        /// <summary>每幀：我被打中幾次（血量往下掉算一次；0.4 秒內連續掉血只算一次，持續傷害不會一直加）</summary>
        void TrackMyHits()
        {
            if (!RatedStage || Me == null || phase != Phase.Fighting) { lastMyHp = -1f; return; }
            if (lastMyHp >= 0f && Me.hp < lastMyHp - 0.5f && Time.time - lastHurtAt > 0.4f) { myHitsTaken++; lastHurtAt = Time.time; }
            lastMyHp = Me.hp;
        }

        bool ComboLive => combo >= 2 && Time.time - lastKillAt <= ComboWindow;
        /// <summary>連殺的掉寶率倍率（只算小怪的裝備）</summary>
        float ComboLootMul => !RatedStage || !ComboLive ? 1f : combo >= 20 ? 1.35f : combo >= 10 ? 1.2f : combo >= 5 ? 1.1f : 1f;

        /// <summary>HUD：連殺數（右側，接不下去前慢慢淡掉）</summary>
        void DrawComboHud(float W, float H)
        {
            if (!ComboLive || phase != Phase.Fighting) return;
            float left = ComboWindow - (Time.time - lastKillAt);
            float a = Mathf.Clamp01(left / 1.2f);
            int fs = Mathf.RoundToInt(H * 0.03f);
            float pop = Mathf.Clamp01((Time.time - lastKillAt) * 6f);
            int size = Mathf.RoundToInt(fs * (1.35f - 0.35f * pop));
            var col = combo >= 20 ? new Color(1f, 0.35f, 0.9f, a) : combo >= 10 ? new Color(1f, 0.45f, 0.2f, a) : new Color(1f, 0.85f, 0.35f, a);
            UiKit.Text(new Rect(W * 0.6f, H * 0.3f, W * 0.37f, fs * 1.8f), $"🔥 連殺 ×{combo}", size, col, TextAnchor.MiddleRight, true);
            float mul = ComboLootMul;
            if (mul > 1f) UiKit.Text(new Rect(W * 0.6f, H * 0.3f + fs * 1.6f, W * 0.37f, fs * 1.2f), $"掉寶率 ×{mul:0.##}", Mathf.RoundToInt(fs * 0.6f), new Color(1f, 0.9f, 0.6f, a), TextAnchor.MiddleRight, true);
            // 剩下的時間：一條細線
            GUI.color = new Color(col.r, col.g, col.b, a * 0.8f);
            float bw = W * 0.2f * Mathf.Clamp01(left / ComboWindow);
            GUI.DrawTexture(new Rect(W * 0.97f - bw, H * 0.3f + fs * 1.55f, bw, Mathf.Max(2f, fs * 0.1f)), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        /// <summary>通關：算評等、給獎勵，回傳結算畫面要加的文字</summary>
        string GrantStageRating(StoryStage st, float rmul)
        {
            if (!RatedStage || st == null) return "";
            int waves = Mathf.Max(1, st.LastWave - st.FirstWave + 1);
            float par = 75f * waves + (st.HasBoss ? 60f : 0f);
            float t = Time.time - stageT0;
            float hitsPerWave = myHitsTaken / (float)waves;
            int sTime = t <= par ? 40 : t <= par * 1.5f ? 25 : 10;
            int sHits = hitsPerWave <= 2f ? 30 : hitsPerWave <= 4f ? 20 : hitsPerWave <= 7f ? 10 : 0;
            int sCombo = maxCombo >= 15 ? 30 : maxCombo >= 8 ? 20 : maxCombo >= 4 ? 10 : 0;
            int score = sTime + sHits + sCombo;
            string rank = score >= 85 ? "S" : score >= 65 ? "A" : score >= 45 ? "B" : "C";
            string note = $"\n🏅 評等 {rank}（{score} 分）：用時 {Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}（{sTime}）　被打中 {myHitsTaken} 次（{sHits}）　最高連殺 {maxCombo}（{sCombo}）";
            if (rank == "S")
            {
                int g = Mathf.RoundToInt(Economy.StageGold(st) * rmul * 0.5f);
                pendingGold += g; TrialCoins.Coins += 1;
                bool item = TryDrop(true, 1);
                note += $"\n　S 評等獎勵：金幣 +{g}、試煉幣 +1" + (item ? "、裝備一件" : "");
            }
            else if (rank == "A")
            {
                int g = Mathf.RoundToInt(Economy.StageGold(st) * rmul * 0.2f);
                pendingGold += g;
                note += $"\n　A 評等獎勵：金幣 +{g}";
            }
            string key = $"rank_{st.id}_{difficulty}";
            string best = PlayerPrefs.GetString(key, "");
            if (best == "" || RankValue(rank) > RankValue(best)) { PlayerPrefs.SetString(key, rank); PlayerPrefs.Save(); note += best == "" ? "" : $"　🆕 刷新最佳（原本 {best}）"; }
            else note += $"　（最佳 {best}）";
            return note;
        }

        static int RankValue(string r) => r == "S" ? 4 : r == "A" ? 3 : r == "B" ? 2 : r == "C" ? 1 : 0;
        /// <summary>這一關、這個難度最好的評等（沒打過＝""）</summary>
        public static string BestRank(string stageId, int diff) => PlayerPrefs.GetString($"rank_{stageId}_{diff}", "");
    }
}
