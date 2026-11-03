using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 8 方向 2D 圖片的敵人外觀（使用者要求的測試版：日式 Q 版、每個動作都有 8 方向）。
    ///   圖：Resources/SpriteEnemies/{sheet}.png，2048×2048，每格 128×128（16×16 格）。
    ///   排法：第 slot 格＝(動作的第一格 + 第幾張) × 8 + 方向，slot 從左上往右、再往下數。
    ///     動作（張數）：待機 4、走路 6、預備 2、出手 3、受擊 2、倒下 4（共 21 張 × 8 方向＝168 格）。
    ///     方向 0～7：敵人面向相對鏡頭的角度，每 45° 一個；0＝面向鏡頭（正面），2＝面向畫面右邊，4＝背對鏡頭，6＝面向畫面左邊。
    ///   每一格裡角色的腳底在格子底邊往上 FootPad（0.06）的地方、水平置中；一格的高度對應 def.height 公尺。
    ///   顯示：一片永遠正對鏡頭（只轉水平方向）的四邊形，依面向與鏡頭的夾角選方向；Sprites/Default 材質（不受光，跟著場景亮度另外調色）。
    ///   介面跟 MinionRig 一樣（MoveSpeed、Windup、Attack、PlayHit、SetDead），由 SoloBattle 轉過來。
    ///
    ///   高解析度格式（優先用；6 張都在才用，不然退回上面的單張圖）：每個動作一張
    ///     Resources/SpriteEnemies/{sheet}_{idle|walk|windup|attack|hurt|die}.png
    ///     橫的 8 格＝方向 0～7（同上），直的每一列＝這個動作的第幾張（最上面＝第 0 張）；每格正方形，邊長＝圖寬 ÷ 8（建議 256）。
    ///     例：walk 是 6 張 → 2048×1536；idle、die 4 張 → 2048×1024；windup、hurt 2 張 → 2048×512；attack 3 張 → 2048×768。
    ///     腳底位置、高度的規則跟單張圖一樣（腳底在格子底邊往上 6%）。
    /// </summary>
    public sealed class SpriteEnemyView : MonoBehaviour
    {
        public const int Cell = 128, Grid = 16, Dirs = 8;
        public const float FootPad = 0.06f;
        public enum Act { Idle = 0, Walk = 1, Windup = 2, Attack = 3, Hurt = 4, Die = 5 }
        public static readonly int[] Frames = { 4, 6, 2, 3, 2, 4 };
        static readonly int[] First = { 0, 4, 10, 12, 15, 17 };

        public float MoveSpeed;
        SpriteMonsterDef def;
        Transform quad; Mesh mesh; MeshRenderer mr; MaterialPropertyBlock block;
        readonly Vector2[] uv = new Vector2[4];
        Act act = Act.Idle; float actT, actDur, hurtUntil, deadAt = -1f, flashUntil;
        int lastSlot = -1; bool dead; float walkPhase, born;
        public const float FadeIn = 0.6f;   // 出現時淡入（SpawnFade 換材質的透明度會被這裡每幀設的 _Color 蓋掉，所以自己做）

        public static readonly string[] ActNames = { "idle", "walk", "windup", "attack", "hurt", "die" };
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Texture2D[]> hiRes = new Dictionary<string, Texture2D[]>();
        /// <summary>高解析度的每動作圖（6 張都在才回傳；沒有＝null，用單張圖）</summary>
        static Texture2D[] HiResFor(string sheet)
        {
            if (hiRes.TryGetValue(sheet, out var t)) return t;
            t = new Texture2D[ActNames.Length];
            for (int i = 0; i < t.Length; i++)
            {
                t[i] = Resources.Load<Texture2D>("SpriteEnemies/" + sheet + "_" + ActNames[i]);
                if (!t[i]) { t = null; break; }
                t[i].wrapMode = TextureWrapMode.Clamp; t[i].filterMode = FilterMode.Bilinear;
            }
            hiRes[sheet] = t;
            return t;
        }
        Texture2D[] hi;
        static Material MatFor(string sheet)
        {
            if (mats.TryGetValue(sheet, out var m) && m) return m;
            var tex = Resources.Load<Texture2D>("SpriteEnemies/" + sheet);
            m = new Material(Shader.Find("Sprites/Default")) { name = "Sprite enemy " + sheet, mainTexture = tex };
            if (tex) { tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear; }
            mats[sheet] = m;
            return m;
        }

        public static SpriteEnemyView Create(SpriteMonsterDef d, Transform parent)
        {
            var go = new GameObject("Sprite enemy " + d.sheet);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SpriteEnemyView>();
            v.def = d;
            var q = new GameObject("Billboard");
            q.transform.SetParent(go.transform, false);
            q.transform.localPosition = Vector3.up * d.lift;
            v.quad = q.transform;
            v.hi = HiResFor(d.sheet);
            v.mesh = new Mesh { name = "Sprite enemy quad" };
            float h = v.hi != null && d.hiHeight > 0f ? d.hiHeight : d.height, w = h, foot = h * FootPad;
            v.mesh.vertices = new[] { new Vector3(-w / 2f, -foot, 0f), new Vector3(w / 2f, -foot, 0f), new Vector3(w / 2f, h - foot, 0f), new Vector3(-w / 2f, h - foot, 0f) };
            v.mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            v.mesh.uv = new Vector2[4];
            v.mesh.RecalculateBounds();
            q.AddComponent<MeshFilter>().sharedMesh = v.mesh;
            v.mr = q.AddComponent<MeshRenderer>();
            v.mr.sharedMaterial = MatFor(d.sheet);
            v.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            v.mr.receiveShadows = false;
            v.block = new MaterialPropertyBlock(); v.born = Time.time;
            v.LateUpdate();
            return v;
        }

        public void Windup(float duration) { if (dead) return; act = Act.Windup; actT = 0f; actDur = Mathf.Max(0.05f, duration); }
        public void Attack() { if (dead) return; act = Act.Attack; actT = 0f; actDur = 0.36f; }
        public void PlayHit() { if (dead) return; hurtUntil = Time.time + 0.28f; flashUntil = Time.time + 0.12f; }
        public void SetDead(bool d) { if (d == dead) return; dead = d; if (d) { act = Act.Die; actT = 0f; actDur = 0.6f; deadAt = Time.time; } }

        /// <summary>依面向（父物件的 forward）與鏡頭方向選 8 方向之一</summary>
        int DirIndex(Camera cam)
        {
            var f = transform.parent ? transform.parent.forward : transform.forward; f.y = 0f;
            var toCam = cam.transform.position - transform.position; toCam.y = 0f;
            if (f.sqrMagnitude < 1e-6f || toCam.sqrMagnitude < 1e-6f) return 0;
            f.Normalize(); toCam.Normalize();
            var screenRight = -Vector3.Cross(Vector3.up, toCam);   // 從鏡頭看過去的畫面右邊
            float a = Mathf.Atan2(Vector3.Dot(f, screenRight), Vector3.Dot(f, toCam)) * Mathf.Rad2Deg;   // 0＝面向鏡頭、+90＝面向畫面右邊
            return ((Mathf.RoundToInt(a / 45f) % Dirs) + Dirs) % Dirs;
        }

        void LateUpdate()
        {
            var cam = Camera.main; if (!cam || quad == null) return;
            float dt = Time.deltaTime; actT += dt;
            // 面對鏡頭（只轉水平）
            var look = cam.transform.position - quad.position; look.y = 0f;
            if (look.sqrMagnitude > 1e-6f) quad.rotation = Quaternion.LookRotation(-look.normalized, Vector3.up);
            // 這一張是哪個動作的第幾張
            Act a; int frame;
            if (dead) { a = Act.Die; frame = Mathf.Min(Frames[5] - 1, Mathf.FloorToInt(actT / actDur * Frames[5])); }
            else if (Time.time < hurtUntil && act != Act.Attack) { a = Act.Hurt; frame = Mathf.Min(1, Mathf.FloorToInt((0.28f - (hurtUntil - Time.time)) / 0.14f)); }
            else if (act == Act.Windup) { a = Act.Windup; frame = actT < actDur * 0.4f ? 0 : 1; }
            else if (act == Act.Attack && actT < actDur) { a = Act.Attack; frame = Mathf.Min(2, Mathf.FloorToInt(actT / actDur * 3f)); }
            else
            {
                if (act == Act.Attack) act = Act.Idle;
                if (MoveSpeed > 0.12f) { walkPhase += dt * Mathf.Clamp(MoveSpeed * 7f, 5f, 14f); a = Act.Walk; frame = Mathf.FloorToInt(walkPhase) % Frames[1]; }
                else { a = Act.Idle; frame = Mathf.FloorToInt(Time.time * 5f + Mathf.Abs(GetInstanceID() % 1000) * 0.37f) % Frames[0]; }
            }
            int slot = (First[(int)a] + frame) * Dirs + DirIndex(cam);
            if (slot != lastSlot)
            {
                lastSlot = slot;
                float u0, u1, v0, v1, ix, iy;
                if (hi != null)
                {
                    // 每動作一張：橫＝方向、直＝第幾張（最上面是第 0 張）
                    var t = hi[(int)a]; int rows = Frames[(int)a], dir = slot % Dirs;
                    u0 = dir / (float)Dirs; u1 = (dir + 1) / (float)Dirs; v1 = 1f - frame / (float)rows; v0 = 1f - (frame + 1) / (float)rows;
                    ix = 0.5f / t.width; iy = 0.5f / t.height;
                }
                else
                {
                    int cx = slot % Grid, cy = slot / Grid;
                    u0 = cx / (float)Grid; u1 = (cx + 1) / (float)Grid; v1 = 1f - cy / (float)Grid; v0 = 1f - (cy + 1) / (float)Grid;
                    ix = iy = 0.5f / (Cell * Grid);   // 往內縮半個像素，避免取到隔壁格
                }
                uv[0] = new Vector2(u0 + ix, v0 + iy); uv[1] = new Vector2(u1 - ix, v0 + iy); uv[2] = new Vector2(u1 - ix, v1 - iy); uv[3] = new Vector2(u0 + ix, v1 - iy);
                mesh.uv = uv;
            }
            // 受擊閃白、倒下淡出；場景偏暗時跟著暗一點
            float fade = deadAt >= 0f ? Mathf.Clamp01(1f - (Time.time - deadAt - 0.6f) / 0.5f) : 1f;
            fade *= Mathf.Clamp01((Time.time - born) / FadeIn);
            // 城堡的距離漸暗：跟牆、地板一樣，遠處由黑慢慢變出顏色
            float fog = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ExploreArt.CastleFogNear, ExploreArt.CastleFogFar, Vector3.Distance(cam.transform.position, quad.position)));
            var c = Time.time < flashUntil ? new Color(fog, 0.55f * fog, 0.55f * fog, fade) : new Color(fog, fog, fog, fade);
            // Sprites/Default 的 _MainTex 是 [PerRendererData]：用了 MaterialPropertyBlock 就一定要把圖也放進去，不然會取到預設的白圖（使用者回報「白背景」）
            mr.GetPropertyBlock(block);
            Texture tex = hi != null ? hi[(int)a] : mr.sharedMaterial ? mr.sharedMaterial.mainTexture : null;
            if (tex) block.SetTexture("_MainTex", tex);
            block.SetColor("_Color", c); mr.SetPropertyBlock(block);
        }

        void OnDestroy() { if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
