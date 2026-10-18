using UnityEngine;
namespace SpellDuel
{
    public static partial class TownArt
    {
        /// <summary>Named villagers share portrait palettes and identifying accessories. +z is the face.</summary>
        public static GameObject CreateNpc(string name,Transform parent)
        {
            string key=Portraits.Key(name);var persona=Portraits.PersonaFor(key);var root=Group(parent,"Resident "+key,Vector3.zero);
            var p=Group(root,"Breathing figure",Vector3.zero);
            bool child=key=="小葉",elder=key=="老焰"||key=="翠"||key=="阿潮"||key=="公會長";
            bool elf=key=="翠"||key=="楓"||child;
            Color skin=C("efbba1"),hair=C("623d54"),cloth=C("9c4c64"),iris=C("a17385");
            switch(key)
            {
                case "老焰":skin=C("d7a27a");hair=C("e4d9bd");cloth=C("a64232");iris=C("b9954e");break;
                case "小鐵":skin=C("eeb88e");hair=C("6c3b27");cloth=C("9d5d32");iris=C("e0ac39");break;
                case "米菈":skin=C("dca17e");hair=C("553354");cloth=C("268e9c");iris=C("ac6b3c");break;
                case "翠":skin=C("eccca6");hair=C("e3edd4");cloth=C("376e5c");iris=C("488e66");break;
                case "楓":skin=C("e8b48c");hair=C("a75b35");cloth=C("315e50");iris=C("dbb254");break;
                case "小葉":skin=C("ffdab9");hair=C("527e60");cloth=C("d9aa51");iris=C("76bc78");break;
                case "海斗":skin=C("dba487");hair=C("253e59");cloth=C("365879");iris=C("76b8c5");break;
                case "阿潮":skin=C("ba8768");hair=C("d4d7cb");cloth=C("43787f");iris=C("466b70");break;
                case "鐵匠":skin=C("ce986e");hair=C("795341");cloth=C("b96c49");iris=Gold;break;
                case "店員":skin=C("f1c5a1");hair=C("78533e");cloth=C("589b9b");iris=C("73915c");break;
                case "公會長":skin=C("d2a383");hair=C("d7cebb");cloth=C("743948");iris=C("b09460");break;
            }
            float height=child?1.2f:key=="小鐵"?1.6f:1.75f;
            // Build in adult proportions; scale the inner figure so caller placement remains untouched.
            p.localScale=Vector3.one*(height/1.75f);
            DressResident(p,key,skin);
            Cyl(p,"Neck",new Vector3(0,1.27f,-.035f),new Vector3(.145f*persona.Width,.10f,.12f),skin);
            // 同一套截面頭模與曲面眼片，沿用居民的髮色、眼色與表情。
            var face=Group(p,"立體臉",new Vector3(0,1.48f,.055f));face.localScale=Vector3.one*1.15f;
            var features=AnimeFace.Build(face,key,persona,skin,hair,iris,false,elder,elf);
            // 居民不播放臉部動畫；停用閉眼線的物件，避免靜態合批把隱藏線烘焙成可見。
            foreach(var lid in features.Lids){lid.gameObject.SetActive(false);Object.Destroy(lid.gameObject);}
            AnimeFace.Hair(face,key,persona,hair);HeadwearArt.Build(face,key,persona);
            if(elder)for(int s=-1;s<=1;s+=2)AnimeFace.Detail(face,key,persona,new Vector2(s*.093f,-.006f),new Vector2(s*.110f,-.017f),.002f,C("ac6c56"));
            switch(key)
            {
                case "老焰":
                    Beard(p,hair);BellPin(p,new Vector3(.12f,1.13f,.2f),.09f);
                    Beam(p,"Walking staff",new Vector3(-.33f,.04f,.11f),new Vector3(-.33f,1.16f,.11f),.045f,Wood);break;
                case "小鐵":
                    ResidentBox(p,"Soot cheek",new Vector3(-.12f,1.43f,.18f),new Vector3(.04f,.014f,.01f),C("6c6056"));break;
                case "米菈":
                    ResidentBox(p,"Merchant backpack",new Vector3(0,1.03f,-.26f),new Vector3(.44f,.57f,.27f),C("8d7251"));
                    var roll=Cyl(p,"Rolled blanket",new Vector3(0,1.36f,-.26f),new Vector3(.18f,.28f,.18f),Cream);roll.transform.localRotation=Quaternion.Euler(0,0,90);
                    Beam(p,"Crossbody strap",new Vector3(-.18f,1.22f,.15f),new Vector3(.17f,.78f,.20f),.035f,Gold);Earring(p,Gold);break;
                case "翠":

                    for(int side=-1;side<=1;side+=2)Leaf(p,new Vector3(side*.16f,1.18f,.14f),.17f,C("68a25c"),-side*40);break;
                case "楓":
                    ResidentBox(p,"Leaf lamp frame",new Vector3(-.3f,.76f,.13f),new Vector3(.14f,.20f,.12f),Gold);
                    ResidentBox(p,"Leaf lamp paper",new Vector3(-.3f,.76f,.20f),new Vector3(.10f,.14f,.02f),C("d9ee87"));Leaf(p,new Vector3(-.3f,.9f,.14f),.1f,C("7ebf78"),15);
                    AnimeFace.Detail(face,key,persona,new Vector2(.100f,-.025f),new Vector2(.110f,-.055f),.002f,C("ac6c56"));break;
                case "小葉":
                    for(int s=-1;s<=1;s+=2){Ball(p,"Rosy cheek",new Vector3(s*.12f,1.445f,.184f),new Vector3(.045f,.024f,.01f),C("e99786"));}
                    ResidentBox(p,"Story tablet",new Vector3(.22f,.82f,.17f),new Vector3(.16f,.24f,.045f),C("ac7950"));Beam(p,"Tablet name",new Vector3(.22f,.76f,.20f),new Vector3(.22f,.88f,.20f),.014f,Cream);break;
                case "海斗":
                    Beam(p,"Anchor shank",new Vector3(0,1.13f,.20f),new Vector3(0,1.01f,.20f),.02f,Gold);ResidentBox(p,"Anchor flukes",new Vector3(0,1.01f,.20f),new Vector3(.10f,.02f,.02f),Gold);break;
                case "凪":

                    Earring(p,Gold);break;
                case "阿潮":
                    Beard(p,hair);
                    Beam(p,"Fishing rod",new Vector3(-.31f,.1f,.04f),new Vector3(-.38f,1.9f,.04f),.023f,Wood);Beam(p,"Fishing line",new Vector3(-.38f,1.9f,.04f),new Vector3(-.39f,.92f,.04f),.005f,Cream);break;
                case "鐵匠":
                    Beard(p,hair);
                    Beam(p,"Smith hammer grip",new Vector3(.31f,.70f,.08f),new Vector3(.31f,1.07f,.08f),.05f,Wood);
                    ResidentBox(p,"Smith hammer head",new Vector3(.31f,1.10f,.08f),new Vector3(.25f,.14f,.16f),C("86a2ad"));break;
                case "店員":

                    ResidentBox(p,"Medicine satchel",new Vector3(-.23f,.78f,.04f),new Vector3(.17f,.23f,.17f),Gold);Bottle(p,new Vector3(-.29f,.82f,.15f),C("e67985"),.17f);
                    break;
                case "公會長":
                    for(int i=-1;i<=1;i++)BellPin(p,new Vector3(i*.08f,1.13f,.20f),.065f);
                    AnimeFace.Detail(face,key,persona,new Vector2(.100f,-.025f),new Vector2(.110f,-.061f),.002f,C("a96861"));break;
            }
            // Bake a posed neck and rib cage into the merged meshes; feet remain planted.
            var neck=Group(p,"Persona neck",new Vector3(0,1.3f,0));
            var chest=Group(p,"Persona chest",new Vector3(0,.85f,0));
            var pieces=new System.Collections.Generic.List<Transform>();
            for(int i=0;i<p.childCount;i++)pieces.Add(p.GetChild(i));
            foreach(var tf in pieces)
            {
                if(tf==neck||tf==chest||tf.gameObject.name=="Outfit hip")continue;
                var pos=tf.localPosition;
                bool held=tf.gameObject.name.Contains("staff")||tf.gameObject.name.Contains("rod")||tf.gameObject.name.Contains("line")||tf.gameObject.name.Contains("blanket");
                if(pos.y>=1.32f&&!held){tf.SetParent(neck,false);tf.localPosition=pos-new Vector3(0,1.3f,0);}
                else if(pos.y>.66f){tf.SetParent(chest,false);tf.localPosition=pos-new Vector3(0,.85f,0);}
            }
            neck.SetParent(chest,false);neck.localPosition=new Vector3(0,.45f,0);
            neck.localRotation=Quaternion.Euler(persona.Pitch,persona.Yaw,persona.Roll);
            chest.localRotation=Quaternion.Euler(persona.Lean,0,persona.Roll*.12f);
            // 頭部獨立合批，保留解析陰影所需的頭部座標，不讓轉頭後的光向偏移。
            foreach(var renderer in p.GetComponentsInChildren<MeshRenderer>())
            {
                var material=renderer.sharedMaterial;
                if(material.name!=null&&material.name.StartsWith("Town "))
                    renderer.sharedMaterial=AnimeFace.Material(material.color,ColorUtility.ToHtmlStringRGB(material.color)==ColorUtility.ToHtmlStringRGB(skin)?2:0);
            }
            neck.SetParent(null,false);Batch(p);Batch(neck);neck.SetParent(chest,false);
            p.gameObject.AddComponent<TownArtMotion>().breathe=.008f;return root.gameObject;
        }
        static void DressResident(Transform p,string id,Color skin)
        {
            var torso=Group(p,"Outfit torso",new Vector3(0,.78f,0));torso.localScale=new Vector3(id=="鐵匠"?1.22f:1.08f,.86f,1.12f);OutfitArt.Add(torso,id,"torso",skin);
            for(int side=-1;side<=1;side+=2)
            {
                string suffix=side<0?"L":"R";var hip=Group(p,"Outfit hip",new Vector3(side*.12f,.85f,0));
                OutfitArt.Add(hip,id,"thigh"+suffix,skin);var knee=Group(hip,"Outfit knee",new Vector3(0,-.42f,0));OutfitArt.Add(knee,id,"calf"+suffix,skin);
                var arm=Group(p,"Outfit shoulder",new Vector3(side*(id=="鐵匠"?.29f:.23f),1.205f,.045f));arm.localScale=Vector3.one*.74f;arm.localRotation=Quaternion.Euler(0,0,side*9);
                OutfitArt.Add(arm,id,"upper"+suffix,skin);var elbow=Group(arm,"Outfit elbow",new Vector3(0,-.28f,0));if(id=="小葉"&&side>0)elbow.localRotation=Quaternion.Euler(-25,0,0);OutfitArt.Add(elbow,id,"fore"+suffix,skin);
                var hand=Group(elbow,"Outfit hand",new Vector3(0,-.25f,0));OutfitArt.Add(hand,id,"hand"+((id=="老焰"||id=="阿潮")&&side<0||id=="鐵匠"&&side>0||id=="小葉"&&side>0?"Grip":"Relax")+suffix,skin);
            }
        }
        static readonly System.Collections.Generic.Dictionary<string,Mesh> residentPropCache=new System.Collections.Generic.Dictionary<string,Mesh>();
        static GameObject ResidentBox(Transform p,string name,Vector3 at,Vector3 size,Color color)
        {
            // Bevelled bag/pocket silhouette from lofted rings, not the built-in cube.
            string key=name+"/"+size.x+"/"+size.y+"/"+size.z+"/"+ColorUtility.ToHtmlStringRGB(color);
            if(!residentPropCache.TryGetValue(key,out var mesh))
            {
                var b=new OutfitArt.Builder();float x=size.x*.5f,z=size.z*.5f,y=size.y*.5f;
                b.Loft(new[]{y,y*.78f,-y*.78f,-y},new[]{x*.80f,x,x,x*.80f},new[]{z*.80f,z,z,z*.80f},color);
                mesh=b.Finish("Resident prop "+name);residentPropCache[key]=mesh;
            }
            return AnimeFace.Part(p,name,mesh,at,AnimeFace.Material(Color.white,4));
        }
        static void Beard(Transform p,Color hair)
        {
            // 鬍鬚避開新的後收嘴周，保留老人的柔笑與鐵匠張嘴大笑。
            var beard=Cone(p,"Pointed beard",new Vector3(0,1.335f,.115f),new Vector3(.26f,.22f,.17f),hair);beard.transform.localRotation=Quaternion.Euler(0,0,180);
            for(int s=-1;s<=1;s+=2)Ball(p,"Moustache",new Vector3(s*.060f,1.385f,.193f),new Vector3(.10f,.045f,.035f),hair);
        }
        static void Apron(Transform p,Color color)
        {
            ResidentBox(p,"Apron bib",new Vector3(0,1.00f,.164f),new Vector3(.28f,.29f,.035f),color);
            ResidentBox(p,"Apron skirt",new Vector3(0,.67f,.18f),new Vector3(.38f,.39f,.04f),color);
            for(int s=-1;s<=1;s+=2)Beam(p,"Apron strap",new Vector3(s*.14f,1.19f,.12f),new Vector3(s*.11f,.96f,.19f),.025f,Gold);
            ResidentBox(p,"Apron pocket",new Vector3(.05f,.72f,.209f),new Vector3(.15f,.11f,.018f),Mix(color,Gold,.25f));
        }
        static void Coat(Transform p,Color color,float bottom)
        {
            Ball(p,"Mantle back",new Vector3(0,(1.22f+bottom)*.5f,-.12f),new Vector3(.53f,1.35f-bottom,.20f),color);
            for(int s=-1;s<=1;s+=2)Beam(p,"Coat gold seam",new Vector3(s*.22f,1.15f,-.08f),new Vector3(s*.22f,bottom,-.08f),.018f,Gold);
        }
        static void Earring(Transform p,Color color)
        {
            Ball(p,"Golden earring",new Vector3(.185f,1.42f,.10f),new Vector3(.035f,.065f,.02f),color);
        }
        static void BellPin(Transform p,Vector3 at,float size)
        {
            Part(p,"Bell clasp",Shape("bell"),at,Vector3.one*size,Gold);
        }
    }
}
