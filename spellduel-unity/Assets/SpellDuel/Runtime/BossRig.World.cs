using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public partial class BossRig
    {
        // Preview-only factory. Existing Create(0..4), AI and encounter entry points are untouched.
        public static WorldBossArt CreateWorld(string id)=>WorldBossArt.Create(id);
    }
    public sealed class WorldBossArt:MonoBehaviour
    {
        public static readonly string[] Ids={"wb_furnace","wb_canopy","wb_leviathan"};
        public static readonly string[] Names={"晨鐘熔爐泰坦","記名林冠龍","白帆淵鯨"};
        public static readonly string[] Moves={"sweep","quake","lance","scatter","gather","transition"};
        public string Id {get;private set;}
        public float MoveSpeed {get;set;}
        public int Phase {get;private set;}=1;
        public int BrokenParts {get;private set;}
        public float Height {get;private set;}
        public Color Tint {get;private set;}
        Transform body,head,left,right,tail,heart;
        readonly Transform[] legs=new Transform[6];
        readonly List<Transform> phase2=new List<Transform>(),phase3=new List<Transform>(),covers=new List<Transform>(),breaks=new List<Transform>();
        Renderer[] renderers;MaterialPropertyBlock flash;
        string skill;float age,clock,windDuration,hit,death;bool winding,striking,dead;
        public static WorldBossArt Create(string id)
        {
            int index=System.Array.IndexOf(Ids,id);if(index<0)throw new System.ArgumentException("Unknown world boss",nameof(id));
            var art=new GameObject(Names[index]+" (art preview only)").AddComponent<WorldBossArt>();art.Id=id;art.Build(index);return art;
        }
        Transform P(Transform p,string name,Vector3 at,Vector3 size,Color c,bool point=false,Vector3 euler=default(Vector3))=>EnemyArt.Part(p,name,at,size,c,point,euler);
        void Build(int type)
        {
            Tint=type==0?new Color(1,.39f,.12f):type==1?new Color(.38f,.8f,.42f):new Color(.18f,.66f,.9f);
            var armor=Color.Lerp(Tint,new Color(.26f,.29f,.42f),.6f);var gold=new Color(1,.8f,.35f);
            body=EnemyArt.Pivot(transform,"Articulated world body",Vector3.zero);
            if(type==0)
            {
                Height=7.2f;body.localPosition=new Vector3(0,3.2f,0);
                P(body,"Bell furnace torso",Vector3.zero,new Vector3(2.9f,3.4f,1.85f),armor);
                heart=EnemyArt.Pivot(body,"Open bell furnace",new Vector3(0,-.2f,1));P(heart,"Golden furnace heart",Vector3.zero,new Vector3(1.1f,1.6f,.45f),gold);
                for(int s=-1;s<=1;s+=2)
                {
                    var leg=legs[s<0?0:1]=EnemyArt.Pivot(body,s<0?"Left pillar":"Right pillar",new Vector3(s*.85f,-1.3f,0));
                    P(leg,"Stone greave",new Vector3(0,-.8f,0),new Vector3(.85f,2,.95f),armor);P(leg,"Broad planted toe",new Vector3(0,-1.65f,.28f),new Vector3(1.1f,.5f,1.3f),Tint);
                }
                head=EnemyArt.Pivot(body,"Lowered bell head",new Vector3(0,2.25f,.2f));P(head,"Bell crown skull",Vector3.zero,new Vector3(1.65f,1.3f,1.35f),armor);
                for(int s=-1;s<=1;s+=2)P(head,"Furnace eye",new Vector3(s*.35f,0,.68f),new Vector3(.4f,.16f,.08f),gold);
                left=EnemyArt.Pivot(body,"Left low hammer arm",new Vector3(-1.45f,.8f,0));right=EnemyArt.Pivot(body,"Right low hammer arm",new Vector3(1.45f,.8f,0));
                foreach(var arm in new[]{left,right}){P(arm,"Long jointed arm",new Vector3(0,-1.2f,.15f),new Vector3(.9f,2.6f,1),armor);P(arm,"Attack hammer at visible height",new Vector3(0,-2,.4f),new Vector3(1.3f,.8f,1.45f),Tint);}
                for(int i=0;i<3;i++)AddPhase(body,phase3,"Opened furnace exhaust "+i,new Vector3((i-1)*.95f,2.6f,-.5f),new Vector3(.6f,2.4f,.6f),Tint,true);
            }
            else if(type==1)
            {
                Height=6.8f;body.localPosition=new Vector3(0,2.15f,0);
                P(body,"Ancient dragon trunk",new Vector3(0,.3f,-.1f),new Vector3(3.1f,2.8f,4.4f),armor);
                for(int i=0;i<6;i++){var leg=legs[i]=EnemyArt.Pivot(body,"Root leg "+i,new Vector3((i%2==0?-1:1)*1.3f,-.1f,(i/2-1)*1.25f));P(leg,"Curving root limb",new Vector3(0,-.75f,.1f),new Vector3(.6f,1.8f,.72f),armor);P(leg,"Root talon",new Vector3(0,-1.825f,.28f),new Vector3(.85f,.45f,1.1f),gold);}
                head=EnemyArt.Pivot(body,"Lowered readable dragon head",new Vector3(0,.3f,2.4f));P(head,"Long moss skull",Vector3.zero,new Vector3(1.5f,1.15f,1.9f),Tint);
                for(int s=-1;s<=1;s+=2){P(head,"Amber eye",new Vector3(s*.55f,.18f,.7f),new Vector3(.18f,.18f,.2f),gold);P(head,"Branch antler",new Vector3(s*.65f,.95f,-.3f),new Vector3(.34f,2,.34f),armor,true,new Vector3(0,0,-s*30));}
                heart=EnemyArt.Pivot(body,"Amber heart",new Vector3(0,.6f,1.95f));P(heart,"Named amber core",Vector3.zero,new Vector3(.7f,.9f,.32f),gold);
                left=EnemyArt.Pivot(body,"Left canopy",new Vector3(-1,1.2f,-.2f));right=EnemyArt.Pivot(body,"Right canopy",new Vector3(1,1.2f,-.2f));
                for(int i=0;i<5;i++){P(body,"Ancient crown trunk "+i,new Vector3((i-2)*.55f,2.45f,-.5f),new Vector3(.3f,3.2f,.4f),armor,true,new Vector3(0,0,-(i-2)*12));P(body,"Remembered leaf crown "+i,new Vector3((i-2)*.7f,3.5f,-.5f),new Vector3(1.4f,.9f,1.4f),Tint);}
                foreach(var wing in new[]{left,right})P(wing,"Leaf wing",new Vector3(wing==left?-1:1,.4f,0),new Vector3(2.6f,.35f,2.4f),Tint,true,new Vector3(0,0,wing==left?15:-15));
                AddPhase(body,phase3,"Blooming amber crown",new Vector3(0,3.6f,.2f),new Vector3(1.8f,1.9f,1.8f),gold,true);
                tail=EnemyArt.Pivot(body,"Root tail",new Vector3(0,-.2f,-2.4f));P(tail,"Tapered root tail",new Vector3(0,0,-1),new Vector3(.8f,.65f,2.6f),armor,true,new Vector3(-90,0,0));
            }
            else
            {
                Height=6.4f;body.localPosition=new Vector3(0,2.7f,0);
                P(body,"Deep whale body",Vector3.zero,new Vector3(3.5f,3.5f,5.8f),armor);
                P(body,"Ivory whale belly",new Vector3(0,-.8f,.75f),new Vector3(2.7f,1.65f,3.9f),new Color(.77f,.9f,.9f));
                head=EnemyArt.Pivot(body,"Whale brow",new Vector3(0,.1f,2.7f));P(head,"Rounded brow",Vector3.zero,new Vector3(2.2f,1.6f,1.1f),Tint);
                for(int s=-1;s<=1;s+=2)P(head,"Watchful sea eye",new Vector3(s*.8f,.05f,.5f),new Vector3(.27f,.24f,.14f),gold);
                left=EnemyArt.Pivot(body,"Left low sail fin",new Vector3(-1.3f,-.6f,.8f));right=EnemyArt.Pivot(body,"Right low sail fin",new Vector3(1.3f,-.6f,.8f));
                foreach(var fin in new[]{left,right})P(fin,"Sweeping broad fin",new Vector3(fin==left?-1.35f:1.35f,-.1f,-.25f),new Vector3(3.5f,.5f,1.8f),Tint,true,new Vector3(0,0,fin==left?90:-90));
                tail=EnemyArt.Pivot(body,"Whale tail joint",new Vector3(0,0,-3));P(tail,"Tail fan",new Vector3(0,0,-1),new Vector3(4.3f,.5f,2.5f),Tint,true,new Vector3(-90,0,0));
                heart=EnemyArt.Pivot(head,"Tide crystal",new Vector3(0,.45f,.65f));P(heart,"Golden tide core",Vector3.zero,new Vector3(.7f,1.1f,.3f),gold,true);
                for(int i=0;i<3;i++){P(body,"Broken lighthouse pillar "+i,new Vector3((i-1)*.65f,1.75f,-.4f),new Vector3(.55f,2.2f+i*.15f,.55f),new Color(.74f,.76f,.66f));AddPhase(body,phase3,"Charged storm sail "+i,new Vector3((i-1)*1.2f,2.25f,-.65f),new Vector3(.4f,2.8f,1.7f),new Color(.67f,.37f,1),true);}
            }
            for(int i=0;i<3;i++)
            {
                float surface=type==0?1.05f:type==1?2.15f:2.95f;
                var cover=EnemyArt.Pivot(i==0?head:body,"Breakable armor "+i,new Vector3((i-1)*.8f,i==0?.65f:.6f,i==0?0:surface));
                P(cover,"Intact weak point cover",Vector3.zero,new Vector3(.6f,.75f,.3f),armor);covers.Add(cover);
                var broken=EnemyArt.Pivot(cover.parent,"Broken core "+i,cover.localPosition);P(broken,"Exposed named core",Vector3.zero,new Vector3(.35f,.48f,.22f),gold);
                for(int j=-1;j<=1;j++)P(broken,"Fractured armor rim",new Vector3(j*.23f,-.2f,0),new Vector3(.18f,.32f,.18f),armor,true,new Vector3(0,0,j*35));breaks.Add(broken);
                AddPhase(body,phase2,"Opened glowing rib "+i,new Vector3((i-1)*.55f,1,surface),new Vector3(.15f,1.1f,.13f),Tint);
            }
            EnemyArt.Bake(body,"world/"+Id);renderers=GetComponentsInChildren<Renderer>(true);flash=new MaterialPropertyBlock();SetPhase(1);SetPartBroken(0,false);SetPartBroken(1,false);SetPartBroken(2,false);Preview(0);
        }
        void AddPhase(Transform parent,List<Transform> list,string name,Vector3 at,Vector3 size,Color c,bool pointed=false)
        {var t=EnemyArt.Pivot(parent,name,at);P(t,name+" geometry",Vector3.zero,size,c,pointed);list.Add(t);}
        public void SetPhase(int phase)
        {Phase=Mathf.Clamp(phase,1,3);foreach(var t in phase2)t.gameObject.SetActive(Phase>=2);foreach(var t in phase3)t.gameObject.SetActive(Phase>=3);hit=.35f;}
        public void SetPartBroken(int part,bool broken)
        {if(part<0||part>=covers.Count)return;if(broken)BrokenParts|=1<<part;else BrokenParts&=~(1<<part);covers[part].gameObject.SetActive(!broken);breaks[part].gameObject.SetActive(broken);}
        public void Windup(string move,float seconds)
        {if(System.Array.IndexOf(Moves,move)<0)throw new System.ArgumentException("Unknown world boss move");skill=move;age=0;windDuration=Mathf.Max(1.5f,seconds);winding=true;striking=false;}
        public void Attack(){age=0;winding=false;striking=true;}
        public void PlayHit(){hit=.18f;}
        public void SetDead(bool value){dead=value;death=0;}
        public void Preview(float dt)
        {
            clock+=dt;age+=dt;hit=Mathf.Max(0,hit-dt);if(dead)death+=dt;
            float w=winding?Mathf.SmoothStep(0,1,Mathf.Clamp01(age/windDuration)):0,a=striking?Mathf.Sin(Mathf.Clamp01(age/.8f)*Mathf.PI):0;
            if(striking&&age>.8f)striking=false;
            float turn=skill=="sweep"?-w*32+a*65:Mathf.Sin(clock*.65f)*2;
            body.localRotation=Quaternion.Euler(dead?Mathf.Clamp01(death/1.8f)*70:Mathf.Sin(clock)*1.5f+(skill=="lance"?-w*12+a*15:0),turn,0);
            float arm=skill=="quake"?-w*90+a*55:skill=="sweep"?-w*30+a*40:-w*55+a*25;
            left.localRotation=Quaternion.Euler(arm,0,w*15);right.localRotation=Quaternion.Euler(arm,0,-w*15);
            if(skill=="scatter"){left.localRotation=Quaternion.Euler(-w*35,-w*45,w*55);right.localRotation=Quaternion.Euler(-w*35,w*45,-w*55);}
            if(skill=="gather"){left.localRotation=Quaternion.Euler(-w*70,w*35,-w*20);right.localRotation=Quaternion.Euler(-w*70,-w*35,w*20);}
            if(skill=="transition"){left.localRotation=Quaternion.Euler(-w*110,0,w*65);right.localRotation=Quaternion.Euler(-w*110,0,-w*65);}
            head.localRotation=Quaternion.Euler(-w*12+a*8,Mathf.Sin(clock*.7f)*4,0);
            if(tail)tail.localRotation=Quaternion.Euler(0,Mathf.Sin(clock*1.5f)*12+a*30,0);
            for(int i=0;i<legs.Length;i++)if(legs[i])legs[i].localRotation=Quaternion.Euler(Mathf.Sin(clock*2+i*Mathf.PI)*MoveSpeed*10,0,0);
            heart.localScale=Vector3.one*(1+Mathf.Sin(clock*3)*.05f+w*.18f);
            var color=Color.Lerp(Color.white,new Color(2,2,2),hit*3);if(dead)color=Color.Lerp(color,new Color(.3f,.32f,.4f),Mathf.Clamp01(death/2));flash.SetColor("_Color",color);foreach(var r in renderers)if(r)r.SetPropertyBlock(flash);
        }
        void Update(){Preview(Time.deltaTime);}
        public GameObject Warning(string move)
        {return WorldBossWarning.Create(transform.position,transform.forward,move,Tint);}
    }
    public sealed class WorldBossWarning:MonoBehaviour
    {
        public static GameObject Create(Vector3 origin,Vector3 forward,string move,Color tint)
        {
            var root=new GameObject("World boss "+move+" preview telegraph");root.transform.SetPositionAndRotation(origin,Quaternion.LookRotation(forward,Vector3.up));
            var type=move=="sweep"?EliteMove.Sweep:move=="quake"||move=="transition"?EliteMove.Quake:move=="lance"?EliteMove.Line:EliteMove.Shield;
            var fx=EliteEnemyFx.Create(root.transform,type);fx.Place(origin,forward,tint,.8f);
            if(move=="scatter"||move=="gather")
            {
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Cos(a)*1.65f,0,Mathf.Sin(a)*1.65f);
                    var pts=new Vector3[33];for(int j=0;j<pts.Length;j++){float b=j*Mathf.PI/16;pts[j]=p+new Vector3(Mathf.Cos(b)*.32f,.065f,Mathf.Sin(b)*.32f);}
                    SpellFx.Line(root.transform,"Virtual party marker "+(i+1),pts,false,.05f,move=="scatter"?new Color(1,.3f,.6f):new Color(.35f,.85f,1),true,false);
                    SpellFx.Line(root.transform,move=="scatter"?"Separate from linked allies":"Shield / heal channel",new[]{p+Vector3.up*.07f,Vector3.up*.07f},false,.03f,tint,true,false);
                }
            }
            if(move=="transition")for(int i=0;i<3;i++){float r=1.2f+i*.6f;var pts=new Vector3[49];for(int j=0;j<pts.Length;j++){float a=j*Mathf.PI/24;pts[j]=new Vector3(Mathf.Sin(a)*r,.08f+i*.04f,Mathf.Cos(a)*r);}SpellFx.Line(root.transform,"Phase transition shock front",pts,false,.08f,tint,true,false);}
            return root;
        }
    }
}
