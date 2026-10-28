#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  public static void RenderSupportEnemies()
  {
   Directory.CreateDirectory("snapshots");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();
   var light=new GameObject("Enemy preview key").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,155,0);light.intensity=1.1f;
   RenderSettings.ambientLight=new Color(.55f,.58f,.66f);var cam=new GameObject("Enemy preview camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.78f,.81f,.86f);cam.fieldOfView=30;cam.nearClipPlane=.03f;
   SupportEnemyShots(cam);
   foreach(var message in ShaderUtil.GetShaderMessages(Shader.Find("SpellDuel/Toon")))if(message.severity.ToString()=="Error")throw new Exception(message.message);
  }
  static readonly int[] SupportIds={121,122,123,124,141,142,143,144,151,152,153,154,155,156,157,158,159,160,161,162,163,164,165};
  [Serializable] class EnemyAuditEntry{public int id,triangles,renderers,materials;}
  [Serializable] class EnemyAuditReport{public List<EnemyAuditEntry> entries=new List<EnemyAuditEntry>();public int validatedSkills=45;public string checks="23 rigs, finite geometry, <=6000 triangles, animation root stable, idle recovery, no update mesh allocation, stealth material reuse, support refresh/cleanup, 45 independent cue handles/cancellation/lifetime, seven projectile renders";}
  static void SkillEffectAudit(Camera cam)
  {
   string[] ids={"eh_121_sunmend","eh_121_sunward","eh_122_herbmend","eh_122_dustmantle","eh_123_twinseals","eh_123_sandseal","eh_124_sandlunge","eh_141_rainchorus","eh_141_rootlash","eh_142_pearlguard","eh_142_burstbubble","eh_143_marchdrum","eh_143_groundbeat","eh_144_royalstandard","eh_144_polecleave","eh_151_swordcut","eh_151_flankrush","eh_152_bolt","eh_152_triplebolt","eh_153_emberorb","eh_153_kilncircle","eh_154_dagger","eh_154_mistbackstab","eh_155_arrow","eh_155_retreatshot","eh_156_thornstaff","eh_156_seedmend","eh_157_sabercut","eh_157_decksweep","eh_158_waterlance","eh_158_tidalring","eh_159_dagger","eh_159_smokepierce","eh_160_swordthrust","eh_160_guardcounter","eh_161_javelin","eh_161_highcast","eh_162_staffjab","eh_162_sanctuarymend","eh_163_dagger","eh_163_sandtrailstrike","eh_164_sandbolt","eh_164_hourglass","eh_165_shortbow","eh_165_crossstep"};
   var known=new HashSet<string>(ids);foreach(var pair in EnemySkills.ByVariant)foreach(var def in pair.Value)if(!known.Contains(def.id))throw new Exception("Missing approved skill visual: "+def.id);
   foreach(var id in ids)
   {
    var def=EnemySkills.Find(id);float size=def!=null?def.size:.5f;
    int a=EnemySkillFx.Telegraph(id,new Vector3(.1f,0,0),Vector3.forward,1.5f,size);
    int b=EnemySkillFx.Telegraph(id,Vector3.left,Vector3.forward,1.5f,size);
    if(a==0||a==b)throw new Exception("Nonunique telegraph handle");EnemySkillFx.Cancel(a);EnemySkillFx.Cancel(a);
    if(UnityEngine.Object.FindObjectsOfType<EnemySkillView>().Length!=1)throw new Exception("Cancel affected another caster");
    foreach(var v in UnityEngine.Object.FindObjectsOfType<EnemySkillView>())v.Tick(.9f);
    if(id.EndsWith("groundbeat")||id.EndsWith("decksweep")||id.EndsWith("kilncircle")||id.EndsWith("mistbackstab"))
    {var subject=UnityEngine.Object.FindObjectOfType<EnemySkillView>();CarpetFit(cam,subject.gameObject,"enemy_cue_"+id,new Vector3(.3f,.9f,1),720,640);}
    EnemySkillFx.Cancel(b);EnemySkillFx.Cast(id,Vector3.zero,Vector3.forward*.5f,size);
    foreach(var v in UnityEngine.Object.FindObjectsOfType<EnemySkillView>())v.Tick(.15f);
    foreach(var v in UnityEngine.Object.FindObjectsOfType<EnemySkillView>())v.Tick(2);
    foreach(var v in UnityEngine.Object.FindObjectsOfType<EnemySupportView>())v.Tick(2);
    if(UnityEngine.Object.FindObjectsOfType<EnemySkillView>().Length!=0)throw new Exception("Skill effect lifetime leak");
   }
   foreach(var id in new[]{"eh_152_bolt","eh_155_arrow","eh_161_javelin","eh_153_emberorb","eh_158_waterlance","eh_142_burstbubble","eh_164_sandbolt"})
   {var shot=EnemyShotView.Create(id);FiniteBudget(shot.gameObject,1000);CarpetFit(cam,shot.gameObject,"enemy_shot_"+id,new Vector3(1,.4f,.4f),480,480);UnityEngine.Object.DestroyImmediate(shot.gameObject);}
  }
  static void SupportEffectAudit(Camera cam)
  {
   var target=VariantRig.Create(151);EnemySupportFx.Shield(target.transform,2);EnemySupportFx.Shield(target.transform,3);
   if(UnityEngine.Object.FindObjectsOfType<EnemySupportView>().Length!=1)throw new Exception("Duplicate shield on refresh");
   EnemySupportFx.Haste(target.transform,3);EnemySupportFx.RallyAura(target.transform,3);
   foreach(var fx in UnityEngine.Object.FindObjectsOfType<EnemySupportView>())fx.Tick(.4f);
   CarpetShot(cam,target.gameObject,"enemy_support_shield_haste",new Vector3(2,1.8f,3),new Vector3(0,.8f,0),800,800);
   EnemySupportFx.ShieldBreak(target.transform);
   EnemySupportFx.Heal(new Vector3(-.8f,1,0),new Vector3(.5f,.8f,0));EnemySupportFx.Rally(Vector3.zero,1.1f);EnemySupportFx.Summon(new Vector3(.7f,0,0));
   foreach(var fx in UnityEngine.Object.FindObjectsOfType<EnemySupportView>())fx.Tick(.15f);
   CarpetShot(cam,target.gameObject,"enemy_support_heal_rally_summon",new Vector3(2,2.2f,3),new Vector3(0,.6f,0),800,800);
   target.gameObject.SetActive(false);foreach(var fx in UnityEngine.Object.FindObjectsOfType<EnemySupportView>())fx.Tick(5);
   if(UnityEngine.Object.FindObjectsOfType<EnemySupportView>().Length!=0)throw new Exception("Support effect lifecycle leak");UnityEngine.Object.DestroyImmediate(target.gameObject);
  }
  static void SupportEnemyShots(Camera cam)
  {
   var report=new EnemyAuditReport();var fronts=new List<string>();
   var build=typeof(VariantRig).GetMethod("BuildModel",BindingFlags.Instance|BindingFlags.NonPublic);
   var sample=typeof(VariantRig).GetMethod("AnimateModel",BindingFlags.Instance|BindingFlags.NonPublic);
   foreach(int id in SupportIds)
   {
    var rig=VariantRig.Create(id);rig.Sample(.1f);FiniteBudget(rig.gameObject,6000);
    int triangles=0;foreach(var f in rig.GetComponentsInChildren<MeshFilter>(true))triangles+=f.sharedMesh.triangles.Length/3;
    var materialSet=new HashSet<Material>();foreach(var r in rig.GetComponentsInChildren<Renderer>(true))materialSet.Add(r.sharedMaterial);
    report.entries.Add(new EnemyAuditEntry{id=id,triangles=triangles,renderers=rig.GetComponentsInChildren<Renderer>(true).Length,materials=materialSet.Count});
    CarpetFit(cam,rig.gameObject,"enemy_"+id+"_front",new Vector3(0,.1f,1),480,640);fronts.Add("enemy_"+id+"_front");
    CarpetFit(cam,rig.gameObject,"enemy_"+id+"_side",new Vector3(1,.1f,.03f),480,640);
    rig.SetChannel(true);
    rig.Sample(.5f);CarpetFit(cam,rig.gameObject,"enemy_"+id+"_channel",new Vector3(.35f,.1f,1),480,640);
    int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length, materials=Resources.FindObjectsOfTypeAll<Material>().Length;
    rig.SetChannel(false);rig.MoveSpeed=2;for(int n=0;n<10;n++)rig.Sample(.05f);
    rig.MoveSpeed=0;rig.Sample(.5f);
    var shoulder=rig.transform.Find("Pelvis and spine/Right shoulder");var idleRotation=shoulder?shoulder.localRotation:Quaternion.identity;
    string skill=id==157?"eh_157_decksweep":id==144?"eh_144_polecleave":id==160?"eh_160_guardcounter":id==161?"eh_161_highcast":id==155?"eh_155_retreatshot":"basic";
    rig.Windup(skill,1.5f);rig.Sample(1.5f);CarpetFit(cam,rig.gameObject,"enemy_"+id+"_windup",new Vector3(.35f,.1f,1),480,640);
    rig.Strike(skill);rig.Sample(.2f);CarpetFit(cam,rig.gameObject,"enemy_"+id+"_strike",new Vector3(.35f,.1f,1),480,640);
    rig.Sample(1);if(shoulder&&Quaternion.Angle(idleRotation,shoulder.localRotation)>.1f)throw new Exception("Attack did not restore idle shoulder: "+id);
    rig.PlayHit();rig.Sample(.1f);
    rig.SetFleeing(true);rig.MoveSpeed=0;rig.Sample(.5f);CarpetFit(cam,rig.gameObject,"enemy_"+id+"_recover",new Vector3(.35f,.1f,1),480,640);
    rig.SetSneak(true);rig.Sample(.5f);rig.SetSneak(false);rig.Sample(.5f);rig.SetDead(true);rig.Sample(1);rig.SetDead(false);rig.Sample(0);
    if(rig.transform.position!=Vector3.zero)throw new Exception("Animation moved authoritative root");
    if(Resources.FindObjectsOfTypeAll<Mesh>().Length!=meshes)throw new Exception("Enemy per-frame mesh allocation");
    // First ghost material may be created lazily; a second cycle must allocate nothing.
    materials=Resources.FindObjectsOfTypeAll<Material>().Length;rig.SetSneak(true);rig.Sample(.5f);rig.SetSneak(false);rig.Sample(.5f);
    if(Resources.FindObjectsOfTypeAll<Material>().Length!=materials)throw new Exception("Enemy repeated stealth material allocation");
    UnityEngine.Object.DestroyImmediate(rig.gameObject);
   }
   ContactSheet(fronts,"support_enemies_all",6);
   SupportEffectAudit(cam);
   SkillEffectAudit(cam);
   File.WriteAllText("snapshots/support_enemies_audit.json",JsonUtility.ToJson(report,true));
   Debug.Log("[SpellDuel] Support enemy model audit passed");
  }
 }
}
#endif
