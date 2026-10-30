using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  static Material lakeMaterial;
  static readonly System.Collections.Generic.Dictionary<int,Material> backdropMaterials=new System.Collections.Generic.Dictionary<int,Material>();
  static void BackdropLayer(Transform root,int queue)
  {if(!backdropMaterials.TryGetValue(queue,out var mat)||!mat){mat=new Material(Resources.Load<Shader>("Shaders/ExploreBackdrop")??SkyArt.Glow.shader){name="Explore shared backdrop "+queue,renderQueue=queue};backdropMaterials[queue]=mat;}root.GetComponent<Renderer>().sharedMaterial=mat;root.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;root.GetComponent<MeshFilter>().sharedMesh.bounds=new Bounds(Vector3.zero,Vector3.one*1000f);}
  static Material LakeMaterial { get { if(!lakeMaterial)lakeMaterial=new Material(Resources.Load<Shader>("Shaders/ExploreLake")??SkyArt.Glow.shader){name="Explore shared still lake"};return lakeMaterial; } }
  static void WaterDetails(Geo b,int n)
  {
   // Discontinuous arcs, not the old straight white floor stripes. Kept inside the cell.
   for(int arc=0;arc<3;arc++)for(int i=0;i<8;i++)
   {float a=(i/8f*1.7f+arc*2.2f+n*.4f),a2=a+.16f,r=.35f+arc*.28f;var c=new Color(.65f,.88f,.78f,.18f);b.Quad(V(Mathf.Cos(a)*r,.017f,Mathf.Sin(a)*r),V(Mathf.Cos(a2)*r,.017f,Mathf.Sin(a2)*r),V(Mathf.Cos(a2)*(r+.014f),.017f,Mathf.Sin(a2)*(r+.014f)),V(Mathf.Cos(a)*(r+.014f),.017f,Mathf.Sin(a)*(r+.014f)),c);}
   // A split lily pad and a small flower; most surface remains open.
   for(int i=0;i<10;i++){float a=i*.53f,a2=(i+1)*.53f;var at=V(.62f,.027f,.54f);b.Tri(at,at+V(Mathf.Cos(a2)*.23f,0,Mathf.Sin(a2)*.19f),at+V(Mathf.Cos(a)*.23f,0,Mathf.Sin(a)*.19f),Leaf);}
   for(int i=0;i<5;i++){float a=i*1.256f;b.Frond(V(.62f,.04f,.54f),V(.62f+Mathf.Cos(a)*.10f,.11f,.54f+Mathf.Sin(a)*.10f),.04f,Bone);}

  }
  static void ShoreStrip(Geo b)
  {
   for(int i=0;i<10;i++){float x=-1.5f+i*.3f,z=1.29f+.055f*Mathf.Sin(i*2.3f);b.Quad(V(x,.022f,1.49f),V(x+.3f,.022f,1.49f),V(x+.3f,.022f,z),V(x,.022f,z),new Color(.48f,.69f,.50f,.26f));if(i%3==0)b.Quad(V(x,.026f,z),V(x+.2f,.026f,z+.02f),V(x+.2f,.026f,z+.04f),V(x,.026f,z+.016f),new Color(.77f,.88f,.76f,.3f));}
   for(int i=0;i<4;i++){var p=V(-.7f+i*.37f,.02f,1.38f);b.Rod(p,p+V(.02f,.45f+i%2*.17f,0),.01f,Moss,4);b.Frond(p+V(0,.12f,0),p+V(.11f,.4f,0),.03f,Leaf);}
  }
  static void CampFuel(Geo b,int n)
  {
   for(int i=0;i<9;i++){float a=i*2*Mathf.PI/9;b.Crag(V(Mathf.Cos(a)*.77f,0,Mathf.Sin(a)*.77f),V(.36f,.25f+(i%3)*.025f,.32f),Rock,i+n);}
   for(int i=0;i<7;i++){float a=i*2.399f;b.Crown(V(Mathf.Cos(a)*.3f,.07f,Mathf.Sin(a)*.3f),V(.23f,.10f,.21f),i%2==0?Glow(new Color(.8f,.19f,.035f)):new Color(.18f,.13f,.10f),i,6,2);}
   for(int i=0;i<3;i++){float a=i*Mathf.PI/3;var dir=V(Mathf.Cos(a),0,Mathf.Sin(a));var from=dir*(-.59f)+V(0,.16f+i*.045f,0);var to=dir*.59f+V(0,.22f+i*.045f,0);b.Rod(from,to,.12f,Bark,8,.10f);b.Rod(from-dir*.008f,from+dir*.008f,.105f,Bone,8);b.Rod(to-dir*.008f,to+dir*.008f,.088f,Bone,8);}
  }
  static void CampFlames(Geo b)
  {
   for(int i=0;i<6;i++)
   {float angle=i*Mathf.PI/3;var origin=V(Mathf.Cos(angle)*.20f,.22f,Mathf.Sin(angle)*.20f);float height=.78f+(i%3)*.16f;
    FlameTongue(b,origin,height,.17f,angle,new Color(1,.27f,.035f,.85f),i);
    FlameTongue(b,origin+V(0,.015f,.006f),height*.70f,.11f,angle+.2f,new Color(1,.73f,.16f,.96f),i);}
   FlameTongue(b,V(0,.23f,0),.63f,.16f,.3f,new Color(1,.94f,.59f,1),2);
  }
  static void FlameTongue(Geo b,Vector3 origin,float height,float width,float angle,Color c,int seed)
  {
   for(int plane=0;plane<2;plane++){var side=V(Mathf.Cos(angle+plane*Mathf.PI/2),0,Mathf.Sin(angle+plane*Mathf.PI/2));
    for(int j=0;j<5;j++){float t=j/5f,u=(j+1)/5f;var a=origin+V(Mathf.Sin(t*3+seed)*.14f*t,height*t,Mathf.Sin(t*4+seed)*.09f*t);var z=origin+V(Mathf.Sin(u*3+seed)*.14f*u,height*u,Mathf.Sin(u*4+seed)*.09f*u);float wa=width*(1-t)*(.6f+Mathf.Sin(t*Mathf.PI)*.6f),wz=width*(1-u)*(.6f+Mathf.Sin(u*Mathf.PI)*.6f);b.Quad(a-side*wa,a+side*wa,z+side*wz,z-side*wz,c,true);}}
  }
  // Create once beneath the exploration world root, not inside the 24m streamed cell set.
  // Its localPosition is already the map centre. All distant geometry is outside the playable map.
  public static GameObject CreateBackdrop(ExploreMap map)
  {
   if(map==null)return null;var root=Root("Sky backdrop");root.transform.localPosition=(map.World(0,0)+map.World(map.W-1,map.H-1))*.5f;
   float radius=Mathf.Max(map.W,map.H)*ExploreMap.CellSize*.95f;
   var dome=Part(root.transform,"sky/dome",b=>{
    const int sectors=40,bands=8;for(int j=0;j<bands;j++)for(int i=0;i<sectors;i++)
    {float a=i*Mathf.PI*2/sectors,z=(i+1)*Mathf.PI*2/sectors,lo=j*Mathf.PI*.5f/bands,hi=(j+1)*Mathf.PI*.5f/bands;var c=Color.Lerp(new Color(.64f,.84f,.83f),new Color(.16f,.47f,.72f),j/(float)bands);b.Quad(V(Mathf.Cos(a)*Mathf.Cos(lo),Mathf.Sin(lo),Mathf.Sin(a)*Mathf.Cos(lo)),V(Mathf.Cos(z)*Mathf.Cos(lo),Mathf.Sin(lo),Mathf.Sin(z)*Mathf.Cos(lo)),V(Mathf.Cos(z)*Mathf.Cos(hi),Mathf.Sin(hi),Mathf.Sin(z)*Mathf.Cos(hi)),V(Mathf.Cos(a)*Mathf.Cos(hi),Mathf.Sin(hi),Mathf.Sin(a)*Mathf.Cos(hi)),c);}
   b.HeightTint(0,0,1,new Color(.64f,.84f,.83f),new Color(.16f,.47f,.72f));
   },V(0,3,0),true);BackdropLayer(dome,1000);dome.name="SkyDome";dome.localScale=Vector3.one*(radius*1.7f);
   var sun=Part(root.transform,"sky/sun",b=>{b.Crown(Vector3.zero,V(8,8,8),new Color(1,.96f,.75f,1),0,16,4);},V(-radius*.6f,radius*.65f,radius*.8f),true);sun.name="Sun";BackdropLayer(sun,1100);
   for(int i=0;i<12;i++){int key=i%4;float a=i*Mathf.PI/6;Part(root.transform,"sky/cloud/"+key,b=>{for(int l=0;l<6;l++){var at=V((l-2.5f)*4,1.5f*Mathf.Sin(l+key),Mathf.Sin(l*2+key)*3);b.Crown(at,V(11,5+(l%3)*2,8),new Color(.90f,.96f,.95f,.94f),l+key,10,4);b.Crown(at-V(0,1.9f,0),V(10,2,7),new Color(.69f,.82f,.86f,.6f),l+key,8,3);}b.HeightTint(0,-3,8,new Color(.68f,.81f,.85f,.9f),Color.white);},V(Mathf.Cos(a)*radius,26+(i%3)*13,Mathf.Sin(a)*radius),true).name="Cloud_"+i;}
   for(int i=0;i<40;i++){int key=i%4;float a=i*Mathf.PI/20;var t=Part(root.transform,"sky/forest/"+key,b=>{for(int j=0;j<3;j++){float x=(j-1)*5;b.Rod(V(x,0,0),V(x+.7f,15+j*2,0),.48f,new Color(.22f,.40f,.37f),5,.15f);b.Crown(V(x,16+j*2,0),V(10,12,8),Color.Lerp(new Color(.26f,.48f,.42f),new Color(.37f,.59f,.49f),j*.2f),key+j,7,3);}},V(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),true);t.name="DistantForest_"+i;t.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}
   foreach(Transform child in root.transform){if(child.name.StartsWith("Cloud_"))BackdropLayer(child,1200);else if(child.name.StartsWith("DistantForest_"))BackdropLayer(child,1500);}
   return root;
  }
 }
}
