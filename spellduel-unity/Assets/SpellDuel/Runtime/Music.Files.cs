using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace SpellDuel
{
    /// <summary>
    /// 生成的配樂（地端音樂生成機做的 mp3，Docs/music/tracks/；曲目與用途見 Docs/music/tracks.json）。
    ///   ・跟劇情影片、探索特效一樣不打包進 App：App 一開就在背景把全部曲子下載下來（約 60MB，只有第一次；要播的那首優先）。
    ///   ・下載好的用壓縮格式留在記憶體（一首約 1.4MB；解壓的話 90 秒立體聲約 30MB）。
    ///   ・還沒下載好的：用程式合成的配樂頂著（SynthOf）。
    ///   ・勝利／失敗短曲：Jingle 播一次，播的時候背景音樂壓低。
    /// </summary>
    public partial class Music
    {
        public const string TrackUrl = "https://raw.githubusercontent.com/paul771127/AnimeGenerate/claude/mobile-camera-mic-battle-game-v5nhfr/spellduel-unity/Docs/music/tracks/";
        /// <summary>全部的曲目（和 Docs/music/tracks.json 一致）</summary>
        public static readonly string[] Tracks =
        {
            "title", "worldmap", "town_0", "town_1", "town_2", "town_3", "town_4",
            "ch0", "ch1", "ch2", "ch3", "ch4", "ch0_boss", "ch1_boss", "ch2_boss", "ch3_boss", "ch4_boss", "explore", "explore_boss",
            "dg_ash_mine", "dg_ash_mine_boss", "dg_rot_temple", "dg_rot_temple_boss", "dg_sunken_throne", "dg_sunken_throne_boss",
            "dg_dawn_gate", "dg_dawn_gate_boss", "dg_ember_bell_foundry", "dg_ember_bell_foundry_boss", "dg_mist_name_grove", "dg_mist_name_grove_boss",
            "dg_white_sail_echo_reef", "dg_white_sail_echo_reef_boss", "dg_thousand_trap_gallery", "dg_thousand_trap_gallery_boss", "dg_sky_carpet", "dg_sky_carpet_boss",
            "tower", "worldboss", "practice", "victory", "defeat",
        };
        static string Dir => Path.Combine(Application.persistentDataPath, "music");
        static string FileOf(string id) => Path.Combine(Dir, id + ".mp3");
        static bool FileReady(string id) { try { return File.Exists(FileOf(id)); } catch { return false; } }

        AudioSource jingleSrc; float jingleUntil;
        float JingleDuck => Time.unscaledTime < jingleUntil ? 0.25f : 1f;
        readonly HashSet<string> loading = new HashSet<string>(), broken = new HashSet<string>();
        string priority; bool downloading;

        /// <summary>沒有下載好的曲子：用哪一首合成曲頂著</summary>
        static string SynthOf(string id)
        {
            if (id.StartsWith("ch") || id.StartsWith("dg_") || id.StartsWith("explore") || id == "boss" || id.StartsWith("battle")) return id;
            if (id == "tower" || id == "worldboss") return "boss";
            if (id == "practice") return "ch0";
            return "town";   // title、worldmap、town_*
        }

        /// <summary>想播 id：下載好的回傳 "f:"＋id（還在讀檔就先維持現在這首）；沒有就排下載、回傳合成曲</summary>
        string Resolve(string id)
        {
            if (id == null) return null;
            bool known = System.Array.IndexOf(Tracks, id) >= 0;
            if (known && !broken.Contains(id) && FileReady(id))
            {
                string key = "f:" + id;
                if (clips.ContainsKey(key)) return clips[key] != null ? key : SynthOf(id);
                if (!loading.Contains(id)) StartCoroutine(LoadFile(id));
                return playing ?? SynthOf(id);   // 讀檔不到一秒：先維持原本的
            }
            if (known) { priority = id; if (!downloading) StartCoroutine(Download()); }
            return SynthOf(id);
        }

        IEnumerator LoadFile(string id)
        {
            loading.Add(id);
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + FileOf(id), AudioType.MPEG))
            {
                var dh = (DownloadHandlerAudioClip)req.downloadHandler;
                dh.compressed = true; dh.streamAudio = false;   // 留壓縮格式：省記憶體
                yield return req.SendWebRequest();
                AudioClip clip = null;
                if (req.result == UnityWebRequest.Result.Success) { try { clip = DownloadHandlerAudioClip.GetContent(req); } catch { clip = null; } }
                if (clip == null || clip.length < 0.5f) { broken.Add(id); try { File.Delete(FileOf(id)); } catch { } }   // 壞掉的檔：刪掉，下次重新下載
                else { clip.name = "music_file_" + id; clips["f:" + id] = clip; used.Remove("f:" + id); used.Add("f:" + id); Evict("f:" + id); }
            }
            loading.Remove(id);
        }

        /// <summary>丟掉最久沒用、沒在播的曲子（合成的、下載的都算）</summary>
        void Evict(string keep)
        {
            for (int i = 0; i < used.Count && clips.Count > MaxClips; i++)
            {
                string old = used[i];
                if (old == keep || old == playing || (clips[old] != null && (a.clip == clips[old] || b.clip == clips[old]))) continue;
                if (clips[old]) Destroy(clips[old]);
                clips.Remove(old); used.RemoveAt(i); i--;
            }
        }

        /// <summary>下載：App 一開就開始把全部曲子下載下來（現在要播的那首優先）</summary>
        public static void PrefetchAll()
        {
            if (I == null) Ensure();
            if (!I.downloading) I.StartCoroutine(I.Download());
        }
        IEnumerator Download()
        {
            downloading = true;
            try { Directory.CreateDirectory(Dir); } catch { downloading = false; yield break; }
            while (true)
            {
                string next = null;
                if (priority != null && !FileReady(priority)) next = priority;
                else foreach (var t in Tracks) if (!FileReady(t)) { next = t; break; }   // 使用者要求：一開始就全部下載（不限 Wi-Fi）
                priority = null;
                if (next == null) break;
                string tmp = FileOf(next) + ".part";
                using (var req = new UnityWebRequest(TrackUrl + next + ".mp3", UnityWebRequest.kHttpVerbGET) { downloadHandler = new DownloadHandlerFile(tmp) { removeFileOnAbort = true }, timeout = 120 })
                {
                    yield return req.SendWebRequest();
                    bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode == 200;
                    try { if (ok) { if (File.Exists(FileOf(next))) File.Delete(FileOf(next)); File.Move(tmp, FileOf(next)); broken.Remove(next); } else if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    if (!ok) break;   // 沒網路：下次要播的時候再試
                }
            }
            downloading = false;
        }

        /// <summary>播一次短曲（勝利、失敗）：有下載好才播；播的時候背景音樂壓低</summary>
        public static void Jingle(string id)
        {
            if (!On || string.IsNullOrEmpty(id)) return;
            if (I == null) Ensure();
            if (!FileReady(id)) { if (System.Array.IndexOf(Tracks, id) >= 0) { I.priority = id; if (!I.downloading) I.StartCoroutine(I.Download()); } return; }
            I.StartCoroutine(I.PlayJingle(id));
        }
        IEnumerator PlayJingle(string id)
        {
            using (var req = UnityWebRequestMultimedia.GetAudioClip("file://" + FileOf(id), AudioType.MPEG))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null) yield break;
                if (jingleSrc.clip) { var old = jingleSrc.clip; jingleSrc.Stop(); jingleSrc.clip = null; Destroy(old); }
                jingleSrc.clip = clip; jingleSrc.volume = Volume; jingleSrc.Play();
                jingleUntil = Time.unscaledTime + clip.length + 0.5f;
            }
        }
    }
}
