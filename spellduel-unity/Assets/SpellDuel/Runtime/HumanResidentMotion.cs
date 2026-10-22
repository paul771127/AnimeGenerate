using UnityEngine;
namespace SpellDuel
{
    internal sealed class HumanResidentMotion : MonoBehaviour
    {
        string identity;Transform chest,neck,face,figure;HumanPersonality.Pose pose;
        Transform left,right,leftElbow,rightElbow,hipL,hipR,kneeL,kneeR;
        Vector3 chestRest,neckRest;Quaternion chestRotation,neckRotation;float clock;
        bool coat;Vector3[] soleL,soleR;Transform caneGrip;Vector3 caneScale;
        // 村子裡的行為（TownWalk 設定）：走路速度（m/s，0＝站著）、揮手打招呼
        internal float walkSpeed;float walkAmt,walkPhase,waveT=-1;
        internal void Wave(){waveT=0;}
        internal void Configure(string id,Transform rib,Transform n,Transform head,Transform root,Color skin)
        {
            identity=id;chest=rib;neck=n;face=head;figure=root;pose=HumanPersonality.Stance(id,false);
            coat=OutfitArt.Get(id).Hem>.15f;
            if(coat){pose.Twist*=.30f;pose.Roll*=.4f;pose.Shift*=.2f;pose.Step*=.2f;pose.Lean*=.5f;}
            if(id=="海斗"){pose.Twist*=.30f;pose.Roll*=.40f;pose.Shift*=.20f;}
            chestRest=chest.localPosition;if(id=="老焰")pose.Lean=8;
            neckRest=neck.localPosition;chestRotation=chest.localRotation;neckRotation=neck.localRotation;
            foreach(var tf in root.GetComponentsInChildren<Transform>())
            {
                if(tf.gameObject.name=="Outfit shoulder"){if(tf.localPosition.x<0)left=tf;else right=tf;}
                if(tf.gameObject.name=="Outfit hip"){if(tf.localPosition.x<0)hipL=tf;else hipR=tf;}
            }
            leftElbow=left.Find("Outfit elbow");rightElbow=right.Find("Outfit elbow");kneeL=hipL.Find("Outfit knee");kneeR=hipR.Find("Outfit knee");
            soleL=ResidentBody.MeshFor(id,"calfL",skin).vertices;soleR=ResidentBody.MeshFor(id,"calfR",skin).vertices;
            caneGrip=AttachTool(root,"Walking staff",rightElbow.Find("Outfit hand"),new Vector3(.33f,.80f,.11f));caneScale=caneGrip==null?Vector3.one:caneGrip.localScale;
            AttachTool(root,"Fishing rod",leftElbow.Find("Outfit hand"),new Vector3(-.34f,.82f,.04f));AttachTool(root,"Fishing line",leftElbow.Find("Outfit hand"),new Vector3(-.34f,.82f,.04f));
            AttachTool(root,"Smith hammer grip",rightElbow.Find("Outfit hand"),new Vector3(.31f,.80f,.08f));AttachTool(root,"Smith hammer head",rightElbow.Find("Outfit hand"),new Vector3(.31f,.80f,.08f));
            AttachTool(root,"Story tablet",rightElbow.Find("Outfit hand"),new Vector3(.22f,.82f,.17f));AttachTool(root,"Tablet name",rightElbow.Find("Outfit hand"),new Vector3(.22f,.82f,.17f));
            AttachTool(root,"Leaf lamp frame",leftElbow.Find("Outfit hand"),new Vector3(-.30f,.85f,.13f));AttachTool(root,"Leaf lamp paper",leftElbow.Find("Outfit hand"),new Vector3(-.30f,.85f,.13f));
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
            float dt=Mathf.Clamp(t-clock,0,.1f);
            clock=t;float wave=Mathf.Sin(t*1.7f+CharacterLook.Get(identity).Seed),period=6+CharacterLook.Get(identity).Seed%7,phase=t%period;
            float gesture=phase<1.5f?Mathf.Sin(phase/1.5f*Mathf.PI):0;
            chest.localPosition=chestRest+new Vector3(pose.Shift+wave*.002f,wave*.0025f,0);
            chest.localRotation=chestRotation*Quaternion.Euler(pose.Lean,pose.Twist,-pose.Roll*.6f);
            neck.localRotation=neckRotation*Quaternion.Euler(-pose.Lean*.4f,-pose.Twist*.4f+gesture*3,pose.Roll*.35f);
            left.localRotation=Quaternion.Euler(-3-wave*.4f,0,-6);right.localRotation=Quaternion.Euler(-4+wave*.4f,0,6);
            leftElbow.localRotation=Quaternion.Euler(-10+gesture*2,0,0);rightElbow.localRotation=Quaternion.Euler(-10-gesture*2,0,0);
            if(identity=="翠"){left.localRotation=Quaternion.Euler(-3,0,-8);right.localRotation=Quaternion.Euler(-4,0,8);leftElbow.localRotation=Quaternion.Euler(-10,0,-12);rightElbow.localRotation=Quaternion.Euler(-10,0,12);}
            if(identity=="小葉"){left.localRotation=Quaternion.Euler(-12,0,-2);right.localRotation=Quaternion.Euler(-12,0,2);leftElbow.localRotation=Quaternion.Euler(-58,0,0);rightElbow.localRotation=Quaternion.Euler(-58,0,0);}
            // 走路：腿前後擺、膝蓋彎、手臂反向擺、身體上下起伏（和 CharacterRig 一樣的方向）
            walkAmt=Mathf.MoveTowards(walkAmt,walkSpeed>.05f?1:0,dt*4);
            walkPhase+=dt*Mathf.Max(walkSpeed,.3f)*6.5f*walkAmt;
            float sinP=Mathf.Sin(walkPhase),cosP=Mathf.Cos(walkPhase),amp=(coat?14:22)*walkAmt,still=1-walkAmt;
            hipL.localRotation=Quaternion.Euler(-2+sinP*amp,-6*still,0);hipR.localRotation=Quaternion.Euler(-3-sinP*amp,7*still,0);
            kneeL.localRotation=Quaternion.Euler(4+Mathf.Max(0,cosP)*amp*1.2f,0,0);kneeR.localRotation=Quaternion.Euler(6+Mathf.Max(0,-cosP)*amp*1.2f,0,0);
            if(walkAmt>0&&identity!="小葉"&&identity!="翠")
            {
                float swing=sinP*amp*.7f;
                left.localRotation=Quaternion.Euler(-3-swing,0,-6);right.localRotation=Quaternion.Euler(-4+swing,0,6);
            }
            chest.localPosition+=Vector3.up*Mathf.Abs(cosP)*.018f*walkAmt;
            hipL.localPosition=new Vector3(coat?-.105f:-.12f,StoryFigure.HipY(identity),pose.Step*still);hipR.localPosition=new Vector3(coat?.105f:.12f,StoryFigure.HipY(identity),-pose.Step*still);
            Plant(hipL,kneeL,soleL,-.12f);Plant(hipR,kneeR,soleR,.12f);
            if(walkAmt>0){hipL.localPosition=Vector3.Lerp(hipL.localPosition,new Vector3(hipL.localPosition.x,Mathf.Max(hipL.localPosition.y,hipR.localPosition.y),hipL.localPosition.z),walkAmt);hipR.localPosition=new Vector3(hipR.localPosition.x,hipL.localPosition.y,hipR.localPosition.z);}
            // 揮手：右前臂舉起、左右搖（1.8 秒）
            if(waveT>=0)
            {
                waveT+=dt;
                if(waveT>1.8f)waveT=-1;
                else
                {
                    float k=Mathf.Sin(Mathf.Clamp01(waveT/1.8f)*Mathf.PI);k=Mathf.Clamp01(k*1.6f);
                    right.localRotation=Quaternion.Euler(-4-25*k,0,6+40*k);
                    rightElbow.localRotation=Quaternion.Euler(-10-105*k,0,Mathf.Sin(waveT*11)*28*k);
                }
            }
            if(caneGrip!=null)
            {
                caneGrip.rotation=figure.rotation;float gripY=figure.worldToLocalMatrix.MultiplyPoint3x4(caneGrip.position).y;
                caneGrip.localScale=new Vector3(caneScale.x,caneScale.y*gripY/.76f,caneScale.z);
            }
        }
        void Plant(Transform hip,Transform knee,Vector3[] verts,float footX)
        {
            var m=figure.worldToLocalMatrix*knee.localToWorldMatrix;float low=10;
            for(int i=0;i<verts.Length;i++)low=Mathf.Min(low,m.MultiplyPoint3x4(verts[i]).y);hip.localPosition+=new Vector3(footX-m.MultiplyPoint3x4(new Vector3(0,-.43f,.038f)).x,-low,0);
        }
        void Update(){Tick(clock+Time.deltaTime);}
    }
}
