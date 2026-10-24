using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    // Presentation assets only. All meshes/materials use SpellFx's existing caches and tools.
    public static partial class SpellFx
    {
        internal static readonly Color ContractGreen=new Color(.29f,1f,.61f,.8f),HolyGold=new Color(1f,.80f,.28f,.8f);
        internal static void StrokeMesh(PMesh m,Vector3 a,Vector3 b,float width,Color c,Vector3 normal)
        { var side=Vector3.Cross(b-a,normal).normalized*width; m.Ribbon(new[]{a,b},side,new[]{c,c}); }
        internal static Vector3 FloorPoint(float angle,float radius,float y=.025f)=>new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
        internal static void Snow(Transform parent)
        {
            var ps=NewPS(parent,"Six point snow",BlendMat(TexStar),16,true);var m=ps.main;
            m.startLifetime=.8f;m.startSpeed=.06f;m.startSize=new ParticleSystem.MinMaxCurve(.018f,.035f);m.startColor=new Color(.72f,.94f,1f,.65f);
            Sphere(ps,.22f);FadeOut(ps);Rate(ps,7);ps.Play();
        }
        internal static void FairySprinkle(Vector3 at)
        {
            var a=NewAnim("Fairy healing crosses",at,Quaternion.identity,.8f);
            for(int i=0;i<4;i++)
            {
                var t=NewChild(a.transform,"Healing cross");t.localPosition=new Vector3((i-1.5f)*.08f,-.05f-i*.035f,0);
                var bar=new[]{new Vector3(-.035f,0,0),new Vector3(.035f,0,0)};
                a.AddSlash(t,bar,.018f,new Color(.83f,1f,.73f,.8f),i*.035f,.55f,.8f,1.3f,0);
                a.AddSlash(t,bar,.018f,new Color(1f,.77f,.92f,.8f),i*.035f,.55f,.8f,1.3f,90);
            }
        }
        internal static void ContractSeal(Transform root,float height)
        {
            var seal=NewChild(root,"Contract orbit");
            var ring=Line(seal,"Contract circle",Circle(40,.5f),true,.018f,ContractGreen,false,true);
            ring.transform.localRotation=Quaternion.Euler(90,0,0);ring.transform.localPosition=Vector3.up*.03f;
            GlowPart(seal,"contract.runes.v1",m=>{
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;var c=FloorPoint(a,.40f);var radial=FloorPoint(a,1,0);var tangent=Vector3.Cross(Vector3.up,radial);
                    StrokeMesh(m,c-radial*.035f,c+radial*.035f,.010f,ContractGreen,Vector3.up);
                    StrokeMesh(m,c,c+tangent*.035f+radial*.025f,.008f,ContractGreen,Vector3.up);
                }
            });
            var paw=NewChild(root,"Contract paw");paw.localPosition=Vector3.up*height;
            GlowPart(paw,"contract.paw.v1",m=>{
                m.Lathe(SphereProf(.055f,4),8,(z,j)=>ContractGreen);
            });
            // Four toes, all batched into one additional mesh.
            GlowPart(paw,"contract.toes.v1",m=>{
                for(int i=0;i<4;i++)m.Lathe(SphereProf(.025f,4),6,(z,j)=>ContractGreen,1,1,0,null,new Vector3((i-1.5f)*.037f,.067f-Mathf.Abs(i-1.5f)*.012f,0),Quaternion.identity);
            });
            var motion=root.gameObject.AddComponent<ContractSealView>();motion.orbit=seal;motion.paw=paw;
        }
        internal static void ContractSigned(Vector3 at)
        {
            var a=NewAnim("Contract signed",at,Quaternion.identity,.9f);
            var line=new[]{Vector3.zero,Vector3.up*2.2f};
            a.AddSlash(a.transform,line,.12f,new Color(.45f,1f,.65f,.55f),0,.55f,1,1,0);
            var floor=NewChild(a.transform,"Seal burst");floor.localRotation=Quaternion.Euler(90,0,0);
            a.AddRing(floor,Circle(32,1),true,.03f,ContractGreen,0,.65f,.12f,.8f,false);
            Sparks(a.transform,new Color(.65f,1f,.71f),.55f,10,.2f,.7f);
        }
        internal static void ShadowDissolve(Vector3 at)
        {
            var a=NewAnim("Shadow dispersal",at+Vector3.up*.65f,Quaternion.identity,.85f);
            var ps=NewPS(a.transform,"Shadow fragments",BlendMat(TexLeaf),18,true);var m=ps.main;
            m.startColor=new Color(.35f,.12f,.55f,.6f);m.startSize=new ParticleSystem.MinMaxCurve(.04f,.13f);m.startLifetime=.65f;
            m.startSpeed=.6f;m.gravityModifier=-.12f;Sphere(ps,.25f);FadeOut(ps);ps.Emit(16);
        }
        internal static void HolyLanding(Vector3 at,bool heaven)
        {
            var a=NewAnim("Holy descent",at,Quaternion.identity,.8f);float s=heaven?1.7f:1f;
            var line=new[]{Vector3.up*7,Vector3.up*.08f};
            a.AddSlash(a.transform,line,.42f*s,new Color(1f,.81f,.30f,.4f),0,.38f,1,1,0);
            a.AddSlash(a.transform,line,.10f*s,new Color(1f,.98f,.82f,.85f),0,.25f,1,1,0);
            var floor=NewChild(a.transform,"Landing ring");floor.localRotation=Quaternion.Euler(90,0,0);
            a.AddRing(floor,Circle(36,1),true,.035f,HolyGold,0,.55f,.1f,s,false);
        }
        internal static void HolyNeedle(Vector3 at)
        {
            var a=NewAnim("Holy needle hit",at,Quaternion.identity,.3f);
            a.AddSlash(a.transform,new[]{Vector3.up*1.5f,Vector3.zero},.026f,new Color(1f,.96f,.72f,.85f),0,.18f,1,1,0);
        }
        internal static GameObject HolyGround(float radius,bool heaven)
        {
            var go=new GameObject(heaven?"Heaven punishment seal":"Judgement cross seal");
            var seal=NewChild(go.transform,"Holy seal");seal.localScale=Vector3.one*radius;
            var disk=NewChild(seal,"Ground rings");disk.localRotation=Quaternion.Euler(90,0,0);
            Line(disk,"Edge",Circle(48,1),true,.012f,HolyGold,false,true);
            Line(disk,"Inner",Circle(40,.74f),true,.008f,new Color(1f,.94f,.6f,.7f),false,true);
            GlowPart(seal,"holy.cross.runes.v1",m=>{
                var c=new Color(1f,.88f,.46f,.55f);
                StrokeMesh(m,new Vector3(0,.025f,-.63f),new Vector3(0,.025f,.63f),.038f,c,Vector3.up);
                StrokeMesh(m,new Vector3(-.43f,.025f,.17f),new Vector3(.43f,.025f,.17f),.038f,c,Vector3.up);
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;
                    StrokeMesh(m,FloorPoint(a,.84f),FloorPoint(a,.94f),.010f,c,Vector3.up);
                    var p=FloorPoint(a,.88f);StrokeMesh(m,p,p+FloorPoint(a+.7f,.05f,0),.009f,c,Vector3.up);
                }
            });
            var pillars=NewChild(go.transform,"Holy perimeter");pillars.localScale=Vector3.one*radius;
            GlowPart(pillars,"holy.pillars.v1",m=>{
                for(int i=0;i<12;i++){var p=FloorPoint(i*Mathf.PI/6,.97f);StrokeMesh(m,p,p+Vector3.up*.18f,.014f,new Color(1f,.95f,.68f,.5f),FloorPoint(i*Mathf.PI/6,1,0));}
            });
            if(heaven)
            {
                var feathers=NewPS(go.transform,"Heaven feathers",BlendMat(TexLeaf),18,true);var f=feathers.main;
                f.startColor=new Color(1f,.97f,.82f,.65f);f.startSize=new ParticleSystem.MinMaxCurve(.08f,.16f);f.startLifetime=1.8f;
                f.startSpeed=.05f;f.gravityModifier=.09f;feathers.transform.localPosition=Vector3.up*1.4f;
                Disc(feathers,radius,.15f);FadeOut(feathers);Rate(feathers,7);feathers.Play();
            }
            var view=go.AddComponent<HolyGroundView>();view.seal=seal;view.heaven=heaven;view.Init();return go;
        }
        internal static GameObject BarrierShell(float radius,int charges)
        {
            var go=new GameObject("Honeycomb holy barrier");var hull=NewChild(go.transform,"Honeycomb");hull.localScale=Vector3.one*radius;
            GlowPart(hull,"holy.honeycomb.v1",m=>{
                for(int row=0;row<4;row++)for(int col=0;col<12;col++)
                {
                    float p=(row+.5f)*Mathf.PI/8,a=(col+(row%2)*.5f)*Mathf.PI/6;
                    var center=new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a));
                    var tx=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a));var ty=Vector3.Cross(center,tx);
                    for(int k=0;k<6;k++)
                    {
                        float aa=k*Mathf.PI/3,bb=(k+1)*Mathf.PI/3;
                        var x=(center+(tx*Mathf.Cos(aa)+ty*Mathf.Sin(aa))*.16f).normalized;
                        var y=(center+(tx*Mathf.Cos(bb)+ty*Mathf.Sin(bb))*.16f).normalized;
                        x.y*=.72f;y.y*=.72f;
                        StrokeMesh(m,x,y,.0055f,new Color(1f,.87f,.49f,.26f),center);
                    }
                }
            });
            var view=go.AddComponent<BarrierArtView>();view.rings=new LineRenderer[charges];view.LastCharges=charges;
            for(int i=0;i<charges;i++)
            {
                var r=Line(go.transform,"Charge ring "+i,Circle(48,radius*(1-.065f*i)),true,.026f,HolyGold,false,true);
                r.transform.localRotation=Quaternion.Euler(90,0,0);r.transform.localPosition=Vector3.up*(.025f+.018f*i);view.rings[i]=r;
            }
            view.Init();return go;
        }
        internal static void BarrierRipple(Vector3 at)
        {
            var cam=Camera.main;var rot=cam?Quaternion.LookRotation(cam.transform.position-at):Quaternion.identity;
            var a=NewAnim("Barrier hit ripple",at,rot,.5f);
            a.AddRing(a.transform,Polygon(6,1,30),true,.025f,new Color(1f,.94f,.7f,.9f),0,.35f,.04f,.65f,false);
            a.AddRing(a.transform,Circle(32,1),true,.012f,HolyGold,.045f,.4f,.05f,.85f,false);
        }
        internal static void BarrierShatter(Vector3 at,float radius)
        {
            var a=NewAnim("Barrier glass shatter",at,Quaternion.identity,.95f);
            for(int i=0;i<14;i++)
            {
                float angle=i*Mathf.PI*2/14;var part=NewChild(a.transform,"Glass shard");
                part.localPosition=FloorPoint(angle,radius*.70f,.25f+(i%4)*radius*.17f);
                BlendPart(part,"holy.glass."+(i%3),m=>m.Tri(new Vector3(-.10f,-.08f,0),new Vector3(.14f,-.03f,0),new Vector3(0,.20f,0),new Color(1f,.88f,.53f,.65f)));
                var shard=part.gameObject.AddComponent<ReworkGlassShard>();shard.velocity=new Vector3(Mathf.Cos(angle)*.9f,.4f,Mathf.Sin(angle)*.9f);shard.spin=new Vector3(63+i*7,74,32);
            }
        }
        internal static void BuildKunai(Transform parent,string id)
        {
            SolidPart(parent,"kunai.solid.v1",m=>{
                m.sub=0;m.Lathe(Prof(-.1f,0,.1f,.30f,.92f,0),4,null,1,.25f,45);
                m.sub=1;m.Lathe(Prof(-.72f,.065f,-.08f,.065f),8,null);
                m.sub=0;m.Annulus(.12f,.18f,12,Color.white,Color.white,new Vector3(0,0,-.86f),Quaternion.Euler(90,0,0));
                m.sub=2;m.Tri(new Vector3(.07f,0,-.65f),new Vector3(.18f,0,-1.15f),new Vector3(.32f,.04f,-1.04f),Color.white);
                m.Tri(new Vector3(.32f,.04f,-1.04f),new Vector3(.18f,0,-1.15f),new Vector3(.07f,0,-.65f),Color.white);
            },new[]{new Color(.83f,.90f,.96f),new Color(.26f,.31f,.42f),new Color(.91f,.16f,.27f)});
            if(id=="kunai_fire"||id=="kunai_ice")
            {
                bool fire=id=="kunai_fire";var ps=NewPS(parent,fire?"Kunai flame":"Kunai frost",fire?AddMat(TexSoft):BlendMat(TexStar),14,true);
                var p=ps.main;p.startLifetime=.22f;p.startSpeed=.12f;p.startSize=new ParticleSystem.MinMaxCurve(.035f,.10f);
                p.startColor=fire?new Color(1f,.40f,.08f,.8f):new Color(.62f,.90f,1f,.8f);Sphere(ps,.10f);FadeOut(ps);Rate(ps,24);ps.Play();
                if(!fire)GlowPart(parent,"kunai.frost.v1",m=>m.Lathe(Prof(.1f,.32f,.75f,0),6,(z,j)=>new Color(.45f,.83f,1f,.38f),1,.32f));
            }
            if(id=="kunai_bolt")
            {
                var bolt=NewChild(parent,"Kunai lightning");
                Line(bolt,"Yellow arc",new[]{new Vector3(-.12f,0,-.6f),new Vector3(.10f,.04f,-.34f),new Vector3(-.10f,-.02f,-.05f),new Vector3(.12f,.02f,.22f),new Vector3(0,0,.75f)},false,.025f,new Color(1f,.87f,.12f,.9f),true,true);
                bolt.gameObject.AddComponent<ReworkArcFlicker>();
            }
        }
        internal static void BuildShuriken(Transform parent)
        {
            SolidPart(parent,"shuriken.v1",m=>{
                for(int i=0;i<4;i++)
                {
                    float a=i*Mathf.PI*.5f;var tip=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.80f;
                    var side=new Vector3(-Mathf.Sin(a),Mathf.Cos(a),0)*.15f;
                    var hub=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.13f;
                    m.Tri(hub-side,tip,hub+side,new Color(.80f,.89f,.97f));
                    m.Tri(hub+side,tip,hub-side,new Color(.53f,.67f,.80f));
                }
                m.Annulus(.07f,.19f,12,Color.white,Color.white,Vector3.zero,Quaternion.identity);
            },new[]{new Color(.84f,.92f,1f)});
        }
    }
    internal sealed class ContractSealView:MonoBehaviour
    {
        internal Transform orbit,paw;
        void LateUpdate(){if(orbit)orbit.localRotation=Quaternion.Euler(0,Time.time*14,0);if(paw&&Camera.main)paw.rotation=Camera.main.transform.rotation;}
    }
    internal class ReworkFadeView:MonoBehaviour
    {
        LineRenderer[] lines;Color[] colors;MeshRenderer[] meshes;MaterialPropertyBlock block;
        internal void Init(){meshes=GetComponentsInChildren<MeshRenderer>();block=new MaterialPropertyBlock();lines=GetComponentsInChildren<LineRenderer>();colors=new Color[lines.Length];for(int i=0;i<lines.Length;i++)colors[i]=lines[i].startColor;}
        internal void Fade(float k){if(lines==null)Init();var tint=new Color(1,1,1,k);block.SetColor(Shader.PropertyToID("_Color"),tint);block.SetColor(Shader.PropertyToID("_TintColor"),tint);foreach(var mesh in meshes)mesh.SetPropertyBlock(block);for(int i=0;i<lines.Length;i++){var c=colors[i];c.a*=k;lines[i].startColor=c;lines[i].endColor=c;}}
    }
    internal sealed class HolyGroundView:ReworkFadeView
    {
        internal Transform seal;internal bool heaven;
        internal void Tick(float fade){if(seal)seal.localRotation=Quaternion.Euler(0,Time.time*(heaven?10:6),0);Fade(fade);}
    }
    internal sealed class BarrierArtView:ReworkFadeView
    {
        internal LineRenderer[] rings;internal int LastCharges;internal bool shattered;
        internal void Tick(int charges,float fade){Fade(fade);for(int i=0;i<rings.Length;i++)rings[i].gameObject.SetActive(i<charges);LastCharges=charges;}
    }
    internal sealed class ReworkGlassShard:MonoBehaviour
    {
        internal Vector3 velocity,spin;
        void Update(){float dt=Time.deltaTime;velocity.y-=dt*.8f;transform.localPosition+=velocity*dt;transform.Rotate(spin*dt,Space.Self);}
    }
    internal sealed class ReworkArcFlicker:MonoBehaviour
    {void Update(){float p=.8f+.2f*Mathf.Sin(Time.time*47);transform.localScale=new Vector3(p,1/p,1);}}
}
