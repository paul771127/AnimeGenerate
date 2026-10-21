using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public partial class BossRig
    {
        static readonly Dictionary<string,Mesh> referenceMeshes=new Dictionary<string,Mesh>();
        // Only render attachments change. Every original pivot and its animation is retained.
        void BuildReferenceAppearance()
        {
            foreach(var f in body.GetComponentsInChildren<MeshFilter>(true))
            {
                if(f.transform==plateL||f.transform==plateR||Under(f.transform,core))continue;
                f.gameObject.SetActive(false);
            }
            if(Theme==0)ReferenceGolem();else if(Theme==1)ReferenceTree();else ReferenceKraken();
        }
        static bool Under(Transform t,Transform root){while(t!=null){if(t==root)return true;t=t.parent;}return false;}
        Mesh ReferenceMesh(string key,System.Action<OutfitArt.Builder> make)
        {
            key=Theme+"/"+key;if(referenceMeshes.TryGetValue(key,out var mesh))return mesh;
            var b=new OutfitArt.Builder();make(b);mesh=b.Finish("Reference "+key);referenceMeshes[key]=mesh;return mesh;
        }
        GameObject ReferencePart(Transform p,string key,Material mat,System.Action<OutfitArt.Builder> make,Vector3 at=default(Vector3))
            =>MeshPart(p,ReferenceMesh(key,make),mat,at,Vector3.zero,Vector3.one);
        void ReferenceLine(Transform p,string key,Material mat,Vector3[] path,float radius)
        {
            if(Theme==2&&(key.StartsWith("mantle rune")||key.StartsWith("mantle sigil")||key.StartsWith("eye brow")))
                for(int i=0;i<path.Length;i++)path[i]=KrakenSurface(path[i].x,path[i].y);
            ReferencePart(p,key,mat,b=>Sweep(b,path,radius));
        }
        static Vector3 KrakenSurface(float x,float y)
        {
            float[] ys={1.45f,1.25f,.85f,.35f,-.15f,-.55f,-.70f},rx={.03f,.34f,.76f,.98f,.86f,.52f,.26f},rz={.04f,.31f,.58f,.69f,.64f,.42f,.24f},zs={-.26f,-.16f,-.06f,.01f,.06f,.01f,0};
            for(int i=0;i<ys.Length-1;i++)if(y<=ys[i]&&y>=ys[i+1]){float t=(ys[i]-y)/(ys[i]-ys[i+1]),r=Mathf.Lerp(rx[i],rx[i+1],t),d=Mathf.Lerp(rz[i],rz[i+1],t),z=Mathf.Lerp(zs[i],zs[i+1],t);return new Vector3(x,y,z+d*Mathf.Sqrt(Mathf.Max(0,1-x*x/(r*r)))+.018f);}
            return new Vector3(x,y,.1f);
        }
        static void Sweep(OutfitArt.Builder b,Vector3[] path,float radius)
        {
            int start=b.V.Count,n=8;
            for(int j=0;j<path.Length;j++)
            {
                var tangent=(path[Mathf.Min(j+1,path.Length-1)]-path[Mathf.Max(0,j-1)]).normalized;
                var side=Vector3.Cross(tangent,Mathf.Abs(tangent.y)<.9f?Vector3.up:Vector3.forward).normalized;var cross=Vector3.Cross(tangent,side).normalized;
                float r=radius*(1-j/(float)(path.Length-1)*.82f);
                for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n;b.V.Add(path[j]+(side*Mathf.Cos(a)+cross*Mathf.Sin(a))*r);b.C.Add(Color.white);if(j<path.Length-1){int k=start+j*n+i,k1=start+j*n+(i+1)%n;b.T.AddRange(new[]{k,k1,k+n,k1,k1+n,k+n});}}
            }
            for(int i=1;i<n-1;i++){b.T.AddRange(new[]{start,start+i+1,start+i});int e=start+(path.Length-1)*n;b.T.AddRange(new[]{e,e+i,e+i+1});}
        }
        void ReplaceReferencePlate(Transform plate,string key)
        {
            var filter=plate.GetComponent<MeshFilter>();if(filter==null)return;
            // Mesh-local dimensions preserve the original opening displacement and rest transform.
            filter.sharedMesh=ReferenceMesh(key,b=>b.Loft(new[]{.48f,.25f,-.25f,-.48f},new[]{.25f,.48f,.48f,.16f},new[]{.18f,.42f,.42f,.18f},Color.white,0,0,8));
        }
        void ReferenceGolem()
        {
            ReplaceReferencePlate(plateL,"breastplateL");ReplaceReferencePlate(plateR,"breastplateR");
            ReferencePart(body,"pelvis",rockA,b=>b.Loft(new[]{1.43f,1.26f,1.06f,.93f},new[]{.32f,.43f,.34f,.22f},new[]{.24f,.30f,.25f,.19f},Color.white,0,0,8));
            ReferencePart(spine,"rib armour",rockA,b=>b.Loft(new[]{1.48f,1.30f,1.02f,.72f,.43f,.18f},new[]{.25f,.60f,.70f,.53f,.35f,.30f},new[]{.22f,.35f,.42f,.36f,.25f,.23f},Color.white,0,0,10));
            for(int i=0;i<3;i++)ReferencePart(spine,"abdominal plate"+i,rockB,b=>b.Loft(new[]{.64f-i*.18f,.53f-i*.18f,.47f-i*.18f},new[]{.30f,.34f,.22f},new[]{.10f,.13f,.07f},Color.white,0,.27f,8));
            ReferencePart(neck,"molten neck",lavaM,b=>b.Loft(new[]{.21f,.06f,-.18f},new[]{.12f,.17f,.23f},new[]{.12f,.15f,.20f},Color.white,0,0,10));
            ReferencePart(head,"faceless pointed helm",rockC,b=>b.Loft(new[]{.38f,.26f,.05f,-.08f},new[]{.025f,.16f,.17f,.055f},new[]{.025f,.14f,.17f,.05f},Color.white,0,-.025f,8));
            for(int s=-1;s<=1;s+=2)
            {
                Transform hip=s<0?hipL:hipR,knee=s<0?knL:knR,ankle=s<0?anL:anR,shoulder=s<0?shL:shR,elbow=s<0?elL:elR,wrist=s<0?wrL:wrR;
                ReferencePart(hip,"armoured thigh"+s,rockA,b=>b.Loft(new[]{.10f,-.05f,-.25f,-.48f,-.56f},new[]{.18f,.25f,.26f,.17f,.14f},new[]{.19f,.24f,.25f,.17f,.14f},Color.white,0,0,8));
                ReferencePart(knee,"armoured shin"+s,rockC,b=>b.Loft(new[]{.10f,-.04f,-.18f,-.36f,-.45f},new[]{.14f,.21f,.18f,.10f,.13f},new[]{.16f,.24f,.18f,.11f,.14f},Color.white,0,.03f,8));
                ReferencePart(ankle,"splayed boot"+s,rockA,b=>b.Loft(new[]{.14f,.02f,-.08f},new[]{.12f,.22f,.23f},new[]{.15f,.32f,.34f},Color.white,0,.10f,8));
                ReferencePart(shoulder,"angular shoulder"+s,rockC,b=>b.Loft(new[]{.23f,.14f,-.12f,-.28f},new[]{.11f,.34f,.34f,.22f},new[]{.12f,.34f,.35f,.24f},Color.white,0,-.06f,8));
                ReferencePart(shoulder,"upper armour"+s,rockA,b=>b.Loft(new[]{-.16f,-.33f,-.62f,-.80f},new[]{.20f,.25f,.22f,.14f},new[]{.22f,.25f,.21f,.15f},Color.white,0,0,8));
                ReferencePart(elbow,"blade vambrace"+s,rockB,b=>b.Loft(new[]{.06f,-.18f,-.42f,-.76f,-.94f},new[]{.12f,.23f,.28f,.15f,.13f},new[]{.14f,.23f,.28f,.17f,.13f},Color.white,0,.015f,8));
                ReferencePart(wrist,"hand hull"+s,rockA,b=>b.Loft(new[]{.02f,-.13f,-.28f},new[]{.13f,.18f,.12f},new[]{.12f,.14f,.11f},Color.white,0,.02f,8));
                for(int f=0;f<4;f++){int digit=f;ReferenceLine(wrist,"finger"+s+f,rockC,new[]{new Vector3(-.12f+digit*.08f,-.21f,.05f),new Vector3(-.12f+digit*.08f,-.36f,.08f),new Vector3(-.12f+digit*.08f,-.41f,.15f)},.041f);}
                ReferenceLine(wrist,"thumb"+s,rockC,new[]{new Vector3(-s*.13f,-.08f,.02f),new Vector3(-s*.23f,-.17f,.10f),new Vector3(-s*.18f,-.27f,.17f)},.052f);
                ReferenceLine(spine,"molten ribs"+s,lavaM,new[]{new Vector3(s*.19f,1.40f,.27f),new Vector3(s*.52f,1.21f,.35f),new Vector3(s*.57f,.99f,.40f),new Vector3(s*.32f,.65f,.34f),new Vector3(s*.22f,.27f,.24f)},.032f);
                ReferenceLine(shoulder,"molten bicep"+s,lavaM,new[]{new Vector3(s*.13f,-.29f,.21f),new Vector3(s*.15f,-.55f,.21f),new Vector3(0,-.79f,.15f)},.033f);
                ReferenceLine(elbow,"molten forearm"+s,lavaM,new[]{new Vector3(0,-.06f,.15f),new Vector3(s*.13f,-.40f,.27f),new Vector3(0,-.82f,.15f)},.032f);
                ReferenceLine(hip,"molten leg"+s,lavaM,new[]{new Vector3(0,-.03f,.24f),new Vector3(s*.13f,-.27f,.23f),new Vector3(0,-.51f,.17f)},.029f);
            }
        }
        void ReferenceTree()
        {
            ReplaceReferencePlate(plateL,"bark doorL");ReplaceReferencePlate(plateR,"bark doorR");
            ReferencePart(spine,"twisted trunk",rockA,b=>b.Tube(new[]{new Vector3(.08f,1.25f,-.04f),new Vector3(-.12f,.95f,0),new Vector3(.06f,.50f,.03f),new Vector3(-.07f,0,0),new Vector3(0,-.55f,0)},new[]{.18f,.31f,.42f,.38f,.49f},new[]{.18f,.28f,.35f,.34f,.40f},Color.white,12));
            for(int s=-1;s<=1;s+=2)
            {
                Transform hip=s<0?hipL:hipR,knee=s<0?knL:knR,ankle=s<0?anL:anR;
                ReferenceLine(hip,"twisted root leg"+s,rockA,new[]{new Vector3(0,.17f,0),new Vector3(s*.05f,-.12f,.02f),new Vector3(-s*.06f,-.52f,.04f)},.25f);
                ReferenceLine(knee,"root shin"+s,rockB,new[]{new Vector3(0,.05f,0),new Vector3(-s*.06f,-.22f,.04f),new Vector3(s*.04f,-.47f,.06f)},.19f);
                for(int i=0;i<4;i++){float a=i*1.35f;ReferenceLine(ankle,"spreading root"+s+i,rockC,new[]{new Vector3(0,.14f,0),new Vector3(Mathf.Sin(a)*.22f,.02f,Mathf.Cos(a)*.25f),new Vector3(Mathf.Sin(a)*.58f,-.08f,Mathf.Cos(a)*.62f)},.16f);}
                for(int i=0;i<4;i++)
                {
                    float h=.12f+i*.15f,x=s*(.80f+i*.15f),z=-.16f+i*.12f;
                    ReferenceLine(head,"bare major branch"+s+i,rockA,new[]{new Vector3(s*.05f,-.45f,0),new Vector3(s*.30f,-.11f,z),new Vector3(s*.62f,.02f+i*.06f,z),new Vector3(x,h,z),new Vector3(x*1.20f,h+.30f,z-.07f)},.16f-i*.015f);
                    ReferenceLine(head,"bare twig"+s+i,rockB,new[]{new Vector3(s*.55f,-.03f+i*.06f,z),new Vector3(s*.62f,.25f+i*.09f,z+.10f),new Vector3(s*.59f,.50f+i*.07f,z+.14f)},.062f);
                    Part(PrimitiveType.Sphere,head,new Vector3(x*.87f,h+.03f,z),Vector3.one*(.13f+(i%2)*.035f),lavaM);
                }
                ReferenceLine(head,"central fork"+s,rockB,new[]{new Vector3(0,-.14f,-.06f),new Vector3(s*.19f,.30f,-.05f),new Vector3(s*.26f,.79f,-.04f)},.14f);
                Part(PrimitiveType.Sphere,ankle,new Vector3(s*.25f,.05f,.18f),Vector3.one*.18f,lavaM);
            }
            for(int i=0;i<10;i++){float a=i*Mathf.PI/5;ReferenceLine(core,"heart fissure"+i,lavaM,new[]{new Vector3(Mathf.Sin(a)*.15f,Mathf.Cos(a)*.15f,.268f),new Vector3(Mathf.Sin(a+.1f)*.25f,Mathf.Cos(a+.1f)*.25f,.265f),new Vector3(Mathf.Sin(a)*.34f,Mathf.Cos(a)*.34f,.255f)},.013f);}
            for(int side=-1;side<=1;side+=2)
                ReferenceLine(body,"buttress root"+side,rockA,new[]{new Vector3(side*.09f,1.28f,-.08f),new Vector3(side*.30f,.65f,.04f),new Vector3(side*.45f,.18f,.16f),new Vector3(side*.82f,.01f,.38f)},.26f);
            for(int i=0;i<5;i++){float x=-.23f+i*.115f;ReferenceLine(spine,"twisted bark ridge"+i,rockB,new[]{new Vector3(x,-.36f,.29f),new Vector3(x+.08f,.10f,.34f),new Vector3(x-.04f,.56f,.34f),new Vector3(x,.97f,.24f)},.036f);}
            // Dark jagged cavity surrounds the existing animated crystal, no humanoid face.
            ReferencePart(core,"jagged cavity",crystalM,b=>
            {
                int start=b.V.Count;b.V.Add(new Vector3(0,0,.26f));b.C.Add(Color.white);
                for(int i=0;i<24;i++){float a=i*Mathf.PI/12,r=i%2==0?.34f:.22f;b.V.Add(new Vector3(Mathf.Sin(a)*r,Mathf.Cos(a)*r,.25f));b.C.Add(Color.white);b.T.AddRange(new[]{start,start+1+(i+1)%24,start+1+i});}
            });
        }
        void ReferenceKraken()
        {
            // Keep all eight original skinned tentacles and their bones. Only their surface profile changes.
            Mesh swollen=null;
            foreach(var sr in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(swollen==null)
                {
                    const string key="reference.kraken.tentacle";
                    if(!meshCache.TryGetValue(key,out swollen))
                    {
                        swollen=Instantiate(sr.sharedMesh);swollen.name="Broad coiled reference tentacle";
                        var vertices=swollen.vertices;float length=TentBones*TentSeg+TentTip;
                        for(int i=0;i<vertices.Length;i++){var v=vertices[i];float t=Mathf.Clamp01(v.z/length);v.x*=1.9f;v.y=v.y*1.9f+Mathf.Sin(t*Mathf.PI)*.60f;vertices[i]=v;}
                        swollen.vertices=vertices;swollen.RecalculateNormals();swollen.RecalculateBounds();meshCache[key]=swollen;
                    }
                }
                sr.sharedMesh=swollen;
            }
            ReplaceReferencePlate(plateL,"mantle shellL");ReplaceReferencePlate(plateR,"mantle shellR");
            plateL.GetComponent<MeshRenderer>().sharedMaterial=rockA;plateR.GetComponent<MeshRenderer>().sharedMaterial=rockA;
            ReferencePart(mantle,"great octopus mantle",rockA,b=>b.Tube(new[]{new Vector3(0,1.45f,-.26f),new Vector3(0,1.25f,-.16f),new Vector3(0,.85f,-.06f),new Vector3(0,.35f,.01f),new Vector3(0,-.15f,.06f),new Vector3(0,-.55f,.01f),new Vector3(0,-.70f,0)},new[]{.03f,.34f,.76f,.98f,.86f,.52f,.26f},new[]{.04f,.31f,.58f,.69f,.64f,.42f,.24f},Color.white,20));
            ReferencePart(spine,"octopus skirt",rockA,b=>b.Loft(new[]{.92f,.62f,.28f,-.04f,-.37f},new[]{.26f,.43f,.62f,.78f,.56f},new[]{.24f,.38f,.45f,.44f,.32f},Color.white,0,0,16));
            var newEyes=new List<Transform>();
            for(int s=-1;s<=1;s+=2)
            {
                ReferenceLine(mantle,"eye brow"+s,rockC,new[]{new Vector3(s*.18f,-.37f,.43f),new Vector3(s*.35f,-.32f,.45f),new Vector3(s*.48f,-.22f,.37f)},.055f);
                var eye=Part(PrimitiveType.Sphere,mantle,KrakenSurface(s*.32f,-.40f),new Vector3(.18f,.13f,.06f),eyeM,new Vector3(0,s*15,15*s));newEyes.Add(eye.transform);
                ReferenceLine(mantle,"mantle rune"+s,lavaM,new[]{new Vector3(s*.14f,.93f,.20f),new Vector3(s*.32f,.81f,.35f),new Vector3(s*.22f,.67f,.46f),new Vector3(s*.39f,.55f,.48f),new Vector3(s*.28f,.39f,.55f)},.017f);
                ReferenceLine(mantle,"mantle sigil"+s,lavaM,new[]{new Vector3(s*.51f,.21f,.43f),new Vector3(s*.62f,.29f,.34f),new Vector3(s*.61f,.08f,.37f),new Vector3(s*.49f,.01f,.46f),new Vector3(s*.51f,.21f,.43f)},.016f);
            }
            eyes=newEyes.ToArray();eyeBase=new Vector3[eyes.Length];for(int i=0;i<eyes.Length;i++)eyeBase[i]=eyes[i].localScale;
        }
    }
}
