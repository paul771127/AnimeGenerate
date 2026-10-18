using UnityEngine;
namespace SpellDuel
{
    // Keep numeric equations in sync with the SD_HUMAN face block in Toon.shader.
    // Horizontal angle <=60 degrees has no cheek shadow; transition is 0.20 UV wide.
    internal static class HumanFaceLight
    {
        internal static float Smooth(float a,float b,float x){float t=Mathf.Clamp01((x-a)/(b-a));return t*t*(3-2*t);}
        internal static float Occlusion(float u,float v,float front,float side,float part,float wave,float fringeEdge)
        {
            float horizontal=Mathf.Max(.001f,Mathf.Sqrt(front*front+side*side));
            float gate=1-Smooth(.22f,.50f,front/horizontal);
            float x=(u-.5f)*(side<0?2:-2),curve=.20f+.80f*(v-.46f)*(v-.46f);
            float cheek=Smooth(curve-.20f,curve+.20f,x)*gate;
            float nx=(u-.498f)/.028f,ny=(v-.267f)/.012f;
            float nose=1-Smooth(.35f,1,nx*nx+ny*ny);
            float edge=fringeEdge+.028f*Mathf.Sin(u*31+wave)+.020f*Mathf.Sin(u*15+part*4);
            float fringe=Smooth(edge-.02f,edge+.06f,v)*(1-Smooth(.87f,.94f,v));
            float chin=(1-Smooth(0,.06f,v))*.45f;
            return Mathf.Max(Mathf.Max(cheek,nose*.24f),Mathf.Max(fringe*.32f,chin));
        }
        internal static float Fringe(CharacterLook.Look look)=>look.Hair==CharacterLook.HairKind.Crew||look.Hair==CharacterLook.HairKind.Slick?.82f:look.Hair==CharacterLook.HairKind.Bob?.65f:.71f;
    }
}
