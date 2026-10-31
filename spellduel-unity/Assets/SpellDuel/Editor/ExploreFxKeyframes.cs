#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static class ExploreFxKeyframes
 {
  [MenuItem("SpellDuel/Explore/Export 16 animation keyframes")]
  public static void Render()
  {
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
   var light=new GameObject("Keyframe light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=.85f;light.transform.rotation=Quaternion.Euler(25,155,0);
   var cam=new GameObject("Keyframe camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.orthographic=true;cam.nearClipPlane=.02f;cam.farClipPlane=100;cam.allowHDR=false;cam.allowMSAA=false;
   Directory.CreateDirectory("Docs/explore_fx/keyframes");Directory.CreateDirectory("snapshots");
   var shard=ExploreArt.CreateSwitch(new ExSwitch{kind="shard"});CapturePart(cam,shard,"JadeShard","fx_A5_socket_pulse",false,Vector3.forward,.45f,true);
   foreach(var id in new[]{"fx_B1_rune_idle","fx_B1_activate"}){var g=ExploreArt.CreateSwitch(new ExSwitch{kind="lever"});CapturePart(cam,g,"PressurePlate/Rune",id,false,Vector3.up,.45f);}
   var key=ExploreArt.CreateSwitch(new ExSwitch{kind="key"});CapturePart(cam,key,"BronzeKey","fx_B2_key_idle",true,new Vector3(.7f,.25f,1),.45f);
   foreach(var id in new[]{"fx_B2_shard_idle","fx_B2_pickup"}){var g=ExploreArt.CreateSwitch(new ExSwitch{kind="shard"});CapturePart(cam,g,"JadeShard",id,false,new Vector3(.7f,.25f,1),id.EndsWith("pickup")?.33f:.45f);}
   Capture(cam,ExploreArt.CreateHerb(),"fx_C3_herb",true,new Vector3(1,.1f,0),.45f);
   foreach(var id in new[]{"fx_C4_heart_rise_end","fx_C4_heart_idle","fx_C4_heart_take"}){var g=ExploreArt.CreateProp(new ExProp{kind="altar",radius=1});ExploreArt.SetAltarGlow(g,true);CapturePart(cam,g,"AncientHeart",id,false,Vector3.forward,.45f,false,.08f);}
   CapturePart(cam,ExploreArt.CreateProp(new ExProp{kind="campfire",radius=1}),"Fire","fx_D3_fire",false,new Vector3(.2f,.05f,1),.45f);
   var rig=CharacterRig.Create("priest");ExploreArt.DressNpc(rig,"spirit");CapturePart(cam,rig.gameObject,"ExploreNpcDress/Explore Forest spirit","fx_E1_spirit",false,Vector3.forward,.45f);
   var mark=new GameObject("Mark host");ExploreArt.AlertMark(mark.transform,false);CapturePart(cam,mark,"AlertMark/Glyph","fx_E2_alert_end",false,Vector3.forward,.45f);
   for(int i=1;i<=2;i++){var g=new GameObject("Quest host");ExploreArt.QuestMark(g.transform,i);CapturePart(cam,g,"QuestMark/"+(i==1?"Exclaim":"Question"),i==1?"fx_E3_exclaim":"fx_E3_question",false,Vector3.forward,.45f);}
   // Same reusable frame as the presentation API; no location text in a video input frame.
   var banner=(GameObject)typeof(ExploreArt).GetMethod("BannerModel",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);Capture(cam,banner,"fx_E4_banner_end",false,Vector3.forward,.15f,false,.27f);
   WallAudit();File.WriteAllText("snapshots/explore_keyframes_audit.json","{\"count\":16,\"width\":960,\"height\":544,\"staticOnly\":true,\"wallBudget\":300,\"wallCellBounds\":true,\"noColliders\":true}");
  }
  static void CapturePart(Camera cam,GameObject root,string path,string id,bool green,Vector3 view,float fraction,bool unlit=false,float up=0)
  {var t=root.transform.Find(path);if(!t)throw new Exception("Missing animation anchor "+path);t.SetParent(null,true);t.gameObject.SetActive(true);UnityEngine.Object.DestroyImmediate(root);Capture(cam,t.gameObject,id,green,view,fraction,unlit,up);}
  static void Capture(Camera cam,GameObject root,string id,bool green,Vector3 view,float fraction,bool nonGlow=false,float up=0)
  {
   root.transform.position=Vector3.zero;Bounds bounds=new Bounds();bool first=true;
   foreach(var r in root.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
   if(first)throw new Exception("Empty keyframe "+id);
   Mesh copy=null;if(nonGlow){var mf=root.GetComponentInChildren<MeshFilter>();copy=UnityEngine.Object.Instantiate(mf.sharedMesh);var colors=copy.colors;for(int i=0;i<colors.Length;i++)colors[i].a=1;copy.colors=colors;mf.sharedMesh=copy;}
   cam.transform.position=bounds.center+view.normalized*10;cam.transform.LookAt(bounds.center,Mathf.Abs(view.normalized.y)>.99f?Vector3.forward:Vector3.up);cam.aspect=960f/544;
   float h=0,w=0;for(int i=0;i<8;i++){var p=bounds.center+new Vector3((i&1)==0?-bounds.extents.x:bounds.extents.x,(i&2)==0?-bounds.extents.y:bounds.extents.y,(i&4)==0?-bounds.extents.z:bounds.extents.z);var q=cam.transform.InverseTransformPoint(p);h=Mathf.Max(h,Mathf.Abs(q.y));w=Mathf.Max(w,Mathf.Abs(q.x));}
   cam.orthographicSize=Mathf.Max(h/fraction,w/(.65f*cam.aspect));cam.transform.position-=cam.transform.up*(cam.orthographicSize*2*up);cam.backgroundColor=green?Color.green:Color.black;
   var rt=new RenderTexture(960,544,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;cam.targetTexture=rt;RenderTexture.active=rt;cam.Render();var tex=new Texture2D(960,544,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,544),0,0);tex.Apply();File.WriteAllBytes("Docs/explore_fx/keyframes/"+id+".png",tex.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(root);if(copy)UnityEngine.Object.DestroyImmediate(copy);
  }
  static void WallAudit()
  {
   foreach(ExTerrain terrain in Enum.GetValues(typeof(ExTerrain)))for(int i=0;i<4;i++)
   {var g=ExploreArt.CreateWall(terrain,i,0,ExploreMap.CellSize);int tris=0;foreach(var f in g.GetComponentsInChildren<MeshFilter>()){tris+=f.sharedMesh.triangles.Length/3;foreach(var vertex in f.sharedMesh.vertices){var v=g.transform.InverseTransformPoint(f.transform.TransformPoint(vertex));v=Vector3.Scale(v,g.transform.localScale);if(Mathf.Abs(v.x)>ExploreMap.CellSize*.5f+.001f||Mathf.Abs(v.z)>ExploreMap.CellSize*.5f+.001f||v.y<-.001f)throw new Exception("Wall outside 4m cell "+terrain);}}if(tris>300)throw new Exception("Wall budget "+terrain+" "+tris);if(g.GetComponentInChildren<Collider>())throw new Exception("Art added collision");UnityEngine.Object.DestroyImmediate(g);}
  }
 }
}
#endif
