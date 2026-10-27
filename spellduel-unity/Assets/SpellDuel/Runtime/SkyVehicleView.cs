using UnityEngine;
namespace SpellDuel
{
 public sealed class SkyVehicleView : MonoBehaviour
 {
  int kind;Transform wingL,wingR;LineRenderer landing;
  public static SkyVehicleView Create(int kind)
  {var v=new GameObject("Sky vehicle "+kind).AddComponent<SkyVehicleView>();v.kind=Mathf.Clamp(kind,0,4);v.Build();return v;}
  void Build()
  {
   if(kind==0||kind==2)
   {var s=SkyArt.Sail(transform,kind==0?"Kite upper canvas":"Glider canvas",Vector3.zero,new Vector3(kind==0?1.8f:2.8f,1.4f,1),kind==0?new Color(.9f,.26f,.28f):new Color(1,.69f,.25f));s.localRotation=Quaternion.Euler(70,0,0);
    SkyArt.Rod(transform,"Spar",new Vector3(-1,0,0),new Vector3(1,0,0),.045f,SkyArt.Gold);SkyArt.Rod(transform,"Pilot control bar",new Vector3(-.4f,-.5f,0),new Vector3(.4f,-.5f,0),.04f,SkyArt.Ink);
    for(int side=-1;side<=1;side+=2)SkyArt.Rod(transform,"Tension cord",new Vector3(side*.9f,0,0),new Vector3(side*.4f,-.5f,0),.015f,SkyArt.Cream);
    if(kind==0)for(int i=0;i<2;i++)SkyArt.Sail(transform,"Long kite tail",new Vector3((i*2-1)*.35f,0,-.7f),new Vector3(.17f,1.2f,1),SkyArt.Cream).localRotation=Quaternion.Euler(110,0,0);
   }
   else if(kind==1)
   {SkyArt.Oval(transform,"Eagle torso",Vector3.zero,new Vector3(.55f,.6f,1.15f),new Color(.43f,.29f,.22f));SkyArt.Oval(transform,"White eagle head",new Vector3(0,.3f,.55f),new Vector3(.4f,.4f,.4f),SkyArt.Cream);SkyArt.Oval(transform,"Golden hooked beak",new Vector3(0,.22f,.8f),new Vector3(.18f,.22f,.3f),SkyArt.Gold,true);
    for(int side=-1;side<=1;side+=2){var w=SkyArt.Node(transform,"Articulated eagle wing",new Vector3(side*.23f,.12f,0));if(side<0)wingL=w;else wingR=w;for(int j=0;j<6;j++){var f=SkyArt.Oval(w,"Flight feather",new Vector3(side*(.2f+j*.17f),0,-.1f-j*.06f),new Vector3(.32f,.09f,.9f-j*.06f),j%2==0?SkyArt.Cream:SkyArt.Gold,true);f.localRotation=Quaternion.Euler(0,side*25,0);}SkyArt.Rod(transform,"Rider stirrup",new Vector3(side*.25f,0,0),new Vector3(side*.32f,-.3f,0),.04f,SkyArt.Gold);}
    SkyArt.Oval(transform,"Saddle",new Vector3(0,.29f,0),new Vector3(.38f,.10f,.4f),new Color(.25f,.42f,.8f));
   }
   else if(kind==3){SkyArt.Ring(transform,"Retractable floating ring",.6f,SkyArt.Teal,.08f);for(int i=0;i<3;i++){var t=SkyArt.Sail(transform,"Rotor leaf",Vector3.zero,new Vector3(.3f,.8f,1),SkyArt.Cream);t.localRotation=Quaternion.Euler(90,i*120,0);}}
   else
   {SkyArt.Oval(transform,"Captain skiff hull",Vector3.zero,new Vector3(1.6f,.65f,2.1f),new Color(.57f,.28f,.73f));SkyArt.Oval(transform,"Skiff deck",new Vector3(0,.25f,0),new Vector3(1.45f,.15f,1.8f),SkyArt.Cream);for(int side=-1;side<=1;side+=2){var ring=SkyArt.Ring(transform,"Ducted propeller",.32f,SkyArt.Gold,.05f);ring.transform.localPosition=new Vector3(side*.9f,0,-.2f);ring.transform.localRotation=Quaternion.Euler(90,0,0);SkyArt.Rod(transform,"Fan blade",new Vector3(side*.9f-.27f,0,-.2f),new Vector3(side*.9f+.27f,0,-.2f),.07f,SkyArt.Cream);}SkyArt.Rod(transform,"Boarding rope",new Vector3(.6f,.2f,0),new Vector3(.6f,-1.6f,0),.025f,SkyArt.Gold);}
   landing=SkyArt.Ring(null,"Sky landing warning",.35f,new Color(1,.65f,.25f,.8f));landing.useWorldSpace=true;landing.gameObject.SetActive(false);
  }
  public void Tick(float progress,Vector3 from,Vector3 landPoint)
  {
   float t=Mathf.Clamp01(progress);Vector3 side=(from-landPoint);side.y=0;if(side.sqrMagnitude<.1f)side=Vector3.right;side=side.normalized;
   var parallel=landPoint+side*1.3f+Vector3.up*(kind==3?.3f:1.6f);
   transform.position=t<.35f?Vector3.Lerp(from,parallel,Mathf.SmoothStep(0,1,t/.35f)):t<.8f?parallel:Vector3.Lerp(parallel,parallel+side*9+Vector3.up*4,Mathf.SmoothStep(0,1,(t-.8f)/.2f));
   transform.rotation=Quaternion.LookRotation(t<.8f?(landPoint-from).normalized:side);
   if(wingL)wingL.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*35)*22);if(wingR)wingR.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(t*35)*22);
   // A four-second staging timeline gives the .55-.80 landing interval exactly one second.
   landing.gameObject.SetActive(t>=.55f&&t<.8f);for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;var p=landPoint+new Vector3(Mathf.Cos(a)*.35f,.055f,Mathf.Sin(a)*.35f);landing.SetPosition(i,CarpetScene.Active?CarpetScene.Active.SurfacePoint(p):p);}
  }
  void OnDisable(){if(landing)landing.gameObject.SetActive(false);}
  void OnDestroy(){if(landing)SkyArt.Dispose(landing.gameObject);}
 }
}
