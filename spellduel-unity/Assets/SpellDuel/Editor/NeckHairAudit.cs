#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SpellDuel.EditorTools {
public static class NeckHairAudit {
[Serializable] public class Row {public string id;public int triangles,baselineTriangles;public float neckOverlap,collarClearance,hairScalpMin;public bool cache,neckTopInsideJaw;}
[Serializable] public class Report {public string unity;public Row[] classes;}
static bool Inside(Vector3 point,Mesh mesh,Matrix4x4 matrix){
 var dir=new Vector3(.811f,.123f,.571f).normalized;var v=mesh.vertices;var t=mesh.triangles;int crossings=0;
 for(int i=0;i<t.Length;i+=3){var a=matrix.MultiplyPoint3x4(v[t[i]]);var b=matrix.MultiplyPoint3x4(v[t[i+1]]);var c=matrix.MultiplyPoint3x4(v[t[i+2]]);var e=b-a;var f=c-a;var h=Vector3.Cross(dir,f);float det=Vector3.Dot(e,h);if(Mathf.Abs(det)<1e-8f)continue;float inv=1/det;var s=point-a;float u=inv*Vector3.Dot(s,h);if(u<0||u>1)continue;var q=Vector3.Cross(s,e);float w=inv*Vector3.Dot(dir,q);if(w<0||u+w>1)continue;if(inv*Vector3.Dot(f,q)>1e-6f)crossings++;}return crossings%2==1;
}
static float Distance(Vector3 p,Vector3 a,Vector3 b,Vector3 c){
 // Closest point on the triangle's interior or its three clamped edges.
 var n=Vector3.Cross(b-a,c-a);float sq=n.sqrMagnitude;float best=100;
 if(sq>1e-12f){var q=p-n*(Vector3.Dot(p-a,n)/sq);if(Vector3.Dot(Vector3.Cross(b-a,q-a),n)>=0&&Vector3.Dot(Vector3.Cross(c-b,q-b),n)>=0&&Vector3.Dot(Vector3.Cross(a-c,q-c),n)>=0)best=(p-q).magnitude;}
 var vs=new[]{a,b,c};for(int i=0;i<3;i++){var d=vs[(i+1)%3]-vs[i];var q=vs[i]+d*Mathf.Clamp01(Vector3.Dot(p-vs[i],d)/Mathf.Max(1e-12f,d.sqrMagnitude));best=Mathf.Min(best,(p-q).magnitude);}return best;
}
public static Report Run(){
 var rows=new List<Row>();string[] ids={"novice","swordsman","archer","mage","assassin","priest","paladin","berserker","marksman","ranger","archmage","summoner","ninja","shadowdancer","bishop","inquisitor"};
 int[] baselines={12180,12644,13276,11672,11828,12900,12840,11522,14048,13608,12544,11712,12248,11456,13144,12010};
 for(int index=0;index<ids.Length;index++){
  string id=ids[index],basic=Advanced.Get(id)?.baseCls??id;var rig=CharacterRig.Create(basic,id==basic?null:id);var twin=CharacterRig.Create(basic,id==basic?null:id);MeshFilter jaw=null,neck=null,collar=null;var row=new Row{id=id,baselineTriangles=baselines[index],neckTopInsideJaw=true,collarClearance=100,hairScalpMin=100,cache=true};
  foreach(var f in rig.GetComponentsInChildren<MeshFilter>(true)){row.triangles+=f.sharedMesh.triangles.Length/3;if(f.gameObject.name=="Player default neck")neck=f;if(f.gameObject.name=="Separate garment collar")collar=f;if(f.GetComponent<Renderer>().sharedMaterial.HasProperty("_HumanFace")&&f.GetComponent<Renderer>().sharedMaterial.GetFloat("_HumanFace")==1)jaw=f;}
  if(!jaw||!neck||!collar)throw new Exception("Missing fitted parts "+id);
  var jm=rig.transform.worldToLocalMatrix*jaw.transform.localToWorldMatrix;var nm=rig.transform.worldToLocalMatrix*neck.transform.localToWorldMatrix;var cm=rig.transform.worldToLocalMatrix*collar.transform.localToWorldMatrix;
  var nv=neck.sharedMesh.vertices;var nt=neck.sharedMesh.triangles;float low=10,top=-10;foreach(var v in jaw.sharedMesh.vertices)low=Mathf.Min(low,jm.MultiplyPoint3x4(v).y);foreach(var v in nv)top=Mathf.Max(top,nm.MultiplyPoint3x4(v).y);row.neckOverlap=top-low;
  for(int i=0;i<16;i++)row.neckTopInsideJaw&=Inside(nm.MultiplyPoint3x4(nv[i]),jaw.sharedMesh,jm);
  var cv=collar.sharedMesh.vertices;for(int i=50;i<75;i++){var p=cm.MultiplyPoint3x4(cv[i]);if(Inside(p,neck.sharedMesh,nm))throw new Exception("Collar intersects skin "+id);for(int k=0;k<nt.Length;k+=3)row.collarClearance=Mathf.Min(row.collarClearance,Distance(p,nm.MultiplyPoint3x4(nv[nt[k]]),nm.MultiplyPoint3x4(nv[nt[k+1]]),nm.MultiplyPoint3x4(nv[nt[k+2]])));}
  foreach(var v in PlayerHairVertices(rig))row.hairScalpMin=Mathf.Min(row.hairScalpMin,v);
  foreach(var f in twin.GetComponentsInChildren<MeshFilter>())if(f.gameObject.name=="Player default neck")row.cache&=f.sharedMesh==neck.sharedMesh;else if(f.gameObject.name=="Separate garment collar")row.cache&=f.sharedMesh==collar.sharedMesh;
  if(row.triangles>row.baselineTriangles*1.3f||row.neckOverlap<.010f||!row.neckTopInsideJaw||row.collarClearance<.003f||row.hairScalpMin<1.005f||!row.cache)throw new Exception("Neck/hair audit failed "+JsonUtility.ToJson(row));
  rows.Add(row);UnityEngine.Object.DestroyImmediate(rig.gameObject);UnityEngine.Object.DestroyImmediate(twin.gameObject);
 }
 var report=new Report{unity=Application.unityVersion,classes=rows.ToArray()};Directory.CreateDirectory("snapshots");File.WriteAllText("snapshots/neck_hair_audit.json",JsonUtility.ToJson(report,true));Debug.Log("[SpellDuel] Neck/hair audit passed: 16 rigs, jaw containment, collar clearance, scalp and 130% budgets.");return report;
}
// Query the same cached procedural source, not an editor-only approximation.
static IEnumerable<float> PlayerHairVertices(CharacterRig rig){
 var type=typeof(CharacterRig).Assembly.GetType("SpellDuel.PlayerHair");var get=type.GetMethod("MeshFor",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var persona=typeof(Portraits).GetMethod("PersonaFor",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{rig.AdvancedId??rig.ClassId});var mesh=(Mesh)get.Invoke(null,new[]{persona});var distance=type.GetMethod("ScalpDistance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);foreach(var v in mesh.vertices)yield return (float)distance.Invoke(null,new object[]{v});
}
}
}
#endif
