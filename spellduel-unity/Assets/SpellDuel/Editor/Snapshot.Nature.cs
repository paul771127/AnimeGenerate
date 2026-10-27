#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        static void NatureShots(Camera cam)
        {
            var kinds=new[]{Battle.HazardKind.Lava,Battle.HazardKind.Spore,Battle.HazardKind.Roots,Battle.HazardKind.Tide,Battle.HazardKind.Whirl};
            float[] radii={Battle.LavaRadius,Battle.SporeRadius,Battle.RootRadius,Battle.TideHalfWidth,Battle.WhirlRadius};
            for(int i=0;i<kinds.Length;i++)
            {
                var art=HazardArt.Nature(kinds[i],radii[i]);
                int tris=0;foreach(var f in art.GetComponentsInChildren<MeshFilter>(true))
                {
                    tris+=f.sharedMesh.triangles.Length/3;
                    foreach(var p in f.sharedMesh.vertices)if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z))throw new Exception("NaN nature mesh");
                }
                if(tris>3000)throw new Exception("Nature triangle budget");
                for(int state=0;state<3;state++)
                {
                    art.Tick(state*.5f,1.25f);
                    if(state==0 && art.Active.gameObject.activeSelf)throw new Exception("Retracted hazard visible");
                    if(state==0 && kinds[i]==Battle.HazardKind.Whirl && art.Warning.gameObject.activeSelf)throw new Exception("Inactive whirl warning visible");
                    if(state==2 && !art.Active.gameObject.activeSelf)throw new Exception("Active hazard hidden");
                    if(state==2)art.Motes.Simulate(.8f,true,true);
                    if(state==1 && art.Smoke)art.Smoke.Simulate(.8f,true,true);
                    float d=kinds[i]==Battle.HazardKind.Tide?2.4f:1;
                    Shot(cam,art.gameObject,"nature_"+kinds[i]+"_"+state,new Vector3(2.2f,2.8f,4.7f)*d,new Vector3(0,.55f,0),960,720);
                }
                UnityEngine.Object.DestroyImmediate(art.gameObject);
            }
            File.WriteAllText("snapshots/nature_audit.json","{\"kinds\":5,\"states\":15,\"maxTriangles\":3000,\"retractedHidden\":true}");
            Debug.Log("[SpellDuel] Nature art audit passed");
        }
    }
}
#endif
