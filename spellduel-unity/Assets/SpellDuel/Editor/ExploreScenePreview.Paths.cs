#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class ExploreScenePreview
 {
  static void FootprintAudit()
  {
   if(ExploreMap.CellSize!=4)throw new Exception("Expected current 4m map");
   var map=new ExploreMap(5,5);for(int x=0;x<5;x++)for(int z=0;z<5;z++)map.cells[x,z]=ExCell.Floor;map.cells[2,2]=ExCell.Wall;
   foreach(ExTerrain type in Enum.GetValues(typeof(ExTerrain)))for(int variant=0;variant<4;variant++){
    map.terrain[2,2]=type;var root=ExploreArt.CreateWall(map,2,2);
    foreach(var f in root.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v)-root.transform.position;if(Mathf.Abs(p.x)>2.001f||Mathf.Abs(p.z)>2.001f)throw new Exception("Rounded wall exceeds 4m cell "+type);}
    UnityEngine.Object.DestroyImmediate(root);
   }
   int props=0;foreach(string kind in new[]{"tree","stump","boulder","mushroom","tent","crate","totem","pillar","statue","campfire","altar"})for(int n=0;n<(kind=="tree"?5:3);n++)foreach(float radius in new[]{.6f,1f,1.4f}){
    var root=ExploreArt.CreateProp(new ExProp{kind=kind,variant=n,radius=radius,pos=new Vector3(n*.1f,0,0)});
    foreach(var f in root.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices){var p=f.transform.TransformPoint(v)-root.transform.position;if(new Vector2(p.x,p.z).magnitude>radius+.001f)throw new Exception("Prop exceeds radius: "+kind+" "+n+" "+radius);}
    if(root.GetComponentInChildren<Collider>())throw new Exception("Prop introduced collider");UnityEngine.Object.DestroyImmediate(root);props++;
   }
   // A concave L-shaped shore must triangulate as well as the shipped rectangular lake.
   var lakeMap=new ExploreMap(6,6);foreach(var p in new[]{new Vector2Int(1,1),new Vector2Int(2,1),new Vector2Int(3,1),new Vector2Int(1,2),new Vector2Int(1,3)})lakeMap.terrain[p.x,p.y]=ExTerrain.Water;
   var l=new ExLandmark{id="audit_concave_lake",kind="lake",rect=new RectInt(1,1,2,2)};var lake=ExploreArt.CreateLandmark(l,lakeMap);AuditLake(lake,l,lakeMap);UnityEngine.Object.DestroyImmediate(lake);
   // Regression: shipped proportions must have broad corners, not a rounded box.
   var wideMap=new ExploreMap(9,7);
   for(int x=2;x<=6;x++)for(int z=2;z<=4;z++)wideMap.terrain[x,z]=ExTerrain.Water;
   var wideLandmark=new ExLandmark{id="audit_broad_lake",kind="lake",rect=new RectInt(2,2,5,3)};
   var wideLake=ExploreArt.CreateLandmark(wideLandmark,wideMap);AuditLake(wideLake,wideLandmark,wideMap);
   var bounds=wideLake.transform.Find("LakeSurface").GetComponent<MeshFilter>().sharedMesh.bounds;
   float nearest=float.MaxValue;
   foreach(var v in wideLake.transform.Find("LakeSurface").GetComponent<MeshFilter>().sharedMesh.vertices)
    nearest=Mathf.Min(nearest,new Vector2(v.x-bounds.min.x,v.z-bounds.min.z).magnitude);
   if(nearest<2f)throw new Exception("Lake corners are still too square");
   UnityEngine.Object.DestroyImmediate(wideLake);
   File.WriteAllText("snapshots/explore_4m_audit.json","{\"cellSize\":4,\"wallVerticesInsideCell\":true,\"propVariantsChecked\":"+props+",\"propVerticesInsideRadius\":true,\"concaveLake\":true,\"noGameplayEdits\":true}");
  }
  static float Cross2(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static void AuditLake(GameObject root,ExLandmark landmark,ExploreMap map)
  {
   var surface=root.transform.Find("LakeSurface");if(!surface)throw new Exception("Missing curved lake");var mesh=surface.GetComponent<MeshFilter>().sharedMesh;var v=mesh.vertices;var tris=mesh.triangles;
   var positions=new Dictionary<Vector3,int>();var points=new List<Vector3>();var edges=new Dictionary<Vector2Int,int>();
   for(int i=0;i<tris.Length;i+=3){var ids=new int[3];for(int j=0;j<3;j++){var p=v[tris[i+j]];if(!positions.TryGetValue(p,out ids[j])){ids[j]=points.Count;positions[p]=ids[j];points.Add(p);}}for(int j=0;j<3;j++){int a=ids[j],b=ids[(j+1)%3];var edge=new Vector2Int(Math.Min(a,b),Math.Max(a,b));edges.TryGetValue(edge,out int count);edges[edge]=count+1;}}
   var neighbours=new Dictionary<int,List<int>>();foreach(var e in edges)if(e.Value==1){if(!neighbours.ContainsKey(e.Key.x))neighbours[e.Key.x]=new List<int>();if(!neighbours.ContainsKey(e.Key.y))neighbours[e.Key.y]=new List<int>();neighbours[e.Key.x].Add(e.Key.y);neighbours[e.Key.y].Add(e.Key.x);}
   foreach(var pair in neighbours){if(pair.Value.Count!=2)throw new Exception("Lake boundary not closed");var incoming=points[pair.Key]-points[pair.Value[0]];var outgoing=points[pair.Value[1]]-points[pair.Key];if(Vector3.Angle(incoming,outgoing)>28)throw new Exception("Lake contains a sharp corner");}
   for(int x=landmark.rect.xMin-1;x<=landmark.rect.xMax+1;x++)for(int z=landmark.rect.yMin-1;z<=landmark.rect.yMax+1;z++)if(map.InGrid(x,z)&&map.TerrainAt(x,z)==ExTerrain.Water){var pos=map.World(x,z)-root.transform.localPosition;var p=new Vector2(pos.x,pos.z);bool covered=false;
    for(int i=0;i<tris.Length;i+=3){var a=new Vector2(v[tris[i]].x,v[tris[i]].z);var b=new Vector2(v[tris[i+1]].x,v[tris[i+1]].z);var c=new Vector2(v[tris[i+2]].x,v[tris[i+2]].z);float d1=Cross2(b-a,p-a),d2=Cross2(c-b,p-b),d3=Cross2(a-c,p-c);if((d1<=.001f&&d2<=.001f&&d3<=.001f)||(d1>=-.001f&&d2>=-.001f&&d3>=-.001f)){covered=true;break;}}
    if(!covered)throw new Exception("Lake misses water cell centre");
   }
  }
  static void PathCatalogue(Camera cam)
  {
   var map=new ExploreMap(9,9);map.start=new Vector2Int(4,4);int[] coords={1,1,2,1,3,1,3,2,3,3,4,3,5,3,5,4,5,5,4,5,3,5,3,4,5,6,5,7,6,5,7,5};
   for(int i=0;i<coords.Length;i+=2)map.cells[coords[i],coords[i+1]]=ExCell.Floor;
   var root=new GameObject("AR path reference only");for(int i=0;i<coords.Length;i+=2){int x=coords[i],z=coords[i+1];var path=ExploreArt.CreatePath(map,x,z);path.transform.SetParent(root.transform,false);path.transform.localPosition=map.World(x,z);}
   var plane=GameObject.CreatePrimitive(PrimitiveType.Plane);plane.transform.position=Vector3.down*.02f;plane.transform.localScale=Vector3.one*4;UnityEngine.Object.DestroyImmediate(plane.GetComponent<Collider>());
   string[] names={"neutral","wood","tile","carpet"};Color[] colours={new Color(.48f,.47f,.43f),new Color(.31f,.20f,.12f),new Color(.82f,.81f,.76f),new Color(.20f,.24f,.32f)};
   var mat=new Material(Shader.Find("Unlit/Texture"));plane.GetComponent<Renderer>().sharedMaterial=mat;
   for(int k=0;k<names.Length;k++){
    var tex=new Texture2D(64,64,TextureFormat.RGB24,true);var pixels=new Color[4096];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float shade=k==1?.90f+.1f*Mathf.Sin(y*.7f+x*.04f):k==2?(x%32<1||y%32<1?.75f:1):k==3?.94f+.06f*Mathf.Sin(x*2.7f+y*4):1;pixels[y*64+x]=colours[k]*shade;}tex.SetPixels(pixels);tex.Apply(true);mat.mainTexture=tex;mat.mainTextureScale=Vector2.one*8;
    cam.orthographic=true;cam.orthographicSize=18;cam.transform.position=Vector3.up*30;cam.transform.rotation=Quaternion.Euler(90,0,0);Save(cam,"explore_path_ar_"+names[k]+"_top",1080,1080);
    var check=new Texture2D(2,2);check.LoadImage(File.ReadAllBytes("snapshots/explore_path_ar_"+names[k]+"_top.png"));var background=check.GetPixel(5,5);
    for(int n=0;n<coords.Length;n+=2){var screen=cam.WorldToViewportPoint(map.World(coords[n],coords[n+1]));var c=check.GetPixel(Mathf.RoundToInt(screen.x*1079),Mathf.RoundToInt(screen.y*1079));if(c.r-c.g<.07f||c.r-c.b<.15f||Mathf.Abs(c.r-background.r)+Mathf.Abs(c.g-background.g)+Mathf.Abs(c.b-background.b)<.25f)throw new Exception("Path not visible on "+names[k]);}
    UnityEngine.Object.DestroyImmediate(check);
    cam.orthographic=false;cam.fieldOfView=72;cam.transform.position=map.World(1,1)+Vector3.up*1.65f;cam.transform.LookAt(map.World(3,2)+Vector3.up*.1f);Save(cam,"explore_path_ar_"+names[k]+"_eye",960,1280);UnityEngine.Object.DestroyImmediate(tex);
   }
   UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(plane);UnityEngine.Object.DestroyImmediate(mat);cam.orthographic=false;cam.fieldOfView=35;
  }
 }
}
#endif
