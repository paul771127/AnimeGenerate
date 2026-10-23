using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 電腦敵人（人形）的攻擊動作：預備（和畫面上的預警同時開始、同時結束）→ 出手（很快）→ 收招。
    ///   slash：劍士橫砍——劍往右後方拉、轉腰，出手時由右往左掃過胸前。
    ///   stab：刺客連刺——雙匕首收到腰邊、壓低重心，出手時往前突刺、踏一步。
    ///   lunge：刺客伏擊——同 stab，但幾乎沒有預備、踏步更大。
    ///   shoot：獵人射箭——拉弓（左手前伸、右手拉弦到臉旁），出手時放弦。
    ///   magic：法師／牧師——手（法杖）舉起凝聚，出手時往前推。
    /// 預備的長度＝預警時間，所以「看到預警線」和「看到它擺出架勢」是同一刻；出手的瞬間就是判定的瞬間。
    /// </summary>
    public partial class CharacterRig
    {
        string atkKind;
        float atkT = -1f, atkWind;
        const float AtkStrike = 0.12f, AtkHold = 0.1f, AtkRecover = 0.3f;

        /// <summary>播放攻擊動作。windup：預備秒數（＝預警時間；0＝直接出手）</summary>
        public void PlayAttack(string kind, float windup)
        {
            if (dead || isDummy || phoneDriven) return;
            atkKind = kind; atkWind = Mathf.Max(0f, windup); atkT = 0f;
            castT = -1f;   // 不要再疊一次一般的出手動作
        }

        /// <summary>正在播攻擊動作（這時候不用再播 PlayCast）</summary>
        public bool Attacking => atkT >= 0f;

        struct AtkPose { public Arm l, r; public float yaw, lean, step, crouch; }

        AtkPose WindPose(string k) => k switch
        {
            "novice" => new AtkPose { l = new Arm(-35,28,-65,0), r = new Arm(-76,48,-72,-12), yaw = 19, lean = -5, step = -.025f, crouch = .035f },
            "novice_throw" => new AtkPose { l = new Arm(-112,24,-91,-10), r = new Arm(5,13,-27,-5), yaw = -16, lean = -4, step = -.02f, crouch = .02f },
            "slash" => new AtkPose { l = new Arm(-35f, 25f, -50f, 0f), r = new Arm(-85f, 75f, -45f, 0f), yaw = 28f, lean = -4f, step = 0f, crouch = 0.04f },
            "stab" or "lunge" => new AtkPose { l = new Arm(-25f, 25f, -110f, 10f), r = new Arm(-15f, 20f, -120f, 10f), yaw = 18f, lean = 14f, step = -0.05f, crouch = 0.1f },
            "shoot" => new AtkPose { l = chargeL, r = chargeR, yaw = -12f, lean = 0f, step = 0f, crouch = 0f },
            _ => new AtkPose { l = chargeL, r = chargeR, yaw = 8f, lean = -6f, step = 0f, crouch = 0f },
        };

        AtkPose StrikePose(string k) => k switch
        {
            "novice" => new AtkPose { l = new Arm(-18,30,-42,8), r = new Arm(-62,-28,-24,20), yaw = -24, lean = 13, step = .075f, crouch = .025f },
            "novice_throw" => new AtkPose { l = new Arm(-73,-12,-8,38), r = new Arm(-9,20,-34,0), yaw = 17, lean = 11, step = .065f, crouch = .015f },
            "slash" => new AtkPose { l = new Arm(-20f, 15f, -40f, 0f), r = new Arm(-80f, -45f, -10f, 30f), yaw = -32f, lean = 10f, step = 0.1f, crouch = 0.02f },
            "stab" => new AtkPose { l = new Arm(-45f, 15f, -60f, 10f), r = new Arm(-92f, -6f, 0f, 0f), yaw = -10f, lean = 18f, step = 0.18f, crouch = 0.06f },
            "lunge" => new AtkPose { l = new Arm(-92f, 6f, 0f, 0f), r = new Arm(-92f, -6f, 0f, 0f), yaw = 0f, lean = 24f, step = 0.35f, crouch = 0.08f },
            "shoot" => new AtkPose { l = chargeL, r = castR, yaw = -12f, lean = -3f, step = 0f, crouch = 0f },
            _ => new AtkPose { l = castL, r = castR, yaw = -6f, lean = 8f, step = 0.06f, crouch = 0f },
        };

        /// <summary>Animate 裡：疊在一般姿勢上面（倒地前）</summary>
        void AttackArms(float dt, ref Arm aL, ref Arm aR, float deadE)
        {
            if (atkT < 0f) return;
            atkT += dt;
            float total = atkWind + AtkStrike + AtkHold + AtkRecover;
            if (atkT >= total || deadE > 0.01f) { atkT = -1f; return; }
            var w = WindPose(atkKind); var s = StrikePose(atkKind);
            AtkPose p; float k;   // k：疊上去的比例
            if (atkT < atkWind)
            {
                // 預備：慢慢擺好架勢（最後一點點會再往後收，蓄勁）
                float u = atkT / Mathf.Max(0.01f, atkWind);
                p = w; k = Smooth(Mathf.Clamp01(u * 1.6f));
                p.yaw *= 1f + 0.15f * Smooth(u);
            }
            else if (atkT < atkWind + AtkStrike)
            {
                // 出手：很快從架勢掃到結束姿勢
                float u = Smooth((atkT - atkWind) / AtkStrike);
                p = Blend(w, s, u); k = 1f;
            }
            else if (atkT < atkWind + AtkStrike + AtkHold) { p = s; k = 1f; }
            else { p = s; k = 1f - Smooth((atkT - atkWind - AtkStrike - AtkHold) / AtkRecover); }

            aL = Arm.Lerp(aL, p.l, k); aR = Arm.Lerp(aR, p.r, k);
            spine.localRotation *= Quaternion.Euler(p.lean * k, p.yaw * k, 0f);
            var bp = body.localPosition;
            body.localPosition = new Vector3(bp.x, bp.y - p.crouch * k, bp.z + p.step * k);
        }

        static AtkPose Blend(AtkPose a, AtkPose b, float t) => new AtkPose
        {
            l = Arm.Lerp(a.l, b.l, t), r = Arm.Lerp(a.r, b.r, t),
            yaw = Mathf.Lerp(a.yaw, b.yaw, t), lean = Mathf.Lerp(a.lean, b.lean, t),
            step = Mathf.Lerp(a.step, b.step, t), crouch = Mathf.Lerp(a.crouch, b.crouch, t),
        };

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
