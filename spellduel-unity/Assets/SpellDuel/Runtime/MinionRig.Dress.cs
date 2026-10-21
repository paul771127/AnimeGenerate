using UnityEngine;
namespace SpellDuel
{
    public partial class MinionRig
    {
        Transform dressRoot;
        // A local attachment frame for every anatomical type, including floating origin rigs.
        static readonly Vector3[] DressAt={new Vector3(0,-.10f,0),new Vector3(0,-.10f,0),new Vector3(0,-.10f,0),new Vector3(0,-.08f,0),new Vector3(0,-.10f,0),new Vector3(0,-.10f,0),new Vector3(0,-.10f,0),new Vector3(0,-.12f,0),new Vector3(0,.04f,-.08f),new Vector3(0,-.12f,0),new Vector3(0,-.12f,0),new Vector3(0,-.09f,0),new Vector3(0,-.10f,0),new Vector3(0,.02f,.15f),new Vector3(0,-.035f,-.025f),new Vector3(0,-.08f,0),new Vector3(0,.12f,.10f),new Vector3(0,-.03f,0),new Vector3(0,0,.10f)};
        static readonly float[] DressRadius={.13f,.12f,.15f,.13f,.12f,.12f,.12f,.18f,.25f,.15f,.19f,.15f,.18f,.16f,.085f,.11f,.21f,.30f,.19f};
        void ApplyDress(int theme)
        {
            if(dressRoot!=null){dressRoot.gameObject.SetActive(false);Destroy(dressRoot.gameObject);dressRoot=null;}
            if(theme!=3&&theme!=4||body==null)return;
            var anchor=head!=null?head:spine!=null?spine:body;
            dressRoot=Pivot("Regional dress",anchor,DressAt[variantId]);float r=DressRadius[variantId];
            if(theme==3)
            {
                // A closed collar with one black crystal and a recognisably broken three-link chain.
                TownArt.Collar(dressRoot,"Iron restraint",r,.05f,new Color(.25f,.32f,.40f));
                var jewel=TownArt.Box(dressRoot,"Black crystal",new Vector3(0,0,r+.012f),new Vector3(.07f,.08f,.045f),new Color(.10f,.065f,.20f));jewel.transform.localRotation=Quaternion.Euler(0,0,45);
                for(int i=0;i<3;i++)
                {
                    var link=new GameObject("Broken iron link").transform;link.SetParent(dressRoot,false);link.localPosition=new Vector3(r*.65f+i*.016f,-.045f-i*.055f,r*.55f);
                    if(i%2==1)link.localRotation=Quaternion.Euler(0,90,0);
                    for(int edge=0;edge<4;edge++)
                    {if(i==2&&edge==3)continue;var bar=TownArt.Box(link,"Link bar",new Vector3(edge<2?(edge==0?-.022f:.022f):0,edge<2?0:edge==2?-.027f:.027f,0),new Vector3(edge<2?.012f:.055f,edge<2?.058f:.012f,.012f),new Color(.55f,.63f,.70f));}
                }
            }
            else
            {
                TownArt.Collar(dressRoot,"Sand scarf wrap",r*1.04f,.08f,new Color(.82f,.64f,.35f));
                var tail=TownArt.Box(dressRoot,"Woven scarf tail",new Vector3(r*.7f,-.12f,-r*.65f),new Vector3(r*.55f,.23f,.035f),new Color(.74f,.51f,.26f));tail.transform.localRotation=Quaternion.Euler(-12,0,-12);
                for(int i=0;i<3;i++)TownArt.Ball(dressRoot,"Sand caught in cloth",new Vector3((i-1)*r*.45f,.026f,r*.75f),new Vector3(.035f,.018f,.025f),new Color(.94f,.79f,.51f));
            }
            // One renderer/material group rather than one renderer per chain bar.
            TownArt.BatchAttachment(dressRoot);
        }
    }
}
