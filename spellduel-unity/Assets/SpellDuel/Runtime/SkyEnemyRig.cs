using UnityEngine;
namespace SpellDuel
{
 public sealed class SkyEnemyRig : MonoBehaviour
 {
  public float MoveSpeed {get;set;}
  public int Kind {get;private set;}
  Transform body,head,armL,armR,legL,legR,cloth; Vector3 baseBody; float age,wind,windDuration=.1f,strike=10,hit,death;bool winding,dead;
  Renderer[] renderers; MaterialPropertyBlock block;
  static readonly Color[] Colors={new Color(.91f,.25f,.27f),new Color(.2f,.43f,.85f),new Color(1,.66f,.2f),new Color(.2f,.75f,.67f),new Color(.63f,.27f,.8f)};
  public static SkyEnemyRig Create(int kind)
  {var r=new GameObject("SkyEnemy "+kind).AddComponent<SkyEnemyRig>();r.Kind=Mathf.Clamp(kind,0,4);r.Build();r.renderers=r.GetComponentsInChildren<Renderer>();r.block=new MaterialPropertyBlock();return r;}
  void Build()
  {
   var c=Colors[Kind];float scale=Kind==4?1.25f:1;body=SkyArt.Node(transform,"New sky skeleton",new Vector3(0,.65f*scale,0));baseBody=body.localPosition;body.localScale=Vector3.one*scale;
   if(Kind==3)
   {
    SkyArt.Oval(body,"Ceramic belly",Vector3.zero,new Vector3(.65f,.55f,.48f),SkyArt.Cream);SkyArt.Ring(body,"Floating gyroscope",.42f,c,.065f);
    head=SkyArt.Oval(body,"Pilot core",new Vector3(0,.35f,.05f),new Vector3(.32f,.32f,.3f),c);
    for(int i=0;i<3;i++){var t=SkyArt.Node(body,"Rotor leaf "+i,Vector3.zero);t.localRotation=Quaternion.Euler(0,i*120,0);SkyArt.Sail(t,"Jade rotor",new Vector3(0,.05f,.23f),new Vector3(.3f,.4f,.3f),c).localRotation=Quaternion.Euler(70,0,0);}
    armL=Arm(-1,c);armR=Arm(1,c);return;
   }
   SkyArt.Oval(body,"Tailored vest",Vector3.zero,new Vector3(.43f,.5f,.3f),c);
   SkyArt.Oval(body,"Ivory inset",new Vector3(.04f,.02f,.14f),new Vector3(.23f,.37f,.04f),SkyArt.Cream);
   SkyArt.Rod(body,"Diagonal fastening",new Vector3(-.17f,-.1f,.15f),new Vector3(.13f,.19f,.15f),.05f,SkyArt.Ink);
   SkyArt.Oval(body,"Brass buckle",new Vector3(0,-.18f,.17f),new Vector3(.10f,.07f,.035f),SkyArt.Gold);
   head=SkyArt.Node(body,"Head",new Vector3(0,.39f,0));
   SkyArt.Oval(head,"Sculpted mask",Vector3.zero,new Vector3(.27f,.31f,.25f),new Color(.95f,.76f,.55f));
   SkyArt.Oval(head,"Wind hood",new Vector3(0,.09f,-.025f),new Vector3(.31f,.21f,.29f),c);
   for(int side=-1;side<=1;side+=2){SkyArt.Oval(head,"Eye white",new Vector3(side*.065f,.015f,.116f),new Vector3(.065f,.038f,.023f),SkyArt.Cream);SkyArt.Oval(head,"Eye pupil",new Vector3(side*.065f,.015f,.13f),new Vector3(.025f,.03f,.012f),SkyArt.Ink);SkyArt.Rod(head,"Brow",new Vector3(side*.04f,.05f,.13f),new Vector3(side*.10f,.06f,.11f),.014f,SkyArt.Ink);}
   SkyArt.Oval(head,"Nose",new Vector3(0,-.035f,.13f),new Vector3(.035f,.05f,.045f),new Color(.9f,.65f,.46f));
   SkyArt.Rod(head,"Mouth",new Vector3(-.03f,-.08f,.115f),new Vector3(.03f,-.077f,.115f),.008f,SkyArt.Ink);
   armL=Arm(-1,c);armR=Arm(1,c);legL=Leg(-1);legR=Leg(1);
   cloth=SkyArt.Sail(body,"Wind coat tail",new Vector3(0,-.18f,-.14f),new Vector3(Kind==4?.65f:.42f,Kind==4?.7f:.4f,1),c);cloth.localRotation=Quaternion.Euler(165,0,0);
   if(Kind==0){SkyArt.Oval(head,"Flat kite cap",new Vector3(.03f,.15f,0),new Vector3(.43f,.10f,.34f),SkyArt.Cream);SkyArt.Sail(armR,"Curved dagger",new Vector3(0,-.38f,.06f),new Vector3(.13f,.38f,1),SkyArt.Cream).localRotation=Quaternion.Euler(0,0,180);}
   if(Kind==1){SkyArt.Rod(armR,"Spear shaft",new Vector3(0,-.7f,0),new Vector3(0,.5f,0),.035f,SkyArt.Gold);SkyArt.Sail(armR,"Feather spear tip",new Vector3(0,.5f,0),new Vector3(.16f,.32f,1),SkyArt.Cream);for(int i=0;i<3;i++)SkyArt.Oval(body,"Feather pauldron",new Vector3(-.24f-i*.04f,.2f,-i*.035f),new Vector3(.15f,.3f,.07f),SkyArt.Cream,true).localRotation=Quaternion.Euler(0,0,40);}
   if(Kind==2){SkyArt.Rod(armR,"Blowpipe",new Vector3(0,-.3f,0),new Vector3(0,-.3f,.45f),.06f,SkyArt.Gold);for(int side=-1;side<=1;side+=2)SkyArt.Oval(head,"Flight goggle",new Vector3(side*.07f,.05f,.13f),new Vector3(.10f,.07f,.025f),new Color(.25f,.8f,.9f));}
   if(Kind==4){SkyArt.Oval(head,"Captain bicorne",new Vector3(0,.17f,0),new Vector3(.58f,.21f,.3f),c);SkyArt.Sail(head,"Hat pennant",new Vector3(.1f,.22f,0),new Vector3(.13f,.3f,1),SkyArt.Cream);SkyArt.Rod(armR,"Hook hilt",new Vector3(0,-.32f,0),new Vector3(0,-.53f,0),.07f,SkyArt.Gold);var hook=SkyArt.Ring(armR,"Open hook blade",.13f,SkyArt.Cream,.045f,24);hook.loop=false;hook.positionCount=18;hook.transform.localPosition=new Vector3(0,-.62f,0);hook.transform.localRotation=Quaternion.Euler(90,0,0);}
  }
  Transform Arm(int side,Color c)
  {var a=SkyArt.Node(body,"Shoulder "+side,new Vector3(side*(Kind==3?.4f:.23f),.17f,0));SkyArt.Rod(a,"Sloped sleeve",Vector3.zero,new Vector3(side*.055f,-.22f,.02f),.15f,c);SkyArt.Rod(a,"Forearm",new Vector3(side*.055f,-.22f,.02f),new Vector3(side*.06f,-.36f,.06f),.095f,SkyArt.Cream);SkyArt.Oval(a,Kind==3?"Counterweight fist":"Gloved hand",new Vector3(side*.06f,-.36f,.06f),Vector3.one*(Kind==3?.24f:.12f),SkyArt.Gold);return a;}
  Transform Leg(int side){var l=SkyArt.Node(body,"Hip "+side,new Vector3(side*.11f,-.22f,0));SkyArt.Rod(l,"Tapered trouser",Vector3.zero,new Vector3(side*.03f,-.29f,.02f),.15f,SkyArt.Ink);SkyArt.Oval(l,"Flight boot",new Vector3(side*.03f,-.36f,.06f),new Vector3(.16f,.17f,.25f),SkyArt.Gold);return l;}
  public void Windup(float dur){if(dead)return;wind=0;windDuration=Mathf.Max(.05f,dur);winding=true;strike=10;}
  public void Attack(){if(dead)return;winding=false;strike=0;}
  public void PlayHit(){if(!dead)hit=.2f;}
  public void SetDead(bool value){dead=value;death=0;winding=false;strike=10;}
  public void Sample(float dt)
  {
   age+=dt;wind+=dt;strike+=dt;hit=Mathf.Max(0,hit-dt);if(dead)death+=dt;
   float gait=Mathf.Sin(age*(3+Mathf.Abs(MoveSpeed)*5))*Mathf.Clamp01(Mathf.Abs(MoveSpeed)),w=winding?Mathf.Clamp01(wind/windDuration):0,a=strike<.4f?Mathf.Sin(strike/.4f*Mathf.PI):0;
   body.localPosition=baseBody+Vector3.up*(dead?-.3f*Mathf.Clamp01(death):Kind==3?.05f*Mathf.Sin(age*2):.015f*Mathf.Sin(age*3));
   body.localRotation=Quaternion.Euler(dead?85*Mathf.Clamp01(death):w*-12+a*14,0,gait*3);
   if(armL)armL.localRotation=Quaternion.Euler(gait*18-w*45+a*55,0,-8);
   if(armR)armR.localRotation=Quaternion.Euler(-gait*18-w*(Kind==2?65:110)+a*80,Kind==4?w*50:0,8);
   if(legL)legL.localRotation=Quaternion.Euler(gait*25,0,-3);if(legR)legR.localRotation=Quaternion.Euler(-gait*25,0,3);
   if(cloth)cloth.localRotation=Quaternion.Euler(165+Mathf.Sin(age*5)*7,0,Mathf.Sin(age*3)*5);
   foreach(var r in renderers){block.Clear();var c=r.sharedMaterial.color;block.SetColor("_Color",Color.Lerp(dead?c*.45f:c,Color.white,hit/.2f));r.SetPropertyBlock(block);}
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
