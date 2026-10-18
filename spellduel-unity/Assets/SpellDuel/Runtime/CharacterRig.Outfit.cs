using UnityEngine;
using System.Collections.Generic;
namespace SpellDuel
{
    public partial class CharacterRig
    {
        MeshFilter outfitHandL,outfitHandR;
        Mesh[] handShapesL,handShapesR;bool heldHandL,heldHandR;
        void UpdateHandShapes(float cast,float hurt)
        {
            if(outfitHandL==null)return;
            int open=cast>.05f||chargeAmt>.10f?2:0;
            outfitHandL.sharedMesh=handShapesL[heldHandL?1:open];outfitHandR.sharedMesh=handShapesR[heldHandR?1:open];
        }
        void BuildHands(string identity)
        {
            heldHandL=handL.GetComponentsInChildren<MeshFilter>().Length>0;heldHandR=handR.GetComponentsInChildren<MeshFilter>().Length>0;
            handShapesL=new Mesh[3];handShapesR=new Mesh[3];string[] poses={"Relax","Grip","Open"};
            for(int i=0;i<3;i++){handShapesL[i]=OutfitArt.MeshFor(identity,"hand"+poses[i]+"L",Skin);handShapesR[i]=OutfitArt.MeshFor(identity,"hand"+poses[i]+"R",Skin);}
            outfitHandL=AnimeFace.Part(handL,"Outfit handL",handShapesL[heldHandL?1:0],Vector3.zero,AnimeFace.Material(Color.white,4)).GetComponent<MeshFilter>();
            outfitHandR=AnimeFace.Part(handR,"Outfit handR",handShapesR[heldHandR?1:0],Vector3.zero,AnimeFace.Material(Color.white,4)).GetComponent<MeshFilter>();
        }
        static readonly Dictionary<string,Mesh> outfitPropMeshes=new Dictionary<string,Mesh>();
        void BatchOutfitProp(Transform root,string identity)
        {
            if(root==null)return;var groups=new Dictionary<Material,List<CombineInstance>>();var old=new List<GameObject>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var material=filter.GetComponent<Renderer>().sharedMaterial;
                // Animated light orbs keep their own transforms and transparent material.
                if(material.shader==glowShader)continue;
                if(!groups.TryGetValue(material,out var list)){list=new List<CombineInstance>();groups[material]=list;}
                list.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});old.Add(filter.gameObject);
            }
            foreach(var pair in groups)
            {
                string key=identity+"/"+root.gameObject.name+"/"+pair.Key.name;
                if(!outfitPropMeshes.TryGetValue(key,out var mesh))
                {mesh=new Mesh{name="Outfit prop "+key};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();outfitPropMeshes[key]=mesh;}
                AnimeFace.Part(root,"Outfit prop batch",mesh,Vector3.zero,pair.Key);
            }
            foreach(var go in old){go.transform.SetParent(null,false);RemoveVisual(go);}
        }
        // Keep the original grip pivots, poses and special props; replace only clothing render meshes.
        void BuildLayeredOutfit()
        {
            string identity=advancedId??classId;
            foreach(var filter in body.GetComponentsInChildren<MeshFilter>())
            {
                var tf=filter.transform;var renderer=filter.GetComponent<Renderer>();var material=renderer.sharedMaterial;
                if(material.name==null||!material.name.StartsWith("Rig"))continue;
                bool held=(tf.IsChildOf(handL)&&tf.parent!=handL)||(tf.IsChildOf(handR)&&tf.parent!=handR);
                bool prop=shield!=null&&tf.IsChildOf(shield)||floatingBook!=null&&tf.IsChildOf(floatingBook);
                for(var at=tf;at!=null&&at!=body;at=at.parent)if(at.gameObject.name=="Quiver"||at.gameObject.name=="NinjaSword"||at.gameObject.name=="PerchedFalcon")prop=true;
                bool neckSkin=tf.parent==spine&&ColorUtility.ToHtmlStringRGB(material.color)==ColorUtility.ToHtmlStringRGB(Skin);
                bool effect=material.name.StartsWith("RigGlow")&&!tf.IsChildOf(head)||halo!=null&&tf.IsChildOf(halo);
                if(!held&&!prop&&!neckSkin&&!effect){tf.SetParent(null,false);RemoveVisual(filter.gameObject);}
            }
            foreach(var hand in new[]{handL,handR})
            {
                var held=new List<Transform>();for(int i=0;i<hand.childCount;i++)held.Add(hand.GetChild(i));
                foreach(var prop in held)if(prop.GetComponentsInChildren<MeshFilter>().Length>0)BatchOutfitProp(prop,identity);
            }
            BatchOutfitProp(shield,identity);BatchOutfitProp(floatingBook,identity);
            var quiver=spine.Find("Quiver");BatchOutfitProp(quiver,identity);
            OutfitArt.Add(spine,identity,"torso",Skin);
            OutfitArt.Add(hipL,identity,"thighL",Skin);OutfitArt.Add(hipR,identity,"thighR",Skin);
            OutfitArt.Add(knL,identity,"calfL",Skin);OutfitArt.Add(knR,identity,"calfR",Skin);
            OutfitArt.Add(shL,identity,"upperL",Skin);OutfitArt.Add(shR,identity,"upperR",Skin);
            OutfitArt.Add(elL,identity,"foreL",Skin);OutfitArt.Add(elR,identity,"foreR",Skin);
            BuildHands(identity);HeadwearArt.Build(head,identity,facePersona);
            if(cape!=null)OutfitArt.Add(cape,identity,"cape",Skin);
            if(scarfTail!=null)OutfitArt.Add(scarfTail,identity,"scarf",Skin);
            foreach(var part in spine.GetComponentsInChildren<Transform>())if(part.gameObject.name=="DanceRibbon")OutfitArt.Add(part,identity,part.localPosition.x<0?"ribbonL":"ribbonR",Skin);
        }
    }
}
