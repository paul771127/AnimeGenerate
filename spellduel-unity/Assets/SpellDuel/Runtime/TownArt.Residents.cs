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
            bool broad=key=="鐵匠";float shoulder=broad?.57f:.44f;
            for(int s=-1;s<=1;s+=2)
            {
                Ball(p,"Boot",new Vector3(s*.13f,.10f,.055f),new Vector3(.19f,.20f,.32f),C("725443"));
                Cyl(p,"Trouser leg",new Vector3(s*.13f,.37f,0),new Vector3(.16f,.23f,.17f),Ink);
                var sleeve=Ball(p,"Sleeve",new Vector3(s*(shoulder*.5f+.035f),1.03f,0),new Vector3(.20f,.43f,.24f),cloth);sleeve.transform.localRotation=Quaternion.Euler(0,0,s*9);
                Ball(p,"Hand",new Vector3(s*(shoulder*.5f+.06f),.79f,.035f),new Vector3(.13f,.18f,.15f),skin);
            }
            Ball(p,"Tunic shoulders",new Vector3(0,1.04f,0),new Vector3(shoulder,.50f,.29f),cloth);
            Cone(p,"Tunic skirt",new Vector3(0,.56f,0),new Vector3(.51f,.42f,.35f),cloth);
            Box(p,"Waist sash",new Vector3(0,.77f,.01f),new Vector3(.42f,.09f,.33f),Gold);
            Cyl(p,"Neck",new Vector3(0,1.27f,0),new Vector3(.14f,.10f,.14f),skin);
            // Back hair first; sculpted jaw and cheeks keep the face clear of the hair cap.
            Ball(p,"Back hair",new Vector3(0,1.53f,-.045f),new Vector3(.40f,.43f,.34f),hair);
            Ball(p,"Sculpted face",new Vector3(0,1.48f,.055f),new Vector3((child?.36f:.32f)*persona.Width,.38f*persona.Length,.30f),skin);
            Ball(p,"Chin",new Vector3(0,1.35f,.065f),new Vector3(.23f*persona.Jaw,.14f,.23f),skin);
            Ball(p,"Hair crown",new Vector3(0,1.68f,-.015f),new Vector3(.38f*persona.Width,.16f,.33f),hair);
            for(int s=-1;s<=1;s+=2)
            {
                Ball(p,"Ear",new Vector3(s*.17f,1.49f,.02f),new Vector3(.075f,.12f,.08f),skin);
                if(elf){var ear=Cone(p,"Elven ear",new Vector3(s*.17f,1.48f,0),new Vector3(.10f,.20f,.07f),skin);ear.transform.localRotation=Quaternion.Euler(0,0,-s*58);}
                float ex=s*.077f*persona.Width,ey=1.51f,eh=.05f*persona.Eye;
                if(key=="阿潮")eh*=.85f;
                var eye=Group(p,"Personality eye",new Vector3(ex,ey,.194f));eye.localRotation=Quaternion.Euler(0,0,s*persona.Tilt);
                Ball(eye,"Eye contour",Vector3.zero,new Vector3(.105f,eh,.018f),Ink);
                Ball(eye,"Eye white",new Vector3(0,-.003f,.012f),new Vector3(.088f,eh*.72f,.009f),Cream);
                float gx=persona.Gaze*.04f,gy=-persona.Pitch*.00045f;
                Ball(eye,"Iris",new Vector3(gx,gy,.022f),new Vector3(.037f,eh*.68f,.014f),iris);
                Ball(eye,"Pupil",new Vector3(gx,gy,.031f),new Vector3(.016f,eh*.55f,.008f),Ink);
                Ball(eye,"Eye catchlight",new Vector3(gx-.009f,gy+eh*.2f,.036f),Vector3.one*(child?.017f:.011f),Cream);
                float by=1.56f+(s>0?persona.Asym*.0007f:0);
                Beam(p,"Inner brow",new Vector3(s*.04f,by,.198f),new Vector3(s*.08f,by+.006f+persona.Brow*.0004f,.195f),elder?.019f:.014f,hair);
                Beam(p,"Outer brow",new Vector3(s*.08f,by+.006f+persona.Brow*.0004f,.195f),new Vector3(s*.14f,by+persona.Brow*.001f,.18f),elder?.018f:.012f,hair);
                var fringe=Cone(p,"Fringe lock",new Vector3(s*.10f,1.58f,.14f),new Vector3(.16f,.17f,.08f),hair);fringe.transform.localRotation=Quaternion.Euler(0,0,180+s*16);
            }
            Ball(p,"Nose bridge",new Vector3(0,1.454f,.204f),new Vector3(.045f,.074f,.052f),skin);
            if(persona.Open>0)
            {
                Ball(p,"Open expression",new Vector3(0,1.385f,.224f),new Vector3(.080f*(1+Mathf.Max(0,persona.Smile)*.8f),.055f*persona.Open,.014f),Ink);
                Box(p,"Smile teeth",new Vector3(0,1.392f,.234f),new Vector3(.062f*(1+Mathf.Max(0,persona.Smile)*.8f),.013f,.008f),Cream);
            }
            else
            {
                Beam(p,"Smile left",new Vector3(-.038f,1.381f+persona.Smile*.016f,.217f),new Vector3(0,1.381f,.225f),.009f,C("ac6c56"));
                Beam(p,"Smile right",new Vector3(0,1.381f,.225f),new Vector3(.038f,1.381f+persona.Smile*.016f+persona.Asym*.0005f,.217f),.009f,C("ac6c56"));
            }
            if(elder)for(int s=-1;s<=1;s+=2)Beam(p,"Eye crease",new Vector3(s*.12f,1.47f,.185f),new Vector3(s*.145f,1.46f,.172f),.008f,C("ac6c56"));
            // Contrasting folded collar and a small clasp, visible from walking distance.
            for(int s=-1;s<=1;s+=2)Beam(p,"Collar",new Vector3(s*.15f,1.22f,.125f),new Vector3(0,1.10f,.16f),.04f,Cream);
            Ball(p,"Clasp",new Vector3(0,1.10f,.18f),Vector3.one*.06f,Gold);
            switch(key)
            {
                case "老焰":
                    Beard(p,hair);Coat(p,cloth,.78f);BellPin(p,new Vector3(.12f,1.13f,.2f),.09f);
                    Beam(p,"Walking staff",new Vector3(-.33f,.04f,.11f),new Vector3(-.33f,1.16f,.11f),.045f,Wood);break;
                case "小鐵":
                    Apron(p,C("543c35"));Box(p,"Red headband",new Vector3(0,1.66f,.11f),new Vector3(.38f,.045f,.18f),C("c34e3b"));
                    for(int s=-1;s<=1;s+=2){Ball(p,"Brass goggles",new Vector3(s*.087f,1.68f,.2f),new Vector3(.15f,.11f,.07f),Gold);Ball(p,"Goggle glass",new Vector3(s*.087f,1.68f,.24f),new Vector3(.102f,.069f,.02f),C("75baca"));Cone(p,"Spiky hair",new Vector3(s*.14f,1.71f,0),new Vector3(.14f,.18f,.14f),hair);}
                    Box(p,"Soot cheek",new Vector3(-.12f,1.43f,.18f),new Vector3(.04f,.014f,.01f),C("6c6056"));break;
                case "米菈":
                    LongHair(p,hair,1.25f);Cyl(p,"Ochre hat crown",new Vector3(0,1.78f,0),new Vector3(.37f,.13f,.36f),C("e6b961"));
                    Cyl(p,"Wide hat brim",new Vector3(0,1.69f,0),new Vector3(.68f,.025f,.62f),C("bd8b43"));
                    Cyl(p,"Teal hat band",new Vector3(0,1.72f,0),new Vector3(.38f,.025f,.37f),cloth);
                    Box(p,"Merchant backpack",new Vector3(0,1.03f,-.26f),new Vector3(.44f,.57f,.27f),C("8d7251"));
                    var roll=Cyl(p,"Rolled blanket",new Vector3(0,1.36f,-.26f),new Vector3(.18f,.28f,.18f),Cream);roll.transform.localRotation=Quaternion.Euler(0,0,90);
                    Beam(p,"Crossbody strap",new Vector3(-.18f,1.22f,.15f),new Vector3(.17f,.78f,.20f),.035f,Gold);Earring(p,Gold);break;
                case "翠":
                    LongHair(p,hair,1.05f);Coat(p,cloth,.68f);
                    for(int s=-1;s<=1;s+=2){Leaf(p,new Vector3(s*.09f,1.71f,.16f),.14f,C("63a96b"),-s*30);Leaf(p,new Vector3(s*.16f,1.18f,.14f),.17f,C("68a25c"),-s*40);}
                    Ball(p,"Forehead gem",new Vector3(0,1.69f,.19f),Vector3.one*.064f,C("80ce82"));break;
                case "楓":
                    // Hood encloses the back and sides but leaves the face unobscured.
                    Ball(p,"Forest hood",new Vector3(0,1.57f,-.10f),new Vector3(.49f,.48f,.31f),cloth);
                    for(int s=-1;s<=1;s+=2)Beam(p,"Hood opening",new Vector3(0,1.8f,.05f),new Vector3(s*.22f,1.43f,.05f),.085f,cloth);
                    Coat(p,cloth,.67f);Box(p,"Leaf lamp frame",new Vector3(-.3f,.76f,.13f),new Vector3(.14f,.20f,.12f),Gold);
                    Box(p,"Leaf lamp paper",new Vector3(-.3f,.76f,.20f),new Vector3(.10f,.14f,.02f),C("d9ee87"));Leaf(p,new Vector3(-.3f,.9f,.14f),.1f,C("7ebf78"),15);
                    Beam(p,"Cheek scar",new Vector3(.12f,1.46f,.185f),new Vector3(.14f,1.42f,.17f),.008f,C("ac6c56"));break;
                case "小葉":
                    for(int s=-1;s<=1;s+=2){Ball(p,"Sage pigtail",new Vector3(s*.23f,1.55f,-.04f),new Vector3(.15f,.19f,.17f),hair);Ball(p,"Rosy cheek",new Vector3(s*.12f,1.445f,.184f),new Vector3(.045f,.024f,.01f),C("e99786"));}
                    Leaf(p,new Vector3(.12f,1.71f,.14f),.16f,C("b3d96e"),-25);
                    Box(p,"Story tablet",new Vector3(.22f,.82f,.17f),new Vector3(.16f,.24f,.045f),C("ac7950"));Beam(p,"Tablet name",new Vector3(.22f,.76f,.20f),new Vector3(.22f,.88f,.20f),.014f,Cream);break;
                case "海斗":
                    Coat(p,cloth,.70f);Cyl(p,"Captain cap",new Vector3(0,1.78f,0),new Vector3(.43f,.09f,.38f),C("e4e9dd"));
                    Cyl(p,"Navy cap band",new Vector3(0,1.71f,0),new Vector3(.44f,.025f,.39f),hair);
                    Ball(p,"Captain visor",new Vector3(0,1.69f,.14f),new Vector3(.43f,.06f,.26f),hair);Ball(p,"Cap badge",new Vector3(0,1.78f,.20f),Vector3.one*.07f,Gold);
                    for(int s=-1;s<=1;s+=2)Beam(p,"High coat lapel",new Vector3(s*.21f,1.27f,.09f),new Vector3(s*.08f,.99f,.18f),.07f,hair);
                    Beam(p,"Anchor shank",new Vector3(0,1.13f,.20f),new Vector3(0,1.01f,.20f),.02f,Gold);Box(p,"Anchor flukes",new Vector3(0,1.01f,.20f),new Vector3(.10f,.02f,.02f),Gold);break;
                case "凪":
                    Apron(p,Cream);Ball(p,"Plum hair bun",new Vector3(.13f,1.76f,-.07f),Vector3.one*.20f,hair);
                    Beam(p,"Golden hairpin",new Vector3(.05f,1.76f,0),new Vector3(.26f,1.88f,0),.02f,Gold);Earring(p,Gold);break;
                case "阿潮":
                    Beard(p,hair);Cone(p,"Woven straw hat",new Vector3(0,1.69f,0),new Vector3(.76f,.26f,.72f),C("c9a864"));
                    for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Beam(p,"Straw ribs",new Vector3(0,1.96f,0),new Vector3(Mathf.Cos(a)*.37f,1.70f,Mathf.Sin(a)*.35f),.012f,C("8c6b39"));}
                    Beam(p,"Fishing rod",new Vector3(-.31f,.1f,.04f),new Vector3(-.38f,1.9f,.04f),.023f,Wood);Beam(p,"Fishing line",new Vector3(-.38f,1.9f,.04f),new Vector3(-.39f,.92f,.04f),.005f,Cream);break;
                case "鐵匠":
                    Apron(p,C("795341"));Beard(p,hair);
                    Beam(p,"Smith hammer grip",new Vector3(.31f,.70f,.08f),new Vector3(.31f,1.07f,.08f),.05f,Wood);
                    Box(p,"Smith hammer head",new Vector3(.31f,1.10f,.08f),new Vector3(.25f,.14f,.16f),C("86a2ad"));break;
                case "店員":
                    Apron(p,Cream);LongHair(p,hair,1.35f);Ball(p,"Shop hair bun",new Vector3(0,1.65f,-.22f),Vector3.one*.17f,hair);
                    Box(p,"Medicine satchel",new Vector3(-.23f,.78f,.04f),new Vector3(.17f,.23f,.17f),Gold);Bottle(p,new Vector3(-.29f,.82f,.15f),C("e67985"),.17f);
                    Leaf(p,new Vector3(.13f,1.69f,.12f),.13f,C("a9c66b"),-30);break;
                case "公會長":
                    Coat(p,cloth,.55f);Box(p,"Silver dark streak",new Vector3(.11f,1.69f,.12f),new Vector3(.065f,.11f,.1f),C("514452"));
                    for(int s=-1;s<=1;s+=2){Ball(p,"Golden epaulette",new Vector3(s*.23f,1.21f,-.015f),new Vector3(.23f,.08f,.32f),Gold);}
                    for(int i=-1;i<=1;i++)BellPin(p,new Vector3(i*.08f,1.13f,.20f),.065f);
                    Beam(p,"Guildmaster scar",new Vector3(.11f,1.46f,.19f),new Vector3(.14f,1.41f,.17f),.009f,C("a96861"));break;
            }
            // Bake a posed neck and rib cage into the merged meshes; feet remain planted.
            var neck=Group(p,"Persona neck",new Vector3(0,1.3f,0));
            var chest=Group(p,"Persona chest",new Vector3(0,.85f,0));
            var pieces=new System.Collections.Generic.List<Transform>();
            for(int i=0;i<p.childCount;i++)pieces.Add(p.GetChild(i));
            foreach(var tf in pieces)
            {
                if(tf==neck||tf==chest)continue;
                var pos=tf.localPosition;
                bool held=tf.gameObject.name.Contains("staff")||tf.gameObject.name.Contains("rod")||tf.gameObject.name.Contains("line")||tf.gameObject.name.Contains("blanket");
                if(pos.y>=1.32f&&!held){tf.SetParent(neck,false);tf.localPosition=pos-new Vector3(0,1.3f,0);}
                else if(pos.y>.66f){tf.SetParent(chest,false);tf.localPosition=pos-new Vector3(0,.85f,0);}
            }
            neck.SetParent(chest,false);neck.localPosition=new Vector3(0,.45f,0);
            neck.localRotation=Quaternion.Euler(persona.Pitch,persona.Yaw,persona.Roll);
            chest.localRotation=Quaternion.Euler(persona.Lean,0,persona.Roll*.12f);
            Batch(p);p.gameObject.AddComponent<TownArtMotion>().breathe=.008f;return root.gameObject;
        }
        static void LongHair(Transform p,Color hair,float bottom)
        {
            for(int s=-1;s<=1;s+=2)Ball(p,"Long side lock",new Vector3(s*.177f,(1.62f+bottom)*.5f,-.04f),new Vector3(.12f,1.68f-bottom,.19f),hair);
        }
        static void Beard(Transform p,Color hair)
        {
            var beard=Cone(p,"Pointed beard",new Vector3(0,1.41f,.13f),new Vector3(.28f,.27f,.18f),hair);beard.transform.localRotation=Quaternion.Euler(0,0,180);
            for(int s=-1;s<=1;s+=2)Ball(p,"Moustache",new Vector3(s*.045f,1.415f,.215f),new Vector3(.10f,.045f,.035f),hair);
        }
        static void Apron(Transform p,Color color)
        {
            Box(p,"Apron bib",new Vector3(0,1.00f,.164f),new Vector3(.28f,.29f,.035f),color);
            Box(p,"Apron skirt",new Vector3(0,.67f,.18f),new Vector3(.38f,.39f,.04f),color);
            for(int s=-1;s<=1;s+=2)Beam(p,"Apron strap",new Vector3(s*.14f,1.19f,.12f),new Vector3(s*.11f,.96f,.19f),.025f,Gold);
            Box(p,"Apron pocket",new Vector3(.05f,.72f,.209f),new Vector3(.15f,.11f,.018f),Mix(color,Gold,.25f));
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
