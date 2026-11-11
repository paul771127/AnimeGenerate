using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 場上 NPC 的 2D 圖（使用者要求：NPC 也改成跟 2D 敵人一樣的日式漫畫奇幻風；不用戰鬥姿勢，多數只要站著、走路，偶爾有小動作）。
    ///   圖：Resources/SpriteNpcs/{id}_{idle|walk|gesture}.png，三張都在才用（沒有就照舊用 3D 角色模型）。
    ///   每張：橫的 8 格是方向（0 正面、2 面向畫面右邊、4 背面…同 SpriteEnemyView），直的是第幾張；每格 256。
    ///     idle 4 張（呼吸、站著）、walk 6 張（走路循環）、gesture 4 張（小動作：點頭、揮手、整理衣服…，播完回到 idle）。
    ///   站著時每 6～12 秒播一次小動作；受傷閃紅；倒下淡出。永遠正對鏡頭（只轉水平）。
    /// </summary>
    public sealed class SpriteNpcView : MonoBehaviour
    {
        public const int Dirs = 8;
        public const float FootPad = 0.06f, CellHeight = 2.4f, FadeIn = 0.6f;
        static readonly string[] ActNames = { "idle", "walk", "gesture" };
        static readonly int[] Frames = { 4, 6, 4 };

        public float MoveSpeed;
        /// <summary>
        /// 第三人稱（使用者回報：關卡裡的 NPC 沒有跟著鏡頭與視角變化，看起來非常高大）：
        /// 主角縮成 HeroScale，NPC 的圖也一起縮到差不多的身高，並且整張朝向鏡頭（從上往下看時往後仰），不再只轉水平。
        /// </summary>
        public static System.Func<bool> ThirdPersonOn;
        public const float TpScale = 0.4f;
        Texture2D[] tex;
        Transform quad; Mesh mesh; MeshRenderer mr; MaterialPropertyBlock block;
        readonly Vector2[] uv = new Vector2[4];
        int lastKey = -1; float walkPhase, born, nextGesture, gestureAt = -99f, flashUntil, deadAt = -1f;
        static Material mat;
        static readonly Dictionary<string, Texture2D[]> cache = new Dictionary<string, Texture2D[]>();

        static Texture2D[] Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var t)) return t;
            t = new Texture2D[ActNames.Length];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = Resources.Load<Texture2D>("SpriteNpcs/" + id + "_" + ActNames[i]);
                if (!t[i]) { t = null; break; }
                t[i].wrapMode = TextureWrapMode.Clamp; t[i].filterMode = FilterMode.Bilinear;
            }
            cache[id] = t;
            return t;
        }
        public static bool HasArt(string id) => Load(id) != null;

        /// <summary>
        /// 這個人物用哪一套圖：劇情人物照名字（跟對話立繪同一個 id，Story2D.Cast），翠蔭迷林的居民照 NPC id，
        /// 護送事件的旅人用 traveler。沒有圖＝null（用 3D 模型）。
        /// </summary>
        public static string KeyFor(string npcId, string name)
        {
            string k = Story2D.IdOf(name);
            if (k == null && npcId != null)
            {
                switch (npcId)
                {
                    case "elder": k = "jungle_elder"; break;
                    case "hunter": k = "jungle_hunter"; break;
                    case "lost": k = "jungle_explorer"; break;
                    case "outpost": k = "jungle_outpost"; break;
                    case "herbalist": k = "jungle_herbalist"; break;
                    case "guardian": k = "jungle_guardian"; break;
                    case "spirit": k = "jungle_spirit"; break;
                    case "ev_walker": k = "traveler"; break;
                }
            }
            return k != null && HasArt(k) ? k : null;
        }

        public static SpriteNpcView Create(string id, Transform parent = null)
        {
            var t = Load(id); if (t == null) return null;
            var go = new GameObject("Sprite NPC " + id);
            if (parent) go.transform.SetParent(parent, false);
            var v = go.AddComponent<SpriteNpcView>();
            v.tex = t;
            var q = new GameObject("Billboard"); q.transform.SetParent(go.transform, false);
            v.quad = q.transform;
            float h = CellHeight, w = h, foot = h * FootPad;
            v.mesh = new Mesh { name = "Sprite NPC quad" };
            v.mesh.vertices = new[] { new Vector3(-w / 2f, -foot, 0f), new Vector3(w / 2f, -foot, 0f), new Vector3(w / 2f, h - foot, 0f), new Vector3(-w / 2f, h - foot, 0f) };
            v.mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            v.mesh.uv = new Vector2[4];
            v.mesh.RecalculateBounds();
            q.AddComponent<MeshFilter>().sharedMesh = v.mesh;
            v.mr = q.AddComponent<MeshRenderer>();
            if (!mat) mat = new Material(Shader.Find("Sprites/Default")) { name = "Sprite NPC" };
            v.mr.sharedMaterial = mat;
            v.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; v.mr.receiveShadows = false;
            v.block = new MaterialPropertyBlock(); v.born = Time.time;
            v.nextGesture = Time.time + Random.Range(3f, 9f);
            BlobShadow.Attach(go.transform, 0.3f, 0.24f);   // 腳下的接觸陰影（2D 圖也要「站在」地上）
            v.LateUpdate();
            return v;
        }

        /// <summary>位置（腳底）與面向（世界方向）</summary>
        public void SetPose(Vector3 feet, Vector3 forward)
        {
            forward.y = 0f;
            transform.position = feet;
            if (forward.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
        public void PlayHit() { flashUntil = Time.time + 0.12f; }
        public void SetDead(bool d) { if (d && deadAt < 0f) deadAt = Time.time; else if (!d) deadAt = -1f; }
        /// <summary>馬上播一次小動作（對話、接任務時）</summary>
        public void Gesture() { gestureAt = Time.time; nextGesture = Time.time + Random.Range(6f, 12f); }

        int DirIndex(Camera cam)
        {
            var f = transform.forward; f.y = 0f;
            var toCam = cam.transform.position - transform.position; toCam.y = 0f;
            if (f.sqrMagnitude < 1e-6f || toCam.sqrMagnitude < 1e-6f) return 0;
            f.Normalize(); toCam.Normalize();
            var screenRight = -Vector3.Cross(Vector3.up, toCam);
            float a = Mathf.Atan2(Vector3.Dot(f, screenRight), Vector3.Dot(f, toCam)) * Mathf.Rad2Deg;
            return ((Mathf.RoundToInt(a / 45f) % Dirs) + Dirs) % Dirs;
        }

        void LateUpdate()
        {
            var cam = Camera.main; if (!cam || quad == null || tex == null) return;
            float dt = Time.deltaTime, now = Time.time;
            bool tp = ThirdPersonOn != null && ThirdPersonOn();
            float sc = tp ? TpScale : 1f;
            if (Mathf.Abs(transform.localScale.x - sc) > 1e-4f) transform.localScale = Vector3.one * sc;
            var look = cam.transform.position - quad.position; if (!tp) look.y = 0f;   // 第三人稱：連上下也正對鏡頭（以腳底為軸往後仰）
            if (look.sqrMagnitude > 1e-6f) quad.rotation = Quaternion.LookRotation(-look.normalized, Vector3.up);
            // 動作：走路 → 小動作（站著、時間到）→ 待機
            int a, frame;
            const float GestureLen = 1.1f;
            if (MoveSpeed > 0.12f && deadAt < 0f) { walkPhase += dt * Mathf.Clamp(MoveSpeed * 7f, 5f, 12f); a = 1; frame = Mathf.FloorToInt(walkPhase) % Frames[1]; gestureAt = -99f; }
            else
            {
                if (now >= nextGesture && deadAt < 0f) { gestureAt = now; nextGesture = now + Random.Range(6f, 12f); }
                float g = now - gestureAt;
                if (g >= 0f && g < GestureLen) { a = 2; frame = Mathf.Min(Frames[2] - 1, Mathf.FloorToInt(g / GestureLen * Frames[2])); }
                else { a = 0; frame = Mathf.FloorToInt(now * 4f + Mathf.Abs(GetInstanceID() % 1000) * 0.37f) % Frames[0]; }
            }
            int dir = DirIndex(cam), key = (a * 16 + frame) * Dirs + dir;
            var t = tex[a];
            if (key != lastKey)
            {
                lastKey = key;
                int rows = Frames[a];
                float u0 = dir / (float)Dirs, u1 = (dir + 1) / (float)Dirs, v1 = 1f - frame / (float)rows, v0 = 1f - (frame + 1) / (float)rows;
                float ix = 0.5f / t.width, iy = 0.5f / t.height;
                uv[0] = new Vector2(u0 + ix, v0 + iy); uv[1] = new Vector2(u1 - ix, v0 + iy); uv[2] = new Vector2(u1 - ix, v1 - iy); uv[3] = new Vector2(u0 + ix, v1 - iy);
                mesh.uv = uv;
            }
            float fade = Mathf.Clamp01((now - born) / FadeIn) * (deadAt >= 0f ? Mathf.Clamp01(1f - (now - deadAt) / 0.8f) : 1f);
            float fog = !SpriteEnemyView.CastleFog ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ExploreArt.CastleFogNear, ExploreArt.CastleFogFar, Vector3.Distance(cam.transform.position, quad.position)));
            var c = now < flashUntil ? new Color(fog, 0.55f * fog, 0.55f * fog, fade) : new Color(fog, fog, fog, fade);
            mr.GetPropertyBlock(block);
            block.SetTexture("_MainTex", t);   // Sprites/Default 的 _MainTex 是 PerRendererData：一定要放進 block
            block.SetColor("_Color", c); mr.SetPropertyBlock(block);
        }

        void OnDestroy() { if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
