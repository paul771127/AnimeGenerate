using System;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 村子裡的新設施（首頁拿掉的玩法都搬到這裡），每個都有一位 NPC 介紹、問你要不要開始：
    ///   🏰 副本入口（嚮導）：單人進入，或和身邊／遠方的朋友組隊（轉到連線大廳）。
    ///   📅 訓練場（教官）：四種每日挑戰、再上一次教學。
    ///   🔥 討伐告示板（懸賞官）：單人挑戰 Boss（選難度）。
    ///   👀 觀戰席（播報員）：輸入朋友的房間代碼觀戰。
    ///   🗼 無盡塔（大地圖上，塔守）：無盡模式。
    /// 故事關卡、副本的難度在出發前選：普通＝簡單再加 20 級、困難＝再加 40 級。
    /// </summary>
    public partial class WorldUI
    {
        public Action<int> OnStartDaily;          // 每日挑戰（種類）
        public Action OnStartTutorial;            // 再上一次教學
        public Action OnStartBounty;              // 討伐 Boss（單人）
        public Action OnStartTower;               // 無盡塔（無盡模式）
        public Action<bool> OnNetDungeon;         // 副本組隊：false＝身邊的朋友、true＝遠方的朋友
        public Action OnWatch;                    // 觀戰
        public Func<int> GetDifficulty;           // 難度（0 簡單、1 普通、2 困難）
        public Action<int> SetDifficulty;
        public Func<int> EndlessBest;

        /// <summary>村子的設施：建築種類（TownWalk 的 Building／Keeper 用）、NPC 名字、身分、圖示、標題、招呼語</summary>
        public static readonly (string kind, string npc, string role, string icon, string title, string hello)[] Facilities =
        {
            ("dungeon", "嚮導", "副本嚮導", "🏰", "副本入口", "地底的遺跡、沉船、古塔……我都帶人去過。要一個人闖，還是找夥伴一起？"),
            ("training", "教官", "訓練場教官", "📅", "訓練場", "每天的訓練題目都不一樣。閃避、輸出、跑步、綜合，拿到獎牌就有獎勵！"),
            ("bounty", "懸賞官", "討伐告示板", "🔥", "討伐告示板", "告示板上貼著懸賞。打倒大傢伙，酬勞和名聲都是你的。"),
            ("watch", "播報員", "觀戰席", "👀", "觀戰席", "朋友在別的地方比賽？告訴我房間代碼，我帶你上看台。"),
        };
        public static readonly Color[] FacilityColors = { new Color(0.4f, 0.85f, 0.75f), new Color(0.45f, 0.9f, 0.7f), new Color(1f, 0.45f, 0.2f), new Color(0.55f, 0.9f, 1f) };
        public const string TowerKeeper = "塔守";
        static readonly Color TowerColor = new Color(0.75f, 0.45f, 1f);

        /// <summary>建築招牌：圖示＋名稱、做什麼、找誰（鐵匠鋪、道具店、職業工會＋村子的四個設施）</summary>
        public static (string title, string what, string who, Color color) SiteInfo(string kind)
        {
            switch (kind)
            {
                case "smith": return ("⚒ 鐵匠鋪", "強化裝備（+1～+10）", "鐵匠", new Color(1f, 0.6f, 0.3f));
                case "shop": return ("🧪 道具店", "買藥水、回城捲軸・賣裝備", "店員", new Color(0.5f, 0.85f, 1f));
                case "guild": return ("🏛 職業工會", "轉職試煉・選進階職業", "公會長", new Color(0.85f, 0.6f, 1f));
                case "dungeon": return ("🏰 副本入口", "單人或和朋友組隊挑戰副本", "嚮導", FacilityColors[0]);
                case "training": return ("📅 訓練場", "每日挑戰（閃避、輸出、跑步、綜合）・再上一次教學", "教官", FacilityColors[1]);
                case "bounty": return ("🔥 討伐告示板", "單人討伐 Boss", "懸賞官", FacilityColors[2]);
                case "watch": return ("👀 觀戰席", "輸入房間代碼，看朋友比賽", "播報員", FacilityColors[3]);
            }
            return (kind, "", "", Color.white);
        }

        static int FacilityIndex(string kind) { for (int i = 0; i < Facilities.Length; i++) if (Facilities[i].kind == kind) return i; return -1; }
        public static string FacilityIcon(string kind) { int i = FacilityIndex(kind); return i >= 0 ? Facilities[i].icon + " " : ""; }

        static Screen FacilityScreen(string kind) => kind == "dungeon" ? Screen.Dungeon : kind == "training" ? Screen.Training : kind == "bounty" ? Screen.Bounty : kind == "watch" ? Screen.Watch : Screen.Town;

        int Difficulty => Mathf.Clamp(GetDifficulty?.Invoke() ?? 0, 0, 2);
        static readonly string[] DiffNames = { "簡單", "普通", "困難" };
        static readonly Color[] DiffColors = { new Color(0.4f, 0.9f, 0.5f), new Color(1f, 0.8f, 0.3f), new Color(1f, 0.35f, 0.35f) };

        /// <summary>難度三個按鈕（故事關卡、副本：普通＋20 級、困難＋40 級）</summary>
        void DifficultyRow(Rect r, int fsS, bool levels)
        {
            float gap = r.height * 0.15f, bw = (r.width - gap * 2) / 3f;
            for (int i = 0; i < 3; i++)
            {
                string label = DiffNames[i] + (levels && i > 0 ? $"（＋{i * SoloBattle.LevelPerDifficulty} 級）" : "");
                if (UiKit.Button(new Rect(r.x + i * (bw + gap), r.y, bw, r.height), label, fsS, Difficulty == i, DiffColors[i])) SetDifficulty?.Invoke(i);
            }
        }

        /// <summary>難度加的等級（說明文字用）</summary>
        int Boost => SoloBattle.LevelBoostFor(Difficulty);
        string BoostText => Boost > 0 ? $"（{DiffNames[Difficulty]}：敵人＋{Boost} 級、獎勵 ×{SoloBattle.RewardMulFor(Difficulty):0.#}、掉落裝備＋{Boost} 級）" : "";

        /// <summary>設施畫面上方：NPC 頭像＋名字＋介紹（一段話）</summary>
        void NpcIntro(Rect r, string npc, string role, string text, Color c, int fs, int fsS, float gap, ref float y)
        {
            float h = fs * 4.4f;
            var cr = new Rect(r.x + gap, y, r.width - gap * 2, h);
            UiKit.Card(cr, 0.6f, c, 0.75f);
            float ps = h - gap * 0.8f;
            var pr = new Rect(cr.x + gap * 0.4f, cr.y + gap * 0.4f, ps, ps);
            GUI.DrawTexture(pr, Portraits.Get(npc, "npc"), ScaleMode.ScaleToFit, true);
            UiKit.Text(new Rect(pr.xMax + gap, cr.y + gap * 0.2f, cr.width - ps - gap * 2, fs * 1.2f), $"{npc}・{role}", fsS, c, TextAnchor.MiddleLeft, true);
            UiKit.Text(new Rect(pr.xMax + gap, cr.y + fs * 1.3f, cr.width - ps - gap * 2, h - fs * 1.4f), $"「{text}」", fsS, Color.white, TextAnchor.UpperLeft);
            y += h + gap * 0.6f;
        }

        void BackToTown(Rect r, int fs, float gap)
        {
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), $"🏘 回{Towns[town].name}", fs)) screen = Screen.Town;
        }

        // ---------------------------------------------------------------- 📅 訓練場：每日挑戰＋教學
        void DrawTraining(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var f = Facilities[FacilityIndex("training")]; var c = FacilityColors[FacilityIndex("training")];
            float y = r.y + gap;
            Header(r, $"{f.icon} {f.title}", c, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, f.npc, f.role, f.hello, c, fs, fsS, gap, ref y);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.2f), $"📅 今天的每日挑戰（{DateTime.Now:M/d}）：每個挑戰每天領一次獎勵", fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true);
            y += fs * 1.4f;
            float bottom = r.yMax - gap * 2 - fs * 2.1f - fs * 2.4f;
            float ch = Mathf.Min(fs * 4.6f, (bottom - y) / DailyChallenge.KindCount - gap * 0.4f);
            for (int i = 0; i < DailyChallenge.KindCount; i++)
            {
                var k = (DailyChallenge.Kind)i; int m = DailyChallenge.MedalToday(k);
                var cr = new Rect(r.x + gap, y, r.width - gap * 2, ch);
                UiKit.Card(cr, 0.6f, DailyChallenge.Colors[i], 0.75f);
                float bw = cr.width * 0.24f;
                UiKit.Text(new Rect(cr.x + gap, cr.y, cr.width - bw - gap * 2, fs * 1.3f), $"{DailyChallenge.Names[i]}　{(m > 0 ? DailyChallenge.MedalNames[m] : "今天還沒拿獎牌")}", fsS, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(cr.x + gap, cr.y + fs * 1.3f, cr.width - bw - gap * 2, ch - fs * 1.35f), DailyChallenge.Descs[i], Mathf.RoundToInt(fsS * 0.92f), new Color(0.85f, 0.9f, 1f), TextAnchor.UpperLeft);
                if (UiKit.Button(new Rect(cr.xMax - bw - gap * 0.4f, cr.y + gap * 0.4f, bw, ch - gap * 0.8f), "▶ 開始", fsS, true, DailyChallenge.Colors[i])) { OnStartDaily?.Invoke(i); return; }
                y += ch + gap * 0.4f;
            }
            if (UiKit.Button(new Rect(r.x + gap, r.yMax - gap * 2 - fs * 2.1f - fs * 2.2f, r.width - gap * 2, fs * 2.1f), "🎓 再上一次教學（蹲、跳、閃、詠唱、放招）", fsS, false, new Color(0.35f, 0.95f, 1f))) { OnStartTutorial?.Invoke(); return; }
            BackToTown(r, fs, gap);
        }

        // ---------------------------------------------------------------- 🔥 討伐告示板：單人 Boss
        void DrawBounty(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            int fi = FacilityIndex("bounty"); var f = Facilities[fi]; var c = FacilityColors[fi];
            float y = r.y + gap;
            Header(r, $"{f.icon} {f.title}", c, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, f.npc, f.role, f.hello, c, fs, fsS, gap, ref y);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 3.4f),
                "一個人挑戰 Boss，會用到蹲、跳、閃和你全部的技能；Boss 血量剩一半會怒吼換招，還會召喚小怪。\n用職業的基本能力值（不看等級與裝備），不能使用道具。", fsS, new Color(0.95f, 0.9f, 0.85f), TextAnchor.UpperLeft);
            y += fs * 3.6f;
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.2f), "難度（簡單：Boss 較慢、你受到的傷害較少）", fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true);
            y += fs * 1.3f;
            DifficultyRow(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2f), fsS, false);
            y += fs * 2f + gap;
            if (UiKit.Button(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2.4f), "🔥 接下懸賞，出發討伐", fs, true, c)) { OnStartBounty?.Invoke(); return; }
            BackToTown(r, fs, gap);
        }

        // ---------------------------------------------------------------- 👀 觀戰席
        void DrawWatchDesk(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            int fi = FacilityIndex("watch"); var f = Facilities[fi]; var c = FacilityColors[fi];
            float y = r.y + gap;
            Header(r, $"{f.icon} {f.title}", c, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, f.npc, f.role, f.hello, c, fs, fsS, gap, ref y);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 4f),
                "從斜上方看朋友的對戰、合作打 Boss、副本（比賽中途也能進）。手指拖曳移動、兩指縮放旋轉。\n要離開村子、連上朋友的房間：輸入朋友的房間代碼就能觀戰。", fsS, new Color(0.9f, 0.95f, 1f), TextAnchor.UpperLeft);
            y += fs * 4.2f;
            if (UiKit.Button(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2.4f), "👀 去看台（輸入房間代碼）", fs, true, c)) { OnWatch?.Invoke(); return; }
            BackToTown(r, fs, gap);
        }

        // ---------------------------------------------------------------- 🗼 無盡塔（大地圖）
        void DrawTower(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            float y = r.y + gap;
            Header(r, "🗼 無盡塔", TowerColor, prof, fs, fsS, fsL, gap, ref y);
            NpcIntro(r, TowerKeeper, "無盡塔的守塔人", "這座塔沒有頂。每往上一層，敵人就更強一點；能爬多高，全看你自己。", TowerColor, fs, fsS, gap, ref y);
            int best = EndlessBest?.Invoke() ?? 0;
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 4.4f),
                $"無盡模式：一波接一波，每 10 波一隻 Boss，地圖從熔岩、森林、海上、角鬥場到山道輪替。\n用角色的等級、技能樹與裝備，可以帶道具；每過一波結算戰利品。\n🏆 最佳紀錄：{best} 分", fsS, new Color(0.92f, 0.88f, 1f), TextAnchor.UpperLeft);
            y += fs * 4.6f;
            if (UiKit.Button(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2.4f), "🗼 走進無盡塔", fs, true, TowerColor)) { OnStartTower?.Invoke(); return; }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), "🗺 回大地圖", fs)) screen = Screen.Map;
        }
    }
}
