using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    /// <summary>眼型與髮型的共用美術表；戰鬥、居民、頭像讀取相同資料。</summary>
    internal static class CharacterLook
    {
        internal enum EyeKind { Upturned,Droop,Almond,Round,Slender,HalfClosed,Sharp,Sanpaku }
        internal enum HairKind { Crop,Spikes,SidePart,Bob,Straight,HighPony,LowPony,Twins,Bun,Slick,Wolf,Crew,Braid,LongSide }
        internal sealed class Look
        {
            internal EyeKind Eye;internal HairKind Hair;internal Color Iris,HairColor;internal float Pupil,Tail,Part,Length;internal int Seed;
            internal Look(EyeKind eye,HairKind hair,string iris,string color,float pupil,float tail,float part,float length,int seed)
            {Eye=eye;Hair=hair;Iris=Hex(iris);HairColor=Hex(color);Pupil=pupil;Tail=tail;Part=part;Length=length;Seed=seed;}
        }
        static readonly Dictionary<string,Look> all=new Dictionary<string,Look>
        {
            {"swordsman",new Look(EyeKind.Sharp,HairKind.Crop,"668cc7","704a32",.36f,.3f,-.35f,.07f,1)},
            {"archer",new Look(EyeKind.Slender,HairKind.HighPony,"839957","78462e",.39f,.2f,.35f,.24f,2)},
            {"mage",new Look(EyeKind.Upturned,HairKind.SidePart,"b18bcf","d8d9e8",.38f,.7f,-.55f,.09f,3)},
            {"assassin",new Look(EyeKind.Sanpaku,HairKind.Wolf,"bd7897","4f355e",.35f,.5f,.45f,.19f,4)},
            {"priest",new Look(EyeKind.Droop,HairKind.Straight,"69a9a4","e4c785",.44f,-.6f,0,.25f,5)},
            {"novice",new Look(EyeKind.Round,HairKind.Crop,"829c63","765039",.43f,.12f,.28f,.085f,13)},   // 初心者：圓眼、短髮
            {"paladin",new Look(EyeKind.Almond,HairKind.Slick,"b29547","b28a53",.40f,.1f,-.2f,.08f,6)},
            {"berserker",new Look(EyeKind.Sanpaku,HairKind.Spikes,"cf7859","783c2e",.35f,.6f,.15f,.11f,7)},
            {"marksman",new Look(EyeKind.Slender,HairKind.LowPony,"b69b65","827b65",.35f,.1f,-.6f,.21f,8)},
            {"ranger",new Look(EyeKind.Round,HairKind.Wolf,"64bfa0","ad683d",.43f,.35f,-.4f,.17f,9)},
            {"archmage",new Look(EyeKind.HalfClosed,HairKind.LongSide,"9c7bc7","c2b6db",.38f,.4f,.65f,.30f,10)},
            {"summoner",new Look(EyeKind.Round,HairKind.Twins,"da9a52","a95732",.45f,.2f,-.15f,.19f,11)},
            {"ninja",new Look(EyeKind.Sharp,HairKind.Bun,"8097ac","304353",.35f,.1f,.2f,.08f,12)},
            {"shadowdancer",new Look(EyeKind.Upturned,HairKind.Bob,"d980b7","613c78",.40f,.9f,-.7f,.16f,13)},
            {"bishop",new Look(EyeKind.Droop,HairKind.Crew,"82aaa0","dcd5b9",.42f,-.3f,.3f,.04f,14)},
            {"inquisitor",new Look(EyeKind.HalfClosed,HairKind.Crop,"b29356","b9a16b",.35f,.35f,.6f,.055f,15)},
            {"嚮導",new Look(EyeKind.HalfClosed,HairKind.SidePart,"8a9f64","70513e",.37f,.2f,-.5f,.11f,41)},
            {"教官",new Look(EyeKind.Sharp,HairKind.Crew,"6d939a","554a43",.34f,.45f,.2f,.035f,42)},
            {"懸賞官",new Look(EyeKind.Almond,HairKind.Slick,"a078b3","403750",.39f,.1f,-.6f,.10f,43)},
            {"播報員",new Look(EyeKind.Round,HairKind.HighPony,"58bfc4","bc6441",.45f,.3f,.4f,.23f,44)},
            {"塔守",new Look(EyeKind.HalfClosed,HairKind.LongSide,"aa9ad9","cfccde",.36f,-.2f,.6f,.38f,45)},
            {"老焰",new Look(EyeKind.HalfClosed,HairKind.Bun,"b9954e","f3f3ed",.42f,-.2f,0,.035f,16)},
            {"小鐵",new Look(EyeKind.Sharp,HairKind.Spikes,"e0ac39","6c3b27",.36f,.65f,.25f,.14f,17)},
            {"米菈",new Look(EyeKind.Slender,HairKind.Bob,"ac6b3c","553354",.38f,.7f,.6f,.19f,18)},
            {"翠",new Look(EyeKind.HalfClosed,HairKind.Straight,"488e66","e3edd4",.39f,.0f,0,1.55f,19)},
            {"楓",new Look(EyeKind.Upturned,HairKind.Wolf,"dbb254","a75b35",.36f,.45f,-.5f,.20f,20)},
            {"小葉",new Look(EyeKind.Round,HairKind.Twins,"76bc78","83d34a",.45f,-.1f,.2f,.55f,21)},
            {"海斗",new Look(EyeKind.Almond,HairKind.Crop,"d5a34b","253e59",.43f,.4f,-.4f,.10f,22)},
            {"凪",new Look(EyeKind.Round,HairKind.Bob,"8d394a","812c48",.44f,-.6f,-.5f,.22f,23)},
            {"阿潮",new Look(EyeKind.HalfClosed,HairKind.Crop,"466b70","d4d7cb",.40f,-.3f,.5f,.05f,24)},
            {"鐵匠",new Look(EyeKind.Sharp,HairKind.Crop,"b88943","795341",.37f,.25f,-.2f,.085f,25)},
            {"店員",new Look(EyeKind.Round,HairKind.LowPony,"73915c","78533e",.45f,-.35f,.55f,.16f,26)},
            {"公會長",new Look(EyeKind.Sanpaku,HairKind.SidePart,"b09460","d7cebb",.35f,.25f,-.65f,.065f,27)},
            {"艾琳",new Look(EyeKind.Round,HairKind.Bob,"bd79ff","ded6f3",.45f,.1f,-.25f,.29f,28)},
            {"澪",new Look(EyeKind.HalfClosed,HairKind.Straight,"91def5","bbecf5",.40f,-.4f,0,1.7f,29)}
        };
        internal static string ClassId(string name)
        {
            switch(name)
            {
                case "初心者":return "novice";case "劍士":return "swordsman";case "獵人":return "archer";case "法師":return "mage";case "刺客":return "assassin";case "牧師":return "priest";
                case "聖騎士":return "paladin";case "狂戰士":return "berserker";case "神射手":return "marksman";case "遊俠":return "ranger";case "大魔導士":return "archmage";
                case "召喚師":return "summoner";case "忍者":return "ninja";case "影舞者":return "shadowdancer";case "主教":return "bishop";case "審判者":return "inquisitor";
                default:return all.ContainsKey(name??"")&&name.Length>0&&name[0]<128?name:null;
            }
        }
        internal static Look Get(string key)=>all.TryGetValue(Portraits.Key(key),out var look)?look:all["swordsman"];
        internal static Color Hex(string hex){int n=Convert.ToInt32(hex,16);return new Color(((n>>16)&255)/255f,((n>>8)&255)/255f,(n&255)/255f);}
        internal static IEnumerable<KeyValuePair<string,Look>> All=>all;
    }
    /// <summary>透明底眼睛像素，兩眼共用光源位置；同一畫法也直接用在 2D 頭像。</summary>
    internal static class EyeArt
    {
        internal const int W=128,H=96;
        static readonly Dictionary<string,Color32[]> pixels=new Dictionary<string,Color32[]>();
        static float Clamp(float x)=>Math.Max(0,Math.Min(1,x));
        static float Mix(float a,float b,float t)=>a+(b-a)*t;
        static bool Oval(float x,float y,float cx,float cy,float rx,float ry)=>((x-cx)*(x-cx)/(rx*rx)+(y-cy)*(y-cy)/(ry*ry))<1;
        internal static Color32[] Render(string id,int side,int glance=0)
        {
            if(StoryCostume.Has(id)||id=="澪")return StoryEyes.Render(id,side,glance);
            string key=id+side+"/"+glance;if(pixels.TryGetValue(key,out var cached))return cached;
            var look=CharacterLook.Get(id);var persona=Portraits.PersonaFor(id);float pupil=look.Pupil*persona.Pupil;float gaze=persona.Gaze*.14f+glance*.085f;var iris=look.Iris;var result=new Color32[W*H];
            for(int py=0;py<H;py++)for(int px=0;px<W;px++)
            {
                float red=0,green=0,blue=0,alpha=0;
                for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++)
                {
                    float x=((px+(sx+.5f)*.5f)/W)*2-1,y=((py+(sy+.5f)*.5f)/H)*2-1;
                    float u=x/.72f,arch=(float)Math.Sqrt(Math.Max(0,1-u*u)),tilt=look.Tail*.12f*side*u;
                    float top=.46f*arch+tilt,bottom=-.43f*arch+tilt,iy=.14f,rx=.36f,ry=.50f;
                    switch(look.Eye)
                    {
                        case CharacterLook.EyeKind.Upturned:top=.40f*arch+tilt;bottom=-.37f*arch+tilt;break;
                        case CharacterLook.EyeKind.Droop:top=.42f*arch+tilt;iy=.015f;break;
                        case CharacterLook.EyeKind.Round:top=.50f*arch+tilt;rx=.39f;ry=.53f;break;
                        case CharacterLook.EyeKind.Slender:top=.31f*arch+tilt;bottom=-.31f*arch+tilt;iy=.12f;break;
                        case CharacterLook.EyeKind.HalfClosed:top=.10f*arch+tilt;bottom=-.36f*arch+tilt;iy=.15f;break;
                        case CharacterLook.EyeKind.Sharp:top=.36f*arch+tilt;bottom=-.33f*arch+tilt;rx=.32f;ry=.46f;break;
                        case CharacterLook.EyeKind.Sanpaku:top=.37f*arch+tilt;iy=.27f;rx=.32f;ry=.42f;break;
                    }
                    float r=0,g=0,b=0,a=0;
                    if(Math.Abs(u)<=1&&y<top&&y>bottom)
                    {
                        float shade=Clamp((y-bottom)/(top-bottom));r=Mix(.98f,.83f,shade*shade*.55f);g=Mix(.97f,.86f,shade*shade*.55f);b=Mix(.91f,.91f,shade*shade*.35f);a=1;
                        float d=(x-gaze)*(x-gaze)/(rx*rx)+(y-iy)*(y-iy)/(ry*ry);
                        if(d<1)
                        {
                            float grad=Clamp((iy+ry-y)/(ry*2));r=Mix(.12f,Math.Min(1,iris.r*1.12f+.08f),grad);g=Mix(.075f,Math.Min(1,iris.g*1.12f+.08f),grad);b=Mix(.15f,Math.Min(1,iris.b*1.12f+.08f),grad);
                            if(d>.82f){r*=.65f;g*=.65f;b*=.70f;}
                            if(d>.46f&&d<.78f&&y<iy-.22f){r=Mix(r,1,.23f);g=Mix(g,1,.23f);b=Mix(b,1,.23f);}
                            if(Oval(x,y,gaze,iy+.025f,rx*pupil,.31f)){r=.10f;g=.055f;b=.13f;}
                            if(Oval(x,y,gaze-.135f,Math.Min(iy+.17f,top-.11f),.087f,.070f)||Oval(x,y,gaze+.145f,Math.Max(iy-.23f,bottom+.055f),.032f,.028f)){r=1;g=1;b=.97f;}
                        }
                    }
                    // 上眼線中間厚、內側尖；外側眼尾獨立延伸，下眼線不封閉。
                    float outward=side*u,thick=.17f*(.25f+.75f*arch)*(outward>0?1.15f:.85f);
                    bool upper=Math.Abs(u)<1&&Math.Abs(y-top)<thick*.5f;
                    float ox=side*x,t=(ox-.68f)/.28f,ty=look.Tail*.12f+t*look.Tail*.16f;
                    bool tail=t>=0&&t<=1&&Math.Abs(y-ty)<.065f*(1-t);
                    if(upper||tail){r=.14f;g=.07f;b=.17f;a=1;}
                    else if(Math.Abs(u)<.93f&&outward>.34f&&Math.Abs(y-bottom)<.018f){r=.40f;g=.27f;b=.36f;a=.75f;}
                    else if((look.Eye==CharacterLook.EyeKind.Droop||look.Eye==CharacterLook.EyeKind.HalfClosed)&&Math.Abs(u)<.65f&&Math.Abs(y-top-.18f)<.012f){r=.48f;g=.30f;b=.33f;a=.50f;}
                    red+=r*a;green+=g*a;blue+=b*a;alpha+=a;
                }
                int k=py*W+px;if(alpha>0)result[k]=new Color32((byte)(Clamp(red/alpha)*255),(byte)(Clamp(green/alpha)*255),(byte)(Clamp(blue/alpha)*255),(byte)(Clamp(alpha*.25f)*255));
            }
            pixels[key]=result;return result;
        }
    }
}
