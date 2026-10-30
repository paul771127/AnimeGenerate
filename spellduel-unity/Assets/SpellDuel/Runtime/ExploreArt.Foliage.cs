using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  static readonly Material[] foliageMaterials=new Material[4];
  static Material FoliageMaterial(int seed)
  {
   seed=((seed%4)+4)%4;if(foliageMaterials[seed])return foliageMaterials[seed];
   const int size=256;var pixels=new Color32[size*size];var random=new System.Random(713+seed*891);
   // Small pointed leaves with an ink edge and a central vein; generated once, no imported image.
   for(int leaf=0;leaf<260;leaf++)
   {
    double angle=random.NextDouble()*Math.PI*2;float radius=Mathf.Sqrt((float)random.NextDouble())*.78f;
    float cx=128+Mathf.Cos((float)angle)*radius*128,cy=128+Mathf.Sin((float)angle)*radius*115;
    float length=8+(float)random.NextDouble()*12,width=3+(float)random.NextDouble()*4,turn=(float)random.NextDouble()*Mathf.PI*2;
    float co=Mathf.Cos(turn),si=Mathf.Sin(turn);int reach=Mathf.CeilToInt(length+width+3);
    var green=Color.Lerp(new Color(.10f,.30f,.12f),new Color(.48f,.69f,.25f),(float)random.NextDouble());
    for(int y=Mathf.Max(0,(int)cy-reach);y<=Mathf.Min(255,(int)cy+reach);y++)for(int x=Mathf.Max(0,(int)cx-reach);x<=Mathf.Min(255,(int)cx+reach);x++)
    {float dx=x-cx,dy=y-cy,u=(dx*co+dy*si)/length;if(Mathf.Abs(u)>=1)continue;float centre=1.4f*(1-u*u),v=(-dx*si+dy*co)-centre,half=width*(1-u*u);if(Mathf.Abs(v)>half)continue;
     Color col=Mathf.Abs(v)>half-.75f?new Color(.055f,.15f,.065f):Mathf.Abs(v)<.55f?Color.Lerp(green,Bone,.28f):green;pixels[y*size+x]=col;}
   }
   var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Explore generated leaves "+seed,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};texture.SetPixels32(pixels);texture.Apply(true,true);
   var mat=new Material(Resources.Load<Shader>("Shaders/ExploreFoliage")??SkyArt.Glow.shader){name="Explore shared leaf cutout "+seed};mat.mainTexture=texture;foliageMaterials[seed]=mat;return mat;
  }
  sealed class LeafMesh
  {
   readonly List<Vector3> v=new List<Vector3>(),normals=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<Color> colors=new List<Color>();readonly List<int> tris=new List<int>();
   internal void Cloud(Vector3 center,Vector3 size,int seed,Color tint,float limit=0,bool circular=false)
   {
    for(int plane=0;plane<3;plane++)
    {float angle=plane*Mathf.PI/3+seed*.63f;var side=V(Mathf.Cos(angle),0,Mathf.Sin(angle))*size.x*.5f;var up=V(.05f*Mathf.Sin(seed+plane),size.y*.5f,0);int start=v.Count;
     Vector3[] points={center-side-up,center+side-up,center+side+up,center-side+up};Vector2[] tex={Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
     for(int i=0;i<4;i++){var p=points[i];if(limit>0){if(circular){float r=new Vector2(p.x,p.z).magnitude;if(r>limit){p.x*=limit/r;p.z*=limit/r;}}else{p.x=Mathf.Clamp(p.x,-limit,limit);p.z=Mathf.Clamp(p.z,-limit,limit);}}p.y=Mathf.Max(0,p.y);v.Add(p);normals.Add((points[i]-center+Vector3.up*size.y*.25f).normalized);uv.Add(tex[i]);colors.Add(tint);}
     tris.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
   }
   internal Mesh Finish(string name){var m=new Mesh{name=name};m.SetVertices(v);m.SetNormals(normals);m.SetUVs(0,uv);m.SetColors(colors);m.SetTriangles(tris,0);m.RecalculateBounds();return m;}
  }
  static Transform Foliage(Transform parent,string key,int seed,Action<LeafMesh> build)
  {if(!meshes.TryGetValue(key,out var mesh)||!mesh){var b=new LeafMesh();build(b);mesh=b.Finish(key);meshes[key]=mesh;}return AnimeFace.Part(parent,key,mesh,Vector3.zero,FoliageMaterial(seed)).transform;}
  static void ThicketFoliage(Transform parent,int seed)
  {
   Foliage(parent,"foliage/wall/"+seed,seed,b=>{for(int i=0;i<7;i++){float a=i*2.399f+seed*.65f,r=i<3?.63f:.73f,y=i<3?.68f+(i%2)*.2f:1.95f+(i%3)*.15f;b.Cloud(V(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r),V(1.85f,1.5f,1.7f),seed+i,Color.white,1.5f);}}).name="CanopyLeaves";
  }
  static void TreeFoliage(Transform parent,int species,int seed)
  {
   if(species!=1&&species!=4)return;Foliage(parent,"foliage/tree/"+species+"/"+seed,seed,b=>{float h=3.7f+seed*.12f;
    for(int i=0;i<7;i++){float a=i*2.399f+seed*.55f;var at=i==6?V(.18f,h+.1f,-.1f):V(Mathf.Cos(a)*.50f,h-.35f+(i%3)*.27f,Mathf.Sin(a)*.50f);b.Cloud(at,V(1.3f,1.4f,1.2f),i+seed,Color.white,.98f,true);}
   }).name="CanopyLeaves";
  }
 }
}
