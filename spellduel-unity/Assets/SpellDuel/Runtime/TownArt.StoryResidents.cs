using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        static void FitStoryHeight(Transform face,string id,Transform actor,float height)
        {
            if(!StoryCostume.Has(id))return;
            float top=-10,chin=10,lo=10,hi=-10;
            foreach(var f in face.GetComponentsInChildren<MeshFilter>())
            {
                var m=actor.worldToLocalMatrix*f.transform.localToWorldMatrix;
                foreach(var v in f.sharedMesh.vertices)top=Mathf.Max(top,m.MultiplyPoint3x4(v).y);
            }
            var mx=actor.worldToLocalMatrix*face.localToWorldMatrix;
            foreach(var v in AnimeFace.FittingVertices(Portraits.PersonaFor(id))){var w=mx.MultiplyPoint3x4(v);chin=Mathf.Min(chin,w.y);lo=Mathf.Min(lo,w.x);hi=Mathf.Max(hi,w.x);}
            float heads=id=="小葉"?4.8f:id=="艾琳"?6.2f:id=="凪"?6.4f:id=="翠"?5.6f:id=="老焰"?7.1f:id=="海斗"?6.9f:6.6f;
            float sy=(height/heads)/(top-chin),width=id=="小葉"?.175f:id=="翠"?.175f:.185f;
            var scale=face.localScale;scale.x*=width/(hi-lo);scale.y*=sy;scale.z*=sy;face.localScale=scale;
            top=-10;foreach(var f in face.GetComponentsInChildren<MeshFilter>()){var m=actor.worldToLocalMatrix*f.transform.localToWorldMatrix;foreach(var v in f.sharedMesh.vertices)top=Mathf.Max(top,m.MultiplyPoint3x4(v).y);}
            // New hood/topknot/crown must fit the original resident height, never stretch the neck.
            face.position+=actor.TransformPoint(new Vector3(0,height-top,0))-actor.TransformPoint(Vector3.zero);
        }
        static Mesh storyBeard;
        static void FitStoryBeard(Transform beard)
        {
            if(storyBeard==null)
            {
                var b=new OutfitArt.Builder();b.Tube(new[]{Vector3.zero,new Vector3(0,-.10f,.025f),new Vector3(0,-.47f,.055f),new Vector3(0,-.78f,.04f)},new[]{.11f,.105f,.075f,.006f},new[]{.035f,.045f,.025f,.004f},Color.white,12);storyBeard=b.Finish("Bell keeper long beard");
            }
            beard.GetComponent<MeshFilter>().sharedMesh=storyBeard;beard.localPosition=new Vector3(0,1.335f,.23f);beard.localRotation=Quaternion.identity;beard.localScale=Vector3.one;
        }
        // Named props follow the fitted hand; no gameplay components or interaction changes.
        static void StoryResidentProps(string id,Transform figure)
        {
            if(id=="小葉")
            {
                var tablet=Group(figure,"Chest story tablet",new Vector3(0,1.08f,.285f));
                ResidentBox(tablet,"Wood tablet frame",Vector3.zero,new Vector3(.30f,.33f,.036f),C("91663f"));ResidentBox(tablet,"Story tablet paper",new Vector3(0,0,.022f),new Vector3(.258f,.29f,.012f),C("ead4a4"));
                for(int i=0;i<6;i++)Beam(tablet,"Written story",new Vector3(-.095f,.105f-i*.038f,.031f),new Vector3(.095f,.105f-i*.038f,.031f),.003f,C("80653f"));Batch(tablet);return;
            }
            if(id!="艾琳"&&id!="凪")return;Transform hand=null;
            foreach(var tf in figure.GetComponentsInChildren<Transform>())if(tf.gameObject.name=="Outfit shoulder"&&tf.localPosition.x<0)hand=tf.Find("Outfit elbow").Find("Outfit hand");
            if(hand==null)return;
            var prop=Group(hand,id=="艾琳"?"Scribe carved staff":"Innkeeper lantern",new Vector3(0,-.03f,.035f));
            if(id=="艾琳")
            {
                Beam(prop,"Carved wooden staff",new Vector3(0,-.75f,0),new Vector3(0,.73f,0),.035f,Wood);
                for(int i=0;i<3;i++)Cyl(prop,"Carved staff collar",new Vector3(0,.52f+i*.055f,0),new Vector3(.055f,.014f,.055f),Gold);
                for(int side=-1;side<=1;side+=2){Beam(prop,"Carved orb root",new Vector3(0,.66f,0),new Vector3(side*.095f,.73f,0),.022f,Wood);Beam(prop,"Curving orb claw",new Vector3(side*.095f,.73f,0),new Vector3(side*.10f,.82f,0),.020f,Wood);Beam(prop,"Carved claw tip",new Vector3(side*.10f,.82f,0),new Vector3(side*.055f,.86f,0),.015f,Wood);}
                Glow(prop,"Violet staff orb",new Vector3(0,.79f,0),.115f,C("ae74ef"));
            }
            else
            {
                // Closed frame and visible handle, not a floating light patch.
                for(int side=-1;side<=1;side+=2)Beam(prop,"Lantern handle",new Vector3(0,.01f,0),new Vector3(side*.055f,-.10f,0),.012f,C("252333"));
                Box(prop,"Lantern bottom",new Vector3(0,-.29f,0),new Vector3(.16f,.025f,.13f),C("252333"));
                Box(prop,"Lantern top",new Vector3(0,-.12f,0),new Vector3(.16f,.025f,.13f),C("252333"));
                for(int side=-1;side<=1;side+=2)for(int z=-1;z<=1;z+=2)Beam(prop,"Lantern frame",new Vector3(side*.07f,-.12f,z*.055f),new Vector3(side*.07f,-.29f,z*.055f),.015f,C("252333"));
                Box(prop,"Lantern warm glass",new Vector3(0,-.20f,0),new Vector3(.105f,.13f,.075f),Cream);
            }
            Batch(prop);
        }
    }
}
