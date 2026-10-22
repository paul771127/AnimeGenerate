using UnityEngine;
namespace SpellDuel
{
    /// <summary>Cached procedural nine-slice crystal panels. Drawing helpers never own game state.</summary>
    public static partial class UiKit
    {
        public static readonly Color Gold = new Color(.94f,.76f,.43f), Paper = new Color(.97f,.94f,.85f), Muted = new Color(.72f,.79f,.88f);
        static Texture2D panel, outline, bevel, circle, diamond, hexagon;
        static GUIStyle panelStyle, frameStyle, bevelStyle, textStyle;
        static readonly GUIContent content = new GUIContent();
        static readonly string[] statGlyphs={"力","智","體","敏","精","幸"};
        public static int BaseFont => Mathf.Max(18, Mathf.RoundToInt(Mathf.Min(Screen.height / 46f, Screen.width / 21f)));

        // ---- 手指拖曳捲動（IMGUI 的捲動區在手機上只能拖捲軸）：在 GUI.BeginScrollView 之前呼叫，回傳新的捲動位置。
        //      拖超過 12 像素才算拖曳（之前的點擊照常）；拖曳放開不會觸發按鈕；放開後有慣性滑行。
        static int dragId=-1,glideId=-1;static Vector2 dragLast,dragVel,glideVel;static float dragTotal;static bool dragMoved;
        public static Vector2 DragScroll(Rect view,Vector2 scroll,Rect content)
        {
            var e=Event.current;if(e==null)return scroll;
            int id=Mathf.RoundToInt(view.x*7f+view.y*13f+view.width*17f+view.height*31f);
            float maxX=Mathf.Max(0,content.width-view.width),maxY=Mathf.Max(0,content.height-view.height);
            switch(e.type)
            {
                case EventType.MouseDown:
                    if(view.Contains(e.mousePosition)){dragId=id;dragLast=e.mousePosition;dragTotal=0;dragMoved=false;dragVel=Vector2.zero;if(glideId==id)glideId=-1;}
                    break;
                case EventType.MouseDrag:
                    if(dragId==id)
                    {
                        var d=e.mousePosition-dragLast;dragLast=e.mousePosition;dragTotal+=d.magnitude;
                        if(dragTotal>12f)dragMoved=true;
                        if(dragMoved)
                        {
                            if(maxX<=0)d.x=0;
                            scroll-=d;dragVel=Vector2.Lerp(dragVel,d/Mathf.Max(1f/120f,Time.unscaledDeltaTime),.5f);
                            GUIUtility.hotControl=0;e.Use();   // 取消按鈕的按下狀態：拖曳不會變成點擊
                        }
                    }
                    break;
                case EventType.MouseUp:
                    if(dragId==id){if(dragMoved){e.Use();glideId=id;glideVel=dragVel;}dragId=-1;}
                    break;
                case EventType.Repaint:
                    if(glideId==id&&dragId!=id)
                    {
                        float dt=Mathf.Min(.05f,Time.unscaledDeltaTime);
                        scroll-=glideVel*dt;glideVel*=Mathf.Exp(-5f*dt);
                        if(glideVel.sqrMagnitude<400f)glideId=-1;
                    }
                    break;
            }
            scroll.x=Mathf.Clamp(scroll.x,0,maxX);scroll.y=Mathf.Clamp(scroll.y,0,maxY);
            return scroll;
        }
        static void Init()
        {
            if(panel!=null)return;
            panel=MakePanel(96,0);outline=MakePanel(96,1);bevel=MakePanel(96,2);
            circle=MakeNode(96,0);diamond=MakeNode(96,1);hexagon=MakeNode(96,2);
            panelStyle=new GUIStyle{normal={background=panel},border=new RectOffset(18,18,18,18)};
            frameStyle=new GUIStyle{normal={background=outline},border=new RectOffset(18,18,18,18)};
            bevelStyle=new GUIStyle{normal={background=bevel},border=new RectOffset(18,18,18,18)};
            textStyle=new GUIStyle(GUI.skin.label){wordWrap=true,richText=false,clipping=TextClipping.Clip,padding=new RectOffset(0,0,0,0),margin=new RectOffset(0,0,0,0)};
        }
        // Crystal cut corners and deterministic engraving, generated once. Row zero is the bottom.
        static Texture2D MakePanel(int n,int kind)
        {
            var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name="UI crystal "+kind,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=x+.5f,v=y+.5f;
                float edge=Mathf.Min(u,v,n-u,n-v,(u+v-10f)*.7071f,(n-u+v-10f)*.7071f,(u+n-v-10f)*.7071f,(2*n-u-v-10f)*.7071f);
                float a=Mathf.Clamp01(edge+.5f);float light=.70f+.30f*v/n;
                if(kind==1){a*=Mathf.Clamp01(2.2f-edge)+Mathf.Clamp01(1f-Mathf.Abs(edge-6f));light=1f;}
                if(kind==2){a*=Mathf.Clamp01(4f-edge);light=v>n*.5f?1f:.32f;}
                if(kind==0){float etch=(x+y)%19==0?.025f:0;light+=etch;}
                pixels[y*n+x]=new Color(light,light,light,Mathf.Clamp01(a));
            }
            t.SetPixels32(pixels);t.Apply(false,true);return t;
        }
        static Texture2D MakeNode(int n,int kind)
        {
            var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name="UI node "+kind,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};var px=new Color32[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=Mathf.Abs((x+.5f)/n*2-1),v=Mathf.Abs((y+.5f)/n*2-1);
                float d=kind==0?Mathf.Sqrt(u*u+v*v):kind==1?u+v:Mathf.Max(u*.866f+v*.5f,v);
                bool border=d>.82f;px[y*n+x]=new Color(border?1:.12f,border?1:.16f,border?1:.24f,Mathf.Clamp01((.96f-d)*n*.5f));
            }
            t.SetPixels32(px);t.Apply(false,true);return t;
        }
        static Rect Inset(Rect r,float amount)=>new Rect(r.x+amount,r.y+amount,Mathf.Max(0,r.width-amount*2),Mathf.Max(0,r.height-amount*2));
        static void Paint(Rect r,GUIStyle style,Color color)
        {
            if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;
            var old=GUI.color;GUI.color=color;style.Draw(r,false,false,false,false);GUI.color=old;
        }
        public static void Card(Rect r,float alpha=.62f,Color? accent=null,float accentAlpha=.9f)
        {
            if(Event.current.type!=EventType.Repaint||r.width<=0||r.height<=0)return;Init();var ac=accent??Gold;
            Paint(new Rect(r.x,r.y+3,r.width,r.height),panelStyle,new Color(.015f,.025f,.055f,alpha*.75f));
            Paint(r,panelStyle,new Color(.085f,.115f,.20f,alpha));
            Paint(r,frameStyle,new Color(ac.r,ac.g,ac.b,accentAlpha*.70f));
            if(r.height>BaseFont*2&&r.width>BaseFont*4)
            {
                float s=Mathf.Min(10,BaseFont*.20f);Gem(new Rect(r.x+6,r.y+6,s,s),ac);Gem(new Rect(r.xMax-6-s,r.yMax-6-s,s,s),ac);
            }
        }
        public static void Text(Rect r,string text,int size,Color color,TextAnchor anchor=TextAnchor.MiddleLeft,bool bold=false,float minScale=.55f)
        {
            if(string.IsNullOrEmpty(text)||r.width<=0||r.height<=0)return;Init();content.text=text;
            textStyle.alignment=anchor;textStyle.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
            int fs=Mathf.Max(10,size),min=Mathf.Max(10,Mathf.RoundToInt(size*minScale));textStyle.fontSize=fs;
            while(fs>min&&textStyle.CalcHeight(content,r.width)>r.height+1){fs--;textStyle.fontSize=fs;}
            var keep=GUI.color;var prior=textStyle.normal.textColor;textStyle.normal.textColor=Color.white;
            float o=Mathf.Max(1,fs*.055f);GUI.color=new Color(.015f,.025f,.06f,color.a*.9f);
            GUI.Label(new Rect(r.x+o,r.y+o,r.width,r.height),content,textStyle);
            GUI.color=color;GUI.Label(r,content,textStyle);GUI.color=keep;textStyle.normal.textColor=prior;
        }
        public static bool Button(Rect r,string text,int size,bool selected=false,Color? accent=null,bool enabled=true)
        {
            Init();var ac=accent??Gold;
            bool pressed=enabled&&r.Contains(Event.current.mousePosition)&&Input.GetMouseButton(0);
            var face=new Rect(r.x,r.y+(pressed?2:0),r.width,Mathf.Max(0,r.height-3));
            Color baseColor=!enabled?new Color(.14f,.16f,.20f,.8f):selected?Color.Lerp(new Color(.14f,.19f,.29f),ac,.43f):new Color(.16f,.21f,.32f,.97f);
            Paint(new Rect(r.x,r.y+3,r.width,r.height-3),panelStyle,new Color(.02f,.03f,.065f,.95f));
            Paint(face,panelStyle,baseColor);Paint(face,bevelStyle,new Color(.82f,.89f,1,enabled?.5f:.15f));
            Paint(face,frameStyle,enabled?selected?ac:Color.Lerp(ac,Muted,.65f):new Color(.4f,.44f,.5f,.5f));
            if(selected&&r.width>size*2.5f)Gem(new Rect(r.x+5,r.y+r.height*.5f-3,6,6),Gold);
            float pad=Mathf.Min(r.height*.12f,size*.4f);
            Text(Inset(face,pad),text,size,enabled?Paper:new Color(.57f,.61f,.68f),TextAnchor.MiddleCenter,selected);
            // Keep Unity's original hit rectangle/control semantics and the caller's enabled predicate.
            bool hit=enabled&&GUI.Button(r,GUIContent.none,GUIStyle.none);
            if(hit)Sfx.Play("click",.5f);   // 按鈕音效
            return hit;
        }
        public static void Bar(Rect r,float v,Color c,string text,int size)
        {
            Init();Paint(r,panelStyle,new Color(.025f,.035f,.07f,.94f));
            var inside=Inset(r,3);float width=inside.width*Mathf.Clamp01(v);
            if(width>0){var fill=new Rect(inside.x,inside.y,width,inside.height);Paint(fill,panelStyle,c);Paint(fill,bevelStyle,new Color(1,1,1,.45f));}
            Paint(r,frameStyle,new Color(.85f,.81f,.64f,.8f));
            if(!string.IsNullOrEmpty(text))Text(Inset(r,2),text,size,Paper,TextAnchor.MiddleCenter,true,.5f);
        }
        public static void Pill(float centerY,string text,int size,Color color,float maxWidth,float alpha=.55f)
        {
            if(string.IsNullOrEmpty(text))return;Init();content.text=text;textStyle.fontSize=size;textStyle.fontStyle=FontStyle.Bold;
            float w=Mathf.Min(maxWidth,textStyle.CalcSize(content).x+size*2);
            float h=Mathf.Min(textStyle.CalcHeight(content,Mathf.Max(1,w-size*2))+size*.8f,size*3.6f);
            var r=new Rect((Screen.width-w)*.5f,centerY-h*.5f,w,h);Card(r,Mathf.Max(.72f,alpha),color,.95f);
            Gem(new Rect(r.x+size*.25f,centerY-size*.13f,size*.26f,size*.26f),color);
            Gem(new Rect(r.xMax-size*.51f,centerY-size*.13f,size*.26f,size*.26f),color);
            Text(Inset(r,size*.48f),text,size,color,TextAnchor.MiddleCenter,true);
        }
        public static void Title(Rect r,string text,Color accent,int size=0)
        {
            Init();Paint(r,panelStyle,Color.Lerp(new Color(.14f,.17f,.29f),accent,.24f));Paint(r,bevelStyle,new Color(1,1,1,.3f));
            Divider(new Rect(r.x+8,r.yMax-3,r.width-16,2),accent);
            Text(Inset(r,8),text,size>0?size:BaseFont,Paper,TextAnchor.MiddleLeft,true);
        }
        public static void Tab(Rect r,bool selected,Color accent)
        {
            if(selected)Divider(new Rect(r.x+6,r.yMax-3,r.width-12,3),accent);
        }
        public static void Divider(Rect r,Color c)
        {
            if(Event.current.type!=EventType.Repaint)return;var keep=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=keep;
        }
        public static void Gem(Rect r,Color c)
        {
            Init();if(Event.current.type!=EventType.Repaint)return;var keep=GUI.color;GUI.color=c;GUI.DrawTexture(r,diamond);GUI.color=keep;
        }
        public static void Badge(Rect r,Texture2D icon,Color c,int shape=0,float iconLight=1f)
        {
            Init();if(Event.current.type!=EventType.Repaint)return;var keep=GUI.color;GUI.color=c;GUI.DrawTexture(r,shape==1?diamond:shape==2?hexagon:circle);
            GUI.color=new Color(iconLight,iconLight,iconLight,1);if(icon!=null)GUI.DrawTexture(Inset(r,r.width*(shape==1?.24f:.17f)),icon,ScaleMode.ScaleToFit,true);GUI.color=keep;
        }
        public static float TextHeight(string text,float width,int size)
        {
            Init();content.text=text;textStyle.fontSize=size;textStyle.fontStyle=FontStyle.Normal;
            return textStyle.CalcHeight(content,Mathf.Max(1,width));
        }
        public static void Halo(Rect r,Color c,float strength)
        {
            Init();Paint(r,frameStyle,new Color(c.r,c.g,c.b,Mathf.Clamp01(strength)));
        }
        public static void Lock(Rect r,Color c)
        {
            Divider(new Rect(r.x+r.width*.15f,r.y+r.height*.42f,r.width*.7f,r.height*.5f),c);
            Divider(new Rect(r.x+r.width*.25f,r.y+r.height*.12f,r.width*.5f,r.height*.12f),c);
            Divider(new Rect(r.x+r.width*.25f,r.y+r.height*.12f,r.width*.12f,r.height*.48f),c);
            Divider(new Rect(r.x+r.width*.63f,r.y+r.height*.12f,r.width*.12f,r.height*.48f),c);
        }
        public static void StatGlyph(Rect r,int stat,Color c)
        {
            // CJK abbreviations stay legible even on fonts without emoji glyphs.
            Text(r,statGlyphs[Mathf.Clamp(stat,0,5)],Mathf.RoundToInt(r.height*.45f),c,TextAnchor.MiddleCenter,true);
        }
        public static void Star(Rect r,Color c)
        {
            Gem(new Rect(r.x+r.width*.35f,r.y,r.width*.3f,r.height),c);
            Gem(new Rect(r.x,r.y+r.height*.35f,r.width,r.height*.3f),c);
        }
        public static void Backdrop(Rect r,Color accent,float alpha=.96f)
        {
            Init();Paint(r,panelStyle,new Color(.04f,.065f,.12f,alpha));
            if(Event.current.type!=EventType.Repaint)return;
            // Large, quiet constellations leave the content area readable.
            for(int i=0;i<7;i++){float y=r.y+r.height*(i+.5f)/7;Divider(new Rect(r.x+r.width*.08f,y,r.width*.84f,1),new Color(accent.r,accent.g,accent.b,.08f));Gem(new Rect(r.x+r.width*.89f-4,y-4,8,8),new Color(accent.r,accent.g,accent.b,.16f));}
        }
        // Procedural region vignette, also used for illustrated mode cards. No interaction.
        public static void Emblem(Rect r,int kind,Color c)
        {
            Badge(r,null,c,kind==0?1:2);float x=r.x+r.width*.23f,y=r.y+r.height*.24f,w=r.width*.54f,h=r.height*.52f;
            if(kind==0){Divider(new Rect(x+w*.44f,y,w*.12f,h),Paper);Divider(new Rect(x+w*.15f,y+h*.57f,w*.7f,h*.12f),Gold);}
            else if(kind==1){Gem(new Rect(x,y+h*.3f,w*.55f,h*.65f),Paper);Gem(new Rect(x+w*.45f,y,w*.55f,h*.65f),Gold);}
            else if(kind==2){Divider(new Rect(x,y,w,h),new Color(.7f,.85f,.68f,.6f));Gem(new Rect(x+w*.12f,y+h*.1f,w*.65f,h*.7f),Gold);}
            else{Gem(new Rect(x+w*.2f,y+h*.15f,w*.6f,h*.7f),Paper);Divider(new Rect(x,y+h*.47f,w,h*.07f),Gold);}
        }
        public static void ShopArt(Rect r,int kind,Color accent)
        {
            Badge(r,null,accent,2);float x=r.x+r.width*.2f,y=r.y+r.height*.2f,w=r.width*.6f,h=r.height*.6f;
            if(kind==0)
            {
                Divider(new Rect(x,y+h*.18f,w,h*.22f),Paper);
                Divider(new Rect(x+w*.3f,y+h*.35f,w*.35f,h*.4f),Gold);
                Divider(new Rect(x+w*.12f,y+h*.75f,w*.76f,h*.12f),Paper);
                Gem(new Rect(x+w*.78f,y+h*.12f,w*.25f,h*.3f),Gold);
            }
            else if(kind==1)
            {
                for(int j=0;j<3;j++)
                {
                    var bottle=new Rect(x+j*w*.34f,y+h*.27f,w*.28f,h*.55f);
                    Badge(bottle,null,j==0?new Color(.95f,.55f,.58f):j==1?new Color(.45f,.8f,1):new Color(.65f,.86f,.5f));
                    Divider(new Rect(bottle.x+bottle.width*.3f,y+h*.15f,bottle.width*.4f,h*.2f),Gold);
                }
            }
            else
            {
                Gem(new Rect(x,y,w,h),Gold);
                Divider(new Rect(x+w*.45f,y+h*.15f,w*.1f,h*.7f),Paper);
                Divider(new Rect(x+w*.2f,y+h*.55f,w*.6f,h*.1f),Paper);
            }
        }
        public static void MapArt(Rect r,int theme,Color c)
        {
            Card(r,.65f,c,.45f);
            if(Event.current.type!=EventType.Repaint)return;
            for(int i=0;i<5;i++)
            {
                var q=new Rect(r.x+r.width*(.1f+i*.18f),r.y+r.height*.27f,r.width*.12f,r.height*.5f);
                if(theme==0){Gem(q,new Color(c.r,c.g,c.b,.18f));}
                else if(theme==1){Badge(q,null,new Color(c.r,c.g,c.b,.15f),0);}
                else Divider(new Rect(q.x,q.y+q.height*.7f,q.width,q.height*.1f),new Color(c.r,c.g,c.b,.22f));
            }
        }
        public static void Result(Rect r,bool won)
        {
            Card(r,.98f,won?Gold:Muted,.95f);
            if(won)
            {
                for(int i=0;i<5;i++)Gem(new Rect(r.x+r.width*(.20f+i*.15f)-5,r.y+8,10,10),Gold);
                Star(new Rect(r.x+12,r.y+12,BaseFont,BaseFont),new Color(Gold.r,Gold.g,Gold.b,.65f));
                Star(new Rect(r.xMax-12-BaseFont,r.y+12,BaseFont,BaseFont),new Color(Gold.r,Gold.g,Gold.b,.65f));
            }
            else Divider(new Rect(r.x+12,r.y+8,r.width-24,2),new Color(.55f,.49f,.65f));
        }
    }
}
