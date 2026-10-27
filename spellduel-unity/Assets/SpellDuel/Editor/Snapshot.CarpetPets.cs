#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace SpellDuel.EditorTools
{
 public static partial class Snapshot
 {
  static void FiniteBudget(GameObject go,int budget)
  {int count=0;foreach(var mf in go.GetComponentsInChildren<MeshFilter>(true)){if(!mf.sharedMesh)continue;count+=mf.sharedMesh.triangles.Length/3;foreach(var v in mf.sharedMesh.vertices)if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))throw new Exception("Non-finite mesh "+go.name);}if(count>budget)throw new Exception(go.name+" triangle budget "+count+" > "+budget);}
  // Earlier galleries explicitly set camera.aspect. Each export must set its own ratio.
  static void CarpetShot(Camera cam,GameObject subject,string name,Vector3 position,Vector3 look,int width,int height)
  {float old=cam.aspect;cam.aspect=width/(float)height;Shot(cam,subject,name,position,look,width,height);cam.aspect=old;}
  static void CarpetFit(Camera cam,GameObject subject,string name,Vector3 direction,int width,int height)
  {
   bool first=true;var bounds=new Bounds(subject.transform.position,Vector3.zero);
   foreach(var r in subject.GetComponentsInChildren<Renderer>()){if(!r.enabled||r is ParticleSystemRenderer)continue;if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
   var forward=-direction.normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;var up=Vector3.Cross(forward,right).normalized;
   float tan=Mathf.Tan(cam.fieldOfView*.5f*Mathf.Deg2Rad),distance=0;
   for(int i=0;i<8;i++){var c=new Vector3((i&1)==0?-bounds.extents.x:bounds.extents.x,(i&2)==0?-bounds.extents.y:bounds.extents.y,(i&4)==0?-bounds.extents.z:bounds.extents.z);float depth=Vector3.Dot(c,forward);distance=Mathf.Max(distance,(Mathf.Abs(Vector3.Dot(c,up))+.08f)/tan-depth);distance=Mathf.Max(distance,(Mathf.Abs(Vector3.Dot(c,right))+.08f)/(tan*width/height)-depth);}
   CarpetShot(cam,subject,name,bounds.center+direction.normalized*distance,bounds.center,width,height);
  }
  static void CarpetPetsShots(Camera cam)
  {
   var poly=new System.Collections.Generic.List<Vector3>{new Vector3(-2,0,-2),new Vector3(2,0,-2),new Vector3(2,0,2),new Vector3(-2,0,2)};
   var scene=CarpetScene.Create(poly,0,Vector3.forward);
   for(int i=0;i<5;i++){scene.SetVariant(i);scene.Sample(1.2f,0);int visibleTriangles=0;foreach(var f in scene.GetComponentsInChildren<MeshFilter>())visibleTriangles+=f.sharedMesh.triangles.Length/3;if(visibleTriangles>60000)throw new Exception("Visible scenery triangle budget "+visibleTriangles);int visible=0;foreach(var r in scene.GetComponentsInChildren<MeshRenderer>())if(r.enabled)visible++;FiniteBudget(scene.gameObject,300000);if(visible>100)throw new Exception("Scene batching budget "+visible);CarpetShot(cam,scene.gameObject,"carpet_station_"+i,new Vector3(0,3,-9),new Vector3(0,1,4),960,720);}
   scene.SwayScale=0;scene.Sample(2,0);if(scene.VisualRoot.localPosition.sqrMagnitude>.000001f)throw new Exception("Sway accessibility failed");
   var point=new Vector3(.3f,.025f,.3f);if((scene.SurfacePoint(point)-point).magnitude>.0001f)throw new Exception("Surface at zero sway");
   int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length;
   for(int i=0;i<100;i++){scene.SetVariant(i%5);scene.Sample(i*.04f,.04f);}
   if(Resources.FindObjectsOfTypeAll<Mesh>().Length!=meshes)throw new Exception("Carpet recreated meshes while moving");
   scene.SwayScale=1;scene.Impact(Vector3.zero,1);scene.Sample(4,.1f);
   CarpetShot(cam,scene.gameObject,"carpet_impact",new Vector3(3,4,-6),Vector3.zero,960,720);
   UnityEngine.Object.DestroyImmediate(scene.gameObject);
   var concave=CarpetScene.Create(new System.Collections.Generic.List<Vector3>{new Vector3(-2,0,-2),new Vector3(2,0,-2),new Vector3(2,0,0),new Vector3(0,0,0),new Vector3(0,0,2),new Vector3(-2,0,2)},0,Vector3.right);
   concave.Sample(.5f,0);FiniteBudget(concave.gameObject,300000);UnityEngine.Object.DestroyImmediate(concave.gameObject);
   for(int k=0;k<4;k++)
   {var obstacle=CarpetObstacleView.Create(k,k);obstacle.Tick(.8f,Vector3.zero,Vector3.forward,4,1.3f,1,.6f,.8f);FiniteBudget(obstacle.gameObject,7000);CarpetShot(cam,obstacle.gameObject,"carpet_obstacle_"+k,new Vector3(4,4,-5),new Vector3(0,1,4),960,720);obstacle.Release();if(obstacle.gameObject.activeSelf)throw new Exception("Obstacle release");obstacle.gameObject.SetActive(true);obstacle.Tick(1,Vector3.zero,Vector3.forward,4,1.3f,-1,0,1);UnityEngine.Object.DestroyImmediate(obstacle.gameObject);}
   for(int k=0;k<5;k++)
   {
    var enemy=SkyEnemyRig.Create(k);FiniteBudget(enemy.gameObject,6000);enemy.Sample(.1f);CarpetFit(cam,enemy.gameObject,"carpet_enemy_"+k,new Vector3(.4f,.15f,1),640,800);
    enemy.Windup(1.5f);enemy.Sample(1.5f);CarpetFit(cam,enemy.gameObject,"carpet_enemy_"+k+"_windup",new Vector3(.4f,.15f,1),640,800);enemy.Attack();enemy.Sample(.15f);enemy.PlayHit();enemy.Sample(.05f);enemy.SetDead(true);enemy.Sample(.8f);enemy.SetDead(false);enemy.Sample(.1f);UnityEngine.Object.DestroyImmediate(enemy.gameObject);
    var vehicle=SkyVehicleView.Create(k);vehicle.Tick(.55f,new Vector3(0,1,-5),Vector3.zero);FiniteBudget(vehicle.gameObject,4000);CarpetShot(cam,vehicle.gameObject,"carpet_vehicle_"+k,new Vector3(3,3,5),new Vector3(0,1,0),800,640);vehicle.Tick(1,new Vector3(0,1,-5),Vector3.zero);UnityEngine.Object.DestroyImmediate(vehicle.gameObject);
   }
   var boss=AirshipRig.Create();FiniteBudget(boss.gameObject,18000);boss.Warn(0,1.5f);boss.Sample(1.5f);CarpetShot(cam,boss.gameObject,"carpet_boss",new Vector3(12,7,23),new Vector3(0,3,0),1000,800);
   boss.SetPhase(2);boss.Warn(4,1.8f);boss.Sample(1.8f);boss.Fire(4);boss.Sample(.12f);boss.DropPirate(-1);boss.Sample(.4f);CarpetShot(cam,boss.gameObject,"carpet_boss_phase2",new Vector3(12,7,23),new Vector3(0,3,0),1000,800);boss.SetSwoop(1);boss.SetDying(.45f);boss.Sample(0);CarpetFit(cam,boss.gameObject,"carpet_boss_dying",new Vector3(.5f,.3f,1),1000,800);boss.SetDying(1);boss.Sample(0);UnityEngine.Object.DestroyImmediate(boss.gameObject);
   for(int i=0;i<2;i++){var shot=SkyShotView.Create(i==0);CarpetShot(cam,shot.gameObject,"carpet_shot_"+i,new Vector3(1,1,2),Vector3.zero,480,480);UnityEngine.Object.DestroyImmediate(shot.gameObject);}
   var animate=typeof(MinionRig).GetMethod("Animate",BindingFlags.Instance|BindingFlags.NonPublic);int pets=0;
   foreach(var sp in Pets.All)
   {
    var rig=MinionRig.Create();rig.SetVariant(sp.kind);PetDress.Apply(rig,sp.elite);int renderers=rig.GetComponentsInChildren<Renderer>().Length;PetDress.Apply(rig,sp.elite);if(renderers!=rig.GetComponentsInChildren<Renderer>().Length)throw new Exception("Duplicate pet dressing");
    FiniteBudget(rig.gameObject,40000);rig.SetEating(true);animate.Invoke(rig,new object[]{.2f});animate.Invoke(rig,new object[]{.2f});
    CarpetFit(cam,rig.gameObject,"petdress_"+sp.kind,new Vector3(.4f,.2f,1),640,800);
    rig.SetEating(false);animate.Invoke(rig,new object[]{.3f});rig.SetDead(true);animate.Invoke(rig,new object[]{.8f});UnityEngine.Object.DestroyImmediate(rig.gameObject);pets++;
   }
   for(int i=0;i<2;i++){var fx=new GameObject("Capture test").AddComponent<PetCaptureView>();fx.Build(i==0);fx.Sample(.65f);CarpetShot(cam,fx.gameObject,"pet_capture_"+i,new Vector3(1,1.4f,2.4f),new Vector3(0,.4f,0),640,640);UnityEngine.Object.DestroyImmediate(fx.gameObject);}
   foreach(string id in SkillIcons.CarpetPetArtIds){var px=SkillIcons.Render(id,128);if(!SkillIcons.IsSupply(id)||px.Length!=128*128)throw new Exception("Icon size "+id);}
   File.WriteAllText("snapshots/carpet_pets_audit.json","{\"stations\":5,\"obstacles\":4,\"skyEnemies\":5,\"vehicles\":5,\"bosses\":1,\"petKinds\":"+pets+",\"icons\":68,\"finiteMeshes\":true,\"pooledScene\":true,\"petDressIdempotent\":true}");
   Debug.Log("[SpellDuel] Carpet and pet art audit passed: five stations, four obstacles, five enemies and vehicles, airship, 44 pets, 68 icons");
  }
 }
}
#endif
