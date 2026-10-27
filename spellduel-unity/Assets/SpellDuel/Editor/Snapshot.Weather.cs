#if UNITY_EDITOR
using System.Collections.Generic;using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  static void WeatherShots(Camera cam)
  {
   var poly=new List<Vector3>{new Vector3(-3,0,-3),new Vector3(3,0,-3),new Vector3(3,0,3),new Vector3(-3,0,3)};
   foreach(var w in new[]{Battle.Weather.Rain,Battle.Weather.Fog,Battle.Weather.Moon,Battle.Weather.Wind})
   {
    var scene=ForestScene.Create(poly,0);scene.SetVariant(2);
    var art=StageWeatherArt.Create(w,poly,0);foreach(var p in art.GetComponentsInChildren<ParticleSystem>())p.Simulate(8,true,true);
    Shot(cam,art.gameObject,"weather_"+w,new Vector3(0,2,-2.8f),new Vector3(w==Battle.Weather.Moon?3:0,w==Battle.Weather.Moon?5:1.5f,9),1080,900);UnityEngine.Object.DestroyImmediate(art.gameObject);UnityEngine.Object.DestroyImmediate(scene.gameObject);
   }
  }
 }
}
#endif
