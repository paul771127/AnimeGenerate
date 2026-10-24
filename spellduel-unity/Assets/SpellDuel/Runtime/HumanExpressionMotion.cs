using UnityEngine;
namespace SpellDuel
{
    // Cached geometry/material switches only. CharacterRig drives fighters; residents tick themselves.
    internal sealed class HumanExpressionMotion : MonoBehaviour
    {
        internal bool External,Visible=true;
        internal float ResidentEyeClose,ResidentMouthOpen;
        string identity;Portraits.Persona persona;AnimeFace.Features features;Portraits.FacePlan plan;
        readonly Mesh[] lips=new Mesh[5];readonly Material[] gaze=new Material[3];
        Renderer[] eyes,lids;Renderer mouthRenderer,teethRenderer,lipRenderer;Vector3[] browOrigins;Quaternion headRest;
        uint rng;float nextBlink,blinkAt=-100,nextGlance,glanceAt=-100,nextMicro,microAt=-100,hitUntil=-100,lastTime;
        int glanceSide=1;bool hitWasActive;float lastHurt;internal int CurrentState;internal float Micro,Glance;
        float Rand(){rng=1664525u*rng+1013904223u;return (rng&65535)/65535f;}
        internal void Initialize(string id,Portraits.Persona p,AnimeFace.Features f)
        {
            mouthRenderer=f.Mouth.GetComponent<Renderer>();teethRenderer=f.Mouth.Find("UpperTeeth").GetComponent<Renderer>();lipRenderer=f.Lip.GetComponent<Renderer>();
            identity=id;persona=p;features=f;plan=new Portraits.FacePlan(p);headRest=transform.localRotation;rng=(uint)(CharacterLook.Get(id).Seed*7919+17);
            nextBlink=2+4*Rand();nextGlance=8+7*Rand();nextMicro=6+6*Rand();
            eyes=new Renderer[f.Eyes.Count];lids=new Renderer[f.Lids.Count*2];browOrigins=new Vector3[f.Brows.Count];
            for(int i=0;i<eyes.Length;i++)eyes[i]=f.Eyes[i].GetComponentInChildren<MeshRenderer>();
            for(int i=0;i<f.Lids.Count;i++){var list=f.Lids[i].GetComponentsInChildren<Renderer>();for(int k=0;k<list.Length;k++)lids[i*2+k]=list[k];}
            for(int i=0;i<browOrigins.Length;i++)browOrigins[i]=f.Brows[i].localPosition;
            for(int i=0;i<5;i++)lips[i]=AnimeFace.MouthShape(id,p,i);
            for(int i=0;i<3;i++)gaze[i]=AnimeFace.EyeMaterial(id,i-1);
            Tick(0,0,0,false,0);
        }
        internal void Tick(float t,float cast,float hurt,bool down,float combat)
        {
            if(features==null)return;
            if(t>=nextBlink){blinkAt=t;nextBlink=t+2+4*Rand();}
            if(t>=nextGlance){glanceAt=t;nextGlance=t+8+7*Rand();glanceSide=Rand()<.5f?-1:1;}
            if(t>=nextMicro){microAt=t;nextMicro=t+6+6*Rand();}
            float blink=Mathf.Clamp01(1-Mathf.Abs(t-blinkAt-.095f)/.095f);
            Glance=t-glanceAt<1.6f?Mathf.Sin(Mathf.Clamp01((t-glanceAt)/1.6f)*Mathf.PI)*glanceSide:0;
            Micro=t-microAt<1.7f?Mathf.Sin(Mathf.Clamp01((t-microAt)/1.7f)*Mathf.PI):0;
            if(hurt>.001f&&(!hitWasActive||hurt>lastHurt+.01f))hitUntil=t+.3f;hitWasActive=hurt>.001f;lastHurt=hurt;
            float hit=Mathf.Max(hurt,t<hitUntil?1:0),close=down?1:Mathf.Max(blink,hit);if(!External)close=Mathf.Max(close,ResidentEyeClose);
            CurrentState=down?4:hit>0?4:cast>.08f?3:combat>.5f?2:Micro>.15f?1:0;
            if(features.Lip.sharedMesh!=lips[CurrentState])features.Lip.sharedMesh=lips[CurrentState];
            int gazeIndex=Glance>.3f?2:Glance<-.3f?0:1;
            float lid=persona.Lid*1.6f+(combat*.045f),squint=(1-lid)*(1-.995f*close);
            for(int i=0;i<eyes.Length;i++)
            {
                if(eyes[i].sharedMaterial!=gaze[gazeIndex])eyes[i].sharedMaterial=gaze[gazeIndex];eyes[i].enabled=Visible;
                var eye=features.Eyes[i];eye.localScale=new Vector3(1,squint,1);
                eye.localPosition=new Vector3(eye.localPosition.x,plan.EyeY-plan.EyeHeight*.485f*close,.118f);
            }
            for(int i=0;i<lids.Length;i++)if(lids[i]!=null)lids[i].enabled=Visible&&close>.65f;
            for(int i=0;i<features.Lids.Count;i++){var lidTf=features.Lids[i];lidTf.localPosition=new Vector3(lidTf.localPosition.x,plan.EyeY+.024f-plan.EyeHeight*.45f*close,.122f);}
            for(int i=0;i<browOrigins.Length;i++)
            {
                float side=i==0?-1:1,asym=HumanPersonality.For(identity)==HumanPersonality.Temper.Cunning&&i==1?3:0;
                features.Brows[i].localPosition=browOrigins[i]+new Vector3(0,Micro*.002f-combat*.0015f-hit*.002f,0);
                features.Brows[i].localRotation=Quaternion.Euler(0,0,side*(combat*7+hit*9-Micro*3+asym));
            }
            float open=down?0:Mathf.Max(persona.Open+(persona.Open>0?Micro*.06f*(1-combat):0),cast);
            if(!External)open=Mathf.Max(open,ResidentMouthOpen);
            if(CurrentState==2)open=HumanPersonality.For(identity)==HumanPersonality.Temper.Fierce?.30f:HumanPersonality.For(identity)==HumanPersonality.Temper.Shy?.18f:0;
            if(CurrentState==3)open=Mathf.Max(open,.65f);
            if(hit>0)open=.22f;
            float mouthHeight=Mathf.Max(.0005f,plan.MouthWidth*.60f*open);
            features.Mouth.localScale=new Vector3(plan.MouthWidth,mouthHeight,mouthHeight);
            mouthRenderer.enabled=teethRenderer.enabled=Visible&&!down&&open>.01f;
            lipRenderer.enabled=Visible&&open<.10f;
            transform.localRotation=headRest*Quaternion.Euler(0,Glance*4*(1-combat),Micro*.5f*(1-combat));lastTime=t;
        }
        void Update(){if(!External)Tick(lastTime+Time.deltaTime,0,0,false,0);}
    }
}
