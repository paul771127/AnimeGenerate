using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        static readonly Dictionary<string,int> matchBaselines=new Dictionary<string,int>{{"novice",9296},{"swordsman",10112},{"archer",9800},{"mage",9392},{"assassin",9180},{"priest",10888},{"paladin",10888},{"berserker",9686},{"marksman",9888},{"ranger",10772},{"archmage",10192},{"summoner",10212},{"ninja",9356},{"shadowdancer",10036},{"bishop",10220},{"inquisitor",9020}};
        internal static int MatchTriangleBudget(string id)=>matchBaselines.TryGetValue(id,out var baseline)?baseline*3/2:15000;
        static Texture2D MatchLoad(string path)
        {var t=new Texture2D(2,2);t.LoadImage(File.ReadAllBytes(path));return t;}
        static void MatchBlit(Color32[] dst,int width,Texture2D src,int x,int y,int w,int h)
        {
            var pixels=src.GetPixels32();
            for(int j=0;j<h;j++)for(int i=0;i<w;i++)
            {
                var c=pixels[Mathf.Min(src.height-1,j*src.height/h)*src.width+Mathf.Min(src.width-1,i*src.width/w)];
                int at=(y+j)*width+x+i;float a=c.a/255f;var old=dst[at];
                dst[at]=new Color32((byte)(c.r*a+old.r*(1-a)),(byte)(c.g*a+old.g*(1-a)),(byte)(c.b*a+old.b*(1-a)),255);
            }
        }
        static void MatchCard(Camera cam,CharacterRig rig,string id)
        {
            Shot(cam,rig.gameObject,"match_"+id+"_front",new Vector3(0,1.23f,2.75f),new Vector3(0,1.18f,0),512,768);
            Shot(cam,rig.gameObject,"match_"+id+"_45",new Vector3(1.945f,1.23f,1.945f),new Vector3(0,1.18f,0),512,768);
            var art=MatchLoad("Assets/SpellDuel/Resources/Story2D/classes/"+id+".png");
            var front=MatchLoad("snapshots/match_"+id+"_front.png");var side=MatchLoad("snapshots/match_"+id+"_45.png");
            var pixels=new Color32[1536*768];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(32,43,62,255);
            MatchBlit(pixels,1536,art,0,0,512,768);MatchBlit(pixels,1536,front,512,0,512,768);MatchBlit(pixels,1536,side,1024,0,512,768);
            var sheet=new Texture2D(1536,768,TextureFormat.RGB24,false);sheet.SetPixels32(pixels);sheet.Apply();File.WriteAllBytes("snapshots/match_"+id+".png",sheet.EncodeToPNG());
            Object.DestroyImmediate(art);Object.DestroyImmediate(front);Object.DestroyImmediate(side);Object.DestroyImmediate(sheet);
        }
        static void MatchOverview()
        {
            var pixels=new Color32[1536*768];int i=0;
            foreach(var pair in matchBaselines)
            {
                var card=MatchLoad("snapshots/match_"+pair.Key+".png");MatchBlit(pixels,1536,card,(i%4)*384,(3-i/4)*192,384,192);i++;Object.DestroyImmediate(card);
            }
            var sheet=new Texture2D(1536,768,TextureFormat.RGB24,false);sheet.SetPixels32(pixels);sheet.Apply();File.WriteAllBytes("snapshots/match_all_16.png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
        }
    }
}
