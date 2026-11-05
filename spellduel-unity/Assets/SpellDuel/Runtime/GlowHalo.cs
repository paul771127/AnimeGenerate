using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 假的光暈（Unity 內建管線沒有後製 Bloom）：發光的東西（火盆、香火、魔法燈）旁邊放一片永遠面向鏡頭的疊加光圈，
    /// 會慢慢呼吸、偶爾閃爍，看起來像真的在發亮。所有光暈共用同一張放射漸層貼圖與材質（用 MaterialPropertyBlock 換顏色）。
    /// </summary>
    public class GlowHalo : MonoBehaviour
    {
        static Mesh quad; static Material mat; static Texture2D tex;
        MeshRenderer mr; MaterialPropertyBlock block;
        Color color; float size, seed, flicker;

        /// <summary>在 parent 底下的 localPos 放一片光暈（size＝直徑，公尺；flicker＝火光的閃爍程度 0～1）</summary>
        public static GlowHalo Create(Transform parent, Vector3 localPos, Color c, float size, float flicker = 0.3f)
        {
            Init();
            var go = new GameObject("Glow halo");
            go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            var h = go.AddComponent<GlowHalo>();
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            h.mr = go.AddComponent<MeshRenderer>(); h.mr.sharedMaterial = mat;
            h.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; h.mr.receiveShadows = false;
            h.block = new MaterialPropertyBlock(); h.color = c; h.size = size; h.flicker = flicker; h.seed = Random.value * 10f;
            return h;
        }

        static void Init()
        {
            if (quad) return;
            quad = new Mesh { name = "Glow halo quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateBounds();
            const int N = 64; tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float d = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
                float a = Mathf.Clamp01(1f - d); a = a * a * (0.6f + 0.4f * a);   // 中間亮、外圈柔
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            var sh = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            mat = new Material(sh) { name = "Glow halo", mainTexture = tex };
        }

        void LateUpdate()
        {
            var cam = Camera.main; if (!cam || mr == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
            float t = Time.time + seed;
            float k = 0.8f + 0.2f * Mathf.Sin(t * 1.7f) + flicker * 0.25f * (Mathf.PerlinNoise(t * 6f, seed) - 0.5f);
            transform.localScale = Vector3.one * size * (0.95f + 0.05f * Mathf.Sin(t * 2.3f));
            mr.GetPropertyBlock(block);
            block.SetTexture("_MainTex", tex);
            block.SetColor("_TintColor", new Color(color.r, color.g, color.b, color.a * 0.5f * k));   // Particles/Additive 用 _TintColor（×2）
            block.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * k));
            mr.SetPropertyBlock(block);
        }
    }
}
