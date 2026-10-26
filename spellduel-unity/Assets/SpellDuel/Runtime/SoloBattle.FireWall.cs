using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 火球牆（訓練場的每日挑戰・閃避）：
    ///   火球一次只從場地的一面（前／右／後／左，以開始時面向為準）飛過來，一面 10 顆：前後錯開 0.25 秒、左右位置隨機、
    ///   高低隨機——貼地的（0.3m）跳起來躲、胸口高的（1.45m）蹲下躲（側身走開也行）。
    ///   一面的火球都飛過中線後馬上換另一面（不停頓），先預警方向 1.2 秒再射；每換一面速度 +0.1 m/s、數量 +1 顆。
    /// 技能：x_fire_low（jumpable）／x_fire_high（duckable），由 En（看不見的出題者）射出。
    /// </summary>
    public partial class SoloBattle
    {
        public const int FwBalls0 = 10;
        public const float FwSpeed0 = 2f, FwSpeedStep = 0.1f, FwGap = 0.25f, FwWarn = 1.2f, FwLowY = 0.3f, FwHighY = 1.45f;
        static readonly string[] FwSideNames = { "前面", "右邊", "後面", "左邊" };

        int fwWave = -1, fwSide = -1, fwFired; float fwNextAt, fwWarnUntil, fwLastFireAt, fwReach;
        Vector3 fwFwd, fwCenter, fwOrigin, fwDir, fwLat; float fwHalfW;
        bool fwWarning;   // 預警中（下一面還沒開始射）
        int FwBalls => FwBalls0 + Mathf.Max(0, fwWave);
        float FwSpeed => FwSpeed0 + FwSpeedStep * Mathf.Max(0, fwWave);
        Vector3 FwSideDir(int side) => side == 0 ? fwFwd : side == 1 ? Vector3.Cross(Vector3.up, fwFwd).normalized : side == 2 ? -fwFwd : -Vector3.Cross(Vector3.up, fwFwd).normalized;

        void StartFireWall()
        {
            fwWave = -1; fwSide = -1; fwFired = 0; fwNextAt = 0f; fwWarnUntil = 0f; fwWarning = false;
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

        /// <summary>換一面：發射線＝那一面的邊（離起點最遠處），先預警</summary>
        void FwNextSide(float now)
        {
            fwWave++;
            int side; do side = dailyRng.Next(4); while (side == fwSide && fwWave > 0);
            fwSide = side;
            fwDir = -FwSideDir(side);   // 火球飛的方向：從那一面往對面
            fwLat = Vector3.Cross(Vector3.up, fwDir).normalized;
            fwReach = Mathf.Max(2.5f, FwReachIn(fwCenter, FwSideDir(side), 8f));
            fwOrigin = fwCenter + FwSideDir(side) * fwReach;
            var inner = fwOrigin - FwSideDir(side) * 0.3f;
            fwHalfW = Mathf.Max(0.8f, Mathf.Min(FwReachIn(inner, fwLat, 4f), FwReachIn(inner, -fwLat, 4f)));
            fwFired = 0; fwNextAt = now + FwWarn; fwWarnUntil = now + FwWarn + 0.6f; fwWarning = true;
            Sfx.Play("aim_warn", 0.9f); Haptics.Play(Haptics.Kind.Warning);
            floaters.Add(new Floater { text = $"⚠ 第 {fwWave + 1} 面：火球從{FwSideNames[side]}來！{FwBalls} 顆・{FwSpeed:0.0} m/s", color = new Color(1f, 0.6f, 0.4f), born = Time.time, screen = true });
        }

        void FwFire(float now)
        {
            var en = En;
            bool low = dailyRng.NextDouble() < 0.5;
            float x = ((float)dailyRng.NextDouble() * 2f - 1f) * fwHalfW;
            var from = fwOrigin + fwLat * x;
            var keep = en.head;
            en.head = new Vector3(from.x, (low ? FwLowY : FwHighY) + 0.25f, from.z);   // 出手點＝頭的位置往下 0.25
            en.forward = fwDir;
            var sk = Skills.All[low ? "x_fire_low" : "x_fire_high"];
            en.charging = sk; en.chargeStart = now - 100f;
            int before = battle.projectiles.Count;
            battle.TryRelease(en, fwDir, from + fwDir * 10f, out _);
            for (int j = before; j < battle.projectiles.Count; j++) { battle.projectiles[j].speedMul = FwSpeed; battle.projectiles[j].hitMul = 1f; }
            en.head = keep;   // 出題者回到遠處（不會被鎖定）
            fwFired++; fwLastFireAt = now; fwWarning = false;
            Sfx.Play("shot", 0.5f, low ? 0.8f : 1.1f);
        }

        /// <summary>每幀：這一面每 FwGap 秒一顆；最後一顆飛過中線就換下一面（先預警，不停頓）。回傳這一幀射了幾顆</summary>
        int UpdateFireWall(float now)
        {
            int fired = 0;
            if (fwWave < 0) FwNextSide(now);
            while (fwFired < FwBalls && now >= fwNextAt) { FwFire(now); fired++; fwNextAt += FwGap; }
            if (fwFired >= FwBalls && now >= fwLastFireAt + fwReach / FwSpeed) FwNextSide(now);
            return fired;
        }

        /// <summary>HUD：發射那一面的方向箭頭；預警中畫面中央大字</summary>
        void DrawFireWallHud(float W, float H, float lh, int fsS, int fsL, float pillW)
        {
            if (fwSide < 0) return;
            DrawEdgeArrow(fwOrigin + Vector3.up * 1f, $"🔥 火球從{FwSideNames[fwSide]}來", new Color(1f, 0.55f, 0.3f), W, H, lh, fsS);
            if (battle.now < fwWarnUntil) UiKit.Pill(H * 0.62f, $"⚠ 第 {fwWave + 1} 面：火球從{FwSideNames[fwSide]}來！低的⬆跳、高的⬇蹲", fsL, new Color(1f, 0.6f, 0.35f), pillW);
        }
    }
}
