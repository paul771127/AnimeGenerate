using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Volumetric, closed, cubic-swept locks. NPCs only; the first 121 vertices remain the fitted hair shell.
    internal static class StoryHair
    {
        internal static bool Has(string id)=>StoryCostume.Has(id)||id=="澪";
        static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t){float s=1-t;return a*(s*s*s)+b*(3*s*s*t)+c*(3*s*t*t)+d*(t*t*t);}
        static void Lock(OutfitArt.Builder m,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float width,float depth,float tone=1)
        {
            int start=m.V.Count;const int rows=10,sides=8;
            for(int j=0;j<=rows;j++)
            {
                float t=j/(float)rows;var at=Curve(a,b,c,d,t);var direction=(Curve(a,b,c,d,Mathf.Min(1,t+.008f))-Curve(a,b,c,d,Mathf.Max(0,t-.008f))).normalized;
                var right=Vector3.Cross(direction,Vector3.forward).normalized;if(right.sqrMagnitude<.1f)right=Vector3.right;var normal=Vector3.Cross(right,direction).normalized;
                float taper=Mathf.Pow(1-t,.48f)*(.76f+.24f*Mathf.Sin(t*Mathf.PI))+.004f;
                for(int i=0;i<sides;i++)
                {
                    float q=i*Mathf.PI/4;var n=right*Mathf.Cos(q)+normal*Mathf.Sin(q);
                    m.V.Add(at+right*(Mathf.Cos(q)*width*taper)+normal*(Mathf.Sin(q)*depth*taper));
                    float shade=tone*(n.z<-.2f?.52f:n.y>.15f?1.07f:.86f);m.C.Add(new Color(shade,shade,shade,1));
                    if(j<rows){int k=start+j*sides+i,nn=start+j*sides+(i+1)%sides;m.T.AddRange(new[]{k,k+sides,nn,nn,k+sides,nn+sides});}
                }
            }
            for(int cap=0;cap<2;cap++){int k=start+(cap==0?0:rows*sides);for(int i=1;i<sides-1;i++)m.T.AddRange(cap==0?new[]{k,k+i,k+i+1}:new[]{k,k+i+1,k+i});}
        }
        internal static Mesh Build(string id,Portraits.Persona p)
        {
            var m=new OutfitArt.Builder();var look=CharacterLook.Get(id);bool bob=id=="艾琳"||id=="凪",longHair=id=="翠"||id=="澪",twins=id=="小葉",shortHair=id=="海斗"||id=="小鐵"||id=="老焰";
            const int sides=20,rows=6;m.V.Add(new Vector3(0,.177f*p.Length,-.009f));m.C.Add(new Color(.68f,.68f,.68f,1));
            for(int j=1;j<=rows;j++)for(int i=0;i<sides;i++)
            {
                float phi=i*2*Mathf.PI/sides,front=Mathf.Cos(phi),theta=j/(float)rows*(front>.3f?1.06f:1.90f);
                m.V.Add(new Vector3(Mathf.Sin(theta)*Mathf.Sin(phi)*.158f*p.Width,Mathf.Cos(theta)*.177f*p.Length,Mathf.Sin(theta)*front*.159f-.009f));m.C.Add(new Color(.69f,.69f,.69f,1));
                int n=(i+1)%sides;if(j==1)m.T.AddRange(new[]{0,1+i,1+n});else{int k=1+(j-2)*sides+i,nn=1+(j-2)*sides+n;m.T.AddRange(new[]{k,k+sides,nn,nn,k+sides,nn+sides});}
            }
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,x=Mathf.Sin(a),z=Mathf.Cos(a);bool spike=id=="小鐵";
                Lock(m,new Vector3(x*.03f,.16f,z*.025f),new Vector3(x*.10f,spike?.26f:.19f,z*.10f),new Vector3(x*.15f,spike?.23f:.13f,z*.155f),new Vector3(x*(spike?.22f:.16f),spike?.20f:.065f,z*.15f),.042f,.025f,i%3==0?.86f:1);
            }
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.064f,y=(id=="老焰"?.105f:.038f)+(i%2)*.012f;float root=look.Part*.035f;
                Lock(m,new Vector3(root+x*.22f,.155f,.06f),new Vector3(x*.6f,.135f,.17f),new Vector3(x,.075f,.177f),new Vector3(x+(i<2?-.012f:.012f),y,AnimeFace.Surface(x,y,p)+.012f),.041f,.021f,i%2==0?.96f:1.02f);
            }
            for(int side=-1;side<=1;side+=2)for(int tier=0;tier<2;tier++)
            {
                float len=longHair?look.Length:bob?(id=="艾琳"?.20f:.16f):.035f,front=tier==0?.15f:-.02f;
                Lock(m,new Vector3(side*.135f,.09f,front*.4f),new Vector3(side*.187f,-.01f,front),new Vector3(side*(bob?.21f:shortHair?.15f:.18f),-len*.82f,front),new Vector3(side*(bob?.23f:shortHair?.145f:.165f),-len+(bob?.035f:0),front),.044f,.027f,tier==0?1:.78f);
            }
            if(twins)
            {
                for(int side=-1;side<=1;side+=2)for(int k=0;k<5;k++)
                    Lock(m,new Vector3(side*.16f,.125f,-.07f-k*.008f),new Vector3(side*(.34f+k*.008f),.08f,-.16f),new Vector3(side*(.36f+k*.007f),-.38f,-.16f+k*.012f),new Vector3(side*(.23f+k*.018f),-.50f+k*.015f,-.06f+k*.012f),.068f,.042f,k%2==0?.95f:.83f);
            }
            else for(int i=0;i<8;i++)
            {
                float a=(i/7f-.5f)*2.6f,x=Mathf.Sin(a)*.14f,z=-Mathf.Cos(a)*.151f,len=longHair?look.Length:bob?(id=="艾琳"?.20f:.16f):.045f;
                Lock(m,new Vector3(x,.065f,z),new Vector3(x*(id=="翠"?2.4f:1.16f),-len*.25f,z-.035f),new Vector3(x*(id=="翠"?3.0f:bob?1.40f:1.13f),-len*(id=="翠"?.65f:1),z-.018f),new Vector3(x*(id=="翠"?1.75f:bob?1.48f:1.10f),-len+(bob?.035f:0),z*.88f),.046f,.030f,i%3==0?.82f:1);
            }
            if(id=="老焰")for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Lock(m,new Vector3(0,.15f,-.06f),new Vector3(Mathf.Cos(a)*.07f,.30f,-.04f),new Vector3(Mathf.Cos(a)*.07f,.30f,.015f),new Vector3(0,.18f,.025f),.031f,.023f);}
            return m.Finish("Volume story hair "+id);
        }
    }
}
