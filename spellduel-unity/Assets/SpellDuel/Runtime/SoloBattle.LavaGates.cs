using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 熔岩地圖的身體障礙＝火山噴出來的岩漿塊（使用者回報：障礙物看起來是地上扁扁一攤，看不出是什麼、要夠高）。
    /// 全部是立體的：外面一層焦黑的岩殼，裂縫透出橘紅色的熔岩光，上面冒熱氣、有光暈。
    ///   ・跳：橫跨走道的一排岩漿塊，約 0.5～0.65m 高（看得出要跳過去）。
    ///   ・蹲：從牆邊的噴口橫噴過走道的熔岩流，在頭的高度（底部比站著的頭低一點），會脈動、滴下岩漿。
    ///   ・側步：半邊路上一大顆 2m 高的熔岩巨石。
    /// 玩家走近（13m 內）時，岩漿塊從天上掉下來砸在地上（看得出是火山噴出來的），蹲的熔岩流則從噴口噴出來。
    /// 判定照舊（SoloBattle.BodyGates.cs）。
    /// </summary>
    public partial class SoloBattle
    {
        static Material lavaGlowMat, lavaHotMat;

        static Material LavaGlow()
        {
            if (!lavaGlowMat) { lavaGlowMat = new Material(Shader.Find("Sprites/Default")) { name = "Lava glow", color = new Color(1f, 0.55f, 0.15f), renderQueue = 2460 }; }
            return lavaGlowMat;
        }
        static Material LavaHot()
        {
            if (!lavaHotMat) { lavaHotMat = new Material(Shader.Find("Sprites/Default")) { name = "Lava hot", color = new Color(1f, 0.86f, 0.4f), renderQueue = 2461 }; }
            return lavaHotMat;
        }

        /// <summary>熔岩地圖的障礙（只有熔岩主題；其他主題 null）</summary>
        GameObject LavaGate(int kind, string theme)
        {
            if (theme != "volcano") return null;
            var rng = new System.Random(kind * 7919 + bodyGates.Count * 104729);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject(kind == GateJump ? "Lava gate (jump)" : kind == GateDuck ? "Lava gate (duck)" : "Lava gate (side)");
            var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
            var crust = new Color(0.16f, 0.13f, 0.14f);
            Transform Piece(PrimitiveType t, Vector3 at, Vector3 size, Color c, Vector3 euler = default)
            {
                var g = Prim(t, c); g.transform.SetParent(body, false);
                g.transform.localPosition = at; g.transform.localScale = size; g.transform.localRotation = Quaternion.Euler(euler);
                return g.transform;
            }
            Transform Glow(PrimitiveType t, Vector3 at, Vector3 size, bool hot = false)
            {
                var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>());
                var r = g.GetComponent<Renderer>(); r.sharedMaterial = hot ? LavaHot() : LavaGlow(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.transform.SetParent(body, false); g.transform.localPosition = at; g.transform.localScale = size;
                return g.transform;
            }
            // 一顆岩漿塊：熔岩核心（發光）外面包幾片焦黑的岩殼，殼的縫透出光
            void Blob(Vector3 at, float size)
            {
                Glow(PrimitiveType.Sphere, at + Vector3.up * size * 0.5f, Vector3.one * size * 0.92f);
                Glow(PrimitiveType.Sphere, at + Vector3.up * size * 0.62f, Vector3.one * size * 0.45f, true);
                for (int i = 0; i < 5; i++)
                {
                    var dir = Quaternion.Euler(R(-40f, 60f), i * 72f + R(-15f, 15f), 0f) * Vector3.forward;
                    Piece(PrimitiveType.Sphere, at + Vector3.up * size * 0.5f + dir * size * 0.2f, new Vector3(size * R(0.62f, 0.78f), size * R(0.55f, 0.7f), size * R(0.62f, 0.78f)), Color.Lerp(crust, new Color(0.3f, 0.2f, 0.18f), R(0f, 0.5f)), new Vector3(R(0f, 360f), R(0f, 360f), R(0f, 360f)));
                }
                GlowHalo.Create(body, at + Vector3.up * size * 0.6f, new Color(1f, 0.45f, 0.15f, 0.55f), size * 2.2f, 0.35f);
            }
            float span = GateHalfW * 2f - 0.3f;
            var drop = root.AddComponent<LavaGateDrop>(); if (drop) drop.body = body; else body.gameObject.SetActive(true);   // 動畫元件建立失敗也要看得到岩漿塊
            if (kind == GateJump)
            {
                int n = 4;
                for (int i = 0; i < n; i++) Blob(new Vector3(-span * 0.5f + span * (i + 0.5f) / n + R(-0.12f, 0.12f), 0f, R(-0.1f, 0.1f)), R(0.55f, 0.68f));
                Glow(PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(span, 0.03f, 0.5f));   // 地上流成一條的熔岩
            }
            else if (kind == GateDuck)
            {
                if (drop) drop.jet = true;
                float head = (StandH > 0.5f ? StandH : 1.55f) - 0.25f, y = head + 0.12f;   // 熔岩流的底部約在 head - 0.1
                body.localPosition = new Vector3(-GateHalfW, 0f, 0f);   // 從左邊牆的噴口往右噴（縮放的原點在噴口）
                var jet = Glow(PrimitiveType.Cylinder, new Vector3(GateHalfW, y, 0f), new Vector3(0.36f, GateHalfW, 0.36f)); jet.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var core = Glow(PrimitiveType.Cylinder, new Vector3(GateHalfW, y + 0.03f, 0f), new Vector3(0.16f, GateHalfW, 0.16f), true); core.localRotation = Quaternion.Euler(0f, 0f, 90f);
                for (int i = 0; i < 6; i++) Piece(PrimitiveType.Sphere, new Vector3(R(0.3f, GateHalfW * 2f - 0.3f), y + R(-0.05f, 0.12f), R(-0.12f, 0.12f)), new Vector3(R(0.18f, 0.3f), R(0.1f, 0.16f), R(0.18f, 0.3f)), crust);   // 浮在表面的焦黑硬殼
                Blob(new Vector3(0.1f, y - 0.45f, 0f), 0.75f);   // 噴口那一端
                for (int i = 0; i < 3; i++) Glow(PrimitiveType.Sphere, new Vector3(R(0.8f, GateHalfW * 2f - 0.5f), y - R(0.3f, 0.7f), 0f), Vector3.one * R(0.07f, 0.12f));   // 滴下來的岩漿
                GlowHalo.Create(body, new Vector3(GateHalfW, y, 0f), new Color(1f, 0.5f, 0.15f, 0.5f), 3.6f, 0.4f);
            }
            else
            {
                float side = kind == GateWallLeft ? -1f : 1f;
                var c = new Vector3(side * GateHalfW * 0.5f, 0f, 0f);
                Blob(c, 1.9f);
                Blob(c + new Vector3(-side * 0.6f, 0f, 0.35f), 0.8f);
            }
            // 地上的提示線（紅＝跳、藍＝蹲、綠＝從這邊過）：不跟著掉落動畫
            var line = kind == GateJump ? new Color(1f, 0.4f, 0.3f) : kind == GateDuck ? new Color(0.4f, 0.6f, 1f) : new Color(0.4f, 1f, 0.5f);
            var b = Prim(PrimitiveType.Cube, line); b.transform.SetParent(root.transform, false);
            if (kind == GateJump || kind == GateDuck) { b.transform.localPosition = new Vector3(0f, 0.01f, -1.2f); b.transform.localScale = new Vector3(GateHalfW * 2f - 0.2f, 0.01f, 0.15f); }
            else { float s2 = kind == GateWallLeft ? -1f : 1f; b.transform.localPosition = new Vector3(-s2 * GateHalfW * 0.5f, 0.01f, -1.2f); b.transform.localScale = new Vector3(GateHalfW - 0.3f, 0.01f, 0.4f); }
            if (drop) body.gameObject.SetActive(false);
            return root;
        }
    }

    /// <summary>岩漿塊落下／熔岩噴出的動畫（獨立的類別：巢狀的 MonoBehaviour 在打包後可能建立不起來）</summary>
    public sealed class LavaGateDrop : MonoBehaviour
    {
        public Transform body; public bool jet; public float startAt = -1f;
        const float FallTime = 0.55f, JetTime = 0.45f;
        public bool Done => startAt >= 0f && Time.time - startAt > (jet ? JetTime : FallTime);
        public void Trigger() { if (startAt < 0f) { startAt = Time.time; if (body) body.gameObject.SetActive(true); } }
        void Update()
        {
            if (!body) return;
            if (startAt < 0f) { if (body.gameObject.activeSelf) body.gameObject.SetActive(false); return; }
            float k = Mathf.Clamp01((Time.time - startAt) / (jet ? JetTime : FallTime));
            if (jet) body.localScale = new Vector3(Mathf.SmoothStep(0.02f, 1f, k), 1f, 1f);                 // 熔岩流從噴口往對面伸過去
            else { float e = k * k; body.localPosition = new Vector3(0f, Mathf.Lerp(9f, 0f, e), 0f); }          // 從天上砸下來
            // 落地後輕輕脈動（像還在發熱）
            if (k >= 1f && !jet) { float p = 1f + 0.015f * Mathf.Sin(Time.time * 4f + transform.position.x); body.localScale = new Vector3(1f, p, 1f); }
        }
    }
}
