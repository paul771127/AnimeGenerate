#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        static readonly BindingFlags PoseFields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly string[] PoseIds={"novice","swordsman","archer","mage","assassin","priest","paladin","berserker","marksman","ranger","archmage","summoner","ninja","shadowdancer","bishop","inquisitor"};
        [Serializable] public class PoseRow {public string id;public float crouchDrop,jumpLift,landingCompression,chargeArmDegrees;public bool finite,phonePriority,attackRecovered,cacheStable;public int images;}
        [Serializable] public class PoseReport {public string unity;public PoseRow[] classes;}
        static Transform PoseJoint(CharacterRig r,string field)=>(Transform)typeof(CharacterRig).GetField(field,PoseFields).GetValue(r);
        static float PoseFloat(CharacterRig r,string field)=>(float)typeof(CharacterRig).GetField(field,PoseFields).GetValue(r);
        static void PoseStep(CharacterRig r,float time)
        {var method=typeof(CharacterRig).GetMethod("Animate",PoseFields);for(float elapsed=0;elapsed<time-.00001f;elapsed+=1f/60)method.Invoke(r,new object[]{Mathf.Min(1f/60,time-elapsed)});}
        static void PosePhone(CharacterRig r,float offset)
        {
            // A calibrated sample enters the public API. Reset only transport interpolation in edit mode
            // (Time.deltaTime is zero); the visual crouch/landing animation still advances at 60 Hz.
            float eye=PoseFloat(r,"rigEye");if(eye<=0)eye=PoseJoint(r,"head").position.y+.013f;
            typeof(CharacterRig).GetField("phoneLast",PoseFields).SetValue(r,-99f);
            r.DrivePhone(new Vector3(0,eye+offset,CharacterRig.PhoneReach),Quaternion.identity,eye,eye-.6f,false,Vector3.zero);
        }
        static string PoseAttack(string id)
        {var cls=Advanced.Get(id)?.baseCls??id;return cls=="novice"?"novice":cls=="swordsman"?"slash":cls=="archer"?"shoot":cls=="assassin"?"stab":"magic";}

        static void PlayerPoseShots(Camera cam)
        {
            var rows=new List<PoseRow>();
            foreach(string id in PoseIds)
            {
                var row=new PoseRow{id=id,finite=true,phonePriority=true,attackRecovered=true,cacheStable=true};
                var shots=new List<string>();string cls=Advanced.Get(id)?.baseCls??id;
                foreach(string state in new[]{"stand","combat","crouch","jump","charge","attack","guard"})
                {
                    var rig=CharacterRig.Create(cls,cls==id?null:id);rig.InCombat=state!="stand";PoseStep(rig,.6f);
                    float height=PoseJoint(rig,"head").position.y;
                    if(state=="crouch"||state=="jump")
                    {
                        PosePhone(rig,state=="crouch"?-.58f:.26f);PoseStep(rig,.3f);
                        if(state=="crouch")row.crouchDrop=height-PoseJoint(rig,"head").position.y;
                        else row.jumpLift=PoseJoint(rig,"body").localPosition.y;
                    }
                    if(state=="charge")
                    {rig.Charge=.1f;PoseStep(rig,.3f);var q=PoseJoint(rig,"shR").localRotation;rig.Charge=1;PoseStep(rig,.3f);row.chargeArmDegrees=Quaternion.Angle(q,PoseJoint(rig,"shR").localRotation);}
                    if(state=="attack"){rig.PlayAttack(PoseAttack(id),.6f);PoseStep(rig,.75f);}
                    if(state=="guard"){rig.Guarding=true;PoseStep(rig,.3f);}
                    foreach(var t in rig.GetComponentsInChildren<Transform>())if(float.IsNaN(t.position.sqrMagnitude)||float.IsInfinity(t.position.sqrMagnitude))row.finite=false;
                    string name="player_pose_"+id+"_"+state;shots.Add(name);row.images++;
                    CarpetFit(cam,rig.gameObject,name,new Vector3(.38f,.12f,1),480,640);
                    if(state=="crouch")
                    {float y=PoseJoint(rig,"body").position.y;rig.PlayAttack(PoseAttack(id),0);PoseStep(rig,.14f);row.phonePriority=Mathf.Abs(PoseJoint(rig,"body").position.y-y)<.015f;}
                    if(state=="jump")
                    {PosePhone(rig,0);PoseStep(rig,1f/60);row.landingCompression=PoseFloat(rig,"poseLanding");PoseStep(rig,.4f);if(PoseFloat(rig,"poseLanding")>.001f)throw new Exception("Landing did not recover "+id);}
                    if(state=="attack"){PoseStep(rig,.7f);row.attackRecovered=!rig.Attacking;}
                    // No per-frame mesh/material creation during the seven animation states.
                    int meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,materials=Resources.FindObjectsOfTypeAll<Material>().Length;
                    PoseStep(rig,.3f);row.cacheStable&=meshes==Resources.FindObjectsOfTypeAll<Mesh>().Length&&materials==Resources.FindObjectsOfTypeAll<Material>().Length;
                    UnityEngine.Object.DestroyImmediate(rig.gameObject);
                }
                ContactSheet(shots,"player_pose_"+id+"_overview",7);
                if(!row.finite||!row.phonePriority||!row.attackRecovered||!row.cacheStable||row.crouchDrop<.3f||row.jumpLift<.15f||row.chargeArmDegrees<3||row.landingCompression<=0)throw new Exception("Player pose audit failed "+JsonUtility.ToJson(row));
                rows.Add(row);
            }
            File.WriteAllText("snapshots/player_pose_audit.json",JsonUtility.ToJson(new PoseReport{unity=Application.unityVersion,classes=rows.ToArray()},true));
            Debug.Log("[SpellDuel] 16 player classes / 112 poses: phone priority, jump landing, progressive charge, recovery and caches passed.");
        }
    }
}
#endif
