using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;

namespace SpellDuel
{
    /// <summary>
    /// 故事探索的特效影片（影片生成機用 MiniMax H3 做的 mp4，960×544、約 5 秒；Docs/explore_fx/）。
    /// 跟劇情影片一樣不打包進 App：進探索時在背景從 GitHub 下載（約 5MB，只有第一次），還沒下載好的就先不播。
    /// 播法：場景裡一片面向鏡頭的方形（billboard），黑底的用加法混色、綠幕的去綠（Resources/Shaders/ExploreFxVideo）。
    /// 手機同時能解的影片有限：最多 MaxPlayers 支同時播，一次性的優先，循環的只播最近的幾個。
    /// </summary>
    public class ExploreFx : MonoBehaviour
    {
        public const string BaseUrl = "https://raw.githubusercontent.com/paul771127/AnimeGenerate/claude/mobile-camera-mic-battle-game-v5nhfr/spellduel-unity/Docs/explore_fx/videos/";
        public const int MaxPlayers = 6;
        const float ClipLen = 5.17f;

        /// <summary>id → (綠幕?, 遊戲裡的秒數；一次性的會加速到這個長度)。和 Docs/explore_fx/shots.json 一致</summary>
        static readonly Dictionary<string, (bool green, float sec)> Spec = new Dictionary<string, (bool, float)>
        {
            ["fx_A_unseal_burst"] = (false, 1.5f), ["fx_A5_socket_pulse"] = (false, 0.8f),
            ["fx_B1_rune_idle"] = (false, 2f), ["fx_B1_activate"] = (false, 1f), ["fx_B2_pickup"] = (false, 0.6f),
            ["fx_C1_glint_gold"] = (false, 3f), ["fx_C1_glint_green"] = (false, 3f), ["fx_C1_glint_eyes"] = (false, 3f), ["fx_C1_glint_white"] = (false, 3f),
            ["fx_C2_discover"] = (false, 1f), ["fx_C4_heart_rise"] = (false, 2f), ["fx_C4_heart_take"] = (false, 1f),
            ["fx_D3_fire"] = (false, 1f), ["fx_D4_spores"] = (false, 4f),
            ["fx_E2_alert"] = (false, 0.8f), ["fx_E2_roar_wave"] = (false, 1f),
        };

        static string Dir => Path.Combine(Application.persistentDataPath, "explore_fx");
        static string PathOf(string id) => Path.Combine(Dir, id + ".mp4");
        public static bool Ready(string id) { try { return File.Exists(PathOf(id)); } catch { return false; } }

        static ExploreFx inst;
        static bool downloading;
        /// <summary>進探索時呼叫：背景下載還沒有的影片（不擋遊戲）</summary>
        public static void Prefetch()
        {
            if (downloading) return;
            if (!inst) inst = new GameObject("Explore fx videos").AddComponent<ExploreFx>();
            inst.StartCoroutine(inst.Download());
        }
        IEnumerator Download()
        {
            downloading = true;
            try { Directory.CreateDirectory(Dir); } catch { downloading = false; yield break; }
            foreach (var id in Spec.Keys)
            {
                if (Ready(id)) continue;
                string tmp = PathOf(id) + ".part";
                using (var req = new UnityWebRequest(BaseUrl + id + ".mp4", UnityWebRequest.kHttpVerbGET) { downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true }, timeout = 60 })
                {
                    yield return req.SendWebRequest();
                    bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode == 200;
                    try { if (ok) File.Move(tmp, PathOf(id)); else if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    if (!ok) break;   // 沒網路：下次進探索再試
                }
            }
            downloading = false;
        }

        // ---------------------------------------------------------------- 播放
        class Player
        {
            public GameObject go; public VideoPlayer vp; public RenderTexture rt; public Material mat;
            public object key; public bool loop; public Func<Vector3> at; public float size, born;
        }
        readonly List<Player> players = new List<Player>();
        static Shader shader;

        /// <summary>播一次（播完自己消失）；at＝每幀的位置（parent 的本地座標）</summary>
        public static void Once(Transform parent, string id, Func<Vector3> at, float size)
        {
            if (!parent || !Ready(id)) return;
            if (!inst) inst = new GameObject("Explore fx videos").AddComponent<ExploreFx>();
            inst.Make(parent, id, new object(), false, at, size);
        }

