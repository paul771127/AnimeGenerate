using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Shared low-poly curved solids; no colliders, imported textures, per-instance materials or Update allocations.
    public static class EnemyArt
    {
        static Mesh round,spike;
        static readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        static readonly Dictionary<string,Mesh> batches=new Dictionary<string,Mesh>();
        public static Transform Pivot(Transform p,string n,Vector3 at)
        {var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=at;return t;}
        static Material Mat(Color c)
        {
            if(materials.TryGetValue(c,out var m)&&m)return m;ToonStyle.Apply();m=new Material(Shader.Find("SpellDuel/Toon")){color=c};
            // Reuse the existing vertex-color garment path, with facial lighting disabled.
            // The unbatched temporary pieces only supply colors to Bake; the white batch material renders them.
            if(c==Color.white){m.EnableKeyword("SD_HUMAN");m.SetFloat("_HumanHair",1);m.SetFloat("_HumanFace",0);m.SetFloat("_HumanOutline",1);}
            materials[c]=m;return m;
        }
        static Mesh Solid(bool pointed)
        {
            var v=new List<Vector3>();var tri=new List<int>();const int sides=6;int rings=pointed?1:3;
            for(int j=0;j<=rings;j++)for(int i=0;i<sides;i++)
            {float t=(float)j/rings,a=i*Mathf.PI*2/sides;float radius=pointed?(1-t)*.5f:Mathf.Sin(t*Mathf.PI)*.5f;v.Add(new Vector3(Mathf.Cos(a)*radius,pointed?t-.5f:-Mathf.Cos(t*Mathf.PI)*.5f,Mathf.Sin(a)*radius));}
            for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){int a=j*sides+i,b=j*sides+(i+1)%sides,c=a+sides,d=b+sides;tri.Add(a);tri.Add(c);tri.Add(b);tri.Add(b);tri.Add(c);tri.Add(d);}
            if(pointed)
            {
                // Close the broad base of fins, horns and blades; an open base exposes the black outline pass.
                int start=v.Count;for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*.5f,-.5f,Mathf.Sin(a)*.5f));}
                int center=v.Count;v.Add(new Vector3(0,-.5f,0));
                for(int i=0;i<sides;i++){tri.Add(center);tri.Add(start+i);tri.Add(start+(i+1)%sides);}
            }
            var m=new Mesh{name=pointed?"Tapered horn":"Curved six-sided solid"};m.SetVertices(v);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        public static Transform Part(Transform p,string n,Vector3 at,Vector3 size,Color c,bool pointed=false,Vector3 euler=default(Vector3))
        {
            if(!round)round=Solid(false);if(!spike)spike=Solid(true);
            var t=Pivot(p,n,at);t.localScale=size;t.localRotation=Quaternion.Euler(euler);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=pointed?spike:round;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Mat(c);return t;
        }
        public static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,Color c)
        {var t=Part(p,n,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),c);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        public static void Bake(Transform root,string key)
        {
            var solids=new List<Transform>();var pivots=new List<Transform>();
            for(int i=0;i<root.childCount;i++){var t=root.GetChild(i);if(t.GetComponent<MeshFilter>())solids.Add(t);else pivots.Add(t);}
            foreach(var t in pivots)Bake(t,key+"/"+t.name);
            if(solids.Count==0)return;
            if(!batches.TryGetValue(key,out var mesh)||!mesh)
            {
                var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var triangles=new List<int>();
                foreach(var t in solids)
                {
                    var m=t.GetComponent<MeshFilter>().sharedMesh;var vv=m.vertices;var nn=m.normals;var color=t.GetComponent<Renderer>().sharedMaterial.color;
                    var matrix=Matrix4x4.TRS(t.localPosition,t.localRotation,t.localScale);var normal=matrix.inverse.transpose;int start=vertices.Count;
                    for(int i=0;i<vv.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(vv[i]));normals.Add(normal.MultiplyVector(nn[i]).normalized);colors.Add(color);}
                    foreach(int i in m.triangles)triangles.Add(start+i);
                }
                mesh=new Mesh{name=key};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();batches[key]=mesh;
            }
            foreach(var t in solids){t.gameObject.SetActive(false);if(Application.isPlaying)Object.Destroy(t.gameObject);else Object.DestroyImmediate(t.gameObject);}
            var go=new GameObject("Batched "+root.name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Mat(Color.white);
        }
    }
}
