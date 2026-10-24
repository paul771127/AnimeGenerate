using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>有厚度的彎曲髮束與開口髮殼；同一網格亦投影成頭像頭髮。</summary>
    internal static class AnimeHair
    {
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,Color32[]> images=new Dictionary<string,Color32[]>();
        internal const int W=256,H=320;
        static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,float t)=>a*((1-t)*(1-t))+b*(2*t*(1-t))+c*(t*t);
        static float originalTop;
        // Two fine strands occupy the old root envelope; each ends in a single tip vertex.
        static void Lock(List<Vector3> v,List<int> tri,List<Color> colors,Vector3 a,Vector3 b,Vector3 c,float width,float depth,float layer=1,float twist=0)
        {
            // Preserve the old upper envelope while refining only the strand construction.
            for(int j=0;j<=7;j++)
            {
                float t=j/7f;var at=Curve(a,b,c,t);var dir=((b-a)*(2*(1-t))+(c-b)*(2*t)).normalized;
                var right=Vector3.Cross(dir,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;var normal=Vector3.Cross(right,dir).normalized;
                float taper=(.88f+.12f*Mathf.Sin(t*Mathf.PI))*Mathf.Pow(1-t,.62f)+.012f;
                for(int i=0;i<5;i++){float q=i*2*Mathf.PI/5+twist*t;originalTop=Mathf.Max(originalTop,(at+right*(Mathf.Cos(q)*width*taper)+normal*(Mathf.Sin(q)*depth*taper)).y);}
            }
            const int rows=6,sides=5;
            for(int strand=-1;strand<=1;strand+=2)
            {
                int start=v.Count;
                for(int j=0;j<rows;j++)
                {
                    float t=j/(float)rows;var at=Curve(a,b,c,t);var dir=((b-a)*(2*(1-t))+(c-b)*(2*t)).normalized;
                    var right=Vector3.Cross(dir,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;var normal=Vector3.Cross(right,dir).normalized;
                    float taper=(.88f+.12f*Mathf.Sin(t*Mathf.PI))*Mathf.Pow(1-t,1.12f);
                    at+=right*(strand*width*.5f*taper)+Vector3.right*(strand*width*.16f*t*t);
                    for(int i=0;i<sides;i++)
                    {
                        float q=i*2*Mathf.PI/sides+twist*t;
                        v.Add(at+right*(Mathf.Cos(q)*width*.50f*taper)+normal*(Mathf.Sin(q)*depth*.80f*taper));
                        float tone=(t<.17f?.48f:t>.30f&&t<.66f?1.20f:.83f)*layer*(Mathf.Sin(q)<-.2f?.68f:1)*(strand<0?.96f:1);
                        colors.Add(new Color(tone*(1+.09f*t),tone,tone*(1-.12f*t)));
                        if(j<rows-1){int k=start+j*sides+i,n=start+j*sides+(i+1)%sides;tri.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});}
                    }
                }
                int tip=v.Count;v.Add(c+Vector3.right*(strand*width*.16f));colors.Add(new Color(.73f*layer,.73f*layer,.73f*layer));
                for(int i=0;i<sides;i++){int k=start+(rows-1)*sides+i,n=start+(rows-1)*sides+(i+1)%sides;tri.AddRange(new[]{k,tip,n});}
                for(int i=1;i<sides-1;i++)tri.AddRange(new[]{start,start+i,start+i+1});
            }
        }
        internal static void KeepHairTop(List<Vector3> vertices,float reference)
        {
            float top=.12f;for(int i=0;i<vertices.Count;i++)top=Mathf.Max(top,vertices[i].y);
            float scale=(reference-.12f)/Mathf.Max(.001f,top-.12f);
            for(int i=0;i<vertices.Count;i++){var v=vertices[i];if(v.y>.12f)v.y=.12f+(v.y-.12f)*scale;vertices[i]=v;}
        }
        static Vector3 Shell(float t,float phi,Portraits.Persona p,CharacterLook.Look look)
        {
            float front=Mathf.Cos(phi),limit=front>.3f?1.05f+(1-front)*.5f:1.75f+(1-front)*.15f;
            if(look.Hair==CharacterLook.HairKind.Crew)limit-=front>0?.08f:0;
            float theta=t*limit;
            float part=1-.055f*Mathf.Exp(-Mathf.Pow((Mathf.Sin(phi)-look.Part)/.19f,2))*Mathf.Sin(theta);
            return new Vector3(.158f*p.Width*Mathf.Sin(theta)*Mathf.Sin(phi)*part,.177f*p.Length*Mathf.Cos(theta),(front>0?.165f:.161f)*Mathf.Sin(theta)*front-.009f);
        }
        internal static Mesh MeshFor(string id,Portraits.Persona p)
        {
            if(meshes.TryGetValue(id,out var mesh))return mesh;
            if(StoryHair.Has(id)){mesh=StoryHair.Build(id,p);meshes[id]=mesh;return mesh;}
            var look=CharacterLook.Get(id);var kind=look.Hair;
            var v=new List<Vector3>{new Vector3(0,.177f*p.Length,-.009f)};var tri=new List<int>();var colors=new List<Color>{new Color(.46f,.46f,.46f)};
            const int sides=20,rows=6;
            for(int j=1;j<=rows;j++)for(int i=0;i<sides;i++)
            {
                float phi=i*2*Mathf.PI/sides;v.Add(Shell(j/(float)rows,phi,p,look));colors.Add(new Color(.44f,.44f,.47f));int n=(i+1)%sides;
                if(j==1)tri.AddRange(new[]{0,1+i,1+n});else {int a=1+(j-2)*sides+i,b=1+(j-2)*sides+n;tri.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
            }
            originalTop=.177f*p.Length;
            bool crew=kind==CharacterLook.HairKind.Crew,slick=kind==CharacterLook.HairKind.Slick;
            // Twelve interleaved crown sweeps, not a smooth cap. The part has a visible shallow valley.
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6+look.Part*.35f,side=Mathf.Sin(a),front=Mathf.Cos(a);
                float rootX=look.Part*.045f+side*.035f,top=crew?.172f:.180f+(look.Seed%3)*.005f;
                float endY=front>.3f?.085f:-.015f-(look.Seed%4)*.008f;
                float endX=side*(crew?.149f:.166f),endZ=front*.151f-.010f;
                if(slick){top+=.018f;endY=.045f;endZ-=.022f;}
                Lock(v,tri,colors,new Vector3(rootX,.162f,-.012f+front*.025f),new Vector3(side*.115f,top,front*.112f-.01f),new Vector3(endX,endY,endZ),crew?.019f:.030f,.012f,i%3==0?.77f:1,look.Part*.4f);
            }
            int bangs=crew?8:6;
            for(int i=0;i<bangs;i++)
            {
                float u=i/(float)(bangs-1),x=(u-.5f)*.25f,root=look.Part*.08f;
                float y=.060f+.022f*Mathf.Sin(i*2.3f+look.Seed);
                if(kind==CharacterLook.HairKind.SidePart||kind==CharacterLook.HairKind.LongSide)y-=.026f*(look.Part<0?u:1-u);
                if(kind==CharacterLook.HairKind.Straight)y=.065f+(i%2)*.009f;
                y=Mathf.Max(.013f,y-.020f);
                if(crew)y=.095f+(i%2)*.012f;if(slick)y=.079f;
                Lock(v,tri,colors,new Vector3(root+(x-root)*.2f,.153f,.052f),new Vector3(x*.70f+root*.3f,.123f,.158f),new Vector3(x,y,AnimeFace.HairSeat(x,y,p)+.009f),crew?.015f:.025f+(i%2)*.004f,.010f,i%3==0?.80f:1,.25f*(i%2==0?1:-1));
            }
            if(!crew)for(int side=-1;side<=1;side+=2)for(int tier=0;tier<2;tier++)
            {
                float length=kind==CharacterLook.HairKind.Straight||kind==CharacterLook.HairKind.LongSide||kind==CharacterLook.HairKind.Bob?look.Length:.055f+(look.Seed%3)*.019f;
                if(kind==CharacterLook.HairKind.SidePart&&side<0)length*=.4f;
                float flare=kind==CharacterLook.HairKind.Wolf?.035f:0;
                Lock(v,tri,colors,new Vector3(side*.126f,.125f,-tier*.035f),new Vector3(side*(.171f+tier*.008f),.02f,id=="翠"?.19f-tier*.035f:.045f-tier*.055f),new Vector3(side*(.150f+flare+tier*.008f),-length+tier*.035f,id=="翠"?.20f-tier*.035f:.025f-tier*.045f),.029f,.014f,tier==0?1:.66f,side*.3f);
            }
            bool longBack=kind==CharacterLook.HairKind.Straight||kind==CharacterLook.HairKind.LongSide||kind==CharacterLook.HairKind.Bob||kind==CharacterLook.HairKind.Wolf;
            if(longBack)for(int i=0;i<8;i++)
            {
                float a=(i/7f-.5f)*2.5f,x=Mathf.Sin(a)*.132f,z=-Mathf.Cos(a)*.147f;
                float flip=kind==CharacterLook.HairKind.Wolf||id=="艾琳"||id=="凪"?1.32f:1.08f;
                Lock(v,tri,colors,new Vector3(x,.07f,z),new Vector3(x*1.18f,-look.Length*.45f,z-.020f),new Vector3(x*flip,-look.Length+Mathf.Sin(i*2+look.Seed)*.028f,z*.84f),.037f,.017f,i%2==0?.72f:1,i*.06f);
            }
            if(kind==CharacterLook.HairKind.HighPony||kind==CharacterLook.HairKind.LowPony||kind==CharacterLook.HairKind.Twins||kind==CharacterLook.HairKind.Braid)
            {
                int tails=kind==CharacterLook.HairKind.Twins?2:1;
                for(int k=0;k<tails;k++)for(int strand=0;strand<(id=="小葉"?8:4);strand++)
                {
                    float side=tails==1?look.Part:(k==0?-1:1),x=tails==1?side*.08f:side*.16f,y=kind==CharacterLook.HairKind.LowPony?-.025f:look.Seed==21?.075f:.135f;
                    Lock(v,tri,colors,new Vector3(x,y,-.11f),new Vector3(x+side*(id=="小葉"?.16f:.11f),y+.14f,-.22f-strand*.011f),new Vector3(x+side*(.12f+(strand%2)*.02f),-look.Length+strand*.012f,-.20f+strand*.025f),id=="小葉"?.060f:.028f,id=="小葉"?.033f:.019f,strand==0?.65f:1,strand*.13f);
                }
            }
            if(kind==CharacterLook.HairKind.Bun)for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3,shift=look.Part*.16f,bunY=id=="老焰"?.32f:look.Seed==23?.175f:.245f,bunZ=id=="老焰"?-.03f:look.Seed==23?-.23f:-.17f;
                Lock(v,tri,colors,new Vector3(shift,.13f,-.12f),new Vector3(shift+Mathf.Cos(a)*.08f,bunY+Mathf.Sin(a)*.035f,bunZ),new Vector3(shift,.13f,-.16f),.030f,.023f,i%2==0?.72f:1,.5f);
            }
            if(kind==CharacterLook.HairKind.Spikes)for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4;Lock(v,tri,colors,new Vector3(Mathf.Sin(a)*.085f,.135f,Mathf.Cos(a)*.065f),new Vector3(Mathf.Sin(a)*.15f,.19f,Mathf.Cos(a)*.10f),new Vector3(Mathf.Sin(a)*.19f,.18f+look.Length*.42f+(i%2)*.02f,Mathf.Cos(a)*.14f),.030f,.016f,i%2==0?.75f:1,.3f);
            }
            // Bespoke short-hair signatures; the silhouettes remain different without headgear.
            if(look.Seed==1||look.Seed==3||look.Seed==6||look.Seed==14||look.Seed==15||look.Seed==22||look.Seed==24||look.Seed==25||look.Seed==27)
            {
                int mode=look.Seed;
                for(int i=0;i<3;i++)
                {
                    float x=(i-1)*.025f;Vector3 a=new Vector3(x,.155f,.02f),b,c;
                    if(mode==1){b=new Vector3(-.08f+x,.243f,.02f);c=new Vector3(-.11f+x,.216f,.06f);}
                    else if(mode==3){b=new Vector3(.13f+x,.320f,.10f);c=new Vector3(-.08f+x,.146f,.145f);}
                    else if(mode==6){b=new Vector3(x,.242f,.065f);c=new Vector3(x+.025f,.17f,-.19f-i*.018f);}
                    else if(mode==22){b=new Vector3(x+.02f,.265f,.08f);c=new Vector3(x+.10f,.205f,.15f);}
                    else if(mode==14){b=new Vector3(-.08f+x,.200f,.08f);c=new Vector3(.075f+x,.137f,.135f);}
                    else if(mode==15){b=new Vector3(-.12f+x,.145f,.158f);c=new Vector3(-.069f+x,.024f+i*.007f,.14f);}
                    else if(mode==16){a=new Vector3((i%2==0?-1:1)*.13f,.075f,-.09f);b=a+new Vector3((i%2==0?-1:1)*.09f,.06f,-.025f);c=b+new Vector3(-.01f,-.025f,-.02f);}
                    else if(mode==24){b=new Vector3(.07f+x,.24f,.02f);c=new Vector3(.13f+x,.19f,-.01f);}
                    else if(mode==25){b=new Vector3(-.14f+x,.18f,.02f);c=new Vector3(-.20f+x,.13f,.04f);}
                    else {b=new Vector3(-.12f+x,.20f,.14f);c=new Vector3(.08f+x,.058f,.14f);}
                    Lock(v,tri,colors,a,b,c,.022f,.010f,i==0?.73f:1,.3f);
                }
            }
            if(look.Seed==27)for(int i=0;i<2;i++)Lock(v,tri,colors,new Vector3(-.13f,.025f,-.04f),new Vector3(-.225f,-.065f,-.065f),new Vector3(-.19f+i*.014f,-.19f,-.06f),.017f,.010f,i==0?.7f:1,.7f);
            if(look.Seed==15)Lock(v,tri,colors,new Vector3(-.13f,.09f,.02f),new Vector3(-.21f,-.015f,.04f),new Vector3(-.185f,-.12f,.04f),.025f,.011f,.85f,.2f);
            KeepHairTop(v,originalTop);
            mesh=new Mesh{name="髮型 "+id};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.colors=colors.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[id]=mesh;return mesh;
        }
        internal static void Build(Transform head,string id,Portraits.Persona p)=>AnimeFace.Part(head,"整體髮型",HeadwearArt.HairFor(id,p),Vector3.zero,AnimeFace.Material(CharacterLook.Get(id).HairColor,4));
        internal static Color32[] Portrait(string id,Portraits.Persona p,bool frontOnly)
        {
            string key=id+frontOnly;if(images.TryGetValue(key,out var image))return image;
            var mesh=HeadwearArt.HairFor(id,p);var vertices=mesh.vertices;var tri=mesh.triangles;var colors=mesh.colors;var hair=CharacterLook.Get(id).HairColor;
            var depth=new float[W*H];for(int i=0;i<depth.Length;i++)depth[i]=-100;image=new Color32[W*H];
            for(int i=0;i<tri.Length;i+=3)
            {
                var a=vertices[tri[i]];var b=vertices[tri[i+1]];var c=vertices[tri[i+2]];
                if(Vector3.Cross(b-a,c-a).z<=0)continue;
                int x0=Mathf.Clamp((int)((Mathf.Min(a.x,Mathf.Min(b.x,c.x))+.28f)/.56f*W),0,W-1),x1=Mathf.Clamp((int)((Mathf.Max(a.x,Mathf.Max(b.x,c.x))+.28f)/.56f*W)+1,0,W-1);
                int y0=Mathf.Clamp((int)((Mathf.Min(a.y,Mathf.Min(b.y,c.y))+.34f)/.60f*H),0,H-1),y1=Mathf.Clamp((int)((Mathf.Max(a.y,Mathf.Max(b.y,c.y))+.34f)/.60f*H)+1,0,H-1);
                float den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(den)<1e-9f)continue;
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float px=(x+.5f)/W*.56f-.28f,py=(y+.5f)/H*.60f-.34f;
                    float u=((b.y-c.y)*(px-c.x)+(c.x-b.x)*(py-c.y))/den,v0=((c.y-a.y)*(px-c.x)+(a.x-c.x)*(py-c.y))/den,w=1-u-v0;
                    if(u<0||v0<0||w<0)continue;float z=u*a.z+v0*b.z+w*c.z;int k=y*W+x;
                    if(z<=depth[k])continue;
                    if(frontOnly&&Mathf.Abs(px)<.14f*p.Width&&py>-.157f*p.Length&&py<.157f*p.Length&&z<AnimeFace.Surface(px,py,p)+.001f)continue;
                    // Back hair belongs behind the neck/shoulders; don't paint it over the portrait collar.
                    if(frontOnly&&py<-.157f&&Mathf.Abs(px)<.105f&&z<.08f)continue;
                    if(frontOnly&&py<-.22f&&Mathf.Abs(px)<.22f&&z<.10f)continue;
                    depth[k]=z;var ca=colors[tri[i]];var cb=colors[tri[i+1]];var cc=colors[tri[i+2]];
                    float red=u*ca.r+v0*cb.r+w*cc.r,green=u*ca.g+v0*cb.g+w*cc.g,blue=u*ca.b+v0*cb.b+w*cc.b;
                    image[k]=new Color32((byte)(Mathf.Clamp01(hair.r*red)*255),(byte)(Mathf.Clamp01(hair.g*green)*255),(byte)(Mathf.Clamp01(hair.b*blue)*255),255);
                }
            }
            if(images.Count>=16)images.Clear();images[key]=image;return image;
        }
    }
}
