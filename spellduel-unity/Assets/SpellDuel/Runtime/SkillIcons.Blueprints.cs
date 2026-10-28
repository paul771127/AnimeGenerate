using System;
namespace SpellDuel
{
 public static partial class SkillIcons
 {
  public static readonly string[] BlueprintArtIds={"bp_skewer","bp_chicken","bp_furnace","bp_mushroom","bp_bamboo","bp_planter","bp_rumbarrel","bp_jelly","bp_shark","bp_trophy","bp_corn","bp_neonhalo","bp_luckycat","bp_hourglass","bp_camel"};
  static void BDisc(Cv c,float x,float y,float r,V color){PetShape(c,(a,b)=>Circle(a,b,x,y,r),color);}
  static void BRect(Cv c,float x,float y,float w,float h,V color){PetShape(c,(a,b)=>Box(a,b,x,y,w,h,0,.025f),color);}
  static bool DrawBlueprint(Cv c,string original)
  {
   bool scroll=original.StartsWith("rcp_bp_");string id=scroll?original.Substring(4):original;
   if(Array.IndexOf(BlueprintArtIds,id)<0 && id!="mat_band_crest"&&id!="mat_storm_gem"&&id!="mat_night_edge"&&id!="mat_aid_sigil")return false;
   V gold=V.Hex("#ffca59"),cream=V.Hex("#fff0cb"),red=V.Hex("#e95335"),green=V.Hex("#79bd48"),blue=V.Hex("#64d6f0"),ink=V.Hex("#222c42"),brown=V.Hex("#a95c31");
   if(scroll){BRect(c,0,0,.46f,.58f,V.Hex("#dbc18d"));BRect(c,0,.55f,.55f,.07f,cream);BRect(c,0,-.55f,.55f,.07f,cream);BDisc(c,.4f,-.48f,.1f,red);}
   switch(id)
   {
    case "bp_skewer":
     SoulStroke(c,-.25f,-.48f,.25f,.48f,.025f,gold);for(int i=0;i<4;i++){float y=-.25f+i*.16f;PetShape(c,(x,z)=>Box(x,z,y*.5f,y,.15f,.065f,-.45f,.025f),i%2==0?red:green);}break;
    case "bp_chicken":
     BDisc(c,0,-.07f,.31f,cream);BDisc(c,.12f,.24f,.19f,cream);for(int i=0;i<3;i++)BDisc(c,.03f+i*.07f,.41f,.055f,red);SoulPoly(c,gold,.26f,.27f,.43f,.18f,.26f,.13f);BDisc(c,.17f,.28f,.025f,ink);SoulLeaf(c,-.21f,-.06f,.16f,-.6f,gold);break;
    case "bp_furnace":
     BRect(c,0,-.05f,.29f,.32f,ink);BRect(c,.17f,.34f,.065f,.16f,brown);BRect(c,0,-.01f,.20f,.19f,gold);BRect(c,0,-.01f,.16f,.15f,red);for(int i=-1;i<=1;i++)SoulStroke(c,i*.09f,-.17f,i*.09f,.14f,.018f,ink);break;
    case "bp_mushroom":
     BRect(c,0,-.20f,.10f,.21f,cream);SoulPoly(c,red,-.42f,.02f,-.29f,.30f,0,.43f,.29f,.30f,.42f,.02f);BDisc(c,-.20f,.17f,.06f,cream);BDisc(c,.08f,.29f,.065f,cream);BDisc(c,.27f,.12f,.045f,cream);break;
    case "bp_bamboo":
     SoulBow(c,green);for(int i=0;i<4;i++)BDisc(c,.21f,-.24f+i*.14f,.023f,ink);SoulLeaf(c,-.24f,.28f,.14f,-.5f,green);break;
    case "bp_planter":
     SoulPoly(c,brown,-.29f,.02f,.29f,.02f,.21f,-.38f,-.21f,-.38f);BRect(c,0,.03f,.31f,.055f,gold);SoulStroke(c,0,.07f,0,.38f,.025f,brown);BDisc(c,-.10f,.27f,.12f,green);BDisc(c,.10f,.34f,.13f,green);break;
    case "bp_rumbarrel":
     PetShape(c,(x,y)=>Box(x,y,0,0,.30f,.39f,0,.13f),brown);for(int i=-1;i<=1;i++)BRect(c,0,i*.25f,.30f,.035f,ink);SoulStroke(c,.13f,.3f,.13f,.49f,.02f,cream);SoulStroke(c,.13f,.49f,.30f,.49f,.02f,cream);break;
    case "bp_jelly":
     SoulStroke(c,0,-.48f,0,.16f,.035f,gold);BDisc(c,0,.23f,.24f,blue);for(int i=-2;i<=2;i++){float x=i*.075f;SoulStroke(c,x,.12f,x+.035f,-.05f,.016f,cream);}BDisc(c,-.08f,.25f,.022f,ink);BDisc(c,.08f,.25f,.022f,ink);break;
    case "bp_shark":
     SoulPoly(c,blue,-.40f,-.23f,-.30f,.12f,-.06f,.32f,.30f,.18f,.43f,-.1f,.29f,-.27f);SoulPoly(c,ink,-.24f,-.10f,.29f,-.10f,.21f,-.23f,-.20f,-.23f);for(int i=-2;i<=2;i++){float x=i*.085f;SoulPoly(c,cream,x-.03f,-.10f,x+.03f,-.10f,x,-.18f);}BDisc(c,.15f,.08f,.027f,ink);SoulPoly(c,blue,-.18f,.17f,-.1f,.45f,.04f,.25f);break;
    case "bp_trophy":
     for(int side=-1;side<=1;side+=2)PetShape(c,(x,y)=>Math.Abs(Circle(x,y,side*.28f,.13f,.13f))-.025f,gold);
     SoulPoly(c,gold,-.26f,.33f,.26f,.33f,.18f,-.08f,0,-.19f,-.18f,-.08f);SoulStroke(c,0,-.16f,0,-.36f,.035f,gold);BRect(c,0,-.39f,.19f,.04f,gold);SoulStroke(c,0,.04f,0,.23f,.022f,ink);break;
    case "bp_corn":
     SoulStroke(c,0,-.47f,0,.45f,.025f,brown);BRect(c,0,.12f,.17f,.31f,gold);for(int row=0;row<5;row++)for(int col=-1;col<=1;col++)BDisc(c,col*.09f,-.10f+row*.11f,.035f,row%3==0?brown:cream);SoulLeaf(c,-.17f,-.22f,.18f,-.4f,green);break;
    case "bp_neonhalo":
     PetShape(c,(x,y)=>Math.Abs((float)Math.Sqrt(x*x+y*y*3)-.34f)-.035f,blue);BRect(c,.1f,-.28f,.25f,.08f,V.Hex("#f077d2"));for(int i=0;i<5;i++)SoulStroke(c,-.06f+i*.08f,-.31f,-.06f+i*.08f,-.25f,.012f,cream);break;
    case "bp_luckycat":
     BDisc(c,0,-.14f,.22f,cream);BDisc(c,0,.19f,.21f,cream);SoulPoly(c,gold,-.2f,.28f,-.19f,.47f,-.05f,.35f);SoulPoly(c,gold,.2f,.28f,.19f,.47f,.05f,.35f);BRect(c,0,.30f,.22f,.035f,blue);BDisc(c,-.075f,.19f,.02f,ink);BDisc(c,.075f,.19f,.02f,ink);BRect(c,.29f,.05f,.06f,.18f,cream);BDisc(c,0,-.15f,.10f,gold);break;
    case "bp_hourglass":
     SoulPoly(c,blue,-.23f,.35f,.23f,.35f,.05f,0,.23f,-.35f,-.23f,-.35f,-.05f,0);foreach(float y in new[]{-.37f,.37f})BRect(c,0,y,.28f,.04f,gold);SoulStroke(c,0,-.24f,0,.23f,.018f,gold);SoulPoly(c,gold,-.07f,.12f,0,.25f,.07f,.12f);break;
    case "bp_camel":
     BDisc(c,-.15f,.12f,.20f,brown);BDisc(c,.15f,.12f,.20f,brown);BRect(c,0,-.15f,.31f,.20f,gold);BDisc(c,0,-.03f,.15f,cream);BDisc(c,-.055f,.03f,.018f,ink);BDisc(c,.055f,.03f,.018f,ink);SoulStroke(c,0,-.15f,.19f,-.12f,.015f,green);break;
    case "mat_band_crest": SoulShield(c,red);SoulBlade(c,gold,true);break;
    case "mat_storm_gem": SoulPoly(c,blue,0,.45f,.32f,.05f,.14f,-.35f,-.14f,-.35f,-.32f,.05f);SoulPoly(c,cream,.08f,.29f,-.13f,-.02f,.05f,-.02f,-.06f,-.25f,.20f,.10f,.03f,.10f);break;
    case "mat_night_edge": SoulBlade(c,V.Hex("#b791e6"));SoulPoly(c,ink,.1f,.12f,.3f,.23f,.21f,-.05f,.1f,-.1f);break;
    case "mat_aid_sigil": BDisc(c,0,0,.35f,gold);BRect(c,0,0,.22f,.065f,green);BRect(c,0,0,.065f,.22f,green);break;
   }
   return true;
  }
 }
}
