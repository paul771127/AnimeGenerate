using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 本機咒語辨識（離線、不需要任何語音服務；從網頁版 voice-local.js 移植）：
    ///   1. 玩家先把每個技能的咒語各唸 2 次當樣本。
    ///   2. 對戰時用音量偵測切出每一句話，算 MFCC 特徵，用 DTW 和樣本比對；最像且夠像的技能就觸發。
    /// 比對的是「自己的聲音」，任何語言、任何咒語都可以；對手的聲音通常比較小聲，也會被音量門檻擋掉。
    /// </summary>
    public class KeywordSpotter
    {
        public const int NCep = 12;
        const int NMel = 26;
        const float FrameMs = 25f, HopMs = 10f;
        const int Hangover = 18;                  // 安靜幾個 hop（×10ms）算一句話結束
        const int MinFrames = 15, MaxFrames = 250, Preroll = 12;

        public readonly int SampleRate;
        readonly int frameLen, hop, nfft;
        readonly float[] win, re, im, logMel;
        readonly float[][] bank;

        // 斷句狀態
        readonly List<float> buf = new List<float>();
        readonly List<Frame> history = new List<Frame>(), utt = new List<Frame>();
        float floor; int calib, loud, quiet; bool inSpeech;
        public float Level { get; private set; }        // 最近一個 frame 的音量（RMS）
        public float StartThreshold { get; private set; } = 0.006f;
        public bool InSpeech => inSpeech;

        /// <summary>切出一句話：MFCC 序列（已正規化）與音量峰值</summary>
        public event Action<float[][], float> OnUtterance;

        struct Frame { public float[] cep; public float rms; }

        public KeywordSpotter(int sampleRate)
        {
            SampleRate = sampleRate;
            frameLen = Mathf.RoundToInt(sampleRate * FrameMs / 1000f);
            hop = Mathf.RoundToInt(sampleRate * HopMs / 1000f);
            nfft = 1; while (nfft < frameLen) nfft <<= 1;
            win = new float[frameLen];
            for (int i = 0; i < frameLen; i++) win[i] = 0.54f - 0.46f * Mathf.Cos(2f * Mathf.PI * i / (frameLen - 1));
            re = new float[nfft]; im = new float[nfft]; logMel = new float[NMel];

            float Mel(float f) => 2595f * Mathf.Log10(1f + f / 700f);
            float Inv(float m) => 700f * (Mathf.Pow(10f, m / 2595f) - 1f);
            float lo = Mel(100f), hi = Mel(Mathf.Min(7000f, sampleRate / 2f));
            var pts = new int[NMel + 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = Mathf.FloorToInt((nfft + 1) * Inv(lo + (hi - lo) * i / (NMel + 1)) / sampleRate);
            bank = new float[NMel][];
            for (int m = 1; m <= NMel; m++)
            {
                var f = new float[nfft / 2 + 1];
                for (int k = pts[m - 1]; k < pts[m]; k++) f[k] = (k - pts[m - 1]) / (float)Mathf.Max(1, pts[m] - pts[m - 1]);
                for (int k = pts[m]; k < pts[m + 1]; k++) f[k] = (pts[m + 1] - k) / (float)Mathf.Max(1, pts[m + 1] - pts[m]);
                bank[m - 1] = f;
            }
        }

        public void Reset()
        {
            buf.Clear(); history.Clear(); utt.Clear();
            floor = 0f; calib = 0; loud = 0; quiet = 0; inSpeech = false;
        }

        // ------------------------------------------------------------ 訊號處理
        static void Fft(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = -2 * Math.PI / len; float wr = (float)Math.Cos(ang), wi = (float)Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    float cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        float tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti;
                        re[a] += tr; im[a] += ti;
                        float nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                    }
                }
            }
        }

        Frame ComputeFrame(List<float> x, int off)
        {
            Array.Clear(re, 0, nfft); Array.Clear(im, 0, nfft);
            float e = 0f, prev = off > 0 ? x[off - 1] : 0f;
            for (int i = 0; i < frameLen; i++)
            {
                float v = x[off + i];
                e += v * v;
                re[i] = (v - 0.97f * prev) * win[i];   // 預強調
                prev = v;
            }
            Fft(re, im);
            int half = nfft / 2 + 1;
            for (int m = 0; m < NMel; m++)
            {
                var f = bank[m]; double s = 0;
                for (int k = 0; k < half; k++) if (f[k] != 0f) s += f[k] * (re[k] * re[k] + im[k] * im[k]);
                logMel[m] = (float)Math.Log(s + 1e-10);
            }
            var cep = new float[NCep];
            for (int c = 1; c <= NCep; c++)
            {
                double s = 0;
                for (int m = 0; m < NMel; m++) s += logMel[m] * Math.Cos(Math.PI * c * (m + 0.5) / NMel);
                cep[c - 1] = (float)s;
            }
            return new Frame { cep = cep, rms = Mathf.Sqrt(e / frameLen) };
        }

        /// <summary>送入新的音訊樣本（單聲道、-1～1）</summary>
        public void Push(float[] samples, int count)
        {
            for (int i = 0; i < count; i++) buf.Add(samples[i]);
            int off = 0;
            while (off + frameLen <= buf.Count) { OnFrame(ComputeFrame(buf, off)); off += hop; }
            if (off > 0) buf.RemoveRange(0, off);
        }

        void OnFrame(Frame f)
        {
            Level = f.rms;
            // 噪音底：先用前 30 個 frame 校正，之後只在非說話時慢慢追蹤
            if (calib < 30) { floor += f.rms / 30f; calib++; return; }
            float startThr = Mathf.Max(floor * 3.2f, 0.006f), endThr = Mathf.Max(floor * 2f, 0.004f);
            StartThreshold = startThr;
            if (!inSpeech)
            {
                floor = f.rms < floor * 2.5f ? floor * 0.99f + f.rms * 0.01f : floor * 0.999f + f.rms * 0.001f;
                history.Add(f);
                if (history.Count > Preroll) history.RemoveAt(0);
                loud = f.rms > startThr ? loud + 1 : 0;
                if (loud >= 3) { inSpeech = true; utt.Clear(); utt.AddRange(history); quiet = 0; }
                return;
            }
            utt.Add(f);
            quiet = f.rms < endThr ? quiet + 1 : 0;
            if (quiet >= Hangover || utt.Count >= MaxFrames)
            {
                inSpeech = false; history.Clear(); loud = 0;
                int n = Mathf.Min(utt.Count, utt.Count - quiet + 3);
                if (n >= MinFrames)
                {
                    float peak = 0f;
                    var seq = new float[n][];
                    for (int i = 0; i < n; i++) { seq[i] = utt[i].cep; peak = Mathf.Max(peak, utt[i].rms); }
                    OnUtterance?.Invoke(Normalize(seq), peak);
                }
                utt.Clear();
            }
        }

        /// <summary>倒頻譜平均正規化：消除麥克風、距離造成的整體音色差</summary>
        static float[][] Normalize(float[][] seq)
        {
            var mean = new float[NCep];
            foreach (var f in seq) for (int i = 0; i < NCep; i++) mean[i] += f[i] / seq.Length;
            var o = new float[seq.Length][];
            for (int j = 0; j < seq.Length; j++)
            {
                o[j] = new float[NCep];
                for (int i = 0; i < NCep; i++) o[j][i] = seq[j][i] - mean[i];
            }
            return o;
        }

        public static float Dtw(float[][] a, float[][] b)
        {
            int n = a.Length, m = b.Length;
            int band = Mathf.Max(Mathf.Abs(n - m) + 10, Mathf.RoundToInt(Mathf.Max(n, m) * 0.35f));
            var prev = new double[m + 1]; var cur = new double[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = double.PositiveInfinity;
            prev[0] = 0;
            for (int i = 1; i <= n; i++)
            {
                for (int j = 0; j <= m; j++) cur[j] = double.PositiveInfinity;
                int c = (int)Math.Round((double)i * m / n);
                int j0 = Math.Max(1, c - band), j1 = Math.Min(m, c + band);
                for (int j = j0; j <= j1; j++)
                {
                    double d = 0; var x = a[i - 1]; var y = b[j - 1];
                    for (int k = 0; k < NCep; k++) { double t = x[k] - y[k]; d += t * t; }
                    cur[j] = Math.Sqrt(d) + Math.Min(prev[j], Math.Min(cur[j - 1], prev[j - 1]));
                }
                (prev, cur) = (cur, prev);
            }
            return (float)(prev[m] / (n + m));
        }
    }

    /// <summary>每個技能的咒語樣本（存在 PlayerPrefs）</summary>
    [Serializable]
    public class VoiceTemplates
    {
        [Serializable] public class Seq { public int frames; public float[] data; }
        [Serializable] public class Entry { public string id; public float thr, peak; public List<Seq> templates = new List<Seq>(); }
        public List<Entry> entries = new List<Entry>();

        const string Key = "sd_voice_v1";

        public static VoiceTemplates Load()
        {
            try { var s = PlayerPrefs.GetString(Key, ""); if (!string.IsNullOrEmpty(s)) return JsonUtility.FromJson<VoiceTemplates>(s) ?? new VoiceTemplates(); }
            catch { }
            return new VoiceTemplates();
        }
        public void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(this)); PlayerPrefs.Save(); }

        public Entry Get(string id) { foreach (var e in entries) if (e.id == id) return e; return null; }
        public int Count(string id) => Get(id)?.templates.Count ?? 0;
        public bool Ready(string id) => Count(id) >= 2;

        public static Seq Pack(float[][] seq)
        {
            var d = new float[seq.Length * KeywordSpotter.NCep];
            for (int i = 0; i < seq.Length; i++) Array.Copy(seq[i], 0, d, i * KeywordSpotter.NCep, KeywordSpotter.NCep);
            return new Seq { frames = seq.Length, data = d };
        }
        public static float[][] Unpack(Seq s)
        {
            var o = new float[s.frames][];
            for (int i = 0; i < s.frames; i++) { o[i] = new float[KeywordSpotter.NCep]; Array.Copy(s.data, i * KeywordSpotter.NCep, o[i], 0, KeywordSpotter.NCep); }
            return o;
        }

        /// <summary>錄一個樣本；第 2 個之後計算這個技能的門檻（兩次樣本彼此的距離 × 1.6）</summary>
        public void AddSample(string id, float[][] seq, float peak)
        {
            var e = Get(id);
            if (e == null) { e = new Entry { id = id }; entries.Add(e); }
            if (e.templates.Count >= 2) e.templates.Clear();
            e.templates.Add(Pack(seq));
            e.peak = e.templates.Count == 1 ? peak : (e.peak + peak) / 2f;
            if (e.templates.Count >= 2)
            {
                float intra = KeywordSpotter.Dtw(Unpack(e.templates[0]), Unpack(e.templates[1]));
                e.thr = Mathf.Max(intra * 1.6f, intra + 3f);
            }
        }

        public void ClearSkill(string id) { var e = Get(id); if (e != null) entries.Remove(e); }

        public struct Result { public string id; public string reason; public float best, second; }

        /// <summary>在可用的技能中找最像的；不夠像、分不清、太小聲（可能是對手的聲音）都不觸發</summary>
        public Result Classify(float[][] seq, float peak, IEnumerable<string> skillIds)
        {
            string bestId = null; float best = float.MaxValue, second = float.MaxValue, thr = 0f, refPeak = peak;
            foreach (var id in skillIds)
            {
                var e = Get(id);
                if (e == null || e.templates.Count < 2) continue;
                float d = float.MaxValue;
                foreach (var t in e.templates) d = Mathf.Min(d, KeywordSpotter.Dtw(seq, Unpack(t)));
                if (d < best) { second = best; best = d; bestId = id; thr = e.thr; refPeak = e.peak > 0 ? e.peak : peak; }
                else if (d < second) second = d;
            }
            var r = new Result { best = best, second = second };
            if (bestId == null) { r.reason = "還沒錄咒語"; return r; }
            if (best > thr) r.reason = "不夠像";
            else if (second < float.MaxValue && best > second * 0.92f) r.reason = "分不清";
            else if (peak / refPeak < 0.3f) r.reason = "太小聲（可能是對手的聲音）";
            else r.id = bestId;
            return r;
        }
    }

    /// <summary>手機麥克風 → KeywordSpotter（Unity Microphone，iOS／Android 都可用）</summary>
    public class MicInput
    {
        public KeywordSpotter Spotter { get; private set; }
        public bool Running { get; private set; }
        public string Error { get; private set; } = "";
        AudioClip clip; string device; int readPos;
        float[] chunk = new float[4096];

        public bool Start()
        {
            if (Running) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                Error = "請允許麥克風權限後再按一次";
                return false;
            }
#endif
            if (Microphone.devices.Length == 0) { Error = "找不到麥克風"; return false; }
            device = Microphone.devices[0];
            Microphone.GetDeviceCaps(device, out int minF, out int maxF);
            int rate = 16000;
            if (minF > 0 || maxF > 0) rate = Mathf.Clamp(rate, Mathf.Max(minF, 8000), maxF > 0 ? maxF : 48000);
            clip = Microphone.Start(device, true, 2, rate);
            if (clip == null) { Error = "麥克風啟動失敗"; return false; }
            Spotter = new KeywordSpotter(clip.frequency);
            readPos = 0;
            Running = true; Error = "";
            return true;
        }

        public void Stop()
        {
            if (!Running) return;
            Microphone.End(device);
            Running = false;
        }

        /// <summary>每幀呼叫：讀取新錄到的樣本送進辨識</summary>
        public void Tick()
        {
            if (!Running || clip == null) return;
            int pos = Microphone.GetPosition(device);
            if (pos < 0 || pos == readPos) return;
            int total = clip.samples;
            int avail = pos > readPos ? pos - readPos : total - readPos + pos;
            while (avail > 0)
            {
                int n = Mathf.Min(avail, chunk.Length, total - readPos);
                if (chunk.Length < n) chunk = new float[n];
                var tmp = n == chunk.Length ? chunk : new float[n];
                clip.GetData(tmp, readPos);
                Spotter.Push(tmp, n);
                readPos = (readPos + n) % total;
                avail -= n;
            }
        }
    }
}
