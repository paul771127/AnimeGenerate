#if UNITY_EDITOR
using System;using System.Collections.Generic;using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  static void SceneVariantShots(Camera cam)
  {
   var poly=new List<Vector3>{new Vector3(-3,0,-3),new Vector3(3,0,-3),new Vector3(3,0,3),new Vector3(-3,0,3)};
   for(int theme=0;theme<5;theme++)
   {
    GameObject root=theme==0?VolcanoScene.Create(poly,0).gameObject:theme==1?ForestScene.Create(poly,0).gameObject:theme==2?SeaScene.Create(poly,0).gameObject:theme==3?ArenaScene.Create(poly,0).gameObject:CanyonScene.Create(poly,0).gameObject;
    var art=root.GetComponent<SceneVariantArt>();int count=root.GetComponentsInChildren<MeshFilter>(true).Length;
    for(int v=0;v<3;v++)
    {
     art.Set(v);if(root.GetComponentsInChildren<MeshFilter>(true).Length!=count)throw new Exception("Variant allocated meshes during switch");
     Shot(cam,root,"scene_"+theme+"_"+v,new Vector3(0,5,-13),new Vector3(0,1,3),1280,800);
    }
    UnityEngine.Object.DestroyImmediate(root);
   }
   Debug.Log("[SpellDuel] Scene variant audit passed");
  }
 }
}
#endif
