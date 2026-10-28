using System;
using UnityEngine;
namespace SpellDuel
{
 /// <summary>Presentation-only enemy rig. +Z faces forward; origin is feet. No damage, AI or network ownership.</summary>
 public sealed partial class VariantRig
 {
  Transform enemyRing,channelHalo;LineRenderer enemyLine,channelLine;
  public float MoveSpeed {get{return moveSpeed;}set{moveSpeed=float.IsNaN(value)||float.IsInfinity(value)?0:value;}}
  public static VariantRig Create(int variantId)
  {
   if(!((variantId>=121&&variantId<=124)||(variantId>=141&&variantId<=144)||(variantId>=151&&variantId<=165)))throw new ArgumentOutOfRangeException(nameof(variantId));
   var rig=new GameObject("Variant enemy "+variantId).AddComponent<VariantRig>();rig.BuildModel(variantId);rig.BuildPresentation();rig.Sample(0);return rig;
  }
  void BuildPresentation()
  {
   enemyRing=VariantGeometry.Node(transform,"Enemy red ground seal",new Vector3(0,.018f,0));enemyRing.localRotation=Quaternion.Euler(90,0,0);
   enemyLine=SpellFx.Line(enemyRing,"Broken enemy ring",SpellFx.Circle(40,variantId==124?.25f:.36f),true,.018f,new Color(.85f,.08f,.13f,.65f),false,true);
   channelHalo=VariantGeometry.Node(handR?handR:head,"Enemy channel seal",new Vector3(0,.7f,0));
   channelLine=SpellFx.Line(channelHalo,"Channel rune",SpellFx.Polygon(6,.15f,30),true,.016f,new Color(.86f,.14f,.28f,.8f),true,false);channelHalo.gameObject.SetActive(false);
  }
  public void Windup(string skillId,float duration){if(dead)return;move=string.IsNullOrEmpty(skillId)?"basic":skillId;windDuration=float.IsNaN(duration)||float.IsInfinity(duration)?1:Mathf.Max(.05f,duration);windAge=0;winding=true;attackAge=20;}
  public void Strike(string skillId){if(dead)return;move=string.IsNullOrEmpty(skillId)?"basic":skillId;winding=false;channel=false;attackAge=0;}
  public void PlayHit(){if(!dead)hitAge=0;}
  public void SetDead(bool value){if(dead==value)return;dead=value;deathAge=0;winding=channel=fleeing=sneaking=false;move="basic";attackAge=20;hitAge=1;moveSpeed=0;if(!value){channelBlend=sneakBlend=fleeBlend=0;}Sample(0);}
  public void SetChannel(bool on){if(dead)return;channel=on;if(on)attackAge=20;}
  public void ChannelBroken(){if(dead)return;channel=winding=false;channelBlend=0;attackAge=20;hitAge=0;if(channelHalo)SpellFx.Impact("assassin",new Color(.70f,.12f,.3f),channelHalo.position,.28f);}
  public void SetFleeing(bool on){if(dead)return;fleeing=on;if(on){channel=winding=false;attackAge=20;move="basic";}}
  public void SetSneak(bool on){if(!dead)sneaking=on;}
  /// <summary>Explicit animation sampling for editor previews; runtime calls this automatically.</summary>
  public void Sample(float deltaTime)
  {
   if(!torso)return;AnimateModel(Mathf.Max(0,deltaTime));
   if(enemyRing){enemyRing.gameObject.SetActive(!dead);var c=new Color(.85f,.08f,.13f,Mathf.Lerp(.65f,.25f,sneakBlend));enemyLine.startColor=enemyLine.endColor=c;}
   if(channelHalo){channelHalo.gameObject.SetActive(!dead&&(winding||channelBlend>.01f));channelHalo.localRotation=Quaternion.Euler(0,0,age*75);channelHalo.localScale=Vector3.one*(.8f+.2f*Mathf.Sin(age*8));}
  }
  void Update(){Sample(Time.deltaTime);}
 }
}
