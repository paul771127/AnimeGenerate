using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  static readonly System.Collections.Generic.Dictionary<ExGround,Material> groundMaterials=new System.Collections.Generic.Dictionary<ExGround,Material>();
  static Transform GroundSurface(Transform parent)
  {
   const string key="ground/fine/surface";
   if(!meshes.TryGetValue(key,out var mesh)||!mesh)
   {
    // UVs survive Unity's dynamic batching; object-space POSITION does not.
    mesh=new Mesh{name="Explore cached fine ground"};
    mesh.vertices=new[]{V(-1.53f,.002f,-1.53f),V(-1.53f,.002f,1.53f),V(1.53f,.002f,1.53f),V(1.53f,.002f,-1.53f)};
    mesh.uv=new[]{new Vector2(-1.53f,-1.53f),new Vector2(-1.53f,1.53f),new Vector2(1.53f,1.53f),new Vector2(1.53f,-1.53f)};
    mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();meshes[key]=mesh;
   }
   return AnimeFace.Part(parent,key,mesh,Vector3.zero,SkyArt.Glow).transform;
  }
  static Material GroundMaterial(ExGround ground)
  {
   if(groundMaterials.TryGetValue(ground,out var cached)&&cached)return cached;
   bool grass=ground==ExGround.Grass,dirt=ground==ExGround.Dirt,shore=ground==ExGround.Shore,cave=ground==ExGround.Cave;
   int size=grass?512:256;var pixels=new Color[size*size];
   Color low=grass?new Color(.23f,.34f,.13f):dirt?new Color(.32f,.25f,.17f):shore?new Color(.51f,.48f,.33f):cave?new Color(.26f,.30f,.28f):new Color(.30f,.38f,.32f);
   Color high=grass?new Color(.40f,.49f,.24f):dirt?new Color(.46f,.37f,.25f):shore?new Color(.67f,.62f,.45f):cave?new Color(.40f,.43f,.38f):new Color(.45f,.51f,.42f);
   for(int y=0;y<size;y++)for(int x=0;x<size;x++)
   {
    float u=x/(float)size,v=y/(float)size;
    float broad=GroundNoise(u,v,4),fine=GroundNoise(u,v,43);
    Color c=Color.Lerp(low,high,.24f+broad*.48f+fine*.18f);
    if(grass)c=Color.Lerp(c,new Color(.39f,.35f,.22f),Mathf.SmoothStep(.0f,.28f,Mathf.Clamp01((GroundNoise(u+.31f,v+.27f,7)-.52f)*3)));
    // Gentle worn joints on stone only, not giant raised polygons on every material.
    if(!grass&&!dirt&&!shore&&!cave)
    {float row=Mathf.Floor(v*5),a=Mathf.Repeat(u*5+row*.5f,1),b=Mathf.Repeat(v*5,1);float joint=Mathf.Min(Mathf.Min(a,1-a),Mathf.Min(b,1-b));c*=Mathf.Lerp(.83f,1,Mathf.SmoothStep(0,1,joint/.025f));}
    pixels[y*size+x]=c;
   }
   var rng=new System.Random(12347+(int)ground*379);
   int strokes=grass?11500:950;
   for(int n=0;n<strokes;n++)
   {
    float x=(float)rng.NextDouble()*size,y=(float)rng.NextDouble()*size,angle=(float)rng.NextDouble()*Mathf.PI*2;
    float length=grass?3+(float)rng.NextDouble()*8:.4f+(float)rng.NextDouble()*1.4f;
    float width=grass?.45f+(float)rng.NextDouble()*.7f:.45f+(float)rng.NextDouble()*.55f;
    Color tint=grass?Color.Lerp(new Color(.22f,.34f,.12f),new Color(.57f,.62f,.31f),(float)rng.NextDouble()):Color.Lerp(low*.88f,high,(float)rng.NextDouble());
    GroundStroke(pixels,size,x,y,angle,length,width,tint,grass?.68f:.35f);
   }
   var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name="Explore generated fine ground "+ground,wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
   texture.SetPixels(pixels);texture.Apply(true,true);
   var shader=Resources.Load<Shader>("Shaders/ExploreGround");
   var mat=new Material(shader?shader:SkyArt.Glow.shader){name="Explore shared fine ground "+ground};mat.mainTexture=texture;
   mat.SetFloat("_Opacity",grass?.42f:.34f);groundMaterials[ground]=mat;return mat;
  }
  // Periodic smooth value noise: the last texels blend continuously into the first.
  static float GroundNoise(float u,float v,int cells)
  {
   float x=Mathf.Repeat(u,1)*cells,y=Mathf.Repeat(v,1)*cells;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);
   float a=x-ix,b=y-iy;a=a*a*(3-2*a);b=b*b*(3-2*b);
   return Mathf.Lerp(Mathf.Lerp(GroundHash(ix,iy,cells),GroundHash(ix+1,iy,cells),a),Mathf.Lerp(GroundHash(ix,iy+1,cells),GroundHash(ix+1,iy+1,cells),a),b);
  }
  static float GroundHash(int x,int y,int cells)
  {unchecked{uint h=(uint)((x%cells)*374761393+(y%cells)*668265263+cells*7919);h=(h^(h>>13))*1274126177;return (h&65535)/65535f;}}
  static void GroundStroke(Color[] pixels,int size,float cx,float cy,float angle,float length,float width,Color tint,float opacity)
  {
   float co=Mathf.Cos(angle),si=Mathf.Sin(angle);int reach=Mathf.CeilToInt(length+width+1);
   for(int y=Mathf.FloorToInt(cy)-reach;y<=Mathf.FloorToInt(cy)+reach;y++)for(int x=Mathf.FloorToInt(cx)-reach;x<=Mathf.FloorToInt(cx)+reach;x++)
   {
    float dx=x+.5f-cx,dy=y+.5f-cy,t=(dx*co+dy*si)/length;if(t<-.1f||t>1.1f)continue;
    float lateral=Mathf.Abs(-dx*si+dy*co-.65f*Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI));
    float coverage=Mathf.Clamp01(width*(1-Mathf.Clamp01(t))+.65f-lateral)*Mathf.Clamp01((1-t)*length+.5f)*Mathf.Clamp01(t*length+.5f);
    if(coverage<=0)continue;int index=((y%size+size)%size)*size+(x%size+size)%size;
    pixels[index]=Color.Lerp(pixels[index],tint,coverage*opacity);
   }
  }
 }
}
