using System;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    public static partial class SkillIcons
    {
        static readonly Dictionary<(PassiveKind,string),Texture2D> passiveCache=new Dictionary<(PassiveKind,string),Texture2D>();
        public static Texture2D GetPassive(PassiveKind kind,string cls)
        {
            var key=(kind,cls);
            if(passiveCache.TryGetValue(key,out var t)&&t!=null)return t;
            t=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Passive "+cls+" "+kind,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            t.SetPixels32(RenderPassive(kind,cls,Size));t.Apply(false,true);passiveCache[key]=t;return t;
        }
        public static Color32[] RenderPassive(PassiveKind kind,string cls,int size)
        {
            size=Math.Max(8,Math.Min(1024,size));var cv=new Cv(size);
            var colour=Skills.Classes.TryGetValue(cls??"",out var cd)?new V(cd.color.r,cd.color.g,cd.color.b):V.Hex("#e7bc68");
            var light=V.Lerp(colour,V.White,.65f);Background(cv,colour,colour,"passive:"+kind);
            string name=kind.ToString();
            if(name.Contains("Shield")||name.Contains("Guard")||name=="Armor"||name=="LastStand")
                Glyph(cv,(x,y)=>Math.Max(Math.Abs(x)*.78f-y*.18f-.27f,Math.Max(y-.4f,-y-.43f)),colour);
            else if(name.Contains("Regen")||name.Contains("Hp")||name.Contains("Leech")||name.Contains("heal")||name=="Lifesteal")
                Glyph(cv,(x,y)=>Math.Min(Box(x,y,0,0,.11f,.4f),Box(x,y,0,0,.36f,.11f)),colour);
            else if(name.Contains("Ice")||name.Contains("Freeze"))
            {
                for(int i=0;i<3;i++){float a=i*(float)Math.PI/3;float dx=(float)Math.Cos(a)*.4f,dy=(float)Math.Sin(a)*.4f;Glyph(cv,(x,y)=>Seg(x,y,-dx,-dy,dx,dy)-.045f,colour);}
            }
            else if(name.Contains("Poison")||name.Contains("Venom")||name=="Burn"||name.Contains("Fire"))
                Glyph(cv,(x,y)=>Math.Max(Len(x,y+.12f)-.28f,Math.Abs(x)-(.42f-y*.55f)),colour);
            else if(name.Contains("Crit")||name.Contains("Aim")||name.Contains("Shot")||name=="Accuracy")
            {
                Glyph(cv,(x,y)=>Math.Abs(Len(x,y)-.3f)-.065f,colour);
                cv.Solid((x,y)=>Len(x,y)-.075f,light);
            }
            else if(name.Contains("Mp")||name.Contains("Spell")||name.Contains("Arcane")||name.Contains("Cast"))
                Glyph(cv,(x,y)=>Diamond(x,y,0,0,.28f,.43f,0),colour);
            else
            {
                Glyph(cv,(x,y)=>Seg(x,y,-.26f,-.34f,.26f,.34f)-.085f,colour);
                Glyph(cv,(x,y)=>Seg(x,y,-.3f,-.13f,-.02f,-.36f)-.055f,light);
            }
            // Inset diamond and twin lower bars distinguish a passive from a circular active.
            cv.Solid((x,y)=>Math.Abs(Math.Abs(x)+Math.Abs(y)-.84f)-.065f,new V(.012f,.018f,.035f));
            cv.Fill((x,y)=>Math.Abs(Math.Abs(x)+Math.Abs(y)-.84f)-.033f,(x,y,d)=>y-x*.35f>0?light:colour*.55f);
            cv.Solid((x,y)=>Box(x,y,-.11f,-.67f,.045f,.055f),light);
            cv.Solid((x,y)=>Box(x,y,.11f,-.67f,.045f,.055f),light);
            return cv.Output();
        }
    }
}
