using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 一次性重置存檔（2026-10 改成「初心者 → 10 級轉職」的單一主角色制度，使用者選擇全部重來）：
    /// 角色、背包、信箱、金幣、道具、故事／副本進度、教學、紀錄全部清掉，從初心者 1 級重新開始。
    /// 保留：音效／音樂／震動／環境光設定、對話窗行數、難度、蹲下校正、錄好的咒語、運動紀錄、上次的連線代碼。
    /// 只在第一次開這個版本時執行一次（GameRoot.Start 最先呼叫）。
    /// </summary>
    public static class SaveReset
    {
        public const string Flag = "sd_reset_novice_v1";
        static readonly string[] Ints = { "SD_SfxOn", "SD_MusicOn", "SD_ArLight", "SD_HapticsOn", "sd_chat_lines", "sd_difficulty", "sd_daily_pick", "sd_ex_squats", "sd_ex_jumps", "sd_ex_dodges" };
        static readonly string[] Floats = { "SD_SfxVol", "SD_MusicVol", "SD_CrouchRatio", "sd_ex_kcal", "sd_ex_minutes" };
        static readonly string[] Strings = { "sd_last_ip", "sd_last_code", "sd_voice_v1" };

        /// <summary>還沒重置過就重置（回傳有沒有重置）</summary>
        public static bool RunOnce()
        {
            try
            {
                if (PlayerPrefs.GetInt(Flag, 0) == 1) return false;
                bool hadSave = PlayerPrefs.HasKey("sd_my_class") || PlayerPrefs.HasKey("sd_bag");
                var ints = new Dictionary<string, int>(); var floats = new Dictionary<string, float>(); var strings = new Dictionary<string, string>();
                foreach (var k in Ints) if (PlayerPrefs.HasKey(k)) ints[k] = PlayerPrefs.GetInt(k);
                foreach (var k in Floats) if (PlayerPrefs.HasKey(k)) floats[k] = PlayerPrefs.GetFloat(k);
                foreach (var k in Strings) if (PlayerPrefs.HasKey(k)) strings[k] = PlayerPrefs.GetString(k);
                PlayerPrefs.DeleteAll();
                foreach (var kv in ints) PlayerPrefs.SetInt(kv.Key, kv.Value);
                foreach (var kv in floats) PlayerPrefs.SetFloat(kv.Key, kv.Value);
                foreach (var kv in strings) PlayerPrefs.SetString(kv.Key, kv.Value);
                PlayerPrefs.SetInt(Flag, 1);
                PlayerPrefs.SetString("sd_my_class", Skills.Novice);
                PlayerPrefs.Save();
                JustReset = hadSave;
                return true;
            }
            catch { return false; }
        }
        /// <summary>這次開遊戲剛重置了舊存檔（首頁提示一次）</summary>
        public static bool JustReset;
    }
}
