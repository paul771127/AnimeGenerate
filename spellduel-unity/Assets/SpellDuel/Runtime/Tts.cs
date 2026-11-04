using System.Runtime.InteropServices;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 文字轉語音（電腦唸給玩家聽：背誦試煉、快問快答）。
    ///   iOS：AVSpeechSynthesizer（Plugins/iOS/TtsBridge.mm，台灣中文的聲音）
    ///   Android：android.speech.tts.TextToSpeech（第一次用時初始化）
    ///   編輯器與其他平台：不出聲（畫面上照樣有文字）。
    /// 語音辨識在唸的期間會聽到電腦自己的聲音，所以呼叫的人要用 BusyUntil 忽略那段時間聽到的字。
    /// </summary>
    public static class Tts
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void sd_tts_speak(string utf8, float rate);
        [DllImport("__Internal")] static extern void sd_tts_stop();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject tts; static bool ready;
        class InitListener : AndroidJavaProxy
        {
            public InitListener() : base("android.speech.tts.TextToSpeech$OnInitListener") { }
            void onInit(int status)
            {
                ready = status == 0;
                if (!ready || tts == null) return;
                try
                {
                    using (var loc = new AndroidJavaObject("java.util.Locale", "zh", "TW")) tts.Call<int>("setLanguage", loc);
                    tts.Call<int>("setSpeechRate", 0.9f);
                }
                catch (System.Exception) { }
            }
        }
        static void EnsureAndroid()
        {
            if (tts != null) return;
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                    tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", act, new InitListener());
                }
            }
            catch (System.Exception e) { Debug.LogWarning("TTS: " + e.Message); }
        }
#endif

        /// <summary>這段時間內電腦還在唸（估計值：每個字約 0.27 秒＋0.6 秒緩衝）</summary>
        public static float BusyUntil { get; private set; }

        public static bool Supported =>
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            true;
#else
            false;
#endif

        public static void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            BusyUntil = Time.time + 0.6f + text.Length * 0.27f;
#if UNITY_IOS && !UNITY_EDITOR
            try { sd_tts_speak(text, 0.45f); } catch (System.Exception) { }
#elif UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroid();
            if (tts != null && ready) { try { using (var b = new AndroidJavaObject("android.os.Bundle")) tts.Call<int>("speak", text, 0, b, "sd"); } catch (System.Exception) { } }
#endif
        }

        /// <summary>先初始化（Android 的 TextToSpeech 要等一下才能用）：玩法開始時叫一次</summary>
        public static void Warmup()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroid();
#endif
        }

        public static void Stop()
        {
            BusyUntil = 0f;
#if UNITY_IOS && !UNITY_EDITOR
            try { sd_tts_stop(); } catch (System.Exception) { }
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null) { try { tts.Call<int>("stop"); } catch (System.Exception) { } }
#endif
        }
    }
}
