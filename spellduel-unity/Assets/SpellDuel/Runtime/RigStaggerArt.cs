using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
 // Additive presentation layer: restored before the rig's ordinary pose, applied afterwards.
 // Never moves the actor root, queries health or sends combat/network events.
 internal sealed class RigStaggerArt
 {
  internal enum Style { Humanoid,Caster,Heavy,Quadruped,Plant,Fish,Bird,Crab,Jelly,Puffer,Clockwork,Assassin,Archer }
  struct Joint {public Transform t;public int role;public Vector3 p,s;public Quaternion q;}
  readonly Joint[] joints;readonly Style style;readonly Transform halo;readonly LineRenderer[] stars;float age=10,duration=.9f;bool applied;
  public bool Active=>age<duration;
  public RigStaggerArt(Style kind,Transform body,Transform head,Transform left,Transform right,Transform legL,Transform legR,Transform tail,Transform extra)
  {
   style=kind;var list=new List<Joint>();var seen=new HashSet<Transform>();var parts=new[]{body,head,left,right,legL,legR,tail,extra};
   for(int i=0;i<parts.Length;i++)if(parts[i]&&seen.Add(parts[i]))list.Add(new Joint{t=parts[i],role=i});joints=list.ToArray();
   halo=SkyArt.Node(head?head:body,"Stagger dizzy stars",new Vector3(0,head?.30f:.5f,0));stars=new LineRenderer[3];
   for(int i=0;i<3;i++){var p=new Vector3[10];for(int j=0;j<10;j++){float a=j*Mathf.PI/5;float radius=j%2==0?.045f:.019f;p[j]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);}stars[i]=SpellFx.Line(halo,"Dizzy star "+i,p,true,.009f,new Color(1,.84f,.28f,.85f),true,false);}halo.gameObject.SetActive(false);
  }
  public void Play(float seconds){Restore();duration=float.IsNaN(seconds)||float.IsInfinity(seconds)?.9f:Mathf.Max(.08f,seconds);age=0;halo.gameObject.SetActive(true);}
  public void Restore(){if(!applied)return;for(int i=0;i<joints.Length;i++){var j=joints[i];if(j.t){j.t.localPosition=j.p;j.t.localRotation=j.q;j.t.localScale=j.s;}}applied=false;}
  public void Cancel(){Restore();age=duration;if(halo)halo.gameObject.SetActive(false);}
  public void Dispose(){Cancel();if(halo)SkyArt.Dispose(halo.gameObject);}
  public void Apply(float dt,bool dead)
  {
   if(dead){Cancel();return;}if(!Active)return;age+=Mathf.Max(0,dt);float u=Mathf.Clamp01(age/duration);float k=u<.12f?Mathf.Sin(u/.12f*Mathf.PI*.5f):u<.32f?1:1-Mathf.SmoothStep(0,1,(u-.32f)/.68f);
   if(u>=1){halo.gameObject.SetActive(false);return;}float sway=Mathf.Sin(age*23)*k*(1-u);
   for(int i=0;i<joints.Length;i++)
   {
    var j=joints[i];if(!j.t)continue;j.p=j.t.localPosition;j.q=j.t.localRotation;j.s=j.t.localScale;joints[i]=j;
    Vector3 offset=Vector3.zero,turn=Vector3.zero,scale=Vector3.one;int side=j.role==2||j.role==4?-1:1;
    if(j.role==0)
    {
     float lean=style==Style.Heavy?-15:style==Style.Quadruped?-18:style==Style.Plant?-38:style==Style.Assassin?-20:-30;
     turn=new Vector3(lean*k,sway*5,(style==Style.Archer?16:6)*k);offset=new Vector3(sway*.018f,-.04f*k,-(style==Style.Heavy?.065f:.12f)*k);
     if(style==Style.Fish){turn=new Vector3(12*k,30*k,32*k);offset.y=.06f*k;}
     if(style==Style.Bird){turn=new Vector3(20*k,15*k,26*k);offset.y=-.09f*k;}
     if(style==Style.Crab){turn=new Vector3(-6*k,0,sway*6);offset.y=-.07f*k;scale=new Vector3(1,1-.1f*k,1);}
     if(style==Style.Jelly){turn=new Vector3(8*k,sway*15,14*k);scale=new Vector3(1+.18f*k,1-.35f*k,1+.18f*k);offset.y=.04f*k;}
     if(style==Style.Puffer){turn=new Vector3(sway*15,20*k,30*k);scale=Vector3.one*(1-.25f*k);}
     if(style==Style.Clockwork){turn=new Vector3(-18*k,28*k,22*k);offset.y=-.08f*k;}
    }
    else if(j.role==1)turn=new Vector3((style==Style.Plant?32:16)*k,sway*9,(style==Style.Caster?23:12)*k);
    else if(j.role==2||j.role==3)
    {
     turn=new Vector3((style==Style.Caster?45:style==Style.Crab?65:30)*k,side*12*k,side*18*k);
     if(style==Style.Bird)turn=new Vector3(24*k,side*45*k,-side*55*k);
     if(style==Style.Quadruped)turn=new Vector3(28*k,0,side*12*k);
     if(style==Style.Clockwork)turn=new Vector3((side<0?65:20)*k,0,side*30*k);
    }
    else if(j.role==4||j.role==5)turn=new Vector3((side<0?-14:12)*k,0,side*5*k);
    else if(j.role==6)turn=new Vector3(20*k,(style==Style.Fish?55:25)*k+sway*12,0);
    else turn=new Vector3(35*k,0,sway*12);
    j.t.localPosition=j.p+offset;j.t.localRotation=j.q*Quaternion.Euler(turn);j.t.localScale=Vector3.Scale(j.s,scale);
   }
   for(int i=0;i<stars.Length;i++){float a=age*7+i*Mathf.PI*2/3;stars[i].transform.localPosition=new Vector3(Mathf.Cos(a)*.17f,Mathf.Sin(a*2)*.025f,Mathf.Sin(a)*.12f);var c=new Color(1,.84f,.28f,.85f*k);stars[i].startColor=stars[i].endColor=c;}applied=true;
  }
 }
}
