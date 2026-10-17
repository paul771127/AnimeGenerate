using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>Procedural cel-painted town sets. Positions remain in the caller's local space.
    /// Static pieces are combined per material at each public entry point; no colliders or art assets.</summary>
    public static partial class TownArt
    {
        static readonly Color Wood = C("865334"), Gold = C("e7b756"), Cream = C("f1dfb4"), Ink = C("344354");
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly Dictionary<string, Mesh> shapes = new Dictionary<string, Mesh>();
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);
        static Transform Group(Transform p, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform; t.SetParent(p, false); t.localPosition = pos; return t;
        }
        static Material Mat(Color c, bool glowing = false)
        {
            // Explicit shader key: opaque/glow colors cannot collide after Color32 quantization.
            string key = ColorUtility.ToHtmlStringRGBA(c) + (glowing ? " glow" : " toon");
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            var shader = glowing ? Shader.Find("Sprites/Default") : ToonStyle.LitShader();
            m = new Material(shader ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Sprites/Default")) { color = c, name = "Town " + key };
            mats[key] = m; return m;
        }
        static GameObject Part(Transform p, string name, Mesh mesh, Vector3 pos, Vector3 scale, Color color, bool glow = false)
        {
            var go = new GameObject(name); go.transform.SetParent(p, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color, glow); return go;
        }
        static Mesh Shape(string name)
        {
            if (shapes.TryGetValue(name, out var m)) return m;
            if (name == "box")
            {
                // Separate face vertices retain crisp cel planes; averaged corner normals close outline gaps.
                var v = new List<Vector3>(); var n = new List<Vector3>(); var tris = new List<int>();
                Vector3[] corners = { new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f) };
                int[] faces = {0,3,2,1,4,5,6,7,0,4,7,3,1,2,6,5,3,7,6,2,0,1,5,4};
                foreach (int k in faces) { v.Add(corners[k]); n.Add(corners[k].normalized); }
                for(int f=0;f<6;f++){int k=f*4;tris.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
                m = new Mesh {name="Town smooth-normal box"};m.SetVertices(v);m.SetNormals(n);m.SetTriangles(tris,0);m.RecalculateBounds();
            }
            else if (name == "roof")
            {
                m = new Mesh {name="Town gable"};m.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(0,1,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,1,.5f)};
                m.triangles=new[]{0,2,1,3,4,5,0,3,5,0,5,2,1,2,5,1,5,4,0,1,4,0,4,3};m.RecalculateNormals();m.RecalculateBounds();
            }
            else
            {
                // Lathed silhouettes, including inexpensive spheres and peaked straw hats.
                int sides = name == "sphere" ? 12 : 16;
                Vector2[] profile = name == "sphere" ? new[]{new Vector2(0,-.5f),new Vector2(.25f,-.433f),new Vector2(.433f,-.25f),new Vector2(.5f,0),new Vector2(.433f,.25f),new Vector2(.25f,.433f),new Vector2(0,.5f)} :
                    name == "cone" ? new[]{new Vector2(0,0),new Vector2(.5f,0),new Vector2(0,1)} :
                    name == "bell" ? new[]{new Vector2(0,-.5f),new Vector2(.5f,-.5f),new Vector2(.43f,-.33f),new Vector2(.3f,.25f),new Vector2(.16f,.5f),new Vector2(0,.5f)} :
                    new[]{new Vector2(0,-1),new Vector2(.5f,-1),new Vector2(.5f,1),new Vector2(0,1)};
                var v=new List<Vector3>();var t=new List<int>();
                for(int j=0;j<profile.Length;j++)for(int i=0;i<=sides;i++){float a=i*2*Mathf.PI/sides;v.Add(new Vector3(Mathf.Cos(a)*profile[j].x,profile[j].y,Mathf.Sin(a)*profile[j].x));}
                for(int j=0;j<profile.Length-1;j++)for(int i=0;i<sides;i++){int k=j*(sides+1)+i;t.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});}
                m=new Mesh{name="Town "+name};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();
                // Weld duplicated seam normals without changing topology.
                var norms=m.normals;for(int j=0;j<profile.Length;j++){int k=j*(sides+1);var avg=(norms[k]+norms[k+sides]).normalized;norms[k]=norms[k+sides]=avg;}m.normals=norms;m.RecalculateBounds();
            }
            shapes[name]=m;return m;
        }
        public static GameObject Box(Transform p,string n,Vector3 pos,Vector3 s,Color c) => Part(p,n,Shape("box"),pos,s,c);
        public static GameObject Cyl(Transform p,string n,Vector3 pos,Vector3 s,Color c) => Part(p,n,Shape("cylinder"),pos,s,c);
        public static GameObject Ball(Transform p,string n,Vector3 pos,Vector3 s,Color c) => Part(p,n,Shape("sphere"),pos,s,c);
        public static GameObject Glow(Transform p,string n,Vector3 pos,float size,Color c) => Part(p,n,Shape("sphere"),pos,Vector3.one*size,c,true);
        static GameObject Cone(Transform p,string n,Vector3 pos,Vector3 s,Color c) => Part(p,n,Shape("cone"),pos,s,c);
        static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,Color c)
        {
            var go=Box(p,n,(a+b)*.5f,new Vector3(width,width,(b-a).magnitude),c);
            go.transform.localRotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);
        }
        static void Ring(Transform p,string name,float radius,float width,float y,Color c,bool glow=false,int count=32)
        {
            for(int i=0;i<count;i++)
            {
                float a=i*2*Mathf.PI/count,b=(i+1)*2*Mathf.PI/count;
                var start=new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);var end=new Vector3(Mathf.Cos(b)*radius,y,Mathf.Sin(b)*radius);
                var go=Part(p,name,Shape("box"),(start+end)*.5f,new Vector3(width,.012f,(end-start).magnitude+width*.3f),c,glow);
                go.transform.localRotation=Quaternion.LookRotation(end-start,Vector3.up);
            }
        }
        // Combine only this call's group. Animated children are created/batched independently.
        static void Batch(Transform root)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();var source=new List<GameObject>();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())
            {
                var r=f.GetComponent<MeshRenderer>();if(r==null||f.sharedMesh==null)continue;
                if(!groups.TryGetValue(r.sharedMaterial,out var list)){list=new List<CombineInstance>();groups[r.sharedMaterial]=list;}
                list.Add(new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});source.Add(f.gameObject);
            }
            var owner=root.gameObject.AddComponent<TownArtMeshOwner>();
            foreach(var pair in groups)
            {
                var m=new Mesh{name=root.name+" combined"};m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                m.CombineMeshes(pair.Value.ToArray(),true,true);m.RecalculateBounds();owner.meshes.Add(m);
                Part(root,"Paint "+pair.Key.name,m,Vector3.zero,Vector3.one,pair.Key.color).GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            foreach(var go in source){go.SetActive(false);Object.Destroy(go);}
        }
        static void Lamp(Transform p,Vector3 at,int town)
        {
            var g=Group(p,"Lantern",at);
            Cyl(g,"Stem",new Vector3(0,.9f,0),new Vector3(.09f,.9f,.09f),Wood);
            Box(g,"Lantern frame",new Vector3(0,1.85f,0),new Vector3(.3f,.42f,.3f),town==1?C("689c5b"):Gold);
            Box(g,"Paper light",new Vector3(0,1.85f,-.16f),new Vector3(.20f,.27f,.025f),Cream);
            Cone(g,"Lantern cap",new Vector3(0,2.08f,0),new Vector3(.48f,.2f,.48f),town==2?C("397dba"):C("a75a3d"));
            if(town==1)Leaf(g,new Vector3(.1f,2.14f,0),.35f,C("91ba61"),25);
        }
        static void Leaf(Transform p,Vector3 at,float size,Color c,float angle=0)
        {
            var g=Cone(p,"Pointed leaf",at,new Vector3(size*.6f,size,size*.16f),c);g.transform.localRotation=Quaternion.Euler(0,0,angle);
        }
    }
    // Meshes belong to a town instance, unlike cached unit meshes/materials.
    public sealed class TownArtMeshOwner : MonoBehaviour
    {
        public readonly List<Mesh> meshes=new List<Mesh>();
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Object.Destroy(mesh);}
    }
    public sealed class TownArtMotion : MonoBehaviour
    {
        public float turn, bob, breathe; Vector3 origin,scale; float phase;
        void Start(){origin=transform.localPosition;scale=transform.localScale;phase=origin.x*1.7f+origin.z;}
        void Update(){float wave=Mathf.Sin(Time.time*1.4f+phase);transform.localPosition=origin+Vector3.up*(wave*bob);transform.localScale=new Vector3(scale.x,scale.y*(1+wave*breathe),scale.z);if(turn!=0)transform.Rotate(0,turn*Time.deltaTime,0,Space.Self);}
    }
}
