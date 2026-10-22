using UnityEngine;
namespace SpellDuel
{
    // Shared resident measurements; does not change actor roots, dialogue or interaction.
    internal static class StoryFigure
    {
        internal static bool Girl(string id)=>id=="艾琳"||id=="凪"||id=="翠"||id=="小葉"||id=="米菈"||id=="店員";
        internal static bool LongLegs(string id)=>id=="艾琳"||id=="凪"||id=="小葉";
        internal static float ShoulderX(string id)=>id=="鐵匠"?.205f:id=="小葉"?.16f:id=="小鐵"?.14f:id=="翠"?.12f:(id=="米菈"||id=="店員")?.118f:Girl(id)?.110f:.15f;
        internal static float ShoulderY(string id)=>id=="小葉"?1.35f:id=="翠"?1.375f:id=="鐵匠"?1.345f:1.37f;
        internal static float TorsoY(string id)=>LongLegs(id)?.90f:.78f;
        internal static float HipY(string id)=>LongLegs(id)?.99f:.85f;
        internal static float Thigh(string id)=>LongLegs(id)?.465f:.42f;
        internal static void Tailor(OutfitArt.Builder b,string id,string part)
        {
            bool girl=Girl(id),longLegs=LongLegs(id);
            for(int i=0;i<b.V.Count;i++)
            {
                var v=b.V[i];
                if(part=="torso")
                {
                    float waist=1-Mathf.Clamp01(Mathf.Abs(v.y-.16f)/.26f);
                    float fit=girl?Mathf.Lerp(.87f,.73f,waist):id=="鐵匠"?1:.94f;
                    v.x*=fit;v.z*=girl?Mathf.Lerp(.96f,.84f,waist):1;
                    if(longLegs)v.y*=v.y>=0?.775f:id=="小葉"?.83f:1.10f;
                    else if(v.y>.49f)v.y=.49f+(v.y-.49f)*.86f;
                    // Smooth clavicle envelope: no top shelf at the arm join.
                    if(v.y>(longLegs?.39f:.52f)&&Mathf.Abs(v.x)>.055f)
                    {
                        float cap=(longLegs?.55f:.69f)-(Mathf.Abs(v.x)-.055f)*.51f;
                        v.y=Mathf.Min(v.y,cap);
                    }
                }
                else if(part.StartsWith("upper"))
                {
                    if(id=="凪"&&b.C[i].a<.25f&&v.y<-.025f&&v.y>-.19f){v.x*=.62f;v.z*=.62f;}
                    float scale=girl?.78f:id=="鐵匠"?1:.84f;v.x*=scale;v.z*=scale;
                    // Dome the proximal cap; the sleeve volume stays below the shoulder.
                    if(v.y>-.03f)v.y-=.018f*Mathf.Clamp01((v.y+.03f)/.07f);
                }
                else if(part.StartsWith("fore")){v.x*=girl?.83f:1;v.z*=girl?.88f:1;}
                else if(longLegs&&part.StartsWith("thigh")){v.y*=.465f/.42f;v.x*=.86f;v.z*=.91f;}
                else if(longLegs&&part.StartsWith("calf")){v.y*=.525f/.43f;v.x*=.86f;v.z*=.94f;}
                b.V[i]=v;
            }
            b.ClearSmoothNormals();
        }
    }
}
