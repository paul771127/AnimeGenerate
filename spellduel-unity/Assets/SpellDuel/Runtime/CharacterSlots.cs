using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 角色欄位（最多 3 個角色）：每個角色有自己的角色資料、背包、信箱、金幣、道具、故事／副本進度、技能配置、每日挑戰紀錄；
    /// 共用的只有「倉庫」（Progress.Stash）和設定。
    /// 存法：每個角色的存檔 key 前面加「s{欄位}g{代數}.」（CharacterSlots.Key）。刪除角色＝代數 +1（舊資料全部作廢，
    /// PlayerPrefs 不能列出所有 key，所以用換代數的方式，保證刪得乾淨），再把知道的舊 key 清掉省空間。
    /// </summary>
    public static class CharacterSlots
    {
        public const int Max = 3;
        public const int NameMax = 8;

        /// <summary>現在玩的欄位（−1＝還沒選）</summary>
        public static int Current { get; private set; } = -1;
        /// <summary>上次玩的欄位（選角畫面預設選它）</summary>
        public static int Last => Mathf.Clamp(Pi("sd_slot_last", 0), 0, Max - 1);

        static int Gen(int s) => Pi($"sd_slot{s}_gen", 0);
        public static string Prefix(int s) => $"s{s}g{Gen(s)}.";
        /// <summary>這個角色的存檔 key（還沒選角色＝不屬於任何角色的暫存 key，不會寫到別人的存檔）</summary>
        public static string Key(string k) => Current >= 0 ? Prefix(Current) + k : "s_none." + k;
        /// <summary>某個欄位的 key（選角畫面讀別的角色用）</summary>
        public static string KeyOf(int s, string k) => Prefix(s) + k;

        public static bool Exists(int s) => s >= 0 && s < Max && Pi(KeyOf(s, "sd_exists"), 0) == 1;
        public static string Name(int s) => Ps(KeyOf(s, "sd_name"), $"冒險者{s + 1}");
        public static string ClassOf(int s) { var c = Ps(KeyOf(s, "sd_my_class"), Skills.Novice); return Skills.Classes.ContainsKey(c) ? c : Skills.Novice; }
        public static int Count { get { int n = 0; for (int i = 0; i < Max; i++) if (Exists(i)) n++; return n; } }

        /// <summary>讀某個欄位的角色資料（不切換欄位、不進快取；選角畫面的預覽用）</summary>
        public static CharacterProfile PeekProfile(int s) => CharacterProfile.Parse(ClassOf(s), Ps(KeyOf(s, "sd_char_" + ClassOf(s)), ""));

        /// <summary>建立角色：從初心者 1 級開始</summary>
        public static bool Create(int s, string name)
        {
            if (s < 0 || s >= Max || Exists(s)) return false;
            name = (name ?? "").Trim();
            if (name.Length == 0) name = $"冒險者{s + 1}";
            if (name.Length > NameMax) name = name.Substring(0, NameMax);
            PlayerPrefs.SetInt(KeyOf(s, "sd_exists"), 1);
            PlayerPrefs.SetString(KeyOf(s, "sd_name"), name);
            PlayerPrefs.SetString(KeyOf(s, "sd_my_class"), Skills.Novice);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>刪除角色：這個欄位的資料全部作廢（倉庫不受影響）</summary>
        public static void Delete(int s)
        {
            if (s < 0 || s >= Max) return;
            // 知道的 key 先清掉（省空間）；不知道的（例如每日挑戰的日期紀錄）換代數後也不會再被讀到
            string[] keys = { "sd_exists", "sd_name", "sd_my_class", "sd_enemy_class", "sd_bag", "sd_mail", "sd_gold", "sd_items", "sd_story_clear", "sd_story_seen",
                "sd_dungeon_clear", "sd_carry", "sd_tutorial_done", "sd_endless_best" };
            foreach (var k in keys) PlayerPrefs.DeleteKey(KeyOf(s, k));
            foreach (var c in Skills.Classes.Keys) { PlayerPrefs.DeleteKey(KeyOf(s, "sd_char_" + c)); PlayerPrefs.DeleteKey(KeyOf(s, "sd_loadout_" + c)); }
            PlayerPrefs.SetInt($"sd_slot{s}_gen", Gen(s) + 1);
            if (Current == s) Current = -1;
            PlayerPrefs.Save();
        }

        /// <summary>選角色：之後所有存檔都讀寫這個角色（呼叫的人要重讀快取，見 SoloBattle.UseSlot）</summary>
        public static bool Select(int s)
        {
            if (!Exists(s)) return false;
            Current = s;
            PlayerPrefs.SetInt("sd_slot_last", s); PlayerPrefs.Save();
            return true;
        }
        public static void Deselect() => Current = -1;

        static int Pi(string k, int d) { try { return PlayerPrefs.GetInt(k, d); } catch { return d; } }
        static string Ps(string k, string d) { try { var v = PlayerPrefs.GetString(k, d); return string.IsNullOrEmpty(v) ? d : v; } catch { return d; } }
    }
}
