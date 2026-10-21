using System.Collections.Generic;
using UnityEngine;
namespace SpellDuel
{
    // Residents have their own rounded tailoring: combat meshes and bone lengths stay untouched.
    internal static class ResidentBody
    {
        static readonly Dictionary<string,Mesh> cache=new Dictionary<string,Mesh>();
        internal static GameObject Add(Transform parent,string id,string part,Color skin)
            =>AnimeFace.Part(parent,"Outfit "+part,MeshFor(id,part,skin),Vector3.zero,AnimeFace.Material(Color.white,4));
        internal static Mesh MeshFor(string id,string part,Color skin)
        {
            string key=id+part+ColorUtility.ToHtmlStringRGB(skin);if(cache.TryGetValue(key,out var mesh))return mesh;
            var d=OutfitArt.Get(id);var b=new OutfitArt.Builder{Skin=skin};float bulk=id=="鐵匠"?1.10f:1;
            if(part=="torso")
            {
                b.Loft(new[]{.71f,.68f,.645f,.59f,.49f,.35f,.19f,.09f,0f},
                    new[]{.055f,.080f,.146f,.200f,.205f,.181f,.151f,.168f,.176f},
                    new[]{.052f,.066f,.084f,.105f,.130f,.122f,.104f,.116f,.119f},d.Main,0,0,16);
                b.Loft(new[]{.075f,.045f},new[]{.173f,.178f},new[]{.120f,.123f},d.Sub,0,0,16);
                if(d.Hem>.15f)b.Loft(new[]{.045f,-d.Hem*.35f,-d.Hem*.75f,-d.Hem},
                    new[]{.177f,.181f,.186f,.188f},new[]{.123f,.128f,.131f,.129f},d.Main,0,0,16);
                if(id=="艾琳")
                {
                    // Lowered hood: a padded oval behind the neck, keeping all silver-lilac hair visible.
                    b.Loft(new[]{.68f,.59f,.49f},new[]{.095f,.14f,.11f},new[]{.08f,.12f,.09f},d.Main,0,-.09f,16);
                    for(int side=-1;side<=1;side+=2)b.Tube(new[]{new Vector3(side*.077f,.64f,.077f),new Vector3(side*.10f,.43f,.139f),new Vector3(side*.035f,.08f,.129f),new Vector3(side*.05f,-.46f,.131f)},new[]{.005f,.005f,.005f,.005f},new[]{.004f,.004f,.004f,.004f},d.Accent,6);
                }
                // One readable fastener; no loose contrasting panels.
                b.Loft(new[]{.070f,.054f,.041f},new[]{.015f,.018f,.015f},new[]{.004f,.006f,.004f},d.Accent,0,.126f,8);
                if(id=="鐵匠"||id=="小鐵"||id=="凪")
                    b.Panel(new Vector3(-.10f,.50f,.137f),new Vector3(.10f,.50f,.137f),new Vector3(-.14f,id=="凪"?-.42f:-.16f,.137f),new Vector3(.14f,id=="凪"?-.42f:-.16f,.137f),d.Sub,.012f,6,5);
            }
            else if(part.StartsWith("upper"))
                b.Loft(new[]{.042f,.012f,-.035f,-.10f,-.19f,-.28f,-.295f},new[]{.036f,.060f,.072f,.067f,.053f,.040f,.037f},new[]{.035f,.058f,.068f,.064f,.050f,.039f,.037f},id=="鐵匠"?skin:d.Main,0,0,12);
            else if(part.StartsWith("fore"))
                b.Loft(new[]{.025f,0f,-.055f,-.12f,-.20f,-.26f},new[]{.039f,.043f,.055f,.047f,.033f,.030f},new[]{.037f,.041f,.050f,.041f,.029f,.026f},id=="鐵匠"||id=="小鐵"?skin:d.Main,0,0,12);
            else if(part.StartsWith("hand"))
            {
                // Joined fingers form a single mitten hull; only the thumb is separate.
                b.Loft(new[]{.012f,-.015f,-.052f,-.091f,-.110f},new[]{.027f,.038f,.040f,.033f,.023f},new[]{.023f,.026f,.027f,.024f,.016f},skin,0,.008f,12);
                float side=part.EndsWith("L")?-1:1;
                b.Tube(new[]{new Vector3(side*.026f,-.018f,.012f),new Vector3(side*.047f,-.040f,.025f),new Vector3(side*.042f,-.064f,.035f)},new[]{.018f,.017f,.012f},new[]{.017f,.016f,.011f},skin,10);
            }
            else if(part.StartsWith("thigh"))
                b.Loft(new[]{.075f,.015f,-.10f,-.24f,-.36f,-.435f},new[]{.076f,.087f,.084f,.073f,.058f,.053f},new[]{.078f,.087f,.084f,.072f,.057f,.052f},d.Sub,0,0,12);
            else if(part.StartsWith("calf"))
            {
                b.Loft(new[]{.023f,-.025f,-.10f,-.18f,-.27f,-.355f},new[]{.054f,.057f,.069f,.064f,.045f,.037f},new[]{.052f,.055f,.068f,.062f,.043f,.037f},d.Sub,0,0,12);
                b.Tube(new[]{new Vector3(0,-.32f,0),new Vector3(0,-.37f,.033f),new Vector3(0,-.414f,.040f),new Vector3(0,-.43f,.040f)},new[]{.039f,.060f,.068f,.068f},new[]{.045f,.093f,.120f,.120f},d.Sub,12);
            }
            if(bulk!=1)for(int i=0;i<b.V.Count;i++){var v=b.V[i];v.x*=bulk;b.V[i]=v;}
            mesh=b.Finish("Resident body "+key);cache[key]=mesh;return mesh;
        }
    }
}
