using UnityEngine;
namespace SpellDuel
{
    public sealed class MechanicalArt:MonoBehaviour
    {
        public int Kind {get;private set;}public bool ExternalTick,Dead,Hit;public float MoveSpeed,Wind;public bool Striking;
        public int Phase {get;private set;}=1;
        Transform torso,head,left,right,legL,legR,gear;readonly Transform[] tools=new Transform[3];float clock,death;
        static readonly Color Bronze=new Color(.64f,.43f,.23f),Stone=new Color(.46f,.53f,.59f),Cyan=new Color(.3f,.95f,1),Gold=new Color(1,.78f,.3f);
        Renderer[] renderers;MaterialPropertyBlock tint;
        public static MechanicalArt Create(int kind)
        {var a=new GameObject(kind==53?"千機匠魂":"Gallery mechanical enemy "+kind).AddComponent<MechanicalArt>();a.Kind=kind;a.Build();return a;}
        Transform P(Transform root,string name,Vector3 pos,Vector3 size,Color c,bool point=false,Vector3 rotation=default(Vector3))=>EnemyArt.Part(root,name,pos,size,c,point,rotation);
        void Build()
        {
            torso=EnemyArt.Pivot(transform,"Bronze mechanism torso",new Vector3(0,.8f,0));
            if(Kind==52)
            {
                P(torso,"Raven chassis",Vector3.zero,new Vector3(.65f,.55f,.85f),Bronze);head=EnemyArt.Pivot(torso,"Raven head",new Vector3(0,.25f,.48f));
                P(head,"Raven skull",Vector3.zero,new Vector3(.32f,.32f,.39f),Stone);P(head,"Beak",new Vector3(0,-.03f,.27f),new Vector3(.18f,.18f,.35f),Gold,true,new Vector3(90,0,0));
                left=EnemyArt.Pivot(torso,"Left bronze wing",new Vector3(-.25f,.1f,0));right=EnemyArt.Pivot(torso,"Right bronze wing",new Vector3(.25f,.1f,0));
                for(int side=0;side<2;side++)for(int i=0;i<5;i++)P(side==0?left:right,"Tapered metal flight feather "+i,new Vector3((side==0?-1:1)*(.2f+i*.15f),0,-i*.08f),new Vector3(.25f,.14f,.85f-i*.07f),i%2==0?Bronze:Stone,true,new Vector3(90,0,side==0?-20:20));
            }
            else
            {
                P(torso,"Stone plated chest",new Vector3(0,.2f,0),new Vector3(.8f,.75f,.48f),Stone);
                P(torso,"Clock heart",new Vector3(0,.24f,.25f),new Vector3(.28f,.28f,.12f),Cyan);
                head=EnemyArt.Pivot(torso,"Rune eye helmet",new Vector3(0,.75f,.02f));P(head,"Bronze helm",Vector3.zero,new Vector3(.45f,.4f,.4f),Bronze);
                left=EnemyArt.Pivot(torso,"Left joint",new Vector3(-.43f,.44f,0));right=EnemyArt.Pivot(torso,"Right joint",new Vector3(.43f,.44f,0));
                foreach(var arm in new[]{left,right}){P(arm,"Round bearing",Vector3.zero,new Vector3(.3f,.3f,.35f),Gold);P(arm,"Articulated piston",new Vector3(0,-.3f,0),new Vector3(.2f,.55f,.25f),Bronze);P(arm,"Mechanical hand",new Vector3(0,-.6f,.03f),new Vector3(.27f,.21f,.28f),Stone);}
                legL=EnemyArt.Pivot(torso,"Left leg",new Vector3(-.22f,-.13f,0));legR=EnemyArt.Pivot(torso,"Right leg",new Vector3(.22f,-.13f,0));
                foreach(var leg in new[]{legL,legR}){P(leg,"Greave",new Vector3(0,-.28f,0),new Vector3(.28f,.56f,.3f),Bronze);P(leg,"Broad foot",new Vector3(0,-.56f,.1f),new Vector3(.34f,.22f,.43f),Stone);}
                tools[0]=EnemyArt.Pivot(right,"Hammer arm",new Vector3(0,-.47f,.15f));P(tools[0],"Hammer shaft",new Vector3(0,.15f,0),new Vector3(.1f,.8f,.1f),Gold);P(tools[0],"Heavy hammer",new Vector3(0,.55f,0),new Vector3(.65f,.35f,.4f),Bronze);
                tools[1]=EnemyArt.Pivot(right,"Crossbow arm",new Vector3(0,-.42f,.2f));P(tools[1],"Crossbow stock",new Vector3(0,0,.2f),new Vector3(.23f,.2f,.9f),Bronze);P(tools[1],"Crossbow bow",new Vector3(0,0,.38f),new Vector3(1,.12f,.15f),Gold);
                tools[2]=EnemyArt.Pivot(Kind==50?torso:right,"Blade arm",Kind==50?new Vector3(0,-.65f,0):new Vector3(0,-.48f,.25f));
                if(Kind==50)for(int i=0;i<4;i++)P(tools[2],"Low rotating blade "+i,new Vector3(Mathf.Sin(i*Mathf.PI/2)*.6f,0,Mathf.Cos(i*Mathf.PI/2)*.6f),new Vector3(.22f,.12f,1.15f),Stone,true,new Vector3(90,i*90,0));
                else P(tools[2],"Horizontal guillotine",new Vector3(0,.1f,.2f),new Vector3(1.8f,.25f,.18f),Stone,true,new Vector3(0,0,90));
            }
            for(int s=-1;s<=1;s+=2)P(head,"Glowing rune eye",new Vector3(s*.11f,.04f,.21f),new Vector3(.12f,.055f,.035f),Cyan);
            gear=EnemyArt.Pivot(torso,"Rotating toothed halo",new Vector3(0,.5f,-.33f));
            int teeth=Kind==53?16:10;float radius=Kind==53?.84f:.43f;
            for(int i=0;i<teeth;i++){float a=i*Mathf.PI*2/teeth;P(gear,"Bronze gear tooth "+i,new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0),new Vector3(.18f,.28f,.12f),i%2==0?Bronze:Gold,false,new Vector3(0,0,-a*Mathf.Rad2Deg));}
            var ring=new Vector3[65];for(int i=0;i<65;i++){float a=i*Mathf.PI/32;ring[i]=new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0);}
            SpellFx.Line(gear,"Connected rune gear halo",ring,false,.045f,Cyan,true,false);
            EnemyArt.Bake(transform,"mechanical/"+Kind);renderers=GetComponentsInChildren<Renderer>(true);tint=new MaterialPropertyBlock();
            if(Kind==53)transform.localScale=Vector3.one*2;SetPhase(1);Tick(0);
        }
        public void SetPhase(int phase)
        {
            Phase=Mathf.Clamp(phase,1,3);int visible=Kind==48?1:Kind==49?0:2;
            if(Kind==53)visible=Phase-1;
            for(int i=0;i<tools.Length;i++)if(tools[i])tools[i].gameObject.SetActive(i==visible);
        }
        public void Tick(float dt)
        {
            clock+=dt;if(Dead)death+=dt;else death=0;
            gear.localRotation=Quaternion.Euler(0,0,clock*(Wind>0?70:16));
            float attack=Striking?Mathf.Sin(clock*15)*.5f+.5f:0;
            torso.localRotation=Quaternion.Euler(Dead?Mathf.Clamp01(death/.9f)*80:Kind==52?Wind*-35+attack*55:Wind*8+attack*12,Kind==50?Wind*-80+attack*140:0,0);
            left.localRotation=Quaternion.Euler(Kind==52?0:-Wind*130+attack*85,0,Kind==52?Mathf.Sin(clock*5)*(20+Wind*25):Wind*15);
            right.localRotation=Quaternion.Euler(Kind==52?0:-Wind*130+attack*85,0,Kind==52?-Mathf.Sin(clock*5)*(20+Wind*25):-Wind*15);
            if(legL){legL.localRotation=Quaternion.Euler(Mathf.Sin(clock*5)*MoveSpeed*25,0,0);legR.localRotation=Quaternion.Euler(-Mathf.Sin(clock*5)*MoveSpeed*25,0,0);}
            var color=Hit?new Color(2,2,2):Dead?Color.Lerp(Color.white,new Color(.25f,.3f,.35f),Mathf.Clamp01(death)):Color.white;
            tint.SetColor("_Color",color);foreach(var rr in renderers)if(rr)rr.SetPropertyBlock(tint);
        }
        void Update(){if(!ExternalTick)Tick(Time.deltaTime);}
    }
    public partial class MinionRig
    {
        MechanicalArt mechanism;
        void BuildTrapEnemy(int k)
        {variantId=kind=k;sea=plant=false;head=null;embers=null;phase=PhaseNone;dead=false;atkT=hitT=deadT=0;mechanism=MechanicalArt.Create(k);mechanism.ExternalTick=true;mechanism.transform.SetParent(transform,false);body=spine=mechanism.transform;}
        void AnimateTrapEnemy(float dt)
        {atkT+=dt;hitT=Mathf.Max(0,hitT-dt);mechanism.Dead=dead;mechanism.Hit=hitT>0;mechanism.MoveSpeed=MoveSpeed;mechanism.Wind=phase==PhaseWindup?Mathf.Clamp01(atkT/Mathf.Max(.1f,atkDur)):0;mechanism.Striking=phase==PhaseStrike&&atkT<.5f;if(phase==PhaseStrike&&atkT>=.5f)phase=PhaseNone;mechanism.Tick(dt);}
    }
    public static class GalleryResidentArt
    {
        public static void Decorate(CharacterRig rig,string id)
        {
            if(id!="lingyin"&&id!="ark"&&id!="wheat"&&id!="oldsmith"&&id!="merchant")return;
            if(id=="merchant")
            {
                foreach(var rr in rig.GetComponentsInChildren<Renderer>())rr.enabled=false;
                var robot=MechanicalArt.Create(48);robot.transform.SetParent(rig.transform,false);robot.transform.localScale=Vector3.one*.85f;
                var apron=EnemyArt.Pivot(robot.transform,"Merchant apron and change purse",Vector3.zero);
                EnemyArt.Part(apron,"Merchant waist pouch",new Vector3(.35f,.65f,.18f),new Vector3(.3f,.3f,.2f),new Color(.72f,.35f,.13f));EnemyArt.Bake(apron,"vault merchant pouch");return;
            }
            var root=EnemyArt.Pivot(rig.transform,"Gallery resident identity / "+id,Vector3.zero);
            var bronze=new Color(.78f,.5f,.22f);var cyan=new Color(.3f,.95f,1);
            if(id=="lingyin")
            {EnemyArt.Part(root,"Clock listener headset",new Vector3(.13f,1.55f,0),new Vector3(.07f,.13f,.1f),bronze);EnemyArt.Part(root,"Resonance crystal pendant",new Vector3(0,1.12f,.19f),new Vector3(.08f,.15f,.06f),cyan,true);EnemyArt.Part(root,"Fragment satchel",new Vector3(-.25f,.7f,0),new Vector3(.18f,.24f,.15f),bronze);}
            else if(id=="ark")
            {EnemyArt.Part(root,"Scar under left eye",new Vector3(-.065f,1.47f,.105f),new Vector3(.009f,.045f,.012f),new Color(.6f,.22f,.15f),false,new Vector3(0,0,25));EnemyArt.Part(root,"Battered bronze shoulder guard",new Vector3(-.26f,1.2f,0),new Vector3(.23f,.16f,.22f),bronze);EnemyArt.Part(root,"Keyring at belt",new Vector3(.22f,.74f,.13f),new Vector3(.06f,.14f,.06f),cyan);}
            else if(id=="wheat")
            {EnemyArt.Part(root,"Apprentice goggles",new Vector3(0,1.67f,.08f),new Vector3(.22f,.06f,.05f),bronze);EnemyArt.Part(root,"Tool pouch",new Vector3(.21f,.72f,.07f),new Vector3(.17f,.2f,.13f),new Color(.9f,.57f,.2f));EnemyArt.Part(root,"Small repair wrench",new Vector3(.26f,.77f,.13f),new Vector3(.035f,.23f,.035f),cyan);}
            else
            {EnemyArt.Part(root,"Smith work apron",new Vector3(0,.94f,.18f),new Vector3(.3f,.46f,.035f),new Color(.25f,.65f,.85f));EnemyArt.Part(root,"Smith moustache",new Vector3(0,1.435f,.12f),new Vector3(.12f,.035f,.045f),new Color(.5f,.85f,1));EnemyArt.Part(root,"Ghost hammer",new Vector3(.28f,.8f,.1f),new Vector3(.15f,.27f,.15f),bronze);}
            EnemyArt.Bake(root,"gallery resident / "+id);
        }
    }

}
