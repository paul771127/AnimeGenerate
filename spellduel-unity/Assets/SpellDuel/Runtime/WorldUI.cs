using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 故事模式的大地圖與城鎮（全螢幕 IMGUI，由 SoloBattle 呼叫 Draw）：
    ///   大地圖：三個地區（熔岩地帶、森林、海上礁石），每區一座城鎮＋這個地區的關卡；打過的關卡可以重打（刷裝備）。
    ///   城鎮：居民（點一下聊天）、鐵匠鋪（強化裝備）、道具店（買藥水／回城捲軸、賣裝備）、職業工會（Lv30 挑戰轉職試煉，通過後選進階職業）。
    /// </summary>
    public partial class WorldUI
    {
        public enum Screen { Map, Town, Smith, Shop, Guild, Gate, Warp, Dungeon, Training, Bounty, Watch, Tower, Alchemy, Craft, TrialShop }
        public Screen screen = Screen.Map;
        public int town;                         // 目前所在城鎮（＝地區主題 0～2）
        public Action<StoryStage> OnStartStage;  // 出發打關卡
        public Action<StoryStage> OnWatchStory;  // 重看這一關的故事（開場＋已通關的結尾）
        public Action<DungeonDef> OnStartDungeon, OnWatchDungeon;   // 副本：進入／看故事
        public Action OnBack;                    // 離開大地圖（回選擇畫面）
        public Action OnCharacter;               // 打開角色畫面
        public Action OnStartTrial;              // 出發挑戰轉職試煉
        public Action<string> OnStartWorldBoss;  // 大地圖：討伐世界 Boss（WorldBossArt.Ids）
        public Action<string> OnAdvPick;         // 30 級：選了想走的進階職業（開始那條路的專業試煉；換職業＝重來）
        public Action<string> OnJobChange;       // 初心者 10 級：選了職業（SoloBattle 換成那個職業）
        public Action<int> OnJobTrial;           // 初心者 10 級：挑戰轉職試煉的第幾輪（0 起算）
        public Action<string> OnJobTrialPick;    // 初心者 10 級：選了想走的職業（開始／繼續那個職業的試煉；換職業＝重來）
        public Func<string, string> NpcQuestLabel;   // 初心者：這位居民的任務按鈕文字（沒有＝null）
        public Action<string> OnNpcQuest;            // 初心者：點了任務按鈕（開任務對話）
        public string[] JobTrialNames, JobTrialDesc;
        string jobArm; float jobArmUntil;

        // 可走動的 3D 城鎮（TownWalk）：預設用走的；也可以切回清單模式
        public bool walkMode = false;   // 進村子預設是 2D 城鎮圖；按「🚶 逛街」才進 3D 村子
        public TownWalk Walk;                    // SoloBattle 提供
        public Camera Cam;
        public string SpeechNote;                // 語音狀態（不支援語音時提示用按鈕打招呼）
        public string Heard; public float HeardAt = -9f;
        public float holdAt = -9f;               // 「前進」鈕最後按住的時間
        public bool Walking => walkMode && screen == Screen.Town && Walk != null && Walk.Active;

        StoryStage sel;
        int shopTab, page, npcTalk = -1, npcLine;
        Item selItem;
        string toast; float toastUntil;
        readonly System.Random rng = new System.Random();

        void Toast(string s) { toast = s; toastUntil = Time.time + 2.5f; }

        // ---------------------------------------------------------------- 地區與城鎮資料
        public class Town
        {
            public string name, desc; public Color color;
            public (string name, string role, string[] lines)[] npcs;
        }

        public static readonly string[] RegionNames = { "🌋 熔岩地帶", "🌲 迷霧森林", "🌊 海上礁石", "⚔ 赤砂角鬥場", "⛰ 黃土山道" };
        public static readonly Color[] RegionColors = { new Color(1f, 0.45f, 0.2f), new Color(0.35f, 0.8f, 0.4f), new Color(0.3f, 0.65f, 1f), new Color(0.9f, 0.35f, 0.3f), new Color(0.85f, 0.68f, 0.38f) };

        public static readonly Town[] Towns =
        {
            new Town
            {
                name = "炎之里", desc = "晨鐘與鍛爐相伴的山村，灰燼裡藏著不敢提起的船名", color = new Color(1f, 0.55f, 0.3f),
                npcs = new[]
                {
                    ("村長・老焰", "守鐘的村長", new[] { "每天敲晨鐘前，我都會看看山路。鐘聲該讓出門的人知道，家還在這裡。", "有些事，我曾以為不說就能保護孩子。可沉默留下的問題，總得有人回答。", "去山裡記得帶藥。老人的錯，不該讓年輕人拿命去補。" }),
                    ("見習鐵匠・小鐵", "嘴硬的鐵匠學徒", new[] { "師父教我先補裂縫，再敲成形。鐘舌也好，鎧甲也好，都一樣。", "我想打一口不會啞掉的鐘。這樣在山裡迷路的人，就知道該往哪裡走。", "師父說強化到 +5 之前都不會失敗。你把裝備拿來，我去叫師父！" }),
                    ("旅行商人・米菈", "跑三地的行商", new[] { "我跑炎之里、森之里和潮騷港。三個地方的老人，居然都哼過同一段曲子。", "我的貨箱一半裝布和燈油，一半裝口信。有時收信的人等的，比貨物還久。", "出發前備好藥水和回城捲軸吧。做生意嘛，我可盼著客人平安回來。" }),
                },
            },
            new Town
            {
                name = "森之里", desc = "葉燈照著古樹的聚落，木牌替未歸的人留下一席之地", color = new Color(0.45f, 0.85f, 0.45f),
                npcs = new[]
                {
                    ("長老・翠", "守碑的精靈長老", new[] { "記名林裡的木牌不是路標。經過時，請別隨手折下帶走。", "木牌會朽，記得名字的人也會老。把故事教給下一個人，才算好好守住它。", "毒液噴花留下的毒會在地上停留，別急著踩進花囊破掉的地方。" }),
                    ("獵人・楓", "點葉燈的巡林人", new[] { "我的葉燈每十步掛一盞。回頭看得見上一盞，就不會把同伴丟在霧裡。", "樹王記得每個獵人的腳步聲。我小時候偷溜出村，總被牠叫住。", "看見荊棘衝鋒獸先別硬擋，留意地上的紅線，讓牠撲個空。" }),
                    ("小孩・小葉", "替古樹收集故事的孩子", new[] { "樹王說，每個名字後面都有一個故事。我也想讓自己的木牌，有人願意讀。", "我在練習刻木牌。刻錯了可以重來，忘了要寫誰才真的糟糕。", "等我認得所有字，就換我念故事給樹王聽。牠講了那麼久，也該歇一歇。" }),
                },
            },
            new Town
            {
                name = "潮騷港", desc = "等待歸航的港口，酒館的半首歌仍朝著舊燈塔飄去", color = new Color(0.4f, 0.7f, 1f),
                npcs = new[]
                {
                    ("船長・海斗", "白帆號船長的後人", new[] { "祖父留下的航圖，終點畫的不是寶藏，是這座港口。", "祖母常坐在酒館窗邊唱半首歌。我掌舵以後才懂，等船的人也走了一段遠路。", "礁岸的跳躍魚來得快，別只顧看遠處的浪，也留神腳邊。" }),
                    ("酒館老闆娘・凪", "記得歸航曲的掌櫃", new[] { "酒館靠窗的位子總留著。有人笑我少賺一桌，我就多收他一杯茶錢。", "母親教我的歌，我每天收店都會哼。有人聽見，這間酒館就不算冷清。", "燈油、熱茶、乾毛巾，都備好了。海上的事幫不上，岸上的事我來。" }),
                    ("漁夫・阿潮", "熟悉退潮道的老漁夫", new[] { "去舊燈塔要看潮時。海肯讓一條路，可不代表牠會一直等。", "我爹說，以前送船要一直唱，唱到船上的人再也聽不見為止。", "雷電水母放電時離遠點，河豚鼓起來也別湊近。活著回來才有魚湯喝。" }),
                },
            },
            new Town
            {
                name = "赤砂城", desc = "圍著角鬥場長大的城，觀眾的喝采蓋過了地底的鐵鍊聲", color = new Color(0.95f, 0.45f, 0.35f),
                npcs = new[]
                {
                    ("退役鬥士・雷歐", "斷了一隻角的老鬥士", new[] { "場上的大斧頭不會看你是誰。走路前先看地上那條紅帶子，斧刃就是沿著它來回。", "我打了二十年，從沒怕過對手。我怕的是看台上那些不再當人是人的眼睛。", "地刺發紅光就是要刺出來了。踩在鐵板上的時候，腳要比腦袋先動。" }),
                    ("賣票少女・小沙", "在看台間跑腿的女孩", new[] { "以前這裡只是比力氣的地方。自從霸主帶來那塊黑石頭，怪物就越關越多。", "我把每一場的名字都抄在票根背面。贏的、輸的，都是有人等他回家的人。", "聽說青銅鬥魂沒有臉。可我每次經過，都覺得它在看看台最高那一排。" }),
                    ("鍛甲師・鐵娘", "替鬥士修鎧甲的鐵匠", new[] { "鬥士的鎧甲我修了三十年，從沒見過會自己站起來的。那副青銅甲，是活的。", "斧頭不會累，人會。打不過的時候就退到兩把斧頭中間，喘口氣再上。", "帶藥水進場不丟臉。觀眾要看的是英雄，我只想看到你們走著出來。" }),
                },
            },
            new Town
            {
                name = "風沙驛", desc = "黃土山道上最後一座驛站，駝鈴與說書聲在崖壁間迴盪", color = new Color(0.9f, 0.72f, 0.42f),
                npcs = new[]
                {
                    ("驛站長・老駝", "守了一輩子驛站的老人", new[] { "山道兩邊都是崖壁。石頭往下掉之前，地上會先出現影子，看見就跑。", "以前馬幫一天過三趟。自從崖上住了那些東西，連鴿子都不肯從這裡飛。", "黑晶是從山道那頭運出來的。我記得那支商隊，他們不讓任何人看箱子。" }),
                    ("馬幫少女・阿鈴", "牽著駱駝的趕路人", new[] { "怪物會從崖頂跳下來。聽見上面有沙子落下的聲音，就先抬頭。", "我爹的駝鈴掛在驛站門口。他說只要鈴還在響，山道就還有人走。", "幫我看看崖上有沒有鈴鐺的反光。我爹的駝隊……還沒回來。" }),
                    ("說書人・石先生", "知道山道傳說的說書人", new[] { "傳說黃土高原底下睡著一座岩之鐘。鐘聲一停，山就會醒過來找人。", "三鐘響起那天，這裡的崖壁也跟著震了。我想，山也在等它的鐘。", "故事要有人聽才算數。你們回來的時候，記得把山道上的事說給我聽。" }),
                },
            },
        };

        /// <summary>城鎮抵達過（可以傳送）：起始之村一定有；之後要把前一章（前一個地區的關卡）全部打通才抵達</summary>
        public static bool TownOpen(int t) => t == 0 || t > 0 && t < Towns.Length && ChapterCleared(t - 1);

        /// <summary>這個地區（主題）的關卡全部通關</summary>
        public static bool ChapterCleared(int theme)
        {
            bool any = false;
            foreach (var s in Story.AllStages()) if (s.theme == theme) { any = true; if (!Story.IsCleared(s.id)) return false; }
            return any;
        }

        int gateTheme;
        public void OpenGate(int theme) { gateTheme = theme; screen = Screen.Gate; sel = null; }
        public void OpenWarp() { screen = Screen.Warp; }

        public void OpenTown(int t) { town = Mathf.Clamp(t, 0, Towns.Length - 1); screen = Screen.Town; npcTalk = -1; selItem = null; townMapInit = -1; walkMode = false; }   // 每次進村子都先到 2D 城鎮圖
        public void OpenGuild() { screen = Screen.Guild; npcTalk = -1; selItem = null; }
        public void OpenTrialShop() { screen = Screen.TrialShop; npcTalk = -1; selItem = null; }
        /// <summary>試煉商店的老闆：潮騷港的酒館老闆娘・凪</summary>
        public static bool IsTrialShopNpc(string name) => name != null && name.Contains("凪");
        /// <summary>新手指引用：正在聊天的居民、選中的關卡、選中的物品</summary>
        public string TalkingTo => screen == Screen.Town && !walkMode && npcTalk >= 0 && npcTalk < Towns[town].npcs.Length ? Towns[town].npcs[npcTalk].name : null;
        public string SelStageId => sel?.id;
        public Item SelItem => selItem;
        public long FocusUid;

        // ---------------------------------------------------------------- 畫面
        public void Draw(CharacterProfile prof)
        {
            Story.Load();
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            var safe = UnityEngine.Screen.safeArea;
            float topY = H - safe.yMax, botY = H - safe.y, pad = W * 0.03f;
            var r0 = new Rect(pad, topY + pad, W - pad * 2, botY - topY - pad * 2);
            int fs0 = UiKit.BaseFont;
            if (Walking) { DrawTownWalk(r0, prof, fs0, Mathf.RoundToInt(fs0 * 0.82f), Mathf.RoundToInt(fs0 * 1.3f), fs0 * 0.5f); return; }
            UiKit.Backdrop(new Rect(0,0,W,H), Towns[town].color);
            var r = new Rect(pad, topY + pad, W - pad * 2, botY - topY - pad * 2);
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.82f), fsL = Mathf.RoundToInt(fs * 1.3f);
            float gap = fs * 0.5f;
            switch (screen)
            {
                case Screen.Map: DrawMap(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Town: DrawTown(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Smith: DrawSmith(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Shop: DrawShop(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Guild: DrawGuild(r, prof, fs, fsS, fsL, gap); break;
                // 第四批的六個設施：有 2D 背景就用共用框架（背景、標題、右邊 NPC＋對話泡泡），左邊照舊畫功能（WorldUI.Shop2D.cs）
                case Screen.Gate: if (!DrawFramed("gate", "🚪 村口", new Color(1f, 0.85f, 0.45f), "guide", "嚮導", "往東走就是山路。這一章的關卡都從這裡出發。", a => DrawGate(a, prof, fs, fsS, fsL, gap), fs, fsS, fsL)) DrawGate(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Warp: if (!DrawFramed("warp", "🌀 傳送陣", new Color(0.55f, 0.65f, 1f), "guide", "嚮導", "站上法陣，就能回到去過的城鎮。", a => DrawWarp(a, prof, fs, fsS, fsL, gap), fs, fsS, fsL)) DrawWarp(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Dungeon: if (!DrawFramed("dungeon", "🏰 副本入口", new Color(0.4f, 0.85f, 0.75f), "guide", "嚮導", Facilities[0].hello, a => DrawDungeons(a, prof, fs, fsS, fsL, gap), fs, fsS, fsL)) DrawDungeons(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Training: if (!DrawFramed("training", "📅 訓練場", new Color(0.45f, 0.9f, 0.7f), "instructor", "教官", Facilities[1].hello, a => DrawTraining(a, prof, fs, fsS, fsL, gap), fs, fsS, fsL)) DrawTraining(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Bounty: if (!DrawFramed("bounty", "🔥 討伐告示板", new Color(1f, 0.45f, 0.2f), "bountyofficer", "懸賞官", Facilities[2].hello, a => DrawBounty(a, prof, fs, fsS, fsL, gap), fs, fsS, fsL)) DrawBounty(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Watch: if (!DrawFramed("watch", "👀 觀戰席", new Color(0.55f, 0.9f, 1f), "announcer", "播報員", Facilities[3].hello, a => DrawWatchDesk(a, prof, fs, fsS, fsL, gap), fs, fsS, fsL)) DrawWatchDesk(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Tower: DrawTower(r, prof, fs, fsS, fsL, gap); break;
                case Screen.Alchemy: DrawAlchemy(r, prof, fs, fsS, fsL, gap); break;   // 藥劑師（WorldUI.Craft.cs）
                case Screen.TrialShop: DrawTrialShop(r, prof, fs, fsS, fsL, gap); break;   // 試煉商店（WorldUI.Village.cs）
                case Screen.Craft: DrawCraft(r, prof, fs, fsS, fsL, gap); break;       // 工匠
            }
            if (toast != null && Time.time < toastUntil) UiKit.Pill(r.y + r.height * 0.5f, toast, fs, new Color(1f, 0.9f, 0.5f), r.width * 0.9f, 0.85f);
        }

        void Header(Rect r, string title, Color c, CharacterProfile prof, int fs, int fsS, int fsL, float gap, ref float y)
        {
            if (frame2D) { UiKit.Card(r, 0.72f, c, 0.7f); y += gap * 0.5f; return; }   // 2D 設施框架裡：標題、金幣已經畫在上方
            UiKit.Card(r, 0.95f, c, 0.9f);
            bool facility=screen==Screen.Smith||screen==Screen.Shop||screen==Screen.Guild;
            float hr = SettingsMenu.RightEdge(y, y + fs * 1.8f, r.xMax - gap);   // 右上角讓給 ⚙
            UiKit.Title(new Rect(r.x+gap,y,hr-r.x-gap-(facility?fs*1.9f:0),fs*1.8f),title,c,fsL);
            if(facility)UiKit.ShopArt(new Rect(hr-fs*1.65f,y,fs*1.65f,fs*1.65f),screen==Screen.Smith?0:screen==Screen.Shop?1:2,c);
            string cn = Skills.Classes.TryGetValue(prof.cls, out var cd) ? cd.name : prof.cls;
            // 背包容量（滿了紅字）、信箱（有信＝提醒幾天內要領）
            int bc = Progress.Bag.Count; var mails = Progress.Mailbox;
            int soonest = int.MaxValue; foreach (var m in mails) soonest = Mathf.Min(soonest, Progress.MailDaysLeft(m));
            string bagTxt = $"🎒 {bc}/{Progress.BagSize}" + (bc >= Progress.BagSize ? " 已滿" : "");
            string mailTxt = mails.Count > 0 ? $"📬 信箱 {mails.Count} 封（{(soonest <= 0 ? "今天" : soonest + " 天後")}刪除，到角色→裝備領取）" : "";
            UiKit.Text(new Rect(r.x + gap, y+fs*1.9f, r.width - gap * 2, fs * 1.0f), mailTxt, fsS, soonest <= 2 ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.8f, 0.4f), TextAnchor.MiddleLeft, true, 0.6f);
            UiKit.Text(new Rect(r.x + gap, y+fs*1.9f, r.width - gap * 2, fs * 1.0f), $"{cn} Lv{prof.level}　💰 {Economy.Gold}　{bagTxt}", fsS, bc >= Progress.BagSize ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleRight, true);
            y += fs * 3.1f;
        }

        // ---------------------------------------------------------------- 大地圖
        void DrawMap(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            if (DrawWorldMap2(r, prof, fs, fsS, fsL, gap)) return;   // 新的路線圖（WorldUI.WorldMap2.cs）；不能用時照舊
            float y = r.y + gap;
            Header(r, "🗺 大地圖", new Color(1f, 0.85f, 0.45f), prof, fs, fsS, fsL, gap, ref y);
            if (useOldMap && WorldMap2Usable && UiKit.Button(new Rect(r.xMax - gap - fs * 5f, y, fs * 5f, fs * 1.6f), "🗺 路線圖", fsS, false, new Color(1f, 0.85f, 0.45f))) { useOldMap = false; return; }
            if (useOldMap && WorldMap2Usable) y += fs * 1.8f;
            float detailH = sel != null ? fs * 9.6f : fs * 2.4f;
            float towerH = fs * 2.3f;
            float bottom = r.yMax - gap - fs * 2.3f - detailH - towerH - gap * 0.4f;
            float regH = (bottom - y) / Towns.Length;
            for (int t = 0; t < Towns.Length; t++)
            {
                var rr = new Rect(r.x + gap, y + t * regH, r.width - gap * 2, regH - gap * 0.5f);
                bool open = TownOpen(t);
                // 地區底色＋名稱
                UiKit.MapArt(rr,t,open?RegionColors[t]:Color.gray);
                UiKit.Text(new Rect(rr.x + gap * 0.5f, rr.y, rr.width * 0.9f, fs * 1.3f), $"{RegionNames[t]}（{Story.BandText(t)}）", fsS, open ? Color.white : Color.gray, TextAnchor.MiddleLeft, true);
                // 城鎮
                float tw = rr.width * 0.26f, th = Mathf.Min(rr.height - fs * 1.4f, fs * 3.2f);
                var trr = new Rect(rr.x + gap * 0.5f, rr.y + fs * 1.35f, tw, th);
                UiKit.NextTag("town:" + t);   // 新手指引：回村子
                if (UiKit.Button(trr, "", fsS, false, Towns[t].color, open)) { OpenTown(t); return; }
                UiKit.Text(new Rect(trr.x, trr.y, trr.width, trr.height), open ? $"🏘\n{Towns[t].name}" : "🔒\n未開放", fsS, open ? Color.white : Color.gray, TextAnchor.MiddleCenter, true);
                // 這個地區的關卡：沿著路線排開
                var stages = new List<StoryStage>();
                foreach (var s in Story.AllStages()) if (s.theme == t) stages.Add(s);
                float sx0 = trr.xMax + gap, sw = rr.xMax - sx0 - gap * 0.5f;
                int n = Mathf.Max(1, stages.Count);
                float nd = Mathf.Min(fs * 2.6f, sw / Mathf.Max(1, n) - gap * 0.3f, rr.height - fs * 1.5f);
                for (int i = 0; i < stages.Count; i++)
                {
                    var st = stages[i];
                    bool clear = Story.IsCleared(st.id), ok = Story.IsUnlocked(st);
                    float cx = sx0 + (i + 0.5f) * sw / n, cy = rr.y + fs * 1.35f + (rr.height - fs * 1.5f) * (i % 2 == 0 ? 0.35f : 0.65f);
                    // 路線
                    if (i > 0)
                    {
                        float px = sx0 + (i - 0.5f) * sw / n, py = rr.y + fs * 1.35f + (rr.height - fs * 1.5f) * ((i - 1) % 2 == 0 ? 0.35f : 0.65f);
                        DrawLine(new Vector2(px, py), new Vector2(cx, cy), ok ? new Color(1f, 0.95f, 0.8f, 0.6f) : new Color(1f, 1f, 1f, 0.15f), 3f);
                    }
                    var nr = new Rect(cx - nd / 2f, cy - nd / 2f, nd, nd);
                    var col = clear ? new Color(1f, 0.85f, 0.3f) : ok ? RegionColors[t] : new Color(0.4f, 0.4f, 0.45f);
                    UiKit.NextTag("stage:" + st.id);
                    if (UiKit.Button(nr, "", fsS, sel == st, col, ok)) sel = st;
                    UiKit.Text(nr, (clear ? "✔" : ok ? "▶" : "🔒") + "\n" + st.id, fsS, ok ? Color.white : Color.gray, TextAnchor.MiddleCenter, true);
                    if (ok) UiKit.Text(new Rect(nr.x - nd * 0.2f, nr.yMax - fsS * 0.2f, nd * 1.4f, fsS * 1.1f), Story.StarText(st.id), Mathf.RoundToInt(fsS * 0.8f), new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, true);   // 簡單／普通／困難各通關一次一顆星
                }
            }
            // 無盡塔（地圖上的一個地點）
            var twr = new Rect(r.x + gap, bottom + gap * 0.3f, r.width - gap * 2, towerH);
            string towerLabel = $"🗼 無盡塔　每次 5 波・已通過第 {(TowerBlocks?.Invoke() ?? 0) * 5} 波";
            bool towerOpen = Story.IsCleared("2-5");   // 打完 2-5 才能進無盡塔
            if (UiKit.Button(twr, "", fsS, false, TowerColor, towerOpen)) { screen = Screen.Tower; sel = null; return; }
            if (!towerOpen) towerLabel = "🔒 無盡塔　打完 2-5 才開放";
            UiKit.TowerArt(new Rect(twr.x+gap*.2f,twr.y+2,twr.height*1.1f,twr.height-4),TowerColor);
            UiKit.Text(new Rect(twr.x+twr.height*1.15f,twr.y,twr.width-twr.height*1.2f,twr.height),towerLabel,fsS,Color.white,TextAnchor.MiddleCenter,true);
            bottom += towerH + gap * 0.4f;
            // 選中的關卡
            var dr = new Rect(r.x + gap, bottom + gap * 0.3f, r.width - gap * 2, detailH - gap * 0.3f);
            UiKit.Card(dr, 0.6f);
            if (sel == null)
                UiKit.Text(new Rect(dr.x + gap, dr.y, dr.width - gap * 2, dr.height), "點城鎮進去補給；點關卡看說明。打過的關卡可以重打刷裝備（戰利品要過關才拿得到）。", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleLeft);
            else
            {
                bool clear = Story.IsCleared(sel.id), ok = Story.IsUnlocked(sel);
                string waves = sel.HasBoss && sel.fromWave >= 10 ? "Boss 戰" : $"{sel.toWave - sel.fromWave + 1} 波" + (sel.HasBoss ? "＋Boss" : "");
                UiKit.Text(new Rect(dr.x + gap, dr.y + gap * 0.3f, dr.width - gap * 2, fs * 1.4f), $"{sel.id}　{sel.title}" + (clear ? "　✔ 已通關" : Story.LockReason(sel) is string why ? $"　🔒 {why}" : ""), fs, Color.white, TextAnchor.MiddleLeft, true);
                UiKit.Text(new Rect(dr.x + gap, dr.y + fs * 1.7f, dr.width - gap * 2, fs * 2.6f),
                    (sel.desc.Length > 0 ? sel.desc.Split('\n')[0] + "\n" : "") + $"{RegionNames[sel.theme]}・{waves}・推薦 Lv{Mathf.Max(1, sel.FirstWave - 1) + Boost}+・掉落裝備 Lv{Story.LevelBand(sel.theme).lo + Boost}–{Story.LevelBand(sel.theme).hi + Boost}\n經驗 {Mathf.RoundToInt(sel.xp * SoloBattle.RewardMulFor(Difficulty))}・💰 {Mathf.RoundToInt(Economy.StageGold(sel) * SoloBattle.RewardMulFor(Difficulty))}", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperLeft);
                DifficultyRow(new Rect(dr.x + gap, dr.y + fs * 4.5f, dr.width - gap * 2, fs * 1.9f), fsS, true);
                bool story = Story.HasStory(sel) && Story.IsSeen(sel.id);   // 看過的故事不再自動播，可以按這裡重看
                int nb = story ? 3 : 2;
                float bw = (dr.width - gap * (nb + 1)) / nb, bh = fs * 2f, by = dr.yMax - bh - gap * 0.4f;
                if (UiKit.Button(new Rect(dr.x + gap, by, bw, bh), clear ? "⚔ 再次挑戰（刷裝備）" : "⚔ 出發", fsS, true, new Color(0.4f, 0.8f, 1f), ok))
                { var s = sel; sel = null; OnStartStage?.Invoke(s); return; }
                if (story && UiKit.Button(new Rect(dr.x + gap * 2 + bw, by, bw, bh), "📖 看故事", fsS))
                { var s = sel; OnWatchStory?.Invoke(s); return; }
                if (UiKit.Button(new Rect(dr.x + gap * nb + bw * (nb - 1), by, bw, bh), "關閉", fsS)) sel = null;
            }
            // 下方：角色、返回
            float bw2 = (r.width - gap * 3) / 2f, by2 = r.yMax - gap - fs * 2.1f;
            int pts = prof.statPoints + prof.UsableSkillPoints;
            if (UiKit.Button(new Rect(r.x + gap, by2, bw2, fs * 2.1f), pts > 0 ? $"📜 角色（可加點 ×{pts}）" : "📜 角色／裝備", fs, pts > 0, new Color(0.75f, 0.45f, 1f))) OnCharacter?.Invoke();
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw2, by2, bw2, fs * 2.1f), "返回", fs)) OnBack?.Invoke();
        }

        static void DrawLine(Vector2 a, Vector2 b, Color c, float w)
        {
            var m = GUI.matrix;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, len = (b - a).magnitude;
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.color = c; GUI.DrawTexture(new Rect(a.x, a.y - w / 2f, len, w), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.matrix = m;
        }

        // ---------------------------------------------------------------- 城鎮
        void DrawTown(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            if (DrawTownMap(r, prof, fs, fsS, fsL, gap)) return;   // 有 2D 俯瞰圖的城鎮（WorldUI.TownMap.cs）
            var t = Towns[town];
            float y = r.y + gap;
            Header(r, $"🏘 {t.name}", t.color, prof, fs, fsS, fsL, gap, ref y);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.2f), t.desc, fsS, new Color(0.85f, 0.88f, 1f));
            y += fs * 1.4f;
            // 居民
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.2f), "👥 居民（點一下聊天）", fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true);
            y += fs * 1.3f;
            float nw = (r.width - gap * 4) / 3f, nh = fs * 4.5f;
            for (int i = 0; i < t.npcs.Length; i++)
            {
                var nr = new Rect(r.x + gap + i * (nw + gap), y, nw, nh);
                UiKit.NextTag("npc:" + t.npcs[i].name);
                if (UiKit.Button(nr, "", fsS, npcTalk == i, t.color))
                {
                    if (npcTalk == i) npcLine = (npcLine + 1) % t.npcs[i].lines.Length; else { npcTalk = i; npcLine = 0; }
                    Sfx.Play("talk", 0.6f);
                }
                float ips = Mathf.Min(nw-gap,fs*2.4f);
                GUI.DrawTexture(new Rect(nr.center.x-ips*.5f, nr.y+gap*.4f, ips, ips), Portraits.Get(t.npcs[i].name, "npc"), ScaleMode.ScaleToFit, true);
                UiKit.Text(new Rect(nr.x+gap*.3f, nr.y+ips+gap*.5f, nr.width-gap*.6f, nr.height-ips-gap*.7f), $"{t.npcs[i].name}\n{t.npcs[i].role}", fsS, Color.white, TextAnchor.MiddleCenter, true);
            }
            y += nh + gap * 0.5f;
            var talkR = new Rect(r.x + gap, y, r.width - gap * 2, fs * 4.6f);
            UiKit.Card(talkR, 0.55f, npcTalk >= 0 ? t.color : (Color?)null, 0.7f);
            if (npcTalk >= 0)
            {
                // 說話者的頭像＋名字（左邊），台詞（右邊）
                float ps = talkR.height - fs * 1.5f - gap;
                var pr = new Rect(talkR.x + gap, talkR.y + gap * 0.5f, ps, ps);
                GUI.DrawTexture(pr, Portraits.Get(t.npcs[npcTalk].name, "npc"), ScaleMode.ScaleToFit, true);
                UiKit.Text(new Rect(pr.x - gap * 0.5f, pr.yMax, ps + gap, fs * 1.4f), t.npcs[npcTalk].name, fsS, t.color, TextAnchor.MiddleCenter, true);
                string ql = NpcQuestLabel?.Invoke(t.npcs[npcTalk].name);
                float qh = ql != null ? fs * 1.6f : 0f;
                UiKit.Text(new Rect(pr.xMax + gap, talkR.y, talkR.xMax - pr.xMax - gap * 2, talkR.height - qh), $"「{t.npcs[npcTalk].lines[npcLine]}」", fsS, Color.white, TextAnchor.MiddleLeft);
                // 初心者：這位居民有任務（接任務／進度／回報學招式）
                if (ql != null && UiKit.Button(new Rect(pr.xMax + gap, talkR.yMax - qh - gap * 0.3f, talkR.xMax - pr.xMax - gap * 2, qh), ql, fsS, true, new Color(1f, 0.8f, 0.35f)))
                    OnNpcQuest?.Invoke(t.npcs[npcTalk].name);
            }
            else UiKit.Text(new Rect(talkR.x + gap, talkR.y, talkR.width - gap * 2, talkR.height), "（點居民和他聊天，再點一次換下一句）", fsS, Color.white, TextAnchor.MiddleLeft);
            y += talkR.height + gap;
            // 設施
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.2f), "🏠 設施", fsS, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleLeft, true);
            y += fs * 1.3f;
            float bw = (r.width - gap * 3) / 2f, bh = fs * 3f;
            if (UiKit.Button(new Rect(r.x + gap, y, bw, bh), "⚒ 鐵匠鋪\n強化裝備", fs, false, new Color(1f, 0.6f, 0.3f))) { screen = Screen.Smith; selItem = null; page = 0; }
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw, y, bw, bh), "🧪 道具店\n買藥水・賣裝備", fs, false, new Color(0.5f, 0.85f, 1f))) { screen = Screen.Shop; selItem = null; page = 0; }
            y += bh + gap;
            if (UiKit.Button(new Rect(r.x + gap, y, bw, bh), "🏛 職業工會\n轉職・職業任務", fs, false, new Color(0.85f, 0.6f, 1f))) screen = Screen.Guild;
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw, y, bw, bh), "📜 角色／裝備", fs, false, new Color(0.75f, 0.45f, 1f))) OnCharacter?.Invoke();
            y += bh + gap;
            // 村子的其他設施：副本入口、訓練場、討伐告示板、觀戰席（兩排）
            float fw = (r.width - gap * 3) / 2f, fh = bh * 0.8f;
            for (int i = 0; i < Facilities.Length; i++)
            {
                var fa = Facilities[i];
                if (UiKit.Button(new Rect(r.x + gap + (i % 2) * (fw + gap), y + (i / 2) * (fh + gap * 0.5f), fw, fh), $"{fa.icon} {fa.title}", fsS, false, FacilityColors[i])) { OpenFacility(fa.kind); return; }
            }
            // 回大地圖／走進城鎮
            float hb = (r.width - gap * 3) / 2f;
            if (UiKit.Button(new Rect(r.x + gap, r.yMax - gap - fs * 2.1f, hb, fs * 2.1f), "🚶 逛街", fs, false, t.color)) walkMode = true;
            if (UiKit.Button(new Rect(r.x + gap * 2 + hb, r.yMax - gap - fs * 2.1f, hb, fs * 2.1f), "🗺 回大地圖", fs)) screen = Screen.Map;
        }

        // ---------------------------------------------------------------- 副本：清單、說明、進入（單人）／看故事；多人請從連線大廳選「🏰 副本」
        static readonly Color DungeonColor = new Color(0.4f, 0.85f, 0.75f);
        int dungeonPage;
        void DrawDungeons(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            float y = r.y + gap;
            Header(r, "🏰 副本入口", DungeonColor, prof, fs, fsS, fsL, gap, ref y);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2.6f),
                "「副本的敵人血量、攻擊都是一般關卡的 2 倍，數量再跟著隊伍人數變：最少以 2 人計算，3 人敵人數量和 Boss 血量 ×3、4 人 ×4。人越多掉寶率越高（3 人 ×1.25、4 人 ×1.5）。」——嚮導", fsS, new Color(0.85f, 0.95f, 0.9f), TextAnchor.UpperLeft);
            y += fs * 2.8f;
            // 找夥伴：轉到連線大廳（會離開村子；副本在隊伍畫面選）
            float gw = (r.width - gap * 3) / 2f, gh = fs * 2.3f;
            if (UiKit.Button(new Rect(r.x + gap, y, gw, gh), "👥 和身邊的朋友組隊", fsS, false, new Color(1f, 0.55f, 0.35f))) { OnNetDungeon?.Invoke(false); return; }
            if (UiKit.Button(new Rect(r.x + gap * 2 + gw, y, gw, gh), "🌐 和遠方的朋友組隊", fsS, false, new Color(0.75f, 0.55f, 1f))) { OnNetDungeon?.Invoke(true); return; }
            y += gh + gap * 0.5f;
            DifficultyRow(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.9f), fsS, true);   // 單人進入的難度（組隊時由房主在隊伍畫面選）
            y += fs * 2.2f;
            float bottom = r.yMax - gap * 2 - fs * 2.1f;
            // 副本很多時分頁（每頁 PerPage 個）
            const int PerPage = 4;
            int total = Dungeons.All.Count, pages = Mathf.Max(1, (total + PerPage - 1) / PerPage);
            dungeonPage = Mathf.Clamp(dungeonPage, 0, pages - 1);
            if (pages > 1)
            {
                float pw = fs * 3f, py = bottom - fs * 1.5f;
                UiKit.Text(new Rect(r.x + gap, py, r.width - gap * 2, fs * 1.4f), $"第 {dungeonPage + 1}／{pages} 頁（共 {total} 個副本）", fsS, new Color(0.85f, 0.95f, 0.9f), TextAnchor.MiddleCenter);
                if (UiKit.Button(new Rect(r.x + gap, py, pw, fs * 1.4f), "◀", fsS, false, null, dungeonPage > 0)) dungeonPage--;
                if (UiKit.Button(new Rect(r.xMax - gap - pw, py, pw, fs * 1.4f), "▶", fsS, false, null, dungeonPage < pages - 1)) dungeonPage++;
                bottom = py - gap * 0.4f;
            }
            int n = Mathf.Min(PerPage, total - dungeonPage * PerPage);
            float ch = Mathf.Min(fs * 5.2f, (bottom - y) / Mathf.Max(1, n) - gap * 0.4f);
            for (int di = dungeonPage * PerPage; di < Mathf.Min(total, (dungeonPage + 1) * PerPage); di++)
            {
                var d = Dungeons.All[di];
                bool clear = Dungeons.IsCleared(d.id);
                if (!d.Unlocked)
                {
                    var lr = new Rect(r.x + gap, y, r.width - gap * 2, ch);
                    UiKit.Card(lr, 0.35f, new Color(0.4f, 0.4f, 0.45f), 0.6f);
                    var us = Story.Find(d.unlock);
                    UiKit.Text(new Rect(lr.x + gap, lr.y, lr.width - gap * 2, lr.height), $"🔒 {d.name}　Lv{d.level + Boost}\n先通關故事「{d.unlock}{(us != null ? " " + us.title : "")}」才會開放", fsS, Color.gray, TextAnchor.MiddleLeft);
                    y += ch + gap * 0.4f;
                    continue;
                }
                var cr = new Rect(r.x + gap, y, r.width - gap * 2, ch);
                UiKit.Card(cr, 0.6f, clear ? new Color(1f, 0.85f, 0.3f) : DungeonColor, 0.8f);
                float relief = Dungeons.LevelRelief(prof.level, d.level);
                string diff = prof.level < d.level + Boost ? "⚠ 等級不足" : relief >= 0.95f ? "單人：非常困難" : relief >= 0.7f ? "單人：困難" : "單人：可以挑戰";
                UiKit.Text(new Rect(cr.x + gap, cr.y, cr.width * 0.62f, cr.height), $"{(clear ? "✔ " : "")}{d.name}　Lv{d.level + Boost}・{d.minPlayers}～{d.maxPlayers} 人・{d.SizeText}\n{d.desc}\n{diff}{(d.id==TrapGallery.Id ? (TrapGallery.BestSeconds>0 ? $"・最快 {TrapGallery.BestSeconds:0.0} 秒" : "・限時25分鐘") : "")}", fsS, Color.white, TextAnchor.MiddleLeft);
                float bw = cr.width * 0.34f, bh = (cr.height - gap * 1.2f) / 2f;
                if (UiKit.Button(new Rect(cr.xMax - bw - gap * 0.4f, cr.y + gap * 0.4f, bw, bh), "⚔ 單人進入", fsS, false, DungeonColor))
                { OnStartDungeon?.Invoke(d); return; }
                if (Story.IsSeen("dg:" + d.id) && UiKit.Button(new Rect(cr.xMax - bw - gap * 0.4f, cr.y + gap * 0.8f + bh, bw, bh), "📖 看故事", fsS))
                { OnWatchDungeon?.Invoke(d); return; }
                y += ch + gap * 0.4f;
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), $"🏘 回{Towns[town].name}", fs)) screen = Screen.Town;
        }

        // ---------------------------------------------------------------- 城鎮（3D 走動）：上方名稱、中間準心＋居民名牌、下方按鈕
        // ---------------------------------------------------------------- 方向鍵（3D 城鎮走路）
        /// <summary>方向鍵目前按著的方向（x＝右、y＝前）與轉身（+1＝右轉）；padFrame＝最後一次畫方向鍵的幀（沒畫就不算）</summary>
        public Vector2 PadMove; public float PadTurn; int padFrame = -9;
        public bool PadLive => Time.frameCount - padFrame <= 2;

        /// <summary>手指（或滑鼠）有沒有按在這個 GUI 範圍裡（按住不放也算）</summary>
        static bool Held(Rect r)
        {
            if (UiLock.Blocking) return false;   // 新手指引：只能按指定的按鈕
            float H = UnityEngine.Screen.height;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled && r.Contains(new Vector2(t.position.x, H - t.position.y))) return true;
            }
            return Input.touchCount == 0 && Input.GetMouseButton(0) && r.Contains(new Vector2(Input.mousePosition.x, H - Input.mousePosition.y));
        }

        /// <summary>左下角的方向鍵（▲▼◀▶）＋右下角的轉身鈕（↺↻）；回傳方向鍵區塊的上緣</summary>
        float DrawPad(Rect r, float bottom, int fs, float gap)
        {
            float b = Mathf.Min(fs * 2.6f, r.width * 0.15f);
            float cx = r.x + gap + b * 1.5f, top = bottom - b * 3f;
            Rect up = new Rect(cx - b / 2f, top, b, b), dn = new Rect(cx - b / 2f, top + b * 2f, b, b);
            Rect lf = new Rect(cx - b * 1.5f, top + b, b, b), rt = new Rect(cx + b / 2f, top + b, b, b);
            Rect tl = new Rect(r.xMax - gap - b * 2.2f, top + b, b, b), tr = new Rect(r.xMax - gap - b, top + b, b, b);
            bool hu = Held(up), hd = Held(dn), hl = Held(lf), hr = Held(rt), htl = Held(tl), htr = Held(tr);
            var col = new Color(0.55f, 0.9f, 1f);
            void Key(Rect k, string label, bool on)
            {
                UiKit.Card(k, on ? 0.85f : 0.5f, col, on ? 1f : 0.5f);
                UiKit.Text(k, label, Mathf.RoundToInt(fs * 1.1f), on ? Color.white : new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, true);
                if (Event.current.type == EventType.MouseDown && k.Contains(Event.current.mousePosition)) Event.current.Use();   // 按方向鍵不要穿透到底下
            }
            Key(up, "▲", hu); Key(dn, "▼", hd); Key(lf, "◀", hl); Key(rt, "▶", hr);
            Key(tl, "↺", htl); Key(tr, "↻", htr);
            UiKit.Text(new Rect(tl.x - gap, tl.y - fs * 1.3f, tr.xMax - tl.x + gap * 2, fs * 1.2f), "轉身", Mathf.RoundToInt(fs * 0.75f), new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            PadMove = new Vector2((hr ? 1f : 0f) - (hl ? 1f : 0f), (hu ? 1f : 0f) - (hd ? 1f : 0f));
            PadTurn = (htr ? 1f : 0f) - (htl ? 1f : 0f);
            padFrame = Time.frameCount;
            return top;
        }

        void DrawTownWalk(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            var t = Towns[town];
            float W = UnityEngine.Screen.width, H = UnityEngine.Screen.height;
            // 居民名牌（3D 位置投影到畫面）
            if (Cam != null)
                foreach (var n in Walk.npcs)
                {
                    if (!Walk.LabelPos(Cam, n, out var g, out float dist) || dist > 16f) continue;
                    bool isNear = Walk.Near == n;
                    float k = Mathf.Clamp(1.4f - dist / 12f, 0.55f, 1.2f);
                    int lf = Mathf.RoundToInt(fsS * k);
                    float lw = lf * 9f, lh = lf * (isNear ? 3.6f : 2.4f);
                    var lr = new Rect(g.x - lw / 2f, g.y - lh, lw, lh);
                    UiKit.Card(lr, isNear ? 0.8f : 0.45f, n.opens != null ? new Color(1f, 0.85f, 0.4f) : t.color, isNear ? 1f : 0.5f);
                    string icon = n.opens == "smith" ? "⚒ " : n.opens == "shop" ? "🧪 " : n.opens == "guild" ? "🏛 " : FacilityIcon(n.opens);
                    string qmark = NpcQuestLabel?.Invoke(n.name) != null ? "❗" : "";   // 初心者：有任務的居民
                    UiKit.Text(lr, $"{qmark}{icon}{n.name}\n{n.role}" + (isNear ? "\n💬 說「你好」" : ""), lf, isNear ? new Color(1f, 0.95f, 0.6f) : Color.white, TextAnchor.MiddleCenter, true);
                }
            // 建築招牌：走近（12m 內）時在屋頂上方顯示做什麼的、找誰；越近越清楚
            if (Cam != null)
                foreach (var site in Walk.sites)
                {
                    if (!Walk.ScreenPos(Cam, site.pos + Vector3.up * site.height, out var sg, out float sd) || sd > 12f) continue;
                    var info = SiteInfo(site.kind);
                    bool close = sd < 6f;
                    float k = Mathf.Clamp(1.3f - sd / 14f, 0.6f, 1.15f);
                    int sf = Mathf.RoundToInt(fsS * k);
                    float sw = sf * 13f, sh = sf * (close ? 3.9f : 2.6f);
                    var sr = new Rect(sg.x - sw / 2f, sg.y - sh, sw, sh);
                    UiKit.Card(sr, close ? 0.85f : 0.55f, info.color, close ? 1f : 0.6f);
                    UiKit.Text(sr, $"{info.title}\n{info.what}" + (close ? $"\n💬 找門口的{info.who}說「你好」" : ""), sf, close ? Color.white : new Color(0.92f, 0.94f, 1f), TextAnchor.MiddleCenter, true);
                }
            // 出口與傳送陣的標示
            if (Cam != null)
            {
                foreach (var e in Walk.exits)
                    if (Walk.ScreenPos(Cam, e.pos + Vector3.up * 2.6f, out var g, out float d) && d < 30f)
                    {
                        float lw = fsS * 10f, lh = fsS * 2.6f;
                        var lr = new Rect(g.x - lw / 2f, g.y - lh, lw, lh);
                        UiKit.Card(lr, 0.65f, RegionColors[e.theme], 0.9f);
                        UiKit.Text(lr, $"🚪 第 {e.theme + 1} 章 {RegionNames[e.theme]}\n{Story.BandText(e.theme)}・走出去選關卡", fsS, Color.white, TextAnchor.MiddleCenter, true);
                    }
                if (Walk.ScreenPos(Cam, Walk.WarpPos + Vector3.up * 0.6f, out var wg, out float wd) && wd < 20f)
                {
                    float lw = fsS * 8f, lh = fsS * 2.4f;
                    var lr = new Rect(wg.x - lw / 2f, wg.y - lh, lw, lh);
                    UiKit.Card(lr, 0.55f, new Color(0.6f, 0.5f, 1f), 0.8f);
                    float wp = Walk.WarpProgress;
                    UiKit.Text(lr, wp > 0f ? $"🌀 傳送中… {Mathf.RoundToInt(wp * 100)}%" : "🌀 傳送陣\n站上去 0.5 秒傳送到其他城鎮", fsS, Color.white, TextAnchor.MiddleCenter, true);
                    if (wp > 0f) UiKit.Bar(new Rect(lr.x, lr.yMax + 2f, lr.width, lh * 0.25f), wp, new Color(0.6f, 0.5f, 1f), "", fsS);
                }
            }
            // 準心
            float cs = fs * 0.9f;
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.DrawTexture(new Rect(W / 2f - cs / 2f, H / 2f - 1.5f, cs, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(W / 2f - 1.5f, H / 2f - cs / 2f, 3f, cs), Texture2D.whiteTexture);
            GUI.color = Color.white;
            // 上方：城鎮名稱＋說明
            var top = new Rect(r.x, r.y, r.width, fs * 3.6f);
            UiKit.Card(top, 0.7f, t.color, 0.9f);
            UiKit.Text(new Rect(top.x + gap, top.y, top.width*.46f-gap, fs * 1.8f), $"🏘 {t.name}", fsL, Color.white, TextAnchor.MiddleLeft, true);
            string cn = Skills.Classes.TryGetValue(prof.cls, out var cd) ? cd.name : prof.cls;
            UiKit.Text(new Rect(top.x + top.width*.46f, top.y, SettingsMenu.RightEdge(top.y, top.y + fs * 1.8f, top.xMax - gap) - (top.x + top.width*.46f), fs * 1.8f), $"{cn} Lv{prof.level}　💰 {Economy.Gold}", fsS, new Color(1f, 0.9f, 0.5f), TextAnchor.MiddleRight, true);
            UiKit.Text(new Rect(top.x + gap, top.y + fs * 1.7f, top.width - gap * 2, fs * 1.8f), "🕹 左下方向鍵走路、右下轉身（也可以上下晃動手機原地踏步）　💬 走到居民面前說「你好」或揮手聊天", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleLeft);
            float y = top.yMax + gap * 0.5f;
            if (!string.IsNullOrEmpty(SpeechNote)) { UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.3f), SpeechNote, fsS, new Color(1f, 0.8f, 0.5f), TextAnchor.MiddleLeft); y += fs * 1.4f; }
            if (Heard != null && Time.time - HeardAt < 2f) UiKit.Pill(y + fs, $"🎤 {Heard}", fsS, Color.white, r.width * 0.7f, 0.6f);
            if (Walk.Walking) UiKit.Pill(H / 2f + fs * 2.5f, "🚶 走路中", fsS, new Color(0.7f, 1f, 0.7f), r.width * 0.4f, 0.4f);

            // 下方：沒有「前進」「打招呼」按鈕（晃動手機走路、說話或揮手打招呼）；角色／清單／大地圖
            float bh = fs * 2.3f, by = r.yMax - bh - gap;
            var near = Walk.Near;
            float padTop = DrawPad(r, by - gap, fs, gap);
            if (near != null) UiKit.Pill(padTop - fs * 1.4f, $"👋 對{Portraits.Key(near.name)}說「你好」，或舉起手掌揮一揮", fsS, Color.white, r.width * 0.9f, 0.7f);
            float bw3 = (r.width - gap * 5) / 4f, by2 = by;
            int pts = prof.statPoints + prof.UsableSkillPoints;
            if (UiKit.Button(new Rect(r.x + gap, by2, bw3, bh * 0.85f), pts > 0 ? $"📜 角色 ×{pts}" : "📜 角色", fsS, pts > 0, new Color(0.75f, 0.45f, 1f))) OnCharacter?.Invoke();
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw3, by2, bw3, bh * 0.85f), "🏰 副本", fsS, false, DungeonColor)) screen = Screen.Dungeon;
            if (UiKit.Button(new Rect(r.x + gap * 3 + bw3 * 2, by2, bw3, bh * 0.85f), "🗺 城鎮圖", fsS)) walkMode = false;   // 回 2D 城鎮圖
            if (UiKit.Button(new Rect(r.x + gap * 4 + bw3 * 3, by2, bw3, bh * 0.85f), "🗺 大地圖", fsS)) screen = Screen.Map;
        }

        // ---------------------------------------------------------------- 城鎮出口：選這一章的關卡
        void DrawGate(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            int th = Mathf.Clamp(gateTheme, 0, RegionNames.Length - 1);
            float y = r.y + gap;
            Header(r, $"🚪 第 {th + 1} 章 {RegionNames[th]}", RegionColors[th], prof, fs, fsS, fsL, gap, ref y);
            var band = Story.LevelBand(th);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.3f), $"{Story.BandText(th)}　掉落裝備 Lv{band.lo + Boost}–{band.hi + Boost}　打過的關卡可以重打刷裝備", fsS, new Color(0.85f, 0.88f, 1f));
            y += fs * 1.5f;
            DifficultyRow(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.9f), fsS, true);   // 普通＝＋40 級、困難＝＋80 級
            y += fs * 2.2f;
            var stages = new List<StoryStage>();
            foreach (var s in Story.AllStages()) if (s.theme == th) stages.Add(s);
            float bottom = r.yMax - gap * 2 - fs * 2.1f;
            float ch = Mathf.Min(fs * 4.2f, (bottom - y) / Mathf.Max(1, stages.Count) - gap * 0.4f);
            foreach (var st in stages)
            {
                bool clear = Story.IsCleared(st.id), ok = Story.IsUnlocked(st);
                var cr = new Rect(r.x + gap, y, r.width - gap * 2, ch);
                UiKit.Card(cr, ok ? 0.6f : 0.35f, clear ? new Color(1f, 0.85f, 0.3f) : ok ? RegionColors[th] : new Color(0.4f, 0.4f, 0.45f), 0.8f);
                string waves = st.HasBoss && st.fromWave >= 10 ? "Boss 戰" : $"{st.toWave - st.fromWave + 1} 波" + (st.HasBoss ? "＋Boss" : "");
                UiKit.Text(new Rect(cr.x + gap, cr.y, cr.width * 0.68f, cr.height), $"{(clear ? "✔" : ok ? "▶" : "🔒")} {st.id}　{st.title}　{(ok ? Story.StarText(st.id) : "")}\n{waves}・推薦 Lv{Mathf.Max(1, st.FirstWave - 1) + Boost}+・經驗 {Mathf.RoundToInt(st.xp * SoloBattle.RewardMulFor(Difficulty))}・💰 {Mathf.RoundToInt(Economy.StageGold(st) * SoloBattle.RewardMulFor(Difficulty))}",
                    fsS, ok ? Color.white : Color.gray, TextAnchor.MiddleLeft, true);
                bool story = Story.HasStory(st) && Story.IsSeen(st.id);
                float bwid = cr.width * (story ? 0.2f : 0.3f);
                var br = new Rect(cr.xMax - bwid, cr.y + gap * 0.4f, bwid - gap * 0.4f, cr.height - gap * 0.8f);
                if (UiKit.Button(br, ok ? (clear ? "⚔ 再打" : "⚔ 出發") : Story.LockReason(st) is string lr ? (lr.Contains("轉職") ? "🔒 先轉職" : "🔒 先上投石課") : "🔒 先過前一關", fsS, ok && !clear, RegionColors[th], ok))
                { OnStartStage?.Invoke(st); return; }
                if (story && UiKit.Button(new Rect(br.x - bwid, br.y, bwid - gap * 0.4f, br.height), "📖 故事", fsS))
                { OnWatchStory?.Invoke(st); return; }
                y += ch + gap * 0.4f;
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), $"🏘 回{Towns[town].name}", fs)) screen = Screen.Town;
        }

        // ---------------------------------------------------------------- 傳送陣：傳送到抵達過的城鎮
        void DrawWarp(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            float y = r.y + gap;
            Header(r, "🌀 傳送陣", new Color(0.6f, 0.5f, 1f), prof, fs, fsS, fsL, gap, ref y);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.3f), "抵達過的城鎮可以直接傳送過去；打通一章後就能走到下一個城鎮。", fsS, new Color(0.85f, 0.88f, 1f));
            y += fs * 1.6f;
            float ch = fs * 4.2f;
            for (int t = 0; t < Towns.Length; t++)
            {
                bool open = TownOpen(t), here = t == town;
                var cr = new Rect(r.x + gap, y, r.width - gap * 2, ch);
                UiKit.Card(cr, open ? 0.6f : 0.35f, open ? Towns[t].color : new Color(0.4f, 0.4f, 0.45f), 0.8f);
                UiKit.Text(new Rect(cr.x + gap, cr.y, cr.width * 0.66f, cr.height), $"🏘 {Towns[t].name}　{RegionNames[t]}\n" + (open ? Towns[t].desc : $"🔒 打通第 {t} 章後抵達"), fsS, open ? Color.white : Color.gray, TextAnchor.MiddleLeft, true);
                var br = new Rect(cr.xMax - cr.width * 0.3f, cr.y + gap * 0.4f, cr.width * 0.3f - gap * 0.4f, cr.height - gap * 0.8f);
                if (UiKit.Button(br, here ? "📍 目前位置" : open ? "🌀 傳送" : "🔒", fsS, open && !here, Towns[t].color, open && !here))
                { OpenTown(t); Sfx.Play("poof", 0.8f); Haptics.Play(Haptics.Kind.Success); return; }
                y += ch + gap * 0.5f;
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), $"🏘 留在{Towns[town].name}", fs)) screen = Screen.Town;
        }

        /// <summary>打完招呼後打開設施</summary>
        public void OpenFacility(string kind)
        {
            selItem = null; page = 0;
            screen = kind == "smith" ? Screen.Smith : kind == "shop" ? Screen.Shop : kind == "guild" ? Screen.Guild : FacilityScreen(kind);
        }

        // ---------------------------------------------------------------- 物品清單（身上＋背包，分頁）
        List<Item> AllItems(CharacterProfile prof)
        {
            var list = new List<Item>();
            foreach (var it in prof.equipped) if (it != null) list.Add(it);
            list.AddRange(Progress.Bag);
            return list;
        }

        /// <summary>畫物品格子（每頁 8 件）；回傳下一個 y</summary>
        float DrawItemGrid(Rect r, List<Item> items, CharacterProfile prof, int fs, int fsS, float gap, float y, Func<Item, string> sub)
        {
            const int per = 8;
            int pages = Mathf.Max(1, (items.Count + per - 1) / per);
            if (FocusUid != 0) { int fi = items.FindIndex(x => x.uid == FocusUid); if (fi >= 0) page = fi / per; FocusUid = 0; }   // 新手指引：翻到要點的那一件
            page = Mathf.Clamp(page, 0, pages - 1);
            float cw = (r.width - gap * 3) / 2f, ch = fs * 2.2f;
            for (int i = 0; i < per; i++)
            {
                int k = page * per + i;
                if (k >= items.Count) break;
                var it = items[k];
                var cr = new Rect(r.x + gap + (i % 2) * (cw + gap), y + (i / 2) * (ch + gap * 0.4f), cw, ch);
                bool worn = Array.IndexOf(prof.equipped, it) >= 0;
                UiKit.NextTag("item:" + it.uid);
                if (UiKit.Button(cr, "", fsS, selItem == it, Item.RarityColors[it.rarity])) selItem = it;
                UiKit.Text(new Rect(cr.x + 3, cr.y, cr.width - 6, cr.height * 0.55f), (worn ? "👕 " : "") + it.FullName, fsS, Item.RarityColors[it.rarity], TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(cr.x + 3, cr.y + cr.height * 0.5f, cr.width - 6, cr.height * 0.48f), sub(it), Mathf.RoundToInt(fsS * 0.85f), new Color(0.85f, 0.88f, 1f), TextAnchor.MiddleCenter);
            }
            y += 4 * (ch + gap * 0.4f);
            if (pages > 1)
            {
                float pw = r.width * 0.2f;
                if (UiKit.Button(new Rect(r.x + gap, y, pw, fs * 1.6f), "◀", fsS, false, null, page > 0)) page--;
                UiKit.Text(new Rect(r.x, y, r.width, fs * 1.6f), $"{page + 1}/{pages}", fsS, Color.white, TextAnchor.MiddleCenter);
                if (UiKit.Button(new Rect(r.xMax - gap - pw, y, pw, fs * 1.6f), "▶", fsS, false, null, page < pages - 1)) page++;
                y += fs * 1.8f;
            }
            return y;
        }

        // ---------------------------------------------------------------- 鐵匠鋪
        void DrawSmith(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            float y = r.y + gap;
            if (DrawSmith2D(prof, fs, fsS, fsL)) return;   // 2D 鐵匠鋪（WorldUI.Smith2D.cs）
            Header(r, $"⚒ 鐵匠鋪（{Towns[town].name}）", new Color(1f, 0.6f, 0.3f), prof, fs, fsS, fsL, gap, ref y);
            UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.3f), $"每強化 1 級：主屬性 +{Economy.EnhancePerLevel * 100:0}%（最多 +{Economy.MaxPlus}）。+5 之前一定成功，之後失敗只扣金幣。", fsS, new Color(0.85f, 0.88f, 1f));
            y += fs * 1.5f;
            var items = AllItems(prof);
            if (items.Count == 0) UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2f), "沒有裝備可以強化（過關才拿得到裝備）", fs, Color.gray);
            y = DrawItemGrid(r, items, prof, fs, fsS, gap, y, it => it.plus >= Economy.MaxPlus ? "已滿級" : $"→+{it.plus + 1}　💰{Economy.EnhanceCost(it)}・{Economy.EnhanceRate(it) * 100:0}%");
            if (selItem != null && items.Contains(selItem))
            {
                var dr = new Rect(r.x + gap, y, r.width - gap * 2, fs * 4.2f);
                UiKit.Card(dr, 0.6f, Item.RarityColors[selItem.rarity], 0.8f);
                var mods = new List<string>();
                mods.AddRange(selItem.ModLines());
                if (selItem.plus < Economy.MaxPlus && selItem.mods.Count > 0) mods.Add($"強化後 {Item.AffixText(selItem.mods[0].Key, selItem.mods[0].Value * (1f + Economy.EnhancePerLevel * (selItem.plus + 1)))}（只加主屬性）");
                UiKit.Text(new Rect(dr.x + gap, dr.y, dr.width - gap * 2, dr.height * 0.55f), $"{selItem.FullName}\n{string.Join("、", mods)}", fsS, Color.white, TextAnchor.MiddleLeft, false, 0.5f);
                bool can = selItem.plus < Economy.MaxPlus;
                if (UiKit.Button(new Rect(dr.x + dr.width * 0.2f, dr.yMax - fs * 1.9f, dr.width * 0.6f, fs * 1.7f), can ? $"⚒ 強化（💰 {Economy.EnhanceCost(selItem)}・成功率 {Economy.EnhanceRate(selItem) * 100:0}%）" : "已經是 +10", fsS, true, new Color(1f, 0.6f, 0.3f), can))
                {
                    bool ok = Economy.Enhance(selItem, rng, out var msg);
                    Toast(msg); Sfx.Play("anvil", 0.8f); if (ok) Sfx.Play("charged", 0.6f, 1.2f); else Sfx.Play("fail", 0.7f);
                    Progress.SaveBag(); Progress.SaveChar(prof);
                }
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), "🏘 回城鎮", fs)) screen = Screen.Town;
        }

        // ---------------------------------------------------------------- 道具店
        void DrawShop(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            float y = r.y + gap;
            if (DrawShop2D(prof, fs, fsS, fsL)) return;   // 2D 道具店（WorldUI.Shop2D.cs）
            Header(r, $"🧪 道具店（{Towns[town].name}）", new Color(0.5f, 0.85f, 1f), prof, fs, fsS, fsL, gap, ref y);
            float tw = (r.width - gap * 3) / 2f;
            if (UiKit.Button(new Rect(r.x + gap, y, tw, fs * 1.8f), "買道具", fs, shopTab == 0)) { shopTab = 0; selItem = null; }
            if (UiKit.Button(new Rect(r.x + gap * 2 + tw, y, tw, fs * 1.8f), "賣裝備", fs, shopTab == 1)) { shopTab = 1; selItem = null; page = 0; }
            UiKit.Tab(new Rect(r.x+gap,y,tw,fs*1.8f),shopTab==0,new Color(.5f,.85f,1));
            UiKit.Tab(new Rect(r.x+gap*2+tw,y,tw,fs*1.8f),shopTab==1,UiKit.Gold);
            y += fs * 2.1f;
            if (shopTab == 0)
            {
                foreach (var c in Economy.Consumables)
                {
                    var cr = new Rect(r.x + gap, y, r.width - gap * 2, fs * 2.6f);
                    UiKit.Card(cr, 0.8f, new Color(.5f,.85f,1));
                    UiKit.Text(new Rect(cr.x + gap, cr.y, cr.width * 0.62f, cr.height), $"{c.icon} {c.name}（持有 {Economy.Count(c.id)}）\n{c.desc}", fsS, Color.white, TextAnchor.MiddleLeft, true);
                    UiKit.NextTag("buy:" + c.id);
                    if (UiKit.Button(new Rect(cr.xMax - cr.width * 0.33f, cr.y + gap * 0.3f, cr.width * 0.32f - gap * 0.3f, cr.height - gap * 0.6f), $"💰 {c.price}", fsS, false, new Color(1f, 0.85f, 0.3f), Economy.Gold >= c.price))
                    {
                        if (Economy.Buy(c, out var why)) { Toast($"買了 {c.name}"); Sfx.Play("coin", 0.8f); } else Toast(why);
                    }
                    y += cr.height + gap * 0.4f;
                }
                UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2.2f), "藥水在戰鬥中用唸的（「喝藥水」「大藥水」「魔力藥水」）或比 🤙 使用，30 秒只能喝一瓶；回城捲軸說「回城」，只能在故事關卡用。", fsS, new Color(0.8f, 0.84f, 0.95f), TextAnchor.UpperLeft);
            }
            else
            {
                var bag = new List<Item>(Progress.Bag);
                UiKit.Bar(new Rect(r.x + gap, y, r.width - gap * 2, fs * 1.1f), bag.Count / (float)Progress.BagSize, bag.Count >= Progress.BagSize ? new Color(1f, 0.35f, 0.3f) : bag.Count >= Progress.BagSize * 0.8f ? new Color(1f, 0.8f, 0.3f) : new Color(0.45f, 0.85f, 0.5f), $"背包 {bag.Count}/{Progress.BagSize}" + (bag.Count >= Progress.BagSize ? "（已滿：新拿到的裝備會寄到信箱，7 天後刪除）" : $"（空 {Progress.BagFree} 格）"), fsS);
                y += fs * 1.4f;
                if (bag.Count == 0) UiKit.Text(new Rect(r.x + gap, y, r.width - gap * 2, fs * 2f), "背包是空的（身上穿的要先卸下才能賣）", fs, Color.gray);
                y = DrawItemGrid(r, bag, prof, fs, fsS, gap, y, it => $"賣 💰 {Economy.SellPrice(it)}");
                if (selItem != null && bag.Contains(selItem))
                {
                    if (UiKit.Button(new Rect(r.x + r.width * 0.2f, y, r.width * 0.6f, fs * 2f), $"賣掉 {selItem.FullName}（💰 {Economy.SellPrice(selItem)}）", fsS, true, new Color(1f, 0.85f, 0.3f)))
                    {
                        Economy.Gold += Economy.SellPrice(selItem);
                        Progress.Bag.Remove(selItem); Progress.SaveBag();
                        Toast($"賣掉了 {selItem.FullName}"); Sfx.Play("coin", 0.8f, 0.9f);
                        selItem = null;
                    }
                }
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), "🏘 回城鎮", fs)) screen = Screen.Town;
        }

        // ---------------------------------------------------------------- 職業工會（轉職：Lv30 → 轉職試煉 → 二選一，選了不能換）
        string advPick;                // 正在確認的進階職業
        float advPickAt;
        int guildScroll;

        string jobInfo; Vector2 jobInfoScroll;   // 轉職：正在看哪個職業的介紹

        /// <summary>職業介紹：特色、能力、普攻、一開始的 3 招、技能樹流派、30 級的兩個進階職業</summary>
        static string JobIntro(string id)
        {
            var c = Skills.Classes[id];
            var sb = new System.Text.StringBuilder();
            sb.Append(c.desc).Append("\n\n");
            sb.Append($"❤ HP {c.maxHp}　💧 MP {c.maxMp}　主要能力：{StatInfo.Names[(int)StatInfo.AttackStat(id)]}\n");
            var basic = Skills.BasicOf(id);
            if (basic != null) sb.Append($"\n👊 普攻「{basic.name}」：{basic.desc}\n");
            sb.Append("\n✨ 一開始會的技能（唸技能名稱 → 放招手勢）：\n");
            foreach (var sid in SkillTrees.Starters(id))
                if (Skills.All.TryGetValue(sid, out var sk)) sb.Append($"・{sk.name}（{HandGesture.ReleaseLabel(sk)}）：{sk.desc}\n");
            var schools = SkillTrees.Schools(id);
            if (schools != null) sb.Append($"\n🌳 技能樹三個流派：{string.Join("、", schools)}\n");
            sb.Append($"\n🏛 {Advanced.Level} 級可以進階（通過試煉後二選一，選了不能換）：\n");
            foreach (var a in Advanced.All.Values)
            {
                if (a.baseCls != id) continue;
                sb.Append($"\n【{a.name}】{a.desc}\n　核心：{a.core}\n" + (string.IsNullOrEmpty(a.weak) ? "" : $"　代價：{a.weak}\n"));
                var names = new System.Collections.Generic.List<string>();
                foreach (var sid in a.skills) if (Skills.All.TryGetValue(sid, out var sk)) names.Add(sk.name);
                sb.Append($"　進階技能：{string.Join("、", names)}\n");
            }
            return sb.ToString();
        }

        void DrawJobInfo(Rect r, CharacterProfile prof, int fs, int fsS, float gap, float y)
        {
            var c = Skills.Classes[jobInfo];
            bool ok = prof.CanChangeJob;
            var cr = new Rect(r.x + gap, y, r.width - gap * 2, r.yMax - gap * 3 - fs * 2.2f - y);
            UiKit.Card(cr, 0.9f, c.color, 0.9f);
            UiKit.Text(new Rect(cr.x + gap, cr.y + gap * 0.5f, cr.width - gap * 2, fs * 1.6f), $"{c.name}的介紹", Mathf.RoundToInt(fs * 1.15f), c.color, TextAnchor.MiddleLeft, true);
            string text = JobIntro(jobInfo);
            // 職業 2D 立繪（Codex）：放在右邊，介紹文字在左邊
            var art = Story2D.ClassPortrait(jobInfo, null);
            float artW = art != null ? cr.width * 0.42f : 0f;
            if (art != null)
            {
                float ah = Mathf.Min(cr.height - fs * 2.2f, artW * art.height / Mathf.Max(1f, art.width));
                GUI.DrawTexture(new Rect(cr.xMax - artW - gap * 0.5f, cr.y + fs * 2f, artW, ah), art, ScaleMode.ScaleToFit, true);
            }
            var view = new Rect(cr.x + gap, cr.y + fs * 2f, cr.width - gap * 2 - artW, cr.height - fs * 2.4f);
            float th = UiKit.TextHeight(text, view.width - fs, fsS) + fs;
            var content = new Rect(0, 0, view.width - fs * 0.8f, Mathf.Max(view.height, th));
            jobInfoScroll = UiKit.DragScroll(view, jobInfoScroll, content);
            jobInfoScroll = GUI.BeginScrollView(view, jobInfoScroll, content, false, true);
            UiKit.Text(new Rect(0, 0, content.width, th), text, fsS, new Color(0.93f, 0.95f, 1f), TextAnchor.UpperLeft, false, 0.55f);
            GUI.EndScrollView();
            float bw = (r.width - gap * 3) / 2f, by = r.yMax - gap - fs * 2.1f;
            if (UiKit.Button(new Rect(r.x + gap, by, bw, fs * 2.1f), "◀ 返回", fs)) { jobInfo = null; return; }
            bool armed = jobArm == jobInfo && Time.time < jobArmUntil, lvOk2 = prof.level >= Skills.JobLevel, mine = prof.jobTrialCls == jobInfo;
            string bt = !lvOk2 ? $"Lv{Skills.JobLevel} 後" : ok ? (mine ? (armed ? "再按一次確定" : $"✨ 成為{c.name}") : "你選的不是這條") : mine ? "繼續試煉" : prof.jobTrial > 0 ? "重選這條路（試煉重來）" : "選這條路（挑戰試煉）";
            if (UiKit.Button(new Rect(r.x + gap * 2 + bw, by, bw, fs * 2.1f), bt, fsS, armed, c.color, lvOk2 && (!ok || mine)))
            {
                if (!ok) { var id = jobInfo; jobInfo = null; OnJobTrialPick?.Invoke(id); return; }
                if (armed) { var id = jobInfo; jobArm = null; jobInfo = null; OnJobChange?.Invoke(id); return; }
                jobArm = jobInfo; jobArmUntil = Time.time + 3f;
                Toast($"成為「{c.name}」？選定便不能更換，3 秒內再按一次確定");
            }
        }

        /// <summary>初心者轉職：5 個職業選一個（10 級才能選；選了不能換，要按兩次確認）；點職業看介紹</summary>
        void DrawJobChange(Rect r, CharacterProfile prof, int fs, int fsS, float gap, float y)
        {
            if (jobInfo != null && Skills.Classes.ContainsKey(jobInfo)) { DrawJobInfo(r, prof, fs, fsS, gap, y); return; }
            bool ok = prof.CanChangeJob, lvOk = prof.level >= Skills.JobLevel;
            string chosen = prof.jobTrialCls;
            string status = "公會長：「" + GuildNoviceLine(prof) + "」" + (lvOk ? "" : $"\n目前 Lv{prof.level}（還差 {Skills.JobLevel - prof.level} 級）");
            GUI.DrawTexture(new Rect(r.x + gap, y, fs * 2.6f, fs * 2.6f), Portraits.Get("公會長", "guild"), ScaleMode.ScaleToFit, true);
            UiKit.Text(new Rect(r.x + gap * 2 + fs * 2.6f, y, r.width - gap * 3 - fs * 2.6f, fs * 2.8f), status, fsS, Color.white, TextAnchor.UpperLeft);
            y += fs * 3f;
            float bottom = r.yMax - gap * 2 - fs * 2.1f;
            // 轉職試煉兩關：選了職業才出題（通過的打勾、下一關可以挑戰）
            if (chosen != null && !ok)
            {
                float th = fs * 2.6f;
                for (int i = 0; i < Skills.JobTrialRounds; i++)
                {
                    bool done = prof.jobTrial > i, next = lvOk && prof.jobTrial == i;
                    var tr = new Rect(r.x + gap, y, r.width - gap * 2, th);
                    var tc = done ? new Color(0.45f, 0.9f, 0.5f) : next ? new Color(1f, 0.6f, 0.3f) : UiKit.Muted;
                    UiKit.Card(tr, next ? 0.65f : 0.4f, tc, next ? 0.9f : 0.5f);
                    float bw2 = tr.width * 0.28f;
                    string nm = i == 0 ? SoloBattle.ClassTrialName(chosen) : JobTrialNames != null && i < JobTrialNames.Length ? JobTrialNames[i] : $"第 {i + 1} 關";
                    UiKit.Text(new Rect(tr.x + gap, tr.y, tr.width - bw2 - gap * 2.5f, th), $"{(done ? "✔ " : "🎓 ")}{nm}", fsS, done || next ? Color.white : Color.gray, TextAnchor.MiddleLeft, false, 0.55f);
                    if (UiKit.Button(new Rect(tr.xMax - bw2 - gap * 0.4f, tr.y + gap * 0.4f, bw2, th - gap * 0.8f), done ? "已通過" : next ? "挑戰" : "先過前一關", fsS, next, tc, next)) { OnJobTrial?.Invoke(i); return; }
                    y += th + gap * 0.5f;
                }
            }
            int n = Skills.ClassOrder.Length;
            float ch = Mathf.Min(fs * 3.6f, (bottom - y) / n - gap * 0.4f);
            for (int i = 0; i < n; i++)
            {
                var id = Skills.ClassOrder[i]; var c = Skills.Classes[id];
                bool mine = chosen == id;
                var cr = new Rect(r.x + gap, y, r.width - gap * 2, ch);
                UiKit.Card(cr, mine ? 0.75f : 0.5f, c.color, mine ? 1f : 0.6f);
                float bw = cr.width * 0.28f;
                var basic = Skills.BasicOf(id);
                float bs = ch * 0.62f;
                var bsr = new Rect(cr.x + gap * 0.5f, cr.y + (ch - bs) / 2f, bs, bs);
                if (!Story2D.DrawFace(bsr, Story2D.ClassPortrait(id, null), c.color) && basic != null) UiKit.Badge(bsr, SkillIcons.Get(basic.id), c.color);
                UiKit.Text(new Rect(cr.x + bs + gap, cr.y, cr.width - bs - bw - gap * 2.5f, ch), $"{c.name}{(mine ? "　✔ 你選的路" : "")}　HP {c.maxHp}・MP {c.maxMp}　ⓘ 點這裡看介紹\n{c.desc}", fsS, Color.white, TextAnchor.MiddleLeft, false, 0.55f);
                if (GUI.Button(new Rect(cr.x, cr.y, cr.width - bw - gap, ch), GUIContent.none, GUIStyle.none)) { jobInfo = id; jobInfoScroll = Vector2.zero; Sfx.Play("select", 0.5f); return; }
                bool armed = jobArm == id && Time.time < jobArmUntil;
                string bt; bool can;
                if (!lvOk) { bt = $"Lv{Skills.JobLevel}"; can = false; }
                else if (ok) { bt = mine ? (armed ? "再按一次\n確定" : $"✨ 成為\n{c.name}") : "不是你選的"; can = mine; }
                else if (prof.jobTrial == 0) { bt = mine ? "挑戰試煉" : "選這條路"; can = true; }
                else { bt = mine ? "繼續試煉" : "重選\n（試煉重來）"; can = true; }
                if (UiKit.Button(new Rect(cr.xMax - bw - gap * 0.4f, cr.y + gap * 0.4f, bw, ch - gap * 0.8f), bt, fsS, armed || (mine && !ok), c.color, can))
                {
                    if (!ok) { OnJobTrialPick?.Invoke(id); return; }
                    if (armed) { jobArm = null; OnJobChange?.Invoke(id); return; }
                    jobArm = id; jobArmUntil = Time.time + 3f;
                    Toast($"成為「{c.name}」？選定便不能更換，3 秒內再按一次確定");
                }
                y += ch + gap * 0.4f;
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), "🏘 回城鎮", fs)) screen = Screen.Town;
        }

        void DrawGuild(Rect r, CharacterProfile prof, int fs, int fsS, int fsL, float gap)
        {
            float y = r.y + gap;
            if (DrawGuild2D(prof, fs, fsS, fsL)) return;   // 2D 職業工會：初心者選職業（WorldUI.Shop2D.cs）
            Header(r, "🏛 職業工會", new Color(0.85f, 0.6f, 1f), prof, fs, fsS, fsL, gap, ref y);
            if (prof.cls == Skills.Novice) { DrawJobChange(r, prof, fs, fsS, gap, y); return; }   // 初心者：選職業（10 級）
            string cn = Skills.Classes.TryGetValue(prof.cls, out var cd) ? cd.name : prof.cls;
            bool lvOk = prof.level >= Advanced.Level;
            var cur = prof.Adv;
            var pick = Advanced.Get(prof.advPick); if (pick != null && pick.baseCls != prof.cls) pick = null;
            string proName = SoloBattle.ClassTrialName(prof.cls);
            string status;
            if (cur != null) status = $"公會長：「{cur.name}，願你的力量替人留燈。到「角色」的「進階」分頁修習新技能吧。」";
            else if (!lvOk) status = $"公會長：「{cn}，先聽清自己的聲音。到了 Lv{Advanced.Level}，再來接受轉職試煉。」\n目前 Lv{prof.level}（還差 {Advanced.Level - prof.level} 級）";
            else if (pick == null) status = "公會長：「三鐘各有聲音，你也該選自己的路了。先選想走的那條，我會給你兩關試煉：先是當年那套專業試煉、更難；再來是海上礁石的實戰。」";
            else if (!prof.advProPassed) status = $"公會長：「{pick.name}嗎？先過{proName}——這次比當年難。失敗了再來找我就好。」";
            else if (!prof.trialPassed) status = $"公會長：「{proName}過了。接下來是實戰：海上礁石的連續波次，不能用藥水，倒下須重來。」";
            else status = $"公會長：「兩關都過了。按下去，你就是{pick.name}了——選定便不能更換。」";
            GUI.DrawTexture(new Rect(r.x+gap,y,fs*2.6f,fs*2.6f),Portraits.Get("公會長","guild"),ScaleMode.ScaleToFit,true);
            UiKit.Text(new Rect(r.x + gap*2+fs*2.6f, y, r.width - gap * 3-fs*2.6f, fs * 2.6f), status, fsS, Color.white, TextAnchor.UpperLeft);
            y += fs * 2.8f;

            // 轉職試煉兩關（選了進階職業才出題）：1 專業試煉（更難）、2 實戰試煉（海上礁石、要報名費）
            if (cur == null && pick != null)
            {
                float th = fs * 3.2f;
                for (int i = 0; i < 2; i++)
                {
                    bool done = i == 0 ? prof.advProPassed : prof.trialPassed, next = lvOk && !done && (i == 0 || prof.advProPassed);
                    var tr = new Rect(r.x + gap, y, r.width - gap * 2, th);
                    var tc = done ? new Color(0.45f, 0.9f, 0.5f) : next ? new Color(1f, 0.55f, 0.3f) : UiKit.Muted;
                    UiKit.Card(tr, next ? 0.6f : 0.4f, tc, next ? 0.9f : 0.5f);
                    string nm = i == 0 ? $"⚔ 進階・{proName}" : "⚔ 實戰試煉", ds = i == 0 ? SoloBattle.ClassTrialDesc(prof.cls, false, true) : TrialText;
                    UiKit.Text(new Rect(tr.x + gap, tr.y, tr.width * 0.64f, tr.height), $"{(done ? "✔ " : "")}{nm}{(done ? "（已通過）" : "")}\n{ds}", Mathf.RoundToInt(fsS * 0.92f), done || next ? Color.white : Color.gray, TextAnchor.MiddleLeft, false, 0.5f);
                    string bt = done ? "已通過" : !lvOk ? $"需要 Lv{Advanced.Level}" : !next ? "先過前一關" : i == 0 ? "挑戰" : $"挑戰\n💰 {Advanced.TrialGold}";
                    if (UiKit.Button(new Rect(tr.xMax - tr.width * 0.34f, tr.y + gap * 0.4f, tr.width * 0.33f - gap * 0.3f, tr.height - gap * 0.8f), bt, fsS, next, tc, next))
                    {
                        if (i == 0) { OnAdvPick?.Invoke(pick.id); return; }
                        if (Economy.Gold < Advanced.TrialGold) Toast($"金幣不夠（需要 {Advanced.TrialGold}）");
                        else { Economy.Gold -= Advanced.TrialGold; OnStartTrial?.Invoke(); return; }
                    }
                    y += th + gap * 0.5f;
                }
            }

            // 兩個進階職業
            var advs = Advanced.Of(prof.cls);
            float listTop = y, listBot = r.yMax - gap * 2 - fs * 2.1f;
            float cardH = fs * 7.6f;
            int maxScroll = Mathf.Max(0, Mathf.CeilToInt((advs.Length * (cardH + gap * 0.5f) - (listBot - listTop)) / (cardH * 0.5f)));
            guildScroll = Mathf.Clamp(guildScroll, 0, maxScroll);
            y -= guildScroll * cardH * 0.5f;
            foreach (var a in advs)
            {
                var cr = new Rect(r.x + gap, y, r.width - gap * 2, cardH);
                y += cardH + gap * 0.5f;
                if (cr.yMax < listTop || cr.y > listBot) continue;
                bool mine = cur != null && cur.id == a.id;
                UiKit.Card(cr, mine ? 0.8f : 0.5f, a.color, mine ? 1f : 0.7f);
                var names = System.Array.ConvertAll(a.skills, id => Skills.All.TryGetValue(id, out var sd) ? sd.name : id);
                string body = $"✨ {a.name}　{a.desc}{(mine ? "（目前職業）" : "")}\n" +
                              $"{StatLine(a)}\n" +
                              $"🔷 {a.core}\n" +
                              $"⚠ {a.weak}\n" +
                              $"📜 新技能：{string.Join("、", names)}";
                float fsz = cr.height * 0.42f;   // 進階職業的 2D 頭像（Codex）
                bool hasFace = Story2D.DrawFace(new Rect(cr.x + gap * 0.6f, cr.y + gap * 0.6f, fsz, fsz), Story2D.ClassPortrait(a.baseCls, a.id), a.color);
                float tx = hasFace ? fsz + gap * 1.2f : 0f;
                UiKit.Text(new Rect(cr.x + gap + tx, cr.y + gap * 0.3f, cr.width * 0.7f - gap - tx, cr.height - gap * 0.6f), body, fsS, Color.white, TextAnchor.UpperLeft, false, 0.5f);
                if (cur == null && lvOk)
                {
                    bool chosen = pick != null && pick.id == a.id;
                    var br = new Rect(cr.xMax - cr.width * 0.28f, cr.y + gap * 0.5f, cr.width * 0.27f - gap * 0.3f, cr.height - gap);
                    if (prof.CanAdvance)
                    {
                        bool confirming = chosen && advPick == a.id && Time.time - advPickAt < 6f;
                        if (UiKit.Button(br, !chosen ? "你選的\n不是這條" : confirming ? $"確定轉職\n成為{a.name}？\n（不能換）" : $"✨ 成為\n{a.name}", fsS, confirming, a.color, chosen))
                        {
                            if (!confirming) { advPick = a.id; advPickAt = Time.time; Toast($"再按一次確定成為{a.name}（選了就不能換）"); }
                            else if (prof.Advance(a.id))
                            {
                                Progress.SaveChar(prof); advPick = null;
                                Toast($"🎉 轉職成功！你成為了{a.name}，學會「{names[0]}」");
                                Sfx.Play("win"); Haptics.Play(Haptics.Kind.Success);
                            }
                        }
                    }
                    else if (UiKit.Button(br, chosen ? (prof.advProPassed ? "✔ 你選的路\n（試煉中）" : "挑戰\n專業試煉") : pick == null ? $"選這條路\n{a.name}" : "重選\n（試煉重來）", fsS, chosen, a.color, !(chosen && prof.advProPassed)))
                    { OnAdvPick?.Invoke(a.id); return; }
                }
            }
            if (maxScroll > 0)
            {
                float bw = fs * 2.2f;
                if (UiKit.Button(new Rect(r.xMax - gap - bw, listTop, bw, bw), "▲", fs, false, null, guildScroll > 0)) guildScroll--;
                if (UiKit.Button(new Rect(r.xMax - gap - bw, listBot - bw, bw, bw), "▼", fs, false, null, guildScroll < maxScroll)) guildScroll++;
            }
            if (UiKit.Button(new Rect(r.x + r.width * 0.25f, r.yMax - gap - fs * 2.1f, r.width * 0.5f, fs * 2.1f), "🏘 回城鎮", fs)) screen = Screen.Town;
        }

        public static string TrialText =>
            $"{RegionNames[Battle.ThemeOf(Advanced.TrialLastWave)].Split(' ')[1]}第 {Advanced.TrialFirstWave}～{Advanced.TrialLastWave} 波（最後一波是 Boss）；敵人 HP ×{Advanced.TrialHpMul:0.#}、攻擊 ×{Advanced.TrialDmgMul:0.#}；不能用藥水與回城捲軸";

        static string StatLine(AdvDef a)
        {
            var parts = new List<string>();
            if (a.hpAdd != 0) parts.Add($"HP {(a.hpAdd > 0 ? "+" : "")}{a.hpAdd}");
            if (a.mpAdd != 0) parts.Add($"MP +{a.mpAdd}");
            if (a.atkMul != 1f) parts.Add($"攻擊 {(a.atkMul > 1f ? "+" : "")}{(a.atkMul - 1f) * 100f:0}%");
            if (a.armor != 0f) parts.Add(a.armor > 0f ? $"受傷 -{a.armor * 100f:0}%" : $"受傷 +{-a.armor * 100f:0}%");
            if (a.crit > 0f) parts.Add($"爆擊 +{a.crit * 100f:0}%");
            if (a.evade > 0f) parts.Add($"閃避 +{a.evade * 100f:0}%");
            if (a.healMul != 1f) parts.Add($"治療 {(a.healMul > 1f ? "+" : "")}{(a.healMul - 1f) * 100f:0}%");
            for (int i = 0; i < StatInfo.Count; i++) if (a.stats[i] != 0) parts.Add($"{StatInfo.Names[i]} +{a.stats[i]}");
            return "📊 " + string.Join("・", parts);
        }
    }
}
