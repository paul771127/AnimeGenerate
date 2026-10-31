#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static class ExploreScenePreview
 {
  [MenuItem("SpellDuel/Explore/Export jungle scene and keyframes")]
  public static void Render()
  {
   ExploreFxKeyframes.Render();var cam=UnityEngine.Object.FindObjectOfType<Camera>();cam.orthographic=false;cam.fieldOfView=72;cam.farClipPlane=60;cam.backgroundColor=new Color(.19f,.27f,.26f);
   var map=JungleMaze.Build();var root=new GameObject("Explore scene preview");Directory.CreateDirectory("snapshots");
   for(int x=0;x<map.W;x++)for(int z=0;z<map.H;z++)
   {
    var cell=map.At(x,z);GameObject g=null;
    if(cell==ExCell.Floor)g=ExploreArt.CreateGround(map.GroundAt(x,z),x,z,ExploreMap.CellSize);
    else if(cell==ExCell.Gate){var seal=map.SealOf(x,z);g=seal==null?ExploreArt.CreateGate(1,3):ExploreArt.CreateSeal(seal,x,z,3);}
    else {bool edge=false;foreach(var d in ExploreMap.Dirs)if(map.At(x+d.x,z+d.y)!=ExCell.Wall)edge=true;if(edge)g=ExploreArt.CreateWall(map.TerrainAt(x,z),x,z,3);}
    if(g){g.transform.SetParent(root.transform,false);g.transform.localPosition=map.World(x,z);}
   }
   foreach(var p in map.props){var g=ExploreArt.CreateProp(p);g.transform.SetParent(root.transform,false);g.transform.localPosition=p.pos;}
   foreach(var l in map.landmarks){var g=ExploreArt.CreateLandmark(l,map);g.transform.SetParent(root.transform,false);if(l.kind=="lake"){int water=0;for(int x=l.rect.xMin-1;x<=l.rect.xMax+1;x++)for(int z=l.rect.yMin-1;z<=l.rect.yMax+1;z++)if(map.InGrid(x,z)&&map.TerrainAt(x,z)==ExTerrain.Water)water++;var lake=g.transform.Find("LakeSurface");if(!lake||lake.GetComponent<MeshFilter>().sharedMesh.triangles.Length!=water*6)throw new Exception("Lake surface does not cover all water cells");}}
   foreach(var a in map.arenas){var g=ExploreArt.CreateArena(a.n,a.radius);g.transform.SetParent(root.transform,false);g.transform.localPosition=a.center;var rc=map.Cell(a.center+g.transform.Find("RimSet").localPosition);if(map.At(rc.x,rc.y)!=ExCell.Wall)throw new Exception("Arena scenery on open path "+a.n);}
   foreach(var sw in map.switches){var g=ExploreArt.CreateSwitch(sw);g.transform.SetParent(root.transform,false);g.transform.localPosition=sw.pos;}
   if(root.GetComponentInChildren<Collider>())throw new Exception("Explore art introduced a collider");
   foreach(var n in map.npcs){var rig=CharacterRig.Create(n.look);rig.InCombat=false;ExploreArt.DressNpc(rig,n.id);rig.transform.SetParent(root.transform,false);rig.transform.localPosition=n.pos;}
   var backdrop=ExploreArt.CreateBackdrop(map);backdrop.transform.SetParent(root.transform,false);
   foreach(var l in map.landmarks)
   {
    var center=map.World((l.rect.xMin+l.rect.xMax)/2,(l.rect.yMin+l.rect.yMax)/2);Vector3 desired=map.World((l.rect.xMin+l.rect.xMax)/2,l.rect.yMin);Vector3 at=desired;float best=float.MaxValue;
    for(int x=l.rect.xMin;x<=l.rect.xMax;x++)for(int z=l.rect.yMin;z<=l.rect.yMax;z++){var p=map.World(x,z);if(!map.Walkable(p))continue;float score=(p-desired).sqrMagnitude;if((p-center).sqrMagnitude<9)score+=100;if(score<best){best=score;at=p;}}
    var target=center+Vector3.up*(l.kind=="lake"?.35f:1.8f);ChooseView(map,l,root.transform,ref at,ref target);cam.transform.position=at+Vector3.up*1.65f;if((target-cam.transform.position).sqrMagnitude<.5f)target+=Vector3.forward*3;cam.transform.LookAt(target);Save(cam,"explore_"+l.id+"_first",960,1280);
   }
   backdrop.SetActive(false);cam.farClipPlane=500;var middle=(map.World(0,0)+map.World(map.W-1,map.H-1))*.5f;cam.orthographic=true;cam.orthographicSize=map.W*3*.54f;cam.transform.position=middle+Vector3.up*160;cam.transform.rotation=Quaternion.Euler(90,0,0);Save(cam,"explore_map_top",1800,1800);
   root.SetActive(false);cam.orthographic=false;cam.fieldOfView=35;cam.backgroundColor=new Color(.13f,.17f,.20f);Catalogue(cam);
   File.WriteAllText("snapshots/explore_scene_audit.json","{\"landmarks\":"+map.landmarks.Count+",\"props\":"+map.props.Count+",\"noColliders\":true,\"staticStates\":true,\"cachedFactories\":true,\"fullLakeWithEdgeOnlyStreaming\":true,\"keyframes\":16}");
  }
  static void ChooseView(ExploreMap map,ExLandmark l,Transform root,ref Vector3 at,ref Vector3 target)
  {
   if(l.kind=="lake"||l.kind=="arena")return;var targets=new List<Vector3>();
   if(l.kind=="fort"||l.kind=="cave"||l.kind=="palace"||l.kind=="rocks")
   {var decor=root.Find("Explore Landmark "+l.id);if(decor)foreach(Transform t in decor)if(t.name.StartsWith("WallScenery_"))targets.Add(t.position+Vector3.up*3.3f);}
   else foreach(var p in map.props){var c=map.Cell(p.pos);if(l.Contains(c)&&(p.kind=="tent"||p.kind=="mushroom"))targets.Add(p.pos+Vector3.up*1.4f);}
   float best=float.MaxValue;foreach(var aim in targets)for(int x=l.rect.xMin;x<=l.rect.xMax;x++)for(int z=l.rect.yMin;z<=l.rect.yMax;z++)
   {var p=map.World(x,z);if(!map.Walkable(p))continue;var flat=aim-p;flat.y=0;float d=flat.magnitude;if(d<4||d>15)continue;bool clear=true;for(float travel=.5f;travel<d-1.6f;travel+=.5f){var c=map.Cell(p+flat*(travel/d));if(map.At(c.x,c.y)!=ExCell.Floor){clear=false;break;}}if(!clear)continue;float score=Mathf.Abs(d-7)+.1f*Mathf.Abs(x-(l.rect.xMin+l.rect.xMax)*.5f);if(score<best){best=score;at=p;target=aim;}}
  }
  static void Catalogue(Camera cam)
  {
   GroundCatalogue(cam);
   foreach(string kind in new[]{"tree","stump","boulder","mushroom","tent","crate","totem","pillar","statue","campfire","altar"})for(int i=0;i<(kind=="tree"?5:3);i++)
   {var g=ExploreArt.CreateProp(new ExProp{kind=kind,variant=i,radius=1});Fit(cam,g,"explore_prop_"+kind+"_"+i);UnityEngine.Object.DestroyImmediate(g);}
   foreach(ExTerrain t in Enum.GetValues(typeof(ExTerrain))){var g=ExploreArt.CreateWall(t,1,2,3);Fit(cam,g,"explore_wall_"+t);UnityEngine.Object.DestroyImmediate(g);}
   for(int n=1;n<=4;n++){var g=ExploreArt.CreateArena(n,4);Fit(cam,g,"explore_arena_"+n);UnityEngine.Object.DestroyImmediate(g);}
   // Equal-camera comparison: repeated species must differ in silhouette, not only colour.
   var lineup=new GameObject("Tree variation lineup");
   for(int i=0;i<4;i++){var g=ExploreArt.CreateProp(new ExProp{kind="tree",variant=1,radius=1,pos=new Vector3(i*.1f,0,0)});g.transform.SetParent(lineup.transform,false);g.transform.localPosition=new Vector3(i*2.25f,0,0);}
   Fit(cam,lineup,"explore_tree_variation_lineup");UnityEngine.Object.DestroyImmediate(lineup);
   lineup=new GameObject("Wall variation lineup");for(int i=0;i<4;i++){var g=ExploreArt.CreateWall(ExTerrain.Thicket,i,0,3);g.transform.SetParent(lineup.transform,false);g.transform.localPosition=new Vector3(i*3.3f,0,0);}Fit(cam,lineup,"explore_thicket_variation_lineup");UnityEngine.Object.DestroyImmediate(lineup);
   lineup=new GameObject("Tent true scale comparison");var tent=ExploreArt.CreateProp(new ExProp{kind="tent",radius=.9f});tent.transform.SetParent(lineup.transform,false);
   var monster=MinionRig.Create();monster.SetVariant(9);monster.transform.SetParent(lineup.transform,false);monster.transform.localPosition=new Vector3(1.3f,0,.3f);
   var human=CharacterRig.Create("swordsman");human.transform.SetParent(lineup.transform,false);human.transform.localPosition=new Vector3(-1.45f,0,0);
   Fit(cam,lineup,"explore_tent_scale");UnityEngine.Object.DestroyImmediate(lineup);
   var map=JungleMaze.Build();foreach(var seal in map.seals){var g=ExploreArt.CreateSeal(seal,0,0,3);Fit(cam,g,"explore_"+seal.id+"_closed");seal.have=seal.need;ExploreArt.SetSealOpen(g,false);Fit(cam,g,"explore_"+seal.id+"_ready");ExploreArt.SetSealOpen(g,true);if(g.transform.Find("Closed").gameObject.activeSelf)throw new Exception("Seal did not hide");ExploreArt.SetSealOpen(g,false);if(!g.transform.Find("Closed").gameObject.activeSelf)throw new Exception("Seal did not close");UnityEngine.Object.DestroyImmediate(g);}
   foreach(var npc in map.npcs){var rig=CharacterRig.Create(npc.look);rig.InCombat=false;ExploreArt.DressNpc(rig,npc.id);Fit(cam,rig.gameObject,"explore_npc_"+npc.id);UnityEngine.Object.DestroyImmediate(rig.gameObject);}
   // Warm factories twice: the second cycle must reuse every mesh and material.
   int mesh=0,mat=0,tex=0;for(int pass=0;pass<2;pass++){foreach(ExTerrain t in Enum.GetValues(typeof(ExTerrain)))for(int i=0;i<4;i++)UnityEngine.Object.DestroyImmediate(ExploreArt.CreateWall(t,i,0,3));foreach(ExGround g in Enum.GetValues(typeof(ExGround)))UnityEngine.Object.DestroyImmediate(ExploreArt.CreateGround(g,17,23,3));for(int i=0;i<5;i++)UnityEngine.Object.DestroyImmediate(ExploreArt.CreateProp(new ExProp{kind="tree",variant=i,radius=.6f}));int m=Resources.FindObjectsOfTypeAll<Mesh>().Length,mt=Resources.FindObjectsOfTypeAll<Material>().Length,tx=Resources.FindObjectsOfTypeAll<Texture2D>().Length;if(pass==1&&(m!=mesh||mt!=mat||tx!=tex))throw new Exception("Explore factory failed cache check");mesh=m;mat=mt;tex=tx;}
  }
  static void GroundCatalogue(Camera cam)
  {
   var backing=GameObject.CreatePrimitive(PrimitiveType.Plane);backing.name="Preview only neutral AR floor";backing.transform.position=Vector3.down*.02f;backing.transform.localScale=Vector3.one*1.2f;
   UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());var mat=new Material(Shader.Find("Unlit/Color"));mat.color=new Color(.48f,.47f,.43f);backing.GetComponent<Renderer>().sharedMaterial=mat;
   foreach(ExGround type in Enum.GetValues(typeof(ExGround)))
   {
    var grid=new GameObject("Ground nine cell seam check");
    for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
    {
     var g=ExploreArt.CreateGround(type,x,z,3);g.transform.SetParent(grid.transform,false);g.transform.localPosition=new Vector3(x*3,0,z*3);
     var r=g.GetComponentInChildren<Renderer>();var texture=r.sharedMaterial.mainTexture as Texture2D;
     if(g.GetComponentInChildren<Collider>()||!texture||texture.mipmapCount<2||texture.wrapMode!=TextureWrapMode.Repeat||r.sharedMaterial.shader.name!="SpellDuel/ExploreGround"||!r.sharedMaterial.shader.isSupported)throw new Exception("Ground material audit failed: "+type);
     if(g.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length!=6)throw new Exception("Ground tile exceeds two triangles");
    }
    cam.orthographic=true;cam.orthographicSize=4.7f;cam.transform.position=Vector3.up*9;cam.transform.rotation=Quaternion.Euler(90,0,0);Save(cam,"explore_ground_"+type+"_top",960,960);
    if(type==ExGround.Grass)
    {
     var image=new Texture2D(2,2);image.LoadImage(File.ReadAllBytes("snapshots/explore_ground_Grass_top.png"));
     for(int x=0;x<3;x++)for(int y=0;y<3;y++){var c=image.GetPixel(174+x*306,174+y*306);if(c.g-c.r<.012f)throw new Exception("Grass tile missing in native render: "+x+","+y);}
     UnityEngine.Object.DestroyImmediate(image);
    }
    cam.orthographic=false;cam.fieldOfView=65;cam.transform.position=new Vector3(.4f,1.65f,-3.6f);cam.transform.LookAt(new Vector3(0,0,.5f));Save(cam,"explore_ground_"+type+"_eye",960,960);UnityEngine.Object.DestroyImmediate(grid);
   }
   UnityEngine.Object.DestroyImmediate(backing);UnityEngine.Object.DestroyImmediate(mat);cam.orthographic=false;cam.fieldOfView=35;
   File.WriteAllText("snapshots/explore_ground_audit.json","{\"types\":5,\"trianglesPerTile\":2,\"generatedTextures\":true,\"mipmaps\":true,\"repeatingTextures\":true,\"noColliders\":true,\"nineTilesVisibleInNativePixels\":true,\"neutralFloorPreviewOnly\":true}");
  }
  static void Fit(Camera cam,GameObject g,string name)
  {var bounds=new Bounds(g.transform.position,Vector3.zero);foreach(var mf in g.GetComponentsInChildren<MeshFilter>()){var renderer=mf.GetComponent<Renderer>();if(!mf.sharedMesh||!renderer||!renderer.enabled||renderer.forceRenderingOff)continue;var b=mf.sharedMesh.bounds;for(int i=0;i<8;i++){var corner=b.center+new Vector3((i&1)==0?-b.extents.x:b.extents.x,(i&2)==0?-b.extents.y:b.extents.y,(i&4)==0?-b.extents.z:b.extents.z);bounds.Encapsulate(mf.transform.TransformPoint(corner));}}var size=Mathf.Max(bounds.size.y,bounds.size.x,bounds.size.z);cam.transform.position=bounds.center+new Vector3(.7f,.35f,1).normalized*Mathf.Max(2,size*2.3f);cam.transform.LookAt(bounds.center);Save(cam,name,800,800);}
  static void Save(Camera cam,string name,int w,int h)
  {cam.aspect=w/(float)h;var rt=new RenderTexture(w,h,24);var old=RenderTexture.active;cam.targetTexture=rt;RenderTexture.active=rt;cam.Render();var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes("snapshots/"+name+".png",tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);}
 }
}
#endif
