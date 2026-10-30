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
   BlueprintGearShots(cam);
  }
  [Serializable] public class BlueprintAuditRow {public string id,cls;public int triangles;public bool restored;}
  [Serializable] public class BlueprintAudit {public BlueprintAuditRow[] gear;public int icons;public bool iconCache;}
  static void BlueprintGearShots(Camera cam)
  {
   var rows=new List<BlueprintAuditRow>();var iconNames=new List<string>();float fov=cam.fieldOfView;cam.fieldOfView=30;
   foreach(var bp in Blueprints.All)
   {
    string[] classes=bp.slot==Slot.Weapon?new[]{"swordsman","archer","mage"}:new[]{"novice","priest"};
    foreach(string cls in classes)
    {
     var rig=CharacterRig.Create(cls);rig.InCombat=false;PoseStep(rig,.6f);
     var originals=rig.GetComponentsInChildren<Renderer>();var hidden=new bool[originals.Length];for(int k=0;k<originals.Length;k++)hidden[k]=originals[k].forceRenderingOff;
     var look=new GearLook();look.pieces[(int)bp.slot]=new GearLook.Piece{rarity=bp.rarity,special=bp.id,set=""};rig.SetGear(look);rig.InCombat=true;PoseStep(rig,.6f);
     int triangles=0;foreach(var motion in rig.GetComponentsInChildren<BlueprintGearMotion>()){motion.Sample(.3f);foreach(var mesh in motion.GetComponentsInChildren<MeshFilter>())triangles+=mesh.sharedMesh.triangles.Length/3;foreach(var line in motion.GetComponentsInChildren<LineRenderer>())triangles+=line.positionCount*6;}
     if(triangles<=0||triangles>1500)throw new Exception(bp.id+" gear budget "+triangles);
     CarpetFit(cam,rig.gameObject,"blueprint_"+bp.id+"_"+cls,new Vector3(.35f,.08f,1),640,900);
     if(cls==classes[0])
     {
      var effects=rig.GetComponentsInChildren<BlueprintGearMotion>();
      rig.MoveSpeed=1.5f;rig.PlayHit();typeof(CharacterRig).GetField("poseLastLift",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(rig,.20f);
      int meshCount=Resources.FindObjectsOfTypeAll<Mesh>().Length,matCount=Resources.FindObjectsOfTypeAll<Material>().Length;
      for(int frame=0;frame<60;frame++)foreach(var effect in effects)effect.Sample(.016f);
      if(meshCount!=Resources.FindObjectsOfTypeAll<Mesh>().Length||matCount!=Resources.FindObjectsOfTypeAll<Material>().Length)throw new Exception("Blueprint detail cache allocation "+bp.id);
      foreach(var ps in rig.GetComponentsInChildren<ParticleSystem>())ps.Simulate(.8f,true,true);
      CarpetFit(cam,effects[0].gameObject,"blueprint_detail_"+bp.id,new Vector3(.15f,.15f,1),720,720);
     }
     rig.SetGear(null);bool restored=true;for(int k=0;k<originals.Length;k++)if(originals[k] && originals[k].forceRenderingOff!=hidden[k])restored=false;
     if(!restored)throw new Exception("Blueprint unequip did not restore "+bp.id);
     rows.Add(new BlueprintAuditRow{id=bp.id,cls=cls,triangles=triangles,restored=restored});UnityEngine.Object.DestroyImmediate(rig.gameObject);
    }
    iconNames.Add(bp.id);iconNames.Add(bp.ScrollId);
   }
   iconNames.AddRange(new[]{"mat_band_crest","mat_storm_gem","mat_night_edge","mat_aid_sigil"});
   var hashes=new System.Collections.Generic.HashSet<string>();bool cache=true;
   foreach(string id in iconNames)
   {
    var tex=SkillIcons.Get(id);cache&=tex==SkillIcons.Get(id);
    var pixels=SkillIcons.Render(id,128);var png=new Texture2D(128,128,TextureFormat.RGBA32,false);png.SetPixels32(pixels);png.Apply();var bytes=png.EncodeToPNG();
    string hash=Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(bytes));if(!hashes.Add(hash))throw new Exception("Duplicate blueprint icon "+id);
    File.WriteAllBytes("snapshots/blueprint_icon_"+id+".png",bytes);UnityEngine.Object.DestroyImmediate(png);
   }
   if(iconNames.Count!=34||!cache)throw new Exception("Blueprint icon count/cache");
   File.WriteAllText("snapshots/blueprint_audit.json",JsonUtility.ToJson(new BlueprintAudit{gear=rows.ToArray(),icons=iconNames.Count,iconCache=cache},true));cam.fieldOfView=fov;
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
