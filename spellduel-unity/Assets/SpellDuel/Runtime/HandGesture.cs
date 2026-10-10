using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 手勢（從網頁版移植）：鏡頭只拍得到自己伸到鏡頭前的手腕與手指。
    ///   每個技能有自己的手勢（食指、剪刀、三指、搖滾、六、讚）：比出來停 0.35 秒＝開始詠唱該技能。
    ///   放招動作依職業不同（Style）：
    ///     法師 Burst：✊ 握拳聚氣 → 1.5 秒內 🖐️ 張開。
    ///     弓箭手 Bow：在畫面中間握拳（捏弦）→ 往旁邊慢慢拉開（拉滿）→ 張開手放箭；箭往開始拉弓的位置射。
    ///     劍士 Chop：手掌伸直（手刀）快速橫向／斜向劈過畫面；往劈過的中點斬。
    ///     刺客 Thrust：手刀快速往前刺（手遠離手機 → 畫面上變小）；往刺出的位置。
    ///   瞄準：方向＝手腕 → 中指根部，準星在指尖再往前延伸一段（螢幕座標）。
    /// 輸入是 PoseDetector 的手部關鍵點（視埠座標，左下為原點）。
    /// </summary>
    public class HandGesture
    {
        public enum Shape { None, Fist, Open, One, Two, Three, Rock, Call, Thumb, Other }
        public enum Style { Burst, Bow, Chop, Thrust }

        public Style ReleaseStyle = Style.Burst;     // 由職業決定（SoloBattle 設定）
        public Vector2 ReleaseAim { get; private set; }   // 放招事件時的瞄準點（螢幕像素）
        public float BowDraw { get; private set; }        // 弓箭手拉弓進度 0..1（畫面提示用）
        public Vector2 BowAnchor { get; private set; }    // 開始拉弓的位置
        public bool BowHolding { get; private set; }

        public static Style StyleOf(string classId) => classId switch
        {
            "archer" => Style.Bow, "swordsman" => Style.Chop, "assassin" => Style.Thrust, _ => Style.Burst,
        };

        public static string StyleHint(Style st) => st switch
        {
            Style.Bow => "拉弓：在畫面中間握拳 → 往旁邊慢慢拉 → 張開手放箭",
            Style.Chop => "手刀斬：手掌伸直，快速橫劈過畫面",
            Style.Thrust => "突刺：手刀快速往前刺出（手遠離手機）",
            _ => "聚氣：握拳 → 張開手放出",
        };

        // 最近約 1 秒的手掌軌跡（劈砍、突刺用）
        struct Sample { public float t; public Vector2 palm; public float size; public Shape shape; }
        readonly Sample[] hist = new Sample[16]; int histN, histHead;
        void PushHist(Sample s) { hist[histHead] = s; histHead = (histHead + 1) % hist.Length; if (histN < hist.Length) histN++; }
        Sample HistAgo(int k) => hist[(histHead - 1 - k + hist.Length * 2) % hist.Length];   // k=0 最新

        /// <summary>可以用來選技能的手勢（依職業技能順序分配，同職業不重複）</summary>
        public static readonly Shape[] SkillShapes = { Shape.One, Shape.Two, Shape.Three, Shape.Rock, Shape.Call, Shape.Thumb };

        public static string ShapeName(Shape s) => s switch
        {
            Shape.Fist => "握拳", Shape.Open => "張開", Shape.One => "食指", Shape.Two => "剪刀", Shape.Three => "三指",
            Shape.Rock => "搖滾", Shape.Call => "六", Shape.Thumb => "讚", _ => "",
        };

        /// <summary>手指伸直狀態：拇指、食指、中指、無名指、小指</summary>
        public static bool[] Fingers(Shape s) => s switch
        {
            Shape.Fist => new[] { false, false, false, false, false },
            Shape.Open => new[] { true, true, true, true, true },
            Shape.One => new[] { false, true, false, false, false },
            Shape.Two => new[] { false, true, true, false, false },
            Shape.Three => new[] { false, true, true, true, false },
            Shape.Rock => new[] { false, true, false, false, true },
            Shape.Call => new[] { true, false, false, false, true },
            Shape.Thumb => new[] { true, false, false, false, false },
            _ => new[] { false, false, false, false, false },
        };

        /// <summary>畫一個手勢小圖：手掌＋五根手指（伸直的長、彎起來的短）</summary>
        public static void DrawIcon(Rect r, Shape s, Color c)
        {
            var f = Fingers(s);
            GUI.color = c;
            float pw = r.width * 0.62f, ph = r.height * 0.36f, px = r.x + (r.width - pw) / 2f + r.width * 0.06f, py = r.yMax - ph;
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);           // 手掌
            float fw = pw / 4f * 0.72f;
            for (int i = 0; i < 4; i++)
            {
                float fh = f[i + 1] ? r.height * 0.55f : r.height * 0.12f;
                GUI.DrawTexture(new Rect(px + i * pw / 4f + (pw / 4f - fw) / 2f, py - fh, fw, fh), Texture2D.whiteTexture);
            }
            // 拇指：伸直時往左斜上，彎起時貼著手掌
            float tw = f[0] ? r.width * 0.3f : r.width * 0.12f;
            GUI.DrawTexture(new Rect(px - tw, py + ph * (f[0] ? 0.05f : 0.35f), tw, fw), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public bool HandVisible { get; private set; }
        public Shape Current { get; private set; } = Shape.None;
        public Vector2 Aim { get; private set; }        // 準星（螢幕像素，左下為原點，和 Input.mousePosition 相同）
        public Vector2 Palm { get; private set; }       // 手掌中心（螢幕像素）
        public Vector2[] Points { get; } = new Vector2[PoseDetector.HandCount];   // 螢幕像素
        public float AimReach = 0.25f;                  // 準星離指尖的距離（螢幕高度的比例）

        float lastFist = -99f, lastHandTime = -99f, lastResultTime = -1f;
        Shape pending = Shape.None; int pendingCount;
        bool releaseQueued;
        Shape selectQueued = Shape.None; float shapeSince; bool selectFired;
        float lastRelease = -99f;   // 放招後 0.6 秒內不重複觸發

        void Queue(float t, Vector2 aim) { if (t - lastRelease < 0.6f) return; releaseQueued = true; lastRelease = t; ReleaseAim = aim; }
        float bowStart;
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

            var shape = Classify(Points, palm, palmSize);

            // 連續兩次相同才算（避免偵測抖動）
            if (shape == pending) pendingCount++; else { pending = shape; pendingCount = 1; }
            if (pendingCount >= 2 && shape != Current)
            {
                var prev = Current;
                Current = shape;
                shapeSince = t; selectFired = false;
                if (shape == Shape.Fist) lastFist = t;
                if (ReleaseStyle == Style.Burst && shape == Shape.Open && prev != Shape.Open && t - lastFist < 1.5f) { Queue(t, Aim); lastFist = -99f; }
            }
            if (Current == Shape.Fist) lastFist = t;
            // 選技能手勢：同一個手勢維持 0.35 秒才算（避免換手勢途中誤觸）
            if (!selectFired && System.Array.IndexOf(SkillShapes, Current) >= 0 && t - shapeSince >= 0.35f) { selectQueued = Current; selectFired = true; }

            PushHist(new Sample { t = t, palm = palm, size = palmSize, shape = shape });
            bool fingersOut = shape != Shape.Fist && shape != Shape.None;

            // 弓箭手：畫面中間握拳 → 往旁邊拉（橫向超過畫面寬 15%、至少 0.3 秒）→ 張開放箭；沒拉滿就張開＝取消
            if (ReleaseStyle == Style.Bow)
            {
                if (Current == Shape.Fist)
                {
                    if (!BowHolding)
                    {
                        if (Mathf.Abs(palm.x - W / 2f) < W * 0.25f && Mathf.Abs(palm.y - H / 2f) < H * 0.3f) { BowHolding = true; BowAnchor = palm; bowStart = t; }
                    }
                    else BowDraw = Mathf.Clamp01(Mathf.Abs(palm.x - BowAnchor.x) / (W * 0.15f)) * (t - bowStart >= 0.3f ? 1f : 0.99f);
                }
                else if (BowHolding && Current != Shape.Fist && pendingCount >= 2)
                {
                    if (BowDraw >= 1f && fingersOut) Queue(t, BowAnchor);
                    BowHolding = false; BowDraw = 0f;
                }
            }
            else { BowHolding = false; BowDraw = 0f; }

            // 劍士：張開的手在 0.4 秒內移動超過畫面寬 25%（手刀劈過）
            if (ReleaseStyle == Style.Chop && fingersOut && histN >= 3)
            {
                for (int k = 2; k < histN; k++)
                {
                    var o = HistAgo(k);
                    if (t - o.t > 0.4f) break;
                    if (o.shape == Shape.Fist || o.shape == Shape.None) break;
                    if (Vector2.Distance(o.palm, palm) > W * 0.25f) { Queue(t, (o.palm + palm) / 2f); break; }
                }
            }

            // 刺客：手刀往前刺 → 手遠離手機，畫面上手掌在 0.35 秒內縮小 25% 以上
            if (ReleaseStyle == Style.Thrust && fingersOut && histN >= 3)
            {
                for (int k = 2; k < histN; k++)
                {
                    var o = HistAgo(k);
                    if (t - o.t > 0.35f) break;
                    if (o.shape == Shape.Fist || o.shape == Shape.None) break;
                    if (palmSize < o.size * 0.75f) { Queue(t, palm); break; }
                }
            }

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

        /// <summary>判斷手勢：每根手指是否伸直（指尖離手腕比第二關節遠很多）</summary>
        static Shape Classify(Vector2[] p, Vector2 palm, float palmSize)
        {
            var wrist = p[0];
            var e = new bool[4];
            int extended = 0;
            for (int f = 0; f < 4; f++)
            {
                int pip = 6 + f * 4, tip = 8 + f * 4;
                e[f] = Vector2.Distance(wrist, p[tip]) > Vector2.Distance(wrist, p[pip]) * 1.15f;
                if (e[f]) extended++;
            }
            // 拇指：指尖離手掌中心夠遠（彎起來時會貼在手掌或食指旁），而且比拇指關節更遠離手腕
            bool thumb = Vector2.Distance(p[4], palm) > palmSize * 0.7f && Vector2.Distance(wrist, p[4]) > Vector2.Distance(wrist, p[3]) * 1.03f;
            bool I = e[0], M = e[1], R = e[2], P = e[3];
            if (extended == 4) return Shape.Open;
            if (extended == 0) return thumb ? Shape.Thumb : Shape.Fist;
            if (I && !M && !R && !P) return Shape.One;
            if (I && M && !R && !P) return Shape.Two;
            if (I && M && R && !P) return Shape.Three;
            if (I && !M && !R && P) return Shape.Rock;
            if (!I && !M && !R && P) return thumb ? Shape.Call : Shape.Other;
            return Shape.Other;
        }

        /// <summary>取走「選技能」事件：比出技能手勢並維持住（一次性）</summary>
        public bool ConsumeSelect(out Shape s)
        {
            s = selectQueued;
            selectQueued = Shape.None;
            return s != Shape.None;
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
            // 弓箭手拉弓：起點標記＋拉弓進度條（拉滿變綠）
            if (BowHolding)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.8f);
                GUI.DrawTexture(new Rect(BowAnchor.x - d, H - BowAnchor.y - d, d * 2, d * 2), Texture2D.whiteTexture);
                float bw = Screen.width * 0.25f;
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(Palm.x - bw / 2, H - Palm.y - d * 6, bw, d), Texture2D.whiteTexture);
                GUI.color = BowDraw >= 1f ? Color.green : Color.yellow;
                GUI.DrawTexture(new Rect(Palm.x - bw / 2, H - Palm.y - d * 6, bw * BowDraw, d), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(Palm.x - 80, H - Palm.y - d * 10, 160, d * 4), BowDraw >= 1f ? "拉滿！張手放箭" : "拉弓中…", style);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(Palm.x - 80, H - Palm.y + d * 2, 160, d * 5), Label, style);
        }

        public string Label => Current switch
        {
            Shape.Fist => "握拳（蓄力）",
            Shape.Open => "張開",
            Shape.None => "",
            Shape.Other => "？",
            _ => ShapeName(Current),
        };
    }
}
