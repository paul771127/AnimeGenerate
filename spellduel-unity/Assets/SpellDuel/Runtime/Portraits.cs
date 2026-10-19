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
            internal Persona(float width,float jaw,float length,float eye,float tilt,float gaze,float brow,float asym,float smile,float open,float pitch,float yaw,float roll,float lean,float lid=0,float pupil=1)
            {
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
            internal float Top,Chin,EyeY,EyeX,EyeWidth,EyeHeight,NoseY,MouthY,MouthWidth,BrowY,EarTop,EarBottom;
            internal FacePlan(Persona p)
            {
                Top=.157f*p.Length;Chin=-Top;EyeY=.013f;float d=EyeY-Chin;
                EyeX=.056f*p.Width;EyeWidth=.057f*p.Width;EyeHeight=EyeWidth*(.66f+(p.Eye-1)*.10f);
                NoseY=EyeY-.50f*d;MouthY=EyeY-.70f*d;MouthWidth=EyeX*2*.65f;
                BrowY=EyeY+EyeHeight*1.1f;EarTop=BrowY;EarBottom=NoseY;
            }
        }
        internal static Persona PersonaFor(string key)
        {
            var p=BasePersonaFor(key);HumanPersonality.Apply(ref p,Key(key));return p;
        }
        static Persona BasePersonaFor(string key)
        {
            switch(Key(key))
            {
                case "艾琳": return new Persona(0.984f,0.944f,1f,1.0338f,1.6f,0f,1.4f,0f,0.65f,0f,-0.8f,1.2f,-0.8f,0f,0f,1f);
                case "澪": return new Persona(0.972f,0.944f,1.016f,0.883f,-3.6f,-0.22f,-1.75f,0f,0f,0f,2.8f,-4.8f,0.8f,0f,0.09f,1f);
                case "老焰": return new Persona(1.016f,1f,1.004f,0.9064f,-2.8f,-0.08f,-4.55f,0f,0.22f,0f,2.4f,-2f,0f,3f,0.072f,1f);
                case "小鐵": return new Persona(1.028f,1.056f,0.994f,0.9636f,3.6f,0.2f,4.9f,1f,0.42f,0f,-0.4f,3.6f,-1.2f,5f,0.028f,0.85f);
                case "米菈": return new Persona(0.976f,0.94f,1.008f,0.9116f,4.8f,-0.22f,2.8f,3f,1f,0f,-1.2f,-5.2f,2.8f,1f,0.068f,1f);
                case "翠": return new Persona(0.956f,0.95f,1.024f,0.87f,0.8f,0.04f,0f,0f,0f,0f,-2.8f,3.2f,0f,-2f,0.1f,1f);
                case "楓": return new Persona(0.976f,0.976f,1f,0.9272f,3.2f,0.12f,1.75f,0f,0.36f,0f,0f,-3.2f,0.4f,7f,0.056f,1f);
                case "小葉": return new Persona(1.04f,1.04f,0.988f,1.117f,-2.4f,-0.12f,-4.2f,0.5f,0.15f,0.62f,1.6f,-2.8f,-3.6f,2f,0f,1.1f);
                case "海斗": return new Persona(1.032f,1.06f,1f,1f,2f,0f,3.5f,0f,0.9f,0.65f,-2.8f,2f,0f,-3f,0f,1f);
                case "凪": return new Persona(1.02f,1.02f,0.996f,0.9636f,-4f,0.06f,-1.4f,0f,0.8f,0f,0.4f,-2.4f,1.2f,0f,0.028f,1f);
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
                case "berserker": return new Persona(1.04f,1.06f,1f,0.9272f,5.6f,0f,6f,-0.5f,-0.5f,0.65f,2f,-1.6f,-0.8f,12f,0.056f,0.85f);
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
            public void DrawEye(int side,FacePlan plan)=>Image(EyeArt.Render(identity,side),EyeArt.W,EyeArt.H,side*plan.EyeX*3,plan.EyeY*3+.09f,plan.EyeWidth*3/1.44f,plan.EyeHeight*3/.90f*(1-persona.Lid*1.6f));
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
                Fill((x,y)=>shape(x,y)-ink,Ink);
                Fill(shape,c,1f,true);
            }
            // 人臉獨立的柔和淡粉遮蔽，不影響怪物及其他配件的畫法。
            public void Face(Paint skin,params float[] outline)
            {
                Shape face=(x,y)=>PolygonD(x,y,outline);
                Fill((x,y)=>face(x,y)-.006f,new Paint(skin.r*.40f,skin.g*.28f,skin.b*.29f));
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
                    result[k]=new Color32(B(red[k]),B(green[k]),B(blue[k]),B(alpha));
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
            var p=cv.persona;var plan=new FacePlan(p);
            float fw=.140f*p.Width*3,jaw=.094f*p.Width*(.70f+.30f*p.Jaw)*3,chin=plan.Chin*3+.09f;
            float et=plan.EarTop*3+.09f,eb=plan.EarBottom*3+.09f;
            for(int side=-1;side<=1;side+=2)
            {
                float ex=side*(.14f*p.Width+.007f)*3;
                if(elf)cv.Poly(skin,ex-side*.04f,et,ex+side*.09f,(et+eb)*.5f+.04f,ex,eb);
                else cv.Oval(ex,(et+eb)*.5f,.055f,(et-eb)*.5f,skin);
            }
            cv.Face(skin,-.32f*p.Width,.46f,-fw*.95f,.23f,-fw,.06f,-jaw,plan.MouthY*3+.09f,-.15f,chin+.055f,-plan.MouthWidth*1.5f,chin,plan.MouthWidth*1.5f,chin,.15f,chin+.055f,jaw,plan.MouthY*3+.09f,fw,.06f,fw*.95f,.23f,.32f*p.Width,.46f,0,plan.Top*3+.09f);
            for(int side=-1;side<=1;side+=2)cv.DrawEye(side,plan);
            float noseY=plan.NoseY*3+.09f,mouthY=plan.MouthY*3+.09f,mw=plan.MouthWidth*1.5f;
            cv.Oval(.006f,noseY+.023f,.009f,.012f,Paint.Mix(skin,White,.20f),0);
            cv.Line(-.012f,noseY,.003f,noseY-.003f,.003f,Paint.Mix(skin,Paint.Hex("#A45E50"),.38f));
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
                float by=plan.BrowY*3+.09f+(side>0?p.Asym*.0018f:0),bend=p.Brow*.0174533f*plan.EyeWidth*3;
                cv.Line(inner,by,side*plan.EyeX*3,by+.0045f+bend*.5f,elder?.014f:.010f,hair);
                cv.Line(side*plan.EyeX*3,by+.0045f+bend*.5f,outer,by+bend,elder?.012f:.008f,hair);
                if(elder){cv.Line(side*.29f,.11f,side*.33f,.09f,.004f,skin*.65f);cv.Line(side*.28f,.07f,side*.31f,.05f,.004f,skin*.65f);}
            }
            cv.Hair(true);
        }
        static void ClassPortrait(Canvas cv,string id)
        {
            var look=CharacterLook.Get(id);var hair=new Paint(look.HairColor.r,look.HairColor.g,look.HairColor.b);
            var main=OutfitArt.Get(id).Main;var cloth=new Paint(main.r,main.g,main.b);
            Bust(cv,Paint.Hex("#f3c89f"),hair,cloth,new Paint(look.Iris.r,look.Iris.g,look.Iris.b));

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
            var hair=Paint.Hex("#963f51");
            Bust(cv,Paint.Hex("#f7c9ac"),hair,Paint.Hex("#7250aa"),Paint.Hex("#d1a04b"));

            cv.Poly(Paint.Hex("#4a377d"),-0.36f,0.63f,-0.19f,0.79f,0.21f,0.76f,0.39f,0.62f,0.17f,0.59f,-0.17f,0.59f);
            cv.Ring(0.24f,0.65f,0.047f,0.052f,0.013f,Gold);
            cv.Box(-0.40f,-0.81f,0.11f,0.19f,Paint.Hex("#733746"),-0.14f);
            cv.Line(-0.46f,-0.67f,-0.33f,-0.68f,0.014f,Gold);
            cv.Gem(0f,-0.72f,0.070f,Paint.Hex("#64d7ef"));
        }
        static void OldFlame(Canvas cv)
        {
            var hair=Paint.Hex("#e4d9bd");
            Bust(cv,Paint.Hex("#d7a27a"),hair,Paint.Hex("#a64232"),Paint.Hex("#b9954e"),true);

            cv.Poly(hair,-0.30f,-0.07f,-0.13f,-0.13f,0f,-0.09f,0.13f,-0.13f,0.30f,-0.07f,0.22f,-0.39f,0f,-0.52f,-0.22f,-0.39f);
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
            cv.Box(0f,-0.88f,0.29f,0.23f,Paint.Hex("#543c35"));
            cv.Line(-0.30f,-0.60f,-0.20f,-0.90f,0.023f,Gold);cv.Line(0.30f,-0.60f,0.20f,-0.90f,0.023f,Gold);
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
            cv.Gem(0f,-0.70f,0.075f,Paint.Hex("#885344"));
            Leaf(cv,-0.36f,-0.71f,0.18f,Paint.Hex("#68a25c"));Leaf(cv,0.36f,-0.71f,0.18f,Paint.Hex("#68a25c"));
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
            cv.Box(0.32f,-0.82f,0.10f,0.15f,Paint.Hex("#ac7950"),-0.10f);
            cv.Line(0.28f,-0.73f,0.36f,-0.82f,0.006f,White);
        }
        static void Kaito(Canvas cv)
        {
            var hair=Paint.Hex("#253e59");
            Bust(cv,Paint.Hex("#dba487"),hair,Paint.Hex("#365879"),Paint.Hex("#76b8c5"));

            cv.Poly(Paint.Hex("#243b52"),-0.49f,-0.55f,-0.16f,-0.44f,-0.10f,-0.87f,-0.48f,-0.75f);
            cv.Poly(Paint.Hex("#243b52"),0.49f,-0.55f,0.16f,-0.44f,0.10f,-0.87f,0.48f,-0.75f);
            cv.Line(0f,-0.56f,0f,-0.89f,0.016f,Gold);
            cv.Ring(0f,-0.84f,0.065f,0.030f,0.012f,Gold);
        }
        static void Nagi(Canvas cv)
        {
            var hair=Paint.Hex("#623d54");

            Bust(cv,Paint.Hex("#efbba1"),hair,Paint.Hex("#9c4c64"),Paint.Hex("#a17385"));

            cv.Poly(White,-0.30f,-0.61f,0.30f,-0.61f,0.37f,-1f,-0.37f,-1f);
            cv.Line(-0.27f,-0.53f,-0.25f,-0.81f,0.020f,White);cv.Line(0.27f,-0.53f,0.25f,-0.81f,0.020f,White);
            cv.Ring(-0.39f,-0.09f,0.031f,0.045f,0.010f,Gold);

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
            var hair=Paint.Hex("#4b8998");

            Bust(cv,Paint.Hex("#d9e4dc"),hair,Paint.Hex("#b3d7d7"),Paint.Hex("#5099bd"));

            cv.Poly(Paint.Hex("#edf1da"),-0.32f,0.64f,-0.23f,0.77f,0.20f,0.74f,0.33f,0.62f,0.20f,0.55f,-0.18f,0.57f);
            cv.Line(-0.27f,0.62f,0.29f,0.61f,0.019f,Paint.Hex("#477a91"));
            for(int i=0;i<5;i++)
            {
                float a=i*1.256637f;
                cv.Oval(0.33f+(float)Math.Cos(a)*0.055f,0.39f+(float)Math.Sin(a)*0.055f,0.035f,0.035f,White,0.004f);
            }
            cv.Oval(0.33f,0.39f,0.025f,0.025f,Gold,0.004f);
            cv.Line(-0.18f,-0.51f,0f,-0.70f,0.012f,White);cv.Line(0.18f,-0.51f,0f,-0.70f,0.012f,White);
            cv.Gem(0f,-0.72f,0.075f,Paint.Hex("#d5b1b7"));
            for(int i=0;i<3;i++)cv.Oval(-0.66f+i*0.09f,0.13f+i*0.13f,0.014f,0.014f,Paint.Hex("#a4e8df"),0f);
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
            Paint rock=Paint.Hex("#985b47"),light=Paint.Hex("#bc8557"),lava=Paint.Hex("#ffb348");
            cv.Poly(rock,-0.78f,-0.90f,-0.64f,-0.48f,-0.31f,-0.39f,0.34f,-0.39f,0.66f,-0.51f,0.79f,-0.92f);
            cv.Poly(rock,-0.48f,0.42f,-0.29f,0.69f,0.23f,0.70f,0.51f,0.42f,0.43f,-0.30f,0.17f,-0.50f,-0.24f,-0.46f,-0.46f,-0.18f);
            cv.Poly(light,-0.51f,0.43f,-0.20f,0.51f,-0.01f,0.32f,-0.14f,0.20f,-0.50f,0.29f);
            cv.Poly(light,0.51f,0.43f,0.20f,0.51f,0.01f,0.32f,0.14f,0.20f,0.50f,0.29f);
            cv.Poly(lava,-0.38f,0.21f,-0.07f,0.12f,-0.31f,0.05f);cv.Poly(lava,0.38f,0.21f,0.07f,0.12f,0.31f,0.05f);
            cv.Box(0f,-0.22f,0.25f,0.055f,Ink);
            cv.Poly(lava,-.19f,-.22f,-.10f,-.18f,.03f,-.22f,.18f,-.17f,.15f,-.30f,-.13f,-.30f);
            cv.Line(0.06f,0.65f,-0.04f,0.49f,0.017f,lava);cv.Line(-0.04f,0.49f,0.05f,0.30f,0.017f,lava);
            cv.Line(-0.16f,-0.56f,-0.03f,-0.73f,0.025f,lava);cv.Line(-0.03f,-0.73f,0.12f,-0.55f,0.025f,lava);
            cv.Gem(0f,-0.77f,0.10f,Paint.Hex("#ffe2a4"));
        }
        static void Treant(Canvas cv)
        {
            Paint wood=Paint.Hex("#9d623d"),leaf=Paint.Hex("#597e4a"),vein=Paint.Hex("#c67be8");
            for(int s=-1;s<=1;s+=2)
            {
                cv.Poly(wood,s*0.24f,0.45f,s*0.39f,0.73f,s*0.68f,0.84f,s*0.48f,0.54f,s*0.55f,0.29f);
                cv.Poly(wood,s*0.24f,-0.42f,s*0.69f,-0.88f,s*0.45f,-0.91f,s*0.09f,-0.56f);
                Leaf(cv,s*0.48f,0.52f,0.24f,leaf,s*0.06f);
            }
            cv.Poly(wood,-0.34f,0.51f,0f,0.67f,0.35f,0.49f,0.39f,-0.21f,0.21f,-0.53f,0f,-0.72f,-0.21f,-0.53f,-0.40f,-0.19f);
            cv.Poly(Paint.Hex("#5a393b"),-0.29f,0.21f,-0.02f,0.10f,-0.25f,0.02f);cv.Poly(Paint.Hex("#5a393b"),0.29f,0.21f,0.02f,0.10f,0.25f,0.02f);
            for(int s=-1;s<=1;s+=2)cv.Oval(s*0.18f,0.11f,0.044f,0.023f,Paint.Hex("#c6ed75"),0f);
            cv.Line(-.30f,.28f,-.08f,.35f,.025f,wood);cv.Line(.30f,.28f,.08f,.35f,.025f,wood);
            cv.Poly(Ink,-0.14f,-0.23f,-0.02f,-0.19f,0.16f,-0.27f,0.07f,-0.46f,-0.08f,-0.42f);
            cv.Line(-0.02f,0.51f,0.06f,0.28f,0.015f,vein);cv.Line(0.06f,0.28f,-0.04f,-0.03f,0.015f,vein);
            cv.Line(-0.26f,-0.12f,-0.20f,-0.45f,0.011f,Gold);cv.Line(0.24f,-0.16f,0.17f,-0.51f,0.011f,Gold);
            Leaf(cv,0f,0.64f,0.19f,Paint.Hex("#81aa57"));
        }
        static void SeaDemon(Canvas cv)
        {
            Paint hide=Paint.Hex("#398a98"),coral=Paint.Hex("#bc6b91"),glow=Paint.Hex("#9aeae0");
            for(int s=-1;s<=1;s+=2)
            {
                cv.Oval(s*0.49f,-0.56f,0.20f,0.36f,hide);
                cv.Oval(s*0.62f,-0.39f,0.14f,0.16f,hide);
                for(int i=0;i<3;i++)cv.Oval(s*(0.47f+i*0.05f),-0.76f+i*0.16f,0.028f,0.035f,glow,0.004f);
            }
            cv.Oval(0f,0.15f,0.48f,0.56f,hide);
            cv.Poly(coral,-0.39f,0.54f,-0.48f,0.81f,-0.18f,0.68f,0f,0.86f,0.18f,0.68f,0.48f,0.81f,0.39f,0.54f,0f,0.64f);
            cv.Poly(Ink,-0.35f,0.18f,-0.08f,0.07f,-0.28f,-0.02f);cv.Poly(Ink,0.35f,0.18f,0.08f,0.07f,0.28f,-0.02f);
            for(int s=-1;s<=1;s+=2)cv.Oval(s*0.23f,0.08f,0.058f,0.034f,glow,0f);
            cv.Poly(Ink,-.23f,-.15f,.23f,-.05f,.14f,-.36f,-.07f,-.33f);
            for(int s=-1;s<=1;s+=2)cv.Poly(White,s*0.12f,-0.16f,s*0.05f,-0.17f,s*0.06f,-0.27f);
            for(int i=0;i<3;i++)cv.Gem((i-1)*0.12f,0.40f-(i%2)*0.07f,0.025f,glow);
            cv.Ring(0f,-0.71f,0.21f,0.19f,0.025f,Paint.Hex("#5c697e"));
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
