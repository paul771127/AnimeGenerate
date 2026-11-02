using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  static readonly Dictionary<string,Material> castleMaterials=new Dictionary<string,Material>();
  static Mesh castleQuad;
  static readonly Vector4[] castleProps={
   new Vector4(.051834f,.566188f,.158692f,.351675f),new Vector4(.300638f,.570175f,.150718f,.342903f),
   new Vector4(.533493f,.578150f,.183413f,.334928f),new Vector4(.789474f,.572568f,.165869f,.330144f),
   new Vector4(.047049f,.115630f,.165072f,.318979f),new Vector4(.287879f,.188198f,.174641f,.153110f),
   new Vector4(.530303f,.168262f,.178628f,.179426f),new Vector4(.795853f,.126794f,.156300f,.285486f)};
  static Material CastleMaterial(string atlas,bool backdrop=false)
  {
   string key=atlas+(backdrop?"/backdrop":"");
   if(castleMaterials.TryGetValue(key,out var mat)&&mat)return mat;
   mat=new Material(Resources.Load<Shader>("Shaders/CastleIllustration")){name="Castle shared "+key,mainTexture=Resources.Load<Texture2D>("CastleScene/"+atlas)};
   mat.SetFloat("_Background",backdrop?1:0);mat.SetFloat("_ZWrite",backdrop?0:1);mat.renderQueue=backdrop?1000:2450;
   castleMaterials[key]=mat;return mat;
  }
  static Vector4 CastleTile(int i,int cols,int rows)
  {float e=.0016f;return new Vector4(i%cols/(float)cols+e,1-(i/cols+1)/(float)rows+e,1f/cols-e*2,1f/rows-e*2);}
  static Transform CastlePanel(Transform parent,string name,string atlas,Vector4 rect,Vector3 at,float width,float height,bool backdrop=false,Vector2 repeat=default(Vector2))
  {
   if(!castleQuad){castleQuad=new Mesh{name="Castle shared unit illustration quad"};castleQuad.vertices=new[]{V(-.5f,0,0),V(.5f,0,0),V(.5f,1,0),V(-.5f,1,0)};castleQuad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};castleQuad.triangles=new[]{0,2,1,0,3,2};castleQuad.RecalculateBounds();}
   var g=AnimeFace.Part(parent,name,castleQuad,at,CastleMaterial(atlas,backdrop));g.transform.localScale=V(width,height,1);
   var mr=g.GetComponent<Renderer>();mr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;mr.receiveShadows=false;
   var block=new MaterialPropertyBlock();block.SetVector("_AtlasRect",rect);block.SetVector("_Tiling",new Vector4(repeat.x>0?repeat.x:1,repeat.y>0?repeat.y:1,0,0));mr.SetPropertyBlock(block);return g.transform;
  }
  static int CastleRoom(ExploreMap map,int x,int z)
  {
   var a=DungeonLayout(map).nearest[x,z];string id=a?.node?.id;
   return id=="castle_gallery"?1:id=="castle_library"?2:id=="castle_vault"?3:id=="castle_throne"?4:0;
  }
  static Transform CastleProp(Transform parent,int kind,Vector3 at,float height,bool sway=false)
  {
   var uv=castleProps[kind];var t=CastlePanel(parent,"Castle billboard "+kind,"props",uv,at,height*uv.z/uv.w,height);
   var motion=t.gameObject.AddComponent<CastleBillboardArt>();motion.sway=sway;return t;
  }
  static GameObject CreateCastleWall(ExploreMap map,int x,int z)
  {
   var root=Root("Castle illustrated wall");int room=CastleRoom(map,x,z),n=Variant(x,z),open=0;
   for(int i=0;i<4;i++){var d=ExploreMap.Dirs[i];if(map.At(x+d.x,z+d.y)!=ExCell.Wall)open++;}
   var a=DungeonLayout(map).nearest[x,z];bool feature=a!=null&&x==a.rect.xMin+a.rect.width/2&&z==a.rect.yMax+1;
   bool niche=open==1&&(feature||n==0);
   int tile=room==1?3:room==2?4:room==3?5:room==4?6:n%3;
   float half=ExploreMap.CellSize*.5f;
   for(int i=0;i<4;i++)
   {
    var d=ExploreMap.Dirs[i];if(map.At(x+d.x,z+d.y)==ExCell.Wall)continue;
    var face=SkyArt.Node(root.transform,"WallFace_"+i,Vector3.zero);face.localRotation=Quaternion.Euler(0,i==0?90:i==1?-90:i==2?0:180,0);
    CastlePanel(face,"PaintedWall","walls",CastleTile(tile,4,2),V(0,0,niche?-.3f:half-.01f),ExploreMap.CellSize,7);
    if(niche)
    {
     for(int s=-1;s<=1;s+=2){var side=CastlePanel(face,"AlcoveReturn","walls",CastleTile(0,4,2),V(s*(half-.01f),0,(half-.3f)*.5f),half+.3f,7);side.localRotation=Quaternion.Euler(0,90,0);}
     int prop=feature?(room==4?4:room==3?5:room==2?6:room==1?3:0):1;
     CastleProp(face,prop,V(0,0,.8f),prop==5?1.45f:prop==6?1.2f:2.7f);
    }
   }
   var cap=CastlePanel(root.transform,"WallTop","surfaces",CastleTile(3,2,2),V(0,7,-half),ExploreMap.CellSize,ExploreMap.CellSize);cap.localRotation=Quaternion.Euler(90,0,0);
   return root;
  }
  static void DressCastlePath(GameObject root,ExploreMap map,int x,int z)
  {
   const string key="floor";if(!castleMaterials.TryGetValue(key,out var mat)||!mat)
   {mat=new Material(Resources.Load<Shader>("Shaders/DungeonExploreFloor")){name="Castle shared painted floor"};mat.SetFloat("_Theme",6);mat.SetTexture("_CastleAtlas",Resources.Load<Texture2D>("CastleScene/surfaces"));castleMaterials[key]=mat;}
   var renderer=root.transform.Find("PathSurface").GetComponent<Renderer>();renderer.sharedMaterial=mat;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
   var near=DungeonLayout(map).nearest[x,z];var a=near!=null&&near.Contains(new Vector2Int(x,z))?near:null;int edges=0;
   if(a!=null){if(x==a.rect.xMax)edges|=1;if(x==a.rect.xMin)edges|=2;if(z==a.rect.yMax)edges|=4;if(z==a.rect.yMin)edges|=8;}
   block.SetFloat("_Room",a!=null?1:0);block.SetFloat("_RoomEdge",edges);block.SetVector("_CastleCenter",a!=null?new Vector4(a.center.x,a.center.z,0,0):Vector4.zero);renderer.SetPropertyBlock(block);
   var shoulder=root.transform.Find("BlockedShoulder");if(shoulder)shoulder.gameObject.SetActive(false);
   var ceiling=CastlePanel(root.transform,"CastleCellCeiling","surfaces",CastleTile(3,2,2),V(0,7,-ExploreMap.CellSize*.5f),ExploreMap.CellSize,ExploreMap.CellSize);ceiling.localRotation=Quaternion.Euler(90,0,0);
   if(a!=null&&map.Cell(a.center)==new Vector2Int(x,z))CastleProp(root.transform,2,V(0,4.6f,0),2.4f,true);
  }
  static GameObject CreateCastleGate(ExploreMap map,int x,int z)
  {
   var root=Root("Castle illustrated portcullis");float half=ExploreMap.CellSize*.5f;
   var frame=CastleTile(0,4,2);frame.z*=.18f;
   for(int s=-1;s<=1;s+=2)CastlePanel(root.transform,"GateColumn","walls",frame,V(s*(half-.17f),0,0),.34f,7);
   CastlePanel(root.transform,"GateHeader","walls",CastleTile(0,4,2),V(0,3.6f,0),ExploreMap.CellSize-.68f,3.4f);
   var moving=SkyArt.Node(root.transform,"Closed",Vector3.zero);CastlePanel(moving,"PaintedIronBars","props",castleProps[7],Vector3.zero,ExploreMap.CellSize-.68f,3.6f);
   var floor=CastlePanel(root.transform,"GateFloor","surfaces",CastleTile(0,2,2),V(0,.005f,-half),ExploreMap.CellSize,ExploreMap.CellSize);floor.localRotation=Quaternion.Euler(90,0,0);
   var layout=DungeonLayout(map);var key=new Vector2Int(x,z);bool opened=map.GateOpen(x,z);
   if(!opened)layout.closedSeen.Add(key);else if(!layout.openedAt.ContainsKey(key))layout.openedAt[key]=layout.closedSeen.Contains(key)?Time.time:Time.time-1;
   var motion=root.AddComponent<DungeonGateArt>();motion.closed=moving;motion.open=opened;motion.elapsed=opened?Time.time-layout.openedAt[key]:0;motion.Sample(motion.elapsed);return root;
  }
  static GameObject CreateCastleBackdrop(ExploreMap map)
  {
   var root=Root("Castle enclosed painted interior");root.transform.localPosition=(map.World(0,0)+map.World(map.W-1,map.H-1))*.5f;
   float rx=map.W*ExploreMap.CellSize*.5f+8,rz=map.H*ExploreMap.CellSize*.5f+8;
   for(int i=0;i<4;i++)
   {
    bool side=i<2;float span=(side?rz:rx)*2;var t=CastlePanel(root.transform,"DistantVault_"+i,"surfaces",CastleTile(2,2,2),i==0?V(rx,-3,0):i==1?V(-rx,-3,0):i==2?V(0,-3,rz):V(0,-3,-rz),span,19,true,new Vector2(span/14,1));t.localRotation=Quaternion.Euler(0,side?90:0,0);
   }
   var roof=CastlePanel(root.transform,"ClosedVaultCeiling","surfaces",CastleTile(3,2,2),V(0,16,-rz),rx*2,rz*2,true,new Vector2(rx/7,rz/7));roof.localRotation=Quaternion.Euler(90,0,0);
   HorizonColors(map,out var ground,out var low);GroundBowl(root.transform,ground,low,Mathf.Max(rx,rz)*3);
   return root;
  }
 }
 public sealed class CastleBillboardArt:MonoBehaviour
 {
  public bool sway;
  public void Face(Camera cam,float seconds)
  {
   if(!cam)return;var look=cam.transform.position-transform.position;look.y=0;
   if(look.sqrMagnitude>.0001f)transform.rotation=Quaternion.LookRotation(-look.normalized,Vector3.up)*Quaternion.Euler(0,0,sway?Mathf.Sin(seconds*.85f)*2.5f:0);
  }
  void LateUpdate(){Face(Camera.main,Time.time);}
 }
}
