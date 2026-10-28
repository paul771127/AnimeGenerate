using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static class EnemySkillFx
 {
  static int nextHandle;static readonly Dictionary<int,EnemySkillView> cues=new Dictionary<int,EnemySkillView>();
  public static int Telegraph(string skillId,Vector3 at,Vector3 dir,float seconds,float size)
  {
   if(string.IsNullOrEmpty(skillId)||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds)||size<0||float.IsNaN(size)||float.IsInfinity(size))return 0;
   int handle;do{nextHandle=nextHandle==int.MaxValue?1:nextHandle+1;handle=nextHandle;}while(cues.ContainsKey(handle));
   var v=new GameObject("Enemy telegraph "+skillId).AddComponent<EnemySkillView>();cues.Add(handle,v);v.Init(skillId,at,dir,seconds,size,true,handle);return handle;
  }
  public static void Cancel(int handle){if(cues.TryGetValue(handle,out var view)){cues.Remove(handle);if(view)view.Release();}}
  internal static void Forget(int handle,EnemySkillView view){if(cues.TryGetValue(handle,out var old)&&ReferenceEquals(old,view))cues.Remove(handle);}
  public static void Cast(string skillId,Vector3 from,Vector3 to){var def=EnemySkills.Find(skillId);Cast(skillId,from,to,def!=null?def.size:.5f);}
  // Optional radius override for callers whose cue differs from the skill's default size.
  public static void Cast(string skillId,Vector3 from,Vector3 to,float size)
  {
   if(string.IsNullOrEmpty(skillId)||size<0||float.IsNaN(size)||float.IsInfinity(size))return;
   if(skillId.EndsWith("sunmend")||skillId.EndsWith("herbmend")||skillId.EndsWith("rainchorus")||skillId.EndsWith("seedmend")||skillId.EndsWith("sanctuarymend")){EnemySupportFx.Heal(from,to);return;}
   if(skillId.EndsWith("twinseals")){EnemySupportFx.Summon(to);return;}
   if(skillId.EndsWith("royalstandard")){EnemySupportFx.Rally(from,size);return;}
   var def=EnemySkills.Find(skillId);bool ground=def!=null&&def.shape==EsShape.Circle;
   var v=new GameObject("Enemy cast "+skillId).AddComponent<EnemySkillView>();v.Init(skillId,ground?to:from,to-from,def!=null&&def.shape==EsShape.JumpRing?EnemySkills.RingTime:.5f,size,false,0);
  }
 }
 public sealed class EnemySkillView:MonoBehaviour
 {
  string skill;int handle;bool warning,released;float age,life,radius;EsShape shape;float angle;
  readonly List<LineRenderer> lines=new List<LineRenderer>();readonly List<Color> colors=new List<Color>();
  static readonly Color Red=new Color(1,.16f,.12f,.8f),Orange=new Color(1,.60f,.10f,.9f),Blue=new Color(.22f,.62f,1,.85f);
  Color color;
  void Line(string name,Vector3[] points,bool loop=false,float width=.034f)
  {
   if(warning){var ink=new Color(.075f,.025f,.10f,.95f);var edge=SpellFx.Line(transform,name+" contrast edge",points,loop,width*2.2f,ink,false,false);edge.sortingOrder=31;lines.Add(edge);colors.Add(ink);}
   var l=SpellFx.Line(transform,name,points,loop,width,color,false,false);l.sortingOrder=32;lines.Add(l);colors.Add(color);
  }
  Vector3[] Arc(float r,float span,float y,int count=40){var p=new Vector3[count];for(int i=0;i<count;i++){float a=(-span*.5f+span*i/(count-1))*Mathf.Deg2Rad;p[i]=new Vector3(Mathf.Sin(a)*r,y,Mathf.Cos(a)*r);}return p;}
  internal void Init(string id,Vector3 at,Vector3 dir,float seconds,float size,bool cue,int h)
  {
   skill=id;handle=h;life=Mathf.Max(.05f,seconds);radius=size;warning=cue;var def=EnemySkills.Find(id);shape=def!=null?def.shape:EsShape.SelfGuard;angle=def!=null?def.angle:90;
   dir.y=0;if(dir.sqrMagnitude<.000001f)dir=Vector3.forward;transform.SetPositionAndRotation(at,Quaternion.LookRotation(dir,Vector3.up));
   color=shape==EsShape.JumpRing?Orange:shape==EsShape.DuckArc?Blue:Red;
   if(!cue){BuildCast();Tick(0);return;}
   if(shape==EsShape.Circle||shape==EsShape.JumpRing){Line("Locked danger boundary",Arc(radius,360,.018f),true);if(shape==EsShape.Circle){for(int i=-1;i<=1;i++){float z=i*radius*.45f;float x=Mathf.Sqrt(Mathf.Max(0,radius*radius-z*z));Line("Danger hatch",new[]{new Vector3(-x,.025f,z),new Vector3(x,.025f,z)},false,.013f);}}else Arrow(true);}
   else if(shape==EsShape.Fan||shape==EsShape.DuckArc){float y=shape==EsShape.DuckArc?1.1f:.025f;var arc=Arc(radius,angle,y);Line("Locked sweep arc",arc);Line("Sweep edges",new[]{arc[0],Vector3.up*y,arc[arc.Length-1]});if(shape==EsShape.DuckArc){Arrow(false);Line("Ground projection",Arc(radius,angle,.02f),false,.012f);}}
   else if(shape==EsShape.SelfShield||shape==EsShape.SelfGuard){Line("Interruptible channel sigil",new[]{new Vector3(-.14f,.8f,0),new Vector3(0,1.05f,0),new Vector3(.14f,.8f,0),new Vector3(0,.55f,0)},true);}
   else{float half=def!=null?def.width*.5f:.125f;Line("Locked lane",new[]{new Vector3(-half,.025f,0),new Vector3(-half,.025f,radius),new Vector3(half,.025f,radius),new Vector3(half,.025f,0)},true);for(int i=0;i<6;i++){float z=radius*(i+.5f)/6;Line("Lane hatch",new[]{new Vector3(-half,.03f,z-.06f),new Vector3(half,.03f,z+.06f)},false,.014f);}}
   Tick(0);
  }
  void Arrow(bool up){float y=up?.23f:.85f,d=up?.14f:-.14f;Line(up?"Jump chevron":"Crouch chevron",new[]{new Vector3(-.10f,y, radius*.7f),new Vector3(0,y+d,radius*.7f),new Vector3(.10f,y,radius*.7f)},false,.034f);}
  void BuildCast()
  {
   if(shape==EsShape.JumpRing){Line("Expanding ground impact",Arc(1,360,.02f),true,.055f);Line("Broken inner wave",Arc(.87f,360,.025f),true,.018f);return;}
   if(shape==EsShape.DuckArc){Line("Chest height slash ribbon",Arc(radius,angle,1.1f),false,.09f);Line("Chest height leading edge",Arc(radius*.94f,angle,1.1f),false,.02f);return;}
   if(shape==EsShape.Circle){bool sand=skill.Contains("sand")||skill.Contains("hourglass");color=sand?new Color(.86f,.48f,.18f,.85f):skill.Contains("kiln")?new Color(1,.28f,.08f,.9f):Red;Line("Ground strike seal",Arc(radius,360,.025f),true,.04f);
    for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius*.6f;Line(sand?"Rising sand shard":"Impact ember",new[]{p,p+Vector3.up*(sand?.55f:.4f),p*.7f},false,.045f);}if(skill.EndsWith("hourglass"))Line("Falling hourglass",new[]{new Vector3(-.13f,.8f,0),new Vector3(.13f,.8f,0),new Vector3(-.13f,.4f,0),new Vector3(.13f,.4f,0),new Vector3(-.13f,.8f,0)},false,.025f);return;}
   if(shape==EsShape.Shot||shape==EsShape.Volley||shape==EsShape.Cross||shape==EsShape.Retreat){Line("Release sparks",new[]{new Vector3(-.1f,1.1f,0),new Vector3(0,1.2f,.12f),new Vector3(.1f,1.1f,0)},false,.04f);return;}
   if(shape==EsShape.SelfShield||shape==EsShape.SelfGuard){color=new Color(.62f,.2f,.52f,.65f);for(int i=0;i<3;i++)Line("Protective seal",Arc(.22f+i*.035f,250,.7f+i*.16f),false,.035f);return;}
   if(skill.EndsWith("rootlash")){color=new Color(.30f,.48f,.17f,.8f);var vine=new Vector3[14];for(int i=0;i<vine.Length;i++)vine[i]=new Vector3(Mathf.Sin(i*2)*.07f,.13f,radius*i/(vine.Length-1));Line("Whipping vine",vine,false,.055f);return;}
   Line("Weapon sweep",Arc(Mathf.Min(radius,1.2f),95,.85f),false,.075f);Line("Steel cutting edge",Arc(Mathf.Min(radius,1.2f)*.95f,95,.85f),false,.018f);
   if(shape==EsShape.Back){color=new Color(.42f,.14f,.49f,.5f);Line("Local fading decoy",new[]{new Vector3(-.16f,.3f,0),new Vector3(-.12f,1.4f,0),new Vector3(.12f,1.4f,0),new Vector3(.16f,.3f,0)},false,.035f);}
  }
  public void Tick(float dt)
  {
   if(released)return;age+=Mathf.Max(0,dt);if(age>=life){Release();return;}float p=Mathf.Clamp01(age/life),alpha=warning?.45f+.55f*p:1-p;
   if(!warning&&shape==EsShape.JumpRing)transform.localScale=new Vector3(Mathf.Max(.001f,radius*p),1,Mathf.Max(.001f,radius*p));
   for(int i=0;i<lines.Count;i++){var c=colors[i];c.a*=alpha;lines[i].startColor=lines[i].endColor=c;}
  }
  internal void Release(){if(released)return;released=true;EnemySkillFx.Forget(handle,this);gameObject.SetActive(false);VariantGeometry.Dispose(gameObject);}
  void Update(){Tick(Time.deltaTime);}void OnDestroy(){EnemySkillFx.Forget(handle,this);}
 }
}
