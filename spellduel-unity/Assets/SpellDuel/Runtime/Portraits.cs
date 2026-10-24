using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpellDuel
{
    /// <summary>
    /// 對話框旁的說話者頭像：全部用程式即時畫出來（不需要圖檔）。
    ///   Get(speaker, tag) 依說話者名字（劇本 story.txt、城鎮居民、公會長…）取得頭像；
    ///   還沒有專屬畫法的人用「預設頭像」：名牌顏色（Story.TagColor）的圓角方框＋半身剪影。
    /// 專屬頭像以輪廓、賽璐珞色塊與配件區分角色；框線沿用名牌顏色。
    /// Render() 共用程序眼像素與髮型網格（離線測試需 Unity API 替身）；Get() 建立頭像 Texture2D 並快取。像素第 0 列是最下面。
    /// </summary>
    public static class Portraits
    {
        public const int Size = 128;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        /// <summary>居民名字「村長・老焰」→「老焰」（和劇本裡同一個人共用頭像）</summary>
        public static string Key(string speaker)
        {
            speaker = speaker ?? "";
            int i = speaker.LastIndexOf('・');
            return i >= 0 ? speaker.Substring(i + 1).Trim() : speaker.Trim();
        }

        public static Texture2D Get(string speaker, string tag)
        {
            speaker = Key(speaker);
            string key = (speaker ?? "") + "|" + (tag ?? "");
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Portrait " + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var c = Story.TagColor(tag);
            t.SetPixels32(Render(speaker ?? "", c.r, c.g, c.b, Size));
            t.Apply(false, true);
            cache[key] = t;
            return t;
        }

        /// <summary>畫出頭像像素（size×size，第 0 列在下）；r,g,b＝名牌顏色</summary>
        public static Color32[] Render(string speaker,float r,float g,float b,int size)
        {
            size=Math.Max(8,Math.Min(512,size));string key=Key(speaker);bool human=CharacterLook.ClassId(key)!=null;
            if(!human)foreach(var look in CharacterLook.All)if(look.Key==key){human=true;break;}
            if(!human)return RenderCore(key,r,g,b,size);
            int high=Math.Min(1024,size*3);var pixels=RenderCore(key,r,g,b,high);var result=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int x0=x*high/size,x1=(x+1)*high/size,y0=y*high/size,y1=(y+1)*high/size;float red=0,green=0,blue=0,alpha=0,count=0;
                for(int yy=y0;yy<y1;yy++)for(int xx=x0;xx<x1;xx++){var c=pixels[yy*high+xx];red+=c.r*c.a;green+=c.g*c.a;blue+=c.b*c.a;alpha+=c.a;count++;}
                result[y*size+x]=new Color32((byte)(alpha>0?red/alpha:0),(byte)(alpha>0?green/alpha:0),(byte)(alpha>0?blue/alpha:0),(byte)(alpha/count));
            }
            return result;
        }
        static Color32[] RenderCore(string speaker,float r,float g,float b,int size)
        {
            size = Math.Max(8, Math.Min(1024, size));
            speaker = Key(speaker);
            var canvas = new Canvas(size, r, g, b) { persona=PersonaFor(speaker), posing=true, identity=speaker };
            switch (speaker)
            {
                case "嚮導": case "教官": case "懸賞官": case "播報員": case "塔守": FacilityPortrait(canvas,speaker); break;
                case "艾琳": Erin(canvas); break;
                case "老焰": OldFlame(canvas); break;
                case "小鐵": LittleSmith(canvas); break;
                case "米菈": Mila(canvas); break;
                case "翠": Cui(canvas); break;
                case "楓": Maple(canvas); break;
                case "小葉": LittleLeaf(canvas); break;
                case "海斗": Kaito(canvas); break;
                case "凪": Nagi(canvas); break;
                case "阿潮": Tide(canvas); break;
                case "澪": Mio(canvas); break;
                case "公會長": Guildmaster(canvas); break;
                case "鐵匠": Smith(canvas); break;
                case "店員": Clerk(canvas); break;
                case "熔岩巨像": LavaGolem(canvas); break;
                case "腐化樹王": Treant(canvas); break;
                case "深海巨妖": SeaDemon(canvas); break;
                case "深淵海妖": SeaDemon(canvas); break;
                default:
                    string cls=CharacterLook.ClassId(speaker);
                    if(cls!=null){canvas.identity=cls;canvas.persona=PersonaFor(cls);ClassPortrait(canvas,cls);canvas.Headwear();return canvas.Output();}
                    var px = new Color32[size * size];
                    Default(px, size, r, g, b, Seed(speaker));
                    return px;
            }
            canvas.Headwear();return canvas.Output();
        }

        // One art specification is shared by dialogue portraits, villagers and combat rigs.
        // Width/Jaw/Length/Eye are proportions; Tilt/Brow/Asym shape the features.
        // Gaze is lateral; Pitch/Yaw/Roll/Lean are pose offsets in degrees.
        internal struct Persona
        {
            internal float Width,Jaw,Length,Eye,Tilt,Gaze,Brow,Asym,Smile,Open,Pitch,Yaw,Roll,Lean,Lid,Pupil;
            internal bool Story,SoftNose,Masculine,Elder;
            internal Persona(float width,float jaw,float length,float eye,float tilt,float gaze,float brow,float asym,float smile,float open,float pitch,float yaw,float roll,float lean,float lid=0,float pupil=1)
            {
                Story=SoftNose=Masculine=Elder=false;
                // 在正常人類比例內保留個性，不再用極端縮眼或歪頭區分角色。
                Width=Clamp(width,.95f,1.05f);Jaw=Clamp(jaw,.94f,1.06f);Length=Clamp(length,.95f,1.05f);
                Eye=Clamp(eye,.85f,1.15f);Tilt=Clamp(tilt,-6,6);Gaze=Clamp(gaze,-.22f,.22f);
                Brow=Clamp(brow,-6,6);Asym=Clamp(asym,-3,3);Smile=Clamp(smile,-.6f,1);Open=Clamp(open,0,.8f);
                Pitch=Clamp(pitch,-6,6);Yaw=Clamp(yaw,-6,6);Roll=Clamp(roll,-6,6);Lean=lean;
                Lid=Clamp(lid,0,.12f);Pupil=Clamp(pupil,.85f,1.1f);
            }
            static float Clamp(float n,float lo,float hi)=>Math.Max(lo,Math.Min(hi,n));
        }
        // 共用解剖基準；3D 與頭像皆由此換算，單位與戰鬥頭模相同。
        internal struct FacePlan
        {
            internal float FaceWidth,Hairline,Top,Chin,EyeY,EyeX,EyeWidth,EyeHeight,NoseY,MouthY,MouthWidth,BrowY,EarTop,EarBottom;
            internal FacePlan(Persona p)
            {
                Top=.157f*p.Length;Chin=-Top;Hairline=.123f*p.Length;float height=Hairline-Chin;
                FaceWidth=.206f*p.Length*Lerp(.984f,1.012f,(p.Width-.95f)/.10f);
                EyeY=Chin+height*.465f;EyeWidth=FaceWidth*.25f;EyeX=EyeWidth;
                EyeHeight=EyeWidth*(p.Elder?.47f:p.Masculine?.55f:.70f+(p.Eye-1)*.15f);
                NoseY=Chin+height*.30f;MouthY=Chin+height*.17f;MouthWidth=EyeWidth*.60f;
                BrowY=EyeY+height*.07f;EarTop=BrowY;EarBottom=NoseY;

            }
        }
        internal static Persona PersonaFor(string key)
        {
            var p=BasePersonaFor(key);HumanPersonality.Apply(ref p,Key(key));p.Story=StoryCostume.Has(Key(key))||Key(key)=="澪";p.SoftNose=Key(key)=="艾琳"||Key(key)=="凪"||Key(key)=="小葉"||Key(key)=="澪";if(p.Story)p.Open*=.15f;
            string id=Key(key);p.Elder=id=="老焰"||id=="阿潮"||id=="公會長"||id=="塔守";
            p.Masculine=p.Elder||id=="海斗"||id=="小鐵"||id=="鐵匠"||id=="教官"||id=="swordsman"||id=="paladin"||id=="inquisitor";
            return p;
        }
        static Persona BasePersonaFor(string key)
        {
            switch(Key(key))
            {
                case "novice": return new Persona(1.01f,.975f,1f,1.02f,-1f,.04f,-2f,0f,.42f,0f,1f,-2f,-1f,1f,.025f,1f);
                case "嚮導": return new Persona(1f,1f,1f,0.92f,0f,0f,-1f,0f,0.3f,0f,0f,0f,0f,0f,.04f,1f);
                case "教官": return new Persona(1f,1f,1f,0.91f,0f,0f,3f,0f,0f,0f,0f,0f,0f,0f,.04f,1f);
                case "懸賞官": return new Persona(1f,1f,1f,0.96f,0f,0f,1f,0f,0.15f,0f,0f,0f,0f,0f,.04f,1f);
                case "播報員": return new Persona(1f,1f,1f,1.06f,0f,0f,-1f,0f,0.9f,0f,0f,0f,0f,0f,.04f,1f);
                case "塔守": return new Persona(1f,1f,1f,0.88f,0f,0f,-2f,0f,0.1f,0f,0f,0f,0f,0f,.04f,1f);
                case "艾琳": return new Persona(0.96f,0.944f,0.97f,1.14f,1.6f,0f,1.4f,0f,0.65f,0f,-0.8f,1.2f,-0.8f,0f,0f,1f);
                case "澪": return new Persona(0.972f,0.944f,1.016f,0.883f,-3.6f,-0.22f,-1.75f,0f,0f,0f,2.8f,-4.8f,0.8f,0f,0.09f,1f);
                case "老焰": return new Persona(1.016f,1f,1.004f,0.9064f,-2.8f,-0.08f,-4.55f,0f,0.22f,0f,2.4f,-2f,0f,3f,0.072f,1f);
                case "小鐵": return new Persona(1.028f,1.056f,0.994f,0.9636f,3.6f,0.2f,4.9f,1f,0.42f,0f,-0.4f,3.6f,-1.2f,5f,0.028f,0.85f);
                case "米菈": return new Persona(0.976f,0.94f,1.008f,0.9116f,4.8f,-0.22f,2.8f,3f,1f,0f,-1.2f,-5.2f,2.8f,1f,0.068f,1f);
                case "翠": return new Persona(0.956f,0.95f,1.024f,0.87f,0.8f,0.04f,0f,0f,0f,0f,-2.8f,3.2f,0f,-2f,0.1f,1f);
                case "楓": return new Persona(0.976f,0.976f,1f,0.9272f,3.2f,0.12f,1.75f,0f,0.36f,0f,0f,-3.2f,0.4f,7f,0.056f,1f);
                case "小葉": return new Persona(1.04f,1.04f,0.988f,1.117f,-2.4f,-0.12f,-4.2f,0.5f,0.15f,0.62f,1.6f,-2.8f,-3.6f,2f,0f,1.1f);
                case "海斗": return new Persona(1.032f,1.06f,1f,1f,2f,0f,3.5f,0f,0.9f,0.65f,-2.8f,2f,0f,-3f,0f,1f);
                case "凪": return new Persona(0.98f,0.96f,0.98f,1.10f,-4f,0.06f,-1.4f,0f,0.8f,0f,0.4f,-2.4f,1.2f,0f,0.028f,1f);
                case "阿潮": return new Persona(1.028f,1.01f,1.012f,0.85f,-2.4f,0.13f,-3.5f,0.5f,-0.25f,0f,2.4f,4f,0.4f,5f,0.12f,1f);
                case "鐵匠": return new Persona(1.048f,1.06f,0.996f,0.9272f,0.4f,0f,0f,0f,1f,0.8f,-2f,-1.6f,-0.8f,3f,0.056f,1f);
                case "店員": return new Persona(1.032f,1.03f,0.992f,1.065f,-2.8f,-0.12f,-3.5f,1f,0.55f,0.25f,1.2f,2f,-2f,2f,0f,1.1f);
                case "公會長": return new Persona(0.976f,1.024f,1.026f,0.8856f,2.8f,0f,0f,0f,0f,0f,-2.4f,0f,0f,-2f,0.088f,1f);
                case "swordsman": return new Persona(1.028f,1.06f,1f,0.948f,0.8f,0f,0f,0f,0f,0f,-0.8f,0f,0f,-1f,0.04f,1f);
                case "archer": return new Persona(0.976f,0.96f,1.006f,0.9142f,2.8f,0f,0.35f,0f,0.08f,0f,0f,-1.6f,0f,4f,0.066f,1f);
                case "mage": return new Persona(0.964f,0.94f,1.008f,0.9428f,4f,0.1f,3.15f,0.75f,0.8f,0f,-2f,2f,-0.8f,-2f,0.044f,1f);
                case "assassin": return new Persona(0.96f,0.94f,1.02f,0.8752f,4.8f,-0.22f,1.05f,3f,0.8f,0f,0.8f,-3.6f,1.2f,6f,0.096f,1f);
                case "priest": return new Persona(1.02f,1.016f,0.994f,1.013f,-4f,0f,-1.4f,0f,0.7f,0f,0.8f,0f,0.4f,0f,0f,1f);
                case "paladin": return new Persona(1.02f,1.06f,1.012f,0.9558f,-0.8f,0f,0f,0f,0.12f,0f,-2.4f,0f,0f,-3f,0.034f,1f);
                case "berserker": return new Persona(.985f,.975f,1.02f,.94f,1f,-.02f,0f,0f,-.06f,0f,-1f,-2f,.2f,0f,.075f,1f);
                case "marksman": return new Persona(0.96f,0.96f,1.016f,0.8648f,1.6f,0f,0.35f,0f,0f,0f,-2.4f,0f,0f,1f,0.104f,1f);
                case "ranger": return new Persona(0.992f,0.98f,0.996f,1.0208f,2.4f,0.06f,4.2f,0.75f,0.9f,0.5f,-0.4f,-2f,3.2f,3f,0f,1f);
                case "archmage": return new Persona(0.952f,0.94f,1.024f,0.8648f,3.6f,0f,2.8f,0f,0.4f,0f,-4.4f,2.4f,0f,-3f,0.104f,1f);
                case "summoner": return new Persona(0.996f,0.964f,0.996f,1.078f,1.2f,0.22f,1.4f,3f,0.85f,0.55f,-0.4f,-3.2f,-2.8f,3f,0f,1.1f);
                case "ninja": return new Persona(0.964f,0.96f,1.012f,0.883f,1.6f,0f,0f,0f,0f,0f,0.4f,0f,0f,5f,0.09f,1f);
                case "shadowdancer": return new Persona(0.96f,0.94f,1.01f,0.9168f,6f,-0.22f,3.5f,2.5f,1f,0f,-1.6f,4f,2.4f,1f,0.064f,1f);
                case "bishop": return new Persona(1.02f,1.01f,1.016f,0.9168f,-3.6f,0f,-1.75f,0f,0.6f,0f,1.2f,-1.6f,0f,0f,0.064f,1f);
                case "inquisitor": return new Persona(0.968f,1.01f,1.02f,0.8804f,4.8f,0f,6f,0f,-0.1f,0f,-2f,0f,0f,-2f,0.092f,0.85f);
                default:return new Persona(1f,1f,1f,1f,0f,0f,0f,0f,0f,0f,0f,0f,0f,0f,0f,1f);
            }
        }
        // SDF portrait canvas: cel-tone paint, ink contours and a one-pixel AA fringe.
        struct Paint
        {
            public float r, g, b;
            public Paint(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
            public static Paint Hex(string h)
            {
                int v = Convert.ToInt32(h.TrimStart('#'), 16);
                return new Paint(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
            }
            public static Paint Mix(Paint a, Paint b, float t) => new Paint(Lerp(a.r,b.r,t),Lerp(a.g,b.g,t),Lerp(a.b,b.b,t));
            public static Paint operator *(Paint a, float f) => new Paint(a.r*f,a.g*f,a.b*f);
        }
        static readonly Paint Ink = Paint.Hex("#171c30"), White = Paint.Hex("#fff8e8"), Gold = Paint.Hex("#ecc45f");
        delegate float Shape(float x, float y);
        sealed class Canvas
        {
            public readonly int n;
            public Persona persona=PersonaFor("");
            public bool posing;
            public string identity;
            readonly float[] red, green, blue;
            readonly Paint border;
            public Canvas(int n, float r, float g, float b)
            {
                this.n=n; border=new Paint(r,g,b);
                red=new float[n*n];green=new float[n*n];blue=new float[n*n];
                for(int y=0;y<n;y++) for(int x=0;x<n;x++)
                {
                    var c=Paint.Mix(Ink,border,0.14f+0.13f*(y/(float)n));
                    int k=y*n+x;red[k]=c.r;green[k]=c.g;blue[k]=c.b;
                }
                Line(-0.78f,-0.65f,0.58f,0.77f,0.006f,Paint.Mix(Ink,border,0.5f));
                Line(-0.49f,-0.74f,0.78f,0.54f,0.003f,Paint.Mix(Ink,border,0.35f));
            }
            // 像素畫法由 3D 與頭像共用，保留同一個光源下的高光方向。
            void Image(Color32[] pixels,int width,int height,float centerX,float centerY,float halfWidth,float halfHeight)
            {
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float u=(x+.5f)*2/n-1,v=(y+.5f)*2/n-1,sx=u,sy=v;
                    if(posing){float a=persona.Roll*.0174533f,ca=(float)Math.Cos(a),sa=(float)Math.Sin(a),dx=u-persona.Yaw*.0018f*(1+v),dy=v+.15f;sx=(dx*ca+dy*sa)/(1-Math.Abs(persona.Yaw)*.003f);sy=-dx*sa+dy*ca-.15f+persona.Pitch*.0015f;}
                    float tx=(sx-centerX)/(halfWidth*2)+.5f,ty=(sy-centerY)/(halfHeight*2)+.5f;
                    if(tx<0||tx>=1||ty<0||ty>=1)continue;var c=pixels[(int)(ty*height)*width+(int)(tx*width)];float alpha=c.a/255f;int k=y*n+x;
                    red[k]=Lerp(red[k],c.r/255f,alpha);green[k]=Lerp(green[k],c.g/255f,alpha);blue[k]=Lerp(blue[k],c.b/255f,alpha);
                }
            }
            public void DrawEye(int side,FacePlan plan)
            {
                if(identity=="澪"||identity=="翠")
                {
                    float x=side*plan.EyeX*3,y=plan.EyeY*3+.09f;
                    var c=identity=="澪"?Paint.Hex("#d8fbff"):Paint.Hex("#655848");
                    Line(x-.069f,y+.008f,x,y-.009f,.007f,c);Line(x,y-.009f,x+.069f,y+.008f,.007f,c);return;
                }
                Image(EyeArt.Render(identity,side),EyeArt.W,EyeArt.H,side*plan.EyeX*3,plan.EyeY*3+.09f,plan.EyeWidth*3/1.44f,plan.EyeHeight*3/.90f*(1-persona.Lid*1.6f));
            }
            public void StoryDress()
            {
                if(!StoryCostume.Has(identity)&&identity!="澪")return;
                var d=OutfitArt.Get(identity);var cloth=new Paint(d.Main.r,d.Main.g,d.Main.b);Paint cream=Paint.Hex("#fff5e8"),black=Paint.Hex("#252333");
                Poly(cloth,-.17f,-.48f,-.50f,-.59f,-.62f,-1,.62f,-1,.50f,-.59f,.17f,-.48f);
                if(identity=="艾琳"||identity=="老焰")
                {
                    Poly(identity=="艾琳"?cream:black,-.12f,-.50f,.12f,-.50f,.22f,-1,-.22f,-1);
                    for(int side=-1;side<=1;side+=2)Line(side*.13f,-.51f,side*.23f,-1,.011f,identity=="艾琳"?Gold:cloth);
                    if(identity=="艾琳"){Ring(0,-.48f,.14f,.035f,.023f,black);Gem(0,-.65f,.035f,Gold);}
                }
                else if(identity=="凪")
                {
                    for(int side=-1;side<=1;side+=2){Oval(side*.46f,-.66f,.12f,.13f,cream);Poly(cream,side*.11f,-.49f,0,-.60f,side*.19f,-.66f,side*.22f,-.53f);Line(side*.24f,-.57f,side*.20f,-1,.025f,cream);}
                    Poly(cream,-.20f,-.75f,.20f,-.75f,.26f,-1,-.26f,-1);Bow(0,-.63f,.09f,black);
                }
                else if(identity=="小鐵")
                {
                    Poly(cream,-.17f,-.48f,-.50f,-.59f,-.62f,-1,.62f,-1,.50f,-.59f,.17f,-.48f);
                    Poly(Paint.Hex("#966537"),-.25f,-.76f,.25f,-.76f,.30f,-1,-.30f,-1);
                    for(int side=-1;side<=1;side+=2)Line(side*.30f,-.58f,side*.22f,-1,.025f,Paint.Hex("#966537"));
                    Poly(Paint.Hex("#bf3434"),-.19f,-.48f,.19f,-.48f,.09f,-.69f,0,-.76f,-.10f,-.65f);
                }
                else if(identity=="海斗")
                {
                    for(int side=-1;side<=1;side+=2){Poly(Paint.Hex("#163d73"),side*.14f,-.49f,side*.29f,-.59f,side*.08f,-.77f,side*.02f,-.64f);Line(side*.34f,-.59f,side*.52f,-.65f,.025f,Gold);for(int i=0;i<3;i++)Oval(side*.11f,-.76f-i*.10f,.016f,.016f,Gold,.003f);}
                    Ring(.34f,-.73f,.023f,.024f,.006f,Gold);Line(.34f,-.75f,.34f,-.88f,.009f,Gold);Line(.27f,-.83f,.34f,-.88f,.009f,Gold);Line(.41f,-.83f,.34f,-.88f,.009f,Gold);
                }
                else if(identity=="翠")
                {
                    Line(-.16f,-.48f,.16f,-.88f,.044f,cream);Line(.16f,-.48f,-.16f,-.88f,.044f,cream);Box(0,-.96f,.33f,.065f,Paint.Hex("#245b43"));Gem(0,-.94f,.04f,Gold);
                }
                else if(identity=="小葉")
                {
                    for(int side=-1;side<=1;side+=2)Oval(side*.46f,-.79f,.12f,.18f,cream);Box(0,-.98f,.33f,.045f,black);
                }
                else if(identity=="澪")
                {
                    Box(0,-.49f,.15f,.06f,Paint.Hex("#cceef5"));
                    for(int i=0;i<9;i++){float x=-.20f+i*.05f;Oval(x,-.59f-Math.Abs(x)*.20f,.027f,.035f,White,.003f);Ring(x,-.67f,.018f,.024f,.004f,White);}
                }
            }
            void Bow(float x,float y,float size,Paint c){Poly(c,x,y,x-size,y+size*.45f,x-size,y-size*.45f);Poly(c,x,y,x+size,y+size*.45f,x+size,y-size*.45f);Oval(x,y,.014f,.019f,c,.003f);}
            public void Headwear(){if(HeadwearArt.Has(identity))Image(HeadwearArt.Portrait(identity,persona),HeadwearArt.W,HeadwearArt.H,0,.12f*3+.09f,.34f*3,.28f*3);}
            public void Hair(bool front)=>Image(AnimeHair.Portrait(identity,persona,front),AnimeHair.W,AnimeHair.H,0,-.04f*3+.09f,.28f*3,.30f*3);
            public void Fill(Shape shape, Paint c, float opacity=1f, bool cel=false)
            {
                float px=2f/n;
                for(int y=0;y<n;y++) for(int x=0;x<n;x++)
                {
                    float u=(x+0.5f)*px-1f,v=(y+0.5f)*px-1f;
                    float sx=u,sy=v;
                    if(posing)
                    {
                        float a0=persona.Roll*0.0174533f,ca=(float)Math.Cos(a0),sa=(float)Math.Sin(a0);
                        float dx=u-persona.Yaw*.0018f*(1+v),dy=v+.15f;
                        sx=(dx*ca+dy*sa)/(1-Math.Abs(persona.Yaw)*.003f);sy=-dx*sa+dy*ca-.15f+persona.Pitch*.0015f;
                    }
                    float a=Math.Max(0f,Math.Min(1f,0.5f-shape(sx,sy)/px))*opacity;
                    if(a<=0f)continue;
                    var tint=cel && u+v*0.50f>0.11f?Paint.Mix(c,White,0.15f):cel?c*0.76f:c;
                    int k=y*n+x;
                    red[k]=Lerp(red[k],tint.r,a);green[k]=Lerp(green[k],tint.g,a);blue[k]=Lerp(blue[k],tint.b,a);
                }
            }
            public void Solid(Shape shape, Paint c, float ink=0.015f)
            {
                Fill((x,y)=>shape(x,y)-ink,identity=="澪"?Paint.Hex("#d8fbff"):Ink);
                Fill(shape,c,1f,true);
            }
            // 人臉獨立的柔和淡粉遮蔽，不影響怪物及其他配件的畫法。
            public void Face(Paint skin,params float[] outline)
            {
                Shape face=(x,y)=>PolygonD(x,y,outline);
                Fill((x,y)=>face(x,y)-.006f,identity=="澪"?Paint.Hex("#d8fbff"):new Paint(skin.r*.40f,skin.g*.28f,skin.b*.29f));
                Fill(face,skin);
                var look=CharacterLook.Get(identity);float wave=look.Seed*.71f,fringe=HumanFaceLight.Fringe(look);
                // Same UV convention and pink occlusion as the 3D face. Portrait key light is frontal.
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    float u=(x+.5f)*2/n-1,v=(y+.5f)*2/n-1,sx=u,sy=v;
                    if(posing){float a=persona.Roll*.0174533f,ca=(float)Math.Cos(a),sa=(float)Math.Sin(a),dx=u-persona.Yaw*.0018f*(1+v),dy=v+.15f;sx=(dx*ca+dy*sa)/(1-Math.Abs(persona.Yaw)*.003f);sy=-dx*sa+dy*ca-.15f+persona.Pitch*.0015f;}
                    if(face(sx,sy)>0)continue;
                    float fu=.5f+sx/(.96f*persona.Width),fv=.5f+(sy-.09f)/(.96f*persona.Length);
                    float shadow=HumanFaceLight.Occlusion(fu,fv,1,0,look.Part,wave,fringe);int k=y*n+x;
                    red[k]=skin.r*(1-.07f*shadow);green[k]=skin.g*(1-.15f*shadow);blue[k]=skin.b*(1-.15f*shadow);
                }

            }
            public void Oval(float x,float y,float rx,float ry,Paint c,float ink=0.015f) => Solid((u,v)=>OvalD(u,v,x,y,rx,ry),c,ink);
            public void Box(float x,float y,float hx,float hy,Paint c,float rot=0f) => Solid((u,v)=>BoxD(u,v,x,y,hx,hy,rot),c);
            public void Poly(Paint c,params float[] p) => Solid((x,y)=>PolygonD(x,y,p),c);
            public void Line(float ax,float ay,float bx,float by,float w,Paint c) => Fill((x,y)=>SegmentD(x,y,ax,ay,bx,by)-w,c);
            public void Ring(float x,float y,float rx,float ry,float w,Paint c) => Solid((u,v)=>Math.Abs(OvalD(u,v,x,y,rx,ry))-w,c,0.008f);
            public void Gem(float x,float y,float s,Paint c)
            {
                Poly(Gold,x,y+s*1.3f,x+s,y,x,y-s*1.3f,x-s,y);
                Poly(c,x,y+s,x+s*0.65f,y,x,y-s,x-s*0.65f,y);
                Line(x-s*0.32f,y+s*0.1f,x,y+s*0.6f,0.009f,White);
            }
            public Color32[] Output()
            {
                posing=false;
                Fill((x,y)=>Math.Abs(RoundD(x,y,0.925f,0.925f,0.13f))-0.026f,border);
                Fill((x,y)=>Math.Abs(RoundD(x,y,0.88f,0.88f,0.11f))-0.003f,Paint.Mix(border,White,0.35f));
                for(int i=0;i<3;i++) Fill((x,y)=>OvalD(x,y,-0.08f+i*0.08f,0.925f,0.016f,0.016f),White);
                var result=new Color32[n*n];float step=2f/n;
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    int k=y*n+x;
                    float alpha=0.5f-RoundD((x+0.5f)*step-1f,(y+0.5f)*step-1f,0.965f,0.965f,0.15f)/step;
                    result[k]=new Color32(B(red[k]),B(green[k]),B(blue[k]),B(identity=="澪"?Math.Max(0f,Math.Min(1f,alpha))*.70f:alpha));
                }
                return result;
            }
        }

        static float OvalD(float x,float y,float cx,float cy,float rx,float ry)
        { x=(x-cx)/rx;y=(y-cy)/ry;return ((float)Math.Sqrt(x*x+y*y)-1f)*Math.Min(rx,ry); }
        static float BoxD(float x,float y,float cx,float cy,float hx,float hy,float a=0f)
        {
            float ca=(float)Math.Cos(a),sa=(float)Math.Sin(a),u=(x-cx)*ca+(y-cy)*sa,v=-(x-cx)*sa+(y-cy)*ca;
            float qx=Math.Abs(u)-hx,qy=Math.Abs(v)-hy;
            return (float)Math.Sqrt(Math.Max(qx,0)*Math.Max(qx,0)+Math.Max(qy,0)*Math.Max(qy,0))+Math.Min(Math.Max(qx,qy),0);
        }
        static float RoundD(float x,float y,float hx,float hy,float r) => BoxD(x,y,0f,0f,hx-r,hy-r)-r;
        static float SegmentD(float x,float y,float ax,float ay,float bx,float by)
        {
            float dx=bx-ax,dy=by-ay,t=Math.Max(0f,Math.Min(1f,((x-ax)*dx+(y-ay)*dy)/Math.Max(0.000001f,dx*dx+dy*dy)));
            x-=ax+dx*t;y-=ay+dy*t;return (float)Math.Sqrt(x*x+y*y);
        }
        static float PolygonD(float x,float y,float[] p)
        {
            float d=10f;bool inside=false;
            for(int i=0,j=p.Length-2;i<p.Length;j=i,i+=2)
            {
                d=Math.Min(d,SegmentD(x,y,p[j],p[j+1],p[i],p[i+1]));
                if((p[i+1]>y)!=(p[j+1]>y) && x<(p[j]-p[i])*(y-p[i+1])/(p[j+1]-p[i+1])+p[i])inside=!inside;
            }
            return inside?-d:d;
        }

        // Shared anatomy is deliberately independent of frame/tag color. Identity is in
        // silhouette, hair, costume and facial proportions rather than palette swapping.
        static void Bust(Canvas cv,Paint skin,Paint hair,Paint cloth,Paint iris,bool elder=false,bool child=false,bool elf=false)
        {
            cv.Hair(false);
            var outfit=OutfitArt.Get(cv.identity);
            cloth=new Paint(outfit.Main.r,outfit.Main.g,outfit.Main.b);
            var lining=new Paint(outfit.Sub.r,outfit.Sub.g,outfit.Sub.b);var trim=new Paint(outfit.Accent.r,outfit.Accent.g,outfit.Accent.b);
            cv.Poly(cloth,-.16f,-.49f,-.50f,-.59f,-.62f,-1f,.62f,-1f,.50f,-.59f,.16f,-.49f);
            // Closed jacket, complete collar and one readable metal zipper. No tie or chest marks.
            cv.Poly(lining,-.16f,-.49f,-.11f,-.59f,.11f,-.59f,.16f,-.49f,.12f,-.46f,-.12f,-.46f);
            cv.Line(0,-.61f,0,-1f,.005f,trim);cv.Oval(0,-.70f,.012f,.023f,trim,.003f);
            if(outfit.Cut==0||outfit.Cut==5||outfit.Cut==6)cv.Oval(-.46f,-.69f,.10f,.08f,lining,.008f);
            if(outfit.Cut==4||outfit.Cut==5||outfit.Cut==13||outfit.Cut==14){cv.Line(.27f,-.69f,.27f,-.83f,.009f,trim);cv.Line(.22f,-.75f,.32f,-.75f,.009f,trim);}
            cv.StoryDress();
            var p=cv.persona;var plan=new FacePlan(p);
            float fw=.140f*p.Width*3,jaw=.094f*p.Width*(.70f+.30f*p.Jaw)*3,chin=plan.Chin*3+.09f;
            float et=plan.EarTop*3+.09f,eb=plan.EarBottom*3+.09f;
            for(int side=-1;side<=1;side+=2)
            {
                float ex=side*(plan.FaceWidth*.5f+.006f)*3;
                if(elf)cv.Poly(skin,ex-side*.04f,et,ex+side*.09f,(et+eb)*.5f+.04f,ex,eb);
                else cv.Oval(ex,(et+eb)*.5f,.042f,(et-eb)*.5f,skin);
            }
            cv.Face(skin,AnimeFace.Outline(p));
            for(int side=-1;side<=1;side+=2)cv.DrawEye(side,plan);
            float noseY=plan.NoseY*3+.09f,mouthY=plan.MouthY*3+.09f,mw=plan.MouthWidth*1.5f;
            if(!p.SoftNose)cv.Oval(.006f,noseY+.023f,.009f,.012f,Paint.Mix(skin,White,.20f),0);
            cv.Line(-.0105f,noseY,.0045f,noseY-.0021f,.0024f,Paint.Mix(skin,Paint.Hex("#A45E50"),.38f));
            if(p.Open>0){float mh=plan.MouthWidth*.9f*p.Open;cv.Oval(0,mouthY,mw,mh,Paint.Hex("#74363F"),.003f);cv.Oval(0,mouthY+mh*.46f,mw*.72f,mh*.15f,White,0);}
            else
            {
                for(int i=0;i<16;i++)
                {
                    float a=i/8f-1,b=(i+1)/8f-1;
                    float ay=mouthY+p.Smile*.012f*a*a+p.Asym*.0027f*a+.0018f*(float)Math.Cos(a*Math.PI*2)*(1-a*a);
                    float by=mouthY+p.Smile*.012f*b*b+p.Asym*.0027f*b+.0018f*(float)Math.Cos(b*Math.PI*2)*(1-b*b);
                    cv.Line(a*mw,ay,b*mw,by,.0025f*(1-.65f*a*a),Paint.Hex("#A36663"));
                }
            }
            cv.Oval(0,mouthY-.016f,mw*.45f,.004f,Paint.Mix(skin,White,.12f),0);
            for(int side=-1;side<=1;side+=2)
            {
                float inner=side*(plan.EyeX-plan.EyeWidth*.5f)*3,outer=inner+side*plan.EyeWidth*3.3f;
                float by=plan.BrowY*3+.09f+(side>0?p.Asym*.0018f:0),bend=(p.Masculine?0:p.Brow*.35f-1)*.0174533f*plan.EyeWidth*3;
                cv.Line(inner,by,side*plan.EyeX*3,by+.0045f+bend*.5f,p.Elder?.0135f:p.Masculine?.009f:.0048f,hair);
                cv.Line(side*plan.EyeX*3,by+.0045f+bend*.5f,outer,by+bend,p.Elder?.012f:p.Masculine?.0084f:.0042f,hair);
                float cx=side*plan.EyeX*3.66f,cy=(plan.EyeY-plan.EyeHeight*.72f)*3+.09f;
                cv.Fill((u,v)=>OvalD(u,v,cx,cy,.051f,.021f),Paint.Mix(skin,Paint.Hex("#f27385"),.06f));
                if(p.Elder)
                {
                    var crease=Paint.Mix(skin,Paint.Hex("#875048"),.30f);
                    cv.Line(side*(plan.EyeX-.018f)*3,(plan.EyeY-.012f)*3+.09f,side*(plan.EyeX+.022f)*3,(plan.EyeY-.014f)*3+.09f,.0027f,crease);
                    cv.Line(side*.048f,noseY-.012f,side*.066f,mouthY+.003f,.0024f,crease);
                }
            }
            cv.Hair(true);
        }
        static void ClassPortrait(Canvas cv,string id)
        {
            var look=CharacterLook.Get(id);var hair=new Paint(look.HairColor.r,look.HairColor.g,look.HairColor.b);
            var main=OutfitArt.Get(id).Main;var cloth=new Paint(main.r,main.g,main.b);
            Bust(cv,Paint.Hex("#f3c89f"),hair,cloth,new Paint(look.Iris.r,look.Iris.g,look.Iris.b));
            if(id=="berserker")
            {
                var blue=Paint.Hex("#397a96");var jade=Paint.Hex("#75cdd0");
                cv.Line(-.16f,-.49f,.16f,-.91f,.040f,blue);
                cv.Line(.16f,-.49f,-.05f,-.78f,.022f,jade);
            }
            if(id=="novice")
            {
                // Same canvas bag strap and folded collar as the 3D traveler.
                var leather=Paint.Hex("#786044");var linen=Paint.Hex("#d9ceaa");
                cv.Line(.28f,-.50f,-.30f,-.96f,.045f,leather);
                cv.Line(-.14f,-.48f,.02f,-.69f,.018f,linen);
                cv.Line(.02f,-.69f,.08f,-.77f,.010f,leather);
            }

        }
        static void FacilityPortrait(Canvas cv,string id)
        {
            var look=CharacterLook.Get(id);var design=OutfitArt.Get(id);var skin=FacilityPeople.Skin(id);
            var leather=Paint.Hex("#9b673a");var main=new Paint(design.Main.r,design.Main.g,design.Main.b);
            if(id=="嚮導")cv.Box(.43f,-.61f,.22f,.48f,leather);
            if(id=="塔守")cv.Poly(main,-.66f,-.86f,-.58f,.36f,-.29f,.80f,.28f,.80f,.56f,.36f,.68f,-.86f);
            Bust(cv,new Paint(skin.r,skin.g,skin.b),new Paint(look.HairColor.r,look.HairColor.g,look.HairColor.b),main,new Paint(look.Iris.r,look.Iris.g,look.Iris.b),id=="塔守");
            if(id=="嚮導")
            {
                cv.Poly(leather,-.09f,-.49f,-.30f,-.51f,-.40f,-.98f,-.09f,-.98f);cv.Poly(leather,.09f,-.49f,.30f,-.51f,.40f,-.98f,.09f,-.98f);
                cv.Line(-.30f,-.46f,-.32f,-.97f,.026f,leather);cv.Line(.30f,-.46f,.32f,-.97f,.026f,leather);
                cv.Line(.225f,.060f,.306f,-.054f,.006f,Paint.Hex("#a86d59"));
                cv.Ring(-.53f,-.61f,.065f,.08f,.012f,Gold);cv.Box(-.53f,-.83f,.14f,.22f,Gold);cv.Box(-.53f,-.83f,.09f,.15f,Paint.Hex("#ffe6a0"));
            }
            else if(id=="教官")
            {
                cv.Line(-.16f,-.48f,.03f,-.75f,.008f,White);cv.Line(.16f,-.48f,.03f,-.75f,.008f,White);cv.Box(.03f,-.76f,.075f,.05f,Gold);
                for(int i=0;i<3;i++){cv.Line(-.62f,-.73f-i*.045f,-.48f,-.69f-i*.045f,.017f,White);cv.Line(.48f,-.69f-i*.045f,.62f,-.73f-i*.045f,.017f,White);}
            }
            else if(id=="懸賞官")
            {
                var plan=new FacePlan(cv.persona);for(int side=-1;side<=1;side+=2)cv.Ring(side*plan.EyeX*3,plan.EyeY*3+.09f,.126f,.09f,.008f,Gold);
                cv.Line(-.04f,.09f,.04f,.09f,.007f,Gold);cv.Line(-.29f,-.48f,.34f,-.95f,.030f,leather);
                cv.Box(-.51f,-.82f,.17f,.28f,White);cv.Box(-.51f,-.67f,.21f,.025f,Gold);cv.Box(-.51f,-.97f,.21f,.025f,Gold);Leaf(cv,.52f,-.68f,.16f,Paint.Hex("#b7aad9"),.05f);
            }
            else if(id=="播報員")
            {
                cv.Poly(Paint.Hex("#36b8bb"),-.34f,-.44f,-.70f,-.70f,-.55f,-.89f,-.23f,-.56f);cv.Poly(Paint.Hex("#36b8bb"),.34f,-.44f,.70f,-.70f,.55f,-.89f,.23f,-.56f);
                cv.Poly(Gold,.35f,-.89f,.47f,-.80f,.67f,-.55f,.76f,-.78f);cv.Line(-.58f,-.99f,-.58f,-.51f,.015f,leather);cv.Poly(Paint.Hex("#36c8cb"),-.57f,-.51f,-.36f,-.59f,-.57f,-.69f);
            }
            else
            {
                cv.Line(-.49f,-.57f,-.49f,-.77f,.015f,Gold);var xy=new float[20];for(int i=0;i<10;i++){float a=Mathf.PI*.5f+i*Mathf.PI/5,rr=i%2==0?.13f:.060f;xy[i*2]=-.49f+Mathf.Cos(a)*rr;xy[i*2+1]=-.85f+Mathf.Sin(a)*rr;}cv.Poly(Gold,xy);cv.Oval(-.49f,-.85f,.07f,.07f,White);
            }
        }
        static void BellBrooch(Canvas cv,float x,float y,float s)
        {
            cv.Oval(x,y+s*0.20f,s*0.54f,s*0.65f,Gold);
            cv.Box(x,y-s*0.24f,s*0.68f,s*0.12f,Gold);
            cv.Oval(x,y-s*0.43f,s*0.15f,s*0.15f,White,0.006f);
        }
        static void Leaf(Canvas cv,float x,float y,float s,Paint c,float lean=0f)
        {
            cv.Poly(c,x-s*0.18f,y-s*0.55f,x-s*0.47f,y+s*0.13f,x+lean,y+s*0.70f,x+s*0.42f,y+s*0.14f);
            cv.Line(x,y-s*0.40f,x+lean*0.5f,y+s*0.45f,0.007f,Gold);
        }

        static void Erin(Canvas cv)
        {
            cv.Box(.48f,-.66f,.13f,.30f,Paint.Hex("#875832"));
            for(int i=0;i<3;i++)cv.Box(.40f+i*.07f,-.32f,.025f,.14f,White);
            Bust(cv,Paint.Hex("#f7c9ac"),Paint.Hex("#d1c3e8"),Paint.Hex("#513180"),Paint.Hex("#aa70e5"));
            // Raised hood is rendered from the same headwear mesh as the resident.
            cv.Line(-.18f,-.50f,-.29f,-.95f,.014f,Gold);cv.Line(.18f,-.50f,.29f,-.95f,.014f,Gold);
            cv.Line(-.32f,-.62f,.26f,-.98f,.022f,Paint.Hex("#875832"));
            cv.Box(-.62f,-.52f,.018f,.40f,Paint.Hex("#865334"));cv.Oval(-.62f,-.12f,.07f,.07f,Paint.Hex("#ae74ef"),.006f);
        }
        static void OldFlame(Canvas cv)
        {
            var hair=Paint.Hex("#f3f3ed");
            Bust(cv,Paint.Hex("#d7a27a"),hair,Paint.Hex("#a64232"),Paint.Hex("#b9954e"),true);

            cv.Poly(hair,-0.30f,-0.07f,-0.13f,-0.13f,0f,-0.09f,0.13f,-0.13f,0.30f,-0.07f,0.22f,-0.39f,.16f,-.66f,.12f,-.63f,0f,-.92f,-.11f,-.66f,-.15f,-.70f,-0.22f,-0.39f);
            cv.Line(-0.15f,-0.14f,-0.02f,-0.20f,0.035f,hair*0.75f);cv.Line(0.15f,-0.14f,0.02f,-0.20f,0.035f,hair*0.75f);
            cv.Line(-0.08f,-0.235f,0.08f,-0.235f,0.007f,Ink);
            cv.Box(-0.47f,-0.76f,0.025f,0.28f,Paint.Hex("#75472c"),-0.20f);
            BellBrooch(cv,0.22f,-0.64f,0.10f);
            cv.Line(-0.13f,0.40f,0.08f,0.41f,0.005f,Paint.Hex("#aa7655"));
        }
        static void LittleSmith(Canvas cv)
        {
            var hair=Paint.Hex("#6c3b27");
            Bust(cv,Paint.Hex("#eeb88e"),hair,Paint.Hex("#9d5d32"),Paint.Hex("#e0ac39"));

            cv.Fill((x,y)=>OvalD(x,y,-0.27f,-0.08f,0.05f,0.018f),Paint.Hex("#6c6056"));


        }
        static void Mila(Canvas cv)
        {
            var hair=Paint.Hex("#553354");
            cv.Box(0.56f,-0.35f,0.15f,0.33f,Paint.Hex("#8d7251"),0.1f);
            cv.Box(0.55f,0.02f,0.18f,0.085f,Paint.Hex("#d5c497"));
            Bust(cv,Paint.Hex("#dca17e"),hair,Paint.Hex("#268e9c"),Paint.Hex("#ac6b3c"));

            cv.Ring(0.38f,-0.09f,0.030f,0.05f,0.010f,Gold);
            cv.Line(-0.32f,-0.58f,0.18f,-0.96f,0.033f,Paint.Hex("#d5b374"));
            cv.Gem(0.02f,-0.83f,0.045f,Paint.Hex("#db8e56"));
        }
        static void Cui(Canvas cv)
        {
            var hair=Paint.Hex("#e3edd4");

            Bust(cv,Paint.Hex("#eccca6"),hair,Paint.Hex("#376e5c"),Paint.Hex("#488e66"),true,false,true);

            for(int side=-1;side<=1;side+=2)cv.Line(side*.30f,.24f,side*.30f,-.59f,.012f,Gold);


        }
        static void Maple(Canvas cv)
        {
            var hair=Paint.Hex("#a75b35");var hood=Paint.Hex("#315e50");
            cv.Poly(hood,-0.54f,-0.45f,-0.50f,0.48f,-0.20f,0.83f,0.22f,0.81f,0.52f,0.48f,0.53f,-0.45f);
            Bust(cv,Paint.Hex("#e8b48c"),hair,hood,Paint.Hex("#dbb254"),false,false,true);


            cv.Line(0.24f,0.02f,0.29f,-0.09f,0.007f,Paint.Hex("#ac6c56"));
            cv.Box(-0.47f,-0.69f,0.10f,0.16f,Gold);
            cv.Box(-0.47f,-0.67f,0.065f,0.10f,Paint.Hex("#d9ee87"));
            cv.Ring(-0.47f,-0.48f,0.047f,0.06f,0.010f,Gold);
            Leaf(cv,0.31f,-0.65f,0.13f,Paint.Hex("#7ebf78"));
        }
        static void LittleLeaf(Canvas cv)
        {
            var hair=Paint.Hex("#527e60");

            Bust(cv,Paint.Hex("#ffdab9"),hair,Paint.Hex("#d9aa51"),Paint.Hex("#76bc78"),false,true,true);

            cv.Fill((x,y)=>OvalD(x,y,-0.27f,-0.07f,0.055f,0.025f),Paint.Hex("#e99786"),0.65f);
            cv.Fill((x,y)=>OvalD(x,y,0.27f,-0.07f,0.055f,0.025f),Paint.Hex("#e99786"),0.65f);
            cv.Box(0,-.83f,.18f,.19f,Paint.Hex("#ac7950"));cv.Box(0,-.83f,.145f,.16f,Paint.Hex("#e5cc91"));
            for(int i=0;i<4;i++)cv.Line(-.10f,-.75f-i*.045f,.10f,-.75f-i*.045f,.004f,Paint.Hex("#765b3d"));
        }
        static void Kaito(Canvas cv)
        {
            var hair=Paint.Hex("#253e59");
            Bust(cv,Paint.Hex("#dba487"),hair,Paint.Hex("#365879"),Paint.Hex("#76b8c5"));


        }
        static void Nagi(Canvas cv)
        {
            var hair=Paint.Hex("#812c48");

            Bust(cv,Paint.Hex("#efbba1"),hair,Paint.Hex("#9c4c64"),Paint.Hex("#a17385"));



            cv.Ring(0.39f,-0.09f,0.031f,0.045f,0.010f,Gold);
            cv.Ring(-.65f,-.56f,.06f,.06f,.009f,Ink);cv.Box(-.65f,-.76f,.085f,.12f,Ink);cv.Box(-.65f,-.76f,.057f,.09f,Paint.Hex("#fff0bb"));

        }
        static void Tide(Canvas cv)
        {
            var hair=Paint.Hex("#d4d7cb");
            Bust(cv,Paint.Hex("#ba8768"),hair,Paint.Hex("#43787f"),Paint.Hex("#466b70"),true);
            cv.Poly(hair,-0.30f,-0.09f,-0.18f,-0.18f,0f,-0.12f,0.22f,-0.10f,0.28f,-0.33f,0f,-0.49f,-0.26f,-0.34f);
            cv.Line(-0.16f,-0.16f,0.12f,-0.16f,0.022f,hair);
            cv.Box(-0.49f,-0.73f,0.025f,0.23f,Paint.Hex("#745c43"),0.25f);
        }
        static void Mio(Canvas cv)
        {
            Bust(cv,Paint.Hex("#c9f1fa"),Paint.Hex("#bbecf5"),Paint.Hex("#a8e5ef"),Paint.Hex("#91def5"));

            for(int i=0;i<6;i++)cv.Oval(-.66f+i*.08f,.06f+i*.11f,.016f,.016f,Paint.Hex("#d8fbff"),0);
        }

        static void Guildmaster(Canvas cv)
        {
            var hair=Paint.Hex("#d7cebb");
            Bust(cv,Paint.Hex("#d2a383"),hair,Paint.Hex("#743948"),Paint.Hex("#b09460"),true);

            cv.Poly(Paint.Hex("#514452"),0.20f,0.65f,0.33f,0.50f,0.28f,0.32f,0.14f,0.43f);
            cv.Line(0.22f,0.03f,0.29f,-0.10f,0.008f,Paint.Hex("#a96861"));
            cv.Line(0.265f,-0.025f,0.30f,-0.045f,0.005f,Paint.Hex("#a96861"));
            for(int i=-1;i<=1;i++)BellBrooch(cv,i*0.10f,-0.73f,0.055f);
        }

        static void Smith(Canvas cv)
        {
            var h=Paint.Hex("#795341");Bust(cv,Paint.Hex("#ce986e"),h,Paint.Hex("#b96c49"),Gold,true);
            cv.Poly(h,-.30f,-.09f,-.18f,-.29f,0,-.38f,.18f,-.29f,.30f,-.09f,.25f,-.46f,0,-.60f,-.25f,-.46f);
            cv.Oval(0,-.22f,.11f,.065f,Ink);cv.Box(0,-.20f,.085f,.017f,White);
            cv.Box(0,-.90f,.30f,.25f,h);cv.Line(-.27f,-.54f,-.21f,-.95f,.025f,Gold);cv.Line(.27f,-.54f,.21f,-.95f,.025f,Gold);
            cv.Box(.48f,-.72f,.025f,.24f,Paint.Hex("#865334"));cv.Box(.48f,-.47f,.12f,.07f,Paint.Hex("#86a2ad"));
        }
        static void Clerk(Canvas cv)
        {
            var h=Paint.Hex("#78533e");Bust(cv,Paint.Hex("#f1c5a1"),h,Paint.Hex("#589b9b"),Paint.Hex("#73915c"));
            cv.Poly(White,-.27f,-.59f,.27f,-.59f,.34f,-1,-.34f,-1);Leaf(cv,.23f,.57f,.15f,Paint.Hex("#a9c66b"),-.04f);
            cv.Oval(-.45f,-.77f,.075f,.11f,Paint.Hex("#e67985"));cv.Box(-.45f,-.63f,.035f,.04f,Gold);
        }
        static void LavaGolem(Canvas cv)
        {
            Paint rock=Paint.Hex("#292c36"),light=Paint.Hex("#484c57"),lava=Paint.Hex("#ff8b23");
            cv.Poly(rock,-.72f,-.94f,-.68f,-.10f,-.37f,.01f,.37f,.01f,.68f,-.10f,.72f,-.94f);
            for(int side=-1;side<=1;side+=2)
            {
                cv.Poly(light,side*.28f,-.22f,side*.49f,.12f,side*.75f,.03f,side*.84f,-.27f,side*.55f,-.34f);
                cv.Line(side*.13f,.19f,side*.27f,-.17f,.031f,lava);cv.Line(side*.27f,-.17f,side*.48f,-.38f,.020f,lava);cv.Line(side*.48f,-.38f,side*.29f,-.73f,.018f,lava);
                cv.Poly(light,side*.03f,-.34f,side*.37f,-.27f,side*.48f,-.48f,side*.18f,-.58f);
            }
            cv.Poly(lava,-.12f,.30f,.12f,.30f,.20f,-.19f,-.20f,-.19f);
            cv.Poly(rock,0,.69f,.18f,.52f,.17f,.28f,0,.12f,-.17f,.28f,-.18f,.52f);
            for(int side=-1;side<=1;side+=2)cv.Line(side*.06f,.35f,side*.10f,.38f,.006f,lava);
            cv.Poly(lava,0,-.31f,.12f,-.56f,0,-.83f,-.12f,-.56f);cv.Poly(Ink,0,-.36f,.083f,-.56f,0,-.76f,-.083f,-.56f);
        }
        static void Treant(Canvas cv)
        {
            Paint wood=Paint.Hex("#514538"),vein=Paint.Hex("#a34be9");
            cv.Poly(wood,-.30f,-1,-.19f,-.55f,-.22f,-.05f,-.10f,.40f,.13f,.40f,.24f,-.06f,.17f,-.55f,.33f,-1);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
            {
                float y=.02f+i*.19f,x=side*(.43f+i*.15f);
                cv.Line(0,-.10f,x,y,.055f-i*.012f,wood);cv.Line(x,y,x+side*.07f,.69f+i*.06f,.020f,wood);cv.Line(x,y,side*.79f,y+.17f,.018f,wood);
                cv.Oval(x,y+.14f,.043f,.050f,vein,.003f);
                cv.Line(side*.10f,-.75f,side*(.43f+i*.16f),-.90f-i*.02f,.032f,wood);
            }
            cv.Line(0,.16f,.10f,.81f,.028f,wood);
            var hole=new float[40];for(int i=0;i<20;i++){float a=i*(float)Math.PI/10,r=i%2==0?.20f:.12f;hole[i*2]=(float)Math.Sin(a)*r;hole[i*2+1]=-.31f+(float)Math.Cos(a)*r;}
            cv.Poly(Ink,hole);for(int i=0;i<7;i++){float a=i*(float)Math.PI*2/7;cv.Line((float)Math.Sin(a)*.09f,-.31f+(float)Math.Cos(a)*.09f,(float)Math.Sin(a)*.17f,-.31f+(float)Math.Cos(a)*.17f,.008f,vein);}
        }
        static void SeaDemon(Canvas cv)
        {
            Paint hide=Paint.Hex("#18544f"),glow=Paint.Hex("#7ef2e5"),sucker=Paint.Hex("#b7dce0");
            for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
            {
                float x=side*(.39f+i*.12f),y=-.73f+i*.14f;
                cv.Line(side*.16f,-.44f,x,y,.10f-i*.012f,hide);cv.Line(x,y,side*.80f,y+.15f,.053f-i*.006f,hide);cv.Line(side*.80f,y+.15f,side*.72f,y+.30f,.026f,hide);
                for(int k=0;k<3;k++)cv.Oval(side*(.35f+k*.11f),y+.03f+k*.035f,.020f,.022f,sucker,.003f);
            }
            cv.Poly(hide,-.26f,-.49f,-.39f,-.12f,-.48f,.22f,-.31f,.55f,.02f,.70f,.20f,.79f,.36f,.63f,.48f,.22f,.39f,-.12f,.26f,-.49f);
            for(int side=-1;side<=1;side+=2){cv.Line(side*.13f,.57f,side*.23f,.42f,.012f,glow);cv.Line(side*.23f,.42f,side*.13f,.29f,.012f,glow);cv.Line(side*.13f,.29f,side*.25f,.15f,.012f,glow);cv.Oval(side*.21f,-.16f,.062f,.042f,glow,.005f);}
            cv.Poly(Ink,0,-.34f,.11f,-.56f,0,-.86f,-.11f,-.56f);for(int i=0;i<4;i++)cv.Line(-.038f,-.48f-i*.05f,.038f,-.48f-i*.05f,.004f,sucker);
        }

        static int Seed(string s) { int h = 17; foreach (var ch in s) h = h * 31 + ch; return h & 0x7fffffff; }

        /// <summary>預設頭像：圓角方框（名牌色漸層）＋半身剪影（頭、肩），髮色依名字變化</summary>
        static void Default(Color32[] px, int n, float r, float g, float b, int seed)
        {
            float hue = (seed % 360) / 360f;
            HsvToRgb(hue, 0.45f, 0.35f, out float hr, out float hg, out float hb);   // 髮色
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;   // -1..1，v 往上
                    // 圓角方框
                    float qx = Math.Abs(u) - 0.8f, qy = Math.Abs(v) - 0.8f;
                    float box = (float)Math.Sqrt(Math.Max(qx, 0f) * Math.Max(qx, 0f) + Math.Max(qy, 0f) * Math.Max(qy, 0f)) - 0.18f;
                    if (box > 0.02f) { px[y * n + x] = new Color32(0, 0, 0, 0); continue; }
                    float k = 0.35f + 0.35f * (v + 1f) * 0.5f;
                    float cr = r * k, cg = g * k, cb = b * k;
                    // 邊框
                    if (box > -0.05f) { cr = Lerp(cr, 1f, 0.55f); cg = Lerp(cg, 0.92f, 0.55f); cb = Lerp(cb, 0.7f, 0.55f); }
                    else
                    {
                        // 肩膀（下方的半橢圓）
                        float sh = (u * u) / 0.55f + ((v + 1.05f) * (v + 1.05f)) / 0.45f;
                        // 頭（圓）
                        float hd = (u * u + (v - 0.12f) * (v - 0.12f)) / 0.16f;
                        // 頭髮（頭的上半＋瀏海）
                        bool hair = hd < 1.12f && (v > 0.2f || Math.Abs(u) > 0.3f) && v > -0.15f;
                        if (sh < 1f) { cr = r * 0.55f; cg = g * 0.55f; cb = b * 0.55f; }
                        if (hd < 1f) { cr = 1f; cg = 0.86f; cb = 0.74f; }
                        if (hair) { cr = hr; cg = hg; cb = hb; }
                        // 眼睛
                        if (hd < 1f && !hair && Math.Abs(v - 0.08f) < 0.06f && Math.Abs(Math.Abs(u) - 0.15f) < 0.045f) { cr = 0.15f; cg = 0.12f; cb = 0.2f; }
                    }
                    float a = box > -0.0f ? Math.Max(0f, 1f - box / 0.02f) : 1f;
                    px[y * n + x] = new Color32(B(cr), B(cg), B(cb), B(a));
                }
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static byte B(float v) => (byte)(Math.Max(0f, Math.Min(1f, v)) * 255f + 0.5f);
        static void HsvToRgb(float h, float s, float v, out float r, out float g, out float b)
        {
            float i = (float)Math.Floor(h * 6f), f = h * 6f - i, p = v * (1f - s), q = v * (1f - f * s), t = v * (1f - (1f - f) * s);
            switch (((int)i) % 6)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
        }
    }
}
