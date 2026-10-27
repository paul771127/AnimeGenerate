using UnityEngine;
namespace SpellDuel
{
 // PetFx is already the gameplay enum in Pets.cs. Keep that type unchanged.
 public static class PetArtFx
 {
  public static void Capture(Vector3 pos,bool success){var v=new GameObject(success?"Pet captured":"Pet escaped").AddComponent<PetCaptureView>();v.transform.position=pos;v.Build(success);}
 }
 public sealed class PetCaptureView : MonoBehaviour
 {
  LineRenderer rope;Transform paw;ParticleSystem sparks;bool success;float age;
  public void Build(bool result)
  {
   success=result;var c=result?SkyArt.Teal:new Color(1,.65f,.3f);rope=SkyArt.Line(transform,"Contract cord",65,.035f,c);paw=SkyArt.Node(transform,"Friend paw seal",new Vector3(0,.5f,0));
   SkyArt.Oval(paw,"Paw pad",Vector3.zero,new Vector3(.18f,.15f,.045f),SkyArt.Cream);
   for(int i=0;i<4;i++)SkyArt.Oval(paw,"Toe pad",new Vector3((i-1.5f)*.07f,.12f-Mathf.Abs(i-1.5f)*.025f,0),new Vector3(.055f,.075f,.04f),SkyArt.Cream);
   sparks=SpellFx.NewPS(transform,"Local capture motes",SpellFx.BlendMat(SpellFx.TexSoft),28,false);var m=sparks.main;m.startColor=c;m.startLifetime=.6f;m.startSize=.05f;m.startSpeed=.45f;var em=sparks.emission;em.enabled=false;sparks.Emit(24);Sample(0);
  }
  public void Sample(float time)
  {age=time;float t=Mathf.Clamp01(time/.8f),r=success?Mathf.Lerp(.65f,.12f,t):Mathf.Lerp(.2f,.9f,t);for(int i=0;i<65;i++){float f=i/64f,a=f*Mathf.PI*2*(success?1:1-t*.4f);rope.SetPosition(i,new Vector3(Mathf.Cos(a)*r,.12f+Mathf.Sin(a*2)*.05f,Mathf.Sin(a)*r));}float fade=Mathf.Clamp01((1.3f-time)/.45f);var c=success?SkyArt.Teal:new Color(1,.65f,.3f);c.a=fade;rope.startColor=rope.endColor=c;paw.gameObject.SetActive(success&&time>.35f);paw.localScale=Vector3.one*Mathf.Clamp01((time-.35f)*4)*fade;paw.localPosition=Vector3.up*(.4f+t*.25f);}
  void Update(){Sample(age+Time.deltaTime);if(age>1.3f)SkyArt.Dispose(gameObject);}
 }
}
