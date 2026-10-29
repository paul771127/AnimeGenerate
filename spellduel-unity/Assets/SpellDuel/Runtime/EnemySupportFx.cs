using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 /// <summary>World/scene-space visual effects only. Reapplication refreshes, never stacks identical target effects.</summary>
 public static class EnemySupportFx
 {
  static readonly Dictionary<Transform,Dictionary<int,EnemySupportView>> attached=new Dictionary<Transform,Dictionary<int,EnemySupportView>>();
  internal static EnemySupportView Spawn(int kind,Vector3 at,float seconds,float radius=1,Transform target=null,Vector3 end=default(Vector3))
  {var v=new GameObject("Enemy support FX "+kind).AddComponent<EnemySupportView>();v.Init(kind,at,seconds,radius,target,end);return v;}
  static void Follow(Transform target,int kind,float seconds)
  {
   if(!target||!target.gameObject.activeInHierarchy||seconds<=0||float.IsNaN(seconds)||float.IsInfinity(seconds))return;
   if(!attached.TryGetValue(target,out var effects)){effects=new Dictionary<int,EnemySupportView>();attached[target]=effects;}
   if(effects.TryGetValue(kind,out var old)&&old){old.Refresh(seconds);return;}
   effects[kind]=Spawn(kind,target.position,seconds,1,target);
  }
  internal static void Forget(Transform target,int kind,EnemySupportView view)
  {if(ReferenceEquals(target,null))return;if(attached.TryGetValue(target,out var effects)&&effects.TryGetValue(kind,out var old)&&ReferenceEquals(old,view)){effects.Remove(kind);if(effects.Count==0)attached.Remove(target);}}
  public static void Heal(Vector3 from,Vector3 to){Spawn(0,from,.65f,1,null,to);}
  public static void Shield(Transform target,float seconds){Follow(target,1,seconds);}
  public static void ShieldBreak(Transform target)
  {
   if(!target)return;Vector3 at=target.position+Vector3.up*.85f;float radius=.5f;
   if(attached.TryGetValue(target,out var effects)&&effects.TryGetValue(1,out var view)&&view){at=view.transform.position;radius=view.Radius;view.Release();}
   Spawn(6,at,.55f,radius);
  }
  public static void Haste(Transform target,float seconds){Follow(target,2,seconds);}
  public static void Rally(Vector3 center,float radius){if(radius>0&&!float.IsNaN(radius)&&!float.IsInfinity(radius))Spawn(3,center,.9f,radius);}
  public static void RallyAura(Transform target,float seconds){Follow(target,4,seconds);}
  public static void Summon(Vector3 at){Spawn(5,at,1.1f,.6f);}
 }
 public sealed class EnemySupportView : MonoBehaviour
 {
  internal float Radius{get;private set;}
  int kind;float age,life,heightRatio=1;Transform target;Vector3 start,end,offset;bool following,released;float sunk;
  readonly List<LineRenderer> lines=new List<LineRenderer>();readonly List<Color> colors=new List<Color>();
  Vector3[] healPoints;Renderer bubble;MaterialPropertyBlock block;
  static readonly Color Green=new Color(.30f,.60f,.34f,.8f),Purple=new Color(.61f,.26f,.65f,.6f),Crimson=new Color(.94f,.15f,.25f,.8f),Gold=new Color(.85f,.49f,.16f,.75f);
  LineRenderer Line(string name,Vector3[] points,bool loop,float width,Color color){var l=SpellFx.Line(transform,name,points,loop,width,color,false,false);lines.Add(l);colors.Add(color);return l;}
  static Vector3[] Ring(int n,float radius,float y=0,float span=360)
  {var pts=new Vector3[n];for(int i=0;i<n;i++){float a=(span==360?i/(float)n:i/(float)(n-1))*span*Mathf.Deg2Rad;pts[i]=new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius);}return pts;}
  internal void Init(int effect,Vector3 at,float seconds,float radius,Transform follow,Vector3 to)
  {
   kind=effect;start=at;end=to;sunk=0;life=Mathf.Max(.05f,seconds);Radius=radius;target=follow;following=follow;transform.position=at;
   if(follow){bool found=false;var b=new Bounds(follow.position,Vector3.zero);foreach(var r in follow.GetComponentsInChildren<MeshRenderer>()){if(!r.enabled)continue;if(!found){b=r.bounds;found=true;}else b.Encapsulate(r.bounds);}Radius=found?Mathf.Clamp(Mathf.Max(b.extents.x,b.extents.z)*1.1f,.25f,.85f):.45f;heightRatio=found?Mathf.Clamp(b.extents.y*1.08f/Radius,1,3):1.7f;offset=kind==1?Vector3.up*(found?Mathf.Max(.25f,b.center.y-follow.position.y):.8f):Vector3.up*.025f;transform.position=follow.position+offset;}
   if(kind==0){healPoints=new Vector3[25];Line("Healing filament",healPoints,false,.026f,Green);Line("Travelling remedy",new[]{Vector3.zero,Vector3.up*.035f},false,.08f,Green);}
   else if(kind==1){for(int i=0;i<3;i++){var l=Line("Bubble meridian",Ring(40,Radius),true,.014f,Purple);l.transform.localRotation=Quaternion.Euler(i==0?0:90,0,i*60);}var shell=VariantGeometry.Oval(transform,"Translucent pearl shell",Vector3.zero,Vector3.one*Radius*2,Purple);bubble=shell.GetComponent<Renderer>();bubble.sharedMaterial=VariantGeometry.Ghost;block=new MaterialPropertyBlock();}
   else if(kind==2){for(int i=0;i<3;i++){var l=Line("Enemy march wind",Ring(20,Radius*(.65f+i*.18f),i*.045f,220),false,.023f,Crimson);l.transform.localRotation=Quaternion.Euler(0,i*120,0);}}
   else if(kind==3||kind==5){Line("Outer seal",Ring(48,Radius,.018f),true,.027f,kind==3?Gold:Crimson);Line("Inner seal",Ring(32,Radius*.72f,.025f),true,.018f,Purple);for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var d=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));Line("Seal spoke",new[]{d*Radius*.76f+Vector3.up*.028f,d*Radius*.96f+Vector3.up*.028f},false,.025f,Gold);}if(kind==3)Line("Flag pulse",new[]{Vector3.zero,Vector3.up*.8f,new Vector3(.3f,.72f,0),new Vector3(0,.5f,0)},false,.026f,Gold);}
   else if(kind==4){for(int i=0;i<3;i++){var l=Line("Enemy rally chevron",new[]{new Vector3(-Radius*.45f,.1f,0),new Vector3(0,.27f,0),new Vector3(Radius*.45f,.1f,0)},false,.023f,Gold);l.transform.localRotation=Quaternion.Euler(0,i*120,0);}}
   else if(kind==6){for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var p=new Vector3(Mathf.Cos(a),Mathf.Sin(a),Mathf.Sin(a*2)*.5f)*Radius;Line("Broken pearl shard",new[]{p,p+Vector3.up*.07f,p+Vector3.right*.05f},true,.018f,Purple);}}
   Tick(0);
  }
  internal void Refresh(float seconds){life=Mathf.Max(.05f,seconds);age=0;}
  public void Tick(float dt)
  {
   if(released)return;if(following&&(!target||!target.gameObject.activeInHierarchy)){Release();return;}age+=Mathf.Max(0,dt);if(age>=life){Release();return;}float p=Mathf.Clamp01(age/life),fade=Mathf.Min(1,(life-age)/.25f);
   if(following)transform.position=target.position+offset;
   if(kind==0){for(int i=0;i<healPoints.Length;i++){float t=i/(float)(healPoints.Length-1);healPoints[i]=Vector3.Lerp(Vector3.zero,end-start,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.25f);}lines[0].SetPositions(healPoints);lines[1].transform.localPosition=Vector3.Lerp(Vector3.zero,end-start,p)+Vector3.up*Mathf.Sin(p*Mathf.PI)*.25f;}
   if(kind==1){transform.localScale=new Vector3(1,heightRatio,1)*(1+.025f*Mathf.Sin(age*5));block.Clear();block.SetColor("_Color",new Color(Purple.r,Purple.g,Purple.b,.07f*fade));bubble.SetPropertyBlock(block);}
   if(kind==2)transform.rotation=Quaternion.Euler(0,age*150,0);
   if(kind==3||kind==5)transform.localScale=Vector3.one*Mathf.Lerp(.65f,1,Mathf.SmoothStep(0,1,p));
   if(kind==4)transform.localPosition+=Vector3.up*(Mathf.Repeat(age*.4f,.45f));
   if(kind==6){transform.localScale=Vector3.one*(1+p*.65f);transform.position+=Vector3.down*(p*p*.2f-sunk);sunk=p*p*.2f;}
   for(int i=0;i<lines.Count;i++){var c=colors[i];c.a*=fade;lines[i].startColor=lines[i].endColor=c;}
  }
  internal void Release(){if(released)return;released=true;EnemySupportFx.Forget(target,kind,this);gameObject.SetActive(false);VariantGeometry.Dispose(gameObject);}
  void Update(){Tick(Time.deltaTime);}
  void OnDestroy(){EnemySupportFx.Forget(target,kind,this);}
 }
}
