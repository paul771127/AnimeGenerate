using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 身體動作的畫面提示（使用者回報：做動作那邊不知道要做什麼、完成了哪些；莫名其妙就被絆倒）：
    ///   ・鏡像試煉：畫面上方一排動作卡（⬇ 蹲下、⬆ 跳起來、⬅ 往左、➡ 往右）。
    ///     示範時一張一張亮起來；換玩家時，做完的打勾變綠、現在要做的放大閃黃色、下面有剩餘時間條；上面寫第幾輪。
    ///   ・路上的身體障礙（跳、蹲、側步）：前方 5m 內、在這條路上時，畫面中間顯示大大的「⬆ 跳過去！」和距離，
    ///     到了要做動作的位置變成「現在！」；每一個障礙都會提示（不只第一次）。
    /// </summary>
    public partial class SoloBattle
    {
        static readonly string[] MoveIcons = { "⬇", "⬆", "⬅", "➡" };
        static readonly Color[] MoveColors = { new Color(0.4f, 0.65f, 1f), new Color(1f, 0.45f, 0.35f), new Color(0.45f, 1f, 0.55f), new Color(0.45f, 1f, 0.55f) };

        void DrawActionCue(float W, float H)
        {
            if (battle == null || Me == null || phase != Phase.Fighting) return;
            if (battle.MirrorActive) DrawMirrorCue(W, H);
            else DrawGateCue(W, H);
        }

        void DrawMirrorCue(float W, float H)
        {
            int fs = UiKit.BaseFont;
            var seq = battle.MirrorSeq; int n = seq.Count; if (n == 0) return;
            float pad = fs * 0.6f, cw = Mathf.Min(fs * 5.2f, (W - pad * 2f) / n - pad * 0.5f), ch = cw * 1.15f;
            float totalW = n * cw + (n - 1) * pad * 0.5f, x0 = (W - totalW) * 0.5f, y0 = H * 0.17f;
            var panel = new Rect(x0 - pad, y0 - fs * 2.6f, totalW + pad * 2f, ch + fs * 4.6f);
            UiKit.MenuCard(panel, 0.88f, new Color(0.55f, 0.75f, 1f));
            bool demo = battle.MirrorStep < 0, pen = battle.MirrorPenalty;
            string head = pen ? "⚔ 做錯或太慢了：先打倒湧出來的怪，再看一次示範"
                : demo ? $"👀 看好了！記住順序（第 {battle.MirrorRound + 1}／{battle.MirrorRounds} 輪）"
                : $"🙋 換你了！照順序做（第 {battle.MirrorRound + 1}／{battle.MirrorRounds} 輪）";
            UiKit.Text(new Rect(panel.x, y0 - fs * 2.4f, panel.width, fs * 2f), head, fs, Color.white, TextAnchor.MiddleCenter, true);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 9f);
            for (int i = 0; i < n; i++)
            {
                int k = seq[i];
                var r = new Rect(x0 + i * (cw + pad * 0.5f), y0, cw, ch);
                bool done = !demo && !pen && i < battle.MirrorStep;
                bool cur = !demo && !pen && i == battle.MirrorStep;
                bool lit = demo && i == battle.MirrorDemoIndex;
                bool shown = demo && i < battle.MirrorDemoIndex;
                Color c = done ? new Color(0.35f, 0.9f, 0.45f) : cur ? Color.Lerp(new Color(1f, 0.85f, 0.2f), Color.white, pulse * 0.3f) : lit ? MoveColors[k] : new Color(0.32f, 0.34f, 0.42f);
                if (cur) r = new Rect(r.x - cw * 0.06f, r.y - ch * 0.06f, r.width * 1.12f, r.height * 1.12f);
                GUI.color = new Color(c.r, c.g, c.b, cur ? 0.95f : 0.85f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = Color.white;
                // 示範還沒輪到的：只顯示「？」（照順序一個一個亮）
                string icon = demo && !lit && !shown ? "？" : done ? "✓" : MoveIcons[k];
                string name = demo && !lit && !shown ? "" : Battle.MoveNames[k];
                var tc = done || cur || lit ? new Color(0.08f, 0.08f, 0.12f) : Color.white;
                UiKit.Text(new Rect(r.x, r.y + r.height * 0.06f, r.width, r.height * 0.55f), icon, Mathf.RoundToInt(fs * 2.2f), tc, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(r.x, r.y + r.height * 0.6f, r.width, r.height * 0.36f), name, Mathf.RoundToInt(fs * 0.85f), tc, TextAnchor.MiddleCenter, true);
                UiKit.Text(new Rect(r.x, r.yMax + fs * 0.1f, r.width, fs * 1.2f), $"{i + 1}", Mathf.RoundToInt(fs * 0.8f), new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter);
            }
            // 底下：現在要做什麼＋剩餘時間
            float by = y0 + ch + fs * 1.4f;
            if (!demo && !pen && battle.MirrorStep < n)
            {
                int k = seq[battle.MirrorStep];
                UiKit.Text(new Rect(panel.x, by - fs * 0.2f, panel.width, fs * 1.4f), $"現在：{MoveIcons[k]} {Battle.MoveNames[k]}！", fs, new Color(1f, 0.9f, 0.4f), TextAnchor.MiddleCenter, true);
                float t = Mathf.Clamp01(battle.MirrorTimeLeft / 8f);
                GUI.color = new Color(0f, 0f, 0f, 0.5f); GUI.DrawTexture(new Rect(x0, by + fs * 1.3f, totalW, fs * 0.35f), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(1f, 0.3f, 0.2f), new Color(0.4f, 1f, 0.5f), t); GUI.DrawTexture(new Rect(x0, by + fs * 1.3f, totalW * t, fs * 0.35f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            else if (demo) UiKit.Text(new Rect(panel.x, by - fs * 0.2f, panel.width, fs * 1.4f), $"示範完 {battle.MirrorDemoLeft:0} 秒後換你", Mathf.RoundToInt(fs * 0.85f), new Color(0.8f, 0.9f, 1f), TextAnchor.MiddleCenter);
        }

        void DrawGateCue(float W, float H)
        {
            if (bodyGates.Count == 0 || exploreRun == null || !BodyGatesOn(exploreRun)) return;
            BodyGate best = null; float bestD = 5.5f, bestS = 0f;
            var me = Me.Feet;
            foreach (var g in bodyGates)
            {
                if (Time.time < g.coolUntil) continue;   // 剛穿過
                var right = new Vector3(g.fwd.z, 0f, -g.fwd.x);
                float s = Vector3.Dot(me - g.pos, g.fwd), lat = Vector3.Dot(me - g.pos, right);
                if (Mathf.Abs(lat) > GateHalfW + 0.2f || Mathf.Abs(s) > bestD) continue;
                best = g; bestD = Mathf.Abs(s); bestS = s;
            }
            if (best == null) return;
            int fs = UiKit.BaseFont;
            bool now = bestD < GatePoseRange;
            string what = best.kind == GateJump ? "⬆ 跳過去！" : best.kind == GateDuck ? "⬇ 蹲下鑽過去！" : best.kind == GateWallLeft ? "➡ 往右邊閃！" : "⬅ 往左邊閃！";
            var col = best.kind == GateJump ? new Color(1f, 0.45f, 0.35f) : best.kind == GateDuck ? new Color(0.45f, 0.65f, 1f) : new Color(0.45f, 1f, 0.55f);
            float pulse = now ? 0.75f + 0.25f * Mathf.Sin(Time.time * 14f) : 1f;
            var r = new Rect(W * 0.5f - fs * 8f, H * 0.3f, fs * 16f, fs * 4.2f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(col.r, col.g, col.b, 0.9f * pulse); GUI.DrawTexture(new Rect(r.x, r.y, r.width, fs * 0.3f), Texture2D.whiteTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - fs * 0.3f, r.width, fs * 0.3f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            UiKit.Text(new Rect(r.x, r.y + fs * 0.3f, r.width, fs * 2.4f), now ? "現在！" + what : what, Mathf.RoundToInt(fs * (now ? 1.7f : 1.4f)), col, TextAnchor.MiddleCenter, true);
            UiKit.Text(new Rect(r.x, r.y + fs * 2.6f, r.width, fs * 1.3f), now ? "穿過的時候做出動作" : $"前方 {bestD:0.0}m", Mathf.RoundToInt(fs * 0.85f), Color.white, TextAnchor.MiddleCenter);
        }
    }
}
