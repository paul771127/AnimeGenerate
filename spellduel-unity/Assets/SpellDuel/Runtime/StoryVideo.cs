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
    ///   ・第一～三章的 Boss 關（1-4、2-4、3-4）：開場前播前 4 個鏡頭，通關後播剩下的鏡頭。
    ///   ・「📖 看故事」重看時也會播。
    /// 還沒下載時直接下載（約 6～10 MB，不再詢問；下載中可以取消跳過）；播放時長按畫面 1 秒跳過（點一下也會出現「跳過」按鈕）。
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
                case "1-1": return before ? new Clip { file = "v0_prologue.mp4", title = "序章：晨鐘啞了", from = 0, to = 8, shots = 8, mb = 8 } : null;
                case "1-4": return Split("v1_golem.mp4", "熔岩巨像・守門人的拳", 4, 6, 7, before);
                case "2-4": return Split("v2_treeking.mp4", "腐化樹王・不肯入睡的心", 4, 5, 6, before);
                case "3-4": return Split("v3_kraken.mp4", "深海巨妖・黎明的三重奏", 4, 8, 10, before);
            }
            return null;
        }
        static Clip Split(string file, string title, int cut, int shots, int mb, bool before) =>
            new Clip { file = file, title = title + (before ? "" : "（結局）"), from = before ? 0 : cut, to = before ? cut : shots, shots = shots, mb = mb };

        enum State { Idle, Downloading, Failed, Preparing, Playing }
        static StoryVideo inst;
        State state;
        Clip clip; Action done;
        UnityWebRequest req;
        VideoPlayer vp; RenderTexture rt;
        double endT; float shownAt, tapAt = -9f, holdFrom = -1f;
        bool holdArmed;   // 開始播放後手指要先放開過一次（點進來的那一下不算長按）
        public const float HoldToSkip = 1f;   // 長按幾秒跳過
        string error;

        /// <summary>影片相關的畫面開著（下載中、播放中）：其他畫面不要畫、不要收點擊</summary>
        public static bool Active => inst != null && inst.state != State.Idle;
        public static bool Playing => inst != null && (inst.state == State.Playing || inst.state == State.Preparing);

        static string Dir => Path.Combine(Application.persistentDataPath, "story_video");
        static string PathOf(Clip c) => Path.Combine(Dir, c.file);
        public static bool Downloaded(Clip c) { try { return File.Exists(PathOf(c)) && new FileInfo(PathOf(c)).Length > 100_000; } catch { return false; } }

        /// <summary>播一段劇情影片，播完（或跳過、沒有影片）再呼叫 then</summary>
        public static void Play(Clip c, Action then)
        {
            if (c == null) { then?.Invoke(); return; }
            if (inst == null) inst = new GameObject("Story Video").AddComponent<StoryVideo>();
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
            clip = c; done = then; error = null;
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
            req = new UnityWebRequest(BaseUrl + clip.file, UnityWebRequest.kHttpVerbGET) { downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true }, timeout = 120 };
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
            if (ok && Downloaded(clip)) StartPlayback();
            else { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } state = State.Failed; error ??= "下載的檔案不完整"; }
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
                double len = p.length > 0 ? p.length : 5.17 * clip.shots;
                double startT = len * clip.from / Mathf.Max(1, clip.shots);
                endT = clip.to >= clip.shots ? len + 1 : len * clip.to / Mathf.Max(1, clip.shots);
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
            if (state == State.Downloading) UpdateDownload();
            else if (state == State.Playing && vp && vp.time >= endT) Finish();
            // 長按畫面跳過（觸控或滑鼠按住；載入中也可以）
            if (state == State.Playing || state == State.Preparing)
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
                // 長按跳過：按住時下方出現進度條；點一下或開頭 3 秒顯示提示與「跳過」按鈕
                if (Event.current.type == EventType.MouseDown) tapAt = Time.unscaledTime;
                float hold = holdFrom >= 0f ? Mathf.Clamp01((Time.unscaledTime - holdFrom) / HoldToSkip) : 0f;
                float by = H - pad - bh - Screen.safeArea.y;
                if (hold > 0.08f)
                    UiKit.Bar(new Rect(pad, by, bw, bh * 0.6f), hold, UiKit.Gold, "繼續按住跳過…", fsS);
                else if (Time.unscaledTime - shownAt < 3f || Time.unscaledTime - tapAt < 3f)
                {
                    UiKit.Text(new Rect(pad, by, bw - W * 0.32f, bh), "長按畫面 1 秒跳過", fsS, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleLeft);
                    if (UiKit.Button(new Rect(W - pad - W * 0.3f, by, W * 0.3f, bh), "跳過 ▶▶", fsS)) Finish();
                }
                return;
            }
            var r = new Rect(pad, H * 0.3f, bw, H * 0.4f);
            UiKit.Card(r, 0.95f, UiKit.Gold, 0.9f);
            float y = r.y + pad * 0.6f, x = r.x + pad * 0.6f, w2 = r.width - pad * 1.2f;
            UiKit.Text(new Rect(x, y, w2, fs * 1.6f), "🎬 劇情影片：" + clip.title, fs, Color.white, TextAnchor.MiddleLeft, true);
            y += fs * 2f;
            if (state == State.Downloading)
            {
                float p = req != null ? req.downloadProgress : 0f;
                UiKit.Bar(new Rect(x, y, w2, fs * 1.2f), p, UiKit.Gold, $"下載中… {p * 100f:0}%（約 {clip.mb} MB，只有第一次）", fsS);
                y += fs * 2f;
                if (UiKit.Button(new Rect(x, y, w2, bh * 0.85f), "取消（跳過影片）", fsS)) Finish();
            }
            else if (state == State.Failed)
            {
                UiKit.Text(new Rect(x, y, w2, fs * 2.4f), "⚠ " + error, fsS, new Color(1f, 0.6f, 0.5f));
                y += fs * 2.8f;
                float hw = (w2 - pad * 0.4f) / 2f;
                if (UiKit.Button(new Rect(x, y, hw, bh), "重試", fs, true, UiKit.Gold)) { Cleanup(); StartDownload(); }
                if (UiKit.Button(new Rect(x + hw + pad * 0.4f, y, hw, bh), "跳過", fs)) Finish();
            }
        }
    }
}
