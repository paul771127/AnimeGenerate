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
        public const float ThirdPersonDist = 3f;
        const string ThirdPersonKey = "SD_ThirdPerson";
        public static bool ThirdPerson
        {
            get => PlayerPrefs.GetInt(ThirdPersonKey, 1) == 1;
            set { PlayerPrefs.SetInt(ThirdPersonKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        CharacterRig selfRig; Vector3 selfLastFeet;
        Vector3 tpLastFwd, tpOffsetW; bool tpHas;

        bool TpOn => ThirdPerson && !NetMode && !Watching && battle != null && phase == Phase.Fighting && Me != null && area != null;

        /// <summary>SyncPlayer 之後：把角色放到手機前方；轉身時世界平移，讓角色留在原地</summary>
        void ApplyThirdPerson(Fighter me)
        {
            if (!TpOn) { tpHas = false; tpOffsetW = Vector3.zero; return; }
            var f = Fighter.Flat(me.forward);
            if (f.sqrMagnitude < 1e-4f) f = tpHas ? tpLastFwd : Vector3.forward;
            f.Normalize();
            if (tpHas)
            {
                var delta = -(f - tpLastFwd) * ThirdPersonDist;
                if (delta.sqrMagnitude > 0.004f * 0.004f)   // 手機微微晃動不平移（省效能）；累積夠了才一次移
                {
                    ShiftWorld(-WorldFrame.DirFromWorld(delta));
                    me.head += delta; dashMoveW += delta;
                    tpLastFwd = f;
                }
                else f = tpLastFwd;
            }
            else { tpLastFwd = f; tpHas = true; }
            tpOffsetW = f * ThirdPersonDist;
            me.head += tpOffsetW;
        }

        /// <summary>每幀：主角的 3D 角色跟著角色位置、面向、動作</summary>
        void UpdateSelfRig()
        {
            bool show = TpOn && Me != null;
            if (!show) { if (selfRig) { Destroy(selfRig.gameObject); selfRig = null; } return; }
            var adv = Progress.Char(myClass).adv;
            if (selfRig && (selfRig.ClassId != myClass || selfRig.AdvancedId != adv)) { Destroy(selfRig.gameObject); selfRig = null; }
            if (!selfRig)
            {
                selfRig = CharacterRig.Create(myClass, adv);
                selfRig.SetGear(GearLook.Of(Progress.Char(myClass).equipped));
                selfLastFeet = Me.Feet;
            }
            float dt = Mathf.Max(1e-3f, Time.deltaTime);
            selfRig.SetPose(WorldFrame.FromWorld(Me.Feet), WorldFrame.DirFromWorld(Fighter.Flat(Me.forward)));
            selfRig.MoveSpeed = Mathf.Lerp(selfRig.MoveSpeed, Fighter.Flat(Me.Feet - selfLastFeet).magnitude / dt, 0.2f);
            selfLastFeet = Me.Feet;
            selfRig.SetDead(!Me.Alive);
            selfRig.Charge = Me.Alive && Me.charging != null ? Mathf.Clamp01((battle.now - Me.chargeStart) / Mathf.Max(0.3f, Me.charging.charge)) : 0f;
            selfRig.Guarding = Me.Alive && Me.crouching;
        }
    }
}
