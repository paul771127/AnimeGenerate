using UnityEngine;
namespace SpellDuel
{
 public partial class MinionRig
 {
  RigStaggerArt staggerArt;
  public void PlayStagger(float seconds)
  {
   if(dead||!body)return;phase=PhaseNone;atkT=0;PlayHit();
   if(staggerArt==null)
   {
    var style=RigStaggerArt.Style.Humanoid;
    if(variantId==2||variantId==4||variantId==21)style=RigStaggerArt.Style.Heavy;
    else if(variantId==8||variantId==19||variantId==22)style=RigStaggerArt.Style.Quadruped;
    else if(variantId>=7&&variantId<=12&&variantId!=9)style=RigStaggerArt.Style.Plant;
    else if(variantId==9||variantId==6||variantId==1)style=RigStaggerArt.Style.Caster;
    else if(variantId==13||variantId==15||variantId==20)style=RigStaggerArt.Style.Fish;
    else if(variantId==14)style=RigStaggerArt.Style.Bird;
    else if(variantId==16)style=RigStaggerArt.Style.Crab;
    else if(variantId==17)style=RigStaggerArt.Style.Jelly;
    else if(variantId==18||variantId==3)style=RigStaggerArt.Style.Puffer;
    else if(variantId>=23&&variantId<48){int role=(variantId-23)%5;style=role==0?RigStaggerArt.Style.Heavy:role==1?RigStaggerArt.Style.Archer:role==4?RigStaggerArt.Style.Assassin:RigStaggerArt.Style.Caster;}
    bool elite=variantId>=23&&variantId<48,area=variantId>=19&&variantId<23;
    staggerArt=new RigStaggerArt(style,body,head,elite?eliteArmL:area?areaFrontL:shL?shL:finL,elite?eliteArmR:area?areaFrontR:shR?shR:finR,elite?eliteLegL:hipL,elite?eliteLegR:hipR,area?areaTail:tailT,elite?eliteTool:area?areaWeapon:neck);
   }
   staggerArt.Play(seconds);
  }
  void ResetStaggerArt(){if(staggerArt!=null){staggerArt.Dispose();staggerArt=null;}}
 }
 public sealed partial class VariantRig
 {
  RigStaggerArt staggerArt;
  public void PlayStagger(float seconds)
  {
   if(dead||!torso)return;winding=channel=false;channelBlend=0;attackAge=20;PlayHit();
   if(staggerArt==null){var style=variantId==124?RigStaggerArt.Style.Jelly:caster?RigStaggerArt.Style.Caster:archer?RigStaggerArt.Style.Archer:assassin?RigStaggerArt.Style.Assassin:RigStaggerArt.Style.Heavy;staggerArt=new RigStaggerArt(style,torso,head,upperL,upperR,hipL,hipR,tail,accessory);}
   staggerArt.Play(seconds);
  }
 }
 public sealed partial class SkyEnemyRig
 {
  RigStaggerArt staggerArt;
  public void PlayStagger(float seconds)
  {
   // The captain is a boss; the caller also excludes bosses from stagger triggers.
   if(dead||Kind==4)return;winding=false;strike=10;PlayHit();
   if(staggerArt==null)staggerArt=new RigStaggerArt(Kind==3?RigStaggerArt.Style.Clockwork:Kind==2?RigStaggerArt.Style.Archer:Kind==1?RigStaggerArt.Style.Heavy:RigStaggerArt.Style.Assassin,body,head,armL,armR,legL,legR,null,cloth);
   staggerArt.Play(seconds);
  }
 }
}
