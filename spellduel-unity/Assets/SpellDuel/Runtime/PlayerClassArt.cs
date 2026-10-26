using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Only explicit playable ids are routed here. Named NPCs never use these overrides.
    internal static class PlayerClassArt
    {
        internal static readonly string[] Ids={"novice","swordsman","archer","mage","assassin","priest","paladin","berserker","marksman","ranger","archmage","summoner","ninja","shadowdancer","bishop","inquisitor"};
        internal static bool IsPlayer(string id){for(int i=0;i<Ids.Length;i++)if(Ids[i]==id)return true;return false;}
        internal static readonly CharacterLook.Look HeroLook=new CharacterLook.Look(CharacterLook.EyeKind.Sharp,CharacterLook.HairKind.Spikes,"947447","65432e",.40f,.22f,-.48f,.09f,31);
        internal static Portraits.Persona HeroPersona(Portraits.Persona old)
        {
            var p=new Portraits.Persona(.97f,.96f,.98f,1.04f,1,0,.5f,0,.30f,0,old.Pitch,old.Yaw,old.Roll,old.Lean,.02f,1);
            p.Masculine=true;return p;
        }
        static readonly Dictionary<string,OutfitArt.Design> designs=new Dictionary<string,OutfitArt.Design>
        {
            {"novice",new OutfitArt.Design("novice","褐色兜帽旅裝","6a4935","403731","b59a68",.28f,.065f,29)},
            {"swordsman",new OutfitArt.Design("swordsman","銀甲藍白長襟","284978","293344","cdb77a",.36f,.073f,0)},
            {"archer",new OutfitArt.Design("archer","綠金巡林披肩","3d6538","3c3930","b79b60",.26f,.064f,8)},
            {"mage",new OutfitArt.Design("mage","紫金開襟法袍","583b79","303047","c9aa65",.62f,.100f,2)},
            {"assassin",new OutfitArt.Design("assassin","黑紅交帶輕甲","382b39","292b36","c2a16c",.28f,.060f,3)},
            {"priest",new OutfitArt.Design("priest","白金藍內襟祭服","ede2cb","293750","cbb466",.66f,.088f,4)},
            {"paladin",new OutfitArt.Design("paladin","白藍金騎士長衣","f0e9d5","263d66","d5bc74",.44f,.080f,5)},
            {"berserker",new OutfitArt.Design("berserker","白藍劍袍紅腰封","f1e9da","34425d","a94148",.62f,.095f,30)},
            {"marksman",new OutfitArt.Design("marksman","深藍金邊長獵裝","2c3c60","2e333e","e2c680",.56f,.070f,7)},
            {"ranger",new OutfitArt.Design("ranger","綠披風皮革獵裝","355e35","483f2f","c1a664",.30f,.070f,8)},
            {"archmage",new OutfitArt.Design("archmage","紫黑金寬領法袍","543674","2b2942","d5b675",.68f,.110f,9)},
            {"summoner",new OutfitArt.Design("summoner","橘金長襟召喚服","a75f2e","38302e","ddbc72",.50f,.090f,10)},
            {"ninja",new OutfitArt.Design("ninja","靛藍交領與長巾","394970","252b3e","8b98b4",.25f,.065f,11)},
            {"shadowdancer",new OutfitArt.Design("shadowdancer","黑紫金輕甲洋紅襟","493150","302a3d","b89b5b",.32f,.065f,3)},
            {"bishop",new OutfitArt.Design("bishop","紫白金儀式祭服","f0e4cf","503b70","d4b878",.69f,.100f,13)},
            {"inquisitor",new OutfitArt.Design("inquisitor","白金無袖審判衣","e9d5b5","32303b","d6ad57",.55f,.060f,14)}
        };
        static PlayerClassArt()
        {
            // Garment length is independent of the already accepted stance/sole solver.
            float[] hems={.065f,.055f,.035f,.48f,0,.59f,.42f,.56f,.44f,.24f,.63f,.16f,.11f,.02f,.66f,.51f};
            for(int i=0;i<Ids.Length;i++)designs[Ids[i]].AnimationHem=hems[i];
        }
        internal static Color ArmCloth(string id,bool upper,Color skin)
        {
            if(id=="inquisitor"&&upper)return skin;
            if(id=="archer"||id=="ranger"||id=="mage"||id=="archmage"||id=="priest"||id=="bishop")return upper?Inner(id):Design(id).Main;
            return Design(id).Main;
        }
        internal static void ArmDetail(OutfitArt.Builder b,OutfitArt.Design d,bool upper,bool left)
        {
            bool metal=d.Id=="swordsman"||d.Id=="paladin"||d.Id=="marksman";
            if(metal)
            {
                Color plate=d.Id=="marksman"?d.Main:CharacterLook.Hex("b6c2d1");
                if(upper)for(int tier=0;tier<3;tier++)
                {
                    float y=-.035f-tier*.037f,r=.090f-tier*.005f;
                    b.Loft(new[]{y+.015f,y,y-.027f},new[]{r*.75f,r,r*.91f},new[]{r*.73f,r,r*.90f},plate);
                    b.Loft(new[]{y-.021f,y-.029f},new[]{r*.94f,r*.90f},new[]{r*.93f,r*.89f},d.Accent);
                }
                else
                {
                    b.Loft(new[]{-.020f,-.060f,-.130f,-.205f},new[]{.051f,.073f,.064f,.040f},new[]{.053f,.072f,.055f,.034f},plate);
                    b.Loft(new[]{-.060f,-.074f},new[]{.074f,.073f},new[]{.073f,.072f},d.Accent);
                }
            }
            else if(d.Id=="summoner"&&!upper&&left)
            {
                for(int i=0;i<4;i++){float y=-.03f-i*.044f;b.Loft(new[]{y+.01f,y-.008f,y-.035f},new[]{.052f,.071f,.062f},new[]{.056f,.077f,.065f},i%2==0?CharacterLook.Hex("705340"):CharacterLook.Hex("d28b3d"),0,0,8);}
            }
            else if(!upper)
            {
                b.Loft(new[]{-.095f,-.111f,-.19f,-.215f},new[]{.058f,.058f,.043f,.037f},new[]{.051f,.050f,.033f,.028f},CharacterLook.Hex(d.Id=="priest"||d.Id=="bishop"?"e8dcc3":"624834"));
                b.Loft(new[]{-.101f,-.112f},new[]{.059f,.058f},new[]{.052f,.051f},d.Accent);
            }
        }
        internal static OutfitArt.Design Design(string id)=>designs[id];
        internal static Color Inner(string id)=>CharacterLook.Hex(id=="novice"?"3e332c":id=="priest"?"2c3d58":id=="paladin"?"334f84":id=="summoner"?"443735":id=="assassin"?"5f2731":id=="shadowdancer"?"702c68":id=="ninja"?"303b56":id=="bishop"?"675087":id=="inquisitor"?"332f34":"eee4d2");
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        static void ClothGrid(OutfitArt.Builder b,Vector3[,] points,Color color,float thickness=.004f)
        {
            int rows=points.GetLength(0),cols=points.GetLength(1),start=b.V.Count,stride=rows*cols;
            for(int face=0;face<2;face++)for(int j=0;j<rows;j++)for(int i=0;i<cols;i++)
            {
                var along=points[Mathf.Min(j+1,rows-1),i]-points[Mathf.Max(0,j-1),i];var across=points[j,Mathf.Min(i+1,cols-1)]-points[j,Mathf.Max(0,i-1)];
                var normal=Vector3.Cross(along,across).normalized;
                b.V.Add(points[j,i]+normal*(face==0?thickness:-thickness));b.C.Add(color);
                if(j==rows-1||i==cols-1)continue;int k=start+face*stride+j*cols+i;
                if(face==0)b.T.AddRange(new[]{k,k+cols,k+1,k+1,k+cols,k+cols+1});else b.T.AddRange(new[]{k,k+1,k+cols,k+1,k+cols+1,k+cols});
            }
            for(int j=0;j<rows-1;j++)foreach(int i in new[]{0,cols-1})
            {int k=start+j*cols+i,n=k+cols;b.T.AddRange(new[]{k,n,k+stride,n,n+stride,k+stride});}
            for(int i=0;i<cols-1;i++)foreach(int j in new[]{0,rows-1})
            {int k=start+j*cols+i;b.T.AddRange(new[]{k,k+stride,k+1,k+1,k+stride,k+stride+1});}
        }
        static void Clasp(OutfitArt.Builder b,Vector3 center,float radius,Color rim,Color inset)
        {
            const int sides=12;int start=b.V.Count;
            for(int ring=0;ring<2;ring++)for(int i=0;i<sides;i++){
                float a=i*2*Mathf.PI/sides,r=radius*(ring==0?1:.65f);
                b.V.Add(center+new Vector3(Mathf.Sin(a)*r,Mathf.Cos(a)*r,ring==0?0:.004f));b.C.Add(rim);
                if(ring==1){int k=start+i,n=start+(i+1)%sides;b.T.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});}
            }
            int tip=b.V.Count;b.V.Add(center+Vector3.forward*.002f);b.C.Add(inset);
            for(int i=0;i<sides;i++)b.T.AddRange(new[]{start+sides+i,tip,start+sides+(i+1)%sides});
        }
        internal static Mesh MeshFor(string id,string part)
        {
            string key=id+"/"+part;if(meshes.TryGetValue(key,out var cached))return cached;
            var b=new OutfitArt.Builder();var d=Design(id);var inner=Inner(id);var leather=CharacterLook.Hex("5a4132");
            bool armour=id=="swordsman"||id=="paladin"||id=="marksman";
            if(part=="torso")
            {
                // The shoulder base ends below the separate collar. Its old tall cap
                // filled the V opening and made clothing look fused to the neck.
                b.Loft(new[]{.522f,.505f,.475f,.43f,.34f,.24f,.14f,.055f,-.01f},new[]{.081f,.144f,.180f,.191f,.178f,.151f,.147f,.161f,.174f},new[]{.080f,.091f,.102f,.120f,.115f,.103f,.098f,.110f,.115f},d.Main);
                // White/dark undershirt is an actual inset front panel between continuous lapels.
                var fy=new[]{.515f,.47f,.40f,.30f,.20f,.11f};var fz=new[]{.092f,.124f,.133f,.126f,.115f,.110f};
                var shirt=new Vector3[fy.Length,5];
                for(int j=0;j<fy.Length;j++)for(int i=0;i<5;i++){float u=i/2f-1;shirt[j,i]=new Vector3(u*Mathf.Lerp(.065f,.075f,j/(float)(fy.Length-1)),fy[j],fz[j]+.003f*(1-u*u));}
                ClothGrid(b,shirt,inner);
                for(int side=-1;side<=1;side+=2)
                {
                    var lapel=new Vector3[6,3];
                    for(int j=0;j<6;j++)for(int i=0;i<3;i++)
                    {
                        float t=j/5f,u=i/2f;
                        float innerX=Mathf.Lerp(.053f,.075f,t),outerX=innerX+Mathf.Lerp(.039f,.024f,t);
                        lapel[j,i]=new Vector3(side*Mathf.Lerp(innerX,outerX,u),Mathf.Lerp(.528f,.108f,t),Mathf.Lerp(.103f,.121f,t)+.040f*Mathf.Sin(t*Mathf.PI)+.008f*Mathf.Sin(u*Mathf.PI));
                    }
                    // Turned lapels have a broad face and a narrow hem, not a pair of floating rails.
                    ClothGrid(b,lapel,id=="priest"||id=="paladin"?inner:d.Main);
                    for(int j=0;j<5;j++)b.Seam(lapel[j,0]+Vector3.forward*.006f,lapel[j+1,0]+Vector3.forward*.006f,.006f,d.Accent);
                }
                // A real shoulder-to-waist carrying strap, with a buckle and belt.
                for(int i=0;i<4;i++)
                {
                    float t=i/4f,u=(i+1)/4f;
                    var a=new Vector3(Mathf.Lerp(-.12f,.133f,t),Mathf.Lerp(.49f,.13f,t),t<.25f?.133f:t<.75f?.149f:.137f);
                    var c=new Vector3(Mathf.Lerp(-.12f,.133f,u),Mathf.Lerp(.49f,.13f,u),u<.25f?.133f:u<.75f?.149f:.137f);
                    b.Seam(a,c,id=="assassin"?.034f:.027f,leather);
                    if(id=="assassin"||id=="shadowdancer"){a.x=-a.x;c.x=-c.x;a.z+=.003f;c.z+=.003f;b.Seam(a,c,.026f,leather);}
                }
                Clasp(b,new Vector3(.025f,.28f,.164f),.014f,d.Accent,leather);
                b.Loft(new[]{.09f,.045f,.020f},new[]{.171f,.180f,.181f},new[]{.121f,.125f,.127f},id=="berserker"?d.Accent:leather,0,-.01f);
                b.Badge(0,.058f,.127f,.042f,.034f,d.Accent);
                // Separate tails instead of a tight cylindrical skirt: the legs can move between them.
                for(int side=-1;side<=1;side+=2)
                {
                    float end=-d.Hem,wide=id=="novice"?.19f:.20f;
                    b.Panel(new Vector3(side*.025f,.025f,.122f),new Vector3(side*.169f,.025f,.095f),new Vector3(side*.072f,end+.025f,.110f),new Vector3(side*wide,end+.060f,.045f),d.Main,.018f,5,3);
                    b.Seam(new Vector3(side*.025f,.020f,.130f),new Vector3(side*.072f,end+.027f,.119f),.010f,d.Accent);
                    if(id!="novice"&&id!="assassin")b.Panel(new Vector3(side*.080f,.010f,.131f),new Vector3(side*.135f,.010f,.116f),new Vector3(side*.115f,end+.065f,.125f),new Vector3(side*.162f,end+.080f,.092f),inner,.006f,3,2);
                }
                // Rear coat vent stays narrow below the belt; no outward triangular hip fins.
                b.Panel(new Vector3(.16f,.02f,-.116f),new Vector3(-.16f,.02f,-.116f),new Vector3(.18f,-d.Hem+.03f,-.14f),new Vector3(-.18f,-d.Hem+.03f,-.14f),d.Main,-.008f,4,3);
                if(id=="ninja")
                {
                    b.Panel(new Vector3(-.069f,.522f,.106f),new Vector3(-.116f,.491f,.128f),new Vector3(.100f,.255f,.145f),new Vector3(.142f,.287f,.138f),d.Main,.010f,5,2);
                    b.Seam(new Vector3(-.068f,.521f,.116f),new Vector3(.100f,.255f,.155f),.006f,d.Accent);
                }
                if(id=="novice")for(int lace=0;lace<3;lace++){
                    float y=.468f-lace*.028f,z=.136f;
                    b.Seam(new Vector3(-.022f,y,z),new Vector3(.022f,y-.018f,z+.002f),.004f,leather);
                    b.Seam(new Vector3(.022f,y,z),new Vector3(-.022f,y-.018f,z+.002f),.004f,leather);
                }
                if(id=="berserker")
                {
                    b.Seam(new Vector3(-.06f,.54f,.10f),new Vector3(.10f,.30f,.150f),.028f,CharacterLook.Hex("59708f"));
                    b.Seam(new Vector3(-.05f,.54f,.112f),new Vector3(.11f,.30f,.154f),.006f,d.Accent);
                }
                if(id=="priest"||id=="bishop"||id=="inquisitor")
                {
                    b.Seam(new Vector3(0,.38f,.153f),new Vector3(0,.27f,.155f),.010f,d.Accent);
                    b.Seam(new Vector3(-.038f,.345f,.156f),new Vector3(.038f,.345f,.156f),.010f,d.Accent);
                }
                // A continuous shoulder mantle wraps from chest to back. Its curved
                // outside edge follows the deltoid instead of forming rectangular wings.
                bool hood=id=="novice"||id=="archer"||id=="ranger";
                for(int side=-1;side<=1;side+=2)
                {
                    Color mantle=armour?CharacterLook.Hex("b6c2d1"):id=="assassin"?CharacterLook.Hex("762d36"):d.Main;
                    float width=hood?.287f:id=="archmage"?.292f:.278f;
                    var shoulder=new Vector3[5,10];
                    for(int j=0;j<5;j++)for(int i=0;i<10;i++)
                    {
                        float t=j/4f,u=i/9f,angle=Mathf.Lerp(.39f,2.98f,side>0?u:1-u);
                        float f=Mathf.Cos(angle),x=Mathf.Sin(angle),hem=.395f-.060f*Mathf.Abs(f);
                        if(id=="novice")hem-=.010f*(i%3==0?1:0);
                        shoulder[j,i]=new Vector3(side*x*Mathf.Lerp(.073f,width,Mathf.Sin(t*Mathf.PI*.5f)),Mathf.Lerp(.548f,hem,t*t*t),f*Mathf.Lerp(.074f,.159f,Mathf.Sin(t*Mathf.PI*.5f))-.009f);
                    }
                    ClothGrid(b,shoulder,mantle,.005f);
                    for(int i=0;i<9;i++)b.Seam(shoulder[4,i]+Vector3.forward*.004f,shoulder[4,i+1]+Vector3.forward*.004f,id=="novice"?.004f:.008f,d.Accent);
                    if(id!="novice"&&id!="ninja")Clasp(b,new Vector3(side*.120f,.490f,.174f),.018f,d.Accent,d.Main);
                    if(id=="archmage"||id=="paladin"||id=="shadowdancer"||id=="bishop")
                    {
                        var overlay=new Vector3[3,7];
                        for(int j=0;j<3;j++)for(int i=0;i<7;i++){
                            float t=j/2f,a=Mathf.Lerp(.52f,2.65f,side>0?i/6f:1-i/6f);
                            overlay[j,i]=new Vector3(side*Mathf.Sin(a)*Mathf.Lerp(.086f,width+.009f,Mathf.Sin(t*Mathf.PI*.5f)),Mathf.Lerp(.562f,.453f-.015f*Mathf.Abs(Mathf.Cos(a)),t*t*t),Mathf.Cos(a)*Mathf.Lerp(.082f,.151f,Mathf.Sin(t*Mathf.PI*.5f))-.006f);
                        }
                        ClothGrid(b,overlay,d.Main,.005f);
                        for(int i=0;i<6;i++)b.Seam(overlay[2,i]+Vector3.forward*.005f,overlay[2,i+1]+Vector3.forward*.005f,.007f,d.Accent);
                    }
                }
                if(id=="bishop")
                {
                    // Two purple stoles continue from the shoulders down the white vestment.
                    for(int side=-1;side<=1;side+=2){
                        b.Panel(new Vector3(side*.080f,.43f,.164f),new Vector3(side*.132f,.43f,.150f),new Vector3(side*.070f,-.58f,.147f),new Vector3(side*.127f,-.55f,.125f),d.Sub,.006f,6,2);
                        b.Seam(new Vector3(side*.075f,.425f,.173f),new Vector3(side*.070f,-.58f,.156f),.006f,d.Accent);
                    }
                }

            }
            else if(part=="cape")
            {
                float width=id=="novice"?.26f:id=="ranger"||id=="archer"?.28f:.23f;
                float length=id=="novice"||id=="assassin"?.50f:id=="archer"||id=="ranger"?.62f:id=="swordsman"?.60f:.88f;
                Color cape=id=="paladin"?CharacterLook.Hex("2d4673"):id=="assassin"?CharacterLook.Hex("762d36"):d.Main;
                var cloth=new Vector3[8,13];
                for(int j=0;j<8;j++)for(int i=0;i<13;i++)
                {
                    float t=j/7f,u=i/6f-1;
                    cloth[j,i]=new Vector3(u*width*(1-.10f*t),.045f-.09f*Mathf.Abs(u)-length*t+t*t*(.03f+.035f*Mathf.Sin(u*5)),.10f*u*u-.065f*t+.018f*Mathf.Sin(u*7)*t);
                }
                ClothGrid(b,cloth,cape);
                for(int j=0;j<7;j++)foreach(int edge in new[]{0,12})b.Seam(cloth[j,edge]+new Vector3(0,0,-.009f),cloth[j+1,edge]+new Vector3(0,0,-.009f),.010f,d.Accent);
                for(int i=0;i<12;i++)b.Seam(cloth[7,i]+new Vector3(0,0,-.009f),cloth[7,i+1]+new Vector3(0,0,-.009f),.010f,d.Accent);
                if(id=="novice"||id=="archer"||id=="ranger")
                    b.Loft(new[]{.03f,-.02f,-.11f,-.19f},new[]{.11f,.14f,.14f,.07f},new[]{.10f,.13f,.115f,.06f},cape,0,.025f);
            }
            else if(part=="neck")
            {
                // A continuous tapered bridge into the fitted jaw; bones and height stay unchanged.
                b.Skin=AnimeFace.SkinFor("novice");
                b.Loft(new[]{.660f,.610f,.570f,.535f},new[]{.028f,.039f,.051f,.060f},new[]{.032f,.041f,.048f,.054f},b.Skin,0,-.020f,12);
            }
            else if(part=="crown")
            {
                // Purple mitre with a white front and gold ribs, seated on the unchanged short hair.
                b.Loft(new[]{.29f,.25f,.13f,.095f},new[]{.006f,.06f,.151f,.150f},new[]{.005f,.046f,.115f,.115f},d.Sub);
                b.Panel(new Vector3(-.115f,.11f,.117f),new Vector3(.115f,.11f,.117f),new Vector3(-.018f,.285f,.025f),new Vector3(.018f,.285f,.025f),d.Main,.005f,4,2);
                b.Seam(new Vector3(-.135f,.11f,.12f),new Vector3(0,.30f,.025f),.012f,d.Accent);b.Seam(new Vector3(.135f,.11f,.12f),new Vector3(0,.30f,.025f),.012f,d.Accent);
                b.Seam(new Vector3(0,.13f,.126f),new Vector3(0,.24f,.078f),.012f,d.Accent);
            }
            cached=b.Finish("Player reference "+key);meshes[key]=cached;return cached;
        }
        internal static void Add(Transform parent,string id,string part)=>AnimeFace.Part(parent,"Player default "+part,MeshFor(id,part),Vector3.zero,AnimeFace.Material(Color.white,4));
    }
}
