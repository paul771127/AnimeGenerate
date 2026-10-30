using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  public static GameObject CreateTrapVisual(SkillDef s)
  {
   if(s==null||(s.id!="landmine"&&s.id!="snaretrap"&&s.id!="trapchain"))return null;
   var go=new GameObject("SpellFx trap "+s.id);go.AddComponent<TrapArtView>().Build(s.id);return go;
  }
  public static void SetTrapArmed(GameObject trap,bool armed){if(trap){var v=trap.GetComponent<TrapArtView>();if(v)v.SetArmed(armed);}}
  public static void MineBlast(Vector3 atWorld,float radius,Color c)
  {
   radius=Mathf.Max(.05f,radius);var a=NewAnim("SpellFx MineBlast",atWorld,Quaternion.identity,1.8f);
   var floor=SkyArt.Node(a.transform,"Exact blast radius",Vector3.up*.025f);floor.localRotation=Quaternion.Euler(90,0,0);
   a.AddRing(floor,Circle(64,1),true,.075f,c,0,.65f,.05f,radius);a.AddRing(floor,Circle(64,1),true,.018f,new Color(1,.85f,.4f),.10f,.7f,.05f,radius);
   var fire=NewPS(a.transform,"Hot rolling fireball",AddMat(TexSoft),28,false);var m=fire.main;m.startLifetime=new ParticleSystem.MinMaxCurve(.25f,.6f);m.startSpeed=new ParticleSystem.MinMaxCurve(.8f,2.6f);m.startSize=new ParticleSystem.MinMaxCurve(.22f,.6f);m.startColor=new ParticleSystem.MinMaxGradient(c,new Color(1,.82f,.3f));Sphere(fire,.15f);FadeOut(fire);fire.Play();fire.Emit(24);
   var smoke=NewPS(a.transform,"Vertical smoke column",BlendMat(TexSoft),16,false);smoke.transform.rotation=Quaternion.Euler(-90,0,0);m=smoke.main;m.startLifetime=1.4f;m.startSpeed=new ParticleSystem.MinMaxCurve(.7f,1.8f);m.startSize=new ParticleSystem.MinMaxCurve(.35f,.7f);m.startColor=new Color(.24f,.24f,.27f,.6f);Cone(smoke,15,.1f);FadeOut(smoke);smoke.Play();smoke.Emit(12);
   var chips=NewPS(a.transform,"Thrown rock chips",BlendMat(TexLine),24,false);m=chips.main;m.startLifetime=.75f;m.startSpeed=new ParticleSystem.MinMaxCurve(2,4);m.startSize=new ParticleSystem.MinMaxCurve(.035f,.09f);m.startColor=new Color(.55f,.34f,.2f);m.gravityModifier=1;Sphere(chips,.12f);FadeOut(chips);chips.Play();chips.Emit(20);
  }
 }
 public sealed class TrapArtView:MonoBehaviour
 {
  Transform lamp,left,right;Renderer lampRenderer;Material unlit,lit;LineRenderer charge;bool armed;float time;string id;
  public void Build(string kind)
  {
   id=kind;var steel=new Color(.32f,.40f,.48f);var rim=new Color(.66f,.74f,.8f);
   SkyArt.Oval(transform,"Half buried pressure plate",new Vector3(0,-.015f,0),new Vector3(.63f,.13f,.63f),steel);
   var ring=SkyArt.Ring(transform,"Steel rim",.28f,rim,.025f,32);ring.transform.localPosition=Vector3.up*.025f;
   for(int i=0;i<6;i++){float a=i*Mathf.PI/3;SkyArt.Oval(transform,"Rivet",new Vector3(Mathf.Cos(a)*.235f,.06f,Mathf.Sin(a)*.235f),Vector3.one*.035f,rim);}
   if(kind=="landmine")
   {
    SkyArt.Oval(transform,"Fuse collar",new Vector3(0,.06f,0),new Vector3(.18f,.08f,.18f),rim);
    lamp=SkyArt.Oval(transform,"Arming lamp",new Vector3(0,.11f,0),new Vector3(.09f,.035f,.09f),new Color(1,.1f,.035f));
    charge=SkyArt.Ring(transform,"Blinking armed ring",.115f,new Color(1,.18f,.02f),.012f,24);charge.transform.localPosition=Vector3.up*.085f;
   }
   else
   {
    left=Jaw(-1,rim);right=Jaw(1,rim);lamp=SkyArt.Oval(transform,"Pressure trigger",Vector3.up*.07f,new Vector3(.14f,.045f,.14f),new Color(.9f,.6f,.16f));
    charge=SkyArt.Ring(transform,"Armed trigger",.09f,new Color(1,.7f,.15f),.012f,20);charge.transform.localPosition=Vector3.up*.1f;
    if(kind=="trapchain")for(int i=0;i<9;i++){var link=SkyArt.Ring(transform,"Interlocking chain link",.045f,rim,.012f,10);link.transform.localPosition=new Vector3(.34f+i*.065f,.02f,Mathf.Sin(i*.6f)*.08f);link.transform.localRotation=Quaternion.Euler(i%2==0?0:65,0,0);}
   }
   lampRenderer=lamp.GetComponent<Renderer>();lit=lampRenderer.sharedMaterial;unlit=SkyArt.Mat(new Color(.17f,.10f,.08f));SetArmed(false);
  }
  Transform Jaw(int side,Color c)
  {
   var p=SkyArt.Node(transform,"Hinged toothed jaw",new Vector3(side*.25f,.03f,0));
   for(int i=0;i<7;i++){float a=(i/6f-.5f)*Mathf.PI;var v=new Vector3(side*(Mathf.Cos(a)*.22f-.25f),.03f,Mathf.Sin(a)*.22f);SkyArt.Oval(p,"Steel jaw tooth",v,new Vector3(.045f,.11f,.04f),c,true);if(i>0){float b=((i-1)/6f-.5f)*Mathf.PI;SkyArt.Rod(p,"Jaw arch",new Vector3(side*(Mathf.Cos(b)*.22f-.25f),0,Mathf.Sin(b)*.22f),new Vector3(v.x,0,v.z),.025f,c);}}
   return p;
  }
  public void SetArmed(bool value){armed=value;if(lampRenderer)lampRenderer.sharedMaterial=value?lit:unlit;Sample(0);}
  public void Sample(float dt)
  {
   time+=Mathf.Max(0,dt);if(charge){var c=id=="landmine"?new Color(1,.12f,.02f):new Color(1,.65f,.1f);c.a=armed?.35f+.65f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(time*8)),4):.06f;charge.startColor=charge.endColor=c;}
   if(lamp)lamp.localScale=Vector3.one*(armed?1:.6f);
   if(left){left.localRotation=Quaternion.Euler(0,0,armed?-16:-65);right.localRotation=Quaternion.Euler(0,0,armed?16:65);}
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
