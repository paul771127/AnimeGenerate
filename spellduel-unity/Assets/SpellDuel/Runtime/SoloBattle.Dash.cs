using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace SpellDuel
{
    /// <summary>
    /// 衝刺（使用者要求，只有單人故事模式）：比讚 👍（大拇指朝上）→ 角色以 DashSpeed 往面向的方向一直衝；
    /// 拳頭移開、或換成其他手勢 → 停下。正在詠唱技能時不能衝。
    /// AR 裡手機的位置就是角色的位置，所以「往前衝」是把整個世界（場地、場景、敵人、法術）往身後推：
    ///   世界原點（WorldFrame.Marker）往後挪；建好後就不再每幀擺位置的場景物件、場地框也一起平移。
    /// 衝刺不會衝出場地、也不會穿過敵人；打完這一場全部移回原位。
    /// 衝過之後實體房間和場地就對不上了 → 走到真實房間邊緣時另外提醒（小心撞牆）。
    /// </summary>
    public partial class SoloBattle
    {
        public const float DashSpeed = 2.5f;   // 公尺／秒（試過 3.5，使用者覺得太快，改回 2.5）
        /// <summary>故事探索的衝刺：1.5 倍（使用者要求；地圖一格改 4m、路線變長）</summary>
        public const float ExploreDashMul = 1.5f;
        float DashSpeedNow => exploreRun != null ? DashSpeed * ExploreDashMul : DashSpeed;
        const float DashGrace = 0.15f;         // 手勢偵測偶爾漏一兩張不算放開
        // 和普攻分開（使用者回報以前的「比 5→握拳」很容易干涉：獵人握拳→張開放箭、劍士手掌橫劃；現在改比讚）：
        const float DashThumbHold = 0.15f;     // 讚要比著這麼久才開始衝（手勢一閃而過不算）
        const float DashAfterBasic = 0.8f;     // 剛出過普攻（或做了普攻動作）這麼久內不會開始衝刺
        const float BasicAfterDash = 0.5f;     // 衝刺中、剛停下這麼久內不會打普攻
        float dashOpenSince = -99f, lastBasicAt = -99f, dashEndedAt = -99f; bool dashWasOpen;
        /// <summary>現在不打普攻（衝刺中、剛停下）</summary>
        bool BasicBlockedByDash => dashing || Time.time - dashEndedAt < BasicAfterDash;
        const float DashEdge = 0.4f;           // 離場地邊界這麼近就停
        const float DashFoeGap = 0.9f;         // 衝到敵人面前這麼近就停（不穿過去）
        const float RoomWarnDist = 0.5f;       // 離真實房間邊緣這麼近就提醒
        float dashOpenAt = -99f, dashLostAt = -1f, dashStepAt, nextRoomWarn;
        bool dashing, dashStuck;
        float dashMovedAt = -99f, dashStartAt = -99f, dashStuckAt = -99f;
        float dashDistance;   // 這一場總共衝了幾公尺（新手教學用）
        Vector3 dashTotalS;   // 到目前為止世界總共被平移多少（session 座標）
        Vector3 dashMoveW;    // 衝刺讓角色在場地上前進了多少（場地座標）
        /// <summary>扣掉衝刺之後「人真的走動」的位置：側跨閃避、運動紀錄用（衝刺不算側跨，也不算走路）</summary>
        Vector3 PhysFeet(Fighter f) => f == Me ? f.Feet - dashMoveW : f.Feet;
        int dashAreaRev = -1; // 開始平移時場地的版本：之後場地重畫／重新放置（新場地就是實體房間）→ 累積的平移作廢
        readonly List<GameObject> dashRoots = new List<GameObject>();
        readonly Dictionary<GameObject, bool> dashRootMove = new Dictionary<GameObject, bool>();

        bool DashAllowed => (storyStage != null || exploreRun != null) && !NetMode && dungeon == null && battle != null && !battle.Over
            && phase == Phase.Fighting && !InIntro && !frozen && Me != null && Me.Alive && Me.charging == null && chantPending == null
            && !battle.Gallery && !battle.CarpetBoss && battle.clash == null && area != null && area.state == PlayArea.State.Done;

        void UpdateDash(float dt)
        {
            // 比讚 👍 就衝、放下就停（使用者選的：以前的「比 5→握拳」和普攻的手掌／握拳容易互相干涉）
            bool thumb = Hand != null && Hand.HandVisible && Hand.ThumbUp;
            if (thumb) { if (!dashWasOpen) dashOpenSince = Time.time; dashOpenAt = Time.time; }
            dashWasOpen = thumb;
            if (!DashAllowed) { StopDash(); return; }
            if (!dashing)
            {
                bool armed = thumb && Time.time - dashOpenSince >= DashThumbHold && Time.time - lastBasicAt > DashAfterBasic;
                if (armed) { dashing = true; dashLostAt = -1f; dashStepAt = Time.time; dashStartAt = Time.time; Haptics.Play(Haptics.Kind.Light); if (!BtFlag("dash")) BtMark("dash"); }
                else return;
            }
            if (thumb) dashLostAt = -1f;
            else if (dashLostAt < 0f) dashLostAt = Time.time;
            else if (Time.time - dashLostAt > DashGrace) { StopDash(); return; }
            if (!thumb) return;

            var dir = Fighter.Flat(Me.forward);
            if (dir.sqrMagnitude < 1e-4f || dt <= 0f) return;
            dir.Normalize();
            // 這一步實際能走多遠：碰到場地邊界或敵人就只走到那裡（可能是 0＝卡住）。
            // 世界只照「實際走的距離」平移：卡住時場地、景物、敵人都不會動。
            float want = DashSpeedNow * dt;
            float step = DashStepAllowed(dir, want, out string why);
            if (exploreRun != null && step < want * 0.6f) ExSlide(ref dir, ref step, want);   // 故事探索：擦到牆角、樹就沿著邊滑過去，不要整個停住
            if (step < 1e-4f)
            {
                // 撞到邊界／牆（使用者要求）：衝刺真的停下來，不要維持衝刺動作卻不動。放下手、轉個方向再比讚就能再衝
                if (!dashStuck) dashStuckAt = Time.time;
                dashStuck = true; DashBlocked(why);
                if (Time.time - dashStuckAt > 0.25f)
                {
                    StopDash(); dashOpenAt = -99f; dashOpenSince = Time.time + 999f;   // 要放下手再比一次讚
                    Haptics.Play(Haptics.Kind.Light);
                    Say($"🛑 {dashWhy ?? "前面過不去"}：衝刺停下來了。放下手、轉向能走的方向，再比讚 👍", 2.5f);
                }
                return;
            }
            dashStuck = false;
            var move = dir * step;
            ShiftWorld(-WorldFrame.DirFromWorld(move));
            Me.head += move; dashMoveW += move; dashDistance += step; dashMovedAt = Time.time;   // 這一幀就生效（下一幀 SyncPlayer 會用新的原點重算，結果一樣）
            if (Time.time >= dashStepAt) { dashStepAt = Time.time + 0.32f; Sfx.Play("select", 0.2f, 0.7f); shake = Mathf.Max(shake, 1.5f); }
        }

        /// <summary>
        /// 往 dir 最多能衝多遠（≤ want）：分小段檢查，停在第一個不行的位置前。
        /// 不行＝出了場地、或離邊界不到 DashEdge 而且越來越近（貼著邊界時往場內衝可以）、或離敵人不到 DashFoeGap 而且越來越近。
        /// </summary>
        float DashStepAllowed(Vector3 dir, float want, out string why)
        {
            why = null;
            const int N = 4;
            float ok = 0f;
            var from = Me.Feet;
            float edge0 = area.DistanceToEdge(WorldFrame.FromWorld(from));
            for (int i = 1; i <= N; i++)
            {
                float d = want * i / N;
                var p = from + dir * d; var ps = WorldFrame.FromWorld(p);
                if (exploreRun != null)
                {
                    // 故事探索：沒有場地邊界，碰到迷宮的牆（或還沒打開的藤蔓門）才停
                    if (!exploreRun.map.Walkable(p, 0.35f)) { var ahead = from + dir * (d + 0.6f); why = exploreRun.map.ClosedSealAt(ahead) != null ? "封印還沒解開" : exploreRun.map.ClosedGateAt(ahead) != null ? "藤蔓門還沒打開" : "前面過不去"; break; }
                }
                else
                {
                    float edge = area.DistanceToEdge(ps);
                    if (!area.Inside(ps) || edge < DashEdge && edge < edge0) { why = "前面是場地邊界"; break; }
                }
                bool foe = false;
                foreach (var f in DashFoes())
                {
                    if (!f.Alive) continue;
                    float fd = Battle.FlatDistance(p, f.Feet);
                    if (fd < DashFoeGap && fd < Battle.FlatDistance(from, f.Feet)) { foe = true; break; }
                }
                if (foe) break;
                ok = d;
            }
            return ok;
        }

        /// <summary>
        /// 故事探索（使用者回報經過樹或障礙物就卡住）：正前方過不去時，試著往左右偏 25°～80° 滑過去（速度照偏的角度打折），
        /// 挑走得最遠的方向；真的是死路（正面撞牆）才停。
        /// </summary>
        void ExSlide(ref Vector3 dir, ref float step, float want)
        {
            var bestDir = dir; float best = step;
            foreach (float ang in new[] { 25f, -25f, 45f, -45f, 65f, -65f, 80f, -80f })
            {
                var d = Quaternion.Euler(0f, ang, 0f) * dir;
                float s = DashStepAllowed(d, want, out _) * Mathf.Cos(ang * Mathf.Deg2Rad);
                if (s > best + 1e-4f) { best = s; bestDir = d; }
            }
            if (best <= step) return;
            dir = bestDir; step = best;
        }

        IEnumerable<Fighter> DashFoes()
        {
            if (En != null) yield return En;
            foreach (var m in battle.minions) if (!m.hidden) yield return m;   // 埋在地下的食人花看不到，不擋路
        }

        string dashWhy;
        void DashBlocked(string why) => dashWhy = why ?? "敵人擋在前面";

        /// <summary>衝刺的手勢做到哪：0 還沒、1 比了讚（等一下就衝）、2 衝刺中</summary>
        int DashGestureStage => dashing && dashLostAt < 0f ? 2 : dashWasOpen ? 1 : 0;

        void StopDash() { if (dashing) dashEndedAt = Time.time; dashing = false; dashStuck = false; dashLostAt = -1f; }

        /// <summary>整個世界平移 s（session 座標）：世界原點、場地框、場景物件（AR 相機、AR Session 不動）</summary>
        void ShiftWorld(Vector3 s)
        {
            if (s.sqrMagnitude < 1e-12f) return;
            DashCheckArea();
            if (dashTotalS == Vector3.zero) dashAreaRev = area.Revision;
            var mk = WorldFrame.Marker;
            WorldFrame.SetMarker(new Pose(mk.position + s, mk.rotation));
            area.Shift(s);
            dashTotalS += s;
            dashRoots.Clear();
            SceneManager.GetActiveScene().GetRootGameObjects(dashRoots);
            foreach (var go in dashRoots)
            {
                if (go == null) continue;
                // 要不要跟著移（場地框、AR Session、裝著相機的 XR Origin 不動）：每個根物件只判斷一次（整棵找相機很慢）
                if (!dashRootMove.TryGetValue(go, out bool mv)) dashRootMove[go] = mv = !go.GetComponent<PlayArea>() && !go.GetComponent<ARSession>() && !go.GetComponentInChildren<Camera>(true);
                if (mv) go.transform.position += s;
            }
            // 平移 transform 搬不動的東西：世界座標的線（刀光、射擊預警線…）、拖尾、世界空間的粒子（已經噴出去的）
            // 每幀重算位置的線會被它自己蓋掉，結果一樣正確
            foreach (var lr in FindObjectsOfType<LineRenderer>())
            {
                if (!lr.useWorldSpace || DashSkip(lr.transform)) continue;
                int n = lr.positionCount; if (n == 0) continue;
                if (dashPts.Length < n) dashPts = new Vector3[n * 2];
                lr.GetPositions(dashPts);
                for (int i = 0; i < n; i++) dashPts[i] += s;
                lr.SetPositions(dashPts);
            }
            foreach (var tr in FindObjectsOfType<TrailRenderer>())
            {
                if (DashSkip(tr.transform)) continue;
                int n = tr.positionCount; if (n == 0) continue;
                if (dashPts.Length < n) dashPts = new Vector3[n * 2];
                tr.GetPositions(dashPts);
                for (int i = 0; i < n; i++) dashPts[i] += s;
                tr.SetPositions(dashPts);
            }
            foreach (var ps in FindObjectsOfType<ParticleSystem>())
            {
                if (ps.main.simulationSpace != ParticleSystemSimulationSpace.World || DashSkip(ps.transform)) continue;
                if (dashParts.Length < ps.main.maxParticles) dashParts = new ParticleSystem.Particle[ps.main.maxParticles];
                int n = ps.GetParticles(dashParts); if (n == 0) continue;
                for (int i = 0; i < n; i++) dashParts[i].position += s;
                ps.SetParticles(dashParts, n);
            }
        }
        Vector3[] dashPts = new Vector3[64];
        ParticleSystem.Particle[] dashParts = new ParticleSystem.Particle[256];
        /// <summary>不跟著世界走的：場地框（PlayArea.Shift 自己搬）、掛在相機上的</summary>
        static bool DashSkip(Transform t) => t.GetComponentInParent<PlayArea>() || t.GetComponentInParent<Camera>();

        /// <summary>場地在衝刺後被重畫／重新放置：新的場地就是實體房間，之前累積的平移不用再移回去</summary>
        void DashCheckArea()
        {
            if (dashTotalS != Vector3.zero && area.Revision != dashAreaRev) { dashTotalS = Vector3.zero; dashMoveW = Vector3.zero; moveHist.Clear(); exHasFeet = false; }
        }

        /// <summary>這一場打完：世界移回原位（實體房間和場地重新對上）</summary>
        void ResetDash()
        {
            StopDash(); dashOpenAt = -99f; dashDistance = 0f;
            if (area != null) DashCheckArea();
            if (dashTotalS.sqrMagnitude > 1e-8f && area != null) ShiftWorld(-dashTotalS);
            dashTotalS = Vector3.zero; dashMoveW = Vector3.zero; dashRootMove.Clear();
        }

        /// <summary>衝過之後場地和實體房間對不上：手機走到真實房間邊緣（衝刺前的場地框）就提醒</summary>
        void CheckRealRoom()
        {
            if (area != null) DashCheckArea();
            if (exploreRun != null) return;   // 故事探索：沒有場地框，不提醒（使用者要求；高度檢查在 PlayArea 照常）
            if (dashTotalS.sqrMagnitude < 0.01f || area == null || area.Polygon.Count < 3 || !cam || Time.time < nextRoomWarn) return;
            // 場地框被平移了 dashTotalS：原本的房間＝現在的框 − dashTotalS → 手機在原本的房間裡 ⇔ 手機＋dashTotalS 在現在的框裡
            var p = cam.transform.position + dashTotalS;
            if (area.Inside(p) && area.DistanceToEdge(p) >= RoomWarnDist) return;
            nextRoomWarn = Time.time + 2.5f;
            Haptics.Play(Haptics.Kind.Heavy);
            Say("⚠ 小心！你快走到真實房間的邊緣了（衝刺過後，場地和房間對不上）", 2.5f);
        }

        void DrawDashHud(float W, float H)
        {
            if (!dashing || dashLostAt >= 0f || !DashAllowed) return;   // 暫停、追蹤中斷、打完：不顯示
            // 卡住（邊界、敵人擋著）：不畫速度線，只提示衝不過去
            if (dashStuck || Time.time - dashMovedAt > 0.15f) { UiKit.Pill(H * 0.62f, $"🛑 {dashWhy ?? "前面"}，衝不過去（鬆開拳頭）", Mathf.RoundToInt(H * 0.022f), new Color(1f, 0.6f, 0.45f), W * 0.5f); return; }
            // 第一人稱的速度線（使用者要求更明顯）：從畫面四周往中心收的放射線，往外流動；四周加暗角；中間留空不擋視線
            float k = Mathf.Clamp01((Time.time - dashStartAt) / 0.2f);   // 剛開始衝的 0.2 秒淡入
            var c = new Vector2(W * 0.5f, H * 0.5f);
            GUI.color = new Color(0f, 0f, 0f, 0.5f * k);
            GUI.DrawTexture(new Rect(0, 0, W, H), DashVignette);
            var keep = GUI.matrix;
            const int N = 48;
            for (int i = 0; i < N; i++)
            {
                float h = Mathf.Repeat(i * 0.6180339f, 1f);                      // 每條線固定的隨機值
                float ang = (i + h * 0.6f) * 360f / N, rad = ang * Mathf.Deg2Rad;
                // 這個方向從中心到螢幕邊緣的距離：直向、橫向畫面都一樣看得到四周的線
                float edge = Mathf.Min(c.x / Mathf.Max(0.001f, Mathf.Abs(Mathf.Cos(rad))), c.y / Mathf.Max(0.001f, Mathf.Abs(Mathf.Sin(rad))));
                float t = Mathf.Repeat(Time.time * (2.4f + h * 1.6f) + h * 7f, 1f);   // 0→1：從內往外流
                float r0 = edge * (0.4f + 0.45f * t), len = edge * (0.3f + 0.3f * h);
                float thick = Mathf.Min(W, H) * (0.008f + 0.012f * Mathf.Repeat(h * 3.7f, 1f));
                float a = k * (0.65f + 0.35f * h) * Mathf.Clamp01(t * 5f);          // 剛冒出來時淡入
                GUI.matrix = keep; GUIUtility.RotateAroundPivot(ang, c);
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(new Rect(c.x + r0, c.y - thick * 0.5f, len, thick), DashStreak);
            }
            GUI.matrix = keep;
            GUI.color = Color.white;
        }

        static Texture2D dashStreak, dashVignette;
        /// <summary>速度線：靠中心那端細、透明，往外變粗變亮（左→右）</summary>
        static Texture2D DashStreak
        {
            get
            {
                if (dashStreak) return dashStreak;
                const int w = 64, h = 8; dashStreak = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
                {
                    float u = x / (w - 1f), half = 0.15f + 0.85f * u, v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    dashStreak.SetPixel(x, y, new Color(1f, 1f, 1f, u * u * Mathf.Clamp01((half - v) * 4f)));
                }
                dashStreak.Apply(); return dashStreak;
            }
        }
        /// <summary>暗角：中間透明、四周變暗</summary>
        static Texture2D DashVignette
        {
            get
            {
                if (dashVignette) return dashVignette;
                const int n = 64; dashVignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < n; x++) for (int y = 0; y < n; y++)
                {
                    float d = new Vector2(x / (n - 1f) - 0.5f, y / (n - 1f) - 0.5f).magnitude * 2f;
                    dashVignette.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((d - 0.55f) / 0.6f)));
                }
                dashVignette.Apply(); return dashVignette;
            }
        }
    }
}
