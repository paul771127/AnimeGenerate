using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  // Seeds are quantised to four mesh keys per species; no per-frame generation or random state.
  static void NaturalTree(Geo b,int species,int seed)
  {
   RootFoot(b,Bark);float h=species==2?2.5f:species==3?2.7f:3.7f+seed*.12f;
   var fork=V((seed-1.5f)*.09f,h*.57f,.09f);var top=V(.18f,h,-.10f);
   b.Vine(V(0,.12f,0),fork,.14f,.27f,Bark,4);b.Rod(fork,top,.20f,Bark,7,.065f);
   if(species==0||species==2)
   {
    // Palm / fern: an arched rib with many narrow leaflets, not one broad diamond.
    int tiers=species==2?3:1;
    for(int tier=0;tier<tiers;tier++)for(int arm=0;arm<8;arm++)
    {float a=arm*Mathf.PI/4+seed*.4f+tier*.3f;var dir=V(Mathf.Cos(a),0,Mathf.Sin(a));var side=V(-dir.z,0,dir.x);var start=species==0?top:V(0,1.1f+tier*.65f,0);float length=species==0?.86f:.9f-tier*.09f;
     var prev=start;for(int j=1;j<=5;j++){float u=j/5f;var next=start+dir*(length*u)+V(0,Mathf.Sin(u*Mathf.PI)*.28f-u*.38f,0);b.Rod(prev,next,.013f,Moss,4,.009f);
      if(j<5)for(int sign=-1;sign<=1;sign+=2){float width=.19f*(1-u*.55f);b.Frond(next,next+side*(sign*width)+dir*.13f-V(0,.10f,0),.045f,(arm+tier)%2==0?Leaf:Moss);}prev=next;}}
    if(species==0)for(int i=0;i<3;i++)b.Crown(top+V((i-1)*.13f,-.15f,.08f),V(.18f,.24f,.18f),Bark,i,6,2);
   }
   else if(species==3)
   {
    Leaves(b,V(0,1,0),.9f,Leaf,9);b.Crown(V(.12f,2.65f,0),V(1.3f,.9f,1.2f),Leaf,seed,10,4);
    b.Crown(V(.12f,2.8f,.35f),V(.9f,.5f,.65f),new Color(.7f,.25f,.38f),seed,10,3);
    b.Box(V(.12f,2.81f,.66f),V(.72f,.10f,.035f),Dark);for(int i=-2;i<=2;i++)b.Gem(V(.12f+i*.14f,2.76f,.68f),V(.075f,.17f,.08f),Bone,4);
   }
   else
   {
    for(int arm=0;arm<5;arm++)
    {float a=arm*2.399f+seed*.55f;var tip=V(Mathf.Cos(a)*.56f,h-.48f+(arm%3)*.24f,Mathf.Sin(a)*.56f);b.Rod(fork,tip,.12f,Bark,6,.04f);
     b.Crown(tip,V(.95f,.9f+arm%2*.18f,.95f),Color.Lerp(Leaf,Moss,arm*.08f),seed+arm,9,3);
     b.Crown(tip+V(-Mathf.Sin(a)*.22f,.33f,Mathf.Cos(a)*.22f),V(.65f,.7f,.65f),Color.Lerp(Leaf,Moss,.28f),seed+arm+3,8,3);
     if(arm<3){var foot=V(Mathf.Cos(a)*.76f,.13f,Mathf.Sin(a)*.76f);b.Vine(tip,foot,.08f,.025f,species==1?Bark:Leaf,7);if(species==4)b.Gem(foot+V(0,.35f,0),V(.12f,.22f,.12f),Glow(Light),5);}
    }
    b.Crown(top,V(.85f,.95f,.85f),Leaf,seed+9,9,3);
   }
   // Props have circular collision footprints; keep foliage and roots inside those footprints.
   for(int i=0;i<b.v.Count;i++){var v=b.v[i];float r=new Vector2(v.x,v.z).magnitude;if(r>.98f){v.x*=.98f/r;v.z*=.98f/r;}b.v[i]=v;}
  }
  static void NaturalBoulder(Geo b,int type,int seed)
  {
   if(type==0){b.Crag(Vector3.zero,V(1.65f,1.25f,1.65f),Rock,seed+1);b.Crag(V(-.5f,0,.35f),V(.7f,.58f,.8f),Rock,seed+5);}
   else if(type==1){b.Crag(V(-.18f,0,0),V(1.25f,1.8f,1.35f),Rock,seed+3);b.Crag(V(.49f,0,.10f),V(.72f,1.1f,1.08f),Color.Lerp(Rock,Bone,.08f),seed+9);}
   else{b.Crag(Vector3.zero,V(1.85f,.8f,1.65f),Rock,seed+7);b.Crag(V(-.12f,.35f,0),V(1.5f,.62f,1.25f),Color.Lerp(Rock,Bone,.09f),seed+2);}
   for(int i=0;i<3;i++)b.Crown(V(-.4f+i*.27f,.45f+i*.14f,.55f),V(.4f,.13f,.3f),Moss,seed+i,6,2);
   for(int i=0;i<b.v.Count;i++){var v=b.v[i];float r=new Vector2(v.x,v.z).magnitude;if(r>.99f){v.x*=.99f/r;v.z*=.99f/r;}b.v[i]=v;}
  }
  static void HabitableTent(Geo b,int n)
  {
   var cloth=n==0?new Color(.67f,.49f,.25f):n==1?Leaf:new Color(.48f,.35f,.31f);
   const float w=.72f,d=.68f,eave=1.70f,ridge=2.25f;
   // Upright walls give useful headroom across the entrance, unlike a miniature A-frame.
   for(int side=-1;side<=1;side+=2){float x=side*w;
    b.Quad(V(x,0,-d),V(x,eave,-d),V(x,eave,d),V(x,0,d),cloth,true);
    b.Quad(V(x,eave,-d),V(0,ridge,-d),V(0,ridge-.10f,d),V(x,eave,d),Color.Lerp(cloth,Bone,.1f),true);
    b.Rod(V(x,0,d),V(x,eave+.04f,d),.035f,Bark,6);b.Rod(V(x,0,-d),V(x,eave+.04f,-d),.035f,Bark,6);
    b.Quad(V(side*.56f,0,d+.005f),V(side*.56f,1.65f,d+.005f),V(x,eave,d+.005f),V(x,0,d+.005f),cloth,true);
    b.Rod(V(side*.62f,.35f,d+.025f),V(side*.62f,1.65f,d+.025f),.06f,Color.Lerp(cloth,Bone,.2f),7,.08f);
    for(int i=0;i<5;i++)b.Rod(V(x,eave-i*.33f,d+.015f),V(x-side*.07f,eave-i*.33f-.08f,d+.015f),.009f,Bone,4);
   }
   b.Quad(V(-w,0,-d),V(w,0,-d),V(w,eave,-d),V(-w,eave,-d),cloth,true);
   b.Tri(V(-w,eave,-d),V(w,eave,-d),V(0,ridge,-d),cloth);
   b.Tri(V(-w,eave,d),V(w,eave,d),V(0,ridge-.1f,d),cloth);b.Tri(V(w,eave,d),V(-w,eave,d),V(0,ridge-.1f,d),cloth);
   b.Rod(V(-w,eave,d+.02f),V(w,eave,d+.02f),.035f,Bark,6);
   b.Rod(V(0,ridge,-d),V(0,ridge-.10f,d),.025f,Bone,5);
   b.Box(V(0,.015f,0),V(1.4f,.03f,1.3f),Bark);
  }
 }
}
