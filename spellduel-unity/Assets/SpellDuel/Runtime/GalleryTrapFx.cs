using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public sealed class GalleryTrapFx:MonoBehaviour
    {
        public Battle.TrapCue Cue {get;private set;}
        readonly List<LineRenderer> warnings=new List<LineRenderer>();readonly List<Transform> missiles=new List<Transform>();
        Transform moving;float width;static readonly Color Red=new Color(1,.12f,.06f,.85f),Bronze=new Color(.65f,.43f,.2f);
        LineRenderer Line(string name,Vector3[] p,float w=.05f)
        {var l=SpellFx.Line(transform,name,p,false,w,Red,true,false);warnings.Add(l);return l;}
        public static GameObject Pillar(Vector3 world)
        {
            var go=new GameObject("Indestructible bronze flame column");go.transform.position=world;
            EnemyArt.Part(go.transform,"Stone foot",new Vector3(0,.12f,0),new Vector3(.48f,.24f,.48f),new Color(.47f,.53f,.57f));
            EnemyArt.Part(go.transform,"Bronze bearing",new Vector3(0,.34f,0),new Vector3(.29f,.45f,.29f),Bronze);
            EnemyArt.Part(go.transform,"Ankle-height rotating nozzle",new Vector3(0,.22f,.19f),new Vector3(.14f,.14f,.36f),new Color(1,.5f,.16f));
            EnemyArt.Bake(go.transform,"vault flame pillar");return go;
        }
        public static GalleryTrapFx Create(Battle.TrapCue c)
        {
            var fx=new GameObject("Gallery cue "+c.id+" "+c.kind).AddComponent<GalleryTrapFx>();fx.Cue=c;fx.width=c.halfX;
            var forward=c.kind==Battle.TrapKind.Rock?Vector3.forward:c.forward;if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            fx.transform.SetPositionAndRotation(WorldFrame.FromWorld(c.center),Quaternion.LookRotation(WorldFrame.DirFromWorld(forward),Vector3.up));
            if(c.kind==Battle.TrapKind.Rock)
            {
                for(int q=0;q<4;q++)if((c.mask&(1<<q))!=0)
                {
                    float x=q%2==0?-c.halfX:0,z=q<2?-c.halfZ:0;
                    fx.Line("Single unsafe quadrant",new[]{new Vector3(x,.04f,z),new Vector3(x+c.halfX,.04f,z),new Vector3(x+c.halfX,.04f,z+c.halfZ),new Vector3(x,.04f,z+c.halfZ),new Vector3(x,.04f,z)});
                    for(int j=0;j<4;j++){float xx=x+c.halfX*(j+.5f)/4;fx.Line("Cracked falling-stone lane",new[]{new Vector3(xx,.035f,z),new Vector3(xx+.15f,.035f,z+c.halfZ*.5f),new Vector3(xx,.035f,z+c.halfZ)},.025f);}
                    var rock=EnemyArt.Part(fx.transform,"Descending ceiling slab",new Vector3(x+c.halfX*.5f,3,z+c.halfZ*.5f),new Vector3(c.halfX*.7f,.4f,c.halfZ*.7f),new Color(.55f,.61f,.65f));fx.missiles.Add(rock);
                }
            }
            else if(c.kind==Battle.TrapKind.Arrows||c.kind==Battle.TrapKind.Log)
            {
                int count=c.kind==Battle.TrapKind.Log?1:c.count;
                for(int i=0;i<count;i++)
                {
                    float lane=c.kind==Battle.TrapKind.Log?0:-c.halfX*.85f+i*c.halfX*.75f/Mathf.Max(1,count-1);
                    fx.Line("Warning trajectory from one wall",new[]{new Vector3(lane,.045f,-c.radius),new Vector3(lane,.045f,c.radius)},.06f);
                    var m=EnemyArt.Pivot(fx.transform,c.kind==Battle.TrapKind.Log?"Suspended horizontal log":"Heavy bronze arrow",new Vector3(lane,c.kind==Battle.TrapKind.Log?2:1,-c.radius));
                    if(c.kind==Battle.TrapKind.Log){EnemyArt.Part(m,"Wooden sweep cylinder",Vector3.zero,new Vector3(c.halfX*2,.35f,.35f),new Color(.58f,.31f,.14f));EnemyArt.Part(m,"Iron log band",new Vector3(-c.halfX*.6f,0,0),new Vector3(.1f,.38f,.38f),Bronze);EnemyArt.Part(m,"Iron log band right",new Vector3(c.halfX*.6f,0,0),new Vector3(.1f,.38f,.38f),Bronze);}
                    else {EnemyArt.Part(m,"Arrow shaft",Vector3.zero,new Vector3(.035f,.035f,.5f),Bronze);EnemyArt.Part(m,"Sharp arrowhead",new Vector3(0,0,.28f),new Vector3(.12f,.12f,.2f),Color.white,true,new Vector3(90,0,0));}
                    EnemyArt.Bake(m,"vault missile/"+c.kind+"/"+i+"/"+c.halfX.ToString(System.Globalization.CultureInfo.InvariantCulture));fx.missiles.Add(m);
                }
            }
            else
            {
                float angle=c.kind==Battle.TrapKind.High?150:360;float y=c.kind==Battle.TrapKind.High?1.2f:.04f;
                if(c.kind==Battle.TrapKind.Bolt||c.kind==Battle.TrapKind.Volley)
                {
                    int n=c.kind==Battle.TrapKind.Volley?3:1;
                    for(int i=0;i<n;i++){float s=(i-(n-1)*.5f)*.3f;fx.Line("Locked crossbow laser",new[]{new Vector3(0,.05f,0),new Vector3(s*c.radius,.05f,c.radius)},.065f);}
                }
                else
                {
                    var points=new Vector3[65];for(int i=0;i<65;i++){float a=(-angle*.5f+angle*i/64)*Mathf.Deg2Rad;points[i]=new Vector3(Mathf.Sin(a)*c.radius,y,Mathf.Cos(a)*c.radius);}fx.Line("Readable dodge boundary",points,.06f);
                }
                if(c.kind==Battle.TrapKind.Flame)
                {
                    fx.moving=EnemyArt.Pivot(fx.transform,"Rotating ankle flame",Vector3.zero);
                    var points=new[]{new Vector3(0,.15f,.12f),new Vector3(0,.15f,2)};
                    SpellFx.Line(fx.moving,"Low flame tongue",points,false,.18f,new Color(1,.35f,.05f,.85f),true,false);
                    SpellFx.Line(fx.moving,"Bright flame core",points,false,.055f,new Color(1,.9f,.4f,.9f),true,false);
                }
            }
            fx.Tick(c.Start);return fx;
        }
        public void Tick(float time)
        {
            var c=Cue;bool show=!c.done&&time>=c.Start&&time<=c.hit+c.duration;
            foreach(var r in warnings){r.gameObject.SetActive(show);float p=Mathf.Clamp01((time-c.Start)/c.warn);var col=Red;col.a=.35f+.6f*p;r.startColor=r.endColor=col;r.widthMultiplier=1+(p>.85f?Mathf.Sin(time*22)*.3f:0);}
            float u=Mathf.Clamp01((time-c.hit)/Mathf.Max(.01f,c.duration));
            if(moving){moving.gameObject.SetActive(show&&time>=c.hit);moving.localRotation=Quaternion.Euler(0,u*360,0);}
            foreach(var m in missiles)
            {
                m.gameObject.SetActive(show);var p=m.localPosition;
                if(c.kind==Battle.TrapKind.Rock)p.y=time<c.hit?3:.18f+3*(1-u);
                else {p.z=time<c.hit?-c.radius:-c.radius+c.radius*2*u;p.y=c.kind==Battle.TrapKind.Log?(time<c.hit?2:1.3f):1;}
                m.localPosition=p;
            }
        }
    }
}
