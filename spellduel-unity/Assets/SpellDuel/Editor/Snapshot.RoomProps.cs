#if UNITY_EDITOR
using System;using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  static void RoomPropShots(Camera cam)
  {
   foreach(var id in new[]{"ev_core","ev_totem_0","ev_statue_0","ev_chest_0"})
   {
    var v=HazardArt.RoomProp(id,Battle.ElementColor(1));v.Tick(100,100,false,1);
    Shot(cam,v.gameObject,"room_"+id,new Vector3(2,2,3.5f),new Vector3(0,.7f,0),720,900);
    v.Pulse(2);v.Tick(40,100,true,2.1f);
    Shot(cam,v.gameObject,"room_"+id+"_lit",new Vector3(2,2,3.5f),new Vector3(0,.7f,0),720,900);
    if(id=="ev_core" && !v.Cracks.activeSelf)throw new Exception("Damaged bell cracks hidden");
    UnityEngine.Object.DestroyImmediate(v.gameObject);
   }
   foreach(var kind in new[]{"runes","orb","chant","escort","collapse","eyes"})
   {
    var v=HazardArt.RoomPoint(kind,2,Battle.ElementColor(2));v.Tick(1,1);
    Shot(cam,v.gameObject,"room_point_"+kind,new Vector3(2,3,4),new Vector3(0,kind=="chant"?.8f:0,0),960,720);
    if(kind=="collapse"){v.Tick(2,2);v.Debris.Simulate(.15f,true,true);Shot(cam,v.gameObject,"room_collapse_open",new Vector3(2,3,4),Vector3.zero,960,720);}
    UnityEngine.Object.DestroyImmediate(v.gameObject);
   }
   Debug.Log("[SpellDuel] Room prop art audit passed");
  }
 }
}
#endif
