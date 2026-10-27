using UnityEngine;
namespace SpellDuel
{
 public sealed class SkyShotView : MonoBehaviour
 {
  public static SkyShotView Create(bool low)
  {
   var v=new GameObject(low?"Low orange sky shot":"High blue sky shot").AddComponent<SkyShotView>();var c=low?new Color(1,.48f,.12f):new Color(.23f,.62f,1);
   SkyArt.Oval(v.transform,"Luminous cannon core",Vector3.zero,low?new Vector3(.35f,.18f,.38f):new Vector3(.2f,.32f,.48f),SkyArt.Cream,!low);
   for(int i=0;i<2;i++){var l=SkyArt.Line(v.transform,"Height coded tail",3,.065f,c);l.SetPosition(0,new Vector3((i*2-1)*.07f,0,0));l.SetPosition(1,new Vector3((i*2-1)*.11f,0,-.4f));l.SetPosition(2,new Vector3((i*2-1)*.05f,0,-.9f));l.endColor=new Color(c.r,c.g,c.b,0);l.endWidth=.005f;}
   return v;
  }
 }
}
