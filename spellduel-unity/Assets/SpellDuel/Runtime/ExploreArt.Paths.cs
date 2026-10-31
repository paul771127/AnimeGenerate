using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  static readonly Dictionary<ExGround,Material> pathMaterials=new Dictionary<ExGround,Material>();
  // Map-aware wall factory rounds exposed convex corners without crossing the cell boundary.
  public static GameObject CreateWall(ExploreMap map,int x,int z)
  {
   if(map==null)return null;var root=CreateWall(map.TerrainAt(x,z),x,z,ExploreMap.CellSize);int corners=0;
   for(int i=0;i<4;i++){int sx=i%2==0?1:-1,sz=i<2?1:-1;if(map.Open(x+sx,z)&&map.Open(x,z+sz))corners|=1<<i;}
   if(corners==0)return root;
   foreach(var f in root.GetComponentsInChildren<MeshFilter>())
   {
    string key="wall/rounded/"+f.sharedMesh.name+"/"+corners+"/"+ExploreMap.CellSize;
    if(!meshes.TryGetValue(key,out var mesh)||!mesh){mesh=UnityEngine.Object.Instantiate(f.sharedMesh);mesh.name=key;var vertices=mesh.vertices;float radius=.45f*3/ExploreMap.CellSize;
     for(int j=0;j<vertices.Length;j++){var v=root.transform.InverseTransformPoint(f.transform.TransformPoint(vertices[j]));
      for(int i=0;i<4;i++)if((corners&(1<<i))!=0){int sx=i%2==0?1:-1,sz=i<2?1:-1;var delta=new Vector2(v.x*sx-(1.5f-radius),v.z*sz-(1.5f-radius));if(delta.x>0&&delta.y>0&&delta.magnitude>radius){delta=delta.normalized*radius;v.x=(1.5f-radius+delta.x)*sx;v.z=(1.5f-radius+delta.y)*sz;}}
      vertices[j]=f.transform.InverseTransformPoint(root.transform.TransformPoint(v));}
     mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[key]=mesh;
    }f.sharedMesh=mesh;
   }return root;
  }
  // Root at cell centre; caller assigns map.World(x,z). Refresh neighbours when a gate changes.
  public static GameObject CreatePath(ExploreMap map,int x,int z)
  {
   if(map==null||!map.Open(x,z))return null;
   int mask=0,diag=0;for(int i=0;i<4;i++){var d=ExploreMap.Dirs[i];if(map.Open(x+d.x,z+d.y))mask|=1<<i;}
   for(int i=0;i<4;i++)if(map.Open(x+(i%2==0?1:-1),z+(i<2?1:-1)))diag|=1<<i;
   var root=Root("Connected path "+x+" "+z);float half=ExploreMap.CellSize*.5f;string key="path/"+mask+"/"+diag+"/"+ExploreMap.CellSize;
   if(!meshes.TryGetValue(key,out var mesh)||!mesh)
   {
    mesh=new Mesh{name="Explore cached connected path"};mesh.vertices=new[]{V(-half,.006f,-half),V(-half,.006f,half),V(half,.006f,half),V(half,.006f,-half)};
    mesh.uv=new[]{new Vector2(-half,-half),new Vector2(-half,half),new Vector2(half,half),new Vector2(half,-half)};
    var data=new List<Vector4>();for(int i=0;i<4;i++)data.Add(new Vector4(mask,diag,half,ExploreMap.PathFillet));mesh.SetUVs(1,data);mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();meshes[key]=mesh;
   }
   var type=map.GroundAt(x,z);if(!pathMaterials.TryGetValue(type,out var material)||!material)
   {
    material=new Material(Resources.Load<Shader>("Shaders/ExplorePath")){name="Explore shared readable path "+type};
    material.mainTexture=GroundMaterial(type==ExGround.Grass?ExGround.Dirt:type).mainTexture;
    material.SetColor("_PathColor",type==ExGround.Cave?new Color(.64f,.68f,.64f):type==ExGround.Stone?new Color(.72f,.74f,.59f):type==ExGround.Shore?new Color(.82f,.72f,.48f):new Color(.76f,.59f,.34f));
    material.SetColor("_VergeColor",type==ExGround.Cave||type==ExGround.Stone?new Color(.23f,.30f,.27f):new Color(.20f,.32f,.12f));pathMaterials[type]=material;
   }
   var surface=AnimeFace.Part(root.transform,"PathSurface",mesh,Vector3.zero,material);var block=new MaterialPropertyBlock();var holes=new Vector4[16];int count=0;var centre=map.World(x,z);
   foreach(var p in map.props){var at=p.pos-centre;float reach=p.radius+.35f;if(Mathf.Abs(at.x)>half+reach||Mathf.Abs(at.z)>half+reach)continue;if(count<holes.Length)holes[count++]=new Vector4(at.x,at.z,reach,0);}
   block.SetVector("_TexOffset",new Vector4(centre.x,centre.z,0,0));block.SetVectorArray("_Props",holes);block.SetInt("_PropCount",count);surface.GetComponent<Renderer>().SetPropertyBlock(block);
   // Small low stones trace the blocked shoulder, never extending into the bright safe lane.
   if(mask!=15)Part(root.transform,"path/shoulder/"+mask+"/"+half,b=>{
    for(int side=0;side<4;side++)if((mask&(1<<side))==0)for(int i=0;i<3;i++){
     var normal=side==0?Vector3.right:side==1?Vector3.left:side==2?Vector3.forward:Vector3.back;var tangent=V(-normal.z,0,normal.x);var at=normal*(half-.13f)+tangent*((i-1)*.42f);
     b.Crown(at+Vector3.up*.04f,V(.16f,.09f,.13f),Color.Lerp(Rock,Moss,.3f),i+side,5,2);
    }
    for(int k=0;k<4;k++){int sx=k%2==0?1:-1,sz=k<2?1:-1;int bx=sx>0?1:2,bz=sz>0?4:8;if((mask&bx)!=0||(mask&bz)!=0)continue;
     for(int j=1;j<5;j++){float a=j*Mathf.PI/10,r=ExploreMap.PathFillet-.35f+.16f;var at=V((half-ExploreMap.PathFillet+Mathf.Cos(a)*r)*sx,.04f,(half-ExploreMap.PathFillet+Mathf.Sin(a)*r)*sz);b.Crown(at,V(.18f,.10f,.18f),Moss,j,5,2);}}
   }).name="BlockedShoulder";
   return root;
  }
 }
}
