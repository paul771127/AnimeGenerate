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
   internal readonly List<Vector3> v=new List<Vector3>();readonly List<int> t=new List<int>();readonly List<Color> c=new List<Color>();
   internal void Tri(Vector3 a,Vector3 b,Vector3 d,Color col){int n=v.Count;v.Add(a);v.Add(b);v.Add(d);c.Add(col);c.Add(col);c.Add(col);t.Add(n);t.Add(n+1);t.Add(n+2);}
   internal void Quad(Vector3 a,Vector3 b,Vector3 d,Vector3 e,Color col,bool both=false){Tri(a,b,d,col);Tri(a,d,e,col);if(both){Tri(d,b,a,col);Tri(e,d,a,col);}}
   internal void Box(Vector3 p,Vector3 s,Color col){var a=p-s*.5f;var b=p+s*.5f;Quad(V(a.x,a.y,b.z),V(b.x,a.y,b.z),V(b.x,b.y,b.z),V(a.x,b.y,b.z),col);Quad(V(b.x,a.y,a.z),V(a.x,a.y,a.z),V(a.x,b.y,a.z),V(b.x,b.y,a.z),col);Quad(V(a.x,a.y,a.z),V(a.x,a.y,b.z),V(a.x,b.y,b.z),V(a.x,b.y,a.z),col);Quad(V(b.x,a.y,b.z),V(b.x,a.y,a.z),V(b.x,b.y,a.z),V(b.x,b.y,b.z),col);Quad(V(a.x,b.y,b.z),V(b.x,b.y,b.z),V(b.x,b.y,a.z),V(a.x,b.y,a.z),col);Quad(V(a.x,a.y,a.z),V(b.x,a.y,a.z),V(b.x,a.y,b.z),V(a.x,a.y,b.z),col);}
   internal void Rod(Vector3 a,Vector3 b,float r,Color col,int sides=5,float end=-1){if(end<0)end=r;var q=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);for(int i=0;i<sides;i++){float u=i*2*Mathf.PI/sides,w=(i+1)*2*Mathf.PI/sides;var p=q*V(Mathf.Cos(u),0,Mathf.Sin(u));var n=q*V(Mathf.Cos(w),0,Mathf.Sin(w));Quad(a+p*r,b+p*end,b+n*end,a+n*r,col);Tri(a,a+p*r,a+n*r,col);Tri(b,b+n*end,b+p*end,col);}}
   internal void Gem(Vector3 p,Vector3 size,Color col,int sides=6){var top=p+V(0,size.y*.5f,0);var bot=p-V(0,size.y*.5f,0);for(int i=0;i<sides;i++){float a=i*2*Mathf.PI/sides,b=(i+1)*2*Mathf.PI/sides;var u=p+V(Mathf.Cos(a)*size.x*.5f,0,Mathf.Sin(a)*size.z*.5f);var w=p+V(Mathf.Cos(b)*size.x*.5f,0,Mathf.Sin(b)*size.z*.5f);Tri(top,w,u,col);Tri(bot,u,w,col);}}
   internal void Frond(Vector3 a,Vector3 tip,float width,Color col){var side=Vector3.Cross((tip-a).normalized,Vector3.up).normalized*width;var mid=Vector3.Lerp(a,tip,.48f)+Vector3.up*.12f;Quad(a,mid-side,tip,mid+side,col,true);}
   internal void Vine(Vector3 a,Vector3 b,float bend,float radius,Color col,int steps=5){var prev=a;for(int i=1;i<=steps;i++){float u=i/(float)steps;var p=Vector3.Lerp(a,b,u)+V(Mathf.Sin(u*Mathf.PI)*bend,0,Mathf.Sin(u*Mathf.PI*2)*bend*.3f);Rod(prev,p,radius,col,4,radius*.94f);prev=p;}}
   internal void Ring(Vector3 p,float radius,float width,Color col,int sides=20){for(int i=0;i<sides;i++){float a=i*2*Mathf.PI/sides,b=(i+1)*2*Mathf.PI/sides;Quad(p+V(Mathf.Cos(a)*(radius-width),0,Mathf.Sin(a)*(radius-width)),p+V(Mathf.Cos(b)*(radius-width),0,Mathf.Sin(b)*(radius-width)),p+V(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),p+V(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),col);}}
   internal void FadeTile(float radius,Color col)
   {Vector3[] a={V(-radius,.002f,-radius),V(-radius,.002f,radius),V(radius,.002f,radius),V(radius,.002f,-radius)};Color clear=col;clear.a=0;Quad(a[0]*.85f,a[1]*.85f,a[2]*.85f,a[3]*.85f,col);for(int i=0;i<4;i++){int j=(i+1)%4;int k=v.Count;v.Add(a[i]*.85f);v.Add(a[j]*.85f);v.Add(a[j]);v.Add(a[i]);c.Add(col);c.Add(col);c.Add(clear);c.Add(clear);t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}}
   internal void ClampCell(float half){for(int i=0;i<v.Count;i++){var p=v[i];p.x=Mathf.Clamp(p.x,-half,half);p.z=Mathf.Clamp(p.z,-half,half);p.y=Mathf.Max(0,p.y);v[i]=p;}}
   internal Mesh Finish(string name){var m=new Mesh{name="Explore cached "+name};if(v.Count>65535)m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.SetVertices(v);m.SetTriangles(t,0);m.SetColors(c);m.RecalculateNormals();m.RecalculateBounds();return m;}
  }
  public static GameObject CreateWall(ExTerrain terrain,int x,int z,float cell)
  {
   int n=Variant(x,z);var root=Root(terrain+" wall");Part(root.transform,"wall/"+terrain+"/"+n,b=>Wall(b,terrain,n)).name="WallBody";
   if(terrain==ExTerrain.Thicket||terrain==ExTerrain.Cave)for(int i=0;i<2;i++){int k=i;Part(root.transform,"wall/looseVine/"+i,b=>b.Rod(V(k==0?-.7f:.7f,2.4f,1.43f),V(k==0?-.6f:.6f,1.1f,1.43f),.028f,Moss,4)).name="LooseVine_"+i;}
   if(terrain==ExTerrain.Palisade){Part(root.transform,"wall/flag",b=>b.Quad(V(-.4f,2.3f,1.48f),V(.4f,2.3f,1.48f),V(.3f,1.6f,1.48f),V(-.3f,1.7f,1.48f),Leaf,true)).name="WarBanner";Part(root.transform,"wall/boneCharm",b=>{b.Gem(V(0,2.06f,1.43f),V(.22f,.28f,.12f),Bone,4);for(int side=-1;side<=1;side+=2)b.Gem(V(side*.05f,2.08f,1.497f),V(.035f,.04f,.005f),Dark,4);}).name="BoneCharm";}
   root.transform.localScale=Vector3.one*(cell/3f);return root;
  }
  static void Wall(Geo b,ExTerrain t,int n)
  {
   float shift=(n-1.5f)*.09f;
   if(t==ExTerrain.Thicket)
   {
    b.Box(V(0,.45f,0),V(3,.9f,3),Leaf);for(int i=0;i<3;i++){float x=(i-1)*.96f;b.Rod(V(x,0,.2f),V(x+shift,2.65f,-.1f),.20f,Bark,5,.13f);b.Gem(V(x,1.6f,-.6f),V(1.7f,1.6f,1.8f),i%2==0?Leaf:Moss);b.Gem(V(x,2.4f,.55f),V(1.7f,1,1.8f),Leaf);b.Frond(V(x,.7f,1.3f),V(x+.4f,1.4f,1.42f),.36f,Moss);b.Rod(V(x,2.3f,1.2f),V(x+shift,.6f,1.3f),.035f,Moss,4);}
   }
   else if(t==ExTerrain.Rock||t==ExTerrain.Cave)
   {
    var col=t==ExTerrain.Cave?new Color(.27f,.32f,.37f):Rock;
    for(int j=0;j<3;j++){b.Box(V(shift*(j-1),.48f+j*.85f,0),V(3,.96f,3),col*(1-j*.06f));for(int i=0;i<2;i++)b.Gem(V((i-.5f)*1.5f,.7f+j*.85f,1.3f),V(1.4f,1, .5f),col);}
    for(int i=0;i<3;i++){float x=i-1;b.Frond(V(x,.8f,1.48f),V(x+.4f,1.25f,1.45f),.18f,t==ExTerrain.Cave?Glow(Jade):Moss);if(t==ExTerrain.Cave)b.Rod(V(x,3,0),V(x+.12f,2.2f,0),.19f,col,4,0);}
   }
   else if(t==ExTerrain.Water)
   {
    b.Box(V(0,.006f,0),V(3,.012f,3),Water);for(int i=0;i<4;i++){float z=-1.1f+i*.7f;b.Quad(V(-1.2f,.019f,z),V(-.2f,.019f,z+.07f),V(.8f,.019f,z+.035f),V(-.1f,.019f,z-.015f),Glow(Light));}b.Gem(V(.65f,.035f,.55f),V(.65f,.04f,.55f),Leaf,7);b.Gem(V(.7f,.15f,.57f),V(.16f,.22f,.16f),Bone,5);for(int i=0;i<3;i++){var tip=V(-1.35f+i*.11f,.36f+i*.09f,1.35f);b.Rod(V(tip.x,.02f,tip.z),tip,.012f,Moss,4);b.Frond(tip*.7f,tip+V(.12f,.02f,0),.035f,Leaf);}
   }
   else if(t==ExTerrain.Palisade)
   {
    b.Box(V(0,1,0),V(3,2,2.7f),Bark*.8f);for(int i=0;i<6;i++){float x=-1.25f+i*.5f;b.Rod(V(x,0,1.18f),V(x,2.2f+(i+n)%3*.16f,1.18f),.27f,Bark,5,.20f);b.Rod(V(x,2.2f+(i+n)%3*.16f,1.18f),V(x,2.65f+(i+n)%3*.16f,1.18f),.20f,Bone,4,0);}for(int i=0;i<2;i++)b.Rod(V(-1.5f,.7f+i*.85f,1.43f),V(1.5f,.7f+i*.85f,1.43f),.05f,Bone,4);
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
