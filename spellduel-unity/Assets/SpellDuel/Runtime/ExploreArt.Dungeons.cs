using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  // Presentation lookup only: never writes cells, props, doors, area state, RNG or difficulty.
  sealed class DungeonArtLayout
  {
   internal readonly int[,] theme; internal readonly ExArea[,] nearest;
   internal readonly HashSet<Vector2Int> closedSeen=new HashSet<Vector2Int>();
   internal readonly Dictionary<Vector2Int,float> openedAt=new Dictionary<Vector2Int,float>();
   internal DungeonArtLayout(ExploreMap map)
   {
    theme=new int[map.W,map.H];nearest=new ExArea[map.W,map.H];
    for(int x=0;x<map.W;x++)for(int z=0;z<map.H;z++)
    {
     ExArea best=null;float distance=float.MaxValue;
     foreach(var a in map.areas){float dx=Mathf.Max(a.rect.xMin-x,0,x-a.rect.xMax),dz=Mathf.Max(a.rect.yMin-z,0,z-a.rect.yMax);float d=dx*dx+dz*dz;if(d<distance){distance=d;best=a;}}
     nearest[x,z]=best;theme[x,z]=map.nodeAreas?5:best!=null?Mathf.Clamp(map.dungeon.ThemeOf(best.node),0,4):0;
    }
   }
  }
  static readonly ConditionalWeakTable<ExploreMap,DungeonArtLayout> dungeonLayouts=new ConditionalWeakTable<ExploreMap,DungeonArtLayout>();
  static DungeonArtLayout DungeonLayout(ExploreMap m)=>dungeonLayouts.GetValue(m,map=>new DungeonArtLayout(map));
  public static int DungeonThemeAt(ExploreMap m,int x,int z)
  {if(m.nodeAreas)return 5;return m.InGrid(x,z)?DungeonLayout(m).theme[x,z]:0;}
  public static Color DungeonThemeColor(int theme)=>theme==0?new Color(1,.36f,.12f):theme==1?new Color(.38f,.80f,.47f):theme==2?new Color(.28f,.77f,.88f):theme==3?new Color(.86f,.34f,.22f):theme==4?new Color(.88f,.61f,.31f):new Color(.94f,.72f,.30f);
  static Color DungeonRock(int t)=>t==0?new Color(.22f,.24f,.30f):t==2?new Color(.31f,.48f,.52f):t==4?new Color(.59f,.32f,.22f):t==5?new Color(.28f,.34f,.39f):new Color(.48f,.43f,.34f);
  public static ExGround DungeonBaseGround(ExploreMap m)=>m.nodeAreas?ExGround.Stone:m.theme=="volcano"?ExGround.Cave:m.theme=="sea"?ExGround.Shore:m.theme=="arena"?ExGround.Stone:m.theme=="canyon"?ExGround.Dirt:ExGround.Grass;
  internal static GameObject CreateDungeonWall(ExploreMap m,int x,int z)
  {
   int theme=DungeonThemeAt(m,x,z),n=Variant(x,z);var a=m.InGrid(x,z)?DungeonLayout(m).nearest[x,z]:null;
   int role=a!=null&&x==a.rect.xMin+a.rect.width/2&&z==a.rect.yMax+1?(a.IsBoss?3:a.IsChest?1:a.IsEvent?2:4):0;
   GameObject root;
   if(theme==1&&role==0)root=CreateWall(ExTerrain.Thicket,x,z,ExploreMap.CellSize);
   else
   {
    root=Root("Dungeon wall "+theme+" "+n);
    Part(root.transform,"dungeon/wall/"+theme+"/"+n+"/"+role,b=>{DungeonWallBody(b,theme,n,role);b.ClampCell(1.49f);}).name="WallBody";
    Part(root.transform,"dungeon/glow/"+theme+"/"+n+"/"+role,b=>{DungeonWallGlow(b,theme,n,role);b.ClampCell(1.49f);},Vector3.zero,true).name="InlaidLight";
    root.transform.localScale=Vector3.one*(ExploreMap.CellSize/3f);
   }
   // A wall's facade faces the adjacent path; the entire solid silhouette stays in its wall cell.
   int face=0;for(int i=0;i<4;i++){var d=ExploreMap.Dirs[i];if(m.At(x+d.x,z+d.y)!=ExCell.Wall){face=i;break;}}
   if(theme!=1||role!=0){float angle=face==0?90:face==1?-90:face==2?0:180;foreach(Transform part in root.transform)part.localRotation=Quaternion.Euler(0,angle,0);}
   if(n==0||theme==1)
   {
    var mist=Part(root.transform,"dungeon/air/"+theme,b=>{
     Color c=theme==0?new Color(.5f,.40f,.34f,.11f):theme==2?new Color(.6f,.88f,.9f,.08f):new Color(.76f,.84f,.75f,.09f);
     for(int i=0;i<3;i++)b.Crown(V((i-1)*.45f,.8f+i*.3f,.3f),V(.75f,.35f,.65f),c,i,6,2);
    },Vector3.zero,true);mist.name="DriftingMist";var motion=mist.gameObject.AddComponent<DungeonMistArt>();motion.origin=mist.localPosition;motion.phase=n+x*.17f+z*.11f;
   }
   return root;
  }
  static void DungeonWallBody(Geo b,int t,int n,int role)
  {
   var stone=DungeonRock(t);var metal=new Color(.29f,.34f,.39f);var brass=new Color(.70f,.49f,.19f);
   bool alcove=n==0||role>0;
   if(t==3)
   {
    for(int j=0;j<3;j++)b.Box(V(0,.23f+j*.34f,-.85f+j*.18f),V(2.9f,.46f,1.1f),Color.Lerp(stone,Bone,j*.08f));
    for(int i=0;i<5;i++){float x=(i-2)*.57f;b.Rod(V(x,0,-.4f),V(x,2.4f+(i%2)*.22f,-.4f),.20f,Bark,6,.13f);b.Rod(V(x,2.4f+(i%2)*.22f,-.4f),V(x,2.7f+(i%2)*.22f,-.4f),.13f,Bone,6,0);}
    for(int j=0;j<2;j++)b.Box(V(0,.8f+j,-.16f),V(2.9f,.13f,.13f),brass);
    b.Rod(V(-1.2f,0,1.1f),V(-1.2f,3,1.1f),.055f,brass,6);b.Quad(V(-1.15f,2.9f,1.1f),V(-.2f,2.75f,1.1f),V(-.32f,1.8f,1.1f),V(-1.15f,2.1f,1.1f),DungeonThemeColor(t),true);
   }
   else if(t==5)
   {
    b.Box(V(0,1.4f,-.7f),V(2.95f,2.8f,1.4f),metal);
    for(int i=0;i<3;i++){float x=(i-1)*.94f;b.Box(V(x,1.4f,.02f),V(.88f,2.65f,.09f),Color.Lerp(metal,Bone,.08f));for(int y=0;y<4;y++)b.Gem(V(x-.32f,.25f+y*.75f,.09f),V(.085f,.085f,.06f),brass,6);}
    for(int s=-1;s<=1;s+=2){b.Rod(V(s*1.2f,.1f,.3f),V(s*1.2f,2.6f,.3f),.10f,brass,8);b.Rod(V(s*1.2f,2.6f,.3f),V(s*.55f,2.6f,.3f),.1f,brass,8);}
    DungeonGear(b,V(0,1.7f,.27f),.62f,brass);DungeonGear(b,V(.72f,2.33f,.25f),.32f,metal);
   }
   else
   {
    b.Crag(V(0,0,alcove?-.65f:0),V(2.96f,2.5f+n*.22f,alcove?1.55f:2.96f),stone,n+7*t);
    for(int j=0;j<3;j++)b.Crag(V((j-1)*.95f,0,alcove?-.15f:.9f),V(.85f,.4f+(j%2)*.18f,.8f),Color.Lerp(stone,Bone,.10f),j+n);
    // Crag height bands supply canyon strata without floating horizontal strips.
   }
   if(role==3)
   {
    b.Box(V(0,.18f,.72f),V(2.4f,.36f,1.3f),stone);b.Box(V(0,.67f,.64f),V(1.05f,.38f,.85f),stone);
    b.Box(V(0,1.45f,.30f),V(1.15f,1.6f,.3f),stone);for(int s=-1;s<=1;s+=2){b.Rod(V(s*.68f,.3f,.65f),V(s*.68f,1.25f,.65f),.14f,brass,6);b.Gem(V(s*.68f,1.45f,.65f),V(.3f,.5f,.28f),DungeonThemeColor(t),6);}
    for(int i=0;i<3;i++)b.Gem(V((i-1)*.35f,2.3f,.3f),V(.25f,i==1?.75f:.5f,.22f),brass,4);
   }
   else if(role==1)
   {
    b.Rod(V(0,0,.6f),V(0,.3f,.6f),.72f,stone,10);b.Box(V(0,.58f,.6f),V(1,.55f,.65f),Bark);b.Crown(V(0,.86f,.6f),V(1.02f,.35f,.67f),brass,n,8,2);
    for(int s=-1;s<=1;s+=2)b.Box(V(s*.34f,.65f,.95f),V(.09f,.6f,.045f),brass);b.Box(V(0,.67f,.96f),V(.18f,.20f,.05f),brass);
   }
   else if(role==4)
   {
    b.Quad(V(-1.05f,0,.25f),V(0,1.5f,.25f),V(0,1.5f,1.35f),V(-1.05f,0,1.35f),Color.Lerp(DungeonThemeColor(t),Bone,.45f),true);
    b.Quad(V(0,1.5f,.25f),V(1.05f,0,.25f),V(1.05f,0,1.35f),V(0,1.5f,1.35f),Color.Lerp(DungeonThemeColor(t),Bark,.45f),true);b.Rod(V(0,0,1.35f),V(0,1.5f,1.35f),.045f,Bark,5);
   }
   else if(alcove)DungeonProp(b,t,n);
   if(t==1){for(int i=0;i<3;i++){float x=(i-1)*.8f;b.Frond(V(x,.1f,.9f),V(x+.2f,.9f,1.15f),.18f,Moss);}b.Rod(V(1.1f,0,1),V(1.1f,2.5f,1),.055f,Bark,5);b.Rod(V(1.1f,2.5f,1),V(.65f,2.5f,1),.045f,Bark,5);b.Box(V(.65f,2.12f,1),V(.33f,.50f,.33f),brass);}
   if(t==2)for(int i=0;i<3;i++){float x=(i-1)*.8f;var c=i%2==0?new Color(.90f,.43f,.36f):new Color(.62f,.42f,.75f);b.Rod(V(x,0,1.2f),V(x,.7f,1.2f),.06f,c,5,.025f);for(int s=-1;s<=1;s+=2)b.Rod(V(x,.3f,1.2f),V(x+s*.24f,.62f,1.22f),.038f,c,5,.012f);b.Frond(V(x,.04f,.9f),V(x-.12f,.95f,1),.09f,new Color(.18f,.52f,.37f));}
  }
  static void DungeonProp(Geo b,int t,int n)
  {
   var iron=new Color(.30f,.35f,.39f);var gold=new Color(.72f,.52f,.22f);
   if(t==0)
   {
    for(int s=-1;s<=1;s+=2)b.Box(V(s*.45f,.06f,.45f),V(.065f,.10f,1.9f),iron);
    for(int i=0;i<5;i++)b.Box(V(0,.03f,-.3f+i*.35f),V(1.25f,.06f,.12f),Bark);
    b.Box(V(0,.68f,.5f),V(1.15f,.62f,.90f),iron);
    for(int s=-1;s<=1;s+=2)for(int j=0;j<2;j++)b.Rod(V(s*.63f,.24f,.2f+j*.60f),V(s*.70f,.24f,.2f+j*.60f),.22f,iron,10);
    for(int i=0;i<3;i++)b.Crag(V((i-1)*.3f,.95f,.5f),V(.4f,.32f,.4f),new Color(.19f,.17f,.23f),i);
    b.Rod(V(.96f,0,.9f),V(.96f,.6f,.9f),.30f,gold,8,.39f);b.Ring(V(.96f,.61f,.9f),.39f,.055f,iron,12);
   }
   else if(t==2)
   {
    for(int i=0;i<5;i++){float x=(i-2)*.26f;b.Rod(V(x,.1f,-.05f),V(x,.5f,1.2f),.085f,Bark,5);}
    for(int i=0;i<3;i++)b.Vine(V(-.65f,.18f,i*.43f),V(.65f,.18f,i*.43f),.1f,.06f,Bone,5);
    b.Rod(V(0,.3f,.3f),V(.22f,1.8f,.4f),.07f,Bark,6,.035f);b.Quad(V(.18f,1.7f,.4f),V(.83f,.96f,.4f),V(.12f,.85f,.4f),V(.1f,1,.4f),new Color(.86f,.83f,.66f),true);
    for(int i=0;i<3;i++)b.Crown(V(-.8f+i*.7f,.06f,1.1f),V(.25f,.1f,.18f),Bone,i,7,2);
   }
   else if(t==3)
   {
    for(int s=-1;s<=1;s+=2)b.Rod(V(s*.8f,0,.8f),V(s*.8f,1.6f,.8f),.06f,Bark,6);
    b.Box(V(0,1.25f,.8f),V(1.8f,.10f,.14f),Bark);for(int i=0;i<3;i++){float x=(i-1)*.48f;b.Rod(V(x,.05f,.6f),V(x,1.4f,.6f),.045f,Bark,6);b.Gem(V(x,1.7f,.6f),V(.20f,.62f,.10f),iron,4);}
   }
   else if(t==4)
   {
    b.Crag(V(-.7f,0,.6f),V(.7f,2,.7f),new Color(.74f,.46f,.26f),4);
    b.Rod(V(.6f,0,.8f),V(.4f,1.6f,.8f),.12f,Bark,6,.035f);b.Rod(V(.45f,1.1f,.8f),V(.95f,1.8f,.7f),.055f,Bark,5,.015f);b.Rod(V(.45f,.8f,.8f),V(.1f,1.3f,.75f),.06f,Bark,5,.012f);
   }
   else if(t==5)
   {b.Rod(V(-.8f,.30f,.8f),V(.85f,.30f,.8f),.16f,gold,8);b.Rod(V(.85f,.30f,.8f),V(.85f,1.3f,.8f),.16f,gold,8);b.Ring(V(.85f,1.3f,.8f),.23f,.07f,iron,10);DungeonGear(b,V(-.4f,.85f,.8f),.38f,gold);}
  }
  static void DungeonGear(Geo b,Vector3 p,float r,Color col)
  {
   for(int i=0;i<16;i++){float a=i*Mathf.PI/8,z=(i+1)*Mathf.PI/8;float ra=i%2==0?r:r*.82f,rb=(i+1)%2==0?r:r*.82f;var c=V(Mathf.Cos(a),Mathf.Sin(a),0);var d=V(Mathf.Cos(z),Mathf.Sin(z),0);b.Quad(p+c*r*.43f,p+c*ra,p+d*rb,p+d*r*.43f,col,true);}
   for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;b.Rod(p,p+V(Mathf.Cos(a)*r*.5f,Mathf.Sin(a)*r*.5f,0),.055f,col,4);}
  }
  static bool DungeonCragFront(Geo rock,float x,float y,out float z)
  {
   z=float.NegativeInfinity;
   for(int k=0;k<rock.v.Count;k+=3)
   {
    var a=rock.v[k];var b=rock.v[k+1];var c=rock.v[k+2];
    float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<.00001f)continue;
    float u=((b.y-c.y)*(x-c.x)+(c.x-b.x)*(y-c.y))/det;
    float v=((c.y-a.y)*(x-c.x)+(a.x-c.x)*(y-c.y))/det,w=1-u-v;
    if(u>=0&&v>=0&&w>=0)z=Mathf.Max(z,u*a.z+v*b.z+w*c.z);
   }
   return !float.IsNegativeInfinity(z);
  }
  static void DungeonWallGlow(Geo b,int t,int n,int role)
  {
   var c=DungeonThemeColor(t);c.a=.55f;
   if(t==0)
   {
    // Sample the actual crag faces once when this cached mesh is built; cracks hug stone.
    bool alcove=n==0||role>0;var rock=new Geo();
    rock.Crag(V(0,0,alcove?-.65f:0),V(2.96f,2.5f+n*.22f,alcove?1.55f:2.96f),DungeonRock(t),n);
    for(int i=0;i<3;i++)
    {
     Vector3 last=Vector3.zero;bool previous=false;
     for(int j=0;j<=8;j++)
     {
      float u=j/8f,x=(i-1)*.70f+u*.22f+Mathf.Sin(u*9+i)*.08f,y=.25f+u*2.05f;
      bool hit=DungeonCragFront(rock,x,y,out float z);var at=V(x,y,z+.012f);
      if(hit&&previous)b.Rod(last,at,.018f,c,4);last=at;previous=hit;
     }
    }
   }
   if(t==1)b.Box(V(.65f,2.12f,1.18f),V(.23f,.37f,.02f),new Color(.87f,1,.5f,.65f));
   if(t==5){b.Box(V(0,.72f,.14f),V(1.9f,.035f,.015f),new Color(.32f,.82f,.91f,.48f));}
   if(role==4){b.Ring(V(1.13f,.04f,1.15f),.25f,.08f,new Color(1,.40f,.09f,.8f),10);FlameTongue(b,V(1.13f,.06f,1.15f),.65f,.12f,0,new Color(1,.6f,.14f,.7f),n);}
   if(role==2){b.Ring(V(0,.025f,.8f),.56f,.045f,c,24);for(int i=0;i<6;i++){float a=i*Mathf.PI/3;b.Gem(V(Mathf.Cos(a)*.7f,.045f,.8f+Mathf.Sin(a)*.5f),V(.12f,.03f,.12f),c,4);}}
  }
 }
 public sealed class DungeonMistArt:MonoBehaviour
 {
  public Vector3 origin; public float phase;
  void Update(){transform.localPosition=origin+new Vector3(Mathf.Sin(Time.time*.3f+phase)*.12f,Mathf.Sin(Time.time*.4f+phase)*.15f,0);}
 }
}
