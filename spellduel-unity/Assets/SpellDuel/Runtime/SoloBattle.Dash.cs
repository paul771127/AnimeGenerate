using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace SpellDuel
{
    /// <summary>
    /// 衝刺（使用者要求，只有單人故事模式）：比 5（手掌張開）再握拳 → 角色以 DashSpeed 往面向的方向一直衝；
    /// 拳頭移開、或換成其他手勢 → 停下。正在詠唱技能時不能衝。
    /// AR 裡手機的位置就是角色的位置，所以「往前衝」是把整個世界（場地、場景、敵人、法術）往身後推：
    ///   世界原點（WorldFrame.Marker）往後挪；建好後就不再每幀擺位置的場景物件、場地框也一起平移。
    /// 衝刺不會衝出場地、也不會穿過敵人；打完這一場全部移回原位。
    /// 衝過之後實體房間和場地就對不上了 → 走到真實房間邊緣時另外提醒（小心撞牆）。
    /// </summary>
    public partial class SoloBattle
    {
        public const float DashSpeed = 0.8f;   // 公尺／秒
        const float DashArm = 0.8f;            // 比 5 之後這麼久內握拳才算
        const float DashGrace = 0.15f;         // 手勢偵測偶爾漏一兩張不算放開
        const float DashEdge = 0.4f;           // 離場地邊界這麼近就停
        const float DashFoeGap = 0.9f;         // 衝到敵人面前這麼近就停（不穿過去）
        const float RoomWarnDist = 0.5f;       // 離真實房間邊緣這麼近就提醒
        float dashOpenAt = -99f, dashLostAt = -1f, dashStepAt, nextRoomWarn;
        bool dashing;
        Vector3 dashTotalS;   // 到目前為止世界總共被平移多少（session 座標）
        readonly List<GameObject> dashRoots = new List<GameObject>();

        bool DashAllowed => storyStage != null && !NetMode && dungeon == null && battle != null && !battle.Over
            && phase == Phase.Fighting && !InIntro && !frozen && Me != null && Me.Alive && Me.charging == null && chantPending == null
            && !battle.Gallery && !battle.CarpetBoss && battle.clash == null && area != null && area.state == PlayArea.State.Done;

        void UpdateDash(float dt)
        {
            bool fist = Hand != null && Hand.HandVisible && Hand.Current == HandGesture.Shape.Fist;
            if (Hand != null && Hand.HandVisible && Hand.Current == HandGesture.Shape.Open) dashOpenAt = Time.time;
            if (!DashAllowed) { StopDash(); return; }
            if (!dashing)
            {
                if (fist && Time.time - dashOpenAt <= DashArm) { dashing = true; dashLostAt = -1f; dashStepAt = Time.time; Haptics.Play(Haptics.Kind.Light); }
                else return;
            }
            if (fist) dashLostAt = -1f;
            else if (dashLostAt < 0f) dashLostAt = Time.time;
            else if (Time.time - dashLostAt > DashGrace) { StopDash(); return; }
            if (!fist) return;

            var dir = Fighter.Flat(Me.forward);
            if (dir.sqrMagnitude < 1e-4f || dt <= 0f) return;
            dir.Normalize();
            var next = Me.Feet + dir * (DashSpeed * dt);
            var ns = WorldFrame.FromWorld(next);
            if (!area.Inside(ns) || area.DistanceToEdge(ns) < DashEdge) { DashBlocked("前面是場地邊界"); return; }
            foreach (var f in DashFoes())
                if (f.Alive && Battle.FlatDistance(next, f.Feet) < DashFoeGap && Battle.FlatDistance(next, f.Feet) < Battle.FlatDistance(Me.Feet, f.Feet)) { DashBlocked(null); return; }
            ShiftWorld(-WorldFrame.DirFromWorld(dir * (DashSpeed * dt)));
            Me.head += dir * (DashSpeed * dt);   // 這一幀就生效（下一幀 SyncPlayer 會用新的原點重算，結果一樣）
            if (Time.time >= dashStepAt) { dashStepAt = Time.time + 0.32f; Sfx.Play("select", 0.2f, 0.7f); shake = Mathf.Max(shake, 1.5f); }
        }

        IEnumerable<Fighter> DashFoes()
        {
            if (En != null) yield return En;
            foreach (var m in battle.minions) yield return m;
        }

        void DashBlocked(string why)
        {
            if (why != null && Time.time >= nextRoomWarn) { nextRoomWarn = Time.time + 2f; Say($"🛑 {why}，衝不過去", 1.2f); }
        }

        void StopDash() { dashing = false; dashLostAt = -1f; }

        /// <summary>整個世界平移 s（session 座標）：世界原點、場地框、場景物件（AR 相機、AR Session 不動）</summary>
        void ShiftWorld(Vector3 s)
        {
            if (s.sqrMagnitude < 1e-12f) return;
            var mk = WorldFrame.Marker;
            WorldFrame.SetMarker(new Pose(mk.position + s, mk.rotation));
            area.Shift(s);
            dashTotalS += s;
            dashRoots.Clear();
            SceneManager.GetActiveScene().GetRootGameObjects(dashRoots);
            foreach (var go in dashRoots)
            {
                if (go == null || go.GetComponent<PlayArea>() || go.GetComponent<ARSession>() || go.GetComponentInChildren<Camera>(true)) continue;
                go.transform.position += s;
            }
        }

        /// <summary>這一場打完：世界移回原位（實體房間和場地重新對上）</summary>
        void ResetDash()
        {
            StopDash(); dashOpenAt = -99f;
            if (dashTotalS.sqrMagnitude > 1e-8f && area != null) ShiftWorld(-dashTotalS);
            dashTotalS = Vector3.zero;
        }

        /// <summary>衝過之後場地和實體房間對不上：手機走到真實房間邊緣（衝刺前的場地框）就提醒</summary>
        void CheckRealRoom()
        {
            if (dashTotalS.sqrMagnitude < 0.01f || area == null || area.Polygon.Count < 3 || !cam || Time.time < nextRoomWarn) return;
            var p = cam.transform.position - dashTotalS;   // 換到衝刺前的座標，和原本的場地框比
            if (area.Inside(p) && area.DistanceToEdge(p) >= RoomWarnDist) return;
            nextRoomWarn = Time.time + 2.5f;
            Haptics.Play(Haptics.Kind.Heavy);
            Say("⚠ 小心！你快走到真實房間的邊緣了（衝刺過後，場地和房間對不上）", 2.5f);
        }

        void DrawDashHud(float W, float H)
        {
            if (!dashing || dashLostAt >= 0f) return;
            // 畫面兩側的速度線
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            for (int i = 0; i < 10; i++)
            {
                float y = H * (0.1f + 0.08f * i) + Mathf.Repeat(Time.time * 900f + i * 137f, H * 0.08f);
                float len = W * (0.08f + 0.05f * ((i * 7) % 3));
                GUI.DrawTexture(new Rect(0, y, len, 3), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(W - len, H - y, len, 3), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
    }
}
