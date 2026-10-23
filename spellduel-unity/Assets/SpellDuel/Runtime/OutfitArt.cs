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
            {"novice",new Design("novice","短襟旅行布衣與捲袖","dfbd80","786044","d9ceaa",.065f,.065f,29)},   // 初心者：樸素的旅行布衣
            {"paladin",new Design("paladin","半披風白色軍裝","fff0d4","31569a","e8b94d",.42f,.075f,5)},
            {"berserker",new Design("berserker","青玉交領長劍袍","e7f1f4","397a96","75cdd0",.56f,.088f,30)},
            {"marksman",new Design("marksman","單肩狙擊長夾克","327db5","293e73","ebcb66",.44f,.063f,7)},
            {"ranger",new Design("ranger","斜肩山地短斗篷","1bb88d","245a4b","efb36a",.24f,.077f,8)},
            {"archmage",new Design("archmage","符文寬袖長風衣","8d42dd","403080","75fff1",.63f,.109f,9)},
            {"summoner",new Design("summoner","多口袋短帽衫","f58b2b","503e84","ceff53",.16f,.090f,10)},
            {"ninja",new Design("ninja","半袖交領忍者機能服","456db0","2c3874","d6b764",.11f,.065f,11)},
            {"shadowdancer",new Design("shadowdancer","露肩短裝與舞帶","d544b4","513181","62f5ef",.02f,.057f,12)},
            {"bishop",new Design("bishop","寬領儀式長外套","fff0d1","6056a3","dbb25c",.66f,.095f,13)},
            {"inquisitor",new Design("inquisitor","斜裁無袖審判長背心","efad25","39477d","efe1b1",.51f,.059f,14)},
            {"嚮導",new Design("嚮導","皮甲旅行外衣","73884b","815b40","dfb75f",.18f,.065f,17)},
            {"教官",new Design("教官","退役軍人無袖訓練服","37677d","566058","d9b46b",.05f,.060f,24)},
            {"懸賞官",new Design("懸賞官","書記長背心","6d5993","e4d8bb","d4b266",.32f,.065f,25)},
            {"播報員",new Design("播報員","雙色播報披肩","cf614b","36b8bb","f2ca61",.12f,.07f,17)},
            {"塔守",new Design("塔守","星燈守塔斗篷","69518f","49466d","d7c78b",.6f,.06f,26)},
            {"老焰",new Design("老焰","紅色守鐘工作長袍","bd3429","873425","e9bd72",.60f,.071f,15)},
            {"小鐵",new Design("小鐵","捲袖鐵工工作服","b57b45","684635","e9b85d",.08f,.088f,16)},
            {"米菈",new Design("米菈","多袋旅行長背心","13b4bd","55407a","dfb75f",.43f,.064f,17)},
            {"翠",new Design("翠","藤繡交領寬袖和服","8ca74c","34583a","cbbf79",.51f,.086f,18)},
            {"楓",new Design("楓","斜肩森林巡守衣","2c9a65","235e50","dfac61",.28f,.073f,19)},
            {"小葉",new Design("小葉","寬袖短上衣與百褶下擺","f2b935","477853","e5d8ac",.17f,.094f,20)},
            {"海斗",new Design("海斗","雙排金扣海軍短夾克","286fc0","293b80","d8c798",0f,.077f,21)},
            {"凪",new Design("凪","白圍裙粉紅洋裝","e66e9c","fff0e4","d3af62",.48f,.095f,22)},
            {"阿潮",new Design("阿潮","短袖海釣工作外套","189ca3","4b467c","dec28c",.12f,.080f,23)},
            {"鐵匠",new Design("鐵匠","厚肩無袖皮工作服","db7037","733d42","e5b975",.10f,.073f,24)},
            {"店員",new Design("店員","藥師翻領短外套","2cbcb3","fff0d2","e5bd64",.31f,.080f,25)},
            {"艾琳",new Design("艾琳","金邊抄寫員連帽長外套","513180","2f2559","e5b955",.48f,.075f,27)},
            {"澪",new Design("澪","潮汐觀測短外套","a8e5ef","79bccf","e3faff",.3f,.07f,28)},
            {"公會長",new Design("公會長","斜披肩公會長風衣","a32d56","3c467e","d7af5e",.60f,.084f,26)}
        };
        internal static Design Get(string id)=>designs.TryGetValue(Portraits.Key(id),out var d)?d:designs["swordsman"];
        internal static IEnumerable<KeyValuePair<string,Design>> All=>designs;
        static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        internal sealed class Builder
        {
            internal Color Skin;
            readonly Dictionary<int,Vector3> smoothNormals=new Dictionary<int,Vector3>();
            internal readonly List<Vector3> V=new List<Vector3>();internal readonly List<int> T=new List<int>();internal readonly List<Color> C=new List<Color>();
            void Vertex(Vector3 v,Color c){V.Add(v);C.Add(c);}
            internal void Tube(Vector3[] centers,float[] widths,float[] depths,Color color,int sides=10)
            {
                int start=V.Count;
                for(int j=0;j<centers.Length;j++)for(int i=0;i<sides;i++)
                {
                    float a=i*2*Mathf.PI/sides,fold=1;
                    float shade=1;
                    var paint=color*shade;paint.a=Mathf.Abs(color.r-Skin.r)+Mathf.Abs(color.g-Skin.g)+Mathf.Abs(color.b-Skin.b)<.001f?0:1;
                    Vertex(centers[j]+new Vector3(Mathf.Sin(a)*widths[j]*fold,0,Mathf.Cos(a)*depths[j]*fold),paint);
                    // Analytic ring normals: independent of triangle diagonals and cap fans.
                    int prev=j>0?j-1:0,next=j<centers.Length-1?j+1:centers.Length-1;
                    var along=centers[next]-centers[prev]+new Vector3(Mathf.Sin(a)*(widths[next]-widths[prev]),0,Mathf.Cos(a)*(depths[next]-depths[prev]));
                    var around=new Vector3(Mathf.Cos(a)*widths[j],0,-Mathf.Sin(a)*depths[j]);
                    smoothNormals[V.Count-1]=Vector3.Cross(along,around).normalized;
                    if(j==centers.Length-1)continue;int k=start+j*sides+i,n=start+j*sides+(i+1)%sides;T.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});
                }
                // Horizontal rings go down the limb. Reverse caps for a closed outward hull.
                for(int cap=0;cap<2;cap++){int k=start+(cap==0?0:(centers.Length-1)*sides);for(int i=1;i<sides-1;i++)T.AddRange(cap==0?new[]{k,k+i,k+i+1}:new[]{k,k+i+1,k+i});}
            }
            internal void Loft(float[] y,float[] rx,float[] rz,Color c,float x=0,float z=0,int sides=12)
            {var path=new Vector3[y.Length];for(int i=0;i<path.Length;i++)path[i]=new Vector3(x,y[i],z);Tube(path,rx,rz,c,sides);}
            internal void Panel(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float bulge=.014f,int rows=4,int cols=3)
            {
                int start=V.Count;
                for(int face=0;face<2;face++)for(int j=0;j<=rows;j++)for(int i=0;i<=cols;i++)
                {
                    float t=j/(float)rows,u=i/(float)cols;
                    // Inset only the four corner samples; the following edge samples form a bevel.
                    if(rows>1&&cols>1&&(j==0||j==rows)){if(i==0)u=.065f;else if(i==cols)u=.935f;}
                    var left=a*(1-t)+c*t;var right=b*(1-t)+d*t;
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
            internal void ClearSmoothNormals()=>smoothNormals.Clear();
            internal Mesh Finish(string name){var m=new Mesh{name=name};m.SetVertices(V);m.SetTriangles(T,0);m.colors=C.ToArray();m.RecalculateNormals();var normals=m.normals;foreach(var pair in smoothNormals)normals[pair.Key]=pair.Value;m.normals=normals;m.RecalculateBounds();return m;}
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

                float wide=d.Cut==10||d.Cut==20?.087f:d.Cut==3||d.Cut==12?.062f:.076f;
                b.Loft(new[]{.01f,-.06f,-.17f,-.29f,-.415f},new[]{wide*.90f,wide,wide*.94f,wide*.78f,wide*.67f},new[]{wide*.90f,wide*.94f,wide*.88f,wide*.76f,wide*.69f},d.Cut==12&&left?skin:sub);

            }
            else if(part.StartsWith("calf"))
            {


                bool sneaker=d.Cut==0||d.Cut==10,tabi=d.Cut==11,high=d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==12&&left;
                Color boot=high?main:sub;float soleWidth=tabi?.064f:sneaker?.088f:.079f,toe=tabi?.112f:sneaker?.142f:.136f;
                b.Loft(new[]{.005f,-.07f,-.15f,-.25f,-.34f},new[]{.051f,.069f,.067f,.049f,.045f},new[]{.051f,.065f,.06f,.046f,.045f},high?main:sub);
                if(d.Cut==0||d.Cut==5)KneeGuard(b,sub,accent);
                b.Loft(new[]{sneaker?-.33f:high?-.19f:-.285f,-.35f,-.385f,-.414f},new[]{high?.064f:.049f,.063f,soleWidth*.97f,soleWidth*.95f},new[]{.055f,.10f,toe*.985f,toe*.96f},boot,0,.038f);
                b.Loft(new[]{-.414f,-.430f},new[]{soleWidth,soleWidth},new[]{toe,toe},d.Id=="novice"?sub:accent,0,.038f);


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
            string list="輔色立領、細金屬拉鍊與圓頭拉片、整圈腰帶與唯一中央扣、完整袖口／手套邊環、手套、單色褲管、靴筒與鞋面、連續鞋底邊";
            if(d.Hem>.01f)list+="、連續圓弧衣襬";
            if(d.Cut==0||d.Cut==5)list+="、包覆弧形護膝";
            if(d.Cut==0||d.Cut==5||d.Cut==6)list+="、左側圓弧肩甲";
            if(d.Cut==2||d.Cut==9||d.Cut==10)list+="、後垂兜帽";
            if(d.Cut==6||d.Cut==16||d.Cut==24||d.Cut==22||d.Cut==25)list+="、完整肩帶工作圍裙與袋蓋口袋";
            if(d.Cut==8||d.Cut==19||d.Cut==21||d.Cut==26)list+="、圓角單肩披片與扣環";
            if(d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==14)list+="、十字徽章";
            return list;
        }
        static void Skirt(Builder b,Design d)
        {
            // Follow the hips below the belt. Short hems taper; long coats have only slight ease.
            const int sides=16,rows=5;int start=b.V.Count;
            for(int layer=0;layer<2;layer++)for(int j=0;j<=rows;j++)for(int i=0;i<sides;i++)
            {
                float a=i*2*Mathf.PI/sides,t=j/(float)rows;
                float ease=Mathf.Clamp01((d.Hem-.15f)/.50f),fall=t*t*(3-2*t);
                float rx=Mathf.Lerp(.174f,.170f+.020f*ease,fall)-layer*.005f;
                float rz=Mathf.Lerp(.119f,.123f+.008f*ease,fall)-layer*.005f;
                // Scale the front rise to garment length so a short hem cannot turn upward.
                float rise=Mathf.Min(.025f,d.Hem*.18f)*Mathf.Pow(Mathf.Max(0,Mathf.Cos(a)),4)*t*t;
                b.V.Add(new Vector3(Mathf.Sin(a)*rx,.025f-d.Hem*t+rise,Mathf.Cos(a)*rz-.012f));b.C.Add(d.Main);
                if(j==rows)continue;int k=start+layer*(rows+1)*sides+j*sides+i,n=start+layer*(rows+1)*sides+j*sides+(i+1)%sides;
                b.T.AddRange(layer==0?new[]{k,k+sides,n,n,k+sides,n+sides}:new[]{k,n,k+sides,n,n+sides,k+sides});
            }
            int stride=(rows+1)*sides;for(int i=0;i<sides;i++)foreach(int j in new[]{0,rows})
            {int k=start+j*sides+i,n=start+j*sides+(i+1)%sides;b.T.AddRange(j==0?new[]{k,n,k+stride,n,n+stride,k+stride}:new[]{k,k+stride,n,n,k+stride,n+stride});}
        }
        static void Pocket(Builder b,float x,float y,float z,float w,float h,Color cloth,Color trim)
        {
            b.Badge(x,y,z,w,h,cloth);
            b.Panel(new Vector3(x-w*.5f,y+h*.54f,z+.010f),new Vector3(x+w*.5f,y+h*.54f,z+.010f),new Vector3(x-w*.47f,y+h*.27f,z+.014f),new Vector3(x+w*.47f,y+h*.27f,z+.014f),cloth,0,1,2);

        }
        static void KneeGuard(Builder b,Color cloth,Color trim)
        {
            // Curved, thick strap-backed pad rather than a flat contrasting sticker.
            b.Loft(new[]{.005f,-.015f,-.045f,-.075f,-.09f},new[]{.043f,.058f,.063f,.054f,.039f},new[]{.052f,.062f,.067f,.058f,.048f},cloth,0,.007f);

        }
        static void BuildTorso(Builder b,Design d,Color skin,bool bare)
        {
            float bulk=Strength(d),fit=d.Cut==3||d.Cut==11||d.Cut==12?.94f:1;
            // Neck -> trapezius -> round shoulder; ribs, waist, pelvis and rear gluteal curve.
            var ys=new[]{.565f,.535f,.505f,.475f,.43f,.34f,.24f,.14f,.055f,-.01f};
            var rx=new[]{.067f,.090f,.137f,.175f,.185f,.170f,.143f,.139f,.153f,.166f};
            var rz=new[]{.061f,.069f,.085f,.094f,.113f,.108f,.096f,.091f,.103f,.108f};
            var path=new Vector3[ys.Length];
            for(int i=0;i<ys.Length;i++){rx[i]*=(.94f+.06f*bulk)*fit;path[i]=new Vector3(0,ys[i],i>=7?-.012f:i==4?.012f:0);if(!bare){rx[i]+=.008f;rz[i]+=.007f;}}
            b.Tube(path,rx,rz,bare?skin:d.Main,12);if(bare)return;
            // One closed jacket: no overlapping dark shirt/tie or floating chest marks.
            b.Loft(new[]{.575f,.555f,.535f},new[]{.073f,.080f,.084f},new[]{.066f,.071f,.074f},d.Sub);
            if(d.Id!="novice"&&d.Id!="berserker") {
            b.Tube(new[]{new Vector3(0,.43f,.133f),new Vector3(0,.34f,.126f),new Vector3(0,.24f,.114f),new Vector3(0,.14f,.097f),new Vector3(0,.09f,.100f)},new[]{.0025f,.0025f,.0025f,.0025f,.0025f},new[]{.002f,.002f,.002f,.002f,.002f},d.Accent,6);
            b.Loft(new[]{.405f,.392f,.379f},new[]{.005f,.007f,.005f},new[]{.003f,.004f,.003f},d.Accent,0,.134f,8); // rounded zipper pull
            }
            else if(d.Id=="novice") {
                // An overlapping cloth neckline and one linen tie, not a metal zipper.
                b.Seam(new Vector3(-.065f,.52f,.085f),new Vector3(.018f,.39f,.140f),.018f,d.Accent);
                b.Seam(new Vector3(.018f,.39f,.140f),new Vector3(.045f,.34f,.138f),.008f,d.Sub);
            }
            b.Loft(new[]{.070f,.041f},new[]{.171f,.176f},new[]{.120f,.123f},d.Sub,0,-.012f);
            b.Loft(new[]{.068f,.055f,.042f},new[]{.018f,.022f,.018f},new[]{.005f,.007f,.005f},d.Accent,0,.116f,8); // one central buckle
            if(d.Hem>.01f)Skirt(b,d);
            if(d.Cut==2||d.Cut==9||d.Cut==10)b.Loft(new[]{.54f,.48f,.40f},new[]{.115f,.14f,.09f},new[]{.11f,.14f,.10f},d.Main,0,-.105f);
            if(d.Cut==4||d.Cut==5||d.Cut==13||d.Cut==14){b.Seam(new Vector3(.09f,.40f,.134f),new Vector3(.09f,.33f,.137f),.009f,d.Accent);b.Seam(new Vector3(.065f,.375f,.138f),new Vector3(.115f,.375f,.138f),.009f,d.Accent);}
            if(d.Cut==6||d.Cut==16||d.Cut==24||d.Cut==22||d.Cut==25)
            {
                b.Panel(new Vector3(-.10f,.29f,.134f),new Vector3(.10f,.29f,.134f),new Vector3(-.15f,-.29f,.145f),new Vector3(.15f,-.27f,.145f),d.Sub,.010f,4,3);
                b.Seam(new Vector3(-.085f,.29f,.14f),new Vector3(-.12f,.45f,.125f),.012f,d.Sub);b.Seam(new Vector3(.085f,.29f,.14f),new Vector3(.12f,.45f,.125f),.012f,d.Sub);
                Pocket(b,0,-.13f,.160f,.12f,.075f,d.Sub,d.Accent);
            }
            if(d.Id=="berserker")
            {
                // Crossed lapels follow the chest; both are functional cloth edges.
                b.Seam(new Vector3(-.068f,.54f,.080f),new Vector3(.085f,.32f,.143f),.026f,d.Sub);
                b.Seam(new Vector3(.068f,.54f,.080f),new Vector3(-.045f,.38f,.146f),.017f,d.Accent);
                b.Loft(new[]{.098f,.068f,.038f},new[]{.172f,.174f,.174f},new[]{.121f,.122f,.124f},d.Sub,0,-.012f);
            }
            if(d.Cut==8||d.Cut==19||d.Cut==21||d.Cut==26)
            {
                b.Panel(new Vector3(-.225f,.47f,.075f),new Vector3(-.09f,.53f,.086f),new Vector3(-.27f,.19f,.10f),new Vector3(-.05f,.24f,.145f),d.Main,.018f,4,3);
                b.Badge(-.12f,.45f,.124f,.028f,.030f,d.Accent);
            }
        }
        static void BuildArm(Builder b,Design d,Color skin,bool upper,bool left,bool anatomical=false)
        {
            float power=Strength(d),sleeve=d.Id=="berserker"?1.17f:d.Cut==2||d.Cut==9||d.Cut==13?1.18f:1;
            bool bare=anatomical||d.Cut==1||d.Cut==6||d.Cut==14||d.Cut==24||d.Cut==12;
            Color cloth=bare||d.Id=="novice"&&!upper?skin:d.Main;
            if(upper)
            {
                var y=new[]{.026f,.005f,-.025f,-.075f,-.13f,-.19f,-.25f,-.28f};
                var rx=new[]{.015f,.033f,.058f,.065f,.060f,.051f,.039f,.034f};
                var rz=new[]{.015f,.032f,.055f,.060f,.065f,.056f,.037f,.033f};
                var path=new Vector3[y.Length];for(int i=0;i<y.Length;i++){rx[i]*=power*(bare?1:sleeve);rz[i]*=power*(bare?1:sleeve);float outset=d.Cut==3||d.Cut==11||d.Cut==12||d.Cut>=15?.012f:0;path[i]=new Vector3((left?-1:1)*outset*(i==2?1:i==3?.65f:i==4?.20f:0),y[i],i==4?.008f:i==3?-.004f:0);}
                if(d.Cut!=0&&d.Cut!=5&&d.Cut!=6)for(int i=0;i<4;i++){rx[i]*=.80f;rz[i]*=.88f;}
                b.Tube(path,rx,rz,cloth,12);
                if(!anatomical&&left&&(d.Cut==0||d.Cut==5||d.Cut==6))
                    b.Loft(new[]{-.010f,-.030f,-.055f,-.085f,-.115f},new[]{.030f*power,.050f*power,.072f*power,.073f*power,.060f*power},new[]{.046f*power,.062f*power,.071f*power,.069f*power,.063f*power},d.Sub);
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
                if(!anatomical&&d.Id!="novice")b.Loft(new[]{-.205f,-.235f},new[]{.039f*power,.035f*power},new[]{.031f*power,.027f*power},d.Sub);

            }
        }
        static void BuildHand(Builder b,Design d,Color skin,bool left,int pose)
        {
            float side=left?-1:1;bool covered=d.Cut==3||d.Cut==11||d.Cut==5||d.Cut==13;
            b.Loft(new[]{.012f,-.005f,-.037f},new[]{.029f,.035f,.034f},new[]{.021f,.020f,.017f},d.Sub,0,0,10); // palm and cuff
            bool swordSign=d.Id=="berserker"&&left;
            if(swordSign)for(int finger=0;finger<4;finger++)
            {
                float x=-.027f+finger*.018f;bool straight=finger<2;
                b.Tube(new[]{new Vector3(x,-.030f,0),new Vector3(x,-.060f,straight?0:.025f),new Vector3(x,straight?-.115f:-.052f,straight?-.003f:.043f)},new[]{.010f,.009f,.007f},new[]{.010f,.009f,.007f},skin,8);
            }
            if(!swordSign)for(int pair=-1;pair<=1;pair+=2)
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
