using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>Head-local headwear, fitted from the actual unchanged hair mesh; shared by portraits and villagers.</summary>
    internal static class HeadwearArt
    {
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static readonly Dictionary<string,Color32[]> images=new Dictionary<string,Color32[]>();
        internal const int W=256,H=256;
        internal static bool Has(string id)=>id=="mage"||id=="archmage"||id=="summoner"||id=="priest"||id=="bishop"||id=="米菈"||id=="海斗"||id=="阿潮"||id=="小鐵"||id=="翠"||id=="小葉"||id=="店員"||id=="凪";
        internal static float Seat(string id,Portraits.Persona p)
        {
            var v=AnimeHair.MeshFor(id,p).vertices;float top=-1;
            // The first 121 vertices are the actual shell, excluding cowlicks/ponytails.
            for(int i=0;i<Mathf.Min(121,v.Length);i++)top=Mathf.Max(top,v[i].y);
            return top*.52f;
        }
        internal static float FitRadius(string id,Portraits.Persona p,float y)
        {
            float r=.135f;var vertices=AnimeHair.MeshFor(id,p).vertices;
            for(int i=0;i<Mathf.Min(121,vertices.Length);i++){var v=vertices[i];if(Mathf.Abs(v.y-y)<.035f)r=Mathf.Max(r,Mathf.Sqrt(v.x*v.x+v.z*v.z));}
            return Mathf.Clamp(r*.99f,.142f,.165f);
        }
        static void Ellipsoid(OutfitArt.Builder b,Vector3 at,Vector3 size,Color c)
        {
            var ys=new[]{1f,.7f,0f,-.7f,-1f};var rx=new[]{.10f,.72f,1f,.72f,.10f};var path=new Vector3[5];var x=new float[5];var z=new float[5];
            for(int i=0;i<5;i++){path[i]=at+new Vector3(0,ys[i]*size.y,0);x[i]=rx[i]*size.x;z[i]=rx[i]*size.z;}b.Tube(path,x,z,c,10);
        }
        internal static Mesh MeshFor(string id,Portraits.Persona p)
        {
            if(meshes.TryGetValue(id,out var found))return found;
            var b=new OutfitArt.Builder();if(!Has(id)){found=b.Finish("Headwear none");meshes[id]=found;return found;}
            var d=OutfitArt.Get(id);float seat=Seat(id,p),r=FitRadius(id,p,seat);var gold=CharacterLook.Hex("ffc94c");var navy=CharacterLook.Hex("253e75");
            bool crown=id=="priest"||id=="bishop",point=id=="mage"||id=="archmage"||id=="summoner",hat=crown||point||id=="米菈"||id=="海斗"||id=="阿潮";
            if(hat)
            {
                Color c=id=="米菈"?CharacterLook.Hex("efbf55"):id=="阿潮"?CharacterLook.Hex("dca94d"):id=="海斗"?CharacterLook.Hex("fff2d8"):d.Main;
                if(id=="阿潮")
                {
                    b.Loft(new[]{seat+.145f,seat+.12f,seat+.055f,seat},new[]{.006f,.080f,.205f,.285f},new[]{.006f,.080f,.205f,.270f},c);
                    for(int i=0;i<10;i++){float a=i*Mathf.PI/5;b.Seam(new Vector3(Mathf.Sin(a)*.025f,seat+.14f,Mathf.Cos(a)*.025f),new Vector3(Mathf.Sin(a)*.278f,seat+.004f,Mathf.Cos(a)*.263f),.004f,CharacterLook.Hex("a36d28"));}
                }
                else
                {
                    float height=crown?.150f:point?.150f:id=="海斗"?.105f:.115f;
                    b.Loft(new[]{seat+.012f,seat-.002f},new[]{point||id=="米菈"?.263f:r*1.14f,point||id=="米菈"?.263f:r*1.14f},new[]{point||id=="米菈"?.235f:r*1.10f,point||id=="米菈"?.235f:r*1.10f},c);
                    var path=new[]{new Vector3(point?.035f:0,seat+height,-.015f),new Vector3(0,seat+height*.73f,-.008f),new Vector3(0,seat+.026f,0),new Vector3(0,seat-.008f,0)};
                    b.Tube(path,new[]{point?.008f:crown?.075f:r*.83f,point?r*.50f:crown?r*.70f:r*.98f,r,r},new[]{point?.008f:crown?.065f:r*.77f,point?r*.50f:crown?r*.65f:r*.90f,r*.93f,r*.93f},c,16);
                    b.Tube(new[]{new Vector3(0,seat+.027f,0),new Vector3(0,seat+.004f,0)},new[]{r*1.012f,r*1.012f},new[]{r*.95f,r*.95f},id=="海斗"?navy:d.Sub,16);
                    if(id=="海斗")Ellipsoid(b,new Vector3(0,seat-.007f,.116f),new Vector3(r*1.1f,.012f,.12f),navy);
                    if(crown||id=="海斗")
                    {
                        b.Seam(new Vector3(0,seat+.035f,r*.96f),new Vector3(0,seat+.095f,r*.73f),.012f,gold);
                        b.Seam(new Vector3(-.025f,seat+.065f,r*.86f),new Vector3(.025f,seat+.065f,r*.86f),.012f,gold);
                    }
                }
            }
            else if(id=="小鐵")
            {
                b.Loft(new[]{seat+.012f,seat-.012f},new[]{r,r},new[]{r*.92f,r*.92f},CharacterLook.Hex("d34d32"));
                for(int side=-1;side<=1;side+=2){Ellipsoid(b,new Vector3(side*.073f,seat+.012f,.155f),new Vector3(.050f,.035f,.020f),CharacterLook.Hex("e99731"));Ellipsoid(b,new Vector3(side*.073f,seat+.012f,.174f),new Vector3(.035f,.024f,.006f),CharacterLook.Hex("f3b956"));}
            }
            else if(id=="凪")b.Seam(new Vector3(-.145f,.145f,-.14f),new Vector3(-.045f,.205f,-.14f),.009f,gold);
            else
            {
                float x=id=="翠"?0:.105f,y=seat+.005f,z=AnimeFace.Surface(x,y,p)+.015f;
                Ellipsoid(b,new Vector3(x,y,z),new Vector3(.030f,.013f,.009f),id=="翠"?CharacterLook.Hex("70e38c"):CharacterLook.Hex("b5ed60"));
                b.Seam(new Vector3(x-.024f,y-.004f,z+.006f),new Vector3(x+.024f,y+.004f,z+.006f),.004f,gold);
            }
            found=b.Finish("Headwear "+id);meshes[id]=found;return found;
        }
        static readonly Dictionary<string,Mesh> fittedHair=new Dictionary<string,Mesh>();
        internal static Mesh HairFor(string id,Portraits.Persona p)
        {
            var source=AnimeHair.MeshFor(id,p);
            bool covered=id=="mage"||id=="archmage"||id=="summoner"||id=="priest"||id=="bishop"||id=="米菈"||id=="海斗"||id=="阿潮";
            if(!covered)return source;if(fittedHair.TryGetValue(id,out var mesh))return mesh;
            float seat=Seat(id,p),r=FitRadius(id,p,seat);var vertices=new Vector3[source.vertices.Length];System.Array.Copy(source.vertices,vertices,vertices.Length);
            for(int i=0;i<vertices.Length;i++)
            {
                var v=vertices[i];float radial=Mathf.Sqrt(v.x*v.x+v.z*v.z);
                // Only the crown hidden inside the hat is compressed; bangs/tails below/outside it keep their silhouette.
                if(v.y>seat&&radial<.225f)
                {
                    v.y=seat+.002f+(v.y-seat)*.14f;float limit=r*.78f;
                    if(radial>limit){v.x*=limit/radial;v.z*=limit/radial;}vertices[i]=v;
                }
            }
            mesh=new Mesh{name="Hat-compressed crown "+id,vertices=vertices,triangles=source.triangles,colors=source.colors,uv=source.uv};mesh.RecalculateNormals();mesh.RecalculateBounds();fittedHair[id]=mesh;return mesh;
        }
        internal static void Build(Transform head,string id,Portraits.Persona p)
        {if(Has(id))AnimeFace.Part(head,"Headwear fitted "+id,MeshFor(id,p),Vector3.zero,AnimeFace.Material(Color.white,4));}
        internal static Color32[] Portrait(string id,Portraits.Persona p)
        {
            if(images.TryGetValue(id,out var image))return image;image=new Color32[W*H];if(!Has(id)){if(images.Count>=12)images.Clear();images[id]=image;return image;}
            float seat=Seat(id,p);var mesh=MeshFor(id,p);var vs=mesh.vertices;var ts=mesh.triangles;var cs=mesh.colors;var depth=new float[W*H];for(int k=0;k<depth.Length;k++)depth[k]=-100;
            for(int t=0;t<ts.Length;t+=3)
            {
                var a=vs[ts[t]];var b=vs[ts[t+1]];var c=vs[ts[t+2]];if(Vector3.Cross(b-a,c-a).z<=0)continue;
                float den=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(den)<1e-9f)continue;
                int x0=Mathf.Clamp((int)((Mathf.Min(a.x,Mathf.Min(b.x,c.x))+.34f)/.68f*W),0,W-1),x1=Mathf.Clamp((int)((Mathf.Max(a.x,Mathf.Max(b.x,c.x))+.34f)/.68f*W)+1,0,W-1);
                int y0=Mathf.Clamp((int)((Mathf.Min(a.y,Mathf.Min(b.y,c.y))+.16f)/.56f*H),0,H-1),y1=Mathf.Clamp((int)((Mathf.Max(a.y,Mathf.Max(b.y,c.y))+.16f)/.56f*H)+1,0,H-1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float px=(x+.5f)/W*.68f-.34f,py=(y+.5f)/H*.56f-.16f,u=((b.y-c.y)*(px-c.x)+(c.x-b.x)*(py-c.y))/den,v=((c.y-a.y)*(px-c.x)+(a.x-c.x)*(py-c.y))/den,w=1-u-v;
                    if(u<0||v<0||w<0)continue;float z=u*a.z+v*b.z+w*c.z;int k=y*W+x;if(z<=depth[k])continue;
                    // Hair genuinely in front of the brim (bangs) remains visible below the fitted seat.
                    if(py<seat-.012f&&Mathf.Abs(px)<.14f&&z<AnimeFace.Surface(px,py,p))continue;
                    depth[k]=z;var ca=cs[ts[t]];var cb=cs[ts[t+1]];var cc=cs[ts[t+2]];image[k]=new Color32((byte)(Mathf.Clamp01(ca.r*u+cb.r*v+cc.r*w)*255),(byte)(Mathf.Clamp01(ca.g*u+cb.g*v+cc.g*w)*255),(byte)(Mathf.Clamp01(ca.b*u+cb.b*v+cc.b*w)*255),255);
                }
            }
            if(images.Count>=12)images.Clear();images[id]=image;return image;
        }
    }
}
