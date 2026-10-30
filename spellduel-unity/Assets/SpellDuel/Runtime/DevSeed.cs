using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 開發版（TestFlight 的 SpellDuel Dev，Bundle ID com.paul771127.spellduel.dev）第一次開啟時，
    /// 在空的角色欄建好 50 級的測試角色（使用者要求）：遊俠、忍者、大魔導士。
    /// 開發版只在 CI 簽章時換 Bundle ID（Unity 專案、BuildScript 都一樣），所以在執行時看 Application.identifier 判斷；
    /// 正式版（com.paul771127.spellduel）完全不會執行。已經有角色的欄位不動；只做一次。
    /// 角色：已轉進階職業、新手教學／村莊導覽／轉職禮物都算完成、故事全部通關（50 級＝第 5 章打完）；
    /// 能力點與技能點留著自己加。
    /// 另外（sd_dev_seed_items_v1，已經建好角色的開發版也會補）：每種寵物的誘餌各 BaitEach 個、
    /// 一組武器製作材料＝Lv50 特殊裝備「燒肉串串大劍」的配方卷軸＋全部材料（交給鐵匠學會就能做）。
    /// </summary>
    public static class DevSeed
    {
        public const string Flag = "sd_dev_seed_v1";
        public const string ItemsFlag = "sd_dev_seed_items_v1";
        public const int Level = 50, Gold = 100000, BaitEach = 3;
        public const string WeaponBlueprint = "bp_skewer";   // 燒肉串串大劍（Lv50）
        public static bool IsDevApp(string bundleId) => bundleId != null && bundleId.EndsWith(".dev");
        /// <summary>開發版：所有 Boss 體積減半（外觀、身高、判定半徑；使用者要求）。正式版＝1</summary>
        public static float BossScale => bossScale ??= IsDevApp(Application.identifier) ? 0.5f : 1f;
        static float? bossScale;
        public static readonly (string cls, string adv, string name)[] Chars =
        {
            ("archer", "ranger", "遊俠50"), ("assassin", "ninja", "忍者50"), ("mage", "archmage", "大魔導50"),
        };

        /// <summary>GameRoot.Start 呼叫（SaveReset 之後）；回傳建了幾隻</summary>
        public static int RunOnce() => RunOnce(Application.identifier);

        public static int RunOnce(string bundleId)
        {
            int made = 0;
            try
            {
                if (!IsDevApp(bundleId)) return 0;
                // 角色：只建一次
                if (PlayerPrefs.GetInt(Flag, 0) != 1)
                {
                    Story.Load();
                    foreach (var c in Chars)
                    {
                        int s = FreeSlot(); if (s < 0) break;
                        if (!CharacterSlots.Create(s, c.name)) continue;
                        var p = MakeProfile(c.cls, c.adv);
                        string K(string k) => CharacterSlots.KeyOf(s, k);
                        PlayerPrefs.SetString(K("sd_my_class"), c.cls);
                        PlayerPrefs.SetString(K("sd_char_" + c.cls), p.Serialize());
                        PlayerPrefs.SetString(K("sd_gold"), Gold.ToString());
                        // 新角色的引導都算做完（不會再從初心者教學開始）
                        PlayerPrefs.SetInt(K("sd_tutorial_done"), 1);
                        PlayerPrefs.SetInt(K("sd_guide"), 99);   // SoloBattle.GuideDone（村莊導覽完成）
                        PlayerPrefs.SetInt(K("sd_gift_job"), 1);
                        PlayerPrefs.SetInt(K("sd_starter_gear"), 1);
                        foreach (var bt in new[] { "11", "12", "dash" }) PlayerPrefs.SetInt(K("sd_bt" + bt), 1);
                        // 故事全部通關（50 級＝第 5 章打完）
                        var ids = new List<string>();
                        foreach (var st in Story.AllStages()) ids.Add(st.id);
                        PlayerPrefs.SetString(K("sd_story_clear"), string.Join(",", ids));
                        made++;
                    }
                    PlayerPrefs.SetInt(Flag, 1);
                }
                // 道具：補給所有測試角色（照名字認；之前的版本已經建好的也補）
                if (PlayerPrefs.GetInt(ItemsFlag, 0) != 1)
                {
                    var give = StarterItems();
                    for (int s = 0; s < CharacterSlots.Max; s++)
                    {
                        if (!CharacterSlots.Exists(s) || System.Array.FindIndex(Chars, c => c.name == CharacterSlots.Name(s)) < 0) continue;
                        string key = CharacterSlots.KeyOf(s, "sd_items");
                        PlayerPrefs.SetString(key, MergeItems(PlayerPrefs.GetString(key, ""), give));
                    }
                    PlayerPrefs.SetInt(ItemsFlag, 1);
                }
                PlayerPrefs.Save();
            }
            catch (System.Exception e) { Debug.LogWarning("DevSeed: " + e.Message); }
            return made;
        }

        /// <summary>50 級、已轉進階職業的角色資料（能力點、技能點都還沒加）</summary>
        public static CharacterProfile MakeProfile(string cls, string adv)
        {
            var p = CharacterProfile.Parse(cls, "");
            p.novicePast = true;
            p.level = Level; p.xp = CharacterProfile.XpForLevel(Level);
            p.statPoints = (Level - 1) * CharacterProfile.StatPerLevel;
            p.trialPassed = true; p.advPick = adv; p.advProPassed = true; p.adv = adv;
            p.NormalizeSkillPoints();
            return p;
        }

        /// <summary>測試道具：每種寵物的誘餌、燒肉串串大劍的配方卷軸與材料</summary>
        public static Dictionary<string, int> StarterItems()
        {
            var d = new Dictionary<string, int>();
            foreach (var sp in Pets.All) d[sp.bait] = BaitEach;
            var b = Blueprints.All.Find(x => x.id == WeaponBlueprint);
            if (b != null)
            {
                d[b.ScrollId] = 1;
                foreach (var (m, n) in b.need) d[m.StartsWith("mat_") ? m : "mat_" + m] = n;
            }
            return d;
        }

        /// <summary>道具欄存檔（Economy 的格式「id:數量,…」）加上 add</summary>
        public static string MergeItems(string saved, Dictionary<string, int> add)
        {
            var d = new Dictionary<string, int>();
            foreach (var kv in (saved ?? "").Split(','))
            {
                var p = kv.Split(':');
                if (p.Length == 2 && int.TryParse(p[1], out var n) && n > 0) d[p[0]] = n;
            }
            foreach (var kv in add) d[kv.Key] = (d.TryGetValue(kv.Key, out var have) ? have : 0) + kv.Value;
            var parts = new List<string>();
            foreach (var kv in d) if (kv.Value > 0) parts.Add(kv.Key + ":" + kv.Value);
            return string.Join(",", parts);
        }

        static int FreeSlot() { for (int i = 0; i < CharacterSlots.Max; i++) if (!CharacterSlots.Exists(i)) return i; return -1; }
    }
}
