using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  // State setters intentionally snap. Another session supplies animation on these named children.
  public static GameObject CreateGate(int arena,float cell)
  {
   var root=Root("Vine gate "+arena);var closed=SkyArt.Node(root.transform,"Closed",Vector3.zero);
   for(int i=0;i<6;i++){int k=i;var t=Part(closed,"gate/vine/"+i,b=>{float x=-1.2f+k*.48f;b.Vine(V(x,0,.1f),V(-x,2.45f,.1f),k%2==0?.32f:-.32f,.15f,Bark,6);b.Vine(V(x,.1f,.18f),V(-x,2.3f,.18f),.28f,.025f,Glow(new Color(.45f,.16f,.62f)),6);});t.name="Vine_"+i;}
   root.transform.localScale=Vector3.one*(cell/3f);return root;
  }
  public static void SetGateOpen(GameObject gate,bool open){if(!gate)return;var c=gate.transform.Find("Closed");if(c)c.gameObject.SetActive(!open);}
  public static GameObject CreateSeal(ExSeal seal,int x,int z,float cell)
  {
   if(seal==null)return null;int type=seal.id=="seal2"?2:seal.id=="seal3"?3:seal.id=="seal4"?4:1;
   var root=Root(seal.name);var state=root.AddComponent<ExploreSealState>();state.source=seal;
   Part(root.transform,"seal/frame/"+type,b=>{for(int s=-1;s<=1;s+=2)b.Rod(V(s*1.3f,0,0),V(s*1.3f,2.8f,0),.17f,type==2?Bark:Rock,6);b.Rod(V(-1.3f,2.8f,0),V(1.3f,2.8f,0),.16f,type==2?Bark:Jade,6);}).name="Frame";
   var closed=SkyArt.Node(root.transform,"Closed",Vector3.zero);
   if(type==1||type==3)
   {for(int i=0;i<(type==3?2:5);i++){int k=i;var t=Part(closed,"seal/root/"+type+"/"+i,b=>{float side=type==3?(k==0?-.65f:.65f):-.9f+k*.45f;b.Vine(V(side,0,0),V(-side,2.5f,0),.45f,.16f,Leaf,7);b.Vine(V(-side,.1f,.08f),V(side,2.45f,.08f),-.35f,.12f,Moss,7);});t.name="RootKnot_"+i;}}
   else if(type==2)
   {
    for(int s=-1;s<=1;s+=2){int side=s;var pivot=SkyArt.Node(closed,s<0?"DoorLeft":"DoorRight",V(s*1.22f,0,0));Part(pivot,"seal/wood/"+s,b=>{for(int i=0;i<3;i++)b.Rod(V(-side*(.2f+i*.4f),0,0),V(-side*(.2f+i*.4f),2.5f,0),.22f,Bark,5,.15f);b.Rod(V(0,1, .25f),V(-side*1.2f,1,.25f),.055f,Bone,4);});}
    Part(closed,"seal/lock",b=>{b.Box(V(0,1.2f,.40f),V(.65f,.60f,.22f),new Color(.67f,.45f,.16f));b.Vine(V(-.23f,1.45f,.4f),V(.23f,1.45f,.4f),.0f,.06f,Bone,2);b.Rod(V(-.23f,1.45f,.4f),V(-.23f,1.8f,.4f),.06f,Bone);b.Rod(V(.23f,1.45f,.4f),V(.23f,1.8f,.4f),.06f,Bone);b.Rod(V(-.23f,1.8f,.4f),V(.23f,1.8f,.4f),.06f,Bone);b.Gem(V(0,1.22f,.53f),V(.1f,.18f,.02f),Dark);}).name="BronzeLock";
   }
   else
   {
    Part(closed,"seal/jadeDoor",b=>{b.Box(V(0,1.3f,0),V(2.35f,2.6f,.4f),Rock);for(int s=-1;s<=1;s+=2)b.Vine(V(s*.9f,2.55f,.23f),V(s*.7f,.1f,.23f),s*.22f,.045f,Leaf,7);}).name="StoneDoor";
    for(int i=0;i<3;i++){var socket=SkyArt.Node(closed,"Socket_"+i,V((i-1)*.65f,1.6f,.24f));Part(socket,"seal/socket",b=>b.Gem(Vector3.zero,V(.45f,.65f,.12f),Dark));Part(socket,"seal/socketLit",b=>b.Gem(Vector3.zero,V(.32f,.49f,.18f),Glow(Jade))).name="Lit";}
   }
   root.transform.localScale=Vector3.one*(cell/3f);SetSealOpen(root,seal.open);return root;
  }
  public static void SetSealOpen(GameObject g,bool open)
  {if(!g)return;SetGateOpen(g,open);var state=g.GetComponent<ExploreSealState>();if(state&&state.source!=null)for(int i=0;i<3;i++){var socket=g.transform.Find("Closed/Socket_"+i+"/Lit");if(socket)socket.gameObject.SetActive(i<state.source.have);}}
  public static GameObject CreateSwitch(ExSwitch sw)
  {
   if(sw==null)return null;var root=Root("Switch "+sw.kind);var state=root.AddComponent<ExploreSwitchState>();state.lever=sw.kind=="lever";
   if(state.lever)
   {
    Part(root.transform,"switch/base",b=>{b.Rod(Vector3.zero,V(0,.25f,0),.48f,Rock,8);for(int i=0;i<4;i++){float a=i*Mathf.PI/2;b.Gem(V(Mathf.Cos(a)*.4f,.32f,Mathf.Sin(a)*.4f),V(.12f,.2f,.12f),Jade);}}).name="Pedestal";
    var plate=Part(root.transform,"switch/plate",b=>b.Rod(V(0,0,0),V(0,.12f,0),.34f,Rock,8),V(0,.25f,0));plate.name="PressurePlate";
    Part(plate,"switch/rune",b=>{b.Ring(V(0,.125f,0),.23f,.03f,Glow(Jade),12);b.Quad(V(0,.127f,.18f),V(.13f,.127f,0),V(0,.127f,-.18f),V(-.13f,.127f,0),Glow(Jade));}).name="Rune";
   }
   else
   {
    Part(root.transform,"switch/rest",b=>b.Gem(V(0,.12f,0),V(.45f,.24f,.45f),Rock));
    Part(root.transform,sw.kind=="key"?"switch/key":"switch/shard",b=>{if(sw.kind=="key"){var gold=new Color(.76f,.50f,.17f);b.Rod(V(0,-.30f,0),V(0,.16f,0),.045f,gold,7);for(int j=0;j<2;j++)b.Box(V(.075f,-.22f+j*.12f,0),V(.16f,.06f,.08f),gold);for(int i=0;i<10;i++){float a=i*Mathf.PI/5,d=(i+1)*Mathf.PI/5;b.Rod(V(Mathf.Cos(a)*.13f,.28f+Mathf.Sin(a)*.13f,0),V(Mathf.Cos(d)*.13f,.28f+Mathf.Sin(d)*.13f,0),.035f,gold,5);}}else b.Gem(Vector3.zero,V(.27f,.70f,.26f),Glow(Jade),5);},V(0,1,0)).name=sw.kind=="key"?"BronzeKey":"JadeShard";
   }
   if(sw.done)SetSwitchDone(root);return root;
  }
  public static void SetSwitchDone(GameObject g)
  {if(!g)return;var p=g.transform.Find("PressurePlate");if(p){p.localPosition=V(0,.11f,0);var r=p.Find("Rune");if(r)r.localScale=Vector3.one*1.08f;}else{var k=g.transform.Find("BronzeKey");if(!k)k=g.transform.Find("JadeShard");if(k)k.gameObject.SetActive(false);}}
 }
 public sealed class ExploreSealState:MonoBehaviour { internal ExSeal source; }
 public sealed class ExploreSwitchState:MonoBehaviour { internal bool lever; }
}
