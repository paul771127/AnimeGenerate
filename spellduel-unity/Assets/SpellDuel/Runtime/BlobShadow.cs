using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 腳下的接觸陰影：一塊柔和的橢圓暗影貼在真實地板上（場地座標 y＝0），讓 AR 裡的角色看起來「站在」地上。
    /// 不算真的陰影（手機負擔很小）：一張程式產生的放射漸層貼圖，所有影子共用同一個材質與網格。
    /// 飄在空中的（海鳥、水母、跳起來的）：離地越高，影子越淡、越大一點。
    /// </summary>
    public class BlobShadow : MonoBehaviour
    {
        static Mesh quad; static Material mat;
        Transform target, shadow;
        float radius, depth;
        bool project;   // true＝投影到場地地板（會飄起來的：小怪、寵物）；false＝貼在 target 的腳底（根節點就在腳底的角色、Boss、居民）
        public const float MaxAlpha = 0.42f;

        /// <summary>幫 target 加上腳下的影子（radius＝橫向半徑，公尺；depth＝前後半徑，0＝和橫向一樣；projectToFloor＝會飄起來的，影子投到場地地板）</summary>
        public static BlobShadow Attach(Transform target, float radius, float depth = 0f, bool projectToFloor = false)
        {
            if (target == null) return null;
            var b = target.GetComponent<BlobShadow>();
            if (b == null) b = target.gameObject.AddComponent<BlobShadow>();
            b.target = target; b.radius = radius; b.depth = depth > 0f ? depth : radius; b.project = projectToFloor;
            return b;
        }

        void Start()
        {
            Init();
            var go = new GameObject("Contact Shadow");
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            shadow = go.transform;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (shadow == null || target == null) return;
            bool show = target.gameObject.activeInHierarchy && AnyRendererOn();
            if (shadow.gameObject.activeSelf != show) shadow.gameObject.SetActive(show);
            if (!show) return;
            float h = 0f;
            if (project)
            {
                // 投影到地板（場地座標 y＝0）；離地越高越淡
                var w = WorldFrame.ToWorld(target.position);
                h = Mathf.Max(0f, w.y);
                w.y = 0.006f;
                shadow.position = WorldFrame.FromWorld(w);
                var fwd = WorldFrame.DirToWorld(target.forward); fwd.y = 0f;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
                shadow.rotation = WorldFrame.Marker.rotation * Quaternion.LookRotation(fwd.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            }
            else
            {
                // 根節點就在腳底：貼在腳下（稍微抬高一點，避免和地板閃爍）
                var up = target.parent != null ? target.parent.up : Vector3.up;
                var fwd = Vector3.ProjectOnPlane(target.forward, up);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, up);
                shadow.position = target.position + up * 0.006f;
                shadow.rotation = Quaternion.LookRotation(fwd.normalized, up) * Quaternion.Euler(90f, 0f, 0f);
            }
            float s = target.lossyScale.y > 1e-3f ? target.lossyScale.y : 1f;
            float grow = 1f + Mathf.Min(1f, h * 0.25f);
            shadow.localScale = new Vector3(radius * 2f * s * grow, depth * 2f * s * grow, 1f);
            float a = Mathf.Clamp01(1f - h / 4f);
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_Color", new Color(0f, 0f, 0f, MaxAlpha * a));
            shadow.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }
        MaterialPropertyBlock block;
        Renderer[] rends; float nextScan;

        /// <summary>角色被隱藏（SetVisible(false)、淡出）時影子也不要畫</summary>
        bool AnyRendererOn()
        {
            if (rends == null || Time.time > nextScan) { rends = target.GetComponentsInChildren<Renderer>(); nextScan = Time.time + 1f; }
            foreach (var r in rends) if (r != null && r.enabled && r.transform != shadow && r.gameObject.activeInHierarchy) return true;
            return false;
        }

        void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }
        void OnDisable() { if (shadow != null) shadow.gameObject.SetActive(false); }

        static void Init()
        {
            if (quad != null) return;
            quad = new Mesh { name = "Contact Shadow Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateBounds();
            // 放射漸層：中心深、邊緣柔和淡出
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Contact Shadow" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                    px[y * N + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            mat = new Material(Shader.Find("Sprites/Default")) { name = "Contact Shadow", mainTexture = tex, renderQueue = 2990 };   // 在不透明物件之後、其他半透明之前
            mat.color = new Color(0f, 0f, 0f, MaxAlpha);
        }
    }
}
