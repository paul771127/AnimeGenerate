using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        // Existing fitted humanoid templates keep the accepted face/eyes/hair pipeline intact.
        static GameObject CreateDesertResident(string id,Transform parent)
        {
            string basis=id=="雷歐"?"海斗":id=="小沙"?"店員":id=="鐵娘"?"凪":id=="老駝"?"老焰":id=="阿鈴"?"米菈":"公會長";
            var go=CreateResident(basis,parent,false);go.name="Resident "+id;
            var figure=go.transform.Find("Breathing figure");Transform face=null,rightHand=null,leftHand=null;
            foreach(var tf in go.GetComponentsInChildren<Transform>())
            {
                if(tf.gameObject.name=="立體臉")face=tf;
                if(tf.gameObject.name=="Outfit shoulder"){var hand=tf.Find("Outfit elbow").Find("Outfit hand");if(tf.localPosition.x>0)rightHand=hand;else leftHand=hand;}
            }
            var chest=figure.Find("Persona chest");
            var garments=Group(chest,"Regional resident accessories",new Vector3(0,-.85f,0));
            Color cloth=id=="阿鈴"||id=="老駝"?Accent(4):Accent(3);
            if(id=="雷歐")
            {
                // Helmet hugs the fitted hair frame. One intact horn and one visibly broken stump.
                var oldHat=face.Find("Headwear fitted 海斗");if(oldHat!=null){oldHat.gameObject.SetActive(false);Object.Destroy(oldHat.gameObject);}
                var helm=Group(face,"Retired fighter broken horn helmet",new Vector3(0,.10f+AnimeFace.HeadwearDrop(Portraits.PersonaFor(basis)),-.015f));
                Ball(helm,"Bronze helmet shell",Vector3.zero,new Vector3(.35f,.21f,.29f),Gold);
                Cone(helm,"Intact left horn",new Vector3(-.17f,.045f,0),new Vector3(.07f,.16f,.07f),Cream).transform.localRotation=Quaternion.Euler(0,0,35);
                Cyl(helm,"Broken right horn stump",new Vector3(.17f,.045f,0),new Vector3(.07f,.026f,.07f),Cream);
                ResidentBox(garments,"Veteran bronze breastplate",new Vector3(0,1.12f,.145f),new Vector3(.33f,.32f,.045f),Gold);
                Batch(helm);
            }
            else if(id=="小沙")
            {
                var book=Group(rightHand,"Ticket seller grip",new Vector3(0,-.04f,.04f));ResidentBox(book,"Ticket ledger",Vector3.zero,new Vector3(.14f,.18f,.035f),cloth);
                for(int i=0;i<3;i++)Box(book,"Perforated ticket stubs",new Vector3(-.04f+i*.04f,.03f,.025f),new Vector3(.028f,.08f,.009f),Cream);Batch(book);
            }
            else if(id=="鐵娘")
            {
                ResidentBox(garments,"Armour smith leather apron",new Vector3(0,1.04f,.16f),new Vector3(.28f,.46f,.035f),Wood);
                var hammer=Group(rightHand,"Armour smith hammer",new Vector3(0,-.025f,.04f));Beam(hammer,"Hammer handle",new Vector3(0,-.12f,0),new Vector3(0,.15f,0),.03f,Wood);ResidentBox(hammer,"Riveting hammer head",new Vector3(0,.15f,0),new Vector3(.18f,.09f,.10f),Ink);Batch(hammer);
            }
            else if(id=="阿鈴")
            {
                for(int i=-1;i<=1;i++)Part(garments,"Caravan daughter's bells",Shape("bell"),new Vector3(i*.06f,1.15f,.18f),Vector3.one*.065f,Gold);
                ResidentBox(garments,"Caravan scarf",new Vector3(.08f,1.30f,.10f),new Vector3(.15f,.22f,.06f),cloth);
            }
            else if(id=="老駝")
            {
                ResidentBox(garments,"Station keeper indigo shawl",new Vector3(0,1.32f,-.08f),new Vector3(.36f,.19f,.10f),cloth);
                Part(garments,"Station key bell",Shape("bell"),new Vector3(.13f,.88f,.16f),Vector3.one*.10f,Gold);
            }
            else
            {
                var scroll=Group(leftHand,"Storyteller scroll grip",new Vector3(0,-.02f,.06f));ResidentBox(scroll,"Story scroll parchment",Vector3.zero,new Vector3(.12f,.22f,.025f),Cream);
                for(int side=-1;side<=1;side+=2)Cyl(scroll,"Scroll wooden roller",new Vector3(0,side*.11f,0),new Vector3(.035f,.09f,.035f),Wood).transform.localRotation=Quaternion.Euler(0,0,90);Batch(scroll);
            }
            Batch(garments);var motion=figure.GetComponent<HumanResidentMotion>();motion.SetIdleSet(id);ResidentActionProps(id,figure);motion.BindActionArt();return go;
        }
    }
}
