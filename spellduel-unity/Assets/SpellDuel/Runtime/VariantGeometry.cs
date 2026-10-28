using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 // Geometry vocabulary only. These enemies never instantiate a player or minion rig.
 internal static class VariantGeometry
 {
  static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
  static readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
  static Material ghost;
  internal static readonly Color Ink=new Color(.15f,.12f,.19f), Red=new Color(.83f,.075f,.14f), Metal=new Color(.58f,.64f,.69f), Gold=new Color(.75f,.46f,.16f), Skin=new Color(.91f,.69f,.53f);
  internal static Material Mat(Color c){if(!materials.TryGetValue(c,out var m)||!m){m=new Material(c.a<.99f?Shader.Find("Sprites/Default"):ToonStyle.LitShader()){name="Enemy cel",color=c};materials[c]=m;}return m;}
  internal static Material Ghost{get{if(!ghost)ghost=new Material(Shader.Find("Sprites/Default")){name="Enemy translucent silhouette"};return ghost;}}
  internal static Transform Node(Transform p,string name,Vector3 at){var t=new GameObject(name).transform;t.SetParent(p,false);t.localPosition=at;return t;}
  internal static Transform Part(Transform p,string name,Mesh mesh,Vector3 at,Vector3 scale,Color color)
  {var t=Node(p,name,at);t.localScale=scale;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=Mat(color);return t;}
  internal static Mesh Loft(string key,float[] ys,float[] widths,float[] depths,int sides=10)
  {
   if(meshes.TryGetValue(key,out var mesh)&&mesh)return mesh;
   var v=new List<Vector3>();var tr=new List<int>();
   for(int j=0;j<ys.Length;j++)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*widths[j],ys[j],Mathf.Sin(a)*depths[j]));}
   for(int j=0;j<ys.Length-1;j++)for(int i=0;i<sides;i++){int a=j*sides+i,b=j*sides+(i+1)%sides,c=a+sides,d=b+sides;tr.Add(a);tr.Add(c);tr.Add(b);tr.Add(b);tr.Add(c);tr.Add(d);}
   int bottom=v.Count;v.Add(new Vector3(0,ys[0],0));int top=v.Count;v.Add(new Vector3(0,ys[ys.Length-1],0));
   for(int i=0;i<sides;i++){tr.Add(bottom);tr.Add(i);tr.Add((i+1)%sides);tr.Add(top);tr.Add((ys.Length-1)*sides+(i+1)%sides);tr.Add((ys.Length-1)*sides+i);}
   mesh=new Mesh{name="Enemy "+key};mesh.SetVertices(v);mesh.SetTriangles(tr,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[key]=mesh;return mesh;
  }
  internal static Mesh Round{get{return Loft("round",new[]{-.5f,-.4f,-.2f,.2f,.4f,.5f},new[]{.015f,.3f,.46f,.46f,.3f,.015f},new[]{.015f,.3f,.46f,.46f,.3f,.015f},8);}}
  internal static Transform Oval(Transform p,string name,Vector3 at,Vector3 scale,Color c){return Part(p,name,Round,at,scale,c);}
  internal static Transform Rod(Transform p,string name,Vector3 a,Vector3 b,float width,Color c)
  {var mesh=Loft("rounded strut",new[]{-.5f,0f,.5f},new[]{.3f,.5f,.3f},new[]{.3f,.5f,.3f},6);var t=Part(p,name,mesh,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),c);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t;}
  internal static Mesh Cloth{get{return Loft("tailored coat",new[]{-.5f,-.28f,0,.28f,.5f},new[]{.46f,.43f,.33f,.42f,.36f},new[]{.28f,.27f,.24f,.28f,.22f},12);}}
  internal static Mesh Head{get{return Loft("sculpted enemy face",new[]{-.5f,-.42f,-.24f,.02f,.26f,.43f,.5f},new[]{.08f,.2f,.34f,.43f,.40f,.28f,.02f},new[]{.1f,.23f,.33f,.38f,.37f,.28f,.02f},12);}}
  internal static Transform Blade(Transform p,string name,Vector3 at,float length,float width,Color c)
  {var m=Loft("bevel blade",new[]{0f,.12f,.72f,1f},new[]{.1f,.5f,.36f,0f},new[]{.05f,.12f,.08f,0f},4);return Part(p,name,m,at,new Vector3(width,length,width),c);}
  internal static Transform ClothPanel(Transform p,string name,Vector3 at,Vector3 scale,Color c)
  {
   if(!meshes.TryGetValue("banner",out var m)||!m){m=new Mesh{name="Enemy curved banner"};var v=new List<Vector3>();var t=new List<int>();for(int y=0;y<4;y++)for(int x=0;x<4;x++)v.Add(new Vector3(x/3f,-y/3f,.08f*Mathf.Sin(x/3f*5+y)));
    for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=y*4+x;foreach(int n in new[]{a,a+4,a+1,a+1,a+4,a+5,a+1,a+4,a,a+5,a+4,a+1})t.Add(n);}m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();meshes["banner"]=m;}
   return Part(p,name,m,at,scale,c);
  }
  internal static void Dispose(Object o){if(!o)return;if(Application.isPlaying)Object.Destroy(o);else Object.DestroyImmediate(o);}
  // Bake only within a joint, keeping the skeleton animated. Identical enemies reuse cached meshes.
  internal static void Batch(int id,Transform[] bones)
  {
   var set=new HashSet<Transform>(bones);
   foreach(var bone in bones){var groups=new Dictionary<Material,List<CombineInstance>>();var old=new List<GameObject>();
    foreach(var f in bone.GetComponentsInChildren<MeshFilter>()){var ancestor=f.transform.parent;while(ancestor&&ancestor!=bone&&!set.Contains(ancestor))ancestor=ancestor.parent;if(ancestor!=bone)continue;
     var r=f.GetComponent<MeshRenderer>();if(!r)continue;var mat=r.sharedMaterial;if(!groups.TryGetValue(mat,out var list)){list=new List<CombineInstance>();groups[mat]=list;}list.Add(new CombineInstance{mesh=f.sharedMesh,transform=bone.worldToLocalMatrix*f.transform.localToWorldMatrix});old.Add(f.gameObject);}
    foreach(var pair in groups){string key=id+"/"+bone.name+"/"+ColorUtility.ToHtmlStringRGBA(pair.Key.color);if(!meshes.TryGetValue(key,out var mesh)||!mesh){mesh=new Mesh{name="Enemy joint "+key};mesh.CombineMeshes(pair.Value.ToArray(),true,true);meshes[key]=mesh;}Part(bone,"Joint surface",mesh,Vector3.zero,Vector3.one,pair.Key.color);}
    foreach(var go in old){go.SetActive(false);Dispose(go);}
   }
  }
 }
}
