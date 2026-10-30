using System;
using System.Collections.Generic;
namespace SpellDuel
{
 public static partial class SkillIcons
 {
  public static readonly string[] CarpetPetArtIds=BuildCarpetPetIds();
  static string[] BuildCarpetPetIds(){var a=new List<string>();for(int i=0;i<48;i++)a.Add("bait_m"+i);foreach(var k in new[]{"fire","rock","shadow","plant","wing","tide","thunder","shell"})a.Add("food_star_"+k);a.AddRange(new[]{"food_basic","food_fine","food_star","mat_tame_seal","mat_cloud_silk","mat_wind_crystal","mat_sky_badge","mat_airship_compass","soul_sky_kite","soul_sky_eagle","soul_sky_glider","soul_sky_doll","soul_sky_captain","soul_sky_airship","skyweave_weapon","skyweave_head","skyweave_body","skyweave_boots","skyweave_trinket","bp_tailwind"});return a.ToArray();}
  public static bool IsCarpetPetArt(string id)=>id!=null&&Array.IndexOf(CarpetPetArtIds,id)>=0;
  static readonly int[] BaitPools={0,0,1,0,1,0,2,3,1,3,3,3,3,5,4,5,7,6,7,1,5,1,1,1,0,2,0,2,1,3,3,3,4,7,5,6,5,5,1,4,2,7,2,1,1,2,3,4};
  static readonly string[] PetInkColors={"#ed7543","#aaaac6","#ac76dd","#73bf72","#e9cb7d","#56bdd9","#b390f1","#de9172"};
  static void PetShape(Cv c,Sdf f,V tint){c.Solid((x,y)=>f(x,y)-.035f,V.Hex("#142033"));c.Fill(f,(x,y,d)=>V.Lerp(tint,V.Hex("#142033"),y<-.16f?.28f:0));}
  static void PetPaw(Cv c,float x,float y,float s,V tint){PetShape(c,(a,b)=>Circle(a,b,x,y,s*.23f),tint);for(int j=0;j<4;j++){float px=x+(j-1.5f)*s*.18f,py=y+s*(.28f-Math.Abs(j-1.5f)*.05f);c.Solid((a,b)=>Circle(a,b,px,py,s*.09f),tint);}}
  static void WingMark(Cv c,V col){for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++){float x=side*(.12f+i*.12f),y=.1f+i*.055f;c.Solid((a,b)=>Box(a,b,x,y,.07f,.25f,side*-.55f,.05f),col);}}
  static bool DrawCarpetPet(Cv c,string id)
  {
   if(!IsCarpetPetArt(id))return false;
   V cream=V.Hex("#fff4d4"),gold=V.Hex("#ffca63"),teal=V.Hex("#51cdb9"),ink=V.Hex("#142033");
   if(id.StartsWith("bait_m"))
   {
    int kind=int.Parse(id.Substring(6)),pool=BaitPools[kind];V col=V.Hex(PetInkColors[pool]);
    if(pool==0){PetShape(c,(x,y)=>Box(x,y,0,0,.34f,.29f,.35f,.12f),col);for(int j=-1;j<=1;j++){float a=j*.17f;c.Solid((x,y)=>Box(x,y,a,0,.02f,.2f,.35f),ink);}c.Solid((x,y)=>Box(x,y,-.28f,-.45f,.025f,.25f,.35f),gold);}
    if(pool==1||pool==2){PetShape(c,(x,y)=>Poly(x,y,new[]{-.45f,-.25f,-.25f,.43f,.17f,.55f,.44f,.04f,.23f,-.42f}),col);c.Solid((x,y)=>Tri(x,y,-.25f,.43f,.17f,.55f,-.03f,-.15f),cream,.7f);c.Solid((x,y)=>Tri(x,y,-.03f,-.15f,.44f,.04f,.23f,-.42f),ink,.35f);if(pool==2)c.Solid((x,y)=>Box(x,y,.02f,.12f,.06f,.06f,.785f),cream);}
    if(pool==3){PetShape(c,(x,y)=>Math.Min(Box(x,y,-.12f,0,.18f,.43f,-.45f,.13f),Box(x,y,.15f,.03f,.18f,.43f,.5f,.13f)),col);c.Solid((x,y)=>Box(x,y,0,-.05f,.30f,.035f,-.1f),cream);}
    if(pool==4){PetShape(c,(x,y)=>Box(x,y,0,-.05f,.35f,.38f,0,.17f),col);c.Solid((x,y)=>Box(x,y,0,.32f,.27f,.04f),teal);WingMark(c,cream);}
    if(pool==5){PetShape(c,(x,y)=>Math.Min(Box(x,y,.08f,0,.37f,.18f,-.2f,.15f),Tri(x,y,-.22f,0,-.53f,.3f,-.48f,-.24f)),col);c.Solid((x,y)=>Circle(x,y,.3f,.05f,.035f),ink);for(int j=0;j<3;j++){float a=j*.14f-.1f;c.Solid((x,y)=>Box(x,y,a,0,.015f,.11f,-.2f),cream);}}
    if(pool==6){PetShape(c,(x,y)=>Box(x,y,0,-.06f,.38f,.30f,0,.17f),col);c.Solid((x,y)=>Poly(x,y,new[]{.07f,.4f,-.2f,.02f,-.02f,.02f,-.12f,-.32f,.23f,.12f,.05f,.12f}),cream);}
    if(pool==7){PetShape(c,(x,y)=>Circle(x,y,0,0,.42f),col);for(int j=0;j<34;j++){float t=j/33f,a=t*9,r=.04f+t*.28f,px=(float)Math.Cos(a)*r,py=(float)Math.Sin(a)*r;c.Solid((x,y)=>Circle(x,y,px,py,.025f),cream);}}
    // Identity beads preserve the permitted shared silhouette, while the gold seal identifies elite bait.
    for(int j=0;j<1+kind%3;j++){float x=-.2f+j*.16f;c.Solid((a,b)=>Circle(a,b,x,-.56f,.045f),cream);}
    if(kind>=23)for(int side=-1;side<=1;side+=2){float x=side*.38f;PetShape(c,(a,b)=>Circle(a,b,x,.4f,.15f),gold);c.Solid((a,b)=>Box(a,b,x,.4f,.045f,.06f,.785f),ink);}
    return true;
   }
   if(id.StartsWith("food_"))
   {int sp=Array.IndexOf(new[]{"food_star_fire","food_star_rock","food_star_shadow","food_star_plant","food_star_wing","food_star_tide","food_star_thunder","food_star_shell"},id);V col=sp>=0?V.Hex(PetInkColors[sp]):id=="food_basic"?V.Hex("#bc885a"):id=="food_fine"?teal:V.Hex("#717bc8");PetShape(c,(x,y)=>Box(x,y,0,-.05f,.38f,.46f,0,.13f),col);PetShape(c,(x,y)=>Box(x,y,0,.4f,.40f,.07f,0,.035f),gold);if(id.StartsWith("food_star")){c.Solid((x,y)=>Box(x,y,0,.04f,.21f,.07f,.785f),cream);c.Solid((x,y)=>Box(x,y,0,.04f,.07f,.21f,.785f),cream);}else PetPaw(c,0,-.07f,.8f,cream);return true;}
   if(id=="mat_cloud_silk"){PetShape(c,(x,y)=>Box(x,y,0,0,.3f,.4f,0,.08f),cream);for(int i=0;i<6;i++){float y=-.28f+i*.11f;c.Solid((x,b)=>Box(x,b,0,y,.34f,.025f,-.08f),teal);}return true;}
   if(id=="mat_wind_crystal"){PetShape(c,(x,y)=>Poly(x,y,new[]{0f,.62f,.38f,.1f,0f,-.55f,-.38f,.1f}),teal);c.Solid((x,y)=>Tri(x,y,0,.62f,0,-.55f,-.38f,.1f),cream,.65f);return true;}
   if(id=="bp_tailwind")
   {PetShape(c,(x,y)=>Math.Min(Circle(x,y,0,-.12f,.39f),Box(x,y,0,.3f,.16f,.24f)),V.Hex("#b9e4e5"));c.Solid((x,y)=>Math.Max(Circle(x,y,0,-.12f,.33f),y-.03f),teal);PetShape(c,(x,y)=>Box(x,y,0,.53f,.20f,.07f,0,.02f),gold);c.Solid((x,y)=>Box(x,y,-.18f,-.08f,.03f,.16f),cream);return true;}
   if(id.StartsWith("skyweave_"))
   {
    if(id.EndsWith("weapon")){PetShape(c,(x,y)=>Poly(x,y,new[]{-.1f,-.4f,.02f,.6f,.2f,.72f,.16f,-.36f}),cream);PetShape(c,(x,y)=>Box(x,y,.02f,-.37f,.3f,.06f),gold);PetShape(c,(x,y)=>Box(x,y,.02f,-.55f,.07f,.13f),teal);}
    else if(id.EndsWith("head")){PetShape(c,(x,y)=>Box(x,y,0,0,.56f,.09f),teal);for(int side=-1;side<=1;side+=2){float x=side*.25f;PetShape(c,(a,b)=>Box(a,b,x,.02f,.22f,.18f,0,.07f),gold);c.Solid((a,b)=>Box(a,b,x,.02f,.16f,.12f,0,.04f),teal);}}
    else if(id.EndsWith("body")){PetShape(c,(x,y)=>Poly(x,y,new[]{-.2f,.5f,-.56f,.2f,-.44f,-.07f,-.28f,.07f,-.32f,-.5f,.32f,-.5f,.28f,.07f,.44f,-.07f,.56f,.2f,.2f,.5f,0f,.27f}),teal);c.Solid((x,y)=>Box(x,y,0,-.12f,.035f,.35f),cream);c.Solid((x,y)=>Box(x,y,0,-.28f,.28f,.035f),gold);}
    else if(id.EndsWith("boots")){for(int side=-1;side<=1;side+=2){float dx=side*.23f;PetShape(c,(x,y)=>Math.Min(Box(x,y,dx,.1f,.15f,.38f,0,.05f),Box(x,y,dx+.05f,-.28f,.22f,.12f,0,.06f)),teal);c.Solid((x,y)=>Box(x,y,dx,.25f,.16f,.035f),gold);}}
    else {WingMark(c,gold);PetShape(c,(x,y)=>Circle(x,y,0,-.15f,.24f),teal);c.Solid((x,y)=>Tri(x,y,-.08f,-.2f,0,.05f,.08f,-.2f),cream);}
    return true;
   }
   // Medallions and souls each keep a different, source-specific centre silhouette.
   bool soul=id.StartsWith("soul_");V baseCol=soul?teal:gold;
   PetShape(c,(x,y)=>Circle(x,y,0,0,.49f),baseCol);c.Solid((x,y)=>Circle(x,y,0,0,.39f),ink);
   if(id=="mat_tame_seal")PetPaw(c,0,-.08f,1,cream);
   else if(id.EndsWith("kite")){PetShape(c,(x,y)=>Box(x,y,0,.08f,.19f,.24f,.785f),V.Hex("#ed7543"));c.Solid((x,y)=>Box(x,y,.02f,-.3f,.025f,.18f,-.3f),cream);}
   else if(id.EndsWith("eagle")){WingMark(c,cream);c.Solid((x,y)=>Circle(x,y,0,-.13f,.13f),teal);}
   else if(id.EndsWith("glider")){c.Solid((x,y)=>Tri(x,y,-.35f,-.18f,0,.38f,.35f,-.18f),gold);c.Solid((x,y)=>Circle(x,y,0,-.14f,.09f),teal);}
   else if(id.EndsWith("doll")){for(int j=0;j<3;j++){float a=j*2.094f,px=(float)Math.Cos(a)*.22f,py=(float)Math.Sin(a)*.22f;c.Solid((x,y)=>Circle(x,y,px,py,.13f),teal);}c.Solid((x,y)=>Circle(x,y,0,0,.11f),cream);}
   else if(id.EndsWith("captain")){c.Solid((x,y)=>Math.Abs(Circle(x,y,0,.08f,.24f))-.045f,gold);c.Solid((x,y)=>Box(x,y,.2f,-.21f,.035f,.22f),V.Hex("#c28deb"));}
   else if(id=="mat_sky_badge"){WingMark(c,gold);c.Solid((x,y)=>Box(x,y,0,-.12f,.09f,.18f,.785f),cream);}
   else {for(int j=0;j<8;j++){float a=j*(float)Math.PI/4f,px=(float)Math.Cos(a)*.35f,py=(float)Math.Sin(a)*.35f;c.Solid((x,y)=>Circle(x,y,px,py,.045f),gold);}c.Solid((x,y)=>Tri(x,y,-.12f,-.22f,0,.35f,.12f,-.22f),id=="mat_airship_compass"?V.Hex("#f48170"):teal);c.Solid((x,y)=>Tri(x,y,-.06f,-.15f,0,.24f,0,-.15f),cream);}
   return true;
  }
 }
}
