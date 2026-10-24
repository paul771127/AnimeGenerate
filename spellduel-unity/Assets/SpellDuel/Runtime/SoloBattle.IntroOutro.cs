using UnityEngine;

namespace SpellDuel
{
    /// <summary>戰鬥開始（倒數）與結束（勝利／倒下）時畫面中央的文字</summary>
    public partial class SoloBattle
    {
        void DrawIntroOutro()
        {
            if (Watching || StoryVideo.Active) return;
            float W = Screen.width, H = Screen.height;
            int fs = UiKit.BaseFont;
            if (InIntro && !InReady)
            {
                float left = introUntil - Time.time;
                string title = storyStage != null ? storyStage.title : dungeon != null ? dungeon.name : daily.HasValue ? DailyChallenge.Names[(int)daily.Value] : tutorial ? "教學" : "";
                var r = new Rect(0, H * 0.32f, W, fs * 8f);
                GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
                UiKit.Text(new Rect(0, r.y + fs * 0.4f, W, fs * 2.6f), "⚔ 戰鬥開始", Mathf.RoundToInt(fs * 2.2f), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, true);
                if (title.Length > 0) UiKit.Text(new Rect(0, r.y + fs * 3f, W, fs * 1.6f), title, fs, Color.white, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(0, r.y + fs * 4.6f, W, fs * 3f), $"{Mathf.CeilToInt(Mathf.Max(0.01f, left - 0.4f))}", Mathf.RoundToInt(fs * 2.6f), Color.white, TextAnchor.MiddleCenter, true);
                return;
            }
            if (outroUntil > 0f && phase == Phase.Fighting)
            {
                var r = new Rect(0, H * 0.34f, W, fs * 6f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white;
                UiKit.Text(new Rect(0, r.y + fs * 0.5f, W, fs * 2.6f), outroWin ? "🏆 戰鬥結束・勝利！" : "💀 戰鬥結束", Mathf.RoundToInt(fs * 2f), outroWin ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.5f, 0.45f), TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(0, r.y + fs * 3.4f, W, fs * 1.6f), outroWin ? "敵人全部打倒了，正在整理戰利品…" : "你倒下了…", fs, Color.white, TextAnchor.MiddleCenter, true);
            }
        }
    }
}
