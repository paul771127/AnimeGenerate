using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public sealed partial class CarpetScene
 {
  static readonly Material[] cloudMats=new Material[5];
  static readonly Mesh[] skyMeshes=new Mesh[5];
  static Mesh cliffMesh;static Material cliffMaterial;static Texture2D cloudNoise;
  static readonly Mesh[] bridgeDecks=new Mesh[3];
  readonly List<LineRenderer> lightning=new List<LineRenderer>();
  readonly List<Renderer> stormClouds=new List<Renderer>();
  readonly List<Transform> waterfallSheets=new List<Transform>();
  MaterialPropertyBlock cloudBlock;Mesh hemMesh;Vector3[] hemRest,hemVertices;float flash;
  static Material CloudMaterial(int s)
  {
   if(cloudMats[s])return cloudMats[s];
   var m=new Material(Resources.Load<Shader>("Shaders/CarpetCloud")){name="Shared layered cloud "+s};
   m.SetColor("_Color",s==3?new Color(.36f,.42f,.56f):new Color(.96f,.97f,1));
   m.SetColor("_Shade",s==3?new Color(.09f,.12f,.23f):s==0||s==4?new Color(.50f,.43f,.58f):new Color(.49f,.63f,.75f));
   m.SetColor("_Rim",s==0||s==4?new Color(1,.77f,.38f):new Color(.78f,.88f,1));m.SetTexture("_NoiseTex",CloudNoise());return cloudMats[s]=m;
  }
  static Texture2D CloudNoise()
  {
   if(cloudNoise)return cloudNoise;
   const int size=128;var pixels=new Color32[size*size];
   for(int y=0;y<size;y++)for(int x=0;x<size;x++)
   {
    float u=x/(float)size,v=y/(float)size,n=0;
    // Tileable two-octave cloud density. Generated once; all five stations share 64 KB.
    for(int octave=0;octave<2;octave++)
    {
     float f=octave==0?6:18,weight=octave==0?.74f:.26f;
     float a=Mathf.Lerp(Mathf.PerlinNoise(u*f+11,v*f+17),Mathf.PerlinNoise((u-1)*f+11,v*f+17),u);
     float b=Mathf.Lerp(Mathf.PerlinNoise(u*f+11,(v-1)*f+17),Mathf.PerlinNoise((u-1)*f+11,(v-1)*f+17),u);
     n+=Mathf.Lerp(a,b,v)*weight;
    }
    byte c=(byte)Mathf.RoundToInt(Mathf.Clamp01((n-.5f)*2+.5f)*255);pixels[y*size+x]=new Color32(c,c,c,255);
   }
   cloudNoise=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Shared procedural cloud density",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};cloudNoise.SetPixels32(pixels);cloudNoise.Apply(false,true);return cloudNoise;
  }
  void BuildSky(Transform root,int s)
  {
   if(!skyMeshes[s])
   {
    var verts=new List<Vector3>();var colors=new List<Color>();var tris=new List<int>();
    var horizon=s==3?new Color(.30f,.34f,.48f):s==0||s==4?new Color(1,.72f,.48f):new Color(.69f,.85f,.92f);
    var zenith=s==3?new Color(.055f,.08f,.16f):new Color(.12f,.38f,.68f);
    for(int j=0;j<=10;j++)for(int i=0;i<=64;i++){float a=i*Mathf.PI*2/64,v=j/10f,e=Mathf.Lerp(-.18f,Mathf.PI*.5f,v);verts.Add(new Vector3(Mathf.Sin(a)*Mathf.Cos(e)*120,Mathf.Sin(e)*120,Mathf.Cos(a)*Mathf.Cos(e)*120));colors.Add(Color.Lerp(horizon,zenith,Mathf.Pow(v,.65f)));}
    for(int j=0;j<10;j++)for(int i=0;i<64;i++){int k=j*65+i;tris.AddRange(new[]{k,k+65,k+1,k+1,k+65,k+66});}
    var mesh=new Mesh{name="Procedural gradient sky "+s};mesh.SetVertices(verts);mesh.SetColors(colors);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();skyMeshes[s]=mesh;
   }
   SkyArt.MeshPart(root,"Distant sky gradient",skyMeshes[s],SkyArt.Glow,Vector3.zero,Vector3.one).GetComponent<Renderer>().sortingOrder=-100;
   CloudDeck(root,s);
   var sun=SkyArt.Oval(root,s==3?"Moon through storm":"Directional dawn sun",new Vector3(s==4?0:-14,s==4?10:12,65),Vector3.one*(s==3?3:5),SkyArt.Cream);sun.GetComponent<Renderer>().sharedMaterial=SkyArt.Glow;sun.GetComponent<Renderer>().sortingOrder=-90;
   var props=new MaterialPropertyBlock();props.SetColor("_Color",s==3?new Color(.7f,.8f,1):new Color(1,.88f,.56f));sun.GetComponent<Renderer>().SetPropertyBlock(props);
  }
  void CloudDeck(Transform root,int station)
  {
   var v=new List<Vector3>();var colors=new List<Color>();var tr=new List<int>();
   for(int j=0;j<=5;j++)for(int i=0;i<=48;i++)
   {
    float a=i*Mathf.PI*2/48,r=Mathf.Lerp(halfWidth+.1f,115,j/5f),wave=Mathf.Sin(a*7+j*2)*Mathf.Sin(a*3-j)*.7f;
    v.Add(new Vector3(Mathf.Sin(a)*r,-7+wave,Mathf.Cos(a)*r));
    colors.Add(Color.Lerp(station==3?new Color(.14f,.19f,.30f):new Color(.65f,.70f,.80f),station==3?new Color(.35f,.41f,.52f):new Color(.91f,.94f,1),.5f+wave*.4f));
   }
   for(int j=0;j<5;j++)for(int i=0;i<48;i++){int k=j*49+i;tr.AddRange(new[]{k,k+49,k+1,k+1,k+49,k+50});}
   var mesh=new Mesh{name="Unbroken distant cloud deck"};mesh.SetVertices(v);mesh.SetColors(colors);mesh.SetTriangles(tr,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
   var part=SkyArt.MeshPart(root,"Low cloud sea outside arena",mesh,CloudMaterial(station),Vector3.zero,Vector3.one);var density=new MaterialPropertyBlock();density.SetFloat("_Detail",1.35f);density.SetFloat("_RimStrength",.12f);part.GetComponent<Renderer>().SetPropertyBlock(density);part.gameObject.AddComponent<SkyMeshOwner>().Meshes.Add(mesh);
  }
  void Cloud(Transform root,int station,int layer)
  {
   float scale=1+layer*.6f;
   for(int p=0;p<4;p++)
   {
    var at=new Vector3((p-1.5f)*1.12f,(p%2)*.35f,Mathf.Sin(p*2.2f)*.6f)*scale;
    if(station==1&&layer==1)at.y-=3.4f; // Keep the grass, bridge and waterfall lip above their cloud skirt.
    var part=SkyArt.Oval(root,"Cloud volume",at,new Vector3(3.7f,1.35f+(p%2)*.9f,4.7f)*scale,Color.white);
    part.GetComponent<Renderer>().sharedMaterial=CloudMaterial(station);
   }
  }
  void BuildScenery()
  {
   cloudBlock=new MaterialPropertyBlock();
   for(int s=0;s<5;s++)
   {
    var root=scenes[s]=SkyArt.Node(transform,"Station "+s,Vector3.zero);BuildSky(root,s);
    if(s==4)Airship(root);
    for(int i=0;i<30;i++)
    {
     int layer=i%3;float side=i%2==0?-1:1;
     float x=side*(halfWidth+(layer==0?3.4f:layer==1?9:22)+(i%4)*1.5f);
     var t=SkyArt.Node(root,"Depth layer "+layer,new Vector3(x,layer==0?-2.2f:layer==1?-1.7f:-5,-20+i*3));
     scenery.Add(t);rates.Add(layer==0?1:layer==1?.38f:.055f);Cloud(t,s,layer);
     if(layer==0)
     {
      windStreaks.Add(SkyArt.Rod(t,"Wind streak",new Vector3(0,.5f,-3),new Vector3(0,.5f,3),.023f,SkyArt.Cream));
      // Wisps skim the outside of the cloth, never a screen-wide fog plane.
      var mist=SpellFx.NewPS(t,"Passing edge mist",SpellFx.BlendMat(SpellFx.TexSoft),5,true);mist.transform.localPosition=Vector3.up*1.35f;var m=mist.main;m.startLifetime=2.2f;m.startSpeed=.12f;m.startSize=1.8f;m.startColor=new Color(.85f,.91f,1,.085f);SpellFx.Sphere(mist,.7f);SpellFx.Rate(mist,1);SpellFx.FadeOut(mist);mist.Play();
     }
     if(layer==1)
     {
      if(s==1)Island(t,i);
      else if(s==2)Ravine(t,i);
      else if(s==3)Beacon(t,i);
     }
     SkyArt.MergeScenery(t);
     if(s==3)foreach(var r in t.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial==CloudMaterial(s))stormClouds.Add(r);
    }
   }
  }
  void Airship(Transform root)
  {
   // Distant scenery only: above and beyond the arena, with dawn directly behind its envelope.
   var ship=SkyArt.Node(root,"Intercept airship silhouette",new Vector3(0,5,44));
   SkyArt.Oval(ship,"Gold edged airship envelope",new Vector3(0,2.5f,0),new Vector3(10,3.8f,5.4f),new Color(.70f,.33f,.20f));
   SkyArt.Oval(ship,"Suspended timber gondola",Vector3.zero,new Vector3(6,1.4f,2.8f),new Color(.28f,.20f,.18f),true);
   SkyArt.Oval(ship,"Gondola upper deck",new Vector3(0,.48f,0),new Vector3(5.8f,.25f,2.7f),new Color(.56f,.37f,.21f));
   for(int side=-1;side<=1;side+=2)
   {
    for(int j=-1;j<=1;j++)SkyArt.Rod(ship,"Suspension rope",new Vector3(j*2,.5f,side*1.2f),new Vector3(j*2,2.5f,side*2.55f),.045f,SkyArt.Gold);
    SkyArt.Rod(ship,"Deck side rail",new Vector3(-2.7f,.85f,side*1.15f),new Vector3(2.7f,.85f,side*1.15f),.07f,SkyArt.Gold);
    var fin=SkyArt.Sail(ship,"Steering canvas",new Vector3(side*4.2f,1.5f,.2f),new Vector3(2,2.2f,1),SkyArt.Teal);fin.localRotation=Quaternion.Euler(0,0,-side*50);
   }
   SkyArt.MergeScenery(ship);
  }
  void Island(Transform t,int i)
  {
   SkyArt.Oval(t,"Weathered island root",new Vector3(0,-2,0),new Vector3(5,6,6),new Color(.38f,.37f,.40f),true);
   SkyArt.Oval(t,"Grass shelf",Vector3.up*.4f,new Vector3(5.6f,.65f,6.3f),new Color(.25f,.55f,.30f));
   for(int k=0;k<3;k++)SkyArt.Oval(t,"Exposed rock shelf",new Vector3(.6f*Mathf.Sin(k),-k*.8f,0),new Vector3(5-k*.8f,.6f,5.8f-k),new Color(.52f,.48f,.42f));
   var fall=SkyArt.Line(t,"Waterfall ribbons",4,.27f,new Color(.63f,.89f,1,.6f));fall.SetPositions(new[]{new Vector3(2.55f,.78f,.7f),new Vector3(3.10f,-.1f,.7f),new Vector3(3.15f,-2,.7f),new Vector3(3.5f,-6,.7f)});waterfallSheets.Add(fall.transform);
   // The next island on the same side is 18 m ahead and may sit 3 m sideways.
   float dx=Mathf.Sign(t.localPosition.x)*(((i+6)%30)%4-i%4)*1.5f;
   for(int side=-1;side<=1;side+=2)
   {
    var chain=SkyArt.Line(t,"Suspended iron chain",12,.07f,new Color(.24f,.27f,.32f));
    for(int k=0;k<12;k++){float u=k/11f;chain.SetPosition(k,new Vector3(dx*u+side*.55f,.9f-Mathf.Sin(u*Mathf.PI)*.65f,2+u*14));}
   }
   int key=Mathf.RoundToInt(dx/3)+1;
   if(!bridgeDecks[key])
   {
    var v=new List<Vector3>();var tris=new List<int>();
    for(int j=0;j<32;j++)
    {
     float u=(j+.5f)/32;var p=new Vector3(dx*u,.5f-Mathf.Sin(u*Mathf.PI)*.65f,2+u*14);int k=v.Count;
     v.Add(p+new Vector3(-.62f,0,-.16f));v.Add(p+new Vector3(-.62f,0,.16f));v.Add(p+new Vector3(.62f,0,-.16f));v.Add(p+new Vector3(.62f,0,.16f));tris.AddRange(new[]{k,k+1,k+2,k+2,k+1,k+3});
    }
    var mesh=new Mesh{name="Shared suspended bridge treads "+key};mesh.SetVertices(v);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();bridgeDecks[key]=mesh;
   }
   SkyArt.MeshPart(t,"Bridge deck connected to next island",bridgeDecks[key],SkyArt.Mat(new Color(.42f,.27f,.15f)),Vector3.zero,Vector3.one);
  }
  static void EnsureCliff()
  {
   if(cliffMesh)return;
   var v=new List<Vector3>();var tr=new List<int>();
   float[] widths={.60f,.98f,.87f,1,.79f,.48f};
   for(int j=0;j<6;j++)for(int i=0;i<12;i++)
   {
    float a=i*Mathf.PI*2/12,r=widths[j]*(1+.10f*Mathf.Sin(a*3+j*.8f));
    v.Add(new Vector3(Mathf.Cos(a)*r*.5f,j/5f-.5f+.025f*Mathf.Sin(a*2+j),Mathf.Sin(a)*r*.5f));
   }
   for(int j=0;j<5;j++)for(int i=0;i<12;i++){int a=j*12+i,b=j*12+(i+1)%12;tr.AddRange(new[]{a,a+12,b,b,a+12,b+12});}
   for(int i=1;i<11;i++){tr.AddRange(new[]{0,i,(i+1)%12});tr.AddRange(new[]{60,60+i+1,60+i});}
   cliffMesh=new Mesh{name="Shared rounded stratified cliff"};cliffMesh.SetVertices(v);cliffMesh.SetTriangles(tr,0);cliffMesh.RecalculateNormals();cliffMesh.RecalculateBounds();
   cliffMaterial=new Material(Resources.Load<Shader>("Shaders/CarpetCloud")){name="Shared sunlit sandstone"};
   cliffMaterial.SetColor("_Color",new Color(.81f,.57f,.34f));cliffMaterial.SetColor("_Shade",new Color(.39f,.27f,.25f));cliffMaterial.SetColor("_Rim",new Color(.83f,.65f,.44f));
  }
  void Ravine(Transform t,int i)
  {
   EnsureCliff();
   for(int k=0;k<3;k++)
   {
    var at=new Vector3(k*.5f,k*1.6f-2,0);var size=new Vector3(5-k*.4f,14-k,20);
    SkyArt.MeshPart(t,"Rounded wind carved canyon wall",cliffMesh,cliffMaterial,at,size);
    // The strata wrap around the exposed inward face, instead of floating in front of the cliff.
    var seam=SkyArt.Line(t,"Horizontal erosion strata",9,.10f,new Color(.73f,.51f,.33f));
    float facing=t.localPosition.x>0?Mathf.PI:0;
    for(int j=0;j<9;j++){float a=facing-Mathf.PI*.45f+j*Mathf.PI*.9f/8;seam.SetPosition(j,at+new Vector3(Mathf.Cos(a)*size.x*.49f,k*.18f+.08f*Mathf.Sin(j),Mathf.Sin(a)*size.z*.49f));}
   }
  }
  void Beacon(Transform t,int i)
  {
   SkyArt.Oval(t,"Storm tower base",new Vector3(0,1,0),new Vector3(1.6f,5,1.6f),new Color(.23f,.30f,.41f));
   for(int j=0;j<3;j++){var ring=SkyArt.Ring(t,"Beacon lens",.65f,new Color(.40f,.88f,1,.8f),.07f);ring.transform.localPosition=Vector3.up*(3+j*.22f);}
   var bolt=SkyArt.Line(t,"Fork lightning",5,.075f,new Color(.75f,.86f,1,0));bolt.SetPositions(new[]{new Vector3(-1,9,0),new Vector3(.2f,7,0),new Vector3(-.6f,6,0),new Vector3(.5f,4,0),new Vector3(0,3.5f,0)});lightning.Add(bolt);
  }
  void BuildHem(List<Vector3> polygon)
  {
   var v=new List<Vector3>();var triangles=new List<int>();
   for(int i=0;i<polygon.Count;i++)
   {
    var a=polygon[i];var b=polygon[(i+1)%polygon.Count];int n=Mathf.CeilToInt(Vector3.Distance(a,b)/.15f);
    for(int j=0;j<n;j++){var x=Vector3.Lerp(a,b,j/(float)n);var y=Vector3.Lerp(a,b,(j+1)/(float)n);int k=v.Count;v.Add(x);v.Add(y);v.Add(x-Vector3.up*.045f);v.Add(y-Vector3.up*.045f);triangles.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
   }
   hemMesh=new Mesh{name="Thick woven carpet hem"};hemMesh.SetVertices(v);hemMesh.SetTriangles(triangles,0);hemMesh.RecalculateNormals();hemMesh.MarkDynamic();hemRest=v.ToArray();hemVertices=new Vector3[v.Count];SkyArt.MeshPart(VisualRoot,"Fabric thickness",hemMesh,SkyArt.Mat(new Color(.30f,.07f,.16f)),Vector3.zero,Vector3.one);
  }
  void SampleHem(){for(int i=0;i<hemRest.Length;i++)hemVertices[i]=hemRest[i]+Vector3.up*HeightAt(hemRest[i]);hemMesh.vertices=hemVertices;hemMesh.RecalculateBounds();}
  void SampleEnvironment(float time,float dt)
  {
   flash=Station==3?Mathf.Pow(Mathf.Max(0,Mathf.Sin(time*1.27f)),36)*.7f:0;
   foreach(var line in lightning){var c=new Color(.8f,.9f,1,flash);line.startColor=line.endColor=c;}
   cloudBlock.SetFloat("_Flash",flash);foreach(var r in stormClouds)if(r)r.SetPropertyBlock(cloudBlock);
   foreach(var w in waterfallSheets)w.localScale=new Vector3(1+.035f*Mathf.Sin(time*3),1,1);
  }
 }
}
