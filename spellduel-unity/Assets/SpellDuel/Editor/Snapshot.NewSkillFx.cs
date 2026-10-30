#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  public static void RenderNewSkillFx()
  {
   Directory.CreateDirectory("snapshots");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();WorldFrame.Reset();
   var cam=new GameObject("FX preview").AddComponent<Camera>();cam.nearClipPlane=.05f;cam.fieldOfView=55;cam.backgroundColor=new Color(.12f,.16f,.22f);
   var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,150,0);RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
   var go=SpellFx.CreateProjectileVisual(Skills.All["arcanewyrm"],"mage");var wyrm=go.GetComponent<ArcaneWyrmView>();
   for(int i=0;i<=120;i++)
   {
    float t=i/120f;go.transform.position=new Vector3(Mathf.Sin(t*2.1f)*2,1.3f,t*4);go.transform.rotation=Quaternion.LookRotation(new Vector3(4.2f*Mathf.Cos(t*2.1f),0,4));wyrm.Sample(1/120f);
    if(i==15||i==40||i==80||i==120)Shot(cam,go,"newfx_arcanewyrm_frame_"+i,new Vector3(5,3,6),new Vector3(1,1.2f,2),960,720);
   }
   if(wyrm.VisibleSections!=25)throw new Exception("Wyrm path failed to grow to 24 body segments");
   var before=wyrm.SectionWorld(12);var shift=new Vector3(8,0,-3);WorldFrame.SetMarker(new Pose(shift,Quaternion.identity));go.transform.position+=shift;wyrm.Sample(0);
   if(Vector3.Distance(wyrm.SectionWorld(12),before+shift)>.001f)throw new Exception("Wyrm AR shift discontinuity");
   WorldFrame.Reset();go.transform.position-=shift;wyrm.Sample(0);
   Shot(cam,go,"newfx_arcanewyrm_spectator",new Vector3(5,3,6),new Vector3(1,1.2f,2),960,720);
   Shot(cam,go,"newfx_arcanewyrm_firstperson",new Vector3(0,1.6f,-1),new Vector3(1,1.3f,3),720,960);
   wyrm.BeginFade();wyrm.Sample(.4f);Shot(cam,go,"newfx_arcanewyrm_fade",new Vector3(5,3,6),new Vector3(1,1.2f,2),960,720);
   UnityEngine.Object.DestroyImmediate(go);
   foreach(string id in new[]{"landmine","snaretrap","trapchain"})
   {
    var trap=SpellFx.CreateTrapVisual(Skills.All[id]);SpellFx.SetTrapArmed(trap,true);trap.GetComponent<TrapArtView>().Sample(.2f);
    Shot(cam,trap,"newfx_"+id+"_spectator",new Vector3(.8f,.8f,1),Vector3.zero,720,720);
    Shot(cam,trap,"newfx_"+id+"_firstperson",new Vector3(0,1.6f,-.8f),Vector3.zero,720,960);UnityEngine.Object.DestroyImmediate(trap);
   }
   File.WriteAllText("snapshots/newfx_audit.json","{\"wyrmSegments\":24,\"arShiftContinuous\":true}");
  }
 }
}
#endif
