using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static class PlayerMatch2DAudit
    {
        [Serializable] public sealed class Row {public string id;public int triangles,renderers;public bool torsoMeshCached,gearVisible,crownPreserved,neckJoined;}
        [Serializable] public sealed class Report {public string unityVersion;public Row[] classes;public int failures;}
        public static void Run()
        {
            string[] ids={"novice","swordsman","archer","mage","assassin","priest","paladin","berserker","marksman","ranger","archmage","summoner","ninja","shadowdancer","bishop","inquisitor"};
            var rows=new List<Row>();var animate=typeof(CharacterRig).GetMethod("Animate",BindingFlags.Instance|BindingFlags.NonPublic);int failures=0;
            foreach(string id in ids)
            {
                string basic=Advanced.Get(id)?.baseCls??id;var a=CharacterRig.Create(basic,id==basic?null:id);var b=CharacterRig.Create(basic,id==basic?null:id);
                var row=new Row{id=id,crownPreserved=true};Mesh first=null,second=null;
                foreach(var f in a.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(f.sharedMesh==null)continue;row.triangles+=f.sharedMesh.triangles.Length/3;
                    if(f.gameObject.name=="Outfit torso")first=f.sharedMesh;
                    foreach(var v in f.sharedMesh.vertices)if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))throw new InvalidOperationException(id+" has non-finite vertices");
                }
                foreach(var f in b.GetComponentsInChildren<MeshFilter>(true))if(f.gameObject.name=="Outfit torso")second=f.sharedMesh;
                float jawBottom=float.PositiveInfinity,neckTop=float.NegativeInfinity;
                foreach(var f in a.GetComponentsInChildren<MeshFilter>())
                {
                    var material=f.GetComponent<Renderer>().sharedMaterial;
                    bool jaw=material!=null&&material.HasProperty("_HumanFace")&&material.GetFloat("_HumanFace")==1;
                    bool neck=f.gameObject.name=="Player default neck";
                    if(!jaw&&!neck)continue;
                    foreach(var vertex in f.sharedMesh.vertices){float y=f.transform.TransformPoint(vertex).y;if(jaw)jawBottom=Mathf.Min(jawBottom,y);if(neck)neckTop=Mathf.Max(neckTop,y);}
                }
                row.neckJoined=neckTop>=jawBottom+.003f;if(!row.neckJoined)failures++;
                row.torsoMeshCached=first!=null&&first==second;row.renderers=a.GetComponentsInChildren<Renderer>(true).Length;
                if(!row.torsoMeshCached||row.triangles>Snapshot.MatchTriangleBudget(id))failures++;
                a.SetPose(Vector3.zero,Vector3.forward);a.MoveSpeed=.8f;animate.Invoke(a,new object[]{.1f});a.Charge=.9f;a.PlayCast();animate.Invoke(a,new object[]{.1f});a.PlayHit();animate.Invoke(a,new object[]{.02f});a.SetVisible(false);a.SetVisible(true);a.SetDead(true);animate.Invoke(a,new object[]{.2f});a.SetDead(false);
                a.SetGear(GearLook.Parse("3:flame/3:flame/3:flame/3:flame/3:flame"));
                foreach(var r in a.GetComponentsInChildren<Renderer>())if(r.gameObject.name.StartsWith("Equipment ")&&r.enabled)row.gearVisible=true;
                if(!row.gearVisible)failures++;
                if(id=="bishop")
                {
                    bool found=false;foreach(var f in a.GetComponentsInChildren<MeshFilter>())if(f.gameObject.name=="Player default crown")found=true;
                    row.crownPreserved=found;if(!found)failures++;
                }
                a.SetGear(null);rows.Add(row);UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(b.gameObject);
            }
            Directory.CreateDirectory("snapshots");var report=new Report{unityVersion=Application.unityVersion,classes=rows.ToArray(),failures=failures};
            File.WriteAllText("snapshots/match_validation.json",JsonUtility.ToJson(report,true));
            if(failures>0)throw new InvalidOperationException("Player 2D match audit failures: "+failures);
            Debug.Log("[SpellDuel] 16 player rigs: mesh budget, cache, animation and equipment audit passed");
        }
    }
}
