using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public partial class CharacterRig
 {
  static readonly Dictionary<string,Mesh> blueprintMeshes=new Dictionary<string,Mesh>();
  static readonly Color BGold=new Color(1,.68f,.17f),BInk=new Color(.13f,.16f,.22f),BCream=new Color(1,.93f,.72f),BRed=new Color(.84f,.16f,.10f),BGreen=new Color(.29f,.66f,.15f);
  internal float BlueprintHitAmount=>Mathf.Clamp01(hitT/HitDur);
  internal float BlueprintLift=>poseLastLift;
  static bool HasBlueprintArt(string id)=>!string.IsNullOrEmpty(id)&&System.Array.IndexOf(SkillIcons.BlueprintArtIds,id)>=0&&Blueprints.Get(id)!=null;
  bool AddBlueprint(Transform parent,string slot,GearLook.Piece piece)
  {
   var def=Blueprints.Get(piece.special);if(def==null||!HasBlueprintArt(piece.special))return false;   // 還沒有專屬造型的配方：照一般裝備畫
   if(slot=="trinket")parent=piece.special=="bp_neonhalo"?head:spine;
   if(!blueprintMeshes.TryGetValue(piece.special,out var mesh)){mesh=MakeBlueprint(piece.special);blueprintMeshes[piece.special]=mesh;}
   var root=AnimeFace.Part(parent,"Blueprint "+piece.special,mesh,Vector3.zero,AnimeFace.Material(Color.white,4));GearPart(root);
   if(slot=="weapon")
   {
    // All existing weapon joints use local Z as the long axis, including BowPoint.
    root.transform.localRotation=parent.name=="Bow"&&piece.special=="bp_bamboo"?Quaternion.LookRotation(Vector3.left,Vector3.forward):Quaternion.Euler(90,0,0);
    if(piece.special=="bp_bamboo")root.transform.localPosition=-(root.transform.localRotation*new Vector3(.22f,.25f,0));
   }
   var motion=root.AddComponent<BlueprintGearMotion>();motion.id=piece.special;motion.rig=this;
   if(piece.rarity==5)
   {
    var glow=SpellFx.Line(root.transform,"Mythic signature light",SpellFx.Circle(24,slot=="body"?.24f:.13f),true,.012f,new Color(.48f,.86f,1,.55f),true,false);
    glow.transform.localPosition=slot=="body"?new Vector3(0,.28f,.24f):slot=="feet"?new Vector3(0,-.32f,.12f):new Vector3(0,.35f,0);motion.glow=glow;
   }
   if(piece.special=="bp_furnace"||piece.special=="bp_skewer"||piece.special=="bp_mushroom")
   {
    var ps=SpellFx.NewPS(root.transform,"Equipment motes",SpellFx.BlendMat(SpellFx.TexSoft),6,true);var m=ps.main;m.startLifetime=.8f;m.startSpeed=.10f;m.startSize=.025f;m.startColor=piece.special=="bp_mushroom"?new Color(.9f,1,.65f,.5f):new Color(1,.55f,.14f,.6f);
    ps.transform.localPosition=piece.special=="bp_furnace"?new Vector3(.17f,.64f,-.19f):piece.special=="bp_mushroom"?new Vector3(0,.23f,0):new Vector3(0,1.1f,0);SpellFx.Sphere(ps,.04f);SpellFx.Rate(ps,3);SpellFx.FadeOut(ps);ps.Play();
   }
   if(piece.special=="bp_bamboo")
   {var line=SpellFx.Line(root.transform,"Bamboo bowstring",new[]{new Vector3(0,-.43f,0),new Vector3(-.1f,.25f,0),new Vector3(0,.93f,0)},false,.008f,BCream,false,false);motion.bowString=line;}
   if(piece.special=="bp_jelly")
   {
    var bell=SkyArt.Oval(root.transform,"Translucent jelly bell",new Vector3(0,1.07f,0),new Vector3(.35f,.25f,.32f),Color.white);bell.GetComponent<Renderer>().sharedMaterial=SkyArt.Glow;
    var tint=new MaterialPropertyBlock();tint.SetColor("_Color",new Color(.43f,.84f,1,.48f));bell.GetComponent<Renderer>().SetPropertyBlock(tint);motion.moving=bell;motion.baseScale=bell.localScale;
   }
   if(piece.special=="bp_luckycat")motion.moving=SkyArt.Oval(root.transform,"Waving lucky paw",new Vector3(.12f,.34f,.25f),new Vector3(.065f,.15f,.07f),BCream);
   if(piece.special=="bp_chicken")motion.moving=SkyArt.Oval(root.transform,"Clucking beak",new Vector3(0,.26f,.29f),new Vector3(.11f,.08f,.16f),BGold);
   motion.details=BuildBlueprintDetails(root.transform,piece.special);
   return true;
  }
  static void Blob(OutfitArt.Builder b,Vector3 p,Vector3 size,Color c,int sides=8)
  {b.Loft(new[]{p.y+size.y*.5f,p.y+size.y*.32f,p.y-size.y*.32f,p.y-size.y*.5f},new[]{size.x*.12f,size.x*.5f,size.x*.5f,size.x*.12f},new[]{size.z*.12f,size.z*.5f,size.z*.5f,size.z*.12f},c,p.x,p.z,sides);}
  static void Band(OutfitArt.Builder b,Vector3 p,float r,float depth,float height,Color c,int sides=10)
  {b.Loft(new[]{p.y+height*.5f,p.y-height*.5f},new[]{r,r},new[]{depth,depth},c,p.x,p.z,sides);}
  static void LineB(OutfitArt.Builder b,Vector3 a,Vector3 z,float width,Color c)
  {int start=b.V.Count;float length=(z-a).magnitude;b.Loft(new[]{length*.5f,-length*.5f},new[]{width,width},new[]{width,width},c,0,0,5);var q=Quaternion.FromToRotation(Vector3.up,z-a);for(int i=start;i<b.V.Count;i++)b.V[i]=q*b.V[i]+(a+z)*.5f;}
  static void EyesB(OutfitArt.Builder b,Vector3 p,float spread)
  {for(int side=-1;side<=1;side+=2)Blob(b,p+Vector3.right*spread*side,new Vector3(.025f,.032f,.018f),BInk,6);}
  static void RingB(OutfitArt.Builder b,Vector3 p,float radius,float width,Color c,bool vertical=false)
  {for(int i=0;i<16;i++){float a=i*Mathf.PI/8,d=(i+1)*Mathf.PI/8;var x=vertical?new Vector3(Mathf.Cos(a),Mathf.Sin(a),0):new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var y=vertical?new Vector3(Mathf.Cos(d),Mathf.Sin(d),0):new Vector3(Mathf.Cos(d),0,Mathf.Sin(d));LineB(b,p+x*radius,p+y*radius,width,c);}}
  static void LabelSaint(OutfitArt.Builder b)
  {
   string[] glyphs={"111101110001111","010101111101101","111010010010111","101111111111101","111010010010010"};
   for(int letter=0;letter<5;letter++)for(int y=0;y<5;y++)for(int x=0;x<3;x++)if(glyphs[letter][y*3+x]=='1')
   {float px=.397f-letter*.041f-x*.010f,py=.325f-y*.010f;int k=b.V.Count;b.V.AddRange(new[]{new Vector3(px,py,.026f),new Vector3(px-.008f,py,.026f),new Vector3(px,py-.008f,.026f),new Vector3(px-.008f,py-.008f,.026f)});for(int q=0;q<4;q++)b.C.Add(BCream);b.T.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});}
  }
  static Mesh MakeBlueprint(string id)
  {
   var b=new OutfitArt.Builder();b.Skin=new Color(.123f,.456f,.789f);
   switch(id)
   {
    case "bp_skewer":
     LineB(b,new Vector3(0,-.18f,0),new Vector3(0,1.25f,0),.018f,BCream);
     for(int i=0;i<6;i++){float y=.23f+i*.15f;Blob(b,new Vector3(0,y,0),new Vector3(i%3==0?.23f:.19f,.12f,.17f),i%3==0?new Color(.64f,.20f,.09f):i%3==1?BGreen:BCream);if(i%3==0)for(int j=0;j<2;j++)LineB(b,new Vector3(-.08f,y-.03f+j*.05f,.085f),new Vector3(.08f,y+j*.05f,.085f),.007f,BInk);}
     break;
    case "bp_chicken":
     Blob(b,new Vector3(0,.16f,0),new Vector3(.45f,.23f,.40f),BCream);Blob(b,new Vector3(0,.29f,.16f),new Vector3(.21f,.24f,.22f),BCream);
     // The crest is a separate animated part (BlueprintDetails).
     EyesB(b,new Vector3(0,.32f,.247f),.065f);
     for(int side=-1;side<=1;side+=2)Blob(b,new Vector3(side*.22f,.17f,0),new Vector3(.12f,.14f,.25f),BGold);break;
    case "bp_furnace":
     Blob(b,new Vector3(0,.26f,0),new Vector3(.59f,.54f,.44f),BInk);Band(b,new Vector3(0,.04f,0),.31f,.23f,.05f,BGold);
     b.Badge(0,.27f,.225f,.18f,.16f,BGold);b.Badge(0,.27f,.24f,.14f,.12f,new Color(1,.30f,.03f));
     for(int i=-1;i<=1;i++)LineB(b,new Vector3(i*.06f,.16f,.255f),new Vector3(i*.06f,.38f,.255f),.012f,BInk);
     Band(b,new Vector3(.17f,.56f,-.19f),.055f,.055f,.28f,BInk);Band(b,new Vector3(.17f,.69f,-.19f),.075f,.075f,.035f,BGold);break;
    case "bp_mushroom":
     Band(b,new Vector3(0,.13f,0),.17f,.15f,.13f,BCream);
     b.Loft(new[]{.39f,.33f,.18f,.15f},new[]{.04f,.25f,.39f,.31f},new[]{.04f,.23f,.35f,.30f},BRed,0,0,12);
     for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;Blob(b,new Vector3(Mathf.Cos(a)*.19f,.355f,Mathf.Sin(a)*.17f),new Vector3(.085f,.028f,.085f),BCream,6);}break;
    case "bp_bamboo":
     for(int i=0;i<6;i++){float y=-.42f+i*.23f,x=.22f*Mathf.Sin((y+.42f)/1.38f*Mathf.PI);LineB(b,new Vector3(x,y,0),new Vector3(.22f*Mathf.Sin((y+.65f)/1.38f*Mathf.PI),y+.23f,0),.045f,BGreen);Blob(b,new Vector3(x,y+.05f,.047f),new Vector3(.03f,.035f,.015f),BInk,6);}
     Blob(b,new Vector3(.24f,.24f,0),new Vector3(.14f,.32f,.13f),new Color(.70f,.78f,.30f));break;
    case "bp_planter":
     b.Loft(new[]{-.22f,-.25f,-.42f},new[]{.14f,.13f,.095f},new[]{.17f,.17f,.13f},new Color(.70f,.31f,.15f),0,.035f,10);
     Band(b,new Vector3(0,-.235f,.035f),.15f,.18f,.035f,BGold);
     break; // Trees and flowers are animated children.
    case "bp_rumbarrel":
     b.Loft(new[]{.51f,.42f,.10f,.01f},new[]{.22f,.31f,.31f,.22f},new[]{.20f,.27f,.27f,.20f},new Color(.56f,.29f,.12f),0,0,12);
     foreach(float y in new[]{.07f,.26f,.46f})Band(b,new Vector3(0,y,0),y==.26f?.319f:.275f,y==.26f?.279f:.24f,.035f,BInk,12);
     LineB(b,new Vector3(.16f,.48f,.15f),new Vector3(.16f,.66f,.15f),.018f,BCream);LineB(b,new Vector3(.16f,.66f,.15f),new Vector3(.26f,.69f,.15f),.018f,BCream);break;
    case "bp_jelly":
     LineB(b,new Vector3(0,-.20f,0),new Vector3(0,1.1f,0),.025f,new Color(.24f,.64f,.79f));
     EyesB(b,new Vector3(0,1.05f,.155f),.065f);
     for(int i=0;i<5;i++){float x=(i-2)*.055f;LineB(b,new Vector3(x,.99f,0),new Vector3(x+.03f,.82f,.02f),.009f,new Color(.68f,.53f,1));LineB(b,new Vector3(x+.03f,.82f,.02f),new Vector3(x-.02f,.72f,.03f),.008f,BCream);}break;
    case "bp_shark":
     Blob(b,new Vector3(0,-.35f,.10f),new Vector3(.24f,.16f,.40f),new Color(.30f,.56f,.73f));
     b.Badge(0,-.36f,.30f,.095f,.04f,BInk);for(int i=-2;i<=2;i++)b.Panel(new Vector3(i*.033f-.012f,-.33f,.31f),new Vector3(i*.033f+.012f,-.33f,.31f),new Vector3(i*.033f,-.37f,.315f),new Vector3(i*.033f,-.37f,.315f),BCream,0,1,1);
     b.Panel(new Vector3(-.02f,-.29f,.05f),new Vector3(.02f,-.29f,.05f),new Vector3(0,-.16f,.01f),new Vector3(.01f,-.16f,.01f),new Color(.3f,.75f,.9f),0,1,1);EyesB(b,new Vector3(0,-.30f,.23f),.075f);break;
    case "bp_trophy":
     b.Loft(new[]{.47f,.42f,.17f,.12f},new[]{.11f,.11f,.24f,.25f},new[]{.10f,.1f,.20f,.21f},BGold,0,0,12);Band(b,new Vector3(0,.48f,0),.15f,.12f,.035f,BGold);
     for(int side=-1;side<=1;side+=2)RingB(b,new Vector3(side*.24f,.24f,0),.105f,.018f,BGold,true);
     b.Badge(0,.275f,.211f,.15f,.065f,BGold);TrophyInscription(b);break;
    case "bp_corn":
     LineB(b,new Vector3(0,-.25f,0),new Vector3(0,1.62f,0),.025f,new Color(.56f,.36f,.13f));Blob(b,new Vector3(0,1.13f,0),new Vector3(.25f,.70f,.25f),BGold);
     for(int row=0;row<7;row++)for(int col=0;col<4;col++){float a=col*Mathf.PI*.5f;Blob(b,new Vector3(Mathf.Cos(a)*.12f,.84f+row*.085f,Mathf.Sin(a)*.12f),new Vector3(.065f,.065f,.065f),row%3==0?new Color(.56f,.31f,.07f):BCream,4);}
     break;
    case "bp_neonhalo":
     RingB(b,new Vector3(0,.34f,0),.25f,.014f,new Color(.32f,1,.89f));b.Badge(.31f,.30f,.02f,.12f,.05f,new Color(.18f,.13f,.32f));
     LabelSaint(b);break;
    case "bp_luckycat":
     Blob(b,new Vector3(0,.25f,.23f),new Vector3(.17f,.20f,.12f),BCream);Blob(b,new Vector3(0,.37f,.24f),new Vector3(.17f,.14f,.13f),BCream);
     for(int side=-1;side<=1;side+=2)Blob(b,new Vector3(side*.065f,.44f,.24f),new Vector3(.065f,.085f,.06f),BGold,4);
     Band(b,new Vector3(0,.405f,.24f),.09f,.07f,.035f,new Color(.28f,.65f,.76f));EyesB(b,new Vector3(0,.37f,.307f),.04f);b.Badge(0,.25f,.299f,.055f,.065f,BGold);break;
    case "bp_hourglass":
     for(int side=-1;side<=1;side+=2)LineB(b,new Vector3(side*.095f,.15f,.23f),new Vector3(side*.095f,.43f,.23f),.012f,BGold);
     foreach(float y in new[]{.15f,.43f})Band(b,new Vector3(0,y,.23f),.115f,.075f,.025f,BGold,8);
     b.Loft(new[]{.41f,.29f,.17f},new[]{.075f,.015f,.075f},new[]{.055f,.012f,.055f},new Color(.56f,.83f,.91f),0,.23f,8);LineB(b,new Vector3(0,.20f,.29f),new Vector3(0,.38f,.29f),.008f,BGold);break;
    case "bp_camel":
     Blob(b,new Vector3(0,.25f,0),new Vector3(.54f,.50f,.40f),new Color(.65f,.42f,.20f));
     for(int side=-1;side<=1;side+=2){Blob(b,new Vector3(side*.14f,.35f,-.22f),new Vector3(.24f,.39f,.26f),new Color(.76f,.54f,.29f));Band(b,new Vector3(side*.14f,.29f,-.24f),.13f,.14f,.035f,BInk);}
     for(int side=-1;side<=1;side+=2)Blob(b,new Vector3(side*.09f,.46f,.25f),new Vector3(.05f,.10f,.04f),BCream,6);
     for(int j=0;j<8;j++){float x=(j%4-1.5f)*.07f;float y=.32f+(j/4)*.11f;b.Panel(new Vector3(x-.015f,y,-.355f),new Vector3(x+.015f,y,-.355f),new Vector3(x,y-.05f,-.36f),new Vector3(x,y-.05f,-.36f),new Color(.56f,.35f,.17f),0,1,1);}
     Blob(b,new Vector3(0,.35f,.26f),new Vector3(.21f,.20f,.21f),new Color(.84f,.63f,.36f));EyesB(b,new Vector3(0,.37f,.36f),.07f);break; // Chewing muzzle and grass live in the animated child.
   }
   return b.Finish("Blueprint "+id);
  }
 }
 public sealed class BlueprintGearMotion:MonoBehaviour
 {
  internal string id;internal CharacterRig rig;internal LineRenderer glow,bowString;internal Transform moving;internal Vector3 baseScale;internal BlueprintGearDetail details;float time;
  public void Sample(float dt)
  {
   time+=Mathf.Max(0,dt);
   if(details)details.Sample(dt,rig);
   if(glow){var c=Color.HSVToRGB(Mathf.Repeat(time*.08f+.45f,1),.45f,1);c.a=.40f+.15f*Mathf.Sin(time*2);glow.startColor=glow.endColor=c;}
   if(moving){if(id=="bp_jelly")moving.localScale=Vector3.Scale(baseScale,new Vector3(1+.04f*Mathf.Sin(time*4),1-.05f*Mathf.Sin(time*4),1+.04f*Mathf.Sin(time*4)));else if(id=="bp_luckycat")moving.localRotation=Quaternion.Euler(0,0,20*Mathf.Sin(time*3));else if(rig)moving.localRotation=Quaternion.Euler(rig.BlueprintHitAmount*32,0,0);}
   if(bowString&&rig)bowString.SetPosition(1,new Vector3(-.10f-.18f*rig.Charge,.25f,0));
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
