using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 使用者要求：人（鏡頭）貼近障礙物時，障礙物變半透明，免得擋住視線看不到敵人與預警。
    ///   每幀取所有模型的外框離鏡頭最近的距離：FadeFar 以外照常；越近越透明，貼到 FadeNear 以內只剩 MinAlpha。
    ///   材質是共用快取，不能直接改透明度 → 變透明時換成這個物件自己的半透明複本，離開就換回原本的材質。
    ///   線條（預警線）、粒子不動。
    /// </summary>
    public sealed class NearFade : MonoBehaviour
    {
        public float FadeNear = 0.35f, FadeFar = 1.4f, MinAlpha = 0.22f;
        static Shader fadeShader;
        readonly List<Renderer> rends = new List<Renderer>();
        readonly List<Material[]> originals = new List<Material[]>();
        readonly List<Material[]> copies = new List<Material[]>();
        readonly List<Color[]> colors = new List<Color[]>();
        bool faded; float alpha = 1f;

        public static NearFade Attach(GameObject go) { var f = go.GetComponent<NearFade>(); return f ? f : go.AddComponent<NearFade>(); }

        void Collect()
        {
            rends.Clear(); originals.Clear(); copies.Clear(); colors.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                rends.Add(r); originals.Add(r.sharedMaterials); copies.Add(null); colors.Add(null);
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            if (rends.Count == 0) Collect();
            float d = float.MaxValue; var eye = cam.transform.position;
            foreach (var r in rends) if (r && r.enabled && r.gameObject.activeInHierarchy) d = Mathf.Min(d, Vector3.Distance(r.bounds.ClosestPoint(eye), eye));
            float want = d >= FadeFar ? 1f : Mathf.Lerp(MinAlpha, 1f, Mathf.InverseLerp(FadeNear, FadeFar, d));
            alpha = Mathf.MoveTowards(alpha, want, Time.deltaTime * 4f);
            if (alpha >= 0.995f) { if (faded) Restore(); return; }
            if (!faded) Swap();
            for (int i = 0; i < rends.Count; i++)
            {
                var cs = copies[i]; if (cs == null) continue;
                for (int k = 0; k < cs.Length; k++) if (cs[k]) { var c = colors[i][k]; c.a *= alpha; cs[k].color = c; }
            }
        }

        void Swap()
        {
            if (fadeShader == null) fadeShader = Shader.Find("Sprites/Default");
            if (fadeShader == null) return;
            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i]; if (!r) continue;
                if (copies[i] == null)
                {
                    var src = originals[i]; var cs = new Material[src.Length]; var col = new Color[src.Length];
                    for (int k = 0; k < src.Length; k++)
                    {
                        if (!src[k]) continue;
                        col[k] = src[k].HasProperty("_Color") ? src[k].color : Color.white;
                        cs[k] = new Material(fadeShader) { name = src[k].name + " (near fade)", renderQueue = 3000 };
                        if (src[k].HasProperty("_MainTex") && src[k].mainTexture) cs[k].mainTexture = src[k].mainTexture;
                    }
                    copies[i] = cs; colors[i] = col;
                }
                r.sharedMaterials = copies[i];
            }
            faded = true;
        }

        void Restore()
        {
            for (int i = 0; i < rends.Count; i++) if (rends[i]) rends[i].sharedMaterials = originals[i];
            faded = false; alpha = 1f;
        }

        void OnDisable() { if (faded) Restore(); }
        void OnDestroy()
        {
            if (faded) Restore();
            foreach (var cs in copies) if (cs != null) foreach (var m in cs) if (m) Destroy(m);
        }
    }
}
