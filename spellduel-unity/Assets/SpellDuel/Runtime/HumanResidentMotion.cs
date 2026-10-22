using UnityEngine;
namespace SpellDuel
{
    // Presentation only: TownWalk still owns wandering, speed, facing and greetings.
    internal sealed partial class HumanResidentMotion : MonoBehaviour
    {
        string identity,bodyIdentity;Transform chest,neck,face,figure;
        Transform left,right,leftElbow,rightElbow,handL,handR,hipL,hipR,kneeL,kneeR,hemL,hemR,sleeveL,sleeveR;
        Vector3 chestRest;Quaternion chestRotation,neckRotation,figureRotation;HumanPersonality.Pose pose;
        HumanExpressionMotion expression;float clock,waveT=-1,walkAmt,walkPhase;bool coat,child,elder;
        Transform caneGrip;Vector3 caneScale;
        // These two entry points are used by TownWalk; their meaning is unchanged.
        internal float walkSpeed;
        internal string Identity=>identity;internal string BodyIdentity=>bodyIdentity;
        internal void Wave(){waveT=0;}
        /// <summary>長袍／長裙（衣襬很長）：走路時腿會穿出衣服，先不讓他走動</summary>
        internal bool LongRobe=>coat;
        internal void Configure(string id,Transform rib,Transform n,Transform head,Transform root,Color skin)
        {
            identity=bodyIdentity=id;chest=rib;neck=n;face=head;figure=root;
            chestRest=chest.localPosition;chestRotation=chest.localRotation;neckRotation=neck.localRotation;figureRotation=figure.localRotation;
            pose=HumanPersonality.Stance(id,false);coat=OutfitArt.Get(id).Hem>.15f||id=="翠"||id=="凪"||id=="老焰"||id=="艾琳";
            foreach(var tf in root.GetComponentsInChildren<Transform>())
            {
                if(tf.gameObject.name=="Outfit shoulder"){if(tf.localPosition.x<0)left=tf;else right=tf;}
                if(tf.gameObject.name=="Outfit hip"){if(tf.localPosition.x<0)hipL=tf;else hipR=tf;}
                if(tf.gameObject.name=="Resident sleeve L")sleeveL=tf;if(tf.gameObject.name=="Resident sleeve R")sleeveR=tf;
                if(tf.gameObject.name=="Resident hem L")hemL=tf;if(tf.gameObject.name=="Resident hem R")hemR=tf;
            }
            leftElbow=left.Find("Outfit elbow");rightElbow=right.Find("Outfit elbow");handL=leftElbow.Find("Outfit hand");handR=rightElbow.Find("Outfit hand");
            kneeL=hipL.Find("Outfit knee");kneeR=hipR.Find("Outfit knee");
            legL=new Leg(hipL,kneeL);legR=new Leg(hipR,kneeR);expression=face.GetComponent<HumanExpressionMotion>();
            caneGrip=AttachTool(root,"Walking staff",handR,new Vector3(.33f,.80f,.11f));caneScale=caneGrip==null?Vector3.one:caneGrip.localScale;
            AttachTool(root,"Fishing rod",handL,new Vector3(-.34f,.82f,.04f));AttachTool(root,"Fishing line",handL,new Vector3(-.34f,.82f,.04f));
            AttachTool(root,"Smith hammer grip",handR,new Vector3(.31f,.80f,.08f));AttachTool(root,"Smith hammer head",handR,new Vector3(.31f,.80f,.08f));
            AttachTool(root,"Leaf lamp frame",handL,new Vector3(-.30f,.85f,.13f));AttachTool(root,"Leaf lamp paper",handL,new Vector3(-.30f,.85f,.13f));
            SetIdleSet(id);
        }
        internal void SetIdleSet(string id)
        {
            identity=id;child=id=="小葉";elder=id=="老焰"||id=="翠"||id=="阿潮"||id=="公會長"||id=="老駝"||id=="石先生";
            int seed=0;foreach(char c in id)seed=(seed*31+c)&32767;idlePeriod=8+seed%4;idleOffset=(seed%29)*.13f;
            feetReady=false;
        }
        static Transform AttachTool(Transform root,string name,Transform hand,Vector3 grip)
        {
            foreach(var tf in root.GetComponentsInChildren<Transform>())if(tf.gameObject.name==name)
            {
                var pivot=new GameObject("ResidentToolGrip "+name).transform;pivot.SetParent(root,false);pivot.localPosition=grip;
                tf.SetParent(pivot,true);pivot.SetParent(hand,true);pivot.localPosition=new Vector3(0,-.03f,0);return pivot;
            }
            return null;
        }
        internal void Tick(float t)
        {
            if(figure==null)return;float dt=Mathf.Clamp(t-clock,0,.1f);clock=t;
            float target=walkSpeed>.05f?1:0;if(walkAmt<.001f&&target>0)walkPhase=.28f;
            walkAmt=Mathf.MoveTowards(walkAmt,target,dt*4);
            float stride=(child?.38f:elder?.44f:coat?.46f:.62f)*figure.lossyScale.y;
            walkPhase+=(Mathf.Max(walkSpeed,.20f*walkAmt)/Mathf.Max(.1f,stride))*dt;
            float breath=Mathf.Sin(t*1.7f+idleOffset),walking=Mathf.Sin(walkPhase*Mathf.PI*2);
            SampleIdle(t,1-walkAmt);figure.localRotation=Quaternion.Slerp(figure.localRotation,figureRotation*Quaternion.Euler(0,idleSpin,0),dt<=0?1:1-Mathf.Exp(-dt*14));
            float lean=elder?5:pose.Lean*.25f;
            chest.localPosition=chestRest+new Vector3(pose.Shift*.25f,breath*.0015f+idleHop-walkAmt*.022f+idleDrop+(child?Mathf.Abs(walking)*.014f*walkAmt:0),0);
            chest.localRotation=chestRotation*Quaternion.Euler(lean+idleLean,pose.Twist*.20f+idleTwist,-pose.Roll*.18f+idleRoll);
            neck.localRotation=neckRotation*Quaternion.Euler(-lean*.35f+idleNod,idleLook,-idleRoll*.25f);
            left.localRotation=Quaternion.Euler(-4-walking*10*walkAmt,0,-8);right.localRotation=Quaternion.Euler(-4+walking*10*walkAmt,0,8);
            leftElbow.localRotation=Quaternion.Euler(-12-Mathf.Abs(walking)*8*walkAmt,0,0);rightElbow.localRotation=Quaternion.Euler(-12-Mathf.Abs(walking)*8*walkAmt,0,0);
            handL.localRotation=handR.localRotation=Quaternion.identity;
            if(idleWeight>0){if(idleUseL)Arm(true,idleL,idlePalmL,idleWeight);if(idleUseR)Arm(false,idleR,idlePalmR,idleWeight);}
            // Keep the child's tablet and the elder's cane supported even between gestures.
            if(child&&idleWeight<.01f){Arm(true,new Vector3(-.16f,1.08f,.28f),Quaternion.identity,1);Arm(false,new Vector3(.15f,1.08f,.28f),Quaternion.identity,1);}
            if(caneGrip!=null&&walkAmt<.1f&&idleWeight<.01f)Arm(false,new Vector3(.26f,.95f,.18f),Quaternion.identity,1);
            if(waveT>=0)
            {
                waveT+=dt;if(waveT>=1.8f)waveT=-1;
                else
                {
                    float k=Ease(Mathf.Clamp01(waveT/.32f))*Ease(Mathf.Clamp01((1.8f-waveT)/.38f));bool useLeft=caneGrip!=null||identity=="教官"||identity=="播報員"||identity=="鐵娘"||identity=="鐵匠"||identity=="小鐵";
                    float side=useLeft?-1:1;Arm(useLeft,new Vector3(side*.29f,elder?1.40f:1.46f,.23f),Quaternion.Euler(0,0,(identity=="播報員"?0:180)+side*(18+Mathf.Sin(waveT*10)*17)),k);
                }
            }
            if(sleeveL!=null)sleeveL.rotation=figure.rotation*Quaternion.Euler(0,0,-3+Mathf.Sin(t)*1.5f);
            if(sleeveR!=null)sleeveR.rotation=figure.rotation*Quaternion.Euler(0,0,3-Mathf.Sin(t)*1.5f);
            UpdateFeet(dt,stride);float cloth=walking*walkAmt;
            if(hemL!=null)hemL.localRotation=Quaternion.Euler(cloth*(coat?4:6),0,Mathf.Sin(t*1.4f)*.6f+idleRoll*.10f);
            if(hemR!=null)hemR.localRotation=Quaternion.Euler(-cloth*(coat?4:6),0,-Mathf.Sin(t*1.4f)*.6f+idleRoll*.10f);
            if(expression!=null){expression.ResidentEyeClose=idleEyes;expression.ResidentMouthOpen=idleMouth;}
            TickActionArt();
            if(caneGrip!=null)
            {
                caneGrip.rotation=figure.rotation;float gripY=figure.InverseTransformPoint(caneGrip.position).y;
                caneGrip.localScale=new Vector3(caneScale.x,caneScale.y*Mathf.Max(.2f,(gripY-.028f)/.76f),caneScale.z);
            }
        }
        static float Ease(float v)=>v*v*(3-2*v);
        void Arm(bool isLeft,Vector3 localGoal,Quaternion palm,float amount)
        {
            Transform shoulder=isLeft?left:right,elbow=isLeft?leftElbow:rightElbow,hand=isLeft?handL:handR;
            var a=shoulder.rotation;var b=elbow.localRotation;var c=hand.localRotation;Vector3 start=shoulder.position,goal=figure.TransformPoint(localGoal);
            float upper=elbow.localPosition.magnitude*shoulder.lossyScale.y,lower=hand.localPosition.magnitude*elbow.lossyScale.y;
            Vector3 delta=goal-start;float length=Mathf.Clamp(delta.magnitude,.06f,upper+lower-.003f);Vector3 axis=delta.normalized;
            Vector3 bend=figure.rotation*new Vector3(isLeft?-1:1,-.1f,-.20f);bend=(bend-axis*Vector3.Dot(bend,axis)).normalized;
            float along=(upper*upper-lower*lower+length*length)/(2*length),height=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));Vector3 middle=start+axis*along+bend*height;
            shoulder.rotation=Quaternion.FromToRotation(Vector3.down,middle-start);elbow.rotation=Quaternion.FromToRotation(Vector3.down,start+axis*length-middle);hand.rotation=figure.rotation*palm;
            Quaternion solvedA=shoulder.rotation,solvedB=elbow.localRotation,solvedC=hand.localRotation;
            shoulder.rotation=Quaternion.Slerp(a,solvedA,amount);elbow.localRotation=Quaternion.Slerp(b,solvedB,amount);hand.localRotation=Quaternion.Slerp(c,solvedC,amount);
        }
        void OnEnable(){feetReady=false;}
        void Update(){Tick(clock+Time.deltaTime);}
    }
}
