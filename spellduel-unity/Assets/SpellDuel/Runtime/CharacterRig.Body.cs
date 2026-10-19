using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 真人的分身：角色的姿勢照著那個人的手機動。
    ///   假設：手機一隻手拿在眼前、對著前方；另一隻手伸到手機前比手勢施法。
    ///   ・手機高度 → 站／蹲（依那個人校正的站姿、蹲姿高度），太高＝跳起來
    ///   ・手機朝向 → 身體轉向、抬頭低頭
    ///   ・左手：握著手機（手伸到眼前）
    ///   ・右手：鏡頭裡看到手勢時伸到手勢的位置，沒有就照原本的動作（走路擺手、詠唱、出手…）
    ///   ・整個角色依站姿高度等比例縮放，頭跟真人的頭一樣高
    /// 沒有呼叫 DrivePhone（電腦對手、Boss、居民）就照原本的動作。
    /// </summary>
    public partial class CharacterRig
    {
        /// <summary>角色站直時眼睛離地的高度（未縮放，公尺）</summary>
        const float RigEyeHeight = 1.57f;
        /// <summary>手機拿在眼睛前方多遠（公尺）</summary>
        public const float PhoneReach = 0.3f;
        const float ThighLen = 0.42f, ShinLen = 0.43f;

        bool phoneDriven;
        Vector3 phonePos, gesturePos;
        Quaternion phoneRot = Quaternion.identity;
        float standH = 1.55f, crouchH = 0.95f, crouchAmt, gestureAmt, phoneLast = -99f;
        bool gesture;

        /// <summary>
        /// 用真人手機的位置／朝向驅動角色（場景座標）。standH／crouchH：那個人站姿、蹲姿時的手機高度（≤0＝沒有校正，用預設）。
        /// gestureHand：手勢的手在鏡頭裡；gestureAt：那隻手的位置（場景座標）。每幀呼叫；超過 0.5 秒沒呼叫就回到一般動作。
        /// </summary>
        public void DrivePhone(Vector3 phone, Quaternion rot, float stand, float crouch, bool gestureHand, Vector3 gestureAt)
        {
            if (isDummy || body == null) return;
            phonePos = phone; phoneRot = rot; gesture = gestureHand; gesturePos = gestureAt;
            if (stand > 0.6f && stand < 2.3f) standH = stand;
            crouchH = crouch > 0.2f && crouch < standH - 0.1f ? crouch : standH * 0.62f;
            phoneLast = Time.time; phoneDriven = true;

            // 腳在眼睛正下方，眼睛在手機後方 PhoneReach
            var fwd = rot * Vector3.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : transform.forward;
            float s = Mathf.Clamp(standH / RigEyeHeight, 0.6f, 1.3f);
            transform.localScale = Vector3.one * s;
            var feet = phone - fwd * PhoneReach; feet.y = 0f;
            transform.SetPositionAndRotation(feet, Quaternion.LookRotation(fwd, Vector3.up));
        }

        /// <summary>Animate 最後呼叫：有手機資料時覆寫腿、身體、頭、手臂</summary>
        void ApplyPhoneBody(float dt, float deadE)
        {
            if (!phoneDriven) return;
            if (Time.time - phoneLast > 0.5f) { phoneDriven = false; transform.localScale = Vector3.one; return; }
            float k = 1f - deadE;
            float s = transform.lossyScale.y > 1e-3f ? transform.lossyScale.y : 1f;

            // ---- 站／蹲：手機比站姿低多少 → 蹲多深；比站姿高＝跳起來
            float y = transform.InverseTransformPoint(phonePos).y;      // 角色本地（未縮放）的手機高度
            float standLocal = standH / s, crouchLocal = crouchH / s;
            crouchAmt = Mathf.Lerp(crouchAmt, Mathf.Clamp01((standLocal - y) / Mathf.Max(0.1f, standLocal - crouchLocal)), 1f - Mathf.Exp(-dt * 12f));
            float drop = Mathf.Clamp(RigEyeHeight - y, 0f, 0.75f) * k;
            float lift = Mathf.Clamp(y - RigEyeHeight, 0f, 0.4f) * k;
            float a = SquatAngle(drop);                                  // 大腿往前抬的角度（度）
            float b = a * 0.9f;                                          // 小腿往後的角度
            float footZ = ThighLen * Mathf.Sin(a * Mathf.Deg2Rad) - ShinLen * Mathf.Sin(b * Mathf.Deg2Rad);
            var bp = body.localPosition;
            body.localPosition = new Vector3(bp.x, bp.y - drop + lift, bp.z - footZ);
            float walkLegs = Mathf.Clamp01(1f - crouchAmt * 2f);           // 蹲下時不擺腿
            hipL.localRotation = Quaternion.Slerp(Quaternion.Euler(-a, 0f, -3f), hipL.localRotation, walkLegs * (1f - Mathf.Clamp01(a / 15f)));
            hipR.localRotation = Quaternion.Slerp(Quaternion.Euler(-a, 0f, 3f), hipR.localRotation, walkLegs * (1f - Mathf.Clamp01(a / 15f)));
            if (a > 1f) { knL.localRotation = Quaternion.Euler(a + b, 0f, 0f); knR.localRotation = Quaternion.Euler(a + b, 0f, 0f); }

            // ---- 身體轉向、頭：手機往下看＝低頭（蹲下時上身前傾保持平衡）
            var f = Quaternion.Inverse(transform.rotation) * (phoneRot * Vector3.forward);
            float pitch = Mathf.Asin(Mathf.Clamp(-f.y, -1f, 1f)) * Mathf.Rad2Deg;   // 往下看為正
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            spine.localRotation = Quaternion.Euler(crouchAmt * 22f * k + Mathf.Clamp(pitch, -10f, 30f) * 0.25f, Mathf.Clamp(yaw, -25f, 25f) * 0.4f, 0f);
            neck.localRotation = Quaternion.Euler(Mathf.Clamp(pitch * 0.75f - crouchAmt * 15f, -35f, 45f), Mathf.Clamp(yaw, -40f, 40f) * 0.6f, 0f);

            if (k <= 0.01f) return;
            var bodyFwd = transform.forward; var up = transform.up; var right = transform.right;
            // ---- 左手拿手機（伸到眼前）
            SolveArm(shL, elL, handL, Vector3.Lerp(handL.position, phonePos + up * -0.02f * s, k), -right, bodyFwd, up);
            // ---- 右手：比手勢時伸到手勢的位置
            gestureAmt = Mathf.MoveTowards(gestureAmt, gesture ? 1f : 0f, dt * 6f);
            if (gestureAmt > 0.001f) SolveArm(shR, elR, handR, Vector3.Lerp(handR.position, gesturePos, gestureAmt * k), right, bodyFwd, up);
        }

        /// <summary>蹲低 drop（公尺）需要大腿抬多少度（小腿往後 0.9 倍，腳留在原地）</summary>
        static float SquatAngle(float drop)
        {
            if (drop <= 0.001f) return 0f;
            float lo = 0f, hi = 115f;
            for (int i = 0; i < 18; i++)
            {
                float m = (lo + hi) * 0.5f;
                float h = ThighLen * Mathf.Cos(m * Mathf.Deg2Rad) + ShinLen * Mathf.Cos(m * 0.9f * Mathf.Deg2Rad);
                if (ThighLen + ShinLen - h < drop) lo = m; else hi = m;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>兩節手臂的 IK：手伸到 target（世界座標）；手肘往外下方彎</summary>
        static void SolveArm(Transform sh, Transform el, Transform hand, Vector3 target, Vector3 outward, Vector3 fwd, Vector3 up)
        {
            Vector3 S = sh.position;
            float l1 = Vector3.Distance(S, el.position), l2 = Vector3.Distance(el.position, hand.position);
            if (l1 < 1e-4f || l2 < 1e-4f) return;
            var toT = target - S; float d = toT.magnitude;
            if (d < 1e-4f) return;
            var dir = toT / d;
            d = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 1e-3f, (l1 + l2) * 0.999f);
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            var pole = outward * 0.6f - up * 0.8f - fwd * 0.1f;
            var ortho = Vector3.ProjectOnPlane(pole, dir);
            if (ortho.sqrMagnitude < 1e-6f) ortho = Vector3.ProjectOnPlane(-up, dir);
            ortho.Normalize();
            var E = S + dir * (l1 * cosA) + ortho * (l1 * Mathf.Sqrt(1f - cosA * cosA));
            var dU = (E - S).normalized;
            sh.rotation = Quaternion.LookRotation(Fwd(fwd, dU), -dU);
            var dL = (S + dir * d - el.position).normalized;
            el.rotation = Quaternion.LookRotation(Fwd(fwd, dL), -dL);
            hand.localRotation = Quaternion.identity;
        }

        static Vector3 Fwd(Vector3 fwd, Vector3 axis)
        {
            var f = Vector3.ProjectOnPlane(fwd, axis);
            if (f.sqrMagnitude < 1e-6f) f = Vector3.ProjectOnPlane(Vector3.up, axis);
            return f.normalized;
        }
    }
}
