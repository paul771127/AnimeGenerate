using UnityEngine;
namespace SpellDuel
{
 public partial class MinionRig
 {
  bool petEating;float eatBlend,eatClock;Transform eatHead,eatSpine;Quaternion eatHeadBase,eatSpineBase;bool eatApplied;
  internal int PetArtKind => variantId;
  internal Transform PetArtAnchor => head?head:spine?spine:body;
  internal bool PetArtDead => dead;
  public void SetEating(bool eating){petEating=eating;}
  void RestoreEatingPose(){if(!eatApplied)return;if(eatHead)eatHead.localRotation=eatHeadBase;if(eatSpine)eatSpine.localRotation=eatSpineBase;eatApplied=false;}
  void Animate(float dt){RestoreEatingPose();AnimateCore(dt);EatingPose(dt);}
  void EatingPose(float dt)
  {
   eatClock+=dt;eatBlend=Mathf.MoveTowards(eatBlend,petEating&&!dead?1:0,dt*6);if(eatBlend<=0||dead||phase!=PhaseNone)return;
   eatHead=head;eatSpine=spine&&spine!=head?spine:null;
   if(eatHead){eatHeadBase=eatHead.localRotation;eatHead.localRotation=eatHeadBase*Quaternion.Euler((12+Mathf.Sin(eatClock*11)*3)*eatBlend,Mathf.Sin(eatClock*4)*3*eatBlend,0);}
   if(eatSpine){eatSpineBase=eatSpine.localRotation;eatSpine.localRotation=eatSpineBase*Quaternion.Euler((variantId==8?8:5)*eatBlend,0,Mathf.Sin(eatClock*6)*1.5f*eatBlend);}
   eatApplied=true;
  }
 }
}
