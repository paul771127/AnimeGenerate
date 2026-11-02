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
   int start=b.v.Count;
   // Discontinuous arcs, not the old straight white floor stripes. Kept inside the cell.
   for(int arc=0;arc<3;arc++)for(int i=0;i<8;i++)
   {float a=(i/8f*1.7f+arc*2.2f+n*.4f),a2=a+.16f,r=.35f+arc*.28f;var c=new Color(.65f,.88f,.78f,.18f);b.Quad(V(Mathf.Cos(a)*r,.017f,Mathf.Sin(a)*r),V(Mathf.Cos(a2)*r,.017f,Mathf.Sin(a2)*r),V(Mathf.Cos(a2)*(r+.014f),.017f,Mathf.Sin(a2)*(r+.014f)),V(Mathf.Cos(a)*(r+.014f),.017f,Mathf.Sin(a)*(r+.014f)),c);}
   // A split lily pad and a small flower; most surface remains open.
   for(int i=0;i<10;i++){float a=i*.53f,a2=(i+1)*.53f;var at=V(.62f,.027f,.54f);b.Tri(at,at+V(Mathf.Cos(a2)*.23f,0,Mathf.Sin(a2)*.19f),at+V(Mathf.Cos(a)*.23f,0,Mathf.Sin(a)*.19f),Leaf);}
   for(int i=0;i<5;i++){float a=i*1.256f;b.Frond(V(.62f,.04f,.54f),V(.62f+Mathf.Cos(a)*.10f,.11f,.54f+Mathf.Sin(a)*.10f),.04f,Bone);}

   var rotation=Quaternion.Euler(0,n*97,0);for(int i=start;i<b.v.Count;i++)b.v[i]=rotation*b.v[i]+V(.08f*Mathf.Sin(n*2),0,.08f*Mathf.Cos(n*3));

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
  /// <summary>
  /// 地平線以下的「地面碗」（使用者要求：不能透出外面的現實背景，而且補起來的地方要自然、不要像方形色塊）：
  /// 跟天空一樣用 ExploreBackdrop（拉到遠裁切面以內、最先畫），蓋住整個下半球。
  /// 顏色是自然的漸層：地平線上方 4° 接天空底色 → 地平線附近是遠方的霧氣色（和地面網格遠處淡出的顏色一樣）→ 往下慢慢變成深一點的地面色，帶一點斑駁。
  /// </summary>
  static void GroundBowl(Transform root,Color ground,Color skyLow,float scale)
  {
   var haze=HazeOf(ground,skyLow);
   var key="sky/bowl/"+ColorUtility.ToHtmlStringRGB(ground)+ColorUtility.ToHtmlStringRGB(skyLow);
   var bowl=Part(root,key,b=>{
    const int sectors=48,bands=18;float top=4f*Mathf.Deg2Rad,bottom=-Mathf.PI*.5f;
    for(int j=0;j<bands;j++)for(int i=0;i<sectors;i++)
    {
     // 帶子在地平線附近比較密（漸層才順）
     float t0=j/(float)bands,t1=(j+1)/(float)bands;float hi=Mathf.Lerp(top,bottom,t0*t0),lo=Mathf.Lerp(top,bottom,t1*t1);
     float a=i*Mathf.PI*2/sectors,z=(i+1)*Mathf.PI*2/sectors;
     b.Quad(V(Mathf.Cos(a)*Mathf.Cos(lo),Mathf.Sin(lo),Mathf.Sin(a)*Mathf.Cos(lo)),V(Mathf.Cos(z)*Mathf.Cos(lo),Mathf.Sin(lo),Mathf.Sin(z)*Mathf.Cos(lo)),V(Mathf.Cos(z)*Mathf.Cos(hi),Mathf.Sin(hi),Mathf.Sin(z)*Mathf.Cos(hi)),V(Mathf.Cos(a)*Mathf.Cos(hi),Mathf.Sin(hi),Mathf.Sin(a)*Mathf.Cos(hi)),ground);
    }
    var deep=ground*.82f;deep.a=1f;
    b.ColorBy(0,p=>{
     float e=Mathf.Asin(Mathf.Clamp(p.y,-1f,1f)),ang=Mathf.Atan2(p.z,p.x);
     if(e>=-.03f)return Color.Lerp(haze,skyLow,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-.03f,.07f,e)));
     // 斑駁：幾個不同頻率的起伏，顏色差一點點（不會有規則的格子）
     float n=.5f+.25f*Mathf.Sin(ang*5f+e*9f)+.15f*Mathf.Sin(ang*13f-e*17f+1.3f)+.1f*Mathf.Sin(ang*29f+e*31f+2.1f);
     var g=Color.Lerp(haze,Color.Lerp(deep,ground,n),Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(-.03f,-.35f,e)));g.a=1f;return g;
    });
   },Vector3.zero,true);
   bowl.name="GroundBowl";bowl.localScale=Vector3.one*scale;BackdropLayer(bowl,990);
  }
  /// <summary>遠方的霧氣色：地面和天空底色混一點（地面網格遠處淡出成這個顏色、地面碗的地平線也是這個顏色，接起來沒有縫）</summary>
  public static Color HazeOf(Color ground,Color skyLow){var h=Color.Lerp(ground,skyLow,.45f);h.a=1f;return h;}
  /// <summary>這張地圖的地面色、天空底色（地面碗與地面網格的遠方淡出用）</summary>
  public static void HorizonColors(ExploreMap map,out Color ground,out Color skyLow)
  {
   if(map!=null&&map.theme=="castle"){ground=new Color(.16f,.10f,.12f);skyLow=new Color(.12f,.08f,.11f);return;}
   if(map==null||map.dungeon==null||!(map.nodeAreas||map.theme!="jungle")){ground=new Color(.30f,.42f,.24f);skyLow=new Color(.64f,.84f,.83f);return;}
   int theme=map.nodeAreas?5:map.theme=="volcano"?0:map.theme=="sea"?2:map.theme=="arena"?3:4;
   skyLow=theme==0?new Color(.58f,.23f,.14f):theme==2?new Color(.68f,.86f,.89f):theme==5?new Color(.32f,.29f,.22f):new Color(.86f,.68f,.46f);
   ground=theme==0?new Color(.20f,.17f,.18f):theme==2?new Color(.72f,.68f,.55f):theme==3?new Color(.58f,.44f,.30f):theme==5?new Color(.28f,.28f,.30f):new Color(.55f,.33f,.22f);
  }
  // Create once beneath the exploration world root, not inside the 24m streamed cell set.
  // Its localPosition is already the map centre. All distant geometry is outside the playable map.
  public static GameObject CreateBackdrop(ExploreMap map)
  {
   if(map==null)return null;if(map.dungeon!=null&&(map.nodeAreas||map.theme!="jungle"))return CreateDungeonBackdrop(map);var root=Root("Sky backdrop");root.transform.localPosition=(map.World(0,0)+map.World(map.W-1,map.H-1))*.5f;
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
   {HorizonColors(map,out var gc,out var sl);GroundBowl(root.transform,gc,sl,radius*1.7f);}   // 叢林：草地色
   return root;
  }
 }
}
