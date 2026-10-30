using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  public static void WyrmFade(GameObject wyrm){if(!wyrm)return;var v=wyrm.GetComponent<ArcaneWyrmView>();if(v)v.BeginFade();else Object.Destroy(wyrm);}
  public static void WyrmPierce(Vector3 atWorld){var a=NewAnim("SpellFx Wyrm pierce",atWorld,Quaternion.identity,.45f);Sparks(a.transform,new Color(.65f,.95f,1),.45f,9,.3f,1.2f);}
  static void BuildArcaneWyrm(GameObject root,Color c){root.AddComponent<ArcaneWyrmView>().Build(c);}
 }
 public sealed class ArcaneWyrmView:MonoBehaviour
 {
  const int Rings=25,Sides=8,History=128;const float Length=2.4f;
  readonly Vector3[] trail=new Vector3[History],centers=new Vector3[Rings];
  readonly Vector3[] vertices=new Vector3[Rings*Sides+(Rings-1)*3];
  readonly Color[] colors=new Color[Rings*Sides+(Rings-1)*3];
  Mesh mesh;Transform head;LineRenderer spine;LineRenderer[] scales;LineRenderer[] whiskers;ParticleSystem motes;int newest,count;float time,fade=-1;Vector3 previous,frozen;Color tint;bool initialized;
  public int VisibleSections{get;private set;}
  public Vector3 SectionWorld(int index)=>WorldFrame.FromWorld(centers[Mathf.Clamp(index,0,Rings-1)]);
  public void Build(Color c)
  {
   tint=c;head=new GameObject("Horned luminous dragon head").transform;head.SetParent(transform,false);
   SkyArt.Oval(head,"Dragon skull",new Vector3(0,0,-.015f),new Vector3(.24f,.19f,.29f),c);
   SkyArt.Oval(head,"Long dragon snout",new Vector3(0,-.025f,.12f),new Vector3(.15f,.085f,.22f),Color.Lerp(c,Color.white,.4f));
   SkyArt.Oval(head,"Open mouth shadow",new Vector3(0,-.07f,.15f),new Vector3(.12f,.035f,.18f),new Color(.14f,.31f,.40f));
   SkyArt.Oval(head,"Lower dragon jaw",new Vector3(0,-.10f,.12f),new Vector3(.14f,.045f,.23f),c);
   whiskers=new LineRenderer[2];
   for(int side=-1;side<=1;side+=2)
   {
    var eye=SkyArt.Oval(head,"Luminous eye",new Vector3(side*.105f,.045f,.07f),new Vector3(.034f,.032f,.065f),Color.white);eye.GetComponent<Renderer>().sharedMaterial=SpellFx.AddMat(SpellFx.TexSoft);
    SpellFx.Line(head,"Antler horn",new[]{new Vector3(side*.075f,.07f,-.07f),new Vector3(side*.13f,.23f,-.13f),new Vector3(side*.18f,.27f,-.22f)},false,.025f,new Color(1,.87f,.48f),false,false);
    SpellFx.Line(head,"Horn fork",new[]{new Vector3(side*.12f,.18f,-.11f),new Vector3(side*.22f,.21f,-.08f)},false,.015f,new Color(1,.87f,.48f),false,false);
    whiskers[(side+1)/2]=SpellFx.Line(head,"Flowing whisker",new Vector3[9],false,.011f,c,true,false);
   }
   mesh=new Mesh{name="Arcane wyrm dynamic swept body"};mesh.MarkDynamic();var tris=new int[(Rings-1)*Sides*6+(Rings-1)*3];int at=0;
   for(int j=0;j<Rings-1;j++)for(int i=0;i<Sides;i++){int a=j*Sides+i,b=j*Sides+(i+1)%Sides;tris[at++]=a;tris[at++]=a+Sides;tris[at++]=b;tris[at++]=b;tris[at++]=a+Sides;tris[at++]=b+Sides;}
   for(int j=0;j<Rings-1;j++){int a=Rings*Sides+j*3;tris[at++]=a;tris[at++]=a+1;tris[at++]=a+2;}
   for(int j=0;j<Rings;j++)for(int i=0;i<Sides;i++)colors[j*Sides+i]=Color.Lerp(c,Color.white,i<4?.45f:.05f);
   for(int i=Rings*Sides;i<colors.Length;i++)colors[i]=new Color(1,.86f,.5f);
   mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=tris;
   SkyArt.MeshPart(transform,"Flexible tapered dragon",mesh,AnimeFace.Material(Color.white,4),Vector3.zero,Vector3.one);
   spine=SpellFx.Line(transform,"Dorsal scale light",new Vector3[Rings],false,.012f,Color.white,true,false);
   scales=new LineRenderer[2];for(int i=0;i<2;i++)scales[i]=SpellFx.Line(transform,"Chevron scale highlights",new Vector3[Rings],false,.007f,new Color(.85f,1,1,.45f),true,false);
   motes=BlueprintGearDetail.Particles(transform,"Tail and whisker light dust",Vector3.zero,c,.018f,32,.1f,.4f,0);
  }
  void Push(Vector3 p){if(count>1&&Vector3.Distance(trail[(newest-1+History)%History],p)<.04f){trail[newest]=p;return;}newest=(newest+1)%History;trail[newest]=p;count=Mathf.Min(History,count+1);}
  Vector3 Along(float distance,out bool available)
  {
   var p=trail[newest];for(int n=1;n<count;n++){var q=trail[(newest-n+History)%History];float d=Vector3.Distance(p,q);if(distance<=d){available=true;return Vector3.Lerp(p,q,d>.00001f?distance/d:0);}distance-=d;p=q;}
   available=distance<.001f;return p;
  }
  public void BeginFade(){if(fade>=0)return;fade=0;frozen=WorldFrame.ToWorld(transform.position);}
  public void Sample(float dt)
  {
   dt=Mathf.Max(0,dt);time+=dt;var now=WorldFrame.ToWorld(transform.position);
   if(!initialized){initialized=true;previous=now;trail[0]=now;count=1;newest=0;}
   if(fade<0)
   {
    float distance=Vector3.Distance(previous,now);if(distance>6){count=1;newest=0;trail[0]=now;}
    else if(distance>.001f){int steps=Mathf.Min(History-1,Mathf.CeilToInt(distance/.04f));for(int i=1;i<=steps;i++)Push(Vector3.Lerp(previous,now,i/(float)steps));}
    previous=now;
   }
   else{fade+=dt;transform.position=WorldFrame.FromWorld(frozen);if(fade>1.05f){if(Application.isPlaying)Destroy(gameObject);return;}}
   VisibleSections=0;
   for(int j=0;j<Rings;j++)
   {
    float u=j/(float)(Rings-1);bool available;var p=Along(u*Length,out available);if(available)VisibleSections++;
    bool unused;var ahead=Along(Mathf.Max(0,u*Length-.05f),out unused);var behind=Along(u*Length+.05f,out unused);var forward=(ahead-behind).normalized;if(forward.sqrMagnitude<.01f)forward=WorldFrame.DirToWorld(transform.forward);
    var right=Vector3.Cross(Vector3.up,forward).normalized;if(right.sqrMagnitude<.01f)right=Vector3.right;var up=Vector3.Cross(forward,right).normalized;
    if(available)p+=right*(Mathf.Sin(time*8-u*9)*.045f*u)+up*(Mathf.Sin(time*6-u*7)*.027f*u);
    centers[j]=p;float disappear=fade<0?1:1-Mathf.Clamp01((fade-u*.65f)/.22f);float radius=available?(.105f*(1-u)+.008f)*disappear:0;
    for(int i=0;i<Sides;i++){float a=i*Mathf.PI*2/Sides;vertices[j*Sides+i]=transform.InverseTransformPoint(WorldFrame.FromWorld(p+(right*Mathf.Cos(a)+up*Mathf.Sin(a))*radius));}
    for(int side=0;side<2;side++)scales[side].SetPosition(j,transform.InverseTransformPoint(WorldFrame.FromWorld(p+right*((side==0?-1:1)*radius*.8f)+up*(radius*(j%2==0?.58f:.2f)))));
    spine.SetPosition(j,transform.InverseTransformPoint(WorldFrame.FromWorld(p+up*radius)));
    if(j<Rings-1){int k=Rings*Sides+j*3;vertices[k]=vertices[j*Sides+2];vertices[k+1]=transform.InverseTransformPoint(WorldFrame.FromWorld(p+up*(radius+.065f*disappear)*(available?1:0)));vertices[k+2]=transform.InverseTransformPoint(WorldFrame.FromWorld(p-forward*.085f+up*radius));}
   }
   mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();head.localScale=Vector3.one*(fade<0?1:1-Mathf.Clamp01(fade/.18f));
   for(int w=0;w<2;w++)for(int i=0;i<9;i++){float u=i/8f;whiskers[w].SetPosition(i,new Vector3((w==0?-1:1)*(.06f+u*.24f),-.025f+Mathf.Sin(time*7-u*5)*.035f*u,.15f-u*.38f));}
   var color=tint;color.a=fade<0?.65f:Mathf.Clamp01(1-fade);spine.startColor=spine.endColor=color;foreach(var scale in scales)scale.startColor=scale.endColor=color;
   if(dt>0&&count>2){var emit=new ParticleSystem.EmitParams{position=transform.InverseTransformPoint(SectionWorld(fade<0?Mathf.Max(0,VisibleSections-1):Mathf.Clamp(Mathf.RoundToInt(fade/.65f*24),0,24)))};motes.Emit(emit,1);}
  }
  void LateUpdate(){Sample(Time.deltaTime);}
  void OnDestroy(){SkyArt.Dispose(mesh);}
 }
}
