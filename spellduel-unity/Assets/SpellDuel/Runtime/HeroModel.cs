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
        static readonly Dictionary<string, string> Kits = new Dictionary<string, string>
        {
            { "archmage", "CharKit/archmage_v1/" },
        };
        /// <summary>這個職業／進階職業有沒有生成器的模型（法師還沒轉職也先用大魔導師的）</summary>
        public static string KitFor(string cls, string adv)
        {
            if (adv != null && Kits.TryGetValue(adv, out var k)) return k;
            if (cls == "mage" && (adv == null || adv == "archmage")) return Kits["archmage"];
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

        public static HeroModel Create(string cls, string adv)
        {
            string kit = KitFor(cls, adv); if (kit == null) return null;
            var src = Resources.Load<GameObject>(kit + "archmage");
            if (!src) return null;
            var go = Instantiate(src);
            go.name = "Hero " + kit;
            var h = go.AddComponent<HeroModel>();
            h.Kit = kit;
            if (!h.Setup(kit)) { Destroy(go); return null; }
            return h;
        }

        bool Setup(string kit)
        {
            // 材質
            if (!bodyMats.TryGetValue(kit, out var body) || !body)
            {
                var sh = Resources.Load<Shader>(kit + "archmage_toon");
                if (sh == null || !sh.isSupported) sh = Shader.Find("Unlit/Texture");
                body = new Material(sh) { name = "Hero body " + kit, mainTexture = Resources.Load<Texture2D>(kit + "archmage_albedo") };
                var em = Resources.Load<Texture2D>(kit + "archmage_emission"); if (em && body.HasProperty("_EmissionMap")) body.SetTexture("_EmissionMap", em);
                bodyMats[kit] = body;
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
            foreach (var c in Resources.LoadAll<AnimationClip>(kit + "archmage"))
            {
                if (c == null || c.name.StartsWith("__preview__")) continue;
                string n = c.name; int bar = n.LastIndexOf('|'); if (bar >= 0) n = n.Substring(bar + 1);
                clips[n.ToLowerInvariant()] = c;
            }
            if (clips.Count == 0) return true;   // 沒有動作也照樣顯示（站姿）
            graph = PlayableGraph.Create("Hero " + kit);
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
            if (!clips.TryGetValue(name, out var c)) return;
            curName = null; Play(name, false);
            curSpeed = Mathf.Max(0.1f, speed);
            lockUntil = Time.time + Mathf.Max(0.2f, c.length * 0.9f / curSpeed);
        }
        public bool Busy => Time.time < lockUntil;

        /// <summary>每幀：位置、面向（session 座標）＋動作狀態</summary>
        public void Drive(Vector3 feet, Vector3 forward, float speed, bool dead, bool charging, bool crouch, bool air)
        {
            forward.y = 0f; if (forward.sqrMagnitude < 1e-4f) forward = transform.forward;
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
                else if (charging) Play("cast_loop", true);
                else if (crouch) Play("crouch", true);
                else if (speed > 1.6f) Play("run", true);
                else if (speed > 0.25f) Play("walk", true);
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
        }
    }
}
