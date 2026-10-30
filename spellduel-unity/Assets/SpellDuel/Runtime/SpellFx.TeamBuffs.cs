using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  internal static Color SkillTint(string id){SkillDef s;if(Skills.All.TryGetValue(id,out s))return s.color;return new Color(.7f,.85f,1);}
  public static void TeamBuffCast(string skillId,Transform owner)
  {
   if(!owner)return;var go=new GameObject("SpellFx buff cast "+skillId);go.transform.SetParent(owner,false);go.AddComponent<TeamBuffArt>().Build(skillId,true);
  }
  public static void TeamBuffAura(Transform owner,string skillId,float remain01)
  {
   if(!owner||string.IsNullOrEmpty(skillId))return;var set=owner.GetComponent<TeamBuffArtSet>();if(!set){if(remain01<=0)return;set=owner.gameObject.AddComponent<TeamBuffArtSet>();}set.Set(skillId,remain01);
  }
  // Cosmetic hit direction only; damage and ward duration remain owned by combat.
  public static void TeamBuffHit(Transform owner,Vector3 hitWorld)
  {if(owner){var set=owner.GetComponent<TeamBuffArtSet>();if(set)set.Hit(hitWorld);}}
 }
 public sealed class TeamBuffArtSet:MonoBehaviour
 {
  readonly Dictionary<string,TeamBuffArt> views=new Dictionary<string,TeamBuffArt>();
  public void Set(string id,float level)
  {
   TeamBuffArt view;if(!views.TryGetValue(id,out view)||!view){if(level<=0)return;var go=new GameObject("SpellFx aura "+id);go.transform.SetParent(transform,false);view=go.AddComponent<TeamBuffArt>();view.Build(id,false);views[id]=view;}
   view.SetLevel(level);
  }
  public void Hit(Vector3 p){TeamBuffArt view;if(views.TryGetValue("elementward",out view)&&view&&view.gameObject.activeSelf)view.Hit(p);}
 }
 public sealed class TeamBuffArt:MonoBehaviour
 {
  string id;bool burst;float age,level=1,hit;int hitPiece;Color tint;Transform art;LineRenderer[] lines,edge;Vector3[][] basis;Color[] colors;Camera edgeCamera;
  public void Build(string skill,bool casting)
  {
   id=skill;burst=casting;tint=SpellFx.SkillTint(id);art=SkyArt.Node(transform,"World buff motif",Vector3.zero);var list=new List<LineRenderer>();var shades=new List<Color>();
   if(id=="arcanehaste")
   {
    Add(list,shades,Orbit(.42f,.02f,48),true,tint,.018f);
    for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Add(list,shades,new[]{P(a,.35f,.02f),P(a,.40f,.02f)},false,tint,.015f);}
    Add(list,shades,new[]{Vector3.up*.02f,new Vector3(.23f,.02f,0)},false,Color.white,.018f);Add(list,shades,new[]{Vector3.up*.02f,new Vector3(0,.02f,.30f)},false,tint,.012f);
   }
   else if(id=="elementward")
   {
    var four=new[]{new Color(1,.35f,.22f,.45f),new Color(.25f,.7f,1,.45f),new Color(.4f,1,.67f,.45f),new Color(1,.79f,.35f,.45f)};
    for(int i=0;i<4;i++){var p=new Vector3[17];float a=i*Mathf.PI/2;for(int k=0;k<17;k++){float u=k/16f*Mathf.PI*2;p[k]=new Vector3(Mathf.Cos(a)*(.4f+.12f*Mathf.Cos(u)),.85f+.6f*Mathf.Sin(u),Mathf.Sin(a)*(.4f+.12f*Mathf.Cos(u)));}Add(list,shades,p,true,four[i],.045f);}
   }
   else if(id=="rangerhaste")
   {
    for(int j=0;j<3;j++){var p=new Vector3[28];for(int i=0;i<p.Length;i++){float u=i/27f;float a=u*Mathf.PI*1.5f+j*2.1f;p[i]=P(a,.43f,.06f+u*(burst?1.5f:.2f));}Add(list,shades,p,false,tint,.018f);}
    Add(list,shades,new[]{new Vector3(.35f,.7f,0),new Vector3(.46f,.9f,.03f),new Vector3(.35f,1.1f,0)},false,tint,.014f);
   }
   else // Hawk eye and orbiting feather sigils.
   {
    for(int j=0;j<4;j++){float a=j*Mathf.PI/2;var center=P(a,.43f,.8f);var p=new[]{center+Vector3.up*.16f,center+new Vector3(.07f,0,0),center-Vector3.up*.16f,center-new Vector3(.07f,0,0),center+Vector3.up*.16f};Add(list,shades,p,false,tint,.016f);Add(list,shades,new[]{p[0],p[2]},false,tint,.008f);}
    if(burst){Add(list,shades,new[]{new Vector3(-.3f,1.1f,.28f),new Vector3(0,1.22f,.28f),new Vector3(.3f,1.1f,.28f),new Vector3(0,1.03f,.28f),new Vector3(-.3f,1.1f,.28f)},false,tint,.022f);}
   }
   lines=list.ToArray();colors=shades.ToArray();basis=new Vector3[lines.Length][];for(int i=0;i<lines.Length;i++){basis[i]=new Vector3[lines[i].positionCount];lines[i].GetPositions(basis[i]);}Sample(0);
  }
  static Vector3 P(float a,float r,float y)=>new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
  static Vector3[] Orbit(float r,float y,int n){var p=new Vector3[n];for(int i=0;i<n;i++)p[i]=P(i*Mathf.PI*2/n,r,y);return p;}
  void Add(List<LineRenderer> list,List<Color> shades,Vector3[] pts,bool loop,Color c,float w){list.Add(SpellFx.Line(art,id+" motif",pts,loop,w,c,true,false));shades.Add(c);}
  public void SetLevel(float value){level=Mathf.Clamp01(value);gameObject.SetActive(level>0);}
  public void Hit(Vector3 at){var p=transform.InverseTransformPoint(at);hitPiece=((Mathf.RoundToInt(Mathf.Atan2(p.z,p.x)/(Mathf.PI/2))%4)+4)%4;hit=.22f;}
  void DrawEdge(Camera cam,float alpha)
  {
   if(edgeCamera!=cam){ClearEdge();edgeCamera=cam;edge=new LineRenderer[4];for(int i=0;i<4;i++){edge[i]=SpellFx.Line(cam.transform,"Self "+id+" corner",new Vector3[5],false,.002f,tint,true,false);edge[i].sortingOrder=70;}}
   float z=cam.nearClipPlane+.04f,y=Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad)*z,x=y*cam.aspect;
   for(int i=0;i<4;i++)
   {
    float sx=(i%2==0?-1:1),sy=(i<2?-1:1);float inset=id=="arcanehaste"?.82f:id=="elementward"?.90f:id=="rangerhaste"?.86f:.94f;var lr=edge[i];
    for(int k=0;k<5;k++){float u=k/4f;float xx=inset-.12f*u,yy=inset-.035f*Mathf.Sin(u*Mathf.PI);if(id=="hawkeye"){xx=k<2?inset-.12f+k*.12f:inset;yy=k<2?inset:inset-(k-1)*.04f;}lr.SetPosition(k,new Vector3(sx*x*xx,sy*y*yy,z));}
    var c=id=="elementward"?colors[i]:tint;c.a=.45f*alpha;lr.startColor=lr.endColor=c;lr.widthMultiplier=y*.012f;lr.enabled=alpha>.001f;
   }
  }
  public void Sample(float dt)
  {
   age+=Mathf.Max(0,dt);hit=Mathf.Max(0,hit-dt);float alpha=burst?Mathf.Sin(Mathf.Clamp01(age/1.1f)*Mathf.PI):Mathf.Min(1,level*5)*.6f;
   var cam=Camera.main;bool self=cam&&transform.parent&&SelfFxEdge.NearOwner(cam,transform.parent.position);art.gameObject.SetActive(!self);if(self)DrawEdge(cam,alpha);else if(edge!=null)foreach(var e in edge)if(e)e.enabled=false;
   art.localRotation=Quaternion.Euler(0,age*(id=="arcanehaste"?75:35),0);
   art.localPosition=id=="arcanehaste"?Vector3.up*(burst?Mathf.Clamp01(age/.8f)*1.6f:.75f):Vector3.zero;
   if(!burst&&id=="arcanehaste")art.localScale=Vector3.one*.45f;
   for(int i=0;i<lines.Length;i++){var c=colors[i];c.a*=alpha;if(id=="elementward"&&hit>0&&i==hitPiece)c=Color.Lerp(c,Color.white,hit/.22f);lines[i].startColor=lines[i].endColor=c;if(id=="arcanehaste"&&i>=13)lines[i].transform.localRotation=Quaternion.Euler(0,age*(i==13?170:32),0);}
   if(burst&&age>1.1f&&Application.isPlaying)Destroy(gameObject);
  }
  void LateUpdate(){Sample(Time.deltaTime);}
  void ClearEdge(){if(edge!=null)foreach(var e in edge)if(e)SkyArt.Dispose(e.gameObject);edge=null;edgeCamera=null;}
  void OnDisable(){if(edge!=null)foreach(var e in edge)if(e)e.enabled=false;}
  void OnDestroy(){ClearEdge();}
 }
}
