using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 // Presentation only. Roots are ground-centred, unscaled; the caller owns placement and collision.
 public static partial class ExploreArt
 {
  static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
  internal static readonly Color Bark=new Color(.38f,.22f,.12f), Leaf=new Color(.16f,.48f,.24f), Moss=new Color(.46f,.65f,.24f), Rock=new Color(.40f,.46f,.45f), Jade=new Color(.17f,.64f,.51f), Bone=new Color(.89f,.83f,.61f), Dark=new Color(.20f,.13f,.29f), Light=new Color(.70f,.95f,1), Water=new Color(.08f,.40f,.58f);
  internal static Color Glow(Color c){c.a=.5f;return c;}
  internal static GameObject Root(string name)=>new GameObject("Explore "+name);
  internal static Transform Part(Transform parent,string key,Action<Geo> build,Vector3 at=default(Vector3),bool transparent=false)
  {
   if(!meshes.TryGetValue(key,out var mesh)||!mesh){var b=new Geo();build(b);mesh=b.Finish(key);meshes[key]=mesh;}
   return AnimeFace.Part(parent,key,mesh,at,transparent?SkyArt.Glow:AnimeFace.Material(Color.white,4)).transform;
  }
  internal static int Variant(int x,int z,int count=4){unchecked{return (int)((uint)(x*73856093^z*19349663)%count);}}
  internal static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
  internal sealed class Geo
  {
   internal readonly List<Vector3> v=new List<Vector3>();readonly List<int> t=new List<int>();readonly List<Color> c=new List<Color>();readonly Dictionary<int,Vector3> foliageNormals=new Dictionary<int,Vector3>();
   internal void Tri(Vector3 a,Vector3 b,Vector3 d,Color col){int n=v.Count;v.Add(a);v.Add(b);v.Add(d);c.Add(col);c.Add(col);c.Add(col);t.Add(n);t.Add(n+1);t.Add(n+2);}
   internal void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color col,bool both=false){Tri(a,b,d,col);Tri(a,d,e,col);if(both){Tri(d,b,a,col);Tri(e,d,a,col);}}
   internal void Box(Vector3 p,Vector3 s,Color col){var a=p-s*.5f;var b=p+s*.5f;Quad(V(a.x,a.y,b.z),V(b.x,a.y,b.z),V(b.x,b.y,b.z),V(a.x,b.y,b.z),col);Quad(V(b.x,a.y,a.z),V(a.x,a.y,a.z),V(a.x,b.y,a.z),V(b.x,b.y,a.z),col);Quad(V(a.x,a.y,a.z),V(a.x,a.y,b.z),V(a.x,b.y,b.z),V(a.x,b.y,a.z),col);Quad(V(b.x,a.y,b.z),V(b.x,a.y,a.z),V(b.x,b.y,a.z),V(b.x,b.y,b.z),col);Quad(V(a.x,b.y,b.z),V(b.x,b.y,b.z),V(b.x,b.y,a.z),V(a.x,b.y,a.z),col);Quad(V(a.x,a.y,a.z),V(b.x,a.y,a.z),V(b.x,a.y,b.z),V(a.x,a.y,b.z),col);}
   internal void Rod(Vector3 a,Vector3 b,float r,Color col,int sides=5,float end=-1){if(end<0)end=r;var q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);for(int i=0;i<sides;i++){float u=i*2*Mathf.PI/sides,w=(i+1)*2*Mathf.PI/sides;var p=q*V(Mathf.Cos(u),0,Mathf.Sin(u));var n=q*V(Mathf.Cos(w),0,Mathf.Sin(w));Quad(a+p*r,b+p*end,b+n*end,a+n*r,col);Tri(a,a+p*r,a+n*r,col);Tri(b,b+n*end,b+p*end,col);}}
   internal void Gem(Vector3 p,Vector3 size,Color col,int sides=6){var top=p+V(0,size.y*.5f,0);var bot=p-V(0,size.y*.5f,0);for(int i=0;i<sides;i++){float a=i*2*Mathf.PI/sides,b=(i+1)*2*Mathf.PI/sides;var u=p+V(Mathf.Cos(a)*size.x*.5f,0,Mathf.Sin(a)*size.z*.5f);var w=p+V(Mathf.Cos(b)*size.x*.5f,0,Mathf.Sin(b)*size.z*.5f);Tri(top,w,u,col);Tri(bot,u,w,col);}}
   internal void Frond(Vector3 a,Vector3 tip,float width,Color col){var side=Vector3.Cross((tip-a).normalized,Vector3.up).normalized*width;var mid=Vector3.Lerp(a,tip,.48f)+Vector3.up*.12f;Quad(a,mid-side,tip,mid+side,col,true);}
   internal void Vine(Vector3 a,Vector3 b,float bend,float radius,Color col,int steps=5){var prev=a;for(int i=1;i<=steps;i++){float u=i/(float)steps;var p=Vector3.Lerp(a,b,u)+V(Mathf.Sin(u*Mathf.PI)*bend,0,Mathf.Sin(u*Mathf.PI*2)*bend*.3f);Rod(prev,p,radius,col,4,radius*.94f);prev=p;}}
   internal void Ring(Vector3 p,float radius,float width,Color col,int sides=20){for(int i=0;i<sides;i++){float a=i*2*Mathf.PI/sides,b=(i+1)*2*Mathf.PI/sides;Quad(p+V(Mathf.Cos(a)*(radius-width),0,Mathf.Sin(a)*(radius-width)),p+V(Mathf.Cos(b)*(radius-width),0,Mathf.Sin(b)*(radius-width)),p+V(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),p+V(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),col);}}
   internal void Crag(Vector3 center,Vector3 size,Color col,int seed=0)
   {
    int sides=9;var rings=new Vector3[4,sides];
    for(int j=0;j<4;j++)for(int i=0;i<sides;i++)
    {float a=i*Mathf.PI*2/sides+seed*.31f;float r=(j==0?.88f:j==1?1:j==2?.82f:.47f)*(1+.14f*Mathf.Sin(i*1.73f+seed*2+j*.63f));float y=j==0?0:size.y*(j==1?.25f:j==2?.68f:.94f)*(1+.06f*Mathf.Sin(i*2+seed));rings[j,i]=center+V(Mathf.Cos(a)*size.x*.5f*r+size.x*.045f*j*Mathf.Sin(seed+1),y,Mathf.Sin(a)*size.z*.5f*r);}
    for(int j=0;j<3;j++)for(int i=0;i<sides;i++){int k=(i+1)%sides;var c=Color.Lerp(col,Bone,.035f*j);Quad(rings[j,i],rings[j+1,i],rings[j+1,k],rings[j,k],c);}
    for(int i=0;i<sides;i++){int k=(i+1)%sides;Tri(center,rings[0,i],rings[0,k],col);Tri(center+V(size.x*.1f*Mathf.Sin(seed+1),size.y,0),rings[3,k],rings[3,i],col);}
   }
   // Rounded, scalloped foliage: two latitude rings plus poles, no diamond-shaped equator.
   internal void Crown(Vector3 center,Vector3 size,Color col,int seed,int sides=6,int bands=2)
   {
    int first=v.Count;var rings=new Vector3[bands,sides];
    for(int j=0;j<bands;j++)for(int i=0;i<sides;i++)
    {float lat=Mathf.PI*(j+1)/(bands+1),a=i*Mathf.PI*2/sides;float r=Mathf.Sin(lat)*(1+.12f*Mathf.Sin(i*2.3f+seed));rings[j,i]=center+V(Mathf.Cos(a)*size.x*.5f*r,Mathf.Cos(lat)*size.y*.5f,Mathf.Sin(a)*size.z*.5f*r);}
    for(int i=0;i<sides;i++){int k=(i+1)%sides;Tri(center+V(.04f*size.x,size.y*.5f,0),rings[0,k],rings[0,i],col);Tri(center-V(0,size.y*.5f,0),rings[bands-1,i],rings[bands-1,k],col);
     for(int j=0;j<bands-1;j++)Quad(rings[j,i],rings[j,k],rings[j+1,k],rings[j+1,i],col);}
    for(int i=first;i<v.Count;i++){var delta=v[i]-center;foliageNormals[i]=V(delta.x/(size.x*size.x),delta.y/(size.y*size.y),delta.z/(size.z*size.z)).normalized;}
   }
   internal void FadeTile(float radius,Color col)
   {Vector3[] a={V(-radius,.002f,-radius),V(-radius,.002f,radius),V(radius,.002f,radius),V(radius,.002f,-radius)};Color clear=col;clear.a=0;Quad(a[0]*.85f,a[1]*.85f,a[2]*.85f,a[3]*.85f,col);for(int i=0;i<4;i++){int j=(i+1)%4;int k=v.Count;v.Add(a[i]*.85f);v.Add(a[j]*.85f);v.Add(a[j]);v.Add(a[i]);c.Add(col);c.Add(col);c.Add(clear);c.Add(clear);t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}}
   internal void ClampCell(float half){for(int i=0;i<v.Count;i++){var p=v[i];p.x=Mathf.Clamp(p.x,-half,half);p.z=Mathf.Clamp(p.z,-half,half);p.y=Mathf.Max(0,p.y);v[i]=p;}}
   internal void HeightTint(int first,float low,float high,Color bottom,Color top){for(int i=first;i<v.Count;i++)c[i]=Color.Lerp(bottom,top,Mathf.InverseLerp(low,high,v[i].y));}
   internal Mesh Finish(string name){var m=new Mesh{name="Explore cached "+name};if(v.Count>65535)m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.SetVertices(v);m.SetTriangles(t,0);m.SetColors(c);m.RecalculateNormals();if(foliageNormals.Count>0){var normals=m.normals;foreach(var pair in foliageNormals)normals[pair.Key]=pair.Value;m.normals=normals;}m.RecalculateBounds();return m;}
  }
  public static GameObject CreateWall(ExTerrain terrain,int x,int z,float cell)
  {
   int n=Variant(x,z);var root=Root(terrain+" wall");var body=Part(root.transform,"wall/"+terrain+"/"+n,b=>Wall(b,terrain,n));body.name="WallBody";
   if(terrain==ExTerrain.Water){body.GetComponent<Renderer>().sharedMaterial=LakeMaterial;Part(root.transform,"water/details/"+n,b=>WaterDetails(b,n),Vector3.zero,true).name="WaterDetails";}
   if(terrain==ExTerrain.Thicket||terrain==ExTerrain.Cave)for(int i=0;i<2;i++){int k=i;Part(root.transform,"wall/looseVine/"+i,b=>b.Rod(V(k==0?-.7f:.7f,2.4f,1.43f),V(k==0?-.6f:.6f,1.1f,1.43f),.028f,Moss,4)).name="LooseVine_"+i;}
   if(terrain==ExTerrain.Palisade){Part(root.transform,"wall/flag",b=>b.Quad(V(-.4f,2.3f,1.48f),V(.4f,2.3f,1.48f),V(.3f,1.6f,1.48f),V(-.3f,1.7f,1.48f),Leaf,true)).name="WarBanner";Part(root.transform,"wall/boneCharm",b=>{b.Gem(V(0,2.06f,1.43f),V(.22f,.28f,.12f),Bone,4);for(int side=-1;side<=1;side+=2)b.Gem(V(side*.05f,2.08f,1.497f),V(.035f,.04f,.005f),Dark,4);}).name="BoneCharm";}
   root.transform.localScale=V(cell/3f,cell/3f*(terrain==ExTerrain.Thicket?1.4f:1),cell/3f);return root;
  }
  static void Wall(Geo b,ExTerrain t,int n)
  {
   float shift=(n-1.5f)*.09f;
   if(t==ExTerrain.Thicket)
   {
    // Four genuinely different arrangements, kept below 300 triangles including loose vines.
    for(int i=0;i<2;i++){float x=(i==0?-.6f:.55f);b.Rod(V(x,0,.1f),V(x+shift,2.3f-(n%2)*.25f,-.12f),.20f,Bark,4,.10f);}
    for(int i=0;i<2;i++){float a=i*3.1416f+n*.6f;b.Rod(V(0,.7f,0),V(Mathf.Cos(a)*1.35f,.04f,Mathf.Sin(a)*1.35f),.13f,Bark,4,.05f);}
    for(int i=0;i<5;i++){float a=i*2.399f+n*.65f;float r=i<2?.55f:.70f;float y=i<2?.7f+i*.16f:1.95f+(i%3)*.13f;var at=V(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);b.Crown(at,V(1.8f+(i%2)*.15f,1.38f,1.8f),Color.Lerp(Leaf,Moss,i<2?.48f:.12f*(i%3)),n*7+i,6,3);}
    for(int i=0;i<4;i++){float a=i*Mathf.PI/2+n*.3f;b.Frond(V(Mathf.Cos(a),.2f,Mathf.Sin(a)),V(Mathf.Cos(a)*1.35f,.85f,Mathf.Sin(a)*1.35f),.17f,Moss);}

   }
   else if(t==ExTerrain.Rock||t==ExTerrain.Cave)
   {
    var col=t==ExTerrain.Cave?new Color(.27f,.32f,.37f):Rock;b.Crag(Vector3.zero,V(3,2.8f,3),col,n);
    for(int i=0;i<4;i++){float a=i*Mathf.PI/2;var at=V(Mathf.Cos(a)*1.4f,.8f,Mathf.Sin(a)*1.4f);b.Frond(at,at+V(-Mathf.Sin(a)*.4f,.45f,Mathf.Cos(a)*.4f),.14f,t==ExTerrain.Cave?Glow(Jade):Moss);b.Gem(V(Mathf.Cos(a)*1.2f,.32f,Mathf.Sin(a)*1.2f),V(.65f,.64f,.65f),col);if(t==ExTerrain.Cave)b.Rod(V(Mathf.Cos(a)*.7f,2.8f,Mathf.Sin(a)*.7f),V(Mathf.Cos(a)*.7f,2.1f,Mathf.Sin(a)*.7f),.14f,col,4,0);}
   }
   else if(t==ExTerrain.Water)
   {
    b.Quad(V(-1.5f,.008f,-1.5f),V(-1.5f,.008f,1.5f),V(1.5f,.008f,1.5f),V(1.5f,.008f,-1.5f),Color.white);

   }
   else if(t==ExTerrain.Palisade)
   {
    for(int face=0;face<4;face++){var q=Quaternion.Euler(0,face*90,0);int start=b.v.Count;
     for(int i=0;i<4;i++){float x=-1.125f+i*.75f,h=2.05f+((i+n)%3)*.12f;b.Box(V(x,h*.5f,1.28f),V(.74f,h,.42f),Bark);var peak=V(x,h+.42f,1.28f);var c=V(x,h,1.28f);Vector3[] v={c+V(-.37f,0,-.21f),c+V(-.37f,0,.21f),c+V(.37f,0,.21f),c+V(.37f,0,-.21f)};for(int k=0;k<4;k++)b.Tri(v[k],v[(k+1)%4],peak,Bone);}
     for(int i=0;i<2;i++){float y=.65f+i*.85f;b.Quad(V(-1.5f,y,1.495f),V(1.5f,y,1.495f),V(1.5f,y+.045f,1.495f),V(-1.5f,y+.045f,1.495f),Bone);}for(int i=start;i<b.v.Count;i++)b.v[i]=q*b.v[i];
    }
   }
   else
   {
    for(int j=0;j<3;j++)for(int i=0;i<3;i++)b.Box(V((i-1)*1.01f,.39f+j*.76f,0),V(.98f,.72f,3),j%2==0?Rock:Jade*.7f);
    b.Vine(V(-.8f,2.4f,1.49f),V(.4f,.1f,1.49f),.18f,.045f,Leaf,4);for(int i=0;i<4;i++)b.Frond(V(-.5f+i*.15f,1.9f-i*.4f,1.48f),V(-.1f+i*.15f,2-i*.4f,1.49f),.12f,Moss);
   }
   b.ClampCell(1.5f);
  }
 }
}
