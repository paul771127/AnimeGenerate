using System;
namespace SpellDuel
{
 public static partial class SkillIcons
 {
  public static readonly string[] NewSkillArtIds={"arcanewyrm","starfall","arcanebreak","arcanehaste","elementward","landmine","rangerhaste","hawkeye","falcondive","holyblade","consecrate","layhands","headshot","scattershot","blastarrow","spiritbolt","earthspike","pactcheer","suiton","futon","bladestorm","poisondart","nightveil","holynova","divinegrace","lightpillar","holyfire","verdict","mindfocus","meditate","prep","taunt","bless","haste","spring","focus","rollarrow","echo_fire","echo_ice","arcaneburst","venom_burst","retribution","swordfervor","killinstinct","huntfrenzy","chainstorm","soulharvest","renjutsu","reaperdance","graceflow","martyrflame"};
  static void NRing(Cv c,float x,float y,float r,V v,float width=.025f){PetShape(c,(a,b)=>Math.Abs(Circle(a,b,x,y,r))-width,v);}
  static void NArrow(Cv c,float x,float y,float angle,V v){float dx=(float)Math.Sin(angle),dy=(float)Math.Cos(angle);SoulStroke(c,x-dx*.29f,y-dy*.29f,x+dx*.29f,y+dy*.29f,.025f,v);SoulPoly(c,v,x+dx*.38f,y+dy*.38f,x+dx*.17f-dy*.08f,y+dy*.17f+dx*.08f,x+dx*.17f+dy*.08f,y+dy*.17f-dx*.08f);}
  static void NEye(Cv c,V v){SoulPoly(c,v,-.4f,0,-.2f,.15f,.18f,.15f,.4f,0,.18f,-.12f,-.2f,-.12f);BDisc(c,0,.02f,.10f,V.Hex("#252d49"));BDisc(c,.035f,.065f,.025f,V.Hex("#ffffff"));}
  static void NFeather(Cv c,float x,float y,V v){SoulLeaf(c,x,y,.25f,-.5f,v);SoulStroke(c,x-.1f,y-.26f,x+.1f,y+.25f,.018f,V.Hex("#fff4ce"));}
  static void NClock(Cv c,V v){NRing(c,0,0,.32f,v);for(int i=0;i<8;i++){float a=i*(float)Math.PI/4;SoulStroke(c,(float)Math.Cos(a)*.26f,(float)Math.Sin(a)*.26f,(float)Math.Cos(a)*.3f,(float)Math.Sin(a)*.3f,.012f,v);}SoulStroke(c,0,0,0,.21f,.025f,v);SoulStroke(c,0,0,.18f,-.09f,.025f,v);}
  static void NSoul(Cv c,V v){SoulPoly(c,v,-.25f,.1f,-.20f,.30f,0,.40f,.20f,.3f,.25f,.10f,.08f,-.15f,.12f,-.42f,-.09f,-.23f,-.2f,-.1f);BDisc(c,-.07f,.14f,.03f,V.Hex("#252943"));BDisc(c,.07f,.14f,.03f,V.Hex("#252943"));}
  static void NewSkillIcon(Cv c,string id,V v)
  {
   V white=V.Hex("#fff3d4"),ink=V.Hex("#20283e"),gold=V.Hex("#ffd169"),purple=V.Hex("#c58afa"),red=V.Hex("#ff6677"),green=V.Hex("#75eab2");
   switch(id)
   {
    case "arcanewyrm":
     SoulPoly(c,v,-.35f,-.35f,-.31f,.14f,-.12f,.33f,.08f,.30f,.22f,.16f,.41f,.10f,.36f,-.06f,.14f,-.12f,.07f,-.30f);SoulStroke(c,-.15f,.27f,-.25f,.48f,.028f,gold);SoulStroke(c,.02f,.29f,.12f,.48f,.028f,gold);SoulStroke(c,.2f,-.05f,.42f,-.22f,.013f,white);BDisc(c,.07f,.15f,.035f,white);SoulStroke(c,.18f,-.02f,.33f,-.02f,.02f,ink);break;
    case "starfall":for(int i=0;i<3;i++){float x=-.24f+i*.22f,y=.25f-i*.2f;SoulStroke(c,x-.1f,y+.2f,x,y,.04f,red);BDisc(c,x,y,.075f,gold);}NRing(c,0,-.35f,.26f,v,.015f);break;
    case "arcanebreak":BDisc(c,0,0,.20f,purple);NRing(c,0,0,.33f,v);for(int i=0;i<6;i++){float a=i*(float)Math.PI/3;SoulStroke(c,(float)Math.Cos(a)*.50f,(float)Math.Sin(a)*.50f,(float)Math.Cos(a)*.37f,(float)Math.Sin(a)*.37f,.025f,white);}break;
    case "arcanehaste":NClock(c,v);SoulPoly(c,white,.28f,.38f,.18f,.18f,.30f,.18f,.15f,-.03f,.44f,.25f,.31f,.25f);break;
    case "elementward":SoulShield(c,gold);for(int i=0;i<4;i++){float a=i*(float)Math.PI/2;BDisc(c,(float)Math.Cos(a)*.18f,(float)Math.Sin(a)*.18f,.08f,i==0?red:i==1?green:i==2?purple:v);}break;
    case "landmine":BRect(c,0,-.1f,.32f,.13f,ink);NRing(c,0,-.04f,.28f,v);BDisc(c,0,.02f,.08f,red);for(int i=-1;i<=1;i++)SoulStroke(c,i*.17f,.25f,i*.21f,.4f,.02f,gold);break;
    case "rangerhaste":NArrow(c,0,0,.6f,green);for(int i=0;i<3;i++)SoulStroke(c,-.43f,-.18f+i*.13f,-.1f,-.05f+i*.13f,.02f,v);break;
    case "hawkeye":NEye(c,gold);NFeather(c,-.22f,-.23f,v);break;
    case "falcondive":SoulPoly(c,gold,-.45f,.30f,-.1f,.12f,0,.23f,.1f,.12f,.45f,.30f,.25f,-.02f,.09f,-.07f,0,-.36f,-.09f,-.07f,-.25f,-.02f);SoulStroke(c,0,-.32f,0,-.46f,.02f,white);break;
    case "holyblade":SoulBlade(c,gold);PetShape(c,(x,y)=>Math.Max(Math.Abs(Circle(x,y,0,0,.40f))-.035f,-x),v);break;
    case "consecrate":NRing(c,0,-.07f,.37f,gold);NRing(c,0,-.07f,.25f,v);BRect(c,0,0,.04f,.22f,white);BRect(c,0,.06f,.15f,.035f,white);break;
    case "layhands":for(int side=-1;side<=1;side+=2){SoulPoly(c,gold,side*.05f,-.33f,side*.23f,-.18f,side*.31f,.20f,side*.20f,.26f,side*.16f,.03f,side*.06f,-.02f);}BDisc(c,0,.23f,.10f,white);break;
    case "headshot":NRing(c,.13f,.13f,.19f,red);SoulStroke(c,-.42f,-.36f,.28f,.28f,.035f,white);BDisc(c,.13f,.13f,.035f,gold);break;
    case "scattershot":for(int i=-2;i<=2;i++)NArrow(c,i*.07f,-.03f,i*.32f,gold);break;
    case "blastarrow":NArrow(c,0,.08f,.55f,v);for(int i=0;i<6;i++){float a=i*(float)Math.PI/3;SoulStroke(c,.15f+(float)Math.Cos(a)*.12f,.22f+(float)Math.Sin(a)*.12f,.15f+(float)Math.Cos(a)*.25f,.22f+(float)Math.Sin(a)*.25f,.03f,red);}break;
    case "spiritbolt":NSoul(c,purple);SoulStroke(c,-.30f,-.1f,-.42f,-.4f,.018f,v);break;
    case "earthspike":for(int i=-1;i<=1;i++)SoulPoly(c,v,i*.22f-.10f,-.33f,i*.22f,.35f-Math.Abs(i)*.18f,i*.22f+.12f,-.33f);SoulStroke(c,-.4f,-.36f,.4f,-.36f,.025f,gold);break;
    case "pactcheer":NRing(c,0,0,.33f,v);SoulPoly(c,gold,0,.30f,.27f,-.17f,-.27f,-.17f);NRing(c,0,0,.13f,white);break;
    case "suiton":for(int i=-2;i<=2;i++){float x=i*.14f;SoulStroke(c,x,-.32f,x,.28f-Math.Abs(i)*.05f,.03f,v);}NRing(c,0,0,.36f,v,.018f);break;
    case "futon":for(int i=0;i<3;i++){float y=i*.16f-.16f;SoulStroke(c,-.38f,y-.1f,.3f,y+.06f,.026f,green);}SoulPoly(c,white,.22f,.3f,.46f,.13f,.29f,.02f);break;
    case "bladestorm":for(int i=0;i<3;i++){float a=i*2.094f;NArrow(c,(float)Math.Cos(a)*.14f,(float)Math.Sin(a)*.14f,a+.8f,purple);}NRing(c,0,0,.36f,v,.015f);break;
    case "poisondart":for(int i=-1;i<=1;i++)NArrow(c,i*.12f,0,i*.45f,green);BDisc(c,.30f,-.25f,.07f,v);break;
    case "nightveil":SoulPoly(c,purple,0,.43f,.19f,.25f,.37f,-.38f,0,-.24f,-.37f,-.38f,-.19f,.25f);SoulPoly(c,ink,0,.25f,.12f,.02f,-.12f,.02f);break;
    case "holynova":for(int i=0;i<8;i++){float a=i*(float)Math.PI/4;SoulStroke(c,(float)Math.Cos(a)*.15f,(float)Math.Sin(a)*.15f,(float)Math.Cos(a)*.42f,(float)Math.Sin(a)*.42f,.025f,gold);}BDisc(c,0,0,.14f,white);break;
    case "divinegrace":for(int i=-2;i<=2;i++)SoulStroke(c,i*.15f,.38f-Math.Abs(i)*.04f,i*.15f-.06f,-.1f,.02f,green);BRect(c,0,-.22f,.14f,.03f,white);BRect(c,0,-.22f,.03f,.14f,white);break;
    case "lightpillar":BRect(c,0,.04f,.035f,.43f,white);SoulStroke(c,-.10f,-.34f,-.1f,.28f,.012f,gold);SoulStroke(c,.1f,-.34f,.1f,.28f,.012f,gold);NRing(c,0,-.3f,.14f,v);break;
    case "holyfire":SoulPoly(c,red,0,.46f,.27f,.02f,.21f,-.28f,0,-.40f,-.23f,-.25f,-.29f,.05f,-.08f,.17f);SoulPoly(c,gold,0,.18f,.13f,-.15f,0,-.31f,-.13f,-.15f);break;
    case "verdict":SoulStroke(c,-.23f,-.40f,.15f,.25f,.055f,gold);PetShape(c,(x,y)=>Box(x,y,.12f,.23f,.28f,.12f,-.5f,.025f),white);break;
    case "mindfocus":NEye(c,green);NClock(c,v);break;
    case "meditate":BDisc(c,0,.19f,.10f,v);SoulPoly(c,v,0,.04f,.25f,-.23f,.37f,-.34f,-.37f,-.34f,-.25f,-.23f);NRing(c,0,0,.43f,purple,.015f);break;
    case "prep":SoulBlade(c,purple);NClock(c,v);BDisc(c,.27f,-.27f,.07f,red);break;
    case "taunt":SoulShield(c,v);SoulStroke(c,0,.22f,0,-.03f,.04f,gold);BDisc(c,0,-.15f,.045f,gold);break;
    case "bless":NFeather(c,-.22f,0,gold);NFeather(c,.22f,0,gold);BRect(c,0,0,.04f,.23f,white);BRect(c,0,.07f,.14f,.03f,white);break;
    case "haste":for(int i=-1;i<=1;i++)SoulPoly(c,v,-.25f,i*.19f-.08f,0,i*.19f+.1f,.25f,i*.19f-.08f,0,i*.19f);break;
    case "spring":for(int i=-1;i<=1;i++){SoulLeaf(c,i*.18f,.1f,.23f,i*.5f,green);}NRing(c,0,-.28f,.21f,v,.02f);break;
    case "focus":NRing(c,0,0,.27f,gold);SoulStroke(c,0,-.43f,0,.43f,.015f,v);SoulStroke(c,-.43f,0,.43f,0,.015f,v);BDisc(c,0,0,.05f,white);break;
    case "rollarrow":NArrow(c,0,0,.7f,green);NRing(c,0,0,.39f,v);SoulPoly(c,v,-.38f,.04f,-.50f,.19f,-.25f,.19f);break;
    case "echo_fire":Fire(c);NRing(c,0,0,.41f,purple,.025f);BDisc(c,-.33f,.30f,.05f,white);break;
    case "echo_ice":Ice(c);NRing(c,0,0,.41f,purple,.025f);BDisc(c,.33f,.30f,.05f,white);break;
    case "arcaneburst":for(int i=0;i<5;i++){float a=i*1.256f;SoulPoly(c,purple,0,0,(float)Math.Cos(a)*.45f,(float)Math.Sin(a)*.45f,(float)Math.Cos(a+.35f)*.22f,(float)Math.Sin(a+.35f)*.22f);}BDisc(c,0,0,.09f,white);break;
    case "venom_burst":for(int i=0;i<7;i++){float a=i*.897f;BDisc(c,(float)Math.Cos(a)*.28f,(float)Math.Sin(a)*.28f,.09f,green);}BDisc(c,0,0,.18f,v);BDisc(c,-.05f,.04f,.025f,ink);BDisc(c,.05f,.04f,.025f,ink);break;
    case "retribution":SoulShield(c,gold);SoulBlade(c,red);break;
    case "swordfervor":SoulBlade(c,white);for(int i=-1;i<=1;i++)SoulStroke(c,i*.17f-.05f,-.38f,i*.17f+.06f,-.14f,.025f,red);break;
    case "killinstinct":NEye(c,red);SoulStroke(c,-.15f,-.4f,.2f,.4f,.025f,white);break;
    case "huntfrenzy":for(int i=-1;i<=1;i++)SoulStroke(c,i*.20f-.05f,-.35f,i*.20f+.06f,.35f,.04f,green);NRing(c,0,0,.43f,gold,.012f);break;
    case "chainstorm":Thunder(c,v);for(int i=-1;i<=1;i++)NRing(c,i*.24f,-.23f,.10f,white,.015f);break;
    case "soulharvest":NSoul(c,v);NRing(c,0,0,.46f,gold,.018f);BDisc(c,.33f,.26f,.04f,white);break;
    case "renjutsu":for(int i=-1;i<=1;i++){float y=i*.22f;SoulPoly(c,v,-.12f,y,0,y+.12f,.12f,y,0,y-.12f);}SoulStroke(c,-.27f,-.3f,-.27f,.3f,.016f,white);break;
    case "reaperdance":SoulStroke(c,-.1f,-.43f,-.1f,.32f,.04f,purple);SoulPoly(c,white,-.1f,.32f,.22f,.30f,.4f,.08f,.13f,.19f,-.1f,.18f);break;
    case "graceflow":BRect(c,0,0,.055f,.28f,white);BRect(c,0,.05f,.2f,.045f,white);for(int i=-1;i<=1;i++)BDisc(c,i*.22f,-.3f,.055f,green);NRing(c,0,0,.42f,v,.015f);break;
    case "martyrflame":SoulShield(c,red);SoulPoly(c,gold,0,.36f,.18f,-.07f,0,-.28f,-.18f,-.07f);BRect(c,0,-.02f,.025f,.13f,white);break;
   }
  }
 }
}
