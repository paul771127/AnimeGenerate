using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  static readonly Dictionary<int,Material> dungeonFloors=new Dictionary<int,Material>();
  internal static void DressDungeonPath(GameObject root,ExploreMap map,int x,int z)
  {
   int theme=DungeonThemeAt(map,x,z);var near=DungeonLayout(map).nearest[x,z];var area=near!=null&&near.Contains(new Vector2Int(x,z))?near:null;
   int key=theme*2+(area!=null?1:0);
   if(!dungeonFloors.TryGetValue(key,out var mat)||!mat)
   {
    mat=new Material(Resources.Load<Shader>("Shaders/DungeonExploreFloor")){name="Dungeon cached floor "+key};
    ExGround ground=theme==0?ExGround.Cave:theme==2?ExGround.Shore:theme==3||theme==5?ExGround.Stone:ExGround.Dirt;
    mat.mainTexture=GroundTexture(ground);mat.SetFloat("_Theme",theme);mat.SetFloat("_Room",area!=null?1:0);
    mat.SetColor("_Accent",DungeonThemeColor(theme));dungeonFloors[key]=mat;
   }
   var surface=root.transform.Find("PathSurface").GetComponent<Renderer>();surface.sharedMaterial=mat;
   var block=new MaterialPropertyBlock();surface.GetPropertyBlock(block);int edges=0;
   if(area!=null){if(x==area.rect.xMax)edges|=1;if(x==area.rect.xMin)edges|=2;if(z==area.rect.yMax)edges|=4;if(z==area.rect.yMin)edges|=8;}
   block.SetFloat("_RoomEdge",edges);block.SetFloat("_RegionTheme",DungeonRegionAt(map,x,z));block.SetColor("_Accent",DungeonRegionColor(map,x,z));surface.SetPropertyBlock(block);
   var shoulder=root.transform.Find("BlockedShoulder");if(shoulder)shoulder.gameObject.SetActive(false);
   if(area!=null&&x==area.rect.xMin+area.rect.width/2&&z==area.rect.yMin+area.rect.height/2)
   {
    int kind=area.IsBoss?3:area.IsChest?1:area.IsEvent?2:0;
    Part(root.transform,"dungeon/roomMark/"+theme+"/"+DungeonRegionAt(map,x,z)+"/"+kind,b=>{
     Color c=DungeonRegionColor(map,x,z);c.a=.70f;
     b.Ring(V(0,.016f,0),kind==3?1.65f:1.25f,.04f,c,32);
     if(kind==1){b.Quad(V(-.55f,.017f,-.32f),V(-.55f,.017f,.35f),V(.55f,.017f,.35f),V(.55f,.017f,-.32f),new Color(.72f,.54f,.25f,.55f));b.Ring(V(0,.018f,0),.22f,.06f,c,8);}
     else for(int i=0;i<(kind==2?8:4);i++){float a=i*Mathf.PI*2/(kind==2?8:4);var p=V(Mathf.Cos(a)*.95f,.018f,Mathf.Sin(a)*.95f);b.Gem(p,V(.20f,.005f,.30f),c,4);}
    },Vector3.zero,true).name="RoomInsignia";
   }
  }
  public static GameObject CreateDungeonArena(int theme,float radius)
  {
   var g=Root("Dungeon boss arena perimeter");Part(g.transform,"dungeon/arena/"+theme,b=>{var c=DungeonThemeColor(theme);c.a=.45f;b.Ring(V(0,.012f,0),1,.008f,c,64);},Vector3.zero,true).localScale=new Vector3(radius*.94f,1,radius*.94f);return g;
  }
  public static GameObject CreateDungeonGate(ExploreMap map,int x,int z)
  {
   int theme=DungeonThemeAt(map,x,z);var key=new Vector2Int(x,z);var layout=DungeonLayout(map);bool open=map.GateOpen(x,z);
   if(!open)layout.closedSeen.Add(key);
   else if(!layout.openedAt.ContainsKey(key))layout.openedAt[key]=layout.closedSeen.Contains(key)?Time.time:Time.time-1;
   var root=Root("Dungeon seal "+theme);float half=ExploreMap.CellSize*.5f;
   Part(root.transform,"dungeon/gateFrame/"+theme,b=>{
    var c=theme==1?Bark:DungeonRock(theme);for(int s=-1;s<=1;s+=2){b.Rod(V(s*(half-.13f),0,0),V(s*(half-.13f),3.5f,0),.12f,c,8);b.Gem(V(s*(half-.13f),3.55f,0),V(.24f,.4f,.24f),DungeonThemeColor(theme),6);}
    b.Rod(V(-half+.13f,3.4f,0),V(half-.13f,3.4f,0),.12f,c,8);
   }).name="Frame";
   var moving=SkyArt.Node(root.transform,"Closed",Vector3.zero);
   Part(moving,"dungeon/gateBars/"+theme,b=>{
    var c=theme==1?Bark:theme==5?new Color(.75f,.54f,.24f):DungeonRock(theme);
    for(int i=0;i<7;i++){float px=(i-3)*.51f;if(theme==1)b.Vine(V(px,0,0),V(-px,3.25f,0),.25f,.08f,c,6);else b.Rod(V(px,0,0),V(px,3.25f,0),.065f,c,6);}
    for(int i=0;i<2;i++)b.Rod(V(-1.65f,.7f+i*1.5f,0),V(1.65f,.7f+i*1.5f,0),.07f,c,6);
   });
   var rune=Part(moving,"dungeon/gateRune/"+theme+"/"+DungeonRegionAt(map,x,z),b=>{var c=DungeonRegionColor(map,x,z);c.a=.55f;b.Ring(Vector3.zero,.66f,.035f,c,28);for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;b.Gem(V(Mathf.Cos(a)*.76f,0,Mathf.Sin(a)*.76f),V(.17f,.025f,.17f),c,4);}},V(0,1.6f,.15f),true);rune.localRotation=Quaternion.Euler(90,0,0);
   var state=root.AddComponent<DungeonGateArt>();state.closed=moving;state.open=open;state.elapsed=open?Time.time-layout.openedAt[key]:0;state.Sample(state.elapsed);return root;
  }
  static GameObject CreateDungeonBackdrop(ExploreMap map)
  {
   int theme=map.nodeAreas?5:map.theme=="volcano"?0:map.theme=="sea"?2:map.theme=="arena"?3:4;
   var root=Root("Dungeon horizon "+theme);root.transform.localPosition=(map.World(0,0)+map.World(map.W-1,map.H-1))*.5f;
   float radius=Mathf.Max(map.W,map.H)*ExploreMap.CellSize;
   Color low=theme==0?new Color(.58f,.23f,.14f):theme==2?new Color(.68f,.86f,.89f):theme==5?new Color(.32f,.29f,.22f):new Color(.86f,.68f,.46f);
   Color high=theme==0?new Color(.12f,.13f,.21f):theme==5?new Color(.09f,.14f,.20f):new Color(.21f,.46f,.67f);
   var sky=Part(root.transform,"dungeon/sky/"+theme,b=>{
    for(int j=0;j<8;j++)for(int i=0;i<32;i++){float a=i*Mathf.PI/16,z=(i+1)*Mathf.PI/16,l=j*Mathf.PI/16,h=(j+1)*Mathf.PI/16;b.Quad(V(Mathf.Cos(a)*Mathf.Cos(l),Mathf.Sin(l),Mathf.Sin(a)*Mathf.Cos(l)),V(Mathf.Cos(z)*Mathf.Cos(l),Mathf.Sin(l),Mathf.Sin(z)*Mathf.Cos(l)),V(Mathf.Cos(z)*Mathf.Cos(h),Mathf.Sin(h),Mathf.Sin(z)*Mathf.Cos(h)),V(Mathf.Cos(a)*Mathf.Cos(h),Mathf.Sin(h),Mathf.Sin(a)*Mathf.Cos(h)),Color.Lerp(low,high,j/8f));}b.HeightTint(0,0,1,low,high);
   },V(0,-2,0),true);sky.localScale=Vector3.one*radius*1.7f;BackdropLayer(sky,1000);
   for(int i=0;i<16;i++){int n=i%4;float angle=i*Mathf.PI/8;var distant=Part(root.transform,"dungeon/horizon/"+theme+"/"+n,b=>{
    if(theme==2){for(int k=0;k<3;k++)b.Crag(V((k-1)*9,0,0),V(15,5+k*2,14),new Color(.35f,.56f,.59f),k+n);}
    else if(theme==5){for(int k=0;k<3;k++)b.Box(V((k-1)*7,8,0),V(5,16+k*2,6),Color.Lerp(high,low,.3f));}
    else for(int k=0;k<3;k++)b.Crag(V((k-1)*8,0,0),V(16,12+(k+n)%3*5,12),Color.Lerp(low,high,.5f),k+n);
   },V(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius),true);distant.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);BackdropLayer(distant,1400);}
   // 地平線以下的地面碗（不透出現實背景）：各主題的地面色
   {HorizonColors(map,out var gc,out var sl);GroundBowl(root.transform,gc,sl,radius*1.7f);}
   return root;
  }
  public static void DrawDungeonAreaMap(ExploreRun run,Rect box,int x0,int z0,int span)
  {
   var map=run.map;if(map.dungeon==null)return;float cs=box.width/span;var old=GUI.color;GUI.color=Color.white;
   foreach(var a in map.areas)
   {
    int cx=a.rect.xMin+a.rect.width/2,cz=a.rect.yMin+a.rect.height/2;if(cx<x0||cz<z0||cx>=x0+span||cz>=z0+span||!run.explored.Contains(new Vector2Int(cx,cz)))continue;
    float px=box.x+(cx-x0+.5f)*cs,py=box.y+(span-1-(cz-z0)+.5f)*cs,s=Mathf.Clamp(cs*2,10,28);
    var col=DungeonRegionColor(map,cx,cz);if(a.entered&&!a.cleared)col=Color.Lerp(col,Color.white,.35f+.30f*Mathf.Sin(Time.time*4));
    UiKit.Disc(new Rect(px-s*.65f,py-s*.65f,s*1.3f,s*1.3f),new Color(.06f,.09f,.13f,.95f));
    if(a.IsBoss){for(int i=-1;i<=1;i++){float xx=px+i*s*.35f;UiKit.Gem(new Rect(xx-s*.12f,py-s*(i==0?.5f:.35f),s*.24f,s*.6f),col);}UiKit.Divider(new Rect(px-s*.5f,py+s*.1f,s,s*.2f),col);}
    else if(a.IsChest){UiKit.Divider(new Rect(px-s*.4f,py-s*.24f,s*.8f,s*.52f),col);UiKit.Divider(new Rect(px-s*.06f,py-s*.24f,s*.12f,s*.52f),UiKit.Theme.Ink);}
    else UiKit.Gem(new Rect(px-s*.35f,py-s*.35f,s*.7f,s*.7f),col);
    if(a.cleared)
    {
     float bx=px+s*.46f,by=py+s*.40f,k=s*.5f;
     UiKit.Disc(new Rect(bx-k*.65f,by-k*.65f,k*1.3f,k*1.3f),UiKit.Theme.Ink);
     DungeonMapLine(new Vector2(bx-k*.4f,by),new Vector2(bx-k*.08f,by+k*.3f),k*.19f,UiKit.Theme.Positive);
     DungeonMapLine(new Vector2(bx-k*.08f,by+k*.3f),new Vector2(bx+k*.42f,by-k*.35f),k*.19f,UiKit.Theme.Positive);
    }
   }
   GUI.color=old;
  }
  static void DungeonMapLine(Vector2 a,Vector2 b,float width,Color c){var matrix=GUI.matrix;var d=b-a;GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);UiKit.Divider(new Rect(a.x,a.y-width*.5f,d.magnitude,width),c);GUI.matrix=matrix;}
 }
 public sealed class DungeonGateArt:MonoBehaviour
 {
  public Transform closed;public bool open;public float elapsed;
  public void Sample(float seconds){if(!closed)return;float u=open?Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds/.75f)):0;closed.localPosition=Vector3.up*(3.7f*u);closed.gameObject.SetActive(u<1);}
  void Update(){if(open){elapsed+=Time.deltaTime;Sample(elapsed);}}
 }
}
