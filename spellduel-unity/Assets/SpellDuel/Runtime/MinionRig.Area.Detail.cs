using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 public partial class MinionRig
 {
  // Geometry is baked once per species / joint / colour layer, never rebuilt while animating.
  static readonly Dictionary<string,Mesh> rareMeshes=new Dictionary<string,Mesh>();
  static Material rareEelMaterial;
  static Vector3 R(float x,float y,float z)=>new Vector3(x,y,z);
  Transform RarePart(Transform parent,string name,Color colour,Action<PMesh> build,int light=0)
  {
   string key=kind+"/"+name;
   if(!rareMeshes.TryGetValue(key,out var mesh)||!mesh){var b=new PMesh(1);build(b);mesh=b.Build("Rare "+key,true,true);RareSmooth(mesh);rareMeshes[key]=mesh;}
   var t=Part(parent,mesh,Vector3.zero,Vector3.one,Vector3.zero,colour,.35f);t.name=name;
   if(light>0){var rr=t.GetComponent<Renderer>();rr.sharedMaterial=glowMat;
    if(light==1||light==2){rockList.RemoveAt(rockList.Count-1);rockCols.RemoveAt(rockCols.Count-1);rockGlows.RemoveAt(rockGlows.Count-1);if(light==1)lavaList.Add(rr);else eyeList.Add(rr);}
   }
   return t;
  }
  static void RareSmooth(Mesh mesh)
  {
   var vs=mesh.vertices;var original=mesh.normals;var ns=new Vector3[vs.Length];var groups=new Dictionary<Vector3Int,List<Vector3>>();
   for(int i=0;i<vs.Length;i++){var k=Vector3Int.RoundToInt(vs[i]*100000);if(!groups.TryGetValue(k,out var list)){list=new List<Vector3>();groups[k]=list;}list.Add(original[i]);}
   for(int i=0;i<vs.Length;i++){var sum=Vector3.zero;foreach(var n in groups[Vector3Int.RoundToInt(vs[i]*100000)])if(Vector3.Dot(original[i],n)>.1f)sum+=n;ns[i]=sum.normalized;}
   mesh.normals=ns;
  }
  static Vector3 RareCurve(Vector3[] p,float t)
  {
   float u=Mathf.Clamp01(t)*(p.Length-1);int i=Mathf.Min(p.Length-2,(int)u);u-=i;
   var a=p[Mathf.Max(0,i-1)];var b=p[i];var c=p[i+1];var d=p[Mathf.Min(p.Length-1,i+2)];
   return .5f*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
  }
  static void RareTube(PMesh b,Vector3[] path,float[] widths,int steps=8,int sides=7,float flatten=1,bool caps=true)
  {
   var rings=new Vector3[steps+1,sides];var total=path[path.Length-1]-path[0];
   var axis=Mathf.Abs(Vector3.Dot(total.normalized,Vector3.up))>.8f?Vector3.forward:Vector3.up;
   for(int j=0;j<=steps;j++){
    float t=j/(float)steps;var centre=RareCurve(path,t);var tangent=(RareCurve(path,Mathf.Min(1,t+.003f))-RareCurve(path,Mathf.Max(0,t-.003f))).normalized;
    var side=Vector3.Cross(axis,tangent).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;var normal=Vector3.Cross(tangent,side).normalized;
    float f=t*(widths.Length-1);int k=Mathf.Min(widths.Length-2,(int)f);float radius=Mathf.Lerp(widths[k],widths[k+1],f-k);
    for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;rings[j,i]=centre+radius*(side*Mathf.Cos(a)+normal*Mathf.Sin(a)*flatten);}
   }
   for(int j=0;j<steps;j++)for(int i=0;i<sides;i++){int n=(i+1)%sides;b.Tri(rings[j,i],rings[j+1,n],rings[j+1,i],Color.white);b.Tri(rings[j,i],rings[j,n],rings[j+1,n],Color.white);}
   if(caps)for(int i=0;i<sides;i++){int n=(i+1)%sides;b.Tri(path[0],rings[0,n],rings[0,i],Color.white);b.Tri(path[path.Length-1],rings[steps,i],rings[steps,n],Color.white);}
  }
  static void RareRod(PMesh b,Vector3 a,Vector3 c,float radius,float end=0,int sides=5)
  {
   if(end>.002f){RareTube(b,new[]{a,c},new[]{radius,end},1,sides);return;}
   var axis=(c-a).normalized;var side=Vector3.Cross(Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up,axis).normalized;var normal=Vector3.Cross(axis,side);
   for(int i=0;i<sides;i++){float u=i*Mathf.PI*2/sides,v=(i+1)*Mathf.PI*2/sides;var p=a+radius*(side*Mathf.Cos(u)+normal*Mathf.Sin(u));var q=a+radius*(side*Mathf.Cos(v)+normal*Mathf.Sin(v));b.Tri(p,q,c,Color.white);b.Tri(a,q,p,Color.white);}
  }
  static void RareForm(PMesh b,Vector3 at,Vector3 size,int sides=8)
  {b.Lathe(new[]{-.5f,0f,-.36f,.68f,-.12f,1f,.18f,.94f,.38f,.60f,.5f,0f},sides,null,size.x*.5f,size.y*.5f,off:at,warp:p=>R(p.x,p.y,p.z*size.z));}
  static void RarePlate(PMesh b,Vector3 at,Vector3 size,float tilt=0)
  {
   var q=Quaternion.Euler(0,0,tilt);var rim=new Vector3[6];var inner=new Vector3[6];
   for(int i=0;i<6;i++){float a=i*Mathf.PI/3;rim[i]=at+q*R(Mathf.Cos(a)*size.x*.5f,0,Mathf.Sin(a)*size.z*.5f);inner[i]=at+q*R(Mathf.Cos(a)*size.x*.35f,size.y*.65f,Mathf.Sin(a)*size.z*.35f);}
   var top=at+q*R(.03f*size.x,size.y,0);
   for(int i=0;i<6;i++){int n=(i+1)%6;b.Quad(rim[i],inner[i],inner[n],rim[n],Color.white);b.Tri(inner[i],top,inner[n],Color.white);b.Tri(at,rim[i],rim[n],Color.white);}
  }
  static void RareLeaf(PMesh b,Vector3 baseAt,Vector3 tip,float width)
  {var side=Vector3.Cross((tip-baseAt).normalized,Vector3.forward).normalized*width;if(side.sqrMagnitude<.00001f)side=Vector3.right*width;var mid=Vector3.Lerp(baseAt,tip,.55f)+Vector3.forward*.02f;b.Quad(baseAt,mid+side,tip,mid-side,Color.white);b.Quad(baseAt,mid-side,tip,mid+side,Color.white);}
  static void RareRing(PMesh b,Vector3 centre,Vector2 radius,float thickness,Quaternion q,int segments=10,int sides=4)
  {
   for(int j=0;j<segments;j++)for(int i=0;i<sides;i++){
    float a=j*Mathf.PI*2/segments,c=(j+1)*Mathf.PI*2/segments,u=i*Mathf.PI*2/sides,v=(i+1)*Mathf.PI*2/sides;
    Func<float,float,Vector3> p=(t,h)=>centre+q*R(Mathf.Cos(t)*(radius.x+thickness*Mathf.Cos(h)),Mathf.Sin(t)*(radius.y+thickness*Mathf.Cos(h)),thickness*Mathf.Sin(h));
    b.Quad(p(a,u),p(c,u),p(c,v),p(a,v),Color.white);
   }
  }
  void RareEyes(Transform parent,string label,float x,float y,float z,Color iris,float size=.065f)
  {
   RarePart(parent,label+" sockets",new Color(.19f,.13f,.12f),b=>{for(int s=-1;s<=1;s+=2)RareForm(b,R(s*x,y,z-.014f),R(size*2.9f,size*2.1f,.035f),6);});
   RarePart(parent,label+" luminous eyes",iris,b=>{for(int s=-1;s<=1;s+=2)RareForm(b,R(s*x,y,z+.009f),R(size*1.8f,size*1.2f,.024f),6);},2);
   RarePart(parent,label+" catchlights",Color.white,b=>{for(int s=-1;s<=1;s+=2)RareForm(b,R(s*x-.012f,y+.014f,z+.025f),R(.019f,.019f,.014f),5);},3);
  }
  void BuildAncientRootBeast()
  {
   spine.localPosition=R(0,.64f,0);var bark=new Color(.49f,.29f,.13f);var pale=new Color(.69f,.47f,.23f);var moss=new Color(.40f,.65f,.16f);
   RarePart(spine,"Interwoven structural roots",bark,b=>{
    for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9;var path=new Vector3[5];for(int j=0;j<5;j++){float z=-.68f+j*.34f,t=a+j*.40f+Mathf.Sin(j*1.1f+i)*.24f;path[j]=R(Mathf.Cos(t)*(.31f+.1f*Mathf.Sin(j*Mathf.PI/4)),Mathf.Sin(t)*.25f,z);}RareTube(b,path,new[]{.07f,.13f,.14f,.12f,.06f},6,6);}
   });
   RarePart(spine,"Bark ridges and fissures",pale,b=>{for(int i=0;i<7;i++){float a=i*Mathf.PI*2/7;RareTube(b,new[]{R(Mathf.Cos(a)*.47f,Mathf.Sin(a)*.28f,-.55f),R(Mathf.Cos(a+.13f)*.47f,Mathf.Sin(a+.13f)*.30f,0),R(Mathf.Cos(a)*.44f,Mathf.Sin(a)*.27f,.56f)},new[]{.008f,.018f,.003f},5,4);}});
   for(int i=0;i<4;i++){
    int n=i;var leg=areaLegs[i]=Pivot("Articulated leg",spine,R((i%2==0?-1:1)*.43f,-.04f,i<2?.49f:-.48f));
    RarePart(leg,"Root claw "+i,bark,b=>{for(int j=-1;j<=1;j++)RareTube(b,new[]{R(j*.07f,.02f,0),R(j*.09f,-.23f,n<2?-.13f:.16f),R(j*.10f,-.45f,n<2?.04f:-.08f),R(j*.12f,-.53f,.08f),R(j*.16f,-.59f,.23f)},new[]{.07f,n<2?.065f:.085f,.04f,.045f,.008f},6,5);});
    RarePart(leg,"Claw bark "+i,pale,b=>{for(int j=-1;j<=1;j++)RareRod(b,R(j*.11f,-.49f,.12f),R(j*.16f,-.59f,.24f),.022f,.003f);});
   }
   areaFrontL=areaLegs[0];areaFrontR=areaLegs[1];
   RarePart(spine,"Moss garden",moss,b=>{for(int i=0;i<4;i++)RarePlate(b,R((i%2==0?-.18f:.19f),.31f,-.51f+i*.32f),R(.45f,.09f,.44f));});
   RarePart(spine,"Sap spine",new Color(.78f,.97f,.14f),b=>{for(int i=0;i<5;i++)RareTube(b,new[]{R(0,.35f,-.6f+i*.25f),R(.035f,.4f,-.52f+i*.25f),R(0,.37f,-.44f+i*.25f)},new[]{.013f,.022f,.009f},2,4);},1);
   RarePart(spine,"Back forest trunks",bark,b=>{for(int i=0;i<3;i++){float x=(i%2==0?-.22f:.23f),z=-.43f+i*.34f;RareTube(b,new[]{R(x,.35f,z),R(x-.04f,.57f,z),R(x+.03f,.80f-i*.05f,z)},new[]{.045f,.027f,.003f},3,5);for(int s=-1;s<=1;s+=2)RareRod(b,R(x,.57f,z),R(x+s*.13f,.70f,z+.06f),.016f,.002f);}});
   RarePart(spine,"Forest leaves and ferns",new Color(.23f,.52f,.16f),b=>{for(int i=0;i<3;i++){float x=(i%2==0?-.22f:.23f),z=-.43f+i*.34f;for(int j=0;j<5;j++){float a=j*2.4f;RareLeaf(b,R(x,.69f,z),R(x+Mathf.Cos(a)*.19f,.78f+Mathf.Sin(a)*.09f,z+Mathf.Sin(a)*.15f),.085f);}}for(int j=0;j<7;j++){float z=-.5f+j*.15f;RareLeaf(b,R(.28f,.35f,z),R(.47f,.45f,z+.1f),.045f);}});
   RarePart(spine,"Ivory mushroom stems",new Color(.86f,.78f,.53f),b=>{for(int i=0;i<3;i++)RareRod(b,R(-.35f,.32f,-.4f+i*.36f),R(-.35f,.44f,-.4f+i*.36f),.018f,.012f);});
   RarePart(spine,"Amber mushroom caps",new Color(.87f,.39f,.13f),b=>{for(int i=0;i<3;i++)RarePlate(b,R(-.35f,.44f,-.4f+i*.36f),R(.15f,.045f,.13f));});
   head=Pivot("Root crown",spine,R(0,.12f,.82f));
   RarePart(head,"Stump crown",bark,b=>{RareTube(b,new[]{R(0,-.24f,0),R(0,-.07f,.02f),R(0,.2f,0),R(0,.29f,-.02f)},new[]{.21f,.32f,.29f,.24f},3,9,.9f);for(int s=-1;s<=1;s+=2){RareTube(b,new[]{R(s*.21f,.2f,0),R(s*.31f,.43f,-.03f),R(s*.40f,.62f,-.02f),R(s*.48f,.75f,.04f)},new[]{.075f,.054f,.033f,.001f},4,5);RareTube(b,new[]{R(s*.32f,.45f,-.03f),R(s*.47f,.53f,.01f),R(s*.55f,.62f,.02f)},new[]{.035f,.023f,.001f},3,4);}});
   RarePart(head,"Cut wood grain",pale,b=>{RarePlate(b,R(0,.29f,-.02f),R(.43f,.012f,.40f));for(int s=-1;s<=1;s+=2)RareTube(b,new[]{R(s*.28f,.12f,.17f),R(s*.26f,-.1f,.19f),R(s*.18f,-.22f,.2f)},new[]{.012f,.018f,.005f},3,4);});
   RarePart(head,"Hanging vines",moss,b=>{for(int s=-1;s<=1;s+=2){RareTube(b,new[]{R(s*.43f,.58f,0),R(s*.48f,.34f,.03f),R(s*.37f,.26f,.06f)},new[]{.012f,.012f,.003f},4,4);RareLeaf(b,R(s*.43f,.48f,0),R(s*.56f,.42f,.06f),.06f);}});
   RareEyes(head,"Hollow",.16f,.03f,.276f,new Color(1,.90f,.25f),.065f);
  }
  void BuildTidewhipEel()
  {
   spine.localPosition=R(0,.16f,0);var blue=new Color(.12f,.48f,.67f);var aqua=new Color(.26f,.83f,.79f);var ivory=new Color(.74f,.91f,.78f);
   var points=new[]{R(0,0,-.08f),R(-.10f,.27f,.02f),R(-.11f,.56f,.12f),R(.02f,.81f,.22f),R(0,1.19f,.42f)};
   var tube=RarePart(spine,"Continuous curved eel",Color.white,b=>RareTube(b,points,new[]{.24f,.27f,.23f,.21f,.22f},16,12));
   if(!rareEelMaterial){rareEelMaterial=new Material(rockMat){name="Rare eel shared blue teal gradient"};var tex=new Texture2D(32,2,TextureFormat.RGB24,false){name="Procedural eel gradient",wrapMode=TextureWrapMode.Clamp};for(int x=0;x<32;x++)for(int y=0;y<2;y++)tex.SetPixel(x,y,Color.Lerp(new Color(.11f,.29f,.55f),aqua,x/31f));tex.Apply(false,true);rareEelMaterial.mainTexture=tex;}
   tube.GetComponent<Renderer>().sharedMaterial=rareEelMaterial;var mesh=tube.GetComponent<MeshFilter>().sharedMesh;if(mesh.uv.Length==0){var vs=mesh.vertices;var uv=new Vector2[vs.Length];for(int i=0;i<vs.Length;i++)uv[i]=new Vector2(Mathf.Clamp01((vs[i].y+.2f)/1.5f),.5f);mesh.uv=uv;}
   RarePart(spine,"Pale eel belly",ivory,b=>{
    var rings=new Vector3[17,3];float[] radii={.24f,.27f,.23f,.21f,.22f};
    for(int j=0;j<=16;j++){float t=j/16f;var p=RareCurve(points,t);var tangent=(RareCurve(points,Mathf.Min(1,t+.003f))-RareCurve(points,Mathf.Max(0,t-.003f))).normalized;var side=Vector3.Cross(Vector3.forward,tangent).normalized;var normal=Vector3.Cross(tangent,side);float f=t*4;int k=Mathf.Min(3,(int)f);float radius=Mathf.Lerp(radii[k],radii[k+1],f-k)*1.015f;
     for(int i=0;i<3;i++){float angle=Mathf.PI/3+i*Mathf.PI/6;rings[j,i]=p+radius*(side*Mathf.Cos(angle)+normal*Mathf.Sin(angle));}}
    for(int j=0;j<16;j++)for(int i=0;i<2;i++){b.Tri(rings[j,i],rings[j+1,i+1],rings[j+1,i],Color.white);b.Tri(rings[j,i],rings[j,i+1],rings[j+1,i+1],Color.white);}
   });
   RarePart(spine,"Bioluminescent flank spots",aqua,b=>{for(int i=0;i<7;i++){var p=RareCurve(points,.13f+i*.12f);for(int s=-1;s<=1;s+=2)RareForm(b,p+R(s*.22f,0,.05f),R(.035f,.05f,.035f),5);}},1);
   RarePart(spine,"Translucent dorsal sail",new Color(.19f,.8f,.85f,.48f),b=>{for(int j=0;j<10;j++){float t=j/10f,u=(j+1)/10f;var a=RareCurve(points,t)-Vector3.forward*.21f;var c=RareCurve(points,u)-Vector3.forward*.21f;var d=c-Vector3.forward*(Mathf.Sin(u*Mathf.PI)*.40f);var e=a-Vector3.forward*(Mathf.Sin(t*Mathf.PI)*.40f);b.Quad(a,c,d,e,Color.white);b.Quad(e,d,c,a,Color.white);}},3);
   RarePart(spine,"Dorsal fin rays",ivory,b=>{for(int j=1;j<10;j++){float t=j/10f;var a=RareCurve(points,t)-Vector3.forward*.21f;RareRod(b,a,a-Vector3.forward*(Mathf.Sin(t*Mathf.PI)*.40f),.009f,.003f,4);}});
   // Retain all five legacy segment pivots and the jaw parent, while the skin is one mesh.
   Transform section=spine;for(int i=0;i<5;i++)section=Pivot("Curved eel segment",section,R(0,i==0?0:.26f,i==0?0:.09f));
   head=Pivot("Eel jaw",section,R(0,.22f,.16f));
   RarePart(head,"Eel cranium",blue,b=>RareTube(b,new[]{R(0,-.13f,-.26f),R(0,-.01f,-.12f),R(0,.02f,.14f),R(0,-.06f,.34f)},new[]{.20f,.25f,.22f,.12f},5,10,.7f));
   RarePart(head,"Mouth interior",new Color(.26f,.13f,.24f),b=>RareForm(b,R(0,-.13f,.18f),R(.35f,.10f,.38f),8));
   RarePart(head,"Sculpted lower jaw",ivory,b=>RareTube(b,new[]{R(0,-.12f,-.08f),R(0,-.21f,.12f),R(0,-.20f,.34f)},new[]{.15f,.16f,.08f},4,8,.34f));
   RarePart(head,"Small needle teeth",ivory,b=>{for(int s=-1;s<=1;s+=2)for(int i=0;i<5;i++){float z=.02f+i*.055f;RareRod(b,R(s*(.16f-i*.012f),-.095f,z),R(s*(.14f-i*.01f),-.17f,z+.01f),.018f,.001f,4);}});
   RarePart(head,"Gill slits",new Color(.09f,.24f,.33f),b=>{for(int s=-1;s<=1;s+=2)for(int i=0;i<3;i++)RareTube(b,new[]{R(s*.205f,.015f,-.19f+i*.055f),R(s*.23f,-.05f,-.19f+i*.055f),R(s*.18f,-.12f,-.16f+i*.055f)},new[]{.006f,.012f,.003f},2,4);});
   RareEyes(head,"Eel",.18f,.055f,.18f,new Color(1,.72f,.18f),.08f);
   areaTail=Pivot("Whip tail",spine,R(0,.15f,-.15f));
   RarePart(areaTail,"Continuous whip tail",blue,b=>RareTube(b,new[]{R(0,0,-.12f),R(.13f,.03f,-.49f),R(.35f,.12f,-.87f),R(.60f,.27f,-1.27f)},new[]{.22f,.16f,.105f,.045f},12,9,.65f));
   RarePart(areaTail,"Fan tail membrane",new Color(.2f,.82f,.84f,.6f),b=>{for(int j=0;j<8;j++){float a=(-.7f+j*.175f),c=a+.175f;var at=R(.60f,.27f,-1.27f);var d=at+R(Mathf.Sin(a)*.36f,Mathf.Sin(j*.6f)*.03f,-Mathf.Cos(a)*.35f);var e=at+R(Mathf.Sin(c)*.36f,Mathf.Sin((j+1)*.6f)*.03f,-Mathf.Cos(c)*.35f);b.Tri(at,d,e,Color.white);b.Tri(at,e,d,Color.white);}},3);
   RarePart(areaTail,"Fan tail rays",ivory,b=>{for(int j=0;j<=8;j++){float a=-.7f+j*.175f;var at=R(.60f,.27f,-1.27f);RareRod(b,at,at+R(Mathf.Sin(a)*.36f,Mathf.Sin(j*.6f)*.03f,-Mathf.Cos(a)*.35f),.008f,.002f,4);}});
   RarePart(body,"Puddle ripples and spray",new Color(.28f,.8f,.91f,.43f),b=>{for(int k=0;k<3;k++){float r=.35f+k*.20f;RareRing(b,R(0,.022f,0),new Vector2(r,r*.8f),.016f,Quaternion.Euler(90,0,0),24,3);}for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var at=R(Mathf.Cos(a)*.42f,.02f,Mathf.Sin(a)*.33f);RareTube(b,new[]{at,at+R(Mathf.Cos(a)*.07f,.16f,Mathf.Sin(a)*.07f),at+R(Mathf.Cos(a)*.13f,.09f,Mathf.Sin(a)*.13f)},new[]{.028f,.016f,.001f},3,4);}},3);
  }
  void BuildChainGladiator()
  {
   spine.localPosition=R(0,.86f,0);var skin=new Color(.76f,.48f,.32f);var iron=new Color(.42f,.51f,.62f);var trim=new Color(.73f,.70f,.53f);var leather=new Color(.35f,.22f,.15f);var red=new Color(.69f,.17f,.19f);
   RarePart(spine,"Tapered muscular torso",skin,b=>{
    RareTube(b,new[]{R(0,.55f,0),R(0,.68f,0),R(0,.76f,0)},new[]{.16f,.13f,.14f},2,7,.84f);
    RareTube(b,new[]{R(0,-.15f,0),R(0,.03f,0),R(0,.27f,0),R(0,.48f,0),R(0,.61f,0)},new[]{.29f,.28f,.40f,.40f,.19f},5,12,.65f);
    for(int s=-1;s<=1;s+=2){RareForm(b,R(s*.20f,.40f,.19f),R(.42f,.29f,.19f),8);for(int i=0;i<3;i++)RareForm(b,R(s*.095f,.04f+i*.09f,.20f),R(.19f,.13f,.09f),5);}
   });
   RarePart(spine,"Healed chest scars",new Color(.57f,.29f,.24f),b=>{RareTube(b,new[]{R(-.31f,.47f,.26f),R(-.19f,.37f,.293f),R(-.10f,.29f,.27f)},new[]{.007f,.011f,.005f},3,4);for(int i=0;i<3;i++)RareRod(b,R(-.27f+i*.055f,.43f-i*.055f,.291f),R(-.22f+i*.055f,.45f-i*.055f,.291f),.004f,.004f,4);});
   RarePart(spine,"Leather belt and shoulder harness",leather,b=>{RareTube(b,new[]{R(0,-.16f,0),R(0,-.04f,0)},new[]{.34f,.33f},1,12,.73f);RareTube(b,new[]{R(-.34f,.54f,.04f),R(-.27f,.45f,.25f),R(-.16f,.23f,.26f),R(-.05f,-.02f,.22f)},new[]{.035f,.035f,.035f,.035f},5,4,.4f);});
   RarePart(spine,"Overlapping leather war skirt",red,b=>{for(int i=0;i<9;i++){float a=i*Mathf.PI*2/9,c=a+.53f;var p=R(Mathf.Cos(a)*.32f,-.15f,Mathf.Sin(a)*.25f);var q=R(Mathf.Cos(c)*.32f,-.15f,Mathf.Sin(c)*.25f);var r=R(Mathf.Cos(c)*.36f,-.39f,Mathf.Sin(c)*.29f);var t=R(Mathf.Cos(a)*.36f,-.37f,Mathf.Sin(a)*.29f);b.Quad(p,q,r,t,Color.white);b.Quad(t,r,q,p,Color.white);}});
   RarePart(spine,"Belt buckle and skirt studs",trim,b=>{RareRing(b,R(0,-.10f,.255f),new Vector2(.065f,.043f),.011f,Quaternion.identity,8);for(int i=-2;i<=2;i++)RareRod(b,R(i*.10f,-.09f,.245f),R(i*.10f,-.09f,.262f),.013f,.007f,4);});
   for(int s=-1;s<=1;s+=2){int side=s;var leg=areaLegs[s<0?0:1]=Pivot("Gladiator leg",body,R(s*.24f,.77f,0));
    RarePart(leg,"Muscle leg "+s,skin,b=>RareTube(b,new[]{R(0,0,0),R(0,-.19f,0),R(0,-.36f,.03f),R(0,-.51f,0),R(0,-.63f,.02f)},new[]{.15f,.17f,.12f,.14f,.09f},6,8,.94f));
    RarePart(leg,"Boot and shin plate "+s,iron,b=>{RareForm(b,R(0,-.65f,.07f),R(.33f,.23f,.46f),8);RareTube(b,new[]{R(0,-.33f,.12f),R(0,-.44f,.15f),R(0,-.59f,.12f)},new[]{.10f,.12f,.08f},3,6,.28f);});
    RarePart(leg,"Leg wraps "+s,leather,b=>{for(int i=0;i<2;i++)RareRing(b,R(0,-.37f-i*.15f,0),new Vector2(.126f,.13f),.018f,Quaternion.Euler(90,0,0),8,3);});
    var arm=Pivot("Sloping deltoid",spine,R(s*.47f,.52f,0));if(s>0)areaFrontR=arm;else areaFrontL=arm;
    RarePart(arm,"Defined shoulder biceps forearm "+s,skin,b=>{
     RareTube(b,new[]{R(0,.01f,0),R(s*.06f,-.13f,0),R(s*.07f,-.29f,0),R(s*.08f,-.40f,.03f),R(s*.08f,-.55f,.06f)},new[]{.16f,.19f,.12f,.14f,.09f},6,9,.92f);
     RareForm(b,R(s*.07f,-.18f,.10f),R(.23f,.30f,.16f),6);RareForm(b,R(s*.08f,-.55f,.06f),R(.21f,.18f,.19f),6);
    });
    RarePart(arm,"Bracer and broken manacle "+s,iron,b=>{RareTube(b,new[]{R(s*.08f,-.37f,.04f),R(s*.08f,-.48f,.05f)},new[]{.145f,.115f},1,8);RareRing(b,R(s*.08f,-.51f,.06f),new Vector2(.122f,.115f),.022f,Quaternion.Euler(90,0,0),8);for(int j=0;j<2;j++)RareRing(b,R(s*.20f,-.51f-j*.075f,.06f),new Vector2(.029f,.043f),.009f,Quaternion.Euler(0,j*90,0),6,3);});
    if(s<0){RarePart(arm,"Single riveted shoulder armour",iron,b=>{RarePlate(b,R(-.035f,.09f,0),R(.43f,.12f,.41f),-18);RarePlate(b,R(-.16f,-.04f,.01f),R(.25f,.075f,.39f),-35);});
     RarePart(arm,"Shoulder rivets",trim,b=>{for(int j=0;j<4;j++){float a=j*Mathf.PI/2;RareRod(b,R(-.04f+Mathf.Cos(a)*.15f,.17f,Mathf.Sin(a)*.13f),R(-.04f+Mathf.Cos(a)*.15f,.20f,Mathf.Sin(a)*.13f),.018f,.009f,4);}});}
   }
   head=Pivot("Iron mask",spine,R(0,.91f,0));
   RarePart(head,"Bevelled iron mask",iron,b=>RareTube(b,new[]{R(0,-.24f,0),R(0,-.12f,0),R(0,.10f,0),R(0,.25f,-.015f)},new[]{.15f,.23f,.23f,.12f},3,10,.92f));
   RarePart(head,"Dark mouth grille backing",new Color(.18f,.22f,.28f),b=>RareForm(b,R(0,-.12f,.214f),R(.27f,.16f,.035f),6));
   RarePart(head,"Grille bars mask rivets",trim,b=>{for(int i=-2;i<=2;i++)RareRod(b,R(i*.045f,-.19f,.242f),R(i*.045f,-.05f,.242f),.009f,.009f,4);for(int s=-1;s<=1;s+=2)for(int i=0;i<3;i++)RareRod(b,R(s*.19f,-.14f+i*.13f,.14f),R(s*.19f,-.14f+i*.13f,.16f),.016f,.009f,4);RareRod(b,R(0,-.04f,.235f),R(0,.18f,.22f),.024f,.018f,4);});
   RarePart(head,"Red horsehair crest",red,b=>{for(int i=0;i<6;i++)RareTube(b,new[]{R(0,.20f,-.17f+i*.055f),R(0,.32f,-.20f+i*.055f),R(0,.38f,-.24f+i*.055f)},new[]{.035f,.025f,.002f},2,4);});
   RareEyes(head,"Mask slit",.105f,.065f,.214f,new Color(1,.57f,.2f),.042f);
   areaWeapon=Pivot("Chain rotating from fist",areaFrontR,R(.08f,-.56f,.06f));
   RarePart(areaWeapon,"Interlocked iron chain",iron,b=>{for(int i=0;i<9;i++)RareRing(b,R(.10f+i*.12f,0,0),new Vector2(.078f,.040f),.012f,Quaternion.Euler(i%2*90,0,0),8,4);RareForm(b,R(1.23f,0,0),Vector3.one*.34f,10);});
   RarePart(areaWeapon,"Morningstar spikes",trim,b=>{for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var dir=R(0,Mathf.Cos(a),Mathf.Sin(a));RareRod(b,R(1.23f,0,0)+dir*.15f,R(1.23f,0,0)+dir*.28f,.043f,.001f,5);}RareRod(b,R(1.38f,0,0),R(1.50f,0,0),.04f,.001f);});
  }
  void BuildRiftplateLizard()
  {
   spine.localPosition=R(0,.59f,0);var hide=new Color(.66f,.40f,.20f);var stone=new Color(.83f,.61f,.35f);var pale=new Color(.94f,.76f,.46f);var lava=new Color(1,.39f,.08f);
   RarePart(spine,"Low tapered reptile torso",hide,b=>RareTube(b,new[]{R(0,-.03f,-.79f),R(0,0,-.42f),R(0,0,.13f),R(0,-.03f,.72f)},new[]{.17f,.47f,.46f,.21f},8,10,.65f));
   for(int i=0;i<4;i++){
    int n=i;float side=i%2==0?-1:1;var knee=R(side*.13f,-.20f,i<2?-.13f:.17f);var hock=R(side*.045f,-.42f,i<2?.05f:-.07f);
    var leg=areaLegs[i]=Pivot("Articulated leg",spine,R((i%2==0?-1:1)*.43f,-.04f,i<2?.49f:-.48f));
    RarePart(leg,"Reptile haunch and toes "+i,hide,b=>{RareTube(b,new[]{R(0,0,0),R(side*.08f,-.08f,n<2?-.05f:.07f),knee},new[]{.15f,n<2?.14f:.17f,.10f},3,7,.88f);RareTube(b,new[]{knee,hock,R(0,-.50f,.14f)},new[]{.10f,.065f,.10f},4,7,.85f);for(int j=-1;j<=1;j++)RareTube(b,new[]{R(j*.07f,-.49f,.09f),R(j*.10f,-.53f,.19f),R(j*.13f,-.53f,.28f)},new[]{.043f,.034f,.015f},2,4);});
    RarePart(leg,"Stone claws "+i,pale,b=>{for(int j=-1;j<=1;j++)RareRod(b,R(j*.13f,-.53f,.24f),R(j*.14f,-.53f,.34f),.03f,.002f,4);RarePlate(b,knee+R(0,.015f,.035f),R(.19f,.05f,.19f));});
   }
   areaFrontL=areaLegs[0];areaFrontR=areaLegs[1];
   RarePart(spine,"Overlapping fractured back plates",stone,b=>{for(int i=0;i<5;i++)for(int s=-1;s<=1;s+=2){float z=-.60f+i*.27f;RarePlate(b,R(s*.24f,.25f,z),R(.49f,.15f,.41f),s*20);}});
   RarePart(spine,"Plate cleavage ledges and sand scales",pale,b=>{for(int i=0;i<5;i++)for(int s=-1;s<=1;s+=2){float z=-.6f+i*.27f;RarePlate(b,R(s*.32f,.27f,z+.1f),R(.27f,.03f,.13f),s*20);for(int j=0;j<2;j++)RarePlate(b,R(s*.43f,.02f-j*.10f,z),R(.1f,.025f,.18f),s*65);}});
   RarePart(spine,"Lava fissure network",lava,b=>{for(int i=0;i<5;i++){float z=-.58f+i*.27f;RareTube(b,new[]{R(-.37f,.30f,z),R(-.18f,.405f,z+.05f),R(0,.34f,z),R(.17f,.41f,z-.04f),R(.39f,.30f,z+.02f)},new[]{.009f,.015f,.013f,.011f,.005f},4,4);}},1);
   RarePart(spine,"Loose gravel on plate rims",new Color(.61f,.44f,.27f),b=>{for(int i=0;i<8;i++)RarePlate(b,R(i%2==0?-.35f:.35f,.32f,-.62f+i*.17f),R(.065f,.04f,.075f));});
   head=Pivot("Lizard head",spine,R(0,-.02f,.92f));
   RarePart(head,"Wedge rock skull",stone,b=>RareTube(b,new[]{R(0,0,-.29f),R(0,.025f,-.10f),R(0,-.02f,.19f),R(0,-.06f,.35f)},new[]{.23f,.31f,.25f,.14f},3,8,.60f));
   RarePart(head,"Rock lower jaw",hide,b=>RareTube(b,new[]{R(0,-.10f,-.12f),R(0,-.19f,.12f),R(0,-.17f,.33f)},new[]{.19f,.20f,.11f},3,7,.28f));
   RarePart(head,"Rock horns and fangs",pale,b=>{for(int s=-1;s<=1;s+=2){RareTube(b,new[]{R(s*.19f,.13f,-.08f),R(s*.24f,.27f,-.18f),R(s*.26f,.36f,-.26f)},new[]{.067f,.04f,.001f},3,5);for(int i=0;i<3;i++)RareRod(b,R(s*(.19f-i*.025f),-.14f,.08f+i*.08f),R(s*(.18f-i*.025f),-.075f,.10f+i*.08f),.025f,.001f,4);}});
   RareEyes(head,"Amber",.20f,.04f,.235f,new Color(1,.43f,.08f),.052f);
   areaTail=Pivot("Tapering stone tail",spine,R(0,0,-.72f));
   RarePart(areaTail,"Heavy curved tail",hide,b=>RareTube(b,new[]{R(0,0,-.08f),R(.10f,-.05f,-.43f),R(.25f,-.13f,-.82f),R(.42f,-.17f,-1.24f)},new[]{.22f,.19f,.13f,.10f},7,8,.75f));
   RarePart(areaTail,"Tail armour and rock hammer",stone,b=>{for(int i=0;i<4;i++)RarePlate(b,R(.06f+i*.085f,.13f-i*.04f,-.19f-i*.24f),R(.35f-i*.04f,.08f,.31f));RareForm(b,R(.42f,-.16f,-1.23f),R(.39f,.29f,.40f),7);});
   RarePart(areaTail,"Tail hammer ember seam",lava,b=>RareTube(b,new[]{R(.27f,-.12f,-1.24f),R(.41f,-.025f,-1.26f),R(.55f,-.12f,-1.25f)},new[]{.009f,.014f,.006f},3,4),1);
  }
 }
}
