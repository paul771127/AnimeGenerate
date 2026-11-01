#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace SpellDuel.EditorTools
{
 public static class DungeonMapArtPreview
 {
  [Serializable] public class Row {public string id;public int areas,edges,baselineUnreachable;public bool unchanged,deterministic,cache;}
  [Serializable] public class View {public string theme,dungeon;public int cells,triangles,renderers;}
  [Serializable] public class Report {public string unity;public float cellSize;public bool noColliders,wallBounds,gateAnimation;public List<Row> maps=new List<Row>();public List<View> views=new List<View>();}
  static void Check(bool condition,string text){if(!condition)throw new Exception("Dungeon map art: "+text);}
  static string Fingerprint(ExploreMap m)
  {
   var b=new StringBuilder();b.Append(m.W).Append(',').Append(m.H).Append(':').Append(m.start);
   for(int x=0;x<m.W;x++)for(int z=0;z<m.H;z++)b.Append((int)m.At(x,z)).Append((int)m.TerrainAt(x,z)).Append((int)m.GroundAt(x,z));
   foreach(var a in m.areas){b.Append(a.index).Append(a.rect).Append(a.cleared).Append(a.entered);foreach(var p in a.exits)b.Append(p);}
   foreach(var p in m.props)b.Append(p.kind).Append(p.pos).Append(p.radius);
   foreach(var p in m.monsters)b.Append(p.id).Append(p.kind).Append(p.home).Append(p.wave);
   return b.ToString();
  }
  static bool WalkRoute(ExploreMap m,Vector2Int from,Vector2Int to)
  {
   var q=new Queue<Vector2Int>();var seen=new HashSet<Vector2Int>();q.Enqueue(from);seen.Add(from);
   while(q.Count>0){var c=q.Dequeue();if(c==to)return true;foreach(var d in ExploreMap.Dirs){var n=c+d;if(!m.InGrid(n.x,n.y)||seen.Contains(n)||!m.Walkable(m.World(n.x,n.y)))continue;seen.Add(n);q.Enqueue(n);}}return false;
  }
  static int RouteAudit(ExploreMap m,out int edges)
  {
   int failed=0;edges=0;
   Check(m.Reachable(m.start,m.Cell(m.areas[0].center))&&WalkRoute(m,m.start,m.Cell(m.areas[0].center)),m.name+" entry blocked");
   // Test every authored directed connection with only the source room cleared.
   foreach(var a in m.areas){foreach(var b in m.areas)b.cleared=false;a.cleared=true;
    foreach(int next in a.next){edges++;var from=m.Cell(a.center);var to=m.Cell(m.areas[next].center);if(!m.Reachable(from,to)||!WalkRoute(m,from,to))failed++;}}
   foreach(var a in m.areas)a.cleared=true;
   foreach(var a in m.areas)if(a.IsBoss)Check(m.Reachable(m.start,m.Cell(a.center))&&WalkRoute(m,m.start,m.Cell(a.center)),m.name+" final boss unreachable");
   foreach(var a in m.areas)a.cleared=false;return failed;
  }
  static GameObject BuildView(ExploreMap map,Vector2Int center,out int count)
  {
   var root=new GameObject("Dungeon streamed native view");count=0;
   var baseMethod=typeof(SoloBattle).GetMethod("CreateExploreBaseGround",BindingFlags.NonPublic|BindingFlags.Static);
   var baseGround=(GameObject)baseMethod.Invoke(null,new object[]{map});baseGround.transform.SetParent(root.transform,false);
   for(int x=center.x-6;x<=center.x+6;x++)for(int z=center.y-6;z<=center.y+6;z++)
   {
    if(!map.InGrid(x,z))continue;var cell=map.At(x,z);GameObject g;
    if(cell==ExCell.Wall){bool edge=false;foreach(var d in ExploreMap.Dirs)if(map.Open(x+d.x,z+d.y))edge=true;if(!edge)continue;g=ExploreArt.CreateWall(map,x,z);AuditBounds(g);}
    else if(cell==ExCell.Gate){g=ExploreArt.CreateDungeonGate(map,x,z);g.transform.localRotation=map.Open(x,z+1)||map.Open(x,z-1)?Quaternion.identity:Quaternion.Euler(0,90,0);}
    else g=ExploreArt.CreatePath(map,x,z);
    if(!g)continue;g.transform.SetParent(root.transform,false);g.transform.localPosition=map.World(x,z);count++;
   }
   Check(root.GetComponentInChildren<Collider>(true)==null,"visuals add colliders");return root;
  }
  static void AuditBounds(GameObject wall)
  {
   foreach(var f in wall.GetComponentsInChildren<MeshFilter>())foreach(var v in f.sharedMesh.vertices)
   {
    var p=f.transform.TransformPoint(v)-wall.transform.position;
    // Mist is non-solid and moves slightly, but all static wall scenery must fit a 4m cell.
    if(f.name=="DriftingMist")continue;
    Check(Mathf.Abs(p.x)<=ExploreMap.CellSize*.5f+.015f&&Mathf.Abs(p.z)<=ExploreMap.CellSize*.5f+.015f,"wall outside cell: "+wall.name+" / "+f.name+" "+p);
   }
  }
  static void Save(Camera cam,string name,int w,int h)
  {
   cam.aspect=w/(float)h;var rt=new RenderTexture(w,h,24);var old=RenderTexture.active;cam.targetTexture=rt;RenderTexture.active=rt;cam.Render();var image=new Texture2D(w,h,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();File.WriteAllBytes("snapshots/"+name+".png",image.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
  }
  [MenuItem("SpellDuel/Explore/Dungeon themes and routes")]
  public static void Render()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientLight=new Color(.68f,.69f,.72f);
   var light=new GameObject("Dungeon preview sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.05f;light.transform.rotation=Quaternion.Euler(42,-35,0);
   var cam=new GameObject("Dungeon preview camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.10f,.15f,.18f);cam.nearClipPlane=.05f;cam.farClipPlane=1000;cam.fieldOfView=68;
   Dungeons.Parse(Resources.Load<TextAsset>("dungeons").text);Check(Dungeons.errors.Count==0,string.Join(";",Dungeons.errors));Directory.CreateDirectory("snapshots");
   var report=new Report{unity=Application.unityVersion,cellSize=ExploreMap.CellSize,noColliders=true,wallBounds=true};var representatives=new ExploreMap[6];var targetAreas=new ExArea[6];
   foreach(var d in Dungeons.All)
   {
    if(!DungeonMaze.Supports(d))continue;var map=DungeonMaze.Build(d);var row=new Row{id=d.id,areas=map.areas.Count};row.deterministic=Fingerprint(map)==Fingerprint(DungeonMaze.Build(d));Check(row.deterministic,d.id+" seed differs");
    row.baselineUnreachable=RouteAudit(map,out row.edges);var before=Fingerprint(map);
    foreach(var a in map.areas)
    {
     int t=map.nodeAreas?5:d.ThemeOf(a.node);
     // Prefer a dedicated theme map over a side room in a mixed dungeon, so its sky agrees.
     bool matching=map.nodeAreas||d.BossTheme==t;
     bool priorMatching=representatives[t]!=null&&(representatives[t].nodeAreas||representatives[t].dungeon.BossTheme==t);
     if(representatives[t]==null||matching&&!priorMatching){representatives[t]=map;targetAreas[t]=a;}
    }
    // Exercise all area geometry, but keep production's 6-cell streaming window.
    foreach(var a in map.areas){int unused;var root=BuildView(map,map.Cell(a.center),out unused);UnityEngine.Object.DestroyImmediate(root);}
    row.unchanged=Fingerprint(map)==before;Check(row.unchanged,d.id+" art changed map data");int edges;Check(RouteAudit(map,out edges)==row.baselineUnreachable,d.id+" art changed reachability");
    // Do not conceal pre-existing topology bugs behind an art change.
    Check(row.baselineUnreachable==0,d.id+" has "+row.baselineUnreachable+" unreachable baseline connections");row.cache=true;report.maps.Add(row);
   }
   // Arena/canyon currently occur only inside the mechanical gallery. Preview their
   // normal art using editor-only fixtures; do not edit the authored dungeon catalog.
   for(int t=0;t<5;t++)if(representatives[t]==null)
   {
    var fixture=new DungeonDef{id="art_fixture_theme_"+t,name="Theme art fixture",level=10};
    fixture.rooms.Add(new DungeonRoom{id="camp",title="Camp",wave=1,theme=t});
    fixture.rooms.Add(new DungeonRoom{id="boss",title="Throne",kind="boss",wave=10,theme=t});
    representatives[t]=DungeonMaze.Build(fixture);targetAreas[t]=representatives[t].areas[0];
   }
   string[] names={"volcano","jungle","sea","arena","canyon","clockwork"};
   for(int t=0;t<6;t++)
   {
    var map=representatives[t];Check(map!=null,"no representative for "+names[t]);var a=targetAreas[t];int count;var root=BuildView(map,map.Cell(a.center),out count);
    var view=new View{theme=names[t],dungeon=map.dungeon.id,cells=count};foreach(var f in root.GetComponentsInChildren<MeshFilter>())view.triangles+=f.sharedMesh.triangles.Length/3;view.renderers=root.GetComponentsInChildren<Renderer>().Length;report.views.Add(view);
    cam.orthographic=true;cam.orthographicSize=26;cam.transform.position=a.center+Vector3.up*80;cam.transform.rotation=Quaternion.Euler(90,0,0);Save(cam,"dungeon_"+names[t]+"_top",1400,1400);
    var sky=ExploreArt.CreateBackdrop(map);sky.transform.SetParent(root.transform,false);cam.orthographic=false;cam.fieldOfView=68;cam.transform.position=a.center+new Vector3(-3,1.65f,-2);cam.transform.LookAt(a.center+new Vector3(0,1.5f,10));Save(cam,"dungeon_"+names[t]+"_first",1080,1440);
    UnityEngine.Object.DestroyImmediate(root);
    // Warm each factory twice, including rounded walls and the parameterized floor materials.
    int meshes=0,materials=0,textures=0;
    for(int pass=0;pass<2;pass++)
    {
     var cached=BuildView(map,map.Cell(a.center),out count);UnityEngine.Object.DestroyImmediate(cached);
     int mm=Resources.FindObjectsOfTypeAll<Mesh>().Length,mt=Resources.FindObjectsOfTypeAll<Material>().Length,tt=Resources.FindObjectsOfTypeAll<Texture2D>().Length;
     // The legacy full-map underlay creates one mesh/material per scene, not per cell/frame.
     if(pass==1)Check(mm==meshes+1&&mt==materials+1&&tt==textures,"streamed factory cache "+names[t]);meshes=mm;materials=mt;textures=tt;
    }
    if(a.exits.Count>0)
    {
     var door=a.exits[0];a.cleared=false;var closed=ExploreArt.CreateDungeonGate(map,door.x,door.y);UnityEngine.Object.DestroyImmediate(closed);a.cleared=true;
     var gate=ExploreArt.CreateDungeonGate(map,door.x,door.y);var state=gate.GetComponent<DungeonGateArt>();state.Sample(.375f);Check(state.closed.localPosition.y>1&&state.closed.localPosition.y<3,"gate halfway");state.Sample(.8f);Check(!state.closed.gameObject.activeSelf&&map.GateOpen(door.x,door.y),"gate open");UnityEngine.Object.DestroyImmediate(gate);a.cleared=false;
    }
   }
   report.gateAnimation=true;File.WriteAllText("snapshots/dungeon_maps_audit.json",JsonUtility.ToJson(report,true));Debug.Log("Dungeon art/routes/native preview passed for "+report.maps.Count+" maps");
  }
 }
}
#endif
