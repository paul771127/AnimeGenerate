#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        [Serializable] public class PolishFxRow { public string id,phase,view; public int particles,meshTriangles,lineTriangleBound; }
        [Serializable] public class PolishFxReport { public string unity; public List<string> auditedSkills=new List<string>(); public List<PolishFxRow> samples=new List<PolishFxRow>();public bool mobileFpsMeasured=false;public int sixSpellParticles,sixSpellTriangleBound; }
        public static void RenderUiSkillFx()
        {
            Directory.CreateDirectory("snapshots");EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);ToonStyle.Apply();WorldFrame.Reset();
            var camera=new GameObject("UI polish FX camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.03f;camera.fieldOfView=55;camera.backgroundColor=new Color(.09f,.12f,.18f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Key").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-35,0);RenderSettings.ambientLight=Color.white*.65f;
            var report=new PolishFxReport{unity=Application.unityVersion};
            foreach(var definition in Skills.All.Values)
                if(Skills.Classes.ContainsKey(definition.cls)&&!definition.cls.StartsWith("boss"))
                    report.auditedSkills.Add(definition.id+" | "+definition.cls+" | "+definition.type+" | radius="+definition.radius+" area="+definition.areaRadius+" range="+definition.rangeMax);
            if(SpellReadability.NearFade(.2f)>.1f||SpellReadability.NearFade(1f)<.99f)throw new Exception("Near-camera attenuation invalid");
            if(SpellReadability.ViewDistance(new Bounds(Vector3.zero,new Vector3(6,.05f,6)),new Vector3(0,1.6f,0))<1)throw new Exception("Ground perimeter incorrectly faded around camera");
            var ids="nv_swing nv_stone nv_aid slash thrust wave quickshot snipe triple fire ice meteor knife poison smoke holy prayheal cleanse shieldbash holyblade sanction jianlai yujian wanjian headshot scattershot arrowrain falcon rangerhaste landmine arcanewyrm starfall arcanebreak firespirit spiritbolt earthspike shuriken suiton futon poisondart execute poisonfog revive massheal sanctuary holylance holyfire verdict".Split(' ');
            foreach(string id in ids)
            {
                if(!Skills.All.TryGetValue(id,out var skill))throw new Exception("Unknown fixture skill "+id);
                foreach(string view in new[]{"self","spectator"})foreach(string phase in new[]{"charge","release","impact"})
                {
                    NewFxClean();var center=new Vector3(0,1.2f,2);var eye=view=="self"?new Vector3(0,1.6f,0):new Vector3(1.6f,2f,.3f);
                    camera.transform.position=eye;camera.transform.LookAt(center);
                    var anchor=new GameObject("Preview caster");anchor.transform.position=new Vector3(0,0,2);
                    GameObject subject=anchor;
                    if(phase=="charge")
                    {
                        var charge=SpellFx.StartCharge(skill.cls,skill.color,view=="self"?camera.transform:anchor.transform,view=="self"?new Vector3(0,-.15f,.45f):new Vector3(0,1.2f,.4f),view=="self"?.35f:1f);charge.Progress=1;subject=charge.gameObject;
                        typeof(ChargeFx).GetField("clock",RareFlags).SetValue(charge,.5f);typeof(ChargeFx).GetField("shownP",RareFlags).SetValue(charge,1f);typeof(ChargeFx).GetField("readyAmt",RareFlags).SetValue(charge,1f);typeof(ChargeFx).GetMethod("LateUpdate",RareFlags).Invoke(charge,null);
                    }
                    else if(phase=="release")
                    {
                        if(id=="jianlai"){subject=SpellFx.CreateSwordSageBlade();subject.transform.position=center;subject.transform.localScale=Vector3.one*.19f;}
                        else if(id=="yujian"){subject=SpellFx.CreateProjectileVisual(Skills.All["yj_blade"],skill.cls);subject.transform.position=center;}
                        else if(id=="falcon"||id=="firespirit")AdvancedFx.Summon(id=="falcon"?"falcon":"fire",center,skill.color);
                        else if(id=="rangerhaste")SpellFx.TeamBuffCast(id,anchor.transform);
                        else if(skill.type==SkillType.Projectile){subject=SpellFx.CreateProjectileVisual(skill,skill.cls);subject.transform.position=center;
                            for(int sample=0;sample<12;sample++){subject.transform.position=center+Vector3.forward*(sample/11f-.5f);NewFxSample(.0125f);}}
                        else if(skill.type==SkillType.Trap){subject=SpellFx.CreateTrapVisual(skill);SpellFx.SetTrapArmed(subject,true);subject.transform.position=new Vector3(0,0,2);}
                        else {SpellFx.CastBurst(skill.cls,skill.color,center,Vector3.forward);SpellFx.AreaBurst(id,new Vector3(0,0,2),skill.areaRadius>0?skill.areaRadius:skill.rangeMax);AdvancedFx.Area(id,new Vector3(0,0,2),skill.areaRadius>0?skill.areaRadius:skill.rangeMax,skill.color);}
                    }
                    else
                    {
                        SpellFx.Hit(SpellFx.HitClassOf(skill)??skill.cls,skill.color,center,1,false);
                        if(id=="arcanewyrm")SpellFx.WyrmPierce(center);
                        else if(id=="arcanebreak")SpellFx.ArcaneBreakHit(center);
                        else SpellFx.SkillImpact(id,center);
                    }
                    NewFxSample(.15f);

                    foreach(var fx in UnityEngine.Object.FindObjectsOfType<SpellReadability>())fx.Evaluate(camera,.15f);
                    var row=new PolishFxRow{id=id,phase=phase,view=view};
                    foreach(var ps in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())row.particles+=ps.main.maxParticles;
                    foreach(var mesh in UnityEngine.Object.FindObjectsOfType<MeshFilter>())if(mesh.sharedMesh)row.meshTriangles+=mesh.sharedMesh.triangles.Length/3;
                    foreach(var line in UnityEngine.Object.FindObjectsOfType<LineRenderer>())row.lineTriangleBound+=(line.positionCount+2)*(4+line.numCornerVertices*4+line.numCapVertices*4);
                    if(row.particles>576)throw new Exception(id+" combined-stage particle budget exceeded");
                    if(row.meshTriangles+row.lineTriangleBound>24000)throw new Exception(id+" geometry budget exceeded");
                    int textures=Resources.FindObjectsOfTypeAll<Texture2D>().Length,materials=Resources.FindObjectsOfTypeAll<Material>().Length;
                    var readability=UnityEngine.Object.FindObjectsOfType<SpellReadability>();
                    for(int iteration=0;iteration<8;iteration++)foreach(var fx in readability)fx.Evaluate(camera,.15f);
                    if(textures!=Resources.FindObjectsOfTypeAll<Texture2D>().Length||materials!=Resources.FindObjectsOfTypeAll<Material>().Length)throw new Exception("Readability allocates textures/materials while ticking");
                    report.samples.Add(row);Shot(camera,subject,"polish_fx_"+id+"_"+view+"_"+phase,eye,center,720,960);
                    if(phase=="impact")foreach(var fx in readability)
                    {
                        fx.Evaluate(camera,.5f);
                        if(SpellReadability.IsImpact(fx.name))foreach(var renderer in fx.GetComponentsInChildren<Renderer>())
                            if(renderer.enabled)throw new Exception("Impact obscures view after 0.5 seconds: "+fx.name);
                    }
                }
            }
            NewFxClean();string[] ultimates={"meteor","starfall","arcanewyrm","wanjian","verdict","arcanebreak"};
            for(int i=0;i<ultimates.Length;i++)
            {
                var definition=Skills.All[ultimates[i]];var fx=SpellFx.CreateProjectileVisual(definition,definition.cls);
                fx.transform.position=new Vector3((i%3-1)*1.2f,1.2f,2+i/3*1.3f);
            }
            NewFxSample(.15f);foreach(var fx in UnityEngine.Object.FindObjectsOfType<SpellReadability>())fx.Evaluate(camera,.15f);
            foreach(var ps in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())report.sixSpellParticles+=ps.main.maxParticles;
            foreach(var mf in UnityEngine.Object.FindObjectsOfType<MeshFilter>())if(mf.sharedMesh)report.sixSpellTriangleBound+=mf.sharedMesh.triangles.Length/3;
            foreach(var line in UnityEngine.Object.FindObjectsOfType<LineRenderer>())report.sixSpellTriangleBound+=(line.positionCount+2)*(4+line.numCornerVertices*4+line.numCapVertices*4);
            if(report.sixSpellParticles>3456||report.sixSpellTriangleBound>144000)throw new Exception("Six-spell combined budget exceeded");
            Shot(camera,camera.gameObject,"polish_fx_six_ultimates",new Vector3(4,3,-1),new Vector3(0,1.2f,2.5f),1080,1440);
            File.WriteAllText("snapshots/polish_fx_audit.json",JsonUtility.ToJson(report,true));NewFxClean();
        }
    }
}
#endif
