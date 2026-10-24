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
        // Final head-local coordinates, deliberately independent of the old fitting envelope.
        internal static void FilmBuild(OutfitArt.Builder b,string id)
        {
            var gold=CharacterLook.Hex("d8b25d");
            if(id=="艾琳")
            {
                var purple=CharacterLook.Hex("382354");var lining=CharacterLook.Hex("8959bd");
                float[] xs={0,.055f,.105f,.137f,.160f,.172f,.160f,.115f};
                float[] ys={.245f,.204f,.130f,.040f,-.050f,-.115f,-.135f,-.145f};
                for(int side=-1;side<=1;side+=2)for(int i=0;i<xs.Length-1;i++)
                {
                    var a=new Vector3(side*xs[i],ys[i],.108f);var c=new Vector3(side*xs[i+1],ys[i+1],.108f);
                    var ia=new Vector3(side*Mathf.Max(0,xs[i]-.018f),ys[i]-.019f,.128f);var ic=new Vector3(side*Mathf.Max(0,xs[i+1]-.018f),ys[i+1]-.019f,.128f);
                    var ba=new Vector3(a.x*.84f,a.y>.10f?.10f+(a.y-.10f)*.45f:a.y-.008f,-.205f);var bc=new Vector3(c.x*.84f,c.y>.10f?.10f+(c.y-.10f)*.45f:c.y-.008f,-.205f);
                    b.Panel(a,c,ba,bc,purple,.003f,2,1);b.Panel(a,c,ia,ic,purple,0,1,1);
                    b.Seam(ia,ic,.006f,lining);b.Seam(a+Vector3.forward*.005f,c+Vector3.forward*.005f,.003f,gold);
                    b.Panel(ba,bc,new Vector3(0,.035f,-.217f),new Vector3(0,.035f,-.217f),purple,0,1,1);
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
