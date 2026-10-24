using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 每場戰鬥開始前的準備（單人；連線模式兩邊要同步開打，維持原本的自動校正）：
    ///   ① 站直，手機舉在眼前平常操作的高度、對著前方，穩住 1 秒 → 記下站姿高度
    ///   ② 蹲下 → 記下蹲姿高度（蹲下躲攻擊的門檻照這個算）
    ///   ③ 站起來
    /// 每一步做到就打勾＋閃一下＋震動，再顯示下一步；全部完成才出現「⚔ 戰鬥開始」倒數，敵人這段時間不會動。
    /// 15 秒還蹲不下去（坐著玩、空間太矮）會出現「跳過」。
    /// </summary>
    public partial class SoloBattle
    {
        int readyStep = -1;              // -1 不用（連線、沒開打）、0～2 進行中、3 完成
        float readyHoldSince = -1f, readyStepAt, readyFlashAt = -9f, readyStand, readyMin;
        readonly List<float> readyYs = new List<float>();
        bool readyCalibrated;            // 這場已經用準備步驟校正過（戰鬥中的自動校正就不再做）
        public const float ReadyHoldTime = 1f, ReadySkipAfter = 15f;
        static readonly string[] ReadyTitles = { "📱 站直，手機舉在眼前", "⬇ 蹲下", "⬆ 站起來" };

        bool InReady => readyStep >= 0 && readyStep < 3 && phase == Phase.Fighting;

        void BeginReadyCheck() { readyStep = 0; readyHoldSince = -1f; readyStepAt = Time.time; readyYs.Clear(); readyCalibrated = false; }

        /// <summary>每幀（開場階段）：看手機高度與朝向推進步驟；還沒完成就把開場倒數往後推</summary>
        void UpdateReadyCheck()
        {
            if (!InReady) return;
            introUntil = Time.time + IntroTime;   // 準備好之前不開始倒數
            if (!Tracking.Ok || cam == null || StoryVideo.Active || dlg != null) { readyHoldSince = -1f; return; }   // 播劇情影片、對話中：先等
            float y = WorldFrame.ToWorld(cam.transform.position).y;
            var fw = cam.transform.forward;
            switch (readyStep)
            {
                case 0:
                {
                    // 手機在眼前（約 1～2.2m 高）、鏡頭大致朝前（不往地上或天花板看），而且穩住
                    bool ok = y > 0.95f && y < 2.3f && Mathf.Abs(fw.y) < 0.45f;
                    if (ok) { readyYs.Add(y); if (readyYs.Count > 90) readyYs.RemoveAt(0); }
                    bool steady = false;
                    if (ok && readyYs.Count >= 10)
                    {
                        float mn = float.MaxValue, mx = float.MinValue; int from = Mathf.Max(0, readyYs.Count - 30);
                        for (int i = from; i < readyYs.Count; i++) { mn = Mathf.Min(mn, readyYs[i]); mx = Mathf.Max(mx, readyYs[i]); }
                        steady = mx - mn < 0.06f;
                    }
                    if (!(ok && steady)) { readyHoldSince = -1f; if (!ok) readyYs.Clear(); return; }
                    if (readyHoldSince < 0f) readyHoldSince = Time.time;
                    if (Time.time - readyHoldSince < ReadyHoldTime) return;
                    float sum = 0f; int n = 0; for (int i = Mathf.Max(0, readyYs.Count - 30); i < readyYs.Count; i++) { sum += readyYs[i]; n++; }
                    readyStand = sum / Mathf.Max(1, n); readyMin = readyStand;
                    ReadyNext();
                    break;
                }
                case 1:
                    readyMin = Mathf.Min(readyMin, y);
                    if (y < readyStand - Mathf.Max(0.25f, readyStand * 0.2f))
                    {
                        if (readyHoldSince < 0f) readyHoldSince = Time.time;
                        if (Time.time - readyHoldSince > 0.3f) ReadyNext();
                    }
                    else readyHoldSince = -1f;
                    break;
                case 2:
                    readyMin = Mathf.Min(readyMin, y);
                    if (y > readyStand - 0.1f) { SaveReadyCalibration(true); ReadyNext(); }
                    break;
            }
        }

        void ReadyNext()
        {
            readyStep++; readyHoldSince = -1f; readyStepAt = Time.time; readyFlashAt = Time.time;
            Haptics.Play(readyStep >= 3 ? Haptics.Kind.Success : Haptics.Kind.Medium);
            Sfx.Play(readyStep >= 3 ? "charged" : "select", 0.8f, 1f + 0.15f * readyStep);
            if (readyStep >= 3) { introUntil = Time.time + IntroTime; Sfx.Play("select", 0.7f, 0.8f); }
        }

        /// <summary>把準備步驟量到的高度交給身高校正（站姿＋蹲姿比例）</summary>
        void SaveReadyCalibration(bool crouched)
        {
            standH = readyStand; standY = readyStand; readyCalibrated = true; calibN = 0; crouchCalib = 0;
            float r = readyMin / readyStand;
            if (crouched && r > 0.3f && r < 0.85f) { try { PlayerPrefs.SetFloat(CrouchRatioKey, r); PlayerPrefs.Save(); } catch { } }
        }

        void DrawReadyCheck()
        {
            if (StoryVideo.Active || dlg != null) return;
            if (!InReady && !(readyStep >= 3 && Time.time - readyFlashAt < 0.6f && phase == Phase.Fighting)) return;
            float W = Screen.width, H = Screen.height; int fs = UiKit.BaseFont, fsL = Mathf.RoundToInt(fs * 1.5f);
            float since = Time.time - readyFlashAt;
            if (since < 0.45f) EdgeFlash(new Color(0.45f, 1f, 0.5f), 1f - since / 0.45f);   // 做到了：畫面四邊亮一下
            if (readyStep >= 3) return;   // 完成：接著是「⚔ 戰鬥開始」倒數（DrawIntroOutro）
            var r = new Rect(W * 0.06f, H * 0.2f, W * 0.88f, fs * 13f);
            UiKit.Card(r, 0.88f, UiKit.Gold, 0.8f);
            float x = r.x + fs, w = r.width - fs * 2, y = r.y + fs * 0.6f;
            UiKit.Text(new Rect(x, y, w, fs * 1.8f), "戰鬥準備：身高校正", Mathf.RoundToInt(fs * 1.2f), UiKit.Gold, TextAnchor.MiddleCenter, true);
            y += fs * 2.2f;
            for (int i = 0; i < 3; i++)
            {
                bool done = i < readyStep, cur = i == readyStep;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                var c = done ? new Color(0.45f, 1f, 0.5f) : cur ? Color.Lerp(Color.white, UiKit.Gold, pulse) : new Color(1f, 1f, 1f, 0.35f);
                UiKit.Text(new Rect(x, y, w, fs * 1.9f), (done ? "✅ " : cur ? "👉 " : "⬜ ") + ReadyTitles[i], cur ? fsL : fs, c, TextAnchor.MiddleLeft, true);
                y += fs * 2.1f;
            }
            string tip = !Tracking.Ok ? "AR 追蹤中…慢慢移動手機掃一下周圍" : readyStep switch
            {
                0 => readyHoldSince > 0f ? $"很好，保持不動… {Mathf.Max(0f, ReadyHoldTime - (Time.time - readyHoldSince)):0.0}" : "像平常玩的時候一樣，手機拿在眼睛前面、鏡頭對著前方，穩住 1 秒",
                1 => "手機一樣對著前方，整個人蹲下去（之後蹲下就能躲開橫掃攻擊）",
                _ => "站起來，回到剛才的高度",
            };
            UiKit.Text(new Rect(x, y, w, fs * 2.4f), tip, fs, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleCenter);
            y += fs * 2.6f;
            if (readyStep >= 1 && Time.time - readyStepAt > ReadySkipAfter)
            {
                // 坐著玩、空間太矮：可以跳過蹲姿（用預設的蹲姿比例）
                if (UiKit.Button(new Rect(r.center.x - fs * 5f, y, fs * 10f, fs * 2f), "跳過蹲下", fs))
                { SaveReadyCalibration(false); readyStep = 2; ReadyNext(); }
            }
        }
    }
}
