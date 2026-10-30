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
   var cam=new GameObject("FX preview").AddComponent<Camera>();cam.tag="MainCamera";cam.clearFlags=CameraClearFlags.SolidColor;cam.nearClipPlane=.05f;cam.fieldOfView=55;cam.backgroundColor=new Color(.12f,.16f,.22f);
   var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,150,0);RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
   var go=SpellFx.CreateProjectileVisual(Skills.All["arcanewyrm"],"mage");var wyrm=go.GetComponent<ArcaneWyrmView>();
   for(int i=0;i<=120;i++)
   {
    float t=i/120f;go.transform.position=new Vector3(Mathf.Sin(t*2.1f)*2,1.3f,t*4);go.transform.rotation=Quaternion.LookRotation(new Vector3(4.2f*Mathf.Cos(t*2.1f),0,4));wyrm.Sample(1/120f);
    if(i==15||i==40||i==80||i==120)CarpetShot(cam,go,"newfx_arcanewyrm_frame_"+i,new Vector3(5,3,6),new Vector3(1,1.2f,2),960,720);
   }
   if(wyrm.VisibleSections!=25)throw new Exception("Wyrm path failed to grow to 24 body segments");
   var before=wyrm.SectionWorld(12);var shift=new Vector3(8,0,-3);WorldFrame.SetMarker(new Pose(shift,Quaternion.identity));go.transform.position+=shift;wyrm.Sample(0);
   if(Vector3.Distance(wyrm.SectionWorld(12),before+shift)>.001f)throw new Exception("Wyrm AR shift discontinuity");
   WorldFrame.Reset();go.transform.position-=shift;wyrm.Sample(0);
   CarpetShot(cam,go,"newfx_arcanewyrm_spectator",new Vector3(5,3,6),new Vector3(1,1.2f,2),960,720);
   CarpetShot(cam,go,"newfx_arcanewyrm_firstperson",new Vector3(0,1.6f,-1),new Vector3(1,1.3f,3),720,960);
   CarpetShot(cam,go,"newfx_arcanewyrm_head",go.transform.position+new Vector3(.6f,.3f,.8f),go.transform.position,720,720);
   wyrm.BeginFade();wyrm.Sample(.4f);CarpetShot(cam,go,"newfx_arcanewyrm_fade",new Vector3(5,3,6),new Vector3(1,1.2f,2),960,720);
   UnityEngine.Object.DestroyImmediate(go);
   foreach(string id in new[]{"landmine","snaretrap","trapchain"})
   {
    var trap=SpellFx.CreateTrapVisual(Skills.All[id]);SpellFx.SetTrapArmed(trap,true);trap.GetComponent<TrapArtView>().Sample(.2f);
    CarpetShot(cam,trap,"newfx_"+id+"_spectator",new Vector3(.8f,.8f,1),Vector3.zero,720,720);
    CarpetShot(cam,trap,"newfx_"+id+"_firstperson",new Vector3(0,1.6f,-.8f),Vector3.zero,720,960);UnityEngine.Object.DestroyImmediate(trap);
   }
   SpellFx.FalconDive(new Vector3(0,2,0),new Vector3(0,1,3),.35f);var falcon=UnityEngine.Object.FindObjectOfType<FalconDiveArt>();falcon.Sample(.2f);
   CarpetShot(cam,falcon.gameObject,"newfx_falcondive_spectator",new Vector3(2,2,3),falcon.transform.position,720,720);
   CarpetShot(cam,falcon.gameObject,"newfx_falcondive_firstperson",new Vector3(0,1.6f,-.5f),new Vector3(0,1.4f,3),720,960);
   falcon.Sample(.15f);if(Vector3.Distance(falcon.transform.position,new Vector3(0,1,3))>.001f)throw new Exception("Falcon contact timing drift");UnityEngine.Object.DestroyImmediate(falcon.gameObject);
   var slowGo=SpellFx.CreateProjectileVisual(Skills.All["arcanewyrm"],"mage");var slow=slowGo.GetComponent<ArcaneWyrmView>();slow.Sample(0);int meshCount=Resources.FindObjectsOfTypeAll<Mesh>().Length,materialCount=Resources.FindObjectsOfTypeAll<Material>().Length;
   for(int i=1;i<=1000;i++){slowGo.transform.position=Vector3.forward*(i*.003f);slow.Sample(.001f);}if(slow.VisibleSections!=25)throw new Exception("Dense wyrm history lost body length");
   if(meshCount!=Resources.FindObjectsOfTypeAll<Mesh>().Length||materialCount!=Resources.FindObjectsOfTypeAll<Material>().Length)throw new Exception("Wyrm per-frame mesh or material allocation");UnityEngine.Object.DestroyImmediate(slowGo);
   NewFxCatalogShots(cam);
   File.WriteAllText("snapshots/newfx_audit.json","{\"wyrmSegments\":24,\"arShiftContinuous\":true,\"denseHistoryLength\":true,\"wyrmCacheStable\":true,\"falconContactTime\":0.35,\"iconsUnique\":51,\"independentAuraClear\":true,\"auraCacheStable\":true}");
  }
  static void NewFxSample(float dt)
  {
   foreach(var b in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
   {
    if(b is CharacterRig)continue;if(b is HitStackArt stack)stack.Sample();
    var type=b.GetType();var method=type.GetMethod("Sample",new[]{typeof(float)});if(method!=null)method.Invoke(b,new object[]{dt});
    if(type.Name=="FxAnim"){type.GetField("time",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(b,dt);type.GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(b,null);}
   }
   foreach(var ps in UnityEngine.Object.FindObjectsOfType<ParticleSystem>()){ps.useAutoRandomSeed=false;ps.randomSeed=78;ps.Simulate(dt,true,true);}
  }
  static void NewFxClean()
  {
   foreach(var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())if(!go.GetComponent<Camera>()&&!go.GetComponent<Light>())UnityEngine.Object.DestroyImmediate(go);
  }
  static void NewFxPair(Camera cam,string id,GameObject subject,Vector3 center,bool close=false)
  {
   cam.aspect=960f/720;var spectator=close?center+new Vector3(.8f,.4f,1.1f):center+((id.Contains("area")||id=="mineblast"||id=="starfall_zone")?new Vector3(4.5f,3.5f,5.5f):new Vector3(2,1.4f,3));cam.transform.position=spectator;NewFxSample(.12f);CarpetShot(cam,subject,"newfx_"+id+"_spectator",spectator,center,960,720);
   cam.aspect=720f/960;cam.transform.position=new Vector3(0,1.6f,-.1f);cam.transform.LookAt(center+Vector3.forward*2);if(subject.GetComponent<CharacterRig>())foreach(var r in subject.GetComponentsInChildren<MeshRenderer>())r.enabled=false;NewFxSample(0);CarpetShot(cam,subject,"newfx_"+id+"_firstperson",new Vector3(0,1.6f,-.1f),center+Vector3.forward*2,720,960);
  }
  static void NewFxCatalogShots(Camera cam)
  {
   NewFxClean();var hashes=new System.Collections.Generic.HashSet<string>();
   foreach(string id in SkillIcons.NewSkillArtIds)
   {
    var pixels=SkillIcons.Render(id,128);var t=new Texture2D(128,128,TextureFormat.RGBA32,false);t.SetPixels32(pixels);t.Apply();var data=t.EncodeToPNG();var hash=Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(data));if(!hashes.Add(hash))throw new Exception("Duplicate new skill icon "+id);File.WriteAllBytes("snapshots/newfx_icon_"+id+".png",data);UnityEngine.Object.DestroyImmediate(t);if(SkillIcons.Get(id)!=SkillIcons.Get(id))throw new Exception("Icon cache "+id);
   }
   foreach(string id in new[]{"starfall","arcanebreak","holyblade","headshot","scattershot","blastarrow","spiritbolt","earthspike","suiton","futon","poisondart","lightpillar","holyfire","verdict","rollarrow","echo_fire","echo_ice"})
   {var go=SpellFx.CreateProjectileVisual(Skills.All[id],Skills.All[id].cls);go.transform.position=new Vector3(0,1.2f,2);NewFxPair(cam,id,go,new Vector3(0,1.2f,2),true);NewFxClean();}
   foreach(string id in new[]{"consecrate","blastarrow","earthspike","suiton","bladestorm","holynova","arcaneburst","venom_burst","starfall"})
   {SpellFx.AreaBurst(id,new Vector3(0,0,2),id=="starfall"?3.5f:2);var go=UnityEngine.Object.FindObjectOfType<AreaSkillArt>();NewFxPair(cam,id+"_area",go.gameObject,new Vector3(0,.25f,2));NewFxClean();}
   foreach(string id in new[]{"arcanehaste","elementward","rangerhaste","hawkeye","layhands","pactcheer","nightveil","divinegrace","mindfocus","meditate","bless","haste","spring","focus","retribution","swordfervor","killinstinct","huntfrenzy","chainstorm","soulharvest","renjutsu","reaperdance","graceflow","martyrflame"})
   {
    var rig=CharacterRig.Create("novice");rig.InCombat=false;SpellFx.TeamBuffAura(rig.transform,id,1);NewFxPair(cam,id,rig.gameObject,new Vector3(0,.9f,0));
    int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,mats=Resources.FindObjectsOfTypeAll<Material>().Length;for(int i=0;i<60;i++)SpellFx.TeamBuffAura(rig.transform,id,.7f);if(meshes!=Resources.FindObjectsOfTypeAll<Mesh>().Length||mats!=Resources.FindObjectsOfTypeAll<Material>().Length)throw new Exception("Aura frame allocation "+id);
    SpellFx.TeamBuffAura(rig.transform,id,0);var view=rig.GetComponentInChildren<TeamBuffArt>();if(view)throw new Exception("Aura did not clear "+id);NewFxClean();
   }
   var owner=CharacterRig.Create("novice");SpellFx.TeamBuffAura(owner.transform,"arcanehaste",1);SpellFx.TeamBuffAura(owner.transform,"elementward",1);SpellFx.TeamBuffAura(owner.transform,"arcanehaste",0);if(owner.GetComponentsInChildren<TeamBuffArt>().Length!=1)throw new Exception("Independent aura state");NewFxClean();
   foreach(string id in new[]{"mineblast","starfall_zone","arcanebreak_hit","chainarc","thorns","execute","firststrike","souldrain","killrush","stackaura","taunt","prep"})
   {
    var rig=CharacterRig.Create("novice");var at=new Vector3(0,.9f,2);
    switch(id)
    {
     case "mineblast":SpellFx.MineBlast(new Vector3(0,0,2),2.5f,Color.red);break;
     case "starfall_zone":SpellFx.StarfallZone(new Vector3(0,0,2),3.5f,2);break;
     case "arcanebreak_hit":SpellFx.ArcaneBreakHit(at);break;
     case "chainarc":SpellFx.ChainArc(new Vector3(-.8f,.9f,2),new Vector3(.8f,1.1f,2),Color.cyan);break;
     case "thorns":SpellFx.Thorns(rig.transform,at);break;
     case "execute":SpellFx.ExecuteHit(at);break;
     case "firststrike":SpellFx.FirstStrikeHit(at);break;
     case "souldrain":SpellFx.SoulDrain(at,rig.transform);break;
     case "killrush":SpellFx.KillRush(rig.transform);break;
     case "stackaura":SpellFx.StackAura(rig.transform,7);break;
     case "taunt":SpellFx.CastBurst("sword_taunt",Skills.All["taunt"].color,rig.transform.position,Vector3.forward);break;
     case "prep":SpellFx.CastBurst("assassin",Skills.All["prep"].color,rig.transform.position,Vector3.forward);break;
    }
    NewFxPair(cam,id,rig.gameObject,at);NewFxClean();
   }
  }

 }
}
#endif
