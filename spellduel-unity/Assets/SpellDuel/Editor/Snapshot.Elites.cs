#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace SpellDuel.EditorTools
{
    public static partial class Snapshot
    {
        static void EliteShots(Camera cam)
        {
            EliteAudit.Run();var step=typeof(MinionRig).GetMethod("Animate",BindingFlags.NonPublic|BindingFlags.Instance);
            for(int chapter=0;chapter<5;chapter++)
            {
                var fronts=new List<string>();var sides=new List<string>();
                for(int i=0;i<5;i++)
                {
                    int kind=23+chapter*5+i;var d=EliteCatalog.Get(kind);var rig=MinionRig.Create();rig.SetVariant(kind);
                    string prefix="elite_"+kind;
                    Shot(cam,rig.gameObject,prefix+"_front",new Vector3(0,1.35f,4.4f),new Vector3(0,.9f,0),640,640);fronts.Add(prefix+"_front");
                    Shot(cam,rig.gameObject,prefix+"_45",new Vector3(3,1.5f,3.2f),new Vector3(0,.9f,0),640,640);sides.Add(prefix+"_45");
                    for(int slot=0;slot<2;slot++)
                    {
                        var move=d.moves[slot];rig.EliteSkill(slot);rig.Windup(EliteCatalog.Windup(move));step.Invoke(rig,new object[]{EliteCatalog.Windup(move)});
                        var fx=EliteEnemyFx.Create(null,move);fx.Place(new Vector3(0,0,move==EliteMove.Burst?1.4f:0),Vector3.forward,d.color,.85f);
                        Shot(cam,rig.gameObject,prefix+"_skill"+slot+"_warn",new Vector3(4,4,5),new Vector3(0,.5f,.6f),800,640);
                        rig.Attack();step.Invoke(rig,new object[]{.17f});fx.Place(Vector3.zero,Vector3.forward,d.color,.2f,true);
                        if(move==EliteMove.Sweep)fx.transform.position+=Vector3.up*1.2f;
                        if(move==EliteMove.Quake)fx.transform.localScale=Vector3.one*.4f;
                        Shot(cam,rig.gameObject,prefix+"_skill"+slot+"_impact",new Vector3(4,3.6f,5),new Vector3(0,.6f,.6f),800,640);Object.DestroyImmediate(fx.gameObject);
                    }
                    Object.DestroyImmediate(rig.gameObject);
                }
                EliteSheet(fronts,"elite_chapter"+(chapter+1)+"_front");EliteSheet(sides,"elite_chapter"+(chapter+1)+"_45");
            }
        }
        static void EliteSheet(List<string> images,string name)
        {
            const int tile=320;var sheet=new Texture2D(tile*images.Count,tile,TextureFormat.RGB24,false);
            for(int i=0;i<images.Count;i++)
            {
                var src=new Texture2D(2,2);src.LoadImage(System.IO.File.ReadAllBytes("snapshots/"+images[i]+".png"));var pixels=new Color32[tile*tile];
                for(int y=0;y<tile;y++)for(int x=0;x<tile;x++)pixels[y*tile+x]=src.GetPixelBilinear((x+.5f)/tile,(y+.5f)/tile);
                sheet.SetPixels32(i*tile,0,tile,tile,pixels);Object.DestroyImmediate(src);
            }
            sheet.Apply();System.IO.File.WriteAllBytes("snapshots/"+name+".png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
        }
    }
}
#endif
