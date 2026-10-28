using UnityEngine;
namespace SpellDuel
{
 public static partial class SpellFx
 {
  public static void SwordShieldGain(Transform owner,float fill)
  {if(!owner||!owner.gameObject.activeInHierarchy)return;var v=owner.GetComponent<SwordShieldView>();if(!v)v=owner.gameObject.AddComponent<SwordShieldView>();v.Gain(fill);}
  public static void SwordShieldAura(Transform owner,float fill)
  {if(!owner)return;var v=owner.GetComponent<SwordShieldView>();if(!v && fill>0 && owner.gameObject.activeInHierarchy)v=owner.gameObject.AddComponent<SwordShieldView>();if(v)v.SetFill(fill);}
 }
 // Retained on the owner, so zero / gain / refresh never creates another material or effect.
 public sealed class SwordShieldView:MonoBehaviour
 {
  Transform root;LineRenderer[] blades;float fill,age,pulse,lastGain=-1;Camera edgeCamera;bool ownsEdge;
  public int GainBursts{get;private set;}
  void Build()
  {
   root=new GameObject("Blue sword qi shield").transform;root.SetParent(transform,false);blades=new LineRenderer[8];
   for(int i=0;i<8;i++)blades[i]=SpellFx.Line(root,"Sword qi shard "+i,new[]{new Vector3(0,-.13f,0),new Vector3(-.024f,.06f,0),new Vector3(0,.23f,0),new Vector3(.024f,.06f,0),new Vector3(0,-.13f,0)},false,.012f,new Color(.40f,.82f,1,.4f),true,false);
  }
  public void SetFill(float value)
  {
   fill=float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);
   if(fill>0&&!root)Build();if(root)root.gameObject.SetActive(fill>0&&gameObject.activeInHierarchy);
   if(fill<=0){pulse=0;ClearEdge();}else Tick(0);
  }
  public void Gain(float value)
  {SetFill(value);if(fill<=0 || age-lastGain<.3f)return;lastGain=age;pulse=1;GainBursts++;Tick(0);}
  public void Tick(float dt)
  {
   age+=Mathf.Max(0,dt);pulse=Mathf.Max(0,pulse-dt*2.4f);if(!root||fill<=0)return;
   for(int i=0;i<blades.Length;i++)
   {
    float a=age*.65f+i*Mathf.PI*.25f,r=.36f+pulse*.38f;
    var blade=blades[i];blade.transform.localPosition=new Vector3(Mathf.Cos(a)*r,.35f+(i%3)*.25f+Mathf.Sin(age*1.3f+i)*.045f,Mathf.Sin(a)*r);
    blade.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,18*Mathf.Sin(a));
    var c=Color.Lerp(new Color(.20f,.53f,1),new Color(.78f,.96f,1),fill);c.a=(.12f+.30f*fill)+pulse*.24f;
    blade.startColor=blade.endColor=c;blade.widthMultiplier=.008f+.008f*fill+pulse*.006f;
   }
   var cam=Camera.main;bool local=cam&&SelfFxEdge.NearOwner(cam,transform.position);
   if(ownsEdge&&(!local||cam!=edgeCamera))ClearEdge();
   if(local){edgeCamera=cam;ownsEdge=true;SelfFxEdge.Shield(cam,fill);}
  }
  void ClearEdge(){if(ownsEdge&&edgeCamera)SelfFxEdge.Shield(edgeCamera,0);ownsEdge=false;edgeCamera=null;}
  void Update(){Tick(Time.deltaTime);}
  void OnDisable(){fill=0;pulse=0;if(root)root.gameObject.SetActive(false);ClearEdge();}
  void OnDestroy(){ClearEdge();if(root)VariantGeometry.Dispose(root.gameObject);}
 }
}
