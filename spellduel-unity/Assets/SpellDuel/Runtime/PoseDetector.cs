using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace SpellDuel
{
    /// <summary>
    /// 攻擊方用自己的鏡頭找出畫面中的人（骨架關鍵點），再換算成房間座標：
    ///   1. 把 AR 鏡頭畫面（和螢幕上看到的一樣，含裁切與旋轉）縮小畫到一張小圖，讀回 CPU。
    ///   2. 交給手機內建的人體姿勢偵測：Android 用 Google ML Kit，iOS 用 Apple Vision（iOS 14 以上）。
    ///   3. 回傳的關鍵點是「畫面上的位置」；配合拍攝當下的鏡頭位置與朝向，就能拉出一條射線。
    ///   4. 腳踝的射線和地板（世界座標 y=0）的交點 = 對手站的位置。
    /// 全部在手機上跑，不需要網路、不需要額外下載模型。
    /// </summary>
    public class PoseDetector
    {
        // 關鍵點順序（原生端回傳的也是這個順序）
        public const int Nose = 0, LShoulder = 1, RShoulder = 2, LHip = 3, RHip = 4, LAnkle = 5, RAnkle = 6, Count = 7;
        const int CaptureWidth = 288;
        const float Interval = 0.08f;   // 最多每秒約 12 次

        public class Result
        {
            public float time;                       // 拍攝時間（Time.time）
            public Vector3 camPos; public Quaternion camRot; public Matrix4x4 proj;   // 拍攝當下的鏡頭（AR 座標）
            public bool found;
            public readonly Vector2[] pt = new Vector2[Count];   // 視埠座標（0～1，左下為原點，和 Camera.ViewportToWorldPoint 相同）
            public readonly float[] conf = new float[Count];

            /// <summary>從拍攝當下的鏡頭穿過某個關鍵點的射線（AR 座標）</summary>
            public Ray RayThrough(int i)
            {
                // 視埠 → NDC → 鏡頭座標（Unity 鏡頭往 +Z 看）
                float nx = pt[i].x * 2f - 1f, ny = pt[i].y * 2f - 1f;
                var local = new Vector3((nx + proj.m02) / proj.m00, (ny + proj.m12) / proj.m11, 1f);
                return new Ray(camPos, (camRot * local).normalized);
            }
        }

        public Result Latest { get; private set; }   // 最近一次的偵測結果（不論有沒有找到人）
        public bool Supported { get; private set; }
        public string Status { get; private set; } = "未啟動";
        public RenderTexture Preview => rt;          // 除錯用：送去偵測的畫面
        public bool Enabled = true;

        Camera cam;
        ARCameraBackground background;
        RenderTexture rt;
        Texture2D readTex;
        byte[] frame;
        sbyte[] frameS;
        int w, h;
        float nextCapture;
        bool readbackPending, nativeBusy;
        Result pending;          // 已送出、等結果的那一張
        bool flipY;              // 有些繪圖 API 畫到 RenderTexture 會上下顛倒；一直找不到人就自動換方向試
        int misses;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject bridge;
#endif
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int sd_pose_available();
        [DllImport("__Internal")] static extern int sd_pose_submit(byte[] rgba, int width, int height);
        [DllImport("__Internal")] static extern int sd_pose_poll(float[] result, int n);
#endif

        public void Init(Camera camera, ARCameraBackground bg)
        {
            cam = camera;
            background = bg;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                bridge = new AndroidJavaObject("com.paul771127.spellduel.PoseBridge");
                Supported = true;
#elif UNITY_IOS && !UNITY_EDITOR
                Supported = sd_pose_available() != 0;
#else
                Supported = false;
#endif
            }
            catch (Exception e) { Supported = false; Status = "偵測器啟動失敗：" + e.Message; return; }
            Status = Supported ? "偵測中" : "這個平台沒有人體偵測（iOS 需 14 以上）";
        }

        /// <summary>在 LateUpdate 呼叫：視情況擷取一張畫面送去偵測，並收取結果</summary>
        public void Tick()
        {
            if (!Supported) return;
            Poll();
            if (!Enabled || readbackPending || nativeBusy || Time.time < nextCapture) return;
            if (background == null || background.material == null) return;
            nextCapture = Time.time + Interval;
            Capture();
        }

        void Capture()
        {
            int ch = Mathf.RoundToInt(CaptureWidth * (float)Screen.height / Mathf.Max(1, Screen.width)) & ~1;
            if (rt == null || rt.width != CaptureWidth || rt.height != ch)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(CaptureWidth, ch, 0, RenderTextureFormat.ARGB32);
                w = CaptureWidth; h = ch;
                frame = new byte[w * h * 4];
                frameS = new sbyte[w * h * 4];
            }
            // 用 AR 背景的材質畫到小圖：和螢幕上看到的畫面相同（含裁切、旋轉）
            var prev = RenderTexture.active;
            Graphics.Blit(null, rt, background.material);
            RenderTexture.active = prev;

            pending = new Result
            {
                time = Time.time,
                camPos = cam.transform.position,
                camRot = cam.transform.rotation,
                proj = cam.projectionMatrix,
            };

            if (SystemInfo.supportsAsyncGPUReadback)
            {
                readbackPending = true;
                AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, req =>
                {
                    readbackPending = false;
                    if (req.hasError) return;
                    Submit(req.GetData<byte>());
                });
            }
            else
            {
                if (readTex == null || readTex.width != w || readTex.height != h) readTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                prev = RenderTexture.active;
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                RenderTexture.active = prev;
                Submit(readTex.GetRawTextureData<byte>());
            }
        }

        // Unity 讀回的資料是由下往上；偵測器要由上往下的正常影像
        void Submit(Unity.Collections.NativeArray<byte> data)
        {
            if (data.Length < w * h * 4) return;
            int row = w * 4;
            var tmp = data.ToArray();
            for (int y = 0; y < h; y++)
            {
                int src = (flipY ? y : h - 1 - y) * row;
                Buffer.BlockCopy(tmp, src, frame, y * row, row);
            }
            for (int i = 3; i < frame.Length; i += 4) frame[i] = 255;   // alpha 一律不透明
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                Buffer.BlockCopy(frame, 0, frameS, 0, frame.Length);
                nativeBusy = bridge.Call<bool>("submit", frameS, w, h);
#elif UNITY_IOS && !UNITY_EDITOR
                nativeBusy = sd_pose_submit(frame, w, h) != 0;
#endif
            }
            catch (Exception e) { Status = "偵測失敗：" + e.Message; nativeBusy = false; }
        }

        readonly float[] raw = new float[2 + Count * 3];

        void Poll()
        {
            if (!nativeBusy) return;
            float[] r = null;
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                r = bridge.Call<float[]>("poll");
#elif UNITY_IOS && !UNITY_EDITOR
                if (sd_pose_poll(raw, raw.Length) != 0) r = raw;
#endif
            }
            catch (Exception e) { Status = "偵測失敗：" + e.Message; nativeBusy = false; return; }
            if (r == null || r.Length < raw.Length) return;   // 還沒好

            nativeBusy = false;
            var res = pending;
            if (res == null) return;
            res.found = r[1] > 0.5f;
            for (int i = 0; i < Count; i++)
            {
                // 原生端：左上為原點、0～1 → 轉成視埠（左下為原點）
                res.pt[i] = new Vector2(r[2 + i * 3], 1f - r[3 + i * 3]);
                res.conf[i] = r[4 + i * 3];
            }
            Latest = res;
            if (res.found) { misses = 0; Status = "偵測中：有找到人"; }
            else if (++misses % 12 == 0) { flipY = !flipY; Status = "偵測中：畫面中沒有人"; }
        }
    }
}
