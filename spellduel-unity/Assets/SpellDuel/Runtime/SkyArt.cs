using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    // Shared procedural vocabulary; no primitive colliders, imported meshes or per-frame materials.
    internal static class SkyArt
    {
        static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        static Mesh oval, spindle, sail;
        static Material glow;
        public static readonly Color Ink = new Color(.12f,.16f,.24f), Cream = new Color(1,.91f,.72f), Gold = new Color(1,.68f,.22f), Teal = new Color(.12f,.72f,.65f);
        public static Material Mat(Color c)
        {
            if (!mats.TryGetValue(c,out var m) || !m) { m=new Material(ToonStyle.LitShader()){name="Sky cel",color=c}; mats[c]=m; }
            return m;
        }
        public static Material Glow { get { if(!glow) glow=new Material(Shader.Find("Sprites/Default")){name="Sky translucent"}; return glow; } }
        public static Transform Node(Transform p,string name,Vector3 at)
        { var t=new GameObject(name).transform;t.SetParent(p,false);t.localPosition=at;return t; }
        static Mesh Round(bool pointed)
        {
            var b=new ArtKit.MB(1); const int n=12,rings=7;
            for(int j=0;j<=rings;j++)for(int i=0;i<=n;i++)
            { float v=j/(float)rings,a=i*Mathf.PI*2/n,y=Mathf.Cos(v*Mathf.PI)*.5f,r=Mathf.Sin(v*Mathf.PI)*.5f;if(pointed)r*=1-v*.7f;
              var p=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r); b.Add(p,p.normalized,new Vector2(i/(float)n,v)); }
            for(int j=0;j<rings;j++)for(int i=0;i<n;i++){int a=j*(n+1)+i;b.Tri(0,a,a+1,a+n+1);b.Tri(0,a+1,a+n+2,a+n+1);}
            return b.Build(pointed?"Sky tapered oval":"Sky oval");
        }
        public static Transform Oval(Transform p,string name,Vector3 at,Vector3 size,Color c,bool pointed=false)
        {
            if(!oval)oval=Round(false);if(!spindle)spindle=Round(true);
            return MeshPart(p,name,pointed?spindle:oval,Mat(c),at,size);
        }
        public static Transform MeshPart(Transform p,string name,Mesh mesh,Material mat,Vector3 at,Vector3 size)
        { var t=Node(p,name,at);t.localScale=size;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return t; }
        public static Transform Rod(Transform p,string name,Vector3 a,Vector3 b,float width,Color c)
        { var t=Oval(p,name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),c); t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t; }
        public static LineRenderer Line(Transform p,string name,int points,float width,Color c,bool loop=false)
        { var t=Node(p,name,Vector3.zero);var l=t.gameObject.AddComponent<LineRenderer>();l.useWorldSpace=false;l.sharedMaterial=Glow;l.positionCount=points;l.startWidth=l.endWidth=width;l.startColor=l.endColor=c;l.loop=loop;l.numCornerVertices=2;l.numCapVertices=2;return l; }
        public static LineRenderer Ring(Transform p,string name,float radius,Color c,float width=.025f,int count=48)
        { var l=Line(p,name,count,width,c,true);for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;l.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}return l; }
        public static Transform Sail(Transform p,string name,Vector3 at,Vector3 size,Color c)
        {
            if(!sail){var b=new ArtKit.MB(1);var a=new Vector3(-.5f,0,0);var d=new Vector3(.5f,0,0);var top=new Vector3(0,1,0);var mid=new Vector3(0,.35f,.09f);b.Facet(0,a,mid,top);b.Facet(0,mid,d,top);b.Facet(0,top,mid,a);b.Facet(0,top,d,mid);sail=b.Build("Cambered triangular canvas");}
            return MeshPart(p,name,sail,Mat(c),at,size);
        }
        public static void MergeScenery(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var old=root.GetComponentsInChildren<MeshRenderer>();
            foreach(var r in old){if(r.name=="Wind streak")continue;var f=r.GetComponent<MeshFilter>();if(!f||!f.sharedMesh)continue;var mat=r.sharedMaterial;if(!groups.TryGetValue(mat,out var list)){list=new List<CombineInstance>();groups[mat]=list;}list.Add(new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});}
            var owner=root.gameObject.AddComponent<SkyMeshOwner>();
            foreach(var pair in groups){var mesh=new Mesh{name="Pooled scenery batch"};mesh.CombineMeshes(pair.Value.ToArray(),true,true);owner.Meshes.Add(mesh);MeshPart(root,"Batched scenery",mesh,pair.Key,Vector3.zero,Vector3.one);}
            foreach(var r in old)if(r.name!="Wind streak"){r.enabled=false;Dispose(r.gameObject);}
        }
        public static void Dispose(Object o){if(!o)return;if(Application.isPlaying)Object.Destroy(o);else Object.DestroyImmediate(o);}
    }
    internal sealed class SkyMeshOwner : MonoBehaviour
    {
        public readonly List<Mesh> Meshes=new List<Mesh>();
        void OnDestroy(){foreach(var mesh in Meshes)SkyArt.Dispose(mesh);}
    }
}
