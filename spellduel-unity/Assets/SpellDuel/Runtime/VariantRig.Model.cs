using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public sealed partial class VariantRig : MonoBehaviour
 {
  int variantId; Transform torso,head,upperL,upperR,foreL,foreR,handL,handR,hipL,hipR,kneeL,kneeR,tail,accessory,potion;
  readonly List<Transform> joints=new List<Transform>(); Vector3 torsoRest; Color uniform,trim;
  bool caster,archer,assassin; Renderer[] surfaces;Material[] originalMaterials;MaterialPropertyBlock tint;
  static readonly Color[] TeamColors={new Color(.48f,.12f,.13f),new Color(.12f,.32f,.24f),new Color(.12f,.24f,.42f),new Color(.38f,.22f,.37f),new Color(.42f,.28f,.16f)};
  Transform Joint(Transform p,string n,Vector3 v){var t=VariantGeometry.Node(p,n,v);joints.Add(t);return t;}
  void O(Transform p,string n,float x,float y,float z,float w,float h,float d,Color c){VariantGeometry.Oval(p,n,new Vector3(x,y,z),new Vector3(w,h,d),c);}
  void R(Transform p,string n,Vector3 a,Vector3 b,float w,Color c){VariantGeometry.Rod(p,n,a,b,w,c);}
  void BuildModel(int id)
  {
   variantId=id;int team=id>=151?(id-151)/3:id==141?1:id==142?2:id==121||id==143?3:4;uniform=TeamColors[team];trim=team==0?VariantGeometry.Metal:VariantGeometry.Gold;
   caster=id==121||id==122||id==123||id==141||id==142||id==153||id==156||id==158||id==162||id==164;
   archer=id==152||id==155||id==161||id==165;assassin=id==154||id==159||id==163;
   torso=Joint(transform,"Pelvis and spine",new Vector3(0,.87f,0));torsoRest=torso.localPosition;
   if(id==124){BuildSpirit();FinishModel();return;}
   float width=id==122||id==143?.59f:assassin?.37f:.45f;
   VariantGeometry.Part(torso,"Tailored enemy jacket",VariantGeometry.Cloth,new Vector3(0,.18f,0),new Vector3(width,.55f,.62f),uniform);
   O(torso,"Collar",0,.43f,0,.27f,.09f,.22f,VariantGeometry.Ink);
   O(torso,"Waist belt",0,-.035f,0,width*.88f,.08f,.29f,VariantGeometry.Ink);
   O(torso,"Enemy red clasp",.045f,-.035f,.152f,.075f,.055f,.025f,VariantGeometry.Red);
   R(torso,"Functional weapon baldric",new Vector3(-.14f,-.03f,.15f),new Vector3(.16f,.4f,.13f),.042f,trim);
   head=Joint(torso,"Head and hostile face",new Vector3(0,.58f,.015f));
   VariantGeometry.Part(head,"Face",VariantGeometry.Head,Vector3.zero,new Vector3(.32f,.36f,.32f),VariantGeometry.Skin);
   O(head,"Hair cap",0,.087f,-.028f,.32f,.23f,.29f,VariantGeometry.Ink);
   for(int i=0;i<7;i++){float x=(i-3)*.04f;var tuft=VariantGeometry.Blade(head,"Tapered fringe",new Vector3(x,.105f,.095f),.13f,.065f,VariantGeometry.Ink);tuft.localRotation=Quaternion.Euler(12,0,170+(i-3)*5);}
   for(int side=-1;side<=1;side+=2){O(head,"Ear",side*.134f,-.005f,0,.05f,.075f,.04f,VariantGeometry.Skin);O(head,"Narrow eye",side*.061f,.007f,.112f,.071f,.028f,.016f,new Color(.95f,.87f,.74f));O(head,"Crimson iris",side*.055f,.007f,.123f,.024f,.028f,.008f,VariantGeometry.Red);
    R(head,"Angry sloping brow",new Vector3(side*.025f,.039f,.12f),new Vector3(side*.1f,.065f,.105f),.017f,VariantGeometry.Ink);}
   O(head,"Nose bridge",0,-.032f,.119f,.028f,.047f,.03f,VariantGeometry.Skin);
   R(head,"Crooked smirk",new Vector3(-.035f,-.082f,.103f),new Vector3(.033f,-.072f,.11f),.009f,VariantGeometry.Ink);
   for(int s=-1;s<=1;s+=2){var upper=Joint(torso,s<0?"Left shoulder":"Right shoulder",new Vector3(s*width*.46f,.35f,0));R(upper,"Sloping deltoid",Vector3.zero,new Vector3(s*.035f,-.16f,0),.17f,uniform);
    var fore=Joint(upper,s<0?"Left elbow":"Right elbow",new Vector3(s*.035f,-.22f,0));R(fore,"Tapered vambrace",Vector3.zero,new Vector3(0,-.19f,.01f),.11f,VariantGeometry.Ink);O(fore,"Bracer edge",0,-.04f,.01f,.12f,.045f,.12f,trim);
    var hand=Joint(fore,s<0?"Left grip":"Right grip",new Vector3(0,-.22f,.01f));O(hand,"Knuckled glove",0,0,0,.09f,.11f,.085f,VariantGeometry.Skin);O(hand,"Thumb grip",-s*.035f,.018f,.035f,.035f,.06f,.04f,VariantGeometry.Skin);
    var hip=Joint(torso,s<0?"Left hip":"Right hip",new Vector3(s*.105f,-.07f,0));R(hip,"Thigh",Vector3.zero,new Vector3(0,-.31f,0),.17f,uniform);
    var knee=Joint(hip,s<0?"Left knee":"Right knee",new Vector3(0,-.34f,0));R(knee,"Boot shaft",Vector3.zero,new Vector3(0,-.29f,0),.14f,VariantGeometry.Ink);O(knee,"Rounded boot",0,-.36f,.045f,.17f,.14f,.28f,VariantGeometry.Ink);O(knee,"Metal toe cap",0,-.36f,.13f,.16f,.11f,.1f,trim);
    if(s<0){upperL=upper;foreL=fore;handL=hand;hipL=hip;kneeL=knee;}else{upperR=upper;foreR=fore;handR=hand;hipR=hip;kneeR=knee;}}
   tail=Joint(torso,"Coat tails",new Vector3(-.15f,.1f,-.13f));VariantGeometry.ClothPanel(tail,"Red inner coat",Vector3.zero,new Vector3(.30f,caster?.63f:.36f,1),VariantGeometry.Red);
   potion=Joint(handL,"Recovery vial",new Vector3(0,.065f,.02f));O(potion,"Flask",0,0,0,.075f,.12f,.075f,new Color(.36f,.53f,.22f));O(potion,"Stopper",0,.07f,0,.045f,.04f,.045f,trim);
   switch(id)
   {
    case 121: Mitre(); Staff("Split sun",0);Sun(head,new Vector3(0,.18f,.06f),.1f);break;
    case 122: O(torso,"Ceramic medicine backpack",0,.16f,-.25f,.43f,.48f,.31f,new Color(.18f,.43f,.40f));for(int s=-1;s<=1;s+=2)R(torso,"Jar strap",new Vector3(s*.14f,.42f,-.12f),new Vector3(s*.14f,-.04f,-.16f),.045f,trim);Staff("Medicine ladle",1);O(head,"Apothecary wrap",0,.15f,0,.40f,.10f,.32f,trim);break;
    case 123: Hood();O(torso,"Summoning stone tablet",0,.23f,-.23f,.43f,.62f,.14f,VariantGeometry.Metal);for(int i=0;i<3;i++)O(torso,"Tablet seal",0,.04f+i*.15f,-.31f,.12f,.09f,.025f,VariantGeometry.Red);Staff("Sealed stone",2);break;
    case 141: Hood();Staff("Rain bells",3);for(int s=-1;s<=1;s+=2){R(head,"Wood antler",new Vector3(s*.1f,.13f,0),new Vector3(s*.25f,.36f,0),.045f,trim);R(head,"Antler fork",new Vector3(s*.17f,.23f,0),new Vector3(s*.13f,.34f,0),.035f,trim);}break;
    case 142: BuildWings();Staff("Pearl conch",4);O(head,"Pearl crown",0,.17f,0,.28f,.06f,.23f,trim);break;
    case 143: Drum();Mallet(handL);Mallet(handR);O(head,"Drummer headwrap",0,.12f,0,.35f,.09f,.30f,VariantGeometry.Red);break;
    case 144: Hood();Flag();break;
    case 151: Sword(handR,false);Shield(handL);Helmet();break;
    case 152: Hood();Crossbow();break;
    case 153: Hood();Staff("Coal brazier",5);break;
    case 154: Hood();Sword(handL,true);Sword(handR,true);Scarf();break;
    case 155: Hood();Bow();Quiver();break;
    case 156: Hood();Staff("Thorn seed",6);O(torso,"Seed satchel",-.19f,-.08f,.09f,.18f,.22f,.13f,trim);break;
    case 157: PirateHat();Sword(handL,false);Sword(handR,false);break;
    case 158: PirateHat();Staff("Tide conch",4);break;
    case 159: Scarf();Sword(handL,true);Sword(handR,true);O(head,"Pirate headscarf",0,.14f,-.015f,.36f,.10f,.31f,uniform);break;
    case 160: Helmet();Sword(handR,false);Shield(handL);O(torso,"Champion shoulder armor",-.24f,.35f,0,.30f,.20f,.31f,trim);break;
    case 161: Helmet();Spear();break;
    case 162: Mitre();Staff("Iron shrine",0);break;
    case 163: Hood();Scarf();Sword(handL,true);Sword(handR,true);break;
    case 164: Hood();Staff("Hourglass",7);break;
    case 165: Hood();Bow();Quiver();O(torso,"Bone trophy",.18f,-.11f,.1f,.08f,.18f,.07f,VariantGeometry.Metal);break;
   }
   // The same five team emblems, with role-independent enemy red backing.
   O(torso,"Enemy team seal",-.09f,.29f,.16f,.10f,.12f,.025f,VariantGeometry.Red);
   for(int i=0;i<=team;i++)R(torso,"Team seal tally",new Vector3(-.12f+i*.014f,.26f,.178f),new Vector3(-.12f+i*.014f,.32f,.178f),.007f,trim);
   FinishModel();
  }
  void FinishModel(){VariantGeometry.Batch(variantId,joints.ToArray());surfaces=GetComponentsInChildren<Renderer>();originalMaterials=new Material[surfaces.Length];for(int i=0;i<surfaces.Length;i++)originalMaterials[i]=surfaces[i].sharedMaterial;tint=new MaterialPropertyBlock();if(potion)potion.gameObject.SetActive(false);}
  void Hood(){O(head,"Low forehead hood",0,.12f,-.025f,.40f,.21f,.34f,uniform);for(int s=-1;s<=1;s+=2)O(head,"Hood cheek border",s*.154f,-.015f,-.025f,.065f,.28f,.20f,uniform);}
  void Mitre(){for(int s=-1;s<=1;s+=2){var t=VariantGeometry.Blade(head,"Split mitre",new Vector3(s*.08f,.1f,0),.31f,.22f,trim);t.localRotation=Quaternion.Euler(0,0,-s*12);}O(head,"Red mitre band",0,.14f,0,.34f,.06f,.27f,VariantGeometry.Red);}
  void Helmet(){O(head,"Iron helmet crown",0,.13f,-.02f,.37f,.18f,.34f,VariantGeometry.Metal);for(int s=-1;s<=1;s+=2)O(head,"Cheek armor",s*.15f,-.025f,0,.065f,.22f,.20f,trim);VariantGeometry.Blade(head,"Red crest",new Vector3(0,.2f,-.04f),.22f,.18f,VariantGeometry.Red);}
  void PirateHat(){O(head,"Low tricorne",0,.16f,-.015f,.54f,.13f,.36f,uniform);O(head,"Hat red clasp",0,.18f,.15f,.09f,.09f,.025f,VariantGeometry.Red);}
  void Scarf(){O(head,"Lower face scarf",0,-.095f,.04f,.29f,.12f,.23f,uniform);}
  void Sword(Transform hand,bool shortBlade){R(hand,"Sword grip",new Vector3(0,-.06f,0),new Vector3(0,.08f,0),.045f,VariantGeometry.Ink);R(hand,"Sword guard",new Vector3(-.1f,.08f,0),new Vector3(.1f,.08f,0),.035f,trim);VariantGeometry.Blade(hand,"Bevelled steel blade",new Vector3(0,.09f,0),shortBlade?.30f:.58f,shortBlade?.1f:.13f,VariantGeometry.Metal);}
  void Shield(Transform hand){O(hand,"Forearm buckler",0,.04f,.095f,.32f,.39f,.075f,trim);O(hand,"Red shield boss",0,.04f,.14f,.18f,.22f,.03f,VariantGeometry.Red);}
  void Bow(){for(int s=-1;s<=1;s+=2){R(handL,"Curved bow limb",Vector3.zero,new Vector3(0,s*.24f,.12f),.04f,trim);R(handL,"Bow tip",new Vector3(0,s*.24f,.12f),new Vector3(0,s*.38f,.06f),.03f,trim);}R(handL,"Bow string",new Vector3(0,-.38f,.06f),new Vector3(0,.38f,.06f),.008f,VariantGeometry.Metal);R(handR,"Nocked arrow",new Vector3(0,0,-.13f),new Vector3(0,0,.38f),.018f,trim);}
  void Crossbow(){R(handL,"Crossbow stock",new Vector3(0,0,-.2f),new Vector3(0,0,.35f),.085f,uniform);R(handL,"Crossbow limbs",new Vector3(-.29f,0,.22f),new Vector3(.29f,0,.22f),.045f,trim);R(handL,"Crossbow string L",new Vector3(-.29f,0,.22f),Vector3.zero,.01f,VariantGeometry.Metal);R(handL,"Crossbow string R",new Vector3(.29f,0,.22f),Vector3.zero,.01f,VariantGeometry.Metal);}
  void Quiver(){O(torso,"Back quiver",.14f,.16f,-.23f,.16f,.45f,.14f,VariantGeometry.Ink);for(int i=0;i<3;i++)R(torso,"Spare arrow",new Vector3(.11f+i*.04f,.2f,-.23f),new Vector3(.1f+i*.04f,.55f,-.23f),.018f,trim);}
  void Spear(){R(handR,"Javelin shaft",new Vector3(0,-.5f,0),new Vector3(0,.60f,0),.035f,trim);VariantGeometry.Blade(handR,"Javelin steel tip",new Vector3(0,.6f,0),.23f,.13f,VariantGeometry.Metal);}
  void Sun(Transform p,Vector3 at,float radius){for(int i=0;i<8;i++){float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;R(p,"Hollow sun segment",at+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius,at+new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius,.023f,trim);}}
  void Staff(string name,int style){R(handR,name+" shaft",new Vector3(0,-.42f,0),new Vector3(0,.64f,0),.038f,trim);var top=new Vector3(0,.72f,0);
   if(style==0)Sun(handR,top,.15f);
   else if(style==1){O(handR,"Ladle bowl",0,.68f,.04f,.23f,.10f,.22f,trim);O(handR,"Herbal dose",0,.73f,.04f,.13f,.04f,.12f,new Color(.28f,.48f,.23f));}
   else if(style==3){R(handR,"Bell crossbar",top+Vector3.left*.2f,top+Vector3.right*.2f,.035f,trim);for(int i=-1;i<=1;i++){R(handR,"Bell cord",top+Vector3.right*i*.16f,top+Vector3.right*i*.16f+Vector3.down*.13f,.014f,VariantGeometry.Red);O(handR,"Rain bell",i*.16f,.56f,0,.10f,.13f,.10f,trim);}}
   else if(style==7){for(int s=-1;s<=1;s+=2){O(handR,"Hourglass rim",0,.72f+s*.13f,0,.22f,.045f,.15f,trim);R(handR,"Hourglass support",new Vector3(s*.09f,.6f,0),new Vector3(s*.09f,.84f,0),.025f,trim);}O(handR,"Hourglass sand",0,.72f,0,.09f,.18f,.09f,VariantGeometry.Red);}
   else{O(handR,name+" core",0,.73f,0,.21f,.24f,.18f,style==4?new Color(.41f,.55f,.58f):VariantGeometry.Red);for(int i=-1;i<=1;i++){var b=VariantGeometry.Blade(handR,name+" prong",new Vector3(i*.095f,.64f,0),.28f,.07f,style==6?uniform:trim);b.localRotation=Quaternion.Euler(0,0,-i*18);}}
  }
  void Drum(){accessory=Joint(torso,"Back drum",new Vector3(0,.18f,-.3f));O(accessory,"Bronze drum shell",0,0,0,.60f,.57f,.36f,trim);O(accessory,"Taut red hide",0,0,-.18f,.54f,.51f,.035f,VariantGeometry.Red);for(int i=-1;i<=1;i+=2)R(torso,"Drum harness",new Vector3(i*.14f,.42f,.13f),new Vector3(i*.12f,-.04f,.14f),.055f,VariantGeometry.Ink);}
  void Mallet(Transform hand){R(hand,"Drumstick",new Vector3(0,-.05f,0),new Vector3(0,.32f,0),.035f,trim);O(hand,"Padded drum beater",0,.32f,0,.13f,.14f,.13f,VariantGeometry.Red);}
  void Flag(){R(handR,"War standard pole",new Vector3(0,-.5f,0),new Vector3(0,1.05f,0),.045f,trim);accessory=Joint(handR,"Flag cloth joint",new Vector3(0,1,0));VariantGeometry.ClothPanel(accessory,"Broad enemy standard",Vector3.zero,new Vector3(.58f,.69f,1),uniform);VariantGeometry.ClothPanel(accessory,"Crimson standard stripe",new Vector3(.25f,-.035f,.012f),new Vector3(.085f,.60f,1),VariantGeometry.Red);}
  void BuildWings(){accessory=Joint(torso,"Wings",new Vector3(0,.30f,-.12f));for(int s=-1;s<=1;s+=2){var w=VariantGeometry.Oval(accessory,"Veined fairy wing",new Vector3(s*.29f,.08f,-.08f),new Vector3(.38f,.64f,.035f),new Color(.39f,.38f,.66f));w.localRotation=Quaternion.Euler(0,s*15,-s*35);R(accessory,"Wing vein",new Vector3(0,0,0),new Vector3(s*.43f,.29f,-.09f),.015f,VariantGeometry.Red);}}
  void BuildSpirit(){torsoRest=new Vector3(0,.30f,0);torso.localPosition=torsoRest;O(torso,"Sand vortex",0,0,0,.39f,.45f,.33f,trim);head=Joint(torso,"Spirit mask",new Vector3(0,.28f,.02f));O(head,"Fox sand mask",0,0,0,.30f,.24f,.23f,trim);for(int s=-1;s<=1;s+=2){VariantGeometry.Blade(head,"Sand ear",new Vector3(s*.10f,.06f,0),.19f,.12f,uniform);O(head,"Red spirit eye",s*.066f,.01f,.11f,.065f,.024f,.018f,VariantGeometry.Red);}for(int i=0;i<6;i++){float a=i*Mathf.PI/3;O(torso,"Orbiting sand grain",Mathf.Cos(a)*.22f,-.13f+(i%2)*.05f,Mathf.Sin(a)*.22f,.05f,.06f,.05f,uniform);}}
 }
}
