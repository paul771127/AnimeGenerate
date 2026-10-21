using UnityEngine;
namespace SpellDuel
{
    internal sealed class HumanResidentMotion : MonoBehaviour
    {
        string identity;Transform chest,neck,face,figure;HumanPersonality.Pose pose;
        Transform left,right,leftElbow,rightElbow,hipL,hipR,kneeL,kneeR;
        Vector3 chestRest,neckRest;Quaternion chestRotation,neckRotation;float clock;
        bool coat;Vector3[] soleL,soleR;Transform caneGrip;Vector3 caneScale;
        internal void Configure(string id,Transform rib,Transform n,Transform head,Transform root,Color skin)
        {
            identity=id;chest=rib;neck=n;face=head;figure=root;pose=HumanPersonality.Stance(id,false);
            coat=OutfitArt.Get(id).Hem>.15f;
            if(coat){pose.Twist*=.30f;pose.Roll*=.4f;pose.Shift*=.2f;pose.Step*=.2f;pose.Lean*=.5f;}
            chestRest=chest.localPosition;neckRest=neck.localPosition;chestRotation=chest.localRotation;neckRotation=neck.localRotation;
            foreach(var tf in root.GetComponentsInChildren<Transform>())
            {
                if(tf.gameObject.name=="Outfit shoulder"){if(tf.localPosition.x<0)left=tf;else right=tf;}
                if(tf.gameObject.name=="Outfit hip"){if(tf.localPosition.x<0)hipL=tf;else hipR=tf;}
            }
            leftElbow=left.Find("Outfit elbow");rightElbow=right.Find("Outfit elbow");kneeL=hipL.Find("Outfit knee");kneeR=hipR.Find("Outfit knee");
            soleL=ResidentBody.MeshFor(id,"calfL",skin).vertices;soleR=ResidentBody.MeshFor(id,"calfR",skin).vertices;
            caneGrip=AttachTool(root,"Walking staff",leftElbow.Find("Outfit hand"),new Vector3(-.33f,.80f,.11f));caneScale=caneGrip==null?Vector3.one:caneGrip.localScale;
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
            clock=t;float wave=Mathf.Sin(t*1.7f+CharacterLook.Get(identity).Seed),period=6+CharacterLook.Get(identity).Seed%7,phase=t%period;
            float gesture=phase<1.5f?Mathf.Sin(phase/1.5f*Mathf.PI):0;
            chest.localPosition=chestRest+new Vector3(pose.Shift+wave*.002f,wave*.0025f,0);
            chest.localRotation=chestRotation*Quaternion.Euler(pose.Lean,pose.Twist,-pose.Roll*.6f);
            neck.localRotation=neckRotation*Quaternion.Euler(-pose.Lean*.4f,-pose.Twist*.4f+gesture*3,pose.Roll*.35f);
            left.localRotation=Quaternion.Euler(-4-wave,0,-2);right.localRotation=Quaternion.Euler(-6+wave,0,2);
            leftElbow.localRotation=Quaternion.Euler(8+gesture*2,0,0);rightElbow.localRotation=Quaternion.Euler(12-gesture*2,0,0);
            hipL.localRotation=Quaternion.Euler(-2,-6,0);hipR.localRotation=Quaternion.Euler(-3,7,0);kneeL.localRotation=Quaternion.Euler(4,0,0);kneeR.localRotation=Quaternion.Euler(6,0,0);
            hipL.localPosition=new Vector3(coat?-.105f:-.12f,.85f,pose.Step);hipR.localPosition=new Vector3(coat?.105f:.12f,.85f,-pose.Step);
            Plant(hipL,kneeL,soleL,-.12f);Plant(hipR,kneeR,soleR,.12f);
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
