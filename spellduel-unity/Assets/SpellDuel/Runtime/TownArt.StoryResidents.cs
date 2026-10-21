using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        static Mesh storyBeard;
        static void FitStoryBeard(Transform beard)
        {
            if(storyBeard==null)
            {
                var b=new OutfitArt.Builder();b.Tube(new[]{Vector3.zero,new Vector3(0,-.10f,.025f),new Vector3(0,-.26f,.035f),new Vector3(0,-.40f,.025f)},new[]{.11f,.095f,.055f,.004f},new[]{.035f,.045f,.025f,.004f},Color.white,12);storyBeard=b.Finish("Bell keeper long beard");
            }
            beard.GetComponent<MeshFilter>().sharedMesh=storyBeard;beard.localPosition=new Vector3(0,1.335f,.23f);beard.localRotation=Quaternion.identity;beard.localScale=Vector3.one;
        }
        // Named props follow the fitted hand; no gameplay components or interaction changes.
        static void StoryResidentProps(string id,Transform figure)
        {
            if(id!="艾琳"&&id!="凪")return;Transform hand=null;
            foreach(var tf in figure.GetComponentsInChildren<Transform>())if(tf.gameObject.name=="Outfit shoulder"&&tf.localPosition.x<0)hand=tf.Find("Outfit elbow").Find("Outfit hand");
            if(hand==null)return;
            var prop=Group(hand,id=="艾琳"?"Scribe carved staff":"Innkeeper lantern",new Vector3(0,-.03f,.035f));
            if(id=="艾琳")
            {
                Beam(prop,"Carved wooden staff",new Vector3(0,-.75f,0),new Vector3(0,.73f,0),.035f,Wood);
                for(int i=0;i<3;i++)Cyl(prop,"Carved staff collar",new Vector3(0,.52f+i*.055f,0),new Vector3(.055f,.014f,.055f),Gold);
                for(int side=-1;side<=1;side+=2)Beam(prop,"Orb cradle",new Vector3(0,.67f,0),new Vector3(side*.06f,.78f,0),.018f,Wood);
                Glow(prop,"Violet staff orb",new Vector3(0,.79f,0),.115f,C("ae74ef"));
            }
            else
            {
                // Closed frame and visible handle, not a floating light patch.
                for(int side=-1;side<=1;side+=2)Beam(prop,"Lantern handle",new Vector3(0,.01f,0),new Vector3(side*.055f,-.10f,0),.012f,Gold);
                Box(prop,"Lantern bottom",new Vector3(0,-.29f,0),new Vector3(.16f,.025f,.13f),Gold);
                Box(prop,"Lantern top",new Vector3(0,-.12f,0),new Vector3(.16f,.025f,.13f),Gold);
                for(int side=-1;side<=1;side+=2)for(int z=-1;z<=1;z+=2)Beam(prop,"Lantern frame",new Vector3(side*.07f,-.12f,z*.055f),new Vector3(side*.07f,-.29f,z*.055f),.015f,Gold);
                Box(prop,"Lantern warm glass",new Vector3(0,-.20f,0),new Vector3(.105f,.13f,.075f),Cream);
            }
            Batch(prop);
        }
    }
}