        /// <summary>循環：每幀對要顯示的東西呼叫（show＝false 就收掉）；同一個 key 只會有一支</summary>
        public static void Loop(Transform parent, object key, string id, bool show, Func<Vector3> at, float size)
        {
            if (!inst) { if (!show) return; inst = new GameObject("Explore fx videos").AddComponent<ExploreFx>(); }
            var p = inst.players.Find(x => x.key == key);
            if (!show) { if (p != null) inst.Kill(p); return; }
            if (p != null) { p.at = at; return; }
            if (!parent || !Ready(id)) return;
            // 循環的只佔空位，不擠掉別的（一次性的才會擠掉最舊的循環）
            if (inst.players.Count >= MaxPlayers) return;
            inst.Make(parent, id, key, true, at, size);
        }

        /// <summary>離開探索：全部收掉</summary>
        public static void Clear() { if (inst) foreach (var p in inst.players.ToArray()) inst.Kill(p); }

        void Make(Transform parent, string id, object key, bool loop, Func<Vector3> at, float size)
        {
            if (players.Count >= MaxPlayers)
            {
                var old = players.Find(x => x.loop);   // 一次性的優先：擠掉一支循環的
                if (old == null || loop) return;
                Kill(old);
            }
            if (!shader) shader = Resources.Load<Shader>("Shaders/ExploreFxVideo") ?? Shader.Find("SpellDuel/ExploreFxVideo");
            if (!shader) return;
            (bool green, float sec) spec = Spec.TryGetValue(id, out var s) ? s : (false, ClipLen);
            var p = new Player { key = key, loop = loop, at = at, size = size, born = Time.time };
            p.go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            p.go.name = "fx " + id;
            var col = p.go.GetComponent<Collider>(); if (col) Destroy(col);
            p.go.transform.SetParent(parent, false);
            p.go.transform.localScale = new Vector3(size * 960f / 544f, size, 1f);
            p.rt = new RenderTexture(480, 272, 0);
            p.mat = new Material(shader); p.mat.SetTexture("_MainTex", p.rt); p.mat.SetFloat("_Key", spec.green ? 1f : 0f);
            var r = p.go.GetComponent<Renderer>(); r.sharedMaterial = p.mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            p.vp = p.go.AddComponent<VideoPlayer>();
            p.vp.source = VideoSource.Url; p.vp.url = "file://" + PathOf(id);
            p.vp.renderMode = VideoRenderMode.RenderTexture; p.vp.targetTexture = p.rt;
            p.vp.audioOutputMode = VideoAudioOutputMode.None; p.vp.playOnAwake = false;
            p.vp.isLooping = loop;
            p.vp.playbackSpeed = loop ? 1f : Mathf.Clamp(ClipLen / Mathf.Max(0.3f, spec.sec), 1f, 4f);   // 一次性：加速到遊戲裡的長度
            if (!loop) p.vp.loopPointReached += vp => { var q = players.Find(x => x.vp == vp); if (q != null) Kill(q); };
            p.vp.prepareCompleted += vp => vp.Play();
            p.vp.errorReceived += (vp, msg) => { var q = players.Find(x => x.vp == vp); if (q != null) Kill(q); };
            p.vp.Prepare();
            players.Add(p);
            Place(p);
        }

        void Kill(Player p)
        {
            players.Remove(p);
            if (p.vp) p.vp.Stop();
            if (p.go) Destroy(p.go);
            if (p.rt) { p.rt.Release(); Destroy(p.rt); }
            if (p.mat) Destroy(p.mat);
        }

        void Place(Player p)
        {
            if (!p.go) return;
            p.go.transform.localPosition = p.at();
            var cam = Camera.main;
            if (cam) p.go.transform.rotation = Quaternion.LookRotation(p.go.transform.position - cam.transform.position, cam.transform.up);
        }

        void LateUpdate()
        {
            for (int i = players.Count - 1; i >= 0; i--)
            {
                var p = players[i];
                if (!p.go || !p.go.transform.parent) { Kill(p); continue; }
                if (!p.loop && Time.time - p.born > 8f) { Kill(p); continue; }   // 保險：一次性的最多 8 秒
                Place(p);
            }
        }
    }
}
