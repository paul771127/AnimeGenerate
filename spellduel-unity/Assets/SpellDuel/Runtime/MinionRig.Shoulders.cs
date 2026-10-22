using UnityEngine;
namespace SpellDuel
{
    public partial class MinionRig
    {
        static Mesh roundHumanoidTrunk,roundHumanoidArm;
        // Only bipedal imp/caster/rider surfaces. Animals, fungi and all Bosses retain their silhouettes.
        void FitHumanoidShoulders()
        {
            if(variantId>6&&variantId!=9&&variantId!=15)return;
            if(roundHumanoidTrunk==null)
            {
                var b=new OutfitArt.Builder();b.Loft(new[]{.50f,.45f,.30f,.04f,-.35f,-.50f},new[]{.16f,.25f,.44f,.50f,.44f,.40f},new[]{.19f,.27f,.43f,.49f,.44f,.38f},Color.white,0,0,12);roundHumanoidTrunk=b.Finish("Sloped minion clavicle");
                b=new OutfitArt.Builder();b.Loft(new[]{.50f,.43f,.23f,-.12f,-.40f,-.50f},new[]{.13f,.35f,.50f,.47f,.38f,.27f},new[]{.13f,.34f,.48f,.45f,.37f,.27f},Color.white,0,0,12);roundHumanoidArm=b.Finish("Round minion deltoid");
            }
            if(variantId<=6)
                foreach(var f in spine.GetComponentsInChildren<MeshFilter>())if(f.transform.parent==spine&&f.GetComponent<Renderer>().sharedMaterial.shader==litShader){f.sharedMesh=roundHumanoidTrunk;break;}
            foreach(var shoulder in new[]{shL,shR})
            {
                if(shoulder==null)continue;var at=shoulder.localPosition;at.x*=.94f;at.y-=.015f;shoulder.localPosition=at;
                if(variantId<=6)foreach(var f in shoulder.GetComponentsInChildren<MeshFilter>())if(f.transform.parent==shoulder){f.sharedMesh=roundHumanoidArm;break;}
            }
        }
    }
}
