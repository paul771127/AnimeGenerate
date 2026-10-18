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
            {"swordsman",new Design("swordsman","斜襟機能短夾克","307cf1","253d8a","ffc52e",.055f,.073f,0)},
            {"archer",new Design("archer","輕量戰術背心","8bcf27","245c42","f5b857",.035f,.058f,1)},
            {"mage",new Design("mage","符文連帽派克大衣","a353ef","3b3484","65fff0",.48f,.104f,2)},
            {"assassin",new Design("assassin","交叉綁帶高領戰鬥服","7443ab","343b7c","ff76dc",0f,.053f,3)},
            {"priest",new Design("priest","金邊分襟醫療長外套","fff0d4","3d68a2","e9b955",.59f,.082f,4)},
            {"paladin",new Design("paladin","半披風白色軍裝","fff0d4","31569a","e8b94d",.42f,.075f,5)},
            {"berserker",new Design("berserker","無袖騎士皮夾克","e34d39","792d4c","e9b66c",.02f,.062f,6)},
            {"marksman",new Design("marksman","單肩狙擊長夾克","327db5","293e73","ebcb66",.44f,.063f,7)},
            {"ranger",new Design("ranger","斜肩山地短斗篷","1bb88d","245a4b","efb36a",.24f,.077f,8)},
            {"archmage",new Design("archmage","符文寬袖長風衣","8d42dd","403080","75fff1",.63f,.109f,9)},
            {"summoner",new Design("summoner","多口袋短帽衫","f58b2b","503e84","ceff53",.16f,.090f,10)},
            {"ninja",new Design("ninja","半袖交領忍者機能服","456db0","2c3874","d6b764",.11f,.065f,11)},
            {"shadowdancer",new Design("shadowdancer","露肩短裝與舞帶","d544b4","513181","62f5ef",.02f,.057f,12)},
            {"bishop",new Design("bishop","寬領儀式長外套","fff0d1","6056a3","dbb25c",.66f,.095f,13)},
            {"inquisitor",new Design("inquisitor","斜裁無袖審判長背心","efad25","39477d","efe1b1",.51f,.059f,14)},
            {"老焰",new Design("老焰","不對稱老村長外套","d34829","563b70","e9bd72",.35f,.071f,15)},
            {"小鐵",new Design("小鐵","捲袖鐵工工作服","c9752e","683f41","e9b85d",.08f,.088f,16)},
            {"米菈",new Design("米菈","多袋旅行長背心","13b4bd","55407a","dfb75f",.43f,.064f,17)},
            {"翠",new Design("翠","葉領研究長外套","50b982","24664b","cbbf79",.51f,.086f,18)},
            {"楓",new Design("楓","斜肩森林巡守衣","2c9a65","235e50","dfac61",.28f,.073f,19)},
            {"小葉",new Design("小葉","寬袖短上衣與百褶下擺","f2b935","477853","e5d8ac",.17f,.094f,20)},
            {"海斗",new Design("海斗","單肩海軍長夾克","286fc0","293b80","d8c798",.49f,.077f,21)},
            {"凪",new Design("凪","偏襟圍裙與泡袖","cd4270","fff0d8","d3af62",.28f,.095f,22)},
            {"阿潮",new Design("阿潮","短袖海釣工作外套","189ca3","4b467c","dec28c",.12f,.080f,23)},
            {"鐵匠",new Design("鐵匠","厚肩無袖皮工作服","db7037","733d42","e5b975",.10f,.073f,24)},
            {"店員",new Design("店員","藥師翻領短外套","2cbcb3","fff0d2","e5bd64",.31f,.080f,25)},
            {"艾琳",new Design("艾琳","公會導引短夾克","8b43d4","404780","d9b85b",.2f,.075f,27)},
            {"澪",new Design("澪","潮汐觀測短外套","269eb8","2b548d","d4cf8f",.3f,.07f,28)},
            {"公會長",new Design("公會長","斜披肩公會長風衣","a32d56","3c467e","d7af5e",.60f,.084f,26)}
        };
        internal static Design Get(string id)=>designs.TryGetValue(Portraits.Key(id),out var d)?d:designs["swordsman"];
        internal static IEnumerable<KeyValuePair<string,Design>> All=>designs;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        internal sealed class Builder
        {
            internal Color Skin;internal bool LegProfile;
            internal readonly List<Vector3> V=new List<Vector3>();internal readonly List<int> T=new List<int>();internal readonly List<Color> C=new List<Color>();
            void Vertex(Vector3 v,Color c){V.Add(v);C.Add(c);}
            internal void Tube(Vector3[] centers,float[] widths,float[] depths,Color color,int sides=10)
            {
                int start=V.Count;
                for(int j=0;j<centers.Length;j++)for(int i=0;i<sides;i++)
                {
                    float a=i*2*Mathf.PI/sides,fold=LegProfile&&i%3==0&&j>0&&j<centers.Length-1?1.045f:1;
                    float shade=1;
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
                    Color shade=color;shade.a=1;Vertex(at,shade);
                    if(j==rows||i==cols)continue;int k=start+face*(rows+1)*(cols+1)+j*(cols+1)+i;
                    if(face==0)T.AddRange(new[]{k,k+cols+1,k+1,k+1,k+cols+1,k+cols+2});else T.AddRange(new[]{k,k+1,k+cols+1,k+1,k+cols+2,k+cols+1});
                }
                int stride=(rows+1)*(cols+1);
                for(int j=0;j<rows;j++)foreach(int i in new[]{0,cols}){int k=start+j*(cols+1)+i,n=k+cols+1;if(i==0)T.AddRange(new[]{k,k+stride,n,n,k+stride,n+stride});else T.AddRange(new[]{k,n,k+stride,n,n+stride,k+stride});}
                for(int i=0;i<cols;i++)foreach(int j in new[]{0,rows}){int k=start+j*(cols+1)+i;if(j==0)T.AddRange(new[]{k,k+1,k+stride,k+1,k+1+stride,k+stride});else T.AddRange(new[]{k,k+stride,k+1,k+1,k+stride,k+1+stride});}
            }
            internal void Seam(Vector3 a,Vector3 b,float width,Color color)
            {
                var delta=b-a;var right=Vector3.Cross(new Vector3(0,0,1),delta.normalized).normalized*width*.5f;
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
            if(part=="torso"||part=="body")BuildTorso(b,d,skin,part=="body");
            else if(part.StartsWith("upper")||part.StartsWith("fore"))BuildArm(b,d,skin,part.StartsWith("upper"),left);
            else if(part.StartsWith("anatomy"))BuildArm(b,d,skin,part.StartsWith("anatomyUpper"),left,true);
            else if(part.StartsWith("thigh"))
            {
                b.LegProfile=true;
                float wide=d.Cut==10||d.Cut==20?.087f:d.Cut==3||d.Cut==12?.062f:.076f;
                b.Loft(new[]{.01f,-.06f,-.17f,-.29f,-.415f},new[]{wide*.90f,wide,wide*.94f,wide*.78f,wide*.67f},new[]{wide*.90f,wide*.94f,wide*.88f,wide*.76f,wide*.69f},d.Cut==12&&left?skin:sub);
                if(left&&(d.Cut==0||d.Cut==1||d.Cut==7||d.Cut==10||d.Cut==11||d.Cut==16))Pocket(b,-.012f,-.18f,.073f,.063f,.086f,sub,accent);
            }
            else if(part.StartsWith("calf"))
            {
                b.LegProfile=true;
                b.Loft(new[]{.025f,0f,-.025f},new[]{.035f,.055f,.035f},new[]{.034f,.053f,.034f},sub);
                bool sneaker=d.Cut==0||d.Cut==10,tabi=d.Cut==11,high=d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==12&&left;
                Color boot=high?main:sub;float soleWidth=tabi?.064f:sneaker?.088f:.079f,toe=tabi?.112f:sneaker?.142f:.136f;
                b.Loft(new[]{.005f,-.07f,-.15f,-.25f,-.34f},new[]{.051f,.069f,.067f,.049f,.045f},new[]{.051f,.065f,.06f,.046f,.045f},high?main:sub);
                if(d.Cut==0||d.Cut==5||d.Cut==7||d.Cut==11)KneeGuard(b,sub,accent);
                b.Loft(new[]{sneaker?-.33f:high?-.19f:-.285f,-.35f,-.385f,-.414f},new[]{high?.064f:.049f,.063f,soleWidth*.97f,soleWidth*.95f},new[]{.055f,.10f,toe*.985f,toe*.96f},boot,0,.038f);
                b.Loft(new[]{-.414f,-.430f},new[]{soleWidth,soleWidth},new[]{toe,toe},accent,0,.038f);
                if(high)b.Seam(new Vector3(-.03f,-.03f,.078f),new Vector3(-.03f,-.29f,.09f),.007f,accent);
                for(int i=0;i<3;i++)b.Seam(new Vector3(-.041f,-.34f-i*.018f,.13f+i*.009f),new Vector3(.041f,-.34f-i*.018f,.13f+i*.009f),.006f,accent);
            }
            else if(part.StartsWith("hand"))BuildHand(b,d,skin,left,part.Contains("Open")?2:part.Contains("Grip")?1:0);
            else if(part=="cape"||part=="scarf"||part.StartsWith("ribbon"))
            {
                float w=part=="cape"?.20f:part=="scarf"?.06f:.035f,h=part=="cape"?d.Hem+.12f:part=="scarf"?.48f:.55f;
                b.Panel(new Vector3(w,0,0),new Vector3(-w,0,0),new Vector3(w*1.18f,-h,-.045f),new Vector3(-w*.85f,-h+.06f,-.09f),main,-.045f);
                b.Seam(new Vector3(-w,0,-.008f),new Vector3(-w*.85f,-h+.06f,-.10f),.010f,accent);
            }
            m=b.Finish("Outfit "+key);meshes[key]=m;return m;
        }
        internal static float Strength(Design d)=>d.Cut==0||d.Cut==5||d.Cut==6||d.Cut==16||d.Cut==24?1.15f:d.Cut==3||d.Cut==12||d.Cut==18||d.Cut==20||d.Cut==22?.86f:1;
        internal static string Components(Design d)
        {
            bool fitted=d.Cut==3||d.Cut==11||d.Cut==12;
            string list=(fitted?"貼身立領上衣":"圓弧肩線外套、內搭門襟、雙翻領與立領")+"、拉鍊與拉片、腰帶與扣環、單一胸袋（袋蓋＋開口）、袖口／護腕、手套、靴筒／鞋面、鞋底與鞋帶";
            if(d.Hem>.01f)list+="、分襟下擺與內裡／滾邊";
            if(d.Cut==0||d.Cut==1||d.Cut==7||d.Cut==10||d.Cut==11||d.Cut==16)list+="、左腿工裝袋（袋蓋＋開口）";
            if(d.Cut==0||d.Cut==5||d.Cut==7||d.Cut==11)list+="、有厚度的弧形護膝";
            if(d.Cut==0||d.Cut==5||d.Cut==6||d.Cut==14)list+="、左側弧形肩甲";
            if(d.Cut==2||d.Cut==9||d.Cut==10)list+="、後垂兜帽";
            if(d.Cut==6)list+="、扣帶皮革護胸／護腹裙與收納袋";
            if(d.Cut==16||d.Cut==24||d.Cut==22||d.Cut==25)list+="、有肩帶與袋蓋的工作圍裙";
            if(d.Cut==8||d.Cut==19||d.Cut==21||d.Cut==26)list+="、左肩披片與扣環";
            if(d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==14)list+="、領襟十字徽章";
            return list;
        }
        static void Pocket(Builder b,float x,float y,float z,float w,float h,Color cloth,Color trim)
        {
            b.Badge(x,y,z,w,h,cloth);
            b.Panel(new Vector3(x-w*.5f,y+h*.54f,z+.010f),new Vector3(x+w*.5f,y+h*.54f,z+.010f),new Vector3(x-w*.47f,y+h*.27f,z+.014f),new Vector3(x+w*.47f,y+h*.27f,z+.014f),cloth,0,1,2);
            b.Seam(new Vector3(x-w*.40f,y+h*.25f,z+.018f),new Vector3(x+w*.40f,y+h*.25f,z+.018f),.003f,trim);
        }
        static void KneeGuard(Builder b,Color cloth,Color trim)
        {
            // Curved, thick strap-backed pad rather than a flat contrasting sticker.
            b.Loft(new[]{-.015f,-.045f,-.08f},new[]{.047f,.052f,.043f},new[]{.032f,.038f,.027f},cloth,0,.040f);
            b.Seam(new Vector3(-.025f,-.055f,.076f),new Vector3(.025f,-.055f,.076f),.004f,trim);
        }
        static void BuildTorso(Builder b,Design d,Color skin,bool bare)
        {
            float bulk=Strength(d),fit=d.Cut==3||d.Cut==11||d.Cut==12?.94f:1;
            // Neck -> trapezius -> round shoulder; ribs, waist, pelvis and rear gluteal curve.
            var ys=new[]{.565f,.535f,.515f,.49f,.43f,.34f,.24f,.14f,.055f,-.01f};
            var rx=new[]{.067f,.090f,.158f,.194f,.185f,.170f,.143f,.139f,.153f,.166f};
            var rz=new[]{.061f,.069f,.085f,.094f,.113f,.108f,.096f,.091f,.103f,.108f};
            var path=new Vector3[ys.Length];
            for(int i=0;i<ys.Length;i++){rx[i]*=(.94f+.06f*bulk)*fit;path[i]=new Vector3(0,ys[i],i>=7?-.012f:i==4?.012f:0);if(!bare){rx[i]+=.008f;rz[i]+=.007f;}}
            b.Tube(path,rx,rz,bare?skin:d.Main,12);if(bare)return;
            bool fitted=d.Cut==3||d.Cut==11||d.Cut==12;
            // Inner shirt seen through a real jacket opening; restrained single lapel on either side.
            if(!fitted)
            {
                b.Panel(new Vector3(-.050f,.475f,.116f),new Vector3(.050f,.475f,.116f),new Vector3(-.043f,.075f,.118f),new Vector3(.043f,.075f,.118f),d.Sub,.014f,5,3);
                b.Panel(new Vector3(-.085f,.51f,.088f),new Vector3(-.022f,.43f,.132f),new Vector3(-.085f,.34f,.126f),new Vector3(-.043f,.37f,.143f),d.Main,0,3,2);
                b.Panel(new Vector3(.022f,.43f,.132f),new Vector3(.085f,.51f,.088f),new Vector3(.043f,.37f,.143f),new Vector3(.085f,.34f,.126f),d.Main,0,3,2);
            }
            b.Loft(new[]{.575f,.535f},new[]{.073f,.084f},new[]{.066f,.074f},d.Sub);
            b.Seam(new Vector3(0,.43f,.145f),new Vector3(0,.09f,.125f),.0045f,d.Accent);
            b.Badge(.007f,.39f,.148f,.012f,.026f,d.Accent); // zipper pull
            Pocket(b,.105f,.31f,.124f,.060f,.065f,d.Main,d.Accent);
            b.Loft(new[]{.070f,.041f},new[]{.165f,.165f},new[]{.116f,.116f},d.Sub);
            b.Badge(.015f,.055f,.121f,.042f,.025f,d.Accent);
            if(d.Hem>.01f)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    float end=-d.Hem*(side<0?1:.80f),outer=side*(d.Cut==2||d.Cut==9||d.Cut==13?.26f:.215f);
                    b.Panel(new Vector3(side*.05f,.02f,.117f),new Vector3(side*.16f,.02f,.10f),new Vector3(side*.075f,end+.035f,.132f),new Vector3(outer,end,.07f),d.Main,.018f,5,3);
                    b.Seam(new Vector3(side*.16f,.02f,.112f),new Vector3(outer,end+.008f,.079f),.006f,d.Accent);
                }
                b.Panel(new Vector3(.16f,.02f,-.12f),new Vector3(-.16f,.02f,-.12f),new Vector3(.205f,-d.Hem*.8f,-.16f),new Vector3(-.22f,-d.Hem,-.17f),d.Main,-.025f,5,4);
            }
            if(d.Cut==2||d.Cut==9||d.Cut==10)b.Loft(new[]{.54f,.48f,.40f},new[]{.115f,.14f,.09f},new[]{.11f,.14f,.10f},d.Main,0,-.105f);
            if(d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==14){b.Seam(new Vector3(.09f,.40f,.134f),new Vector3(.09f,.33f,.137f),.009f,d.Accent);b.Seam(new Vector3(.065f,.375f,.138f),new Vector3(.115f,.375f,.138f),.009f,d.Accent);}
            if(d.Cut==6||d.Cut==16||d.Cut==24||d.Cut==22||d.Cut==25)
            {
                b.Panel(new Vector3(-.10f,.29f,.134f),new Vector3(.10f,.29f,.134f),new Vector3(-.15f,-.29f,.145f),new Vector3(.15f,-.27f,.145f),d.Sub,.010f,4,3);
                b.Seam(new Vector3(-.085f,.29f,.14f),new Vector3(-.12f,.45f,.125f),.012f,d.Sub);b.Seam(new Vector3(.085f,.29f,.14f),new Vector3(.12f,.45f,.125f),.012f,d.Sub);
                Pocket(b,0,-.13f,.160f,.12f,.075f,d.Sub,d.Accent);
            }
            if(d.Cut==8||d.Cut==19||d.Cut==21||d.Cut==26)
            {
                b.Panel(new Vector3(-.225f,.47f,.075f),new Vector3(-.09f,.53f,.086f),new Vector3(-.27f,.19f,.10f),new Vector3(-.05f,.24f,.145f),d.Main,.018f,4,3);
                b.Badge(-.12f,.45f,.124f,.028f,.030f,d.Accent);
            }
        }
        static void BuildArm(Builder b,Design d,Color skin,bool upper,bool left,bool anatomical=false)
        {
            float power=Strength(d),sleeve=d.Cut==2||d.Cut==9||d.Cut==13?1.18f:1;
            bool bare=anatomical||d.Cut==1||d.Cut==6||d.Cut==14||d.Cut==24||d.Cut==12;
            Color cloth=bare?skin:d.Main;
            if(upper)
            {
                var y=new[]{.026f,.005f,-.025f,-.075f,-.13f,-.19f,-.25f,-.28f};
                var rx=new[]{.020f,.045f,.068f,.065f,.060f,.051f,.039f,.034f};
                var rz=new[]{.019f,.043f,.064f,.060f,.065f,.056f,.037f,.033f};
                var path=new Vector3[y.Length];for(int i=0;i<y.Length;i++){rx[i]*=power*(bare?1:sleeve);rz[i]*=power*(bare?1:sleeve);float outset=d.Cut==3||d.Cut==11||d.Cut==12||d.Cut>=15?.025f:0;path[i]=new Vector3((left?-1:1)*outset*(i==2?1:i==3?.65f:i==4?.20f:0),y[i],i==4?.008f:i==3?-.004f:0);}
                b.Tube(path,rx,rz,cloth,12);
                if(!anatomical&&left&&(d.Cut==0||d.Cut==5||d.Cut==6||d.Cut==14))
                    b.Loft(new[]{-.015f,-.055f,-.10f},new[]{.058f*power,.077f*power,.066f*power},new[]{.056f*power,.073f*power,.064f*power},d.Sub);
                if(!anatomical&&!bare)b.Loft(new[]{-.245f,-.273f},new[]{.043f*power,.039f*power},new[]{.041f*power,.037f*power},d.Sub);
            }
            else
            {
                var y=new[]{.022f,0f,-.05f,-.09f,-.15f,-.21f,-.25f};
                var rx=new[]{.030f,.038f,.054f,.052f,.044f,.032f,.030f};
                var rz=new[]{.029f,.036f,.050f,.046f,.036f,.023f,.021f};
                var path=new Vector3[y.Length];for(int i=0;i<y.Length;i++){rx[i]*=power*(bare?1:sleeve);rz[i]*=power*(bare?1:sleeve);path[i]=new Vector3(0,y[i],i==2?.004f:0);}
                b.Tube(path,rx,rz,cloth,12);
                // A single fitted cuff follows the tapered wrist; no full-length colour strip.
                if(!anatomical)b.Loft(new[]{-.205f,-.235f},new[]{.035f*power,.033f*power},new[]{.027f*power,.025f*power},d.Sub);
                if(!bare)b.Seam(new Vector3(-.022f,-.025f,.041f),new Vector3(.018f,-.030f,.041f),.003f,d.Main*.85f);
            }
        }
        static void BuildHand(Builder b,Design d,Color skin,bool left,int pose)
        {
            float side=left?-1:1;bool covered=d.Cut==3||d.Cut==11||d.Cut==5||d.Cut==13;
            b.Loft(new[]{.012f,-.005f,-.037f},new[]{.029f,.035f,.034f},new[]{.021f,.020f,.017f},d.Sub); // palm and cuff
            for(int pair=-1;pair<=1;pair+=2)
            {
                float x=pair*.018f,end=pose==2?-.101f:pose==1?-.055f:-.080f;
                var path=new[]{new Vector3(x,-.032f,.001f),new Vector3(x*(pose==2?1.22f:1),-.053f,pose==1?.020f:.005f),new Vector3(x*(pose==2?1.45f:.90f),end,pose==1?.037f:pose==0?.020f:.002f)};
                b.Tube(path,new[]{.016f,.015f,.011f},new[]{.016f,.014f,.010f},covered?d.Sub:skin,8);
            }
            var thumb=new[]{new Vector3(side*.029f,-.012f,.001f),new Vector3(side*.047f,-.029f,.012f),new Vector3(side*(pose==2?.062f:.031f),pose==2?-.045f:-.050f,pose==1?.038f:.021f)};
            b.Tube(thumb,new[]{.013f,.013f,.010f},new[]{.013f,.012f,.009f},covered?d.Sub:skin,8);
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
