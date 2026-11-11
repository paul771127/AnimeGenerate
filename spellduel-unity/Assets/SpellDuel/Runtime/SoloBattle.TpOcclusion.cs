using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 第三人稱：場景擋住主角時變半透明（使用者要求：視角被場景遮住、看不到主角時，場景要變半透明）。
    ///   ・每 0.1 秒從相機往主角（腳、身體、頭）拉三條線，碰到的場景物件（副本／小關地圖的牆、岩柱、景物、機關…exRoot 底下）換成半透明材質。
    ///   ・不再擋住就換回原本的材質。地面（很扁的）、角色、敵人、NPC、特效不處理。
    ///   ・半透明材質：Sprites/Default（保留原本的貼圖與顏色，透明度 FadeAlpha），每種原材質只做一份。
    /// </summary>
    public partial class SoloBattle
    {
        const float FadeAlpha = 0.28f, OccEvery = 0.1f, OccCacheEvery = 1f;

        class OccFaded { public Material[] mats; public MaterialPropertyBlock block; }
        readonly Dictionary<Renderer, OccFaded> occFaded = new Dictionary<Renderer, OccFaded>();
        readonly Dictionary<Material, Material> occFadeMat = new Dictionary<Material, Material>();
        readonly List<Renderer> occCands = new List<Renderer>();
        readonly HashSet<Renderer> occNow = new HashSet<Renderer>();
        float occNextAt, occCacheAt; Transform occCacheRoot;
        static Shader occShader;

        void UpdateTpOcclusion(bool on)
        {
            if (!on || !cam || Me == null || !exRoot) { if (occFaded.Count > 0) OccRestoreAll(); return; }
            if (Time.time < occNextAt) return;
            occNextAt = Time.time + OccEvery;
            // 候選：exRoot 底下的場景（每秒重抓一次；角色、敵人、NPC、特效不算）
            if (Time.time >= occCacheAt || occCacheRoot != exRoot)
            {
                if (occCacheRoot != exRoot) { OccRestoreAll(); foreach (var f in occFadeMat.Values) if (f) Destroy(f); occFadeMat.Clear(); }   // 換了一張地圖
                occCacheAt = Time.time + OccCacheEvery; occCacheRoot = exRoot;
                occCands.Clear();
                foreach (var r in exRoot.GetComponentsInChildren<Renderer>())
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    if (r.GetComponentInParent<SpriteEnemyView>() || r.GetComponentInParent<SpriteNpcView>() || r.GetComponentInParent<CharacterRig>()) continue;
                    occCands.Add(r);
                }
            }
            var camPos = cam.transform.position;
            var feet = WorldFrame.FromWorld(Me.Feet);
            var targets = new[] { feet + Vector3.up * 0.15f, feet + Vector3.up * 0.45f, feet + Vector3.up * 0.8f };
            occNow.Clear();
            foreach (var r in occCands)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (b.size.y < 0.3f) continue;                         // 地面、地上的貼圖
                if (b.Contains(feet + Vector3.up * 0.45f)) continue;  // 包住主角的大物件（整片場景）
                if ((b.center - camPos).sqrMagnitude > 900f) continue; // 太遠的不可能擋（30m）
                foreach (var t in targets)
                {
                    var d = t - camPos; float len = d.magnitude; if (len < 0.01f) continue;
                    if (b.IntersectRay(new Ray(camPos, d / len), out float hit) && hit < len - 0.05f) { occNow.Add(r); break; }
                }
            }
            // 不再擋住的換回去
            var back = new List<Renderer>();
            foreach (var kv in occFaded) if (!occNow.Contains(kv.Key)) back.Add(kv.Key);
            foreach (var r in back) OccRestore(r);
            // 新擋住的換成半透明
            foreach (var r in occNow) if (!occFaded.ContainsKey(r)) OccFade(r);
        }

        void OccFade(Renderer r)
        {
            if (!occShader) occShader = Shader.Find("Sprites/Default");
            if (!occShader) return;
            var src = r.sharedMaterials;
            if (src == null || src.Length == 0) return;
            foreach (var m in src) if (m && m.renderQueue >= 2500) return;   // 本來就是透明的（光暈、特效）
            MaterialPropertyBlock blk = null; Color? blkColor = null;
            if (r.HasPropertyBlock())
            {
                blk = new MaterialPropertyBlock(); r.GetPropertyBlock(blk);
                if (blk.HasColor("_Color")) blkColor = blk.GetColor("_Color");
                else if (blk.HasColor("_BaseColor")) blkColor = blk.GetColor("_BaseColor");
                r.SetPropertyBlock(null);
            }
            var dst = new Material[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var m = src[i]; if (!m) continue;
                Material f;
                if (blkColor.HasValue || !occFadeMat.TryGetValue(m, out f) || !f)
                {
                    f = new Material(occShader) { name = m.name + " (fade)", renderQueue = 3000 };
                    Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (tex) f.mainTexture = tex;
                    Color c = blkColor ?? (m.HasProperty("_Color") ? m.GetColor("_Color") : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
                    c.a = FadeAlpha; f.color = c;
                    if (!blkColor.HasValue) occFadeMat[m] = f;
                }
                dst[i] = f;
            }
            occFaded[r] = new OccFaded { mats = src, block = blk };
            r.sharedMaterials = dst;
        }

        void OccRestore(Renderer r)
        {
            if (!occFaded.TryGetValue(r, out var o)) return;
            occFaded.Remove(r);
            if (!r) return;
            // 這個材質是為了這個 renderer 的顏色另外做的（有 property block）：用完丟掉
            if (o.block != null) foreach (var m in r.sharedMaterials) if (m && !occFadeMat.ContainsValue(m)) Destroy(m);
            r.sharedMaterials = o.mats;
            if (o.block != null) r.SetPropertyBlock(o.block);
        }

        void OccRestoreAll()
        {
            foreach (var r in new List<Renderer>(occFaded.Keys)) OccRestore(r);
            occFaded.Clear(); occCands.Clear(); occCacheRoot = null;
        }
    }
}
