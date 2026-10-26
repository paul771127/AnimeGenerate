#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace SpellDuel.EditorTools {
public static partial class Snapshot {
static void NeckHairShots(Camera cam){
 string[] ids={"novice","swordsman","archer","mage","assassin","priest","paladin","berserker","marksman","ranger","archmage","summoner","ninja","shadowdancer","bishop","inquisitor"};var overview=new List<string>();
 foreach(var id in ids){
  string basic=Advanced.Get(id)?.baseCls??id;var rig=CharacterRig.Create(basic,id==basic?null:id);rig.InCombat=false;typeof(CharacterRig).GetMethod("Animate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rig,new object[]{.5f});Vector3 target=rig.transform.position+Vector3.up*1.55f;
  foreach(var f in rig.GetComponentsInChildren<MeshFilter>())if(f.GetComponent<Renderer>().sharedMaterial.HasProperty("_HumanFace")&&f.GetComponent<Renderer>().sharedMaterial.GetFloat("_HumanFace")==1){target=f.GetComponent<Renderer>().bounds.center;break;}
  var angles=new[]{new Vector3(0,.01f,.85f),new Vector3(.85f,.01f,0),new Vector3(.60f,.01f,.60f),new Vector3(.22f,.55f,.60f),new Vector3(0,.02f,-.85f),new Vector3(.18f,-.30f,.72f)};string[] names={"front","side","45","overhead","back","low"};
  for(int i=0;i<names.Length;i++)Shot(cam,rig.gameObject,"neck_"+id+"_"+names[i],target+angles[i],target-Vector3.up*.025f,640,640);
  FullShot(cam,rig,"neck_"+id+"_full_front",new Vector3(0,.025f,1));FullShot(cam,rig,"neck_"+id+"_full_45",new Vector3(.70f,.025f,.70f));
  rig.SetGear(GearLook.Parse("3:flame/3:flame/3:flame/3:flame/3:flame"));Shot(cam,rig.gameObject,"neck_"+id+"_gear_overhead",target+angles[3],target-Vector3.up*.025f,640,640);
  overview.Add("neck_"+id+"_front");Object.DestroyImmediate(rig.gameObject);
 }
 ContactSheet(overview,"neck_all_16",4);Debug.Log("[SpellDuel] Native neck/hair gallery: 16 classes, 9 views each.");
}
}}
#endif
