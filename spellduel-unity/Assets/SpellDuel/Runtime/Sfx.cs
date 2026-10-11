using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 音效：全部在程式裡合成（不需要音效檔）。啟動時算好波形存成 AudioClip，播放時從小型音源池挑一個空的。
    ///   放招（各職業不同）、命中、被打、格擋、回復、蓄滿、警告、爆炸、選招、勝敗…
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }
        public static float Volume = 0.8f;

        const int SR = 22050;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource[] pool;
        int next;
        System.Random rng = new System.Random(7);

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            I = go.AddComponent<Sfx>();
        }

        void Awake()
        {
            I = this;
            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                pool[i] = s;
            }
            Build();
        }

        /// <summary>播放音效（pitch 會加一點隨機變化，連續播不會聽起來一模一樣）</summary>
        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (I == null) Ensure();
            if (!I.clips.TryGetValue(name, out var clip)) return;
            var s = I.pool[I.next]; I.next = (I.next + 1) % I.pool.Length;
            s.pitch = pitch * (0.95f + 0.1f * (float)I.rng.NextDouble());
            s.PlayOneShot(clip, Mathf.Clamp01(volume * Volume));
        }

        /// <summary>各職業的放招音效</summary>
        public static void Cast(string classId, float volume = 1f) => Play("cast_" + (classId ?? "mage"), volume);

        // ================================================================ 合成
        void Build()
        {
            // 法師：上揚的閃亮和弦＋一點雜訊
            Add("cast_mage", 0.45f, (t, d) => Env(t, 0.01f, d) * (0.5f * Sin(t, 520f + 900f * t) + 0.3f * Sin(t, 780f + 1350f * t) + 0.15f * Noise() * (1f - t / d)));
            // 獵人：弓弦的「繃」（撥弦）＋箭飛出去的咻
            Add("cast_archer", 0.4f, (t, d) => Pluck(t, 196f, 9f) * 0.8f + Env(t, 0.02f, 0.3f) * 0.35f * Noise() * Hp());
            // 劍士：揮劍的風切聲（雜訊、音高往下掃）
            Add("cast_swordsman", 0.32f, (t, d) => Env(t, 0.04f, d) * Swoosh(t, d, 2400f, 600f) * 0.9f);
            // 刺客：短促的咻＋金屬喀
            Add("cast_assassin", 0.22f, (t, d) => Env(t, 0.01f, d) * Swoosh(t, d, 3500f, 1500f) * 0.8f + (t < 0.03f ? 0.6f * Sin(t, 3200f) * (1f - t / 0.03f) : 0f));
            // 牧師：清亮的鐘聲和弦
            Add("cast_priest", 0.8f, (t, d) => Bell(t, 660f) * 0.4f + Bell(t, 990f) * 0.25f + Bell(t, 1320f) * 0.15f);
            // 命中：重擊（低頻下沉＋雜訊爆裂）
            Add("hit", 0.28f, (t, d) => Env(t, 0.002f, d) * (0.9f * Sin(t, 140f - 260f * t) + 0.6f * Noise() * Mathf.Exp(-t * 30f)));
            // 被打：更低更悶，加一點破音
            Add("hurt", 0.35f, (t, d) => Mathf.Clamp(Env(t, 0.002f, d) * (1.4f * Sin(t, 90f - 120f * t) + 0.7f * Noise() * Mathf.Exp(-t * 18f)), -0.9f, 0.9f));
            // 格擋／護盾：金屬鏗
            Add("block", 0.5f, (t, d) => Mathf.Exp(-t * 9f) * (0.4f * Sin(t, 1250f) + 0.3f * Sin(t, 1873f) + 0.2f * Sin(t, 2710f)) + (t < 0.01f ? Noise() * 0.5f : 0f));
            // 回復：上升琶音
            Add("heal", 0.6f, (t, d) => { int k = Mathf.Min(3, (int)(t / 0.12f)); float f = 523f * Mathf.Pow(1.26f, k); return Env(t - k * 0.12f, 0.01f, 0.3f) * 0.45f * Sin(t, f); });
            // 蓄滿：清脆的叮
            Add("charged", 0.35f, (t, d) => Mathf.Exp(-t * 10f) * (0.5f * Sin(t, 1568f) + 0.25f * Sin(t, 2349f)));
            // 法術逼近：兩聲短警告
            Add("warn", 0.26f, (t, d) => ((t % 0.13f) < 0.07f ? 0.35f : 0f) * Square(t, 880f));
            // 爆炸：低頻轟＋長雜訊
            Add("boom", 0.9f, (t, d) => Mathf.Clamp(Mathf.Exp(-t * 4f) * (1.2f * Sin(t, 70f - 40f * t) + 0.8f * LowNoise(0.15f)), -0.95f, 0.95f));
            // 唸對技能名稱：短促的確認音
            Add("select", 0.12f, (t, d) => Env(t, 0.005f, d) * 0.4f * (Sin(t, 880f) + Sin(t, 1320f) * 0.5f));
            // 閃避成功：輕快的咻（往上）
            Add("dodge", 0.25f, (t, d) => Env(t, 0.02f, d) * Swoosh(t, d, 900f, 3000f) * 0.7f);
            // 陷阱、定身：喀嚓
            Add("trap", 0.2f, (t, d) => Mathf.Exp(-t * 25f) * (0.6f * Noise() + 0.4f * Square(t, 300f)));
            // Boss 怒吼：低沉的咆哮
            Add("roar", 1.2f, (t, d) => Env(t, 0.15f, d) * Mathf.Clamp(0.8f * Saw(t, 55f + 10f * Mathf.Sin(t * 30f)) * (0.6f + 0.4f * LowNoise(0.3f)), -0.8f, 0.8f));
            // 勝利／失敗
            Add("win", 1.1f, (t, d) => { float[] n = { 523f, 659f, 784f, 1047f }; int k = Mathf.Min(3, (int)(t / 0.16f)); return Env(t - k * 0.16f, 0.01f, k == 3 ? 0.6f : 0.2f) * 0.4f * (Sin(t, n[k]) + 0.3f * Sin(t, n[k] * 2f)); });
            Add("lose", 1.1f, (t, d) => { float[] n = { 392f, 370f, 349f, 262f }; int k = Mathf.Min(3, (int)(t / 0.22f)); return Env(t - k * 0.22f, 0.01f, k == 3 ? 0.6f : 0.25f) * 0.35f * Square(t, n[k]) * 0.6f; });
        }

        void Add(string name, float seconds, Func<float, float, float> f)
        {
            int n = Mathf.Max(1, (int)(seconds * SR));
            var data = new float[n];
            lp = 0f; hpPrev = 0f; hpOut = 0f;
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                curNoise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float v = f(t, seconds);
                // 頭尾淡入淡出，避免爆音
                float edge = Mathf.Min(1f, Mathf.Min(i, n - 1 - i) / (SR * 0.004f));
                data[i] = v * edge;
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            if (peak > 0.95f) for (int i = 0; i < n; i++) data[i] *= 0.95f / peak;
            var clip = AudioClip.Create(name, n, 1, SR, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }

        // ---- 合成用的小工具（純函式，t＝秒）
        float curNoise, lp, hpPrev, hpOut;
        float Noise() => curNoise;
        float LowNoise(float k) { lp += (curNoise - lp) * k; return lp * 2.5f; }   // 一階低通的雜訊（悶）
        float Hp() { hpOut = 0.7f * (hpOut + curNoise - hpPrev); hpPrev = curNoise; return hpOut; }   // 高通（尖）
        static float Sin(float t, float f) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
        static float Saw(float t, float f) => 2f * ((t * f) % 1f) - 1f;
        static float Env(float t, float attack, float length)
        {
            if (t < 0f || t > length) return 0f;
            float a = t < attack ? t / attack : 1f;
            return a * Mathf.Pow(1f - t / length, 2f);
        }
        float Swoosh(float t, float d, float f0, float f1)
        {
            // 帶通的感覺：低通係數隨時間掃過
            float f = Mathf.Lerp(f0, f1, t / d);
            float k = Mathf.Clamp01(2f * Mathf.PI * f / SR);
            lp += (curNoise - lp) * k;
            return (curNoise - lp) * 0.5f + lp * 0.8f;
        }
        static float Pluck(float t, float f, float decay) => Mathf.Exp(-t * decay) * (0.6f * Sin(t, f) + 0.3f * Sin(t, f * 2f) + 0.15f * Sin(t, f * 3f));
        static float Bell(float t, float f) => Mathf.Exp(-t * 4f) * (Sin(t, f) + 0.4f * Sin(t, f * 2.76f) * Mathf.Exp(-t * 6f));
    }
}
