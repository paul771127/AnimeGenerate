using System.Runtime.InteropServices;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 手機震動：輕／中／重三種力道（iOS 用 Taptic Engine，Android 用 VibrationEffect 指定時間與強度）。
    /// 不支援時退回 Handheld.Vibrate（只有重震動會用）。
    /// </summary>
    public static class Haptics
    {
        public enum Kind { Light, Medium, Heavy, Success, Warning }
        public static bool Enabled = true;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void sd_haptic(int kind);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk = -1;
#endif

        public static void Play(Kind k)
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            try { sd_haptic((int)k); } catch (System.Exception) { if (k == Kind.Heavy) Handheld.Vibrate(); }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var act = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
                }
                long ms; int amp;
                switch (k)
                {
                    case Kind.Light: ms = 18; amp = 70; break;
                    case Kind.Medium: ms = 35; amp = 150; break;
                    case Kind.Success: ms = 60; amp = 120; break;
                    case Kind.Warning: ms = 80; amp = 200; break;
                    default: ms = 90; amp = 255; break;
                }
                if (sdk >= 26)
                {
                    using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var eff = ve.CallStatic<AndroidJavaObject>("createOneShot", ms, amp))
                        vibrator.Call("vibrate", eff);
                }
                else vibrator.Call("vibrate", ms);
            }
            catch (System.Exception) { if (k == Kind.Heavy) Handheld.Vibrate(); }
#else
            if (k == Kind.Heavy && Application.isMobilePlatform) Handheld.Vibrate();
#endif
        }
    }
}
