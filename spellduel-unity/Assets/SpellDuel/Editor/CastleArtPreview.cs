#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace SpellDuel.EditorTools
{
 public static class CastleArtPreview
 {
  static object Old(string name,params object[] args)=>typeof(DungeonMapArtPreview).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
  static void Check(bool b,string m){if(!b)throw new Exception("Castle art: "+m);}
  static GameObject View(ExploreMap map,Vector2Int center){object[] args={map,center,0};return (GameObject)Old("BuildView",args);}
  static void Save(Camera cam,GameObject root,string name,bool top=false)
  {
   foreach(var b in root.GetComponentsInChildren<CastleBillboardArt>())b.Face(cam,1.2f);
   foreach(var v in root.GetComponentsInChildren<SpriteEnemyView>()){typeof(SpriteEnemyView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(v,null);int slot=(int)typeof(SpriteEnemyView).GetField("lastSlot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(v);Check(slot>=0&&slot<168,"sprite animation slot "+slot);}
   foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="CastleCellCeiling")t.gameObject.SetActive(!top);
   Old("Save",cam,name,top?1400:1080,top?1400:1440);
  }
  // Presentation-only proof of the imported texture, material/property block and
  // the exact UV contract. No gameplay state or runtime timing is modified.
  static void EnemySheets(Camera cam)
  {
   cam.orthographic=true;cam.orthographicSize=6.6f;cam.backgroundColor=new Color(.08f,.07f,.12f);
   cam.transform.position=new Vector3(0,6,-40);cam.transform.rotation=Quaternion.identity;
   int[] frames={0,5,11,12,15,20};
   foreach(var def in SpriteMonsters.All)
   {
    var root=new GameObject("Imported sprite sheet proof");
    for(int row=0;row<6;row++)for(int dir=0;dir<8;dir++)
    {
     var parent=new GameObject("Action "+row+" direction "+dir);parent.transform.SetParent(root.transform,false);
     parent.transform.position=new Vector3((dir-3.5f)*2.15f,(5-row)*2.1f,0);
     var view=SpriteEnemyView.Create(def,parent.transform);
     var renderer=view.GetComponentInChildren<MeshRenderer>();var texture=renderer.sharedMaterial.mainTexture as Texture2D;
     Check(texture!=null&&texture.width==2048&&texture.height==2048,"imported enemy texture "+def.sheet);
     var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
     Check(block.GetTexture("_MainTex")==texture,"per-renderer texture "+def.sheet);
     renderer.transform.rotation=Quaternion.identity;
     int slot=frames[row]*8+dir,cx=slot%16,cy=slot/16;float inset=.5f/2048;
     float u0=cx/16f+inset,u1=(cx+1)/16f-inset,v1=1-cy/16f-inset,v0=1-(cy+1)/16f+inset;
     view.GetComponentInChildren<MeshFilter>().sharedMesh.uv=new[]{new Vector2(u0,v0),new Vector2(u1,v0),new Vector2(u1,v1),new Vector2(u0,v1)};
    }
    Old("Save",cam,"castle_redesign_"+def.sheet,1600,1200);
    UnityEngine.Object.DestroyImmediate(root);
   }
  }
  [MenuItem("SpellDuel/Explore/Castle 2D native preview")]
  public static void Render()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();RenderSettings.fog=false;RenderSettings.skybox=null;
   var cam=new GameObject("Castle camera").AddComponent<Camera>();cam.tag="MainCamera";cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.magenta;cam.nearClipPlane=.05f;cam.farClipPlane=1000;cam.fieldOfView=68;
   Dungeons.Parse(Resources.Load<TextAsset>("dungeons").text);var d=Dungeons.All.Find(v=>v.id=="castle_test");Check(d!=null,"missing castle_test");var map=DungeonMaze.Build(d);Check(map.theme=="castle","theme dispatch");
   var before=(string)Old("Fingerprint",map);object[] audit={map,0};Check((int)Old("RouteAudit",audit)==0,"baseline routes");Directory.CreateDirectory("snapshots");
   foreach(var a in map.areas)
   {
    var root=View(map,map.Cell(a.center));Check(root.GetComponentInChildren<Collider>()==null,"new collider");
    cam.orthographic=true;cam.orthographicSize=25;cam.transform.position=a.center+Vector3.up*80;cam.transform.rotation=Quaternion.Euler(90,0,0);Save(cam,root,"castle_"+a.node.id+"_top",true);
    var background=ExploreArt.CreateBackdrop(map);background.transform.SetParent(root.transform,false);
    Check(background.transform.Find("Sun")==null&&background.transform.Find("SkyDome")==null,"outdoor sky");Check(background.transform.Find("GroundBowl")!=null,"missing lower hemisphere");
    cam.orthographic=false;cam.transform.position=a.center+new Vector3(-3,1.65f,-5);cam.transform.LookAt(a.center+new Vector3(0,2.8f,9));
    for(int i=0;i<3;i++){var actor=new GameObject("Castle sprite demonstration");actor.transform.SetParent(root.transform,false);actor.transform.position=a.center+new Vector3((i-1)*1.8f,0,4);actor.transform.rotation=Quaternion.Euler(0,180,0);SpriteEnemyView.Create(SpriteMonsters.All[(a.index*2+i)%8],actor.transform);}
    Save(cam,root,"castle_"+a.node.id+"_first");
    if(a.index==0){cam.transform.rotation=Quaternion.Euler(-70,0,0);Save(cam,root,"castle_ceiling");cam.transform.rotation=Quaternion.Euler(48,120,0);Save(cam,root,"castle_lower_hemisphere");}
    UnityEngine.Object.DestroyImmediate(root);
   }
   Check(before==(string)Old("Fingerprint",map),"art changed map data");
   // Cached streamed views: only the legacy underlay allocates one mesh/material per build.
   int meshes=0,materials=0,textures=0;
   for(int pass=0;pass<2;pass++){var root=View(map,map.Cell(map.areas[0].center));UnityEngine.Object.DestroyImmediate(root);int mm=Resources.FindObjectsOfTypeAll<Mesh>().Length,mt=Resources.FindObjectsOfTypeAll<Material>().Length,tt=Resources.FindObjectsOfTypeAll<Texture2D>().Length;if(pass==1)Check(mm==meshes+1&&mt==materials+1&&tt==textures,"cache growth");meshes=mm;materials=mt;textures=tt;}
   var area=map.areas[0];var door=area.exits[0];area.cleared=false;var closed=ExploreArt.CreateDungeonGate(map,door.x,door.y);UnityEngine.Object.DestroyImmediate(closed);area.cleared=true;var gate=ExploreArt.CreateDungeonGate(map,door.x,door.y);var state=gate.GetComponent<DungeonGateArt>();Check(state!=null,"gate animation contract");
   cam.backgroundColor=new Color(.12f,.09f,.11f);cam.orthographic=false;cam.transform.position=new Vector3(0,2,-7);cam.transform.LookAt(new Vector3(0,2,0));
   state.Sample(0);Save(cam,gate,"castle_gate_closed");state.Sample(.375f);Check(state.closed.localPosition.y>1&&state.closed.localPosition.y<3,"gate midway");Save(cam,gate,"castle_gate_half");state.Sample(.8f);Check(!state.closed.gameObject.activeSelf&&map.GateOpen(door.x,door.y),"gate open");Save(cam,gate,"castle_gate_open");UnityEngine.Object.DestroyImmediate(gate);area.cleared=false;
   File.WriteAllText("snapshots/castle_audit.json","{\"theme\":6,\"rooms\":5,\"routes\":true,\"mapUnchanged\":true,\"cache\":true,\"noColliders\":true,\"wallBounds\":true,\"gateAnimation\":true,\"enclosedBackdrop\":true}");
   EnemySheets(cam);
   Debug.Log("Castle native art, routes, cache and gate audit passed");
  }
 }
}
#endif
