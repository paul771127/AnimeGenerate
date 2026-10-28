#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  public static void RenderRemainingArt()
  {
   Directory.CreateDirectory("snapshots");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();
   var cam=new GameObject("Remaining art camera").AddComponent<Camera>();cam.tag="MainCamera";cam.nearClipPlane=.05f;cam.fieldOfView=65;cam.backgroundColor=new Color(.37f,.43f,.51f);
   var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(40,150,0);
   RenderSettings.ambientLight=new Color(.6f,.6f,.65f);
   var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*3;floor.GetComponent<Renderer>().sharedMaterial=new Material(Shader.Find("Standard")){color=new Color(.32f,.36f,.30f)};
   RemainingArtShots(cam);
  }
  static void SwordShieldShots(Camera cam)
  {
   var owner=CharacterRig.Create("swordsman");cam.transform.position=new Vector3(0,1.6f,0);
   SpellFx.SwordShieldGain(owner.transform,.7f);var fx=owner.GetComponent<SwordShieldView>();
   int materials=Resources.FindObjectsOfTypeAll<Material>().Length,meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;
   for(int i=0;i<12;i++){SpellFx.SwordShieldGain(owner.transform,.7f);SpellFx.SwordShieldAura(owner.transform,.7f);}
   if(fx.GainBursts!=1)throw new Exception("Sword shield gain not throttled");
   if(Resources.FindObjectsOfTypeAll<Material>().Length!=materials||Resources.FindObjectsOfTypeAll<Mesh>().Length!=meshes)throw new Exception("Sword shield refresh allocation");
   fx.Tick(.31f);SpellFx.SwordShieldGain(owner.transform,1);if(fx.GainBursts!=2)throw new Exception("Sword shield gain never recovers");
   cam.transform.position=new Vector3(3,2,4);fx.Tick(.12f);
   CarpetShot(cam,owner.gameObject,"remaining_shield_observer",new Vector3(2,1.5f,3),new Vector3(0,.8f,0),720,1000);
   foreach(var r in owner.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
   cam.transform.position=new Vector3(0,1.6f,0);fx.Tick(0);
   CarpetShot(cam,owner.gameObject,"remaining_shield_first",new Vector3(0,1.6f,0),new Vector3(0,1.3f,3),720,1100);
   SpellFx.SwordShieldAura(owner.transform,0);if(owner.transform.Find("Blue sword qi shield").gameObject.activeSelf)throw new Exception("Shield zero did not hide");
   SpellFx.SwordShieldAura(owner.transform,1);owner.gameObject.SetActive(false);if(owner.transform.Find("Blue sword qi shield").gameObject.activeSelf)throw new Exception("Shield disable cleanup");
   UnityEngine.Object.DestroyImmediate(owner.gameObject);var edge=cam.GetComponent<SelfFxEdge>();if(edge)UnityEngine.Object.DestroyImmediate(edge);
  }
  static void RemainingArtShots(Camera cam)
  {
   float fov=cam.fieldOfView;cam.fieldOfView=65;
   for(int i=0;i<2;i++)
   {
    bool strong=i==1;float radius=strong?3:1.5f;string id=strong?"heaven":"judgement";
    cam.transform.SetPositionAndRotation(new Vector3(0,1.6f,0),Quaternion.Euler(18,0,0));
    var ground=SpellFx.HolyGround(radius,strong);SpellFx.HolyLanding(Vector3.zero,strong);
    var burst=UnityEngine.Object.FindObjectOfType<HolySelfBurst>();if(!burst)throw new Exception("Missing holy landing");burst.Tick(.15f);
    int rays=0;foreach(var l in burst.GetComponentsInChildren<LineRenderer>())if(l.name.StartsWith("Perimeter light")){var p=l.GetPosition(0);if(new Vector2(p.x,p.z).magnitude<radius*.9f)throw new Exception("Holy beam obstructs centre");rays++;}
    if(rays!=(strong?12:8))throw new Exception("Holy perimeter count");
    CarpetShot(cam,ground,"remaining_holy_"+id+"_first",new Vector3(0,1.6f,0),new Vector3(0,.3f,3),720,1100);
    var edge=cam.GetComponent<SelfFxEdge>();if(edge)UnityEngine.Object.DestroyImmediate(edge);
    CarpetShot(cam,ground,"remaining_holy_"+id+"_observer",new Vector3(4,4,6),Vector3.zero,960,720);
    burst.Tick(1);if(burst && burst.gameObject.activeSelf)throw new Exception("Holy burst cleanup");UnityEngine.Object.DestroyImmediate(ground);
   }
   SwordShieldShots(cam);
   cam.fieldOfView=fov;File.WriteAllText("snapshots/remaining_art_audit.json","{\"holyCentreClear\":true,\"holyRadii\":[1.5,3],\"holyCleanup\":true,\"shieldThrottleCacheCleanup\":true}");
  }
 }
}
#endif
