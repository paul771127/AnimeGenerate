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
