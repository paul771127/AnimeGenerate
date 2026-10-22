using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Cached cel-painted GUI illustrations; no imported images and no per-frame rasterization.
    internal static class VillageIllustrations
    {
        static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        internal static Texture2D Get(int kind,Color accent)
        {
            string key=kind+ColorUtility.ToHtmlStringRGB(accent);if(cache.TryGetValue(key,out var found))return found;
            var pixels=Pixels(kind,accent,out int w,out int h);var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Village illustration "+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};tex.SetPixels32(pixels);tex.Apply(false,true);cache[key]=tex;return tex;
        }
        sealed class Paint
        {
            internal int w,h;internal Color32[] pixels;
            internal Paint(int width,int height){w=width;h=height;pixels=new Color32[w*h];}
            internal void Poly(Color c,params float[] p)
            {
                float minX=w,maxX=0,minY=h,maxY=0;for(int i=0;i<p.Length;i+=2){minX=Mathf.Min(minX,p[i]);maxX=Mathf.Max(maxX,p[i]);minY=Mathf.Min(minY,p[i+1]);maxY=Mathf.Max(maxY,p[i+1]);}
                for(int y=Mathf.Max(0,Mathf.FloorToInt(minY));y<Mathf.Min(h,Mathf.CeilToInt(maxY));y++)for(int x=Mathf.Max(0,Mathf.FloorToInt(minX));x<Mathf.Min(w,Mathf.CeilToInt(maxX));x++)
                {bool inside=false;for(int i=0,j=p.Length-2;i<p.Length;j=i,i+=2)if((p[i+1]>y+.5f)!=(p[j+1]>y+.5f)&&(x+.5f<(p[j]-p[i])*(y+.5f-p[i+1])/(p[j+1]-p[i+1])+p[i]))inside=!inside;if(inside)pixels[(h-1-y)*w+x]=c;}
            }
            internal void Circle(float x,float y,float radius,Color c)
            {for(int yy=Mathf.Max(0,Mathf.FloorToInt(y-radius));yy<Mathf.Min(h,Mathf.CeilToInt(y+radius));yy++)for(int xx=Mathf.Max(0,Mathf.FloorToInt(x-radius));xx<Mathf.Min(w,Mathf.CeilToInt(x+radius));xx++)if((xx+.5f-x)*(xx+.5f-x)+(yy+.5f-y)*(yy+.5f-y)<radius*radius)pixels[(h-1-yy)*w+xx]=c;}
            internal void Stroke(float x,float y,float xx,float yy,float width,Color c)
            {float length=Mathf.Sqrt((xx-x)*(xx-x)+(yy-y)*(yy-y));int steps=Mathf.Max(1,Mathf.CeilToInt(length/2));for(int i=0;i<=steps;i++)Circle(Mathf.Lerp(x,xx,i/(float)steps),Mathf.Lerp(y,yy,i/(float)steps),width*.5f,c);}
        }
        internal static Color32[] Pixels(int kind,Color accent,out int width,out int height)
        {
            width=256;height=kind==0?128:256;var p=new Paint(width,height);Color ink=CharacterLook.Hex("273247"),paper=CharacterLook.Hex("fff1d1"),gold=CharacterLook.Hex("e7b756"),wood=CharacterLook.Hex("986741"),shade=Color.Lerp(accent,ink,.55f);
            if(kind==0)
            {
                p.Circle(131,26,63,new Color(accent.r,accent.g,accent.b,.18f));
                p.Poly(ink,103,-8,147,-8,153,111,172,124,81,124,99,111);p.Poly(shade,108,-8,144,-8,149,111,126,119,103,110);p.Poly(accent,109,-8,121,-8,120,114,103,110);
                for(int i=0;i<7;i++){float y=8+i*15;p.Poly(gold,102,y,149,y,149,y+3,102,y+3);for(int j=0;j<2;j++){float x=116+j*17;p.Poly(ink,x,y+5,x+6,y+5,x+6,y+12,x,y+12);p.Poly(paper,x+1,y+6,x+4,y+6,x+4,y+11,x+1,y+11);}}
                for(int i=0;i<3;i++){float x=23+i*73,y=24+i*31;p.Stroke(x,y,x+57,y,6,new Color(.80f,.84f,1,.55f));p.Stroke(x+8,y+6,x+40,y+6,3,new Color(.8f,.84f,1,.35f));}
                p.Stroke(75,125,178,125,4,gold);
            }
            else if(kind==1)
            {
                p.Circle(128,128,113,new Color(accent.r,accent.g,accent.b,.15f));p.Stroke(85,170,63,225,10,ink);p.Stroke(130,164,151,225,10,ink);p.Stroke(85,170,63,225,6,wood);p.Stroke(130,164,151,225,6,wood);
                p.Stroke(205,114,205,225,10,wood);p.Stroke(173,139,235,139,10,wood);p.Circle(205,97,24,ink);p.Circle(205,96,20,gold);p.Poly(shade,184,121,226,121,223,171,188,171);p.Stroke(190,151,220,151,5,paper);
                p.Circle(106,117,78,ink);p.Circle(102,111,74,wood);p.Circle(102,111,65,paper);p.Circle(102,111,51,accent);p.Circle(102,111,37,paper);p.Circle(102,111,23,shade);p.Circle(102,111,11,gold);
                p.Poly(new Color(1,1,1,.2f),47,75,95,47,156,90,109,96);p.Stroke(223,28,106,109,8,ink);p.Stroke(223,28,106,109,4,gold);p.Poly(paper,221,22,222,45,202,37,202,18);p.Poly(gold,96,115,112,91,121,115);
            }
            else
            {
                p.Circle(125,121,111,new Color(accent.r,accent.g,accent.b,.15f));
                for(int i=0;i<10;i++){float a=i*Mathf.PI/5;float[] points=new float[8];for(int j=0;j<4;j++){float radius=j<2?105:77,angle=a+(j==0||j==3?-.13f:.13f);points[j*2]=111+Mathf.Cos(angle)*radius;points[j*2+1]=113+Mathf.Sin(angle)*radius;}p.Poly(ink,points);for(int j=0;j<4;j++){points[j*2]=111+(points[j*2]-111)*.92f;points[j*2+1]=113+(points[j*2+1]-113)*.92f;}p.Poly(accent,points);}
                p.Circle(111,113,85,ink);p.Circle(111,111,79,accent);p.Poly(shade,112,34,188,95,183,147,133,185,112,111);p.Circle(111,111,52,ink);p.Circle(111,109,45,paper);p.Circle(111,109,32,shade);p.Poly(gold,111,86,131,98,131,121,111,133,91,121,91,98);
                p.Circle(204,194,37,ink);p.Circle(204,192,31,gold);for(int i=0;i<6;i++){float a=i*Mathf.PI/3;p.Stroke(204+Mathf.Cos(a)*25,192+Mathf.Sin(a)*25,204+Mathf.Cos(a)*38,192+Mathf.Sin(a)*38,10,gold);}p.Circle(204,192,14,ink);p.Circle(204,192,8,paper);
                for(int j=0;j<2;j++){float y=204+j*22;p.Stroke(25,y,132,y,10,ink);p.Stroke(25,y,132,y,4,paper);p.Circle(j==0?65:101,y,11,gold);p.Circle(j==0?65:101,y,5,paper);}
            }
            return p.pixels;
        }
    }
}
