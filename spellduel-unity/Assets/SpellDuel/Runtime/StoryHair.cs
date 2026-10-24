using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Volumetric, closed, cubic-swept locks. NPCs only; the first 121 vertices remain the fitted hair shell.
    internal static class StoryHair
    {
        internal static bool Has(string id)=>StoryCostume.Has(id)||id=="澪";
        static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float s=1-t;return a*(s*s*s)+b*(3*s*s*t)+c*(3*s*t*t)+d*(t*t*t);}
        static float originalTop;
        static void Lock(OutfitArt.Builder m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float width,float depth,float tone=1,float taperPower=1.10f)
        {
            for(int j=0;j<=10;j++)
            {
                float t=j/10f;var at=Curve(a,b,c,d,t);var direction=(Curve(a,b,c,d,Mathf.Min(1,t+.008f))-Curve(a,b,c,d,Mathf.Max(0,t-.008f))).normalized;
                var right=Vector3.Cross(direction,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;var normal=Vector3.Cross(right,direction).normalized;
                float taper=Mathf.Pow(1-t,.48f)*(.76f+.24f*Mathf.Sin(t*Mathf.PI))+.004f;
                for(int i=0;i<8;i++){float q=i*Mathf.PI/4;originalTop=Mathf.Max(originalTop,(at+right*(Mathf.Cos(q)*width*taper)+normal*(Mathf.Sin(q)*depth*taper)).y);}
            }
            const int rows=8,sides=6;
            for(int strand=-1;strand<=1;strand+=2)
            {
                int start=m.V.Count;
                for(int j=0;j<rows;j++)
                {
                    float t=j/(float)rows;var at=Curve(a,b,c,d,t);var direction=(Curve(a,b,c,d,Mathf.Min(1,t+.008f))-Curve(a,b,c,d,Mathf.Max(0,t-.008f))).normalized;
                    var right=Vector3.Cross(direction,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;var normal=Vector3.Cross(right,direction).normalized;
                    float taper=Mathf.Pow(1-t,taperPower)*(.76f+.24f*Mathf.Sin(t*Mathf.PI));
                    at+=right*(strand*width*.5f*taper)+Vector3.right*(strand*width*.18f*t*t);
                    for(int i=0;i<sides;i++)
                    {
                        float q=i*2*Mathf.PI/sides;var n=right*Mathf.Cos(q)+normal*Mathf.Sin(q);
                        m.V.Add(at+right*(Mathf.Cos(q)*width*.5f*taper)+normal*(Mathf.Sin(q)*depth*.80f*taper));
                        float shade=tone*(n.z<-.2f?.52f:n.y>.15f?1.07f:.86f)*(strand<0?.97f:1);m.C.Add(new Color(shade,shade,shade,1));
                        if(j<rows-1){int k=start+j*sides+i,nn=start+j*sides+(i+1)%sides;m.T.AddRange(new[]{k,k+sides,nn,nn,k+sides,nn+sides});}
                    }
                }
                int tip=m.V.Count;m.V.Add(d+Vector3.right*(strand*width*.18f));m.C.Add(new Color(tone*.80f,tone*.80f,tone*.80f,1));
                for(int i=0;i<sides;i++){int k=start+(rows-1)*sides+i,nn=start+(rows-1)*sides+(i+1)%sides;m.T.AddRange(new[]{k,tip,nn});}
                for(int i=1;i<sides-1;i++)m.T.AddRange(new[]{start,start+i,start+i+1});
            }
        }
        static void FilmLock(OutfitArt.Builder m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float width,float depth,float tone=1)
        {Lock(m,a,b,c,d,width,depth,tone,.65f);}
        static void FitRearLocks(List<Vector3> vertices,Portraits.Persona p)
        {
            // Preserve every six-vertex tube ring: translate the ring rather than flattening individual vertices.
            for(int i=0;i<121;i++){var v=vertices[i];if(v.z<0){v.z=Mathf.Min(v.z,AnimeFace.HairBackSurface(v.x,v.y,p)-.005f);vertices[i]=v;}}
            for(int start=121;start+48<vertices.Count;start+=49)
            {
                for(int ring=0;ring<8;ring++)
                {
                    int at=start+ring*6;float z=0,push=0;for(int k=0;k<6;k++)z+=vertices[at+k].z;
                    if(z>=-.06f)continue;
                    for(int k=0;k<6;k++){var v=vertices[at+k];push=Mathf.Min(push,AnimeFace.HairBackSurface(v.x,v.y,p)-.006f-v.z);}
                    for(int k=0;k<6;k++){var v=vertices[at+k];v.z+=push;vertices[at+k]=v;}
                }
                var tip=vertices[start+48];if(tip.z<0){tip.z=Mathf.Min(tip.z,AnimeFace.HairBackSurface(tip.x,tip.y,p)-.006f);vertices[start+48]=tip;}
            }
        }
        static readonly Dictionary<string,Mesh> filmHair=new Dictionary<string,Mesh>();
        // Visible film silhouettes are fitted AFTER the legacy sizing pass: body/head scale stays unchanged.
        internal static Mesh FilmMesh(string id,Portraits.Persona p)
        {
            if(id!="艾琳"&&id!="凪"&&id!="海斗"&&id!="老焰"&&id!="翠"&&id!="澪"&&id!="小葉")return AnimeHair.MeshFor(id,p);
            if(filmHair.TryGetValue(id,out var cached))return cached;
            var m=new OutfitArt.Builder();
            bool elder=id=="老焰",male=id=="海斗",longHair=id=="翠"||id=="澪",twins=id=="小葉";
            // Build an un-stretched visible scalp: the legacy fitting shell has a different crown envelope.
            const int sides=20,rows=6;m.V.Add(new Vector3(0,.177f*p.Length,-.009f));m.C.Add(new Color(.90f,.90f,.90f,1));
            for(int j=1;j<=rows;j++)for(int i=0;i<sides;i++)
            {
                float phi=i*2*Mathf.PI/sides,front=Mathf.Cos(phi),theta=j/(float)rows*(front>.3f?(elder?.70f:1.10f):1.90f);
                m.V.Add(new Vector3(Mathf.Sin(theta)*Mathf.Sin(phi)*.158f*p.Width,Mathf.Cos(theta)*.177f*p.Length,Mathf.Sin(theta)*front*(front>0?.171f:.159f)-.009f));
                float tone=front>.3f?.88f:.74f;m.C.Add(new Color(tone,tone,tone,1));
                int n=(i+1)%sides;if(j==1)m.T.AddRange(new[]{0,1+i,1+n});else{int k=1+(j-2)*sides+i,nn=1+(j-2)*sides+n;m.T.AddRange(new[]{k,k+sides,nn,nn,k+sides,nn+sides});}
            }
            float part=id=="艾琳"?-.018f:id=="海斗"?-.030f:id=="澪"?.025f:0;
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6,x=Mathf.Sin(a),z=Mathf.Cos(a);
                FilmLock(m,new Vector3(part+x*.025f,.166f,z*.025f),new Vector3(x*.075f,.192f,z*.085f),new Vector3(x*.132f,.145f,z*.145f),new Vector3(x*.150f,.075f,z*.150f),.021f,.016f,i%3==0?.84f:1);
            }
            if(!elder)for(int i=0;i<7;i++)
            {
                float[] tipsX=id=="艾琳"?new[]{-.090f,-.060f,-.023f,-.006f,.015f,.055f,.087f}:male?new[]{-.085f,-.060f,-.025f,.018f,.038f,.070f,.095f}:id=="澪"?new[]{-.090f,-.060f,-.035f,-.010f,.020f,.055f,.085f}:twins?new[]{-.070f,-.050f,-.025f,0,.025f,.050f,.070f}:new[]{-.090f,-.060f,-.030f,0,.030f,.060f,.090f};
                float[] tipsY=id=="艾琳"?new[]{.040f,.032f,-.012f,-.047f,-.024f,.020f,.034f}:male?new[]{.020f,.010f,-.010f,-.050f,-.035f,.020f,.040f}:id=="澪"?new[]{.018f,-.014f,-.032f,-.038f,-.020f,.015f,.035f}:twins?new[]{.040f,.025f,.015f,-.018f,.015f,.025f,.040f}:id=="凪"?new[]{.025f,.015f,.005f,-.030f,.005f,.015f,.030f}:new[]{.035f,.025f,.010f,-.020f,.010f,.025f,.035f};
                float x=tipsX[i],tip=tipsY[i];
                FilmLock(m,new Vector3(part+x*.20f,.155f,.062f),new Vector3(part+x*.55f,.135f,.157f),new Vector3(x,.065f,.176f),new Vector3(x,tip,AnimeFace.HairSeat(x,tip,p)+.013f),.030f,.014f,i%3==0?.88f:1);
            }
            if(!elder)for(int side=-1;side<=1;side+=2)
                FilmLock(m,new Vector3(part+side*.045f,.15f,.080f),new Vector3(side*.095f,.12f,.150f),new Vector3(side*.125f,.045f,.149f),new Vector3(side*.132f,-.050f,.125f),.029f,.014f,.96f);
            for(int side=-1;side<=1;side+=2)for(int tier=0;tier<3;tier++)
            {
                float end=elder?-.055f:male?-.025f:longHair?-.29f:id=="艾琳"?-.17f:-.145f;
                float bulge=id=="凪"?.182f:male?.135f:.151f,tip=male?.132f:id=="艾琳"?.116f:.140f;
                float z=.126f-tier*.042f;
                FilmLock(m,new Vector3(side*.128f,.125f,z*.5f),new Vector3(side*bulge,.035f,z),new Vector3(side*bulge,end*.78f,z+.012f),new Vector3(side*tip,end+tier*.009f,z+.022f),elder?.012f:.028f,.014f,tier==0?1:.83f);
            }
            for(int i=0;i<10;i++)
            {
                float a=(i/9f-.5f)*2.7f,x=Mathf.Sin(a)*.14f,z=-Mathf.Cos(a)*.151f;
                float len=longHair?CharacterLook.Get(id).Length:male?.035f+(i%3)*.009f:elder?.055f:id=="艾琳"?.185f:.16f;
                float fan=longHair?2.0f:1.04f;
                FilmLock(m,new Vector3(x,.070f,z),new Vector3(x*fan,-len*.25f,z-(male?.012f:.065f)),new Vector3(x*fan,-len*.78f,z-(male?.018f:.10f)),new Vector3(x*(longHair?1.65f:.90f),-len,z-.055f),.029f,.017f,i%3==0?.80f:1);
            }
            if(male)for(int side=-1;side<=1;side+=2)for(int i=0;i<2;i++)
                FilmLock(m,new Vector3(side*.115f,.075f,-.035f-i*.035f),new Vector3(side*.145f,.060f,-.025f-i*.040f),new Vector3(side*.158f,.030f,-.020f-i*.040f),new Vector3(side*.174f,.035f-i*.022f,-.015f-i*.040f),.012f,.009f,i==0?1:.85f);
            if(twins)for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
                FilmLock(m,new Vector3(side*.16f,.14f,-.065f),new Vector3(side*.225f,.09f,-.10f),new Vector3(side*.215f,-.09f,-.11f),new Vector3(side*(.165f+i*.012f),-.15f+i*.012f,-.08f),.021f,.015f,i==1?1:.85f);
            if(elder)for(int i=0;i<6;i++){float a=i*Mathf.PI/3;FilmLock(m,new Vector3(0,.15f,-.06f),new Vector3(Mathf.Cos(a)*.045f,.30f,-.04f),new Vector3(Mathf.Cos(a)*.045f,.30f,.015f),new Vector3(0,.18f,.025f),.020f,.016f);}
            if(id=="艾琳"||male)for(int i=0;i<m.V.Count;i++)
            {var v=m.V[i];v.x*=id=="艾琳"?.80f:.82f;if(id=="艾琳")v.x*=1-.18f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.16f,v.y));if(v.z<0)v.z*=id=="艾琳"?.82f:.74f;
                if(id=="艾琳")
                {
                    if(v.z>.12f)v.z=.12f+(v.z-.12f)*.55f;
                    if(v.z<0)v.z=-.009f+(v.z+.009f)*.90f;
                }m.V[i]=v;}
            if(id=="艾琳")FitRearLocks(m.V,p);
            cached=m.Finish("Film silhouette "+id);filmHair[id]=cached;return cached;
        }
        internal static Mesh Build(string id,Portraits.Persona p)
        {
            var m=new OutfitArt.Builder();var look=CharacterLook.Get(id);bool bob=id=="艾琳"||id=="凪",longHair=id=="翠"||id=="澪",twins=id=="小葉",shortHair=id=="海斗"||id=="小鐵"||id=="老焰";
            const int sides=20,rows=6;m.V.Add(new Vector3(0,.177f*p.Length,-.009f));m.C.Add(new Color(.68f,.68f,.68f,1));
            for(int j=1;j<=rows;j++)for(int i=0;i<sides;i++)
            {
                float phi=i*2*Mathf.PI/sides,front=Mathf.Cos(phi),theta=j/(float)rows*(front>.3f?1.06f:1.90f);
                m.V.Add(new Vector3(Mathf.Sin(theta)*Mathf.Sin(phi)*.158f*p.Width,Mathf.Cos(theta)*.177f*p.Length,Mathf.Sin(theta)*front*(front>0?.171f:.159f)-.009f));m.C.Add(new Color(.69f,.69f,.69f,1));
                int n=(i+1)%sides;if(j==1)m.T.AddRange(new[]{0,1+i,1+n});else{int k=1+(j-2)*sides+i,nn=1+(j-2)*sides+n;m.T.AddRange(new[]{k,k+sides,nn,nn,k+sides,nn+sides});}
            }
            originalTop=.177f*p.Length;
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,x=Mathf.Sin(a),z=Mathf.Cos(a);bool spike=id=="小鐵";
                Lock(m,new Vector3(x*.03f,.16f,z*.025f),new Vector3(x*.10f,spike?.26f:.19f,z*.10f),new Vector3(x*.15f,spike?.23f:.13f,z*.155f),new Vector3(x*(spike?.22f:.16f),spike?.20f:.065f,z*.15f),.042f,.025f,i%3==0?.86f:1);
            }
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.064f,y=(id=="老焰"?.090f:.014f)+(i%2)*.010f;float root=look.Part*.035f;
                Lock(m,new Vector3(root+x*.22f,.155f,.06f),new Vector3(x*.6f,.135f,.17f),new Vector3(x,.075f,.177f),new Vector3(x+(i<2?-.012f:.012f),y,AnimeFace.HairSeat(x,y,p)+.012f),.041f,.021f,i%2==0?.96f:1.02f);
            }
            for(int side=-1;side<=1;side+=2)for(int tier=0;tier<2;tier++)
            {
                float len=longHair?look.Length:bob?(id=="艾琳"?.20f:.16f):.035f,front=tier==0?.15f:-.02f;
                Lock(m,new Vector3(side*.135f,.09f,front*.4f),new Vector3(side*.187f,-.01f,front),new Vector3(side*(bob?.21f:shortHair?.15f:.18f),-len*.82f,front),new Vector3(side*(bob?.23f:shortHair?.145f:.165f),-len+(bob?.035f:0),front),.044f,.027f,tier==0?1:.78f);
            }
            if(twins)
            {
                for(int side=-1;side<=1;side+=2)for(int k=0;k<5;k++)
                    Lock(m,new Vector3(side*.16f,.125f,-.07f-k*.008f),new Vector3(side*(.34f+k*.008f),.08f,-.16f),new Vector3(side*(.36f+k*.007f),-.38f,-.16f+k*.012f),new Vector3(side*(.23f+k*.018f),-.50f+k*.015f,-.06f+k*.012f),.068f,.042f,k%2==0?.95f:.83f);
            }
            else for(int i=0;i<8;i++)
            {
                float a=(i/7f-.5f)*2.6f,x=Mathf.Sin(a)*.14f,z=-Mathf.Cos(a)*.151f,len=longHair?look.Length:bob?(id=="艾琳"?.20f:.16f):.045f;
                Lock(m,new Vector3(x,.065f,z),new Vector3(x*(id=="翠"?2.4f:1.16f),-len*.25f,z-.035f),new Vector3(x*(id=="翠"?3.0f:bob?1.40f:1.13f),-len*(id=="翠"?.65f:1),z-.018f),new Vector3(x*(id=="翠"?1.75f:bob?1.48f:1.10f),-len+(bob?.035f:0),z*.88f),.046f,.030f,i%3==0?.82f:1);
            }
            if(id=="老焰")for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Lock(m,new Vector3(0,.15f,-.06f),new Vector3(Mathf.Cos(a)*.07f,.30f,-.04f),new Vector3(Mathf.Cos(a)*.07f,.30f,.015f),new Vector3(0,.18f,.025f),.031f,.023f);}
            AnimeHair.KeepHairTop(m.V,originalTop);
            return m.Finish("Volume story hair "+id);
        }
    }
}
