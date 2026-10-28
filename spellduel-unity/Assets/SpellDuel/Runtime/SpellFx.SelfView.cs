using UnityEngine;
namespace SpellDuel
{
 // World-space presentation only: no damage query, target selection or duration ownership.
 public sealed class HolySelfBurst:MonoBehaviour
 {
  LineRenderer[] rays;LineRenderer wave,echo;float age,radius;bool heaven;
  public void Build(bool strong)
  {
   heaven=strong;radius=strong?3:1.5f;
   var floor=new GameObject("Floor expansion").transform;floor.SetParent(transform,false);floor.localRotation=Quaternion.Euler(90,0,0);floor.localPosition=Vector3.up*.035f;
   wave=SpellFx.Line(floor,"Range wave",SpellFx.Circle(64,1),true,.045f,SpellFx.HolyGold,false,true);
   echo=SpellFx.Line(floor,"Trailing wave",SpellFx.Circle(64,1),true,.018f,new Color(1,1,.75f,.7f),false,true);
   rays=new LineRenderer[strong?12:8];
   for(int i=0;i<rays.Length;i++)
   {
    var p=SpellFx.FloorPoint(i*Mathf.PI*2/rays.Length,radius*.94f);
    rays[i]=SpellFx.Line(transform,"Perimeter light "+i,new[]{p+Vector3.up*(strong?2.8f:1.8f),p},false,strong?.045f:.03f,new Color(1,.91f,.55f,.75f),true,false);
   }
   Tick(0);
  }
  public void Tick(float dt)
  {
   age+=Mathf.Max(0,dt);if(age>=.9f){gameObject.SetActive(false);VariantGeometry.Dispose(gameObject);return;}
   float k=Mathf.Clamp01(age/.65f),r=Mathf.Lerp(.08f,radius,1-Mathf.Pow(1-k,3));
   wave.transform.localScale=Vector3.one*r;echo.transform.localScale=Vector3.one*Mathf.Max(.02f,r-.12f);
   var gold=SpellFx.HolyGold;gold.a*=1-age/.9f;wave.startColor=wave.endColor=gold;echo.startColor=echo.endColor=gold;
   for(int i=0;i<rays.Length;i++){var c=new Color(1,.93f,.66f,.78f*Mathf.Clamp01(1-(age-i*.012f)/.65f));rays[i].startColor=rays[i].endColor=c;}
  }
  void Update(){Tick(Time.deltaTime);}
 }
 // Four corner accents, leaving the central 76% of the view unobstructed.
 // A component per camera is reused for rapid casts; it owns only its own lines.
 public sealed class SelfFxEdge:MonoBehaviour
 {
  Camera view;LineRenderer[] corners;Color tint;float pulse,level;
  public static bool NearOwner(Camera cam,Vector3 at)
  {var d=cam.transform.position-at;return d.x*d.x+d.z*d.z<.75f*.75f && d.y>-.1f && d.y<2.6f;}
  public static void Pulse(Camera cam,Color color,float strength)
  {var fx=Get(cam);fx.tint=color;fx.pulse=Mathf.Max(fx.pulse,strength);fx.Draw();}
  static SelfFxEdge Get(Camera cam)
  {
   var fx=cam.GetComponent<SelfFxEdge>();if(fx)return fx;
   fx=cam.gameObject.AddComponent<SelfFxEdge>();fx.view=cam;fx.corners=new LineRenderer[4];
   for(int i=0;i<4;i++){fx.corners[i]=SpellFx.Line(cam.transform,"Self spell corner "+i,new Vector3[3],false,.002f,Color.clear,false,false);fx.corners[i].sortingOrder=80;}
   return fx;
  }
  public static void Shield(Camera cam,float fill)
  {var fx=Get(cam);fx.level=Mathf.Clamp01(fill);fx.Draw();}
  void Draw()
  {
   float z=view.nearClipPlane+.035f,y=Mathf.Tan(view.fieldOfView*.5f*Mathf.Deg2Rad)*z,x=y*view.aspect;
   var c=pulse>0?tint:new Color(.45f,.83f,1,.28f);c.a*=Mathf.Clamp01(pulse+level);
   for(int i=0;i<4;i++)
   {float sx=(i&1)==0?-1:1,sy=i<2?-1:1;var line=corners[i];line.SetPosition(0,new Vector3(sx*x*.76f,sy*y*.94f,z));line.SetPosition(1,new Vector3(sx*x*.94f,sy*y*.94f,z));line.SetPosition(2,new Vector3(sx*x*.94f,sy*y*.76f,z));line.widthMultiplier=y*.017f;line.startColor=line.endColor=c;line.enabled=c.a>.001f;}
  }
  void LateUpdate(){pulse=Mathf.Max(0,pulse-Time.deltaTime*1.3f);Draw();}
  void OnDestroy(){if(corners!=null)foreach(var l in corners)if(l)VariantGeometry.Dispose(l.gameObject);}
 }
}
