using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 技能圖示：全部用程式即時畫出來（不需要任何圖檔）。
    ///   深色圓形徽章（職業色徑向漸層）＋金屬細邊框與寶石＋每個技能獨特的發光主圖（有向距離場繪製、反鋸齒）
    ///   ＋柔光暈（bloom）＋內部漸層高光＋星光點綴。
    /// Render() 是純 C# 計算（不呼叫任何 Unity 原生函式，不用 Mathf），可在 Unity 外做單元測試；
    /// Get() 才建立 Texture2D 並快取。像素順序同 Texture2D.SetPixels32：第 0 列是最下面。
    /// </summary>
    public static class SkillIcons
    {
        public const int Size = 128;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>取得技能圖示（128×128、無 mipmap、雙線性、Clamp；同一技能只產生一次）。</summary>
        public static Texture2D Get(string skillId)
        {
            skillId = skillId ?? "";
            if (cache.TryGetValue(skillId, out var t) && t != null) return t;
            t = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "SkillIcon " + skillId,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            t.SetPixels32(Render(skillId, Size));
            t.Apply(false, true);
            cache[skillId] = t;
            return t;
        }

        /// <summary>畫出技能圖示的像素（size×size，第 0 列在下）。純計算、結果固定（同輸入同輸出）。</summary>
        public static Color32[] Render(string skillId, int size)
        {
            size = size < 8 ? 8 : size > 1024 ? 1024 : size;
            skillId = skillId ?? "";
            V sc = V.Hex("#c8c8ff"), cc = V.Hex("#9090c0");
            if (Skills.All.TryGetValue(skillId, out var def))
            {
                sc = new V(def.color.r, def.color.g, def.color.b);
                if (Skills.Classes.TryGetValue(def.cls, out var cd)) cc = new V(cd.color.r, cd.color.g, cd.color.b);
            }
            var cv = new Cv(size);
            Background(cv, cc, sc, skillId);
            switch (skillId)
            {
                case "quickshot": QuickShot(cv, sc); break;
                case "snipe": Snipe(cv, sc); break;
                case "triple": Triple(cv, sc); break;
                case "snaretrap": SnareTrap(cv, sc); break;
                case "blasttrap": BlastTrap(cv, sc); break;
                case "fire": Fire(cv); break;
                case "ice": Ice(cv); break;
                case "thunder": Thunder(cv, sc); break;
                case "wind": Wind(cv, sc); break;
                case "meteor": Meteor(cv); break;
                case "heal": Heal(cv, sc); break;
                case "backstab": Backstab(cv, sc); break;
                case "shadow": Shadow(cv, sc); break;
                case "poison": Poison(cv, sc); break;
                case "knife": Knife(cv, sc); break;
                case "smoke": Smoke(cv, sc); break;
                case "slash": Slash(cv, cc); break;
                case "thrust": Thrust(cv, sc); break;
                case "wave": Wave(cv, sc); break;
                case "block": Block(cv, sc); break;
                case "ironwall": IronWall(cv, sc); break;
                case "counter": Counter(cv, sc); break;
                default: Orb(cv, sc); break;
            }
            Rim(cv, cc, sc);
            return cv.Output();
        }

        // ================================================================ 底座：徽章背景與邊框
        const float BadgeR = 0.95f;

        static void Background(Cv cv, V cls, V sc, string id)
        {
            var center = cls * 0.30f + sc * 0.14f + new V(0.02f, 0.02f, 0.05f);
            var edge = new V(0.025f, 0.02f, 0.05f);
            var outside = V.Lerp(cls, V.White, 0.35f);
            int seed = Hash(id);
            for (int j = 0; j < cv.n; j++)
                for (int i = 0; i < cv.n; i++)
                {
                    float x = cv.X(i), y = cv.X(j);
                    float r = Len(x, y);
                    int k = j * cv.n + i;
                    if (r > BadgeR + cv.px) { cv.r[k] = outside.r; cv.g[k] = outside.g; cv.b[k] = outside.b; continue; }
                    float t = SStep(0f, BadgeR, r);
                    var c = V.Lerp(center, edge, t * (2f - t));
                    // 背景的放射光紋＋細微雜訊，避免平面感
                    float ang = (float)Math.Atan2(y, x);
                    float rays = 0.5f + 0.5f * (float)Math.Sin(ang * 14f + seed % 7);
                    c = c + sc * (0.035f * rays * (1f - t));
                    float nz = ValueNoise(x * 6f + seed % 13, y * 6f) - 0.5f;
                    c = c * (1f + 0.22f * nz);
                    // 上方微光
                    c = c + cls * (0.05f * Clamp01(y) * (1f - t));
                    cv.r[k] = c.r; cv.g[k] = c.g; cv.b[k] = c.b;
                }
            // 主圖背後的柔光
            cv.Glow((x, y) => Len(x, y) - 0.05f, sc, 0.35f, 0.16f);
        }

        static void Rim(Cv cv, V cls, V sc)
        {
            var dark = cls * 0.22f;
            var light = V.Lerp(cls, V.White, 0.65f);
            // 內側亮線（技能色）
            cv.Glow((x, y) => Math.Abs(Len(x, y) - 0.84f) - 0.004f, sc, 0.02f, 0.45f);
            cv.Solid((x, y) => Math.Abs(Len(x, y) - 0.84f) - 0.006f, V.Lerp(sc, V.White, 0.35f), 0.9f);
            // 金屬外框：深色描邊＋上亮下暗的漸層＋高光帶
            cv.Solid((x, y) => Math.Abs(Len(x, y) - 0.895f) - 0.058f, new V(0.01f, 0.01f, 0.02f), 0.95f);
            cv.Fill((x, y) => Math.Abs(Len(x, y) - 0.895f) - 0.042f, (x, y, d) =>
            {
                float t = Clamp01(0.5f + 0.55f * (y * 0.85f - x * 0.25f));
                var c = V.Lerp(dark, light, t * t);
                float ring = Len(x, y) - 0.895f;          // -0.042..0.042
                float bevel = 1f - Math.Abs(ring) / 0.042f;
                c = c * (0.65f + 0.55f * bevel);
                c = c + V.White * (0.35f * Gauss(ring - 0.018f, 0.008f) * Clamp01(y + 0.3f));
                return c;
            });
            // 細的刻紋（外框中間的暗線）
            cv.Solid((x, y) => Math.Abs(Len(x, y) - 0.895f) - 0.004f, dark * 0.6f, 0.55f);
            // 四顆寶石（上下左右）＋斜角的小鉚釘
            for (int q = 0; q < 4; q++)
            {
                float a = (float)(Math.PI * 0.5 * q + Math.PI * 0.5);
                float gx = 0.895f * (float)Math.Cos(a), gy = 0.895f * (float)Math.Sin(a);
                float rot = a;
                cv.Solid((x, y) => Diamond(x, y, gx, gy, 0.085f, 0.06f, rot) - 0.012f, new V(0.01f, 0.01f, 0.02f));
                cv.Glow((x, y) => Diamond(x, y, gx, gy, 0.07f, 0.048f, rot), sc, 0.04f, 0.5f);
                cv.Fill((x, y) => Diamond(x, y, gx, gy, 0.07f, 0.048f, rot), Hot(sc * 0.75f, V.Lerp(sc, V.White, 0.85f), 0.035f));
                float ha = a + 0.785f;
                float hx = 0.895f * (float)Math.Cos(ha), hy = 0.895f * (float)Math.Sin(ha);
                cv.Fill((x, y) => Circle(x, y, hx, hy, 0.022f), (x, y, d) => V.Lerp(light, dark, Clamp01(0.5f + (y - hy - (x - hx)) * 18f)));
            }
        }

        // ================================================================ 弓箭手
        static void QuickShot(Cv cv, V sc)
        {
            var glow = V.Lerp(sc, new V(0.6f, 1f, 0.3f), 0.4f);
            // 速度線（箭的後方）
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                float o = i * 0.11f, l0 = 0.25f + Math.Abs(i) * 0.08f;
                float ax = -0.62f + o * 0.707f + 0.1f, ay = -0.62f - o * 0.707f + 0.1f;
                float bx = ax + l0 * 0.707f, by = ay + l0 * 0.707f;
                cv.Glow((x, y) => Seg(x, y, ax, ay, bx, by) - 0.008f, glow, 0.03f, 0.6f);
                cv.Fill((x, y) => SegTaper(x, y, ax, ay, bx, by, 0.003f, 0.018f), (x, y, d) => V.Lerp(glow, V.White, 0.5f), 0.85f);
            }
            var ar = new Arrow(-0.52f, -0.52f, 0.6f, 0.6f, 1.15f);
            DrawArrow(cv, ar, sc, glow);
            Sparkle(cv, 0.42f, 0.66f, 0.11f, V.White);
            Sparkle(cv, -0.55f, 0.25f, 0.06f, glow);
            Sparkle(cv, 0.3f, -0.5f, 0.05f, glow);
        }

        static void Snipe(Cv cv, V sc)
        {
            var red = new V(1f, 0.32f, 0.22f);
            // 瞄準鏡：外圈＋十字刻度＋中心紅點
            Sdf scope = (x, y) =>
            {
                float d = Math.Abs(Len(x, y) - 0.5f) - 0.026f;
                d = Math.Min(d, Math.Abs(Len(x, y) - 0.36f) - 0.008f);
                d = Math.Min(d, Box(x, y, 0f, 0.53f, 0.018f, 0.16f));
                d = Math.Min(d, Box(x, y, 0f, -0.53f, 0.018f, 0.16f));
                d = Math.Min(d, Box(x, y, 0.53f, 0f, 0.16f, 0.018f));
                d = Math.Min(d, Box(x, y, -0.53f, 0f, 0.16f, 0.018f));
                return d;
            };
            cv.Glow(scope, sc, 0.06f, 0.7f);
            cv.Solid((x, y) => scope(x, y) - 0.02f, Ink, 0.85f);
            cv.Fill(scope, Hot(sc, V.Lerp(sc, V.White, 0.8f), 0.02f));
            // 斜穿的箭
            var ar = new Arrow(-0.62f, -0.36f, 0.58f, 0.34f, 1.0f);
            DrawArrow(cv, ar, sc, V.Lerp(sc, new V(1f, 0.7f, 0.2f), 0.4f));
            // 準心命中點
            cv.Glow((x, y) => Circle(x, y, 0f, 0f, 0.03f), red, 0.05f, 1.2f);
            cv.Fill((x, y) => Circle(x, y, 0f, 0f, 0.035f), Hot(red, V.White, 0.03f));
            Sparkle(cv, 0.58f, 0.34f, 0.13f, V.White);
            Sparkle(cv, -0.45f, 0.5f, 0.05f, sc);
        }

        static void Triple(Cv cv, V sc)
        {
            var glow = V.Lerp(sc, new V(0.5f, 1f, 0.3f), 0.3f);
            float[] ang = { -24f, 24f, 0f };
            foreach (var a in ang)
            {
                float rad = (90f + a) * Deg;
                float ox = 0f, oy = -0.62f;
                float tx = ox + 1.22f * (float)Math.Cos(rad), ty = oy + 1.22f * (float)Math.Sin(rad);
                float sx = ox + 0.08f * (float)Math.Cos(rad), sy = oy + 0.08f * (float)Math.Sin(rad);
                DrawArrow(cv, new Arrow(sx, sy, tx, ty, a == 0f ? 0.95f : 0.85f), sc, glow);
            }
            Sparkle(cv, 0f, 0.62f, 0.1f, V.White);
            Sparkle(cv, -0.5f, 0.48f, 0.07f, glow);
            Sparkle(cv, 0.5f, 0.48f, 0.07f, glow);
        }

        static void SnareTrap(Cv cv, V sc)
        {
            var steelD = new V(0.28f, 0.3f, 0.36f);
            var steelL = new V(0.94f, 0.96f, 1f);
            // 正面看的捕獸夾：上下兩片半橢圓鋼顎，利齒上下交錯咬合；兩側是彈簧鉸鏈，下方有底座與鐵鍊
            const float rx = 0.56f, ry = 0.36f, th = 0.055f, gap = 0.13f;
            Sdf lowJaw = (x, y) => Math.Max(Math.Abs(Ellipse(x, y, 0f, -gap, rx, ry)) - th, y + gap);
            Sdf upJaw = (x, y) => Math.Max(Math.Abs(Ellipse(x, y, 0f, gap, rx, ry)) - th, -(y - gap));
            var lowT = new[] { -0.3f, -0.1f, 0.1f, 0.3f };
            var upT = new[] { -0.2f, 0f, 0.2f };
            var lowB = new float[lowT.Length]; var upB = new float[upT.Length];
            for (int k = 0; k < lowT.Length; k++) lowB[k] = -gap - (ry - th) * (float)Math.Sqrt(Math.Max(0f, 1f - lowT[k] * lowT[k] / (rx * rx))) + 0.01f;
            for (int k = 0; k < upT.Length; k++) upB[k] = gap + (ry - th) * (float)Math.Sqrt(Math.Max(0f, 1f - upT[k] * upT[k] / (rx * rx))) - 0.01f;
            Sdf teeth = (x, y) =>
            {
                float d = 9f;
                for (int k = 0; k < lowT.Length; k++) d = Math.Min(d, Tri(x, y, lowT[k] - 0.085f, lowB[k], lowT[k] + 0.085f, lowB[k], lowT[k], -0.03f));
                for (int k = 0; k < upT.Length; k++) d = Math.Min(d, Tri(x, y, upT[k] - 0.085f, upB[k], upT[k], 0.03f, upT[k] + 0.085f, upB[k]));
                return d;
            };
            Sdf plate = (x, y) => Box(x, y, 0f, -0.5f, 0.3f, 0.045f, 0f, 0.02f);
            Sdf chain = (x, y) => Math.Min(Math.Abs(Ellipse(x, y, 0.4f, -0.56f, 0.07f, 0.045f)) - 0.016f, Math.Abs(Ellipse(x, y, 0.53f, -0.6f, 0.045f, 0.065f)) - 0.016f);
            Sdf all = (x, y) => Math.Min(Math.Min(Math.Min(lowJaw(x, y), upJaw(x, y)), teeth(x, y)), Math.Min(plate(x, y), chain(x, y)));
            cv.Glow(all, sc, 0.09f, 1f);
            cv.Solid((x, y) => all(x, y) - 0.022f, Ink, 0.92f);
            // 嘴裡的橘色危險光
            cv.Glow((x, y) => Ellipse(x, y, 0f, 0f, 0.36f, 0.05f), sc, 0.1f, 1f);
            cv.Fill(chain, Metal(steelD, steelL, 60f));
            cv.Fill(plate, Metal(steelD, steelL, 90f));
            cv.Fill(teeth, (x, y, d) =>
            {
                // 每顆齒左亮右暗（兩個斜面），齒尖帶一點橘光
                float fx = x - (float)Math.Round(x / 0.1f) * 0.1f;
                var c = fx < 0 ? steelL : V.Lerp(steelD, steelL, 0.45f);
                return V.Lerp(c, V.Lerp(sc, V.White, 0.4f), SStep(0.09f, 0.0f, Math.Abs(y)) * 0.6f);
            });
            cv.Fill(lowJaw, Metal(steelD, steelL, 100f));
            cv.Fill(upJaw, Metal(steelD, steelL, 80f));
            // 鉸鏈彈簧（連接上下顎）
            foreach (float hx in new[] { -0.56f, 0.56f })
            {
                Sdf spring = (x, y) => Box(x, y, hx, 0f, 0.06f, gap + 0.04f, 0f, 0.03f);
                cv.Solid((x, y) => spring(x, y) - 0.022f, Ink);
                cv.Fill(spring, (x, y, d) => V.Lerp(steelD, steelL, 0.5f + 0.5f * (float)Math.Sin((y + x * 0.4f) * 70f)) * (0.7f + 0.4f * Clamp01(0.5f - (x - hx) * 6f)));
                cv.Glow((x, y) => Circle(x, y, hx, 0f, 0.015f), sc, 0.03f, 0.7f);
            }
            Sparkle(cv, -0.28f, 0.42f, 0.1f, V.White);
            Sparkle(cv, 0.0f, 0.0f, 0.1f, sc);
        }

        static void BlastTrap(Cv cv, V sc)
        {
            float bx = -0.08f, by = -0.12f, br = 0.4f;
            var fire = new V(1f, 0.55f, 0.15f);
            // 爆炸星（引信末端）
            float ex = 0.42f, ey = 0.46f;
            Sdf star = (x, y) => Star(x, y, ex, ey, 0.26f, 0.1f, 8, 0.2f);
            cv.Glow(star, new V(1f, 0.4f, 0.1f), 0.12f, 1.1f);
            cv.Fill(star, Hot(new V(1f, 0.35f, 0.08f), new V(1f, 0.97f, 0.7f), 0.08f));
            cv.Fill((x, y) => Star(x, y, ex, ey, 0.14f, 0.06f, 8, 0f), Hot(new V(1f, 0.85f, 0.3f), V.White, 0.05f));
            // 引信
            Sdf fuse = (x, y) => ArcT(x, y, 0.36f, 0.12f, 0.24f, 165f * Deg, -95f * Deg, 0.022f, 0.022f, false);
            cv.Solid((x, y) => fuse(x, y) - 0.018f, Ink);
            cv.Fill(fuse, (x, y, d) => new V(0.75f, 0.6f, 0.4f) * (0.7f + 0.3f * (float)Math.Sin((x + y) * 60f)));
            // 炸彈本體：深色亮面球＋紅色輪廓光
            Sdf body = (x, y) => Circle(x, y, bx, by, br);
            Sdf cap = (x, y) => Box(x, y, 0.17f, 0.17f, 0.1f, 0.07f, -45f * Deg, 0.02f);
            cv.Glow((x, y) => Math.Min(body(x, y), cap(x, y)), sc, 0.08f, 1f);
            cv.Solid((x, y) => Math.Min(body(x, y), cap(x, y)) - 0.025f, Ink);
            cv.Fill(cap, Metal(new V(0.25f, 0.22f, 0.25f), new V(0.8f, 0.75f, 0.75f), 45f));
            cv.Fill(body, Sphere(bx, by, br, new V(0.1f, 0.08f, 0.13f), new V(0.45f, 0.4f, 0.55f), sc, 0.9f));
            // 炸彈上的小骷髏（危險！）
            Skull(cv, bx + 0.02f, by - 0.04f, 0.16f, sc * 0.9f);
            Sparkle(cv, ex, ey, 0.2f, V.White);
            Sparkle(cv, 0.6f, 0.15f, 0.07f, fire);
            Sparkle(cv, 0.15f, 0.68f, 0.06f, fire);
        }

        // ================================================================ 法師
        static void Fire(Cv cv)
        {
            var red = new V(0.95f, 0.18f, 0.05f);
            var orange = new V(1f, 0.5f, 0.06f);
            var yellow = new V(1f, 0.86f, 0.3f);
            var white = new V(1f, 0.98f, 0.85f);
            Sdf outer = (x, y) => Math.Min(Flame(x, y, 0f, -0.42f, 1f, 0f),
                                     Math.Min(Flame(x, y, -0.28f, -0.4f, 0.55f, 1.3f), Flame(x, y, 0.27f, -0.42f, 0.6f, 2.1f)));
            cv.Glow(outer, orange, 0.14f, 1.1f);
            cv.Solid((x, y) => outer(x, y) - 0.02f, new V(0.15f, 0.02f, 0f), 0.85f);
            cv.Fill(outer, (x, y, d) => V.Lerp(red, orange, SStep(0f, 0.08f, -d)));
            Sdf mid = (x, y) => Flame(x, y, 0.02f, -0.44f, 0.74f, 0.7f);
            cv.Fill(mid, (x, y, d) => V.Lerp(orange, yellow, SStep(0f, 0.12f, -d)));
            Sdf core = (x, y) => Flame(x, y, 0.03f, -0.46f, 0.45f, 1.5f);
            cv.Fill(core, (x, y, d) => V.Lerp(yellow, white, SStep(0f, 0.08f, -d)));
            // 火星
            Ember(cv, -0.45f, 0.42f, 0.035f, orange);
            Ember(cv, 0.48f, 0.3f, 0.03f, yellow);
            Ember(cv, 0.3f, 0.62f, 0.025f, orange);
            Ember(cv, -0.2f, 0.66f, 0.02f, yellow);
            Sparkle(cv, 0.05f, -0.2f, 0.12f, white);
        }

        /// <summary>火焰形：下圓上尖的淚滴，往上越晃越大。base 在 (cx,cy)，scale 放大倍率。</summary>
        static float Flame(float x, float y, float cx, float cy, float scale, float phase)
        {
            float u = (x - cx) / scale, v = (y - cy) / scale;
            float wob = 0.09f * (float)Math.Sin(v * 6.5f + phase) * Clamp01(v * 0.9f);
            u += wob;
            // 尖端微微往右彎
            u -= 0.12f * v * v * 0.5f;
            float d = RoundCone(u, v, 0f, 0.3f, 0.3f, 0f, 1.2f, 0.01f);
            return d * scale;
        }

        static void Ember(Cv cv, float x0, float y0, float r, V c)
        {
            cv.Glow((x, y) => Circle(x, y, x0, y0, r * 0.5f), c, r * 1.5f, 0.9f);
            cv.Fill((x, y) => Circle(x, y, x0, y0, r * 0.6f), Hot(c, V.White, r * 0.5f));
        }

        static void Ice(Cv cv)
        {
            var cyan = new V(0.37f, 0.84f, 1f);
            var light = new V(0.88f, 0.98f, 1f);
            var deep = new V(0.06f, 0.3f, 0.62f);
            var shards = new[]
            {
                new Shard(-0.2f, -0.5f, 0.68f, 0.13f, 30f),
                new Shard(0.22f, -0.5f, 0.74f, 0.14f, -28f),
                new Shard(-0.04f, -0.55f, 1.08f, 0.17f, 4f),
                new Shard(-0.42f, -0.4f, 0.32f, 0.08f, 58f),
                new Shard(0.42f, -0.42f, 0.36f, 0.08f, -60f),
            };
            Sdf all = (x, y) =>
            {
                float d = 9f;
                foreach (var s in shards) d = Math.Min(d, s.D(x, y));
                return d;
            };
            cv.Glow(all, cyan, 0.12f, 0.95f);
            cv.Solid((x, y) => all(x, y) - 0.022f, new V(0.01f, 0.04f, 0.1f), 0.9f);
            int[] order = { 3, 4, 0, 1, 2 };
            foreach (int oi in order)
            {
                var s = shards[oi];
                cv.Solid((x, y) => s.D(x, y) - 0.012f, new V(0.01f, 0.04f, 0.1f), 0.9f);
                cv.Fill(s.D, (x, y, d) =>
                {
                    s.Local(x, y, out float u, out float v);
                    // 晶面：左面亮、右面暗，頂端最亮；中央稜線
                    float face = u < 0f ? 1f : 0.45f;
                    float top = SStep(s.len * 0.55f, s.len * 0.95f, v);
                    var c = V.Lerp(deep, cyan, face * (0.55f + 0.45f * SStep(-0.1f, s.len, v)));
                    c = V.Lerp(c, light, Math.Max(top * 0.7f, face * 0.35f * SStep(0.04f, 0f, -u - 0.0f) ));
                    c = c + V.White * (0.7f * Gauss(u, 0.006f) * SStep(0f, s.len * 0.7f, v));
                    c = c + light * (0.5f * (1f - SStep(0f, 0.025f, -d)));
                    return c;
                });
            }
            Sparkle(cv, -0.1f, 0.5f, 0.14f, V.White);
            Sparkle(cv, 0.42f, 0.18f, 0.08f, light);
            Sparkle(cv, -0.48f, 0.05f, 0.07f, light);
            Sparkle(cv, 0.12f, -0.25f, 0.05f, V.White);
        }

        sealed class Shard
        {
            public readonly float bx, by, len, w, ca, sa;
            readonly float[] pts;
            public Shard(float bx, float by, float len, float w, float angDeg)
            {
                this.bx = bx; this.by = by; this.len = len; this.w = w;
                float a = angDeg * Deg;
                ca = (float)Math.Cos(a); sa = (float)Math.Sin(a);
                pts = new[] { 0f, len, w, len * 0.68f, w * 0.85f, 0f, 0f, -0.06f, -w * 0.85f, 0f, -w, len * 0.68f };
            }
            public void Local(float x, float y, out float u, out float v)
            {
                float dx = x - bx, dy = y - by;
                u = dx * ca + dy * sa; v = -dx * sa + dy * ca;
            }
            public float D(float x, float y) { Local(x, y, out float u, out float v); return Poly(u, v, pts); }
        }

        static void Thunder(Cv cv, V sc)
        {
            var violet = V.Lerp(sc, new V(0.55f, 0.3f, 1f), 0.3f);
            var pts = new[] { 0.06f, 0.7f, -0.34f, 0.0f, -0.04f, 0.0f, -0.2f, -0.7f, 0.36f, 0.1f, 0.06f, 0.1f, 0.3f, 0.7f };
            Sdf bolt = (x, y) => Poly(x, y, pts) - 0.012f;
            // 背後的分岔小閃電
            Sdf fork = (x, y) => Math.Min(Math.Min(Seg(x, y, -0.3f, 0.02f, -0.52f, -0.18f), Seg(x, y, -0.52f, -0.18f, -0.5f, -0.4f)),
                                     Math.Min(Seg(x, y, 0.32f, 0.1f, 0.55f, 0.24f), Seg(x, y, 0.55f, 0.24f, 0.6f, 0.44f))) - 0.012f;
            cv.Glow(fork, violet, 0.05f, 0.8f);
            cv.Fill(fork, Hot(violet, V.White, 0.012f));
            cv.Glow(bolt, violet, 0.16f, 1.3f);
            cv.Glow(bolt, V.White, 0.035f, 0.35f);
            cv.Solid((x, y) => bolt(x, y) - 0.02f, new V(0.06f, 0f, 0.14f), 0.9f);
            cv.Fill(bolt, (x, y, d) =>
            {
                var c = V.Lerp(violet, new V(0.96f, 0.92f, 1f), SStep(0f, 0.06f, -d));
                return c + V.White * (0.25f * Clamp01(y + 0.3f));
            });
            Sparkle(cv, 0.06f, 0.7f, 0.1f, V.White);
            Sparkle(cv, -0.2f, -0.7f, 0.14f, V.White);
            Sparkle(cv, -0.5f, 0.45f, 0.06f, violet);
            Sparkle(cv, 0.55f, -0.35f, 0.06f, violet);
        }

        static void Wind(Cv cv, V sc)
        {
            var green = sc;
            var deep = new V(0.1f, 0.6f, 0.4f);
            Sdf gusts = (x, y) =>
            {
                float d = ArcT(x, y, 0.02f, 0.02f, 0.5f, 200f * Deg, -235f * Deg, 0.01f, 0.085f, true);
                d = Math.Min(d, ArcT(x, y, 0.06f, 0.0f, 0.3f, 15f * Deg, -230f * Deg, 0.008f, 0.07f, true));
                d = Math.Min(d, ArcT(x, y, 0.02f, 0.04f, 0.12f, 200f * Deg, -220f * Deg, 0.006f, 0.05f, true));
                return d;
            };
            Sdf streaks = (x, y) => Math.Min(SegTaper(x, y, -0.68f, -0.52f, 0.0f, -0.52f, 0.005f, 0.03f),
                                    Math.Min(SegTaper(x, y, -0.2f, 0.6f, 0.38f, 0.6f, 0.03f, 0.005f),
                                             SegTaper(x, y, 0.3f, -0.66f, 0.62f, -0.4f, 0.025f, 0.004f)));
            cv.Glow(streaks, green, 0.04f, 0.6f);
            cv.Fill(streaks, Hot(green, V.White, 0.02f), 0.9f);
            cv.Glow(gusts, green, 0.1f, 1f);
            cv.Solid((x, y) => gusts(x, y) - 0.02f, new V(0f, 0.06f, 0.04f), 0.85f);
            cv.Fill(gusts, (x, y, d) => V.Lerp(V.Lerp(deep, green, 0.6f), new V(0.9f, 1f, 0.92f), SStep(0f, 0.05f, -d)));
            // 葉片
            Leaf(cv, 0.52f, 0.32f, 0.12f, 40f, green);
            Leaf(cv, -0.58f, 0.2f, 0.09f, -30f, green);
            Leaf(cv, 0.22f, -0.42f, 0.08f, 120f, green);
            Sparkle(cv, -0.3f, 0.45f, 0.08f, V.White);
        }

        static void Leaf(Cv cv, float lx, float ly, float s, float angDeg, V c)
        {
            float a = angDeg * Deg, ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            Sdf leaf = (x, y) =>
            {
                float dx = x - lx, dy = y - ly;
                float u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                // 兩個圓的交集＝兩端尖的葉片
                return Math.Max(Circle(u, v, 0f, -s * 0.75f, s * 1.25f), Circle(u, v, 0f, s * 0.75f, s * 1.25f));
            };
            cv.Glow(leaf, c, 0.04f, 0.7f);
            cv.Solid((x, y) => leaf(x, y) - 0.015f, new V(0f, 0.06f, 0.03f), 0.85f);
            cv.Fill(leaf, (x, y, d) =>
            {
                float dx = x - lx, dy = y - ly;
                float v = -dx * sa + dy * ca;
                var col = V.Lerp(c * 0.6f, V.Lerp(c, V.White, 0.5f), SStep(0f, 0.04f, -d));
                return col * (v > 0 ? 1.1f : 0.8f) + V.White * (0.4f * Gauss(v, 0.006f));
            });
        }

        static void Meteor(Cv cv)
        {
            float mx = 0.2f, my = -0.2f, mr = 0.27f;
            var red = new V(1f, 0.2f, 0.12f);
            var orange = new V(1f, 0.55f, 0.1f);
            var yellow = new V(1f, 0.9f, 0.45f);
            // 火焰尾巴（往左上）
            Sdf tail = (x, y) =>
            {
                float wob = 0.03f * (float)Math.Sin((x - y) * 9f);
                return RoundCone(x + wob, y - wob, mx, my, mr + 0.06f, -0.62f, 0.62f, 0.02f);
            };
            Sdf tail2 = (x, y) =>
            {
                float wob = 0.025f * (float)Math.Sin((x - y) * 13f + 1f);
                return RoundCone(x + wob, y - wob, mx, my, mr - 0.02f, -0.4f, 0.4f, 0.01f);
            };
            Sdf tail3 = (x, y) => RoundCone(x, y, mx, my, mr - 0.1f, -0.18f, 0.18f, 0.01f);
            cv.Glow(tail, red, 0.14f, 1f);
            cv.Fill(tail, (x, y, d) => V.Lerp(red, orange, SStep(0f, 0.1f, -d)), 0.95f);
            cv.Fill(tail2, (x, y, d) => V.Lerp(orange, yellow, SStep(0f, 0.08f, -d)), 0.95f);
            cv.Fill(tail3, (x, y, d) => V.Lerp(yellow, V.White, SStep(0f, 0.06f, -d)), 0.95f);
            // 岩石本體：凹凸的輪廓＋熔岩裂紋
            Sdf rock = (x, y) =>
            {
                float a = (float)Math.Atan2(y - my, x - mx);
                float bump = 0.025f * (float)Math.Sin(a * 5f + 0.7f) + 0.015f * (float)Math.Sin(a * 9f + 2f);
                return Circle(x, y, mx, my, mr) - bump;
            };
            cv.Solid((x, y) => rock(x, y) - 0.024f, new V(0.08f, 0.02f, 0f));
            var sphere = Sphere(mx, my, mr, new V(0.12f, 0.07f, 0.06f), new V(0.55f, 0.4f, 0.32f), orange, 0.9f);
            cv.Fill(rock, (x, y, d) =>
            {
                var c = sphere(x, y, d);
                float n = Fbm(x * 7f + 3f, y * 7f);
                c = c * (0.75f + 0.5f * n);
                // 熔岩裂紋
                float crack = Gauss(Fbm(x * 4.5f + 10f, y * 4.5f + 4f) - 0.5f, 0.025f);
                c = V.Lerp(c, V.Lerp(orange, yellow, 0.4f), crack * 0.9f);
                // 迎風面（左上）被燒紅
                float hot = Clamp01(((mx - x) + (y - my)) / mr * 0.7f);
                return c + orange * (0.35f * hot);
            });
            // 撞擊坑
            cv.Fill((x, y) => Circle(x, y, mx + 0.08f, my - 0.08f, 0.06f), (x, y, d) => new V(0.08f, 0.04f, 0.03f), 0.6f);
            cv.Fill((x, y) => Circle(x, y, mx - 0.1f, my - 0.12f, 0.035f), (x, y, d) => new V(0.08f, 0.04f, 0.03f), 0.5f);
            Ember(cv, -0.55f, 0.15f, 0.03f, orange);
            Ember(cv, -0.1f, 0.62f, 0.03f, yellow);
            Ember(cv, -0.3f, 0.35f, 0.02f, yellow);
            Sparkle(cv, -0.05f, 0.05f, 0.1f, yellow);
        }

        static void Heal(Cv cv, V sc)
        {
            var light = new V(0.85f, 1f, 0.88f);
            Sdf cross = (x, y) => Math.Min(Box(x, y, 0f, 0f, 0.14f, 0.46f, 0f, 0.06f), Box(x, y, 0f, 0.02f, 0.46f, 0.14f, 0f, 0.06f));
            // 背後的光環與光芒
            cv.Glow((x, y) => Math.Abs(Len(x, y) - 0.6f) - 0.01f, sc, 0.05f, 0.5f);
            cv.Glow((x, y) => Star(x, y, 0f, 0f, 0.7f, 0.08f, 8, 0.39f), sc, 0.06f, 0.35f);
            cv.Glow(cross, sc, 0.16f, 1.1f);
            cv.Solid((x, y) => cross(x, y) - 0.025f, new V(0f, 0.08f, 0.02f), 0.9f);
            cv.Fill(cross, (x, y, d) =>
            {
                var c = V.Lerp(sc * 0.75f, light, SStep(0f, 0.11f, -d));
                return c + V.White * (0.25f * Clamp01(y - x + 0.2f) * (1f - SStep(0f, 0.04f, -d)));
            });
            // 小葉子
            Leaf(cv, 0.3f, 0.36f, 0.09f, 45f, sc);
            Sparkle(cv, 0f, 0f, 0.22f, V.White);
            Sparkle(cv, -0.45f, 0.42f, 0.09f, light);
            Sparkle(cv, 0.45f, -0.4f, 0.08f, light);
            Sparkle(cv, -0.36f, -0.5f, 0.06f, sc);
            Sparkle(cv, 0.55f, 0.1f, 0.05f, sc);
        }

        // ================================================================ 刺客
        static void Backstab(Cv cv, V sc)
        {
            // 匕首整體放大 K 倍：局部座標除以 K、距離再乘回 K
            const float K = 1.22f;
            float a = 18f * Deg, ca = (float)Math.Cos(a), sa = (float)Math.Sin(a), lc = ca / K, ls = sa / K;
            float ox = 0.0f, oy = 0.06f;
            var blood = new V(0.95f, 0.08f, 0.15f);
            var steelD = new V(0.35f, 0.33f, 0.42f);
            var steelL = new V(0.95f, 0.95f, 1f);
            var gold = new V(0.95f, 0.72f, 0.3f);
            var blade = new[] { -0.085f, 0.1f, 0.085f, 0.1f, 0.07f, -0.36f, 0f, -0.7f, -0.07f, -0.36f };
            Sdf B = (x, y) => { L(x, y, ox, oy, lc, ls, out float u, out float v); return Poly(u, v, blade) * K; };
            Sdf guard = (x, y) => { L(x, y, ox, oy, lc, ls, out float u, out float v); return Math.Min(Box(u, v, 0f, 0.14f, 0.24f, 0.04f, 0f, 0.03f), Math.Min(Circle(u, v, 0.26f, 0.17f, 0.05f), Circle(u, v, -0.26f, 0.17f, 0.05f))) * K; };
            Sdf grip = (x, y) => { L(x, y, ox, oy, lc, ls, out float u, out float v); return Box(u, v, 0f, 0.33f, 0.05f, 0.15f, 0f, 0.02f) * K; };
            Sdf pommel = (x, y) => { L(x, y, ox, oy, lc, ls, out float u, out float v); return Circle(u, v, 0f, 0.53f, 0.075f) * K; };
            Sdf all = (x, y) => Math.Min(Math.Min(B(x, y), guard(x, y)), Math.Min(grip(x, y), pommel(x, y)));
            cv.Glow(all, sc, 0.12f, 0.8f);
            cv.Glow(B, blood, 0.06f, 0.8f);
            cv.Solid((x, y) => all(x, y) - 0.022f, Ink, 0.95f);
            cv.Fill(B, (x, y, d) =>
            {
                L(x, y, ox, oy, lc, ls, out float u, out float v);
                var c = u < 0 ? V.Lerp(steelD, steelL, 0.85f) : V.Lerp(steelD, steelL, 0.3f);
                c = c * (0.8f + 0.25f * SStep(-0.7f, 0.1f, v));
                // 刃緣血紅光
                float edge = 1f - SStep(0f, 0.035f, -d);
                c = V.Lerp(c, V.Lerp(blood, V.White, 0.2f), edge * 0.85f);
                // 血槽
                c = c * (1f - 0.35f * Gauss(u, 0.008f) * SStep(-0.45f, 0.05f, v));
                return c;
            });
            cv.Fill(grip, (x, y, d) =>
            {
                L(x, y, ox, oy, lc, ls, out float u, out float v);
                float wrap = 0.5f + 0.5f * (float)Math.Sin((v + u * 0.6f) * 70f);
                return V.Lerp(new V(0.18f, 0.08f, 0.2f), new V(0.45f, 0.25f, 0.45f), wrap);
            });
            cv.Fill(guard, Metal(gold * 0.45f, V.Lerp(gold, V.White, 0.5f), 80f));
            cv.Fill(pommel, Hot(sc * 0.7f, V.Lerp(sc, V.White, 0.8f), 0.05f));
            // 血滴
            L2W(0f, -0.8f * K, ox, oy, ca, sa, out float dx1, out float dy1);
            Sdf drop = (x, y) => RoundCone(x, y, dx1, dy1 - 0.02f, 0.035f, dx1, dy1 + 0.06f, 0.0f);
            cv.Glow(drop, blood, 0.04f, 0.8f);
            cv.Fill(drop, Hot(blood, new V(1f, 0.6f, 0.6f), 0.03f));
            L2W(0.07f * K, -0.36f * K, ox, oy, ca, sa, out float gx, out float gy);
            Sparkle(cv, gx, gy, 0.12f, V.White);
            Sparkle(cv, -0.5f, 0.35f, 0.06f, sc);
        }

        static void Shadow(Cv cv, V sc)
        {
            var violet = V.Lerp(sc, new V(0.55f, 0.25f, 1f), 0.35f);
            // 背後的紫色新月
            Sdf moon = (x, y) => Math.Max(Circle(x, y, 0.12f, 0.12f, 0.6f), -Circle(x, y, 0.3f, 0.26f, 0.56f));
            cv.Glow(moon, violet, 0.08f, 0.9f);
            cv.Fill(moon, Hot(violet * 0.8f, new V(0.95f, 0.85f, 1f), 0.08f));
            // 兜帽人影
            Sdf hood = (x, y) =>
            {
                float head = RoundCone(x, y, 0f, 0.0f, 0.3f, 0.06f, 0.5f, 0.04f);
                float body = Poly(x, y, Cloak);
                return Smin(head, body, 0.12f);
            };
            cv.Glow(hood, violet, 0.07f, 1.1f);
            cv.Fill(hood, (x, y, d) =>
            {
                var c = V.Lerp(new V(0.03f, 0.01f, 0.06f), new V(0.12f, 0.05f, 0.2f), Clamp01(0.5f + y * 0.6f - x * 0.4f));
                float rim = 1f - SStep(0f, 0.035f, -d);
                return V.Lerp(c, V.Lerp(violet, V.White, 0.3f), rim * 0.9f);
            });
            // 臉部陰影與發光雙眼
            Sdf face = (x, y) => Ellipse(x, y, 0f, 0.0f, 0.17f, 0.2f);
            cv.Fill(face, (x, y, d) => new V(0f, 0f, 0.01f), 0.95f);
            Sdf eyes = (x, y) => Math.Min(Box(x, y, -0.075f, 0.01f, 0.045f, 0.016f, -14f * Deg, 0.012f), Box(x, y, 0.075f, 0.01f, 0.045f, 0.016f, 14f * Deg, 0.012f));
            cv.Glow(eyes, violet, 0.05f, 1.4f);
            cv.Fill(eyes, Hot(violet, V.White, 0.012f));
            // 斗篷的皺褶線
            cv.Fill((x, y) => Math.Min(Seg(x, y, -0.12f, -0.25f, -0.2f, -0.65f), Seg(x, y, 0.12f, -0.25f, 0.22f, -0.65f)) - 0.006f, (x, y, d) => violet * 0.5f, 0.7f);
            Sparkle(cv, 0.55f, 0.4f, 0.08f, V.White);
            Sparkle(cv, -0.5f, 0.3f, 0.06f, violet);
            Sparkle(cv, -0.4f, -0.3f, 0.05f, violet);
        }

        static readonly float[] Cloak = { -0.5f, -0.8f, 0.5f, -0.8f, 0.3f, -0.05f, 0.18f, 0.1f, -0.18f, 0.1f, -0.3f, -0.05f };

        static void Poison(Cv cv, V sc)
        {
            var purple = new V(0.7f, 0.3f, 1f);
            var deep = new V(0.05f, 0.35f, 0.15f);
            var light = new V(0.82f, 1f, 0.6f);
            Sdf drop = (x, y) => RoundCone(x, y, -0.04f, -0.18f, 0.36f, -0.04f, 0.6f, 0.015f);
            cv.Glow(drop, purple, 0.12f, 0.9f);
            cv.Glow(drop, sc, 0.05f, 0.6f);
            cv.Solid((x, y) => drop(x, y) - 0.024f, new V(0.03f, 0f, 0.06f));
            cv.Fill(drop, (x, y, d) =>
            {
                var c = V.Lerp(deep, sc, Clamp01(0.55f + (y + 0.18f) * 0.5f - (x + 0.04f) * 0.6f));
                // 裡面的紫色漩渦
                float sw = (float)Math.Sin(Len(x + 0.04f, y + 0.18f) * 22f - (float)Math.Atan2(y + 0.18f, x + 0.04f) * 2f);
                c = V.Lerp(c, purple * 0.8f, 0.25f * SStep(0.6f, 1f, sw) * SStep(0f, 0.1f, -d));
                float rim = 1f - SStep(0f, 0.04f, -d);
                return V.Lerp(c, V.Lerp(purple, V.White, 0.3f), rim * 0.6f);
            });
            // 高光
            cv.Fill((x, y) => Ellipse(x, y, -0.17f, -0.05f, 0.06f, 0.12f, -0.4f), (x, y, d) => V.Lerp(light, V.White, 0.5f), 0.75f);
            cv.Fill((x, y) => Circle(x, y, -0.2f, 0.12f, 0.025f), (x, y, d) => V.White, 0.8f);
            // 小骷髏標記
            Skull(cv, 0.06f, -0.22f, 0.12f, new V(0.08f, 0.0f, 0.12f));
            // 泡泡
            Bubble(cv, 0.4f, 0.3f, 0.07f, sc);
            Bubble(cv, 0.52f, 0.0f, 0.045f, purple);
            Bubble(cv, -0.45f, 0.35f, 0.05f, purple);
            Bubble(cv, 0.35f, 0.55f, 0.035f, sc);
            Bubble(cv, -0.52f, -0.2f, 0.04f, sc);
            Sparkle(cv, -0.2f, 0.12f, 0.08f, V.White);
        }

        static void Skull(Cv cv, float sx, float sy, float s, V c)
        {
            Sdf head = (x, y) => Smin(Circle(x, y, sx, sy + s * 0.15f, s * 0.62f), Box(x, y, sx, sy - s * 0.45f, s * 0.36f, s * 0.22f, 0f, s * 0.08f), s * 0.15f);
            Sdf holes = (x, y) => Math.Min(Math.Min(Circle(x, y, sx - s * 0.25f, sy + s * 0.05f, s * 0.17f), Circle(x, y, sx + s * 0.25f, sy + s * 0.05f, s * 0.17f)),
                                           Tri(x, y, sx, sy - s * 0.12f, sx - s * 0.08f, sy - s * 0.28f, sx + s * 0.08f, sy - s * 0.28f));
            cv.Fill((x, y) => Math.Max(head(x, y), -holes(x, y)), (x, y, d) => c, 0.7f);
        }

        static void Bubble(Cv cv, float bx, float by, float r, V c)
        {
            cv.Glow((x, y) => Math.Abs(Circle(x, y, bx, by, r)) - 0.008f, c, 0.03f, 0.6f);
            cv.Fill((x, y) => Circle(x, y, bx, by, r), (x, y, d) => c * 0.25f, 0.6f);
            cv.Fill((x, y) => Math.Abs(Circle(x, y, bx, by, r)) - 0.01f, (x, y, d) => V.Lerp(c, V.White, 0.4f));
            cv.Fill((x, y) => Circle(x, y, bx - r * 0.35f, by + r * 0.35f, r * 0.22f), (x, y, d) => V.White, 0.9f);
        }

        static void Knife(Cv cv, V sc)
        {
            var steelD = new V(0.32f, 0.36f, 0.45f);
            var steelL = new V(0.96f, 0.98f, 1f);
            var glow = V.Lerp(sc, new V(0.85f, 0.5f, 1f), 0.4f);
            var knives = new[] { new Kunai(-0.34f, -0.18f, 18f), new Kunai(0.34f, -0.18f, -18f), new Kunai(0f, -0.1f, 0f) };
            Sdf all = (x, y) => Math.Min(Math.Min(knives[0].D(x, y), knives[1].D(x, y)), knives[2].D(x, y));
            cv.Glow(all, glow, 0.1f, 0.8f);
            foreach (var k in knives)
            {
                cv.Solid((x, y) => k.D(x, y) - 0.022f, Ink, 0.95f);
                cv.Fill(k.Blade, (x, y, d) =>
                {
                    k.Local(x, y, out float u, out float v);
                    var c = u < 0 ? V.Lerp(steelD, steelL, 0.9f) : V.Lerp(steelD, steelL, 0.35f);
                    c = c + V.White * (0.5f * Gauss(u, 0.007f));
                    return V.Lerp(c, V.Lerp(glow, V.White, 0.5f), (1f - SStep(0f, 0.02f, -d)) * 0.6f);
                });
                cv.Fill(k.Grip, (x, y, d) =>
                {
                    k.Local(x, y, out float u, out float v);
                    float wrap = 0.5f + 0.5f * (float)Math.Sin((v + u) * 80f);
                    return V.Lerp(new V(0.12f, 0.06f, 0.14f), new V(0.42f, 0.3f, 0.46f), wrap);
                });
                cv.Fill(k.Ring, Metal(steelD, steelL, 60f));
                k.L2W(0f, 0.5f, out float tx, out float ty);
                Sparkle(cv, tx, ty, 0.07f, V.White);
            }
            Sparkle(cv, 0.55f, 0.45f, 0.06f, glow);
        }

        sealed class Kunai
        {
            readonly float ox, oy, ca, sa;
            static readonly float[] blade = { 0f, 0.62f, 0.1f, 0.16f, 0.035f, 0.06f, -0.035f, 0.06f, -0.1f, 0.16f };
            public Kunai(float ox, float oy, float angDeg) { this.ox = ox; this.oy = oy; float a = angDeg * Deg; ca = (float)Math.Cos(a); sa = (float)Math.Sin(a); }
            public void Local(float x, float y, out float u, out float v) => L(x, y, ox, oy, ca, sa, out u, out v);
            public void L2W(float u, float v, out float x, out float y) => SkillIcons.L2W(u, v, ox, oy, ca, sa, out x, out y);
            public float Blade(float x, float y) { Local(x, y, out float u, out float v); return Poly(u, v, blade); }
            public float Grip(float x, float y) { Local(x, y, out float u, out float v); return Box(u, v, 0f, -0.12f, 0.028f, 0.18f, 0f, 0.01f); }
            public float Ring(float x, float y) { Local(x, y, out float u, out float v); return Math.Abs(Circle(u, v, 0f, -0.38f, 0.065f)) - 0.02f; }
            public float D(float x, float y) => Math.Min(Math.Min(Blade(x, y), Grip(x, y)), Ring(x, y));
        }

        static void Smoke(Cv cv, V sc)
        {
            var violet = new V(0.6f, 0.42f, 0.9f);
            var light = new V(0.93f, 0.92f, 0.97f);
            var dark = new V(0.34f, 0.31f, 0.44f);
            // 煙霧彈：左下的小炸彈冒出往右上翻滾的煙團
            float bx = -0.38f, by = -0.42f, br = 0.17f;
            var puffs = new[]
            {
                new[] { 0.3f, 0.45f, 0.2f }, new[] { 0.48f, 0.18f, 0.17f }, new[] { 0.05f, 0.4f, 0.17f },
                new[] { 0.22f, 0.12f, 0.24f }, new[] { -0.1f, 0.12f, 0.19f }, new[] { 0.4f, -0.12f, 0.15f },
                new[] { -0.1f, -0.18f, 0.16f }, new[] { 0.14f, -0.24f, 0.13f },
            };
            Sdf cloud = (x, y) =>
            {
                float d = 9f;
                foreach (var p in puffs) d = Smin(d, Circle(x, y, p[0], p[1], p[2]), 0.09f);
                return d;
            };
            cv.Glow(cloud, violet, 0.12f, 0.75f);
            cv.Solid((x, y) => cloud(x, y) - 0.022f, new V(0.02f, 0.01f, 0.04f), 0.9f);
            cv.Fill(cloud, (x, y, d) => dark);
            foreach (var p in puffs)
            {
                float px = p[0], py = p[1], pr = p[2];
                cv.Fill((x, y) => Math.Max(Circle(x, y, px, py, pr - 0.015f), cloud(x, y) + 0.012f), (x, y, d) =>
                {
                    float lx = (x - px) / pr, ly = (y - py) / pr;
                    float lit = Clamp01(0.5f + 0.55f * (ly * 0.85f - lx * 0.35f));
                    var c = V.Lerp(V.Lerp(dark, violet, 0.2f), V.Lerp(sc, light, 0.7f), lit);
                    // 團與團之間只留淡淡的陰影，不要變成一顆顆葡萄
                    c = c * (0.86f + 0.14f * SStep(0f, 0.05f, -d));
                    return c * (0.92f + 0.16f * Fbm(x * 6f + 7f, y * 6f));
                });
            }
            // 煙裡的捲紋（淡）
            Sdf curls = (x, y) => Math.Min(ArcT(x, y, 0.24f, 0.16f, 0.1f, 160f * Deg, -200f * Deg, 0.003f, 0.014f, true),
                                           ArcT(x, y, 0.32f, 0.47f, 0.07f, 170f * Deg, -190f * Deg, 0.003f, 0.012f, true));
            cv.Fill(curls, (x, y, d) => V.White, 0.45f);
            // 炸彈與引信
            cv.Solid((x, y) => Circle(x, y, bx, by, br) - 0.022f, Ink);
            cv.Fill((x, y) => Box(x, y, bx + 0.11f, by + 0.11f, 0.05f, 0.035f, -45f * Deg, 0.01f), Metal(new V(0.3f, 0.28f, 0.32f), new V(0.8f, 0.78f, 0.82f), 45f));
            cv.Fill((x, y) => Circle(x, y, bx, by, br), Sphere(bx, by, br, new V(0.1f, 0.08f, 0.14f), new V(0.5f, 0.45f, 0.6f), violet, 0.9f));
            Sparkle(cv, bx + 0.17f, by + 0.17f, 0.1f, new V(1f, 0.85f, 0.5f));
            Sparkle(cv, -0.5f, 0.45f, 0.07f, violet);
            Sparkle(cv, 0.62f, 0.45f, 0.06f, V.White);
        }

        // ================================================================ 劍士
        static void Slash(Cv cv, V cls)
        {
            var blue = V.Lerp(cls, new V(0.55f, 0.8f, 1f), 0.4f);
            var white = new V(0.98f, 0.99f, 1f);
            var steelD = new V(0.4f, 0.44f, 0.55f);
            var gold = new V(1f, 0.8f, 0.35f);
            // 斬擊弧：細長的新月，從右上掃到左下（往左上凸）
            Sdf cres = (x, y) => Math.Max(Circle(x, y, 0.2f, -0.2f, 0.72f), -Circle(x, y, 0.34f, -0.34f, 0.7f));
            Sdf cres2 = (x, y) => Math.Max(Circle(x, y, 0.3f, -0.3f, 0.66f), -Circle(x, y, 0.4f, -0.4f, 0.65f));
            cv.Glow(cres2, blue, 0.06f, 0.5f);
            cv.Fill(cres2, (x, y, d) => V.Lerp(blue, white, SStep(0f, 0.03f, -d)), 0.5f);
            cv.Glow(cres, blue, 0.12f, 1.1f);
            cv.Fill(cres, (x, y, d) => V.Lerp(blue, white, SStep(0f, 0.06f, -d)));
            // 劍：從左下斜向右上，劍身壓在斬擊弧上
            float a = -45f * Deg, ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);   // 局部 +v 指向右上
            float ox = -0.28f, oy = -0.28f;
            var blade = new[] { -0.06f, 0f, 0.06f, 0f, 0.06f, 0.78f, 0f, 0.92f, -0.06f, 0.78f };
            Sdf B = (x, y) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return Poly(u, v, blade); };
            Sdf guard = (x, y) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return Box(u, v, 0f, -0.02f, 0.2f, 0.04f, 0f, 0.03f); };
            Sdf hilt = (x, y) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return Math.Min(Box(u, v, 0f, -0.16f, 0.04f, 0.13f, 0f, 0.015f), Circle(u, v, 0f, -0.32f, 0.065f)); };
            Sdf all = (x, y) => Math.Min(Math.Min(B(x, y), guard(x, y)), hilt(x, y));
            cv.Glow(all, blue, 0.06f, 0.6f);
            cv.Solid((x, y) => all(x, y) - 0.022f, Ink, 0.95f);
            cv.Fill(hilt, (x, y, d) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return V.Lerp(new V(0.1f, 0.15f, 0.35f), new V(0.3f, 0.45f, 0.8f), 0.5f + 0.5f * (float)Math.Sin(v * 70f)); });
            cv.Fill(B, (x, y, d) =>
            {
                L(x, y, ox, oy, ca, sa, out float u, out float v);
                var c = u < 0 ? V.Lerp(steelD, white, 0.95f) : V.Lerp(steelD, white, 0.45f);
                return c + V.White * (0.4f * Gauss(u, 0.006f));
            });
            cv.Fill(guard, Metal(gold * 0.45f, V.Lerp(gold, V.White, 0.6f), 45f));
            L2W(0f, 0.92f, ox, oy, ca, sa, out float tx, out float ty);
            Sparkle(cv, tx, ty, 0.16f, white);
            Sparkle(cv, -0.5f, 0.36f, 0.12f, white);
            Sparkle(cv, 0.4f, -0.58f, 0.08f, blue);
        }

        static void Thrust(Cv cv, V sc)
        {
            var gold = sc;
            var steelD = new V(0.4f, 0.4f, 0.48f);
            var steelL = new V(1f, 1f, 1f);
            float a = 45f * Deg, ca = (float)Math.Cos(a - (float)Math.PI / 2), sa = (float)Math.Sin(a - (float)Math.PI / 2);
            float ox = -0.32f, oy = -0.32f;   // 護手位置；局部 +v 指向劍尖（右上）
            var blade = new[] { -0.065f, 0f, 0.065f, 0f, 0.0f, 1.1f };
            Sdf B = (x, y) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return Poly(u, v, blade); };
            Sdf guard = (x, y) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return Math.Min(Box(u, v, 0f, -0.02f, 0.2f, 0.035f, 0f, 0.03f), Math.Max(Circle(u, v, 0f, 0.06f, 0.13f), -v - 0.0f)); };
            Sdf hilt = (x, y) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return Math.Min(Box(u, v, 0f, -0.15f, 0.035f, 0.12f, 0f, 0.015f), Circle(u, v, 0f, -0.3f, 0.06f)); };
            // 劍尖的衝擊：放射短線＋星芒
            L2W(0f, 1.1f, ox, oy, ca, sa, out float tx, out float ty);
            Sdf impact = (x, y) =>
            {
                float d = 9f;
                for (int i = 0; i < 7; i++)
                {
                    float ang = (-70f + i * 23.3f) * Deg + a;
                    float c0 = (float)Math.Cos(ang), s0 = (float)Math.Sin(ang);
                    d = Math.Min(d, SegTaper(x, y, tx + c0 * 0.1f, ty + s0 * 0.1f, tx + c0 * 0.26f, ty + s0 * 0.26f, 0.018f, 0.004f));
                }
                return d;
            };
            cv.Glow(impact, gold, 0.04f, 0.8f);
            cv.Fill(impact, Hot(gold, V.White, 0.012f));
            Sdf all = (x, y) => Math.Min(Math.Min(B(x, y), guard(x, y)), hilt(x, y));
            cv.Glow(all, gold, 0.09f, 0.9f);
            cv.Solid((x, y) => all(x, y) - 0.022f, Ink, 0.95f);
            cv.Fill(hilt, (x, y, d) => { L(x, y, ox, oy, ca, sa, out float u, out float v); return V.Lerp(new V(0.3f, 0.15f, 0.08f), new V(0.6f, 0.35f, 0.15f), 0.5f + 0.5f * (float)Math.Sin(v * 70f)); });
            cv.Fill(B, (x, y, d) =>
            {
                L(x, y, ox, oy, ca, sa, out float u, out float v);
                var c = u < 0 ? V.Lerp(steelD, steelL, 0.95f) : V.Lerp(steelD, steelL, 0.45f);
                return V.Lerp(c, V.Lerp(gold, V.White, 0.6f), SStep(0.6f, 1.05f, v));
            });
            cv.Fill(guard, Metal(gold * 0.4f, V.Lerp(gold, V.White, 0.6f), 120f));
            cv.Glow((x, y) => Circle(x, y, tx, ty, 0.02f), V.White, 0.05f, 1f);
            Sparkle(cv, tx, ty, 0.2f, V.White);
            Sparkle(cv, -0.55f, 0.3f, 0.06f, gold);
            Sparkle(cv, 0.3f, -0.55f, 0.06f, gold);
        }

        static void Wave(Cv cv, V sc)
        {
            var white = new V(0.96f, 0.98f, 1f);
            var blue = V.Lerp(sc, new V(0.3f, 0.6f, 1f), 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float cx = -0.5f + i * 0.26f;
                float op = i == 2 ? 1f : i == 1 ? 0.65f : 0.38f;
                float r = 0.42f + i * 0.08f;
                Sdf cres = (x, y) => Math.Max(Circle(x, y, cx, 0f, r), -Circle(x, y, cx - 0.16f - i * 0.02f, 0f, r * 1.02f));
                cv.Glow(cres, blue, 0.1f, 0.9f * op);
                if (i == 2) cv.Solid((x, y) => cres(x, y) - 0.018f, new V(0.01f, 0.03f, 0.1f), 0.85f);
                cv.Fill(cres, (x, y, d) => V.Lerp(blue, white, SStep(0f, 0.06f, -d)), op);
            }
            Sdf lines = (x, y) => Math.Min(SegTaper(x, y, -0.7f, 0.2f, -0.3f, 0.2f, 0.003f, 0.012f), Math.Min(SegTaper(x, y, -0.66f, -0.22f, -0.2f, -0.22f, 0.003f, 0.012f), SegTaper(x, y, -0.6f, 0f, -0.36f, 0f, 0.002f, 0.009f)));
            cv.Glow(lines, blue, 0.03f, 0.6f);
            cv.Fill(lines, (x, y, d) => white, 0.8f);
            Sparkle(cv, 0.42f, 0.0f, 0.13f, V.White);
            Sparkle(cv, 0.18f, 0.5f, 0.06f, blue);
            Sparkle(cv, 0.2f, -0.5f, 0.06f, blue);
        }

        static void Block(Cv cv, V sc)
        {
            var silverD = new V(0.35f, 0.38f, 0.48f);
            var silverL = new V(0.95f, 0.97f, 1f);
            var gold = new V(1f, 0.8f, 0.35f);
            Sdf shield = (x, y) => Math.Max(Math.Max(Circle(x, y, 0.5f, 0.18f, 0.92f), Circle(x, y, -0.5f, 0.18f, 0.92f)), y - 0.5f) - 0.02f;
            Sdf face = (x, y) => shield(x, y) + 0.075f;
            cv.Glow(shield, sc, 0.13f, 1f);
            cv.Solid((x, y) => shield(x, y) - 0.025f, Ink, 0.95f);
            cv.Fill(shield, Metal(silverD, silverL, 100f));
            cv.Fill(face, (x, y, d) =>
            {
                var c = V.Lerp(sc * 0.35f, V.Lerp(sc, V.White, 0.25f), Clamp01(0.5f + y * 0.6f - x * 0.3f));
                // 左右兩半不同深淺（盾面紋章分割）
                if (x > 0) c = c * 0.78f;
                float rim = 1f - SStep(0f, 0.03f, -d);
                return c * (1f - 0.4f * rim);
            });
            // 中央金色紋章：十字星＋盾心
            Sdf emblem = (x, y) => Math.Min(Star(x, y, 0f, 0.06f, 0.3f, 0.06f, 4, 0f), Circle(x, y, 0f, 0.06f, 0.09f));
            cv.Solid((x, y) => emblem(x, y) - 0.015f, Ink, 0.8f);
            cv.Fill(emblem, Metal(gold * 0.45f, V.Lerp(gold, V.White, 0.6f), 120f));
            cv.Fill((x, y) => Circle(x, y, 0f, 0.06f, 0.05f), Hot(sc, V.White, 0.04f));
            // 盾面反光
            cv.Fill((x, y) => Math.Max(face(x, y), Math.Abs(x + y * 0.6f + 0.15f) - 0.04f), (x, y, d) => V.White, 0.18f);
            Sparkle(cv, -0.3f, 0.42f, 0.13f, V.White);
            Sparkle(cv, 0.45f, -0.3f, 0.06f, sc);
        }

        static void IronWall(Cv cv, V sc)
        {
            var stoneD = new V(0.22f, 0.26f, 0.33f);
            var stoneL = new V(0.7f, 0.76f, 0.86f);
            var cyan = new V(0.6f, 0.85f, 1f);
            Sdf wall = (x, y) =>
            {
                float d = Box(x, y, 0f, -0.14f, 0.52f, 0.38f, 0f, 0.02f);
                for (int i = -1; i <= 1; i++) d = Math.Min(d, Box(x, y, i * 0.38f, 0.32f, 0.12f, 0.12f, 0f, 0.02f));
                return d;
            };
            cv.Glow(wall, cyan, 0.12f, 0.9f);
            cv.Solid((x, y) => wall(x, y) - 0.025f, Ink, 0.95f);
            // 磚縫底色
            cv.Fill(wall, (x, y, d) => new V(0.06f, 0.07f, 0.1f));
            // 一塊塊磚
            const float bh = 0.152f, bw = 0.3f;
            cv.Fill((x, y) =>
            {
                float wd = wall(x, y);
                float yy = y + 0.52f;
                int row = (int)Math.Floor(yy / bh);
                float off = (row & 1) == 0 ? 0f : bw * 0.5f;
                float xx = x + 0.6f + off;
                int col = (int)Math.Floor(xx / bw);
                float cx = (col + 0.5f) * bw - 0.6f - off, cy = (row + 0.5f) * bh - 0.52f;
                // 城垛上的磚自己是一整塊
                if (y > 0.2f) return Math.Max(wd + 0.022f, -9f);
                return Math.Max(Box(x, y, cx, cy, bw * 0.5f - 0.016f, bh * 0.5f - 0.016f, 0f, 0.02f), wd + 0.022f);
            }, (x, y, d) =>
            {
                float yy = y + 0.52f;
                int row = (int)Math.Floor(yy / bh);
                float off = (row & 1) == 0 ? 0f : bw * 0.5f;
                int col = (int)Math.Floor((x + 0.6f + off) / bw);
                float tint = (Hash2(col, row) & 255) / 255f;
                var c = V.Lerp(stoneD, stoneL, 0.35f + 0.35f * tint + 0.3f * Clamp01(y * 0.8f + 0.3f));
                c = c * (0.85f + 0.3f * Fbm(x * 9f, y * 9f));
                // 磚的上緣亮、下緣暗（立體感）
                float bevel = 1f - SStep(0f, 0.03f, -d);
                return c * (1f + 0.25f * bevel);
            });
            // 金屬斜反光帶
            cv.Add(wall, (x, y, d) => V.White * (0.35f * Gauss(x * 0.8f + y - 0.15f, 0.05f) + 0.15f * Gauss(x * 0.8f + y - 0.35f, 0.02f)));
            // 鐵鉚釘帶
            cv.Fill((x, y) => Math.Max(Box(x, y, 0f, 0.2f, 0.52f, 0.035f, 0f, 0f), wall(x, y)), Metal(stoneD * 0.7f, V.Lerp(sc, V.White, 0.7f), 90f));
            for (int i = 0; i < 5; i++)
            {
                float rx = -0.4f + i * 0.2f;
                cv.Fill((x, y) => Circle(x, y, rx, 0.2f, 0.022f), (x, y, d) => V.Lerp(V.White, stoneD, Clamp01(0.5f + (rx - x + y - 0.2f) * 20f)));
            }
            Sparkle(cv, -0.32f, 0.12f, 0.12f, V.White);
            Sparkle(cv, 0.45f, -0.4f, 0.06f, cyan);
        }

        static void Counter(Cv cv, V sc)
        {
            var light = V.Lerp(sc, V.White, 0.7f);
            const float R = 0.44f;
            const float span = 130f * Deg;
            var heads = new float[2][];
            for (int k = 0; k < 2; k++)
            {
                float ae = (30f + 180f * k) * Deg + span;
                float px = R * (float)Math.Cos(ae), py = R * (float)Math.Sin(ae);
                float tx = -(float)Math.Sin(ae), ty = (float)Math.Cos(ae);   // 逆時針切線
                float nx = (float)Math.Cos(ae), ny = (float)Math.Sin(ae);
                heads[k] = new[] { px + tx * 0.24f, py + ty * 0.24f, px + nx * 0.17f - tx * 0.02f, py + ny * 0.17f - ty * 0.02f, px - nx * 0.17f - tx * 0.02f, py - ny * 0.17f - ty * 0.02f };
            }
            Sdf arrows = (x, y) =>
            {
                float d = 9f;
                for (int k = 0; k < 2; k++)
                {
                    var h = heads[k];
                    d = Math.Min(d, ArcT(x, y, 0f, 0f, R, (30f + 180f * k) * Deg, span, 0.025f, 0.065f, false));
                    d = Math.Min(d, Tri(x, y, h[0], h[1], h[2], h[3], h[4], h[5]));
                }
                return d;
            };
            cv.Glow(arrows, sc, 0.11f, 1.1f);
            cv.Solid((x, y) => arrows(x, y) - 0.022f, new V(0.08f, 0f, 0.05f), 0.9f);
            cv.Fill(arrows, (x, y, d) => V.Lerp(sc * 0.85f, light, SStep(0f, 0.06f, -d)));
            // 中央反彈閃光
            Sdf star = (x, y) => Star(x, y, 0f, 0f, 0.2f, 0.06f, 4, 0.785f);
            cv.Glow(star, sc, 0.06f, 0.9f);
            cv.Fill(star, Hot(sc, V.White, 0.04f));
            Sparkle(cv, 0f, 0f, 0.26f, V.White);
            Sparkle(cv, 0.52f, 0.45f, 0.07f, light);
            Sparkle(cv, -0.52f, -0.45f, 0.07f, light);
        }

        static void Orb(Cv cv, V sc)
        {
            Sdf orb = (x, y) => Circle(x, y, 0f, 0f, 0.35f);
            cv.Glow(orb, sc, 0.12f, 1f);
            cv.Fill(orb, Hot(sc, V.White, 0.3f));
            Sparkle(cv, 0f, 0f, 0.2f, V.White);
        }

        // ================================================================ 箭
        sealed class Arrow
        {
            readonly float ax, ay, dx, dy, len, s;
            static readonly float[] head = { 0f, 0f, -0.27f, 0.14f, -0.2f, 0f, -0.27f, -0.14f };
            static readonly float[] fl = { 0f, 0.025f, 0.24f, 0.025f, 0.16f, 0.13f, -0.06f, 0.13f };
            public Arrow(float ax, float ay, float bx, float by, float s)
            {
                this.ax = ax; this.ay = ay; this.s = s;
                dx = bx - ax; dy = by - ay; len = Len(dx, dy);
                dx /= len; dy /= len;
            }
            void Loc(float x, float y, out float u, out float v)
            {
                float px = x - ax, py = y - ay;
                u = px * dx + py * dy; v = -px * dy + py * dx;
            }
            public float Shaft(float x, float y) { Loc(x, y, out float u, out float v); return Box(u, v, len * 0.5f - 0.08f * s, 0f, len * 0.5f - 0.1f * s, 0.022f * s, 0f, 0.01f * s); }
            public float Head(float x, float y) { Loc(x, y, out float u, out float v); return Poly((u - len) / s, v / s, head) * s; }
            public float Fletch(float x, float y)
            {
                Loc(x, y, out float u, out float v);
                float uu = u / s - 0.02f;
                return Math.Min(Poly(uu, v / s, fl), Poly(uu, -v / s, fl)) * s;
            }
            public float All(float x, float y) => Math.Min(Math.Min(Shaft(x, y), Head(x, y)), Fletch(x, y));
            public float Along(float x, float y) { Loc(x, y, out float u, out _); return u / len; }
            public float Across(float x, float y) { Loc(x, y, out _, out float v); return v; }
        }

        static void DrawArrow(Cv cv, Arrow ar, V sc, V glow)
        {
            cv.Glow(ar.All, glow, 0.08f, 0.9f);
            cv.Solid((x, y) => ar.All(x, y) - 0.02f, Ink, 0.92f);
            cv.Fill(ar.Fletch, (x, y, d) =>
            {
                var c = V.Lerp(sc * 0.75f, V.Lerp(sc, V.White, 0.6f), SStep(0f, 0.03f, -d));
                // 羽毛的斜紋
                float st = 0.5f + 0.5f * (float)Math.Sin(ar.Along(x, y) * 90f + ar.Across(x, y) * 60f);
                return c * (0.85f + 0.2f * st);
            });
            cv.Fill(ar.Shaft, (x, y, d) => V.Lerp(new V(0.55f, 0.4f, 0.25f), V.Lerp(sc, V.White, 0.75f), Clamp01(0.4f + ar.Across(x, y) * 25f)));
            cv.Fill(ar.Head, (x, y, d) =>
            {
                var c = ar.Across(x, y) > 0 ? new V(0.97f, 0.98f, 1f) : new V(0.55f, 0.6f, 0.7f);
                return V.Lerp(V.Lerp(glow, V.White, 0.3f), c, SStep(0f, 0.025f, -d));
            });
        }

        // ================================================================ 星光點綴：四芒星（疊加）
        static void Sparkle(Cv cv, float sx, float sy, float size, V c)
        {
            int n = cv.n;
            float ext = size * 1.1f;
            int i0 = cv.I(sx - ext), i1 = cv.I(sx + ext), j0 = cv.I(sy - ext), j1 = cv.I(sy + ext);
            float k = 1f / size;
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    float x = (cv.X(i) - sx) * k, y = (cv.X(j) - sy) * k;
                    float ax = Math.Abs(x), ay = Math.Abs(y);
                    float ray = Clamp01(1f - ax) * (float)Math.Exp(-ay * 22f) + Clamp01(1f - ay) * (float)Math.Exp(-ax * 22f);
                    float dx = (ax + ay) * 0.7071f;
                    ray += 0.4f * Clamp01(1f - dx * 1.6f) * (float)Math.Exp(-Math.Abs(ax - ay) * 30f);
                    float core = (float)Math.Exp(-(x * x + y * y) * 30f);
                    float v = ray * 0.9f + core * 1.2f;
                    if (v < 0.002f) continue;
                    int idx = j * n + i;
                    var col = V.Lerp(c, V.White, Clamp01(core * 1.2f));
                    cv.r[idx] += col.r * v; cv.g[idx] += col.g * v; cv.b[idx] += col.b * v;
                }
        }

        // ================================================================ 著色器（依位置與距離算顏色）
        static readonly V Ink = new V(0.015f, 0.012f, 0.03f);

        /// <summary>邊緣是 edge 色、越往內越接近 core 色（發光物體的「白熱核心」）。</summary>
        static Shade Hot(V edge, V core, float depth) => (x, y, d) => V.Lerp(edge, core, SStep(0f, depth, -d));

        /// <summary>金屬：沿某方向的明暗漸層＋高光帶＋內緣亮線。</summary>
        static Shade Metal(V dark, V light, float angDeg)
        {
            float a = angDeg * Deg, ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
            return (x, y, d) =>
            {
                float t = x * ca + y * sa;
                float s = SStep(-0.6f, 0.6f, t);
                var c = V.Lerp(dark, light, s);
                c = c + V.White * (0.45f * Gauss((float)Math.Sin(t * 9f), 0.18f));
                c = c + light * (0.35f * (1f - SStep(0f, 0.02f, -d)));
                return c;
            };
        }

        /// <summary>球面打光（左上光源）＋邊緣輪廓光 rimC。</summary>
        static Shade Sphere(float cx, float cy, float r, V dark, V light, V rimC, float rimK)
        {
            return (x, y, d) =>
            {
                float nx = (x - cx) / r, ny = (y - cy) / r;
                float q = nx * nx + ny * ny;
                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - q));
                float diff = Clamp01(-nx * 0.5f + ny * 0.6f + nz * 0.62f);
                var c = V.Lerp(dark, light, diff * diff);
                float hx = nx + 0.35f, hy = ny - 0.4f;
                c = c + V.White * (0.8f * (float)Math.Exp(-(hx * hx + hy * hy) * 28f));
                float rim = SStep(0.55f, 1f, (float)Math.Sqrt(q)) * Clamp01(nx * 0.6f - ny * 0.6f + 0.6f);
                return c + rimC * (rimK * rim);
            };
        }

        // ================================================================ 有向距離場（單位：徽章座標，-1..1，Y 朝上）
        const float Deg = (float)(Math.PI / 180.0);

        static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        static float Circle(float x, float y, float cx, float cy, float r) => Len(x - cx, y - cy) - r;
        static float Ellipse(float x, float y, float cx, float cy, float a, float b, float rot = 0f)
        {
            float dx = x - cx, dy = y - cy;
            if (rot != 0f) { float c = (float)Math.Cos(rot), s = (float)Math.Sin(rot); float t = dx * c + dy * s; dy = -dx * s + dy * c; dx = t; }
            float k = Len(dx / a, dy / b);
            return (k - 1f) * Math.Min(a, b);
        }

        static float Box(float x, float y, float cx, float cy, float hx, float hy, float rot = 0f, float round = 0f)
        {
            float dx = x - cx, dy = y - cy;
            if (rot != 0f) { float c = (float)Math.Cos(rot), s = (float)Math.Sin(rot); float t = dx * c + dy * s; dy = -dx * s + dy * c; dx = t; }
            float qx = Math.Abs(dx) - hx + round, qy = Math.Abs(dy) - hy + round;
            return Len(Math.Max(qx, 0f), Math.Max(qy, 0f)) + Math.Min(Math.Max(qx, qy), 0f) - round;
        }

        static float Diamond(float x, float y, float cx, float cy, float h, float w, float rot)
        {
            float dx = x - cx, dy = y - cy;
            float c = (float)Math.Cos(rot), s = (float)Math.Sin(rot);
            float u = dx * c + dy * s, v = -dx * s + dy * c;   // u 沿徑向
            return (Math.Abs(u) / h + Math.Abs(v) / w - 1f) * Math.Min(h, w) * 0.7f;
        }

        static float Seg(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay, ex = bx - ax, ey = by - ay;
            float h = Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
            return Len(px - ex * h, py - ey * h);
        }

        static float SegTaper(float x, float y, float ax, float ay, float bx, float by, float ra, float rb)
        {
            float px = x - ax, py = y - ay, ex = bx - ax, ey = by - ay;
            float h = Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
            return Len(px - ex * h, py - ey * h) - (ra + (rb - ra) * h);
        }

        /// <summary>圓錐膠囊（a 端半徑 r1、b 端半徑 r2）：淚滴、火焰、尾巴都用它。</summary>
        static float RoundCone(float x, float y, float ax, float ay, float r1, float bx, float by, float r2)
        {
            float ex = bx - ax, ey = by - ay, h = Len(ex, ey);
            if (h < 1e-5f) return Circle(x, y, ax, ay, Math.Max(r1, r2));
            ex /= h; ey /= h;
            float px = x - ax, py = y - ay;
            float v = px * ex + py * ey, u = Math.Abs(-px * ey + py * ex);
            float b = (r1 - r2) / h;
            if (b >= 1f) return Len(px, py) - r1;
            float a = (float)Math.Sqrt(1f - b * b);
            float k = -u * b + v * a;
            if (k < 0f) return Len(u, v) - r1;
            if (k > a * h) return Len(u, v - h) - r2;
            return u * a + v * b - r1;
        }

        static float Tri(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            Edge(x, y, ax, ay, bx, by, out float d0, out float s0);
            Edge(x, y, bx, by, cx, cy, out float d1, out float s1);
            Edge(x, y, cx, cy, ax, ay, out float d2, out float s2);
            float d = Math.Min(d0, Math.Min(d1, d2));
            float orient = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            bool inside = orient > 0 ? (s0 >= 0 && s1 >= 0 && s2 >= 0) : (s0 <= 0 && s1 <= 0 && s2 <= 0);
            return inside ? -(float)Math.Sqrt(d) : (float)Math.Sqrt(d);
        }

        static void Edge(float x, float y, float ax, float ay, float bx, float by, out float d2, out float side)
        {
            float ex = bx - ax, ey = by - ay, px = x - ax, py = y - ay;
            float h = Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
            float qx = px - ex * h, qy = py - ey * h;
            d2 = qx * qx + qy * qy;
            side = ex * py - ey * px;
        }

        /// <summary>任意（可凹）多邊形，pts = x0,y0,x1,y1...</summary>
        static float Poly(float x, float y, float[] pts)
        {
            int n = pts.Length / 2;
            float d = (x - pts[0]) * (x - pts[0]) + (y - pts[1]) * (y - pts[1]);
            float s = 1f;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float vix = pts[i * 2], viy = pts[i * 2 + 1], vjx = pts[j * 2], vjy = pts[j * 2 + 1];
                float ex = vjx - vix, ey = vjy - viy, wx = x - vix, wy = y - viy;
                float h = Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Math.Min(d, bx * bx + by * by);
                bool c1 = y >= viy, c2 = y < vjy, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * (float)Math.Sqrt(d);
        }

        /// <summary>n 角星（r 外半徑、ri 內半徑、rot 旋轉弧度）。</summary>
        static float Star(float x, float y, float cx, float cy, float r, float ri, int n, float rot)
        {
            float dx = x - cx, dy = y - cy;
            float a = (float)Math.Atan2(dy, dx) - rot;
            float seg = (float)(Math.PI * 2 / n);
            a = a - (float)Math.Floor(a / seg) * seg - seg * 0.5f;   // -seg/2..seg/2，0 對準尖角
            float l = Len(dx, dy);
            float px = l * (float)Math.Cos(a), py = Math.Abs(l * (float)Math.Sin(a));
            // 尖角 (r,0) 到凹角 (ri*cos(seg/2), ri*sin(seg/2)) 的邊
            float qx = ri * (float)Math.Cos(seg * 0.5f), qy = ri * (float)Math.Sin(seg * 0.5f);
            Edge(px, py, r, 0f, qx, qy, out float d2, out float side);
            return side > 0 ? -(float)Math.Sqrt(d2) : (float)Math.Sqrt(d2);
        }

        /// <summary>錐形圓弧：圓心 c、半徑 r，從角度 a0 掃過 span（可負＝順時針），寬度 w0→w1；bulge＝中間最粗。</summary>
        static float ArcT(float x, float y, float cx, float cy, float r, float a0, float span, float w0, float w1, bool bulge)
        {
            float dx = x - cx, dy = y - cy;
            float ang = (float)Math.Atan2(dy, dx);
            float sg = span < 0 ? -1f : 1f, sp = Math.Abs(span);
            float rel = (ang - a0) * sg;
            const float TWO = (float)(Math.PI * 2);
            rel -= (float)Math.Floor(rel / TWO) * TWO;
            float t; bool inside = rel <= sp;
            if (inside) t = rel / sp;
            else t = (rel - sp) < (TWO - rel) ? 1f : 0f;
            float w = bulge ? w0 + (w1 - w0) * (float)Math.Sin(Math.PI * t) : w0 + (w1 - w0) * t;
            if (inside) return Math.Abs(Len(dx, dy) - r) - w;
            float ae = a0 + sg * t * sp;
            float ex = cx + r * (float)Math.Cos(ae), ey = cy + r * (float)Math.Sin(ae);
            return Len(x - ex, y - ey) - w;
        }

        static float Smin(float a, float b, float k)
        {
            float h = Clamp01(0.5f + 0.5f * (b - a) / k);
            return b + (a - b) * h - k * h * (1f - h);
        }

        static void L(float x, float y, float ox, float oy, float ca, float sa, out float u, out float v)
        {
            float dx = x - ox, dy = y - oy;
            u = dx * ca + dy * sa; v = -dx * sa + dy * ca;
        }

        static void L2W(float u, float v, float ox, float oy, float ca, float sa, out float x, out float y)
        {
            x = ox + u * ca - v * sa; y = oy + u * sa + v * ca;
        }

        // ================================================================ 數學小工具（不用 Mathf，Unity 外也能跑）
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float SStep(float e0, float e1, float x) { float t = Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        static float Gauss(float x, float w) => (float)Math.Exp(-(x * x) / (w * w));

        static int Hash(string s) { unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h & 0x7fffffff; } }
        static int Hash2(int x, int y) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (int)((h ^ (h >> 16)) & 0x7fffffff); } }
        static float Rnd(int x, int y) => (Hash2(x, y) & 0xffff) / 65535f;

        static float ValueNoise(float x, float y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Rnd(ix, iy), b = Rnd(ix + 1, iy), c = Rnd(ix, iy + 1), d = Rnd(ix + 1, iy + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        static float Fbm(float x, float y) => ValueNoise(x, y) * 0.57f + ValueNoise(x * 2.03f + 5.2f, y * 2.03f + 1.3f) * 0.29f + ValueNoise(x * 4.1f + 9.1f, y * 4.1f + 3.7f) * 0.14f;

        // ================================================================ 畫布
        delegate float Sdf(float x, float y);
        delegate V Shade(float x, float y, float d);

        struct V
        {
            public float r, g, b;
            public V(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
            public static readonly V White = new V(1f, 1f, 1f);
            public static V operator +(V a, V b) => new V(a.r + b.r, a.g + b.g, a.b + b.b);
            public static V operator *(V a, float k) => new V(a.r * k, a.g * k, a.b * k);
            public static V Lerp(V a, V b, float t) => new V(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
            public static V Hex(string h)
            {
                int v = Convert.ToInt32(h.TrimStart('#'), 16);
                return new V(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
            }
        }

        sealed class Cv
        {
            public readonly int n;
            public readonly float px;              // 一個像素的寬度（徽章座標）
            public readonly float[] r, g, b;
            readonly float[] coord;

            public Cv(int n)
            {
                this.n = n;
                px = 2f / n;
                r = new float[n * n]; g = new float[n * n]; b = new float[n * n];
                coord = new float[n];
                for (int i = 0; i < n; i++) coord[i] = (i + 0.5f) / n * 2f - 1f;
            }

            public float X(int i) => coord[i];
            public int I(float x) { int i = (int)Math.Floor((x + 1f) * 0.5f * n); return i < 0 ? 0 : i >= n ? n - 1 : i; }

            /// <summary>依距離場填色（反鋸齒：距離在 ±半像素內線性過渡），opacity 0..1。</summary>
            public void Fill(Sdf f, Shade s, float opacity = 1f) => Run(f, s, opacity, 0);

            public void Solid(Sdf f, V c, float opacity = 1f) => Fill(f, (x, y, d) => c, opacity);

            /// <summary>疊加光暈：形狀內全亮，外面依距離指數衰減（bloom）。</summary>
            public void Glow(Sdf f, V c, float radius, float k)
            {
                if (k <= 0f) return;
                // 光暈很柔和：距離場在粗網格上取樣再雙線性內插（半徑越大網格越粗），省下大部分計算
                int step = radius >= 8f * px ? 4 : radius >= 3f * px ? 2 : 1;
                if (step == 1)
                {
                    glowC = c; glowInv = 1f / radius; glowK = k;
                    float reach = radius * (float)Math.Log(Math.Max(1.0001f, k / 0.004f));
                    Run(f, null, 1f, 1, reach);
                    return;
                }
                int m = (n + step - 1) / step + 1;
                if (grid == null || grid.Length < m * m) grid = new float[m * m];
                for (int gj = 0; gj < m; gj++)
                {
                    float y = -1f + (gj * step + 0.5f) * px;
                    for (int gi = 0; gi < m; gi++) grid[gj * m + gi] = f(-1f + (gi * step + 0.5f) * px, y);
                }
                float inv = 1f / radius, istep = 1f / step;
                for (int j = 0; j < n; j++)
                {
                    int gj = j / step; float fy = (j - gj * step) * istep;
                    for (int i = 0; i < n; i++)
                    {
                        int gi = i / step; float fx = (i - gi * step) * istep;
                        int q = gj * m + gi;
                        float d0 = grid[q] + (grid[q + 1] - grid[q]) * fx;
                        float d1 = grid[q + m] + (grid[q + m + 1] - grid[q + m]) * fx;
                        float d = d0 + (d1 - d0) * fy;
                        float v = d <= 0f ? k : k * (float)Math.Exp(-d * inv);
                        if (v < 0.004f) continue;
                        int idx = j * n + i;
                        r[idx] += c.r * v; g[idx] += c.g * v; b[idx] += c.b * v;
                    }
                }
            }

            float[] grid;

            /// <summary>只在形狀內疊加（反光帶等）。</summary>
            public void Add(Sdf f, Shade s) => Run(f, s, 1f, 2);

            V glowC; float glowInv, glowK;
            const int T = 8;

            /// <summary>
            /// 逐像素執行（mode 0＝覆蓋混色、1＝光暈疊加、2＝形狀內疊加）。
            /// 先在 8×8 區塊中心取樣距離場：距離遠大於區塊半徑＋作用範圍的區塊整塊跳過（距離場斜率≈1，留 1.6 倍安全係數）。
            /// </summary>
            void Run(Sdf f, Shade s, float opacity, int mode, float reach = 0f)
            {
                float inv = 1f / px;
                float half = T * 0.5f * px * 1.4143f;
                float skip = half * 1.6f + 2f * px + reach;
                for (int ty = 0; ty < n; ty += T)
                    for (int tx = 0; tx < n; tx += T)
                    {
                        int jx = Math.Min(n, ty + T), ix = Math.Min(n, tx + T);
                        float cx = -1f + (tx + ix) * 0.5f * px, cy = -1f + (ty + jx) * 0.5f * px;
                        if (f(cx, cy) > skip) continue;
                        for (int j = ty; j < jx; j++)
                        {
                            float y = coord[j];
                            for (int i = tx; i < ix; i++)
                            {
                                float x = coord[i];
                                float d = f(x, y);
                                int k = j * n + i;
                                if (mode == 1)
                                {
                                    float v = d <= 0f ? 1f : (float)Math.Exp(-d * glowInv);
                                    v *= glowK;
                                    if (v < 0.004f) continue;
                                    r[k] += glowC.r * v; g[k] += glowC.g * v; b[k] += glowC.b * v;
                                    continue;
                                }
                                float a = 0.5f - d * inv;
                                if (a <= 0f) continue;
                                if (a > 1f) a = 1f;
                                var c = s(x, y, d);
                                if (mode == 2) { r[k] += c.r * a; g[k] += c.g * a; b[k] += c.b * a; continue; }
                                a *= opacity;
                                r[k] += (c.r - r[k]) * a; g[k] += (c.g - g[k]) * a; b[k] += (c.b - b[k]) * a;
                            }
                        }
                    }
            }

            /// <summary>色調映射（柔和壓高光、過曝的顏色往白色溢出）＋徽章外的透明度與外光暈。</summary>
            public Color32[] Output()
            {
                var o = new Color32[n * n];
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int k = j * n + i;
                        float cr = r[k], cg = g[k], cb = b[k];
                        float m = Math.Max(cr, Math.Max(cg, cb));
                        if (m > 1f)
                        {
                            float spill = Math.Min(1f, (m - 1f) * 0.5f);
                            cr += (m - cr) * spill; cg += (m - cg) * spill; cb += (m - cb) * spill;
                        }
                        float rr = Len(coord[i], coord[j]);
                        float a = Clamp01(0.5f - (rr - BadgeR) / px);
                        if (a < 1f) a = Math.Max(a, 0.55f * (float)Math.Exp(-(rr - BadgeR) / 0.022f));
                        o[k] = new Color32(Lut(cr), Lut(cg), Lut(cb), (byte)(a * 255f + 0.5f));
                    }
                return o;
            }

            // 色調映射＋輕微 gamma（提亮暗部，讓深色徽章在手機上不會糊成一片黑）查表：輸入 0..LutMax
            const int LutN = 2048; const float LutMax = 4f;
            static byte[] lut;

            static byte Lut(float v)
            {
                if (lut == null)
                {
                    var t = new byte[LutN + 1];
                    for (int q = 0; q <= LutN; q++)
                    {
                        float x = q * (LutMax / LutN);
                        const float knee = 0.78f;
                        if (x > knee) x = knee + (1f - knee) * (1f - (float)Math.Exp(-(x - knee) / (1f - knee)));
                        x = (float)Math.Pow(Clamp01(x), 0.92);
                        t[q] = (byte)Math.Min(255, (int)(x * 255f + 0.5f));
                    }
                    lut = t;
                }
                int idx = (int)(v * (LutN / LutMax) + 0.5f);
                return lut[idx < 0 ? 0 : idx > LutN ? LutN : idx];
            }
        }
    }
}
