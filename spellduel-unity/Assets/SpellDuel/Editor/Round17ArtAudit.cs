#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace SpellDuel.Editor
{
    // Run in Play Mode: TownArt intentionally uses deferred Destroy for static batching.
    public static class Round17ArtAudit
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        public static readonly string[] Residents={"鐵匠","小鐵","店員","米菈","公會長","老焰","翠","小葉","楓","海斗","凪","阿潮","雷歐","小沙","鐵娘","老駝","阿鈴","石先生","嚮導","教官","懸賞官","播報員","艾琳"};
        [Serializable] public sealed class Row{public string id,bodyTemplate;public int actions,samples,ankles,triangles;public float minFootY=100,maxContactDrift;public bool skinAssetsStable=true,heldPropsStable=true,stationStable=true;public int heldProps;}
        [Serializable] public sealed class Report{public string method="Actual resident animator, two-bone skinning, 60 Hz idle/walk/turn/stop/wave; world-space support ankle drift";public Row[] rows;public int failures;}
        [MenuItem("SpellDuel/Art/Validate Round 17 residents (Play Mode)")]
        public static void Menu()
        {
            if(!Application.isPlaying){Debug.Log("Enter Play Mode before running the resident audit.");return;}
            var report=Run();string path=Path.Combine(Application.dataPath,"../Docs/round17_preview/unity_resident_audit.json");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));Debug.Log("Resident audit failures: "+report.failures+". "+path);AssetDatabase.Refresh();
        }
        sealed class Foot
        {
            internal SkinnedMeshRenderer renderer;internal Mesh mesh;internal Material material;internal Vector3[] vertices;internal BoneWeight[] weights;internal Matrix4x4[] bind;
            internal Foot(SkinnedMeshRenderer r){renderer=r;mesh=r.sharedMesh;material=r.sharedMaterial;vertices=mesh.vertices;weights=mesh.boneWeights;bind=mesh.bindposes;}
            internal float Lowest(float floor)
            {
                var m0=renderer.bones[0].localToWorldMatrix*bind[0];var m1=renderer.bones[1].localToWorldMatrix*bind[1];float low=100;
                for(int i=0;i<vertices.Length;i++){var w=weights[i];Vector3 v=m0.MultiplyPoint3x4(vertices[i])*w.weight0+m1.MultiplyPoint3x4(vertices[i])*w.weight1;low=Mathf.Min(low,v.y-floor);}return low;
            }
        }
        static object Field(object target,string name)=>target.GetType().GetField(name,Flags).GetValue(target);
        static float Drift(object motion,string side)
        {
            var leg=Field(motion,side);if((bool)Field(leg,"swinging")||(float)Field(leg,"pivot")>=0)return 0;
            var ankle=(Transform)Field(leg,"ankle");var anchor=(Vector3)Field(leg,"anchor");return new Vector2(ankle.position.x-anchor.x,ankle.position.z-anchor.z).magnitude;
        }
        public static Report Run()
        {
            var rows=new List<Row>();int failures=0;var parent=new GameObject("Temporary round 17 audit");parent.transform.localRotation=Quaternion.Euler(0,37,0);
            try{foreach(string id in Residents)
            {
                var actor=TownArt.CreateNpc(id,parent.transform);var components=actor.GetComponentsInChildren<Component>().Where(c=>c.GetType().Name=="HumanResidentMotion").ToArray();if(components.Length!=1)throw new InvalidOperationException(id+" must have exactly one resident animator");
                object motion=components[0];var type=motion.GetType();var tick=type.GetMethod("Tick",Flags);var speed=type.GetField("walkSpeed",Flags);var wave=type.GetMethod("Wave",Flags);
                var feet=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.gameObject.name.StartsWith("Outfit walking")).Select(r=>new Foot(r)).ToArray();if(feet.Length!=2)throw new InvalidOperationException(id+" missing ankle skin");
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();var meshes=skins.Select(r=>r.sharedMesh).ToArray();var materials=skins.Select(r=>r.sharedMaterial).ToArray();
                var row=new Row{id=id,bodyTemplate=(string)type.GetProperty("BodyIdentity",Flags).GetValue(motion),actions=(int)type.GetProperty("IdleActionCount",Flags).GetValue(motion),ankles=feet.Length,triangles=actor.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.gameObject.activeSelf).Sum(f=>f.sharedMesh.triangles.Length/3)+skins.Sum(r=>r.sharedMesh.triangles.Length/3)};
                var grips=new List<Transform>();foreach(var h in actor.GetComponentsInChildren<Transform>().Where(x=>x.gameObject.name=="Outfit hand"))for(int j=0;j<h.childCount;j++){var prop=h.GetChild(j);if(!prop.gameObject.name.StartsWith("Outfit "))grips.Add(prop);}
                var gripParents=grips.Select(g=>g.parent).ToArray();var gripPositions=grips.Select(g=>g.localPosition).ToArray();row.heldProps=grips.Count;
                Transform station=(Transform)Field(motion,"station");Vector3 stationWorld=station==null?Vector3.zero:station.position;
                float t=0;
                void Sample(bool checkDrift)
                {
                    tick.Invoke(motion,new object[]{t});foreach(var foot in feet)row.minFootY=Mathf.Min(row.minFootY,foot.Lowest(actor.transform.position.y));
                    if(checkDrift)row.maxContactDrift=Mathf.Max(row.maxContactDrift,Mathf.Max(Drift(motion,"legL"),Drift(motion,"legR")));
                    for(int j=0;j<grips.Count;j++)row.heldPropsStable&=grips[j].parent==gripParents[j]&&(grips[j].localPosition-gripPositions[j]).magnitude<.0001f;
                    if(station!=null)row.stationStable&=(station.position-stationWorld).magnitude<.0001f;
                    for(int k=0;k<skins.Length;k++)row.skinAssetsStable&=skins[k].sharedMesh==meshes[k]&&skins[k].sharedMaterial==materials[k];row.samples++;
                }
                // Whole idle cycles include every role action and the child's lifted dance.
                for(int f=0;f<row.actions*11*30;f++){t+=1/30f;Sample(false);}
                speed.SetValue(motion,.55f);
                for(int f=0;f<600;f++){t+=1/60f;float yaw=f<240?0:Mathf.Min(95,(f-240)*.4f);actor.transform.localRotation=Quaternion.Euler(0,yaw,0);actor.transform.localPosition+=Quaternion.Euler(0,yaw,0)*Vector3.forward*(.55f/60);Sample(f>30);}
                speed.SetValue(motion,0f);
                for(int f=0;f<150;f++){t+=1/60f;Sample(f>45);}
                wave.Invoke(motion,null);for(int f=0;f<114;f++){t+=1/60f;Sample(true);}
                if(row.minFootY<-.001f||row.maxContactDrift>.003f||!row.skinAssetsStable||!row.heldPropsStable||!row.stationStable)failures++;rows.Add(row);UnityEngine.Object.DestroyImmediate(actor);
            }}finally{UnityEngine.Object.DestroyImmediate(parent);}
            return new Report{rows=rows.ToArray(),failures=failures};
        }
    }
}
#endif
