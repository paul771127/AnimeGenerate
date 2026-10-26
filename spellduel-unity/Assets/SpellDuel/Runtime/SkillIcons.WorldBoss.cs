using System;
namespace SpellDuel {
public static partial class SkillIcons {
static void DrawWorldBossNotice(Cv c){
 V ink=V.Hex("#17243d"),gold=V.Hex("#ffcf70"),red=V.Hex("#ec665c"),paper=V.Hex("#fff0c9");
 c.Solid((x,y)=>Circle(x,y,0,0,.74f),ink);c.Solid((x,y)=>Math.Abs(Circle(x,y,0,0,.66f))-.035f,gold);
 c.Solid((x,y)=>Tri(x,y,-.42f,.15f,-.63f,.63f,-.12f,.36f),gold);c.Solid((x,y)=>Tri(x,y,.42f,.15f,.63f,.63f,.12f,.36f),gold);
 var shield=new[]{-.42f,.30f,0,.43f,.42f,.30f,.31f,-.22f,0,-.49f,-.31f,-.22f};c.Solid((x,y)=>Poly(x,y,shield),gold);
 var inset=new[]{-.34f,.25f,0,.35f,.34f,.25f,.25f,-.17f,0,-.39f,-.25f,-.17f};c.Solid((x,y)=>Poly(x,y,inset),ink);
 c.Solid((x,y)=>Box(x,y,-.16f,.12f,.095f,.035f,-.25f),red);c.Solid((x,y)=>Box(x,y,.16f,.12f,.095f,.035f,.25f),red);
 c.Solid((x,y)=>Box(x,y,0,-.13f,.045f,.16f),paper);c.Solid((x,y)=>Circle(x,y,0,-.36f,.045f),red);
}
}}
