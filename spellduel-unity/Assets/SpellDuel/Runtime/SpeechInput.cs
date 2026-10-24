using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 喊技能名稱放招：用手機內建的語音辨識（連續聆聽、回報中途結果），聽到的文字交給 SpeechMatcher 比對。
    ///   Android：android.speech.SpeechRecognizer（SpeechBridge.java）
    ///   iOS：Apple Speech 框架 SFSpeechRecognizer（SpeechBridge.mm，支援時在手機上辨識、不需要網路）
    /// 同一句話只觸發一次（第一個夠像的中途結果就觸發，之後等這句話結束才會再觸發）。
    /// 編輯器與其他平台：Supported = false。
    /// </summary>
    public class SpeechInput
    {
        public bool Supported { get; private set; }
        public bool Running { get; private set; }
        public string Status { get; private set; } = "未啟動";
        public string LastHeard => tracker.LastHeard;

        /// <summary>目前可以喊的技能 id（例如裝備中的 3 個技能）</summary>
        public IList<string> Candidates
        {
            get => candidates;
            set { candidates = value; RefreshNames(); }
        }

        /// <summary>聽到某個技能的名稱（參數為技能 id）；每句話最多一次</summary>
        public event Action<string> OnSkill;

        /// <summary>每段辨識文字（中途結果與最後結果都會送；城鎮裡用來聽招呼語）</summary>
        public event Action<string> OnText;

        /// <summary>額外告訴辨識器的詞（例如城鎮的招呼語），提高辨識正確率</summary>
        public IList<string> ExtraHints
        {
            get => extraHints;
            set { extraHints = value; if (Running) { try { PushHints(); } catch (Exception) { } } }
        }
        IList<string> extraHints;

        IList<string> candidates;
        readonly List<string> ids = new List<string>(), names = new List<string>();
        readonly SpeechTracker tracker = new SpeechTracker();
        readonly List<string> alts = new List<string>();
        bool permissionPending;
        float nextPermissionCheck;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject bridge;
#endif
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int sd_speech_available();
        [DllImport("__Internal")] static extern void sd_speech_start();
        [DllImport("__Internal")] static extern void sd_speech_stop();
        [DllImport("__Internal")] static extern void sd_speech_set_hints(byte[] utf8);
        [DllImport("__Internal")] static extern int sd_speech_poll(byte[] buf, int len);
        [DllImport("__Internal")] static extern int sd_speech_status(byte[] buf, int len);
        readonly byte[] pollBuf = new byte[8192], statusBuf = new byte[512];
#endif

        public SpeechInput()
        {
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                bridge = new AndroidJavaObject("com.paul771127.spellduel.SpeechBridge");
                Supported = bridge.Call<bool>("isAvailable");
                if (!Supported) Status = "這支手機沒有語音辨識服務";
#elif UNITY_IOS && !UNITY_EDITOR
                Supported = sd_speech_available() != 0;
                if (!Supported) Status = "這支手機的語音辨識不支援中文";
#else
                Supported = false;
                Status = "這個平台沒有語音辨識";
#endif
            }
            catch (Exception e) { Supported = false; Status = "語音辨識啟動失敗：" + e.Message; }
        }

        /// <summary>
        /// 開始聆聽。Android 第一次會先跳出麥克風權限要求並回傳 false；
        /// 之後在 Tick 裡偵測到使用者允許了就會自動開始。
        /// </summary>
        public bool Start()
        {
            if (!Supported) return false;
            if (Running) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                permissionPending = true;
                Status = "請允許麥克風權限";
                return false;
            }
#endif
            permissionPending = false;
            tracker.Reset();
            try
            {
                PushHints();
#if UNITY_ANDROID && !UNITY_EDITOR
                bridge.Call("start");
#elif UNITY_IOS && !UNITY_EDITOR
                sd_speech_start();
#endif
            }
            catch (Exception e) { Status = "語音辨識啟動失敗：" + e.Message; return false; }
            Running = true;
            Status = "聆聽中";
            return true;
        }

        public void Stop()
        {
            permissionPending = false;
            if (!Running) return;
            Running = false;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                bridge.Call("stop");
#elif UNITY_IOS && !UNITY_EDITOR
                sd_speech_stop();
#endif
            }
            catch (Exception e) { Status = "語音辨識停止失敗：" + e.Message; return; }
            Status = "已停止";
        }

        /// <summary>每幀呼叫：取回新的辨識文字並比對技能名稱</summary>
        public void Tick()
        {
            if (!Supported) return;
            float now = Time.realtimeSinceStartup;
            if (permissionPending && !Running && now >= nextPermissionCheck)
            {
                nextPermissionCheck = now + 0.5f;
#if UNITY_ANDROID && !UNITY_EDITOR
                if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone)) Start();
#endif
            }
            if (!Running) return;

            string[] lines = null;
            string native = null;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                lines = bridge.Call<string[]>("poll");
                native = bridge.Call<string>("getError");
