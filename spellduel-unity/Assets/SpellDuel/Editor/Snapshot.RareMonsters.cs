#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  const BindingFlags RareFlags=BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Public;
  static readonly string[] RareIds={"ancient_root_beast","tidewhip_eel","chain_gladiator","riftplate_lizard"};
  [Serializable] public class RareAuditRow {public int kind,triangles,renderers,registeredColours,registeredEyes,chargedSurfaces;public Vector3 oldSize,newSize;public bool sharedMeshes,pivotsUnchanged,hitChangesColour,deathChangesColour,chargeChangesColour;}
  [Serializable] public class RareAuditReport {public string unity;public RareAuditRow[] enemies;}
  static object RareField(MinionRig r,string name)=>typeof(MinionRig).GetField(name,RareFlags).GetValue(r);
  static void RareTick(MinionRig r,float dt)=>typeof(MinionRig).GetMethod("AnimateAreaEnemy",RareFlags).Invoke(r,new object[]{dt});
  static MinionRig RareCreate(int kind,bool legacy=false)
  {
   typeof(MinionRig).GetField("rarePreviewLegacy",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,legacy);
   UnityEngine.Random.InitState(1900+kind);var r=MinionRig.Create();r.SetVariant(kind);typeof(MinionRig).GetField("clock",RareFlags).SetValue(r,0f);RareTick(r,0);
   typeof(MinionRig).GetField("rarePreviewLegacy",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);return r;
  }
  static string RarePath(Transform t,Transform root){if(!t)return "null";if(t==root)return "root";return RarePath(t.parent,root)+"/"+t.name;}
  static List<string> RarePivots(MinionRig r)
  {
   var names=new List<string>();foreach(var f in new[]{"body","spine","head","areaFrontL","areaFrontR","areaTail","areaWeapon"})names.Add(f+":"+RarePath((Transform)RareField(r,f),r.transform));
   foreach(var t in (Transform[])RareField(r,"areaLegs"))names.Add(RarePath(t,r.transform));return names;
  }
  static Bounds RareBounds(MinionRig r)
  {
   var bounds=new Bounds(r.transform.position,Vector3.zero);bool first=true;
   foreach(var f in r.GetComponentsInChildren<MeshFilter>(true)){if(!f.transform.IsChildOf((Transform)RareField(r,"body")))continue;var b=f.sharedMesh.bounds;
    for(int i=0;i<8;i++){var p=f.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}}
   return bounds;
  }
  static Color RareColour(Renderer r){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);return block.GetColor("_Color");}
  static void RareRequire(bool condition,string text){if(!condition)throw new Exception("Rare monster audit: "+text);}
  static RareAuditRow RareAudit(MinionRig r,MinionRig old,int kind)
  {
   var a=new RareAuditRow{kind=kind,oldSize=RareBounds(old).size,newSize=RareBounds(r).size};var meshes=r.GetComponentsInChildren<MeshFilter>();a.renderers=r.GetComponentsInChildren<Renderer>().Length;
   foreach(var f in meshes)for(int sub=0;sub<f.sharedMesh.subMeshCount;sub++)a.triangles+=(int)f.sharedMesh.GetIndexCount(sub)/3;
   RareRequire(a.triangles<=4000,kind+" has "+a.triangles+" triangles");RareRequire(!r.GetComponentInChildren<Collider>(),"art adds collider");
   a.pivotsUnchanged=string.Join("\n",RarePivots(r))==string.Join("\n",RarePivots(old));RareRequire(a.pivotsUnchanged,"pivot hierarchy changed "+kind);
   var clone=RareCreate(kind);var other=clone.GetComponentsInChildren<MeshFilter>();a.sharedMeshes=meshes.Length==other.Length;for(int i=0;i<Math.Min(meshes.Length,other.Length);i++)a.sharedMeshes&=meshes[i].sharedMesh==other[i].sharedMesh;UnityEngine.Object.DestroyImmediate(clone.gameObject);RareRequire(a.sharedMeshes,"cache miss "+kind);
   a.registeredColours=((IList)RareField(r,"rockList")).Count;a.registeredEyes=((IList)RareField(r,"eyeList")).Count;
   foreach(float g in (IList)RareField(r,"rockGlows"))if(g>0)a.chargedSurfaces++;
   RareRequire(a.registeredColours>0&&a.registeredEyes>0&&a.chargedSurfaces>0,"empty colour registration "+kind);
   RareRequire(a.registeredColours==((IList)RareField(r,"rockCols")).Count&&a.registeredColours==((IList)RareField(r,"rockGlows")).Count,"colour arrays mismatch");
   RareRequire(a.newSize.x<=a.oldSize.x*1.25f+.08f&&a.newSize.y<=a.oldSize.y*1.25f+.08f&&a.newSize.z<=a.oldSize.z*1.25f+.08f,"size grew too much "+kind+" "+a.oldSize+" -> "+a.newSize);
   var renderer=(Renderer)((IList)RareField(r,"rockList"))[0];var baseC=RareColour(renderer);r.PlayHit();RareTick(r,0);a.hitChangesColour=RareColour(renderer)!=baseC;RareTick(r,1);
   r.Windup(1.5f);RareTick(r,1.5f);a.chargeChangesColour=RareColour(renderer)!=baseC;
   r.SetDead(true);RareTick(r,.9f);a.deathChangesColour=RareColour(renderer)!=baseC;RareRequire(a.hitChangesColour&&a.chargeChangesColour&&a.deathChangesColour,"colour animation failed "+kind);
   return a;
  }
  static void RareShot(Camera cam,GameObject subject,string name,Bounds bounds,Vector3 direction)
  {
   cam.transform.position=bounds.center+direction.normalized*7;cam.transform.LookAt(bounds.center);float halfX=0,halfY=0;
   for(int i=0;i<8;i++){var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var v=cam.transform.InverseTransformPoint(p);halfX=Mathf.Max(halfX,Mathf.Abs(v.x));halfY=Mathf.Max(halfY,Mathf.Abs(v.y));}
   cam.orthographicSize=Mathf.Max(halfX,halfY)*1.14f;
   Shot(cam,subject,name,cam.transform.position,bounds.center,960,960);
  }
  public static void RenderRareMonsters()
  {
   Directory.CreateDirectory("snapshots");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();
   foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())if(light.type==LightType.Directional)UnityEngine.Object.DestroyImmediate(light.gameObject);
   var lamp=new GameObject("Rare portrait key").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.15f;lamp.transform.rotation=Quaternion.Euler(38,155,0);
   RenderSettings.ambientLight=new Color(.56f,.59f,.64f);
   var cam=new GameObject("Rare art camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.76f,.81f,.84f);cam.orthographic=true;cam.orthographicSize=1.43f;cam.nearClipPlane=.02f;
   var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*4;floor.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Standard")){color=new Color(.48f,.53f,.47f)};
   var rows=new List<RareAuditRow>();
   for(int k=19;k<=22;k++){
    var old=RareCreate(k,true);old.gameObject.SetActive(false);var current=RareCreate(k);rows.Add(RareAudit(current,old,k));UnityEngine.Object.DestroyImmediate(current.gameObject);UnityEngine.Object.DestroyImmediate(old.gameObject);
    var frameBounds=new Bounds[3];
    for(int pass=0;pass<2;pass++){
     var r=RareCreate(k,pass==0);
     for(int state=0;state<3;state++){
      if(state==1){r.Windup(1.5f);RareTick(r,1.5f);}if(state==2){r.Attack();RareTick(r,.20f);}
      if(pass==0)frameBounds[state]=RareBounds(r);else frameBounds[state].Encapsulate(RareBounds(r));
     }UnityEngine.Object.DestroyImmediate(r.gameObject);
    }
    for(int pass=0;pass<2;pass++){
     var r=RareCreate(k,pass==0);string prefix="rare_"+k+"_"+(pass==0?"before":"after");
     RareShot(cam,r.gameObject,prefix+"_front",frameBounds[0],new Vector3(0,.05f,1));
     RareShot(cam,r.gameObject,prefix+"_side",frameBounds[0],new Vector3(1,.06f,0));
     RareShot(cam,r.gameObject,prefix+"_rear45",frameBounds[0],new Vector3(.7f,.15f,-1));
     r.Windup(1.5f);RareTick(r,1.5f);RareShot(cam,r.gameObject,prefix+"_windup",frameBounds[1],new Vector3(.6f,.2f,.8f));
     r.Attack();RareTick(r,.20f);RareShot(cam,r.gameObject,prefix+"_attack",frameBounds[2],new Vector3(.6f,.2f,.8f));
     UnityEngine.Object.DestroyImmediate(r.gameObject);
    }
   }
   for(int pass=0;pass<2;pass++){
    var root=new GameObject("True scale lineup");for(int i=0;i<4;i++){var r=RareCreate(19+i,pass==0);r.transform.SetParent(root.transform,false);r.transform.localPosition=new Vector3((i-2)*3.3f,0,0);}
    var ordinary=MinionRig.Create();ordinary.SetVariant(8);ordinary.transform.SetParent(root.transform,false);ordinary.transform.localPosition=new Vector3(6.1f,0,0);
    var player=CharacterRig.Create("novice");player.InCombat=false;player.transform.SetParent(root.transform,false);player.transform.localPosition=new Vector3(8f,0,0);
    cam.orthographicSize=3.1f;Shot(cam,root,"rare_lineup_"+(pass==0?"before":"after"),new Vector3(.8f,3,14),new Vector3(.8f,.85f,0),2800,900);UnityEngine.Object.DestroyImmediate(root);
   }
   File.WriteAllText("snapshots/rare_audit.json",JsonUtility.ToJson(new RareAuditReport{unity=Application.unityVersion,enemies=rows.ToArray()},true));Debug.Log("Rare monster audit and native previews complete");
  }
 }
}
#endif
