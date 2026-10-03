using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 兩支手機共用的「世界座標」：以地上的標記圖為原點。
    /// 每支手機各自的 AR 座標（session space）不同，但都看得到同一張標記圖，
    /// 所以把所有要同步的位置都換成「相對標記圖」的座標再傳送，兩邊就一致了。
    /// 標記圖平放在地上時：Y 軸＝圖的法線（朝上），Z 軸＝圖的上緣方向（三角形那一側）。
    /// </summary>
    public static class WorldFrame
    {
        public static bool Calibrated { get; private set; }
        public static Pose Marker { get; private set; } = Pose.identity;
        public static float LastSeenTime { get; private set; } = -999f;

        public static void SetMarker(Pose markerInSession)
        {
            Marker = markerInSession;
            Calibrated = true;
            LastSeenTime = Time.time;
        }

        public static void Reset() => Calibrated = false;

        // 位置
        public static Vector3 ToWorld(Vector3 sessionPos) => Quaternion.Inverse(Marker.rotation) * (sessionPos - Marker.position);
        public static Vector3 FromWorld(Vector3 worldPos) => Marker.position + Marker.rotation * worldPos;

        // 方向
        public static Vector3 DirToWorld(Vector3 sessionDir) => Quaternion.Inverse(Marker.rotation) * sessionDir;
        public static Vector3 DirFromWorld(Vector3 worldDir) => Marker.rotation * worldDir;

        // 旋轉
        public static Quaternion RotToWorld(Quaternion sessionRot) => Quaternion.Inverse(Marker.rotation) * sessionRot;
        public static Quaternion RotFromWorld(Quaternion worldRot) => Marker.rotation * worldRot;
    }
}
