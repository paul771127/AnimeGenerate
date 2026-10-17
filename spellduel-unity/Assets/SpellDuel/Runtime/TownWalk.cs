using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 可以走動的 3D 城鎮（故事模式進城時）：
    ///   走路：上下晃動手機（像原地踏步）＝往螢幕準心的方向前進；真的走路也會動（AR）。也可以按住「前進」。
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

        public bool Active => root != null;
        public int Town { get; private set; } = -1;
        public readonly List<Npc> npcs = new List<Npc>();
        public Npc Near { get; private set; }            // 可以打招呼的居民
        public bool Walking => speed > 0.15f;
        public string LastGreet { get; private set; } = ""; public float LastGreetAt = -9f;

        /// <summary>打招呼成功（SoloBattle 打開對話框）</summary>
        public Action<Npc> OnGreet;

        GameObject root;
        readonly List<(Vector2 c, float r)> walls = new List<(Vector2, float)>();
        readonly StepDetector steps = new StepDetector();
        float speed, walkUntil, holdUntil, greetLock;
        public const float TownRadius = 13f, StepSpeed = 1.4f, StepHold = 0.5f, TalkRange = 2.6f, TalkAngle = 50f;
        static readonly Vector2 PlazaC = new Vector2(0f, 5f);

        // ---------------------------------------------------------------- 進城／離開
        public void Enter(int town, Camera cam, float floorY)
        {
            Exit();
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
            Building("house", new Vector2(-9.5f, 1.5f), 2.1f);
            Building("house", new Vector2(9.5f, 1.5f), 2.1f);
            Building("landmark", town == 2 ? new Vector2(9f, 13f) : new Vector2(-9f, 13f), 1.4f);
            npcs.Clear();
            Keeper("鐵匠", "鐵匠鋪老闆", "smith", new Vector2(-7f, 8f), new[] { "要強化裝備嗎？把東西放上鐵砧吧。" });
            Keeper("店員", "道具店店員", "shop", new Vector2(7f, 8f), new[] { "歡迎光臨！藥水、回城捲軸都有，也收裝備喔。" });
            Keeper("公會長", "職業工會", "guild", new Vector2(0f, 13.5f), new[] { "歡迎來到職業工會。轉職試煉與進階職業，都在這裡辦。" });
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

        public void Exit()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null; Town = -1; npcs.Clear(); walls.Clear(); Near = null;
        }

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
            n.tf = go.transform;
            n.tf.localPosition = n.pos; n.tf.localRotation = Quaternion.Euler(0f, n.yaw, 0f);
            npcs.Add(n);
            walls.Add((new Vector2(n.pos.x, n.pos.z), 0.45f));
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

        /// <summary>移動後的位置：不能穿過建築／居民，不能走出城鎮</summary>
        Vector2 Resolve(Vector2 p)
        {
            for (int k = 0; k < 2; k++)
                foreach (var (c, r) in walls)
                {
                    var v = p - c; float rr = r + 0.3f;
                    if (v.sqrMagnitude < rr * rr) p = c + (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.down) * rr;
                }
            var fromC = p - PlazaC;
            if (fromC.magnitude > TownRadius) p = PlazaC + fromC.normalized * TownRadius;
            return p;
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
