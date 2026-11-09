using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 第三人稱（使用者要求）：攝影機在角色後方，看得到整隻主角。
    /// AR 裡攝影機就是手機，搬不動 → 反過來把「角色」放在手機前方 ThirdPersonDist 公尺（地面上），
    /// 所有判定（被打、走路、牆、敵人追誰）都用角色的位置。
    /// 轉動手機時：角色留在原地、只轉身，畫面像攝影機繞著角色轉（用衝刺的世界平移 ShiftWorld 做，打完一場移回原位）。
    /// 蹲下、跳起來照舊看手機的高度（角色跟著做）。
    /// 只有單人（連線的對戰、合作照舊第一人稱）；設定裡可以關掉。
    /// 角色模型：角色生成器做好之前先用程式拼的角色（CharacterRig）。
    /// </summary>
    public partial class SoloBattle
    {
        /// <summary>
        /// 角色在手機前方幾公尺；往左偏多少（越肩：角色在畫面偏左，正前方近身的敵人從右肩旁邊看得到）；
        /// 攝影機高度＝約 2.9m（使用者要求：頭頂上方半顆頭再高 1m）→ 世界往下降「這個高度 − 手機站著的高度」
        /// </summary>
        public const float ThirdPersonDist = 3.5f, ThirdPersonSide = 0.55f, HeroCamHeight = 2.9f, MaxLift = 2.3f;   // 使用者：至少比頭頂上方半顆頭再高 1m
        float tpLift;
        /// <summary>第三人稱主角的大小（使用者要求縮成 70%；只縮外觀，判定不變）</summary>
        public const float HeroScale = 0.49f;   // 使用者：70% 再縮 70%
        /// <summary>第三人稱的準心在畫面高度的哪裡（從上往下的比例）：比正中間高一點，不會對到角色的頭</summary>
        public const float TpAimY = 0.38f;
        static bool tpAimNow;
        /// <summary>準心的位置（GUI 座標比例，從上往下）</summary>
        static float AimYFrac => tpAimNow ? TpAimY : 0.5f;
        const string ThirdPersonKey = "SD_ThirdPerson";
        public static bool ThirdPerson
        {
            get => PlayerPrefs.GetInt(ThirdPersonKey, 1) == 1;
            set { PlayerPrefs.SetInt(ThirdPersonKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        CharacterRig selfRig;
        Vector3 tpLastFwd, tpOffsetW; bool tpHas, tpLifted;
        Vector3 tpTurnMoveW;

        bool TpOn => ThirdPerson && !NetMode && !Watching && battle != null && phase == Phase.Fighting && Me != null && area != null;

        /// <summary>SyncPlayer 之後：把角色放到手機前方；轉身時世界平移，讓角色留在原地</summary>
        void ApplyThirdPerson(Fighter me)
        {
            tpAimNow = TpOn;
            if (!TpOn)
            {
                if (tpLifted && area != null) { ShiftWorld(WorldFrame.DirFromWorld(Vector3.up) * tpLift); me.head.y -= tpLift; }   // 中途關掉：世界升回來
                tpLifted = false; tpHas = false; tpOffsetW = Vector3.zero; return;
            }
            // 攝影機比角色頭頂高半顆頭：整個世界往下降（判定用的高度扣回來，蹲、跳照舊）
            if (!tpLifted)
            {
                float stand = standH > 0.8f ? standH : me.head.y;   // 站著時手機的高度（校正過就用校正值）
                tpLift = Mathf.Clamp(HeroCamHeight - stand, 0f, MaxLift);
                if (tpLift > 0.01f) ShiftWorld(WorldFrame.DirFromWorld(Vector3.down) * tpLift);
                tpLifted = true;   // 這一幀的位置是平移前算的，不用扣
            }
            else me.head.y -= tpLift;
            var f = Fighter.Flat(me.forward);
            if (f.sqrMagnitude < 1e-4f) f = tpHas ? tpLastFwd : Vector3.forward;
            f.Normalize();
            if (tpHas)
            {
                // 原地轉身時手機會繞著身體畫一小段弧（手臂長）：也一起扣掉，角色才不會跟著挪（使用者：旋轉時不用動）
                var delta = -(TpOffset(f) - TpOffset(tpLastFwd)) - (f - tpLastFwd) * TpArm;
                if (delta.sqrMagnitude > 0.004f * 0.004f)   // 手機微微晃動不平移（省效能）；累積夠了才一次移
                {
                    ShiftWorld(-WorldFrame.DirFromWorld(delta));
                    me.head += delta; dashMoveW += delta;
                    tpTurnMoveW += delta;
                    tpLastFwd = f;
                }
                else f = tpLastFwd;
            }
            else { tpLastFwd = f; tpHas = true; heroHasMotion = false; }
            tpOffsetW = TpOffset(f);
            me.head += tpOffsetW;
        }

        /// <summary>手機離身體的距離（轉身時手機畫的弧半徑）</summary>
        const float TpArm = 0.3f;

        /// <summary>角色相對手機的位置：前方 ThirdPersonDist、往左 ThirdPersonSide</summary>
        static Vector3 TpOffset(Vector3 f) => f * ThirdPersonDist - new Vector3(f.z, 0f, -f.x) * ThirdPersonSide;

        /// <summary>放招／普攻的動作（使用者回報大魔導師攻擊沒動作：以前只有詠唱放出才播，普攻沒有）。普攻播快一點</summary>
        void HeroAttackAnim(bool basic)
        {
            if (heroAimDir.sqrMagnitude > 0.5f) { heroFaceDir = heroAimDir; heroFaceUntil = Time.time + 1.2f; }   // 轉向準心對的方向攻擊
            if (heroModel) { heroModel.PlayOnce("cast", basic ? 2.2f : 1.3f); heroCastAnimAt = Time.time; }
            else if (selfRig) { selfRig.PlayAttack(myClass == Skills.Novice ? "novice" : AttackKindOf(myClass), basic ? 0.06f : 0.15f); }
        }
        float heroCastAnimAt = -9f, heroFaceUntil = -9f;
        Vector3 heroAimDir, heroFaceDir, heroMoveW; float heroSpeed;
        Vector3 heroLastPhone, heroLastForward, heroLastVirtualMove; bool heroHasMotion;

        // 只扣掉實際觀測到的轉身弧線；手機位置沒動時，不憑旋轉量產生反向位移。
        static Vector3 HeroTranslationStep(Vector3 phoneStep, Vector3 armArc)
        {
            phoneStep.y = 0f; armArc.y = 0f;
            if (armArc.sqrMagnitude > 1e-12f)
                phoneStep -= armArc * Mathf.Clamp01(Vector3.Dot(phoneStep, armArc) / armArc.sqrMagnitude);
            return phoneStep;
        }

        float UpdateHeroMovement(float dt)
        {
            if (!Tracking.Ok) { heroHasMotion = false; heroMoveW = Vector3.zero; heroSpeed = 0f; return 0f; }
            var phone = cam.transform.position;
            var forward = Fighter.Flat(cam.transform.forward).normalized;
            // dashMoveW 也包含第三人稱轉向的世界補償；補償不能算成走路或衝刺。
            var virtualMove = dashMoveW - tpTurnMoveW;
            var step = Vector3.zero;
            if (heroHasMotion)
            {
                var physical = HeroTranslationStep(phone - heroLastPhone, (forward - heroLastForward) * TpArm);
                step = WorldFrame.DirToWorld(physical) + virtualMove - heroLastVirtualMove;
            }
            heroLastPhone = phone; heroLastForward = forward; heroLastVirtualMove = virtualMove; heroHasMotion = true;
            var velocity = Fighter.Flat(step) / dt;
            float rawSpeed = velocity.magnitude;
            // 停下後立即歸零，避免平滑殘值讓原地旋轉繼續踏步；忽略定位的細小抖動。
            if (rawSpeed < 0.12f) { heroSpeed = 0f; heroMoveW = Vector3.zero; return 0f; }
            heroSpeed = Mathf.Lerp(heroSpeed, rawSpeed, 1f - Mathf.Exp(-dt * 6f));
            heroMoveW = Vector3.Lerp(heroMoveW, velocity, 1f - Mathf.Exp(-dt * 10f));
            return heroSpeed < 0.45f ? 0f : heroSpeed;
        }
        /// <summary>角色現在要面對的方向（場地座標）：剛攻擊過＝準心的方向，否則＝手機面向</summary>
        Vector3 HeroFacing => Time.time < heroFaceUntil ? heroFaceDir : Fighter.Flat(Me.forward);

        /// <summary>每幀：主角的 3D 角色跟著角色位置、面向、動作（有生成器的模型就用模型：HeroModel.cs）</summary>
        HeroModel heroModel; bool heroWasCharging; float heroLastHp = -1f;
        void UpdateSelfRig()
        {
            bool show = TpOn && Me != null;
            if (!show)
            {
                if (selfRig) { Destroy(selfRig.gameObject); selfRig = null; }
                if (heroModel) { Destroy(heroModel.gameObject); heroModel = null; }
                heroLastHp = -1f; heroHasMotion = false; heroSpeed = 0f; heroMoveW = Vector3.zero; return;
            }
            var adv = Progress.Char(myClass).adv;
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            float speed = UpdateHeroMovement(dt);
            // 生成器的模型
            string kit = HeroModel.KitFor(myClass, adv);
            if (heroModel && heroModel.Kit != kit) { Destroy(heroModel.gameObject); heroModel = null; }
            if (!heroModel && kit != null) { heroModel = HeroModel.Create(myClass, adv); if (heroModel) heroModel.transform.localScale = heroModel.transform.localScale * HeroScale; }
            if (heroModel)
            {
                if (selfRig) { Destroy(selfRig.gameObject); selfRig = null; }
                bool charging = Me.Alive && Me.charging != null;
                if (heroWasCharging && !charging && Me.Alive && Time.time - heroCastAnimAt > 0.3f) heroModel.PlayOnce("cast", 1.3f);   // 放出去了（沒經過 ReleaseAt 的也播）
                heroWasCharging = charging;
                if (heroLastHp >= 0f && Me.hp < heroLastHp - 0.5f && Me.Alive && !charging) heroModel.PlayOnce("hit");
                heroLastHp = Me.hp;
                heroModel.Drive(WorldFrame.FromWorld(Me.Feet), WorldFrame.DirFromWorld(HeroFacing), WorldFrame.DirFromWorld(heroMoveW), speed, !Me.Alive, charging, Me.crouching, Me.airborneUntil > battle.now, Time.time < heroFaceUntil);
                return;
            }
            // 沒有模型：程式拼的角色
            if (selfRig && (selfRig.ClassId != myClass || selfRig.AdvancedId != adv)) { Destroy(selfRig.gameObject); selfRig = null; }
            if (!selfRig)
            {
                selfRig = CharacterRig.Create(myClass, adv);
                selfRig.SetGear(GearLook.Of(Progress.Char(myClass).equipped));
                selfRig.transform.localScale = selfRig.transform.localScale * HeroScale;
            }
            selfRig.SetPose(WorldFrame.FromWorld(Me.Feet), WorldFrame.DirFromWorld(HeroFacing));
            selfRig.MoveSpeed = speed;
            selfRig.SetDead(!Me.Alive);
            selfRig.Charge = Me.Alive && Me.charging != null ? Mathf.Clamp01((battle.now - Me.chargeStart) / Mathf.Max(0.3f, Me.charging.charge)) : 0f;
            selfRig.Guarding = Me.Alive && Me.crouching;
        }
    }
}
