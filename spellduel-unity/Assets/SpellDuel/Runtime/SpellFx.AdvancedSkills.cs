using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  static bool IsDedicatedSkillProjectile(string id){switch(id){case "arcanewyrm":case "starfall":case "arcanebreak":case "holyblade":case "headshot":case "scattershot":case "blastarrow":case "spiritbolt":case "earthspike":case "suiton":case "futon":case "poisondart":case "lightpillar":case "holyfire":case "verdict":case "rollarrow":case "echo_fire":case "echo_ice":return true;default:return false;}}
  static void AdvancedProjectile(GameObject root,string id,Color c){root.AddComponent<AdvancedProjectileArt>().Build(id,c);}
  public static void AreaBurst(string skillId,Vector3 centerWorld,float radius)
  {
   if(!AreaSkillArt.Known(skillId))return;var go=new GameObject("SpellFx area "+skillId);go.transform.position=centerWorld;go.AddComponent<AreaSkillArt>().Build(skillId,Mathf.Max(.05f,radius));
  }
  public static void SkillImpact(string skillId,Vector3 atWorld)
  {
   if(skillId=="arcanebreak"){ArcaneBreakHit(atWorld);return;}
   var c=SkillTint(skillId);var a=NewAnim("SpellFx impact "+skillId,atWorld,Quaternion.identity,.8f);
   if(skillId=="headshot"){for(int i=0;i<4;i++)a.AddSlash(a.transform,new[]{Vector3.up*.05f,Vector3.up*.35f},.025f,c,0,.23f,.2f,1,i*90);Sparks(a.transform,c,.5f,12,1,2.5f);}
   else if(skillId=="poisondart"){var ps=NewPS(a.transform,"Poison impact mist",BlendMat(TexSoft),12,false);var m=ps.main;m.startLifetime=.7f;m.startSize=.23f;m.startColor=new Color(.3f,.8f,.2f,.35f);m.startSpeed=.4f;Sphere(ps,.07f);FadeOut(ps);ps.Play();ps.Emit(10);}
   else if(skillId=="verdict")
   {for(int j=0;j<7;j++){float ang=j*Mathf.PI*2/7;var p=new Vector3[5];for(int i=0;i<5;i++){float a0=ang+Mathf.Sin(i*3+j)*.16f;p[i]=new Vector3(Mathf.Cos(a0)*i*.17f,.02f,Mathf.Sin(a0)*i*.17f);}a.AddArc(a.transform,p,.022f,c,0,.65f,.3f,1,0);}}
   else Sparks(a.transform,c,.5f,10,.5f,1.5f);
  }
 }
 public sealed class AdvancedProjectileArt:MonoBehaviour
 {
  string id;float age;Transform motif;LineRenderer[] lines;Color tint;
  public void Build(string skill,Color c)
  {
   id=skill;tint=c;motif=SkyArt.Node(transform,"Dedicated "+id,Vector3.zero);var ls=new List<LineRenderer>();
   if(id=="holyblade"||id=="futon")
   {
    for(int j=0;j<3;j++){var p=new Vector3[25];for(int i=0;i<25;i++){float a=(i/24f-.5f)*Mathf.PI*1.3f;p[i]=new Vector3(Mathf.Sin(a)*(.48f-j*.025f),j*.014f,Mathf.Cos(a)*.22f-j*.07f);}ls.Add(SpellFx.Line(motif,"Tapered crescent blade",p,false,j==0?.055f:.014f,new Color(c.r,c.g,c.b,j==0?.8f:.4f),true,false));ls[ls.Count-1].widthCurve=SpellFx.ArcTaper;}
   }
   else if(id=="spiritbolt")
   {
    SkyArt.Oval(motif,"Little soul head",Vector3.zero,new Vector3(.19f,.22f,.20f),new Color(.72f,.57f,.96f));
    for(int s=-1;s<=1;s+=2)SkyArt.Oval(motif,"Soul eye",new Vector3(s*.04f,.025f,.091f),new Vector3(.025f,.044f,.018f),new Color(.17f,.10f,.3f));
    SkyArt.Oval(motif,"Soul mouth",new Vector3(0,-.04f,.095f),new Vector3(.025f,.03f,.015f),new Color(.24f,.1f,.32f));
    ls.Add(SpellFx.Line(motif,"Ghost wisp tail",new Vector3[12],false,.10f,c,true,false));ls[0].widthCurve=AnimationCurve.Linear(0,1,1,0);
   }
   else if(id=="verdict")
   {
    SkyArt.Rod(motif,"Luminous warhammer handle",new Vector3(0,-.55f,0),new Vector3(0,.12f,0),.07f,new Color(1,.84f,.43f));
    SkyArt.Oval(motif,"Barrel hammer head",new Vector3(0,.13f,0),new Vector3(.66f,.27f,.24f),new Color(1,.9f,.62f));
    for(int side=-1;side<=1;side+=2){var ring=SpellFx.Line(motif,"Hammer striking face",SpellFx.Circle(24,.14f),true,.025f,c,true,false);ring.transform.localPosition=new Vector3(side*.32f,.13f,0);ring.transform.localRotation=Quaternion.Euler(0,90,0);}
    ls.Add(SpellFx.Line(motif,"Hammer holy cross",new[]{new Vector3(0,.03f,.13f),new Vector3(0,.23f,.13f)},false,.025f,Color.white,true,false));ls.Add(SpellFx.Line(motif,"Hammer cross arms",new[]{new Vector3(-.09f,.13f,.13f),new Vector3(.09f,.13f,.13f)},false,.025f,Color.white,true,false));
   }
   else if(id=="lightpillar")
   {
    for(int j=-1;j<=1;j++)ls.Add(SpellFx.Line(motif,"Narrow descending holy ray",new[]{new Vector3(j*.045f,0,-1.3f),new Vector3(j*.045f,0,.15f)},false,j==0?.055f:.012f,j==0?Color.white:c,true,false));
   }
   else if(id=="echo_ice")
   {
    var ice=SkyArt.Oval(motif,"Small echoed ice lance",Vector3.zero,new Vector3(.13f,.52f,.13f),new Color(.55f,.9f,1),true);ice.localRotation=Quaternion.Euler(90,0,0);
    ls.Add(SpellFx.Line(motif,"Ice crystalline seam",new[]{Vector3.back*.22f,Vector3.forward*.25f},false,.014f,Color.white,true,false));
   }
   else if(id=="holyfire"||id=="echo_fire")
   {
    for(int j=0;j<5;j++){var flame=SkyArt.Oval(motif,"Sacred flame tongue",new Vector3(Mathf.Sin(j*2.4f)*.07f,Mathf.Cos(j*2.4f)*.07f,-j*.035f),new Vector3(.12f,.12f,.33f-j*.02f),j%2==0?c:new Color(1,.92f,.55f),true);flame.localRotation=Quaternion.Euler(90,0,0);}
   }
   else if(id=="earthspike"||id=="suiton")
   {var ring=SkyArt.Ring(motif,"Target impact sigil",.35f,c,.025f,32);ring.transform.localRotation=Quaternion.Euler(90,0,0);ls.Add(ring);}
   else // One visual per real arrow/dart, no duplicate fake projectiles.
   {
    float length=id=="headshot"?1.2f:id=="poisondart"?.32f:.62f;
    ls.Add(SpellFx.Line(motif,"Arrow luminous shaft",new[]{Vector3.back*length,Vector3.forward*.05f},false,id=="headshot"?.013f:.025f,c,true,false));
    var tip=SkyArt.Sail(motif,"Arrowhead",Vector3.forward*.02f,new Vector3(id=="poisondart"?.09f:.14f,.20f,1),c);tip.localRotation=Quaternion.Euler(90,0,0);
    if(id!="headshot")for(int s=-1;s<=1;s+=2)ls.Add(SpellFx.Line(motif,"Fletching",new[]{new Vector3(s*.09f,0,-length+.08f),new Vector3(0,0,-length+.2f)},false,.025f,c,true,false));
    if(id=="blastarrow")for(int j=0;j<3;j++)ls.Add(SpellFx.Line(motif,"Burning arrow fuse",new[]{new Vector3(0,j*.02f,-.12f),new Vector3(.06f,j*.02f,-.3f),new Vector3(0,j*.02f,-.5f)},false,.025f,new Color(1,.65f,.1f,.7f),true,false));
   }
   if(id=="rollarrow")for(int j=0;j<2;j++){var p=new Vector3[20];for(int i=0;i<20;i++){float a=i/19f*Mathf.PI*1.5f+j*Mathf.PI;p[i]=new Vector3(Mathf.Cos(a)*.13f,Mathf.Sin(a)*.13f,-i*.03f);}ls.Add(SpellFx.Line(motif,"Rolling green afterimage",p,false,.012f,new Color(.35f,1,.6f,.4f),true,false));}
   lines=ls.ToArray();if(id=="echo_fire"||id=="echo_ice")motif.localScale=Vector3.one*.65f;
   if(id.StartsWith("echo_")){var ring=SpellFx.Line(motif,"Arcane echo orbit",SpellFx.Circle(32,.18f),true,.012f,new Color(.8f,.4f,1),true,false);ring.transform.localRotation=Quaternion.Euler(35,50,0);}
  }
  public void Sample(float dt)
  {
   age+=Mathf.Max(0,dt);if(id=="spiritbolt")for(int i=0;i<12;i++){float u=i/11f;lines[0].SetPosition(i,new Vector3(Mathf.Sin(age*12-u*5)*.08f*u,Mathf.Cos(age*9-u*6)*.04f*u,-u*.5f));}
   if(id=="holyfire"||id=="echo_fire")motif.localRotation=Quaternion.Euler(0,0,age*140);
   if(id=="holyblade"||id=="futon")motif.localRotation=Quaternion.Euler(0,0,Mathf.Sin(age*8)*8);
   if(id=="rollarrow")motif.localRotation=Quaternion.Euler(0,0,age*360);
  }
  void Update(){Sample(Time.deltaTime);}
 }
 public sealed class AreaSkillArt:MonoBehaviour
 {
  public static bool Known(string id)=>id=="consecrate"||id=="blastarrow"||id=="earthspike"||id=="suiton"||id=="bladestorm"||id=="holynova"||id=="arcaneburst"||id=="venom_burst"||id=="starfall";
  string id;float age,radius;Color tint;LineRenderer[] rings;Transform[] pieces;Vector3[] sizes;LineRenderer[] cage;
  public void Build(string skill,float r)
  {
   id=skill;radius=r;tint=SpellFx.SkillTint(id);rings=new LineRenderer[2];for(int i=0;i<2;i++){rings[i]=SkyArt.Ring(transform,"Expanding damage boundary",1,tint,i==0?.035f:.015f,64);rings[i].transform.localPosition=Vector3.up*(.025f+i*.01f);}
   var list=new List<Transform>();
   if(id=="earthspike")for(int i=0;i<10;i++){float a=i*Mathf.PI/5;var p=SkyArt.Oval(transform,"Rising stone fang",new Vector3(Mathf.Cos(a)*r*.7f,0,Mathf.Sin(a)*r*.7f),new Vector3(.22f,.8f+i%3*.15f,.24f),new Color(.52f,.42f,.27f),true);p.localRotation=Quaternion.Euler(Mathf.Cos(a)*18,0,180-Mathf.Sin(a)*18);list.Add(p);}
   if(id=="suiton")
   {cage=new LineRenderer[10];for(int j=0;j<10;j++){var p=new Vector3[18];float a=j*Mathf.PI/5;for(int i=0;i<18;i++){float u=i/17f;p[i]=new Vector3(Mathf.Cos(a)*r*Mathf.Sin(u*Mathf.PI),u*1.9f,Mathf.Sin(a)*r*Mathf.Sin(u*Mathf.PI));}cage[j]=SpellFx.Line(transform,"Water prison rib",p,false,.045f,new Color(.3f,.85f,1,.6f),true,false);}}
   if(id=="bladestorm")
   {cage=new LineRenderer[6];for(int j=0;j<6;j++){var p=new Vector3[24];for(int i=0;i<24;i++){float a=i/23f*Mathf.PI*.8f+j; p[i]=new Vector3(Mathf.Cos(a)*r,.15f+j*.17f,Mathf.Sin(a)*r);}cage[j]=SpellFx.Line(transform,"Whirling knife crescent",p,false,.045f,tint,true,false);cage[j].widthCurve=SpellFx.ArcTaper;}}
   if(id=="consecrate")
   {cage=new LineRenderer[8];for(int j=0;j<8;j++){float a=j*Mathf.PI/4;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-d.z,0,d.x);var center=d*r*.7f+Vector3.up*.03f;cage[j]=SpellFx.Line(transform,"Holy seal cross",new[]{center-d*.2f,center+d*.2f,center,center+side*.12f,center-side*.12f},false,.02f,tint,true,false);}}
   if(id=="blastarrow"||id=="starfall"||id=="arcaneburst"||id=="venom_burst"||id=="holynova")
   {
    var ps=SpellFx.NewPS(transform,"Radial elemental bloom",id=="venom_burst"?SpellFx.BlendMat(SpellFx.TexSoft):SpellFx.AddMat(SpellFx.TexSoft),32,false);var m=ps.main;m.startLifetime=.65f;m.startSpeed=new ParticleSystem.MinMaxCurve(.4f,r*1.5f);m.startSize=id=="venom_burst"?.38f:.12f;m.startColor=tint;SpellFx.Sphere(ps,.05f);SpellFx.FadeOut(ps);ps.Play();ps.Emit(28);
   }
   pieces=list.ToArray();sizes=new Vector3[pieces.Length];for(int i=0;i<pieces.Length;i++)sizes[i]=pieces[i].localScale;Sample(0);
  }
  public void Sample(float dt)
  {
   age+=Mathf.Max(0,dt);float u=Mathf.Clamp01(age/.65f),expand=1-Mathf.Pow(1-u,3);var c=tint;c.a=.8f*(1-u);
   for(int i=0;i<rings.Length;i++){rings[i].transform.localScale=Vector3.one*Mathf.Max(.01f,radius*Mathf.Clamp01(expand-i*.08f));rings[i].startColor=rings[i].endColor=c;}
   float height=Mathf.Sin(Mathf.Clamp01(age/.9f)*Mathf.PI);for(int i=0;i<pieces.Length;i++){pieces[i].localScale=new Vector3(sizes[i].x,sizes[i].y*height,sizes[i].z);var p=pieces[i].localPosition;p.y=sizes[i].y*height*.5f;pieces[i].localPosition=p;}
   if(cage!=null)foreach(var line in cage){line.startColor=line.endColor=c;if(id=="bladestorm")line.transform.localRotation=Quaternion.Euler(0,age*480,0);if(id=="suiton")line.transform.localScale=new Vector3(1,Mathf.Min(1,age*7),1);}
   if(age>1&&Application.isPlaying)Destroy(gameObject);
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
