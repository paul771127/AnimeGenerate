using UnityEngine;
namespace SpellDuel
{
    // Reference costumes are NPC-only. Geometry follows the existing resident joints.
    internal static class StoryCostume
    {
        static Color C(string h)=>CharacterLook.Hex(h);
        internal static bool Has(string id)=>id=="艾琳"||id=="凪"||id=="老焰"||id=="海斗"||id=="小鐵"||id=="翠"||id=="小葉";
        internal static bool Build(OutfitArt.Builder b,string id,string part,Color skin)
        {
            if(!Has(id))return false;
            if(part.StartsWith("hand"))
            {
                if(id!="小鐵")return false;
                b.Loft(new[]{.012f,-.015f,-.052f,-.091f,-.110f},new[]{.027f,.038f,.040f,.033f,.023f},new[]{.023f,.026f,.027f,.024f,.016f},skin,0,.008f,12);
                b.Loft(new[]{.015f,-.02f,-.062f},new[]{.032f,.042f,.042f},new[]{.028f,.032f,.031f},C("253a4e"),0,.008f,12);
                float side=part.EndsWith("L")?-1:1;b.Tube(new[]{new Vector3(side*.026f,-.018f,.012f),new Vector3(side*.047f,-.040f,.025f),new Vector3(side*.042f,-.064f,.035f)},new[]{.018f,.017f,.012f},new[]{.017f,.016f,.011f},skin,10);return true;
            }
            Color white=C("fff5e8"),black=C("252333"),gold=C("dcb45e"),main=OutfitArt.Get(id).Main;
            if(part=="torso")
            {
                Color inner=id=="艾琳"||id=="小鐵"?white:id=="老焰"?black:main;
                b.Loft(new[]{.71f,.68f,.64f,.58f,.49f,.35f,.19f,.09f,0f},new[]{.055f,.080f,.146f,.19f,.195f,.171f,.145f,.157f,.164f},new[]{.052f,.066f,.084f,.105f,.128f,.118f,.105f,.112f,.116f},inner,0,0,16);
                if(id=="艾琳")
                {
                    Skirt(b,-.025f,white,.174f);OpenCoat(b,.56f,main,gold,.35f,CharacterLook.Hex("8959bd"));
                    for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++){float x=side*(.145f+i*.012f);b.Seam(new Vector3(x,-.51f,.105f),new Vector3(x+side*.012f,-.45f,.105f),.003f,gold);}
                    Belt(b,.15f,C("785139"),.012f);Belt(b,.075f,C("785139"),.012f);
                    b.Loft(new[]{.712f,.68f},new[]{.058f,.065f},new[]{.058f,.067f},black,0,0,12);
                    b.Badge(0,.48f,.136f,.032f,.040f,gold);
                }
                else if(id=="凪")
                {
                    Skirt(b,-.55f,main,.36f);Frill(b,-.55f,.361f,white);
                    Apron(b,white);
                    for(int s=-1;s<=1;s+=2){b.Seam(new Vector3(s*.105f,.58f,.125f),new Vector3(s*.09f,.11f,.134f),.014f,white);b.Panel(new Vector3(s*.10f,.64f,.098f),new Vector3(0,.60f,.13f),new Vector3(s*.12f,.53f,.139f),new Vector3(s*.025f,.57f,.139f),white);}
                    Bow(b,0,.55f,.155f,.044f,black);
                }
                else if(id=="老焰")
                {
                    OpenCoat(b,.48f,main,main,.30f);CrossCollar(b,black,C("4c4140"));Belt(b,.12f,main,.04f);
                    b.Panel(new Vector3(.03f,.10f,.143f),new Vector3(.065f,.10f,.143f),new Vector3(.065f,-.24f,.15f),new Vector3(.10f,-.23f,.15f),main);
                }
                else if(id=="海斗")
                {
                    CrossCollar(b,C("286fc0"),C("163d73"));
                    for(int s=-1;s<=1;s+=2)for(int i=0;i<3;i++)b.Loft(new[]{.43f-i*.115f,.418f-i*.115f},new[]{.012f,.012f},new[]{.008f,.008f},gold,s*.065f,.139f,8);

                }
                else if(id=="小鐵")
                {
                    var leather=C("966537");
                    b.Panel(new Vector3(-.105f,.45f,.139f),new Vector3(.105f,.45f,.139f),new Vector3(-.16f,.02f,.128f),new Vector3(.16f,.02f,.128f),leather,.008f,5,4);
                    for(int s=-1;s<=1;s+=2){b.Seam(new Vector3(s*.115f,.63f,.086f),new Vector3(s*.09f,.32f,.145f),.018f,leather);b.Badge(s*.09f,.43f,.151f,.009f,.009f,gold);}
                    Belt(b,.045f,black,.018f);
                    b.Loft(new[]{.065f,.04f,-.075f,-.09f},new[]{.042f,.050f,.050f,.038f},new[]{.033f,.040f,.040f,.028f},leather,.195f,.015f,10);b.Badge(.195f,.01f,.061f,.018f,.024f,gold);
                    b.Panel(new Vector3(-.10f,.67f,.103f),new Vector3(.10f,.67f,.103f),new Vector3(0,.45f,.151f),new Vector3(.015f,.45f,.151f),C("bf3434"));
                }
                else if(id=="翠")
                {
                    Skirt(b,-.65f,main,.26f);OpenCoat(b,.65f,main,C("b4d39a"),.55f,C("d7e6c2"));CrossCollar(b,white,C("b6cfab"));Belt(b,.14f,C("245b43"),.08f);b.Badge(0,.15f,.142f,.038f,.032f,gold);
                    Vine(b,-.58f,.185f,.137f,C("b4d39a"));
                }
                else if(id=="小葉")
                {
                    Skirt(b,-.27f,main,.28f);Frill(b,-.27f,.28f,white);Belt(b,.10f,black,.019f);Bow(b,0,.10f,.139f,.035f,black);
                    for(int i=0;i<5;i++)b.Badge(-.12f+i*.06f,-.23f,.143f,.014f,.020f,gold);
                }
                return true;
            }
            bool upper=part.StartsWith("upper"),fore=part.StartsWith("fore");
            if(upper||fore)
            {
                bool shortSleeve=id=="凪"||id=="小鐵";
                if(shortSleeve)
                {
                    b.Loft(upper?new[]{.04f,0f,-.06f,-.13f,-.17f,-.23f,-.295f}:new[]{.025f,-.02f,-.10f,-.20f,-.26f},upper?new[]{.036f,.060f,.072f,.067f,.056f,.044f,.037f}:new[]{.039f,.043f,.053f,.033f,.030f},upper?new[]{.035f,.058f,.068f,.064f,.053f,.042f,.037f}:new[]{.037f,.041f,.050f,.029f,.026f},skin,0,0,12);
                    if(upper)b.Loft(new[]{.005f,-.025f,-.075f,-.13f,-.17f,-.185f},new[]{.018f,.041f,.063f,.067f,.050f,.039f},new[]{.019f,.041f,.062f,.066f,.048f,.038f},white,0,0,12);
                    return true;
                }
                if(id=="小葉"&&upper)
                {
                    b.Loft(new[]{.04f,0f,-.09f},new[]{.038f,.057f,.058f},new[]{.037f,.054f,.055f},skin,0,0,12);
                    b.Loft(new[]{-.08f,-.12f,-.19f,-.27f,-.295f},new[]{.060f,.075f,.098f,.080f,.053f},new[]{.058f,.072f,.093f,.076f,.052f},white,0,0,12);return true;
                }
                Color cloth=id=="小葉"?white:main;float wide=id=="翠"?.115f:id=="老焰"?.090f:.060f;
                b.Loft(upper?new[]{.04f,0f,-.06f,-.16f,-.25f,-.295f}:new[]{.025f,-.02f,-.10f,-.20f,-.26f},upper?new[]{.040f,.066f,wide,wide,.060f,.055f}:new[]{.053f,.061f,wide,wide,.063f},upper?new[]{.038f,.063f,wide*.95f,wide*.95f,.058f,.052f}:new[]{.051f,.059f,wide*.95f,wide*.95f,.060f},cloth,0,0,12);
                if(id=="翠"&&upper)
                {
                    // Sleeve bag hangs behind the wrist: hand remains outside its opening.
                    b.Panel(new Vector3(-.075f,-.08f,-.035f),new Vector3(.075f,-.08f,-.035f),new Vector3(-.27f,-1.12f,-.025f),new Vector3(.27f,-1.12f,-.025f),main,.025f,8,5);
                    Vine(b,-1.03f,.13f,.013f,C("b4d39a"));
                }
                if(upper&&id=="海斗")b.Loft(new[]{-.014f,-.035f,-.065f},new[]{.029f,.055f,.064f},new[]{.041f,.060f,.065f},gold,0,0,12);
                if(fore&&(id=="艾琳"||id=="海斗"))for(int i=0;i<(id=="海斗"?2:1);i++)b.Loft(new[]{-.20f+i*.03f,-.22f+i*.03f},new[]{.069f,.068f},new[]{.066f,.065f},gold,0,0,12);
                return true;
            }
            if(part.StartsWith("thigh"))
            {
                Color pants=id=="海斗"?white:id=="小鐵"?C("966537"):id=="小葉"?white:id=="翠"||id=="凪"?skin:black;
                b.Loft(new[]{.075f,.015f,-.10f,-.24f,-.36f,-.435f},id=="老焰"?new[]{.09f,.12f,.14f,.145f,.11f,.075f}:new[]{.076f,.087f,.084f,.073f,.058f,.053f},id=="老焰"?new[]{.09f,.12f,.13f,.135f,.10f,.07f}:new[]{.078f,.087f,.084f,.072f,.057f,.052f},pants,0,0,12);
                if(id=="艾琳"){b.Loft(new[]{.077f,.015f,-.10f,-.145f},new[]{.078f,.089f,.086f,.080f},new[]{.080f,.089f,.086f,.080f},skin,0,0,12);}
                if(id=="艾琳")b.Loft(new[]{-.15f,-.175f},new[]{.081f,.080f},new[]{.081f,.080f},gold,0,0,12);
                return true;
            }
            if(part.StartsWith("calf"))
            {
                Color sock=id=="海斗"||id=="小葉"||id=="老焰"?white:id=="艾琳"?black:skin;
                b.Loft(new[]{.023f,-.025f,-.10f,-.18f,-.27f,-.355f},new[]{.054f,.057f,.069f,.064f,.045f,.037f},new[]{.052f,.055f,.068f,.062f,.043f,.037f},sock,0,0,12);
                if(id=="小鐵")b.Loft(new[]{.025f,-.05f,-.075f},new[]{.062f,.063f,.060f},new[]{.060f,.060f,.059f},C("c5af8f"),0,0,12);
                Color shoe=id=="艾琳"||id=="小鐵"?C("694835"):id=="凪"?C("702c40"):id=="老焰"?white:id=="翠"?skin:black;
                float boot=id=="海斗"||id=="小鐵"||id=="艾琳"?-.23f:-.36f;
                b.Tube(new[]{new Vector3(0,boot,0),new Vector3(0,-.37f,.025f),new Vector3(0,-.414f,.040f),new Vector3(0,-.43f,.040f)},new[]{.045f,.060f,.068f,.068f},new[]{.047f,.093f,.120f,.120f},shoe,12);
                if(id=="老焰"||id=="翠")
                {
                    b.Tube(new[]{new Vector3(0,-.418f,.04f),new Vector3(0,-.43f,.04f)},new[]{.069f,.069f},new[]{.121f,.121f},black,12);
                    b.Seam(new Vector3(-.04f,-.383f,.035f),new Vector3(0,-.386f,.125f),.012f,black);b.Seam(new Vector3(.04f,-.383f,.035f),new Vector3(0,-.386f,.125f),.012f,black);
                }
                if(id=="小葉")for(int i=0;i<2;i++)b.Badge(0,-.09f-i*.05f,.066f,.013f,.014f,gold);
                if(id=="小鐵")for(int i=0;i<3;i++)b.Seam(new Vector3(-.027f,-.29f-i*.03f,.052f),new Vector3(.027f,-.29f-i*.03f,.052f),.003f,white);
                return true;
            }
            return false;
        }
        static void Apron(OutfitArt.Builder b,Color c)
        {
            const int rows=12,cols=12;int start=b.V.Count;
            for(int j=0;j<=rows;j++)for(int i=0;i<=cols;i++)
            {
                float t=j/(float)rows,y=Mathf.Lerp(.53f,-.345f,t),s=Mathf.Clamp01((.025f-y)/.575f),smooth=s*s*(3-2*s),r=Mathf.Lerp(.166f,.36f,smooth),d=Mathf.Lerp(.119f,.252f,s),x=(i/(float)cols*2-1)*Mathf.Lerp(.085f,.29f,t);
                float z=y>0?.14f:Mathf.Sqrt(Mathf.Max(0,1-x*x/(r*r)))*d+.012f;
                b.V.Add(new Vector3(x,y+(j==rows?Mathf.Cos(i*Mathf.PI)*.006f:0),z));b.C.Add(c);
                if(j<rows&&i<cols){int k=start+j*(cols+1)+i;b.T.AddRange(new[]{k,k+cols+1,k+1,k+1,k+cols+1,k+cols+2});}
            }
        }
        static void Belt(OutfitArt.Builder b,float y,Color c,float half)=>b.Loft(new[]{y+half,y-half},new[]{.168f,.170f},new[]{.127f,.128f},c,0,0,16);
        static void Skirt(OutfitArt.Builder b,float end,Color c,float radius)
        {
            const int rows=8,sides=32;int start=b.V.Count;
            for(int j=0;j<=rows;j++)for(int i=0;i<sides;i++)
            {
                float t=j/(float)rows,a=i*2*Mathf.PI/sides,pleat=1+Mathf.Sin(a*8)*.034f*t*t;
                float r=Mathf.Lerp(.166f,radius,t*t*(3-2*t));b.V.Add(new Vector3(Mathf.Sin(a)*r*pleat,Mathf.Lerp(.025f,end,t),Mathf.Cos(a)*Mathf.Lerp(.119f,radius*.70f,t)*pleat));b.C.Add(c);
                if(j<rows){int k=start+j*sides+i,n=start+j*sides+(i+1)%sides;b.T.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});}
            }
        }
        static void Frill(OutfitArt.Builder b,float y,float radius,Color c)
        {
            const int sides=48;int start=b.V.Count;
            for(int row=0;row<3;row++)for(int i=0;i<sides;i++)
            {
                float a=i*2*Mathf.PI/sides,wave=Mathf.Cos(a*12)*.007f;
                b.V.Add(new Vector3(Mathf.Sin(a)*(radius+row*.004f),y-row*.016f+(row==2?wave:0),Mathf.Cos(a)*(radius*.70f+row*.004f)));b.C.Add(c);
                if(row<2){int k=start+row*sides+i,n=start+row*sides+(i+1)%sides;b.T.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});}
            }
        }
        static void Bow(OutfitArt.Builder b,float x,float y,float z,float size,Color c)
        {
            for(int s=-1;s<=1;s+=2)b.Panel(new Vector3(x,y,z),new Vector3(x+s*size,y+size*.6f,z),new Vector3(x,y-.01f,z),new Vector3(x+s*size,y-size*.6f,z),c,.005f,2,2);
            b.Badge(x,y,z+.01f,size*.24f,size*.26f,c);
        }
        static void CrossCollar(OutfitArt.Builder b,Color c,Color edge)
        {
            for(int s=-1;s<=1;s+=2){var a=new Vector3(s*.07f,.68f,.08f);var d=new Vector3(-s*.055f,.38f,.139f);b.Seam(a,d,.022f,c);b.Seam(a+new Vector3(s*.025f,0,0),d+new Vector3(s*.025f,0,0),.004f,edge);}
        }
        static void Vine(OutfitArt.Builder b,float y,float width,float z,Color c)
        {
            for(int i=0;i<8;i++){float x=-width+i*width/4;b.Seam(new Vector3(x,y+(i%2)*.018f,z),new Vector3(x+width/4,y+((i+1)%2)*.018f,z),.003f,c);b.Badge(x,y+.03f,z,.010f,.018f,c);}
        }
        static void OpenCoat(OutfitArt.Builder b,float hem,Color c,Color edge,float flare=.28f,Color lining=default(Color))
        {
            if(lining.a==0)lining=c*.80f;
            float[] ys={.67f,.58f,.43f,.20f,.07f,-hem*.30f,-hem*.65f,-hem};float[] rx={.082f,.19f,.20f,.17f,.18f,Mathf.Lerp(.18f,flare,.25f),Mathf.Lerp(.18f,flare,.65f),flare};float[] rz={.082f,.115f,.141f,.13f,.135f,.157f,.184f,.215f};
            const int n=24;int start=b.V.Count;
            for(int face=0;face<2;face++)for(int j=0;j<ys.Length;j++)for(int i=0;i<=n;i++)
            {
                float a=.72f+(2*Mathf.PI-1.44f)*i/n,fold=1+.025f*Mathf.Sin(a*7)*Mathf.Clamp01(-ys[j]/hem),thick=face==0?0:.006f;
                b.V.Add(new Vector3(Mathf.Sin(a)*(rx[j]*fold-thick),ys[j],Mathf.Cos(a)*(rz[j]*fold-thick)));b.C.Add(face==0?c:lining);
                if(j<ys.Length-1&&i<n){int k=start+face*ys.Length*(n+1)+j*(n+1)+i;b.T.AddRange(face==0?new[]{k,k+n+1,k+1,k+1,k+n+1,k+n+2}:new[]{k,k+1,k+n+1,k+1,k+n+2,k+n+1});}
            }
            for(int side=-1;side<=1;side+=2)for(int j=0;j<ys.Length-1;j++)
            {
                Vector3 a=new Vector3(side*Mathf.Sin(.72f)*rx[j],ys[j],Mathf.Cos(.72f)*rz[j]),c1=new Vector3(side*Mathf.Sin(.72f)*rx[j+1],ys[j+1],Mathf.Cos(.72f)*rz[j+1]);
                b.Seam(a+new Vector3(0,0,.007f),c1+new Vector3(0,0,.007f),.005f,edge);
                b.Panel(a,c1,a+new Vector3(-side*.009f,0,.008f),c1+new Vector3(-side*.009f,0,.008f),lining,0,1,1);
            }
            for(int i=0;i<n;i++){float a=.72f+(2*Mathf.PI-1.44f)*i/n,aa=.72f+(2*Mathf.PI-1.44f)*(i+1)/n;b.Seam(new Vector3(Mathf.Sin(a)*flare,-hem,Mathf.Cos(a)*.215f),new Vector3(Mathf.Sin(aa)*flare,-hem,Mathf.Cos(aa)*.215f),.004f,edge);}
        }
    }
}
