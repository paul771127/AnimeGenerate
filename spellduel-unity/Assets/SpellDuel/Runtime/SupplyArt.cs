using UnityEngine;
namespace SpellDuel {
public static class SupplyArt {
static Texture2D dot;
public static bool Draw(Rect r,string id){if(!SkillIcons.IsSupply(id))return false;var tex=SkillIcons.Get(id);if(!tex)return false;GUI.DrawTexture(r,tex,ScaleMode.ScaleToFit,true);return true;}
public static void Disc(Rect r,Color c){if(!dot){dot=new Texture2D(32,32,TextureFormat.RGBA32,false);var a=new Color32[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float d=new Vector2(x-15.5f,y-15.5f).magnitude;byte alpha=(byte)(Mathf.Clamp01(15.5f-d)*255);a[y*32+x]=new Color32(255,255,255,alpha);}dot.SetPixels32(a);dot.Apply(false,true);}var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,dot);GUI.color=old;}
}
}
