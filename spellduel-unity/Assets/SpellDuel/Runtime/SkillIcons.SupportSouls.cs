using System;
namespace SpellDuel
{
    public static partial class SkillIcons
    {
        public static readonly string[] SupportSoulArtIds={"soul_m141","soul_m142","soul_m143","soul_m144","soul_m151","soul_m152","soul_m153","soul_m154","soul_m155","soul_m156","soul_m157","soul_m158","soul_m159","soul_m160","soul_m161","soul_m162","soul_m163","soul_m164","soul_m165"};
        static readonly string[] SupportSoulColors={"#8cff73","#ffb3f2","#ff8050","#ffd84d","#ff7350","#ff9a50","#ff5a33","#73d980","#8ce666","#66ff99","#598ce6","#4dccff","#4059a0","#ffcc59","#e6b366","#fff299","#bf8c59","#f2bf66","#d9a659"};
        static void SoulPoly(Cv c,V color,params float[] points){PetShape(c,(x,y)=>Poly(x,y,points),color);}
        static void SoulStroke(Cv c,float ax,float ay,float bx,float by,float width,V color)
        {float dx=bx-ax,dy=by-ay,angle=(float)Math.Atan2(dy,dx),length=(float)Math.Sqrt(dx*dx+dy*dy);PetShape(c,(x,y)=>Box(x,y,(ax+bx)*.5f,(ay+by)*.5f,length*.5f,width,angle,width),color);}
        static void SoulShield(Cv c,V color){SoulPoly(c,color,-.3f,.28f,0,.37f,.3f,.28f,.24f,-.15f,0,-.4f,-.24f,-.15f);}
        static void SoulBlade(Cv c,V color,bool slim=false)
        {float w=slim?.055f:.09f;SoulPoly(c,color,-w,-.18f,-w,.27f,0,.52f,w,.27f,w,-.18f);SoulStroke(c,-.19f,-.2f,.19f,-.2f,.035f,V.Hex("#ffe6a6"));SoulStroke(c,0,-.24f,0,-.43f,.045f,color);c.Solid((x,y)=>Box(x,y,-.027f,.1f,.015f,.23f),V.Hex("#fff8e3"));}
        static void SoulLeaf(Cv c,float ox,float oy,float size,float angle,V color)
        {PetShape(c,(x,y)=>Box(x,y,ox,oy,size*.32f,size,angle,size*.28f),color);}
        static void SoulBow(Cv c,V color)
        {c.Solid((x,y)=>Math.Max(Math.Abs(Circle(x,y,-.12f,0,.36f))-.045f,-x-.12f),V.Hex("#142033"));c.Solid((x,y)=>Math.Max(Math.Abs(Circle(x,y,-.12f,0,.36f))-.025f,-x-.12f),color);SoulStroke(c,-.10f,-.36f,-.10f,.36f,.012f,V.Hex("#fff8e3"));SoulStroke(c,-.34f,0,.38f,0,.022f,color);SoulPoly(c,V.Hex("#fff8e3"),.46f,0,.29f,.09f,.29f,-.09f);}
        static bool DrawSupportSoul(Cv c,string id)
        {
            int ix=Array.IndexOf(SupportSoulArtIds,id);if(ix<0)return false;
            V col=V.Hex(SupportSoulColors[ix]),ivory=V.Hex("#fff4d4"),gold=V.Hex("#ffd271"),ink=V.Hex("#142033");
            // Same dark outlined medallion language as the six existing sky souls.
            PetShape(c,(x,y)=>Circle(x,y,0,0,.56f),col);c.Solid((x,y)=>Circle(x,y,0,0,.47f),ink);
            c.Solid((x,y)=>Box(x,y,0,.55f,.055f,.055f,.785f),ivory);
            switch(id)
            {
                case "soul_m141": // Leaf staff / steady regeneration.
                    SoulStroke(c,0,-.37f,0,.28f,.035f,gold);
                    SoulLeaf(c,-.12f,.13f,.20f,-.65f,col);SoulLeaf(c,.13f,.26f,.19f,.65f,col);
                    c.Solid((x,y)=>Box(x,y,0,-.12f,.14f,.025f),ivory);c.Solid((x,y)=>Box(x,y,0,-.12f,.025f,.14f),ivory);break;
                case "soul_m142": // Fairy wings protecting a central shield.
                    for(int side=-1;side<=1;side+=2){SoulLeaf(c,side*.26f,.16f,.20f,side*.6f,col);SoulLeaf(c,side*.29f,-.13f,.14f,side*-.7f,col);}
                    SoulPoly(c,ivory,-.15f,.15f,.15f,.15f,.12f,-.13f,0,-.25f,-.12f,-.13f);break;
                case "soul_m143": // Drum and paired beaters, not a generic mana drop.
                    PetShape(c,(x,y)=>Box(x,y,0,-.09f,.27f,.23f,0,.06f),col);
                    SoulStroke(c,-.28f,.16f,.28f,.16f,.035f,gold);SoulStroke(c,-.28f,-.31f,.28f,-.31f,.035f,gold);
                    for(int i=-1;i<=1;i++){float dx=i*.16f;SoulStroke(c,dx-.06f,-.26f,dx+.06f,.1f,.018f,ivory);}
                    SoulStroke(c,-.29f,.36f,.05f,.18f,.024f,ivory);SoulStroke(c,.29f,.36f,-.05f,.18f,.024f,ivory);break;
                case "soul_m144":
                    SoulStroke(c,-.22f,-.4f,-.22f,.4f,.025f,gold);
                    SoulPoly(c,col,-.18f,.34f,.30f,.30f,.21f,.08f,.30f,-.12f,-.18f,-.08f);
                    SoulPoly(c,ivory,-.08f,.2f,.12f,.2f,.09f,.04f,.02f,-.02f,-.05f,.04f);break;
                case "soul_m151":
                    SoulBlade(c,ivory);SoulPoly(c,col,.24f,.04f,.37f,-.17f,.34f,-.30f,.21f,-.33f,.15f,-.22f);break;
                case "soul_m152":
                    SoulStroke(c,-.35f,.17f,.35f,.17f,.04f,col);SoulStroke(c,0,-.37f,0,.35f,.04f,gold);
                    SoulStroke(c,-.35f,.17f,0,-.05f,.015f,ivory);SoulStroke(c,.35f,.17f,0,-.05f,.015f,ivory);
                    c.Solid((x,y)=>Math.Abs(Circle(x,y,0,.18f,.14f))-.02f,ivory);break;
                case "soul_m153":
                    SoulStroke(c,0,-.4f,0,.13f,.035f,gold);
                    SoulPoly(c,col,-.25f,.14f,-.18f,.33f,-.08f,.19f,.06f,.48f,.2f,.27f,.25f,.06f,.1f,-.08f,-.12f,-.08f);
                    SoulPoly(c,ivory,-.09f,.04f,.02f,.27f,.11f,.04f,0,-.03f);break;
                case "soul_m154":
                    SoulBlade(c,col,true);SoulLeaf(c,-.26f,.1f,.2f,-.7f,ivory);
                    c.Solid((x,y)=>Circle(x,y,.26f,-.27f,.09f),col);SoulPoly(c,col,.17f,-.27f,.26f,-.06f,.35f,-.27f);break;
                case "soul_m155":
                    SoulBow(c,col);SoulLeaf(c,.13f,.3f,.15f,.7f,ivory);
                    SoulStroke(c,-.28f,-.26f,-.18f,-.26f,.022f,gold);break;
                case "soul_m156":
                    for(int side=-1;side<=1;side+=2){SoulStroke(c,side*.12f,-.05f,side*.26f,.35f,.025f,col);SoulStroke(c,side*.2f,.17f,side*.36f,.27f,.025f,col);}
                    SoulPoly(c,ivory,-.20f,-.12f,-.13f,.02f,0,-.04f,.13f,.02f,.20f,-.12f,0,-.34f);break;
                case "soul_m157":
                    SoulPoly(c,ivory,-.3f,-.3f,-.05f,.03f,.2f,.37f,.25f,.18f,.12f,-.03f,-.15f,-.37f);
                    PetShape(c,(x,y)=>Circle(x,y,.22f,-.21f,.16f),gold);c.Solid((x,y)=>Box(x,y,.22f,-.21f,.027f,.1f),ink);break;
                case "soul_m158":
                    SoulPoly(c,col,0,.43f,.22f,.1f,0,-.24f,-.22f,.1f);
                    SoulPoly(c,ivory,0,.43f,0,-.24f,-.13f,.1f);
                    SoulStroke(c,-.32f,-.26f,-.08f,-.36f,.025f,col);SoulStroke(c,-.08f,-.36f,.31f,-.22f,.025f,col);break;
                case "soul_m159":
                    SoulStroke(c,-.29f,-.31f,.29f,.32f,.048f,ivory);SoulStroke(c,.29f,-.31f,-.29f,.32f,.048f,col);
                    SoulStroke(c,-.3f,-.14f,-.1f,-.33f,.025f,gold);SoulStroke(c,.3f,-.14f,.1f,-.33f,.025f,gold);
                    SoulPoly(c,ivory,0,.2f,.06f,.06f,.2f,0,.06f,-.06f,0,-.2f,-.06f,-.06f,-.2f,0,-.06f,.06f);break;
                case "soul_m160":
                    SoulShield(c,col);c.Solid((x,y)=>Box(x,y,0,.02f,.035f,.21f),ivory);c.Solid((x,y)=>Box(x,y,0,.05f,.16f,.035f),ivory);
                    for(int side=-1;side<=1;side+=2)for(int j=0;j<3;j++)SoulLeaf(c,side*(.32f-.025f*j),-.22f+j*.17f,.09f,side*.55f,gold);break;
                case "soul_m161":
                    SoulStroke(c,-.20f,-.37f,.12f,.24f,.027f,gold);SoulPoly(c,ivory,.02f,.22f,.25f,.46f,.19f,.12f);
                    SoulStroke(c,-.32f,-.17f,0,-.3f,.025f,col);SoulStroke(c,0,-.3f,.30f,-.14f,.025f,col);break;
                case "soul_m162":
                    SoulPoly(c,col,-.28f,.13f,.28f,.13f,.18f,-.13f,.04f,-.20f,.04f,-.33f,.2f,-.38f,-.2f,-.38f,-.04f,-.33f,-.04f,-.20f,-.18f,-.13f);
                    SoulStroke(c,0,.21f,0,.44f,.025f,ivory);SoulStroke(c,-.11f,.33f,.11f,.33f,.025f,ivory);break;
                case "soul_m163":
                    SoulBlade(c,ivory,true);SoulStroke(c,-.36f,-.06f,-.19f,.12f,.025f,col);SoulStroke(c,-.36f,-.24f,-.19f,-.06f,.025f,col);
                    SoulPoly(c,col,.2f,-.27f,.35f,-.06f,.38f,-.28f);break;
                case "soul_m164":
                    SoulPoly(c,col,-.24f,.33f,.24f,.33f,.08f,0,.24f,-.33f,-.24f,-.33f,-.08f,0);
                    SoulStroke(c,-.29f,.36f,.29f,.36f,.035f,gold);SoulStroke(c,-.29f,-.36f,.29f,-.36f,.035f,gold);
                    SoulPoly(c,ivory,-.12f,.19f,.12f,.19f,0,.03f);SoulPoly(c,ivory,-.13f,-.24f,0,-.1f,.13f,-.24f);break;
                case "soul_m165":
                    SoulBow(c,col);SoulPoly(c,gold,-.13f,-.32f,0,-.1f,.13f,-.32f);
                    c.Solid((x,y)=>Box(x,y,-.26f,.30f,.06f,.06f,.785f),ivory);break;
            }
            return true;
        }
    }
}
