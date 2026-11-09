using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SpellDuel
{
    /// <summary>
    /// 角色生成器做的主角 3D 模型（chargen 工單 001：Resources/CharKit/archmage_v1/）。
    /// 第三人稱的主角用（SoloBattle.ThirdPerson.cs）：有模型的職業用它，沒有的照舊用程式拼的 CharacterRig。
    /// 材質：本體＝生成器附的賽璐璐 shader（albedo＋發光遮罩），OUTLINE_* ＝黑色描邊。
    /// 動作：FBX 裡的 10 個動作，用 Playables 直接播（不用在編輯器做 Animator Controller）；兩個動作之間淡入淡出。
    /// </summary>
    public sealed class HeroModel : MonoBehaviour
    {
        /// <summary>職業 id → 有沒有模型（Resources/CharKit/&lt;id&gt;_v1/&lt;id&gt;.fbx；工單 003 交一個就自動用一個）</summary>
        static readonly Dictionary<string, bool> kitExists = new Dictionary<string, bool>();
        static bool HasKit(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!kitExists.TryGetValue(id, out var ok)) kitExists[id] = ok = Resources.Load<GameObject>(KitDir(id) + id) != null;
            return ok;
        }
        static string KitDir(string id) => "CharKit/" + id + "_v1/";
        /// <summary>這個職業用哪一套模型：進階職業的 → 基本職業的 → 法師還沒有就先用大魔導師；都沒有＝null（用程式拼的角色）</summary>
        public static string KitFor(string cls, string adv)
        {
            if (HasKit(adv)) return adv;
            if (HasKit(cls)) return cls;
            if (cls == "mage" && (adv == null || adv == "archmage") && HasKit("archmage")) return "archmage";
            return null;
        }

        static readonly Dictionary<string, Material> bodyMats = new Dictionary<string, Material>();
        static Material outlineMat;

        public string Kit { get; private set; }
        Animator animator;
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        AnimationClipPlayable cur, prev;
        string curName; bool curLoop; float curTime, prevTime, fade = 1f, lockUntil, curSpeed = 1f;
        const float FadeTime = 0.18f;

        Vector3 smFeet, smFwd; bool posed;
        // 施法改用法杖（使用者回報：動作是空著的左手在施法）：施法時把右手（拿法杖）往前舉、左手放下
        Transform rUpper, rLower, rHand, lUpper, lLower, lHand, staff;
        float staffW;

        public static HeroModel Create(string cls, string adv)
        {
            string id = KitFor(cls, adv); if (id == null) return null;
            var src = Resources.Load<GameObject>(KitDir(id) + id);
            if (!src) return null;
            var go = Instantiate(src);
            go.name = "Hero " + id;
            var h = go.AddComponent<HeroModel>();
            h.Kit = id;
            if (!h.Setup(id)) { Destroy(go); return null; }
            return h;
        }

        bool Setup(string id)
        {
            string kit = KitDir(id);
            // 材質：共用的賽璐璐 shader（Shaders/CharCel）；舊的 kit 自己附的也可以
            if (!bodyMats.TryGetValue(id, out var body) || !body)
            {
                var sh = Resources.Load<Shader>("Shaders/CharCel");
                if (sh == null || !sh.isSupported) sh = Resources.Load<Shader>(kit + id + "_toon");
                if (sh == null || !sh.isSupported) sh = Shader.Find("Unlit/Texture");
                body = new Material(sh) { name = "Hero body " + id, mainTexture = Resources.Load<Texture2D>(kit + id + "_albedo") };
                var em = Resources.Load<Texture2D>(kit + id + "_emission"); if (em && body.HasProperty("_EmissionMap")) body.SetTexture("_EmissionMap", em);
                bodyMats[id] = body;
            }
            if (!outlineMat)
            {
                var osh = Resources.Load<Shader>("Shaders/CharOutline");
                if (osh != null && osh.isSupported) outlineMat = new Material(osh) { name = "Hero outline" };
            }
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                bool outline = r.name.StartsWith("OUTLINE");
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = outline ? outlineMat : body;
                if (outline && !outlineMat) { r.enabled = false; continue; }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;   // 動作會超出原本的範圍：不要被裁掉
            }
            // 動作
            animator = GetComponentInChildren<Animator>();
            if (!animator) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var c in Resources.LoadAll<AnimationClip>(kit.TrimEnd('/')))   // 整個資料夾：主 FBX 和 archmage@動作.fbx（工單 002 的側移、後退、法杖施法）
            {
                if (c == null || c.name.StartsWith("__preview__")) continue;
                string n = c.name; int bar = n.LastIndexOf('|'); if (bar >= 0) n = n.Substring(bar + 1);
                clips[n.ToLowerInvariant()] = c;
            }
            Transform Bone(string n) { foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            rUpper = Bone("J_Bip_R_UpperArm"); rLower = Bone("J_Bip_R_LowerArm"); rHand = Bone("J_Bip_R_Hand");
            lUpper = Bone("J_Bip_L_UpperArm"); lLower = Bone("J_Bip_L_LowerArm"); lHand = Bone("J_Bip_L_Hand");
            staff = Bone("Staff") ?? Bone("Weapon_R");
            if (clips.Count == 0) return true;   // 沒有動作也照樣顯示（站姿）
            graph = PlayableGraph.Create("Hero " + id);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, 2);
            var output = AnimationPlayableOutput.Create(graph, "Hero", animator);
            output.SetSourcePlayable(mixer);
            Play("idle", true, true);
            graph.Play();
            return true;
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        /// <summary>換動作（同一個就不動）；force＝立刻換、不淡入</summary>
        public void Play(string name, bool loop, bool force = false)
        {
            if (!graph.IsValid() || name == curName || !clips.TryGetValue(name, out var clip)) return;
            if (prev.IsValid()) { graph.Disconnect(mixer, 1); prev.Destroy(); }
            if (cur.IsValid()) { graph.Disconnect(mixer, 0); prev = cur; prevTime = curTime; graph.Connect(prev, 0, mixer, 1); }
            cur = AnimationClipPlayable.Create(graph, clip);
            cur.SetApplyFootIK(false);
            graph.Connect(cur, 0, mixer, 0);
            curName = name; curLoop = loop; curTime = 0f; curSpeed = 1f; fade = force || !prev.IsValid() ? 1f : 0f;
        }

        /// <summary>一次性的動作（施法、被打、跳）：播完前不會被走路之類的蓋掉</summary>
        public void PlayOnce(string name, float speed = 1f)
        {
            if (name == "cast") name = CastClip; else if (name == "attack") name = AttackClip;
            if (!clips.TryGetValue(name, out var c)) return;
            curName = null; Play(name, false);
            curSpeed = Mathf.Max(0.1f, speed);
            lockUntil = Time.time + Mathf.Max(0.2f, c.length * 0.9f / curSpeed);
        }
        public bool Busy => Time.time < lockUntil;

        /// <summary>每幀：位置、面向（session 座標）＋動作狀態</summary>
        public bool Has(string n) => clips.ContainsKey(n);
        /// <summary>有沒有側移／後退的動作（工單 002）；沒有＝往旁邊、往後走時轉身面向移動方向</summary>
        bool HasStrafe => Has("walk_back") && Has("walk_left") && Has("walk_right");
        // 工單 003 的名字（attack／skill／skill_loop）→ 大魔導師的（cast_staff…）→ 001 的（cast…）
        string Pick(params string[] names) { foreach (var n in names) if (Has(n)) return n; return names[names.Length - 1]; }
        string AttackClip => Pick("attack", "cast_staff", "cast");
        string CastClip => Pick("skill", "cast_staff", "cast");
        string CastLoopClip => Pick("skill_loop", "cast_staff_loop", "cast_loop");

        /// <summary>每幀：位置、面向、移動方向（session 座標）＋動作狀態；lockFacing＝剛攻擊完、要一直面向準心</summary>
        public void Drive(Vector3 feet, Vector3 forward, Vector3 move, float speed, bool dead, bool charging, bool crouch, bool air, bool lockFacing)
        {
            forward.y = 0f; if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
            move.y = 0f;
            string walk = "walk", run = "run";
            if (speed > 0.25f && move.sqrMagnitude > 1e-6f && !charging && !lockFacing)
            {
                var m = move.normalized; var f = forward.normalized; var r = new Vector3(f.z, 0f, -f.x);
                float fw = Vector3.Dot(m, f), rt = Vector3.Dot(m, r);
                if (HasStrafe)
                {
                    // 面向前方，照移動方向挑：前、後、左、右
                    if (fw < -0.5f) { walk = "walk_back"; run = Has("run_back") ? "run_back" : "walk_back"; }
                    else if (Mathf.Abs(rt) > 0.6f) { walk = rt > 0 ? "walk_right" : "walk_left"; run = Has(rt > 0 ? "run_right" : "run_left") ? (rt > 0 ? "run_right" : "run_left") : walk; }
                }
                else if (fw < 0.7f) forward = m;   // 還沒有側移、後退動作：轉身面向移動方向走
            }
            float dt = Time.deltaTime;
            if (!posed) { smFeet = feet; smFwd = forward.normalized; posed = true; }
            smFeet = Vector3.Lerp(smFeet, feet, 1f - Mathf.Exp(-dt * 18f));
            smFwd = Vector3.Slerp(smFwd, forward.normalized, 1f - Mathf.Exp(-dt * 12f));
            transform.SetPositionAndRotation(smFeet, Quaternion.LookRotation(smFwd, Vector3.up));
            // 動作狀態
            if (dead) { if (curName != "die") { lockUntil = 0f; Play("die", false); } }
            else if (!Busy)
            {
                if (air) Play("jump", false);
                else if (charging) Play(CastLoopClip, true);
                else if (crouch) Play("crouch", true);
                else if (speed > 1.6f) Play(run, true);
                else if (speed > 0.25f) Play(walk, true);
                else Play("idle", true);
            }
            Tick(dt);
        }

        void Tick(float dt)
        {
            if (!graph.IsValid() || !cur.IsValid()) return;
            curTime += dt * curSpeed; prevTime += dt;
            float len = Mathf.Max(0.05f, cur.GetAnimationClip().length);
            cur.SetTime(curLoop ? curTime % len : Mathf.Min(curTime, len));
            if (prev.IsValid()) { float pl = Mathf.Max(0.05f, prev.GetAnimationClip().length); prev.SetTime(prevTime % pl); }
            fade = Mathf.MoveTowards(fade, 1f, dt / FadeTime);
            mixer.SetInputWeight(0, fade);
            mixer.SetInputWeight(1, prev.IsValid() ? 1f - fade : 0f);
            graph.Evaluate(0f);
            StaffPose(dt);
        }

        /// <summary>動作算完之後：施法中把右手法杖往前（放出）／往上前方（詠唱）舉，左手垂下</summary>
        void StaffPose(float dt)
        {
            bool casting = curName == "cast" || curName == "cast_loop";   // 有法杖施法的正式動作（cast_staff）就不用扳手臂
            staffW = Mathf.MoveTowards(staffW, casting ? 1f : 0f, dt * 6f);
            if (staffW <= 0.001f || !rUpper || !rLower || !rHand) return;
            var f = transform.forward; var up = Vector3.up; var right = transform.right;
            var armDir = curName == "cast" ? (f * 0.9f + up * 0.3f + right * 0.05f).normalized : (f * 0.45f + up * 0.85f + right * 0.1f).normalized;
            void Aim(Transform a, Transform b, Vector3 dir)
            {
                var cur = b.position - a.position; if (cur.sqrMagnitude < 1e-8f) return;
                var q = Quaternion.FromToRotation(cur.normalized, dir);
                a.rotation = Quaternion.Slerp(Quaternion.identity, q, staffW) * a.rotation;
            }
            Aim(rUpper, rLower, armDir); Aim(rLower, rHand, armDir);
            // 法杖頂端朝手臂的方向往前（法杖骨頭的 Y 軸當杖身）
            if (staff) { var q = Quaternion.FromToRotation(staff.up, (armDir + up * 0.6f).normalized); staff.rotation = Quaternion.Slerp(Quaternion.identity, q, staffW * 0.8f) * staff.rotation; }
            if (lUpper && lLower && lHand) { var down = (-up * 0.95f + f * 0.15f - right * 0.2f).normalized; Aim(lUpper, lLower, down); Aim(lLower, lHand, down); }
        }

        /// <summary>法球從哪裡射出（session 座標）：法杖頂端附近</summary>
        public Vector3 CastPoint
        {
            get
            {
                float s = transform.lossyScale.y;
                return transform.position + Vector3.up * 1.45f * s + transform.forward * 0.65f * s;
            }
        }
    }
}
