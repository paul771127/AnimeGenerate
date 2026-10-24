using UnityEngine;
namespace SpellDuel
{
    internal static class StoryHeadwear
    {
        static void Cord(OutfitArt.Builder b,Vector3[] path,float radius,Color color)
        {
            int start=b.V.Count,n=8;
            for(int j=0;j<path.Length;j++)
            {
                var direction=(path[Mathf.Min(j+1,path.Length-1)]-path[Mathf.Max(0,j-1)]).normalized;var side=Vector3.Cross(direction,Vector3.forward).normalized;var cross=Vector3.Cross(direction,side).normalized;
                for(int i=0;i<n;i++){float a=i*Mathf.PI/4;b.V.Add(path[j]+(side*Mathf.Cos(a)+cross*Mathf.Sin(a))*radius*(1-j*.28f));b.C.Add(color);if(j<path.Length-1){int k=start+j*n+i,k1=start+j*n+(i+1)%n;b.T.AddRange(new[]{k,k1,k+n,k1,k1+n,k+n});}}
            }
        }
        static void HoodRoof(OutfitArt.Builder b,Vector3[] front,Color color)
        {
            const int rows=7;int n=front.Length;
            for(int layer=0;layer<2;layer++)
            {
                int start=b.V.Count;
                for(int j=0;j<rows;j++)for(int i=0;i<n;i++)
                {
                    float t=j/(float)(rows-1),smooth=t*t*(3-2*t);var f=front[i];
                    float rearY=f.y>.10f?.10f+(f.y-.10f)*.45f:f.y-.006f;
                    var v=new Vector3(Mathf.Lerp(f.x,f.x*.98f,smooth),Mathf.Lerp(f.y,rearY,smooth)+.014f*Mathf.Sin(t*Mathf.PI)*Mathf.Clamp01((f.y+.01f)/.20f),Mathf.Lerp(.108f,-.176f,t));
                    v.y+=layer==0?.002f:-.002f;b.V.Add(v);b.C.Add(color);
                    if(j==rows-1||i==n-1)continue;int k=start+j*n+i;
                    if(layer==0)b.T.AddRange(new[]{k,k+1,k+n,k+1,k+n+1,k+n});else b.T.AddRange(new[]{k,k+n,k+1,k+1,k+n,k+n+1});
                }
                int center=b.V.Count;b.V.Add(new Vector3(0,.025f,layer==0?-.180f:-.174f));b.C.Add(color);
                for(int i=0;i<n-1;i++){int k=start+(rows-1)*n+i;if(layer==0)b.T.AddRange(new[]{center,k,k+1});else b.T.AddRange(new[]{center,k+1,k});}
            }
        }
        static void HoodRim(OutfitArt.Builder b,Vector3[] front,Color cloth,Color lining)
        {
            int n=front.Length;
            for(int layer=0;layer<2;layer++)
            {
                int start=b.V.Count;
                for(int row=0;row<3;row++)for(int i=0;i<n;i++)
                {
                    var f=front[i];float side=f.x<0?-1:1;
                    var v=row==0?f:row==1?new Vector3(side*Mathf.Max(0,Mathf.Abs(f.x)-.022f),f.y-.014f,.148f):new Vector3(side*Mathf.Max(0,Mathf.Abs(f.x)-.034f),f.y-.023f,.140f);
                    v.z+=layer==0?.001f:-.001f;b.V.Add(v);b.C.Add(row==2?lining:cloth);
                    if(row==2||i==n-1)continue;int k=start+row*n+i;
                    if(layer==0)b.T.AddRange(new[]{k,k+n,k+1,k+1,k+n,k+n+1});else b.T.AddRange(new[]{k,k+1,k+n,k+1,k+n+1,k+n});
                }
            }
        }
        // Final head-local coordinates, deliberately independent of the old fitting envelope.
        internal static void FilmBuild(OutfitArt.Builder b,string id)
        {
            var gold=CharacterLook.Hex("d8b25d");
            if(id=="艾琳")
            {
                var purple=CharacterLook.Hex("382354");var lining=CharacterLook.Hex("8959bd");
                // Dense Bezier aperture: rounded upper shoulders and a soft central peak.
                const int arc=18,tail=5;var half=new Vector3[arc+tail+1];
                for(int i=0;i<=arc;i++){float t=i/(float)arc,u=1-t;half[i]=new Vector3(0,.240f,.108f)*(u*u*u)+new Vector3(.060f,.238f,.108f)*(3*u*u*t)+new Vector3(.135f,.075f,.108f)*(3*u*t*t)+new Vector3(.172f,-.115f,.108f)*(t*t*t);}
                for(int i=1;i<=tail;i++){float t=i/(float)tail,u=1-t;half[arc+i]=new Vector3(.172f,-.115f,.108f)*(u*u*u)+new Vector3(.180f,-.140f,.108f)*(3*u*u*t)+new Vector3(.140f,-.153f,.108f)*(3*u*t*t)+new Vector3(.115f,-.145f,.108f)*(t*t*t);}
                var front=new Vector3[half.Length*2-1];
                for(int i=0;i<half.Length;i++){front[half.Length-1+i]=half[i];var v=half[i];v.x=-v.x;front[half.Length-1-i]=v;}
                HoodRoof(b,front,purple);HoodRim(b,front,purple,lining);
                for(int i=0;i<front.Length-1;i++)
                {
                    var a=front[i];var c=front[i+1];
                    b.Seam(a+Vector3.forward*.005f,c+Vector3.forward*.005f,.003f,gold);
                }
                return;
            }
            var cream=CharacterLook.Hex("fff2d8");var ink=CharacterLook.Hex("222d40");
            b.Tube(new[]{new Vector3(0,.163f,-.014f),new Vector3(0,.153f,-.012f),new Vector3(0,.126f,-.006f),new Vector3(0,.093f,0)},new[]{.025f,.100f,.146f,.137f},new[]{.023f,.090f,.127f,.119f},cream,24);
            b.Tube(new[]{new Vector3(0,.098f,0),new Vector3(0,.076f,0)},new[]{.138f,.134f},new[]{.120f,.118f},ink,24);
            b.Tube(new[]{new Vector3(0,.084f,0),new Vector3(0,.080f,0)},new[]{.139f,.138f},new[]{.122f,.122f},gold,24);
            for(int i=0;i<16;i++)
            {
                float a=-Mathf.PI*.5f+i*Mathf.PI/16,c=-Mathf.PI*.5f+(i+1)*Mathf.PI/16;
                b.Panel(new Vector3(Mathf.Sin(a)*.132f,.077f,Mathf.Cos(a)*.110f),new Vector3(Mathf.Sin(c)*.132f,.077f,Mathf.Cos(c)*.110f),new Vector3(Mathf.Sin(a)*.144f,.065f,.030f+Mathf.Cos(a)*.151f),new Vector3(Mathf.Sin(c)*.144f,.065f,.030f+Mathf.Cos(c)*.151f),ink,.002f,1,1);
            }
            // Naval badge: shield, anchor stem, ring and curved flukes, rather than the old cross.
            b.Loft(new[]{.139f,.130f,.108f,.100f},new[]{.004f,.015f,.014f,.003f},new[]{.004f,.005f,.005f,.003f},gold,0,.126f,10);
            b.Seam(new Vector3(0,.129f,.136f),new Vector3(0,.109f,.139f),.0025f,ink);
            b.Seam(new Vector3(-.009f,.116f,.138f),new Vector3(0,.108f,.139f),.0025f,ink);
            b.Seam(new Vector3(0,.108f,.139f),new Vector3(.009f,.116f,.138f),.0025f,ink);
        }
        internal static bool Build(OutfitArt.Builder b,string id,Portraits.Persona p,float seat,float radius)
        {
            var gold=CharacterLook.Hex("d8b25d");
            if(id=="艾琳")
            {
                // Open face aperture. The cap surrounds the bangs rather than covering eyes or nose.
                var purple=CharacterLook.Hex("382354");var lining=CharacterLook.Hex("8959bd");
                const int n=18;for(int i=0;i<n;i++)
                {
                    float a=-.30f+i*(Mathf.PI+.60f)/n,c=-.30f+(i+1)*(Mathf.PI+.60f)/n;
                    var outerA=new Vector3(Mathf.Cos(a)*.214f,Mathf.Sin(a)*.258f-.025f+Mathf.Pow(Mathf.Max(0,Mathf.Sin(a)),12)*.045f,.095f);
                    var outerB=new Vector3(Mathf.Cos(c)*.214f,Mathf.Sin(c)*.258f-.025f+Mathf.Pow(Mathf.Max(0,Mathf.Sin(c)),12)*.045f,.095f);
                    var innerA=new Vector3(Mathf.Cos(a)*.181f,Mathf.Sin(a)*.224f-.035f,.119f);
                    var innerB=new Vector3(Mathf.Cos(c)*.181f,Mathf.Sin(c)*.224f-.035f,.119f);
                    var backA=new Vector3(outerA.x*.77f,outerA.y*.90f,-.15f);var backB=new Vector3(outerB.x*.77f,outerB.y*.90f,-.15f);
                    b.Panel(outerA,outerB,backA,backB,purple,.004f,2,1);
                    b.Panel(outerA,outerB,innerA,innerB,purple,0,1,1);
                    b.Seam(innerA+new Vector3(0,0,.004f),innerB+new Vector3(0,0,.004f),.010f,lining);
                    b.Seam(outerA+new Vector3(0,0,.008f),outerB+new Vector3(0,0,.008f),.005f,gold);
                    b.Panel(backA,backB,new Vector3(0,.04f,-.18f),new Vector3(0,.04f,-.18f),purple,0,1,1);
                }
                return true;
            }
            if(id=="翠")
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var root=new Vector3(0,.175f,.07f);var bend=new Vector3(side*.14f,.205f,.04f);var tip=new Vector3(side*.29f,.238f,-.005f);
                    Cord(b,new[]{root,bend,tip},.012f,gold);
                    Cord(b,new[]{bend,new Vector3(side*.22f,.18f,.025f),new Vector3(side*.32f,.20f,-.005f)},.010f,gold);
                    b.Seam(new Vector3(side*.04f,.18f,.083f),new Vector3(side*.075f,.26f,.07f),.009f,gold);
                }
                b.Loft(new[]{.267f,.235f,.19f},new[]{.002f,.033f,.008f},new[]{.004f,.013f,.005f},CharacterLook.Hex("9ebd65"),0,.085f,8);
                return true;
            }
            if(id=="小葉")
            {
                var red=CharacterLook.Hex("c82e3d");
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side*.175f;
                    b.Panel(new Vector3(x,.11f,.07f),new Vector3(x-side*.075f,.23f,.045f),new Vector3(x+side*.015f,.09f,.075f),new Vector3(x-side*.09f,.13f,.06f),red,.004f,2,2);
                    b.Panel(new Vector3(x,.11f,.07f),new Vector3(x+side*.09f,.20f,.04f),new Vector3(x+side*.015f,.09f,.075f),new Vector3(x+side*.09f,.09f,.06f),red,.004f,2,2);
                    b.Loft(new[]{.125f,.105f,.085f},new[]{.013f,.021f,.013f},new[]{.009f,.015f,.009f},red,x,.085f,8);
                }
                return true;
            }
            return false;
        }
    }
}
