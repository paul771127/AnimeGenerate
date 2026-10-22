#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace SpellDuel.Editor
{
    // Editor-only: samples the real rig animator and the actual closed first torso loft.
    // No collision/gameplay fields are read or modified.
    public static class Round14ArtAudit
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        [Serializable] public sealed class Row { public string id,action,side;public int samples;public float minimumSignedDistance; }
        [Serializable] public sealed class Report { public string method="Signed distance to the first closed torso loft; 60 Hz samples";public Row[] rows;public int failures; }
        [MenuItem("SpellDuel/Art/Validate Round 14 wrists")]
        public static void Menu()
        {
            string path=Path.Combine(Application.dataPath,"../Docs/round14_preview/unity_motion_audit.json");
            var report=Run();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
            Debug.Log("Round 14 wrist audit: "+report.failures+" failing rows. "+path);AssetDatabase.Refresh();
        }
        static void Animate(CharacterRig rig,float dt)=>typeof(CharacterRig).GetMethod("Animate",Flags).Invoke(rig,new object[]{dt});
        static Transform Bone(CharacterRig rig,string name)=>(Transform)typeof(CharacterRig).GetField(name,Flags).GetValue(rig);
        public static Report Run()
        {
            var result=new List<Row>();int failures=0;
            string[] bases={"swordsman","archer","mage","assassin","priest"},advanced={"paladin","berserker","marksman","ranger","archmage","summoner","ninja","shadowdancer","bishop","inquisitor"};
            string[] actions={"idle","combat","walk","dash","guard","charge","cast","hit","slash","stab","lunge","shoot","magic","down"};
            var root=new GameObject("Temporary round 14 art audit");var light=root.AddComponent<Light>();light.type=LightType.Directional;
            try
            {
                for(int cls=0;cls<15;cls++)
                {
                    string id=cls<5?bases[cls]:advanced[cls-5],baseId=cls<5?id:bases[(cls-5)/2];var rig=CharacterRig.Create(baseId,cls<5?null:id,root.transform);
                    var spine=Bone(rig,"spine");var torso=spine.Find("Outfit torso").GetComponent<MeshFilter>().sharedMesh;var vertices=torso.vertices;var indices=torso.triangles;
                    var hands=new[]{Bone(rig,"handL"),Bone(rig,"handR")};
                    foreach(string action in actions)
                    {
                        rig.SetDead(false);rig.InCombat=false;rig.MoveSpeed=0;rig.Guarding=false;rig.Charge=0;
                        for(int i=0;i<72;i++)Animate(rig,1/60f);
                        switch(action){case "combat":rig.InCombat=true;break;case "walk":rig.MoveSpeed=1.2f;break;case "dash":rig.MoveSpeed=4;break;case "guard":rig.Guarding=true;break;case "charge":rig.Charge=1;break;case "cast":rig.PlayCast();break;case "hit":rig.PlayHit();break;case "down":rig.SetDead(true);break;default:if(action!="idle")rig.PlayAttack(action,.35f);break;}
                        float[] minimum={float.MaxValue,float.MaxValue};const int samples=72;
                        for(int frame=0;frame<samples;frame++)
                        {
                            Animate(rig,1/60f);
                            for(int hand=0;hand<2;hand++)minimum[hand]=Mathf.Min(minimum[hand],SignedDistance(spine.InverseTransformPoint(hands[hand].position),vertices,indices,236));
                        }
                        for(int hand=0;hand<2;hand++){result.Add(new Row{id=id,action=action,side=hand==0?"L":"R",samples=samples,minimumSignedDistance=minimum[hand]});if(minimum[hand]<=0)failures++;}
                    }
                    UnityEngine.Object.DestroyImmediate(rig.gameObject);
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
            return new Report{rows=result.ToArray(),failures=failures};
        }
        static float SignedDistance(Vector3 p,Vector3[] v,int[] triangles,int triangleCount)
        {
            float minimum=float.MaxValue;var hits=new List<float>();var direction=new Vector3(.936f,.137f,.322f).normalized;
            for(int t=0;t<Mathf.Min(triangleCount*3,triangles.Length);t+=3)
            {
                Vector3 a=v[triangles[t]],b=v[triangles[t+1]],c=v[triangles[t+2]];
                minimum=Mathf.Min(minimum,Vector3.Distance(p,Closest(p,a,b,c)));
                var e1=b-a;var e2=c-a;var h=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-8f)continue;
                var s=p-a;float u=Vector3.Dot(s,h)/det;if(u<0||u>1)continue;var q=Vector3.Cross(s,e1);float w=Vector3.Dot(direction,q)/det;if(w<0||u+w>1)continue;
                float distance=Vector3.Dot(e2,q)/det;if(distance>1e-6f)hits.Add(distance);
            }
            hits.Sort();int distinct=0;float last=-1;foreach(float h in hits)if(h-last>1e-5f){distinct++;last=h;}
            return (distinct%2==0?1:-1)*minimum;
        }
        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float inv=1/(va+vb+vc);return a+ab*(vb*inv)+ac*(vc*inv);
        }
    }
}
#endif
