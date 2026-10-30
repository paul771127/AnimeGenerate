using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  public static void SetAltarGlow(GameObject altarProp,bool heartThere)
  {
   if(!altarProp)return;var heart=altarProp.transform.Find("AncientHeart");
   if(!heart){heart=Part(altarProp.transform,"quest/heart",b=>{b.Gem(V(-.14f,.08f,0),V(.40f,.48f,.35f),Glow(Jade));b.Gem(V(.14f,.08f,0),V(.40f,.48f,.35f),Glow(Jade));b.Gem(V(0,-.18f,0),V(.36f,.45f,.32f),Bark);b.Vine(V(-.2f,-.12f,.18f),V(.2f,.2f,.18f),.12f,.018f,Glow(Light),5);Leaves(b,V(0,.25f,0),.33f,Moss,5);},V(0,1.95f,0));heart.name="AncientHeart";}
   heart.gameObject.SetActive(heartThere);
  }
  public static GameObject CreateHerb()
  {var root=Root("Moon herb");Part(root.transform,"quest/herb",b=>{for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;var tip=V(Mathf.Cos(a)*.19f,.3f+(i%3)*.09f,Mathf.Sin(a)*.19f);b.Frond(Vector3.zero,tip,.055f,Leaf);b.Gem(tip,V(.04f,.09f,.04f),Glow(Light),4);}}).name="MoonLeaves";return root;}
  public static GameObject CreateSparkle(string kind)
  {var root=Root("Sparkle "+kind);Part(root.transform,"quest/sparkle/"+(kind??"chest"),b=>{var color=kind=="chest"?new Color(1,.88f,.57f,.15f):new Color(.65f,1,.91f,.14f);b.Quad(V(-.035f,0,0),V(0,.09f,0),V(.035f,0,0),V(0,-.09f,0),color,true);},V(0,.16f,0),true).name="Glint";return root;}
  public static void SparkleFound(Vector3 atWorld,string kind){/* Deliberately empty: C2 video supplied by the user's animation session. */}
  internal static GameObject SpiritModel()
  {
   var root=Root("Forest spirit");Part(root.transform,"npc/spirit",b=>{var green=Glow(new Color(.51f,.93f,.74f));b.Gem(V(0,1.12f,0),V(.42f,.5f,.36f),green,10);b.Gem(V(0,.74f,0),V(.30f,.52f,.26f),green,8);b.Vine(V(0,.64f,0),V(.16f,.16f,-.03f),.1f,.09f,green,6);for(int s=-1;s<=1;s+=2){b.Rod(V(s*.12f,.86f,0),V(s*.3f,.64f,.06f),.045f,green);b.Gem(V(s*.075f,1.15f,.169f),V(.065f,.09f,.025f),new Color(.10f,.32f,.30f),6);}b.Rod(V(-.05f,1.02f,.175f),V(.05f,1.02f,.175f),.012f,Bone,4);Leaves(b,V(0,1.34f,0),.29f,Moss,7);}).name="SpiritBody";
   for(int i=0;i<3;i++){int k=i;Part(root.transform,"npc/spiritLeaf/"+i,b=>b.Frond(Vector3.zero,V(.14f,.08f,0),.045f,Glow(Moss)),V((i-1)*.3f,.70f+i*.14f,.04f)).name="OrbitLeaf_"+k;}return root;
  }
  static Transform BoneAt(Transform root,string name){foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return root;}
  public static void DressNpc(CharacterRig rig,string npcId)
  {
   if(!rig)return;var old=rig.GetComponent<ExploreNpcDress>();if(old&&old.identity==npcId)return;if(old){old.Restore();SkyArt.Dispose(old);}
   var dress=rig.gameObject.AddComponent<ExploreNpcDress>();dress.identity=npcId;var root=SkyArt.Node(rig.transform,"ExploreNpcDress",Vector3.zero);dress.art=root;
   if(npcId=="spirit")
   {dress.originals=rig.GetComponentsInChildren<Renderer>(true);dress.hidden=new bool[dress.originals.Length];for(int i=0;i<dress.originals.Length;i++){dress.hidden[i]=dress.originals[i].forceRenderingOff;dress.originals[i].forceRenderingOff=true;}var spirit=SpiritModel();spirit.transform.SetParent(root,false);return;}
   // Accessories attach to existing bones. CharacterRig's API and animations are untouched.
   var body=Part(root,"npc/dress/"+npcId,b=>{
    if(npcId=="elder"){b.Frond(V(0,.52f,.19f),V(-.23f,.18f,.24f),.13f,Moss);b.Frond(V(0,.52f,.19f),V(.23f,.18f,.24f),.13f,Leaf);b.Gem(V(0,.35f,.24f),V(.09f,.13f,.04f),Glow(Jade));}
    else if(npcId=="hunter"){b.Box(V(.22f,.30f,-.23f),V(.18f,.5f,.13f),Bark);for(int i=0;i<3;i++){b.Rod(V(.16f+i*.05f,.3f,-.23f),V(.16f+i*.05f,.7f,-.23f),.009f,Bone,4);b.Frond(V(.16f+i*.05f,.64f,-.23f),V(.18f+i*.05f,.76f,-.23f),.025f,Moss);}}
    else if(npcId=="lost"){b.Box(V(0,.3f,-.25f),V(.40f,.45f,.19f),new Color(.59f,.39f,.22f));b.Rod(V(-.18f,.55f,-.25f),V(.18f,.55f,-.25f),.075f,Bone,6);b.Box(V(.18f,.1f,.17f),V(.18f,.15f,.08f),Bone);}
    else if(npcId=="herbalist"){b.Box(V(0,.22f,.19f),V(.29f,.36f,.025f),Bone);b.Rod(V(-.23f,.04f,.05f),V(-.23f,.2f,.05f),.10f,Bark,7);Leaves(b,V(-.23f,.22f,.05f),.16f,Leaf,7);}
    else {b.Gem(V(0,.42f,.21f),V(.20f,.22f,.04f),npcId=="guardian"?Jade:Bone,6);for(int s=-1;s<=1;s+=2)b.Frond(V(s*.12f,.5f,0),V(s*.28f,.41f,.08f),.09f,npcId=="guardian"?Moss:Bark);}
   });body.SetParent(BoneAt(rig.transform,"Spine"),false);dress.body=body;
   if(npcId=="elder"){var beard=Part(root,"npc/elderBeard",b=>{for(int i=-1;i<=1;i++)b.Gem(V(i*.035f,-.11f,.12f),V(.06f,.20f,.06f),Bone,5);});beard.SetParent(BoneAt(rig.transform,"Head"),false);dress.head=beard;}
  }
  internal static GameObject MarkModel(int state,bool alert=false)
  {
   var root=Root("Mark");Part(root.transform,"mark/"+state+"/"+alert,b=>{
    Color color=Glow(alert?new Color(1,.18f,.13f):new Color(1,.81f,.25f));
    if(state==1)b.Rod(V(0,.02f,0),V(0,.32f,0),.045f,color,6,.06f);
    else {Vector3[] p={V(-.12f,.21f,0),V(-.10f,.31f,0),V(.03f,.36f,0),V(.13f,.29f,0),V(.10f,.19f,0),V(0,.11f,0),V(0,.03f,0)};for(int i=1;i<p.Length;i++)b.Rod(p[i-1],p[i],.035f,color,6);}
    b.Gem(V(0,-.10f,0),V(.10f,.10f,.08f),color,6);
   }).name="Glyph";return root;
  }
  public static void QuestMark(Transform npc,int state)
  {if(!npc)return;var root=npc.Find("QuestMark");if(!root){root=SkyArt.Node(npc,"QuestMark",V(0,2.1f,0));for(int i=1;i<=2;i++){var m=MarkModel(i);m.name=i==1?"Exclaim":"Question";m.transform.SetParent(root,false);}}root.gameObject.SetActive(state==1||state==2);root.Find("Exclaim").gameObject.SetActive(state==1);root.Find("Question").gameObject.SetActive(state==2);}
  public static void AlertMark(Transform monster,bool callsHelp)
  {if(!monster)return;var t=monster.Find("AlertMark");if(!t){var g=MarkModel(1,true);g.name="AlertMark";g.transform.SetParent(monster,false);g.transform.localPosition=V(0,2.1f,0);t=g.transform;Part(t,"mark/caller",b=>{for(int s=-1;s<=1;s+=2)b.Frond(V(s*.16f,.15f,0),V(s*.32f,.15f,0),.04f,Glow(new Color(1,.3f,.2f)));}).name="CallerAccent";}t.gameObject.SetActive(true);t.Find("CallerAccent").gameObject.SetActive(callsHelp);}
  internal static GameObject BannerModel()
  {var root=Root("Banner");Part(root.transform,"ui/banner",b=>{b.Box(Vector3.zero,V(3,.42f,.025f),new Color(.09f,.25f,.18f));for(int s=-1;s<=1;s+=2){b.Rod(V(-1.5f,s*.21f,.02f),V(1.5f,s*.21f,.02f),.018f,Bone,4);b.Frond(V(s*1.4f,0,.02f),V(s*1.8f,.16f,.02f),.13f,Moss);b.Frond(V(s*1.4f,0,.02f),V(s*1.8f,-.16f,.02f),.13f,Leaf);}}).name="RibbonFrame";return root;}
  static ExploreLocationBanner banner;
  public static void LandmarkFound(ExLandmark l){if(!banner)banner=new GameObject("Explore location banner").AddComponent<ExploreLocationBanner>();banner.Title=l==null?"":l.name;banner.gameObject.SetActive(l!=null);}
 }
 public sealed class ExploreNpcDress:MonoBehaviour
 {
  internal string identity;internal Transform art,body,head;internal Renderer[] originals;internal bool[] hidden;
  internal void Restore(){if(originals!=null)for(int i=0;i<originals.Length;i++)if(originals[i])originals[i].forceRenderingOff=hidden[i];if(body)SkyArt.Dispose(body.gameObject);if(head)SkyArt.Dispose(head.gameObject);if(art)SkyArt.Dispose(art.gameObject);}
 }
 public sealed class ExploreLocationBanner:MonoBehaviour
 {
  public string Title;
  void OnGUI(){if(string.IsNullOrEmpty(Title))return;float w=Screen.width*.72f,h=Screen.height*.055f;var r=new Rect((Screen.width-w)*.5f,Screen.height*.10f,w,h);UiKit.Card(r,.9f,ExploreArt.Jade,.9f);UiKit.Text(r,Title,Mathf.RoundToInt(h*.47f),ExploreArt.Bone,TextAnchor.MiddleCenter);}
 }
}
