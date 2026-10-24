using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Paint once per identity/glance. Texture coordinates are shared by portraits and curved 3D eye patches.
    internal static class StoryEyes
    {
        static readonly Dictionary<string,Color32[]> cache=new Dictionary<string,Color32[]>();
        internal static Color32[] Render(string id,int side,int glance)
        {
            string key=id+side+"/"+glance;if(cache.TryGetValue(key,out var image))return image;
            image=new Color32[EyeArt.W*EyeArt.H];var look=CharacterLook.Get(id);var ink=CharacterLook.Hex(id=="澪"?"52667c":"281529");
            bool closed=id=="澪";float opening=id=="翠"?.26f:id=="老焰"?.18f:id=="海斗"?.36f:id=="小鐵"?.44f:.52f;
            var p=Portraits.PersonaFor(id);var lashes=EyeArt.LashTriangles(opening,look.Tail*.10f,p.Masculine||p.Elder,closed);
            for(int py=0;py<EyeArt.H;py++)for(int px=0;px<EyeArt.W;px++)
            {
                Color sum=new Color(0,0,0,0);float weight=0;
                for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)
                {
                    float x=(px+(sx+.5f)*.5f)/EyeArt.W*2-1,y=(py+(sy+.5f)*.5f)/EyeArt.H*2-1,u=x/.72f;
                    float arch=Mathf.Sqrt(Mathf.Max(0,1-u*u)),slope=look.Tail*.10f*side*u,upper=opening*arch+slope,lower=-.37f*arch+slope;
                    Color c=new Color(0,0,0,0);
                    if(closed)
                    {
                        if(Mathf.Abs(u)<1&&Mathf.Abs(y-(-.02f-.055f*side*u))<.035f*(1-.5f*Mathf.Abs(u)))c=ink;
                    }
                    else
                    {
                        if(Mathf.Abs(u)<1&&y<upper&&y>lower)
                        {
                            c=Color.Lerp(new Color(1,.98f,.96f),new Color(.83f,.85f,.91f),Mathf.Clamp01((y-lower)/(upper-lower))*.30f);
                            float gaze=glance*.07f,dx=x-gaze,dy=y-.23f,d=dx*dx/(.42f*.42f)+dy*dy/(.63f*.63f);
                            if(d<1)
                            {
                                float gradient=Mathf.Clamp01((.77f-y)/1.05f);c=Color.Lerp(look.Iris*.24f,Color.Lerp(look.Iris,Color.white,.22f),gradient);c.a=1;
                                if(d>.87f)c=Color.Lerp(c,ink,.58f);
                                if(dx*dx/(.09f*.09f)+(y-.25f)*(y-.25f)/(.29f*.29f)<1)c=ink;
                                if((dx+.13f)*(dx+.13f)/(.083f*.083f)+(y-.27f)*(y-.27f)/(.072f*.072f)<1||(dx-.15f)*(dx-.15f)/(.030f*.030f)+(y+.17f)*(y+.17f)/(.027f*.027f)<1)c=Color.white;
                            }
                        }
                        float outward=side*u,thick=.13f*(.35f+.65f*arch);
                        float t=(side*x-.66f)/.17f;
                        if(Mathf.Abs(u)<1&&Mathf.Abs(y-upper)<thick*.5f||t>=0&&t<1&&Mathf.Abs(y-(.03f+t*.12f))<.073f*(1-t))c=ink;
                        if(Mathf.Abs(u)<.95f&&outward>.55f&&Mathf.Abs(y-lower)<.015f)c=Color.Lerp(ink,look.Iris,.25f);
                        // The outer lash is a tapering wedge, never a complete lower outline.
                        if(outward>.65f&&outward<1.04f&&y>upper&&y<upper+.045f*(1-(outward-.65f)/.39f))c=ink;
                    }
                    if(EyeArt.OnLashes(x,y,side,lashes))c=ink;
                    sum.r+=c.r*c.a;sum.g+=c.g*c.a;sum.b+=c.b*c.a;weight+=c.a;
                }
                if(weight>0)image[py*EyeArt.W+px]=(Color32)new Color(sum.r/weight,sum.g/weight,sum.b/weight,weight*.25f);
            }
            cache[key]=image;return image;
        }
    }
}
