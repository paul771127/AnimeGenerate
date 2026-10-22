using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 可以走動的 3D 城鎮（故事模式進城時）：
    ///   走路：上下晃動手機（像原地踏步）＝往螢幕準心的方向前進；真的走路也會動（AR）。
    ///   聊天：走到居民面前（約 2.5m 內、在畫面前方）說「嗨」「你好」之類的招呼語，就會開始對話；
    ///         設施的店員打完招呼後打開鐵匠鋪／道具店／職業工會。
    /// 外觀全部在 TownArt（美術負責）。座標：城鎮根物件放在進城時的腳下，面向進城時的鏡頭方向；
    /// 往前走＝把整個城鎮往後移（玩家實際不用移動）。
    /// </summary>
    public class TownWalk
    {
        public class Npc
        {
            public string name, role; public string[] lines;
            public string opens;          // 打完招呼後打開的設施（smith／shop／guild）；null＝一般居民
            public Vector3 pos; public float yaw;
            public Transform tf;
            public int lineAt;            // 下次聊天從第幾句開始
        }

        /// <summary>城鎮出口（柵欄的缺口）：走出去打開這一章的關卡選單</summary>
        public class Exit { public Vector2 dir; public int theme; public Vector3 pos; }
        public readonly List<Exit> exits = new List<Exit>();
        public Vector3 WarpPos { get; private set; }

        public bool Active => root != null;
        public int Town { get; private set; } = -1;
        public readonly List<Npc> npcs = new List<Npc>();
        public Npc Near { get; private set; }            // 可以打招呼的居民
        public bool Walking => speed > 0.15f;
        public string LastGreet { get; private set; } = ""; public float LastGreetAt = -9f;

        /// <summary>打招呼成功（SoloBattle 打開對話框）</summary>
        public Action<Npc> OnGreet;
        /// <summary>走出出口（參數＝那一章的主題 0～2）</summary>
        public Action<int> OnExit;
        /// <summary>踏上傳送陣</summary>
        public Action OnWarp;
        bool onWarp; float warpEnterAt = -1f;
        /// <summary>要在傳送陣上站多久才會傳送（秒）</summary>
        public const float WarpStay = 0.5f;
        /// <summary>站上傳送陣的進度（0～1；畫面可以畫倒數）</summary>
        public float WarpProgress => warpEnterAt < 0f ? 0f : Mathf.Clamp01((Time.time - warpEnterAt) / WarpStay);

        GameObject root;
        readonly List<(Vector2 c, float r)> walls = new List<(Vector2, float)>();
        readonly StepDetector steps = new StepDetector();
        float speed, walkUntil, holdUntil, greetLock;
        public const float GateHalf = 1.4f, ExitDepth = 1.2f;
        /// <summary>居民的障礙半徑：太近時 AR 角色看起來超大，保持約 1.5m 的距離</summary>
        public const float NpcKeepOff = 1.2f;
        public const float TownRadius = 13f, StepSpeed = 1.4f, StepHold = 0.5f, TalkRange = 2.6f, TalkAngle = 50f;
        static readonly Vector2 PlazaC = new Vector2(0f, 5f);
        /// <summary>村子設施的位置：副本入口、訓練場在廣場兩側（原本的民房），討伐告示板、觀戰席在入口兩邊</summary>
        static readonly (string kind, Vector2 at)[] FacilitySpots =
        {
            ("dungeon", new Vector2(-9.5f, 1.5f)), ("training", new Vector2(9.5f, 1.5f)),
            ("bounty", new Vector2(-5.5f, -5f)), ("watch", new Vector2(5.5f, -5f)),
        };

        // ---------------------------------------------------------------- 進城／離開
        public void Enter(int town, Camera cam, float floorY)
        {
            Leave();
            Town = town;
            root = new GameObject($"Town {WorldUI.Towns[town].name}");
            var f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            root.transform.SetPositionAndRotation(new Vector3(cam.transform.position.x, floorY, cam.transform.position.z), Quaternion.LookRotation(f.normalized, Vector3.up));
            TownArt.BuildScenery(town, root.transform);
            walls.Clear();
            // 設施＋地標＋民房（門朝廣場）
            var t = WorldUI.Towns[town];
            Building("smith", new Vector2(-7f, 8f), 2.1f);
            Building("shop", new Vector2(7f, 8f), 2.1f);
            Building("guild", new Vector2(0f, 13.5f), 2.9f);
            // 村子的其他設施（原本的兩間民房改成副本入口、訓練場；入口附近再加討伐告示板、觀戰席）
            foreach (var (kind, at) in FacilitySpots) Building(kind, at, kind == "bounty" || kind == "watch" ? 1.5f : 2.1f);
            Building("landmark", town == 2 ? new Vector2(8f, 12f) : new Vector2(-8f, 12f), 1.4f);
            // 柵欄＋出口：東邊往這一章（主題＝城鎮），西邊回上一章
            exits.Clear();
            if (town > 0) exits.Add(new Exit { dir = Vector2.left, theme = town - 1 });
            if (HasStages(town)) exits.Add(new Exit { dir = Vector2.right, theme = town });
            var gaps = new List<Vector2>();
            foreach (var e in exits)
            {
                gaps.Add(e.dir);
                var gp = PlazaC + e.dir * TownRadius;
                e.pos = new Vector3(gp.x, 0f, gp.y);
                var g = new GameObject("Gate " + e.theme);
                g.transform.SetParent(root.transform, false);
                g.transform.localPosition = e.pos;
                g.transform.localRotation = Quaternion.Euler(0f, Yaw(e.dir), 0f);   // +z 朝外
                TownArt.BuildGate(town, e.theme, g.transform);
            }
            TownArt.BuildFence(town, root.transform, new Vector3(PlazaC.x, 0f, PlazaC.y), TownRadius, gaps, GateHalf);
            // 傳送陣（廣場東南角）
            WarpPos = new Vector3(5f, 0f, 0.5f);
            var w = new GameObject("Warp Circle");
            w.transform.SetParent(root.transform, false);
            w.transform.localPosition = WarpPos;
            TownArt.BuildWarpPad(town, w.transform);
            onWarp = false;
            npcs.Clear();
            Keeper("鐵匠", "鐵匠鋪老闆", "smith", new Vector2(-7f, 8f), new[] { "要強化裝備嗎？把東西放上鐵砧吧。" });
            Keeper("店員", "道具店店員", "shop", new Vector2(7f, 8f), new[] { "歡迎光臨！藥水、回城捲軸都有，也收裝備喔。" });
            Keeper("公會長", "職業工會", "guild", new Vector2(0f, 13.5f), new[] { "歡迎來到職業工會。轉職試煉與進階職業，都在這裡辦。" });
            foreach (var (kind, at) in FacilitySpots)
                foreach (var fa in WorldUI.Facilities)
                    if (fa.kind == kind) Keeper(fa.npc, fa.role, kind, at, new[] { fa.hello });
            var spots = new[] { new Vector2(-3.2f, 3.6f), new Vector2(3.4f, 4.2f), new Vector2(-1.2f, 8.4f) };
            for (int i = 0; i < t.npcs.Length; i++)
            {
                var (name, role, lines) = t.npcs[i];
                var p = spots[i % spots.Length];
                AddNpc(new Npc { name = name, role = role, lines = lines, pos = new Vector3(p.x, 0f, p.y), yaw = Yaw(PlazaC + new Vector2(0f, -3f) - p) });
            }
            speed = 0f; Near = null; greetLock = 0f;
            steps.Reset();
        }

        public void Leave()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null; Town = -1; npcs.Clear(); walls.Clear(); exits.Clear(); Near = null;
        }

        static bool HasStages(int theme) { foreach (var st in Story.AllStages()) if (st.theme == theme) return true; return false; }

        public void SetVisible(bool v) { if (root != null && root.activeSelf != v) root.SetActive(v); }

        static float Yaw(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        void Building(string kind, Vector2 at, float radius)
        {
            var go = new GameObject("Building " + kind);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(at.x, 0f, at.y);
            // 門（-z）朝廣場
            go.transform.localRotation = Quaternion.Euler(0f, Yaw(at - PlazaC), 0f);
            TownArt.BuildBuilding(Town, kind, go.transform);
            walls.Add((at, radius));
        }

        void Keeper(string name, string role, string opens, Vector2 building, string[] lines)
        {
            var toPlaza = (PlazaC - building).normalized;
            var p = building + toPlaza * 3.1f;
            AddNpc(new Npc { name = name, role = role, lines = lines, opens = opens, pos = new Vector3(p.x, 0f, p.y), yaw = Yaw(toPlaza) });
        }

        void AddNpc(Npc n)
        {
            var go = TownArt.CreateNpc(n.name, root.transform);
            BlobShadow.Attach(go.transform, 0.3f, 0.24f);   // 腳下的接觸陰影
            n.tf = go.transform;
            n.tf.localPosition = n.pos; n.tf.localRotation = Quaternion.Euler(0f, n.yaw, 0f);
            npcs.Add(n);
            walls.Add((new Vector2(n.pos.x, n.pos.z), NpcKeepOff));
        }

        // ---------------------------------------------------------------- 每幀
        /// <param name="holdWalk">按住「前進」鈕</param>
        public void Update(Camera cam, float dt, bool holdWalk)
        {
            if (root == null || cam == null) return;
            float now = Time.time;
            // 晃動手機＝踏一步
            if (steps.Feed(Input.acceleration, now)) { walkUntil = now + StepHold; Haptics.Play(Haptics.Kind.Light); }
            if (holdWalk) holdUntil = now + 0.1f;
            float key = 0f;
#if UNITY_EDITOR
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) key = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) key = -0.6f;
#endif
            float target = now < walkUntil || now < holdUntil ? StepSpeed : key * StepSpeed;
            speed = Mathf.MoveTowards(speed, target, dt * (Mathf.Abs(target) > Mathf.Abs(speed) ? 6f : 4f));
            var tr = root.transform;
            var me = tr.InverseTransformPoint(cam.transform.position);
            var meXZ = new Vector2(me.x, me.z);
            if (Mathf.Abs(speed) > 0.01f)
            {
                var f = tr.InverseTransformDirection(cam.transform.forward); f.y = 0f;
                if (f.sqrMagnitude > 1e-4f)
                {
                    var d = new Vector2(f.x, f.z).normalized * speed * dt;
                    var to = Resolve(meXZ + d);
                    var delta = to - meXZ;
                    // 城鎮往反方向移，玩家看起來就往前走
                    tr.position -= tr.TransformVector(new Vector3(delta.x, 0f, delta.y));
                    meXZ = to;
                }
            }
            // 走出柵欄的出口 → 選關；選完回來時站在出口內側
            foreach (var e in exits)
                if (Vector2.Dot(meXZ - PlazaC, e.dir) > TownRadius + ExitDepth)
                {
                    MovePlayer(tr, ref meXZ, PlazaC + e.dir * (TownRadius - 1.5f));
                    speed = 0f; walkUntil = holdUntil = 0f;
                    OnExit?.Invoke(e.theme);
                    break;
                }
            // 傳送陣
            float wd = Vector2.Distance(meXZ, new Vector2(WarpPos.x, WarpPos.z));
            // 站在傳送陣上 WarpStay 秒才觸發（走過去不會誤傳送）；離開 1.3m 才重新計時
            if (!onWarp && wd < 0.8f)
            {
                if (warpEnterAt < 0f) warpEnterAt = Time.time;
                if (Time.time - warpEnterAt >= WarpStay) { onWarp = true; warpEnterAt = -1f; OnWarp?.Invoke(); }
            }
            else if (!onWarp) warpEnterAt = -1f;
            if (onWarp && wd > 1.3f) onWarp = false;
            // 居民：玩家靠近時轉頭看玩家；找可以打招呼的人
            Near = null; float best = float.MaxValue;
            var camF = tr.InverseTransformDirection(cam.transform.forward); var camF2 = new Vector2(camF.x, camF.z);
            foreach (var n in npcs)
            {
                var np = new Vector2(n.pos.x, n.pos.z);
                float dist = Vector2.Distance(np, meXZ);
                float yaw = dist < 4f ? Yaw(meXZ - np) : n.yaw;
                n.tf.localRotation = Quaternion.Slerp(n.tf.localRotation, Quaternion.Euler(0f, yaw, 0f), dt * 4f);
                if (dist > TalkRange) continue;
                float ang = camF2.sqrMagnitude > 1e-4f ? Vector2.Angle(camF2, np - meXZ) : 0f;
                if (ang > TalkAngle && dist > 1.2f) continue;
                float score = dist + ang * 0.02f;
                if (score < best) { best = score; Near = n; }
            }
        }

        /// <summary>把玩家（在城鎮座標）移到 to：移動整個城鎮</summary>
        static void MovePlayer(Transform tr, ref Vector2 me, Vector2 to)
        {
            var delta = to - me;
            tr.position -= tr.TransformVector(new Vector3(delta.x, 0f, delta.y));
            me = to;
        }

        /// <summary>移動後的位置：不能穿過建築／居民，不能走出柵欄（出口的缺口可以）</summary>
        Vector2 Resolve(Vector2 p)
        {
            for (int k = 0; k < 2; k++)
                foreach (var (c, r) in walls)
                {
                    var v = p - c; float rr = r + 0.3f;
                    if (v.sqrMagnitude < rr * rr) p = c + (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.down) * rr;
                }
            return Fence(p, PlazaC, TownRadius, exits.ConvertAll(e => e.dir), GateHalf, ExitDepth + 0.5f);
        }

        /// <summary>聽到一段話：有招呼語而且面前有人就開始聊天。回傳是否觸發。</summary>
        public bool Heard(string text)
        {
            if (root == null || string.IsNullOrEmpty(text) || Time.time < greetLock) return false;
            string g = Greeting.Find(text);
            if (g == null) return false;
            LastGreet = g; LastGreetAt = Time.time;
            return Greet();
        }

        /// <summary>和面前的人打招呼（語音或按鈕）</summary>
        public bool Greet()
        {
            if (Near == null) return false;
            greetLock = Time.time + 1.5f;
            OnGreet?.Invoke(Near);
            return true;
        }

        /// <summary>柵欄（純數學，可在 Unity 外測試）：圈外只有出口缺口的通道可以走（通道寬 2×half、深 depth）</summary>
        public static Vector2 Fence(Vector2 p, Vector2 c, float r, List<Vector2> gaps, float half, float depth)
        {
            var v = p - c;
            float inner = r - 0.3f;
            if (v.magnitude <= inner) return p;
            foreach (var g in gaps)
            {
                float along = Vector2.Dot(v, g), side = g.x * v.y - g.y * v.x;
                // 在缺口的通道裡；已經走到柵欄外的通道時，撞到通道兩側只會被擋在通道內
                if (along > 0f && (Mathf.Abs(side) <= half - 0.3f || along > r && Mathf.Abs(side) < half + 1f))
                    return c + g * Mathf.Min(along, r + depth) + new Vector2(-g.y, g.x) * Mathf.Clamp(side, -(half - 0.3f), half - 0.3f);
            }
            return c + v.normalized * inner;
        }

        /// <summary>城鎮座標的一點在畫面上的位置（GUI 座標）</summary>
        public bool ScreenPos(Camera cam, Vector3 local, out Vector2 gui, out float dist)
        {
            gui = default; dist = 0f;
            if (root == null || cam == null) return false;
            var wp = root.transform.TransformPoint(local);
            var sp = cam.WorldToScreenPoint(wp);
            dist = Vector3.Distance(cam.transform.position, wp);
            if (sp.z <= 0.1f) return false;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }

        /// <summary>某個居民頭上名牌的螢幕位置（GUI 座標）；在鏡頭後面回傳 false</summary>
        public bool LabelPos(Camera cam, Npc n, out Vector2 gui, out float dist)
        {
            gui = default; dist = 0f;
            if (root == null || cam == null || n.tf == null) return false;
            var wp = n.tf.position + root.transform.up * (2.05f * n.tf.localScale.y);
            var sp = cam.WorldToScreenPoint(wp);
            dist = Vector3.Distance(cam.transform.position, n.tf.position);
            if (sp.z <= 0.1f) return false;
            gui = new Vector2(sp.x, Screen.height - sp.y);
            return true;
        }
    }

    /// <summary>招呼語（純 C#，可在 Unity 外測試）</summary>
    public static class Greeting
    {
        public static readonly string[] Words =
        {
            "你好", "您好", "妳好", "你們好", "大家好", "哈囉", "哈嘍", "哈喽", "哈罗", "嗨", "嘿", "安安", "早安", "午安", "晚安", "早啊", "早上好",
            "晚上好", "下午好", "喂", "哈啰", "hello", "hi", "hey", "哈哈囉",
        };

        /// <summary>這段話裡的招呼語（沒有回傳 null）</summary>
        public static string Find(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string t = text.Trim().ToLowerInvariant();
            foreach (var w in Words)
            {
                int i = t.IndexOf(w, StringComparison.Ordinal);
                if (i < 0) continue;
                // 英文要是獨立的字（"this" 裡的 hi 不算）
                if (w[0] < 128)
                {
                    bool l = i == 0 || !char.IsLetter(t[i - 1]), r = i + w.Length >= t.Length || !char.IsLetter(t[i + w.Length]);
                    if (!l || !r) continue;
                }
                return w;
            }
            return null;
        }
    }

    /// <summary>
    /// 踏步偵測（純 C#，可在 Unity 外測試）：手機上下晃動時加速度大小會一高一低。
    /// 加速度大小比慢慢追蹤的基準（約 1g）高出 Threshold 以上、而且離上一步夠久，就算一步。
    /// </summary>
    public class StepDetector
    {
        public float Threshold = 0.16f, MinInterval = 0.28f, Rearm = 0.06f;
        float baseG = 1f, lastStep = -9f; bool armed = true;

        public void Reset() { baseG = 1f; lastStep = -9f; armed = true; }

        public bool Feed(Vector3 acc, float now)
        {
            float m = acc.magnitude;
            if (m < 0.05f) return false;          // 沒有加速度計（編輯器）
            baseG = Mathf.Lerp(baseG, m, 0.02f);
            float dev = m - baseG;
            if (!armed) { if (dev < Rearm) armed = true; return false; }
            if (dev > Threshold && now - lastStep > MinInterval) { lastStep = now; armed = false; return true; }
            return false;
        }
    }
}
