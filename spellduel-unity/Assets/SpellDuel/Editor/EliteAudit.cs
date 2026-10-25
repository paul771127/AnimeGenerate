#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static class EliteAudit
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Require(bool value,string message){if(!value)throw new Exception(message);}
        static object Call(Battle b,string name,params object[] args)=>typeof(Battle).GetMethod(name,Private).Invoke(b,args);
        public static int Triangles(GameObject go){int n=0;foreach(var f in go.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh)n+=f.sharedMesh.triangles.Length/3;return n;}
        static Battle Make(bool remote=false)
        {var p=new Fighter("audit",Skills.Classes["mage"],new string[0],true);p.head=new Vector3(0,1.7f,1.2f);p.maxHp=10000;p.hp=10000;var e=new Fighter("boss",Skills.Classes["boss"],new string[0],false);return new Battle(p,e){remoteEnemy=remote};}
        public static bool VertexColorPath(GameObject go)
        {
            bool found=false;
            foreach(var f in go.GetComponentsInChildren<MeshFilter>())
            {
                if(!f.sharedMesh||!f.sharedMesh.name.StartsWith("elite/")&&!f.sharedMesh.name.StartsWith("world/"))continue;
                var m=f.GetComponent<Renderer>().sharedMaterial;
                if(!m.IsKeywordEnabled("SD_HUMAN")||m.GetFloat("_HumanHair")<.5f||m.GetFloat("_HumanFace")>.5f)return false;
                var colors=f.sharedMesh.colors;if(colors.Length!=f.sharedMesh.vertexCount)return false;
                foreach(var c in colors)if(Mathf.Abs(c.r-c.b)>.08f||Mathf.Abs(c.g-c.b)>.08f)found=true;
            }
            return found;
        }
        [Serializable] public class Row{public int kind,triangles,baseline;public bool soul,hostWarning,remoteWarning,remoteStrike,vertexColors;}
        [Serializable] public class Report{public string unity;public List<Row> classes=new List<Row>();public int testedSkills;}
        [MenuItem("SpellDuel/Audit/Elite encounters")]
        public static void Run()
        {
            EliteCombatAudit.Run();DungeonSpawnAudit.Run();
            var report=new Report{unity=Application.unityVersion};
            foreach(var d in EliteCatalog.All)
            {
                var baseRig=MinionRig.Create();baseRig.SetVariant(d.baseKind);int baseline=Triangles(baseRig.gameObject);UnityEngine.Object.DestroyImmediate(baseRig.gameObject);
                var rig=MinionRig.Create();rig.SetVariant(d.kind);int tris=Triangles(rig.gameObject);
                Require(tris<=baseline*2,d.name+" exceeds double baseline triangles: "+tris+" / "+baseline);
                var row=new Row{kind=d.kind,triangles=tris,baseline=baseline,soul=Souls.OfMinion(d.kind)!=null&&Mathf.Abs(Souls.OfMinion(d.kind).chance-.01f)<.00001f};Require(row.soul,d.name+" soul missing");
                row.vertexColors=VertexColorPath(rig.gameObject);Require(row.vertexColors,d.name+" vertex color material disabled");
                for(int slot=0;slot<2;slot++)
                {
                    var b=Make();var m=b.SpawnMinion(Vector3.zero,1,d.kind);var skill=Skills.All[EliteCatalog.SkillId(d.kind,slot)];var state=b.EliteView(m);
                    Call(b,"TickElite",m,b.player,1.2f,.016f,0);state=b.EliteView(m);
                    // Force each branch through its real host windup/strike path.
                    state.slot=slot;state.center=d.moves[slot]==EliteMove.Burst?b.player.Feet:Vector3.zero;state.forward=Vector3.forward;state.duration=skill.charge;state.until=b.now+skill.charge;m.windupUntil=state.until;
                    Require(skill.charge>=1,row.kind+" warning shorter than one second");row.hostWarning=state.until>b.now;
                    b.now+=skill.charge+.01f;Call(b,"TickElite",m,b.player,1.2f,.016f,0);
                    Require(state.ready[slot]>b.now,d.name+" cooldown missing");
                    var remote=Make(true);remote.RemoteEliteWarn(1,skill.id,state.center,Vector3.forward,skill.charge);
                    var rm=remote.minions.Find(x=>x.minionId==1);row.remoteWarning=remote.EliteView(rm).slot==slot&&rm.windupUntil>remote.now;
                    float hp=remote.player.hp;remote.RemoteSpecialStrike(1,skill.id,state.center,Vector3.forward,20);row.remoteStrike=remote.EliteView(rm).striking;
                    if(d.moves[slot]==EliteMove.Sweep||d.moves[slot]==EliteMove.Line||d.moves[slot]==EliteMove.Burst)Require(remote.player.hp<hp,d.name+" remote hit missing");
                    if(d.moves[slot]==EliteMove.Sweep){var duck=Make(true);duck.player.crouching=true;duck.RemoteSpecialStrike(1,skill.id,Vector3.zero,Vector3.forward,20);Require(duck.player.hp==10000,"Crouch did not avoid sweep");}
                    var far=Make(true);far.player.head=new Vector3(0,1.7f,5);far.RemoteSpecialStrike(1,skill.id,Vector3.zero,Vector3.forward,20);Require(far.player.hp==10000,"Attack hit outside telegraph");
                    rig.EliteSkill(slot);rig.Windup(skill.charge);typeof(MinionRig).GetMethod("Animate",Private).Invoke(rig,new object[]{skill.charge});rig.Attack();typeof(MinionRig).GetMethod("Animate",Private).Invoke(rig,new object[]{.15f});report.testedSkills++;
                    Require(row.hostWarning&&row.remoteWarning&&row.remoteStrike,d.name+" missing synchronized state");
                }
                rig.PlayHit();rig.SetDead(true);typeof(MinionRig).GetMethod("Animate",Private).Invoke(rig,new object[]{.4f});UnityEngine.Object.DestroyImmediate(rig.gameObject);report.classes.Add(row);
            }
            var spawn=Make();spawn.endless=true;spawn.wave=15;spawn.themeOverride=1;spawn.eliteEncounters=true;
            int k=(int)Call(spawn,"EliteSpawnKind",7,5,false);Require(k>=28&&k<=32,"Wrong theme elite");spawn.SpawnMinion(Vector3.zero,1,k);Require((int)Call(spawn,"EliteSpawnKind",7,5,false)==7,"Two elites at once");
            spawn.minions.Clear();spawn.specialKind=19;Require((int)Call(spawn,"EliteSpawnKind",7,5,false)==7,"Elite mixed with rare encounter");
            Directory.CreateDirectory("snapshots");File.WriteAllText("snapshots/elite_audit.json",JsonUtility.ToJson(report,true));
            Debug.Log("[SpellDuel] Elite audit: 25 rigs, 50 skills, host/client hit and crouch/range checks, souls, spawn exclusion passed");
        }
    }
}
#endif
