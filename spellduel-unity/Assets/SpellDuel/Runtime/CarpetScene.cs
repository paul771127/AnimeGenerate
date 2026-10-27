using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>Visual-only flying arena. Coordinates passed by callers remain unswayed world coordinates.</summary>
    public sealed class CarpetScene : MonoBehaviour
    {
        public float Speed { get; set; } = 12;
        public float SwayScale { get; set; } = 1;
        public Transform VisualRoot { get; private set; }
        public int Station { get; private set; }
        public static CarpetScene Active { get; private set; }
        readonly Transform[] scenes=new Transform[5];
        readonly List<Transform> scenery=new List<Transform>();
        readonly List<float> rates=new List<float>();
        readonly List<LineRenderer> trims=new List<LineRenderer>();
        readonly List<Vector3[]> trimPoints=new List<Vector3[]>();
        Mesh cloth; Vector3[] rest,vertices,normals; float clock; Vector3 impactPoint; float impactAge=10,impactStrength;
        ParticleSystem dust; LineRenderer ripple; float halfWidth=2;
        static readonly Color[] Skies={new Color(.64f,.83f,.98f),new Color(.3f,.75f,.88f),new Color(.8f,.63f,.45f),new Color(.21f,.24f,.48f),new Color(.98f,.73f,.45f)};
        public static CarpetScene Create(List<Vector3> arenaPolygon,float floorY,Vector3 forward)
        {
            if(arenaPolygon==null||arenaPolygon.Count<3)throw new ArgumentException("Carpet needs at least three arena points");
            var c=new GameObject("CarpetScene").AddComponent<CarpetScene>();
            Vector3 center=Vector3.zero;foreach(var v in arenaPolygon)center+=v;center/=arenaPolygon.Count;center.y=floorY;
            forward.y=0;if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            c.transform.SetPositionAndRotation(center,Quaternion.LookRotation(forward.normalized));
            c.VisualRoot=SkyArt.Node(c.transform,"Swaying cloth only",Vector3.zero);
            var poly=new List<Vector3>();foreach(var v in arenaPolygon){var q=c.transform.InverseTransformPoint(v);q.y=.025f;if(poly.Count==0||(q-poly[poly.Count-1]).sqrMagnitude>.00001f)poly.Add(q);}
            if(poly.Count>3&&(poly[0]-poly[poly.Count-1]).sqrMagnitude<.00001f)poly.RemoveAt(poly.Count-1);
            c.BuildCloth(poly);c.BuildScenery();
            c.dust=SpellFx.NewPS(c.VisualRoot,"Landing cloud dust",SpellFx.BlendMat(SpellFx.TexSoft),24,false);
            var m=c.dust.main;m.startLifetime=.45f;m.startSize=.11f;m.startSpeed=.35f;m.startColor=new Color(1,.9f,.7f,.5f);var e=c.dust.emission;e.enabled=false;
            c.ripple=SkyArt.Ring(c.VisualRoot,"Impact ripple",1,new Color(1,.85f,.5f,.5f),.018f);c.ripple.gameObject.SetActive(false);
            Active=c;c.SetVariant(0);c.Sample(0,0);return c;
        }
        static float Cross(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        void BuildCloth(List<Vector3> p)
        {
            float area=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];area+=a.x*b.z-b.x*a.z;halfWidth=Mathf.Max(halfWidth,Mathf.Abs(a.x));}
            if(area<0)p.Reverse();
            var order=new List<int>();for(int i=0;i<p.Count;i++)order.Add(i);
            var mb=new ArtKit.MB(1);int guard=p.Count*p.Count;
            while(order.Count>2&&guard-->0)
            {
                bool cut=false;
                for(int i=0;i<order.Count;i++)
                { int ia=order[(i+order.Count-1)%order.Count],ib=order[i],ic=order[(i+1)%order.Count];var a=p[ia];var b=p[ib];var d=p[ic];if(Cross(a,b,d)<=.000001f)continue;
                  bool inside=false;foreach(int j in order)if(j!=ia&&j!=ib&&j!=ic&&Cross(a,b,p[j])>=0&&Cross(b,d,p[j])>=0&&Cross(d,a,p[j])>=0){inside=true;break;}
                  if(inside)continue;Subdivide(mb,a,d,b,3);order.RemoveAt(i);cut=true;break; }
                if(!cut)throw new ArgumentException("Arena polygon must be simple and nondegenerate");
            }
            cloth=mb.Build("Subdivided calibrated carpet");cloth.MarkDynamic();rest=cloth.vertices;vertices=new Vector3[rest.Length];normals=new Vector3[rest.Length];
            SkyArt.MeshPart(VisualRoot,"Wine woven cloth",cloth,SkyArt.Mat(new Color(.65f,.15f,.31f)),Vector3.zero,Vector3.one);
            for(int edge=0;edge<p.Count;edge++)
            {
                Vector3 a=p[edge],b=p[(edge+1)%p.Count];int n=Mathf.Max(3,Mathf.CeilToInt((b-a).magnitude/.15f));
                for(int stripe=0;stripe<3;stripe++)
                { var pts=new Vector3[n+1];for(int j=0;j<=n;j++)pts[j]=Vector3.Lerp(a,b,j/(float)n)*(1-stripe*.035f)+Vector3.up*.012f;
                  AddTrim("Woven border",pts,stripe==0?.06f:.025f,stripe==1?SkyArt.Cream:SkyArt.Teal); }
                for(int j=0;j<n;j++)
                { var q=Vector3.Lerp(a,b,(j+.5f)/n);var outward=new Vector3((b-a).z,0,-(b-a).x).normalized;
                  AddTrim("Gold fringe",new[]{q,q+outward*.10f+Vector3.down*.02f,q+outward*.19f+Vector3.down*.04f},.018f,SkyArt.Gold); }
            }
            // Interior navigation embroidery, clipped to the actual polygon via the triangulated cloth vertices.
            for(int i=0;i<rest.Length;i+=3)
            { var a=rest[i];var b=rest[i+1];if(Mathf.Abs((a+b).x)<.09f)AddTrim("Route seam",new[]{a+Vector3.up*.012f,b+Vector3.up*.012f},.012f,SkyArt.Cream); }
        }
        static void Subdivide(ArtKit.MB b,Vector3 a,Vector3 c,Vector3 d,int level)
        {
            if(level>0){var ac=(a+c)*.5f;var cd=(c+d)*.5f;var da=(d+a)*.5f;Subdivide(b,a,ac,da,level-1);Subdivide(b,ac,c,cd,level-1);Subdivide(b,da,cd,d,level-1);Subdivide(b,ac,cd,da,level-1);return;}
            int k=b.Add(a,Vector3.up,new Vector2(a.x,a.z));b.Add(c,Vector3.up,new Vector2(c.x,c.z));b.Add(d,Vector3.up,new Vector2(d.x,d.z));b.Tri(0,k,k+1,k+2);
        }
        void AddTrim(string name,Vector3[] pts,float width,Color c){var l=SkyArt.Line(VisualRoot,name,pts.Length,width,c);l.SetPositions(pts);trims.Add(l);trimPoints.Add(pts);}
        void BuildScenery()
        {
            for(int s=0;s<5;s++)
            {
                var root=scenes[s]=SkyArt.Node(transform,"Station "+s,Vector3.zero);
                // Huge backdrop below the horizon, never replaces the AR camera background or global lighting.
                SkyArt.Oval(root,"Distant cloud sea",new Vector3(0,-8,28),new Vector3(100,8,110),Skies[s]);
                SkyArt.Oval(root,"Sun or storm halo",new Vector3(-13,12,54),new Vector3(9,9,2),s==3?new Color(.55f,.6f,.9f):SkyArt.Cream);
                for(int i=0;i<30;i++)
                {
                    int layer=i%3;float side=i%2==0?-1:1;var t=SkyArt.Node(root,"Pooled layer "+layer,new Vector3(side*(halfWidth+2+layer*7+(i%4)),layer==0?-1.8f:layer==1?-.5f:-3, -14+i*3));
                    scenery.Add(t);rates.Add(layer==0?1:layer==1?.35f:.02f);
                    Color cloud=s==3?new Color(.38f,.4f,.61f):new Color(.88f,.94f,1);
                    if(layer!=1||s==0||s==4)
                    {SkyArt.Oval(t,"Wind shaped cloud",Vector3.zero,new Vector3(3+layer*2,.7f+layer*.3f,5+layer*3),cloud);if(layer==0)SkyArt.Rod(t,"Wind streak",new Vector3(0,.5f,-2),new Vector3(0,.5f,2),.025f,SkyArt.Cream);}
                    else if(s==1)
                    {SkyArt.Oval(t,"Floating island keel",new Vector3(0,-1,0),new Vector3(3,4,4),new Color(.5f,.43f,.55f),true);SkyArt.Oval(t,"Grass crown",Vector3.zero,new Vector3(3.4f,.55f,4.2f),new Color(.3f,.77f,.49f));for(int j=0;j<3;j++)SkyArt.Rod(t,"Hanging mooring",new Vector3(j*.4f,0,0),new Vector3(j*.4f,-3,0),.08f,SkyArt.Gold);}
                    else if(s==2)
                    {SkyArt.Oval(t,"Wind carved red pillar",Vector3.zero,new Vector3(3,12,6),new Color(.8f,.43f,.25f));SkyArt.Oval(t,"Sandstone cap",new Vector3(0,4,0),new Vector3(4,1,7),SkyArt.Cream);}
                    else
                    {SkyArt.Oval(t,"Thunderhead",Vector3.zero,new Vector3(6,7,6),cloud);SkyArt.Rod(t,"Storm navigation beacon",new Vector3(0,0,-2),new Vector3(0,3,-2),.13f,new Color(.7f,.85f,1));}
                }
            }
        }
        public void SetVariant(int station){Station=Mathf.Clamp(station,0,4);for(int i=0;i<5;i++)scenes[i].gameObject.SetActive(i==Station);}
        public void Impact(Vector3 at,float strength){impactPoint=transform.InverseTransformPoint(at);impactStrength=Mathf.Clamp01(strength);impactAge=0;dust.transform.localPosition=impactPoint+Vector3.up*.08f;dust.Emit(Mathf.RoundToInt(8+16*impactStrength));}
        float HeightAt(Vector3 p)
        {float wave=(Mathf.Sin(p.x*2+clock*2.1f)+Mathf.Sin(p.z*2.4f-clock*2.7f))*.005f;float d=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(impactPoint.x,impactPoint.z));float k=Mathf.Clamp01(1-impactAge/.3f);return Mathf.Clamp01(SwayScale)*(wave- .08f*impactStrength*Mathf.Sin(Mathf.PI*Mathf.Clamp01(impactAge/.3f))*Mathf.Exp(-d*d*3)+.016f*k*Mathf.Sin(d*12-impactAge*24));}
        /// <summary>Map an unswayed world warning/landing point onto the rendered cloth. Never use for collision.</summary>
        public Vector3 SurfacePoint(Vector3 world){var p=transform.InverseTransformPoint(world);p.y+=HeightAt(p);return VisualRoot.TransformPoint(p);}
        public void Sample(float time,float dt)
        {
            clock=time;impactAge+=Mathf.Max(0,dt);float sway=Mathf.Clamp01(SwayScale),v=Mathf.Clamp(Speed/12,.3f,1.5f);
            VisualRoot.localPosition=Vector3.up*(.03f*Mathf.Sin(time*2.1f)*v*sway);
            VisualRoot.localRotation=Quaternion.Euler(Mathf.Sin(time*1.7f)*1.2f*v*sway,0,Mathf.Sin(time*2.3f+.7f)*v*sway);
            for(int i=0;i<rest.Length;i++){var p=rest[i];vertices[i]=p+Vector3.up*HeightAt(p);float dx=(HeightAt(p+Vector3.right*.01f)-HeightAt(p-Vector3.right*.01f))/.02f,dz=(HeightAt(p+Vector3.forward*.01f)-HeightAt(p-Vector3.forward*.01f))/.02f;normals[i]=new Vector3(-dx,1,-dz).normalized;}
            cloth.vertices=vertices;cloth.normals=normals;cloth.RecalculateBounds();
            for(int i=0;i<trims.Count;i++)for(int j=0;j<trimPoints[i].Length;j++){var p=trimPoints[i][j];trims[i].SetPosition(j,p+Vector3.up*HeightAt(p));}
            for(int i=Station*30;i<(Station+1)*30;i++){var t=scenery[i];var p=t.localPosition;p.z-=Mathf.Max(0,Speed)*rates[i]*dt;if(p.z< -24)p.z+=90;t.localPosition=p; if(i%3==0)t.localScale=new Vector3(1,1,Mathf.Clamp(Speed/12,.5f,1.6f));}
            ripple.gameObject.SetActive(impactAge<.6f);if(impactAge<.6f){ripple.transform.localPosition=new Vector3(impactPoint.x,.05f,impactPoint.z);ripple.transform.localScale=Vector3.one*(.15f+impactAge*3);var c=new Color(1,.85f,.5f,(1-impactAge/.6f)*.65f);ripple.startColor=ripple.endColor=c;}
        }
        void Update(){Sample(Time.time,Time.deltaTime);}
        void OnDestroy(){if(Active==this)Active=null;SkyArt.Dispose(cloth);}
    }
}
