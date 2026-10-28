#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  public static void RenderSkyGear()
  {
   Directory.CreateDirectory("snapshots");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();
   var cam=new GameObject("Sky gear preview").AddComponent<Camera>();cam.nearClipPlane=.05f;cam.fieldOfView=65;cam.backgroundColor=new Color(.55f,.65f,.75f);
   var light=new GameObject("Preview key").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,150,0);RenderSettings.ambientLight=new Color(.6f,.6f,.65f);
   SkyEnvironmentShots(cam);
  }
  [Serializable] public class SkyEnvironmentAudit{public int[] triangles;public bool surfaceUnchanged,cacheStable;public float highBar;}
  static void SkyEnvironmentShots(Camera cam)
  {
   float oldFov=cam.fieldOfView;cam.fieldOfView=65;
   var scene=CarpetScene.Create(new List<Vector3>{new Vector3(-2,0,-2),new Vector3(2,0,-2),new Vector3(2,0,2),new Vector3(-2,0,2)},0,Vector3.forward);
   var report=new SkyEnvironmentAudit{triangles=new int[5],surfaceUnchanged=true,cacheStable=true,highBar=CarpetObstacleView.HighBar};
   for(int s=0;s<5;s++)
   {
    scene.SetVariant(s);scene.Sample(s==3?1.24f:1.2f,0);
    foreach(var f in scene.GetComponentsInChildren<MeshFilter>())report.triangles[s]+=f.sharedMesh.triangles.Length/3;
    foreach(var l in scene.GetComponentsInChildren<LineRenderer>())report.triangles[s]+=l.positionCount*6;
    if(report.triangles[s]>40000)throw new Exception("Sky environment >40k triangles "+s+" "+report.triangles[s]);
    var cloth=scene.VisualRoot.Find("Wine woven cloth").GetComponent<MeshFilter>().sharedMesh;
    foreach(var v in cloth.vertices){var basePoint=scene.transform.TransformPoint(new Vector3(v.x,.025f,v.z));if(Vector3.Distance(scene.SurfacePoint(basePoint),scene.VisualRoot.TransformPoint(v))>.0001f)report.surfaceUnchanged=false;}
    CarpetShot(cam,scene.gameObject,"skyenv_"+s+"_first",new Vector3(0,1.5f,-1.2f),new Vector3(0,1,20),720,1100);
    CarpetShot(cam,scene.gameObject,"skyenv_"+s+"_top",new Vector3(0,18,-12),new Vector3(0,-1,7),1100,850);
   }
   int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,materials=Resources.FindObjectsOfTypeAll<Material>().Length;scene.Sample(2,.1f);scene.Sample(3,.1f);
   report.cacheStable=meshes==Resources.FindObjectsOfTypeAll<Mesh>().Length&&materials==Resources.FindObjectsOfTypeAll<Material>().Length;
   if(!report.surfaceUnchanged||!report.cacheStable||Mathf.Abs(report.highBar-1.1f)>.001f)throw new Exception("Sky contract regression");
   File.WriteAllText("snapshots/skyenv_audit.json",JsonUtility.ToJson(report,true));UnityEngine.Object.DestroyImmediate(scene.gameObject);cam.fieldOfView=oldFov;
  }
 }
}
#endif
