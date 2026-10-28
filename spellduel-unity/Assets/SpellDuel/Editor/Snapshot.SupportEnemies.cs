#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  static readonly int[] SupportIds={121,122,123,124,141,142,143,144,151,152,153,154,155,156,157,158,159,160,161,162,163,164,165};
  [Serializable] class EnemyAuditEntry{public int id,triangles,renderers;}
  [Serializable] class EnemyAuditReport{public List<EnemyAuditEntry> entries=new List<EnemyAuditEntry>();public string checks="23 distinct rigs, finite geometry, <=6000 triangles, front/side/channel renders";}
  static void SupportEnemyShots(Camera cam)
  {
   var report=new EnemyAuditReport();var fronts=new List<string>();
   var build=typeof(VariantRig).GetMethod("BuildModel",BindingFlags.Instance|BindingFlags.NonPublic);
   var sample=typeof(VariantRig).GetMethod("AnimateModel",BindingFlags.Instance|BindingFlags.NonPublic);
   foreach(int id in SupportIds)
   {
    var rig=new GameObject("Enemy audit "+id).AddComponent<VariantRig>();build.Invoke(rig,new object[]{id});sample.Invoke(rig,new object[]{.1f});FiniteBudget(rig.gameObject,6000);
    int triangles=0;foreach(var f in rig.GetComponentsInChildren<MeshFilter>(true))triangles+=f.sharedMesh.triangles.Length/3;
    report.entries.Add(new EnemyAuditEntry{id=id,triangles=triangles,renderers=rig.GetComponentsInChildren<Renderer>(true).Length});
    CarpetFit(cam,rig.gameObject,"enemy_"+id+"_front",new Vector3(0,.1f,1),480,640);fronts.Add("enemy_"+id+"_front");
    CarpetFit(cam,rig.gameObject,"enemy_"+id+"_side",new Vector3(1,.1f,.03f),480,640);
    typeof(VariantRig).GetField("channel",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rig,true);
    sample.Invoke(rig,new object[]{.5f});CarpetFit(cam,rig.gameObject,"enemy_"+id+"_channel",new Vector3(.35f,.1f,1),480,640);
    UnityEngine.Object.DestroyImmediate(rig.gameObject);
   }
   ContactSheet(fronts,"support_enemies_all",6);
   File.WriteAllText("snapshots/support_enemies_audit.json",JsonUtility.ToJson(report,true));
   Debug.Log("[SpellDuel] Support enemy model audit passed");
  }
 }
}
#endif
