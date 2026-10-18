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
        internal const int W=128,H=160;
        static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,float t)=>a*((1-t)*(1-t))+b*(2*t*(1-t))+c*(t*t);
        static void Lock(List<Vector3> v,List<int> tri,List<Color> colors,Vector3 a,Vector3 b,Vector3 c,float width,float depth)
        {
            int start=v.Count;const int rows=6,sides=6;
            for(int j=0;j<=rows;j++)
            {
                float t=j/(float)rows;var at=Curve(a,b,c,t);var direction=((b-a)*(2*(1-t))+(c-b)*(2*t)).normalized;
                var right=Vector3.Cross(direction,new Vector3(0,0,1)).normalized;if(right.sqrMagnitude<.1f)right=new Vector3(1,0,0);
                var normal=Vector3.Cross(right,direction).normalized;float taper=(.8f+.2f*Mathf.Sin(t*Mathf.PI))*(1-.96f*t*t*t*t);
                for(int i=0;i<sides;i++)
                {
                    float q=i*2*Mathf.PI/sides;v.Add(at+right*(Mathf.Cos(q)*width*taper)+normal*(Mathf.Sin(q)*depth*taper));
                    float shade=.72f+.32f*t;if(t>.23f&&t<.47f)shade+=.15f;colors.Add(new Color(shade,shade,shade));
                    if(j==rows)continue;int k=start+j*sides+i,n=start+j*sides+(i+1)%sides;
                    tri.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});
                }
            }
            // 封住髮根與髮尖，避免描邊漏縫。
            for(int cap=0;cap<2;cap++){int k=start+(cap==0?0:rows*sides);for(int i=1;i<sides-1;i++)tri.AddRange(cap==0?new[]{k,k+i,k+i+1}:new[]{k,k+i+1,k+i});}
        }
        static Vector3 Shell(float t,float phi,Portraits.Persona p,CharacterLook.Look look)
        {
            float front=Mathf.Cos(phi),limit=front>.3f?1.12f+(1-front)*.5f:1.75f+(1-front)*.15f;
            if(look.Hair==CharacterLook.HairKind.Crew&&front>0)limit-=look.Seed==16?.20f:.04f;
            if(look.Hair==CharacterLook.HairKind.Crop&&front<.3f)limit+=look.Length*2;
            float theta=t*limit,groove=1+.025f*Mathf.Cos(phi*7+look.Part*3)*Mathf.Sin(theta);
            float x=.151f*p.Width*Mathf.Sin(theta)*Mathf.Sin(phi)*groove;
            float z=(front>0?.145f:.164f)*Mathf.Sin(theta)*front-.008f;
            float y=.170f*p.Length*Mathf.Cos(theta)+.004f*Mathf.Cos(phi*3+look.Seed)*Mathf.Sin(theta);
            return new Vector3(x,y,z);
        }
        internal static Mesh MeshFor(string id,Portraits.Persona p)
        {
            if(meshes.TryGetValue(id,out var mesh))return mesh;var look=CharacterLook.Get(id);
            var v=new List<Vector3>{new Vector3(0,.170f*p.Length,-.008f)};var tri=new List<int>();var colors=new List<Color>{new Color(.72f,.72f,.72f)};
            const int sides=24,rows=8;
            for(int j=1;j<=rows;j++)for(int i=0;i<sides;i++)
            {
                float phi=i*2*Mathf.PI/sides;v.Add(Shell(j/(float)rows,phi,p,look));float tone=.74f+.22f*j/rows;
                if(j==3&&i%6!=0)tone=1.20f;colors.Add(new Color(tone,tone,tone));
                int n=(i+1)%sides;
                if(j==1)tri.AddRange(new[]{0,1+i,1+n});
                else {int a=1+(j-2)*sides+i,b=1+(j-2)*sides+n;tri.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
            }
            var kind=look.Hair;
            bool crew=kind==CharacterLook.HairKind.Crew,slick=kind==CharacterLook.HairKind.Slick;
            int bangCount=crew?0:slick?3:5+(look.Seed%2);
            for(int i=0;i<bangCount;i++)
            {
                float u=bangCount==1?0:i/(float)(bangCount-1),x=(u-.5f)*.24f;
                float root=look.Part*.09f,tipY=.067f+.018f*Mathf.Sin(i*2.4f+look.Seed);
                if(kind==CharacterLook.HairKind.SidePart||kind==CharacterLook.HairKind.Bob||kind==CharacterLook.HairKind.LongSide)tipY-=.022f*(1-u);
                if(slick)tipY=.12f;
                Lock(v,tri,colors,new Vector3(root+(x-root)*.25f,.148f,.066f),new Vector3(x*.65f+root*.3f,.118f,.151f),new Vector3(x,tipY,AnimeFace.Surface(x,tipY,p)+.006f),.025f+(i%2)*.004f,.008f);
            }
            // 側髮長短與後髮分開控制；不靠換色冒充不同髮型。
            if(!crew)for(int side=-1;side<=1;side+=2)
            {
                float length=kind==CharacterLook.HairKind.Bob?look.Length:kind==CharacterLook.HairKind.Straight||kind==CharacterLook.HairKind.LongSide?look.Length:.075f+(look.Seed%3)*.015f;
                Lock(v,tri,colors,new Vector3(side*.129f,.10f,.02f),new Vector3(side*.165f,-.005f,.04f),new Vector3(side*(.14f+(look.Seed%2)*.018f),-length,.025f),.025f,.012f);
            }
            bool longBack=kind==CharacterLook.HairKind.Straight||kind==CharacterLook.HairKind.Bob||kind==CharacterLook.HairKind.LongSide||kind==CharacterLook.HairKind.Wolf;
            if(longBack)for(int i=0;i<7;i++)
            {
                float a=(i/6f-.5f)*2.3f,x=Mathf.Sin(a)*.135f,z=-Mathf.Cos(a)*.14f;
                Lock(v,tri,colors,new Vector3(x,.04f,z),new Vector3(x*1.10f,-look.Length*.5f,z-.018f),new Vector3(x*1.10f,-look.Length+Mathf.Sin(i*2+look.Seed)*.025f,z*.82f),.033f,.014f);
            }
            if(kind==CharacterLook.HairKind.HighPony||kind==CharacterLook.HairKind.LowPony||kind==CharacterLook.HairKind.Braid||kind==CharacterLook.HairKind.Twins)
            {
                int count=kind==CharacterLook.HairKind.Twins?2:1;
                for(int k=0;k<count;k++)
                {
                    float side=count==1?look.Part:(k==0?-1:1),x=count==1?side*.06f:side*.16f;
                    float y=kind==CharacterLook.HairKind.LowPony?-.055f:.135f;
                    for(int strand=0;strand<3;strand++)Lock(v,tri,colors,new Vector3(x,y,-.11f),new Vector3(x+side*.07f,y+.10f,-.21f-strand*.01f),new Vector3(x+side*.08f,-look.Length,-.20f+strand*.018f),.024f,.017f);
                }
            }
            if(kind==CharacterLook.HairKind.Bun)for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*.4f;Lock(v,tri,colors,new Vector3(look.Part*.12f,.13f,-.12f),new Vector3(look.Part*.12f+Mathf.Cos(a)*.07f,.21f+look.Length*.2f+Mathf.Sin(a)*.04f,-.17f),new Vector3(look.Part*.12f,.13f,-.16f),.028f,.021f);
            }
            if(kind==CharacterLook.HairKind.Spikes)for(int i=0;i<8;i++)
            {
                float a=i*2*Mathf.PI/8;Lock(v,tri,colors,new Vector3(Mathf.Sin(a)*.095f,.11f,Mathf.Cos(a)*.07f),new Vector3(Mathf.Sin(a)*.14f,.19f,Mathf.Cos(a)*.10f),new Vector3(Mathf.Sin(a)*.16f,.17f+look.Length*.5f+(i%2)*.025f,Mathf.Cos(a)*.12f),.023f,.018f);
            }
            mesh=new Mesh{name="髮型 "+id};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.colors=colors.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[id]=mesh;return mesh;
        }
        internal static void Build(Transform head,string id,Portraits.Persona p)=>AnimeFace.Part(head,"整體髮型",MeshFor(id,p),Vector3.zero,AnimeFace.Material(CharacterLook.Get(id).HairColor,4));
        internal static Color32[] Portrait(string id,Portraits.Persona p,bool frontOnly)
        {
            string key=id+frontOnly;if(images.TryGetValue(key,out var image))return image;
            var mesh=MeshFor(id,p);var vertices=mesh.vertices;var tri=mesh.triangles;var colors=mesh.colors;var hair=CharacterLook.Get(id).HairColor;
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
                    depth[k]=z;float tone=u*colors[tri[i]].r+v0*colors[tri[i+1]].r+w*colors[tri[i+2]].r;
                    image[k]=new Color32((byte)(Mathf.Clamp01(hair.r*tone)*255),(byte)(Mathf.Clamp01(hair.g*tone)*255),(byte)(Mathf.Clamp01(hair.b*tone)*255),255);
                }
            }
            images[key]=image;return image;
        }
    }
}
