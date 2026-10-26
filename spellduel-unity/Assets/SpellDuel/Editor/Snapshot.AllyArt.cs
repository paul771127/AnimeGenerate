#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace SpellDuel.EditorTools {
public static partial class Snapshot {
static readonly BindingFlags AllyInstance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
[Serializable] public class AllyRow {public string id,gear;public float anchorClearance,deathGray,reviveGray,reviveLight,fadeOpacity;public Vector3 labelScreen,faceScreen;public Rect labelRect,faceRect;}
[Serializable] public class AllyReport {public string unity;public AllyRow[] allies;public bool sixVisible,offlineLatch;}
static void PendingAllyShots(Camera cam){
 var probeGo=new GameObject("Ally visual lifecycle probe");var probe=probeGo.AddComponent<SoloBattle>();probe.enabled=false;
 var departed=(HashSet<int>)typeof(SoloBattle).GetField("allyArtDeparted",AllyInstance).GetValue(probe);var gate=typeof(SoloBattle).GetMethod("AllyArtPresent",AllyInstance);departed.Add(5);
 if((bool)gate.Invoke(probe,new object[]{5}))throw new Exception("Disconnected art respawns");probe.NetPeers[5]=new SoloBattle.PeerPose{seen=Time.time-1};if((bool)gate.Invoke(probe,new object[]{5}))throw new Exception("Stale pose revives art");probe.NetPeers[5].seen=Time.time;if(!(bool)gate.Invoke(probe,new object[]{5})||departed.Contains(5))throw new Exception("Reconnected art not restored");UnityEngine.Object.DestroyImmediate(probeGo);

 float oldAspect=cam.aspect;cam.aspect=2f;var usedLabels=new List<Rect>();var rows=new List<AllyRow>();var rigs=new List<CharacterRig>();var type=typeof(CharacterRig).Assembly.GetType("SpellDuel.AllyPresentation");var get=type.GetMethod("For",BindingFlags.Static|BindingFlags.NonPublic);var state=type.GetMethod("State",AllyInstance);var step=type.GetMethod("Step",AllyInstance);var removing=type.GetMethod("BeginRemoval",AllyInstance);var anchor=type.GetProperty("LabelAnchor",AllyInstance);var place=type.GetMethod("PlaceLabel",BindingFlags.Static|BindingFlags.NonPublic);var animate=typeof(CharacterRig).GetMethod("Animate",AllyInstance);
 string[] ids={"novice","swordsman","mage","paladin","ninja","bishop"};
 for(int i=0;i<ids.Length;i++){
  string id=ids[i],basic=Advanced.Get(id)?.baseCls??id;var rig=CharacterRig.Create(basic,id==basic?null:id);rigs.Add(rig);rig.SetPose(new Vector3((i-2.5f)*1.05f,0,0),Vector3.forward);rig.SetGear(GearLook.Parse((i%4+1)+":flame/3:flame/2:flame/1:flame/2:flame"));
  var art=get.Invoke(null,new object[]{rig});var p=(Vector3)anchor.GetValue(art);float headTop=-10;
  foreach(var f in rig.GetComponentsInChildren<MeshFilter>())if(f.GetComponent<Renderer>().sharedMaterial.HasProperty("_HumanFace")&&f.GetComponent<Renderer>().sharedMaterial.GetFloat("_HumanFace")==1)headTop=Mathf.Max(headTop,f.GetComponent<Renderer>().bounds.max.y);
  var row=new AllyRow{id=id,gear=rig.GearCode,anchorClearance=p.y-headTop};if(row.anchorClearance<.12f)throw new Exception("Ally label crosses face "+id);rows.Add(row);state.Invoke(art,new object[]{true});step.Invoke(art,new object[]{0f});
 }
 Action<string> shot=name=>Shot(cam,rigs[0].gameObject,"pending_allies_six_"+name,new Vector3(0,2.6f,9.8f),new Vector3(0,1.05f,0),1536,768);
 shot("alive");
 for(int i=0;i<rigs.Count;i++){
  var art=get.Invoke(null,new object[]{rigs[i]});rows[i].labelScreen=AllyScreen(cam,(Vector3)anchor.GetValue(art));rows[i].faceScreen=AllyScreen(cam,rigs[i].transform.position+Vector3.up*1.55f);rows[i].labelRect=(Rect)place.Invoke(null,new object[]{rows[i].labelScreen,1536f,768f,24,usedLabels});
  foreach(var f in rigs[i].GetComponentsInChildren<MeshFilter>())if(f.GetComponent<Renderer>().sharedMaterial.HasProperty("_HumanFace")&&f.GetComponent<Renderer>().sharedMaterial.GetFloat("_HumanFace")==1){var bounds=f.GetComponent<Renderer>().bounds;Vector2 lo=new Vector2(10000,10000),hi=new Vector2(-10000,-10000);for(int corner=0;corner<8;corner++){var v=AllyScreen(cam,bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1)));lo=new Vector2(Mathf.Min(lo.x,v.x),Mathf.Min(lo.y,768-v.y));hi=new Vector2(Mathf.Max(hi.x,v.x),Mathf.Max(hi.y,768-v.y));}rows[i].faceRect=Rect.MinMaxRect(lo.x,lo.y,hi.x,hi.y);break;}
  state.Invoke(art,new object[]{false});rigs[i].SetDead(true);animate.Invoke(rigs[i],new object[]{1f});step.Invoke(art,new object[]{0f});rows[i].deathGray=ReadAllyFloat(rigs[i],"_ArtGray");if(rows[i].deathGray<.8f)throw new Exception("Ally not desaturated");
 }foreach(var label in rows)foreach(var face in rows)if(label.labelRect.Overlaps(face.faceRect))throw new Exception("Ally label overlaps a face");shot("down");
 for(int i=0;i<rigs.Count;i++){var art=get.Invoke(null,new object[]{rigs[i]});state.Invoke(art,new object[]{true});rigs[i].SetDead(false);animate.Invoke(rigs[i],new object[]{1f});step.Invoke(art,new object[]{.05f});rows[i].reviveGray=ReadAllyFloat(rigs[i],"_ArtGray");rows[i].reviveLight=ReadAllyFloat(rigs[i],"_ArtRevive");if(rows[i].reviveGray!=0||rows[i].reviveLight<=0)throw new Exception("Revive state not restored");}shot("revive");
 for(int i=0;i<rigs.Count;i++){var art=get.Invoke(null,new object[]{rigs[i]});removing.Invoke(art,null);step.Invoke(art,new object[]{.325f});rows[i].fadeOpacity=ReadAllyFloat(rigs[i],"_ArtFade");if(Mathf.Abs(rows[i].fadeOpacity-.5f)>.001f)throw new Exception("Offline fade missing");}shot("offline");
 File.WriteAllText("snapshots/pending_ally_audit.json",JsonUtility.ToJson(new AllyReport{unity=Application.unityVersion,allies=rows.ToArray(),sixVisible=true,offlineLatch=true},true));foreach(var rig in rigs)UnityEngine.Object.DestroyImmediate(rig.gameObject);cam.aspect=oldAspect;
 foreach(string id in new[]{"wb_furnace","wb_canopy","wb_leviathan"}){var tex=Resources.Load<Texture2D>("WorldBossNotices/"+id);if(!tex||tex.width!=768||tex.height!=1152)throw new Exception("Missing world boss poster "+id);}
 Debug.Log("[SpellDuel] Pending ally audit passed: six geared rigs, labels above faces, death/revive/fade and three posters.");
}
static Vector3 AllyScreen(Camera c,Vector3 p){var v=c.WorldToViewportPoint(p);return new Vector3(v.x*1536,v.y*768,v.z);}
static float ReadAllyFloat(CharacterRig rig,string key){foreach(var r in rig.GetComponentsInChildren<Renderer>())if(r.sharedMaterial.HasProperty("_ArtFade")){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);return b.GetFloat(key);}throw new Exception("No toon ally renderer");}
}}
#endif
