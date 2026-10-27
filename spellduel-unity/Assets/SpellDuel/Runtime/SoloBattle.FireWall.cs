using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 火球牆（訓練場「每日訓練」的閃避訓練；刺客的轉職試煉仍是每 5 秒進一隻敵人）——使用者要求的版本：
    ///   一波＝一次射出 FwBalls 顆，全部從方形場地的同一面（前／右／後／左，以開始時面向為準）飛過來；
    ///   左右位置隨機、高低隨機（貼地 0.3m 跳起來躲、胸口高 1.45m 蹲下躲）、前後錯開（出發點往外退 0～FwDepth 公尺，不會同時到）。
    ///   第一波 5 顆；每 5 秒射下一波（先預警方向 1.2 秒），每一波速度 +0.1 m/s、數量 +1 顆。
    /// 技能：x_fire_low（jumpable）／x_fire_high（duckable），由 En（看不見的出題者）射出。
    /// </summary>
    public partial class SoloBattle
    {
        public const int FwBalls0 = 5;
        public const float FwSpeed0 = 2f, FwSpeedStep = 0.1f, FwWarn = 1.2f, FwEvery = 5f, FwDepth = 1.5f, FwLowY = 0.3f, FwHighY = 1.45f;
        static readonly string[] FwSideNames = { "前面", "右邊", "後面", "左邊" };

        int fwWave = -1, fwSide = -1; float fwFireAt, fwWarnUntil, fwReach;
        Vector3 fwFwd, fwCenter, fwOrigin, fwDir, fwLat; float fwHalfW;
        bool fwPending;   // 這一波預警中、還沒射
        int FwBalls => FwBalls0 + Mathf.Max(0, fwWave);
        float FwSpeed => FwSpeed0 + FwSpeedStep * Mathf.Max(0, fwWave);
        Vector3 FwSideDir(int side) => side == 0 ? fwFwd : side == 1 ? Vector3.Cross(Vector3.up, fwFwd).normalized : side == 2 ? -fwFwd : -Vector3.Cross(Vector3.up, fwFwd).normalized;

        void StartFireWall()
        {
            fwWave = -1; fwSide = -1; fwFireAt = 0f; fwWarnUntil = 0f; fwPending = false;
            fwFwd = Fighter.Flat(Me.forward); if (fwFwd == Vector3.zero) fwFwd = Vector3.forward;
            fwCenter = Me.Feet;
        }

        /// <summary>從 from 往 dir 走，場地內最遠到哪（最多 maxD）</summary>
        float FwReachIn(Vector3 from, Vector3 dir, float maxD)
        {
            float r = 0f;
            for (float d = 0.25f; d <= maxD; d += 0.25f) { var p = WorldFrame.FromWorld(from + dir * d); if (!area.Inside(p) || area.DistanceToEdge(p) < 0.3f) break; r = d; }
            return r;
        }

        /// <summary>下一波：挑一面（發射線＝那一面的邊），預警 FwWarn 秒後射</summary>
        void FwNextWave(float fireAt)
        {
            fwWave++;
            int side = dailyRng.Next(4);
            fwSide = side;
            fwDir = -FwSideDir(side);   // 火球飛的方向：從那一面往對面
            fwLat = Vector3.Cross(Vector3.up, fwDir).normalized;
            fwReach = Mathf.Max(2.5f, FwReachIn(fwCenter, FwSideDir(side), 8f));
            fwOrigin = fwCenter + FwSideDir(side) * fwReach;
            var inner = fwOrigin - FwSideDir(side) * 0.3f;
            fwHalfW = Mathf.Max(0.8f, Mathf.Min(FwReachIn(inner, fwLat, 4f), FwReachIn(inner, -fwLat, 4f)));
            fwFireAt = fireAt; fwWarnUntil = fireAt + 0.6f; fwPending = true;
            Sfx.Play("aim_warn", 0.9f); Haptics.Play(Haptics.Kind.Warning);
            floaters.Add(new Floater { text = $"⚠ 第 {fwWave + 1} 波：火球從{FwSideNames[side]}來！{FwBalls} 顆・{FwSpeed:0.0} m/s", color = new Color(1f, 0.6f, 0.4f), born = Time.time, screen = true });
        }

        /// <summary>一次射出這一波的全部火球：左右、高低隨機，出發點前後錯開</summary>
        void FwFireWave(float now)
        {
            var en = En;
            var keep = en.head;
            int n = FwBalls;
            for (int i = 0; i < n; i++)
            {
                bool low = dailyRng.NextDouble() < 0.5;
                float x = (((i + (float)dailyRng.NextDouble()) / n) * 2f - 1f) * fwHalfW;   // 左右分散（每顆一段，段內隨機）
                float back = (float)dailyRng.NextDouble() * FwDepth;                        // 前後錯開
                var from = fwOrigin + fwLat * x - fwDir * back;
                en.head = new Vector3(from.x, (low ? FwLowY : FwHighY) + 0.25f, from.z);   // 出手點＝頭的位置往下 0.25
                en.forward = fwDir;
                var sk = Skills.All[low ? "x_fire_low" : "x_fire_high"];
                en.charging = sk; en.chargeStart = now - 100f;
                int before = battle.projectiles.Count;
                battle.TryRelease(en, fwDir, from + fwDir * 10f, out _);
                for (int j = before; j < battle.projectiles.Count; j++) { battle.projectiles[j].speedMul = FwSpeed; battle.projectiles[j].hitMul = 1f; }
            }
            en.head = keep;   // 出題者回到遠處（不會被鎖定）
            fwPending = false;
            Sfx.Play("shot", 0.7f, 0.9f);
        }

        /// <summary>每幀：時間到就整波射出，下一波在 FwEvery 秒後（之前 FwWarn 秒先預警）。回傳這一幀射了幾顆</summary>
        int UpdateFireWall(float now)
        {
            if (fwWave < 0) FwNextWave(now + FwWarn);
            if (fwPending && now >= fwFireAt)
            {
                int n = FwBalls;
                FwFireWave(now);
                float next = fwFireAt + FwEvery;
                FwNextWave(next);   // 下一波：方向先預警（畫面上的大字在發射前 FwWarn 秒才出現）
                return n;
            }
            return 0;
        }

        /// <summary>HUD：發射那一面的方向箭頭；預警中畫面中央大字</summary>
        void DrawFireWallHud(float W, float H, float lh, int fsS, int fsL, float pillW)
        {
            if (fwSide < 0) return;
            DrawEdgeArrow(fwOrigin + Vector3.up * 1f, $"🔥 火球從{FwSideNames[fwSide]}來", new Color(1f, 0.55f, 0.3f), W, H, lh, fsS);
            if (fwPending && battle.now >= fwFireAt - FwWarn) UiKit.Pill(H * 0.62f, $"⚠ 第 {fwWave + 1} 波：火球從{FwSideNames[fwSide]}來！低的⬆跳、高的⬇蹲", fsL, new Color(1f, 0.6f, 0.35f), pillW);
        }
    }
}
