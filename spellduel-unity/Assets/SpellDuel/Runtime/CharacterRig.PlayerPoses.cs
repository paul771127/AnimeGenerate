using UnityEngine;

namespace SpellDuel
{
    public partial class CharacterRig
    {
        // Cached once per rig. These values affect presentation only, never combat timing.
        float poseCrouchLean, poseJumpTuck, poseJumpAsym, poseChargeLean, poseChargeTwist;
        float poseLanding, poseLastLift;
        bool poseLongCoat;
        Transform poseStick, poseBow;
        LineRenderer poseString;

        void SetupPlayerPoses()
        {
            string id = advancedId ?? classId;
            if(id=="novice")poseStick=handR.Find("Sword");
            if(classId=="archer")
            {
                poseBow=handL.Find("Bow");
                if(poseBow!=null)
                {
                    foreach(var renderer in poseBow.GetComponentsInChildren<MeshRenderer>())
                        if(renderer.sharedMaterial.shader==glowShader)RemoveVisual(renderer.gameObject);
                    var stringRoot=new GameObject("Animated bowstring");stringRoot.transform.SetParent(poseBow,false);
                    poseString=stringRoot.AddComponent<LineRenderer>();poseString.useWorldSpace=true;poseString.positionCount=3;
                    poseString.widthMultiplier=.004f;poseString.sharedMaterial=Glow(new Color(.95f,.93f,.78f));
                    poseString.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            poseLongCoat = OutfitArt.Get(id).AnimationHem > .15f;
            poseCrouchLean = 19; poseJumpTuck = 28; poseJumpAsym = 8;
            poseChargeLean = 2; poseChargeTwist = -8;
            castMaskL = castMaskR = 1;
            switch (id)
            {
                case "novice":
                    PoseArms(new Arm(-42,18,-54,-8), new Arm(-68,32,-65,-8), new Arm(-52,12,-66,0), new Arm(-48,-5,-54,0));
                    poseChargeTwist=16; poseJumpTuck=32; break;
                case "swordsman":
                    PoseArms(new Arm(-38,22,-60,0), new Arm(-78,48,-65,-12), new Arm(-55,16,-66,0), new Arm(-64,-20,-42,12));
                    poseChargeTwist=22; poseChargeLean=-3; break;
                case "paladin":
                    PoseArms(new Arm(-53,18,-56,0), new Arm(-68,28,-72,-8), new Arm(-65,5,-54,0), new Arm(-38,18,-66,0));
                    poseJumpTuck=23; poseJumpAsym=3; poseCrouchLean=14; break;
                case "berserker": // 劍仙: poised sword-finger release; keep the existing jian/qi rig.
                    PoseArms(new Arm(-72,-8,-32,8), new Arm(-35,26,-58,0), new Arm(-48,22,-32,8), new Arm(-53,-8,-60,8));
                    poseChargeTwist=-16; poseJumpTuck=20; poseJumpAsym=16; break;
                case "archer":
                    PoseArms(new Arm(-78,10,-12,0), new Arm(-62,-26,-104,0), new Arm(-42,24,-36,0), new Arm(-36,-6,-86,0));
                    poseChargeTwist=18; poseCrouchLean=17; break;
                case "marksman":
                    PoseArms(new Arm(-84,6,-8,0), new Arm(-72,-32,-106,0), new Arm(-48,16,-28,0), new Arm(-44,-14,-94,0));
                    poseChargeTwist=25; poseJumpTuck=22; poseJumpAsym=4; break;
                case "ranger":
                    PoseArms(new Arm(-70,18,-22,0), new Arm(-54,-20,-92,8), new Arm(-38,30,-44,0), new Arm(-30,-10,-80,0));
                    poseChargeTwist=12; poseJumpTuck=38; poseJumpAsym=17; poseCrouchLean=24; break;
                case "mage":
                    PoseArms(new Arm(-52,24,-62,12), new Arm(-60,16,-38,0), new Arm(-48,8,-70,10), new Arm(-40,22,-62,0));
                    poseChargeLean=-3; poseChargeTwist=-12; poseJumpTuck=20; break;
                case "archmage":
                    PoseArms(new Arm(-65,36,-38,24), new Arm(-80,20,-18,0), new Arm(-58,25,-50,20), new Arm(-50,10,-55,0));
                    poseChargeLean=-4; poseChargeTwist=-18; poseJumpTuck=18; poseJumpAsym=4; break;
                case "summoner":
                    PoseArms(new Arm(-65,32,-40,28), new Arm(-43,26,-60,8), new Arm(-52,30,-58,18), new Arm(-32,12,-72,0));
                    poseChargeTwist=14; poseJumpTuck=30; poseJumpAsym=15; break;
                case "assassin":
                    PoseArms(new Arm(-28,30,-102,65), new Arm(-18,28,-108,65), new Arm(-42,24,-78,65), new Arm(-52,-4,-60,65));
                    poseChargeLean=12; poseChargeTwist=20; poseJumpTuck=42; poseCrouchLean=27; break;
                case "ninja":
                    PoseArms(new Arm(-50,-10,-76,45), new Arm(-46,32,-96,65), new Arm(-52,16,-65,40), new Arm(-36,-14,-90,60));
                    poseChargeLean=9; poseChargeTwist=-20; poseJumpTuck=45; poseJumpAsym=18; poseCrouchLean=26; break;
                case "shadowdancer":
                    PoseArms(new Arm(-35,42,-65,60), new Arm(-62,8,-38,65), new Arm(-48,36,-52,60), new Arm(-60,4,-52,65));
                    poseChargeLean=8; poseChargeTwist=-25; poseJumpTuck=36; poseJumpAsym=23; poseCrouchLean=25; break;
                case "priest":
                    PoseArms(new Arm(-47,8,-72,20), new Arm(-62,14,-34,0), new Arm(-53,8,-62,20), new Arm(-36,18,-68,0));
                    poseChargeLean=-2; poseChargeTwist=-6; poseJumpTuck=18; poseJumpAsym=3; poseCrouchLean=15; break;
                case "bishop":
                    PoseArms(new Arm(-42,18,-62,12), new Arm(-76,14,-22,8), new Arm(-45,22,-58,10), new Arm(-52,10,-48,12));
                    poseChargeLean=-3; poseChargeTwist=8; poseJumpTuck=16; poseJumpAsym=2; poseCrouchLean=14; break;
                case "inquisitor":
                    PoseArms(new Arm(-42,26,-56,18), new Arm(-75,30,-36,-8), new Arm(-58,16,-58,15), new Arm(-60,-8,-42,8));
                    poseChargeLean=5; poseChargeTwist=18; poseJumpTuck=30; poseCrouchLean=21; break;
            }
            // Bow arms retain the string-release pose; all casters visibly push from the gathered pose.
            if(classId=="mage" || classId=="priest")
            {castL=new Arm(-84,18,-12,38);castR=new Arm(-88,12,-8,20);}
        }

        void PoseArms(Arm leftCharge, Arm rightCharge, Arm leftGuard, Arm rightGuard)
        {chargeL=leftCharge;chargeR=rightCharge;guardL=leftGuard;guardR=rightGuard;}

        void ApplyPlayerGrip(float deadE)
        {
            // Cant a long blade/staff out of the face without moving it out of the gripping hand.
            if(!Attacking && deadE<.01f && (classId=="swordsman"||classId=="mage"||classId=="priest"))
                handR.localRotation*=Quaternion.Euler(0,classId=="swordsman"?18:15,0);
            if(poseBow!=null && poseString!=null)
            {
                float pull=Mathf.Lerp(.25f,1,ChargePoseWeight(Mathf.Clamp01(Charge)))*combatAmount*(1-guardAmt)*(1-deadE);
                if(Attacking)pull=atkT<atkWind?Smooth(atkT/Mathf.Max(.01f,atkWind)):1-Smooth((atkT-atkWind)/AtkStrike);
                if(castT>=0)pull*=1-Smooth(castT/CastDur);
                var middle=poseBow.TransformPoint(new Vector3(0,.16f+.20f*pull,0));
                if(pull>.05f && !gesture)
                    SolveArm(shR,elR,handR,Vector3.Lerp(handR.position,middle,Mathf.Clamp01(pull*4)),transform.right,transform.forward,transform.up);
                poseString.SetPosition(0,poseBow.TransformPoint(BowPoint(-1,.57f,.16f)));
                poseString.SetPosition(1,middle);
                poseString.SetPosition(2,poseBow.TransformPoint(BowPoint(1,.57f,.16f)));
            }
            if(poseStick==null || deadE>.01f || (phoneDriven && !Attacking && Charge<=0 && !Guarding))return;
            if(castT>=0 || (Attacking && atkKind=="novice_throw"))return;
            float k=Mathf.Max(combatAmount,Mathf.Max(chargeAmt,guardAmt));
            var target=poseStick.TransformPoint(new Vector3(0,0,.14f));
            SolveArm(shL,elL,handL,Vector3.Lerp(handL.position,target,k),-transform.right,transform.forward,transform.up);
            if(k>.9f && outfitHandL!=null)outfitHandL.sharedMesh=handShapesL[1];
        }

        float ChargePoseWeight(float progress) => chargeAmt * Mathf.Lerp(.12f,1, Smooth(progress));

        void PlayerChargeBody(float weight, float deadE)
        {
            float k=weight*(1-deadE)*(1-guardAmt);
            if(phoneDriven)return; // Physical crouching/jumping owns torso height and footing.
            spine.localRotation*=Quaternion.Euler(poseChargeLean*k,poseChargeTwist*k,0);
        }

        void PhoneLegPose(float dt, float drop, float lift, float alive)
        {
            // Short compression only after a real downward landing, decaying in ~0.2 seconds.
            if(poseLastLift>.015f && lift<.015f)poseLanding=Mathf.Max(poseLanding,Mathf.Min(.055f,poseLastLift*.7f));
            poseLastLift=lift;
            poseLanding=Mathf.MoveTowards(poseLanding,0,Mathf.Max(0,dt)*.26f);
            float compression=Mathf.Min(.72f,drop+poseLanding*alive);
            float angle=SquatAngle(compression),back=angle*.9f;
            float tuck=Smooth(Mathf.Clamp01(lift/.24f))*alive;
            float halfWidth=.085f*(classId=="assassin"?.85f:1);
            float spread=poseLongCoat?0:2.5f*crouchAmt*alive;
            hipL.localPosition=new Vector3(-halfWidth,.85f,0);
            hipR.localPosition=new Vector3(halfWidth,.85f,0);
            float legs=Mathf.Clamp01(compression/.07f+tuck);
            hipL.localRotation=Quaternion.Slerp(hipL.localRotation,Quaternion.Euler(-angle-poseJumpTuck*1.35f*tuck,0,-spread),legs);
            hipR.localRotation=Quaternion.Slerp(hipR.localRotation,Quaternion.Euler(-angle-(poseJumpTuck-poseJumpAsym)*1.35f*tuck,0,spread),legs);
            knL.localRotation=Quaternion.Slerp(knL.localRotation,Quaternion.Euler(angle+back+poseJumpTuck*2.3f*tuck,0,0),legs);
            knR.localRotation=Quaternion.Slerp(knR.localRotation,Quaternion.Euler(angle+back+(poseJumpTuck-poseJumpAsym)*2.3f*tuck,0,0),legs);
            float footZ=ThighLen*Mathf.Sin(angle*Mathf.Deg2Rad)-ShinLen*Mathf.Sin(back*Mathf.Deg2Rad);
            var bp=body.localPosition;
            body.localPosition=new Vector3(bp.x,-compression+lift,bp.z-footZ);
            if(lift<.005f && alive>.99f){PlantSole(hipL,knL,leftSole);PlantSole(hipR,knR,rightSole);}
            // Arm lift is additive; attacks/guard remain readable and weapons remain hand children.
            float arm=tuck*(1-guardAmt)*(Attacking?.25f:1);
            shR.localRotation*=Quaternion.Euler(-15*arm,0,8*arm);
            shL.localRotation*=Quaternion.Euler(-12*arm,0,-8*arm);
        }
    }
}
