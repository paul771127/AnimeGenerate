using UnityEngine;
namespace SpellDuel
{
 /// <summary>Root is the caller's stable flight anchor; local +Z is the bow / direction toward the carpet.</summary>
 public sealed class AirshipRig : MonoBehaviour
 {
  Transform ship; readonly Transform[] doors=new Transform[12],barrels=new Transform[12],pirates=new Transform[3];
  readonly float[] drops={10,10,10}; readonly Vector3[] dropStart=new Vector3[3];
  ParticleSystem smoke,flash; int phase=1,side=1,move=-1;float gap,warningAge=10,warningDuration=1,fireAge=10,swoop,dying=-1,clock;
  public static AirshipRig Create(){var r=new GameObject("Airship Zhuifeng").AddComponent<AirshipRig>();r.Build();return r;}
  void Build()
  {
   ship=SkyArt.Node(transform,"Flying ship visual",Vector3.zero);
   var red=new Color(.79f,.18f,.29f);var wood=new Color(.53f,.3f,.2f);
   SkyArt.Oval(ship,"Aerodynamic hull",new Vector3(0,0,0),new Vector3(5.8f,2.2f,9),wood,true).localRotation=Quaternion.Euler(90,0,0);
   SkyArt.Oval(ship,"Ivory deck",new Vector3(0,.8f,0),new Vector3(5.4f,.3f,8.2f),SkyArt.Cream);
   SkyArt.Oval(ship,"Red white air envelope",new Vector3(0,5,-1),new Vector3(6.4f,4.4f,10),red);
   for(int i=0;i<7;i++)
   {var ring=SkyArt.Ring(ship,"Balloon reinforcement",1,SkyArt.Cream,.08f);ring.transform.localPosition=new Vector3(0,5,-4+i);ring.transform.localRotation=Quaternion.Euler(90,0,0);ring.transform.localScale=new Vector3(2.8f,1,2.05f);}
   for(int s=-1;s<=1;s+=2)
   {for(int j=0;j<3;j++)SkyArt.Rod(ship,"Suspension cable",new Vector3(s*2.1f,.8f,-3+j*2.8f),new Vector3(s*2.2f,4,-3+j*2.8f),.06f,SkyArt.Gold);
    SkyArt.Rod(ship,"Deck safety rail",new Vector3(s*2.45f,1.5f,-3),new Vector3(s*2.45f,1.5f,3),.075f,SkyArt.Gold);
    for(int j=0;j<5;j++)SkyArt.Rod(ship,"Rail post",new Vector3(s*2.45f,.8f,-3+j*1.5f),new Vector3(s*2.45f,1.5f,-3+j*1.5f),.055f,SkyArt.Gold);
    SkyArt.Oval(ship,"Boarding platform",new Vector3(s*2.7f,.85f,1),new Vector3(1,.15f,1.5f),wood);
    SkyArt.Rod(ship,"Boarding rope",new Vector3(s*2.7f,1,1),new Vector3(s*2.7f,-1.5f,1),.045f,SkyArt.Cream);
    var tail=SkyArt.Sail(ship,"Rudder canvas",new Vector3(s*1.4f,1,-4),new Vector3(1.5f,2.4f,1),red);tail.localRotation=Quaternion.Euler(0,s*30,0);}
   SkyArt.Oval(ship,"Reachable bow anchor",new Vector3(0,-.7f,4.1f),new Vector3(2.2f,.5f,1.3f),SkyArt.Gold);
   SkyArt.Ring(ship,"Anchor target",.45f,SkyArt.Teal,.06f).transform.SetLocalPositionAndRotation(new Vector3(0,-.5f,4.6f),Quaternion.Euler(90,0,0));
   for(int i=0;i<12;i++)
   {float x=(i%6-2.5f)*.72f,y=i<6?-.35f:.4f;
    barrels[i]=SkyArt.Node(ship,"Recoil barrel "+i,new Vector3(x,y,3.7f));SkyArt.Rod(barrels[i],"Brass cannon",Vector3.zero,Vector3.forward*.7f,.32f,SkyArt.Gold);SkyArt.Oval(barrels[i],"Dark bore",new Vector3(0,0,.71f),new Vector3(.22f,.22f,.03f),SkyArt.Ink);
    doors[i]=SkyArt.Node(ship,"Hinged gun port "+i,new Vector3(x,y+.28f,4.05f));SkyArt.Oval(doors[i],"Port shield",new Vector3(0,-.22f,0),new Vector3(.58f,.48f,.10f),red);}
   for(int i=0;i<3;i++)
   {pirates[i]=SkyArt.Node(ship,"Deck pirate "+i,new Vector3((i-1)*1.4f,1.2f,1.8f));dropStart[i]=pirates[i].localPosition;SkyArt.Oval(pirates[i],"Coat",Vector3.zero,new Vector3(.35f,.55f,.3f),red);SkyArt.Oval(pirates[i],"Head",Vector3.up*.42f,Vector3.one*.25f,SkyArt.Cream);SkyArt.Oval(pirates[i],"Hat",Vector3.up*.54f,new Vector3(.4f,.1f,.3f),SkyArt.Ink);}
   smoke=SpellFx.NewPS(ship,"Phase two stack smoke",SpellFx.BlendMat(SpellFx.TexSoft),60,false);smoke.transform.localPosition=new Vector3(0,2,-2);var sm=smoke.main;sm.startColor=new Color(.2f,.2f,.27f,.4f);sm.startSize=1.2f;sm.startLifetime=2;sm.startSpeed=.6f;var se=smoke.emission;se.rateOverTime=0;
   flash=SpellFx.NewPS(ship,"Cannon sparks",SpellFx.BlendMat(SpellFx.TexSoft),48,false);var fm=flash.main;fm.startLifetime=.25f;fm.startSize=.16f;fm.startSpeed=1.3f;var fe=flash.emission;fe.enabled=false;
  }
  public void SetPhase(int value){phase=value>=2?2:1;var e=smoke.emission;e.rateOverTime=phase==2?18:0;if(phase==2)smoke.Play();else smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
  public void SetSide(int value){side=value<0?-1:1;}
  /// <summary>Safe gap centre, metres in ship-local X (positive toward the ship's right).</summary>
  public void SetGap(float value){gap=Mathf.Clamp(value,-1.8f,1.8f);}
  public void Warn(int value,float dur){move=Mathf.Clamp(value,0,4);warningAge=0;warningDuration=Mathf.Max(.05f,dur);}
  bool Selected(int i,int m){float x=(i%6-2.5f)*.72f;return m==4||m==0&&i<6||m==1&&i>=6||m==2&&x*side>0||m==3&&Mathf.Abs(x-gap)>.6f;}
  public void Fire(int value)
  {move=Mathf.Clamp(value,0,4);fireAge=0;warningAge=warningDuration;
   for(int i=0;i<12;i++)if(Selected(i,move)){flash.transform.localPosition=barrels[i].localPosition+Vector3.forward*.75f;flash.Emit(3);}
  }
  public void SetSwoop(float t){swoop=Mathf.Clamp01(t);Pose();}
  public void DropPirate(int value){int i=value<0?0:value>0?2:1;drops[i]=0;pirates[i].gameObject.SetActive(true);}
  public void SetDying(float t){dying=Mathf.Clamp01(t);Pose();if(t>0&&!smoke.isPlaying)smoke.Play();var e=smoke.emission;e.rateOverTime=40;}
  void Pose(){ship.localPosition=new Vector3(0,-2*swoop,8*swoop)+(dying>=0?new Vector3(0,-dying*9,-dying*5):Vector3.zero);ship.localRotation=Quaternion.Euler(dying>=0?dying*65:Mathf.Sin(clock)*1.5f,0,dying>=0?dying*25:Mathf.Sin(clock*.7f));ship.localScale=Vector3.one*(dying>=0?Mathf.Lerp(1,.7f,dying):1);}
  public void Sample(float dt)
  {
   clock+=dt;warningAge+=dt;fireAge+=dt;Pose();float w=Mathf.Clamp01(warningAge/warningDuration);
   for(int i=0;i<12;i++){bool selected=move>=0&&Selected(i,move);float open=phase==2?1:selected?w:0;doors[i].localRotation=Quaternion.Euler(-105*open,0,0);var p=barrels[i].localPosition;p.z=3.7f-(selected&&fireAge<.3f?.22f*Mathf.Sin(fireAge/.3f*Mathf.PI):0);barrels[i].localPosition=p;}
   for(int i=0;i<3;i++){drops[i]+=dt;if(drops[i]<1){float t=drops[i];pirates[i].localPosition=Vector3.Lerp(dropStart[i],new Vector3((i-1)*.8f,-2,9),t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*2;pirates[i].localRotation=Quaternion.Euler(t*25,0,0);}else{pirates[i].localPosition=dropStart[i];pirates[i].localRotation=Quaternion.identity;}}
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
