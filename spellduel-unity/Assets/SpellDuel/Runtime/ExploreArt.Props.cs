using UnityEngine;
namespace SpellDuel
{
 public static partial class ExploreArt
 {
  public static GameObject CreateProp(ExProp p)
  {
   if(p==null)return null;var root=Root(p.kind);int n=((p.variant%3)+3)%3;
   if(p.kind=="tree")n=((p.variant%5)+5)%5;
   Part(root.transform,"prop/"+p.kind+"/"+n,b=>{Prop(b,p.kind,n);PropVariant(b,p.kind,n);});float radius=Mathf.Max(.05f,p.radius);float height=p.kind=="tree"?.85f:p.kind=="mushroom"?1.3f:p.kind=="totem"||p.kind=="pillar"||p.kind=="statue"?.65f:radius;root.transform.localScale=V(radius,Mathf.Max(radius,height),radius);
   if(p.kind=="campfire")Part(root.transform,"Fire",b=>{b.Gem(V(0,.7f,0),V(.65f,1.1f,.65f),Glow(new Color(1,.53f,.12f)),7);b.Gem(V(0,.52f,.1f),V(.35f,.6f,.35f),Glow(Bone));});
   return root;
  }
  static void PropVariant(Geo b,string kind,int n)
  {
   if(kind=="crate"&&n>0)for(int side=-1;side<=1;side+=2)b.Box(V(side*.45f,.66f,.72f),V(n==1?.07f:.13f,1.35f,.035f),n==1?Rock:Bone);
   if(kind=="totem")for(int i=0;i<=n;i++)b.Frond(V(-.7f+i*.5f,3.6f,.1f),V(-.5f+i*.5f,2.9f,.2f),.13f,n==1?Jade:Bone);
   if(kind=="statue"&&n>0)for(int side=-1;side<=1;side+=2)b.Gem(V(side*.3f,3.14f,0),V(.18f,.6f+n*.1f,.18f),n==1?Jade:Bone,5);
   if(kind=="altar")for(int i=0;i<3+n;i++){float a=i*Mathf.PI*2/(3+n);b.Gem(V(Mathf.Cos(a)*.7f,.9f,Mathf.Sin(a)*.7f),V(.12f,.25f,.12f),n==1?Bone:Jade,4);}
  }
  static void RootFoot(Geo b,Color color){for(int i=0;i<8;i++){float a=i*Mathf.PI/4;b.Rod(V(0,.65f,0),V(Mathf.Cos(a)*.91f,.06f,Mathf.Sin(a)*.91f),.17f,color,5,.10f);}}
  static void Leaves(Geo b,Vector3 at,float length,Color color,int count=8){for(int i=0;i<count;i++){float a=i*2*Mathf.PI/count;b.Frond(at,at+V(Mathf.Cos(a)*length,-length*.16f,Mathf.Sin(a)*length),length*.22f,color);}}
  static void Mushroom(Geo b,Vector3 at,float scale,int n)
  {
   b.Rod(at,at+V(.10f,1.7f*scale,0),.32f*scale,Bone,7,.20f*scale);
   var top=at+V(.10f,1.65f*scale,0);var col=n==2?new Color(.42f,.39f,.8f):n%2==0?new Color(.69f,.27f,.49f):Jade;
   b.Gem(top,V(1.95f,.85f,1.95f)*scale,col,10);b.Ring(top-V(0,.04f,0),.82f*scale,.10f*scale,Glow(Light),10);
   for(int i=0;i<5;i++){float a=i*1.256f;b.Gem(top+V(Mathf.Cos(a)*.45f,.20f,Mathf.Sin(a)*.45f)*scale,V(.18f,.07f,.18f)*scale,Glow(Bone),5);}
  }
  static void Skull(Geo b,Vector3 p,float scale)
  {b.Gem(p,V(.6f,.7f,.4f)*scale,Bone);for(int s=-1;s<=1;s+=2)b.Gem(p+V(s*.14f,.04f,.18f)*scale,V(.14f,.15f,.06f)*scale,Dark,4);b.Box(p+V(0,-.30f,0)*scale,V(.32f,.17f,.26f)*scale,Bone);}
  static void Prop(Geo b,string kind,int n)
  {
   if(kind=="tree")
   {
    RootFoot(b,Bark);float h=n==2?2.8f:n==3?2.6f:4.2f;b.Rod(V(0,.15f,0),V(.18f,h,0),.55f,Bark,8,.25f);
    if(n==0){for(int i=0;i<4;i++)b.Ring(V(.04f,i*.7f+.4f,0),.48f-i*.04f,.04f,Bone,8);Leaves(b,V(.18f,h,0),1.05f,Moss,9);}
    else if(n==1){for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;b.Gem(V(Mathf.Cos(a)*.35f,h-.25f,Mathf.Sin(a)*.35f),V(1.25f,1.25f,1.25f),Leaf,7);b.Vine(V(Mathf.Cos(a)*.75f,h-.3f,Mathf.Sin(a)*.75f),V(Mathf.Cos(a)*.87f,.1f,Mathf.Sin(a)*.87f),.12f,.06f,Bark,6);}}
    else if(n==2){for(int j=0;j<3;j++)Leaves(b,V(0,1.3f+j*.65f,0),1-j*.12f,j%2==0?Moss:Leaf,7);}
    else if(n==3){b.Gem(V(0,2.8f,0),V(1.75f,1.1f,1.75f),new Color(.7f,.25f,.38f),9);b.Box(V(0,2.9f,.83f),V(.9f,.13f,.05f),Dark);for(int i=-2;i<=2;i++)b.Gem(V(i*.18f,2.85f,.85f),V(.11f,.25f,.10f),Bone,4);Leaves(b,V(0,1,0),1,Leaf);}
    else {Leaves(b,V(0,h,0),1,Moss);for(int i=0;i<4;i++){float a=i*Mathf.PI/2;b.Vine(V(Mathf.Cos(a)*.6f,h,Mathf.Sin(a)*.6f),V(Mathf.Cos(a)*.8f,.6f,Mathf.Sin(a)*.8f),.12f,.065f,Leaf,6);b.Gem(V(Mathf.Cos(a)*.8f,.6f,Mathf.Sin(a)*.8f),V(.2f,.4f,.2f),Glow(Light),5);}}
   }
   else if(kind=="stump")
   {RootFoot(b,Bark);b.Rod(V(0,.1f,0),V(.08f,1.2f+n*.12f,0),.75f,Bark,9,.62f);for(int i=0;i<3;i++)b.Ring(V(.08f,1.21f+n*.12f,0),.20f+i*.18f,.03f,Bone,12);Mushroom(b,V(.65f,.25f,0),.2f,n);}
   else if(kind=="boulder")
   {b.Crag(Vector3.zero,V(1.96f,1.4f+n*.12f,1.96f),Rock,n);b.Frond(V(-.5f,.6f,.75f),V(.3f,1.2f,.75f),.27f,Moss);}
   else if(kind=="mushroom")
   {b.Rod(V(0,.01f,0),V(0,.24f,0),.98f,Moss,10,.85f);Mushroom(b,Vector3.zero,1,n);Mushroom(b,V(-.55f,0,.35f),.35f,1-n);}
   else if(kind=="tent")
   {
    var col=n==0?new Color(.67f,.49f,.25f):n==1?Leaf:new Color(.48f,.35f,.31f);
    b.Quad(V(-.68f,0,-.68f),V(0,1.65f,-.68f),V(0,1.65f,.68f),V(-.68f,0,.68f),col,true);b.Quad(V(0,1.65f,-.68f),V(.68f,0,-.68f),V(.68f,0,.68f),V(0,1.65f,.68f),col,true);
    b.Tri(V(-.68f,0,-.68f),V(.68f,0,-.68f),V(0,1.65f,-.68f),col);b.Tri(V(-.68f,0,.68f),V(0,1.65f,.68f),V(0,0,.38f),col);b.Tri(V(.68f,0,.68f),V(0,0,.38f),V(0,1.65f,.68f),col);
    b.Box(V(0,.025f,0),V(1.4f,.05f,1.4f),Bark);b.Rod(V(0,0,.70f),V(0,1.8f,.70f),.035f,Bark);for(int s=-1;s<=1;s+=2)b.Rod(V(0,1.6f,s*.65f),V(s*.70f,.03f,s*.70f),.018f,Bone,4);
   }
   else if(kind=="crate")
   {b.Box(V(0,.65f,0),V(1.4f,1.3f,1.4f),Bark);for(int j=0;j<4;j++){float y=.14f+j*.32f;b.Box(V(0,y,.707f),V(1.4f,.025f,.015f),Dark);b.Box(V(.707f,y,0),V(.015f,.025f,1.4f),Dark);}for(int s=-1;s<=1;s+=2)b.Rod(V(s*.60f,.10f,.73f),V(-s*.60f,1.2f,.73f),.06f,Bone,4);}
   else if(kind=="campfire")
   {for(int i=0;i<9;i++){float a=i*2*Mathf.PI/9;b.Gem(V(Mathf.Cos(a)*.78f,.17f,Mathf.Sin(a)*.78f),V(.4f,.35f,.4f),Rock);}for(int i=-1;i<=1;i++)b.Rod(V(-.55f,.18f,i*.25f),V(.55f,.28f,-i*.25f),.13f,Bark,6);}
   else if(kind=="totem")
   {b.Rod(V(0,0,0),V(0,4.2f,0),.85f,Bark,7,.55f);for(int j=0;j<3;j++){Skull(b,V(0,1+j, .57f),.85f);b.Ring(V(0,.7f+j,0),.81f-j*.065f,.07f,Jade);}b.Rod(V(-.95f,3.5f,0),V(.95f,3.5f,0),.08f,Bark);Leaves(b,V(0,4.2f,0),.85f,Moss,5);}
   else if(kind=="pillar")
   {b.Rod(Vector3.zero,V(0,.3f,0),.98f,Rock,8);b.Rod(V(0,.3f,0),V(0,4.2f-n*.45f,0),.65f,Jade*.8f,8);b.Rod(V(0,4.2f-n*.45f,0),V(0,4.5f-n*.45f,0),.87f,Rock,8);b.Vine(V(-.6f,3.8f,.25f),V(.6f,.3f,.25f),.2f,.07f,Leaf,7);}
   else if(kind=="statue")
   {b.Rod(Vector3.zero,V(0,.35f,0),.98f,Rock,8);b.Rod(V(0,.35f,0),V(0,2.2f,0),.70f,Jade*.8f,7,.38f);b.Gem(V(0,2.75f,0),V(.9f,1.1f,.8f),Rock,8);b.Rod(V(-.4f,2.1f,0),V(-.7f,1.2f,.4f),.20f,Rock);b.Rod(V(.4f,2.1f,0),V(.7f,1.2f,.4f),.20f,Rock);Leaves(b,V(0,3.2f,0),.6f,Moss,5);}
   else if(kind=="altar")
   {b.Rod(Vector3.zero,V(0,.35f,0),.98f,Rock,10);b.Rod(V(0,.35f,0),V(0,1.1f,0),.65f,Jade*.8f,8);b.Rod(V(0,1.1f,0),V(0,1.3f,0),.95f,Rock,10);b.Ring(V(0,1.31f,0),.72f,.045f,Jade,16);for(int i=0;i<4;i++){float a=i*Mathf.PI/2;b.Gem(V(Mathf.Cos(a)*.65f,1.42f,Mathf.Sin(a)*.65f),V(.15f,.22f,.15f),Jade);}}
  }
 }
}
