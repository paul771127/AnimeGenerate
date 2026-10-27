#if UNITY_EDITOR
using System;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  static void CarpetPetsShots(Camera cam)
  {
   var poly=new System.Collections.Generic.List<Vector3>{new Vector3(-2,0,-2),new Vector3(2,0,-2),new Vector3(2,0,2),new Vector3(-2,0,2)};
   var scene=CarpetScene.Create(poly,0,Vector3.forward);
   for(int i=0;i<5;i++){scene.SetVariant(i);scene.Sample(1.2f,0);Shot(cam,scene.gameObject,"carpet_station_"+i,new Vector3(0,3,-9),new Vector3(0,1,4),960,720);}
   scene.SwayScale=0;scene.Sample(2,0);if(scene.VisualRoot.localPosition.sqrMagnitude>.000001f)throw new Exception("Sway accessibility failed");
   var point=new Vector3(.3f,.025f,.3f);if((scene.SurfacePoint(point)-point).magnitude>.0001f)throw new Exception("Surface at zero sway");
   int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;
   for(int i=0;i<100;i++){scene.SetVariant(i%5);scene.Sample(i*.04f,.04f);}
   if(Resources.FindObjectsOfTypeAll<Mesh>().Length!=meshes)throw new Exception("Carpet recreated meshes while moving");
   scene.SwayScale=1;scene.Impact(Vector3.zero,1);scene.Sample(4,.1f);
   Shot(cam,scene.gameObject,"carpet_impact",new Vector3(3,4,-6),Vector3.zero,960,720);
   UnityEngine.Object.DestroyImmediate(scene.gameObject);
   Debug.Log("[SpellDuel] Carpet scene art audit passed");
  }
 }
}
#endif
