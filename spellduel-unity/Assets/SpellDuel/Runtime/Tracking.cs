using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SpellDuel
{
    /// <summary>
    /// AR 追蹤狀態。追蹤中斷時（對著白牆、太暗、晃太快、鏡頭被擋住）手機不知道自己在哪，
    /// 這時的位置不可信，所以暫停命中判定。恢復後要穩定 0.5 秒才算恢復，避免一閃一閃。
    /// GameRoot 每幀呼叫 Tick()。
    /// </summary>
    public static class Tracking
    {
        public static bool Ok { get; private set; } = true;
        static float goodSince = -1f;

        public static void Tick(float time)
        {
            bool raw = ARSession.state == ARSessionState.SessionTracking;
            if (!raw) { Ok = false; goodSince = -1f; return; }
            if (goodSince < 0) goodSince = time;
            if (time - goodSince >= 0.5f) Ok = true;
        }

        public static string Reason
        {
            get
            {
                switch (ARSession.notTrackingReason)
                {
                    case NotTrackingReason.Initializing: return "AR 正在啟動";
                    case NotTrackingReason.Relocalizing: return "正在重新定位";
                    case NotTrackingReason.InsufficientLight: return "光線太暗";
                    case NotTrackingReason.InsufficientFeatures: return "畫面缺少紋理（避免對著白牆、天花板）";
                    case NotTrackingReason.ExcessiveMotion: return "手機晃動太快";
                    case NotTrackingReason.CameraUnavailable: return "鏡頭無法使用（被擋住？）";
                    default: return "請把鏡頭對著有紋理的地方，慢慢移動";
                }
            }
        }
    }
}
