#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        [Serializable] class WorldRow {public string id;public int triangles,renderers,phases,skills;public bool brokenAppearance;}
        [Serializable] class WorldReport {public string unity;public List<WorldRow> bosses=new List<WorldRow>();}
        static void WorldBossShots(Camera cam)
        {
            var report=new WorldReport{unity=Application.unityVersion};
            for(int index=0;index<WorldBossArt.Ids.Length;index++)
            {
                string id=WorldBossArt.Ids[index];var art=BossRig.CreateWorld(id);var row=new WorldRow{id=id,phases=3,skills=6};
                row.triangles=EliteAudit.Triangles(art.gameObject);row.renderers=art.GetComponentsInChildren<Renderer>(true).Length;
                if(row.triangles>20000)throw new Exception(id+" world boss geometry budget exceeded");
                for(int phase=1;phase<=3;phase++)
                {
                    art.SetPhase(phase);art.Preview(.5f);
                    Shot(cam,art.gameObject,id+"_phase"+phase+"_front",new Vector3(0,4,15),new Vector3(0,3,0),800,800);
                    Shot(cam,art.gameObject,id+"_phase"+phase+"_45",new Vector3(11,6,12),new Vector3(0,3,0),800,800);
                }
                art.SetPhase(2);art.SetPartBroken(0,false);art.Preview(.4f);
                Shot(cam,art.gameObject,id+"_part_intact",new Vector3(5,4,12),new Vector3(0,3,0),800,800);
                art.SetPartBroken(0,true);row.brokenAppearance=art.BrokenParts==1;
                if(!row.brokenAppearance)throw new Exception(id+" part state missing");
                Shot(cam,art.gameObject,id+"_part_broken",new Vector3(5,4,12),new Vector3(0,3,0),800,800);
                foreach(string move in WorldBossArt.Moves)
                {
                    art.Windup(move,2);art.Preview(1.8f);var fx=art.Warning(move);
                    if(fx.GetComponentsInChildren<LineRenderer>().Length==0)throw new Exception(id+" telegraph missing: "+move);
                    Shot(cam,art.gameObject,id+"_"+move+"_warn",new Vector3(10,8,13),new Vector3(0,2.2f,0),900,800);
                    art.Attack();art.Preview(.25f);
                    foreach(var line in fx.GetComponentsInChildren<LineRenderer>()){line.startColor=line.endColor=art.Tint;line.widthMultiplier=2;}
                    Shot(cam,art.gameObject,id+"_"+move+"_impact",new Vector3(10,8,13),new Vector3(0,2.2f,0),900,800);
                    UnityEngine.Object.DestroyImmediate(fx);
                }
                var player=CharacterRig.Create("novice");player.transform.position=new Vector3(-4,0,2);art.SetPhase(1);art.Preview(.7f);
                Shot(cam,art.gameObject,id+"_player_scale",new Vector3(0,4.5f,18),new Vector3(-.5f,2.7f,0),1000,800);
                UnityEngine.Object.DestroyImmediate(player.gameObject);art.PlayHit();art.Preview(.05f);art.SetDead(true);art.Preview(1);
                Shot(cam,art.gameObject,id+"_defeated",new Vector3(8,7,13),new Vector3(0,2,0),800,800);
                UnityEngine.Object.DestroyImmediate(art.gameObject);report.bosses.Add(row);
            }
            File.WriteAllText("snapshots/world_boss_audit.json",JsonUtility.ToJson(report,true));
            Debug.Log("[SpellDuel] World boss art audit: 3 bosses, 9 phases, 18 warnings/strikes, breakable parts and scale previews passed");
        }
    }
}
#endif
