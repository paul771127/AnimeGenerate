using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  public static void ChainArc(Vector3 fromWorld,Vector3 toWorld,Color c)
  {
   var d=toWorld-fromWorld;var a=NewAnim("SpellFx chain arc",fromWorld,Quaternion.identity,.18f);var p=new Vector3[13];var side=Vector3.Cross(d.normalized,Vector3.up);if(side.sqrMagnitude<.01f)side=Vector3.right;
   for(int i=0;i<13;i++){float u=i/12f;p[i]=d*u+side*(i==0||i==12?0:Mathf.Sin(i*7.3f)*.07f);}
   a.AddSlash(a.transform,p,.023f,c,0,.15f,1,1,0);
  }
  public static void Thorns(Transform victim,Vector3 attackerWorld)
  {
   if(!victim)return;var from=victim.position+Vector3.up*.8f;var d=attackerWorld-from;if(d.sqrMagnitude<.001f)d=Vector3.forward;var a=NewAnim("SpellFx reflected thorns",from,Quaternion.LookRotation(d),.35f);
   for(int i=-1;i<=1;i++)a.AddSlash(a.transform,new[]{new Vector3(i*.08f,0,0),new Vector3(i*.03f,0,Mathf.Min(1,d.magnitude))},.035f,new Color(1,.65f,.25f),i==0?0:.03f,.25f,.2f,1,0);
  }
  public static void StackAura(Transform owner,int stacks)
  {
   if(!owner)return;var v=owner.GetComponent<HitStackArt>();if(!v){if(stacks<=0)return;v=owner.gameObject.AddComponent<HitStackArt>();v.Build();}v.SetStacks(stacks);
  }
  public static void ExecuteHit(Vector3 atWorld)
  {var a=NewAnim("SpellFx execute",atWorld,Quaternion.identity,.45f);for(int i=0;i<2;i++)a.AddSlash(a.transform,new[]{new Vector3(-.3f,-.45f,0),new Vector3(.3f,.45f,0)},.075f,new Color(1,.12f,.22f),i*.035f,.3f,.4f,1.2f,i*80);}
  public static void FirstStrikeHit(Vector3 atWorld)
  {var a=NewAnim("SpellFx first strike",atWorld,Quaternion.identity,.4f);a.AddRing(a.transform,Circle(40,1),true,.03f,Color.white,0,.3f,.03f,.55f);Sparks(a.transform,Color.white,.4f,8,1,2);}
  public static void KillRush(Transform owner){if(owner)TeamBuffCast("rangerhaste",owner);}
  public static void SoulDrain(Vector3 fromWorld,Transform owner)
  {if(!owner)return;var go=new GameObject("SpellFx soul drain");go.AddComponent<SoulDrainArt>().Build(fromWorld,owner);}
 }
 public sealed class HitStackArt:MonoBehaviour
 {
  Transform root;LineRenderer[] dots;int stacks;
  public void Build(){root=SkyArt.Node(transform,"Ten hit-stack motes",Vector3.zero);dots=new LineRenderer[10];for(int i=0;i<10;i++)dots[i]=SpellFx.Line(root,"Stack "+(i+1),new Vector3[2],false,.035f,new Color(1,.6f,.2f),true,false);}
  public void SetStacks(int value){stacks=Mathf.Clamp(value,0,10);root.gameObject.SetActive(stacks>0);Sample();}
  public void Sample()
  {
   var cam=Camera.main;bool self=cam&&SelfFxEdge.NearOwner(cam,transform.position);
   for(int i=0;i<10;i++){var line=dots[i];line.enabled=i<stacks;if(self){float z=cam.nearClipPlane+.045f,y=Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad)*z,x=y*cam.aspect;float xx=(-.72f+i*.16f)*x;var p=cam.transform.TransformPoint(new Vector3(xx,-y*.9f,z));line.SetPosition(0,root.InverseTransformPoint(p));line.SetPosition(1,root.InverseTransformPoint(p+cam.transform.up*y*.03f));line.widthMultiplier=y*.028f;}else{float a=i*Mathf.PI/5;var p=new Vector3(Mathf.Cos(a)*.42f,.65f,Mathf.Sin(a)*.42f);line.SetPosition(0,p);line.SetPosition(1,p+Vector3.up*.045f);line.widthMultiplier=.025f;}}
  }
  void LateUpdate(){if(root&&stacks>0)Sample();}
 }
 public sealed class SoulDrainArt:MonoBehaviour
 {
  Vector3 start;Transform owner;float age;LineRenderer tail;readonly Vector3[] points=new Vector3[10];
  public void Build(Vector3 from,Transform target){start=WorldFrame.ToWorld(from);owner=target;SkyArt.Oval(transform,"Returning soul pearl",Vector3.zero,Vector3.one*.10f,new Color(.73f,.43f,1));tail=SpellFx.Line(transform,"Soul ribbon",points,false,.045f,new Color(.75f,.4f,1,.6f),true,false);tail.widthCurve=AnimationCurve.Linear(0,1,1,0);Sample(0);}
  public void Sample(float dt)
  {
   if(!owner){if(Application.isPlaying)Destroy(gameObject);return;}age+=Mathf.Max(0,dt);float u=Mathf.Clamp01(age/.6f);var end=WorldFrame.ToWorld(owner.position+Vector3.up*.9f);var p=Vector3.Lerp(start,end,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*.4f;transform.position=WorldFrame.FromWorld(p);
   for(int i=0;i<10;i++){float k=Mathf.Max(0,u-i*.018f);var q=Vector3.Lerp(start,end,k)+Vector3.up*Mathf.Sin(k*Mathf.PI)*.4f;tail.SetPosition(i,transform.InverseTransformPoint(WorldFrame.FromWorld(q)));}transform.localScale=Vector3.one*(1-u*.65f);if(age>=.6f&&Application.isPlaying)Destroy(gameObject);
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
