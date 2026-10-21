using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Small shared meshes: hollow ring shells, stratified stone and an eight-face black crystal.
    internal static class ChapterArt
    {
        static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
        internal static Mesh Shell(bool hollow=true)
        {
            string key=hollow?"Armour shell":"Stratified rock";if(cache.TryGetValue(key,out var m))return m;
            if(!hollow){var b=new OutfitArt.Builder();b.Loft(new[]{.5f,.40f,.15f,-.12f,-.35f,-.5f},new[]{.32f,.49f,.50f,.45f,.48f,.35f},new[]{.30f,.46f,.50f,.45f,.47f,.34f},Color.white,0,0,12);m=b.Finish(key);}
            else
            {
                var v=new List<Vector3>();var t=new List<int>();const int n=16;
                for(int wall=0;wall<2;wall++)for(int row=0;row<3;row++)for(int i=0;i<n;i++)
                {float a=i*2*Mathf.PI/n,r=(row==1?.50f:.43f)-wall*.055f;v.Add(new Vector3(Mathf.Sin(a)*r,.5f-row*.5f,Mathf.Cos(a)*r));}
                for(int wall=0;wall<2;wall++)for(int row=0;row<2;row++)for(int i=0;i<n;i++){int a=wall*3*n+row*n+i,b=wall*3*n+row*n+(i+1)%n;t.AddRange(wall==0?new[]{a,b,a+n,b,b+n,a+n}:new[]{a,a+n,b,b,a+n,b+n});}
                foreach(int row in new[]{0,2})for(int i=0;i<n;i++){int a=row*n+i,b=row*n+(i+1)%n;t.AddRange(row==0?new[]{a,a+3*n,b,b,a+3*n,b+3*n}:new[]{a,b,a+3*n,b,b+3*n,a+3*n});}
                for(int i=0;i<t.Count;i+=3){int swap=t[i+1];t[i+1]=t[i+2];t[i+2]=swap;}
                m=new Mesh{name=key};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();
            }
            var uv=new Vector2[m.vertexCount];var verts=m.vertices;for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(.5f,verts[i].y+.5f);m.uv=uv;cache[key]=m;return m;
        }
        static Texture2D strata;
        internal static Texture2D Strata()
        {
            if(strata!=null)return strata;strata=new Texture2D(8,64,TextureFormat.RGBA32,false){name="Procedural sandstone strata",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[8*64];for(int y=0;y<64;y++)for(int x=0;x<8;x++){float shade=y%13<3?.72f:y%13<5?.86f:1;pixels[y*8+x]=new Color(shade,shade,shade);}
            strata.SetPixels(pixels);strata.Apply(false,true);return strata;
        }
        // Six engraved given names: 山、川、木、石、禾、火. Strokes, not imported fonts/textures.
        internal static Mesh MemorialName(int index,bool crossed)
        {
            string key="Memorial name "+index+" "+crossed;if(cache.TryGetValue(key,out var m))return m;
            var b=new OutfitArt.Builder();
            float[][][] glyphs={
                new[]{new[]{0f,.45f,0f,-.35f},new[]{-.4f,.16f,-.4f,-.35f},new[]{.4f,.16f,.4f,-.35f},new[]{-.4f,-.35f,.4f,-.35f}},
                new[]{new[]{-.34f,.42f,-.34f,-.10f},new[]{-.34f,-.10f,-.44f,-.42f},new[]{0f,.4f,0f,-.35f},new[]{.35f,.43f,.35f,-.43f}},
                new[]{new[]{-.44f,.20f,.44f,.20f},new[]{0f,.44f,0f,-.44f},new[]{0f,.15f,-.43f,-.34f},new[]{0f,.15f,.43f,-.34f}},
                new[]{new[]{-.42f,.37f,.43f,.37f},new[]{-.13f,.37f,-.42f,-.12f},new[]{-.23f,-.03f,.35f,-.03f},new[]{-.23f,-.03f,-.23f,-.40f},new[]{.35f,-.03f,.35f,-.40f},new[]{-.23f,-.40f,.35f,-.40f}},
                new[]{new[]{-.32f,.35f,.28f,.45f},new[]{-.43f,.15f,.43f,.15f},new[]{0f,.4f,0f,-.45f},new[]{0f,.1f,-.4f,-.32f},new[]{0f,.1f,.4f,-.32f}},
                new[]{new[]{0f,.44f,-.06f,-.12f},new[]{-.06f,-.12f,-.43f,-.43f},new[]{-.06f,-.12f,.42f,-.43f},new[]{-.37f,.12f,-.18f,-.03f},new[]{.37f,.15f,.17f,-.02f}}
            };
            foreach(var line in glyphs[index%6])b.Seam(new Vector3(line[0],line[1],0),new Vector3(line[2],line[3],0),.055f,Color.white);
            if(crossed)b.Seam(new Vector3(-.48f,0,.012f),new Vector3(.48f,0,.012f),.07f,Color.white);
            m=b.Finish(key);m.uv=new Vector2[m.vertexCount];cache[key]=m;return m;
        }
        internal static Mesh Crystal()
        {
            if(cache.TryGetValue("Black crystal",out var m))return m;
            m=new Mesh{name="Black crystal"};m.vertices=new[]{new Vector3(0,.5f,0),new Vector3(0,-.5f,0),new Vector3(-.5f,0,0),new Vector3(0,0,.5f),new Vector3(.5f,0,0),new Vector3(0,0,-.5f)};m.triangles=new[]{0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2};var tris=m.triangles;for(int i=0;i<tris.Length;i+=3){int swap=tris[i+1];tris[i+1]=tris[i+2];tris[i+2]=swap;}m.triangles=tris;m.RecalculateNormals();m.RecalculateBounds();cache["Black crystal"]=m;return m;
        }
    }
}
