using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  static void NewSkillProjectile(GameObject root,string id,Color c){root.AddComponent<NewSkillProjectileArt>().Build(id,c);}
  public static void StarfallZone(Vector3 centerWorld,float radius,float seconds)
  {
   var go=new GameObject("SpellFx Starfall zone");go.transform.position=centerWorld;go.AddComponent<StarfallZoneArt>().Build(Mathf.Max(.05f,radius),Mathf.Max(.05f,seconds));
  }
  public static void ArcaneBreakHit(Vector3 atWorld)
  {
   var a=NewAnim("SpellFx ArcaneBreak implosion",atWorld,Quaternion.identity,.9f);var c=SkillTint("arcanebreak");
   for(int i=0;i<3;i++){var p=SkyArt.Node(a.transform,"Unstable spatial lens",Vector3.zero);p.localRotation=Quaternion.Euler(i*58,i*35,15);a.AddRing(p,Circle(48,1),true,.035f,c,0,.18f,.7f,.03f);a.AddRing(p,Circle(48,1),true,.018f,new Color(1,.8f,1),.18f,.5f,.05f,1.1f);}
   for(int i=0;i<12;i++){float angle=i*Mathf.PI/6;var p=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);a.AddSlash(a.transform,new[]{p*.12f,p*.7f},.04f,c,.18f,.4f,.1f,1.5f,i*11);}
   var dust=NewPS(a.transform,"Delayed arcane fragments",AddMat(TexStar),24,false);var main=dust.main;main.loop=false;main.startLifetime=.45f;main.startSpeed=new ParticleSystem.MinMaxCurve(1.4f,3.2f);main.startSize=.045f;main.startColor=c;Sphere(dust,.08f);FadeOut(dust);var emission=dust.emission;emission.SetBursts(new[]{new ParticleSystem.Burst(.18f,(short)24)});dust.Play();
  }
 }
 public sealed class NewSkillProjectileArt:MonoBehaviour
 {
  string id;Color tint;Transform core;Transform[] bits;LineRenderer[] paths;float age;
  public void Build(string skill,Color c)
  {
   id=skill;tint=c;
   if(id=="starfall")
   {
    bits=new Transform[7];paths=new LineRenderer[7];
    for(int i=0;i<7;i++){float a=i*2.4f,r=i==0?0:.22f+(.12f*(i%3));var at=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,-(i%3)*.15f);bits[i]=SkyArt.Oval(transform,"Individual falling star",at,new Vector3(.095f,.09f,.17f),c,true);bits[i].localRotation=Quaternion.Euler(90,0,0);paths[i]=SpellFx.Line(transform,"Meteor hot tail",new[]{at,at-Vector3.forward*(.35f+i*.06f)},false,.028f,new Color(1,.8f,.5f,.8f),true,false);}
   }
   else
   {
    core=SkyArt.Oval(transform,"Compressed arcane core",Vector3.zero,Vector3.one*.25f,new Color(.25f,.035f,.34f));bits=new Transform[3];paths=new LineRenderer[12];
    for(int i=0;i<3;i++){var ring=SpellFx.Line(transform,"Gyroscopic compression seal",SpellFx.Circle(40,.23f),true,.016f,c,true,false);bits[i]=ring.transform;bits[i].localRotation=Quaternion.Euler(i*57,i*83,0);}
    for(int i=0;i<12;i++)paths[i]=SpellFx.Line(transform,"Infalling arcane filament",new Vector3[4],false,.01f,c,true,false);
   }
   Sample(0);
  }
  public void Sample(float dt)
  {
   age+=Mathf.Max(0,dt);
   if(id=="starfall"){for(int i=0;i<bits.Length;i++)bits[i].localScale=new Vector3(.095f,.09f,.17f)*(1+.1f*Mathf.Sin(age*18+i));return;}
   core.localScale=Vector3.one*(.25f+.012f*Mathf.Sin(age*31));
   for(int i=0;i<bits.Length;i++)bits[i].localRotation=Quaternion.Euler(i*57+age*60,i*83+age*90,age*30);
   for(int i=0;i<paths.Length;i++){float a=i*Mathf.PI/6+age*.3f;float r=.22f+Mathf.Repeat(i*.0833f-age*1.1f,1)*.37f;for(int k=0;k<4;k++){float u=k/3f;paths[i].SetPosition(k,new Vector3(Mathf.Cos(a+u*.35f)*(r+u*.13f),Mathf.Sin(a+u*.35f)*(r+u*.13f),Mathf.Sin(i*2.7f)*.1f));}}
  }
  void Update(){Sample(Time.deltaTime);}
 }
 public sealed class StarfallZoneArt:MonoBehaviour
 {
  float age,life,radius;LineRenderer ring,star,pulse;LineRenderer[] ticks;
  public void Build(float r,float seconds)
  {
   radius=r;life=seconds;var c=SpellFx.SkillTint("starfall");ring=SkyArt.Ring(transform,"Seven metre perimeter",r,c,.025f,64);ring.transform.localPosition=Vector3.up*.025f;
   var p=new Vector3[6];for(int i=0;i<6;i++){float a=i*Mathf.PI*4/5;p[i]=new Vector3(Mathf.Cos(a)*r*.88f,.03f,Mathf.Sin(a)*r*.88f);}star=SpellFx.Line(transform,"Five point star warning",p,false,.018f,c,true,false);
   pulse=SkyArt.Ring(transform,"Three successive impact waves",1,c,.035f,64);pulse.transform.localPosition=Vector3.up*.04f;
   ticks=new LineRenderer[12];for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var v=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));ticks[i]=SpellFx.Line(transform,"Astral range notch",new[]{v*r*.94f+Vector3.up*.03f,v*r+Vector3.up*.03f},false,.025f,c,true,false);}
  }
  public void Sample(float dt){age+=Mathf.Max(0,dt);float alpha=Mathf.Min(1,(life-age)*4);var c=SpellFx.SkillTint("starfall");c.a=Mathf.Max(0,alpha)*(.4f+.2f*Mathf.Sin(age*10));ring.startColor=ring.endColor=star.startColor=star.endColor=c;int wave=Mathf.FloorToInt(age/.6f);float phase=Mathf.Repeat(age,.6f)/.6f;pulse.enabled=wave<3;pulse.transform.localScale=Vector3.one*radius*(1-Mathf.Pow(1-phase,3));var flash=c;flash.a=(1-phase)*(wave==2?.95f:.55f);pulse.startColor=pulse.endColor=flash;pulse.widthMultiplier=wave==2?.07f:.035f;foreach(var t in ticks)t.startColor=t.endColor=c;if(age>=life&&Application.isPlaying)Destroy(gameObject);}
  void Update(){Sample(Time.deltaTime);}
 }
}
