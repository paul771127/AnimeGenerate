using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 設定選單：畫面右上角一直有個 ⚙ 按鈕（以前在右側 30% 高度，會壓到副本清單、技能卡等按鈕、搶走點擊），點開可以開關「音效」「音樂」「震動」、調音量（PlayerPrefs 記住）。
    /// 自己一個 MonoBehaviour、GUI.depth 最前面：選單打開時吃掉點擊，不會點到後面的按鈕。戰鬥不會暫停。
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        public static SettingsMenu I { get; private set; }
        public static bool Open { get; private set; }
        /// <summary>這時候不要畫 ⚙（例如場地預覽）</summary>
        public static System.Func<bool> Hide;
        /// <summary>「🏠 回首頁」（GameRoot 設定）；InBattle＝現在在戰鬥中（要多按一次確認）</summary>
        public static System.Action GoHome;
        public static System.Func<bool> InBattle;
        bool homeConfirm;

        /// <summary>⚙ 按鈕的位置（右上角，安全區內）：其他畫面用 RightEdge 讓出這一塊</summary>
        public static Rect GearRect
        {
            get
            {
                float W = Screen.width, H = Screen.height, sz = UiKit.BaseFont * 1.8f;
                var safe = Screen.safeArea;
                return new Rect(safe.xMax - sz - W * 0.015f, H - safe.yMax + W * 0.012f, sz, sz);
            }
        }
        /// <summary>y0～y1 這一條橫帶的右邊界：會碰到 ⚙ 就停在 ⚙ 左邊，否則用 fallback</summary>
        public static float RightEdge(float y0, float y1, float fallback)
        {
            if (Hide != null && Hide()) return fallback;
            var g = GearRect;
            return y1 > g.y && y0 < g.yMax ? Mathf.Min(fallback, g.x - UiKit.BaseFont * 0.3f) : fallback;
        }
        /// <summary>⚙ 下面第一個不會被蓋到的 y（捲動清單的上緣用）</summary>
        public static float BelowGear => GearRect.yMax + UiKit.BaseFont * 0.2f;

        /// <summary>首頁的「⚙ 設定」卡片：直接打開選單</summary>
        public static void Show() { Ensure(); Open = true; if (I != null) I.homeConfirm = false; }

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
            var gear = GearRect;
            if (!Open)
            {
                if (UiKit.Button(gear, "⚙", Mathf.RoundToInt(fs * 1.1f))) { Open = true; homeConfirm = false; }
                return;
            }

            // 背景變暗＋面板
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
            float pw = Mathf.Min(W * 0.9f, fs * 22f), rowH = fs * 2.4f, pad = fs * 0.8f;
            float ph = pad * 2 + fs * 2f + rowH * 4 + fs * 2.6f + (GoHome != null ? fs * 3.6f : 0f);
            var r = new Rect((W - pw) / 2f, (H - ph) / 2f, pw, ph);
            UiKit.Card(r, 0.95f, UiKit.Gold, 0.8f);
            UiKit.Text(new Rect(r.x + pad, r.y + pad * 0.6f, r.width - pad * 2, fs * 1.8f), "⚙ 設定", Mathf.RoundToInt(fs * 1.15f), UiKit.Gold, TextAnchor.MiddleCenter, true);
            float y = r.y + pad + fs * 2f;

            Row(ref y, r, pad, rowH, fs, fsS, "🔊 音效", Sfx.On, v => { Sfx.SetOn(v); if (v) Sfx.Play("select"); }, Sfx.Volume, Sfx.SetVolume);
            Row(ref y, r, pad, rowH, fs, fsS, "🎵 音樂", Music.On, Music.SetOn, Music.Volume, Music.SetVolume);
            Row(ref y, r, pad, rowH, fs, fsS, "📳 震動", Haptics.Enabled, v => { Haptics.Enabled = v; PlayerPrefs.SetInt("SD_HapticsOn", v ? 1 : 0); PlayerPrefs.Save(); if (v) Haptics.Play(Haptics.Kind.Medium); }, -1f, null);
            Row(ref y, r, pad, rowH, fs, fsS, "💡 環境光", ArLighting.On, ArLighting.SetOn, -1f, null);   // AR 光照估計：角色跟著房間明暗

            // 回首頁：戰鬥中要再按一次確認（這一場的戰利品不會保留）
            if (GoHome != null)
            {
                bool battle = InBattle != null && InBattle();
                var hr = new Rect(r.x + pad, y + fs * 0.4f, r.width - pad * 2, fs * 2.2f);
                string label = homeConfirm ? (battle ? "⚠ 再按一次回首頁（這場的戰利品不會保留）" : "⚠ 再按一次回首頁") : "🏠 回首頁";
                if (UiKit.Button(hr, label, homeConfirm ? fsS : fs, homeConfirm, new Color(1f, 0.55f, 0.45f)))
                {
                    if (homeConfirm) { homeConfirm = false; Open = false; GoHome(); return; }
                    homeConfirm = true;
                }
            }
            if (UiKit.Button(new Rect(r.x + pw * 0.25f, r.yMax - pad - fs * 2.2f, pw * 0.5f, fs * 2.2f), "關閉", fs, true, new Color(0.5f, 0.8f, 1f))) { Open = false; homeConfirm = false; }

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
