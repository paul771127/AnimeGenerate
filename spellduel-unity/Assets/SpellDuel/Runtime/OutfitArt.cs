using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>Joint-mounted, cached garment meshes. Every layer is geometry, never an imported asset.</summary>
    internal static class OutfitArt
    {
        internal sealed class Design
        {
            internal string Id,Name;internal Color Main,Sub,Accent;internal float Hem,Sleeve;internal int Cut;
            internal Design(string id,string name,string main,string sub,string accent,float hem,float sleeve,int cut)
            {Id=id;Name=name;Main=CharacterLook.Hex(main);Sub=CharacterLook.Hex(sub);Accent=CharacterLook.Hex(accent);Hem=hem;Sleeve=sleeve;Cut=cut;}
        }
        static readonly Dictionary<string,Design> designs=new Dictionary<string,Design>
        {
            {"swordsman",new Design("swordsman","斜襟機能短夾克","5489c7","303e58","ffbc63",.055f,.073f,0)},
            {"archer",new Design("archer","輕量戰術背心","8daa47","354d46","f5b857",.035f,.058f,1)},
            {"mage",new Design("mage","符文連帽派克大衣","9673c9","303859","7ce5dc",.48f,.104f,2)},
            {"assassin",new Design("assassin","交叉綁帶高領戰鬥服","5b466f","303b50","e887cb",0f,.053f,3)},
            {"priest",new Design("priest","金邊分襟醫療長外套","eee6d2","53677b","e9b955",.59f,.082f,4)},
            {"paladin",new Design("paladin","半披風白色軍裝","eee6d2","425369","e8b94d",.42f,.075f,5)},
            {"berserker",new Design("berserker","無袖騎士皮夾克","bc5149","5a3641","e9b66c",.02f,.062f,6)},
            {"marksman",new Design("marksman","單肩狙擊長夾克","6d8193","39414f","ebcb66",.44f,.063f,7)},
            {"ranger",new Design("ranger","斜肩山地短斗篷","43947b","3c5148","efb36a",.24f,.077f,8)},
            {"archmage",new Design("archmage","符文寬袖長風衣","8461b8","39364f","9ce7e4",.63f,.109f,9)},
            {"summoner",new Design("summoner","多口袋短帽衫","d6884b","4a485c","b9e877",.16f,.090f,10)},
            {"ninja",new Design("ninja","半袖交領忍者機能服","596e86","303c50","d6b764",.11f,.065f,11)},
            {"shadowdancer",new Design("shadowdancer","露肩短裝與舞帶","ad5b9c","473a61","8de0e1",.02f,.057f,12)},
            {"bishop",new Design("bishop","寬領儀式長外套","e9e1c9","686e85","dbb25c",.66f,.095f,13)},
            {"inquisitor",new Design("inquisitor","斜裁無袖審判長背心","c7973e","434957","efe1b1",.51f,.059f,14)},
            {"老焰",new Design("老焰","不對稱老村長外套","a64232","4e4958","e9bd72",.35f,.071f,15)},
            {"小鐵",new Design("小鐵","捲袖鐵工工作服","9d5d32","4f3e3c","e9b85d",.08f,.088f,16)},
            {"米菈",new Design("米菈","多袋旅行長背心","268e9c","4b4759","dfb75f",.43f,.064f,17)},
            {"翠",new Design("翠","葉領研究長外套","5b8e74","355249","cbbf79",.51f,.086f,18)},
            {"楓",new Design("楓","斜肩森林巡守衣","4d7e68","314c48","dfac61",.28f,.073f,19)},
            {"小葉",new Design("小葉","寬袖短上衣與百褶下擺","d9aa51","5f755c","e5d8ac",.17f,.094f,20)},
            {"海斗",new Design("海斗","單肩海軍長夾克","456b8c","303d55","d8c798",.49f,.077f,21)},
            {"凪",new Design("凪","偏襟圍裙與泡袖","9c4c64","ede1c5","d3af62",.28f,.095f,22)},
            {"阿潮",new Design("阿潮","短袖海釣工作外套","43787f","4e4d5e","dec28c",.12f,.080f,23)},
            {"鐵匠",new Design("鐵匠","厚肩無袖皮工作服","b96c49","5d4541","e5b975",.10f,.073f,24)},
            {"店員",new Design("店員","藥師翻領短外套","589b9b","e3dbc6","e5bd64",.31f,.080f,25)},
            {"艾琳",new Design("艾琳","公會導引短夾克","7250aa","3d405c","d9b85b",.2f,.075f,27)},
            {"澪",new Design("澪","潮汐觀測短外套","4b8998","334c69","d4cf8f",.3f,.07f,28)},
            {"公會長",new Design("公會長","斜披肩公會長風衣","743948","40475a","d7af5e",.60f,.084f,26)}
        };
        internal static Design Get(string id)=>designs.TryGetValue(Portraits.Key(id),out var d)?d:designs["swordsman"];
        internal static IEnumerable<KeyValuePair<string,Design>> All=>designs;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        internal sealed class Builder
        {
            internal Color Skin;
            internal readonly List<Vector3> V=new List<Vector3>();internal readonly List<int> T=new List<int>();internal readonly List<Color> C=new List<Color>();
            void Vertex(Vector3 v,Color c){V.Add(v);C.Add(c);}
            internal void Tube(Vector3[] centers,float[] widths,float[] depths,Color color,int sides=10)
            {
                int start=V.Count;
                for(int j=0;j<centers.Length;j++)for(int i=0;i<sides;i++)
                {
                    float a=i*2*Mathf.PI/sides,fold=i%3==0&&j>0&&j<centers.Length-1?1.045f:1;
                    float shade=i>=sides/2?.72f:1;if(j%3==1)shade*=.91f;
                    var paint=color*shade;paint.a=Mathf.Abs(color.r-Skin.r)+Mathf.Abs(color.g-Skin.g)+Mathf.Abs(color.b-Skin.b)<.001f?0:1;
                    Vertex(centers[j]+new Vector3(Mathf.Sin(a)*widths[j]*fold,0,Mathf.Cos(a)*depths[j]*fold),paint);
                    if(j==centers.Length-1)continue;int k=start+j*sides+i,n=start+j*sides+(i+1)%sides;T.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});
                }
                // Horizontal rings go down the limb. Reverse caps for a closed outward hull.
                for(int cap=0;cap<2;cap++){int k=start+(cap==0?0:(centers.Length-1)*sides);for(int i=1;i<sides-1;i++)T.AddRange(cap==0?new[]{k,k+i,k+i+1}:new[]{k,k+i+1,k+i});}
            }
            internal void Loft(float[] y,float[] rx,float[] rz,Color c,float x=0,float z=0)
            {var path=new Vector3[y.Length];for(int i=0;i<path.Length;i++)path[i]=new Vector3(x,y[i],z);Tube(path,rx,rz,c);}
            internal void Panel(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float bulge=.014f,int rows=4,int cols=3)
            {
                int start=V.Count;
                for(int face=0;face<2;face++)for(int j=0;j<=rows;j++)for(int i=0;i<=cols;i++)
                {
                    float t=j/(float)rows,u=i/(float)cols;var left=a*(1-t)+c*t;var right=b*(1-t)+d*t;
                    var at=left*(1-u)+right*u;at.z+=Mathf.Sin(u*Mathf.PI)*Mathf.Sin(t*Mathf.PI)*bulge+(face==0?.004f:-.004f);
                    Color shade=color*(i==0?.68f:i==cols?.88f:1);shade.a=1;Vertex(at,shade);
                    if(j==rows||i==cols)continue;int k=start+face*(rows+1)*(cols+1)+j*(cols+1)+i;
                    if(face==0)T.AddRange(new[]{k,k+cols+1,k+1,k+1,k+cols+1,k+cols+2});else T.AddRange(new[]{k,k+1,k+cols+1,k+1,k+cols+2,k+cols+1});
                }
                int stride=(rows+1)*(cols+1);
                for(int j=0;j<rows;j++)foreach(int i in new[]{0,cols}){int k=start+j*(cols+1)+i,n=k+cols+1;if(i==0)T.AddRange(new[]{k,k+stride,n,n,k+stride,n+stride});else T.AddRange(new[]{k,n,k+stride,n,n+stride,k+stride});}
                for(int i=0;i<cols;i++)foreach(int j in new[]{0,rows}){int k=start+j*(cols+1)+i;if(j==0)T.AddRange(new[]{k,k+1,k+stride,k+1,k+1+stride,k+stride});else T.AddRange(new[]{k,k+stride,k+1,k+1,k+stride,k+1+stride});}
            }
            internal void Seam(Vector3 a,Vector3 b,float width,Color color)
            {
                var delta=b-a;var right=Vector3.Cross(delta.normalized,new Vector3(0,0,1)).normalized*width*.5f;
                if(right.sqrMagnitude<.00000001f)right=new Vector3(width*.5f,0,0);
                Panel(a-right,a+right,b-right,b+right,color,0,1,1);
            }
            internal void GlowSeam(Vector3 a,Vector3 b,float width,Color color)
            {
                int first=C.Count;Seam(a,b,width,color);for(int i=first;i<C.Count;i++){var c=C[i];c.a=.5f;C[i]=c;}
            }
            internal void Badge(float x,float y,float z,float w,float h,Color color)
            {Panel(new Vector3(x-w*.5f,y+h*.5f,z),new Vector3(x+w*.5f,y+h*.5f,z),new Vector3(x-w*.43f,y-h*.5f,z+.003f),new Vector3(x+w*.43f,y-h*.5f,z+.003f),color,.006f,1,1);}
            internal Mesh Finish(string name){var m=new Mesh{name=name};m.SetVertices(V);m.SetTriangles(T,0);m.colors=C.ToArray();m.RecalculateNormals();m.RecalculateBounds();return m;}
        }
        internal static Mesh MeshFor(string id,string part,Color skin)
        {
            string key=id+"/"+part+ColorUtility.ToHtmlStringRGB(skin);if(meshes.TryGetValue(key,out var m))return m;
            var d=Get(id);var b=new Builder{Skin=skin};var main=d.Main;var sub=d.Sub;var accent=d.Accent;bool left=part.EndsWith("L");float side=left?-1:1;
            if(part=="torso")
            {
                bool fitted=d.Cut==3||d.Cut==11||d.Cut==12;
                bool oversize=d.Cut==2||d.Cut==9||d.Cut==13;
                b.Loft(new[]{.49f,.40f,.24f,.11f,.015f},new[]{.172f,.181f,.148f,.137f,.163f},new[]{.091f,.126f,.105f,.101f,.112f},fitted?main:sub);
                // Open jacket front, a shaped back and one deliberately lower lapel.
                if(!fitted)
                {
                b.Panel(new Vector3(-.184f,.47f,.085f),new Vector3(-.042f,.48f,.125f),new Vector3(-.177f,-.035f,.133f),new Vector3(-.018f,.025f,.148f),main);
                b.Panel(new Vector3(.040f,.47f,.131f),new Vector3(.184f,.47f,.083f),new Vector3(.045f,.02f,.152f),new Vector3(.190f,-.055f,.125f),main);
                b.Panel(new Vector3(.18f,.48f,-.10f),new Vector3(-.18f,.48f,-.10f),new Vector3(.17f,-.02f,-.12f),new Vector3(-.17f,-.02f,-.12f),main,-.02f);
                }
                b.Loft(new[]{.55f,.49f,.475f},new[]{.079f,.108f,.110f},new[]{.069f,.086f,.088f},main);
                if(!fitted)
                {
                b.Panel(new Vector3(-.11f,.50f,.12f),new Vector3(-.02f,.43f,.167f),new Vector3(-.13f,.29f,.151f),new Vector3(-.045f,.36f,.177f),sub);
                b.Panel(new Vector3(.025f,.44f,.172f),new Vector3(.12f,.49f,.118f),new Vector3(.04f,.31f,.171f),new Vector3(.14f,.34f,.147f),main*.8f);
                }
                b.Seam(new Vector3(-.022f,.435f,.179f),new Vector3(-.02f,.03f,.162f),.008f,accent);
                for(int i=0;i<7;i++)b.Badge(-.019f,.38f-i*.048f,.181f,.019f,.007f,accent);
                b.Badge(.105f,.285f,.161f,.085f,.070f,sub);b.Seam(new Vector3(.067f,.31f,.175f),new Vector3(.144f,.31f,.17f),.006f,accent);
                // Curved split tails and lining have different lengths; no closed cone traps the legs.
                float hem=d.Hem;
                if(hem>.01f)for(int s=-1;s<=1;s+=2)
                {
                    float end=-hem*(s<0?1:.76f),outX=(d.Cut==2||d.Cut==9||d.Cut==13?.29f:.225f)*s;
                    b.Panel(new Vector3(s*.045f,.02f,.136f),new Vector3(s*.17f,.02f,.12f),new Vector3(s*.09f,end+.04f,.155f),new Vector3(outX,end,.07f),s<0?main:sub,.023f);
                    b.Seam(new Vector3(s*.17f,.015f,.133f),new Vector3(outX,end+.009f,.084f),.009f,accent);
                }
                if(hem>.01f)b.Panel(new Vector3(.17f,.02f,-.12f),new Vector3(-.17f,.02f,-.12f),new Vector3(.21f,-hem*.85f,-.18f),new Vector3(-.24f,-hem,-.16f),main,-.034f);
                // Back yoke and faction chevrons remain visible in the rear view.
                b.Seam(new Vector3(-.15f,.35f,-.144f),new Vector3(0,.29f,-.159f),.013f,accent);
                b.Seam(new Vector3(0,.29f,-.159f),new Vector3(.15f,.35f,-.144f),.013f,accent);
                b.Loft(new[]{.075f,.03f},new[]{.174f,.174f},new[]{.123f,.123f},sub);b.Badge(.04f,.052f,.14f,.055f,.035f,accent);
                b.Seam(new Vector3(-.12f,.44f,.162f),new Vector3(.13f,.10f,.159f),d.Cut==1||d.Cut==7||d.Cut==11?.035f:.014f,sub);
                if(d.Cut==2||d.Cut==9||d.Cut==10)b.Loft(new[]{.57f,.48f,.41f},new[]{.12f,.145f,.095f},new[]{.13f,.16f,.12f},main,0,-.095f);
                if(d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==14){b.Seam(new Vector3(.085f,.37f,.184f),new Vector3(.085f,.22f,.184f),.013f,accent);b.Seam(new Vector3(.04f,.32f,.184f),new Vector3(.13f,.32f,.184f),.013f,accent);}
                if(d.Cut==6||d.Cut==16||d.Cut==24)b.Panel(new Vector3(-.14f,.20f,.15f),new Vector3(.13f,.23f,.15f),new Vector3(-.18f,-.26f,.14f),new Vector3(.18f,-.20f,.16f),sub);
                if(d.Cut==8||d.Cut==19||d.Cut==21||d.Cut==26)b.Panel(new Vector3(-.26f,.46f,.10f),new Vector3(-.06f,.51f,.13f),new Vector3(-.31f,.16f,.12f),new Vector3(-.04f,.20f,.18f),main,.03f);
                if(d.Cut==0||d.Cut==1||d.Cut==10)b.Panel(new Vector3(.14f,.03f,.13f),new Vector3(.21f,.02f,.12f),new Vector3(.22f,-.25f,.15f),new Vector3(.27f,-.24f,.13f),sub);
                if(fitted){b.Seam(new Vector3(-.14f,.44f,.151f),new Vector3(.12f,.11f,.147f),.022f,sub);b.Seam(new Vector3(.14f,.44f,.151f),new Vector3(-.12f,.14f,.148f),.020f,accent);}
                if(d.Cut==2||d.Cut==9){b.GlowSeam(new Vector3(.075f,.44f,.182f),new Vector3(.13f,.37f,.182f),.010f,accent);b.GlowSeam(new Vector3(.13f,.37f,.182f),new Vector3(.08f,.29f,.182f),.010f,accent);}
                if(d.Cut==20)for(int i=0;i<5;i++)b.Badge(-.16f+i*.08f,-.12f,.17f,.05f,.15f,i%2==0?main:sub);
                if(d.Cut==22||d.Cut==25)b.Panel(new Vector3(-.11f,.3f,.17f),new Vector3(.13f,.3f,.17f),new Vector3(-.20f,-.38f,.18f),new Vector3(.20f,-.32f,.18f),sub);
                for(int i=0;i<b.V.Count;i++)
                {
                    var at=b.V[i];if(oversize)at.x*=1.15f;if(fitted){at.x*=.90f;if(at.z>.13f)at.z=.13f+(at.z-.13f)*.4f;}b.V[i]=at;
                }
            }
            else if(part.StartsWith("upper"))
            {
                float wide=d.Sleeve*(left?1.12f:.88f);Color c=d.Cut==1||d.Cut==23||((d.Cut==6||d.Cut==14||d.Cut==24||d.Cut==12)&&!left)?skin:main;
                b.Loft(new[]{.015f,-.045f,-.12f,-.22f,-.27f},new[]{wide*.82f,wide*1.12f,wide,wide*.74f,wide*.70f},new[]{wide*.85f,wide,wide*.91f,wide*.73f,wide*.68f},c);
                if(left&&(d.Cut==0||d.Cut==5||d.Cut==6||d.Cut==14))b.Panel(new Vector3(-.09f,.02f,.045f),new Vector3(.07f,.025f,.045f),new Vector3(-.11f,-.14f,.085f),new Vector3(.06f,-.10f,.09f),sub,.025f);
                b.Loft(new[]{-.20f,-.24f},new[]{wide*.83f,wide*.8f},new[]{wide*.80f,wide*.77f},sub);
                b.Badge(side*.015f,-.085f,wide+.007f,.058f,.042f,accent);
            }
            else if(part.StartsWith("fore"))
            {
                float wide=d.Sleeve*((d.Cut==2||d.Cut==9||d.Cut==13)&&left?1.12f:.75f);
                b.Loft(new[]{.035f,0f,-.035f},new[]{.035f,wide*.88f,.035f},new[]{.034f,wide*.84f,.034f},left?main:sub);
                b.Loft(new[]{-.005f,-.06f,-.14f,-.20f,-.25f},new[]{wide*.80f,wide,wide*.86f,wide*.69f,wide*.66f},new[]{wide*.76f,wide*.90f,wide*.77f,wide*.66f,wide*.63f},left?main:sub);
                b.Loft(new[]{-.18f,-.22f},new[]{wide*.95f,wide*.95f},new[]{wide*.88f,wide*.88f},accent);
                if(!left)b.Badge(0,-.105f,wide*.9f,.045f,.08f,main);
            }
            else if(part.StartsWith("thigh"))
            {
                float wide=d.Cut==10||d.Cut==20?.087f:d.Cut==3||d.Cut==12?.062f:.076f;
                b.Loft(new[]{.01f,-.06f,-.17f,-.29f,-.415f},new[]{wide*.90f,wide,wide*.94f,wide*.78f,wide*.67f},new[]{wide*.90f,wide*.94f,wide*.88f,wide*.76f,wide*.69f},d.Cut==12&&left?skin:sub);
                if(left){b.Loft(new[]{-.24f,-.275f},new[]{wide*.89f,wide*.86f},new[]{wide*.90f,wide*.87f},main);b.Badge(-.037f,-.18f,.065f,.07f,.10f,main);}
                else b.Seam(new Vector3(.035f,-.05f,.078f),new Vector3(.035f,-.35f,.06f),.009f,accent);
            }
            else if(part.StartsWith("calf"))
            {
                b.Loft(new[]{.025f,0f,-.025f},new[]{.035f,.055f,.035f},new[]{.034f,.053f,.034f},sub);
                bool sneaker=d.Cut==0||d.Cut==10,tabi=d.Cut==11,high=d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==12&&left;
                Color boot=high?main:sub;float soleWidth=tabi?.064f:sneaker?.088f:.079f,toe=tabi?.112f:sneaker?.142f:.136f;
                b.Loft(new[]{.005f,-.07f,-.15f,-.25f,-.34f},new[]{.051f,.069f,.067f,.049f,.045f},new[]{.051f,.065f,.06f,.046f,.045f},high?main:left?sub:main);
                b.Badge(0,-.035f,.064f,.065f,.085f,left?main:sub);
                b.Loft(new[]{sneaker?-.33f:high?-.19f:-.285f,-.35f,-.385f,-.414f},new[]{high?.064f:.049f,.063f,soleWidth*.97f,soleWidth*.95f},new[]{.055f,.10f,toe*.985f,toe*.96f},boot,0,.038f);
                b.Loft(new[]{-.414f,-.430f},new[]{soleWidth,soleWidth},new[]{toe,toe},accent,0,.038f);
                if(high)b.Seam(new Vector3(-.03f,-.03f,.078f),new Vector3(-.03f,-.29f,.09f),.007f,accent);
                for(int i=0;i<3;i++)b.Seam(new Vector3(-.041f,-.34f-i*.018f,.13f+i*.009f),new Vector3(.041f,-.34f-i*.018f,.13f+i*.009f),.006f,accent);
            }
            else if(part.StartsWith("hand"))
            {
                b.Loft(new[]{0f,-.027f},new[]{.040f,.047f},new[]{.036f,.039f},sub);
                b.Loft(new[]{-.027f,-.066f},new[]{.047f,.035f},new[]{.039f,.027f},d.Cut==12&&left?sub:skin);
            }
            else if(part=="cape"||part=="scarf"||part.StartsWith("ribbon"))
            {
                float w=part=="cape"?.20f:part=="scarf"?.06f:.035f,h=part=="cape"?d.Hem+.12f:part=="scarf"?.48f:.55f;
                b.Panel(new Vector3(w,0,0),new Vector3(-w,0,0),new Vector3(w*1.18f,-h,-.045f),new Vector3(-w*.85f,-h+.06f,-.09f),main,-.045f);
                b.Seam(new Vector3(-w,0,-.008f),new Vector3(-w*.85f,-h+.06f,-.10f),.010f,accent);
            }
            m=b.Finish("Outfit "+key);meshes[key]=m;return m;
        }
        static Material flashMaterial;
        internal static Material FlashMaterial()
        {
            if(flashMaterial!=null)return flashMaterial;
            // The old sprite flash interprets garment alpha tags as transparency. Keep the hit swap opaque.
            flashMaterial=new Material(ToonStyle.LitShader()){name="Human outfit flash",color=new Color(1,.92f,.84f)};
            if(flashMaterial.shader.name=="SpellDuel/Toon")
            {
                flashMaterial.EnableKeyword("SD_HUMAN");flashMaterial.SetFloat("_HumanFace",2);flashMaterial.SetFloat("_HumanHair",0);
                flashMaterial.SetFloat("_HumanOutline",.60f);flashMaterial.SetColor("_HumanSkinShade",Color.white);
            }
            return flashMaterial;
        }
        internal static GameObject Add(Transform parent,string id,string part,Color skin)
            =>AnimeFace.Part(parent,"Outfit "+part,MeshFor(id,part,skin),Vector3.zero,AnimeFace.Material(Color.white,4));
    }
}
