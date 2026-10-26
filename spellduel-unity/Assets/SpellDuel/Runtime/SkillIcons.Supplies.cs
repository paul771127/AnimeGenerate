using System;
using UnityEngine;
namespace SpellDuel {
public static partial class SkillIcons {
public static readonly string[] PotionArtIds={"bp_str","bp_eye","bp_regen","bp_swift","bp_wise","bp_ward","bp_blood","bp_iron","bp_vigor","bp_wind"};
static readonly string[] PotionArtColors={"#ef584f","#ffca4e","#61d27d","#40d4ba","#b98aff","#559aff","#bd375f","#8fb3c8","#aad34e","#66e4ef"};
public static readonly string[] MaterialArtIds={"mat_ore","mat_ore_hi","mat_refine","mat_refine_hi","mat_elite","mat_core_golem","mat_core_tree","mat_core_sea","mat_core_arena","mat_core_canyon","mat_soul_smith"};
public static bool IsSupply(string id)=>id!=null&&(Array.IndexOf(PotionArtIds,id)>=0||Array.IndexOf(MaterialArtIds,id)>=0);
static bool DrawSupply(Cv c,string id){
 if(!IsSupply(id))return false;int potion=Array.IndexOf(PotionArtIds,id);V ink=V.Hex("#142033"),cream=V.Hex("#fff4d4");
 if(potion>=0){var tint=V.Hex(PotionArtColors[potion]);Sdf bottle=(x,y)=>Math.Min(Circle(x,y,0,-.18f,.46f),Box(x,y,0,.32f,.18f,.29f));c.Solid((x,y)=>bottle(x,y)-.035f,ink);c.Solid(bottle,V.Hex("#cce5eb"));c.Solid((x,y)=>Math.Max(Circle(x,y,0,-.18f,.40f),y-.04f-(float)Math.Sin(x*7)*.018f),(V)tint);c.Fill((x,y)=>Math.Max(Circle(x,y,0,-.18f,.40f),y-.04f),(x,y,d)=>V.Lerp(tint,ink,y<-.28f?.32f:.03f));c.Solid((x,y)=>Box(x,y,0,.58f,.23f,.075f,0,.035f),ink);c.Solid((x,y)=>Box(x,y,0,.60f,.20f,.045f),cream);c.Solid((x,y)=>Box(x,y,0,.72f,.15f,.08f,0,.025f),V.Hex("#bd8754"));c.Solid((x,y)=>Box(x,y,0,-.23f,.23f,.12f,0,.03f),cream);c.Solid((x,y)=>Box(x,y,0,-.23f,.055f,.055f,.785f),tint);c.Solid((x,y)=>Box(x,y,-.23f,-.15f,.025f,.19f,-.12f,.02f),V.Hex("#ffffff"),.85f);c.Solid((x,y)=>Circle(x,y,.22f,-.4f,.025f),cream);return true;}
 bool hi=id.EndsWith("_hi"),core=id.StartsWith("mat_core_"),soul=id=="mat_soul_smith";
 V col=core?id.EndsWith("golem")?V.Hex("#ff7650"):id.EndsWith("tree")?V.Hex("#7ee06f"):id.EndsWith("sea")?V.Hex("#69e3f2"):id.EndsWith("arena")?V.Hex("#f7cd64"):V.Hex("#dca46b"):soul?V.Hex("#fff5c6"):id.Contains("refine")?V.Hex(hi?"#ffbe57":"#71b9ff"):id.Contains("ore")?V.Hex(hi?"#a9d5ec":"#a1a8b0"):id=="mat_elite"?V.Hex("#f5cb66"):V.Hex("#9fd3bc");
 if(core||soul||id=="mat_elite"){c.Glow((x,y)=>Circle(x,y,0,0,.4f),col,.10f,.3f);c.Solid((x,y)=>Circle(x,y,0,0,.52f),ink);c.Solid((x,y)=>Circle(x,y,0,0,.47f),col);c.Solid((x,y)=>Circle(x,y,0,0,.36f),V.Lerp(col,ink,.38f));c.Solid((x,y)=>Box(x,y,0,0,.20f,.28f,.2f),col);c.Solid((x,y)=>Box(x,y,-.035f,.09f,.075f,.13f,.2f),cream);for(int i=0;i<4;i++){float a=i*Mathf.PI/2;c.Solid((x,y)=>Circle(x,y,(float)Math.Sin(a)*.51f,(float)Math.Cos(a)*.51f,.08f),cream);}if(id=="mat_elite"){c.Solid((x,y)=>Tri(x,y,-.34f,-.38f,-.43f,-.75f,-.08f,-.56f),V.Hex("#c75555"));c.Solid((x,y)=>Tri(x,y,.34f,-.38f,.43f,-.75f,.08f,-.56f),V.Hex("#c75555"));c.Solid((x,y)=>Box(x,y,0,0,.08f,.15f,.785f),ink);}
 if(soul){c.Solid((x,y)=>Box(x,y,0,-.05f,.04f,.3f,-.6f),ink);c.Solid((x,y)=>Box(x,y,.12f,.14f,.19f,.09f,-.6f),cream);}
 if(id=="mat_core_tree"){c.Solid((x,y)=>Box(x,y,-.12f,.03f,.07f,.2f,-.5f,.06f),V.Hex("#d0ff9c"));c.Solid((x,y)=>Box(x,y,.12f,.03f,.07f,.2f,.5f,.06f),V.Hex("#d0ff9c"));}
 if(id=="mat_core_golem"){c.Solid((x,y)=>Box(x,y,-.03f,.05f,.02f,.23f,.5f),ink);c.Solid((x,y)=>Box(x,y,.04f,-.10f,.02f,.18f,-.6f),ink);}
 if(id=="mat_core_sea"){c.Solid((x,y)=>Math.Min(Circle(x,y,0,-.1f,.12f),Tri(x,y,-.1f,-.04f,0,.24f,.1f,-.04f)),cream);}
 if(id=="mat_core_arena"){c.Solid((x,y)=>Box(x,y,0,0,.035f,.24f),cream);c.Solid((x,y)=>Box(x,y,0,.08f,.20f,.035f),cream);}
 if(id=="mat_core_canyon"){c.Solid((x,y)=>Tri(x,y,-.25f,-.15f,-.09f,.19f,.04f,-.15f),cream);c.Solid((x,y)=>Tri(x,y,.02f,-.18f,.14f,.10f,.28f,-.18f),cream);}
}
 else{float[] p={-.55f,-.28f,-.4f,.3f,-.08f,.62f,.39f,.38f,.58f,-.22f,.10f,-.55f};c.Solid((x,y)=>Poly(x,y,p)-.035f,ink);c.Solid((x,y)=>Poly(x,y,p),col);c.Solid((x,y)=>Tri(x,y,-.4f,.3f,-.08f,.62f,.04f,-.08f),cream,.75f);c.Solid((x,y)=>Tri(x,y,.04f,-.08f,.58f,-.22f,.1f,-.55f),V.Lerp(col,ink,.4f));if(hi)c.Solid((x,y)=>Box(x,y,.3f,.36f,.07f,.14f,.4f),cream);}
 return true;
}
}
}
