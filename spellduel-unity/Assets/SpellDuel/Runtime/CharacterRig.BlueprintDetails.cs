using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public partial class CharacterRig
 {
  static readonly Dictionary<string,Mesh> blueprintDetailMeshes=new Dictionary<string,Mesh>();
  static void TrophyInscription(OutfitArt.Builder b)
  {
   // Vector strokes: 第一名, no font or imported texture required on either mobile platform.
   float[][] strokes={
    new[]{0f,0f,.25f,.17f},new[]{.12f,.09f,.40f,.09f},new[]{.25f,.09f,.30f,-.04f},new[]{.50f,0f,.70f,.17f},new[]{.61f,.09f,.95f,.09f},new[]{.77f,.09f,.83f,-.04f},
    new[]{.14f,-.18f,.86f,-.18f},new[]{.86f,-.18f,.86f,-.38f},new[]{.14f,-.38f,.86f,-.38f},new[]{.14f,-.38f,.14f,-.57f},new[]{.14f,-.57f,.86f,-.57f},new[]{.86f,-.57f,.86f,-.79f},new[]{.50f,-.18f,.50f,-.90f},new[]{.48f,-.59f,.11f,-.86f},
    new[]{1.12f,-.40f,1.95f,-.40f},
    new[]{2.13f,-.15f,2.36f,.12f},new[]{2.28f,0f,2.82f,0f},new[]{2.82f,0f,2.13f,-.48f},new[]{2.28f,-.13f,2.51f,-.30f},
    new[]{2.28f,-.48f,2.87f,-.48f},new[]{2.87f,-.48f,2.87f,-.86f},new[]{2.87f,-.86f,2.28f,-.86f},new[]{2.28f,-.86f,2.28f,-.48f}
   };
   foreach(var t in strokes)LineB(b,new Vector3((t[0]-1.5f)*.09f,.31f+t[1]*.09f,.215f),new Vector3((t[2]-1.5f)*.09f,.31f+t[3]*.09f,.215f),.0038f,BInk);
  }
  static Transform BlueprintDetailPart(Transform parent,string key)
  {
   if(!blueprintDetailMeshes.TryGetValue(key,out var mesh))
   {
    var b=new OutfitArt.Builder();b.Skin=new Color(.123f,.456f,.789f);
    if(key=="crest")for(int i=0;i<3;i++)Blob(b,new Vector3(0,.04f,i*.055f),new Vector3(.045f,.09f,.065f),BRed,6);
    if(key=="trees")for(int side=-1;side<=1;side+=2){LineB(b,new Vector3(side*.10f,0,.03f),new Vector3(side*.17f,.16f,.02f),.014f,new Color(.39f,.23f,.11f));Blob(b,new Vector3(side*.17f,.17f,.02f),new Vector3(.15f,.15f,.15f),BGreen,6);}
    if(key=="flowers")for(int side=-1;side<=1;side+=2)for(int j=0;j<5;j++){float a=j*Mathf.PI*2/5;Blob(b,new Vector3(side*.17f+Mathf.Cos(a)*.026f,.17f+Mathf.Sin(a)*.026f,.10f),new Vector3(.025f,.025f,.012f),new Color(1,.45f,.65f),4);}
    if(key=="jaw") {Blob(b,new Vector3(0,-.015f,.06f),new Vector3(.20f,.045f,.17f),BCream,6);for(int i=-2;i<=2;i++)b.Panel(new Vector3(i*.032f-.010f,.005f,.14f),new Vector3(i*.032f+.010f,.005f,.14f),new Vector3(i*.032f,.04f,.14f),new Vector3(i*.032f,.04f,.14f),BCream,0,1,1);}
    if(key=="muzzle"){Blob(b,Vector3.zero,new Vector3(.22f,.12f,.19f),BCream);LineB(b,new Vector3(0,-.03f,.08f),new Vector3(.13f,-.02f,.10f),.012f,BGreen);}
    if(key=="coins")for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;Blob(b,new Vector3(Mathf.Sin(a)*.065f,.25f+Mathf.Cos(a)*.065f,.31f),new Vector3(.036f,.042f,.010f),BGold,6);}
    if(key=="clockhands")for(int j=0;j<3;j++){float a=j*Mathf.PI*2/3;var c=new Vector3(Mathf.Cos(a)*.15f,.29f+Mathf.Sin(a)*.15f,.31f);LineB(b,c,c+new Vector3(.04f,.04f,0),.004f,BGold);LineB(b,c,c+new Vector3(-.025f,.02f,0),.004f,BCream);}
    mesh=b.Finish("Blueprint detail "+key);blueprintDetailMeshes[key]=mesh;
   }
   return AnimeFace.Part(parent,"Blueprint detail "+key,mesh,Vector3.zero,AnimeFace.Material(Color.white,4)).transform;
  }
  static BlueprintGearDetail BuildBlueprintDetails(Transform root,string id)
  {
   var d=root.gameObject.AddComponent<BlueprintGearDetail>();d.id=id;
   switch(id)
   {
    case "bp_chicken":d.part=BlueprintDetailPart(root,"crest");d.part.localPosition=new Vector3(0,.36f,.09f);break;
    case "bp_planter":d.part=BlueprintDetailPart(root,"trees");d.part.localPosition=new Vector3(0,-.24f,0);d.second=BlueprintDetailPart(d.part,"flowers");break;
    case "bp_shark":d.part=BlueprintDetailPart(root,"jaw");d.part.localPosition=new Vector3(0,-.38f,.17f);d.line=SpellFx.Line(root,"Shark luminous dorsal fin",new[]{new Vector3(0,-.29f,.05f),new Vector3(0,-.16f,.01f),new Vector3(0,-.29f,-.02f)},false,.009f,new Color(.3f,.9f,1,.6f),true,false);break;
    case "bp_luckycat":BlueprintDetailPart(root,"coins");break;
    case "bp_hourglass":d.part=BlueprintDetailPart(root,"clockhands");d.stream=BlueprintGearDetail.Particles(root,"Upward sand",new Vector3(0,.17f,.30f),BGold,.006f,12,.13f,1.8f,6);break;
    case "bp_camel":d.part=BlueprintDetailPart(root,"muzzle");d.part.localPosition=new Vector3(0,.28f,.35f);d.stream=BlueprintGearDetail.Particles(root,"Camel spit",new Vector3(0,.25f,.45f),new Color(.8f,1,.8f,.6f),.018f,5,.45f,.55f,0);d.stream.transform.localRotation=Quaternion.Euler(90,0,0);break;
    case "bp_jelly":d.line=SpellFx.Line(root,"Tentacle lightning",new Vector3[6],false,.008f,new Color(.65f,.75f,1,.8f),true,false);break;
    case "bp_neonhalo":d.line=SpellFx.Line(root,"Neon chasing segment",new Vector3[7],false,.021f,Color.white,true,false);break;
    case "bp_furnace":d.stream=BlueprintGearDetail.Particles(root,"Chimney smoke",new Vector3(.17f,.70f,-.19f),new Color(.35f,.37f,.42f,.36f),.07f,6,.15f,1.5f,3);break;
    case "bp_skewer":d.stream=BlueprintGearDetail.Particles(root,"Dripping hot oil",new Vector3(0,1.18f,0),new Color(1,.6f,.12f,.7f),.012f,5,.02f,.7f,3);var oil=d.stream.main;oil.gravityModifier=.2f;break;
    case "bp_bamboo":d.stream=BlueprintGearDetail.Particles(root,"Wind swept bamboo leaves",new Vector3(.22f,.25f,0),BGreen,.04f,10,.7f,.6f,0);break;
   }
   return d;
  }
  /// <summary>Visual-only impact notification; caller owns hit timing and world/AR conversion.</summary>
  public void BlueprintImpact(Vector3 atWorld)
  {
   if(gear==null||gear[Slot.Weapon].special!="bp_corn")return;
   var go=new GameObject("Popped corn impact");go.transform.position=atWorld;
   var ps=BlueprintGearDetail.Particles(go.transform,"Popcorn",Vector3.zero,BCream,.045f,14,1.4f,.7f,0);var main=ps.main;main.gravityModifier=.25f;SpellFx.Sphere(ps,.06f);ps.Emit(12);Destroy(go,1.1f);
  }
 }
 public sealed class BlueprintGearDetail:MonoBehaviour
 {
  internal string id;internal Transform part,second;internal LineRenderer line;internal ParticleSystem stream;
  float time,lastHit,lastCharge,lastSpit;bool lastAttack;AudioSource audioSource;static readonly Dictionary<string,AudioClip> sounds=new Dictionary<string,AudioClip>();
  internal static ParticleSystem Particles(Transform parent,string name,Vector3 at,Color c,float size,int max,float speed,float life,float rate)
  {
   var ps=SpellFx.NewPS(parent,name,SpellFx.BlendMat(SpellFx.TexSoft),max,false);ps.transform.localPosition=at;ps.transform.localRotation=Quaternion.Euler(-90,0,0);
   var m=ps.main;m.startColor=c;m.startSize=size;m.startSpeed=speed;m.startLifetime=life;SpellFx.Cone(ps,12,.012f);SpellFx.FadeOut(ps);SpellFx.Rate(ps,rate);ps.Play();return ps;
  }
  void Sound(string key)
  {
   if(!Application.isPlaying||!Sfx.On)return;
   if(!sounds.TryGetValue(key,out var clip))
   {
    const int rate=22050;int count=rate/3;var data=new float[count];float phase=0;
    for(int i=0;i<count;i++){float t=i/(float)rate;float freq=key=="cluck"?330+130*Mathf.Sin(t*45):660+85*Mathf.Sin(t*33);phase+=freq*Mathf.PI*2/rate;data[i]=Mathf.Sin(phase)*Mathf.Sin(Mathf.PI*i/count)*.25f*(key=="cluck"?Mathf.Max(0,Mathf.Sin(t*65)):1);}
    clip=AudioClip.Create("Procedural blueprint "+key,count,1,rate,false);clip.SetData(data,0);sounds[key]=clip;
   }
   if(!audioSource){audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;audioSource.maxDistance=8;}
   audioSource.PlayOneShot(clip,Sfx.Volume*.35f);
  }
  public void Sample(float dt,CharacterRig rig)
  {
   time+=Mathf.Max(0,dt);float hit=rig?rig.BlueprintHitAmount:0,move=rig?Mathf.Clamp01(rig.MoveSpeed):0,charge=rig?rig.Charge:0;bool attack=rig&&rig.Attacking;
   if(id=="bp_chicken"&&part){part.localScale=new Vector3(1,1+hit*.7f,1);if(hit>lastHit+.2f)Sound("cluck");}
   if(id=="bp_planter"&&part){part.localRotation=Quaternion.Euler(7*Mathf.Sin(time*7)*move,0,9*Mathf.Sin(time*5)*move);second.localScale=Vector3.one*Mathf.SmoothStep(.05f,1,.5f+.5f*Mathf.Sin(time*.8f));}
   if(id=="bp_shark"&&part){float lift=rig?Mathf.Clamp01(rig.BlueprintLift/.12f):0;part.localRotation=Quaternion.Euler(25*lift*(.5f+.5f*Mathf.Sin(time*24)),0,0);}
   if(id=="bp_camel"&&part){part.localRotation=Quaternion.Euler(0,0,5*Mathf.Sin(time*4));if(time-lastSpit>7){lastSpit=time;if(stream)stream.Emit(3);}}
   if(id=="bp_hourglass"&&part)part.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*.8f)*12);
   if(id=="bp_jelly"&&line){for(int i=0;i<6;i++)line.SetPosition(i,new Vector3((i-2.5f)*.045f,.82f+.045f*Mathf.Sin(time*23+i*3),.04f));line.enabled=Mathf.Sin(time*17)>.45f;}
   if(id=="bp_neonhalo"&&line){for(int i=0;i<7;i++){float a=time*2+i*.1f;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.25f,.34f,Mathf.Sin(a)*.25f));}var c=Color.HSVToRGB(Mathf.Repeat(time*.12f,1),.5f,1);line.startColor=line.endColor=c;}
   if(id=="bp_bamboo"){if(charge>0&&lastCharge<=0)Sound("flute");if(attack&&!lastAttack&&stream)stream.Emit(8);}
   lastHit=hit;lastCharge=charge;lastAttack=attack;
  }
 }
}
