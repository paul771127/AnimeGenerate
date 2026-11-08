using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 身體障礙的地形外觀（使用者要求：加入地形高低起伏，例如前方高起就要跳、上面有橫倒的樹就要蹲）。
    /// 把原本的紅色低欄、藍色橫樑、橘色半邊牆換成跟地圖主題搭的地形：
    ///   ・跳：倒在地上的樹幹、隆起的岩脊或土坡、漂流木加珊瑚、矮木柵、碎石堆（最高約 0.4m）
    ///   ・蹲：橫倒架在頭上的大樹、低矮的岩石拱門、斷掉的船桅加破帆、掛旗的橫梁、倒塌的屋梁（底部比站著的頭低）
    ///   ・側步：倒下的大樹根、崩落的巨石、礁岩、木箱堆（擋住半邊路）
    /// 判定跟原本一樣（SoloBattle.BodyGates.cs），只換外觀；地上仍保留顏色提示線（紅＝跳、藍＝蹲、綠＝從這邊過）。
    /// AR 的地板是現實的平地，所以「高起」只是看起來高起：玩家跳過去之後還是在原來的高度。
    /// </summary>
    public partial class SoloBattle
    {
        struct GatePalette { public Color rock, wood, accent, leaf; public bool glow; }

        static GatePalette GatePal(string theme)
        {
            switch (theme)
            {
                case "volcano": return new GatePalette { rock = new Color(0.2f, 0.17f, 0.17f), wood = new Color(0.22f, 0.14f, 0.1f), accent = new Color(1f, 0.45f, 0.12f), leaf = new Color(0.3f, 0.22f, 0.18f), glow = true };
                case "sea": return new GatePalette { rock = new Color(0.36f, 0.42f, 0.45f), wood = new Color(0.55f, 0.42f, 0.3f), accent = new Color(0.95f, 0.5f, 0.55f), leaf = new Color(0.85f, 0.82f, 0.7f) };
                case "arena": return new GatePalette { rock = new Color(0.62f, 0.55f, 0.45f), wood = new Color(0.5f, 0.32f, 0.18f), accent = new Color(0.8f, 0.15f, 0.12f), leaf = new Color(0.9f, 0.75f, 0.3f) };
                case "canyon": return new GatePalette { rock = new Color(0.78f, 0.52f, 0.32f), wood = new Color(0.5f, 0.36f, 0.22f), accent = new Color(0.6f, 0.38f, 0.24f), leaf = new Color(0.55f, 0.6f, 0.3f) };
                case "castle": return new GatePalette { rock = new Color(0.36f, 0.35f, 0.4f), wood = new Color(0.25f, 0.18f, 0.14f), accent = new Color(0.55f, 0.5f, 0.7f), leaf = new Color(0.8f, 0.8f, 0.85f) };
                default: return new GatePalette { rock = new Color(0.45f, 0.44f, 0.38f), wood = new Color(0.42f, 0.28f, 0.16f), accent = new Color(0.35f, 0.5f, 0.25f), leaf = new Color(0.28f, 0.55f, 0.24f) };   // 森林
            }
        }

        /// <summary>提示訊息用的名稱</summary>
        static string GateHint(int kind, string theme)
        {
            if (kind == GateJump) return theme switch
            {
                "volcano" => "🟥 火山噴出來的岩漿塊擋在路上：跳過去！",
                "canyon" => "🟥 前方地面高起：跳上去！",
                "sea" => "🟥 漂流木擋路：跳過去！",
                "arena" => "🟥 矮木柵：跳過去！",
                "castle" => "🟥 碎石堆：跳過去！",
                _ => "🟥 倒在地上的樹幹：跳過去！",
            };
            if (kind == GateDuck) return theme switch
            {
                "volcano" => "🟦 熔岩從牆邊橫噴過來：蹲下鑽過去！",
                "canyon" => "🟦 低矮的岩石拱門：蹲下鑽過去！",
                "sea" => "🟦 斷掉的船桅橫在頭上：蹲下鑽過去！",
                "arena" => "🟦 低橫梁：蹲下鑽過去！",
                "castle" => "🟦 倒塌的屋梁：蹲下鑽過去！",
                _ => "🟦 前方有大樹橫倒在頭上：蹲下鑽過去！",
            };
            return theme == "volcano" ? "🟧 熔岩巨石擋住半邊路：從綠色那一邊過（往旁邊跨一步）！" : "🟧 半邊路被擋住了：從綠色那一邊過（往旁邊跨一步）！";
        }

        /// <summary>照主題做地形外觀（座標：z＝前進方向，x＝橫跨路面，障礙在 z=0）</summary>
        GameObject ThemedGate(int kind, string theme)
        {
            var pal = GatePal(theme);
            var rng = new System.Random(kind * 977 + bodyGates.Count * 131 + (theme ?? "").Length);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var root = new GameObject(kind == GateJump ? "Terrain gate (jump)" : kind == GateDuck ? "Terrain gate (duck)" : "Terrain gate (side)");
            Transform Add(PrimitiveType t, Vector3 at, Vector3 size, Color c, Vector3 euler = default)
            {
                var b = Prim(t, c); b.transform.SetParent(root.transform, false);
                b.transform.localPosition = at; b.transform.localScale = size; b.transform.localRotation = Quaternion.Euler(euler);
                return b.transform;
            }
            void Lump(Vector3 at, Vector3 size, Color c) => Add(PrimitiveType.Sphere, at, size, Color.Lerp(c, Color.black, R(0f, 0.18f)), new Vector3(R(-8f, 8f), R(0f, 360f), R(-8f, 8f)));
            // 橫躺的圓木：Unity 的圓柱是 Y 軸、高 2 → 轉 90° 讓它沿 x 方向
            void Log(Vector3 at, float len, float r, Color c, float tilt = 0f) => Add(PrimitiveType.Cylinder, at, new Vector3(r * 2f, len * 0.5f, r * 2f), c, new Vector3(0f, R(-4f, 4f), 90f + tilt));
            void Line(float x, float w, float depth, Color c) => Add(PrimitiveType.Cube, new Vector3(x, 0.01f, -1.2f), new Vector3(w, 0.01f, depth), c);
            float span = GateHalfW * 2f - 0.2f, half = span * 0.5f;
            float head = (StandH > 0.5f ? StandH : 1.55f) - 0.25f;   // 蹲的障礙底部約在 head - 0.1（跟原本的橫樑一樣）

            if (kind == GateJump)
            {
                switch (theme)
                {
                    case "volcano":
                        for (int i = 0; i < 7; i++) Lump(new Vector3(-half + span * (i + 0.5f) / 7f, 0.05f, R(-0.1f, 0.1f)), new Vector3(R(0.7f, 0.95f), R(0.6f, 0.75f), R(0.55f, 0.8f)), pal.rock);
                        Add(PrimitiveType.Cube, new Vector3(0f, 0.015f, -0.45f), new Vector3(span, 0.02f, 0.08f), pal.accent);   // 岩脊前面發光的熔岩裂縫
                        break;
                    case "canyon":
                        // 土坡：一片寬的隆起地面，上面幾層岩層
                        Add(PrimitiveType.Sphere, new Vector3(0f, -0.02f, 0f), new Vector3(span * 1.05f, 0.75f, 1.5f), pal.rock);
                        Add(PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.05f), new Vector3(span * 0.85f, 0.6f, 1.1f), Color.Lerp(pal.rock, Color.white, 0.12f));
                        for (int i = 0; i < 4; i++) Lump(new Vector3(R(-half, half), 0.3f, R(-0.2f, 0.3f)), new Vector3(R(0.18f, 0.3f), 0.15f, R(0.18f, 0.3f)), pal.accent);
                        break;
                    case "sea":
                        Log(new Vector3(0f, 0.2f, 0f), span, 0.18f, pal.wood);
                        for (int i = 0; i < 5; i++) Lump(new Vector3(R(-half, half), 0.1f, R(0.12f, 0.3f) * (i % 2 == 0 ? 1 : -1)), new Vector3(R(0.25f, 0.4f), R(0.3f, 0.45f), R(0.25f, 0.35f)), pal.accent);   // 珊瑚
                        break;
                    case "arena":
                        Add(PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(span, 0.1f, 0.08f), pal.wood);
                        Add(PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(span, 0.1f, 0.08f), pal.wood);
                        for (int i = -2; i <= 2; i++) Add(PrimitiveType.Cube, new Vector3(i * half * 0.48f, 0.19f, 0f), new Vector3(0.1f, 0.38f, 0.12f), Color.Lerp(pal.wood, Color.black, 0.2f));
                        break;
                    case "castle":
                        for (int i = 0; i < 9; i++) Add(PrimitiveType.Cube, new Vector3(R(-half, half), R(0.08f, 0.2f), R(-0.25f, 0.25f)), new Vector3(R(0.3f, 0.6f), R(0.18f, 0.3f), R(0.25f, 0.45f)), Color.Lerp(pal.rock, Color.white, R(0f, 0.15f)), new Vector3(R(-15f, 15f), R(0f, 90f), R(-15f, 15f)));
                        break;
                    default:   // 森林：倒在地上的樹幹＋青苔
                        Log(new Vector3(0f, 0.2f, 0f), span + 0.3f, 0.2f, pal.wood, R(-2f, 2f));
                        for (int i = 0; i < 4; i++) Lump(new Vector3(R(-half, half), 0.36f, R(-0.06f, 0.06f)), new Vector3(R(0.25f, 0.45f), 0.1f, 0.22f), pal.accent);
                        Add(PrimitiveType.Cylinder, new Vector3(half + 0.25f, 0.2f, 0f), new Vector3(0.5f, 0.02f, 0.5f), Color.Lerp(pal.wood, Color.white, 0.25f), new Vector3(0f, 0f, 90f));   // 斷面
                        break;
                }
                Line(0f, span, 0.15f, new Color(1f, 0.4f, 0.3f));   // 地上的提示線：在這裡起跳
            }
            else if (kind == GateDuck)
            {
                float y = head;   // 障礙中心高度（底部約 y - 0.1）
                switch (theme)
                {
                    case "volcano":
                    case "canyon":
                        // 低矮的岩石拱門：兩邊岩柱，頭上一道岩梁
                        for (int s = -1; s <= 1; s += 2)
                            for (int k = 0; k < 4; k++) Lump(new Vector3(s * (half + R(-0.05f, 0.15f)), y * (k + 0.5f) / 4f, R(-0.08f, 0.08f)), new Vector3(R(0.55f, 0.75f), y / 3.2f, R(0.5f, 0.7f)), pal.rock);
                        for (int i = 0; i < 7; i++) Lump(new Vector3(-half + span * (i + 0.5f) / 7f, y + 0.12f + R(0f, 0.1f), R(-0.06f, 0.06f)), new Vector3(R(0.7f, 0.9f), 0.44f, R(0.5f, 0.7f)), pal.rock);
                        if (pal.glow) Add(PrimitiveType.Cube, new Vector3(0f, y - 0.06f, -0.27f), new Vector3(span * 0.8f, 0.03f, 0.02f), pal.accent);   // 拱門底下發光的熔岩紋
                        break;
                    case "sea":
                        Log(new Vector3(0f, y, 0f), span + 0.6f, 0.1f, pal.wood, 5f);
                        Add(PrimitiveType.Cylinder, new Vector3(-half - 0.15f, y * 0.5f + 0.3f, 0.1f), new Vector3(0.24f, y * 0.5f + 0.3f, 0.24f), pal.wood, new Vector3(0f, 0f, -8f));   // 斜靠的斷桅
                        Add(PrimitiveType.Cube, new Vector3(0.3f, y + 0.35f, 0.02f), new Vector3(span * 0.7f, 0.6f, 0.02f), pal.leaf, new Vector3(0f, 0f, 3f));   // 破帆（掛在橫桅上面）
                        break;
                    case "arena":
                        Add(PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(span, 0.2f, 0.2f), pal.wood);
                        for (int s = -1; s <= 1; s += 2) Add(PrimitiveType.Cube, new Vector3(s * half, (y + 0.1f) * 0.5f, 0f), new Vector3(0.18f, y + 0.1f, 0.18f), Color.Lerp(pal.wood, Color.black, 0.2f));
                        for (int i = -1; i <= 1; i++) Add(PrimitiveType.Cube, new Vector3(i * half * 0.6f, y + 0.4f, -0.11f), new Vector3(0.45f, 0.6f, 0.01f), i == 0 ? pal.leaf : pal.accent);   // 旗子（往上掛，不擋路）
                        break;
                    case "castle":
                        Log(new Vector3(0f, y, 0f), span + 0.5f, 0.13f, pal.wood, -7f);
                        for (int i = 0; i < 6; i++) Add(PrimitiveType.Cube, new Vector3(R(-half, half) * 1.1f, R(0.08f, 0.2f), R(-0.4f, 0.4f)), new Vector3(R(0.25f, 0.45f), R(0.15f, 0.25f), R(0.2f, 0.35f)), pal.rock, new Vector3(R(-15f, 15f), R(0f, 90f), R(-15f, 15f)));
                        Add(PrimitiveType.Cube, new Vector3(-half - 0.1f, y * 0.5f, 0f), new Vector3(0.35f, y, 0.35f), pal.rock);   // 一邊靠著斷柱
                        break;
                    default:   // 森林：大樹橫倒，一端架在樹墩上，另一端的樹冠壓在另一邊
                        Log(new Vector3(0f, y + 0.05f, 0f), span + 1.2f, 0.17f, pal.wood, 4f);
                        Add(PrimitiveType.Cylinder, new Vector3(-half - 0.2f, y * 0.5f, 0f), new Vector3(0.5f, y * 0.5f, 0.5f), Color.Lerp(pal.wood, Color.black, 0.15f));   // 樹墩
                        for (int i = 0; i < 6; i++) Lump(new Vector3(half + R(-0.1f, 0.6f), R(0.4f, y + 0.5f), R(-0.4f, 0.4f)), Vector3.one * R(0.6f, 0.9f), pal.leaf);   // 另一邊的樹冠
                        for (int i = 0; i < 4; i++) Lump(new Vector3(R(-half, half * 0.6f), y + 0.25f, R(-0.1f, 0.1f)), new Vector3(R(0.35f, 0.55f), 0.28f, 0.35f), pal.leaf);   // 樹幹上的枝葉（往上長）
                        break;
                }
                Line(0f, span, 0.15f, new Color(0.4f, 0.6f, 1f));
            }
            else
            {
                float side = kind == GateWallLeft ? -1f : 1f;
                float cx = side * GateHalfW * 0.5f;
                switch (theme)
                {
                    case "arena":
                        for (int i = 0; i < 5; i++) Add(PrimitiveType.Cube, new Vector3(cx + R(-0.6f, 0.6f), 0.35f + (i / 3) * 0.7f, R(-0.15f, 0.15f)), Vector3.one * 0.7f, pal.wood, new Vector3(0f, R(-15f, 15f), 0f));   // 木箱堆
                        break;
                    case "castle":
                        for (int i = 0; i < 6; i++) Add(PrimitiveType.Cube, new Vector3(cx + R(-0.7f, 0.7f), R(0.3f, 1.4f), R(-0.2f, 0.2f)), new Vector3(R(0.5f, 0.9f), R(0.4f, 0.7f), R(0.4f, 0.6f)), pal.rock, new Vector3(R(-10f, 10f), R(0f, 40f), R(-10f, 10f)));
                        break;
                    case "forest":
                    case null:
                    case "":
                        Lump(new Vector3(cx, 0.9f, 0f), new Vector3(GateHalfW + 0.2f, 1.9f, 0.9f), pal.leaf);   // 倒下大樹的樹冠＋樹根
                        Lump(new Vector3(cx + side * 0.3f, 0.6f, 0.15f), new Vector3(1.2f, 1.2f, 0.8f), Color.Lerp(pal.wood, Color.black, 0.1f));
                        break;
                    default:   // 崩落的巨石、礁岩
                        Lump(new Vector3(cx, 0.85f, 0f), new Vector3(GateHalfW + 0.2f, 1.9f, 1.1f), pal.rock);
                        Lump(new Vector3(cx + side * 0.4f, 0.4f, 0.4f), new Vector3(0.9f, 0.8f, 0.8f), pal.rock);
                        if (pal.glow) Add(PrimitiveType.Cube, new Vector3(cx, 0.02f, -0.6f), new Vector3(GateHalfW, 0.02f, 0.06f), pal.accent);
                        break;
                }
                Add(PrimitiveType.Cube, new Vector3(-side * GateHalfW * 0.5f, 0.01f, -1.2f), new Vector3(GateHalfW - 0.3f, 0.01f, 0.4f), new Color(0.4f, 1f, 0.5f));   // 地上綠色：從這邊過
            }
            return root;
        }
    }
}
