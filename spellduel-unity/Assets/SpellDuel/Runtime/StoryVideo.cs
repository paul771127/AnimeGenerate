using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace SpellDuel
{
    /// <summary>
    /// 劇情影片（MiniMax 生成，960×544、H.264）：不打包進 App，第一次要播時才從 GitHub 下載，存在手機裡之後就不用再下載。
    ///   ・序章：1-1 開場前播整支。
    ///   ・第一～三章的 Boss 關（1-4、2-6、3-6）：開場前播前 4 個鏡頭，通關後播剩下的鏡頭。
    ///   ・「📖 看故事」重看時也會播。
    /// 遊戲一開始（GameRoot.Start）就把四支影片全部下載好（約 31 MB，只有第一次）：畫面顯示「載入中」，不能取消；
    /// 沒有網路下載失敗時才會出現「重試」與「先進遊戲」（之後要播時會再下載）。
    /// 第一次看不能跳過；「📖 看故事」重看時可以長按畫面 1 秒跳過。播放時下方有字幕（每個鏡頭一句，Subs）。
    /// 影片檔放在 repo 的 spellduel-unity/Docs/story_video/videos/（Assets 外面，不會被打包）。
    /// </summary>
    public class StoryVideo : MonoBehaviour
    {
        public const string BaseUrl = "https://raw.githubusercontent.com/paul771127/AnimeGenerate/claude/mobile-camera-mic-battle-game-v5nhfr/spellduel-unity/Docs/story_video/videos/";

        public class Clip
        {
            public string file, title; public int from, to, shots, mb;
            public string Key => $"{file}:{from}-{to}";
        }

        /// <summary>關卡的劇情影片：before＝開場前、否則＝通關後；沒有就回傳 null</summary>
        public static Clip For(string stageId, bool before)
        {
            switch (stageId)
            {
                case "1-1": return before ? new Clip { file = "v0_prologue_v9.mp4", title = "序章：晨鐘啞了", from = 0, to = 8, shots = 8, mb = 8 } : null;
                case "1-4": return Split("v1_golem_v9.mp4", "熔岩巨像・守門人的拳", 4, 6, 7, before);
                case "2-6": return Split("v2_treeking_v9.mp4", "腐化樹王・不肯入睡的心", 4, 5, 6, before);
                case "3-6": return Split("v3_kraken_v9.mp4", "深海巨妖・黎明的三重奏", 4, 8, 10, before);
            }
            return null;
        }
        static Clip Split(string file, string title, int cut, int shots, int mb, bool before) =>
            new Clip { file = file, title = title + (before ? "" : "（結局）"), from = before ? 0 : cut, to = before ? cut : shots, shots = shots, mb = mb };

        /// <summary>每個鏡頭的長度（秒）：生成時固定 125 幀 ÷ 24 fps</summary>
        public const double ShotSec = 5.184;
        /// <summary>準備畫面的 kuso 標題：每 2.5 秒隨機換一句（不說「下載影片」）</summary>
        static readonly string[] KusoLines =
        {
            "準備招待勇者中…", "努力布置場景中…", "老焰正在擦亮晨鐘…", "小鐵在搬鐵砧，請稍等…", "米菈在整理貨箱…",
            "熔岩小鬼在排隊彩排…", "正在幫史萊姆…喔不，沒有史萊姆", "幫 Boss 化妝中…", "替熔岩巨像暖身中…",
            "掃掉村口的落葉中…", "把雲霧吹到正確位置…", "替關卡插好旗子…", "點亮葉燈中…", "幫海鳥練習俯衝…",
            "調整重力中，請勿跳躍…", "替勇者泡一杯熱茶…", "數金幣數到一半…", "說服樹王先別睡…", "幫角鬥場鋪沙中…",
            "替藥水貼標籤中…", "正在背台詞的 NPC：請稍候…", "鐘聲調音中…",
        };
        static int kusoIdx = -1; static float kusoAt = -99f;
        static string KusoLine()
        {
            if (kusoIdx < 0 || Time.unscaledTime - kusoAt > 2.5f)
            {
                int n; do n = UnityEngine.Random.Range(0, KusoLines.Length); while (n == kusoIdx && KusoLines.Length > 1);
                kusoIdx = n; kusoAt = Time.unscaledTime;
            }
            return KusoLines[kusoIdx];
        }

        /// <summary>全部的影片檔（遊戲一開始全部下載）</summary>
        public static readonly (string file, int mb)[] Files = { ("v0_prologue_v9.mp4", 6), ("v1_golem_v9.mp4", 6), ("v2_treeking_v9.mp4", 5), ("v3_kraken_v9.mp4", 8) };

        /// <summary>每句配音在影片裡實際的（開始, 結束）秒數，兩個一組。混音時（v6 起鏡頭會配合台詞拉長，鏡頭不再等長），
        /// 所以字幕和分段播放都照這個時間，不用鏡頭長度推算。由 Docs/story_video/tools/narration_mix.py 算出（narration/timing.json），換配音要一起更新。</summary>
        static readonly System.Collections.Generic.Dictionary<string, float[]> LineTimes = new System.Collections.Generic.Dictionary<string, float[]>
        {
            ["v0_prologue_v9.mp4"] = new[] { 0.12f, 7.72f, 8.19f, 14.07f, 14.54f, 21.91f, 22.38f, 25.48f, 27.54f, 32.66f, 33.13f, 40.00f, 40.47f, 47.24f, 47.71f, 51.57f },
            ["v1_golem_v9.mp4"] = new[] { 0.12f, 6.44f, 6.91f, 16.00f, 16.47f, 19.69f, 21.68f, 25.02f, 26.85f, 34.14f, 34.61f, 40.14f },
            ["v2_treeking_v9.mp4"] = new[] { 0.12f, 5.48f, 5.95f, 13.78f, 14.25f, 19.89f, 20.36f, 25.41f, 25.88f, 31.73f },
            ["v3_kraken_v9.mp4"] = new[] { 0.12f, 7.02f, 7.49f, 11.85f, 12.65f, 15.53f, 17.86f, 23.32f, 23.79f, 29.49f, 29.96f, 36.02f, 36.49f, 41.61f, 42.08f, 47.43f },
        };

        /// <summary>第 i 句開始的秒數（沒有時間表＝照鏡頭長度）</summary>
        static double LineStart(Clip c, int i) => LineTimes.TryGetValue(c.file, out var t) && i * 2 < t.Length ? t[i * 2] : ShotSec * i;
        /// <summary>分段播放的切點：第 i 句的鏡頭開頭（v6 起每句在自己鏡頭開始後約 0.12 秒開口，前一句一定已經講完）</summary>
        static double CutAt(Clip c, int i) =>
            LineTimes.TryGetValue(c.file, out var t) && i > 0 && i * 2 < t.Length ? System.Math.Max(t[i * 2 - 1], t[i * 2] - 0.25) : ShotSec * i;

        /// <summary>字幕：每支影片每句配音一行（出現時間照 LineTimes）。影片的聲音＝旁白與角色各自的配音念這些字幕（v3：老焰是老人男聲、Boss 是怪物聲；播放時背景音樂用遊戲的）；
        /// 旁白的句子不標「旁白：」，角色的台詞前面寫名字。影片檔是 *_v9.mp4（v9：再修 7 句讀音（哑、晶）；v8：重新配音（老焰後製減輕、取消拼音標記）；v7 起結尾定格推近淡出；v6 起畫面配合配音——每個鏡頭放慢／停格到台詞講完，每句都在自己的鏡頭開口；換了聲音，檔名不同才會重新下載）。</summary>
        static readonly System.Collections.Generic.Dictionary<string, string[]> Subs = new System.Collections.Generic.Dictionary<string, string[]>
        {
            ["v0_prologue_v9.mp4"] = new[]
            {
                "灰燼像雪一樣落下。炎之里的晨鐘，已經三天沒有響過。",
                "鐘樓裡，鐘舌斷了一截，鐘身爬滿了黑色的結晶。",
                "老焰：鐘聲該讓出門的人知道，家還在這裡……可它啞了。",
                "一位旅人走上村口的石階——那就是你。",
                "艾琳：我是艾琳。你也聽見了嗎？山裡有東西在敲。",
                "山腹透出紅光。咚、咚、咚——三下，又是三下。",
                "火山、森林、海港……三座鐘，一座接一座熄滅了。",
                "艾琳：走吧，先去礦坑救採礦隊。我們把晨鐘找回來。",
            },
            ["v1_golem_v9.mp4"] = new[]
            {
                "熔岩大廳深處，巨像守在石閘前，胸口的黑晶不停脈動。",
                "熔岩巨像：放過一人……洪潮便會進來……封住……所有的路……",
                "艾琳：牠被困在一次沒結束的守門裡。攔住牠！",
                "巨像高舉雙拳砸向地面——小心衝擊波！",
                "黑晶碎裂，巨像緩緩跪下，熔岩的光漸漸暗去。",
                "艾琳扣回鐘舌。鐘聲響起，金色的聲波掃過整座山。",
            },
            ["v2_treeking_v9.mp4"] = new[]
            {
                "霧中的記名林。古樹上，掛著無數刻了名字的木牌。",
                "腐化樹王：我若睡去……再也沒有人，等他們回來……",
                "艾琳：翠會重刻木牌，小葉會讀名冊。你不必獨自記住。",
                "腐化樹王：別讓……最後一個名字……消失！",
                "黑晶裂開，樹根展平。一顆發光的種子落在名冊上。",
            },
            ["v3_kraken_v9.mp4"] = new[]
            {
                "暴風雨的夜，港口的舊燈塔在雨中掃過一道光。",
                "巨妖攀上礁石。胸前的黑晶上，刻滿了名字。",
                "海斗：凪還在岸上守著燈。我們也不退！",
                "澪：別把活人的名字給牠……聽，歌還沒唱完。",
                "黑晶崩散，巨妖沉回深海。雨，停了。",
                "三座鐘隔著山、林與海，終於一同響起。",
                "白帆化入晨光。港口立起了歸航者的名碑。",
                "艾琳：這次我寫下了所有名字。回家吧，把紀行念給大家聽。",
            },
        };

        enum State { Idle, Preloading, Downloading, Failed, Preparing, Playing }
        static StoryVideo inst;
        State state;
        Clip clip; Action done;
        UnityWebRequest req;
        VideoPlayer vp; RenderTexture rt;
        double endT; float shownAt, tapAt = -9f, holdFrom = -1f;
        bool holdArmed;   // 開始播放後手指要先放開過一次（點進來的那一下不算長按）
        public const float HoldToSkip = 1f;   // 長按幾秒跳過
        string error;
        bool skippable;            // 「📖 看故事」重看：可以長按跳過（第一次看不行）
        int preIdx, preTries;      // 一開始全部下載：第幾支、這支試了幾次
        bool preFailed;            // 一開始全部下載失敗（Failed 畫面的按鈕不同）

        /// <summary>影片相關的畫面開著（下載中、播放中）：其他畫面不要畫、不要收點擊</summary>
        public static bool Active => inst != null && inst.state != State.Idle;
        public static bool Playing => inst != null && (inst.state == State.Playing || inst.state == State.Preparing);

        static string Dir => Path.Combine(Application.persistentDataPath, "story_video");
        static string PathOf(Clip c) => Path.Combine(Dir, c.file);
        public static bool Downloaded(Clip c) { try { return File.Exists(PathOf(c)) && new FileInfo(PathOf(c)).Length > 100_000; } catch { return false; } }

        static bool FileOk(string file) { try { var f = Path.Combine(Dir, file); return File.Exists(f) && new FileInfo(f).Length > 100_000; } catch { return false; } }
        public static bool AllDownloaded => Array.TrueForAll(Files, f => FileOk(f.file));

        /// <summary>遊戲一開始：還沒下載的影片全部下載（畫面顯示「載入中」）</summary>
        public static void PreloadAll()
        {
            // 舊版影片（原本的配樂）：刪掉，省手機空間
            foreach (var old in new[] { "v0_prologue.mp4", "v1_golem.mp4", "v2_treeking.mp4", "v3_kraken.mp4" })
                try { var op = Path.Combine(Dir, old); if (File.Exists(op)) File.Delete(op); } catch { }
            if (AllDownloaded) return;
            if (inst == null) inst = new GameObject("Story Video").AddComponent<StoryVideo>();
            inst.Cleanup();
            inst.preIdx = 0; inst.preTries = 0; inst.preFailed = false; inst.error = null;
            inst.NextPreload();
        }

        void NextPreload()
        {
            while (preIdx < Files.Length && FileOk(Files[preIdx].file)) { preIdx++; preTries = 0; }
            if (preIdx >= Files.Length) { state = State.Idle; return; }
            clip = new Clip { file = Files[preIdx].file, title = "", mb = Files[preIdx].mb };
            StartDownload();
            state = State.Preloading;
        }

        /// <summary>播一段劇情影片，播完（或沒有影片）再呼叫 then；skip＝可以長按跳過（重看）</summary>
        public static void Play(Clip c, Action then, bool skip = false)
        {
            if (c == null) { then?.Invoke(); return; }
            if (inst == null) inst = new GameObject("Story Video").AddComponent<StoryVideo>();
            inst.skippable = skip;
            inst.Begin(c, then);
        }

        /// <summary>直接關掉（回首頁）：不呼叫播完後要做的事</summary>
        public static void Stop()
        {
            if (inst == null || inst.state == State.Idle) return;
            inst.done = null; inst.Finish();
        }

        void Begin(Clip c, Action then)
        {
            Cleanup();
            clip = c; done = then; error = null; preFailed = false;
            if (Downloaded(c)) StartPlayback();
            else StartDownload();   // 沒下載過：直接下載（使用者要求，不再問）
        }

        void Finish()
        {
            var d = done; done = null;
            Cleanup();
            state = State.Idle;
            d?.Invoke();
        }

        void Cleanup()
        {
            if (req != null) { req.Abort(); req.Dispose(); req = null; }
            if (vp) { vp.Stop(); Destroy(vp); vp = null; }
            if (rt) { rt.Release(); Destroy(rt); rt = null; }
        }

        void OnDestroy() { Cleanup(); if (inst == this) inst = null; }

        // ---------------------------------------------------------------- 下載
        void StartDownload()
        {
            try { Directory.CreateDirectory(Dir); } catch { }
            string tmp = PathOf(clip) + ".part";
            req = new UnityWebRequest(BaseUrl + clip.file, UnityWebRequest.kHttpVerbGET) { downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true }, timeout = 180 };
            req.SendWebRequest();
            state = State.Downloading;
        }

        void UpdateDownload()
        {
            if (req == null || !req.isDone) return;
            bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode == 200;
            string tmp = PathOf(clip) + ".part";
            error = ok ? null : (req.responseCode == 404 ? "影片還沒上傳" : $"下載失敗（{req.error}）");
            req.Dispose(); req = null;
            if (ok)
            {
                try { if (File.Exists(PathOf(clip))) File.Delete(PathOf(clip)); File.Move(tmp, PathOf(clip)); }
                catch (Exception e) { ok = false; error = "存檔失敗：" + e.Message; }
            }
            bool pre = state == State.Preloading;
            if (ok && Downloaded(clip)) { if (pre) { preIdx++; preTries = 0; NextPreload(); } else StartPlayback(); return; }
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            error ??= "下載的檔案不完整";
            if (pre && ++preTries < 3) { StartDownload(); state = State.Preloading; return; }   // 一開始的下載：自動重試 2 次
            preFailed = pre; state = State.Failed;
        }

        // ---------------------------------------------------------------- 播放
        void StartPlayback()
        {
            rt = new RenderTexture(960, 544, 0);
            holdArmed = false; holdFrom = -1f;
            vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            vp.url = PathOf(clip);
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.isLooping = false;
            vp.errorReceived += (p, msg) => { error = "播放失敗：" + msg; state = State.Failed; };
            vp.prepareCompleted += p =>
            {
                double len = p.length > 0 ? p.length : ShotSec * clip.shots;
                double startT = clip.from > 0 ? CutAt(clip, clip.from) : 0;   // 照配音實際時間切（不是鏡頭長度），台詞不會被切掉或重播
                endT = clip.to >= clip.shots ? len + 1 : CutAt(clip, clip.to);
                p.time = startT;
                p.SetDirectAudioVolume(0, 1f);
                p.Play();
                state = State.Playing; shownAt = Time.unscaledTime;
            };
            vp.loopPointReached += p => { if (state == State.Playing) Finish(); };
            vp.Prepare();
            state = State.Preparing;
        }

        void Update()
        {
            if (state == State.Downloading || state == State.Preloading) UpdateDownload();
            else if (state == State.Playing && vp && vp.time >= endT) Finish();
            // 長按畫面跳過（只有重看可以；觸控或滑鼠按住）
            if (skippable && (state == State.Playing || state == State.Preparing))
            {
                bool down = Input.touchCount > 0 || Input.GetMouseButton(0);
                if (!down) { holdFrom = -1f; holdArmed = true; }
                else if (!holdArmed) { }
                else if (holdFrom < 0f) holdFrom = Time.unscaledTime;
                else if (Time.unscaledTime - holdFrom >= HoldToSkip) { holdFrom = -1f; Finish(); }
            }
            else holdFrom = -1f;
        }

        // ---------------------------------------------------------------- 畫面（SoloBattle.DrawGUI 最上層呼叫）
        public static void DrawGUI()
        {
            if (inst == null || inst.state == State.Idle) return;
            inst.Draw();
        }

        static int TotalMb { get { int n = 0; foreach (var f in Files) n += f.mb; return n; } }

        /// <summary>現在播到的鏡頭的字幕（沒有＝null）</summary>
        string Subtitle()
        {
            if (vp == null || clip == null || !Subs.TryGetValue(clip.file, out var lines)) return null;
            // 字幕跟著配音：顯示「已經開始講」的最後一句（提早 0.15 秒出現），句子之間的空檔繼續顯示上一句
            double now = vp.time + 0.15; int shot = -1;
            for (int i = 0; i < lines.Length; i++) if (LineStart(clip, i) <= now) shot = i;
            return shot >= 0 && shot < lines.Length && shot >= clip.from && shot < clip.to ? lines[shot].Replace("{玩家}", "你") : null;
        }

        void Draw()
        {
            float W = Screen.width, H = Screen.height, pad = W * 0.04f;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.8f);
            GUI.color = Color.black; GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            float bh = fs * 2.4f, bw = W - pad * 2;
            if (state == State.Playing || state == State.Preparing)
            {
                if (rt && state == State.Playing)
                {
                    // 保持 960:544 的比例（直拿手機：上下留黑邊）
                    float aspect = 960f / 544f, w = W, h = W / aspect;
                    if (h > H) { h = H; w = H * aspect; }
                    GUI.DrawTexture(new Rect((W - w) / 2f, (H - h) / 2f, w, h), rt, ScaleMode.StretchToFill, false);
                }
                else UiKit.Text(new Rect(0, H * 0.45f, W, fs * 2f), "🎬 載入中…", fs, Color.white, TextAnchor.MiddleCenter);
                UiKit.Text(new Rect(pad, H * 0.08f, bw, fs * 1.6f), "🎬 " + clip.title, fsS, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft, true);
                float by = H - pad - bh - Screen.safeArea.y;
                // 字幕：畫面下方（影片下面的黑邊），現在這個鏡頭的那一句
                string sub = state == State.Playing ? Subtitle() : null;
                if (sub != null)
                {
                    float sh = fs * 3.2f, sy = by - sh - pad * 0.5f;
                    GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(pad * 0.5f, sy, W - pad, sh), Texture2D.whiteTexture); GUI.color = Color.white;
                    UiKit.Text(new Rect(pad, sy, bw, sh), sub, fs, Color.white, TextAnchor.MiddleCenter, true);
                }
                if (!skippable) return;
                // 重看：長按跳過（按住時下方出現進度條；點一下或開頭 3 秒顯示提示）
                if (Event.current.type == EventType.MouseDown) tapAt = Time.unscaledTime;
                float hold = holdFrom >= 0f ? Mathf.Clamp01((Time.unscaledTime - holdFrom) / HoldToSkip) : 0f;
                if (hold > 0.08f)
                    UiKit.Bar(new Rect(pad, by, bw, bh * 0.6f), hold, UiKit.Gold, "繼續按住跳過…", fsS);
                else if (Time.unscaledTime - shownAt < 3f || Time.unscaledTime - tapAt < 3f)
                    UiKit.Text(new Rect(pad, by, bw, bh), "長按畫面 1 秒跳過", fsS, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
                return;
            }
            var r = new Rect(pad, H * 0.3f, bw, H * 0.4f);
            UiKit.Card(r, 0.95f, UiKit.Gold, 0.9f);
            float y = r.y + pad * 0.6f, x = r.x + pad * 0.6f, w2 = r.width - pad * 1.2f;
            bool pre = state == State.Preloading || preFailed;
            UiKit.Text(new Rect(x, y, w2, fs * 1.6f), state == State.Failed ? "⏳ 載入中…" : "⏳ " + KusoLine(), Mathf.RoundToInt(fs * 1.2f), Color.white, TextAnchor.MiddleCenter, true);
            y += fs * 2.2f;
            if (state == State.Downloading || state == State.Preloading)
            {
                float p = req != null ? req.downloadProgress : 0f;
                string what = pre ? $"準備中 {preIdx + 1}/{Files.Length}" : "準備中";
                UiKit.Bar(new Rect(x, y, w2, fs * 1.2f), p, UiKit.Gold, $"{what}　{p * 100f:0}%", fsS);
                y += fs * 2f;
                UiKit.Text(new Rect(x, y, w2, fs * 2.4f), pre ? "第一次開遊戲要多準備一下（之後就不用了），建議連 Wi-Fi。" : "馬上就好（只有第一次）", fsS, new Color(0.85f, 0.88f, 1f), TextAnchor.UpperCenter);
            }
            else if (state == State.Failed)
            {
                // 下載失敗（多半是沒有網路）：重試；真的不行可以先繼續（之後要播時會再下載）
                UiKit.Text(new Rect(x, y, w2, fs * 2.4f), "⚠ " + error + "\n請確認網路後重試", fsS, new Color(1f, 0.6f, 0.5f), TextAnchor.UpperCenter);
                y += fs * 2.8f;
                float hw = (w2 - pad * 0.4f) / 2f;
                if (UiKit.Button(new Rect(x, y, hw, bh), "重試", fs, true, UiKit.Gold)) { Cleanup(); if (preFailed) { preFailed = false; preTries = 0; NextPreload(); } else { StartDownload(); } }
                if (UiKit.Button(new Rect(x + hw + pad * 0.4f, y, hw, bh), preFailed ? "先進遊戲" : "先繼續", fsS)) { if (preFailed) { preFailed = false; state = State.Idle; } else Finish(); }
            }
        }
    }
}
