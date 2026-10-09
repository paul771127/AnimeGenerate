using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 手勢（從網頁版移植）：鏡頭只拍得到自己伸到鏡頭前的手腕與手指。
    ///   ✊ 握拳＝蓄力姿勢；握拳後 1.5 秒內 🖐️ 張開手＝放招（「握拳→張開」）。
    ///   手往上快速一揮（甩手）也會放招。
    ///   瞄準：方向＝手腕 → 中指根部，準星在指尖再往前延伸一段（螢幕座標）。
    /// 輸入是 PoseDetector 的手部關鍵點（視埠座標，左下為原點）。
    /// </summary>
    public class HandGesture
    {
        public enum Shape { None, Fist, Open, Other }

        public bool HandVisible { get; private set; }
        public Shape Current { get; private set; } = Shape.None;
        public Vector2 Aim { get; private set; }        // 準星（螢幕像素，左下為原點，和 Input.mousePosition 相同）
        public Vector2 Palm { get; private set; }       // 手掌中心（螢幕像素）
        public Vector2[] Points { get; } = new Vector2[PoseDetector.HandCount];   // 螢幕像素
        public float AimReach = 0.25f;                  // 準星離指尖的距離（螢幕高度的比例）

        float lastFist = -99f, lastHandTime = -99f, lastResultTime = -1f;
        Shape pending = Shape.None; int pendingCount;
        bool releaseQueued;
        float lastRelease = -99f;   // 放招後 0.6 秒內不重複觸發

        void Queue(float t) { if (t - lastRelease < 0.6f) return; releaseQueued = true; lastRelease = t; }
        Vector2 prevPalm; float prevPalmTime = -99f;
        bool hasAim;

        /// <summary>有新的偵測結果時呼叫（screenW/H：螢幕像素）</summary>
        public void Update(PoseDetector.Result r, float now, float screenW, float screenH)
        {
            if (r != null && r.time != lastResultTime)
            {
                lastResultTime = r.time;
                if (r.handFound) Consume(r, screenW, screenH);
                else if (now - lastHandTime > 0.4f) { HandVisible = false; Current = Shape.None; pendingCount = 0; }
            }
            if (now - lastHandTime > 0.6f) { HandVisible = false; Current = Shape.None; }
        }

        void Consume(PoseDetector.Result r, float W, float H)
        {
            float t = r.time;
            for (int i = 0; i < PoseDetector.HandCount; i++) Points[i] = new Vector2(r.hand[i].x * W, r.hand[i].y * H);
            HandVisible = true; lastHandTime = t;

            var wrist = Points[0];
            var palm = (Points[0] + Points[5] + Points[9] + Points[13] + Points[17]) / 5f;
            Palm = palm;
            float palmSize = Mathf.Max(1f, Vector2.Distance(wrist, Points[9]));

            // 每根手指（食指～小指）伸直：指尖離手腕比第二關節遠很多
            int extended = 0, curled = 0;
            for (int f = 0; f < 4; f++)
            {
                int pip = 6 + f * 4, tip = 8 + f * 4;
                float dTip = Vector2.Distance(wrist, Points[tip]), dPip = Vector2.Distance(wrist, Points[pip]);
                if (dTip > dPip * 1.15f) extended++;
                else if (Vector2.Distance(Points[tip], palm) < palmSize * 0.9f) curled++;
            }
            var shape = extended >= 3 ? Shape.Open : (curled >= 3 || extended == 0) ? Shape.Fist : Shape.Other;

            // 連續兩次相同才算（避免偵測抖動）
            if (shape == pending) pendingCount++; else { pending = shape; pendingCount = 1; }
            if (pendingCount >= 2 && shape != Current)
            {
                var prev = Current;
                Current = shape;
                if (shape == Shape.Fist) lastFist = t;
                if (shape == Shape.Open && prev != Shape.Open && t - lastFist < 1.5f) { Queue(t); lastFist = -99f; }
            }
            if (Current == Shape.Fist) lastFist = t;

            // 甩手：手掌往上快速移動（每秒超過 2.2 個螢幕高）
            if (t - prevPalmTime < 0.25f && t > prevPalmTime)
            {
                float vy = (palm.y - prevPalm.y) / (t - prevPalmTime);
                if (vy > H * 2.2f) Queue(t);
            }
            prevPalm = palm; prevPalmTime = t;

            // 瞄準：手腕 → 中指根部的方向，準星在指尖再往前
            var dir = Points[9] - wrist;
            float len = dir.magnitude;
            dir = len > 1e-3f ? dir / len : Vector2.up;
            var tipPt = Points[9] + dir * len * 0.9f;
            var raw = tipPt + dir * (H * AimReach);
            raw = new Vector2(Mathf.Clamp(raw.x, 10f, W - 10f), Mathf.Clamp(raw.y, 10f, H - 10f));
            Aim = hasAim ? Vector2.Lerp(Aim, raw, 0.5f) : raw;
            hasAim = true;
        }

        /// <summary>取走「放招」事件（一次性）</summary>
        public bool ConsumeRelease()
        {
            if (!releaseQueued) return false;
            releaseQueued = false;
            return true;
        }

        public void ClearRelease() => releaseQueued = false;

        /// <summary>畫出手的關鍵點、準星與手勢（OnGUI 中呼叫）</summary>
        public void DrawGUI(GUIStyle style, Color aimColor)
        {
            if (!HandVisible) return;
            float H = Screen.height, d = Mathf.Max(6f, Screen.width * 0.012f);
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            foreach (var p in Points) GUI.DrawTexture(new Rect(p.x - d / 2, H - p.y - d / 2, d, d), Texture2D.whiteTexture);
            // 準星：十字＋圓點
            float r = d * 3f;
            GUI.color = aimColor;
            GUI.DrawTexture(new Rect(Aim.x - r, H - Aim.y - 1.5f, r * 2, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Aim.x - 1.5f, H - Aim.y - r, 3f, r * 2), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Palm.x - 80, H - Palm.y + d * 2, 160, d * 5), Label, style);
        }

        public string Label => Current switch
        {
            Shape.Fist => "✊ 蓄力",
            Shape.Open => "🖐️ 張開",
            Shape.Other => "✋",
            _ => "",
        };
    }
}
