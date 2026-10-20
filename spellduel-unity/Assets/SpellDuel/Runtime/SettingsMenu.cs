using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 設定選單：畫面右側一直有個 ⚙ 按鈕，點開可以開關「音效」「音樂」「震動」、調音量（PlayerPrefs 記住）。
    /// 自己一個 MonoBehaviour、GUI.depth 最前面：選單打開時吃掉點擊，不會點到後面的按鈕。戰鬥不會暫停。
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        public static SettingsMenu I { get; private set; }
        public static bool Open { get; private set; }
        /// <summary>這時候不要畫 ⚙（例如場地預覽）</summary>
        public static System.Func<bool> Hide;

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("Settings Menu");
            DontDestroyOnLoad(go);
            I = go.AddComponent<SettingsMenu>();
            Haptics.Enabled = PlayerPrefs.GetInt("SD_HapticsOn", 1) == 1;
        }

        void OnGUI()
        {
            GUI.depth = -100;
            if (Hide != null && Hide() && !Open) return;
            float W = Screen.width, H = Screen.height;
            int fs = UiKit.BaseFont, fsS = Mathf.RoundToInt(fs * 0.85f);
            var safe = Screen.safeArea;
            float sz = fs * 2.1f;
            var gear = new Rect(safe.xMax - sz - W * 0.01f, H * 0.3f, sz, sz);
            if (!Open)
            {
                if (UiKit.Button(gear, "⚙", Mathf.RoundToInt(fs * 1.1f))) Open = true;
                return;
            }

            // 背景變暗＋面板
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            float pw = Mathf.Min(W * 0.9f, fs * 22f), rowH = fs * 2.4f, pad = fs * 0.8f;
            float ph = pad * 2 + fs * 2f + rowH * 3 + fs * 2.6f;
            var r = new Rect((W - pw) / 2f, (H - ph) / 2f, pw, ph);
            UiKit.Card(r, 0.95f, UiKit.Gold, 0.8f);
            UiKit.Text(new Rect(r.x + pad, r.y + pad * 0.6f, r.width - pad * 2, fs * 1.8f), "⚙ 設定", Mathf.RoundToInt(fs * 1.15f), UiKit.Gold, TextAnchor.MiddleCenter, true);
            float y = r.y + pad + fs * 2f;

            Row(ref y, r, pad, rowH, fs, fsS, "🔊 音效", Sfx.On, v => { Sfx.SetOn(v); if (v) Sfx.Play("select"); }, Sfx.Volume, Sfx.SetVolume);
            Row(ref y, r, pad, rowH, fs, fsS, "🎵 音樂", Music.On, Music.SetOn, Music.Volume, Music.SetVolume);
            Row(ref y, r, pad, rowH, fs, fsS, "📳 震動", Haptics.Enabled, v => { Haptics.Enabled = v; PlayerPrefs.SetInt("SD_HapticsOn", v ? 1 : 0); PlayerPrefs.Save(); if (v) Haptics.Play(Haptics.Kind.Medium); }, -1f, null);

            if (UiKit.Button(new Rect(r.x + pw * 0.25f, r.yMax - pad - fs * 2.2f, pw * 0.5f, fs * 2.2f), "關閉", fs, true, new Color(0.5f, 0.8f, 1f))) Open = false;

            // 選單打開時：點擊不要傳到後面的介面
            var e = Event.current;
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag) e.Use();
        }

        /// <summary>一列：名稱、開／關按鈕、音量滑桿（volume ＜ 0＝沒有滑桿）</summary>
        static void Row(ref float y, Rect r, float pad, float rowH, int fs, int fsS, string label, bool on, System.Action<bool> set, float volume, System.Action<float> setVol)
        {
            float x = r.x + pad, w = r.width - pad * 2;
            UiKit.Text(new Rect(x, y, w * 0.32f, rowH * 0.8f), label, fs, Color.white, TextAnchor.MiddleLeft, true);
            if (UiKit.Button(new Rect(x + w * 0.32f, y + rowH * 0.08f, w * 0.22f, rowH * 0.64f), on ? "開" : "關", fsS, on, on ? new Color(0.4f, 0.95f, 0.55f) : new Color(1f, 0.45f, 0.4f))) set(!on);
            if (volume >= 0f && setVol != null)
            {
                var sr = new Rect(x + w * 0.58f, y + rowH * 0.28f, w * 0.42f, rowH * 0.3f);
                GUI.enabled = on;
                var track = new GUIStyle(GUI.skin.horizontalSlider) { fixedHeight = sr.height * 0.5f };
                var thumb = new GUIStyle(GUI.skin.horizontalSliderThumb) { fixedHeight = sr.height * 1.3f, fixedWidth = sr.height * 1.3f };
                float nv = GUI.HorizontalSlider(sr, volume, 0f, 1f, track, thumb);
                GUI.enabled = true;
                if (Mathf.Abs(nv - volume) > 0.001f) setVol(nv);
                UiKit.Text(new Rect(sr.x, sr.yMax, sr.width, rowH * 0.32f), $"音量 {Mathf.RoundToInt(volume * 100)}%", Mathf.RoundToInt(fsS * 0.8f), UiKit.Muted, TextAnchor.UpperCenter);
            }
            y += rowH;
        }
    }
}
