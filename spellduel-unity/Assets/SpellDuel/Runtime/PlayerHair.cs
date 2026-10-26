using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel {
// One protagonist across sixteen outfits; named residents retain their own hair.
internal static class PlayerHair {
static Mesh cached;
internal static float ScalpDistance(Vector3 p) {float z=p.z+.009f,d=z>0?.137f:.158f;return Mathf.Sqrt(p.x*p.x/(.110f*.110f)+z*z/(d*d)+(p.y-.005f)*(p.y-.005f)/(.158f*.158f));}
static Vector3 Seat(Vector3 p){float q=ScalpDistance(p);if(q<1.012f){p=new Vector3(p.x,p.y-.005f,p.z+.009f)*(1.012f/Mathf.Max(.001f,q));p+=new Vector3(0,.005f,-.009f);}return p;}
// Signed radial clearance from the actual final skull, in the hair's local frame.
// Horizontal rays preserve vertex height when seating hair around the compacted head.
internal static float FinalScalpClearance(Vector3 point,Vector3[] skull,int[] triangles){
 var origin=new Vector3(0,point.y,-.009f);var delta=point-origin;float radius=delta.magnitude;
 if(radius<.00001f)return 1;var direction=delta/radius;float surface=-1;
 for(int i=0;i<triangles.Length;i+=3){
  var a=skull[triangles[i]];var b=skull[triangles[i+1]];var c=skull[triangles[i+2]];
  if(point.y<Mathf.Min(a.y,Mathf.Min(b.y,c.y))||point.y>Mathf.Max(a.y,Mathf.Max(b.y,c.y)))continue;
  var e=b-a;var f=c-a;var h=Vector3.Cross(direction,f);float determinant=Vector3.Dot(e,h);
  if(Mathf.Abs(determinant)<1e-8f)continue;float inv=1/determinant;var offset=origin-a;
  float u=Vector3.Dot(offset,h)*inv;if(u<0||u>1)continue;
  var q=Vector3.Cross(offset,e);float w=Vector3.Dot(direction,q)*inv;if(w<0||u+w>1)continue;
  float hit=Vector3.Dot(f,q)*inv;if(hit>0)surface=Mathf.Max(surface,hit);
 }
 return surface<0?1:radius-surface;
}
internal static void SealFinalScalp(Vector3[] hair,Vector3[] skull,int[] triangles){
 // Allow for interpolation across the shell's polygon faces, not just its vertices.
 const float clearance=.006f;
 for(int i=0;i<hair.Length;i++){
  float gap=FinalScalpClearance(hair[i],skull,triangles);
  if(gap>=clearance)continue;
  var outward=hair[i]-new Vector3(0,hair[i].y,-.009f);
  if(outward.sqrMagnitude>.00000001f)hair[i]+=outward.normalized*(clearance-gap);
 }
}
static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,float t)=>a*(1-t)*(1-t)+b*2*t*(1-t)+c*t*t;
static void Lock(List<Vector3> v,List<int> tri,List<Color> colors,Vector3 a,Vector3 b,Vector3 c,float width,float depth,float shade=1){
 int start=v.Count;const int rows=7,sides=8;
 for(int j=0;j<rows;j++){
  float u=j/(float)rows;var center=Curve(a,b,c,u);var dir=((b-a)*(1-u)+(c-b)*u).normalized;var right=Vector3.Cross(dir,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;var normal=Vector3.Cross(right,dir).normalized;
  float taper=Mathf.Pow(1-u,.76f)*(.86f+.14f*Mathf.Sin(u*Mathf.PI));
  for(int k=0;k<sides;k++){float angle=k*2*Mathf.PI/sides;var p=Seat(center+right*(Mathf.Cos(angle)*width*taper)+normal*(Mathf.Sin(angle)*depth*taper));v.Add(p);
   float band=Mathf.Exp(-Mathf.Pow((p.y-(.112f-.035f*p.x))/.016f,2));float tone=(.82f+band*.35f)*(Mathf.Sin(angle)<-.35f?.86f:1)*shade;colors.Add(new Color(tone,tone*.96f,tone*.88f,.75f));
   if(j<rows-1){int n=start+j*sides+k,m=start+j*sides+(k+1)%sides;tri.AddRange(new[]{n,n+sides,m,m,n+sides,m+sides});}
  }
 }
 int tip=v.Count;v.Add(Seat(c));colors.Add(new Color(.67f*shade,.63f*shade,.56f*shade,.75f));
 for(int k=0;k<sides;k++){int n=start+(rows-1)*sides+k,m=start+(rows-1)*sides+(k+1)%sides;tri.AddRange(new[]{n,tip,m});}
 for(int k=1;k<sides-1;k++)tri.AddRange(new[]{start,start+k,start+k+1});
}
internal static Mesh MeshFor(Portraits.Persona persona){
 if(cached)return cached;var v=new List<Vector3>();var tri=new List<int>();var colors=new List<Color>();const int sides=24,rows=8;
 // The shell is continuous from the fringe roots to the nape. A smooth
 // front/side transition avoids the old vertical "helmet" edges at the ears.
 v.Add(Seat(new Vector3(0,.171f,-.009f)));colors.Add(new Color(.65f,.61f,.55f));
 for(int j=1;j<=rows;j++)for(int i=0;i<sides;i++){
  float phi=i*2*Mathf.PI/sides,front=Mathf.Cos(phi);
  float limit=front>=0?Mathf.Lerp(1.65f,1.06f,Mathf.SmoothStep(0,1,front)):Mathf.Lerp(1.65f,1.94f,-front);
  limit+=.035f*Mathf.Sin(phi*7);float theta=j/(float)rows*limit;
  v.Add(Seat(new Vector3(Mathf.Sin(phi)*Mathf.Sin(theta)*.130f,Mathf.Cos(theta)*.175f-.004f,front*Mathf.Sin(theta)*.161f-.009f)));colors.Add(new Color(.65f,.61f,.55f));
  int next=(i+1)%sides;
  if(j==1)tri.AddRange(new[]{0,1+i,1+next});
  else {int n=1+(j-2)*sides+i,m=1+(j-2)*sides+next;tri.AddRange(new[]{n,n+sides,m,m,n+sides,m+sides});}
 }
 // Broad overlapping crown sweeps share an off-centre part. The end points
 // alternate slightly so the silhouette reads as a tousled short haircut.
 for(int i=0;i<11;i++){
  float a=(i+.28f)*2*Mathf.PI/11,s=Mathf.Sin(a),f=Mathf.Cos(a);
  Lock(v,tri,colors,new Vector3(.027f+s*.023f,.166f,f*.026f-.016f),
   new Vector3(s*.107f-.019f,.184f+(i%3)*.003f,f*.116f-.013f),
   new Vector3(s*(.137f+(i%2)*.009f)-.008f,f>.3f?.078f:-.038f-(i%3)*.009f,f*.156f-.013f),.030f,.020f,i%3==0?.93f:1);
 }
 // Seven deliberate, asymmetric locks replace the evenly spaced tiny teeth.
 // The small centre opening leaves the eyes and their existing brow animation readable.
 float[] tipX={-.117f,-.085f,-.046f,-.009f,.047f,.086f,.119f};
 float[] tipY={.021f,.032f,.040f,.053f,.026f,.012f,.002f};
 for(int i=0;i<tipX.Length;i++){
  float x=tipX[i],s=i<4?-1:1;
  Lock(v,tri,colors,new Vector3(.023f+s*.009f,.153f,.068f),
   new Vector3(x*.68f,.133f+(i%2)*.007f,.157f),
   new Vector3(x,tipY[i],.143f-Mathf.Abs(x)*.16f),i==3?.022f:.029f,.019f,i==1||i==5?.93f:1);
 }
 // Short temple and nape locks have circular volume even in the side view.
 for(int s=-1;s<=1;s+=2){
  Lock(v,tri,colors,new Vector3(s*.102f,.087f,.045f),new Vector3(s*.127f,.017f,.070f),new Vector3(s*.109f,-.046f,.055f),.014f,.013f,.92f);
  for(int j=0;j<2;j++)Lock(v,tri,colors,new Vector3(s*.103f,.084f,-.077f-j*.030f),
   new Vector3(s*(.127f-j*.008f),.012f,-.080f-j*.038f),
   new Vector3(s*(.099f-j*.012f),-.067f-j*.004f,-.072f-j*.045f),.022f,.017f,.90f);
 }
 float reference=AnimeHair.MeshFor("player_identity",persona).bounds.max.y;AnimeHair.KeepHairTop(v,reference);
 cached=new Mesh{name="Player sculpted swept hair"};cached.SetVertices(v);cached.SetTriangles(tri,0);cached.colors=colors.ToArray();cached.RecalculateNormals();cached.RecalculateBounds();return cached;
}
}}
