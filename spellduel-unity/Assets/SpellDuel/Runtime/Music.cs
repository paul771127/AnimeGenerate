using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 背景音樂：全部在程式裡合成（不需要音樂檔）。每首是 16 小節（A、B 兩段）、可以無縫循環的曲子：
    ///   town（城鎮／選單）、ch0～ch4（五章）、explore（故事探索）、dg_＜副本 id＞（每個副本一首）；加「_boss」是同一首的 Boss 版。
    ///   每首的調式、速度、樂器（笛、鐘、撥弦、鋸齒波）、鼓的節奏型都不一樣（使用者要求：每個大關卡與副本配不同的配樂）。
    /// 第一次要播時才在背景執行緒算波形（不卡畫面），算好後淡入；換曲時兩首交叉淡入淡出。
    /// 開關與音量在設定選單（PlayerPrefs 記住）。
    /// </summary>
    public class Music : MonoBehaviour
    {
        public static Music I { get; private set; }
        /// <summary>壓低音量（播劇情影片時讓旁白聽得清楚）：1＝正常</summary>
        public static float Duck = 1f;
        static float volume = 0.45f; static bool on = true, loaded;
        static void Load() { if (loaded) return; loaded = true; on = PlayerPrefs.GetInt("SD_MusicOn", 1) == 1; volume = PlayerPrefs.GetFloat("SD_MusicVol", 0.45f); }
        public static bool On { get { Load(); return on; } }
        public static float Volume { get { Load(); return volume; } }
        public static void SetOn(bool v) { Load(); on = v; PlayerPrefs.SetInt("SD_MusicOn", v ? 1 : 0); PlayerPrefs.Save(); }
        public static void SetVolume(float v) { Load(); volume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("SD_MusicVol", volume); PlayerPrefs.Save(); }

        const int SR = 22050;
        AudioSource a, b;          // 交叉淡入用的兩個音源（a＝現在這首）
        string want, playing;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, Task<float[]>> jobs = new Dictionary<string, Task<float[]>>();
        readonly List<string> used = new List<string>();   // 建好的順序（舊的在前）
        const int MaxClips = 5;

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("Music");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Music>();
        }

        /// <summary>想要播哪一首（null＝安靜）；每幀呼叫也沒關係，換曲才會有動作</summary>
        public static void Want(string track)
        {
            if (I == null) Ensure();
            I.want = track;
        }

        void Awake()
        {
            I = this;
            a = gameObject.AddComponent<AudioSource>(); b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b }) { s.loop = true; s.playOnAwake = false; s.spatialBlend = 0f; s.volume = 0f; }
        }

        void Update()
        {
            string target = On ? want : null;
            if (target != null && !clips.ContainsKey(target))
            {
                // 還沒算好：背景執行緒算，好了再建 AudioClip（AudioClip 只能在主執行緒建）
                if (!jobs.TryGetValue(target, out var job)) { string id = target; jobs[target] = Task.Run(() => Compose(id)); }
                else if (job.IsCompleted)
                {
                    jobs.Remove(target);
                    if (job.Status == TaskStatus.RanToCompletion)
                    {
                        var data = job.Result;
                        var clip = AudioClip.Create("music_" + target, data.Length, 1, SR, false);
                        clip.SetData(data, 0);
                        clips[target] = clip; used.Remove(target); used.Add(target);
                        // 一首約 2MB：最多留 MaxClips 首（最久沒用的、而且沒在播的先丟）
                        for (int i = 0; i < used.Count && clips.Count > MaxClips; i++)
                        {
                            string old = used[i];
                            if (old == target || old == playing || a.clip == clips[old] || b.clip == clips[old]) continue;
                            if (clips[old]) Destroy(clips[old]);
                            clips.Remove(old); used.RemoveAt(i); i--;
                        }
                    }
                    else clips[target] = null;   // 合成失敗：這首就不播
                }
            }
            if (target != playing && (target == null || clips.ContainsKey(target)))
            {
                // 換曲：舊的移到 b 淡出，新的在 a 從頭淡入
                var t = a; a = b; b = t;
                playing = target;
                if (target != null && clips[target] != null) { a.clip = clips[target]; a.volume = 0f; a.Play(); }
                else a.Stop();
            }
            float dt = Time.unscaledDeltaTime;
            a.volume = Mathf.MoveTowards(a.volume, playing != null ? Volume * Duck : 0f, dt * 0.6f);
            b.volume = Mathf.MoveTowards(b.volume, 0f, dt * 0.8f);
            if (b.isPlaying && b.volume <= 0f) b.Stop();
        }

        // ================================================================ 作曲＋合成（純數學，可在背景執行緒跑）
        class Style
        {
            public float bpm; public int root;           // root：MIDI 音高（主音）
            public int[] scale;                           // 音階（半音）
            public int[][] chords;                        // 8 小節的和弦（音階級數，0 起算）；B 段另外用 chordsB（null＝A 段往上移）
            public int[][] chordsB;
            public float drums, bass, arp, pad, lead;     // 各聲部音量
            public int arpRate = 4;                       // 一拍幾個琶音
            public bool driving;                          // 大鼓每拍都打（快歌）
            public Wave leadWave = Wave.Square;           // 旋律的樂器
            public Wave arpWave = Wave.Pluck;
            public int groove;                            // 鼓：0 一般、1 每拍大鼓、2 太鼓（切分＋低鼓）、3 半拍感（慢）、4 行進小鼓、5 機關（十六分的滴答）
            public float swing;                           // 搖擺（0～0.3）：反拍往後拖
            public int seed;
        }

        static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 }, Major = { 0, 2, 4, 5, 7, 9, 11 }, Dorian = { 0, 2, 3, 5, 7, 9, 10 },
            Phrygian = { 0, 1, 3, 5, 7, 8, 10 }, HarmMinor = { 0, 2, 3, 5, 7, 8, 11 }, Lydian = { 0, 2, 4, 6, 7, 9, 11 }, Mixolydian = { 0, 2, 4, 5, 7, 9, 10 },
            PentaMinor = { 0, 3, 5, 7, 10 }, PentaMajor = { 0, 2, 4, 7, 9 };
        static int[][] Ch(params int[] d) { var r = new int[d.Length][]; for (int i = 0; i < d.Length; i++) r[i] = new[] { d[i] }; return r; }

        /// <summary>
        /// 每一章、每一個副本、故事探索各一首（使用者要求）；「＿boss」＝同一首的 Boss 版（更快、更重、大鼓每拍）。
        /// 舊的 id（battle0～2、boss）還能用。
        /// </summary>
        static Style StyleOf(string id)
        {
            bool boss = id.EndsWith("_boss");
            string b = boss ? id.Substring(0, id.Length - 5) : id;
            Style st = b switch
            {
                // 城鎮：慢、大調，I–V–vi–IV
                "town" => new Style { bpm = 92, root = 60, scale = Major, chords = Ch(0, 4, 5, 3, 0, 4, 3, 4), chordsB = Ch(5, 3, 0, 4, 5, 3, 1, 4), drums = 0.25f, bass = 0.5f, arp = 0.35f, pad = 0.6f, lead = 0.45f, arpRate = 2, leadWave = Wave.Flute },
                // ---- 五章
                "ch0" or "battle0" => new Style { bpm = 140, root = 50, scale = Minor, chords = Ch(0, 5, 2, 6, 0, 5, 6, 4), drums = 0.9f, bass = 0.75f, arp = 0.35f, pad = 0.3f, lead = 0.55f, driving = true },   // 熔岩：D 小調，重鼓
                "ch1" or "battle1" => new Style { bpm = 124, root = 52, scale = Dorian, chords = Ch(0, 3, 0, 6, 0, 3, 4, 6), drums = 0.65f, bass = 0.6f, arp = 0.5f, pad = 0.35f, lead = 0.5f, leadWave = Wave.Flute, swing = 0.12f },   // 森林：E 多利亞，笛子
                "ch2" or "battle2" => new Style { bpm = 130, root = 57, scale = Minor, chords = Ch(0, 6, 5, 4, 0, 6, 5, 6), drums = 0.7f, bass = 0.65f, arp = 0.45f, pad = 0.45f, lead = 0.5f, arpRate = 3 },   // 海港：A 小調，三連音的浪
                "ch3" => new Style { bpm = 132, root = 48, scale = HarmMinor, chords = Ch(0, 0, 5, 4, 0, 3, 4, 4), drums = 0.95f, bass = 0.7f, arp = 0.3f, pad = 0.3f, lead = 0.55f, leadWave = Wave.Saw, groove = 4 },   // 角鬥場：C 和聲小調，行進小鼓
                "ch4" => new Style { bpm = 112, root = 45, scale = Phrygian, chords = Ch(0, 1, 0, 6, 0, 1, 5, 1), drums = 0.8f, bass = 0.7f, arp = 0.4f, pad = 0.35f, lead = 0.5f, leadWave = Wave.Flute, groove = 2, swing = 0.18f },   // 峽谷：A 弗里吉亞，太鼓、沙漠
                "explore" => new Style { bpm = 104, root = 55, scale = PentaMinor, chords = Ch(0, 3, 2, 4, 0, 3, 1, 4), drums = 0.4f, bass = 0.5f, arp = 0.45f, pad = 0.55f, lead = 0.4f, leadWave = Wave.Flute, arpWave = Wave.Bell, arpRate = 2, groove = 3 },   // 翠蔭迷林：G 五聲，探險
                // ---- 副本
                "dg_ash_mine" => new Style { bpm = 128, root = 47, scale = Minor, chords = Ch(0, 0, 5, 6, 0, 3, 4, 4), drums = 0.9f, bass = 0.8f, arp = 0.3f, pad = 0.3f, lead = 0.5f, arpWave = Wave.Bell, groove = 4 },   // 灰燼礦坑：敲打的礦坑行進
                "dg_rot_temple" => new Style { bpm = 92, root = 50, scale = Phrygian, chords = Ch(0, 1, 0, 6, 5, 1, 0, 1), drums = 0.55f, bass = 0.6f, arp = 0.3f, pad = 0.7f, lead = 0.45f, leadWave = Wave.Bell, arpRate = 2, groove = 3 },   // 腐根神殿：陰森、慢
                "dg_sunken_throne" => new Style { bpm = 104, root = 53, scale = Dorian, chords = Ch(0, 6, 3, 0, 0, 6, 4, 3), drums = 0.45f, bass = 0.55f, arp = 0.5f, pad = 0.6f, lead = 0.45f, leadWave = Wave.Flute, arpRate = 3, groove = 3 },   // 沉沒王座：水底
                "dg_dawn_gate" => new Style { bpm = 136, root = 55, scale = Major, chords = Ch(0, 4, 5, 3, 0, 4, 3, 4), chordsB = Ch(3, 4, 5, 5, 3, 4, 1, 4), drums = 0.85f, bass = 0.7f, arp = 0.4f, pad = 0.45f, lead = 0.55f, leadWave = Wave.Saw, groove = 4 },   // 黎明之門：英雄、大調
                "dg_ember_bell_foundry" => new Style { bpm = 120, root = 49, scale = HarmMinor, chords = Ch(0, 5, 3, 4, 0, 5, 1, 4), drums = 0.8f, bass = 0.7f, arp = 0.45f, pad = 0.35f, lead = 0.55f, leadWave = Wave.Bell, arpWave = Wave.Bell, groove = 2 },   // 餘燼鑄鐘坊：鐘聲
                "dg_mist_name_grove" => new Style { bpm = 98, root = 52, scale = PentaMinor, chords = Ch(0, 2, 3, 1, 0, 2, 4, 3), drums = 0.4f, bass = 0.5f, arp = 0.5f, pad = 0.6f, lead = 0.45f, leadWave = Wave.Flute, arpRate = 2, groove = 3, swing = 0.1f },   // 霧燈記名徑：霧裡的笛聲
                "dg_white_sail_echo_reef" => new Style { bpm = 134, root = 57, scale = Mixolydian, chords = Ch(0, 6, 3, 0, 0, 6, 4, 4), drums = 0.75f, bass = 0.65f, arp = 0.5f, pad = 0.35f, lead = 0.5f, arpRate = 3, swing = 0.2f },   // 白帆回聲礁：航海的搖擺
                "dg_thousand_trap_gallery" => new Style { bpm = 146, root = 50, scale = HarmMinor, chords = Ch(0, 4, 0, 5, 3, 4, 0, 4), drums = 0.8f, bass = 0.75f, arp = 0.55f, pad = 0.25f, lead = 0.45f, arpRate = 4, groove = 5 },   // 千機迴廊：齒輪滴答
                "dg_sky_carpet" => new Style { bpm = 152, root = 53, scale = Lydian, chords = Ch(0, 1, 0, 4, 0, 1, 5, 4), drums = 0.85f, bass = 0.7f, arp = 0.5f, pad = 0.4f, lead = 0.55f, leadWave = Wave.Saw, driving = true },   // 天穹航路：飛翔、明亮
                "dg_other" => new Style { bpm = 126, root = 51, scale = Dorian, chords = Ch(0, 3, 6, 4, 0, 3, 4, 6), drums = 0.75f, bass = 0.65f, arp = 0.45f, pad = 0.4f, lead = 0.5f, groove = 2 },
                // Boss（舊 id）：C 小調，快、重
                _ => new Style { bpm = 152, root = 48, scale = Minor, chords = Ch(0, 0, 5, 4, 0, 1, 5, 4), drums = 1f, bass = 0.85f, arp = 0.4f, pad = 0.35f, lead = 0.5f, driving = true },
            };
            st.seed = id.Length * 7919;
            foreach (char c in id) st.seed = st.seed * 31 + c;   // 不用 GetHashCode（每次執行可能不一樣）
            if (boss)
            {
                st.bpm += 16f; st.driving = true; st.drums = Math.Min(1f, st.drums + 0.25f); st.bass = Math.Min(1f, st.bass + 0.1f);
                if (st.groove == 3) st.groove = 0;
                st.leadWave = st.leadWave == Wave.Flute ? Wave.Saw : st.leadWave; st.swing *= 0.5f;
            }
            return st;
        }

        /// <summary>這一關的曲子 id：副本用副本 id、故事探索用 explore、其他照章節（0～4）</summary>
        public static string TrackFor(string dungeonId, bool explore, int theme, bool boss)
        {
            string id = !string.IsNullOrEmpty(dungeonId) ? (KnownDungeon(dungeonId) ? "dg_" + dungeonId : "dg_other") : explore ? "explore" : "ch" + Math.Max(0, Math.Min(4, theme));
            return boss ? id + "_boss" : id;
        }
        static bool KnownDungeon(string id) => id == "ash_mine" || id == "rot_temple" || id == "sunken_throne" || id == "dawn_gate" || id == "ember_bell_foundry"
            || id == "mist_name_grove" || id == "white_sail_echo_reef" || id == "thousand_trap_gallery" || id == "sky_carpet";

        static float[] Compose(string id)
        {
            var st = StyleOf(id);
            var rnd = new System.Random(st.seed & 0x7fffffff);
            float beat = 60f / st.bpm, bar = beat * 4f;
            const int Bars = 16;   // A 段 8 小節＋B 段 8 小節
            int n = (int)(bar * Bars * SR);
            var buf = new float[n];
            float Hz(int midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);
            int Deg(int degree, int octave) { int len = st.scale.Length; int o = (int)Math.Floor(degree / (float)len); int k = degree - o * len; return st.root + st.scale[k] + 12 * (o + octave); }
            float Sw(float t, float step, int i) => t + (i % 2 == 1 ? step * st.swing : 0f);   // 反拍往後拖
            // 旋律動機：A 段一組、B 段另一組，每兩小節重複一次（聽起來像一首歌，不是亂彈）
            int[][] motifs = new int[2][];
            float[][] rhythms = new float[2][];
            for (int s = 0; s < 2; s++)
            {
                var notes = new List<int>(); var lens = new List<float>(); float tot = 0f;
                while (tot < 8f - 0.01f)   // 兩小節＝8 拍
                {
                    float len = rnd.NextDouble() < 0.45 ? 1f : rnd.NextDouble() < 0.6 ? 0.5f : rnd.NextDouble() < 0.7 ? 2f : 1.5f;
                    len = Math.Min(len, 8f - tot);
                    notes.Add(rnd.NextDouble() < 0.15 ? int.MinValue : rnd.Next(-2, 6)); lens.Add(len); tot += len;
                }
                motifs[s] = notes.ToArray(); rhythms[s] = lens.ToArray();
            }

            for (int barI = 0; barI < Bars; barI++)
            {
                bool sectB = barI >= 8; int bi = barI % 8;
                float t0 = barI * bar;
                int c = sectB ? (st.chordsB != null ? st.chordsB[bi][0] : st.chords[bi][0] + (bi % 2 == 0 ? 3 : 0)) : st.chords[bi][0];
                if (barI == Bars - 1) c = st.chords[7][0];   // 最後一小節接回開頭
                int[] triad = { Deg(c, 0), Deg(c + 2, 0), Deg(c + 4, 0) };
                // 鋪底和弦（高八度、柔和）
                foreach (var m in triad) Note(buf, t0, bar, Hz(m + 12), Wave.Pad, st.pad * 0.12f);
                // 低音：八分音符（快歌）或四分
                for (int i = 0; i < (st.driving ? 8 : 4); i++)
                {
                    float step = st.driving ? beat / 2f : beat;
                    int m = Deg(c, -1) - 12 + (i % 4 == 3 && !st.driving ? 7 : 0);
                    Note(buf, Sw(t0 + i * step, step, i), step * 0.9f, Hz(m), Wave.Saw, st.bass * 0.22f);
                }
                // 琶音：A 段上行、B 段上下來回且高八度
                int per = st.arpRate * 4;
                for (int i = 0; i < per; i++)
                {
                    int idx = sectB ? new[] { 0, 1, 2, 1 }[i % 4] : (i % 4 == 3) ? 1 : i % 3;
                    int m = triad[idx] + 12 + (i % 8 >= 4 || sectB ? 12 : 0);
                    float step = beat / st.arpRate;
                    Note(buf, Sw(t0 + i * step, step, i), step * 1.6f, Hz(m), st.arpWave, st.arp * (st.arpWave == Wave.Bell ? 0.09f : 0.13f));
                }
                // 旋律：每兩小節用同一個動機，跟著和弦移調；最後一個音落回主音
                if (bi % 2 == 0)
                {
                    var mo = motifs[sectB ? 1 : 0]; var rh = rhythms[sectB ? 1 : 0];
                    float lt = 0f;
                    for (int k = 0; k < mo.Length; k++)
                    {
                        float len = rh[k] * beat;
                        bool last = barI == Bars - 2 && k == mo.Length - 1;
                        if (mo[k] != int.MinValue || last)
                        {
                            int deg = last ? 7 : c + mo[k];
                            Note(buf, Sw(t0 + lt, beat / 2f, (int)(lt / (beat / 2f) + 0.01f)), len * 0.92f, Hz(Deg(deg, 1)), st.leadWave, st.lead * LeadGain(st.leadWave));
                        }
                        lt += len;
                    }
                }
                // 鼓
                Groove(buf, st, t0, beat, barI);
                if (barI % 4 == 3) for (int f = 0; f < 4; f++) Drum(buf, t0 + bar - beat + f * beat / 4f, st.groove == 2 ? 3 : 1, st.drums * (0.12f + 0.05f * f));   // 過門
            }
            // 正規化
            float peak = 0.0001f; foreach (var v in buf) peak = Math.Max(peak, Math.Abs(v));
            float g = 0.85f / peak;
            for (int i = 0; i < n; i++) buf[i] *= g;
            return buf;
        }

        static float LeadGain(Wave w) => w == Wave.Flute ? 0.12f : w == Wave.Bell ? 0.1f : w == Wave.Saw ? 0.07f : 0.09f;

        /// <summary>鼓的節奏型（一小節）</summary>
        static void Groove(float[] buf, Style st, float t0, float beat, int barI)
        {
            float d = st.drums;
            for (int bt = 0; bt < 4; bt++)
            {
                float tb = t0 + bt * beat;
                switch (st.groove)
                {
                    case 2:   // 太鼓：低鼓切分，沒有小鼓
                        if (bt == 0 || bt == 2) Drum(buf, tb, 0, d * 0.5f);
                        if (bt == 1 || bt == 3) Drum(buf, tb + beat * 0.5f, 3, d * 0.35f);
                        if (bt == 3) Drum(buf, tb, 3, d * 0.28f);
                        Drum(buf, tb + beat * (0.5f + st.swing * 0.5f), 2, d * 0.06f);
                        break;
                    case 3:   // 半拍感：大鼓 1、小鼓 3，鈸只打四分
                        if (bt == 0 || st.driving) Drum(buf, tb, 0, d * 0.5f);
                        if (bt == 2) Drum(buf, tb, 1, d * 0.3f);
                        Drum(buf, tb, 2, d * 0.07f);
                        break;
                    case 4:   // 行進：大鼓 1、3，小鼓一直十六分滾（重音在反拍）
                        if (bt == 0 || bt == 2 || st.driving) Drum(buf, tb, 0, d * 0.5f);
                        for (int s = 0; s < 4; s++) Drum(buf, tb + s * beat / 4f, 1, d * (s == 2 ? 0.2f : bt % 2 == 1 && s == 0 ? 0.3f : 0.07f));
                        break;
                    case 5:   // 機關：十六分的滴答、大鼓 1 與 2.5
                        if (bt == 0 || bt == 2 && barI % 2 == 1 || st.driving) Drum(buf, tb, 0, d * 0.5f);
                        if (bt == 1) Drum(buf, tb + beat * 0.5f, 0, d * 0.35f);
                        if (bt == 1 || bt == 3) Drum(buf, tb, 1, d * 0.3f);
                        for (int s = 0; s < 4; s++) Drum(buf, tb + s * beat / 4f, 2, d * (s == 0 ? 0.12f : 0.07f));
                        break;
                    default:  // 一般／每拍大鼓
                        if (bt == 0 || bt == 2 || st.driving) Drum(buf, tb, 0, d * 0.5f);
                        if (bt == 1 || bt == 3) Drum(buf, tb, 1, d * 0.32f);
                        for (int h = 0; h < 2; h++) Drum(buf, tb + h * beat / 2f + (h == 1 ? beat / 2f * st.swing : 0f), 2, d * (h == 0 ? 0.1f : 0.07f));
                        break;
                }
            }
        }

        enum Wave { Pad, Saw, Pluck, Square, Flute, Bell }

        /// <summary>加一個音（尾巴超過曲子長度就繞回開頭，循環時不會斷）</summary>
        static void Note(float[] buf, float start, float dur, float f, Wave w, float vol)
        {
            int n = buf.Length, s0 = (int)(start * SR), len = (int)(dur * SR);
            float att = w == Wave.Pad ? 0.25f : w == Wave.Pluck || w == Wave.Bell ? 0.003f : w == Wave.Flute ? 0.04f : 0.01f;
            double tw = 2.0 * Math.PI * f / SR;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float env = Math.Min(1f, t / att);
                if (w == Wave.Pluck) env *= (float)Math.Exp(-t * 7.0);
                else if (w == Wave.Bell) env *= (float)Math.Exp(-t * 3.2);
                else { float rel = Math.Min(0.06f, dur * 0.3f); if (t > dur - rel) env *= Math.Max(0f, (dur - t) / rel); }
                double ph = tw * i;
                float v = w switch
                {
                    Wave.Pad => (float)(Math.Sin(ph) + 0.3 * Math.Sin(ph * 2.0 + 0.4) + 0.12 * Math.Sin(ph * 3.003)),
                    Wave.Saw => (float)(Math.Sin(ph) + 0.5 * Math.Sin(ph * 2) + 0.33 * Math.Sin(ph * 3) + 0.25 * Math.Sin(ph * 4) + 0.2 * Math.Sin(ph * 5)) * 0.7f,
                    Wave.Pluck => (float)(Math.Sin(ph) + 0.4 * Math.Sin(ph * 2) * Math.Exp(-t * 12.0) + 0.2 * Math.Sin(ph * 3) * Math.Exp(-t * 18.0)),
                    Wave.Flute => (float)(Math.Sin(ph + 0.25 * Math.Sin(2.0 * Math.PI * 5.5 * t) * Math.Min(1.0, t * 3.0)) + 0.12 * Math.Sin(ph * 2.0)),   // 笛：顫音慢慢出來
                    Wave.Bell => (float)Math.Sin(ph + 1.6 * Math.Exp(-t * 4.0) * Math.Sin(ph * 3.5)),   // 鐘：FM，起音亮、尾巴純
                    _ => (float)(Math.Sin(ph) + 0.33 * Math.Sin(ph * 3) + 0.2 * Math.Sin(ph * 5)) * 0.8f,
                };
                buf[(s0 + i) % n] += v * env * vol;
            }
        }

        /// <summary>鼓：0＝大鼓、1＝小鼓、2＝鈸、3＝低鼓（太鼓）</summary>
        static void Drum(float[] buf, float start, int kind, float vol)
        {
            int n = buf.Length, s0 = (int)(start * SR);
            float dur = kind == 0 ? 0.28f : kind == 1 ? 0.18f : kind == 3 ? 0.35f : 0.05f;
            int len = (int)(dur * SR);
            var r = new System.Random(s0 * 3 + kind);
            float lp = 0f, prev = 0f, hp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(r.NextDouble() * 2.0 - 1.0);
                float v;
                if (kind == 0) v = (float)(Math.Sin(2.0 * Math.PI * (50.0 * t + 90.0 * (1.0 - Math.Exp(-t * 30.0)) / 30.0)) * Math.Exp(-t * 9.0)) * 1.3f;
                else if (kind == 3) v = (float)(Math.Sin(2.0 * Math.PI * (95.0 * t + 60.0 * (1.0 - Math.Exp(-t * 20.0)) / 20.0)) * Math.Exp(-t * 7.0) + 0.15 * noise * Math.Exp(-t * 40.0));   // 低鼓（太鼓）
                else if (kind == 1) { lp += (noise - lp) * 0.5f; v = (float)(0.6 * lp * Math.Exp(-t * 18.0) + 0.4 * Math.Sin(2.0 * Math.PI * 190.0 * t) * Math.Exp(-t * 25.0)); }
                else { hp = 0.6f * (hp + noise - prev); prev = noise; v = hp * (float)Math.Exp(-t * 60.0); }
                buf[(s0 + i) % n] += v * vol;
            }
        }
    }
}
