using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  static readonly Dictionary<string,List<List<Vector2>>> lakeContours=new Dictionary<string,List<List<Vector2>>>();
  static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static void RoundedLake(Transform root,ExLandmark l,ExploreMap map,Vector3 centre)
  {
   var cells=new HashSet<Vector2Int>();var key="lake/rounded/"+ExploreMap.CellSize+"/";
   for(int x=l.rect.xMin-1;x<=l.rect.xMax+1;x++)for(int z=l.rect.yMin-1;z<=l.rect.yMax+1;z++)if(map.InGrid(x,z)&&map.TerrainAt(x,z)==ExTerrain.Water){cells.Add(new Vector2Int(x,z));key+=x+","+z+";";}
   if(!lakeContours.TryGetValue(key,out var contours)){contours=LakeContours(cells,map,centre);lakeContours[key]=contours;}
   var surface=Part(root,key,b=>{foreach(var loop in contours)FillLake(b,loop);});surface.name="LakeSurface";
   var mesh=surface.GetComponent<MeshFilter>().sharedMesh;if(mesh.uv.Length==0){var v=mesh.vertices;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++)uv[i]=new Vector2(v[i].x,v[i].z);mesh.uv=uv;}
   surface.GetComponent<Renderer>().sharedMaterial=LakeMaterial;
   Part(root,key+"/shore",b=>{
    foreach(var loop in contours){float accumulated=0;int plant=0;for(int i=0;i<loop.Count;i++){
     var a=loop[i];var c=loop[(i+1)%loop.Count];var na=LakeOutward(loop,i);var nc=LakeOutward(loop,(i+1)%loop.Count);
     float wa=.24f+.05f*Mathf.Sin(i*.73f),wc=.24f+.05f*Mathf.Sin((i+1)%loop.Count*.73f);
     b.Quad(V(a.x,.023f,a.y),V(c.x,.023f,c.y),V(c.x+nc.x*wc,.024f,c.y+nc.y*wc),V(a.x+na.x*wa,.024f,a.y+na.y*wa),new Color(.31f,.43f,.25f));
     accumulated+=(c-a).magnitude;
     if(accumulated>.75f+.25f*Mathf.Sin(plant*1.7f)){
      accumulated=0;var at=V(a.x-na.x*.12f,.03f,a.y-na.y*.12f);float size=.35f+.18f*(.5f+.5f*Mathf.Sin(plant*2.3f));
      if(plant%3!=0)b.Crown(at,V(size,.18f+(plant%3)*.06f,size*.8f),Color.Lerp(Rock,Moss,.25f),plant,7,2);
      if(plant%2==0)for(int n=0;n<4;n++){var foot=at+V((n-1.5f)*.045f,0,.04f*Mathf.Sin(n));b.Rod(foot,foot+V(.04f,.45f+n*.08f,.03f),.009f,Moss,4);b.Frond(foot+Vector3.up*.12f,foot+V(.18f,.40f,.06f),.028f,Leaf);}
      plant++;
     }
    }}
   }).name="Shore_CurvedContour";
  }
  static List<List<Vector2>> LakeContours(HashSet<Vector2Int> cells,ExploreMap map,Vector3 centre)
  {
   var edges=new Dictionary<Vector2Int,List<Vector2Int>>();Action<Vector2Int,Vector2Int> add=(a,b)=>{if(!edges.TryGetValue(a,out var list)){list=new List<Vector2Int>();edges[a]=list;}list.Add(b);};
   foreach(var c in cells){int x=c.x*2,z=c.y*2;var a=new Vector2Int(x-1,z-1);var b=new Vector2Int(x+1,z-1);var d=new Vector2Int(x+1,z+1);var e=new Vector2Int(x-1,z+1);
    if(!cells.Contains(c+Vector2Int.down))add(a,b);if(!cells.Contains(c+Vector2Int.right))add(b,d);if(!cells.Contains(c+Vector2Int.up))add(d,e);if(!cells.Contains(c+Vector2Int.left))add(e,a);
   }
   var result=new List<List<Vector2>>();var origin=map.World(0,0)-centre;float scale=ExploreMap.CellSize*.5f;
   while(edges.Count>0)
   {
    Vector2Int start=default(Vector2Int);foreach(var pair in edges){start=pair.Key;break;}var raw=new List<Vector2>();var at=start;int guard=0;
    do {raw.Add(new Vector2(at.x*scale+origin.x,at.y*scale+origin.z));if(!edges.TryGetValue(at,out var next))throw new InvalidOperationException("Open lake contour");var to=next[0];next.RemoveAt(0);if(next.Count==0)edges.Remove(at);at=to;if(++guard>10000)throw new InvalidOperationException("Lake contour loop");}while(at!=start);
    for(int i=raw.Count-1;i>=0;i--){var prev=raw[(i+raw.Count-1)%raw.Count];var here=raw[i];var next=raw[(i+1)%raw.Count];if(Mathf.Abs(Cross(here-prev,next-here))<.001f)raw.RemoveAt(i);}
    var curve=new List<Vector2>();
    for(int i=0;i<raw.Count;i++){
     var before=raw[(i+raw.Count-1)%raw.Count];var corner=raw[i];var after=raw[(i+1)%raw.Count];var incoming=(corner-before).normalized;var outgoing=(after-corner).normalized;
     bool convex=Cross(incoming,outgoing)>0;
     // Broad circular bends replace tiny fillets: the 20x12m lake uses a 5.5m radius.
     // Keep narrow/concave shapes safe by limiting to 46% of either adjoining edge.
     float span=convex?ExploreMap.CellSize*1.375f:1.25f;
     span=Mathf.Min(span,Mathf.Min((corner-before).magnitude,(after-corner).magnitude)*.46f);
     var a=corner-incoming*span;var b=corner+outgoing*span;
     if(curve.Count>0)SmoothLakeEdge(curve,curve[curve.Count-1],a);
     // Circular convex corners have continuous tangents at both banks. Concave
     // corners retain the smaller quadratic blend within the blocked margin.
     int samples=Mathf.Max(12,Mathf.CeilToInt(span*Mathf.PI*.5f/.28f));
     var arcCentre=a+outgoing*span;
     for(int j=0;j<=samples;j++){
      float t=j/(float)samples;
      curve.Add(convex?arcCentre-outgoing*(span*Mathf.Cos(t*Mathf.PI*.5f))+incoming*(span*Mathf.Sin(t*Mathf.PI*.5f))
       :(1-t)*(1-t)*a+2*(1-t)*t*corner+t*t*b);
     }
    }
    SmoothLakeEdge(curve,curve[curve.Count-1],curve[0]);result.Add(curve);
   }
   return result;
  }
  static Vector2 LakeOutward(List<Vector2> loop,int i)
  {var tangent=(loop[(i+1)%loop.Count]-loop[(i+loop.Count-1)%loop.Count]).normalized;return new Vector2(tangent.y,-tangent.x);}
  static void SmoothLakeEdge(List<Vector2> output,Vector2 a,Vector2 b)
  {
   float amplitude=.34f*Mathf.Clamp01((b-a).sqrMagnitude/64f);int steps=Mathf.CeilToInt((b-a).magnitude/.35f);var dir=(b-a).normalized;var inward=new Vector2(-dir.y,dir.x);
   for(int i=1;i<steps;i++){float t=i/(float)steps;output.Add(Vector2.Lerp(a,b,t)+inward*(amplitude*Mathf.Pow(Mathf.Sin(t*Mathf.PI),2)*(.65f+.35f*Mathf.Sin(t*Mathf.PI*4))));}
  }
  static void FillLake(Geo b,List<Vector2> loop)
  {
   var ids=new List<int>();for(int i=0;i<loop.Count;i++)if(i==0||(loop[i]-loop[i-1]).sqrMagnitude>.000001f)ids.Add(i);
   int attempts=0;
   while(ids.Count>2){bool cut=false;for(int k=0;k<ids.Count;k++){
    int ia=ids[(k+ids.Count-1)%ids.Count],ib=ids[k],ic=ids[(k+1)%ids.Count];var a=loop[ia];var c=loop[ib];var d=loop[ic];float turn=Cross(c-a,d-c);if(Mathf.Abs(turn)<.000001f){ids.RemoveAt(k);cut=true;break;}if(turn<0)continue;
    bool inside=false;foreach(int ix in ids){if(ix==ia||ix==ib||ix==ic)continue;var p=loop[ix];if(Cross(c-a,p-a)>.000001f&&Cross(d-c,p-c)>.000001f&&Cross(a-d,p-d)>.000001f){inside=true;break;}}
    if(inside)continue;b.Tri(V(a.x,.012f,a.y),V(d.x,.012f,d.y),V(c.x,.012f,c.y),Color.white);ids.RemoveAt(k);cut=true;break;
   }if(!cut||++attempts>20000)throw new InvalidOperationException("Cannot triangulate lake contour");}
  }
 }
}
