using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  public static GameObject CreateLandmark(ExLandmark l,ExploreMap map)
  {
   if(l==null||map==null)return null;var root=Root("Landmark "+l.id);var center=map.World((l.rect.xMin+l.rect.xMax)/2,(l.rect.yMin+l.rect.yMax)/2);root.transform.localPosition=center;
   // The cell streamer may omit enclosed wall cells. A lake must still have a continuous surface.
   if(l.kind=="lake")
   {
    var water=Part(root.transform,"landmark/lakeSurface/"+l.id,b=>{for(int x=l.rect.xMin-1;x<=l.rect.xMax+1;x++)for(int z=l.rect.yMin-1;z<=l.rect.yMax+1;z++)if(map.InGrid(x,z)&&map.TerrainAt(x,z)==ExTerrain.Water){var at=map.World(x,z)-center;b.Quad(at+V(-1.5f,.012f,-1.5f),at+V(-1.5f,.012f,1.5f),at+V(1.5f,.012f,1.5f),at+V(1.5f,.012f,-1.5f),Color.white);}});
    water.name="LakeSurface";water.GetComponent<Renderer>().sharedMaterial=LakeMaterial;
   }
   // Only solid wall cells carry tall scenery. Never span a path with roofs or tree crowns.
   int placed=0;
   for(int x=l.rect.xMin-1;x<=l.rect.xMax+1;x++)for(int z=l.rect.yMin-1;z<=l.rect.yMax+1;z++)
   {
    if(!map.InGrid(x,z)||map.At(x,z)!=ExCell.Wall)continue;var terrain=map.TerrainAt(x,z);var at=map.World(x,z)-center;
    if(l.kind=="lake"&&terrain==ExTerrain.Water)
    {foreach(var direction in ExploreMap.Dirs)if(map.InGrid(x+direction.x,z+direction.y)&&map.TerrainAt(x+direction.x,z+direction.y)!=ExTerrain.Water){var shore=Part(root.transform,"water/shore",b=>ShoreStrip(b),at,true);shore.name="Shore_"+x+"_"+z+"_"+direction.x+"_"+direction.y;shore.localRotation=Quaternion.Euler(0,Mathf.Atan2(direction.x,direction.y)*Mathf.Rad2Deg,0);}
     if(Variant(x,z,3)==0){Part(root.transform,"landmark/moon",b=>{b.Ring(V(0,.025f,0),.65f,.08f,new Color(.65f,.9f,1,.055f),20);b.Ring(V(0,.027f,0),.40f,.06f,new Color(.8f,.94f,1,.035f),16);},at,true).name="MoonReflection_"+x+"_"+z;Part(root.transform,"landmark/fireflies",b=>{for(int i=0;i<5;i++)b.Gem(V(Mathf.Sin(i*2.3f)*.7f,.35f+i*.09f,Mathf.Cos(i*2.3f)*.7f),V(.035f,.035f,.035f),Glow(Bone),4);},at).name="Fireflies_"+x+"_"+z;}continue;}
    if(terrain==ExTerrain.Water||placed>=8||Variant(x,z,3)!=0)continue;
    bool edge=false;foreach(var d in ExploreMap.Dirs)if(map.Open(x+d.x,z+d.y))edge=true;if(!edge)continue;
    if(l.kind=="fort"||l.kind=="start")Part(root.transform,"landmark/flag",b=>{b.Rod(V(1,2.5f,0),V(1,4.4f,0),.04f,Bark);b.Quad(V(1,4.3f,0),V(.2f,4.3f,0),V(.4f,3.6f,0),V(1,3.8f,0),Leaf,true);},at).name="Banner_"+x+"_"+z;
    if(l.kind=="fort"||l.kind=="start")Part(root.transform,"landmark/torch",b=>{b.Rod(V(1,2.7f,1),V(1,3.3f,1),.05f,Bark);b.Gem(V(1,3.43f,1),V(.24f,.42f,.24f),Glow(new Color(1,.61f,.2f)),6);},at).name="Torch_"+x+"_"+z;
    if(l.kind=="cave")Part(root.transform,"landmark/drops",b=>{for(int i=0;i<3;i++)b.Gem(V(-.5f+i*.5f,2.7f+i*.15f,.8f),V(.035f,.10f,.035f),Glow(Light),4);},at).name="WaterDrops_"+x+"_"+z;
    if(l.kind=="grove")Part(root.transform,"landmark/spores",b=>{for(int i=0;i<6;i++)b.Gem(V(Mathf.Sin(i*2f),2.5f+i*.23f,Mathf.Cos(i*2f)),V(.045f,.045f,.045f),Glow(Light),4);},at).name="Spores_"+x+"_"+z;
    int n=placed++%3;Part(root.transform,"landmark/"+l.kind+"/"+n,b=>LandmarkPiece(b,l.kind,n),at).name="WallScenery_"+x+"_"+z;
   }
   return root;
  }
  static void LandmarkPiece(Geo b,string kind,int n)
  {
   if(kind=="lake")
   {b.Rod(V(0,2.4f,0),V(0,3.2f,0),.15f,Rock,6);b.Box(V(0,3.3f,0),V(.5f,.35f,.5f),Glow(Bone));b.Gem(V(0,3.6f,0),V(.85f,.3f,.85f),Rock,6);}
   else if(kind=="fort")
   {for(int s=-1;s<=1;s+=2)for(int d=-1;d<=1;d+=2)b.Rod(V(s*.9f,0,d*.9f),V(s*.9f,4.3f,d*.9f),.10f,Bark,5);b.Box(V(0,3.2f,0),V(2.1f,.18f,2.1f),Bark);for(int s=-1;s<=1;s+=2)b.Rod(V(-.95f,3.8f,s*.9f),V(.95f,3.8f,s*.9f),.055f,Bone);b.Gem(V(0,4.4f,0),V(2.4f,.55f,2.4f),Leaf,4);Skull(b,V(0,3.5f,1.08f),.65f);}
   else if(kind=="rocks")
   {b.Rod(V(0,2.4f,0),V(.2f,4.3f+n*.4f,0),.8f,Rock,7,.3f);b.Vine(V(-.4f,2.5f,0),V(.65f,4.8f,.3f),-.45f,.13f,Bark,6);Leaves(b,V(.65f,4.8f,.3f),.65f,Moss,6);}
   else if(kind=="cave")
   {b.Rod(V(-.95f,2.6f,0),V(-.8f,3.8f,0),.28f,Rock,6);b.Rod(V(.95f,2.6f,0),V(.8f,3.8f,0),.28f,Rock,6);b.Rod(V(-.8f,3.8f,0),V(.8f,3.8f,0),.30f,Rock,6);for(int i=-1;i<=1;i++){b.Gem(V(i*.5f,3.15f,.8f),V(.24f,.8f,.24f),Glow(Light),5);b.Rod(V(i*.5f,3.8f,0),V(i*.5f,3.3f,0),.13f,Rock,4,0);}}
   else if(kind=="palace")
   {for(int s=-1;s<=1;s+=2){b.Rod(V(s*.9f,0,0),V(s*.9f,3.6f,0),.28f,Rock,8);b.Vine(V(s*.9f,.2f,.32f),V(s*.9f,3.5f,.32f),.16f,.06f,Leaf,6);}b.Box(V(0,3.65f,0),V(2.4f,.35f,1.4f),Jade);b.Gem(V(.2f,4.03f,0),V(2.4f,.55f,2),Jade*.8f,4);b.Gem(V(0,3.55f,.78f),V(.4f,.5f,.08f),Bone,6);}
   else if(kind=="grove")
   {Mushroom(b,V(0,2.25f,0),1.2f,n);}
   else if(kind=="start")
   {b.Box(V(0,1.65f,0),V(2.4f,3.3f,2.4f),Bark);b.Gem(V(0,3.55f,0),V(2.8f,1,2.8f),Moss,4);b.Box(V(0,2,1.21f),V(.65f,.8f,.04f),Dark);b.Rod(V(0,1.6f,1.25f),V(0,2.4f,1.25f),.035f,Bone);b.Rod(V(-.32f,2,1.25f),V(.32f,2,1.25f),.035f,Bone);}
   else
   {for(int s=-1;s<=1;s+=2)b.Rod(V(s*.85f,1.8f,0),V(s*.85f,3.4f,0),.08f,Bark);b.Rod(V(-.9f,3.35f,0),V(.9f,3.35f,0),.06f,Bark);b.Quad(V(-.6f,3.28f,0),V(.6f,3.28f,0),V(.45f,2.4f,0),V(-.45f,2.4f,0),Bone,true);Skull(b,V(0,2.9f,.08f),.4f);}
   b.ClampCell(1.42f);
  }
  public static GameObject CreateArena(int n,float radius)
  {
   var root=Root("Arena "+n);float r=Mathf.Max(1,radius);
   Part(root.transform,"arena/boundary/"+n,b=>{b.Ring(V(0,.012f,0),1,.013f,new Color(.48f,.75f,.53f,.33f),48);for(int i=0;i<12;i++){float a=i*Mathf.PI/6;var p=V(Mathf.Cos(a)*.97f,.014f,Mathf.Sin(a)*.97f);b.Gem(p,V(.025f,.002f,.025f),new Color(.7f,.9f,.6f,.4f),4);}},Vector3.zero,true).localScale=V(r,1,r);
   // Dress only the far rim, not the fighting area. The raised silhouette is outside the supplied radius.
   // Fixed JungleMaze wall anchors; the second arena's north centre is a gate, so offset one cell east.
   var rim=SkyArt.Node(root.transform,"RimSet",V(n==2?3:0,0,r+(n==1?2:n==2?3:n==3?6:7)));
   Part(rim,"arena/set/"+n,b=>{
    if(n==1){for(int i=-2;i<=2;i++)b.Vine(V(i*.7f,0,0),V(i*.55f,2.2f,0),.4f,.2f,Bark,6);Leaves(b,V(0,1.5f,0),1.2f,Moss);}
    else if(n==2){for(int i=-1;i<=1;i++)Mushroom(b,V(i*.9f,0,0),.6f+(i==0?.3f:0),i+1);}
    else if(n==3){for(int s=-1;s<=1;s+=2){b.Rod(V(s,0,0),V(s,2.6f,0),.08f,Bark);Skull(b,V(s,2.3f,0),.5f);for(int i=0;i<3;i++)b.Frond(V(s,2.2f,0),V(s+.15f,1.6f-i*.13f,0),.09f,i%2==0?Jade:Bone);}}
    else {b.Rod(Vector3.zero,V(0,.5f,0),1.1f,Bark,8);for(int i=-2;i<=2;i++)b.Vine(V(i*.45f,0,.3f),V(i*.4f,2.8f-Mathf.Abs(i)*.4f,0),i*.12f,.20f,Dark,6);b.Box(V(0,.65f,.35f),V(1.3f,.25f,.75f),Bark);for(int s=-1;s<=1;s+=2)b.Vine(V(s*1.4f,0,0),V(s*.6f,1,.4f),s*.3f,.10f,Glow(new Color(.43f,.15f,.52f)),6);}
    b.ClampCell(1.42f);
   });return root;
  }
  public static GameObject CreateGround(ExGround g,int x,int z,float cell)
  {
   var root=Root("Ground "+g);int n=Variant(x,z);Part(root.transform,"ground/"+g+"/"+n,b=>{
    var col=g==ExGround.Grass?Leaf:g==ExGround.Dirt?Bark:g==ExGround.Shore?Bone:g==ExGround.Cave?Rock:Jade;col.a=.16f;
    b.FadeTile(1.5f,col);
    // Subtle edge details; the middle remains readable through the alpha floor.
    col.a=.25f;for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;var p=V(Mathf.Cos(a)*1.25f,.004f,Mathf.Sin(a)*1.25f);if(g==ExGround.Grass)b.Frond(p,p+V(.09f,.003f,.18f),.045f,col);else b.Gem(p,V(.17f,.005f,.12f),col,5);}
   },Vector3.zero,true);root.transform.localScale=V(cell/3,1,cell/3);return root;
  }
 }
}