#elif UNITY_IOS && !UNITY_EDITOR
                int n = sd_speech_poll(pollBuf, pollBuf.Length);
                if (n > 0) lines = Encoding.UTF8.GetString(pollBuf, 0, n).Split('\n');
                int m = sd_speech_status(statusBuf, statusBuf.Length);
                native = m > 0 ? Encoding.UTF8.GetString(statusBuf, 0, m) : "";
#endif
            }
            catch (Exception e) { Status = "語音辨識失敗：" + e.Message; return; }
            if (native != null) Status = native.Length > 0 ? native : "聆聽中";

            if (lines != null) Feed(lines, now);
            tracker.Update(now);
        }

        /// <summary>處理原生端傳來的多行 "P:…"／"F:…"（連續的 F: 是同一句話的不同候選）</summary>
        void Feed(string[] lines, float now)
        {
            alts.Clear();
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l == null || l.Length < 3 || l[1] != ':') continue;
                if (l[0] == 'P')
                {
                    FlushFinal(now);
                    alts.Add(l.Substring(2));
                    OnText?.Invoke(alts[0]);
                    Fire(tracker.Feed(false, alts, names, now, chantFrom));
                    alts.Clear();
                }
                else if (l[0] == 'F') alts.Add(l.Substring(2));
            }
            FlushFinal(now);
        }

        void FlushFinal(float now)
        {
            if (alts.Count == 0) return;
            OnText?.Invoke(alts[0]);
            Fire(tracker.Feed(true, alts, names, now, chantFrom));
            alts.Clear();
        }

        void Fire(int index)
        {
            FireOne(index);
            FireOne(tracker.TakeQueued());
        }

        void FireOne(int index)
        {
            if (index < 0 || index >= ids.Count) return;
            OnSkill?.Invoke(ids[index]);
        }

        /// <summary>咒語在 OnSkill 回傳的 id 前綴（"chant:fire"）</summary>
        public const string ChantPrefix = "chant:";
        int chantFrom = int.MaxValue;   // names 中從這個索引開始是咒語

        void RefreshNames()
        {
            ids.Clear(); names.Clear();
            if (candidates != null)
                foreach (var id in candidates)
                    if (id != null && !ids.Contains(id) && Skills.All.TryGetValue(id, out var s) && !string.IsNullOrEmpty(s.name))
                    { ids.Add(id); names.Add(s.name); }
            // 法師的咒語：唸完技能名稱後（同一句話裡也可以）接著唸
            chantFrom = names.Count;
            if (candidates != null)
                foreach (var id in candidates)
                    if (id != null && Skills.All.TryGetValue(id, out var s) && !string.IsNullOrEmpty(s.chant) && !ids.Contains(ChantPrefix + id))
                    { ids.Add(ChantPrefix + id); names.Add(s.chant); }
            if (Running) { try { PushHints(); } catch (Exception) { } }
        }

        // 把技能名稱告訴辨識器（iOS contextualStrings／Android 13+ biasing），提高辨識正確率
        void PushHints()
        {
            var all = new List<string>(names);
            if (extraHints != null) all.AddRange(extraHints);
            string joined = string.Join("\n", all);
#if UNITY_ANDROID && !UNITY_EDITOR
            bridge.Call("setHints", joined);
#elif UNITY_IOS && !UNITY_EDITOR
            var b = Encoding.UTF8.GetBytes(joined + "\0");
            sd_speech_set_hints(b);
#endif
        }
    }

    /// <summary>
    /// 斷句與防重複（純 C#，可在 Unity 外測試）：
    ///   同一句話的中途結果會一直變長，第一個夠像的就觸發，之後鎖住，
    ///   直到收到最後結果（F:）或約 1.2 秒沒有新文字才解鎖；解鎖後只比對「新增加的部分」，
    ///   避免 iOS 一個任務累積多句話時把已經觸發過的技能名稱再觸發一次。
    /// </summary>
    public class SpeechTracker
    {
        public float QuietReset = 1.2f;
        public string LastHeard { get; private set; } = "";

        bool locked;
        int consumed;
        int chantPos;            // 咒語從這個位置之後找（技能名稱結尾）
        int queued = -1;         // 同一段文字裡技能名稱後面就接著咒語：下一次取出

        /// <summary>取出排隊中的觸發（同一段文字裡技能名稱＋咒語）</summary>
        public int TakeQueued() { int q = queued; queued = -1; return q; }            // 正規化後的文字中，前面這麼多字已經處理過
        string last = "";        // 上一個（正規化後的）假設
        float lastChange;
        readonly List<string> regions = new List<string>();

        public void Reset() { locked = false; consumed = 0; chantPos = 0; queued = -1; last = ""; LastHeard = ""; }

        /// <summary>沒有新文字超過 QuietReset 秒：這句話結束</summary>
        public void Update(float now)
        {
            if (locked && now - lastChange > QuietReset)
            {
                locked = false;
                consumed = last.Length;
            }
        }

        /// <summary>names[hit] 是別的名稱的開頭，而且句尾剛好停在那個較長名稱的前半段（還可能接下去）</summary>
        static bool MayContinue(string t0, IList<string> names, int hit)
        {
            string n = SpeechMatcher.Normalize(names[hit]);
            for (int i = 0; i < names.Count; i++)
            {
                if (i == hit) continue;
                string l = SpeechMatcher.Normalize(names[i]);
                if (l.Length <= n.Length || !l.StartsWith(n, StringComparison.Ordinal)) continue;
                for (int k = n.Length; k < l.Length; k++)
                    if (t0.EndsWith(l.Substring(0, k), StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>咒語（names[lockedFrom..]）只比對 chantPos 之後的文字</summary>
        int MatchChant(IList<string> alts, IList<string> names, int lockedFrom, string t0)
        {
            if (lockedFrom >= names.Count || chantPos >= t0.Length) return -1;
            regions.Clear();
            foreach (var a in alts)
            {
                string n = SpeechMatcher.Normalize(a);
                int skip = Math.Min(chantPos, SpeechMatcher.CommonPrefix(n, t0));
                regions.Add(n.Substring(Math.Min(skip, n.Length)));
            }
            var sub = new List<string>();
            for (int i = lockedFrom; i < names.Count; i++) sub.Add(names[i]);
            int h = SpeechMatcher.Best(regions, sub, out var score, out _, true);
            if (h < 0 || score < SpeechMatcher.ChantMinScore) return -1;
            chantPos = t0.Length;
            return lockedFrom + h;
        }

        /// <summary>送進一個假設（final＝最後結果，alts 為候選文字）；觸發時回傳 names 的索引，否則 -1</summary>
        /// <param name="lockedFrom">names 中從這個索引開始的名稱（咒語）在鎖住時仍可比對「新增加的部分」：唸完技能名稱馬上接咒語</param>
        public int Feed(bool final, IList<string> alts, IList<string> names, float now, int lockedFrom = int.MaxValue)
        {
            if (alts == null || alts.Count == 0) return -1;
            bool afterQuiet = now - lastChange > QuietReset;   // 上一句話已經講完
            Update(now);
            string t0 = SpeechMatcher.Normalize(alts[0]);
            if (!string.IsNullOrEmpty(alts[0])) LastHeard = alts[0].Trim();
            string prev = last;
            if (!final && afterQuiet && !locked && (t0 == prev || !t0.StartsWith(prev)))
            {
                // 安靜一陣子後，辨識器開了新的一句（原生端同一句裡不會重複送一樣的文字）：
                // 就算文字和上一句一模一樣（再唸一次同一個技能），也當成新的一句從頭比對
                prev = ""; consumed = 0;
            }
            else if (!final && t0 == last) return -1;   // 沒有新內容
            consumed = Math.Min(consumed, SpeechMatcher.CommonPrefix(t0, prev));
            last = t0;
            lastChange = now;

            int hit = -1;
            if (!locked && names != null && names.Count > 0)
            {
                regions.Clear();
                foreach (var a in alts)
                {
                    string n = SpeechMatcher.Normalize(a);
                    int skip = Math.Min(consumed, SpeechMatcher.CommonPrefix(n, prev));
                    regions.Add(n.Substring(Math.Min(skip, n.Length)));
                }
                if (lockedFrom >= names.Count) hit = SpeechMatcher.Best(regions, names, out _, out _, true);
                else
                {
                    // 技能名稱和咒語分開比（同一段文字可能兩個都有，不算互相競爭）：先找技能名稱，沒有再找咒語
                    var skillNames = new List<string>(); var chants = new List<string>();
                    for (int i = 0; i < names.Count; i++) (i < lockedFrom ? skillNames : chants).Add(names[i]);
                    hit = SpeechMatcher.Best(regions, skillNames, out _, out _, true);
                    if (hit < 0) { hit = SpeechMatcher.Best(regions, chants, out var cs, out _, true); if (hit >= 0 && cs < SpeechMatcher.ChantMinScore) hit = -1; if (hit >= 0) hit += lockedFrom; }
                }
                // 名稱是另一個名稱的開頭（刺殺／刺殺準備）：話還沒講完、句尾可能正要接下去 → 先等
                if (hit >= 0 && !final && MayContinue(t0, names, hit)) return -1;
                if (hit >= 0)
                {
                    locked = true;
                    // 技能名稱在這段文字的哪裡結束：後面的字留給咒語
                    int skip0 = Math.Min(consumed, SpeechMatcher.CommonPrefix(t0, prev));
                    chantPos = skip0 + SpeechMatcher.MatchEnd(t0.Substring(Math.Min(skip0, t0.Length)), SpeechMatcher.Normalize(names[hit]));
                    consumed = t0.Length;
                    if (hit < lockedFrom) queued = MatchChant(alts, names, lockedFrom, t0);
                }
            }
            else if (locked && names != null && lockedFrom < names.Count)
                hit = MatchChant(alts, names, lockedFrom, t0);   // 已經觸發過技能名稱：同一句話後面接的咒語
            if (final) { locked = false; consumed = 0; chantPos = 0; last = ""; }
            return hit;
        }
    }

    /// <summary>
    /// 技能名稱比對（純 C#，可在 Unity 外測試）。
    ///   1. 正規化：去掉空白標點、全形轉半形、簡體轉繁體（只轉技能名稱用到的字）。
    ///   2. 每個字查拼音（不含聲調），同音字算很像；z/zh、c/ch、s/sh、n/l、f/h、en/eng、in/ing、an/ang 算有點像。
    ///   3. 近似子字串比對（編輯距離）：名稱可以出現在一句話裡面（「我要火球術」）。
    ///   4. 最高分要夠高、而且明顯比第二名高，才算數。
    /// </summary>
    public static class SpeechMatcher
    {
        public const float MinScoreShort = 0.8f;   // 兩個字的名稱：門檻比較高（容易誤判）
        public const float MinScore = 0.72f;
        public const float MinMargin = 0.08f;
        public const float ChantMinScore = 0.86f;   // 咒語要幾乎唸完整（長咒語不能只唸一半就放；同音字仍可）
        const float InsCost = 0.6f;    // 名稱中間多出一個字
        const float DelCost = 0.75f;   // 名稱少了一個字

        // 簡體 → 繁體（技能名稱用到的字與常見異體字）
        const string SimpTrad =
            "击擊连連兽獸夹夾术術枪槍鎗槍风風陨隕愈癒瘉癒袭襲飞飛烟煙菸煙雾霧弹彈斩斬剑劍劒劍剣劍气氣挡擋铁鐵疗療猎獵" +
            "圣聖审審护護祷禱净淨广廣杀殺腾騰冻凍结結坠墜静靜灵靈恶惡恒恆环環电電闪閃极極讽諷点點" +
            "鹰鷹领領将將应應现現罚罰记記听聽躯軀冲衝贯貫标標滚滾从從号號凛凜为為约約卫衛弃棄处處决決复復体體惩懲" +
            "时時头頭实實识識来來万萬归歸总總开開";

        // 拼音（不含聲調）→ 字：技能名稱的每個字，加上辨識器可能輸出的同音字（繁簡都有）
        const string PinyinTable =
            "su 速素訴诉宿塑蘇苏俗粟肅肃|she 射社設设舍涉攝摄蛇舌奢赦|ju 狙居局句據据聚具劇剧舉举菊拘鞠巨拒距鋸锯駒驹|" +
            "ji 擊击機机雞鸡基積积極极即及吉急級级集幾几己計计記记紀纪技季濟济際际績绩跡迹姬激擠挤寄寂籍輯辑疾脊肌飢饥|" +
            "san 三散傘伞參叁叄3|lian 連连聯联臉脸練练戀恋憐怜蓮莲廉簾帘煉炼鏈链鍊|" +
            "shi 矢石是時时事使試试師师十實实識识世市式室史始示士施失食詩诗濕湿拾視视氏勢势適适釋释飾饰屍尸獅狮逝誓蝕蚀|" +
            "bu 捕不步部布補补簿埠怖卜哺佈|shou 獸兽手受首收守壽寿授售瘦|jia 夾夹家加假價价架甲嫁佳嘉駕驾挾挟頰颊莢荚枷|" +
            "bao 爆報报包寶宝保抱暴飽饱堡豹胞鮑鲍雹|lie 裂列烈獵猎劣咧冽|" +
            "xian 陷先現现線线限縣县顯显鮮鲜險险獻献仙閒闲嫌憲宪賢贤鹹咸弦掀纖纤羨羡餡馅腺|" +
            "jing 阱經经精景警靜静井境鏡镜敬京驚惊競竞淨净頸颈晶睛鯨鲸徑径莖茎荊荆|huo 火或活貨货獲获禍祸夥伙惑霍豁|" +
            "qiu 球求秋丘邱囚酋裘糗鰍鳅|shu 術术數数書书樹树屬属輸输熟鼠舒叔束述蔬梳殊薯署曙恕淑疏豎竖暑蜀|" +
            "bing 冰病兵並并餅饼柄丙秉稟禀|qiang 槍枪鎗強强牆墙搶抢腔嗆呛薔蔷羌|lei 雷類类累淚泪蕾擂肋壘垒磊鐳镭|" +
            "feng 風风封峰鋒锋豐丰瘋疯蜂楓枫縫缝逢奉鳳凤諷讽|ren 刃人任認认仁忍韌韧紉纫妊|yun 隕陨運运雲云允孕韻韵暈晕勻匀蘊蕴耘殞殒|" +
            "zhi 治之知只直指至制製智值職职紙纸志支止致質质植執执置旨織织枝肢脂侄秩滯滞稚汁芝隻|" +
            "yu 癒愈瘉與与於于語语魚鱼雨玉預预遇域欲育獄狱餘余宇羽浴御寓裕郁鬱娛娱愚愉漁渔譽誉|" +
            "bei 背被北備备杯悲輩辈貝贝倍碑卑狽狈焙|ci 刺次詞词此辭辞慈磁雌瓷賜赐疵祠|" +
            "ying 影應应英營营迎贏赢硬映鷹鹰嬰婴櫻樱盈穎颖蠅蝇螢萤熒荧|xi 襲袭西系係洗喜細细息希習习席戲戏吸析夕惜溪稀錫锡膝熙嬉犧牺悉昔隙晰媳|" +
            "du 毒度讀读都獨独督堵賭赌杜渡肚鍍镀妒篤笃|fei 飛飞非費费肥廢废肺菲啡匪誹诽妃沸吠|dao 刀到道倒導导島岛盜盗稻蹈悼叨搗捣禱祷|" +
            "yan 煙烟菸眼言研演嚴严驗验鹽盐顏颜延沿炎宴燕岩掩厭厌焰豔艳硯砚雁咽|wu 霧雾無无五物務务誤误午武舞屋烏乌吳吴悟伍侮污汙勿戊晤5|" +
            "dan 彈弹但單单蛋擔担淡丹膽胆誕诞旦氮耽|zhan 斬斩站戰战展佔占沾盞盏綻绽嶄崭棧栈湛瞻|tu 突圖图土吐途徒塗涂兔凸禿秃屠|" +
            "jian 劍剑劒剣見见間间件建健簡简減减檢检尖堅坚箭漸渐鍵键艦舰監监兼肩煎剪薦荐賤贱|" +
            "qi 氣气起其期七奇器騎骑汽齊齐棋旗妻企啟启戚欺漆歧祈豈岂泣契砌7|ge 格個个歌哥各割革隔閣阁鴿鸽戈葛擱搁胳|" +
            "dang 擋挡當当黨党蕩荡檔档盪|tie 鐵铁貼贴帖|bi 壁比必筆笔閉闭幣币避鼻碧臂畢毕逼彼弊蔽斃毙璧|" +
            "fan 反飯饭翻番凡犯範范煩烦繁返泛販贩帆|liao 療疗了料聊遼辽|" +
            // 口音（捲舌／前後鼻音不分）時辨識器可能輸出的近音字
            "zan 贊赞讚咱暫暂|bin 賓宾彬濱滨斌鬢|se 色瑟澀涩塞|shan 山閃闪善扇衫杉珊|sang 桑嗓喪丧|nian 年念黏|" +
            "liang 量兩两亮涼凉糧粮梁輛辆|si 四死思絲丝私司斯寺似4|sou 搜嗖艘|nie 捏聶聂鎳镍|xiang 想向相像香鄉乡象響响箱祥項项|" +
            "jin 金今進进近緊紧斤盡尽禁筋錦锦|qian 前錢钱千簽签淺浅牽牵遷迁欠|nei 內内|fen 分份粉紛纷奮奋憤愤墳坟芬|" +
            "hong 紅红洪轟轰宏虹哄|reng 仍扔|zi 自字子資资姿紫滋仔|chi 吃持遲迟池尺齒齿赤翅斥|yin 因音引銀银印飲饮隱隐陰阴|" +
            "hui 會会回灰揮挥輝辉毀毁|yang 樣样陽阳洋羊養养揚扬仰氧|jiang 將将講讲江獎奖降姜醬酱疆|fang 方放房防訪访芳仿紡纺|" +
            "huan 換换環环歡欢緩缓還还幻患喚唤|" +
            // 牧師：聖光彈、審判、治療術、淨化、守護、祈禱
            "sheng 聖圣生聲声升勝胜省盛剩繩绳牲|guang 光廣广逛|shen 審审神身深伸申甚慎腎肾紳绅|pan 判盼盤盘攀潘叛畔|" +
            "hua 化花話话畫画華华滑劃划|kang 抗康扛炕慷亢|fa 法發发罰罚乏伐閥阀筏髮|hu 護护戶户湖虎呼胡壺壶糊互狐乎|sha 殺杀沙紗纱砂傻鯊鲨煞啥|ming 冥明名命鳴鸣銘铭|zhong 中重種种眾众鐘钟終终忠腫肿|zhun 準准|" +
            // 法師咒語（Skills.cs 的 chant）用到的字
            "ben 奔本笨苯|teng 騰腾疼藤|ba 吧把爸巴拔霸罷罢八8|dong 凍冻動动東东懂洞冬|jie 結结節节解接街姐界借介屆届潔洁|han 寒漢汉喊含韓韩旱汗|" +
            "kuang 狂況况礦矿框筐曠旷|zhui 墜坠追綴缀|luo 落羅罗洛絡络駱骆螺|xing 星行形型醒性興兴姓幸刑|chen 辰沉陳陈晨塵尘臣稱称趁|" +
            "tui 退推腿頹颓|xie 邪些寫写謝谢鞋協协斜血|e 惡恶餓饿額额鵝鹅|xin 心新信辛欣鋅锌|ling 靈灵領领零令鈴铃齡龄0|pi 劈皮批披匹疲脾|ting 霆聽听停庭挺廷亭|" +
            "yong 永用勇泳擁拥湧涌|heng 恆恒橫横衡哼|tian 天田甜填添|shang 上傷伤商賞赏尚|de 的得德地|a 啊阿|zuo 作做坐座左昨|" +
            "di 地第低底弟帝敵敌滴|dian 電电點点店典顛颠|nu 怒努奴|" +
            "chao 嘲朝超潮炒吵巢抄|si 撕|ming 命|sheng 生|yi 一1|er 二2|liu 六6|jiu 九9|" +
            // 進階職業的技能名稱與咒語（Skills.cs 的 adv 技能）
            "dun 盾遁頓顿噸吨蹲敦燉炖鈍钝|chong 衝冲充崇蟲虫寵宠|cai 裁才材財财採采彩菜猜踩|xuan 旋選选宣玄懸悬炫|" +
            "qu 軀躯去取區区曲趣驅驱渠屈|guan 貫贯關关觀观管官館馆冠罐慣惯|chuan 穿川傳传船串喘|biao 標标表錶飆飙彪|gun 滾滚棍|" +
            "cong 從从聰聪匆蔥葱叢丛|wu 吾|hao 號号好豪毫浩耗|xia 下夏嚇吓瞎峽峡狹狭霞|lin 凜凛林臨临鄰邻淋琳磷|da 大打達达答搭|" +
            "wei 為为衛卫位未味圍围偉伟維维微威尾胃|mo 魔模莫末摸磨默墨膜陌|li 力立利理里離离例歷历麗丽禮礼李|yue 約约月越樂乐閱阅躍跃岳|" +
            "yi 以意已易衣醫医億亿義义異异議议依移憶忆藝艺益翼|yuan 元原園园員员遠远願愿源圓圆院援緣缘|po 破坡婆迫潑泼|qi 棄弃|" +
            "ti 替體体提題题梯踢|chu 處处出初除楚觸触儲储廚厨|jue 決决覺觉絕绝角爵掘|fu 復复福服父付府富負负婦妇浮扶副傅附覆幅腹|" +
            "qun 群裙|bi 庇|cheng 懲惩成城程承誠诚乘稱称橙|jie 戒|zeng 增曾贈赠憎|" +
            // 初心者：木棍揮擊、投石、急救（投石常被聽成「同時」）
            "tou 投頭头偷透|tong 同通童痛統统銅铜桶筒|mu 木目母幕慕墓牧|jiu 救就九久舊旧究酒揪|" +
            // 劍仙：劍來、御劍術、萬劍歸宗
            "lai 來来賴赖萊莱|wan 萬万完玩晚碗彎弯灣湾|gui 歸归鬼貴贵櫃柜規规龜龟|zong 宗總总縱纵蹤踪棕粽|gang 罡剛刚鋼钢港崗岗綱纲缸|ti 體|kai 開开凱凯慨楷揩";

        static readonly Dictionary<char, char> toTrad = new Dictionary<char, char>();
        static readonly Dictionary<char, List<string>> pinyin = new Dictionary<char, List<string>>();
        static readonly string[] Initials = { "zh", "ch", "sh", "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h", "j", "q", "x", "r", "z", "c", "s", "y", "w" };

        static SpeechMatcher()
        {
            for (int i = 0; i + 1 < SimpTrad.Length; i += 2)
                if (SimpTrad[i] != SimpTrad[i + 1]) toTrad[SimpTrad[i]] = SimpTrad[i + 1];
            foreach (var group in PinyinTable.Split('|'))
            {
                int sp = group.IndexOf(' ');
                if (sp <= 0) continue;
                string py = group.Substring(0, sp);
                for (int i = sp + 1; i < group.Length; i++)
                {
                    char c = group[i];
                    if (!pinyin.TryGetValue(c, out var list)) pinyin[c] = list = new List<string>(1);
                    if (!list.Contains(py)) list.Add(py);
                }
            }
        }

        /// <summary>辨識器常見的誤聽 → 技能名稱（正規化後直接替換）。例：「刺殺」常被聽成「自殺」</summary>
        static readonly string[,] Misheard =
        {
            { "同時", "投石" }, { "同石", "投石" }, { "頭石", "投石" }, { "投時", "投石" }, { "偷時", "投石" }, { "通識", "投石" }, { "投實", "投石" }, { "痛死", "投石" },
            { "自殺", "刺殺" }, { "次殺", "刺殺" }, { "此殺", "刺殺" }, { "四殺", "刺殺" }, { "賜殺", "刺殺" }, { "刺沙", "刺殺" }, { "自沙", "刺殺" },
            // 「三連矢」常被聽成「三點10」「三連10」之類（數字）
            { "三點10", "三連矢" }, { "3點10", "三連矢" }, { "三點十", "三連矢" }, { "3點十", "三連矢" }, { "三點時", "三連矢" }, { "三點是", "三連矢" },
            { "三連10", "三連矢" }, { "3連10", "三連矢" }, { "三連十", "三連矢" }, { "3連十", "三連矢" }, { "三年10", "三連矢" }, { "三年十", "三連矢" },
            { "三連是", "三連矢" }, { "三連時", "三連矢" }, { "三連使", "三連矢" }, { "三連事", "三連矢" }, { "310", "三連矢" },
            // 「風刃」常被聽成「風箏」「烹飪」之類（簡體的「风」正規化後已是「風」）
            { "風箏", "風刃" }, { "風筝", "風刃" }, { "烹飪", "風刃" }, { "烹饪", "風刃" }, { "豐潤", "風刃" }, { "豐润", "風刃" }, { "丰润", "風刃" },
            { "鋒刃", "風刃" }, { "锋刃", "風刃" }, { "封刃", "風刃" }, { "風人", "風刃" }, { "風任", "風刃" }, { "風認", "風刃" }, { "風韌", "風刃" }, { "風忍", "風刃" },
            { "瘋人", "風刃" }, { "疯人", "風刃" }, { "楓刃", "風刃" }, { "蜂刃", "風刃" }, { "風潤", "風刃" }, { "風润", "風刃" },
        };

        /// <summary>去掉空白與標點、全形轉半形、簡體轉繁體、英文轉小寫</summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                char c = ch;
                if (c >= '！' && c <= '～') c = (char)(c - 0xFEE0);   // 全形 → 半形
                if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c)) continue;
                if (toTrad.TryGetValue(c, out var t)) c = t;
                sb.Append(char.ToLowerInvariant(c));
            }
            var r = sb.ToString();
            for (int i = 0; i < Misheard.GetLength(0); i++) if (r.Contains(Misheard[i, 0])) r = r.Replace(Misheard[i, 0], Misheard[i, 1]);
            return r;
        }

        public static int CommonPrefix(string a, string b)
        {
            int n = Math.Min(a?.Length ?? 0, b?.Length ?? 0), i = 0;
            while (i < n && a[i] == b[i]) i++;
            return i;
        }

        static void Split(string py, out string ini, out string fin)
        {
            foreach (var p in Initials)
                if (py.StartsWith(p, StringComparison.Ordinal)) { ini = p; fin = py.Substring(p.Length); return; }
            ini = ""; fin = py;
        }

        static bool Pair(string a, string b, string x, string y) => (a == x && b == y) || (a == y && b == x);

        static float InitialSim(string a, string b)
        {
            if (a == b) return 1f;
            if (Pair(a, b, "z", "zh") || Pair(a, b, "c", "ch") || Pair(a, b, "s", "sh") || Pair(a, b, "n", "l") ||
                Pair(a, b, "f", "h") || Pair(a, b, "l", "r")) return 0.8f;
            // 送氣／不送氣（z／c、zh／ch、j／q、d／t、b／p、g／k）：手機辨識常搞混，給一點分
            if (Pair(a, b, "z", "c") || Pair(a, b, "zh", "ch") || Pair(a, b, "j", "q") || Pair(a, b, "d", "t") || Pair(a, b, "b", "p") || Pair(a, b, "g", "k") || Pair(a, b, "z", "ch") || Pair(a, b, "zh", "c")) return 0.6f;
            return 0f;
        }

        static float FinalSim(string a, string b)
        {
            if (a == b) return 1f;
            if (Pair(a, b, "an", "ang") || Pair(a, b, "en", "eng") || Pair(a, b, "in", "ing") || Pair(a, b, "ian", "iang") ||
                Pair(a, b, "uan", "uang") || Pair(a, b, "o", "uo") || Pair(a, b, "e", "o")) return 0.8f;
            return 0f;
        }

        /// <summary>兩個拼音音節的相似度 0～1</summary>
        public static float SyllableSim(string a, string b)
        {
            if (a == b) return 1f;
            Split(a, out var ia, out var fa);
            Split(b, out var ib, out var fb);
            return 0.5f * InitialSim(ia, ib) + 0.5f * FinalSim(fa, fb);
        }

        /// <summary>兩個（已正規化的）字的相似度：同字 1、同音 0.9、近音較低、查不到拼音 0</summary>
        public static float CharSim(char a, char b)
        {
            if (a == b) return 1f;
            if (!pinyin.TryGetValue(a, out var pa) || !pinyin.TryGetValue(b, out var pb)) return 0f;
            float best = 0f;
            foreach (var x in pa)
                foreach (var y in pb)
                {
                    float s = x == y ? 0.9f : SyllableSim(x, y) * 0.85f;
                    if (s > best) best = s;
                }
            return best;
        }

        /// <summary>名稱出現在文字中（允許在句子裡、允許錯字）的分數 0～1；兩者都要先 Normalize</summary>
        public static float Score(string text, string name)
        {
            int m = name.Length, n = text.Length;
            if (m == 0 || n == 0) return 0f;
            // 近似子字串比對：文字前後多出來的字不扣分
            var prev = new float[n + 1];
            var cur = new float[n + 1];
            for (int i = 1; i <= m; i++)
            {
                cur[0] = i * DelCost;
                for (int j = 1; j <= n; j++)
                {
                    float sub = prev[j - 1] + (1f - CharSim(name[i - 1], text[j - 1]));
                    float del = prev[j] + DelCost;
                    float ins = cur[j - 1] + InsCost;
                    cur[j] = Math.Min(sub, Math.Min(del, ins));
                }
                var tmp = prev; prev = cur; cur = tmp;
            }
            float best = float.MaxValue;
            for (int j = 0; j <= n; j++) best = Math.Min(best, prev[j]);
            return Math.Max(0f, 1f - best / m);
        }

        /// <summary>名稱在文字中最像的位置的結尾（字數）；找不到回傳 0</summary>
        public static int MatchEnd(string text, string name)
        {
            int m = name.Length, n = text.Length;
            if (m == 0 || n == 0) return 0;
            var prev = new float[n + 1];
            var cur = new float[n + 1];
            for (int i = 1; i <= m; i++)
            {
                cur[0] = i * DelCost;
                for (int j = 1; j <= n; j++)
                {
                    float sub = prev[j - 1] + (1f - CharSim(name[i - 1], text[j - 1]));
                    cur[j] = Math.Min(sub, Math.Min(prev[j] + DelCost, cur[j - 1] + InsCost));
                }
                var tmp = prev; prev = cur; cur = tmp;
            }
            int bj = 0; float best = float.MaxValue;
            for (int j = 0; j <= n; j++) if (prev[j] < best) { best = prev[j]; bj = j; }
            return bj;
        }

        public static float Threshold(string normalizedName) => normalizedName.Length <= 2 ? MinScoreShort : MinScore;

        /// <summary>
        /// 從多個候選文字（同一句話的不同辨識結果）中找最符合的名稱。
        /// 回傳 names 的索引；不夠像或分不清（第二名太接近）時回傳 -1。
        /// </summary>
        public static int Best(IList<string> hypotheses, IList<string> names, out float bestScore, out float secondScore, bool normalized = false)
        {
            bestScore = 0f; secondScore = 0f;
            if (hypotheses == null || names == null || names.Count == 0) return -1;
            int bestIdx = -1;
            float bestRel = float.MinValue;
            var normNames = new string[names.Count];
            for (int k = 0; k < names.Count; k++) normNames[k] = Normalize(names[k]);
            // 每個名稱取所有候選文字中的最高分；以「超過門檻多少」排名，讓長短名稱公平比較
            var rel = new float[names.Count];
            var raw = new float[names.Count];
            for (int k = 0; k < names.Count; k++)
            {
                float s = 0f;
                foreach (var h in hypotheses)
                {
                    string t = normalized ? h : Normalize(h);
                    if (string.IsNullOrEmpty(t)) continue;
                    s = Math.Max(s, Score(t, normNames[k]));
                }
                raw[k] = s;
                rel[k] = s - Threshold(normNames[k]);
                if (rel[k] > bestRel) { bestRel = rel[k]; bestIdx = k; }
            }
            if (bestIdx < 0) return -1;
            float secondRel = float.MinValue;
            for (int k = 0; k < names.Count; k++)
                // 被最佳名稱包含的較短名稱（刺殺準備裡的刺殺）不算競爭者
                if (k != bestIdx && normNames[k] != normNames[bestIdx] && !normNames[bestIdx].Contains(normNames[k]) && rel[k] > secondRel) { secondRel = rel[k]; secondScore = raw[k]; }
            bestScore = raw[bestIdx];
            if (bestRel < 0f) return -1;
            if (secondRel > float.MinValue && bestRel - secondRel < MinMargin) return -1;
            return bestIdx;
        }

        /// <summary>直接用技能 id 比對（名稱取自 Skills.All）；回傳技能 id 或 null</summary>
        public static string Match(IList<string> hypotheses, IList<string> skillIds)
        {
            if (skillIds == null) return null;
            var ids = new List<string>();
            var names = new List<string>();
            foreach (var id in skillIds)
                if (id != null && Skills.All.TryGetValue(id, out var s)) { ids.Add(id); names.Add(s.name); }
            int k = Best(hypotheses, names, out _, out _);
            return k >= 0 ? ids[k] : null;
        }
    }
}
