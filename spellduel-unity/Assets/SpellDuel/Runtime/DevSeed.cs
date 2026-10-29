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
    /// </summary>
    public static class DevSeed
    {
        public const string Flag = "sd_dev_seed_v1";
        public const int Level = 50, Gold = 100000;
        public static bool IsDevApp(string bundleId) => bundleId != null && bundleId.EndsWith(".dev");
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
                if (!IsDevApp(bundleId) || PlayerPrefs.GetInt(Flag, 0) == 1) return 0;
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
                    var ids = new System.Collections.Generic.List<string>();
                    foreach (var st in Story.AllStages()) ids.Add(st.id);
                    PlayerPrefs.SetString(K("sd_story_clear"), string.Join(",", ids));
                    made++;
                }
                PlayerPrefs.SetInt(Flag, 1);
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

        static int FreeSlot() { for (int i = 0; i < CharacterSlots.Max; i++) if (!CharacterSlots.Exists(i)) return i; return -1; }
    }
}
