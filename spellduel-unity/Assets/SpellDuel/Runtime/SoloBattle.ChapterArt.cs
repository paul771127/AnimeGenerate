using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 第一大關的手繪 2D 障礙物、陷阱、脫出點（CODEX_TASK_OBSTACLE_ART.md；圖在 Resources/StageArt/Obstacles/）。
    /// 有圖就用圖，沒圖照舊用程式拼的（SoloBattle.GateArt.cs、SoloBattle.Chapter.cs）。
    ///   ・跳／蹲／側步：一張立起來的圖橫跨走道、正對走來的方向（不轉向鏡頭）；三個版本隨機挑。
    ///     蹲：圖裡橫樑底邊在圖高 45%、地面在 4% → 照玩家身高縮放，讓橫樑底邊對到頭的高度。
    ///     側步：圖的右邊是自然收尾；牆在右邊時左右翻轉。
    ///   ・噴口：平貼地面的 idle／warn 兩張；噴發是 4 格的岩漿柱（轉向鏡頭）。
    ///   ・落石：石頭（轉向鏡頭）、落地後的碎石（平貼地面）。
    ///   ・脫出點：平貼地面、慢慢轉的魔法陣＋加亮的光柱。
    /// </summary>
    public partial class SoloBattle
    {
        static readonly Dictionary<string, Texture2D> obTex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Texture2D, Material> obMat = new Dictionary<Texture2D, Material>();
        static readonly Dictionary<Texture2D, Material> obAddMat = new Dictionary<Texture2D, Material>();

        static Texture2D ObTex(string name)
        {
            if (obTex.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("StageArt/Obstacles/" + name);
            if (t) { t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; }
            obTex[name] = t;
            return t;
        }

        static Material ObMat(Texture2D t, bool additive = false, int queue = 2950)
        {
            var dict = additive ? obAddMat : obMat;
            if (dict.TryGetValue(t, out var m) && m) return m;
            var sh = additive ? (Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default")) : Shader.Find("Sprites/Default");
            m = new Material(sh) { name = "Obstacle " + t.name, mainTexture = t, renderQueue = queue };
            if (additive && m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            dict[t] = m;
            return m;
        }

        /// <summary>
        /// 一張圖的四邊形。flat＝平貼地面（中心在原點）；否則立起來（x 橫向、y 往上，正面朝 -z），foot＝圖底邊往上多少是地面。
        /// u0／u1：只用圖的一部分（動畫格）；flip＝左右翻轉。
        /// </summary>
        static GameObject ObQuad(Transform parent, string name, Material mat, float w, float h, float foot, bool flat, float u0 = 0f, float u1 = 1f, bool flip = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = name };
            mesh.vertices = flat
                ? new[] { new Vector3(-w / 2f, 0f, -h / 2f), new Vector3(w / 2f, 0f, -h / 2f), new Vector3(w / 2f, 0f, h / 2f), new Vector3(-w / 2f, 0f, h / 2f) }
                : new[] { new Vector3(-w / 2f, -h * foot, 0f), new Vector3(w / 2f, -h * foot, 0f), new Vector3(w / 2f, h * (1f - foot), 0f), new Vector3(-w / 2f, h * (1f - foot), 0f) };
            float a = flip ? u1 : u0, b = flip ? u0 : u1;
            mesh.uv = new[] { new Vector2(a, 0f), new Vector2(b, 0f), new Vector2(b, 1f), new Vector2(a, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go;
        }

        /// <summary>換一個四邊形的 u 範圍（噴發動畫換格）</summary>
        static void ObSetU(GameObject q, float u0, float u1)
        {
            var mf = q ? q.GetComponent<MeshFilter>() : null; if (mf == null) return;
            mf.sharedMesh.uv = new[] { new Vector2(u0, 0f), new Vector2(u1, 0f), new Vector2(u1, 1f), new Vector2(u0, 1f) };
        }

        /// <summary>手繪的身體障礙（現在只有熔岩）；沒有圖＝null</summary>
        GameObject PaintedGate(int kind, string theme)
        {
            if (theme != "volcano") return null;
            int v = Random.Range(0, 3);
            string key = kind == GateJump ? "volcano_jump_" : kind == GateDuck ? "volcano_duck_" : "volcano_side_";
            var t = ObTex(key + v) ?? ObTex(key + "0");
            if (!t) return null;
            var root = new GameObject(kind == GateJump ? "Painted gate (jump)" : kind == GateDuck ? "Painted gate (duck)" : "Painted gate (side)");
            var mat = ObMat(t);
            const float Foot = 0.042f;
            if (kind == GateJump) ObQuad(root.transform, "Ridge", mat, 4.2f, 1.05f, Foot, false);
            else if (kind == GateDuck)
            {
                // 橫樑底邊（圖高 45%，離地 41%）對到「比站著的頭低一點」：跟原本的橫樑一樣在 head - 0.1
                float head = (StandH > 0.5f ? StandH : 1.55f) - 0.25f;
                float h = Mathf.Clamp((head - 0.1f) / 0.41f, 2.2f, 3.6f), w = Mathf.Min(h * 2f, 4.8f);
                ObQuad(root.transform, "Beam", mat, w, h, Foot, false);
            }
            else
            {
                float side = kind == GateWallLeft ? -1f : 1f;
                var q = ObQuad(root.transform, "Boulders", mat, GateHalfW + 0.3f, GateHalfW + 0.3f, Foot, false, 0f, 1f, side > 0f);   // 圖的右邊是自然收尾：牆在右邊就翻過來
                q.transform.localPosition = new Vector3(side * GateHalfW * 0.5f, 0f, 0f);
            }
            // 地上的顏色提示線照舊（紅＝跳、藍＝蹲、綠＝從這邊過）
            var line = kind == GateJump ? new Color(1f, 0.4f, 0.3f) : kind == GateDuck ? new Color(0.4f, 0.6f, 1f) : new Color(0.4f, 1f, 0.5f);
            var b = Prim(PrimitiveType.Cube, line); b.transform.SetParent(root.transform, false);
            if (kind == GateJump || kind == GateDuck) { b.transform.localPosition = new Vector3(0f, 0.01f, -1.2f); b.transform.localScale = new Vector3(GateHalfW * 2f - 0.2f, 0.01f, 0.15f); }
            else { float side = kind == GateWallLeft ? -1f : 1f; b.transform.localPosition = new Vector3(-side * GateHalfW * 0.5f, 0.01f, -1.2f); b.transform.localScale = new Vector3(GateHalfW - 0.3f, 0.01f, 0.4f); }
            return root;
        }

        /// <summary>噴口：有圖就用圖（core＝idle、ring＝warn、column＝噴發的岩漿柱）</summary>
        bool PaintedVent(ChVent v)
        {
            var idle = ObTex("volcano_vent_idle"); var warn = ObTex("volcano_vent_warn"); var erupt = ObTex("volcano_vent_erupt");
            if (!idle || !warn || !erupt) return false;
            v.go = new GameObject("Lava vent"); v.go.transform.SetParent(chRoot, false); v.go.transform.localPosition = v.pos;
            v.core = ObQuad(v.go.transform, "Vent", ObMat(idle, false, 2905), 1.6f, 1.6f, 0f, true); v.core.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            v.ring = ObQuad(v.go.transform, "Vent warn", ObMat(warn, false, 2906), 1.6f, 1.6f, 0f, true); v.ring.transform.localPosition = new Vector3(0f, 0.018f, 0f); v.ring.SetActive(false);
            v.column = ObQuad(v.go.transform, "Eruption", ObMat(erupt, false, 2960), 1.1f, 4.2f, 0.02f, false, 0f, 0.25f); v.column.SetActive(false);
            v.painted = true;
            v.go.SetActive(false);
            return true;
        }

        /// <summary>落石：有圖就用圖（石頭轉向鏡頭；落地後換成碎石）</summary>
        bool PaintedRock(ChRock rk)
        {
            var fall = ObTex("volcano_rock_fall"); var rub = ObTex("volcano_rock_rubble");
            if (!fall || !rub) return false;
            rk.rock = ObQuad(rk.go.transform, "Rock", ObMat(fall, false, 2960), 1.3f, 1.3f, 0.5f, false);
            rk.rubble = ObQuad(rk.go.transform, "Rubble", ObMat(rub, false, 2906), 2.4f, 2.4f, 0f, true); rk.rubble.transform.localPosition = new Vector3(0f, 0.02f, 0f); rk.rubble.SetActive(false);
            rk.rock.SetActive(false);
            return true;
        }

        /// <summary>脫出點：有圖就用圖（魔法陣平貼地面、慢慢轉；光柱加亮）</summary>
        GameObject PaintedExit()
        {
            var circle = ObTex("exit_circle"); var pillar = ObTex("exit_pillar");
            if (!circle || !pillar) return null;
            var root = new GameObject("Exit point");
            var c = ObQuad(root.transform, "Circle", ObMat(circle, false, 2907), 2.8f, 2.8f, 0f, true); c.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ObQuad(root.transform, "Pillar", ObMat(pillar, true, 2970), 0.7f, 3.4f, 0f, false);
            GlowHalo.Create(root.transform, new Vector3(0f, 1.0f, 0f), new Color(0.45f, 0.8f, 1f, 0.6f), 2.6f, 0.25f);
            return root;
        }

        /// <summary>每幀：轉向鏡頭的圖（噴發柱、落石、光柱）、魔法陣慢慢轉</summary>
        void FacePaintedChapter()
        {
            if (cam == null) return;
            var cp = cam.transform.position;
            void Face(GameObject g) { if (!g || !g.activeInHierarchy) return; var d = g.transform.position - cp; d.y = 0f; if (d.sqrMagnitude > 1e-4f) g.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up); }
            foreach (var v in chVents) if (v.painted) Face(v.column);
            foreach (var rk in chRocks) if (rk.rubble != null) Face(rk.rock);
            foreach (var e in chExits)
            {
                if (!e.go || !e.go.activeInHierarchy) continue;
                var c = e.go.transform.Find("Circle"); if (c) c.localRotation = Quaternion.Euler(0f, Time.time * 12f, 0f);
                var p = e.go.transform.Find("Pillar"); if (p) Face(p.gameObject);
            }
        }
    }
}
