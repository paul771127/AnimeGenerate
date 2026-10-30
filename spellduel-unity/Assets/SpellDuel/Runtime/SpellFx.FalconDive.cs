using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  public static void FalconDive(Vector3 fromWorld,Vector3 toWorld,float seconds)
  {var go=new GameObject("SpellFx Falcon dive");go.AddComponent<FalconDiveArt>().Build(fromWorld,toWorld,seconds);}
  internal static void FalconClaw(Vector3 at)
  {
   var a=NewAnim("SpellFx Falcon claw",at,Quaternion.identity,.65f);var c=new Color(1,.72f,.24f);
   for(int i=-1;i<=1;i++)a.AddSlash(a.transform,new[]{new Vector3(-.16f+i*.08f,.23f,0),new Vector3(.08f+i*.08f,-.18f,0)},.025f,c,0,.3f,.3f,1,15);
   var ps=NewPS(a.transform,"Scattered golden feathers",AddMat(TexLine),12,false);var m=ps.main;m.startLifetime=.6f;m.startSize=new ParticleSystem.MinMaxCurve(.035f,.08f);m.startColor=c;m.startSpeed=new ParticleSystem.MinMaxCurve(.5f,1.4f);m.gravityModifier=.1f;Sphere(ps,.1f);FadeOut(ps);ps.Play();ps.Emit(10);
  }
 }
 public sealed class FalconDiveArt:MonoBehaviour
 {
  Vector3 start,end,direction;float duration,age;bool struck;Transform[] wings=new Transform[2];LineRenderer[] streaks;readonly Vector3[] points=new Vector3[6];
  public void Build(Vector3 from,Vector3 to,float seconds)
  {
   start=WorldFrame.ToWorld(from);end=WorldFrame.ToWorld(to);duration=Mathf.Max(.01f,seconds);direction=(end-start).normalized;if(direction.sqrMagnitude<.01f)direction=Vector3.forward;
   var gold=new Color(1,.58f,.16f);var cream=new Color(1,.88f,.56f);
   SkyArt.Oval(transform,"Falcon streamlined body",Vector3.zero,new Vector3(.17f,.19f,.42f),gold);
   SkyArt.Oval(transform,"Falcon cream throat",new Vector3(0,.06f,.18f),new Vector3(.17f,.17f,.19f),cream);
   SkyArt.Oval(transform,"Hooked beak",new Vector3(0,.025f,.28f),new Vector3(.06f,.07f,.11f),new Color(.35f,.22f,.15f),true);
   for(int side=-1;side<=1;side+=2)
   {
    SkyArt.Oval(transform,"Sharp falcon eye",new Vector3(side*.077f,.085f,.20f),new Vector3(.025f,.024f,.035f),new Color(.15f,.20f,.25f));
    var wing=SkyArt.Node(transform,"Articulated dive wing",new Vector3(side*.07f,.01f,0));wings[(side+1)/2]=wing;
    SkyArt.Oval(wing,"Wing shoulder",new Vector3(side*.14f,0,-.03f),new Vector3(.29f,.055f,.18f),gold);
    for(int i=0;i<6;i++){var feather=SkyArt.Oval(wing,"Tapered flight feather",new Vector3(side*(.23f+i*.035f),0,-.08f-i*.025f),new Vector3(.055f,.025f,.24f-i*.012f),i%2==0?cream:gold,true);feather.localRotation=Quaternion.Euler(0,side*(10+i*7),0);}
    for(int i=0;i<3;i++)SkyArt.Rod(transform,"Curved grasping talon",new Vector3(side*.045f,-.08f,.06f),new Vector3(side*(.035f+i*.017f),-.15f,.13f),.012f,cream);
   }
   for(int i=-1;i<=1;i++){var tail=SkyArt.Oval(transform,"Split tail feather",new Vector3(i*.045f,-.015f,-.29f),new Vector3(.055f,.025f,.24f),cream,true);tail.localRotation=Quaternion.Euler(0,i*-14,0);}
   streaks=new LineRenderer[2];for(int i=0;i<2;i++)streaks[i]=SpellFx.Line(transform,"Golden wing wake",new Vector3[6],false,.012f,new Color(1,.7f,.2f,.65f),true,false);
   Sample(0);
  }
  public void Sample(float dt)
  {
   age+=Mathf.Max(0,dt);float t=Mathf.Clamp01(age/duration),exit=Mathf.Max(0,age-duration);var p=Vector3.Lerp(start,end,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.18f);
   if(exit>0)p=end+direction*(exit*1.4f)+Vector3.up*(exit*exit*4);
   transform.position=WorldFrame.FromWorld(p);transform.rotation=Quaternion.LookRotation(WorldFrame.DirFromWorld(exit>0?(direction+Vector3.up*(exit*5)).normalized:direction));
   float fold=exit>0?Mathf.Lerp(70,5,Mathf.Clamp01(exit/.2f)):Mathf.Lerp(5,70,Mathf.Clamp01(t*3));
   wings[0].localRotation=Quaternion.Euler(0,-fold,0);wings[1].localRotation=Quaternion.Euler(0,fold,0);
   float fade=1-Mathf.Clamp01((exit-.15f)/.45f);transform.localScale=Vector3.one*fade;
   for(int s=0;s<2;s++){for(int i=0;i<6;i++){float u=i/5f;points[i]=new Vector3((s==0?-1:1)*(.12f+u*.06f),Mathf.Sin(u*3-age*10)*.025f,-u*.7f);streaks[s].SetPosition(i,points[i]);}var c=new Color(1,.7f,.2f,.65f*fade);streaks[s].startColor=streaks[s].endColor=c;}
   if(!struck&&age>=duration){struck=true;SpellFx.FalconClaw(WorldFrame.FromWorld(end));}
   if(exit>.65f&&Application.isPlaying)Destroy(gameObject);
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
