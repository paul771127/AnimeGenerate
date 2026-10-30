using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 受擊硬直的動作（使用者要求：小怪、菁英、稀有怪每掉 1/3 血）：整個模型往後仰、往後退一小步，再慢慢站回來。
    /// 套在模型根物件上，任何小怪模型都能用。SoloBattle 每幀會重設位置與面向；沒重設的幀先把上一幀加的偏移拿掉，不會越疊越歪。
    /// </summary>
    public class StaggerJolt : MonoBehaviour
    {
        public const float Duration = 0.9f, LeanDeg = 28f, PushBack = 0.15f;
        float t = -1f; Vector3 away;
        Vector3 appliedPos; Quaternion appliedRot; Vector3 basePos; Quaternion baseRot; bool applied;

        public static void Play(GameObject go, Vector3 awayDir)
        {
            if (go == null) return;
            var j = go.GetComponent<StaggerJolt>();
            if (j == null) j = go.AddComponent<StaggerJolt>();
            awayDir.y = 0f;
            j.away = awayDir.sqrMagnitude > 1e-6f ? awayDir.normalized : -go.transform.forward;
            j.t = 0f;
        }

        /// <summary>0→1→0 的曲線：0.08 秒內被打到最仰，之後慢慢回來（帶一點回彈）</summary>
        public static float Curve(float t)
        {
            if (t < 0f || t >= Duration) return 0f;
            if (t < 0.08f) return t / 0.08f;
            float k = (t - 0.08f) / (Duration - 0.08f);
            return (1f - k) * (1f - k) * (1f + 0.25f * Mathf.Sin(k * Mathf.PI * 3f));
        }

        void LateUpdate()
        {
            var tr = transform;
            // 這幀沒有被重設（還是上一幀加過偏移的樣子）：先還原
            if (applied && tr.position == appliedPos && tr.rotation == appliedRot) { tr.SetPositionAndRotation(basePos, baseRot); }
            applied = false;
            if (t < 0f) return;
            t += Time.deltaTime;
            float k = Curve(t);
            if (t >= Duration) { t = -1f; return; }
            basePos = tr.position; baseRot = tr.rotation;
            var axis = Vector3.Cross(Vector3.up, away); if (axis.sqrMagnitude < 1e-6f) axis = tr.right;
            var rot = Quaternion.AngleAxis(LeanDeg * k, axis.normalized) * baseRot;
            var pos = basePos + away * (PushBack * k);
            tr.SetPositionAndRotation(pos, rot);
            appliedPos = pos; appliedRot = rot; applied = true;
        }
    }
}
