using UnityEngine;
namespace SpellDuel
{
 public sealed class CarpetObstacleView : MonoBehaviour
 {
  // Tick has no duration argument. Set this to the logic's actual flight duration before Tick.
  public float TravelSeconds { get; set; } = 6;
  public float WarningLeadSeconds { get; set; } = 1.8f;
  Transform visual,left,right,lintel; LineRenderer[] marks; int kind; bool released,passing; float passedAt; Vector3 direction,front; float width,height,gap,gapW; int side;
  public static CarpetObstacleView Create(int kind,int station)
  {
   var v=new GameObject("Sky obstacle "+kind).AddComponent<CarpetObstacleView>();v.kind=Mathf.Clamp(kind,0,3);
   v.visual=SkyArt.Node(v.transform,"Approaching obstacle",Vector3.zero);
   Color c=station==0?SkyArt.Cream:station==1?new Color(.43f,.72f,.54f):station==2?new Color(.83f,.46f,.28f):station==3?new Color(.43f,.46f,.75f):SkyArt.Gold;
   v.left=SkyArt.Oval(v.visual,"Left span",Vector3.zero,Vector3.one,c);v.right=SkyArt.Oval(v.visual,"Right span",Vector3.zero,Vector3.one,c);v.lintel=SkyArt.Oval(v.visual,"Arch lintel",Vector3.zero,Vector3.one,c);
   for(int i=0;i<7;i++)
   {float x=(i-3)*.13f;var rib=SkyArt.Oval(v.left,station==3?"Lightning conductor":"Structural rib",new Vector3(x,0,.4f),new Vector3(.045f,1.1f,.2f),station==2?SkyArt.Cream:SkyArt.Gold);}
   v.marks=new LineRenderer[12];for(int i=0;i<v.marks.Length;i++)v.marks[i]=SkyArt.Line(v.transform,"Warning "+i,2,.045f,i==11?new Color(.3f,1,.58f,.9f):new Color(1,.24f,.18f,.65f));
   return v;
  }
  public void Tick(float progress,Vector3 frontCenter,Vector3 forward,float carpetWidth,float chestHeight,int side,float gapCenter,float gapWidth)
  {
   if(released)return;passing=false;direction=forward;direction.y=0;if(direction.sqrMagnitude<.001f)direction=Vector3.forward;direction.Normalize();front=frontCenter;width=Mathf.Max(.5f,carpetWidth);height=Mathf.Max(.4f,chestHeight);this.side=side<0?-1:1;
   gapW=Mathf.Clamp(gapWidth,.3f,width);gap=Mathf.Clamp(gapCenter,-width*.5f+gapW*.5f,width*.5f-gapW*.5f);
   transform.SetPositionAndRotation(front,Quaternion.LookRotation(direction));visual.localPosition=Vector3.forward*(1-Mathf.Clamp01(progress))*30;
   left.gameObject.SetActive(true);right.gameObject.SetActive(kind==3);lintel.gameObject.SetActive(kind==3);
   if(kind<2){left.localPosition=new Vector3(0,kind==0?.15f:height*.85f,0);left.localScale=new Vector3(width+1,.22f,.3f);}
   else if(kind==2){left.localPosition=new Vector3(this.side*width*.25f,height*.6f,0);left.localScale=new Vector3(width*.5f,height*1.5f,.8f);}
   else
   {float lo=gap-gapW*.5f,hi=gap+gapW*.5f,wl=lo+width*.5f,wr=width*.5f-hi;
    left.localPosition=new Vector3((-width*.5f+lo)*.5f,height*.6f,0);left.localScale=new Vector3(wl,height*1.8f,.4f);left.gameObject.SetActive(wl>.01f);
    right.localPosition=new Vector3((width*.5f+hi)*.5f,height*.6f,0);right.localScale=new Vector3(wr,height*1.8f,.4f);right.gameObject.SetActive(wr>.01f);
    lintel.localPosition=new Vector3(0,height*1.65f,0);lintel.localScale=new Vector3(width+.4f,.25f,.4f);}
   float lead=Mathf.Max(1.2f,WarningLeadSeconds);bool warning=(1-Mathf.Clamp01(progress))*Mathf.Max(.01f,TravelSeconds)<=lead;
   foreach(var m in marks)m.gameObject.SetActive(false);
   if(!warning)return;
   if(kind<2)
   {Color c=kind==0?new Color(1,.5f,.12f,.95f):new Color(.25f,.65f,1,.95f);float y=kind==0?.15f:height*.85f;Segment(0,new Vector3(-width*.5f,y,0),new Vector3(width*.5f,y,0),c);
    float d=kind==0?1:-1;for(int i=0;i<2;i++){Segment(1+i*2,new Vector3(-.17f,.07f,-.55f-i*.23f),new Vector3(0,.07f,-.55f-i*.23f+d*.16f),c);Segment(2+i*2,new Vector3(.17f,.07f,-.55f-i*.23f),new Vector3(0,.07f,-.55f-i*.23f+d*.16f),c);}}
   else
   {for(int i=0;i<10;i++){float x=Mathf.Lerp(-width*.5f,width*.5f,i/9f);if(kind==2&&x*this.side<0||kind==3&&Mathf.Abs(x-gap)<gapW*.5f)continue;Segment(i,new Vector3(x,.065f,0),new Vector3(x,.065f,-1.3f),new Color(1,.25f,.2f,.65f));}
    if(kind==3){var m=marks[11];m.gameObject.SetActive(true);m.positionCount=5;var c=new Color(.3f,1,.58f,.9f);m.startColor=m.endColor=c;Set(m,0,new Vector3(gap-gapW*.5f,.07f,0));Set(m,1,new Vector3(gap+gapW*.5f,.07f,0));Set(m,2,new Vector3(gap+gapW*.5f,.07f,-1.3f));Set(m,3,new Vector3(gap-gapW*.5f,.07f,-1.3f));Set(m,4,new Vector3(gap-gapW*.5f,.07f,0));}}
  }
  void Set(LineRenderer l,int i,Vector3 p){var world=transform.TransformPoint(p);if(CarpetScene.Active)world=CarpetScene.Active.SurfacePoint(world);l.SetPosition(i,transform.InverseTransformPoint(world));}
  void Segment(int i,Vector3 a,Vector3 b,Color c){var l=marks[i];l.gameObject.SetActive(true);l.positionCount=2;l.startColor=l.endColor=c;Set(l,0,a);Set(l,1,b);}
  public void Hit(){if(released)return;SpellFx.Impact("golem",SkyArt.Gold,front+Vector3.up*.3f,.45f);Passed();}
  public void Passed(){if(released||passing)return;passing=true;passedAt=Time.time;foreach(var m in marks)m.gameObject.SetActive(false);}
  public void Release(){released=true;gameObject.SetActive(false);}
  void OnEnable(){released=false;passing=false;}
  void Update(){if(passing&&!released){visual.localPosition-=Vector3.forward*12*Time.deltaTime;if(Time.time-passedAt>1.1f)Release();}}
 }
}
