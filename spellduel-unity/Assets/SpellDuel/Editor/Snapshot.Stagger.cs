#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  static readonly BindingFlags StaggerFlags=BindingFlags.Instance|BindingFlags.NonPublic;
  static void StaggerShots(Camera cam)
  {
   NewFxClean();int count=0;
   for(int kind=0;kind<=52;kind++)
   {
    var rig=MinionRig.Create();rig.SetVariant(kind);var sample=typeof(MinionRig).GetMethod("Animate",StaggerFlags);rig.Windup(1.5f);
    StaggerCase(cam,rig.gameObject,"minion_"+kind,dt=>sample.Invoke(rig,new object[]{dt}),rig.PlayStagger,rig.SetDead,false,kind<23||kind%5==3);count++;
   }
   foreach(int id in new[]{121,122,123,124,141,142,143,144,151,152,153,154,155,156,157,158,159,160,161,162,163,164,165})
   {var rig=VariantRig.Create(id);rig.Windup("basic",1.5f);StaggerCase(cam,rig.gameObject,"variant_"+id,rig.Sample,rig.PlayStagger,rig.SetDead,false,id<151);count++;}
   for(int kind=0;kind<5;kind++)
   {var rig=SkyEnemyRig.Create(kind);rig.Windup(1.5f);StaggerCase(cam,rig.gameObject,"sky_"+kind,rig.Sample,rig.PlayStagger,rig.SetDead,kind==4,true);count++;}
   File.WriteAllText("snapshots/newfx_stagger_audit.json","{\"models\":"+count+",\"rootUnchanged\":true,\"zeroDeltaNoAccumulation\":true,\"recovery\":true,\"deathCancels\":true,\"bossExcluded\":true,\"repeatCacheStable\":true}");
  }
  static void StaggerCase(Camera cam,GameObject go,string id,Action<float> sample,Action<float> play,Action<bool> dead,bool boss,bool sequence)
  {
   sample(.35f);var rootPos=go.transform.position;var rootRot=go.transform.rotation;
   if(sequence)CarpetFit(cam,go,"newfx_stagger_"+id+"_before",new Vector3(.4f,.15f,1),480,640);
   play(.9f);sample(.18f);if(sequence||!boss)CarpetFit(cam,go,"newfx_stagger_"+id+"_peak",new Vector3(.4f,.15f,1),480,640);
   var rig=go.GetComponent<MinionRig>() as MonoBehaviour;if(!rig)rig=go.GetComponent<VariantRig>();if(!rig)rig=go.GetComponent<SkyEnemyRig>();
   var field=rig.GetType().GetField("staggerArt",StaggerFlags);var art=field.GetValue(rig);if(boss){if(art!=null)throw new Exception("Boss staggered "+id);UnityEngine.Object.DestroyImmediate(go);return;}
   if(art==null)throw new Exception("Missing stagger "+id);
   var bones=go.GetComponentsInChildren<Transform>();var positions=new Vector3[bones.Length];var rotations=new Quaternion[bones.Length];for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
   for(int frame=0;frame<20;frame++)sample(0);
   for(int i=0;i<bones.Length;i++)if(Vector3.Distance(positions[i],bones[i].localPosition)>.0001f||Quaternion.Angle(rotations[i],bones[i].localRotation)>.05f)throw new Exception("Accumulating stagger "+id+" "+bones[i].name);
   sample(.8f);if((bool)art.GetType().GetProperty("Active").GetValue(art))throw new Exception("Stagger did not recover "+id);
   if(sequence)CarpetFit(cam,go,"newfx_stagger_"+id+"_recovered",new Vector3(.4f,.15f,1),480,640);
   int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,mats=Resources.FindObjectsOfTypeAll<Material>().Length;
   for(int i=0;i<4;i++){play(.9f);sample(.2f);sample(.8f);}if(meshes!=Resources.FindObjectsOfTypeAll<Mesh>().Length||mats!=Resources.FindObjectsOfTypeAll<Material>().Length)throw new Exception("Repeated stagger allocation "+id);
   play(.9f);sample(.1f);dead(true);sample(.1f);if((bool)art.GetType().GetProperty("Active").GetValue(art))throw new Exception("Death left stagger active "+id);
   if(go.transform.position!=rootPos||Quaternion.Angle(go.transform.rotation,rootRot)>.001f)throw new Exception("Stagger moved gameplay root "+id);UnityEngine.Object.DestroyImmediate(go);
  }
 }
}
#endif
