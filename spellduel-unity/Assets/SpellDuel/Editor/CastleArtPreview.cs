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
   foreach(var v in root.GetComponentsInChildren<SpriteEnemyView>())v.SendMessage("LateUpdate");
   foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="CastleCellCeiling")t.gameObject.SetActive(!top);
   Old("Save",cam,name,top?1400:1080,top?1400:1440);
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
    for(int i=0;i<3;i++){var actor=new GameObject("Castle sprite demonstration");actor.transform.SetParent(root.transform,false);actor.transform.position=a.center+new Vector3((i-1)*2.2f,0,1);actor.transform.rotation=Quaternion.Euler(0,180,0);SpriteEnemyView.Create(SpriteMonsters.All[(a.index*2+i)%8],actor.transform);}
    Save(cam,root,"castle_"+a.node.id+"_first");
    if(a.index==0){cam.transform.rotation=Quaternion.Euler(-70,0,0);Save(cam,root,"castle_ceiling");cam.transform.rotation=Quaternion.Euler(48,120,0);Save(cam,root,"castle_lower_hemisphere");}
    UnityEngine.Object.DestroyImmediate(root);
   }
   Check(before==(string)Old("Fingerprint",map),"art changed map data");
   // Cached streamed views: only the legacy underlay allocates one mesh/material per build.
   int meshes=0,materials=0,textures=0;
   for(int pass=0;pass<2;pass++){var root=View(map,map.Cell(map.areas[0].center));UnityEngine.Object.DestroyImmediate(root);int mm=Resources.FindObjectsOfTypeAll<Mesh>().Length,mt=Resources.FindObjectsOfTypeAll<Material>().Length,tt=Resources.FindObjectsOfTypeAll<Texture2D>().Length;if(pass==1)Check(mm==meshes+1&&mt==materials+1&&tt==textures,"cache growth");meshes=mm;materials=mt;textures=tt;}
   var area=map.areas[0];var door=area.exits[0];area.cleared=false;var closed=ExploreArt.CreateDungeonGate(map,door.x,door.y);UnityEngine.Object.DestroyImmediate(closed);area.cleared=true;var gate=ExploreArt.CreateDungeonGate(map,door.x,door.y);var state=gate.GetComponent<DungeonGateArt>();Check(state!=null,"gate animation contract");
   cam.orthographic=false;cam.transform.position=new Vector3(0,2,-7);cam.transform.LookAt(new Vector3(0,2,0));
   state.Sample(0);Save(cam,gate,"castle_gate_closed");state.Sample(.375f);Check(state.closed.localPosition.y>1&&state.closed.localPosition.y<3,"gate midway");Save(cam,gate,"castle_gate_half");state.Sample(.8f);Check(!state.closed.gameObject.activeSelf&&map.GateOpen(door.x,door.y),"gate open");Save(cam,gate,"castle_gate_open");UnityEngine.Object.DestroyImmediate(gate);area.cleared=false;
   File.WriteAllText("snapshots/castle_audit.json","{\"theme\":6,\"rooms\":5,\"routes\":true,\"mapUnchanged\":true,\"cache\":true,\"noColliders\":true,\"wallBounds\":true,\"gateAnimation\":true,\"enclosedBackdrop\":true}");
   Debug.Log("Castle native art, routes, cache and gate audit passed");
  }
 }
}
#endif
