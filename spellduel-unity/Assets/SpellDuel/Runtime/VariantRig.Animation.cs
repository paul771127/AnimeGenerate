using UnityEngine;
namespace SpellDuel
{
 public sealed partial class VariantRig
 {
  float age,windAge,windDuration=1,attackAge=20,hitAge=1,deathAge,channelBlend,sneakBlend,fleeBlend;
  bool dead,winding,channel,fleeing,sneaking;string move="basic";float moveSpeed;
  Vector3 previousPosition; bool positionReady; float backwards;
  static void Pose(Transform t,float x,float y=0,float z=0){if(t)t.localRotation=Quaternion.Euler(x,y,z);}
  void AnimateModel(float dt)
  {
   dt=Mathf.Max(0,dt);age+=dt;windAge+=dt;attackAge+=dt;hitAge+=dt;if(dead)deathAge+=dt;
   channelBlend=Mathf.MoveTowards(channelBlend,channel&&!dead?1:0,dt*5);sneakBlend=Mathf.MoveTowards(sneakBlend,sneaking&&!dead?1:0,dt*5);fleeBlend=Mathf.MoveTowards(fleeBlend,fleeing&&!dead?1:0,dt*5);
   if(positionReady&&dt>0){float velocity=Vector3.Dot(transform.position-previousPosition,transform.forward)/dt;backwards=Mathf.MoveTowards(backwards,velocity<-.1f?1:0,dt*6);}previousPosition=transform.position;positionReady=true;
   float speed=Mathf.Clamp(Mathf.Abs(moveSpeed),0,4),gait=Mathf.Sin(age*(5+speed*3)),stride=Mathf.Clamp01(speed),w=winding?Mathf.SmoothStep(0,1,windAge/windDuration):0;
   float a=attackAge<.65f?Mathf.Sin(attackAge/.65f*Mathf.PI):0,flinch=Mathf.Clamp01(1-hitAge/.23f),fall=Mathf.SmoothStep(0,1,deathAge/.65f);
   float hostile=assassin?12:5,breath=Mathf.Sin(age*2.4f+variantId)*.008f;
   torso.localPosition=torsoRest+Vector3.up*(dead?-.42f*fall:breath+stride*Mathf.Abs(gait)*.018f);
   Pose(torso,dead?82*fall:hostile+stride*8-w*8+a*13-flinch*15,dead?0:Mathf.Sin(age*1.3f)*2,dead?12*fall:gait*stride*2);
   Pose(head,dead?18*fall:-hostile*.6f-w*6,Mathf.Sin(age*.8f+variantId)*4);
   if(variantId==124){torso.localPosition+=Vector3.up*Mathf.Sin(age*5)*.035f;Pose(torso,dead?80*fall:w*-25+a*40-flinch*15,age*12);ApplyTint(flinch,fall);return;}
   Pose(upperL,gait*stride*26,0,-12);Pose(upperR,-gait*stride*26,0,12);Pose(foreL,-18-stride*15);Pose(foreR,-18-stride*15);
   Pose(hipL,gait*stride*30,0,-3);Pose(hipR,-gait*stride*30,0,3);Pose(kneeL,Mathf.Max(0,-gait)*stride*32);Pose(kneeR,Mathf.Max(0,gait)*stride*32);
   Pose(handL,0);Pose(handR,0);Pose(tail,Mathf.Sin(age*4)*7+stride*18,0,Mathf.Sin(age*2)*4);
   float cast=Mathf.Max(w,channelBlend);
   if(caster){Pose(upperR,-cast*105+a*35,0,12+cast*12);Pose(foreR,-15-cast*12);Pose(upperL,-cast*62,0,-12-cast*18);Pose(foreL,-20-cast*30);}
   else if(archer){float aim=Mathf.Max(w,a);Pose(upperL,-aim*80,aim*15,-12);Pose(foreL,-12);Pose(upperR,-aim*68,-aim*36,15);Pose(foreR,-20-aim*65);}
   else {Pose(upperR,-stride*gait*26-w*112+a*55,-w*25+a*55,12);Pose(foreR,-18-w*22);Pose(upperL,stride*gait*26-w*45+a*20,0,-12);}
   if(variantId==143){float drum=Mathf.Max(cast,a);Pose(upperL,drum*(55+Mathf.Sin(age*13)*20),-drum*35,-35);Pose(upperR,drum*(55-Mathf.Sin(age*13)*20),drum*35,35);Pose(foreL,-20-drum*20);Pose(foreR,-20-drum*20);}
   if(variantId==144){Pose(upperR,-25-cast*55+a*35,cast*20,15);Pose(foreR,-10-cast*15);Pose(accessory,0,Mathf.Sin(age*5)*12,Mathf.Sin(age*3)*5);}
   if(variantId==142){torso.localPosition+=Vector3.up*(.11f+Mathf.Sin(age*3)*.035f);Pose(accessory,0,Mathf.Sin(age*8)*14);Pose(hipL,12);Pose(hipR,20);Pose(kneeL,24);Pose(kneeR,32);}
   // Skill-specific body mechanics. Root position is never animated: authority owns displacement.
   if(move.EndsWith("flankrush")||move.EndsWith("sandlunge"))Pose(torso,hostile+w*10+a*24,0,0);
   if(move.EndsWith("decksweep")||move.EndsWith("polecleave")){Pose(torso,hostile,-w*35+a*80,0);Pose(upperR,-65, -w*45+a*80,15);}
   if(move.EndsWith("guardcounter")){Pose(upperL,-60-w*20,20,-10);Pose(foreL,-65);}
   if(move.EndsWith("highcast")){Pose(upperR,-w*160+a*70,0,15);Pose(foreR,-25-w*30);}
   if(move.EndsWith("retreatshot")&&attackAge<.65f){float roll=Mathf.Clamp01(attackAge/.65f);Pose(torso,hostile-360*roll,0,0);torso.localPosition=torsoRest+Vector3.up*(-.20f*Mathf.Sin(roll*Mathf.PI));Pose(hipL,55);Pose(hipR,55);Pose(kneeL,80);Pose(kneeR,80);}
   if(backwards>.1f&&stride>.1f&&!winding&&attackAge>.65f){Pose(hipL,-gait*stride*25);Pose(hipR,gait*stride*25);}
   bool recover=fleeing&&!dead&&speed<.1f;
   if(potion)potion.gameObject.SetActive(recover&&!caster);
   if(recover){if(caster){Pose(upperL,-60,0,-20);Pose(upperR,-65,0,20);Pose(foreL,-55);Pose(foreR,-55);}else{Pose(upperL,-98,0,-8);Pose(foreL,-100);Pose(handL,25);}}
   if(dead){if(potion)potion.gameObject.SetActive(false);Pose(upperL,20*fall,0,-25*fall);Pose(upperR,15*fall,0,35*fall);Pose(foreL,-15);Pose(foreR,-15);Pose(hipL,5);Pose(hipR,-12);Pose(kneeL,10);Pose(kneeR,22);}
   ApplyTint(flinch,fall);
  }
  void ApplyTint(float flinch,float fall)
  {
   for(int i=0;i<surfaces.Length;i++){var r=surfaces[i];if(!r)continue;bool ghost=sneakBlend>.01f;var wanted=ghost?VariantGeometry.Ghost:originalMaterials[i];if(r.sharedMaterial!=wanted)r.sharedMaterial=wanted;tint.Clear();var c=originalMaterials[i].color;c=Color.Lerp(c,Color.white,flinch*.75f);if(dead)c=Color.Lerp(c,c*.5f,fall);c.a*=Mathf.Lerp(1,.26f,sneakBlend);tint.SetColor("_Color",c);r.SetPropertyBlock(tint);}
  }
 }
}
