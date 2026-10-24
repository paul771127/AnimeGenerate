using UnityEngine;
namespace SpellDuel
{
    internal static class FacilityPeople
    {
        internal static bool Has(string id)=>id=="嚮導"||id=="教官"||id=="懸賞官"||id=="播報員"||id=="塔守";
        internal static Color Skin(string id)=>AnimeFace.SkinFor(id);
    }
    public static partial class TownArt
    {
        static Mesh facilitySpectacles,facilityTrumpet;
        static Transform FacilityHand(Transform figure,bool left)
        {
            foreach(var t in figure.GetComponentsInChildren<Transform>())if(t.gameObject.name=="Outfit shoulder"&&(t.localPosition.x<0)==left)return t.Find("Outfit elbow").Find("Outfit hand");return figure;
        }
        static void FacilityResidentProps(string id,Transform p,Transform face)
        {
            if(!FacilityPeople.Has(id))return;
            var gear=Group(p,"Facility resident equipment",Vector3.zero);
            if(id=="嚮導")
            {
                ResidentBox(gear,"Expedition leather backpack",new Vector3(0,1.06f,-.24f),new Vector3(.40f,.46f,.22f),C("9b673a"));
                var rope=Group(gear,"Backpack rope coil",new Vector3(.19f,1.10f,-.38f));rope.localRotation=Quaternion.Euler(90,0,0);for(int i=0;i<3;i++)Ring(rope,"Coiled climbing rope",.09f+i*.023f,.015f,0,Cream,false,14);
                for(int side=-1;side<=1;side+=2)Beam(gear,"Leather pack harness",new Vector3(side*.11f,1.36f,.08f),new Vector3(side*.12f,.91f,.14f),.023f,C("9b673a"));
                AnimeFace.Detail(face,id,Portraits.PersonaFor(id),new Vector2(.075f,-.010f),new Vector2(.102f,-.048f),.002f,C("a86d59"));
                var lamp=Group(FacilityHand(p,true),"Guide hand lantern",new Vector3(0,-.04f,.045f));
                for(int side=-1;side<=1;side+=2){Beam(lamp,"Lantern handle",Vector3.zero,new Vector3(side*.06f,-.09f,0),.012f,Gold);for(int z=-1;z<=1;z+=2)Beam(lamp,"Lantern brass cage",new Vector3(side*.06f,-.09f,z*.05f),new Vector3(side*.06f,-.26f,z*.05f),.012f,Gold);}
                Box(lamp,"Lantern cap",new Vector3(0,-.09f,0),new Vector3(.15f,.025f,.13f),Wood);Box(lamp,"Lantern base",new Vector3(0,-.26f,0),new Vector3(.15f,.025f,.13f),Wood);Glow(lamp,"Lantern warm glass",new Vector3(0,-.17f,0),.12f,C("ffe6a0"));Batch(lamp);
            }
            else if(id=="教官")
            {
                for(int side=-1;side<=1;side+=2)Beam(gear,"Whistle lanyard",new Vector3(side*.07f,1.40f,.07f),new Vector3(.025f,1.18f,.145f),.008f,Cream);
                Box(gear,"Brass training whistle",new Vector3(.025f,1.17f,.16f),new Vector3(.04f,.035f,.065f),Gold);
                var sword=Group(FacilityHand(p,false),"Instructor wooden sword",new Vector3(0,-.025f,0));Beam(sword,"Sword wooden grip",new Vector3(0,0,-.07f),new Vector3(0,0,.08f),.035f,Wood);Beam(sword,"Wooden crossguard",new Vector3(-.10f,0,.08f),new Vector3(.10f,0,.08f),.035f,Gold);Beam(sword,"Blunt practice blade",new Vector3(0,0,.08f),new Vector3(0,0,.60f),.055f,C("b98b52"));Batch(sword);
            }
            else if(id=="懸賞官")
            {
                ResidentBox(gear,"Clerk document satchel",new Vector3(.20f,.91f,.17f),new Vector3(.24f,.20f,.065f),C("8c573c"));Beam(gear,"Satchel diagonal strap",new Vector3(-.13f,1.37f,.09f),new Vector3(.22f,.90f,.20f),.025f,C("8c573c"));
                var glasses=new OutfitArt.Builder();var persona=Portraits.PersonaFor(id);var plan=new Portraits.FacePlan(persona);
                for(int side=-1;side<=1;side+=2)for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;float x=side*plan.EyeX+Mathf.Cos(a)*.042f,y=plan.EyeY+Mathf.Sin(a)*.030f,xx=side*plan.EyeX+Mathf.Cos(b)*.042f,yy=plan.EyeY+Mathf.Sin(b)*.030f;glasses.Seam(new Vector3(x,y,AnimeFace.Surface(x,y,persona)+.006f),new Vector3(xx,yy,AnimeFace.Surface(xx,yy,persona)+.006f),.0025f,Gold);}
                glasses.Seam(new Vector3(-.014f,plan.EyeY,AnimeFace.Surface(-.014f,plan.EyeY,persona)+.006f),new Vector3(.014f,plan.EyeY,AnimeFace.Surface(.014f,plan.EyeY,persona)+.006f),.002f,Gold);if(facilitySpectacles==null)facilitySpectacles=glasses.Finish("Cached clerk spectacles");AnimeFace.Part(face,"Clerk round spectacles",facilitySpectacles,Vector3.zero,AnimeFace.Material(Color.white,3));
                var scroll=Group(FacilityHand(p,true),"Clerk reward scroll",new Vector3(0,-.06f,.045f));Box(scroll,"Unrolled parchment",new Vector3(0,-.05f,.025f),new Vector3(.17f,.22f,.015f),Cream);for(int side=-1;side<=1;side+=2){var roll=Cyl(scroll,"Scroll rolled end",new Vector3(0,-.05f+side*.12f,.025f),new Vector3(.03f,.11f,.03f),Gold);roll.transform.localRotation=Quaternion.Euler(0,0,90);}for(int i=0;i<3;i++)Box(scroll,"Scroll ink",new Vector3(0,.015f-i*.045f,.04f),new Vector3(.11f,.008f,.008f),Ink);Batch(scroll);
                var pen=Group(FacilityHand(p,false),"Clerk quill",new Vector3(0,-.02f,.05f));Beam(pen,"Quill shaft",Vector3.zero,new Vector3(.02f,.22f,.035f),.010f,Gold);Leaf(pen,new Vector3(.02f,.17f,.04f),.13f,C("b7aad9"),-12);Batch(pen);
            }
            else if(id=="播報員")
            {
                var horn=Group(FacilityHand(p,false),"Announcer megaphone",new Vector3(0,-.02f,.04f));var shell=new OutfitArt.Builder();shell.Loft(new[]{0f,.12f,.25f},new[]{.025f,.06f,.115f},new[]{.025f,.06f,.115f},Gold,0,0,12);shell.Loft(new[]{.254f,.23f},new[]{.11f,.09f},new[]{.11f,.09f},Ink,0,0,12);if(facilityTrumpet==null)facilityTrumpet=shell.Finish("Announcer brass speaking trumpet");var mesh=facilityTrumpet;var go=Part(horn,"Speaking trumpet",mesh,Vector3.zero,Vector3.one,Color.white);go.GetComponent<MeshRenderer>().sharedMaterial=AnimeFace.Material(Color.white,4);go.transform.localRotation=Quaternion.Euler(90,0,0);Batch(horn);
                var flag=Group(FacilityHand(p,true),"Announcer signal flag",new Vector3(0,-.025f,.03f));Beam(flag,"Flag staff",new Vector3(0,-.06f,0),new Vector3(0,.40f,.03f),.017f,Wood);Part(flag,"Signal pennant",Shape("roof"),new Vector3(.08f,.25f,.03f),new Vector3(.22f,.14f,.018f),C("36c8cb"));Batch(flag);
            }
            Batch(gear);
        }
    }
}
