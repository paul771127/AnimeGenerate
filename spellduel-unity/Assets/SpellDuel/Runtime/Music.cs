using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 背景音樂：全部在程式裡合成（不需要音樂檔）。每首是 8 小節、可以無縫循環的短曲：
    ///   town（城鎮／選單：慢、大調、輕柔）、battle0／1／2（熔岩／森林／海上的戰鬥）、boss（Boss 戰：快、重）。
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
                        clips[target] = clip;
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
            public int[][] chords;                        // 8 小節的和弦（音階級數，0 起算）
            public float drums, bass, arp, pad, lead;     // 各聲部音量
            public int arpRate = 4;                       // 一拍幾個琶音
            public bool driving;                          // 大鼓每拍都打（快歌）
        }

        static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 }, Major = { 0, 2, 4, 5, 7, 9, 11 }, Dorian = { 0, 2, 3, 5, 7, 9, 10 };

        static Style StyleOf(string id) => id switch
        {
            // 城鎮：慢、大調，I–V–vi–IV
            "town" => new Style { bpm = 92, root = 60, scale = Major, chords = new[] { new[] { 0 }, new[] { 4 }, new[] { 5 }, new[] { 3 }, new[] { 0 }, new[] { 4 }, new[] { 3 }, new[] { 4 } }, drums = 0.25f, bass = 0.5f, arp = 0.35f, pad = 0.45f, lead = 0.3f, arpRate = 2 },
            // 熔岩：D 小調，i–VI–III–VII，重鼓
            "battle0" => new Style { bpm = 140, root = 50, scale = Minor, chords = new[] { new[] { 0 }, new[] { 5 }, new[] { 2 }, new[] { 6 }, new[] { 0 }, new[] { 5 }, new[] { 6 }, new[] { 4 } }, drums = 0.9f, bass = 0.75f, arp = 0.35f, pad = 0.3f, lead = 0.35f, driving = true },
            // 森林：E 多利亞，輕快的撥弦
            "battle1" => new Style { bpm = 124, root = 52, scale = Dorian, chords = new[] { new[] { 0 }, new[] { 3 }, new[] { 0 }, new[] { 6 }, new[] { 0 }, new[] { 3 }, new[] { 4 }, new[] { 6 } }, drums = 0.65f, bass = 0.6f, arp = 0.5f, pad = 0.3f, lead = 0.35f },
            // 海上：A 小調，起伏的琶音
            "battle2" => new Style { bpm = 130, root = 57, scale = Minor, chords = new[] { new[] { 0 }, new[] { 6 }, new[] { 5 }, new[] { 4 }, new[] { 0 }, new[] { 6 }, new[] { 5 }, new[] { 6 } }, drums = 0.7f, bass = 0.65f, arp = 0.45f, pad = 0.4f, lead = 0.3f },
            // Boss：C 小調，快、重
            _ => new Style { bpm = 152, root = 48, scale = Minor, chords = new[] { new[] { 0 }, new[] { 0 }, new[] { 5 }, new[] { 4 }, new[] { 0 }, new[] { 1 }, new[] { 5 }, new[] { 4 } }, drums = 1f, bass = 0.85f, arp = 0.4f, pad = 0.35f, lead = 0.4f, driving = true },
        };

        static float[] Compose(string id)
        {
            var st = StyleOf(id);
            var rnd = new System.Random(id.GetHashCode() & 0x7fffffff);
            float beat = 60f / st.bpm, bar = beat * 4f;
            int n = (int)(bar * 8 * SR);
            var buf = new float[n];
            float Hz(int midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);
            int Deg(int degree, int octave) { int len = st.scale.Length; int o = (int)Math.Floor(degree / (float)len); int k = degree - o * len; return st.root + st.scale[k] + 12 * (o + octave); }

            for (int barI = 0; barI < 8; barI++)
            {
                float t0 = barI * bar;
                int c = st.chords[barI][0];
                int[] triad = { Deg(c, 0), Deg(c + 2, 0), Deg(c + 4, 0) };
                // 鋪底和弦（高八度、柔和）
                foreach (var m in triad) Note(buf, t0, bar, Hz(m + 12), Wave.Pad, st.pad * 0.12f);
                // 低音：八分音符（快歌）或四分
                for (int i = 0; i < (st.driving ? 8 : 4); i++)
                {
                    float step = st.driving ? beat / 2f : beat;
                    int m = Deg(c, -1) - 12 + (i % 4 == 3 && !st.driving ? 7 : 0);
                    Note(buf, t0 + i * step, step * 0.9f, Hz(m), Wave.Saw, st.bass * 0.22f);
                }
                // 琶音
                int per = st.arpRate * 4;
                for (int i = 0; i < per; i++)
                {
                    int m = triad[(i % 4 == 3) ? 1 : i % 3] + 12 + (i % 8 >= 4 ? 12 : 0);
                    Note(buf, t0 + i * beat / st.arpRate, beat / st.arpRate * 1.6f, Hz(m), Wave.Pluck, st.arp * 0.13f);
                }
                // 旋律：每小節幾個音，落在和弦音或音階上，最後一小節回主音
                float lt = 0f;
                while (lt < bar - 0.01f)
                {
                    float len = beat * (rnd.NextDouble() < 0.5 ? 1f : rnd.NextDouble() < 0.5 ? 0.5f : 2f);
                    len = Math.Min(len, bar - lt);
                    if (rnd.NextDouble() < 0.82)
                    {
                        int deg = barI == 7 && lt + len >= bar - 0.01f ? 7 : c + rnd.Next(0, 3) * 2 + (rnd.NextDouble() < 0.25 ? 1 : 0);
                        Note(buf, t0 + lt, len * 0.95f, Hz(Deg(deg, 1)), Wave.Square, st.lead * 0.09f);
                    }
                    lt += len;
                }
                // 鼓
                for (int bt = 0; bt < 4; bt++)
                {
                    float tb = t0 + bt * beat;
                    if (bt == 0 || bt == 2 || st.driving) Drum(buf, tb, 0, st.drums * 0.5f);
                    if (bt == 1 || bt == 3) Drum(buf, tb, 1, st.drums * 0.32f);
                    for (int h = 0; h < 2; h++) Drum(buf, tb + h * beat / 2f, 2, st.drums * (h == 0 ? 0.1f : 0.07f));
                }
                if (barI % 4 == 3) for (int f = 0; f < 4; f++) Drum(buf, t0 + bar - beat + f * beat / 4f, 1, st.drums * (0.12f + 0.05f * f));   // 過門
            }
            // 正規化
            float peak = 0.0001f; foreach (var v in buf) peak = Math.Max(peak, Math.Abs(v));
            float g = 0.85f / peak;
            for (int i = 0; i < n; i++) buf[i] *= g;
            return buf;
        }

        enum Wave { Pad, Saw, Pluck, Square }

        /// <summary>加一個音（尾巴超過曲子長度就繞回開頭，循環時不會斷）</summary>
        static void Note(float[] buf, float start, float dur, float f, Wave w, float vol)
        {
            int n = buf.Length, s0 = (int)(start * SR), len = (int)(dur * SR);
            float att = w == Wave.Pad ? 0.25f : w == Wave.Pluck ? 0.003f : 0.01f;
            double tw = 2.0 * Math.PI * f / SR;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float env = Math.Min(1f, t / att);
                if (w == Wave.Pluck) env *= (float)Math.Exp(-t * 7.0);
                else { float rel = Math.Min(0.06f, dur * 0.3f); if (t > dur - rel) env *= Math.Max(0f, (dur - t) / rel); }
                double ph = tw * i;
                float v = w switch
                {
                    Wave.Pad => (float)(Math.Sin(ph) + 0.3 * Math.Sin(ph * 2.0 + 0.4) + 0.12 * Math.Sin(ph * 3.003)),
                    Wave.Saw => (float)(Math.Sin(ph) + 0.5 * Math.Sin(ph * 2) + 0.33 * Math.Sin(ph * 3) + 0.25 * Math.Sin(ph * 4) + 0.2 * Math.Sin(ph * 5)) * 0.7f,
                    Wave.Pluck => (float)(Math.Sin(ph) + 0.4 * Math.Sin(ph * 2) * Math.Exp(-t * 12.0) + 0.2 * Math.Sin(ph * 3) * Math.Exp(-t * 18.0)),
                    _ => (float)(Math.Sin(ph) + 0.33 * Math.Sin(ph * 3) + 0.2 * Math.Sin(ph * 5)) * 0.8f,
                };
                buf[(s0 + i) % n] += v * env * vol;
            }
        }

        /// <summary>鼓：0＝大鼓、1＝小鼓、2＝鈸</summary>
        static void Drum(float[] buf, float start, int kind, float vol)
        {
            int n = buf.Length, s0 = (int)(start * SR);
            float dur = kind == 0 ? 0.28f : kind == 1 ? 0.18f : 0.05f;
            int len = (int)(dur * SR);
            var r = new System.Random(s0 * 3 + kind);
            float lp = 0f, prev = 0f, hp = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float noise = (float)(r.NextDouble() * 2.0 - 1.0);
                float v;
                if (kind == 0) v = (float)(Math.Sin(2.0 * Math.PI * (50.0 * t + 90.0 * (1.0 - Math.Exp(-t * 30.0)) / 30.0)) * Math.Exp(-t * 9.0)) * 1.3f;
                else if (kind == 1) { lp += (noise - lp) * 0.5f; v = (float)(0.6 * lp * Math.Exp(-t * 18.0) + 0.4 * Math.Sin(2.0 * Math.PI * 190.0 * t) * Math.Exp(-t * 25.0)); }
                else { hp = 0.6f * (hp + noise - prev); prev = noise; v = hp * (float)Math.Exp(-t * 60.0); }
                buf[(s0 + i) % n] += v * vol;
            }
        }
    }
}
