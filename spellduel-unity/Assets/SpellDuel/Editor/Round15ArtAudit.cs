#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace SpellDuel.Editor
{
    public static class Round15ArtAudit
    {
        [Serializable] public sealed class Row{public string id,configuration;public int baseTriangles,addedTriangles,extraRenderers;public float ratio;}
        [Serializable] public sealed class Report{public Row[] rows;public string method="Real CharacterRig.SetGear; mesh, renderer, idempotence, removal and material restoration checks";}
        [MenuItem("SpellDuel/Art/Validate Round 15 equipment")]
        public static void Menu(){var report=Run();string path=Path.Combine(Application.dataPath,"../Docs/round15_preview/unity_equipment_audit.json");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));Debug.Log("Round 15 equipment audit passed: "+report.rows.Length+" states. "+path);AssetDatabase.Refresh();}
        static int Tri(CharacterRig r)=>r.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
        static GearLook Look(int rarity,string set="",int slot=-1){var g=new GearLook();for(int i=0;i<5;i++)if(slot<0||slot==i)g.pieces[i]=new GearLook.Piece{rarity=rarity,set=set};return g;}
        public static Report Run()
        {
            var rows=new List<Row>();string[] bases={"swordsman","archer","mage","assassin","priest"},advanced={"paladin","berserker","marksman","ranger","archmage","summoner","ninja","shadowdancer","bishop","inquisitor"},sets={"flame","grove","tide","gladiator","loess","gale","guard"};
            var root=new GameObject("Temporary round 15 equipment audit");root.AddComponent<Light>().type=LightType.Directional;
            try{for(int cls=0;cls<15;cls++)
            {
                string id=cls<5?bases[cls]:advanced[cls-5];var rig=CharacterRig.Create(cls<5?id:bases[(cls-5)/2],cls<5?null:id,root.transform);
                int triangles=Tri(rig),renderers=rig.GetComponentsInChildren<Renderer>().Length;var sourceMeshes=rig.GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f,f=>f.sharedMesh);var sourceMaterials=rig.GetComponentsInChildren<Renderer>().ToDictionary(r=>r,r=>r.sharedMaterial);
                void Check(string label,GearLook g)
                {
                    rig.SetGear(g);int added=Tri(rig)-triangles,extra=rig.GetComponentsInChildren<Renderer>().Length-renderers;
                    if(added>triangles*.25f||extra>6)throw new InvalidOperationException(id+" equipment budget exceeded");
                    foreach(var pair in sourceMeshes)if(pair.Key.sharedMesh!=pair.Value)throw new InvalidOperationException(id+" original mesh changed");
                    int count=rig.GetComponentsInChildren<Renderer>().Length;rig.SetGear(g);rig.gameObject.SetActive(false);rig.gameObject.SetActive(true);
                    if(rig.GetComponentsInChildren<Renderer>().Length!=count)throw new InvalidOperationException(id+" visibility/idempotence failure");
                    rows.Add(new Row{id=id,configuration=label,baseTriangles=triangles,addedTriangles=added,ratio=added/(float)triangles,extraRenderers=extra});rig.SetGear(null);
                    if(Tri(rig)!=triangles||rig.GetComponentsInChildren<Renderer>().Length!=renderers)throw new InvalidOperationException(id+" stale gear after removal");
                    foreach(var pair in sourceMaterials)if(pair.Key.sharedMaterial!=pair.Value)throw new InvalidOperationException(id+" material not restored");
                }
                for(int slot=0;slot<5;slot++)for(int rarity=0;rarity<5;rarity++)Check("slot"+slot+"/rarity"+rarity,Look(rarity,"",slot));
                foreach(var set in sets)Check(set,Look(4,set));for(int rarity=0;rarity<5;rarity++)Check("all/rarity"+rarity,Look(rarity));
                var mixed=Look(2);for(int i=0;i<5;i++)mixed.pieces[i]=new GearLook.Piece{rarity=i,set=sets[(cls+i)%7]};Check("mixed",mixed);
                UnityEngine.Object.DestroyImmediate(rig.gameObject);
            }}finally{UnityEngine.Object.DestroyImmediate(root);}
            return new Report{rows=rows.ToArray()};
        }
    }
}
#endif
