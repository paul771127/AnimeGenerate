using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 敵人出現時的淡入：把底下所有模型的材質暫時換成半透明的複本，透明度 0 → 1，結束後換回原本的材質。
    /// （角色的材質是共用快取，不能直接改透明度，所以用複本；淡入中被打到閃白，原本的材質會先被換回來，不影響。）
    /// </summary>
    public class SpawnFade : MonoBehaviour
    {
        public const float DefaultDuration = 0.9f;
        static Shader fadeShader;

        readonly List<Renderer> rends = new List<Renderer>();
        readonly List<Material[]> originals = new List<Material[]>();
        readonly List<Material[]> faded = new List<Material[]>();
        readonly List<Color[]> baseColors = new List<Color[]>();
        float t, duration;
        bool running;

        /// <summary>讓 go（含子物件）淡入出現</summary>
        public static void Play(GameObject go, float duration = DefaultDuration)
        {
            if (go == null) return;
            var f = go.GetComponent<SpawnFade>();
            if (f == null) f = go.AddComponent<SpawnFade>();
            f.Begin(duration);
        }

        void Begin(float d)
        {
            Restore();
            if (fadeShader == null) fadeShader = Shader.Find("Sprites/Default");
            if (fadeShader == null) return;
            duration = Mathf.Max(0.05f, d); t = 0f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                var mats = r.sharedMaterials;
                var copy = new Material[mats.Length]; var cols = new Color[mats.Length];
                bool any = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || !m.HasProperty("_Color")) { copy[i] = m; continue; }
                    cols[i] = m.color;
                    copy[i] = new Material(fadeShader) { color = new Color(m.color.r, m.color.g, m.color.b, 0f), renderQueue = 3000, name = m.name + " (fade)" };
                    any = true;
                }
                if (!any) continue;
                rends.Add(r); originals.Add(mats); faded.Add(copy); baseColors.Add(cols);
                r.sharedMaterials = copy;
            }
            running = rends.Count > 0;
        }

        void Update()
        {
            if (!running) return;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = k * k * (3f - 2f * k);
            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i];
                if (r == null) continue;
                var mats = r.sharedMaterials;
                // 淡入中原本的材質被換回來（例如被打到閃白結束）：這個部位就不再淡入
                if (mats.Length != faded[i].Length || (mats.Length > 0 && mats[0] != faded[i][0])) continue;
                for (int j = 0; j < faded[i].Length; j++)
                {
                    var m = faded[i][j];
                    if (m == null || m == originals[i][j]) continue;
                    var c = baseColors[i][j]; m.color = new Color(c.r, c.g, c.b, c.a * a);
                }
            }
            if (k >= 1f) Restore();
        }

        void Restore()
        {
            for (int i = 0; i < rends.Count; i++)
            {
                var r = rends[i];
                if (r != null)
                {
                    var mats = r.sharedMaterials;
                    if (mats.Length == faded[i].Length && (mats.Length == 0 || mats[0] == faded[i][0])) r.sharedMaterials = originals[i];
                }
                foreach (var m in faded[i]) if (m != null && System.Array.IndexOf(originals[i], m) < 0) Destroy(m);
            }
            rends.Clear(); originals.Clear(); faded.Clear(); baseColors.Clear();
            running = false;
        }

        void OnDestroy() => Restore();
    }
}
