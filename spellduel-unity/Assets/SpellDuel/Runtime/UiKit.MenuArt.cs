using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public static partial class UiKit
    {
        static readonly Dictionary<string,Texture2D> symbols=new Dictionary<string,Texture2D>();
        public static void MenuCard(Rect r,float alpha=.92f,Color? accent=null,float accentAlpha=.9f)
        {
            Card(r,Mathf.Lerp(.66f,1f,Mathf.Clamp01(alpha)),accent,accentAlpha);
            Init();Paint(Inset(r,3),bevelStyle,new Color(.76f,.86f,1f,.12f));
        }
        public static void RarityCard(Rect r,int rarity,float alpha=.94f)
        {
            var colour=Item.RarityColors[Mathf.Clamp(rarity,0,Item.RarityColors.Length-1)];
            MenuCard(r,alpha,colour,1);
            Divider(new Rect(r.x+5,r.y+8,3,Mathf.Max(0,r.height-16)),colour);
            if(rarity>=4)
            {
                // A narrow rim highlight, not a full-card flash; six-second period.
                float t=Mathf.Repeat(Time.unscaledTime*.16f,1f),x=r.x+8+t*Mathf.Max(0,r.width-16);
                float a=Mathf.Sin(t*Mathf.PI)*.48f;
                Divider(new Rect(x,r.y+2,Mathf.Min(18,r.xMax-x),2),new Color(1,1,1,a));
            }
        }
        // Marker geometry stays readable without an emoji font.
        public static void MapToken(Rect r,Color colour,bool quest)
        {
            Card(r,.98f,colour,1);
            if(quest)TrySymbol(r,"❗",Mathf.RoundToInt(r.height*.75f),colour);
            else
            {
                Disc(new Rect(r.center.x-r.width*.13f,r.y+r.height*.18f,r.width*.26f,r.height*.26f),colour);
                Gem(new Rect(r.center.x-r.width*.25f,r.y+r.height*.48f,r.width*.5f,r.height*.36f),colour);
            }
        }
        static bool TrySymbol(Rect r,string text,int size,Color colour)
        {
            string key;
            switch(text)
            {
                case "⚙":case "⚙️":key="gear";break;
                case "💰":key="coin";break;
                case "🧪":key="potion";break;
                case "📜":key="scroll";break;
                case "⚔":case "⚔️":key="sword";break;
                case "🛡":case "🛡️":key="shield";break;
                case "🎤":key="mic";break;
                case "❗":case "⚠":case "⚠️":key="alert";break;
                case "ⓘ":case "ℹ":key="info";break;
                case "✕":case "✖":case "×":key="close";break;
                case "✔":case "✓":key="check";break;
                default:return false;
            }
            if(!symbols.TryGetValue(key,out var tex))
            {
                tex=MakeSymbol(key);symbols[key]=tex;
            }
            float side=Mathf.Min(Mathf.Min(r.width,r.height),size*1.2f);
            var keep=GUI.color;GUI.color=colour;
            GUI.DrawTexture(new Rect(r.center.x-side*.5f,r.center.y-side*.5f,side,side),tex);GUI.color=keep;return true;
        }
        static float SymbolSegment(float x,float y,float ax,float ay,float bx,float by)
        {
            float dx=bx-ax,dy=by-ay,t=Mathf.Clamp01(((x-ax)*dx+(y-ay)*dy)/Mathf.Max(.0001f,dx*dx+dy*dy));
            return Mathf.Sqrt((x-ax-t*dx)*(x-ax-t*dx)+(y-ay-t*dy)*(y-ay-t*dy));
        }
        static Texture2D MakeSymbol(string key)
        {
            const int n=64;var tex=new Texture2D(n,n,TextureFormat.RGBA32,false){name="UI symbol "+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[n*n];
            for(int j=0;j<n;j++)for(int i=0;i<n;i++)
            {
                float x=(i+.5f)/n*2-1,y=(j+.5f)/n*2-1,len=Mathf.Sqrt(x*x+y*y),d=1;
                switch(key)
                {
                    case "coin":d=Mathf.Min(Mathf.Abs(len-.68f)-.085f,Mathf.Max(Mathf.Abs(x)-.065f,Mathf.Abs(y)-.42f));break;
                    case "gear":d=Mathf.Min(Mathf.Abs(len-.4f)-.095f,Mathf.Max(Mathf.Abs(len-.65f)-.17f,.55f-Mathf.Cos(Mathf.Atan2(y,x)*8)));break;
                    case "shield":d=Mathf.Max(Mathf.Abs(x)-(.62f+y*.22f),Mathf.Max(y-.65f,-y-.75f));d=Mathf.Abs(d)-.07f;break;
                    case "sword":d=Mathf.Min(SymbolSegment(x,y,-.5f,-.6f,.5f,.6f)-.09f,SymbolSegment(x,y,-.6f,-.12f,-.05f,-.58f)-.075f);break;
                    case "close":d=Mathf.Min(SymbolSegment(x,y,-.55f,-.55f,.55f,.55f),SymbolSegment(x,y,-.55f,.55f,.55f,-.55f))-.09f;break;
                    case "check":d=Mathf.Min(SymbolSegment(x,y,-.65f,0,-.15f,-.4f),SymbolSegment(x,y,-.15f,-.4f,.65f,.5f))-.095f;break;
                    case "alert":d=Mathf.Min(SymbolSegment(x,y,0,-.12f,0,.65f)-.11f,Mathf.Sqrt(x*x+(y+.52f)*(y+.52f))-.13f);break;
                    case "info":d=Mathf.Min(Mathf.Abs(len-.72f)-.07f,Mathf.Min(SymbolSegment(x,y,0,-.4f,0,.13f)-.09f,Mathf.Sqrt(x*x+(y-.42f)*(y-.42f))-.1f));break;
                    case "mic":d=Mathf.Min(Mathf.Abs(SymbolSegment(x,y,0,.05f,0,.48f)-.21f)-.06f,SymbolSegment(x,y,0,-.3f,0,-.7f)-.075f);d=Mathf.Min(d,SymbolSegment(x,y,-.3f,-.7f,.3f,-.7f)-.07f);break;
                    case "scroll":d=Mathf.Abs(Mathf.Max(Mathf.Abs(x)-.48f,Mathf.Abs(y)-.65f))-.075f;d=Mathf.Min(d,Mathf.Max(Mathf.Abs(x)-.25f,Mathf.Abs(y-.1f)-.065f));break;
                    case "potion":d=Mathf.Min(Mathf.Abs(Mathf.Sqrt(x*x+(y+.22f)*(y+.22f))-.43f)-.07f,Mathf.Abs(Mathf.Max(Mathf.Abs(x)-.17f,Mathf.Abs(y-.43f)-.23f))-.07f);break;
                }
                byte a=(byte)Mathf.RoundToInt(Mathf.Clamp01(.5f-d*n*.5f)*255);pixels[j*n+i]=new Color32(255,255,255,a);
            }
            tex.SetPixels32(pixels);tex.Apply(false,true);return tex;
        }
    }
}
