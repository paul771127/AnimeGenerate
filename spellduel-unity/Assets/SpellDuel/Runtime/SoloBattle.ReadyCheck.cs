using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 每場戰鬥開始前的準備（單人；連線模式兩邊要同步開打，維持原本的自動校正）：
    ///   ① 站直，手機舉在眼前平常操作的高度、對著前方，穩住 1 秒 → 記下站姿高度
    ///   ② 蹲下 → 記下蹲姿高度（蹲下躲攻擊的門檻照這個算）
    ///   ③ 站起來
    ///   ④ 手掌比五、往上移 → 手勢方向校正（手在畫面上往下走＝影像上下顛倒：翻過來並鎖住）
    /// 每一步做到就打勾＋閃一下＋震動，再顯示下一步；全部完成才出現「⚔ 戰鬥開始」倒數，敵人這段時間不會動。
    /// 15 秒還蹲不下去（坐著玩、空間太矮）會出現「跳過」。
    /// </summary>
    public partial class SoloBattle
    {
        int readyStep = -1;              // -1 不用（連線、沒開打）、0～4 進行中（4＝手勢校正的結果）、5（ReadyDone）完成
        public const int ReadyDone = 5;
        /// <summary>手勢校正的結果畫面停幾秒（可以重新校正）</summary>
        public const float HandResultTime = 5f;
        string handResult;
        /// <summary>GameRoot：手勢影像上下翻過來（並鎖住）／確認方向正確（鎖住）</summary>
        public System.Action FlipHand, LockHand;
        float handAnchorY = float.NaN; int handDir1;
        /// <summary>手掌在畫面上要移動多少（畫面高度的比例）才算「往上／往下移了」</summary>
        public const float HandMoveShare = 0.15f;
        float readyHoldSince = -1f, readyStepAt, readyFlashAt = -9f, readyStand, readyMin;
        readonly List<float> readyYs = new List<float>();
        bool readyCalibrated;            // 這場已經用準備步驟校正過（戰鬥中的自動校正就不再做）
        public const float ReadyHoldTime = 1f, ReadySkipAfter = 15f;
        static readonly string[] ReadyTitles = { "📱 站直，手機舉在眼前", "⬇ 蹲下", "⬆ 站起來", "✋ 手掌比五，往上移", "✔ 手勢方向確認" };

        bool InReady => readyStep >= 0 && readyStep < ReadyDone && phase == Phase.Fighting;

        void BeginReadyCheck() { readyStep = 0; readyHoldSince = -1f; readyStepAt = Time.time; readyYs.Clear(); readyCalibrated = false; handAnchorY = float.NaN; handDir1 = 0; }

        /// <summary>重新校正手勢方向（回到「往上移」）</summary>
        void RestartHandCheck() { readyStep = 3; readyStepAt = Time.time; readyHoldSince = -1f; handAnchorY = float.NaN; handDir1 = 0; handResult = null; Sfx.Play("select", 0.6f, 1.1f); }

        /// <summary>
        /// 手勢方向校正：手掌在畫面上移動超過 HandMoveShare → 回傳方向（+1＝往上、−1＝往下；0＝還沒）。
        /// 看不到手就重新起算。
        /// </summary>
        int HandMoved()
        {
            if (Hand == null || !Hand.HandVisible) { handAnchorY = float.NaN; return 0; }
            float y = Hand.Palm.y;   // 螢幕像素（左下為原點，往上為正）
            if (float.IsNaN(handAnchorY)) { handAnchorY = y; return 0; }
            float d = y - handAnchorY, need = Screen.height * HandMoveShare;
            return d > need ? 1 : d < -need ? -1 : 0;
        }


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
                    // 鏡頭大致朝前（不往地上或天花板看），而且穩住。高度先不管：地板可能抓錯（桌面、樓下），蹲完再校正
                    bool ok = Mathf.Abs(fw.y) < 0.45f;
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
                    if (y > readyStand - 0.1f) { FixFloorHeight(); SaveReadyCalibration(true); ReadyNext(); handAnchorY = float.NaN; }
                    break;
                case 3:
                {
                    // 叫玩家往上移：畫面上手掌往上走＝方向正確；往下走＝影像上下顛倒（翻過來）
                    int m = HandMoved();
                    if (m == 0) break;
                    handDir1 = m;
                    if (m < 0) { FlipHand?.Invoke(); handResult = "🔄 畫面上下顛倒，已經翻正了"; }
                    else { LockHand?.Invoke(); handResult = "✅ 手勢方向正確"; }
                    ReadyNext();   // 結果畫面：停幾秒，可以重新校正
                    break;
                }
                case 4:
                    if (Time.time - readyStepAt >= HandResultTime) ReadyNext();
                    break;
            }
        }

        void ReadyNext()
        {
            readyStep++; readyHoldSince = -1f; readyStepAt = Time.time; readyFlashAt = Time.time;
            Haptics.Play(readyStep >= ReadyDone ? Haptics.Kind.Success : Haptics.Kind.Medium);
            Sfx.Play(readyStep >= ReadyDone ? "charged" : "select", 0.8f, 1f + 0.15f * readyStep);
            if (readyStep >= ReadyDone) { introUntil = Time.time + IntroTime; Sfx.Play("select", 0.7f, 0.8f); }
        }

        /// <summary>GameRoot：把場地地板上下移 d 公尺（正＝往上）</summary>
        public System.Action<float> RequestRelevel;
        /// <summary>站著時手機離地的合理範圍（m）；不在範圍內＝地板抓錯（桌面、椅子、樓下）</summary>
        public const float StandMin = 1.05f, StandMax = 2.05f;
        /// <summary>蹲下的幅度大約是站姿高度的 40%</summary>
        public const float CrouchShare = 0.4f;

        /// <summary>
        /// 用蹲下的幅度推算站姿的手機高度（和地板抓到哪裡無關），拿來檢查地板：
        /// 量到的站姿高度不合理 → 地板移到「站姿高度＝推算值」的位置（推算夾在 1.25～1.75m；沒蹲下用 1.5m）。
        /// 回傳地板要往上移幾公尺（負的＝往下；0＝不用改）。純計算，方便測試。
        /// </summary>
        public static float FloorCorrection(float stand, float lowest)
        {
            if (stand >= StandMin && stand <= StandMax) return 0f;
            float drop = stand - lowest;
            float est = drop >= 0.2f ? Mathf.Clamp(drop / CrouchShare, 1.25f, 1.75f) : 1.5f;
            return stand - est;
        }

        void FixFloorHeight()
        {
            float d = FloorCorrection(readyStand, readyMin);
            if (Mathf.Abs(d) < 0.01f || RequestRelevel == null) return;
            RequestRelevel(d);
            // 已經建好的場景（熔岩、森林、海上、角鬥場、山道）跟著地板一起移
            foreach (var sc in new Component[] { volcano, forest, sea, arenaScene, canyon }) if (sc) sc.transform.position += Vector3.up * d;
            readyStand -= d; readyMin -= d; standY -= d; lastHeadY = float.NaN;
            Say($"📏 地板高度校正了（{(d > 0 ? "往上" : "往下")} {Mathf.Abs(d) * 100f:0} 公分）", 2.5f);
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
            if (!InReady && !(readyStep >= ReadyDone && Time.time - readyFlashAt < 0.6f && phase == Phase.Fighting)) return;
            float W = Screen.width, H = Screen.height; int fs = UiKit.BaseFont, fsL = Mathf.RoundToInt(fs * 1.5f);
            float since = Time.time - readyFlashAt;
            if (since < 0.45f) EdgeFlash(new Color(0.45f, 1f, 0.5f), 1f - since / 0.45f);   // 做到了：畫面四邊亮一下
            if (readyStep >= ReadyDone) return;   // 完成：接著是「⚔ 戰鬥開始」倒數（DrawIntroOutro）
            var r = new Rect(W * 0.06f, H * 0.14f, W * 0.88f, fs * 19.5f);
            UiKit.Card(r, 0.88f, UiKit.Gold, 0.8f);
            float x = r.x + fs, w = r.width - fs * 2, y = r.y + fs * 0.6f;
            UiKit.Text(new Rect(x, y, w, fs * 1.8f), readyStep >= 3 ? "戰鬥準備：手勢方向校正" : "戰鬥準備：身高校正", Mathf.RoundToInt(fs * 1.2f), UiKit.Gold, TextAnchor.MiddleCenter, true);
            y += fs * 2.2f;
            for (int i = 0; i < ReadyDone; i++)
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
                2 => "站起來，回到剛才的高度",
                3 => Hand == null || !Hand.HandVisible ? "把手伸到鏡頭前面、手掌比五（張開），讓畫面看得到手" : "很好，看到手了！手掌慢慢往上移",
                _ => $"{handResult}　（{Mathf.Max(0f, HandResultTime - (Time.time - readyStepAt)):0} 秒後開始）",
            };
            UiKit.Text(new Rect(x, y, w, fs * 2.4f), tip, fs, new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleCenter);
            y += fs * 2.6f;
            if (readyStep == 4)
            {
                // 手勢校正的結果：直接開始，或重新校正
                float bw2 = (w - fs) / 2f;
                if (UiKit.Button(new Rect(x, y, bw2, fs * 2f), "⚔ 開始", fs, true, UiKit.Gold)) ReadyNext();
                if (UiKit.Button(new Rect(x + bw2 + fs, y, bw2, fs * 2f), "🔄 重新校正", fs)) RestartHandCheck();
            }
            else if (readyStep >= 3 && Time.time - readyStepAt > ReadySkipAfter)
            {
                // 偵測不到手（光線太暗等）：可以跳過手勢校正（照原本的自動判斷）
                if (UiKit.Button(new Rect(r.center.x - fs * 6f, y, fs * 12f, fs * 2f), "跳過手勢校正", fs))
                { readyStep = ReadyDone - 1; ReadyNext(); }
            }
            else if (readyStep >= 1 && readyStep < 3 && Time.time - readyStepAt > ReadySkipAfter)
            {
                // 坐著玩、空間太矮：可以跳過蹲姿（用預設的蹲姿比例）
                if (UiKit.Button(new Rect(r.center.x - fs * 5f, y, fs * 10f, fs * 2f), "跳過蹲下", fs))
                { readyMin = readyStand; FixFloorHeight(); SaveReadyCalibration(false); readyStep = 2; ReadyNext(); }
            }
        }
    }
}
